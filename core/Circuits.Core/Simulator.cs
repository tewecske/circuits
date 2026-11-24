namespace Circuits.Core;

/// <summary>
/// Steps a <see cref="Netlist"/> in discrete ticks. Wires are instant; every NAND and every switch
/// takes one tick to respond. Nets driven by several outputs are OR-ed together.
/// </summary>
public sealed class Simulator
{
    Netlist _netlist = Netlist.Empty;
    byte[] _state = [];
    byte[] _next = [];
    byte[] _driverOn = [];

    public Netlist Netlist => _netlist;
    public long Tick { get; private set; }

    /// <summary>On/off (1/0) per net id, for the renderer.</summary>
    public ReadOnlySpan<byte> NetStates => _state;

    /// <summary>
    /// Switches to a new netlist. Gate outputs are carried over by component id, so latches keep
    /// their value across edits.
    /// </summary>
    public void Load(Netlist netlist)
    {
        var state = new byte[netlist.NetCount];

        var old = _netlist;
        if (old.GateCount > 0 && netlist.GateCount > 0)
        {
            var previous = new Dictionary<int, byte>(old.GateCount);
            for (int g = 0; g < old.GateCount; g++)
                previous[old.GateComponent[g]] = _state[old.GateOut[g]];
            for (int g = 0; g < netlist.GateCount; g++)
                if (previous.TryGetValue(netlist.GateComponent[g], out var value))
                    state[netlist.GateOut[g]] |= value;
        }

        var driverOn = new byte[netlist.DriverCount];
        for (int d = 0; d < driverOn.Length; d++)
        {
            driverOn[d] = netlist.DriverInitial[d] ? (byte)1 : (byte)0;
            state[netlist.DriverNet[d]] |= driverOn[d];
        }

        _netlist = netlist;
        _state = state;
        _next = new byte[netlist.NetCount];
        _driverOn = driverOn;
    }

    /// <summary>Sets a switch or input. Takes effect on the next tick.</summary>
    public bool SetDriver(int componentId, bool on)
    {
        int d = _netlist.DriverIndexOf(componentId);
        if (d < 0) return false;
        _driverOn[d] = on ? (byte)1 : (byte)0;
        return true;
    }

    public void Step(int ticks = 1)
    {
        var inA = _netlist.GateInA;
        var inB = _netlist.GateInB;
        var outs = _netlist.GateOut;
        var driverNet = _netlist.DriverNet;
        var driverOn = _driverOn;

        for (int t = 0; t < ticks; t++)
        {
            var cur = _state;
            var next = _next;
            Array.Clear(next);

            for (int d = 0; d < driverNet.Length; d++)
                next[driverNet[d]] |= driverOn[d];

            for (int g = 0; g < outs.Length; g++)
                next[outs[g]] |= (byte)((cur[inA[g]] & cur[inB[g]]) ^ 1);

            _state = next;
            _next = cur;
        }
        Tick += ticks;
    }

    public bool IsNetOn(int net) => (uint)net < (uint)_state.Length && _state[net] != 0;

    public bool IsPinOn(int componentId, int pinIndex) => IsNetOn(_netlist.GetPinNet(componentId, pinIndex));
}
