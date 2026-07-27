using GcodeViewer.Models;

namespace GcodeViewer.Parsing;

/// <summary>
/// 栅格往返扫描 G-code 生成器（自 AMCP.PrintSteps.FrmPrintStep2.GenpicGcode 移植）。
/// 以图片左上角为 0,0 点，按行往返扫描游程编码像素(Pixcel)集合，生成栅格填充 G-code。
/// </summary>
public static class RasterRcodeGenerator
{
    /// <summary>
    /// 像素栅格四个角的位置标识，用于判定一层扫描的终点位置与起始角。
    /// </summary>
    public enum VertexPos { LeftBottom, LeftTop, RightTop, RightBottom }

    /// <summary>
    /// 根据游程编码像素集合生成栅格往返扫描 G-code。
    /// isReverse=false 时从左下角正向扫描；isReverse=true 时根据行数奇偶性从左上角或右上角反向扫描。
    /// </summary>
    public static List<string> GenpicGcode(List<List<Pixcel>> Pixcels, double velo, double step, double linespace, double advancedis, bool isVchange, bool isReverse, int width, double vchange0, double vchange1, double dischange0, double dischange1, double pchange0, double pchange1)
    {
        //以图片左上角为0,0点开始，一次完成一个横方向
        List<string> gcodes = new List<string>();
        string str;

        double nowx = 0, nowy = 0;//通过解析像素值得到的x，y值
        double pnowx = 0, pnowy = 0;//经过提前出丝实际打印的x，y值

        //计算一层终点的位置
        VertexPos layerEndPos = VertexPos.LeftBottom;
        double edgeX = 0, edgeY = 0;
        edgeY = (Pixcels.Count - 1) * linespace;
        int countX = 0, countY = 0;
        countY = Pixcels.Count;
        foreach (var pix in Pixcels[0])
        {
            edgeX += pix.Count * step;
            countX += pix.Count;
        }
        //edgeX -= step;
        if (countY % 2 == 0)
        {
            layerEndPos = VertexPos.LeftTop;
        }
        else
        {
            layerEndPos = VertexPos.RightTop;
        }
        if (!isReverse)//正常方向
        {
            //添加抬头
            str = "G1 X0 Y0";
            gcodes.Add(str);
            //添加打印路径,从左下角开始遍历
            for (int i = 0; i < Pixcels.Count; i++)
            {
                //X轴正方向,提前出丝即减去设置的提前出丝距离
                if (i % 2 == 0)
                {
                    for (int j = 0; j < Pixcels[i].Count; j++)
                    {
                        if (isVchange)
                        {
                            if (j == 0)
                            {
                                nowx = nowx + step * Pixcels[i][j].Count;
                                if (j != Pixcels[i].Count - 1)
                                {
                                    pnowx = nowx - advancedis;
                                    if (pnowx < 0) { pnowx = 0; }
                                }
                                else
                                {
                                    pnowx = nowx;
                                }
                                str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                gcodes.Add(str);
                            }
                            else
                            {
                                if (Pixcels[i][j].Depth == 0)//从1到0，为0切换速度，距离
                                {
                                    if (dischange0 < step * Pixcels[i][j].Count)//变速距离比当前像素移动距离短
                                    {
                                        nowx = nowx + dischange0;
                                        pnowx = nowx - advancedis;
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                        gcodes.Add(str);
                                        nowx = nowx + step * Pixcels[i][j].Count - dischange0;
                                        if (j != Pixcels[i].Count - 1)
                                        {
                                            pnowx = nowx - advancedis;
                                            if (pnowx < 0) { pnowx = 0; }
                                        }
                                        else
                                        {
                                            pnowx = nowx;
                                        }
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                        gcodes.Add(str);
                                    }
                                    else//变速距离比当前像素移动距离长，以当前像素距离为主
                                    {
                                        nowx = nowx + step * Pixcels[i][j].Count;
                                        if (j != Pixcels[i].Count - 1)
                                        {
                                            pnowx = nowx - advancedis;
                                            if (pnowx < 0) { pnowx = 0; }
                                        }
                                        else
                                        {
                                            pnowx = nowx;
                                        }
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                        gcodes.Add(str);
                                    }
                                }
                                else//从0到1，为1切换速度，距离
                                {
                                    if (dischange1 < step * Pixcels[i][j].Count)
                                    {
                                        nowx = nowx + dischange1;
                                        pnowx = nowx - advancedis;
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange1.ToString("F1"));
                                        gcodes.Add(str);
                                        nowx = nowx + step * Pixcels[i][j].Count - dischange1;
                                        if (j != Pixcels[i].Count - 1)
                                        {
                                            pnowx = nowx - advancedis;
                                            if (pnowx < 0) { pnowx = 0; }
                                        }
                                        else
                                        {
                                            pnowx = nowx;
                                        }
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                        gcodes.Add(str);
                                    }
                                    else
                                    {
                                        nowx = nowx + step * Pixcels[i][j].Count;
                                        if (j != Pixcels[i].Count - 1)
                                        {
                                            pnowx = nowx - advancedis;
                                            if (pnowx < 0) { pnowx = 0; }
                                        }
                                        else
                                        {
                                            pnowx = nowx;
                                        }
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange1.ToString("F1"));
                                        gcodes.Add(str);
                                    }
                                }
                            }
                        }
                        else
                        {
                            nowx = nowx + step * Pixcels[i][j].Count;
                            if (j != Pixcels[i].Count - 1)
                            {
                                pnowx = nowx - advancedis;
                                if (pnowx < 0) { pnowx = 0; }
                            }
                            else//最后一段不需要提前出丝
                            {
                                pnowx = nowx;
                            }
                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                            gcodes.Add(str);
                        }
                    }
                }
                //X轴负方向,提前出丝即加上设置的提前出丝距离
                else
                {
                    //逆序遍历
                    for (int j = Pixcels[i].Count - 1; j >= 0; j--)
                    {
                        if (isVchange)
                        {
                            if (j == Pixcels[i].Count - 1)
                            {
                                nowx = nowx - step * Pixcels[i][j].Count;
                                if (j != 0)
                                {
                                    pnowx = nowx + advancedis;
                                    if (pnowx > step * width) { pnowx = step * width; }
                                }
                                else
                                {
                                    pnowx = nowx;
                                }
                                str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                gcodes.Add(str);
                            }
                            else
                            {
                                if (Pixcels[i][j].Depth == 0)//从1到0，为0切换速度，距离
                                {
                                    if (dischange0 < step * Pixcels[i][j].Count)
                                    {
                                        nowx = nowx - dischange0;
                                        pnowx = nowx + advancedis;
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                        gcodes.Add(str);
                                        nowx = nowx - step * Pixcels[i][j].Count + dischange0;
                                        if (j != 0)
                                        {
                                            pnowx = nowx + advancedis;
                                            if (pnowx > step * width) { pnowx = step * width; }
                                        }
                                        else
                                        {
                                            pnowx = nowx;
                                        }
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                        gcodes.Add(str);
                                    }
                                    else
                                    {
                                        nowx = nowx - step * Pixcels[i][j].Count;
                                        if (j != 0)
                                        {
                                            pnowx = nowx + advancedis;
                                            if (pnowx > step * width) { pnowx = step * width; }
                                        }
                                        else
                                        {
                                            pnowx = nowx;
                                        }
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                        gcodes.Add(str);
                                    }
                                }
                                else//从0到1，为1切换速度，距离
                                {
                                    if (dischange1 < step * Pixcels[i][j].Count)
                                    {
                                        nowx = nowx - dischange1;
                                        pnowx = nowx + advancedis;
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange1.ToString("F1"));
                                        gcodes.Add(str);
                                        nowx = nowx - step * Pixcels[i][j].Count + dischange1;
                                        if (j != 0)
                                        {
                                            pnowx = nowx + advancedis;
                                            if (pnowx > step * width) { pnowx = step * width; }
                                        }
                                        else
                                        {
                                            pnowx = nowx;
                                        }
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                        gcodes.Add(str);
                                    }
                                    else
                                    {
                                        nowx = nowx - step * Pixcels[i][j].Count;
                                        if (j != 0)
                                        {
                                            pnowx = nowx + advancedis;
                                            if (pnowx > step * width) { pnowx = step * width; }
                                        }
                                        else
                                        {
                                            pnowx = nowx;
                                        }
                                        str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                        gcodes.Add(str);
                                    }
                                }
                            }
                        }
                        else
                        {
                            nowx = nowx - step * Pixcels[i][j].Count;
                            if (j != 0)
                            {
                                pnowx = nowx + advancedis;
                                if (pnowx > step * width) { pnowx = step * width; }
                            }
                            else
                            {
                                pnowx = nowx;
                            }
                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                            gcodes.Add(str);
                        }
                    }
                }
                nowy = linespace * (i + 1);
                if (i < Pixcels.Count - 1)//最后一条不添加下一个起点
                {
                    //移动到打印起点
                    str = string.Format("G1 X{0} Y{1} V{2} T{3}", nowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][Pixcels[i].Count - 1].Depth.ToString());
                    gcodes.Add(str);
                }
            }
        }
        else
        {
            if (layerEndPos == VertexPos.LeftTop)
            {
                //添加抬头
                str = $"G1 X{0} Y{edgeY}";
                gcodes.Add(str);
                nowx = 0; nowy = edgeY;
                //添加打印路径,从左上角开始遍历
                for (int i = Pixcels.Count - 1; i >= 0; i--)
                {
                    //X轴正方向,提前出丝即减去设置的提前出丝距离
                    if (i % 2 == 1)
                    {
                        for (int j = 0; j < Pixcels[i].Count; j++)
                        {
                            if (isVchange)
                            {
                                if (j == 0)
                                {
                                    nowx = nowx + step * Pixcels[i][j].Count;
                                    if (j != Pixcels[i].Count - 1)
                                    {
                                        pnowx = nowx - advancedis;
                                        if (pnowx < 0) { pnowx = 0; }
                                    }
                                    else
                                    {
                                        pnowx = nowx;
                                    }
                                    str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                    gcodes.Add(str);
                                }
                                else
                                {
                                    if (Pixcels[i][j].Depth == 0)//从1到0，为0切换速度，距离
                                    {
                                        if (dischange0 < step * Pixcels[i][j].Count)//变速距离比当前像素移动距离短
                                        {
                                            nowx = nowx + dischange0;
                                            pnowx = nowx - advancedis;
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                            gcodes.Add(str);
                                            nowx = nowx + step * Pixcels[i][j].Count - dischange0;
                                            if (j != Pixcels[i].Count - 1)
                                            {
                                                pnowx = nowx - advancedis;
                                                if (pnowx < 0) { pnowx = 0; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                            gcodes.Add(str);
                                        }
                                        else//变速距离比当前像素移动距离长，以当前像素距离为主
                                        {
                                            nowx = nowx + step * Pixcels[i][j].Count;
                                            if (j != Pixcels[i].Count - 1)
                                            {
                                                pnowx = nowx - advancedis;
                                                if (pnowx < 0) { pnowx = 0; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                            gcodes.Add(str);
                                        }
                                    }
                                    else//从0到1，为1切换速度，距离
                                    {
                                        if (dischange1 < step * Pixcels[i][j].Count)
                                        {
                                            nowx = nowx + dischange1;
                                            pnowx = nowx - advancedis;
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange1.ToString("F1"));
                                            gcodes.Add(str);
                                            nowx = nowx + step * Pixcels[i][j].Count - dischange1;
                                            if (j != Pixcels[i].Count - 1)
                                            {
                                                pnowx = nowx - advancedis;
                                                if (pnowx < 0) { pnowx = 0; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                            gcodes.Add(str);
                                        }
                                        else
                                        {
                                            nowx = nowx + step * Pixcels[i][j].Count;
                                            if (j != Pixcels[i].Count - 1)
                                            {
                                                pnowx = nowx - advancedis;
                                                if (pnowx < 0) { pnowx = 0; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange1.ToString("F1"));
                                            gcodes.Add(str);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                nowx = nowx + step * Pixcels[i][j].Count;
                                if (j != Pixcels[i].Count - 1)
                                {
                                    pnowx = nowx - advancedis;
                                    if (pnowx < 0) { pnowx = 0; }
                                }
                                else//最后一段不需要提前出丝
                                {
                                    pnowx = nowx;
                                }
                                str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                gcodes.Add(str);
                            }
                        }
                    }
                    //X轴负方向,提前出丝即加上设置的提前出丝距离
                    else
                    {
                        //逆序遍历
                        for (int j = Pixcels[i].Count - 1; j >= 0; j--)
                        {
                            if (isVchange)
                            {
                                if (j == Pixcels[i].Count - 1)
                                {
                                    nowx = nowx - step * Pixcels[i][j].Count;
                                    if (j != 0)
                                    {
                                        pnowx = nowx + advancedis;
                                        if (pnowx > step * width) { pnowx = step * width; }
                                    }
                                    else
                                    {
                                        pnowx = nowx;
                                    }
                                    str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                    gcodes.Add(str);
                                }
                                else
                                {
                                    if (Pixcels[i][j].Depth == 0)//从1到0，为0切换速度，距离
                                    {
                                        if (dischange0 < step * Pixcels[i][j].Count)
                                        {
                                            nowx = nowx - dischange0;
                                            pnowx = nowx + advancedis;
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                            gcodes.Add(str);
                                            nowx = nowx - step * Pixcels[i][j].Count + dischange0;
                                            if (j != 0)
                                            {
                                                pnowx = nowx + advancedis;
                                                if (pnowx > step * width) { pnowx = step * width; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                            gcodes.Add(str);
                                        }
                                        else
                                        {
                                            nowx = nowx - step * Pixcels[i][j].Count;
                                            if (j != 0)
                                            {
                                                pnowx = nowx + advancedis;
                                                if (pnowx > step * width) { pnowx = step * width; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                            gcodes.Add(str);
                                        }
                                    }
                                    else//从0到1，为1切换速度，距离
                                    {
                                        if (dischange1 < step * Pixcels[i][j].Count)
                                        {
                                            nowx = nowx - dischange1;
                                            pnowx = nowx + advancedis;
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange1.ToString("F1"));
                                            gcodes.Add(str);
                                            nowx = nowx - step * Pixcels[i][j].Count + dischange1;
                                            if (j != 0)
                                            {
                                                pnowx = nowx + advancedis;
                                                if (pnowx > step * width) { pnowx = step * width; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                            gcodes.Add(str);
                                        }
                                        else
                                        {
                                            nowx = nowx - step * Pixcels[i][j].Count;
                                            if (j != 0)
                                            {
                                                pnowx = nowx + advancedis;
                                                if (pnowx > step * width) { pnowx = step * width; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                            gcodes.Add(str);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                nowx = nowx - step * Pixcels[i][j].Count;
                                if (j != 0)
                                {
                                    pnowx = nowx + advancedis;
                                    if (pnowx > step * width) { pnowx = step * width; }
                                }
                                else
                                {
                                    pnowx = nowx;
                                }
                                str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                gcodes.Add(str);
                            }
                        }
                    }
                    nowy = linespace * (i - 1);
                    if (i > 0)
                    {
                        //移动到打印起点
                        str = string.Format("G1 X{0} Y{1} V{2} T{3}", nowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][Pixcels[i].Count - 1].Depth.ToString());
                        gcodes.Add(str);
                    }
                }
            }
            else if (layerEndPos == VertexPos.RightTop)
            {
                //右上角
                //添加抬头
                str = $"G1 X{edgeX} Y{edgeY}";
                gcodes.Add(str);
                nowx = edgeX; nowy = edgeY;
                //添加打印路径,从右上角开始遍历
                for (int i = Pixcels.Count - 1; i >= 0; i--)
                {
                    //X轴正方向
                    if (i % 2 == 1)
                    {
                        for (int j = 0; j < Pixcels[i].Count; j++)
                        {
                            if (isVchange)
                            {
                                if (j == 0)
                                {
                                    nowx = nowx + step * Pixcels[i][j].Count;
                                    if (j != Pixcels[i].Count - 1)
                                    {
                                        pnowx = nowx - advancedis;
                                        if (pnowx < 0) { pnowx = 0; }
                                    }
                                    else
                                    {
                                        pnowx = nowx;
                                    }
                                    str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                    gcodes.Add(str);
                                }
                                else
                                {
                                    if (Pixcels[i][j].Depth == 0)//从1到0，为0切换速度，距离
                                    {
                                        if (dischange0 < step * Pixcels[i][j].Count)//变速距离比当前像素移动距离短
                                        {
                                            nowx = nowx + dischange0;
                                            pnowx = nowx - advancedis;
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                            gcodes.Add(str);
                                            nowx = nowx + step * Pixcels[i][j].Count - dischange0;
                                            if (j != Pixcels[i].Count - 1)
                                            {
                                                pnowx = nowx - advancedis;
                                                if (pnowx < 0) { pnowx = 0; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                            gcodes.Add(str);
                                        }
                                        else//变速距离比当前像素移动距离长，以当前像素距离为主
                                        {
                                            nowx = nowx + step * Pixcels[i][j].Count;
                                            if (j != Pixcels[i].Count - 1)
                                            {
                                                pnowx = nowx - advancedis;
                                                if (pnowx < 0) { pnowx = 0; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                            gcodes.Add(str);
                                        }
                                    }
                                    else//从0到1，为1切换速度，距离
                                    {
                                        if (dischange1 < step * Pixcels[i][j].Count)
                                        {
                                            nowx = nowx + dischange1;
                                            pnowx = nowx - advancedis;
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange1.ToString("F1"));
                                            gcodes.Add(str);
                                            nowx = nowx + step * Pixcels[i][j].Count - dischange1;
                                            if (j != Pixcels[i].Count - 1)
                                            {
                                                pnowx = nowx - advancedis;
                                                if (pnowx < 0) { pnowx = 0; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                            gcodes.Add(str);
                                        }
                                        else
                                        {
                                            nowx = nowx + step * Pixcels[i][j].Count;
                                            if (j != Pixcels[i].Count - 1)
                                            {
                                                pnowx = nowx - advancedis;
                                                if (pnowx < 0) { pnowx = 0; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange1.ToString("F1"));
                                            gcodes.Add(str);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                nowx = nowx + step * Pixcels[i][j].Count;
                                if (j != Pixcels[i].Count - 1)
                                {
                                    pnowx = nowx - advancedis;
                                    if (pnowx < 0) { pnowx = 0; }
                                }
                                else//最后一段不需要提前出丝
                                {
                                    pnowx = nowx;
                                }
                                str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                gcodes.Add(str);
                            }
                        }
                    }
                    //X轴负方向,提前出丝即加上设置的提前出丝距离
                    else
                    {
                        //逆序遍历
                        for (int j = Pixcels[i].Count - 1; j >= 0; j--)
                        {
                            if (isVchange)
                            {
                                if (j == Pixcels[i].Count - 1)
                                {
                                    nowx = nowx - step * Pixcels[i][j].Count;
                                    if (j != 0)
                                    {
                                        pnowx = nowx + advancedis;
                                        if (pnowx > step * width) { pnowx = step * width; }
                                    }
                                    else
                                    {
                                        pnowx = nowx;
                                    }
                                    str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                    gcodes.Add(str);
                                }
                                else
                                {
                                    if (Pixcels[i][j].Depth == 0)//从1到0，为0切换速度，距离
                                    {
                                        if (dischange0 < step * Pixcels[i][j].Count)
                                        {
                                            nowx = nowx - dischange0;
                                            pnowx = nowx + advancedis;
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                            gcodes.Add(str);
                                            nowx = nowx - step * Pixcels[i][j].Count + dischange0;
                                            if (j != 0)
                                            {
                                                pnowx = nowx + advancedis;
                                                if (pnowx > step * width) { pnowx = step * width; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                            gcodes.Add(str);
                                        }
                                        else
                                        {
                                            nowx = nowx - step * Pixcels[i][j].Count;
                                            if (j != 0)
                                            {
                                                pnowx = nowx + advancedis;
                                                if (pnowx > step * width) { pnowx = step * width; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange0.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange0.ToString("F1"));
                                            gcodes.Add(str);
                                        }
                                    }
                                    else//从0到1，为1切换速度，距离
                                    {
                                        if (dischange1 < step * Pixcels[i][j].Count)
                                        {
                                            nowx = nowx - dischange1;
                                            pnowx = nowx + advancedis;
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3} P{4}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString(), pchange1.ToString("F1"));
                                            gcodes.Add(str);
                                            nowx = nowx - step * Pixcels[i][j].Count + dischange1;
                                            if (j != 0)
                                            {
                                                pnowx = nowx + advancedis;
                                                if (pnowx > step * width) { pnowx = step * width; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                            gcodes.Add(str);
                                        }
                                        else
                                        {
                                            nowx = nowx - step * Pixcels[i][j].Count;
                                            if (j != 0)
                                            {
                                                pnowx = nowx + advancedis;
                                                if (pnowx > step * width) { pnowx = step * width; }
                                            }
                                            else
                                            {
                                                pnowx = nowx;
                                            }
                                            str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), vchange1.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                            gcodes.Add(str);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                nowx = nowx - step * Pixcels[i][j].Count;
                                if (j != 0)
                                {
                                    pnowx = nowx + advancedis;
                                    if (pnowx > step * width) { pnowx = step * width; }
                                }
                                else
                                {
                                    pnowx = nowx;
                                }
                                str = string.Format("G1 X{0} Y{1} V{2} T{3}", pnowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][j].Depth.ToString());
                                gcodes.Add(str);
                            }
                        }
                    }
                    nowy = linespace * (i - 1);
                    if (i > 0)
                    {
                        //移动到打印起点
                        str = string.Format("G1 X{0} Y{1} V{2} T{3}", nowx.ToString("F3"), nowy.ToString("F3"), velo.ToString("F3"), Pixcels[i][Pixcels[i].Count - 1].Depth.ToString());
                        gcodes.Add(str);
                    }
                }
            }
            else
            {
                //计算有误
            }
        }

        return gcodes;
    }
}
