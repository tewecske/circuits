namespace Circuits.Core;

public enum ComponentKind : byte
{
    Switch,
    Input,
    Output,
    Nand,
}

public enum PinKind : byte
{
    In,
    Out,
}

/// <summary>A pin in the unrotated footprint. <see cref="Side"/> is the edge it connects through.</summary>
public readonly record struct PinDef(GridPos Local, Direction Side, PinKind Kind);

/// <summary>A pin of a placed component, in board coordinates.</summary>
public readonly record struct PlacedPin(int Index, GridPos Cell, Direction Side, PinKind Kind)
{
    /// <summary>The cell just outside the pin. A wire there, or a pin facing back, joins this pin's net.</summary>
    public GridPos ConnectionCell => Cell + Side.Offset();
}

/// <summary>Static shape of a component kind: footprint size and pins.</summary>
public sealed class ComponentDef
{
    ComponentDef(ComponentKind kind, string name, int width, int height, params PinDef[] pins)
    {
        Kind = kind;
        Name = name;
        Width = width;
        Height = height;
        Pins = pins;
    }

    public static ComponentDef Switch { get; } = new(ComponentKind.Switch, "Switch", 1, 1,
        new PinDef(new GridPos(0, 0), Direction.East, PinKind.Out));

    public static ComponentDef Input { get; } = new(ComponentKind.Input, "Input", 1, 1,
        new PinDef(new GridPos(0, 0), Direction.East, PinKind.Out));

    public static ComponentDef Output { get; } = new(ComponentKind.Output, "Output", 1, 1,
        new PinDef(new GridPos(0, 0), Direction.West, PinKind.In));

    public static ComponentDef Nand { get; } = new(ComponentKind.Nand, "NAND", 3, 3,
        new PinDef(new GridPos(0, 0), Direction.West, PinKind.In),
        new PinDef(new GridPos(0, 2), Direction.West, PinKind.In),
        new PinDef(new GridPos(2, 1), Direction.East, PinKind.Out));

    /// <summary>All definitions, indexed by <see cref="ComponentKind"/>.</summary>
    public static IReadOnlyList<ComponentDef> All { get; } = [Switch, Input, Output, Nand];

    public static ComponentDef Get(ComponentKind kind) => All[(int)kind];

    public ComponentKind Kind { get; }
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<PinDef> Pins { get; }

    public (int Width, int Height) SizeFor(Rotation r) => ((int)r & 1) == 0 ? (Width, Height) : (Height, Width);

    /// <summary>Maps a cell of the unrotated footprint into the footprint turned by <paramref name="r"/>.</summary>
    public GridPos RotateLocal(GridPos local, Rotation r)
    {
        int x = local.X, y = local.Y, h = Height, w = Width;
        for (int i = 0; i < (int)r; i++)
        {
            (x, y) = (h - 1 - y, x);
            (w, h) = (h, w);
        }
        return new GridPos(x, y);
    }

    public IEnumerable<GridPos> Footprint(GridPos origin, Rotation r)
    {
        var (w, h) = SizeFor(r);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                yield return new GridPos(origin.X + x, origin.Y + y);
    }
}

/// <summary>A component placed on a <see cref="Board"/>.</summary>
public sealed class Component
{
    internal Component(int id, ComponentKind kind, GridPos origin, Rotation rotation, string? label)
    {
        Id = id;
        Kind = kind;
        Origin = origin;
        Rotation = rotation;
        Label = label;
    }

    public int Id { get; }
    public ComponentKind Kind { get; }
    public ComponentDef Def => ComponentDef.Get(Kind);

    /// <summary>Top-left cell of the rotated footprint.</summary>
    public GridPos Origin { get; }

    public Rotation Rotation { get; }
    public string? Label { get; }

    /// <summary>Position of a switch or input. Always false for other kinds.</summary>
    public bool IsOn { get; internal set; }

    public bool IsDriver => Kind is ComponentKind.Switch or ComponentKind.Input;
    public int PinCount => Def.Pins.Count;

    public IEnumerable<GridPos> Cells() => Def.Footprint(Origin, Rotation);

    public PlacedPin GetPin(int index)
    {
        var pin = Def.Pins[index];
        return new PlacedPin(index, Origin + Def.RotateLocal(pin.Local, Rotation), pin.Side.Rotate(Rotation), pin.Kind);
    }
}
