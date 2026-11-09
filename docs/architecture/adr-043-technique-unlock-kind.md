# ADR-043: 技法解锁状态的权威载体 (`TechniqueUnlocked`)

## Status

**Accepted**(2026-11-09)

> **2026-11-09 用户裁定「按你的，M1–M7 实现」** —— 即 §8 的 Q1 / Q2 / Q3 三项均按起草倾向:
> **写者归 30** · **技法 id 复用处置轴 action id** · **起步集 = 空流一次性写入**。
> 同日按 M1–M7 落盘(见 §5 与文末「实现记录」)。
> **起草依据**:用户 2026-11-09 三项裁定 —— ① 三层正交模型(知识永久 / 物品可缺 / 熟练只改稳度与省料);
> ② 钩子病例每章 1–3 例;③ 毛地黄 / 金鸡纳补奇遇
> (`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §0)。
> **本件的职责 = 为该模型里「知识层」找一个可重建、可存档、有唯一写者的载体。**
> ⚠️ **§Decision D1–D10 由起草轮就地拟定** —— Q1–Q3 已由用户裁定,其余细项(D4 载荷形状 /
> D6 去重语义 / D8 折叠豁免 / D10 有界性)未经逐条确认,实现轮前仍可改(同 ADR-034 / 037 / 038 口径)。

## Date

2026-11-09

## Last Verified

2026-11-09

## Decision Makers

dr_guyang(用户 · 2026-11-09 三项裁定)· technical-director(起草)
· 30 技能与熟练度(H1 ③ 已登记的「非物品能力」所有者)
· 10 急救动作 / 11 处方用药(技法本身的所有者)
· 7a 持久化(折叠 / 重建义务的承接方)

## Summary

技法解锁(「这个大夫会不会这一招」)**今天没有任何载体** ——
`systems-index.md` §11 的 **H1(模态/动作解锁无拥有者)** 只登记了所有权分治,
且其对「非物品能力」的裁决是 **「归 30 熟练度,**不设解锁门**」**(`:827`)。
用户 2026-11-09 改判了后半句:技法**要**有门,门的钥匙 = 奇遇 / 手册。
⇒ 本 ADR 必须回答:**门的状态存在哪、谁写、怎么重建、怎么查询。**

裁决:**新增病史流 `Kind` `TechniqueUnlocked`**(追加在 `EventKind` 枚举末尾),
载荷 `(actor_id, technique_id, source_id)` 全整数域;**写者 = 30**(承 H1 ③ 的归属);
**技法 id 复用处置轴 `disease_action_axis.json` 的 action id**(零新表、零新 id 空间);
运行时经 `ITechniqueUnlockQuery`(住 `Sim.Contracts`)查询,实装 = 病史流折叠投影;
**不参与 7a 折叠、不独立快照**(同 `SkillGrown` 的处置,ADR-026 §⑦ 案 1)。

**本件同时改判两处既有口径**,均须逐条登记、不得静默:
① H1 ③ 的「非物品能力不设解锁门」→ **设门**;
② `skill-system.md:232` 的「给技能加门亦违法」→ **适用范围收窄到 19 项技能**(技法不是技能)。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(事件边界 · Kind 登记 · 所有权裁决) |
| **Knowledge Risk** | **LOW** —— 纯数据边界裁决,零引擎 API |
| **Post-Cutoff APIs Used** | **None** |
| **Verification Required** | 新 Kind 经 `entities.yaml` 登记后重跑 `tools/kindgen/kindgen.py`;生成器断言 **A1–A5** 全过(ADR-024 §⑤)。**枚举成员追加在末尾** —— 隐式值即声明序号,插入中间 = 其后全部成员值 +1 = 历史存档读错 Kind(ADR-012 黄金夹具会红;实测先例见 `EventKind.cs` 追加区注) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-024**(registry = Kind 单一真源)· **ADR-005**(病史流唯一真源 · 主机唯一 Append)· **ADR-009**(§二 三流分类)· **ADR-026**(§⑦ `SkillGrown` 的折叠豁免先例)· **ADR-010**(§三 义务清单) |
| **Enables** | **T4**(抽取的技法门:`treatable_by` ∩ 已解锁集)· **10 的动作可用性判定** · **42/48 的手册缺页**(钩子病例的「缺哪一门技法」有 id 可指) |
| **Blocks** | T4 及以后 —— 没有本件的查询接口,`treatable_by` 的过滤**没有输入源** |

---

## 1. Context(为什么必须裁)

### 1.1 三层模型里,只有「知识层」没有载体

| 层 | 决定 | 今天的载体 | 状态 |
|---|---|---|---|
| 物品 | 做不做得成 | 20 库存 / 21a 物品 id | ✅ 有 |
| 熟练 | 做得稳不稳 | 30 `QueryLevel` | ✅ 有 |
| **知识** | **想不想得到这一招** | **无** | ❌ **本件要补的** |

### 1.2 H1 的裁决与用户新裁定**直接冲突**(必须显式改判)

`systems-index.md:827` 逐字:

> ③ **非物品能力**(验尸 / 接生 / 正骨)→ **30 熟练度,不设解锁门**

用户 2026-11-09:「技能的开启是需要剧情的奇遇或者 boss 关卡事件」。
⇒ H1 ③ 的后半句**作废**,前半句(归 30)**保留**。

### 1.3 `skill-system.md:232` 的禁令**不覆盖技法**

> 那一句的上下文是《奇遇与成长路径扩展》提案的 **8 条「战斗解锁」**,裁定理由逐字:
> 「本系统**已含格斗线** … 那 8 个「解锁」是对**已有技能**的叙事包装,非新机制。
> **给技能加门亦违法** —— 本作无天赋树 / 职业 / 等级」
>
> ⇒ 它管的是 **19 项技能**(`skill-system.md:60-82` 的闭集)。
> **技法**(「会不会包扎 / 会不会清创」)从来不在那张表里 —— 它是 10 / 11 的**动作**。
> **本件不新增技能、不改 30 的表**;30 只承接一个**新的事件种类**,与「加技能」是两件事。
> ⚠️ 但这条区别**必须写进 30 的 GDD**(回写义务见 §6),否则下一个人会按 `:232` 判本件违规。

### 1.4 为什么不能「不落流」

解锁状态若只活在内存或存档头:
- 读档后**无法解释**「为什么这一招会」—— 违反 ADR-005「事件流是唯一真源」;
- 诊断难度、教学回放、51 遥测都失去可复算的输入;
- ADR-040 §D11(「角色进度跟人走」)将来要把成长搬出世界档时,没有可搬的东西。

---

## 2. Decision

### D1 — 载体 = 新 Kind `TechniqueUnlocked`,落**病史流**

- 追加在 `EventKind` 枚举的**追加区末尾**(不动任何既有成员的值);
- `entities.yaml` 登记三字段:`stream: history` / `author` / `payload_schema`;
- 路由由 registry 决定(`StreamRouting.Of`),**与枚举位置无关**。

**为什么是病史流而不是世界流**:世界流装的是**世界状态**(POI / 掉落 / 结构,ADR-009 §二),
玩家能力不是世界状态。且病史流已有同族先例 —— `SkillGrown` 也是玩家级事实,同样落在这里(ADR-026)。

### D2 — 写者 = **30**

- 承 H1 ③ 的前半句(「非物品能力 → 30」),只改「不设门」那半句;
- 30 已有 `SkillGrown` 的写者身份与入口(`SkillGrownEmitter` + 主机侧 port),同构复用;
- ⚠️ **调用方 ≠ 写者**:奇遇 / 手册的宿主只是**调用方**,经统一入口请求,由主机 `Append`
  (承 ADR-005 主机唯一 Append)。

### D3 — 入口 = `TechniqueUnlockEmitter.EmitUnlock(...)`(纯载荷构造)

```
TechniqueUnlockedPayload EmitUnlock(int actorId, int techniqueId, int sourceId)
```

> **实现期订正**:起草稿写成四参(含 `long tick`),但载荷**不含 tick**(它是事件头的量,D4)
> —— 多一个形参只会让调用方把 tick 传进来再由载荷丢掉。**现签名 = 三参**。

- **不 Append** —— 与 `SkillGrownEmitter.EmitGrowth` 同构(构造载荷 → 调用方编码 + 建事件 + Append);
- 只挡 **负数**(`actorId < 0` / `techniqueId < 0` / `sourceId < 0`);
- ⚠️ **闭集校验不在发射器**:`Sim.Contracts` 拿不到处置轴(不读数据)。技法 id 是否属于轴闭集
  由**写者侧** `TechniqueUnlockWriter` 校验(它持有轴的全量 id 集)—— 见 §5 M6;
- 住 `Sim.Contracts`(门 A 内,零引擎引用)。

### D4 — 载荷 = `(actor_id:i32, technique_id:i32, source_id:i32)`

| 字段 | 语义 | 约束 |
|---|---|---|
| `actor_id` | 谁学会了 | 玩家 id 空间(ADR-006 Amendment B 适用面扩大) |
| `technique_id` | 哪一招 | **= 处置轴 action id**(见 D5) |
| `source_id` | 从哪学的 | 手册页 / 奇遇 id;**整数**,供排查与 51 遥测,**不参与任何判定** |

- 无 `Level`、无 `patient_id` —— 解锁是**布尔事实**,不是量;
- 事件头 `Tick` / `Seq` 承载时刻与次序;`Patient = PatientId.None`(世界级,与 ADR-007 五支一致)。

### D5 — 技法 id 空间 = 处置轴 `disease_action_axis.json` 的 **action id**

- 同一个 id 同时被三处使用:**解锁**、`treatable_by`(轴内)、10/11 的动作判定 ⇒ **单一 id 空间**;
- **不新建技法表** —— 那会造第二张 id 表并与轴的 `owner` 字段分叉;
- 今日轴 5 行(止血包扎 / 节奏通气 / 柳树皮 / 毛地黄 / 金鸡纳),扩容归内容轮(T7)。

### D6 — 唯一性:同一 `(actor, technique)` **至多一条**

- 重复授予 = **no-op,不写、不抛**(内容重复不是错误;抛会让内容作者卡死);
- 这是有界性的基础(D10)。

### D7 — 查询:`ITechniqueUnlockQuery` 住 `Sim.Contracts`

```
bool IsUnlocked(int actorId, int techniqueId);
```

- **不复用 `ISkillLevelQuery`**:布尔 vs 等级,语义不同;合一会让「未学会」与「0 级」混为一谈
  (后者仍可执行、只是无加成 —— `skill-system.md:518`);
- 实装 = **病史流折叠投影**(仿 `HarvestSkillLevelProjection` 实现 `ISkillLevelQuery` 的先例),
  住 `Gameplay.Boot`;**同源可重建**(ADR-016 §一 三源不变量)。

### D8 — 不参与 7a 折叠、不独立快照

- 折叠谓词 `Folded(p)` **豁免** `TechniqueUnlocked` 行 —— 与 `SkillGrown` 完全同型
  (ADR-026 §⑦ 案 1:独立快照被否,从流重构);
- ⇒ `ADR-010 §三` 追加一条义务(编号归该文件自己的清单),内容 = 「`TechniqueUnlocked` 不折叠、不快照、
  读档后由流重建」。

### D9 — P0 起步集:**建流时一次性写入**

- 判据 = **事件流为空**(新世界);写完后流非空,读档不会重发 ⇒ 幂等;
- 内容 = 「你怀里那本手册教的三样」(`campaign-arc.md:42-51` 的 止血 / 固定 / 通气);
- **具体清单归内容轮**,本件只定机制。**实现轮取的起步值 = `{0, 1}`**(止血包扎 / 节奏型通气
  —— `OQ-10-4` 已裁的 P0 两动作;手册的「三样」在机制上落在两个动作的多拍里)。
  ⚠️ 该数组是 `TechniqueUnlockBootstrap.StartingTechniques`,**内容值,改它不动任何机制判据**。

> ⚠️ **为什么不做成「数据里的 `starting: true` 标志」**:那会造**第二真源** ——
> 同一事实既在烘焙数据里、又在事件流里,两者可分叉(与 `treatable_by` 拆轴的同一条理由)。
> 一次性写入保证「事件流是唯一真源」在**第一天**就成立。

### D10 — 有界性:`≤ |actors| × |techniques|`,单调只增

- 每个 `(actor, technique)` 至多一条(D6);
- **只增不减** —— 不存在「忘掉一招」的路径(`skill-system.md:218`「不掉已解锁的动作」的机制落点);
- 与 tick 频率、帧率无关(解锁由内容事件触发,非逐帧)。

---

## 3. Alternatives Considered

| 备选 | 否决理由 |
|---|---|
| **A. 落世界流** | 玩家能力不是世界状态(ADR-009 §二 的三态分类);且世界流有界性论证(`:§六`)要按 `|actors|×|techniques|` 重算,而它不是世界量 |
| **B. 由已有事件派生**(`PoiStateChanged(奇遇POI已清)` / `EncounterEnded`)| 奇遇**今天没有系统宿主**(H1 本身就是这条缺口);且「同一 POI 清两次」与「解锁」不是同一个量,派生链一改就断 |
| **C. 塞进 `SkillGrown`**(`object_id` = technique id) | 语义挪用:`SkillGrown` 的载荷带 `novelty_class` / `xp_gain`,解锁没有这两样;且会让 51 的成长读数混进「非成长」事件 |
| **D. 只存存档头 / 快照** | 违 ADR-005 唯一真源;读档后无法解释来源(本件 §1.4) |
| **E. 纯内存 + 每局重授** | 读档丢失;且「学过的不会忘」是叙事承诺(`skill-system.md:218`) |
| **F. 新建一张技法表(独立于处置轴)** | 第二套 id 空间;与轴的 `owner` / `treatable_by` 必然分叉(D5) |

---

## 4. Consequences

### 正面

- T4 的技法门首次**有输入源**;`treatable_by` 的过滤从「查表」变成「查表 ∩ 已解锁」;
- 钩子病例的「缺哪一门技法」有 id 可指(`source_id` 之外还有 `technique_id`);
- 51 可离线重算「什么时候学会的、从哪学的」;
- ADR-040 §D11 的角色进度外迁有了可搬的对象(`SkillGrown` + `TechniqueUnlocked` 同族)。

### 代价

- 病史流多一支**非病人**事件(先例:`SkillGrown`);
- 30 的 GDD 要补一条「本禁令不覆盖技法」的边界注,否则会被读成违规(R1);
- 7a 的折叠谓词要增一条豁免(D8),`ADR-010 §三` 义务清单加一行。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| **R1** 「给技能加门」的误判 —— 后续读者按 `skill-system.md:232` 判本件违规 | 高(原文确实写着) | 中(裁决被推翻) | 30 的 GDD 回写补显式边界注(§6);本件 §1.3 已逐字给出区分 |
| **R2** 静态起步集与奇遇授予形成两套写入路径 | 中 | 中(第二真源) | 两者**走同一个** `EmitUnlock` + 同一个 port;起步集只是「在空流上多调几次」 |
| **R3** 重复授予静默吞掉内容作者的错误 | 中 | 低 | D6 选 no-op;port 在 debug 日志打一行(**不落流**) |
| **R4** `source_id` 演变成第二真源 | 中 | 高(判定分叉) | 明文:它**不参与任何判定**,只供排查 / 遥测;按它分支的代码一律违规 |
| **R5** 枚举追加位置写错(插进中间) | 低 | 高(历史存档读错 Kind) | 追加区末尾;ADR-012 黄金夹具兜底(先例见 `EventKind.cs` 追加区注) |
| **R6** 折叠豁免漏做 ⇒ 读档后「学过的忘了」 | 中 | 中 | V-7 专测「`TechniqueUnlocked` 不进折叠视图」;M5 与 7a 同批 |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU(帧时间) | — | 查询 = 每 tick 一次流折叠(与 `SkillGrown` 同量级;现实负载 < 0.1 ms) | 16.6 ms |
| Memory | — | 每个 `(actor, technique)` 一条事件;P0 ≈ 10 条 × 数十字节 | — |
| Load Time | — | 读档时随三流一起解码,无额外步骤 | — |
| Network(联机) | — | 解锁须主机权威(归 45 的 GDD 轮,**本件不裁**) | — |

> ⚠️ **实现轮的口径(2026-11-09)**:查询按 `HarvestSkillLevelProjection` 的**同款 O(n) 自后向前扫描**
> 实装,**不加缓存** —— 在本类内部缓存会引入「缓存何时失效」这个新判据(同该件的立场)。
> P0 的调用面 = 病人出现时的池过滤 / 动作可用性,不是逐帧热路径。**若实测逼近 tick 预算,
> 须另开 ADR 再优化**,不得在实现里私自加缓存。

---

## 5. Migration Plan

| 步 | 内容 | 判据 |
|---|---|---|
| **M1** | `entities.yaml` 追加 `SimEvent.Kind.TechniqueUnlocked` 三字段 | 三字段齐;`author` 非空 |
| **M2** | `Sim.Contracts/EventKind.cs` 追加区末尾加成员 | 既有成员值零位移(黄金夹具 `simEventHeader_matchesGoldenBytes` / `B03_sim_event_header_bytes` 仍绿) |
| **M2b** | 支数字面量 **38 → 39**:生产码 **7 处**(`EventKind.cs:1,14` · `PayloadEncoder.cs:12,141` · `SimEventCodec.cs:84` · `PayloadCodec.cs:32,101`) | ⚠️ 与 `TD-persist-11` 同型:**必须一次改全**。`drug_event_test.cs` 与 `payload_decoder_test.cs` 拿该串当**探针键** —— 只改一处会让那两条白名单完整性夹具**静默退化为恒绿** |
| **M3** | 重跑 `tools/kindgen/kindgen.py` | A1–A5 全过;`StreamRouting.g.cs` 出现该分支,生成参数变 `17 history / 5 case / 17 world = 39 支` |
| **M4** | `TechniqueUnlockPayload` struct + `TechniqueUnlockEmitter` + `PayloadCodec` 重载 | kindgen A2(载荷 ∈ 整数域)过 |
| **M5** | `ITechniqueUnlockQuery`(Sim.Contracts)+ 折叠实装(Boot) | 夹具:空流 ⇒ false;一条 ⇒ true;异 actor ⇒ false |
| **M6** | 主机侧 port(`TryUnlock` —— 查重 + 编码 + Append) | 夹具:重复授予 ⇒ 流长度不变 |
| **M7** | 起步集:空流时写入(D9) | 夹具:新世界 ⇒ N 条;读档 ⇒ 不重发 |

> **✅ M1–M7 全部落盘(2026-11-09)**:`entities.yaml` 新条目 · `EventKind.cs` 追加 · kindgen(39 支,17/5/17)· `Payloads/HistoryPayloads.cs` + `SkillSystem/TechniqueUnlockEmitter.cs` + `PayloadCodec.History.cs` + 两支 dispatch · `Sim.Contracts/ITechniqueUnlockQuery.cs` + `Gameplay.Boot/TechniqueUnlockProjection.cs` · `TechniqueUnlockWriter.cs`(闭集 + 去重)· `TechniqueUnlockBootstrap.cs`(`BootRoot` 第 4.4a 步)· `HistoryFolder.Erase` 显式豁免 · `CompositionRoot`/`CompositionRootServices` 装配。
> ⚠️ **M2b 的「38 → 39」实为 7 处生产码 + 3 个测试字号面**(`drug_event_test` / `payload_decoder_test` / `payload_encoder_test`)—— 起草时只点名 2 个测试,**实现轮实测为 3**(第三个是 36→37 的编码侧计数)。
> 证据 = `production/qa/evidence/impl-adr043-technique-unlock-2026-11-09.md`。

---

## 6. 回写义务(本件落盘后必须同步)

| 文件 | 内容 |
|---|---|
| `design/gdd/systems-index.md:827` | H1 ③「不设解锁门」→「设奇遇门,载体 = ADR-043」;状态「ADR 推迟 P1a」→「ADR 已落」 |
| `design/gdd/skill-system.md` | §1 / §3.4 的「给技能加门亦违法」补边界注:**不覆盖技法**;技法解锁不新增技能 |
| `docs/architecture/adr-010-persistence-save-format.md` §三 | 追加「`TechniqueUnlocked` 不折叠 / 不快照」义务 |
| `docs/architecture/adr-024-kind-single-source.md` | V-2 条目数 38 → 39 |
| `design/registry/entities.yaml` | 新条目(本件 M1) |

---

## 7. Validation Criteria

| # | 判据 | 状态 |
|---|---|---|
| V-1 | `entities.yaml` 条目三字段齐、`author` 非空 | **PASS**(kindgen A4 兜底) |
| V-2 | kindgen A1–A5 全过,`StreamRouting` 有该分支 | **PASS**(39 支,17/5/17) |
| V-3 | 枚举追加后**既有黄金字节**不变 | **PASS**(`SimCodecRoundtripTest` / `DeterminismGoldenFixturesTest`) |
| V-4 | `IsUnlocked` 折叠正确(空 / 单条 / 异 actor) | **PASS** |
| V-5 | 重复授予 ⇒ no-op(流长度不变) | **PASS** |
| V-6 | 空流起步集写入一次;读档不重发 | **PASS** |
| V-7 | `TechniqueUnlocked` 行**不**进入折叠视图 | **PASS(结构性,非独立 Fold 夹具)** —— `Patient = None` 使 `belongsToFolded` 恒假,且 `HistoryFolder.Erase` 显式列该 Kind;未写独立夹具,如实登记 |
| V-8 | 技法门接入后(T4),未解锁病种**永不入池** | **NOT-RUN**(归 T4) |

> **V-1…V-7 有载体,V-8 未跑** —— 禁借绿。证据 =
> `production/qa/evidence/impl-adr043-technique-unlock-2026-11-09.md`(含实现期三处订正)。

---

## 8. 三项裁定(2026-11-09 用户「按你的」· 原为起草轮的分歧点)

| # | 问题 | 裁定 | 落点 |
|---|---|---|---|
| **Q1** | 写者归 **30**,还是另立所有者? | **30** —— 不新立系统,H1 已把「非物品能力」判给它 | `TechniqueUnlockWriter` 住 Boot,author = 30(registry) |
| **Q2** | 技法 id **复用处置轴 action id**,还是另立技法表? | **复用** —— 单一 id 空间,免第二真源 | 写者的闭集 = `axis.Actions[].ActionId`(10 + 11 两侧) |
| **Q3** | P0 起步集用**空流一次性写入**,还是数据表 `starting` 标志? | **空流写入** —— 事件流第一天就是唯一真源 | `TechniqueUnlockBootstrap.Ensure`(BootRoot 第 4.4a 步) |

---

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|---|---|---|---|
| `design/gdd/skill-system.md` | 30 技能 | 「不掉已解锁的动作」(§3.3 `:218`)—— 需有一个**可重建**的解锁集合 | 解锁落事件流(D1)⇒ 只增不减(D10),读档后由流重建(D7) |
| `design/gdd/emergency-procedures.md` | 10 急救 | 动作可用性(规则三 / 规则四的 `action` 输入)| `ITechniqueUnlockQuery`(D7)+ 技法 id = action id(D5)|
| `design/gdd/disease-simulation.md` | 9 | `treatable_by` 的极性/适用集是单一真源(D-9-K)| 技法门**只读**轴、不复制(D5);过滤发生在消费方(T4)|
| `design/gdd/content/campaign-arc.md` | 内容 | 序章「手册教你三样」(`:42-51`)| 起步集 = 空流一次性写入(D9)|
| `design/gdd/persistence-service.md` | 7a | 折叠 / 不独立快照 | D8:豁免折叠(同 `SkillGrown`)|
| `design/gdd/systems-index.md` | 架构 | H1 ③「非物品能力不设解锁门」(`:827`)| **本件改判**:设门,载体 = 本 ADR(§1.2)|

## Related

- **改判**:`systems-index.md:827` H1 ③ 的后半句(「不设解锁门」)—— 见 §1.2
- **范围收窄**:`skill-system.md:232`「给技能加门亦违法」→ 只覆盖 19 项技能 —— 见 §1.3
- 上游裁决:ADR-005 / 007 / 008 / 009 / 010 / 024 / 026 / 030 / 040
- 设计依据:`design/gdd/reviews/technique-unlock-map-2026-11-09.md`
  (三层模型 · 病例两类门 · T1–T7)
- 同族先例:`SkillGrown`(`entities.yaml:2660` · `SkillGrownEmitter` · ADR-026 §⑦)
- 枚举追加纪律:`unity/Assets/Sim.Contracts/EventKind.cs` 追加区注
