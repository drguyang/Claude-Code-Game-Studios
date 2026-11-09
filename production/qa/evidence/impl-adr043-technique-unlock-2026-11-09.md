# ADR-043 技法解锁载体 —— 实施与证据(2026-11-09)

> **范围**:`docs/architecture/adr-043-technique-unlock-kind.md` §5 的 **M1–M7**(用户 2026-11-09
> 「按你的，M1–M7 实现」)。
> **上游**:T1(`027b065d` 拆表)· T2(`impl-t2-registry-views-and-columns-2026-11-09.md`)。
> **本件不声称 T4 的技法门已接线** —— 那要等抽取侧消费 `ITechniqueUnlockQuery`。

---

## 1. 改动清单

| 步 | 文件 | 行 | 内容 |
|---|---|---|---|
| M1 | `design/registry/entities.yaml` | 3351 | 新增 `SimEvent.Kind.TechniqueUnlocked`(stream: history · author: 30 · payload_schema 三字段) |
| M2 | `unity/Assets/Sim.Contracts/EventKind.cs` | 73 | 成员追加在**追加区末尾**(既有成员值零位移) |
| M2b | 生产码 7 处 | — | 支数 38→39:`EventKind.cs:1,14` · `PayloadEncoder.cs:12,142` · `SimEventCodec.cs:84` · `PayloadCodec.cs:32,102` |
| M2b | 测试 3 处 | — | `payload_decoder_test`(38→39 ×2)· `payload_encoder_test`(36→37 ×2)· `drug_event_test`(探针哨兵) |
| M3 | `tools/kindgen/kindgen.py` + `StreamRouting.g.cs` | 311 / 38 | 期望支数 38→39;重跑 A1–A5 全过,生成 `17 history / 5 case / 17 world = 39 支` |
| M4 | `Sim.Contracts/Payloads/HistoryPayloads.cs` | 427 | `TechniqueUnlockedPayload(actorId, techniqueId, sourceId)` |
| M4 | `Sim.Contracts/SkillSystem/TechniqueUnlockEmitter.cs` | 28 | `EmitUnlock` 三参(只挡负数;闭集校验在写者侧) |
| M4 | `Sim.Codec/PayloadCodec.History.cs` | 652 / 661 | `Encode` / `DecodeTechniqueUnlocked`(tag 1/2/3,全必填) |
| M5 | `Sim.Contracts/ITechniqueUnlockQuery.cs` | 18 | `bool IsUnlocked(actorId, techniqueId)` |
| M5 | `Gameplay.Boot/TechniqueUnlockProjection.cs` | 22 | 病史流折叠(O(n),不加缓存 —— 与 `HarvestSkillLevelProjection` 同口径) |
| M6 | `Gameplay.Boot/TechniqueUnlockWriter.cs` | 20 | 闭集门 + 去重 no-op + 唯一 Append |
| M7 | `Gameplay.Boot/TechniqueUnlockBootstrap.cs` | 19 | 空流一次性写起步集;`BootRoot.cs:573`(第 4.4a 步)调用 |
| 装配 | `CompositionRoot.cs` | 2199-2200 | 构造投影 + 写者,入服务袋 |
| 装配 | `CompositionRootServices.cs` | — | `TechniqueUnlocks` / `TechniqueUnlockWriter` 两属性 |
| **连带** | `Sim/HistoryFolder.cs` | 181-186 | `Erase` 的「不抹除面」显式加 `TechniqueUnlocked`(结构上 `Patient=None` 本就不命中,此行使不变式在代码里可见) |
| **连带** | `docs/architecture/adr-010 §三` | 249 | **义务 16** |
| **连带** | `design/gdd/systems-index.md:827` | — | H1 ③「不设解锁门」→ 设奇遇门 |
| **连带** | `design/gdd/skill-system.md` | 234 | 「给技能加门亦违法」的**边界注**(只管 19 项技能) |
| **连带** | `adr-024` V-2 | 241 | 实测支数 39 |

---

## 2. 回归

**三元组**:`2026-11-09T09:13Z`(EditMode)/ `09:14Z`(PlayMode)· **全量** · `027b065d` + 工作树。

| 套件 | 本次 | 对照基线(T2 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7188 / 7151 / 0 / 37`** | `7178 / 7141 / 0 / 37` | **+10 / +10** |
| PlayMode(全量) | **`370 / 366 / 0 / 2 / 2`** | 同值 | 0 |

**等式核对**:`7188 − 7178 = 10` = 新增 `[Test]` 数 **10**(`technique_unlock_test.cs`)。

**定向**:`TechniqueUnlockTest 10/10` · `PayloadDecoderTest 20/20` · `DrugEventTest 30/30` ·
`SimCodecRoundtripTest 30/30` · `DeterminismGoldenFixturesTest 5/5`。

> 黄金字节两条(`B03_sim_event_header_bytes` / `simEventHeader_matchesGoldenBytes`)仍绿 ⇒
> **追加枚举成员没有位移任何既有 Kind 的值**(ADR-012 的判据,红了先查实现、禁改期望值)。

### 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 1) 支数 = registry 条目数 = StreamRouting case 数 = 39
grep -c "^  - name: SimEvent.Kind\." design/registry/entities.yaml
grep -c "=> StreamId\." unity/Assets/Sim/StreamRouting.g.cs
# 2) 剩余「38 支」只允许出现在历史叙述里
grep -rn "38 支" --include=*.cs unity/Assets | grep -v "/Tests/"
```

---

## 3. 验证表(ADR-043 §7 的 V-1…V-8)

| # | 判据 | 状态 | 载体 |
|---|---|---|---|
| V-1 | registry 三字段齐、author 非空 | **PASS** | M1;kindgen A4(author 必填)对此条兜底 |
| V-2 | kindgen A1–A5 全过 + routing 有该分支 | **PASS** | 生成器输出:`A1–A5 全过 → 写出 …(39 支 switch)`;`StreamRouting.g.cs:38` |
| V-3 | 枚举追加后既有黄金字节不变 | **PASS** | `SimCodecRoundtripTest` / `DeterminismGoldenFixturesTest` |
| V-4 | `IsUnlocked` 折叠正确(空 / 单条 / 异 actor) | **PASS** | `test_projection_emptyStream_isFalse` · `test_projection_afterUnlock_isTrueOnlyForThatPair` |
| V-5 | 重复授予 ⇒ no-op(流长度不变) | **PASS** | `test_writer_duplicateGrant_isNoOp` |
| V-6 | 空流写起步集一次;非空流不重发 | **PASS** | `test_bootstrap_emptyStream_writesStartingSetOnce` · `test_bootstrap_nonEmptyStream_isNoOp` |
| V-7 | `TechniqueUnlocked` 行**不**进入折叠视图 | **PASS(结构性,非独立夹具)** | 该行 `Patient = PatientId.None(-1)` ⇒ `HistoryFolder.Erase` 的 `belongsToFolded` 恒假;代码另显式列出该 Kind(`HistoryFolder.cs:186`)。⚠️ **未写独立 Fold 夹具** —— 如实登记,不借绿 |
| V-8 | 技法门接入后未解锁病种永不入池 | **NOT-RUN** | 归 **T4** |

---

## 4. 实施期的三处订正(与 ADR 文本不一致,已回写 ADR)

1. **发射器签名 3 参而非 4 参** —— 起草稿带 `long tick`,但载荷不含 tick(D4)。
   多一个形参只会让调用方传进来再丢掉。ADR §D3 已订正并留原文。
2. **闭集校验落在写者侧,不在发射器** —— `Sim.Contracts` 不读数据,拿不到处置轴。
   `TechniqueUnlockWriter` 持 `ISet<int>` 做校验;发射器只挡负数。ADR §D3 已订正。
3. **「38 → 39」的测试字号面是 3 个不是 2 个** —— 起草只点名 `drug_event_test` /
   `payload_decoder_test`,实测还有 `payload_encoder_test`(36→37 的**编码侧**计数)。
   ADR §5 实现注已补。

---

## 5. 未做(不得借绿)

| 项 | 状态 |
|---|---|
| T4:抽取的技法门(`treatable_by` ∩ 已解锁集) | **未做** |
| 技法起步集的**正式内容**(当前 `{0,1}` 是内容轮前的最小值) | **未做** |
| 奇遇宿主调用 `TryUnlock` 的真实调用点 | **未做** —— 奇遇系统本身未建(H1 的上游) |
| 42/48 的「手册缺页」(钩子病例的呈现) | **未做**(T6) |
| 联机侧的解锁权威(45) | **未做** —— 本 ADR 明确不裁 |
