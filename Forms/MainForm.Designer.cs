using System.Drawing;
using System.Windows.Forms;
using GcodeViewer.Rendering;

namespace GcodeViewer.Forms;

/// <summary>
/// 主窗体的设计器分部文件。
/// 此处严格遵循 WinForms 设计器可解析的标准模式（InitializeComponent 为扁平序列）：
/// 顶部统一 new 所有组件，逐个设属性，禁用 var/元组/自定义辅助方法/内联匿名对象。
/// 注意：SplitContainer 的 SplitterDistance 在构造期不可设置（宽度为 0 会越界），
/// 运行期布局比例由 MainForm.ApplyDefaultLayout() 在 Load 时应用。
/// </summary>
public sealed partial class MainForm
{
    // ---- 容器 ----
    private SplitContainer _splitOuter;
    private SplitContainer _splitMain;
    private SplitContainer _splitMid;
    private Panel _leftPanel;
    private Panel _codePanel;
    private ToolStripSeparator _sepFile;
    private ToolStripSeparator _sepView;

    // ---- 左侧：打开按钮 + 层/切换列表 ----
    private Button _openBtn;
    private TabControl _leftTabs;
    private TabPage _tabLayers;
    private TabPage _tabSwitches;
    private ListView _layerList;
    private ListView _switchList;

    // ---- 中：3D 视图 ----
    private Viewport3D _viewport;

    // ---- 右：统计/编辑 ----
    private StatsPanel _statsPanel;

    // ---- 下：gcode 文本浏览 ----
    private FlowLayoutPanel _gotoBar;
    private Label _lblGoto;
    private TextBox _gotoBox;
    private Button _gotoBtn;
    private ListView _codeList;

    // ---- 状态栏 ----
    private Label _statusBar;

    // ---- 菜单 ----
    private MenuStrip _menuStrip;
    private ToolStripMenuItem _miFile;
    private ToolStripMenuItem _miOpen;
    private ToolStripMenuItem _miSave;
    private ToolStripMenuItem _miExit;
    private ToolStripMenuItem _miView;
    private ToolStripMenuItem _miFullScreen;
    private ToolStripMenuItem _miResetLayout;
    private ToolStripMenuItem _miFit;
    private ToolStripMenuItem _miTopView;
    private ToolStripMenuItem _miHelp;
    private ToolStripMenuItem _miAbout;
    private ToolStripMenuItem _miEvaluate;
    private ToolStripMenuItem _miEvalCompare;
    private ToolStripMenuItem _miGenerate;
    private ToolStripMenuItem _miPathGen;
    private ToolStripMenuItem _miBigData;
    private ToolStripMenuItem _miVoxelGen;

    private System.ComponentModel.IContainer components = null;

    private void InitializeComponent()
    {
        _splitOuter = new SplitContainer();
        _splitMain = new SplitContainer();
        _leftPanel = new Panel();
        _leftTabs = new TabControl();
        _tabLayers = new TabPage();
        _layerList = new ListView();
        _tabSwitches = new TabPage();
        _switchList = new ListView();
        _openBtn = new Button();
        _splitMid = new SplitContainer();
        _viewport = new Viewport3D();
        _statsPanel = new StatsPanel();
        _codePanel = new Panel();
        _codeList = new ListView();
        _gotoBar = new FlowLayoutPanel();
        _lblGoto = new Label();
        _gotoBox = new TextBox();
        _gotoBtn = new Button();
        _statusBar = new Label();
        _menuStrip = new MenuStrip();
        _miFile = new ToolStripMenuItem();
        _miOpen = new ToolStripMenuItem();
        _miSave = new ToolStripMenuItem();
        _sepFile = new ToolStripSeparator();
        _miExit = new ToolStripMenuItem();
        _miView = new ToolStripMenuItem();
        _miFullScreen = new ToolStripMenuItem();
        _miResetLayout = new ToolStripMenuItem();
        _sepView = new ToolStripSeparator();
        _miFit = new ToolStripMenuItem();
        _miTopView = new ToolStripMenuItem();
        _miEvaluate = new ToolStripMenuItem();
        _miEvalCompare = new ToolStripMenuItem();
        _miGenerate = new ToolStripMenuItem();
        _miPathGen = new ToolStripMenuItem();
        _miBigData = new ToolStripMenuItem();
        _miVoxelGen = new ToolStripMenuItem();
        _miHelp = new ToolStripMenuItem();
        _miAbout = new ToolStripMenuItem();
        _miLineWidth = new ToolStripMenuItem();
        ((System.ComponentModel.ISupportInitialize)_splitOuter).BeginInit();
        _splitOuter.Panel1.SuspendLayout();
        _splitOuter.Panel2.SuspendLayout();
        _splitOuter.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_splitMain).BeginInit();
        _splitMain.Panel1.SuspendLayout();
        _splitMain.Panel2.SuspendLayout();
        _splitMain.SuspendLayout();
        _leftPanel.SuspendLayout();
        _leftTabs.SuspendLayout();
        _tabLayers.SuspendLayout();
        _tabSwitches.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_splitMid).BeginInit();
        _splitMid.Panel1.SuspendLayout();
        _splitMid.Panel2.SuspendLayout();
        _splitMid.SuspendLayout();
        _codePanel.SuspendLayout();
        _gotoBar.SuspendLayout();
        _menuStrip.SuspendLayout();
        SuspendLayout();
        // 
        // _splitOuter
        // 
        _splitOuter.Dock = DockStyle.Fill;
        _splitOuter.Location = new Point(0, 25);
        _splitOuter.Name = "_splitOuter";
        _splitOuter.Orientation = Orientation.Horizontal;
        // 
        // _splitOuter.Panel1
        // 
        _splitOuter.Panel1.Controls.Add(_splitMain);
        // 
        // _splitOuter.Panel2
        // 
        _splitOuter.Panel2.Controls.Add(_codePanel);
        _splitOuter.Size = new Size(1364, 792);
        _splitOuter.SplitterDistance = 396;
        _splitOuter.TabIndex = 0;
        // 
        // _splitMain
        // 
        _splitMain.Dock = DockStyle.Fill;
        _splitMain.Location = new Point(0, 0);
        _splitMain.Name = "_splitMain";
        // 
        // _splitMain.Panel1
        // 
        _splitMain.Panel1.Controls.Add(_leftPanel);
        // 
        // _splitMain.Panel2
        // 
        _splitMain.Panel2.Controls.Add(_splitMid);
        _splitMain.Size = new Size(1364, 396);
        _splitMain.SplitterDistance = 454;
        _splitMain.TabIndex = 0;
        // 
        // _leftPanel
        // 
        _leftPanel.Controls.Add(_leftTabs);
        _leftPanel.Controls.Add(_openBtn);
        _leftPanel.Dock = DockStyle.Fill;
        _leftPanel.Location = new Point(0, 0);
        _leftPanel.Name = "_leftPanel";
        _leftPanel.Size = new Size(454, 396);
        _leftPanel.TabIndex = 0;
        // 
        // _leftTabs
        // 
        _leftTabs.Controls.Add(_tabLayers);
        _leftTabs.Controls.Add(_tabSwitches);
        _leftTabs.Dock = DockStyle.Fill;
        _leftTabs.Location = new Point(0, 34);
        _leftTabs.Name = "_leftTabs";
        _leftTabs.SelectedIndex = 0;
        _leftTabs.Size = new Size(454, 362);
        _leftTabs.TabIndex = 0;
        // 
        // _tabLayers
        // 
        _tabLayers.Controls.Add(_layerList);
        _tabLayers.Location = new Point(4, 26);
        _tabLayers.Name = "_tabLayers";
        _tabLayers.Size = new Size(446, 332);
        _tabLayers.TabIndex = 0;
        _tabLayers.Text = "层列表";
        // 
        // _layerList
        // 
        _layerList.Dock = DockStyle.Fill;
        _layerList.Font = new Font("Consolas", 9F);
        _layerList.FullRowSelect = true;
        _layerList.Location = new Point(0, 0);
        _layerList.Name = "_layerList";
        _layerList.Size = new Size(446, 332);
        _layerList.TabIndex = 0;
        _layerList.UseCompatibleStateImageBehavior = false;
        _layerList.View = View.Details;
        // 
        // _tabSwitches
        // 
        _tabSwitches.Controls.Add(_switchList);
        _tabSwitches.Location = new Point(4, 26);
        _tabSwitches.Name = "_tabSwitches";
        _tabSwitches.Size = new Size(446, 332);
        _tabSwitches.TabIndex = 1;
        _tabSwitches.Text = "切换点";
        // 
        // _switchList
        // 
        _switchList.Dock = DockStyle.Fill;
        _switchList.Font = new Font("Consolas", 9F);
        _switchList.FullRowSelect = true;
        _switchList.Location = new Point(0, 0);
        _switchList.Name = "_switchList";
        _switchList.Size = new Size(446, 332);
        _switchList.TabIndex = 0;
        _switchList.UseCompatibleStateImageBehavior = false;
        _switchList.View = View.Details;
        // 
        // _openBtn
        // 
        _openBtn.Dock = DockStyle.Top;
        _openBtn.FlatStyle = FlatStyle.System;
        _openBtn.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        _openBtn.Location = new Point(0, 0);
        _openBtn.Name = "_openBtn";
        _openBtn.Size = new Size(454, 34);
        _openBtn.TabIndex = 1;
        _openBtn.Text = "打开文件…";
        _openBtn.Click += _openBtn_Click_1;
        // 
        // _splitMid
        // 
        _splitMid.Dock = DockStyle.Fill;
        _splitMid.Location = new Point(0, 0);
        _splitMid.Name = "_splitMid";
        // 
        // _splitMid.Panel1
        // 
        _splitMid.Panel1.Controls.Add(_viewport);
        // 
        // _splitMid.Panel2
        // 
        _splitMid.Panel2.Controls.Add(_statsPanel);
        _splitMid.Size = new Size(906, 396);
        _splitMid.SplitterDistance = 302;
        _splitMid.TabIndex = 0;
        // 
        // _viewport
        // 
        _viewport.BackColor = Color.White;
        _viewport.ColorByLayer = false;
        _viewport.ColorByTool = true;
        _viewport.Dock = DockStyle.Fill;
        _viewport.FilterLayer = -1;
        _viewport.G0LineWidth = 2F;
        _viewport.G1LineWidth = 3.5F;
        _viewport.Location = new Point(0, 0);
        _viewport.Name = "_viewport";
        _viewport.ShowModified = true;
        _viewport.ShowStl = true;
        _viewport.ShowToolChange = true;
        _viewport.ShowVoxel = true;
        _viewport.Size = new Size(302, 396);
        _viewport.StlMaxEdges = 24000;
        _viewport.StlWireColor = Color.Silver;
        _viewport.TabIndex = 0;
        _viewport.VoxelColor = Color.SteelBlue;
        _viewport.VoxelMaxPoints = 12000;
        // 
        // _statsPanel
        // 
        _statsPanel.AutoScroll = true;
        _statsPanel.Dock = DockStyle.Fill;
        _statsPanel.Location = new Point(0, 0);
        _statsPanel.Name = "_statsPanel";
        _statsPanel.Padding = new Padding(8);
        _statsPanel.Size = new Size(600, 396);
        _statsPanel.TabIndex = 0;
        // 
        // _codePanel
        // 
        _codePanel.Controls.Add(_codeList);
        _codePanel.Controls.Add(_gotoBar);
        _codePanel.Dock = DockStyle.Fill;
        _codePanel.Location = new Point(0, 0);
        _codePanel.Name = "_codePanel";
        _codePanel.Size = new Size(1364, 392);
        _codePanel.TabIndex = 0;
        // 
        // _codeList
        // 
        _codeList.Dock = DockStyle.Bottom;
        _codeList.Font = new Font("Consolas", 9F);
        _codeList.FullRowSelect = true;
        _codeList.Location = new Point(0, 173);
        _codeList.Name = "_codeList";
        _codeList.Size = new Size(1364, 219);
        _codeList.TabIndex = 0;
        _codeList.UseCompatibleStateImageBehavior = false;
        _codeList.View = View.Details;
        // 
        // _gotoBar
        // 
        _gotoBar.Controls.Add(_lblGoto);
        _gotoBar.Controls.Add(_gotoBox);
        _gotoBar.Controls.Add(_gotoBtn);
        _gotoBar.Dock = DockStyle.Top;
        _gotoBar.Location = new Point(0, 0);
        _gotoBar.Name = "_gotoBar";
        _gotoBar.Padding = new Padding(4);
        _gotoBar.Size = new Size(1364, 70);
        _gotoBar.TabIndex = 1;
        // 
        // _lblGoto
        // 
        _lblGoto.AutoSize = true;
        _lblGoto.Location = new Point(7, 10);
        _lblGoto.Margin = new Padding(3, 6, 3, 0);
        _lblGoto.Name = "_lblGoto";
        _lblGoto.Size = new Size(59, 17);
        _lblGoto.TabIndex = 0;
        _lblGoto.Text = "跳转行号:";
        // 
        // _gotoBox
        // 
        _gotoBox.Location = new Point(72, 7);
        _gotoBox.Name = "_gotoBox";
        _gotoBox.Size = new Size(80, 23);
        _gotoBox.TabIndex = 1;
        // 
        // _gotoBtn
        // 
        _gotoBtn.AutoSize = true;
        _gotoBtn.Location = new Point(158, 7);
        _gotoBtn.Name = "_gotoBtn";
        _gotoBtn.Size = new Size(75, 27);
        _gotoBtn.TabIndex = 2;
        _gotoBtn.Text = "跳转";
        // 
        // _statusBar
        // 
        _statusBar.BackColor = Color.FromArgb(45, 45, 48);
        _statusBar.Dock = DockStyle.Bottom;
        _statusBar.ForeColor = Color.WhiteSmoke;
        _statusBar.Location = new Point(0, 817);
        _statusBar.Name = "_statusBar";
        _statusBar.Padding = new Padding(8, 0, 0, 0);
        _statusBar.Size = new Size(1364, 24);
        _statusBar.TabIndex = 1;
        _statusBar.Text = "文件: (未打开)";
        _statusBar.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _menuStrip
        // 
        _menuStrip.Items.AddRange(new ToolStripItem[] { _miFile, _miView, _miEvaluate, _miGenerate, _miHelp });
        _menuStrip.Location = new Point(0, 0);
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new Size(1364, 25);
        _menuStrip.TabIndex = 2;
        // 
        // _miFile
        // 
        _miFile.DropDownItems.AddRange(new ToolStripItem[] { _miOpen, _miSave, _sepFile, _miExit });
        _miFile.Name = "_miFile";
        _miFile.Size = new Size(58, 21);
        _miFile.Text = "文件(&F)";
        // 
        // _miOpen
        // 
        _miOpen.Name = "_miOpen";
        _miOpen.Size = new Size(154, 22);
        _miOpen.Text = "打开…";
        // 
        // _miSave
        // 
        _miSave.Name = "_miSave";
        _miSave.Size = new Size(154, 22);
        _miSave.Text = "保存(另存为)…";
        // 
        // _sepFile
        // 
        _sepFile.Name = "_sepFile";
        _sepFile.Size = new Size(151, 6);
        // 
        // _miExit
        // 
        _miExit.Name = "_miExit";
        _miExit.Size = new Size(154, 22);
        _miExit.Text = "退出";
        // 
        // _miView
        // 
        _miView.DropDownItems.AddRange(new ToolStripItem[] { _miFullScreen, _miResetLayout, _sepView, _miFit, _miTopView });
        _miView.Name = "_miView";
        _miView.Size = new Size(60, 21);
        _miView.Text = "视图(&V)";
        // 
        // _miFullScreen
        // 
        _miFullScreen.Name = "_miFullScreen";
        _miFullScreen.ShortcutKeys = Keys.F11;
        _miFullScreen.Size = new Size(160, 22);
        _miFullScreen.Text = "全屏 3D";
        // 
        // _miResetLayout
        // 
        _miResetLayout.Name = "_miResetLayout";
        _miResetLayout.Size = new Size(160, 22);
        _miResetLayout.Text = "重置布局";
        // 
        // _sepView
        // 
        _sepView.Name = "_sepView";
        _sepView.Size = new Size(157, 6);
        // 
        // _miFit
        // 
        _miFit.Name = "_miFit";
        _miFit.Size = new Size(160, 22);
        _miFit.Text = "自适应缩放路径";
        // 
        // _miTopView
        // 
        _miTopView.Name = "_miTopView";
        _miTopView.Size = new Size(160, 22);
        _miTopView.Text = "俯视(沿 Z 轴)";
        // 
        // _miEvaluate
        // 
        _miEvaluate.DropDownItems.AddRange(new ToolStripItem[] { _miEvalCompare, _miLineWidth });
        _miEvaluate.Name = "_miEvaluate";
        _miEvaluate.Size = new Size(59, 21);
        _miEvaluate.Text = "评估(&E)";
        // 
        // _miEvalCompare
        // 
        _miEvalCompare.Name = "_miEvalCompare";
        _miEvalCompare.Size = new Size(278, 22);
        _miEvalCompare.Text = "路径对比评估…";
        // 
        // _miGenerate
        // 
        _miGenerate.DropDownItems.AddRange(new ToolStripItem[] { _miPathGen, _miBigData, _miVoxelGen });
        _miGenerate.Name = "_miGenerate";
        _miGenerate.Size = new Size(61, 21);
        _miGenerate.Text = "生成(&G)";
        // 
        // _miPathGen
        // 
        _miPathGen.Name = "_miPathGen";
        _miPathGen.Size = new Size(272, 22);
        _miPathGen.Text = "路径生成（G-code→简化→CSV）…";
        // 
        // _miBigData
        // 
        _miBigData.Name = "_miBigData";
        _miBigData.Size = new Size(272, 22);
        _miBigData.Text = "多组双材料 G-code 生成…";
        //
        // _miVoxelGen
        //
        _miVoxelGen.Name = "_miVoxelGen";
        _miVoxelGen.Size = new Size(272, 22);
        _miVoxelGen.Text = "体素生成器（STL→体素 CSV）…";
        // 
        // _miHelp
        // 
        _miHelp.DropDownItems.AddRange(new ToolStripItem[] { _miAbout });
        _miHelp.Name = "_miHelp";
        _miHelp.Size = new Size(61, 21);
        _miHelp.Text = "帮助(&H)";
        // 
        // _miAbout
        // 
        _miAbout.Name = "_miAbout";
        _miAbout.Size = new Size(100, 22);
        _miAbout.Text = "说明";
        // 
        // _miLineWidth
        // 
        _miLineWidth.Name = "_miLineWidth";
        _miLineWidth.Size = new Size(278, 22);
        _miLineWidth.Text = "打印线宽提取（图像→HSV→线宽）…";
        // 
        // MainForm
        // 
        ClientSize = new Size(1364, 841);
        Controls.Add(_splitOuter);
        Controls.Add(_statusBar);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "GcodeViewer — G-code 路径分析与编辑";
        _splitOuter.Panel1.ResumeLayout(false);
        _splitOuter.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitOuter).EndInit();
        _splitOuter.ResumeLayout(false);
        _splitMain.Panel1.ResumeLayout(false);
        _splitMain.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitMain).EndInit();
        _splitMain.ResumeLayout(false);
        _leftPanel.ResumeLayout(false);
        _leftTabs.ResumeLayout(false);
        _tabLayers.ResumeLayout(false);
        _tabSwitches.ResumeLayout(false);
        _splitMid.Panel1.ResumeLayout(false);
        _splitMid.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitMid).EndInit();
        _splitMid.ResumeLayout(false);
        _codePanel.ResumeLayout(false);
        _gotoBar.ResumeLayout(false);
        _gotoBar.PerformLayout();
        _menuStrip.ResumeLayout(false);
        _menuStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private ToolStripMenuItem _miLineWidth;
}
