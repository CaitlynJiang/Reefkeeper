# Reefkeeper — underwater prototype

Unity **6.3 LTS / 6000.3.19f1**, Universal Render Pipeline, XR Interaction Toolkit **3.3.2**. On the `immersive-opening` branch, open the `Reefkeeper` folder in Unity Hub, then open `Assets/Reefkeeper/Scenes/ImmersiveOpening.unity` and press Play. The opening starts above water; F2 switches the Editor preview to XR simulation. Android builds enter XR directly.

Headset-confirmed baseline (2026-11-19): PICO SDK **6.2.0**, layered launcher icon, and the first tool-grip gesture logic. **The user confirmed that hand swimming works on the Swan headset with the rollback APK.** Tag `swan-swim-baseline-2026-11-19` preserves that version; the specific cause in the later gesture/comfort changes is not yet isolated. See [opening notes](docs/immersive-opening.txt) and [comparison scope](docs/swan-hand-controls-comparison.txt). The milestones below describe earlier prototype checkpoints.

Current increment: [tool grip offsets only](docs/swan-grip-offsets-only.txt), keeping the verified swimming/gesture implementation unchanged. The new tool fit awaits headset testing.

![Milestone 08 — teal water, softer sand and clearer caustics](docs/previews/milestone-08-nursery.png)

Milestone 01: Windows build succeeds; movement math and runtime movement/collision checks pass. Three offscreen scene renders were captured for visual review (HUD is not part of those renders). These are procedural prototype assets; underwater materials and art can be replaced independently.

Milestone 02: imported URP-WaterShaders with its original subgraph GUIDs, configured Unity 6 fullscreen color/depth rendering, and applied its caustics to reef materials. Windows shader compilation and runtime movement/collision checks pass; three underwater views were rendered and reviewed. Headset/XR support remains untested.

Milestone 03: added a winding sand channel, raised banks and dunes, with a saved mesh collider; introduced a bilingual Editor art panel for lighting, materials and terrain parameters. Windows build and runtime checks, including descent onto the uneven seabed, pass. The palette and placeholder models remain ready for the user's art direction.

Milestone 04: added Unity's XR Interaction Simulator, underwater XR Origin movement, a grabbable brush and a nursery rack with 24 cleanable patches. The Windows build and both runtime suites pass: simulated swimming/braking, keyboard grip and release, brush following, contact-based cleaning, completion, reset and mode switching. Screenshots below are engine renders with the HUD omitted. Headset and Rokoko input remain untested.

Milestone 05: imported Quaternius's seven animated fish models with separate URP materials and reusable prefabs. Three small-fish schools and one manta ray now swim through the saved reef scene. Windows build, fish runtime checks and the XR interaction regression pass. Validation covers actual skinned vertex deformation, looping animation, path motion, seabed clearance and all 24 rack patches. Also removed the starter rig's 1 mm movement cutoff, which discarded slow XR motion at high frame rates, and made the opt-in input check work in hidden windows. The existing scene palette and nursery layout are preserved for further art direction.

Milestone 06: used the supplied photographic mood board to shift the water toward blue, soften caustics, introduce fine sand/ripple texture and replace the table-shaped rack with a three-tier coral nursery. The nursery has slender stems, open lattice trays, suspended branching fragments, buoyancy floats and seabed tethers. Its 24 cleanable patches sit along the three front rims; three additional trees establish the nursery setting. Quaternius fish models, materials and animations retain their existing style. The Windows build and XR interaction checks pass, including all 24 patches, completion and reset; three art views were rendered and visually reviewed.

Milestone 07: brightened the underwater palette with slightly warm daylight, lighter shadows and a subtle green tint near the sand. The water ceiling now uses irregular animated reflection patches and a brighter overhead color; its fog blending preserves those highlights. Surface reflection strength and ripple scale are editable material properties. The Windows build and shader compilation pass, and three matching art views were rendered for visual review. This pass changes lighting, grading and materials; the nursery geometry and Quaternius fish assets are unchanged.

Milestone 08: shifted the water slightly toward green, reduced the contrast of static sand ripples and grain, and strengthened the animated caustics on the sand and limestone. Sand caustics have a broader pattern and more focused highlights so the sunlight reads separately from the sediment texture. The Windows build passes; matching nursery and reef views were rendered and visually reviewed.

Milestone 09: added F11 borderless fullscreen and resizable windows, plus 84 grabbable nursery fragments. Aimed fragments show an edge outline and a G prompt. Picking another fragment destroys the previously held one; F5 restores the nursery. Laptop pickup uses an XRI selection hand slot, while simulator controllers can grab the same `XRGrabInteractable` objects directly. The Windows build, pickup/fullscreen runtime check and existing XR swimming/brush regression all pass, including cleaning all 24 patches.

Milestone 10: focused cleaning on one attached-algae spot, with white brush pickup feedback, persistent G pickup, hold-LMB scrub animation and a progress bar. Cleaning removes the coating and fronds progressively; H returns the tool and F5 restores the practice setup. Added two user-provided looping audio tracks with ambience emphasized over music. Windows build, the complete keyboard/mouse cleaning loop, audio playback state, native XR brush/swimming and coral pickup/fullscreen regressions pass. Headset and glove input remain untested.

Milestone 11: integrated the supplied original UI shapes with Nunito SemiBold/Bold, warm cream text and dark blue-gray panels. Cleaning uses an aligned arc track and tinted fill with a moving round cap. One context hint replaces the overlapping prompts; F1 opens controls and O folds objectives. A screen overlay keeps the interface clear of underwater distortion. Completion fades out and automatically folds the objective. Algae now clears from the receding coating edges inward, and a soft warm light marks the practice spot.

The Windows build and the complete desktop cleaning check pass, including asset/font references, F1/O toggles, pickup, occlusion/range rejection, physical brush motion, spatial growth removal, pause/resume, completion hiding, tool exchange and reset. The screenshots are captured from the visible player after rendering the HUD. Run the cleaning check in a visible window so Unity can capture the screen overlay.

Milestone 12: added Kenney keyboard/mouse and left/right Meta Quest Grip prompts, matching the active interaction mode. Desktop and simulator body mode use G and the left mouse button; controller mode uses the Grip icon for the interacting hand. The builder checks the XRI Select binding against GripButton. Brush release means releasing Grip, while coral retains its existing persistent hold behavior. Folded controls explain G as the simulator's Grip input. The original arc, panels and Nunito typography remain in place.

The supplied brush_scrub.wav now loops only during physical contact strokes. Idle holding, air movement and obstructed attempts are silent; releasing, leaving the surface, completing or resetting stops the sound. The source recording is unchanged.

Windows build and both desktop/XR simulator checks pass, including G/mouse glyphs, left/right Grip switching, native brush release, sound during contact strokes, silent idle/air movement and sound stopping on pause/completion. Quest hardware remains untested.

| Keyboard pickup | Right-hand Grip release |
| --- | --- |
| ![Keyboard G pickup](docs/previews/milestone-12-keyboard.png) | ![Right-hand Grip release](docs/previews/milestone-12-quest-grip.png) |

![Nursery frame and suspended fragments](docs/previews/milestone-08-detail.png)

Earlier milestone 04 interaction views:

![Brush close-up on the previous table-shaped rack](docs/previews/milestone-04-brushing.png)

| Before cleaning | After cleaning |
| --- | --- |
| ![Dirty rack](docs/previews/milestone-04-before.png) | ![Clean rack](docs/previews/milestone-04-after.png) |

## XR simulator and rack cleaning

The official simulator drives tracked head/controller input. The brush is an `XRGrabInteractable`; cleaning uses bristle contact and swept distance. The current practice loop has **one algae-covered area on the middle front rim**. Holding the brush still, waving it in the air, or moving it while unheld does not clean. The attached coating and short fronds disappear progressively, exposing the frame underneath.

For the laptop demo, use body/FPS mode or press **F2** for desktop controls. Aim at the brush on the small tray: its edges turn white and **[G] Pick up brush** appears. Tap G once to keep it in hand. Move within 1.35 metres of the algae and aim at it, then **hold LMB**. The brush makes small lateral and fore/aft strokes on the surface; actual bristle travel drives the progress bar. Release LMB to pause, then resume without losing progress. **H** returns the brush to the tray; **F5** resets the practice spot and tools. Switching camera modes also returns the brush. Grabbing a coral returns the brush, and grabbing the brush clears the held coral.

Algae tufts shrink away as the coating's edges recede inward, based on their positions rather than creation order. A soft, slightly warm point light helps the practice spot read. Completion feedback stays for 1.2 seconds, fades over 0.6 seconds, then stays hidden even when the brush remains held or the completed spot is targeted again. Resetting restores the growth and progress UI. The task light is editable under **Practice spot / attached algae → Soft task light**.

| Brush pickup | Cleaning in progress | Completed |
| --- | --- | --- |
| ![Brush pickup](docs/previews/milestone-11-brush.png) | ![Cleaning progress](docs/previews/milestone-11-cleaning.png) | ![Clean nursery rim after feedback fades](docs/previews/milestone-11-clean.png) |

These engine captures include the complete laptop HUD. The duplicate movement, inventory and simulator overlays have been removed. Controls start folded; press **F1** to open them. Press **O** to fold or expand the single objective.

| Input | Action |
| --- | --- |
| Tab | Cycle between body/FPS and device manipulation modes |
| `[` / `]` | Select the left / right controller for manipulation |
| Mouse | Rotate the active head/controller; right-click toggles mouse capture |
| WASD, Q / E | Move the body in FPS mode, or the selected controller in device mode |
| Shift / B | Boost / brake while swimming in FPS mode |
| Tap G in body/FPS mode | Pick up the aimed brush or coral |
| Hold LMB in body/FPS mode | Animate the held brush against the nearby algae |
| H in body/FPS mode | Return the brush to its tray |
| Hold G in controller mode | Grip with the selected controller; release G to drop the brush |
| F5 | Restore dirty patches, brush and all nursery fragments |
| F2 | Switch between XR simulation and the original desktop swimmer |
| F11 | Toggle borderless fullscreen / original window size |
| F1 | Open / close controls |
| O | Fold / expand objectives |

For manual controller practice, select the right controller with **]**, aim at the brush on the tray, then hold **G**. In device mode, move the brush with **WASD + Q/E**; rotate it until the bristles face down, touch the dirty rim and sweep sideways. Keep G held while brushing. F1 shows controls for the current mode. Repeated bracket presses can switch device types; this prototype is configured for controller simulation, not optical hand tracking.

The simulator remains explicitly included in Windows builds. Hardware XR/OpenXR provider setup, stereo rendering, headset performance, headset comfort and Rokoko live input are not validated. The screen-space HUD is for the laptop demo. Keyboard simulation and recorded/synthetic poses must not be presented as live glove tracking.

## Coral pickup and fullscreen

For the simplest laptop check, press **F2**, click to capture the mouse, swim close to a hanging fragment and aim the center dot at it. Its edges light up bright white and **G Pick up coral** appears within 3 metres of unobstructed view. Tap **G** to pick it up; it stays at the lower right of the view after releasing the key. Aim at another fragment and tap G again to replace it. The previous held GameObject is destroyed, and its former suspension line stays hidden until reset. Pressing G with no target keeps the current coral.

The XR simulator's body/FPS mode also supports center aiming and G. Center aim takes priority in this mode, so the same key cannot also pick a different coral with the simulated controller. In controller mode, use the existing controller ray or direct grip: native selection transfers to the same persistent hand slot when grip is released. Only one coral is retained across both hands and camera modes. Grabbing the brush clears the held coral; picking coral returns the brush to its rest. **F5** resets the brush, dirt, every fragment and suspension line. Coral placement is not implemented yet.

**F11** switches between borderless fullscreen at the display's resolution and the previous window size. Windows also supports resizing/maximizing the window and its normal Alt+Enter fullscreen shortcut.

| Aimed coral: edge highlight | Picked coral: single held item |
| --- | --- |
| ![Highlighted coral](docs/previews/milestone-09-highlight.png) | ![Held coral](docs/previews/milestone-09-held.png) |

These older engine views omit the HUD; the current HUD shows a single contextual pickup or replacement hint during play.

## Desktop controls (F2)

| Input | Action |
| --- | --- |
| Mouse | Look |
| WASD | Swim relative to view direction |
| Space / C | Rise / descend |
| Shift + movement | Sprint / stronger fin kick |
| Right mouse button or X | Active brake; overrides propulsion |
| Release movement | Coast with water drag |
| R | Reset to the starting point |
| Esc / left click | Release cursor / resume |

No gravity, head bob, camera roll or sprint FOV changes in desktop swimming. Change the public settings on **Diver / desktop input → DiveMotor** to tune the feel. `ReefXRSwim` moves the XR Origin with its own collider in simulator FPS mode; independent controller positioning remains the official simulator's responsibility.

## Editing the look and terrain

Open the **Art controls** panel from the **Reefkeeper** menu with `ShallowReef` open and Play stopped. This panel edits the actual scene lighting, fog, shared grading profile and materials; there is no runtime script that overwrites your art choices.

- Water color, visibility, sunlight, exposure and saturation preview immediately. Enable Scene view effects/fog/post-processing, or use Game view, to see the complete look.
- Select a material button to edit its normal Inspector, including Tint, Main Texture and Caustic Color. These are shared materials; duplicate and assign a material to a particular object if it should have its own look.
- Open `Assets/Reefkeeper/Generated/Water.mat` to adjust **Daylight through water**, **Broken sky reflections**, **Reflection strength** and **Ripple scale**. A smaller ripple scale makes broader patches. This lightweight ceiling shader gives an underwater impression of the surface; it does not simulate refraction of above-water objects.
- Adjust bank height, dunes, channel depth and detail, then use the apply-terrain button. This replaces only the seabed mesh/collider and moves registered props by the change in ground elevation; it preserves their placement offsets, materials and object identities. Undo is supported. Grounded prop references live on the seabed's `ReefSeabed` component; register new prop roots without also registering their children.
- Use the save-materials-and-scene button to save everything. Edits made during Play are disabled in this panel. **Create or rebuild shallow reef** is the original full generator and resets the scene; avoid it when working on your own art/layout. **BuildWindows** builds the saved scene without regenerating it.

The seabed is a mesh with smooth sand banks, dunes and a winding trough, not Unity Terrain/Landscape. The current models and flat textures are still placeholders; these tools prepare the scene for the user's realistic art direction rather than making the placeholder art photorealistic.

## Current scope

- A procedurally authored shallow reef with animated caustics, coral, limestone, a distant arch, fish, seagrass, particles and a water ceiling.
- Underwater caustics and subtle fullscreen distortion from aniruddhahar's MIT-licensed URP-WaterShaders. The original Shader Graphs are preserved; scene-specific materials and Unity 6 renderer configuration live in ReefSceneBuilder. Adjust `Generated/UnderwaterDistortion.mat` (`_Distortion`, currently 0.002) to change or disable distortion; rebuilding resets generated settings.
- View-relative swimming, boost, drag, active braking, collisions, a surface limit and an exploration boundary.
- Small depth/speed display and keyboard hints.
- Editable source assets and a reproducible scene builder. Generated assets live in `Assets/Reefkeeper/Generated`; save a copy of the scene before rebuilding if you have edited it by hand.

The current nursery uses simple project-authored geometry based on the supplied reference photographs. Select **WORKSHOP / coral nursery maintenance → Nursery tree / interactive** to edit the working tree; additional trees live under **NURSERY / suspended coral trees**. Keep the interaction components/contact points when replacing its visual meshes. All four trees have grabbable hanging fragments; placing nursery fragments is the next interaction. Swimming motion capture and a current-meter prop remain optional later additions.

## Soundscape

The two user-provided tracks play continuously as 2D loops: `Assets/Audio/Ambience/amb_underwater_loop.wav` at **0.75** volume and `Assets/Audio/Music/bgm_dream_culture.mp3` at **0.15**. The source recordings are unchanged; no trimming, effects or crossfades are applied. Unity streams them during playback. Select **WORKSHOP / coral nursery maintenance → ReefSoundscape** to adjust the two volume fields or replace the clips. Track provenance/licensing information was not included with the supplied files.

New interface assets can be placed in `Assets/UI`; interaction sound cues belong in `Assets/Audio/SFX`. Include source/license notes with supplied assets.

`Assets/Audio/SFX/brush_scrub.wav` is connected through **ReefBrushAudio** on the brush. Volume defaults to **0.35**, with partial spatial positioning (0.6) and no Doppler. It uses the same actual contact/movement result as cleaning, including native simulated-controller strokes. A short 80 ms grace bridges stroke turning points; release and lost contact stop immediately. The original ambience and music settings are unchanged.

Input icons live under `Assets/UI/InputPrompts`, with Kenney's CC0 license. F1 opens the icon-based controls. Body mode shows keyboard/mouse glyphs even while the XR simulator is enabled; controller mode shows left/right Grip glyphs for hover or selection. Quest art does not enable headset support or replace hardware validation.

The supplied original UI artwork is imported under `Assets/UI/Shapes`. Both arc sprites retain their full 512px canvas and share the same runtime rect. `Reefkeeper/UI Arc Progress` reveals the tinted layer from bottom to top and preserves its round moving end. The hint panel uses sliced borders of 80 / 52 / 80 / 52 pixels (left / bottom / right / top), with independent text. Nunito SemiBold and Bold live under `Assets/UI/Fonts/Nunito`, with their SIL Open Font License and source notes. `ReefCleaningHUD` owns the laptop interface; `ReefCleaningBuilder.PolishAndBuild` reapplies its import settings and references to the saved scene.

## Animated fish

Quaternius's CC0 Animated Fish Pack supplies seven rigged models and swimming clips. The scene uses 26 small fish across three species plus one manta ray. Their animation phases and speeds vary, and `ReefFishSchool` moves each group around an editable loop while maintaining clearance above the seabed. These are ambient paths; obstacle avoidance, predator behavior and a full Boids simulation are not implemented.

- Select **REEF / Quaternius animated fish** in the Hierarchy, then one of its school children. Move that child to shift the route; adjust **Orbit**, **Angular Speed** and **Height Variation** in its Inspector.
- Reusable prefabs, separate species materials and animation controllers live in `Assets/Reefkeeper/Generated/AnimatedFish`. Dolphin, shark and whale prefabs are included as optional assets and are not placed in this nursery scene.
- Original FBX files and their license live in `Assets/ThirdParty/QuaterniusAnimatedFish`. The previous procedural school is disabled in the scene and can be restored.
- Environment candidates are listed in [the stylized reef asset shortlist](docs/STYLIZED_REEF_ASSETS.zh-CN.md); those environment packs have not been imported.

## Mocap capture while the loaner gloves are available

Keep the original Rokoko recordings and ask the hardware desk which export permission is available. Export animation with finger bones (FBX suitable for Unity when available), together with the actor/rig and calibration information. Record slow and natural-speed takes, with a neutral lead-in and lead-out.

Suggested clips: relaxed hand, empty-hand fist/open, brush grip, repeated brushing, brush release, coral-fragment pinch, place and release, optional instrument grip/button press. Use the same prop grip placement each take. Record left/right consistently; name takes with hand, action and speed. Glove finger capture does not imply tracked arm or world-position motion: preserve the distinction when reviewing assets.

Recorded clips should remain reusable without the gloves; interaction events will be separate from hardware adapters. Today's live glove input and any recorded animation playback must be identified honestly in the demonstration.

## Validation

**Reefkeeper → Validate swimming math** checks speed limits, diagonal normalization, coasting, brake priority and drag across frame rates. The command-line runtime smoke check also exercises actual CharacterController collision, descent onto the uneven seabed and the surface limit, and captures four rendered views:

```text
Reefkeeper.exe -reef-smoke <absolute-output-directory> -screen-width 1600 -screen-height 900 -windowed
```

Build with `Reefkeeper.Editor.ReefSceneBuilder.BuildWindows` through Unity's `-executeMethod`, or the normal Windows Build Profiles workflow. Outputs are local under `Reefkeeper/Builds/Windows` and are not committed.

The opt-in XR check uses simulated keyboard actions for swimming, controller translation, grip and release, then replays precise tracked-controller poses to sweep the configured dirt patch. It checks stationary contact, air strokes, unheld motion, completion, reset and switching modes, and captures an overview, a brush close-up and before/after images:

```text
Reefkeeper.exe -reef-xr-smoke <absolute-output-directory> -screen-width 1600 -screen-height 900 -windowed
```

The fish check verifies actual skinned mesh deformation over time, looping clips, path motion, supported URP materials and sampled seabed clearance. It captures portraits of all four placed species and a habitat view:

```text
Reefkeeper.exe -reef-fish-smoke <absolute-output-directory> -screen-width 1600 -screen-height 900 -windowed
```

For repeatable art review, `-reef-art-capture <absolute-output-directory>` saves three engine renders: nursery overview, frame detail and the reef with fish. These offscreen views omit the HUD and simulator controller models. The local mood-board photographs remain reference material under the ignored `artifacts/moodboard` directory; they are not packaged as game textures.

The pickup check exercises keyboard G, hover feedback, solid occlusion and reach, single-item replacement/destruction, empty-aim behavior, reset, exclusive FPS center-aim input, native simulated controller grip, persistent holding and camera mode changes. It also switches F11 fullscreen on and off and verifies restoration of the original window size:

```text
Reefkeeper.exe -reef-pickup-smoke <absolute-output-directory> -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

The cleaning check drives real keyboard/mouse input for brush pickup, persistent holding, occlusion/range rejection, pause/resume, physical contact animation, progress UI, gradual growth removal, completion, returning the brush, coral/tool exclusion and reset. It also verifies both audio sources are playing and loop, with music set below ambience:

```text
Reefkeeper.exe -reef-cleaning-smoke <absolute-output-directory> -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

## Credits

Attached algae uses project-authored short fronds and coating geometry, with Rob Tuytel's [Moss Wood](https://polyhaven.com/a/moss_wood) diffuse and normal maps from Poly Haven ([CC0](https://polyhaven.com/license)). The moss scan is an artistic placeholder for marine growth. Provenance and other reviewed candidates are in `Assets/ThirdParty/PolyHavenMossWood/README.md`.

Unity's bundled Universal 3D template supplies the URP render settings. Reef geometry, movement, interface and supporting shaders were authored during this hackathon with Codex assistance. Underwater Shader Graphs are by **aniruddhahar**, [URP-WaterShaders](https://github.com/aniruddhahar/URP-WaterShaders), MIT, pinned to `1d18105ab76f894e3187d32bb4e412dbcb35df2c`. Source, license and integration notes are in `Reefkeeper/Assets/ThirdParty/URP-WaterShaders`; the license is also copied alongside Windows builds.

XR rig, controller models, input actions and simulator assets come from Unity's **XR Interaction Toolkit 3.3.2** samples, under the Unity Companion License; see `Reefkeeper/Assets/Samples/XRI-LICENSE.md`. Sample controller materials are overridden with project URP materials. The underwater reef and nursery props remain project-authored placeholders.

Animated fish are by **Quaternius**, [LowPoly Animated Fish](https://quaternius.itch.io/lowpoly-animated-fish), CC0 1.0. Original files were downloaded from [the creator's OpenGameArt release](https://opengameart.org/content/animated-fish). Their license and provenance are preserved in `Assets/ThirdParty/QuaterniusAnimatedFish`; the license is also copied alongside Windows builds.
