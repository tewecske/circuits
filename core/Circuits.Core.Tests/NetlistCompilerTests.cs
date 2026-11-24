namespace Circuits.Core.Tests;

public class NetlistCompilerTests
{
    static int NetAt(Netlist netlist, int x, int y) => netlist.GetCellNets(new GridPos(x, y)).Primary;

    [Fact]
    public void TouchingWiresFormOneNet()
    {
        var board = new Board();
        for (int x = 0; x < 6; x++) board.SetCell(new GridPos(x, 0), CellType.Wire);
        board.SetCell(new GridPos(0, 2), CellType.Wire);

        var netlist = NetlistCompiler.Compile(board);

        Assert.Equal(2, netlist.NetCount);
        for (int x = 1; x < 6; x++) Assert.Equal(NetAt(netlist, 0, 0), NetAt(netlist, x, 0));
        Assert.NotEqual(NetAt(netlist, 0, 0), NetAt(netlist, 0, 2));
        Assert.Equal(-1, NetAt(netlist, 0, 1));
    }

    [Fact]
    public void WiresConnectAcrossChunkBorders()
    {
        var board = new Board();
        for (int x = -70; x <= 70; x++) board.SetCell(new GridPos(x, 0), CellType.Wire);
        for (int y = -70; y <= 70; y++) board.SetCell(new GridPos(5, y), CellType.Wire);

        var netlist = NetlistCompiler.Compile(board);

        Assert.Equal(1, netlist.NetCount);
        Assert.True(board.Chunks.Count >= 6);
    }

    [Fact]
    public void CrossingKeepsHorizontalAndVerticalApart()
    {
        var circuit = TestCircuits.FromAscii(
            ".#.",
            "#+#",
            ".#.");
        var netlist = circuit.Netlist;

        int horizontal = NetAt(netlist, 0, 1);
        int vertical = NetAt(netlist, 1, 0);
        Assert.Equal(2, netlist.NetCount);
        Assert.Equal(horizontal, NetAt(netlist, 2, 1));
        Assert.Equal(vertical, NetAt(netlist, 1, 2));
        Assert.NotEqual(horizontal, vertical);
        Assert.Equal((horizontal, vertical), netlist.GetCellNets(new GridPos(1, 1)));
    }

    [Fact]
    public void SignalsPassThroughConsecutiveCrossings()
    {
        var netlist = TestCircuits.FromAscii("#++#").Netlist;

        Assert.Equal(NetAt(netlist, 0, 0), NetAt(netlist, 3, 0));
    }

    [Fact]
    public void WireTouchingAGateBodyDoesNotConnect()
    {
        var board = new Board();
        var nand = board.PlaceComponent(ComponentKind.Nand, new GridPos(0, 0))!;
        board.SetCell(new GridPos(-1, 1), CellType.Wire); // beside the body, between the inputs
        board.SetCell(new GridPos(1, 3), CellType.Wire);  // below the body
        board.SetCell(new GridPos(3, 0), CellType.Wire);  // right of the body, above the output

        var netlist = NetlistCompiler.Compile(board);

        Assert.Equal(6, netlist.NetCount);
        var pins = Enumerable.Range(0, 3).Select(p => netlist.GetPinNet(nand.Id, p)).ToArray();
        Assert.Equal(3, pins.Distinct().Count());
        Assert.DoesNotContain(NetAt(netlist, -1, 1), pins);
    }

    [Fact]
    public void FacingPinsConnectWithoutAWire()
    {
        var board = new Board();
        var input = board.PlaceComponent(ComponentKind.Input, new GridPos(0, 0))!;
        var output = board.PlaceComponent(ComponentKind.Output, new GridPos(1, 0))!;

        var netlist = NetlistCompiler.Compile(board);

        Assert.Equal(1, netlist.NetCount);
        Assert.Equal(netlist.GetPinNet(input.Id, 0), netlist.GetPinNet(output.Id, 0));
    }

    [Fact]
    public void PinsThatDoNotFaceEachOtherStayApart()
    {
        var board = new Board();
        var a = board.PlaceComponent(ComponentKind.Input, new GridPos(0, 0))!;
        var b = board.PlaceComponent(ComponentKind.Input, new GridPos(1, 0))!;

        var netlist = NetlistCompiler.Compile(board);

        Assert.NotEqual(netlist.GetPinNet(a.Id, 0), netlist.GetPinNet(b.Id, 0));
    }

    [Fact]
    public void PinUsesTheMatchingAxisOfACrossing()
    {
        var circuit = TestCircuits.FromAscii(
            ".#.",
            "A+a",
            ".#.");
        var netlist = circuit.Netlist;

        int input = netlist.GetPinNet(circuit.Find("A").Id, 0);
        Assert.Equal(input, netlist.GetPinNet(circuit.Find("a").Id, 0));
        Assert.NotEqual(input, NetAt(netlist, 1, 0));
    }

    [Fact]
    public void UnconnectedPinsEachGetTheirOwnNet()
    {
        var board = new Board();
        board.PlaceComponent(ComponentKind.Nand, new GridPos(0, 0));

        var netlist = NetlistCompiler.Compile(board);

        Assert.Equal(3, netlist.NetCount);
        Assert.Equal(1, netlist.GateCount);
    }
}
