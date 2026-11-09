# V3 prototype — 2026-11-09

## English

**Implemented**
- Two cleaning tools: brush for algae, file-shaped scraper for fire coral, with wrong-tool feedback.
- Pale, curved fire coral on the practice bench and coral trees, plus contact-driven chips that fly off and fade while scraping.
- A standalone five-slot pebble tool menu with actual in-game tool captures. First pickup unlocks each tool, the tutorial table keeps its display, and selecting a slot equips and closes the menu.
- Tap Tab / right-controller A to toggle; Esc / right-controller B to close. Swimming continues while the menu is open. Simulator device cycling moves to backquote.
- Coral care cues indicate blocked growth, cleaning feedback, completed cleaning and readiness for fertilizer.

**Validation**
- Unity compilation and 85 Play Mode integration assertions passed. Desktop and simulated controller input were exercised; physical PICO hardware has not been tested.

**TODO**
- Build the PVC dive-card journal: tips, coral / marine-life encyclopedia, credits and temporary doodles.
- Refine the brush model and UI details; update captured icons whenever models change.
- Add the remaining three tools, including the planned fertilizer tool, and persistent inventory saving.
- Test pointer reach, readability, comfort and performance on a PICO headset; prepare a V3 build configuration (the existing build menu still targets V2).
- Later stretch goals: fire-coral flame/damage behavior, fish-attraction unlocks and construction / observation platforms.

## 中文

**本版完成**
- 双工具清理：刷子清海藻、锉刀形刮刀清火珊瑚，并提示工具用错。
- 浅色、弯曲的火珊瑚已布置在练习台和珊瑚树上；有效刮削时会飞出并淡出珊瑚碎屑。
- 独立五槽鹅卵石工具菜单，图标取自游戏实际模型截图。首次拾取解锁，教学桌保留展示，点击槽位直接装备并收起菜单。
- 点按 Tab／右手 A 开关菜单，Esc／右手 B 关闭。菜单打开时仍可游泳；模拟器切换设备改为反引号键。
- 珊瑚养护表情提示生长受阻、清理反馈、清理完成和可施肥状态。

**验证**
- Unity 编译及 85 项 Play Mode 集成检查通过，覆盖桌面和模拟手柄操作；尚未进行 PICO 真机测试。

**TODO**
- PVC 潜水卡日志：操作说明、珊瑚／海洋生物图鉴、Credits 和自动消失的涂鸦。
- 优化刷子模型与 UI 细节；模型改变后同步重拍图标。
- 增加另外三个工具（含计划中的施肥器），以及背包存档。
- PICO 真机验证指针触达、可读性、舒适度和性能；配置 V3 构建入口（当前构建菜单仍指向 V2）。
- 后续 stretch goals：火珊瑚喷火／伤害、吸引鱼类并解锁图鉴、建造与观景台。
