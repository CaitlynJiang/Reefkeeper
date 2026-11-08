# V3: brush and scraping tool

Open `Reefkeeper/Assets/Reefkeeper/Scenes/ShallowReef_V3.unity` and enter Play Mode.
The V3 scene is derived from Xinyu's V2 scene plus the existing remote `v3` work
(headset controls, red coral, coral crab and colony reactions).

| Surface | Tool | Action |
| --- | --- | --- |
| Green algae | Brush | Move the bristles across the surface. |
| Orange fire coral on nursery planting surfaces | Scraping tool | Move the blade forwards/backwards, perpendicular to its edge. |

Both tools are beside the original tool tray. In desktop / simulator body mode,
aim at a tool and press **G**, then aim at a nearby patch and hold **left mouse**.
**H** returns tools; **F5** resets the nursery. In simulator controller mode or
with tracked controllers, use Grip to hold the tool and move it against the
surface. The mouse animation uses the same contact sweep as tracked motion.

The wrong tool does not advance progress; the HUD names the required tool.
Stationary contact, pickup snapping and motion beneath the surface do not clean.
The scraper also rejects sideways motion along the blade. Cleared surfaces open
their planting sockets. Algae keeps the existing timed regrowth behavior;
fire coral stays cleared until nursery reset. Existing RedCoral shop stock is a
separate asset and is not a cleaning target.

The new scraper and fire coral use simple placeholder geometry and materials for
interaction iteration. The original V2 scene is retained. The project still uses
the existing PICO Unity Integration SDK 3.4.0; this change does not update SDKs.

## Development

- `Reefkeeper > V3 > Set up dual cleaning tools` creates the V3 scene if missing.
  Re-running it opens and validates the existing V3 layout without replacing it.
- `Reefkeeper > V3 > Validate dual cleaning scene` checks tool references and surfaces.
- The existing PICO build menu targets **V2**. V3 is currently an Editor prototype;
  select the V3 scene explicitly if preparing a separate build.
- The opt-in integration runner starts only with `-reef-v3-smoke <output-folder>`.
  `Reefkeeper.Editor.V3CleaningSetup.RunAndCheck` generates/validates the scene,
  enters Play Mode, exercises XRI pickup and physics, and writes `result.txt`.
  Use `-batchmode -nographics -executeMethod` with that method, and omit `-quit`.

Tests cover tool/surface matching, keyboard pickup, wrong-tool prompts, occlusion,
partial progress and pause/resume, complete visual removal, tool switching,
tracked-style directional scraping, planting gates, regrowth and reset.
Editor tests do not establish PICO hardware compatibility or rendering performance.

Validated in Unity 6000.3.19f1 on 2026-11-09: compilation, scene validation
(29 cleaning surfaces), and all 26 Play Mode integration assertions passed.
Local report: `artifacts/v3-cleaning-check-3/result.txt` (ignored by Git).
