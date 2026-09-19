# 音频系统(系统 44)评审日志

> 评审史轨道:`design/gdd/reviews/[doc-name]-review-log.md`。每条评审按时间倒序追加。

## Review — 2026-09-18 — **首轮评审:MAJOR REVISION NEEDED →(Scope L · 8 项阻断)同日全修并落盘 → 用户裁定接受修订、免二轮 → ✅ Approved**
Scope signal: **L**
Specialists: **8 位 adversarial(full 模式)+ creative-director 综合终裁**
Blocking items: **8** | Recommended: 16(同批修)
Summary: 首轮 `/design-review --depth full`。8 位专家逐域对抗 + creative-director 综合,裁定
**MAJOR REVISION NEEDED**。**核心结构洞察**(CD 综合原话要点):「设计对『**不得做什么**』的规格
远严于『**必须做什么**』。这个反转就是整个裁决 —— 幻想的主通道(门后呼吸)没有总线、没有学习路径、
被埋在优先级表里。」**三条系统性根因**:① 幻想主通道缺载体;② `AudioListener` 单实例约束被误读为
**总线数约束**(伪前提),据此否掉了「各按本机技能档」这条听觉成长轴;③ 事件表 schema 缺
`trigger_source`,白名单只能捕获**具名**触发源,**基于时序**的状态播报(阈值跨越等)从 prose 缝隙漏过。
**8 项阻断**(来源标注):F-44.2 公式量纲破裂 · AC-44-09 不可执行且可绕过 · 「单 Listener ⇒ 每设备一条总线」
为假 · 幻想主通道无总线 · TierMap 列集自相矛盾 · Intensity 无声学映射 · 远程 cue 来源 / 病人锚点未声明 ·
DTO 引用 sim 类型 + AC-B1 grep 退步。**同日全修**,4 项用户 D 裁决照准。**跨 ADR 涟漪**:ADR-018
(§三 七总线 · §五 理由重写 · §六 加 `trigger_source` · §七 P1a→P1b · §一 删 `VitalsDto` · Alt-4 由驳回改采纳)·
**ADR-001**(§一之二 新增 `IPositionalChannel` —— per-`ActorId` latest-value 表 + 消费者登记 + 病人 / 受伤实体
锚点发布者 + cue 非复制;原文零处提及音频,阻断 7 的根因)。
Prior verdict resolved: **First review**

### 用户四项 D 裁决(2026-09-18,均照准)

| # | 裁决 | 含义 |
|---|------|------|
| **D-A** | **重开 2026-09-14 联机裁决 → 本地档** | `SetTier(TierSource.Local)`:各设备按本机玩家技能档渲染听诊音 —— 保住听觉侧熟练度成长轴;原「统一取主机技能」的伪前提已证伪 |
| **D-B** | **优先级 tie-break 改距离归一化** | 单一定义的优先级表,平局按距离归一 —— 原「关键 cue」措辞撤销 |
| **D-C** | **接受无障碍 P0 非目标** | 三条具名项 + 显式 P0 非目标(「隔墙听觉检测在 P0 无视觉等价物」);ADVISORY |
| **D-D** | **现改 ADR-001** | 第二 QoS 表化 + 44 消费者登记 + 锚点发布者 + cue 非复制 |

### 8 项阻断 → 修法(全量)

| # | 阻断(来源) | 修法 | 落点 |
|---|--------------|------|------|
| 1 | **F-44.2 公式量纲破裂**(systems-designer / audio-director / sound-designer) | dB/dB 除法 → 线性化后相减:`SNR_dB = SIGNAL_dB − 10·log10(10^(NF/10)+10^(CN/10))`,钳位 ±24 dB | §F-44.2 |
| 2 | **AC-44-09 不可执行 + 可绕过**(sound-designer / qa-lead / audio-director) | schema 增 `whitelist_category` + **`trigger_source`**;`OQ-44-4` 升格为 §Event Table Schema;双重负向夹具 + NOT-RUN 守卫 | §规则二 · §Event Table Schema · AC-44-09 |
| 3 | **「单 Listener ⇒ 每设备一条总线」为假**(unity-specialist / game-designer / network-programmer) | 重开 2026-09-14 裁决 → `SetTier(TierSource.Local)`;ADR-018 §五 理由重写、Alt-4 拒绝理由改 | §规则七 · AC-44-07 · ADR-018 §五 |
| 4 | **幻想主通道无总线**(audio-director / game-designer) | 新增 **F-44.7 世界语境呼吸路径**(3D + 遮挡低通 + 衰减 + 多病人规则) | §F-44.7 · AC-44-D8 |
| 5 | **TierMap 列集自相矛盾 / 悬空符号**(sound-designer / systems-designer) | 定型唯一列集;删 `breathLayer`;钉 Hz/dB 量纲;`Tier` 移出 DTO;ramp ≥50 ms | §F-44.1 · §F-44.5 |
| 6 | **Intensity 无声学映射**(sound-designer) | 新增 **F-44.6**(增益 / 密度 / 分桶) | §F-44.6 |
| 7 | **远程 cue 来源 / 病人锚点未声明**(network-programmer) | 写清 **cue 不复制**(客户端自派生)+ 病人锚点发布者;**ADR-001 现改** | §Edge Cases · AC-44-D7 · ADR-001 §一之二 |
| 8 | **DTO 引用 sim 类型 / AC-B1 grep 退步**(unity-specialist / qa-lead) | 定型独立契约程序集(仅 BCL);AC-44-B1 升为 asmdef 白名单 + IL 扫描 | §Interactions 注② · AC-44-B1 |

### 推荐项同批修

`VitalsDto` 幽灵入口删除(规则一)· `DialogueFocus` 快照自相矛盾(规则三 / AC-44-C1)·
`EndLoop` 永不到达 · 咳嗽 vs 呼吸层相位锁定(AC-44-14)· 快照 / 脚本参数归属分离 ·
优先级表合并 + 删「关键 cue」· 无障碍三条具名项 + P0 非目标(D-C)· VR P1a/P1b 三方矛盾 ·
证据类型 `[A]/[L]` 全组标注 · 缺失素材族(把脉 / 脚步矩阵 / 建筑 / 敌人 / 动物 / 玩家自身 / 天气 / 混响)·
AC 计数 27 → 32 · `OQ-44-3` 消解 / `OQ-44-4` 升格 / `OQ-44-8` 新增 · Addressables 卡顿边界。

### 登记面涟漪(同批落盘)

`audio-system.md`(主)· `adr-018`(§三 / §五 / §六 / §七 / §一 / Alt-4 / References)· **`adr-001`(§一之二)** ·
`tr-registry.yaml`(`TR-concept-002` 修订 · `TR-diag-024` 改判 · `TR-diag-025` 硬化)· `traceability-index.md`
(§5 五行回填 · 变更历史 2026-09-18 行)· `architecture.yaml`(`replay_pipe` 缩窄 + **新 `positional_channel` 契约**)·
`systems-index.md`(行 44 / 行 45 的「ADR-001 未定」/ §2 行 44 / §设计序 20)。

### 结案(2026-09-18):用户裁定接受修订、**免二轮** → ✅ Approved

- **免二轮 = 显式风险接受,不是「已核对」** —— 8 项阻断由**撰写 + 修订的同一上下文**判修,**未经独立复核清点**。
- **重开触发条件**(任一命中即须开新会话跑二轮 `/design-review`):
  1. **门 A / 契约程序集实证失败** —— AC-44-B1 的 asmdef 白名单 + IL 扫描在实现期拦下 `AudioCueDto` 引 sim 类型(原阻断 8)。
  2. **`IPositionalChannel` 被 45 的实现证伪** —— 表化 latest-value 在 P1b 真实网络条件下无法承载 per-`ActorId` 锚点(原阻断 7)。
  3. **白名单断言被绕过** —— AC-44-09 在**非具名触发源**(基于时序的状态播报)上出现 false-negative,证明 `trigger_source` 仍不足。
  4. **`SetTier(TierSource.Local)` 在联机实现中被证伪** —— 听觉熟练度成长轴(裁定 D-A)在 4 人同场下产生不可接受分叉。

### 残留未还清判据(非阻断,排期项)

`TICK_SECONDS`(D-8-7)· `Project(Sign_j) → [0,1]`(D-8-4)· 声学方向 / sound-bible 实例(与 `/art-bible` 同批)·
`OQ-44-8` 发布者实现随 45 走 P1b(契约已定,代码未写)。
