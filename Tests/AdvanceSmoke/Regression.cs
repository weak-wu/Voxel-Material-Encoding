using GcodeViewer.Models;
using GcodeViewer.Parsing;

static class Regression
{
    public static int Run()
    {
        int failed = 0, count = 0;
        var random = new Random(731);
        var lengths = new List<double[]>
        {
            new[] {20.0, 10, 20, 10, 20}, new[] {2.0, 10, 2, 10, 20},
            new[] {20.0, 0.05, 0.08, 0.03, 10}, new[] {0.0, 10, 20},
            new[] {20.0}, new[] {20.0, 10}
        };
        for (int i = 0; i < 30; i++)
            lengths.Add(Enumerable.Range(0, 7).Select(_ => 0.01 + random.NextDouble() * 12).ToArray());
        foreach (var runs in lengths)
        foreach (int firstTool in new[] {0, 1})
        foreach (var (a0, a1) in new[] {(3.0, 6.0), (6.0, 3.0), (0.0, 0.0), (0.0, 6.0), (6.0, 6.0)})
        foreach (bool speed in new[] {false, true})
        {
            double s = 0;
            var input = new List<Point3D>();
            for (int i = 0; i < runs.Length; i++)
            {
                // Reverse diagonal: arc length differs from X and uses all three axes.
                input.Add(new(-s / 3, s * 2 / 3, s * 2 / 3, tool: firstTool ^ (i % 2)));
                s += runs[i];
            }
            input.Add(new(-s / 3, s * 2 / 3, s * 2 / 3, tool: input[^1].Tool));
            count++;
            try
            {
                var stats = new AdvanceStats();
                var output = PathGenerator.DirectGeneratePath(input, a0, a1, 17, 11, 5, 7, 20, 20,
                    enableVeloChange: speed, stats: stats);
                var edges = output.Where((pt, i) => i > 0 && pt.Tool != output[i - 1].Tool).ToList();
                double Required(int index) => input[index].Tool == 0 ? a0 : a1;
                double ExpectedAdvance(int index) =>
                    Enumerable.Range(index, runs.Length - index).Select(Required).Max();
                double prefix = runs.Length > 1 ? ExpectedAdvance(1) : 0;
                int skipped = runs.Length > 1 && runs[0] < 1e-8 ? 1 : 0;
                if (edges.Count != runs.Length - 1 - skipped)
                    throw new Exception($"lost boundary: expected {runs.Length - 1 - skipped}, actual {edges.Count}");
                double sourceArc = runs[0];
                for (int i = 1; i < runs.Length; i++)
                {
                    double expected = sourceArc - ExpectedAdvance(i);
                    var edge = i <= skipped ? output[0] : edges[i - 1 - skipped];
                    double actual = -edge.X * 3;
                    if (Math.Abs(actual - expected) > 1e-6 || edge.Tool != input[i].Tool)
                        throw new Exception($"boundary {i}: expected arc {expected:F6}/T{input[i].Tool}, actual {actual:F6}/T{edge.Tool}");
                    sourceArc += runs[i];
                }
                if (Math.Abs(-output[0].X * 3 + prefix) > 1e-6 || output[^1].DistanceTo(input[^1]) > 1e-6)
                    throw new Exception("path endpoint moved");
                if (Math.Abs(output.Zip(output.Skip(1), (a,b) => a.DistanceTo(b)).Sum() - s - prefix) > 1e-6)
                    throw new Exception("geometric arc length changed");
                double runStartArc = -prefix;
                for (int i = 0; i < runs.Length; i++)
                {
                    double runEndArc = i + 1 < runs.Length
                        ? -((i < skipped ? output[0] : edges[i - skipped]).X) * 3 : -output[^1].X * 3;
                    if (runEndArc - runStartArc < runs[i] - 1e-6)
                        throw new Exception($"material run {i} shortened");
                    runStartArc = runEndArc;
                }
                if (stats.SwitchCount0 + stats.SwitchCount1 != runs.Length - 1)
                    throw new Exception("statistics counted transitions incorrectly");
            }
            catch (Exception ex)
            {
                failed++;
                if (failed <= 12) Console.WriteLine($"FAIL runs=[{string.Join(",", runs)}], first={firstTool}, advance={a0}/{a1}, speed={speed}: {ex.Message}");
            }
        }
        foreach (bool speed in new[] {false, true})
        {
            count++;
            var bent = new List<Point3D>
            {
                new(0,0,tool:1), new(10.13,0,tool:1), new(10.13,9.87,tool:0),
                new(15.13,9.87,tool:0), new(15.13,14.87,tool:1), new(35.13,14.87,tool:1)
            };
            var output = PathGenerator.DirectGeneratePath(bent, 3, 6, 17, 11, 5, 7, 1, 1,
                enableVeloChange: speed);
            double arc = output.Zip(output.Skip(1), (a,b) => a.DistanceTo(b)).Sum();
            double t0Length = output.Zip(output.Skip(1), (a,b) => a.Tool == 0 ? a.DistanceTo(b) : 0).Sum();
            if (Math.Abs(arc - 56) > 1e-6 || Math.Abs(t0Length - 10) > 1e-6)
            {
                failed++;
                Console.WriteLine($"FAIL bent path speed={speed}: total={arc:F6}/56, T0={t0Length:F6}/10");
            }
        }
        Console.WriteLine($"Regression: {count - failed}/{count} passed");
        return failed;
    }
}
