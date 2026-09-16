using System.Drawing.Imaging;
using GcodeViewer.Geometry;
using GcodeViewer.Models;
using GcodeViewer.Spiral;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using OpenCvSharp;
using static GcodeViewer.Geometry.CfsGeometry;
using Geo = NetTopologySuite.Geometries.Geometry;

namespace GcodeViewer.Services;

/// <summary>
/// Native C# implementation of executeMain.py's image-to-CFS pipeline.
/// OpenCVSharp supplies contour hierarchy extraction and NetTopologySuite
/// supplies the Shapely-equivalent polygon/offset operations.
/// </summary>
public sealed class CfsNativeAlgorithm
{
    public CfsResult Generate(Bitmap image, CfsOptions options, CancellationToken token = default)
    {
        Validate(options);
        using var gray = ToGray(image);
        using var mask = ForegroundMask(gray);
        var polygons = ExtractPolygons(mask, out int sourceContourCount);
        if (polygons.Count == 0) return new();

        var rawBounds = polygons.Select(p => p.EnvelopeInternal).Aggregate(new Envelope(), (a, b) =>
        {
            a.ExpandToInclude(b); return a;
        });
        // X 和 Y 必须使用同一个倍率，避免将原图轮廓强制拉伸到独立的宽、高尺寸。
        double coordinateScale = options.PixelSizeMm * options.ScaleFactor;
        polygons = polygons.Select(p => Scale(p, rawBounds.MinX, rawBounds.MinY,
            coordinateScale, coordinateScale)).ToList();

        if (options.PolygonSimplifyToleranceMm > 0)
            polygons = polygons.SelectMany(p => Polygons(
                    TopologyPreservingSimplifier.Simplify(p, options.PolygonSimplifyToleranceMm)))
                .Where(p => p.Area > 1e-8).ToList();

        if (options.BoundaryMm > 0)
            polygons = polygons.SelectMany(p => Polygons(Buffer(p, -options.BoundaryMm, 8)))
                .Where(p => p.Area > 1e-8).ToList();

        var generated = new List<List<Coordinate>>();
        int isoContourCount = 0;
        foreach (var polygon in polygons.OrderByDescending(p => p.Area))
        {
            token.ThrowIfCancellationRequested();
            var polygonPaths = new List<List<Coordinate>>();
            if (options.SpiralType == "spiral")
            {
                var family = CfsContourTree.Standard(polygon, options.SpacingMm, token);
                isoContourCount += family.Contours.Count;
                var path = ContourSpiral.Generate(family.Contours.Skip(1).ToList(),
                    options.SpacingMm, 0, token);
                if (path.Count > 1) polygonPaths.Add(path);
            }
            else
            {
                var family = CfsContourTree.Build(polygon, options.SpacingMm, token, out int count);
                isoContourCount += count;
                polygonPaths.AddRange(options.SpiralType == "connected_fermat"
                    ? FermatContourSpiral.GenerateConnected(family, options.SpacingMm, polygon, token)
                    : FermatContourSpiral.GenerateUnconnected(family, options.SpacingMm, token));
            }

            if (options.Optimize)
                polygonPaths = polygonPaths
                    .Select(path => CfsPathOptimizer.Optimize(path, options.SpacingMm, polygon, token))
                    .Where(path => path.Count > 1)
                    .ToList();
            generated.AddRange(polygonPaths);
        }

        var paths = new List<List<Point2D>>();
        foreach (var coordinates in generated.Where(p => p.Count > 1))
        {
            token.ThrowIfCancellationRequested();
            var processed = options.SimplifyToleranceMm > 0
                ? CfsGeometry.Simplify(coordinates, options.SimplifyToleranceMm)
                : coordinates;
            if (options.PointSpacingMm > 0)
                processed = CfsGeometry.Densify(processed, options.PointSpacingMm);
            if (processed.Count > 1)
                paths.Add(processed.Select(p => new Point2D(p.X, p.Y)).ToList());
        }

        return new CfsResult
        {
            Paths = paths,
            ForegroundPixels = Cv2.CountNonZero(mask),
            ContourCount = sourceContourCount,
            IsoContourCount = isoContourCount,
            PolygonCount = polygons.Count,
            WidthMm = rawBounds.Width * coordinateScale,
            HeightMm = rawBounds.Height * coordinateScale
        };
    }

    private static void Validate(CfsOptions options)
    {
        if (options.PixelSizeMm <= 0) throw new ArgumentOutOfRangeException(nameof(options.PixelSizeMm), "单像素尺寸必须大于 0。");
        if (options.ScaleFactor <= 0) throw new ArgumentOutOfRangeException(nameof(options.ScaleFactor), "缩放比例必须大于 0。");
        if (options.SpacingMm <= 0) throw new ArgumentOutOfRangeException(nameof(options.SpacingMm), "线间距必须大于 0。");
        if (options.PointSpacingMm < 0 || options.SimplifyToleranceMm < 0 || options.BoundaryMm < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "点间距、简化容差和边界偏移不能为负数。");
    }

    private static Mat ToGray(Bitmap image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return Cv2.ImDecode(stream.ToArray(), ImreadModes.Grayscale);
    }

    private static Mat ForegroundMask(Mat gray)
    {
        var mask = new Mat();
        Cv2.Threshold(gray, mask, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        // Infer the background from the image frame instead of assuming that
        // the foreground always occupies less than half of the bitmap.
        int cols = mask.Cols;
        int rows = mask.Rows;
        int whiteBorder = 0;
        int borderPixels = Math.Max(1, cols * 2 + Math.Max(0, rows - 2) * 2);
        for (int x = 0; x < cols; x++)
        {
            if (mask.At<byte>(0, x) != 0) whiteBorder++;
            if (rows > 1 && mask.At<byte>(rows - 1, x) != 0) whiteBorder++;
        }
        for (int y = 1; y + 1 < rows; y++)
        {
            if (mask.At<byte>(y, 0) != 0) whiteBorder++;
            if (cols > 1 && mask.At<byte>(y, cols - 1) != 0) whiteBorder++;
        }
        if (whiteBorder > borderPixels / 2) Cv2.BitwiseNot(mask, mask);
        return mask;
    }

    private static List<Polygon> ExtractPolygons(Mat mask, out int contourCount)
    {
        using var copy = mask.Clone();
        Cv2.FindContours(copy, out var contours, out var hierarchy,
            RetrievalModes.CComp, ContourApproximationModes.ApproxSimple);
        contourCount = contours.Length;
        var polygons = new List<Polygon>();
        for (int i = 0; i < contours.Length; i++)
        {
            if (hierarchy[i].Parent >= 0 || contours[i].Length < 3) continue;
            var shell = Ring(contours[i]);
            if (shell == null) continue;
            var holes = new List<LinearRing>();
            int child = hierarchy[i].Child;
            while (child >= 0)
            {
                var hole = Ring(contours[child]);
                if (hole != null) holes.Add(hole);
                child = hierarchy[child].Next;
            }
            try
            {
                Geo geometry = Factory.CreatePolygon(shell, holes.ToArray());
                if (!geometry.IsValid) geometry = geometry.Buffer(0);
                polygons.AddRange(Polygons(geometry).Where(p => p.Area > 1e-8));
            }
            catch (ArgumentException)
            {
                // OpenCV may occasionally return a degenerate pixel contour.
            }
        }
        return polygons;
    }

    private static LinearRing? Ring(IEnumerable<OpenCvSharp.Point> points)
    {
        var coordinates = points.Select(p => new Coordinate(p.X, p.Y)).ToList();
        if (coordinates.Count < 3) return null;
        if (!coordinates[0].Equals2D(coordinates[^1])) coordinates.Add(new Coordinate(coordinates[0]));
        if (coordinates.Count < 4) return null;
        try { return Factory.CreateLinearRing(coordinates.ToArray()); }
        catch (ArgumentException) { return null; }
    }

    private static Polygon Scale(Polygon polygon, double minX, double minY, double sx, double sy)
    {
        LinearRing Transform(LineString ring) => Factory.CreateLinearRing(ring.Coordinates
            .Select(p => new Coordinate((p.X - minX) * sx, (p.Y - minY) * sy)).ToArray());
        return Factory.CreatePolygon(Transform(polygon.ExteriorRing),
            Enumerable.Range(0, polygon.NumInteriorRings).Select(i => Transform(polygon.GetInteriorRingN(i))).ToArray());
    }
}
