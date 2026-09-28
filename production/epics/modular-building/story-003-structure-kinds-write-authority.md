# Story 003: Structure* 三 Kind 载荷、唯一写权、实例表与字节稳定

> **Epic**: 模块化建造
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/modular-building.md`(规则四 载荷 · 规则五 写权 · B7 实例表为准 · EC-23-09 · AC-23-09/10/12/13)
**Requirement**: TR-building-001(三个 Structure* Kind 落世界流)· TR-building-005(structure_id 与病人共用 IIdAuthority 空间;折叠谓词不适用结构行)· TR-building-006(建造重放字节稳定,绑 ADR-012 黄金夹具)· TR-building-007(23 程序集零呈现引用;42 不是建造状态写者)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主,Amendment J): Structure* 属世界流 + 全序键 · ADR-010(次): §三 义务 12(structure_id 与 ItemInstanceId 共用 id 空间)/ §一 序列化字节面 · ADR-024(次): registry 真源 + kindgen A1–A5 · ADR-007(次): `Patient = PatientId.None(-1)` 哨兵不污染高水位 · ADR-025(次): `Sim`/`Gameplay.Presentation` 边界 = 写者守卫的编译期表达
**ADR Decision Summary**: 三支载荷(全整数,AC-23-01 表/规则四):`StructurePlaced{structure_id, cell, module_id, orientation, variant}` / `StructureRemoved{structure_id, cell, module_id}`(无 orientation/variant)/ `StructureModified{structure_id, cell, module_id, new_orientation, new_variant, modified_fields}`;`modified_fields` 白名单 = 朝向/变体(**无换模块位**,归 P1a);`Patient = PatientId.None`;23 = 三支唯一写者(24 只读布局派生数值、**不发** `StructureModified` —— 结清 ADR-009「23/24」双名);实例表 `structure_id → (anchor, module_id, orientation, variant)` = 第三份派生态,**实例表为准、载荷仅校验**(B7);重放字节稳定 = encode→decode→re-encode 字节相等,刷新政策绑 ADR-012 §三(golden-vN,禁单平台独签)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 载荷/路由/写权为纯 C#(LOW);MEDIUM 面 = 序列化字节黄金夹具属 ADR-012 域(矩阵不存在 ⇒ 跨平台签名 EXTERNAL,Mono 往返等值本故事自证)+ 重放实例化 `InstantiateAsync/Release` 泄漏纪律(ADR-023 拆序六步,呈现侧,归接线故事线)。

**Control Manifest Rules (this layer)**:
- Required: 载荷字段 = 规则四定稿逐字; Append 入口对非 23 程序集 `internal` 不可见 / 引用集白名单(AC-23-13 写者守卫);实例表 fold 与事件流一致(BakedInitial ⊕ 事件);结构行不进折叠谓词豁免清单之外(ADR-010 义务 12 现文)
- Forbidden: 42/24/任何其他系统 Append Structure*(唯一写者);float 入载荷;`modified_fields` 携带数值结果(= 24 派生,AC-23-11 邻接);换模块位(白名单外)
- Guardrail: golden-vN 单平台禁签 —— 本故事只在 Mono 生成草稿值并标注「待三格同签」;AC-23-09 的跨平台半边判据原文保留,不得记绿

---

## Acceptance Criteria

*From GDD `design/gdd/modular-building.md`, scoped to this story:*

- [ ] **AC-23-10**: `GIVEN` 载荷定稿(规则四),`THEN` 字段 = `structure_id / cell / module_id / orientation / variant`(Removed 无 orientation/variant;Modified 含 `modified_fields`),全整数、无 float;`modified_fields` 白名单 = 朝向/变体(无换模块位)
- [ ] **AC-23-09**: `GIVEN` 任意放置/拆除/改造序列,`WHEN` 序列化 → 重放,`THEN` 字节流逐位稳定(往返 = encode→decode→re-encode 字节相等);刷新政策绑 ADR-012 §三。**⚠️ 跨平台签名半边 EXTERNAL · BLOCKED-BY-ADR-012**(矩阵不存在;本故事交 Mono 往返等值 + golden 草稿,禁单平台独签)
- [ ] **AC-23-13**: `GIVEN` 42 渲染层,`THEN` 42 不是 `Structure*` 写者 —— ① 写者守卫:Append 对 42 程序集 internal 不可见/引用集白名单(违规 = 构建失败);② 零事件半:预览取消零 sim 事件(spy-sink PlayMode)
- [ ] **AC-23-12**: `GIVEN` 23 实现程序集,`THEN` 无任何呈现 API 引用(无 Mesh/Transform/GameObject;grep + 程序集断言,承 ADR-013 §9 C3 同构)
- [ ] **B7 实例表为准**: `GIVEN` 重放后,`THEN` 实例表 = fold(BakedInitial ⊕ Structure*),载荷与表不一致时**表为准、载荷仅校验**(具名冲突告警);`structure_id` 与 `ItemInstanceId` 共用 `IIdAuthority` 空间(铸造接线 = inventory-items Story 002 件,折叠谓词不适用结构行)

---

## Implementation Notes

*Derived from ADR-009 Amendment J(主)/ ADR-010 §三 义务 12:*

1. 编排层(主机,following Story 002 = true):`structure_id = ItemInstanceId 共空间 Next()`(ADR-010 §五 机制 A)→ `Append(StructurePlaced{五字段, Patient=None})` → 实例表更新;Removed/Modified 同构;同 tick 多事件按全序键 `Seq` 发号(与 Craft/三支采集共序,ADR-008)。
2. 写者守卫实现形:三支 Append 门面方法 `internal` 于 23 权威程序集;引用集白名单 = asmdef 显式 references(ADR-025 清单封闭性:未登记引用 = 构建失败);42 侧只读投影/事件消费。
3. 序列化:载荷 = ADR-010 全二进制 codec 既有件(Fix 显式小端、禁 float);本故事新增三支的 encode/decode 字段序钉死 + Mono 往返测试;golden 草稿值落 `tests/golden/` 待 ADR-012 三格同签(政策原文保留)。
4. 实例表:纯 fold 派生(不独立快照,ADR-009 派生态);读口 `structure_at(cell)` 服务 24 邻接(下游边,systems-index 已双向)与拆除查找。
5. 折叠谓词纪律:7a 折叠豁免/谓词**不适用于结构行**(ADR-016 §二 同型裁定)—— 本故事交付「结构行进 max(id) 扫描且不被折叠吞并」的断言(inventory-items Story 002 的高水位并集在此交叉)。
6. Modified 与 Removed+Placed 互斥纪律(EC-23-10):改朝向只发 Modified(同 `structure_id` 连续身份),编排层禁「拆了重建」偷懒路径(测试反证:改朝向序列后流中无 Removed/Placed 对)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:判定(本件的 true 分支输入)
- Story 004:Overlay/占用表(与实例表并列的另两份派生态)
- Story 005:`Modifiable` 谓词与 `modified_fields` 语义判定(本件先立载荷与写权)
- Story 006:拆除的返还结算(Removed 的下游效果归 20/23 交接)
- inventory-items Story 002:`IIdAuthority` 本体(共用空间接线)
- ADR-012 落地轮:三格同签与 F7 spike(AC-23-09 EXTERNAL 半边)
- ADR-023/呈现线:重放实例化与占用解耦(`InstantiateAsync/Release`、占位模板预载)—— 呈现接线故事,不在 sim 本件

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-23-10**: 载荷形状。
  - Given: 三支载荷类型反射面。
  - When: 字段集/类型比对。
  - Then: 与规则四逐字一致;全整数;Removed 恰缺两字段;`modified_fields` 枚举 ⊆ {orientation, variant}。
  - Edge cases: 注入 float/换模块位 ⇒ 构造期拒(禁可选字段糊弄)。
- **AC-23-09**(Mono 半边): 字节往返。
  - Given: 1000 事件混合序列(三支全型 + 边界 id 值)。
  - When: encode→decode→re-encode。
  - Then: 两轮字节全等;字段序/端序钉死;golden 草稿生成并标 `PENDING-3GRID`。
  - Edge cases: `Patient = -1` 行往返;空 `modified_fields`(非法,拒);跨平台半边 Ignore(EXTERNAL)。
- **AC-23-13**: 写者守卫。
  - Given: 编译期(引用集)+ PlayMode spy-sink。
  - When: 42 代码引用 Append(注入负样例编译);预览取消操作。
  - Then: 前者构建失败(asmdef 断);后者零事件;23 外程序集(24/42/44/51)Append 面不可见。
  - Edge cases: 测试装配豁免口径同前例;多写者竞态(仅主机,P0 单机 ⇒ 断编排单点)。
- **AC-23-12**: 零呈现引用。
  - Given: 23 sim 程序集引用集 + 类型面。
  - When: 程序集断言(引用 ⊆ 门 A 白名单恰 {BCL, Sim.Contracts})+ 符号扫描 Mesh/Transform/GameObject。
  - Then: 零命中(与 ADR-017 §二 sim 引用集白名单断言同一条构建失败线)。
  - Edge cases: `Sim.Contracts` 内契约类型不算呈现(按 ADR-025 清单判)。
- **B7**: 表为准。
  - Given: 注入载荷与 fold 不一致的损坏流(合成:同 id 两 Placed 不同 module)。
  - When: 重建。
  - Then: 实例表取 fold 序结果、冲突具名告警、不崩溃不静默;structure_id 与 item id 共空间不撞号(高水位并扫,交叉 inventory Story 002 用例)。
  - Edge cases: 结构行在折叠场景保留(不折叠吞 id)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `unity/Assets/Tests/PlayMode/building_stream_write_authority_test.cs` — must exist and pass(spy-sink + 守卫)
- Logic(载荷/字节/扫描): `unity/Assets/Tests/EditMode/Building/structure_payload_and_codec_test.cs`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002(true 分支编排),inventory-items Story 002(共空间铸造),item-database/persistence 既有 codec 件(Complete 线),`entities.yaml` 三支既有 Kind(零 kindgen 变更)
- Unlocks: Story 004(事件输入进 Overlay),Story 005(Modified 写权已立),Story 006(Removed 下游),ADR-012 轮(交 golden 草稿入册)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
