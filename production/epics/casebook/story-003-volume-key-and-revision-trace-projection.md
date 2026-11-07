# Story 003: 分册键与改写痕读时投影(F-39.2)

> **Epic**: 脉案
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/casebook.md`(规则三 分册 · F-39.2 改写痕投影 · 「旧」态铅笔对勾 = 非颜色 · AC-39-13 痕不落盘)
**Requirement**: TR-casebook-003(`Judgment` 作者轴字段进病例流) · TR-casebook-006(防间接泄漏的**能力面**:只能检索自己病人的投影,partial = 45 铸造契约残)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-008(主): 病例事件流 · ADR-006: 定点域边界(id 稳定性) · ADR-001: 联机 pipe(分册跨机私有的网络侧前提)
**ADR Decision Summary**: `JudgmentRecorded` / `JudgmentRevised` 携带 `author_player_id`(ADR-008 Amendment 具名);某作者对某病例的有效判断 `J(c,p)` = 「作者 p 在病例 c 上的**最后一笔**判断事件」= 读时从病例流折叠;痕(改写史)= 病例流的读时投影,**零独立存储**(AC-39-13:痕 ∉ 可序列化类型、∉ `CaseClosed` 载荷)。分册键 = `player_id`(经 `IIdAuthority`,跨 join/leave/迁移稳定且不重号)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯折叠投影,零引擎 API。

**Control Manifest Rules (this layer)**:
- Required: 投影 = 病例流的纯函数(同流同输出);按 `(Tick, Seq)` 升序扫同 (case, author) 键取末笔;分册互访判据只认 `player_id`
- Forbidden: 痕落盘(任何写 `revision` / `trace` 字段进存档或 `CaseClosed` payload 的代码路径);39 直接读**别人的**分册(能力面 = 检索 API 以 `player_id` 过滤为硬参数,无「全量」重载)
- Guardrail: `J(c,p)` 折叠在联机 4 人 = 4 本独立册同一病例流上做 4 次过滤投影 —— 零合并逻辑

---

## Acceptance Criteria

*From GDD `design/gdd/casebook.md`, scoped to this story:*

- [ ] `J(c, p)` = 病例流中 (case c, author p) 的最后一笔 `JudgmentRecorded/JudgmentRevised`(按 `(Tick, Seq)` 全序取末),纯函数读时折叠
- [ ] 改写痕(第几版 / 「旧」态)由流投影得出,**零独立存储**;AC-39-13 静态断言:痕字段 ∉ 任何可序列化类型、∉ `CaseClosed` 载荷
- [ ] 分册可见性判据 = `player_id`:玩家 p 的分册检索 API 只能取回自己落过笔的病例判断;无参数为「全部作者」的重载存在即失败(AC-39-04 ①② 部分;③ id 跨机稳定性 **BLOCKED-BY-45**)
- [ ] 同一玩家换机 / 重连 / 主机迁移后 `player_id` 不变 ⇒ 分册不裂(单机夹具模拟迁移:`max(id)+1` 高水位重构,承 ADR-006 Amend. B);联机真夹具 **BLOCKED-BY-45,禁借绿**
- [ ] 「旧」态数据标记(供 42 渲染铅笔对勾)是**枚举非颜色**(整数态值,承全案「非色报态」纪律)
- [ ] 4 人联机投影互不污染:同一病例流,4 个 `player_id` 各投影各的分册,内容独立(AC-39-04 夹具的可单机子集:4 个发号 id 模拟)

---

## Implementation Notes

*Derived from ADR-008 Implementation Guidelines:*

1. 投影器 `ProjectVolume(events, player_id) → volume` —— 先按 `author_player_id == player_id` 过滤,再按病例分组取末笔;输入 = Story 002 的只读事件投影,输出为不可变列表。
2. 「旧」态 = 该 (c,p) 存在 ≥ 1 笔被后续笔覆盖 ⇒ 前笔打 `Stale` 枚举;当前笔 = `Current`。无第三方状态位。
3. 落点 `Gameplay.UI` 内的 39 模块;`player_id` 过滤参数是**方法签名级**约束(忘传 = 编译不过),不是运行时 if —— 这是 TR-casebook-006「能力面」的形状。
4. 单机夹具模拟迁移:构造「事件流 + 计数器重建(`next = max+1`)」两状态,断言投影不变。真联机迁移 = P1b 45 轮后回跑同一夹具(记 BLOCKED-BY,不阻塞本 story 收口)。
5. ⚠️ **数值冻结**:痕深度展示上限(册子「改写史」页最多回翻几版)归用户数值轮;投影本身无深度上限(流有多长痕有多长)。

---

## Out of Scope

- [Story 004]: 反幻想守卫(分组/检索禁用扫描)
- [Story 005]: 「旧」态的视觉呈现(铅笔对勾走查,AC-39-12 [L])
- [Story 006]: 落笔写流路径(判定输入上行归 ADR-001 §一之三,实现归 45 轮;本 story 只读流)
- 45 铸造契约(TR-casebook-002/006 residual):`player_id` 铸造的网路时序保证 → gap,禁借绿

---

## QA Test Cases

> 🔁 **2026-11-08 回填(sprint-26 `/qa-plan`)** —— 本节由 `production/qa/qa-plan-sprint-26-2026-11-08.md` §S26-2 派生。

**[Logic story — automated test specs]**
文件:`unity/Assets/Tests/EditMode/Casebook/casebook_volume_projection_test.cs`(约 9 条)

- **AC-1 · `J(c,p)` 末笔折叠**
  - Given: p 对 c 依次 Record → Revise → Revise;q 对 c 也 Record
  - When: `ProjectVolume(E, p)`
  - Then: c 的**第三笔 = `Current`**、前两笔 `Stale`;**q 的笔不出现**在 p 的分册
  - Edge: 同 `Tick` 的两笔按 `Seq` 定序;零落笔 ⇒ 空白页(页在、痕无),可聚焦

- **AC-2 · 痕零落盘(AC-39-13)**
  - Given: 全部可序列化类型 + `CaseClosed` 载荷 schema
  - When: 反射扫字段名 / 类型
  - Then: 无 `trace` / `revision` / `stale` 类字段
  - Edge: 存档 round-trip 后投影重算 == 存档前投影(**逐位**)

- **AC-3 · 跨机不裂(单机夹具,口径收窄)**
  - Given: 事件流固定、p.id = 2;模拟「存档 → 新会话 → 计数器由 `max(id)+1` 重构」
  - Then: 以 p=2 再投影,分册**逐位相同**;迁移后新人得 id=3 不影响 p=2
  - ⚠️ **本条只证「重构不变」,不证「跨机稳定」** —— `AC-39-04 ③` 是 `BLOCKED-BY-45`,记 `NOT-RUN`

- **AC-4 · 无「全量」重载(能力面)**
  - Given: `ProjectVolume` 公开签名
  - Then: `player_id` 为必选 int 参数;**不存在** `(events)` 单参重载(以反射/编译期判,**不靠人看代码**)
  - Edge: 传未发号的 id ⇒ 空分册(**不报错、不退化为全量**)

- **AC-5 · 4 人互不污染**
  - Given: 同一病例流,4 个发号 id
  - Then: 各投影各的分册,内容独立(AC-39-04 的可单机子集)

- **AC-6 · 「旧」态是枚举非颜色**
  - Then: 呈现侧 `Stale` / `Current` 是**整数态值**,不是色值

- **假绿防线**
  - **F3**:折叠顺序若用被测常量做期望值 ⇒ 改常量时两边一起动;期望值写**字面量**

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Casebook/casebook_volume_projection_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(分册键 = 真发号 player_id)· Story 002(病例流只读投影与全序基座)
- Unlocks: Story 005(「旧」态 / 空白页渲染读本项目)· Story 006(落笔后痕即时更新走同一投影)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
