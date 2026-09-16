using GcodeViewer.Models;
using GcodeViewer.Parsing;

if (args.Contains("--acceptance")) return Acceptance.Run() == 0 ? 0 : 1;

if (args.Contains("--probe"))
{
    foreach (double middleLength in new[] { 10.0, 2.0 })
    foreach (var (a0, a1) in new[] { (0.0, 0.0), (3.0, 3.0), (3.0, 6.0), (6.0, 3.0) })
    {
        var input = new List<Point3D>
        {
            new(0, 0, tool: 1), new(20, 0, tool: 0),
            new(20 + middleLength, 0, tool: 1), new(40 + middleLength, 0, tool: 1)
        };
        foreach (bool speedChange in new[] { false, true })
        {
            var output = PathGenerator.DirectGeneratePath(input, a0, a1, 10, 10, 5, 5, 1, 1,
                enableVeloChange: speedChange);
            var edges = output.Where((pt, i) => i > 0 && pt.Tool != output[i - 1].Tool).ToList();
            Console.WriteLine($"T0={middleLength}, advance0/1={a0}/{a1}, speedChange={speedChange}: " +
                $"runs={edges[0].X:F3}/{edges[1].X - edges[0].X:F3}/{output[^1].X - edges[1].X:F3}");
        }
    }
    return 0;
}

// Boundary coordinates describe the start of the following material run.
var source = new List<Point3D>
{
    new(0, 0, tool: 1), new(20, 0, tool: 0),
    new(30, 0, tool: 1), new(50, 0, tool: 1)
};
var offset = PathGenerator.ApplyAdvanceToolOffset(source, 3, 6);
var processed = PathGenerator.DirectGeneratePath(source, 3, 6, 10, 10, 5, 5, 1, 1);
int failed = 0;
foreach (var (name, points) in new[] { ("offset", offset), ("ProcessLayer", processed) })
{
    var boundaries = points.Where((pt, i) => i > 0 && pt.Tool != points[i - 1].Tool).ToList();
    double length0 = boundaries[1].X - boundaries[0].X;
    bool pass = length0 >= 10 - 1e-6;
    Console.WriteLine($"{(pass ? "PASS" : "FAIL")} {name}: T0 original=10 mm, actual={length0:F6} mm; boundaries={string.Join(", ", boundaries.Select(pt => pt.X.ToString("F6")))}");
    if (!pass) failed++;
}
failed += Regression.Run();
failed += Acceptance.Run();
return failed == 0 ? 0 : 1;
