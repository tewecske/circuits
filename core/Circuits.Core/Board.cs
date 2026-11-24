namespace Circuits.Core;

public enum CellType : byte
{
    Empty,
    Wire,

    /// <summary>Passes a horizontal and a vertical line through each other without joining them.</summary>
    Crossing,
}

/// <summary>A square block of wire cells. Chunks are created on first paint and dropped when empty.</summary>
public sealed class Chunk
{
    internal readonly CellType[] Data = new CellType[Board.ChunkArea];
    internal int Used;

    /// <summary>Cells in row-major order; index with <see cref="Board.LocalIndex"/>.</summary>
    public ReadOnlySpan<CellType> Cells => Data;
}

/// <summary>
/// The editable design: an unbounded grid of wire cells plus the components placed on it.
/// Component cells are tracked separately and never hold wire.
/// </summary>
public sealed class Board
{
    public const int ChunkShift = 6;
    public const int ChunkSize = 1 << ChunkShift;
    public const int ChunkArea = ChunkSize * ChunkSize;
    const int ChunkMask = ChunkSize - 1;

    readonly Dictionary<GridPos, Chunk> _chunks = new();
    readonly Dictionary<int, Component> _components = new();
    readonly Dictionary<GridPos, int> _occupied = new();
    int _nextId = 1;

    /// <summary>Bumped on every change that can alter connectivity. Toggling a switch does not count.</summary>
    public long Revision { get; private set; }

    public IReadOnlyDictionary<GridPos, Chunk> Chunks => _chunks;
    public IReadOnlyCollection<Component> Components => _components.Values;

    public static GridPos ChunkOf(GridPos p) => new(p.X >> ChunkShift, p.Y >> ChunkShift);

    public static int LocalIndex(GridPos p) => ((p.Y & ChunkMask) << ChunkShift) | (p.X & ChunkMask);

    public CellType GetCell(GridPos p) =>
        _chunks.TryGetValue(ChunkOf(p), out var chunk) ? chunk.Data[LocalIndex(p)] : CellType.Empty;

    /// <summary>Paints or clears one wire cell. Fails on cells covered by a component.</summary>
    public bool SetCell(GridPos p, CellType type)
    {
        if (_occupied.ContainsKey(p)) return false;

        var key = ChunkOf(p);
        if (!_chunks.TryGetValue(key, out var chunk))
        {
            if (type == CellType.Empty) return true;
            chunk = new Chunk();
            _chunks.Add(key, chunk);
        }

        int i = LocalIndex(p);
        var old = chunk.Data[i];
        if (old == type) return true;

        chunk.Data[i] = type;
        if (old == CellType.Empty) chunk.Used++;
        else if (type == CellType.Empty && --chunk.Used == 0) _chunks.Remove(key);
        Revision++;
        return true;
    }

    public Component? GetComponent(int id) => _components.GetValueOrDefault(id);

    public Component? ComponentAt(GridPos p) => _occupied.TryGetValue(p, out var id) ? _components[id] : null;

    public bool CanPlace(ComponentKind kind, GridPos origin, Rotation rotation)
    {
        foreach (var cell in ComponentDef.Get(kind).Footprint(origin, rotation))
            if (_occupied.ContainsKey(cell) || GetCell(cell) != CellType.Empty)
                return false;
        return true;
    }

    /// <summary>Places a component with its top-left at <paramref name="origin"/>, or returns null if the space is taken.</summary>
    public Component? PlaceComponent(ComponentKind kind, GridPos origin, Rotation rotation = Rotation.R0, string? label = null)
    {
        if (!CanPlace(kind, origin, rotation)) return null;

        var component = new Component(_nextId++, kind, origin, rotation, label);
        _components.Add(component.Id, component);
        foreach (var cell in component.Cells())
            _occupied.Add(cell, component.Id);
        Revision++;
        return component;
    }

    public bool RemoveComponent(int id)
    {
        if (!_components.Remove(id, out var component)) return false;

        foreach (var cell in component.Cells())
            _occupied.Remove(cell);
        Revision++;
        return true;
    }

    /// <summary>Clears whatever is at <paramref name="p"/>: the whole component covering it, or its wire.</summary>
    public bool Erase(GridPos p)
    {
        var component = ComponentAt(p);
        if (component != null) return RemoveComponent(component.Id);
        return GetCell(p) != CellType.Empty && SetCell(p, CellType.Empty);
    }

    /// <summary>Sets a switch or input. Use <see cref="Circuit.SetSwitch"/> to also update a running simulation.</summary>
    public bool SetSwitch(int id, bool on)
    {
        if (!_components.TryGetValue(id, out var component) || !component.IsDriver) return false;
        component.IsOn = on;
        return true;
    }
}
