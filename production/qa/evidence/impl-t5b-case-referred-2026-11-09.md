# T5b 钩子病例的收场载体 `CaseReferred` —— 实施与证据(2026-11-09)

> **范围**:`docs/architecture/adr-044-case-referred-kind.md`(用户 2026-11-09 裁定「按乙」)。
> **上游**:T5a(`impl-t5a-hook-case-judgment-2026-11-09.md`)。
> **本件不声称「转诊可用」** —— 真实调用点(玩家转诊交互)不存在,见 §6。

---

## 1. 改动清单

| # | 文件 | 性质 | 内容 |
|---|---|---|---|
| 1 | `docs/architecture/adr-044-case-referred-kind.md` | **新** | 载体裁决(甲被驳的实证 / 乙采纳 / D1–D7) |
| 2 | `design/registry/entities.yaml` | 改 | `SimEvent.Kind.CaseReferred`(stream: case · author: 37) |
| 3 | `unity/Assets/Sim.Contracts/EventKind.cs` | 改 | 追加**末尾**(既有成员值零位移) |
| 4 | `tools/kindgen/kindgen.py` + `StreamRouting.g.cs` | 改 | 期望支数 39 → **40**;生成 `case` 支 6 |
| 5 | `unity/Assets/Sim.Contracts/Payloads/CasePayloads.cs` | 改 | `CaseReferredPayload`(patient_id / case_id / reason) |
| 6 | `unity/Assets/Sim.Codec/PayloadCodec.Case.cs` | 改 | 编解码(tag 1/2/3,全必填)+ `RequireReasonDeclared` 闭集守卫 |
| 7 | `unity/Assets/Sim.Codec/PayloadCodec.cs` / `PayloadEncoder.cs` / `SimEventCodec.cs` | 改 | 分派 + 支数字面量 39 → 40 |
| 8 | `unity/Assets/Gameplay.Boot/HookCaseClosureWriter.cs` | **新** | 唯一 Append 落点;闭 reason 集 + 去重 no-op |
| 9 | `unity/Assets/Gameplay.Boot/CompositionRoot.cs` / `CompositionRootServices.cs` | 改 | 装配 + 服务袋暴露 |
| 10 | `unity/Assets/Tests/EditMode/CaseSystem/case_referred_test.cs` | **新** | **9 条** |
| 11 | 三处测试探针 | 改 | `payload_decoder_test`(39→40)· `payload_encoder_test`(37→38)· `drug_event_test`(39→40) |

---

## 2. 为什么是「乙」而不是「甲」(ADR-044 §1.2 的实证)

甲(新增 `PatientOutcome.Transferred = 3`)会被**两处解码器**同时挡住,而那是**故意的**:

- `PayloadCodec.History.cs` `RequireOutcomeDeclared`:非 `{1,2}` 即抛;
- `FoldRowCodec.cs` `ValidOutcomes = {1,2}`,理由原文:「未知 outcome(3) = 档损坏 / 跨版本布局漂移」,
  夹具 `fold_row_codec_test.cs:93-94` 逐字断言。

⇒ 加 3 会把**损坏信号**变成**合法数据**(旧档里错位的 3 会被读成「转诊」),
是存档格式级变更(版本 + 迁移 + 黄金夹具重签)。**乙不碰这一层**。

---

## 3. 语义边界(ADR-044 D4 / D5)

| 问题 | 答案 |
|---|---|
| 它是病程终态吗? | **不是**。F4 九态说「病怎么走」;转诊说「这个病人不再由我处置」。`PatientOutcome` 词表不扩 |
| 它自动结案吗? | **不**。与 `CaseClosed` 正交:转诊后病例仍可开着,直到有人结案 |
| 一个病例能转诊几次? | **至多一次**。重复 = no-op(写者侧判,不抛) |
| `reason` 值域? | `{0 = 转诊, 1 = 送医}` 闭集,**编解码两侧都拒收域外值** |

---

## 4. 回归

**三元组**:`2026-11-09T11:15:09Z`(EditMode 结束)/ `11:16:39Z`(PlayMode 结束)·
**全量** · `20d3e2bd`(T5a)+ 工作树。

| 套件 | 本次 | 对照基线(T5a 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7233 / 7196 / 0 / 37`** | `7224 / 7187 / 0 / 37` | **+9 / +9** |
| PlayMode(全量) | **`370 / 366 / 0 / 2 / 2`** | `370 / 366 / 0 / 2 / 2` | 0 |

**等式核对**:`7233 − 7224 = 9` = 新夹具 `[Test]` 数 **9**。✔

**一轮编译失败(如实登记)**:`case_referred_test.cs` 漏 `using DaYiJingCheng.Sim;`
(`StreamRouting` 在该命名空间)⇒ 停在编译期,补上后全绿。

### 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import xml.etree.ElementTree as ET
for p in ("unity/Logs/editmode-t5e.xml", "unity/Logs/playmode-t5b.xml"):
    r = ET.parse(p).getroot(); at = r.attrib
    print(p, at["total"], at["passed"], at["failed"], at.get("inconclusive"), at["skipped"], at["end-time"])
PY
# 期望:
#   editmode-t5e.xml  7233 7196 0 0 37 2026-11-09 11:15:09Z
#   playmode-t5b.xml  370  366  0 2 2  2026-11-09 11:16:39Z

# 支数 = 40(history 17 / case 6 / world 17)
grep -c "^  - name: SimEvent.Kind\." design/registry/entities.yaml
grep -c "=> StreamId\." unity/Assets/Sim/StreamRouting.g.cs
```

---

## 5. 夹具覆盖(9 条)

| # | 判据 | 反证 |
|---|---|---|
| 1 | 路由 = 病例流 | 把 registry 的 `stream` 改成 history ⇒ 本条红 |
| 2 | 载荷往返逐值相等 | 少写一个 tag ⇒ 往返不等 |
| 3 | 写者:唯一 Append、头部 `Patient` = 病人、`Tick` = 入参 | 把 Patient 写成 None ⇒ 本条红 |
| 4 | 重复转诊 = no-op(返回 false、流长度不变) | 删 `AlreadyReferred` ⇒ 本条红 |
| 5 | 不同 `case_id` 不被去重 | 去重键写成「按病人」⇒ 本条红 |
| 6 | 闭集外 reason:写者抛且不留半条 | 删写者的闭集守卫 ⇒ 本条红 |
| 7 | 闭集外 reason:**解码**也拒(改写末 4 字节为 99) | 删 `RequireReasonDeclared` ⇒ 本条红 |
| 8 | 哨兵病人拒收 | 删 `IsNone` 守卫 ⇒ 本条红 |
| 9 | 负 tick 拒收 | 删 tick 守卫 ⇒ 本条红 |

**无自指判据(F3)**:期望值全为字面量。

---

## 6. 未做(不得借绿)

| 项 | 状态 |
|---|---|
| **玩家转诊交互**(42/48 的按钮与流程) | **未做** —— 写者**零生产调用方**(ADR-044 §D7) |
| 转诊后的病例状态机(是否/何时自动结案) | **未定义** —— ADR-044 §D5 明写归 37 GDD |
| 「转诊」与「送医」在玩法上是否等价 | 保留两值;若结算轮认定等价,收窄闭集即可 |
| 钩子病例的生产暴露面 | 仍为零(驱动上限 2 人 + 轮转首两格是伤情,见 T5a 证据 §6) |
| 「章」的运行时边界 | 仍不存在(T5a 已登记) |

> ⚠️ **本轮交付的是「载体 + 写者」,不是「转诊功能」。** 任何人读到这里都应记住:
> 没有调用方与交互,就没有一次真实的转诊发生过。
