# 玩家控制器与移动 (Player Controller & Movement, #1) — Review Log

## Review — 2026-09-16 — Verdict: NEEDS REVISION(首轮 `/design-review`)

Scope signal: **L**(单系统,但与 3 份 ADR 的 Amendment 耦合 · 6 条根因触达 4 份 registry 面)
Specialists: unity-specialist · systems-designer · qa-lead · ux-designer · performance-analyst(均 haiku 并行)
→ 收束:**creative-director**(Opus,串行综合)
Blocking items: **5**(按根因合并为 **6**)| Recommended: 见根因表 | Prior verdict: First review —— 本仓 `design/gdd/reviews/` 下此前无本文件评审日志

Summary(creative-director 收束):**「边界是对的,移动公式是空的」**。
边界裁决 —— **位移 = 纯表现态 · sim 中唯一投影 = 跨格世界流事件 · 相机只读不持状态** ——
经五领域**全部通过**,不需重做(承 ADR-020 §四 / §五,与 42 / 44 的呈现层三件套同构)。
坏在**六条共享根因**上,其中**第 1 条被三方独立命中**(game-designer B1 · systems-designer B1 ·
unity-specialist B9)—— 全场最强信号:**全篇没有任何公式把 `MoveInput` 的二维方向映射到世界速度方向**。
`v_target`(F-1-2)被写成**标量**,F-1-3 却用它减向量 `v_horiz`,F-1-4 再读 `v_horiz.x/.z` ——
**不能既是又不是**;`v_horiz` 的方向全篇无定义。

### 根因诊断(本轮的承重结论)

**41 条发现落在 6 条共享根因上。⇒ 不按 41 条修 —— 按 6 个根因修。**
（与 3 的首轮「按根因而非按条修」同一纪律。）

| # | 根因 | 命中者 | 严重度 |
|---|------|--------|--------|
| **1** | **移动基向量从未定义** —— `MoveInput`(二维) → 世界方向的映射缺失 | `game-designer B1` + `systems-designer B1` + `unity-specialist B9`(**三方独立命中**) | 🔴 最高 |
| **2** | **归并算符三版不等价**且 **guard 错对象** —— tick 内跨格如何折叠,三处写法互不等价 | `unity-specialist` + `qa-lead` | 🔴 |
| **3** | **谁 `Append` 从未定义** —— 客户端 / 主机 / 各自,三种解读都说得通 | `systems-designer` + `network` 视角 | 🔴 |
| **4** | **F-1-1a 用错变量** —— 拿 `TICK_PERIOD` 做隧穿上界,而隧穿与 tick 无关、与帧长有关;且**不等式所有权反转** | `unity-specialist` + `qa-lead` | 🔴 |
| **5** | **`CharacterController` 参数从未命名** —— 只说「走 collide-and-slide」,一个参数都没点 | `unity-specialist` | 🔴 |
| **6** | **接地模型三命题数学不相容** —— 「静止不调 `Move`」⇔「`isGrounded` 可信」⇔「斜坡不滑」不可同时成立 | `unity-specialist` + `game-designer` | 🔴 |

> 根因 6 是**唯一无法在本轮「改完」的** —— 三命题不相容是数学事实,不是措辞错。
> 用户裁定**推迟到 spike**(见下裁定 ④)。

### Blocking Items → 处置表(2026-09-16 当日完成)

| # | 根因 | 处置 | 落点 |
|----|------|------|------|
| 1 | **移动基向量缺失**(三方独立命中) | **新立 F-1-8**:`v̂_world = f̂ · input.y + r̂ · input.x`,其中 `(f̂, r̂) = 相机 YawBasis`(**相机相对**);**基须水平化且正交归一**;退化情形(俯视 `proj(f̂) ≈ 0`)登记 `OQ-1-14` | F-1-8 · `AC-1-31`(单位性)· `AC-1-35`④(次序)· §States 基更新块 |
| 2 | **归并算符三版不等价 + guard 错对象** | 收敛为 **§States 唯一权威版**:≤1 `Append` / tick;tick 内后续跨格**覆盖**待发值(`pending_cell`);guard 挂在**正确对象**上;补 **`else → pending_cell := null`** 这一句承重分支 | §States 归并块 · `AC-1-32` · EC-2 |
| 3 | **谁 `Append` 从未定义** | **R5:主机唯一 `Append`** —— 主机是唯一 `Step` / `CatchUp` 执行者 ⇒ 主机是唯一 Appender;客户端模式下 `IEventSink.Append` **调用点数 = 0**,其格经 **ADR-001 第二 QoS**(unreliable latest-value)上行;⇒ **`ActorCellEntered` 移出可靠通道** | R5 · `AC-1-30` · EC-16 · `OQ-1-9` 结案 · `O-4` |
| 4 | **F-1-1a 用错变量 + 所有权反转** | 改用 **`MAX_DT`**(非 `TICK_PERIOD`)—— 水平防隧穿约束是 `SPEED_MAX × MAX_DT ≤ LATTICE_SIZE`;不等式**所有者反转**为 `LATTICE_SIZE`(**归 6** —— 2026-09-16 `/consistency-check` C-1 订正;原文写「ADR-015」是幽灵引用);**y 轴显式豁免**(自由落体可远超 `SPEED_MAX`) | F-1-1a 订正 ①② · F-1-1c · `AC-1-06a/b/c` |
| 5 | **`CharacterController` 参数从未命名** | **新立 F-1-9**:`slopeLimit`(语义归 **ADR-015** 逻辑层几何)· `stepOffset`(**归关卡几何,不归 1**)· `minMoveDistance = 0` · `skinWidth`;加载期**同源校验**,不一致即硬失败 | F-1-9 · `AC-1-33` ② · `O-9` → ADR-015 **§一之补** |
| 6 | **接地模型三命题不相容** | **新立 R12** + **`OQ-1-12`**(🔴 P0 开工前须裁)—— 单独起最小 spike 实测 `isGrounded` 更新时机 / `Vector3.zero` 调用的推挤 / 坡面行为,**不与 ADR-012 F7 混批** | R12 · `OQ-1-12` · `AC-1-29`(ADVISORY · 前向登记) |

### 用户裁定(2026-09-16,四项;经一次多标签问答)

| # | 议题 | 裁定 |
|---|------|------|
| ① | 下一步 | **现在修**(本会话内逐项修订;**P0 五条 blocker 与三处裁定合并为一次提问**,不中途打断) |
| ② | **冲刺档(`SPEED_SPRINT`)** | **P0 整档砍掉** —— 初稿 Sprint **无代价** ⇒ `SPEED_SPRINT > SPEED_WALK` **恒成立** ⇒ **严格支配 Walk** ⇒ Walk 沦为死内容,与**支柱一**冲突。**P1a 恢复的前置 = 先设计代价**(须与地貌 / 情境**并列**第三条修改源,或对 Walk / Sprint 非对称乘数)⇒ `OQ-1-11` |
| ③ | **接地模型**(三命题不相容) | **推迟到 spike 后再定** —— 见根因 6 处置;`OQ-1-12` | 
| ④ | **移动基向量** | **相机相对**(1 读 2 的**只读 yaw basis**;非世界锁定、非输入锁定)⇒ F-1-8;系统 2 须交付 `ICameraRig.YawBasis` ⇒ **`O-8`** |

### 因根因 3 / 4 派生的口径订正(跨 6 文件,本轮一并落)

1. **「事件频率 = 格穿越率」→「事件率上界 = tick 频率」** —— 归并算符的存在使上界与帧率 / 位移距离**解耦**。
   波及:本 GDD(Overview / R5 / EC-2 / EC-4)· **ADR-009 Amendment G** · **ADR-016 §三** ·
   **ADR-020 §四 / `AC-20-04`** · `architecture.yaml` ×2 · `entities.yaml` · `technical-preferences.md` ×2 ·
   `traceability-index.md`。
2. **判据口径订正:「grep 无 `Vector3` → `Append`」→「反射断言载荷字段类型」** ——
   原判据是**假阳性机器**:F-1-6 的跨格检测**必然读 `Vector3`**(渲染层位置),而它**不写流**。
   判据 = 反射扫描 `SimEvent` 载荷的**字段类型**,**不是** grep 源文本。

### AC 计量变化

| | 首轮落盘 | 评审后 | 说明 |
|---|---------|--------|------|
| 编号 | 28 | **35** | 新增 `AC-1-29…35`(六条根因逐条派生) |
| 条目 | 30 | **38** | `AC-1-06` 拆 `a/b/c`(+2)· `AC-1-20` 拆 `a/b`(+1) |
| BLOCKING | 26 | **32** | |
| ADVISORY | 4 | **5** | `AC-1-29` 新增(接地模型前向登记) |
| EXTERNAL | 0 | **1** | `AC-1-22` 改判(`OQ-1-8` 的 29 / 25 / 45 三条上界) |

分组:`A` = `01·02·03·28·30·31` · `B` = `06a/b/c·07·09·10·11·12·33` · `C` = `04·05·08·13·14·15·16·17·32·34` ·
`D` = `18·19·20a·20b·21·35` · `E` = `22(EXTERNAL)·23·24` · `F` = `25·26` · `G` = `27·29`。
**编号不按组连续**(`04` / `05` 属 C 组却在 B 组区间内)—— 编号是**登记的稳定 id,增删不重排**,错位是**预期**。

### 落盘的涟漪(ADR / registry 面)

| 落点 | 内容 |
|------|------|
| **ADR-005 Amendment F** | 开**边界程序集**(`WorldPos` + 六个 P0 抽象点,零 `UnityEngine`);门 A 是**单向**约束(只约束 sim 实现程序集) |
| **ADR-006 Amendment F** | **算术域 vs 边界域切分** —— §一/§三/§四(定点算术)**不适用**于本系统;§二(边界)**适用** |
| **ADR-009 Amendment G** | `ActorCellEntered` 登记为第 **10** 个 `Kind`;`tick` 语义收窄为「**观察到**跨格的那一 tick」;**频率口径订正**为上界 = tick 频率 |
| **ADR-015 §一之补** | **可走性契约** —— `slopeLimit` / `stepOffset` 的语义 + 归属 + **同源纪律**(两侧不一致 = 装载失败);§Validation 增「可走性同源 BLOCKING」(**结清 `O-9`**) |
| **ADR-020 §四 + `AC-20-03/04`** | 频率口径订正;新增 **`AC-20-13`**(跨格 `Append` 权 = 主机唯一)与 **`AC-20-14`**(`YawBasis` 只读 + 水平化正交归一) |
| **ADR-016 §三** | 频率口径订正 ×3 处 |
| `architecture.yaml` | `player_presentation_motion` 写权补「主机唯一 Append」;`camera_readonly_view` 接口补 `YawBasis`;`player_cell_crossing_event` 生产者 / 消费者 / 签名 / `referenced_by` 全量刷新;`player_movement_model` 可走性措辞订正(**`OQ-1-2` 出处订正**) |
| `entities.yaml` | `SimEvent.Kind.ActorCellEntered` constraint 补主机唯一 Append + 非可靠通道 + `tick` 语义 |
| `tr-registry.yaml` | **+4 条(217 → 221)**:`TR-player-005`(移动基向量 · ⚠️ partial)· `TR-player-006`(主机唯一 Append · ✅ covered)· `TR-player-007`(`CharacterController` 参数契约 · ⚠️ partial)· `TR-camera-006`(`YawBasis` · ⚠️ partial)。累计 **124 ✅ / 21 ⚠️ / 76 ❌ / 221** |
| `traceability-index.md` | §8 明细 + 汇总 + 优先修复清单 + 变更历史同步 |
| `systems-index.md` | 系统 1 行状态 + §11 队列表 + 计数 |
| `input-system.md` | 规则二清单补 `Sprint` / `Jump` 两个动作,`Sprint` 标 **⚠️ 无 P0 消费者**(**结清 `O-7`**) |
| `technical-preferences.md` | ADR-016 条目频率口径订正 · ADR-020 条目判据改「反射断言,不是 grep」+ Append 权 |

### 未决项(2026-09-16)

- 🔴 **`OQ-1-12`(接地模型)** —— **P0 开工前唯一真阻塞**。须单独起最小 spike,结果回填
  轴 2 表进入条件 + EC-1①/EC-9/EC-10/EC-11 四条写法 + `AC-1-08`/`AC-1-17` 判据 +
  `AC-1-21`③「贴墙分离」实测口径。**不与 ADR-012 F7 混批。**
- **`OQ-1-8`(传送频次上界)** —— `EXTERNAL`。其主语(29 / 25 / 45)三条**均无 GDD** ⇒
  `AC-1-22` 在三者侧**不可签署**;EC-4 的有界性论证**当前只有 1 这一半**。
- **登记义务仍悬空 = `O-1…O-6` + `O-8`** —— 执行点全在**尚无 GDD 的系统**里
  (4 / 10 / 24 / 25 / 27 / 29 / 44 / 45);**`O-8`**(2 须交付 `YawBasis`)是其中唯一
  会让**游戏不可玩**的(缺它则移动方向无定义,而 GDD 全程**可编译、可测试通过**)。
- **`O-7` / `O-9` / `O-10`** —— ✅ **本轮结清**(3 的绑定悬空 · ADR-015 可走性点名 · ADR-009 `tick` 措辞)。
- **P0 其余待裁** —— `OQ-1-1`(移动无跳过路径是否成立)· `OQ-1-3`(VR room-scale 归 P1a)·
  `OQ-1-4`(源头消抖探针)· `OQ-1-6`(5 天气是否 P0 影响走感)· `OQ-1-7`(25 击退注入权)·
  `OQ-1-10` / `OQ-1-13` / `OQ-1-14`(手感层,首次 playtest)。

Prior verdict resolved: First review —— 六条根因当日**全部修订落盘**(GDD + 4 份 ADR Amendment
+ 5 份 registry / 索引面)。**待用户裁定是否复审**;系统 1 保持 **In Review**,未标 Approved。
**未提交**(硬约束:不主动提交,等用户指令)。

---

## Review — 2026-09-16(第二轮)—— Verdict: NEEDS REVISION

Scope signal: **S**(三条阻塞全在**一处泄漏面** —— 首轮修订的涟漪未跨文件收尾;
无新设计决策、无用户裁定、无新增 ADR)
Specialists: **无**(`--depth lean` —— 单会话分析,不派子代理)
Blocking items: **3** | Recommended: **4** | Prior verdict resolved: **大部分是** ——
首轮六条根因的**本 GDD 内**修订均已落盘且自洽;**但四处外溢承接没跟上**(见下)。

Summary:首轮修订的**质量很高、本文件内自洽** —— 三条不变量(位移=表现态 / 唯一 sim 投影 =
跨格事件 / 主机唯一 Append)在 GDD 内闭环,公式链(F-1-1a→F-1-1c→F-1-2→F-1-3→F-1-4→F-1-8)
无残留量纲错。**本轮缴获全在"改了 A 但没改引用 A 的 B"这一类**:
① `TICK_PERIOD → MAX_DT` 的订正**只落在本 GDD 与评审日志**,而 `adr-009:455` 与
`entities.yaml:596` **仍在教旧变量** —— 那正是首轮根因 4 自称要防的**静默失效**(用 tick 做
隧穿上界,帧率 < tick 频率时断言通过而隧穿照发生);且 `entities.yaml` 的公式还**多带一个
`SPEED_SPRINT`**(R7 已砍档,该符号在 P0 不存在)。
② `AC-1-03` ② 的合取项 `事件数 ≤ 相异格数` 与 **EC-3(回访再发一条)** 数学上不相容 ——
`A→B→A→B` 发 3 条而相异格只有 2 个 ⇒ **正确实现会被判失败**;另有两处同文副本
(`adr-020:286` · `tr-registry:1419`)。
③ `JUMP_HEIGHT_MAX` 被引用于 `AC-1-11` ② 与 EC-8 的作废说明,但 F-1-5 **从未定义它**
(R7 砍档时连带丢失)—— 符号悬空。

### Blocking Items → 处置表(2026-09-16 当日完成)

| # | 发现 | 处置 | 落点 |
|---|------|------|------|
| B1 | **`TICK_PERIOD` 残留**(首轮根因 4 的外溢未收尾) | `adr-009:455` 就地订正为 `SPEED_MAX × MAX_DT ≤ LATTICE_SIZE` 并写明"用 `TICK_PERIOD` 是**用错变量**";`entities.yaml` 公式 `SPEED_SPRINT`→`SPEED_MODE_MAX`、`TICK_PERIOD`→`MAX_DT`,注释补两条订正理由 + 所有权反转 | `adr-009` Amendment G §三 · `entities.yaml` `horizontal_antitunneling_invariant` + `SimEvent.Kind.ActorCellEntered` |
| B2 | **`AC-1-03` ② 不可满足**(与 EC-3 相抵) | 删去 `≤ 相异格数`,改为**逐 tick 核对**「`Append` 的格 == 该 tick 边沿的 `pending_cell`」+ 写明正确不变量是**序列**性质(相邻不同格转移数)而非集合大小 | GDD `AC-1-03` ② / R5 注 / `adr-020:286` / `tr-registry:1419` |
| B3 | **`JUMP_HEIGHT_MAX` 无定义**(R7 砍档的连带丢失) | 在 F-1-5 补闭式(按情况甲 / 乙两支),使 `AC-1-11` ② 的 `JUMP_HEIGHT_MIN ≤ JUMP_HEIGHT_MAX` 可求值;EC-8 的作废说明改写为"作废理由是**方向错**,不是符号缺定义" | GDD F-1-5 派生量块 · EC-8 注 |

### Recommended Revisions → 处置表

| # | 发现 | 处置 | 落点 |
|---|------|------|------|
| R1 | 客户端本地 `last_committed_cell` 与主机权威态**无同步纪律** —— 预测回滚可能让主机 Append 一条权威从未到过的格 | 新增**提交态归主机**三分句:客户端只上行 `pending_cell`;主机 `last_committed_cell` 只由自己的 Append 推进(相同值丢弃);`AC-1-30` 补反向用例③ | GDD §States 归并块末 · `AC-1-30` ③ · `AC-20-13` |
| R2 | `adr-016:429` 的 Validation 项仍写「事件数 **=** 格穿越数」(首轮已订正正文 :194,漏了这里) | 就地改为「上界 = tick 频率」+ 订正理由 | `adr-016` §Validation |
| R3 | F-1-7 值表层列了 `K_terrain_turn?`(带问号),而 F-1-4 的 `TURN_RATE` **只吃什么乘数都没说** —— 地貌是否影响转向未定 | 从值表层删去该可选键(F-1-4 只吃 `K_context_turn`;地貌若日后要影响转向,须与 F-1-1a 同级重核) | GDD F-1-7 值表层 · 交叉见 `OQ-1-13` |
| R4 | `patient-ai.md:459` 仍写「1 …… ❌ 无 GDD」,而 1 的 GDD 已于同日落盘 | 就地更新为 ✅ + 注明事件率上界口径(不是"= 格穿越率") | `patient-ai.md` §Dependencies 上游表 |

### 未决项(本轮结束后)

- 🔴 **`OQ-1-12`(接地模型)— 仍是系统 1 唯一的 P0 开工阻塞级未决项**。须单独起最小 spike,
  结果回填轴 2 表进入条件 + EC-1① / EC-9 / EC-10 / EC-11 四条写法 + `AC-1-08` /
  `AC-1-17` 判据 + `AC-1-21`③「贴墙分离」实测口径。**不与 ADR-012 F7 混批。**
- **`OQ-1-8`(传送频次上界,`EXTERNAL`)** —— 主语(29 / 25 / 45)三条**均无 GDD**;
  EC-4 的有界性论证当前**只有 1 这一半**。
- **登记义务仍悬空 = `O-1…O-6` + `O-8`** —— 执行点全在尚无 GDD 的系统里;
  **`O-8`(2 须交付 `YawBasis`)是其中唯一会让游戏不可玩的**(缺它则移动方向无定义)。
- **建议第三轮复审用新会话**(本轮为 `lean` 单会话,未派对抗式子代理;
  自评与对首轮修订的自评共享同一上下文,**独立性弱于首轮**)。

Prior verdict resolved: **Yes,基本** —— 首轮六条根因的 GDD 内修订经本轮逐条核验**全部在位且自洽**;
本轮三条阻塞**均为其外溢承接的收尾**,非设计缺陷。

---

## Review — 2026-09-16(结案)—— Verdict: APPROVED

Scope signal: **S**(无新增设计内容 —— 本轮仅用户裁定接受修订并结案)
Specialists: **无**(用户裁定;不开复审)
Blocking items: **0** | Recommended: **0** | Prior verdict resolved: **Yes** ——
第二轮 3 条阻塞 + 4 条建议**当日全部修订落盘**,本轮无残留项。

Summary:**用户裁定接受两轮修订,不再重评审 → 系统 1 结案 Approved。**
第二轮缴获的三条阻塞(`TICK_PERIOD` 残留 / `AC-1-03`② 不可满足合取项 / `JUMP_HEIGHT_MAX` 无定义)
全属**首轮修订的外溢承接未收尾**,已跨 7 文件就地订正;四条建议亦一并落盘。
**遗留风险显式登记为硬前置门**(非阻塞 —— 不阻碍 GDD 结案,但阻碍 P0 开工):

- 🔴 **`OQ-1-12`(接地模型三命题不相容)** —— **P0 开工前须单独起最小 spike** 裁决;
  结果回填轴 2 表进入条件 + EC-1① / EC-9 / EC-10 / EC-11 四条写法 + `AC-1-08` / `AC-1-17` 判据 +
  `AC-1-21`③「贴墙分离」实测口径。**不与 ADR-012 F7 混批。**
- **`OQ-1-8`(传送频次上界,`EXTERNAL`)** —— 其主语(29 / 25 / 45)三条**均无 GDD**;
  EC-4 的有界性论证当前**只有 1 这一半**;`AC-1-22` 在三者侧不可签署。
- **登记义务仍悬空 = `O-1…O-6` + `O-8`** —— 执行点全在尚无 GDD 的系统里;
  **`O-8`**(2 须交付 `YawBasis`)是其中唯一会让**游戏不可玩**的。

**系统 1 状态更新**:`In Review` → **`Approved`**(`systems-index.md` 行 1 + §11 队列 + §7 计数 + GDD 头 Status 块已同步)。
**未提交**(硬约束:不主动提交,等用户指令)。
