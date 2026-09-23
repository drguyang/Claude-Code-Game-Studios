# Story 002: Schema 类型与复合主键

> **Epic**: 物品与配方数据库
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-23

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: TR-itemdb-007(配方 schema)◆ · TR-itemdb-008(药材 schema)◆ · TR-itemdb-009(炮制方法 schema)◆ · TR-itemdb-010(药物 potency / half_life 字段定义 · partial)· TR-itemdb-011(Schema E 库存槽形状)◆ · TR-itemdb-016(品质分级与品质轴定义)◆ · TR-itemdb-017(21a 与 17 采集系统的数据边界)◆ · TR-itemdb-018(9 侧字段名对齐 drug_potency / half_life)· TR-itemdb-030(21a 与 20 库存的所有权边界)◆
◆ = `status: no-adr-by-design`,结构上无架构裁决可挂,归属件 = GDD 自身;永不因补 ADR 转 ✅。
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(主): 数据管线与 JSON 解析器 · ADR-006(次): 定点域边界数据契约
**ADR Decision Summary**: ADR-014 决定作者态 = `assets/data/*.json`,Fix 字段在 JSON 里必须是字符串,逐 schema 已知键白名单(未知键 = 烘焙期硬失败),每文件 `schema_version` 版本不匹配 = 硬失败;ADR-006 决定 `weight` / `stack_max` 是 `int` 计数、`Fix` 不可经 Unity 内置序列化器承载(须自定义编码器)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: ADR-014 Engine Knowledge Risk MEDIUM(Addressables 6.2+ 抛异常为 post-cutoff,须实测;结构面不依赖);ADR-006 MEDIUM(不依赖 post-cutoff API)。

**Control Manifest Rules (this layer)**:
- Required: 承载 `Fix` 数据者只能落 `assets/data/*.json` 并经 `FixParse` 读入;`Fix` 承载字段 JSON 里必须是字符串(`"offset": "3/4"`);逐 schema 白名单 + `schema_version` + 数组长度校验全部烘焙期硬失败
- Forbidden: `JsonConvert.DeserializeObject<T>` / `JObject.Parse`(数字经 double/decimal 中转 = 浮点泄漏,绕开 `FixParse`);`Fix` 字段用 JSON 数字承载
- Guardrail: 全量校验失败 = 构建失败,不是警告;作者态解析失败在烘焙期即失败、不进运行期

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [ ] **AC-21a-21**: 同 (base_id, processing_state) 复合主键重复 ⇒ 拒绝
- [ ] **AC-21a-22**: category / processing_state 取枚举外字面量 ⇒ 拒绝(枚举闭合)
- [ ] **AC-21a-27**: ItemInstance 含任何 UnityEngine 类型字段 ⇒ 静态断言失败(§Schema E;断言须递归走类型图,禁 [SerializeReference]/接口/抽象字段下的子类携带引用)
- [ ] **AC-21a-48**(schema/求解器代码侧重): No hardcoded values —— 全部读数据文件
- [ ] **AC-21a-49**: §Debt Register 每一行都有 owner 与状态标记;任一 ⏳ 行无 owner 即失败(欠账不许匿名)
- [ ] **AC-21a-59**: stackable 被显式写入数据文件 ⇒ 校验拒绝 —— 它是 stack_max > 1 的派生量,不存储(§Schema A)

---

## Implementation Notes

*Derived from ADR-014 §Decision (primary) / ADR-006 (secondary):*

- 凡承载 `Fix` 数据者**只能**落 `assets/data/*.json` 并经 `FixParse` 读入;承载 `Fix` 的字段**必须外置**(内置序列化器对 `Fix` 静默归零)—— ADR-014 §S1:119,125。硬口径收窄到承载 `Fix` 的字段;纯 `int` 常量若 GDD 声明为固定常量则合法 —— §S1:127
- 逐 schema 维护**已知键白名单:未知键 = 烘焙期硬失败**;每文件带 `schema_version`,版本不匹配 = 硬失败;数组长度校验(`axis_offset_by_quality[]` / `quality_character[]` 必须 = `MAX_QUALITY`)—— ADR-014 §三:179,184,191
- `Fix` 承载字段在 JSON 里**必须是字符串,不是 JSON 数字** —— ADR-014 §四:201
- `weight` / `stack_max` 是 `int` 计数,不是 `Fix`(D-21-17)—— ADR-006 §Decision 一
- `Fix` 不可经 Unity 内置序列化器承载,须自定义编码器,由 EditMode 探针守住(D-21-18,实现落 Story 010;本故事的静态断言 AC-27 先拦 UnityEngine 字段)
- 执法体统一形态 = **构建期断言,不落成散文**(manifest 元规则)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: FixParse 字符串边界解析本身(本故事只调用它,不重写)
- Story 006 / 007: 配方表与物品表的负向夹具校验套件(AC-7~20/66、AC-13~15/23~25)
- Story 008: 两阶段烘焙管线与 Addressables(本故事先建类型 + 编辑期校验;管线归 008)
- Story 010: Fix 自定义编码器往返与容器闭包(AC-53/58/31/34/35/63)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-21**: 同 (base_id, processing_state) 复合主键重复 ⇒ 拒绝。
  - Given: 物品表两条记录同 base_id 且同 processing_state。
  - When: 构建期 schema 校验。
  - Then: 硬失败;同 base_id 不同 state(合法四条目形态)与不同 base_id 同 state(Edge Cases 明文「完全正常」)均通过。
  - Edge cases: 重复恰两条 / 三重重复;大小写不同字面量(枚举外归 AC-22);空 base_id(字段非空校验另挡)。
  - Negative fixture: `invalid_dup_key.json`

- **AC-21a-22**: category / processing_state 取枚举外字面量 ⇒ 拒绝(枚举闭合)。
  - Given: category ∉ {material, drug, tool, weapon, build_part, food} 或 processing_state ∉ {raw, dried, extracted, tincture, pill}(P0 集)。
  - When: 构建期校验。
  - Then: 硬失败;全部枚举内字面量通过(JSON 中必须是明文字符串,非 int —— 交叉 AC-21a-26)。
  - Edge cases: 拼写近似值("Raw");空串;null;int 编码(交 AC-26 拒)。
  - Negative fixture: `invalid_enum.json`

- **AC-21a-27**: ItemInstance 含任何 UnityEngine 类型字段 ⇒ 静态断言失败(§Schema E;断言须递归走类型图,禁 [SerializeReference]/接口/抽象字段下的子类携带引用)。
  - Given: ItemInstance 类型或其可达类型图中出现 UnityEngine.* 字段(含 SO/Sprite/GameObject 引用,含经接口/抽象/[SerializeReference] 间接携带)。
  - When: 构建期静态断言扫描。
  - Then: 硬失败;闭集 POD(instance_id:long / item_key / quality:int / qty:int / children:long[])通过。
  - Edge cases: 一层直接引用;二层嵌套子类引用;children 为 long[] 合法;Fix 字段(若有)须走自定义编码器而非 Unity 序列化(D-21-18,交叉 Story 010)。
  - Negative fixture: `invalid_instance_unity_ref.cs`(.cs,静态断言夹具,GDD 指名)

- **AC-21a-48**(schema/代码侧重): No hardcoded values —— 全部读数据文件(§Tuning Knobs 归属与存放)。
  - Given: 调参旋钮全集(EFF_*/QTY_MULT_*/RETAIN_*/ENV_MOD_*/各 *_CAP/MAX_QUALITY/可感知地板等,GDD §Tuning Knobs 所列)。
  - When: 静态扫描 src 侧求解/校验代码。
  - Then: 上述旋钮无一以字面量出现,全部经数据文件(作者态 JSON → 烘焙产物 → IDataProvider)读入;单测内边界值字面量按 coding-standards 例外(「边界值测试中数字本身就是重点」)豁免。
  - Edge cases: 结构常量(枚举字面量 half_life、Q16.16 scale 因子)的白名单边界 —— 白名单须显式列出,不得静默放行旋钮;SKILL_CAP 为引用非本系统拥有。
  - Negative fixture: 无(GDD 未命名)。

- **AC-21a-49**: §Debt Register 每一行都有 owner 与状态标记;任一 ⏳ 行无 owner 即失败(欠账不许匿名)。
  - Given: design/gdd/item-database.md §Debt Register(D-21-9/11/12/20/26/27/32/34/21b 等行)。
  - When: 解析台账行。
  - Then: 每行含 owner 字段 + 状态标记;带 ⏳ 的行 owner 非空;缺任一 ⇒ 失败。
  - Edge cases: owner 写"—"且状态非 ⏳(以「⏳ 行必须有真名 owner」为准,规避号不算 owner);表头/分隔行不计;新增行无 owner 立即红。
  - Negative fixture: 无(GDD 未命名;文档断言,代码构造夹具段)。测试路径亦可记冒烟证据 `production/qa/smoke-*.md`(ADVISORY 门)。

- **AC-21a-59**: stackable 被显式写入数据文件 ⇒ 校验拒绝 —— 它是 stack_max > 1 的派生量,不存储(§Schema A)。
  - Given: ItemDef 记录含显式 `stackable` 字段(无论 true/false)。
  - When: 构建期 schema 校验。
  - Then: 硬失败;无该字段时派生值 = (stack_max > 1),stack_max=1 ⇒ 不可堆叠合法。
  - Edge cases: stackable:true + stack_max:1(漂移最恶劣形);stackable:false + stack_max:99;字段为 null 而非缺失(仍拒 —— 显式写入即拒)。
  - Negative fixture: `invalid_stored_stackable.json`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/item_database/schema_types_primary_key_test.cs` — must exist and pass

**Status**: [x] Created 2026-09-23 — 编译真身 `unity/Assets/Tests/EditMode/ItemDatabase/schema_types_primary_key_test.cs`(账本路径经 `tests/unit/item_database/README.md` 说明,ADR-025 §⑤ 路径分家,同 Story 001);**run: NOT-RUN(超算无 Unity Editor)—— 用户 override 关闭,第一跟进 = 桌面 EditMode 跑 31 测 + 补 17 个 `.meta`**

---

## Completion Notes
**Completed**: 2026-09-23(用户显式 override —— verdict 曾为 BLOCKED 仅因测试 NOT-RUN,双评审修复后代码面无 BLOCKING)
**Criteria**: 6/6 implemented & test-covered(AC-21 · 22 · 27 · 48 · 49 · 59 → 31 test functions;**run status: NOT-RUN —— 桌面 EditMode 为第一跟进,跑绿后翻 VERIFIED,禁借绿**)
**Deviations**(均为 advisory,零 BLOCKING):
- AC-48「值 vs 名」判据局限:文本扫描测旋钮**标识符**子串,硬编码数值不带旋钮名扫不到 —— 方法论固有局限,以 QA Test Cases 标识符口径为准
- AC-48 反向锁(F6)仍为手写清单,GDD 未来新增旋钮行不会自动进 neverWhitelisted(qa 残留 G2;动态化与 half_life 结构重叠冲突,留待后续)
- F5 stripComments 不识别 `@` verbatim 串、插值洞按串置空(欠检不误报)—— 已登记于函数 doc
- S3 测试私有助手 camelCase 承 Story 001 先例;S4 `ItemKey.GetHashCode` 进程内随机化(当前无跨进程哈希依赖,进黄金哈希时须改固定算法)
- `TryParseRecipeOwner` 归 Story 007;AC-21 空 base_id 归 Story 006;AC-22 int 编码产物扫描(AC-26)归 Story 006;AC-27 Fix 编码器(AC-53)归 Story 010
- 两级校验缝隙:`FindStoredStackableKeys` 接原始 JSON 键集 —— **Story 008 绑定层必须把每条记录原始键集一并传入**(同缝服务未知键白名单,ADR-014 §三),已登记校验器 doc
- `LegalTransitions`(string[])与 `ProcessingTransition[]` 两形态并存承 GDD 原文,绑定归 008;`InflictsInjury = string[]` 单值收敛归 008;`QualityDistribution`/`TcmProfile` 空占位不发明字段(形状归 17/P1a)
- ~~17 个 `.meta` 全缺~~ 桌面 Unity 打开时生成(**禁手写 GUID**),随桌面跑测批补交 —— 当前 open,桌面待办
**Test Evidence**: 编译真身 `unity/Assets/Tests/EditMode/ItemDatabase/schema_types_primary_key_test.cs`(31 functions)+ 违例类型夹具 `invalid_instance_unity_ref.cs` + 3 个 QA 负向 JSON 夹具(`tests/unit/item_database/fixtures/`);**run = NOT-RUN**
**Code Review**: Complete — `/code-review`(2026-09-23)= unity-specialist **CHANGES REQUIRED(窄:仅 R1 桌面 .meta;S1 建议随批修)** + qa-tester **BLOCKING(AC-49 锚点)+ GAPS**;修复 **F1–F7 全部落盘并经 grep 核验**,qa-tester 复核 **BLOCKING 解除、F2/F3/F5/F6 到位**;残留 2 窄项均登记上方 Deviations(F7 嵌套泛型已修,G2 反向锁联动不修)。unity-specialist 预结论:R1(.meta)补齐后即达 APPROVED
**First follow-up**: 【桌面】① 打开 Unity 触发 17 个 `.meta` 生成并补交(含 `Sim.Contracts/ItemDatabase/` 目录);② EditMode 跑 `SchemaTypesPrimaryKeyTest` 31 测全绿(**新增验证点:AC-49 真 GDD rowCount 应=25;AC-48 F5 置空后仍 0 违例;genericContainer 恰 3 条**)+ 回归 `FixParseBoundaryTest`(确认两 asmdef references 变更未破坏 Story 001);③ 跑绿回报后翻 run = VERIFIED

---

## Dependencies

- Depends on: Story 001(FixParse 边界契约)
- Unlocks: Story 006 / 007(校验套件需要已定稿的类型),Story 008(烘焙管线需要 schema 类型)
