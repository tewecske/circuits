namespace Circuits.Core;

/// <summary>A cell on the board. Y grows downwards, as on screen.</summary>
public readonly record struct GridPos(int X, int Y)
{
    public static GridPos operator +(GridPos a, GridPos b) => new(a.X + b.X, a.Y + b.Y);

    public override string ToString() => $"({X}, {Y})";
}

/// <summary>The four grid directions, in clockwise order.</summary>
public enum Direction : byte
{
    East,
    South,
    West,
    North,
}

/// <summary>Number of clockwise quarter turns.</summary>
public enum Rotation : byte
{
    R0,
    R90,
    R180,
    R270,
}

public static class DirectionExtensions
{
    public static GridPos Offset(this Direction d) => d switch
    {
        Direction.East => new GridPos(1, 0),
        Direction.South => new GridPos(0, 1),
        Direction.West => new GridPos(-1, 0),
        _ => new GridPos(0, -1),
    };

    public static Direction Opposite(this Direction d) => (Direction)(((int)d + 2) & 3);

    public static Direction Rotate(this Direction d, Rotation r) => (Direction)(((int)d + (int)r) & 3);

    public static bool IsHorizontal(this Direction d) => d is Direction.East or Direction.West;
}

public static class RotationExtensions
{
    public static Rotation Next(this Rotation r) => (Rotation)(((int)r + 1) & 3);
}
