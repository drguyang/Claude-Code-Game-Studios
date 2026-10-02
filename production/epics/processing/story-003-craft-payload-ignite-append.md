# Story 003: Craft 事件载荷十字 + 点火即落流 + 全序键铸造

> **Epic**: 炮制
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/processing.md`(规则八 · R-18-A · D-21-28[甲] · 规则五 ActualConsumed · 规则七原子序 · AC-18-11/16/25/19)
**Requirement**: TR-processing-009(ActualConsumed 随 Craft 落世界流,18 是唯一产生者)· TR-processing-016(发起即落流,完成=派生,无进程态 —— ADR-010)· TR-processing-011(与 20 原子性:先验容量后扣,失败整体回滚)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主,Amendment J): `Craft` 属世界流 + 三流全序键 · ADR-005(次): 主机唯一 Append/发号 · ADR-006(次): 载荷 ⊆ 整数域(Fix raw long,禁 float)· ADR-010(次): 事件流即全部持久化 · ADR-024(次): Kind 路由真源 entities.yaml
**ADR Decision Summary**: 点火即落流:`Craft` 载荷十位(R-18-A 定稿)`{actor_id, recipe_id, start_tick, duration_ticks, input_instance_ids[], ActualConsumed[], OutputQty[], OutputQuality[], output_instance_ids[], tool_cell}`;`output_instance_ids[]` 于**点火 tick** 由主机经 `ItemInstanceId.Next()` 依全序键顺序铸造(认领 20 的 R10/前置 6);全序键 = `(Tick, StreamPriority, Patient=None, Seq)`(D-21-28 裁定[甲]);原子序:准入 → 单炉 → 调 F1 → **20 Apply(先验容量)→ 20 接受后才 Append `Craft`**(AC-18-12 点火时点容量不足 = 零事件);`Patient = PatientId.None`(-1 哨兵,不污染高水位,ADR-007 ④)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 载荷、发号、落流为门 A 纯 C# 数据契约(`Craft` Kind 已入 registry,零 kindgen 变更);序列化字节面归 7a/codec 既有件(ADR-010 全二进制),本故事只断事件语义;跨平台逐位(AC-18-15)归 ADR-012(EXTERNAL)。

**Control Manifest Rules (this layer)**:
- Required: 载荷十位齐且 ⊆ 整数域;`Craft` 落**点火 tick**(非完成 tick);`ActualConsumed[]` 由 F1 出参原样入载荷(承 AC-18-01② 同引用链);产出 id 铸造次序 = 全序键序;`tool_cell` = 整数格(ADR-015 `WorldPos`)
- Forbidden: `float`/`double` 入载荷;完成 tick 才落流(= 进程态伪形);18 直写 `output_instance_ids` 于自己的计数器(必须经 `IIdAuthority`);向 `entities.yaml` 追加第四 `Craft*` Kind(零新增,复用既有)
- Guardrail: 合成夹具配方驱动载荷形状测试(OQ-18-4 真实配方未冻结 ⇒ AC-18-25:形状与恒在性义务不因内容未定豁免);载荷字段名/序与 `entities.yaml` 的 `Craft.payload_schema` 逐字对齐(ADR-024 真源优先)

---

## Acceptance Criteria

*From GDD `design/gdd/processing.md`, scoped to this story:*

- [ ] **AC-18-16** [I]: 一次炮制读世界流 ⇒ ① `Craft` 落**点火 tick**;② 载荷**五位齐**(actor_id / output_instance_ids[] / tool_cell / start_tick / ActualConsumed,R-18-A);③ `output_instance_ids` 于点火 tick 由主机经 `ItemInstanceId.Next()` 依全序键顺序铸造(认领 20 R10);④ 全序键 = `(Tick, StreamPriority, Patient=None, Seq)`(D-21-28[甲])
- [ ] **AC-18-11**: 一次炮制读 `Craft` 载荷 ⇒ 含 `ActualConsumed[]`(21a AC-21a-52 实现侧归 18,承注③)
- [ ] **AC-18-25**: `OQ-18-4` 真实配方未冻结时,以**合成夹具配方**跑 AC-18-11 / AC-18-16 仍全绿 —— 字段形状与恒在性义务不因内容未定而豁免(注③)
- [ ] **AC-18-19** [I]: 任意运行期炮制验守恒律 `Σ(weight × ActualConsumed) ≥ Σ(weight × produced × QM⁻¹)` ⇒ 不破。⚠️ **EXTERNAL · BLOCKED-BY-21a(O-18-R3)** —— 21a 构建期聚合式与运行期逐条 Round 不同形(Edge Cases 反例),真修归 21a,回写未落 ⇒ **不得记绿**;本故事只交运行期反例复现夹具

---

## Implementation Notes

*Derived from ADR-009 Amendment J(主)/ ADR-006 §四:*

1. 点火事务序(主机,单 tick 内):`Admissible`(Story 001)→ 单炉空闲(Story 004 状态机)→ F1(Story 002,恰 1 次)→ **20.Apply(先验容量,后扣 input 实例)** → 20 接受 ⇒ `IIdAuthority.ItemInstanceId.Next()` × |outputs| 铸造 → `Append(Craft{十位})`。任一步失败 ⇒ 回滚至零事件(AC-18-12 拒绝在时点,不存在中途回滚)。
2. `Seq` 由主机发号(ADR-005 Amendment A `SimEvent.Seq`);同 tick 多事件(含 17 三支、23 结构事件)按全序键比较有序,`StreamPriority` 病史 < 病例 < 世界(ADR-008/009)。
3. 铸造次序:多产出实例按 `outputs[]` 索引序逐个 `Next()`(确定性次序 = 全序键可重放的必要条件);id 单调性测试 + fold 重建一致性(承 inventory-items R10 认领)。
4. `ActualConsumed[]` 为 F1 返回数组的**载荷化拷贝**(整数化后入流;载荷禁引用型 —— 序列化边界在 ADR-010 codec)。
5. `tool_cell`:点火时器具所在整数格(与 `EnvMod` 锚点同一格,不重采样);P0 医馆器具位归 24 布局,18 只搬运读数。
6. 守恒律反例夹具:按 GDD Edge Cases 构造「构建期聚合 vs 运行期逐条 Round」可击穿组合,交 21a O-18-R3 修复轮做正反对照(AC-18-19 本件不得记绿的证据链,登记而非掩埋)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:F1 调用与供料(本故事消费其出参)
- Story 004:完成派生(`CompleteTick = start + duration`)与等待期行为(本故事只落 start 侧)
- Story 005:完成时点调度、起货、溢出 `DropSpawned`
- inventory-items epic:20.Apply 本体、`DropDespawned` 消耗形状(R2/OQ-20-10 未裁面)、高水位扫流含 Craft 产出 id(R3)
- 7a(ADR-010):Craft 行的二进制序列化与折叠(18 不写 codec)
- ADR-012 落地轮:AC-18-15 跨平台三出参逐位(EXTERNAL,矩阵不存在)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-18-16**: 落流时点 + 十位 + 铸造 + 序键。
  - Given: spy-sink;合成夹具配方(2 输入 1 输出)。
  - When: 点火 tick T。
  - Then: `Craft.tick == T`;十位齐、类型 ⊆ {int, long(Fix raw), 整数数组};`output_instance_ids` 长度 = 输出条目数、单调、与同 tick `Seq` 序一致;`Patient == -1`。
  - Edge cases: 同 tick 两玩家各点火 ⇒ 两条 `Craft` 按全序键可比且无 tie 歧义(Seq 唯一);完成 tick 不再落第二条 Craft(无「完成事件」Kind)。
- **AC-18-11**: ActualConsumed 在载荷。
  - Given: EFF < 1 的夹具(F1 实耗 > 名义量)。
  - When: 落流。
  - Then: 载荷含 `ActualConsumed[]` 且 = F1 出参整数化;fold 侧(20)按该数组扣减可重建(供 inventory-items Story 001 交叉样本)。
  - Edge cases: EFF = 1(实耗=名义);数组零长(非法,装载即拒)。
- **AC-18-25**: 合成夹具不豁免形状。
  - Given: 真实配方表为空/占位(OQ-18-4 内容轮未冻结)。
  - When: 跑 AC-18-11/16 同一测试体。
  - Then: 全绿(形状义务独立于内容);真实表落地后同体再跑。
  - Edge cases: 夹具字段缺失 ⇒ 编译期/构造期即拒(禁可选字段糊形状)。
- **AC-18-19**(EXTERNAL 登记): 守恒反例夹具交付。
  - Given: 构造击穿组合(逐条 Round 累计 > 聚合式)。
  - When: 运行期结算。
  - Then: 反例可复现并归档供 21a 修复;本 AC 在 O-18-R3 关闭前保持未勾 + `Ignore("BLOCKED-BY-21a O-18-R3")`。
  - Edge cases: 修复后翻真判据(回归对照用同一夹具种子)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `unity/Assets/Tests/PlayMode/craft_event_payload_test.cs` — must exist and pass(spy-sink + fold 交叉)
- Logic(载荷整数域/形状): `unity/Assets/Tests/EditMode/Processing/craft_payload_shape_test.cs`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(准入),Story 002(F1 出参),Story 004(单炉空闲判定 —— 点火事务第一步;两者互为时序前后但测试夹具可先行桩),inventory-items Story 002(`IIdAuthority` 铸造权威件),`entities.yaml` 既有 `Craft` Kind(零变更)
- Unlocks: Story 004(重放等值消费本条落流形状),Story 005(完成派生自载荷),inventory-items Story 001(fold 并入 Craft 增益侧 R10 样本)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
