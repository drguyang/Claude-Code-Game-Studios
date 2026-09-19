# 敌人 AI(系统 27)评审日志

> 评审史轨道:`design/gdd/reviews/[doc-name]-review-log.md`。每条评审按时间倒序追加。

## Review — 2026-09-18 — **重开评审结案:NEEDS REVISION → 4 项文档级阻断全部落盘 → Status 保持 Approved**
Scope signal: L
Verdict path: **重开评审(lean · 独立会话,评审者独立于撰写上下文)** —— 触发条件命中(`O-27-3`
2026-09-18 结案 = 25 载荷定稿 ≠ provisional 假设:档位折叠归 9、27 改读 `QueryHurtLevel`)。
判 **NEEDS REVISION**(4 项阻断,全部**文档级/规格空洞**,零机制重裁)。用户裁定 **[A] 现在就修订** +
**[甲] 遭遇级硬时限**(`ENCOUNTER_TIMEOUT` 的机械定义 = 遭遇级硬时限,非状态机第六态)。
**4 项阻断全部落盘**:
- **R1 「超时」无机械定义** —— `EncounterEnded{reason=超时}` 此前无判据。**修**:新增
  **规则二十之二** `ENCOUNTER_TIMEOUT`(遭遇级硬时限:自 `EncounterStarted` 起最长 tick 数,
  到点 → `EncounterEnded{reason=超时}`,与敌人当前状态无关,同一遭遇只写一次,结束 ≠ 实体消失)
  + **Tuning knob 五之三**(安全范围 `> DISENGAGE_DELAY`,防「超时 / 脱离」竞态)
  + **OQ-27-7** 登记(须在写遭遇生命周期之前有量级)+ **AC-27-35** 可达性判据注。
- **R2 A24 联动未在 27 侧登记** —— `R_CONTACT ↔ RANGE_CELLS`(`RANGE(default_attack(e)) ≤
  R_CONTACT(e)`,烘焙期两表同批可见)仅在 25 侧断言(`AC-25-5-12`)。**修**:新增 **`O-27-10`**
  (27 侧义务 = 烘焙器两表同批可见,不新增运行期接口)+ **R_CONTACT 参数行交叉引用**。
- **R3 同 tick 求值顺序未钉死** —— 同 tick 多敌人 + 遭遇级检查的先后未定义 ⇒ 静默实现依赖。
  **修**:Formulas 节 blockquote —— ① actor_id 升序逐个求值六态机(纯函数,读上 tick 快照,
  互斥转移 ⇒ 同 tick 无链式反应)② 遭遇级检查(`ENCOUNTER_TIMEOUT` 到点 → 写 `EncounterEnded`)
  ③ 写输出;同 tick 两条 `EncounterEnded` 互斥。
- **R4 陈旧标记** —— 规则十八 provisional 契约 / 依赖表 25 行 / 双向性核对 / `O-27-3` 硬前置
  的「9 / 25」旧口径。**修**:provisional **已解除**(`Kind` 三元组定名入 `entities.yaml` +
  ADR-009 §三 Amendment H,27 侧零改动兑付);`HurtLevel` 折叠改归 **9**(`QueryHurtLevel` 查询);
  陈旧「世界流(9 / 25)」→「世界流(9)」;`O-27-3` → ✅ 已结案。
**登记进裁决堆(冻结纪律)**:机制零改动;量级归 `OQ-27-7`(`ENCOUNTER_TIMEOUT > DISENGAGE_DELAY`)。
**未被动摇**:文件头 Status **Approved**(原结案判定为设计级,本轮为规格空洞)。
**计数面**:规则 22 → **23**(+规则二十之二)· 义务 `O-27-1…9` → **`O-27-1…10`**(+A24 联动)。
Prior verdict resolved: Yes(2026-09-17 Approved 免二轮 → 重开触发命中 → 本轮 NEEDS REVISION → 修订落盘,Status 保持 Approved)

---

## Review — 2026-09-17 — **结案:✅ Approved(免二轮)**
Scope signal: **L**
Verdict path: 首轮 `MAJOR REVISION NEEDED` → 用户裁定 `[A]` 本轮修订 → **10 项阻断 + 4 项设计裁定 +
2 项 CD 提级项当日全部落盘** → **用户裁定接受修订、免二轮** → 标记 Approved。
Summary: 用户接受首轮修订结果,不再开二轮评审。**这是显式风险接受,不是「已核对」** ——
首轮修订的每一项都未经第二双眼睛复核,而「改动自身引入新缺陷」正是门分裂那类问题的高发形态
(尤其 **AC-27-11 的 2×2 叉乘**的四个组合、**B2 冻结口径**与 **B9 兽/人趴地分叉**三处结构性改动)。
**重开评审的触发条件**（三者任一）:① `OQ-27-7` 标定(`NODE_BUDGET` / `FLANK_TIMEOUT` 的量级 ——
**有正确性面**,不是纯手感值);② `O-27-3` 结清(25 格斗线落盘,`TR-enemy-017` 从 `partial` 转正);
③ 本 GDD 的承重面(六态机 / `entity_kind` + `down_class` 语义 / 世界流写者拆分)被任何后续系统修改。

**结案涟漪**:`enemy-ai.md` 文件头 → **Approved**(含「免二轮说明」+ 重开触发条件)·
`systems-index.md` **四处** → Approved(§1 表 row 27 · §11 队列行 15 · §10 已有 GDD 行 · §10 P0 已设计行
+ §design-order 矩阵 row 27 划除)· `production/session-state/active.md` 本节重写。

**未结项(随结案带入实现期,不阻塞)**:`O-27-3`(25 无 GDD —— 🔴 P0 唯一硬前置)·
`OQ-27-7`(计时器 / 搜索预算组量级)· `O-27-8`(三份对侧 GDD 双向性回填)·
`O-27-9`(导航格「代价」字段三处对账)。

Prior verdict resolved: **首轮条目见下** —— 10 项阻断全部修订落盘;二轮未跑(用户裁定免)。

---

## Review — 2026-09-17 — Verdict: MAJOR REVISION NEEDED → **用户裁定 [A] 本轮修订;10 项阻断 + 4 项设计裁定当日全部落盘**(~~待二轮评审~~ → **同日用户裁定免二轮,已 Approved,见上方结案条目**)
Scope signal: **L**
Specialists: game-designer · systems-designer · qa-lead · ai-programmer · unity-specialist(×5 报告,均并行)· creative-director(opus 串行综合)
Blocking items: 10 | Recommended: 6(含 2 项 CD 提级为必改)
Summary: 首轮 `/design-review`(`full`)。creative-director(opus)终裁:**27 的架构姿态是正确的** ——
决策进 sim / 运动表现态的分层承 ADR-016 承得干净,门 A / 门 B 与 ADR-005·006·015 自洽,
未发现需要推翻的架构错误。**10 项阻断全部是规格空洞与未加门的判据,不是设计错误** ⇒
**按 1→9 顺序一次性补齐即可,无重架构**。CD 同时裁定:四项跨域口径(兽/人倒地 · LOD 冻结 ·
目标选择 · 行为树工具)**由用户拍板**,不得由 GDD 单方面决定。

**🔴 最具后果的一项(五路专家收敛,`[game-designer][systems-designer][qa-lead][ai-programmer][CD]`)**:
**F-27-2 与 F-27-5 的门分裂** —— 同一个 `Disengage` 的士气分支,F-27-2 用 `morale_enabled` 定义,
F-27-5 用 `flank_enabled` 定义;而原判据(原稿 AC-27-10,**今 AC-27-11**)的取样点恰好只取 P0 实存的两行(`T/T` 与 `F/F`),
**在这两行上两门同值 ⇒ 用哪个门结果都一样 ⇒ 判据恒绿**。这是「AC 全绿而 bug 在」的教科书形态。
**修法(最便宜、信号最高)**:删 F-27-5 的重复分支,`Disengage_if_morale` **单一出处**在 F-27-2;
**AC-27-11 改为 `(morale_enabled, flank_enabled)` 的 2×2 叉乘** —— 交叉行 `T/F` 才是探针,
且这是全案**唯一**必须枚举「不与 P0 数据同构」的参数组合的判据。

**十项 BLOCKING(2026-09-17 全部修订落盘)**:

- **B1 门分裂**(`[五路收敛][CD]`,最高后果)—— 见上。**修**:F-27-5 删除重复分支;
  `Disengage_if_morale` 单一定义在 F-27-2;AC-27-11 改 2×2 叉乘并写明「为什么必须是叉乘」。
- **B2 冻结 / LOD 自相矛盾**(`[systems-designer][ai-programmer]`)—— 规则十与 AC-27-04/05
  断言「冻结**不**改变 sim 真值」,但 `LogiPose` / `acc` / `path_cursor` 是**积分量**
  (不是 tick 的纯函数),**跳过 step 就是少积分** ⇒ 原断言物理上不可能成立。
  且 AC-27-04 比较「LOD 开 vs 关」两配置 —— 那两者**本来就不该**产生相同轨迹。
  **修**:① 规则十改写为「冻结**确实**改变位姿;冻结态可由三源重放复现,但 ≠ 从不冻结态;
  **不补算**」;② 冻结判据删「**离屏**」(相机事实 = 表现态,作冻结依据会让客户端重算与主机分叉且静默);
  ③ AC-27-04 改**同配置逐位一致** + 节流输入的反射白名单;AC-27-05 点名**停机 / 恢复 + `acc` 不变量**;
  ④ 新增 **EC-27-23**;⑤ 删除 F-27-3 里**误引**的 ADR-016 §九「冻结不改变判定」。
- **B3 int64 断言的**对象**写错**(`[systems-designer][qa-lead]`)—— 原写 `R_VIS < 46341`,
  声称是「`d2` 的 int64 前置」。但 `R_VIS` 只是**比较阈值**,对 `d2` 的中间量零约束;
  溢出风险来自**坐标差**的量程。**修**:断言改为**世界坐标量程** `3 × (2W)² < 2^63`
  (保守取 `W ≤ 2^30`,承 ADR-015 §三);新增 **EC-27-24**;AC-27-08 的断言组同步改写。
  **不加运行期 clamp**(clamp 会把溢出变成静默错误答案)。
- **B4 `Band` 三行并列 ⇒ 求值次序依赖实现**(`[qa-lead][ai-programmer]`)—— F-27-1 的
  `Patrol / Alert / Chase` 写成三个**并列条件**,`R_CHASE < R_ALERT`(常见配置)时两行同时为真
  ⇒ 结果取决于实现者的行序,且**静默**。**修**:定义**求值次序 + 首真胜出**,用 `ChaseReady`
  保证互斥;新增 **AC-27-14**(互斥性性质测试)。
- **B5 符号未定义**(`[qa-lead][systems-designer]`)—— `ALERT_TIMEOUT` / `CHASE_TIMEOUT` /
  `FLANK_TIMEOUT` / `NODE_BUDGET` / `STEP_COST` / `REPLAN_PERIOD` 在公式里出现但**无定义处**;
  `HurtLevel` / `nearby_allies` / `downed_allies` / `w_threat` / `last_contact_tick` /
  `last_path_tick` / `PathFailed` / `in_combat` / `engaged_with_player` / `Moving` / `Facing` 同。
  **修**:全部补定义与归属(`HurtLevel` 归 9/25 的整数投影,27 只读不累加;`nearby_allies` /
  `downed_allies` 的成员判据 = **同一 `encounter_id`**(不是半径);`in_combat` =
  `State ∈ {Chase, Flank, Engage}`);新增 `OQ-27-7` 承载计时器 / 搜索预算组的定值。
- **B6 悬空引用与错引**(`[qa-lead][CD]`)—— EC-27-16 引「ADR-002 全序键」(ADR-002 是地形,
  全序键在 **ADR-009 §一**);`game-concept.md:535`(黄铜读数条)被当作「锚点②先听见后看见」
  的实际出处(实为 `random-events.md:41/:67`);45 行引「四十三的后果」(43 无此内容)。
  **修**:三处订正 + 全库 grep 零残留。
- **B7 双向性声称不实**(`[qa-lead][CD]`)—— 本 GDD 声称与 23 / 6 已双向,但对侧
  (`modular-building.md:468` 标「单边」· `world-and-ecozones.md:416/:783` 标「❌ 单边 ——
  27 无 GDD」)**仍是旧标记**。**修**:诚实标注 + 新增 **`O-27-8`**(对侧回填);
  ⚠️ **依据「内容已成立」不得写成「已双向」** —— 判据是对侧的文本,不是本侧的意图。
- **B8 已声明但未落盘的义务**(`[qa-lead]`)—— `O-27-5`(两个 Kind 进 `entities.yaml` +
  ADR-009 §三)与 `O-27-6`(`ActorCellEntered` 的 P0 范围补注)在 GDD 里写作「已登记」,
  但 **ADR-009 §三 骨架里没有**。**修**:本轮**实际落盘** ——
  `entities.yaml:1948/1960`(核对)+ **ADR-009 §三 追加 `EncounterStarted` / `EncounterEnded`**
  (**Amendment H**,10 → 12 个 Kind)+ §三 补注「`ActorCellEntered` 的 `Actor` 命名在 P0 只服务玩家,
  27 要发须另开 ADR」(复评门纪律)。
- **B9 兽 / 人倒地无规格**(`[game-designer][systems-designer]`,**提级为设计裁定**)——
  规则十六写「倒地」但**从未定义野兽倒地与人类倒地是否同一件事**:人形倒地可被 10 救治
  (支柱二的实现),野兽倒地**不可救治但可取材**(28,P1a)。原文让二者共用一条路径 ⇒
  要么 P0 就得实现取材(越界),要么野兽永远卡在「可救治的昏迷」这个语义谎言里。
  **用户裁定:加 `entity_kind`(`Human`/`Beast`)+ `down_class`(`Healable`/`Salvageable`)
  两个整数枚举** —— 是**身份**(烘焙行上的枚举),**不是分支**(不出现于任何转移或公式,
  只用于写输出);**两个而不是一个**(一个枚举只回答一个问题;合并会使 P1a 的「驯化中的熊」不可表达)。
  规则六表 + 规则十六的路由表重写;新增 **AC-27-27**;`down_class` 分叉发生在**消费者侧**
  (Healable → 10 / Salvageable → 28),27 只负责写对字段。
- **B10 AC 判据含不可判定的负存在断言 + 覆盖缺口**(`[qa-lead]`,CD 提级为必改)——
  原 25 条 AC 里 **5 条是负存在断言**(「零 `ActorCellEntered`」/「零第三方库」/
  「零 `isBeast` 分支」/「零 `Physics.Raycast`」)—— **不可判定**(你无法证明「没有第四种」)。
  另有覆盖缺口:`Flank` 态**无任何 AC 断言其可达与可退出** / `Chase→Alert` 未测 /
  `EncounterEnded` 的三值枚举未测 / `path_cursor` 重算未测 / `Down` 的决策停止未测 /
  调试视图隔离未测。**修**:全部改为**正面白名单**形式;AC 由 **25 → 35**(33 BLOCKING + 2 ADVISORY);
  新增 10 条对应缺口(AC-27-06 · 13 · 14 · 15 · 20 · 21 · 27 · 28 · 34 · 35)。

**用户的四项设计裁定(`PR-27-6`,均照准)**:
① **兽 / 人倒地** = `entity_kind` + `down_class` **两个**整数枚举(身份非分支);
② **LOD 冻结判据删「离屏」** —— 只用 sim 量 `d2` / `in_combat`;
③ **目标选择** = P0 单人形式(最近格距 + **最低 `actor_id`** 决胜)+ 具名扩展点 `target_policy`
   (P0 不实现非 `NearestVisible` 值,构建期 `throw`);新增 **F-27-6**;
④ **砍掉编辑器期行为树工具** —— P0 手写 `assets/data/ai_enemy.json`,走 ADR-016 §四
   **Alternative 5**(自研数据表载体);**消解 ADR-016 唯一一处 post-cutoff 悬置**,
   其 `Knowledge Risk` 由两处降为一处。

**CD 的两项额外必改(均落盘)**:
- **CD-1 ADR-016 §九 的「冻结不改变判定」必须就地改**(`[CD]`)—— 该句是 B2 的**源头**,
  只改 27 的 GDD 会留下权威件与子件冲突。**已落盘**:§九 删「离屏」+ 证伪原句 +
  `acc` 同冻;**同批修** `Knowledge Risk` / `Post-Cutoff APIs Used` / `Verification Required` /
  Alternative 5 / `Risks`(两行)/ `Validation Criteria` / `Migration Plan` / `Header`。
- **CD-2 「单原型」的口径澄清须双向**(`[CD]`)—— 原「P0 只留一个原型」与 P0 交付两类敌人
  (兵痞 / 野兽)表面冲突。**已落盘**:**「单原型」= 行为程序的表达能力上限,不是实体种类数**;
  P0 = **单套行为程序 × 两类参数行**;判据 = AC-27-32 的分支谓词白名单。

**Recommended(6 项,均已落盘)**:F-27-6 / F-27-7 成型(目标选择与 A* 从「规则里的散文」
升为可测公式)· F-27-7 的缓存策略表与 `path_cursor` 保值 · `EnemySignalDto` 补形状
(42 / 44 此前不知道 27 给什么)· 锚点② 的引用订正 · Tuning Knobs 补 `ALERT_TIMEOUT` 组
与 `NODE_BUDGET` 组(并写明 `NODE_BUDGET` 太小是**假阳性**风险 ⇒ AC-27-21)·
`O-27-9`(导航格「代价」字段三处对账 —— 6 写三字段 / 23 只回布尔 / 27 用常量)。

**Unresolved(带进二轮)**:`O-27-3`(25 无 GDD,P0 唯一硬前置)· `OQ-27-7`(计时器 /
搜索预算组的量级 —— `NODE_BUDGET` 是**半规格**,有正确性面,不能推迟到调参轮)·
`O-27-8`(三份对侧 GDD 的双向性回填)· `O-27-9`(导航格代价字段对账)。

**涟漪已落盘**:`adr-016`(§Header / §四 / §九 / `Verification` / Alternative 5 / `Risks` /
`Consequences` / `Validation Criteria` / `Migration Plan` / `Ripples`)·
`adr-009`(§三 **Amendment H** + `ActorCellEntered` 范围补注)· `entities.yaml`
(`EncounterEnded` 的 `reason` `全灭` → **`全倒地`** + 引用编号订正)· `systems-index.md`
(row 27 §1 表 / §6 设计序 / §11 队列 / §10 已有 GDD 行 / 原型口径行 五处)。

Prior verdict resolved: **First review** —— 本条目为首轮;~~二轮评审须新会话~~ **2026-09-17 用户裁定免二轮,直接 Approved(见上方结案条目)**。
