using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>沿弧长安排切换，后续段的长度不足向前传递，仅允许压缩初始材料段。</summary>
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
        // 从后向前传播最大提前量，保证 L' >= L；可借用长度仅来自初始段。
        double initialLength = result[0].BoundaryS;
        double requiredAdvance = 0;
        for (int i = result.Count - 1; i >= 0; i--)
        {
            var t = result[i];
            double requested = Math.Max(0, t.NewTool == 0 ? advance0 : advance1);
            requiredAdvance = Math.Max(requiredAdvance, requested);
            double actual = Math.Min(requiredAdvance, initialLength);
            result[i] = t with { AdvanceS = t.BoundaryS - actual, ActualAdvance = actual };
            stats?.Record(t.NewTool, requested, actual, PathGenerator.PathEps);
        }
        return result;
    }
}
