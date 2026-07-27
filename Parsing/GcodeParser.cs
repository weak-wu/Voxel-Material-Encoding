using System.Globalization;
using System.Text.RegularExpressions;
using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// 逐行解析 gcode。同时保留原始文本与解析结构，编辑后可基于 move 列表重算统计、
/// 无损保存。
/// 关键点：
/// - 兼容全角/半角标点（用户样本里 ;LAYER： 用全角冒号）。
/// - G0/G1 不带 E/F：仅用类型区分空行程/打印。
/// - 绝对模式(G90)为默认；支持 G91 相对、G92 坐标系偏置。
/// - T0/T1/Tn：工具切换跟踪。
/// </summary>
public static class GcodeParser
{
    // ;LAYER[:：] 0 —— 半角或全角冒号
    private static readonly Regex LayerRegex =
        new(@"(?i)LAYER\s*[:：]\s*(\d+)");

    // 行首指令：G0..G3 / Tn / G90 / G91 / G92。末尾不加 \b ——
    // 否则 "G0X10"(无空格) 因 0 与 X 之间无词边界而失配。G 后的数字由下方按数值归一化。
    private static readonly Regex WordRegex =
        new(@"(?i)^(G\d+|T\d+|M\d+)");

    // 单个轴参数：X12.3 / Y-4.5 / Z0 / E1.2 / F1500
    private static readonly Regex AxisRegex =
        new(@"(?i)(?<![A-Za-z])([XYZEF])(\s*[-+]?\d*\.?\d+)");

    // 行内速度参数：V5.000 / V-2.5（mm/s）。V 不入坐标，单独取值赋给 move.Speed。
    private static readonly Regex SpeedRegex =
        new(@"(?i)(?<![A-Za-z])V(\s*[-+]?\d*\.?\d+)");

    // 行内工具切换：T0 / T1 / Tn（兼容与 G1 同行的情况，如 "G1 X10 T1"）
    private static readonly Regex InlineTRegex =
        new(@"(?i)\bT(\d+)\b");

    /// <summary>把全角标点统一成半角，便于正则与分词。</summary>
    private static string Normalize(string s)
    {
        // 全角冒号、全角空格、全角数字/符号——只处理实际会遇到的
        return s
            .Replace('：', ':')
            .Replace('，', ',')
            .Replace('（', '(')
            .Replace('）', ')')
            .Replace('　', ' '); // 全角空格
    }

    public static ParsedGcode Parse(string path)
    {
        var text = File.ReadAllText(path);
        return ParseText(text, path);
    }

    public static ParsedGcode ParseText(string text, string path)
    {
        var result = new ParsedGcode { SourcePath = path };
        var rawLines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        // 解析状态
        double curX = 0, curY = 0, curZ = 0;   // 当前绝对位置
        bool absolute = true;                    // G90 默认绝对
        int currentLayer = 0;
        bool hasLayer = false;   // 是否遇到 ;LAYER 注释（标记源文件含层信息）
        int currentTool = 0;
        bool toolInitialized = false;            // 首个 T 指令前不视为切换

        int moveIdx = 0;

        for (int i = 0; i < rawLines.Length; i++)
        {
            var original = rawLines[i];
            var line = original.Trim();

            var gline = new GcodeLine(i, original);
            result.Lines.Add(gline);

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // 注释处理：;LAYER 等元数据在注释里
            int semi = line.IndexOf(';');
            string codePart = semi >= 0 ? line.Substring(0, semi).Trim() : line;
            string commentPart = semi >= 0 ? line.Substring(semi) : string.Empty;
            if (string.IsNullOrWhiteSpace(codePart) && !string.IsNullOrWhiteSpace(commentPart))
                gline.IsComment = true;

            // 层信息（从注释里取，半角化后再匹配）
            if (semi >= 0)
            {
                var mc = LayerRegex.Match(Normalize(commentPart));
                if (mc.Success)
                {
                    currentLayer = int.Parse(mc.Groups[1].Value, CultureInfo.InvariantCulture);
                    hasLayer = true;   // 标记源文件含 ;LAYER 层信息
                }
            }
            if (codePart.Length == 0)
                continue;

            var wordMatch = WordRegex.Match(codePart);
            if (!wordMatch.Success) continue;

            string word = wordMatch.Groups[1].Value.ToUpperInvariant();

            // G 词归一化为数值：兼容带前导零的 CNC 写法 G0/G00、G1/G01、G2/G02、G3/G03，
            // 以及 G90/G91/G92/G28 等。按数值判别，避免字符串 "G00"!="G0" 导致整行被漏算。
            int gnum = -1;
            bool isG = word[0] == 'G' && int.TryParse(word.AsSpan(1), out gnum);

            // 模式切换
            if (isG && gnum == 90) { absolute = true; continue; }
            if (isG && gnum == 91) { absolute = false; continue; }
            if (isG && gnum == 92)
            {
                // 坐标系重置：G92 X0 Y0 Z0 把当前点设为指定值
                var axes = ParseAxes(codePart);
                if (axes.TryGetValue('X', out var vx)) curX = vx;
                if (axes.TryGetValue('Y', out var vy)) curY = vy;
                if (axes.TryGetValue('Z', out var vz)) curZ = vz;
                continue;
            }

            // 工具切换 T0/T1/Tn
            if (word[0] == 'T')
            {
                int t = int.Parse(word.Substring(1), CultureInfo.InvariantCulture);
                currentTool = t;
                toolInitialized = true;
                continue;
            }

            // 仅处理 G0/G1 移动（G2/G3 圆弧暂按线性近似端点）
            if (!isG || (gnum != 0 && gnum != 1 && gnum != 2 && gnum != 3))
                continue;

            // 检测本行是否内嵌工具切换（如 "G1 X10 Y10 T1"），
            // 兼容多材料打印的紧凑 gcode 格式：T 与 G1 同行时同样触发工具追踪。
            var tMatch = InlineTRegex.Match(codePart);
            if (tMatch.Success)
            {
                int inlineTool = int.Parse(tMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                if (!toolInitialized || inlineTool != currentTool)
                {
                    currentTool = inlineTool;
                    toolInitialized = true;
                }
            }

            var move = new GcodeMove
            {
                Index = moveIdx++,
                LineNumber = i,
                Layer = currentLayer,
                Type = (gnum == 0) ? MoveType.G0 : MoveType.G1,
                Tool = currentTool,
                PrevX = curX, PrevY = curY, PrevZ = curZ,
                X = curX, Y = curY, Z = curZ,
            };

            var axisVals = ParseAxes(codePart);
            move.HasX = axisVals.ContainsKey('X');
            move.HasY = axisVals.ContainsKey('Y');
            move.HasZ = axisVals.ContainsKey('Z');

            if (axisVals.TryGetValue('X', out double nx))
                move.X = absolute ? nx : curX + nx;
            if (axisVals.TryGetValue('Y', out double ny))
                move.Y = absolute ? ny : curY + ny;
            if (axisVals.TryGetValue('Z', out double nz))
                move.Z = absolute ? nz : curZ + nz;

            // 行内速度 V（mm/s）：正常段/切换段速度不同，导出 CSV 时据此逐段算步长。
            var speedMatch = SpeedRegex.Match(codePart);
            if (speedMatch.Success &&
                double.TryParse(speedMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double sv))
                move.Speed = sv;

            move.RecomputeSegmentLength();
            result.Moves.Add(move);
            gline.Move = move;

            curX = move.X; curY = move.Y; curZ = move.Z;
            result.Bounds.Include(curX, curY, curZ);
        }

        // 标注工具切换点 & 计算累计长度（在所有 move 产出后做一遍）
        FinalizeToolAndCumulative(result, ref toolInitialized);
        StatsCalculator.Recompute(result);
        result.HasLayerInfo = hasLayer;
        return result;
    }

    /// <summary>从一行代码部分解析所有轴参数。</summary>
    private static Dictionary<char, double> ParseAxes(string codePart)
    {
        var dict = new Dictionary<char, double>(4);
        foreach (Match m in AxisRegex.Matches(codePart))
        {
            char axis = char.ToUpperInvariant(m.Groups[1].Value[0]);
            if (axis == 'E' || axis == 'F') continue; // 挤出/进给暂不入坐标
            if (double.TryParse(m.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
                dict[axis] = v;
        }
        return dict;
    }

    private static void FinalizeToolAndCumulative(ParsedGcode result, ref bool toolInitialized)
    {
        int prevTool = -1;
        double cum = 0;
        foreach (var m in result.Moves)
        {
            cum += m.SegmentLength;
            m.CumulativeLength = cum;
            if (toolInitialized && m.Tool != prevTool && prevTool >= 0)
                m.IsToolChange = true;
            prevTool = m.Tool;
        }
    }
}
