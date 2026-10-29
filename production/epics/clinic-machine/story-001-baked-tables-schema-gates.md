# Story 001: 烘焙表与 schema 构建门

> **Epic**: 医馆即机器
> **Status**: Complete
> **Layer**: Core
> **Type**: Config-Data
> **Estimate**: 6h
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-29

> **🔁 2026-10-21 挂标已解除(S17-4)** —— `O-24-1` 已兑现,闭集来源落地于
> `assets/data/item_database_furniture.json`(21a 侧),生产链路强制由
> `test_productionMenuPath_coreForcesFurnClosure` 钉住。详见 §Test Evidence「补记之二」。
> **下列原文保留作沿革记录**(挂标语义已失效,勿按原文读):
>
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
- [x] ~~**`BLOCKED-BY-O-24-1`**~~ AC-24-08:FURN_TABLE 字段断言通过(字段类型 ∈ 整数域 / Fix 字符串;缺字段 = 构建失败)
  <br>🔁 **2026-10-21 解除挂标**(S17-4):闭集来源 `assets/data/item_database_furniture.json` 已落地;
  三条域门各有负向夹具(`ClinicFurnitureCatalogueTest` §一)
- [x] ~~**`BLOCKED-BY-O-24-1`**~~ AC-24-12:ROOM_TABLE 谓词子句类型闭集 ⊆ FURN_TYPES(未知子句类型 = 构建失败)
  <br>🔁 **2026-10-21 解除挂标**(S17-4):(a) 半边 2026-10-20 已证;(b) 半边由
  `test_productionMenuPath_coreForcesFurnClosure` 钉住(`FurnMembershipEnforced == true` + 逐子句 ∈ 闭集)
- [x] O-24-5 溢出界断言:`n_max × TIER_MAX < 2^31` 且 `|W_MIN| ≤ W_max`(fixture 双向覆盖)
- [x] O-24-4:`ENV_MOD_MIN < 0` 成立(21a 联动项,本 story 只断言表值)
- [x] `K_CONTEXT_MAX = max(K_speed)` 由烘焙表派生并作为导出值可供系统 1 读取(规则三)

> 🔁 **2026-10-21**:下列挂标语义**已失效**(`O-24-1` 已兑现,两条可勾)。原文保留。
>
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
**Status**: [x] Created — **42 个测试函数**(实现轮 29 + 评审修复轮 13),全 `Passed`
**全量回归**: `4054 / 4018 / 0 / 36` · `2026-10-20 19:10:23 +0800` · `修复轮全量 EditMode` · `fb87e2a`+工作树(退出码 0)
**证伪轮**: **19/19 转红** —— 实现轮 13(F1–F13)+ 评审修复轮 6(G1–G6)。
见原件 `production/qa/evidence/impl-clinic-001-2026-10-20.md` §四 / §七,
评审原件 `production/qa/evidence/review-clinic-001-2026-10-20.md` §七。
(F4 首轮存活 → 夹具被 `base_env` 域校验借绿,已修断言并复核转红;F13 首轮为探针打错靶,修正后转红)

### 2026-10-21 补记 —— 三表**补烘落盘 + cooked→Addressables 接线**(S17-2 前置)

> 上列 42 测与 19/19 证伪轮为 story-001 本体面,原文保留。本节补的是本体之外**从未跑过**的两件事:
> 三份产物此前**从未落盘**,运行期装载面**从未接线**。由 S17-2(乘子求值)倒逼补齐。

**① 补烘落盘**(`DataBakeMenu.BakeClinicMachineTables()`,:535)

- `unity/Assets/DataCooked/clinicmachine_{context,adj,room}.cooked.bytes`(+meta),`ConfigVersion = 0x4301F2B1`
- 烘焙日志逐字:`FURN_TYPES 闭集 = NOT-RUN — O-24-1 未兑现(TD-clinic-01)`

**② 运行期接线**(四处闭合,由 `provider_wiring_gate_test` 守)

| 面 | 落点 |
|---|---|
| Sim 侧表 | `unity/Assets/Sim/Clinic/ClinicMachineTables.cs`(`ClinicContextTable` / `ClinicAdjTable` / `ClinicRoomTable` + 镜像枚举 + 构造期拒收) |
| 运行期解码 | `unity/Assets/Gameplay.Presentation/ClinicMachineCookedCodec.cs` |
| Provider 支路 | `AddressablesDataProvider`:三 address(`:139` 区)/ 三 `Load<T>` / 三 `Ensure*` / `Preload()` 三调用(`:323-325`) |
| 预载清单 | `DataCorePreloader` 16 → 19 条 |
| 组注册 | `data-core.asset` ⇒ 19 条地址 |

**③ 证据(本补记的判据)**

| 项 | 结果 | 依据 |
|---|---|---|
| 盘上 ↔ 现烘逐字段 | 16 测全绿(三表各一条 disk↔fresh) | `ClinicMachineCookedCodecTest` |
| 四处闭合(门①②③④) | 11 测全绿 | `ProviderWiringGateTest`(S17-2 起含三条 clinic address 断言) |
| 漂移门登记 | 23 测全绿(三份产物进 `BakedArtifacts`,带现烘委托) | `cooked_drift_all_artifacts_test` |
| 证伪 | W1(地址改一字符)⇒ 恰 2 条红 · W2(`Preload` 漏一条)⇒ 恰 1 条红 | `tools/falsify/specs/s17-2-clinic-wiring.json` |
| 只读视图 | `Rows`/`Rooms` 不可强转回数组;R1/R2 各恰 1 条红 | `s17-2-clinic-readonly.json` |

**⚠️ 本轮抓到的两处真实欠账(不是夹具问题)**:
- 首跑全量 `cooked_drift_all_artifacts_test` **两条红**:三份产物已落盘但未进漂移门登记表(`pinnedDeclared` 16 ≠ 19、幽灵产物点名三份)
  ⇒ **「补烘 ≠ 登记 ≠ 接线」是三件事**;修法 = 加 `BakedArtifacts` 三条 + `pinnedDeclared` 19。
- `ClinicAdjTable` 首版按「有向条目数」比对自对 ⇒ 自对 `(bed,bed)` 被计 2 次,误报对称性缺失;已改为按无序键首次插入累加。

**回归**:`4237 / 4201 / 0 / 36` · `2026-10-21 09:02 CST` · `sprint-17 批 2 S17-2 收尾轮` · `3a22dbf`+工作树;
对 `4198 / 4162 / 0 / 36 @ 08:02 · 1dde232` 做 `fullname` 集合差 = **Δ +39 / 消失 0**(零回归)。
原件:`production/qa/evidence/impl-s17-2-clinic-modifier-eval-2026-10-21.md` §一 / §三。

### 2026-10-21 补记之二 —— **`O-24-1` 兑现:闭集来源落地**(S17-4)

> 上两节原文保留。本节补的是**挂标两条 AC 的上游** —— `FURN_TABLE` / `FURN_TYPES`
> 此前「双向登记在册、零承载」,故 `AC-24-08` / `AC-24-12` **结构上不可达**。本件把那根断链接上。

**① 交付面(21a 侧 = 闭集来源)**

| 文件 | 内容 |
|---|---|
| `assets/data/item_database_furniture.json`(新) | 作者态:`tier_max:3` + `furn[]` **7 行**(`bed` / `surgery_table` / `sterile_cabinet` / `herb_cabinet` / `grinder` / `drying_rack` / `chamber_pot`) |
| `unity/Assets/Editor.Tools.Bake/ItemDatabaseFurniture{Binder,CookedWriter,Bake}.cs`(新) | 三条域门(`tier ∈ [0,tier_max]` · `\|e_env\| ≤ C_max=1/64` · `tier_max` 与 24 对账)+ deterministic 编码 |
| `unity/Assets/DataCooked/item_database_furniture.cooked.bytes`(新) | **216 B / 7 类型**,`ConfigVersion = 0xD0DC16A9` |

**② 交付面(24 侧 = 消费)**

| 面 | 落点 |
|---|---|
| 运行期表 | `Sim/Clinic/ClinicFurnitureCatalogue.cs`(+ `FurnTypes` 闭集) |
| 解码 | `Gameplay.Presentation/ClinicFurnitureCookedCodec.cs` |
| Provider 支路 | `AddressablesDataProvider`:第 20 条 address + `Load<ClinicFurnitureDataSet>` + `EnsureClinicFurniture()` + `Preload()` |
| 预载清单 | `DataCorePreloader` 19 → **20** |
| 组注册 | `data-core` 19 → **20** 条(经 `EnsureDataCoreGroup` 菜单) |
| **生产属性源** | `Sim/Clinic/ClinicFurnitureTraitsSource.cs`(三值:`Furniture`/`NonFurniture`/`Unknown`) |
| 真判据入口 | `ClinicRoomExtraction.Extract(view, furnTypes)` 重载 |
| 哨兵 | `ClinicModifierEvaluator.FurnTraitsProductionReady => **true**` |

**③ 证据(本补记的判据)**

| 项 | 结果 | 依据 |
|---|---|---|
| 烘焙期域门(AC-24-08) | **10 条 = 9 负向 + 1 正向**(`awk` 切 §一 段实测);负向全拒,正向种子通过 | `ClinicFurnitureCatalogueTest` §一 |
| 读方↔写方镜像 | 盘上产物 ↔ 现烘**逐字节**相同 | `test_furniture_diskMatchesFreshBake` |
| **AC-24-12 闭集强制(生产链路)** | `FurnMembershipEnforced == true` 且每个子句 type ∈ 闭集 | `test_productionMenuPath_coreForcesFurnClosure` |
| 证伪 | 4 变异全部 `exactRed` 恰中(F1 红 2 / F2 红 3 / F3 红 2 / **F4 红 1**) | `tools/falsify/specs/s17-4-furniture-catalogue.json` |
| 全量回归 | `4265 / 4229 / 0 / 36` · `2026-10-21 10:17 CST` · `S17-4` · `15fcda8`+工作树 | `unity/Logs/s17-4-full-regression-r2.xml` |

**⚠️ 本轮抓到的一处真实半接线态(不是夹具问题)**:首跑全量 `ProviderWiringGateTest` **1 红** ——
`item_database_furniture` 的产物烘了、`Preload()` 调了、`DataCorePreloader` 登记了,
**但 `data-core` 组里没注册条目**(19 vs 20)⇒ 运行期会 E-13 启动期硬失败。
修复 = 走生产菜单 `EnsureDataCoreGroup` 组内补条目(未手改 `.asset`)。
**这是「补烘 ≠ 登记 ≠ 接线」第三次现身** —— 漂移门(产物)与接线门④(Preload)全绿,
只有接线门②(组条目)看得见。

**④ 挂标解除**

| AC | 变化 |
|---|---|
| `AC-24-08` | `BLOCKED-BY-O-24-1` **解除** —— 字段类型 ∈ 整数域 / `Fix` 字符串 / 缺字段 = 构建失败,三条域门各有负向夹具 |
| `AC-24-12` | `BLOCKED-BY-O-24-1` **解除** —— (a) 半边 2026-10-20 已证;(b) 半边由本件 `FurnMembershipEnforced == true` 钉住 |

原件:`production/qa/evidence/impl-s17-4-furniture-catalogue-2026-10-21.md`。

**⚠️ 仍未转 `Complete`,理由已变**:原理由是两条 AC 挂标(**已解除**);现为
① `FurnTypeClosureEnforced` 仍 `false`(默认单参入口走代理判据,**装配层尚未选真判据入口**);
② §评审残余 16 条 🟡(族 A 判别力借绿族 9 条)未修;
③ IL2CPP 三格 NOT-RUN。

### 2026-10-29 补证伪轮 —— R-17 的 8 条边界夹具(收口前置)

> **由来**:评审原件 §七之三 第 1 条登记「R-17 的 9 条新测只抽证 1 例(G3);其余 8 条判别力未经反证」。
> 2026-10-29 收口轮按要求**逐条补证**。

| 变异 | 被停用的守卫 | 转红夹具 | 结果 |
|---|---|---|---|
| **M1** | `ClinicMachineContextBinder` `contexts` 空表守卫 | `test_context_empty_table_rejected` | **RED 命中** |
| **M2** | `ClinicMachineRoomBinder` `rooms` 空表守卫 | `test_room_empty_table_rejected` | **RED 命中** |
| **M3** | `ClinicMachineRoomBinder` `clauses` 空数组守卫 | `test_room_empty_clauses_rejected` | **RED 命中** |
| **M4** | `ClinicMachineAdjBinder` 重复键守卫 | `test_adj_duplicate_pair_rejected` | **RED 命中** |
| **M5** | `ClinicMachineContextBinder` 重复 `room_type` 守卫 | `test_context_duplicate_room_type_rejected` | **RED 命中** |
| **M6** | `ClinicMachineSchema` `nMax <= 0` 守卫 | `test_overflow_n_max_nonpositive_rejected` | **RED 命中** |
| **M7** | `ClinicMachineSchema` `wMinRaw < 0` 守卫 | `test_adj_wmin_equals_wmax_rejected` | **RED 命中** |
| **M8** | `ClinicMachineSchema` Fix 值 `null` 守卫 | `test_context_null_fix_value_rejected` | **RED 命中** |

**每条都是 `total=42` 下恰好 1 条红,且就是期望的那条** ⇒ ① 该夹具有判别力;
② 无交叉污染(不误伤邻条)。**8/8。**

**还原**:每条 `cp` 回原文件后 **SHA-256 逐字节**比对一致(`RESTORE-OK` ×8);
轮末 `git status --porcelain unity/Assets/Editor.Tools.Bake/` **零输出**。

> ⚠️ **首轮作废并留痕(2026-10-29)**:第一次跑把**文件名**当类名
> (`…ClinicMachine.clinicmachine_tables_schema_test`),8 条**全部 `total=0`(一条没跑)**而退出码 0;
> 且驱动器**没有 `total==0` 守卫**,把「没跑到」记成了 `RED=NONE` —— 与 story-006 同型的坑,第二次。
> 已重写驱动器:`total==0 ⇒ FILTER-MISS` 并**中止整轮**、要求**期望夹具在红名单里**、
> 变异后全绿则判 `GREEN-假存活`。修正后先跑基线探测 **`42/42`** 确认过滤器命中,才开跑。

> 🔁 **一处少报的更正**:原文「R-17 的 9 条新测**只抽证 1 例**」不准确 ——
> 评审原件的 `G5` / `G6` 打的 `test_furn_empty_table_rejected` / `test_furn_duplicate_type_rejected`
> **也在这 9 条里** ⇒ 当时至少覆盖 **3 条**,不是 1 条。本轮补 8 条后,
> 该族除 `test_adj_empty_pairs_rejected`(G3 已证)外**全部有反证**。


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
  ~~cooked → Addressables 接线不在本 story~~ **✅ 2026-10-21 已补(见 §Test Evidence「补记」)** ·
  IL2CPP 三格 NOT RUN(本机仅 Mono) · 逐值调参归用户数值轮。<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。
- **2026-10-21 补记**:三表补烘落盘 + 四处接线闭合已落,判据见 §Test Evidence「补记」。
  **`Status` 仍 `In Review`** —— `AC-24-08` / `AC-24-12` 的闭集半边仍挂 `BLOCKED-BY-O-24-1`,
  两条**仍不可勾**;接线补齐不动摇该挂标。
  > 🔁 **2026-10-21 稍后订正(晚于上句)** —— 上面这条补记写于 **S17-4 解除挂标之前**,
  > 其「仍挂 `BLOCKED-BY-O-24-1` / 两条仍不可勾」**已被同日的 S17-4 取代**:
  > `O-24-1` 已兑现(闭集来源 `assets/data/item_database_furniture.json` 落地),
  > 两条 AC 在 §Acceptance Criteria 已勾(`[x] ~~**BLOCKED-BY-O-24-1**~~`)。
  > ⇒ **本 story 现仍为 `In Review`,但理由已换** —— 见上方 §Test Evidence「补记之二」④ 之后的
  > 三条(**① `FurnTypeClosureEnforced` 仍 `false` · ② 评审残余 🟡 未修 · ③ IL2CPP 三格 NOT-RUN**),
  > **不再是挂标**。原文保留不删。

**2026-10-20 · 评审修复轮(前 5 项 · 🔴 4 → 0)**

- **评审原件**:`production/qa/evidence/review-clinic-001-2026-10-20.md`(双专家并行;合并判定
  `CHANGES REQUIRED` · 4 🔴 / 16 🟡)。修复轮后总判定 → **`APPROVED WITH SUGGESTIONS`**。
- **已修**:R-08(`k_speed` 档集判别力,补 `1/2` 夹具)· D9(补 `RejectControlChars`,原伪注释自此为真)·
  R-10a(断言由共享 token `"W_MIN"` 改为钉子分支 `"界健全性失败"`)· R-17(补 9 条边界测)·
  D1/D2(`AssertFurnTableShape` 补「空表拒」「重复 `type` 拒」)。**测试函数 29 → 42**。
- **证伪**:修复轮 6/6 转红(每条新守卫逐一停用验证);工作树零变异残留(`SHA-256` 逐字节校验)。
- **未修(15 🟡)**:R-09 · R-10b/c/d · R-18 · R-19 · R-21 · S23 · S24 · D4 · D8 · S19 · S16 ·
  R-13 · S25 · S6 —— 均为判别力收紧或建议项,不阻断本体交付。
- **⚠️ 两条如实登记**:① R-17 的 9 条新测**只抽证 1 例**(G3),其余 8 条判别力未经反证;
  ② 修复轮**只跑 EditMode**,PlayMode / IL2CPP 三格仍 NOT RUN。
- **仍未转 `Complete`**:`AC-24-08` / `AC-24-12` 的闭集半边仍挂 `BLOCKED-BY-O-24-1`(不变)。
  > 🔁 **2026-10-21 订正(晚于上句)**:本行写于 **2026-10-20 评审修复轮**,其「挂标不变」**已被 S17-4 取代**
  > —— 挂标已解除,两条 AC 已勾。**仍不转 `Complete` 的理由见 §Test Evidence「补记之二」④ 之后的三条**
  > (`FurnTypeClosureEnforced == false` / 评审残余 🟡 / IL2CPP 三格)。原文保留不删。

**2026-10-29 · 收口轮(`/story-done` · lean)**

- **判定**:**COMPLETE WITH NOTES** —— AC **7/7**;`ClinicMachine` 命名空间实跑 **`194 / 194 / 0 / 0`**;
  本 story 夹具类 `ClinicMachineTablesSchemaTest` **42/42**。
- **三条「不转 Complete」理由逐条核**:
  - ① `FurnTypeClosureEnforced == false` —— ✅ **实质已解决**。生产装配 `ClinicRoomMatchingService`
    选的是**真判据重载** `Extract(view, FURN_TYPES)`(story-006 落地);该哨兵描述的是
    **默认单参入口**仍是代理,而生产链**不走默认入口**。`test_room_furn_membership_absent_sentinel_false`
    正是钉「缺席可观测而非静默」。⇒ **不阻断本 story**。
  - ② 评审残余 **15 条 🟡** —— ⚠️ **成立**,但评审终判为 `APPROVED WITH SUGGESTIONS`(4 🔴 已清零),
    按 `coding-standards` 归 **ADVISORY**。
  - ③ IL2CPP 三格 NOT-RUN —— ✅ **已被 2026-10-21 订正撤销**:
    Linux-x64-IL2CPP 已于 09-24 实测 3/3;Linux-ARM64-IL2CPP 目标不存在(用户裁定长期未达);
    CI 常驻缺凭证。属**矩阵级**,非本件个体判据。
- **补证伪 8/8 转红**(见 §Test Evidence「2026-10-29 补证伪轮」)⇒ **Falsifiability Gate = PASS**。
- **ADVISORY 遗留(不阻断)**:
  1. 15 条 🟡 建议项未修(清单见 `review-clinic-001-2026-10-20.md` §二)。
  2. ⚠️ **`tr-registry.yaml` 的 `TR-clinic-006` 仍记 `partial`**,note 写「n_max 悬于 `OQ-24-5` 未裁」
     —— **该依据已失效**:`OQ-24-5` 于 **2026-10-07 裁定 `n_max = 64`**,且本 story 的溢出夹具
     已按真边界 (715827882 通过 / 715827883 拒绝) 落地。**本件未改注册表**,建议回填轮订正
     (与路线图 §七 **SP-9** 同族)。
- **未跑**:PlayMode(本 story 是烘焙期 Config-Data,判据面在 EditMode);IL2CPP 三格(矩阵级,见上)。
- **代码评审**:✅ 已过 —— `production/qa/evidence/review-clinic-001-2026-10-20.md`,
  `CHANGES REQUIRED → APPROVED WITH SUGGESTIONS`,4 🔴 → 0。
- **Scope**:实现发生在 2026-10-20/21,今日**无法回溯核对改动集**,如实登记为不可核。
