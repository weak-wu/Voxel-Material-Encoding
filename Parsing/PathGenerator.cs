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

    private const double PathEps = 1e-6;

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
    /// </summary>
    /// <param name="points">原始路径点集（Tool 值已映射）</param>
    /// <param name="advanceDis0">切入材料 A(T0) 时所用提前出丝距离(mm)——即 B→A 切换的提前量</param>
    /// <param name="advanceDis1">切入材料 B(T1) 时所用提前出丝距离(mm)——即 A→B 切换的提前量</param>
    /// <returns>施加偏移后的点集</returns>
    public static List<Point3D> ApplyAdvanceToolOffset(List<Point3D> points, double advanceDis0, double advanceDis1)
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

        // 2. 收集 Tool 切换事件：按"切入材料 newTool"选择对应提前距离
        //    newTool==0(A) 用 advanceDis0；newTool==1(B) 用 advanceDis1
        var transitions = new List<(double boundaryS, int newTool, double advanceS, double adv)>();
        // 先收集所有 Tool 切换边界(弧长 + 切入材料)，供下方按相邻体素段长约束 advance
        var switches = new List<(double boundaryS, int newTool)>();
        for (int i = 0; i < n - 1; i++)
        {
            if (points[i].Tool != points[i + 1].Tool)
                switches.Add((cumul[i + 1], points[i + 1].Tool));
        }

        
        for (int i = 0; i < n - 1; i++)
        {
            if (points[i].Tool != points[i + 1].Tool)
            {
                int k = transitions.Count;              // 当前切换序号(添加前)，用于查 switches[k]
                double boundaryS = switches[k].boundaryS;
                int newTool = switches[k].newTool;
                double adv = newTool == 0 ? advanceDis0 : advanceDis1;

                // 前段(oldTool)/后段(newTool)长度：advance 不得大于二者，否则新材料提前出丝会挤压相邻体素
                double prevBoundary = k > 0 ? switches[k - 1].boundaryS : 0.0;
                double oldLen = boundaryS - prevBoundary;
                double nextBoundary = k + 1 < switches.Count ? switches[k + 1].boundaryS : totalLen;
                double newLen = nextBoundary - boundaryS;

                // 首个切换(k==0)前为路径最前端，允许被吞(不保留)，故仅受后段(newLen)约束
                double cap = (k == 0) ? newLen : Math.Min(oldLen, newLen);

                // 静默限幅：advance 超过相邻体素段长上限时自动回缩至 cap(不抛异常)，避免挤压相邻体素
                double advS = Math.Max(prevBoundary, boundaryS - Math.Min(adv, cap));
                double actualAdv = boundaryS - advS;     // 实际生效提前量(≥0)
                transitions.Add((boundaryS, newTool, advS, actualAdv));
            }
        }
        transitions = transitions
    .OrderBy(t => t.advanceS)
    .ThenBy(t => t.boundaryS)
    .ToList();
        // 3. 关键弧长位置 = 原始点 + 提前点，排序去重
        var positions = new List<double>(cumul);
        foreach (var t in transitions)
            if (t.adv > PathEps) positions.Add(t.advanceS);

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
                if (s >= t.advanceS - PathEps) tool = t.newTool;
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
            if (toolChangePrev || toolChangeNext)
                filtered[filtered.Count - 1] = cur;
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
                        fillPt.Tool = cur.Tool;
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
    /// 直接生成路径 CSV 的完整处理管线（自 AMCP.FrmPrintStep2.btnDirectGenerateCsv_Click 移植）。
    /// 按层处理：提前偏移 → 变速区域构建 → 关键点弧长→坐标 → 分段插值 → 密度过滤。
    /// 扩展：打印速度与提前出丝距离均按材料 A/B 分别设置——
    ///   材料 A = T0(Tool 0)、材料 B = T1(Tool 1)；各段步长按其所打印材料选择。
    /// </summary>
    /// <param name="points">映射后的路径点集</param>
    /// <param name="advanceDis0">切入材料 A(T0) 的提前出丝距离(mm)——即 B→A 切换的提前量</param>
    /// <param name="advanceDis1">切入材料 B(T1) 的提前出丝距离(mm)——即 A→B 切换的提前量</param>
    /// <param name="velo0">材料 A(T0) 打印速度(mm/s)</param>
    /// <param name="velo1">材料 B(T1) 打印速度(mm/s)</param>
    /// <param name="vChange0">T0 切换速度(mm/s)</param>
    /// <param name="vChange1">T1 切换速度(mm/s)</param>
    /// <param name="disChange0">T0 切换距离(mm)</param>
    /// <param name="disChange1">T1 切换距离(mm)</param>
    /// <param name="dt">等时间采样周期(s，默认 0.02=50Hz)。各材料步长 = 速度 × dt，决定插值密度与密度过滤阈值。
    ///   与 StatsPanel 的 numdt 统一：dt 越小点越密、回放速度越贴近设计速度。</param>
    /// <returns>处理后的路径点集</returns>
    /// <summary>
    /// 跨越切换点的变速区域 [zStart, zEnd] 内弧长 s 处的折线渐变速度（匀速变化、全程无突变）。
    /// 切换点 zMid(=zs) 处速度 = vc；前半 [zStart, zMid]：旧材料速度 veloOld 线性渐变到 vc；
    /// 后半 [zMid, zEnd]：vc 线性渐变到新材料速度 veloNew。
    /// 例：veloOld=2、vc=4、veloNew=5 → 2→4→5 单调线性过渡，切换点处恰好为 vc，与两端正常段连续。
    /// 渐变速率由切换距离 dc(=zEnd−zStart) 与速度差共同决定——dc 越大变化越平缓。
    /// </summary>
    /// <param name="s">当前点弧长</param>
    /// <param name="zStart">变速区域起点弧长（zs − dc/2，旧材料侧）</param>
    /// <param name="zMid">切换点弧长（=zs，区域中点）</param>
    /// <param name="zEnd">变速区域终点弧长（zs + dc/2，新材料侧）</param>
    /// <param name="veloOld">旧材料正常速度</param>
    /// <param name="vc">变速速度（切换点处达到）</param>
    /// <param name="veloNew">新材料正常速度</param>
    /// <returns>s 处的渐变速度（mm/s）</returns>
    private static double SpeedAtBridge(double s, double zStart, double zRampEnd, double zHoldEnd, double zEnd, double veloOld, double vc, double veloNew)
    {
        if (s <= zRampEnd)
        {
            double len = zRampEnd - zStart;
            if (len <= PathEps) return vc;
            double t = Math.Clamp((s - zStart) / len, 0.0, 1.0);
            return veloOld + (vc - veloOld) * t;
        }

        if (s <= zHoldEnd)
            return vc;

        double lenOut = zEnd - zHoldEnd;
        if (lenOut <= PathEps) return vc;

        double tOut = Math.Clamp((s - zHoldEnd) / lenOut, 0.0, 1.0);
        return vc + (veloNew - vc) * tOut;
    }

    /// <summary>
    /// 跨越切换点的变速区域内的变步长插值：沿段 [a, b]（弧长 [sA, sB]）按折线速度曲线推进。
    /// 每步步长 = 当前弧长处的渐变速度 × dt，点距随速度匀速变化（速度快处点疏、慢处点密）。
    /// 每个生成点的 Feed 写入该步渐变速度；Tool 按点弧长判断——切换点 zMid 前用旧材料、起用新材料。
    /// </summary>
    /// <param name="a">段起点（关键点）</param>
    /// <param name="b">段终点（关键点）</param>
    /// <param name="sA">段起点弧长</param>
    /// <param name="sB">段终点弧长</param>
    /// <param name="zStart/zMid/zEnd">所属变速区域起/中/终弧长</param>
    /// <param name="veloOld/vc/veloNew">旧材料/切换/新材料速度</param>
    /// <param name="toolOld/toolNew">旧/新材料 Tool 号</param>
    /// <param name="dt">采样周期(s)，步长 = 速度 × dt</param>
    /// <param name="pointsOut">输出点列表</param>
    private static void SearchPointBridge(Point3D a, Point3D b,
        double sA, double sB, double zStart, double zRampEnd, double zHoldEnd, double zEnd,
        double veloOld, double vc, double veloNew, int toolOld, int toolNew,
        double dt, List<Point3D> pointsOut)
    {
        double segArc = sB - sA;
        if (segArc <= PathEps) return;

        double s = sA;
        int safety = 100000;                       // 迭代上限，防止极小步长死循环
        while (s < sB - PathEps && safety-- > 0)
        {
            double v = SpeedAtBridge(s, zStart, zRampEnd, zHoldEnd, zEnd, veloOld, vc, veloNew);
            if (double.IsNaN(v) || double.IsInfinity(v) || v <= PathEps)
            {
                throw new ArgumentException("变速区域内速度必须大于 0。");
            }

            double step = v * dt;
            //if (step < PathEps) step = PathEps;    // 防止零步长卡死

            double sNext = s + step;
            if (sNext > sB - PathEps * 0.5) sNext = sB;   // 推近段末即归一，避免尾部碎点

            double r = segArc > PathEps ? (sNext - sA) / segArc : 1.0;
            r = Math.Max(0.0, Math.Min(1.0, r));
            // 按点弧长(sNext)判 Tool：切换点(zMid)之前为旧材料，切换点及之后为新材料
            int ptTool = toolNew; /*(sNext < zMid - PathEps) ? toolOld : toolNew;*/
            Point3D pt = new Point3D(
                a.X + (b.X - a.X) * r, a.Y + (b.Y - a.Y) * r, a.Z + (b.Z - a.Z) * r,
                b.Extrude, v, b.Pressure, ptTool, b.Layer, b.GridType, b.MaterialA);
            pointsOut.Add(pt);
            s = sNext;
        }
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

    public static List<Point3D> DirectGeneratePath(List<Point3D> points,
        double advanceDis0, double advanceDis1, double velo0, double velo1,
        double vChange0, double vChange1, double disChange0, double disChange1,
        double dt = 0.02, bool enableVeloChange = false)
    {
        if (dt <= 0) dt = 0.02; // 防御：采样周期必须为正
        //参数检查
        if (dt <= PathEps)
            throw new ArgumentException("dt 必须大于 0。");

        if (velo0 <= PathEps || velo1 <= PathEps)
            throw new ArgumentException("材料正常速度必须大于 0。");

        if (advanceDis0 < 0 || advanceDis1 < 0 ||
            disChange0 < 0 || disChange1 < 0)
            throw new ArgumentException("距离参数不能为负数。");
        // 注：T0/T1 变速速度的合法性由 SearchPointBridge 内 v<=PathEps 时抛出兜底，此处不重复校验
        var result = new List<Point3D>();
        if (points == null || points.Count == 0) return result;
        if (!enableVeloChange)
        {
            disChange0 = 0;
            disChange1 = 0;
        }
        // 各材料步长 = 速度 × dt：步长决定插值密度与密度过滤阈值
        double movestep0 = velo0 * dt; // 材料 A(T0)
        double movestep1 = velo1 * dt; // 材料 B(T1)

        // 按所打印材料选择步长：Tool 0→A 步长，Tool 1→B 步长
        double StepOfTool(int tool) => tool == 0 ? movestep0 : movestep1;

        int layerCount = points.Max(p => p.Layer) + 1;

        // 上一非空层的末点：用于层间 Z 向过渡段（喷头抬升/下降）的速度插值衔接
        Point3D? prevLast = null;

        // 将单层输出接入总结果：先补"上一层末点 → 本层首点"的 Z 向过渡段（按 step=vz×dt 补中间点、
        // Feed=vz），再追加本层点，并更新 prevLast 为本层末点供下一层衔接。首层 prevLast 为 null 不补过渡。
        // vz 取下一段（本层首点）所属材料的正常打印速度，与正常段保持一致。
        void AppendLayer(List<Point3D> layerOut)
        {
            if (layerOut.Count == 0) return;
            if (prevLast != null)
            {
                int nextTool = layerOut[0].Tool;
                double vz = (nextTool == 0) ? velo0 : velo1;   // 沿用下一段（本层首点）材料速度
                result.AddRange(InterpolateLayerTransition(prevLast, layerOut[0], vz, dt));
            }
            result.AddRange(layerOut);
            prevLast = layerOut[^1];
        }

        for (int layerIdx = 0; layerIdx < layerCount; layerIdx++)
        {
            List<Point3D> pointsInLayer = points.Where(p => p.Layer == layerIdx).ToList();
            if (pointsInLayer.Count == 0) continue;

            // 1) 应用提前距离 Tool 偏移（A/B 各自提前量）
            List<Point3D> offsetPoints = ApplyAdvanceToolOffset(pointsInLayer, advanceDis0, advanceDis1);
            if (offsetPoints.Count < 2) { AppendLayer(offsetPoints); continue; }

            int nOff = offsetPoints.Count;

            // 2a) 累计弧长
            double[] cumulOff = new double[nOff];
            cumulOff[0] = 0;
            for (int i = 1; i < nOff; i++)
                cumulOff[i] = cumulOff[i - 1] + offsetPoints[i].DistanceTo(offsetPoints[i - 1]);
            double totalOffArc = cumulOff[nOff - 1];

            // 2b) 变速区域：跨越切换点 zs 的 [zs−dc/2, zs+dc/2] 范围内速度按折线渐变
            //     前半 veloOld→vc、后半 vc→veloNew，切换点处速度=vc，两端与相邻正常段连续、全程无突变
            var sourceCumul = new double[pointsInLayer.Count];
            sourceCumul[0] = 0.0;

            for (int i = 1; i < pointsInLayer.Count; i++)
            {
                sourceCumul[i] =
                    sourceCumul[i - 1] +
                    pointsInLayer[i].DistanceTo(pointsInLayer[i - 1]);
            }
            // 预先收集所有切换边界弧长，供下方对 advance 做与 ApplyAdvanceToolOffset 一致的静默限幅
            var switchBounds = new List<double>();
            for (int i = 1; i < pointsInLayer.Count; i++)
            {
                if (pointsInLayer[i].Tool != pointsInLayer[i - 1].Tool)
                    switchBounds.Add(sourceCumul[i]);
            }
            int switchIdx = 0;
            var zones = new List<(double zStart, double zRampEnd, double zHoldEnd, double zEnd,
                                    double veloOld, double vc, double veloNew, int toolOld, int toolNew)>();
            for (int i = 1; i < pointsInLayer.Count; i++)
            {
                if (pointsInLayer[i].Tool == pointsInLayer[i - 1].Tool)
                    continue;

                int toolOld = pointsInLayer[i - 1].Tool;
                int toolNew = pointsInLayer[i].Tool;

                double boundaryS = sourceCumul[i];

                double advance = toolNew == 0
                    ? advanceDis0
                    : advanceDis1;

                // 与 ApplyAdvanceToolOffset 完全一致的实际生效提前量(静默限幅)：
                //   cap = (首个切换 ? newLen : min(oldLen,newLen))；advS=实际提前切换点；actualAdv=生效提前量
                int k = switchIdx;
                switchIdx++;                       // 无论下方是否 continue，序号都已推进
                double prevBoundary = k > 0 ? switchBounds[k - 1] : 0.0;
                double oldLen = boundaryS - prevBoundary;
                double nextBoundary = k + 1 < switchBounds.Count ? switchBounds[k + 1] : sourceCumul[^1];
                double newLen = nextBoundary - boundaryS;
                double cap = (k == 0) ? newLen : Math.Min(oldLen, newLen);
                double advS = Math.Max(prevBoundary, boundaryS - Math.Min(advance, cap));
                double actualAdv = boundaryS - advS;
                double switchS = advS;

                double requestedChange = toolNew == 0
                    ? disChange0
                    : disChange1;

                // 变速区必须位于实际提前点 advS 与原始切换点 boundaryS 之间，可用长度=actualAdv(已限幅)
                double available = actualAdv;
                double changeLength = Math.Min(requestedChange, available);

                if (changeLength <= PathEps)
                    continue;

                // 速度变化结束于原始切换点 B
                double zStart = boundaryS - changeLength;//变速开始
                double zEnd = boundaryS;//原始切换点
                //double zStart = boundaryS - changeLength;
               // double zMid = (zStart + zEnd) * 0.5;
                
               
                double rampLength = changeLength * 0.15;
                double zRampEnd = zStart + rampLength;//到达切换速度
             
                double zHoldEnd = zEnd - rampLength;//保持切换速度结束
                double veloOld = toolOld == 0 ? velo0 : velo1;
                double veloNew = toolNew == 0 ? velo0 : velo1;
                double vc = toolNew == 0 ? vChange0 : vChange1;

                zones.Add((
                    zStart,
                    zRampEnd,         
                    zHoldEnd,
                    zEnd,
                    veloOld,
                    vc,
                    veloNew,
                    toolOld,
                    toolNew));
            }
            //if (advanceDis0 > PathEps || advanceDis1 > PathEps)
            //{
            //    for (int i = 1; i < nOff; i++)
            //    {
            //        if (offsetPoints[i].Tool != offsetPoints[i - 1].Tool)
            //        {
            //            int nt = offsetPoints[i].Tool;          // 新材料
            //            int ot = offsetPoints[i - 1].Tool;      // 旧材料
            //            double vc = (nt == 0) ? vChange0 : vChange1;
            //            double dc = (nt == 0) ? disChange0 : disChange1;
            //            double veloNew = (nt == 0) ? velo0 : velo1;
            //            double veloOld = (ot == 0) ? velo0 : velo1;
            //            // 变速距离不超过该材料对应的提前出丝距离
            //            dc = Math.Min(dc, (nt == 0) ? advanceDis0 : advanceDis1);
            //            if (dc < PathEps) continue;
            //            double zs = cumulOff[i];                // 切换点 = 变速区域中点 zMid
            //            double half = dc * 0.5;
            //            double zStart = Math.Max(0.0, zs - half);
            //            double zEnd = Math.Min(totalOffArc, zs + half);
            //            zones.Add((zStart, zs, zEnd, veloOld, vc, veloNew, ot, nt));
            //        }
            //    }
            //}
            //新增

            zones = zones.OrderBy(z => z.zStart).ToList();

            for (int i = 1; i < zones.Count; i++)
            {
                if (zones[i].zStart < zones[i - 1].zEnd - PathEps)
                {
                    throw new ArgumentException(
                        "变速区域发生重叠，请减小提前距离或变速距离。");
                }
            }
            // 2c) 关键弧长位置（原始顶点 + 变速区域两端；切换点 zMid 已作为原始顶点在上方加入）
            var keyArcs = new List<(double arc, bool isToolSwitch, int toolVal)>();
            for (int i = 0; i < nOff; i++)
            {
                bool sw = (i > 0 && offsetPoints[i].Tool != offsetPoints[i - 1].Tool);
                keyArcs.Add((cumulOff[i], sw, offsetPoints[i].Tool));
            }
            foreach (var z in zones)
            {
                if (z.zStart > PathEps && z.zStart < totalOffArc - PathEps)
                {
                    // 这里已经提前切换，所以使用新 Tool
                    keyArcs.Add((z.zStart, false, z.toolNew));
                    keyArcs.Add((z.zRampEnd, false, z.toolNew));
                    keyArcs.Add((z.zHoldEnd, false, z.toolNew));
                }
                if (z.zEnd > PathEps && z.zEnd < totalOffArc - PathEps)
                {
                    // 保留原始切换点 B
                    keyArcs.Add((z.zEnd, false, z.toolNew));
                }
            }
            // 排序去重
            keyArcs.Sort((a, b) => a.arc.CompareTo(b.arc));
            var uniqueArcs = new List<(double arc, bool isToolSwitch, int toolVal)>();
            foreach (var ka in keyArcs)
            {
                if (uniqueArcs.Count == 0 || ka.arc - uniqueArcs[uniqueArcs.Count - 1].arc > PathEps * 0.5)
                    uniqueArcs.Add(ka);
                else
                {
                    var last = uniqueArcs[uniqueArcs.Count - 1];
                    uniqueArcs[uniqueArcs.Count - 1] = (last.arc, last.isToolSwitch || ka.isToolSwitch, ka.toolVal);
                }
            }

            // 2d) 关键弧长 → 关键点坐标（线性插值）
            var keyPts = new List<Point3D>();
            var keyIsSw = new List<bool>();
            foreach (var (arc, isSw, tool) in uniqueArcs)
            {
                Point3D pt;
                if (arc <= PathEps)
                    pt = ClonePoint(offsetPoints[0]);
                else if (arc >= totalOffArc - PathEps)
                    pt = ClonePoint(offsetPoints[nOff - 1]);
                else
                {
                    int seg = 1;
                    while (seg < cumulOff.Length && cumulOff[seg] < arc - PathEps) seg++;
                    if (seg >= cumulOff.Length) seg = cumulOff.Length - 1;
                    double s0 = cumulOff[seg - 1], s1 = cumulOff[seg];
                    double r = (s1 - s0 > PathEps) ? (arc - s0) / (s1 - s0) : 0;
                    r = Math.Max(0, Math.Min(1, r));
                    Point3D a = offsetPoints[seg - 1], b = offsetPoints[seg];
                    pt = new Point3D(
                        a.X + (b.X - a.X) * r, a.Y + (b.Y - a.Y) * r, a.Z + (b.Z - a.Z) * r,
                        b.Extrude, b.Feed, b.Pressure, tool, b.Layer, b.GridType, b.MaterialA);
                }
                keyPts.Add(pt);
                keyIsSw.Add(isSw);
            }

            // 2e) 重新计算关键点累计弧长
            int kN = keyPts.Count;
            double[] kCumul = new double[kN];
            kCumul[0] = 0;
            for (int i = 1; i < kN; i++)
                kCumul[i] = kCumul[i - 1] + keyPts[i].DistanceTo(keyPts[i - 1]);

            // 2f) 确定每段步长与是否在变速区域内：变速区域内实际按折线渐变步长插值（见 2g），
            //     此处 segSteps 仅作参考（变速区域用 vc*dt 标记，供 switchRegionMap 记录）
            double[] segSteps = new double[kN - 1];
            bool[] segInZone = new bool[kN - 1];
            for (int i = 0; i < kN - 1; i++)
            {
                // 段 [i,i+1] 所打印材料取段起点工具
                int segTool = keyPts[i].Tool;
                segSteps[i] = (segTool == 0) ? movestep0 : movestep1;
                segInZone[i] = false;
                double s = kCumul[i];
                foreach (var z in zones)
                {
                    if (s >= z.zStart - PathEps && s < z.zEnd - PathEps)
                    { segSteps[i] = z.vc * dt; segInZone[i] = true; break; }
                }
            }

            // 2g) 逐段插值：变速区域段（含其中的切换边界段）按折线变步长（速度匀变），其余段恒定步长
            var switchRegionMap = new List<(int startIdx, int endIdx, double switchStep)>();
            double rem = 0.0;
            var layerInterpolated = new List<Point3D>();
            int swRegStart = -1;
            double swRegStep = 0;

            for (int i = 0; i < kN - 1; i++)
            {
                bool isBoundary = keyIsSw[i + 1];
                double step = segSteps[i];
                bool inZone = segInZone[i];
                int before = layerInterpolated.Count;

                if (inZone && swRegStart < 0) { swRegStart = before; swRegStep = step; }
                if (!inZone && swRegStart >= 0)
                {
                    switchRegionMap.Add((swRegStart, before, swRegStep));
                    swRegStart = -1;
                }

                // 查找本段所属变速区域（inZone 时），取出折线参数与新旧材料
                double zStart = 0, zRampEnd = 0, zHoldEnd = 0, zEnd = 0, zVeloOld = 0, zVc = 0, zVeloNew = 0;
                int zToolOld = keyPts[i].Tool, zToolNew = keyPts[i + 1].Tool;
                bool hasZone = false;
                if (inZone)
                {
                    foreach (var z in zones)
                    {
                        if (kCumul[i] >= z.zStart - PathEps && kCumul[i] < z.zEnd - PathEps)
                        {
                            zStart = z.zStart; zRampEnd = z.zRampEnd; zHoldEnd = z.zHoldEnd; zEnd = z.zEnd;
                            zVeloOld = z.veloOld; zVc = z.vc; zVeloNew = z.veloNew;
                            zToolOld = z.toolOld; zToolNew = z.toolNew;
                            hasZone = true; break;
                        }
                    }
                }

                if (inZone && hasZone)
                {
                    // 变速区域段（含切换边界段）：折线变步长插值，veloOld→vc→veloNew 匀速渐变；
                    // Feed=渐变速度，Tool 在 SearchPointBridge 内按弧长于切换点前后切换；
                    // 切换点由变步长精确推进至段端点生成，无需额外插入 boundary
                    SearchPointBridge(keyPts[i], keyPts[i + 1], kCumul[i], kCumul[i + 1],
                        zStart, zRampEnd, zHoldEnd, zEnd, zVeloOld, zVc, zVeloNew, zToolOld, zToolNew, dt, layerInterpolated);
                    rem = 0.0;   // 变速区域独立步进，余量重置
                }
                else if (isBoundary)
                {
                    // 非变速区域的 Tool 切换边界：恒定步长插值 + 精确插入切换点（新 Tool 值）
                    SearchPoint(keyPts[i], keyPts[i + 1], step, ref rem, layerInterpolated);
                    int toolForInterp = keyPts[i].Tool;
                    for (int j = before; j < layerInterpolated.Count; j++)
                    {
                        layerInterpolated[j].Tool = toolForInterp;
                        layerInterpolated[j].Feed = (toolForInterp == 0) ? velo0 : velo1;
                    }
                    Point3D boundary = ClonePoint(keyPts[i + 1]);
                    boundary.Feed = (boundary.Tool == 0) ? velo0 : velo1;
                    if (layerInterpolated.Count > 0
                        && layerInterpolated[layerInterpolated.Count - 1].DistanceTo(boundary) < step * 0.5)
                        layerInterpolated[layerInterpolated.Count - 1] = boundary;
                    else
                        layerInterpolated.Add(boundary);
                    rem = 0.0;
                }
                else
                {
                    // 正常段：恒定步长，Feed=材料正常速度
                    SearchPoint(keyPts[i], keyPts[i + 1], step, ref rem, layerInterpolated);
                    for (int j = before; j < layerInterpolated.Count; j++)
                        layerInterpolated[j].Feed = (layerInterpolated[j].Tool == 0) ? velo0 : velo1;
                    // 变速→正常过渡处（zone end）重置余量
                    bool atZoneEnd = false;
                    foreach (var z in zones)
                    { if (Math.Abs(kCumul[i + 1] - z.zEnd) < PathEps) { atZoneEnd = true; break; } }
                    if (atZoneEnd) rem = 0.0;
                }
            }
            if (swRegStart >= 0)
                switchRegionMap.Add((swRegStart, layerInterpolated.Count, swRegStep));

            // 补上最后一个关键点
            Point3D lastKey = keyPts[kN - 1];
            if (layerInterpolated.Count == 0
                || layerInterpolated[layerInterpolated.Count - 1].DistanceTo(lastKey) > PathEps)
                layerInterpolated.Add(lastKey);

            // 2h) 分区域密度过滤
            List<Point3D> filtered;
            if (switchRegionMap.Count > 0)
            {
                int totalPts = layerInterpolated.Count;
                filtered = new List<Point3D>();
                var sortedRegions = switchRegionMap.OrderBy(r => r.startIdx).ToList();

                int segStart = 0;
                foreach (var region in sortedRegions)
                {
                    int regionStart = Math.Max(0, region.startIdx);
                    int regionEnd = Math.Min(region.endIdx, totalPts);

                    // 正常区域段 [segStart, regionStart)：按其所打印材料选择步长
                    if (regionStart > segStart)
                    {
                        var normalSeg = layerInterpolated.Skip(segStart).Take(regionStart - segStart).ToList();
                        int nTool = normalSeg.Count > 0 ? normalSeg[0].Tool : 0;
                        double ns = StepOfTool(nTool);
                        filtered.AddRange(FilterDensePoints(normalSeg, ns * 0.8, ns * 1.2));
                    }
                    // 变速区域段 [regionStart, regionEnd)：已按梯形渐变步长精确生成，
                    // 跳过密度过滤以保留速度渐变对应的渐变点密度
                    var switchSeg = layerInterpolated.Skip(regionStart).Take(regionEnd - regionStart).ToList();
                    if (switchSeg.Count > 0 && regionStart < totalPts)
                    {
                        if (switchSeg[0].DistanceTo(layerInterpolated[regionStart]) > PathEps)
                            switchSeg.Insert(0, ClonePoint(layerInterpolated[regionStart]));
                    }
                    filtered.AddRange(switchSeg);
                    segStart = regionEnd;
                }
                // 尾部正常区域
                if (totalPts > segStart)
                {
                    var tailSeg = layerInterpolated.Skip(segStart).Take(totalPts - segStart).ToList();
                    int nTool = tailSeg.Count > 0 ? tailSeg[0].Tool : 0;
                    double ns = StepOfTool(nTool);
                    filtered.AddRange(FilterDensePoints(tailSeg, ns * 0.8, ns * 1.2));
                }
            }
            else
            {
                // 无变速区域：按 Tool 连续段分组，各用其材料步长过滤
                filtered = new List<Point3D>();
                int runStart = 0;
                for (int i = 1; i <= layerInterpolated.Count; i++)
                {
                    bool breakRun = i == layerInterpolated.Count
                                 || layerInterpolated[i].Tool != layerInterpolated[runStart].Tool;
                    if (!breakRun) continue;

                    var run = layerInterpolated.Skip(runStart).Take(i - runStart).ToList();
                    double ns = StepOfTool(run.Count > 0 ? run[0].Tool : 0);
                    filtered.AddRange(FilterDensePoints(run, ns * 0.8, ns * 1.2));
                    runStart = i;
                }
            }

            AppendLayer(filtered);
        }

        // Feed 兜底：变速区域点的 Feed 已在梯形插值时写入渐变速度，正常段点已写入材料正常速度；
        //           此处仅对极少数未经过插值流程的点（如路径过短直通点）按材料正常速度补齐
        foreach (var p in result)
            if (p.Feed <= PathEps)
                p.Feed = (p.Tool == 0) ? velo0 : velo1;

        return result;
    }
}
