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
    public event Action<bool, bool, bool>? ColorModeChanged; // byTool, byLayer, bySpeed
    /// <summary>采样周期 dt 变化（numdt.ValueChanged）；主窗据此重算按速度着色范围。</summary>
    public event Action? SamplePeriodChanged;
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

        _cbColorTool.CheckedChanged += (s, e) => ColorModeChanged?.Invoke(_cbColorTool.Checked, _cbColorLayer.Checked, _cbColorSpeed.Checked);
        _cbColorLayer.CheckedChanged += (s, e) => ColorModeChanged?.Invoke(_cbColorTool.Checked, _cbColorLayer.Checked, _cbColorSpeed.Checked);
        _cbColorSpeed.CheckedChanged += (s, e) => ColorModeChanged?.Invoke(_cbColorTool.Checked, _cbColorLayer.Checked, _cbColorSpeed.Checked);

        // 采样周期 dt 变化：通知主窗重算按速度着色范围
        numdt.ValueChanged += (s, e) => SamplePeriodChanged?.Invoke();
        _cbToolChange.CheckedChanged += (s, e) => ShowToolChanged?.Invoke(_cbToolChange.Checked);
        _cbModified.CheckedChanged += (s, e) => ShowModifiedChanged?.Invoke(_cbModified.Checked);
        _cbFilterLayer.CheckedChanged += (s, e) => OnFilterToggle();
        _layerTrack.Scroll += (s, e) => OnLayerTrack();
    }

    /// <summary>层号 → 代表 Z（CSV 按 Z 分层时由主窗注入，用于滑块标签显示真实 Z）。null 表示按层号显示。</summary>
    private Dictionary<int, double>? _layerZMap;

    private void OnFilterToggle()
    {
        UpdateLayerLabel();
        FilterLayerChanged?.Invoke(_cbFilterLayer.Checked ? _layerTrack.Value : -1);
    }

    private void OnLayerTrack()
    {
        UpdateLayerLabel();
        if (_cbFilterLayer.Checked) FilterLayerChanged?.Invoke(_layerTrack.Value);
    }

    /// <summary>仅刷新"层: ..."标签文本（不发事件），供滑块滚动与文件载入共用。</summary>
    private void UpdateLayerLabel()
    {
        if (_cbFilterLayer.Checked && _layerTrack.Value >= 0)
        {
            _lblLayer.Text = (_layerZMap != null && _layerZMap.TryGetValue(_layerTrack.Value, out double z))
                ? $"层 Z={z:0.###}"
                : $"层: {_layerTrack.Value}";
        }
        else
        {
            _lblLayer.Text = "层: 全部";
        }
    }

    /// <summary>设置层过滤滑块范围；zMap 非 null 时（CSV 按 Z 分层）滑块标签显示真实 Z。
    /// 本方法仅在载入新文件时调用，故一并重置层过滤（避免上一文件的 FilterLayer 残留导致新文件空白）。</summary>
    public void SetLayerRange(int maxLayer, Dictionary<int, double>? zMap = null)
    {
        _layerZMap = zMap;
        _layerTrack.Maximum = Math.Max(0, maxLayer);
        _layerTrack.Value = -1;                 // 新文件：滑块归位
        _cbFilterLayer.Checked = false;         // 关闭层过滤（触发 OnFilterToggle → FilterLayerChanged(-1) 重置视口）
        UpdateLayerLabel();
    }

    /// <summary>设置"按材料/按层/按速度着色"复选框，并触发 ColorModeChanged（供外部强制切换显示模式）。</summary>
    public void SetColorMode(bool byTool, bool byLayer, bool bySpeed)
    {
        _cbColorTool.Checked = byTool;
        _cbColorLayer.Checked = byLayer;
        _cbColorSpeed.Checked = bySpeed;
        ColorModeChanged?.Invoke(byTool, byLayer, bySpeed);
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

    /// <summary>当前采样周期(秒)，直接读 numdt(ms)/1000。供主窗设置 Viewport3D.SpeedSamplePeriod。</summary>
    public double SamplePeriodSeconds => (double)numdt.Value / 1000.0;

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
        public readonly double Speed;       // 该点速度(mm/s)，继承所在段 m.Speed，写入 CSV 第7列供精确着色
        public readonly bool Structural;    // true=段终点等几何关键点，滤波不可剔除
        public SamplePt(double x, double y, double z, string type, int tool, double pressure, double speed, bool structural)
        {
            X = x; Y = y; Z = z; Type = type; Tool = tool; Pressure = pressure; Speed = speed; Structural = structural;
        }
    }

    /// <summary>
    /// 把 ParsedGcode 导出为 7 列点 CSV：X,Y,Z,G0/G1,T0/T1,P,Speed。末列 Speed 供按速度着色读真实速度(非点距反推)。
    /// 【速度一致性】G0 与 G1 统一按本行设计速度 V 做弧长固定步长采样，步长 step=V×samplePeriod（V 缺失或≤0 时
    ///   退化为 DefaultStep）。段内插值点布在 k·step 处，相邻距=step=V·T → 回放速度精确=V。
    ///   两道优化消除反推异常速度：①段尾余数 r=L mod step —— r<半步长时并入末段(末段距≤1.5·step)，避免随机
    ///   短间隔反推 0.5；②近零长段(L<半步长，变速/切换重合点)整段跳过，避免重点反推 0。
    ///   最终任意相邻点距∈[0.5·step,1.5·step]，反推速度∈[0.5V,1.5V]，无 0/0.5 伪影。
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

            // 近零长段（L < 半步长，典型为变速/切换处的近重合点）：整段跳过不输出点，
            // 避免产生点距≈0 的重点(反推 0 速)；其端点由相邻段覆盖，几何不受影响。
            if (L < step * 0.5) continue;

            // 固定步长布点：插值点在 k·step(k=1,2,…)处，相邻距=step=V·dt，回放速度精确=V。
            int nFull = (int)Math.Floor(L / step);      // 段内完整 step 的个数
            double r = L - nFull * step;                // 段尾余数 = L mod step
            // 段尾余数合并：余数 < 半步长时省略最后一个插值点，把余数并入末段(末段距=step+r≤1.5·step)；
            // 否则保留该插值点(末段距=r≥0.5·step)。从而任意相邻点距∈[0.5·step,1.5·step]→反推速度∈[0.5V,1.5V]，
            // 既无段尾随机短间隔(伪 0.5)、也无近重点(伪 0)。用 k·step/L 算参数避免浮点累加漂移。
            int lastK = (r < step * 0.5 && nFull >= 1) ? nFull - 1 : nFull;
            for (int k = 1; k <= lastK; k++)
            {
                double t = (k * step) / L;
                double x = m.PrevX + (m.X - m.PrevX) * t;
                double y = m.PrevY + (m.Y - m.PrevY) * t;
                double z = m.PrevZ + (m.Z - m.PrevZ) * t;
                pts.Add(new SamplePt(x, y, z, type, m.Tool, m.Pressure, m.Speed, structural: false));
            }
            // 段终点（结构点）：始终输出以保几何角点；与上一输出点距∈[0.5·step,1.5·step]。
            pts.Add(new SamplePt(m.X, m.Y, m.Z, type, m.Tool, m.Pressure, m.Speed, structural: true));
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

        // 写文件（7 列，与 CsvPathReader 兼容）：X,Y,Z,G0/G1,T0/T1,P,Speed
        // 末列 Speed=该点速度(mm/s)，使按速度着色读真实速度而非点距反推，杜绝 0/0.5 等采样伪影。
        var lines = new List<string>(kept.Count);
        foreach (var p in kept)
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "{0:F3},{1:F3},{2:F3},{3},{4},{5:F0},{6:F3}",
                p.X, p.Y, p.Z, p.Type, "T" + p.Tool, p.Pressure, p.Speed));

        File.WriteAllLines(path, lines, Encoding.UTF8);
        return lines.Count;
    }
}
