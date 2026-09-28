# Story 006: 呈现白名单与零状态播报

> **Epic**: 死亡与复活
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28
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

- [ ] AC-29-06 [A]:枚举死亡呈现路径全部产物,每一产物 ∈ {过渡动画 · 环境音 · 42 拟物元件} 白名单(正向断言;违例名单示例红)
- [ ] AC-29-07 [A]:递归扫描呈现层全部 DTO —— 零代价数值字段(掉落数/掉级数/死亡次数均不得出现)
- [ ] AC-29-11 **[L]**:人工走查「倒下 → 静默 → 醒来 → 回程」节奏成立(主创签核,证据文档落 `production/qa/evidence/`)
- [ ] AC-29-12 **[L]**:人工听测零状态播报音(无 jingle/无 ducking 报失败;与 AC-44-09 联合)
- [ ] UX Flag:全屏过渡的 `/ux-design` spec 已产出并先行于本 story 关闭(GDD 明写前置)
- [ ] 呈现不持状态:死亡态 DTO = sim 事件/查询的投影;重开呈现层不改变任何 sim 事实(负断言:无写路径)

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
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 005(落点/相位事件流);`/ux-design` 全屏过渡 spec(硬前置);42 元件库既有件
- Unlocks: 29 epic 验收;与 44 的 AC-44-09 联测

---

## Completion Notes

*(留空 — story 关闭时回填)*
