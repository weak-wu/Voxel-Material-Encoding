namespace GcodeViewer.Models;

/// <summary>
/// 源文件的一行，保留原始文本以便无损保存。
/// 若本行解析为一条 G0/G1 移动，Move 指向该对象（双向引用，编辑后同步重写文本）。
/// 注释/空行/其它指令的 Move 为 null。
/// </summary>
public sealed class GcodeLine
{
    public int LineNumber { get; set; }
    public string RawText { get; set; } = string.Empty;
    public GcodeMove? Move { get; set; }

    /// <summary>本行是否为注释（以 ; 开头或全注释）。</summary>
    public bool IsComment { get; set; }

    public GcodeLine(int lineNumber, string rawText)
    {
        LineNumber = lineNumber;
        RawText = rawText;
    }
}
