using System.Drawing.Imaging;
using GcodeViewer.Models;
using OpenCvSharp;
namespace GcodeViewer.Services;
public sealed class CfsAlgorithm
{
 public CfsResult Generate(Bitmap image,CfsOptions o)
 {
  using var gray=ToGray(image); using var mask=new Mat(); Cv2.Threshold(gray,mask,0,255,ThresholdTypes.Binary|ThresholdTypes.Otsu); if(Cv2.CountNonZero(mask)>mask.Rows*mask.Cols/2) Cv2.BitwiseNot(mask,mask);
  Cv2.FindContours(mask,out var contours,out _,RetrievalModes.External,ContourApproximationModes.ApproxSimple); if(contours.Length==0)return new(); var contour=contours.OrderByDescending(c=>Cv2.ContourArea(c)).First(); var points=contour.Select(p=>new Point2D(p.X*o.PixelSizeMm,p.Y*o.PixelSizeMm)).ToList(); var bounds=Bounds(points); var sx=o.ScaleFactor;var sy=sx; points=points.Select(p=>new Point2D((p.X-bounds.minX)*sx,(p.Y-bounds.minY)*sy)).ToList(); var w=bounds.w*sx;var h=bounds.h*sy; var paths=o.SpiralType.StartsWith("spiral")?GenerateOffsets(mask,contour,o,sx,sy):GenerateFermat(points,o); if(o.Optimize)for(int i=0;i<paths.Count;i++)paths[i]=Simplify(paths[i],o.SimplifyToleranceMm); return new(){Paths=paths.Where(p=>p.Count>1).ToList(),ForegroundPixels=Cv2.CountNonZero(mask),ContourCount=contours.Length,WidthMm=w,HeightMm=h};
 }
 private static Mat ToGray(Bitmap b){using var ms=new MemoryStream();b.Save(ms,ImageFormat.Png);return Cv2.ImDecode(ms.ToArray(),ImreadModes.Grayscale);}
 private static List<List<Point2D>> GenerateOffsets(Mat mask,OpenCvSharp.Point[] contour,CfsOptions o,double sx,double sy){var result=new List<List<Point2D>>();using var work=mask.Clone();int step=Math.Max(1,(int)Math.Round(o.SpacingMm/(o.PixelSizeMm*Math.Max(sx,sy))));for(int n=0;n<1000;n++){Cv2.FindContours(work,out var cs,out _,RetrievalModes.External,ContourApproximationModes.ApproxSimple);if(cs.Length==0)break;var c=cs.OrderByDescending(c=>Cv2.ContourArea(c)).First();if(Cv2.ContourArea(c)<4)break;result.Add(c.Select(p=>new Point2D(p.X*o.PixelSizeMm*sx,p.Y*o.PixelSizeMm*sy)).ToList());using var kernel=Cv2.GetStructuringElement(MorphShapes.Ellipse,new OpenCvSharp.Size(step*2+1,step*2+1));Cv2.Erode(work,work,kernel);if(work.Empty()||Cv2.CountNonZero(work)==0)break;}return result;}
 private static List<List<Point2D>> GenerateFermat(List<Point2D> poly,CfsOptions o){var b=Bounds(poly);var center=new Point2D(poly.Average(p=>p.X),poly.Average(p=>p.Y));double max=poly.Max(p=>p.DistanceTo(center));double bb=Math.Max(.0001,o.SpacingMm/(2*Math.PI));var result=new List<List<Point2D>>();var current=new List<Point2D>();double last=0;for(double t=0;t<max/bb*2*Math.PI;t+=.04){double r=bb*t;var p=new Point2D(center.X+r*Math.Cos(t),center.Y+r*Math.Sin(t));bool inside=PointInPolygon(p,poly)&&p.X>=o.BoundaryMm&&p.Y>=o.BoundaryMm&&p.X<=b.maxX-o.BoundaryMm&&p.Y<=b.maxY-o.BoundaryMm;if(inside){if(current.Count==0&&last>0){}current.Add(p);}else if(current.Count>1){result.Add(current);current=new();}last=t;}if(current.Count>1)result.Add(current);return result;}
 private static bool PointInPolygon(Point2D p,List<Point2D> poly){bool c=false;for(int i=0,j=poly.Count-1;i<poly.Count;j=i++){if(((poly[i].Y>p.Y)!=(poly[j].Y>p.Y))&&(p.X<(poly[j].X-poly[i].X)*(p.Y-poly[i].Y)/(poly[j].Y-poly[i].Y)+poly[i].X))c=!c;}return c;}
 private static (double minX,double minY,double maxX,double maxY,double w,double h) Bounds(List<Point2D> p){var minX=p.Min(x=>x.X);var minY=p.Min(x=>x.Y);var maxX=p.Max(x=>x.X);var maxY=p.Max(x=>x.Y);return(minX,minY,maxX,maxY,maxX-minX,maxY-minY);}
 private static List<Point2D> Simplify(List<Point2D> p,double e){if(p.Count<3||e<=0)return p;var keep=new bool[p.Count];keep[0]=keep[^1]=true;Rdp(p,0,p.Count-1,e*e,keep);return p.Where((_,i)=>keep[i]).ToList();}
 private static void Rdp(List<Point2D> p,int a,int b,double e,bool[] keep){double md=0;int idx=-1;var s=p[a];var t=p[b];var dx=t.X-s.X;var dy=t.Y-s.Y;var den=dx*dx+dy*dy;for(int i=a+1;i<b;i++){double u=den<1e-12?0:((p[i].X-s.X)*dx+(p[i].Y-s.Y)*dy)/den;u=Math.Clamp(u,0,1);var q=new Point2D(s.X+u*dx,s.Y+u*dy);var d=p[i].DistanceTo(q);if(d>md){md=d;idx=i;}}if(md*md>e){keep[idx]=true;Rdp(p,a,idx,e,keep);Rdp(p,idx,b,e,keep);}}
}


