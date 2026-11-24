namespace Circuits.Core;

/// <summary>Turns a <see cref="Board"/> into a <see cref="Netlist"/> by grouping connected wires and pins.</summary>
public static class NetlistCompiler
{
    /// <summary>Union-find node ids for one chunk. A wire has the same node on both axes; a crossing has two.</summary>
    sealed class CellNodes
    {
        public readonly int[] H = new int[Board.ChunkArea];
        public readonly int[] V = new int[Board.ChunkArea];

        public CellNodes()
        {
            Array.Fill(H, -1);
            Array.Fill(V, -1);
        }
    }

    public static Netlist Compile(Board board)
    {
        int nodeCount = 0;
        var nodes = new Dictionary<GridPos, CellNodes>(board.Chunks.Count);
        foreach (var (key, chunk) in board.Chunks)
        {
            var n = new CellNodes();
            var cells = chunk.Cells;
            for (int i = 0; i < Board.ChunkArea; i++)
            {
                switch (cells[i])
                {
                    case CellType.Wire:
                        n.H[i] = n.V[i] = nodeCount++;
                        break;
                    case CellType.Crossing:
                        n.H[i] = nodeCount++;
                        n.V[i] = nodeCount++;
                        break;
                }
            }
            nodes.Add(key, n);
        }

        var components = board.Components.OrderBy(c => c.Id).ToArray();
        var pinNodes = new int[components.Length][];
        var pinAt = new Dictionary<(GridPos Cell, Direction Side), int>();
        for (int c = 0; c < components.Length; c++)
        {
            var component = components[c];
            pinNodes[c] = new int[component.PinCount];
            for (int p = 0; p < component.PinCount; p++)
            {
                var pin = component.GetPin(p);
                pinNodes[c][p] = nodeCount;
                pinAt[(pin.Cell, pin.Side)] = nodeCount++;
            }
        }

        var sets = new UnionFind(nodeCount);

        // Join each wire to its east and south neighbours, which visits every adjacent pair once.
        foreach (var (key, n) in nodes)
        {
            int baseX = key.X << Board.ChunkShift, baseY = key.Y << Board.ChunkShift;
            for (int ly = 0; ly < Board.ChunkSize; ly++)
            {
                for (int lx = 0; lx < Board.ChunkSize; lx++)
                {
                    int i = (ly << Board.ChunkShift) | lx;
                    if (n.H[i] < 0) continue;

                    int east = lx < Board.ChunkSize - 1
                        ? n.H[i + 1]
                        : NodeAt(nodes, new GridPos(baseX + lx + 1, baseY + ly), horizontal: true);
                    if (east >= 0) sets.Union(n.H[i], east);

                    int south = ly < Board.ChunkSize - 1
                        ? n.V[i + Board.ChunkSize]
                        : NodeAt(nodes, new GridPos(baseX + lx, baseY + ly + 1), horizontal: false);
                    if (south >= 0) sets.Union(n.V[i], south);
                }
            }
        }

        // A pin joins the wire just outside it, or a pin in that cell facing back.
        for (int c = 0; c < components.Length; c++)
        {
            for (int p = 0; p < components[c].PinCount; p++)
            {
                var pin = components[c].GetPin(p);
                var outside = pin.ConnectionCell;
                int target = NodeAt(nodes, outside, pin.Side.IsHorizontal());
                if (target < 0 && !pinAt.TryGetValue((outside, pin.Side.Opposite()), out target)) continue;
                sets.Union(pinNodes[c][p], target);
            }
        }

        var netOfRoot = new int[nodeCount];
        Array.Fill(netOfRoot, -1);
        int netCount = 0;
        int NetOf(int node)
        {
            int root = sets.Find(node);
            if (netOfRoot[root] < 0) netOfRoot[root] = netCount++;
            return netOfRoot[root];
        }

        var pinNets = new Dictionary<int, int[]>(components.Length);
        var gateInA = new List<int>();
        var gateInB = new List<int>();
        var gateOut = new List<int>();
        var gateComponent = new List<int>();
        var driverNet = new List<int>();
        var driverComponent = new List<int>();
        var driverInitial = new List<bool>();
        for (int c = 0; c < components.Length; c++)
        {
            var component = components[c];
            var nets = new int[component.PinCount];
            for (int p = 0; p < nets.Length; p++)
                nets[p] = NetOf(pinNodes[c][p]);
            pinNets.Add(component.Id, nets);

            switch (component.Kind)
            {
                case ComponentKind.Nand:
                    gateInA.Add(nets[0]);
                    gateInB.Add(nets[1]);
                    gateOut.Add(nets[2]);
                    gateComponent.Add(component.Id);
                    break;
                case ComponentKind.Switch or ComponentKind.Input:
                    driverNet.Add(nets[0]);
                    driverComponent.Add(component.Id);
                    driverInitial.Add(component.IsOn);
                    break;
            }
        }

        var cellNets = new Dictionary<GridPos, NetChunk>(nodes.Count);
        foreach (var (key, n) in nodes)
        {
            var chunk = new NetChunk();
            for (int i = 0; i < Board.ChunkArea; i++)
            {
                if (n.H[i] < 0) continue;
                chunk.PrimaryNets[i] = NetOf(n.H[i]);
                if (n.V[i] != n.H[i]) chunk.SecondaryNets[i] = NetOf(n.V[i]);
            }
            cellNets.Add(key, chunk);
        }

        return new Netlist(netCount,
            gateInA.ToArray(), gateInB.ToArray(), gateOut.ToArray(), gateComponent.ToArray(),
            driverNet.ToArray(), driverComponent.ToArray(), driverInitial.ToArray(),
            pinNets, cellNets);
    }

    static int NodeAt(Dictionary<GridPos, CellNodes> nodes, GridPos p, bool horizontal)
    {
        if (!nodes.TryGetValue(Board.ChunkOf(p), out var n)) return -1;
        int i = Board.LocalIndex(p);
        return horizontal ? n.H[i] : n.V[i];
    }
}

internal sealed class UnionFind
{
    readonly int[] _parent;
    readonly int[] _size;

    public UnionFind(int count)
    {
        _parent = new int[count];
        _size = new int[count];
        for (int i = 0; i < count; i++)
        {
            _parent[i] = i;
            _size[i] = 1;
        }
    }

    public int Find(int x)
    {
        while (_parent[x] != x)
        {
            _parent[x] = _parent[_parent[x]];
            x = _parent[x];
        }
        return x;
    }

    public void Union(int a, int b)
    {
        a = Find(a);
        b = Find(b);
        if (a == b) return;
        if (_size[a] < _size[b]) (a, b) = (b, a);
        _parent[b] = a;
        _size[a] += _size[b];
    }
}
