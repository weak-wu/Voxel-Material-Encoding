using OpenCvSharp;

namespace GcodeViewer.Parsing;

/// <summary>
/// 打印线宽提取的纯图像处理管线（无 UI 依赖）：
/// HSV 颜色分割 → 形态学去噪 → 最大连通域 → fitLine 计算倾角并旋转到水平 → 逐列测上下边缘距离 → 平滑 → 统计。
/// 数学模型：
///   1) 颜色分割：在 HSV 空间用上下限做InRange，得二值掩膜 M0 = {p | Lo ≤ HSV(p) ≤ Hi}。
///   2) 倾角：对最大连通域的所有前景像素做最小二乘直线拟合（fitLine, L2），得方向 (vx,vy)；
///      令 θ = atan2(vy,vx)（图像坐标系，y 向下）。用 GetRotationMatrix2D(center, θ) 旋转，
///      可使该方向被映射为水平（见下方推导），即把线条校正到水平。
///   3) 线宽：旋转后的掩膜中，逐列 x 取最上/最下前景行 yTop、yBot，该列线宽(像素) = yBot - yTop + 1；
///      再按标定 px/mm 换算为毫米。
/// 推导（GetRotationMatrix2D 以 θ 为参数，α=cosθ, β=sinθ，变换方向为 (α·dx+β·dy, -β·dx+α·dy)）：
///   取 θ=atan2(vy,vx)、(dx,dy)=(vx,vy)，则 -β·dx+α·dy = -sinθ·vx+cosθ·vy = 0，旋转后方向为水平。
/// </summary>
public static class LineWidthMeasure
{
    /// <summary>管线输入参数（可变结构体，便于调用方逐字段赋值后以 in 传入）。</summary>
    public struct Params
    {
        /// <summary>HSV 下限 (H:0–180, S:0–255, V:0–255)。</summary>
        public Scalar Lo;
        /// <summary>HSV 上限 (H:0–180, S:0–255, V:0–255)。</summary>
        public Scalar Hi;
        /// <summary>形态学核边长(像素)；0=关闭，否则用 3/5/7。先开运算后闭运算。</summary>
        public int MorphKsize;
        /// <summary>连通域最小面积阈值(像素²)，小于此值的连通域视为噪点丢弃。</summary>
        public double MinAreaPx;
        /// <summary>标定：每毫米对应的像素数。线宽(mm)=线宽(px)/PxPerMm。</summary>
        public double PxPerMm;
        /// <summary>线宽曲线滑动平均窗口(列数)；0=不平滑。</summary>
        public int SmoothWindow;
    }

    /// <summary>测量结果：曲线、统计量与各阶段掩膜（Mat 由调用方 Dispose，或用 Dispose()）。</summary>
    public sealed class Result : IDisposable
    {
        /// <summary>检测到的线条倾角(度，归一化到[-90,90])。</summary>
        public double AngleDeg;
        /// <summary>沿线条方向的列位置(mm)，与 WidthMm 一一对应。</summary>
        public double[] PosMm = Array.Empty<double>();
        /// <summary>逐列线宽(mm)；间隙列记 NaN。</summary>
        public double[] WidthMm = Array.Empty<double>();
        /// <summary>平滑后的线宽(mm)；SmoothWindow=0 时同 WidthMm。</summary>
        public double[] WidthSmoothedMm = Array.Empty<double>();
        public double Mean, Std, Min, Max;       // 对非 NaN 线宽统计
        public double Cv;                         // 变异系数 = Std/Mean
        public int ValidCount;                    // 非 NaN 列数
        /// <summary>每列线宽上下边缘在「旋转后掩膜」中的行号(像素)；间隙列为 -1。供交互探查绘制测宽线段。</summary>
        public int[] RowTopPx = Array.Empty<int>();
        public int[] RowBotPx = Array.Empty<int>();

        public Mat? MaskRaw;        // HSV 分割原始掩膜
        public Mat? MaskLargest;    // 最大连通域掩膜
        public Mat? RotatedMask;    // 旋转到水平后的掩膜（用于测宽与预览）
        public Mat? RotatedBgr;     // 旋转到水平后的原图（预览用）

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            MaskRaw?.Dispose(); MaskLargest?.Dispose(); RotatedMask?.Dispose(); RotatedBgr?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// 运行完整管线。bgr 不会被释放（调用方持有）。返回 null 表示未分割到 ≥ MinAreaPx 的连通域。
    /// 异常：参数非法或图像为空时抛 ArgumentException。
    /// </summary>
    public static Result? Run(Mat bgr, in Params p)
    {
        if (bgr == null || bgr.Empty())
            throw new ArgumentException("图像为空。", nameof(bgr));
        if (bgr.Channels() < 3)
            throw new ArgumentException("需要 3 通道 BGR 彩色图像。", nameof(bgr));
        if (p.PxPerMm <= 0)
            throw new ArgumentException("PxPerMm 必须为正。", nameof(p));

        var result = new Result();
        // 临时 Mats 用 using 自动释放；返回用的 4 个 Mat 由 result 持有。
        Mat? hsv = null, mask = null, kernel = null, labels = null, stats = null, centroids = null;
        Mat? maskLargest = null, rotM = null;
        try
        {
            // 1) HSV 分割
            hsv = new Mat();
            Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
            mask = new Mat();
            Cv2.InRange(hsv, p.Lo, p.Hi, mask);
            result.MaskRaw = mask.Clone(); // 保留原始掩膜预览
            hsv.Dispose(); hsv = null;

            // 2) 形态学去噪（先开运算去毛刺，再闭运算填小洞）
            if (p.MorphKsize > 0)
            {
                int k = p.MorphKsize % 2 == 1 ? p.MorphKsize : p.MorphKsize + 1; // 核边长需为奇数
                kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(k, k));
                Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
                Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
                kernel.Dispose(); kernel = null;
            }

            // 3) 连通域：取面积 ≥ MinAreaPx 的最大连通域
            labels = new Mat();
            stats = new Mat();
            centroids = new Mat();
            int n = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids,
                PixelConnectivity.Connectivity8, MatType.CV_32S);
            int best = -1;
            double bestArea = 0;
            const int ccArea = (int)ConnectedComponentsTypes.Area; // CC_STAT_AREA 列
            for (int i = 1; i < n; i++) // 0=背景
            {
                double a = stats.At<int>(i, ccArea);
                if (a >= p.MinAreaPx && a > bestArea) { bestArea = a; best = i; }
            }
            if (best < 0) return null; // 无合格连通域

            maskLargest = new Mat();
            Cv2.Compare(labels, new Scalar(best), maskLargest, CmpTypes.EQ); // 命中=255
            result.MaskLargest = maskLargest.Clone();
            labels.Dispose(); labels = null;
            stats.Dispose(); stats = null;
            centroids.Dispose(); centroids = null;

            // 4) 倾角：用图像矩的主轴方向求线条方向角（对噪声稳健，且无 FitLine 重载差异）。
            //    θ = 0.5·atan2(2·μ11, μ20−μ02) 为图像坐标系(原点左上、y 向下)下主轴与 +x 轴夹角(度)，
            //    天然落在 [-90,90]；方向 180° 歧义不影响水平化。推导见文件头：把 θ 直接交给 GetRotationMatrix2D。
            var mu = Cv2.Moments(maskLargest, true);
            double denom = mu.Mu20 - mu.Mu02;   // 必须用「中心矩」μ20/μ02/μ11，而非原始矩 m_ij(后者含质心位置，会污染角度)
            double angleDeg = (denom == 0 && mu.Mu11 == 0)
                ? 0
                : 0.5 * Math.Atan2(2 * mu.Mu11, denom) * 180.0 / Math.PI;
            result.AngleDeg = angleDeg;

            // 5) 旋转到水平（掩膜与原图同矩阵）
            var center = new Point2f((bgr.Width - 1) * 0.5f, (bgr.Height - 1) * 0.5f);
            rotM = Cv2.GetRotationMatrix2D(center, angleDeg, 1.0);
            var dsize = new OpenCvSharp.Size(bgr.Width, bgr.Height);
            var rotatedMask = new Mat();
            Cv2.WarpAffine(maskLargest, rotatedMask, rotM, dsize,
                InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(0));
            result.RotatedMask = rotatedMask;
            var rotatedBgr = new Mat();
            Cv2.WarpAffine(bgr, rotatedBgr, rotM, dsize,
                InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(0, 0, 0));
            result.RotatedBgr = rotatedBgr;
            rotM.Dispose(); rotM = null;

            // 6) 逐列测宽：旋转后掩膜每列最上/最下前景行距 = 线宽(px)；并记录上下边缘行号供交互探查
            int W = rotatedMask.Width, H = rotatedMask.Height;
            rotatedMask.GetArray(out byte[] px); // 行主序一维，长度 H*W
            var widthPx = new double[W];
            var rowTop = new int[W];
            var rowBot = new int[W];
            for (int x = 0; x < W; x++)
            {
                int yTop = -1, yBot = -1;
                for (int y = 0; y < H; y++)
                {
                    if (px[y * W + x] != 0)
                    {
                        if (yTop < 0) yTop = y;
                        yBot = y;
                    }
                }
                widthPx[x] = (yTop < 0) ? double.NaN : (yBot - yTop + 1);
                rowTop[x] = yTop;
                rowBot[x] = yBot;
            }
            result.RowTopPx = rowTop;
            result.RowBotPx = rowBot;

            // 7) 换算到 mm + 位置(mm)
            var posMm = new double[W];
            var widthMm = new double[W];
            for (int x = 0; x < W; x++)
            {
                posMm[x] = x / p.PxPerMm;
                widthMm[x] = double.IsNaN(widthPx[x]) ? double.NaN : widthPx[x] / p.PxPerMm;
            }
            result.PosMm = posMm;
            result.WidthMm = widthMm;

            // 8) 平滑（滑动平均，跳过 NaN）
            result.WidthSmoothedMm = Smooth(widthMm, Math.Max(0, p.SmoothWindow));

            // 9) 统计（仅非 NaN）
            ComputeStats(widthMm, result);
            return result;
        }
        finally
        {
            // 释放本方法内的临时 Mat；返回用的 4 个已在 result 中，不在此释放
            hsv?.Dispose(); mask?.Dispose(); kernel?.Dispose();
            labels?.Dispose(); stats?.Dispose(); centroids?.Dispose();
            maskLargest?.Dispose(); rotM?.Dispose();
        }
    }

    /// <summary>居中滑动平均；window≤1 或全 NaN 时原样返回拷贝。NaN 不参与求平均。</summary>
    private static double[] Smooth(double[] src, int window)
    {
        int n = src.Length;
        if (window <= 1 || n == 0) return (double[])src.Clone();
        int half = window / 2;
        var dst = new double[n];
        for (int i = 0; i < n; i++)
        {
            if (double.IsNaN(src[i])) { dst[i] = double.NaN; continue; }
            double sum = 0; int cnt = 0;
            for (int k = -half; k <= half; k++)
            {
                int j = i + k;
                if (j < 0 || j >= n) continue;
                if (double.IsNaN(src[j])) continue;
                sum += src[j]; cnt++;
            }
            dst[i] = cnt > 0 ? sum / cnt : double.NaN;
        }
        return dst;
    }

    /// <summary>对非 NaN 值计算 Mean/Std/Min/Max/Cv，写入 result。</summary>
    private static void ComputeStats(double[] v, Result result)
    {
        double sum = 0, sq = 0, mn = double.PositiveInfinity, mx = double.NegativeInfinity;
        int cnt = 0;
        for (int i = 0; i < v.Length; i++)
        {
            if (double.IsNaN(v[i])) continue;
            double x = v[i];
            sum += x; sq += x * x;
            if (x < mn) mn = x;
            if (x > mx) mx = x;
            cnt++;
        }
        result.ValidCount = cnt;
        if (cnt == 0)
        {
            result.Mean = result.Std = result.Min = result.Max = 0; result.Cv = 0;
            return;
        }
        double mean = sum / cnt;
        double var = sq / cnt - mean * mean;
        if (var < 0) var = 0;
        result.Mean = mean;
        result.Std = Math.Sqrt(var);
        result.Min = mn;
        result.Max = mx;
        result.Cv = mean > 0 ? result.Std / mean : 0;
    }
}
