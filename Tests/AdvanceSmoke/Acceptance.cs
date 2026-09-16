using GcodeViewer.Models;
using GcodeViewer.Parsing;

static class Acceptance
{
    public static int Run()
    {
        int failed = 0;
        void Check(string name, Action test)
        {
            try { test(); Console.WriteLine($"PASS {name}"); }
            catch (Exception e) { failed++; Console.WriteLine($"FAIL {name}: {e.Message}"); }
        }
        void Near(double actual, double expected)
        {
            if (Math.Abs(actual - expected) > 1e-6)
                throw new Exception($"expected {expected}, actual {actual}");
        }
        var source = new List<Point3D>
        {
            new(0, 0, tool: 1), new(20, 0, tool: 0),
            new(30, 0, tool: 1), new(50, 0, tool: 1)
        };
        foreach (bool speed in new[] { false, true })
        Check($"first run compensation speed={speed}", () =>
        {
            var output = PathGenerator.DirectGeneratePath(source, 3, 6, 10, 12, 20, 24, 4, 4,
                enableVeloChange: speed);
            Near(output[0].X, -6);
            var edges = output.Where((p, i) => i > 0 && p.Tool != output[i - 1].Tool).ToList();
            Near(edges[0].X - output[0].X, 20);
            Near(edges[1].X - edges[0].X, 10);
            Near(output[^1].X - edges[1].X, 26);
        });
        Check("speed reaches transition at 5% and recovers over final 5%", () =>
        {
            var output = PathGenerator.DirectGeneratePath(source, 3, 6, 10, 12, 20, 24, 4, 4,
                enableVeloChange: true);
            double Feed(double x)
            {
                var matches = output.Where(p => Math.Abs(p.X - x) < 1e-6).ToList();
                if (matches.Count == 0) throw new Exception($"missing speed keypoint x={x}");
                foreach (var p in matches) Near(p.Feed, matches[0].Feed);
                return matches[0].Feed;
            }
            // First actual switch is x=14; zone [14,18], ramps [14,14.2] and [17.8,18].
            Near(Feed(14), 12);
            Near(Feed(14.2), 20);
            Near(Feed(17.8), 20);
            Near(Feed(18), 10);
        });
        Check("zone ending at final path point restores print speed", () =>
        {
            var output = PathGenerator.DirectGeneratePath(new() { new(0,0,tool:1), new(20,0,tool:0) },
                6, 3, 10, 12, 20, 24, 6, 6, enableVeloChange: true);
            Near(output[^1].Feed, 10);
        });
        foreach (int newTool in new[] { 0, 1 })
        foreach (double vc in new[] { 5.0, 25.0 })
        Check($"5% curve newTool={newTool}, transition speed={vc}", () =>
        {
            var output = PathGenerator.DirectGeneratePath(new() { new(0,0,tool:1-newTool), new(20,0,tool:newTool), new(40,0,tool:newTool) },
                6, 6, 10, 12, vc, vc, 4, 4, dt:0.2, enableVeloChange:true);
            void SpeedAt(double x, double expected)
            {
                var matches = output.Where(p => Math.Abs(p.X - x) < 1e-6).ToList();
                if (matches.Count == 0) throw new Exception($"missing curve keypoint x={x}");
                foreach (var p in matches) Near(p.Feed, expected);
            }
            SpeedAt(14, newTool == 0 ? 12 : 10);
            SpeedAt(14.2, vc);
            SpeedAt(17.8, vc);
            SpeedAt(18, newTool == 0 ? 10 : 12);
        });
        foreach (int firstTool in new[] { 0, 1 })
        foreach (bool speed in new[] { false, true })
        Check($"short first run and large advance tool={firstTool}, speed={speed}", () =>
        {
            var input = new List<Point3D> { new(0,0,tool:firstTool), new(1,0,tool:1-firstTool), new(2,0,tool:1-firstTool) };
            var stats = new AdvanceStats();
            var output = PathGenerator.DirectGeneratePath(input, 50, 50, 10, 12, 20, 24, 50, 50,
                enableVeloChange: speed, stats: stats);
            Near(output[0].X, -50);
            var boundary = output.First(p => p.Tool != firstTool);
            Near(boundary.X - output[0].X, 1);
            Near(output[^1].X - boundary.X, 51);
            Near(stats.StartCompensationLength, 50);
            Near(input[0].X, 0);
        });
        Check("layer compensation uses each layer direction", () =>
        {
            var input = new List<Point3D>
            {
                new(0,0,tool:1,layer:0), new(20,0,tool:0,layer:0), new(30,0,tool:0,layer:0),
                new(0,0,1,tool:0,layer:1), new(0,20,1,tool:1,layer:1), new(0,30,1,tool:1,layer:1)
            };
            var stats = new AdvanceStats();
            var output = PathGenerator.DirectGeneratePath(input, 3, 6, 10, 12, 20, 24, 2, 2,
                enableVeloChange:true, stats:stats);
            Near(output[0].X, -3);
            if (!output.Any(p => p.Layer == 1 && Math.Abs(p.Y + 6) < 1e-6 && Math.Abs(p.Z - 1) < 1e-6))
                throw new Exception("second layer compensation start missing");
            Near(stats.StartCompensationLength, 9);
            Near(stats.CompensatedLayerCount, 2);
        });
        return failed;
    }
}
