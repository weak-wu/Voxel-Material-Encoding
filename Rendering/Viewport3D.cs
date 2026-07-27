using System.Drawing;
using System.Drawing.Drawing2D;
using GcodeViewer.Models;
using GcodeViewer.Parsing;

namespace GcodeViewer.Rendering;

/// <summary>
/// 3D 路径视图 UserControl。自写线框投影（GDI+ 双缓冲）。
/// 鼠标：左键拖=旋转、滚轮=缩放、右键拖=平移、双击=重置；左键单击=选点。
/// 事件：SelectedMoveChanged、SceneInvalidated。
/// </summary>
public sealed class Viewport3D : UserControl
{
    private ParsedGcode? _gcode;
    private readonly Camera _cam = new();
    private readonly List<PointF> _screenPts = new();

    // 显示开关
    public bool ColorByTool { get; set; } = true;
    public bool ColorByLayer { get; set; } = false;
    public bool ShowToolChange { get; set; } = true;
    public bool ShowModified { get; set; } = true;
    public int FilterLayer { get; set; } = -1;   // -1 = 全部

    // 线宽（像素）：默认加粗，白底截图更清晰，可按需微调
    /// <summary>空行程 G0 虚线线宽。</summary>
    public float G0LineWidth { get; set; } = 2f;
    /// <summary>打印 G1 实线线宽。</summary>
    public float G1LineWidth { get; set; } = 3.5f;
    public int SelectedMoveIndex { get; private set; } = -1;

    // ---- STL 模型线框显示（SDF 映射的空间参照，与路径同坐标系）----
    private VoxelMapper.StlMesh? _stlMesh;
    private double _sceneCx, _sceneCy, _sceneCz;   // 本帧统一的场景中心(路径+STL 联合包围盒)
    /// <summary>是否绘制 STL 线框。</summary>
    public bool ShowStl { get; set; } = true;
    /// <summary>STL 线框颜色（默认浅灰，作为背景参照不抢路径）。</summary>
    public Color StlWireColor { get; set; } = Color.Silver;
    /// <summary>STL 线框单帧最多绘制边数，超过则等间隔抽样，避免大模型卡顿。</summary>
    public int StlMaxEdges { get; set; } = 24000;

    // ---- 体素点云显示（体素映射的源数据可视化，与 STL 线框并列；按 Origin/Step 物理坐标定位）----
    private VoxelMapper.VoxelData? _voxelData;
    /// <summary>是否绘制体素点云。</summary>
    public bool ShowVoxel { get; set; } = true;
    /// <summary>体素点云颜色（默认钢蓝，与 STL 浅灰线框区分）。</summary>
    public Color VoxelColor { get; set; } = Color.SteelBlue;
    /// <summary>体素点云单帧最多绘制点数，超过则等间隔抽样。</summary>
    public int VoxelMaxPoints { get; set; } = 12000;

    /// <summary>设置体素数据并重置缩放以贴合(路径+STL+体素)联合包围盒；传 null 清除。</summary>
    public void SetVoxelData(VoxelMapper.VoxelData? v)
    {
        _voxelData = v;
        _userZoomed = false;   // 新数据进来后重新贴合视图
        FitToView();
        Invalidate();
    }

    /// <summary>设置 STL 网格并重置缩放以贴合(路径+STL)联合包围盒；传 null 清除。</summary>
    public void SetStlMesh(VoxelMapper.StlMesh? mesh)
    {
        _stlMesh = mesh;
        _userZoomed = false;   // 新模型进来后重新贴合视图
        FitToView();
        Invalidate();
    }


    private Overlay3D? _overlay;   // 评估叠加层（偏差热力线 / 体素点云），可为空

    /// <summary>设置评估叠加层。传 null 清除。叠加层坐标系与当前 gcode 一致，按主路径包围盒居中投影。</summary>
    public void SetOverlay(Overlay3D? overlay) { _overlay = overlay; Invalidate(); }

    private double _unitScale = 1.0; // 把 gcode 坐标缩放到屏幕友好尺度
    private PointF _lastMouse;
    private bool _dragging;
    private bool _movedDuringDrag;

    public event EventHandler<int?>? SelectedMoveChanged;

    public Viewport3D()
    {
        DoubleBuffered = true;
        // 纯白背景，便于直接截图到论文（避免深色底需后期抠图）
        BackColor = Color.White;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        _cam.UpdateTrig();
    }

    /// <summary>设置当前 gcode 数据并重置视图（角度/缩放/平移）。</summary>
    public void SetGcode(ParsedGcode? g)
    {
        _gcode = g;
        ResetView();
        Invalidate();
    }

    /// <summary>
    /// 仅替换数据并重绘，保留当前相机角度/缩放/平移。
    /// 用于编辑(G0↔G1 或坐标微调)后刷新画面，不让用户失去观察视角。
    /// </summary>
    public void UpdateData(ParsedGcode? g)
    {
        _gcode = g;
        Invalidate();
    }

    public void ResetView()
    {
        _cam.Yaw = -Math.PI / 6;
        _cam.Pitch = -Math.PI / 6;
        _cam.Pan = PointF.Empty;
        _cam.Distance = 400;
        _userZoomed = false;
        FitToView();
        Invalidate();
    }

    /// <summary>
    /// 沿 Z 轴垂直俯视（顶视图）：Yaw/Pitch 归零，XY 平面正对屏幕。
    /// 投影推导：Pitch=0 时 z2=z1，Z 不影响屏幕坐标，仅保留 XY 平面投影。
    /// 适合截图到论文展示整体路径轮廓。配合“全屏 3D”(F11) 可得无干扰截图。
    /// </summary>
    public void TopView()
    {
        _cam.Yaw = 0;
        _cam.Pitch = 0;
        _cam.Pan = PointF.Empty;
        _userZoomed = false;
        _cam.UpdateTrig();   // 改了角度必须刷新三角函数缓存，否则投影用旧值
        FitToView();
        Invalidate();
    }

    /// <summary>按当前视图尺寸缩放场景(不重置角度)。场景范围取 gcode 与 STL 联合包围盒，保证两者同屏可见。</summary>
    public void FitToView()
    {
        if (GetSceneBounds(out _, out _, out _, out double maxExtent) && maxExtent > 0.001)
        {
            double screenSize = Math.Min(Math.Max(Width, 1), Math.Max(Height, 1)) * 0.78;
            _cam.Distance = maxExtent * _cam.Focal / Math.Max(screenSize, 1);
            if (_cam.Distance < 80) _cam.Distance = 80;
        }
    }

    // 视图尺寸变化（如折叠面板、窗口缩放）时自动贴合，除非用户已手动缩放
    private bool _userZoomed;
    private Size _lastSize = Size.Empty;
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_lastSize == Size.Empty) { _lastSize = Size; return; }
        // 尺寸显著变化且用户没手动缩放 → 重新贴合
        if (!_userZoomed && Math.Abs(Width - _lastSize.Width) + Math.Abs(Height - _lastSize.Height) > 8)
        {
            FitToView();
        }
        _lastSize = Size;
        Invalidate();
    }

    public void SelectMove(int index)
    {
        SelectedMoveIndex = index;
        Invalidate();
    }

    // ---- 鼠标交互 ----
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _lastMouse = e.Location;
        _movedDuringDrag = false;
        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            _dragging = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        float dx = e.X - _lastMouse.X, dy = e.Y - _lastMouse.Y;
        if (Math.Abs(dx) + Math.Abs(dy) > 2) _movedDuringDrag = true;
        _lastMouse = e.Location;

        if ((Control.ModifierKeys & Keys.Shift) != 0 || MouseButtons == MouseButtons.Right || e.Button == MouseButtons.Right)
        {
            _cam.Pan = new PointF(_cam.Pan.X + dx, _cam.Pan.Y + dy);
        }
        else
        {
            _cam.Yaw += dx * 0.01;
            _cam.Pitch += dy * 0.01;
            _cam.Pitch = Math.Clamp(_cam.Pitch, -Math.PI / 2 + 0.01, Math.PI / 2 - 0.01);
        }
        _cam.UpdateTrig();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        bool wasDragging = _dragging;
        _dragging = false;
        // 左键无拖动 → 视为点击选点
        if (wasDragging && e.Button == MouseButtons.Left && !_movedDuringDrag)
            PickAt(e.Location);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _cam.Distance *= e.Delta > 0 ? 0.9 : 1.1;
        _cam.Distance = Math.Clamp(_cam.Distance, 20, 50000);
        _userZoomed = true;  // 用户手动缩放后，不再自动贴合
        Invalidate();
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        ResetView();
    }

    private void PickAt(PointF pt)
    {
        if (_gcode == null || _screenPts.Count == 0) return;
        double best = double.MaxValue;
        int bestIdx = -1;
        double threshold = 8.0;
        for (int i = 0; i < _gcode.Moves.Count; i++)
        {
            if (i >= _screenPts.Count) break;
            double d = HitTest.Dist(pt, _screenPts[i]);
            if (d < best) { best = d; bestIdx = i; }
        }
        if (best <= threshold && bestIdx >= 0)
        {
            SelectedMoveIndex = bestIdx;
            SelectedMoveChanged?.Invoke(this, bestIdx);
            Invalidate();
        }
    }

    // ---- 绘制 ----
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        bool hasGcode = _gcode != null && _gcode.Moves.Count > 0 && _gcode.Bounds.IsValid;
        bool hasStl = ShowStl && _stlMesh != null && _stlMesh.IsValid;
        bool hasVoxel = ShowVoxel && _voxelData != null && _voxelData.Matrix.Count > 0;
        if (!hasGcode && !hasStl && !hasVoxel)
        {
            DrawHint(g, "请打开 gcode 文件或导入 STL/体素");
            return;
        }

        // 统一场景中心 = (gcode 包围盒 ∪ STL 包围盒 ∪ 体素包围盒) 的中心，保证三者同框对齐
        GetSceneBounds(out _sceneCx, out _sceneCy, out _sceneCz, out _);
        var center = new PointF(Width / 2f, Height / 2f);

        // 1. 先画 STL 线框（作为路径的空间参照，画在路径之下）
        if (hasStl) DrawStlWireframe(g, center);
        // 1b. 再画体素点云（源数据可视化，画在路径之下、STL 之上）
        if (hasVoxel) DrawVoxelPoints(g, center);

        // 仅 STL（无路径）：跳过路径绘制，但仍尝试叠加层后返回
        if (!hasGcode) { DrawOverlay(g, center); return; }

        // 2. gcode 路径投影（用统一场景中心归一化；此处 _gcode 必非空，! 抑制空告警）
        _screenPts.Clear();
        for (int i = 0; i < _gcode!.Moves.Count; i++)
        {
            var m = _gcode.Moves[i];
            var p = new Vec3(m.X - _sceneCx, m.Y - _sceneCy, m.Z - _sceneCz);
            _screenPts.Add(_cam.Project(p, center));
        }

        // 画线段（带显示开关）
        // 跟踪上一个未被层过滤的屏幕点；首条可见 move 的起点使用其 Prev 位置（文件起始坐标）。
        PointF? prevScreenPt = null;
        for (int i = 0; i < _gcode.Moves.Count; i++)
        {
            var m = _gcode.Moves[i];
            if (FilterLayer >= 0 && m.Layer != FilterLayer) continue;

            PointF c = _screenPts[i];
            PointF a;
            if (prevScreenPt.HasValue)
            {
                // 后续线段：起点 = 上一条可见 move 的屏幕终点
                a = prevScreenPt.Value;
            }
            else
            {
                // 第一条可见 move：起点 = 本 move 的 Prev 位置（gcode 起始坐标），
                // 需独立投影，因为 Prev 不在 _screenPts 中。
                var prevP = new Vec3(m.PrevX - _sceneCx, m.PrevY - _sceneCy, m.PrevZ - _sceneCz);
                a = _cam.Project(prevP, center);
            }

            using var pen = new Pen(GetColor(m), m.Type == MoveType.G0 ? G0LineWidth : G1LineWidth);
            if (m.Type == MoveType.G0) pen.DashStyle = DashStyle.Dot;
            g.DrawLine(pen, a, c);

            prevScreenPt = c;
        }

        // 切换点 / 修改点 / 选中点 标记
        if (ShowToolChange || ShowModified)
        {
            for (int i = 0; i < _gcode.Moves.Count; i++)
            {
                if (FilterLayer >= 0 && _gcode.Moves[i].Layer != FilterLayer) continue;
                var m = _gcode.Moves[i];
                var p = _screenPts[i];
                if (ShowToolChange && m.IsToolChange)
                    DrawMarker(g, p, Color.Orange, 4f);
                if (ShowModified && m.IsModified)
                    DrawRing(g, p, Color.Gold, 7f);
            }
        }

        // 选中点（白底改用黑色十字+黑环，保证可见）
        if (SelectedMoveIndex >= 0 && SelectedMoveIndex < _screenPts.Count)
        {
            var p = _screenPts[SelectedMoveIndex];
            using var pen = new Pen(Color.Black, 1.5f);
            g.DrawLine(pen, p.X - 10, p.Y, p.X + 10, p.Y);
            g.DrawLine(pen, p.X, p.Y - 10, p.X, p.Y + 10);
            DrawRing(g, p, Color.Black, 8f);
        }

        // 评估叠加层（偏差热力线 / 体素点云）——叠加在主路径之上
        DrawOverlay(g, center);
    }

    /// <summary>计算 (gcode ∪ STL) 联合包围盒的中心与最大边长；无任何数据时返回 false。</summary>
    private bool GetSceneBounds(out double cx, out double cy, out double cz, out double maxExtent)
    {
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        double minZ = double.MaxValue, maxZ = double.MinValue;
        bool any = false;

        if (_gcode != null && _gcode.Bounds.IsValid)
        {
            minX = Math.Min(minX, _gcode.Bounds.MinX); maxX = Math.Max(maxX, _gcode.Bounds.MaxX);
            minY = Math.Min(minY, _gcode.Bounds.MinY); maxY = Math.Max(maxY, _gcode.Bounds.MaxY);
            minZ = Math.Min(minZ, _gcode.Bounds.MinZ); maxZ = Math.Max(maxZ, _gcode.Bounds.MaxZ);
            any = true;
        }
        if (_stlMesh != null && _stlMesh.IsValid)
        {
            minX = Math.Min(minX, _stlMesh.MinX); maxX = Math.Max(maxX, _stlMesh.MaxX);
            minY = Math.Min(minY, _stlMesh.MinY); maxY = Math.Max(maxY, _stlMesh.MaxY);
            minZ = Math.Min(minZ, _stlMesh.MinZ); maxZ = Math.Max(maxZ, _stlMesh.MaxZ);
            any = true;
        }
        if (_voxelData != null && _voxelData.Matrix.Count > 0
            && _voxelData.ColCount > 0 && _voxelData.RowCount > 0)
        {
            // 体素物理范围：[Origin, Origin + count*Step]（Step 可能为正/负，取 min/max）
            var v = _voxelData;
            int lz = v.Matrix.Count;
            double vxMin = Math.Min(v.OriginX, v.OriginX + v.ColCount * v.StepX);
            double vxMax = Math.Max(v.OriginX, v.OriginX + v.ColCount * v.StepX);
            double vyMin = Math.Min(v.OriginY, v.OriginY + v.RowCount * v.StepY);
            double vyMax = Math.Max(v.OriginY, v.OriginY + v.RowCount * v.StepY);
            double vzMin = Math.Min(v.OriginZ, v.OriginZ + lz * v.StepZ);
            double vzMax = Math.Max(v.OriginZ, v.OriginZ + lz * v.StepZ);
            minX = Math.Min(minX, vxMin); maxX = Math.Max(maxX, vxMax);
            minY = Math.Min(minY, vyMin); maxY = Math.Max(maxY, vyMax);
            minZ = Math.Min(minZ, vzMin); maxZ = Math.Max(maxZ, vzMax);
            any = true;
        }

        if (!any) { cx = cy = cz = 0; maxExtent = 0; return false; }
        cx = (minX + maxX) * 0.5;
        cy = (minY + maxY) * 0.5;
        cz = (minZ + maxZ) * 0.5;
        maxExtent = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
        return true;
    }

    /// <summary>绘制 STL 三角网格线框：每片投出 3 条边；面片过多时等间隔抽样以控帧。</summary>
    private void DrawStlWireframe(Graphics g, PointF center)
    {
        var tris = _stlMesh!.Triangles;
        if (tris == null || tris.Count == 0) return;

        int n = tris.Count;
        // 抽样步长：使总绘制边数 ≈ StlMaxEdges
        int stride = n * 3 > StlMaxEdges ? (int)Math.Ceiling(n * 3.0 / StlMaxEdges) : 1;

        using var pen = new Pen(StlWireColor, 0.8f);
        for (int i = 0; i < n; i += stride)
        {
            var t = tris[i];
            var pa = _cam.Project(new Vec3(t.Ax - _sceneCx, t.Ay - _sceneCy, t.Az - _sceneCz), center);
            var pb = _cam.Project(new Vec3(t.Bx - _sceneCx, t.By - _sceneCy, t.Bz - _sceneCz), center);
            var pc = _cam.Project(new Vec3(t.Cx - _sceneCx, t.Cy - _sceneCy, t.Cz - _sceneCz), center);
            g.DrawLine(pen, pa, pb);
            g.DrawLine(pen, pb, pc);
            g.DrawLine(pen, pc, pa);
        }
    }

    /// <summary>
    /// 绘制体素点云：遍历 [Z层][Y行][X列RLE]，对 Depth≠0(有材料) 段展开为单元物理中心点，
    /// 减统一场景中心后用相机投影画小圆点；点数过多时等间隔抽样以控帧。
    /// 单元中心 = (OriginX+(col+0.5)·StepX, OriginY+(row+0.5)·StepY, OriginZ+(layer+0.5)·StepZ)。
    /// </summary>
    private void DrawVoxelPoints(Graphics g, PointF center)
    {
        var v = _voxelData!;
        var matrix = v.Matrix;
        if (matrix == null || matrix.Count == 0) return;

        // 收集所有有材料单元的物理中心（统一着色，便于抽样）
        var pts = new List<Vec3>();
        for (int layer = 0; layer < matrix.Count; layer++)
        {
            double cz = v.OriginZ + (layer + 0.5) * v.StepZ;
            var rows = matrix[layer];
            for (int row = 0; row < rows.Count; row++)
            {
                double cy = v.OriginY + (row + 0.5) * v.StepY;
                int col = 0;
                foreach (var seg in rows[row])
                {
                    int n = Math.Max(0, seg.Count);
                    if (seg.Depth != 0)
                    {
                        for (int k = 0; k < n; k++, col++)
                        {
                            double cx = v.OriginX + (col + 0.5) * v.StepX;
                            pts.Add(new Vec3(cx, cy, cz));
                        }
                    }
                    else col += n;   // 空段仅推进列计数
                }
            }
        }
        if (pts.Count == 0) return;

        int stride = pts.Count > VoxelMaxPoints ? (int)Math.Ceiling((double)pts.Count / VoxelMaxPoints) : 1;
        using var br = new SolidBrush(Color.FromArgb(170, VoxelColor));
        const float r = 2.0f;
        for (int i = 0; i < pts.Count; i += stride)
        {
            var p = pts[i];
            var sp = _cam.Project(new Vec3(p.X - _sceneCx, p.Y - _sceneCy, p.Z - _sceneCz), center);
            g.FillEllipse(br, sp.X - r, sp.Y - r, r * 2, r * 2);
        }
    }

    /// <summary>绘制评估叠加层：偏差热力线段 + 体素点云（与主路径共享相机/归一化中心）。</summary>
    private void DrawOverlay(Graphics g, PointF center)
    {
        if (_overlay == null) return;
        // 与主路径一致：减统一场景中心后投影（_sceneCx/Cy/Cz 已在 OnPaint 中算好）
        Vec3 Norm(Vec3 p) => new(p.X - _sceneCx, p.Y - _sceneCy, p.Z - _sceneCz);

        // 偏差热力线
        if (_overlay.Segments != null)
        {
            foreach (var seg in _overlay.Segments)
            {
                var pa = _cam.Project(Norm(seg.A), center);
                var pb = _cam.Project(Norm(seg.B), center);
                using var pen = new Pen(seg.Color, seg.Width);
                g.DrawLine(pen, pa, pb);
            }
        }

        // 体素点云（点数过多时等间隔抽样，避免卡顿）
        if (_overlay.VoxelPoints != null && _overlay.VoxelPoints.Count > 0)
        {
            int count = _overlay.VoxelPoints.Count;
            const int maxPoints = 8000;
            int stride = count > maxPoints ? (int)Math.Ceiling((double)count / maxPoints) : 1;
            for (int i = 0; i < count; i += stride)
            {
                var vp = _overlay.VoxelPoints[i];
                var pp = _cam.Project(Norm(vp.Center), center);
                using var br = new SolidBrush(Color.FromArgb(170, vp.Color));
                g.FillEllipse(br, pp.X - 1.6f, pp.Y - 1.6f, 3.2f, 3.2f);
            }
        }
    }

    private Color GetColor(GcodeMove m)
    {
        // G0 空行程：固定中性灰虚线，不随工具/层着色，
        // 与 G1 的 T0(蓝)/T1(红) 明确区分（对应"说明"框中"G0=空行程(灰虚线)"）。
        if (m.Type == MoveType.G0) return Color.LightSeaGreen;

        if (ColorByLayer)
        {
            // 按 HSV 循环取色
            double hue = (m.Layer * 47.0) % 360.0;
            return ColorFromHSV(hue, 0.85, 0.9);
        }
        if (!ColorByTool) return Color.LimeGreen;
        //
        return m.Tool switch
        {
            0 => Color.Red,      // T0=红
            1 => Color.Blue,     // T1=蓝
            _ => Color.LimeGreen,
        };
    }

    private static void DrawMarker(Graphics g, PointF p, Color c, float r)
    {
        using var b = new SolidBrush(c);
        g.FillEllipse(b, p.X - r, p.Y - r, r * 2, r * 2);
    }

    private static void DrawRing(Graphics g, PointF p, Color c, float r)
    {
        using var pen = new Pen(c, 1.6f);
        g.DrawEllipse(pen, p.X - r, p.Y - r, r * 2, r * 2);
    }

    private void DrawHint(Graphics g, string text)
    {
        // 白底下用深灰文字，保证对比度
        using var b = new SolidBrush(Color.FromArgb(90, 90, 90));
        using var f = new Font("Microsoft YaHei UI", 11F);
        var sz = g.MeasureString(text, f);
        g.DrawString(text, f, b, (Width - sz.Width) / 2, (Height - sz.Height) / 2);
    }

    private static Color ColorFromHSV(double hue, double saturation, double value)
    {
        int hi = (int)(hue / 60.0) % 6;
        double f = hue / 60.0 - Math.Floor(hue / 60.0);
        double v = value;
        double p = v * (1 - saturation);
        double q = v * (1 - f * saturation);
        double t = v * (1 - (1 - f) * saturation);
        (double r, double gg, double bb) = hi switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
        return Color.FromArgb((int)(r * 255), (int)(gg * 255), (int)(bb * 255));
    }
}

/// <summary>评估叠加层数据：偏差热力线段 + 体素点云（坐标与世界路径同系，按主路径包围盒居中）。</summary>
public sealed class Overlay3D
{
    /// <summary>偏差热力线段集合：起点、终点、颜色（按偏差值映射）、线宽。</summary>
    public List<(Vec3 A, Vec3 B, Color Color, float Width)> Segments { get; set; } = new();

    /// <summary>体素点云：每个占据体素的中心点 + 颜色（按材料/工具上色）。</summary>
    public List<(Vec3 Center, Color Color)> VoxelPoints { get; set; } = new();
}
