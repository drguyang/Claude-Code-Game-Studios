# processing GDD — 评审日志(系统 18 炮制)

> 文档:`design/gdd/processing.md` · 逐条修订的完整口径以 GDD 正文为准,本日志只记评审事实。

---

## Review — 2026-09-19 — Verdict: ~~NEEDS REVISION~~ → **✅ Approved(当日修订 + 用户裁定免二轮)**

Scope signal: **M**(中等复杂度:3 公式(刻意零新数学)· 3-6 依赖(21a/5/24/30/20/42/44/7a)· 无需新 ADR —— 走 ADR-009 Amendment 通道)
Specialists: `full` —— game-designer · systems-designer · qa-lead · network-programmer · ux-designer · audio-director(并行)+ creative-director(Opus 串行综合)
Blocking items: **11** | Recommended: ~7(当日全量落盘)
Prior verdict resolved: **First review**(2026-09-18 成稿,本条为首轮)

### 头号根因:「载荷三缺」(7 / 11 条阻断同根)

`Craft` 事件原稿载荷只有 `{recipe_id, start_tick, duration_ticks, inputs[], outputs[]}` 六字段,
缺三个下游运行期必需的位置 —— 由此一条根因发散出七条阻断:

| 缺位 | 后果(静默,非报错) |
|---|---|
| 无 `actor_id` | 20 的 `InventoryOf(player)` 无法归因「谁扣的料」;联机两玩家同格各自起炉不可分 |
| 无 `output_instance_ids[]` | 20 前置 6 / R10 要求「产出实例 id 由主机铸造且可重放」,原稿把铸造时点推给「完成时现铸」= 完成时无事件 ⇒ 无法重放 |
| 无 `tool_cell` | 「器具占用」是派生态,重建需要求值位置;原稿只有 `recipe_id`,派生态无从重算 |

### 十一条 BLOCKING 与落点

| # | 根因 | 修订落点 |
|---|---|---|
| BL-1 | `Craft` 落世界流,但**总序键从未裁决**(= 21a `D-21-28`,原稿自己把它当「已存在的规则」引用,而该规则不存在) | **R-18-E / 用户裁定 [甲]**:沿用三流全序键 `(Tick, StreamPriority, Patient=None, Seq)`(ADR-008 §一)+ `Seq` 主机 `Append` 发号;`PatientId.None = -1` 哨兵承 ADR-007 ④ 不污染 `max(id)` 高水位;**ActorId 位被载荷 `actor_id` 吸收**(不占键位)。落 **ADR-009 Amendment J** + `entities.yaml` + 21a `D-21-28` ✅结案 |
| BL-2 | 「起货」时容量不足没有任何处置 —— 原稿把它推给 20 的「整体回滚」,但完成时点**没有回滚对象**(料已在点火 tick 扣掉) | **用户裁定 [甲]**:溢出物当场由 **20 从 `Craft` 事件派生折叠 `DropSpawned`**(落医馆格,`instance_id` 沿用载荷不重铸)—— **复用既有 Kind,零新增**;18 不写流。O-18-R4 登 20 前置 6b |
| BL-3 | 子集判据 `outputs[].processing_state ≠ inputs[].processing_state` 在 `item_key = (base_id, processing_state)` **复合主键下结构性不互斥**(19 制作 / 23 建造的配方同样「改变 state」) | **R-18-B / 用户裁定 [甲]**:`Recipe.owner ∈ {process, craft, build}` **显式字段**回 21a(**D-21-30** 已落盘 + `AC-21a-66`);「state 变化」降为**单向校验**。⚠️ 18 侧 `AC-18-18` 记 **EXTERNAL · BLOCKED-BY-21a**(有 schema 无实现 ⇒ 不得记绿) |
| BL-4 | 「多炉 / 取消 / 排队」三者混在一个旋钮行 + 原稿正文另一处已写死「不能同时做另一炉」= **自相矛盾**;且链深口径混乱(raw→pill 是 2 还是 3?) | **用户裁定 [甲]**:**单炉、不可取消**(第二炉点火 = 主机拒绝**零事件**;排队被否 = 进程态的一种,与已裁的 `OQ-18-5` 冲突)。**跨玩家并发允许**(每玩家各自单炉);**器具互斥剥出另立 `OQ-18-8` 归 24**(18 不自裁)。链深**定义**本轮给 = **转化次数**(raw→pill = 3 次转化 3 条事件);**值**仍归 `OQ-18-4`。旋钮行删除,新增 `P0_MAX_CHAIN_DEPTH` |
| BL-5 | 载荷三位缺失(见上表) | **R-18-A**:载荷定稿九字段 `Craft = {actor_id, recipe_id, start_tick, duration_ticks, input_instance_ids[], ActualConsumed[], OutputQty[], OutputQuality[], output_instance_ids[], tool_cell}`;`output_instance_ids[]` = **主机于点火 tick** 经 `IIdAuthority.ItemInstanceId.Next()` **依全序键顺序**铸造(认领 20 前置 6 / R10) |
| BL-6 | `EnvMod` 的**求和 + 钳制全文无执行落点** —— 5:395 说「21a 会钳」、AC-5-19 说「5 不钳」、21a `:484` 变量表是**入参断言不是操作**;`:533` 的构建期不等式保护了一个**不存在的包络** | **R-18-C**:两分量(`EnvMod_climate` ← 5 / `EnvMod_clinic` ← 24)18 **原样透传、不相加不钳制**;`EnvMod_total = clamp(climate+clinic, ENV_MOD_MIN, ENV_MOD_MAX)` **具名进 21a F1 正文**(= **D-21-31** 落盘)。5 侧 **AC-5-19 就地收窄**为只断自家不钳(涟漪注)。⚠️ 顺带自查出一条记账反模式:**跨系统 AC 指认**(两文档互相指认对方有断言 ⇒ 谁都不可判)—— 首例自我援引 5 的 B-3 |
| BL-7 | 支柱挂载两处错误:页眉写「支柱三 = 转化是有代价」(**支柱三原文是「还原 × 整体」**);另一处挂「支柱五 · 君臣佐使」(配伍/组方被 `game-concept.md:284` 反支柱原文**明禁**,且 18 规则九禁读药性 ⇒ 该词在 18 **零载体**) | 页眉订正:「转化是有代价」= **21a 的锚点三**,不是 game-concept 支柱三;支柱五的落点改为**「炮制 = 那个时代的标准化手艺」**(火候与时节),删「君臣佐使」。Game Feel 两行的误挂同批订正 |
| BL-8 | 「配方可知性」只有禁令(「不做灰置网格」)没有正向面 —— 玩家在史实内**怎么知道**有哪些方子可做,全文无载;与幻想节自述矛盾 | 规则十拆**面板侧(禁)/ 世界侧(须有)**两栏;`Admissible` 谓词升为 18 → 42 的**只读接缝义务 `O-18-R6`**;`AC-18-10` 拆 18 侧半边(iff 双向)+ `AC-18-10b` 呈现半边记 **EXTERNAL · BLOCKED-BY-42**(42 屏清单尚未认领炮制交互)。内容侧线索挂 `OQ-18-4` |
| BL-9 | 原稿边例行「守恒律被击穿 = 不可达」**为假**:构建期校验是**聚合式** `Σ(w_out×qty×QM_MAX) ≤ EFF_MAX×Σ(w_in×Ceil(qty/EFF_MAX))`,运行期是**逐条 `Round(HALF_AWAY)`**,二者**不同形** | **D-21-32** 落盘(修法进 21a 规则八注):**逐条同形极值式** `Σ(w_out × max(1, Round(qty_i×QM_MAX))) ≤ EFF_MAX × Σ(w_in × Ceil(qty_j/EFF_MAX))` = 新 **AC-21a-65**(聚合式降为必要非充分)。**已验反例**(python 复算):`w_in=10 / w_out=6 / EFF_MAX=1 / QM_MAX=1.5 / qty=1` ⇒ 构建期 `1.5×6×1=9 ≤ 10` ✓,运行期 `Round(1.5)=2 → 2×6=12 > 10` ✗。⚠️ **注**:此前口头携带的「base 积 100 → 132>100」经复算**不成立,已弃**。18 侧 `AC-18-19` 在 21a 修复前不得记绿 |
| BL-10 | 44 把 `sfx_decoct_*` 挂在 **23/24** 名下,而发射方是 18;Dependencies 双向记账皆缺 | 44 `:697` **发射方改判 18**,命名改 `sfx_process_light / _complete / _work`;18 上游表 + 44 cue 表各补一行。**AD-1 三护栏**入 §Audio(收尾 = `start+duration` 纯函数 / 淡出非帧对齐 / 禁床边界音「用耳朵数 tick」)+ **炉火声床禁被 StethoscopeFocus 压没**;`AC-18-22` 去标注盲测镜像 5 的 AC-5-10 |
| BL-11 | AC 表 17 条中 6 条 grep 式判据、3 条依赖未落地外件却记为可验(「**借来的绿**」);`AC-18-17` 的「存档任何字段不含」不可测 | AC 全表改造:**分组 A–D · 25 条 · 级别列**(BLOCKING 20 / ADVISORY 1 / **EXTERNAL·BLOCKED-BY 4**:10b→42 · 15→ADR-012+F7·夹具 · 18→21a owner · 19→21a 守恒实现);判据一律机制化 = **spy 夹具 / Roslyn-IL 符号引用扫描(禁 grep 字符串匹配)/ 入参恒等断言 / 反例哨兵 / 证据归档 `production/qa/evidence/processing-XX-<date>.md`**;`AC-18-07` 并入 `AC-18-01`②;`AC-18-17` 改为「18 程序集零 `[Serializable]` + 7a 段清单不含 18 记录」两处机器可断言面 |

### 专家分歧与裁决

**network-programmer 与 creative-director 在「点火时机」上分歧**(唯一一处未走用户裁定、由 CD 判词直接并案的):
NP 认为「准入 + 扣料 + 铸造」应在同一 tick 由主机原子完成 ⇒ 无需具名机制;CD 判词:**「什么时辰起炉」是 18
唯一有玩家判断面的动作**(火候 = `EnvMod_raw` 的块哈希阶梯 × 点火即时值 ⇒ 同一料不同时刻起炉结果不同),
若把它写成原子实现细节,支柱一的判断面在炮制侧**归零**。⇒ 升格为具名机制 **R-18-A**,`AC-18-21` 配
「等待期可读性」下限(在场观察一个 tick 误差内可判起货),其具名渲染形态挂 `OQ-18-9`。

ux-designer 与 systems-designer 在「无声拒绝」上分歧:UX 主张拒绝须有反馈(否则玩家以为卡死),
SD 主张反馈即提示(违反零数字铁律与 AC-44-09)。合案 = **拒绝走物理事件面**(起手截停、料退回原格),
不进 UI、不出声 —— 与 ADR-011 Amendment B「本地即时呈现 + 主机终裁」同构。

### 四项用户裁定(2026-09-19,AskUserQuestion,均选推荐案 [甲])

**Q-1** `Craft` 沿用三流全序键(R-18-E)· **Q-2** 溢出物当场 `DropSpawned`(R-18-D)·
**Q-3** 显式 `Recipe.owner` 字段回 21a(R-18-B)· **Q-4** 单炉、不可取消(R-18-A 的并发半边;器具互斥剥出 `OQ-18-8`)。

### 结案方式

修订当日全部落盘 + 涟漪六件后,用户裁定 **[B] 接受修订、直接标 Approved(免二轮)**。
⚠️ **免二轮 = 显式风险接受,不是「已核对」**。**重开触发条件(四者任一)**:
① `OQ-18-4` 内容轮改写 `duration_ticks` 量级或链深定义(现定义 = 转化次数,值未裁);
② `OQ-18-7` 在 **ADR-001 窄修订轮**(45 GDD 轮,与 `OQ-4-10` / `OQ-10-9` / `20-BL-4` 同批)被裁为「客户端逐帧上行」而非聚合意图 —— 则规则三/八的「客户端预答 = 纯呈现」口径作废;
③ 21a 未落 `D-21-30` / `D-21-31` / `D-21-32` 而 18 侧 `AC-18-18` / `AC-18-19` 被记绿(**借来的绿**复发);
④ `OQ-18-8`(24 的器具互斥占用表)与 18 规则八「跨玩家并发允许」相撞并改写并发模型表。

### 未结(随各轮落地,不阻塞开工)

`OQ-18-4`(配方条数 + `P0_MAX_CHAIN_DEPTH` 值 —— 与 `OQ-25-8` tick 频率同批裁最经济)·
`OQ-18-7`(意图上行通道载体 —— P1b 前,45 / ADR-001)· `OQ-18-8`(跨玩家器具互斥 —— 24 的占用表)·
`OQ-18-9`(等待期器物载体具名形态;远程不可知 **P0 = 显式无**,不加同步)。
**随他档**:21a `D-21-32` 的烘焙管线实现(未写)· `D-21-33`(`SkillLevel` newtype,待认领)·
20 前置 6b(溢出派生折叠)· 42 的 O-18-R6 接缝认领。数值全归用户。
