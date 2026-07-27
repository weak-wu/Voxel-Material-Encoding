using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// 对 move 列表聚合统计。可对同一 ParsedGcode 反复调用（编辑后重算）。
/// 不重新解析文件，O(n)。
/// </summary>
public static class StatsCalculator
{
    public static void Recompute(ParsedGcode g)
    {
        var s = g.Stats;
        s.LayerCount = 0;
        s.MoveCount = g.Moves.Count;
        s.G0Count = 0;
        s.G1Count = 0;
        s.G1PrintLength = 0;
        s.G0TravelLength = 0;
        s.ToolChangeCount = 0;
        s.G0G1SwitchCount = 0;// 统计 G0/G1 切换次数
        s.AvgToolChangeDistance = 0;
        s.LengthPerTool.Clear();
        s.Layers.Clear();

        if (g.Moves.Count == 0) return;

        // 各工具段：仅累加 G1（打印）；切换间距用累计路径差
        double lastSwitchCum = 0;
        double switchSum = 0;

        // 层聚合
        var layerMap = new Dictionary<int, LayerInfo>();
        int prevTool = -1;
        MoveType? prevType = null;   // null = 尚无上一条 move（首条不计为切换）
        foreach (var m in g.Moves)
        {
            // G0/G1 切换：本条类型与上一条不同才算（首条 prevType==null 不计）。
            // 同时给 move 打上 IsG0G1Switch 标志，供每层统计与渲染复用。
            m.IsG0G1Switch = prevType.HasValue && m.Type != prevType.Value;
            if (m.IsG0G1Switch) s.G0G1SwitchCount++;

            if (m.Type == MoveType.G1)
            {
                s.G1Count++;
                s.G1PrintLength += m.SegmentLength;
                if (!s.LengthPerTool.ContainsKey(m.Tool)) s.LengthPerTool[m.Tool] = 0;
                s.LengthPerTool[m.Tool] += m.SegmentLength;
            }
            else
            {
                s.G0Count++;
                s.G0TravelLength += m.SegmentLength;
            }
            prevType = m.Type;

            if (m.IsToolChange && prevTool >= 0)
            {
                s.ToolChangeCount++;
                switchSum += (m.CumulativeLength - lastSwitchCum);
                lastSwitchCum = m.CumulativeLength;
            }
            prevTool = m.Tool;

            if (!layerMap.TryGetValue(m.Layer, out var li))
            {
                li = new LayerInfo { Layer = m.Layer, StartLine = m.LineNumber };
                layerMap[m.Layer] = li;
            }
            li.EndLine = m.LineNumber;
            li.MoveCount++;
            if (m.Type == MoveType.G1) li.G1Length += m.SegmentLength;
            else li.G0Length += m.SegmentLength;
            if (m.IsToolChange) li.ToolChangeCount++;
            if (m.IsG0G1Switch) li.G0G1SwitchCount++;
        }

        s.LayerCount = layerMap.Count;
        if (s.ToolChangeCount > 0)
            s.AvgToolChangeDistance = switchSum / s.ToolChangeCount;

        // 层按编号排序
        foreach (var kv in layerMap.OrderBy(kv => kv.Key))
            s.Layers.Add(kv.Value);
    }

    /// <summary>
    /// 坐标/类型编辑后调用：重算受影响 move 的段长、其后所有 move 的累计长度、
    /// 工具切换标记，再聚合统计。startIndex 为首个坐标发生变化的 move 下标。
    /// </summary>
    public static void RecomputeAfterEdit(ParsedGcode g, int startIndex)
    {
        if (g.Moves.Count == 0) return;
        if (startIndex < 0) startIndex = 0;
        if (startIndex >= g.Moves.Count) startIndex = g.Moves.Count - 1;

        // 重算从 startIndex 起每段的 Prev/SegmentLength 与累计长度
        // startIndex 的 Prev 取自前一条 move 的终点（或自身，若为第一条）
        double cum = startIndex > 0 ? g.Moves[startIndex - 1].CumulativeLength : 0;
        for (int i = startIndex; i < g.Moves.Count; i++)
        {
            var m = g.Moves[i];
            if (i > 0)
            {
                var p = g.Moves[i - 1];
                m.PrevX = p.X; m.PrevY = p.Y; m.PrevZ = p.Z;
            }
            m.RecomputeSegmentLength();
            cum += m.SegmentLength;
            m.CumulativeLength = cum;
        }

        // 重算包围盒（坐标编辑可能改变极值）
        g.Bounds.IsValid = false;
        g.Bounds.MinX = g.Bounds.MinY = g.Bounds.MinZ = double.MaxValue;
        g.Bounds.MaxX = g.Bounds.MaxY = g.Bounds.MaxZ = double.MinValue;
        foreach (var m in g.Moves)
            g.Bounds.Include(m.X, m.Y, m.Z);

        Recompute(g);
    }
}
