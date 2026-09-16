using GcodeViewer.Models;
namespace GcodeViewer.Spiral;
public sealed class SpiralPathGenerator
{
    public List<Point2D> Generate(Contour boundary, double spacing, int turns=40){ if(boundary.Points.Count<2||spacing<=0)return new(); var result=new List<Point2D>(); var center=boundary.Points.Aggregate(new Point2D(0,0),(s,p)=>new(s.X+p.X,s.Y+p.Y)); center=new(center.X/boundary.Points.Count,center.Y/boundary.Points.Count); double max=boundary.Points.Max(p=>p.DistanceTo(center)); for(double r=max;r>spacing/2;r-=spacing){int n=Math.Max(16,(int)(2*Math.PI*r/(spacing/2))); for(int i=0;i<n;i++){double a=2*Math.PI*i/n + (max-r)*0.08; result.Add(new(center.X+r*Math.Cos(a),center.Y+r*Math.Sin(a)));}} return result; }
}
