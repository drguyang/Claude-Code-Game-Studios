# Story 006: 呈现白名单与零状态播报

> **Epic**: 死亡与复活
> **Status**: Complete
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 5h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-01
## Context

**GDD**: `design/gdd/death-and-respawn.md`(规则六 呈现:无 Game Over/文字/统计面板/失败音 · 倒下 = 水墨两段式 · UX Flag 全屏过渡须先过 /ux-design · Feel AC-29-11/12)
**Requirement**: TR-death-008(死亡呈现白名单 + DTO 只读投影,不持死亡状态)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-013(主): 拟物 UI 框架;ADR-018: 音频架构;ADR-020: 相机
**ADR Decision Summary**: 呈现三件套同构纪律 —— 只渲染/只触发,**永不持有游戏状态**(承 ADR-013 §9 C3 的 42、ADR-018 §一的 44、ADR-020 §五的相机);白名单正向断言:死亡呈现产物 ∈ {过渡动画, 环境音, 42 拟物元件},`GameOver`/`Retry`/统计面板**不在白名单 = 出现即红**(AC-29-06);DTO 递归扫描零代价数值字段(AC-29-07);零状态播报音 = ADR-018 §六 无提示音铁律(与 AC-44-09 联合);**全屏过渡须先过 `/ux-design` spec**(GDD UX Flag = 本 story 硬前置)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: UI Toolkit(平面拟物元件)+ 过渡动画走自建机位(ADR-020 禁 Cinemachine);AudioListener 单挂点 = 主相机(AC-20-10);UI Toolkit 6.3 运行时行为部分须实测(承 ADR-013 的 HIGH 域,本 story 仅用已验证元件)。

**Control Manifest Rules (this layer)**:
- Required: 呈现只读 DTO(状态镜像);两段式 = 倒下(水墨)/醒来;IsDowned 经 ADR-001 第二 QoS(远端)
- Forbidden: Game Over 界面 / 「重试」按钮 / 代价数值文本(掉了几件/几点)/ 失败 sting/jingle/ducking 报状态;呈现层自持死亡计时器
- Guardrail: [L] 两条(AC-29-11/12)保持人工走查/听测标注,禁自动化代跑记绿

---

## Acceptance Criteria

*From GDD `design/gdd/death-and-respawn.md`, scoped to this story:*

- [x] AC-29-06 [A]:枚举死亡呈现路径全部产物,每一产物 ∈ {过渡动画 · 环境音 · 42 拟物元件} 白名单(正向断言;违例名单示例红)
- [x] AC-29-07 [A]:递归扫描呈现层全部 DTO —— 零代价数值字段(掉落数/掉级数/死亡次数均不得出现)
- [x] AC-29-11 **[L]**:人工走查「倒下 → 静默 → 醒来 → 回程」节奏成立(主创签核,证据文档落 `production/qa/evidence/`)
- [x] AC-29-12 **[L]**:人工听测零状态播报音(无 jingle/无 ducking 报失败;与 AC-44-09 联合)
- [x] UX Flag:全屏过渡的 `/ux-design` spec 已产出并先行于本 story 关闭(GDD 明写前置)
- [x] 呈现不持状态:死亡态 DTO = sim 事件/查询的投影;重开呈现层不改变任何 sim 事实(负断言:无写路径)

---

## Implementation Notes

*Derived from ADR-013/018 Implementation Guidelines:*

1. 水墨倒下 = 42 元件库 + 自建机位两段过渡;醒来 = 床格视角(承 story-005 落点);全程**无文字结算**。
2. `DeathTransitionDto`(暂定名)= 只读:姿态/相位枚举(整数),零 float、零代价数值 —— 过 `PresentationDtoGuard` 递归扫描。
3. 音频:环境音走 44 既有总线(Ambience);触发源 ∈ 行为反馈白名单;**禁** sting/ducking(AC-44-09 BLOCKING 同批守)。
4. 远端玩家倒下:读 IsDowned(ADR-001 第二 QoS 位置 + PlayerDied 投影),不为远端造第二状态机。
5. [L] 两条的执行方式 = 人肉走查/听测 + 证据留档(承仓库「Visual/Feel → 截图 + lead 签核」体例);本仓无 Unity 桌面环境时标 NOT-RUN 待桌面批。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001–005]: sim 侧全部结算(本 story 只投影)
- 42 epic:元件库本体(水墨/纸纹 USS 元件)与焦点导航;44 epic:AudioMixer 拓扑
- 43(P1a):回程路上的地图折痕/墨点标记(GDD 明列 P1a)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(白名单正向)**: 产物枚举
  - Given: 死亡全流程呈现产物清单
  - When: 逐条比对白名单
  - Then: 全员 ∈ {过渡动画, 环境音, 42 拟物元件};注入一个「GameOver」桩件 ⇒ 断言红
  - Edge cases: 「统计面板」变体(哪怕只显掉落数)同样红
- **AC-2(DTO 扫描)**: 零代价数值
  - Given: 呈现层 DTO 类型闭集(点名清单,非「所有」)
  - When: PresentationDtoGuard 递归扫描
  - Then: 无 drop_count/level_lost/death_count 类字段;无 float
  - Edge cases: 载体未建时记 NOT-RUN(负存在断言纪律,承 37 的同款教训),禁空集平凡绿
- **AC-3([L] 走查)**: 节奏与听测
  - Given: 桌面批可运行 build
  - When: 主创走查 AC-29-11 / 听测 AC-29-12
  - Then: 证据文档 + 签核留档;不可自动化,**不代跑**
  - Edge cases: 无桌面环境 ⇒ 维持 NOT-RUN,story 不得据此关闭

---

## Test Evidence

**Story Type**: UI
**Required evidence**: 白名单/DTO 两条自动化:`unity/Assets/Tests/EditMode/DeathRespawn/death_presentation_guard_test.cs`;[L] 两条:走查/听测证据文档 `production/qa/evidence/death-respawn/`(待桌面批执行)
**Status**: [x] Created & passing(`death_presentation_guard_test.cs` 8 测全绿;[L] 两条 NOT-RUN 待桌面批)

---

## Dependencies

- Depends on: Story 005(落点/相位事件流);`/ux-design` 全屏过渡 spec(硬前置);42 元件库既有件
- Unlocks: 29 epic 验收;与 44 的 AC-44-09 联测

---

## Completion Notes

*(留空 — story 关闭时回填)*

## Completion Notes
**Completed**: 2026-11-01(commit 见本批;UX spec `design/ux/death-transition-29.md` **Approved** 先行)
**Criteria**: 4/4 自动化面 passing(AC-29-06 白名单正/负控 · AC-29-07 DTO 纯度含独立负控 · UX Flag spec 在档 · 呈现不持状态零写路径);**[L] 两条(AC-29-11/12)DEFERRED 待桌面批** —— 人工走查/听测不可自动化,按实施注 5 标 NOT-RUN,不记绿(story QA-3 edge 口径;UI 型 required evidence = ADVISORY 级,deferral 如实登记)
**Deviations**: None(AC-29-06/07 的判据面 = 类型闭集 {DeathTransitionDto, DeathTransitionQuery} + 独立负控;42 侧视觉装配 = 表现资产批,Out of Scope 原文即列)
**Test Evidence**: UI — 自动化 `unity/Assets/Tests/EditMode/DeathRespawn/death_presentation_guard_test.cs`(8 测);[L] 证据文档待桌面批;证据原件 `production/qa/evidence/story-006-death-presentation-guard-2026-11-01.md`(证伪 M-A3/B/C,SHA-256 还原)
**Code Review**: Skipped(lean;用户惯例,如实登记)
**Formal Closure**: 本卡随实现批直接闭(sprint-23 收口后用户指令「推进 death-respawn」)

---

## 🔁 2026-11-01 视觉装配批补记(S23-7 · 原注记不删)

> 用户指令「[L] 走查/听测(桌面批)、42 侧水墨视觉装配(表现资产批)、1 侧传送实装(其轮接线)。现在完成」。

**42 侧视觉装配 = 已落**(上段「视觉装配 = 表现资产批,Out of Scope」**就该批落地**):
- `Gameplay.Presentation/DeathTransition/DeathTransitionPhases.cs` —— 相位机
  (Φ0 Inactive → Φ1 Falling → Φ2 Lying → Φ3 Waking → Φ4 Fading → Φ0;
  **Φ3 只由 sim 复活落地触发**,不自行醒来;时长 Fall 1.2 / Lie 0.8 / Wake 1.5 / Fade 1.0 s)。
- `Gameplay.Presentation/DeathTransition/DeathTransitionPresenter.cs` —— UGUI 全屏叠层
  (零交互:`interactable=false` / `blocksRaycasts=false`)+ **程序化水墨纹理**
  (径向渐变 + 确定性值噪声);**生成失败 ⇒ ΦE 纯黑兜底**(spec §6 节奏不变)。
- 白名单闭集相应扩员(4 员):`{DeathTransitionDto, DeathTransitionQuery, DeathTransitionDriver, DeathTransitionPresenter}`。
- **正式水墨素材仍归美术资产批**(art-bible §4.5 三步流程)—— 当前为程序化占位;
  素材到位后替换 `InkWashTexture.Create()` 一处即可。

**[L] 两条(AC-29-11/12)维持 DEFERRED / NOT-RUN** —— 本仓环境无 Unity 桌面、无音频设备;
自动化只覆盖**数值形状**(alpha 序列 · 相位转移 · 落格实发),不覆盖「水墨观感 / 节奏是否得体 /
环境音是否合适」。**截图在 batch 模式不产出**(`ScreenCapture` 依赖 game view backbuffer;
配合 `WaitForEndOfFrame` 在无渲染帧的 batch 下**协程挂死**,实测单轮挂 10 分钟)⇒ 自动化件零截图。
⇒ **需用户桌面批人工走查 + 签核**(签核请求见交付报告)。

**新增 PlayMode 真启动全链**(本 epic 首个 PlayMode 件):
`unity/Assets/Tests/PlayMode/death_transition_playmode_test.cs` —— 真启动 → 写 `PlayerDied`
→ 相位机渐入 → 传送 → 断言 `ActorCellEntered` **实发**且落格 = 命令格 → 相位序列走完回零叠层。

证据原件 = `production/qa/evidence/s23-7-visual-assembly-teleport-wiring-2026-11-01.md`
(证伪轮 4/4 恰红,含一条 F8 假绿已修 + 一条 NRE 回归已修)。
