using System.Globalization;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenCvSharp;
using GcodeViewer.Parsing;

namespace GcodeViewer.Forms;

/// <summary>
/// 打印线宽提取窗体：导入打印线条照片 → HSV 颜色分割 → 形态学去噪 → 取最大连通域 →
/// 用图像矩主轴自动计算倾角并旋转到水平 → 逐列测量上下边缘距离得到线宽 →
/// 用 ScottPlot 折线图展示沿线线宽波动，并给出均值/标准差/CV 等统计量。
/// 处理管线见 <see cref="LineWidthMeasure"/>；控件/布局/事件见 LineWidthForm.Designer.cs。
/// </summary>
public sealed partial class LineWidthForm : Form
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private Mat? _bgr;              // 原图（BGR），加载后常驻，关闭时释放
    private LineWidthMeasure.Result? _last;  // 最近一次测量结果（持有其中的 Mat）
    private Bitmap? _previewBmp;    // 当前预览位图（切换预览/重算时释放旧的）
    private bool _measuring;        // 重入保护（参数滚动期间可能连续触发）

    // 极值标记：在「旋转后」预览上标出最细/最粗线宽所在列
    private int _minCol = -1, _maxCol = -1;
    private double _minW, _maxW;
    // 折线图点击探查的列号（-1=无）
    private int _probeCol = -1;
    // 当前预览图像尺寸（供 Paint 叠加层做 Zoom 坐标换算）
    private int _previewW, _previewH;
    // 手动两点测量
    private bool _manualMode;
    private int _manualStep;            // 0=待点第一点, 1=已点第一点, 2=两点完成
    private PointF _manualA, _manualB;
    private double _manualPx;

    // 框选取线（ROI）：图片中有多条线时框选要测量的那条
    private bool _roiDrawing;           // 正在拖画选框
    private bool _roiDragStarted;       // 本轮拖画是否已开始(移动超阈值)，用于区分单击/拖画
    private System.Drawing.Point _roiStart; // 拖画起点（当前预览图像坐标）
    private Rectangle _roiRect;         // 已确认选框（原图坐标；Width=0=未框选）
    private Rectangle _roiDraft;        // 拖画中的草框（当前预览图像坐标；Width=0=无）
    private Mat? _roiBgr;               // 选框裁剪后的原图（null=未框选/显示全图）

    public LineWidthForm()
    {
        InitializeComponent();
        Load += OnFormLoad;
    }

    /// <summary>窗体加载后设置分隔条比例与图表初始外观（构造期面板尺寸为 0）。</summary>
    private void OnFormLoad(object? sender, EventArgs e)
    {
        _splitRight.SplitterDistance = Math.Max(80, _splitRight.Height * 55 / 100);
        _plot.Plot.XLabel("沿线位置 (mm)");
        _plot.Plot.YLabel("线宽 (mm)");
        _plot.Plot.Title("（加载数据后显示线宽曲线）");
        _plot.Refresh();
    }

    // ===================== 加载图像 =====================
    private void OnLoad(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "图像|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件|*.*",
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var img = Cv2.ImRead(ofd.FileName, ImreadModes.Color);
            if (img.Empty())
            {
                MessageBox.Show(this, "无法读取图像，请检查文件格式。", "提示");
                return;
            }
            _bgr?.Dispose();
            _bgr = img;
            _roiBgr?.Dispose(); _roiBgr = null;        // 换图→清空旧框选
            _roiRect = Rectangle.Empty;
            _lblFileName.Text = Path.GetFileName(ofd.FileName);
            Measure(silent: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "加载失败：" + ex.Message, "错误");
        }
    }

    // ===================== 测量 =====================
    private void OnMeasure(object? sender, EventArgs e) => Measure(silent: false);

    /// <summary>任一参数变动即重算（图像未加载时静默返回，避免构造期弹出提示）。</summary>
    private void OnParamChanged(object? sender, EventArgs e)
    {
        Measure(silent: true);
    }

    private void Measure(bool silent)
    {
        if (_bgr == null)
        {
            if (!silent) MessageBox.Show(this, "请先加载图像。", "提示");
            return;
        }
        if (_measuring) return; // 重入保护
        _measuring = true;
        try
        {
            // 若已框选 ROI，提前裁剪原图；让管线只处理选区内的线条
            if (_roiRect.Width > 0 && _roiBgr == null)
            {
                var rc = _roiRect;
                rc.Intersect(new System.Drawing.Rectangle(0, 0, _bgr.Width, _bgr.Height));
                if (rc.Width > 0 && rc.Height > 0)
                    _roiBgr = new Mat(_bgr, new OpenCvSharp.Rect(rc.X, rc.Y, rc.Width, rc.Height)).Clone();
            }
            Mat activeBgr = _roiBgr ?? _bgr;

            var p = BuildParams();
            _last?.Dispose();
            LineWidthMeasure.Result? r;
            try { r = LineWidthMeasure.Run(activeBgr, p); }
            catch (Exception ex)
            {
                _last = null;
                _minCol = _maxCol = -1;
                _lblStats.Text = "测量出错：" + ex.Message;
                ClearChart();
                RenderPreview();
                return;
            }
            _last = r;
            if (r == null)
            {
                _minCol = _maxCol = -1;
                _lblStats.Text = "未分割到满足「最小连通域面积」的区域。\n请放宽 HSV 范围或调小最小面积。";
                ClearChart();
                RenderPreview();
                return;
            }
            UpdateStats();
            ComputeMinMaxColumns(r);
            UpdateChart();
            RenderPreview();
        }
        finally
        {
            _measuring = false;
        }
    }

    /// <summary>从控件读取参数，并保证 HSV 下限 ≤ 上限。</summary>
    private LineWidthMeasure.Params BuildParams()
    {
        double hLo = (int)_numHlo.Value, hHi = (int)_numHhi.Value;
        double sLo = (int)_numSlo.Value, sHi = (int)_numShi.Value;
        double vLo = (int)_numVlo.Value, vHi = (int)_numVhi.Value;
        return new LineWidthMeasure.Params
        {
            Lo = new Scalar(Math.Min(hLo, hHi), Math.Min(sLo, sHi), Math.Min(vLo, vHi)),
            Hi = new Scalar(Math.Max(hLo, hHi), Math.Max(sLo, sHi), Math.Max(vLo, vHi)),
            MorphKsize = (int)_numMorph.Value,
            MinAreaPx = (double)_numMinArea.Value,
            PxPerMm = (double)_numPxPerMm.Value,
            SmoothWindow = (int)_numSmooth.Value,
        };
    }

    private void RefreshHsvLabels()
    {
        // HSV 改为 NumericUpDown 后，数值直接显示在控件内，无需同步标签（保留空实现以防外部调用）。
    }

    // ===================== 预览 =====================
    private void OnPreviewModeChanged(object? sender, EventArgs e)
    {
        _manualStep = 0; // 切换预览=切换坐标系，旧的手动点失效，重新点
        RenderPreview();
    }

    /// <summary>按预览模式下拉选择把对应 Mat 渲染到 PictureBox。</summary>
    private void RenderPreview()
    {
        Bitmap? nb = null;
        try
        {
            if (_bgr == null) { _previewW = _previewH = 0; SetPreview(null); return; }
            int mode = _cboPreview.SelectedIndex;
            Mat? src = mode switch
            {
                0 => _roiBgr ?? _bgr,       // 原图（如有框选则显示裁剪后）
                1 => _last?.MaskRaw,        // 原始掩膜
                2 => _last?.MaskLargest,    // 最大连通域
                3 => _last?.RotatedMask,    // 旋转后掩膜
                4 => _last?.RotatedBgr,     // 旋转后原图（极值标记的主舞台）
                _ => _bgr,
            };
            if (src == null) { _previewW = _previewH = 0; SetPreview(null); return; }
            _previewW = src.Width; _previewH = src.Height; // 供 Paint 叠加层换算坐标
            if (src.Channels() == 1)
            {
                using var bgr = new Mat();
                Cv2.CvtColor(src, bgr, ColorConversionCodes.GRAY2BGR);
                nb = MatToBitmap(bgr);
            }
            else
            {
                nb = MatToBitmap(src);
            }
        }
        catch (Exception ex)
        {
            _lblStats.Text = "预览渲染失败：" + ex.Message;
        }
        SetPreview(nb);
    }

    /// <summary>替换预览位图，释放旧位图。</summary>
    private void SetPreview(Bitmap? nb)
    {
        var old = _picPreview.Image;
        if (old is Bitmap ob && ob != _previewBmp) ob.Dispose();
        _previewBmp?.Dispose();
        _previewBmp = nb;
        _picPreview.Image = nb;
    }

    /// <summary>
    /// 把 CV_8U Mat 转为 System.Drawing.Bitmap（不依赖 OpenCvSharp.Extensions）。
    /// 3 通道 BGR 直接对应 Windows 的 24bppRgb(内存同为 BGR)；1 通道灰度用 8bpp 索引+灰阶调色板。
    /// 用 m.Data 原始指针 Marshal.Copy 取出全部字节后再按行写入位图——按字节处理，
    /// 绕开 GetArray&lt;byte&gt; 对多通道 Mat 报「CV_8UC3 不兼容」的限制，且兼容 Mat.Step()(可能含行尾填充)
    /// 与 Bitmap.Stride(4 字节对齐)不一致。
    /// </summary>
    private static Bitmap MatToBitmap(Mat m)
    {
        int w = m.Width, h = m.Height, ch = m.Channels();
        long step = m.Step();                 // Mat 每行字节数（可能 ≥ w*ch）
        int rowBytes = w * ch;                // 每行实际像素字节
        int total = (int)(step * h);          // Mat 数据总字节（含行尾填充）
        byte[] data = new byte[total];
        Marshal.Copy(m.Data, data, 0, total); // 非托管→托管（ptr→array）

        var rect = new Rectangle(0, 0, w, h);
        if (ch >= 3)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            for (int y = 0; y < h; y++)
                Marshal.Copy(data, (int)(y * step), (IntPtr)(bd.Scan0.ToInt64() + y * bd.Stride), rowBytes);
            bmp.UnlockBits(bd);
            return bmp;
        }
        else
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format8bppIndexed);
            var pal = bmp.Palette;
            for (int i = 0; i < 256; i++) pal.Entries[i] = Color.FromArgb(i, i, i);
            bmp.Palette = pal; // 设置灰阶调色板
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            for (int y = 0; y < h; y++)
                Marshal.Copy(data, (int)(y * step), (IntPtr)(bd.Scan0.ToInt64() + y * bd.Stride), rowBytes);
            bmp.UnlockBits(bd);
            return bmp;
        }
    }

    // ===================== 预览叠加层：极值标记 + 手动测量 =====================
    /// <summary>在预览图上叠加：①最细/最粗列的上下边缘两点+白线(任意视图，反旋转回原坐标)；②手动两点测量；③折线图探查。</summary>
    private void OnPreviewPaint(object? sender, PaintEventArgs e)
    {
        if (_previewW <= 0 || _previewH <= 0) return;
        float s = ImgScale(out float ox, out float oy);
        if (s <= 0) return;
        var g = e.Graphics;
        int mode = _cboPreview.SelectedIndex;
        var r = _last;

        // 反旋转系数：把「旋转后测量空间」的点映射回原图坐标。仅原图/掩膜视图(0,1,2)需要；
        // 旋转后视图(3,4)与测量空间一致，直接用 (col,row)。
        bool needInv = r != null && (mode == 0 || mode == 1 || mode == 2);
        double m00 = 0, m01 = 0, m02 = 0, m10 = 0, m11 = 0, m12 = 0;
        if (needInv)
        {
            var center = new Point2f((_previewW - 1) * 0.5f, (_previewH - 1) * 0.5f);
            using var M = Cv2.GetRotationMatrix2D(center, (float)r!.AngleDeg, 1.0);
            using var Minv = new Mat();
            Cv2.InvertAffineTransform(M, Minv);
            m00 = Minv.At<double>(0, 0); m01 = Minv.At<double>(0, 1); m02 = Minv.At<double>(0, 2);
            m10 = Minv.At<double>(1, 0); m11 = Minv.At<double>(1, 1); m12 = Minv.At<double>(1, 2);
        }
        // 旋转后空间(col,row) → 当前预览的显示坐标
        PointF EP(int col, int row)
        {
            float ix = col, iy = row;
            if (needInv) { ix = (float)(m00 * col + m01 * row + m02); iy = (float)(m10 * col + m11 * row + m12); }
            return new PointF(ox + ix * s, oy + iy * s);
        }
        // 画某列的上下边缘两点 + 连线 + 标签（颜色由调用方指定）
        void DrawPair(int col, Color color, string label)
        {
            if (r == null || col < 0 || col >= r.RowTopPx.Length) return;
            int t = r.RowTopPx[col], b = r.RowBotPx[col];
            if (t < 0 || b < 0) return; // 间隙列不画
            PointF top = EP(col, t), bot = EP(col, b);
            using var pen = new Pen(color, 3f);
            using var br = new SolidBrush(color);
            g.DrawLine(pen, top, bot);
            g.FillEllipse(br, top.X - 4, top.Y - 4, 8, 8); // 上点
            g.FillEllipse(br, bot.X - 4, bot.Y - 4, 8, 8); // 下点
            DrawLabel(g, label, top.X + 6, top.Y, color);
        }

        // ⓞ ROI 选框：拖画中画草框(_roiDraft)，否则在未裁剪时画已确认选框(_roiRect，黄虚线)
        Rectangle roiDraw = (_roiDrawing && _roiDraft.Width > 0)
            ? _roiDraft
            : ((_roiBgr == null && _roiRect.Width > 0) ? _roiRect : Rectangle.Empty);
        if (roiDraw.Width > 0)
        {
            float rx = ox + roiDraw.X * s, ry = oy + roiDraw.Y * s;
            float rw = roiDraw.Width * s, rh = roiDraw.Height * s;
            using var roiPen = new Pen(Color.Yellow, 1.5f)
            { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.DrawRectangle(roiPen, rx, ry, rw, rh);
        }

        // ⓪ 线宽边缘描边：把每列上下边缘连成两条青色折线，高亮线条的上下边界(任意视图)
        if (r != null && r.RowTopPx.Length > 0)
        {
            using var edgePen = new Pen(Color.Cyan, 1.5f);
            var topPts = new List<PointF>();
            var botPts = new List<PointF>();
            for (int x = 0; x < r.RowTopPx.Length; x++)
            {
                int t = r.RowTopPx[x], b = r.RowBotPx[x];
                if (t < 0 || b < 0) // 间隙：断开当前折线段
                {
                    if (topPts.Count > 1) g.DrawLines(edgePen, topPts.ToArray());
                    if (botPts.Count > 1) g.DrawLines(edgePen, botPts.ToArray());
                    topPts.Clear(); botPts.Clear();
                    continue;
                }
                topPts.Add(EP(x, t));
                botPts.Add(EP(x, b));
            }
            if (topPts.Count > 1) g.DrawLines(edgePen, topPts.ToArray());
            if (botPts.Count > 1) g.DrawLines(edgePen, botPts.ToArray());
        }

        // ① 极值标记：白线+白点（任意视图都显示；最细/最粗靠标签区分）
        if (r != null && _minCol >= 0)
        {
            DrawPair(_minCol, Color.White, $"最细 {_minW:F2}mm");
            DrawPair(_maxCol, Color.White, $"最粗 {_maxW:F2}mm");
        }

        // ③ 折线图探查：橙线+橙点（任意视图）
        if (r != null && _probeCol >= 0)
        {
            double w = _probeCol < r.WidthMm.Length ? r.WidthMm[_probeCol] : double.NaN;
            DrawPair(_probeCol, Color.Orange, double.IsNaN(w) ? "探查(间隙)" : $"探查 {w:F2}mm @ {r.PosMm[_probeCol]:F1}mm");
        }

        // ② 手动两点测量
        if (_manualMode && _manualStep >= 1)
        {
            PointF a = ToDisplay(_manualA, ox, oy, s);
            using var pen = new Pen(Color.Yellow, 2f);
            g.FillEllipse(Brushes.Yellow, a.X - 3, a.Y - 3, 6, 6);
            if (_manualStep >= 2)
            {
                PointF b = ToDisplay(_manualB, ox, oy, s);
                g.DrawLine(pen, a, b);
                g.FillEllipse(Brushes.Yellow, b.X - 3, b.Y - 3, 6, 6);
                double pxPerMm = (double)_numPxPerMm.Value;
                double mm = pxPerMm > 0 ? _manualPx / pxPerMm : 0;
                DrawLabel(g, $"{_manualPx:F1}px = {mm:F3}mm", (a.X + b.X) / 2f, (a.Y + b.Y) / 2f, Color.Yellow);
            }
        }
    }

    /// <summary>手动测量：鼠标点击取点（第一点→第二点→完成；再点重置）。</summary>
    private void OnPreviewMouseClick(object? sender, MouseEventArgs e)
    {
        // 仅看手动模式：ROI 框选已由 Down/Move/Up 区分单击与拖画处理，
        // 拖画(移动大)不触发 Click，故框选模式下单击仍可手动测点(修复"框完后不允许手动测量")
        if (!_manualMode || _previewW <= 0) return;
        float s = ImgScale(out float ox, out float oy);
        if (s <= 0) return;
        PointF p = ToImage(e.Location, ox, oy, s);
        if (p.X < 0 || p.Y < 0 || p.X >= _previewW || p.Y >= _previewH) return; // 点在图外忽略
        if (_manualStep == 0 || _manualStep >= 2)
        {
            _manualA = p; _manualStep = 1;
        }
        else
        {
            _manualB = p; _manualStep = 2;
            double dx = _manualB.X - _manualA.X, dy = _manualB.Y - _manualA.Y;
            _manualPx = Math.Sqrt(dx * dx + dy * dy);
        }
        _picPreview.Invalidate();
    }

    /// <summary>勾选/取消「手动测量」：切换十字光标并清空已点标记。</summary>
    private void OnManualToggle(object? sender, EventArgs e)
    {
        _manualMode = _chkManual.Checked;
        _manualStep = 0;
        _picPreview.Cursor = _manualMode || _chkRoi.Checked ? Cursors.Cross : Cursors.Default;
        _picPreview.Invalidate();
    }

    /// <summary>勾选/取消「框选取线」：进入/退出 ROI 拖画模式（强制回原图以便在原坐标画框）。</summary>
    private void OnRoiToggle(object? sender, EventArgs e)
    {
        if (_chkRoi.Checked)
        {
            _cboPreview.SelectedIndex = 0; // 强制回原图，确保选框在原图坐标
        }
        else
        {
            // 取消框选：清空选框与裁剪图，回到全图；旧测量结果基于裁剪，一并清空
            _roiRect = Rectangle.Empty;
            _roiBgr?.Dispose(); _roiBgr = null;
            _last?.Dispose(); _last = null;
            _minCol = _maxCol = -1; _probeCol = -1;
            ClearChart();
            RenderPreview();
        }
        _picPreview.Cursor = _chkRoi.Checked || _manualMode ? Cursors.Cross : Cursors.Default;
        _picPreview.Invalidate();
    }

    // ===================== ROI 鼠标拖画 =====================
    private void OnPreviewMouseDown(object? sender, MouseEventArgs e)
    {
        if (!_chkRoi.Checked || _previewW <= 0) return;
        float s = ImgScale(out float ox, out float oy);
        if (s <= 0) return;
        _roiDrawing = true;
        _roiDragStarted = false;   // 待首次明显移动才认定为拖画
        _roiStart = System.Drawing.Point.Truncate(ToImage(e.Location, ox, oy, s)); // 显示→当前图像坐标
        _roiDraft = Rectangle.Empty;   // 清草框；不清 _roiRect(单击测点不破坏已确认选框)
        _picPreview.Invalidate();
    }

    private void OnPreviewMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_chkRoi.Checked || !_roiDrawing) return;
        float s = ImgScale(out float ox, out float oy);
        if (s <= 0) return;
        PointF cur = ToImage(e.Location, ox, oy, s);
        // 首次明显移动(≥3px)认定为拖画：若当前显示的是裁剪图(_roiBgr)，先转回原图坐标，
        // 保证新选框定位在原图(与 Measure 裁剪源一致)，避免在裁剪图上重复框选时坐标错位
        if (!_roiDragStarted)
        {
            double ddx = cur.X - _roiStart.X, ddy = cur.Y - _roiStart.Y;
            if (Math.Sqrt(ddx * ddx + ddy * ddy) >= 3)
            {
                _roiDragStarted = true;
                if (_roiBgr != null && _roiRect.Width > 0)
                {
                    // 裁剪图像坐标 → 原图坐标(加上裁剪区域左上偏移)
                    _roiStart = new System.Drawing.Point(_roiStart.X + _roiRect.X, _roiStart.Y + _roiRect.Y);
                    _roiBgr.Dispose(); _roiBgr = null;
                    _last?.Dispose(); _last = null;
                    _minCol = _maxCol = -1; _probeCol = -1;
                    ClearChart();
                    RenderPreview();   // 切回原图显示，_previewW/H 变为原图尺寸
                    s = ImgScale(out ox, out oy);
                    cur = s > 0 ? ToImage(e.Location, ox, oy, s) : cur;   // 按原图尺寸重算当前点
                }
            }
        }
        if (_roiDragStarted)
        {
            _roiDraft = NormalizeRect(_roiStart, cur);
            _roiDraft.Intersect(new Rectangle(0, 0, _previewW, _previewH)); // 截断到图像范围内
            _picPreview.Invalidate();
        }
    }

    private void OnPreviewMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_chkRoi.Checked || !_roiDrawing) return;
        _roiDrawing = false;
        // 未拖画(单击)或草框过小：丢弃草框，保留已确认选框；交由 MouseClick 做手动测点
        if (!_roiDragStarted || _roiDraft.Width < 5 || _roiDraft.Height < 5)
        {
            _roiDraft = Rectangle.Empty;
            _roiDragStarted = false;
            _picPreview.Invalidate();
            return;
        }
        // 拖画框选确认：草框转正，清旧裁剪并自动测量(裁剪到选区)，修复"框取后仍需手动点测量"
        _roiRect = _roiDraft;
        _roiDraft = Rectangle.Empty;
        _roiDragStarted = false;
        _roiBgr?.Dispose(); _roiBgr = null;   // 新选框：清旧裁剪，让 Measure 按新框重新裁
        _picPreview.Invalidate();
        Measure(silent: false);
    }

    /// <summary>把对角两点归一化为正宽高的 Rectangle（左上角+正值宽高）。</summary>
    private static Rectangle NormalizeRect(System.Drawing.Point a, PointF b)
    {
        int x = Math.Min(a.X, (int)b.X), y = Math.Min(a.Y, (int)b.Y);
        int w = Math.Abs((int)b.X - a.X), h = Math.Abs((int)b.Y - a.Y);
        return new Rectangle(x, y, w, h);
    }

    /// <summary>找出最细/最粗线宽所在的列(旋转后图坐标)，用于叠加标记。</summary>
    private void ComputeMinMaxColumns(LineWidthMeasure.Result r)
    {
        _minCol = -1; _maxCol = -1;
        double mn = double.PositiveInfinity, mx = double.NegativeInfinity;
        for (int x = 0; x < r.WidthMm.Length; x++)
        {
            if (double.IsNaN(r.WidthMm[x])) continue;
            if (r.WidthMm[x] < mn) { mn = r.WidthMm[x]; _minCol = x; }
            if (r.WidthMm[x] > mx) { mx = r.WidthMm[x]; _maxCol = x; }
        }
        _minW = mn; _maxW = mx;
    }

    /// <summary>SizeMode=Zoom 下：图像→显示的缩放比 s 与左上偏移 (ox,oy)。</summary>
    private float ImgScale(out float ox, out float oy)
    {
        int iw = _previewW, ih = _previewH;
        int pw = _picPreview.ClientSize.Width, ph = _picPreview.ClientSize.Height;
        if (iw <= 0 || ih <= 0 || pw <= 0 || ph <= 0) { ox = oy = 0; return 0; }
        float s = Math.Min((float)pw / iw, (float)ph / ih);
        ox = (pw - iw * s) / 2f; oy = (ph - ih * s) / 2f;
        return s;
    }

    private static PointF ToDisplay(PointF img, float ox, float oy, float s) =>
        new(ox + img.X * s, oy + img.Y * s);
    private static PointF ToImage(System.Drawing.Point disp, float ox, float oy, float s) =>
        new((disp.X - ox) / s, (disp.Y - oy) / s);

    /// <summary>带半透明黑底的文字标签，保证叠在图上仍可读。</summary>
    private static void DrawLabel(Graphics g, string text, float x, float y, Color c)
    {
        using var f = new Font("Segoe UI", 9F, FontStyle.Bold);
        SizeF sz = g.MeasureString(text, f);
        using var bg = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
        using var fg = new SolidBrush(c);
        g.FillRectangle(bg, x, y, sz.Width, sz.Height);
        g.DrawString(text, f, fg, x, y);
    }

    // ===================== 折线图 =====================
    private void ClearChart()
    {
        _plot.Plot.Clear();
        _plot.Plot.Title("（无可显示数据）");
        _plot.Refresh();
    }

    private void UpdateChart()
    {
        var r = _last;
        if (r == null) { ClearChart(); return; }
        _plot.Plot.Clear();
        // 按 NaN 分段绘制，避免跨越间隙的连线
        var xs = r.PosMm; var ys = r.WidthMm;
        var segX = new List<double>(); var segY = new List<double>();
        for (int i = 0; i < xs.Length; i++)
        {
            if (double.IsNaN(ys[i]))
            {
                AddSegment(segX, segY);
                continue;
            }
            segX.Add(xs[i]); segY.Add(ys[i]);
        }
        AddSegment(segX, segY);

        // 均值参考线（虚线）
        var hl = _plot.Plot.Add.HorizontalLine(r.Mean);
        hl.LineWidth = 1;
        hl.Color = ScottPlot.Color.FromHtml("#d62728");
        hl.LinePattern = ScottPlot.LinePattern.Dashed;

        // 极值点高亮：最细(绿)/最粗(红)
        if (_minCol >= 0) AddPointMark(r.PosMm[_minCol], r.WidthMm[_minCol], ScottPlot.Color.FromHtml("#2ca02c"));
        if (_maxCol >= 0) AddPointMark(r.PosMm[_maxCol], r.WidthMm[_maxCol], ScottPlot.Color.FromHtml("#d62728"));

        // 折线图点击探查：橙色竖线 + 点
        if (_probeCol >= 0 && _probeCol < r.PosMm.Length && !double.IsNaN(r.WidthMm[_probeCol]))
        {
            var vl = _plot.Plot.Add.VerticalLine(r.PosMm[_probeCol]);
            vl.LineWidth = 1;
            vl.Color = ScottPlot.Color.FromHtml("#ff7f0e");
            vl.LinePattern = ScottPlot.LinePattern.Dashed;
            AddPointMark(r.PosMm[_probeCol], r.WidthMm[_probeCol], ScottPlot.Color.FromHtml("#ff7f0e"));
        }

        _plot.Plot.XLabel("沿线位置 (mm)");
        _plot.Plot.YLabel("线宽 (mm)");
        _plot.Plot.Title($"μ={r.Mean:F3} mm   σ={r.Std:F3}   CV={r.Cv * 100:F1}%   倾角={r.AngleDeg:F2}°   有效列={r.ValidCount}");
        _plot.Refresh();

        void AddSegment(List<double> sx, List<double> sy)
        {
            if (sx.Count < 1) return;
            var sp = _plot.Plot.Add.Scatter(sx.ToArray(), sy.ToArray());
            sp.MarkerSize = 0;
            sp.LineWidth = 1.5f;
            sp.Color = ScottPlot.Color.FromHtml("#1f77b4");
            sx.Clear(); sy.Clear();
        }
        void AddPointMark(double x, double y, ScottPlot.Color c)
        {
            var sp = _plot.Plot.Add.Scatter(new[] { x }, new[] { y });
            sp.MarkerSize = 8;
            sp.LineWidth = 0;
            sp.Color = c;
        }
    }

    /// <summary>点击折线图：把鼠标处位置(列)设为探查点，同步在图与折线图高亮。</summary>
    private void OnChartClick(object? sender, MouseEventArgs e)
    {
        var r = _last;
        if (r == null || r.PosMm.Length == 0) return;
        double xmm = _plot.Plot.GetCoordinates(e.X, e.Y).X; // 像素 → 数据 x(mm)
        double pxPerMm = (double)_numPxPerMm.Value;
        int col = pxPerMm > 0 ? (int)Math.Round(xmm * pxPerMm) : 0;
        _probeCol = Math.Clamp(col, 0, r.PosMm.Length - 1);
        UpdateChart();          // 折线图标探查点/竖线
        _picPreview.Invalidate(); // 图上标测宽线段
    }

    // ===================== 统计文本 =====================
    private void UpdateStats()
    {
        var r = _last;
        if (r == null) { _lblStats.Text = ""; return; }
        _lblStats.Text =
            $"检测倾角      {r.AngleDeg,8:F2} °\r\n" +
            $"有效列数      {r.ValidCount,8}\r\n" +
            $"线宽均值 μ    {r.Mean,8:F3} mm\r\n" +
            $"标准差   σ    {r.Std,8:F3} mm\r\n" +
            $"最小线宽      {r.Min,8:F3} mm\r\n" +
            $"最大线宽      {r.Max,8:F3} mm\r\n" +
            $"极差(max-min) {r.Max - r.Min,8:F3} mm\r\n" +
            $"变异系数 CV   {r.Cv * 100,8:F2} %";
    }

    // ===================== 导出 CSV =====================
    private void OnExport(object? sender, EventArgs e)
    {
        var r = _last;
        if (r == null) { MessageBox.Show(this, "尚无测量结果，请先测量。", "提示"); return; }
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV|*.csv|所有文件|*.*",
            DefaultExt = "csv",
            FileName = "linewidth.csv",
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var sb = new System.Text.StringBuilder();
            // 表头
            sb.Append(string.Format(Inv, "位置mm,线宽mm,平滑线宽mm,倾角deg,均值mm,标准差mm,CV\r\n"));
            for (int i = 0; i < r.PosMm.Length; i++)
            {
                double w = r.WidthMm[i], ws = r.WidthSmoothedMm[i];
                sb.Append(string.Format(Inv, "{0:F3},{1},{2},{3:F2},{4:F3},{5:F3},{6:F4}\r\n",
                    r.PosMm[i],
                    double.IsNaN(w) ? "" : w.ToString("F3", Inv),
                    double.IsNaN(ws) ? "" : ws.ToString("F3", Inv),
                    r.AngleDeg, r.Mean, r.Std, r.Cv));
            }
            File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
            MessageBox.Show(this, $"已导出 {r.PosMm.Length} 行到：\n{sfd.FileName}", "完成");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：" + ex.Message, "错误");
        }
    }

    // ===================== 资源释放 =====================
    /// <summary>关闭时释放 OpenCV 的 Mat 与位图，避免托管/非托管内存泄漏。</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewBmp?.Dispose();
            if (_picPreview.Image is Bitmap b && b != _previewBmp) b.Dispose();
            _last?.Dispose();
            _roiBgr?.Dispose();
            _bgr?.Dispose();
        }
        base.Dispose(disposing);
    }
}
