using UnityEngine;

namespace Yunus.Game.Gameplay
{
    public class Triangle : MonoBehaviour
{
    public int x, y;
    public int boxIndex = -1;          // Cell identifier (y*gridWidth + x)
    public float angle;                // 0/90/180/270
    public Facing facing;              // Up, Left, Down, Right
    public int posIndex;               // Up=1, Right=2, Left=3, Down=4
    public bool isSnapped;

    /// <summary>Index of the shape that owns this triangle during generation; -1 = unowned.</summary>
    public int ownerShapeIndex = -1;

    public enum Facing { Up = 1, Left = 3, Down = 4, Right = 2 }
    public Vector2Int gridPos => new Vector2Int(x, y);
    public void Init(int x, int y, float angle, int box = -1)
    {
        this.x = x;
        this.y = y;
        this.boxIndex = box;
        SetAngle(angle);
        isSnapped = false;
        ownerShapeIndex = -1;
    }

    public void SetAngle(float newAngle)
    {
        angle = SnapRightAngle(newAngle);   // 0/90/180/270
        facing = AngleToFacing(angle);       // 0=Up, 1=Left, 2=Down, 3=Right
        posIndex = FacingToPosIndex(facing); // Up=1, Right=2, Left=3, Down=4
    }

    public Vector2Int FacingVec =>
        facing == Facing.Up ? Vector2Int.up :
        facing == Facing.Left ? Vector2Int.left :
        facing == Facing.Down ? Vector2Int.down :
                                 Vector2Int.right;

    // --- mapping helpers ---
    public static int FacingToPosIndex(Facing f)
    {
        // �STENEN s�ra: Up=1, Right=2, Left=3, Down=4
        switch (f)
        {
            case Facing.Up: return 1;
            case Facing.Right: return 2;
            case Facing.Left: return 3;
            case Facing.Down: return 4;
            default: return 0;
        }
    }

    public static Facing PosIndexToFacing(int i)
    {
        switch (i)
        {
            case 1: return Facing.Up;
            case 2: return Facing.Right;
            case 3: return Facing.Left;
            case 4: return Facing.Down;
            default: return Facing.Up;
        }
    }

    public static int OppositePosIndex(int i)
    {
        // Up(1) <-> Down(4), Right(2) <-> Left(3)
        if (i == 1) return 4;
        if (i == 4) return 1;
        if (i == 2) return 3;
        if (i == 3) return 2;
        return i;
    }

    public float SnapRightAngle(float a)
    {
        float snapped = Mathf.Round(a / 90f) * 90f;
        return Mathf.Repeat(snapped, 360f);
    }

    public Facing AngleToFacing(float a)
    {
        int normalized = Mathf.RoundToInt(Mathf.Repeat(a, 360f));

        switch (normalized)
        {
            case 180: return Facing.Up;     // posIndex 1
            case 90: return Facing.Right;  // posIndex 2
            case 270: return Facing.Left;   // posIndex 3
            case 0: return Facing.Down;   // posIndex 4
            default: return Facing.Up;
        }
    }
    public void SnapState(bool spanestate = false)
    {
        isSnapped = spanestate;
        //if(spanestate) gameObject.SetActive(false);

    }
    }
}
