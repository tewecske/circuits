namespace Circuits.Core;

/// <summary>A board together with its simulation. Recompiles lazily after the board changes.</summary>
public sealed class Circuit
{
    long _compiledRevision = -1;

    public Circuit(Board? board = null)
    {
        Board = board ?? new Board();
    }

    public Board Board { get; }
    public Simulator Simulator { get; } = new();

    public Netlist Netlist
    {
        get
        {
            EnsureCompiled();
            return Simulator.Netlist;
        }
    }

    /// <summary>Rebuilds the netlist if the board changed since the last compile. Returns true if it did.</summary>
    public bool EnsureCompiled()
    {
        if (_compiledRevision == Board.Revision) return false;
        Simulator.Load(NetlistCompiler.Compile(Board));
        _compiledRevision = Board.Revision;
        return true;
    }

    public bool SetSwitch(int componentId, bool on)
    {
        if (!Board.SetSwitch(componentId, on)) return false;
        EnsureCompiled();
        Simulator.SetDriver(componentId, on);
        return true;
    }

    public bool Toggle(int componentId) =>
        Board.GetComponent(componentId) is { IsDriver: true } component && SetSwitch(componentId, !component.IsOn);

    public void Step(int ticks = 1)
    {
        EnsureCompiled();
        Simulator.Step(ticks);
    }

    /// <summary>State of a component's first pin: the lamp of an Output, the output of a switch.</summary>
    public bool ReadOutput(int componentId)
    {
        EnsureCompiled();
        return Simulator.IsPinOn(componentId, 0);
    }
}
