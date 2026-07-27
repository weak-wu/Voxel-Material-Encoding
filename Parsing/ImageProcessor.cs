using System.Drawing;
using System.Drawing.Drawing2D;
using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// 图片处理：自 AMCP.FrmPrintStep2 的图片流程移植。
/// 把位图按亮度二值化为 RLE 像素矩阵（depth 0=白/1=黑），供 Picture2Gcode 生成栅格路径。
/// </summary>
public static class ImageProcessor
{
    /// <summary>高质量双线性缩放位图到目标尺寸。自 FrmPrintStep2.ResizeImage 移植。</summary>
    public static Bitmap ResizeImage(Bitmap source, int targetWidth, int targetHeight,
        InterpolationMode mode = InterpolationMode.HighQualityBilinear)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (targetHeight <= 0 || targetWidth <= 0) throw new ArgumentException("目标宽高必须大于0");

        var result = new Bitmap(targetWidth, targetHeight);
        result.SetResolution(source.HorizontalResolution, source.VerticalResolution);
        using var g = Graphics.FromImage(result);
        g.InterpolationMode = mode;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(source, new Rectangle(0, 0, targetWidth, targetHeight),
            new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
        return result;
    }

    /// <summary>
    /// 位图 → 二值 RLE 像素矩阵。逐行按亮度(<128=黑)做游程编码。
    /// 自 FrmPrintStep2.ProcessImage 移植。返回 [Y行][RLE段]。
    /// </summary>
    public static List<List<Pixcel>> ProcessImage(Bitmap bitmap)
    {
        var matrix = new List<List<Pixcel>>();
        if (bitmap == null) return matrix;

        int width = bitmap.Width, height = bitmap.Height;
        for (int y = 0; y < height; y++)
        {
            var pixels = new List<Pixcel>();
            int counts = 0;
            bool isblack = false, iswhite = false;

            for (int x = 0; x < width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                int bitdepth = (int)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);

                if (bitdepth < 128) // 黑
                {
                    if (iswhite) { pixels.Add(new Pixcel(0, counts)); counts = 0; }
                    if (!isblack) { isblack = true; iswhite = false; }
                    counts++;
                }
                else // 白
                {
                    if (isblack) { pixels.Add(new Pixcel(1, counts)); counts = 0; }
                    if (!iswhite) { iswhite = true; isblack = false; }
                    counts++;
                }
            }
            // 行末收尾（与原实现一致：末像素黑→depth=1，白→depth=0）
            var last = bitmap.GetPixel(width - 1, y);
            int lastDepth = (int)(last.R * 0.3 + last.G * 0.59 + last.B * 0.11);
            pixels.Add(new Pixcel(lastDepth < 128 ? 1 : 0, counts));

            matrix.Add(new List<Pixcel>(pixels));
        }
        return matrix;
    }
}
