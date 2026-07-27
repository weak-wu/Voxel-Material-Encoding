using System.Globalization;
using System.IO;
using System.Text;
using GcodeViewer.Models;

namespace GcodeViewer.Forms;

/// <summary>
/// 右侧控制面板：显示开关（按材料/按层着色、切换点、已修改点、层过滤）+ 选中点坐标编辑 + 撤销。
/// 控件声明在 StatsPanel.Designer.cs，可在 VS 设计器里可视化编辑。
/// </summary>
public sealed partial class StatsPanel : UserControl
{
    public event Action<MoveType>? ChangeTypeRequested;
    public event Action<char, double>? CoordChangeRequested;
    public event Action? UndoRequested;
    public event Action<bool, bool>? ColorModeChanged; // byTool, byLayer
    public event Action<bool>? ShowToolChanged;
    public event Action<bool>? ShowModifiedChanged;
    public event Action<int>? FilterLayerChanged; // -1 = 全部

    /// <summary>请求导出当前路径为密集点 CSV；参数为采样周期（s）。数据 _gcode 由主窗提供。</summary>
    public event Action<double>? ExportCsvRequested;

    public StatsPanel()
    {
        InitializeComponent();
        WireEvents();
    }

    private void WireEvents()//写入事件
    {
        _btnG0.Click += (s, e) => ChangeTypeRequested?.Invoke(MoveType.G0);
        _btnG1.Click += (s, e) => ChangeTypeRequested?.Invoke(MoveType.G1);
        _btnApplyCoord.Click += (s, e) => ApplyCoord();
        _btnUndo.Click += (s, e) => UndoRequested?.Invoke();

        _cbColorTool.CheckedChanged += (s, e) => ColorModeChanged?.Invoke(_cbColorTool.Checked, _cbColorLayer.Checked);
        _cbColorLayer.CheckedChanged += (s, e) => ColorModeChanged?.Invoke(_cbColorTool.Checked, _cbColorLayer.Checked);
        _cbToolChange.CheckedChanged += (s, e) => ShowToolChanged?.Invoke(_cbToolChange.Checked);
        _cbModified.CheckedChanged += (s, e) => ShowModifiedChanged?.Invoke(_cbModified.Checked);
        _cbFilterLayer.CheckedChanged += (s, e) => OnFilterToggle();
        _layerTrack.Scroll += (s, e) => OnLayerTrack();
    }

    private void OnFilterToggle()
    {
        if (_cbFilterLayer.Checked) FilterLayerChanged?.Invoke(_layerTrack.Value);
        else FilterLayerChanged?.Invoke(-1);
    }

    private void OnLayerTrack()
    {
        _lblLayer.Text = _cbFilterLayer.Checked && _layerTrack.Value >= 0
            ? $"层: {_layerTrack.Value}" : "层: 全部";
        if (_cbFilterLayer.Checked) FilterLayerChanged?.Invoke(_layerTrack.Value);
    }

    public void SetLayerRange(int maxLayer)
    {
        _layerTrack.Maximum = Math.Max(0, maxLayer);
        if (_layerTrack.Value > _layerTrack.Maximum) _layerTrack.Value = _layerTrack.Maximum;
    }

    /// <summary>设置"按材料/按层着色"复选框，并触发 ColorModeChanged（供外部强制切换显示模式）。</summary>
    public void SetColorMode(bool byTool, bool byLayer)
    {
        _cbColorTool.Checked = byTool;
        _cbColorLayer.Checked = byLayer;
        ColorModeChanged?.Invoke(byTool, byLayer);
    }

    private void ApplyCoord()
    {
        CoordChangeRequested?.Invoke('X', (double)_numX.Value);
        CoordChangeRequested?.Invoke('Y', (double)_numY.Value);
        CoordChangeRequested?.Invoke('Z', (double)_numZ.Value);
    }

    public void ShowPoint(GcodeMove? m, GcodeMove? prev)
    {
        bool has = m != null;
        _btnG0.Enabled = _btnG1.Enabled = _btnApplyCoord.Enabled =
            _numX.Enabled = _numY.Enabled = _numZ.Enabled = has;
        if (m == null)
        {
            _lblPointInfo.Text = "(未选中移动点)";
            return;
        }
        string typ = m.Type == MoveType.G0 ? "G0 空行程" : "G1 打印";
        string mod = m.IsModified ? "  [已修改]" : "";
        string tc = m.IsToolChange ? "  [切换点]" : "";
        _lblPointInfo.Text =
            $"行{m.LineNumber}  层{m.Layer}  {typ}{tc}{mod}\n" +
            $"工具 T{m.Tool}   累计 {m.CumulativeLength:0.###} mm   气压 {m.Pressure:0.###}";
        _numX.Value = (decimal)Math.Clamp(m.X, (double)_numX.Minimum, (double)_numX.Maximum);
        _numY.Value = (decimal)Math.Clamp(m.Y, (double)_numY.Minimum, (double)_numY.Maximum);
        _numZ.Value = (decimal)Math.Clamp(m.Z, (double)_numZ.Minimum, (double)_numZ.Maximum);
    }

    /// <summary>等时间采样的控制周期（s，50Hz）。每段步长 = 该行速度 V × 本周期。由面板上 numdt(ms) 设置。</summary>
    private double SamplePeriod = 0.02;

    /// <summary>静态兜底周期（s）：ExportMovesCsv 为静态方法，无法读取实例字段 SamplePeriod，故单独提供默认值。</summary>
    private const double DefaultSamplePeriod = 0.02;

    /// <summary>"导出 CSV" 按钮：采样周期 dt 由面板 numdt(ms) 设置，各 G0/G1 行步长 = V × dt。</summary>
    private void OnExportCsv(object? sender, EventArgs e)
    {
        SamplePeriod = Convert.ToDouble(numdt.Value) / 1000.0; // ms → s
        ExportCsvRequested?.Invoke(SamplePeriod);
    }

    /// <summary>无 V 参数（或 V≤0）行的兜底步长（mm），保证无速度信息时仍能正常插值。</summary>
    private const double DefaultStep = 1.0;

    /// <summary>近重合阈值（mm）：两点欧氏距离小于该值视为「重复点」，滤波时剔除非结构点。</summary>
    private const double DupEps = 1e-4;

    /// <summary>采样点中间结构：坐标 + 属性 + 是否结构点（段终点，滤波时强制保留）。</summary>
    private readonly struct SamplePt
    {
        public readonly double X, Y, Z;
        public readonly string Type;        // "G0" / "G1"
        public readonly int Tool;
        public readonly double Pressure;
        public readonly bool Structural;    // true=段终点等几何关键点，滤波不可剔除
        public SamplePt(double x, double y, double z, string type, int tool, double pressure, bool structural)
        {
            X = x; Y = y; Z = z; Type = type; Tool = tool; Pressure = pressure; Structural = structural;
        }
    }

    /// <summary>
    /// 把 ParsedGcode 导出为 6 列点 CSV：X,Y,Z,G0/G1,T0/T1,P。
    /// 【速度一致性】G0 与 G1 统一按本行设计速度 V 做弧长等步长采样，步长 step = V × samplePeriod
    ///   （V 缺失或 ≤0 时退化为 DefaultStep）。段内插值点严格布在弧长 s = step, 2·step, … 处，
    ///   相邻点距恒为 step = V·T → 以 samplePeriod 等时回放时速度恒等于设计速度 V。
    ///   段终点（角点）始终输出以保几何；段末尾若残留一个 &lt; step 的短间隔，体现拐点减速，不再加密/抽稀。
    /// 【点集滤波】仅剔除与上一个保留点近乎重合（距离 &lt; DupEps）的非结构插值点
    ///   （整步命中终点造成的重复点、零长段重复点等），结构点（段终点）一律保留——即「保几何、仅清重复点」。
    /// 格式与 CsvPathReader 兼容，主窗可直接重新打开预览。返回导出点数。
    /// </summary>
    public static int ExportMovesCsv(ParsedGcode g, double samplePeriod, string path)
    {
        if (samplePeriod <= 0) samplePeriod = DefaultSamplePeriod;
        var pts = new List<SamplePt>(g.Moves.Count * 4);

        foreach (var m in g.Moves)
        {
            string type = m.Type == MoveType.G1 ? "G1" : "G0";
            // 步长按本行设计速度 V 计算（d = V·T）；无 V 时退化为默认步长。G0/G1 统一处理。
            double step = m.Speed > 0 ? m.Speed * samplePeriod : DefaultStep;
            if (step <= 0) step = DefaultStep;              // 防御：步长必须为正
            double L = m.SegmentLength;

            // 段内等步长插值：在弧长 k·step（k=1,2,…）处布点；k·step < L - DupEps 保证最后一个插值点不与终点重合。
            // 用整数 k 累乘 step 避免浮点累加漂移；每点距上一个保留点（段起点或上一插值点）恰为 step → 速度 = V。
            for (int k = 1; k * step < L - DupEps; k++)
            {
                double t = L > DupEps ? (k * step) / L : 0.0;
                double x = m.PrevX + (m.X - m.PrevX) * t;
                double y = m.PrevY + (m.Y - m.PrevY) * t;
                double z = m.PrevZ + (m.Z - m.PrevZ) * t;
                pts.Add(new SamplePt(x, y, z, type, m.Tool, m.Pressure, structural: false));
            }
            // 段终点：始终输出（保几何角点），标记为结构点。
            pts.Add(new SamplePt(m.X, m.Y, m.Z, type, m.Tool, m.Pressure, structural: true));
        }

        // 点集滤波：剔除与上一个保留点近重合（距离 < DupEps）的非结构插值点；结构点一律保留。
        var kept = new List<SamplePt>(pts.Count);
        for (int i = 0; i < pts.Count; i++)
        {
            SamplePt p = pts[i];
            if (!p.Structural && kept.Count > 0)
            {
                SamplePt last = kept[kept.Count - 1];
                double dx = p.X - last.X, dy = p.Y - last.Y, dz = p.Z - last.Z;
                if (dx * dx + dy * dy + dz * dz < DupEps * DupEps) continue; // 近重合，剔除
            }
            kept.Add(p);
        }

        // 写文件（6 列，与 CsvPathReader 兼容）
        var lines = new List<string>(kept.Count);
        foreach (var p in kept)
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "{0:F3},{1:F3},{2:F3},{3},{4},{5:F0}",
                p.X, p.Y, p.Z, p.Type, "T" + p.Tool, p.Pressure));

        File.WriteAllLines(path, lines, Encoding.UTF8);
        return lines.Count;
    }
}
