# P0 剩余面路线图 —— 静态骨架 + 动态推进量

**建立**:2026-10-20 · 用户裁定「静态方案结合动态方案」
**适用范围**:P0 剩余 **44 条 story**(M3 → M4 之间)
**不适用**:M4 内容面(42 项资产 / 数值轮)· P1a/P1b 面

> ## 实测三元组(本文件所有数字的出处)
>
> | 字段 | 值 |
> |---|---|
> | **截至时刻** | `2026-10-20T06:27` UTC |
> | **批次** | 路线图首建轮(非 sprint 收口批) |
> | **commit** | `dd7af25` |
>
> 口径:`production/epics/*/story-*.md` 逐文件取**首个 `Status:` 行**解析,非引用旧件。
> ⚠️ **任何引用本文件数字的下游件必须同时抄三元组** —— 否则即「伪基线」(`smoke-check` §基线溯源同款风险)。

---

## 〇、本文件的定位:两层,不混

| 层 | 回答什么 | 变化频率 | 谁定 |
|---|---|---|---|
| **静态层**(§一–§五) | 剩余面**由哪些件构成**、**谁在等谁**、**哪几道门必须先清** | 立卡/结案时变 | 本文件 |
| **动态层**(§六) | **这一轮拿哪几条**、**拿几条** | 每轮变 | 每轮 `/sprint-plan` 现裁 |

**为什么不合并成一份「8 个 sprint 的计划」**:见 §六·一(三条实测理由)。

---

## 一、剩余面全貌(实测)

| 量 | 值 |
|---|---|
| epic / story | **31 / 226** |
| story `Complete` | **182** |
| **剩余 story** | **44** —— 42 `Ready` + 1 `Pending` + 1 部分完成 |
| 全 story `Complete` 的 epic | **18 / 31** |
| 剩余面中带 `BLOCKED-BY` 标记 | **27 / 44** |
| 不带任何阻塞标记 | **17 / 44** |

> ⚠️ **`Ready` ≠ 可勾全 AC**。42 条 `Ready` 里 27 条卡面自带 `BLOCKED-BY`。
> 立卡状态与判据可达性是**两个字段**,混读会得到「还差 44 件,很快」的错误预期。
>
> ⚠️ **标记存在 ≠ 真阻塞**。实测已有 **3 处标记陈旧**(§七);每次取件前须按 §六·三 复核。

### 1.1 剩余件按 epic 分布

| epic | 剩余 | 形态 |
|---|---|---|
| `skeuomorphic-ui` | 1 | 部分完成(019-b/d 待切图冻结)` |
| `emergency-procedures` | 1 | 素材本体 |
| `medical-consequences` | 1 | 通道集成 |
| `patient-ai` | 1 | 素材本体 |
| `world-ecozones` | 1 | `Pending`(桌面走查 + 工具 CI) |
| `save-slot-ui` | 1 | UI · **`EPIC.md` 已于 2026-10-20 补建**;⚠️ 上游 7a 契约零落点,见 **G-4** |
| `casebook` | 6 | 单链 |
| `clinic-machine` | 5 | 单链 |
| `death-respawn` | 6 | 单链 |
| `foraging` | 5 | 单链 |
| `inventory-items` | 6 | 单链 |
| `processing` | 5 | 单链 |
| `tutorial-onboarding` | 5 | 单链 |

---

## 二、静态依赖图(由各 story 的 `Depends on / Unlocks` 机械导出)

### 2.1 七条链

```
casebook(39 脉案)
  001 ─→ 002 ─→ 003 ─→ 004
                     ↘ 005(UI,依赖 002/003/004 + 42 元件库)
                        ↘ 006(Integration,依赖 001/003/005 + 2 ICameraRig + 7a 锚点)

clinic-machine(24 医馆乘子)
  001 ─→ 002 ─→ 003 ─→ 004 ─→ 005
                ↘ (002 依赖系统 23 structure_at;未就绪可桩)

death-respawn(29 死亡轮回)
  001 ─→ 002
      ├─→ 003
      └─→ 004 ─→ 005 ─→ 006(UI)

foraging(15 采集)
  001 ─→ 002 ─→ 003
      └─→ 004 ─→ 005(依赖 003 + 004 + inventory-003)

inventory-items(20 库存)
  001 ─→ 002
      ├─→ 003 ─→ 004 ─→ 005 ─→ 006(UI)
      └─→ (002 与 foraging-003 / processing-003 双向)

processing(17 炮制)
  001 ─→ 002 ─→ 003
      └─→ 004 ─→ 005

tutorial-onboarding(48 教学)
  001 ─→ 002 ─→ 003 ─→ 004(UI)─→ 005(Integration 走查)
```

### 2.2 跨链耦合簇 —— 必须同批,不得拆

| 簇 | 成员 | 耦合点 |
|---|---|---|
| **物品流转簇** | `foraging-003` · `processing-003` · `inventory-001/002` | 事件载荷形状 ↔ `IIdAuthority` 铸造权威,互为上下游 |
| **容量簇** | `inventory-003/004` · `foraging-005` | `CanCarry` 是采集动作的采前闸 |
| **呈现收尾簇** | `casebook-005/006` · `inventory-006` · `tutorial-004/005` · `death-respawn-006` | 全部落在 42 元件库 + UI 走查面 |

> 拆开的代价:各自引接口桩,桩面绿而联调不绿 —— 本仓已有同型先例
> (`processing-002` 的 `BLOCKED-BY-42`,卡面自陈「只保证机制侧供数无数字泄漏」)。

### 2.3 五个单件收尾(不属任何链)

| 件 | 外部前置 |
|---|---|
| `emergency-procedures/story-008` | 素材本体(外部美术 / 用户) |
| `patient-ai/story-005` | 素材本体 |
| `skeuomorphic-ui/story-019`(b/d) | **五族切图与 atlas 布局冻结** = 美术裁定(019-c 已 Complete,零外部依赖) |
| `world-ecozones/story-005` | **ADR-022 关卡工具 CI**(外部产物) |
| `save-slot-ui/story-001` | **7a 的槽位契约**(`Checkpoint` / `Load` / `Writable` / `Loadable` / `Gating`)—— 见 **G-4**。~~该 epic 无 `EPIC.md`,需先补~~ ✅ 2026-10-20 已补 |

---

## 三、★ 四道硬门(最高杠杆 —— 清一道解锁一整片)

> **这一节是本次实测最重要的产出。**
> 四道门中有三道**不在剩余 44 条里** —— 它们挂在**已标 `Complete` 的 epic 之下**,
> 是「epic 关了但契约没落」的形态。不清它们,29 / 18 / 7a / 7b 的整片 AC 永远不可勾。
>
> ⚠️ **2026-10-20 补建 `save-slot-ui/EPIC.md` 时新揭 G-4** —— 它与 G-1 / G-2 **同族**
> (ADR 已裁 · GDD 已写 · epic 已标 `Complete` · 代码零落点),
> 但**不在前一轮普查的扫描面内**:上一轮只扫 §三 已登记的三道,
> **`Checkpoint` / `Writable` / `Gating` 这几个符号从未进过扫描列表**。

| # | 门 | 谁在等 | 实测(2026-10-20) | 挂牌 |
|---|---|---|---|---|
| **G-1** | `SelfLimited(entity, d)` **逐实体**判死谓词 | `29` 的 `AC-29-01/02/05/08/10/17`(6 条)· `25` 的 `AC-25-6-08` · `29` 全 epic 的 `BLOCKED-BY-9` 根因 | `unity/Assets/` 全树(排除注释)**零命中**。存在的是**疾病级** `Curve.SelfLimit` 标志(`RegistrySchema.cs:107`)与 F1 求值(`ProgressionEvaluator.cs:220`)**—— 与「逐实体谓词」是两件事,不得互顶** | ✅ **`TD-sim-01`**(2026-10-20 登记) |
| **G-2** | `Folded(p)` 折叠谓词 | `29` 的 `AC-29-16` · 7a 全链折叠面 | 全树 `bool Fold` / `FoldEligible` / `CanFold` / `ShouldFold` **零命中**(`CasebookScreen.IsFolded` 同名巧合,非此物) | ✅ **`TD-persist-01`**(2026-10-20 登记) |
| ~~**G-3**~~ | ~~`Recipe.owner ∈ {process, craft, build}` 字段~~ | ~~`18` 的 `AC-18-18`~~ | ❌ **本条判定已证伪,不成立** —— 字段**存在**:`Sim.Contracts/ItemDatabase/Recipe.cs:41` 的 `RecipeOwner Owner` + `RecipeOwner.cs:9` 枚举 + `CookedCodec.cs:181` 烘焙期读取。**原判错因 = grep 目录限定成 `Sim/ItemDatabase/`,而字段住 `Sim.Contracts/`**;且今日更早的 `42ab88c`(S-2)已订正判据 + 补真表 + 三转红证伪 | ❌ **不登记**;错因留档于债册 §「同批被证伪的一条」 |
| **G-4** | **7a 槽位契约的高层面** —— `Checkpoint(SaveSlot)` / `Load(SaveSlot)` / `Writable` / `Loadable` / `Gating` | `7b` 的 `AC-7b-01`(唯一写入 API)· `AC-7b-02` / `AC-7b-10`(两投影全部来自 7a)· 7a 自身的 `AC-7a-19`(读档即锁 / 载入安全上下文门) | **零落点**(2026-10-20 实测,详见下表)。`ISaveService.cs:20-28` 只到 `SaveBytes` / `LoadBytes`,其 doc 自陈「高层方法（Checkpoint/SaveOnExit/Load）归后续 story」;`SaveSlot` 只有 `SlotSeq` 一个字段,无头信息 | ✅ **`TD-persist-02`**(2026-10-20 登记,用户裁定「G-4 也登记 TD,不立 story」) |

### G-4 的逐项实测(2026-10-20T06:50 UTC · `EPIC.md` 补建轮 · `5353e38`)

扫描面:**`unity/Assets/Sim.Codec/` + `Sim/` + `Sim.Contracts/` 三目录全部 `.cs`,排除注释行**。
⚠️ 按 §十一 的元教训,**先声明扫描面再落结论**。

| 符号 | 生产侧命中 | 说明 |
|---|---|---|
| `void Checkpoint` / `Checkpoint(` | **0** | AC-7b-01 的宾语不存在 |
| `SaveOnExit` | **0** | ADR-010 §六 三处触发点之一 |
| `void Load` / `Load(SaveSlot` | **0** | 读侧入口不存在 |
| `Writable` | **1** | ❌ **非同物** —— `Sim/EnemyEncounter.cs:91` 的 `ValidateWritableSet()`(敌人遭遇集),与 7a 槽位可写性无关 |
| `Loadable` | **0** | AC-7b-02 / 10 的投影源不存在 |
| `Gating` | **0** | AC-7a-19 读侧半边不存在 |

**为什么它同族但漏在上一轮之外**:G-1 / G-2 的门是**逐个符号去 grep** 发现的,
它们的**发现路径**是「先有下游卡面的 `BLOCKED-BY` 指向某个 ADR 义务,再顺着义务名去 grep」。
`7b` 恰好是**唯一一个卡面零挂标**的 epic ⇒ **没有下游 `BLOCKED-BY` 把 G-4 顶出来**,
而 `Checkpoint` 又从未进过任何一轮的扫描符号表。
⇒ **§八 失败条件 #3 已触发**(见下)。

**共同形态(仍成立的 G-1 / G-2 / G-4)**:三者都是「ADR 裁了、GDD 写了、epic 关了、代码里没有」,
且**在归属 epic 自己的卡面里完全看不见** —— `disease-simulation/` 全 7 条 story 对 `SelfLimited` 零命中;
`persistence-service/` 两条 story 对 `Folded` 零命中、对 `Checkpoint` / `Writable` **同样零命中**
(见 §八 #3);而下游(29 / 25 / 37 / 53 / **7b**)各有引用自陈。
⇒ 形态 = **记在别人的账上,自己的账上看不见**。

> **✅ 2026-10-20 结账:三道全入册**(用户裁定 G-1 / G-2 / G-4 均「登记 TD,不立 story」)
>
> | 门 | TD | 归属 epic(均标全 `Complete`) | 触发条件 |
> |---|---|---|---|
> | G-1 | `TD-sim-01` | `disease-simulation`(7/7) | `death-respawn` 任一 story 开工前 / 9 下一轮动 F4 或 `StateMachine` |
> | G-2 | `TD-persist-01` | `persistence-service`(2/2) | `death-respawn/story-003` 开工前 / 7a 下次动 `Sim.Codec` |
> | G-4 | **`TD-persist-02`** | `persistence-service`(2/2) | **`save-slot-ui/story-001` 勾 AC-7b-01/02/10 之前** / 7a 下次动 `Sim.Codec` |
>
> ⚠️ **入册 ≠ 已还**:三条的 AC 判据**一条都仍不可达**。`§八 #3` 的普查待办**不因此关闭** ——
> 若普查再揭新户,本表继续追加。**G-4 的入册反而证明了普查的必要性**:它是在**第 31 个 epic 的补建过程里**
> 偶然撞见的,不是扫出来的。

⇒ **处置(用户裁定 2026-10-20「不立 story」)**:两条已登记入
`docs/tech-debt-register.md`(`TD-sim-01` / `TD-persist-01`),**不补立 story**。
理由:两个 epic 均已标全 `Complete`,补立会把计数从「全绿」打回「未全绿」、扰动 M3 分母;
而两条实现面各只一个谓词,随触发条件并入下一个动该 epic 的轮次即可。
⚠️ **不立 story ≠ 不还** —— 两条各带具名触发条件。

---

## 四、批次划分(静态)

**划分依据 = 阻塞源**,不是工作量,不是日期。

### 批 A —— 无外部阻塞,立即可推

**A-1 · 全 AC 可勾(卡面零 `BLOCKED-BY` 标记 **且** 上游契约逐代码实测存在)**

| 件 | 卡面挂标数 | 上游契约实测(2026-10-20) |
|---|---|---|
| `clinic-machine/001 · 002 · 003` | 0 / 0 / 0 | ⬜ **未跑 R-4**(见下表注) |
| `tutorial-onboarding/001` | 0 | ⬜ **未跑 R-4** |
| `casebook/002` | 0 | ⬜ **未跑 R-4** |
| ~~`save-slot-ui/001`~~ | ~~0~~ | ❌ **移出 —— 上游 7a 契约零落点(G-4)**,**唯一跑过 R-4 的一件** |

> ⚠️ **「⬜ 未跑」≠「✅ 无缺口」**(禁借绿)。本轮只因 `save-slot-ui/001` 移出而跑了
> **它一件**的 R-4;其余三格是**未测**,不是**通过**。
> 它们仍留在 A-1 的理由是:① 卡面零挂标;② **未发现**反例(不是**已排除**)。
> ⇒ **取件时必须先对这 2–4 件补跑 R-4**,未跑完不得宣称「A-1 全绿」。

> ⚠️ **全 44 条里仅 17 条卡面零挂标**,其中 **5 条落 A-1** · **7 条落批 B**
> (`casebook-004` / `clinic-machine-005` / `foraging-003` / `inventory-004` /
> `medical-consequences-004` / `processing-005` / `death-respawn-006`)·
> **4 条落批 C**(`emergency-008` / `patient-ai-005` / `world-ecozones-005` / `casebook-005`)·
> **1 条移出**(`save-slot-ui/001` → **A-2**,见下)。
> ⇒ **A-1 是本轮唯一「现在动手就能勾满」的 5 条**;其余 39 条无论做得多完整,
> `Status` 都只能停在「本体已交 / AC 留挂」的双列状态。

> **🔁 2026-10-20 订正(补建 `save-slot-ui/EPIC.md` 时实测)**
>
> A-1 的入选判据原为**单一条件**「卡面零 `BLOCKED-BY` 标记」。这是**卡面属性**,
> **不是代码测量** —— 而卡面挂标只登记了**立卡当时已知**的阻塞。
>
> `save-slot-ui/001` 正是这个漏洞的实例:卡面 0 挂标,但**逐代码实测**其两条上游链
> 在今日树里**都不存在**(`Checkpoint` / `Load` / `Writable` / `Loadable` / `Gating`
> 生产侧全零命中,见 **G-4**)。它之所以漏网,恰恰**因为**它是唯一零挂标的 epic ——
> **没有下游 `BLOCKED-BY` 把缺口顶出来**。
>
> ⇒ **A-1 判据自此改为双条件合取**:卡面零挂标 **∧** 上游契约逐代码实测存在。
> 第二条 = §6.3 于 2026-10-20 新增的 **R-4**(上游契约逐代码实测)。
> ⚠️ 本文件只对本条跑了这次逐代码实测,**其余 16 条零挂标件的上游未逐个扫**
> (见 §八 #3 的处置要求)—— 不宣称它们已过第二条。

**A-2 · 本体可交,但 AC 有半边必须挂 `BLOCKED-BY`**

| 件 | 挂标 | 性质 |
|---|---|---|
| `tutorial-onboarding/002 · 003` | `BLOCKED-BY-45` | **联机运行面**;卡面逐字「单机夹具不替代其 closure」 |
| `casebook/001 · 003` | `BLOCKED-BY-45` | 同上,`player_id` 跨机稳定性验证 |
| `foraging/001` | `BLOCKED-BY`(21a `item_key` 行) | 表行未落 |
| `foraging/002` | `BLOCKED-BY-ADR-012` | 三格矩阵不存在 + F7 spike 未跑 ⇒ **只有 Mono 侧自洽可记** |
| `processing/001` | `BLOCKED-BY-21a`(`O-18-R1`) | `Recipe.owner` 字段未落 = **G-3** |
| `processing/002` | `BLOCKED-BY-42` | 只卡**走查半边**(`AC-18-10b`);机制侧供数可交 |
| `inventory-items/001` | `R2/R3/R10/R11/OQ-20-5` 五件 | R10 已由 18 认领 / R11 已闭合 / **R3 未落地** / R2 载荷 / OQ-20-5 已裁 |
| `inventory-items/002` | `BLOCKED-BY-R3` | 高水位扫描集含 `BakedInitial` |
| `inventory-items/003` | `BLOCKED-BY-R9` | 走查半边在 `story-006`;机制侧可交 |
| **`save-slot-ui/001`** | **`BLOCKED-BY-7a`(G-4)** —— ⚠️ **卡面未记此标记**,由本轮逐代码实测补出 | **卡面挂标数 0,但上游五个符号(`Checkpoint` / `Load` / `Writable` / `Loadable` / `Gating`)生产侧全零落点**。⇒ **AC-7b-01 / 02 / 10 判据不可达**;剩下可交的 = 槽行 + 排序(`AC-7b-04`)+ 灰置呈现骨架(`AC-7b-06` / `07` / `12` 的一半) |

**批 A 的静态判据**:每条件的 `Depends on` 全部 ∈ {已 `Complete` 件, 同批前序件, 接口桩}。
⚠️ **该判据只保证「能开工」,不保证「能勾满」** —— 后者见 A-1/A-2 之分。

> **为什么仍把 A-2 放批 A**:它们的**本体**零外部阻塞,阻塞只在**验收的另一半**。
> 推迟它们 = 把可推进的机制件压着等联机/美术,而本仓纪律允许「本体交 + 半边挂标」
> (`death-respawn` 全链、`processing-002` 均已有此形态先例)。
> ⚠️ **但决不允许把「本体交」记成 `Complete`** —— 那正是 §五 要堵的假绿。

### 批 B —— 链尾(接批 A 完成后可推)

| 链 | 件 | 前置在批 A 内 |
|---|---|---|
| `casebook` | 004 → 005 → 006 | 是(⚠️ 005/006 另需 `OQ-CB-7` 形态 ⇒ 实为批 C 半) |
| `clinic-machine` | 004 → 005 | 004 另需 21a F1 clamp / 系统 1 K 接口 |
| `foraging` | 004 → 005 | 是(⚠️ `OQ-17-2` 已裁,见 SP-1) |
| `inventory-items` | 004 → 005 → 006 | 是 |
| `processing` | 003 → 004 → 005 | 是 |
| `tutorial-onboarding` | 004 → 005 | 是 |
| `death-respawn` | **全部 6 件** | ⚠️ **否 —— 全链 `BLOCKED-BY-9`,见 G-1** |
| `medical-consequences` | 004 | 13/42/44 均已 Complete |

### 批 C —— 外部阻塞(判据今日不可达)

| 件 | 阻塞类型 | 解除方 |
|---|---|---|
| `emergency-procedures/008` · `patient-ai/005` | 素材本体 | 外部美术 / 用户 |
| `skeuomorphic-ui/019`(b/d) | 切图冻结 = **美术裁定** | 用户 |
| `world-ecozones/005` | 关卡工具 CI | 外部产物 |
| `death-respawn/006` | `/ux-design` 全屏过渡 spec | UX 轮 |
| `casebook/005/006` | `OQ-CB-7`(结案形态) | 39 UX 轮(2026-10-20 已登记) |

> **批 C 不是「低优先级」,是「现在做等于自造断言」**。
> 承 `24ac2da` / `35b3a1f` 两轮 data-core 的裁定:这类件**先登记、不硬接**。

---

## 五、M3 Exit 判据的口径修正(必须改,否则假绿)

`production/milestones/README.md:371` 现写:

> - [ ] 31 个 P0 epic 全部 `Complete`

**该判据今天会假绿**。实测:`audio-system` **21/21 story 全 `Complete`**,
但 EPIC 头部自陈 **⛔ 不得收口** —— `TD-audio-100…109` 的 P0 面未清完
(`100` 的 10-c 已移出转 `TD-listen-01`;`101/102/109 第二半` 仍开)。

同型第二例:`telemetry-analytics` 10/10 全 `Complete`,但 `TD-epic-01` 明确登记
「10 条 story 中 3 条已转 `In Review`,余 7 条仍挂 `Complete` —— **不得读成已修好**」。

⇒ **判据改写为两条合取**:

```
M3 Exit（epic 面） =
    该 epic 的全部 story Status = Complete
  ∧ 该 epic 名下 TD 的 P0 面 = 0
```

⚠️ **这不是加码,是补回原判据漏掉的那一半**。本项目已两次踩过「story 全绿 ≠ epic 可收口」
(`TD-epic-01` / `TD-audio-100…109`),且 `audio-system/EPIC.md:11` 逐字写
「**不得据『17 份 story 全 Complete』推定已闭**」。

---

## 六、动态层:每轮怎么切

### 6.1 为什么不做「8 个 sprint 的日期表」

三条实测理由(均非保守):

1. **`estimate_days` 已于 sprint-12 废弃** —— sprint-11 声明 8 天 / 可用 6.4 天,实际 **2 个执行日清 16 件**,偏差 **≈2.6×**
   (`production/sprints/sprint-12.md:8-10`)。
2. **「件/日」已不可跨 sprint 比较** —— sprint-13 是 **8 件/日**,sprint-14 是 **3.7 件/日**,
   原因是**件变大**(单件 = 换真生产类 + 证伪轮 + 复评转 Complete 三合一),不是产能下降
   (`retro-sprint-14-2026-10-18.md:65-68`)。按件/日外推会系统性地两头错。
3. **计划外追加率 = 57%**(sprint-14:4/7)。这部分在规划时不可知,但**每轮必然发生**。

⇒ 强行排日期 = 重演 sprint-11 的估算失真,且会被后续文档当基线继承。

### 6.2 每轮取件的三条规则(照 `CAPACITY-MODEL.md`)

| 规则 | 内容 |
|---|---|
| **批量上限** | Must ≤ **2–4 件/批**;并发 ≤ **3** |
| **L 件单列** | 复杂度 L(≥3 依赖或跨平台族)**独占 1 批额度**,不与 S/M 混批 |
| **追加预算** | 规划时**预留 1 件额度**给计划外追加(实测追加率 57%,不预留必然超容) |

复杂度 C 的判法(`CAPACITY-MODEL.md` §三):**由依赖数 + 判别力要求定,不由工作量估**。
- **S** = 零依赖且判据单一
- **M** = 1–2 依赖,判据可机械构造
- **L** = ≥3 依赖,或需跨平台族,或需用户裁定

### 6.3 取件前的四条硬复核(`sprint-plan` Phase 1 的强制前置)

每轮开始,**逐条**对拟取件跑:

| # | 复核 | 判据 | 本轮实测已有的反例 |
|---|---|---|---|
| **R-1** | 卡面 `Status` 是否现势 | 打开文件读,不抄快照 | `story-019` 已由 `In Progress` 转 `Complete`(10-c 移出)—— 旧快照会读错 |
| **R-2** | 每个 `BLOCKED-BY-X` 的 **X 是否仍成立** | 对 X 跑一次实测(grep / 读裁决定) | `OQ-17-2` 已裁 2026-10-06,`foraging-004/005` 仍挂标记 |
| **R-3** | 每件的前提是「实现」还是「补登记」 | 读 EPIC §Epic Status + 代码 grep | 本仓已两次踩(见 §七) |
| **R-4** | 该件的**上游契约**在代码里是否存在(不只看卡面挂标) | 对上游 ADR 点名的**义务符号**逐个 grep,先声明扫描面 | **`save-slot-ui/001`** —— 卡面 0 挂标,上游 `Checkpoint` / `Load` / `Writable` / `Loadable` / `Gating` **全零落点**(G-4) |

> **R-2 是本路线图新增的硬前置**。此前 `sprint-plan` 只要求 R-1/R-3;
> 实测 27/44 条带 `BLOCKED-BY`,其中已有 3 处陈旧 ⇒ 不逐条复核就是在按旧账排期。
>
> **R-4 是 2026-10-20 新增的第四道(由 `save-slot-ui/001` 移出 A-1 一役提出)**。
> 它的存在理由:**R-1/R-2/R-3 全部只看「本件」与「本件的标记」,没有一条检查「本件依赖的上游到底存不存在」**。
> 卡面 `BLOCKED-BY` 是**立卡当时**对已知阻塞的登记;「上游 ADR 裁了但代码从没写」这一类
> **在卡面上结构性地写不出来** —— 立卡人不会去 grep 上游。
> ⚠️ **R-4 的执行成本显著高于 R-1~R-3**(要读上游 ADR 的义务清单再逐个 grep),
> 故不要求对全部 44 条预跑:**只在拟取件时对本轮那 2–4 件跑**。

### 6.4 每轮收口时回写本文件

| 时机 | 回写什么 |
|---|---|
| 某条件转 `Complete` | §1 计数 + §4 该链状态 |
| 某道 G-x 门清掉 | §3 划掉 + 从 §4 移除对应阻塞 |
| 发现新 `BLOCKED-BY` 或某标记陈旧 | §7 增行 |
| 连续 3 轮 C 判法偏差 > 1 档 | 按 `CAPACITY-MODEL.md` §六 条件 #4 换旋钮 |

---

## 七、陈旧前提清单(实测,取件前必须复核)

| # | 位置 | 旧说 | 实测 | 处置 |
|---|---|---|---|---|
| **SP-1** | `foraging/story-004:36` · `story-005:42` | `BLOCKED-BY-OQ-17-2`(`GATHER_MUL_CAP` 未裁) | `OQ-17-2` **已于 2026-10-06 裁定**(`foraging.md:711`:`GATHER_MUL_CAP = 3` · `GatherMul(L)=1+L/30` · `CapTable[L]` 61 项) | 取件时**就地订正卡面**;判据仍可能要数值轮冻结真实表数字 —— **但不该再写「未裁」** |
| **SP-2** | `death-respawn` 全 epic | `BLOCKED-BY-9` / `BLOCKED-BY-7a` | 两条**仍成立**(G-1 `SelfLimited` 零命中 · G-2 `Folded(p)` 零命中)—— 但性质已由「裁决待定」变「实现待落」 | 取件前确认,不订正 |
| **SP-3** | `milestones/README.md:49` / `:372` | `22/31` · `220 story / 172 Complete / 46 Ready` | **2026-10-15 快照,已过期**(现 31/226/182/44) | 下次 epic 收口核查时刷新 |
| **SP-4** | `audio-system/story-019` | 旧 `In Progress` | **已 `Complete`**(10-c 移出转 `TD-listen-01`) | 已订正于该卡 `:4` |
| **SP-5** | `Milestones §五 M3` | 「31 个 epic 全 Complete」单条判据 | 见 §五:会假绿 | **本文件要求改写为两条合取** |
| **SP-6** | 本文件 §四 **A-1** | 判据 = 「卡面零 `BLOCKED-BY` 标记」**单条件** | ⚠️ **判据本身有洞**(2026-10-20 实测):卡面挂标 = **立卡当时已知**的阻塞,不含「上游契约从未落过」。`save-slot-ui/001` 卡面 0 挂标但上游 5 个符号全零命中(G-4) | **判据已改为双条件合取**(§四 A-1 注);⚠️ **其余 16 条零挂标件的上游未逐个扫** —— 下次取件前按新判据补扫 |

---

## 八、本路线图的失败条件(防它变成下一份没人用的文档)

| # | 若发生 | 则 |
|---|---|---|
| 1 | 某轮 `/sprint-plan` 未引用 §6.3 四条复核(尤 **R-4**) | 本文件判为**未落地**,不因「已写」而算兑现 |
| 2 | 有人用「件/日」做线性外推排日期 | 等同 `CAPACITY-MODEL.md` §六 条件 #3 触发 ⇒ 本文件 §6.1 作废 |
| 3 | 又出现「epic 全 story Complete 但契约零命中」的新户 | ⇒ §三 的门清单**不完备**;须重做一次全 epic 契约普查,不得只补这一户 |
| **3′** | — | **⚠️ 已触发(2026-10-20)**。新户 = **G-4**(7a 的槽位契约,`persistence-service` 已标 `Complete 2/2`)。**处置未做**:§三 现在是**四道门,但仍是追赶式登记**,不是普查产出。⇒ **待办 = 对 18 个「全 story Complete」的 epic 逐个跑一次「其 ADR 义务符号 vs 代码」普查**(按 §十一 元教训,先声明扫描面)。**在普查做完之前,§八 #3 的判据保持红** |
| 4 | §1 计数与本文件三元组相距 > 3 轮未刷 | 计数段作废,重测后再用 |

---

## 九、本轮的「已落 / 已证」双列

承 `.claude/skills/code-review` Phase 8b 口径。

| 项 | Landed(已落) | Proven(已证) | Proven-by |
|---|---|---|---|
| 剩余 44 条计数 | ✅ | ✅ | 命令:逐文件首个 `Status:` 行解析(本文件 §〇 三元组 `dd7af25`) |
| 依赖图 §2 | ✅ | ✅ | 各 story `Depends on / Unlocks` 段逐条抄录,未推断 |
| G-1 `SelfLimited` 零命中 | ✅ | ✅ | 对 `unity/Assets` 全树 grep 带实参的 `SelfLimited(`(C# 文件)⇒ **0 处** |
| G-2 `Folded(p)` 零命中 | ✅ | ✅ | 对 `unity/Assets` 全树 grep `bool Folded`(C# 文件)⇒ **0 处** |
| ~~G-3 `Recipe.owner` 零命中~~ | ⚠️ **命令属实,结论不属实** | ❌ **撤回** | 命令本身为真(该目录下确为 0 处),但它验的是**「`Sim/ItemDatabase/` 目录下没有」**,而 §三 的断言是**「全树没有」** —— **扫描面 ≠ 断言面**。按本文件 §十一 元教训,未声明扫描面即落结论 ⇒ 该行当时**不具备 Proven 资格**,2026-10-20 就地撤回 |
| ~~G-x 门不在 TD 册~~ | ✅ | ❌ **已过期** | 该行描述的是**登记前**的时刻。当日稍后 G-1 / G-2 入册,**G-4 亦于同日入册为 `TD-persist-02`** ⇒ 现测 `SelfLimited` **6** · `Folded(p)` **5** · `Recipe.owner` **2**(销账记录)· `TD-persist-02` **1 处**。本行意图已被 §三 的结账表取代 |
| SP-1 陈旧阻塞 | ✅ | ✅ | `OQ-17-2` 裁定行 `design/gdd/foraging.md:711` 与卡面标记 `story-004:36` 并置 |
| M3 判据假绿 | ✅ | ✅ | `audio-system/EPIC.md:11` 自陈「不得据 17 份 story 全 Complete 推定已闭」+ `TD-epic-01` |
| ~~批 A-1/A-2 之分~~ | ✅ | ❌ **撤回** | ⚠️ **本行原标 ✅ 是借绿** —— 它验的只是「挂标数」,而 A-1 的断言是「**全 AC 可勾**」。两者不是一件事:`save-slot-ui/001` 挂标 = 0,但 AC-7b-01/02/10 的判据不可达。**A-1 新判据 = 双条件合取**(§四),本轮只对移出件跑了第二条 |
| **G-4 五个符号零命中** | ✅ | ✅ | 扫描面 = `unity/Assets/Sim.Codec/` + `Sim/` + `Sim.Contracts/` 全部 `.cs`,排除注释行;`Checkpoint` / `SaveOnExit` / `Load(SaveSlot` / `Loadable` / `Gating` ⇒ **各 0 处**;**`Writable` 1 处但非同物**(`Sim/EnemyEncounter.cs:91` `ValidateWritableSet`)。三元组 `2026-10-20T06:50` UTC · EPIC 补建轮 · `5353e38` |
| **`save-slot-ui/001` 移出 A-1** | ✅ | ✅ | 双向可复算:① 生产侧五符号零命中(上行);② `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/SaveSlotsScreen.cs` 实测 **44 行**、零槽行零投影调用 ⇒ 剩余面是「槽行+排序+投影+焦点」,不是「只差验收」 |
| 批次 A/B/C 归类 | ✅ | ✗ | **归类是判断,非实测** —— 挂标数可测,但「该批该不该这么切」无夹具;只能由取件后是否碰壁证伪 |
| 动态层三条规则 | ✅ | ✗ | 规则本身无判据;其有效性待下一轮 `/sprint-plan` 实战 |

> **`Landed=✅ / Proven=✗` 是合法的诚实状态** —— 批次划分与动态规则属判断面,
> 不宣称已证。**本轮未运行任何夹具,故不得为它们标 Proven ✅**。

---

## 十、立即可做的下一件事

**✅ 已完成(2026-10-20)**:G-1 / G-2 已登记入 `docs/tech-debt-register.md`
(`TD-sim-01` / `TD-persist-01`),用户裁定**不立 story**。G-3 经复核**证伪,不登记**(见 §三)。

**🔁 2026-10-20 第二次更新(`save-slot-ui/EPIC.md` 补建轮)**:

- `save-slot-ui/001` **已移出 A-1 → A-2**(理由 = **G-4**,见 §三)。
  ⚠️ **移出 ≠ 该件不能开始** —— 它仍可交「槽行 + 排序 + 灰置呈现骨架」,
  只是 `AC-7b-01 / 02 / 10` 必须挂 `BLOCKED-BY-7a`,**不得勾、不得记 `Complete`**。
- `save-slot-ui/EPIC.md` **已补建**(31/31 个 epic 目录自此均有 `EPIC.md`)。
- **A-1 判据改为双条件合取**(卡面零挂标 ∧ 上游契约逐代码实测存在),见 §四 注。

**下一步 = 按 §四 批 A-1 取件**:五条零挂标件 —— `clinic-machine/001-003` ·
`tutorial-onboarding/001` · `casebook/002`。
⚠️ 取件前按 §6.3 跑四条复核(R-1 卡面状态 / R-2 每个 `BLOCKED-BY` 是否仍成立 / R-3 实现还是补登记 / **R-4 上游契约逐代码实测**),
**并按 §四 A-1 的新增第二条,对拟取件的上游契约跑一次逐代码实测** —— 本轮只对 `save-slot-ui/001` 跑过。

**待用户裁定两件**(本轮只登记,不自裁):
1. **G-4 怎么还** —— 7a 的 `Checkpoint` / `Load` / `Writable` / `Loadable` / `Gating`,
   而 `persistence-service` 已标 `Complete 2/2`。**补 story** 还是 **照 G-1/G-2 先例登记 TD**?
2. **§八 #3′ 的普查** —— 18 个「全 story Complete」的 epic 是否需要逐个跑一次「ADR 义务符号 vs 代码」?

⚠️ **本文件未改任何代码 / 数据 / 产物**,亦未改 `milestones/README.md` 的计数与 M3 判据
(§五 只是提出改写要求,实际改写归 epic 收口核查轮)。

**不经用户指令不 commit。**

---

## 十一、本批的自我订正(留痕)

> ⚠️ **2026-10-20 补建 `EPIC.md` 轮追加 C-3 / C-4 / C-5**(原 C-1 / C-2 保留不动)。

| # | 原判 | 订正后 | 错因 |
|---|---|---|---|
| **C-1** | **G-3** `Recipe.owner` 字段未落地 | ❌ **证伪,不登记** —— 字段存在于 `Sim.Contracts/ItemDatabase/Recipe.cs:41` | grep 时把目录限定成 `Sim/ItemDatabase/`,而字段住在 **`Sim.Contracts/`**。**目录限定写错即得出反结论** |
| **C-2** | 三道门「都没有 story 承载」 | ⚠️ **收窄** —— G-2 的缺口**被三个下游 epic 各自登记过**(`case-system/story-006:39/:134` · `disease-simulation/story-002:54` · `death-respawn/story-003`),只是**从未入债册**且**责任 epic 自己没提** | 原判把「没有承载实现的 story」说成了「没有任何 story 提过」—— 两件事不同 |
| **C-3** | **A-1 判据** = 「卡面零 `BLOCKED-BY` 标记」单条件 | ⚠️ **改为双条件合取** —— 卡面零挂标 **∧** 上游契约逐代码实测存在 | **把「卡面属性」当成了「代码事实」**。卡面挂标只登记**立卡当时已知**的阻塞;「上游契约从未落过」这种缺口在卡面上**结构上写不出来**。`save-slot-ui/001` 即实例 |
| **C-4** | **G-x 门普查「已完备」** | ❌ **不完备** —— 追加 **G-4**,§三 由「三道」改「四道」 | 门是**逐个符号去 grep** 发现的,而符号表来自「下游卡面的 `BLOCKED-BY` 指向某个 ADR 义务」。**零挂标的 epic 没有下游指针** ⇒ 它的缺口**永远顶不出来**。同 C-3 的病根 |
| **C-5** | §九 表内 `批 A-1/A-2 之分` 标 **Proven ✅**;~~`G-3 零命中`~~ 标 **✅✅** | A-1/A-2 行**撤回 Proven**;**G-3 行整行撤回** | 两行都是**扫描面 ≠ 断言面**:A-1 验的是挂标数、断言的是「全 AC 可勾」;G-3 验的是「`Sim/ItemDatabase/` 没有」、断言的是「全树没有」。**这正是 §十一 元教训本身,却在本文件 §九 里犯了两次** |

⇒ **元教训**:「按标识符 grep 零命中」**只在扫描面覆盖全树时才成立**。
此后同型判定须**先声明扫描面**(扫了哪些目录、用了哪些别名),再落结论。
