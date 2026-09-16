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
/// CSV 无 ;LAYER 注释，解析后由 <see cref="ZLayerDetector"/> 按 Z 坐标聚类成合成层
/// （HasLayerInfo=true、IsZLayered=true），从而支持逐层查看。
/// </summary>
public static class CsvPathReader
{
    public static ParsedGcode Parse(string path)
    {
        var text = File.ReadAllText(path);
        return ParseText(text, path);
    }

    //解析csv
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

            MoveType type = fields.Length > 3 ? ParseType(fields[3]) : MoveType.G1;//存在第4列(G0/G1)则解析，否则默认 G1
            int tool = fields.Length > 4 ? ParseTool(fields[4]) : 0;
            double pressure = fields.Length > 5 && TryParseDouble(fields[5], out double p) ? p : 0;
            // 第7列(可选) = 速度 V(mm/s)：PathGenerator 生成的 CSV 末列 Feed 即点速度。读入后按速度着色
            // 直接用真实速度，无需点距反推(杜绝采样网格伪影)。缺列/解析失败 → 0，着色时退化为点距/dt 反推。
            double speed = fields.Length > 6 && TryParseDouble(fields[6], out double sp) ? sp : 0;
            bool isFirst = moveIdx == 0;
            var move = new GcodeMove
            {
                Index = moveIdx++,
                LineNumber = i,
                Layer = 0,
                Type = type,
                Tool = tool,
                // 首条 move 的 Prev 设为自身坐标
                PrevX = isFirst ? x : curX,
                PrevY = isFirst ? y : curY,
                PrevZ = isFirst ? z : curZ,
                X = x, Y = y, Z = z,
                Pressure = pressure,
                Speed = speed,
            };
            move.RecomputeSegmentLength();
            result.Moves.Add(move);
            gline.Move = move;

            curX = x; curY = y; curZ = z;
            result.Bounds.Include(x, y, z);
        }

        FinalizeToolAndCumulative(result);

        // CSV 无 ;LAYER 注释：按 Z 坐标聚类成合成层，使层列表 / 层过滤 / 按层着色均可用于 CSV，
        // 实现逐层查看。必须在 Recompute 之前赋 move.Layer，以便统计按层聚合 LayerInfo。
        int zLayerCount = ZLayerDetector.AssignLayers(result.Moves, result.LayerZMap);
        result.HasLayerInfo = zLayerCount > 0;
        result.IsZLayered = true;

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
        if (t == "G0") return MoveType.G0;

        if (double.TryParse(t, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d))
        {
            return Math.Abs(d) < 1e-9 ? MoveType.G0 : MoveType.G1;
        }
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
