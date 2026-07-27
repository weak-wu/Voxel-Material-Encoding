using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using GcodeViewer.Parsing;

namespace GcodeViewer.Forms;

    public partial class 大数据G_code : Form
    {
        // 主窗引用：用于把生成的 gcode 送回主窗口 3D 预览（与 PathGeneratorForm 同款集成模式）
        private readonly MainForm _owner;

        // 抓取 gcode 行里的 X/Y/Z/T/P 数值（V 与 E/F 忽略）。
        // 镜像 GcodeParser.AxisRegex，额外扩展出 T、P，供 CSV 导出提取气压/工具号。
        private static readonly Regex AxisValueRegex =
            new(@"(?i)(?<![A-Za-z])([XYZTP])(\s*[-+]?\d*\.?\d+)", RegexOptions.Compiled);

        public 大数据G_code(MainForm owner)
        {
            _owner = owner;
            InitializeComponent();
        }

        //存贮单组的gcode与所有gcode

        List<string> listGcodeSingle = new List<string>();
        List<List<string>> listGcodeAll = new List<List<string>>();

        public class GCodeParamGroup
        {
            /// 打印区域的X方向长度（单位：毫米）。
            public double xlen;

            // 打印区域的Y方向长度（单位：毫米）。
            public double ylen;

            // 内层路径的层高（单位：毫米）。
            public double innerLayerHeight;

            //外层路径的层高（单位：毫米）。
            public double outerLayerHeight;

            // 正常打印时的移动速度（单位：毫米/秒）。
            public double normalSpeed0;
            public double normalSpeed1;

            //材料的打印气压
            public double normalPressure0;
            public double normalPressure1;

            //材料A切换时的速度（单位：毫米/秒）。
            public double switchSpeed0;

            // 材料B切换时的速度（单位：毫米/秒）。
            public double switchSpeed1;

            // 材料A切换时的提前切换距离（单位：毫米）。
            public double switchStep0;

            //材料B切换时的提前切换距离（单位：毫米）。
            public double switchStep1;

            // 是否启用连续出料模式。
            public bool ContinuousExtrusion;

            // 材料A的出料长度（单位：毫米）。
            public double materialLen0;

            // 材料B的出料长度（单位：毫米）。
            public double materialLen1;

            public string groupName = string.Empty; // 可选：用于显示

            //材料01重复次数
            public int repeatCount0;
            public int repeatCount1;
            public bool isRepeat;
        }

        GCodeParamGroup gCodeParamGroup = new GCodeParamGroup();

        /// <summary>把所有组的 gcode 行合并为一个列表（组间插入空行，与原生成逻辑一致）。</summary>
        private List<string> BuildAllGcodeLines()
        {
            var all = new List<string>();
            foreach (var group in listGcodeAll)
            {
                all.AddRange(group);
                all.Add(""); // 组间空行
            }
            return all;
        }

        private void btnGenerateGCode_Click(object sender, EventArgs e)
        {
            try
            {
                // 合并所有组的 GCode（组间空行分隔）
                var allGcodeLines = BuildAllGcodeLines();

                // 文件名优先用用户输入，否则用默认
                string fileName = txtOutputFile.Text.Trim();

                saveFileDialog1.Filter = "GCODE files (*.gcode)|*.gcode";
                saveFileDialog1.DefaultExt = "gcode";
                saveFileDialog1.FileName = fileName + "_" + DateTime.Now.ToString("y.M.d hhmm");
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    fileName = saveFileDialog1.FileName;
                    // 直接覆盖写入（File.WriteAllLines 默认覆盖，等价于原 GV.WriteLineTextFile）
                    File.WriteAllLines(fileName, allGcodeLines, Encoding.UTF8);
                    if (DialogResult.OK == MessageBox.Show("数据已保存到：" + fileName + "\n\n点击确定打开文件。点击取消关闭此对话。", "提示", MessageBoxButtons.OKCancel))
                    {
                        try
                        {
                            // .NET 8 下 Process.Start(string) 默认 UseShellExecute=false 会抛 Win32Exception，
                            // 必须显式 UseShellExecute=true 才能用系统默认程序打开文件。
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fileName)
                            {
                                UseShellExecute = true
                            });
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("打开失败！");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败: " + ex.Message);
            }
        }

        // 在主窗口 3D 预览：把生成的 gcode 文本送回主窗解析并显示（不关闭本窗，便于继续调参）
        private void btnViewInMain_Click(object sender, EventArgs e)
        {
            try
            {
                if (listGcodeAll.Count == 0)
                {
                    MessageBox.Show("当前没有可预览的路径，请先添加组。", "提示");
                    return;
                }
                var allGcodeLines = BuildAllGcodeLines();
                string text = string.Join(Environment.NewLine, allGcodeLines);

                string name = string.IsNullOrWhiteSpace(txtOutputFile.Text)
                    ? "大数据_" + DateTime.Now.ToString("MMdd_HHmm")
                    : txtOutputFile.Text.Trim();

                // GcodeParser.ParseText 能解析本窗体生成的 "G1 X.. Y.. V.. T.. P.." 行：
                // AxisRegex 只抓 X/Y/Z/E/F（V、P 自动忽略），InlineTRegex 抓内联 T0/T1。
                var parsed = GcodeParser.ParseText(text, name);
                _owner.LoadExternal(parsed, name);
                _owner.BringToFront();
            }
            catch (Exception ex)
            {
                MessageBox.Show("预览失败: " + ex.Message);
            }
        }

        //生成单组的gcode
        private List<string> GenerateDualMaterialGCode(double xStart, double yStart, double xLen, double yLen,
    double normalSpeedA, double normalSpeedB,
    double switchSpeedA, double switchSpeedB,
    double switchStepA, double switchStepB,
    double innerLayerHeight,
    double materialLenA, double materialLenB, bool forward
            , double pressA, double pressB)
        {
            var gcodeLines = new List<string>();
            // 生成GCode
            try
            {
                int lines = (int)Math.Ceiling(yLen / innerLayerHeight);
                double xLeft = xStart;
                double xRight = xStart + xLen;
                double y = yStart;
                bool isT0 = true; // true: T0, false: T1
                double totalDistance = 0.0;
                gcodeLines.Add("; Zigzag T0-T1 switching G-code");
                gcodeLines.Add($"; XLen: {xLen} YLen: {yLen} 行数: {lines}");
                gcodeLines.Add($"; NormalSpeedA: {normalSpeedA} NormalSpeedB: {normalSpeedB} SwitchSpeedA: {switchSpeedA} SwitchSpeedB: {switchSpeedB}");
                gcodeLines.Add($"; SwitchStepA: {switchStepA} SwitchStepB: {switchStepB} MaterialLenA: {materialLenA} MaterialLenB: {materialLenB}");

                //gcodeLines.Add($"G1 X{xStart:F3} Y{yStart:F3}");
                /* gcodeLines.Add("G0 Z0")*/
                ;

                // 初始移动到第0行的起点（respect forward）
                double firstRowStartX = forward ? xStart : xStart + xLen;
                gcodeLines.Add($"G1 X{firstRowStartX:F3} Y{yStart:F3}");

                //循环每一条线条
                for (int i = 0; i < lines; i++)
                {         
                    // 当前行的方向：第0行使用传入的 forward，后续行交替
                    bool rowForward = (i % 2 == 0) ? forward : !forward;

                    double x0 = rowForward ? xStart : xStart + xLen;
                    double x1 = rowForward ? xStart + xLen : xStart;
                    int dir = rowForward ? 1 : -1;

                    double curX = x0;
                    int t = isT0 ? 0 : 1;//初始材料
                    double curMaterialLen = isT0 ? materialLenA : materialLenB;//材料长度
                    double curNormalSpeed = isT0 ? normalSpeedA : normalSpeedB;//正常打印速度
                    double curNormalPress = isT0 ? pressA : pressB;//正常打印的气压
                    double curSwitchSpeed = isT0 ? switchSpeedA : switchSpeedB;//切换速度
                    double curSwitchStep = isT0 ? switchStepA : switchStepB;//切换使用距离
                    double remain = Math.Abs(x1 - curX);//剩余距离
                    while (remain > 1e-6)
                    {
                        // 1. 当前材料打印一段长度
                        double segLen = curMaterialLen;
                        if (segLen > remain) segLen = remain;
                        double nextX = curX + dir * segLen;
                        if ((dir == 1 && nextX > x1) || (dir == -1 && nextX < x1))
                            nextX = x1;

                        gcodeLines.Add($"G1 X{nextX:F3} Y{y:F3} V{curNormalSpeed:F3} T{t} P{curNormalPress}");
                        totalDistance += Math.Abs(nextX - curX);

                        curX = nextX;
                        remain = Math.Abs(x1 - curX);

                        // 到达端点不再切换
                        if (Math.Abs(curX - x1) < 1e-6)
                            break;

                        bool switchMange = false;
                        switchMange = chkSwitchManage.Checked;
                        // 2. 切换段
                        double switchStep = curSwitchStep;
                        double switchSpeed = curSwitchSpeed;
                        double switchX = curX + dir * switchStep;
                        // 不越界
                        if ((dir == 1 && switchX > x1) || (dir == -1 && switchX < x1))
                            switchX = x1;
                        // 切换材料
                        t = 1 - t;
                        curNormalSpeed = t == 0 ? normalSpeedA : normalSpeedB;
                        curNormalPress = t == 0 ? pressA : pressB;
                        curSwitchSpeed = t == 0 ? switchSpeedA : switchSpeedB;
                        curSwitchStep = t == 0 ? switchStepA : switchStepB;
                        curMaterialLen = t == 0 ? materialLenA : materialLenB;

                        //切换时的参数调整
                        if (switchMange)
                        {                      
                            //0-1时，气压切换为1的气压，但材料依旧是0材料
                            gcodeLines.Add($"G1 X{switchX:F3} Y{y:F3} V{switchSpeed:F3} T{t} P{curNormalPress}");

                            totalDistance += Math.Abs(switchX - curX);
                            curX = switchX;
                            remain = Math.Abs(x1 - curX);                       
                        }  
                        else
                        {
                            switchX = curX;
                            totalDistance += Math.Abs(switchX - curX);
                            curX = switchX;
                            remain = Math.Abs(x1 - curX);
                        }         
                    }

                    // 到达端点，换向
                    double nextY = y + innerLayerHeight;
                    if (i < lines - 1)
                        gcodeLines.Add($"G1 X{curX:F3} Y{nextY:F3} V{curNormalSpeed:F3} T{t}");

                    y = nextY;
                    isT0 = !isT0;
                }

                return gcodeLines;
            }
            catch (Exception e)
            {
                gcodeLines.Add($"; Error generating G-code: {e.Message}");
                return gcodeLines;
            }
        }

        private double lastEndX = 0;
        private double lastEndY = 0;
        private bool hasLastGroup = false;
        //添加有一组参数
        private void btnAddGroup_Click(object sender, EventArgs e)
        {
            try
            {
                //获取参数
                gCodeParamGroup.xlen = (double)numXlen.Value;
                gCodeParamGroup.ylen = (double)numYlen.Value;
                gCodeParamGroup.innerLayerHeight = (double)numInnerLayerHeight.Value;
                //gCodeParamGroup.outerLayerHeight = (double)numOuterLayerHeight.Value;

                gCodeParamGroup.normalSpeed0 = (double)numNormalSpeed0.Value;
                gCodeParamGroup.normalSpeed1 = (double)numNormalSpeed1.Value;
                gCodeParamGroup.normalPressure0 = (double)numNormalPressure1.Value;
                gCodeParamGroup.normalPressure1 = (double)numNormalPressure2.Value;

                gCodeParamGroup.switchSpeed0 = (double)num_vChange0.Value;
                gCodeParamGroup.switchSpeed1 = (double)num_vChange1.Value;
                gCodeParamGroup.switchStep0 = (double)num_stepChange0.Value;
                gCodeParamGroup.switchStep1 = (double)num_stepChange1.Value;
                gCodeParamGroup.ContinuousExtrusion = chkContinuousExtrusion.Checked;
                gCodeParamGroup.materialLen0 = (double)numMaterialLen0.Value;
                gCodeParamGroup.materialLen1 = (double)numMaterialLen1.Value;

                //新增切换前先重复挤出
                gCodeParamGroup.repeatCount0 = (int)num_Repeat0.Value;
                gCodeParamGroup.repeatCount1 = (int)num_Repeat1.Value;
                gCodeParamGroup.isRepeat = chkIsRepeat.Checked;

                // 计算新组起点
                List<string> preLines = new List<string>();
                double xStart = 0;
                double yStart = 0;
                bool forward = true;

                // 生成GCode
                //移动到起点
                if (hasLastGroup)
                {
                    xStart = lastEndX;
                    yStart = lastEndY + (double)numOuterLayerHeight.Value;
                    preLines.Add(gCodeParamGroup.ContinuousExtrusion
        ? $"G1 X{xStart:F3} Y{yStart:F3}"
        : $"G0 X{xStart:F3} Y{yStart:F3}");//是否连续出丝到下一组
                }

                //若用户选择了重复挤出，则在切换前先挤出指定长度的材料

                double leftX = xStart;
                double rightX = xStart + gCodeParamGroup.xlen;
                //创建重复
                if (gCodeParamGroup.isRepeat)
                {

                    double lastX = xStart;
                    //起点
                    preLines.Add($"G1 X{xStart:F3} Y{yStart:F3} V{gCodeParamGroup.normalSpeed0:F3} T0");
                    for (int i = 0; i < gCodeParamGroup.repeatCount0; i++)
                    {
                        double endX = forward ? rightX : leftX;
                        preLines.Add($"G1 X{endX:F3} Y{yStart:F3} V{gCodeParamGroup.normalSpeed0:F3} T0");//横向
                        lastX = endX;
                        yStart += gCodeParamGroup.innerLayerHeight;
                        preLines.Add($"G1 X{endX:F3} Y{yStart:F3} V{gCodeParamGroup.normalSpeed0:F3} T0");//向下
                        forward = !forward;
                    }
                    // T1 重复段：使用 normalSpeed1 和 T1
                    //forward = true;
                    for (int i = 0; i < gCodeParamGroup.repeatCount1; i++)
                    {
                        double endX = forward ? rightX : leftX;
                        preLines.Add($"G1 X{endX:F3} Y{yStart:F3} V{gCodeParamGroup.normalSpeed1:F3} T1");
                        lastX = endX;
                        yStart += gCodeParamGroup.innerLayerHeight;
                        preLines.Add($"G1 X{endX:F3} Y{yStart:F3} V{gCodeParamGroup.normalSpeed0:F3} T1");//向下
                        forward = !forward;
                    }
                }


                //生成当前组的GCode，重复后，起始的方向会改变
                List<string> generated = (GenerateDualMaterialGCode(
            xStart, yStart,
            gCodeParamGroup.xlen, gCodeParamGroup.ylen,
            gCodeParamGroup.normalSpeed0, gCodeParamGroup.normalSpeed1,
            gCodeParamGroup.switchSpeed0, gCodeParamGroup.switchSpeed1,
            gCodeParamGroup.switchStep0, gCodeParamGroup.switchStep1,
            gCodeParamGroup.innerLayerHeight,
            gCodeParamGroup.materialLen0, gCodeParamGroup.materialLen1, forward
            ,gCodeParamGroup.normalPressure0, gCodeParamGroup.normalPressure1));

                preLines.AddRange(generated);
                listGcodeSingle = preLines;

                // 记录本组终点
                lastEndX = xStart;
                lastEndY = yStart + gCodeParamGroup.ylen;
                hasLastGroup = true;

                // 显示组信息
                string groupInfo = $"组{listGocdeGroups.Items.Count + 1}: X: {gCodeParamGroup.xlen} ,Y: {gCodeParamGroup.ylen},{lastEndY:F1})  行距:{gCodeParamGroup.innerLayerHeight}";
                listGocdeGroups.Items.Add(groupInfo);

                // 自适应行宽
                using (Graphics g = listGocdeGroups.CreateGraphics())
                {
                    int maxWidth = 0;
                    foreach (var item in listGocdeGroups.Items)
                    {
                        int itemWidth = (int)g.MeasureString(item.ToString(), listGocdeGroups.Font).Width;
                        if (itemWidth > maxWidth)
                            maxWidth = itemWidth;
                    }
                    listGocdeGroups.HorizontalScrollbar = true;
                    listGocdeGroups.ScrollAlwaysVisible = true;
                    listGocdeGroups.Width = Math.Max(listGocdeGroups.Width, maxWidth + 30);
                }

                // 保存GCode
                listGcodeAll.Add(new List<string>(listGcodeSingle));
                listGcodeSingle.Clear();
                preLines.Clear();

                MessageBox.Show("添加成功: " + groupInfo);

            }
            catch (Exception ex)
            {
                MessageBox.Show("添加失败: " + ex.Message);
            }
        }

        private void btnClearAll_Click(object sender, EventArgs e)
        {
            listGcodeAll.Clear();
            listGcodeSingle.Clear();
            listGocdeGroups.Items.Clear();
            txtOutputFile.Text = string.Empty;
            // 同步重置组间接力游标，避免清空后再添加组时从旧终点继续
            lastEndX = 0;
            lastEndY = 0;
            hasLastGroup = false;
        }

        // 导出与 CsvPathReader 兼容的 6 列路径 CSV：X,Y,Z,G0/G1,T0/T1,P(气压)
        // （主窗「打开」可直接选此 CSV 预览，实现 gcode↔csv 往返一致）
        private void btnGenerateCsv_Click(object sender, EventArgs e)
        {
            try
            {
                if (listGcodeAll.Count == 0)
                {
                    MessageBox.Show("当前没有可导出的路径，请先添加组。", "提示");
                    return;
                }
                var allGcodeLines = BuildAllGcodeLines();

                var csvLines = new List<string>();
                double curX = 0, curY = 0, curZ = 0;   // 本窗体生成的 gcode 从不写 Z，恒为 0
                int curT = 0;                          // 默认工具 T0（与 GcodeParser 一致）
                double curP = 0;                       // 气压缺失时按 0，后续行延续

                // 行首 G 指令：G0 → 空行程；G1/其它 → 打印
                var gWordRe = new Regex(@"(?i)^G(\d)", RegexOptions.Compiled);

                foreach (string raw in allGcodeLines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";")) continue; // 空行/注释跳过

                    var gw = gWordRe.Match(line);
                    if (!gw.Success) continue;                                // 非 G 指令行跳过
                    string type = gw.Groups[1].Value == "0" ? "G0" : "G1";

                    // 抓本行出现的各 token；缺失的轴/工具/气压沿用上一行（延续语义）
                    var vals = new Dictionary<char, double>(5);
                    foreach (Match m in AxisValueRegex.Matches(line))
                    {
                        char axis = char.ToUpperInvariant(m.Groups[1].Value[0]);
                        if (double.TryParse(m.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
                            vals[axis] = v;
                    }
                    if (vals.ContainsKey('X')) curX = vals['X'];
                    if (vals.ContainsKey('Y')) curY = vals['Y'];
                    if (vals.ContainsKey('Z')) curZ = vals['Z'];
                    if (vals.ContainsKey('T')) curT = (int)vals['T'];
                    if (vals.ContainsKey('P')) curP = vals['P'];

                    csvLines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0:F3},{1:F3},{2:F3},{3},{4},{5:F0}",
                        curX, curY, curZ, type, "T" + curT, curP));
                }

                // 选保存路径（复用 Designer 已声明的 saveFileDialog1）
                saveFileDialog1.Filter = "CSV files (*.csv)|*.csv";
                saveFileDialog1.DefaultExt = "csv";
                string baseName = string.IsNullOrWhiteSpace(txtOutputFile.Text) ? "Gcode_Groups" : txtOutputFile.Text.Trim();
                saveFileDialog1.FileName = baseName + "_" + DateTime.Now.ToString("MMdd_HHmm");
                if (saveFileDialog1.ShowDialog() != DialogResult.OK) return;

                File.WriteAllLines(saveFileDialog1.FileName, csvLines, Encoding.UTF8);
                MessageBox.Show("CSV 已保存：" + saveFileDialog1.FileName +
                                "\n共 " + csvLines.Count + " 行（可用主窗「打开」选此 CSV 直接预览）",
                                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败: " + ex.Message);
            }
        }
    }

