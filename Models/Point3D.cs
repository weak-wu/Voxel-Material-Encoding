namespace GcodeViewer.Models;

/// <summary>
/// 路径点模型（自 AMCP.FrmPrintStep2.Point3D 移植）。
/// 用于"路径生成"流程：从 G-code 解析出的三维路径点，携带打印工艺参数。
/// 与 GcodeViewer 的 GcodeMove（编辑用）解耦——本类仅承载几何与工艺信息，
/// 需要渲染时再由 PathGenerator.ToParsedGcode 转换为 ParsedGcode。
/// </summary>
public class Point3D
{
    /// <summary>坐标 X/Y/Z（mm）。</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    /// <summary>挤出标志：1=打印(G1)，0=空行程(G0)。</summary>
    public int Extrude { get; set; }

    /// <summary>打印速度 V（mm/s 或 mm/min，取决于源文件）。</summary>
    public double Feed { get; set; }

    /// <summary>打印气压 P（kPa）。</summary>
    public double Pressure { get; set; }

    /// <summary>喷头/工具号 T（0 或 1，对应双材料）。</summary>
    public int Tool { get; set; }

    /// <summary>层号（来自 ;LAYER 注释）。</summary>
    public int Layer { get; set; }

    /// <summary>倾斜网格类型（4D 打印用，本查看器暂不使用）。</summary>
    public int GridType { get; set; }

    /// <summary>双材料标志：true=材料A。</summary>
    public bool MaterialA { get; set; }

    public Point3D()
    {
    }

    public Point3D(double x, double y, double z = 0, int extrude = 1, double feed = 0,
                   double pressure = 0, int tool = 0, int layer = 0, int gridType = 0, bool materialA = true)
    {
        X = x;
        Y = y;
        Z = z;
        Extrude = extrude;
        Feed = feed;
        Pressure = pressure;
        Tool = tool;
        Layer = layer;
        GridType = gridType;
        MaterialA = materialA;
    }

    /// <summary>当前点到目标点的欧氏距离（mm）。</summary>
    public double DistanceTo(Point3D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        double dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>向量减法（自 AMCP 移植，用于 FilterDensePoints 等路径处理）。</summary>
    public static Point3D operator -(Point3D a, Point3D b)
    {
        return new Point3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.Extrude, a.Feed, a.Pressure, a.Tool, a.Layer, a.GridType, a.MaterialA);
    }

    /// <summary>向量加法（自 AMCP 移植）。</summary>
    public static Point3D operator +(Point3D a, Point3D b)
    {
        return new Point3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z, b.Extrude, b.Feed, b.Pressure, b.Tool, b.Layer, b.GridType, b.MaterialA);
    }

    /// <summary>标量乘法（自 AMCP 移植）。</summary>
    public static Point3D operator *(double m, Point3D a)
    {
        return new Point3D(m * a.X, m * a.Y, m * a.Z, a.Extrude, a.Feed, a.Pressure, a.Tool, a.Layer, a.GridType, a.MaterialA);
    }

    /// <summary>向量模长（自 AMCP 移植）。</summary>
    public double Length(Point3D a)
    {
        return Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
    }

    /// <summary>单位向量（自 AMCP 移植）。</summary>
    public Point3D Unitvector(Point3D a)
    {
        double length = Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
        if (length < 1e-6) return new Point3D(0, 0, 0, a.Extrude, a.Feed, a.Pressure, a.Tool, a.Layer, a.GridType, a.MaterialA);
        return new Point3D(a.X / length, a.Y / length, a.Z / length, a.Extrude, a.Feed, a.Pressure, a.Tool, a.Layer, a.GridType, a.MaterialA);
    }

    public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
}
