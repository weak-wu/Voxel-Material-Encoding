using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>沿弧长安排切换，提前量向前传播，并在起点前补偿首段，保证各材料段不缩短。</summary>
internal static class AdvancePlanner
{
    internal sealed record Transition(double BoundaryS, int OldTool, int NewTool,
        double AdvanceS, double ActualAdvance);

    public static List<Transition> Build(List<Point3D> points, double advance0, double advance1,
        AdvanceStats? stats = null)
    {
        var result = new List<Transition>();
        double arc = 0;
        for (int i = 1; i < points.Count; i++)
        {
            arc += points[i].DistanceTo(points[i - 1]);
            if (points[i].Tool != points[i - 1].Tool)
                result.Add(new Transition(arc, points[i - 1].Tool, points[i].Tool, arc, 0));
        }
        if (result.Count == 0) return result;

        // 后续段 L' = L + 本次提前量 - 下次提前量。
        // 从后向前传播最大提前量，保证 L' >= L；首段损失由起点前的补偿路径补足。
        double requiredAdvance = 0;
        for (int i = result.Count - 1; i >= 0; i--)
        {
            var t = result[i];
            double requested = Math.Max(0, t.NewTool == 0 ? advance0 : advance1);
            requiredAdvance = Math.Max(requiredAdvance, requested);
            double actual = requiredAdvance;
            result[i] = t with { AdvanceS = t.BoundaryS - actual, ActualAdvance = actual };
            stats?.Record(t.NewTool, requested, actual, PathGenerator.PathEps);
        }
        return result;
    }

    /// <summary>沿首个有效路径段的反方向延长起点，并将切换弧长平移到补偿后的坐标系。</summary>
    public static (List<Point3D> Points, List<Transition> Transitions) Prepare(
        List<Point3D> points, double advance0, double advance1, AdvanceStats? stats = null)
    {
        var transitions = Build(points, advance0, advance1, stats);
        double prefix = transitions.Count == 0 ? 0 : transitions[0].ActualAdvance;
        if (prefix <= PathGenerator.PathEps) return (points, transitions);

        var start = points[0];
        var directionEnd = points.Skip(1).FirstOrDefault(p => p.DistanceTo(start) > PathGenerator.PathEps);
        if (directionEnd == null)
            throw new ArgumentException("路径没有有效方向，无法在起点前补偿首段提前量。");
        double scale = prefix / start.DistanceTo(directionEnd);
        var extendedStart = PathGenerator.ClonePoint(start);
        extendedStart.X -= (directionEnd.X - start.X) * scale;
        extendedStart.Y -= (directionEnd.Y - start.Y) * scale;
        extendedStart.Z -= (directionEnd.Z - start.Z) * scale;
        var extended = new List<Point3D>(points.Count + 1) { extendedStart };
        extended.AddRange(points);
        var shifted = transitions.Select(t => t with
        {
            BoundaryS = t.BoundaryS + prefix,
            AdvanceS = t.AdvanceS + prefix
        }).ToList();
        if (stats != null)
        {
            stats.StartCompensationLength += prefix;
            stats.CompensatedLayerCount++;
        }
        return (extended, shifted);
    }
}
