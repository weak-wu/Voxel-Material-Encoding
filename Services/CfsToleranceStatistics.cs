using System.Diagnostics;

namespace GcodeViewer.Services;

public sealed record CfsToleranceRow(decimal ToleranceMm, int PathCount, long PointCount,
    long BaselinePointCount, long Reduction, double ReductionPercent, long ElapsedMs);

public static class CfsToleranceStatistics
{
    // Decimal arithmetic avoids skipped endpoints caused by floating-point accumulation.
    // Always include the requested end, even when the last interval is shorter.
    public static IReadOnlyList<decimal> BuildTolerances(decimal start, decimal end, decimal step)
    {
        if (start < 0 || end < start)
            throw new ArgumentException("容差必须非负，且结束容差不能小于起始容差。");
        if (step <= 0) throw new ArgumentException("容差步长必须大于 0。");
        var values = new List<decimal> { start };
        while (values[^1] < end)
        {
            if (values.Count >= 1000)
                throw new ArgumentException("一次最多测试 1000 个容差值，请增大步长或缩小范围。");
            values.Add(values[^1] + Math.Min(step, end - values[^1]));
        }
        return values;
    }

    public static IReadOnlyList<CfsToleranceRow> Run(Bitmap image, CfsOptions options,
        decimal start, decimal end, decimal step,
        IProgress<CfsToleranceRow>? progress = null, CancellationToken token = default)
    {
        var tolerances = BuildTolerances(start, end, step);
        // Snapshot all options; only path simplification changes across runs.
        var settings = new CfsOptions
        {
            SpiralType = options.SpiralType, PixelSizeMm = options.PixelSizeMm,
            ScaleFactor = options.ScaleFactor, SpacingMm = options.SpacingMm,
            BoundaryMm = options.BoundaryMm, PointSpacingMm = options.PointSpacingMm,
            PolygonSimplifyToleranceMm = options.PolygonSimplifyToleranceMm,
            Optimize = options.Optimize, Layers = options.Layers,
            LayerHeightMm = options.LayerHeightMm, SimplifyToleranceMm = 0
        };
        var algorithm = new CfsNativeAlgorithm();
        token.ThrowIfCancellationRequested();
        var timer = Stopwatch.StartNew();
        var baseline = algorithm.Generate(image, settings, token);
        timer.Stop();
        if (baseline.Paths.Count == 0)
            throw new InvalidOperationException("没有生成有效路径，请检查图像前景和路径参数。");
        long baselinePoints = baseline.Paths.Sum(p => (long)p.Count);
        long baselineMs = timer.ElapsedMilliseconds;
        var rows = new List<CfsToleranceRow>();
        foreach (decimal tolerance in tolerances)
        {
            token.ThrowIfCancellationRequested();
            settings.SimplifyToleranceMm = (double)tolerance;
            timer.Restart();
            var result = tolerance == 0 ? baseline : algorithm.Generate(image, settings, token);
            timer.Stop();
            token.ThrowIfCancellationRequested();
            if (result.Paths.Count == 0)
                throw new InvalidOperationException($"容差 {tolerance} mm 没有生成有效路径。");
            long points = result.Paths.Sum(p => (long)p.Count);
            long reduction = baselinePoints - points;
            var row = new CfsToleranceRow(tolerance, result.Paths.Count, points,
                baselinePoints, reduction, 100.0 * reduction / baselinePoints,
                tolerance == 0 ? baselineMs : timer.ElapsedMilliseconds);
            rows.Add(row);
            progress?.Report(row);
        }
        return rows;
    }
}
