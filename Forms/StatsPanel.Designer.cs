using System.Drawing;
using System.Windows.Forms;

namespace GcodeViewer.Forms;

/// <summary>
/// 统计面板设计器分部文件。严格遵循 WinForms 设计器可解析标准模式。
/// 精简后仅保留两类功能控件：显示控制（_grpView）与选中点编辑（_grpPoint）+ 撤销。
/// 所有控件绝对定位，VS 设计器里可直接选中、拖动、改属性。
/// </summary>
public sealed partial class StatsPanel
{
    private GroupBox _grpView;
    private CheckBox _cbColorTool;
    private CheckBox _cbColorLayer;
    private CheckBox _cbColorSpeed;
    private CheckBox _cbToolChange;
    private CheckBox _cbModified;
    private CheckBox _cbFilterLayer;
    private Label _lblLayer;
    private TrackBar _layerTrack;
    private GroupBox _grpPoint;
    private Label _lblPointInfo;
    private Label _lblX;
    private Label _lblY;
    private Label _lblZ;
    private NumericUpDown _numX;
    private NumericUpDown _numY;
    private NumericUpDown _numZ;
    private Button _btnApplyCoord;
    private Button _btnG0;
    private Button _btnG1;
    private Button _btnUndo;
    private GroupBox _grpExport;
    private Label _lblSampleStep;
    private Button _btnExportCsv;

    private System.ComponentModel.IContainer components = null;

    private void InitializeComponent()
    {
        _grpView = new GroupBox();
        _cbColorTool = new CheckBox();
        _cbColorLayer = new CheckBox();
        _cbColorSpeed = new CheckBox();
        _cbToolChange = new CheckBox();
        _cbModified = new CheckBox();
        _cbFilterLayer = new CheckBox();
        _lblLayer = new Label();
        _layerTrack = new TrackBar();
        _grpPoint = new GroupBox();
        _lblPointInfo = new Label();
        _lblX = new Label();
        _numX = new NumericUpDown();
        _lblY = new Label();
        _numY = new NumericUpDown();
        _lblZ = new Label();
        _numZ = new NumericUpDown();
        _btnApplyCoord = new Button();
        _btnG0 = new Button();
        _btnG1 = new Button();
        _btnUndo = new Button();
        _grpExport = new GroupBox();
        label1 = new Label();
        numdt = new NumericUpDown();
        _lblSampleStep = new Label();
        _btnExportCsv = new Button();
        groupBox1 = new GroupBox();
        label6 = new Label();
        numericUpDown5 = new NumericUpDown();
        label4 = new Label();
        numericUpDown3 = new NumericUpDown();
        label5 = new Label();
        numericUpDown4 = new NumericUpDown();
        checkBox1 = new CheckBox();
        button1 = new Button();
        label3 = new Label();
        numericUpDown2 = new NumericUpDown();
        label2 = new Label();
        numericUpDown1 = new NumericUpDown();
        _grpView.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_layerTrack).BeginInit();
        _grpPoint.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numX).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numY).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numZ).BeginInit();
        _grpExport.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numdt).BeginInit();
        groupBox1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numericUpDown5).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericUpDown3).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericUpDown4).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericUpDown2).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericUpDown1).BeginInit();
        SuspendLayout();
        // 
        // _grpView
        // 
        _grpView.Controls.Add(_cbColorTool);
        _grpView.Controls.Add(_cbColorLayer);
        _grpView.Controls.Add(_cbColorSpeed);
        _grpView.Controls.Add(_cbToolChange);
        _grpView.Controls.Add(_cbModified);
        _grpView.Controls.Add(_cbFilterLayer);
        _grpView.Controls.Add(_lblLayer);
        _grpView.Controls.Add(_layerTrack);
        _grpView.Location = new Point(12, 12);
        _grpView.Name = "_grpView";
        _grpView.Size = new Size(276, 156);
        _grpView.TabIndex = 0;
        _grpView.TabStop = false;
        _grpView.Text = "显示";
        // 
        // _cbColorTool
        // 
        _cbColorTool.AutoSize = true;
        _cbColorTool.Location = new Point(12, 24);
        _cbColorTool.Name = "_cbColorTool";
        _cbColorTool.Size = new Size(87, 21);
        _cbColorTool.TabIndex = 0;
        _cbColorTool.Text = "按材料着色";
        // 
        // _cbColorLayer
        // 
        _cbColorLayer.AutoSize = true;
        _cbColorLayer.Location = new Point(150, 24);
        _cbColorLayer.Name = "_cbColorLayer";
        _cbColorLayer.Size = new Size(75, 21);
        _cbColorLayer.TabIndex = 1;
        _cbColorLayer.Text = "按层着色";
        // 
        // _cbColorSpeed
        // 
        _cbColorSpeed.AutoSize = true;
        _cbColorSpeed.Location = new Point(12, 72);
        _cbColorSpeed.Name = "_cbColorSpeed";
        _cbColorSpeed.Size = new Size(87, 21);
        _cbColorSpeed.TabIndex = 2;
        _cbColorSpeed.Text = "按速度着色";
        // 
        // _cbToolChange
        // 
        _cbToolChange.AutoSize = true;
        _cbToolChange.Checked = true;
        _cbToolChange.CheckState = CheckState.Checked;
        _cbToolChange.Location = new Point(12, 48);
        _cbToolChange.Name = "_cbToolChange";
        _cbToolChange.Size = new Size(87, 21);
        _cbToolChange.TabIndex = 3;
        _cbToolChange.Text = "显示切换点";
        // 
        // _cbModified
        // 
        _cbModified.AutoSize = true;
        _cbModified.Checked = true;
        _cbModified.CheckState = CheckState.Checked;
        _cbModified.Location = new Point(150, 48);
        _cbModified.Name = "_cbModified";
        _cbModified.Size = new Size(99, 21);
        _cbModified.TabIndex = 4;
        _cbModified.Text = "显示已修改点";
        // 
        // _cbFilterLayer
        // 
        _cbFilterLayer.AutoSize = true;
        _cbFilterLayer.Location = new Point(12, 96);
        _cbFilterLayer.Name = "_cbFilterLayer";
        _cbFilterLayer.Size = new Size(99, 21);
        _cbFilterLayer.TabIndex = 5;
        _cbFilterLayer.Text = "仅显示选中层";
        // 
        // _lblLayer
        // 
        _lblLayer.AutoSize = true;
        _lblLayer.Location = new Point(12, 124);
        _lblLayer.Name = "_lblLayer";
        _lblLayer.Size = new Size(51, 17);
        _lblLayer.TabIndex = 6;
        _lblLayer.Text = "层: 全部";
        // 
        // _layerTrack
        // 
        _layerTrack.Location = new Point(80, 120);
        _layerTrack.Maximum = 0;
        _layerTrack.Minimum = -1;
        _layerTrack.Name = "_layerTrack";
        _layerTrack.Size = new Size(180, 45);
        _layerTrack.TabIndex = 7;
        _layerTrack.Value = -1;
        // 
        // _grpPoint
        // 
        _grpPoint.Controls.Add(_lblPointInfo);
        _grpPoint.Controls.Add(_lblX);
        _grpPoint.Controls.Add(_numX);
        _grpPoint.Controls.Add(_lblY);
        _grpPoint.Controls.Add(_numY);
        _grpPoint.Controls.Add(_lblZ);
        _grpPoint.Controls.Add(_numZ);
        _grpPoint.Controls.Add(_btnApplyCoord);
        _grpPoint.Controls.Add(_btnG0);
        _grpPoint.Controls.Add(_btnG1);
        _grpPoint.Location = new Point(12, 178);
        _grpPoint.Name = "_grpPoint";
        _grpPoint.Size = new Size(276, 168);
        _grpPoint.TabIndex = 1;
        _grpPoint.TabStop = false;
        _grpPoint.Text = "选中点";
        // 
        // _lblPointInfo
        // 
        _lblPointInfo.Font = new Font("Consolas", 9F);
        _lblPointInfo.Location = new Point(10, 20);
        _lblPointInfo.Name = "_lblPointInfo";
        _lblPointInfo.Size = new Size(256, 44);
        _lblPointInfo.TabIndex = 0;
        // 
        // _lblX
        // 
        _lblX.AutoSize = true;
        _lblX.Location = new Point(10, 70);
        _lblX.Name = "_lblX";
        _lblX.Size = new Size(16, 17);
        _lblX.TabIndex = 1;
        _lblX.Text = "X";
        // 
        // _numX
        // 
        _numX.DecimalPlaces = 3;
        _numX.Font = new Font("Consolas", 9F);
        _numX.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        _numX.Location = new Point(28, 68);
        _numX.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
        _numX.Minimum = new decimal(new int[] { 100000, 0, 0, int.MinValue });
        _numX.Name = "_numX";
        _numX.Size = new Size(95, 22);
        _numX.TabIndex = 2;
        // 
        // _lblY
        // 
        _lblY.AutoSize = true;
        _lblY.Location = new Point(135, 70);
        _lblY.Name = "_lblY";
        _lblY.Size = new Size(15, 17);
        _lblY.TabIndex = 3;
        _lblY.Text = "Y";
        // 
        // _numY
        // 
        _numY.DecimalPlaces = 3;
        _numY.Font = new Font("Consolas", 9F);
        _numY.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        _numY.Location = new Point(153, 68);
        _numY.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
        _numY.Minimum = new decimal(new int[] { 100000, 0, 0, int.MinValue });
        _numY.Name = "_numY";
        _numY.Size = new Size(95, 22);
        _numY.TabIndex = 4;
        // 
        // _lblZ
        // 
        _lblZ.AutoSize = true;
        _lblZ.Location = new Point(10, 100);
        _lblZ.Name = "_lblZ";
        _lblZ.Size = new Size(15, 17);
        _lblZ.TabIndex = 5;
        _lblZ.Text = "Z";
        // 
        // _numZ
        // 
        _numZ.DecimalPlaces = 3;
        _numZ.Font = new Font("Consolas", 9F);
        _numZ.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        _numZ.Location = new Point(28, 98);
        _numZ.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
        _numZ.Minimum = new decimal(new int[] { 100000, 0, 0, int.MinValue });
        _numZ.Name = "_numZ";
        _numZ.Size = new Size(95, 22);
        _numZ.TabIndex = 6;
        // 
        // _btnApplyCoord
        // 
        _btnApplyCoord.Location = new Point(140, 98);
        _btnApplyCoord.Name = "_btnApplyCoord";
        _btnApplyCoord.Size = new Size(90, 28);
        _btnApplyCoord.TabIndex = 7;
        _btnApplyCoord.Text = "应用坐标";
        // 
        // _btnG0
        // 
        _btnG0.Location = new Point(10, 132);
        _btnG0.Name = "_btnG0";
        _btnG0.Size = new Size(125, 30);
        _btnG0.TabIndex = 8;
        _btnG0.Text = "设为 G0";
        // 
        // _btnG1
        // 
        _btnG1.Location = new Point(145, 132);
        _btnG1.Name = "_btnG1";
        _btnG1.Size = new Size(125, 30);
        _btnG1.TabIndex = 9;
        _btnG1.Text = "设为 G1";
        // 
        // _btnUndo
        // 
        _btnUndo.Location = new Point(12, 400);
        _btnUndo.Name = "_btnUndo";
        _btnUndo.Size = new Size(135, 32);
        _btnUndo.TabIndex = 2;
        _btnUndo.Text = "撤销 (Ctrl+Z)";
        // 
        // _grpExport
        // 
        _grpExport.Controls.Add(label1);
        _grpExport.Controls.Add(numdt);
        _grpExport.Controls.Add(_lblSampleStep);
        _grpExport.Controls.Add(_btnExportCsv);
        _grpExport.Location = new Point(12, 330);
        _grpExport.Name = "_grpExport";
        _grpExport.Size = new Size(276, 60);
        _grpExport.TabIndex = 3;
        _grpExport.TabStop = false;
        _grpExport.Text = "导出 CSV";
        // 
        // label1
        // 
        label1.AutoSize = true;
        label1.Location = new Point(111, 27);
        label1.Name = "label1";
        label1.Size = new Size(25, 17);
        label1.TabIndex = 4;
        label1.Text = "ms";
        // 
        // numdt
        // 
        numdt.Location = new Point(36, 25);
        numdt.Name = "numdt";
        numdt.Size = new Size(63, 23);
        numdt.TabIndex = 3;
        numdt.Value = new decimal(new int[] { 20, 0, 0, 0 });
        // 
        // _lblSampleStep
        // 
        _lblSampleStep.AutoSize = true;
        _lblSampleStep.Location = new Point(10, 28);
        _lblSampleStep.Name = "_lblSampleStep";
        _lblSampleStep.Size = new Size(20, 17);
        _lblSampleStep.TabIndex = 0;
        _lblSampleStep.Text = "dt";
        // 
        // _btnExportCsv
        // 
        _btnExportCsv.Location = new Point(180, 22);
        _btnExportCsv.Name = "_btnExportCsv";
        _btnExportCsv.Size = new Size(90, 26);
        _btnExportCsv.TabIndex = 2;
        _btnExportCsv.Text = "导出 CSV";
        _btnExportCsv.Click += OnExportCsv;
        // 
        // groupBox1
        // 
        groupBox1.Controls.Add(label6);
        groupBox1.Controls.Add(numericUpDown5);
        groupBox1.Controls.Add(label4);
        groupBox1.Controls.Add(numericUpDown3);
        groupBox1.Controls.Add(label5);
        groupBox1.Controls.Add(numericUpDown4);
        groupBox1.Controls.Add(checkBox1);
        groupBox1.Controls.Add(button1);
        groupBox1.Controls.Add(label3);
        groupBox1.Controls.Add(numericUpDown2);
        groupBox1.Controls.Add(label2);
        groupBox1.Controls.Add(numericUpDown1);
        groupBox1.Location = new Point(308, 12);
        groupBox1.Name = "groupBox1";
        groupBox1.Size = new Size(236, 378);
        groupBox1.TabIndex = 10;
        groupBox1.TabStop = false;
        groupBox1.Text = "CFS生成";
        // 
        // label6
        // 
        label6.AutoSize = true;
        label6.Location = new Point(6, 262);
        label6.Name = "label6";
        label6.Size = new Size(45, 17);
        label6.TabIndex = 18;
        label6.Text = "space:";
        // 
        // numericUpDown5
        // 
        numericUpDown5.Location = new Point(68, 259);
        numericUpDown5.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
        numericUpDown5.Name = "numericUpDown5";
        numericUpDown5.Size = new Size(120, 23);
        numericUpDown5.TabIndex = 17;
        // 
        // label4
        // 
        label4.AutoSize = true;
        label4.Location = new Point(6, 233);
        label4.Name = "label4";
        label4.Size = new Size(44, 17);
        label4.TabIndex = 15;
        label4.Text = "smh：";
        // 
        // numericUpDown3
        // 
        numericUpDown3.Location = new Point(68, 230);
        numericUpDown3.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
        numericUpDown3.Name = "numericUpDown3";
        numericUpDown3.Size = new Size(120, 23);
        numericUpDown3.TabIndex = 13;
        numericUpDown3.Value = new decimal(new int[] { 1000, 0, 0, 0 });
        // 
        // label5
        // 
        label5.AutoSize = true;
        label5.Location = new Point(6, 206);
        label5.Name = "label5";
        label5.Size = new Size(40, 17);
        label5.TabIndex = 16;
        label5.Text = "reg：";
        // 
        // numericUpDown4
        // 
        numericUpDown4.DecimalPlaces = 3;
        numericUpDown4.Location = new Point(68, 203);
        numericUpDown4.Name = "numericUpDown4";
        numericUpDown4.Size = new Size(120, 23);
        numericUpDown4.TabIndex = 14;
        numericUpDown4.Value = new decimal(new int[] { 1, 0, 0, 131072 });
        // 
        // checkBox1
        // 
        checkBox1.AutoSize = true;
        checkBox1.Location = new Point(68, 166);
        checkBox1.Name = "checkBox1";
        checkBox1.Size = new Size(75, 21);
        checkBox1.TabIndex = 12;
        checkBox1.Text = "曲线优化";
        checkBox1.UseVisualStyleBackColor = true;
        // 
        // button1
        // 
        button1.Location = new Point(6, 24);
        button1.Name = "button1";
        button1.Size = new Size(182, 43);
        button1.TabIndex = 11;
        button1.Text = "打开STL";
        button1.UseVisualStyleBackColor = true;
        // 
        // label3
        // 
        label3.AutoSize = true;
        label3.Location = new Point(6, 124);
        label3.Name = "label3";
        label3.Size = new Size(44, 17);
        label3.TabIndex = 10;
        label3.Text = "层高：";
        // 
        // numericUpDown2
        // 
        numericUpDown2.Location = new Point(68, 121);
        numericUpDown2.Name = "numericUpDown2";
        numericUpDown2.Size = new Size(120, 23);
        numericUpDown2.TabIndex = 0;
        // 
        // label2
        // 
        label2.AutoSize = true;
        label2.Location = new Point(6, 97);
        label2.Name = "label2";
        label2.Size = new Size(56, 17);
        label2.TabIndex = 10;
        label2.Text = "线间距：";
        // 
        // numericUpDown1
        // 
        numericUpDown1.Location = new Point(68, 94);
        numericUpDown1.Name = "numericUpDown1";
        numericUpDown1.Size = new Size(120, 23);
        numericUpDown1.TabIndex = 0;
        // 
        // StatsPanel
        // 
        AutoScroll = true;
        Controls.Add(groupBox1);
        Controls.Add(_grpView);
        Controls.Add(_grpPoint);
        Controls.Add(_btnUndo);
        Controls.Add(_grpExport);
        Name = "StatsPanel";
        Padding = new Padding(8);
        Size = new Size(1312, 908);
        _grpView.ResumeLayout(false);
        _grpView.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_layerTrack).EndInit();
        _grpPoint.ResumeLayout(false);
        _grpPoint.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numX).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numY).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numZ).EndInit();
        _grpExport.ResumeLayout(false);
        _grpExport.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numdt).EndInit();
        groupBox1.ResumeLayout(false);
        groupBox1.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numericUpDown5).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericUpDown3).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericUpDown4).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericUpDown2).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericUpDown1).EndInit();
        ResumeLayout(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private NumericUpDown numdt;
    private Label label1;
    private GroupBox groupBox1;
    private Label label3;
    private NumericUpDown numericUpDown2;
    private Label label2;
    private NumericUpDown numericUpDown1;
    private Button button1;
    private Label label6;
    private NumericUpDown numericUpDown5;
    private Label label4;
    private NumericUpDown numericUpDown3;
    private Label label5;
    private NumericUpDown numericUpDown4;
    private CheckBox checkBox1;
}
