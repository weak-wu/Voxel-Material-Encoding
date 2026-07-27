using System.Globalization;
using System.Text;
using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// 体素生成器：以光线投影法（ray casting / 扫描线体素化）把 STL 三角网格离散为体素栅格。
/// 对栅格的每一列（垂直于投影方向的平面），沿投影轴发射一条射线穿过 STL 网格；
/// 求射线与全部三角形的交点（Möller–Trumbore），交点按弧长排序后奇偶计数——
/// 越过奇数个交点 = 已进入实体（体内=1），偶数个 = 在实体外（体外=0）。
/// 每列只做一次“射线↔网格”求交，该列所有轴向体素复用交点做区间包含判定，
/// 较“逐体素中心点判定”快约 N_scan 倍。
/// 输出 <see cref="VoxelGrid"/> 可经 <see cref="ExportVoxelCsv"/> 导出为标准体素 CSV
/// （格式对齐示例 50x50棋盘格_vox_*.csv，可被 <see cref="VoxelMapper.ParseVoxelFile"/> 读回），
/// 或经 <see cref="ToVoxelData"/> 转 <see cref="VoxelMapper.VoxelData"/> 供 Viewport3D 预览。
/// </summary>
public static class VoxelGenerator
{
    private const double EPS = 1e-9;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>体素栅格：稠密 0/1 数值 + 网格元数据。Values 线性存储(X 最快、Z 最慢)。</summary>
    public class VoxelGrid
    {
        /// <summary>体素值(0=体外 / 1=体内)，索引 <c>ix + iy*CountX + iz*CountX*CountY</c>。</summary>
        public int[] Values = Array.Empty<int>();
        public double OriginX, OriginY, OriginZ;
        public double VoxelSize = 1.0;
        public int CountX, CountY, CountZ;
        public string SourcePath = "";
        public int TotalCount => CountX * CountY * CountZ;

        /// <summary>按 (ix,iy,iz) 取/设体素值。</summary>
        public int this[int ix, int iy, int iz]
        {
            get => Values[ix + iy * CountX + iz * CountX * CountY];
            set => Values[ix + iy * CountX + iz * CountX * CountY] = value;
        }
    }

    /// <summary>
    /// 光线投影体素化：沿 <paramref name="axis"/> (0=X,1=Y,2=Z) 方向投射射线，
    /// 把 <paramref name="mesh"/> 离散为体素栅格。网格原点对齐到 voxelSize 网格并覆盖模型包围盒。
    /// 不旋转模型——射线方向与体素坐标始终在模型原始坐标系，输出 CSV 坐标即原模型空间。
    /// 含共享边交点去重：消除三角化对角线处"同一表面被两三角形各命中一次"导致的奇偶翻转(对角线下方缺体素)。
    /// </summary>
    /// <param name="mesh">STL 三角网格(由 VoxelMapper.ParseStlFile 导入)。</param>
    /// <param name="voxelSize">体素边长(mm)。</param>
    /// <param name="axis">投影方向：0=沿 X、1=沿 Y、2=沿 Z。</param>
    public static VoxelGrid VoxelizeByRayCasting(VoxelMapper.StlMesh mesh, double voxelSize, int axis)
    {
        if (mesh == null || !mesh.IsValid)
            throw new ArgumentException("STL 网格无效或未加载。");
        if (voxelSize <= EPS)
            throw new ArgumentException("体素边长必须大于 0。");
        if (axis < 0 || axis > 2)
            throw new ArgumentException("投影方向必须是 0(X)/1(Y)/2(Z)。");

        // 1. 网格范围：原点对齐到 voxelSize 网格(floor)，终点向上取整(ceil)，完全覆盖模型包围盒
        double[] min = { mesh.MinX, mesh.MinY, mesh.MinZ };
        double[] max = { mesh.MaxX, mesh.MaxY, mesh.MaxZ };
        double[] origin = new double[3];
        int[] count = new int[3];
        for (int a = 0; a < 3; a++)
        {
            origin[a] = Math.Floor(min[a] / voxelSize) * voxelSize;
            double end = Math.Ceiling(max[a] / voxelSize) * voxelSize;
            count[a] = Math.Max(1, (int)Math.Round((end - origin[a]) / voxelSize));
        }

        var grid = new VoxelGrid
        {
            VoxelSize = voxelSize,
            OriginX = origin[0], OriginY = origin[1], OriginZ = origin[2],
            CountX = count[0], CountY = count[1], CountZ = count[2],
            Values = new int[count[0] * count[1] * count[2]],
            SourcePath = mesh.SourcePath,
        };

        // 2. 投影轴 scan=axis；列平面另两轴 uA,vA
        int scan = axis;
        int uA = (axis + 1) % 3;
        int vA = (axis + 2) % 3;

        var tris = mesh.Triangles;
        var crossings = new List<double>(tris.Count);   // 复用，避免逐列分配
        double[] o = new double[3];                     // 射线起点(循环内改值，不重新分配)
        int[] idx = new int[3];                         // (ix,iy,iz) 写入索引

        // 3. 逐列发射射线：列 = (iu 沿 uA, iv 沿 vA)
        for (int iu = 0; iu < count[uA]; iu++)
        {
            o[uA] = origin[uA] + (iu + 0.5) * voxelSize;
            for (int iv = 0; iv < count[vA]; iv++)
            {
                o[vA] = origin[vA] + (iv + 0.5) * voxelSize;
                o[scan] = origin[scan] - voxelSize;     // 射线起点略低于网格底，保证穿过整列

                // 求射线与全部三角形的交点(取 scan 轴坐标)
                crossings.Clear();
                for (int t = 0; t < tris.Count; t++)
                {
                    if (RayHitAxis(o[0], o[1], o[2], scan, tris[t], out double hitCoord))
                        crossings.Add(hitCoord);
                }
                if (crossings.Count == 0) continue;     // 该列无贯穿，整列保持体外(0)
                crossings.Sort();

                // 3b. 交点去重(就地压缩)：STL 常把一个矩形面片沿对角线切成两个三角形，二者共享该对角线。
                //     当某列射线恰好穿过共享边(或顶点)时，两个三角形均报告命中 → 同一表面被计 2 次，
                //     破坏"每穿过一个表面 c+1"的奇偶前提，使该列体内/体外整段翻转——
                //     表现为正方体模型对角线下方整列缺体素。
                //     合并几乎重合(z 差 << voxelSize)的相邻交点为一组(无论命中几次只记 1 次)，恢复正确奇偶。
                //     阈值远小于体素边长，真实薄壁的两个表面间距 ≥ voxelSize 量级，不会被误并。
                double mergeTol = Math.Max(voxelSize * 1e-4, 1e-7);
                int w = 0;                          // 去重后写入位置
                for (int r = 0; r < crossings.Count;)
                {
                    double cur = crossings[r];
                    int nxt = r + 1;
                    while (nxt < crossings.Count && crossings[nxt] - cur < mergeTol) nxt++;
                    crossings[w++] = cur;           // 一组重合交点仅保留一个
                    r = nxt;
                }
                crossings.RemoveRange(w, crossings.Count - w);

                // 4. 该列每个扫描体素：累计已越过的交点数 c，奇=体内/偶=体外(标准射线奇偶法)
                //    体素中心按 scan 递增，c 单调推进，O(count) 完成整列
                int c = 0;
                for (int isc = 0; isc < count[scan]; isc++)
                {
                    double centerS = origin[scan] + (isc + 0.5) * voxelSize;
                    while (c < crossings.Count && crossings[c] < centerS) c++;
                    idx[scan] = isc; idx[uA] = iu; idx[vA] = iv;
                    grid[idx[0], idx[1], idx[2]] = (c & 1) == 1 ? 1 : 0;
                }
            }
        }
        return grid;
    }

    /// <summary>
    /// Möller–Trumbore 射线-三角形相交(标量化、零堆分配)。
    /// 射线起点 (ox,oy,oz)、方向为 scan 轴单位向量；命中时 <paramref name="hitCoord"/> = 交点的 scan 轴坐标。
    /// 与 VoxelMapper.RayTriangleHit 同算法，自含一份以避免改动现有 private 可见性。
    /// </summary>
    private static bool RayHitAxis(double ox, double oy, double oz, int scan,
        VoxelMapper.StlTriangle tri, out double hitCoord)
    {
        hitCoord = 0;
        // 方向向量 D：scan 分量=1，其余=0
        double dx = 0, dy = 0, dz = 0;
        if (scan == 0) dx = 1.0; else if (scan == 1) dy = 1.0; else dz = 1.0;

        double e1x = tri.Bx - tri.Ax, e1y = tri.By - tri.Ay, e1z = tri.Bz - tri.Az;
        double e2x = tri.Cx - tri.Ax, e2y = tri.Cy - tri.Ay, e2z = tri.Cz - tri.Az;

        // p = D × e2 ; det = e1 · p
        double px = dy * e2z - dz * e2y;
        double py = dz * e2x - dx * e2z;
        double pz = dx * e2y - dy * e2x;
        double det = e1x * px + e1y * py + e1z * pz;
        if (Math.Abs(det) < 1e-12) return false;        // 射线与三角面平行
        double inv = 1.0 / det;

        // u = (s · p) · inv,  s = O - A
        double sx = ox - tri.Ax, sy = oy - tri.Ay, sz = oz - tri.Az;
        double u = (sx * px + sy * py + sz * pz) * inv;
        if (u < -1e-9 || u > 1 + 1e-9) return false;

        // v = (D · q) · inv,  q = s × e1
        double qx = sy * e1z - sz * e1y;
        double qy = sz * e1x - sx * e1z;
        double qz = sx * e1y - sy * e1x;
        double v = (dx * qx + dy * qy + dz * qz) * inv;
        if (v < -1e-9 || u + v > 1 + 1e-9) return false;

        double t = (e2x * qx + e2y * qy + e2z * qz) * inv;
        if (t <= EPS) return false;                      // 仅取正向交点

        // 交点 = O + t·D，D 的 scan 分量为 1，故 scan 坐标 = O[scan] + t
        hitCoord = (scan == 0 ? ox : (scan == 1 ? oy : oz)) + t;
        return true;
    }

    /// <summary>
    /// 导出体素栅格为 CSV(对齐示例格式)：4 行 # 注释头 + 表头 + 全部体素(含 Value=0)。
    /// 列顺序为 Z 层 → Y 行 → X 列；坐标为体素中心。
    /// </summary>
    /// <returns>是否写入成功。</returns>
    public static bool ExportVoxelCsv(VoxelGrid grid, string path)
    {
        try
        {
            if (grid == null || grid.TotalCount == 0) return false;

            var lines = new List<string>(grid.TotalCount + 5);
            // 注释头：ModelSizeX/Y/Z = 体素个数；VoxelSize = 边长(mm)
            lines.Add("# ModelSizeX=" + grid.CountX.ToString(Inv));
            lines.Add("# ModelSizeY=" + grid.CountY.ToString(Inv));
            lines.Add("# ModelSizeZ=" + grid.CountZ.ToString(Inv));
            lines.Add("# VoxelSize=" + grid.VoxelSize.ToString("0.######", Inv));
            lines.Add("X,Y,Z,Value");

            double s = grid.VoxelSize;
            string fmt = "0.####";
            for (int iz = 0; iz < grid.CountZ; iz++)
            {
                double z = grid.OriginZ + (iz + 0.5) * s;
                for (int iy = 0; iy < grid.CountY; iy++)
                {
                    double y = grid.OriginY + (iy + 0.5) * s;
                    for (int ix = 0; ix < grid.CountX; ix++)
                    {
                        double x = grid.OriginX + (ix + 0.5) * s;
                        int val = grid[ix, iy, iz];
                        lines.Add(x.ToString(fmt, Inv) + "," +
                                  y.ToString(fmt, Inv) + "," +
                                  z.ToString(fmt, Inv) + "," +
                                  val.ToString(Inv));
                    }
                }
            }
            File.WriteAllLines(path, lines, Encoding.UTF8);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 把体素栅格转为 <see cref="VoxelMapper.VoxelData"/>（Z 层 RLE 矩阵），
    /// 供 Viewport3D.SetVoxelData 直接渲染体素点云。元数据(Origin/Step/ColCount/RowCount)同步填充。
    /// </summary>
    public static VoxelMapper.VoxelData ToVoxelData(VoxelGrid grid)
    {
        var data = new VoxelMapper.VoxelData { SourcePath = grid?.SourcePath ?? "" };
        if (grid == null) return data;

        data.OriginX = grid.OriginX; data.OriginY = grid.OriginY; data.OriginZ = grid.OriginZ;
        data.StepX = grid.VoxelSize; data.StepY = grid.VoxelSize; data.StepZ = grid.VoxelSize;
        data.ColCount = grid.CountX; data.RowCount = grid.CountY;

        for (int iz = 0; iz < grid.CountZ; iz++)
        {
            var rows = new List<List<Pixcel>>(grid.CountY);
            for (int iy = 0; iy < grid.CountY; iy++)
            {
                var seq = new int[grid.CountX];
                for (int ix = 0; ix < grid.CountX; ix++)
                    seq[ix] = grid[ix, iy, iz];
                rows.Add(EncodeRle(seq));
            }
            data.Matrix.Add(rows);
        }
        return data;
    }

    /// <summary>一维 0/1 序列 RLE 编码(与 VoxelMapper.EncodeLayerRLE 同语义：Depth=材料值, Count=游程长)。</summary>
    private static List<Pixcel> EncodeRle(int[] seq)
    {
        var pixels = new List<Pixcel>();
        if (seq == null || seq.Length == 0) return pixels;

        int runVal = seq[0];
        int runCnt = 1;
        for (int i = 1; i < seq.Length; i++)
        {
            if (seq[i] == runVal) runCnt++;
            else { pixels.Add(new Pixcel(runVal, runCnt)); runVal = seq[i]; runCnt = 1; }
        }
        pixels.Add(new Pixcel(runVal, runCnt));
        return pixels;
    }
}
