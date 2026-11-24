namespace Circuits.Core.Tests;

public class SimulatorTests
{
    static readonly string[] Nand =
    [
        "A#Nnn.",
        "..nnnq",
        "B#nnn.",
    ];

    static readonly string[] Not =
    [
        ".#Nnn.",
        "A#nnnq",
        ".#nnn.",
    ];

    static readonly string[] And =
    [
        "A#Nnn.#Nnn.",
        "..nnn##nnnq",
        "B#nnn.#nnn.",
    ];

    static readonly string[] Or =
    [
        ".#Nnn......",
        "A#nnn##....",
        ".#nnn.#Nnn.",
        ".......nnnq",
        ".#Nnn.#nnn.",
        "B#nnn##....",
        ".#nnn......",
    ];

    // Classic four-NAND XOR: n1 = A nand B, then (A nand n1) nand (B nand n1).
    static readonly string[] Xor =
    [
        "A########Nnn......",
        "..#......nnn##....",
        "..#....##nnn.#....",
        "..##Nnn#.....#Nnn.",
        "....nnn#......nnnq",
        "..##nnn#.....#nnn.",
        "..#....##Nnn.#....",
        "..#......nnn##....",
        "B########nnn......",
    ];

    // Active-low SR latch: two cross-coupled NANDs, the feedback lines meet at a crossing.
    static readonly string[] SrLatch =
    [
        "S#Nnn....",
        "..nnn###q",
        ".#nnn..#.",
        ".#####.#.",
        ".....#.#.",
        ".####+##.",
        ".#Nnn#...",
        "..nnn#...",
        "R#nnn....",
    ];

    [Fact]
    public void NandTruthTable() =>
        TestCircuits.FromAscii(Nand).AssertTruthTable("AB", "q", v => !(v[0] && v[1]));

    [Fact]
    public void NotFromOneNand() =>
        TestCircuits.FromAscii(Not).AssertTruthTable("A", "q", v => !v[0]);

    [Fact]
    public void AndFromTwoNands() =>
        TestCircuits.FromAscii(And).AssertTruthTable("AB", "q", v => v[0] && v[1]);

    [Fact]
    public void OrFromThreeNands() =>
        TestCircuits.FromAscii(Or).AssertTruthTable("AB", "q", v => v[0] || v[1]);

    [Fact]
    public void XorFromFourNands() =>
        TestCircuits.FromAscii(Xor).AssertTruthTable("AB", "q", v => v[0] ^ v[1]);

    [Fact]
    public void SeveralDriversOnOneNetAreOred() =>
        TestCircuits.FromAscii(
            "A#.",
            ".#q",
            "B#.").AssertTruthTable("AB", "q", v => v[0] || v[1]);

    [Fact]
    public void CrossingCarriesTwoIndependentSignals()
    {
        var circuit = TestCircuits.FromAscii(
            ".B#..",
            "..#..",
            "A#+#a",
            "..#..",
            "..#b.");

        circuit.AssertTruthTable("AB", "a", v => v[0]);
        circuit.AssertTruthTable("AB", "b", v => v[1]);
    }

    [Fact]
    public void SwitchesAndGatesEachTakeOneTick()
    {
        var circuit = TestCircuits.FromAscii(Not);
        circuit.Step(5);
        Assert.True(circuit.Read("q"));

        circuit.Set("A", true);
        circuit.Step();
        Assert.True(circuit.Read("q")); // the input net is on, the gate has not reacted yet
        circuit.Step();
        Assert.False(circuit.Read("q"));
    }

    [Fact]
    public void SwitchComponentDrivesLikeAnInput()
    {
        var circuit = new Circuit();
        var sw = circuit.Board.PlaceComponent(ComponentKind.Switch, new GridPos(0, 0))!;
        var lamp = circuit.Board.PlaceComponent(ComponentKind.Output, new GridPos(1, 0))!;

        Assert.True(circuit.Toggle(sw.Id));
        circuit.Step();
        Assert.True(circuit.ReadOutput(lamp.Id));

        Assert.True(circuit.Toggle(sw.Id));
        circuit.Step();
        Assert.False(circuit.ReadOutput(lamp.Id));
        Assert.False(circuit.Toggle(lamp.Id));
    }

    [Fact]
    public void SrLatchSetsResetsAndHolds()
    {
        var circuit = TestCircuits.FromAscii(SrLatch);

        circuit.Set("R", true);
        circuit.Step(10);
        Assert.True(circuit.Read("q"));

        circuit.Set("S", true);
        circuit.Step(10);
        Assert.True(circuit.Read("q"));

        circuit.Set("R", false);
        circuit.Step(10);
        Assert.False(circuit.Read("q"));

        circuit.Set("R", true);
        circuit.Step(10);
        Assert.False(circuit.Read("q"));
    }

    [Fact]
    public void LatchKeepsItsValueWhenTheBoardIsEdited()
    {
        var circuit = TestCircuits.FromAscii(SrLatch);
        circuit.Set("R", true);
        circuit.Step(10);
        circuit.Set("S", true);
        circuit.Set("R", false);
        circuit.Step(10);
        circuit.Set("R", true);
        circuit.Step(10);
        Assert.False(circuit.Read("q"));

        circuit.Board.SetCell(new GridPos(40, 40), CellType.Wire);
        Assert.True(circuit.EnsureCompiled());
        for (int i = 0; i < 10; i++)
        {
            circuit.Step();
            Assert.False(circuit.Read("q"));
        }
    }

    [Fact]
    public void NandFedBackIntoItselfOscillatesEveryTick()
    {
        var circuit = TestCircuits.FromAscii(
            "#####.",
            "#Nnn#.",
            "#nnn#q",
            "#nnn..");

        circuit.Step();
        bool previous = circuit.Read("q");
        for (int i = 0; i < 8; i++)
        {
            circuit.Step();
            bool current = circuit.Read("q");
            Assert.NotEqual(previous, current);
            previous = current;
        }
    }

    [Fact]
    public void UnconnectedNandOutputsOn()
    {
        var circuit = new Circuit();
        var nand = circuit.Board.PlaceComponent(ComponentKind.Nand, new GridPos(0, 0))!;

        circuit.Step();

        Assert.True(circuit.Simulator.IsPinOn(nand.Id, 2));
        Assert.Equal(1, circuit.Simulator.Tick);
    }
}
