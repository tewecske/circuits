# Circuits: plan

A performant, cross-platform (Windows/Linux, later Android) logic-circuit sandbox. The genre is
the same as Virtual Circuit Board and Turing Complete, but the name, art and content are our own.
This first slice has a grid, wires, and four components: Switch, Input, Output and NAND.
In-game programming comes later.

## 1. Tech

**Godot 4 (.NET edition) + C#.** The simulation core is plain C# with no Godot dependency.

- The heavy work is the simulation, which runs in plain C# on flat arrays on a separate thread.
  Wire drawing runs in a GPU shader. Neither depends on GDScript speed.
- Godot handles menus, options, input, audio, saving and export to Windows, Linux and Android.
- Because the core is engine-independent, it can be unit tested headless and could later move
  to Rust or C++ (GDExtension) if that is ever needed.

| Option | Speed | Menus/UI | Win/Linux/Android | Verdict |
|---|---|---|---|---|
| **Godot 4 + C#** | High (C# core + shaders) | Excellent | ✅ ✅ ✅ | **Chosen** |
| Godot + GDScript only | Too slow for big circuits | Excellent | ✅ ✅ ✅ | UI only |
| Godot + Rust/C++ GDExtension | Highest | Excellent | ✅ ✅ ✅ (harder builds) | Upgrade path |
| Bevy / SDL3 + ImGui | Highest | Build it yourself | Android painful | Too much UI work |

## 2. Core concepts

- **Grid:** unbounded and split into 64×64 chunks. Each cell is empty, wire, or a wire crossing.
  Component cells are tracked separately and never hold wire.
- **Wires:** painted cell by cell. Wire cells that touch on the four sides connect.
  A **crossing** cell lets a horizontal and a vertical line pass without joining.
- **Components** sit on the grid, have pins on their edges, and can be rotated in 90° steps.
  - **Switch:** click to toggle. Drives its output.
  - **Input:** a labelled input port for the design. It acts like a switch for now, and later
    becomes the pin of a reusable sub-circuit or a level input.
  - **Output:** a labelled lamp/port that lights when its net is on.
  - **NAND:** 3×3 cells, two inputs on the west side, one output on the east side.
    NAND is functionally complete: NOT, AND, OR, XOR, latches and everything else can be built from it.
- **Pin rule:** a pin connects to the cell just outside its side. A wire there (or the matching
  axis of a crossing) joins the pin's net. So does another pin in that cell that faces back.
  A wire that only touches a component's body does not connect.
- **Simulation:**
  - Wires carry signals instantly.
  - Every NAND, and every switch or input, takes **1 tick** to respond.
  - When several outputs drive one net, the result is OR (wired-OR).
  - Controls: pause, single step, speed slider (1 to 1000+ ticks/s) and "max".
  - Gate outputs survive edits, so a latch keeps its value after you change the board.

## 3. Architecture

```
core/Circuits.Core          (pure C#, no Godot)
  Board            chunked wire cells + components, Revision counter
  ComponentDef     footprints and pins per kind, rotation
  NetlistCompiler  Board → Netlist (union-find over wire cells, crossings and pins)
  Netlist          net count, NAND gates as parallel arrays, drivers, per-cell and per-pin net ids
  Simulator        double-buffered byte[] net states, tick loop, state carry-over on reload
  Circuit          Board + Simulator facade, recompiles lazily when Board.Revision changes
  (later) Commands undo/redo; Serializer versioned save format
core/Circuits.Core.Tests    xUnit; circuits written as ASCII art
game/                       (Godot project, milestone 2+)
  BoardView        chunk textures + wire shader, MultiMesh for components
  Tools            select, place, wire, erase, pan/zoom (mouse + touch)
  UI               main menu, side palette, toolbar, options, tutorial overlay
```

- **Recompile on edit.** The netlist is rebuilt after each change (debounced). Union-find over
  a million cells takes milliseconds, so incremental compiling can wait.
- **Sim thread** (milestone 3). The simulation runs on its own thread and publishes a snapshot
  of net states each frame. Rendering only reads the snapshot.
- **Rendering.**
  - Each chunk texture stores the net id per cell.
  - A small "state texture" stores on/off per net id and is uploaded each frame.
  - The fragment shader colours wires from those two textures, so a huge board costs about the
    same to draw as a small one.
  - Components are drawn through a MultiMesh with per-instance state.

## 4. Visuals

- Dark board with a faint grid that fades out as you zoom out.
- Off wires are dim and saturated. On wires are bright with HDR glow (2D HDR + WorldEnvironment
  glow), plus an optional slow pulse along active wires.
- Gate bodies show their output state, and lamps glow with a soft halo.
- Hovering a wire highlights its whole net.
- Ghost preview with valid/invalid tint while dragging, and a short snap animation on placement.
- Small, soft sounds for placing and toggling.

## 5. Editor and controls

- **Side palette:** searchable by name and tags, with collapsible categories and hotkeys 1–4.
  Drag a component onto the board, or click it and then click the board. `R` rotates.
- **Tools:**
  - Select: box select, move, copy/paste, delete.
  - Wire painter: draws straight lines, Shift locks the direction.
  - Crossing and eraser.
- **Navigation:** wheel to zoom, middle mouse or Space+drag to pan. On Android: pinch to zoom,
  two-finger pan, long-press for a context menu.
- **Toolbar:** play/pause, step, speed, undo/redo, save, and a tick counter.

## 6. Main menu and options

- **Main menu:** Continue, New Sandbox, Load Design (thumbnail grid), Tutorial, Options,
  Quit (hidden on Android).
- **Options** (saved to `user://settings.cfg`):
  - **Audio:** master, music, SFX and UI volume; mute when unfocused.
  - **Video:**
    - Window mode, resolution, VSync, FPS cap.
    - UI scale, glow intensity (including off), grid opacity.
    - Reduced motion.
  - **Accessibility:**
    - Colour presets: Default, Deuteranopia, Protanopia, Tritanopia, High Contrast, Monochrome.
      Colours come from Okabe-Ito-style palettes.
    - Custom colour pickers.
    - Never rely on hue alone: on/off also differ in brightness, and an optional pattern mode
      adds stripes and pin marks.
    - Text size, a no-flashing limit, and a hold-vs-toggle choice for drags.
  - **Controls:** remappable keybindings through InputMap.

## 7. Tutorial

A scripted overlay that highlights a UI element and waits until a condition is met:

1. Pan and zoom.
2. Wire a Switch to an Output and toggle it.
3. Explore a NAND.
4. Build NOT.
5. Build AND.
6. Challenge: build OR without hints.

A truth-table checker drives the Inputs and compares the Outputs, so steps validate themselves.
The same checker can later power puzzle levels.

## 8. Save/Load

- Designs are saved to `user://designs/<name>.circuit` as versioned JSON:
  - Metadata.
  - Components: kind, origin, rotation, label, switch state.
  - Wire cells: per-chunk run-length encoding, base64.
- A PNG thumbnail is saved next to each design.
- Autosave. Writes go to a temp file that is then renamed, so a crash can't corrupt a save.
- The format leaves room for "save design as component".

## 9. Milestones

1. **Core** ✅ (in progress): board, compiler, simulator and facade, with tests for NOT, AND,
   OR, XOR, an SR latch, an oscillator, crossings and wired-OR.
2. **Board view:** Godot project in `game/`, chunk rendering, wire shader, pan/zoom, wire
   painting, placing the four components.
3. **Editor:** select/move/delete, undo/redo, searchable drag-and-drop palette, sim thread and
   controls.
4. **Save/Load:** file format, thumbnails, load screen, autosave.
5. **Menus and options:** main menu, audio, video and accessibility settings, colour presets.
6. **Tutorial:** overlay plus truth-table checker.
7. **Polish and export:** glow, sounds, Windows and Linux builds, then Android touch controls.

**Performance targets:** 60 fps with a board of 1M wire cells and 100k gates; a 1k-gate
circuit simulating at 10k+ ticks/s.

**Later, out of scope for now:** custom components/sub-circuits, buses, in-game programming, levels.

## Development

```sh
nix develop          # .NET 10 SDK, Godot 4 (.NET), git, git-lfs
dotnet test          # runs core tests
godot4-mono          # editor; launch from inside the shell so it finds the SDK

nix develop .#assets   # + Inkscape, Krita, Audacity
nix develop .#windows  # + Wine, so exports from Linux can set .exe icons
nix develop .#android  # + JDK 17, Android SDK/NDK (unfree, licence accepted in flake.nix)
```

Not in the flake:
- Godot .NET export templates. Install them from the editor (Editor → Manage Export Templates);
  they must match the editor version exactly.
- Your C# editor (Rider, VS Code + C# Dev Kit, or a language server).
