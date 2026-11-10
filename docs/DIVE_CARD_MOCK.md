# 3D dive-card study / 潜水卡原型

Enter Play Mode normally in the V3 workshop. The card is available immediately and starts closed: J / left controller Y toggles it; Esc / right controller B closes it. No preview-menu setup is required. **Reefkeeper > V3 > Preview 3D dive cards** remains an optional shortcut to open it. Click a tab, or point the right controller and press its trigger, to change selection.

The XR Interaction Simulator's keyboard joystick-left bindings use **Left Arrow** instead of J, reserving J for the journal. I / K / L retain their simulator up / down / right mappings. The actual headset controller mappings are unchanged.

The PVC card face is 25% larger than the initial study, with unchanged thickness and binding-ring size. Sky-blue/lake-blue PVC, alternating warm yellow/apricot tabs, a cream selected tab, faint water/bubble print, and a Lucky Cat shop stamp establish the visual direction. The stamp comes from a capture of the existing LuckyCat prefab; see the resource README for provenance.

Hold the left mouse button and drag on the face to doodle in black; a click leaves a dot. The native XR pointer uses the right trigger for the same drawing path (not headset-tested). Released strokes stay for 3 seconds and fade over 1.5 seconds. Drawing is bounded to the face; changing tabs or closing clears it. The tethered pencil remains decorative in this prototype. Ink is temporary and is not saved.

Opening the card freezes swimming and cleaning, hides the held tool, and fixes the card in world space. In desktop mode, hold RMB and move the mouse to look around; release RMB to return to the UI pointer and LMB drawing. Looking suppresses drawing and tab clicks. The card stays at its original world pose. Closing restores swimming and the held tool. Real headset pose tracking stays active, with stick turning disabled; only the editor interaction simulator is suspended. The card has no colliders. This does not prevent nearby scenery from visually intersecting it.

Coral stickers are 35 transparent captures of all growth stages across the 11 existing coral/sponge prefabs, with white silhouette borders. Three decorate the card corners; CORALS shows the four BranchCoral stages. The center of the guide remains available for doodling. Sources are listed in `Assets/Reefkeeper/Resources/DiveCards/CoralStickers/README.txt`. Re-capture using **Reefkeeper > V3 > Capture coral stage stickers**; the original models/materials are retained. Stages are framed individually for clarity, so sticker size is not a physical growth-size comparison.

This is a local visual prototype: tabs change highlight/title and the coral tab displays a sample growth strip. Full encyclopedia content, final tab taxonomy and headset usability validation remain pending. Desktop and simulator checks cover fixed placement, input locking/restoration, quick reopen, RMB look without swimming, drawing/fade and mouse tab selection; no physical headset test has been performed.

中文：正常点 Play 即可使用；J 呼出／收起，Esc 关闭，鼠标点标签切换。模拟摇杆向左改用 ←，避免与 J 冲突。板面放大 25%，底色偏蓝；打开时锁定移动和清理，PC 按住右键可环顾，松开后恢复指针，板子位置固定。VR 保留真实头部追踪，禁用摇杆转向。Lucky Cat 印章及珊瑚白边贴纸都来自游戏已有模型实拍；11 种珊瑚／海绵共 35 个阶段已拍摄，首页贴 3 张、珊瑚页展示分枝珊瑚的 4 个阶段。按住鼠标左键在板面拖动可涂鸦，松开后保留 3 秒、淡出 1.5 秒；翻页或收起清空。绑笔暂为装饰。当前仍是视觉原型，完整图鉴内容和真机体验待完善。
