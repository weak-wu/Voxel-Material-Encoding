using NetTopologySuite.Geometries;
using static GcodeViewer.Geometry.CfsGeometry;

namespace GcodeViewer.Geometry;

/// <summary>
/// Curve post-optimization from Zhao et al., "Connected Fermat Spirals for
/// Layered Fabrication", Appendix, equations (1)-(6).  The curve is sampled
/// more densely around high-curvature regions and optimized with the three
/// paper terms: displacement, chord-length Laplacian smoothness (alpha=200),
/// and inter-turn spacing (beta=1).
/// </summary>
internal static class CfsPathOptimizer
{
    private const double SmoothWeight = 200.0;
    private const double SpacingWeight = 1.0;
    private const int MaximumSamples = 1200;
    private readonly record struct Footpoint(int Point, int Other, double T, bool IsVertex);
    private sealed record Row(int[] Indices, double[] Values, double Target, double Weight);

    internal static List<Coordinate> Optimize(
        IReadOnlyList<Coordinate> source, double targetSpacing,
        Polygon domain, CancellationToken token)
    {
        var original = CurvatureAdaptiveSample(source, targetSpacing);
        if (original.Count < 3) return original;

        var current = original.Select(p => new Coordinate(p)).ToList();
        for (int outer = 0; outer < 8; outer++)
        {
            token.ThrowIfCancellationRequested();
            var outerStart = current.Select(p => new Coordinate(p)).ToList();
            var footpoints = FindFootpoints(current, targetSpacing, token);

            // Gauss-Newton loop with fixed footpoint identities.  The
            // distances and their gradients are linearized at every step.
            for (int inner = 0; inner < 10; inner++)
            {
                var rows = BuildRows(original, current, footpoints, targetSpacing);
                var solved = Solve(rows, Flatten(current), token);
                var next = Unflatten(solved);
                double displacement = MaxDisplacement(current, next);
                current = next;
                if (displacement < 1e-6) break;
            }

            if (MaxDisplacement(outerStart, current) < 1e-5) break;
        }

        if (current.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
            return original;

        var optimizedLine = Line(current);
        var originalLine = Line(original);
        if (originalLine.IsSimple && !optimizedLine.IsSimple)
            return original;

        // The paper optimizes a fill curve inside R.  Reject a numerical
        // solution that leaves the source region or cuts through a hole.
        var tolerance = Math.Max(1e-7, targetSpacing * 1e-5);
        if (!domain.Buffer(tolerance).Covers(optimizedLine))
            return original;

        return current;
    }

    private static List<Coordinate> CurvatureAdaptiveSample(
        IReadOnlyList<Coordinate> source, double spacing)
    {
        var points = new List<Coordinate>();
        foreach (var point in source)
            if (points.Count == 0 || points[^1].Distance(point) > 1e-9)
                points.Add(new Coordinate(point));
        if (points.Count < 3) return points;

        int n = points.Count;
        var curvature = new double[n];
        for (int i = 1; i + 1 < n; i++)
        {
            var a = points[i - 1]; var b = points[i]; var c = points[i + 1];
            double ux = b.X - a.X, uy = b.Y - a.Y;
            double vx = c.X - b.X, vy = c.Y - b.Y;
            double ul = Math.Sqrt(ux * ux + uy * uy);
            double vl = Math.Sqrt(vx * vx + vy * vy);
            if (ul <= 1e-12 || vl <= 1e-12) continue;
            double cosine = Math.Clamp((ux * vx + uy * vy) / (ul * vl), -1, 1);
            curvature[i] = Math.Acos(cosine) / Math.PI;
        }

        var segmentWeight = new double[n - 1];
        var cumulative = new double[n];
        double length = 0;
        for (int i = 0; i + 1 < n; i++)
        {
            double segmentLength = points[i].Distance(points[i + 1]);
            length += segmentLength;
            segmentWeight[i] = segmentLength * (1 + 4 * (curvature[i] + curvature[i + 1]) * .5);
            cumulative[i + 1] = cumulative[i] + segmentWeight[i];
        }
        if (length <= 1e-9 || cumulative[^1] <= 1e-9) return points;

        int samples = Math.Clamp(
            (int)Math.Ceiling(length / Math.Max(spacing * .5, 1e-6)) + 1,
            Math.Min(20, n), MaximumSamples);
        var result = new List<Coordinate>(samples);
        int segment = 0;
        for (int k = 0; k < samples; k++)
        {
            double target = cumulative[^1] * k / Math.Max(1, samples - 1);
            while (segment + 1 < cumulative.Length - 1 && cumulative[segment + 1] < target)
                segment++;
            double weight = Math.Max(segmentWeight[segment], 1e-12);
            double t = Math.Clamp((target - cumulative[segment]) / weight, 0, 1);
            var a = points[segment]; var b = points[segment + 1];
            result.Add(new Coordinate(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
        }
        result[0] = new Coordinate(points[0]);
        result[^1] = new Coordinate(points[^1]);
        return result;
    }

    private static List<Footpoint> FindFootpoints(
        IReadOnlyList<Coordinate> points, double spacing, CancellationToken token)
    {
        int n = points.Count;
        var arc = new double[n];
        for (int i = 1; i < n; i++) arc[i] = arc[i - 1] + points[i - 1].Distance(points[i]);
        var result = new List<Footpoint>();
        double maximumDistance = spacing * 2;
        double minimumDistance = spacing * .25;
        double minimumArcGap = spacing * 2.5;

        for (int i = 0; i < n; i++)
        {
            if ((i & 31) == 0) token.ThrowIfCancellationRequested();
            double bestDistance = double.MaxValue;
            int bestSegment = -1;
            double bestT = 0;
            for (int j = 0; j + 1 < n; j++)
            {
                if (Math.Abs(i - j) <= 5 || Math.Abs(i - (j + 1)) <= 5) continue;
                double arcGap = Math.Min(Math.Abs(arc[i] - arc[j]), Math.Abs(arc[i] - arc[j + 1]));
                if (arcGap < minimumArcGap) continue;

                var a = points[j]; var b = points[j + 1];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double length2 = dx * dx + dy * dy;
                double t = length2 <= 1e-18 ? 0 :
                    ((points[i].X - a.X) * dx + (points[i].Y - a.Y) * dy) / length2;
                t = Math.Clamp(t, 0, 1);
                double fx = a.X + dx * t, fy = a.Y + dy * t;
                double ex = points[i].X - fx, ey = points[i].Y - fy;
                double distance = Math.Sqrt(ex * ex + ey * ey);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestSegment = j;
                    bestT = t;
                }
            }
            if (bestSegment < 0 || bestDistance < minimumDistance || bestDistance > maximumDistance)
                continue;
            if (bestT <= 1e-6)
                result.Add(new Footpoint(i, bestSegment, 0, true));
            else if (bestT >= 1 - 1e-6)
                result.Add(new Footpoint(i, bestSegment + 1, 0, true));
            else
                result.Add(new Footpoint(i, bestSegment, bestT, false));
        }
        return result;
    }

    private static List<Row> BuildRows(
        IReadOnlyList<Coordinate> original, IReadOnlyList<Coordinate> current,
        IReadOnlyList<Footpoint> footpoints, double spacing)
    {
        int n = original.Count;
        var rows = new List<Row>(n * 4);

        // f_reg: squared displacement from the curvature-adaptive samples.
        for (int i = 0; i < n; i++)
        {
            rows.Add(new Row(new[] { i }, new[] { 1.0 }, original[i].X, 1));
            rows.Add(new Row(new[] { n + i }, new[] { 1.0 }, original[i].Y, 1));
        }

        // f_smooth: chord-length weighted discrete Laplacian.
        for (int i = 0; i + 2 < n; i++)
        {
            double d1 = original[i].Distance(original[i + 1]);
            double d2 = original[i + 1].Distance(original[i + 2]);
            double u = d1 + d2 > 1e-12 ? d1 / (d1 + d2) : .5;
            var values = new[] { 1 - u, -1.0, u };
            rows.Add(new Row(new[] { i, i + 1, i + 2 }, values, 0, SmoothWeight));
            rows.Add(new Row(new[] { n + i, n + i + 1, n + i + 2 }, values, 0, SmoothWeight));
        }

        // f_space: equations (3)-(5), linearized at the current iterate.
        foreach (var footprint in footpoints)
        {
            int i = footprint.Point;
            if (footprint.IsVertex)
            {
                int j = footprint.Other;
                double dx = current[i].X - current[j].X;
                double dy = current[i].Y - current[j].Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance <= 1e-10) continue;
                double gx = dx / distance, gy = dy / distance;
                double target = spacing - distance +
                    gx * current[i].X + gy * current[i].Y -
                    gx * current[j].X - gy * current[j].Y;
                rows.Add(new Row(
                    new[] { i, n + i, j, n + j },
                    new[] { gx, gy, -gx, -gy }, target, SpacingWeight));
            }
            else
            {
                int j = footprint.Other;
                double t = footprint.T;
                double fx = (1 - t) * current[j].X + t * current[j + 1].X;
                double fy = (1 - t) * current[j].Y + t * current[j + 1].Y;
                double dx = current[i].X - fx, dy = current[i].Y - fy;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance <= 1e-10) continue;
                double gx = dx / distance, gy = dy / distance;
                var indices = new[] { i, n + i, j, n + j, j + 1, n + j + 1 };
                var values = new[]
                {
                    gx, gy, -(1 - t) * gx, -(1 - t) * gy, -t * gx, -t * gy
                };
                double target = spacing - distance;
                for (int k = 0; k < indices.Length; k++)
                    target += values[k] * Value(current, indices[k]);
                rows.Add(new Row(indices, values, target, SpacingWeight));
            }
        }
        return rows;
    }

    private static double Value(IReadOnlyList<Coordinate> points, int index)
        => index < points.Count ? points[index].X : points[index - points.Count].Y;

    private static double[] Flatten(IReadOnlyList<Coordinate> points)
    {
        int n = points.Count;
        var values = new double[n * 2];
        for (int i = 0; i < n; i++) { values[i] = points[i].X; values[n + i] = points[i].Y; }
        return values;
    }

    private static List<Coordinate> Unflatten(double[] values)
    {
        int n = values.Length / 2;
        return Enumerable.Range(0, n).Select(i => new Coordinate(values[i], values[n + i])).ToList();
    }

    private static double MaxDisplacement(
        IReadOnlyList<Coordinate> first, IReadOnlyList<Coordinate> second)
    {
        double maximum = 0;
        for (int i = 0; i < first.Count; i++) maximum = Math.Max(maximum, first[i].Distance(second[i]));
        return maximum;
    }

    private static double[] Solve(
        IReadOnlyList<Row> rows, double[] initial, CancellationToken token)
    {
        int n = initial.Length;
        var rhs = new double[n];
        var diagonal = new double[n];
        foreach (var row in rows)
            for (int i = 0; i < row.Indices.Length; i++)
            {
                int index = row.Indices[i];
                rhs[index] += row.Weight * row.Values[i] * row.Target;
                diagonal[index] += row.Weight * row.Values[i] * row.Values[i];
            }

        double[] Multiply(double[] vector)
        {
            var value = new double[n];
            foreach (var row in rows)
            {
                double dot = 0;
                for (int i = 0; i < row.Indices.Length; i++) dot += row.Values[i] * vector[row.Indices[i]];
                for (int i = 0; i < row.Indices.Length; i++)
                    value[row.Indices[i]] += row.Weight * row.Values[i] * dot;
            }
            return value;
        }

        var x = initial.ToArray();
        var ax = Multiply(x);
        var residual = rhs.Zip(ax, (a, b) => a - b).ToArray();
        var z = residual.Select((value, i) => value / Math.Max(diagonal[i], 1e-12)).ToArray();
        var direction = z.ToArray();
        double rz = Dot(residual, z);
        double rhsNorm = Math.Max(1, Math.Sqrt(Dot(rhs, rhs)));
        for (int iteration = 0; iteration < Math.Min(2500, n * 10); iteration++)
        {
            if ((iteration & 63) == 0) token.ThrowIfCancellationRequested();
            var product = Multiply(direction);
            double denominator = Dot(direction, product);
            if (Math.Abs(denominator) < 1e-24) break;
            double step = rz / denominator;
            for (int i = 0; i < n; i++)
            {
                x[i] += step * direction[i];
                residual[i] -= step * product[i];
            }
            if (Math.Sqrt(Dot(residual, residual)) / rhsNorm < 1e-9) break;
            z = residual.Select((value, i) => value / Math.Max(diagonal[i], 1e-12)).ToArray();
            double nextRz = Dot(residual, z);
            double beta = nextRz / Math.Max(Math.Abs(rz), 1e-30);
            for (int i = 0; i < n; i++) direction[i] = z[i] + beta * direction[i];
            rz = nextRz;
        }
        return x.All(double.IsFinite) ? x : initial;
    }

    private static double Dot(double[] a, double[] b)
    {
        double value = 0;
        for (int i = 0; i < a.Length; i++) value += a[i] * b[i];
        return value;
    }
}
