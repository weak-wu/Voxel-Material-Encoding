using System.Drawing;
using System.Windows.Forms;
using GcodeViewer.Rendering;

namespace GcodeViewer.Forms;

/// <summary>
/// 路径评估窗体的设计器分部文件。
/// 严格遵循 WinForms 设计器可解析的标准模式：InitializeComponent 为扁平 new + 逐属性赋值序列，
/// 禁用 var / 工厂方法 / 复杂内联初始化器。所有控件、布局与事件均在此声明，
/// VS 设计器里可直接选中、拖动、改属性；按钮 Click 等事件在属性窗口事件面板可见可编辑。
/// </summary>
public sealed partial class EvaluationForm
{
    // ---- 顶部工具栏 ----
    private FlowLayoutPanel _toolbar;
    private Button _btnAdd;
    private Button _btnRemove;
    private Button _btnClear;
    private Button _btnCompute;
    private Button _btnExport;
    private Label _lblRef;
    private ComboBox _cmbRef;
    private Label _lblVoxel;
    private NumericUpDown _numVoxel;
    private Label _lblStep;
    private NumericUpDown _numStep;

    // ---- 中部分栏：左表格 / 右预览 ----
    private SplitContainer _split;
    private DataGridView _dgv;
    private DataGridViewTextBoxColumn colFile;
    private DataGridViewTextBoxColumn colPoints;
    private DataGridViewTextBoxColumn colG1Len;
    private DataGridViewTextBoxColumn colDensity;
    private DataGridViewTextBoxColumn colOccVol;
    private DataGridViewTextBoxColumn colEmpVol;
    private DataGridViewTextBoxColumn colOccRatio;
    private DataGridViewTextBoxColumn colT0Vol;
    private DataGridViewTextBoxColumn colT1Vol;
    private DataGridViewTextBoxColumn colMixed;
    private DataGridViewTextBoxColumn colSwitch;
    private DataGridViewTextBoxColumn colMeanDev;
    private DataGridViewTextBoxColumn colMaxDev;
    private DataGridViewTextBoxColumn colRms;
    private DataGridViewTextBoxColumn colHausdorff;
    private Panel _rightPanel;
    private Panel _modeBar;
    private RadioButton _rbDev;
    private RadioButton _rbVoxel;
    private Viewport3D _preview;

    // ---- 底部状态栏 ----
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _status;

    private System.ComponentModel.IContainer components = null;

    private void InitializeComponent()
    {
        this._toolbar = new System.Windows.Forms.FlowLayoutPanel();
        this._btnAdd = new System.Windows.Forms.Button();
        this._btnRemove = new System.Windows.Forms.Button();
        this._btnClear = new System.Windows.Forms.Button();
        this._btnCompute = new System.Windows.Forms.Button();
        this._btnExport = new System.Windows.Forms.Button();
        this._lblRef = new System.Windows.Forms.Label();
        this._cmbRef = new System.Windows.Forms.ComboBox();
        this._lblVoxel = new System.Windows.Forms.Label();
        this._numVoxel = new System.Windows.Forms.NumericUpDown();
        this._lblStep = new System.Windows.Forms.Label();
        this._numStep = new System.Windows.Forms.NumericUpDown();
        this._split = new System.Windows.Forms.SplitContainer();
        this._dgv = new System.Windows.Forms.DataGridView();
        this.colFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colPoints = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colG1Len = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colDensity = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colOccVol = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colEmpVol = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colOccRatio = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colT0Vol = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colT1Vol = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colMixed = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colSwitch = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colMeanDev = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colMaxDev = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colRms = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colHausdorff = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this._rightPanel = new System.Windows.Forms.Panel();
        this._modeBar = new System.Windows.Forms.Panel();
        this._rbDev = new System.Windows.Forms.RadioButton();
        this._rbVoxel = new System.Windows.Forms.RadioButton();
        this._preview = new Viewport3D();
        this._statusStrip = new System.Windows.Forms.StatusStrip();
        this._status = new System.Windows.Forms.ToolStripStatusLabel();
        this._toolbar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this._numVoxel)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this._numStep)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
        this._split.Panel1.SuspendLayout();
        this._split.Panel2.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this._dgv)).BeginInit();
        this._rightPanel.SuspendLayout();
        this._modeBar.SuspendLayout();
        this._statusStrip.SuspendLayout();
        this.SuspendLayout();
        //
        // _btnAdd
        //
        this._btnAdd.AutoSize = true;
        this._btnAdd.Margin = new System.Windows.Forms.Padding(3, 1, 3, 0);
        this._btnAdd.Name = "_btnAdd";
        this._btnAdd.Padding = new System.Windows.Forms.Padding(6, 0, 6, 0);
        this._btnAdd.Text = "添加 CSV…";
        //
        // _btnRemove
        //
        this._btnRemove.AutoSize = true;
        this._btnRemove.Margin = new System.Windows.Forms.Padding(3, 1, 3, 0);
        this._btnRemove.Name = "_btnRemove";
        this._btnRemove.Padding = new System.Windows.Forms.Padding(6, 0, 6, 0);
        this._btnRemove.Text = "移除";
        //
        // _btnClear
        //
        this._btnClear.AutoSize = true;
        this._btnClear.Margin = new System.Windows.Forms.Padding(3, 1, 3, 0);
        this._btnClear.Name = "_btnClear";
        this._btnClear.Padding = new System.Windows.Forms.Padding(6, 0, 6, 0);
        this._btnClear.Text = "清空";
        //
        // _btnCompute
        //
        this._btnCompute.AutoSize = true;
        this._btnCompute.Margin = new System.Windows.Forms.Padding(3, 1, 3, 0);
        this._btnCompute.Name = "_btnCompute";
        this._btnCompute.Padding = new System.Windows.Forms.Padding(6, 0, 6, 0);
        this._btnCompute.Text = "计算";
        //
        // _btnExport
        //
        this._btnExport.AutoSize = true;
        this._btnExport.Margin = new System.Windows.Forms.Padding(3, 1, 3, 0);
        this._btnExport.Name = "_btnExport";
        this._btnExport.Padding = new System.Windows.Forms.Padding(6, 0, 6, 0);
        this._btnExport.Text = "导出 CSV…";
        //
        // _lblRef
        //
        this._lblRef.AutoSize = true;
        this._lblRef.ForeColor = SystemColors.ControlText;
        this._lblRef.Margin = new System.Windows.Forms.Padding(8, 4, 2, 0);
        this._lblRef.Name = "_lblRef";
        this._lblRef.Text = "基准:";
        //
        // _cmbRef
        //
        this._cmbRef.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this._cmbRef.Margin = new System.Windows.Forms.Padding(6, 2, 6, 0);
        this._cmbRef.Name = "_cmbRef";
        this._cmbRef.Width = 200;
        //
        // _lblVoxel
        //
        this._lblVoxel.AutoSize = true;
        this._lblVoxel.ForeColor = SystemColors.ControlText;
        this._lblVoxel.Margin = new System.Windows.Forms.Padding(8, 4, 2, 0);
        this._lblVoxel.Name = "_lblVoxel";
        this._lblVoxel.Text = "体素尺寸 mm:";
        //
        // _numVoxel
        //
        this._numVoxel.DecimalPlaces = 2;
        this._numVoxel.Increment = 0.1m;
        this._numVoxel.Margin = new System.Windows.Forms.Padding(2, 1, 6, 0);
        this._numVoxel.Maximum = 10m;
        this._numVoxel.Minimum = 0.1m;
        this._numVoxel.Name = "_numVoxel";
        this._numVoxel.Value = 0.5m;
        this._numVoxel.Width = 60;
        //
        // _lblStep
        //
        this._lblStep.AutoSize = true;
        this._lblStep.ForeColor = SystemColors.ControlText;
        this._lblStep.Margin = new System.Windows.Forms.Padding(8, 4, 2, 0);
        this._lblStep.Name = "_lblStep";
        this._lblStep.Text = "采样步长 mm:";
        //
        // _numStep
        //
        this._numStep.DecimalPlaces = 2;
        this._numStep.Increment = 0.1m;
        this._numStep.Margin = new System.Windows.Forms.Padding(2, 1, 6, 0);
        this._numStep.Maximum = 5m;
        this._numStep.Minimum = 0.1m;
        this._numStep.Name = "_numStep";
        this._numStep.Value = 0.5m;
        this._numStep.Width = 60;
        //
        // _toolbar
        //
        this._toolbar.Controls.Add(this._btnAdd);
        this._toolbar.Controls.Add(this._btnRemove);
        this._toolbar.Controls.Add(this._btnClear);
        this._toolbar.Controls.Add(this._lblRef);
        this._toolbar.Controls.Add(this._cmbRef);
        this._toolbar.Controls.Add(this._lblVoxel);
        this._toolbar.Controls.Add(this._numVoxel);
        this._toolbar.Controls.Add(this._lblStep);
        this._toolbar.Controls.Add(this._numStep);
        this._toolbar.Controls.Add(this._btnCompute);
        this._toolbar.Controls.Add(this._btnExport);
        this._toolbar.Dock = System.Windows.Forms.DockStyle.Top;
        this._toolbar.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
        this._toolbar.Height = 44;
        this._toolbar.Name = "_toolbar";
        this._toolbar.Padding = new System.Windows.Forms.Padding(6, 8, 6, 4);
        this._toolbar.WrapContents = false;
        //
        // colFile
        //
        this.colFile.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
        this.colFile.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colFile.HeaderText = "文件";
        this.colFile.Name = "colFile";
        this.colFile.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colFile.Width = 180;
        //
        // colPoints
        //
        this.colPoints.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colPoints.HeaderText = "点数";
        this.colPoints.Name = "colPoints";
        this.colPoints.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colPoints.Width = 56;
        //
        // colG1Len
        //
        this.colG1Len.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colG1Len.HeaderText = "G1长mm";
        this.colG1Len.Name = "colG1Len";
        this.colG1Len.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colG1Len.Width = 70;
        //
        // colDensity
        //
        this.colDensity.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colDensity.HeaderText = "线密度点/mm";
        this.colDensity.Name = "colDensity";
        this.colDensity.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colDensity.Width = 84;
        //
        // colOccVol
        //
        this.colOccVol.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colOccVol.HeaderText = "占据体积mm3";
        this.colOccVol.Name = "colOccVol";
        this.colOccVol.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colOccVol.Width = 92;
        //
        // colEmpVol
        //
        this.colEmpVol.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colEmpVol.HeaderText = "空闲体积mm3";
        this.colEmpVol.Name = "colEmpVol";
        this.colEmpVol.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colEmpVol.Width = 92;
        //
        // colOccRatio
        //
        this.colOccRatio.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colOccRatio.HeaderText = "占据率%";
        this.colOccRatio.Name = "colOccRatio";
        this.colOccRatio.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colOccRatio.Width = 70;
        //
        // colT0Vol
        //
        this.colT0Vol.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colT0Vol.HeaderText = "T0体积mm3";
        this.colT0Vol.Name = "colT0Vol";
        this.colT0Vol.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colT0Vol.Width = 82;
        //
        // colT1Vol
        //
        this.colT1Vol.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colT1Vol.HeaderText = "T1体积mm3";
        this.colT1Vol.Name = "colT1Vol";
        this.colT1Vol.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colT1Vol.Width = 82;
        //
        // colMixed
        //
        this.colMixed.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colMixed.HeaderText = "混合体素";
        this.colMixed.Name = "colMixed";
        this.colMixed.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colMixed.Width = 70;
        //
        // colSwitch
        //
        this.colSwitch.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colSwitch.HeaderText = "切换次数";
        this.colSwitch.Name = "colSwitch";
        this.colSwitch.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colSwitch.Width = 70;
        //
        // colMeanDev
        //
        this.colMeanDev.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colMeanDev.HeaderText = "平均偏差mm";
        this.colMeanDev.Name = "colMeanDev";
        this.colMeanDev.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colMeanDev.Width = 82;
        //
        // colMaxDev
        //
        this.colMaxDev.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colMaxDev.HeaderText = "最大偏差mm";
        this.colMaxDev.Name = "colMaxDev";
        this.colMaxDev.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colMaxDev.Width = 82;
        //
        // colRms
        //
        this.colRms.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colRms.HeaderText = "RMSmm";
        this.colRms.Name = "colRms";
        this.colRms.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colRms.Width = 66;
        //
        // colHausdorff
        //
        this.colHausdorff.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        this.colHausdorff.HeaderText = "Hausdorffmm";
        this.colHausdorff.Name = "colHausdorff";
        this.colHausdorff.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
        this.colHausdorff.Width = 86;
        //
        // _dgv
        //
        this._dgv.AllowUserToAddRows = false;
        this._dgv.AllowUserToDeleteRows = false;
        this._dgv.AllowUserToResizeRows = false;
        this._dgv.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.None;
        this._dgv.BackgroundColor = SystemColors.Window;
        this._dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };
        this._dgv.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colFile,
            this.colPoints,
            this.colG1Len,
            this.colDensity,
            this.colOccVol,
            this.colEmpVol,
            this.colOccRatio,
            this.colT0Vol,
            this.colT1Vol,
            this.colMixed,
            this.colSwitch,
            this.colMeanDev,
            this.colMaxDev,
            this.colRms,
            this.colHausdorff});
        this._dgv.DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Consolas", 9F) };
        this._dgv.Dock = System.Windows.Forms.DockStyle.Fill;
        this._dgv.EnableHeadersVisualStyles = false;
        this._dgv.MultiSelect = false;
        this._dgv.Name = "_dgv";
        this._dgv.ReadOnly = true;
        this._dgv.RowHeadersVisible = false;
        this._dgv.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        //
        // _rbDev
        //
        this._rbDev.AutoSize = true;
        this._rbDev.Checked = true;
        this._rbDev.ForeColor = Color.WhiteSmoke;
        this._rbDev.Location = new Point(8, 8);
        this._rbDev.Name = "_rbDev";
        this._rbDev.TabStop = true;
        this._rbDev.Text = "偏差热力";
        //
        // _rbVoxel
        //
        this._rbVoxel.AutoSize = true;
        this._rbVoxel.ForeColor = Color.WhiteSmoke;
        this._rbVoxel.Location = new Point(110, 8);
        this._rbVoxel.Name = "_rbVoxel";
        this._rbVoxel.Text = "体素点云";
        //
        // _modeBar
        //
        this._modeBar.BackColor = Color.FromArgb(45, 45, 48);
        this._modeBar.Controls.Add(this._rbDev);
        this._modeBar.Controls.Add(this._rbVoxel);
        this._modeBar.Dock = System.Windows.Forms.DockStyle.Top;
        this._modeBar.Height = 34;
        this._modeBar.Name = "_modeBar";
        //
        // _preview
        //
        this._preview.Dock = System.Windows.Forms.DockStyle.Fill;
        this._preview.Name = "_preview";
        this._preview.ShowModified = false;
        this._preview.ShowToolChange = false;
        //
        // _rightPanel
        //
        // 停靠顺序：先 Add Fill(_preview) 在底层，再 Add Top(_modeBar) 因 z-order 更高而先停靠占据顶部
        this._rightPanel.Controls.Add(this._preview);
        this._rightPanel.Controls.Add(this._modeBar);
        this._rightPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this._rightPanel.Name = "_rightPanel";
        //
        // _split
        //
        this._split.Dock = System.Windows.Forms.DockStyle.Fill;
        this._split.Name = "_split";
        this._split.Orientation = System.Windows.Forms.Orientation.Vertical;
        //
        // _split.Panel1
        //
        this._split.Panel1.Controls.Add(this._dgv);
        //
        // _split.Panel2
        //
        this._split.Panel2.Controls.Add(this._rightPanel);
        // 注：SplitterDistance 在构造期不可设置（Panel2 宽度为 0 会越界），由主文件 Load 时应用
        //
        // _status
        //
        this._status.Name = "_status";
        this._status.Text = "请添加 CSV 路径文件";
        //
        // _statusStrip
        //
        this._statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this._status });
        this._statusStrip.Name = "_statusStrip";
        //
        // 事件绑定（设计器标准形式，属性窗口事件面板可见）
        //
        this._btnAdd.Click += new System.EventHandler(this.OnAdd);
        this._btnRemove.Click += new System.EventHandler(this.OnRemove);
        this._btnClear.Click += new System.EventHandler(this.OnClear);
        this._btnCompute.Click += new System.EventHandler(this.OnCompute);
        this._btnExport.Click += new System.EventHandler(this.OnExport);
        this._cmbRef.SelectedIndexChanged += new System.EventHandler(this.OnRefChanged);
        this._dgv.SelectionChanged += new System.EventHandler(this.OnGridSelectionChanged);
        this._rbDev.CheckedChanged += new System.EventHandler(this.OnModeChanged);
        //
        // EvaluationForm
        //
        this.ClientSize = new Size(1140, 700);
        this.Controls.Add(this._split);
        this.Controls.Add(this._toolbar);
        this.Controls.Add(this._statusStrip);
        this.MinimumSize = new Size(900, 560);
        this.Name = "EvaluationForm";
        this.ShowIcon = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "路径评估 — 偏差 / 密度 / 体素体积";
        this._toolbar.ResumeLayout(false);
        this._toolbar.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this._numVoxel)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this._numStep)).EndInit();
        this._split.Panel1.ResumeLayout(false);
        this._split.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
        this._split.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)(this._dgv)).EndInit();
        this._rightPanel.ResumeLayout(false);
        this._modeBar.ResumeLayout(false);
        this._modeBar.PerformLayout();
        this._statusStrip.ResumeLayout(false);
        this._statusStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }
}
