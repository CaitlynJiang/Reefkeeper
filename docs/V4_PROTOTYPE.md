# V4 — Shipwreck habitat MVP / 沉船栖地原型

Branch: `v4-prototype`, based on `origin/v3` at `df686a8` (includes the V3 journal and Xinyu's hand tracking work).

## Quick playtest

1. Open `Reefkeeper/Assets/Reefkeeper/Scenes/ShallowReef_V4.unity`, or **Reefkeeper > V4 > Open habitat prototype**, and press Play. Editor preview starts in desktop mode near the shipwreck; F2 toggles the XR simulator while the journal is closed.
2. J opens the journal. **CORALS > Get TEST KIT** grants two Branch Coral, one Mushroom Coral and two Lemon Tube Sponge. This is an explicitly labelled shortcut, usable once per V4 profile.
3. Click a collected coral to select it and close the journal. Look at a mint-colored shipwreck pad: a transparent mature coral previews the result. Press G to snap it into place. Three slots are on the near section and two at the far end of the wreck. All five use the existing authored wreck positions.
4. Look at an occupied slot and press G to return its coral. Selecting or cancelling does not spend stock; successful placement spends exactly one. Esc cancels the selected coral. If the cursor is unlocked afterward, click to resume swimming.
5. **MARINE** is the encyclopedia: left-side details/recipe, right-side selectable entries. Undiscovered entries show a question mark; discovered entries show photos captured from the existing fish assets.

| Combination, anywhere across the five slots | Visitor |
| --- | --- |
| 2 Branch Coral + 1 Mushroom Coral | Reef shoal (`Fish1.prefab`) |
| 2 Lemon Tube Sponge + 1 Mushroom Coral | Striped reef fish (`Fish3.prefab`) |

These are fictional MVP gameplay recipes, not scientific habitat claims. Both can coexist with all five test-kit corals placed. A matching combination unlocks an animated school near the wreck and a permanent encyclopedia record. Visitors stay after discovery, even if the layout is changed.

## Full nursery loop

Grow a nursery coral through the existing cleaning/fertilizing loop. At full maturity, point at it and click LMB (controller trigger / tracked-hand pinch in VR) to harvest it into the collection. Harvesting frees its nursery socket. An immature or dirty coral cannot be harvested, and a mushroom supporting another coral must have its dependents harvested first. All 11 existing coral/sponge types can be collected and displayed; only the three recipe ingredients currently contribute to unlocks.

## Controls and saves

- Desktop: J journal; Esc close/cancel; RMB look around with the journal open; LMB journal selection/doodles or harvest; G place/retrieve on the wreck. Movement remains locked while reading the journal.
- Controller: left Y journal, right B close; point and pull the trigger to harvest/place/retrieve. Tracked hands use the existing palm-menu journal and pinch-to-act rays; journal buttons also accept fingertip touches. Hardware comfort and reach remain unverified.
- MVP placement uses collection selection plus a ray-targeted snap preview. Physically carrying a harvested coral and releasing it onto the wreck is a later interaction refinement.
- V4 inventory, five placements, unlocks and test-kit state save locally under `Reefkeeper.V4.Habitat.1`. **NOTES > Reset V4 save > Confirm RESET** clears only this prototype's progress. F5 retains its existing nursery reset behavior.
- V3 scenes remain unchanged. The V4 builder can recreate the dedicated scene from V3; use it only when intentionally discarding edits to the generated V4 scene.

## Validation and remaining work

Run `ReefV4SmokeCheck.Run()` in Play Mode in the V4 scene. It exercises real journal mouse input and G placement, inventory conservation, invalid placements, both recipe unlocks, visitor spawning, rearrangement, isolated save/reload/reset, and the existing planting/fertilizing/harvest flow. Results and screenshots go to ignored `artifacts/v4-check`. Tests use their own save key and restore input settings.

Validated in Unity 6000.3.19f1: compilation and **31 Play Mode integration checks passed**. The decorated wreck, collection and locked/unlocked encyclopedia were also inspected in-engine. Physical headset interaction has not been tested.

Remaining: headset validation, physical carry/release placement, better empty/occupied-slot feedback, additional recipes/species, polished discovery animation/audio, and broader encyclopedia content. Fish entries currently use descriptive prototype names. Native V4 Android build configuration is not included yet.

## 中文速览

打开 **ShallowReef_V4** 后 Play，默认 PC 模式，出生在沉船附近。按 **J → CORALS → Get TEST KIT** 领取一次测试用成熟珊瑚；点选珊瑚，瞄准薄荷色位置，按 **G** 吸附摆放。对已占位置再按 G 可取回。沉船前部 3 个位置、后部 2 个位置。

组合一：**2 分枝珊瑚 + 1 蘑菇珊瑚**；组合二：**2 柠檬管海绵 + 1 蘑菇珊瑚**。解锁后有真实鱼群绕沉船游动，**MARINE** 图鉴记录发现和组合提示；改变布置不会收回发现。收藏、布局和解锁自动保存，NOTES 中二次确认可重置 V4 存档。

正常流程仍可从苗圃养成：成熟且干净后，瞄准并单击收获，进入收藏。所有 11 类现有珊瑚／海绵都能收集和摆放。第一版用“收藏选中 + 指向吸附”验证玩法，真实拿着搬运、松手摆放及 VR 真机体验留待下一轮。

## Observation cabin checkpoint / 观景舱版本记录

**Added:** a fixed, low-poly observation cabin above the forward shipwreck deck, offset and turned to avoid the existing rigging inside the room. Includes an open side entrance, bench, floor viewing window, moulded frame, opaque edge glints and deck moorings. The independent prefab is `Assets/Reefkeeper/Generated/ObservationCabin/ObservationCabin.prefab`; regenerate it and its V4 placement with **Reefkeeper > V4 > Build observation cabin model** in Edit Mode with the V4 scene saved.

- 3,404 triangles; six opaque material/render groups. This is geometry cost, not a headset frame-rate guarantee. Shadow passes can add rendering work beyond the six groups.
- No rendered transparent panes, refraction, realtime reflections, extra cameras or realtime lights. Window and floor collision are independent from the visible meshes.
- Outer footprint 4.2 × 3.8 m; interior approximately 3.9 × 3.5 m before furniture; ceiling clearance 2.6 m. Entrance clearance 1.3 × 2.22 m. Floor viewing opening 2.6 × 1.05 m.
- Six editable material colors: cream frame, lagoon shell, honey cushions/accents, deep teal fittings, ivory glints and warm lamp lenses. Cushions and yellow fittings currently share one material. Lamp lenses do not cast light.
- Door is parked **open** and has no interaction yet. Swim through using existing movement. No gravity/buoyancy switching, airlock, seated mode or cabin bobbing was added. The bench is scenery, not physical support.
- Validated in Unity 6000.3.19f1: builder compiled; five cabin checks passed (opaque materials, no extra cameras/lights, solid observation floor, 1.8 m tall / 0.6 m wide controller entering and exiting). Exterior, room and floor-view screenshots were inspected. These controller-proxy checks do not validate tracked headset movement or comfort.
- Screenshots in local ignored `artifacts/cabin-*.png` are actual Unity captures. A temporary test-kit layout was used for the photos and the player's saved progress was preserved. The earlier 31 habitat checks belong to the habitat checkpoint, not a newly repeated whole-project test suite.

**中文：**新增悬浮于沉船前甲板上方的低模观景舱，侧门常开，可沿用游泳控制进出；包含长椅、地板观景区、窗框高光及固定缆绳。整舱 3,404 个三角面、6 组不透明材质，颜色可在材质里调整。没有新增透明玻璃、实时灯光或摄像机。已通过 5 项模型／碰撞检查，真机性能和舒适度尚未验证。暂未做开关门、坐下模式或浮力／重力切换。

## Review backlog / 回来一起讨论的修改建议

These are proposals and code-review findings, **not features implemented by this checkpoint**. Priorities reflect the current MVP, not a commitment to expand scope. The user reported about 30 FPS on a previous PICO build; the device model, package/version and CPU/GPU timings remain unknown. The fast desktop Editor samples do not override that device result.

| Priority | Current evidence / gap | Proposed next change and acceptance check |
| --- | --- | --- |
| P0: correct device build and performance | The committed `ProjectSettings/EditorBuildSettings.asset` enables **ShallowReef_V2**, not V4. A separate active Build Profile could override it and must be checked. Android-only rendering optimizations do not run in the Editor. | Create/verify an explicit V4 device build, record device, commit, refresh rate, render scale and AppSW state. Measure CPU/GPU frame times and dropped/long frames along the same nursery → wreck → cabin → journal route; test a sustained session. First establish stable target-device performance before increasing visual cost. |
| P1: one understandable first session | V4 starts near the wreck with a TEST KIT shortcut; the full care → harvest → collection → place → discovery sequence exists but is not a guided first-session flow. | Keep TEST KIT as a clearly labelled developer shortcut. Give a first-time player one small visitor goal, explain why a tool is wrong and when a coral is ready, and celebrate the first arrival. Test whether a new player can complete one loop without verbal help. |
| P1: actual-hand input ownership | `ShipwreckHabitat.Update` checks the left ray first and breaks when `Aim` finds a target even if that hand did not press. A hovering left hand can therefore suppress a right-hand action. Direct selection cancellation currently checks keyboard Esc; the journal's cancel button is the VR fallback. | Resolve the hand that actually pressed first, separate hover from action ownership, and provide an explicit controller/hand cancel action. Test opposite-hand hovering, both hands aiming, leaving UI with a held trigger and working while holding a tool. |
| P1: clear and reversible placement | Five fixed slots, ray selection within 5 m, same trigger retrieves an occupied slot. No manual rotation or physical carry/release. | Improve chosen/empty/occupied/out-of-range feedback and a short placement/return sound or haptic. Guard against unintended retrieval; allow easy undo. Keep snap placement for this MVP rather than expanding into free construction. |
| P1: meaningful combinations | All 11 species can be displayed, but only three contribute to two recipes. Both recipes fit simultaneously in five slots; only species counts matter, not their spatial arrangement. Unlocks and visitors remain after the combination is removed. | Decide whether this is a permanent discovery collection or a living habitat. Suggested direction: permanent encyclopedia entries, while current visitors reflect the current habitat with a gentle delay. Keep decorative species valid; explain their role or later add a few simple habitat traits. Do not punish rearranging a garden. |
| P1: complete session persistence | The V4 save stores collection, wreck placement, discoveries and TEST KIT state. Current nursery care/planting state has no equivalent save path in the reviewed scripts. | Save the nursery species, sockets, growth progress and dirt state alongside habitat progress; restore them consistently. Decide explicitly whether growth advances while away. Test quit/relaunch halfway through caring and after harvesting. |
| P1: readable, reachable journal | The card spawns at a fixed offset in front of the camera without a cabin-clearance query. Marine captions use small text, and hand layout is physically smaller. | Test opening beside the door, window, ceiling and bench; keep the card visible/reachable without intersecting the room. Check smallest text, hit targets, seated/standing users and both hands on the headset. Preserve real head tracking while menu locomotion is locked. |
| P1: cabin comfort and collision | Only a generic controller proxy was tested. Headset physical movement is different from moving the controller capsule. The floor has collision but no rendered pane; the apparent opening may feel like a hole. | Test actual leaning, crouching, head/hand clipping, entry from different heights, seated eye level, and a clear way out. Use edge cues/soft visual feedback if needed; do not forcibly freeze or shove the tracked head. Reposition the floor view so looking at the garden does not require sustained extreme downward neck angles. Keep swimming and a stationary cabin for now. |
| P1: swimming precision | XR already has 45° snap turning. Controller swimming follows gaze, retains inertia and uses the other stick axis for ascent/descent; controller input does not currently expose the desktop brake. Open-hand extension can activate gliding. | Test low-speed approaches and stopping at a cleaning target/doorway; consider an accessible brake/precision mode. Check that pointing or reaching does not unexpectedly start hand gliding, and that turning is not mistaken for vertical movement. These are comfort options, not a proposed gravity switch. |
| P1: visitors as a reward | Fish follow authored elliptical loops; `ReefFishSchool` has no general obstacle avoidance. The new cabin can intersect a visitor path. Placement/retrieval currently rebuilds all displayed corals and unlocked schools. | Author inexpensive paths around the cabin and ship, giving good seated and standing views. Add a gentle arrival cue and short discovery moment. Update only changed slots/schools to avoid unnecessary respawns and potential action-time hitches; profile those transitions separately. |
| P2: presentation polish | Cabin has flat prototype materials and static open door; the encyclopedia has two descriptive visitor entries. | Tune the six-color palette, make invisible-pane boundaries understandable, add species content and shop identity after the core loop/comfort checks. Door controls can follow; defer free building, heavy glass effects, damage/fire mechanics and gravity switching. |

### 建议先做的顺序

1. **真机准备与性能**：确认打包的是 V4，记录机器和版本，先分清 CPU／GPU 瓶颈。上次实机约 30 帧的问题优先于继续加视觉效果。
2. **让第一轮玩法讲得通**：清理 → 培育 → 收获 → 摆放 → 第一群鱼到来 → 图鉴记录 → 进舱观赏。给玩家一个小目标和明确反馈，测试包的 TEST KIT 不替代正式引导。
3. **先修操作，再加物种**：双手指向优先级、VR 取消摆放、误取回、低速停稳与卡片在舱内的位置。现有 45° 转向已经有，不必重复造一套。
4. **决定布置的意义**：图鉴发现永久保留；是否让当前鱼群随组合变化，是下一轮最值得讨论的设计选择。其余珊瑚也可以纯粹用于好看，不必都变成配方材料。
5. **补齐连续游玩的基础**：苗圃养成存档、重新进入游戏后的引导、鱼群绕开船舱、清楚的到访反馈。

**下周真机重点：**持续帧率；刷／刮时能否停稳；左右手是否抢操作；放置／取消／取回是否直觉；潜水卡小字和按钮；舱门通过与头部穿模；低头观景是否累；坐姿／站姿视野；手追踪丢失后是否误游动；退出重开是否丢养成进度。不要把碰撞代理通过当作真实 VR 舒适度验收。

For general locomotion/comfort design, see [Meta's locomotion overview](https://developers.meta.com/vr/design/locomotion-overview/) and [physical locomotion / virtual-environment considerations](https://developers.meta.com/vr/design/locomotion-virtual-environments/). These inform proposed tests; they are not evidence that this prototype has passed them.
