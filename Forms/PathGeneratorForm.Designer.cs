using System.Drawing;
using System.Windows.Forms;
using GcodeViewer.Rendering;

namespace GcodeViewer.Forms;

/// <summary>
/// 路径生成窗体的设计器分部文件。
/// 严格遵循 WinForms 设计器可解析的标准模式：InitializeComponent 为扁平 new + 逐属性赋值序列，
/// 禁用 var / 工厂方法 / 复杂内联初始化器。所有控件、布局与事件均在此声明，
/// VS 设计器里可直接选中、拖动、改属性；按钮 Click 等事件在属性窗口事件面板可见可编辑。
/// </summary>
public sealed partial class PathGeneratorForm
{
    // ---- 数据导入组 ----
    private GroupBox _grpSource;
    private FlowLayoutPanel _flowSrc;
    private Button _btnLoadImage;
    private Label _lblImgW;
    private NumericUpDown _numImgWidth;
    private Label _lblImgH;
    private NumericUpDown _numImgHeight;
    private Panel _sp1;
    private Button _btnLoadVoxel;
    private Panel _spStl;
    private Button _btnLoadStl;
    private Panel _sp2;
    private Label _lblSrcStatus;

    // ---- 工艺参数组 ----
    private GroupBox _grpProcess;
    private FlowLayoutPanel _flowProcess;
    private Label _lblPrintVeloA;
    private NumericUpDown _numPrintVeloA;

    private Panel _sp3;
    private Label _lblEadvancedisA;
    private NumericUpDown _numEadvancedisA;

    private Panel _sp4;
    private Label _lblDisinternal;
    private NumericUpDown _numDisinternal;
    private Panel _sp5;
    private Label _lbldt;
    private NumericUpDown _numdt;
    private CheckBox _chkVelochange;

    private Label _lblPrintVeloB;
    private NumericUpDown _numPrintVeloB;
    private Label _lblEadvancedisB;
    private NumericUpDown _numEadvancedisB;

    // ---- 切换参数组 ----
    private GroupBox _grpSwitch;
    private FlowLayoutPanel _flowSwitch;
    private Label _lblAchangeV;
    private NumericUpDown _numAchangeV;
    private Label _lblAchangeDis;
    private NumericUpDown _numAchangeDis;
    private Label _lblAchangePress;
    private NumericUpDown _numAchangePress;
    private Panel _sp6;
    private Label _lblBchangeV;
    private NumericUpDown _numBchangeV;
    private Label _lblBchangeDis;
    private NumericUpDown _numBchangeDis;
    private Label _lblBchangePress;
    private NumericUpDown _numBchangePress;

    // ---- 操作栏 ----
    private FlowLayoutPanel _buttonBar;
    private Button _btnLoad;
    private Panel _sp7;
    private Label _lblTol;
    private NumericUpDown _numTol;
    private Panel _sp8;
    private Panel _sp9;
    private Button _btnExport;
    private Button _btnSendToMain;
    private Button _btnEvaluateAccuracy;

    // ---- 映射方法切换栏 ----
    private FlowLayoutPanel _mapBar;
    private Label _lblMapMethod;
    private ComboBox _cboMapMethod;
    private Panel _spMap;
    private CheckBox _chkShowStl;
    private CheckBox _chkShowVoxel;
    private Panel _spRot;
    private Button _btnRotXPlus;
    private Button _btnRotXMinus;

    // ---- 文件名显示 ----
    private Panel _fileBar;
    private TextBox _txtGcodeFileName;
    private Label _lblFile;

    // ---- 预览 / 状态 ----
    private Viewport3D _preview;
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _status;

    private System.ComponentModel.IContainer components = null;

    private void InitializeComponent()
    {
        _grpSource = new GroupBox();
        _flowSrc = new FlowLayoutPanel();
        _btnLoadImage = new Button();
        _lblImgW = new Label();
        _numImgWidth = new NumericUpDown();
        _lblImgH = new Label();
        _numImgHeight = new NumericUpDown();
        _sp1 = new Panel();
        _btnLoadVoxel = new Button();
        _spStl = new Panel();
        _btnLoadStl = new Button();
        _sp2 = new Panel();
        _lblSrcStatus = new Label();
        _grpProcess = new GroupBox();
        _flowProcess = new FlowLayoutPanel();
        _lblPrintVeloA = new Label();
        _numPrintVeloA = new NumericUpDown();
        _lblPrintVeloB = new Label();
        _numPrintVeloB = new NumericUpDown();
        _sp3 = new Panel();
        _lblEadvancedisA = new Label();
        _numEadvancedisA = new NumericUpDown();
        _sp4 = new Panel();
        _lblEadvancedisB = new Label();
        _numEadvancedisB = new NumericUpDown();
        _lblDisinternal = new Label();
        _numDisinternal = new NumericUpDown();
        _sp5 = new Panel();
        _lbldt = new Label();
        _numdt = new NumericUpDown();
        _chkVelochange = new CheckBox();
        _grpSwitch = new GroupBox();
        _flowSwitch = new FlowLayoutPanel();
        _lblAchangeV = new Label();
        _numAchangeV = new NumericUpDown();
        _lblAchangeDis = new Label();
        _numAchangeDis = new NumericUpDown();
        _lblAchangePress = new Label();
        _numAchangePress = new NumericUpDown();
        _sp6 = new Panel();
        _lblBchangeV = new Label();
        _numBchangeV = new NumericUpDown();
        _lblBchangeDis = new Label();
        _numBchangeDis = new NumericUpDown();
        _lblBchangePress = new Label();
        _numBchangePress = new NumericUpDown();
        _buttonBar = new FlowLayoutPanel();
        _btnLoad = new Button();
        _sp7 = new Panel();
        _lblTol = new Label();
        _numTol = new NumericUpDown();
        _sp8 = new Panel();
        _sp9 = new Panel();
        _btnExport = new Button();
        _btnSendToMain = new Button();
        _btnEvaluateAccuracy = new Button();
        _mapBar = new FlowLayoutPanel();
        _lblMapMethod = new Label();
        _cboMapMethod = new ComboBox();
        _spMap = new Panel();
        _chkShowStl = new CheckBox();
        _chkShowVoxel = new CheckBox();
        _spRot = new Panel();
        _btnRotXPlus = new Button();
        _btnRotXMinus = new Button();
        _fileBar = new Panel();
        _txtGcodeFileName = new TextBox();
        _lblFile = new Label();
        _preview = new Viewport3D();
        _statusStrip = new StatusStrip();
        _status = new ToolStripStatusLabel();
        _grpSource.SuspendLayout();
        _flowSrc.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numImgWidth).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numImgHeight).BeginInit();
        _grpProcess.SuspendLayout();
        _flowProcess.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numPrintVeloA).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numPrintVeloB).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numEadvancedisA).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numEadvancedisB).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numDisinternal).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numdt).BeginInit();
        _grpSwitch.SuspendLayout();
        _flowSwitch.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numAchangeV).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numAchangeDis).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numAchangePress).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numBchangeV).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numBchangeDis).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numBchangePress).BeginInit();
        _buttonBar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numTol).BeginInit();
        _mapBar.SuspendLayout();
        _fileBar.SuspendLayout();
        _statusStrip.SuspendLayout();
        SuspendLayout();
        // 
        // _grpSource
        // 
        _grpSource.Controls.Add(_flowSrc);
        _grpSource.Dock = DockStyle.Top;
        _grpSource.Location = new Point(0, 0);
        _grpSource.Name = "_grpSource";
        _grpSource.Size = new Size(1244, 76);
        _grpSource.TabIndex = 1;
        _grpSource.TabStop = false;
        _grpSource.Text = "数据导入（图片 / 体素 → 供生成与体素映射）";
        // 
        // _flowSrc
        // 
        _flowSrc.Controls.Add(_btnLoadImage);
        _flowSrc.Controls.Add(_lblImgW);
        _flowSrc.Controls.Add(_numImgWidth);
        _flowSrc.Controls.Add(_lblImgH);
        _flowSrc.Controls.Add(_numImgHeight);
        _flowSrc.Controls.Add(_sp1);
        _flowSrc.Controls.Add(_btnLoadVoxel);
        _flowSrc.Controls.Add(_spStl);
        _flowSrc.Controls.Add(_btnLoadStl);
        _flowSrc.Controls.Add(_sp2);
        _flowSrc.Controls.Add(_lblSrcStatus);
        _flowSrc.Dock = DockStyle.Fill;
        _flowSrc.Location = new Point(3, 19);
        _flowSrc.Name = "_flowSrc";
        _flowSrc.Padding = new Padding(8, 4, 8, 4);
        _flowSrc.Size = new Size(1238, 54);
        _flowSrc.TabIndex = 0;
        // 
        // _btnLoadImage
        // 
        _btnLoadImage.AutoSize = true;
        _btnLoadImage.FlatStyle = FlatStyle.System;
        _btnLoadImage.Location = new Point(8, 8);
        _btnLoadImage.Margin = new Padding(0, 4, 8, 0);
        _btnLoadImage.Name = "_btnLoadImage";
        _btnLoadImage.Size = new Size(127, 37);
        _btnLoadImage.TabIndex = 0;
        _btnLoadImage.Text = "导入图片…";
        _btnLoadImage.Click += OnLoadImage;
        // 
        // _lblImgW
        // 
        _lblImgW.AutoSize = true;
        _lblImgW.Location = new Point(149, 12);
        _lblImgW.Margin = new Padding(6, 8, 2, 0);
        _lblImgW.Name = "_lblImgW";
        _lblImgW.Size = new Size(47, 17);
        _lblImgW.TabIndex = 1;
        _lblImgW.Text = "图片宽:";
        // 
        // _numImgWidth
        // 
        _numImgWidth.Location = new Point(198, 8);
        _numImgWidth.Margin = new Padding(0, 4, 8, 0);
        _numImgWidth.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
        _numImgWidth.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _numImgWidth.Name = "_numImgWidth";
        _numImgWidth.Size = new Size(58, 23);
        _numImgWidth.TabIndex = 2;
        _numImgWidth.Value = new decimal(new int[] { 100, 0, 0, 0 });
        _numImgWidth.ValueChanged += OnImgWidthChanged;
        // 
        // _lblImgH
        // 
        _lblImgH.AutoSize = true;
        _lblImgH.Location = new Point(270, 12);
        _lblImgH.Margin = new Padding(6, 8, 2, 0);
        _lblImgH.Name = "_lblImgH";
        _lblImgH.Size = new Size(23, 17);
        _lblImgH.TabIndex = 3;
        _lblImgH.Text = "高:";
        // 
        // _numImgHeight
        // 
        _numImgHeight.Location = new Point(295, 8);
        _numImgHeight.Margin = new Padding(0, 4, 8, 0);
        _numImgHeight.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
        _numImgHeight.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _numImgHeight.Name = "_numImgHeight";
        _numImgHeight.Size = new Size(58, 23);
        _numImgHeight.TabIndex = 4;
        _numImgHeight.Value = new decimal(new int[] { 100, 0, 0, 0 });
        _numImgHeight.ValueChanged += OnImgHeightChanged;
        // 
        // _sp1
        // 
        _sp1.Location = new Point(361, 4);
        _sp1.Margin = new Padding(0);
        _sp1.Name = "_sp1";
        _sp1.Size = new Size(10, 1);
        _sp1.TabIndex = 5;
        // 
        // _btnLoadVoxel
        // 
        _btnLoadVoxel.AutoSize = true;
        _btnLoadVoxel.FlatStyle = FlatStyle.System;
        _btnLoadVoxel.Location = new Point(371, 8);
        _btnLoadVoxel.Margin = new Padding(0, 4, 8, 0);
        _btnLoadVoxel.Name = "_btnLoadVoxel";
        _btnLoadVoxel.Size = new Size(173, 37);
        _btnLoadVoxel.TabIndex = 6;
        _btnLoadVoxel.Text = "导入体素 CSV…";
        _btnLoadVoxel.Click += OnLoadVoxel;
        // 
        // _spStl
        // 
        _spStl.Location = new Point(552, 4);
        _spStl.Margin = new Padding(0);
        _spStl.Name = "_spStl";
        _spStl.Size = new Size(10, 1);
        _spStl.TabIndex = 7;
        // 
        // _btnLoadStl
        // 
        _btnLoadStl.AutoSize = true;
        _btnLoadStl.FlatStyle = FlatStyle.System;
        _btnLoadStl.Location = new Point(562, 8);
        _btnLoadStl.Margin = new Padding(0, 4, 8, 0);
        _btnLoadStl.Name = "_btnLoadStl";
        _btnLoadStl.Size = new Size(126, 37);
        _btnLoadStl.TabIndex = 8;
        _btnLoadStl.Text = "导入 STL…";
        _btnLoadStl.Click += OnLoadStl;
        // 
        // _sp2
        // 
        _sp2.Location = new Point(696, 4);
        _sp2.Margin = new Padding(0);
        _sp2.Name = "_sp2";
        _sp2.Size = new Size(10, 1);
        _sp2.TabIndex = 7;
        // 
        // _lblSrcStatus
        // 
        _lblSrcStatus.AutoSize = true;
        _lblSrcStatus.ForeColor = Color.DarkSlateGray;
        _lblSrcStatus.Location = new Point(712, 12);
        _lblSrcStatus.Margin = new Padding(6, 8, 0, 0);
        _lblSrcStatus.Name = "_lblSrcStatus";
        _lblSrcStatus.Size = new Size(0, 17);
        _lblSrcStatus.TabIndex = 8;
        // 
        // _grpProcess
        // 
        _grpProcess.Controls.Add(_flowProcess);
        _grpProcess.Dock = DockStyle.Top;
        _grpProcess.Location = new Point(0, 76);
        _grpProcess.Name = "_grpProcess";
        _grpProcess.Size = new Size(1244, 76);
        _grpProcess.TabIndex = 2;
        _grpProcess.TabStop = false;
        _grpProcess.Text = "工艺参数";
        // 
        // _flowProcess
        // 
        _flowProcess.Controls.Add(_lblPrintVeloA);
        _flowProcess.Controls.Add(_numPrintVeloA);
        _flowProcess.Controls.Add(_lblPrintVeloB);
        _flowProcess.Controls.Add(_numPrintVeloB);
        _flowProcess.Controls.Add(_sp3);
        _flowProcess.Controls.Add(_lblEadvancedisA);
        _flowProcess.Controls.Add(_numEadvancedisA);
        _flowProcess.Controls.Add(_sp4);
        _flowProcess.Controls.Add(_lblEadvancedisB);
        _flowProcess.Controls.Add(_numEadvancedisB);
        _flowProcess.Controls.Add(_lblDisinternal);
        _flowProcess.Controls.Add(_numDisinternal);
        _flowProcess.Controls.Add(_sp5);
        _flowProcess.Dock = DockStyle.Fill;
        _flowProcess.Location = new Point(3, 19);
        _flowProcess.Name = "_flowProcess";
        _flowProcess.Padding = new Padding(8, 4, 8, 4);
        _flowProcess.Size = new Size(1238, 54);
        _flowProcess.TabIndex = 0;
        // 
        // _lblPrintVeloA
        // 
        _lblPrintVeloA.AutoSize = true;
        _lblPrintVeloA.Location = new Point(14, 12);
        _lblPrintVeloA.Margin = new Padding(6, 8, 2, 0);
        _lblPrintVeloA.Name = "_lblPrintVeloA";
        _lblPrintVeloA.Size = new Size(67, 17);
        _lblPrintVeloA.TabIndex = 0;
        _lblPrintVeloA.Text = "打印速度A:";
        // 
        // _numPrintVeloA
        // 
        _numPrintVeloA.DecimalPlaces = 1;
        _numPrintVeloA.Location = new Point(83, 8);
        _numPrintVeloA.Margin = new Padding(0, 4, 8, 0);
        _numPrintVeloA.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
        _numPrintVeloA.Name = "_numPrintVeloA";
        _numPrintVeloA.Size = new Size(58, 23);
        _numPrintVeloA.TabIndex = 1;
        _numPrintVeloA.Value = new decimal(new int[] { 5, 0, 0, 0 });
        // 
        // _lblPrintVeloB
        // 
        _lblPrintVeloB.AutoSize = true;
        _lblPrintVeloB.Location = new Point(155, 12);
        _lblPrintVeloB.Margin = new Padding(6, 8, 2, 0);
        _lblPrintVeloB.Name = "_lblPrintVeloB";
        _lblPrintVeloB.Size = new Size(67, 17);
        _lblPrintVeloB.TabIndex = 10;
        _lblPrintVeloB.Text = "打印速度B:";
        // 
        // _numPrintVeloB
        // 
        _numPrintVeloB.DecimalPlaces = 1;
        _numPrintVeloB.Location = new Point(224, 8);
        _numPrintVeloB.Margin = new Padding(0, 4, 8, 0);
        _numPrintVeloB.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
        _numPrintVeloB.Name = "_numPrintVeloB";
        _numPrintVeloB.Size = new Size(58, 23);
        _numPrintVeloB.TabIndex = 9;
        _numPrintVeloB.Value = new decimal(new int[] { 5, 0, 0, 0 });
        // 
        // _sp3
        // 
        _sp3.Location = new Point(290, 4);
        _sp3.Margin = new Padding(0);
        _sp3.Name = "_sp3";
        _sp3.Size = new Size(10, 1);
        _sp3.TabIndex = 2;
        // 
        // _lblEadvancedisA
        // 
        _lblEadvancedisA.AutoSize = true;
        _lblEadvancedisA.Location = new Point(306, 12);
        _lblEadvancedisA.Margin = new Padding(6, 8, 2, 0);
        _lblEadvancedisA.Name = "_lblEadvancedisA";
        _lblEadvancedisA.Size = new Size(97, 17);
        _lblEadvancedisA.TabIndex = 3;
        _lblEadvancedisA.Text = "提前出丝A(mm):";
        // 
        // _numEadvancedisA
        // 
        _numEadvancedisA.DecimalPlaces = 2;
        _numEadvancedisA.Location = new Point(405, 8);
        _numEadvancedisA.Margin = new Padding(0, 4, 8, 0);
        _numEadvancedisA.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
        _numEadvancedisA.Name = "_numEadvancedisA";
        _numEadvancedisA.Size = new Size(58, 23);
        _numEadvancedisA.TabIndex = 4;
        _numEadvancedisA.Value = new decimal(new int[] { 2, 0, 0, 0 });
        // 
        // _sp4
        // 
        _sp4.Location = new Point(471, 4);
        _sp4.Margin = new Padding(0);
        _sp4.Name = "_sp4";
        _sp4.Size = new Size(10, 1);
        _sp4.TabIndex = 5;
        // 
        // _lblEadvancedisB
        // 
        _lblEadvancedisB.AutoSize = true;
        _lblEadvancedisB.Location = new Point(487, 12);
        _lblEadvancedisB.Margin = new Padding(6, 8, 2, 0);
        _lblEadvancedisB.Name = "_lblEadvancedisB";
        _lblEadvancedisB.Size = new Size(97, 17);
        _lblEadvancedisB.TabIndex = 11;
        _lblEadvancedisB.Text = "提前出丝B(mm):";
        // 
        // _numEadvancedisB
        // 
        _numEadvancedisB.DecimalPlaces = 2;
        _numEadvancedisB.Location = new Point(586, 8);
        _numEadvancedisB.Margin = new Padding(0, 4, 8, 0);
        _numEadvancedisB.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
        _numEadvancedisB.Name = "_numEadvancedisB";
        _numEadvancedisB.Size = new Size(58, 23);
        _numEadvancedisB.TabIndex = 12;
        _numEadvancedisB.Value = new decimal(new int[] { 2, 0, 0, 0 });
        // 
        // _lblDisinternal
        // 
        _lblDisinternal.AutoSize = true;
        _lblDisinternal.Location = new Point(658, 12);
        _lblDisinternal.Margin = new Padding(6, 8, 2, 0);
        _lblDisinternal.Name = "_lblDisinternal";
        _lblDisinternal.Size = new Size(125, 17);
        _lblDisinternal.TabIndex = 6;
        _lblDisinternal.Text = "每像素距离间距(mm):";
        // 
        // _numDisinternal
        // 
        _numDisinternal.DecimalPlaces = 2;
        _numDisinternal.Location = new Point(785, 8);
        _numDisinternal.Margin = new Padding(0, 4, 8, 0);
        _numDisinternal.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
        _numDisinternal.Name = "_numDisinternal";
        _numDisinternal.Size = new Size(58, 23);
        _numDisinternal.TabIndex = 7;
        _numDisinternal.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // _sp5
        // 
        _sp5.Location = new Point(851, 4);
        _sp5.Margin = new Padding(0);
        _sp5.Name = "_sp5";
        _sp5.Size = new Size(10, 1);
        _sp5.TabIndex = 8;
        // 
        // _lbldt
        // 
        _lbldt.AutoSize = true;
        _lbldt.Location = new Point(386, 14);
        _lbldt.Margin = new Padding(6, 8, 2, 0);
        _lbldt.Name = "_lbldt";
        _lbldt.Size = new Size(96, 17);
        _lbldt.TabIndex = 13;
        _lbldt.Text = "采样间隔dt(ms):";
        // 
        // _numdt
        // 
        _numdt.Location = new Point(484, 10);
        _numdt.Margin = new Padding(0, 4, 8, 0);
        _numdt.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
        _numdt.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _numdt.Name = "_numdt";
        _numdt.Size = new Size(58, 23);
        _numdt.TabIndex = 14;
        _numdt.Value = new decimal(new int[] { 20, 0, 0, 0 });
        // 
        // _chkVelochange
        // 
        _chkVelochange.AutoSize = true;
        _chkVelochange.Checked = true;
        _chkVelochange.CheckState = CheckState.Checked;
        _chkVelochange.Location = new Point(804, 10);
        _chkVelochange.Margin = new Padding(8, 6, 0, 0);
        _chkVelochange.Name = "_chkVelochange";
        _chkVelochange.Size = new Size(75, 21);
        _chkVelochange.TabIndex = 9;
        _chkVelochange.Text = "切换变速";
        _chkVelochange.CheckedChanged += OnVelochangeChanged;
        // 
        // _grpSwitch
        // 
        _grpSwitch.Controls.Add(_flowSwitch);
        _grpSwitch.Dock = DockStyle.Top;
        _grpSwitch.Location = new Point(0, 152);
        _grpSwitch.Name = "_grpSwitch";
        _grpSwitch.Size = new Size(1244, 76);
        _grpSwitch.TabIndex = 3;
        _grpSwitch.TabStop = false;
        _grpSwitch.Text = "切换参数（变速/变压，勾选“切换变速”时启用）";
        // 
        // _flowSwitch
        // 
        _flowSwitch.Controls.Add(_lblAchangeV);
        _flowSwitch.Controls.Add(_numAchangeV);
        _flowSwitch.Controls.Add(_lblAchangeDis);
        _flowSwitch.Controls.Add(_numAchangeDis);
        _flowSwitch.Controls.Add(_lblAchangePress);
        _flowSwitch.Controls.Add(_numAchangePress);
        _flowSwitch.Controls.Add(_sp6);
        _flowSwitch.Controls.Add(_lblBchangeV);
        _flowSwitch.Controls.Add(_numBchangeV);
        _flowSwitch.Controls.Add(_lblBchangeDis);
        _flowSwitch.Controls.Add(_numBchangeDis);
        _flowSwitch.Controls.Add(_lblBchangePress);
        _flowSwitch.Controls.Add(_numBchangePress);
        _flowSwitch.Controls.Add(_chkVelochange);
        _flowSwitch.Dock = DockStyle.Fill;
        _flowSwitch.Location = new Point(3, 19);
        _flowSwitch.Name = "_flowSwitch";
        _flowSwitch.Padding = new Padding(8, 4, 8, 4);
        _flowSwitch.Size = new Size(1238, 54);
        _flowSwitch.TabIndex = 0;
        // 
        // _lblAchangeV
        // 
        _lblAchangeV.AutoSize = true;
        _lblAchangeV.Location = new Point(14, 12);
        _lblAchangeV.Margin = new Padding(6, 8, 2, 0);
        _lblAchangeV.Name = "_lblAchangeV";
        _lblAchangeV.Size = new Size(73, 17);
        _lblAchangeV.TabIndex = 0;
        _lblAchangeV.Text = "T0 段  速度:";
        // 
        // _numAchangeV
        // 
        _numAchangeV.DecimalPlaces = 1;
        _numAchangeV.Location = new Point(89, 8);
        _numAchangeV.Margin = new Padding(0, 4, 8, 0);
        _numAchangeV.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
        _numAchangeV.Name = "_numAchangeV";
        _numAchangeV.Size = new Size(58, 23);
        _numAchangeV.TabIndex = 1;
        _numAchangeV.Value = new decimal(new int[] { 5, 0, 0, 0 });
        // 
        // _lblAchangeDis
        // 
        _lblAchangeDis.AutoSize = true;
        _lblAchangeDis.Location = new Point(161, 12);
        _lblAchangeDis.Margin = new Padding(6, 8, 2, 0);
        _lblAchangeDis.Name = "_lblAchangeDis";
        _lblAchangeDis.Size = new Size(59, 17);
        _lblAchangeDis.TabIndex = 2;
        _lblAchangeDis.Text = "切换距离:";
        // 
        // _numAchangeDis
        // 
        _numAchangeDis.DecimalPlaces = 2;
        _numAchangeDis.Location = new Point(222, 8);
        _numAchangeDis.Margin = new Padding(0, 4, 8, 0);
        _numAchangeDis.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
        _numAchangeDis.Name = "_numAchangeDis";
        _numAchangeDis.Size = new Size(58, 23);
        _numAchangeDis.TabIndex = 3;
        _numAchangeDis.Value = new decimal(new int[] { 2, 0, 0, 0 });
        // 
        // _lblAchangePress
        // 
        _lblAchangePress.AutoSize = true;
        _lblAchangePress.Location = new Point(294, 12);
        _lblAchangePress.Margin = new Padding(6, 8, 2, 0);
        _lblAchangePress.Name = "_lblAchangePress";
        _lblAchangePress.Size = new Size(35, 17);
        _lblAchangePress.TabIndex = 4;
        _lblAchangePress.Text = "气压:";
        // 
        // _numAchangePress
        // 
        _numAchangePress.DecimalPlaces = 1;
        _numAchangePress.Location = new Point(331, 8);
        _numAchangePress.Margin = new Padding(0, 4, 8, 0);
        _numAchangePress.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
        _numAchangePress.Name = "_numAchangePress";
        _numAchangePress.Size = new Size(58, 23);
        _numAchangePress.TabIndex = 5;
        _numAchangePress.Value = new decimal(new int[] { 100, 0, 0, 0 });
        // 
        // _sp6
        // 
        _sp6.Location = new Point(397, 4);
        _sp6.Margin = new Padding(0);
        _sp6.Name = "_sp6";
        _sp6.Size = new Size(10, 1);
        _sp6.TabIndex = 6;
        // 
        // _lblBchangeV
        // 
        _lblBchangeV.AutoSize = true;
        _lblBchangeV.Location = new Point(413, 12);
        _lblBchangeV.Margin = new Padding(6, 8, 2, 0);
        _lblBchangeV.Name = "_lblBchangeV";
        _lblBchangeV.Size = new Size(73, 17);
        _lblBchangeV.TabIndex = 7;
        _lblBchangeV.Text = "T1 段  速度:";
        // 
        // _numBchangeV
        // 
        _numBchangeV.DecimalPlaces = 1;
        _numBchangeV.Location = new Point(488, 8);
        _numBchangeV.Margin = new Padding(0, 4, 8, 0);
        _numBchangeV.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
        _numBchangeV.Name = "_numBchangeV";
        _numBchangeV.Size = new Size(58, 23);
        _numBchangeV.TabIndex = 8;
        _numBchangeV.Value = new decimal(new int[] { 5, 0, 0, 0 });
        // 
        // _lblBchangeDis
        // 
        _lblBchangeDis.AutoSize = true;
        _lblBchangeDis.Location = new Point(560, 12);
        _lblBchangeDis.Margin = new Padding(6, 8, 2, 0);
        _lblBchangeDis.Name = "_lblBchangeDis";
        _lblBchangeDis.Size = new Size(59, 17);
        _lblBchangeDis.TabIndex = 9;
        _lblBchangeDis.Text = "切换距离:";
        // 
        // _numBchangeDis
        // 
        _numBchangeDis.DecimalPlaces = 2;
        _numBchangeDis.Location = new Point(621, 8);
        _numBchangeDis.Margin = new Padding(0, 4, 8, 0);
        _numBchangeDis.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
        _numBchangeDis.Name = "_numBchangeDis";
        _numBchangeDis.Size = new Size(58, 23);
        _numBchangeDis.TabIndex = 10;
        _numBchangeDis.Value = new decimal(new int[] { 2, 0, 0, 0 });
        // 
        // _lblBchangePress
        // 
        _lblBchangePress.AutoSize = true;
        _lblBchangePress.Location = new Point(693, 12);
        _lblBchangePress.Margin = new Padding(6, 8, 2, 0);
        _lblBchangePress.Name = "_lblBchangePress";
        _lblBchangePress.Size = new Size(35, 17);
        _lblBchangePress.TabIndex = 11;
        _lblBchangePress.Text = "气压:";
        // 
        // _numBchangePress
        // 
        _numBchangePress.DecimalPlaces = 1;
        _numBchangePress.Location = new Point(730, 8);
        _numBchangePress.Margin = new Padding(0, 4, 8, 0);
        _numBchangePress.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
        _numBchangePress.Name = "_numBchangePress";
        _numBchangePress.Size = new Size(58, 23);
        _numBchangePress.TabIndex = 12;
        _numBchangePress.Value = new decimal(new int[] { 100, 0, 0, 0 });
        // 
        // _buttonBar
        // 
        _buttonBar.Controls.Add(_btnLoad);
        _buttonBar.Controls.Add(_sp7);
        _buttonBar.Controls.Add(_lblTol);
        _buttonBar.Controls.Add(_numTol);
        _buttonBar.Controls.Add(_sp8);
        _buttonBar.Controls.Add(_sp9);
        _buttonBar.Controls.Add(_lbldt);
        _buttonBar.Controls.Add(_numdt);
        _buttonBar.Controls.Add(_btnExport);
        _buttonBar.Controls.Add(_btnSendToMain);
        _buttonBar.Controls.Add(_btnEvaluateAccuracy);
        _buttonBar.Dock = DockStyle.Top;
        _buttonBar.Location = new Point(0, 228);
        _buttonBar.Name = "_buttonBar";
        _buttonBar.Padding = new Padding(8, 6, 8, 0);
        _buttonBar.Size = new Size(1244, 44);
        _buttonBar.TabIndex = 4;
        _buttonBar.WrapContents = false;
        // 
        // _btnLoad
        // 
        _btnLoad.AutoSize = true;
        _btnLoad.FlatStyle = FlatStyle.System;
        _btnLoad.Location = new Point(8, 10);
        _btnLoad.Margin = new Padding(0, 4, 8, 0);
        _btnLoad.Name = "_btnLoad";
        _btnLoad.Size = new Size(165, 37);
        _btnLoad.TabIndex = 0;
        _btnLoad.Text = "加载 G-code…";
        _btnLoad.Click += OnLoad;
        // 
        // _sp7
        // 
        _sp7.Location = new Point(181, 6);
        _sp7.Margin = new Padding(0);
        _sp7.Name = "_sp7";
        _sp7.Size = new Size(10, 1);
        _sp7.TabIndex = 1;
        // 
        // _lblTol
        // 
        _lblTol.AutoSize = true;
        _lblTol.Location = new Point(197, 14);
        _lblTol.Margin = new Padding(6, 8, 2, 0);
        _lblTol.Name = "_lblTol";
        _lblTol.Size = new Size(89, 17);
        _lblTol.TabIndex = 2;
        _lblTol.Text = "简化阈值(mm):";
        // 
        // _numTol
        // 
        _numTol.DecimalPlaces = 3;
        _numTol.Location = new Point(288, 10);
        _numTol.Margin = new Padding(0, 4, 8, 0);
        _numTol.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
        _numTol.Name = "_numTol";
        _numTol.Size = new Size(64, 23);
        _numTol.TabIndex = 3;
        _numTol.Value = new decimal(new int[] { 5, 0, 0, 131072 });
        _numTol.ValueChanged += OnTolChanged;
        // 
        // _sp8
        // 
        _sp8.Location = new Point(360, 6);
        _sp8.Margin = new Padding(0);
        _sp8.Name = "_sp8";
        _sp8.Size = new Size(10, 1);
        _sp8.TabIndex = 4;
        // 
        // _sp9
        // 
        _sp9.Location = new Point(370, 6);
        _sp9.Margin = new Padding(0);
        _sp9.Name = "_sp9";
        _sp9.Size = new Size(10, 1);
        _sp9.TabIndex = 6;
        // 
        // _btnExport
        // 
        _btnExport.AutoSize = true;
        _btnExport.FlatStyle = FlatStyle.System;
        _btnExport.Location = new Point(550, 10);
        _btnExport.Margin = new Padding(0, 4, 8, 0);
        _btnExport.Name = "_btnExport";
        _btnExport.Size = new Size(131, 37);
        _btnExport.TabIndex = 7;
        _btnExport.Text = "导出 CSV…";
        _btnExport.Click += OnExport;
        // 
        // _btnSendToMain
        // 
        _btnSendToMain.AutoSize = true;
        _btnSendToMain.FlatStyle = FlatStyle.System;
        _btnSendToMain.Location = new Point(689, 10);
        _btnSendToMain.Margin = new Padding(0, 4, 8, 0);
        _btnSendToMain.Name = "_btnSendToMain";
        _btnSendToMain.Size = new Size(152, 37);
        _btnSendToMain.TabIndex = 11;
        _btnSendToMain.Text = "在主窗口查看";
        _btnSendToMain.Click += OnSendToMain;
        // 
        // _btnEvaluateAccuracy
        // 
        _btnEvaluateAccuracy.AutoSize = true;
        _btnEvaluateAccuracy.FlatStyle = FlatStyle.System;
        _btnEvaluateAccuracy.Location = new Point(849, 10);
        _btnEvaluateAccuracy.Margin = new Padding(0, 4, 8, 0);
        _btnEvaluateAccuracy.Name = "_btnEvaluateAccuracy";
        _btnEvaluateAccuracy.Size = new Size(152, 37);
        _btnEvaluateAccuracy.TabIndex = 12;
        _btnEvaluateAccuracy.Text = "评估映射精度";
        _btnEvaluateAccuracy.Click += OnCompareMappingMethods;
        // 
        // _mapBar
        // 
        _mapBar.Controls.Add(_lblMapMethod);
        _mapBar.Controls.Add(_cboMapMethod);
        _mapBar.Controls.Add(_spMap);
        _mapBar.Controls.Add(_chkShowStl);
        _mapBar.Controls.Add(_chkShowVoxel);
        _mapBar.Controls.Add(_spRot);
        _mapBar.Controls.Add(_btnRotXPlus);
        _mapBar.Controls.Add(_btnRotXMinus);
        _mapBar.Dock = DockStyle.Top;
        _mapBar.Location = new Point(0, 272);
        _mapBar.Name = "_mapBar";
        _mapBar.Padding = new Padding(8, 6, 8, 0);
        _mapBar.Size = new Size(1244, 44);
        _mapBar.TabIndex = 13;
        _mapBar.WrapContents = false;
        // 
        // _lblMapMethod
        // 
        _lblMapMethod.AutoSize = true;
        _lblMapMethod.Location = new Point(14, 14);
        _lblMapMethod.Margin = new Padding(6, 8, 2, 0);
        _lblMapMethod.Name = "_lblMapMethod";
        _lblMapMethod.Size = new Size(83, 17);
        _lblMapMethod.TabIndex = 0;
        _lblMapMethod.Text = "体素映射方法:";
        // 
        // _cboMapMethod
        // 
        _cboMapMethod.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboMapMethod.Items.AddRange(new object[] { "DDA 光线投射(默认)", "最近邻", "双线性插值", "SDF(STL 体内/体外)" });
        _cboMapMethod.Location = new Point(99, 10);
        _cboMapMethod.Margin = new Padding(0, 4, 8, 0);
        _cboMapMethod.Name = "_cboMapMethod";
        _cboMapMethod.Size = new Size(160, 25);
        _cboMapMethod.TabIndex = 1;
        _cboMapMethod.SelectedIndexChanged += OnMapMethodChanged;
        // 
        // _spMap
        // 
        _spMap.Location = new Point(267, 6);
        _spMap.Margin = new Padding(0);
        _spMap.Name = "_spMap";
        _spMap.Size = new Size(10, 1);
        _spMap.TabIndex = 2;
        // 
        // _chkShowStl
        // 
        _chkShowStl.AutoSize = true;
        _chkShowStl.Checked = true;
        _chkShowStl.CheckState = CheckState.Checked;
        _chkShowStl.Location = new Point(285, 12);
        _chkShowStl.Margin = new Padding(8, 6, 0, 0);
        _chkShowStl.Name = "_chkShowStl";
        _chkShowStl.Size = new Size(103, 21);
        _chkShowStl.TabIndex = 3;
        _chkShowStl.Text = "显示 STL 线框";
        _chkShowStl.CheckedChanged += OnShowStlChanged;
        // 
        // _chkShowVoxel
        // 
        _chkShowVoxel.AutoSize = true;
        _chkShowVoxel.Checked = true;
        _chkShowVoxel.CheckState = CheckState.Checked;
        _chkShowVoxel.Location = new Point(396, 12);
        _chkShowVoxel.Margin = new Padding(8, 6, 0, 0);
        _chkShowVoxel.Name = "_chkShowVoxel";
        _chkShowVoxel.Size = new Size(75, 21);
        _chkShowVoxel.TabIndex = 4;
        _chkShowVoxel.Text = "显示体素";
        _chkShowVoxel.CheckedChanged += OnShowVoxelChanged;
        // 
        // _spRot
        // 
        _spRot.Location = new Point(471, 6);
        _spRot.Margin = new Padding(0);
        _spRot.Name = "_spRot";
        _spRot.Size = new Size(10, 1);
        _spRot.TabIndex = 5;
        // 
        // _btnRotXPlus
        // 
        _btnRotXPlus.AutoSize = true;
        _btnRotXPlus.FlatStyle = FlatStyle.System;
        _btnRotXPlus.Location = new Point(481, 10);
        _btnRotXPlus.Margin = new Padding(0, 4, 8, 0);
        _btnRotXPlus.Name = "_btnRotXPlus";
        _btnRotXPlus.Size = new Size(116, 37);
        _btnRotXPlus.TabIndex = 6;
        _btnRotXPlus.Text = "绕X +90°";
        _btnRotXPlus.Click += OnRotXPlus;
        // 
        // _btnRotXMinus
        // 
        _btnRotXMinus.AutoSize = true;
        _btnRotXMinus.FlatStyle = FlatStyle.System;
        _btnRotXMinus.Location = new Point(605, 10);
        _btnRotXMinus.Margin = new Padding(0, 4, 8, 0);
        _btnRotXMinus.Name = "_btnRotXMinus";
        _btnRotXMinus.Size = new Size(116, 37);
        _btnRotXMinus.TabIndex = 7;
        _btnRotXMinus.Text = "绕X −90°";
        _btnRotXMinus.Click += OnRotXMinus;
        // 
        // _fileBar
        // 
        _fileBar.Controls.Add(_txtGcodeFileName);
        _fileBar.Controls.Add(_lblFile);
        _fileBar.Dock = DockStyle.Top;
        _fileBar.Location = new Point(0, 316);
        _fileBar.Name = "_fileBar";
        _fileBar.Size = new Size(1244, 30);
        _fileBar.TabIndex = 5;
        // 
        // _txtGcodeFileName
        // 
        _txtGcodeFileName.BackColor = SystemColors.Control;
        _txtGcodeFileName.Dock = DockStyle.Fill;
        _txtGcodeFileName.Location = new Point(0, 0);
        _txtGcodeFileName.Name = "_txtGcodeFileName";
        _txtGcodeFileName.ReadOnly = true;
        _txtGcodeFileName.Size = new Size(1244, 23);
        _txtGcodeFileName.TabIndex = 0;
        // 
        // _lblFile
        // 
        _lblFile.AutoSize = true;
        _lblFile.Location = new Point(0, 0);
        _lblFile.Margin = new Padding(6, 6, 4, 0);
        _lblFile.Name = "_lblFile";
        _lblFile.Size = new Size(59, 17);
        _lblFile.TabIndex = 1;
        _lblFile.Text = "生成文件:";
        // 
        // _preview
        // 
        _preview.BackColor = Color.White;
        _preview.ColorByLayer = false;
        _preview.ColorBySpeed = false;
        _preview.ColorByTool = false;
        _preview.Dock = DockStyle.Fill;
        _preview.FilterLayer = -1;
        _preview.G0LineWidth = 2F;
        _preview.G1LineWidth = 3.5F;
        _preview.Location = new Point(0, 346);
        _preview.Name = "_preview";
        _preview.ShowModified = true;
        _preview.ShowStl = true;
        _preview.ShowToolChange = true;
        _preview.ShowVoxel = true;
        _preview.Size = new Size(1244, 456);
        _preview.SpeedSamplePeriod = 0.02D;
        _preview.StlMaxEdges = 24000;
        _preview.StlWireColor = Color.Silver;
        _preview.TabIndex = 0;
        _preview.VoxelColor = Color.SteelBlue;
        _preview.VoxelMaxPoints = 12000;
        // 
        // _statusStrip
        // 
        _statusStrip.ImageScalingSize = new Size(28, 28);
        _statusStrip.Items.AddRange(new ToolStripItem[] { _status });
        _statusStrip.Location = new Point(0, 802);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Size = new Size(1244, 22);
        _statusStrip.TabIndex = 6;
        // 
        // _status
        // 
        _status.Name = "_status";
        _status.Size = new Size(1229, 17);
        _status.Spring = true;
        _status.Text = "请加载 G-code 文件。";
        // 
        // PathGeneratorForm
        // 
        ClientSize = new Size(1244, 824);
        Controls.Add(_preview);
        Controls.Add(_statusStrip);
        Controls.Add(_fileBar);
        Controls.Add(_mapBar);
        Controls.Add(_buttonBar);
        Controls.Add(_grpSwitch);
        Controls.Add(_grpProcess);
        Controls.Add(_grpSource);
        Name = "PathGeneratorForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "路径生成 — G-code 解析 / RDP 简化 / 变速规则 / CSV 导出";
        Load += PathGeneratorForm_Load;
        _grpSource.ResumeLayout(false);
        _flowSrc.ResumeLayout(false);
        _flowSrc.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numImgWidth).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numImgHeight).EndInit();
        _grpProcess.ResumeLayout(false);
        _flowProcess.ResumeLayout(false);
        _flowProcess.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numPrintVeloA).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numPrintVeloB).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numEadvancedisA).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numEadvancedisB).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numDisinternal).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numdt).EndInit();
        _grpSwitch.ResumeLayout(false);
        _flowSwitch.ResumeLayout(false);
        _flowSwitch.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numAchangeV).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numAchangeDis).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numAchangePress).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numBchangeV).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numBchangeDis).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numBchangePress).EndInit();
        _buttonBar.ResumeLayout(false);
        _buttonBar.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numTol).EndInit();
        _mapBar.ResumeLayout(false);
        _mapBar.PerformLayout();
        _fileBar.ResumeLayout(false);
        _fileBar.PerformLayout();
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }


}
