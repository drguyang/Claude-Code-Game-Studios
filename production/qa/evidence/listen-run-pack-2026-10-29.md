# 听测执行包(2 名被试 · 2026-10-29 裁定批)

> **本件是执行单,不是证据件。** 证据仍落在三份既有协议件上:
> `breath-layers-listen-evidence.md`(38 项)· `main-channel-listen-evidence.md`(5 项)·
> `voice-variant-listen-evidence-2026-10-20.md`。
> 本件只做三件事:① 把三份协议压成一份可照做的流程 ② 定死记录格式 ③ 写明**前置现在到哪了**。
>
> **裁定来源(用户 2026-10-29 两轮)**:
> ① 先修代码阻断再听 · ② **≥1 人过即算过** · ③ 8 条代码债全修 · ④ 证据 = **具名 A/B + 日期 + 设备**。

---

## 一、前置状态(逐条实测,2026-10-29)

| # | 阻断 | 今日状态 | 判据 |
|---|---|---|---|
| **L-6** | 运行时零 active `AudioListener`(全场景静音) | ✅ **已修(账面滞后)** | `BootRoot.cs:490` `cameraObj.AddComponent<UnityEngine.AudioListener>()`,修于 2026-10-20 |
| **L-1** | 无病人离场路径 | ❌ **仍 open** | `grep -rn "RemovePresent" unity/Assets --include=*.cs` ⇒ 仅 `IPresenceWriter` 声明 + `PresenceRegistry` 定义 + 一条测试,**生产零调用方** |
| **L-2** | 听诊档位恒最低档 | ❌ **仍 open** | `CompositionRoot.cs:810` 仍是局部 `int queryLevel = 1;`,全文件无回写 |
| **L-3** | 病程被占位曲线压到 1.55 s | ⛔ **受阻 —— 设计判据冲突,须裁定**(见 §五) | 尝试拉长曲线 ⇒ 当场打红两条既有 Q1 判据,已撤回 |
| **L-4** | 刷人间隔 20 s/人 | ✅ **已修** | `PatientAppearedDriver` `SpawnIntervalTicks` 400 → **60**(20 s/人 → 3 s/人);EditMode 全量 `5852/5815/0/0/37` 零红 |

⇒ **听测今日仍不可执行。** L-1 / L-2 / L-3 / L-4 四条清完之前,勾任何一项都是借绿
(承三份协议件抬头的 ⛔ 声明)。

**已裁的修法**(用户 2026-10-29):
- **L-1** = 由病程终态驱动 —— 订阅既有的 `PatientOutcomeChanged`(ADR-032)⇒ `RemovePresent`,**不新立机制、不新开 Kind**。
- **L-3** = 只拉长 `CreateFallbackRegistry` 的**占位**曲线(注释须写明「仅听测装配,不是真病程值」),真数值仍归数值轮。
- **L-4** = 随 L-3 一并核对可听窗口(`SpawnIntervalTicks` 本就是「接线旋钮,非裁定项」)。
- **L-2** = 档位须有回写来源(生产契约 `ISkillGrowthPort.QueryLevel(actorId, skillId)` 已在 `CompositionRoot.cs:1045` 构造,但**位置在 `queryLevel` 使用点之后** ⇒ 需调序,属结构性改动)。

---

## 二、判定口径(本轮裁定,覆盖三份协议原措辞)

| 项 | 裁定 |
|---|---|
| 通过判定 | **≥1 人「过」即计该条通过**;两人分歧的条目**单独登记分歧**,不因分歧而判不过 |
| ⚠️ 例外 | `AC-44-08`(细湿啰音非连续水声)与 `AC-44-09`(无提示音铁律)是 **BLOCKING 级** —— 这两条**不适用** ≥1 人规则,须 **2/2** 且分歧时以「有人听到水声」为不通过 |
| 记录格式 | **具名 被试 A / 被试 B + 日期 + 设备型号**,逐项写两人各自的结论 |

---

## 三、执行流程(照做即可)

### 0. 前置检查(每次开听之前跑)

- [ ] 主相机上有且仅有一个 active `AudioListener`(Hierarchy 搜 `AudioListener`,计数 = 1)
- [ ] 戴耳机(不要外放 —— 呼吸层与遮挡的低通差异在外放上不可辨)
- [ ] 记录设备:耳机型号 / 声卡 / 系统音量位置

### 1. 呼吸两层(38 项,`breath-layers-listen-evidence.md`)

- 协议:双盲 forced-choice,三对配对 **(0,1)(1,2)(0,2)**
- 每对 ≥ 8 次;两名被试各做一轮
- 记录:每对「答对次数 / 总次数」+ 两人分别结论

### 2. 主通道(5 项,`main-channel-listen-evidence.md`)

- 协议:双盲 forced-choice(被试不参与素材制作)
- 记录同上

### 3. 语声变体(AC-44-03 / 04,`voice-variant-listen-evidence-2026-10-20.md`)

- ⚠️ **须在 `TD-audio-034` 修完之后听**(今日 `Intensity` 轴与 `gender` 轴**都没接线**,
  同一病人同一 cue 无论轻重恒走同一桶 ⇒ 现在听等于听一个**平坦**的变体集,结果无效)
- 记录同上

---

## 四、签署表骨架(听测完成后填)

| 项 | 协议 | 被试 A | 被试 B | 合并判定(≥1 人过) | AC |
|---|---|---|---|---|---|
| 呼吸两层 · 三档可辨 | breath-layers | `<待填>` | `<待填>` | `<待填>` | AC-44-02 |
| 呼吸两层 · 非静音型(细湿啰音**非**连续水声) | breath-layers | `<待填>` | `<待填>` | **须 2/2** | AC-44-08 |
| 主通道 · 5 项 | main-channel | `<待填>` | `<待填>` | `<待填>` | AC-44-17 |
| 语声变体 · 3 档强度分桶 | voice-variant | `<待填>` | `<待填>` | `<待填>` | AC-44-03 |
| 语声变体 · 交叉淡化 ≥ 50 ms | voice-variant | `<待填>` | `<待填>` | `<待填>` | AC-44-04 |

**被试信息(必填)**

| 字段 | 被试 A | 被试 B |
|---|---|---|
| 称呼(具名) | `<待填>` | `<待填>` |
| 听测日期 | `<待填>` | `<待填>` |
| 耳机型号 | `<待填>` | `<待填>` |
| 是否有听力学 / 医学背景 | `<待填>` | `<待填>` |

> ⚠️ **本表任何 `<待填>` 未填 ⇒ 对应债不得转 `closed`。** 填完并由编排方复核后,
> 才回填 `TD-audio-100 / 101 / 102 / 103 / 105` 与 `TD-listen-01`。


---

## 五、L-3 的判据冲突(2026-10-29 实测,须用户裁定)

**尝试**:按裁定「只拉长占位曲线」,把 `CompositionRoot` 里 fallback 的
`TauRise` 5 → 120、`TauPeak` 40 → 400,让 `SeekMin(0.45) → DeathBandMin(0.90)`
的窗口从 **0.4 s** 拉到 **≈10.2 s**。

**结果**:EditMode 当场 **2 红**:

| 红测 | 为什么 |
|---|---|
| `test_q1_compositionRoot_positionCrossesFloor_withinPlayerWait` | 它钉死 **`firstReadable ≤ 60 tick`**(3 s),理由是 2026-10-12 第五轮 playtest 的病根「按 E 要等很久」。新曲线 `firstReadable ≈ 464 tick`,直接违约 |
| `test_q1_compositionRoot_examine_yieldsWords_atInitialSkill` | 该测推进到 τ=60 就断言可读 |

⇒ **两条要求在**同一条曲线上**数学上不可同时成立**:

```
可读门槛   position ≥ 0.9752(floor(skill=1))
死亡带下限 position ≥ 0.90(DeathBandMin)⇒ BreathShouldLive = false
firstReadable ≤ 60 tick

指数上升 1-exp(-(τ-20)/T):
  τ(0.975) = 20 + 3.70·T  ≤ 60   ⇒  T ≤ 10.8
  可听窗口 = τ(0.90) − τ(0.45) = (2.303 − 0.598)·T ≤ 0.85 s
```

**即:只要「按 E 不必等」的 60 tick 判据不动,呼吸可听窗口就上不了 1 s。**

### 三条出路(请裁一条)

| 案 | 做法 | 代价 |
|---|---|---|
| **甲** | 放宽 Q1 判据(`firstReadable ≤ 60` → 按新曲线重定),让单条曲线同时满足 | 动了**已验收**的 playtest Q1 判据,须写明理由与新的等待预算 |
| **乙** | 给听测**另配一条病种条目**(`DiseaseId = 2`,`DiseaseKey = "fallback_audition"`),长曲线只服务听测装配;`DiseaseId = 1` 原封不动 | 多一条接线期占位病种;`PatientAppearedDriver` 现在取 `registry[0]`,听测路径要能选第 2 条 |
| **丙** | 接受窗口 < 1 s,听测改用**手动逐档触发**(不走自然病程) | 协议要重写,且与三份既有协议件的「自然病程下判别」口径不符 |

**在裁定之前,L-3 保持原值,L-1 / L-2 保持 open,两个人仍不能开听。**
