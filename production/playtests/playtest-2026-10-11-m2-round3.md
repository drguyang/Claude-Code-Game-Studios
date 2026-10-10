# Playtest Report — sprint-05 第三轮人工 playtest

> **Sprint**:sprint-05(Phase B T2.1–T2.5 施加后)
> **Build**:工作树 `main`(T2.1–T2.5 + UI/Click 修复 + 脉案右栏布局修复 + USS type:3 引用修复后)
> **上游**:playtest-2026-10-10-m2-round2.md(第二轮,5A;术前 T2.x 未开)
> ⚠️ 本报告**不替代任何 Logic/Integration 门** —— 自动化判据见各 wiring 测试。
> 模板权威 = `.claude/skills/playtest-report/SKILL.md`(9 节)。

## 1. Session Info

- **Date**:2026-10-11
- **Build**:工作树(Unity Editor `6000.3.24f1` Play 模式;T2.x 后首轮人工)
- **Duration**:约 5 分钟(人工操作;答题面 Q1–Q5)
- **Tester**:dr_guyang(用户)
- **Platform**:PC(Linux 桌面机,Unity Editor)
- **Input Method**:KB+M(键盘 WASD,方向键并入)
- **Session Type**:Targeted test —— 第三轮,覆盖 Phase B:
  **拟物 UI 呈现(脉案页)· 空行纪律 · 世界+UI 同屏 · Console 健康 · 退出**

## 2. Test Focus

| # | 面 | 上轮状态 | 本轮判据 | 结果 |
|---|---|---|---|---|
| 1 | 脉案页可见(右栏布局) | **B**(「?」落第 7 行非右列) | 六行在左(面色…问诊)、病名+「?」在**右栏**、纸面/格线/文字正常 | ✅ A |
| 2 | 六行空行纪律 | ✅ | 读数区空、只有行头、零灰字/占位/徽章/数字 | ✅ A |
| 3 | 世界 + UI 同屏 | ✅ | 地面+玩家+病人胶囊 + 脉案页同屏 | ✅ A |
| 4 | Console 健康 | ✅ | 无红错、无刷屏;新 UI/Click 警告 0;一次性护栏告警 1(见 §6) | ✅ A |
| 5 | 退出 Play | ✅ | 正常退出 | ✅ A |

**5A 全绿** —— 本轮是 sprint-05 Phase B 后首轮人工面,四形态件 + 值班 UI 呈现全部人工可观测。

## 3. First Impressions(前 5 分钟)

- **Understood the goal?**:Partial —— 世界里有病人 + 现在有一张脉案纸
- **Understood the controls?**:Yes —— WASD/方向键移动(方向键并集实测生效)
- **Emotional response**:未口述(灰盒,零内容反馈)
- **Notes**:
  - 启动即见脉案纸页:六行(面色/语声/姿态/呼吸/触感/问诊)在左,**病名 + 「?」在右栏**(上轮「?」落第 7 行的 B 已修复)
  - 六行读数区空、只有行头(空行纪律人工确认 —— 禁假数据面)
  - 3D 世界(地面+玩家+病人胶囊)与脉案页同屏
  - **按 WASD 时玩家有位移**(上轮「xyz 不变」系 UI 首载卡帧吞窗,见 §6)
  - Console:无红错、无 `UI/Click` 警告;仅一条一次性死亡螺旋护栏告警(§5)
  - 正常退出 Play

## 4. Gameplay Flow

### What worked well

- 启动序完整 → 病人出现(胶囊)→ 脉案纸页呈现(拟物 UI 首载 1.6s 一次性卡顿后稳定)
- 脉案六个通道行只读呈现,空行纪律人工可见(无灰字/占位/徽章/数字)
- WASD / 方向键移动正常(虚拟键测 4/4 实证位移链 + 真机人工确认)
- 退出 Play 正常

### Pain points

- 拟物 UI 首载 1.6 秒一次性卡顿(死亡螺旋护栏告警,§5)—— Low,一次性,护栏兜底

### Confusion points

- 上轮「WASD 按了 xyz 不变」被这轮解释清楚:不是移动断,是① 首载卡帧把按键窗口吞掉(护栏丢弃 1.6s 余量);② 高帧率下逐帧位移 0.0033s×5m/s ≈ 0.017m/帧,Inspector 肉眼难追

### Moments of delight

- 无(灰盒,零内容反馈)

## 5. Bugs Encountered

| # | Description | Severity | Reproducible | 处置 |
|---|-------------|----------|-------------|------|
| 1 | ←上轮 B 「?」落第 7 行(纵向堆叠) | Medium(布局) | Yes(上轮) | **本轮已修**:UI Toolkit 默认 flex 纵向 ⇒ five-channels 与 right-column 上下堆叠;补**匿名** row 容器(左右并排;匿名 = 不进双真源 name 序断言)。回归 EditMode 239 全绿 / casebook render PlayMode 11/11 全绿 |
| 2 | `UI/Click not found`(Input System UI 运行集成缺必需动作) | Low(每次 Play 一条) | Yes | **已修**(A 案契约修订):`InputSystem_Actions.inputactions` UI map 补 `Point/Click/ScrollWheel/MiddleClick/RightClick` 5 个运行时必需动作 + 三套设备组绑重;GDD 规则二注「运行时必需层」;`action_asset_identity_test` 断言两层齐全。InputSystem 过滤 276/276 绿 |
| 3 | **Boot.unity 的 `_styleSheets` 引用错版**(手写 `type:2` 无效 ⇒ 9 份 USS 全部 Failed to load) | High(样式表全不上 → 裸文本 UI) | — | **已修(A 案补钉)**:一次性脚本测试让 Unity 自生成 `{fileID:负哈希, type:3}` 正确引用,重载断言 9 份全部可解析后删除。本轮 Q4 确认 0 警告 |
| 4 | WASD「xyz 不变」→ 定性误报 | — | — | 本轮解释:CAR 真机探针 `axis=(0,-1) pos=(0,1.08,0) deltaTime=0.0033 grounded=True` = **输入已读、贴地、帧时正常** ⇒ 移动链健康;卡帧吞窗 + 高帧率逐帧位移微不可见。虚拟键测 4/4 实证位移(见 §7) |
| 5 | **UI 首载卡帧 1.6s**(死亡螺旋护栏:单次推进上限 5 tick,余量 1.6183s 丢弃) | Low(一次性,护栏兜底) | 首次载 UI 面板 | 登记非阻塞:UI Toolkit 面板首载(9 份 USS + 纹理烘焙)的一次性启动成本;护栏已丢余量保 Editor;后续静默 |

## 6. Feature-Specific Feedback

### 拟物 UI 呈现(sprint-05 T2.1–T2.5)

- **Understood purpose?**:Yes —— 无 HUD 下状态到底怎么「看见」:脉案是纸,不是面板
- **Found engaging?**:未口述
- **Suggestions**:值区当前空行(9 侧词表未接线,禁假数据)—— 读数出现是下一里程碑的面;样式表加载序(.ink-wet × .channel-reading 同写 color)已登记为未裁冲突,焦点接线前不可观测

### 空行纪律(**禁假数据的可见面**)

- 六行只读,空行 = 显式「我还没做」。确认人工面**没有**灰字/占位/徽章/数字 —— AC-8-23/43 的运行时人工半通过(自动化半早已绿)
- ⚠️ 不借此绿 Logic/Integration 门:这是呈现纪律,不是体征链判据

### 移动(上轮「xyz 不变」的澄清)

- 虚拟键测(PlayMode `movement_virtual_key_test`,4/4):虚拟 W → z +0.292 · 虚拟摇杆 +X → x +0.293 · 全抬轴零 · 方向键 ↑ 并集生效 —— **移动链被虚拟证据实证**
- 结论:WASD / 方向键由同一绑定表(并集),生产侧无断;上轮观感 = 首载卡帧 + 高帧率逐帧位移微不可见

## 7. Quantitative Data

**人工操作**:约 5 分钟;核心循环覆盖:入口(病人出现)+ 呈现(脉案页)可人工观测;诊断/治疗交互仍 0%(42 交互轮未开)。死亡 0(出界重置)。病人胶囊 xyz 站桩(接线期占位实为设计,非 bug —— 位置归 6+13)。

**自动化(本轮新增,不替代人工)**:
- `movement_virtual_key_test` PlayMode **4/4**:虚拟按键 + 摇杆 → 玩家位移链实证(判据:15 帧 Δ ≥ 0.1m;实得 0.292 / 0.293)
- EditMode + PlayMode 全量回归待本轮收口跑(见 sprint-05.md T3.1 ◐ 注)

**用户选项汇总**:Q1=A(右栏布局正确)· Q2=A(空行)· Q3=A(同屏)· Q4=A(Console 健康)· Q5=A(正常退出)。

## 8. Overall Assessment

- **Would play again?**:N/A(灰盒无可玩内容)
- **Difficulty**:N/A · **Pacing**:N/A · **Session length**:N/A

**一句话**:「一张脉案纸 + 一台会动的世界」—— 四形态件真通道呈现在人工面 5A 全绿;世界、移动、UI、Console、退出五个面全部健康;上轮三大疑团(右栏布局 / UI/Click / WASD xyz)各有明确根因并已修复或澄清。

## 9. Top 3 Priorities from this session

1. **Termé终收尾**:探针已删;`movement_virtual_key_test` 保留为移动链实证测试;第三轮报告落盘;全量回归(EditMode + PlayMode)确认零回归后提交(待 T3.1 复跑)
2. **样式表加载序冲突登记兑现**:`.ink-wet/`.ink-dry` × `.channel-reading*` 同写 `color` 的单元素双类胜负由未钉死加载序决定 —— 焦点接线前须裁(ring 子元素 vs 伪元素,story-021 边界⑥);今日登记不擅动
3. **M2 Exit 复评(T3.3)**:#2 端到端(接线后判)、#8 形态件(施加后判)在 T3.1 复跑后判

## Appendix:发现分类路由(承 SKILL Phase 3)

- **Bug reports**:bug #1(布局 B,已修)、#2(UI/Click,已修)、#3(`_styleSheets` type:3,已修)、#5(首载卡帧,登记非阻塞) —— 均修 / 登记,不新开单
- **Design changes**:无(本轮零设计变更;UI 右栏布局修 = 实现缺陷,非设计)
- **Balance adjustments**:无(数值轮另行)
- **Polish items**:UI 首载卡帧(一次性,护栏兜底)→ polish;病人站桩 → 归 6+13
- **CD-PLAYTEST**:skipped —— Lean mode(默认,`production/review-mode.txt` 未设 full)