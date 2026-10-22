# 草案:`CareApplied.phase` 序数定义权归属(勘察件 · 2026-10-22)

> 状态:**纯勘察,未落盘任何仓库改动**。本文只列事实与两案对比,**不替用户选**。

## ① `CareApplied` 的 `payload_schema` 原文

`design/registry/entities.yaml:2186`(逐字):

```
payload_schema: "tick: i64; action_id: i32(护理动作表 ordinal,K ≤ |护理动作表|); caregiver: i32; phase: 枚举(起 止)。一条事件表示起或止,写入期校验 t(止) ≥ t(起);语义 = 玩家主动护理动作;放医馆本身不计数,EnvMod 不得推出本事件"
```

同条目 `constraint`(`:2187`)同样只写「起/止」,无序数:

```
... 载荷 = { tick, 护理动作_id, 照护者, 起/止 }(均整数/枚举;动作 id ∈ 护理动作表,K ≤ |护理动作表|);一条事件表示【起】或【止】;写入期校验 t(止) ≥ t(起)。 ...
```

`author: "24 医馆与案头(以入向事件写入;disease-simulation.md 规则)"`(`:2185`)。
⚠️ 写者**尚未落地**:`unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs:129` 具名登记
`("CareApplied", "24 医馆与案头写者未落 ...")`。

## ② 同型「裸 int 枚举」字段的序数解决方案对照

| 字段 | 序数定义在哪(file:line) | 载体形态 | 越界门 | 备注 |
|---|---|---|---|---|
| `CareApplied.phase` | **无权威定义** | — | `DiseaseCourse.cs:154` 实现自设 fail-loud(0/1) | 本草案主题 |
| `polarity`(处置) | `unity/Assets/Sim/Prescription/PrescribeFlow.cs:29` `public enum PrescriptionPolarity : int`(`Symptomatic=0 · Causal=1`,见 `ProgressionEvaluator.cs:77-85` 注释) | **C# 枚举,住 `Sim`(非 Contracts)**;载荷字段是 `int`(`HistoryPayloads.cs:175`) | 载荷侧**无**门(codec `PayloadCodec.History.cs:317` 直写 int32);`RegistrySchema.cs:435-437` 的 `R1_CHECK_03` 只管 21a 注册表行 `∈{0,1,2}` | 同注释自陈**缺口**:10 的 `EmergencyActionRow.Polarity` 是另一张表,GDD `:203` 只写集合未声明序数 |
| `method`(急救) | **无权威序数**。GDD `design/gdd/emergency-procedures.md:206` 只写 `method ∈ { Manual, Skip }` | 私有 const **两处副本**:`Gameplay.Boot/TelemetryReporter.cs:346` `EmergencyMethodManual = 0`;`Tests/EditMode/Telemetry/f3_f7_wiring_test.cs:40-41` `MethodManual=0 / MethodSkip=1` | 无门 | entities.yaml `:2403` 只写「枚举(Manual 跳过)」 |
| `cause`(急救) | **无权威序数**。GDD `:207` `cause ∈ { 玩家选择, 降级 }`;`:464` 明写「整数枚举,载荷归 10、枚举名进骨架 —— 承 ADR-021 纪律」 | 无枚举;测试按 `0=玩家选择 / 1=降级` 硬写(`f3_f7_wiring_test.cs:145,181`) | 无门 | entities.yaml `:2420` 只写「cause: 枚举」 |
| `DiseaseKind` | `unity/Assets/Sim/RegistrySchema.cs:151-167` `public enum DiseaseKind` | **C# 枚举**,含显式零值哨兵 `Unspecified = 0`,注释「一律拒收(`R1_CHECK_31`)。不是合法数据形态」 | **注册表加载期**校验(非载荷) | 零值陷阱 = 具名哨兵 + 加载期拒收,不是靠调用方自觉 |
| `PoiState` | `unity/Assets/Sim/World/PoiStateMachine.cs:26-31` `public enum PoiState : int {0,1,2}` | **C# 枚举 + entities.yaml 散文里写死序数**:`:2123` `new_state: 枚举(PoiState 三态 0 Undiscovered → 1 Discovered → 2 Resolved,单调不可逆;见 constants.PoiState)` | 转移表拒逆转移(`PoiStateTransferResult.InvalidTransfer`);**解码侧无范围门**(`PayloadCodec.World.cs:378-402`) | ADR-021 把「具体值」委派给 6 的 GDD,`:2124` 记「2026-09-16 由 6 的 GDD 定稿」⇒ **两处都写了** |
| `PatientOutcome`(**最强先例**) | `unity/Assets/Sim.Contracts/Payloads/HistoryPayloads.cs:55-65` `public enum PatientOutcome : int` | **C# 枚举住 Contracts + 载荷字段仍是 `int`**(`:91`) + **registry 写死序数**(`entities.yaml:3276` `PatientOutcome 枚举:Recovered=1 / Died=2;Unspecified=0 是漏填哨兵`) | **codec 解码门**:`Sim.Codec/PayloadCodec.History.cs:141-146` 拒收非 Recovered/Died | 三处齐备(枚举 + registry 序数 + 解码门),是本项目目前最完整的一套 |

## ③ GDD 侧对「起/止」到底说了什么

**`design/gdd/disease-simulation.md:164`**(规则六病史元组,逐字):

```
病史 = [ (onset_tick, 病种_id, patient_seed, Seq),
         (tick, 处置_id, 施予者, polarity, drug_potency, half_life, Seq),
         (tick, 护理动作_id, 照护者, 起/止, Seq), … ]   // 护理动作起止(2026-09-16 三轮)
```

⇒ 只给了**一个中文词 `起/止` 占据一个字段位**,没有序数、没有字段名 `phase`、没有类型。

**`design/gdd/disease-simulation.md:208-224`**(照护事件化,要点逐字):
- 「「持续照护」在事件流里 = **护理动作的起止事件**(`CareApplied`,载荷带 tick + 动作 id),由 24 以**入向事件**写入」
  ⇒ 连载荷字段都只列了 **tick + 动作 id 两项**,`phase` 在这段里**根本没被点名**。
- 「`last_intervention` 的「照护最后一刻」= 最后一条**护理动作止**」
- 修正口径:「`last_intervention(照护) = max(最后一条护理动作止, 最后一条护理动作事件 tick + CARE_GAP)`」
  ⇒ 语义上「止」必须**可判别**,否则这条派生式算不出来 —— 这是**语义需求**,仍不是序数定义。

**`design/gdd/clinic-machine.md`(系统 24,author)**:**没有任何起/止语义或序数定义**。
仅 `:10` 上游契约提到「9 ... 只收护理动作事件」;`:143` / `:303` 只讲 `EnvMod` 不进 F4 照护判定。

⇒ **结论:author(24)自己也没定义序数**,与 `WriterExistenceGate.cs:129`「写者未落」一致。

## ④ 两案对比

### 甲案 —— `entities.yaml` 的 `payload_schema` 里把序数写死

改法:`entities.yaml:2186` 的 `phase: 枚举(起 止)` → `phase: 枚举(起=0, 止=1)`(`:2187` constraint 同步)。

| 维度 | 内容 |
|---|---|
| 改动面 | **1 文件 1–2 行**(`entities.yaml:2186` / 可选 `:2187`) |
| kindgen | **不需要重跑改产物**。`tools/kindgen/kindgen.py` 的 A2 只扫 C# struct 字段类型(`INT_PRIMITIVES` 白名单,`:197-211`);A2b 只扫 registry 散文里的 `float/double` 字样与数组界证据(`:224-234`)—— **都不解析枚举序数**。`StreamRouting.g.cs:25` 只有 `EventKind.CareApplied => StreamId.History,` 一行,与序数无关。门(A1–A5)照跑,预期不变(⚠️ 本件未实跑验证) |
| `DiseaseCourse` 常量 | **保留 0/1 不变**;但 `DiseaseCourse.cs:171-174` 的 remarks 现在写「序数出处:GDD `:164` 的「起/止」」—— **这句是失实的**(`:164` 无序数),须改为引用 `entities.yaml:2186` |
| 值域门 | 保留 `NoteCareAction` 的 fail-loud,但错误串「本实现约定 0 = 起 / 1 = 止」应改为「entities.yaml:2186」 |
| 优点 | 改动最小;把权威落在 **ADR-024 认定的 registry 真源**上;与 `PoiState`(`:2123`)、`PatientOutcome`(`:3276`)的「registry 写死序数」写法一致 |
| 代价 | **纯散文,机器不可检** —— 序数正确性仍只靠人读 + 实现侧硬编码门;仍是「两处真源」(registry 散文 + C# 常量),没有任何机械门把它们钉在一起 |
| 先例一致性 | 与 PoiState/PatientOutcome 的 **registry 半边**一致;缺它们的另外半边(枚举类型 / 解码门) |

### 乙案 —— 升格为具名 C# 枚举 `CarePhase { Start = 0, Stop = 1 }`,载荷仍传 int

| 维度 | 内容 |
|---|---|
| 改动面 | ① 新增枚举(建议 `Sim.Contracts/Payloads/HistoryPayloads.cs`,与 `PatientOutcome` 同文件同层);② `DiseaseCourse.cs:169/174` 两常量改为 `(int)CarePhase.Start/Stop` 或删除;③ `NoteCareAction` 门改为与枚举比较(`:154`);④ `DiseaseCourseBook.cs:107,114` 参数注释;⑤ 测试 4 处引用(`Tests/EditMode/DiseaseSimulation/f4_wiring_test.cs:385,386,387,414,415`);⑥ 可选:`PayloadCodec.History.cs:232-251` 加解码范围门(照 `PatientOutcome` 的 `:141-146`);⑦ 可选:registry `:2186` 补「见 CarePhase」 |
| ADR-024 A2 | **不触碰** —— 载荷字段仍是 `int`,A2 判的是 **C# struct 字段类型**(`kindgen.py:197-211`) |
| ⚠️ 若把字段类型改成枚举 | **会红**:`kindgen.py` 的 A2 分支是 `if elem in INT_PRIMITIVES: continue / if elem in structs: ... / else fail` —— 枚举类型名既不 ∈ 整数白名单也不 ∈ struct 集 ⇒ 判 fail。**改类型前必须先扩 kindgen**(另立 ADR-024 修订,不在本件范围) |
| 与既有先例一致性 | **与 `PatientOutcome` 完全同构**(枚举住 Contracts + 字段 `int` + registry 写序数 + 解码门);与 `PoiState` 同构(枚举 + 字段 `int`);与 `Polarity` 部分同构(有枚举,但住 `Sim` 作者包而非 Contracts,且无解码门);与 `Method`/`Cause` **不同构**(那两者连枚举都没有) |
| 优点 | 序数**单一 C# 定义**(编译期命名,无魔法数字);可实现 codec 解码门(与 `PatientOutcome` 同法),把「写者用了反序 / 越界」从「运行时靠消费者抛」提前到「解码即拒」;零 codec 风险 |
| 代价 | 新增 `Sim.Contracts` 公共类型面;5–7 个文件联动;若不同步改 registry 散文,**仍是两处真源**(枚举 + registry);与项目既有 `Polarity` 的「不造第二套枚举」取向有张力 |

## ⑤ 两个额外问题

**Q1(若选甲案)`DiseaseCourse.NoteCareAction` 的值域门保留还是改成引用 entities 生成物?**

答:**目前不存在可引用的 entities 生成物**。`tools/kindgen/kindgen.py` 的产物只有
`Sim/StreamRouting.g.cs`(Kind→Stream 路由,`:25` 可见),A1–A5 全是断言,不产出任何枚举/序数常量。
⇒ 只有两个选项,二者都不是「引用生成物」:
- (a) **保留硬编码 0/1 门**,把错误串与 remarks 改为引用 `entities.yaml:2186`(成本 ≈ 0);
- (b) 扩 kindgen 让它从 `payload_schema` 抽取序数并生成常量(**新机制**,需要自己的断言与探针轮,
  且散文解析本身脆弱 —— 与 ADR-024「禁从散文解析」的精神相抵,`entities.yaml` 的
  `stream:` 字段正是为此才从散文升成枚举的)。

**Q2(若选乙案)`CareAppliedPayload.Phase` 的类型要不要从 `int` 改成枚举?**

答:**不改,保持 `int`**。三条理由:
1. **ADR-024 A2 + kindgen 会红**(见上,`kindgen.py:197-211`);
2. **先例**:`PatientOutcome` 是 `Sim.Contracts` 里的具名枚举,载荷字段仍是 `int Outcome`(`HistoryPayloads.cs:91`);
   `PoiState` 同理(`WorldPayloads.cs:164` `int NewState`);
3. codec 的整数域形状(`PayloadCodec.History.cs:222-251` 写 `WriteFieldInt32`)不需要枚举类型。

⇒ 乙案的正确形态 = **枚举只做常量来源 + 门,载荷形状一字不改**。

## ⑥ 没查清 / 未做

- **未实跑 kindgen**,「枚举类型名 ⇒ A2 fail」是从 `kindgen.py:197-211` 的判据读出来的**推断**,不是观测。
- 未读 `PrescriptionPolarity` 枚举体(`PrescribeFlow.cs:29` 之后),`Symptomatic=0/Causal=1` 取自
  `ProgressionEvaluator.cs:77-85` 的注释转述。
- 未读 `R1_CHECK_31` 的校验体(只读了 `DiseaseKind.Unspecified` 的 doc 注释提到它)。
- `PayloadCodec.History.cs:232-251` 只看了 grep 上下文,**未逐行确认** `DecodeCareApplied` 无 phase 范围门
  (从可见行看:tag switch → `RequireCompleteMask` → 直接构造,未见范围检查)。
- 未核 ADR-021 正文中「具体值归 6 的 GDD」的精确行号(只引了 `entities.yaml:2124` 的转述)。
- 未核 `clinic-machine.md` 是否在别处定义了「护理动作表」本身(只按 `护理|照护|CareApplied` 三词 grep)。
