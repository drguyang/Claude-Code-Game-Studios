# Story 001: 作者态教学内容 schema 与烘焙校验

> **Epic**: 教学与引导
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/tutorial-and-onboarding.md`(规则八 作者态内容与数据形状:steps / narration / demonstrations / papers;6 项构建期硬失败校验;P0 内容种子 17 条)
**Requirement**: TR-tutorial-004(作者态内容 JSON 全项构建期校验,covered ADR-014)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(主): 数据管线与 JSON 解析器 · ADR-024: Kind 单一登记真源 · ADR-018: 音频架构(cueId 键空间)
**ADR Decision Summary**: 作者态 = `assets/data/48_tutorial_content.json`,两阶段工具链(阶段1 `JsonTextReader` 仅词法 → 阶段2 自研 per-schema 绑定 + 校验)烘成 deterministic `*.cooked`,住 `data-core` Addressables 组;玩家构建零 JSON 解析器。`predicateKind` 字段取值 ∈ `entities.yaml` 的 `SimEvent.Kind.*`(archive 步例外 = 字面量 `"Checkpoint"`,7a 锚点非 Kind —— 白名单例外在本 schema 钉死);`schemaVersion` = int;文本长度(narration ≤170 自含、paper body ≤170、title ≤2 行)与显式指令扫描(「去X处 / 按X」词族)= **构建期硬失败**,`throw` 级,非告警。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 绑定与校验 = 编辑期 .NET(Tooling 侧,同 ADR-022 / ADR-024 先例,`includePlatforms:["Editor"]` 不进构建);零 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: Fix 字段(若有)JSON 写字符串经 `FixParse`;禁 `JsonConvert.DeserializeObject<T>`(浮点泄漏);构建失败 = 显式 `throw`
- Forbidden: 运行期读 JSON 文本;病种名自由词(必 ∈ 9 的病名闭集词表);口述文本内显式按键 / 显式地点指令
- Guardrail: 引用完整性零孤儿零悬垂(narration→step、demo→actor、paper→step、cueId→44 表、clipKey→13 表、predicateKind→registry)

---

## Acceptance Criteria

*From GDD `design/gdd/tutorial-and-onboarding.md`, scoped to this story:*

- [ ] schema 完整可绑定:`schemaVersion:int`;`steps[]`(id ∈ 闭集 {observe, judge, treat, close, archive, manage},predicateKind ∈ registry + `Checkpoint` 例外);`narration[]`(cueId ∈ 44 音频表 + text ≤170 且自含 + order);`demonstrations[]`(actor = mentor + clipKey);`papers[]`(title ≤2 行 + body ≤170 + aspectRatio + illustrationKey)
- [ ] **6 项构建期校验全为硬失败**:① JSON 合法 ② 引用完整性(无孤儿 / 无悬垂)③ 长度上限 ④ 病名闭集词表扫描(词表 = 9 的病名表,AC-48-13)⑤ 显式指令扫描(「去X处 / 按X」词族)⑥ predicateKind ∈ 白名单(registry 全集 ∪ {Checkpoint})
- [ ] 烘出的 `48_tutorial_content.cooked` 确定性(同输入两次烘焙字节相同,`ConfigVersion` 内容哈希一致)
- [ ] P0 内容种子 17 条(6 步 + 4 口述 + 6 示范 + 1 纸)过全部 6 项校验,烘焙通过
- [ ] 运行期 48 只读 `.cooked`(`IDataProvider` 边界装载),构建产物内零 JSON 解析器符号
- [ ] steps 的 id 闭集 = 恰好 6 员(多 / 少 = 校验失败);观察步并入 ① 锚(observe 谓词 = `CaseOpened`,与 judge 共锚不另设)

---

## Implementation Notes

*Derived from ADR-014 Implementation Guidelines:*

1. 阶段 2 绑定器落 `Editor.Tools` 族(`tools/` 下 per-schema binder,与 ADR-024 kindgen 同构:未登记 schema = 构建失败)。
2. 词表来源接线:病名闭集 = 9 的 `disease_names`(读其 `.cooked`,校验器经 `IDataProvider` 编辑期装载);44 cueId 集 = 音频事件表 `.cooked`;13 clipKey 集 = 示范片段表。**校验器读烘焙产物,不读彼此 JSON**(避免环)。
3. 显式指令扫描词族表(「去」「按」「 press」类)作为校验夹具常量文件(禁硬编码在逻辑里 —— 承数值/词表外置纪律;扩充归文案轮)。
4. 长度自含判据:narration text 不含指代前文才能读的标记(实现 = 扫描器词表,规则八注)。
5. ⚠️ **数值冻结**:170 字符上限 / 2 行上限本身是 GDD 已裁机制值(冻结);文本**内容**归内容轮,本 story 只交种子夹具条目与校验器。
6. `Checkpoint` 例外注释必须写在 schema 绑定器源码注释并链接 ADR-010 §六(防后人当 bug 修)。

---

## Out of Scope

- [Story 002]: 运行期装载后的投影消费
- [Story 003]: 谓词求值与触发(读 `.cooked` 的步定义,但求值逻辑不在本 story)
- [Story 004]: 媒体播放(口述 cue / 示范 clip / 纸近景的呈现)
- 口述 / 纸的全部正式文案(P0 种子为机制验证夹具,终稿归叙事轮)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 6 项校验各有「必失败」夹具
  - Given: 每项违规各一份 JSON(非法语法 / 悬垂 cueId / 171 字 / 词表外病名 / 「按 E」/ `predicateKind=Foo`)
  - When: 跑阶段 2 烘焙
  - Then: 六次全 `throw` 硬失败,错误信息含条目定位(step id + 字段)
  - Edge cases: 孤儿(narration 无对应 step)也归 ② 失败

- **AC-2**: 种子 17 条过闸
  - Given: P0 种子文件
  - When: 烘焙
  - Then: 成功产出 `.cooked`;字节级两次一致(确定性)
  - Edge cases: `Checkpoint` 谓词仅允许挂在 archive 步(挂别步 = ⑥ 失败)

- **AC-3**: 运行期零解析器
  - Given: 构建产物 IL2CPP 符号面(或 EditMode 等价:程序集引用断言)
  - When: 扫描 48 运行期程序集引用集
  - Then: 无 Newtonsoft / `System.Text.Json`;只经 `IDataProvider`
  - Edge cases: 编辑期工具程序集允许(被 `includePlatforms:["Editor"]` 隔离)

- **AC-4**: id 闭集恰 6 员
  - Given: steps 增第 7 员或删除任员的夹具
  - When: 校验
  - Then: 失败
  - Edge cases: observe 与 judge 共锚(同 `CaseOpened`)合法

---

> ### 🔁 QA plan 回填(2026-10-21 · `production/qa/qa-plan-sprint-17-2026-10-21.md`)
>
> 上列规格**原文保留**;只补增量。**估计测试数 ~20**。
>
> | # | 新增判据 | 依据 |
> |---|---|---|
> | 1 | 🔴 **Cross-Cutting 之八 强制项(BLOCKING 级)** —— 第 ④ 项「病名闭集扫描」在**中文散文**上判定:① 谓词处**显式** `StringComparison.Ordinal`(`IndexOf`/`Contains`/`Equals` 一律带,禁默认重载);② 扫描结果**须是结构化计数 / 命中 id 列表**,**失败与否由计数派生**,不得由字符串前缀判真假。测试须**至少一例钉住**(构造只差 `Ordinal` 语义的输入,证明判定不随 culture 变化) | `control-manifest.md:571-590`(Manifest `2026-10-15`,**晚于**本卡 `2026-10-02`) |
> | 2 | **长度边界成对断言**:`narration[].text` 恰 **170** 过 / **171** 拒;`papers[].body` 同;`papers[].title` 恰 **2 行**过 / **3 行**拒。上列只测了 171 单侧 | 规则八校验规则 3 · `paper-closeup-48.md` §12 |
> | 3 | **`ConfigVersion` 内容哈希一致**(不只「字节级两次一致」):同源两次烘焙的 `ConfigVersion`(内容哈希 u32)相等;改一个数值 ⇒ 版本号变 | AC 第 3 条 · ADR-014 §五 |
> | 4 | **程序集引用断言须带 `Count > 0` 守卫** —— 上列 AC-3 的「无 Newtonsoft / `System.Text.Json`」在**空引用集下恒真**;须先断言「扫描的引用集非空」再断言「不含禁用项」 | `presentation_projection_test.cs:412` 记的同型警告 |
> | 5 | **`archive` 步白名单例外**:`predicateKind = 字面 "Checkpoint"`(7a 存档头,**不是** `SimEvent.Kind`);且该字面**不得**被放到其它五步 | 规则八「`archive` 步判据例外」· ADR-024 |
>
> ⚠️ **未定项(实施期须落定)**:第 ④ 项要扫的「9 的病种名表」= **扫描字典**。若实施期该表尚未烘成可读产物,
> AC 第 4 条(种子 17 条过全部 6 项)的「第 ④ 项」记 **`NOT-RUN`,不勾**;
> **不得**用弱化词表凑绿(本仓「自造断言顶替 AC 语义层」形态之一)。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Tutorial/tutorial_content_validation_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: ADR-014 两阶段管线基建(既有)· `entities.yaml` registry(既有,ADR-024)· 9 / 44 / 13 的词表 `.cooked`(并行产出,校验器对缺表报「依赖表未就绪」而非崩)
- Unlocks: Story 003(步定义消费)· Story 004(媒体键消费)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
