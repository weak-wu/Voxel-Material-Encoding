namespace GcodeViewer.Models;

/// <summary>单层统计，供层列表显示与 CSV 导出。</summary>
public sealed class LayerInfo
{
    public int Layer { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public double G0Length { get; set; }
    public double G1Length { get; set; }
    public int MoveCount { get; set; }
    public int ToolChangeCount { get; set; }
    public int G0G1SwitchCount { get; set; }
    public double TotalLength => G0Length + G1Length;
}
