using NetTopologySuite.Geometries;
using Geo = NetTopologySuite.Geometries.Geometry;

namespace GcodeViewer.Geometry;

/// <summary>
/// Dependency-free quadratic least-squares port of optimization_total.py.
/// The regularisation, chord-length Laplacian and linearised inter-contour
/// spacing terms are identical; conjugate gradients replaces CVXPY's solver.
/// </summary>
internal static class CfsPolygonOptimizer
{
    private readonly record struct Footprint(
        int PointA, int VertexB, int SegmentB, double T, double Distance, bool IsVertex);

    private sealed record Row(int[] Indices, double[] Values, double Target, double Weight);

    internal static List<Polygon> Optimize(
        IReadOnlyList<Polygon> polygons, double distance,
        double regularization = 50, double smoothing = 5, double spacing = 5)
    {
        if (polygons.Count == 0) return new();
        var coordinates = polygons.Select(p => p.ExteriorRing.Coordinates).ToArray();
        var footprints = new List<Footprint>[Math.Max(0, polygons.Count - 1)];
        for (int i = 0; i < footprints.Length; i++)
            footprints[i] = FindNearest(coordinates[i], coordinates[i + 1]);

        var result = new List<Polygon>();
        for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
        {
            var source = coordinates[polygonIndex];
            if (source.Length < 4) { result.Add(polygons[polygonIndex]); continue; }
            int n = source.Length;
            var rows = new List<Row>(n * 4);

            for (int i = 0; i < n; i++)
            {
                rows.Add(new(new[] { i }, new[] { 1.0 }, source[i].X, regularization));
                rows.Add(new(new[] { n + i }, new[] { 1.0 }, source[i].Y, regularization));
            }

            for (int i = 0; i + 2 < n; i++)
            {
                double d1 = source[i].Distance(source[i + 1]);
                double d2 = source[i + 1].Distance(source[i + 2]);
                double u = d1 + d2 > 1e-9 ? d1 / (d1 + d2) : .5;
                var values = new[] { 1 - u, -1.0, u };
                rows.Add(new(new[] { i, i + 1, i + 2 }, values, 0, smoothing));
                rows.Add(new(new[] { n + i, n + i + 1, n + i + 2 }, values, 0, smoothing));
            }

            if (polygonIndex < footprints.Length)
                AddOuterRows(rows, footprints[polygonIndex], source,
                    coordinates[polygonIndex + 1], distance, spacing);
            if (polygonIndex > 0)
                AddInnerRows(rows, footprints[polygonIndex - 1],
                    coordinates[polygonIndex - 1], source, distance, spacing);

            var initial = new double[n * 2];
            for (int i = 0; i < n; i++) { initial[i] = source[i].X; initial[n + i] = source[i].Y; }
            var solved = Solve(rows, initial);
            var ring = Enumerable.Range(0, n)
                .Select(i => new Coordinate(solved[i], solved[n + i])).ToArray();
            ring[^1] = new Coordinate(ring[0]);

            try
            {
                var optimized = CfsGeometry.Factory.CreatePolygon(ring);
                Geo fixedGeometry = optimized.IsValid ? optimized : optimized.Buffer(0);
                result.Add(CfsGeometry.Polygons(fixedGeometry).OrderByDescending(p => p.Area).FirstOrDefault()
                    ?? polygons[polygonIndex]);
            }
            catch (ArgumentException)
            {
                result.Add(polygons[polygonIndex]);
            }
        }
        return result;
    }

    private static List<Footprint> FindNearest(Coordinate[] a, Coordinate[] b)
    {
        var result = new List<Footprint>(a.Length);
        if (a.Length == 0 || b.Length == 0) return result;
        for (int i = 0; i < a.Length; i++)
        {
            double vertexDistance = double.MaxValue;
            int vertex = -1;
            for (int j = 0; j < b.Length; j++)
            {
                double d = a[i].Distance(b[j]);
                if (d < vertexDistance) { vertexDistance = d; vertex = j; }
            }

            double segmentDistance = double.MaxValue;
            int segment = -1;
            double segmentT = 0;
            for (int j = 0; j + 1 < b.Length; j++)
            {
                var ab = new Coordinate(b[j + 1].X - b[j].X, b[j + 1].Y - b[j].Y);
                double length2 = ab.X * ab.X + ab.Y * ab.Y;
                double t = length2 <= 1e-18 ? 0 :
                    ((a[i].X - b[j].X) * ab.X + (a[i].Y - b[j].Y) * ab.Y) / length2;
                t = Math.Clamp(t, 0, 1);
                var foot = new Coordinate(b[j].X + ab.X * t, b[j].Y + ab.Y * t);
                double d = a[i].Distance(foot);
                if (d < segmentDistance) { segmentDistance = d; segment = j; segmentT = t; }
            }
            bool useVertex = vertexDistance <= segmentDistance;
            result.Add(new(i, vertex, segment, segmentT,
                useVertex ? vertexDistance : segmentDistance, useVertex));
        }
        return result;
    }

    private static void AddOuterRows(List<Row> rows, IEnumerable<Footprint> pairs,
        Coordinate[] current, Coordinate[] neighbour, double targetDistance, double weight)
    {
        int n = current.Length;
        foreach (var pair in pairs)
        {
            Coordinate foot = pair.IsVertex
                ? neighbour[pair.VertexB]
                : Interpolate(neighbour[pair.SegmentB], neighbour[pair.SegmentB + 1], pair.T);
            var g = Direction(current[pair.PointA], foot, pair.Distance);
            if (g == null) continue;
            double target = targetDistance - pair.Distance +
                g.Value.X * current[pair.PointA].X + g.Value.Y * current[pair.PointA].Y;
            rows.Add(new(new[] { pair.PointA, n + pair.PointA },
                new[] { g.Value.X, g.Value.Y }, target, weight));
        }
    }

    private static void AddInnerRows(List<Row> rows, IEnumerable<Footprint> pairs,
        Coordinate[] neighbour, Coordinate[] current, double targetDistance, double weight)
    {
        int n = current.Length;
        foreach (var pair in pairs)
        {
            var point = neighbour[pair.PointA];
            if (pair.IsVertex)
            {
                int j = pair.VertexB;
                var g = Direction(point, current[j], pair.Distance);
                if (g == null) continue;
                double cx = -g.Value.X, cy = -g.Value.Y;
                double target = targetDistance - pair.Distance + cx * current[j].X + cy * current[j].Y;
                rows.Add(new(new[] { j, n + j }, new[] { cx, cy }, target, weight));
            }
            else
            {
                int j = pair.SegmentB;
                var foot = Interpolate(current[j], current[j + 1], pair.T);
                var g = Direction(point, foot, pair.Distance);
                if (g == null) continue;
                double a = -(1 - pair.T), b = -pair.T;
                var values = new[] { a * g.Value.X, a * g.Value.Y, b * g.Value.X, b * g.Value.Y };
                var indices = new[] { j, n + j, j + 1, n + j + 1 };
                double target = targetDistance - pair.Distance;
                for (int k = 0; k < indices.Length; k++) target += values[k] * Initial(current, indices[k]);
                rows.Add(new(indices, values, target, weight));
            }
        }
    }

    private static (double X, double Y)? Direction(Coordinate from, Coordinate to, double length)
        => length < 1e-10 ? null : ((from.X - to.X) / length, (from.Y - to.Y) / length);

    private static Coordinate Interpolate(Coordinate a, Coordinate b, double t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static double Initial(Coordinate[] points, int combinedIndex)
        => combinedIndex < points.Length ? points[combinedIndex].X : points[combinedIndex - points.Length].Y;

    private static double[] Solve(IReadOnlyList<Row> rows, double[] initial)
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
        var r = rhs.Zip(ax, (a, b) => a - b).ToArray();
        var z = r.Select((v, i) => v / Math.Max(diagonal[i], 1e-12)).ToArray();
        var p = z.ToArray();
        double rz = Dot(r, z);
        double rhsNorm = Math.Max(1, Math.Sqrt(Dot(rhs, rhs)));
        for (int iteration = 0; iteration < Math.Min(2000, n * 10); iteration++)
        {
            var ap = Multiply(p);
            double denominator = Dot(p, ap);
            if (Math.Abs(denominator) < 1e-24) break;
            double alpha = rz / denominator;
            for (int i = 0; i < n; i++) { x[i] += alpha * p[i]; r[i] -= alpha * ap[i]; }
            if (Math.Sqrt(Dot(r, r)) / rhsNorm < 1e-9) break;
            z = r.Select((v, i) => v / Math.Max(diagonal[i], 1e-12)).ToArray();
            double nextRz = Dot(r, z);
            double beta = nextRz / Math.Max(Math.Abs(rz), 1e-30);
            for (int i = 0; i < n; i++) p[i] = z[i] + beta * p[i];
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
