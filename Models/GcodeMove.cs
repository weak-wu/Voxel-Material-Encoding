namespace GcodeViewer.Models;

/// <summary>
/// 一条 G0/G1 移动指令。坐标采用绝对值（已按 G90/G91 解析归一化）。
/// 每条 move 同时持有指向源行的引用，编辑后可同步重写原始文本。
/// </summary>
public sealed class GcodeMove
{
    /// <summary>在 ParsedGcode.Moves 列表中的下标。</summary>
    public int Index { get; set; }

    /// <summary>源文件行号（0-based）。</summary>
    public int LineNumber { get; set; }

    /// <summary>本指令结束时所在层（从 ;LAYER 注释推导）。</summary>
    public int Layer { get; set; }

    /// <summary>移动类型：G0=空行程，G1=打印。</summary>
    public MoveType Type { get; set; }

    /// <summary>当前工具号（T0/T1/Tn），用于材料切换统计与按工具着色。</summary>
    public int Tool { get; set; }

    /// <summary>终点绝对坐标。</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    /// <summary>本行原始解析出的轴标志，决定保存时是否重写该轴 token。</summary>
    public bool HasX { get; set; }
    public bool HasY { get; set; }
    public bool HasZ { get; set; }

    /// <summary>起点（上一条 move 的终点，或文件起始位置）。供距离/重算使用。</summary>
    public double PrevX { get; set; }
    public double PrevY { get; set; }
    public double PrevZ { get; set; }

    /// <summary>本段移动距离（3D 欧氏距离，mm）。</summary>
    public double SegmentLength { get; set; }

    /// <summary>从文件起点到本点（含本段）的累计路径长度。</summary>
    public double CumulativeLength { get; set; }

    /// <summary>用户是否手动修改过本 move（类型或坐标）。</summary>
    public bool IsModified { get; set; }

    /// <summary>本点是否为材料切换点（工具号相对上一 move 发生变化）。</summary>
    public bool IsToolChange { get; set; }

    /// <summary>本点是否为G0/G1切换点。</summary>
    public bool IsG0G1Switch { get; set; }

    /// <summary>气压值 P（仅 CSV 路径文件第 6 列；gcode 文件恒为 0）。</summary>
    public double Pressure { get; set; }

    /// <summary>本段移动速度 V（来自 G1 行内 V 参数，mm/s）。无 V 时为 0，导出时退化为默认步长。</summary>
    public double Speed { get; set; }

    /// <summary>计算并返回本段移动距离。</summary>
    public static double Distance(double ax, double ay, double az, double bx, double by, double bz)
    {
        double dx = bx - ax, dy = by - ay, dz = bz - az;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public double RecomputeSegmentLength()
    {
        SegmentLength = Distance(PrevX, PrevY, PrevZ, X, Y, Z);
        return SegmentLength;
    }
}

public enum MoveType
{
    G0, // 空行程 / 快速移动
    G1  // 打印 / 线性插补
}
