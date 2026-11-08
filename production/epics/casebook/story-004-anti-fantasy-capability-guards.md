# Story 004: 反幻想三条「绝不」的能力面守卫

> **Epic**: 脉案
> **Status**: Complete With Notes
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-08

## Context

**GDD**: `design/gdd/casebook.md`(规则四 三条绝不 · AC-39-05 / AC-39-06 / AC-39-08 / AC-39-09 · `disease_id` 不进呈现层)
**Requirement**: TR-casebook-006(防间接泄漏的**能力面** —— 39 的 API 不暴露任何「按病种聚合 / 计数 / 检索」形状,partial:45 侧残)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-008(主): 病例流 · ADR-013: 拟物 UI 框架 · ADR-009: 世界状态事件化边界(`disease_id` 保密语义 = 防御纵深)
**ADR Decision Summary**: 39 是 `AC-37-15` 类型面守卫(`PresentationDtoGuard` 递归反射)之外的**能力面**落点:① 绝不显示未结病例计数 / 红点 / 提醒;② 绝不按病种分组、排序、检索;③ 绝不提示同源(不暗示「这些病例像同一个病」)。归纳是玩家的动作,不是界面的功能(AC-39-08)。实现载体 = 构建期扫描(反射断言 + USS 文本黑名单),不是散文约定 —— 同 ADR-017 门 A 白名单「把约定升为构建失败」的先例形制。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 守卫本体 = 纯 C# 反射 + 文本扫描;被扫对象含 UI Toolkit USS/UXML 资产(读文件文本,非引擎 API)。

**Control Manifest Rules (this layer)**:
- Required: 判据 = 可执行断言(构建期 / EditMode),非 grep 单点;闭集扫描方向 = 「白名单外即失败」
- Forbidden: 以「运行时不显示」替代「能力面不存在」(API 里能数 ⇒ 泄漏路径已打开);用红点/角标/计数文本表达任何聚合
- Guardrail: 与 8 的词表联动 —— 呈现词经 ADR-014 烘焙,词表**不含**病种分组维度(闭集扫描的输入)

---

## Acceptance Criteria

*From GDD `design/gdd/casebook.md`, scoped to this story:*

- [x] **AC-39-05 能力面**:39 公开 API 与投影 DTO 上不存在「按 `disease_id`/病名 分组 / 排序 / 检索 / 计数」的任何方法或参数(反射断言:公开签名扫描,标识符黑名单 ∈ {GroupByDisease, SortByDisease, SearchDisease, CountBy*, CountUnopened, UnopenedCount…})
- [~] **AC-39-06a 本地化键闭集 + USS 文本黑名单**:脉案用到的全部文本键 ∈ 闭集(「读数 / 落笔 / 翻页 / 合上」类),扫描不出「同源 / 相似 / 同症 / 未结 N / 提醒」类字符串;USS/UXML 里 `text=` 直写 = 黑名单命中即构建失败
  —— **2026-11-08 已补齐**:闭集 = `assets/data/casebook_text.json`(13 键),门侧扫键形 / 重复 / 空文案 / 黑名单,
  **且 UXML 的每个 `text=` 必须 ∈ 闭集值集**(自由新增的字面量绕不过去);`text=` 黑名单半边照旧。**构建期也跑**(`ScanAuthoringSurface`)
- [x] **AC-39-08 零归纳辅助**:39 无任何「跨病例聚合视图」(同一投影不做两次以上病例间的比较;`J(c,p)` 仅病例内)
- [x] **`disease_id` 类型面**:39 消费的全部 DTO 过 `PresentationDtoGuard` 递归扫描子集(与 AC-37-15 同判据、共享扫描器),零命中(= AC-39-01 的本 story 侧落点)
- [~] **AC-39-09 [L]** 「病名列空白、零催促」人工走查项**建立走查脚本与证据模板**(本 story 交付走查用例文档,执行与主创签核归 QA 轮)[L]
  —— **2026-11-08 已真包执行一轮**:零提示 / 零兜底 / 零催办 / 空行可聚焦 / 5 秒停留零像素变化 **全部成立**;
  **手柄路径 · 两例翻页对照 · 听测** 三步仍 `NOT-RUN`,**主创签核未做**(不代签)
- [x] 「未结」状态词仅作为**单病例**的 37 状态读出(已处置/未处置的呈现权归 37 判据),39 不做**跨病例**计数

---

## Implementation Notes

*Derived from ADR-013 / ADR-008 Implementation Guidelines:*

1. 扫描器落 EditMode 测试 + 构建期 hook 双跑(形制照抄 `PresentationDtoGuard` 的「递归反射扫描」:公开方法参数名/返回类型/委托签名三面)。
2. 本地化键闭集 = 作者态数据(ADR-014 管线 `assets/data/casebook_text.json` → `.cooked`);扫描输入用烘焙产物的键集,运行期零 JSON 解析。
3. USS 文本黑名单扫 `text=` / `-unity-text` 属性与 UXML 内联字符串;命中表与 AC-39-06a 词族一一对应(词族表进测试夹具,数值/词表扩充归用户/文案轮)。
4. 「能力面」与「呈现面」分离:本 story 只管 **API 里做不到**;「就算有人偷加,UI 也不显示」不是达标 —— 记入 story 评审检查单。
5. 与 42 的边界:焦点引擎、元件库归 42;本 story 扫的是 **39 程序集** + 39 独占的 UXML 片段,不判 42 全域。

---

## Out of Scope

- [Story 005]: 空行的可聚焦与压痕声(呈现面,42 元件)
- [Story 006]: 落笔/相机路径
- AC-39-06b / 09 / 11 / 12 / 14 的**执行走查**([L] 类,归 QA 轮 + 主创签核;本 story 只立守卫与走查脚本)
- 44 侧零状态播报音(联合 AC-44-09,归音频 epic)

---

## QA Test Cases

> 🔁 **2026-11-08 回填(sprint-26 `/qa-plan`)** —— 本节由 `production/qa/qa-plan-sprint-26-2026-11-08.md` §S26-3 派生。
> ⚠️ **口径**:本 story 只管「API 里**做不到**」。「就算有人偷加,UI 也不显示」**不是达标**。

**[Logic story — automated test specs + 走查脚本]**
文件:`unity/Assets/Tests/EditMode/Casebook/casebook_anti_fantasy_guard_test.cs`(约 8 条)

- **AC-1 · 公开签名黑名单(AC-39-05)**
  - Given: 39 程序集全部 public 类型
  - When: 反射匹配**方法名 / 参数名 / 返回类型名** ∈ `{GroupByDisease, SortByDisease, SearchDisease, CountBy*, CountUnopened, UnopenedCount, …}`
  - Then: 零命中
  - Edge: `[Obsolete]` 内部方法**也算命中**(扫描面 = 全 public,无豁免)
  - ⚠️ **必须名 + 返回类型双面**:`Get()` 返回带病种维度的 DTO 照样泄漏(sprint-25 `TutorialZeroGate` G2 的教训)

- **AC-2 · DTO 无病种维度**
  - When: `PresentationDtoGuard` 递归扫描 + 字段名黑名单
  - Then: `disease_id` 零出现,**含嵌套泛型实参**

- **AC-3 · 文本键闭集(AC-39-06a)**
  - 三项全零:① `.cooked` 键 ⊆ 闭集;② USS/UXML `text=` 直写 = 0;③ 黑名单词族 `{同源, 相似, 同症, 未结N, 提醒}` = 0
  - Edge: 注释与测试夹具**排除**在扫描集外(白名单目录表)—— 否则注释里的「count」被误判

- **AC-4 · 零跨病例聚合(AC-39-08)**
  - Then: 无任何函数接受「≥2 病例」输入对并返回相似/同源判定
  - Edge: **串接组 = 同一病人的分组(允许)**,不是病种分组(禁止)—— 判据写进断言注释 + 一条正控

- **AC-5 · `[L]` 走查脚本(AC-39-09)**
  - 交付:`production/qa/evidence/casebook/story-004-l-walkthrough-scripts.md`
  - 内容:空白病名栏的三案链存档 + 截图清单 + 「零提示 / 零兜底清单 / 零催办」逐条
  - ⚠️ **脚本存在 ≠ 走查已跑**;执行归 QA 轮,未跑即 `NOT-RUN`

- **假绿防线(本件高危)**
  - **F4**:每条守卫配**负夹具**(故意造一个违例类型 ⇒ 必红),否则「零命中」与「扫描面空」不可区分
  - **F3**:黑名单期望值写**字面量**;另加「故意扩充一个词 ⇒ 必红」的变异
  - **F6**:每条守卫给 `file:line` + 一条能证明它会红的命令

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/CasebookGuards/casebook_anti_fantasy_guard_test.cs` + `production/qa/evidence/casebook/story-004-l-walkthrough-scripts.md`([L] 走查脚本模板,执行留 QA 轮) — must exist and pass
**Status**: [x] Created and passing — **23 条判据** · 证伪 **M1–M17** · 构建期探针轮 `Result: Failure`(报文 = `失败门组:39 反幻想`)
原件 = `production/qa/evidence/impl-s26-3-casebook-anti-fantasy-guards-2026-11-08.md`

> 🔁 **2026-11-08 落点订正(原文不删)**:本卡与 QA 计划原写 `Tests/EditMode/Casebook/`,
> **实测不能放那里** —— `Casebook/` 有自己的 asmdef(`Casebook.Tests`),只引
> `Sim.Contracts` + `Gameplay.UI`,**够不着 `Editor.Tools.Gates`**,在那边调不到门本体。
> 实际落点 = `Tests/EditMode/CasebookGuards/`(走大装配 `Sim.Contracts.Tests`)。
> 这是装配事实,不是偏好。

---

## Dependencies

- Depends on: Story 002 / 003(API 面成形后才有可扫对象)
- Unlocks: Story 005(呈现层在「做不到」约束内设计,不再返工)

---

## Completion Notes

**COMPLETE WITH NOTES(2026-11-08 · sprint-26 S26-3)**

### 交付物

| 件 | 路径 |
|---|---|
| 门本体 | `unity/Assets/Editor.Tools.Gates/CasebookAntiFantasyGate.cs` |
| 接线 | `AssemblyGates.RunAll()`(构建前门链)· `gate_wiring_manifest_test` 双向差集两处 |
| 判据 | `unity/Assets/Tests/EditMode/CasebookGuards/casebook_anti_fantasy_guard_test.cs`(23 条) |
| `[L]` 走查脚本 | `production/qa/evidence/casebook/story-004-l-walkthrough-scripts.md` |
| `[L]` 走查**执行** | `production/qa/evidence/casebook/ac39-09-walkthrough-result-2026-11-08.md` + 图目录 |
| 文案键闭集 | `assets/data/casebook_text.json` |
| 证据原件 | `production/qa/evidence/impl-s26-3-casebook-anti-fantasy-guards-2026-11-08.md` |

### 三条「绝不」怎么落成「做不到」

判据落在**公开 API 面**,不落在「界面上有没有画红点」。
R1 病种维度零出现(`diseas` / `diagnos` / `symptom`)—— **这一条把 AC-39-05 整个包住**:
拿不到病种,就无从「按病种分组 / 排序 / 检索 / 计数」。R2 聚合 / 催促词
(`unopened` / `undone` / `pending` / `remind` / `alert` / `badge` / `notify` / `similar` / `samesource`)。

⚠️ **词表刻意不含 `sort` / `group` / `count`** —— `CaseSortKey` / `CaseGroup` / `CaseSheet.Count`
都是合法用法;一律拉黑会造出一条**永远红**的门。M10 反向证明该取舍有判别力(塞进 `count` ⇒ 3 条转红)。

⚠️ **名 + 返回类型双面扫**(承 sprint-25 `TutorialZeroGate` G2 的教训):方法名干净但返回带病种维度的类型
照样泄漏 ⇒ M2 专钉这一面,恰 1 红。

### 前瞻性登记(不借绿)

1. **AC-39-06a 的「本地化键闭集」半边 —— ✅ 2026-11-08 已补齐。**
   落点:`assets/data/casebook_text.json`(13 键,`cb` 前缀 + 驼峰;键名避开 `disease` 词干,见该文件
   `keyNamingNote` —— 机械门分不出「能力面泄漏」与「一个标签」)。
   判据新增:`ScanTextKeySet`(键形 / 重复 / 空文案 / 黑名单 / 空集)+ `ScanUxmlAgainstClosedSet`
   (**UXML 每个 `text=` 必须 ∈ 闭集值集** —— 没有这一条,闭集只是自说自话的清单)+
   `ParseTextKeys`(坏 JSON / 缺 `keys` 必须报错,不许静默退化成空集)。
   ⚠️ 同时补了一处**构建期**缺口:`ScanAuthoringSurface` 接进 `RunAll` —— 此前这两个资产只有 EditMode 读,
   真构建里 AC-39-06a 的资产半边**没有执行体**。
2. **AC-39-09 `[L]` 走查 —— ✅ 2026-11-08 真包跑了一轮**(键鼠)。
   零提示 / 零兜底 / 零催办 / 零同源暗示 / 空行可聚焦 **全部成立**;最强的一条是**焦点停在病名栏 5 秒,
   前后两帧 SHA-256 完全相同**(零像素变化 ⇒ 连「提示闪一下又消失」也被排除)。
   结果与截图 = `production/qa/evidence/casebook/ac39-09-walkthrough-result-2026-11-08.md`。
   ⚠️ **仍 `NOT-RUN`**:手柄路径 · 两例翻页对照(本次只开一例)· 听测零 jingle;**主创签核未做**。
3. **扫描面边界**:只扫 `DaYiJingCheng.Gameplay.UI.Casebook` + `Casebook39.uxml` + `casebook_text.json`;
   42 元件库全域、8 的词表不在此面内。
4. **`BuildFailedException` 报文里的门名 —— ✅ 2026-11-08 改成了派生枚举。**
   不再手写散文清单:`RunAll(out failingGroups)` 每跑一门就记一笔,**报文列的就是真的失败门组**。
   探针轮实测 `失败门组:39 反幻想`(插无条件失败项 ⇒ `Result: Failure`)。
   ⚠️ 补之前它漏了 17 的两个门(散文枚举必漂);改派生后该类缺陷结构性消失。
5. **一条顺带观察(不属本故事)**:`Right` 键在左侧列**不改变焦点**(帧哈希相同),完整环靠 `Tab`(10 站);
   右侧列两格只能 `Tab` 到。归 `AC-39-11` 的走查去判,本轮不裁。

### 判据(23 条)+ 证伪(17 条)+ 探针轮(2 轮)

证伪 M1–M17 全部转红(单红 14 条 / 双红 2 条 / 三红 1 条,归因见证据 §3);SHA-256 逐字节还原,
`grep -rn "MUTANT\|FALSIFY" unity/Assets` 零命中。
**构建期探针轮**:插无条件失败项 ⇒ `Build Finished, Result: Failure` · 探针串在日志 ·
utp `steps` 止于 `Preprocess Player` · 栈帧 `AssemblyGates.cs:1593`(见证据 §4 的两半口径)。

### 回归

EditMode `7145 / 7108 / 0 / 0 / 37` @ `2026-11-08T01:32:07Z` · sprint-26 S26-3(续) · `39c51ab1` + 工作树;
PlayMode `365 / 361 / 0 / 2 / 2` @ `01:33:14Z` 同一 triple。基线(本 story 上一轮 `39c51ab1`)`7134 / 7097`
⇒ **+11 / +11,零回归**;等式 `7145 − 7134 = 11` = 本夹具 +10(13 → 23)+ `assembly_gate_runall_test` +1。

### 一处流程事实

首轮定向 11 条里 1 红 —— **不是门错了,是我的断言写窄了**(断言报文里有类型名,而门的契约是点名命中词面)。
处置 = 改断言贴契约,**不是**改门贴措辞。
