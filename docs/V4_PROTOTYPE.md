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
