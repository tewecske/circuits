namespace Circuits.Core.Tests;

public class BoardTests
{
    [Fact]
    public void ComponentsCannotOverlapComponentsOrWires()
    {
        var board = new Board();
        Assert.NotNull(board.PlaceComponent(ComponentKind.Nand, new GridPos(0, 0)));
        Assert.Null(board.PlaceComponent(ComponentKind.Input, new GridPos(2, 2)));

        board.SetCell(new GridPos(5, 1), CellType.Wire);
        Assert.Null(board.PlaceComponent(ComponentKind.Nand, new GridPos(4, 0)));
        Assert.NotNull(board.PlaceComponent(ComponentKind.Nand, new GridPos(6, 0)));
    }

    [Fact]
    public void WiresCannotBePaintedOnComponents()
    {
        var board = new Board();
        board.PlaceComponent(ComponentKind.Nand, new GridPos(0, 0));

        Assert.False(board.SetCell(new GridPos(1, 1), CellType.Wire));
        Assert.Equal(CellType.Empty, board.GetCell(new GridPos(1, 1)));
        Assert.True(board.SetCell(new GridPos(3, 1), CellType.Wire));
    }

    [Fact]
    public void EraseRemovesTheWholeComponentFromAnyOfItsCells()
    {
        var board = new Board();
        var nand = board.PlaceComponent(ComponentKind.Nand, new GridPos(0, 0))!;

        Assert.True(board.Erase(new GridPos(2, 2)));
        Assert.Null(board.GetComponent(nand.Id));
        Assert.Null(board.ComponentAt(new GridPos(0, 0)));
        Assert.NotNull(board.PlaceComponent(ComponentKind.Nand, new GridPos(0, 0)));
    }

    [Fact]
    public void ErasingTheLastWireDropsItsChunk()
    {
        var board = new Board();
        board.SetCell(new GridPos(3, 3), CellType.Wire);
        Assert.Single(board.Chunks);

        Assert.True(board.Erase(new GridPos(3, 3)));
        Assert.Empty(board.Chunks);
        Assert.False(board.Erase(new GridPos(3, 3)));
    }

    [Fact]
    public void NegativeCoordinatesLandInTheirOwnChunks()
    {
        var board = new Board();
        board.SetCell(new GridPos(-1, -1), CellType.Wire);
        board.SetCell(new GridPos(-65, 3), CellType.Crossing);
        board.SetCell(new GridPos(0, 0), CellType.Wire);

        Assert.Equal(CellType.Wire, board.GetCell(new GridPos(-1, -1)));
        Assert.Equal(CellType.Crossing, board.GetCell(new GridPos(-65, 3)));
        Assert.Equal(CellType.Empty, board.GetCell(new GridPos(-1, 0)));
        Assert.Equal(3, board.Chunks.Count);
        Assert.Equal(new GridPos(-2, 0), Board.ChunkOf(new GridPos(-65, 3)));
    }

    [Fact]
    public void RevisionTracksConnectivityChangesOnly()
    {
        var board = new Board();
        var input = board.PlaceComponent(ComponentKind.Input, new GridPos(0, 0))!;
        long afterPlace = board.Revision;

        board.SetSwitch(input.Id, true);
        board.SetCell(new GridPos(5, 5), CellType.Empty);
        Assert.Equal(afterPlace, board.Revision);

        board.SetCell(new GridPos(5, 5), CellType.Wire);
        board.SetCell(new GridPos(5, 5), CellType.Wire);
        Assert.Equal(afterPlace + 1, board.Revision);
    }

    [Fact]
    public void OnlyDriversCanBeSwitched()
    {
        var board = new Board();
        var output = board.PlaceComponent(ComponentKind.Output, new GridPos(0, 0))!;
        var sw = board.PlaceComponent(ComponentKind.Switch, new GridPos(2, 0))!;

        Assert.False(board.SetSwitch(output.Id, true));
        Assert.True(board.SetSwitch(sw.Id, true));
        Assert.True(sw.IsOn);
    }

    [Fact]
    public void RotationTurnsFootprintAndPinsClockwise()
    {
        var board = new Board();
        var nand = board.PlaceComponent(ComponentKind.Nand, new GridPos(10, 10), Rotation.R90)!;

        // Inputs that faced west now face north, the output faces south.
        Assert.Equal(new PlacedPin(0, new GridPos(12, 10), Direction.North, PinKind.In), nand.GetPin(0));
        Assert.Equal(new PlacedPin(1, new GridPos(10, 10), Direction.North, PinKind.In), nand.GetPin(1));
        Assert.Equal(new PlacedPin(2, new GridPos(11, 12), Direction.South, PinKind.Out), nand.GetPin(2));
        Assert.Equal(new GridPos(11, 13), nand.GetPin(2).ConnectionCell);
    }

    [Fact]
    public void RotateLocalWalksCornersClockwise()
    {
        var def = ComponentDef.Nand;
        Assert.Equal(new GridPos(0, 2), def.RotateLocal(new GridPos(0, 0), Rotation.R270));
        Assert.Equal(new GridPos(2, 2), def.RotateLocal(new GridPos(0, 0), Rotation.R180));
        Assert.Equal((3, 3), def.SizeFor(Rotation.R90));
        Assert.Equal(Rotation.R0, Rotation.R270.Next());
    }
}
