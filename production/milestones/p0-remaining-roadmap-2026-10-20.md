# P0 剩余面路线图 —— 静态骨架 + 动态推进量 + 后续 sprint 序列

> ## ⚠️ 本文件是**三份静态件物理合并后的唯一真源**(2026-10-20 · S16-2)
>
> | 被合并件 | 并入处 | 处置 |
> |---|---|---|
> | `production/p0-remaining-backlog-2026-10-20.md` | **§〇·二 判读边界** · **§一 分母台账** · **§2.4/§2.5** · **§4.4** · **§6.7** · **§七 S-1…S-6** | ✅ **已 `git rm`** |
> | `production/milestones/p0-completion-plan-2026-10-20.md` | **§〇 序列层** · **§3.1 门的两类角色** · **§5.2 进度真源三源** · **§6.5 序列** · **§6.6 裁定留痕** · **§八** | ✅ **已 `git rm`** |
>
> **合并纪律三条**:
> ① **§〇–§十一 编号一律未变** —— 本文件内部 ~60 处 § 引用与**外部 8 处**(跨 5 个文件)零失效;
>    吸收内容一律落为**子节**(§2.4 / §2.5 / §3.1 / §4.4 / §5.2 / §6.5–§6.7)。
> ② **去重而非拼接** —— 重叠面以「信息更全的一方」为准,另一方**整段不搬**:
>    剩余件分布(原 §1.1 保留为形态列 / backlog §1.2–1.3 提供台账)· 依赖链(§2.1 图 + §2.4 逐件可闭否)·
>    陈旧项(**SP-3 ≡ S-4** 一条重复,合并为一行)· 取件规则(§6.2 保留 / backlog §8 逐条并入注)。
> ③ **被吸收件的内部 § 引用已逐处重指** 到新编号(如 backlog 的「承 §三」= 依赖环 ⇒ 现 **§2.5**)。
>    ⚠️ **合并前若有人持有两件的副本,该副本即为第二真源,须作废** —— 本文件自 2026-10-20 起是唯一处。

**建立**:2026-10-20 · 用户裁定「静态方案结合动态方案」
**修订**:2026-10-20 · **S16-2**(三件物理合并;§5.1 落盘两条合取;§一 分母真源裁决)
**适用范围**:P0 剩余 **44 条 story**(M3 → M4 之间)
**不适用**:M4 内容面(42 项资产 / 数值轮)· P1a/P1b 面

> ## 实测三元组(本文件所有数字的出处)
>
> | 字段 | 值 | 管哪几节 |
> |---|---|---|
> | 截至时刻 | `2026-10-20T06:27` UTC | §二/§三/§四/§七/§九/§十/§十一 的门 · 链 · 批次 · 陈旧项实测 |
> | 批次 | 路线图首建轮(非 sprint 收口批) | 同上 |
> | commit | `dd7af25` | 同上 |
> | **刷新三元组** | 截至 `2026-10-20` · 批次 `sprint-16 规划轮(Phase 1 重扫)` · commit `be60b2c` | **仅 §一 分母台账** |
>
> ⚠️ **两个三元组只有一处差、且已定位**:`Ready` **42 → 41**、`In Review` **0 → 1** —— 唯一原因是
> `clinic-machine/story-001` 于 2026-10-20 本体交付转 `In Review`(见 §一 注)。其余各量**两轮同值**。
> 口径:`production/epics/*/story-*.md` 逐文件取**首个 `Status:` 行**解析,非引用旧件。
> ⚠️ **任何引用本文件数字的下游件必须同时抄三元组** —— 否则即「伪基线」(`smoke-check` §基线溯源同款风险)。

---

## 〇、本文件的定位:三层,不混

### 〇·一 三层分工

| 层 | 回答什么 | 变化频率 | 谁定 | 在哪 |
|---|---|---|---|---|
| **静态层** | 剩余面**由哪些件构成**、**谁在等谁**、**哪几道门必须先清** | 立卡/结案时变 | 本文件 | §一–§五 |
| **动态层** | **这一轮拿哪几条**、**拿几条** | 每轮变 | 每轮 `/sprint-plan` 现裁 | §6.1–§6.4 · §6.7 |
| **序列层** | 批次 A/B/C(无序)**展开成有序 sprint** | 每轮复核一次 | 本文件(承动态层) | **§6.5–§6.6** |

**为什么不合并成一份「8 个 sprint 的日期表」**:见 §6.1(三条实测理由)。
**序列层与动态层的关系**:序列层只主张**顺序**,不主张**件数与日期** —— 每轮仍走 `/sprint-plan` + §6.3 四条复核。

### 〇·二 判读边界(先读 —— 承已合并的 backlog §0)

1. **本文件不是排期。** 表内若出现 `Estimate` 列,那是 story 文件头部**原样抄录的字段**,仅供量级参照。
   `CAPACITY-MODEL.md` 明文:「它不预测『几天做完』,只约束『一批别塞太多』」。
   **本文件不合计人日、不给 sprint 数、不给 ETA。**
2. **分母只到 P0。** P1a / P1b 面(VR · 45 联机层 · 无障碍走查 · 目标硬件)不在本文件内。
3. **「阻塞」分三类,不可混读**:
   - **EXTERNAL** —— 依赖项目外部件(数值轮由用户裁定 / 外部美术 / CI 凭证),**agent 侧无法解除**;
   - **`BLOCKED-BY-*`** —— 依赖某个**代码或裁决**落地,agent 侧可推进但判据不得记绿;
   - **STALE** —— story 里写的阻塞**已被后来的裁决解除**,只是卡面没刷(§七 逐条列)。
4. **凡给出「实测」的格子,均附可复算命令**;凡引自其它文件的,标 `引用自`。
   **没有第三种**:不存在「我记得是」的数。
5. **计数判据必须先写死再数**(承 §十一 元教训)—— 「按标识符 grep 零命中」只在**扫描面覆盖全树**时才成立;
   落结论前**先声明扫描面**(扫了哪些目录、用了哪些别名、含不含 `Tests/`)。

### 〇·三 本文件明确**不**覆盖什么(承已合并的 backlog §7)

- **不做件选择** —— 下一个 sprint 做哪几件 = 收口时的 `/sprint-plan`;本文件只给约束(§6.2)与顺序(§6.5)。
- **不给 ETA / 人日 / sprint 数**(承 `CAPACITY-MODEL.md` §3.1)。
- **不动任何 story 的 `Status:`** —— 本文件是**只读勘察产物**。
- **不裁 `OQ-*`** —— 未裁项的裁定权归用户(逐条已标解除者,见 §4.4)。
- **不含 P1a / P1b 面**(VR · 45 联机层 · 无障碍走查 · 目标硬件)。
- **不含 `TD-listen-01` 的执行** —— L-1…L-5 的解除条件已在该债条,§4.4 E-1 只做引用。
- **门(TD)的清偿实施不在本文件** —— 载体已裁为「TD 工作项」(§6.6 ①),实施轮次在 §6.5 的 sprint-18/19。

---

## 一、分母台账(实测订正)

> **本节是三份静态件里唯一有分歧的一节,故吸收时以「信息更全的一方」为准** ——
> 原路线图 §一 只给汇总行,已合并的 backlog §1 给出**逐级分解 + 复算脚本 + epic 级真源裁决**。
> 两者**无实质冲突**,只在本节 §1.2 有一处**分母真源**需要裁(见下)。

### 1.1 story 级台账

| 量 | 实测值 | 复算命令 |
|---|---|---|
| P0 story 总数 | **226** | `ls production/epics/*/story-*.md \| wc -l` |
| `Complete` | **182**(80.5%) | 见下方脚本 |
| `Ready` | **41** | 同上 |
| `In Review` | **1**(`clinic-machine/story-001`) | 同上 |
| 非 `Complete` / 非 `Ready` / 非 `In Review` | **2** | 同上 |
| ⇒ **剩余(非 `Complete`)** | **44** | 182 + 44 = 226 ✓ |
| 剩余面中带 `BLOCKED-BY` 标记 | **27 / 44** | §七 逐条 |
| 不带任何阻塞标记 | **17 / 44** | §七 逐条 |

> ⚠️ **一处口径漂移已定位并留痕(不是新数,是同一个量的两个时点)**:原路线图 §一 写
> 「44 = **42** `Ready` + 1 `Pending` + 1 部分完成」(三元组 `2026-10-20T06:27`),
> 现为「44 = **41** `Ready` + **1** `In Review` + **2** 其他」。**唯一原因 = `clinic-machine/story-001`
> 于 2026-10-20 本体交付转 `In Review`**(旧写法把 `In Review` 折进了 `Ready`)。**总数与 `Complete` 未变。**
>
> ⚠️ **`Ready` ≠ 可勾全 AC**。41 条 `Ready` 里 **27** 条卡面自带 `BLOCKED-BY`。
> 立卡状态与判据可达性是**两个字段**,混读会得到「还差 44 件,很快」的错误预期。
>
> ⚠️ **标记存在 ≠ 真阻塞**。实测已有**多处**标记陈旧(§七);每次取件前须按 §6.3 **四条**复核。

**两件「非 `Complete` / 非 `Ready` / 非 `In Review`」逐条**:

| 件 | Status 原文 | 性质 |
|---|---|---|
| `skeuomorphic-ui/story-019` | `部分完成 —— 019-c/d/e/f Complete · 019-b 冻结轮落定 ✅(门待接线)` | **部分完成**,不是未开工。`019-b` 的判据已实测倒推冻结,剩的是**接线**。 |
| `world-ecozones/story-005` | `Pending — [L] 桌面走查 + 工具 CI` | **待人工 / CI**,非代码面 |

> 复算脚本(读每份 story 的 `> **Status**:` 行;⚠️ 该行**带引用块前缀 `>`**,用
> `^\s*>?\s*\*{0,2}Status\*{0,2}\s*[:：]` 才能命中 —— 漏掉 `>` 会把 226 条全判成「Other」):
> ```bash
> python3 - <<'EOF'
> import re,glob,collections
> by=collections.defaultdict(list); c=collections.Counter()
> for f in sorted(glob.glob('production/epics/*/story-*.md')):
>     t=open(f,encoding='utf-8',errors='replace').read()
>     m=re.search(r'^\s*>?\s*\*{0,2}Status\*{0,2}\s*[:：]\s*(.+)$',t,re.M)
>     s=re.sub(r'[\*\[\]]','',m.group(1)).strip() if m else '(none)'
>     c[s.split()[0] if s else '?']+=1; by[f.split('/')[-2]].append(s.startswith('Complete'))
> print(dict(c)); print('全 Complete epic:',sum(1 for v in by.values() if v and all(v)),'/',len(by))
> EOF
> ```

### 1.2 epic 级 —— ✅ **分母真源裁决**(18 / 31)

| 来源 | 全 `Complete` epic 数 | 状态 |
|---|---|---|
| `production/milestones/README.md:49` / **`:370-385` 块**(行号已随 S16-2 的加注下移;改前为 `:371` / `:377 块`)/ **`:570`**(改前 `:532`) | **22 / 31** | ❌ **陈旧** —— 三元组 `2026-10-15 · sprint-12 收口 · a5ef7c5`,**原地加注降级、原文不删**(S16-2 已落) |
| **本文件实测(按 story `Status:` 逐条归并)** | **18 / 31** | ✅ **唯一真源**(两轮独立实测同值:路线图首建轮 + sprint-16 规划轮重扫) |

> **判据 = epic 全 `Complete` ⟺ 其**全部** story 均 `Complete`**(逐目录归并,不看 `EPIC.md` 的分数)。
> **差额构成(逐户实测,非推断)**:`emergency-procedures`(8/9)· `medical-consequences`(3/4)·
> `patient-ai`(4/5)· `skeuomorphic-ui`(24/25)· `world-ecozones`(5/6)—— 这五户**不是**全 `Complete`。
> 22 与 18 的差另有 **4/31** 来自里程碑文件的旧快照。
> ⚠️ **分母 31** = `production/epics/` 下的 **31 个目录**(`save-slot-ui` 的 `EPIC.md` 已于 2026-10-20 补建,31/31 均有)。

**⚠️ `EPIC.md` 的「X/X story 已实现」不可直接采信** —— 实测其**分母自身就偏小**:

| epic | `EPIC.md` 声称 | 实际 story 文件数 | 差 |
|---|---|---|---|
| `emergency-procedures` | `Complete ✅(7/7 story 已实现)` | **9** | 分母少 2 |
| `patient-ai` | `✅ 4/4 story 全闭` | **5** | 分母少 1 |
| `skeuomorphic-ui` | `Complete ✅(18/18 stories)` | **25** | 分母少 7 |
| `world-ecozones` | `Complete ✅(6/6 story 已实现)` | **6**(实 `5 Complete + 1 Pending`) | **分子多 1** |

⇒ **判 epic 完成度只认 story `Status:` 的逐条归并,不认 `EPIC.md` 的分数。**
四户刷账 = **S16-5**(本 sprint);`medical-consequences` 的复核归其 epic 复评轮。

> ### ✅ 四户刷账已执行(2026-10-21 · S16-5)
>
> | epic | 刷前 header | 刷后 header | 实际 story 文件数 | 判定 |
> |---|---|---|---|---|
> | `emergency-procedures` | `Complete ✅(7/7)` | 🔁 **未 Complete —— 8/9** | **9** | 分母 7→9 ✅ · 状态词改判 |
> | `patient-ai` | `✅ 4/4 story 全闭` | 🔁 **未 Complete —— 4/5** | **5** | 分母 4→5 ✅ · 状态词改判 |
> | `skeuomorphic-ui` | `Complete ✅(18/18)` | 🔁 **未 Complete —— 24/25** | **25** | 分母 18→25 ✅ · 状态词改判 |
> | `world-ecozones` | `Complete ✅(6/6)` | 🔁 **未 Complete —— 5/6** | **6** | **分子 6→5** ✅(分母本已正确) |
>
> **改法**:每份 EPIC.md **插入新的 `**Status**` 行**于原行之上,原行改标
> `**Status(历史 · 原文保留)**`、**内容一字未删**;§Epic Status / §Next Step / §里程碑归属
> 三处派生论断同步就地加注。**零 story 状态变更**(`git status` 对 `story-*.md` 为空)。
>
> **⚠️ 本块推翻的是上文那句「不认 `EPIC.md` 的分数」的**理由**,不是它的**结论**:
> 刷账后**两口径同值**,可复算 ——
> ```
> EPIC.md 直接读出  → 全 Complete epic: 18 / 31
> story 逐条归并    → 全 Complete epic: 18 / 31   (非 Complete 13 户名单逐一相同)
> 改前 EPIC.md 读出 = 22 / 31  ← 22 与 18 的差额 4 户 = 本表四户
> ```
> 原文不删。**今后的读法**:两口径互为校验(任一偏离 ⇒ 说明 EPIC.md 又被写脏或 story 状态漂移)。

### 1.3 分母的构成(44 件的链归属;`Estimate` 为 story 头部原值,量级参照)

| epic | 剩余件数 | 链性质 | `Estimate` 合计(原值) |
|---|---|---|---|
| `casebook` | 6 | 内部**单链** 001→002→003→004→005/006 | 25 h |
| `clinic-machine` | 5 | 内部**单链** 001→002→003→004→005 | 27 h |
| `death-respawn` | 6 | 内部**单链** 001→{002,003,004}→005→006 | 30 h |
| `foraging` | 5 | 内部**单链** 001→002→{003,004}→005 | 28 h |
| `inventory-items` | 6 | 内部**单链** 001→{002,003,004}→005→006 | 33 h |
| `processing` | 5 | 内部**单链** 001→002→004→003→005 | 29 h |
| `tutorial-onboarding` | 5 | 内部**单链** 001→002→003→{004,005} | 21 h |
| `medical-consequences` | 1 | 尾件 | 5 h |
| `patient-ai` | 1 | 尾件(外部美术) | 24 h |
| `emergency-procedures` | 1 | 尾件(外部美术) | 待估 |
| `save-slot-ui` | 1 | 单件 epic(`EPIC.md` 2026-10-20 补建) | 2 h |
| `world-ecozones` | 1 | `Pending`(桌面走查 + 工具 CI) | — |
| `skeuomorphic-ui` | 1 | 部分完成(`019-b/d`) | — |
| **合计** | **44** | | **224 h**(41 件有值) |

> ⚠️ **`224 h` 不是排期 —— 留着它同时也把它钉死**:它是把 41 个 story 头部的 `Estimate` 字段
> 相加的**算术和**。依 `CAPACITY-MODEL.md` §3.1,该值**不折算人日、不折算 sprint 数、不参与任何外推**。
> 列出的唯一用途 = **量级参照**,并提醒一件事:`patient-ai/story-005` 一件就占 **24 h**,
> 是单件里最重的。⚠️ **任何把本列用于外推的行为 ⇒ 触发 §八 失败条件 #3。**

### 1.4 剩余件按 epic 的**形态**(信息性,与 §1.3 同一批的两种切法)

| epic | 剩余 | 形态 |
|---|---|---|
| `skeuomorphic-ui` | 1 | 部分完成(`019-b/d` 待切图冻结) |
| `emergency-procedures` | 1 | 素材本体 |
| `medical-consequences` | 1 | 通道集成 |
| `patient-ai` | 1 | 素材本体 |
| `world-ecozones` | 1 | `Pending`(桌面走查 + 工具 CI) |
| `save-slot-ui` | 1 | UI · `EPIC.md` 已于 2026-10-20 补建;⚠️ 上游 7a 契约零落点,见 **G-4** |
| `casebook` | 6 | 单链 |
| `clinic-machine` | 5 | 单链(`001` 已本体交付转 `In Review`) |
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

### 2.4 逐件「可闭否」(八条链 + 尾件 · 承已合并的 backlog §2)

> **判据**:**可闭** = 该件的全部 AC 有无外部阻断;`⚠️` = 有半件 / 部分 AC 永远绿不了;
> 🔴 = 硬阻(判据今日不可达,不属于「排期问题」)。
> **本节与 §2.1 的关系**:§2.1 给**链的形状**,本节给**逐件的闭合性** —— 同一批事实的两种切法,非重复。

#### 2.4.1 `casebook`(6 件)— 链最短、外部面最小

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 分册态契约与存档段 | ADR-010 codec(既有)· `IIdAuthority` 机制 A(既有) | `player_id` 跨机稳定性 → **EXTERNAL·BLOCKED-BY-45**(P0 单机不阻塞) | ✅ 可闭 |
| 002 病例列表排序纯函数 | 001 | 无 | ✅ 可闭 |
| 003 分册键与改史投影 | 001 · 002 | AC-39-04③ id 跨机 **BLOCKED-BY-45**(单机夹具模拟迁移可先行) | ✅ 可闭 |
| 004 反幻想能力守卫 | 002 · 003 | 无 | ✅ 可闭 |
| 005 页布局与焦点导航 | 002 · 003 · 004 · 42 元件库(**已 Complete,直连**) | 无 | ✅ 可闭 |
| 006 相机模式意图与落笔锚点 | 001 · 003 · 005 · 2 的 `ICameraRig`(既有)· 7a 锚点接口 | 联机落笔形态 **BLOCKED-BY-45**(接口形状可落,运行面不绿) | ⚠️ 半闭 |

> **判读**:001→005 五件**无硬外部门**,是全 P0 剩余面里最干净的一条链。

#### 2.4.2 `clinic-machine`(5 件)— 第二干净

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 烘焙表 schema 门 | ADR-014 两阶段工具链(**已可运行**) | ⚠️ `AC-24-08` / `AC-24-12` 半边 **BLOCKED-BY-O-24-1**(= **G-8**);**本体已于 2026-10-20 交付转 `In Review`**<br>🔁 **2026-10-21 订正**:G-8 已结(S17-4)⇒ 该半边**已解除挂标并勾选**(`story-001:52` / `:55`);原文不删 | ⚠️ 半闭(6/7 AC 面)<br>🔁 **2026-10-21 读数**:卡面 AC 勾选 **7/7**(原 6/7);本件仍 `In Review` 是**另三条**现役理由,**与 G-8 无关** |
| 002 房间切分与连通性 | 001 · 系统 23 `structure_at`(⚠️ **条件已消解,不需桩** —— 见 §七 SP-8) | 无硬门 | ✅ 可闭 |
| 003 修饰符求值 | 001 · 002 | 无 | ✅ 可闭 |
| 004 交付边界 `ClinicEnvDto` | 003 · 21a F1 clamp · 系统 1 K 消费(桩先行) | 联调前 **BLOCKED-BY**,不记绿。⚠️ **实为「接线 / 补登记」非从零实现** —— 见 §七 SP-7 | ⚠️ 半闭 |
| 005 纯性重放与记忆化 | 002 · 003 · 004 | AC 跨平台对拍 **EXTERNAL·BLOCKED-BY-ADR-012** | ⚠️ 半闭 |

#### 2.4.3 `death-respawn`(6 件)— 有一处**硬设计前置缺失**

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 `PlayerDied` 结算 | epic 入口 · 9 的 `DeathCandidate`(⚠️ **未落地,桩先行**)· kindgen(既有) | AC-29-01 全链 **BLOCKED-BY-9** | ⚠️ 半闭 |
| 002 掉落清单与流回收 | 001 · 20 库存读 + `IIdAuthority`(既有)· 4 拾取意图(桩) | AC-29-02/17 **BLOCKED-BY-9** | ⚠️ 半闭 |
| 003 技能等级下降(int 截断) | 001 · 30 story-005(**已 Complete**)· 7a 折叠面 | AC-29-05 **BLOCKED-BY-9**;AC-29-16 见 **§七 S-1** | ⚠️ 半闭 |
| 004 死亡冷却(流派生) | 001 | AC-29-08/10 **BLOCKED-BY-9** | ⚠️ 半闭 |
| 005 复活床格传送命令 | 001 · 004 · 1 的命令消费(桩) | **24 床格「选定规则」未落地** → **BLOCKED-BY-24** | ⚠️ 半闭 |
| 006 呈现白名单 | 005 | 🔴 **`/ux-design` 全屏过渡 spec = 硬前置,实测该 spec 不存在**(`design/ux/` 十份无一份是死亡过渡) | 🔴 **硬阻** |

> ⚠️ `design/ux/` 现有十份:`casebook-39` `clinic-panel-24` `hud` `index` `interaction-patterns`
> `inventory-container-20` `paper-closeup-48` `save-slots-7b` `settings-shell-42` `tutorial-48`。
> **无死亡 / 复活过渡 spec** ⇒ `story-006` 需**先做设计件**(`/ux-design`),不能直接实现。

#### 2.4.4 `foraging`(5 件)— 外部面最宽(跨平台 AC)

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 品种闭集与装载校验 | item-db 001/002(**Complete**)· 关卡工具(编辑期,非运行时阻塞) | 🔴 **21a 的 `item_key` 行未落地** —— 实测 `item_database_items.json` 首条键为 `base_id`,**无 `item_key` 字段**(= **§七 S-3**)⇒ 该子用例 `Ignore` 是**真阻塞**;`OQ-17-7` 未裁(按驻留最小实现,不构成阻塞) | ⚠️ 一件 AC 挂起 |
| 002 品质掷骰管线 | 001 · 30 的 `QueryLevel`(**Complete**)· `SplitMix64` / `Fix`(**Complete**) | AC-17-02 跨平台 **EXTERNAL·BLOCKED-BY-ADR-012**(Mono 半边可证) | ⚠️ 半闭 |
| 003 三事件与身份铸造 | 002 · 30 `EmitGrowth` / `SkillGrown`(**Complete**)· `entities.yaml` 三支(零 kindgen 变更) | 无**外部**门;但见 **§2.5 环 A** | ✅ 可闭 |
| 004 节点余量前缀函数 | 001 · 002 | AC-17-13 **BLOCKED-BY-OQ-17-2** —— ⚠️ **该裁决已于 2026-10-06 落**,卡面未刷(= **§七 SP-1**)⇒ 真阻塞 = **数值轮冻结真表数字**,不是「未裁」 | ⚠️ 形状可闭 / 数值不可 |
| 005 动作原子性与失败收敛 | 003 · 004 · `inventory-items/003` · 3/4(既有 `Complete` 件) | AC-17-07b/13b **EXTERNAL(42 呈现未交付)** + 数值轮 | ⚠️ 半闭 |

#### 2.4.5 `inventory-items`(6 件)— **全 P0 剩余面阻塞最重的一条链**

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 `InventoryOf` 折叠重建 | item-db 001/002(**Complete**)· `foraging/003` · `processing/003` | 🔴 **AC-20-03 BLOCKED-BY-R2/R3/R10/R11/OQ-20-5**(五件);AC-20-16 **BLOCKED-BY-OQ-20-5/R2** | ⚠️ 主体挂起 |
| 002 id 权威与高水位 | 001 · item-db story-008 类先例(**Complete**)· ADR-025 | **R3 未落地**(高水位基线缺 `BakedInitial`)⇒ 预摆物撞号用例挂 `BLOCKED-BY-R3` | ⚠️ 半闭 |
| 003 堆叠与容器折重 | 001 · 004 · item-db 002/004(**Complete**) | `COLUMNS` / `ROWS_PER_PAGE` 归 **42 未定** ⇒ **BLOCKED-BY-R9** | ⚠️ 半闭 |
| 004 载重与容量裁决 | 001 · 003 · item-db 002(**Complete**) | 🔴 **`CARRY_CAP` 值未裁**(`entities.yaml:1780` `value: "待定"`;`OQ-20-2`)⇒ **AC-20-09 的硬不变量无值可验**,且**装载期须硬失败** | 🔴 **数值轮** |
| 005 原子性 / 消耗 / 恢复 / 无确认 | 001 · 003 · 004 · `processing/003,004` · `modular-building 002/005`(**Complete**) | `DropDespawned.qty`(R2)未定稿 ⇒ 子用例 `Ignore` | ⚠️ 半闭 |
| 006 药箱 UI 走查 | 003 · 004 · 005 · 42 元件库(**Complete**) | **AC-20-20 BLOCKED-BY-R9**(页可见集 + 二维几何焦点稳定性);ADR-013 假设 6 / S5 spike(**桌面侧**) | 🔴 多层挂起 |

> **本链的判读**:六件里**没有一件可判全绿**,唯一「数值轮」外的阻塞源是 **R2/R3/R9** 三条
> 前置债 + `CARRY_CAP` 一个数值。⇒ 本链**不适合作为下一个 sprint 的主线**,
> 除非先单独清偿 R2/R3 之一。

#### 2.4.6 `processing`(5 件)— 与 inventory 形成**真实依赖环**(见 §2.5)

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 配方子集与可发起谓词 | item-db 002/003(**Complete**)· 30 story-001(**Complete**)· `inventory-items/001` | ✅ **AC-18-18 的判据面已清(2026-10-20 二轮)**:`RecipeOwner` 枚举 + `Recipe.Owner` 属性 + 烘焙读取面**均已存在**;数据表经补口后 `owner` 分布 = `{process:2, craft:1, build:1}` ⇒ 三子集**均非空**,三段判据(非空 / 并 / 交)**两段皆可判且已证伪判别力**。⚠️ **本 story 主体仍未开工**(`unity/Assets/Sim/Processing/` 目录不存在)—— 已落地的只有 AC-18-18 一项的判据与夹具。详见 **§七 S-2** | ✅ 判据面可闭;主体待开工 |
| 002 F1 调用契约与修饰输入 | 001 · item-db 003(**Complete**)· item-db 011(**Complete**)· 24 侧只读接口(桩) | AC-18-10b **EXTERNAL·BLOCKED-BY-42**(面板未交付) | ⚠️ 半闭 |
| 003 `Craft` 载荷 / 点火 / 落流 | 001 · 002 · 004 · `inventory-items/002` | AC-18-19 **EXTERNAL·BLOCKED-BY-21a(O-18-R3)**(守恒式两处不同形,真修归 21a) | ⚠️ 半闭 |
| 004 炉火时序与无过程状态 | 003 · 001 · `inventory-items/001,002` | AC-18-15 跨平台 **EXTERNAL·BLOCKED-BY-ADR-012** | ⚠️ 半闭 |
| 005 完成调度 / 溢出 / 音频 | 004 · 003 · `inventory-items/001,004` · 44(**Complete,直连**) | 走查三条 **EXTERNAL(42)**;`OQ-18-9`(器具状态渲染形态)未裁 ⇒ 只断 18 侧两输出 | ⚠️ 半闭 |

#### 2.4.7 `tutorial-onboarding`(5 件)— 尾部一件硬阻

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 内容 schema 与烘焙校验 | ADR-014 管线 · `entities.yaml` · 9/44/13 词表 `.cooked`(缺表报「未就绪」不崩) | 无 | ✅ 可闭 |
| 002 只读投影与装配守卫 | 001 · 三流只读快照(既有) | 联机各玩家投影独立 → **BLOCKED-BY-45**(双 id 夹具可先行) | ⚠️ 半闭 |
| 003 锚点完成触发引擎 | 002 · 001 | 同上 **BLOCKED-BY-45** | ⚠️ 半闭 |
| 004 三媒体调度与纸近景模态 | 003 · 001 · 42 近景模态(`ModalId.PaperCloseup48` **已注册**)· 44 字幕管线 | AC-48-09 **BLOCKED-BY-45**;字幕 AC-44-15 的 **48 口述半边 NOT-RUN**(缺口哨兵在) | ⚠️ 半闭 |
| 005 零门槛 / 零键位提示守卫与走查 | 003 · 004 · 39 story-004 | 🔴 **`OQ-C4`(三媒体节奏 spec)未补** ⇒ 走查 `BLOCKED-BY-SPEC`。实测:`uk/tutorial-48.md:273` 与 `paper-closeup-48.md:300` 均明写「**不代 48 拍时序**,归 48 实现轮」⇒ spec 确实不存在 | 🔴 **硬阻** |

#### 2.4.8 六个尾件(不成链)

| 件 | Type | 依赖 / 阻塞 | 可闭 |
|---|---|---|---|
| `medical-consequences/story-004` | Integration | `story-003`(世界流事件在档)· 13/42/44 呈现通道(并行 epic)· 37 三案链夹具 | ⚠️ 半闭(数值走查用相对次序) |
| `patient-ai/story-005` | Visual/Feel | 🔴 **外部美术**(真人病人模型) | 🔴 **不可由 agent 闭** |
| `emergency-procedures/story-008` | Visual/Feel | 🔴 **外部美术**(处置动作资产);素材缺失时 story-015/016 为 Not-Run 面 | 🔴 **不可由 agent 闭** |
| `world-ecozones/story-005` | Visual-Feel | 🔴 **桌面走查 + 关卡工具 CI 产物**;AC-6-25/26 **EXTERNAL** | 🔴 **不可由 agent 闭** |
| `skeuomorphic-ui/story-019` | UI | `019-b` 判据已冻结,**门待接线** | ⚠️ 部分可闭 |
| `save-slot-ui/story-001` | UI | 无列出依赖;`design/ux/save-slots-7b.md` **在场**;⚠️ 但上游 7a 槽位契约零落点 ⇒ **G-4** | ⚠️ 半闭(卡面结构上写不出来,见 §十一 C-3) |

### 2.5 真实依赖环 —— 🔴 必须用契约桩破环(承已合并的 backlog §3)

**findings:存在一条 strict cycle。** 三份 story 的 `Depends on` 互相咬合:

```
inventory-items/story-001  ──►  processing/story-003   ("两支上游事件的产出形状")
        ▲                                │
        │                                ▼
processing/story-001  ◄──────── processing/story-003  ("Story 001(准入)")
        │
        └──► inventory-items/story-001  ("InventoryOf 投影,取实例集")
```

逐边原文(可复算:`grep -o "Depends on:[^|]*" <file>`):

- `inventory-items/story-001` → `… foraging Story 003 / processing Story 003(两支上游事件的产出形状)`
- `processing/story-003` → `… Story 001(准入),Story 002(F1 出参)…`
- `processing/story-001` → `… inventory-items Story 001(InventoryOf 投影,取实例集)`

**为什么这不致命**:三条边依赖的**都不是行为**,是**数据形状**(载荷 struct / 投影签名)。
⇒ 破环手段 = **契约先行**:
1. 先落 `Craft` / `ResourceHarvested` / `DropSpawned` 的**载荷 struct** 与 `InventoryOf` 的**签名**(纯 `Sim.Contracts`);
2. `inventory-items/001` 用**合成流**验证 fold 本体(不依赖 `processing/003` 的实现);
3. `processing/001` 用 `InventoryOf` 的**桩**取实例集;
4. 两侧都落地后,在 `inventory-items/005` **合并联测**。

**⇒ 排期含义**:这三件**不能严格串行**,必须**同批**或**跨批成对**开。
若按「一件一 sprint」串行排,会在第 1 件就卡死。(§6.5 的 sprint-21「物品流转簇」即由此而来。)

**另有一组交叉但不构成环的咬合**(同样须同批考虑):
`foraging/005` → `inventory-items/003` → `inventory-items/001` → `foraging/003`(→ `foraging/002` → `001`)。
`foraging/001` 不反向依赖 inventory ⇒ 无环,但**`inventory-items/001` 必须等 `foraging/003` 落地**。

---

## 三、★ 八道硬门(最高杠杆 —— 清一道解锁一整片)

> **这一节是本次实测最重要的产出。**
> **2026-10-20 普查后由「四道」改「七道」** —— G-5 / G-6 / G-7 是 §八 #3′ 普查(原件
> `production/qa/evidence/census-adr-obligation-2026-10-20.md`)的产出,与 G-1 / G-2 / G-4 **同族**。
> **2026-10-20 取件轮再由「七道」改「八道」** —— **G-8** 是取件 R-4 途中撞见的,
> 与 G-1/G-2/G-4/G-5/G-6/G-7 **同一失效形态,但符号源不同**:前七道的符号取自 **ADR**
> (代码块 / 散文),**G-8 取自 GDD 的 `O-<sys>-<n>` 义务登记** —— 一个**从未被扫过的第三源**
> (见 §十一 **C-9**)。⚠️ **它证明 §八 #3 的「结案」不能读成「门清单已完备」**:
> 普查当天结的案,**同一天**就因换一个符号源而再出新户。
> 八道门中有**七道不在剩余 44 条里** —— 它们挂在**已标 `Complete` 的 epic 之下**,
> 是「epic 关了但契约没落」的形态。不清它们,29 / 18 / 7a / 7b / 1 / 2 / 27 / 52 / **21a** 的整片 AC 永远不可勾。
>
> ⚠️ **2026-10-20 补建 `save-slot-ui/EPIC.md` 时新揭 G-4** —— 它与 G-1 / G-2 **同族**
> (ADR 已裁 · GDD 已写 · epic 已标 `Complete` · 代码零落点),
> 但**不在前一轮普查的扫描面内**:上一轮只扫 §三 已登记的三道,
> **`Checkpoint` / `Writable` / `Gating` 这几个符号从未进过扫描列表**。
> ⇒ **G-4 触发 §八 #3,普查于同日跑完,产出 G-5 / G-6 / G-7**(对照原件 §3.4「够格判据」)。

| # | 门 | 谁在等 | 实测(2026-10-20) | 挂牌 |
|---|---|---|---|---|
| **G-1** | `SelfLimited(entity, d)` **逐实体**判死谓词 | `29` 的 `AC-29-01/02/05/08/10/17`(6 条)· `25` 的 `AC-25-6-08` · `29` 全 epic 的 `BLOCKED-BY-9` 根因 | `unity/Assets/` 全树(排除注释)**零命中**。存在的是**疾病级** `Curve.SelfLimit` 标志(`RegistrySchema.cs:107`)与 F1 求值(`ProgressionEvaluator.cs:220`)**—— 与「逐实体谓词」是两件事,不得互顶** | ✅ **`TD-sim-01`**(2026-10-20 登记)<br>🔁 **2026-10-22(sprint-18 收口):本门未清** —— S18-1 结转 sprint-19(用户裁定),`TD-sim-01` 保持 `open`。⚠️ **§4 的「`death-respawn` 全链 `BLOCKED-BY-9`」不变** |
| **G-2** | `Folded(p)` 折叠谓词 | `29` 的 `AC-29-16` · 7a 全链折叠面 | 全树 `bool Fold` / `FoldEligible` / `CanFold` / `ShouldFold` **零命中**(`CasebookScreen.IsFolded` 同名巧合,非此物) | ✅ **`TD-persist-01`**(2026-10-20 登记)<br>🔁 **2026-10-22(sprint-18 收口):实现已落、本门不主张结案** —— S18-2 `done`(四条件谓词 + 折叠行产出 + 29 测全绿),但 `FoldedPredicate` **零生产调用点**(折叠未进存档路径)、~~`OQ-7a-10` 未裁~~ 🔁 **2026-10-24 已裁(裁定 A:条件① 由 ∃ 收紧为 ∀)**、折叠产出面无变异反证。⇒ **§4 的 `AC-29-16` `BLOCKED-BY-7a 折叠实现` 保持**,~~三项~~ **现剩两项**(零生产调用点 / 产出面无变异反证)同时满足才可转可判 |
| ~~**G-3**~~ | ~~`Recipe.owner ∈ {process, craft, build}` 字段~~ | ~~`18` 的 `AC-18-18`~~ | ❌ **本条判定已证伪,不成立** —— 字段**存在**:`Sim.Contracts/ItemDatabase/Recipe.cs:41` 的 `RecipeOwner Owner` + `RecipeOwner.cs:9` 枚举 + `CookedCodec.cs:181` 烘焙期读取。**原判错因 = grep 目录限定成 `Sim/ItemDatabase/`,而字段住 `Sim.Contracts/`**;且今日更早的 `42ab88c`(S-2)已订正判据 + 补真表 + 三转红证伪 | ❌ **不登记**;错因留档于债册 §「同批被证伪的一条」 |
| **G-4** | **7a 槽位契约的高层面** —— `Checkpoint(SaveSlot)` / `Load(SaveSlot)` / `Writable` / `Loadable` / `Gating` | `7b` 的 `AC-7b-01`(唯一写入 API)· `AC-7b-02` / `AC-7b-10`(两投影全部来自 7a)· 7a 自身的 `AC-7a-19`(读档即锁 / 载入安全上下文门) | **零落点**(2026-10-20 实测,详见下表)。`ISaveService.cs:20-28` 只到 `SaveBytes` / `LoadBytes`,其 doc 自陈「高层方法（Checkpoint/SaveOnExit/Load）归后续 story」;`SaveSlot` 只有 `SlotSeq` 一个字段,无头信息 | ✅ **`TD-persist-02`**(2026-10-20 登记,用户裁定「G-4 也登记 TD,不立 story」)<br>🔁 **2026-10-25 清偿回写(§6.4 义务 · S19-5)**:✅ 门已清 —— `TD-persist-02` + `TD-persist-01` 剩余两项 → **S19-1**(commit `554e2cc`;公开方法集 / 投影 / 折叠接线 / `AC-7a-19` 四子判据落盘)。证据 = `production/qa/evidence/impl-td-persist-03-replay-and-body-2026-10-24.md`(含 S19-1 面)。**本行描述栏的「零落点」为登记时点史实,原文不删。** |
| **G-5** | **`IPlayerMotor`** 契约(ADR-020 §Key Interfaces:`Tick` / `Cell` / `Position` / `ConsumeCellChanged`) | `1` 玩家控制器(**自己的 story-006 点名**)· `2` 相机(`story-003` / `story-001` 点名,以其为只读上游) | **零落点**(2026-10-20 普查)。`PlayerController.cs` 是 `MonoBehaviour`,只有 `Move` / `Teleport` / `GetCell` / `OnTickEdge` 等,**无 `Position` / `Cell` / `ConsumeCellChanged` 成员**;旁证 = `AnchorFollower` 在出货代码里为孤儿(已记于 `review-camera-viewpoint-story-003-td-2026-10-03.md:68`) | ✅ **`TD-player-02`**(2026-10-20 普查登记,用户裁定「能归 epic 的登记 TD,不立 story」)<br>🔁 **2026-10-25 清偿回写(§6.4 义务 · S19-5)**:✅ 门已清 —— → **S19-2**(commit `bd4e6a4`;`IPlayerMotor` 契约 + 委托 + 证伪 F1-F3 全红)。残余 `TD-player-03`(双实现收敛)**另条不并账**。证据 = `impl-s19-2-player-motor-2026-10-24.md`。**「零落点」为登记时点史实,原文不删。** |
| **G-6** | **27 的 sim 侧形状** —— `LogiPose`(`(WorldPos Cell, byte Facing)`)· `ITameable` · `TameState` | `27` 敌人 AI 的 `story-003/004/005` 与 `EPIC.md` 逐条点名;`ITameable` / `TameState` 由 ADR-016 §七「**P0 只定形状**…P0 交付:接口 + 数据字段占位」定 | **全树零命中**(含 `Tests/`)。⇒ 27 的「决策进 sim」这条承重裁决(ADR-016 §二/§九)在代码里**无载体** | ✅ **`TD-enemy-01`**(2026-10-20 普查登记)<br>🔁 **2026-10-25 清偿回写(§6.4 义务 · S19-5)**:✅ 门已清 —— → **S19-3**(commit `ff853fc`;`LogiPose`/`ITameable`/`TameState` 落 `Sim.Contracts` + `EnemyLocomotion.Advance` F-27-4 权威形态 + 证伪 M1'–M8)。证据 = `impl-s19-3-logi-pose-2026-10-25.md`。**「全树零命中」为登记时点史实,原文不删。** |
| **G-7** | **`DeferredThreatSlot`** 状态量(ADR-007 §三/§四:进流 `ThreatDeferred` / `ThreatDeferralCleared`) | `52` 的 `story-004`(整条 story 名即 `density-budget-and-deferred-threat-slot`)· `story-002` · `EPIC.md` | **全树零命中**。⇒ 52 的「延迟威胁须进流(否则主机迁移静默丢失 / 重复补发)」这条核心不变量**无实现** | ✅ **`TD-randomevents-01`**(2026-10-20 普查登记)<br>🔁 **2026-10-25 清偿回写(§6.4 义务 · S19-5)**:✅ 门已清 —— → **S19-4**(commit `f398cb2`;`DeferSlots` 流态载体(唯一写入路径 = 折叠)+ 写者 + `ValidateReconstructable` 假断言撤换 + 门豁免同批撤 + 证伪 M1–M9;含迁移夹具「同段流重放两次逐元素相同」)。证据 = `impl-s19-4-deferred-threat-2026-10-25.md`。**「全树零命中」为登记时点史实,原文不删。** |
| ~~**G-8**~~ 🔁 **已结 2026-10-21** | **`O-24-1` 的落点** —— 21a 家具目录扩展 `type` / `tier` / `e_env` 三字段(`FURN_TYPES` 闭集由此派生) | `24` 的 **`AC-24-08`**(FURN_TABLE 字段缺失/越界 ⇒ 构建失败)· **`AC-24-12`**(`ROOM_TABLE` 子句 `type ⊆ FURN_TYPES`)—— 亦即 clinic-machine 全链 001→005 | **全库零落点**(2026-10-20 取件轮实测):`FURN_TYPES` 只在 `clinic-machine.md` **散文**里出现(6 处),**无任何定义**;`assets/data/item_database_items.json` 的 items **无 `type`/`tier`/`e_env` 字段**(`category:"build_part"` 仅 `wooden_post` 一行);**21a epic 已标 `Complete ✅ 12/12`(2026-09-25),而 12 份 story 里认领 `O-24-1` 的 = 0**。⚠️ **义务本身双向登记在册**(`clinic-machine.md:301` **与** `item-database.md:859`)—— 登记了,没人接 | ✅ **`TD-clinic-01`**(2026-10-20 取件轮登记,用户裁定「维持 GDD `:297` FURN_TABLE 归 21a · 不给已 Complete 的 21a 开新 story」)<br>⚠️ **`clinic-machine/story-001` 的 `AC-24-08` / `AC-24-12` 半边挂 `BLOCKED-BY-O-24-1`,本体同批交**<br>✅ **2026-10-21 已结(S17-4 家具目录落地轮)** —— `FURN_TYPES` 闭集现有生产载体:`assets/data/item_database_furniture.json`(作者态)+ `Editor.Tools.Bake/ItemDatabaseFurniture{Binder,CookedWriter,Bake}.cs` + 产物 `unity/Assets/DataCooked/item_database_furniture.cooked.bytes` + 24 侧 `Sim/Clinic/ClinicFurnitureCatalogue.cs` / `ClinicRoomExtraction.Extract(view, furnTypes)`。`clinic-machine/story-001:52` / `:55` 两条挂标**已解除并勾选**。原件 `production/qa/evidence/impl-s17-4-furniture-catalogue-2026-10-21.md`。**本门自此退出 §3.1 链门集 ⇒ 链门 = G-1 · G-2**(承 §6.4「某道 G-x 门清掉 ⇒ §3 划掉」)。⚠️ 上行「全库零落点」**原文不删** —— 它记的是 **2026-10-20 取件轮**的实测,历史有效 |

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

**共同形态(仍成立的六道)**:六道都是「ADR 裁了、GDD 写了、epic 关了、代码里没有」,
且**在归属 epic 自己的卡面里完全看不见** —— `disease-simulation/` 全 7 条 story 对 `SelfLimited` 零命中;
`persistence-service/` 两条 story 对 `Folded` 零命中、对 `Checkpoint` / `Writable` **同样零命中**
(见 §八 #3);而下游(29 / 25 / 37 / 53 / **7b**)各有引用自陈。
⇒ 形态 = **记在别人的账上,自己的账上看不见**。
> ⚠️ **G-5 / G-6 / G-7 的判别证据强一档**:不仅「归属 epic 全 `Complete`」,而且**该 epic 卡面
> 自己点名了那个符号**(原件 §3.4)。这比 G-1 / G-2 / G-4 更硬 —— 后三者的归属 epic 是**零点名**的。

> **✅ 2026-10-20 结账:六道全入册**(用户裁定「登记 TD,不立 story」;普查轮再裁「能归 epic 的登记 TD」)
>
> | 门 | TD | 归属 epic(均标全 `Complete`) | 触发条件 |
> |---|---|---|---|
> | G-1 | `TD-sim-01` | `disease-simulation`(7/7) | `death-respawn` 任一 story 开工前 / 9 下一轮动 F4 或 `StateMachine` |
> | G-2 | `TD-persist-01` | `persistence-service`(2/2) | `death-respawn/story-003` 开工前 / 7a 下次动 `Sim.Codec` |
> | G-4 | `TD-persist-02` | `persistence-service`(2/2) | **`save-slot-ui/story-001` 勾 AC-7b-01/02/10 之前** / 7a 下次动 `Sim.Codec` |
> | **G-5** | **`TD-player-02`** | `player-controller`(6/6)+ `camera-viewpoint`(6/6) | 1 或 2 的下一轮动到玩家位移 / 相机锚点时 |
> | **G-6** | **`TD-enemy-01`** | `enemy-ai`(5/5) | 27 具备可跑行为程序之前(否则决策无进 sim 的载体) |
> | **G-7** | **`TD-randomevents-01`** | `random-events`(6/6) | 52 下一次动延迟威胁 / 主机迁移语义时 |
>
> 🔁 **2026-10-25(S19-5 回写,原文不删)**:上表 G-4…G-7 四门的**触发条件均已触发并偿付**
> (S19-1..S19-4,commit `554e2cc` / `bd4e6a4` / `ff853fc` / `f398cb2`;逐门证据见 §3 门表同日加注)。
> G-1(`TD-sim-01`)与 G-2(`TD-persist-01`)**不在 sprint-19 判据面** —— G-1 结案与否归
> `death-respawn` / QA 侧核(其「生产侧零构造点」陈旧陈述已于同日在债册加注订正:`519700c`
> 已落生产构造点,`CompositionRoot.cs:311` / `:336`);G-2 的债条本体 `TD-persist-01` 已
> `closed`(债册十六订 2026-10-24,剩余两项随 S19-1 落盘),**门面**(零生产调用点 /
> 产出面变异反证)的翻面归其归属轮,本注不裁。表保留为登记史。
>
> ⚠️ **入册 ≠ 已还**:六道的 AC 判据**一条都仍不可达**。**`§八 #3` 的普查已跑完并结案(2026-10-20)**
> —— 三项未清项(孤儿契约 · 未溯源桶 · 零代码改动声明)**全清**,门清单据实由四道扩为七道。
> ⚠️ 但**结案 ≠ 完备**:C-7 已证 G 型手法对「无 epic 的契约」结构上不可见,
> **再出现新户仍触发 §八 #3**。**G-4 的入册本身即证明了普查的必要性**:
> 它是在**第 31 个 epic 的补建过程里**偶然撞见的,不是扫出来的。

> **另有一类(不占 G 号,因无归属 epic):孤儿契约**
>
> | 契约 | 义务级 | 归属系统级 | 实测 | 挂牌 |
> |---|---|---|---|---|
> | `IReplayPipe` / `ReorderBuffer` | **P0**(ADR-001 `:167` / `:448` 逐字「P0 预埋 / P0 写」) | **P1b**(`systems-index.md:81` 系统 45,**无 epic**) | **八个抽象点落 7 个,它是唯一零落点**(其余 7 个的接口声明全在 `Sim.Contracts/`) | ✅ **`TD-net-01`**(2026-10-20 第二轮专扫登记) |
>
> **为何单列**:G-1…G-7 的判别前提是「归属 epic 全 `Complete`」;孤儿契约**连 epic 都没有**
> ⇒ **G 型手法结构上够不到它**(§十一 C-7)。**这是「P0 义务 · P1b 归属 · 无 epic」的三重错位**。
> `ISceneRouter`/`IWorldSpawner` 与 `IPerTargetIl2CppArgs` 经查**均有显式跟踪面**
> (`BootRoot.cs:20` / ADR-023 §Validation S2 `⏸` / ADR-012 未勾项)⇒ **不同级,不登记**。

⇒ **处置(用户裁定 2026-10-20「不立 story」)**:两条已登记入
`docs/tech-debt-register.md`(`TD-sim-01` / `TD-persist-01`),**不补立 story**。
理由:两个 epic 均已标全 `Complete`,补立会把计数从「全绿」打回「未全绿」、扰动 M3 分母;
而两条实现面各只一个谓词,随触发条件并入下一个动该 epic 的轮次即可。
⚠️ **不立 story ≠ 不还** —— 两条各带具名触发条件。

### 3.1 门的两类角色(链门 / AC 门 —— 排期的关键判读 · 承已合并的 completion-plan §一·3)

**八道门不是一个同质的清单**。按「清一道解锁什么」分两类,**排期含义完全不同**:

| 类 | 门 | 清一道解锁什么 |
|---|---|---|
| **链门** | ~~**G-8**~~(`O-24-1` 家具目录扩展)· **G-1 / G-2**(`SelfLimited` / `Folded`,同在 29)<br>🔁 **2026-10-21 订正**:G-8 已结(S17-4)⇒ **链门现 = G-1 · G-2**(G-8 原文不删) | **整条链的剩余件** —— ~~G-8 → `clinic 004/005`(+`001` 那两条挂标 AC)~~ **已清偿**(两条 AC 已勾、`clinic 004/005` 的解锁前置已满足);G-1 → `death-respawn` 全 6 件 + 29 的 7 条 AC + 25 的 `AC-25-6-08` |
| **AC 门** | **G-4**(7a 槽位契约)· **G-5**(`IPlayerMotor`)· **G-6**(27 sim 侧形状)· **G-7**(`DeferredThreatSlot`) | **已标 `Complete` 的 epic 的 AC** —— 这些 epic **零剩余 story**(7a/7b · 1,2 · 27 · 52) |

> **AC 门直接等于 M3 Exit 的第二条合取**(§五:「该 epic 名下 TD 的 P0 面 = 0」)。
> ⇒ **八道门不是可选工作,它就是 M3 的出口条件。** 不清,`audio-system`(21/21 全 Complete
> 但 EPIC 自陈 ⛔ 不得收口)/ `telemetry-analytics`(10/10 全 Complete 但 `TD-epic-01` 登记
> 留债 )**永远闭不了** —— 而 `milestones/README.md` 的原判据(单条「epic 全 Complete」)**看不见这一点**。
>
> ⚠️ **两类门的工作形态也不同**:链门是**实现**(补谓词 / 补字段),AC 门里 G-4/G-5/G-6/G-7
> 多为**接口形状补落**(三个已由普查认定「归属 epic 卡面自己点名过」)。
> 但**形态差异不改变清偿载体** —— 八道统一走 **TD 工作项**(§5.1)。

---

## 四、批次划分(静态)

**划分依据 = 阻塞源**,不是工作量,不是日期。

### 批 A —— 无外部阻塞,立即可推

**A-1 · 全 AC 可勾(卡面零 `BLOCKED-BY` 标记 **且** 上游契约逐代码实测存在)**

| 件 | 卡面挂标数 | 上游契约实测(2026-10-20) |
|---|---|---|
| `clinic-machine/001` | 0 | ⚠️ **R-4 部分通过(6/7 AC 面)** —— ADR-014 两阶段基建齐(`Editor.Tools.Bake/` 10+ `*Binder` / `*CookedWriter` 先例);`O-24-4` 常量在(`item_database_constants.json: env_mod_min="-1/2"`);`O-24-5` 参数**已裁**(`n_max=64`,2026-10-07)。**❌ `AC-24-08` / `AC-24-12` 的上游 = GDD 级义务 `O-24-1`(21a 家具目录扩展 `type`/`tier`/`e_env`),实测零落点**(`FURN_TYPES` 全仓零 · `item_database_items.json` 无三字段 · 21a epic **Complete 12/12 却零 story 认领**)⇒ **半边挂 `BLOCKED-BY-O-24-1`**(用户裁定 2026-10-20:维持 GDD `:297`「FURN_TABLE 归 21a」,**本体同批交**)。详见 **G-8**<br>🔁 **2026-10-21 订正(G-8 结案 ⇒ 承 §6.4「从 §4 移除对应阻塞」)**:上述 ❌ 半边**已消解** —— `O-24-1` 由 S17-4 兑现(家具目录三字段 + `FURN_TYPES` 闭集生产载体 `ClinicFurnitureCatalogue` / `ClinicFurnitureCookedCodec` / `Editor.Tools.Bake/ItemDatabaseFurniture*`),`AC-24-08` / `AC-24-12` **已解除挂标并勾选**(`clinic-machine/story-001:52` / `:55`)⇒ **本行 R-4 结论现为 7/7 AC 面**。⚠️ 本行上文**原文不删**(记 2026-10-20 取件轮状态);`clinic-001` 自身仍 `In Review`,是**另三条**现役理由 |
| `clinic-machine/002 · 003` | 0 / 0 | ✅ **R-4 已跑(取件轮)** —— `002`:23 `modular-building` **Complete ✅ 7/7**,`World.Structures`(**public**,`World.cs:26`)→ `StructureInstanceRegistry.TryGet`(`StructureKinds.cs:111`)/ `GetInstanceAt`(`:117`)⇒ `structure_at(cell)→{ModuleId,Orientation}` 可达 · `003`:`Fix` 定点域全落,`ROUND_HALF_AWAY_FROM_ZERO`(`Fix.cs:38` / `:135` / `:322`) |
| `tutorial-onboarding/001` | 0 | ✅ **R-4 已跑(取件轮)** —— 35 个 `SimEvent.Kind`(`entities.yaml`)· `tools/kindgen/kindgen.py` · `Sim/StreamRouting.g.cs` · 病名闭集 `DiseaseId` 类型(`Sim.Contracts/Payloads/CasePayloads.cs` 等) |
| `casebook/002` | 0 | ✅ **R-4 已跑(取件轮)** —— 病例流全落:`EventKind.CaseOpened` · `CaseOpenedPayload` · `PayloadCodec.Case.cs` · `CaseId` 内联全序键 `(Tick, Patient, Seq)`(`CodecWriter.cs:130`) |
| ~~`save-slot-ui/001`~~ | ~~0~~ | ❌ **移出 —— 上游 7a 契约零落点(G-4)**,**R-4 首例** |

> ⚠️ **2026-10-20 取件轮 R-4 结论(含一处自我更正)** —— 扫描面 = `unity/Assets/` 全树 C# **不剥注释** + `tools/`,
> 逐符号问同物异名(承 §十一 C-8)。
> **❌ 更正**:本轮**先**写下「A-1 五件全部通过 R-4」—— **该结论过宽,已作废**。
> 真实结论 = **4 件通过 · `clinic-machine/001` 6/7 AC 面通过**(两条半边挂 `BLOCKED-BY-O-24-1`)。
> **错因**:那次 R-4 只扫了 **story 卡面点名的 ADR**,未扫 **GDD 级的跨系统义务**(`O-<sys>-<n>`)—— 见 **C-9** / **G-8**。
> R-1/R-2/R-3 五件全绿(均 `Ready` · 零挂标 · 均为**真「从零实现」**,非补登记)。
> ⚠️ A-1 之外的 16 条零挂标件上游仍未扫(见 SP-6 / §八 #3)。

> ⚠️ **「⬜ 未跑」≠「✅ 无缺口」**(禁借绿)。A-1 判据原为单条件(卡面零挂标),**2026-10-20 改为双条件合取**
> (见下方订正块)。上表三格原本是**未测**,不是**通过** —— 取件轮已补跑,结论见上方 ✅ 块。

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
| `death-respawn` | **全部 6 件** | ⚠️ **否 —— 全链 `BLOCKED-BY-9`,见 G-1**<br>🔁 **2026-10-22(sprint-18 收口)复核:不变** —— G-1 未清(S18-1 结转 sprint-19)⇒ 本行原判**仍然成立**;`AC-29-16` 的 `BLOCKED-BY-7a 折叠实现` 亦保持(G-2 不主张结案)。 |
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

### 4.4 外部阻塞清单(五类 E-1…E-5 · 承已合并的 backlog §4)

> **本节与批 C 的关系**:批 C 列的是**件**,本节列的是**阻塞源** —— 同一批事实的两种切法。
> **五类解除前,相关 AC 永远绿不了**,无论做得多完整。

| # | 外部阻塞 | 实测证据 | 卡住的 AC(点名) | 解除者 |
|---|---|---|---|---|
| **E-1** | **数值轮未冻结**(用户裁定) | `assets/data/` 下**无 `disease_registry.json`**;`CompositionRoot.cs:609-625` 用**占位 plateau 曲线**;`entities.yaml:1780` 的 `CARRY_CAP` = `待定` | `AC-20-09`(硬不变量无值可验)· `AC-17-13`(`GATHER_MUL_CAP`)· `AC-44-02/03/04/08/17` 听测半边 · `TD-listen-01` 的 **L-3 / L-4** | **用户** |
| **E-2** | **ADR-012 三格黄金矩阵不完整** | `tests/golden/` 仅 `medcons/` + `telemetry/`;**CI 的 Linux-x64-IL2CPP job 从未跑过**(未配 `UNITY_CLIENT_ID/SECRET`,job 注释自称「从未跑过」);**Linux-ARM64 格在本 pinned 工具链下物理不可建**(46 个 `BuildTarget` 中零个含 arm) | `AC-17-02` · `AC-18-15` · `clinic-machine/005` 跨平台对拍 | **CI 凭证 + 一次 ARM64 格的用户重裁**<br>🔁 **2026-10-21**:本行三件事性质不同,已拆为 **E-2a / E-2b / E-2c**(见 **§4.5**);其中 E-2a(CI 常驻)经用户裁定为 **EXTERNAL · 无法完成**,已入债册 `TD-ci-01` |
| **E-3** | **45 联机层无 GDD(P1b)** | 全库 `BLOCKED-BY-45` 命中 **36 处 / 16 个文件**(`grep -ro "BLOCKED-BY-45" production/epics/ \| wc -l`);其中 3 处在 `EPIC.md`(audio / prescription / tutorial) | `AC-39-04③` · `AC-48-09` · tutorial 002/003 的联机半边 · `casebook/006` 运行面 | P1b 轮 |
| **E-4** | **外部美术 / 桌面走查** | `patient-ai/story-005`(病人真模型)· `emergency-procedures/story-008`(动作资产)· `world-ecozones/story-005`(桌面 + 工具 CI) | 三件尾 story 本体 | **外部 / 用户桌面** |
| **E-5** | **42 呈现侧未交付的载体** | `processing` `AC-18-10b`(面板)· `foraging` `AC-17-07b/13b`(三类失败反馈) | 4 条 [L] 走查 | 42 + 对应 epic |

> ⚠️ **E-2 的一条独立结论(须用户重裁,不是排期问题)** —— `ADR-012 §Validation` 的
> **Linux-ARM64 行不是「未跑」,是「跑不了」**:本 pinned 工具链的 46 个 `BuildTarget` 里
> 零个含 arm ⇒ **该格在当前工具链下结构性不可达**。出路只有两条(**均须用户裁**):
> ① 把该格改为「**不要求**」并在 ADR-012 就地加注;② **换工具链**(须重跑整个矩阵的逐位性论证)。
> ⇒ **不得**把它当「待排期项」留在表里 —— 它是一处**裁决缺口**,不是一处**工作量缺口**。

#### 4.5 E-2 已拆成三半(2026-10-21 · 用户裁定「CI 常驻 job 无法完成,登记到项目规划」)

> ⚠️ **标题原写「两半已分家」,与正文不符** —— 正文实际拆的是 **E-2a / E-2b / E-2c 三半**
> (`E-2c` = `tests/golden/` 覆盖窄)。2026-10-21 就地订正标题,原文保留于本注(承「原文不删」)。

**E-2 此前是一团** —— 它把三件不同性质的事捆在一个编号下:①CI 常驻 job 从未跑;
②ARM64 格不可达;③`tests/golden/` 覆盖窄。三者的**解除者不同、性质不同**,捆在一起会导致
「某一半解决了就以为 E-2 解决了」。**本节就地拆开,原 E-2 行原文保留作沿革。**

| 半 | 状态 | 性质 | 解除者 |
|---|---|---|---|
| **E-2a · CI 常驻 job** | ⛔ **无法完成(用户裁定 2026-10-21)**<br>🔁 **2026-10-21 补**:本项实为**三段**(A workflow 结构缺陷 / B 凭证 / C 首跑),`EXTERNAL` 只覆盖 **B / C**;A 段 **agent 可修但裁定不做** ⇒ 见 §4.5.1 补 | **凭证缺口**,不是工作量缺口<br>🔁 **补**:归因拆两半 —— A 段是**结构缺陷**、B 段是**凭证缺口** | 见下 §4.5.1 |
| **E-2b · ARM64 格** | ⛔ **结构性不可达**(2026-10-17 已裁) | **裁决缺口** | 见上「E-2 的独立结论」 |
| **E-2c · `tests/golden/` 覆盖窄** | ⚠️ 仍开 | 工作量缺口 | 对应 epic 落地时补 |

##### 4.5.1 E-2a:CI 常驻 job —— 登记为「无法完成」,不再挂账为「待跑」

**用户裁定(2026-10-21)**:**CI 常驻 job 无法完成,登记到项目规划。**

**裁定依据(实测,非推断)**:

| 判据 | 实测 |
|---|---|
| workflow 已建 | ✅ `.github/workflows/unity-tests.yml`(3 个 job:`editmode-tests` / `playmode-tests` / `il2cpp-determinism`) |
| 三个 job 全部 **secret 门控** | ✅ 每个 job 首步都是 `if [ -z "$UNITY_CLIENT_ID" ] … then exit 0` ⇒ **未配凭证时静默 skip,不是失败** |
| 仓库未配凭证 | ✅ `UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET` 均未配置 |
| **凭证从哪来** | Unity 账号的 CI 服务账号授权(`unity auth login --client-id/--client-secret`)—— **须用户侧申请**,agent 侧**无任何路径可取得** |
| 附加未实测项(即使有凭证也未必能跑) | ① 镜像 tag `unityci/editor:ubuntu-6000.3.24f1-linux-il2cpp-3` 是否已发布(game-ci 对新版本有滞后);② 容器内 `-runTests` 是否需额外 license 挂载参数 |

**⇒ 定性:这是 EXTERNAL(承 §〇·一 `CAPACITY-MODEL` 的三分类 —— 「依赖项目外部件,**agent 侧无法解除**」),
不是「排期没排到」。**它此前被反复写成「从未跑过 / 仍开」,那种写法暗示「排上就能做」,
**会误导下一轮取件**。本节把它钉死为**无法完成**。

> ⚠️ **上句的出处引用有误(2026-10-21 就地订正,原文保留不删)**:三分类在**本文件 §〇·二 第 3 条**,
> 不在 §〇·一;而 `CAPACITY-MODEL.md` 讲的是**批次容量**(§3.1),不是这三类。**结论不变** ——
> `TD-ci-01` 的性质仍是 EXTERNAL;变的只是这句注脚指向的节号。

**登记后的三条硬边界(禁借绿)**:

1. **`ADR-012 §Validation` 的「CI 常驻矩阵」项维持未勾** —— 不得因本节登记而勾选,
   也不得因「桌面两腿已跑通」而勾选。桌面证据补的是**判据**,不是**常驻性**。
2. **`AC-17-02` / `AC-18-15` / `clinic-machine/005` 的跨平台对拍半边** —— 维持
   `EXTERNAL · BLOCKED-BY-ADR-012` 口径,**不因桌面两格已跑而解除**。
   ⚠️ 但**已可引用桌面证据**:Linux-x64-Mono `3/3` · Linux-x64-IL2CPP `3/2/0/1` + 外部 diff
   19/19 逐位相同(`production/qa/evidence/c4-il2cpp-matrix-2026-10-21.md`)。
   ⇒ 口径应为「**判据已由桌面轮证得,常驻性未兑现**」,**不是**「跨平台确定性未验证」。
3. **不得把 CI 常驻写成 sprint 的取件项** —— 它是 EXTERNAL,不是 backlog。
   若将来用户提供凭证,应作为**一次性的基础设施动作**恢复,而不是塞进某个 sprint 的 Must Have。

**复活条件(唯一)**:用户提供 `UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET` 并完成一次成功的
`workflow_dispatch` 运行。在此之前**本节结论不变**,任何下游件不得写「CI 常驻已就绪」。

---

###### 4.5.1 补:2026-10-21 代码级勘察 —— 本项是**三段**,`EXTERNAL` 只覆盖两段

> **立此补的缘由**:上方把整件事钉成「EXTERNAL · 无法完成」,**混淆了不同因的两件事**
> —— ① workflow 自身的**结构缺陷**(agent 侧可修)与 ② **凭证缺口**(用户侧)。
> 上方原文**全部保留不删**(它记的是定性那一刻的依据),本补**只增不改**。

| 段 | 内容 | 谁能做 | 状态(2026-10-21) |
|---|---|---|---|
| **A** | workflow 结构缺陷修复 | **agent** | ❌ 未做 —— 用户裁定「**不做,登记**」 |
| **B** | Unity 服务账号凭证 | **只有用户** | ❌ 未申请 |
| **C** | 首次 `workflow_dispatch` 验证 | 依赖 B | ❌ 未跑 |

⇒ **`EXTERNAL` 只对 B / C 成立**。A 段性质是「**可修但裁定不做**」,
**不是**「agent 侧无法解除」。**本项的结论不变,变的是归因。**

**A 段实测出的六个硬缺陷**(全部 `file:line` 可复算;文件 = `.github/workflows/unity-tests.yml`):

| # | 缺陷 | 落点 | 后果 |
|---|---|---|---|
| ① | `if: env.UNITY_CLIENT_ID != ''` **恒假** —— 该变量只写在 `Unity CI Login` **这一个 step 的 `env:`** 里(`:30-32` / `:67-69` / `:122-124`),`if` 却在**后续 step**(`:41` / `:78` / `:136` / `:143` / `:152`)。Actions 的 `env:` 是 **step 级作用域,不跨 step 继承** | 同上 | **配好 secret 也没用** —— 五个干活 step 仍全部 skip |
| ② | `ubuntu-latest` 上**没有 `unity` CLI**(本机为 `1.0.0-beta.8`,非 runner 预装),两 job **无安装步** | `:43` / `:80` | 现状走 `exit 0` ⇒ **绿但零执行**;配 secret 后 ⇒ **command not found ⇒ 红** |
| ③ | `executeMethod` 类名带 namespace,目标类**无 `namespace`** ⇒ 应为 `Ac29Il2CppSwitch.SetIl2cpp`(桌面轮实证 `c4-il2cpp-matrix-2026-10-21.md:115`) | `:139` | 一执行即「找不到方法」 |
| ④ | **登录两套体系混用**:两 job 走 `unity auth login --client-id/--client-secret`,IL2CPP 走 `unity-editor -username/-password`,把**服务账号 Key ID 当用户名** | `:131-132` | 凭证不通,IL2CPP job 登录必失败 |
| ⑤ | **对拍步三处互不匹配** —— 见下子表 | `:157` / `:163` | 该步**结构上不可能通过** |
| ⑥ | `if-no-files-found: warn` | `:174` | 产物缺失只 warn 不失败,与「禁假绿」冲突 |

**⑤ 逐项实测**:

| 行 | 写的 | 实测 |
|---|---|---|
| `:157` | 期望 `il2cpp-hashes.txt` | 测试实际落盘 **`ac29-computed.txt`**(`unity/Assets/Tests/PlayMode/determinism_golden_crossplatform_test.cs:228-230`,可由 `AC29_OUT` 改);**全库零处**写 `il2cpp-hashes.txt` |
| `:163` | `golden_v1_reference.py --check <file>` | 该脚本**无 `--check` 参数**,无 `sys.argv` 解析,`main()` 写死 `golden-v1.txt` |
| `:163` | 指向 `tests/golden/medcons/golden-v1.txt` | 该文件 **32 行**;测试要的 **19 条**在 **`tests/unit/item_database/golden/golden-v1.txt`** |
| (缺) | 未注入 `AC29_OUT` / `AC29_GOLDEN` | player 侧金标准不可达 ⇒ 测试走 `Assert.Ignore`(`:289-294`),**什么都没断言** |

> ⚠️ **NOT-RUN(如实登记)**:`-runTests -testPlatform StandaloneLinux64` 能否
> 「构建 + 运行 player 一步完成」,**本机未验证**。桌面轮走的是 `unity run` + 单独 build。
> 不得据推断写成「可行」或「不可行」。

**A 段若要修,六步**(已勘察出):① `env:` 提到 job 级 / 或 `if` 改读 `secrets`;
② 三 job 统一 `unity auth login`,删 `:131-132`;③ `executeMethod` 改 `Ac29Il2CppSwitch.SetIl2cpp`;
④ 两 job 补 CLI 安装步或统一走 `unityci/editor` 容器;⑤ 对拍步注入 `AC29_OUT` / `AC29_GOLDEN`
并改用外部 diff(照抄 `c4-il2cpp-matrix-2026-10-21.md:123`),删 `--check`;
⑥ `if-no-files-found: error`。

**用户裁定(2026-10-21,晚于上述勘察)**:**不做,规划中登记。**
⇒ A 段**保持现状**(缺陷仍在盘上),本项 `EXTERNAL` 结论不变。
⚠️ **不得**把本节读成「A 段已修」或「A 段不可修」—— 它**可修、未修**。

**同批落点**:

| 落点 | 内容 |
|---|---|
| `docs/tech-debt-register.md` | 新立 **`TD-ci-01`**(治理面 · `P0 面 = N`)—— 见该册条目 |
| `production/sprint-status.yaml` | C-4 的 `why_open` 就地加注「CI 常驻半边已由用户裁定为 EXTERNAL」 |
| `docs/architecture/adr-012-…md` | §Validation 的 CI 常驻行就地加注(原文保留) |
| `production/qa/qa-signoff-sprint-17-2026-10-21.md` | §6.2 第 4 条加注(性质由「仍开(待跑)」→ EXTERNAL;**verdict 不改判**) |
| `production/epics/audio-system/story-007/008/009/010/011/012` | 各件「IL2CPP 三格 / CI 常驻」段加注 —— **6 个文件共 7 处**(`story-010` 有 2 处:§`Out of Scope` 段 + `新增登记` 段);**均不得读成「已解决」** |
| `production/epics/clinic-machine/story-003` | 「CI 常驻矩阵」行加注 |
| `production/milestones/README.md` | **M1 的 `UNITY_LICENSE` secret 勾选为误** —— 就地加注(勾不改,但不得据此认为 CI 就绪) |

---

## 五、M3 Exit 判据的口径修正(必须改,否则假绿)

`production/milestones/README.md` 原写(**改前位于 `:371`**,S16-2 加注后为 `:370-385` 块):

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

### 5.1 落盘记录(README 的 M3 Exit ① —— 改前 `:371`,落盘后为 `:370-385` 块 · 归 S16-2)

| 处 | 现行(旧) | 落盘后 |
|---|---|---|
| `milestones/README.md` Exit ①(改前 `:371`,现 `:370-385` 块) | `- [ ] 31 个 P0 epic 全部 \`Complete\`(⚠️ 现 **22/31**)` | **两条合取**逐字改写(见上);分母刷新为 **18/31**(§1.2 真源裁决);**第二条合取标 `NOT-YET-EVALUABLE`** —— 它要求债册有「P0 面」列,而该列归 **S16-3**,尚未落。**禁标绿、禁标 `[x]`** |

**用户裁定(2026-10-20)**:门的清偿载体 = **TD 工作项** —— 在对应 sprint 内以「TD 偿还工作项」
直接实施,**不立 story、不重开已 `Complete` 的 epic**;同时给债册增「**P0 面 / 状态**」两列
(归 **S16-3**)。⇒ **M3 Exit 第二条合取自此具备了「可机械求值」的设计** —— 它本来就是 **TD 口径,不是 story 口径**。
⚠️ **但「设计了列」≠「今天可求值」**:列未落之前,该条**跑不出数**(见下行块),**禁把本裁定读成该条已绿**。

> ⚠️ **第二条合取今天尚未可判,须如实标 `NOT-YET-EVALUABLE`**:
> 债册现无状态列(状态嵌在「债」列的散文里),故「该 epic 名下 TD 的 P0 面 = 0」**今天跑不出数**。
> **禁为好看而预判为 0**(本仓的假绿禁令;同 §八 #8 的「把还没证的事写成既成事实」)。

> 🔁 **2026-10-20 已结(S16-3 收口轮)—— 原文保留不删**:上块记的是 **S16-3 落盘前**的状态。
> 列已落于 `docs/tech-debt-register.md` **§★ 总索引**(91 行,列 = `归属 epic / 状态 / P0 面`)
> ⇒ **该条现已可求值,而值 = `假`**:**72 条未闭 P0 面债,分布在 12 / 31 个 epic**。
> 求值命令与逐 epic 分布见该节 **§五**(夹在 `TD-INDEX` 标记间,与表同源,第三方可重跑)。
> 🔁 **2026-10-21 回填(用户裁定「升级为 94」)**:上述 `91 行` **现为 94 行**
> (`TD-gate-01/02/03` 三条治理债已入索引;`closed 16→19` · `N 4→7`)。
> **`72` 与 `12 / 31` 一位不动** ⇒ **M3 第二条合取的读数不变,仍 `NOT MET`**。
> `91` 为 S16-3 交货时数,原文保留。
> ⇒ **「可求值」≠「已满足」** —— 本条**不是绿**,它把 M3 的出口量**如实显示为 72 条**(未闭)。
> ~~⚠️ **分母须并列记 `72(Y,未闭) + 3(待说明)`**:4 条 = `TD-net-01`(义务级 P0 · 归属系统 45 = P1b,三重错位)·
> `TD-prescription-05`(归 49 = P2)· `TD-s13-01` / `TD-s13-03`(跨 epic 治理)—— **均待用户裁**。~~
> ✅ **2026-10-20 已裁(原文保留留痕)** —— 四条**全判 `N`**,`待裁` 桶清零 ⇒
> **分母 = 单一的 `72`,不再带 `+ 3` 尾巴**(**数一位不变**,变的是「有没有悬着的格子」)。
> 裁法与理由:`docs/tech-debt-register.md` §一 ✅ 块 + §六 尾注。⚠️ `TD-net-01` 判 `N`
> **只回答「算不算 M3 出口」,不回答「要不要还」** —— ADR-001 的 P0 预埋义务仍有效,仍以 `open` 跟踪。
> ⚠️ 另:债册真债号 **91**(基线写的 92 是全文串扫口径,多算 1 族引用 / 少算 5 个带后缀号,净 +1)。

### 5.2 进度真源:三个来源,只看一个必高估(承已合并的 completion-plan §五)

| 来源 | 今日 | 只看它的问题 |
|---|---|---|
| 剩余 story | **43**(= 44 − 已交的 `clinic-machine/story-001`,2026-10-20) | **漏掉八道门** —— 其中七道挂在已标 `Complete` 的 epic 之下,**不在剩余件里** |
| 八道硬门(§三) | **8 道**(G-3 已证伪不计) | 它们**没有 story 载体** ⇒ 任何按 story 计数的进度条看不见它们 |
| TD 的 P0 面 | 累积中(`TD-clinic-01…17` 等) | **直接等于 M3 第二条合取** ⇒ 不清,`audio-system` / `telemetry-analytics` 永不可收口 |

⇒ **M3 Exit = 三源同时归零**。**报收官进度时,三源须并列,缺一即失真**(§八 #5)。

> 🔁 **2026-10-21 补注(第三源的**盲区**实测,由 N-r2 立债提出)**:
> 第三源「TD 的 P0 面」**只数已入册的债**。实测存在一类**三源全都够不着**的缺口:
> **`modular-building` 的 N-r2(生产装配根)** —— 23 的 7 条 story 全 `Complete`(第一源看不见)、
> 不在 G-1…G-8(第二源看不见)、**此前从未入债册**(第三源看不见)、§6.5 的 sprint 表里
> `modular` 三处命中全是「拿 23 当上游前提」而非「做 23」。
> ⇒ 用户 2026-10-21 裁定**立 `TD-building-01`**(债册),**正是为了让第三源能看见它**;
> 并裁定**不写 sprint-20**,排期留给 `/sprint-plan` 现裁。
> ⚠️ **本条补注不改变三源的定义**,只登记:**第三源的完备性取决于「有人先把它入册」** ——
> 它不是一个能自己发现缺口的源。同类判据见 §八 #3 / #3′ 的「门清单完备性」教训。

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

### 6.5 后续 sprint 序列(sprint-16 … sprint-29 · 承已合并的 completion-plan §三/§四)

> **本节的定位**:**它主张顺序,不主张件集**。件集、前置、挂标、八道门的定义**全部引本文件
> §一–§五** —— 本节**不复制判据**。本节与 §二/§四 冲突时 **以 §二/§四为准**;差集须显式登记。

**判据 = 阻塞源 + 依赖链**,不是工作量、不是日期。

| sprint | 段 | 件集 | 前置 | Exit 判据 |
|---|---|---|---|---|
| **sprint-16** | **口径复位与欠账**(**非 feature** · **零 story 交付**) | **5 件**(2×L + 3×M):**S16-1** `CAPACITY-MODEL` 重标定 = 三元〔✅ 已收口 2026-10-20〕· **S16-2**〔**L**〕**三份静态件物理合并** + M3 Exit **两条合取**落盘(`README:371`)+ **分母真源裁决**(18 vs 22,见 §1.2)· **S16-3**〔**L**〕**债册增「P0 面 / 状态」两列**(**实测 92 条 / ~17 债型表**,非旧写的 82 / ~20 —— 🔁 **已订正:真债号 91 · 含 TD 的表 20 张**)〔✅ 已收口 2026-10-20:求值 = **假 · 72 条(未闭) / 12 epic**〕+ **门清偿载体**成文〔已裁:TD 工作项 —— 不立 story、不重开 epic〕· **S16-4** retro-14 **A4**(测试/工具不得写仓库的机械门)· **S16-5** **EPIC.md 4 户分母刷账** | 无 | 5 件各附**可复算命令 + 预期输出**(禁「已落盘」当判据);**S16-4 过证伪轮**(改坏生产 ⇒ 门红);S16-2 后**静态件只剩一份**(`ls` 实测);四分数同值 **226 / 182 / 44 / 18-31**。<br>**零 story 转 `Complete`** —— 本 sprint 的产物是**让后续所有判据可复算** |
| **sprint-17** | **批 A-1 收口** | `clinic-002` · `clinic-003` · `tutorial-001` · `casebook-002` | — | **4 件 AC 勾满** —— 全库**唯一**能勾满的一类(R-4 四件已跑过) |
| **sprint-18** | **链门** | ~~**G-8**~~ · **G-1** · **G-2**<br>🔁 **2026-10-21 订正**:G-8 已由 **sprint-17(S17-4)** 结案 ⇒ **本 sprint 实欠 = G-1 · G-2**(G-8 原文不删) | sprint-16 ③ | `clinic` / `death-respawn` 两链的 `BLOCKED-BY` **根因**消除;`clinic-001` 的两条挂标 AC 同步转可判<br>🔁 **2026-10-21**:后半句**已兑现** —— 两条 AC 已解除挂标并勾选(`clinic-machine/story-001:52` / `:55`);G-8 对 `clinic 004/005` 的解锁前置亦已满足(**已兑现 ≠ 004/005 已交付**)<br>🔁 **2026-10-22 收口实测:两门皆未清** —— **G-1** = S18-1 结转 sprint-19;**G-2** = 实现已落但**不主张结案**(零生产调用点 / ~~`OQ-7a-10` 未裁~~ 🔁 **2026-10-24 已裁** / 产出面无变异反证)。⇒ 本 sprint **零链门清偿**;`TD-INDEX` 未闭 P0 面 **`73 → 74`**(方向是**立债 + 结转**,不是清债)。⚠️ 规划期写的「清债 `73 → 71`」**作废** |
| **sprint-19** | **AC 门** | **G-4** · **G-5** · **G-6** · **G-7** | sprint-16 ③ | 7a/7b · 1,2 · 27 · 52 名下 TD 的 P0 面归零 ⇒ **M3 第二条合取可判**<br>🔁 **2026-10-25(S19-5 记账订正,原文不删)**:Exit 面的 **G-4 非「一条债」** = **`TD-persist-02` + `TD-persist-01` 剩余两项**(字节半随 `TD-persist-03` 并入;sprint-19 卡面 S19-1 逐字)。**四门清偿实测(2026-10-25)**:G-4 → S19-1(`554e2cc`)· G-5 → S19-2(`bd4e6a4`)· G-6 → S19-3(`ff853fc`)· G-7 → S19-4(`f398cb2`)—— 债册四条已 `closed`。⚠️ **「名下 TD 的 P0 面归零」须读窄为「四门债面归零」**:epic 名下**另条债不在门面**(persistence-service 的 `TD-persist-04` · player-controller 的 `TD-player-03` · disease-simulation 的 `TD-sim-01` 等,各有触发条件与归属轮)—— M3 第二条合取的**全局**求值仍为**假**(**71** 条 / **11** epic,债册 §五 二十订 2026-10-25 实跑),不受四门清偿影响。`save-slot-ui/story-001` 的 `AC-7b-01/02/10` 挂标随 G-4 清偿解除。§3 行回写见同日门表加注;复算命令 = 债册 §五 脚本本体(自包含)。 |
| **sprint-20** | clinic 链尾 + A-2 机制 | `clinic-004` · `clinic-005` · `foraging-001` · `processing-001` | **sprint-18(G-8)** | 本体交;AC 半边按挂标(**不得记 `Complete`**) |
| **sprint-21** | **物品流转簇**(跨链耦合簇,**不得拆**) | `foraging-003` · `processing-003` · `inventory-001` · `inventory-002` | sprint-20 | 事件载荷形状 ↔ `IIdAuthority` 铸造权威**同批冻结** |
| **sprint-22** | **容量簇** | `inventory-003` · `inventory-004` · `foraging-004` · `foraging-005` | sprint-21 | `CanCarry` 采前闸接通 |
| **sprint-23** | **death-respawn 全链**(**L 级,独占额度**) | `death-respawn-001` … `005` | **sprint-18(G-1)** | 全链 `BLOCKED-BY-9` 解除 |
| **sprint-24** | 炮制链尾 + 采集尾 | `processing-002` · `processing-004` · `processing-005` · `foraging-002` | sprint-21 | `processing-002` 的 42 走查半边仍挂 |
| **sprint-25** | 教学链尾 | `tutorial-002` · `tutorial-003` · `tutorial-004` · `tutorial-005` | sprint-17 | `002/003` 挂 `BLOCKED-BY-45`(联机运行面) |
| **sprint-26** | **呈现收尾簇**(**不得拆**) | `casebook-005` · `casebook-006` · `inventory-006` · `death-respawn-006` | sprint-19 · sprint-22 | 全部落在 42 元件库 + UI 走查面 |
| **sprint-27** | 脉案链尾 | `casebook-001` · `casebook-003` · `casebook-004` · `inventory-005` | sprint-17 · sprint-19 | — |
| **sprint-28** | 内部可推收尾 | `medical-consequences-004` · `save-slot-ui-001` | **sprint-19(G-4)** | `AC-7b-01/02/10` 转可判 |
| **sprint-29** | **批 C 外部阻塞** | `emergency-008` · `patient-ai-005` · `skeuo-019(b/d)` · `world-ecozones-005` | **外部**(美术 / UX 裁定 / 关卡工具 CI) | 素材与冻结到位才动 —— **现在做 = 自造断言** |
| **(非 sprint)** | **M4 · 内容面** | 42 VS Critical 资产 + 数值冻结 | **M3 闭环** + **外包交付节点**(唯一未闭输入) | **设计面外,须独立载体** = `production/asset-batches/`(**已建**);**含内部侧总计 ≈73.5 人日**(3 批验收已入) |

#### 6.5.4 🔁 2026-10-29 资产面插入(承 `production/milestones/p0-sweep-2026-10-29.md` §五)

> **为什么要改这一节**:上表**没有任何一行是资产面** —— 而 M3 的 Goal 逐字是
> 「能用灰盒资产走完全部路径」,该句的宾语**在盘上产出为 0**(31 项灰盒全无;
> `World.unity` = `Directional Light` + `Ground` 两个对象)。
> ⇒ 不是「资产没排期」,是**资产面从未进入本序列**。
>
> **与 `asset-batches` 裁定的关系(读准,不是推翻)**:`production/asset-batches/README.md`
> 把**真资产**工量移出 sprint,理由 = 外包侧 ≈58.5 人日 ≫ 内部侧 ≈10.5 人日(量纲不同)。
> **灰盒不是真资产** —— 它是 `art-bible §8.11.1` 定义的**临时态**(proxy mesh + 单色材质),
> **不依赖外部美术** ⇒ 属「内部可推」,**可以且必须进 sprint**。

**插入四张卡(件集增列,不改任何既有行的判据)**

| 卡 | 内容 | 落位 | 复杂度 | 与既有行的关系 |
|---|---|---|---|---|
| **`A2-1`** | `019-b` **atlas 预算门接线**(构建期「单帧同屏瓦片 ≤ 32」断言) | **sprint-26** | S | 挂在呈现收尾簇 —— 同一编译单元(`42` 元件库 + UI 走查) |
| **`A2-2`** | 42 项 VS Critical 的 **2D 子集(界面 1 + HUD 8 + 物品图标 6 = 15 项)外部批次验收槽** | **sprint-29** | S | 与批 C 并置;**交付的是验收,不是资产**;素材未到位挂标不记绿 |
| **`GB-1`** | **世界视觉层灰盒** —— `world_terrain` 数据 → Terrain proxy + 4 生态区边界可视 | **sprint-28** | M | **`sprint-29` 的 `world-ecozones-005` 的硬前置** —— 它的 AC 逐字要求「肉眼核对视觉层地形可读性」,没有视觉层则**无对象可看** |
| **`GB-2`** | **医馆 + 角色 + 道具 + 建造模块 proxy**(走 **Addressables 预制体**,承 ADR-023 ②) | **sprint-28** | M | sprint-28 现只有 2 件,是序列里最轻的一格 |

> ⚠️ **`GB-2` 不是「摆几个方块」**:`World.unity` 受 ADR-023 ② 零 gameplay GameObject 铁律约束
> ⇒ 角色 / 道具 / 建造模块**必须**走 Addressables,而现 `Default Local Group` 只有 **1 个** `world` 地址
> ⇒ **预制体组要从零建**。该工量已计入 `GB-2`,不当作顺手做。

**同批登记的两处「已交付」与两处「无家可归」(不改件集,只如实记)**

| 类 | 内容 |
|---|---|
| **已交付** | 本表 sprint-21 / 22 / 24 / 27 四行的件集中,**9 件已 `Complete`**:`foraging-003` · `processing-003` · `inventory-001` · `inventory-003` · `inventory-004` · `processing-002` · `processing-004` · `foraging-002` · `inventory-005`。⇒ 取件时按 `p0-sweep-2026-10-29.md` §六 的**现件集**,不按本表原行(承 §七 SP-11) |
| **无家可归 ①** | `clinic-machine/story-001` 卡在 **`In Review`** —— 缺的不是实现,是**复核转 `Complete`**;本表**无复核行**<br>✅ **2026-10-29 用户裁定:补进 sprint-21 起手** ⇒ 件号 **`C-1`**(见下) |
| **无家可归 ②** | `persistence-service/story-001` 于 **2026-10-25 QA 轮退回**(`In Progress`,7 条 AC 全未勾);本表 sprint-28 只排了 `save-slot-ui-001`,**无 7a 本体的位置** —— 而 7b 的 `AC-7b-01/02/10` 要求「两投影全部来自 7a」⇒ **7a 不收口,7b 那三条永远不可判**<br>✅ **2026-10-29 用户裁定:并入 sprint-28** ⇒ 件号 **`C-2`**(见下) |

> 🔁 **2026-10-29 用户裁定「两项都按建议补位」** —— 上表末两行由「只登记」转为**已排期**。
> 原文不删:那两行记的是**裁定前**的状态(它们的作用当时正是「取件时不会以为它已有人做」)。
>
> | 件号 | 落位 | 判据(取件时按此勾,不按「看起来做完了」) |
> |---|---|---|
> | **`C-1`** | **sprint-21 起手** | `clinic-machine/story-001` 由 `In Review` → `Complete`。前置已满足(G-8 于 S17-4 清偿 · `AC-24-08` / `AC-24-12` 两条挂标已解除并勾选)。⚠️ **判据 = `/story-done` 走完 + 落 §Completion Notes**,不是改一行 `Status:` |
> | **`C-2`** | **sprint-28**(与 `save-slot-ui-001` 同批) | `persistence-service/story-001` 的 **7 条 AC 逐条判定**(`AC-7a-01…06/18`);退回缘由是「`[x]`=0 / `[ ]`=7,用『文件已落盘』顶替『AC 已判定』」⇒ **每条须有可跑载体或显式 `NOT-RUN` 挂标**。⚠️ **7b 的三条 AC 依赖本件收口**,两者同批是**唯一不返工**的排法 |
>
> ⚠️ **两件的共同性质**:它们**都不是「从零实现」** —— `C-1` 的交付物在树上,`C-2` 的实现大部分已被 S19-1…S19-4 的 TD 工作项覆盖。**真正缺的是「判定」这个动作**。
> 正因如此,它们此前的缺席才不可见:本表的每一行都是「实现件」,**没有一行承载「复核 / 判定」**。


> **注:`clinic-machine/story-001` 已于 2026-10-20 本体交付**(`In Review`,`AC-24-08` / `AC-24-12`
> 半边挂 `BLOCKED-BY-O-24-1`)⇒ 剩余由 **44 → 43**;其挂标在 **sprint-18(G-8)** 清偿时解除。

**件数核对**:17(4)+ 20(4)+ 21(4)+ 22(4)+ 23(5)+ 24(4)+ 25(4)+ 26(4)+ 27(4)+ 28(2)+ 29(4) = **43**
= §一 的 44 − 已交的 `clinic-001`。**逐 epic 对得上**(casebook 6 · clinic 4 · death-respawn 6 ·
foraging 5 · inventory 6 · processing 5 · tutorial 5 · 单件 6)。

#### 6.5.1 🔁 2026-10-20 件集变更(用户裁定四项调整)

> `/sprint-plan` Phase 1 重扫时核出**五处矛盾**(见 `production/sprints/sprint-16.md` §Phase 1),
> 用户当场裁定**四项调整**,上表 sprint-16 行已按此改写:

| # | 调整 | 后果 |
|---|---|---|
| 1 | **压到 5 件** | retro-14 **A3** 与 **A5** 移出本 sprint(去向见下) |
| 2 | **`TD-epic-01` 移到 sprint-19** | ⚠️ **工序须按实测重定** —— `telemetry-analytics` 10 条 story **全部 `Complete`**,`In Review` = **0**;旧写的「3 条 `In Review` 重评」**从起点就错**(债册自身三处互斥:3 条 / 两条 / 一条未动) |
| 3 | **三份静态件物理合并** | 并入 **S16-2**;⚠️ **自我指涉** —— 被合并者之一是**承载本序列表的 `p0-completion-plan` 本身**,须先定落点(已于 S16-2 取推荐:并入本文件、保留原路径) |
| 4 | **纳入 EPIC.md 4 户分母刷账** | 新增 **S16-5**;这原被划为「归各 epic 职权」,现因它**正卡住分母真源**而纳入 |

**移出与去向**(⚠️ 不写去向 ⇒ 该件蒸发,本仓已知失败模式):

| 移出件 | 去向 | 状态 |
|---|---|---|
| `TD-epic-01` 清 P0 面 | **sprint-19**(AC 门批) | 工序重定;`TD-epic-01` 债条头部「3 条 `In Review`」须同批订正 |
| retro-14 **A3**「已证」补分支覆盖判据 | **sprint-19** | 未开工 |
| retro-14 **A5** `TD-player-01` 回写 | **sprint-17** | **实质已修完**(2026-10-20,`3940/3904/0/36`)—— **只差刷 retro 行动项表那一行** |

**同批订正的两处数字**(实测,**非引用**):

| 处 | 旧值 | 实测(2026-10-20) |
|---|---|---|
| 债册规模 | 82 条 / ~20 张表 | **92 个唯一 TD 编号 / ~17 张债型表**(+7 张非债型) |
| `milestones/README.md` 的全 Complete epic(改前 `:371`,现 `:370-385` 块) | 22 / 31 | **18 / 31**(按 story `Status:` 逐条归并;归 **S16-2** 落盘,见 §1.2) |

#### 6.5.2 本序列的三条硬序 / 自由序 / 压缩规则(承 completion-plan §四)

**硬序(不可违反,三条)**:

1. **sprint-18 / 19(门)必须先于其所解锁的件** —— **G-8 → sprint-20 的 clinic 链**;
   **G-1 → sprint-23 的 death-respawn 全链**;**G-4 → sprint-28 的 `save-slot-ui-001`**。
2. **sprint-21 与 sprint-26 两簇内部不得拆**(§2.2 的跨链耦合簇 + 呈现收尾簇)。
3. **sprint-16 必须最先** —— 它的产物(可复算的产能三元 + M3 Exit 判据 + 门载体)是**后续每轮判据的前提**。

**自由序**:sprint-20→22(clinic / foraging / processing / inventory 内部)、sprint-25、sprint-27
之间**可任意交错**,只要簇内不拆、门的硬序不违。

**压缩规则(若 sprint-16 重标定后判定可压缩)**:压缩**只允许合并自由序段**;
**硬序三条与簇边界不动**。⚠️ **不得为压缩把 `clinic-001` 式的「本体交 + 半边挂标」当 AC 勾满**。

#### 6.5.3 清债总体规划(2026-10-25 · 用户裁定「登记到总体规划」—— 原 2026-10-25 登记于 `production/sprints/sprint-20.md`,同日移入本文件,后者留指针)

> **登记时的账面(三十二订)**:`open 61 + partial 6`,未闭 P0 面 **62**,分布 9/31 epic
> (medcons 20 · audio 17 · clinic 14 · telemetry 5 · disease-sim 2 · skeuomorphic-ui/patient-ai/itemdb/building 各 1)。
> 复算命令 = 债册 §五 脚本本体。**本轮(清债轮 + 翻账批)净效应:未闭 P0 面 74 → 62(−12),零新增未闭。**
> 证据件 = `production/qa/evidence/impl-tddc-2026-10-25.md`(清债轮)· 翻账批无证据件(纯索引补翻)。

**结论:剩余 62 条没有下一条值得「专开清债轮」的。** 2026-10-25 能开的三族
(clinic 接受侧判别力 / prescription 分类器 / audio 池泄漏)已全清;剩余面只有三种清法 ——
**搭车**(嵌进已排期轮次,零专轮成本)· **等外部**(催不动)· **等触发**(改到对应文件才值得还)。

**清法一:搭车(约 23 条 —— 嵌进已排期轮次,不专开)**

| 载体 | 顺手还的债 |
|---|---|
| **sprint-20 本身** | S20-2 已含 `TD-building-01` 残余裁定;S20-1/2 动 clinic 烘焙面时可能撞 **`TD-medcons-11`**(CLI 烘焙入口 —— 卡面已预告「撞上挂标不记绿」,届时顺手补入口 = 还债) |
| **53 装配轮(后续 sprint)** | `TD-medcons-13`(cooked 镜像读方)· **`TD-medcons-18`**(D-1 流一致性须装配层先定契约,**轮次前不可跳**)· `TD-itemdb-01` 残段 · `TD-medcons-14`(CAP 分支) |
| **13 语义/接线轮** | `TD-audio-030①` 变体轴 · `TD-audio-033`(knowsClinic)· `TD-audio-034`(强度/性别轴)—— audio 侧最大同族驱动源 |
| **44 收口轮 / 素材批** | `TD-audio-027`(混音拓扑)· `029`(one-shot 池归还)· `031`(装配缝回收)· `032`(四项卫生)· `035`(三处代偿常量,不捆绑) |
| **触发文件被动的轮次** | `TD-epic-01-a/b/c`(解码契约三面)· `TD-medcons-04/06/07`(动 RunAll / Judge 签名时)· `05/08/12/16/17`(窄触发)· `TD-clinic-03/04/05`(动对应 binder 时)· `TD-prescription-08`(42 清理轮)· `TD-prescription-06`(美术资产轮) |

**清法二:等外部(约 18 条 —— 只能等,不排)**

- **数值轮(用户调值)**:`TD-medcons-01/02/19` + `TD-audio-025`
- **被试/实听排期**:`TD-audio-100/101/102/103/105` + `TD-listen-01`(101/102 各有 `034①` 等 B-1 前置)
- **P1b**:`TD-audio-104/107` · **目标硬件定稿**:`TD-audio-108`
- **sprint-23 death-respawn 开工前**:`TD-sim-01` + `TD-clinic-18`(提前做属抢跑,触发未到)
- **CI runner 落地**:`TD-medcons-09` + `TD-clinic-16`(同触发点,**届时必须同批**)

**清法三:等同族合并轮(现在单开 = 多付一次勘察成本)**

- **词法器加固轮**:`TD-clinic-17` + `TD-medcons-10` 是同一个 `JsonStage1Lexer` 的两个口子(债册已注明同轮)
- **`TD-audio-109` 第二半**:待用户裁(债文自陈)
- **TD-medcons 整块 20 条**:S20-7 第③裁(等数值轮 vs 挑前置无关子集)**裁定之前不单方面开**

**执行序(三条)**

1. **现在:sprint-20 开工**(S20-1 起手)—— 把 `TD-medcons-11`「撞上即顺手补 CLI 入口」写进 S20-1 开工注意。
2. **sprint-20 收口时**:跑 S20-7 治理盘点(剩三裁:① W6–W10 · ② QA 2 条 · ③ medcons 整块策略),当轮新掀债即时入册,保持「净新增 ≤ 当轮结清」。
3. **节奏基线**:2026-10-25 单日 74 → 62(清债轮 −10 + 翻账批 −2);后续 sprint 以「每轮收口时未闭 P0 面不涨」为默认线,涨了须在收口件里具名理由。

### 6.6 本轮的裁定留痕(五项已裁 · 承已合并的 completion-plan §6.1)

> **原「仍待裁」= 无**(五项全部已裁)。⚠️ 本表在 2026-10-20 一日内**两次就地订正**,
> 两次是**同型病灶的不同形态**,留在案上比抹掉有用(详 §6.6.1)。

| # | 裁定 | 落地动作 |
|---|---|---|
| **①** | **门的清偿载体 = TD 工作项** —— 以「TD 偿还工作项」直接实施,**不立 story、不重开已 `Complete` 的 epic**;同时**给债册增「P0 面 / 状态」两列** | M3 Exit 第二条合取**自此可机械求值** —— 它本来就是 TD 口径,不是 story 口径。债册实测 **92 条 / ~17 债型表**(原写 82 / ~20,已就地订正)。**归 sprint-16 ③(= S16-3)**。<br>🔁 **2026-10-20 S16-3 已收口**:列已落(`归属 epic / 状态 / P0 面`,**91 行**);⚠️ **真债号 = 91 不是 92**(92 为全文串扫口径 —— 多算 1 族引用 / 少算 5 个带后缀号,净 +1);⚠️ 含 TD 行的表**恰 20 张**(非 ~17)。**求值结果 = 假:72 条(未闭) / 12 个 epic**(详见债册 §★ 总索引 §五)<br>🔁 **2026-10-21**:回填 `TD-gate-01/02/03` ⇒ 债册现役 **94 行**(`closed 19` · `N 7`);**`72 / 12` 不变** |
| **②** | **`CAPACITY-MODEL` 重标定为三元:件 + 复杂度分布 + 追加率** —— 逐字兑现 retro-14 行动项 1;件数保留但**只作记录、不作外推依据** | 归 sprint-16 ①;✅ **已收口 2026-10-20**;落定后据此**复核本件每段的件数上限**(现按实测粒度 3–5 件写就,**非承诺**) |
| **③** | **`TD-epic-01`(51 telemetry)只清 P0 面** | 归 sprint-19(原写 sprint-16 ⑤,已按 §6.5.1 调整 2 移出)。⚠️ 余下留债**不等于**其 `Complete` 可信,债册原注「**不得读成已修好**」继续有效 |
| **④** | **M4 的 ETA 两个真未闭项**(U-4 批次数 · 外部分母口径) | ✅ **已裁(2026-10-20 用户)**:① **U-4 = 3 批**(= 表内实际批次)⇒ 验收 `1.5 × 3` = **4.5 人日已入合计** ⇒ 含内部侧总计 **≈69.0 → ≈73.5 人日**;并**就地订正** `asset-batches/README.md` 批次册的「4 批 = 6.0」(表内只登记 3 批,**「第 4 批」全册无出处**)· ② **外部分母口径 = 外包按里程碑整包交付** ⇒ **「人日/周」对外部侧不适用** —— **这不是「缺一个数」,是该数在本量纲下不存在**;外部摊期真源**改判**为**外包交付节点序列**。<br>⇒ **内部侧 ETA 自此可算**(`(10.5 + 4.5) / 35` ≈ **0.43 周**);**M4 的 ETA 仍未可写**,但阻塞源**收窄为一个输入:外包节点** |
| **⑤** | **§十 加注降级 + 回写陈景**(用户取「就地加注降级」,**不改写成指令形态**) | ✅ **已落地(2026-10-20)**:§十 节首加注块标其为「**候选输入**」而非指令;同节「待用户裁定两件」回写为「**两件均已处置**」—— G-4 → `TD-persist-02`(并升格为统一清偿载体,见 ①)、§八 #3′ → 已跑并结案 |

#### 6.6.1 两处就地订正的留痕(不删)

两处订正**同型病灶的不同形态**:

1. **陈旧前提**(④ 的原描述)—— 照抄了 `milestones/README.md:453` 的「K 登记」行(**2026-10-04 登记当时态**),
   **漏读同一文件 `:476-510` 的处置块**:③ 待估**已做**、载体**已建**。
   ⇒ 已回注 `milestones/README.md`(两处:③ 行 + 「仍有两项未闭」块),**从源头堵住**。
2. **无出处假定**(履行 ④ 时读出)—— `asset-batches/README.md` 批次册的验收行写 `4 批 = 6.0 人日`,
   而**表内只登记 3 批**,「第 4 批」**全册无出处**;同册硬边界 ② 又自陈「批次数**未定**」= **表内自相矛盾**。
   ⇒ 两处均**已就地订正**;真源自此 = `asset-batches/README.md` 节首「2026-10-20 用户裁定两项」块。

### 6.7 现在就能开工的件(无硬外部门 · 承已合并的 backlog §6)

**判据**:该件的全部上游依赖已在盘上(或只需接口桩),且 AC 无 EXTERNAL 阻断。

| 件 | 链位置 | 为什么可开 |
|---|---|---|
| `casebook/001` | 链首 | 只依赖既有的 ADR-010 codec 与 `IIdAuthority` |
| `clinic-machine/001` | 链首 | 只依赖已可运行的 ADR-014 两阶段工具链 |
| `tutorial-onboarding/001` | 链首 | schema + 烘焙校验,缺表报「未就绪」不崩 |
| `save-slot-ui/001` | 孤儿 | 无列出依赖,`design/ux/save-slots-7b.md` 在场 |
| `foraging/001` | 链首 | 上游全 Complete;**但一个子用例因 `item_key` 缺失须挂 `Ignore`**(见 §七 S-3) |
| `death-respawn/001` | 链首 | 9 的 `DeathCandidate` 未落地,**桩先行**;AC-29-01 全链不记绿 |
| `inventory-items/001` + `processing/001` + `processing/003` | **环 A** | **必须同批开**(承 §2.5);`processing/001` 的 AC-18-18 **非空前置 + 真表补口已于 2026-10-20 完成**,开工时判据直接可用 |

**⇒ 三个干净的链首**:`casebook/001` · `clinic-machine/001` · `tutorial-onboarding/001`
(加 `save-slot-ui/001` 一件 2 h 的小件)。**⇒ 一条必须打包的**:环 A 三件。

> ⚠️ **本表与 §四 批 A 不是同一判据**:§四 按**卡面挂标 + 上游契约实测**分 A-1/A-2;
> 本表按「**今天动得了吗**」分。两表**交集非全部** —— `death-respawn/001` 在本表可开,
> 在 §四 却因全链 `BLOCKED-BY-9` 落批 B。**取件时两张都要看**(§八 #6)。

#### 6.7.1 三条供 `/sprint-plan` 直接复用的硬约束(承 backlog §8)

1. **每批 2–4 件,L 件单独跑**(`CAPACITY-MODEL`;= §6.2)。
2. **开工前必须重扫该 epic 的 `EPIC.md` + story `Status:`**(`/sprint-plan` Phase 1 硬前置)——
   实测已有 S-1 / S-2 两处「卡面与事实不符」,不复扫就会照旧阻塞或照旧借绿(= §6.3 R-1/R-3)。
3. **环 A 三件不可拆批**(§2.5)。

---

## 七、陈旧前提清单(实测,取件前必须复核)

| # | 位置 | 旧说 | 实测 | 处置 |
|---|---|---|---|---|
| **SP-1** | `foraging/story-004:36` · `story-005:42` | `BLOCKED-BY-OQ-17-2`(`GATHER_MUL_CAP` 未裁) | `OQ-17-2` **已于 2026-10-06 裁定**(`foraging.md:711`:`GATHER_MUL_CAP = 3` · `GatherMul(L)=1+L/30` · `CapTable[L]` 61 项) | 取件时**就地订正卡面**;判据仍可能要数值轮冻结真实表数字 —— **但不该再写「未裁」** |
| **SP-2** | `death-respawn` 全 epic | `BLOCKED-BY-9` / `BLOCKED-BY-7a` | 两条**仍成立**(G-1 `SelfLimited` 零命中 · G-2 `Folded(p)` 零命中)—— 但性质已由「裁决待定」变「实现待落」 | 取件前确认,不订正 |
| **SP-3** | `milestones/README.md:49` / `:370-385` 块(改前 `:372`) | `22/31` · `220 story / 172 Complete / 46 Ready` | **2026-10-15 快照,已过期**(现 226/182/44 · 18/31) | ✅ **已就地加注(2026-10-20 · S16-2)** —— 四处(`:49` 表 · Exit ① 行 · 2026-10-15 复核块 · `:570` 附表)均已加注真源,**原文保留不删**。**本行结案** |
| **SP-4** | `audio-system/story-019` | 旧 `In Progress` | **已 `Complete`**(10-c 移出转 `TD-listen-01`) | 已订正于该卡 `:4` |
| **SP-5** | `milestones/README.md` 的 M3 Exit ①(改前 `:371`) | 「31 个 epic 全 Complete」**单条判据** | 见 §五:会假绿 | ✅ **已改写为两条合取(2026-10-20 · S16-2 落盘)** —— 原措辞以删除线保留留痕;第二条合取标 **`NOT-YET-EVALUABLE`**(债册列归 S16-3)。**本行结案**<br>🔁 **2026-10-20 补(S16-3 收口)**:列已落 ⇒ 该条自 **`NOT-YET-EVALUABLE`** 转为 **`NOT MET`** —— **实测 72 条(未闭) / 12 个 epic**。**「可求值」≠「已满足」** |
| **SP-6** | 本文件 §四 **A-1** | 判据 = 「卡面零 `BLOCKED-BY` 标记」**单条件** | ⚠️ **判据本身有洞**(2026-10-20 实测):卡面挂标 = **立卡当时已知**的阻塞,不含「上游契约从未落过」。`save-slot-ui/001` 卡面 0 挂标但上游 5 个符号全零命中(G-4) | **判据已改为双条件合取**(§四 A-1 注);✅ **A-1 五件的上游已于取件轮全部补扫**(结论见 §四 A-1);⚠️ **A-1 之外的 16 条零挂标件上游仍未扫** |
| **SP-7** | `clinic-machine/story-004:4`(卡面) | `Status: Ready` ⇒ 读作「从零实现」 | ⚠️ **实为「接线 / 补登记」** —— 其交付物**已在树上**:`Sim.Contracts/ClinicEnvDto.cs`(40 行,`OQ-CP-4` 2026-09-22 裁定件)+ `IClinicEnvQuery`(`:36`)均已落,**生产侧零消费者**(仅 `intent_boundary_test` / `case_secrecy_discipline_test` 两处测试引用) | **取件前就地订正卡面**(该件在 A-2);判据从「实现 DTO + 查询接口」改为「**把 DTO 接进 42 的读面 + AC-24-07 白名单 / AC-24-09 移 42**」 |
| **SP-8** | `clinic-machine/story-002:99` `Depends on` | 「系统 23 的 `structure_at` 只读契约(GDD 侧已定,**实现若未就绪可用 fixture 桩注入**)」 | 23 `modular-building` epic **Complete ✅ 2026-10-03(7/7)**;读面实测在:`World.Structures`(public `World.cs:26`)→ `StructureInstanceRegistry.TryGet`(`StructureKinds.cs:111`)/ `GetInstanceAt`(`:117`),便捷包装 `GetModuleAt`(`World.cs:114`) | **条件已消解 —— 不需桩**;取件时**就地订正卡面**该从句(桩本可为「23 只是老实现在树上」留下借口,与 §十一 元教训同型) |
| **SP-9** | `tr-registry.yaml:4150`(TR-clinic-006 的 `note`) | 「⚠️ `n_max` 悬于 `OQ-24-5` **未裁** ⇒ 断言无参数可写」 | `OQ-24-5` **已于 2026-10-07 裁定 `n_max = 64`**(`clinic-machine.md:397`,并连带拍定 `C_max = 1/64`);note 写于 2026-09-21,早于裁定 ⇒ `status: partial` 的理由**已消失**,断言参数**已有着落**(`n_max × TIER_MAX = 64 × 3 = 192 < 2^31` ✓) | **registry `status` 由 `partial` → `covered`**(判据 = 参数已裁 + 表值可在烘焙门写死);⚠️ **改 registry 归 architecture-review 轮**,本行只登记陈旧事实。**同 SP-1(`OQ-17-2`)型** —— 这是本仓**第二次**「裁定已下、下游仍写未裁」<br>🔁 **2026-10-21 补注**:**本行的「实测」列自身已半陈旧** —— `C_max` 已于 2026-10-21 由 `1/64` 改裁为 **`1/8`**(与 `n_max` **解耦**,原耦合式 `n·C_max ≤ 1` 作废)。**不影响本行的结论**(`n_max = 64` 未变、`partial` 仍该转 `covered`),但**引本行时不得照抄 `C_max = 1/64`**。出处 = `design/gdd/clinic-machine.md:198` 的就地重算注 + `production/qa/evidence/impl-td-itemdb-01-build-modules-2026-10-21.md` §九 |
| **SP-10** | **54 关卡工具**(ADR-022 / `systems-index.md:90,170`) | 全库**三处引用它**(ADR-022 §三 定义 · `systems-index` 登记 · `world-ecozones/story-005` 的 EXTERNAL 前置),读起来像「一个已排期的系统」 | 🔴 **实测:它没有 epic、没有 story、不在 §6.5 的 sprint-16…29 表里、债册无任何条目。** ⇒ **三处引用、零排期载体** —— 承本仓反复记录的「**引用却无登记**」失效模式(同 `CompoundTriggered` / `ActorCellEntered` / ADR-022 自身的立件缘由)。<br>**连带后果(实测)**:① `world_buildslots.json` **全仓零存在**(`assets/data/` 26 份 JSON 无它);② 23 的 `BuildSlotCatalog.IsInBuildSlotRegion`(`:86`)因此**P0 简化为「有槽位即区域内」**,其注释自陈「多边形包含检查需 ADR-022 烘焙数据,**当前无多边形输入**」;③ `AC-23-08` 的第三个半边(占用格 ⊆ `BuildSlotRegion`)**零载体**;④ `world-ecozones/story-005` 排在 **sprint-29 批 C**,该批的判据逐字是「素材与冻结到位才动 —— **现在做 = 自造断言**」 | **用户 2026-10-21 裁定:不立 epic、不排期**,只在本行登记缺口 ⇒ **取件时不会以为它已有人做**。<br>⚠️ **登记 ≠ 解除**:`world_buildslots.json` 在 P0 内**仍不产出**;23 的 Region 校验**维持 P0 简化实现**;`AC-23-08` 第三半边登记为**已知缺口**(承 `TD-building-01` 的「卡住的判据」①)。<br>⚠️ **本行不是「54 可以不做」** —— 它是「**54 今天没有承接者**」;`world-ecozones/story-005` 的 EXTERNAL 阻塞**因此仍成立**<br>🔁 **2026-10-28 处置被推翻(原文不删,逐字保留)** —— 用户裁定 **「推翻裁定,排期完整关卡工具」**(S20-20 挂标 #3–#5 的处置轮)。**「不立 epic、不排期」自此作废**;承接者 = `production/epics/level-tool/`(**EPIC + 6 stories**,2026-10-28 立)。<br>⚠️ **推翻的只是「处置」,不是「实测」** —— 本行「实测」列的五条里,四条仍成立(三处引用 · 无 epic/story · `IsInBuildSlotRegion` P0 简化 · `AC-23-08` 第三半边零载体 · `story-005` 排 sprint-29);**一条已过期**:「`assets/data/` 26 份 JSON 无 `world_buildslots.json`」—— S20-20 起该文件**存在**(schema 2,13 格)。<br>⚠️ **登记 ≠ 解除**:本行列的每一处缺口仍须由 `level-tool` 的 6 个 story **逐条兑现**;立件只把「没有承接者」变成「有承接者」。<br>📌 **取件前必读**:`production/epics/level-tool/EPIC.md` §立件缘由(它回指本行,两处口径须一致) |
| **SP-11** | 本文件 **§6.5** 的 sprint-21 / 22 / 24 / 27 四行件集 | 读作「这些都是待做件」 | 🔴 **实测 9 件已 `Complete`**(`foraging-003` · `processing-003` · `inventory-001` · `inventory-003` · `inventory-004` · `processing-002` · `processing-004` · `foraging-002` · `inventory-005`)—— 该表是 **2026-10-20 快照** | 取件时按 `p0-sweep-2026-10-29.md` **§六** 的现件集;`/sprint-plan` Phase 1 的硬前置(重扫现树)**再次证明必要** —— 本行是它第三次抓到同型陈旧 |
| **SP-12** | §6.5 表 sprint-29 行的 **`skeuo-019(b/d)`** | 读作「两个待办分档」 | 🔴 **两头都不是待办**:`019-b` = **冻结落定 ✅ 2026-10-14**(判据已由 spike 实测倒推,门待接线)· `019-d` = **`Complete ✅` 2026-10-05**。真正欠的只有 `019-b` 的**构建期门** | 已上移为 **`A2-1`**(sprint-26,见 §6.5.4);本行原措辞保留不删 |
| **SP-13** | `persistence-service` 两件均 `Complete`(§6.5 sprint-28 的前提) | 读作「7a 已闭,只剩 7b」 | 🔴 **`story-001` 于 2026-10-25 QA 轮退回**,`In Progress`,**7 条 AC 全未勾**(`Type: Logic` 的自动化证据是 BLOCKING 门;qa-lead 判为第五个假绿形态)。且 §6.5 **无 7a 本体的排期行** | 已登记为「无家可归 ②」(`§6.5.4` 末表);🔁 **2026-10-29 用户裁定:并入 sprint-28** ⇒ 件号 **`C-2`**(判据见 §6.5.4 末块) |

#### 7.1 陈旧阻塞(卡面写了但事实已变 —— 承已合并的 backlog §5)

> **与上方 SP-1…SP-10 的关系**:SP 系列是**本路线图自身**的陈旧前提(取件前复核);
> S 系列是**卡面 / 里程碑文件**的陈旧阻塞(须就地订正)。**同族不同源,不合并编号。**
> ⚠️ **去重登记**:backlog 的 **`S-4` ≡ 上方 `SP-3`**(同一处 `README:49` 的 `22/31`)—— 合并时
> **不再单列**,只在 SP-3 行内保留 S-4 的原始措辞以免丢信息。其余 5 条**无重复**,逐条吸收。

| # | 位置 | 卡面原文 | 实测状态 | 处置 |
|---|---|---|---|---|
| **S-1** | `death-respawn/story-003` `AC-29-16` | `BLOCKED-BY-OQ-7a-9`(折叠豁免待三方裁) | ❌ **已裁**。`ADR-026 §⑦` 明写「**结清 `OQ-7a-9`**」;该 story 自己在 `:49` 也写了「ADR-026 案 1 已裁」 | ✅ **已订正(2026-10-20)**。6 处 `BLOCKED-BY-OQ-7a-9` 全部改为「**BLOCKED-BY-7a 折叠实现**」;原措辞以删除线 + 订正块留痕,**未删原文**。**阻塞性质变更**:「裁决待定」→「实现待落」。实测 `Folded(p)` 生产谓词全库零命中(= **G-2**) |
| **S-2** | `processing/story-001` `AC-18-18` | `EXTERNAL · BLOCKED-BY-21a(O-18-R1)` —— 字段有 schema、无实现 | ⚠️ **性质已变,且原判据曾会恒绿**。实测:有枚举 · 有 `Owner` 属性 · 烘焙期读取;**但三个真问题**:① `Sim/` 侧零消费方;② 数据表**只有 2 条配方、`owner` 全为 `process`** ⇒ `craft`/`build` 子集为空;③ 「并 = 全表、两两交 = ∅」在空子集下**平凡成立** = **空集借绿** | ✅ **已订正且已补口(2026-10-20)**。AC 改为**三段判据**(非空前置 / 并 / 交),拆「判据机制可证」与「真表覆盖度」两段记分;真表补 `craft`/`build` 占位配方后 `owner` 分布 = `{process:2, craft:1, build:1}`;判别力**三次全转红**(数据 `sha256` 逐字节还原)。详 §7.2 |
| **S-3** | `foraging/story-001` | `Ignore("BLOCKED-BY: 21a item_key 行未落地")` | ✅ **阻塞真实**。实测 `item_database_items.json` 的 4 条记录首键为 `base_id`,**无 `item_key` 字段** | **保持**,真阻塞(§2.4.4 已引) |
| ~~**S-4**~~ | ~~`milestones/README.md:49`~~ | ~~`22/31` epic 全 Complete~~ | ❌ **陈旧**(三元组 `2026-10-15`)。实测 **18/31** | 🔁 **≡ SP-3,已并入该行**(去重,不单列) |
| **S-5** | 四个 epic 的 `EPIC.md` 分数 | `emergency-procedures 7/7` · `patient-ai 4/4` · `skeuomorphic-ui 18/18` · `world-ecozones 6/6` | ❌ **分母偏小**(实际 story 数 **9 / 5 / 25 / 6**) | **归 S16-5**(EPIC.md 4 户分母刷账)。⚠️ `world-ecozones` 是「**分子多 1**」而非「分母少 N」,**形态与另三户不同类**,须单独处置 |
| **S-6** | `save-slot-ui` | —— | ⚠️ **曾缺 `EPIC.md`**(31 目录中唯一) | ✅ **已消解(2026-10-20)** —— `EPIC.md` 已补建,**31/31 目录自此均有**。原文保留留痕 |

> **S-2 是本批最重要的一条**:它不是「文档陈旧」,是**一条会恒绿的 AC**。
> 空集子集让「并 = 全表」在零配方时天然成立 —— 这正是本项目反复出现的
> 「自造断言顶替 AC 语义层」的同一形态。
>
> #### 7.2 S-2 结清记录(2026-10-20 · 同日二轮)
>
> - **判据订正**已落卡面(三段判据 + 非空前置),并补两段记分(**禁合并成一条绿**)。
> - **真表补口**已执行:新增 `craft_wooden_splint`(`craft`)与 `assemble_wooden_post`(`build`)
>   两条**占位配方** ⇒ `owner` 分布 = `{process:2, craft:1, build:1}`。
>   ⚠️ **数值为夹具值,非平衡值** —— 真配方内容仍归**数值轮 / 21a 内容批**,本补口不追认内容侧。
> - **判别力已证伪**:删 craft / 删 build / craft 改 `process` ⇒ 真表夹具**三次全转红**
>   (基线自检绿 → 变异红,数据 `sha256` 逐字节还原)。
> - **回归零损伤**:全量 EditMode `3951/3915/0/36`,较前次基线 `3945/3909/0/36` 恰 `+6/+6/0/0`。
>   ⚠️ 该数带其自身三元组,引自 `tests/integration/processing/README.md`;**本节不重签**。
> - **补口途中的一次改道**:首版 craft 产出写成 `drug` 类,会踩红 11 的 `AC-11-20`
>   (药类必须在 `materia_lexicon.json` 有功效词 = **11 的内容**)⇒ 改为 `tool` 类,切断耦合。
> - ⇒ **S-2 从「待处置」转入「已结清」**;`processing/001` 的**判据面**不再有阻塞项
>   (其**主体实现**仍未开工,`Sim/Processing/` 目录不存在 —— 那是排期问题,不是阻塞问题)。

---

## 八、本路线图的失败条件(防它变成下一份没人用的文档)

| # | 若发生 | 则 |
|---|---|---|
| 1 | 某轮 `/sprint-plan` 未引用 §6.3 四条复核(尤 **R-4**) | 本文件判为**未落地**,不因「已写」而算兑现。⚠️ **合并注**:本条**同时**吸收已合并的 completion-plan §七 #2(逐字同义)—— 序列再对,取件判据不对仍是按旧账排期 |
| 2 | 有人用「件/日」做线性外推排日期 | 等同 `CAPACITY-MODEL.md` §六 条件 #3 触发 ⇒ 本文件 §6.1 作废。⚠️ **合并注**:本条**同时**吸收 completion-plan §七 #1(其形为「据 §6.5 的 sprint 序列**推算日期**」)—— §6.5 与 §6.1 一样**无任何起止日**,那是**按裁不填**,不是遗漏 |
| 3 | 又出现「epic 全 story Complete 但契约零命中」的新户 | ⇒ §三 的门清单**不完备**;须重做一次全 epic 契约普查,不得只补这一户。<br>**✅ 已预防性跑完(2026-10-20)—— 本条件已「兑现处置」**:普查见下行 **#3′**,三项未清项(① 孤儿契约 ② 未溯源桶 ③ 代码零改动声明)全部结清 ⇒ **门清单据实由「四道」扩为「七道」(G-5/G-6/G-7),做法自本轮起定为「新户出现前先普查」**。<br>🔴 **⚠️ 且同一天的取件轮就再出新户** —— 取件 R-4 撞见 **G-8**(`O-24-1`,21a 家具目录扩展),**门清单再扩为八道**。⇒ **本条「结案」的意思是「本轮处置已履行」,不是「门清单已完备」** —— 它**当天就被证伪一次**。**根因 = 符号源**:前七道取自 ADR,`O-24-1` 取自 **GDD 的 `O-<sys>-<n>` 义务登记**,是一个**从未进过扫描列表的第三源**(见 §十一 **C-9**)。⇒ **再出现新户仍触发本行**;且**换符号源即等于换扫描面**,不得假设单一源的普查已覆盖全集。 |
| **3′** | — | **✅ 已跑(2026-10-20)** —— 原件 = `production/qa/evidence/census-adr-obligation-2026-10-20.md`(三元组 `2026-10-20T07:59` UTC · §八 #3′ 普查轮 · `54fd5e5`)。<br>**扫描面**:31 个 epic 中含 story 者 → **17 个全 story Complete**(前轮引用的 18 作废,见原件 §4.2);符号源 = 31 份 ADR 的**代码块**(52 符号)+ **散文**(379 符号)两级;代码面 = `unity/Assets/` 全树 C#,**剥注释**,`Tests/` 单列档。<br>**结果**:零命中分类 —— **① 真缺口 12** · ② 命名漂移 5 · ③ 零命中正确 7 · ④ 显式延后 2 · ⑤ 面外噪音余。<br>**够格成为 G 型债的 = 4 项**(须同时满足:符号零命中 ∧ 归属 epic 全 Complete ∧ **epic 卡面自己点名该符号**):`IPlayerMotor`(`player-controller` 6/6 + `camera-viewpoint` 6/6 **双向点名**)· `LogiPose` / `ITameable` / `TameState`(`enemy-ai` 5/5)· `DeferredThreatSlot`(`random-events` 6/6)⇒ **已登记 `TD-player-02` / `TD-enemy-01` / `TD-randomevents-01`**(用户裁定「能归 epic 的登记 TD,不立 story」)。<br>**⚠️ 未清项**:<br>① ~~**孤儿契约未处置**~~ ✅ **第二轮专扫已跑(同日)** —— 原件 §7。结论:**P0 系统无缺 epic**(`systems-index` 的 P0 集合与 31 个 epic 严格一一对应);无 epic 的 23 个系统优先级全为 P1a/P1b/P2/P0-前置。真正的孤儿**仅一条**:`IReplayPipe`/`ReorderBuffer` —— **义务级 P0 ≠ 归属系统级 P1b**(`systems-index.md:81` 记 45 = P1b;ADR-001 `:167` / `:448` 逐字要求「P0 预埋 / P0 写 `IReplayPipe` + `ReorderBuffer`」)⇒ **八个抽象点落了 7 个,它是唯一零落点** ⇒ 已登记 **`TD-net-01`**。`ISceneRouter`/`IWorldSpawner` 与 `IPerTargetIl2CppArgs` **均有显式跟踪面**(`BootRoot.cs:20` / ADR-023 §Validation S2 `⏸` / ADR-012 未勾项)⇒ **不同级,不另立 TD**。<br>② ~~**§4.3 未溯源桶 10 个符号**未逐个核其声明 ADR 是否构成 P0 义务~~ ✅ **第三轮已跑完(同日)** —— 原件 §4.3。**结果:零条新债**(同物异名 2 · 语义已承载 1 · 未开工 4 · ADR 明确排除 2 · 已并入既有 TD 1)。**并更正原件自身一处错**:原写「10 个全树零命中」**不成立** —— `WeaponMultiplier` 9 处(实现为复数 `WeaponMultipliers` + 访问器) · `QualityCap` 6 处 · `ReputationMult` 1 处 均有命中;错因 = 「剥注释 + 精确单数」两误叠加(第三度同型,见 §十一 C-6)。未开工的 4 个(`QualityCap` / `DeathAllowed` / `RegrowWindow` / `InventoryOf`)归属 epic 分别 0/5 · 0/6 · 0/5 · 0/6 ⇒ 按够格判据②**不够格,非债**。<br>③ **本件是勘察,未改任何代码 / GDD / ADR / 卡面。**<br>**⇒ 三项未清项全部结清,#3′ 普查闭环。** |
| 4 | §1 计数与本文件三元组相距 > 3 轮未刷 | 计数段作废,重测后再用 |
| **5** | **三源(§5.2)任一被单独当收官进度报出** | **失真** —— 须并列。只看剩余件数会漏掉八道门(七道挂在已 `Complete` 的 epic 之下);只看门会漏掉 43 条剩余 story;只看 TD 的 P0 面会漏掉「门今天还没有 story 载体」这一事实。(承 completion-plan §七 #4) |
| **6** | **取件时只看了 §四(批 A/B/C)或只看了 §6.7(现在就能开工)之一** | 两表**判据不同、交集非全部** —— `death-respawn/001` 在 §6.7 可开、在 §四 落批 B(全链 `BLOCKED-BY-9`)。**只看一张会做出相反决定** |
| **7** | **某件的件集与本文件 §一–§五 不一致(§6.5 的序列越权改件集)** | **以 §一–§五 为准**,§6.5 只主张顺序;差集须**显式登记**(承 completion-plan §七 #5) |
| **8** | **某件的 `Status` 由「本体交 + 半边挂标」被记成 `Complete`** | **假绿** —— 正是 §5 要堵的形态(承 completion-plan §七 #3)。⚠️ 与 #3 不同:**#3 是「门没清就宣布 epic 收口」,#8 是「单件把半边挂标当勾满」** —— 前者在 epic 层,后者在 story 层 |

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
| **§八 #3′ 普查已跑** | ✅ | ✅ | 原件 `production/qa/evidence/census-adr-obligation-2026-10-20.md`;「17 个全 Complete epic」与「① 类零命中」均有可复算命令(原件 §7) |
| **普查扫描面已声明** | ✅ | ✅ | 原件 §1 表:母体 31 / 子集 17 / 符号源两级(52 + 379)/ 代码面全树剥注释 / `Tests/` 单列档 |
| **① 类 4 项够格(G 型债)** | ✅ | ✅ | **双向可复算**:① 符号全树零命中(原件 §7 命令);② epic 卡面点名处**逐一贴 file 路径**(原件 §3.4) |
| **普查三处假零自曝** | ✅ | ✅ | `GoldenHashV1Test`(`Tests/EditMode/Sim/golden_hash_v1_test.cs:24`)· `StructureKinds`(`Sim/World/StructureKinds.cs`)· `FixPow`/`FixSqrt`(`Fix.cs:191/212/240`)各贴落点 ⇒ 与 G-3 同型,已拦 |
| **「孤儿契约」结论** | ✅ | ✅ | `ls production/epics/` 全表(原件附录 B)**无 network / scene / boot 目录** ⇒ ADR-001 的 45 与 ADR-023 的一对无归属 epic |
| **~~前轮「18 个全 Complete」~~** | ⚠️ **数字已废** | ❌ **撤回** | 本轮判据(文件头 blockquote `Status` 行)可复算得 **17**;差 1 未复核 ⇒ 18 作废,原文不删留闭环 |
| **未溯源桶 10 符号已跑完** | ✅ | ✅ | 逐符号重扫(不剥注释 + 查同物异名)⇒ **零条新债**;分布同物异名 2 / 语义已承载 1 / 未开工 4 / ADR 明确排除 2 / 并入既有 TD 1。**4 个未开工项各自的 epic 计数 0/5·0/6·0/5·0/6 均已贴**(原件 §4.3)⇒ 不够格判据② |
| **「10 个全树零命中」更正** | ✅ | ✅ | ❌ **原说法错**,已就地更正 —— `WeaponMultiplier` 9(`CombatPower.cs:49/54`)· `QualityCap` 6 · `ReputationMult` 1(`EventF1Pipeline.cs:91`)· `ItemId`=`ItemKey`(`Sim.Contracts/ItemDatabase/ItemKey.cs:14`)。第三度同型假零,已立 **C-8** |
| **§八 #3 结案** | ✅ | ✅ | 三项未清项(① 孤儿契约 ② 未溯源桶 ③ 零代码改动声明)全清;§八 #3 行加收口注,#3′ 闭环 |

> **`Landed=✅ / Proven=✗` 是合法的诚实状态** —— 批次划分与动态规则属判断面,
> 不宣称已证。**本轮未运行任何夹具,故不得为它们标 Proven ✅**。

---

## 十、立即可做的下一件事

> ### ⚠️ 2026-10-20 就地加注 —— **本节定性降级为「候选输入」,不是指令**(原文保留,不删)
>
> **本节把一条动态层决定写进了静态层文档,且已被直接执行过。** 按本文件 **§〇 自己定的分工**:
> 「**这一轮拿哪几条**」属**动态层,由每轮 `/sprint-plan` 现裁**;本节却把「下一步 = 取哪几条」
> 写成了**静态层文档里的一条工作队列**,于是 sprint-15 收口(2026-10-20)后无 sprint-16 承接时,
> 它成了唯一活的工作单 —— **`clinic-machine/story-001` 即由此起**(`fb87e2a` / `6e5a8d1`)。
>
> ⇒ **本节自此是「候选输入」,不是指令。** 取件仍须走 `/sprint-plan` + **§6.3 四条复核**(尤 **R-4**)。
> ~~**后续 sprint 的序列**已另立 `production/milestones/p0-completion-plan-2026-10-20.md`~~
> ⇒ **该序列现于本文 §6.5**(原独立件已于 S16-2 物理并入本文,`git rm` 掉,见节首合并表)。
> (A-1 收口已落为 **sprint-17**)。**分工重申:§6.5 主张顺序,§一–§五 主张件集。**
>
> ⚠️ **用户 2026-10-20 裁定本项时,选的正是「就地加注降级 + 回写陈景」** ——
> 即**不为本节另立规则、不改写成指令形态**,只标性质并回写下方两处陈旧项。

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

**~~待用户裁定两件~~ → ✅ 两件均已处置(2026-10-20 回写;原两问原文保留在下方)**
1. **G-4 怎么还** —— 7a 的 `Checkpoint` / `Load` / `Writable` / `Loadable` / `Gating`,
   而 `persistence-service` 已标 `Complete 2/2`。**补 story** 还是 **照 G-1/G-2 先例登记 TD**?
   ✅ **已裁(2026-10-20)**:取**后者** —— **登记 TD,不立 story** ⇒ **`TD-persist-02`**。
   并被本文 **§6.6 ①** ~~`p0-completion-plan-2026-10-20.md` §6.1 ①~~ **升格为八道门的统一清偿载体**
   (**TD 工作项**;不立 story、不重开已 `Complete` 的 epic),归 **sprint-19**。
2. **§八 #3′ 的普查** —— ~~18 个~~ **17 个**「全 story Complete」的 epic 是否需要逐个跑一次「ADR 义务符号 vs 代码」?
   ✅ **已跑并结案(2026-10-20)** —— 见 §八 #3′;新登 **`TD-player-02` / `TD-enemy-01` / `TD-randomevents-01` / `TD-net-01`**。

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
| **C-6** | ~~「18 个全 story Complete 的 epic」~~ | **17**(判据 = 文件头 blockquote `Status` 行,可复算;18 作废,差 1 未复核) | 与前轮同一类错:**计数判据没写死**。且本轮普查**当场自曝三处假零** —— `GoldenHashV1Test`(生产-only 扫描漏了**测试树义务**)· `StructureKinds`(**ADR 引的是文件名,不是符号**)· `FixPow`/`FixSqrt`(**ADR 声明的成员名 ≠ 代码成员名**)。三条合起来的教训:C-5 的「全树」还不够,**还要「Tests/ 单列一档」+「文件名 ≠ 符号」+「别名的反向:同物异名也算命中」** |
| **C-8** | §4.3「未溯源桶 10 个符号**全树零命中**」 | ❌ **第三度同型错** —— `WeaponMultiplier` **9** · `QualityCap` **6** · `ReputationMult` **1** 均有命中;另 `ItemId` = `ItemKey`(**同物异名**) | **C-6 的教训写下后当场又犯一次**:原判据 = 「剥注释 + 精确单数」,而这一桶里**恰好全是「只被注释引用」+「复数/访问器形态」**。⇒ 判据**收窄**为:全树(含 `Tests/`)**不剥注释** ∧ 逐符号问**同物异名** ∧ 判「缺口」前再问**归属 epic 是否已 Complete**。**「写下教训」不等于「下次会遵守」** —— 判据须落成**可执行的命令形态**,不是散文守则(本条即 C-6 三重奏的第四件) |
| **C-9** | 取件轮 R-4 写下「A-1 五件**全部**通过 R-4」 | ❌ **过宽,已作废** —— 实为 **4 件通过 · `clinic-machine/001` 6/7 AC 面通过**(`AC-24-08` / `AC-24-12` 的上游 `O-24-1` 零落点 ⇒ 半边挂标) | **双层盲区,两层都要单独记住**:<br>**① R-4 自身**(§6.3):其判据写的是「对**上游 ADR** 点名的义务符号逐个 grep」⇒ **只扫 story 卡面点名的 ADR**。而 `clinic-machine/story-001` 的 `AC-24-08`/`AC-24-12` 依赖的是 **GDD 级的跨系统义务 `O-24-1`(24 → 21a)**,**不挂在任何 ADR 上** ⇒ 结构上扫不到。<br>**② §八 #3′ 普查的符号源**(原件 §1):只声明了两源 = ADR **代码块**(52)+ ADR **散文**(379)。**GDD 的 `O-<sys>-<n>` 义务登记是第三个源**,从未进过扫描列表 —— 而 G-8 正是从这里冒出来的。<br>⇒ **修正**:R-4 的扫描面**须含 GDD 的 `O-*` 义务登记**;**普查的符号源须增第三源**。<br>⚠️ **本条与 C-6/C-8 不同族**:C-6/C-8 是**同源内的判据写错**(剥注释 / 精确单数),C-9 是**源的枚举不全** —— 前者靠收紧判据解决,后者**只能靠扩容源清单**。**扩源的触发方式 = 换一个符号族去扫**;故本条也是 C-7 的第三层(第 1 层 = 有 epic 无下游指针;第 2 层 = 连 epic 都没有;第 3 层 = **义务根本没走 ADR 这条路**) |
| **C-7** | §八 #3 门清单「普查做完即完备」 | ❌ **仍不完备** —— 发现**孤儿契约**一类:ADR 有义务、但**无归属 epic**(ADR-001 的系统 45 · ADR-023 的一对) | **G 型手法以「归属 epic 全 Complete」为前提** ⇒ 对**没有 epic 的契约结构上不可见**。这是 C-4 盲区的**第二层**(C-4 = 有 epic 但无下游指针;C-7 = 连 epic 都没有)。本轮实测坐实,**专扫未做** |

⇒ **元教训**:「按标识符 grep 零命中」**只在扫描面覆盖全树时才成立**。
此后同型判定须**先声明扫描面**(扫了哪些目录、用了哪些别名),再落结论。
**2026-10-20 追加三条**(由 §八 #3′ 普查当场自曝三处假零得出):
① **`Tests/` 须单列一档** —— 有些 ADR 义务的落点**只在测试树**(`GoldenHashV1Test` 即此);
② **ADR 里的文件名 ≠ 符号** —— `StructureKinds.cs` 存在不等于 `StructureKinds` 类型存在;
③ **别名的反向同样成立** —— 只防「子串假命」不够,**同物异名**(`FixPow` vs `Fix.Pow`)会**假零**,
须对每个零命中问一次「它是不是改过名」。
