# Story 001: 作者态教学内容 schema 与烘焙校验

> **Epic**: 教学与引导
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-15  <!-- 原 `2026-10-02` —— 2026-10-21 /dev-story Phase 2 逐件判定取 [A]（升级并按现行规则实施）；原文保留 -->
> **Last Updated**: 2026-10-21

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

- [x] schema 完整可绑定:`schemaVersion:int`;`steps[]`(id ∈ 闭集 {observe, judge, treat, close, archive, manage},predicateKind ∈ registry + `Checkpoint` 例外);`narration[]`(cueId ∈ 44 音频表 + text ≤170 且自含 + order);`demonstrations[]`(actor = mentor + clipKey);`papers[]`(title ≤2 行 + body ≤170 + aspectRatio + illustrationKey)
- [x] **6 项构建期校验全为硬失败**:① JSON 合法 ② 引用完整性(无孤儿 / 无悬垂)③ 长度上限 ④ 病名闭集词表扫描(词表 = 9 的病名表,AC-48-13)⑤ 显式指令扫描(「去X处 / 按X」词族)⑥ predicateKind ∈ 白名单(registry 全集 ∪ {Checkpoint})
- [x] 烘出的 `48_tutorial_content.cooked` 确定性(同输入两次烘焙字节相同,`ConfigVersion` 内容哈希一致)
- [x] P0 内容种子 17 条(6 步 + 4 口述 + 6 示范 + 1 纸)过全部 6 项校验,烘焙通过
- [x] 运行期 48 只读 `.cooked`(`IDataProvider` 边界装载),构建产物内零 JSON 解析器符号
- [x] steps 的 id 闭集 = 恰好 6 员(多 / 少 = 校验失败);观察步并入 ① 锚(observe 谓词 = `CaseOpened`,与 judge 共锚不另设)

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
>
> 🔁 **2026-10-21 更新(原文不删)**:上面这条「未定项」**已落地**,不再是待定 —
> `assets/data/disease_names.json`(8 病 / 12 显示名,所有者 = 系统 9)+ 生产实现者
> `TutorialDiseaseNameSource` ⇒ ④ **真跑且 Clean**(`AllSixChecksExecuted == true`)。
> ⚠️ 顺带一处**自查**:本注原要求「第 ④ 项记 `NOT-RUN`,不勾」,而上方 AC 第 4 条一直是 `[x]`
> —— 二者**曾经互相矛盾**(note 说不勾、条目却是勾)。现 ④ 真跑,二者一致;
> 保留此记录以说明**当时的勾是被默认值顶上去的**,不是证据。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Tutorial/tutorial_content_validation_test.cs` — must exist and pass
**Status**: [x] Created and passing —— 🔁 2026-10-21:原文为「`[ ] Not yet created`」,
  已就地订正(该目录四件共 **73 测**,全绿,见下方 `Test Evidence` 与 `Completion Notes`)

---

## Dependencies

- Depends on: ADR-014 两阶段管线基建(既有)· `entities.yaml` registry(既有,ADR-024)· 9 / 44 / 13 的词表 `.cooked`(并行产出,校验器对缺表报「依赖表未就绪」而非崩)
- Unlocks: Story 003(步定义消费)· Story 004(媒体键消费)

---

## Completion Notes
**Completed**: 2026-10-21
**Criteria**: 6/6 passing（~~仅 AC 第 2 条的**外部表半边** cueId / clipKey 仍 NOT-RUN,见下~~）
> 🔁 **2026-10-21 回刷：该限定语已解除** —— C-1 结清，`CueIdTableReady` / `ClipKeyTableReady` **双 true**。
> 判据（实测，非自述）：`audio_events.json` 19 行（含 4 条 `Narration_Teach*_01`）·
> `48_demonstration_clips.json` 6 条 clipKey · `unity/Assets/Audio/` 41 wav（含 4 条 TTS 真语声）。
> 证据原件 = `production/qa/evidence/impl-c1-tutorial-media-keys-2026-10-21.md`。原文保留作沿革。
**Deviations**:
- ✅ **已解除（2026-10-21 修整轮）** — ④ 病名闭集词表：此前 `assets/data/` 下无独立病名表 ⇒ 校验器报
  结构化哨兵 `TableNotReady`、AC-48-13 只能 NOT-RUN。现补齐**生产实现者**：
  ① 新 `assets/data/disease_names.json` —— 逐条转录自 `disease-simulation.md` §R3（8 病 / 12 显示名），
     `_note` / `_source` / `_ownerNote` / `_scanSemantics` 自带 schema 说明；所有者为**系统 9**，
     9 的 registry 落地时以本文件为对账对象，**不得另立第二份词表**；
  ② 新 `TutorialDiseaseNameSource`（读该表；**缺席 ⇒ false = NOT-RUN，不阻断**；**损坏 / 空表 ⇒ 硬失败**，
     空表若放行则扫描恒零命中 = 假绿）；
  ③ `TutorialContentBake.BakeFromRepo` **默认注入**该来源（`diseaseNames ?? new TutorialDiseaseNameSource(repoRoot)`）
     —— 原默认 `null` = 未就绪，会让**忘传参的调用点**（如 `cooked_drift_all_artifacts_test` 的再烘路）
     静默退化成 NOT-RUN；现「有仓根 ⇒ ④ 必跑」；
  ④ `DataBakeMenu.BakeTutorialContent` **显式注入**该来源，并在烘焙日志里打印 ④ 三态
     （NOT-RUN 必须肉眼可见）。
  ⇒ 种子内容现在 **④ 真跑且 Clean**，`AllSixChecksExecuted == true`。
- ADVISORY — ② 外部半边(cueId / clipKey):`audio_events.json` 无 `teach_*` 键,全库无 13 示范片段表
  ⇒ `CueIdTableReady` / `ClipKeyTableReady` = false。已由 `test_seed_content_withAllTablesInjected_passesAllSix`
  证明「表就位后可过 6 项」。**这是内容缺口不是代码缺口**（实测 `teach`/`mentor` 在
  `audio_requirements.json` 与 `audio-material-manifest.md` 均 0 命中、`Assets/Audio/` 37 个 wav 零 `teach_*`），
  补表须同时产出音频素材本体会撞 D6 素材存在性门 ⇒ **具名归属：cue + wav = 系统 44；片段表 = 系统 13**。
  > 🔁 **2026-10-21 回刷：本条 ADVISORY 已解除（C-1 结清）** —— 归属的两侧均已落地：
  > ① **44 侧**：`audio_events.json` 15 → **19 行**，新增 4 条 `Narration_Teach{Observe,Judge,Close,Archive}_01`；
  >    `unity/Assets/Audio/` 37 → **41 wav**，4 条为 TTS 真语声（非静音占位）；
  > ② **13 侧**：新建 `assets/data/48_demonstration_clips.json`，**6 条 clipKey 闭集**。
  > 生产侧新增 `Editor.Tools.Bake/TutorialMediaKeySource.cs`（`ITutorialMediaKeySource` 的实现者），
  > 并由 `BakeFromRepo` **默认注入**（同 `diseaseNames` 纪律）⇒ `CueIdTableReady` / `ClipKeyTableReady` **双 true**。
  > ⚠️ **仍开的边界**：13 侧只落 **clipKey 闭集**（键名 + 语义），**片段资产本体未做** —— 表现面归 48 实现轮。
  > 证据原件 = `production/qa/evidence/impl-c1-tutorial-media-keys-2026-10-21.md`。原文保留作沿革。
- ✅ **已解除（2026-10-21 修整轮）** — `ValidateReferences` 71 行：已拆为
  dispatcher / `CollectStepIds` / `ValidateStepReferences` / `MarkReferenced` /
  `ValidateReverseStepFields` / `ValidateNoOrphans` 六件，最大 17 原始（次大 24），错误串逐字保留。
**Test Evidence**: Logic — `unity/Assets/Tests/EditMode/Tutorial/` 四件共 **73 测**
  (validation **43** · runtime table 10 · **provider load 9** · **disease names 11**)
  + 证伪 M1–M4（评审原件 §六之一：culture / 分支 / 缓存 / 阈值）与 M5–M8、M11（§六之二：
  空表守卫 / 损坏降级 / 去重 / 真表永不就绪 / 默认注入回退，7/7 精确）
  + 批 2 MINOR 的 M12b、M13–M18（§六之二：`order` 浮点 / `actor` 闭集 / `aspectRatio` 两种畸形 /
  `illustrationKey` 缺失 / 文本阈值下移 / ② 未就绪语义 / 哨兵可检测性）。
  ⚠️ 批 2 两条**无证伪靶**、如实登记：`E1`（删未用形参）/ `E2`（`out _`）是**纯语法收敛**，
  无行为可变异 —— 其「已证」= **编译器强制** + 全量回归零回归（`4198 / 4162 / 0 / 36`
  @ `2026-10-21 08:02 CST` · `1dde232` + 未提交改动）。
**Code Review**: Complete — 同上原件(0 BLOCKING)
