using System.Globalization;
using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// 体素映射器：自 AMCP.FrmPrintStep2 的体素相关流程移植。
/// 含：体素 CSV 文件解析(ParseVoxelFile)、体素映射(AssignTValues，按 DDA 光线投射给路径点赋材料 Tool 值)。
/// 另提供三种"对比映射方法"用于算法对比：
///   ① 最近邻 AssignTValuesNearest —— 逐点取最近体素单元中心属性；
///   ② 双线性 AssignTValuesBilinear —— 周围 4 体素单元按距离加权插值后二值化；
///   ③ SDF    AssignTValuesSdf     —— 以 STL 三角网格为实体边界做体内/体外判定(体内=1/体外=0)，
///      配套 ParseStlFile 导入 STL 模型。四种方法输出同构 List&lt;Point3D&gt;，便于横向对比精度。
/// </summary>
public static class VoxelMapper
{
    private const double EPS = 1e-6;

    /// <summary>体素数据：多层 RLE 矩阵 + 网格元数据。</summary>
    public class VoxelData
    {
        /// <summary>[层][Y行][RLE段] 的体素矩阵（depth 0/1 表示两种材料）。</summary>
        public List<List<List<Pixcel>>> Matrix = new();
        public double OriginX, OriginY, OriginZ;
        public double StepX, StepY, StepZ;
        public int ColCount;   // X 方向体素列数
        public int RowCount;   // Y 方向体素行数
        public string SourcePath = "";
    }

    // ====================== 体素文件解析 ======================

    /// <summary>
    /// 解析体素 CSV 文件（含 # 注释行 + "X Y Z Value" 标题行），按 Z 分层、按 Y 分行做 RLE 编码。
    /// 自 FrmPrintStep2.ParseVoxelFile 移植。
    /// </summary>
    public static VoxelData ParseVoxelFile(string filePath)
    {
        var data = new VoxelData { SourcePath = filePath };
        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0) return data;

        // 1. 跳过 # 注释行，找到标题行，数据从下一行开始
        int dataStart = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            dataStart = i + 1;
            break;
        }
        if (dataStart == -1 || dataStart >= lines.Length) return data;

        // 2. 收集坐标和值
        var rawData = new Dictionary<double, List<(double X, double Y, int Value)>>();
        var xSet = new HashSet<double>();
        var ySet = new HashSet<double>();
        var zSet = new HashSet<double>();

        for (int i = dataStart; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var parts = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out double x) ||
                !double.TryParse(parts[1], System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out double y) ||
                !double.TryParse(parts[2], System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out double z) ||
                !int.TryParse(parts[3], out int val))
                continue;

            xSet.Add(x); ySet.Add(y); zSet.Add(z);
            if (!rawData.ContainsKey(z)) rawData[z] = new List<(double, double, int)>();
            rawData[z].Add((x, y, val));
        }

        // 3. 排序坐标轴
        var xCoords = xSet.OrderBy(v => v).ToList();
        var yCoords = ySet.OrderBy(v => v).ToList();
        var zCoords = zSet.OrderBy(v => v).ToList();
        int xCount = xCoords.Count, yCount = yCoords.Count;

        // 4. 构建 Z 层 RLE 矩阵
        foreach (double z in zCoords)
        {
            var layerRows = new List<List<Pixcel>>();
            for (int yi = 0; yi < yCount; yi++)
            {
                var rowSequence = new List<int>();
                for (int xi = 0; xi < xCount; xi++)
                {
                    int val = 0;
                    if (rawData.TryGetValue(z, out var points))
                    {
                        double tx = xCoords[xi], ty = yCoords[yi];
                        var pt = points.FirstOrDefault(p => p.X == tx && p.Y == ty);
                        val = pt.Value;
                    }
                    rowSequence.Add(val);
                }
                layerRows.Add(EncodeLayerRLE(rowSequence));
            }
            data.Matrix.Add(layerRows);
        }

        // 体素坐标元数据
        data.StepX = xCoords.Count >= 2 ? xCoords[1] - xCoords[0] : 1.0;
        data.OriginX = xCoords.Count >= 2 ? xCoords[0] - data.StepX * 0.5 : xCoords[0] - 0.5;
        data.StepY = yCoords.Count >= 2 ? yCoords[1] - yCoords[0] : 1.0;
        data.OriginY = yCoords.Count >= 2 ? yCoords[0] - data.StepY * 0.5 : yCoords[0] - 0.5;
        data.StepZ = zCoords.Count >= 2 ? zCoords[1] - zCoords[0] : 1.0;
        data.OriginZ = zCoords.Count >= 2 ? zCoords[0] - data.StepZ * 0.5 : zCoords[0] - 0.5;
        data.ColCount = xCount;
        data.RowCount = yCount;
        return data;
    }

    /// <summary>一维 0/1 序列 RLE 编码（与 ProcessImage 风格一致）。depth=0 表示 Value=0，depth=1 表示 Value=1。</summary>
    private static List<Pixcel> EncodeLayerRLE(List<int> sequence)
    {
        var pixels = new List<Pixcel>();
        if (sequence == null || sequence.Count == 0) return pixels;

        int counts = 0;
        bool isZero = false, isOne = false;

        for (int i = 0; i < sequence.Count; i++)
        {
            int val = sequence[i];
            if (val == 0)
            {
                if (isOne) { pixels.Add(new Pixcel(1, counts)); counts = 0; }
                if (!isZero) { isZero = true; isOne = false; }
                counts++;
            }
            else
            {
                if (isZero) { pixels.Add(new Pixcel(0, counts)); counts = 0; }
                if (!isOne) { isOne = true; isZero = false; }
                counts++;
            }
        }
        pixels.Add(new Pixcel(sequence[^1] == 0 ? 0 : 1, counts));
        return pixels;
    }

    // ====================== 体素映射（路径点赋材料 Tool 值）======================

    /// <summary>
    /// 按体素数据给原始路径点映射材料值(Tool)：用 DDA 光线投射遍历段间经过的体素单元，
    /// 在材料边界处插入过渡点。自 FrmPrintStep2.AssignTValuesFromVoxelFile 移植。
    /// </summary>
    public static List<Point3D> AssignTValues(List<Point3D> originalPoints, VoxelData? voxel, bool insertTransitionalPoints , out int switchesBefore, out int switchesAfter)
    {
        switchesBefore = 0;
        switchesAfter = 0;
        var assigned = new List<Point3D>();
        if (originalPoints == null || originalPoints.Count == 0
            || voxel == null || voxel.Matrix.Count == 0)
            return assigned;

        // 1. 获取原始路径的外接矩形(XY + Z)
        double xMin = originalPoints.Min(p => p.X);
        double xMax = originalPoints.Max(p => p.X);
        double yMin = originalPoints.Min(p => p.Y);
        double yMax = originalPoints.Max(p => p.Y);
        double zMin = originalPoints.Min(p => p.Z);
        double zMax = originalPoints.Max(p => p.Z);

        // 2. 构建体素映射框架 — 将体素网格各轴独立拉伸至覆盖外接矩形(XY 平面 + Z 层)
        int voxelLayerCount = voxel.Matrix.Count;

        // 从体素数据获取行列数
        int vRows = voxel.RowCount > 0 ? voxel.RowCount : voxel.Matrix[0].Count;
        int vCols = voxel.ColCount > 0 ? voxel.ColCount : GetVoxelColumnCount(voxel.Matrix[0]);

        // 计算单元格尺寸，使体素网格恰好覆盖外接矩形(XY 列/行 + Z 层)
        double cellSizeX = vCols > 0 ? (xMax - xMin) / vCols : 1.0;
        double cellSizeY = vRows > 0 ? (yMax - yMin) / vRows : 1.0;
        double cellSizeZ = voxelLayerCount > 0 ? (zMax - zMin) / voxelLayerCount : 1.0;
        if (cellSizeX <= EPS) cellSizeX = 1.0;
        if (cellSizeY <= EPS) cellSizeY = 1.0;
        if (cellSizeZ <= EPS) cellSizeZ = 1.0;

        var frame = new VoxelMapFrame
        {
            XMin = xMin,
            XMax = xMax,
            YMin = yMin,
            YMax = yMax,
            CellSizeX = cellSizeX,
            CellSizeY = cellSizeY,
            ColCount = vCols,
            RowCount = vRows,
            ZMin = zMin,
            ZMax = zMax,
            CellSizeZ = cellSizeZ,
            LayerCount = voxelLayerCount
        };

        // 3. 逐段映射，体素边界插入过渡点
        var vertexDepths = new List<int>(originalPoints.Count);
        for (int i = 0; i < originalPoints.Count; i++)
        {
            var p = originalPoints[i];
            int layerIdx = GetVoxelLayerByZ(p.Z, frame);
            var voxelLayer = voxel.Matrix[layerIdx];
            int d = GetVoxelDepthAtPoint(p.X, p.Y, voxelLayer, frame);
            vertexDepths.Add(d);
        }
        // 计算插点前（仅顶点）的切换点数：相邻顶点 tool 值不同的次数
        for (int i = 0; i < vertexDepths.Count - 1; i++)
            if (vertexDepths[i] != vertexDepths[i + 1]) switchesBefore++;

        for (int i = 0; i < originalPoints.Count; i++)
        {
            var cur = originalPoints[i];
            int voxelLayerIdx = GetVoxelLayerByZ(cur.Z, frame);   // 按路径点 Z 落入拉伸后的体素层
            var voxelLayer = voxel.Matrix[voxelLayerIdx];

            int curDepth = GetVoxelDepthAtPoint(cur.X, cur.Y, voxelLayer, frame);
            var mappedCur = ClonePoint(cur);
            mappedCur.Tool = curDepth;
            AddNoDuplicate(assigned, mappedCur);

            if (i < originalPoints.Count - 1)
            {
                var next = originalPoints[i + 1];
                // 仅当两点落入同一体素层时做段内 DDA 投射(原按打印层号 Layer 判定，
                // 现按 Z 定层；跨体素层段的材料切换由顶点层变化体现)
                insertTransitionalPoints = true;
                if (GetVoxelLayerByZ(cur.Z, frame) == GetVoxelLayerByZ(next.Z, frame) && insertTransitionalPoints)
                {
                    var visits = CastRayDDA(cur, next, frame);
                    int prevDepth = curDepth;

                    // 过渡点沿段前进方向的微移量：令其明确落入"刚进入的新单元"内部(见循环内说明)
                    double segDx = next.X - cur.X, segDy = next.Y - cur.Y;
                    double segLen = Math.Sqrt(segDx * segDx + segDy * segDy);
                    double nudgeX = 0, nudgeY = 0;
                    if (segLen > EPS)
                    {
                        double eps = Math.Min(frame.CellSizeX, frame.CellSizeY) * 1e-6;
                        nudgeX = segDx / segLen * eps;
                        nudgeY = segDy / segLen * eps;
                    }

                    for (int v = 1; v < visits.Count; v++)
                    {
                        double tEntry = visits[v].tEntry;
                        // 终端访问(t≈1)跳过：终点材料由下一轮 next 顶点的 floor 深度决定，
                        // 避免端点位于网格线时插入与 next 冲突的过渡点。visits 按 t 升序，触及终端即 break。
                        if (tEntry > 1.0 - 1e-9) break;

                        int row = visits[v].row, col = visits[v].col;
                        if (row < 0 || row >= frame.RowCount || col < 0 || col >= frame.ColCount) continue;

                        var insertPt = InterpolatePoint(cur, next, tEntry);
                        // 【关键修正】过渡点恰落在网格线上，其 floor 边界归属与"光线穿越进入的新单元"在
                        // 反向移动(向左/向下)时差一个单元——单元 k 占半开区间 [XMin+k·cs, XMin+(k+1)·cs)，
                        // 反向穿越时过渡点 x=XMin+(k+1)·cs 被 floor 归到刚离开的旧单元 k+1，而光线进入的是 k。
                        // 若直接取 visit 单元深度作 Tool，会与评估端 floor 分类不一致，系统性地使反向过渡点
                        // Tool 错位，拉低节点/切换点正确率。此处沿段前进方向微移极小量，令坐标明确进入新单元
                        // 内部，再用与评估同源的 floor 分类取深度，保证 过渡点Tool==评估d_true==光线进入单元 三者一致。
                        insertPt.X += nudgeX;
                        insertPt.Y += nudgeY;
                        int depth = GetVoxelDepthAtPoint(insertPt.X, insertPt.Y, voxelLayer, frame);
                        if (depth != prevDepth)
                        {
                            insertPt.Tool = depth;
                            AddNoDuplicate(assigned, insertPt);
                            prevDepth = depth;
                        }
                    }
                }
            }
        }
        // 计算插点后（assigned 列表）的切换点数
        for (int i = 0; i < assigned.Count - 1; i++)
            if (assigned[i].Tool != assigned[i + 1].Tool) switchesAfter++;
        return assigned;
    }

    // ====================== 对比映射方法（最近邻 / 双线性 / SDF）======================
    // 以下三种方法与 AssignTValues(DDA) 并列，用于体素映射算法横向对比。四者结构一致：
    // 逐顶点赋 Tool 后，沿每条路径段密集采样、在材料翻转处插入过渡点(供 SimplifyPath 保留为材料边界)。
    //   · 最近邻、双线性 —— 基于 VoxelData(体素 CSV)，复用与 AssignTValues 一致的 frame 与按 Z 定层(GetVoxelLayerByZ)；
    //   · SDF            —— 基于连续 STL 三角网格(地面真值)，体内=1/体外=0，按路径尺度定步长(无体素栅格)。

    /// <summary>映射上下文：体素 frame(含 Z 维度)。体素层由 GetVoxelLayerByZ(p.Z, Frame) 按路径点 Z 定。</summary>
    private sealed class MapContext
    {
        public VoxelMapFrame Frame;
    }

    /// <summary>
    /// 构建映射上下文：与 <see cref="AssignTValues"/> 完全一致的 frame(路径外接矩形各轴独立拉伸覆盖体素网格)。
    /// 最近邻/双线性复用之，体素层由 <see cref="GetVoxelLayerByZ"/> 按路径点 Z 定，确保对比基准统一。输入非法时返回 null。
    /// <para>此 frame 即体素侧的<b>尺寸等效</b>：把 vCols×vRows×LayerCount 体素栅格各轴独立拉伸到路径点最小外接矩形，
    /// 与 SDF 把 STL 放缩到路径外接矩形(AssignTValuesSdf)同一基准，保证四种方法横向可比。</para>
    /// </summary>
    private static MapContext? BuildMapContext(List<Point3D>? originalPoints, VoxelData? voxel)
    {
        if (originalPoints == null || originalPoints.Count == 0
            || voxel == null || voxel.Matrix.Count == 0)
            return null;

        // 1. 路径外接矩形(XY + Z)
        double xMin = originalPoints.Min(p => p.X);
        double xMax = originalPoints.Max(p => p.X);
        double yMin = originalPoints.Min(p => p.Y);
        double yMax = originalPoints.Max(p => p.Y);
        double zMin = originalPoints.Min(p => p.Z);
        double zMax = originalPoints.Max(p => p.Z);

        // 2. 体素网格行列数 + 单元尺寸（各轴独立拉伸恰好覆盖外接矩形，含 Z 层）
        int voxelLayerCount = voxel.Matrix.Count;
        int vRows = voxel.RowCount > 0 ? voxel.RowCount : voxel.Matrix[0].Count;
        int vCols = voxel.ColCount > 0 ? voxel.ColCount : GetVoxelColumnCount(voxel.Matrix[0]);
        double cellSizeX = vCols > 0 ? (xMax - xMin) / vCols : 1.0;
        double cellSizeY = vRows > 0 ? (yMax - yMin) / vRows : 1.0;
        double cellSizeZ = voxelLayerCount > 0 ? (zMax - zMin) / voxelLayerCount : 1.0;
        if (cellSizeX <= EPS) cellSizeX = 1.0;
        if (cellSizeY <= EPS) cellSizeY = 1.0;
        if (cellSizeZ <= EPS) cellSizeZ = 1.0;

        var frame = new VoxelMapFrame
        {
            XMin = xMin, XMax = xMax, YMin = yMin, YMax = yMax,
            CellSizeX = cellSizeX, CellSizeY = cellSizeY,
            ColCount = vCols, RowCount = vRows,
            ZMin = zMin, ZMax = zMax, CellSizeZ = cellSizeZ, LayerCount = voxelLayerCount,
        };

        return new MapContext { Frame = frame };
    }

    // ---------- ① 最近邻映射 ----------

    /// <summary>
    /// 最近邻映射：对每个路径点取距离最近的体素单元中心的深度作为 Tool，并<b>沿每条路径段密集采样、
    /// 在材料翻转处插入过渡点</b>(与 DDA 同结构)。这样不会因仅在路径顶点采样而丢失段内体素材料信息——
    /// 经 SimplifyPath(按 Tool 分段 RDP)后，所有材料边界均被保留，与 DDA 输出同等完整。
    /// 注：规则栅格上"最近单元中心"等价于"包含该点的单元"，故本方法结果与 DDA 高度一致，可作一致性基线。
    /// </summary>
    public static List<Point3D> AssignTValuesNearest(List<Point3D>? originalPoints, VoxelData? voxel)
    {
        var assigned = new List<Point3D>();
        var ctx = BuildMapContext(originalPoints, voxel);
        if (ctx == null) return assigned;
        var frame = ctx.Frame;

        for (int i = 0; i < originalPoints!.Count; i++)
        {
            var cur = originalPoints[i];
            int voxelLayerIdx = GetVoxelLayerByZ(cur.Z, ctx.Frame);   // 按路径点 Z 定体素层
            var voxelLayer = voxel!.Matrix[voxelLayerIdx];

            // 1. 当前顶点：取最近体素单元深度
            int depth = GetNearestVoxelDepthAtPoint(cur.X, cur.Y, voxelLayer, frame);
            var mappedCur = ClonePoint(cur);
            mappedCur.Tool = depth;
            AddNoDuplicate(assigned, mappedCur);

            // 2. 段内：沿段密集采样，在材料翻转处插入过渡点(捕获段内体素材料信息)
            if (i < originalPoints.Count - 1)
            {
                var next = originalPoints[i + 1];
                // 仅当两点落入同一体素层时做段内采样(原按打印层号 Layer 判定，现按 Z 定层)
                if (GetVoxelLayerByZ(cur.Z, ctx.Frame) == GetVoxelLayerByZ(next.Z, ctx.Frame))
                {
                    WalkSegmentForTransitions(cur, next, frame,
                        (x, y) => (double)GetNearestVoxelDepthAtPoint(x, y, voxelLayer, frame),
                        assigned, depth, null);
                }
            }
        }
        return assigned;
    }

    /// <summary>最近邻：将路径点落到最近体素单元中心(减 0.5 后四舍五入)，再取该单元深度。</summary>
    private static int GetNearestVoxelDepthAtPoint(double x, double y, List<List<Pixcel>> voxelLayer, VoxelMapFrame frame)
    {
        if (frame.ColCount <= 0 || frame.RowCount <= 0
            || frame.CellSizeX <= EPS || frame.CellSizeY <= EPS) return 0;

        // 体素单元中心位于 (XMin+(col+0.5)·CellSizeX, YMin+(row+0.5)·CellSizeY)
        // 故 (x-XMin)/CellSizeX - 0.5 的整数即"最近中心"列号；四舍五入实现最近邻
        double fc = (x - frame.XMin) / frame.CellSizeX - 0.5;
        double fr = (y - frame.YMin) / frame.CellSizeY - 0.5;
        int col = (int)Math.Round(fc);
        int row = (int)Math.Round(fr);
        col = ClampInt(col, 0, frame.ColCount - 1);
        row = ClampInt(row, 0, frame.RowCount - 1);
        return LookupRLEDepth(voxelLayer, row, col);
    }

    // ---------- ② 双线性插值映射 ----------

    /// <summary>
    /// 双线性插值映射：对每个路径点取其周围 4 个体素单元深度按双线性距离加权得到 [0,1] 连续场，以 0.5 阈值二值化为 Tool；
    /// 并<b>沿每条路径段密集采样、在阈值穿越处插入过渡点</b>(与 DDA 同结构)，避免仅在顶点采样而丢失段内材料切换。
    /// 与 DDA/最近邻的"阶梯式"边界不同，双线性的材料边界出现在连续场穿越 0.5 处(可在单元内部)，边界更平滑。
    /// 越界单元采用边缘夹紧(clamp)，保证边界点仍可插值。
    /// </summary>
    /// <param name="interpolatedValues">可选：输出每个"实际写入"点(顶点+过渡点)处的连续插值值，与返回列表一一对应；过渡点处≈0.5。传 null 则不输出。</param>
    public static List<Point3D> AssignTValuesBilinear(List<Point3D>? originalPoints, VoxelData? voxel,
        List<double>? interpolatedValues = null)
    {
        var assigned = new List<Point3D>();
        interpolatedValues?.Clear();
        var ctx = BuildMapContext(originalPoints, voxel);
        if (ctx == null) return assigned;
        var frame = ctx.Frame;

        for (int i = 0; i < originalPoints!.Count; i++)
        {
            var cur = originalPoints[i];
            int voxelLayerIdx = GetVoxelLayerByZ(cur.Z, ctx.Frame);   // 按路径点 Z 定体素层
            var voxelLayer = voxel!.Matrix[voxelLayerIdx];

            // 1. 当前顶点：双线性插值连续值 → 0.5 阈值二值化
            double v = BilinearDepthAtPoint(cur.X, cur.Y, voxelLayer, frame);
            int depth = v >= 0.5 ? 1 : 0;
            var mappedCur = ClonePoint(cur);
            mappedCur.Tool = depth;
            // 仅在点真正写入时同步连续值，保证与 assigned 一一对齐
            if (AddNoDuplicate(assigned, mappedCur) && interpolatedValues != null)
                interpolatedValues.Add(v);

            // 2. 段内：沿段密集采样，在阈值穿越处插入过渡点(捕获段内材料切换)
            if (i < originalPoints.Count - 1)
            {
                var next = originalPoints[i + 1];
                // 仅当两点落入同一体素层时做段内采样(原按打印层号 Layer 判定，现按 Z 定层)
                if (GetVoxelLayerByZ(cur.Z, ctx.Frame) == GetVoxelLayerByZ(next.Z, ctx.Frame))
                {
                    WalkSegmentForTransitions(cur, next, frame,
                        (x, y) => BilinearDepthAtPoint(x, y, voxelLayer, frame),
                        assigned, depth, interpolatedValues);
                }
            }
        }
        return assigned;
    }

    /// <summary>双线性插值：在 4 个相邻体素单元中心间按距离加权，返回 [0,1] 连续深度；越界单元边缘夹紧。</summary>
    private static double BilinearDepthAtPoint(double x, double y, List<List<Pixcel>> voxelLayer, VoxelMapFrame frame)
    {
        if (frame.ColCount <= 0 || frame.RowCount <= 0
            || frame.CellSizeX <= EPS || frame.CellSizeY <= EPS) return 0;

        // 以体素单元中心为采样节点：fc/fr 整数 = 单元列/行号
        double fc = (x - frame.XMin) / frame.CellSizeX - 0.5;
        double fr = (y - frame.YMin) / frame.CellSizeY - 0.5;
        int col0 = (int)Math.Floor(fc), row0 = (int)Math.Floor(fr);
        double tx = fc - col0, ty = fr - row0;
        int col1 = col0 + 1, row1 = row0 + 1;

        // 边缘夹紧：路径点落在网格外时退化为最近的边缘单元
        col0 = ClampInt(col0, 0, frame.ColCount - 1);
        col1 = ClampInt(col1, 0, frame.ColCount - 1);
        row0 = ClampInt(row0, 0, frame.RowCount - 1);
        row1 = ClampInt(row1, 0, frame.RowCount - 1);

        double v00 = LookupRLEDepth(voxelLayer, row0, col0);
        double v10 = LookupRLEDepth(voxelLayer, row0, col1);
        double v01 = LookupRLEDepth(voxelLayer, row1, col0);
        double v11 = LookupRLEDepth(voxelLayer, row1, col1);

        double vTop = v00 * (1.0 - tx) + v10 * tx;     // 上边两点插值
        double vBot = v01 * (1.0 - tx) + v11 * tx;     // 下边两点插值
        return vTop * (1.0 - ty) + vBot * ty;          // 上下插值
    }

    /// <summary>整型夹紧到 [lo,hi]。</summary>
    private static int ClampInt(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    /// <summary>
    /// 沿线段(cur→next)按"最小体素单元尺寸的一半"细步长采样连续值场 <paramref name="valueAt"/>，在材料(Tool)翻转处
    /// 插入过渡点(经二分细化定位翻转位置)，追加到 <paramref name="assigned"/>。供最近邻/双线性复用——
    /// 二者若仅顶点采样会丢失段内材料切换；沿段密集采样+插过渡点后与 DDA 一样捕获完整体素材料信息，
    /// 供 SimplizePath 保留为材料边界。Tool 由连续值 0.5 阈值二值化得到。
    /// </summary>
    /// <param name="valueAt">连续值场 [0,1](最近邻即 0/1)。</param>
    /// <param name="prevTool">段起点已输出的 Tool，用于判定段内首次翻转。</param>
    /// <param name="contValues">可选：仅在过渡点真正写入时追加其连续值(≈0.5)，与 assigned 末尾一一对齐。</param>
    private static void WalkSegmentForTransitions(Point3D cur, Point3D next, VoxelMapFrame frame,
        Func<double, double, double> valueAt, List<Point3D> assigned, int prevTool,
        List<double>? contValues)
    {
        double dx = next.X - cur.X, dy = next.Y - cur.Y;
        double segLen = Math.Sqrt(dx * dx + dy * dy);
        if (segLen <= EPS) return;

        // 细步长 = 最小体素单元尺寸的一半：保证每单元内 ≥2 个采样点，不漏材料边界
        double step = Math.Min(frame.CellSizeX, frame.CellSizeY) * 0.5;
        if (step <= EPS) return;
        int n = (int)Math.Ceiling(segLen / step);
        const int MAX_SUB = 20000;   // 单段采样上限，防止超长段耗时过长(打印路径段较短，极少触发)
        if (n > MAX_SUB) n = MAX_SUB;

        // 连续值 → Tool(0.5 阈值)；ToolAtT 为参数 t 处的 Tool，供密集采样与二分细化共用
        int ToolAt(double x, double y) => valueAt(x, y) >= 0.5 ? 1 : 0;
        int ToolAtT(double t) => ToolAt(cur.X + dx * t, cur.Y + dy * t);

        // 仅采样段内点 k=1..n-1；段终点(t=1)由外层下一顶点负责输出，避免重复
        for (int k = 1; k < n; k++)
        {
            double t = (double)k / n;
            int depth = ToolAtT(t);
            if (depth == prevTool) continue;   // 未翻转，继续

            // 翻转：在上一采样 tPrev(旧材料) 与 t(新材料) 之间二分定位首次穿越位置
            double tPrev = (double)(k - 1) / n;
            double tCross = RefineTransitionCrossing(ToolAtT, tPrev, t, prevTool);
            var pt = InterpolatePoint(cur, next, tCross);
            pt.Tool = depth;
            if (AddNoDuplicate(assigned, pt) && contValues != null)
                contValues.Add(valueAt(pt.X, pt.Y));   // 过渡点处连续值≈0.5
            prevTool = depth;
        }
    }

    /// <summary>二分定位材料翻转参数 t：tLo 处 Tool=<paramref name="toolLo"/>，tHi 处 Tool≠toolLo，返回首次进入新材料的 t(精度 (tHi-tLo)/2^14)。
    /// <paramref name="toolAtT"/> 为"参数 t 处的 Tool"(2D/3D 通用，由调用方闭包封装坐标插值)，供密集采样与二分细化共用。</summary>
    private static double RefineTransitionCrossing(Func<double, int> toolAtT, double tLo, double tHi, int toolLo)
    {
        const int ITER = 14;
        double lo = tLo, hi = tHi;   // 不变式：lo 处=toolLo，hi 处≠toolLo
        for (int i = 0; i < ITER; i++)
        {
            double mid = (lo + hi) * 0.5;
            if (toolAtT(mid) == toolLo) lo = mid;   // 仍为旧材料 → 边界在右侧
            else hi = mid;                           // 已进入新材料 → 边界在左侧
        }
        return hi;   // 首次进入新材料的保守 t
    }

    /// <summary>
    /// 沿 3D 线段(cur→next)按 <paramref name="step"/> 细步长采样"体内(1)/体外(0)"场 <paramref name="toolAt3D"/>，
    /// 在材料(Tool)翻转处插入过渡点(经二分细化定位穿越位置)，追加到 <paramref name="assigned"/>。
    /// 与 2D 版 <see cref="WalkSegmentForTransitions"/> 同构，区别仅在于采样与插值均含 Z(SDF 为 3D 实体判定)，
    /// 步长由调用方按路径尺度给出(SDF 无体素栅格)。供 <see cref="AssignTValuesSdf"/> 复用——
    /// 若仅在路径顶点采样会丢失段内 STL 表面穿越；沿段密集采样+插过渡点后与其它三种方法一样捕获完整材料边界。
    /// </summary>
    /// <param name="step">段内采样步长(路径坐标，由调用方按路径外接矩形尺度给出)。</param>
    /// <param name="toolAt3D">3D 体内/体外场(体内=1/体外=0)。</param>
    /// <param name="prevTool">段起点已输出的 Tool，用于判定段内首次翻转。</param>
    /// <param name="signedDistances">可选：仅在过渡点真正写入时追加其有符号距离(≈0)，与 assigned 末尾一一对齐。</param>
    /// <param name="signedDistAt3D">3D 有符号距离场(体内正/体外负)，仅供可选输出。</param>
    private static void WalkSdfSegmentForTransitions(Point3D cur, Point3D next, double step,
        Func<double, double, double, int> toolAt3D, List<Point3D> assigned, int prevTool,
        List<double>? signedDistances, Func<double, double, double, double> signedDistAt3D)
    {
        double dx = next.X - cur.X, dy = next.Y - cur.Y, dz = next.Z - cur.Z;
        double segLen = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (segLen <= EPS || step <= EPS) return;

        int n = (int)Math.Ceiling(segLen / step);
        const int MAX_SUB = 8000;   // 单段采样上限(SDF 每次采样为 O(三角面片)，故较 2D 版更小)
        if (n > MAX_SUB) n = MAX_SUB;

        // 参数 t 处的 Tool(局部函数，供密集采样与二分细化共用；含 Z 插值)
        int ToolAtT(double t) => toolAt3D(cur.X + dx * t, cur.Y + dy * t, cur.Z + dz * t);

        // 仅采样段内点 k=1..n-1；段终点(t=1)由外层下一顶点负责输出，避免重复
        for (int k = 1; k < n; k++)
        {
            double t = (double)k / n;
            int depth = ToolAtT(t);
            if (depth == prevTool) continue;   // 未翻转，继续

            // 翻转：在上一采样 tPrev(旧材料) 与 t(新材料) 之间二分定位首次穿越位置
            double tPrev = (double)(k - 1) / n;
            double tCross = RefineTransitionCrossing(ToolAtT, tPrev, t, prevTool);
            var pt = InterpolatePoint(cur, next, tCross);
            pt.Tool = depth;
            if (AddNoDuplicate(assigned, pt) && signedDistances != null)
                signedDistances.Add(signedDistAt3D(pt.X, pt.Y, pt.Z));   // 过渡点处有符号距离≈0
            prevTool = depth;
        }
    }

    // ---------- ③ SDF 映射（STL 实体内外判定）----------

    /// <summary>STL 三角面片：法向量 + 3 顶点（值类型，避免逐片堆分配）。</summary>
    public struct StlTriangle
    {
        public double Nx, Ny, Nz;     // 法向量
        public double Ax, Ay, Az;     // 顶点 A
        public double Bx, By, Bz;     // 顶点 B
        public double Cx, Cy, Cz;     // 顶点 C
    }

    /// <summary>STL 三角网格模型(SDF / 体内-体外判定用)。仅存三角面片与包围盒，零依赖。</summary>
    public class StlMesh
    {
        /// <summary>三角面片集合。</summary>
        public List<StlTriangle> Triangles = new();
        /// <summary>三角面片数(Triangles.Count)。</summary>
        public int TriangleCount;
        /// <summary>模型包围盒(解析时累计)。</summary>
        public double MinX = double.MaxValue, MaxX = double.MinValue;
        public double MinY = double.MaxValue, MaxY = double.MinValue;
        public double MinZ = double.MaxValue, MaxZ = double.MinValue;
        public string SourcePath = "";
        /// <summary>是否解析到至少一个面片。</summary>
        public bool IsValid => TriangleCount > 0;
    }

    /// <summary>
    /// 导入 STL 文件(自动识别 ASCII / 二进制)，解析为 <see cref="StlMesh"/>。
    /// 识别策略：读取偏移 80 处的 uint 面片数，若 84 + 50·count 恰好等于文件长度则按二进制解析；
    /// 否则按 ASCII("solid" 文本)解析。带异常处理，失败返回空 StlMesh(IsValid=false)。
    /// </summary>
    public static StlMesh ParseStlFile(string filePath)
    {
        var mesh = new StlMesh { SourcePath = filePath };
        try
        {
            if (!File.Exists(filePath)) return mesh;
            var bytes = File.ReadAllBytes(filePath);
            if (bytes.Length < 84) return mesh;

            // 1. 二进制判定：偏移 80 处 uint 面片数 + 84 + 50·n 应等于文件长度
            int triCount = BitConverter.ToInt32(bytes, 80);
            bool isBinary = 84 + 50L * triCount == bytes.Length && triCount > 0;

            if (isBinary)
                ParseStlBinary(bytes, triCount, mesh);
            else
                ParseStlAscii(bytes, mesh);

            // 解析为空时复位包围盒，避免残留 MaxValue/MinValue
            if (mesh.TriangleCount == 0)
                mesh.MinX = mesh.MaxX = mesh.MinY = mesh.MaxY = mesh.MinZ = mesh.MaxZ = 0;
        }
        catch
        {
            // 解析失败返回已解析部分（与项目内其它解析器吞异常风格一致，避免崩溃）
        }
        return mesh;
    }

    /// <summary>解析二进制 STL：每片 50 字节 = 12 个 float(法向 + 3 顶点) + 2 字节属性。</summary>
    private static void ParseStlBinary(byte[] bytes, int triCount, StlMesh mesh)
    {
        mesh.Triangles.Capacity = triCount;
        int off = 84;
        for (int i = 0; i < triCount; i++)
        {
            var t = new StlTriangle
            {
                Nx = ReadFloatLE(bytes, off),       Ny = ReadFloatLE(bytes, off + 4),  Nz = ReadFloatLE(bytes, off + 8),
                Ax = ReadFloatLE(bytes, off + 12),  Ay = ReadFloatLE(bytes, off + 16), Az = ReadFloatLE(bytes, off + 20),
                Bx = ReadFloatLE(bytes, off + 24),  By = ReadFloatLE(bytes, off + 28), Bz = ReadFloatLE(bytes, off + 32),
                Cx = ReadFloatLE(bytes, off + 36),  Cy = ReadFloatLE(bytes, off + 40), Cz = ReadFloatLE(bytes, off + 44),
            };
            off += 50;
            mesh.Triangles.Add(t);
            IncludeMeshBbox(mesh, t.Ax, t.Ay, t.Az);
            IncludeMeshBbox(mesh, t.Bx, t.By, t.Bz);
            IncludeMeshBbox(mesh, t.Cx, t.Cy, t.Cz);
        }
        mesh.TriangleCount = mesh.Triangles.Count;
    }

    /// <summary>解析 ASCII STL：按 "facet normal / vertex / endfacet" 关键字逐行解析。</summary>
    private static void ParseStlAscii(byte[] bytes, StlMesh mesh)
    {
        var inv = CultureInfo.InvariantCulture;
        string text = System.Text.Encoding.ASCII.GetString(bytes);
        using var sr = new StringReader(text);
        string? line;
        StlTriangle t = default;
        int vertexIdx = 0;
        bool inFacet = false;

        while ((line = sr.ReadLine()) != null)
        {
            var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0])
            {
                case "facet":   // facet normal nx ny nz
                    inFacet = true;
                    vertexIdx = 0;
                    if (parts.Length >= 5)
                    {
                        t.Nx = double.Parse(parts[2], inv);
                        t.Ny = double.Parse(parts[3], inv);
                        t.Nz = double.Parse(parts[4], inv);
                    }
                    break;

                case "vertex":  // vertex x y z
                    if (!inFacet || parts.Length < 4) break;
                    {
                        double vx = double.Parse(parts[1], inv);
                        double vy = double.Parse(parts[2], inv);
                        double vz = double.Parse(parts[3], inv);
                        switch (vertexIdx)
                        {
                            case 0: t.Ax = vx; t.Ay = vy; t.Az = vz; break;
                            case 1: t.Bx = vx; t.By = vy; t.Bz = vz; break;
                            case 2: t.Cx = vx; t.Cy = vy; t.Cz = vz; break;
                        }
                        IncludeMeshBbox(mesh, vx, vy, vz);
                        vertexIdx++;
                    }
                    break;

                case "endfacet":
                    if (inFacet && vertexIdx == 3) mesh.Triangles.Add(t);
                    inFacet = false;
                    break;
            }
        }
        mesh.TriangleCount = mesh.Triangles.Count;
    }

    /// <summary>按小端读取 4 字节 float(大端机反转字节)。</summary>
    private static float ReadFloatLE(byte[] b, int off)
    {
        if (BitConverter.IsLittleEndian) return BitConverter.ToSingle(b, off);
        byte[] tmp = { b[off], b[off + 1], b[off + 2], b[off + 3] };
        return BitConverter.ToSingle(tmp, 0);
    }

    /// <summary>把一个顶点纳入模型包围盒。</summary>
    private static void IncludeMeshBbox(StlMesh mesh, double x, double y, double z)
    {
        if (x < mesh.MinX) mesh.MinX = x; if (x > mesh.MaxX) mesh.MaxX = x;
        if (y < mesh.MinY) mesh.MinY = y; if (y > mesh.MaxY) mesh.MaxY = y;
        if (z < mesh.MinZ) mesh.MinZ = z; if (z > mesh.MaxZ) mesh.MaxZ = z;
    }

    // ====================== 源数据绕 X 轴旋转（消除轴/平面不匹配）======================
    // 用途：导入的体素/STL 其材料图案常位于 X-Z 平面，而打印路径位于 X-Y 平面，
    // 直接映射会出现"轴不匹配"。交互式地把源绕 X 轴旋转 ±90° 可把图案平面从 X-Z 转到 X-Y，
    // 使其能正确映射到 X-Y 路径。90° 整数倍为纯坐标置换(无三角函数误差、无插值损失)，
    // 属"源数据预处理变换"——不改动任何映射函数，旋转后直接喂入 AssignTValues/AssignTValuesSdf。

    /// <summary>
    /// 将 STL 网格绕 X 轴旋转 steps×90°（steps 可为负，模 4 归一到 0..3），返回旋转后的<b>新</b>网格(原网格不变)。
    /// 顶点、法向同步置换；包围盒由 <see cref="IncludeMeshBbox"/> 重新累计。
    /// 旋转规则(右手系，绕 +X)：1→(x,y,z)=(x,-z,y)；2→(x,-y,-z)；3→(x,z,-y)。
    /// </summary>
    public static StlMesh RotateStlAroundX(StlMesh? src, int steps)
    {
        var dst = new StlMesh { SourcePath = src?.SourcePath ?? "" };
        if (src == null || !src.IsValid) return dst;
        int s = ((steps % 4) + 4) % 4;   // 归一到 0..3
        dst.Triangles.Capacity = src.Triangles.Count;

        for (int i = 0; i < src.Triangles.Count; i++)
        {
            var t = src.Triangles[i];
            var n = Rot(t.Nx, t.Ny, t.Nz, s);
            var a = Rot(t.Ax, t.Ay, t.Az, s);
            var b = Rot(t.Bx, t.By, t.Bz, s);
            var c = Rot(t.Cx, t.Cy, t.Cz, s);
            dst.Triangles.Add(new StlTriangle
            {
                Nx = n.x, Ny = n.y, Nz = n.z,
                Ax = a.x, Ay = a.y, Az = a.z,
                Bx = b.x, By = b.y, Bz = b.z,
                Cx = c.x, Cy = c.y, Cz = c.z,
            });
            IncludeMeshBbox(dst, a.x, a.y, a.z);
            IncludeMeshBbox(dst, b.x, b.y, b.z);
            IncludeMeshBbox(dst, c.x, c.y, c.z);
        }
        dst.TriangleCount = dst.Triangles.Count;
        return dst;

        // 绕 X 轴 s×90° 的坐标置换(局部函数，90° 整数倍无需 sin/cos)
        static (double x, double y, double z) Rot(double x, double y, double z, int s) => s switch
        {
            1 => (x, -z, y),
            2 => (x, -y, -z),
            3 => (x, z, -y),
            _ => (x, y, z),
        };
    }

    /// <summary>
    /// 将体素数据绕 X 轴旋转 steps×90°（steps 可为负，模 4 归一到 0..3），返回旋转后的<b>新</b> VoxelData(原数据不变)。
    /// 实现：解码 RLE→稠密 3D 数组[Z层][Y行][X列]→按 s 重排轴(新 Y/Z 来自旧 Z/Y 的组合+翻转)→重编码 RLE，
    /// 并同步置换 Origin/Step/RowCount/层数，使"新单元中心 == 旧单元中心旋转后的物理坐标"。
    /// 例：十字33x33x4(图案在 X-Z)经 +90° 后 → ColCount=33、RowCount=33、4 层，十字落入 X-Y，可映射到 X-Y 路径。
    /// </summary>
    public static VoxelData RotateVoxelAroundX(VoxelData? src, int steps)
    {
        if (src == null || src.Matrix.Count == 0) return new VoxelData { SourcePath = src?.SourcePath ?? "" };
        int s = ((steps % 4) + 4) % 4;
        if (s == 0) return CloneVoxel(src);   // 0°：直接深拷贝

        int oldCx = src.ColCount;
        int oldRy = src.Matrix[0].Count > 0 ? src.Matrix[0].Count : src.RowCount;   // 以矩阵实际行数为准
        int oldLz = src.Matrix.Count;

        // 1. 解码为稠密 [oldLz][oldRy][oldCx]
        var oldDense = new int[oldLz][][];
        for (int z = 0; z < oldLz; z++)
        {
            var layer = src.Matrix[z];
            oldDense[z] = new int[oldRy][];
            for (int y = 0; y < oldRy; y++)
                oldDense[z][y] = (y < layer.Count) ? DecodeRleRow(layer[y], oldCx) : new int[oldCx];
        }

        // 2. 新维数：X 不变；s∈{1,3} 时新 Y←旧 Z、新 Z←旧 Y；s==2 仅翻转维数不变
        int newCx = oldCx;
        int newRy = (s == 1 || s == 3) ? oldLz : oldRy;
        int newLz = (s == 1 || s == 3) ? oldRy : oldLz;

        // 3. 索引映射 (nx,ny,nz) ← (ix,iy,iz)，由物理旋转 (x,y,z)→(x,?,?) 推导：
        //    s=1: y'=-z→ny=oldLz-1-iz, z'=y→nz=iy
        //    s=2: y'=-y→ny=oldRy-1-iy, z'=-z→nz=oldLz-1-iz
        //    s=3: y'= z→ny=iz,         z'=-y→nz=oldRy-1-iy
        var newDense = new int[newLz][][];
        for (int nz = 0; nz < newLz; nz++)
        {
            newDense[nz] = new int[newRy][];
            for (int ny = 0; ny < newRy; ny++)
                newDense[nz][ny] = new int[newCx];
        }
        for (int iz = 0; iz < oldLz; iz++)
            for (int iy = 0; iy < oldRy; iy++)
                for (int ix = 0; ix < oldCx; ix++)
                {
                    int nx = ix, ny, nz;
                    if (s == 1) { ny = oldLz - 1 - iz; nz = iy; }
                    else if (s == 2) { ny = oldRy - 1 - iy; nz = oldLz - 1 - iz; }
                    else { ny = iz; nz = oldRy - 1 - iy; }   // s==3
                    newDense[nz][ny][nx] = oldDense[iz][iy][ix];
                }

        // 4. 重编码 RLE + 元数据置换
        var dst = new VoxelData { SourcePath = src.SourcePath };
        for (int nz = 0; nz < newLz; nz++)
        {
            var rows = new List<List<Pixcel>>();
            for (int ny = 0; ny < newRy; ny++)
                rows.Add(EncodeLayerRLE(newDense[nz][ny].ToList()));
            dst.Matrix.Add(rows);
        }
        dst.ColCount = newCx;
        dst.RowCount = newRy;

        // 元数据：使新单元中心 = 旧中心旋转后坐标(Origin 取旋转后新轴的最小端)
        double oY = src.OriginY, oZ = src.OriginZ, sY = src.StepY, sZ = src.StepZ;
        dst.OriginX = src.OriginX; dst.StepX = src.StepX; dst.StepY = sZ; dst.StepZ = sY;
        if (s == 1) { dst.OriginY = -(oZ + oldLz * sZ); dst.OriginZ = oY; }
        else if (s == 2) { dst.StepY = sY; dst.StepZ = sZ; dst.OriginY = -(oY + oldRy * sY); dst.OriginZ = -(oZ + oldLz * sZ); }
        else { dst.OriginY = oZ; dst.OriginZ = -(oY + oldRy * sY); }   // s==3
        return dst;
    }

    /// <summary>深拷贝 VoxelData(含 RLE 矩阵；Pixcel 为 struct，ToList 即值拷贝)。</summary>
    private static VoxelData CloneVoxel(VoxelData src)
    {
        var dst = new VoxelData { SourcePath = src.SourcePath };
        dst.OriginX = src.OriginX; dst.OriginY = src.OriginY; dst.OriginZ = src.OriginZ;
        dst.StepX = src.StepX; dst.StepY = src.StepY; dst.StepZ = src.StepZ;
        dst.ColCount = src.ColCount; dst.RowCount = src.RowCount;
        foreach (var layer in src.Matrix)
        {
            var rows = new List<List<Pixcel>>();
            foreach (var row in layer)
                rows.Add(row?.ToList() ?? new List<Pixcel>());
            dst.Matrix.Add(rows);
        }
        return dst;
    }

    /// <summary>把一行 RLE 体素段展开为长度为 count 的 int 数组(越界截断，缺段补 0)。</summary>
    private static int[] DecodeRleRow(List<Pixcel>? rle, int count)
    {
        var arr = new int[count];
        if (rle == null || rle.Count == 0 || count <= 0) return arr;
        int idx = 0;
        foreach (var seg in rle)
        {
            int n = Math.Max(0, seg.Count);
            for (int k = 0; k < n && idx < count; k++, idx++)
                arr[idx] = seg.Depth;
        }
        return arr;
    }

    /// <summary>
    /// SDF(有符号距离场)映射：以 STL 三角网格为实体边界，对每个路径点做"体内/体外"判定——
    /// 体内 Tool=1、体外 Tool=0；并<b>沿每条路径段密集采样、在 STL 表面穿越(体内↔体外)处插入过渡点</b>
    /// (与 DDA/最近邻/双线性同结构)，避免仅在顶点采样而丢失段内材料切换。判定采用射线奇偶相交法
    /// (射线-三角形 Möller–Trumbore 相交计数)：从路径点沿一个非轴对齐方向发射射线，与网格相交次数为奇数则点在体内。
    /// 与基于体素 CSV 的三种方法不同，本方法以连续 STL 表面为真值源，可作为体素映射精度的"地面真值"参照。
    /// <para><b>尺寸等效</b>：映射前先按"路径点最小外接矩形(XY+Z)↔STL 包围盒"做各轴独立放缩对齐——
    /// 等价于将每个路径点逆变换到 STL 原始坐标系后再判定。与 DDA/最近邻/双线性把体素栅格拉伸到路径外接矩形同一基准，
    /// 保证四种方法在相同的"放缩到路径外接矩形"前提下横向可比。轴退化(零区间)时映射到源中点。</para>
    /// </summary>
    /// <param name="signedDistances">可选：输出每个"实际写入"点(顶点+过渡点)的有符号距离(体内为正、体外为负，单位为 STL 原始坐标)，与返回列表一一对应，供连续 SDF 场分析。</param>
    public static List<Point3D> AssignTValuesSdf(List<Point3D>? originalPoints, StlMesh? mesh,
        List<double>? signedDistances = null)
    {
        var assigned = new List<Point3D>();
        signedDistances?.Clear();
        if (originalPoints == null || originalPoints.Count == 0 || mesh == null || !mesh.IsValid)
            return assigned;

        // 尺寸等效：取路径点最小外接矩形(XY+Z)，建立 path 区间 → STL 区间 的各轴映射
        ComputePointBounds(originalPoints,
            out double pXMin, out double pXMax, out double pYMin, out double pYMax,
            out double pZMin, out double pZMax);

        // 局部函数：路径点(x,y,z) → STL 坐标(各轴独立放缩到路径外接矩形)
        (double sx, double sy, double sz) ToStl(double x, double y, double z) => (
            MapToSourceRange(x, pXMin, pXMax, mesh.MinX, mesh.MaxX),
            MapToSourceRange(y, pYMin, pYMax, mesh.MinY, mesh.MaxY),
            MapToSourceRange(z, pZMin, pZMax, mesh.MinZ, mesh.MaxZ));

        // 局部函数：体内=1/体外=0(经尺寸等效后在 STL 原始坐标下做射线奇偶判定)
        int SdfToolAt(double x, double y, double z)
        {
            var (sx, sy, sz) = ToStl(x, y, z);
            return IsPointInsideMesh(sx, sy, sz, mesh) ? 1 : 0;
        }

        // 局部函数：有符号距离(体内为正、体外为负，单位为 STL 原始坐标)，仅供可选输出
        double SdfSignedDistAt(double x, double y, double z)
        {
            var (sx, sy, sz) = ToStl(x, y, z);
            double d = Math.Sqrt(MinSquaredDistanceToMesh(sx, sy, sz, mesh));
            return IsPointInsideMesh(sx, sy, sz, mesh) ? d : -d;
        }

        // 段内采样步长：取路径 XY 外接矩形短边 / 200(SDF 无体素栅格，按路径尺度定分辨率，较体素法更细)，
        // 保证沿段足够密集以捕获 STL 表面穿越；XY 退化时回退到 Z 尺度，再退化取 1.0
        double step = Math.Min(pXMax - pXMin, pYMax - pYMin) / 200.0;
        if (step <= EPS) step = (pZMax - pZMin) / 200.0;
        if (step <= EPS) step = 1.0;

        for (int i = 0; i < originalPoints.Count; i++)
        {
            var cur = originalPoints[i];

            // 1. 当前顶点：体内/体外 → Tool；仅在点真正写入时同步有符号距离(与 assigned 一一对齐)
            int depth = SdfToolAt(cur.X, cur.Y, cur.Z);
            var mappedCur = ClonePoint(cur);
            mappedCur.Tool = depth;
            if (AddNoDuplicate(assigned, mappedCur) && signedDistances != null)
                signedDistances.Add(SdfSignedDistAt(cur.X, cur.Y, cur.Z));

            // 2. 段内：沿段密集采样，在 STL 表面穿越(体内↔体外)处插入过渡点(与 DDA/最近邻/双线性同结构)
            if (i < originalPoints.Count - 1)
            {
                var next = originalPoints[i + 1];
                // SDF 为连续 3D 判定(段内 WalkSdfSegmentForTransitions 已含 Z 插值)，沿用原打印层判定，不按体素层
                if (cur.Layer == next.Layer)
                {
                    WalkSdfSegmentForTransitions(cur, next, step, SdfToolAt,
                        assigned, depth, signedDistances, SdfSignedDistAt);
                }
            }
        }
        return assigned;
    }

    /// <summary>计算点集的轴对齐外接矩形(XY+Z)。调用方需保证 pts 非空。</summary>
    private static void ComputePointBounds(List<Point3D> pts,
        out double xMin, out double xMax, out double yMin, out double yMax,
        out double zMin, out double zMax)
    {
        xMin = yMin = zMin = double.MaxValue;
        xMax = yMax = zMax = double.MinValue;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            if (p.X < xMin) xMin = p.X; if (p.X > xMax) xMax = p.X;
            if (p.Y < yMin) yMin = p.Y; if (p.Y > yMax) yMax = p.Y;
            if (p.Z < zMin) zMin = p.Z; if (p.Z > zMax) zMax = p.Z;
        }
    }

    /// <summary>
    /// 尺寸等效单轴映射：把 [pMin,pMax] 区间的值 v 线性映射到 [sMin,sMax] 区间(即"把源放缩到路径外接矩形"的逆变换)。
    /// 路径轴退化(pRange≈0)→源中点；源轴退化(sRange≈0)→sMin。避免除零。
    /// </summary>
    private static double MapToSourceRange(double v, double pMin, double pMax, double sMin, double sMax)
    {
        double pRange = pMax - pMin;
        if (pRange <= EPS) return (sMin + sMax) * 0.5;
        double sRange = sMax - sMin;
        if (sRange <= EPS) return sMin;
        return sMin + (v - pMin) / pRange * sRange;
    }

    /// <summary>
    /// 计算单点到 STL 实体的有符号距离(体内为正、体外为负)。供需要连续 SDF 场的分析使用。
    /// 注：本函数在 <b>STL 原始坐标</b>下判定，不做尺寸等效放缩；批量路径映射请用
    /// <see cref="AssignTValuesSdf"/>（自带"路径外接矩形↔STL 包围盒"放缩）。
    /// </summary>
    public static double SignedDistanceToMesh(double x, double y, double z, StlMesh? mesh)
    {
        if (mesh == null || !mesh.IsValid) return 0;
        double d = Math.Sqrt(MinSquaredDistanceToMesh(x, y, z, mesh));
        return IsPointInsideMesh(x, y, z, mesh) ? d : -d;
    }

    /// <summary>点是否在 STL 实体内(射线奇偶法)：先包围盒快速剔除，再逐面片相交计数。</summary>
    private static bool IsPointInsideMesh(double x, double y, double z, StlMesh mesh)
    {
        // 1. 包围盒快速剔除(略放 EPS 容差)
        if (x < mesh.MinX - EPS || x > mesh.MaxX + EPS ||
            y < mesh.MinY - EPS || y > mesh.MaxY + EPS ||
            z < mesh.MinZ - EPS || z > mesh.MaxZ + EPS)
            return false;

        // 2. 选一条非轴对齐射线方向，规避"射线穿过边/顶点/共面"的退化情形
        double dx = 1.0, dy = 0.31415927, dz = 0.27182818;
        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        dx /= len; dy /= len; dz /= len;

        // 3. 逐面片 Möller–Trumbore 相交计数(仅计 t>EPS 的正向交点)
        int crossings = 0;
        var tris = mesh.Triangles;
        for (int i = 0; i < tris.Count; i++)
        {
            if (RayTriangleHit(x, y, z, dx, dy, dz, tris[i], out double t) && t > EPS)
                crossings++;
        }
        return (crossings & 1) == 1;   // 奇数=体内，偶数=体外
    }

    /// <summary>Möller–Trumbore 射线-三角形相交：t 为沿射线方向的参数(长度量)。注：StlTriangle 按值传入
    /// (List 索引器本就按值返回)；研究规模网格下每片 96 字节的拷贝开销可接受，超大网格可改 SoA 平铺数组加速。</summary>
    private static bool RayTriangleHit(double ox, double oy, double oz,
        double dx, double dy, double dz, StlTriangle tri, out double t)
    {
        t = 0;
        double e1x = tri.Bx - tri.Ax, e1y = tri.By - tri.Ay, e1z = tri.Bz - tri.Az;
        double e2x = tri.Cx - tri.Ax, e2y = tri.Cy - tri.Ay, e2z = tri.Cz - tri.Az;

        // p = d × e2
        double px = dy * e2z - dz * e2y;
        double py = dz * e2x - dx * e2z;
        double pz = dx * e2y - dy * e2x;
        double det = e1x * px + e1y * py + e1z * pz;
        if (Math.Abs(det) < 1e-12) return false;   // 射线与三角面平行
        double invDet = 1.0 / det;

        // s = o - A，u = (s·p)·invDet
        double sx = ox - tri.Ax, sy = oy - tri.Ay, sz = oz - tri.Az;
        double u = (sx * px + sy * py + sz * pz) * invDet;
        if (u < -1e-9 || u > 1 + 1e-9) return false;

        // q = s × e1，v = (d·q)·invDet
        double qx = sy * e1z - sz * e1y;
        double qy = sz * e1x - sx * e1z;
        double qz = sx * e1y - sy * e1x;
        double v = (dx * qx + dy * qy + dz * qz) * invDet;
        if (v < -1e-9 || u + v > 1 + 1e-9) return false;

        t = (e2x * qx + e2y * qy + e2z * qz) * invDet;
        return true;
    }

    /// <summary>点到 STL 网格的最短距离平方(逐面片取最小)。供 SDF 距离幅值计算。</summary>
    private static double MinSquaredDistanceToMesh(double x, double y, double z, StlMesh mesh)
    {
        double best = double.MaxValue;
        var tris = mesh.Triangles;
        for (int i = 0; i < tris.Count; i++)
        {
            double d = PointTriangleDistanceSq(x, y, z, tris[i]);
            if (d < best) best = d;
        }
        return best == double.MaxValue ? 0 : best;
    }

    /// <summary>点到三角面片最短距离平方(Ericson《Real-Time Collision Detection》算法，分 7 个区域)。</summary>
    private static double PointTriangleDistanceSq(double px, double py, double pz, StlTriangle tri)
    {
        double ax = tri.Ax, ay = tri.Ay, az = tri.Az;
        double bx = tri.Bx, by = tri.By, bz = tri.Bz;
        double cx = tri.Cx, cy = tri.Cy, cz = tri.Cz;

        double abx = bx - ax, aby = by - ay, abz = bz - az;
        double acx = cx - ax, acy = cy - ay, acz = cz - az;
        double apx = px - ax, apy = py - ay, apz = pz - az;
        double d1 = abx * apx + aby * apy + abz * apz;   // AP·AB
        double d2 = acx * apx + acy * apy + acz * apz;   // AP·AC
        if (d1 <= 0 && d2 <= 0) return apx * apx + apy * apy + apz * apz;   // 区域①：顶点 A 最近

        double bpx = px - bx, bpy = py - by, bpz = pz - bz;
        double d3 = abx * bpx + aby * bpy + abz * bpz;   // BP·AB
        double d4 = acx * bpx + acy * bpy + acz * bpz;   // BP·AC
        if (d3 >= 0 && d4 <= d3) return bpx * bpx + bpy * bpy + bpz * bpz; // 区域②：顶点 B 最近

        double vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)                                     // 区域③：边 AB 最近
        {
            double v = d1 / (d1 - d3);
            double qx = ax + v * abx, qy = ay + v * aby, qz = az + v * abz;
            double ex = px - qx, ey = py - qy, ez = pz - qz;
            return ex * ex + ey * ey + ez * ez;
        }

        double cpx = px - cx, cpy = py - cy, cpz = pz - cz;
        double d5 = abx * cpx + aby * cpy + abz * cpz;   // CP·AB
        double d6 = acx * cpx + acy * cpy + acz * cpz;   // CP·AC
        if (d6 >= 0 && d5 <= d6) return cpx * cpx + cpy * cpy + cpz * cpz; // 区域④：顶点 C 最近

        double vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)                                     // 区域⑤：边 AC 最近
        {
            double w = d2 / (d2 - d6);
            double qx = ax + w * acx, qy = ay + w * acy, qz = az + w * acz;
            double ex = px - qx, ey = py - qy, ez = pz - qz;
            return ex * ex + ey * ey + ez * ez;
        }

        double va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)                       // 区域⑥：边 BC 最近
        {
            double w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            double qx = bx + w * (cx - bx), qy = by + w * (cy - by), qz = bz + w * (cz - bz);
            double ex = px - qx, ey = py - qy, ez = pz - qz;
            return ex * ex + ey * ey + ez * ez;
        }

        // 区域⑦：面内最近点(重心坐标)
        double denom = 1.0 / (va + vb + vc);
        double vn = vb * denom;
        double wn = vc * denom;
        double fx = ax + abx * vn + acx * wn;
        double fy = ay + aby * vn + acy * wn;
        double fz = az + abz * vn + acz * wn;
        double dx = px - fx, dy = py - fy, dz = pz - fz;
        return dx * dx + dy * dy + dz * dz;
    }

    /// <summary>DDA 光线投射：遍历 start→end 经过的所有体素单元，返回按进入时刻 t 升序的 (tEntry,row,col)。</summary>
    private static List<(double tEntry, int row, int col)> CastRayDDA(Point3D start, Point3D end, VoxelMapFrame frame)
    {
        var visits = new List<(double, int, int)>();
        double gx0 = (start.X - frame.XMin) / frame.CellSizeX;
        double gy0 = (start.Y - frame.YMin) / frame.CellSizeY;
        double gx1 = (end.X - frame.XMin) / frame.CellSizeX;
        double gy1 = (end.Y - frame.YMin) / frame.CellSizeY;

        double dgx = gx1 - gx0, dgy = gy1 - gy0;
        int col = (int)Math.Floor(gx0), row = (int)Math.Floor(gy0);
        visits.Add((0.0, row, col));
        if (Math.Abs(dgx) < 1e-12 && Math.Abs(dgy) < 1e-12) return visits;

        int stepCol = Math.Sign(dgx), stepRow = Math.Sign(dgy);
        double tDeltaCol = Math.Abs(dgx) > 1e-12 ? Math.Abs(1.0 / dgx) : double.MaxValue;
        double tDeltaRow = Math.Abs(dgy) > 1e-12 ? Math.Abs(1.0 / dgy) : double.MaxValue;

        double tMaxCol = stepCol > 0 ? (col + 1 - gx0) / dgx : (stepCol < 0 ? (col - gx0) / dgx : double.MaxValue);
        double tMaxRow = stepRow > 0 ? (row + 1 - gy0) / dgy : (stepRow < 0 ? (row - gy0) / dgy : double.MaxValue);

        const double CMP_EPS = 1e-9;
        const int MAX_STEPS = 50000;
        for (int step = 0; step < MAX_STEPS; step++)
        {
            if (tMaxCol < tMaxRow - CMP_EPS)
            {
                if (tMaxCol > 1.0 + CMP_EPS) break;
                col += stepCol; visits.Add((tMaxCol, row, col)); tMaxCol += tDeltaCol;
            }
            else if (tMaxRow < tMaxCol - CMP_EPS)
            {
                if (tMaxRow > 1.0 + CMP_EPS) break;
                row += stepRow; visits.Add((tMaxRow, row, col)); tMaxRow += tDeltaRow;
            }
            else
            {
                if (tMaxCol > 1.0 + CMP_EPS) break;
                col += stepCol; row += stepRow; visits.Add((tMaxCol, row, col));
                tMaxCol += tDeltaCol; tMaxRow += tDeltaRow;
            }
        }
        return visits;
    }

    /// <summary>按行列索引从 RLE 体素层解码深度值。</summary>
    private static int LookupRLEDepth(List<List<Pixcel>> voxelLayer, int row, int col)
    {
        if (row < 0 || row >= voxelLayer.Count) return 0;
        var rleRow = voxelLayer[row];
        if (rleRow == null || rleRow.Count == 0) return 0;
        int acc = 0;
        for (int i = 0; i < rleRow.Count; i++)
        {
            acc += Math.Max(0, rleRow[i].Count);
            if (col < acc) return rleRow[i].Depth;
        }
        return rleRow[^1].Depth;
    }

    private static int GetVoxelDepthAtPoint(double x, double y, List<List<Pixcel>> voxelLayer, VoxelMapFrame frame)
    {
        int col = GetVoxelCellIndex(x, frame.XMin, frame.CellSizeX, frame.ColCount);
        int row = GetVoxelCellIndex(y, frame.YMin, frame.CellSizeY, frame.RowCount);
        if (row < 0 || row >= frame.RowCount || col < 0 || col >= frame.ColCount) return 0;
        return LookupRLEDepth(voxelLayer, row, col);
    }

    private static int GetVoxelCellIndex(double value, double min, double cellSize, int count)
    {
        if (count <= 0 || cellSize <= 1e-9) return -1;
        double relative = (value - min) / cellSize;
        if (relative < 0.0 || relative >= count)
        {
            if (Math.Abs(relative - count) <= 1e-9) return count - 1;
            return -1;
        }
        return Math.Max(0, Math.Min(count - 1, (int)Math.Floor(relative)));
    }

    /// <summary>
    /// 按路径点 Z 坐标确定体素层索引（尺寸等效：体素 <see cref="VoxelMapFrame.LayerCount"/> 层
    /// 拉伸到路径点 Z 外接矩形 [ZMin,ZMax]，用 Z 落入哪个单元定层）。
    /// 越界点<b>夹紧</b>到边界层（区别于 <see cref="GetVoxelCellIndex"/> 的 -1 越界返回，
    /// 与 GetNearestVoxelDepthAtPoint/BilinearDepthAtPoint 的边缘夹紧哲学一致）；
    /// 区间退化(LayerCount≤0 或 CellSizeZ≤0)返回第 0 层。
    /// </summary>
    private static int GetVoxelLayerByZ(double z, VoxelMapFrame frame)
    {
        if (frame.LayerCount <= 0 || frame.CellSizeZ <= EPS) return 0;
        int idx = (int)Math.Floor((z - frame.ZMin) / frame.CellSizeZ);
        if (idx < 0) return 0;
        if (idx >= frame.LayerCount) return frame.LayerCount - 1;
        return idx;
    }

    private static int GetVoxelColumnCount(List<List<Pixcel>> layerPixels)
    {
        int maxCols = 0;
        foreach (var row in layerPixels)
        {
            if (row == null) continue;
            int cols = row.Sum(p => Math.Max(0, p.Count));
            if (cols > maxCols) maxCols = cols;
        }
        return maxCols;
    }

    private static Point3D ClonePoint(Point3D p) =>
        new(p.X, p.Y, p.Z, p.Extrude, p.Feed, p.Pressure, p.Tool, p.Layer, p.GridType, p.MaterialA);

    private static Point3D InterpolatePoint(Point3D a, Point3D b, double t)
    {
        t = Math.Max(0.0, Math.Min(1.0, t));
        return new Point3D(
            a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t,
            b.Extrude, b.Feed, b.Pressure, b.Tool, a.Layer, a.GridType, a.MaterialA);
    }

    /// <summary>追加点，若与末点坐标+Tool 完全一致则跳过(去重)。返回是否真正写入(供调用方同步并列输出)。</summary>
    private static bool AddNoDuplicate(List<Point3D> points, Point3D point)
    {
        const double DUP_EPS = 1e-9;
        if (points.Count == 0) { points.Add(point); return true; }
        var last = points[^1];
        if (Math.Abs(last.X - point.X) < DUP_EPS &&
            Math.Abs(last.Y - point.Y) < DUP_EPS &&
            Math.Abs(last.Z - point.Z) < DUP_EPS &&
            last.Tool == point.Tool)
            return false;
        points.Add(point);
        return true;
    }

    // ====================== 体素映射精度评估（自 FrmPrintStep2 移植）======================

    /// <summary>
    /// 评估体素映射后路径的精度：将映射深度(Tool)与原体素文件真实深度逐点对比。
    /// 内部重建与 <see cref="AssignTValues"/> 完全一致的 frame(含 Z 维度)与按 Z 定层规则，保证评估基准统一。
    /// </summary>
    /// <param name="mappedPoints">映射后路径(assignedPoints 或 SimplifyPath 后的点)</param>
    /// <param name="voxel">原体素数据</param>
    /// <param name="originalPoints">原始路径(用于构建与映射一致的 frame 与层映射)；null 时使用 mappedPoints</param>
    /// <param name="sampleSpacing">沿折线密集采样间距(mm)；&lt;=0 时取 min(cellSizeX,cellSizeY)/10</param>
    /// <param name="mode">采样深度取值方式，默认 Stepped</param>
    public static VoxelMapAccuracyReport EvaluateVoxelMapAccuracy(
        List<Point3D> mappedPoints, VoxelData? voxel,
        List<Point3D>? originalPoints = null,
        double sampleSpacing = 0.0,
        VoxelMapSampleMode mode = VoxelMapSampleMode.Stepped)
    {
        var report = new VoxelMapAccuracyReport();
        if (mappedPoints == null || mappedPoints.Count == 0 || voxel == null || voxel.Matrix.Count == 0)
            return report;

        try
        {
            // 用于构建 frame 的源点（默认与映射时一致：用原始路径的外接矩形）
            var frameSrc = (originalPoints != null && originalPoints.Count > 0) ? originalPoints : mappedPoints;

            // ---- 1. 重建与映射一致的 frame（路径外接矩形各轴独立拉伸覆盖体素网格，含 Z 层，与 AssignTValues 统一）----
            int voxelLayerCount = voxel.Matrix.Count;
            int vRows = voxel.RowCount > 0 ? voxel.RowCount : voxel.Matrix[0].Count;
            int vCols = voxel.ColCount > 0 ? voxel.ColCount : GetVoxelColumnCount(voxel.Matrix[0]);

            double xMin = frameSrc.Min(p => p.X);
            double xMax = frameSrc.Max(p => p.X);
            double yMin = frameSrc.Min(p => p.Y);
            double yMax = frameSrc.Max(p => p.Y);
            double zMin = frameSrc.Min(p => p.Z);
            double zMax = frameSrc.Max(p => p.Z);
            double cellSizeX = vCols > 0 ? (xMax - xMin) / vCols : 1.0;
            double cellSizeY = vRows > 0 ? (yMax - yMin) / vRows : 1.0;
            double cellSizeZ = voxelLayerCount > 0 ? (zMax - zMin) / voxelLayerCount : 1.0;
            if (cellSizeX <= EPS) cellSizeX = 1.0;
            if (cellSizeY <= EPS) cellSizeY = 1.0;
            if (cellSizeZ <= EPS) cellSizeZ = 1.0;

            var frame = new VoxelMapFrame
            {
                XMin = xMin, XMax = xMax, YMin = yMin, YMax = yMax,
                CellSizeX = cellSizeX, CellSizeY = cellSizeY,
                ColCount = vCols, RowCount = vRows,
                ZMin = zMin, ZMax = zMax, CellSizeZ = cellSizeZ, LayerCount = voxelLayerCount,
            };

            // ---- 2. 体素层由路径点 Z 定(GetVoxelLayerByZ)，与映射同一规则；不再用"路径层→体素层"字典 ----
            //        hitLayers 在节点循环中收集实际命中的体素层，供覆盖召回率分母统计
            var hitLayers = new HashSet<int>();
            // 路径打印层数(来自 ;LAYER 注释，仅供诊断；与体素层映射无关)
            int pathLayerCount = frameSrc.Select(p => p.Layer).Distinct().Count();

            report.PathLayerCount = pathLayerCount;
            report.VoxelLayerCount = voxelLayerCount;
            // SharedVoxelLayerMax 在节点循环后按实际命中体素层数(hitLayers.Count)赋值

            // 默认采样间距：体素单元短边的 1/10
            if (sampleSpacing <= EPS) sampleSpacing = Math.Min(frame.CellSizeX, frame.CellSizeY) / 10.0;
            if (sampleSpacing <= EPS) sampleSpacing = 0.01;
            report.SampleSpacing = sampleSpacing;

            // ---- 3. 节点级评估：逐节点核对 Tool 与真实深度 ----
            var nodeAbsErr = new List<double>(mappedPoints.Count);
            int coveredNonZero = 0;
            var coveredCells = new HashSet<long>();   // 体素单元去重：key = vLayer*Rows*Cols + row*Cols + col

            // 切换点统计：Tool 值发生变化的节点（材料切换指令处）
            var switchAbsErr = new List<double>();
            int prevNodeTool = -1;
            bool hasPrevNode = false;
            // 实际切换点坐标（用于位置偏差：与理论材料边界位置匹配）
            var actualSwitchPos = new List<(double X, double Y, int Layer)>();

            for (int i = 0; i < mappedPoints.Count; i++)
            {
                var p = mappedPoints[i];
                int vLayerIdx = GetVoxelLayerByZ(p.Z, frame);   // 按路径点 Z 定体素层(与映射同基准)
                hitLayers.Add(vLayerIdx);                       // 收集实际命中的体素层
                var voxelLayer = voxel.Matrix[vLayerIdx];

                int dTrue = GetVoxelDepthAtPoint(p.X, p.Y, voxelLayer, frame);
                int dPred = p.Tool;
                int err = dPred - dTrue;

                nodeAbsErr.Add(Math.Abs(err));
                if (err == 0) report.NodeExactMatch++;

                // 切换点：与上一节点 Tool 不同（即该处下达材料切换指令），统计其定位误差
                if (hasPrevNode && p.Tool != prevNodeTool)
                {
                    report.SwitchPointCount++;
                    switchAbsErr.Add(Math.Abs(err));
                    if (err == 0) report.SwitchPointExactMatch++;
                    actualSwitchPos.Add((p.X, p.Y, p.Layer));   // 记录实际切换位置(供位置偏差匹配)
                }
                prevNodeTool = p.Tool;
                hasPrevNode = true;

                int col = GetVoxelCellIndex(p.X, frame.XMin, frame.CellSizeX, frame.ColCount);
                int row = GetVoxelCellIndex(p.Y, frame.YMin, frame.CellSizeY, frame.RowCount);
                if (row >= 0 && row < frame.RowCount && col >= 0 && col < frame.ColCount)
                {
                    long key = (((long)vLayerIdx) * frame.RowCount + row) * frame.ColCount + col;
                    if (coveredCells.Add(key))
                    {
                        report.CoveredCells++;
                        if (dTrue > 0) coveredNonZero++;
                    }
                }
            }

            // 按 Z 定层后"共享层"概念消失；此处记路径实际命中的不同体素层数(Z 向利用率诊断)
            report.SharedVoxelLayerMax = hitLayers.Count;
            report.NodeCount = mappedPoints.Count;
            report.NodeExactRate = report.NodeCount > 0 ? (double)report.NodeExactMatch / report.NodeCount : 0;
            report.CoveredNonZeroCells = coveredNonZero;
            report.MaterialCoverRate = report.CoveredCells > 0 ? (double)coveredNonZero / report.CoveredCells : 0;
            ComputeErrorStats(nodeAbsErr, out double nMae, out double nMax, out double nStd, out _);
            report.NodeMAE = nMae; report.NodeMaxAE = nMax; report.NodeStdAE = nStd;

            // ---- 4. 路径级评估：沿折线密集采样 ----
            var sampleAbsErr = new List<double>();
            report.ErrBins = new int[VoxelMapAccuracyReport.ErrBinLabels.Length];

            // 边界统计：真实材料 d_true 沿路径发生 0↔1 过渡处（边界带）的采样误差
            var boundaryAbsErr = new List<double>();
            int prevSampleDTrue = -1;
            bool hasPrevSample = false;
            double prevSampleAbsErr = 0;
            double prevSampleX = 0, prevSampleY = 0;
            // 理论切换位置：d_true 变化处（真实材料边界与路径交点）的近似坐标
            var theoreticalSwitchPos = new List<(double X, double Y, int Layer)>();
            const int MAX_RECORDS = 200000;   // 误差明细上限，防止超长路径占用过多内存
            bool detailOn = true;

            for (int i = 0; i < mappedPoints.Count - 1; i++)
            {
                var a = mappedPoints[i];
                var b = mappedPoints[i + 1];
                // 仅当两端点落入同一体素层时在该层内采样(原按打印层号 Layer 判定，现按 Z 定层)
                if (GetVoxelLayerByZ(a.Z, frame) != GetVoxelLayerByZ(b.Z, frame)) continue;

                double segLen = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                if (segLen <= EPS) continue;

                int vLayerIdx = GetVoxelLayerByZ(a.Z, frame);   // 段内采样点沿用段起点 a 的体素层
                var voxelLayer = voxel.Matrix[vLayerIdx];
                int nSteps = (int)Math.Ceiling(segLen / sampleSpacing);
                if (nSteps < 1) nSteps = 1;

                for (int k = 0; k <= nSteps; k++)
                {
                    // 跳过段起点(k=0)以避免与上一段终点重复；首段(i==0)起点保留
                    if (k == 0 && i > 0) continue;

                    double t = (double)k / nSteps;
                    double sx = a.X + (b.X - a.X) * t;
                    double sy = a.Y + (b.Y - a.Y) * t;

                    int dTrue = GetVoxelDepthAtPoint(sx, sy, voxelLayer, frame);
                    // 映射深度：Stepped 取本子段起点 a 的 Tool；Linear 在 a→b 间线性插值
                    int dPred = mode == VoxelMapSampleMode.Linear
                        ? (int)Math.Round(a.Tool + (b.Tool - a.Tool) * t)
                        : a.Tool;

                    int err = dPred - dTrue;
                    sampleAbsErr.Add(Math.Abs(err));
                    if (err == 0) report.SampleExactMatch++;

                    // 边界带：当前采样 d_true 与前一采样不同(材料过渡)，则两者同属边界，计入边界误差
                    int curAbsErr = Math.Abs(err);
                    if (hasPrevSample && dTrue != prevSampleDTrue)
                    {
                        boundaryAbsErr.Add(prevSampleAbsErr);   // 过渡前一侧
                        boundaryAbsErr.Add(curAbsErr);          // 过渡后一侧
                        // 理论材料边界 ≈ 相邻两采样中点(采样间距 ≤ 单元短边/10，近似误差 ≤ Δs/2)
                        theoreticalSwitchPos.Add(((sx + prevSampleX) * 0.5, (sy + prevSampleY) * 0.5, a.Layer));
                    }
                    prevSampleDTrue = dTrue;
                    prevSampleAbsErr = curAbsErr;
                    prevSampleX = sx;
                    prevSampleY = sy;
                    hasPrevSample = true;
                    AccumulateErrorBin(report.ErrBins, Math.Abs(err));
                    report.SampleCount++;

                    if (detailOn)
                    {
                        if (report.ErrorRecords.Count >= MAX_RECORDS)
                            detailOn = false;
                        else
                            report.ErrorRecords.Add(new VoxelMapErrorRecord
                            {
                                X = sx, Y = sy, Layer = a.Layer, VoxelLayerIdx = vLayerIdx,
                                DPred = dPred, DTrue = dTrue, Error = err,
                            });
                    }
                }
            }

            // 路径级统计（若无同层可采样段，回退为节点级以保证报告非空）
            if (report.SampleCount > 0)
            {
                report.SampleExactRate = (double)report.SampleExactMatch / report.SampleCount;
                ComputeErrorStats(sampleAbsErr, out double sMae, out double sMax, out _, out double sRmse);
                report.SampleMAE = sMae; report.SampleMaxAE = sMax; report.SampleRMSE = sRmse;
            }
            else
            {
                report.SampleCount = report.NodeCount;
                report.SampleExactMatch = report.NodeExactMatch;
                report.SampleExactRate = report.NodeExactRate;
                report.SampleMAE = report.NodeMAE;
                report.SampleMaxAE = report.NodeMaxAE;
                report.SampleRMSE = report.NodeMAE;
            }

            // ---- 5. 边界 / 切换点 / 覆盖完整率汇总 ----
            // 边界 MAE / RMSE（RMSE 反映边界锯齿程度）
            if (boundaryAbsErr.Count > 0)
            {
                report.BoundarySampleCount = boundaryAbsErr.Count;
                ComputeErrorStats(boundaryAbsErr, out double bMae, out _, out _, out double bRmse);
                report.BoundaryMAE = bMae;
                report.BoundaryRMSE = bRmse;
            }
            // 切换点 MAE / 正确率
            if (report.SwitchPointCount > 0)
            {
                ComputeErrorStats(switchAbsErr, out double swMae, out _, out _, out _);
                report.SwitchPointMAE = swMae;
                report.SwitchPointExactRate = (double)report.SwitchPointExactMatch / report.SwitchPointCount;
            }
            // 切换点位置偏差（连续几何精度·主指标）：每个实际切换点 → 同层最近理论切换点的几何距离
            report.TheoreticalSwitchCount = theoreticalSwitchPos.Count;
            if (actualSwitchPos.Count > 0 && theoreticalSwitchPos.Count > 0)
            {
                // 理论切换点按层分组，加速同层最近邻查找
                var theoByLayer = new Dictionary<int, List<(double X, double Y, int Layer)>>();
                foreach (var t in theoreticalSwitchPos)
                {
                    if (!theoByLayer.TryGetValue(t.Layer, out var list))
                    {
                        list = new List<(double X, double Y, int Layer)>();
                        theoByLayer[t.Layer] = list;
                    }
                    list.Add(t);
                }
                var posErr = new List<double>();
                foreach (var a in actualSwitchPos)
                {
                    if (!theoByLayer.TryGetValue(a.Layer, out var theoList) || theoList.Count == 0) continue;
                    double bestSq = double.MaxValue;
                    foreach (var t in theoList)
                    {
                        double dd = (a.X - t.X) * (a.X - t.X) + (a.Y - t.Y) * (a.Y - t.Y);
                        if (dd < bestSq) bestSq = dd;
                    }
                    posErr.Add(Math.Sqrt(bestSq));
                }
                if (posErr.Count > 0)
                {
                    ComputeErrorStats(posErr, out double pMae, out double pMax, out _, out double pRmse);
                    report.SwitchPointPosMAE = pMae;
                    report.SwitchPointPosMax = pMax;
                    report.SwitchPointPosRMSE = pRmse;
                }

                // 材料位置偏差(T→S)：每个理论切换点 → 同层最近实际切换点的几何距离。
                // 与上方 S→T 反向配对：S→T 看"切换指令定位精度"，T→S 看"材料边界完整性(真实边界是否被捕捉)"。
                var actualByLayer = new Dictionary<int, List<(double X, double Y, int Layer)>>();
                foreach (var s in actualSwitchPos)
                {
                    if (!actualByLayer.TryGetValue(s.Layer, out var sList))
                    {
                        sList = new List<(double X, double Y, int Layer)>();
                        actualByLayer[s.Layer] = sList;
                    }
                    sList.Add(s);
                }
                var posErrRev = new List<double>();
                foreach (var t in theoreticalSwitchPos)
                {
                    if (!actualByLayer.TryGetValue(t.Layer, out var sList2) || sList2.Count == 0) continue;
                    double bestSq = double.MaxValue;
                    foreach (var s in sList2)
                    {
                        double dd = (t.X - s.X) * (t.X - s.X) + (t.Y - s.Y) * (t.Y - s.Y);
                        if (dd < bestSq) bestSq = dd;
                    }
                    posErrRev.Add(Math.Sqrt(bestSq));
                }
                if (posErrRev.Count > 0)
                {
                    ComputeErrorStats(posErrRev, out double mMae, out double mMax, out _, out double mRmse);
                    report.MaterialPosMAE = mMae;
                    report.MaterialPosMax = mMax;
                    report.MaterialPosRMSE = mRmse;
                }
            }
            // 覆盖完整率(召回率)：分母 = 路径涉及体素层中 d_true>0 的单元总数
            int tpTotal = CountPositiveCellsInLayers(voxel, hitLayers);   // 分母=路径实际命中体素层中 depth>0 单元数
            report.TruePositiveCells = tpTotal;
            report.CoverageRecall = tpTotal > 0 ? (double)coveredNonZero / tpTotal : 0;
        }
        catch
        {
            // 评估失败返回部分填充的报告（与 AMCP 一致：吞异常避免崩溃）
        }
        return report;
    }

    /// <summary>统计指定体素层集合中真实有材料(depth>0)的单元总数（覆盖完整率/召回率的分母）。
    /// 同一体素层被多个路径层映射时只计一次。</summary>
    private static int CountPositiveCellsInLayers(VoxelData voxel, IEnumerable<int> voxelLayerIdxs)
    {
        if (voxel?.Matrix == null) return 0;
        var seen = new HashSet<int>();
        int total = 0;
        foreach (int idx in voxelLayerIdxs)
        {
            if (idx < 0 || idx >= voxel.Matrix.Count) continue;
            if (!seen.Add(idx)) continue;   // 同一体素层只计一次
            var layer = voxel.Matrix[idx];
            if (layer == null) continue;
            foreach (var row in layer)
            {
                if (row == null) continue;
                foreach (var seg in row)
                    if (seg.Depth > 0) total += Math.Max(0, seg.Count);
            }
        }
        return total;
    }

    /// <summary>
    /// 将评估误差明细导出为 CSV（列：X,Y,Layer,VoxelLayer,DPred,DTrue,Error），
    /// 便于在 Origin 中绘制误差空间分布与直方图。
    /// </summary>
    public static bool ExportVoxelMapAccuracyCsv(VoxelMapAccuracyReport report, string filePath)
    {
        try
        {
            if (report == null || report.ErrorRecords == null || report.ErrorRecords.Count == 0) return false;
            var lines = new List<string> { "X,Y,Layer,VoxelLayer,DPred,DTrue,Error" };
            foreach (var r in report.ErrorRecords)
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0:F4},{1:F4},{2},{3},{4},{5},{6}",
                    r.X, r.Y, r.Layer, r.VoxelLayerIdx, r.DPred, r.DTrue, r.Error));
            File.WriteAllLines(filePath, lines);
            return true;
        }
        catch { return false; }
    }

    /// <summary>计算一组绝对误差的 MAE / MaxAE / Std / RMSE</summary>
    private static void ComputeErrorStats(List<double> absErr,
        out double mae, out double max, out double std, out double rmse)
    {
        mae = max = std = rmse = 0;
        if (absErr == null || absErr.Count == 0) return;
        double sum = 0, sumSq = 0;
        max = absErr[0];
        foreach (double e in absErr) { sum += e; sumSq += e * e; if (e > max) max = e; }
        int n = absErr.Count;
        mae = sum / n;
        rmse = Math.Sqrt(sumSq / n);
        double var = sumSq / n - mae * mae;
        std = var > 0 ? Math.Sqrt(var) : 0;
    }

    /// <summary>将绝对误差累计到分桶数组（与 VoxelMapAccuracyReport.ErrBinLabels 对应）。</summary>
    private static void AccumulateErrorBin(int[]? bins, double absErr)
    {
        if (bins == null) return;
        int e = (int)Math.Round(absErr);
        int idx;
        if (e <= 0) idx = 0;
        else if (e == 1) idx = 1;
        else if (e == 2) idx = 2;
        else if (e == 3) idx = 3;
        else if (e <= 5) idx = 4;
        else if (e <= 10) idx = 5;
        else idx = 6;
        bins[idx]++;
    }
}
