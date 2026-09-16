using GcodeViewer.Models;
namespace GcodeViewer.Geometry;
public static class PolylineUtilities
{
    public static Point2D Interpolate(Contour c, double distance) { if(c.Points.Count==0) return default; distance=Math.Clamp(distance,0,c.Length); double acc=0; for(int i=1;i<c.Points.Count;i++){var a=c.Points[i-1];var b=c.Points[i];var len=a.DistanceTo(b);if(acc+len>=distance){var t=(distance-acc)/len;return new(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);}acc+=len;} return c.Points[^1]; }
    public static Contour Resample(Contour c,double step){var p=new List<Point2D>(); for(double d=0;d<c.Length;d+=step)p.Add(Interpolate(c,d)); p.Add(c.Points[^1]); return new Contour(p);}
}
