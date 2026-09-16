using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;
using NetTopologySuite.Operation.Buffer;
using NetTopologySuite.Operation.Distance;
using NetTopologySuite.Index.Strtree;
using Geo = NetTopologySuite.Geometries.Geometry;

namespace GcodeViewer.Geometry;

internal static class CfsGeometry
{
    internal static readonly GeometryFactory Factory = new();
    internal static LineString Line(IEnumerable<Coordinate> points)
    {
        var c = points.ToArray();
        return Factory.CreateLineString(c.Length == 1 ? new[] { c[0], c[0] } : c);
    }
    internal static Geo Buffer(Geo geometry, double distance, int quadrants = 16, JoinStyle join = JoinStyle.Round)
        => geometry.Buffer(distance, new BufferParameters(quadrants, EndCapStyle.Flat, join, 5) { SimplifyFactor = 0 });
    internal static IEnumerable<Polygon> Polygons(Geo geometry)
    {
        if (geometry is Polygon polygon && !polygon.IsEmpty) yield return polygon;
        else if (geometry is GeometryCollection)
            for (int i = 0; i < geometry.NumGeometries; i++)
                foreach (var p in Polygons(geometry.GetGeometryN(i))) yield return p;
    }
    internal static double Project(LineString line, Coordinate p) => new LengthIndexedLine(line).Project(p);
    internal static Coordinate At(LineString line, double distance)
        => new LengthIndexedLine(line).ExtractPoint(Math.Clamp(distance, 0, line.Length));
    internal static (LineString? First, LineString? Second) Cut(LineString line, double distance)
    {
        if (double.IsNaN(distance) || distance >= line.Length) return (line, null);
        if (distance <= 0) return (null, line);
        var c = line.Coordinates;
        // Preserve the Python projection-based cut, including closed-ring endpoints.
        for (int i = 0; i < c.Length; i++)
        {
            double position = Project(line, c[i]);
            if (position == distance) return (Line(c.Take(i + 1)), Line(c.Skip(i)));
            if (position > distance)
            {
                var split = At(line, distance);
                return (Line(c.Take(i).Append(split)), Line(new[] { split }.Concat(c.Skip(i))));
            }
        }
        var end = At(line, distance);
        return (Line(c.Take(c.Length - 1).Append(end)), Line(new[] { end, c[^1] }));
    }
    internal static LineString Cycle(LineString line, Coordinate point)
    {
        var (first, second) = Cut(line, Project(line, point));
        return first == null ? second! : second == null ? first : Line(second.Coordinates.Concat(first.Coordinates));
    }
    internal static Coordinate? CalculatePoint(LineString line, double position, double radius, bool forward = true)
    {
        int direction = forward ? 1 : -1;
        double error = direction * radius;
        var start = At(line, position);
        Coordinate point = start;
        for (int iteration = 0; iteration < 10000; iteration++)
        {
            if (Math.Abs(error) <= 1e-6) return point;
            position += error;
            if (position >= line.Length || position <= 0) return null;
            point = At(line, position);
            error = direction * (radius - point.Distance(start)) / 2;
        }
        return null;
    }
    internal static Coordinate? Endpoint(LineString line, double radius)
    {
        var c = line.Coordinates.Reverse().ToArray();
        for (int i = 1; i < c.Length; i++)
            if (c[i].Distance(c[0]) > radius)
            {
                var circle = Factory.CreatePoint(c[0]).Buffer(radius, 16).Boundary;
                var intersection = circle.Intersection(Line(new[] { c[i - 1], c[i] }));
                return intersection.IsEmpty ? null : intersection.Coordinate;
            }
        return null;
    }
    internal static Coordinate? ContourEndpoint(LineString contour, LineString previous, double radius)
    {
        var point = CalculatePoint(contour, contour.Length, radius, false);
        if (point == null) return null;
        double Distance(Coordinate p) => previous.Distance(Factory.CreatePoint(p));
        if (radius - Distance(point) < 1e-6) return point;
        var distance = Project(contour, point);
        while (Distance(point) < radius)
        {
            distance -= radius;
            if (distance < 0) return null;
            point = At(contour, distance);
        }
        var error = (contour.Length - distance) / 2;
        var position = distance + error;
        point = At(contour, position);
        for (int i = 0; i < 10; i++)
        {
            error /= 2;
            position += Distance(point) < radius ? -error : error;
            point = At(contour, position);
        }
        return point;
    }
    internal static List<Coordinate> RemoveIntersections(IEnumerable<Coordinate> source)
    {
        var line = Line(source);
        if (line.NumPoints < 4 || line.IsSimple) return line.Coordinates.ToList();
        var intersections = new List<Coordinate>();
        var coordinates = line.Coordinates;
        for (int i = 0; i < coordinates.Length - 3; i++)
        {
            var intersection = Line(coordinates.Skip(i).Take(2)).Intersection(Line(coordinates.Skip(i + 2)));
            if (intersection is NetTopologySuite.Geometries.Point || intersection is MultiPoint)
                intersections.AddRange(intersection.Coordinates);
        }
        line = Line(coordinates.Reverse());
        intersections = intersections.OrderBy(p => Project(line, p)).ToList();
        while (intersections.Count > 0)
        {
            var point = intersections[0];
            intersections.RemoveAt(0);
            var (first, second) = Cut(line, Project(line, point));
            if (first == null || second == null || first.NumPoints < 3 || second.NumPoints < 3) continue;
            first = Line(first.Coordinates.SkipLast(1));
            second = Line(second.Coordinates.Skip(1));
            LineString? remainder;
            if (first.Distance(Factory.CreatePoint(point)) < 1e-9)
            {
                var next = Cut(first, Project(first, point));
                first = next.First; remainder = next.Second;
            }
            else
            {
                var next = Cut(second, Project(second, point));
                remainder = next.First; second = next.Second;
            }
            if (first == null || second == null) continue;
            line = Line(first.Coordinates.Concat(second.Coordinates));
            if (remainder != null)
                intersections.RemoveAll(p => remainder.Distance(Factory.CreatePoint(p)) < 1e-9);
        }
        return line.Coordinates.Reverse().ToList();
    }
    internal static bool SafeSpacing(LineString line, double distance, bool cyclic)
    {
        var segments = new List<(LineString Line, double Arc)>();
        double arc = 0;
        var c = line.Coordinates;
        for (int i = 1; i < c.Length; i++)
        {
            var segment = Line(new[] { c[i - 1], c[i] });
            if (segment.Length <= 1e-9) continue;
            segments.Add((segment, arc));
            arc += segment.Length;
        }
        var tree = new STRtree<int>();
        for (int i = 0; i < segments.Count; i++) tree.Insert(segments[i].Line.EnvelopeInternal, i);
        for (int i = 0; i < segments.Count; i++)
        {
            var a = segments[i];
            var envelope = new Envelope(a.Line.EnvelopeInternal);
            envelope.ExpandBy(distance * .95);
            foreach (int j in tree.Query(envelope))
            {
                if (j <= i) continue;
                var b = segments[j];
                if (a.Line.Boundary.Intersects(b.Line.Boundary) || a.Line.Distance(b.Line) >= distance * .95) continue;
                var nearest = DistanceOp.NearestPoints(a.Line, b.Line);
                double delta = Math.Abs(a.Arc + Project(a.Line, nearest[0]) - b.Arc - Project(b.Line, nearest[1]));
                if (cyclic) delta = Math.Min(delta, arc - delta);
                if (delta > distance * 4) return false;
            }
        }
        return true;
    }
    internal static List<Coordinate> Simplify(IReadOnlyList<Coordinate> points, double tolerance)
    {
        if (points.Count < 3 || tolerance <= 0) return points.ToList();
        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int First, int Last)>();
        stack.Push((0, points.Count - 1));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            var segment = new LineSegment(points[a], points[b]);
            double max = tolerance;
            int index = -1;
            for (int i = a + 1; i < b; i++)
            {
                // fermat_spiral.py uses distance to the infinite supporting line.
                double error = segment.DistancePerpendicular(points[i]);
                if (error > max) { max = error; index = i; }
            }
            if (index < 0) continue;
            keep[index] = true;
            stack.Push((a, index)); stack.Push((index, b));
        }
        return points.Where((_, i) => keep[i]).ToList();
    }
    internal static List<Coordinate> Densify(IReadOnlyList<Coordinate> points, double spacing)
    {
        if (points.Count < 2 || spacing <= 0) return points.ToList();
        var result = new List<Coordinate> { points[0] };
        for (int i = 1; i < points.Count; i++)
        {
            var a = points[i - 1]; var b = points[i];
            int count = checked((int)Math.Max(1, Math.Ceiling(a.Distance(b) / spacing)));
            if ((long)result.Count + count > 2_000_000) throw new InvalidOperationException("Point spacing produces more than 2,000,000 points.");
            for (int j = 1; j <= count; j++) result.Add(new Coordinate(a.X + (b.X - a.X) * j / count, a.Y + (b.Y - a.Y) * j / count));
        }
        return result;
    }
}
