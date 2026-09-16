using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// 单层路径处理器：承载 <see cref="PathGenerator.DirectGeneratePath"/> 中"按层独立处理"的全部逻辑。
/// 原巨型方法被拆为 编排(<see cref="ProcessLayer"/>) + 若干职责单一的子步骤，
/// 各子步骤通过 <see cref="PathGenParams"/> 共享工艺参数，避免长参数列表与跨步骤局部变量耦合。
/// </summary>
/// <remarks>
/// 处理管线（每层）：提前偏移 → 变速区域 → 关键点 → 段步长 → 逐段插值 → 分区域密度过滤。
/// 层间 Z 向过渡不在本类(由 <see cref="PathGenerator"/> 编排层负责)。
/// </remarks>
internal static class PathLayerProcessor
{
    private const double PathEps = PathGenerator.PathEps;

    /// <summary>
    /// 单层处理编排：偏移 → zones → 关键点 → 段步长 → 插值 → 过滤，返回该层结果点集。
    /// 行为与原 DirectGeneratePath 单层循环体逐行对应。
    /// </summary>
    public static List<Point3D> ProcessLayer(List<Point3D> inLayer, PathGenParams p, AdvanceStats? stats = null)
    {
        var empty = new List<Point3D>();
        if (inLayer == null || inLayer.Count == 0) return empty;

        // 1) 应用提前距离 Tool 偏移（A/B 各自提前量）
        var transitions = AdvancePlanner.Build(inLayer, p.AdvanceDis0, p.AdvanceDis1, stats);
        List<Point3D> offsetPoints = PathGenerator.ApplyAdvanceToolOffset(inLayer, transitions);
        // 偏移后不足 2 点：直接作为本层结果(原逻辑短路)
        if (offsetPoints.Count < 2) return offsetPoints;

        int nOff = offsetPoints.Count;

        // 2a) 偏移点集累计弧长
        double[] cumulOff = ComputeCumulativeArc(offsetPoints);
        double totalOffArc = cumulOff[nOff - 1];

        // 2b) 变速区域：跨越切换点的 [zStart, zEnd] 范围内速度按折线渐变
        List<SpeedZone> zones = BuildSpeedZones(transitions, p);

        // 2c-2d) 关键弧长位置 + 关键点坐标（线性插值）
        var (keyPts, keyIsSw) = BuildKeyPoints(offsetPoints, cumulOff, totalOffArc, zones);

        // 2e) 关键点累计弧长
        double[] kCumul = ComputeCumulativeArc(keyPts);

        // 2f) 每段步长与是否落在变速区域内
        var (segSteps, segInZone) = ComputeSegSteps(keyPts, kCumul, zones, p);

        // 2g) 逐段插值（变速区域折线变步长 / 边界段 / 正常段）+ 补末点
        var (layerInterpolated, switchRegionMap) = InterpolateKeyPoints(
            keyPts, keyIsSw, kCumul, segSteps, segInZone, zones, p);

        // 2h) 分区域密度过滤
        return FilterByRegions(layerInterpolated, switchRegionMap, p);
    }

    // ====================== 公共工具 ======================

    /// <summary>累计弧长数组 cumul[0]=0，cumul[i]=cumul[i-1]+‖p_i−p_{i-1}‖（消除原三处重复）。</summary>
    private static double[] ComputeCumulativeArc(List<Point3D> pts)
    {
        double[] cumul = new double[pts.Count];
        if (pts.Count == 0) return cumul;
        cumul[0] = 0.0;
        for (int i = 1; i < pts.Count; i++)
            cumul[i] = cumul[i - 1] + pts[i].DistanceTo(pts[i - 1]);
        return cumul;
    }

    // ====================== 变速区域构建 ======================

    /// <summary>
    /// 使用与材料偏移相同的切换计划构建变速区域。
    /// 不启用变速(<see cref="PathGenParams.EnableVeloChange"/>=false)时所有 requestedChange 视为 0 → 区域为空。
    /// </summary>
    /// <exception cref="ArgumentException">变速区域相互重叠时抛出。</exception>
    private static List<SpeedZone> BuildSpeedZones(List<AdvancePlanner.Transition> transitions, PathGenParams p)
    {
        var zones = new List<SpeedZone>();
        for (int i = 0; i < transitions.Count; i++)
        {
            var transition = transitions[i];
            int toolOld = transition.OldTool;
            int toolNew = transition.NewTool;
            double advS = transition.AdvanceS;
            double actualAdv = transition.ActualAdvance;

            double requestedChange = toolNew == 0 ? p.DisChange0 : p.DisChange1;
            if (!p.EnableVeloChange) requestedChange = 0;   // 原：!enableVeloChange → disChange=0

            // 变速区必须位于实际提前点 advS 与原始切换点 boundaryS 之间，可用长度=actualAdv(已限幅)
            double available = actualAdv;
            // 提前点可跨越原边界；变速区必须在下一次实际切换前结束，避免覆盖后续材料。
            if (i + 1 < transitions.Count)
                available = Math.Min(available, transitions[i + 1].AdvanceS - advS);
            double changeLength = Math.Min(requestedChange, available);

            if (changeLength <= PathEps)
                continue;

            // 变速区位于提前区的"开始段"：锚定在实际提前点 advS，沿打印方向延伸 changeLength，
            // 即 [advS, advS+changeLength]（原先锚定在末段 boundaryS 处）。
            double zStart = advS;                    // 变速开始 = 实际提前点(提前区起点)
            double zEnd = advS + changeLength;       // 变速结束

            double rampLength = changeLength * 0.15;
            double zRampEnd = zStart + rampLength;   // 到达切换速度
            double zHoldEnd = zEnd - rampLength;     // 保持切换速度结束

            double veloOld = toolOld == 0 ? p.Velo0 : p.Velo1;
            double veloNew = toolNew == 0 ? p.Velo0 : p.Velo1;
            double vc = toolNew == 0 ? p.VChange0 : p.VChange1;

            zones.Add(new SpeedZone(zStart, zRampEnd, zHoldEnd, zEnd, veloOld, vc, veloNew, toolOld, toolNew));
        }

        zones = zones.OrderBy(z => z.ZStart).ToList();

        // 重叠检查
        for (int i = 1; i < zones.Count; i++)
        {
            if (zones[i].ZStart < zones[i - 1].ZEnd - PathEps)
            {
                throw new ArgumentException(
                    "变速区域发生重叠，请减小提前距离或变速距离。");
            }
        }
        return zones;
    }

    // ====================== 关键点 ======================

    /// <summary>
    /// 关键弧长位置(原始顶点 + 变速区域端点)经排序去重后，映射为关键点坐标(线性插值)。
    /// 返回关键点列表与其"是否为 Tool 切换边界"标记。
    /// </summary>
    private static (List<Point3D> pts, List<bool> isSw) BuildKeyPoints(
        List<Point3D> offsetPoints, double[] cumulOff, double totalOffArc, List<SpeedZone> zones)
    {
        int nOff = offsetPoints.Count;

        // 2c) 关键弧长位置（原始顶点 + 变速区域两端；切换点 zEnd 已作为原始顶点在上方加入）
        var keyArcs = new List<(double arc, bool isToolSwitch, int toolVal)>();
        for (int i = 0; i < nOff; i++)
        {
            bool sw = (i > 0 && offsetPoints[i].Tool != offsetPoints[i - 1].Tool);
            keyArcs.Add((cumulOff[i], sw, offsetPoints[i].Tool));
        }
        foreach (var z in zones)
        {
            if (z.ZStart > PathEps && z.ZStart < totalOffArc - PathEps)
            {
                // 这里已经提前切换，所以使用新 Tool
                keyArcs.Add((z.ZStart, false, z.ToolNew));
                keyArcs.Add((z.ZRampEnd, false, z.ToolNew));
                keyArcs.Add((z.ZHoldEnd, false, z.ToolNew));
            }
            if (z.ZEnd > PathEps && z.ZEnd < totalOffArc - PathEps)
            {
                // 保留原始切换点
                keyArcs.Add((z.ZEnd, false, z.ToolNew));
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
                // 相邻变速区端点可能与切换点重合，材料归属必须以真实切换为准。
                uniqueArcs[uniqueArcs.Count - 1] = (last.arc, last.isToolSwitch || ka.isToolSwitch,
                    last.isToolSwitch ? last.toolVal : ka.toolVal);
            }
        }

        // 2d) 关键弧长 → 关键点坐标（线性插值）
        var keyPts = new List<Point3D>();
        var keyIsSw = new List<bool>();
        foreach (var (arc, isSw, tool) in uniqueArcs)
        {
            Point3D pt;
            if (arc <= PathEps)
                pt = PathGenerator.ClonePoint(offsetPoints[0]);
            else if (arc >= totalOffArc - PathEps)
                pt = PathGenerator.ClonePoint(offsetPoints[nOff - 1]);
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
        return (keyPts, keyIsSw);
    }

    /// <summary>
    /// 计算每个关键点段[i,i+1]的步长与"是否落在变速区域内"标记。
    /// 落在变速区域内时段步长取 Vc×dt，否则取该段所打印材料的正常步长。
    /// </summary>
    private static (double[] steps, bool[] inZone) ComputeSegSteps(
        List<Point3D> keyPts, double[] kCumul, List<SpeedZone> zones, PathGenParams p)
    {
        int kN = keyPts.Count;
        double[] segSteps = new double[kN - 1];
        bool[] segInZone = new bool[kN - 1];
        for (int i = 0; i < kN - 1; i++)
        {
            // 段 [i,i+1] 所打印材料取段起点工具
            int segTool = keyPts[i].Tool;
            segSteps[i] = (segTool == 0) ? p.Step0 : p.Step1;
            segInZone[i] = false;
            double s = kCumul[i];
            foreach (var z in zones)
            {
                if (s >= z.ZStart - PathEps && s < z.ZEnd - PathEps)
                {
                    segSteps[i] = z.Vc * p.Dt;
                    segInZone[i] = true;
                    break;
                }
            }
        }
        return (segSteps, segInZone);
    }

    // ====================== 逐段插值 ======================

    /// <summary>
    /// 逐段插值：变速区域段(含切换边界段)按折线变步长(VeloOld→Vc→VeloNew 匀速渐变)，
    /// 非变速区域的 Tool 切换边界段按恒定步长并精确插入切换点，其余段按恒定步长。
    /// 同时记录变速区域在结果中的下标区间(switchRegionMap，供密度过滤阶段保留渐变点密度)。
    /// 末尾补上最后一个关键点。
    /// </summary>
    private static (List<Point3D> pts, List<(int startIdx, int endIdx, double switchStep)> regions)
        InterpolateKeyPoints(List<Point3D> keyPts, List<bool> keyIsSw, double[] kCumul,
            double[] segSteps, bool[] segInZone, List<SpeedZone> zones, PathGenParams p)
    {
        int kN = keyPts.Count;
        var switchRegionMap = new List<(int startIdx, int endIdx, double switchStep)>();
        double rem = 0.0;
        var first = PathGenerator.ClonePoint(keyPts[0]);
        first.Feed = first.Tool == 0 ? p.Velo0 : p.Velo1;
        var layerInterpolated = new List<Point3D> { first };
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

            // 查找本段所属变速区域(inZone 时)，取出折线参数与新旧材料
            double zStart = 0, zRampEnd = 0, zHoldEnd = 0, zEnd = 0, zVeloOld = 0, zVc = 0, zVeloNew = 0;
            int zToolOld = keyPts[i].Tool, zToolNew = keyPts[i + 1].Tool;
            bool hasZone = false;
            if (inZone)
            {
                foreach (var z in zones)
                {
                    if (kCumul[i] >= z.ZStart - PathEps && kCumul[i] < z.ZEnd - PathEps)
                    {
                        zStart = z.ZStart; zRampEnd = z.ZRampEnd; zHoldEnd = z.ZHoldEnd; zEnd = z.ZEnd;
                        zVeloOld = z.VeloOld; zVc = z.Vc; zVeloNew = z.VeloNew;
                        zToolOld = z.ToolOld; zToolNew = z.ToolNew;
                        hasZone = true; break;
                    }
                }
            }

            // 精确保留关键点（尤其是折线拐点和很短的材料段起点），不能让跨段采样抄近路。
            var segmentStart = PathGenerator.ClonePoint(keyPts[i]);
            segmentStart.Feed = inZone && hasZone
                ? SpeedAtBridge(kCumul[i], zStart, zRampEnd, zHoldEnd, zEnd, zVeloOld, zVc, zVeloNew)
                : (segmentStart.Tool == 0 ? p.Velo0 : p.Velo1);
            if (layerInterpolated.Count > 0 && layerInterpolated[^1].DistanceTo(segmentStart) <= PathEps)
                layerInterpolated[^1] = segmentStart;
            else
                layerInterpolated.Add(segmentStart);

            if (inZone && hasZone)
            {
                // 变速区域段(含切换边界段)：折线变步长插值，veloOld→vc→veloNew 匀速渐变
                SearchPointBridge(keyPts[i], keyPts[i + 1], kCumul[i], kCumul[i + 1],
                    zStart, zRampEnd, zHoldEnd, zEnd, zVeloOld, zVc, zVeloNew, zToolOld, zToolNew, p.Dt, layerInterpolated);
                rem = 0.0;   // 变速区域独立步进，余量重置
            }
            else if (isBoundary)
            {
                // 非变速区域的 Tool 切换边界：恒定步长插值 + 精确插入切换点(新 Tool 值)
                PathGenerator.SearchPoint(keyPts[i], keyPts[i + 1], step, ref rem, layerInterpolated);
                int toolForInterp = keyPts[i].Tool;
                for (int j = before; j < layerInterpolated.Count; j++)
                {
                    layerInterpolated[j].Tool = toolForInterp;
                    layerInterpolated[j].Feed = (toolForInterp == 0) ? p.Velo0 : p.Velo1;
                }
                Point3D boundary = PathGenerator.ClonePoint(keyPts[i + 1]);
                boundary.Feed = (boundary.Tool == 0) ? p.Velo0 : p.Velo1;
                if (layerInterpolated.Count > 0
                    && layerInterpolated[layerInterpolated.Count - 1].DistanceTo(boundary) <= PathEps)
                    layerInterpolated[layerInterpolated.Count - 1] = boundary;
                else
                    layerInterpolated.Add(boundary);
                rem = 0.0;
            }
            else
            {
                // 正常段：恒定步长，Feed=材料正常速度
                PathGenerator.SearchPoint(keyPts[i], keyPts[i + 1], step, ref rem, layerInterpolated);
                for (int j = before; j < layerInterpolated.Count; j++)
                {
                    layerInterpolated[j].Tool = keyPts[i].Tool;
                    layerInterpolated[j].Feed = (layerInterpolated[j].Tool == 0) ? p.Velo0 : p.Velo1;
                }
                // 变速→正常过渡处(zone end)重置余量
                bool atZoneEnd = false;
                foreach (var z in zones)
                { if (Math.Abs(kCumul[i + 1] - z.ZEnd) < PathEps) { atZoneEnd = true; break; } }
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

        return (layerInterpolated, switchRegionMap);
    }

    // ====================== 分区域密度过滤 ======================

    /// <summary>
    /// 分区域密度过滤：存在变速区域时，正常区域段按其材料步长做密度过滤，
    /// 变速区域段跳过过滤以保留梯形渐变对应的渐变点密度；无变速区域时按 Tool 连续段分组过滤。
    /// </summary>
    private static List<Point3D> FilterByRegions(
        List<Point3D> layerInterpolated, List<(int startIdx, int endIdx, double switchStep)> switchRegionMap,
        PathGenParams p)
    {
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
                    double ns = p.StepOf(nTool);
                    filtered.AddRange(PathGenerator.FilterDensePoints(normalSeg, ns * 0.8, ns * 1.2));
                }
                // 变速区域段 [regionStart, regionEnd)：已按梯形渐变步长精确生成，
                // 跳过密度过滤以保留速度渐变对应的渐变点密度
                var switchSeg = layerInterpolated.Skip(regionStart).Take(regionEnd - regionStart).ToList();
                if (switchSeg.Count > 0 && regionStart < totalPts)
                {
                    if (switchSeg[0].DistanceTo(layerInterpolated[regionStart]) > PathEps)
                        switchSeg.Insert(0, PathGenerator.ClonePoint(layerInterpolated[regionStart]));
                }
                filtered.AddRange(switchSeg);
                segStart = regionEnd;
            }
            // 尾部正常区域
            if (totalPts > segStart)
            {
                var tailSeg = layerInterpolated.Skip(segStart).Take(totalPts - segStart).ToList();
                int nTool = tailSeg.Count > 0 ? tailSeg[0].Tool : 0;
                double ns = p.StepOf(nTool);
                filtered.AddRange(PathGenerator.FilterDensePoints(tailSeg, ns * 0.8, ns * 1.2));
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
                double ns = p.StepOf(run.Count > 0 ? run[0].Tool : 0);
                filtered.AddRange(PathGenerator.FilterDensePoints(run, ns * 0.8, ns * 1.2));
                runStart = i;
            }
        }
        return filtered;
    }

    // ====================== 变速区域速度与插值(自 PathGenerator 搬入) ======================

    /// <summary>
    /// 跨越切换点的变速区域 [zStart, zEnd] 内弧长 s 处的折线渐变速度(匀速变化、全程无突变)。
    /// [zStart, zRampEnd]：VeloOld 线性渐变到 Vc；[zRampEnd, zHoldEnd]：保持 Vc；
    /// [zHoldEnd, zEnd]：Vc 线性渐变到 VeloNew。切换点处速度恰好为 Vc，与两端正常段连续。
    /// 渐变速率由变速距离与速度差共同决定——变速距离越大变化越平缓。
    /// </summary>
    private static double SpeedAtBridge(double s, double zStart, double zRampEnd, double zHoldEnd, double zEnd,
        double veloOld, double vc, double veloNew)
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
    /// 跨越切换点的变速区域内的变步长插值：沿段 [a, b](弧长 [sA, sB])按折线速度曲线推进。
    /// 每步步长 = 当前弧长处的渐变速度 × dt，点距随速度匀速变化(速度快处点疏、慢处点密)。
    /// 每个生成点的 Feed 写入该步渐变速度；Tool 取新材料。
    /// </summary>
    /// <exception cref="ArgumentException">变速区域内速度非正时抛出。</exception>
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

            double sNext = s + step;
            if (sNext > sB - PathEps * 0.5) sNext = sB;   // 推近段末即归一，避免尾部碎点

            double r = segArc > PathEps ? (sNext - sA) / segArc : 1.0;
            r = Math.Max(0.0, Math.Min(1.0, r));
            // 按点弧长(sNext)判 Tool：取新材料
            int ptTool = sNext >= sB - PathEps ? b.Tool : toolNew;
            Point3D pt = new Point3D(
                a.X + (b.X - a.X) * r, a.Y + (b.Y - a.Y) * r, a.Z + (b.Z - a.Z) * r,
                b.Extrude, v, b.Pressure, ptTool, b.Layer, b.GridType, b.MaterialA);
            pointsOut.Add(pt);
            s = sNext;
        }
    }
}
