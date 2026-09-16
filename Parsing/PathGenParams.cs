using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// DirectGeneratePath 的工艺参数包。
/// 打包原本散落在 DirectGeneratePath 形参里的 11 个工艺量，并内聚步长计算与合法性校验，
/// 使各处理子步骤无需重复传递长参数列表。
/// </summary>
/// <remarks>
/// 材料 A = T0(Tool 0)、材料 B = T1(Tool 1)；各段步长按其所打印材料选择。
/// </remarks>
internal sealed class PathGenParams
{
    /// <summary>切入材料 A(T0) 的提前出丝距离(mm)——即 B→A 切换的提前量。</summary>
    public double AdvanceDis0 { get; set; }

    /// <summary>切入材料 B(T1) 的提前出丝距离(mm)——即 A→B 切换的提前量。</summary>
    public double AdvanceDis1 { get; set; }

    /// <summary>材料 A(T0) 打印速度(mm/s)。</summary>
    public double Velo0 { get; set; }

    /// <summary>材料 B(T1) 打印速度(mm/s)。</summary>
    public double Velo1 { get; set; }

    /// <summary>T0 切换速度(mm/s)。</summary>
    public double VChange0 { get; set; }

    /// <summary>T1 切换速度(mm/s)。</summary>
    public double VChange1 { get; set; }

    /// <summary>T0 变速距离(mm)。</summary>
    public double DisChange0 { get; set; }

    /// <summary>T1 变速距离(mm)。</summary>
    public double DisChange1 { get; set; }

    /// <summary>等时间采样周期(s，默认 0.02=50Hz)。各材料步长 = 速度 × dt。</summary>
    public double Dt { get; set; }

    /// <summary>是否启用实际提前切换点开始的 5% / 90% / 5% 折线变速。</summary>
    public bool EnableVeloChange { get; set; }

    /// <summary>材料 A(T0) 步长 = Velo0 × Dt。</summary>
    public double Step0 => Velo0 * Dt;

    /// <summary>材料 B(T1) 步长 = Velo1 × Dt。</summary>
    public double Step1 => Velo1 * Dt;

    /// <summary>按所打印材料选择步长：Tool 0→A 步长，其余→B 步长。</summary>
    public double StepOf(int tool) => tool == 0 ? Step0 : Step1;

    /// <summary>
    /// 合法性校验（自原 DirectGeneratePath 入口校验段逐行搬入）。
    /// 含历史冗余原样保留：dt≤0 时先静默修正为 0.02，随后 dt≤PathEps 的 throw 在修正后永不为真。
    /// </summary>
    /// <exception cref="ArgumentException">dt/正常速度非正、距离参数为负时抛出。</exception>
    public void Validate()
    {
        if (Dt <= 0) Dt = 0.02;                                   // 历史：静默修正采样周期
        if (Dt <= PathGenerator.PathEps)                          // 修正后永不为真，原样保留
            throw new ArgumentException("dt 必须大于 0。");
        if (Velo0 <= PathGenerator.PathEps || Velo1 <= PathGenerator.PathEps)
            throw new ArgumentException("材料正常速度必须大于 0。");
        if (AdvanceDis0 < 0 || AdvanceDis1 < 0 ||
            DisChange0 < 0 || DisChange1 < 0)
            throw new ArgumentException("距离参数不能为负数。");
        // 注：T0/T1 变速速度的合法性由 SearchPointBridge 内 v<=PathEps 时抛出兜底，此处不重复校验
    }
}

/// <summary>
/// 从实际提前切换点开始的变速区域（替代原匿名 ValueTuple）。
/// 区域 [ZStart, ZEnd] 锚定在提前区起点(实际提前点 advS)：ZStart=advS，ZEnd=advS+changeLength。
/// [ZStart, ZRampEnd] 速度由 VeloOld 渐变到 Vc，[ZRampEnd, ZHoldEnd] 保持 Vc，[ZHoldEnd, ZEnd] 由 Vc 渐变到 VeloNew。
/// </summary>
internal sealed record SpeedZone(
    double ZStart, double ZRampEnd, double ZHoldEnd, double ZEnd,
    double VeloOld, double Vc, double VeloNew, int ToolOld, int ToolNew);
