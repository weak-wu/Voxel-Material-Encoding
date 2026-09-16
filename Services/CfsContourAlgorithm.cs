using System.Drawing.Imaging;
using GcodeViewer.Models;
using OpenCvSharp;

namespace GcodeViewer.Services;

/// <summary>
/// Backward-compatible entry point. The former raster approximation has been
/// superseded by <see cref="CfsNativeAlgorithm"/>.
/// </summary>
public sealed class CfsContourAlgorithm
{
    public CfsResult Generate(Bitmap image, CfsOptions options)
        => new CfsNativeAlgorithm().Generate(image, options);

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
        if (Cv2.CountNonZero(mask) > mask.Rows * mask.Cols / 2) Cv2.BitwiseNot(mask, mask);
        return mask;
    }

    private static List<List<Point2D>> BuildLevels(Mat input, CfsOptions o,
        double pixel, double sx, double sy)
    {
        var result = new List<List<Point2D>>();
        using var work = input.Clone();
        var stride = Math.Max(1, (int)Math.Round(o.SpacingMm / (pixel * Math.Max(sx, sy))));
        var boundary = Math.Max(0, (int)Math.Round(o.BoundaryMm / (pixel * Math.Max(sx, sy))));
        if (boundary > 0)
        {
            using var b = Cv2.GetStructuringElement(MorphShapes.Ellipse,
                new OpenCvSharp.Size(boundary * 2 + 1, boundary * 2 + 1));
            Cv2.Erode(work, work, b);
        }
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse,
            new OpenCvSharp.Size(stride * 2 + 1, stride * 2 + 1));
        for (var level = 0; level < 1000 && Cv2.CountNonZero(work) > 0; level++)
        {
            Cv2.FindContours(work, out var contours, out _, RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);
            foreach (var contour in contours.OrderByDescending(c => Cv2.ContourArea(c)))
            {
                if (Cv2.ContourArea(contour) < 4) continue;
                var ring = contour.Select(p => new Point2D(p.X * pixel * sx, p.Y * pixel * sy)).ToList();
                if (ring.Count >= 3) result.Add(CleanRing(ring));
            }
            Cv2.Erode(work, work, kernel);
        }
        return result;
    }

    private static List<List<Point2D>> BuildStandard(List<List<Point2D>> levels, CfsOptions o)
        => levels.Count == 0 ? new() : new() { Connect(levels, o.PointSpacingMm) };

    private static List<List<Point2D>> BuildFermat(List<List<Point2D>> levels,
        CfsOptions o, bool connected)
    {
        if (levels.Count == 0) return new();
        if (connected) return new() { Connect(levels, o.PointSpacingMm) };
        return levels.Select(p => Densify(p, Math.Max(.01, o.PointSpacingMm))).ToList();
    }

    private static List<Point2D> Connect(List<List<Point2D>> levels, double pointSpacing)
    {
        var output = new List<Point2D>();
        foreach (var source in levels)
        {
            var ring = CleanRing(source);
            if (ring.Count < 3) continue;
            if (output.Count == 0) output.AddRange(ring);
            else
            {
                var index = NearestIndex(output[^1], ring);
                var ordered = Rotate(ring, index);
                output.AddRange(Densify(new List<Point2D> { output[^1], ordered[0] }, pointSpacing));
                output.AddRange(ordered);
            }
        }
        return Densify(output, Math.Max(.01, pointSpacing));
    }

    private static int NearestIndex(Point2D p, List<Point2D> ring)
    {
        var index = 0; var best = double.MaxValue;
        for (var i = 0; i < ring.Count; i++)
        {
            var d = p.DistanceTo(ring[i]);
            if (d < best) { best = d; index = i; }
        }
        return index;
    }

    private static List<Point2D> Rotate(List<Point2D> ring, int index)
        => Enumerable.Range(0, ring.Count).Select(i => ring[(index + i) % ring.Count]).ToList();

    private static List<Point2D> CleanRing(List<Point2D> source)
    {
        var result = new List<Point2D>();
        foreach (var p in source)
            if (result.Count == 0 || result[^1].DistanceTo(p) > 1e-6) result.Add(p);
        if (result.Count > 2 && result[0].DistanceTo(result[^1]) > 1e-6) result.Add(result[0]);
        return result;
    }

    private static List<Point2D> Densify(List<Point2D> source, double maxSpacing)
    {
        if (source.Count < 2) return source;
        var result = new List<Point2D> { source[0] };
        foreach (var next in source.Skip(1))
        {
            var a = result[^1]; var count = Math.Max(1, (int)Math.Ceiling(a.DistanceTo(next) / maxSpacing));
            for (var i = 1; i <= count; i++)
            {
                var t = (double)i / count;
                result.Add(new Point2D(a.X + (next.X - a.X) * t, a.Y + (next.Y - a.Y) * t));
            }
        }
        return result;
    }

    private static List<Point2D> Simplify(List<Point2D> source, double tolerance)
    {
        if (source.Count < 3 || tolerance <= 0) return source;
        var keep = new bool[source.Count]; keep[0] = keep[^1] = true;
        Rdp(source, 0, source.Count - 1, tolerance * tolerance, keep);
        return source.Where((_, i) => keep[i]).ToList();
    }

    private static void Rdp(List<Point2D> p, int first, int last, double tolerance, bool[] keep)
    {
        var a = p[first]; var b = p[last]; var dx = b.X - a.X; var dy = b.Y - a.Y;
        var den = dx * dx + dy * dy; var max = 0.0; var index = -1;
        for (var i = first + 1; i < last; i++)
        {
            var t = den < 1e-12 ? 0 : ((p[i].X - a.X) * dx + (p[i].Y - a.Y) * dy) / den;
            t = Math.Clamp(t, 0, 1);
            var q = new Point2D(a.X + t * dx, a.Y + t * dy); var d = p[i].DistanceTo(q);
            if (d > max) { max = d; index = i; }
        }
        if (index >= 0 && max * max > tolerance)
        {
            keep[index] = true; Rdp(p, first, index, tolerance, keep); Rdp(p, index, last, tolerance, keep);
        }
    }
}
