using System.Drawing;

namespace GcodeViewer.Rendering;

/// <summary>3D 向量与基础运算。</summary>
public readonly struct Vec3
{
    public readonly double X, Y, Z;
    public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
}

/// <summary>
/// 轻量 3D→2D 投影：欧拉角旋转(Yaw 绕 Z、Pitch 绕 X) + 透视投影。
/// 用于 gcode 这种纯线框数据，自写零依赖、可复现。
/// </summary>
public sealed class Camera
{
    public double Yaw = -Math.PI / 6;    // 绕 Z 轴
    public double Pitch = -Math.PI / 6;  // 绕 X 轴（俯视向下偏）
    public double Distance = 400;        // 相机距场景中心
    public double Focal = 600;           // 透视焦距
    public PointF Pan = PointF.Empty;    // 屏幕平移（像素）

    private double sinY, cosY, sinP, cosP;

    public void UpdateTrig()
    {
        sinY = Math.Sin(Yaw); cosY = Math.Cos(Yaw);
        sinP = Math.Sin(Pitch); cosP = Math.Cos(Pitch);
    }

    /// <summary>把一个相对场景中心的点旋转并透视投影到归一化屏幕坐标（再由调用方加中心+平移）。</summary>
    public PointF Project(Vec3 p, PointF center)
    {
        // 1) 绕 Z 轴旋转 Yaw
        double x1 = p.X * cosY - p.Y * sinY;
        double y1 = p.X * sinY + p.Y * cosY;
        double z1 = p.Z;
        // 2) 绕 X 轴旋转 Pitch
        double y2 = y1 * cosP - z1 * sinP;
        double z2 = y1 * sinP + z1 * cosP;
        double x2 = x1;
        // 3) 透视（相机在 +Z 方向看向原点）
        double depth = Distance - z2;
        if (depth < 1) depth = 1;
        double scale = Focal / depth;
        return new PointF(
            center.X + (float)(x2 * scale) + Pan.X,
            center.Y - (float)(y2 * scale) + Pan.Y);
    }

    /// <summary>返回投影后的深度，用于排序/决定遮挡（越大越靠前）。</summary>
    public double Depth(Vec3 p)
    {
        double y1 = p.X * sinY + p.Y * cosY;
        double z1 = p.Z;
        double z2 = y1 * sinP + z1 * cosP;
        return Distance - z2;
    }
}

/// <summary>2D 屏幕命中测试辅助。</summary>
public static class HitTest
{
    /// <summary>点 P 到线段 AB 的最短距离（像素）。</summary>
    public static double PointToSegment(PointF p, PointF a, PointF b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float len2 = dx * dx + dy * dy;
        if (len2 < 1e-6) return Dist(p, a);
        float t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
        t = Math.Clamp(t, 0f, 1f);
        return Dist(p, new PointF(a.X + t * dx, a.Y + t * dy));
    }

    public static double Dist(PointF a, PointF b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
