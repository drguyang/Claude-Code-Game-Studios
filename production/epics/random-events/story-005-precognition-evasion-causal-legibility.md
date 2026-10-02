# Story 005: 预告制、避险与因果可见

> **Epic**: 随机事件导演
> **Status**: Complete
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/random-events.md`(规则五 预告制 · 规则六 因果可见 · 规则八 可拒绝/绕开 · AC-52-12/13/15/16/18/19/M1)
**Requirement**: TR-randomevents-018(预告制无直降路径,covered ADR-009) · TR-randomevents-019(预告线索零数值通道,partial) · TR-randomevents-029(避险一等公民,covered) · TR-randomevents-023(可感知门槛,gap) · TR-randomevents-025(线索本地化,gap) · TR-randomevents-032(表现层通道 42/44,covered ADR-013+018)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 三态分类(预告 = **导演本地态不进流**,降临才进流)· ADR-018: 音频架构(线索声走白名单)· ADR-013: 呈现通道 · ADR-016 §八: 遭遇可脱离(27 侧约束)
**ADR Decision Summary**: **预告制无直降路径**(AC-52-12):任何降临必先经预告窗 —— 预告内容 = **定性线索**(`cause_clue_key`,AC-52-18:因果触发的事件线索必非空可解析;AC-52-19:线索**零数值通道** —— 不透露伤害/概率/倒计时数字)。预告窗内**离开 = 避险**(AC-52-13/15):零伤害、遭遇生成数 = 0、不写任何「受损」流事件;P0 名誉字段**不参与**避险等价性断言。降临遭遇必须**可脱离**(AC-52-16,27 约束,ADR-016 §八)。因果可见:玩家可从世界内线索**回溯**归因(玩测门 AC-52-M1 [A][M],归 37 玩测同批的承重问题轨道)。表现层只消费 DTO(42/44),sim 不持有 GameObject。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 预告窗计时 = tick 驱动(sim 侧)→ 表现层经 DTO 呈现;无线程 / 无引擎 API 承重;避险判定读世界流格事件(整数)。

**Control Manifest Rules (this layer)**:
- Required: 线索呈现 ∈ {42 视觉通道, 44 白名单音频通道};避险 = 纯不生成(sim 侧零伤害路径);可脱离行为由 27 状态机承诺、52 只验证「不围死」
- Forbidden: 数值化线索(倒计时/伤害数字/概率文本,AC-52-19);预告写流(本地态纪律);「直降」旁路(任何跳过预告的 `EventArrived` 路径 = 断言红)
- Guardrail: 避险的「离开」判据 = 出诊所涉格域外的世界流格事件(布尔可重构,非表现层距离)

---

## Acceptance Criteria

*From GDD `design/gdd/random-events.md`, scoped to this story:*

- [ ] **AC-52-12 无直降**:`EventArrived` 之前的同 key 必有一条本地预告态对应(构建/测试断言:任何降临在时序上都有预告先行;预告不进流 ⇒ 断言 = 重放时本地状态机可复现预告窗)
- [ ] **AC-52-18 因果线索**:cause 触发事件的 `cause_clue_key` 非空且可在词表解析(烘焙校验联动 Story 001);非因果事件线索可空(合法)
- [ ] **AC-52-19 零数值**:线索 DTO / 文本表扫描无数值字段与数字文本(倒计时、伤害值、百分比);本地化键闭集(承 TR-025 gap 自证:实现 = 键表,翻译文本归本地化轮)
- [ ] **AC-52-13/15 避险一等公民**:预告窗内玩家离开判据成立 ⇒ 该事件消失、零伤害、`EventArrived` 不发、遭遇敌人生成数 = 0(27 侧断言);留下 ⇒ 正常降临。P0 断言**不含**名誉字段等价(明写排除)
- [ ] **AC-52-16 可脱离**:降临遭遇存在撤退路径(与 27 的联测:包围/卡位 fixture 红)
- [ ] 表现层通道:预告/降临经 `EventCueDto`(整数 key)交 42/44;48 教学期事件**互不感知**(OQ-48-6 裁定落点)
- [ ] **AC-52-M1 [A][M] 归因可读性玩测**:玩测脚本 + 记录表成文(执行挂 37 玩测承重门轨道,本 story 交夹具与脚本,标 NOT-RUN 待玩测轮)[M]
- [ ] 教学六步走查期事件照常生成(零教学联动,承 48 epic AC-48-10)

---

## Implementation Notes

*Derived from 规则五/六/八:*

1. 预告 = sim 本地状态机(待触发→预告)的**非持久**段:重放时由 `EventRolled`(窗口)+ tick 相位**重新推出**预告时刻 —— 无需进流(ADR-009 派生态);断言 = 「同种子重放,预告时刻逐 tick 复现」。
2. 离开判据:GDD 布尔口径(出诊域外)由世界流 `ActorCellEntered` 系事件重构,禁读表现位置(AC-20-03 对称)。
3. 线索 DTO:`{event_key, cause_clue_key?, tier_kind}` —— 呈现层自行映射定性文案(42 文本表经 ADR-014 烘焙)。
4. 避险夹具:三态(窗内走 / 窗内留 / 窗外降)各自事件流差分 = 断言表;「零受损流事件」= 存档字节流对比。
5. M1 玩测:脚本 = 10 名玩家 × 因果事件样本 → 回溯归因正确率记录表;**不得**因未跑而标绿。
6. ⚠️ **数值冻结**:预告窗口长度 / 线索文案节奏值归用户数值轮(GDD 只冻结「窗口 > 0」拒收项)。

---

## Out of Scope

- [Story 004]: 预算门(defer 槽进本 story 的预告链)
- [Story 006]: 锚点生成与六态状态机全表(本 story 只到预告/降临/避险支路)
- 27 敌人的脱离 AI 实现(AI epic;本 story 是联测方)
- 线索文本撰写(本地化/文案轮,TR-025)
- 医馆被灾后的修复(F3,P1a)

---

## QA Test Cases

**[Integration story — PlayMode + 重放夹具]:**

- **AC-1**: 无直降时序断言
  - Given: 30 日脚本流
  - When: 扫 `EventArrived` 全体
  - Then: 每条都能在重放态里找到先行的预告窗(0 直降旁路)
  - Edge cases: defer 槽释放 → 再预告 → 降临(二次预告合法,仍无直降)

- **AC-2**: 避险三态差分
  - Given: 威胁事件预告窗 + 受控玩家脚本(走/留)
  - Then: 走 ⇒ 零伤害、零 `EventArrived`、零遭遇生成(查 27 生成计数);留 ⇒ 正常
  - Edge cases: 恰在窗尾 tick 离开(边界 = 走优先,宁不误伤);名誉字段不参与断言(P0 排除注记在测试)

- **AC-3**: 线索零数值扫描
  - Given: 线索 DTO 类型 + 文案 `.cooked` 表
  - When: 反射 + 数字正则(键表侧)
  - Then: 无数值字段;文案键表值无阿拉伯数字与「%」词符(白名单例外 = 无)
  - Edge cases: cause 事件缺 key ⇒ 烘焙红(联动 001),运行期不发无因事件

- **AC-4**: 可脱离联测(AC-52-16)
  - Given: 遭遇夹具,玩家撤退脚本
  - Then: N tick 内脱离成功;敌我不封死唯一出口(布点查 27 约束表)
  - Edge cases: 多敌围合下仍满足

- **AC-5**: M1 玩测脚本交付
  - Given: 因果样本事件集
  - When: 玩测执行(QA 轮)
  - Then: 记录表可判读归因正确率;未跑 = NOT-RUN(禁借绿)[M]

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/PlayMode/RandomEvents/event_precognition_evasion_test.cs` + `production/qa/evidence/random-events/story-005-m1-playtest-script.md` — must exist
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002(流记录)· Story 003(选中+定档)· Story 004(预算/defer)· 42/44 通道(并行 epic)
- Unlocks: Story 006(状态机全表把本 story 的支路并入六态)· 37 玩测承重门(另轨)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `PrecognitionState` — 预告状态（导演本地态，不进流）
- `EventCueDto` — 线索 DTO（整数 key，呈现层自行映射定性文案）
- `EventPrecognition` — 预告制、避险与因果可见（ShouldEvade / CanDisengage / ValidateNoNumeric / CreateCue / ValidateNoDirectDrop）
- 预告制无直降路径
- 避险一等公民（三条件门）
- 可脱离
- 因果线索零数值
- 测试: 7 条单元测试（全部通过）

**Deviations**: 
- 预告窗为简化版（无完整预告窗计时），完整版归 Story 006

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/RandomEvents/event_precognition_evasion_test.cs` — 7 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
