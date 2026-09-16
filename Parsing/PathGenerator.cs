using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// 路径生成器：自 AMCP.FrmPrintStep2 的 Path2Gcode 流程移植。
/// 流程：G-code 文件 → GetOriginalGcode（解析为 List&lt;Point3D&gt;）
///      → SimplifyPath（RDP 简化）→ 导出 CSV（原始/简化）。
/// 生成结果可经 ToParsedGcode 转换后在已有 Viewport3D 中直接查看。
/// </summary>
public static class PathGenerator
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ====================== 文件读取 ======================

    /// <summary>读取文本文件全部非空行（自 GV.ReadTextFileLines 移植，UTF-8）。</summary>
    public static string[] ReadTextFileLines(string fileName)
    {
        var lines = new List<string>();
        foreach (var line in File.ReadAllLines(fileName, Encoding.UTF8))
        {
            if (!string.IsNullOrEmpty(line))
                lines.Add(line);
        }
        return lines.ToArray();
    }

    // ====================== 包围盒解析 ======================

    /// <summary>
    /// 扫描 G-code 中所有 G0/G1 行，计算 X/Y/Z 的最小最大值及首个 XY 坐标。
    /// 自 GV.ParseMinMaxG0G1 移植；失败时输出全 0（库代码不弹窗）。
    /// </summary>
    public static void ParseMinMaxG0G1(string fileName,
        out double xMin, out double xMax, out double yMin, out double yMax,
        out double zMin, out double zMax, out double xFirst, out double yFirst)
    {
        const double EMPTY = 99999;
        xMin = EMPTY; yMin = EMPTY; zMin = EMPTY;
        xMax = -EMPTY; yMax = -EMPTY; zMax = -EMPTY;
        xFirst = 0; yFirst = 0;
        bool firstXYFound = false;

        try
        {
            foreach (var raw in File.ReadLines(fileName))
            {
                var line = raw.Trim();
                if (!line.StartsWith("G0") && !line.StartsWith("G1")) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                double? x = null, y = null, z = null;
                foreach (var part in parts)
                {
                    if (part.Length < 2) continue;
                    char axis = char.ToUpper(part[0]);
                    string valStr = part.Substring(1);
                    if (!double.TryParse(valStr, NumberStyles.Any, Inv, out double value)) continue;
                    switch (axis)
                    {
                        case 'X': x = value; break;
                        case 'Y': y = value; break;
                        case 'Z': z = value; break;
                    }
                }

                if (!firstXYFound && x.HasValue && y.HasValue)
                {
                    xFirst = x.Value; yFirst = y.Value; firstXYFound = true;
                }
                if (x.HasValue) { xMin = Math.Min(xMin, x.Value); xMax = Math.Max(xMax, x.Value); }
                if (y.HasValue) { yMin = Math.Min(yMin, y.Value); yMax = Math.Max(yMax, y.Value); }
                if (z.HasValue) { zMin = Math.Min(zMin, z.Value); zMax = Math.Max(zMax, z.Value); }
            }

            if (xMin == EMPTY) { xMin = xMax = 0; }
            if (yMin == EMPTY) { yMin = yMax = 0; }
            if (zMin == EMPTY) { zMin = zMax = 0; }
            if (!firstXYFound) { xFirst = xMin; yFirst = yMin; }
        }
        catch
        {
            xMin = xMax = yMin = yMax = zMin = zMax = 0;
        }
    }

    // ====================== G-code → Point3D ======================

    /// <summary>
    /// 解析 G-code 文件，提取坐标点序列（自 FrmPrintStep2.GetOriginalGcode 移植）。
    /// 坐标按最小值偏移归零（与原逻辑一致）。G1→Extrude=1，G0→Extrude=0。
    /// 注释 ;LAYER/;SPEED/;PRESSURE 分别提供层号、速度、气压。
    /// </summary>
    /// <param name="filePath">G-code 文件全路径。</param>
    /// <param name="defaultSpeed">缺省速度（无 ;SPEED 注释时使用）。</param>
    /// <param name="defaultPressure">缺省气压（无 ;PRESSURE 注释时使用）。</param>
    public static List<Point3D> GetOriginalGcode(string filePath, double defaultSpeed = 0, double defaultPressure = 0)
    {
        var points = new List<Point3D>();
        ParseMinMaxG0G1(filePath,
            out double xMin, out _, out double yMin, out _, out double zMin, out _, out _, out _);

        const double zoomXY = 1.0, zoomZ = 1.0;   // 1:1 缩放
        // 偏移量：使最小坐标对齐到 0（XY），Z 取反偏移（沿用原实现）
        double xOffset = -xMin;
        double yOffset = -yMin;
        // double zOffset = -zMin; // Z 不做偏移（原实现 zOffset 仅参与计算，此处保持原始 Z）

        try
        {
            var strLines = ReadTextFileLines(filePath);

            int layer = 0;
            double valueX = 0, valueY = 0, valueZ = 0, valueF = 0;
            double valueP = 0;
            int valueT = 0;
            // 上一目标位置（缺轴时沿用）
            double[] target = { 0, 0, 0 };

            for (int i = 0; i < strLines.Length; i++)
            {
                var strCmd = strLines[i].ToUpper();

                // ;LAYER / ;LAYER_COUNT
                if (strCmd.StartsWith(";LAYER"))
                {
                    var m = Regex.Match(strCmd, @"^\s*;LAYER(?:_COUNT)?\s*:?\s*(?<LAYER>[-+]?\d+)?", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var g = m.Groups["LAYER"];
                        if (g.Success && !string.IsNullOrWhiteSpace(g.Value) && int.TryParse(g.Value, out int pl))
                            layer = pl;
                        else
                            layer++;
                    }
                    continue;
                }
                // ;SPEED
                if (strCmd.StartsWith(";SPEED"))
                {
                    var m = Regex.Match(strCmd, @"\;SPEED[\s_]*:\s*(?<SPEED>[-+]?\d+(?:\.\d+)?)");
                    valueF = m.Success ? double.Parse(m.Groups["SPEED"].Value, Inv) : defaultSpeed;
                    continue;
                }
                // ;PRESSURE
                if (strCmd.StartsWith(";PRESSURE"))
                {
                    var m = Regex.Match(strCmd, @"\;PRESSURE[\s_]*:\s*(?<PRESSURE>[-+]?\d+(?:\.\d+)?)");
                    valueP = m.Success ? double.Parse(m.Groups["PRESSURE"].Value, Inv) : defaultPressure;
                    continue;
                }

                // 独立工具切换行 T0/T1（更新当前工具号，后续点继承；行内 T 仍可覆盖）
                var toolLineMatch = Regex.Match(strCmd, @"^\s*T(?<T>[-+]?\d+)\s*$");
                if (toolLineMatch.Success)
                {
                    valueT = int.Parse(toolLineMatch.Groups["T"].Value, Inv);
                    continue;
                }

                // 仅处理 G0/G1
                var cmdMatch = Regex.Match(strCmd, @"^(G0|G1)\s");
                if (!cmdMatch.Success) continue;
                int extrudeFlag = cmdMatch.Groups[1].Value == "G1" ? 1 : 0;

                // 各轴（无匹配则沿用上一目标位置）
                valueX = TryAxis(strCmd, 'X', out double dx) ? zoomXY * dx + xOffset : target[0];
                valueY = TryAxis(strCmd, 'Y', out double dy) ? zoomXY * dy + yOffset : target[1];
                valueZ = TryAxis(strCmd, 'Z', out double dz) ? zoomZ * dz : target[2];

                // 速度 V：行内有则更新，否则沿用上一行
                valueF = TryAxis(strCmd, 'V', out double dv) ? dv : valueF;
                // 喷头 T：行内有则更新；无则沿用当前工具（由独立 T0/T1 行设定），使工具切换生效
                if (TryAxisInt(strCmd, 'T', out int dt)) valueT = dt;

                target[0] = valueX; target[1] = valueY; target[2] = valueZ;

                points.Add(new Point3D(valueX, valueY, valueZ, extrudeFlag, valueF, valueP, valueT, layer));
            }
            return points;
        }
        catch
        {
            return points;
        }
    }

    /// <summary>
    /// 解析 G-code 文本（如刚生成的栅格 G-code）为 Point3D 点集，不做坐标偏移。
    /// 用于把 Picture2Gcode/Voxel2Gcode 生成的 G-code 文本转回点集，
    /// 供导出 CSV 与 3D 查看——此时每个点的 Tool 即体素/图片的材料值(depth)。
    /// </summary>
    public static List<Point3D> ParseGcodeText(string text, double defaultSpeed = 0, double defaultPressure = 0)
    {
        var points = new List<Point3D>();
        var strLines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        const double zoomXY = 1.0, zoomZ = 1.0;

        int layer = 0;
        double valueX = 0, valueY = 0, valueZ = 0, valueF = 0, valueP = 0;
        int valueT = 0;
        double[] target = { 0, 0, 0 };

        foreach (var rawLine in strLines)
        {
            var strCmd = rawLine.ToUpper();

            if (strCmd.StartsWith(";LAYER"))
            {
                var m = Regex.Match(strCmd, @"^\s*;LAYER(?:_COUNT)?\s*:?\s*(?<LAYER>[-+]?\d+)?", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var g = m.Groups["LAYER"];
                    if (g.Success && !string.IsNullOrWhiteSpace(g.Value) && int.TryParse(g.Value, out int pl))
                        layer = pl;
                    else
                        layer++;
                }
                continue;
            }
            if (strCmd.StartsWith(";SPEED"))
            {
                var m = Regex.Match(strCmd, @"\;SPEED[\s_]*:\s*(?<SPEED>[-+]?\d+(?:\.\d+)?)");
                valueF = m.Success ? double.Parse(m.Groups["SPEED"].Value, Inv) : defaultSpeed;
                continue;
            }
            if (strCmd.StartsWith(";PRESSURE"))
            {
                var m = Regex.Match(strCmd, @"\;PRESSURE[\s_]*:\s*(?<PRESSURE>[-+]?\d+(?:\.\d+)?)");
                valueP = m.Success ? double.Parse(m.Groups["PRESSURE"].Value, Inv) : defaultPressure;
                continue;
            }

            var toolLineMatch = Regex.Match(strCmd, @"^\s*T(?<T>[-+]?\d+)\s*$");
            if (toolLineMatch.Success) { valueT = int.Parse(toolLineMatch.Groups["T"].Value, Inv); continue; }

            var cmdMatch = Regex.Match(strCmd, @"^(G0|G1)\s");
            if (!cmdMatch.Success) continue;
            int extrudeFlag = cmdMatch.Groups[1].Value == "G1" ? 1 : 0;

            valueX = TryAxis(strCmd, 'X', out double dx) ? zoomXY * dx : target[0];
            valueY = TryAxis(strCmd, 'Y', out double dy) ? zoomXY * dy : target[1];
            valueZ = TryAxis(strCmd, 'Z', out double dz) ? zoomZ * dz : target[2];
            valueF = TryAxis(strCmd, 'V', out double dv) ? dv : valueF;
            if (TryAxisInt(strCmd, 'T', out int dt)) valueT = dt;

            target[0] = valueX; target[1] = valueY; target[2] = valueZ;
            points.Add(new Point3D(valueX, valueY, valueZ, extrudeFlag, valueF, valueP, valueT, layer));
        }
        return points;
    }

    /// <summary>提取某轴的浮点值（如 "X22.527" → 22.527）。</summary>
    private static bool TryAxis(string cmd, char axis, out double value)
    {
        var m = Regex.Match(cmd, axis + @"(?<V>[-+]?\d*\.\d+|[-+]?\d+)");
        if (m.Success)
            return double.TryParse(m.Groups["V"].Value, NumberStyles.Any, Inv, out value);
        value = 0;
        return false;
    }

    /// <summary>提取某轴的整型值（如 "T1" → 1）。</summary>
    private static bool TryAxisInt(string cmd, char axis, out int value)
    {
        var m = Regex.Match(cmd, axis + @"(?<V>[-+]?\d+)");
        if (m.Success)
            return int.TryParse(m.Groups["V"].Value, NumberStyles.Integer, Inv, out value);
        value = 0;
        return false;
    }

    // ====================== RDP 路径简化 ======================

    /// <summary>
    /// 按 Tool 分段对点集做 Ramer–Douglas–Peucker 三维简化。
    /// 自 FrmPrintStep2.SimplifyPath 移植。
    /// </summary>
    /// <param name="tolerance">简化阈值（mm），点到弦的最大允许距离。</param>
    public static List<Point3D> SimplifyPath(List<Point3D> points, double tolerance)
    {
        if (points == null) return new List<Point3D>();
        if (points.Count < 3 || tolerance <= 0) return new List<Point3D>(points);

        var simplified = new List<Point3D>();
        var segment = new List<Point3D>();

        for (int i = 0; i < points.Count; i++)
        {
            // 累积相同 Tool 的点为一段；Tool 变化时先简化上一段再开新段
            if (segment.Count == 0 || points[i].Tool == segment[segment.Count - 1].Tool)
            {
                segment.Add(points[i]);
            }
            else
            {
                if (segment.Count > 0) simplified.AddRange(DouglasPeucker3D(segment, tolerance));
                segment.Clear();
                segment.Add(points[i]);
            }
        }
        if (segment.Count > 0) simplified.AddRange(DouglasPeucker3D(segment, tolerance));
        return simplified;
    }

    /// <summary>RDP 三维递归简化（自 FrmPrintStep2.DouglasPeucker3D 移植）。</summary>
    public static List<Point3D> DouglasPeucker3D(List<Point3D> segment, double tolerance)
    {
        if (segment == null || segment.Count < 3)
            return new List<Point3D>(segment ?? new List<Point3D>());

        double maxDist = 0;
        int maxIndex = 0;
        var first = segment[0];
        var last = segment[segment.Count - 1];

        for (int i = 1; i < segment.Count - 1; i++)
        {
            double dist = PerpendicularDistance3D(segment[i], first, last);
            if (dist > maxDist) { maxDist = dist; maxIndex = i; }
        }

        var result = new List<Point3D>();
        if (maxDist > tolerance)
        {
            var left = DouglasPeucker3D(segment.GetRange(0, maxIndex + 1), tolerance);
            var right = DouglasPeucker3D(segment.GetRange(maxIndex, segment.Count - maxIndex), tolerance);
            result.AddRange(left);
            if (right.Count > 1) result.AddRange(right.GetRange(1, right.Count - 1));
        }
        else
        {
            result.Add(first);
            if (segment.Count > 1) result.Add(last);
        }
        return result;
    }

    /// <summary>点到三维线段的垂直距离（自 FrmPrintStep2.PerpendicularDistance3D 移植）。</summary>
    public static double PerpendicularDistance3D(Point3D point, Point3D lineStart, Point3D lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double dz = lineEnd.Z - lineStart.Z;

        if (dx == 0 && dy == 0 && dz == 0)
        {
            // 线段退化为点
            double px = point.X - lineStart.X;
            double py = point.Y - lineStart.Y;
            double pz = point.Z - lineStart.Z;
            return Math.Sqrt(px * px + py * py + pz * pz);
        }

        double t = ((point.X - lineStart.X) * dx +
                    (point.Y - lineStart.Y) * dy +
                    (point.Z - lineStart.Z) * dz) / (dx * dx + dy * dy + dz * dz);
        double projX = lineStart.X + t * dx;
        double projY = lineStart.Y + t * dy;
        double projZ = lineStart.Z + t * dz;

        double distX = point.X - projX;
        double distY = point.Y - projY;
        double distZ = point.Z - projZ;
        return Math.Sqrt(distX * distX + distY * distY + distZ * distZ);
    }

    // ====================== CSV 导出 ======================

    /// <summary>
    /// 将点集导出为带表头的 CSV（自 FrmPrintStep2.ExportPoint3DListToCsv 移植）。
    /// 列：X,Y,Z,Extrude,Feed,Pressure,Tool,Layer,GridType,MaterialA。
    /// </summary>
    /// <returns>是否写入成功。</returns>
    public static bool ExportPoint3DListToCsv(List<Point3D> points, string path)
    {
        try
        {
            if (points == null || points.Count == 0) return false;

            var lines = new string[points.Count + 1];
            lines[0] = "X,Y,Z,Extrude,Feed,Pressure,Tool,Layer,GridType,MaterialA";
            for (int i = 0; i < points.Count; i++)
            {
                var v = points[i];
                lines[i + 1] =
                    v.X.ToString("0.000", Inv) + "," +
                    v.Y.ToString("0.000", Inv) + "," +
                    (-1 * v.Z).ToString("0.000", Inv) + "," +//z是反向的
                    v.Extrude.ToString(Inv) + "," +
                    v.Feed.ToString("0.000", Inv) + "," +
                    v.Pressure.ToString("0.000", Inv) + "," +
                    v.Tool.ToString(Inv) + "," +
                    v.Layer.ToString(Inv) + "," +
                    v.GridType.ToString(Inv) + "," +
                    v.MaterialA.ToString();
            }
            File.WriteAllLines(path, lines, Encoding.UTF8);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ====================== Point3D → ParsedGcode（查看桥接）======================

    /// <summary>
    /// 把路径点集转换为 ParsedGcode，以便在已有的 Viewport3D 中渲染查看。
    /// 每个 Point3D 生成一条 GcodeMove（含起终点、段长、累计长度、工具切换标记）
    /// 与一条合成的源行 GcodeLine。
    /// </summary>
    public static ParsedGcode ToParsedGcode(List<Point3D> points, string sourcePath = "")
    {
        var result = new ParsedGcode { SourcePath = sourcePath };
        if (points == null || points.Count == 0) return result;

        // 首点 Prev 取自身坐标，使首条 move 为零长（SegmentLength=0）：
        // 渲染器(Viewport3D)对第一条可见 move 用 Prev 当线段起点且不跳过，
        // 若 Prev 为原点(0,0,0) 会画出"原点→gcode 起点"的长直线；取自身坐标后路径即从起点显示。
        double prevX = points[0].X, prevY = points[0].Y, prevZ = points[0].Z;
        int prevTool = -1;
        double cum = 0;

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var type = p.Extrude == 1 ? MoveType.G1 : MoveType.G0;

            // 合成可读源文本（供底部 gcode 浏览列与保存）
            string raw = $"{(type == MoveType.G1 ? "G1" : "G0")} " +
                         $"X{p.X.ToString("0.###", Inv)} Y{p.Y.ToString("0.###", Inv)} Z{p.Z.ToString("0.###", Inv)}";
            var gline = new GcodeLine(i, raw);

            var move = new GcodeMove
            {
                Index = i,
                LineNumber = i,
                Layer = p.Layer,
                Type = type,
                Tool = p.Tool,
                PrevX = prevX,
                PrevY = prevY,
                PrevZ = prevZ,
                X = p.X,
                Y = p.Y,
                Z = p.Z,
                HasX = true,
                HasY = true,
                HasZ = true,
                Pressure = p.Pressure,
            };
            move.RecomputeSegmentLength();
            cum += move.SegmentLength;
            move.CumulativeLength = cum;
            if (move.Tool != prevTool && prevTool >= 0)
                move.IsToolChange = true;
            prevTool = move.Tool;

            result.Moves.Add(move);
            gline.Move = move;
            result.Lines.Add(gline);
            result.Bounds.Include(p.X, p.Y, p.Z);

            prevX = p.X; prevY = p.Y; prevZ = p.Z;
        }

        StatsCalculator.Recompute(result);
        return result;
    }

    // ====================== G-code 生成（自 FrmPrintStep2 移植）======================

    internal const double PathEps = 1e-6;

    /// <summary>
    /// 构造一条 G1 指令字符串（自 FrmPrintStep2.BuildG1 移植）。
    /// 始终输出 V(速度) 与 T(工具)；气压非 0 时追加 P。
    /// </summary>
    public static string BuildG1(Point3D p, double? overrideFeed = null, double? overridePressure = null, int? overrideTool = null)
    {
        double feed = overrideFeed ?? p.Feed;
        double pressure = overridePressure ?? p.Pressure;
        int tool = overrideTool ?? p.Tool;

        string line = string.Format(Inv, "G1 X{0:F3} Y{1:F3} Z{2:F3} V{3:F3} T{4}", p.X, p.Y, p.Z, feed, tool);
        if (Math.Abs(pressure) > 1e-6)
            line += string.Format(Inv, " P{0:F1}", pressure);
        return line;
    }

    /// <summary>
    /// 按"工具切换提前 advanceDistance"规则生成路径 G-code（自 FrmPrintStep2.BuildPathGcodeByPictureRule 移植）。
    /// 在工具(喷头)切换边界前 advanceDistance 处插入切换点，切换点气压置 0。
    /// 变速/变压参数(vChange*/pChange*)为预留接口（与原软件一致，当前生效逻辑为提前切换）：
    ///   vChange0/disChange0/pChange0 对应 T0，vChange1/disChange1/pChange1 对应 T1。
    /// </summary>
    public static List<string> BuildPathGcodeByPictureRule(
        List<Point3D> points, double normalFeed, double advanceDistance,
        double vChange0, double vChange1, double disChange0, double disChange1,
        double pChange0, double pChange1)
    {
        var gcodes = new List<string>();
        if (points == null || points.Count == 0) return gcodes;

        int n = points.Count;
        // 1. 累计弧长
        var cumul = new double[n];
        cumul[0] = 0.0;
        for (int i = 1; i < n; i++)
            cumul[i] = cumul[i - 1] + points[i].DistanceTo(points[i - 1]);

        double totalLen = cumul[n - 1];
        //若总长度过短，则直接输出首点 G1
        if (totalLen <= PathEps)
        {
            gcodes.Add(BuildG1(points[0], overrideFeed: normalFeed));
            return gcodes;
        }

        // 2. 收集工具切换事件（0↔1 均提前 advanceDistance）
        var transitions = new List<(double boundaryS, int newTool, double advanceS)>();
        for (int i = 0; i < n - 1; i++)
        {
            if (points[i].Tool != points[i + 1].Tool)
            {
                double boundaryS = cumul[i + 1];
                int newTool = points[i + 1].Tool;
                double advS = Math.Max(0.0, boundaryS - advanceDistance);
                transitions.Add((boundaryS, newTool, advS));
            }
        }

        // 3. 关键弧长位置 = 原始点 + 提前点，排序去重
        var positions = new List<double>(cumul);
        foreach (var t in transitions)
            if (advanceDistance > PathEps) positions.Add(t.advanceS);

        var sorted = new List<double>();
        foreach (var s in positions.OrderBy(s => s))
        {
            double clamped = Math.Max(0.0, Math.Min(totalLen, s));
            if (sorted.Count == 0 || Math.Abs(sorted[sorted.Count - 1] - clamped) > PathEps)
                sorted.Add(clamped);
        }

        // 4. 逐位置定位并输出 G1
        int toolIdx = 0;
        foreach (double s in sorted)
        {
            int endIdx;
            if (s <= PathEps) endIdx = 1;
            else
            {
                int bIdx = Array.BinarySearch(cumul, s);
                if (bIdx >= 0) endIdx = Math.Max(1, bIdx);
                else { bIdx = ~bIdx; endIdx = Math.Min(Math.Max(bIdx, 1), n - 1); }
            }

            Point3D p;
            if (endIdx <= 0) p = ClonePoint(points[0]);
            else if (Math.Abs(cumul[endIdx] - s) <= PathEps) p = ClonePoint(points[endIdx]);
            else
            {
                double segStart = cumul[endIdx - 1];
                double segLen = cumul[endIdx] - segStart;
                double ratio = segLen <= PathEps ? 1.0 : Math.Max(0.0, Math.Min(1.0, (s - segStart) / segLen));
                p = InterpolatePoint(points[endIdx - 1], points[endIdx], ratio);
            }

            while (toolIdx < n - 1 && cumul[toolIdx + 1] <= s + PathEps) toolIdx++;
            int tool = points[toolIdx].Tool;
            double feed = Math.Abs(p.Feed) > PathEps ? p.Feed : normalFeed;
            double pressure = p.Pressure;

            // 按顺序应用切换事件（后面覆盖前面）
            foreach (var t in transitions)
            {
                if (s >= t.advanceS - PathEps)
                {
                    tool = t.newTool;
                    feed = Math.Abs(p.Feed) > PathEps ? p.Feed : normalFeed;
                    pressure = 0.0;
                }
            }

            string line = BuildG1(p, overrideFeed: feed, overrideTool: tool, overridePressure: pressure);
            if (gcodes.Count == 0 || gcodes[gcodes.Count - 1] != line)
                gcodes.Add(line);
        }
        return gcodes;
    }

    /// <summary>
    /// 按 Path2Gcode 规则生成完整 G-code 文本（含 ;LAYER/;SPEED/;PRESSURE 注释）。
    /// 勾选变速时按层调用 BuildPathGcodeByPictureRule；否则直接输出 G1 序列。
    /// </summary>
    /// <param name="feed">打印速度（写入每点 Feed 与 V）。</param>
    /// <param name="advanceDistance">提前出丝距离(mm)。</param>
    /// <param name="vChange">是否启用变速规则。</param>
    /// <param name="vc0/vc1">T0/T1 切换速度。</param>
    /// <param name="dc0/dc1">T0/T1 切换距离。</param>
    /// <param name="pc0/pc1">T0/T1 切换气压。</param>
    public static List<string> BuildPathGcode(List<Point3D> points, double feed, double advanceDistance, bool vChange,
        double vc0, double vc1, double dc0, double dc1, double pc0, double pc1)
    {
        List<string> result = new List<string>();
        if (points == null || points.Count == 0) return result;

        // 打印速度写入每点 Feed
        foreach (Point3D p in points) p.Feed = feed;

        if (vChange)
        {
            int layerCount = points.Max(p => p.Layer) + 1;
            for (int layer = 0; layer < layerCount; layer++)
            {
                var inLayer = points.Where(p => p.Layer == layer).ToList();
                if (inLayer.Count == 0) continue;
                result.AddRange(BuildPathGcodeByPictureRule(inLayer, feed, advanceDistance, vc0, vc1, dc0, dc1, pc0, pc1));
            }
        }
        else
        {
            foreach (var p in points) result.Add(BuildG1(p));
        }
        return result;
    }

    public static Point3D ClonePoint(Point3D p) =>
        new Point3D(p.X, p.Y, p.Z, p.Extrude, p.Feed, p.Pressure, p.Tool, p.Layer, p.GridType, p.MaterialA);

    public static Point3D InterpolatePoint(Point3D a, Point3D b, double ratio)
    {
        ratio = Math.Max(0.0, Math.Min(1.0, ratio));
        return new Point3D(
            a.X + (b.X - a.X) * ratio, a.Y + (b.Y - a.Y) * ratio, a.Z + (b.Z - a.Z) * ratio,
            b.Extrude, b.Feed, b.Pressure, b.Tool, b.Layer, b.GridType, b.MaterialA);
    }

    /// <summary>
    /// 按弧长在线段上等步长搜索插值点。
    /// 自 AMCP.FrmPrintStep2.SearchPoint 移植。
    /// </summary>
    /// <param name="startPoint">线段起点</param>
    /// <param name="endPoint">线段终点</param>
    /// <param name="moveStep">插值步长(mm)</param>
    /// <param name="remainder">传入/传出：上段剩余弧长</param>
    /// <param name="pointsOut">输出点列表</param>
    public static void SearchPoint(Point3D startPoint, Point3D endPoint, double moveStep,
        ref double remainder, List<Point3D> pointsOut)
    {
        Point3D vLine = new Point3D(
            endPoint.X - startPoint.X, endPoint.Y - startPoint.Y, endPoint.Z - startPoint.Z,
            0, 0, 0, 0, endPoint.Layer, 0, false);
        double lenLine = Math.Sqrt(vLine.X * vLine.X + vLine.Y * vLine.Y + vLine.Z * vLine.Z);
        if (lenLine < remainder)
        {
            remainder -= lenLine;
            return;
        }
        if (moveStep <= 1e-9) return;
        int countPoints = (int)Math.Ceiling((lenLine - remainder) / moveStep);
        Point3D eVline = new Point3D(
            vLine.X / lenLine, vLine.Y / lenLine, vLine.Z / lenLine,
            0, 0, 0, 0, endPoint.Layer, 0, false);
        for (int i = 0; i < countPoints; i++)
        {
            double t = i * moveStep + remainder;
            Point3D pt = new Point3D(
                startPoint.X + t * eVline.X, startPoint.Y + t * eVline.Y, startPoint.Z + t * eVline.Z,
                endPoint.Extrude, endPoint.Feed, endPoint.Pressure, endPoint.Tool,
                endPoint.Layer, endPoint.GridType, endPoint.MaterialA);
            pointsOut.Add(pt);
        }
        remainder = remainder + countPoints * moveStep - lenLine;
    }

    /// <summary>
    /// 对路径点集施加提前出丝距离偏移（Advance Tool Offset）。
    /// 在 Tool 切换边界处提前切换材料，补偿挤出滞后。
    /// 自 AMCP.FrmPrintStep2.ApplyAdvanceToolOffset 移植，并扩展为按"切入材料"分别设置提前距离。
    /// 后续材料段不缩短：所需提前量向前传播，仅初始段可压缩；初始段不足时限幅。
    /// </summary>
    /// <param name="points">原始路径点集（Tool 值已映射）</param>
    /// <param name="advanceDis0">切入材料 A(T0) 时所用提前出丝距离(mm)——即 B→A 切换的提前量</param>
    /// <param name="advanceDis1">切入材料 B(T1) 时所用提前出丝距离(mm)——即 A→B 切换的提前量</param>
    /// <returns>施加偏移后的点集</returns>
    public static List<Point3D> ApplyAdvanceToolOffset(List<Point3D> points, double advanceDis0, double advanceDis1, AdvanceStats? stats = null)
    {
        if (points == null || points.Count == 0) return new List<Point3D>();
        return ApplyAdvanceToolOffset(points, AdvancePlanner.Build(points, advanceDis0, advanceDis1, stats));
    }

    internal static List<Point3D> ApplyAdvanceToolOffset(List<Point3D> points,
        List<AdvancePlanner.Transition> transitions)
    {
        var result = new List<Point3D>();
        if (points == null || points.Count == 0) return result;

        int n = points.Count;
        // 1. 累计弧长
        double[] cumul = new double[n];
        cumul[0] = 0.0;
        for (int i = 1; i < n; i++)
            cumul[i] = cumul[i - 1] + points[i].DistanceTo(points[i - 1]);

        double totalLen = cumul[n - 1];
        if (totalLen <= PathEps)
        {
            result.Add(ClonePoint(points[0]));
            return result;
        }

        // 2. 使用统一规划的切换事件；顺序不变，压缩只传递到初始段。
        // 3. 关键弧长位置 = 原始点 + 提前点，排序去重
        var positions = new List<double>(cumul);
        foreach (var t in transitions)
            if (t.ActualAdvance > PathEps) positions.Add(t.AdvanceS);

        var sorted = new List<double>();
        foreach (var s in positions.OrderBy(s => s))
        {
            double clamped = Math.Max(0.0, Math.Min(totalLen, s));
            if (sorted.Count == 0 || Math.Abs(sorted[sorted.Count - 1] - clamped) > PathEps)
                sorted.Add(clamped);
        }

        // 4. 逐位置定位并生成点
        int toolIdx = 0;
        foreach (double s in sorted)
        {
            // 二分查找所在段
            int endIdx;
            if (s <= PathEps) endIdx = 1;
            else
            {
                int bIdx = Array.BinarySearch(cumul, s);
                if (bIdx >= 0) endIdx = Math.Max(1, bIdx);
                else { bIdx = ~bIdx; endIdx = Math.Min(Math.Max(bIdx, 1), n - 1); }
            }

            Point3D p;
            if (endIdx <= 0) p = ClonePoint(points[0]);
            else if (Math.Abs(cumul[endIdx] - s) <= PathEps) p = ClonePoint(points[endIdx]);
            else
            {
                double segStart = cumul[endIdx - 1];
                double segLen = cumul[endIdx] - segStart;
                double ratio = segLen <= PathEps ? 1.0 : Math.Max(0.0, Math.Min(1.0, (s - segStart) / segLen));
                p = InterpolatePoint(points[endIdx - 1], points[endIdx], ratio);
            }

            // 确定 Tool 值：按顺序应用切换事件
            while (toolIdx < n - 1 && cumul[toolIdx + 1] <= s + PathEps) toolIdx++;
            int tool = points[toolIdx].Tool;
            foreach (var t in transitions)
            {
                if (s >= t.AdvanceS - PathEps) tool = t.NewTool;
            }
            p.Tool = tool;
            result.Add(p);
        }
        return result;
    }

    /// <summary>
    /// 密度过滤器：去除过密点（距离 &lt; minDist）并填补过疏间隙（距离 &gt; maxDist）。
    /// Tool 切换边界点始终保留。自 AMCP.FrmPrintStep2.FilterDensePoints 移植。
    /// </summary>
    public static List<Point3D> FilterDensePoints(List<Point3D> points, double minDist, double maxDist)
    {
        if (points == null || points.Count < 2) return new List<Point3D>(points ?? new List<Point3D>());
        if (minDist <= PathEps) return new List<Point3D>(points);

        // Pass 1: 过滤过密点（保留 Tool 边界点）
        var filtered = new List<Point3D> { points[0] };
        for (int i = 1; i < points.Count; i++)
        {
            Point3D cur = points[i];
            Point3D last = filtered[filtered.Count - 1];
            double dist = cur.DistanceTo(last);
            if (dist >= minDist) { filtered.Add(cur); continue; }

            bool toolChangePrev = (cur.Tool != last.Tool);
            bool toolChangeNext = (i + 1 < points.Count) && (cur.Tool != points[i + 1].Tool);
            // 丢弃折线拐点会缩短几何弧长，材料段长也随之被压缩。
            bool isCorner = i + 1 < points.Count
                && last.DistanceTo(cur) + cur.DistanceTo(points[i + 1])
                    - last.DistanceTo(points[i + 1]) > PathEps * 0.01;
            if (toolChangePrev || toolChangeNext || isCorner)
                filtered.Add(cur); // 保留边界两侧，不能用新材料点覆盖旧材料点而提前切换。
        }
        // 确保最后一个点存在
        Point3D lastOrig = points[points.Count - 1];
        if (filtered[filtered.Count - 1].DistanceTo(lastOrig) > PathEps)
            filtered.Add(lastOrig);

        if (maxDist <= minDist) return filtered;

        // Pass 2: 填补过疏间隙
        var result = new List<Point3D> { filtered[0] };
        for (int i = 1; i < filtered.Count; i++)
        {
            Point3D prev = result[result.Count - 1];
            Point3D cur = filtered[i];
            double dist = cur.DistanceTo(prev);
            if (dist > maxDist)
            {
                Point3D dir = cur - prev;
                double len = dir.Length(dir);
                if (len > PathEps)
                {
                    Point3D unitDir = dir.Unitvector(dir);
                    double walked = 0;
                    while (dist - walked > maxDist + PathEps)
                    {
                        walked += minDist;
                        if (walked >= dist - PathEps) break;
                        Point3D fillPt = prev + walked * unitDir;
                        fillPt.Extrude = cur.Extrude;
                        fillPt.Feed = cur.Feed;
                        fillPt.Tool = prev.Tool;
                        fillPt.Pressure = cur.Pressure;
                        result.Add(fillPt);
                    }
                }
            }
            if (result[result.Count - 1].DistanceTo(cur) > PathEps)
                result.Add(cur);
        }
        return result;
    }

    /// <summary>
    /// 层间 Z 向过渡段的速度插值（补齐 DirectGeneratePath 按层独立处理时缺失的层间速度插值）。
    ///
    /// 原理：DirectGeneratePath 按 Layer 逐层独立插值，层与层之间的 Z 向移动（喷头抬升/下降）
    ///   不属于任何一层的连续弧长，原实现中该段既无中间点、也无速度，CSV 中 Z 直接跳变。
    ///   本方法在上一层末点 a 与本层首点 b 之间的三维直线段上，按等时间采样补齐中间点并写入速度，
    ///   使整条路径（含 Z 向）处处连续且带速度剖面。
    ///
    /// 数学模型：段长 L=‖b−a‖，过渡段速度 vz，采样周期 dt，则
    ///   步长 Δs = vz·dt；中间点参数 t_k = k·Δs（k=1,2,…，t_k＜L，不含端点），
    ///   点坐标 P_k = a + (t_k/L)·(b−a)，速度 Feed(P_k) = vz。
    ///   vz 取下一段（本层首点 b）所属材料的正常打印速度，与正常段一致。
    ///
    /// 说明：起点 a 不重复生成（已在上一层结果中），终点 b 由调用方随后加入；
    ///   中间点继承 b 的 Tool/Pressure/Extrude 等属性，保证过渡段与下一段材料一致。
    /// </summary>
    /// <param name="a">上一层末点（已在结果中，不重复生成）</param>
    /// <param name="b">本层首点（由调用方随后加入，不在此生成）</param>
    /// <param name="vz">过渡段速度(mm/s)=下一段材料正常速度</param>
    /// <param name="dt">采样周期(s)，步长 = vz×dt</param>
    /// <returns>过渡段中间点列表（不含 a、b）；首末重合或步长为 0 时返回空</returns>
    private static List<Point3D> InterpolateLayerTransition(Point3D a, Point3D b, double vz, double dt)
    {
        var pts = new List<Point3D>();
        double dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len <= PathEps) return pts;        // 首末重合：无过渡段，静默跳过（可恢复，不抛异常）

        double step = vz * dt;
        if (step <= PathEps) return pts;       // 防御：速度或周期为 0 时不插值

        double ux = dx / len, uy = dy / len, uz = dz / len;
        // 从距 a 为一个步长处开始等步长推进，严格不到达 b（b 由调用方加入）
        for (double t = step; t < len - PathEps; t += step)
        {
            pts.Add(new Point3D(
                a.X + t * ux, a.Y + t * uy, a.Z + t * uz,
                b.Extrude, vz, b.Pressure, b.Tool, b.Layer, b.GridType, b.MaterialA));
        }
        return pts;
    }
    /// <summary>
    /// 直接生成路径 CSV 的完整处理管线（自 AMCP.FrmPrintStep2.btnDirectGenerateCsv_Click 移植）。
    /// 按层处理：提前偏移 → 变速区域构建 → 关键点弧长→坐标 → 分段插值 → 密度过滤。
    /// 扩展：打印速度与提前出丝距离均按材料 A/B 分别设置——
    ///   材料 A = T0(Tool 0)、材料 B = T1(Tool 1)；各段步长按其所打印材料选择。
    ///
    /// 解耦说明：原巨型方法已拆为 协调层(本方法) + PathLayerProcessor(单层处理) + PathGenParams(参数对象)。
    ///   本方法仅负责 参数打包校验、按层编排、层间 Z 向过渡、Feed 兜底；
    ///   单层内的偏移/变速区域/关键点/插值/过滤全部委托 PathLayerProcessor.ProcessLayer。
    /// </summary>
    /// <param name="points">映射后的路径点集</param>
    /// <param name="advanceDis0">切入材料 A(T0) 的提前出丝距离(mm)——即 B→A 切换的提前量</param>
    /// <param name="advanceDis1">切入材料 B(T1) 的提前出丝距离(mm)——即 A→B 切换的提前量</param>
    /// <param name="velo0">材料 A(T0) 打印速度(mm/s)</param>
    /// <param name="velo1">材料 B(T1) 打印速度(mm/s)</param>
    /// <param name="vChange0">T0 切换速度(mm/s)</param>
    /// <param name="vChange1">T1 切换速度(mm/s)</param>
    /// <param name="disChange0">T0 变速距离(mm)</param>
    /// <param name="disChange1">T1 变速距离(mm)</param>
    /// <param name="dt">等时间采样周期(s，默认 0.02=50Hz)。各材料步长 = 速度 × dt，决定插值密度与密度过滤阈值。
    ///   与 StatsPanel 的 numdt 统一：dt 越小点越密、回放速度越贴近设计速度。</param>
    /// <param name="enableVeloChange">是否启用跨越切换点的变速规则。</param>
    /// <returns>处理后的路径点集</returns>
    public static List<Point3D> DirectGeneratePath(List<Point3D> points,
        double advanceDis0, double advanceDis1, double velo0, double velo1,
        double vChange0, double vChange1, double disChange0, double disChange1,
        double dt = 0.02, bool enableVeloChange = false, AdvanceStats? stats = null)
    {
        // 1) 工艺参数打包 + 合法性校验(原入口校验段搬入 PathGenParams.Validate)
        var p = new PathGenParams
        {
            AdvanceDis0 = advanceDis0,
            AdvanceDis1 = advanceDis1,
            Velo0 = velo0,
            Velo1 = velo1,
            VChange0 = vChange0,
            VChange1 = vChange1,
            DisChange0 = disChange0,
            DisChange1 = disChange1,
            Dt = dt,
            EnableVeloChange = enableVeloChange,
        };
        p.Validate();

        // 2) 空集短路
        var result = new List<Point3D>();
        if (points == null || points.Count == 0) return result;

        // 3) 按层处理：单层逻辑全部委托 PathLayerProcessor，本方法只做层间衔接
        int layerCount = points.Max(pt => pt.Layer) + 1;
        Point3D? prevLast = null;   // 上一非空层末点，用于层间 Z 向过渡段衔接
        for (int layerIdx = 0; layerIdx < layerCount; layerIdx++)
        {
            List<Point3D> pointsInLayer = points.Where(pt => pt.Layer == layerIdx).ToList();
            if (pointsInLayer.Count == 0) continue;
            
            //处理单层
            List<Point3D> layerOut = PathLayerProcessor.ProcessLayer(pointsInLayer, p, stats);
            //处理层间过渡
            AppendLayerTransition(result, ref prevLast, layerOut, p);
        }

        // 4) Feed 兜底
        FinalizeFeed(result, p);
        return result;
    }

    /// <summary>
    /// 将单层输出接入总结果：先补"上一层末点 → 本层首点"的 Z 向过渡段(按 step=vz×dt 补中间点、Feed=vz)，
    /// 再追加本层点，并更新 prevLast 为本层末点供下一层衔接。首层 prevLast 为 null 不补过渡。
    /// vz 取下一段(本层首点)所属材料的正常打印速度，与正常段保持一致。
    /// </summary>
    private static void AppendLayerTransition(List<Point3D> result, ref Point3D? prevLast,
        List<Point3D> layerOut, PathGenParams p)
    {
        if (layerOut.Count == 0) return;
        if (prevLast != null)
        {
            int nextTool = layerOut[0].Tool;
            double vz = (nextTool == 0) ? p.Velo0 : p.Velo1;   // 沿用下一段(本层首点)材料速度
            result.AddRange(InterpolateLayerTransition(prevLast, layerOut[0], vz, p.Dt));
        }
        result.AddRange(layerOut);
        prevLast = layerOut[^1];
    }

    /// <summary>
    /// Feed 兜底：变速区域点的 Feed 已在梯形插值时写入渐变速度，正常段点已写入材料正常速度；
    /// 此处仅对极少数未经过插值流程的点(如路径过短直通点)按材料正常速度补齐。
    /// </summary>
    private static void FinalizeFeed(List<Point3D> result, PathGenParams p)
    {
        foreach (var pt in result)
            if (pt.Feed <= PathEps)
                pt.Feed = (pt.Tool == 0) ? p.Velo0 : p.Velo1;
    }


    public static List<Point3D> AdvanceToolOffset(List<Point3D> points,
        double advanceDis0, double advanceDis1, double velo0, double velo1,
        double vChange0, double vChange1, double disChange0, double disChange1,
        double dt = 0.02, bool enableVeloChange = false)
    {
        List<Point3D> result = new List<Point3D>();

        return result;
    }

}
