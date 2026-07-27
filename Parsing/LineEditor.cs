using System.Globalization;
using System.Text.RegularExpressions;
using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// 把对 GcodeMove 的修改同步回 GcodeLine.RawText，仅替换指令 token 或单个轴数值，
/// 其余文本（注释、其它轴、空格）原样保留，从而实现无损编辑/保存。
/// </summary>
public static class LineEditor
{
    // 行首 G 指令词：匹配 G0/G1（含前导零 G00/G01，无空格 G0X..）。
    // 不加 \b，翻转时统一归一化为 G0/G1。
    private static readonly Regex GWordRegex = new(@"(?i)^G\d+");
    private static readonly Regex AxisRegex =
        new(@"(?i)(?<![A-Za-z])([XYZ])(\s*[-+]?\d*\.?\d+)");

    /// <summary>翻转 G0↔G1，保留坐标与注释原样。兼容 G00/G01 写法，归一化为 G0/G1。</summary>
    public static void ApplyTypeChange(GcodeLine line, MoveType newType)
    {
        if (line.Move == null) return;
        var oldWord = line.Move.Type == MoveType.G0 ? "G0" : "G1";
        var newWord = newType == MoveType.G0 ? "G0" : "G1";
        if (oldWord == newWord) return;

        // 替换行首指令词（G00→G0、G01→G1，统一归一化）
        line.RawText = GWordRegex.Replace(line.RawText, newWord, 1);
        line.Move.Type = newType;
        line.Move.IsModified = true;
    }

    /// <summary>修改某个轴的数值（仅当该轴在原行内显式存在）。保留其它文本原样。</summary>
    public static bool ApplyAxisChange(GcodeLine line, char axis, double newValue)
    {
        if (line.Move == null) return false;
        axis = char.ToUpperInvariant(axis);

        bool replaced = false;
        line.RawText = AxisRegex.Replace(line.RawText, m =>
        {
            if (replaced) return m.Value;
            if (char.ToUpperInvariant(m.Groups[1].Value[0]) == axis)
            {
                replaced = true;
                return axis + newValue.ToString("0.######", CultureInfo.InvariantCulture);
            }
            return m.Value;
        }, 1);

        if (!replaced)
        {
            // 原行没有该轴，追加到指令词后
            string val = newValue.ToString("0.######", CultureInfo.InvariantCulture);
            line.RawText = GWordRegex.Replace(line.RawText, w => w.Value + " " + axis + val, 1);
            replaced = true;
        }

        switch (axis)
        {
            case 'X': line.Move.X = newValue; line.Move.HasX = true; break;
            case 'Y': line.Move.Y = newValue; line.Move.HasY = true; break;
            case 'Z': line.Move.Z = newValue; line.Move.HasZ = true; break;
        }
        line.Move.IsModified = true;
        return true;
    }
}
