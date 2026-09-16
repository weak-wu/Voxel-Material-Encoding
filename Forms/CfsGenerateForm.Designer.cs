namespace GcodeViewer.Forms;

partial class CfsGenerateForm
{
    private System.ComponentModel.IContainer components = null!;
    private GroupBox _options = null!;
    private Button _btnImport = null!;
    private Button _btnGenerate = null!;
    private Button _btnSave = null!;
    private NumericUpDown _nudPixelSize = null!;
    private NumericUpDown _nudSpacing = null!;
    private NumericUpDown _nudScaleFactor = null!;
    private NumericUpDown _nudLayers = null!;
    private NumericUpDown _nudLayerHeight = null!;
    private NumericUpDown _nudPointSpacing = null!;
    private NumericUpDown _nudSimplify = null!;
    private NumericUpDown _nudBoundary = null!;
    private ComboBox _cmbSpiralType = null!;
    private CheckBox _chkOptimize = null!;
    private CheckBox _chkPlot = null!;
    private CheckBox _chkGenerateGcode = null!;
    private Label _lblImage = null!;
    private Label _lblStatus = null!;
    private Label _lblSpiralType = null!;
    private Label _lblPixelSize = null!;
    private Label _lblSpacing = null!;
    private Label _lblScaleFactor = null!;
    private Label _lblLayers = null!;
    private Label _lblLayerHeight = null!;
    private Label _lblPointSpacing = null!;
    private Label _lblSimplify = null!;
    private Label _lblBoundary = null!;
    private PictureBox _picture = null!;
    private Panel _canvas = null!;
    private TextBox _txtGCode = null!;
    private SplitContainer _split = null!;
    private GroupBox _toleranceOptions = null!;
    private NumericUpDown _nudToleranceStart = null!;
    private NumericUpDown _nudToleranceEnd = null!;
    private NumericUpDown _nudToleranceStep = null!;
    private Label _lblToleranceStart = null!;
    private Label _lblToleranceEnd = null!;
    private Label _lblToleranceStep = null!;
    private Button _btnTest = null!;
    private TabControl _outputTabs = null!;
    private TabPage _gcodeTab = null!;
    private TabPage _statisticsTab = null!;
    private DataGridView _statisticsGrid = null!;
    private Label _statisticsHint = null!;
    private Button _btnExportStatistics = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) _toleranceCancellation?.Cancel();
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _options = new GroupBox();
        _lblScaleFactor = new Label();
        groupBox1 = new GroupBox();
        _nudSpacing = new NumericUpDown();
        _lblLayers = new Label();
        _nudLayers = new NumericUpDown();
        _lblPixelSize = new Label();
        _lblLayerHeight = new Label();
        _lblSpacing = new Label();
        _nudLayerHeight = new NumericUpDown();
        _nudPixelSize = new NumericUpDown();
        _lblSimplify = new Label();
        _nudSimplify = new NumericUpDown();
        _chkOptimize = new CheckBox();
        _chkGenerateGcode = new CheckBox();
        _chkPlot = new CheckBox();
        _btnImport = new Button();
        _nudScaleFactor = new NumericUpDown();
        _lblImage = new Label();
        _lblSpiralType = new Label();
        _cmbSpiralType = new ComboBox();
        _lblBoundary = new Label();
        _nudBoundary = new NumericUpDown();
        _lblPointSpacing = new Label();
        _nudPointSpacing = new NumericUpDown();
        _btnGenerate = new Button();
        _btnSave = new Button();
        _lblStatus = new Label();
        _picture = new PictureBox();
        _canvas = new Panel();
        _txtGCode = new TextBox();
        _split = new SplitContainer();
        _toleranceOptions = new GroupBox();
        _nudToleranceStart = new NumericUpDown();
        _nudToleranceEnd = new NumericUpDown();
        _nudToleranceStep = new NumericUpDown();
        _lblToleranceStart = new Label();
        _lblToleranceEnd = new Label();
        _lblToleranceStep = new Label();
        _btnTest = new Button();
        _outputTabs = new TabControl();
        _gcodeTab = new TabPage("G-code");
        _statisticsTab = new TabPage("容差统计表");
        _statisticsGrid = new DataGridView();
        _statisticsHint = new Label();
        _btnExportStatistics = new Button();
        _options.SuspendLayout();
        groupBox1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_nudSpacing).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudLayers).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudLayerHeight).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudPixelSize).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudSimplify).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudScaleFactor).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudBoundary).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_nudPointSpacing).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_picture).BeginInit();
        _canvas.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_split).BeginInit();
        _split.Panel1.SuspendLayout();
        _split.Panel2.SuspendLayout();
        _split.SuspendLayout();
        SuspendLayout();
        // 
        // _options
        // 
        _options.Controls.Add(_lblScaleFactor);
        _options.Controls.Add(groupBox1);
        _options.Controls.Add(_btnImport);
        _options.Controls.Add(_nudScaleFactor);
        _options.Controls.Add(_lblImage);
        _options.Controls.Add(_lblSpiralType);
        _options.Controls.Add(_cmbSpiralType);
        _options.Controls.Add(_lblBoundary);
        _options.Controls.Add(_nudBoundary);
        _options.Controls.Add(_lblPointSpacing);
        _options.Controls.Add(_nudPointSpacing);
        _options.Controls.Add(_btnGenerate);
        _options.Controls.Add(_btnSave);
        _options.Controls.Add(_toleranceOptions);
        _options.Controls.Add(_btnTest);
        _options.Dock = DockStyle.Top;
        _options.Location = new Point(0, 0);
        _options.Name = "_options";
        _options.Size = new Size(1034, 353);
        _options.TabIndex = 19;
        _options.TabStop = false;
        _options.Text = "Fermat-Spirals 参数";
        //
        // 容差批量测试参数（mm）
        //
        _toleranceOptions.Text = "容差测试 (mm)";
        _toleranceOptions.Name = "_toleranceOptions";
        _toleranceOptions.Location = new Point(655, 94);
        _toleranceOptions.Size = new Size(300, 160);
        _toleranceOptions.TabIndex = 22;
        ConfigureLabel(_lblToleranceStart, "起始容差", 16, 34);
        ConfigureLabel(_lblToleranceEnd, "结束容差", 16, 74);
        ConfigureLabel(_lblToleranceStep, "步长", 16, 114);
        ConfigureNumeric(_nudToleranceStart, 0M, 0M, 100M, 3, 130, 30, 0);
        ConfigureNumeric(_nudToleranceEnd, .1M, 0M, 100M, 3, 130, 70, 1);
        ConfigureNumeric(_nudToleranceStep, .01M, .001M, 100M, 3, 130, 110, 2);
        _nudToleranceStart.Name = "_nudToleranceStart";
        _nudToleranceEnd.Name = "_nudToleranceEnd";
        _nudToleranceStep.Name = "_nudToleranceStep";
        _nudToleranceStart.Increment = .01M;
        _nudToleranceEnd.Increment = .01M;
        _nudToleranceStep.Increment = .001M;
        _toleranceOptions.Controls.AddRange(new Control[] {
            _lblToleranceStart, _nudToleranceStart, _lblToleranceEnd,
            _nudToleranceEnd, _lblToleranceStep, _nudToleranceStep });
        _btnTest.Name = "_btnTest";
        _btnTest.Text = "容差测试";
        _btnTest.Location = new Point(245, 306);
        _btnTest.Size = new Size(110, 30);
        _btnTest.TabIndex = 23;
        _btnTest.Click += BtnTest_Click;
        // 
        // _lblScaleFactor
        // 
        _lblScaleFactor.AutoSize = true;
        _lblScaleFactor.Location = new Point(463, 217);
        _lblScaleFactor.Name = "_lblScaleFactor";
        _lblScaleFactor.Size = new Size(56, 17);
        _lblScaleFactor.TabIndex = 7;
        _lblScaleFactor.Text = "缩放比例";
        // 
        // groupBox1
        // 
        groupBox1.Controls.Add(_nudSpacing);
        groupBox1.Controls.Add(_lblLayers);
        groupBox1.Controls.Add(_nudLayers);
        groupBox1.Controls.Add(_lblPixelSize);
        groupBox1.Controls.Add(_lblLayerHeight);
        groupBox1.Controls.Add(_lblSpacing);
        groupBox1.Controls.Add(_nudLayerHeight);
        groupBox1.Controls.Add(_nudPixelSize);
        groupBox1.Controls.Add(_lblSimplify);
        groupBox1.Controls.Add(_nudSimplify);
        groupBox1.Controls.Add(_chkOptimize);
        groupBox1.Controls.Add(_chkGenerateGcode);
        groupBox1.Controls.Add(_chkPlot);
        groupBox1.Location = new Point(6, 94);
        groupBox1.Name = "groupBox1";
        groupBox1.Size = new Size(426, 196);
        groupBox1.TabIndex = 21;
        groupBox1.TabStop = false;
        groupBox1.Text = "路径与输出参数";
        // 
        // _nudSpacing
        // 
        _nudSpacing.DecimalPlaces = 2;
        _nudSpacing.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        _nudSpacing.Location = new Point(132, 42);
        _nudSpacing.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
        _nudSpacing.Minimum = new decimal(new int[] { 1, 0, 0, 131072 });
        _nudSpacing.Name = "_nudSpacing";
        _nudSpacing.Size = new Size(78, 23);
        _nudSpacing.TabIndex = 6;
        _nudSpacing.Value = new decimal(new int[] { 5, 0, 0, 0 });
        // 
        // _lblLayers
        // 
        _lblLayers.AutoSize = true;
        _lblLayers.Location = new Point(61, 77);
        _lblLayers.Name = "_lblLayers";
        _lblLayers.Size = new Size(32, 17);
        _lblLayers.TabIndex = 11;
        _lblLayers.Text = "层数";
        // 
        // _nudLayers
        // 
        _nudLayers.Location = new Point(132, 71);
        _nudLayers.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
        _nudLayers.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _nudLayers.Name = "_nudLayers";
        _nudLayers.Size = new Size(78, 23);
        _nudLayers.TabIndex = 12;
        _nudLayers.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // _lblPixelSize
        // 
        _lblPixelSize.AutoSize = true;
        _lblPixelSize.Location = new Point(0, 19);
        _lblPixelSize.Name = "_lblPixelSize";
        _lblPixelSize.Size = new Size(117, 17);
        _lblPixelSize.TabIndex = 3;
        _lblPixelSize.Text = "单像素尺寸(mm/px)";
        // 
        // _lblLayerHeight
        // 
        _lblLayerHeight.AutoSize = true;
        _lblLayerHeight.Location = new Point(47, 109);
        _lblLayerHeight.Name = "_lblLayerHeight";
        _lblLayerHeight.Size = new Size(62, 17);
        _lblLayerHeight.TabIndex = 13;
        _lblLayerHeight.Text = "层高(mm)";
        // 
        // _lblSpacing
        // 
        _lblSpacing.AutoSize = true;
        _lblSpacing.Location = new Point(43, 48);
        _lblSpacing.Name = "_lblSpacing";
        _lblSpacing.Size = new Size(74, 17);
        _lblSpacing.TabIndex = 5;
        _lblSpacing.Text = "线间距(mm)";
        // 
        // _nudLayerHeight
        // 
        _nudLayerHeight.DecimalPlaces = 2;
        _nudLayerHeight.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        _nudLayerHeight.Location = new Point(132, 107);
        _nudLayerHeight.Minimum = new decimal(new int[] { 1, 0, 0, 131072 });
        _nudLayerHeight.Name = "_nudLayerHeight";
        _nudLayerHeight.Size = new Size(78, 23);
        _nudLayerHeight.TabIndex = 14;
        _nudLayerHeight.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // _nudPixelSize
        // 
        _nudPixelSize.DecimalPlaces = 4;
        _nudPixelSize.Enabled = false;
        _nudPixelSize.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        _nudPixelSize.Location = new Point(132, 13);
        _nudPixelSize.Minimum = new decimal(new int[] { 1, 0, 0, 262144 });
        _nudPixelSize.Name = "_nudPixelSize";
        _nudPixelSize.Size = new Size(78, 23);
        _nudPixelSize.TabIndex = 4;
        _nudPixelSize.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // _lblSimplify
        // 
        _lblSimplify.AutoSize = true;
        _lblSimplify.Location = new Point(256, 19);
        _lblSimplify.Name = "_lblSimplify";
        _lblSimplify.Size = new Size(86, 17);
        _lblSimplify.TabIndex = 17;
        _lblSimplify.Text = "简化容差(mm)";
        // 
        // _nudSimplify
        // 
        _nudSimplify.DecimalPlaces = 3;
        _nudSimplify.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        _nudSimplify.Location = new Point(345, 19);
        _nudSimplify.Name = "_nudSimplify";
        _nudSimplify.Size = new Size(78, 23);
        _nudSimplify.TabIndex = 18;
        _nudSimplify.Value = new decimal(new int[] { 1, 0, 0, 131072 });
        // 
        // _chkOptimize
        // 
        _chkOptimize.AutoSize = true;
        _chkOptimize.Location = new Point(307, 105);
        _chkOptimize.Name = "_chkOptimize";
        _chkOptimize.Size = new Size(116, 21);
        _chkOptimize.TabIndex = 12;
        _chkOptimize.Text = "论文曲线优化";
        _chkOptimize.UseVisualStyleBackColor = true;
        // 
        // _chkGenerateGcode
        // 
        _chkGenerateGcode.AutoSize = true;
        _chkGenerateGcode.Checked = true;
        _chkGenerateGcode.CheckState = CheckState.Checked;
        _chkGenerateGcode.Location = new Point(307, 159);
        _chkGenerateGcode.Name = "_chkGenerateGcode";
        _chkGenerateGcode.Size = new Size(98, 21);
        _chkGenerateGcode.TabIndex = 14;
        _chkGenerateGcode.Text = "生成 G-code";
        _chkGenerateGcode.UseVisualStyleBackColor = true;
        // 
        // _chkPlot
        // 
        _chkPlot.AutoSize = true;
        _chkPlot.Checked = true;
        _chkPlot.CheckState = CheckState.Checked;
        _chkPlot.Location = new Point(307, 132);
        _chkPlot.Name = "_chkPlot";
        _chkPlot.Size = new Size(87, 21);
        _chkPlot.TabIndex = 13;
        _chkPlot.Text = "显示路径图";
        _chkPlot.UseVisualStyleBackColor = true;
        // 
        // _btnImport
        // 
        _btnImport.Location = new Point(15, 25);
        _btnImport.Name = "_btnImport";
        _btnImport.Size = new Size(100, 30);
        _btnImport.TabIndex = 0;
        _btnImport.Text = "导入图片";
        _btnImport.UseVisualStyleBackColor = true;
        _btnImport.Click += BtnImport_Click;
        // 
        // _nudScaleFactor
        // 
        _nudScaleFactor.DecimalPlaces = 3;
        _nudScaleFactor.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        _nudScaleFactor.Location = new Point(540, 214);
        _nudScaleFactor.Minimum = new decimal(new int[] { 1, 0, 0, 196608 });
        _nudScaleFactor.Name = "_nudScaleFactor";
        _nudScaleFactor.Size = new Size(78, 23);
        _nudScaleFactor.TabIndex = 8;
        _nudScaleFactor.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // _lblImage
        // 
        _lblImage.AutoSize = true;
        _lblImage.Location = new Point(125, 32);
        _lblImage.Name = "_lblImage";
        _lblImage.Size = new Size(68, 17);
        _lblImage.TabIndex = 1;
        _lblImage.Text = "未选择图片";
        // 
        // _lblSpiralType
        // 
        _lblSpiralType.AutoSize = true;
        _lblSpiralType.Location = new Point(37, 74);
        _lblSpiralType.Name = "_lblSpiralType";
        _lblSpiralType.Size = new Size(56, 17);
        _lblSpiralType.TabIndex = 2;
        _lblSpiralType.Text = "螺旋类型";
        // 
        // _cmbSpiralType
        // 
        _cmbSpiralType.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbSpiralType.FormattingEnabled = true;
        _cmbSpiralType.Items.AddRange(new object[] { "普通螺旋 (spiral)", "费马螺旋 (fermat)", "连接费马 (connected_fermat)" });
        _cmbSpiralType.Location = new Point(110, 68);
        _cmbSpiralType.Name = "_cmbSpiralType";
        _cmbSpiralType.SelectedIndex = 2;
        _cmbSpiralType.Size = new Size(150, 25);
        _cmbSpiralType.TabIndex = 2;
        // 
        // _lblBoundary
        // 
        _lblBoundary.AutoSize = true;
        _lblBoundary.Enabled = false;
        _lblBoundary.Location = new Point(451, 128);
        _lblBoundary.Name = "_lblBoundary";
        _lblBoundary.Size = new Size(86, 17);
        _lblBoundary.TabIndex = 19;
        _lblBoundary.Text = "边界偏移(mm)";
        // 
        // _nudBoundary
        // 
        _nudBoundary.DecimalPlaces = 2;
        _nudBoundary.Enabled = false;
        _nudBoundary.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        _nudBoundary.Location = new Point(540, 126);
        _nudBoundary.Name = "_nudBoundary";
        _nudBoundary.Size = new Size(78, 23);
        _nudBoundary.TabIndex = 20;
        // 
        // _lblPointSpacing
        // 
        _lblPointSpacing.AutoSize = true;
        _lblPointSpacing.Enabled = false;
        _lblPointSpacing.Location = new Point(451, 171);
        _lblPointSpacing.Name = "_lblPointSpacing";
        _lblPointSpacing.Size = new Size(74, 17);
        _lblPointSpacing.TabIndex = 15;
        _lblPointSpacing.Text = "点间距(mm)";
        // 
        // _nudPointSpacing
        // 
        _nudPointSpacing.DecimalPlaces = 3;
        _nudPointSpacing.Enabled = false;
        _nudPointSpacing.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        _nudPointSpacing.Location = new Point(540, 165);
        _nudPointSpacing.Minimum = new decimal(new int[] { 1, 0, 0, 196608 });
        _nudPointSpacing.Name = "_nudPointSpacing";
        _nudPointSpacing.Size = new Size(78, 23);
        _nudPointSpacing.TabIndex = 16;
        _nudPointSpacing.Value = new decimal(new int[] { 125, 0, 0, 131072 });
        // 
        // _btnGenerate
        // 
        _btnGenerate.Location = new Point(6, 306);
        _btnGenerate.Name = "_btnGenerate";
        _btnGenerate.Size = new Size(105, 30);
        _btnGenerate.TabIndex = 15;
        _btnGenerate.Text = "生成路径";
        _btnGenerate.UseVisualStyleBackColor = true;
        _btnGenerate.Click += BtnGenerate_Click;
        // 
        // _btnSave
        // 
        _btnSave.Enabled = false;
        _btnSave.Location = new Point(121, 306);
        _btnSave.Name = "_btnSave";
        _btnSave.Size = new Size(110, 30);
        _btnSave.TabIndex = 16;
        _btnSave.Text = "导出 G-code";
        _btnSave.UseVisualStyleBackColor = true;
        _btnSave.Click += BtnSave_Click;
        // 
        // _lblStatus
        // 
        _lblStatus.Dock = DockStyle.Bottom;
        _lblStatus.Location = new Point(0, 825);
        _lblStatus.Name = "_lblStatus";
        _lblStatus.Size = new Size(1034, 25);
        _lblStatus.TabIndex = 18;
        _lblStatus.Text = "就绪";
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _picture
        // 
        _picture.Dock = DockStyle.Fill;
        _picture.Location = new Point(0, 0);
        _picture.Name = "_picture";
        _picture.Size = new Size(605, 472);
        _picture.SizeMode = PictureBoxSizeMode.Zoom;
        _picture.TabIndex = 0;
        _picture.TabStop = false;
        _picture.Visible = false;
        // 
        // _canvas
        // 
        _canvas.BackColor = Color.White;
        _canvas.Controls.Add(_picture);
        _canvas.Dock = DockStyle.Fill;
        _canvas.Location = new Point(0, 0);
        _canvas.Name = "_canvas";
        _canvas.Size = new Size(605, 472);
        _canvas.TabIndex = 0;
        _canvas.Paint += Canvas_Paint;
        // 
        // _txtGCode
        // 
        _txtGCode.Dock = DockStyle.Fill;
        _txtGCode.Font = new Font("Consolas", 9F);
        _txtGCode.Location = new Point(0, 0);
        _txtGCode.Multiline = true;
        _txtGCode.Name = "_txtGCode";
        _txtGCode.ScrollBars = ScrollBars.Both;
        _txtGCode.Size = new Size(425, 472);
        _txtGCode.TabIndex = 0;
        _txtGCode.WordWrap = false;
        // 
        // _split
        // 
        _split.Dock = DockStyle.Fill;
        _split.Location = new Point(0, 353);
        _split.Name = "_split";
        // 
        // _split.Panel1
        // 
        _split.Panel1.Controls.Add(_canvas);
        // 
        // _split.Panel2
        // 
        _split.Panel2.Controls.Add(_outputTabs);
        _outputTabs.Name = "_outputTabs";
        _outputTabs.Dock = DockStyle.Fill;
        _outputTabs.TabPages.AddRange(new[] { _gcodeTab, _statisticsTab });
        _gcodeTab.Controls.Add(_txtGCode);
        _statisticsGrid.Name = "_statisticsGrid";
        _statisticsGrid.Dock = DockStyle.Fill;
        _statisticsGrid.ReadOnly = true;
        _statisticsGrid.AllowUserToAddRows = false;
        _statisticsGrid.AllowUserToDeleteRows = false;
        _statisticsGrid.RowHeadersVisible = false;
        _statisticsGrid.AutoGenerateColumns = false;
        _statisticsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _statisticsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _statisticsGrid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
        _statisticsGrid.Columns.AddRange(new DataGridViewColumn[] {
            new DataGridViewTextBoxColumn { DataPropertyName = "ToleranceMm", HeaderText = "容差(mm)" },
            new DataGridViewTextBoxColumn { DataPropertyName = "PathCount", HeaderText = "路径条数" },
            new DataGridViewTextBoxColumn { DataPropertyName = "PointCount", HeaderText = "单层点数" },
            new DataGridViewTextBoxColumn { DataPropertyName = "BaselinePointCount", HeaderText = "零容差点数" },
            new DataGridViewTextBoxColumn { DataPropertyName = "Reduction", HeaderText = "减少点数" },
            new DataGridViewTextBoxColumn { DataPropertyName = "ReductionPercent", HeaderText = "减少比例(%)",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "F2" } },
            new DataGridViewTextBoxColumn { DataPropertyName = "ElapsedMs", HeaderText = "耗时(ms)" }
        });
        _statisticsGrid.DataSource = _toleranceRows;
        _statisticsHint.Dock = DockStyle.Top;
        _statisticsHint.Height = 90;
        _statisticsHint.Text = "设置起始容差、结束容差和步长，点击容差测试。\r\n统计单层最终路径点数（含补点），以零容差为基准。";
        _btnExportStatistics.Text = "导出统计 CSV";
        _btnExportStatistics.Name = "_btnExportStatistics";
        _btnExportStatistics.Dock = DockStyle.Bottom;
        _btnExportStatistics.Height = 32;
        _btnExportStatistics.Enabled = false;
        _btnExportStatistics.Click += BtnExportStatistics_Click;
        _statisticsTab.Controls.Add(_statisticsGrid);
        _statisticsTab.Controls.Add(_statisticsHint);
        _statisticsTab.Controls.Add(_btnExportStatistics);
        _split.Size = new Size(1034, 472);
        _split.SplitterDistance = 605;
        _split.TabIndex = 17;
        // 
        // CfsGenerateForm
        // 
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1034, 850);
        Controls.Add(_split);
        Controls.Add(_lblStatus);
        Controls.Add(_options);
        MinimumSize = new Size(1050, 650);
        Name = "CfsGenerateForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CFS 螺旋线生成";
        _options.ResumeLayout(false);
        _options.PerformLayout();
        groupBox1.ResumeLayout(false);
        groupBox1.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_nudSpacing).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudLayers).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudLayerHeight).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudPixelSize).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudSimplify).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudScaleFactor).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudBoundary).EndInit();
        ((System.ComponentModel.ISupportInitialize)_nudPointSpacing).EndInit();
        ((System.ComponentModel.ISupportInitialize)_picture).EndInit();
        _canvas.ResumeLayout(false);
        _split.Panel1.ResumeLayout(false);
        _split.Panel2.ResumeLayout(false);
        _split.Panel2.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_split).EndInit();
        _split.ResumeLayout(false);
        ResumeLayout(false);
    }

    private static void ConfigureLabel(Label label, string text, int x, int y)
    {
        label.AutoSize = true;
        label.Location = new Point(x, y);
        label.Text = text;
    }

    private static void ConfigureNumeric(NumericUpDown control, decimal value, decimal minimum,
        decimal maximum, int decimalPlaces, int x, int y, int tabIndex)
    {
        control.DecimalPlaces = decimalPlaces;
        control.Increment = decimalPlaces == 0 ? 1M : .1M;
        control.Location = new Point(x, y);
        control.Maximum = maximum;
        control.Minimum = minimum;
        control.Name = "numericUpDown" + tabIndex;
        control.Size = new Size(78, 23);
        control.TabIndex = tabIndex;
        control.Value = value;
    }

    private GroupBox groupBox1;
}
