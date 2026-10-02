# Story 005: 读数状态机与判断状态机(快照/旧态/成长门控)

> **Epic**: 诊断与体征揭示
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 10h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/diagnosis-system.md`(S-8.1 持续型 vs 快照型 · S-8.2 读数四态与两窗口 · S-8.3 判断三态与改写留痕 · 规则五 显式/隐式两半 · 边界铁律②④⑤ · Dependencies 表:8 持规则,39 持两个 tick 值)
**Requirement**: TR-diag-005(铁律④ `EmitGrowth` 仅主机侧、由 `IIdAuthority` 门控)· TR-diag-006(铁律⑤ 每名玩家一本脉案,联机)· TR-diag-016(读数存档归属 39 或病例流)· TR-diag-017(AC-8-47 结案时冻结判断链)· TR-diag-015(D-8-10 登记项)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(S-8.2「旧」态由 9 的 `threshold_transition` 触发 —— 事件为真源,8 只订阅)· ADR-013(`ModalId` 闭集含脉案;`IModalState` 只读;42 只渲染)· ADR-006(全序键决定读数到达序)· ADR-008(病例流 `CaseOpened/CaseClosed/PatternRecognized` 与判断记录 Kind)· ADR-011(S-8.4 动作词表:查体 = 脉案模态行级动作 → 8)
**ADR Decision Summary**: **持续型**(视诊)被动实时跟 tick;**快照型**(触/叩/听/问)一次动作一张快照,按**动作完成那一 tick**采样(非开始时刻)。读数四态 = 待查 / 阳性 / 阴性 / 旧;`RECHECK_WINDOW` 内复查**不产出新读数**;`STALE_WINDOW` 以 tick 计(20 Hz 已裁 ⇒ 秒级体感可判读,值仍归用户)。**8 持规则,39 存两个 tick 的值**(快照 tick / 上次重查 tick);`RECHECK_WINDOW` 本体是 8 的烘焙数据(2026-09-19 口径订正)。判断三态 = 空 ⇄ 疑似 ⇄ 确定;空 = 合法终态;改写留痕**不计分**;置信度不进数值。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(状态机本体 LOW;联机双客户端重放面受 45 前置)
**Engine Notes**: 快照与窗口的时间基准 = `ITickProvider` 的 tick(20 Hz,不由渲染帧驱动,承 ADR-005 实现期义务「步相位」)。

**Control Manifest Rules (this layer)**:
- Required: 「旧」转移**幂等**(多次 transition 仍是一个旧);三读数态两两可分(空行 / 阳性形态 / 阴性形态);EmitGrowth 出口 = 主机 + IIdAuthority 门控;每名玩家自己的脉案各自分叉
- Forbidden: 8 自己存两个 tick 值(归 39);第四态(如「把握不足」独立态 —— 它由墨色/笔迹承载,不是状态值);系统改口「未查」(已查+读不出 = 阴性形态+把握不足);8 记录死亡原因或持死亡标志
- Guardrail: AC-8-25 / AC-8-48 曾标依赖 `D-8-7`(tick 频率)—— ✅ 2026-09-20 已解除(20 Hz),现可直接判;值 `STALE_WINDOW`/`RECHECK_WINDOW` 仍归用户 ⇒ 以合成 tick 数注入

---

## Acceptance Criteria

*From GDD `design/gdd/diagnosis-system.md`, scoped to this story:*

- [ ] **AC-8-21**[L] BLOCKING:三种读数两两可分 —— (a) 未做手段 = **空行**(有格线无字、无水痕压痕、无 badge/灰字/占位符/「未查」文案);(b) 读到体征 = 阳性形态(字、浓墨、有收锋);(c) 做了无体征 = 阴性形态(字仍在)—— 三态由哨兵可分(story 003 字母表)
- [ ] **AC-8-22**[L] BLOCKING:隐式半边 —— `Sign_j` 客观存在但 `< READ_FLOOR(Skill)` ⇒ 状态必须是「已查」,呈现为阴性形态 + 把握不足;**系统不得改口为「未查」**(无空行、无未查标记);与真阴性在玩家眼里不可分是**有意的**
- [ ] **AC-8-24**[U/L 双判据] BLOCKING:态数齐全且无第四态 —— 体征行恰四态、病名栏恰三态(空/疑似/确定);不存在第三档置信度、不存在「不可用」态(裁定⑨)、无任何「未查/把握不足」文字标记或 badge(状态值层单测 + 走查归 story 006)
- [ ] **AC-8-25**[I] BLOCKING:快照冻结 vs 持续刷新 —— 快照型取快照后 9 的 `Progress` 变 ⇒ 该快照**逐字不变**(词/态/墨);同时刻持续型(视诊)**跟随当前 tick** 刷新;∀ 快照型通道无边读(依赖 D-8-7 已解除)
- [ ] **AC-8-48**[I] BLOCKING:采样点逐位一致 —— 两进程令 `Progress` 在动作**进行中**变化 ⇒ 快照型按**动作完成那一 tick**采样(非开始),两进程读出的词逐位相同;持续型跟当前 tick;采样时刻不定死即测试失败
- [ ] **AC-8-27**[L] BLOCKING:「旧」的转移幂等 —— 连续投递 3 个 `threshold_transition` ⇒ 仍是一个「旧」,不叠加标记、不重复提示、**不重复播放任何音效**(无 audio sting;联动 44 的 AC-44-09)
- [ ] **S-8.2 窗口托底**[L]:`当前 tick − 上次重查 tick > RECHECK_WINDOW` ⇒ 标旧(8 持规则);窗口内复查**不产出新读数**;`STALE_WINDOW` 到期 ⇒ 旧;两 tick 值经 39 查询接口取(8 不存)
- [ ] **AC-8-28**[L] BLOCKING:8 不替玩家踩刹车 —— 查遍无结论不落笔即关病例 / 一通道都没查就落笔,**两者都允许**;8 不校验「查够」、不催、不提示、不给兜底清单、不自动填写;病名空白 = 「一张等着落笔的纸」(可写性可见)
- [ ] **AC-8-29**[接口半边 BLOCKING / [V] ADVISORY]:改写留痕且痕不计分 —— 8 与 #53 的输入契约里**无**「改写次数」字段(反射断言);置信度不进任何数值公式(规则八 / S-8.3 性质 3);截图半边归 story 006
- [ ] **AC-8-30**[I] BLOCKING:规则七误诊不报错 —— 写错误病名 ⇒ 零对错提示、零提示音、零战斗式 feedback、零延迟 toast;唯一反馈 = 病人未好转(经 9 病程);诊断正确性只在结算时用(37 / #53),不在当下暴露
- [ ] **AC-8-31**[L] BLOCKING:病人无法配合 ⇒ 未查(裁定⑨后唯一客观「查不了」)—— 昏迷 / 不能坐起 / 剧痛 / 小儿抗拒 ⇒ 问诊所辖通道恒空行;`sign_retraction`/`sign_orthopnea` 视诊不可得 ⇒ 未查;触诊不可得 ⇒ 未查;**但任何通道都不因等级而不可用**(与 AC-8-9 两向互证)
- [ ] **AC-8-47**[L] BLOCKING:病例关闭 —— 任意读数状态下 37 关病例 ⇒ 读数状态机(S-8.2)**全状态清除**;判断状态机(S-8.3)**冻结**并交 #53;8 不记录死亡原因、不持死亡标志
- [ ] **AC-8-49**[I] BLOCKING:与 9 的 AC-22 端点复用 —— 潜伏期病人:8 侧全通道「已查 · 无异常」(合法输出非错误态);把握度按 F-8.3 走,**不构成排除**;9 仍照常推进 `Progress`
- [ ] **铁律④ EmitGrowth 门控**[A] BLOCKING:`EmitGrowth` 调用点仅主机侧,且经 `IIdAuthority` 门控(客户端不调;静态守门 = 出口唯一 + 门控断言,TR-diag-005)
- [ ] **铁律⑤ / AC-8-45**[I] BLOCKING:联机每人一本脉案 —— 两玩家 Skill 不同(如 5/50)对同一病人同动作 ⇒ 读到精度**可以不同**(读数是个人的),两台机器病史事件流哈希**逐位相同**;8 的差异只在表现层;读数分歧不回写(铁律②)。⚠️ 双客户端真联机面 **BLOCKED-BY-45 epic**(P0 以 in-process 双端 fake pipe 判结构;矩阵/网络子句 NOT-RUN,禁借绿)
- [ ] **AC-8-46**[I 半边] BLOCKING:掉级后精度回退的持久化半边 —— 已写下的病名**不回退**(归 37);系统不提示不解释(`[U]` 走查归 006)

---

## Implementation Notes

*Derived from S-8.1/S-8.2/S-8.3 + Dependencies 表:*

1. 状态机分两半且**互不持有**:`ReadingFSM`(四态,输入 = agg 读数 + 9 的 transition 事件 + 两个 tick)与 `JudgmentFSM`(三态,输入 = 玩家落笔意图);后者的**持久化归 39**,8 只产意图(承 AC-8-47「冻结并交 #53」)。
2. 快照型采样点实现 = 动作完成回调携带 `completion_tick`,以该 tick 从 `IVitalsQuery` 取一次值并物化为快照;禁缓存「当前 tick 猜测值」。
3. `threshold_transition` 订阅路径 = 事件流(病史流)消费,幂等化用「已应用 transition 的 (patient, 旧, 新, tick) 集」判定(该集是**当帧缓存**非跨进程状态,须与 AC-8-3 的「无跨调用累积量」口径对齐:实现期若需持久化则改由 39 存 tick,登记评审点)。
4. 无法配合(AC-8-31)的判据来自 9/13 的在场与配合标志(整数),8 不自行判定病情;`reveal_by` 与标志的映射表进 story 002 烘焙数据的扩展位(登记 D-8-2 若增字段)。
5. `EmitGrowth` 出口与 30 的接口签名复用 skill-system epic;门控 = `IIdAuthority.IsHost(actorId)` 式判定(承 ADR-007)。
6. 关病例时的清除次序:先清读数态、再冻结判断态、再交 #53 —— 顺序写成断言(避免「冻结后仍可写」竞态)。

## Out of Scope

- Story 003 / 004:门槛与把握度求值(本 story 只消费其哨兵/阈值结果)
- Story 006:呈现(墨色承载把握不足、页边铅笔勾、压痕声等 [V]/[U] 判据)
- 39 脉案的存储本体(casebook epic;本 story 只经其查询接口)
- 查体动作的表现形式与耗时(D-8-3,归 10/4;OQ-8-1 复查成本托底)
- 52/#53 结算侧(本 story 只保证「痕不进契约」)

## QA Test Cases

*Written at story creation(lean mode).*

- **三态可分**: 三病灶夹具(a/b/c)⇒ 三个不同哨兵,空行夹具断言无任何标记字段被置(AC-8-21)。
- **不改口**: `Sign < FLOOR` 夹具 ⇒ 态 = 已查·阴性形态 + 把握不足标志;断言「未查」态不可达该路径(AC-8-22)。
- **快照冻结**: 取快照 → 推 9 的 Progress → 快照哈希不变,视诊跟 tick(AC-8-25)。
- **完成 tick**: 双进程在动作中段变更 Progress ⇒ 两次词相同且 ≡ 完成 tick 值(AC-8-48)。
- **幂等旧态**: 三 transition ⇒ 旧计数 1、音效触发 0 次额外(sting 断言)(AC-8-27)。
- **窗口托底**: 合成 `RECHECK_WINDOW=5 tick`,复查在 4/5/6 tick ⇒ 无新读数/无新读数/标旧。
- **不踩刹车**: 零查体直接落笔 + 全查体不落笔,两路均成功、零提示事件(AC-8-28)。
- **关病例**: 全态清除 + 判断链冻结 + 8 无死亡字段(AC-8-47)。
- **联机不回写**: fake pipe 双端不同 Skill ⇒ 事件流哈希相同 + 各自快照不同(AC-8-45)。
- **成长门控**: 客户端上下文调 `EmitGrowth` ⇒ 无副作用/被拒;主机 ⇒ 恰一次(铁律④)。

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/DiagnosisSystem/reading_state_test.cs` + `unity/Assets/Tests/PlayMode/DiagnosisSystem/snapshot_staleness_test.cs` — must exist and pass;AC-8-45 网络传输子句 NOT-RUN(BLOCKED-BY-45 epic)
**Status**: [ ] Created — NOT STARTED

---

## Dependencies

- Depends on: Story 001(边界/出口形状)、Story 003(哨兵字母表)、Story 004(构成排除阈值)、casebook epic(39 的两个 tick 查询接口 + 病名持久化)、disease-simulation epic story 002/004/005(transition 事件与 Progress)、case-system epic(37 关病例触发)、skill-system epic(`EmitGrowth`/`IIdAuthority`)
- Unlocks: Story 006(呈现消费四态/三态)、review-all-gdds(判断链端到端)、#53 结算(改写次数缺席的前提)

## Completion Notes
