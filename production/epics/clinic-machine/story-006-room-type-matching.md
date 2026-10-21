# Story 006: 房间类型匹配(ROOM_TABLE 子句求值)+ AC-24-03

> **Epic**: 医馆即机器
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-21
## Context

**GDD**: `design/gdd/clinic-machine.md`(规则二 房间定义与类型判定 · AC-24-03 ROOM_NONE 基例 · EC-24-01 / EC-24-02 / EC-24-07)
**Requirement**: TR-clinic-003(EnvMod 定点域)· TR-clinic-001(邻接 = 整数格判据)· TR-clinic-007(ROOM_NONE 哨兵)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-015(主): 世界几何 —— 单一整数格;ADR-005: 确定性模拟
**ADR Decision Summary**: `WorldPos = (i32 x, i32 y, i32 z)` 单一整数格;房间「红石逻辑」= 整数格 4 邻接判定(非物理 / 非 NavMesh);判据全部整数域、纯函数、可由事件流重建(ADR-016 §一 三源不变量)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数集合运算与字符串 Ordinal 比较,零引擎 API;不依赖 post-cutoff 面。

**Control Manifest Rules (this layer)**:
- Required: 匹配 = **纯函数**(布局 + cooked 表 → room_type 或 ROOM_NONE),无缓存、无状态;字符串判定一律 `StringComparison.Ordinal`(之八);邻接子句只查 +x/+y 单向(与 `adj_sum` 同源,ADR-015 §五)
- Forbidden: 以 `UnityEngine` 几何 / 物理 / NavMesh 判邻接;以 culture-sensitive 比较判 `type` / `room_type`;在 sim 程序集内留匹配结果缓存
- Guardrail: 匹配结果须可经 `ClinicRoomExtractionResult` + `ClinicRoomTable` 从事件流重建(派生态,不进流)

---

## ⚠️ 本 story 的立卡缘由(为何不是 story-003 的一部分)

**`AC-24-03` 此前无主** —— 2026-10-21 实测:

| 扫描 | 结果 |
|---|---|
| `grep -l "AC-24-03" production/epics/clinic-machine/story-*.md` | **空**(001–005 五件全不认领) |
| `grep -rn "MatchRoom\|RoomTypeOf\|EvaluateClause" unity/Assets --include=*.cs` | **0**(整段不存在) |

story-003 的 6 条 AC(01 / 01b / 02 / 四条配套)**不含**房间匹配 —— 其 `base_env` 由调用方喂入
(`ClinicContextTable.TryGetBaseEnv`)。⇒ **S17-2 置 `In Review` 的理由把上游的无主义务记到了 story-003 账上**。
用户 2026-10-21 裁定:**立本 story 认领 `AC-24-03`,story-003 转 `Complete`**。

---

## Acceptance Criteria

*From GDD `design/gdd/clinic-machine.md`, scoped to this story:*

- [ ] AC-24-03:空布局 / 未匹配任何房间类型 ⇒ 输出中性 `(EnvMod, EquipMod) = (0, 0)`(ROOM_NONE 基例,EC-24-01 / EC-24-02)
  <br>载体须**双向**:① 空布局(EC-24-01);② 有家具但谓词全不成立(EC-24-02)—— 两者**分别**一测,
  不得只测一个就宣称覆盖(EC-24-01 是 EC-24-02 的特例,反向不成立)。
- [ ] 三种子句类型各有判据:`min_count`(类型计数 ≥ min)· `has_type`(存在性)· `adjacency_min_count`(方向性邻接计数 ≥ min)
  <br>载体 = 每种至少一正一反;`adjacency_min_count` 须有**方向反例**(把邻居放在 −x 侧 ⇒ 不计数,与 `adj_sum` 单向查法同源)。
- [ ] 谓词 = **子句合取**:全部子句成立才匹配;任一子句不成立 ⇒ 不匹配(不得「部分匹配」)
- [ ] **多匹配裁决 = 声明序首个匹配**(2026-10-21 用户裁定 · GDD `OQ-24-7` 登记待裁)
  <br>载体 = 构造一个**同时满足两个房间类型**的布局,断言胜出者 = `ROOM_TABLE.Rooms` 中**行序靠前**那个;
  并配**缺口哨兵**(见下)使该裁决**可观测而非静默**。
- [ ] 匹配是**纯函数**:同一布局 + 同一表两次求值逐位相同;不持有缓存、不读墙钟 / 表现态(ADR-016 §一)
- [ ] 邻接子句的 `type` / `neighbor_type` 在匹配期**只读表**;未知类型不抛(由 story-001 烘焙门保证闭集),匹配期按「不成立」处理并**可观测**

---

## Implementation Notes

*Derived from ADR-015 / ADR-005 Implementation Guidelines:*

1. **落点**:`Sim/Clinic/ClinicRoomMatcher.cs`(新)。输入 = `ClinicRoomExtractionResult`(story-002)+
   `IClinicFurnitureTraitsSource`(S17-4 生产实现 `ClinicFurnitureTraitsSource`)+ `ClinicRoomTable`(story-001)。
   输出 = `string roomType` 或 `ROOM_NONE` 哨兵。**零 `UnityEngine` 引用**(门 A)。
2. **`ROOM_NONE` 表示**:与既有代码一致 —— 不要新增可空字符串;用 `bool TryMatchRoom(..., out string roomType)`,
   `false` 即 ROOM_NONE。`base_env` 的解析仍归调用方(不改 `ClinicModifierEvaluator` 的签名)。
3. **多匹配裁决**:遍历 `table.Rooms`(**声明序**),首个全子句成立者返回。**不排序、不按名字、不取最大 base_env**。
   代码注释须写明:这是 `OQ-24-7` 的**暂定实现**,裁决未定,改判时本处与载体须同步改。
4. **缺口哨兵**:新增 `ClinicRoomMatcher.MultiMatchPolicyIsProvisional => true`(与
   `FurnTraitsProductionReady` 同款形态),供测试断言「当前裁决仍是暂定的」。**OQ-24-7 裁定后须转 false 并同步改实现**
   —— 届时哨兵测试转红即提醒(不得静默改绿)。
5. **邻接子句方向**:只查 `+x` / `+y` 两向(ADR-015 §五),与 `ClinicModifierEvaluator.SumAdjacencyRaws` 同源。
   **不得**复用 `adj_sum` 的权重查表 —— 邻接子句问的是「有没有那个类型的邻居」,不是「权重和」。
6. **禁止提前造接缝**:本件只做「布局 → room_type」。`ClinicEnvDto` 交付(roomName / contexts[])**归 story-004**;
   Memoize **归 story-005**。本件不碰这两处。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: 三表 schema 与构建门(闭集 / 方向 / 区间在烘焙期已拒,匹配期不再防)
- [Story 002]: 房间析出(本件消费其 `ClinicRoomExtractionResult`)
- [Story 003]: 三乘子求值(本件只产出 `room_type`,`base_env` 仍由调用方查)
- [Story 004]: `ClinicEnvDto` 交付边界与 21a 联测
- [Story 005]: Memoize 与重放等价
- **`OQ-24-7` 的最终裁决** —— 本件只实现暂定裁决并登记待裁

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-03(ROOM_NONE 双向)**
  - Given A:空布局(`RoomCells` 空 ⇒ `HasRoom == false`)
  - Given B:有家具格,但表中无任一房间类型谓词成立
  - When: `TryMatchRoom`
  - Then: 两条**分别**返回 `false`;`ClinicEnvModOf` / `ClinicEquipModOf` 两条路径都返回 `Fix.Zero`
  - Edge cases: 有家具但表为空(表构造器已拒空表,匹配期不可达)

- **子句三型**
  - `min_count`:`bed ≥ 2` —— 1 张床不匹配 / 2 张床匹配
  - `has_type`:只有 1 个 `surgery_table` 即成立
  - `adjacency_min_count`:`surgery_table` 在 +x 侧邻接 `sterile_cabinet` ⇒ 成立;
    把 `sterile_cabinet` 挪到 −x 侧 ⇒ **不成立**(方向反例,与 `adj_sum` 单向查法同源)

- **合取语义**
  - Given: `surgery` 的两条子句只成立一条
  - Then: 不匹配(不得「部分匹配」)

- **多匹配裁决(OQ-24-7 暂定)**
  - Given: 一个同时满足 `ward`(bed ≥ 2)与另一房间类型的布局
  - Then: 返回 `Rooms` 中行序靠前者
  - 且 `MultiMatchPolicyIsProvisional == true`(哨兵;裁定后转红)

- **纯函数**
  - Given: 同一布局 + 同一表
  - When: 连续两次独立求值
  - Then: 逐位相同(承 AC-24-04 的析出侧形态,但本件不主张 story-005 的重放面)

---

## Test Evidence

**Test file**: `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_matcher_test.cs`(新建)
*(账本路径 `tests/unit/clinic_machine/` 可并列注明;一个 AC 允许多条载体)*

**证伪轮(S12-4 · 强制)**:本件命中 **① 定序类**(声明序首个匹配)· **③ 定义域收窄类**(合取 / 方向反例)
⇒ 每项须有「停用守卫 ⇒ 该测转红」的反证记录。至少覆盖:

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| 定序 | 多匹配取声明序首个 | 改遍历为逆序 ⇒ 多匹配测红 |
| 定义域收窄 | 合取语义 | 把 `&&` 改 `||` ⇒ 合取测红 |
| 定义域收窄 | 邻接方向 | 把 `+x` 查法改成双向 ⇒ 方向反例测红 |
| 定义域收窄 | ROOM_NONE 双向 | 删 `HasRoom == false` 短路 ⇒ EC-24-01 测红 |

---

## Dependencies

- **story-001**(`Complete`? 见下) —— `ClinicRoomTable` / `ClinicContextTable` 的形状
- **story-002**(`Complete`) —— `ClinicRoomExtractionResult` 与 `HasRoom` 语义
- **S17-4** —— `ClinicFurnitureTraitsSource`(生产属性源,已落)
- **OQ-24-7** —— 多匹配裁决,**待用户裁**(本件按暂定裁决实现)

---

## Completion Notes

**Completed**: —
