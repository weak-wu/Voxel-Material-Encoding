namespace GcodeViewer.Parsing;

/// <summary>
/// 提前出丝(Advance)的实际生效统计：按材料 A(T0)/B(T1) 汇总每次切换的实际提前量，
/// 用于回显设定值与实际范围、保留段长的额外前移次数及起点前补偿长度。
/// 由 AdvancePlanner 在每次切换处累加(Record)，跨层共享同一实例。
/// </summary>
public sealed class AdvanceStats
{
    /// <summary>各层在原始起点前补偿的路径长度总和(mm)。</summary>
    public double StartCompensationLength { get; set; }
    public int CompensatedLayerCount { get; set; }

    /// <summary>材料 A(T0) 切换次数。</summary>
    public int SwitchCount0 { get; set; }

    /// <summary>材料 B(T1) 切换次数。</summary>
    public int SwitchCount1 { get; set; }

    /// <summary>A(T0) 实际生效提前量的最小值(最受限的一次)；无切换时为 +∞。</summary>
    public double MinActual0 { get; set; } = double.PositiveInfinity;

    /// <summary>B(T1) 实际生效提前量的最小值；无切换时为 +∞。</summary>
    public double MinActual1 { get; set; } = double.PositiveInfinity;

    public double MaxActual0 { get; set; }
    public double MaxActual1 { get; set; }

    /// <summary>为了保留后续段长，向初始段传递而增加提前量的次数。</summary>
    public int ExtendedCount0 { get; set; }
    public int ExtendedCount1 { get; set; }

    /// <summary>A(T0) 被限幅(actualAdv &lt; 设定值)的切换次数。</summary>
    public int ClampedCount0 { get; set; }

    /// <summary>B(T1) 被限幅的切换次数。</summary>
    public int ClampedCount1 { get; set; }

    /// <summary>记录一次切换的实际提前量：tool==0→A，其余→B。
    /// <paramref name="requested"/>=设定提前量，<paramref name="actual"/>=限幅后实际值，
    /// <paramref name="eps"/>=判定限幅的阈值(actual &lt; requested−eps 视为被限幅)。</summary>
    public void Record(int tool, double requested, double actual, double eps)
    {
        if (tool == 0)
        {
            SwitchCount0++;
            if (actual < MinActual0) MinActual0 = actual;
            if (actual > MaxActual0) MaxActual0 = actual;
            if (actual > requested + eps) ExtendedCount0++;
            if (actual < requested - eps) ClampedCount0++;
        }
        else
        {
            SwitchCount1++;
            if (actual < MinActual1) MinActual1 = actual;
            if (actual > MaxActual1) MaxActual1 = actual;
            if (actual > requested + eps) ExtendedCount1++;
            if (actual < requested - eps) ClampedCount1++;
        }
    }
}
