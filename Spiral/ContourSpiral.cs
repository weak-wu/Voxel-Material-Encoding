using NetTopologySuite.Geometries;
using static GcodeViewer.Geometry.CfsGeometry;

namespace GcodeViewer.Spiral;

internal static class ContourSpiral
{
    internal static List<Coordinate> Generate(IReadOnlyList<LineString> contours, double distance,
        int startIndex, CancellationToken token)
    {
        List<Coordinate>? first = null;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var path = Spiral(contours, distance, startIndex + attempt).DistinctBy(p => (p.X, p.Y)).ToList();
            if (path.Count < 2) break;
            first ??= path;
            if (Line(path).IsSimple) return path;
        }
        return first ?? new();
    }
    private static List<Coordinate> Spiral(IReadOnlyList<LineString> contours, double distance, int index)
    {
        var result = new List<Coordinate>();
        if (contours.Count == 0) return result;
        var c = contours[0].Coordinates;
        var indices = Enumerable.Range(0, c.Length)
            .OrderByDescending(i => c[i].Distance(c[(i + c.Length - 1) % c.Length])).ThenByDescending(i => i).ToArray();
        var contour = Cycle(contours[0], c[indices[index % c.Length]]);
        var end = Endpoint(contour, distance);
        if (end == null) return result;
        var cut = Cut(contour, Project(contour, end)).First;
        if (cut != null) result.AddRange(cut.Coordinates);
        var previous = contour;
        foreach (var inner in contours.Skip(1))
        {
            contour = Cycle(inner, At(inner, Project(inner, end)));
            end = ContourEndpoint(contour, previous, distance);
            if (end == null) break;
            cut = Cut(contour, Project(contour, end)).First;
            if (cut != null) result.AddRange(cut.Coordinates);
            previous = contour;
        }
        return result;
    }
}
