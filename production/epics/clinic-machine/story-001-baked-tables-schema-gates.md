# Story 001: 烘焙表与 schema 构建门

> **Epic**: 医馆即机器
> **Status**: In Review
> **Layer**: Core
> **Type**: Config-Data
> **Estimate**: 6h
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-20

> ⚠️ **取件轮定案(2026-10-20)** —— 两条 AC 半边挂标,本体同批交:
> **`AC-24-08` / `AC-24-12` 挂 `BLOCKED-BY-O-24-1`**。
> 病因:`FURN_TYPES` 闭集的来源 = 21a 家具目录扩展 `type`/`tier`/`e_env`(`O-24-1`),
> 而该扩展**实测零落点**(`FURN_TYPES` 全库零定义 · `item_database_items.json` 无三字段 ·
> 21a epic 已 `Complete 12/12` 却零 story 认领)⇒ **两条判据结构上不可达**。
> 已登记 **`TD-clinic-01`**(`docs/tech-debt-register.md`);路线图 §三 **G-8**。
> **用户裁定**:维持 GDD `:297`「`FURN_TABLE` 归 21a」;**不给已 Complete 的 21a 开新 story**;
> 本 story **本体同批交**(`CONTEXT_TABLE` / `ADJ_TABLE` / `ROOM_TABLE` 的 schema 与构建门
> + `O-24-5` 溢出界门)—— 承 A-2「本体交 + 半边挂标」形态。
> ⚠️ **另留一处张力**:Implementation Note #1 写表落 `assets/data/clinicmachine_furn.json`,
> 与 GDD `:297`(目录归 21a)**未消** —— 本轮**不开工该表**,留待 24 的 GDD 回刷轮 / 21a 改版轮。
## Context

**GDD**: `design/gdd/clinic-machine.md`(规则二 ROOM_TABLE · 规则三 CONTEXT_TABLE · 规则四/五 表字段 · O-24-5 烘焙 schema 断言 · O-24-4 ENV_MOD_MIN)
**Requirement**: TR-clinic-004(CONTEXT_TABLE 经 ADR-014 烘焙管线出货;条目数硬上限 K_CONTEXT_MAX) · TR-clinic-005(ADJ_TABLE 对称性 = 加载期硬门) · TR-clinic-006(加成叠乘的整数溢出断言)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(主): 数据管线与 JSON 解析器
**ADR Decision Summary**: 作者态 `assets/data/clinicmachine_*.json` → 两阶段工具链(阶段1 `JsonTextReader` 仅词法 · 阶段2 自研 per-schema 绑定 + `FixParse` + 白名单/schema_version 校验)烘为确定性 `*.cooked`;`Fix` 字段 JSON 写字符串;玩家构建零 JSON 解析器。ROOM_TABLE 烘 `clinicmachine_room.json`,**明确不归 ADR-022 关卡工具**(24 自有表)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 烘焙工具为编辑期 .NET 代码(Editor.Tools 族,不进构建);不依赖 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 全部 `Fix` 字段经 `FixParse` 解析(JSON 字符串形式);常量冻结值(`TIER_MAX=3` / `EQUIP_MOD_CAP=1/4` / `ENV_MIN/MAX=∓1/2` / `K_speed={1,7/8,3/4}` / W 域 `[−1/4,+1/4]`)按 2026-09-25 裁定值落表
- Forbidden: JSON 数字字面量直接经 `double`/`decimal` 中转(绕 `FixParse` 的浮点泄漏);禁 `JsonConvert.DeserializeObject<T>`
- Guardrail: O-24-5 四项断言 + AC-24-05 违规 = 构建期 `throw` 硬失败,非运行时告警

---

## Acceptance Criteria

*From GDD `design/gdd/clinic-machine.md`, scoped to this story:*

- [x] AC-24-05:CONTEXT_TABLE 每项 `K_speed ∈ (0,1]`;违例 fixture(`invalid_context_table.json`)构建期拒绝(throw)
- [x] AC-24-06:ADJ_TABLE 对称性 `w(a,b)==w(b,a)` 为烘焙期硬门;违例 fixture(`invalid_adj_table.json`)构建失败
- [ ] **`BLOCKED-BY-O-24-1`** AC-24-08:FURN_TABLE 字段断言通过(字段类型 ∈ 整数域 / Fix 字符串;缺字段 = 构建失败)
- [ ] **`BLOCKED-BY-O-24-1`** AC-24-12:ROOM_TABLE 谓词子句类型闭集 ⊆ FURN_TYPES(未知子句类型 = 构建失败)
- [x] O-24-5 溢出界断言:`n_max × TIER_MAX < 2^31` 且 `|W_MIN| ≤ W_max`(fixture 双向覆盖)
- [x] O-24-4:`ENV_MOD_MIN < 0` 成立(21a 联动项,本 story 只断言表值)
- [x] `K_CONTEXT_MAX = max(K_speed)` 由烘焙表派生并作为导出值可供系统 1 读取(规则三)

> ⚠️ **`AC-24-08` / `AC-24-12` 的挂标语义**:两条**不得勾、不得记 `Complete`**,直至 `O-24-1` 兑现
> (见头部定案块 + `TD-clinic-01`)。**本体半边仍须实现**(`FURN_TABLE` 的 schema 形状与
> 未知子句类型拒收规则可先落 —— 缺的只是「闭集从哪来」);**取件轮按「本体交 + 半边挂标」推进**。
> ⚠️ **`O-24-5` 参数已有着落**(原卡面未记):`OQ-24-5` **已于 2026-10-07 裁定 `n_max = 64`**
> ⇒ `64 × 3 = 192 < 2^31` ✓;`W` 域已裁 `[−1/4, +1/4]` ⇒ `|W_MIN| = W_MAX = 1/4` ✓。
> (registry 的 TR-clinic-006 note 仍写「未裁」= 陈旧,已记路线图 §七 **SP-9**。)

---

## Implementation Notes

*Derived from ADR-014 Implementation Guidelines:*

1. 表落 `assets/data/clinicmachine_{room,context,adj,furn}.json`(承既有 `[system]_[name]` 先例),经 ADR-014 阶段1/阶段2 工具链烘为 `*.cooked`,入 `data-core` Addressables 组。
2. 阶段2 校验器逐表实现:`FixParse` 白名单(拒浮点字面量)+ 区间校验(`K_speed`、W 域、ENV 域)+ schema_version 头。
3. ADJ 对称性检查在**绑定后**做(遍历全部 `(a,b)` 对断言 `w(a,b)==w(b,a)`),失败 `throw` 显式消息含表名与键对。
4. ROOM_TABLE 谓词语言先定义子句类型枚举(邻接类子句在内,AC-24-12 允许),逐子句对 FURN_TYPES 闭集校验。
5. 两个 invalid fixture 进 `tests/` 夹具目录,构建门测试断言「fixture 必被拒」而非「真表必过」的单向覆盖。
6. 逐值调参(各 FURN 的 e_env / tier 分布、W 具体值)归用户数值轮 —— 本 story 只落 schema 与门,不拍内容值。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 房间析出算法与连通规则(读 cooked 表,不产表)
- [Story 003]: EnvMod/EquipMod 求值公式
- [Story 004]: 乘子交付边界与 ClinicEnvDto
- [Story 005]: 纯函数重放与 Memoize

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(K_speed 区间门)**: 非法 CONTEXT 表被构建期拒绝
  - Given: `invalid_context_table.json`(含 `K_speed = "5/4"`)
  - When: 运行阶段2 烘焙校验
  - Then: 抛构建失败,消息含条目 id 与违例值
  - Edge cases: `K_speed = "0/1"`(下界开区间)同样拒绝;`"1/1"` 通过
- **AC-2(ADJ 对称门)**: 不对称权重对被拒
  - Given: `invalid_adj_table.json`(`w(a,b)=1/8, w(b,a)=0`)
  - When: 烘焙校验
  - Then: 构建失败;对称表通过
  - Edge cases: 单向缺项(仅有 (a,b) 无 (b,a))视为不对称,拒绝
- **AC-3(溢出界)**: `n_max×TIER_MAX ≥ 2^31` 的 fixture 被拒
  - Given: 超限 n_max 的表
  - When: 烘焙校验
  - Then: 构建失败(AC-24-06 配套的 O-24-5 断言)
  - Edge cases: 恰 `2^31−1` 通过

---

## Test Evidence

**Story Type**: Config-Data
**Required evidence**: `unity/Assets/Tests/EditMode/ClinicMachine/clinicmachine_tables_schema_test.cs` — must exist and pass(含两个 invalid fixture 的拒绝断言)
**Status**: [x] Created — **29 个测试函数**,全 `Passed`
**全量回归**: `4041 / 4005 / 0 / 36` · `2026-10-20 18:44:31 +0800` · `story-001 最终全量` · `73d20a4`+工作树(退出码 0)
**证伪轮**: **13/13 转红** —— 见原件 `production/qa/evidence/impl-clinic-001-2026-10-20.md` §四
(F4 首轮存活 → 夹具被 `base_env` 域校验借绿,已修断言并复核转红;F13 首轮为探针打错靶,修正后转红)

---

## Dependencies

- Depends on: ADR-014 两阶段工具链已可运行(item-database / skill-system epic 已建的烘焙面可复用)
- Unlocks: Story 002(房间析出需读 cooked 表)、Story 003(乘子求值需 CONTEXT/ADJ)

---

## Completion Notes

**2026-10-20 · 实现轮(本体交 · 两条 AC 半边维持挂标)**

- **交付**:3 份作者态表(`clinicmachine_{context,adj,room}.json`)· 6 个烘焙工具件
  (`ClinicMachine{Schema,ContextBinder,AdjBinder,RoomBinder,CookedWriter,TablesBake}.cs`)·
  29 个测试函数 · 15 个夹具。证据原件 `production/qa/evidence/impl-clinic-001-2026-10-20.md`。
- **两处裁定落地**(协调方 2026-10-20):
  ① `n_max` 进 `clinicmachine_adj.json` 根(24 自有参数),`TIER_MAX` **不回声进 24 的数据** ——
  以 `ClinicMachineSchema.TierMax = 3L` 具名常数承接(引 `OQ-24-2`),避免第二真源;
  溢出夹具仍为真 JSON 夹具,真边界 `n_max` = **715827882 通过 / 715827883 拒绝**。
  ② AC-24-08 / AC-24-12 的闭集半边**不伪造闭集**:接缝以**注入参数**形式存在,测试用**测试内局部替身**
  (`StubFurnSource`,显式标注「非真闭集」)证接缝通电,并以 `FurnMembershipEnforced == false`
  缺口哨兵使缺席**可观测而非静默**。
- **证伪轮发现的真缺陷**:`test_constants_env_mod_min_nonnegative_rejected` 原断言命中
  `ENV_MOD_MIN` 这一**跨守卫共享 token**,被 `base_env` 域校验借绿;已改为钉 O-24-4 守卫自己的报文前缀
  `"O-24-4 失败"`,复跑转红。
- **未转 `Complete` 的原因**:`AC-24-08` / `AC-24-12` 的闭集来源 = 21a 家具目录扩展 `O-24-1`,
  **实测零落点**(`TD-clinic-01` · 路线图 §三 G-8)⇒ 两条结构上不可达。story 置 **`In Review`**。
- **未跑项**(禁借绿):`|W_MIN| ≤ W_MAX` 分支未单独变异 · 11 条正向接受类测试未入证伪轮 ·
  cooked → Addressables 接线不在本 story · IL2CPP 三格 NOT RUN(本机仅 Mono) · 逐值调参归用户数值轮。
