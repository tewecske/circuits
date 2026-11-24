namespace Circuits.Core;

/// <summary>Net ids for one board chunk, laid out like <see cref="Chunk.Cells"/>. -1 means no wire.</summary>
public sealed class NetChunk
{
    internal readonly int[] PrimaryNets = new int[Board.ChunkArea];
    internal readonly int[] SecondaryNets = new int[Board.ChunkArea];

    internal NetChunk()
    {
        Array.Fill(PrimaryNets, -1);
        Array.Fill(SecondaryNets, -1);
    }

    /// <summary>Net of a wire, or of the horizontal path through a crossing.</summary>
    public ReadOnlySpan<int> Primary => PrimaryNets;

    /// <summary>Net of the vertical path through a crossing; -1 for plain wires.</summary>
    public ReadOnlySpan<int> Secondary => SecondaryNets;
}

/// <summary>
/// A compiled, read-only snapshot of a board. Every connected group of wires and pins is one net,
/// numbered 0..<see cref="NetCount"/>-1.
/// </summary>
public sealed class Netlist
{
    public static Netlist Empty { get; } = new(0, [], [], [], [], [], [], [], new(), new());

    // NAND gates and drivers are stored as parallel arrays to keep the tick loop tight.
    internal readonly int[] GateInA;
    internal readonly int[] GateInB;
    internal readonly int[] GateOut;
    internal readonly int[] GateComponent;
    internal readonly int[] DriverNet;
    internal readonly int[] DriverComponent;
    internal readonly bool[] DriverInitial;

    readonly Dictionary<int, int[]> _pinNets;
    readonly Dictionary<GridPos, NetChunk> _cellNets;
    readonly Dictionary<int, int> _driverIndex;

    internal Netlist(int netCount,
        int[] gateInA, int[] gateInB, int[] gateOut, int[] gateComponent,
        int[] driverNet, int[] driverComponent, bool[] driverInitial,
        Dictionary<int, int[]> pinNets, Dictionary<GridPos, NetChunk> cellNets)
    {
        NetCount = netCount;
        GateInA = gateInA;
        GateInB = gateInB;
        GateOut = gateOut;
        GateComponent = gateComponent;
        DriverNet = driverNet;
        DriverComponent = driverComponent;
        DriverInitial = driverInitial;
        _pinNets = pinNets;
        _cellNets = cellNets;
        _driverIndex = new Dictionary<int, int>(driverComponent.Length);
        for (int i = 0; i < driverComponent.Length; i++)
            _driverIndex.Add(driverComponent[i], i);
    }

    public int NetCount { get; }
    public int GateCount => GateOut.Length;
    public int DriverCount => DriverNet.Length;

    /// <summary>Per-chunk net ids, keyed like <see cref="Board.Chunks"/>. Meant for the renderer.</summary>
    public IReadOnlyDictionary<GridPos, NetChunk> CellNets => _cellNets;

    public int GetPinNet(int componentId, int pinIndex) =>
        _pinNets.TryGetValue(componentId, out var nets) && (uint)pinIndex < (uint)nets.Length ? nets[pinIndex] : -1;

    public (int Primary, int Secondary) GetCellNets(GridPos p)
    {
        if (!_cellNets.TryGetValue(Board.ChunkOf(p), out var chunk)) return (-1, -1);
        int i = Board.LocalIndex(p);
        return (chunk.PrimaryNets[i], chunk.SecondaryNets[i]);
    }

    internal int DriverIndexOf(int componentId) => _driverIndex.GetValueOrDefault(componentId, -1);
}
