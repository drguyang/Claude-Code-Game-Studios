# ADR-044: 钩子病例的收场载体 (`CaseReferred`)

## Status

**Accepted**(2026-11-09)

> **2026-11-09 用户裁定「按乙」** —— 即 §3 的三条路线里取 **乙:新增病例流 Kind**。
> 甲(新增 `PatientOutcome.Transferred = 3`)**被驳**,理由见 §1.2(实证:它与折叠行的
> `{1,2}` 损坏哨兵直接冲突)。丙(不新增载体)不满足用户 2026-11-09 的
> 「收场必须明确:转诊 / 送医 / 死亡,三选一」。
> **§Decision D1–D7 由起草轮就地拟定**,未经用户逐条确认(同 ADR-034 / 037 / 038 / 043 口径)。

## Date

2026-11-09

## Last Verified

2026-11-09

## Decision Makers

dr_guyang(用户 · 2026-11-09「按乙」)· technical-director(起草)
· 37 病例系统(病例流唯一写者,ADR-008 §一)
· 9 病程模拟(终态词表的所有者 —— 本件**不碰**它的词表)

## Summary

`technique-unlock-map-2026-11-09.md` §5.2 的用户裁定要求钩子病例的收场
**必须是「转诊 / 送医 / 死亡」三选一,不许「病人自己好了」**。
其中「死亡」已有载体(`PatientOutcomeChanged` 的 `Died`),而**「转诊 / 送医」没有任何载体** ——
全仓 grep 零命中(设计件里只有那句裁定本身)。

裁决:**新增病例流 `Kind` `CaseReferred`**(追加在 `EventKind` 枚举末尾),
载荷 `(patient_id, case_id, reason)`,`reason ∈ {0 = 转诊, 1 = 送医}` 全整数闭集;
写者 = **37 病例系统**(病例流唯一写者);**它不是一个病程终态** ——
不碰 `PatientOutcome` 词表、不碰折叠行、不动存档格式。

## Engine Compatibility

- **零 post-cutoff API**。纯 `Kind` 登记 + 整数载荷 + 既有 codec 装配路径。
- Engine Knowledge Risk **LOW**。

## ADR Dependencies

- **ADR-008**(病例事件流)—— 本件是它 §三 的第 6 支;「按 `Kind` 路由」不变。
- **ADR-024**(Kind 单一真源)—— registry 先改、kindgen 重跑(条目 39 → **40**)。
- **ADR-032**(病程终态 `PatientOutcomeChanged`)—— **本件明确不扩它**;见 §3 Alt 1。
- **ADR-010**(存档)—— 病例流不物理折叠;本件零布局变更。

---

## 1. Context(为什么必须裁)

### 1.1 用户裁定留下了半个载体

§5.2 的三选一里:

| 结局 | 载体 | 状态 |
|---|---|---|
| 死亡 | `PatientOutcomeChanged`(`Died = 2`) | **已有** |
| 转诊 | — | **无** |
| 送医 | — | **无** |

复算(全仓):`grep -rn "转诊\|送医\|Referral\|Transferred" design/gdd docs/architecture --include=*.md`
⇒ 唯一命中是 map §5.2 那句裁定本身。

### 1.2 为什么不能复用 `PatientOutcome`(甲案被驳的实证)

`PatientOutcome` 是 `{Unspecified=0, Recovered=1, Died=2}`,
而**两处解码器硬编码这两个值**:

- `PayloadCodec.History.cs:139` `RequireOutcomeDeclared` —— 非 1/2 即抛;
- `FoldRowCodec.cs:64` `ValidOutcomes = {1, 2}` —— 折叠行的 outcome 出域即抛,
  **且理由写得很清楚**:「未知 outcome(3) = 档损坏 / 跨版本布局漂移」。
  夹具 `fold_row_codec_test.cs:93-94` 逐字断言了这条。

⇒ 加 `Transferred = 3` 会把**损坏信号**变成**合法数据**:旧档里任何一个错位的 3
都会被读成「转诊」。那不是加一个枚举值,是**存档格式级变更**(须版本 + 迁移 +
黄金夹具重签),代价比本件高一个数量级。

### 1.3 为什么是病例流而不是病史流

- 「病人被转走了」是**病例侧结论**(这个病例不再由我处置),不是病程终态(F4 九态说的是病怎么走)。
- 病例流本就不物理折叠(ADR-008),加一支不影响 7a 的任何判据。
- 写者天然是 37(病例流唯一写者,ADR-008 §一)—— **不新增写者**。

---

## 2. Decision

### D1. 新 `Kind` `CaseReferred`,落**病例流**

追加在 `EventKind` 枚举**末尾**(追加区纪律:既有成员值零位移,ADR-012 黄金字节不变)。

### D2. 写者 = 37 病例系统

与病例流其余四支同格。宿主(42 的转诊交互 / 剧本)**只调用写者**,不直接 `Append`
(ADR-005 主机唯一权威)。

### D3. 载荷 = `(patient_id, case_id, reason)`,全整数域

- `patient_id: i32` —— 非哨兵(转诊是病人级事实)。
- `case_id: 三元组(Tick, Patient, Seq)` —— 与 `CaseClosed` / `JudgmentRecorded` 同形。
- `reason: i32` 闭集 `{0 = 转诊, 1 = 送医}` —— **解码期拒收其它值**(与 `reason` 空表的
  「静默空」不同:闭集不完整时宁可炸)。
- 无 tick / 无 seq 字段(它们是事件头部的量;入载荷 = 第二真源)。

### D4. **不是病程终态** —— 不碰 `PatientOutcome` / 折叠词表 / `FoldRowCodec`

见 §1.2。`Folded(p)` 的条件 ①(终态)不受本支影响。

### D5. 转诊**不自动结案**

`CaseClosed` 与 `CaseReferred` 正交:前者说「这个病例有结论了」,后者说「这个病人被转走了」。
两者可以先后发生,也可以只发生其一(转诊后病例仍开着,直到有人结案)。
**本件不定义二者的强制次序** —— 那是 37 GDD 的事。

### D6. 每病例至多一条

同一 `case_id` 的重复转诊 = **no-op**(写者侧判,不抛)。
有界性 = `|病例|`,与 tick 频率无关(扩展 ADR-008 §六,不重写)。

### D7. 触发点**未接**(如实登记)

「玩家点转诊」的交互(42/48)在本轮不存在。本件只交**载体 + 写者**;
真实调用点归 UI / 交互轮。**不得**因此声称「转诊已可用」。

---

## 3. Alternatives Considered

| 方案 | 判定 |
|---|---|
| **Alt 1** 新增 `PatientOutcome.Transferred = 3` | **驳**(§1.2:与折叠行 `{1,2}` 损坏哨兵冲突,存档格式级代价) |
| **Alt 2** 复用 `CaseClosed(treated = false)` | **驳**:`treated=false` 的语义是「结案且未处置」,它同时覆盖「病人死了」「病人跑了」「玩家判断失误」。用它表示转诊 = 让一个 bool 承担三种结局,收场就不可判了 |
| **Alt 3** 世界流新 Kind | **驳**:转诊的粒度是**病例**,不是世界状态;放世界流会让 37 的病例生命周期跨两条流 |
| **Alt 4** 不新增载体(丙) | **驳**:用户裁定「收场必须明确」,无载体 = 结局不可观测 |

---

## 4. Consequences

- **kindgen 条目 39 → 40**(history 17 / case **6** / world 17);`StreamRouting.g.cs` 重生成。
- **零存档格式变更**:`SaveVersion` 不动、无迁移、黄金字节不变(病例流本就是「按 Kind 路由」的追加式)。
- **零折叠影响**:7a 的 `Folded(p)` / 折叠行六字段都不含本支。
- **新增一支的消费面**:转诊状态可从病例流重扫(与 `CaseClosed` 同法),**不需要**新投影类 ——
  真有消费者时再立,避免空抽象。

---

## Risks

| # | 风险 | 处置 |
|---|---|---|
| R1 | 写者无生产调用方(与 ADR-043 的奇遇宿主同格) | 如实登记为缺口(D7);不做假接线 |
| R2 | 「转诊」与「送医」在玩法上可能等价 | 保留两个值:用户逐字点了两个词;若结算轮认定等价,只需收窄闭集(数据侧改动) |
| R3 | 转诊后病例仍开着,可能长期悬挂 | D5 明写「不定义强制次序」,归 37 GDD;本轮不改 |

---

## 5. Migration Plan

| # | 步 | 判据 |
|---|---|---|
| M1 | `entities.yaml` 追加条目(stream/author/payload_schema 三字段齐) | kindgen A1–A5 全过 |
| M2 | `EventKind` 追加(末尾) | 黄金字节两条仍绿 |
| M3 | kindgen 39 → 40 | 生成 `case` 支 6 |
| M4 | `CaseReferredPayload` + codec 编解码(tag 1/2/3,全必填) | 往返逐值相等;缺字段拒收 |
| M5 | `HookCaseClosureWriter`(闭 reason 集 + 去重 no-op) | 夹具:正常写 / 重复 no-op / 未知 reason 抛 |
| M6 | 支数字面量 39 → 40(生产码 7 处 + 测试探针 3 处) | 无残留「39 支」 |

---

## 6. 回写义务(本件落盘后必须同步)

- `design/gdd/case-system.md`(若其 Kind 清单列举过病例流全集)—— 本轮 grep 后再定。
- `docs/architecture/adr-008-case-event-stream.md` §三:补第 6 支(本件不改 ADR-008 正文,只在
  registry 与 traceability 登记;ADR-008 的 §三 已降级为路由注记,ADR-024 §② 以 registry 为准)。
- `design/registry/entities.yaml`(M1)· `EventKind.cs`(M2)· `StreamRouting.g.cs`(M3)。

---

## 7. Validation Criteria

| # | 判据 | 状态 |
|---|---|---|
| V-1 | registry 三字段齐、author 非空 | **PASS** |
| V-2 | kindgen A1–A5 全过,`case` 支 = 6 | **PASS**(`A1–A5 全过 → 40 支 switch`) |
| V-3 | 枚举追加后既有黄金字节不变 | **PASS**(全量 EditMode 0 红) |
| V-4 | 载荷往返逐值相等 | **PASS**(`test_payload_roundTrips`) |
| V-5 | 重复转诊 = no-op;未知 reason 抛 | **PASS**(`test_writer_duplicateReferral_isNoOp` · `test_writer_unknownReason_failsLoudWithoutWriting` · `test_decoder_unknownReason_isRejected`) |
| V-6 | 路由 = `StreamId.Case` | **PASS**(`test_routing_caseReferred_goesToCaseStream`) |

---

## GDD Requirements Addressed

| 需求 | 出处 |
|---|---|
| 钩子病例收场三选一(转诊 / 送医 / 死亡) | `design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2(用户 2026-11-09 裁定) |

## Related

- `docs/architecture/adr-008-case-event-stream.md`(病例流)
- `docs/architecture/adr-024-kind-single-source.md`(Kind 单一真源)
- `docs/architecture/adr-032-patient-outcome-kind.md`(**不扩**)
- `production/qa/evidence/impl-t5a-hook-case-judgment-2026-11-09.md`(T5a 判定半边)
