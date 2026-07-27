using GcodeViewer.Models;
using GcodeViewer.Parsing;
using GcodeViewer.Rendering;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace GcodeViewer.Forms;

/// <summary>
/// 路径生成窗体（自 AMCP.FrmPrintStep2 的 Path2Gcode 流程移植）。
/// 在原"加载→简化→导出CSV→查看"基础上，复刻原界面工艺参数控件：
///   打印速度、提前出丝距离、切换变速、T0/T1 变速·变压参数、点间距、生成G-code、文件名显示。
/// 参数实时生效：打印速度写入点 Feed 与 G-code 的 V；变速开关决定按层提前切换或直接 G1 序列。
/// 控件/布局/事件见 PathGeneratorForm.Designer.cs（可在 VS 设计器可视化编辑）；内嵌已有 Viewport3D 直接查看。
/// </summary>
public sealed partial class PathGeneratorForm : Form
{
    private readonly MainForm _owner;
    // 数据
    private string _sourceFile = "";                   // 当前加载的 G-code 文件名（含路径）
    private List<Point3D> _rawPoints = new();         // 真正原始点（未映射，对应 AMCP originalPoints）
    private List<Point3D> _originalPoints = new();    // 映射后点（对应 AMCP assignedPoints；无体素时同 _rawPoints）
    private List<Point3D> _simplifiedPoints = new();
    private VoxelMapper.VoxelData? _voxelData;          // 体素数据（体素映射用）—— 旋转后副本，喂映射与预览
    private VoxelMapper.StlMesh? _stlMesh;              // STL 模型（SDF 映射 + 3D 线框显示用）—— 旋转后副本
    private VoxelMapper.VoxelData? _voxelDataOriginal;  // 体素原始解析(旋转基准，导入后不变)
    private VoxelMapper.StlMesh? _stlMeshOriginal;      // STL 原始解析(旋转基准，导入后不变)
    private int _sourceRotXSteps;                       // 源绕 X 轴累积旋转步数(×90°，mod 4)；导入时重置 0
    private List<List<Pixcel>>? _pixelMatrix;           // 图片二值矩阵（Picture2Gcode 用）
    private System.Drawing.Bitmap? _bitmap;             // 已加载图片
    private int _imgWidth = 100, _imgHeight = 100;       // 图片缩放目标尺寸
    private List<Point3D> _generatedPoints = new();      // 生成的栅格路径点（体素/图片，Tool=材料值）

    // ---- 控件字段声明见 PathGeneratorForm.Designer.cs（设计器分部文件，VS 设计器里可视化编辑）----

    public PathGeneratorForm(MainForm owner)
    {
        _owner = owner;
        // 控件、布局与事件均由 InitializeComponent（Designer）创建与绑定
        InitializeComponent();
        EnableActions(false);
        UpdateSwitchGroupEnabled();
    }

    // ====================== 事件处理（事件已在 Designer 中以 += new EventHandler 标准形式绑定）=====================
    private void OnImgWidthChanged(object? sender, EventArgs e) => _imgWidth = (int)_numImgWidth.Value;
    private void OnImgHeightChanged(object? sender, EventArgs e) => _imgHeight = (int)_numImgHeight.Value;
    private void OnTolChanged(object? sender, EventArgs e) => Resimplify();
    private void OnResimplify(object? sender, EventArgs e) => Resimplify();// 手动点击"重新简化"按钮
    private void OnVelochangeChanged(object? sender, EventArgs e) => UpdateSwitchGroupEnabled();
    //控件enable管理
    private void EnableActions(bool enabled) =>
       _btnExport.Enabled = _btnSendToMain.Enabled = _btnEvaluateAccuracy.Enabled = enabled;

    /// <summary>切换参数组仅在勾选"切换变速"时可用（与原界面行为一致）。</summary>
    private void UpdateSwitchGroupEnabled()
    {
        foreach (Control c in _grpSwitch.Controls) c.Enabled = _chkVelochange.Checked;
        _grpSwitch.Enabled = _chkVelochange.Checked;  // 禁用整组灰显标题
    }

    // ====================== 加载 G-code ======================
    private void OnLoad(object? sender, EventArgs e)
    {
        using OpenFileDialog dlg = new OpenFileDialog
        {
            Filter = "G-code|*.gcode;*.g;*.nc;*.tap|所有文件|*.*",
            InitialDirectory = File.Exists(_sourceFile) ? Path.GetDirectoryName(_sourceFile)! : "",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            _sourceFile = dlg.FileName;

            // 1. 解析 G-code，提取原始点集
            List<Point3D> rawPoints = PathGenerator.GetOriginalGcode(_sourceFile);
            if (rawPoints.Count == 0)
            {
                MessageBox.Show(this, "未能从文件中解析出任何 G0/G1 路径点。", "提示");
                return;
            }
            _rawPoints = rawPoints;

            // 2. 按当前选择的映射方法赋材料(Tool)值，并简化、刷新 3D 预览
            ReassignAndResimplify();

            UpdateSrcStatus($"已加载：{Path.GetFileName(_sourceFile)}（{_rawPoints.Count} 点 → 简化 {_simplifiedPoints.Count} 点）");

            //导出中间文件
            //List<string> points = new List<string>();
            //for(int i = 0; i < _originalPoints.Count; i++)
            //{
            //    Point3D p = _originalPoints[i];
            //    points.Add($"{p.X:F3},{p.Y:F3},{p.Z:F3},{p.Extrude},{p.Tool}");
            //}
            //File.WriteAllLines("generated_points.csv", points);

        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "加载失败：" + ex.Message, "错误");
        }
    }

    // ====================== 导入图片（Picture2Gcode 输入）======================
    private void OnLoadImage(object? sender, EventArgs e)
    {
        using OpenFileDialog dlg = new OpenFileDialog { Filter = "图像文件|*.png;*.jpg;*.bmp|所有文件|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _bitmap?.Dispose();
            using var raw = new System.Drawing.Bitmap(dlg.FileName);
            //调整图片尺寸以适应目标栅格大小
            _bitmap = ImageProcessor.ResizeImage(raw, _imgWidth, _imgHeight);
            _pixelMatrix = ImageProcessor.ProcessImage(_bitmap);
            UpdateSrcStatus($"图片已加载：{_bitmap.Width}×{_bitmap.Height}" +
                           $"（RLE 段 {_pixelMatrix.Sum(r => r?.Count ?? 0)}）");
        }
        catch (Exception ex) { MessageBox.Show(this, "图片加载失败：" + ex.Message, "错误"); }
    }

    // ====================== 导入体素（Voxel2Gcode 输入 + 体素映射）======================
    private void OnLoadVoxel(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "体素文件|*.csv|所有文件|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _voxelDataOriginal = VoxelMapper.ParseVoxelFile(dlg.FileName);
            _sourceRotXSteps = 0;                 // 新导入重置旋转
            _chkShowVoxel.Checked = true;          // 默认显示刚导入的体素点云
            ShowSourceInPreview();                // 派生(旋转后)体素副本并显示到 3D 预览

            int layers = _voxelData?.Matrix.Count ?? 0;

            // 若已加载 G-code 路径，自动重新应用当前映射方法并重算简化路径（无需用户手动重新加载）
            if (_rawPoints.Count > 0)
            {
                ReassignAndResimplify();
                UpdateSrcStatus($"体素已加载并已重新映射材料：{_voxelData!.ColCount}×{_voxelData.RowCount}×{layers} 层");
            }
            else
            {
                UpdateSrcStatus($"体素已加载：{_voxelData!.ColCount}×{_voxelData.RowCount}×{layers} 层（现加载 G-code 即自动映射）");
            }
        }
        catch (Exception ex) { MessageBox.Show(this, "体素加载失败：" + ex.Message, "错误"); }
    }

    // ====================== 映射方法切换 / STL 导入与显示 ======================

    /// <summary>当前选中的映射方法索引：0=DDA、1=最近邻、2=双线性、3=SDF(STL)。</summary>
    private int MapMethodIndex => _cboMapMethod.SelectedIndex;

    /// <summary>按当前选择的映射方法对原始路径赋材料(Tool)值；无可用输入源时原样返回。</summary>
    private List<Point3D> ApplyMapping(List<Point3D> raw)
    {
        // 每种方法要求各自的输入源；源缺失时退化为不映射(沿用 gcode 内工具号)
        return MapMethodIndex switch
        {
            1 => _voxelData != null ? VoxelMapper.AssignTValuesNearest(raw, _voxelData) : raw,  // 最近邻
            2 => _voxelData != null ? VoxelMapper.AssignTValuesBilinear(raw, _voxelData) : raw, // 双线性
            3 => _stlMesh != null ? VoxelMapper.AssignTValuesSdf(raw, _stlMesh) : raw,          // SDF(STL)
            _ => _voxelData != null ? VoxelMapper.AssignTValues(raw, _voxelData) : raw,         // default = DDA
        };
    }

    /// <summary>重新执行映射 + 简化 + 刷新 3D 预览（切换方法/导入数据后统一调用）。原始路径为空时直接返回。</summary>
    private void ReassignAndResimplify()
    {
        if (_rawPoints.Count == 0) return;
        _originalPoints = ApplyMapping(_rawPoints);// 映射后点集
        Resimplify();
    }

    /// <summary>
    /// 按当前累积旋转 <see cref="_sourceRotXSteps"/> 从原始源派生旋转副本(体素/STL)，分别喂入 3D 预览显示，
    /// 并使 <see cref="ApplyMapping"/> 后续使用旋转后数据(消除"源图案平面 vs 路径平面"轴不匹配)。
    /// 无相应源时跳过该源；映射重算由调用方按需触发 <see cref="ReassignAndResimplify"/>。
    /// </summary>
    private void ShowSourceInPreview()
    {
        if (_voxelDataOriginal != null)
        {
            _voxelData = VoxelMapper.RotateVoxelAroundX(_voxelDataOriginal, _sourceRotXSteps);
            _preview.SetVoxelData(_voxelData);
        }
        if (_stlMeshOriginal != null && _stlMeshOriginal.IsValid)
        {
            _stlMesh = VoxelMapper.RotateStlAroundX(_stlMeshOriginal, _sourceRotXSteps);
            _preview.SetStlMesh(_stlMesh);
        }
    }

    /// <summary>按钮"绕X +90°"：把当前已加载的源绕 X 轴 +90°。</summary>
    private void OnRotXPlus(object? sender, EventArgs e) => RotateSourceX(+1);

    /// <summary>按钮"绕X −90°"：把当前已加载的源绕 X 轴 −90°。</summary>
    private void OnRotXMinus(object? sender, EventArgs e) => RotateSourceX(-1);

    /// <summary>
    /// 把源绕 X 轴旋转 dir×90°(dir=±1)，累加并归一到 0..3，重派生源副本→刷新预览→重映射。
    /// 无任何源加载时提示。旋转同时作用于预览与映射(重定向源以修正轴不匹配)。
    /// </summary>
    private void RotateSourceX(int dir)
    {
        bool noSource = _voxelDataOriginal == null
                        && (_stlMeshOriginal == null || !_stlMeshOriginal.IsValid);
        if (noSource)
        {
            MessageBox.Show(this, "请先导入体素 CSV 或 STL 模型，再进行旋转。", "提示");
            return;
        }
        _sourceRotXSteps = ((_sourceRotXSteps + dir) % 4 + 4) % 4;   // 累加并归一到 0..3
        ShowSourceInPreview();
        ReassignAndResimplify();   // 旋转改变源数据 → 重映射(无路径时内部早退)
        UpdateSrcStatus($"源已绕 X 轴旋转至 {_sourceRotXSteps * 90}°，映射已刷新");
    }

    /// <summary>切换映射方法：重新映射并刷新预览与状态栏。</summary>
    private void OnMapMethodChanged(object? sender, EventArgs e)
    {
        ReassignAndResimplify();// 重新映射 + 简化 + 刷新预览
        UpdateStatus();// 更新状态栏显示
    }

    /// <summary>勾选/取消"显示 STL 线框"。</summary>
    private void OnShowStlChanged(object? sender, EventArgs e)
    {
        _preview.ShowStl = _chkShowStl.Checked;
        _preview.Invalidate();
    }

    /// <summary>勾选/取消"显示体素"（控制体素点云可见性，不影响映射）。</summary>
    private void OnShowVoxelChanged(object? sender, EventArgs e)
    {
        _preview.ShowVoxel = _chkShowVoxel.Checked;
        _preview.Invalidate();
    }

    /// <summary>导入 STL 模型：解析为三角网格，在 3D 预览显示线框，并作为 SDF 映射的实体边界。</summary>
    private void OnLoadStl(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "STL 模型|*.stl|所有文件|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var mesh = VoxelMapper.ParseStlFile(dlg.FileName);
            if (!mesh.IsValid)
            {
                MessageBox.Show(this, "未能从 STL 解析出三角面片（文件为空或格式不符）。", "提示");
                return;
            }
            _stlMeshOriginal = mesh;             // 存原始(旋转基准)
            _sourceRotXSteps = 0;                 // 新导入重置旋转
            _chkShowStl.Checked = true;          // 默认显示刚导入的 STL 线框
            ShowSourceInPreview();                // 派生(旋转后)STL 副本并显示到 3D 预览
            ReassignAndResimplify();             // 若当前为 SDF 方法且已加载路径，立即重映射
            UpdateSrcStatus($"STL 已加载：{_stlMesh!.TriangleCount} 个三角面片（SDF 映射的实体边界，已显示线框）");
        }
        catch (Exception ex) { MessageBox.Show(this, "STL 加载失败：" + ex.Message, "错误"); }
    }

    // ====================== 状态栏显示 ======================
    private void UpdateSrcStatus(string text) => _lblSrcStatus.Text = text;


    // ====================== 重新简化 + 查看 ======================
    private void Resimplify()
    {
        if (_originalPoints.Count == 0) return;

        //对路径进行点集优化
        double tol = (double)_numTol.Value;// 简化阈值 mm
        _simplifiedPoints = PathGenerator.SimplifyPath(_originalPoints, tol);

        // 打印速度写入每点 Feed（A/T0→veloA，B/T1→veloB），使其进入 CSV 与 G-code 的 V
        double feedA = (double)_numPrintVeloA.Value;
        double feedB = (double)_numPrintVeloB.Value;
        foreach (var p in _simplifiedPoints) p.Feed = (p.Tool == 0) ? feedA : feedB;

        ParsedGcode parsed = PathGenerator.ToParsedGcode(_simplifiedPoints, sourcePath: Path.GetFileNameWithoutExtension(_sourceFile) + "_simplified");
        _preview.SetGcode(parsed);// 刷新 3D 预览

        EnableActions(true);
        UpdateStatus();
    }

    // ====================== 导出 CSV（直接生成：提前偏移 + 变速插值 + 密度过滤）======================
    private void OnExport(object? sender, EventArgs e)
    {
        // 若已生成栅格路径（图片/体素），优先导出含材料 Tool 值的生成点集
        //if (_generatedPoints.Count > 0)
        //{
        //    ExportGeneratedCsv();
        //    return;
        //}
        //if (_originalPoints.Count == 0)
        //{
        //    MessageBox.Show(this, "请先加载路径并映射体素数据。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //    return;
        //}

        // 参数：A、B 分别配置（A=T0，B=T1）
        double advancedisA = (double)_numEadvancedisA.Value;
        double advancedisB = (double)_numEadvancedisB.Value;
        double veloA = (double)_numPrintVeloA.Value;
        double veloB = (double)_numPrintVeloB.Value;

        double vc0 = (double)_numAchangeV.Value, vc1 = (double)_numBchangeV.Value;
        double dc0 = (double)_numAchangeDis.Value, dc1 = (double)_numBchangeDis.Value;

        // 采样周期 dt(s)：由面板 numdt(ms) 设置，与 StatsPanel 导出的 dt 统一；各材料步长 = V × dt。
        double dt = (double)_numdt.Value / 1000.0;

        // 完整处理管线：提前偏移 → 变速区域 → 关键点插值 → 密度过滤
        // DirectGeneratePath 参数顺序：advanceDis0(A), advanceDis1(B), velo0(A), velo1(B), vChange0/1, disChange0/1, dt
        List<Point3D> processed = PathGenerator.DirectGeneratePath(
            _originalPoints, advancedisA, advancedisB, veloA, veloB, vc0, vc1, dc0, dc1, dt, enableVeloChange: _chkVelochange.Checked);

        // 保存（与原版一致：无表头，6 列 X,Y,Z,Extrude,Tool,Pressure）
        using SaveFileDialog sfd = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = "csv",
            FileName = "PathAdvanced_" + DateTime.Now.ToString("yyyyMMdd"),
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            string[] lines = new string[processed.Count];
            for (int i = 0; i < processed.Count; i++)
            {
                Point3D v = processed[i];
                // 末列 Feed 为该点速度(mm/s)：变速区域内为梯形渐变速度，其余为材料正常速度
                lines[i] = v.X.ToString("0.000") + "," +
                           v.Y.ToString("0.000") + "," +
                           (-1*v.Z).ToString("0.000") + "," +
                           v.Extrude.ToString() + "," +
                           v.Tool.ToString() + "," +
                           v.Pressure.ToString("0") + "," +
                           v.Feed.ToString("0.000");
            }
            File.WriteAllLines(sfd.FileName, lines, System.Text.Encoding.UTF8);

            int t0 = processed.Count(p => p.Tool == 0), t1 = processed.Count(p => p.Tool == 1);
            // 变速区域跨越切换点折线渐变（切换点速度=vc，两端线性衔接材料正常速度），仅勾选"切换变速"时显示
            string speedRange = _chkVelochange.Checked
                ? "\r\n变速跨越切换点折线渐变：切换点速度 A/B " + vc0.ToString("F1") + "/" + vc1.ToString("F1") + " mm/s，切换距离 A/B " + dc0.ToString("F2") + "/" + dc1.ToString("F2") + " mm（两端线性衔接材料正常速度，全程无突变）"
                : "";
            MessageBox.Show(this,
                "CSV 已保存到：" + sfd.FileName +
                "\r\n原始 " + _originalPoints.Count + " 点 → 处理后 " + processed.Count + " 点" +
                "\r\n材料 T0:" + t0 + "  T1:" + t1 +
                "\r\n提前距离 A/B：" + advancedisA.ToString("F2") + " / " + advancedisB.ToString("F2") + " mm" +
                "  速度 A/B：" + veloA.ToString("F1") + " / " + veloB.ToString("F1") + " mm/s" +
                "  步长 A/B：" + (veloA * dt).ToString("F3") + " / " + (veloB * dt).ToString("F3") + " mm  (dt=" + ((double)_numdt.Value).ToString("0") + "ms)" +
                speedRange +
                "\r\n列：X,Y,Z,Extrude,Tool,Pressure,Feed(速度mm/s)",
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);

            //赋值
            _generatedPoints = processed;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }


    /// <summary>导出生成的栅格路径点（体素/图片）为 CSV，Tool 列即体素/图片材料值。</summary>
    private void ExportGeneratedCsv()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            FileName = "generated_path.csv",
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        bool ok = PathGenerator.ExportPoint3DListToCsv(_generatedPoints, sfd.FileName);
        int t0 = _generatedPoints.Count(p => p.Tool == 0), t1 = _generatedPoints.Count(p => p.Tool == 1);
        MessageBox.Show(this,
            (ok ? "已导出：" : "导出失败：") + sfd.FileName +
            "\r\n共 " + _generatedPoints.Count + " 点（材料 T0:" + t0 + " T1:" + t1 + "）" +
            "\r\n列：X,Y,Z,Extrude,Feed,Pressure,Tool,Layer,GridType,MaterialA（Tool 即体素/图片材料值）",
            "导出", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    // ====================== 送回主窗口查看 ======================
    private void OnSendToMain(object? sender, EventArgs e)
    {
        // 发送生成后的路径
        List<Point3D> pts = _generatedPoints.Count > 0 ? _generatedPoints : _simplifiedPoints;
        if (pts.Count == 0) return;
        string name = _generatedPoints.Count > 0
            ? "generated"
            : Path.GetFileNameWithoutExtension(_sourceFile) + "_simplified";
        ParsedGcode parsed = PathGenerator.ToParsedGcode(pts, name);
        _owner.LoadExternal(parsed, name);
        _owner.BringToFront();
        _status.Text = "已发送到主窗口查看。";
        //关闭本窗体
        this.Close();
    }

    private void UpdateStatus()
    {
        double tol = (double)_numTol.Value;
        double ratio = _originalPoints.Count > 0 ? _simplifiedPoints.Count * 100.0 / _originalPoints.Count : 0;
        string method = _cboMapMethod.SelectedItem?.ToString() ?? "DDA";
        _status.Text =
            $"文件: {Path.GetFileName(_sourceFile)}  |  " +
            $"原始 {_originalPoints.Count} 点 → 简化 {_simplifiedPoints.Count} 点" +
            $"（阈值 {tol:0.###} mm，保留 {ratio:0.#}%）  |  打印速度 A/B {_numPrintVeloA.Value}/{_numPrintVeloB.Value} mm/s" +
            (_chkVelochange.Checked ? "  变速" : "  恒速") + $"  |  映射: {method}";
    }

    // ====================== 评估体素映射精度（手动触发）======================
    // 对"映射后(assigned)"与"简化后(simplified)"两条路径分别评估：节点级精确匹配率、
    // 沿折线密集采样的 MAE/RMSE/MaxAE、误差分桶直方图、体素覆盖率、层映射诊断；
    // 弹窗对比两者并可选导出误差明细 CSV（供 Origin 绘误差空间分布与直方图）。
    private void OnEvaluateAccuracy(object? sender, EventArgs e)
    {
        if (_voxelData == null || _voxelData.Matrix.Count == 0)
        {
            MessageBox.Show(this, "请先导入体素文件，再评估映射精度。", "提示");
            return;
        }
        if (_originalPoints.Count == 0 || _rawPoints.Count == 0)
        {
            MessageBox.Show(this, "请先加载 G-code 路径。", "提示");
            return;
        }

        try
        {
            // 以原始路径(_rawPoints)构建评估 frame，保证与映射时基准一致；分别评估 assigned / simplified
            var repAssigned = VoxelMapper.EvaluateVoxelMapAccuracy(_originalPoints, _voxelData, _rawPoints);
            var repSimplified = VoxelMapper.EvaluateVoxelMapAccuracy(_simplifiedPoints, _voxelData, _rawPoints);

            string msg = "==== 体素映射精度评估 ====\n\n"
                + "—— 映射后 (assigned) ——\n" + repAssigned + "\n\n"
                + "—— 简化后 (simplified) ——\n" + repSimplified;
            MessageBox.Show(this, msg, "映射精度评估", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 导出误差明细 CSV（弹一次保存对话框，自动生成 _assigned / _simplified 两份）
            using var sfd = new SaveFileDialog
            {
                Title = "导出误差明细 CSV（将同时导出 _assigned / _simplified 两份）",
                Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                FileName = "accuracy_assigned.csv",
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            string dir = Path.GetDirectoryName(sfd.FileName) ?? "";
            if (string.IsNullOrEmpty(dir)) dir = Environment.CurrentDirectory;
            string pathAssigned = Path.Combine(dir, "accuracy_assigned.csv");
            string pathSimplified = Path.Combine(dir, "accuracy_simplified.csv");
            bool ok1 = VoxelMapper.ExportVoxelMapAccuracyCsv(repAssigned, pathAssigned);
            bool ok2 = VoxelMapper.ExportVoxelMapAccuracyCsv(repSimplified, pathSimplified);

            MessageBox.Show(this,
                "误差明细已导出到：" + dir + "\r\n\r\n" +
                "· accuracy_assigned.csv   " + (ok1 ? "（成功）" : "（失败/空）") + "  共 " + repAssigned.ErrorRecords.Count + " 条\r\n" +
                "· accuracy_simplified.csv " + (ok2 ? "（成功）" : "（失败/空）") + "  共 " + repSimplified.ErrorRecords.Count + " 条\r\n" +
                "\r\n列：X,Y,Layer,VoxelLayer,DPred,DTrue,Error（可在 Origin 中绘误差分布与直方图）",
                "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "评估失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnCompareMappingMethods(object? sender, EventArgs e)
    {

        if (_voxelData == null || _voxelData.Matrix.Count == 0)
        {
            MessageBox.Show("请先导入体素。");
            return;
        }

        if (_rawPoints.Count == 0)
        {
            MessageBox.Show("请先导入Gcode。");
            return;
        }
        try
        {
            // 各映射方法：名称 + 赋值委托（与界面 ApplyMapping 同源调用）
            var methods = new (string Name, Func<List<Point3D>> Assign)[]
            {
                ("DDA",      () => VoxelMapper.AssignTValues(_rawPoints, _voxelData!)),
                ("Nearest",  () => VoxelMapper.AssignTValuesNearest(_rawPoints, _voxelData!)),
                ("Bilinear", () => VoxelMapper.AssignTValuesBilinear(_rawPoints, _voxelData!)),
                ("SDF",      () => VoxelMapper.AssignTValuesSdf(_rawPoints, _stlMesh)),
            };

            const int RUNS = 5;   // 运行时间取多次平均，降低单次测量抖动
            // 每方法：运行时间(ms) + 映射点集 + 精度评估报告
            var rows = new List<(string Name, List<Point3D> Points, VoxelMapAccuracyReport Rep, double Ms)>();
            foreach (var (name, assign) in methods)
            {
                // —— 指标1 运行时间：多次测量取平均（最后一次结果用于评估）——
                List<Point3D> pts = new();
                double sumMs = 0;
                for (int r = 0; r < RUNS; r++)
                {
                    var sw = Stopwatch.StartNew();
                    pts = assign();
                    sw.Stop();
                    sumMs += sw.Elapsed.TotalMilliseconds;
                }
                double ms = sumMs / RUNS;

                // 精度评估（4 种方法统一基于同一 voxel 真实深度对比）
                var rep = VoxelMapper.EvaluateVoxelMapAccuracy(pts, _voxelData, _rawPoints);
                rows.Add((name, pts, rep, ms));
            }

            // 弹窗文本：主指标（连续几何精度：切换点位置偏差）紧凑对比
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("====== 映射算法对比（主指标：位置偏差 = 连续几何精度，S↔T 双向）======");
            sb.AppendLine("方法, 切换点位置偏差MAE(mm), 切换点位置偏差RMSE(mm), 切换点位置偏差Max(mm), 材料位置偏差MAE(mm), 实际/理论切换点数");
            foreach (var r in rows)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}, {1:F4}, {2:F4}, {3:F4}, {4:F4}, {5}/{6}",
                    r.Name, r.Rep.SwitchPointPosMAE, r.Rep.SwitchPointPosRMSE,
                    r.Rep.SwitchPointPosMax, r.Rep.MaterialPosMAE,
                    r.Rep.SwitchPointCount, r.Rep.TheoreticalSwitchCount));
            }
            sb.AppendLine();
            sb.AppendLine("（辅助离散一致性指标、效率与核查量见下方导出的 CSV）");

            // 选择保存路径：只导出一个总统计表 CSV（一行一种方法，便于 Origin 横向对比）
            using var sfd = new SaveFileDialog
            {
                Title = "导出映射方法对比总统计表 CSV",
                Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                FileName = "Compare_Methods_summary.csv",
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            // 总统计表分四组：主(连续几何精度·S↔T 双向) → 辅(离散一致性) → 效率 → 核查量
            var csvLines = new List<string>
            {
                "方法," +
                "切换点位置偏差MAE(mm),切换点位置偏差RMSE(mm),切换点位置偏差Max(mm),材料位置偏差MAE(mm)," +   // 主·连续几何精度(S→T + T→S)
                "节点正确率(%),切换点正确率(%),切换点Tool偏差MAE,边界平均误差,边界锯齿RMSE,覆盖完整率(%),材料精度(%)," + // 辅·离散一致性
                "运行时间(ms),每点查询速度(μs/点)," +                                                  // 效率
                "实际切换点数,理论切换点数,节点数,边界样本数,有材料单元总数,覆盖有材料单元数"            // 核查
            };
            foreach (var r in rows)
            {
                double queryUs = r.Rep.NodeCount > 0 ? r.Ms * 1000.0 / r.Rep.NodeCount : 0;
                csvLines.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1:F4},{2:F4},{3:F4},{4:F4}," +                  // 主·连续几何精度(切换点MAE/RMSE/Max + 材料MAE)
                    "{5:F4},{6:F4},{7:F4},{8:F4},{9:F4},{10:F4},{11:F4}," + // 辅·离散一致性
                    "{12:F4},{13:F4}," +                                   // 效率
                    "{14},{15},{16},{17},{18},{19}",                       // 核查
                    r.Name,
                    r.Rep.SwitchPointPosMAE, r.Rep.SwitchPointPosRMSE, r.Rep.SwitchPointPosMax, r.Rep.MaterialPosMAE,
                    r.Rep.NodeExactRate * 100.0, r.Rep.SwitchPointExactRate * 100.0,
                    r.Rep.SwitchPointMAE, r.Rep.BoundaryMAE, r.Rep.BoundaryRMSE,
                    r.Rep.CoverageRecall * 100.0, r.Rep.MaterialCoverRate * 100.0,
                    r.Ms, queryUs,
                    r.Rep.SwitchPointCount, r.Rep.TheoreticalSwitchCount,
                    r.Rep.NodeCount, r.Rep.BoundarySampleCount,
                    r.Rep.TruePositiveCells, r.Rep.CoveredNonZeroCells));
            }

            // 写入文件（含 BOM，便于 Excel/Origin 正确识别中文表头）
            bool ok = false;
            try
            {
                File.WriteAllLines(sfd.FileName, csvLines, new UTF8Encoding(true));
                ok = true;
            }
            catch { /* 写入失败：ok 保持 false，交由弹窗提示 */ }

            MessageBox.Show(this,
                "总统计表已导出：" + sfd.FileName + (ok ? "" : "（写入失败）") + "\r\n\r\n" +
                sb.ToString(),
                "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "评估失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }


    }


    private void PathGeneratorForm_Load(object sender, EventArgs e)
    {
        // 初始化控件默认值
        _cboMapMethod.SelectedIndex = 0; // 默认 DDA
    }
}
