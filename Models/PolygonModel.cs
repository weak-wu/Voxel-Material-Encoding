using GcodeViewer.Models;
namespace GcodeViewer.Models;
public sealed class Contour
{
    public List<Point2D> Points { get; } = new();
    public bool IsClosed => Points.Count > 2 && Points[0].DistanceTo(Points[^1]) < 1e-9;
    public Contour(IEnumerable<Point2D> points) => Points.AddRange(points);
    public double Length { get { double n=0; for(int i=1;i<Points.Count;i++) n+=Points[i-1].DistanceTo(Points[i]); return n; } }
}
public sealed class PolygonModel { public List<Point2D> Outer { get; } = new(); public List<Contour> Holes { get; } = new(); }
