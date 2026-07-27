using System.Drawing;
using System.Windows.Forms;
using GcodeViewer.Rendering;

namespace GcodeViewer.Forms;

/// <summary>
/// 体素生成器窗体的设计器分部文件。
/// 严格遵循 WinForms 设计器可解析标准模式：InitializeComponent 为扁平 new + 逐属性赋值序列，
/// 禁用 var / 工厂方法 / 复杂内联初始化器。控件、布局、事件均在此声明，
/// VS 设计器里可直接选中、拖动、改属性；按钮 Click 事件在属性窗口事件面板可见可编辑。
/// </summary>
public sealed partial class VoxelGeneratorForm
{
    private Panel _barTop;
    private FlowLayoutPanel _flowTop;
    private Button _btnLoadStl;
    private Label _lblStlFile;
    private Label _lblAxis;
    private ComboBox _cboAxis;
    private Label _lblVoxelSize;
    private NumericUpDown _numVoxelSize;
    private Button _btnGenerate;
    private Button _btnExport;
    private Label _lblStatus;
    private Viewport3D _preview;

    private void InitializeComponent()
    {
        _barTop = new Panel();
        _flowTop = new FlowLayoutPanel();
        _btnLoadStl = new Button();
        _lblStlFile = new Label();
        _lblAxis = new Label();
        _cboAxis = new ComboBox();
        _lblVoxelSize = new Label();
        _numVoxelSize = new NumericUpDown();
        _btnGenerate = new Button();
        _btnExport = new Button();
        _lblStatus = new Label();
        _preview = new Viewport3D();

        _barTop.SuspendLayout();
        _flowTop.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numVoxelSize).BeginInit();

        // _btnLoadStl
        _btnLoadStl.AutoSize = true;
        _btnLoadStl.FlatStyle = FlatStyle.System;
        _btnLoadStl.Margin = new Padding(0, 6, 8, 6);
        _btnLoadStl.Name = "_btnLoadStl";
        _btnLoadStl.Text = "导入 STL…";
        _btnLoadStl.Click += OnLoadStl;

        // _lblStlFile
        _lblStlFile.AutoSize = true;
        _lblStlFile.Margin = new Padding(0, 10, 12, 0);
        _lblStlFile.Name = "_lblStlFile";
        _lblStlFile.Text = "(未导入)";

        // _lblAxis
        _lblAxis.AutoSize = true;
        _lblAxis.Margin = new Padding(0, 10, 4, 0);
        _lblAxis.Name = "_lblAxis";
        _lblAxis.Text = "投影方向:";

        // _cboAxis
        _cboAxis.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboAxis.Items.AddRange(new object[] { "Z", "X", "Y" });
        _cboAxis.Margin = new Padding(0, 6, 12, 6);
        _cboAxis.Name = "_cboAxis";
        _cboAxis.Width = 60;

        // _lblVoxelSize
        _lblVoxelSize.AutoSize = true;
        _lblVoxelSize.Margin = new Padding(0, 10, 4, 0);
        _lblVoxelSize.Name = "_lblVoxelSize";
        _lblVoxelSize.Text = "体素边长(mm):";

        // _numVoxelSize
        _numVoxelSize.DecimalPlaces = 3;
        _numVoxelSize.Maximum = 200M;
        _numVoxelSize.Minimum = 0.05M;
        _numVoxelSize.Margin = new Padding(0, 6, 12, 6);
        _numVoxelSize.Name = "_numVoxelSize";
        _numVoxelSize.Value = 1M;
        _numVoxelSize.Width = 70;

        // _btnGenerate
        _btnGenerate.AutoSize = true;
        _btnGenerate.FlatStyle = FlatStyle.System;
        _btnGenerate.Margin = new Padding(0, 6, 8, 6);
        _btnGenerate.Name = "_btnGenerate";
        _btnGenerate.Text = "生成体素";
        _btnGenerate.Click += OnGenerate;

        // _btnExport
        _btnExport.AutoSize = true;
        _btnExport.FlatStyle = FlatStyle.System;
        _btnExport.Margin = new Padding(0, 6, 12, 6);
        _btnExport.Name = "_btnExport";
        _btnExport.Text = "导出 CSV…";
        _btnExport.Click += OnExport;

        // _lblStatus
        _lblStatus.AutoSize = true;
        _lblStatus.Margin = new Padding(0, 10, 0, 0);
        _lblStatus.Name = "_lblStatus";
        _lblStatus.Text = "请导入 STL 后生成。";

        // _flowTop
        _flowTop.Controls.Add(_btnLoadStl);
        _flowTop.Controls.Add(_lblStlFile);
        _flowTop.Controls.Add(_lblAxis);
        _flowTop.Controls.Add(_cboAxis);
        _flowTop.Controls.Add(_lblVoxelSize);
        _flowTop.Controls.Add(_numVoxelSize);
        _flowTop.Controls.Add(_btnGenerate);
        _flowTop.Controls.Add(_btnExport);
        _flowTop.Controls.Add(_lblStatus);
        _flowTop.Dock = DockStyle.Fill;
        _flowTop.FlowDirection = FlowDirection.LeftToRight;
        _flowTop.Name = "_flowTop";
        _flowTop.Padding = new Padding(8, 4, 8, 4);
        _flowTop.WrapContents = true;

        // _barTop
        _barTop.Controls.Add(_flowTop);
        _barTop.Dock = DockStyle.Top;
        _barTop.Name = "_barTop";
        _barTop.Height = 90;

        // _preview
        _preview.BackColor = Color.White;
        _preview.ColorByTool = true;
        _preview.Dock = DockStyle.Fill;
        _preview.Name = "_preview";
        _preview.ShowStl = true;
        _preview.ShowVoxel = true;
        _preview.StlMaxEdges = 24000;
        _preview.StlWireColor = Color.Silver;
        _preview.VoxelColor = Color.SteelBlue;
        _preview.VoxelMaxPoints = 12000;

        // VoxelGeneratorForm
        ClientSize = new Size(1100, 720);
        Controls.Add(_preview);
        Controls.Add(_barTop);
        Name = "VoxelGeneratorForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "体素生成器 — 光线投影法 (STL → 体素 CSV)";

        _barTop.ResumeLayout(false);
        _flowTop.ResumeLayout(false);
        _flowTop.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numVoxelSize).EndInit();
    }
}
