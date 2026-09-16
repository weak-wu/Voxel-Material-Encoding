using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// 按 Z 坐标把无层信息的路径（典型为 CSV）切分为离散层，给每条 move 打上合成层号。
/// 复用下游全部按层逻辑（层列表 / 层过滤 / 按层着色），使 CSV 也能逐层查看。
///
/// 算法（频率加权聚类，对密集采样稳健）：
/// 1) 把 Z 四舍五入到 1e-4 mm 分组计数，消除浮点噪声。
/// 2) 出现频次 ≥ 阈值（max(2, 总点数×0.2%)）的 Z 视为「层锚点」——
///    真实层在恒定 Z 上打印大量点；层间过渡只有少量点，频次低被滤除。
/// 3) 锚点按 Z 升序编号（0,1,2,…）即为合成层号；每条 move（含过渡点）归到最近锚点。
/// 4) 退化：若无频次锚点（连续渐变 Z 或点极稀疏），按绝对容差把 Z 切成 ≤约 100 片；
///    全部同 Z 则归为单层。
/// </summary>
public static class ZLayerDetector
{
    /// <summary>Z 分组前的四舍五入精度（mm，0.1µm）——远小于真实层厚，仅用于消除浮点噪声。</summary>
    private const double ZRound = 1e-4;

    /// <summary>
    /// 把 moves 按 Z 聚类为离散层：原地写回 <paramref name="moves"/> 中每条的 Layer，
    /// 并把「层号 → 代表 Z」填入 <paramref name="layerZ"/>。返回层个数；空 moves 返回 0。
    /// </summary>
    public static int AssignLayers(List<GcodeMove> moves, Dictionary<int, double> layerZ)
    {
        layerZ.Clear();
        if (moves == null || moves.Count == 0) return 0;

        // 1) Z 四舍五入分组计数（键 = 归一化后的 Z，值 = 该 Z 上的点数）
        var groups = new Dictionary<double, int>();
        foreach (var m in moves)
        {
            double rz = Math.Round(m.Z / ZRound) * ZRound;
            groups[rz] = groups.TryGetValue(rz, out int c) ? c + 1 : 1;
        }

        var sortedZ = groups.Keys.OrderBy(z => z).ToList();

        // 全部同 Z：单层
        if (sortedZ.Count == 1)
        {
            foreach (var m in moves) m.Layer = 0;
            layerZ[0] = sortedZ[0];
            return 1;
        }

        // 2) 频次锚点：点数达阈值的 Z 才算真实层
        int threshold = Math.Max(2, (int)Math.Ceiling(moves.Count * 0.002));
        var anchors = sortedZ.Where(z => groups[z] >= threshold).ToList();

        // 3) 退化：无频次锚点（连续渐变 Z 或点极稀疏）→ 按绝对容差切片
        if (anchors.Count == 0)
            return AssignByTolerance(moves, sortedZ, layerZ);

        // 4) 锚点升序编号；每条 move 归到 Z 最近的锚点
        for (int i = 0; i < moves.Count; i++)
            moves[i].Layer = NearestAnchorIndex(anchors, moves[i].Z);

        for (int i = 0; i < anchors.Count; i++)
            layerZ[i] = anchors[i];
        return anchors.Count;
    }

    /// <summary>
    /// 退化路径：无明显离散层（连续渐变 Z），按绝对容差把 [zMin, zMax] 切成 ≤约 100 个连续层片，
    /// 层号压缩为 0..k-1，代表 Z 取各片中点。保证层号连续，滑块不会出现空洞。
    /// </summary>
    private static int AssignByTolerance(List<GcodeMove> moves, List<double> sortedZ, Dictionary<int, double> layerZ)
    {
        double zMin = sortedZ[0];
        double zMax = sortedZ[^1];
        double tol = Math.Max((zMax - zMin) / 100.0, ZRound);

        // 先按容差得到原始片号 floor((Z - zMin) / tol)
        var rawIdx = new int[moves.Count];
        for (int i = 0; i < moves.Count; i++)
            rawIdx[i] = (int)Math.Floor((moves[i].Z - zMin) / tol);

        // 压缩为连续层号；同时记录每层代表 Z（片中点）
        var compact = rawIdx.Distinct().OrderBy(v => v).ToList();
        var remap = new Dictionary<int, int>(compact.Count);
        for (int i = 0; i < compact.Count; i++)
        {
            remap[compact[i]] = i;
            layerZ[i] = zMin + (compact[i] + 0.5) * tol;
        }
        for (int i = 0; i < moves.Count; i++)
            moves[i].Layer = remap[rawIdx[i]];
        return compact.Count;
    }

    /// <summary>在升序锚点列表中返回与 z 最近的锚点下标（二分）。</summary>
    private static int NearestAnchorIndex(List<double> anchors, double z)
    {
        // 边界：z 落在首/末锚点之外，直接取端点
        if (z <= anchors[0]) return 0;
        if (z >= anchors[^1]) return anchors.Count - 1;

        // 二分找首个 ≥ z 的位置 lo，最近者必为 lo-1 或 lo
        int lo = 0, hi = anchors.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (anchors[mid] < z) lo = mid + 1;
            else hi = mid;
        }
        return (z - anchors[lo - 1] <= anchors[lo] - z) ? lo - 1 : lo;
    }
}
