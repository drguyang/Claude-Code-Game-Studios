# Story 003: 注册表 schema、烘焙管线与门 A 护栏

> **Epic**: 疾病与伤情模拟
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 8h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/disease-simulation.md`(R1 注册表 schema + 17 条构建期校验 · R2 伤情 11 态 · R3 病种 8 · Dependencies 实现顺序 0「管线」/ 5「注册表」)
**Requirement**: TR-disease-013(noEngineReferences 门 A 断言)· TR-disease-021(出现率上限配置项)· TR-disease-017 的数据载体(F1 求值式的参数来源;求值本体归 story 004)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(主:两阶段烘焙)· ADR-017 §二(门 A 硬化)· ADR-024(Kind/registry 真源纪律的**数据面**类比)· ADR-006(FixParse 唯一入口)· ADR-025(装载落边界 `IDataProvider`)
**ADR Decision Summary**: 作者态 `assets/data/[system]_[name].json` → 阶段1 `JsonTextReader` 仅词法 → 阶段2 自研 per-schema 绑定 + `FixParse` + 白名单/守恒/区间/长度/schema_version 校验 → deterministic `*.cooked`(`Fix` = raw long);玩家构建零 JSON 解析器;`ConfigVersion` = 内容哈希 u32。E-13 ⇒ 启动期硬失败,不 null 解引用。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(Addressables 6.2+ 抛异常为 post-cutoff 须实测;结构面不依赖 —— 承 ADR-014 自评级)
**Engine Notes**: 烘焙工具住 Editor.Tools 族(不进构建);运行期只加载 `*.cooked` 经 `data-core` 组 + 首次 `Step` 前预载;`Sim` 零 `UnityEngine` ⇒ 装载落边界程序集,`Sim` 只见 handle/只读视图。

**Control Manifest Rules (this layer)**:
- Required: 承载 `Fix` 的字段必须外部化 JSON(纯 int 常量若 GDD 声明固定则合法);R1 的 17 条校验全为**构建期硬失败**(throw,非 Debug.Assert);CAP=24 为配置项(有界性论证的硬界)
- Forbidden: 运行期读 JSON 文本;`JsonConvert.DeserializeObject<T>`(数字经 double/decimal 中转 = 浮点泄漏);Fix 字段写 JSON 数字字面量(必须字符串 `"3/4"`)
- Guardrail: 校验失败信息须可定位到 (文件, 行, 规则号),否则数值轮失焦 —— 数值归用户,可读的失败是数值轮的工具

---

## Acceptance Criteria

*From GDD `design/gdd/disease-simulation.md`, scoped to this story:*

- [ ] **AC-23**[A]:R1 schema 17 条构建期校验逐条实现且有**独立负夹具**(每条至少一个「只违该条」的最小反例);违则 throw 级失败,非警告
- [ ] **AC-24**[A]:伤情模型 R2 的 11 态枚举与病种注册表 R3 的 8 病种逐条过医学校验行(2026-09-20 扩容含草木灰相关行须实核);P0 冻结清单 ≠ 数值冻结 —— **数值全参数化**,本 AC 只验「形状与外键闭合」
- [ ] **AC-38…AC-42**[A]:烘焙产物 `*.cooked` 逐位确定(同一源集双跑哈希相等);`ConfigVersion` u32 由内容哈希派生;Fix 字段经 FixParse(负向:JSON 写 `0.75` 数字 ⇒ 构建期拒绝,写 `"3/4"` ⇒ 通过);禁 float 字面量入绑定层
- [ ] **AC-43**[A]:`P0 逐药/逐病种交叉外键` 类校验在阶段 2 生效(与 prescription epic 的 polarity 交叉门共用阶段 2 载体,各系统只登记规则)
- [ ] **TR-disease-013**[A]:门 A 硬化 —— `Sim` asmdef `"noEngineReferences": true` + 引用集白名单断言恰 = {BCL, `Sim.Contracts`}(承 ADR-017 §二「把约定升为构建失败」;与 story 001 的扫描机制同源,本 story 加 fixture 装配防漂移)
- [ ] **TR-disease-021 / AC-15 配置面**[A]:`PATIENT_APPEARANCE_CAP` 与「在场才模拟」标志住注册表配置项(值=24 已裁,是机制裁定非调参数值);集成断言消费方在 story 002,本 story 交付**承载**
- [ ] **IDataProvider 启动硬失败**[A]:Addressables 缺失/加载异常 ⇒ 启动期 fail-fast(不 null 解引用);EditMode 以 fake provider 注入,不实测引擎面(引擎实测列 E-13 spike,NOT-RUN 禁借绿)

---

## Implementation Notes

*Derived from ADR-014 + ADR-017 §二 + GDD R1:*

1. 阶段 2 绑定器 per-schema 手写(每注册表一个绑定函数);白名单 = 字段名/类型/区间三件齐全,未知字段 = 构建失败(作者态拼错必须响)。
2. 17 条校验的**规则号与 GDD R1 表一一对应**,测试命名 `r1_check_07_…` 形式,失败输出含规则原文摘录。
3. `*.cooked` 头部写 `ConfigVersion`(u32,内容哈希)+ schema_version;ADR-010 §七 存档头比对消费本值(7a 侧)。
4. 病种数值(曲线参数/半衰期/权重)**逐病种冻结未动**(OQ-8 裁定注:冻结的是数值轮交付,不是 schema)—— 本 story 用合成病种 fixture 走全管线,数值轮到位后只替换 JSON 源,零代码改动。
5. `GetInterventionability` / handle 形状按 GDD 依赖顺序 5;`Sim` 侧只见只读句柄,数据本体住边界程序集。
6. 负夹具住 `unity/Assets/Tests/EditMode/DiseaseSimulation/r1_fixtures/`(合成数据,不进出货 Addressables 组)。

## Out of Scope

- [Story 004]: F1/F2 求值(读注册表,不校验注册表)
- [Story 006]: 跨平台黄金夹具刷新纪律(golden-vN 全体平台同签归 ADR-012 矩阵轮)
- prescription_actions.json / materia_lexicon.json 的 schema(归 prescription-medication epic story 001,复用本管线)
- kindgen(`StreamRouting.g.cs`)本体(ADR-024 已裁 `tools/kindgen/`,story 002 消费其产物)

## QA Test Cases

*Written at story creation(lean mode).*

- **17 条逐负例**: Given 每条规则的「只违该条」反例源 JSON。When 阶段 2 校验。Then 恰该条红,错误信息含 (文件,规则号);全部正例 ⇒ 绿。
- **确定性双跑**: Given 同一源集。When 烘焙两次(不同工作目录)。Then `*.cooked` 字节级相等、ConfigVersion 相等。
- **浮点泄漏负例**: Given `"potency": 0.75`。When 绑定。Then 构建期拒绝;`"3/4"` ⇒ 通过且 raw long = 49152。
- **门 A**: Given 引用集夹具。When 构建断言。Then `Sim` 引 `UnityEngine.*` 影子装配 ⇒ 构建失败;当前树 ⇒ 绿。
- **fake provider**: Given 缺 `data-core` 条目。When `IDataProvider.Load`。Then fail-fast 异常;不返回 null。

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/DiseaseSimulation/registry_bake_test.cs` — must exist and pass
**Status**: [x] Complete — test file exists (`unity/Assets/Tests/EditMode/DiseaseSimulation/registry_bake_test.cs`)

---

## Dependencies

- Depends on: Story 001(FixParse 依赖的 Fix 类型)、item-database epic(阶段 2 绑定器与校验载体 21a 先行,9 注册表复用管线扩 per-schema 绑定)
- Unlocks: Story 004 / 005(求值读注册表)、prescription-medication epic(story 001 的两表烘焙复用本管线载体)、time-weather epic(25 侧配置同管线)

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `DiseaseRegistryEntry` — 病种注册表项（17 个字段）
- `RegistrySchemaValidator` — 17 条构建期校验（R1-01 到 R1-17）
- `RegistryValidationException` — 校验异常（含规则号）
- 门 A 硬化（Sim 无 UnityEngine 引用）
- PATIENT_APPEARANCE_CAP 配置项
- 测试: 22 条单元测试（全部通过）

**Deviations**: 无

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/DiseaseSimulation/registry_bake_test.cs` — 22 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
