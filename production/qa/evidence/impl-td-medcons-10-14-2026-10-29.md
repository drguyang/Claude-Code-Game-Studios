# 实现证据 —— `TD-medcons-10` + `TD-medcons-14` 结案(2026-10-29)

- **对象**:`docs/tech-debt-register.md` 的 `TD-medcons-10` / `TD-medcons-14`(A 组 4 项中的 2 项)
- **用户裁定**:
  - `14` —— 「**收窄到「出现类」事件**」(本轮 AskUserQuestion 选项 1,推荐项)
  - `10` —— 无需裁定(纯机械加固,影响面已在债册登记)
- **同批未动**:`TD-medcons-13`(卡装配轮)· `TD-medcons-15`(卡数值轮)—— 见 §五

---

## 一、`TD-medcons-14` —— CAP 只对「病人出现类」事件生效

### 改前(缺陷)

`EventStream.AppendCore` 的 AC-15 分支判据是「**任何带真实 `patient_id`** 的事件,病人不在场且
在场数 ≥ 24 ⇒ throw」。后果:`ConsequenceResolved`(离屏病人的后果)在 CAP 满时被当成
**新病人出现**拒收 ⇒ 后果永久不发,异常沿 `ConsequenceEmitter.Emit` 上抛,破 AC-53-14
「后果状态进世界流」。

`ConsequenceEmitter.cs` 类头当时已自陈这是**跨域接缝**:

> 「该接缝须 9/7a 裁(离屏病人是否应计入 PresentCount / 或后果支走 CAP 豁免)。」

⇒ 这是**登记过的未裁缺口**,不是新发现的 bug。

### 改后

| 文件 | 改动 |
|---|---|
| `unity/Assets/Sim/EventStream.cs` | 新增 `IsPatientAppearance(EventKind)`(今日恰 = `DiseaseOnset`),CAP 分支判据改为 `IsPatientAppearance(e.Kind) && e.Patient != PatientId.None && !IsPresent(...)`;文件头「有界性」一行与门内注释同批改注 |
| `unity/Assets/Sim/Consequences/ConsequenceEmitter.cs` | 类头接缝登记追加「已裁并落地」注(原文不删);S-B2 报文与 `SelectPending` 的「CAP 半提交」举例改注 —— CAP **不再**是本件成因,守卫本身保留(他写者推进流尾仍可造出同一形态) |

**口径依据**:CAP 的量纲是「同场被模拟病人数」(ADR-008 §六 · `TR-disease-021` 叫
`PATIENT_APPEARANCE_CAP`),只有「让某病人开始存在/进入模拟」这一件事会改变它 ——
ADR-030 ① 已裁「病人出现与病程 onset 是**同一个**事件,不立第二支」⇒ 今日恰 = `DiseaseOnset`。

**维护义务已写进代码**:新增「让病人进入模拟」的 Kind 须同批加进 `IsPatientAppearance`,
漏加则该路径绕过 CAP(有界性论证失效且是静默的);反之不得把无关 Kind 塞进去 ——
后者正是本条债的成因。

### 夹具

| 夹具 | 位置 | 断言 |
|---|---|---|
| `test_cap_rejectsBeyond24`(改载荷) | `EditMode/DiseaseSimulation/event_stream_test.cs` | 载荷由 `ActorCellEntered` 改为 `DiseaseOnset` —— 旧载荷在新口径下**不再被 CAP 拦**,留着会退化成一条恒绿 |
| `test_medcons14_offscreenNonAppearanceEvents_notCapped_onlyOnsetIs`(**新增**) | 同上 | ① `ConsequenceResolved` / `CaseClosed` / `CareApplied` 在「满 CAP + 离屏 + 真实 id」下**照常入流**;② `DiseaseOnset` 在**同一场景**下仍拒收(反半,防「收窄」退化成「取消」) |
| `test_tdMedcons14_offscreenPatientDueConsequence_emitsDespiteFullCap`(**由红转绿 · 改名**) | `EditMode/MedConsequences/medcons_delayed_emit_test.cs` | 原 `test_w5_…_capsThrowPropagates_notSwallowed` 断言的是**相反**行为(抛且不吞)。收窄后该行为被裁定为缺陷 ⇒ 断言翻面,并**同时**断言后果真的落地(不是「不抛但也不发」) |

> ⚠️ **旧断言为什么不留一条绿的副本**:留绿的旧断言 = 把缺陷钉成规格。按「原文不删」纪律,
> 旧断言以**文字**留档在该测的 `<remarks>` 里,不保留为可执行夹具。

---

## 二、`TD-medcons-10` —— 共享词法器拒重复 property

### 改前(缺陷)

`JsonStage1Lexer.ReadValue` 把对象属性收进 `List<KeyValuePair<string, JsonNode>>`,
`Props.Add` 前**零重名检查**。后果不是「报错不够早」,而是**同一份 JSON 有两种读法**:
下游 binder 有的线性取**首个**、有的建 `Dictionary` 让**后者覆盖** ⇒ 哪一份生效取决于
**读者实现**,改一个 binder 的取法就能让同一数据换含义 = 静默丢字段。共享词法器 ⇒ 10+ binder 共同入口。

### 改后

`unity/Assets/Editor.Tools.Bake/JsonStage1Lexer.cs`:读到属性名后**先在 `node.Props` 内扫同键**,
命中即抛 `JsonReaderException`,报文点名重复的键与行号。判据放在词法层(阶段 1),
与既有「对象未闭合」同性质 —— **报错要落在坏掉的地方**(ADR-014 §三)。

### 现网影响面(实测,非推断)

```bash
python3 - <<'PY'
import json,glob,io
bad=[]
def hook(pairs):
    seen=set()
    for k,v in pairs:
        if k in seen: bad.append(k)
        seen.add(k)
    return dict(pairs)
for f in sorted(set(glob.glob('assets/**/*.json',recursive=True))):
    json.load(io.open(f,encoding='utf-8'),object_pairs_hook=hook)
print(len(bad))
PY
# 实跑输出:83 份 JSON 中重复键 0 处、解析错误 0 处
```

⇒ 现网烘焙**不受影响**,不存在「加固后某份数据突然红」。

### 夹具 — `json_stage1_lexer_duplicate_key_test.cs`(新增,6 条)

| 类 | 夹具 | 断言 |
|---|---|---|
| 正 | `test_duplicateKey_topLevel_isRejected_namingTheKey` | 顶层重复 ⇒ 拒,报文含键名 + 「重复」 |
| 正 | `test_duplicateKey_nestedInsideArray_isRejected` | 数组元素内重复 ⇒ 拒(防只修顶层) |
| 正 | `test_duplicateKey_secondOpening_isRejected_too` | 重复发生在第 4 个属性 ⇒ 仍拒(不是「首个属性」特例) |
| 反 | `test_sameKeyInDifferentObjects_stillParses` | 同名字段出现在**不同对象**里**合法** —— 防把判据误做成「全文出现过一次即拒」 |
| 反 | `test_keyPrefixOrCaseDifference_isNotDuplicate` | `delay` / `delay_ticks` / `Delay` 互不重复(精确比较,不归一化) |
| 反 | `test_emptyAndSingleProperty_stillParse` | 空对象 / 单属性对象照常 |

---

## 三、证伪轮(S12-4 · BLOCKING 级)

命中的形态:**③ 定义域收窄**(两条都属此类)· **⑤ 阈值边界**(CAP ≥ 24)。

纪律:变异与跑测**在同一个 bash 命令内完成**(改 → 跑 → 还原),还原判据 = **SHA-256 逐字节**,
不用 `git checkout`(会抹掉未提交改动)。窗口内**未编辑任何文件**(含测试件)。

改前哈希:

```text
3ccaeecd5f324824e319279021e9a0d6a23493b317fa7281ce37367edc267532  unity/Assets/Sim/EventStream.cs
694a435907e1a74389fdc797edda569e31c7eda7c2d6bea2fb4364f31b5e2dfd  unity/Assets/Editor.Tools.Bake/JsonStage1Lexer.cs
```

| # | 变异 | 目标夹具 | 结果 |
|---|---|---|---|
| M1 | `IsPatientAppearance` → `return true`(= 旧宽口径,任何 Kind 都受 CAP) | `test_medcons14_offscreenNonAppearanceEvents_…` | ✅ 红(16 跑 15 过 1 红) |
| M2 | `IsPatientAppearance` → `return false`(= 彻底取消 CAP) | `test_cap_rejectsBeyond24` (+ 上一条的 ② 半) | ✅ 红(14 过 2 红) |
| M3 | 词法器同键检查条件追加 `&& false` | 三条 `test_duplicateKey_*` | ✅ 红(3 过 3 红;三条反例仍绿 = 判据未过宽) |

三次还原均 `sha256sum -c` 逐字节通过。

**M1/M2 是一对**:只留一个方向会漏掉「一刀切放宽」或「一刀切收紧」中的一半。

---

## 四、回归(三元组)

| 套件 | 本轮 | 基线 | 等式 |
|---|---|---|---|
| EditMode | **`5985 / 5948 / 0 / 0 / 37`** | `5978 / 5941 / 0 / 0 / 37`(2026-10-29 · 全量 · TD-listen-01 结案批) | `total +7` = 新增 `[Test]` 6(新文件)+ 1(`event_stream_test`)· `passed +7` · `skipped ±0` · `failed 0` |
| PlayMode | **`238 / 234 / 0 / 2 / 2`** | `238 / 234 / 0 / 2 / 2` | 逐项相同(零变动) |

**溯源三元组**:截至时刻 `2026-10-29`(CST)· 批次 = **全量**(非增量)· commit `4042a0fc` + 工作树。

可复算:

```bash
grep -c '\[Test\]' unity/Assets/Tests/EditMode/DiseaseSimulation/json_stage1_lexer_duplicate_key_test.cs  # 6
grep -c '\[Test\]' unity/Assets/Tests/EditMode/DiseaseSimulation/event_stream_test.cs                     # 16(改前 15)
python3 tools/debtcount/debtcount.py --check && echo OK
```

---

## 五、本件**不**主张

| 项 | 状态 |
|---|---|
| `TD-medcons-13`(`*.cooked` 无镜像读方) | ❌ **未动** —— 卡**装配轮**(读方归属未见裁决;现在造一个 = 替装配轮发明契约) |
| `TD-medcons-15`(禁忌/过量代价表) | ❌ **未动** —— 卡**数值轮**(表要数值,数值归用户) |
| A 组其余 15 条 | ❌ 不在本轮(用户指令「先从 A 的 4 项开始」) |
| `TD-clinic-17`(`FloatParseHandling = Decimal` 字面偏离) | ❌ **仍 open** —— 同一文件被改过**不等于**本条已还;该行的触发条件已就地补注 |
| CAP 的**其它**失效面 | ❌ 未测 —— 本件只证「后果不再被 CAP 拦」与「出现类仍被拦」两端 |
| `IsPatientAppearance` 未来的完备性 | ❌ 无机械门 —— 靠代码内维护义务注释 + 本件的两侧夹具;**新增 Kind 时仍可能漏加** |
