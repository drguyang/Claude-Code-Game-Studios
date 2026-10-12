# sprint-08 S1 实施计划(病名册整面 · 甲案 30 novelty 最小链)

> **状态**:**✅ 已落盘并验证(2026-10-12)** —— 件 A–H 全落 · 双变异验证中 · 全量门绿
> (EditMode 3297 = 3250/0/1inc/46skip · PlayMode 148/148)。途中三处结构缺口经用户裁定解决
> (F/G 写路径 = 甲 `ICasebookCommandSink` · `JudgmentRevised` 编码通道 = 甲 补具名重载 ·
> 档位归属 = 甲 住装配层经 DTO 回读);详见 `production/session-state/active.md` 的 S1.1 段。
> **用户裁定**:① 数据源 = **甲案「30 novelty 最小链」** · ② 「见到某病」触发时机 = **查体时**
> **上游**:S0 ✅ + S1.0 ✅(词表烘焙管线;`judgment_lexicon_bake_test` 11/11 绿)

---

## §1 勘察结论(为什么原估低估了)

四专家评估(2026-10-12)把病名册定为「净新 UI 范围」,但**未下钻到「条目集从哪来」**。实测四条链全断:

| 环节 | 现状 | 证据 |
|---|---|---|
| 30 `NoveltyTracker` | 有实现有测试,**零生产调用方**;只有单点 `Query`/`Peek`,**无枚举面** | `Sim.Contracts/SkillSystem/NoveltyTracker.cs` |
| 8 诊断侧 `DiagnosisGrowthGate` | 纯转发件,**零生产调用方** ⇒ P0 运行期不产「见过病」流 | `Gameplay.Presentation/Diagnosis/DiagnosisGrowthGate.cs:27` |
| 词表 `JudgmentLexiconDataSet`(S1.0 建) | **未接 Addressables**,组合根未注入 | `AddressablesDataProvider.cs` 无 `case_judgment` |
| lexicon_id ↔ disease_id 桥 | **不存在** | 全仓零命中 |

**循环死锁分析**(两候选路径皆死):
- 「已见 = 指认过」(事件流 `JudgmentRecorded` 投影):首遇病 D 未指认 ⇒ D 不进已见集 ⇒ 无法指认 D。**死锁**。
- 「已见 = 8 填 novelty」:8 无状态、只读 `VitalsDto`,而 `VitalsDto` **不含 disease_id**(守密 AC-37-15 精神)⇒ 8 填不了 object=D。**死锁**。

**破解**:在**装配层**(`Gameplay.Boot`,可读 disease_id)做投影 —— 查体发生时取病人病种 D → `EmitGrowth(诊断, D)` → `SkillGrown` 落病史流 + novelty 表更新;病名册 = 词表中 `disease_ids ∩ 已见集 ≠ ∅` 的条目,**只出 `lexicon_id` 列表**(守密:42 不见 disease_id)。

**真源确认**:`SkillGrown` 载荷含 `ObjectId`(病种 id)+ `ActorId`(玩家)⇒ **已见集可从病史流重建**(承 ADR-010/026「不独立快照,从流重构」);`NoveltyTracker` 只是运行期加速缓存。与 GDD「派生态由事件流重建」逐字吻合。

---

## §2 实施件(A–H)

| # | 落点 | 内容 | 层 | 依赖 |
|---|---|---|---|---|
| **A** | `NoveltyTracker`(Sim.Contracts) | 加 `EnumerateSeenObjects(int actorId, int skillId)` —— 现有无枚举面 | 30 | — |
| **B** | 新 `SeenDiseaseProjector`(Gameplay.Boot) | 查体发生(`ExaminationLedger.HasExamined` 边沿)→ 从 `DiseaseVitalsService` 取病种 D → `EmitGrowth(诊断, D)` → `SkillGrown` 落病史流 | 装配 | A |
| **C** | 新 `LexiconBookSource`(Gameplay.Boot) | 读已见集 → 经词表 `disease_ids → lexicon_id` 反查 → **只出 `lexicon_id` 列表** | 装配 | A,B,D |
| **D** | `AddressablesDataProvider` + 组合根 | 词表 cooked 接入装载(S1.0 已烘未接)+ 注入 | 装配 | — |
| **E** | `ICasebookDtoSource` 扩展 | 加 `SeenLexiconIds`(显式 `lexicon_id`,**非显示索引** —— TD 第五风险) | 42 契约 | C |
| **F** | `CasebookScreen` | 病名栏 Submit → 右页**内嵌**病名册(**非新 ModalId**,`casebook-39.md:78`);条目按 `lexicon_id` 指认 | 42 | E |
| **G** | 三态循环 | 同栏重复 Submit:空→疑似→确定→擦除;`confidence` u8 映射钉死 | 42 + 契约 | F |
| **H** | `Casebook39.uxml` + cross 断言 | name 序两面同步(否则 `casebook_render_test.cs:283` + `casebook_lexicon_test.cs:288` 同时红) | 42 | F |

**写路径终点**(已存在,零新写者):`PrescriptionPageHost.WriteJudgment(int lexiconId, byte confidence)` → `JudgmentWriter.TryRecord` → `IEventSink.Append(JudgmentRecorded)`。

---

## §3 判据

| 判据 | 层 | 可证伪 |
|---|---|---|
| `test_lexiconBook_unseenDisease_notPointable` | EditMode/PlayMode | Given 病名未见过;When 翻册;Then 条目集不含该病 |
| `test_lexiconBook_pointIdentify_writesJudgmentRecordedWithLexiconId` | PlayMode | Then 病例流新增 `JudgmentRecorded`,lexiconId = X;**变异删接线 ⇒ 零事件恰红** |
| `test_lexiconBook_submitCycle_threeStates` | PlayMode | 同栏再 Submit ×2 ⇒ confidence 疑似→确定→擦除 |
| `test_seenDisease_projector_emitsSkillGrownOnExam` | EditMode | 查体后病史流含 `SkillGrown`(objectId = 病种 D) |
| E2E-1/2/3(sprint-08 §2) | PlayMode | 七环节全链 BLOCKING |

---

## §4 风险

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| A–D 补链超 sprint-08 §2 原估(原假设数据源现成) | **高** | 触 8–9 人日上限 | 甲案已由用户裁定;必要时 B 另立 story,UI 面(E–H)先行 |
| 8 侧「查体即成长」与既有 `DiagnosisGrowthGate`(零调用方)语义冲突 | 中 | 中 | B 件用 `DiagnosisGrowthGate` 现有出口,不新开 |
| `confidence` u8 数值映射未登记(疑似=?/确定=?) | 中 | 低 | G 件钉死:`Empty=0/Suspected=1/Confirmed=2`(对齐 `DiagnosisJudgmentFsm`) |
| cross 断言双面不同步 | 中 | 低 | H 件同批改 UXML + BuildUI |

---

## §5 不做

- 自由文本 / 手写板(`freehand_text` P0 无 UI 入口,OQ-CB-1 甲)
- 病名册分类 / 搜索 / 自动补全 / 体征线索(AC-8-43/44,破反幻想二)
- 折边 / 方笺翻页(S2 范围)
- 数值轮(confidence 映射值之外)
