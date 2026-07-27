using System.Globalization;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using GcodeViewer.Evaluation;
using GcodeViewer.Models;
using GcodeViewer.Parsing;
using GcodeViewer.Rendering;

namespace GcodeViewer.Forms;

/// <summary>
/// 多 CSV 路径评估窗体：导入多条路径，逐条计算点集密度与体素 0/1 体积（仅 G1，按材料细分），
/// 并以选定基准为准计算其余各条的路径偏差（平均/最大/RMS/标准差/双向 Hausdorff）。
/// 结果用表格展示、一键导出 CSV；右侧内嵌 Viewport3D 预览，叠加偏差热力或体素点云。
/// 控件/布局/事件见 EvaluationForm.Designer.cs（可在 VS 设计器可视化编辑）；与主窗体编辑功能相互独立。
/// </summary>
public sealed partial class EvaluationForm : Form
{
    /// <summary>一条已导入路径的全部评估结果。</summary>
    private sealed class PathEntry
    {
        public string FilePath = "";
        public ParsedGcode Parsed = null!;
        public Path3D Path = null!;
        public PathMetrics Metrics = null!;     // 导入时即算
        public VoxelStats? Voxel;               // 点"计算"后填充
        public DeviationMetrics? Deviation;     // 选定基准后填充（基准本身为 null）
        public double CachedVoxelSize = -1;     // 已缓存的体素尺寸；-1=无
        public List<(Vec3 Center, int Tool)>? CachedCenters;  // 体素点云缓存（与 CachedVoxelSize 配对）
    }

    private readonly List<PathEntry> _entries = new();
    private int _referenceIndex = -1;   // 基准在 _entries 中的下标；-1 = 无基准
    private int _selectedIndex = -1;    // dgv 当前选中行对应的 _entries 下标
    private int _previewedReference = -2; // 当前预览背景已设的基准下标；-2=未初始化。避免重复 SetGcode 重置视角
    private bool _suppressPreview;      // 表格填充期间抑制选中事件触发的预览刷新
    private bool _busy;                 // 后台计算进行中（重入保护）
    private PathEntry? _distTarget, _distRef;  // 偏差热力段距离缓存对应的 (候选,基准)
    private double[]? _cachedDists;     // 偏差热力段中点距离缓存（避免每次刷新重算 O(m_cand·m_ref)）

    // 控件字段声明见 EvaluationForm.Designer.cs（设计器分部文件，VS 设计器里可视化编辑）

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public EvaluationForm()
    {
        // 控件、布局与事件均由 InitializeComponent（Designer）创建与绑定
        InitializeComponent();
        // 运行期再设 SplitterDistance（构造期 Panel2 宽度为 0）
        Load += (s, e) =>
        {
            _split.SplitterDistance = Math.Max(620, _split.Width - 460);
        };
    }

    // ===================== 事件处理（事件已在 Designer 中以 += new EventHandler 标准形式绑定）=====================
    private void OnRefChanged(object? sender, EventArgs e)
    {
        // 下拉首项"(无基准)"对应 -1，其余按序对应 _entries
        _referenceIndex = _cmbRef.SelectedIndex <= 0 ? -1 : _cmbRef.SelectedIndex - 1;
        // 偏差计算量随路径规模达 O(n·m)，放后台线程避免界面假死
        _ = RecomputeDeviationAsync();
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressPreview) return;
        if (_dgv.CurrentRow?.Tag is int idx) { _selectedIndex = idx; RefreshPreview(); }
    }

    private void OnModeChanged(object? sender, EventArgs e) => RefreshPreview();

    // ===================== 导入 / 删除 =====================
    private void OnAdd(object? s, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "路径 CSV|*.csv|所有文件|*.*",
            Multiselect = true,
            Title = "选择一个或多个 CSV 路径文件",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        int added = 0;
        foreach (var f in dlg.FileNames)
        {
            try
            {
                var parsed = CsvPathReader.Parse(f);
                var path = Path3D.FromParsed(parsed);
                _entries.Add(new PathEntry
                {
                    FilePath = f,
                    Parsed = parsed,
                    Path = path,
                    Metrics = PathEvaluator.ComputeDensity(path),
                });
                added++;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"导入失败 {Path.GetFileName(f)}：\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        if (added <= 0) return;
        RebuildReferenceCombo();
        RefillGrid();
        if (_entries.Count > 0)
        {
            int selectIdx = _entries.Count - added;   // 选中新加入的第一条
            SelectRowByEntry(selectIdx);
        }
        RefreshPreview();
        UpdateStatus();
    }

    private void OnRemove(object? s, EventArgs e)
    {
        if (_selectedIndex < 0 || _selectedIndex >= _entries.Count) return;
        int removed = _selectedIndex;
        _entries.RemoveAt(removed);
        _selectedIndex = -1;
        if (_referenceIndex == removed) _referenceIndex = -1;
        else if (_referenceIndex > removed) _referenceIndex--;
        RebuildReferenceCombo();
        RefillGrid();
        RefreshPreview();
        UpdateStatus();
    }

    private void OnClear(object? s, EventArgs e)
    {
        _entries.Clear();
        _referenceIndex = -1;
        _selectedIndex = -1;
        RebuildReferenceCombo();
        RefillGrid();
        RefreshPreview();
        UpdateStatus();
    }

    // ===================== 计算 =====================
    private async void OnCompute(object? s, EventArgs e)
    {
        if (_entries.Count == 0) { MessageBox.Show(this, "请先添加 CSV 路径文件。"); return; }
        if (_busy) return;
        double voxel = (double)_numVoxel.Value;
        double step = (double)_numStep.Value;
        int refIdx = _referenceIndex;
        var entries = _entries;   // 捕获到局部，供后台线程安全访问

        SetBusy(true);
        _status.Text = "计算中（密度 / 体素 / 偏差），请稍候…";
        try
        {
            await Task.Run(() =>
            {
                foreach (var en in entries)
                {
                    en.Metrics = PathEvaluator.ComputeDensity(en.Path);
                    en.Voxel = PathEvaluator.Voxelize(en.Path, voxel);
                    // 顺带预算体素点云并缓存（后台），切到点云模式时零延迟
                    en.CachedCenters = PathEvaluator.OccupiedCenters(en.Path, voxel);
                    en.CachedVoxelSize = voxel;
                }
                ComputeDeviations(entries, refIdx, step);
            });
            if (IsDisposed) return;   // 计算期间窗体已关闭则放弃 UI 更新
            RefillGrid();
            if (_selectedIndex >= 0) SelectRowByEntry(_selectedIndex);
            RefreshPreview();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "计算失败：\n" + ex.Message + "\n\n建议增大体素尺寸。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
            UpdateStatus();
        }
    }

    /// <summary>以当前基准异步重算所有偏差。基准变更触发；计算在后台线程，完成后回 UI 刷新表格与预览。</summary>
    private async Task RecomputeDeviationAsync()
    {
        if (_busy) return;
        if (_referenceIndex < 0)
        {
            foreach (var en in _entries) en.Deviation = null;
            RefillGrid();
            RefreshPreview();
            UpdateStatus();
            return;
        }
        double step = (double)_numStep.Value;
        int refIdx = _referenceIndex;
        var entries = _entries;

        SetBusy(true);
        _status.Text = "计算偏差中，请稍候…";
        try
        {
            await Task.Run(() => ComputeDeviations(entries, refIdx, step));
            if (IsDisposed) return;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "偏差计算失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;   // finally 已复位 _busy；异常不外传
        }
        finally
        {
            SetBusy(false);
        }
        RefillGrid();
        if (_selectedIndex >= 0) SelectRowByEntry(_selectedIndex);
        RefreshPreview();
        UpdateStatus();
    }

    /// <summary>纯函数：对每条候选相对基准计算偏差。不访问 UI，可安全在后台线程执行。</summary>
    private static void ComputeDeviations(List<PathEntry> entries, int refIdx, double step)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (refIdx < 0 || i == refIdx) { entries[i].Deviation = null; continue; }
            entries[i].Deviation = PathEvaluator.Deviation(entries[i].Path, entries[refIdx].Path, step);
        }
    }

    /// <summary>后台计算期间禁用所有可触发重算的控件，并切换等待光标（重入保护）。</summary>
    private void SetBusy(bool busy)
    {
        _busy = busy;
        if (IsDisposed) return;   // 窗体已释放则不再触碰控件
        _btnAdd.Enabled = _btnRemove.Enabled = _btnClear.Enabled = _btnCompute.Enabled = _btnExport.Enabled = !busy;
        _cmbRef.Enabled = !busy;
        _numVoxel.Enabled = _numStep.Enabled = !busy;
        _dgv.Enabled = !busy;
        UseWaitCursor = busy;
    }

    // ===================== 表格 =====================
    private void RefillGrid()
    {
        _suppressPreview = true;   // 填充期间抑制选中事件触发的预览刷新（避免反复 SetGcode 重置视角）
        _dgv.Rows.Clear();
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            var v = e.Voxel;
            var d = e.Deviation;
            int row = _dgv.Rows.Add(
                e.Path.FileName,
                e.Metrics.PointCount,
                e.Metrics.PrintLength.ToString("0.###", Inv),
                e.Metrics.LinearDensity.ToString("0.000", Inv),
                v != null ? v.OccupiedVolume.ToString("0.##", Inv) : "-",
                v != null ? v.EmptyVolume.ToString("0.##", Inv) : "-",
                v != null ? v.OccupancyRatio.ToString("0.00", Inv) : "-",
                VoxelVolumeOf(v, 0),
                VoxelVolumeOf(v, 1),
                v != null ? v.MixedCount.ToString(Inv) : "-",
                e.Parsed.Stats.ToolChangeCount,
                Fmt(d?.MeanDev),
                Fmt(d?.MaxDev),
                Fmt(d?.RmsDev),
                Fmt(d?.Hausdorff)
            );
            _dgv.Rows[row].Tag = i;
            if (i == _referenceIndex) _dgv.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(50, 55, 70);
        }
        _suppressPreview = false;
    }

    private static string VoxelVolumeOf(VoxelStats? v, int tool) =>
        v != null && v.PerTool.TryGetValue(tool, out var tv) ? tv.Volume.ToString("0.##", Inv) : "-";

    private static string Fmt(double? d) => d.HasValue ? d.Value.ToString("0.###", Inv) : "-";

    private void SelectRowByEntry(int entryIdx)
    {
        foreach (DataGridViewRow r in _dgv.Rows)
            if (r.Tag is int idx && idx == entryIdx) { r.Selected = true; _selectedIndex = entryIdx; break; }
    }

    private void RebuildReferenceCombo()
    {
        string prev = _cmbRef.SelectedIndex > 0 && _cmbRef.SelectedIndex - 1 < _entries.Count
            ? _entries[_cmbRef.SelectedIndex - 1].FilePath : "";
        _cmbRef.Items.Clear();
        _cmbRef.Items.Add("(无基准)");
        foreach (var e in _entries) _cmbRef.Items.Add(e.Path.FileName);
        // 尝试恢复原选择
        int newRef = -1;
        if (!string.IsNullOrEmpty(prev))
            newRef = _entries.FindIndex(x => x.FilePath == prev);
        _cmbRef.SelectedIndex = newRef >= 0 ? newRef + 1 : 0;
        _referenceIndex = newRef;
    }

    // ===================== 预览 =====================
    private void RefreshPreview()
    {
        if (_referenceIndex < 0 || _referenceIndex >= _entries.Count)
        {
            if (_previewedReference != -1) { _preview.SetGcode(null); _preview.SetOverlay(null); }
            _previewedReference = -1;
            return;
        }
        var reference = _entries[_referenceIndex];
        // 仅在基准变化时才 SetGcode（其内部会 ResetView 重置视角），避免选中行/模式切换时反复重置
        if (_previewedReference != _referenceIndex)
        {
            _preview.SetGcode(reference.Parsed);   // 背景：基准路径
            _previewedReference = _referenceIndex;
        }
        int target = (_selectedIndex >= 0 && _selectedIndex < _entries.Count) ? _selectedIndex : _referenceIndex;
        _preview.SetOverlay(BuildOverlay(_entries[target], reference));
    }

    /// <summary>按当前模式（偏差热力 / 体素点云）构建叠加层。</summary>
    private Overlay3D BuildOverlay(PathEntry target, PathEntry reference)
    {
        var ov = new Overlay3D();
        if (_rbVoxel.Checked)
        {
            // 体素点云：占据体素中心，按材料上色。命中缓存则复用，避免每次预览刷新重算体素化
            double voxel = (double)_numVoxel.Value;
            var centers = target.CachedCenters;
            if (centers == null || target.CachedVoxelSize != voxel)
            {
                centers = PathEvaluator.OccupiedCenters(target.Path, voxel);
                target.CachedCenters = centers;
                target.CachedVoxelSize = voxel;
            }
            foreach (var c in centers)
                ov.VoxelPoints.Add((c.Center, ToolColor(c.Tool)));
        }
        else
        {
            // 偏差热力：候选 G1 段按"段中点到基准折线距离"映射颜色 蓝(0)→红(max)
            var refSegs = reference.Path.Segments;
            if (refSegs.Length == 0) return ov;
            var segs = target.Path.Segments;
            // 段距离数组按 (target,reference) 缓存——重计算 O(m_cand·m_ref) 只算一次；颜色按缓存 max 轻算
            double[] dists;
            if (_distTarget == target && _distRef == reference && _cachedDists?.Length == segs.Length)
            {
                dists = _cachedDists!;
            }
            else
            {
                dists = new double[segs.Length];
                for (int i = 0; i < segs.Length; i++)
                {
                    Vec3 mid = new((segs[i].A.X + segs[i].B.X) * 0.5,
                                   (segs[i].A.Y + segs[i].B.Y) * 0.5,
                                   (segs[i].A.Z + segs[i].B.Z) * 0.5);
                    dists[i] = PathEvaluator.PointToPolylineDistance(mid, refSegs);
                }
                _distTarget = target; _distRef = reference; _cachedDists = dists;
            }
            // 色阶上界优先复用后台已算的偏差 MaxDev；无则现场取 dists 最大值
            double max = target.Deviation?.MaxDev ?? 0;
            if (max < 1e-9) { foreach (var d in dists) if (d > max) max = d; }
            if (max < 1e-9) max = 1;
            for (int i = 0; i < segs.Length; i++)
                ov.Segments.Add((segs[i].A, segs[i].B, DeviationColor(dists[i], max), 1.8f));
        }
        return ov;
    }

    /// <summary>偏差值→颜色：淡蓝(0, 偏差小, 冷端) → 深红(1, 偏差大, 暖端)。max 为色阶上界。</summary>
    private static Color DeviationColor(double d, double max)
    {
        double r = max > 1e-9 ? Math.Clamp(d / max, 0.0, 1.0) : 0;
        // 两端：淡蓝 LightBlue(173,216,230) — 深红 DarkRed(139,0,0)，RGB 三通道线性插值
        int red = (int)(173 + (139 - 173) * r);
        int green = (int)(216 + (0 - 216) * r);
        int blue = (int)(230 + (0 - 230) * r);
        return Color.FromArgb(red, green, blue);
    }

    private static Color ToolColor(int tool) => tool switch
    {
        0 => Color.Red,      // T0=红
        1 => Color.Blue,     // T1=蓝
        _ => Color.LimeGreen,
    };

    // ===================== 导出 =====================
    private void OnExport(object? s, EventArgs e)
    {
        if (_entries.Count == 0) { MessageBox.Show(this, "无可导出数据。"); return; }
        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "path_evaluation.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("文件,点数,G1点数,总长mm,G1长mm,G0长mm,线密度(点/mm),平均点间距mm,体积密度(点/mm3)," +
                          "占据体积mm3,空闲体积mm3,占据率%,T0体积mm3,T1体积mm3,混合体素,切换次数," +
                          "平均偏差mm,最大偏差mm,RMS偏差mm,标准差mm,Hausdorff_mm");
            foreach (var en in _entries)
            {
                var m = en.Metrics;
                var v = en.Voxel;
                var d = en.Deviation;
                sb.Append(CsvCell(m.FileName ?? "")).Append(',');
                sb.Append(m.PointCount).Append(',');
                sb.Append(m.G1PointCount).Append(',');
                sb.Append(F(m.TotalLength)).Append(',');
                sb.Append(F(m.PrintLength)).Append(',');
                sb.Append(F(m.TravelLength)).Append(',');
                sb.Append(F(m.LinearDensity)).Append(',');
                sb.Append(F(m.AvgPointSpacing)).Append(',');
                sb.Append(F(m.VolumeDensity)).Append(',');
                sb.Append(v != null ? F(v.OccupiedVolume) : "").Append(',');
                sb.Append(v != null ? F(v.EmptyVolume) : "").Append(',');
                sb.Append(v != null ? F(v.OccupancyRatio) : "").Append(',');
                sb.Append(VoxelVolumeOf(v, 0)).Append(',');
                sb.Append(VoxelVolumeOf(v, 1)).Append(',');
                sb.Append(v != null ? v.MixedCount.ToString(Inv) : "").Append(',');
                sb.Append(en.Parsed.Stats.ToolChangeCount).Append(',');
                sb.Append(d != null ? F(d.MeanDev) : "").Append(',');
                sb.Append(d != null ? F(d.MaxDev) : "").Append(',');
                sb.Append(d != null ? F(d.RmsDev) : "").Append(',');
                sb.Append(d != null ? F(d.StdDev) : "").Append(',');
                sb.Append(d != null ? F(d.Hausdorff) : "");
                sb.AppendLine();
            }
            // UTF8Encoding(true) 会在文件头自动写 BOM，Excel/Origin 打开中文不乱码
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            MessageBox.Show(this, "已导出：\n" + dlg.FileName, "导出");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string F(double v) => v.ToString("0.######", Inv);
    /// <summary>CSV 单元格转义：含逗号/引号/换行则用双引号包裹并把内部引号翻倍。</summary>
    private static string CsvCell(string s)
    {
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private void UpdateStatus()
    {
        if (IsDisposed) return;
        if (_entries.Count == 0) { _status.Text = "请添加 CSV 路径文件"; return; }
        long totalCells = 0;
        long occupied = 0;
        foreach (var en in _entries) if (en.Voxel != null) { totalCells += en.Voxel.TotalCells; occupied += en.Voxel.OccupiedCount; }
        string basis = _referenceIndex >= 0 ? $"基准={_entries[_referenceIndex].Path.FileName}" : "未设基准";
        _status.Text = $"路径 {_entries.Count} 条 · {basis} · 占据体素 {occupied} / 总 {totalCells}";
    }

}
