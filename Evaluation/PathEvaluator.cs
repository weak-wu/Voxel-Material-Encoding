using GcodeViewer.Models;
using GcodeViewer.Rendering;

namespace GcodeViewer.Evaluation;

/// <summary>
/// 路径评估核心算法：点集密度、体素 0/1 体积（仅 G1，按材料细分）、单基准路径偏差。
/// 所有方法对 <see cref="Path3D"/> 纯几何模型操作，与编辑/解析解耦。
///
/// 设计说明（论文口径）：
///  - 偏差用"沿候选轨迹采样 → 逐点到基准折线最近 3D 距离"的 Chamfer 式度量，
///    外加双向 Hausdorff，兼顾平均趋势与最坏偏离。
///  - 体素化用采样法光栅化 G1 段（步长 ≤ 体素/2 保证连续不漏格），按工具分别累计，
///    混合体素数衡量单喷头多材料的重叠程度。
/// </summary>
public static class PathEvaluator
{
    /// <summary>体素网格总数上限，超过则提示用户增大体素尺寸（防止内存爆炸）。</summary>
    public const long MaxGridCells = 50_000_000;

    // ===================== 几何基础 =====================

    /// <summary>
    /// 点 P 到 3D 线段 AB 的最短距离。
    /// 数学：t = clamp( ((P−A)·(B−A)) / |B−A|² , 0,1 )，最近点 C=A+t(B−A)，距离 |P−C|。
    /// 退化段（A=B）直接返回 |P−A|。
    /// </summary>
    public static double PointToSegmentDistance(Vec3 p, Vec3 a, Vec3 b)
    {
        Vec3 ab = b - a;
        double len2 = ab.X * ab.X + ab.Y * ab.Y + ab.Z * ab.Z;
        if (len2 < 1e-12) return (p - a).Length;     // 退化段
        Vec3 ap = p - a;
        double t = (ap.X * ab.X + ap.Y * ab.Y + ap.Z * ab.Z) / len2;
        t = Math.Clamp(t, 0.0, 1.0);
        Vec3 c = new(a.X + t * ab.X, a.Y + t * ab.Y, a.Z + t * ab.Z);
        return (p - c).Length;
    }

    /// <summary>点 P 到折线（段集）的最短距离：遍历所有段取最小。O(m)。</summary>
    public static double PointToPolylineDistance(Vec3 p, Path3D.Segment[] segs)
    {
        double best = double.MaxValue;
        for (int i = 0; i < segs.Length; i++)
        {
            double d = PointToSegmentDistance(p, segs[i].A, segs[i].B);
            if (d < best) best = d;
        }
        return segs.Length == 0 ? double.MaxValue : best;
    }

    // ===================== ① 点集密度 =====================

    /// <summary>计算点集密度（线密度 / 平均点间距 / 体积密度）。O(1)（值已在 Path3D 中聚合）。</summary>
    public static PathMetrics ComputeDensity(Path3D path) => new()
    {
        FileName = path.FileName,
        PointCount = path.Points.Length,
        G1PointCount = path.G1PointCount,
        TotalLength = path.PrintLength + path.TravelLength,
        PrintLength = path.PrintLength,
        TravelLength = path.TravelLength,
        Bounds = path.Bounds,
    };

    // ===================== ② 体素 0/1 体积 =====================

    /// <summary>
    /// 将 G1 打印段光栅化到边长 <paramref name="voxelSize"/> 的立方网格，统计占据/空闲体积，
    /// 并按材料(工具)分别累计。
    /// </summary>
    /// <exception cref="InvalidOperationException">网格总数超过 <see cref="MaxGridCells"/>。</exception>
    public static VoxelStats Voxelize(Path3D path, double voxelSize)
    {
        if (voxelSize <= 0)
            throw new ArgumentException("体素尺寸必须 > 0", nameof(voxelSize));

        var b = path.Bounds;
        if (!b.IsValid || path.Segments.Length == 0)
            return new VoxelStats { FileName = path.FileName, VoxelSize = voxelSize };

        // 每工具一个占据索引集；只存被占据的格，空闲数 = 总 − 占据数。
        var perToolSets = BuildPerToolSets(path, voxelSize, out int nx, out int ny, out int nz);
        double voxVol = voxelSize * voxelSize * voxelSize;

        // 并集 = 整体占据；混合体素数 = Σ各工具计数 − 并集数（被多工具命中的"额外"计数）
        var union = new HashSet<long>();
        long sumPerTool = 0;
        var perToolResult = new Dictionary<int, (long Count, double Volume)>();
        foreach (var kv in perToolSets)
        {
            union.UnionWith(kv.Value);
            sumPerTool += kv.Value.Count;
            perToolResult[kv.Key] = (kv.Value.Count, kv.Value.Count * voxVol);
        }
        long mixed = sumPerTool - union.Count;

        return new VoxelStats
        {
            FileName = path.FileName,
            VoxelSize = voxelSize,
            NX = nx, NY = ny, NZ = nz,
            OccupiedCount = union.Count,
            PerTool = perToolResult,
            MixedCount = mixed,
        };
    }

    /// <summary>
    /// 构建每工具占据体素索引集，并返回网格维度。统一供 Voxelize 与 OccupiedCenters 复用，
    /// 保证统计与点云可视化口径完全一致。索引编码：idx = k·(ny·nx) + j·nx + i。
    /// </summary>
    private static Dictionary<int, HashSet<long>> BuildPerToolSets(Path3D path, double voxel,
                                                                    out int nx, out int ny, out int nz)
    {
        var b = path.Bounds;
        nx = Math.Max(1, (int)Math.Floor(b.SizeX / voxel) + 1);
        ny = Math.Max(1, (int)Math.Floor(b.SizeY / voxel) + 1);
        nz = Math.Max(1, (int)Math.Floor(b.SizeZ / voxel) + 1);
        long total = (long)nx * ny * nz;
        if (total > MaxGridCells)
            throw new InvalidOperationException(
                $"体素网格 {nx}×{ny}×{nz} = {total} 超过上限 {MaxGridCells}，请增大体素尺寸。");

        var sets = new Dictionary<int, HashSet<long>>();
        double step = voxel * 0.5;   // 步长 ≤ 半体素，保证连续穿过不漏格
        foreach (var seg in path.Segments)
        {
            if (!sets.TryGetValue(seg.Tool, out var set))
            {
                set = new HashSet<long>();
                sets[seg.Tool] = set;
            }
            RasterizeSegment(seg.A, seg.B, b, voxel, nx, ny, nz, step, set);
        }
        return sets;
    }

    /// <summary>
    /// 返回每个占据体素的中心世界坐标 + 工具号（用于点云可视化）。
    /// 与 <see cref="Voxelize"/> 共享同一光栅化结果。
    /// </summary>
    public static List<(Vec3 Center, int Tool)> OccupiedCenters(Path3D path, double voxelSize)
    {
        var result = new List<(Vec3 Center, int Tool)>();
        if (voxelSize <= 0 || !path.Bounds.IsValid || path.Segments.Length == 0)
            return result;

        var sets = BuildPerToolSets(path, voxelSize, out int nx, out int ny, out int nz);
        var b = path.Bounds;
        foreach (var kv in sets)
        {
            foreach (long idx in kv.Value)
            {
                // 解码 idx → (i,j,k)，中心 = Min + (i+0.5)·voxel
                int i = (int)(idx % nx);
                int j = (int)(idx / nx % ny);
                int k = (int)(idx / ((long)nx * ny));
                result.Add((new Vec3(
                    b.MinX + (i + 0.5) * voxelSize,
                    b.MinY + (j + 0.5) * voxelSize,
                    b.MinZ + (k + 0.5) * voxelSize), kv.Key));
            }
        }
        return result;
    }

    /// <summary>沿段以 step 步长采样，每个采样点 floor→体素索引入集（含两端点）。</summary>
    private static void RasterizeSegment(Vec3 a, Vec3 b, BoundingBox box, double voxel,
                                         int nx, int ny, int nz, double step, HashSet<long> set)
    {
        AddCell(a, box, voxel, nx, ny, nz, set);
        AddCell(b, box, voxel, nx, ny, nz, set);
        double dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-9) return;
        int n = Math.Max(1, (int)Math.Ceiling(len / step));
        for (int i = 1; i < n; i++)
        {
            double t = (double)i / n;
            AddCell(new Vec3(a.X + t * dx, a.Y + t * dy, a.Z + t * dz), box, voxel, nx, ny, nz, set);
        }
    }

    /// <summary>把一个世界坐标点映射到体素索引并加入集合。越界点夹到合法范围（防数值误差）。</summary>
    private static void AddCell(Vec3 p, BoundingBox box, double voxel, int nx, int ny, int nz, HashSet<long> set)
    {
        int i = (int)Math.Floor((p.X - box.MinX) / voxel);
        int j = (int)Math.Floor((p.Y - box.MinY) / voxel);
        int k = (int)Math.Floor((p.Z - box.MinZ) / voxel);
        if (i < 0) i = 0; else if (i >= nx) i = nx - 1;
        if (j < 0) j = 0; else if (j >= ny) j = ny - 1;
        if (k < 0) k = 0; else if (k >= nz) k = nz - 1;
        set.Add((long)k * ny * nx + j * nx + i);
    }

    // ===================== ③ 路径偏差（单基准） =====================

    /// <summary>
    /// 候选路径相对基准路径的偏差。
    /// 沿候选 G1 段按 <paramref name="sampleStep"/> 采样，每个采样点到基准折线的最近 3D 距离
    /// 构成偏差序列，统计均值/最大/RMS/标准差；并取反向最大合成双向 Hausdorff。
    /// 复杂度 O((n+m)·m)，n、m 为采样点数，中小规模路径直接可用。
    /// </summary>
    public static DeviationMetrics Deviation(Path3D candidate, Path3D reference, double sampleStep)
    {
        if (sampleStep <= 0)
            throw new ArgumentException("采样步长必须 > 0", nameof(sampleStep));

        var refSegs = reference.Segments;
        var candSegs = candidate.Segments;

        // 无段可比较 → 返回全 0（基线场景）
        if (candSegs.Length == 0 || refSegs.Length == 0)
            return new DeviationMetrics
            {
                FileName = candidate.FileName,
                Reference = reference.FileName,
                SampleStep = sampleStep,
            };

        // 正向：候选采样点 → 基准折线
        var candPts = SamplePolyline(candSegs, sampleStep);
        double sum = 0, sumSq = 0, max = 0;
        for (int i = 0; i < candPts.Count; i++)
        {
            double d = PointToPolylineDistance(candPts[i], refSegs);
            sum += d; sumSq += d * d;
            if (d > max) max = d;
        }
        int n = candPts.Count;
        double mean = n > 0 ? sum / n : 0;
        double rms = n > 0 ? Math.Sqrt(sumSq / n) : 0;
        double variance = n > 0 ? sumSq / n - mean * mean : 0;
        double std = variance > 0 ? Math.Sqrt(variance) : 0;

        // 反向：基准采样点 → 候选折线，取最大，与正向最大合成双向 Hausdorff
        double revMax = 0;
        var refPts = SamplePolyline(refSegs, sampleStep);
        for (int i = 0; i < refPts.Count; i++)
        {
            double d = PointToPolylineDistance(refPts[i], candSegs);
            if (d > revMax) revMax = d;
        }
        double haus = Math.Max(max, revMax);

        return new DeviationMetrics
        {
            FileName = candidate.FileName,
            Reference = reference.FileName,
            SampleStep = sampleStep,
            SampleCount = n,
            MeanDev = mean,
            MaxDev = max,
            RmsDev = rms,
            StdDev = std,
            Hausdorff = haus,
        };
    }

    /// <summary>沿段集按步长采样，返回采样点列表（每段含两端点）。退化为点段只加一次。</summary>
    private static List<Vec3> SamplePolyline(Path3D.Segment[] segs, double step)
    {
        var pts = new List<Vec3>();
        for (int s = 0; s < segs.Length; s++)
        {
            var seg = segs[s];
            double dx = seg.B.X - seg.A.X, dy = seg.B.Y - seg.A.Y, dz = seg.B.Z - seg.A.Z;
            double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            pts.Add(seg.A);
            if (len < 1e-9) continue;       // 退化段：A 已加入，跳过中间与 B（B==A）
            int n = Math.Max(1, (int)Math.Ceiling(len / step));
            for (int i = 1; i < n; i++)
            {
                double t = (double)i / n;
                pts.Add(new Vec3(seg.A.X + t * dx, seg.A.Y + t * dy, seg.A.Z + t * dz));
            }
            pts.Add(seg.B);
        }
        return pts;
    }
}
