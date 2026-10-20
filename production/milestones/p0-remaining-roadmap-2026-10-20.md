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
| `save-slot-ui` | 1 | UI(⚠️ 该 epic **无 `EPIC.md`**) |
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
| `save-slot-ui/story-001` | 无(但该 epic 无 `EPIC.md`,需先补) |

---

## 三、★ 三道硬门(最高杠杆 —— 清一道解锁一整片)

> **这一节是本次实测最重要的产出。**
> 三道门都**不在剩余 44 条里** —— 它们挂在**已标 `Complete` 的 epic 之下**,
> 是「epic 关了但契约没落」的形态。不清它们,29 / 18 / 7a 的整片 AC 永远不可勾。

| # | 门 | 谁在等 | 实测(2026-10-20) | 挂牌 |
|---|---|---|---|---|
| **G-1** | `SelfLimited(entity, d)` **逐实体**判死谓词 | `29` 的 `AC-29-01/02/05/08/10/17`(6 条)· `25` 的 `AC-25-6-08` · `29` 全 epic 的 `BLOCKED-BY-9` 根因 | `unity/Assets/` 全树(排除注释)**零命中**。存在的是**疾病级** `Curve.SelfLimit` 标志(`RegistrySchema.cs:107`)与 F1 求值(`ProgressionEvaluator.cs:220`)**—— 与「逐实体谓词」是两件事,不得互顶** | ✅ **`TD-sim-01`**(2026-10-20 登记) |
| **G-2** | `Folded(p)` 折叠谓词 | `29` 的 `AC-29-16` · 7a 全链折叠面 | 全树 `bool Fold` / `FoldEligible` / `CanFold` / `ShouldFold` **零命中**(`CasebookScreen.IsFolded` 同名巧合,非此物) | ✅ **`TD-persist-01`**(2026-10-20 登记) |
| ~~**G-3**~~ | ~~`Recipe.owner ∈ {process, craft, build}` 字段~~ | ~~`18` 的 `AC-18-18`~~ | ❌ **本条判定已证伪,不成立** —— 字段**存在**:`Sim.Contracts/ItemDatabase/Recipe.cs:41` 的 `RecipeOwner Owner` + `RecipeOwner.cs:9` 枚举 + `CookedCodec.cs:181` 烘焙期读取。**原判错因 = grep 目录限定成 `Sim/ItemDatabase/`,而字段住 `Sim.Contracts/`**;且今日更早的 `42ab88c`(S-2)已订正判据 + 补真表 + 三转红证伪 | ❌ **不登记**;错因留档于债册 §「同批被证伪的一条」 |

**共同形态(仍成立的 G-1 / G-2)**:两者都是「ADR 裁了、GDD 写了、epic 关了、代码里没有」,
且**在归属 epic 自己的卡面里完全看不见** —— `disease-simulation/` 全 7 条 story 对 `SelfLimited` 零命中;
`persistence-service/` 两条 story 对 `Folded` 零命中;而下游(29 / 25 / 37 / 53)各有引用自陈。
⇒ 形态 = **记在别人的账上,自己的账上看不见**。

⇒ **处置(用户裁定 2026-10-20「不立 story」)**:两条已登记入
`docs/tech-debt-register.md`(`TD-sim-01` / `TD-persist-01`),**不补立 story**。
理由:两个 epic 均已标全 `Complete`,补立会把计数从「全绿」打回「未全绿」、扰动 M3 分母;
而两条实现面各只一个谓词,随触发条件并入下一个动该 epic 的轮次即可。
⚠️ **不立 story ≠ 不还** —— 两条各带具名触发条件。

---

## 四、批次划分(静态)

**划分依据 = 阻塞源**,不是工作量,不是日期。

### 批 A —— 无外部阻塞,立即可推

**A-1 · 全 AC 可勾(卡面零 `BLOCKED-BY` 标记,实测)**

| 件 | 实测挂标数 |
|---|---|
| `clinic-machine/001 · 002 · 003` | 0 / 0 / 0 |
| `tutorial-onboarding/001` | 0 |
| `casebook/002` | 0 |
| `save-slot-ui/001`(⚠️ 先补 `EPIC.md`) | 0 |

> ⚠️ **全 44 条里仅 17 条卡面零挂标**,其中 **6 条落 A-1** · **7 条落批 B**
> (`casebook-004` / `clinic-machine-005` / `foraging-003` / `inventory-004` /
> `medical-consequences-004` / `processing-005` / `death-respawn-006`)· **4 条落批 C**
> (`emergency-008` / `patient-ai-005` / `world-ecozones-005` / `casebook-005`)。
> ⇒ **A-1 是本轮唯一「现在动手就能勾满」的 6 条**;其余 38 条无论做得多完整,
> `Status` 都只能停在「本体已交 / AC 留挂」的双列状态。

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

### 6.3 取件前的三条硬复核(`sprint-plan` Phase 1 的强制前置)

每轮开始,**逐条**对拟取件跑:

| # | 复核 | 判据 | 本轮实测已有的反例 |
|---|---|---|---|
| **R-1** | 卡面 `Status` 是否现势 | 打开文件读,不抄快照 | `story-019` 已由 `In Progress` 转 `Complete`(10-c 移出)—— 旧快照会读错 |
| **R-2** | 每个 `BLOCKED-BY-X` 的 **X 是否仍成立** | 对 X 跑一次实测(grep / 读裁决定) | `OQ-17-2` 已裁 2026-10-06,`foraging-004/005` 仍挂标记 |
| **R-3** | 每件的前提是「实现」还是「补登记」 | 读 EPIC §Epic Status + 代码 grep | 本仓已两次踩(见 §七) |

> **R-2 是本路线图新增的硬前置**。此前 `sprint-plan` 只要求 R-1/R-3;
> 实测 27/44 条带 `BLOCKED-BY`,其中已有 3 处陈旧 ⇒ 不逐条复核就是在按旧账排期。

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

---

## 八、本路线图的失败条件(防它变成下一份没人用的文档)

| # | 若发生 | 则 |
|---|---|---|
| 1 | 某轮 `/sprint-plan` 未引用 §6.3 三条复核 | 本文件判为**未落地**,不因「已写」而算兑现 |
| 2 | 有人用「件/日」做线性外推排日期 | 等同 `CAPACITY-MODEL.md` §六 条件 #3 触发 ⇒ 本文件 §6.1 作废 |
| 3 | 又出现「epic 全 story Complete 但契约零命中」的新户 | ⇒ §三 的门清单**不完备**;须重做一次全 epic 契约普查,不得只补这一户 |
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
| G-3 `Recipe.owner` 零命中 | ✅ | ✅ | 对 `unity/Assets/Sim/ItemDatabase/` 下全部 `.cs` grep `Owner` ⇒ **0 处** |
| 三道门不在 TD 册 | ✅ | ✅ | 对 `docs/tech-debt-register.md` 分别 grep `SelfLimited` / `Folded(p)` / `Recipe.owner` ⇒ **三者均 0 命中** |
| SP-1 陈旧阻塞 | ✅ | ✅ | `OQ-17-2` 裁定行 `design/gdd/foraging.md:711` 与卡面标记 `story-004:36` 并置 |
| M3 判据假绿 | ✅ | ✅ | `audio-system/EPIC.md:11` 自陈「不得据 17 份 story 全 Complete 推定已闭」+ `TD-epic-01` |
| 批 A-1/A-2 之分 | ✅ | ✅ | 逐卡面挂标数实测(A-1 六件均 `0`;A-2 逐件挂标见 §四) |
| 批次 A/B/C 归类 | ✅ | ✗ | **归类是判断,非实测** —— 挂标数可测,但「该批该不该这么切」无夹具;只能由取件后是否碰壁证伪 |
| 动态层三条规则 | ✅ | ✗ | 规则本身无判据;其有效性待下一轮 `/sprint-plan` 实战 |

> **`Landed=✅ / Proven=✗` 是合法的诚实状态** —— 批次划分与动态规则属判断面,
> 不宣称已证。**本轮未运行任何夹具,故不得为它们标 Proven ✅**。

---

## 十、立即可做的下一件事

**✅ 已完成(2026-10-20)**:G-1 / G-2 已登记入 `docs/tech-debt-register.md`
(`TD-sim-01` / `TD-persist-01`),用户裁定**不立 story**。G-3 经复核**证伪,不登记**(见 §三)。

**下一步 = 按 §四 批 A-1 取件**:六条零挂标件 —— `clinic-machine/001-003` ·
`tutorial-onboarding/001` · `casebook/002` · `save-slot-ui/001`(该件需先补 `EPIC.md`)。
取件前按 §6.3 跑三条复核(R-1 卡面状态 / R-2 每个 `BLOCKED-BY` 是否仍成立 / R-3 实现还是补登记)。

⚠️ **本文件未改任何代码 / 数据 / 产物**,亦未改 `milestones/README.md` 的计数与 M3 判据
(§五 只是提出改写要求,实际改写归 epic 收口核查轮)。

**不经用户指令不 commit。**

---

## 十一、本批的两处自我订正(留痕)

| # | 原判 | 订正后 | 错因 |
|---|---|---|---|
| **C-1** | **G-3** `Recipe.owner` 字段未落地 | ❌ **证伪,不登记** —— 字段存在于 `Sim.Contracts/ItemDatabase/Recipe.cs:41` | grep 时把目录限定成 `Sim/ItemDatabase/`,而字段住在 **`Sim.Contracts/`**。**目录限定写错即得出反结论** |
| **C-2** | 三道门「都没有 story 承载」 | ⚠️ **收窄** —— G-2 的缺口**被三个下游 epic 各自登记过**(`case-system/story-006:39/:134` · `disease-simulation/story-002:54` · `death-respawn/story-003`),只是**从未入债册**且**责任 epic 自己没提** | 原判把「没有承载实现的 story」说成了「没有任何 story 提过」—— 两件事不同 |

⇒ **元教训**:「按标识符 grep 零命中」**只在扫描面覆盖全树时才成立**。
此后同型判定须**先声明扫描面**(扫了哪些目录、用了哪些别名),再落结论。
