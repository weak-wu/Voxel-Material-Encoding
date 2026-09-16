using System.ComponentModel;
using System.Text;
using GcodeViewer.Services;

namespace GcodeViewer.Forms;

public partial class CfsGenerateForm
{
    private CancellationTokenSource? _toleranceCancellation;
    private readonly BindingList<CfsToleranceRow> _toleranceRows = new();
    private string _statisticsFileName = "容差统计.csv";

    private async void BtnTest_Click(object? sender, EventArgs e)
    {
        if (_toleranceCancellation != null)
        {
            _toleranceCancellation.Cancel();
            _btnTest.Enabled = false;
            _lblStatus.Text = "正在取消容差测试…";
            return;
        }
        if (_image == null)
        {
            MessageBox.Show(this, "请先导入图片。", "提示");
            return;
        }
        decimal start = _nudToleranceStart.Value;
        decimal end = _nudToleranceEnd.Value;
        decimal step = _nudToleranceStep.Value;
        int total;
        try { total = CfsToleranceStatistics.BuildTolerances(start, end, step).Count; }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, ex.Message, "参数错误");
            return;
        }

        var options = ReadOptions();
        using var image = new Bitmap(_image);
        using var cancellation = new CancellationTokenSource();
        _toleranceCancellation = cancellation;
        var enabledStates = _options.Controls.Cast<Control>()
            .Where(c => c != _btnTest).ToDictionary(c => c, c => c.Enabled);
        foreach (var control in enabledStates.Keys) control.Enabled = false;
        _btnTest.Text = "取消测试";
        _btnExportStatistics.Enabled = false;
        _toleranceRows.Clear();
        _statisticsFileName = Path.GetFileNameWithoutExtension(_imageFileName) + "_容差统计.csv";
        _outputTabs.SelectedTab = _statisticsTab;
        _lblStatus.Text = $"正在生成零容差基准，待测试 {total} 个容差值…";
        _statisticsHint.Text = $"{Path.GetFileName(_imageFileName)} | {options.SpiralType} | " +
            $"像素 {options.PixelSizeMm} mm，缩放 {options.ScaleFactor}，线距 {options.SpacingMm} mm，" +
            $"点距 {options.PointSpacingMm} mm，优化 {options.Optimize}\r\n" +
            "单层最终点数（含补点）；减少量 = 零容差点数 − 当前点数；结束容差始终包含。";
        var progress = new Progress<CfsToleranceRow>(row =>
        {
            if (IsDisposed || Disposing || _toleranceCancellation != cancellation || cancellation.IsCancellationRequested)
                return;
            _toleranceRows.Add(row);
            _lblStatus.Text = $"容差测试 {_toleranceRows.Count}/{total}：{row.ToleranceMm} mm，{row.PointCount} 点";
        });
        try
        {
            var rows = await Task.Run(() => CfsToleranceStatistics.Run(image, options,
                start, end, step, progress, cancellation.Token));
            if (IsDisposed || Disposing) return;
            // Use the completed result even if a queued progress notification is still pending.
            _toleranceRows.Clear();
            foreach (var row in rows) _toleranceRows.Add(row);
            _lblStatus.Text = $"容差测试完成：{rows.Count} 组，零容差基准 {rows[0].BaselinePointCount} 点。";
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && !Disposing) _lblStatus.Text = $"测试已取消，保留 {_toleranceRows.Count} 组已完成结果。";
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
            {
                _lblStatus.Text = $"测试失败，保留 {_toleranceRows.Count} 组已完成结果。";
                MessageBox.Show(this, ex.Message, "容差测试失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            _toleranceCancellation = null;
            if (!IsDisposed && !Disposing)
            {
                foreach (var (control, enabled) in enabledStates) control.Enabled = enabled;
                _btnTest.Text = "容差测试";
                _btnTest.Enabled = true;
                _btnExportStatistics.Enabled = _toleranceRows.Count > 0;
            }
        }
    }

    private void BtnExportStatistics_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog { Filter = "CSV 统计表|*.csv", FileName = _statisticsFileName };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var csv = new StringBuilder("简化容差(mm),路径条数,单层路径点数,零容差点数,较零容差减少点数,减少比例(%),耗时(ms)\r\n");
            foreach (var row in _toleranceRows)
                csv.AppendLine(FormattableString.Invariant($"{row.ToleranceMm},{row.PathCount},{row.PointCount},{row.BaselinePointCount},{row.Reduction},{row.ReductionPercent:F4},{row.ElapsedMs}"));
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
