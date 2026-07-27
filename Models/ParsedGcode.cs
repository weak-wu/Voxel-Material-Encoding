namespace GcodeViewer.Models;

/// <summary>整个 gcode 文件的解析结果：源行列表 + move 列表 + 统计 + 包围盒。</summary>
public sealed class ParsedGcode
{
    public string SourcePath { get; set; } = string.Empty;
    public List<GcodeLine> Lines { get; } = new();
    public List<GcodeMove> Moves { get; } = new();
    public GcodeStats Stats { get; set; } = new();
    public BoundingBox Bounds { get; } = new();

    /// <summary>源文件是否含 ;LAYER 注释（层信息）。false 时无法按层识别，应退化为按材料(T0/T1)显示。</summary>
    public bool HasLayerInfo { get; set; }

    public bool IsEmpty => Moves.Count == 0;

    /// <summary>根据源文件行号查找对应 move（行号→move 二分/线性映射）。</summary>
    public GcodeMove? FindMoveByLine(int lineNumber)
    {
        foreach (var m in Moves)
            if (m.LineNumber == lineNumber) return m;
        return null;
    }
}
