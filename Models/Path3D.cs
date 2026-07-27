using GcodeViewer.Rendering;

namespace GcodeViewer.Models;

/// <summary>
/// 纯几何路径模型，供评估模块（偏差 / 密度 / 体素）使用。
/// 与编辑用的 ParsedGcode 解耦：只保留评估所需的点序列与 G1 打印段，
/// 避免 ListView/Line 等无关数据进入计算。
/// </summary>
public sealed class Path3D
{
    /// <summary>一条 G1 打印段：起点 A、终点 B、所属工具号 Tool。</summary>
    public readonly struct Segment
    {
        public readonly Vec3 A, B;
        public readonly int Tool;
        public Segment(Vec3 a, Vec3 b, int tool) { A = a; B = b; Tool = tool; }

        /// <summary>段长（3D 欧氏距离）。</summary>
        public double Length => (B - A).Length;
    }

    /// <summary>源文件名（仅文件名，不含目录）。</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>所有 move 终点（含 G0/G1），用于点数/密度统计。</summary>
    public Vec3[] Points { get; init; } = Array.Empty<Vec3>();

    /// <summary>G1 打印段集合（偏差基准与体素化的对象）。</summary>
    public Segment[] Segments { get; init; } = Array.Empty<Segment>();

    /// <summary>路径包围盒（直接复用解析结果的 Bounds）。</summary>
    public BoundingBox Bounds { get; init; } = new();

    /// <summary>G1 打印总长 mm。</summary>
    public double PrintLength { get; init; }

    /// <summary>G0 空行程总长 mm。</summary>
    public double TravelLength { get; init; }

    /// <summary>G1 打印点数（用于线密度分母）。</summary>
    public int G1PointCount { get; init; }

    /// <summary>
    /// 从解析结果提取纯几何路径。
    /// 仅 G1 段进入 Segments（偏差/体素口径）；Points 收集全部 move 终点（密度统计）。
    /// </summary>
    public static Path3D FromParsed(ParsedGcode g)
    {
        var pts = new Vec3[g.Moves.Count];
        var segs = new List<Segment>(g.Moves.Count);
        double printLen = 0, travelLen = 0;
        int g1Pts = 0;

        for (int i = 0; i < g.Moves.Count; i++)
        {
            var m = g.Moves[i];
            pts[i] = new Vec3(m.X, m.Y, m.Z);

            if (m.Type == MoveType.G1)
            {
                // 段起点取 Prev（上一 move 终点），与 GcodeMove.RecomputeSegmentLength 口径一致
                segs.Add(new Segment(
                    new Vec3(m.PrevX, m.PrevY, m.PrevZ),
                    new Vec3(m.X, m.Y, m.Z),
                    m.Tool));
                printLen += m.SegmentLength;
                g1Pts++;
            }
            else
            {
                travelLen += m.SegmentLength;
            }
        }

        return new Path3D
        {
            FileName = Path.GetFileName(g.SourcePath),
            Points = pts,
            Segments = segs.ToArray(),
            Bounds = g.Bounds,
            PrintLength = printLen,
            TravelLength = travelLen,
            G1PointCount = g1Pts,
        };
    }
}
