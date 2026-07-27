using GcodeViewer.Models;

namespace GcodeViewer.Forms;

/// <summary>一次编辑动作的快照，供 Ctrl+Z 撤销。坐标修改时各轴存 nullable。</summary>
public sealed class EditAction
{
    public int MoveIndex { get; set; }
    public MoveType OldType { get; set; }
    public MoveType NewType { get; set; }
    public bool TypeChanged { get; set; }

    public double? OldX, NewX;
    public double? OldY, NewY;
    public double? OldZ, NewZ;

    public string Description { get; set; } = string.Empty;
}
