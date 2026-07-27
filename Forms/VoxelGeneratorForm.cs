using System.Windows.Forms;
using GcodeViewer.Parsing;

namespace GcodeViewer.Forms;

/// <summary>
/// 体素生成器窗体（STL → 光线投影体素化 → 体素 CSV）。
/// 流程：导入 STL → 选投影方向/体素边长 → 生成体素（内嵌 Viewport3D 点云预览）→ 导出标准体素 CSV。
/// 核心算法在 <see cref="VoxelGenerator"/>；STL 解析复用 <see cref="VoxelMapper.ParseStlFile"/>。
/// 控件/布局/事件见 VoxelGeneratorForm.Designer.cs（设计器分部文件，VS 设计器里可视化编辑）。
/// </summary>
public sealed partial class VoxelGeneratorForm : Form
{
    private VoxelMapper.StlMesh? _mesh;        // 已导入 STL 三角网格（体素化输入）
    private VoxelGenerator.VoxelGrid? _grid;   // 最近一次生成的体素栅格（导出用）
    private string _stlPath = "";              // 已导入 STL 文件全路径（导出默认文件名用）

    public VoxelGeneratorForm()
    {
        // 控件、布局与事件均由 InitializeComponent（Designer）创建与绑定
        InitializeComponent();
        _cboAxis.SelectedIndex = 0;            // 默认沿 Z 方向投影（ComboBox 顺序：Z/X/Y）
        EnableActions(false);
    }

    /// <summary>生成/导出按钮的可用状态统一控制：未导入 STL 前禁用生成，未生成体素前禁用导出。</summary>
    private void EnableActions(bool canGenerate)
    {
        _btnGenerate.Enabled = canGenerate;
        _btnExport.Enabled = _grid != null;
    }

    /// <summary>
    /// 当前投影方向映射到 <see cref="VoxelGenerator.VoxelizeByRayCasting"/> 的 axis 参数(0=X,1=Y,2=Z)。
    /// ComboBox 下拉顺序为 { "Z", "X", "Y" }（Z 排首位=默认），故索引与 axis 编码需做转换。
    /// </summary>
    private int SelectedAxis => _cboAxis.SelectedIndex switch
    {
        0 => 2,   // "Z" → axis 2
        1 => 0,   // "X" → axis 0
        2 => 1,   // "Y" → axis 1
        _ => 2,   // 兜底（未选择时按 Z）
    };

    // ====================== 导入 STL ======================
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
            _mesh = mesh;
            _stlPath = dlg.FileName;
            _lblStlFile.Text = Path.GetFileName(dlg.FileName) + "（" + mesh.TriangleCount + " 面片）";

            // 显示 STL 线框，清空旧体素预览
            _preview.SetStlMesh(mesh);
            _grid = null;
            _preview.SetVoxelData(null);
            _preview.Invalidate();

            EnableActions(true);
            UpdateStatus("STL 已加载：" + mesh.TriangleCount + " 个三角面片，可生成体素。");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "STL 加载失败：" + ex.Message, "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ====================== 生成体素（光线投影法）======================
    private void OnGenerate(object? sender, EventArgs e)
    {
        if (_mesh == null || !_mesh.IsValid)
        {
            MessageBox.Show(this, "请先导入 STL 模型。", "提示");
            return;
        }

        double voxelSize = (double)_numVoxelSize.Value;

        // 体素总数预估（按包围盒向上取整）：避免过小体素边长导致数组过大 OOM
        long estX = (long)Math.Ceiling((_mesh.MaxX - _mesh.MinX) / voxelSize) + 1;
        long estY = (long)Math.Ceiling((_mesh.MaxY - _mesh.MinY) / voxelSize) + 1;
        long estZ = (long)Math.Ceiling((_mesh.MaxZ - _mesh.MinZ) / voxelSize) + 1;
        long estimate = estX * estY * estZ;
        const long WARN_LIMIT = 20_000_000;   // 2000 万体素以上提示确认
        if (estimate > WARN_LIMIT)
        {
            DialogResult r = MessageBox.Show(this,
                "预估体素数约 " + estimate.ToString("N0") + "（" + estX + "×" + estY + "×" + estZ +
                "），可能占用大量内存甚至失败。\r\n是否继续？", "体素量较大",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
        }

        try
        {
            var grid = VoxelGenerator.VoxelizeByRayCasting(_mesh, voxelSize, SelectedAxis);
            _grid = grid;

            // 体素点云预览（转 RLE VoxelData 供 Viewport3D 直接渲染）
            _preview.SetVoxelData(VoxelGenerator.ToVoxelData(grid));
            _preview.Invalidate();

            // 统计体内体素数
            int inside = 0;
            var vals = grid.Values;
            for (int i = 0; i < vals.Length; i++) inside += vals[i];

            EnableActions(true);
            UpdateStatus("体素生成完成：" + grid.CountX + "×" + grid.CountY + "×" + grid.CountZ +
                         " = " + grid.TotalCount + " 体素（体内 " + inside + "），可导出 CSV。");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "生成失败：" + ex.Message, "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ====================== 导出体素 CSV ======================
    private void OnExport(object? sender, EventArgs e)
    {
        if (_grid == null)
        {
            MessageBox.Show(this, "请先生成体素。", "提示");
            return;
        }

        string defaultName = string.IsNullOrEmpty(_stlPath)
            ? "voxel.csv"
            : Path.GetFileNameWithoutExtension(_stlPath) + "_vox.csv";
        using var sfd = new SaveFileDialog
        {
            Filter = "体素文件|*.csv|所有文件|*.*",
            DefaultExt = "csv",
            FileName = defaultName,
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            bool ok = VoxelGenerator.ExportVoxelCsv(_grid, sfd.FileName);
            MessageBox.Show(this,
                ok ? "已导出：" + sfd.FileName : "导出失败（体素为空或写入异常）。",
                ok ? "完成" : "错误",
                MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            UpdateStatus(ok ? "CSV 已导出：" + sfd.FileName : "CSV 导出失败。");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：" + ex.Message, "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateStatus(string text) => _lblStatus.Text = text;
}
