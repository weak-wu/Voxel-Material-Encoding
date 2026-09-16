using GcodeViewer.Models;
namespace GcodeViewer.Services;
public sealed class GenerationOptions { public double Spacing {get;set;}=2; public bool UseFermat {get;set;}=false; }
public sealed class GenerationResult { public List<Point2D> Path {get;set;}=new(); public TimeSpan Elapsed {get;init;} }
