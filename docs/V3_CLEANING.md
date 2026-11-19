# V3: brush and scraping tool

Open `Reefkeeper/Assets/Reefkeeper/Scenes/ShallowReef_V3.unity` and enter Play Mode.
The V3 scene is derived from Xinyu's V2 scene plus the existing remote `v3` work
(headset controls, red coral, coral crab and colony reactions).

| Surface | Tool | Action |
| --- | --- | --- |
| Green algae | Brush | Move the bristles across the surface. |
| Pale gold branching fire coral | File-shaped scraping tool | Move the working face forwards/backwards along the file's length. |

The widened table holds the **brush on the left** and **file on the right**.
The left practice bench presents algae and fire coral side by side. Four extra
larger fire coral samples run lengthwise along the actual planting tree, with
their narrow crust and branch roots conforming to its mesh. These samples can be
cleaned and reset, but do not add planting sockets. Existing fire coral planting
pads keep their planting gates.

In desktop / simulator body mode,
aim at a tool and press **G**, then aim at a nearby patch and hold **left mouse**.
**H** returns tools; **F5** resets the nursery. In simulator controller mode or
with tracked controllers, use Grip to hold the tool and move it against the
surface. The mouse animation uses the same contact sweep as tracked motion.

## Pebble tool shortcut menu

The separate five-slot arc uses warm yellow, lake blue and irregular pebble
shapes. The yellow **Tools** pebble and all slots stay hidden until summoned;
they slide, scale and fade into view over 0.26 seconds, then retract on close.
The lower-left **Tab Tools** hint uses the same key-icon assets and typography
as **F1 Controls**. **Esc Close** appears only while open. Two slots show actual
captures of the current brush and file; three smaller slots reserve future tools.
Tool slots use fill-color changes instead of outlines. The yellow Tools pebble
has a small upper-left curved highlight and a soft downward shadow.

| Input | Action |
| --- | --- |
| Tap **Tab** / click the **Tools** shortcut hint | Toggle the tool menu. |
| Click an acquired tool | Equip it and close the menu. |
| **Esc** | Close and restore the previously held tool. |
| Right controller **A** / **B** | Open or toggle / close the menu. |
| Right controller pointer + trigger | Choose a tool; it follows the right controller without requiring held Grip. |
| **Backquote** (the key below Esc) | Cycle simulator devices; replaces the simulator's former Tab binding. |

Pick up each tool once at the tutorial table to unlock it for the current Play
session. The table retains a visual display while its working tool is away.
Unacquired slots say **Pick up first** and cannot equip; future slots are inactive.
F5 resets the nursery but retains tools acquired in that session. Leaving Play
Mode starts a fresh inventory; no save-game persistence is added yet.

Opening the menu hides the held tool and captures world interaction while
swimming continues. Closing restores it; choosing another returns the old one.
The selection click is consumed until release to avoid immediately cleaning or
planting behind the UI. Desktop and simulator use the mouse. A running headset
uses the existing floating world-space HUD and a right-controller pointer.
The dive-card / journal UI and B/J/left-Y shortcuts remain future work.

`ReefToolMenu` is attached at runtime only when the workshop has a scraper, so the
authored V2 scene is unchanged. Tool sprites live in `Resources/ToolMenu`; its
README records their in-game screenshot sources. Re-capture them when models change.

The wrong tool does not advance progress; the HUD names the required tool.
Stationary contact, pickup snapping and motion beneath the surface do not clean.
The file also rejects sideways motion across its width. Cleared planting surfaces open
their planting sockets. Algae keeps the existing timed regrowth behavior;
fire coral stays cleared until nursery reset. Existing RedCoral shop stock is a
separate asset and is not a cleaning target.

Accepted file strokes shed small, faceted coral chips at the working face. Their
initial velocity follows the stroke and the surface normal, with a little outward
scatter and rotation. They drift in world space and fade/shrink over 0.65–1.05 s,
so returning the file does not drag the chips back to the table. Emission scales
with actual removed travel; idle contact, wrong tools, rejected sideways motion,
and already-cleared patches emit nothing. Nursery reset clears any remaining chips.
The effect uses one scene particle system with a maximum of 96 particles, a shared
mesh/material, no particle physics collisions, and the existing underwater fog.

Fire coral uses curved, irregularly forked branches rising from a narrow, wavy
strip, with rounded tips and a pale gold-to-cream color gradient. The prototype
takes its branching silhouette and pale tips from the
[Naturalis branching fire coral guide](https://caribbean-diving.linnaeus.naturalis.nl/linnaeus_ng/app/views/species/nsr_taxon.php?id=6832).
Each colony has nine growth groups and one branch material, keeping progressive
removal while reducing material passes. The file has a long metal
working face, cross-cut teeth and a black/yellow grip. These are editable prototype
meshes, not final art. Intermittent opening, flame bursts and player damage are
future ideas only; none is implemented in this version.

The original V2 scene is retained. The project still uses
the existing PICO Unity Integration SDK 3.4.0; this change does not update SDKs.

## Development

- `Reefkeeper > V3 > Set up dual cleaning tools` creates the V3 scene if missing.
  Re-running it opens and validates the existing V3 layout without replacing it.
- `Reefkeeper > V3 > Validate dual cleaning scene` checks tool references and surfaces.
- `Reefkeeper > V3 > Apply coral and file feedback` rebuilds the prototype
  meshes and replaces the authored practice/table/tree layout in the open V3 scene.
  This is an intentional layout reset; it also validates tool fit and tree attachment.
- `Reefkeeper > V3 > Update fire coral shape and tree targets` rebuilds the fire
  coral prefab and tree targets while preserving the current tool/table layout.
- `Reefkeeper > V3 > Set up scraping debris VFX` creates/configures the bounded
  emitter and wires it to the scraper. Its assets are referenced by the scene for builds.
- `Reefkeeper > V3 > Import tool screenshot icons` configures the two captured
  tool images as uncompressed UI sprites without changing the scene.
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
VFX checks exercise actual desktop/tracked emissions, world-space placement,
opacity decay, idle/wrong-tool rejection, release/return, and reset cleanup.
Tool-menu checks cover pickup-based unlocking, retained table displays, actual
mouse/Tab/Escape selection, hidden-tool restoration, world-input suppression,
continued swimming, simulator key remapping, and simulated right-controller
A/B/trigger selection and tracked following.
Editor tests do not establish PICO hardware compatibility or rendering performance.

Validated in Unity 6000.3.19f1 on 2026-11-09: compilation and all 85 Play Mode
integration assertions passed, including the existing cleaning/VFX regressions
and the new menu checks. Earlier scene validation checked tabletop tool fit and
attachment of all four fire-coral samples and their branch roots. Actual Game
View captures were inspected for the open and closed tool menu.
Local report: `artifacts/v3-tool-menu-check/result.txt`; previews:
`artifacts/v3-tool-menu-reveal-preview/` (both ignored by Git).
