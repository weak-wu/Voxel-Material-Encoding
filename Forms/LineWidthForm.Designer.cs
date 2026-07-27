using System.Drawing;
using System.Windows.Forms;
using ScottPlot.WinForms;

namespace GcodeViewer.Forms;

/// <summary>
/// 打印线宽提取窗体的设计器分部文件。
/// 遵循 WinForms 设计器可解析的标准模式：InitializeComponent 为扁平 new + 逐属性赋值序列，
/// 事件用方法组（如 _btn.Click += OnHandler;）写在 InitializeComponent 内，不在主文件用 lambda。
/// 控件/布局/事件均在此声明，VS 设计器可直接可视化编辑。
/// 左侧参数面板（HSV/形态学/连通域/标定/平滑），右侧上方预览、下方 ScottPlot 折线图。
/// </summary>
public sealed partial class LineWidthForm
{
    // 顶层
    private SplitContainer _split;          // 左参数 / 右预览+图
    private SplitContainer _splitRight;     // 右侧：上预览 / 下折线图

    // 左侧：加载与文件名
    private Button _btnLoad;
    private Label _lblFileName;

    // 颜色分割(HSV)
    private GroupBox _grpHsv;
    private Label _lblHlo; private NumericUpDown _numHlo;
    private Label _lblHhi; private NumericUpDown _numHhi;
    private Label _lblSlo; private NumericUpDown _numSlo;
    private Label _lblShi; private NumericUpDown _numShi;
    private Label _lblVlo; private NumericUpDown _numVlo;
    private Label _lblVhi; private NumericUpDown _numVhi;

    // 数值参数
    private Label _lblMorph; private NumericUpDown _numMorph;
    private Label _lblMinArea; private NumericUpDown _numMinArea;
    private Label _lblPxPerMm; private NumericUpDown _numPxPerMm;
    private Label _lblSmooth; private NumericUpDown _numSmooth;

    // 操作与结果
    private Button _btnMeasure;
    private Button _btnExport;
    private CheckBox _chkManual;
    private CheckBox _chkRoi;
    private Label _lblStats;

    // 右侧预览
    private Label _lblPreview;
    private ComboBox _cboPreview;
    private PictureBox _picPreview;

    // 折线图（ScottPlot）
    private FormsPlot _plot;

    private System.ComponentModel.IContainer components = null;

    private void InitializeComponent()
    {
        _split = new SplitContainer();
        _lblStats = new Label();
        _btnExport = new Button();
        _btnMeasure = new Button();
        _chkManual = new CheckBox();
        _chkRoi = new CheckBox();
        _numSmooth = new NumericUpDown();
        _lblSmooth = new Label();
        _numPxPerMm = new NumericUpDown();
        _lblPxPerMm = new Label();
        _numMinArea = new NumericUpDown();
        _lblMinArea = new Label();
        _numMorph = new NumericUpDown();
        _lblMorph = new Label();
        _grpHsv = new GroupBox();
        _lblHlo = new Label();
        _numHlo = new NumericUpDown();
        _lblHhi = new Label();
        _numHhi = new NumericUpDown();
        _lblSlo = new Label();
        _numSlo = new NumericUpDown();
        _lblShi = new Label();
        _numShi = new NumericUpDown();
        _lblVlo = new Label();
        _numVlo = new NumericUpDown();
        _lblVhi = new Label();
        _numVhi = new NumericUpDown();
        _lblFileName = new Label();
        _btnLoad = new Button();
        _splitRight = new SplitContainer();
        _picPreview = new PictureBox();
        _cboPreview = new ComboBox();
        _lblPreview = new Label();
        _plot = new FormsPlot();
        ((System.ComponentModel.ISupportInitialize)_split).BeginInit();
        _split.Panel1.SuspendLayout();
        _split.Panel2.SuspendLayout();
        _split.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numSmooth).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numPxPerMm).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numMinArea).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numMorph).BeginInit();
        _grpHsv.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numHlo).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numHhi).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numSlo).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numShi).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numVlo).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numVhi).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_splitRight).BeginInit();
        _splitRight.Panel1.SuspendLayout();
        _splitRight.Panel2.SuspendLayout();
        _splitRight.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_picPreview).BeginInit();
        SuspendLayout();
        // 
        // _split
        // 
        _split.Dock = DockStyle.Fill;
        _split.FixedPanel = FixedPanel.Panel1;
        _split.Location = new Point(0, 0);
        _split.Name = "_split";
        // 
        // _split.Panel1
        // 
        _split.Panel1.Controls.Add(_lblStats);
        _split.Panel1.Controls.Add(_btnExport);
        _split.Panel1.Controls.Add(_btnMeasure);
        _split.Panel1.Controls.Add(_chkManual);
        _split.Panel1.Controls.Add(_chkRoi);
        _split.Panel1.Controls.Add(_numSmooth);
        _split.Panel1.Controls.Add(_lblSmooth);
        _split.Panel1.Controls.Add(_numPxPerMm);
        _split.Panel1.Controls.Add(_lblPxPerMm);
        _split.Panel1.Controls.Add(_numMinArea);
        _split.Panel1.Controls.Add(_lblMinArea);
        _split.Panel1.Controls.Add(_numMorph);
        _split.Panel1.Controls.Add(_lblMorph);
        _split.Panel1.Controls.Add(_grpHsv);
        _split.Panel1.Controls.Add(_lblFileName);
        _split.Panel1.Controls.Add(_btnLoad);
        _split.Panel1MinSize = 360;
        // 
        // _split.Panel2
        // 
        _split.Panel2.Controls.Add(_splitRight);
        _split.Size = new Size(1180, 760);
        _split.SplitterDistance = 390;
        _split.TabIndex = 0;
        // 
        // _lblStats
        // 
        _lblStats.BackColor = Color.FromArgb(248, 248, 248);
        _lblStats.BorderStyle = BorderStyle.FixedSingle;
        _lblStats.Font = new Font("Consolas", 9F);
        _lblStats.Location = new Point(8, 416);
        _lblStats.Name = "_lblStats";
        _lblStats.Size = new Size(356, 300);
        _lblStats.TabIndex = 0;
        _lblStats.Text = "（加载图像并点击「测量」后在此显示统计结果）";
        // 
        // _btnExport
        // 
        _btnExport.AutoSize = true;
        _btnExport.FlatStyle = FlatStyle.System;
        _btnExport.Location = new Point(96, 384);
        _btnExport.Name = "_btnExport";
        _btnExport.Size = new Size(75, 26);
        _btnExport.TabIndex = 1;
        _btnExport.Text = "导出CSV";
        _btnExport.Click += OnExport;
        // 
        // _btnMeasure
        // 
        _btnMeasure.AutoSize = true;
        _btnMeasure.FlatStyle = FlatStyle.System;
        _btnMeasure.Location = new Point(8, 384);
        _btnMeasure.Name = "_btnMeasure";
        _btnMeasure.Size = new Size(75, 26);
        _btnMeasure.TabIndex = 2;
        _btnMeasure.Text = "测量";
        _btnMeasure.Click += OnMeasure;
        // 
        // _chkManual
        // 
        _chkManual.AutoSize = true;
        _chkManual.Location = new Point(180, 389);
        _chkManual.Name = "_chkManual";
        _chkManual.Size = new Size(107, 21);
        _chkManual.TabIndex = 3;
        _chkManual.Text = "手动测量(两点)";
        _chkManual.CheckedChanged += OnManualToggle;
        //
        // _chkRoi
        //
        _chkRoi.AutoSize = true;
        _chkRoi.Location = new Point(290, 389);
        _chkRoi.Name = "_chkRoi";
        _chkRoi.Text = "框选取线";
        _chkRoi.CheckedChanged += OnRoiToggle;
        // 
        // _numSmooth
        // 
        _numSmooth.Location = new Point(180, 348);
        _numSmooth.Maximum = new decimal(new int[] { 999, 0, 0, 0 });
        _numSmooth.Name = "_numSmooth";
        _numSmooth.Size = new Size(56, 23);
        _numSmooth.TabIndex = 3;
        _numSmooth.ValueChanged += OnParamChanged;
        // 
        // _lblSmooth
        // 
        _lblSmooth.AutoSize = true;
        _lblSmooth.Location = new Point(8, 352);
        _lblSmooth.Name = "_lblSmooth";
        _lblSmooth.Size = new Size(110, 17);
        _lblSmooth.TabIndex = 4;
        _lblSmooth.Text = "平滑窗口(列,0=关):";
        // 
        // _numPxPerMm
        // 
        _numPxPerMm.DecimalPlaces = 2;
        _numPxPerMm.Location = new Point(180, 318);
        _numPxPerMm.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
        _numPxPerMm.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _numPxPerMm.Name = "_numPxPerMm";
        _numPxPerMm.Size = new Size(64, 23);
        _numPxPerMm.TabIndex = 5;
        _numPxPerMm.Value = new decimal(new int[] { 10, 0, 0, 0 });
        _numPxPerMm.ValueChanged += OnParamChanged;
        // 
        // _lblPxPerMm
        // 
        _lblPxPerMm.AutoSize = true;
        _lblPxPerMm.Location = new Point(8, 322);
        _lblPxPerMm.Name = "_lblPxPerMm";
        _lblPxPerMm.Size = new Size(80, 17);
        _lblPxPerMm.TabIndex = 6;
        _lblPxPerMm.Text = "标定 px/mm:";
        // 
        // _numMinArea
        // 
        _numMinArea.Location = new Point(180, 288);
        _numMinArea.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
        _numMinArea.Name = "_numMinArea";
        _numMinArea.Size = new Size(80, 23);
        _numMinArea.TabIndex = 7;
        _numMinArea.Value = new decimal(new int[] { 100, 0, 0, 0 });
        _numMinArea.ValueChanged += OnParamChanged;
        // 
        // _lblMinArea
        // 
        _lblMinArea.AutoSize = true;
        _lblMinArea.Location = new Point(8, 292);
        _lblMinArea.Name = "_lblMinArea";
        _lblMinArea.Size = new Size(122, 17);
        _lblMinArea.TabIndex = 8;
        _lblMinArea.Text = "最小连通域面积(px²):";
        // 
        // _numMorph
        // 
        _numMorph.Location = new Point(180, 258);
        _numMorph.Maximum = new decimal(new int[] { 15, 0, 0, 0 });
        _numMorph.Name = "_numMorph";
        _numMorph.Size = new Size(56, 23);
        _numMorph.TabIndex = 9;
        _numMorph.Value = new decimal(new int[] { 3, 0, 0, 0 });
        _numMorph.ValueChanged += OnParamChanged;
        // 
        // _lblMorph
        // 
        _lblMorph.AutoSize = true;
        _lblMorph.Location = new Point(8, 262);
        _lblMorph.Name = "_lblMorph";
        _lblMorph.Size = new Size(120, 17);
        _lblMorph.TabIndex = 10;
        _lblMorph.Text = "形态学核(0关,3/5/7):";
        // 
        // _grpHsv
        // 
        _grpHsv.Controls.Add(_lblHlo);
        _grpHsv.Controls.Add(_numHlo);
        _grpHsv.Controls.Add(_lblHhi);
        _grpHsv.Controls.Add(_numHhi);
        _grpHsv.Controls.Add(_lblSlo);
        _grpHsv.Controls.Add(_numSlo);
        _grpHsv.Controls.Add(_lblShi);
        _grpHsv.Controls.Add(_numShi);
        _grpHsv.Controls.Add(_lblVlo);
        _grpHsv.Controls.Add(_numVlo);
        _grpHsv.Controls.Add(_lblVhi);
        _grpHsv.Controls.Add(_numVhi);
        _grpHsv.Location = new Point(8, 40);
        _grpHsv.Name = "_grpHsv";
        _grpHsv.Size = new Size(356, 212);
        _grpHsv.TabIndex = 11;
        _grpHsv.TabStop = false;
        _grpHsv.Text = "颜色分割 HSV（H=色相0-180, S=饱和度0-255, V=明度0-255）";
        // 
        // _lblHlo
        // 
        _lblHlo.AutoSize = true;
        _lblHlo.Location = new Point(6, 36);
        _lblHlo.Name = "_lblHlo";
        _lblHlo.Size = new Size(76, 17);
        _lblHlo.TabIndex = 0;
        _lblHlo.Text = "色相 H 下限:";
        // 
        // _numHlo
        // 
        _numHlo.Location = new Point(106, 32);
        _numHlo.Maximum = new decimal(new int[] { 180, 0, 0, 0 });
        _numHlo.Name = "_numHlo";
        _numHlo.Size = new Size(60, 23);
        _numHlo.TabIndex = 1;
        _numHlo.ValueChanged += OnParamChanged;
        // 
        // _lblHhi
        // 
        _lblHhi.AutoSize = true;
        _lblHhi.Location = new Point(6, 66);
        _lblHhi.Name = "_lblHhi";
        _lblHhi.Size = new Size(76, 17);
        _lblHhi.TabIndex = 2;
        _lblHhi.Text = "色相 H 上限:";
        // 
        // _numHhi
        // 
        _numHhi.Location = new Point(106, 62);
        _numHhi.Maximum = new decimal(new int[] { 180, 0, 0, 0 });
        _numHhi.Name = "_numHhi";
        _numHhi.Size = new Size(60, 23);
        _numHhi.TabIndex = 3;
        _numHhi.Value = new decimal(new int[] { 180, 0, 0, 0 });
        _numHhi.ValueChanged += OnParamChanged;
        // 
        // _lblSlo
        // 
        _lblSlo.AutoSize = true;
        _lblSlo.Location = new Point(6, 96);
        _lblSlo.Name = "_lblSlo";
        _lblSlo.Size = new Size(74, 17);
        _lblSlo.TabIndex = 4;
        _lblSlo.Text = "饱和 S 下限:";
        // 
        // _numSlo
        // 
        _numSlo.Location = new Point(106, 92);
        _numSlo.Maximum = new decimal(new int[] { 255, 0, 0, 0 });
        _numSlo.Name = "_numSlo";
        _numSlo.Size = new Size(60, 23);
        _numSlo.TabIndex = 5;
        _numSlo.ValueChanged += OnParamChanged;
        // 
        // _lblShi
        // 
        _lblShi.AutoSize = true;
        _lblShi.Location = new Point(6, 126);
        _lblShi.Name = "_lblShi";
        _lblShi.Size = new Size(74, 17);
        _lblShi.TabIndex = 6;
        _lblShi.Text = "饱和 S 上限:";
        // 
        // _numShi
        // 
        _numShi.Location = new Point(106, 122);
        _numShi.Maximum = new decimal(new int[] { 255, 0, 0, 0 });
        _numShi.Name = "_numShi";
        _numShi.Size = new Size(60, 23);
        _numShi.TabIndex = 7;
        _numShi.Value = new decimal(new int[] { 255, 0, 0, 0 });
        _numShi.ValueChanged += OnParamChanged;
        // 
        // _lblVlo
        // 
        _lblVlo.AutoSize = true;
        _lblVlo.Location = new Point(6, 156);
        _lblVlo.Name = "_lblVlo";
        _lblVlo.Size = new Size(75, 17);
        _lblVlo.TabIndex = 8;
        _lblVlo.Text = "明度 V 下限:";
        // 
        // _numVlo
        // 
        _numVlo.Location = new Point(106, 152);
        _numVlo.Maximum = new decimal(new int[] { 255, 0, 0, 0 });
        _numVlo.Name = "_numVlo";
        _numVlo.Size = new Size(60, 23);
        _numVlo.TabIndex = 9;
        _numVlo.Value = new decimal(new int[] { 30, 0, 0, 0 });
        _numVlo.ValueChanged += OnParamChanged;
        // 
        // _lblVhi
        // 
        _lblVhi.AutoSize = true;
        _lblVhi.Location = new Point(6, 186);
        _lblVhi.Name = "_lblVhi";
        _lblVhi.Size = new Size(75, 17);
        _lblVhi.TabIndex = 10;
        _lblVhi.Text = "明度 V 上限:";
        // 
        // _numVhi
        // 
        _numVhi.Location = new Point(106, 182);
        _numVhi.Maximum = new decimal(new int[] { 255, 0, 0, 0 });
        _numVhi.Name = "_numVhi";
        _numVhi.Size = new Size(60, 23);
        _numVhi.TabIndex = 11;
        _numVhi.Value = new decimal(new int[] { 255, 0, 0, 0 });
        _numVhi.ValueChanged += OnParamChanged;
        // 
        // _lblFileName
        // 
        _lblFileName.AutoSize = true;
        _lblFileName.ForeColor = Color.DarkSlateGray;
        _lblFileName.Location = new Point(96, 14);
        _lblFileName.Name = "_lblFileName";
        _lblFileName.Size = new Size(52, 17);
        _lblFileName.TabIndex = 12;
        _lblFileName.Text = "(未加载)";
        // 
        // _btnLoad
        // 
        _btnLoad.AutoSize = true;
        _btnLoad.FlatStyle = FlatStyle.System;
        _btnLoad.Location = new Point(8, 10);
        _btnLoad.Margin = new Padding(0, 4, 8, 0);
        _btnLoad.Name = "_btnLoad";
        _btnLoad.Size = new Size(80, 26);
        _btnLoad.TabIndex = 13;
        _btnLoad.Text = "加载图像…";
        _btnLoad.Click += OnLoad;
        // 
        // _splitRight
        // 
        _splitRight.Dock = DockStyle.Fill;
        _splitRight.Location = new Point(0, 0);
        _splitRight.Name = "_splitRight";
        _splitRight.Orientation = Orientation.Horizontal;
        // 
        // _splitRight.Panel1
        // 
        _splitRight.Panel1.Controls.Add(_picPreview);
        _splitRight.Panel1.Controls.Add(_cboPreview);
        _splitRight.Panel1.Controls.Add(_lblPreview);
        _splitRight.Panel1MinSize = 80;
        // 
        // _splitRight.Panel2
        // 
        _splitRight.Panel2.Controls.Add(_plot);
        _splitRight.Panel2MinSize = 80;
        _splitRight.Size = new Size(786, 760);
        _splitRight.SplitterDistance = 539;
        _splitRight.TabIndex = 0;
        // 
        // _picPreview
        // 
        _picPreview.BackColor = Color.FromArgb(32, 32, 32);
        _picPreview.Dock = DockStyle.Fill;
        _picPreview.Location = new Point(0, 0);
        _picPreview.Name = "_picPreview";
        _picPreview.Size = new Size(786, 539);
        _picPreview.SizeMode = PictureBoxSizeMode.Zoom;
        _picPreview.TabIndex = 0;
        _picPreview.TabStop = false;
        _picPreview.Paint += OnPreviewPaint;
        _picPreview.MouseClick += OnPreviewMouseClick;
        _picPreview.MouseDown += OnPreviewMouseDown;
        _picPreview.MouseMove += OnPreviewMouseMove;
        _picPreview.MouseUp += OnPreviewMouseUp;
        // 
        // _cboPreview
        // 
        _cboPreview.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboPreview.Items.AddRange(new object[] { "原图", "原始掩膜", "最大连通域", "旋转后掩膜", "旋转后原图" });
        _cboPreview.Location = new Point(0, 0);
        _cboPreview.Name = "_cboPreview";
        _cboPreview.Size = new Size(121, 25);
        _cboPreview.TabIndex = 1;
        _cboPreview.SelectedIndexChanged += OnPreviewModeChanged;
        // 
        // _lblPreview
        // 
        _lblPreview.AutoSize = true;
        _lblPreview.Location = new Point(0, 0);
        _lblPreview.Name = "_lblPreview";
        _lblPreview.Size = new Size(35, 17);
        _lblPreview.TabIndex = 2;
        _lblPreview.Text = "预览:";
        // 
        // _plot
        // 
        _plot.Dock = DockStyle.Fill;
        _plot.Location = new Point(0, 0);
        _plot.Name = "_plot";
        _plot.Size = new Size(786, 217);
        _plot.TabIndex = 0;
        _plot.MouseClick += OnChartClick;
        // 
        // LineWidthForm
        // 
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(1180, 760);
        Controls.Add(_split);
        MinimumSize = new Size(900, 560);
        Name = "LineWidthForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "打印线宽提取 — HSV分割 / 水平校正 / 逐列测宽";
        _split.Panel1.ResumeLayout(false);
        _split.Panel1.PerformLayout();
        _split.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_split).EndInit();
        _split.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_numSmooth).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numPxPerMm).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numMinArea).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numMorph).EndInit();
        _grpHsv.ResumeLayout(false);
        _grpHsv.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numHlo).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numHhi).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numSlo).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numShi).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numVlo).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numVhi).EndInit();
        _splitRight.Panel1.ResumeLayout(false);
        _splitRight.Panel1.PerformLayout();
        _splitRight.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitRight).EndInit();
        _splitRight.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_picPreview).EndInit();
        ResumeLayout(false);
    }
}
