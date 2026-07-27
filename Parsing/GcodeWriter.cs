using System.Text;
using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>把 ParsedGcode 的行列表写回文本文件（含编辑后的修改），无损保留注释/空行。</summary>
public static class GcodeWriter
{
    public static string BuildText(ParsedGcode g)
    {
        var sb = new StringBuilder(g.Lines.Count * 24);
        for (int i = 0; i < g.Lines.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            sb.Append(g.Lines[i].RawText);
        }
        return sb.ToString();
    }

    public static void Save(ParsedGcode g, string path)
    {
        File.WriteAllText(path, BuildText(g));
    }

    /// <summary>导出每层统计为 CSV（供论文画图）。</summary>
    public static void ExportLayerCsv(ParsedGcode g, string path)
    {
        //导出内容：层号、起始行号、结束行号、移动次数、G1打印长度、G0空移长度、总长度、空移占比、换刀次数、G0/G1切换次数
        var sb = new StringBuilder();
        sb.AppendLine("Layer,StartLine,EndLine,MoveCount,G1PrintLength_mm,G0TravelLength_mm,TotalLength_mm,TravelRatio%,ToolChangeCount,G0G1SwitchCount");
        foreach (var l in g.Stats.Layers)
        {
            double total = l.TotalLength;
            double ratio = total > 0 ? l.G0Length / total * 100.0 : 0;
            sb.Append(l.Layer).Append(',')
              .Append(l.StartLine).Append(',')
              .Append(l.EndLine).Append(',')
              .Append(l.MoveCount).Append(',')
              .Append(l.G1Length.ToString("0.###")).Append(',')
              .Append(l.G0Length.ToString("0.###")).Append(',')
              .Append(total.ToString("0.###")).Append(',')
              .Append(ratio.ToString("0.##")).Append(',')
              .Append(l.ToolChangeCount).Append(',')
              .Append(l.G0G1SwitchCount).AppendLine();
        }
        File.WriteAllText(path, sb.ToString());
    }
}
