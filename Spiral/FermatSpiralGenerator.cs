using GcodeViewer.Models;
namespace GcodeViewer.Spiral;
public sealed class FermatSpiralGenerator
{ public List<Point2D> Generate(Point2D center,double spacing,double maxRadius){var r=new List<Point2D>(); if(spacing<=0)return r; double b=spacing/(2*Math.PI); for(double t=0;;t+=0.08){double radius=b*t;if(radius>maxRadius)break; r.Add(new(center.X+radius*Math.Cos(t),center.Y+radius*Math.Sin(t)));} return r;} }
