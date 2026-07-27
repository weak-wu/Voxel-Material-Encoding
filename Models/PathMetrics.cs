namespace GcodeViewer.Models;

/// <summary>
/// 单条路径的点集密度指标。
/// 线密度反映轨迹采样分辨率（点/mm），体积密度反映空间分布稀疏程度（点/mm³）。
/// </summary>
public sealed class PathMetrics
{
    public string FileName { get; init; } = string.Empty;

    /// <summary>move 总点数（含 G0/G1）。</summary>
    public int PointCount { get; init; }

    /// <summary>G1 打印点数。</summary>
    public int G1PointCount { get; init; }

    /// <summary>全路径长度（G1+G0）mm。</summary>
    public double TotalLength { get; init; }

    /// <summary>G1 打印长度 mm。</summary>
    public double PrintLength { get; init; }

    /// <summary>G0 空行程长度 mm。</summary>
    public double TravelLength { get; init; }

    /// <summary>线密度：G1 点数 / 打印长度（点/mm）。打印长度为 0 时返回 0。</summary>
    public double LinearDensity => PrintLength > 0 ? G1PointCount / PrintLength : 0;

    /// <summary>平均点间距：打印长度 / (G1点数−1) mm。反映沿轨迹的平均分辨率。</summary>
    public double AvgPointSpacing => G1PointCount > 1 ? PrintLength / (G1PointCount - 1) : 0;

    /// <summary>体积密度：总点数 / 包围盒体积（点/mm³）。</summary>
    public double VolumeDensity
    {
        get
        {
            double v = BoundsVolume;
            return v > 0 ? PointCount / v : 0;
        }
    }

    /// <summary>包围盒体积 mm³。</summary>
    public double BoundsVolume =>
        Bounds.IsValid ? Math.Max(Bounds.SizeX, 0) * Math.Max(Bounds.SizeY, 0) * Math.Max(Bounds.SizeZ, 0) : 0;

    public BoundingBox Bounds { get; init; } = new();
}

/// <summary>
/// 体素 0/1 体积统计（仅 G1 打印段，按材料细分）。
/// 将包围盒离散为边长 VoxelSize 的立方网格，被打印段穿过的体素记 1，否则 0。
/// </summary>
public sealed class VoxelStats
{
    public string FileName { get; init; } = string.Empty;

    /// <summary>体素边长 mm。</summary>
    public double VoxelSize { get; init; }

    /// <summary>三个方向网格数。</summary>
    public int NX { get; init; }
    public int NY { get; init; }
    public int NZ { get; init; }

    /// <summary>单个体素体积 mm³。</summary>
    public double VoxelVolume => VoxelSize * VoxelSize * VoxelSize;

    /// <summary>网格总体素数。</summary>
    public long TotalCells => (long)NX * NY * NZ;

    /// <summary>被任一打印段占据的体素数（即值为 1 的格子数）。</summary>
    public long OccupiedCount { get; init; }

    /// <summary>空闲体素数（值为 0）。</summary>
    public long EmptyCount => TotalCells - OccupiedCount;

    /// <summary>占据体积 mm³（1 的体积）。</summary>
    public double OccupiedVolume => OccupiedCount * VoxelVolume;

    /// <summary>空闲体积 mm³（0 的体积）。</summary>
    public double EmptyVolume => EmptyCount * VoxelVolume;

    /// <summary>网格总体积 mm³。</summary>
    public double TotalVolume => TotalCells * VoxelVolume;

    /// <summary>占据率 = 占据体积 / 总体积（%）。</summary>
    public double OccupancyRatio => TotalVolume > 0 ? OccupiedVolume / TotalVolume * 100.0 : 0;

    /// <summary>各工具占据体素数与体积。key=工具号。</summary>
    public Dictionary<int, (long Count, double Volume)> PerTool { get; init; } = new();

    /// <summary>混合体素数：被 ≥2 个工具同时占据的体素数（单喷头多材料重叠度）。</summary>
    public long MixedCount { get; init; }
}

/// <summary>
/// 候选路径相对基准路径的偏差指标（单基准对比）。
/// 沿候选路径按步长采样，逐点到基准折线的最近 3D 距离，统计分布与双向 Hausdorff。
/// </summary>
public sealed class DeviationMetrics
{
    public string FileName { get; init; } = string.Empty;

    /// <summary>基准文件名。</summary>
    public string Reference { get; init; } = string.Empty;

    /// <summary>采样步长 mm。</summary>
    public double SampleStep { get; init; }

    /// <summary>采样点数。</summary>
    public int SampleCount { get; init; }

    /// <summary>平均偏差 mm。</summary>
    public double MeanDev { get; init; }

    /// <summary>最大偏差 mm（候选→基准方向 sup）。</summary>
    public double MaxDev { get; init; }

    /// <summary>RMS 偏差 mm。</summary>
    public double RmsDev { get; init; }

    /// <summary>偏差标准差 mm。</summary>
    public double StdDev { get; init; }

    /// <summary>双向对称 Hausdorff 距离 mm。</summary>
    public double Hausdorff { get; init; }
}
