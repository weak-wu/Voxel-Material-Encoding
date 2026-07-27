namespace GcodeViewer.Models;

/// <summary>
/// 游程编码(RLE)像素/体素单元（自 AMCP.FrmPrintStep2.pixcel 移植）。
/// depth：段值（图片二值 0=白/1=黑；体素 0/1 表示两种材料）。
/// count：该值连续出现的次数。
/// </summary>
public struct Pixcel
{
    public int Depth;
    public int Count;

    public Pixcel(int depth, int count)
    {
        Depth = depth;
        Count = count;
    }
}

/// <summary>
/// 体素映射框架（自 AMCP.FrmPrintStep2.VoxelMapFrame 移植）。
/// 描述体素网格在路径坐标系下的覆盖范围与单元尺寸（XY 平面 + Z 层），
/// 用于把路径点映射到体素单元/层取深度(材料)值。
/// XY 与 Z 均为"尺寸等效"：体素栅格各轴独立拉伸到路径点最小外接矩形，
/// 使路径点坐标可直接落入对应单元/层（Z 方向用 <c>LayerCount</c> 层拉伸到路径 Z 外接矩形）。
/// </summary>
public struct VoxelMapFrame
{
    public double XMin, XMax, YMin, YMax;
    public double CellSizeX;   // X 方向体素间距
    public double CellSizeY;   // Y 方向体素间距
    public int ColCount;       // X 方向体素列数
    public int RowCount;       // Y 方向体素行数
    public double ZMin, ZMax;  // Z 方向(体素层)覆盖范围(路径点 Z 外接矩形)
    public double CellSizeZ;   // Z 方向体素层间距
    public int LayerCount;     // Z 方向体素层数
}
