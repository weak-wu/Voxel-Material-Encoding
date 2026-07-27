using GcodeViewer.Models;
using GcodeViewer.Parsing;
using GcodeViewer.Rendering;

namespace GcodeViewer.Forms;

/// <summary>
/// 主窗体：左(层/切换列表) + 中(Viewport3D) + 右(StatsPanel) + 下(gcode 文本浏览)。
/// 控件声明在 MainForm.Designer.cs，可在 VS 设计器里可视化编辑。
/// 单一选中状态 _selectedMoveIndex 驱动三向联动。支持 G0↔G1 翻转、坐标微调、撤销、保存、CSV 导出。
/// </summary>
public sealed partial class MainForm : Form
{
    private ParsedGcode? _gcode;
    private bool _dirty;
    private int _selectedMoveIndex = -1;
    private readonly List<EditAction> _undoStack = new();
    private string _currentFile = string.Empty;
    private bool _fullScreen3D;

    public MainForm()
    {
        InitializeComponent();
        WireEvents();
        Load += (s, e) => ApplyDefaultLayout();
    }

    /// <summary>挂接所有控件事件（设计器生成的 InitializeComponent 只建控件，事件在此挂）。</summary>
    private void WireEvents()
    {
        _viewport.SelectedMoveChanged += (s, idx) => { if (idx.HasValue) SetSelected(idx.Value, fromViewport: true); };
        _statsPanel.ChangeTypeRequested += OnChangeType;
        _statsPanel.CoordChangeRequested += OnCoordChange;
        _statsPanel.UndoRequested += () => Undo();
        _statsPanel.ColorModeChanged += (byTool, byLayer) =>
        {
            _viewport.ColorByTool = byTool;
            _viewport.ColorByLayer = byLayer;
            _viewport.Invalidate();
        };
        _statsPanel.ShowToolChanged += v => { _viewport.ShowToolChange = v; _viewport.Invalidate(); };
        _statsPanel.ShowModifiedChanged += v => { _viewport.ShowModified = v; _viewport.Invalidate(); };
        _statsPanel.FilterLayerChanged += layer =>
        {
            _viewport.FilterLayer = layer;
            _viewport.Invalidate();
        };

        // 右侧面板「导出 CSV」：把密集采样后的点集导出（G1 按步长插值、G0 仅终点）
        _statsPanel.ExportCsvRequested += OnStatsExportCsv;

        // gcode 文本与跳转
        _codeList.SelectedIndexChanged += OnCodeSelect;
        _layerList.SelectedIndexChanged += OnLayerSelect;
        _gotoBtn.Click += (s, e) => GotoLine();

        // 菜单 + 左侧打开按钮（两者都调用同一个 Open()）
        _miOpen.Click += (s, e) => Open();
        _miSave.Click += (s, e) => SaveAs();
        _miExit.Click += (s, e) => Close();
        _miFullScreen.Click += (s, e) => ToggleFullScreen3D();
        _miResetLayout.Click += (s, e) => ApplyDefaultLayout();
        _miFit.Click += (s, e) => _viewport.ResetView();
        _miTopView.Click += (s, e) => _viewport.TopView();
        _miEvalCompare.Click += (s, e) => OpenEvaluation();
        _miPathGen.Click += (s, e) => OpenPathGenerator();
        _miBigData.Click += (s, e) => OpenBigDataForm();
        _miVoxelGen.Click += (s, e) => OpenVoxelGenerator();
        _miLineWidth.Click += (s, e) => OpenLineWidthForm();
        _miAbout.Click += (s, e) => MessageBox.Show(
            "G0=空行程(灰虚线)  G1=打印(实线)\n" +
            "T0=红 T1=蓝  切换点=橙点  已修改=金环\n\n" +
            "3D 视图：左键拖=旋转  右键/Shift+左键拖=平移  滚轮=缩放  双击=重置  单击=选点\n" +
            "F11 全屏3D  Ctrl+Z 撤销  Ctrl+S 保存\n" +
            "截图技巧：视图→俯视(沿 Z 轴) 得顶视图，再按 F11 全屏即可无干扰截图\n\n" +
            "选中点后可设为 G0/G1，或在 X/Y/Z 框输入新值后点应用坐标。",
            "说明");

        // 快捷键
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.S) { SaveAs(); e.Handled = true; }
        };
    }

    /// <summary>默认布局：3D 视图占绝大部分区域，其它面板最小化。</summary>
    private void ApplyDefaultLayout()
    {
        // 上下：工作区占 86%，底部 gcode 文本仅留一条带
        _splitOuter.Panel1MinSize = 280;
        _splitOuter.Panel2MinSize = 90;
        if (_splitOuter.Height > 400)
            _splitOuter.SplitterDistance = (int)(_splitOuter.Height * 0.86);

        // 左右(列表)：左侧列表尽量窄
        _splitMain.Panel1MinSize = 150;
        _splitMain.Panel2MinSize = 400;
        _splitMain.SplitterDistance = Math.Min(190, Math.Max(_splitMain.Panel1MinSize, _splitMain.Width - _splitMain.Panel2MinSize - 4));

        // 中(3D) / 右(统计)：3D 占 80% 宽度
        _splitMid.Panel1MinSize = 400;
        _splitMid.Panel2MinSize = 300;
        if (_splitMid.Width > 700)
            _splitMid.SplitterDistance = (int)(_splitMid.Width * 0.80);

        _viewport.ResetView();
    }

    /// <summary>全屏 3D：隐藏左/右/底面板，3D 占满整个客户区。</summary>
    private void ToggleFullScreen3D()
    {
        _fullScreen3D = !_fullScreen3D;
        if (_fullScreen3D)
        {
            _splitMain.Panel1Collapsed = true;
            _splitMid.Panel2Collapsed = true;
            _splitOuter.Panel2Collapsed = true;
        }
        else
        {
            _splitMain.Panel1Collapsed = false;
            _splitMid.Panel2Collapsed = false;
            _splitOuter.Panel2Collapsed = false;
            ApplyDefaultLayout();
        }
        _viewport.ResetView();
    }

    // ---- 文件 ----
    private void Open()
    {
        if (!ConfirmDiscard()) return;
        
        using var dlg = new OpenFileDialog { Filter = "路径 CSV|*.csv|G-code|*.gcode;*.g;*.nc;*.tap|所有文件|*.*" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        OpenPath(dlg.FileName);
    }

    private void OpenPath(string path)
    {
        try
        {
            // CSV 路径文件走专用解析器；其余按 gcode 解析。两者产出同一 ParsedGcode 结构。
            bool isCsv = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
            var g = isCsv ? CsvPathReader.Parse(path) : GcodeParser.Parse(path);
            LoadParsed(g, path);
        }
        catch (Exception ex)
        {
            MessageBox.Show("打开失败：" + ex.Message, "错误");
        }
    }

    /// <summary>
    /// 载入一个已构造好的 ParsedGcode 并刷新全部视图（列表 / 3D / 统计 / 状态）。
    /// 供 OpenPath（文件解析后）与 LoadExternal（路径生成窗体送回）共用。
    /// </summary>
    private void LoadParsed(ParsedGcode g, string displayName)
    {
        _gcode = g;
        _currentFile = displayName;
        _dirty = false;
        _selectedMoveIndex = -1;
        _undoStack.Clear();
        PopulateAll();
        _viewport.SetGcode(_gcode);
        _statsPanel.SetLayerRange(_gcode.Stats.Layers.Count > 0 ? _gcode.Stats.Layers.Max(l => l.Layer) : 0);

        // 无层信息（源文件无 ;LAYER 注释）：无法按层识别，强制按材料(T0/T1)显示
        if (!_gcode.HasLayerInfo)
            _statsPanel.SetColorMode(byTool: true, byLayer: false);

        UpdateStatus();
    }

    /// <summary>供路径生成窗体把生成的 ParsedGcode 送回主窗口查看（公开入口）。</summary>
    public void LoadExternal(ParsedGcode g, string displayName) => LoadParsed(g, displayName);

    /// <summary>打开路径生成窗体（G-code→RDP 简化→CSV 导出），非模态，可与主窗口并存。</summary>
    private void OpenPathGenerator()
    {
        var form = new PathGeneratorForm(this) { Owner = this };
        form.Show(this);
    }

    /// <summary>打开体素生成器窗体（STL→光线投影体素化→体素 CSV），非模态，可与主窗口并存。</summary>
    private void OpenVoxelGenerator()
    {
        var form = new VoxelGeneratorForm() { Owner = this };
        form.Show(this);
    }

    /// <summary>打开多组双材料 G-code 生成窗体（大数据 G-code），非模态，可与主窗口并存。</summary>
    private void OpenBigDataForm()
    {
        var form = new 大数据G_code(this) { Owner = this };
        form.Show(this);
    }

    /// <summary>打开打印线宽提取窗体（图像→HSV分割→水平校正→逐列测宽→折线图），非模态，与主窗口 G-code 数据相互独立。</summary>
    private void OpenLineWidthForm()
    {
        var form = new LineWidthForm() { Owner = this };
        form.Show(this);
    }

    private void SaveAs()
    {
        if (_gcode == null) return;
        using var dlg = new SaveFileDialog
        {
            Filter = "G-code|*.gcode|所有文件|*.*",
            FileName = string.IsNullOrWhiteSpace(_currentFile) ? "output.gcode" : Path.GetFileNameWithoutExtension(_currentFile) + "_edited.gcode",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            GcodeWriter.Save(_gcode, dlg.FileName);
            _dirty = false;
            UpdateStatus();
            MessageBox.Show("已保存到：\n" + dlg.FileName, "保存");
        }
        catch (Exception ex)
        {
            MessageBox.Show("保存失败：" + ex.Message, "错误");
        }
    }

    /// <summary>导出当前路径为密集点 CSV（G0/G1 统一按各行速度 V×采样周期弧长等步长采样 + 近重点滤波，保证回放速度=设计速度），由右侧面板触发。</summary>
    private void OnStatsExportCsv(double samplePeriod)
    {
        if (_gcode == null || _gcode.IsEmpty)
        {
            MessageBox.Show("请先打开路径再导出。", "提示");
            return;
        }
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV 路径|*.csv|所有文件|*.*",
            DefaultExt = "csv",
            FileName = (string.IsNullOrWhiteSpace(_currentFile) ? "path" : Path.GetFileNameWithoutExtension(_currentFile)) + "_dense.csv",
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            int count = StatsPanel.ExportMovesCsv(_gcode, samplePeriod, sfd.FileName);
            MessageBox.Show(
                $"已导出 {count} 个采样点到：\n{sfd.FileName}\n" +
                $"（G0/G1 统一按各行速度 V×{samplePeriod:0.###}s 弧长等步长采样，相邻点距 = V·T → 回放速度 = 设计速度；" +
                $"V 缺失按 1mm 兜底；已剔除近重合冗余点，段终点保留）", "完成");
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message, "错误");
        }
    }

    private bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        return MessageBox.Show("有未保存的修改，是否放弃？", "确认", MessageBoxButtons.YesNo) == DialogResult.Yes;
    }

    /// <summary>打开多 CSV 路径评估窗体（偏差 / 密度 / 体素体积）。模态对话框，独立于当前编辑的文件。</summary>
    private void OpenEvaluation()
    {
        using var form = new EvaluationForm();
        form.ShowDialog(this);
    }

    // ---- 选中状态（三向联动核心）----
    private void SetSelected(int moveIndex, bool fromViewport)
    {
        if (_gcode == null || moveIndex < 0 || moveIndex >= _gcode.Moves.Count) return;
        _selectedMoveIndex = moveIndex;

        var m = _gcode.Moves[moveIndex];
        var prev = moveIndex > 0 ? _gcode.Moves[moveIndex - 1] : null;
        _statsPanel.ShowPoint(m, prev);

        if (!fromViewport) _viewport.SelectMove(moveIndex);
        SelectCodeLine(m.LineNumber, suppressEvent: true);
        UpdateStatus();
    }

    // ---- 填充列表 ----
    private void PopulateAll()
    {
        if (_gcode == null) return;
        _codeList.BeginUpdate();
        _codeList.Items.Clear();
        // 首次填充时添加列头（Designer 未定义列，否则 Details 视图不显示任何行）
        if (_codeList.Columns.Count == 0)
        {
            _codeList.Columns.Add("#", 48);
            _codeList.Columns.Add("G-code 文本", _codeList.Width - 56);
        }
        foreach (var ln in _gcode.Lines)
            _codeList.Items.Add(new ListViewItem(new[] { ln.LineNumber.ToString(), ln.RawText }) { Tag = ln.LineNumber });
        _codeList.EndUpdate();

        _layerList.BeginUpdate();
        _layerList.Items.Clear();
        // 列头只建一次（Designer 未定义）；末列为 G0/G1 切换次数
        if (_layerList.Columns.Count == 0)
        {
            _layerList.Columns.Add("层", 36);
            _layerList.Columns.Add("起始行", 52);
            _layerList.Columns.Add("结束行", 52);
            _layerList.Columns.Add("G1长 mm", 62);
            _layerList.Columns.Add("G0长 mm", 62);
            _layerList.Columns.Add("换刀", 44);
            _layerList.Columns.Add("G0/G1切换", 70);
        }
        foreach (var l in _gcode.Stats.Layers)
            _layerList.Items.Add(new ListViewItem(new[]
            {
                l.Layer.ToString(), l.StartLine.ToString(), l.EndLine.ToString(),
                l.G1Length.ToString("0.###"), l.G0Length.ToString("0.###"), l.ToolChangeCount.ToString(),
                l.G0G1SwitchCount.ToString(),
            })
            { Tag = l.Layer });
        _layerList.EndUpdate();

        _switchList.BeginUpdate();
        _switchList.Items.Clear();
        // 首次填充时添加列头（Designer 未定义列，否则 Details 视图不显示任何行）
        if (_switchList.Columns.Count == 0)
        {
            _switchList.Columns.Add("行号", 48);
            _switchList.Columns.Add("层", 36);
            _switchList.Columns.Add("工具", 48);
            _switchList.Columns.Add("累计长 mm", 80);
        }
        foreach (var m in _gcode.Moves.Where(x => x.IsToolChange))
        {
            _switchList.Items.Add(new ListViewItem(new[]
            {
                m.LineNumber.ToString(), m.Layer.ToString(), "T" + m.Tool,
                m.CumulativeLength.ToString("0.###"),
            })
            { Tag = m.LineNumber });
        }
        _switchList.EndUpdate();
    }

    private void OnCodeSelect(object? sender, EventArgs e)
    {
        if (_codeList.SelectedItems.Count == 0) return;
        if (_codeList.SelectedItems[0].Tag is int lineNum)
        {
            var m = _gcode?.FindMoveByLine(lineNum);
            if (m != null) SetSelected(m.Index, fromViewport: false);
        }
    }

    private void OnLayerSelect(object? sender, EventArgs e)
    {
        if (_layerList.SelectedItems.Count == 0) return;
        if (_layerList.SelectedItems[0].Tag is int layer)
        {
            var first = _gcode?.Moves.FirstOrDefault(x => x.Layer == layer);
            if (first != null) SetSelected(first.Index, fromViewport: false);
        }
    }

    private void SelectCodeLine(int lineNum, bool suppressEvent)
    {
        if (suppressEvent) _codeList.SelectedIndexChanged -= OnCodeSelect;
        foreach (ListViewItem it in _codeList.Items)
        {
            if (it.Tag is int ln && ln == lineNum)
            {
                it.Selected = true;
                it.EnsureVisible();
                break;
            }
        }
        if (suppressEvent) _codeList.SelectedIndexChanged += OnCodeSelect;
    }

    private void GotoLine()
    {
        if (_gcode == null) return;
        if (int.TryParse(_gotoBox.Text.Trim(), out int line))
        {
            var m = _gcode.FindMoveByLine(line);
            if (m != null) SetSelected(m.Index, fromViewport: false);
            else SelectCodeLine(line, suppressEvent: false);
        }
    }

    // ---- 编辑 ----
    private void OnChangeType(MoveType newType)
    {
        if (_gcode == null || _selectedMoveIndex < 0) return;
        var m = _gcode.Moves[_selectedMoveIndex];
        if (m.Type == newType) return;

        _undoStack.Add(new EditAction
        {
            MoveIndex = _selectedMoveIndex,
            TypeChanged = true,
            OldType = m.Type,
            NewType = newType,
            Description = $"行{m.LineNumber}: {m.Type}→{newType}",
        });
        ApplyTypeChange(m, newType);
    }

    private void ApplyTypeChange(GcodeMove m, MoveType newType)
    {
        var line = _gcode!.Lines[m.LineNumber];
        LineEditor.ApplyTypeChange(line, newType);
        StatsCalculator.Recompute(_gcode);
        AfterEdit();
    }

    private void OnCoordChange(char axis, double value)
    {
        if (_gcode == null || _selectedMoveIndex < 0) return;
        var m = _gcode.Moves[_selectedMoveIndex];
        var line = _gcode.Lines[m.LineNumber];

        double oldVal = axis switch { 'X' => m.X, 'Y' => m.Y, _ => m.Z };
        if (Math.Abs(oldVal - value) < 1e-9) return;

        LineEditor.ApplyAxisChange(line, axis, value);

        var act = _undoStack.LastOrDefault(a => a.MoveIndex == _selectedMoveIndex && a.Description.StartsWith($"行{m.LineNumber}: 坐标"));
        if (act == null)
        {
            act = new EditAction { MoveIndex = _selectedMoveIndex, Description = $"行{m.LineNumber}: 坐标" };
            act.OldX = m.X; act.OldY = m.Y; act.OldZ = m.Z;
            _undoStack.Add(act);
        }
        act.NewX ??= m.X; act.NewY ??= m.Y; act.NewZ ??= m.Z;
        switch (axis)
        {
            case 'X': act.NewX = value; break;
            case 'Y': act.NewY = value; break;
            case 'Z': act.NewZ = value; break;
        }

        StatsCalculator.RecomputeAfterEdit(_gcode, _selectedMoveIndex);
        AfterEdit();
    }

    private void AfterEdit()
    {
        if (_gcode == null) return;
        _dirty = true;
        // 仅更新数据并重绘，保留用户当前的 3D 视角与缩放（不重置）
        _viewport.UpdateData(_gcode);
        _viewport.SelectMove(_selectedMoveIndex);
        var m = _gcode.Moves[_selectedMoveIndex];
        var prev = _selectedMoveIndex > 0 ? _gcode.Moves[_selectedMoveIndex - 1] : null;
        _statsPanel.ShowPoint(m, prev);
        PopulateAll();
        SelectCodeLine(m.LineNumber, suppressEvent: true);
        UpdateStatus();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0 || _gcode == null) return;
        var act = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        var m = _gcode.Moves[act.MoveIndex];
        var line = _gcode.Lines[m.LineNumber];

        if (act.TypeChanged)
            LineEditor.ApplyTypeChange(line, act.OldType);
        if (act.NewX.HasValue && act.OldX.HasValue) LineEditor.ApplyAxisChange(line, 'X', act.OldX.Value);
        if (act.NewY.HasValue && act.OldY.HasValue) LineEditor.ApplyAxisChange(line, 'Y', act.OldY.Value);
        if (act.NewZ.HasValue && act.OldZ.HasValue) LineEditor.ApplyAxisChange(line, 'Z', act.OldZ.Value);

        StatsCalculator.RecomputeAfterEdit(_gcode, act.MoveIndex);
        _selectedMoveIndex = act.MoveIndex;
        AfterEdit();
    }

    private void UpdateStatus()
    {
        string dirty = _dirty ? " ●未保存" : "";
        string file = string.IsNullOrEmpty(_currentFile) ? "(未打开)" : Path.GetFileName(_currentFile);
        string sel = _selectedMoveIndex >= 0 ? $" | 选中 move #{_selectedMoveIndex}" : "";
        string layer = _gcode != null && !_gcode.HasLayerInfo ? " | ⚠ 路径无法识别(无层信息)，已按材料 T0/T1 显示" : "";
        _statusBar.Text = $"文件: {file}{dirty} | 撤销栈: {_undoStack.Count}{sel}{layer}";
    }
    public string CurrentFile => _currentFile;

    private void _openBtn_Click_1(object sender, EventArgs e)
    {
        // 左侧工具栏的打开按钮（Designer 绑定此方法）
        Open();
    }
}
