using GcodeViewer.Geometry;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Distance;
using static GcodeViewer.Geometry.CfsGeometry;
using Geo = NetTopologySuite.Geometries.Geometry;

namespace GcodeViewer.Spiral;

/// <summary>
/// C# port of the path construction portion of fermat_spiral.py.  It first
/// creates a contour spiral, folds the unused contour pieces back out to make
/// a Fermat spiral, and finally splices the branches of the contour tree into
/// the root path.
/// </summary>
internal static class FermatContourSpiral
{
    internal static List<List<Coordinate>> GenerateUnconnected(
        CfsFamily family, double distance, CancellationToken token)
    {
        var paths = new List<List<Coordinate>>();
        foreach (var branch in family.Branches)
            paths.AddRange(GenerateUnconnected(branch, distance, token));

        var root = GenerateBest(family.Contours, distance, token);
        if (root.Count > 1) paths.Add(root);
        return paths;
    }

    internal static List<List<Coordinate>> GenerateConnected(
        CfsFamily family, double distance, Polygon domain, CancellationToken token)
    {
        var allowed = domain.Buffer(Math.Max(1e-7, distance * 1e-6));
        var connected = GenerateConnectedSingle(family, distance, allowed, token);
        return connected.Count > 1 ? new() { connected } : new();
    }

    private static List<Coordinate> GenerateConnectedSingle(
        CfsFamily family, double distance, Geo allowed, CancellationToken token)
    {
        var branches = family.Branches
            .Select(branch => GenerateConnectedSingle(branch, distance, allowed, token))
            .Where(path => path.Count > 1)
            .ToList();

        var root = MergeContourFamily(family.Contours, distance, allowed, token);
        if (root.Count < 2)
        {
            if (branches.Count == 0) return new();
            root = branches[0];
            branches.RemoveAt(0);
        }
        foreach (var branch in branches)
            root = InsertOpenPath(root, branch, allowed);
        return root;
    }

    private static List<Coordinate> MergeContourFamily(
        IReadOnlyList<LineString> contours, double distance,
        Geo allowed, CancellationToken token)
    {
        if (contours.Count == 0) return new();
        var path = OpenRing(contours[0], distance);
        foreach (var contour in contours.Skip(1))
        {
            token.ThrowIfCancellationRequested();
            path = InsertRing(path, contour, distance, allowed);
        }
        return path;
    }

    private static List<Coordinate> OpenRing(LineString ring, double distance)
    {
        if (ring.NumPoints < 4 || ring.Length <= 1e-9) return ring.Coordinates.ToList();
        var coordinates = ring.Coordinates;
        int longest = Enumerable.Range(0, coordinates.Length - 1)
            .MaxBy(i => coordinates[i].Distance(coordinates[i + 1]));
        double startPosition = Project(ring, coordinates[longest]);
        var cycled = Cycle(ring, At(ring, startPosition));
        double gap = Math.Min(distance, cycled.Length * .2);
        return (Cut(cycled, gap).Second ?? cycled).Coordinates.ToList();
    }

    private static List<Coordinate> InsertRing(
        List<Coordinate> current, LineString ring, double distance, Geo allowed)
    {
        var currentLine = Line(current);
        if (currentLine.Length <= 1e-9 || ring.Length <= 1e-9) return current;

        var candidates = new List<(double Distance, double Parent, double Child)>();
        var nearest = DistanceOp.NearestPoints(currentLine, ring);
        candidates.Add((nearest[0].Distance(nearest[1]),
            Project(currentLine, nearest[0]), Project(ring, nearest[1])));
        int samples = Math.Clamp((int)Math.Ceiling(currentLine.Length / Math.Max(distance, 1e-6)), 32, 160);
        for (int i = 0; i <= samples; i++)
        {
            double parentPosition = currentLine.Length * i / samples;
            var parentPoint = At(currentLine, parentPosition);
            double childPosition = Project(ring, parentPoint);
            candidates.Add((parentPoint.Distance(At(ring, childPosition)), parentPosition, childPosition));
        }

        foreach (var candidate in candidates.OrderBy(x => x.Distance).Take(48))
            foreach (double gapScale in new[] { 1.0, .6, 1.4 })
            {
                double parentGap = Math.Min(distance * gapScale, currentLine.Length * .15);
                double childGap = Math.Min(distance * gapScale, ring.Length * .15);
                double parentCenter = Math.Clamp(candidate.Parent, parentGap * .5,
                    Math.Max(parentGap * .5, currentLine.Length - parentGap * .5));
                double firstCut = parentCenter - parentGap * .5;
                double secondCut = parentCenter + parentGap * .5;
                var prefix = Cut(currentLine, firstCut).First;
                var suffix = Cut(currentLine, secondCut).Second;
                if (prefix == null || suffix == null) continue;

                double childStart = Wrap(candidate.Child + childGap * .5, ring.Length);
                var cycled = Cycle(ring, At(ring, childStart));
                var longPart = Cut(cycled, Math.Max(0, cycled.Length - childGap)).First;
                if (longPart == null || longPart.NumPoints < 2) continue;

                foreach (var child in new[]
                         {
                             longPart.Coordinates.AsEnumerable(),
                             longPart.Coordinates.Reverse()
                         }.OrderBy(points => prefix.Coordinates[^1].Distance(points.First()) +
                                             suffix.Coordinates[0].Distance(points.Last())))
                {
                    var merged = Clean(prefix.Coordinates.Concat(child).Concat(suffix.Coordinates));
                    var line = Line(merged);
                    if (line.IsSimple && allowed.Covers(line)) return merged;
                }
            }

        throw new InvalidOperationException("无法在不穿越边界或产生自交的情况下连接相邻等距轮廓。");
    }

    private static List<Coordinate> InsertOpenPath(
        List<Coordinate> current, List<Coordinate> branch, Geo allowed)
    {
        var currentLine = Line(current);
        var alternatives = new[] { branch.AsEnumerable(), branch.AsEnumerable().Reverse() };
        foreach (var selected in alternatives)
        {
            var child = selected.ToList();
            double firstCut = Project(currentLine, child[0]);
            double secondCut = Project(currentLine, child[^1]);
            if (firstCut > secondCut)
            {
                child.Reverse();
                (firstCut, secondCut) = (secondCut, firstCut);
            }
            var prefix = Cut(currentLine, firstCut).First;
            var suffix = Cut(currentLine, secondCut).Second;
            if (prefix == null || suffix == null) continue;
            var merged = Clean(prefix.Coordinates.Concat(child).Concat(suffix.Coordinates));
            var line = Line(merged);
            if (line.IsSimple && allowed.Covers(line)) return merged;
        }
        throw new InvalidOperationException("无法在材料区域内无自交地连接螺旋树分支。");
    }

    private static double Wrap(double value, double length)
    {
        value %= length;
        return value < 0 ? value + length : value;
    }

    private static List<Coordinate> GenerateBest(
        IReadOnlyList<LineString> contours, double distance, CancellationToken token)
    {
        List<Coordinate>? firstSpiral = null;
        List<Coordinate>? best = null;
        List<Coordinate>? bestSimple = null;
        double bestRatio = 0;
        double bestSimpleRatio = 0;

        // Same two-level retry strategy as generate_total_path[_connected]:
        // thirty groups, each with the one-hundred starts tried by generate_path.
        for (int attempt = 0; attempt < 30; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var spiral = ContourSpiral.Generate(contours, distance, attempt * 100, token);
            if (spiral.Count < 2) break;
            firstSpiral ??= spiral;

            var fermat = ConvertFermat(spiral, distance);
            if (fermat.Count < 2) continue;
            var spiralLength = Line(spiral).Length;
            var line = Line(fermat);
            var ratio = spiralLength <= 1e-9 ? 0 : line.Length / spiralLength;

            if (line.IsSimple && ratio > .90 && ratio > bestSimpleRatio)
            {
                bestSimple = fermat;
                bestSimpleRatio = ratio;
            }
            if (ratio > bestRatio)
            {
                best = fermat;
                bestRatio = ratio;
            }
        }
        return bestSimple ?? best ?? firstSpiral ?? new();
    }

    private static List<Coordinate> ConvertFermat(List<Coordinate> path, double distance)
    {
        var (outer, pieces, center) = OuterSpiral(path, distance);
        return InnerSpiral(pieces, distance, center, outer);
    }

    private static Coordinate? CalculateBreak(LineString source, Coordinate start, double radius)
    {
        var path = source;
        double projection = 0;
        for (int guard = 0; guard < 10000 && projection <= radius; guard++)
        {
            var remainder = Cut(path, radius).Second;
            if (remainder == null || remainder.Length <= 1e-9) return null;
            path = remainder;
            projection = Project(path, start);
        }
        return At(path, projection);
    }

    private static (List<Coordinate> Path, List<LineString> Pieces, bool Center)
        OuterSpiral(List<Coordinate> coordinates, double distance)
    {
        var path = Line(coordinates);
        var start = path.GetCoordinateN(0);
        var spiral = new List<Coordinate>();
        var pieces = new List<LineString>();

        for (int guard = 0; guard < 10000; guard++)
        {
            var end = CalculateBreak(path, start, distance);
            if (end == null || Nearly(Project(path, end), path.Length))
            {
                spiral.AddRange(path.Coordinates);
                return (Clean(spiral), pieces, true);
            }

            var reroute = CalculatePoint(path, Project(path, end), distance, false);
            if (reroute == null)
            {
                var prefix = Cut(path, Project(path, end)).First;
                if (prefix != null) spiral.AddRange(prefix.Coordinates);
                return (Clean(spiral), new(), true);
            }

            var (prefixPart, centerPart) = Cut(path, Project(path, reroute));
            if (prefixPart == null || centerPart == null) break;
            var (unusedCenter, remainderPart) = Cut(centerPart, Project(centerPart, end));
            if (remainderPart == null) break;
            _ = unusedCenter;

            start = CalculateBreak(remainderPart, reroute, distance);
            spiral.AddRange(prefixPart.Coordinates);
            if (start == null || Nearly(Project(remainderPart, start), remainderPart.Length))
            {
                spiral.AddRange(remainderPart.Coordinates.Reverse());
                return (Clean(spiral), pieces, true);
            }

            var (outer, inner) = Cut(remainderPart, Project(remainderPart, start));
            if (outer == null || inner == null) break;
            pieces.Add(outer);
            path = inner;
        }
        spiral.AddRange(path.Coordinates);
        return (Clean(spiral), pieces, true);
    }

    private static List<Coordinate> InnerSpiral(
        List<LineString> sourcePieces, double distance, bool center, List<Coordinate> sourcePath)
    {
        if (sourcePieces.Count == 0) return RemoveIntersections(sourcePath);

        var pieces = sourcePieces.AsEnumerable().Reverse().ToList();
        var contour = pieces[0];
        var formatted = new List<LineString>();
        var path = sourcePath.ToList();

        if (center)
        {
            var previous = Line(path);
            var remainder = Cut(previous, Project(previous, contour.Coordinates[^1])).Second;
            var end = remainder == null ? null : ContourEndpoint(contour, remainder, distance);
            if (end != null)
            {
                contour = Cut(contour, Project(contour, end)).First ?? contour;
                contour = TrimAtConnectorIntersection(contour, path[^1], end, preferFarthest: true);
                formatted.Add(contour);
            }
            else
            {
                if (pieces.Count == 1) return RemoveIntersections(path);
                pieces.RemoveAt(0);
                contour = pieces[0];
                center = false;
            }
        }

        if (!center)
        {
            contour = Cut(contour, Project(contour, path[^1])).First ?? contour;
            formatted.Add(contour);
            var source = Line(path);
            var prefix = Cut(source, Project(source, contour.Coordinates[^1])).First;
            if (prefix != null) path = prefix.Coordinates.ToList();
            contour = TrimAtConnectorIntersection(contour, path[^1], path[^1], preferFarthest: false);
            formatted[^1] = contour;
        }

        foreach (var item in pieces.Skip(1))
        {
            var reroute = CalculatePoint(item, item.Length, distance, false);
            if (reroute == null) continue;
            var prefix = Cut(item, Project(item, reroute)).First;
            if (prefix != null) formatted.Add(prefix);
        }

        var inner = new List<Coordinate>();
        for (int i = 0; i + 1 < formatted.Count; i++)
        {
            var current = formatted[i];
            double projection = Project(current, formatted[i + 1].Coordinates[^1]);
            var selected = projection <= 1e-9 ? current : Cut(current, projection).Second;
            if (selected != null) inner.AddRange(selected.Coordinates.Reverse());
        }
        if (formatted.Count > 0) inner.AddRange(formatted[^1].Coordinates.Reverse());

        var result = RemoveIntersections(path);
        var cleanInner = RemoveIntersections(inner.AsEnumerable().Reverse().ToList());
        result.AddRange(cleanInner.AsEnumerable().Reverse());
        return Clean(result);
    }

    private static LineString TrimAtConnectorIntersection(
        LineString contour, Coordinate destination, Coordinate reference, bool preferFarthest)
    {
        if (contour.NumPoints < 3) return contour;
        var test = Line(contour.Coordinates.SkipLast(1));
        var connector = Line(new[] { contour.Coordinates[^1], destination });
        var intersection = test.Intersection(connector);
        var candidates = intersection.Coordinates
            .Where(p => p.Distance(contour.Coordinates[^1]) > 1e-8)
            .DistinctBy(p => (Math.Round(p.X, 9), Math.Round(p.Y, 9))).ToList();
        if (candidates.Count == 0) return contour;
        var point = preferFarthest
            ? candidates.MaxBy(p => p.Distance(reference))!
            : candidates.MinBy(p => p.Distance(reference))!;
        return Cut(contour, Project(contour, point)).First ?? contour;
    }

    private static List<Coordinate> Clean(IEnumerable<Coordinate> source)
    {
        var result = new List<Coordinate>();
        foreach (var p in source)
            if (result.Count == 0 || result[^1].Distance(p) > 1e-9)
                result.Add(new Coordinate(p));
        return result;
    }

    private static bool Nearly(double a, double b) => Math.Abs(a - b) <= 1e-7;
}
