using System.Globalization;
using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// 读取 6 列 CSV 路径文件（无表头）：
///   X, Y, Z, G0/G1, T0/T1, P(气压)
/// 产出与 GcodeParser 一致的 ParsedGcode（Moves / Lines / Bounds），
/// 从而复用下游的 3D 渲染、统计（路径长度、切换次数）等全部逻辑。
///
/// 列含义兼容写法：
///   第4列(G0/G1)：0 或 "G0" → G0 空行程；1 或 "G1" 或其它 → G1 打印。
///   第5列(T0/T1)：取数值部分作为工具号（"T1"/"1" → 1）。
///   第6列(气压)：double，缺失按 0。
///
/// CSV 无层信息，所有 move 归入 Layer 0。
/// </summary>
public static class CsvPathReader
{
    public static ParsedGcode Parse(string path)
    {
        var text = File.ReadAllText(path);
        return ParseText(text, path);
    }

    public static ParsedGcode ParseText(string text, string path)
    {
        var result = new ParsedGcode { SourcePath = path };
        var rawLines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        double curX = 0, curY = 0, curZ = 0;   // 上一行终点（首行的起点 = 0,0,0）
        int moveIdx = 0;

        for (int i = 0; i < rawLines.Length; i++)
        {
            var original = rawLines[i];
            var line = original.Trim();
            var gline = new GcodeLine(i, original);
            result.Lines.Add(gline);

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // 逗号或空白分词；CSV 标准用逗号
            var fields = line.Split(',', ';', '\t');

            // 至少需要 X,Y,Z 三列；不足则跳过（容错）
            if (fields.Length < 3)
                continue;

            if (!TryParseDouble(fields[0], out double x) ||
                !TryParseDouble(fields[1], out double y) ||
                !TryParseDouble(fields[2], out double z))
                continue;

            MoveType type = fields.Length > 3 ? ParseType(fields[3]) : MoveType.G1;
            int tool = fields.Length > 4 ? ParseTool(fields[4]) : 0;
            double pressure = fields.Length > 5 && TryParseDouble(fields[5], out double p) ? p : 0;

            var move = new GcodeMove
            {
                Index = moveIdx++,
                LineNumber = i,
                Layer = 0,
                Type = type,
                Tool = tool,
                PrevX = curX, PrevY = curY, PrevZ = curZ,
                X = x, Y = y, Z = z,
                Pressure = pressure,
            };
            move.RecomputeSegmentLength();
            result.Moves.Add(move);
            gline.Move = move;

            curX = x; curY = y; curZ = z;
            result.Bounds.Include(x, y, z);
        }

        FinalizeToolAndCumulative(result);
        StatsCalculator.Recompute(result);
        return result;
    }

    /// <summary>计算累计长度并标注工具切换点（与 GcodeParser 一致）。</summary>
    private static void FinalizeToolAndCumulative(ParsedGcode result)
    {
        int prevTool = -1;
        double cum = 0;
        foreach (var m in result.Moves)
        {
            cum += m.SegmentLength;
            m.CumulativeLength = cum;
            if (m.Tool != prevTool && prevTool >= 0)
                m.IsToolChange = true;
            prevTool = m.Tool;
        }
    }



    private static MoveType ParseType(string s)
    {
        // "0"/"G0" → G0；"1"/"G1"/其它 → G1
        var t = s.Trim().ToUpperInvariant();
        if (t == "0" || t == "G0") return MoveType.G0;
        return MoveType.G1;
    }

    private static int ParseTool(string s)
    {
        // 取数值部分： "T1" → 1，"1" → 1
        var t = s.Trim();
        int n = 0;
        bool anyDigit = false;
        foreach (var ch in t)
        {
            if (ch >= '0' && ch <= '9') { n = n * 10 + (ch - '0'); anyDigit = true; }
            else if (anyDigit) break;   // 读到数字后又遇非数字则停止
        }
        return anyDigit ? n : 0;
    }

    private static bool TryParseDouble(string s, out double v)
        => double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out v);
}
