# V3: brush and scraping tool

Open `Reefkeeper/Assets/Reefkeeper/Scenes/ShallowReef_V3.unity` and enter Play Mode.
The V3 scene is derived from Xinyu's V2 scene plus the existing remote `v3` work
(headset controls, red coral, coral crab and colony reactions).

| Surface | Tool | Action |
| --- | --- | --- |
| Green algae | Brush | Move the bristles across the surface. |
| Orange/yellow spiny fire coral | File-shaped scraping tool | Move the working face forwards/backwards along the file's length. |

The widened table holds the **brush on the left** and **file on the right**.
The left practice bench presents algae and fire coral side by side. Four extra
fire coral samples attach to the coral tree, with their crust and spine roots
conforming to its mesh. These samples can be cleaned and reset, but do not add
planting sockets. Existing fire coral planting pads keep their planting gates.

In desktop / simulator body mode,
aim at a tool and press **G**, then aim at a nearby patch and hold **left mouse**.
**H** returns tools; **F5** resets the nursery. In simulator controller mode or
with tracked controllers, use Grip to hold the tool and move it against the
surface. The mouse animation uses the same contact sweep as tracked motion.

The wrong tool does not advance progress; the HUD names the required tool.
Stationary contact, pickup snapping and motion beneath the surface do not clean.
The file also rejects sideways motion across its width. Cleared planting surfaces open
their planting sockets. Algae keeps the existing timed regrowth behavior;
fire coral stays cleared until nursery reset. Existing RedCoral shop stock is a
separate asset and is not a cleaning target.

Fire coral uses simple, varied orange/yellow tapered spines rising from a thin
base, with different heights, widths, tilts and bends. The file has a long metal
working face, cross-cut teeth and a black/yellow grip. These are editable prototype
meshes, not final art. Intermittent opening, flame bursts and player damage are
future ideas only; none is implemented in this version.

The original V2 scene is retained. The project still uses
the existing PICO Unity Integration SDK 3.4.0; this change does not update SDKs.

## Development

- `Reefkeeper > V3 > Set up dual cleaning tools` creates the V3 scene if missing.
  Re-running it opens and validates the existing V3 layout without replacing it.
- `Reefkeeper > V3 > Validate dual cleaning scene` checks tool references and surfaces.
- `Reefkeeper > V3 > Apply spiny coral and file feedback` rebuilds the prototype
  meshes and replaces the authored practice/table/tree layout in the open V3 scene.
  This is an intentional layout reset; it also validates tool fit and tree attachment.
- The existing PICO build menu targets **V2**. V3 is currently an Editor prototype;
  select the V3 scene explicitly if preparing a separate build.
- The opt-in integration runner starts only with `-reef-v3-smoke <output-folder>`.
  `Reefkeeper.Editor.V3CleaningSetup.RunAndCheck` generates/validates the scene,
  enters Play Mode, exercises XRI pickup and physics, and writes `result.txt`.
  Use `-batchmode -nographics -executeMethod` with that method, and omit `-quit`.
- In an already running Editor Play session, call
  `Reefkeeper.ReefV3SmokeCheck.RunInEditor(outputFolder)` from an Editor script.
  It switches to desktop mode, tests keyboard/mouse and simulated tracked input,
  then stops Play Mode without closing Unity. Temporary input settings are restored.

Tests cover tool/surface matching, keyboard pickup, wrong-tool prompts, occlusion,
partial progress and pause/resume, complete visual removal, tool switching,
tracked-style directional scraping, planting gates, regrowth and reset, plus
wrong-tool rejection and angled tracked contact on every added fire coral sample.
Editor tests do not establish PICO hardware compatibility or rendering performance.

Validated in Unity 6000.3.19f1 on 2026-11-09: compilation, scene validation,
tabletop tool fit, attachment of all four samples and their spine roots, and
all 43 Play Mode integration assertions passed. Scene renders were inspected
for the practice bench, file, fire coral and coral tree.
Local report: `artifacts/v3-feedback-check/result.txt` (ignored by Git).
