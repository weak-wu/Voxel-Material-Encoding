namespace GcodeViewer.Models;
public readonly record struct Point2D(double X, double Y)
{
    public double DistanceTo(Point2D other) => Math.Sqrt(Math.Pow(X-other.X,2)+Math.Pow(Y-other.Y,2));
}
