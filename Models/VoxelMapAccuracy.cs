using System.Globalization;

namespace GcodeViewer.Models;

/// <summary>
/// 体素映射精度评估的数据类型（自 AMCP.FrmPrintStep2 的 VoxelMapAccuracyReport / VoxelMapErrorRecord 移植）。
/// 评估对象：映射后路径中每个节点/采样点携带的 Tool 值（=映射得到的体素深度 d_pred），
/// 相对于"该位置在原体素文件中的真实深度 d_true"的吻合度。
/// </summary>
public enum VoxelMapSampleMode
{
    /// <summary>取采样点所属子段起点 a 的 Tool —— 与"映射后路径 GCode 指令"语义一致（Tool 在节点处切换），推荐。</summary>
    Stepped,
    /// <summary>在相邻映射节点间对 Tool 做线性插值，将路径视为连续深度场的线性近似，给出误差上界。</summary>
    Linear,
}

/// <summary>单条误差记录（供 CSV 导出 / Origin 绘图）。</summary>
public sealed class VoxelMapErrorRecord
{
    public double X, Y;         // 采样点坐标(mm)
    public int Layer;           // 路径层号
    public int VoxelLayerIdx;   // 对应体素层索引
    public int DPred;           // 映射深度(Tool)
    public int DTrue;           // 真实体素深度
    public int Error;           // = DPred - DTrue
}

/// <summary>体素映射精度评估报告。</summary>
public sealed class VoxelMapAccuracyReport
{
    // ---- 节点级（实现正确性自检，理论上应 100% 匹配）----
    public int NodeCount;            // 映射后路径节点总数
    public int NodeExactMatch;       // d_pred == d_true 的节点数
    public double NodeExactRate;     // 节点精确匹配率 = NodeExactMatch/NodeCount
    public double NodeMAE;           // 节点平均绝对误差 |d_pred - d_true|
    public double NodeMaxAE;         // 节点最大绝对误差
    public double NodeStdAE;         // 节点绝对误差标准差

    // ---- 路径级（沿折线密集采样，反映打印实际精度）----
    public int SampleCount;          // 采样点总数
    public double SampleSpacing;     // 采样间距 Δs (mm)
    public int SampleExactMatch;     // d_pred == d_true 的采样点数
    public double SampleExactRate;   // 采样精确匹配率
    public double SampleMAE;         // 采样平均绝对误差
    public double SampleRMSE;        // 采样均方根误差
    public double SampleMaxAE;       // 采样最大绝对误差

    // ---- 误差分桶（基于采样点 |err|，供 Origin 绘直方图）----
    public int[]? ErrBins;           // 顺序: [=0, (0,1], (1,2], (2,3], (3,5], (5,10], (>10]
    public static readonly string[] ErrBinLabels =
        { "=0", "(0,1]", "(1,2]", "(2,3]", "(3,5]", "(5,10]", ">10" };

    // ---- 体素覆盖统计 ----
    public int CoveredCells;         // 路径覆盖到的不同体素单元数
    public int CoveredNonZeroCells;  // 其中真实 depth>0 的单元数
    public double MaterialCoverRate; // = CoveredNonZeroCells/CoveredCells

    // ---- 层映射诊断（精度损失来源）----
    public int PathLayerCount;       // 路径层数
    public int VoxelLayerCount;      // 体素层数
    public int SharedVoxelLayerMax;  // 按 Z 定层后：路径实际命中的不同体素层数(Z 向利用率诊断；原"共享层"语义已废)

    // ---- 边界 / 切换点专项（多材料交界处精度，对应"映射方法对比"核心指标）----
    // 边界样本 B：沿路径密集采样时，真实材料 d_true 发生 0↔1 过渡的采样点（含过渡点及其前一邻点，构成边界带）。
    public int BoundarySampleCount;     // 边界样本数 |B|
    public double BoundaryMAE;          // 边界平均绝对误差 = mean_{k∈B} |d_pred − d_true|（边界定位精度）
    public double BoundaryRMSE;         // 边界均方根误差 = sqrt(mean_{k∈B}(d_pred−d_true)²)（边界锯齿程度）

    // 切换点集 S：映射输出中 Tool 值发生变化的节点（即下达材料切换指令的位置）。
    public int SwitchPointCount;        // 切换点数 |S|
    public int SwitchPointExactMatch;   // 切换点处 d_pred==d_true 的数量
    public double SwitchPointMAE;       // 切换点处平均绝对误差 = mean_{i∈S} |d_pred − d_true|
    public double SwitchPointExactRate; // 切换点正确率 = SwitchPointExactMatch / |S|

    // ---- 位置偏差（连续几何精度，主评价指标；S↔T 双向配对）----
    // 理论切换点 T：真实材料场 d_true 沿路径发生 0↔1 过渡的位置（材料边界与路径交点）。
    // 实际切换点 S：映射输出中 Tool 变化的节点（下达切换指令的位置）。
    //
    // 切换点位置偏差(S→T)：每个实际切换点 → 同层最近理论切换点的几何距离(mm)。
    //   衡量"切换指令定位精度"——下达的每条切换指令离理想边界有多远。
    // 材料位置偏差(T→S)：每个理论切换点 → 同层最近实际切换点的几何距离(mm)。
    //   衡量"材料边界完整性"——每条真实材料边界是否被某条切换指令捕捉到(漏切/多切都会拉大)。
    // 两者构成几何意义上的"精度/召回"配对：单元法 |S|≈|T| 时数值接近，但仍会因漏切/多切而分化。
    public int TheoreticalSwitchCount;    // 理论切换点数 |T|
    public double SwitchPointPosMAE;      // 切换点位置偏差：实际→理论 最近距离的平均值 (mm)
    public double SwitchPointPosRMSE;     // 切换点位置均方根距离误差 (mm)
    public double SwitchPointPosMax;      // 切换点位置最大距离误差 (mm)
    public double MaterialPosMAE;         // 材料位置偏差：理论→实际 最近距离的平均值 (mm)
    public double MaterialPosRMSE;        // 材料位置均方根距离误差 (mm)
    public double MaterialPosMax;         // 材料位置最大距离误差 (mm)

    // ---- 覆盖完整率（召回率 Recall：真实需打印区域被路径覆盖到的比例）----
    public int TruePositiveCells;       // 路径涉及体素层中真实有材料(d_true>0)单元总数（召回率分母）
    public double CoverageRecall;       // 覆盖完整率 = CoveredNonZeroCells / TruePositiveCells

    // ---- 误差明细（采样点级，供导出）----
    public List<VoxelMapErrorRecord> ErrorRecords = new();

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public override string ToString()
    {
        string bins = "";
        if (ErrBins != null)
        {
            for (int i = 0; i < ErrBins.Length; i++)
                bins += string.Format(Inv, "    |err| {0,-7}: {1,8}  ({2:F2}%)\n",
                    ErrBinLabels[i], ErrBins[i],
                    SampleCount > 0 ? 100.0 * ErrBins[i] / SampleCount : 0);
        }
        string baseText = string.Format(Inv,
            "[节点级] 共 {0} 点, 精确匹配 {1} ({2:F2}%), MAE={3:F3}, MaxAE={4:F0}, Std={5:F3}\n" +
            "[路径级] 采样 {6} 点 (Δs={7:F4} mm), 匹配 {8} ({9:F2}%), MAE={10:F3}, RMSE={11:F3}, MaxAE={12:F0}\n" +
            "[覆盖  ] 体素单元 {13}, 有材料 {14} ({15:F1}%)\n" +
            "[层映射] 路径层 {16} / 体素层 {17}, Z 向最大共用 {18} 层\n" +
            "[误差分桶]\n{19}",
            NodeCount, NodeExactMatch, NodeExactRate * 100, NodeMAE, NodeMaxAE, NodeStdAE,
            SampleCount, SampleSpacing, SampleExactMatch, SampleExactRate * 100, SampleMAE, SampleRMSE, SampleMaxAE,
            CoveredCells, CoveredNonZeroCells, MaterialCoverRate * 100,
            PathLayerCount, VoxelLayerCount, SharedVoxelLayerMax, bins);

        // 主指标（连续几何精度·S↔T 双向配对）
        string primary = string.Format(Inv,
            "[切换位置·主] 理论 {0} 点 / 实际 {1} 点, 切换点位置偏差 MAE={2:F4} / RMSE={3:F4} / Max={4:F4} mm\n" +
            "[材料位置·主] 材料位置偏差(理论→实际) MAE={5:F4} / RMSE={6:F4} / Max={7:F4} mm\n",
            TheoreticalSwitchCount, SwitchPointCount,
            SwitchPointPosMAE, SwitchPointPosRMSE, SwitchPointPosMax,
            MaterialPosMAE, MaterialPosRMSE, MaterialPosMax);

        // 辅助指标（离散一致性）：边界 / 切换点Tool值 / 覆盖
        string extra = string.Format(Inv,
            "[边界  ] 样本 {0}, MAE={1:F3}, RMSE={2:F3} (锯齿程度)\n" +
            "[切换点] 共 {3} 点, 正确 {4} ({5:F2}%), Tool偏差MAE={6:F3}\n" +
            "[覆盖率] 有材料单元 {7}, 已覆盖 {8}, 完整率 {9:F2}%\n",
            BoundarySampleCount, BoundaryMAE, BoundaryRMSE,
            SwitchPointCount, SwitchPointExactMatch, SwitchPointExactRate * 100, SwitchPointMAE,
            TruePositiveCells, CoveredNonZeroCells, CoverageRecall * 100);
        return primary + baseText + extra;
    }
}
