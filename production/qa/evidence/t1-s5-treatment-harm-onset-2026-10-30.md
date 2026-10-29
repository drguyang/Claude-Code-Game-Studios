# T1 · S5 —— 新 `Kind` `TreatmentHarmOnset`(登记 / 生成物 / 枚举 / 编解码 / 门豁免)

- **对象**:`ADR-035 §Migration Plan` 的 **S5**;§Decision ④(载荷与所有权)· ⑤(53 写者面扩张)· ⑥(过量支挂起)
- **日期**:2026-10-30
- **判据(S5 行原文)**:A1–A5 全绿;37 → **38**(16 history);贴重跑输出

---

## 一、交付面(15 处)

| 组 | 件 | 内容 |
|---|---|---|
| registry | `design/registry/entities.yaml` | 末尾追加 `SimEvent.Kind.TreatmentHarmOnset`:`stream: history` / `author` / `payload_schema`(6 字段全整数)/ `constraint`(外键 + 档位引用 + 有界性 + 过量挂起) |
| 生成器 | `tools/kindgen/kindgen.py` | 断言数 **37 → 38**;三处注释与 `A5 实测 35→36` 同步 |
| 生成物 | `unity/Assets/Sim/StreamRouting.g.cs` | 重生成:`16 history / 5 case / 17 world = 38 支` + `EventKind.TreatmentHarmOnset => StreamId.History,` |
| 枚举 | `unity/Assets/Sim.Contracts/EventKind.cs` | **末尾追加区**加 `TreatmentHarmOnset`(禁插中间);文件头计数 37→38、15/5/17→16/5/17 |
| 载荷 | `unity/Assets/Sim.Contracts/Payloads/HistoryPayloads.cs` | `TreatmentHarmOnsetPayload`(Tick/PatientId/InjuryId/HarmKind/SeverityBand/Seq,单构造器) |
| 编解码 | `unity/Assets/Sim.Codec/PayloadCodec.History.cs` | `Encode(in TreatmentHarmOnsetPayload)` + `DecodeTreatmentHarmOnset`(tag 1..6,`RequireCompleteMask(…, (1u<<6)-1)`) |
| 分派·解码 | `unity/Assets/Sim.Codec/PayloadCodec.cs` | `case EventKind.TreatmentHarmOnset` + 新增 `DefaultPathMarker` 常量 |
| 分派·编码 | `unity/Assets/Sim.Codec/PayloadEncoder.cs` | `case EventKind.TreatmentHarmOnset`;报文改用 `DefaultPathMarker` |
| 报文 | `unity/Assets/Sim.Codec/SimEventCodec.cs:84` | `37 支` → `38 支` |
| 门豁免 | `unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs` | b7 具名豁免 `TreatmentHarmOnset`(写者归 T2;理由串**含登记日期**) |
| 夹具 | `drug_event_test.cs` / `payload_decoder_test.cs` / `payload_encoder_test.cs` | 闭集哨兵 37/35 → **38/36**;新增 `test_tdPersist11_defaultMarker_isNumberFreeAndOnDefaultPath` |

> **两处 ADR 的 S5 行没写到、实际必须做**:
> ① `kindgen.py` 里的**硬断言数** `if len(kinds) != 37` —— 不改则 `--check` 直接失败;
> ② b7 **豁免表** —— 新 Kind 有 `author` 而写者未落地,不进豁免则构建门红。

---

## 二、判据 ↔ 实跑输出(可复算)

### 2.1 A1–A5

```
$ python3 tools/kindgen/kindgen.py --check
kindgen: A1–A5 全过(--check 不写文件)· A2 严格形:42 个载荷 struct / 170 个字段类型受判(非空跑)
```

### 2.2 37 → 38 与 16 history

```
$ grep -c "^  - name: SimEvent.Kind\." design/registry/entities.yaml
38

$ python3 - <<'PY'   # 按 stream 分布
… ⇒ {'case': 5, 'world': 17, 'history': 16} sum= 38
PY

$ for s in History Case World; do echo -n "$s: "; grep -c "=> StreamId.$s," unity/Assets/Sim/StreamRouting.g.cs; done
History: 16
Case: 5
World: 17

$ grep -n "TreatmentHarmOnset" unity/Assets/Sim/StreamRouting.g.cs
40:            EventKind.TreatmentHarmOnset => StreamId.History,
```

三处数字一致:registry 38 = 生成物 38;分布 16/5/17 两侧逐项相同。

### 2.3 三套件

| 套件 | 结果 |
|---|---|
| `DaYiJingCheng.Tests.PersistenceService` | **254 / 254 / 0** |
| `DaYiJingCheng.Tests.PrescriptionMedication` | **177 / 177 / 0** |
| `DaYiJingCheng.Tests.DataPipeline` | **7 / 7 / 0** |

### 2.4 全量回归

**基线溯源三元组**:截至时刻 `2026-10-30 01:01 CST` · 批次 `S5 收口全量` · commit `2df0830f`+工作树

| 套件 | total | passed | failed | skipped |
|---|---|---|---|---|
| EditMode | **6018** | **5981** | **0** | 37 |
| PlayMode | **238** | **234** | **0** | 2(inconclusive 2) |

- PlayMode 与上次基线 `238/234/0/2/2`(@ 2026-10-28 · `9ba8cbb6`)**逐位相同** —— 本轮未触碰 PlayMode 面。

- **37 条 skipped 全部是既有的 `NOT-RUN` 显式登记**(CameraViewpoint / EmergencyProcedures / EnemyAI /
  Interaction / LevelTool / PatientAI / PlayerController / Telemetry / SkeuomorphicUI),
  逐条核对:无一条来自本轮改动。
- ⚠️ **不主张「与上次基线只差新增数」** —— 上次记录(ADR-034 轮 `5714/5679/0/35` @ 2026-10-28 23:16 · `9ba8cbb6`)
  与本轮之间还有 S3 / S4 两批及其它提交,**未做逐批扣减**,故此处只贴本次实跑值。

---

## 三、证伪轮(S12-4 · BLOCKING)

纪律:变异与跑测**同一命令内**完成;还原判据 = **SHA-256 逐字节**;窗口内未编辑任何文件。

| # | 变异 | 预判红 | 实测红 |
|---|---|---|---|
| M1 | 删 `PayloadCodec.DecodeBoxed` 的 `case EventKind.TreatmentHarmOnset` | 往返类 | `test_adr031_allKinds_roundTripThroughDecoder` + `test_ac2908_allEncodableKinds_roundTrip` = **2 红** ✅ |
| M2 | 删 `PayloadEncoder.EncodeBoxed` 的同名 `case` | 往返类 | 同上 **2 红** ✅ |
| M3 | `DefaultPathMarker` 改成含数字(`[codec-default-38]`) | TD-persist-11 新夹具 | `test_tdPersist11_defaultMarker_isNumberFreeAndOnDefaultPath` = **1 红** ✅ |
| M4 | 把 `TreatmentHarmOnset` 从追加区**插到 `InjuryOnset` 之后** | 黄金字节 | `test_simCodec_simEventHeader_matchesGoldenBytes` + `test_b7_waiverTable_everyEntryHasDatedReason` = **2 红** ✅ |
| M5 | 把生成物的路由改成 `StreamId.World` | — | **0 红** ⚠️ 见下 |

还原:`PayloadCodec.cs` `24ff6d51…` · `PayloadEncoder.cs` `e23cf4ee…` · `EventKind.cs` `4bb49ed7…` ·
`StreamRouting.g.cs` `c289e0fa…` —— 全部逐字节一致,无 `FALSIFY-` 残留。

### M4 的附带发现(真缺陷,已修)

`test_b7_waiverTable_everyEntryHasDatedReason` 要求豁免理由串**含登记日期**。我的豁免文本把
`2026-10-30` 写在了**注释**里、没写进理由串 ⇒ 该夹具转红。**与 M4 变异无关,是 S5 自己引入的**
(它住 `DaYiJingCheng.Tests.Unit.Sim`,不在我先前跑的三套件内)。已把日期补进理由串。

### M5:新 Kind 的**路由**无本地夹具(如实登记)

`StreamRouting.g.cs` 手改成 `World` 后,`DaYiJingCheng.Tests.Unit.Sim` **393/393 全绿** ——
**本地零判别力**。守卫在 CI,不在测试:

```
$ # 模拟 .github/workflows/tests.yml 的 "Generated artifact matches registry" 步骤
$ python3 tools/kindgen/kindgen.py          # 重生成(把路由改回 History)
$ git diff --quiet -- unity/Assets/Sim/StreamRouting.g.cs
CI 步:git diff 非空 ⇒ 会红(OK)
```

⇒ **不构成假绿夹具**(不存在「会绿但测不出」的夹具),但**本地无门**。已登记为判据缺口,
不按「已证」记账。

---

## 四、本件**不**主张

| 项 | 状态 |
|---|---|
| 写者已落地 | ❌ **零写者**:53 的禁忌结算路径归 **T2**(前置 `TD-prescription-09` + `TD-sim-03`);b7 具名豁免即为凭据 |
| 过量支可用 | ❌ §Decision ⑥ 挂起:`Overdose` 成员保留,绑定器 / 构造器双拒 |
| `TD-sim-03` 已解 | ❌ 未动 —— N-1 可行集今日仍为空 |
| S6 / S7 已做 | ❌ 未开工 |
| V-5 已过 | ❌ 仍无处可过 —— 无 CookedWriter / `DataBakeMenu` 入口,今天没有「构建」路径(见 S4 证据件 §五) |
| 本地能抓错路由 | ❌ 见 M5 |
