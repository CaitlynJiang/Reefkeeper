# V3 prototype — 2026-11-10

## English

**Implemented**
- A 3D blue PVC dive-card journal with binding rings, tethered pencil, selected-tab colors and a Lucky Cat stamp captured from the existing model.
- J / left Y opens the card during normal play; Esc / right B closes it. Swimming stops while reading, desktop RMB allows looking around, and the card remains fixed in world space. Real VR head tracking remains active. Simulator joystick-left moves from J to Left Arrow.
- Temporary black doodles: hold LMB and drag, then strokes hold for 3 seconds and fade over 1.5 seconds. Right-trigger drawing is wired for VR, pending hardware validation.
- White-border stickers captured from 35 growth stages across 11 existing coral/sponge prefabs. Three decorate the card; the coral tab previews four BranchCoral stages. A capture menu supports regeneration after asset updates.
- Two cleaning tools: brush for algae, file-shaped scraper for fire coral, with wrong-tool feedback.
- Pale, curved fire coral on the practice bench and coral trees, plus contact-driven chips that fly off and fade while scraping.
- A standalone five-slot pebble tool menu with actual in-game tool captures. First pickup unlocks each tool, the tutorial table keeps its display, and selecting a slot equips and closes the menu.
- The menu stays hidden until summoned, with a short slide/pop/fade animation. The lower-left Tab Tools / Esc Close prompts reuse the existing key-icon style; Close appears only while open.
- Tool slots have no outline; selection changes their fill color. The yellow Tools pebble has a curved highlight and a soft shadow.
- Tap Tab / right-controller A to toggle; Esc / right-controller B to close. Swimming continues while the menu is open. Simulator device cycling moves to backquote.
- Coral care cues indicate blocked growth, cleaning feedback, completed cleaning and readiness for fertilizer.

**Validation**
- Unity compilation and 85 Play Mode integration assertions passed. Desktop and simulated controller input were exercised; physical PICO hardware has not been tested.
- Targeted checks passed for normal-startup J shortcuts, control locking/restoration, fixed card placement, RMB look without swimming, mouse drawing/fade, tab selection and loading all 35 sticker textures.

**TODO**
- Populate the journal with tips, complete coral / marine-life encyclopedia content and credits; finalize tab organization and VR pointer feedback. The current pages are visual prototypes with a sample growth strip.
- Refine the brush model and UI details; update captured icons whenever models change.
- Add the remaining three tools, including the planned fertilizer tool, and persistent inventory saving.
- Test pointer reach, readability, comfort and performance on a PICO headset; prepare a V3 build configuration (the existing build menu still targets V2).
- Later stretch goals: fire-coral flame/damage behavior, fish-attraction unlocks and construction / observation platforms.

## 中文

**本版完成**
- 蓝色 3D PVC 潜水卡：环扣、绑笔、标签选中色，以及游戏已有 Lucky Cat 模型实拍印章。
- 正常运行即可按 J／左手 Y 呼出，Esc／右手 B 收起。读板时停止游泳，PC 按住右键可环顾，板子固定在世界中；VR 保留真实头部追踪。模拟摇杆向左由 J 改为 ←。
- 黑色临时涂鸦：按住鼠标左键拖动，松开后保留 3 秒、淡出 1.5 秒。VR 右扳机绘画已接入，待真机验证。
- 实拍 11 种已有珊瑚／海绵的 35 个生长形态，制成白边贴纸。板面装饰 3 张，珊瑚页展示分枝珊瑚 4 个阶段，并提供重新拍摄菜单。
- 双工具清理：刷子清海藻、锉刀形刮刀清火珊瑚，并提示工具用错。
- 浅色、弯曲的火珊瑚已布置在练习台和珊瑚树上；有效刮削时会飞出并淡出珊瑚碎屑。
- 独立五槽鹅卵石工具菜单，图标取自游戏实际模型截图。首次拾取解锁，教学桌保留展示，点击槽位直接装备并收起菜单。
- 菜单默认隐藏，呼出时滑入、轻弹并淡入，关闭时收回。左下角 Tab Tools／Esc Close 沿用现有按键图标，Close 只在菜单打开时显示。
- 工具槽去除描边，用底色区分选中状态；黄色 Tools 鹅卵石增加弧形高光与柔和阴影。
- 点按 Tab／右手 A 开关菜单，Esc／右手 B 关闭。菜单打开时仍可游泳；模拟器切换设备改为反引号键。
- 珊瑚养护表情提示生长受阻、清理反馈、清理完成和可施肥状态。

**验证**
- Unity 编译及 85 项 Play Mode 集成检查通过，覆盖桌面和模拟手柄操作；尚未进行 PICO 真机测试。
- 定向检查通过：正常启动后 J 快捷键、控制锁定／恢复、板子固定、右键环顾但不能游泳、鼠标涂鸦／淡出、标签切换及全部 35 张贴纸加载。

**TODO**
- 补齐潜水卡操作说明、珊瑚／海洋生物图鉴和 Credits，确定最终标签分类及 VR 指针反馈；目前页面仍为视觉原型，含一组生长阶段示例。
- 优化刷子模型与 UI 细节；模型改变后同步重拍图标。
- 增加另外三个工具（含计划中的施肥器），以及背包存档。
- PICO 真机验证指针触达、可读性、舒适度和性能；配置 V3 构建入口（当前构建菜单仍指向 V2）。
- 后续 stretch goals：火珊瑚喷火／伤害、吸引鱼类并解锁图鉴、建造与观景台。
