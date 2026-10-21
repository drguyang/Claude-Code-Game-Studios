# Story 006: 房间类型匹配(ROOM_TABLE 子句求值)+ AC-24-03

> **Epic**: 医馆即机器
> **Status**: Complete  <!-- 原 `Ready` —— 2026-10-21 实现轮(用户指令「story-006 实现」);原文保留 -->
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

- [x] AC-24-03:空布局 / 未匹配任何房间类型 ⇒ 输出中性 `(EnvMod, EquipMod) = (0, 0)`(ROOM_NONE 基例,EC-24-01 / EC-24-02)
  <br>载体须**双向**:① 空布局(EC-24-01);② 有家具但谓词全不成立(EC-24-02)—— 两者**分别**一测,
  不得只测一个就宣称覆盖(EC-24-01 是 EC-24-02 的特例,反向不成立)。
- [x] 三种子句类型各有判据:`min_count`(类型计数 ≥ min)· `has_type`(存在性)· `adjacency_min_count`(方向性邻接计数 ≥ min)
  <br>载体 = 每种至少一正一反;`adjacency_min_count` 须有**方向反例**(把邻居放在 −x 侧 ⇒ 不计数,与 `adj_sum` 单向查法同源)。
- [x] 谓词 = **子句合取**:全部子句成立才匹配;任一子句不成立 ⇒ 不匹配(不得「部分匹配」)
- [x] **多匹配裁决 = 声明序首个匹配**(2026-10-21 用户裁定 · ~~GDD `OQ-24-7` 登记待裁~~ **`OQ-24-7` 已结案**)
  <br>载体 = 构造一个**同时满足两个房间类型**的布局,断言胜出者 = `ROOM_TABLE.Rooms` 中**行序靠前**那个;
  并配**缺口哨兵**(见下)使该裁决**可观测而非静默**。
  <br>🔁 **2026-10-21 订正(QA 签核轮)**:原文「登记待裁」**已过期**。实测
  `ClinicRoomMatcher.cs:120` = `MultiMatchPolicyIsProvisional => false`;
  哨兵测 `clinic_room_matcher_test.cs:445` 断言 `Is.False`、`:479` 配双向等价断言;
  `EPIC.md:75-77` 记「2026-10-21 补齐轮用户裁定转正 ⇒ `OQ-24-7` 结案」。
  ⚠️ **「声明序」在运行期 = 产物行序**(`ClinicMachineCookedWriter.cs:123` 按 `room_type` 序数排序),
  不是 JSON 手写序 —— 裁决内容不变,勿混读。原文加删除线保留。
- [x] 匹配是**纯函数**:同一布局 + 同一表两次求值逐位相同;不持有缓存、不读墙钟 / 表现态(ADR-016 §一)
- [x] 邻接子句的 `type` / `neighbor_type` 在匹配期**只读表**;未知类型不抛(由 story-001 烘焙门保证闭集),匹配期按「不成立」处理并**可观测**

---

## Implementation Notes

*Derived from ADR-015 / ADR-005 Implementation Guidelines:*

1. **落点**:`Sim/Clinic/ClinicRoomMatcher.cs`(新)。输入 = `ClinicRoomExtractionResult`(story-002)+
   `IClinicFurnitureTraitsSource`(S17-4 生产实现 `ClinicFurnitureTraitsSource`)+ `ClinicRoomTable`(story-001)。
   输出 = `string roomType` 或 `ROOM_NONE` 哨兵。**零 `UnityEngine` 引用**(门 A)。
2. **`ROOM_NONE` 表示**:与既有代码一致 —— 不要新增可空字符串;用 `bool TryMatchRoom(..., out string roomType)`,
   `false` 即 ROOM_NONE。`base_env` 的解析仍归调用方(不改 `ClinicModifierEvaluator` 的签名)。
3. **多匹配裁决**:遍历 `table.Rooms`(**声明序**),首个全子句成立者返回。**不排序、不按名字、不取最大 base_env**。
   ~~代码注释须写明:这是 `OQ-24-7` 的**暂定实现**,裁决未定,改判时本处与载体须同步改。~~
   <br>🔁 **2026-10-21 订正(QA 签核轮)**:**`OQ-24-7` 已结案**,裁决 = 声明序首个匹配(不再是暂定)。
   本行所述的「暂定实现」状态**已结束**;原文加删除线保留。
4. **缺口哨兵**:~~新增 `ClinicRoomMatcher.MultiMatchPolicyIsProvisional => true`(与
   `FurnTraitsProductionReady` 同款形态),供测试断言「当前裁决仍是暂定的」。**OQ-24-7 裁定后须转 false 并同步改实现**
   —— 届时哨兵测试转红即提醒(不得静默改绿)。~~
   <br>🔁 **2026-10-21 订正(QA 签核轮)**:已按本行**执行完毕** —— 哨兵现值 `false`
   (`ClinicRoomMatcher.cs:120`)、哨兵测已翻转为断言 `Is.False`(`clinic_room_matcher_test.cs:445`)
   并配双向等价断言(`:479`)。原文作为**已兑现的义务记录**保留。
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
- ~~**`OQ-24-7` 的最终裁决** —— 本件只实现暂定裁决并登记待裁~~
  <br>🔁 **2026-10-21 订正(QA 签核轮)**:**`OQ-24-7` 已结案**(用户裁定「转正:声明序首个匹配」),
  哨兵已转 `false`、哨兵测已翻转。本行不再成立,原文加删除线保留。
  裁决内容 = 声明序首个匹配;运行期「声明序」= **产物行序**(见上)。

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

- **多匹配裁决(~~OQ-24-7 暂定~~ 🔁 2026-10-21 QA 签核轮:`OQ-24-7` **已结案**,裁决 = 声明序首个匹配)**
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

### 🔁 2026-10-21 补齐轮 / 末轮:反证已全部落地(15 变异 + 6 接线变异)

| 命中形态 | 主要断言 | 反证记录 |
|---|---|---|
| 定序 | 多匹配取声明序首个 | **M1** ⇒ 2 条多匹配测红(`EXACT`) |
| 定义域收窄 | 合取语义 | **M2** ⇒ 4 条红(`EXACT`) |
| 定义域收窄 | 邻接 `+x` 方向 | **M3** ⇒ `neighbourOnMinusX` 红(`EXACT`) |
| 定义域收窄 | 邻接 **`+y`** 方向 | **M15** ⇒ 恰 `neighbourOnMinusY` 1 条红(`total=55/54/1`,`EXACT`) |
| 定义域收窄 | ROOM_NONE 双向 | **M4** ⇒ 2 条 ROOM_NONE 测红(`EXACT`) |
| 定义域收窄 | 未知类型可观测 | **M5 / M6** ⇒ 各恰 1 条红(`EXACT`) |
| 阈值边界 | 邻接 `min` | **M9** ⇒ `requiresBoth` 红(`EXACT`) |
| 阈值边界 | `int.MaxValue` 守卫 | **M10** ⇒ 析出层 1 条红(`EXACT`;匹配层同形态守卫**无变异可证面**,已如实登记) |
| 一次性 / 无缓存 | 纯函数无跨调用缓存 | **M7 / M14**(手动)⇒ 各红(`M14` 实得 2 红,原写目标测保持绿 —— 已如实登记) |

**合计 15/15 `EXACT`**(除 M14 的预期偏差已单独登记);全部还原 `sha256` 逐字节通过,零 `FALSIFY` 残留。
**复算**:`python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/s17-6-clinic-room-matcher.json
--repo . --guard unity/Assets/DataCooked --guard unity/Assets/AddressableAssetsData`
⇒ 2026-10-21 16:0x 重跑 **15 个变异全部恰如预期**,退出码 0。
⚠️ **复算前置**:锚点须先全绿(`check_all_anchors.py` ⇒ 18/18 份 spec)。

**接线面另有 V1–V6**,见 `impl-s17-6-wiring-2026-10-21.md` §四(**5/6 EXACT + V6 过红**,如实登记)。

---

## Dependencies

- **story-001**(`Complete`? 见下) —— `ClinicRoomTable` / `ClinicContextTable` 的形状
- **story-002**(`Complete`) —— `ClinicRoomExtractionResult` 与 `HasRoom` 语义
- **S17-4** —— `ClinicFurnitureTraitsSource`(生产属性源,已落)
- ~~**OQ-24-7** —— 多匹配裁决,**待用户裁**(本件按暂定裁决实现)~~
  <br>🔁 **2026-10-21 订正(QA 签核轮)**:**已结案** —— 用户裁定「转正:声明序首个匹配」,
  哨兵 `MultiMatchPolicyIsProvisional` 已转 `false`。原文加删除线保留。

---

## Completion Notes

**Completed**: **2026-10-21**  <!-- 原值 `—`(未完成);原文保留 -->

**Criteria**: **6/6 passing**(AC-24-03 双向 + 子句三型 + 合取 + 多匹配定序 + 纯函数 + 未知类型可观测)
**Test Evidence**: Logic —— `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_matcher_test.cs`(~~**24 测**~~ **32 测**)
  + PlayMode `unity/Assets/Tests/PlayMode/ClinicMachine/clinic_room_matching_player_test.cs`(5 测,IL2CPP 真跑 `5/5/0/0`)
  <br>🔁 **2026-10-21 订正(QA 签核轮)**:实测 `grep -c '^\s*\[Test\]'` = **32**,非 24。
  24 是**建 story 时的估计值被误记为实测值**;实现轮实际落了 32 条。原文加删除线保留。
  溯源:`2026-10-21 16:51 CST` · 批次 `sprint-17 QA 签核轮全量 EditMode` · `e0ad379`+工作树 ·
  `4350/4314/0/36`(集合差 **新增 3 · 消失 0**,本轮增量与本 story 无关)。
**Code Review**: **已补做**(2026-10-21 补齐轮 · 三路并行 `lead-programmer` / `qa-tester` / `unity-specialist`)。
原件 `production/qa/evidence/review-s17-6-room-matcher-2026-10-21.md`。
⚠️ **该报告评的是补齐轮时点的代码,不追认原实现轮的判定**(承 `review-workflow.md` §不可追补)。
原实现轮确实**未**被评审 —— 这一事实不因补做而回溯改写。

### 落地

| 文件 | 状态 |
|---|---|
| `unity/Assets/Sim/Clinic/ClinicRoomMatcher.cs` | 新建(生产实现,门 A 零引擎) |
| `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_matcher_test.cs` | 新建(~~24 测~~ **32 测** —— 🔁 2026-10-21 QA 签核轮实测订正) |
| `tools/falsify/specs/s17-6-clinic-room-matcher.json` | 新建(6 自动变异 + 1 手动) |
| `production/qa/evidence/impl-s17-6-room-matcher-2026-10-21.md` | 证据原件 |

**零既有文件改动** —— 纯新增。

### 证伪轮(S12-4 · BLOCKING)

**7/7 恰如预期**(6 自动 exactRed + 1 手动复验),受保护路径指纹未变:

| 变异 | 形态 | 停用 ⇒ 转红 |
|---|---|---|
| M1 | ① 定序 | 声明序首个 → 最后者胜 ⇒ 多匹配 2 测红 |
| M2 | ③ 定义域收窄 | 合取 → 只看首子句 ⇒ 4 测红 |
| M3 | ③ 定义域收窄 | `+x` 单向 → 双向 ⇒ −x 方向反例红 |
| M4 | ③ 定义域收窄 | 删 `HasRoom == false` 短路 ⇒ 2 条 ROOM_NONE 测红 |
| M5 | ③ 定义域收窄 | 邻接 `neighbor_type` 引用不计数 ⇒ 1 测红 |
| M6 | ③ 定义域收窄 | 格属性 `Unknown` 静默跳过 ⇒ 1 测红 |
| M7(手动) | ⑤ 一次性 / 无缓存 | 引入跨调用缓存 ⇒ `test_purity_noCache_acrossDifferentLayouts` 红 |

⚠️ **M7 未走自动驱动** —— 缓存需三处改动,超出「单行替换」方法论;驱动里标 `skip: true`
+ `_manualProof`,改 → 跑 → 还原全在同一轮内完成,`sha256sum -c` 逐字节通过(未用 `git checkout`)。
⚠️ **M6 首版形态编译失败**(`if (false) throw` ⇒ switch 段落穿透)⇒ 换形「`Unknown` 单开 `break`」后
恰 1 条转红。教训已写进 spec 的 `_note_on_form`。两处均见证据件 §3.2 / §3.3。

### 回归

`4315 / 4279 / 0 / 36` @ 2026-10-21 13:50–13:52 CST · commit `09340e6`+工作树
(`unity/Logs/s17-6-full-editmode.xml`)。基线 `4291 / 4255 / 0 / 36` @ 12:38
⇒ **+24 / +24 / 0 / 0**;集合差「新增 24 · 消失 0」⇒ **零回归**。

### 偏离 / 自陈边界(禁借绿)

- ~~**`OQ-24-7` 未裁**~~ —— ✅ **2026-10-21 用户裁定「转正:声明序首个匹配」**。
  哨兵 `MultiMatchPolicyIsProvisional` 已由 `true` → **`false`**;实现无需改(暂定实现即裁定内容);
  哨兵测已翻(`Is.True` → `Is.False`),另配 `test_multiMatch_policyIsProvisional_isDetectable` 的双向等价断言。
  **未静默改绿** —— 翻转过程见 `impl-s17-6-wiring-2026-10-21.md` §二。
  <br>⚠️ **同轮发现一处语义订正**:「**声明序**」在运行期 = **产物行序**,**不是** JSON 手写序
  (`ClinicMachineCookedWriter.cs:123` 按 `room_type` 序数排序 ⇒ 实际序 `herb_room, surgery, ward`)。
  裁决**内容**不变,但读者不得把「声明序」读成「JSON 手写序」。
- **AC-24-03 的「⇒ `(0,0)` 中性输出」这一半由 story-003 承担** —— 本件只产出 `room_type` /
  `ROOM_NONE`,不改 `ClinicModifierEvaluator` 签名。两件合起来才是完整的 AC-24-03。
- **M4 的承重面是诊断契约,不是返回值** —— 删短路不改变返回值,只让 `UnknownClauseTypeCount`
  从 0 变非零。卡面证伪表原写「⇒ EC-24-01 测红」,在本实现形态下由「诊断全零」这一条断言承载。
- **M7 只能钉住「缓存泄漏」** —— 一个按 `RoomId` 正确分键的缓存不可观测。
- **「未知类型」在本件被拆成两件**:① 布局里没有该类型的格 ⇒ 不成立 + 可观测(**本件**);
  ② 该类型不在 `FURN_TYPES` 闭集 ⇒ **烘焙期**判据(归 story-001),匹配期无从判定,本件不假装判定。
- ~~**未接线到装配根**~~ —— ✅ **2026-10-21 接线轮已落**(用户裁定「全链真接线(含 23 侧)」)。
  `ClinicRoomMatchingService` + `CompositionRoot` 装配段 + 23 侧 `EnumerateInstances()`;
  证据 `production/qa/evidence/impl-s17-6-wiring-2026-10-21.md`。
  ⚠️ **但布局来源是裸 `StructureInstanceRegistry`,不是 `World`** —— `TD-building-01` 判据的
  `World` 半边与 `IsEffectivelyWalkable` 端到端**仍未验**。
- ~~**IL2CPP 腿未跑**~~ —— ✅ **2026-10-21 真跑通**(用户裁定「加 PlayMode 载体 + 真跑」)。
  载体 `Tests/PlayMode/ClinicMachine/clinic_room_matching_player_test.cs`(5 测),
  在 **Linux-x64-IL2CPP player** 上 **5/5 通过 · 0 Inconclusive · 退出码 0**
  (`unity/Logs/s17-6s-il2cpp-player.xml`,`_APPDOMAIN = IL2CPP Root Domain`)。详见接线件 §5.1 / §8.1。
  ⚠️ **两处如实登记**:
  (a) **构建首跑是红的,根因在既有代码** —— 4 个既有 PlayMode 文件裸用 `UnityEditor.AssetDatabase`
  ⇒ IL2CPP player 构建自 **2026-10-13** 起结构性失败(与 S17-6 无关)。用户裁定「就地修 + 重跑」,
  已加方法级 `#if !UNITY_EDITOR` 守卫修好;该缺口已立 **`TD-testasm-01`** 跟踪
  (仍缺**机械门**,现靠构建失败发现)。
  (b) ~~`Assets/DataCooked/` 不进 player ⇒ 第二段 `Inconclusive`~~ ⇒ **该判断已被本轮自身推翻**:
  `DataCooked/` 确实不在 player 里(磁盘兜底路径在 player 必然抛),但 **cooked 产物已经随
  `data-core` Addressables 组进了 player 构建** ⇒ 传 `new AddressablesDataProvider()` 即可读到。
  首版的 3 条 `Inconclusive` 是**夹具设计错误**,已改为 player 内走 Addressables,
  三条测**现在在 player 里也真跑**。**「cooked 产物随 player 分发」这条缺口不存在。**
  ⚠️ 仍**不是三格矩阵齐** —— 本件只跑 Linux-x64-IL2CPP 一格;ARM64 目标不存在(用户裁定长期未达),
  Windows / Apple Silicon 归发版前。
