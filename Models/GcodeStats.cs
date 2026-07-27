namespace GcodeViewer.Models;

/// <summary>
/// 全局路径统计。对齐论文实验设计口径：
/// G0/G1 分别统计、空行程占比、切换次数、各工具段长度、每层明细。
/// 由 StatsCalculator 对 move 列表聚合得到，编辑后可整体重算。
/// </summary>
public sealed class GcodeStats
{
    public int LayerCount { get; set; }
    public int MoveCount { get; set; }
    public int G0Count { get; set; }
    public int G1Count { get; set; }

    public double G1PrintLength { get; set; }     // G1 打印长度 mm
    public double G0TravelLength { get; set; }    // G0 空行程长度 mm
    public double TotalLength => G1PrintLength + G0TravelLength;
    public double TravelRatio => TotalLength > 0 ? G0TravelLength / TotalLength * 100.0 : 0;

    public int ToolChangeCount { get; set; }

    public int G0G1SwitchCount { get; set; } // G0/G1 切换次数（不含工具切换）
    public double AvgToolChangeDistance { get; set; } // 相邻切换间平均路径 mm

    /// <summary>各工具号 → 该工具打印段累计长度（仅 G1）。</summary>
    public Dictionary<int, double> LengthPerTool { get; } = new();

    /// <summary>层号 → 该层统计。</summary>
    public List<LayerInfo> Layers { get; } = new();
}
