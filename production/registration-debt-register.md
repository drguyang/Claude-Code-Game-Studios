# 登记债总册(Registration Debt Register)

> **用途**:全案**未结登记债**的唯一真源。评审 / 复核 / 观察项产出的「登记不修」项一律入此册,
> 避免散落在 `active.md` / GDD / 评审原件里无人收口。
> **清理纪律**:每项须写「归属轮」(哪个批次 / 哪一轮处理);**归属轮到达时本册是收口清单**。
> 已结项标 ✅ 并留结案锚点(提交 / 原件),不删行(审计轨迹)。

---

## 一、未结登记债(Active)

### A. M2 接线轮 · 阶段 2(A/B/C/D/E 五批 · 2026-10-10 收口未提交)

| ID | 来源 | 内容 | 归属轮 | 备注 |
|---|---|---|---|---|
| AE-测-2 | A+E 评审 | 确定性复算测判别力≈0(纯函数连调两次恒等) | 不列为证据 | 已登;非阻塞 |
| AE-测-3 | A+E 评审 | SeriesTerms 10→9 截断 ≪1 LSB 不可观测 | 可接受 | ε 内盲区 |
| AE-码-3连带 | A+E 评审 | 「施救时病人离场 + 满 CAP」边界未穿真 EventStream | 批次 C 已确认 | 语义已由 C 确认 |
| AE-Process | A+E 评审 | Process 方法 60 行超 40 行 | 后续提私有方法 | 既有状态 |
| BCD-码-1 ✅ | BCD 评审 | GDD 校验 12 `R_rise>ε_MIN` 未进写入期 | ~~数值轮~~ | **2026-10-10 结案**(用户授权「值须你裁」):ε_MIN = **1/65536(Q16.16 raw=1)** 定值入 GDD Tuning §六 + **R1-30** 写入期校验(计算形与求值侧 ExpNeg 逐位同源 ⇒ fail-loud 变子集兜底)+ 双测(负:比值舍 0 ⇒ R_rise 精确 0 抛 RuleNumber 30;正:≈6.5 LSB 不误拒);MUT-R1-30 恰红 1(`test_registry_rRiseAtEpsilonMin_throwsRule30`),还原 SHA-256 逐位一致;锚点 = DiseaseSimulation 134/134(工作树,未提交);GDD 三件套(校验 12 / F3 注 / Tuning §六)同批同步 |
| BCD-码-2 ✅ | BCD 评审 | 玩家事件头 stamp 帧末 `CurrentTick` 与边沿 tick 不同源 | ~~接线轮~~ | **2026-10-10 结案**:`OnTickEdge(long tick)` 签名 + `MovementFeed` 逐边沿回推 `edgeTick`;锚点 = PlayerController 过滤跑 99/96/0 红(工作树,未提交) |
| BCD-码-3 ✅ | BCD 评审 | 流按 Tick 非降不变量无断言 | ~~建议 Append 加单调断言~~ | **2026-10-10 结案**:`EventStream.Append` 去重后/入列表前单调断言 + `Clear` 复位 + `event_stream_test` 新增 3 条;唯一违例 `case_replay_persistence_test` 改 tick 升序;变异恰红 1;锚点 = DiseaseSimulation 132/132(工作树,未提交) |
| BCD-码-4 ✅ | BCD 评审 | b4 asmDirs/白名单缺 `Gameplay.Boot` | ~~gate 轮~~ | **2026-10-10 结案**:`ToFloatScanDirs` 提为字段 + 补 Boot 面 + 白名单补 `Gameplay.Boot`;新增 `assembly_gate_b4_test` 4 条;MUT-b4 恰红 1;锚点 = Unit.Sim 159/159(工作树,未提交) |
| BCD-码-5 ✅ | BCD 评审 | WriterExistenceGate 扫描面缺 Sim.Contracts | ~~gate 轮~~ | **2026-10-10 结案**:`WriterScanDirs` 补 `Assets/Sim.Contracts`(实测该面零命中,不改绿态)+ 结构测试收紧六面全断言;MUT-b7 恰红 1;锚点 = Unit.Sim 159/159(工作树,未提交) |
| BCD-测-1 | BCD 评审 | B 时点 `m2b_disease.xml` 是过滤跑非全量 | 措辞登记 | 已被 C/D 全量重证 |
| BCD-测-2 | BCD 评审 | b7 对 24 豁免 Kind 零覆盖 | 设计可接受 | 三重防护已验 |
| BCD-C-GDD ✅ | C 批交付 | GDD 缺口 8 项(MAX_ACTIVE_DOSE/treatable_by 装载面/极性双源/复发计数无 Kind 等) | ~~9 的 GDD 轮~~ | **2026-10-10 结案**(权威清单 = 批次 C 报告 §四):8 项全数落 `disease-simulation.md` Debt Register **D-9-K…D-9-R** —— 3 项本批裁定(K 极性双源 = **轴为准** / L 极性序数 = **Symptomatic=0·Causal=1 锚 11** / M 复发次数 = **派生态闭式 clamp(⌊τ/relapse_interval⌋−1, ≥0)** 零新 Kind)+ 1 项办结(N = ε_MIN + R1-30,即码-1)+ 4 项登记归属(O 剂量 clamp 值+装载面 → 数值轮+9 装载轮 · P treatable_by → 9 装载轮 · Q F2 上臂盲区 · R signs 阈值 → 8+接线轮);裁定正文同步至 F1 取舍 4 / AC-28 / F1 复发式注 |
| BCD-D-豁免 | D 批交付 | 豁免表 24 条(每条带日期理由 + 已写者禁豁免) | 已写者禁豁免 | b7 三假绿防护;**2026-10-10 复核:无独立修复动作** —— 判据①「已写者禁豁免」②③专测已在 Unit.Sim 159/159 绿,撤条随各归属轮写者落地时机械强制,保持监控 |
| BCD-C-其他 | C 批交付 | ~~b4 门补 Boot 面~~(✅ 2026-10-10 随 BCD-码-4 结案)· 9 GDD 轮 · 数值轮 · 7 项 | 各归其轮 | b4 半边已结 |

### B. O-6 观察项修复轮(`f148fab` · 已收口)

| ID | 来源 | 内容 | 归属轮 | 备注 |
|---|---|---|---|---|
| O6-11项 | O-6 评审 | 扫描面未含 `Gameplay.UI` · 块注释剥除行号下偏 · `EndsWith` 豁免偏松 · 重编码重发去重→45 轮 · 组合层零接线→接线轮 · 同 actor 双写者二选一→接线轮 · 重复 `<summary>` · 文件系统探针=豁免型 · 多 actor×多 tick 真流有界性 · 存档/网络 blob 重映射 round-trip(7a/45 轮) | 各归其轮 | 原件 §二之二 |

### C. GDD 内联登记债( scattered · 待收口到本册)

| ID | 来源 | 内容 | 归属轮 | 备注 |
|---|---|---|---|---|
| D-21-26 | `design/gdd/inventory-and-items.md` | `IIdAuthority` / `ItemInstanceId.Next()` 21a 侧登记债未回收 | 数值轮 / 21a 轮 | 三处(:207/:621/:895)同条 |
| D-8-11 | `design/gdd/diagnosis-system.md` | 试药史是判据之一不是扣分项 → 归 #53 结算 | **#53 结算轮** | :1243/:2051 同条 |
| skill-30 | `design/gdd/skill-system.md` | 30 侧欠两项:① 等级→`EFF` 映射(形状已裁,剩逐档填表)② `EmitGrowth` 入口存在性断言 | 30 数值轮 | :139 |
| PC-债务 | `production/epics/player-controller/story-001` | `Sim` 引用仍在,豁免是已登记债务的显式化 | story-001 轮 | :47 |

### D. 其他散落登记(active.md 内联 · 待收口)

| ID | 来源 | 内容 | 归属轮 | 备注 |
|---|---|---|---|---|
| 静置骑 | sprint-04 :82 | 生产静置骑 y 格界(结构性) | 门④ PlayMode 实测 | 挂 EC-1/AC-1-21 |
| OnPositionSample | sprint-04 :58 | 跨格写者运行期不发 | Phase 1 尾/Phase 2 | 6 项 |
| 新架构缺口 | sprint-04 :297 | 未结待裁 | 待用户裁 | — |
| 未闭色值 | sprint-04 :1094 | `--skeuo-brass-aged` 与 art-bible 不一致 | 待裁 | — |
| C7 已注册类 | sprint-04 :1156 | 黄铜/器具/焦点族零登记表 | 待裁 | 决定 019-d 接图量 |
| S3 落点 | sprint-04 :1758 | S3 落点待裁(新 story-005 或挂 tick 接线 epic) | 归 TD 裁 | — |

---

## 二、已结登记债(Closed · 留审计轨迹)

| ID | 结案 | 锚点 |
|---|---|---|
| — | — | — |

---

## 三、维护规则

1. **新债入册**:评审 / 复核 / 观察项产出「登记不修」时,同时往本册加一行(来源 + 内容 + 归属轮)。
2. **归属轮到达**:按本册「归属轮」列逐项收口;结案标 ✅ + 锚点(提交号 / 原件路径)。
3. **不静默删除**:结案行不删,只标状态;audit trail 完整。
4. **唯一真源**:GDD / active.md 里的「登记债」字样指向本册,不各自维护清单。

> **当前未结总数**:A 类 14 项中 **4 项已结**(码-2/3/4/5)⇒ 未结 ≈ **21 项**(去重前)。
> **最近清理**:2026-10-10 · 由「清理登记债」指令创建。
> **2026-10-10 收口批**:BCD-码-2(接线轮)· BCD-码-3(Append 单调断言)· BCD-码-4(b4 补
> Boot 面)· BCD-码-5(b7 补 Sim.Contracts 面 + 六面收紧)—— 均为**代码修复 + 测试 + 变异实测**
> (三发变异各恰红 1)。
> **2026-10-10 第二收口批**:BCD-码-1(ε_MIN = 1/65536 + R1-30 + 双测,MUT 恰红 1)·
> BCD-C-GDD(8 项全落 GDD,D-9-K…R:3 裁定 + 1 办结 + 4 登记归属)· BCD-D-豁免
> (复核 = 无独立修复动作,转监控)。**A 类无「待用户决策」项残留** —— 剩余 open 项均已
> 归属到具体轮次(数值轮 / 9 装载轮 / 8+接线轮 / 各归属轮写者落地撤条)。
