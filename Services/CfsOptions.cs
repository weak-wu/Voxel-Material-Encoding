using GcodeViewer.Models;
namespace GcodeViewer.Services;
public sealed class CfsOptions { public string SpiralType {get;set;}="connected_fermat"; public double PixelSizeMm {get;set;}=.1; public double ScaleFactor {get;set;}=1; public double SpacingMm {get;set;}=5; public double BoundaryMm {get;set;}=0; public double PointSpacingMm {get;set;}=1.25; public double SimplifyToleranceMm {get;set;}=.01; public double PolygonSimplifyToleranceMm {get;set;}=0; public bool Optimize {get;set;}=true; public int Layers {get;set;}=1; public double LayerHeightMm {get;set;}=.2; }
public sealed class CfsResult { public List<List<Point2D>> Paths {get;set;}=new(); public int ForegroundPixels {get;init;} public int ContourCount {get;init;} public int IsoContourCount {get;init;} public int PolygonCount {get;init;} public double WidthMm {get;init;} public double HeightMm {get;init;} }

