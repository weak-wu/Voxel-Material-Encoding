namespace GcodeViewer.Forms
{
    partial class 大数据G_code
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnGenerateGCode = new Button();
            txtOutputFile = new TextBox();
            label1 = new Label();
            numXlen = new NumericUpDown();
            label2 = new Label();
            label125 = new Label();
            label124 = new Label();
            label123 = new Label();
            num_stepChange1 = new NumericUpDown();
            label122 = new Label();
            num_vChange1 = new NumericUpDown();
            num_stepChange0 = new NumericUpDown();
            label121 = new Label();
            label120 = new Label();
            label119 = new Label();
            num_vChange0 = new NumericUpDown();
            label118 = new Label();
            label98 = new Label();
            label96 = new Label();
            numNormalSpeed0 = new NumericUpDown();
            numYlen = new NumericUpDown();
            label3 = new Label();
            numInnerLayerHeight = new NumericUpDown();
            label4 = new Label();
            la = new Label();
            numOuterLayerHeight = new NumericUpDown();
            chkContinuousExtrusion = new CheckBox();
            numMaterialLen0 = new NumericUpDown();
            label5 = new Label();
            label6 = new Label();
            numMaterialLen1 = new NumericUpDown();
            label7 = new Label();
            label8 = new Label();
            label9 = new Label();
            label10 = new Label();
            label11 = new Label();
            label12 = new Label();
            btnAddGroup = new Button();
            listGocdeGroups = new ListBox();
            btnClearAll = new Button();
            label13 = new Label();
            numNormalSpeed1 = new NumericUpDown();
            label14 = new Label();
            openFileDialog1 = new OpenFileDialog();
            saveFileDialog1 = new SaveFileDialog();
            btnViewInMain = new Button();
            num_Repeat0 = new NumericUpDown();
            label15 = new Label();
            num_Repeat1 = new NumericUpDown();
            label16 = new Label();
            chkIsRepeat = new CheckBox();
            chkSwitchManage = new CheckBox();
            numNormalPressure1 = new NumericUpDown();
            numNormalPressure2 = new NumericUpDown();
            label17 = new Label();
            label18 = new Label();
            label19 = new Label();
            label20 = new Label();
            ((System.ComponentModel.ISupportInitialize)numXlen).BeginInit();
            ((System.ComponentModel.ISupportInitialize)num_stepChange1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)num_vChange1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)num_stepChange0).BeginInit();
            ((System.ComponentModel.ISupportInitialize)num_vChange0).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numNormalSpeed0).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numYlen).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numInnerLayerHeight).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numOuterLayerHeight).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numMaterialLen0).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numMaterialLen1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numNormalSpeed1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)num_Repeat0).BeginInit();
            ((System.ComponentModel.ISupportInitialize)num_Repeat1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numNormalPressure1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numNormalPressure2).BeginInit();
            SuspendLayout();
            // 
            // btnGenerateGCode
            // 
            btnGenerateGCode.Font = new Font("宋体", 12F, FontStyle.Bold, GraphicsUnit.Point, 134);
            btnGenerateGCode.Location = new Point(34, 542);
            btnGenerateGCode.Margin = new Padding(4);
            btnGenerateGCode.Name = "btnGenerateGCode";
            btnGenerateGCode.Size = new Size(155, 51);
            btnGenerateGCode.TabIndex = 0;
            btnGenerateGCode.Text = "生成Gcode";
            btnGenerateGCode.UseVisualStyleBackColor = true;
            btnGenerateGCode.Click += btnGenerateGCode_Click;
            // 
            // txtOutputFile
            // 
            txtOutputFile.Location = new Point(298, 570);
            txtOutputFile.Margin = new Padding(4);
            txtOutputFile.Name = "txtOutputFile";
            txtOutputFile.Size = new Size(254, 23);
            txtOutputFile.TabIndex = 2;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(215, 574);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(68, 17);
            label1.TabIndex = 3;
            label1.Text = "输出文件名";
            // 
            // numXlen
            // 
            numXlen.Location = new Point(124, 85);
            numXlen.Margin = new Padding(4);
            numXlen.Name = "numXlen";
            numXlen.Size = new Size(140, 23);
            numXlen.TabIndex = 4;
            numXlen.Value = new decimal(new int[] { 20, 0, 0, 0 });
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("宋体", 12F);
            label2.Location = new Point(58, 89);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(47, 16);
            label2.TabIndex = 5;
            label2.Text = "X长度";
            // 
            // label125
            // 
            label125.AutoSize = true;
            label125.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label125.Location = new Point(264, 367);
            label125.Margin = new Padding(4, 0, 4, 0);
            label125.Name = "label125";
            label125.Size = new Size(23, 16);
            label125.TabIndex = 286;
            label125.Text = "mm";
            // 
            // label124
            // 
            label124.AutoSize = true;
            label124.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label124.Location = new Point(565, 458);
            label124.Margin = new Padding(4, 0, 4, 0);
            label124.Name = "label124";
            label124.Size = new Size(39, 16);
            label124.TabIndex = 285;
            label124.Text = "mm/s";
            // 
            // label123
            // 
            label123.AutoSize = true;
            label123.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label123.Location = new Point(566, 404);
            label123.Margin = new Padding(4, 0, 4, 0);
            label123.Name = "label123";
            label123.Size = new Size(23, 16);
            label123.TabIndex = 284;
            label123.Text = "mm";
            // 
            // num_stepChange1
            // 
            num_stepChange1.DecimalPlaces = 1;
            num_stepChange1.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            num_stepChange1.Location = new Point(470, 494);
            num_stepChange1.Margin = new Padding(4);
            num_stepChange1.Maximum = new decimal(new int[] { 400, 0, 0, 0 });
            num_stepChange1.Name = "num_stepChange1";
            num_stepChange1.Size = new Size(90, 23);
            num_stepChange1.TabIndex = 283;
            // 
            // label122
            // 
            label122.AutoSize = true;
            label122.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label122.Location = new Point(373, 497);
            label122.Margin = new Padding(4, 0, 4, 0);
            label122.Name = "label122";
            label122.Size = new Size(87, 16);
            label122.TabIndex = 282;
            label122.Text = "1切换距离:";
            // 
            // num_vChange1
            // 
            num_vChange1.DecimalPlaces = 1;
            num_vChange1.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            num_vChange1.Location = new Point(470, 453);
            num_vChange1.Margin = new Padding(4);
            num_vChange1.Maximum = new decimal(new int[] { 400, 0, 0, 0 });
            num_vChange1.Name = "num_vChange1";
            num_vChange1.Size = new Size(90, 23);
            num_vChange1.TabIndex = 281;
            num_vChange1.Value = new decimal(new int[] { 30, 0, 0, 0 });
            // 
            // num_stepChange0
            // 
            num_stepChange0.DecimalPlaces = 1;
            num_stepChange0.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            num_stepChange0.Location = new Point(470, 402);
            num_stepChange0.Margin = new Padding(4);
            num_stepChange0.Maximum = new decimal(new int[] { 400, 0, 0, 0 });
            num_stepChange0.Name = "num_stepChange0";
            num_stepChange0.Size = new Size(90, 23);
            num_stepChange0.TabIndex = 280;
            num_stepChange0.Value = new decimal(new int[] { 3, 0, 0, 0 });
            // 
            // label121
            // 
            label121.AutoSize = true;
            label121.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label121.Location = new Point(373, 407);
            label121.Margin = new Padding(4, 0, 4, 0);
            label121.Name = "label121";
            label121.Size = new Size(87, 16);
            label121.TabIndex = 279;
            label121.Text = "0切换距离:";
            // 
            // label120
            // 
            label120.AutoSize = true;
            label120.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label120.Location = new Point(373, 455);
            label120.Margin = new Padding(4, 0, 4, 0);
            label120.Name = "label120";
            label120.Size = new Size(87, 16);
            label120.TabIndex = 278;
            label120.Text = "1切换速度:";
            // 
            // label119
            // 
            label119.AutoSize = true;
            label119.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label119.Location = new Point(565, 367);
            label119.Margin = new Padding(4, 0, 4, 0);
            label119.Name = "label119";
            label119.Size = new Size(39, 16);
            label119.TabIndex = 277;
            label119.Text = "mm/s";
            // 
            // num_vChange0
            // 
            num_vChange0.DecimalPlaces = 1;
            num_vChange0.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            num_vChange0.Location = new Point(470, 366);
            num_vChange0.Margin = new Padding(4);
            num_vChange0.Maximum = new decimal(new int[] { 400, 0, 0, 0 });
            num_vChange0.Name = "num_vChange0";
            num_vChange0.Size = new Size(90, 23);
            num_vChange0.TabIndex = 276;
            num_vChange0.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // label118
            // 
            label118.AutoSize = true;
            label118.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label118.Location = new Point(373, 367);
            label118.Margin = new Padding(4, 0, 4, 0);
            label118.Name = "label118";
            label118.Size = new Size(87, 16);
            label118.TabIndex = 275;
            label118.Text = "0切换速度:";
            // 
            // label98
            // 
            label98.AutoSize = true;
            label98.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label98.Location = new Point(567, 174);
            label98.Margin = new Padding(4, 0, 4, 0);
            label98.Name = "label98";
            label98.Size = new Size(39, 16);
            label98.TabIndex = 274;
            label98.Text = "mm/s";
            // 
            // label96
            // 
            label96.AutoSize = true;
            label96.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label96.Location = new Point(373, 169);
            label96.Margin = new Padding(4, 0, 4, 0);
            label96.Name = "label96";
            label96.Size = new Size(87, 16);
            label96.TabIndex = 273;
            label96.Text = "0打印速度:";
            // 
            // numNormalSpeed0
            // 
            numNormalSpeed0.DecimalPlaces = 1;
            numNormalSpeed0.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            numNormalSpeed0.Location = new Point(470, 167);
            numNormalSpeed0.Margin = new Padding(4);
            numNormalSpeed0.Maximum = new decimal(new int[] { 400, 0, 0, 0 });
            numNormalSpeed0.Name = "numNormalSpeed0";
            numNormalSpeed0.Size = new Size(90, 23);
            numNormalSpeed0.TabIndex = 272;
            numNormalSpeed0.Value = new decimal(new int[] { 30, 0, 0, 0 });
            // 
            // numYlen
            // 
            numYlen.Location = new Point(124, 123);
            numYlen.Margin = new Padding(4);
            numYlen.Name = "numYlen";
            numYlen.Size = new Size(140, 23);
            numYlen.TabIndex = 4;
            numYlen.Value = new decimal(new int[] { 20, 0, 0, 0 });
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("宋体", 12F);
            label3.Location = new Point(58, 128);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(47, 16);
            label3.TabIndex = 5;
            label3.Text = "Y长度";
            // 
            // numInnerLayerHeight
            // 
            numInnerLayerHeight.Location = new Point(124, 162);
            numInnerLayerHeight.Margin = new Padding(4);
            numInnerLayerHeight.Name = "numInnerLayerHeight";
            numInnerLayerHeight.Size = new Size(140, 23);
            numInnerLayerHeight.TabIndex = 4;
            numInnerLayerHeight.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("宋体", 12F);
            label4.Location = new Point(37, 174);
            label4.Margin = new Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new Size(71, 16);
            label4.TabIndex = 5;
            label4.Text = "组内行距";
            // 
            // la
            // 
            la.AutoSize = true;
            la.Font = new Font("宋体", 12F);
            la.Location = new Point(30, 367);
            la.Margin = new Padding(4, 0, 4, 0);
            la.Name = "la";
            la.Size = new Size(71, 16);
            la.TabIndex = 5;
            la.Text = "组间行距";
            // 
            // numOuterLayerHeight
            // 
            numOuterLayerHeight.Location = new Point(117, 364);
            numOuterLayerHeight.Margin = new Padding(4);
            numOuterLayerHeight.Name = "numOuterLayerHeight";
            numOuterLayerHeight.Size = new Size(140, 23);
            numOuterLayerHeight.TabIndex = 4;
            numOuterLayerHeight.Value = new decimal(new int[] { 2, 0, 0, 0 });
            // 
            // chkContinuousExtrusion
            // 
            chkContinuousExtrusion.AutoSize = true;
            chkContinuousExtrusion.Location = new Point(208, 402);
            chkContinuousExtrusion.Margin = new Padding(4);
            chkContinuousExtrusion.Name = "chkContinuousExtrusion";
            chkContinuousExtrusion.Size = new Size(75, 21);
            chkContinuousExtrusion.TabIndex = 288;
            chkContinuousExtrusion.Text = "组间连续";
            chkContinuousExtrusion.UseVisualStyleBackColor = true;
            // 
            // numMaterialLen0
            // 
            numMaterialLen0.Location = new Point(124, 217);
            numMaterialLen0.Margin = new Padding(4);
            numMaterialLen0.Name = "numMaterialLen0";
            numMaterialLen0.Size = new Size(140, 23);
            numMaterialLen0.TabIndex = 289;
            numMaterialLen0.Value = new decimal(new int[] { 5, 0, 0, 0 });
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("宋体", 12F);
            label5.Location = new Point(14, 217);
            label5.Margin = new Padding(4, 0, 4, 0);
            label5.Name = "label5";
            label5.Size = new Size(79, 16);
            label5.TabIndex = 5;
            label5.Text = "0材料长度";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("宋体", 12F);
            label6.Location = new Point(14, 258);
            label6.Margin = new Padding(4, 0, 4, 0);
            label6.Name = "label6";
            label6.Size = new Size(79, 16);
            label6.TabIndex = 5;
            label6.Text = "1材料长度";
            // 
            // numMaterialLen1
            // 
            numMaterialLen1.Location = new Point(124, 255);
            numMaterialLen1.Margin = new Padding(4);
            numMaterialLen1.Name = "numMaterialLen1";
            numMaterialLen1.Size = new Size(140, 23);
            numMaterialLen1.TabIndex = 289;
            numMaterialLen1.Value = new decimal(new int[] { 5, 0, 0, 0 });
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label7.Location = new Point(271, 221);
            label7.Margin = new Padding(4, 0, 4, 0);
            label7.Name = "label7";
            label7.Size = new Size(23, 16);
            label7.TabIndex = 284;
            label7.Text = "mm";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label8.Location = new Point(271, 262);
            label8.Margin = new Padding(4, 0, 4, 0);
            label8.Name = "label8";
            label8.Size = new Size(23, 16);
            label8.TabIndex = 284;
            label8.Text = "mm";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label9.Location = new Point(567, 497);
            label9.Margin = new Padding(4, 0, 4, 0);
            label9.Name = "label9";
            label9.Size = new Size(23, 16);
            label9.TabIndex = 286;
            label9.Text = "mm";
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label10.Location = new Point(271, 170);
            label10.Margin = new Padding(4, 0, 4, 0);
            label10.Name = "label10";
            label10.Size = new Size(23, 16);
            label10.TabIndex = 286;
            label10.Text = "mm";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label11.Location = new Point(271, 129);
            label11.Margin = new Padding(4, 0, 4, 0);
            label11.Name = "label11";
            label11.Size = new Size(23, 16);
            label11.TabIndex = 286;
            label11.Text = "mm";
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label12.Location = new Point(271, 85);
            label12.Margin = new Padding(4, 0, 4, 0);
            label12.Name = "label12";
            label12.Size = new Size(23, 16);
            label12.TabIndex = 286;
            label12.Text = "mm";
            // 
            // btnAddGroup
            // 
            btnAddGroup.Font = new Font("宋体", 12F);
            btnAddGroup.Location = new Point(673, 408);
            btnAddGroup.Margin = new Padding(4);
            btnAddGroup.Name = "btnAddGroup";
            btnAddGroup.Size = new Size(114, 72);
            btnAddGroup.TabIndex = 290;
            btnAddGroup.Text = "添加组";
            btnAddGroup.UseVisualStyleBackColor = true;
            btnAddGroup.Click += btnAddGroup_Click;
            // 
            // listGocdeGroups
            // 
            listGocdeGroups.FormattingEnabled = true;
            listGocdeGroups.ItemHeight = 17;
            listGocdeGroups.Location = new Point(673, 62);
            listGocdeGroups.Margin = new Padding(4);
            listGocdeGroups.Name = "listGocdeGroups";
            listGocdeGroups.Size = new Size(250, 310);
            listGocdeGroups.TabIndex = 291;
            // 
            // btnClearAll
            // 
            btnClearAll.Location = new Point(836, 408);
            btnClearAll.Margin = new Padding(4);
            btnClearAll.Name = "btnClearAll";
            btnClearAll.Size = new Size(110, 72);
            btnClearAll.TabIndex = 292;
            btnClearAll.Text = "清空";
            btnClearAll.UseVisualStyleBackColor = true;
            btnClearAll.Click += btnClearAll_Click;
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label13.Location = new Point(373, 212);
            label13.Margin = new Padding(4, 0, 4, 0);
            label13.Name = "label13";
            label13.Size = new Size(87, 16);
            label13.TabIndex = 273;
            label13.Text = "1打印速度:";
            // 
            // numNormalSpeed1
            // 
            numNormalSpeed1.DecimalPlaces = 1;
            numNormalSpeed1.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            numNormalSpeed1.Location = new Point(470, 210);
            numNormalSpeed1.Margin = new Padding(4);
            numNormalSpeed1.Maximum = new decimal(new int[] { 400, 0, 0, 0 });
            numNormalSpeed1.Name = "numNormalSpeed1";
            numNormalSpeed1.Size = new Size(90, 23);
            numNormalSpeed1.TabIndex = 272;
            numNormalSpeed1.Value = new decimal(new int[] { 30, 0, 0, 0 });
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label14.Location = new Point(567, 212);
            label14.Margin = new Padding(4, 0, 4, 0);
            label14.Name = "label14";
            label14.Size = new Size(39, 16);
            label14.TabIndex = 274;
            label14.Text = "mm/s";
            // 
            // openFileDialog1
            // 
            openFileDialog1.FileName = "openFileDialog1";
            // 
            // btnViewInMain
            // 
            btnViewInMain.Font = new Font("宋体", 12F, FontStyle.Bold, GraphicsUnit.Point, 134);
            btnViewInMain.Location = new Point(34, 622);
            btnViewInMain.Margin = new Padding(4);
            btnViewInMain.Name = "btnViewInMain";
            btnViewInMain.Size = new Size(155, 51);
            btnViewInMain.TabIndex = 293;
            btnViewInMain.Text = "在主窗口查看";
            btnViewInMain.UseVisualStyleBackColor = true;
            btnViewInMain.Click += btnViewInMain_Click;
            // 
            // num_Repeat0
            // 
            num_Repeat0.Location = new Point(470, 55);
            num_Repeat0.Margin = new Padding(4);
            num_Repeat0.Name = "num_Repeat0";
            num_Repeat0.Size = new Size(66, 23);
            num_Repeat0.TabIndex = 4;
            num_Repeat0.Value = new decimal(new int[] { 3, 0, 0, 0 });
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label15.Location = new Point(373, 58);
            label15.Margin = new Padding(4, 0, 4, 0);
            label15.Name = "label15";
            label15.Size = new Size(95, 16);
            label15.TabIndex = 273;
            label15.Text = "0重复次数：";
            // 
            // num_Repeat1
            // 
            num_Repeat1.Location = new Point(470, 95);
            num_Repeat1.Margin = new Padding(4);
            num_Repeat1.Name = "num_Repeat1";
            num_Repeat1.Size = new Size(66, 23);
            num_Repeat1.TabIndex = 4;
            num_Repeat1.Value = new decimal(new int[] { 3, 0, 0, 0 });
            // 
            // label16
            // 
            label16.AutoSize = true;
            label16.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label16.Location = new Point(373, 98);
            label16.Margin = new Padding(4, 0, 4, 0);
            label16.Name = "label16";
            label16.Size = new Size(95, 16);
            label16.TabIndex = 273;
            label16.Text = "1重复次数：";
            // 
            // chkIsRepeat
            // 
            chkIsRepeat.AutoSize = true;
            chkIsRepeat.Checked = true;
            chkIsRepeat.CheckState = CheckState.Checked;
            chkIsRepeat.Location = new Point(558, 102);
            chkIsRepeat.Margin = new Padding(4);
            chkIsRepeat.Name = "chkIsRepeat";
            chkIsRepeat.Size = new Size(75, 21);
            chkIsRepeat.TabIndex = 288;
            chkIsRepeat.Text = "创建重复";
            chkIsRepeat.UseVisualStyleBackColor = true;
            // 
            // chkSwitchManage
            // 
            chkSwitchManage.AutoSize = true;
            chkSwitchManage.Location = new Point(558, 337);
            chkSwitchManage.Margin = new Padding(4);
            chkSwitchManage.Name = "chkSwitchManage";
            chkSwitchManage.Size = new Size(75, 21);
            chkSwitchManage.TabIndex = 288;
            chkSwitchManage.Text = "切换调节";
            chkSwitchManage.UseVisualStyleBackColor = true;
            // 
            // numNormalPressure1
            // 
            numNormalPressure1.DecimalPlaces = 1;
            numNormalPressure1.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            numNormalPressure1.Location = new Point(470, 252);
            numNormalPressure1.Margin = new Padding(4);
            numNormalPressure1.Maximum = new decimal(new int[] { 600, 0, 0, 0 });
            numNormalPressure1.Name = "numNormalPressure1";
            numNormalPressure1.Size = new Size(90, 23);
            numNormalPressure1.TabIndex = 272;
            numNormalPressure1.Value = new decimal(new int[] { 100, 0, 0, 0 });
            // 
            // numNormalPressure2
            // 
            numNormalPressure2.DecimalPlaces = 1;
            numNormalPressure2.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            numNormalPressure2.Location = new Point(470, 295);
            numNormalPressure2.Margin = new Padding(4);
            numNormalPressure2.Maximum = new decimal(new int[] { 600, 0, 0, 0 });
            numNormalPressure2.Name = "numNormalPressure2";
            numNormalPressure2.Size = new Size(90, 23);
            numNormalPressure2.TabIndex = 272;
            numNormalPressure2.Value = new decimal(new int[] { 100, 0, 0, 0 });
            // 
            // label17
            // 
            label17.AutoSize = true;
            label17.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label17.Location = new Point(373, 254);
            label17.Margin = new Padding(4, 0, 4, 0);
            label17.Name = "label17";
            label17.Size = new Size(87, 16);
            label17.TabIndex = 273;
            label17.Text = "0打印气压:";
            // 
            // label18
            // 
            label18.AutoSize = true;
            label18.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label18.Location = new Point(373, 298);
            label18.Margin = new Padding(4, 0, 4, 0);
            label18.Name = "label18";
            label18.Size = new Size(87, 16);
            label18.TabIndex = 273;
            label18.Text = "1打印气压:";
            // 
            // label19
            // 
            label19.AutoSize = true;
            label19.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label19.Location = new Point(567, 259);
            label19.Margin = new Padding(4, 0, 4, 0);
            label19.Name = "label19";
            label19.Size = new Size(31, 16);
            label19.TabIndex = 274;
            label19.Text = "kPa";
            // 
            // label20
            // 
            label20.AutoSize = true;
            label20.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label20.Location = new Point(567, 298);
            label20.Margin = new Padding(4, 0, 4, 0);
            label20.Name = "label20";
            label20.Size = new Size(31, 16);
            label20.TabIndex = 274;
            label20.Text = "kPa";
            // 
            // 大数据G_code
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(969, 687);
            Controls.Add(btnClearAll);
            Controls.Add(listGocdeGroups);
            Controls.Add(btnAddGroup);
            Controls.Add(numMaterialLen1);
            Controls.Add(numMaterialLen0);
            Controls.Add(chkIsRepeat);
            Controls.Add(chkSwitchManage);
            Controls.Add(chkContinuousExtrusion);
            Controls.Add(label9);
            Controls.Add(label12);
            Controls.Add(label11);
            Controls.Add(label10);
            Controls.Add(label125);
            Controls.Add(label124);
            Controls.Add(label8);
            Controls.Add(label7);
            Controls.Add(label123);
            Controls.Add(num_stepChange1);
            Controls.Add(label122);
            Controls.Add(num_vChange1);
            Controls.Add(num_stepChange0);
            Controls.Add(label121);
            Controls.Add(label120);
            Controls.Add(label119);
            Controls.Add(num_vChange0);
            Controls.Add(label118);
            Controls.Add(label20);
            Controls.Add(label14);
            Controls.Add(label19);
            Controls.Add(label98);
            Controls.Add(label18);
            Controls.Add(label13);
            Controls.Add(label16);
            Controls.Add(label17);
            Controls.Add(label15);
            Controls.Add(numNormalPressure2);
            Controls.Add(label96);
            Controls.Add(numNormalPressure1);
            Controls.Add(numNormalSpeed1);
            Controls.Add(numNormalSpeed0);
            Controls.Add(label6);
            Controls.Add(label5);
            Controls.Add(la);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(numOuterLayerHeight);
            Controls.Add(numInnerLayerHeight);
            Controls.Add(num_Repeat1);
            Controls.Add(numYlen);
            Controls.Add(num_Repeat0);
            Controls.Add(numXlen);
            Controls.Add(label1);
            Controls.Add(txtOutputFile);
            Controls.Add(btnGenerateGCode);
            Controls.Add(btnViewInMain);
            Margin = new Padding(4);
            Name = "大数据G_code";
            Text = "大数据G_code";
            ((System.ComponentModel.ISupportInitialize)numXlen).EndInit();
            ((System.ComponentModel.ISupportInitialize)num_stepChange1).EndInit();
            ((System.ComponentModel.ISupportInitialize)num_vChange1).EndInit();
            ((System.ComponentModel.ISupportInitialize)num_stepChange0).EndInit();
            ((System.ComponentModel.ISupportInitialize)num_vChange0).EndInit();
            ((System.ComponentModel.ISupportInitialize)numNormalSpeed0).EndInit();
            ((System.ComponentModel.ISupportInitialize)numYlen).EndInit();
            ((System.ComponentModel.ISupportInitialize)numInnerLayerHeight).EndInit();
            ((System.ComponentModel.ISupportInitialize)numOuterLayerHeight).EndInit();
            ((System.ComponentModel.ISupportInitialize)numMaterialLen0).EndInit();
            ((System.ComponentModel.ISupportInitialize)numMaterialLen1).EndInit();
            ((System.ComponentModel.ISupportInitialize)numNormalSpeed1).EndInit();
            ((System.ComponentModel.ISupportInitialize)num_Repeat0).EndInit();
            ((System.ComponentModel.ISupportInitialize)num_Repeat1).EndInit();
            ((System.ComponentModel.ISupportInitialize)numNormalPressure1).EndInit();
            ((System.ComponentModel.ISupportInitialize)numNormalPressure2).EndInit();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnGenerateGCode;
        private System.Windows.Forms.Button btnViewInMain;
        private System.Windows.Forms.TextBox txtOutputFile;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numXlen;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label125;
        private System.Windows.Forms.Label label124;
        private System.Windows.Forms.Label label123;
        private System.Windows.Forms.NumericUpDown num_stepChange1;
        private System.Windows.Forms.Label label122;
        private System.Windows.Forms.NumericUpDown num_vChange1;
        private System.Windows.Forms.NumericUpDown num_stepChange0;
        private System.Windows.Forms.Label label121;
        private System.Windows.Forms.Label label120;
        private System.Windows.Forms.Label label119;
        private System.Windows.Forms.NumericUpDown num_vChange0;
        private System.Windows.Forms.Label label118;
        private System.Windows.Forms.Label label98;
        private System.Windows.Forms.Label label96;
        private System.Windows.Forms.NumericUpDown numNormalSpeed0;
        private System.Windows.Forms.NumericUpDown numYlen;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown numInnerLayerHeight;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label la;
        private System.Windows.Forms.NumericUpDown numOuterLayerHeight;
        private System.Windows.Forms.CheckBox chkContinuousExtrusion;
        private System.Windows.Forms.NumericUpDown numMaterialLen0;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.NumericUpDown numMaterialLen1;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Button btnAddGroup;
        private System.Windows.Forms.ListBox listGocdeGroups;
        private System.Windows.Forms.Button btnClearAll;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.NumericUpDown numNormalSpeed1;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.NumericUpDown num_Repeat0;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.NumericUpDown num_Repeat1;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.CheckBox chkIsRepeat;
        private System.Windows.Forms.CheckBox chkSwitchManage;
        private System.Windows.Forms.NumericUpDown numNormalPressure1;
        private System.Windows.Forms.NumericUpDown numNormalPressure2;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Label label20;
    }
}