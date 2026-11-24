namespace Circuits.Core.Tests;

/// <summary>Builds circuits from ASCII art so test layouts stay readable.</summary>
/// <remarks>
/// '#' wire, '+' crossing, '.' empty, 'N' the top-left of a NAND with 'n' for the rest of its 3×3 body,
/// other upper-case letters are Inputs and lower-case letters are Outputs, labelled with that letter.
/// </remarks>
internal static class TestCircuits
{
    public static Circuit FromAscii(params string[] rows)
    {
        var board = new Board();
        var body = new HashSet<GridPos>();
        for (int y = 0; y < rows.Length; y++)
        {
            for (int x = 0; x < rows[y].Length; x++)
            {
                var p = new GridPos(x, y);
                char ch = rows[y][x];
                bool ok = ch switch
                {
                    '.' => true,
                    '#' => board.SetCell(p, CellType.Wire),
                    '+' => board.SetCell(p, CellType.Crossing),
                    'n' => body.Add(p),
                    'N' => board.PlaceComponent(ComponentKind.Nand, p) != null,
                    >= 'A' and <= 'Z' => board.PlaceComponent(ComponentKind.Input, p, label: ch.ToString()) != null,
                    >= 'a' and <= 'z' => board.PlaceComponent(ComponentKind.Output, p, label: ch.ToString()) != null,
                    _ => throw new ArgumentException($"Unknown cell '{ch}' at {p}"),
                };
                if (!ok) throw new ArgumentException($"Could not build '{ch}' at {p}");
            }
        }

        foreach (var nand in board.Components.Where(c => c.Kind == ComponentKind.Nand))
            foreach (var cell in nand.Cells())
                if (cell != nand.Origin && !body.Remove(cell))
                    throw new ArgumentException($"NAND at {nand.Origin} needs 'n' at {cell}");
        if (body.Count > 0)
            throw new ArgumentException($"Stray NAND body cells: {string.Join(", ", body)}");

        return new Circuit(board);
    }

    public static Component Find(this Circuit circuit, string label) =>
        circuit.Board.Components.Single(c => c.Label == label);

    public static void Set(this Circuit circuit, string label, bool on) =>
        Assert.True(circuit.SetSwitch(circuit.Find(label).Id, on));

    public static bool Read(this Circuit circuit, string label) => circuit.ReadOutput(circuit.Find(label).Id);

    /// <summary>Drives every combination of <paramref name="inputs"/> (one label per char) and checks the output.</summary>
    public static void AssertTruthTable(this Circuit circuit, string inputs, string output,
        Func<bool[], bool> expected, int settleTicks = 10)
    {
        for (int mask = 0; mask < 1 << inputs.Length; mask++)
        {
            var values = new bool[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                values[i] = ((mask >> i) & 1) != 0;
                circuit.Set(inputs[i].ToString(), values[i]);
            }
            circuit.Step(settleTicks);

            bool want = expected(values);
            Assert.True(circuit.Read(output) == want,
                $"{output} should be {want} for {inputs}={string.Concat(values.Select(v => v ? '1' : '0'))}");
        }
    }
}
