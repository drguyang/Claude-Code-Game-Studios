# Architecture Review Report — 2026-09-15

> **Mode**: `/architecture-review`(full)
> **Engine**: Unity 6.3 LTS (URP) · C# · PC (Steam) 优先 · 1–4 人联机 P0 起架构预留
> **GDDs Reviewed**: 7 主体 GDD(`case-system` · `diagnosis-system` · `disease-simulation` ·
> `item-database` · `random-events` · `skill-system` · `game-concept`)+ `systems-index` + `concept-benchmark`
> **ADRs Reviewed**: 4(`adr-005` · `adr-006` · `adr-007` · `adr-008`,**全部 Accepted**)
> **Engine Reference**: `docs/engine-reference/unity/`(VERSION · breaking-changes · deprecated-apis,Last verified 2026-02-13)
> **Engine Specialist**: `unity-specialist`(Phase 5 二次意见已并入 §5)
> **需求基线**: 148 条 TR(首次建立,TR 注册表此前为空)

---

## 判定:**FAIL**

不是「有缺口」——是**四份 Accepted 的 ADR 合起来编译不过,且 25 / 31 个 P0 系统零架构覆盖**。

| 维度 | 结果 |
|------|------|
| 需求覆盖 | ✅ 49 / ⚠️ 19 / ❌ 80(共 148) |
| P0 系统架构覆盖 | ✅ 2 · ⚠️ 9 · ❌ 20(共 31) |
| 跨 ADR 冲突 | **10**(其中 2 条为编译级阻塞) |
| ADR 依赖环 | 无(拓扑序 005 → 006 → 007 → 008) |
| 引擎兼容 | 4 / 4 ADR 带 Engine Compatibility 段;零弃用 API;但 **7 项 post-cutoff 假设待 spike** |
| 预门控清单 | **5 / 5 ❌** |

**为什么是 FAIL 而不是 CONCERNS**:判定 FAIL 的门槛是「Foundation/Core 层需求未覆盖」或「阻塞性跨 ADR 冲突」。两条**同时**成立 —— 见 §4 的 C-1(编译不过)与 §9 的 B-6(世界状态事件化边界未裁决,直接卡住 ADR-002/003 的第三方工具选型)。

---

## 1. 可追溯性摘要

完整的 148 行矩阵见 `docs/architecture/traceability-index.md`;TR-ID 已落 `docs/architecture/tr-registry.yaml`。

| GDD | 系统 | TR 数 | ✅ | ⚠️ | ❌ |
|-----|------|-------|---|----|----|
| `case-system.md` | 37 病例系统 | 27 | 17 | 4 | 6 |
| `item-database.md` | 21a 物品与配方 | 32 | 7 | 3 | 22 |
| `random-events.md` | 52 随机事件导演 | 31 | 8 | 1 | 22 |
| `disease-simulation.md` | 9 疾病与伤情 | 22 | 11 | 6 | 5 |
| `diagnosis-system.md` | 8 诊断与体征 | 20 | 4 | 4 | 12 |
| `skill-system.md` | 30 技能与熟练度 | 8 | 0 | 0 | 8 |
| `game-concept.md` | 全案 | 8 | 2 | 1 | 5 |
| **合计** | | **148** | **49** | **19** | **80** |

> **口径说明**:80 条 ❌ 里有相当一部分是 GDD 内部的设计参数与 schema 形状(不需要 ADR 裁决)。
> 但它们**没有别处可登记**,故全部入表并全部计入基线 —— 宁可多记,不可漏记。
> 真正需要新 ADR 的是其中 **Foundation / Core 层**的那一批,见 §10。

### 📌 已知易冲突区(`docs/consistency-failures.md`)

该文件**不存在**。本项目尚无一致性失效台账 —— 建议本轮起建立,把下表的 10 条冲突作为首批登记。

---

## 2. 覆盖缺口 —— 31 个 P0 系统的架构覆盖

| # | 系统 | 类别 | GDD | 架构覆盖 |
|---|------|------|-----|---------|
| 1 | 玩家控制器与移动 | Core | ❌ | ❌ 零 |
| 2 | 摄像机与视角 | Core | ❌ | ❌ 零 |
| 3 | 输入与设备 | Core | ❌ | ❌ 零 |
| 4 | 交互系统 | Core | ❌ | ❌ 零 |
| 5 | 时间与天气 | Core | ❌ | ⚠️ 仅 `ITickProvider` 被点名 |
| 6 | 世界与生态区 | Core | ❌ | ❌ 零 |
| 7a | 持久化服务 | Core | ❌(契约寄生在 9 的 GDD 内) | ⚠️ 四份 ADR 均向它派活,无 ADR 拥有它 |
| 7b | 存档位 UI | Presentation | ❌ | ❌ 零 |
| 8 | 诊断与体征揭示 | Core | ✅ | ⚠️ 仅「唯一浮点出口」+ 铁律④;8 自身浮点域无 ADR |
| 9 | 疾病与伤情模拟 | Core | ✅ | ✅ ADR-005 / 006 |
| 10 | 急救动作模块 | Core | ❌ | ❌ 零 |
| 11 | 处方用药 | Core | ❌ | ❌ 零 |
| 13 | 病人 AI 与行为 | Feature | ❌ | ❌ 零 |
| 17 | 采集 | Core | ❌ | ❌ 零 |
| 18 | 炮制 | Core | ❌ | ❌ 零 |
| 20 | 库存与物品 | Core | ❌ | ⚠️ item-db Schema E 隐含,无 ADR |
| 21 | 物品与配方数据库 | Foundation | ✅ (21a) | ⚠️ ADR-006 覆盖定点/序列化;6 条契约 NO-ADR |
| 23 | 模块化建造 | Feature | ❌ | ❌ 零 |
| 24 | 医馆即机器 | Feature | ❌ | ❌ 零 |
| 25 | 格斗与武器线 | Core | ❌ | ⚠️ 仅落在 ADR-005 定点域内 |
| 27 | 敌人 AI | Feature | ❌ | ❌ 零 |
| 29 | 死亡与复活 | Feature | ❌ | ❌ 零 |
| 30 | 技能与熟练度 | Foundation | ✅ | ❌ **零** —— 全文零 ADR 引用、零定点纪律 |
| 37 | 病例系统 | Feature | ✅ | ✅ ADR-008 |
| 39 | 脉案 | Presentation | ❌ | ❌ 零 |
| 42 | 拟物 UI 框架 | Foundation | ❌ | ❌ 零 |
| 44 | 音频系统 | Foundation | ❌ | ❌ 零 |
| 48 | 教学与引导 | Presentation | ❌ | ❌ 零 |
| 51 | 遥测与分析 | Foundation | ❌ | ❌ 零 |
| 52 | 随机事件导演 | Feature | ✅ | ⚠️ ADR-007 覆盖 6 / 31 条 TR |
| 53 | 医疗后果与责任 | Feature | ❌ | ⚠️ ADR-008 声明 Blocks/订阅,无自有 ADR |

**计**:✅ 2 · ⚠️ 9 · ❌ 20。
**只有 6 / 31 个 P0 系统有 GDD**;架构覆盖几乎全部集中在「确定性模拟边界」这一条线上,表现层与交互层**完全空白**。

---

## 3. (保留位)Phase 3b — RTM

未运行。`rtm` 模式需要 `production/epics/` 与 `tests/` —— 二者**均不存在**。
待 P0 进入 Production 后以 `/architecture-review rtm` 补全 GDD → ADR → Story → Test 全链。

---

## 4. 跨 ADR 冲突

### 🔴 C-1 · `PatientId` 既是静态类又是值类型 —— **编译级阻塞**

| | |
|---|---|
| **类型** | Integration contract conflict(ADR-005/006/008 vs ADR-007) |
| **ADR-007 声称** | `:242` `public static class PatientId { public const int None = -1; }` |
| **ADR-005/006/008 声称** | `:187` `PatientId Next();` · `:188` `VitalsDto GetVitals(PatientId p);` · `:194` `public readonly PatientId Patient;` · ADR-008 `:219-224` 载荷字段 `PatientId patient_id` |
| **影响** | CS0101(同一命名空间内重复定义)。`static class` 不能作字段类型、不能作返回类型、不能 `default()`。ADR-007 自己在 `:179` 写「`PatientId` 的默认值很可能是 `0`」—— 那句只有把它当 struct 才成立,**ADR-007 内部就自相矛盾**。四份 ADR 一字不改则**一行代码都编译不过** |
| **修法** | 单一定义:`public readonly struct PatientId { public readonly int Value; public static readonly PatientId None = new PatientId(-1); }`,四处引用全部保持原样 |
| **归属** | ADR-007 就地修正(与 ADR-005/006/008 不动) |

### 🔴 C-2 · `anchor` —— 同名、异义、异型,且被做了不可能的类型运算 ✅ **已就地修正**

| | |
|---|---|
| **类型** | Integration contract conflict + 命名冲突 |
| **52 侧** | `random-events.md:143` `anchor` = **enum** `{CLINIC_FRONT, TRAVEL_PATH, GATHER_POINT, SETTLEMENT, BIOME_REGION}` —— **事件在哪生成**;`:437-440` 有确定性坐标解析规则;`AC-52-42` 构建期校验枚举成员 |
| **ADR-007 侧** | `:160` `EventArrived Payload = { event_key, tier, anchor, cause_clue_key? }`,**未给类型** |
| **ADR-008 侧** | `:114-118` / `:219-224` `anchor` 类型写作 **`long`**,措辞为「**不透明** anchor(立案上下文 —— 13 无 GDD,故不定义内部形状)」 |
| **硬矛盾** | ADR-008 `:132` 与 `case-system.md:345` 写 `c.anchor.Tick ∈ [e.Tick, c.CloseTick]` —— 对一个「不透明 `long`」取成员 `.Tick`,既不可能是 `long` 的成员,也不可能是 52 的 enum。**这条式子无法实现** |
| **影响** | `EventPayload` 是联合类型:同一个字段名要承载两个不相容的类型与语义。实现者无法定型;`.Tick` 无法落地;ADR-008 `:290` 的缓解措施(「anchor 定义为不透明整数」)与 `:132` 的算术**互相打架** |
| **修法(二选一)** | **(a) 改名分离** —— 52 的 `anchor` → `spawn_anchor`(enum),37 的 → `case_anchor`,并让 ADR-008 明确其内部形状;<br>**(b) 降维** —— 若 37 真正需要的只是「立案那一刻的 tick」,把 `anchor: long` 换成 `opened_tick: long`,顺带消掉「不透明」这个诱导性措辞,`:132` 改为 `c.opened_tick ∈ [e.Tick, c.CloseTick]` |
| **倾向** | **(b)**。理由:「不透明」的动机是「13 无 GDD,不阻塞」,但代价是引入一个**没有定义、没有消费者、却参与类型运算**的字段。37 的可结案判定需要的确实是 tick,不是「立案上下文」这个含糊物 |

### 🟠 C-3 · ADR-005 保留了已被 GDD 撤销的误差预算

| | |
|---|---|
| **类型** | 数值契约冲突(两层文档口径断裂) |
| **ADR-005 `:105-106`** | 「保守带按 `ops × 2⁻¹⁶` 外扩(**实测 ≤ 3×2⁻¹⁶**),可并入 σ 的安全裕量」 |
| **`disease-simulation.md:570`** | 「**⚠️「实测 ≤ 3×2⁻¹⁶」为无出处自引,已撤销** —— 待 `src/` 实现后标定(否则保守带可能窄于真实误差 → **静默漏事件**)」 |
| **影响** | ADR 是权威层,GDD **无权单方面撤销** ADR 里的数字。当前两层文档给出不同口径,而实现者大概率读 ADR。数值风险是真实的:手写 Q16.16 `Exp` 的最坏误差 = 范围规约 + minimax 逼近(约 1–2 ulp)+ 落回 Q16.16 截断(≤0.5 ulp),单次调用 2–3 ulp 属正常;F1 每 tick 求值 2–3 次。若保守带窄于真实误差,**扫描边界会错过真实穿界点 → 状态转移漏判 → 逐位仍一致但语义已错**(与本项目反复警惕的静默失败同类) |
| **修法** | ADR-005 挂修正案:删除「实测 ≤ 3×2⁻¹⁶」,改为「带宽待 `Exp` 黄金文件对拍标定,取 `max(2 × max_err, ops × 2⁻¹⁶)`,常数落 `entities.yaml`」;并补 **单向性验证**(对 `boundary_mode: scan` 的病种,断言扫描返回的边界**含**真实穿界点 —— 这比总误差上界更关键) |
| **归属** | ADR-005 修正案 |

### 🟠 C-4 · `Seq` 的发放语义被 ADR-008 静默改写,未回改 ADR-006

| | |
|---|---|
| **类型** | Integration contract conflict |
| **ADR-006 Amendment A** | `Seq` 由 `IIdAuthority` 统一发放;全序键 `(Tick, Patient, Seq)` |
| **ADR-008 `:94`** | 「两条流共享同一 `Seq` 发放器(计数器从 0 起,**按流分别单调**)」 |
| **ADR-008 `:106`** | 「`Seq` 只在同一 `(Tick, Patient)` 内单调(ADR-006 Amendment A)」 |
| **ADR-008 `:103`** | 排序键 `(Tick, 流优先级, Patient, Seq)` |
| **矛盾** | 二者只能取一:若 `Seq` **按流分别单调**,则两流之间 `Seq` 不可比,`(Tick, Patient, Seq)` 前缀在跨流时**不是全序** —— 必须靠流优先级,而这与 ADR-006 的键定义不符;若 `Seq` **按 `(Tick, Patient)` 全局单调**,则流优先级项**恒为冗余**(同 `(Tick, Patient)` 的 `Seq` 已唯一)。无论取哪个,都要回改 ADR-006 Amendment A |
| **附加** | ADR-008 `:94` 说「两条流共享同一发放器」又说「按流分别单调」,这本身是两个独立单调序列 —— 与「不新造计数器」的声明张力未解 |
| **修法** | 在 ADR-006 挂 Amendment C,把 `Seq` 的发放域**明确定义为 `(StreamId, Tick, Patient)` 内单调**,并把全序键升格为 `(Tick, StreamPriority, Patient, Seq)`;或在 ADR-008 声明「流优先级在 `Seq` 全局单调时为冗余项,保留以便将来解耦」 |

### 🟠 C-5 · 「病史事件流 = 唯一真源」被两条旁路放宽,但 ADR-005 正文未改

| | |
|---|---|
| **类型** | State ownership / 文档完整性 |
| **ADR-005 `:123`** | 「**病史事件流 = 唯一真源**」(单流口径) |
| **旁路 1** | ADR-007 §二 `WorldSeed` 住**存档头**(流之外)。ADR-007 自己论证了它是**参数**不是状态 ⇒ **不冲突**,可接受 |
| **旁路 2** | ADR-008 §一 引入**第二条逻辑流** ⇒ 真源变为「两条流的并集」。ADR-005 §三与 §Implementation Guidelines 5 仍是单流口径,未回改 |
| **影响** | 新实现者读 ADR-005(Foundation 权威件)会得出「只有一条流」「折叠可作用于一切」的错误结论。ADR-008 §六 的**病例流折叠豁免**与 ADR-005 `:207` 的折叠规则直接对立 —— 该对立已由 ADR-008 裁决,但**未回写到 ADR-005** |
| **修法** | ADR-005 挂修正案(或就地)把 `:123` 改为「**事件流(病史流 + 病例流)是唯一真源**」,并在 `:207` 折叠规则处补「病例流不折叠(ADR-008 §六)」 |

### 🟡 C-6 · ADR-005 §Key Interfaces 是过期形状,且与自己的注释自相矛盾

| | |
|---|---|
| **ADR-005 `:190` 注释** | 「── 事件必须带逻辑 tick 且**可全序** ──」 |
| **ADR-005 `:193-198` 代码块** | `SimEvent { Tick, Patient, Kind }` —— **没有 `Seq`,「可全序」无从谈起** |
| **前向指针** | `:331-334` 存在且措辞正确(「本 ADR 的 `SimEvent` 定义以 ADR-006 为准」) |
| **影响** | §Key Interfaces 是**最高的逐字复制源**。实现者抄代码块、看不见三页之后的指针。ADR-007 追加的 `IEventAuthority`(第六抽象点)在 ADR-005 `:181` 的「五个抽象点」里也没有踪迹 |
| **修法** | 就地替换为 ADR-006 Amendment A 的 `{ Tick, Patient, Seq, Kind, Payload }`,保留前向指针;`:181` 注记「五个 + 第六 `IEventAuthority`(ADR-007 §一)」 |

### 🟡 C-7 · ADR-005 保留了已被 ADR-006 判定为**假**的序列化断言

| | |
|---|---|
| **ADR-005 `:176`** | `// ── 定点标量:无行为纯 struct,序列化只存内部 long ──` |
| **ADR-006 §五 `:165-167`** | 「原稿 `:124` 断言『`Fix` 序列化为内部 `long`』,这个断言**不成立** —— 它把「内部表示是 `long`」误当成了「Unity 会序列化该 `long`」」 |
| **影响** | 同上,`§Key Interfaces` 是最高的复制源。失败模式是**静默的**(药物效力被清零,不崩溃、不回放失配)。ADR-006 已纠正,但 ADR-005 的注释原样保留 |
| **修法** | 就地删掉该注释行,改为 `// 序列化必须走自定义编码器 —— 见 ADR-006 §五(内置序列化器会静默归零)` |

### 🟡 C-8 · ADR-006 出现两个连续 `### Negative` 标题

`:290` 是空标题,`:292` 是同样名字的有内容标题。应为编辑残留。影响为文档结构(生成目录/锚点重复),无实质风险。**修法**:删除 `:290`。

### 🟡 C-9 · ADR-006 §Neutral 的「五个抽象点」在 ADR-007 后已过期

ADR-006 `:303-305` 写「不改变**五个抽象点**的数量与职责」。ADR-007 §一 正式追加第六个 `IEventAuthority`。这是**时序性陈旧**(ADR-006 早于 ADR-007),不构成冲突,但同一数字在四份 ADR 里出现了三种说法(5 / 6)。**修法**:ADR-006 §Neutral 补一行「第六抽象点见 ADR-007 §一,本 ADR 未涉及」。

### 🟠 C-10 · ADR-001 带着两条 Accepted ADR 的约束却不存在

| | |
|---|---|
| **ADR-007 `:363`** | 给 ADR-001 追加约束:选型须支持自定义可靠有序消息流 |
| **ADR-008 `:305`** | 再追加:选型须承载**两条**逻辑流 |
| **`technical-preferences.md`** | ADR-001 仍标「**[ADR-001 待建]**」 |
| **影响** | 这不是文档债 —— 是**排期债**。P0 要求「联机架构从 P0 起预留」,而**预留什么、按什么接口预留**由 ADR-001 决定。ADR-001 不落盘,7a 与 45 的接口形状就没有第二份约束来源,四份 ADR 的「可选跳过」降级路径也无处挂 |
| **修法** | 立 ADR-001(见 §7 R-3) |

### 冲突汇总

| # | 严重度 | 类型 | 落点 |
|---|--------|------|------|
| C-1 | 🔴 编译级 | Integration | ADR-007 就地 |
| C-2 | 🔴 不可实现 | Integration + 命名 | ADR-008 + `random-events.md` + `case-system.md` |
| C-3 | 🟠 静默数值错 | 数值契约 | ADR-005 修正案 |
| C-4 | 🟠 全序歧义 | Integration | ADR-006 修正案 |
| C-5 | 🟠 权威件口径 | State ownership | ADR-005 就地 |
| C-6 | 🟡 复制源过期 | 文档完整性 | ADR-005 就地 |
| C-7 | 🟡 复制源含假断言 | 文档完整性 | ADR-005 就地 |
| C-8 | 🟡 结构残留 | 文档缺陷 | ADR-006 就地 |
| C-9 | 🟡 时序陈旧 | 文档完整性 | ADR-006 就地 |
| C-10 | 🟠 排期债 | 缺失依赖 | 立 ADR-001 |

---

## 5. ADR 依赖顺序

```
Depends On 图(全部 Accepted,零未决依赖,零环):

  ADR-005  确定性模拟与状态同步模型        ← Foundation,无依赖
     │
     ├──► ADR-006  定点域边界数据契约
     │        │
     │        └──► ADR-007  事件权威与掷骰状态
     │                 │
     │                 └──► ADR-008  病例事件流
```

**推荐实现顺序**(与拓扑序一致):

| 序 | ADR | 状态 | 说明 |
|----|-----|------|------|
| 1 | ADR-005 | Accepted | `Fix` + SplitMix64 + **定点 `Exp`** + 单元测试 ⇒ **先于任何病种** |
| 2 | ADR-006 | Accepted | `FixParse` + 单一舍入 + 自定义编码器;Amendment A/B 已回填 |
| 3 | ADR-007 | Accepted | `IEventAuthority` 本地占位 + `WorldSeed` 存档头 |
| 4 | ADR-008 | Accepted | `IEventSink` 按 Kind 路由 + 病例流不折叠 |

**未决依赖**:无(四份全为 Accepted)。
**环**:无。

> ⚠️ **但「无未决依赖」是假象** —— 依赖图里**缺了 5 个被反复引用的节点**:
> `ADR-001`(联机,被 C-10)、`ADR-002`(地形)、`ADR-003`(建造)、`ADR-004`(DOTS)、
> **7a 持久化(无 GDD 且无 ADR)**。四份 ADR 都把义务派给 7a,而 7a 没有任何权威件 ——
> §7 的 R-1 / R-4 即为此。

---

## 6. 引擎兼容性审计

### 6.1 审计结果

| 项 | 结果 |
|----|------|
| Engine Compatibility 段齐备 | ✅ **4 / 4** |
| 引擎版本口径一致 | ✅ 4 / 4 均声明 Unity 6.3 LTS |
| `Post-Cutoff APIs Used` | ✅ 4 / 4 均声明 **None** |
| 弃用 API 引用(`deprecated-apis.md` 名单比对) | ✅ **0 处** |
| 陈旧版本引用 | ✅ 无 |
| 知识风险分级 | ⚠️ ADR-005 **HIGH** · ADR-006 MEDIUM · ADR-007 **LOW** · ADR-008 HIGH —— 其中 ADR-005 与 ADR-008 的 HIGH 是**同一件事**(IL2CPP 逐位性),ADR-007 无此暴露故为 LOW,分级可辩护 |

### 6.2 🔴 E-1 · ADR-005 的「零 `UnityEngine` 依赖」判据**不完备**(Unity 专家确认)

| | |
|---|---|
| **ADR-005 `:84` / `:306`** | 「模拟层**零 `UnityEngine` 依赖**(**编译期可验证**)」 |
| **第 1 个漏洞** | asmdef **不设 `"noEngineReferences": true`** 时,Unity 会**隐式注入** `UnityEngine.CoreModule` 引用 —— 「零引用」**静默假通过**。`disease-simulation.md:875` 已指出,但 ADR-005 的判据行未含 |
| **第 2 个漏洞(更隐蔽)** | `System.Math.Exp/Pow` 在 **`System.Runtime`**,不在 `UnityEngine` —— **程序集隔离天然拦不到**。`Unity.Mathematics.math.exp/pow` 同样不在 `UnityEngine`,且带 SIMD 近似、跨后端不逐位。`Math.Abs(long.MinValue)` 也在此列(会抛异常) |
| **正确机制(两道互补门)** | **门 A**:sim asmdef 设 `"noEngineReferences": true`,**且** sim 程序集必须是独立 asmdef(落 `Assembly-CSharp` 则隔离直接失效);<br>**门 B(真正的守门)**:一条 **EditMode 测试**用 `System.Reflection` 加载 sim 程序集,断言 ① `GetReferencedAssemblies()` 无 `UnityEngine.*` / `Unity.Mathematics` / `Unity.Collections`;② 扫全部方法体 IL,无对 `Math.Exp/Pow/Sqrt/Log/Abs`、`MathF` 的 `call/callvirt`;③ sim 类型图上无 `float`/`double` 字段与局部签名 |
| **更早的拦截** | Roslyn analyzer(编译期直接报错)。Unity 6 支持自定义 analyzer,但**确切注册形态属 post-cutoff,须 spike** —— 先用零依赖的门 B 垫底 |
| **修法** | ADR-005 判据行就地补「asmdef 须 `noEngineReferences: true` + 补 `System.Math` 盲区 + 门 B 的 IL 扫描」 |

### 6.3 🔴 E-2 · `System.Int128` **在玩家构建里不存在** —— `disease-simulation.md` §F0 出错(Unity 专家确认)

| | |
|---|---|
| **`disease-simulation.md:543-548`** | 「`Fix` 只用 `long` 存 Q16.16,做乘法时先提升到更大的中间类型(如 **`System.Int128`** 或手工 hi/lo 拆分)」 |
| **裁决** | `System.Int128` 是 **.NET 7 / C# 11** 类型,**不在 netstandard2.1 BCL**。sim 代码必须能进玩家构建 ⇒ **此路不通**。ADR-005 的「中间乘法落 Q32.32」也未钉中间类型,同一盲区 |
| **替代** | ① `Math.BigMul(long, long, out long high)`(**netstandard2.1 有该重载**,给出 128 位乘积 hi:lo)—— 但 Unity 具体 API Compatibility Level 曾有个别缺失史,须一条冒烟测试;<br>② **手写 64×64→128**(32 位部分积,约 10 行)——**不依赖任何 API,推荐** |
| **顺带** | GDD 那行「高精度中间需 ~96 位量级」是**过度设计**:若注册表值域校验把 raw 约束在 \|raw\| ≤ 2³¹,则积 ≤ 2⁶²,`long` 就够。**值域校验 + 128 位中间双保险**即可 |
| **修法** | `disease-simulation.md` §F0 就地替换 `System.Int128`;ADR-005 / ADR-006 补明中间类型 |

### 6.4 🔴 E-3 · FMA 禁令的缓解措施**无法执行** —— `diagnosis-system.md` G-4 需改措辞(Unity 专家确认)

| | |
|---|---|
| **`diagnosis-system.md:1049` G-4②** | 「凡 `a*x+b` 形状(含 `C_neg` 的线性项与 `READ_FLOOR` 的插值)**一律走「预计算定表」或显式 `Mul` 后 `Add`**,禁止编译器自行收缩」 |
| **裁决** | 「**显式 `Mul` 后 `Add`**」**没有任何执行力**。源码写成两条语句,编译产物仍是「先乘后加」两条 IL;IL2CPP 转译成 C++ 后,**平台编译器照样可以融合成单条 FMA**。语言层没有任何表达能阻止下游收缩。**留着这条指令,实现者会以为自己做了防护** |
| **可执行机制** | **机制 A(唯一跨后端可靠):消灭 `a*x+b` 形状** —— G-4 自己列的「预计算定表」才是正道。定表在加载期算好,运行期只有查表 + 单次运算,FMA 无从发生。`C_neg` 线性项与 `READ_FLOOR` 插值**都能定表化**;<br>**机制 B(配合 AC-8-F5)** 用 `PlayerSettings.SetAdditionalIl2CppArgs("--compiler-flags=...")` 钉死融合策略(Clang/GCC `-ffp-contract=off`、MSVC `/fp:precise`)。**局限**:只覆盖 IL2CPP 构建,且是「让平台们统一到不融合」的**构建纪律** —— 改一条配置就悄悄失效。**B 不可替代 A** |
| **有利条件** | Steam 全平台(Win/Mac/Linux)均可 IL2CPP ⇒ 可做到「生产构建全 IL2CPP + 统一编译旗标」,Mono 只留 Editor/测试。AC-8-F5 的可行性从「困难」降为「可控」 |
| **修法** | `diagnosis-system.md` G-4 撤掉「显式 `Mul` 后 `Add`」,改为「**定表化，禁 `a*x+b` 形状**」;机制 B 作为构建纪律另记 |

### 6.5 🟠 E-4 · IL2CPP 逐位性实测 —— 可行,机制明确,应排为 P0 spike

ADR-005 `:37` 把「IL2CPP 逐位性实测」列为 Verification Required 且**至今无实测记录**,
而多条 AC 阻塞其上:`disease-simulation` AC-1 / AC-5b · `item-database` AC-21a-29 ·
`diagnosis-system` AC-8-F5。**专家裁决:测量在 Unity 6.3 上完全可行,且 Fix 整数核心的风险
比 ADR-005 标注的 HIGH 低得多。**

| 层 | 内容 | 风险 |
|----|------|------|
| **层 1 — `Fix` 整数核心** | `long` 四则 / 移位 / `unchecked` 环绕:SplitMix64、Q16.16 乘法(128 位中间)全是纯整数,IL2CPP 转 C++ 后在 MSVC / GCC / Clang 上语义一致;Mono JIT 与 IL2CPP 一致 | **低**。仅两处需钉死:① **有符号右移**(C# 保证算术移位;C++ implementation-defined,实际全平台一致)⇒ 补**负值右移黄金测试**;② 整数除法向零截断与 `ROUND_HALF_AWAY_FROM_ZERO` 在**负值**上的实现(见 E-8)。**AC-1 / AC-5b / AC-21a-29 这类整数逐位 AC,spike 一天就能绿** |
| **层 2 — 表现层 float(AC-8-F5 真正关心的)** | **Mono vs IL2CPP**:`Math.Exp/Pow` 在 Mono 走内嵌 libm,IL2CPP 走平台 C 运行时 —— glibc / MSVC CRT / macOS libm / newlib **末位互不保证**。**FMA**:C# 层 Roslyn 不产 FMA,但 IL2CPP 生成的 C++ 会被平台编译器重新优化融合;**clang/gcc 默认 `-ffp-contract=fast`(融合),MSVC 默认 `/fp:precise`(不融合)** —— Steam 矩阵正好踩中「Windows(MSVC)vs Linux/Mac(Clang)」这对分叉组合 | **这才是 HIGH 的来源** |

**Spike 定义(1–2 人日,排进 P0,先于 9 的实现收尾)**:
写 `Fix` + SplitMix64 + **黄金哈希夹具**(把 raw 校验和写文件);用已批准的
`game-ci/unity-test-runner@v4` 建矩阵 **Linux-x64-IL2CPP + Linux-x64-Mono +
Linux-ARM64-IL2CPP + Windows-x64-IL2CPP** —— 覆盖「后端(Mono/IL2CPP)× ISA(x86-64/ARM64)×
C++ 编译器(clang/MSVC)」的完整兴趣空间,**无需 Apple 硬件**。
结果落成 **ADR-005 修正案**或 `docs/engine-reference/unity/` 实测记录,
并反过来改写 ADR-005 `:37` 的 Verification Required 与风险表。

### 6.6 🟠 E-5 · 7 项 post-cutoff 假设待 spike

| # | 假设 | 出处 | 判断 | 验证手段 |
|---|------|------|------|---------|
| 1 | `System.Int128` 可用 | `disease-simulation.md:545` | ❌ **不符** | 换 hi/lo(见 E-2) |
| 2 | `Math.BigMul(long,long,out long)` 在玩家构建可用 | (E-2 的替代) | ⚠️ netstandard2.1 有此重载,Unity profile 曾有个别缺失史 | 手写绕开,或一条 EditMode 冒烟 |
| 3 | `System.Math.Exp` 跨平台不逐位 | ADR-005 `:37/:222` | ✅ **基本属实** | E-4 的矩阵实测 |
| 4 | `-ffp-contract` 可控 | ADR-005 `:37` | ⚠️ **机制存在**(`SetAdditionalIl2CppArgs --compiler-flags=`),逐目标语法/生效形态属 post-cutoff | spike:两平台各打一包反编译验证无 FMA |
| 5 | Unity 6.3 序列化器对 `readonly struct` 私有字段「跳过/归零」 | ADR-006 §五 | ✅ **方向正确**(三种情形都成立),唯 `readonly` 的确切处理未知 | 照 `AC-21a-53` 的 EditMode 探针,记录为**观察事实** |
| 6 | UI Toolkit 运行时手柄焦点导航可用 | (缺失 ADR ⑤) | ⚠️ 半可信,质量未定 | P0 早期一个两天焦点导航原型 + `NavigationMoveEvent` spike |
| 7 | Roslyn analyzer 在 Unity 6.3 的注册形态 | (E-1 的门 B) | ⚠️ post-cutoff | spike;门 B 先用 IL 扫描 EditMode 测试垫底 |

### 6.7 引擎专家补充的、审计可能遗漏的反模式

| # | 反模式 | 说明 |
|---|--------|------|
| E-6 | **`string.GetHashCode()` / `HashCode.Combine` 进持久化路径** | .NET 字符串哈希**按进程随机化**,Unity/IL2CPP 不保证稳定。纪律须从「禁 `Random.Range`」扩为「**任何 `GetHashCode` 输出不得落盘、不得入事件流、不得参与全序**」,并加 grep 守门 |
| E-7 | **`ITickProvider` 建立在 `Time.fixedDeltaTime` / `Time.fixedTime` 上** | 二者受 `Time.timeScale`、暂停、机器间墙钟漂移影响 —— **暂停即停 tick;两台机器 `fixedDeltaTime` 不一致 ⇒ tick 计数漂移**。DC-3 禁了 `Time.time` / `Time.deltaTime`,但**这是更隐蔽的陷阱**。正确形态:自有纯计数的游戏步进,与 `FixedUpdate` 松耦合;`TICK_SECONDS` 作为纯数据常量全案同值 |
| E-8 | **Q16.16 负值除法/舍入方向未钉死** | C# `/` 向零截断;「向下保守舍入」在负值上若直接用 `/` 会得到**向零**而非向下 ⇒ **保守带在负 Offset 上方向反掉**(`axis_offset_by_quality[]` 可为负,见 `AC-21a-53`)。Fix 规范必须显式定义负数除法方向(建议 `FloorDiv` = 截断后修正) |
| E-9 | **`CultureInfo` 依赖的 `Parse` / `ToString`** | 默认走当前文化。`FixParse` 必须显式 `CultureInfo.InvariantCulture`(`FromRatio("1/2")` 的字串切分与异常消息涉及文化面) |
| E-10 | **`checked` / `unchecked` 双轨纪律缺失** | C# 默认 `unchecked`(环绕)。建议:**sim 程序集发布路径保持 unchecked**(IL2CPP 环绕跨平台一致),**测试程序集用 `csc.rsp -checked+`** 把溢出 bug 在 EditMode 抓出来,并写进 AC |
| E-11 | **`UnityEngine.Random` 禁令的作用域要写清楚** | 禁令**只应扫 sim / 核心程序集**;表现层(粒子、音效 pitch、NavMesh 随机点)用 `UnityEngine.Random` 是正常的,全项目禁会误伤。落成「门 B 只扫 sim 程序集」 |
| E-12 | **`SimEvent.Kind` 的持久化形态未定** | D-21-13 已钉死 `processing_state` 用稳定字符串名,但 `SimEvent.Kind` **是枚举**:codec 必须用**带版本映射的稳定整数或字符串**,禁直接落枚举序号(插值改枚举即静默重映射)。归 7a ADR |
| E-13 | **Addressables 6.2+ 失败抛异常而非返 null** | `docs/engine-reference/unity/breaking-changes.md:71-89`。21a / 7a 的 JSON 数据加载必须 `try/catch` + `handle.Valid` 检查,加载失败要能落到**构建期/启动期硬失败**而非运行时 null 解引用 |
| E-14 | **存档原子写** | 7a 禁用 `PlayerPrefs` 承载事件流(平台实现差异 + 无原子性);用 `Application.persistentDataPath` + 临时文件改名原子写 + 损坏恢复。Steam Cloud Saves 是否启用需另决 |
| E-15 | **`Physics.defaultSolverIterations`(6.0+ 由 6 改 8)与 PhysX 5.1 非确定性** | 只影响表现层(击退、布娃娃),**不得参与事件流**。25 的 `inflicts_injury` 语义事件已是正确隔离,保持 |
| E-16 | **UI Toolkit 无原生 world-space / XR 支持** | VR 急救模式与任何世界空间 UI 须走 UGUI world canvas 或网格 ⇒ **同一 UI 两套栈要早决**(见 §7 R-6) |
| E-17 | **`Fix.ToFloat()` 本身是确定性的** | Q16.16 → `float` 是硬件转换(就近舍入),跨平台逐位一致 —— 可写进 ADR-005 的表现层出口注记,免得将来有人担心出口本身 |

### Engine Specialist Findings(小结)

unity-specialist 对八项审计发现**全部确认为真实问题**,并把严重度分成两档:

- **必须就地改文**:C-6(陈旧代码块)· E-1(判据不完备)· C-3(误差预算自引)· **E-2(`Int128` 不可用)** · **E-3(FMA 缓解无效)**
- **需排期实测而非改文**:E-4(IL2CPP spike,1–2 人日,P0)· E-5#5(序列化探针照 `AC-21a-53`)· 定点 `Exp` 黄金文件夹具

**最大缺口不在四份 ADR 之内** —— 见 §7 R-1。

---

## 7. GDD 修订标记(Architecture → Design Feedback)

> 这些 GDD 的假设与已验证的引擎行为或 Accepted ADR 冲突。**该 GDD 的系统进入实现之前须修订。**

| GDD | 假设 | 现实(来自 ADR / 引擎实测) | 动作 |
|-----|------|---------------------------|------|
| `skill-system.md` | 无 —— 但**全文零 ADR 引用、零定点纪律**;`XP_to_next(n) = C × n^P`(P = **1.4**)直接用浮点分数次幂 | ADR-005 要求全案模拟数学在整数定点域;`diagnosis-system.md` G-1 明令「**指数只取整数或 1/2**」(libm `pow` 跨平台末位不一致)。**`P = 1.4` 与 G-1 直接冲突** | **Needs Revision** —— 补定点纪律一节;`P` 要么定表化,要么改为整数/1/2 指数 |
| `case-system.md` | `c.anchor.Tick ∈ [e.Tick, c.CloseTick]`(`:345`) | ADR-008 把 `anchor` 定义为「**不透明 `long`**」 —— `long` 没有 `.Tick` | ✅ **已修**(`:345` 改为 `c.opened_tick ∈ […]`) |
| `random-events.md` | `anchor` = 生成点枚举(`:143`) | ADR-007 `:160` / ADR-008 `:114` 同名承载另一个语义 | ✅ **已修**(全 16 处改名 `spawn_anchor`) |
| `disease-simulation.md` | §F0 用 `System.Int128` 作中间类型(`:545`) | **netstandard2.1 / IL2CPP 无此类型** —— 玩家构建编译不过 | **Needs Revision** —— 换手写 64×64→128 |
| `disease-simulation.md` | §F0 误差预算沿用 ADR-005 的 `3×2⁻¹⁶` 口径 | GDD 自己已撤销该自引(`:570`),但两层文档仍不一致 | 由 **ADR-005 修正案**收口(见 C-3);GDD 侧已正确 |
| `diagnosis-system.md` | G-4「显式 `Mul` 后 `Add`」防 FMA 收缩(`:1049`) | **语言层无法阻止下游 C++ 编译器融合** —— 该缓解无效 | **Needs Revision** —— 改「定表化,禁 `a*x+b` 形状」 |
| `item-database.md` | 6 条契约标 `⚠️ NO-ADR`(`instance_id` 权威 · Craft 事件总序键 · `ActualConsumed` 持久化 · 可感知地板 · 9 侧字段名对齐 · int64 溢出上界) | ADR-005/006/007 均未钉死 | 待新 ADR(见 §9 B-7) |
| `disease-simulation.md` | 未登记「**病人出现率上限**」配置项 | ADR-008 §六 的病例流有界性论证**硬依赖**它;ADR-008 Status `:13` 已列为残留义务 | **Needs Revision** —— 9 侧补配置项 |

### 系统索引更新(已获批准,Status 字段精确为 `Needs Revision`)

| # | 系统 | 现 Status | 拟改 Status |
|---|------|----------|------------|
| 9 | 疾病与伤情模拟 | `正文全节落盘 · 已复核并大修(...)` | **`Needs Revision`** |
| 30 | 技能与熟练度 ✅ | `已设计` | **`Needs Revision`** |
| 37 | 病例系统 | `正文全节落盘 · In Design(2026-09-15)· ... 待新会话重审` | **`Needs Revision`** |
| 52 | 随机事件导演 | `正文全节落盘 · 首轮 ... 二轮复核 ...` | **`Needs Revision`** |

原 Status 正文已移入「GDD」列保留,不丢失。

---

## 8. 架构文档覆盖(Phase 6)

`docs/architecture/architecture.md` **不存在** ⇒ 本节跳过。

顺带登记的同层缺口:

| 文件 | 状态 |
|------|------|
| `docs/architecture/architecture.md` | ❌ 不存在 |
| `docs/architecture/control-manifest.md` | ❌ 不存在(没有它,Story 无法嵌入架构约束版本) |
| `docs/consistency-failures.md` | ❌ 不存在 |

---

## 9. 阻塞项(必须解决才能改判 PASS)

### 就地修正 —— 文本级,无设计裁决点

| # | 阻塞项 | 落点 | 归属 |
|---|--------|------|------|
| **B-1** | `PatientId` 单一定义为 `readonly struct` + `None` 静态只读哨兵 | ADR-007 `:242` | 就地 |
| **B-2** | `anchor` 拆名/降维 —— 消除同名异型与 `.Tick` 不可能运算 | ADR-008 `:114-132` `:219-224` · `case-system.md:345` · `random-events.md:143` | 就地 + GDD |
| **B-3** | 删 ADR-005 的 `3×2⁻¹⁶` 自引,改为「待 `Exp` 黄金文件对拍标定」+ 补单向性验证 | ADR-005 `:105-106` | 修正案 |
| **B-4** | ADR-005 §Key Interfaces 换为 Amendment A 形状;补第六抽象点;删掉「序列化只存内部 long」假断言 | ADR-005 `:176` `:181` `:193-198` | 就地 |
| **B-5** | `Seq` 发放域明确定义 + 全序键升格 | ADR-006 修正案 C | 修正案 |
| **B-6** | ADR-006 删重复 `### Negative`(`:290`);§Neutral 补第六抽象点注 | ADR-006 | 就地 |

### 本轮已执行的就地修正(2026-09-15)

| # | 项 | 落点 | 状态 |
|---|----|------|------|
| B-1 | `PatientId` 改为 `readonly struct` + `None` 静态只读哨兵 + `IsNone`(消掉 static-class 与字段类型的编译级冲突);并补「`GetHashCode()` 不得落盘」 | ADR-007 §Key Interfaces | ✅ 已改 |
| B-3 | 删掉 ADR-005 的「实测 ≤ 3×2⁻¹⁶」自引,改为「待黄金文件对拍标定 + 落 `entities.yaml`」;并补**边界扫描单向性验证**判据 | ADR-005 §一 | ✅ 已改 |
| B-4 | ADR-005 §Key Interfaces 换为 ADR-006 Amendment A 形状(`Seq` + `Payload`);补第六抽象点 `IEventAuthority`;删掉「序列化只存内部 long」的假断言;标注 `IIdAuthority` 缺 `ItemInstanceId Next()` | ADR-005 §Key Interfaces | ✅ 已改 |
| B-5 | 新增 **Amendment C**(`Seq` 发放域唯一为 `(Tick, Patient)`,不分流;全序键升格 `(Tick, StreamPriority, Patient, Seq)`)| ADR-006 | ✅ 已加 |
| — | 新增 **Amendment D**(真源 = 两流并集;折叠只作用于病史流)—— 兑现 C-5 | ADR-006 | ✅ 已加 |
| B-6 | 删 ADR-006 重复的 `### Negative`;§Neutral 补第六抽象点注 | ADR-006 | ✅ 已改 |
| — | ADR-008 §一 删「按流分别单调」括号注,指向 Amendment C —— 兑现 C-4 | ADR-008 | ✅ 已改 |
| — | ADR-005 §三 加前向指针指向 Amendment D —— 兑现 C-5 | ADR-005 | ✅ 已改 |
| — | ADR-005 补 **E-1** 的可执行判据(门 A asmdef + 门 B IL 扫描),替换「编译期可验证」 | ADR-005 §Context | ✅ 已改 |
| **B-2** | `anchor` 的同名异型与 `.Tick` 不可能运算 | ADR-008 + 两 GDD | ✅ **已改**(取 (b) 降维,见下) |

**B-2 已裁决并落盘(2026-09-15,用户选 (b))**:

- **(b) 降维【已采用】**:37 的 `anchor: long` → `opened_tick: long`(`CaseOpened` / `CaseClosed`);
  `PatternRecognized` 的锚点改用 `anchor_case: CaseId`(顺带补上原本**漏掉**的锚点字段);
  `JudgmentRecorded` / `JudgmentRevised` 改用 `case_id`;`.Tick` 式子改写为 `c.opened_tick ∈ [e.Tick, c.CloseTick]`;
  52 侧生成点枚举改名 `spawn_anchor`(16 处)。**「不透明」措辞全部删除**。
- **(a) 拆名**:未采用。理由:为一个**至今没有 GDD、没有消费者**的系统(13 病人 AI)保留扩展位,
  代价是继续背负一个未定义字段。13 若将来需要,以**追加新字段**的方式引入。

落盘位置:ADR-008 §三 / §四 / Key Interfaces / Risks · `case-system.md:345` `:375` ·
`random-events.md`(全 16 处)· ADR-007 `EventArrived` 载荷。
TR 影响:`TR-case-005` `TR-case-008` `TR-randomevents-005` 由 ⚠️ 升 ✅(见可追溯性索引)。

> **编号说明**:B-1…B-9 为**本轮报告**的阻塞项编号,与各 GDD 内部的 `D-xx` 登记项编号无关。

### 需新 ADR —— 设计裁决点

| # | 阻塞项 | 为什么阻塞 | 优先级 |
|---|--------|-----------|--------|
| **B-7** | **世界状态的事件化边界** —— 开放世界(地形 / 建造 / 物品掉落拾取 / 位置)哪些进流、哪些只做表现 | 21a 已把 `IIdAuthority` 扩到 `ItemInstanceId`(D-21-26);**世界状态正在悄悄进入事件流**。不裁决 ⇒ 「确定性」范围在实现期被各系统各自扩大 ⇒ 地形/建造工具被迫确定性化。**决策空间最大、事后改最贵** | **最高** |
| **B-8** | **`instance_id` 权威 + Craft 事件总序键 + `ActualConsumed` 持久化**(item-db 的 6 条 NO-ADR) | 21a 自己登记的 blocking;`IIdAuthority` 目前只有 `PatientId Next()`,**无 `ItemInstanceId Next()`** ⇒ 迁移复位 = 静默重号(表现为物品悄悄合并/丢失,不崩溃) | 高 |
| **B-9** | **跨平台确定性 CI 门**(golden-hash 构建矩阵) | `disease-sim` AC-1 / `diagnosis` AC-8-F5 / `item-db` AC-21a-29 都**没有执行载体**。没有它,ADR-005 的「两个平台逐位相同」判据是纸面的 | 高 |

---

## 10. 需要的 ADR(按 Foundation → Feature 排序)

| 序 | ADR | 层 | 引擎风险 | 阻塞什么 |
|----|-----|----|---------|---------|
| **R-1** | **世界状态的事件化边界** | Foundation | HIGH | 地形/建造/物品/位置系统;ADR-002/003 的工具选型 |
| **R-2** | **7a 持久化与存档格式**(存档头 / 二进制 codec / 原子写 / 损坏恢复 / 迁移 / `max(patient_id)` 扫两流并集) | Foundation | MEDIUM | 四份 ADR 全部把义务派给它,它自己没有权威件 |
| **R-3** | **ADR-001 联机选型**(Netcode for GameObjects vs Photon Fusion)+ **两条逻辑流的可靠有序承载** | Foundation | MEDIUM | P0「架构预留」的落点;选型须支持自定义可靠有序消息流 + 承载两条流 |
| **R-4** | **输入架构**(Input System 动作映射 / K&M + Gamepad + OpenXR / 绑重持久化 / **急救动作 < 50 ms 延迟路径**) | Foundation | MEDIUM | 3 / 4 / 10 / 42 |
| **R-5** | **跨平台确定性 CI 门**(golden-hash 矩阵 + IL2CPP 编译旗标逐目标登记) | Foundation | HIGH | 三条 AC 无执行载体 |
| **R-6** | **拟物 UI 框架**(UI Toolkit + UGUI 分工 / 手柄焦点导航 / **VR UI 栈** / 拟物样式) | Foundation | HIGH | 42 / 39 / 48 / 7b;**UI Toolkit 无 world-space/XR** |
| **R-7** | **Addressables 数据管线**(`assets/data/*.json` 分组 / 启动预载 / 配置版本号与存档头联动 / 6.2+ 抛异常行为) | Foundation | MEDIUM | 21a / 52 的数据加载 |
| **R-8** | **JSON 解析器选型**(`com.unity.nuget.newtonsoft-json` vs 手写子集读取器) | Foundation | LOW | 21a 数据导入的硬前置;`FixParse` 的接缝 |
| **R-9** | **ADR-002 开放世界地形**(第三方 vs 自研;**第三方地形工具不是确定性的**,若须由 `WorldSeed` 逐位重建则整体出局) | Feature | HIGH | 6 / 23 |
| **R-10** | **ADR-003 建造系统网格**(Voxel vs 模块化;chunk / collider / NavMesh 动态 carving 成本) | Feature | HIGH | 23 / 24 |
| **R-11** | **ADR-004 是否引入 DOTS**(P0 已裁 9 不上 DOTS,全案未决) | Core | HIGH | — |
| **R-12** | **音频架构**(AudioMixer 快照 / 空间化 / 单 AudioListener) | Foundation | LOW | 44;8 的 G-7 称「44 是本节最重的技术债」 |
| **R-13** | **玩家控制器与摄像机**(CharacterController vs kinematic;**Cinemachine 3.0 是 post-cutoff 大改版**) | Core | MEDIUM | 1 / 2 |
| **R-14** | **AI 架构**(13 病人 AI 只读 DTO + NavMesh;27 敌人 AI;开放世界 + 动态建造的 NavMesh 烘焙成本) | Feature | MEDIUM | 13 / 27 |
| **R-15** | **遥测与隐私**(51) | Foundation | LOW | 51 |

> **编排提示**:R-1 与 R-9/R-10 是「确定性边界」这一既有 ADR 系的下一个承重决策,且直接决定第三方工具选型 —— **建议在 21a / 52 / 9 进实现的同时并行起草**。R-3 是 P1b 前置但 P0 需骨架,可与 R-1 并行(都只做接口,不互相阻塞)。

---

## 11. 预门控清单

| 项 | 状态 | 缺什么 |
|----|------|--------|
| `tests/unit/` | ❌ | 目录不存在 → `/test-setup` |
| `tests/integration/` | ❌ | 目录不存在 → `/test-setup` |
| `.github/workflows/tests.yml` | ❌ | 无 CI → `/test-setup` |
| `design/accessibility-requirements.md` | ❌ | 不存在 → `/ux-design` |
| `design/ux/interaction-patterns.md` | ❌ | 不存在 → `/ux-design` |

**5 / 5 ❌。** 预门控未就绪,**不得**进入 `/gate-check pre-production`。

---

## 12. 本轮结论一览

**利好的三件事(专家确认,应保持)**

1. 架构把 netcode 降级为**可靠消息管道** —— sim 不复制,只传 `SimEvent` + 快照 + checkpoint,顺序由 `SimEvent.Seq` 自己重建,传输层**连保序都不必需**(可靠 + 有界重排缓冲即可)。这大幅缩小了 ADR-001 的裁决空间。
2. 表现层 float 被隔离在 `IVitalsQuery` 门面之外,且 `Fix.ToFloat()` 本身是硬件转换、**跨平台逐位一致**。
3. Steam 全平台可收敛到 **IL2CPP 单后端** ⇒ AC-8-F5 从「困难」降为「可控」。

**因此 ADR-005 系的确定性目标在 Unity 6.3 上是可达成且可验证的**,风险集中在表现层 float,而它已有**定表 + 编译旗标**两条可执行路径。

**最大的问题不是四份 ADR 写错了(只错了两处编译级 + 一处引擎 API),而是它们只覆盖了 6 / 31 个 P0 系统,且世界状态的事件化边界没有裁决。**

---

## 13. 复查轮 —— `/architecture-review consistency`(2026-09-15 第二轮)

**范围**:Phase 4 跨 ADR 冲突检测(4 份 ADR:005 / 006 / 007 / 008;Unity 6.3 LTS)。
`docs/consistency-failures.md` **不存在** ⇒ 无「已知易冲突区」可比对。
GDD 未重载 —— 本模式只查 ADR 之间的冲突。

**复查动机**:上一轮报告落盘后,同批执行了 B-1…B-6 就地修正、Amendment C/D、B-2 方案 (b)、
E-1 判据。本轮验证这些修改**是否真的消解了 C-1…C-10**,并重点捕捉**修改自身引入的新冲突**。

### 13.1 上轮 C-1…C-10 结清状态

| # | 严重度 | 结论 | 依据 |
|---|--------|------|------|
| C-1 | 🔴 编译级 | ✅ **已结清** | ADR-007 §Key Interfaces 现为 `readonly struct PatientId`,可作字段 / 参数 / 返回类型 |
| C-2 | 🔴 不可实现 | ✅ **已结清** | `anchor` 已降维:`opened_tick`(37)/ `anchor_case`(37)/ `spawn_anchor`(52);`c.opened_tick ∈ […]` 已改写;`.Tick` 不再作用于不透明 `long` |
| C-3 | 🟠 静默数值错 | ✅ **已结清** | ADR-005 §Decision 一 已删自引 `3×2⁻¹⁶`,改为「待黄金文件对拍标定」;并补单向性验证判据。GDD `:570` 与 ADR 口径一致 |
| C-4 | 🟠 全序歧义 | ✅ **已结清** | ADR-006 **Amendment C** 裁 `Seq` 发放域唯一;ADR-008 §一 括号注已删 |
| C-5 | 🟠 权威件口径 | ✅ **已结清** | ADR-006 **Amendment D** 裁真源 = 两流并集;ADR-005 §Decision 三 已加前向指针 |
| C-6 | 🟡 复制源过期 | ✅ **已结清** | ADR-005 §Key Interfaces 已换为 Amendment A 形状 |
| C-7 | 🟡 假断言 | ✅ **已结清** | ADR-005 假序列化断言已撤销并入注 |
| C-8 | 🟡 结构残留 | ✅ **已结清** | ADR-006 重复 `### Negative` 已删(标题序列无重复) |
| C-9 | 🟡 时序陈旧 | ✅ **已结清** | ADR-006 §Neutral 已补「全案抽象点现为六个」 |
| C-10 | 🟠 排期债 | ❌ **仍未结清** | ADR-001 仍不存在。**性质不变:排期债,非文档冲突** —— 但 `TR-concept-002` 同时是 Foundation 层缺口 |

**小计:C-1…C-9 全部结清;C-10 仍开着(已知,不因本轮改变)。**

### 13.2 本轮新发现的冲突 C-11…C-18

> **来源分类**:C-11 / C-13 / C-14 **由上轮就地修正引入**(B-4 改 §Key Interfaces 时抄漏了类型与其余章节);
> C-12 / C-16 / C-18 是上轮未查出的**既有冲突**;C-15 / C-17 是修正注**自身造成的引用腐化**。

#### 🔴 C-11 · `SimEvent.Seq` 在 ADR-005 与 ADR-006 中**类型不同** —— **上轮修正引入**

```
Type: Integration contract(数据形状)
ADR-005 §Key Interfaces claims:  public readonly int  Seq;
ADR-006 Amendment A claims:      public readonly long Seq;   // ← 权威件
```

**Impact**:两处均可编译,失败**静默**。ADR-006 §五 要求自定义编码器写 `Seq`;
`int` 与 `long` 写出的流**宽差 4 字节** ⇒ 按任一份文档实现的读写两端错位。
ADR-006 §Performance Implications 已按「每条事件多 **8 字节**」计,是该类型为 `long` 的第三方佐证。
这直接击穿 ADR-005 的承重目标(逐位可重放)。

**Resolution(已采纳 1)**:
1. ✅ ADR-005 改为 `long`,并在块首注明「本块只是前向指针,不是第二份权威」
2. ✗ 反向改 Amendment A 为 `int` —— 需重开一份 Accepted 修正案,且与 8 字节口径矛盾

#### 🟠 C-12 · ADR-005 的**规范性**实施指引仍用旧全序键 —— 既有

```
Type: Integration contract(排序语义)
ADR-005 §Implementation Guidelines 2 claims: SimEvent 必须可全序(按 `(Tick, Patient, Seq)`)
ADR-006 Amendment C claims:                  跨流全序键升格为 `(Tick, StreamPriority, Patient, Seq)`
ADR-008 §二 / §Validation Criteria claims:   同上(已用新键)
```

**Impact**:照 ADR-005 的指引实现比较器 ⇒ 同 tick 的病史 / 病例事件**顺序无定义** ⇒
合并排序不稳定 ⇒ F-37.1「全序无平局」的前提失守。**静默类**(逐位仍一致,语义已错)。

**Resolution(已采纳 1)**:
1. ✅ ADR-005 §Implementation Guidelines 2 改为「跨流按新键、单流内按旧键」并加 Amendment C 指针
2. ✗ 保留旧键为唯一键 —— 需重开 Amendment C,且 ADR-008 的 fires-once 论证会连带失效

#### 🟠 C-13 · ADR-005 正文多处仍写「病史事件流 = 唯一真源」—— **上轮只修了一处**

```
Type: State ownership(真源归属)
ADR-005 §Summary / §Architecture 图 / §Related claims: 病史事件流是唯一真源
ADR-006 Amendment D claims:                            真源 = 病史流 ∪ 病例流
ADR-008 §七 claims:                                    max(patient_id) 重构扫两流并集
```

**Impact**:**这不是措辞问题,是 C-1/B-1 级静默漏洞的入口。**
上轮就地修正只在 ADR-005 §Decision 三 加了一条前向指针(该处已 OK),
但 §Summary、§Architecture 图、§Related 三处**未同步**。
维护者照 ADR-005 实现 `max(patient_id)` 重构 ⇒ **只扫病史流** ⇒
病例流中残留的 `patient_id` 漏扫 ⇒ 新 authority 高水位回退 ⇒ **id 复用 ⇒ 病程互写**。
与 Amendment B 要堵的是同一个后门,只是换了个入口。

**Resolution(已采纳 1)**:
1. ✅ ADR-005 §Summary 加 N-3/N-4 修正注、§Architecture 图改标「事件流 = 病史 ∪ 病例」、
   §Related 改写为「对象已扩为并集」并废止旧行号
2. ✗ 在 ADR-006 侧再写一条 Amendment E —— 权威件分散,A 与 D 之间又多一跳

#### 🟡 C-14 · ADR-005 的抽象点计数与实施清单落后于 ADR-007 —— **上轮修正引入**

```
Type: Integration contract(接口清单)
ADR-005 §Summary claims:        P0 必须预留的五个抽象点
ADR-005 §Migration Plan 步骤 2: 定义四个接口(不含 IEventAuthority)
ADR-007 §一 / ADR-006 §Neutral: 全案抽象点现为六个
```

**Impact**:§Migration Plan 是**可执行的实施清单**。照它做,`IEventAuthority` 会缺席,
而 52 的掷骰路径在 P0 就要接上它 ⇒ P1b 补接线 = **重构掷骰调用点** ——
正是 ADR-007 Alternative 1 用来否决「合并进 IEventSink」的同一条理由。

**Resolution(已采纳 1)**:
1. ✅ §Summary 改「六个」并展开枚举;§Migration Plan 步骤 2 改「五个接口」+ 补 `IEventAuthority` + 修正注
2. ✗ 在 ADR-007 侧声明「本 ADR 覆盖 ADR-005 的清单」—— 实施者仍会照 ADR-005 的清单动手

#### 🟡 C-15 · 跨 ADR 的硬编码行号**集体失效** —— **修正注自身造成**

```
Type: 文档完整性(引用腐化)
实测: ADR-005 因历次修正注由约 336 行增至 386 行
受影响: ADR-006 六处(:75 :76 :78 :99 :139 :239)·
        ADR-007 两处(:66 :396)· ADR-008 一处(:45)· ADR-005 自身两处
```

**Impact**:不改变语义,但**会把读者引到错误段落** —— 本轮复核已两次因此误判
(把 Amendment A 的 `:192` 当成现存的规范段落)。在「静默失败类」项目里,
错误的引用比缺失的引用更贵。

**Resolution(已采纳 1)**:
1. ✅ 全部改为**章节锚点**(§Decision 一 / §Decision 三 / §Key Interfaces /
   §Implementation Guidelines N / §六 等),并在 ADR-005 §Related 与 ADR-006 §Migration Plan
   写明「后续新增引用一律用章节名,不得再写行号」
2. ✗ 逐处重算行号 —— 下一次编辑即再次失效

> **2026-09-15 复查轮就地清结**:原「残留:GDD 侧的行号引用」一项**已随本轮 GDD 同步清零** ——
> ADR-006 中 `disease-simulation.md:143 / :868 / :887`、ADR-008 中 `diagnosis-system.md:810 / :815`、
> ADR-005 中 `disease-simulation.md:143 / :570` 均已改为**章节锚点**(详见本节末「就地清结记录」)。

#### 🟡 C-16 · Amendment C / D **没有任何验收判据** —— 既有

```
Type: 文档完整性(可验证性缺口)
ADR-006 §Validation Criteria: 十条判据,全部只覆盖 Amendment A / B
Amendment C(Seq 发放域)/ Amendment D(真源 = 并集): 零判据
```

**Impact**:没有可执行判据守「`Seq` 不分流」与「`max(patient_id)` 扫两流并集」——
而后者是 Amendment B 高水位不变量在双流世界里的**唯一守卫**。
ADR-008 §Validation Criteria 只有跨流全序单测(覆盖了 C 的一半),**无扫并集的判据**。

**Resolution(已采纳 1)**:
1. ✅ ADR-006 §Validation Criteria 补三条:① 同 `(Tick, Patient)` 的 `Seq` 跨流无重号;
   ② 跨流全序无平局(`StreamPriority` 决定);③ `max(patient_id)` 扫两流并集
   (夹具 = 病史流已折叠、病例流仍留该 id)
2. ✗ 只在 ADR-008 侧补 —— 判据应与其裁定的修正案同处

#### 🟡 C-17 · 修正案的**计数描述**与依赖表残留 —— 既有 + 修正注残留

```
Type: 文档完整性(引用计数)
ADR-006: §Summary「两处修正案(A / B)」· §Ordering Note「两处」· §Migration Plan「A / B」· §Related「两处」
ADR-008: §ADR Dependencies 与 §Related 均写「ADR-006(Amendment A/B …)」—— 漏 C / D
```

**Impact**:低(描述性)。但 **ADR-008 §一 / §二 直接依赖 Amendment C**,
依赖表漏记会让读者以为跨流全序键是 ADR-008 自己的裁决 ——
而这正是 C-4 当初的成因(职责归属在文档层不可见)。

**Resolution(已采纳 1)**:✅ 统一改为「Amendment A / B / C / D」;ADR-008 依赖表补 C / D 的内容摘要

#### 🟡 C-18 · `SimEvent` 的**字段序**在两份 ADR 中不一致 —— 既有

```
Type: Integration contract(编码契约)
ADR-006 Amendment A / ADR-008 §一:  { Tick, Patient, Seq, Kind, Payload }
ADR-007 §Constraints / §Related:    { Tick, Patient, Kind, Seq, Payload }
```

**Impact**:字段序本身在 C# 具名构造下无害,但 `SimEvent` 的序列化走的是
**自定义编码器**(ADR-006 §五)—— 若该编码器按**字段位置**而非**字段名**写,
两种抄本就会产出**两种字节流**,且长度对得上、值错位,**静默**。

**Resolution(已采纳 1 + 2 兼施)**:
1. ✅ ADR-007 两处改为 Amendment A 的字段序
2. ✅ ADR-006 §五 明文追加一条契约:「**自定义编码器按字段名编码,不得按字段位置编码**」

### 13.3 复查轮冲突汇总

| # | 严重度 | 类型 | 来源 | 落点 | 状态 |
|---|--------|------|------|------|------|
| C-11 | 🔴 静默流格式错 | Integration | 上轮修正引入 | ADR-005 §Key Interfaces | ✅ 已修 |
| C-12 | 🟠 全序歧义 | Integration | 既有 | ADR-005 §Implementation Guidelines 2 | ✅ 已修 |
| C-13 | 🟠 静默 id 复用入口 | State ownership | 上轮只修一处 | ADR-005 §Summary / 图 / §Related | ✅ 已修 |
| C-14 | 🟡 实施清单漏项 | Integration | 上轮修正引入 | ADR-005 §Summary + §Migration Plan | ✅ 已修 |
| C-15 | 🟡 引用腐化 | 文档完整性 | 修正注自身 | ADR-005/006/007/008 共 11 处 | ✅ 已修 |
| C-16 | 🟡 可验证性缺口 | 文档完整性 | 既有 | ADR-006 §Validation Criteria | ✅ 已修 |
| C-17 | 🟡 依赖表漏记 | 文档完整性 | 既有 + 残留 | ADR-006 + ADR-008 | ✅ 已修 |
| C-18 | 🟡 编码契约缺口 | Integration | 既有 | ADR-007 + ADR-006 §五 | ✅ 已修 |
| C-10 | 🟠 排期债 | 缺失依赖 | 既有 | 立 ADR-001 | ❌ 仍未结清 |

### 13.4 ADR 依赖顺序(复查后,无变化)

| ADR | Depends On | 层 |
|-----|-----------|-----|
| ADR-005 | None | Foundation |
| ADR-006 | ADR-005 | Foundation |
| ADR-007 | ADR-005, ADR-006 | Foundation |
| ADR-008 | ADR-005, ADR-006, ADR-007 | Core |

**拓扑序:005 → 006 → 007 → 008。无环。四份均 Accepted。**

⚠️ **外部依赖悬空(非 ADR 间冲突)**:
- 四份 ADR 都指向 **ADR-001(联机选型)** 与 **7a 持久化**,两者均**不存在**:
  ADR-007 §二 把 `WorldSeed` 的存档头义务押在 7a 上;ADR-008 §Migration Plan 把
  病史流折叠谓词押在 7a 上;ADR-007 §Constraints 要求裁决对 ADR-001 的两个候选都成立。
  **这些是外部前置,不是本组 ADR 的依赖字段所登记项,故不构成「depends on Proposed」。**

### 13.5 复查轮结论

**C-11 / C-13 是本轮最重要的两条**,且**都由上一轮的就地修正引入或遗漏**:

- **C-11** 是「改了复制源、没改权威件」——`int Seq` 留在 ADR-005 里,
  而 ADR-006 的 `long Seq` 才是权威。**两份文档都能编译** ⇒ 静默流格式错。
- **C-13** 是「改了一处、漏了三处」——§Decision 三 修好了,
  §Summary / §Architecture 图 / §Related 还写着「病史事件流是唯一真源」,
  而这句话正是 `max(patient_id)` 重构只扫一条流的口子。

**这两条共同说明一件事:上一轮的修正方法本身有缺陷 ——
在正文里加修正注,既会留下未同步的平行文本(→ C-13 / C-14),
又会让全部行号引用集体失效(→ C-15)。**
后续修正应改为「**就地改写正文 + 在文末留一份修正索引**」,
而不是「正文不动 + 段落内挂注」。

**ADR 之间的实质冲突现在是零**(C-11…C-18 全部结清);
剩下的全部是**覆盖缺口**问题(20 / 31 个 P0 系统无架构覆盖)与 **C-10 的 ADR-001 缺失**。
**总判定维持 FAIL —— 判定依据不在冲突,而在覆盖。**

### 就地清结记录(报告本身不落盘时,ADR / GDD / 注册表 / 索引的改动如下)

| 文件 | 改动 |
|------|------|
| `adr-005-deterministic-sim.md` | C-11 `Seq` → `long`;C-12 全序键补跨流;N-4「六抽象点」;E-1 门 A/B;C-15 行号锚点化 |
| `adr-006-fixed-point-boundary-contract.md` | C-16 补三条验收判据;C-18「按字段名编码」;**「两处修正案」→ A/B/C/D**;GDD 行号 → 章节锚点 |
| `adr-007-event-authority-and-roll-state.md` | C-18 字段序对齐;C-15 行号锚点化;§Related 补 Amendment C 指针 |
| `adr-008-case-event-stream.md` | 依赖表补 A–D;C-15 行号锚点化(GDD 行号 → 章节锚点) |
| `design/gdd/disease-simulation.md` | §Dependencies 抽象点节改「六项」(补 `IEventAuthority` 行 + 双门判据 + Amendment C 指针);「五抽象点」残留全清 |
| `docs/architecture/tr-registry.yaml` | 4 条 TR 状态重裁(008/013/014/019 → ✅),`revision_note` 落款 |
| `docs/architecture/traceability-index.md` | 汇总 **53 ✅ / 15 ⚠️ / 80 ❌**;变更历史新增复查轮 + 待办清结 |

> **TR 口径说明**:状态重裁为 `covered` 依据的是 **ADR-005 修正后文本** + **ADR-007 §一**,
> 均已在复查轮就地落盘 —— **未新增任何 TR-ID,总量 148 不变**。

---

## History

| Date | Verdict | 需求覆盖 | 冲突 | 备注 |
|------|---------|---------|------|------|
| 2026-09-15 | **FAIL** | 49 ✅ / 19 ⚠️ / 80 ❌ | 10 | 首次全量架构复核。TR 注册表首次建立(148 条)。同日落盘 B-1…B-6 就地修正 + E-1 判据(见 §9) |
| 2026-09-15 | **FAIL** | **52 ✅ / 18 ⚠️ / 78 ❌** | 10 → **0 实质** | **复查轮**(`consistency` 模式)。C-1…C-9 结清;C-10 仍开(排期债)。**新发现 C-11…C-18 并全部就地修正**,其中 C-11 / C-13 由上轮修正引入或遗漏。ADR 间实质冲突归零;4 条 TR 重裁(`TR-disease-008/013/014/019`)。判定仍 FAIL(依据 = 覆盖缺口,20 / 31 P0 系统无 ADR) |
