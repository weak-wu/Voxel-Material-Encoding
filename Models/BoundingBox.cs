namespace GcodeViewer.Models;

/// <summary>所有路径坐标的包围盒，用于 3D 视图归一化与居中。</summary>
public sealed class BoundingBox
{
    public double MinX = double.MaxValue, MaxX = double.MinValue;
    public double MinY = double.MaxValue, MaxY = double.MinValue;
    public double MinZ = double.MaxValue, MaxZ = double.MinValue;
    public bool IsValid;

    public void Include(double x, double y, double z)
    {
        if (x < MinX) MinX = x;
        if (x > MaxX) MaxX = x;
        if (y < MinY) MinY = y;
        if (y > MaxY) MaxY = y;
        if (z < MinZ) MinZ = z;
        if (z > MaxZ) MaxZ = z;
        IsValid = true;
    }

    public double SizeX => MaxX - MinX;
    public double SizeY => MaxY - MinY;
    public double SizeZ => MaxZ - MinZ;
    public double CenterX => (MinX + MaxX) * 0.5;
    public double CenterY => (MinY + MaxY) * 0.5;
    public double CenterZ => (MinZ + MaxZ) * 0.5;
    public double MaxExtent => Math.Max(SizeX, Math.Max(SizeY, SizeZ));
}
