namespace GcodeViewer.Models;

/// <summary>单层统计，供层列表显示与 CSV 导出。</summary>
public sealed class LayerInfo
{
    public int Layer { get; set; }
    /// <summary>该层的代表 Z 高度（mm）。CSV 按 Z 分层时为层锚点 Z；G-code（无 Z 映射）取该层首条 move 的 Z。</summary>
    public double Z { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public double G0Length { get; set; }
    public double G1Length { get; set; }
    public int MoveCount { get; set; }
    public int ToolChangeCount { get; set; }
    public int G0G1SwitchCount { get; set; }
    public double TotalLength => G0Length + G1Length;
}
