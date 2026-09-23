# 《大医精诚:破晓之剂》 — Master Architecture

> **本文件是全案唯一的整系统蓝图** —— 它把 31 项 P0 GDD 与 **25** 份 ADR 翻译成
> 一份可实现的技术骨架。ADR 记录**点裁决**,本文件给出这些点所在的**面**。

## Document Status

- Version: 1.0(骨架建立于 2026-09-20)
- Last Updated: 2026-09-23
- Engine: Unity 6.3 LTS(URP)· C# · IL2CPP
- GDDs Covered: P0 31 项(见 §System Layer Map 的「GDD」列);另有 23 项 P1a/P1b/P2 登记于层图但不在本蓝图承诺面内
- ADRs Referenced: ADR-001 · ADR-005 … ADR-**028**(共 **25** 份文件;**ADR-002 / 003 / 004 无独立文件** —— 分别由 ADR-015 / ADR-015 / ADR-017 兑现结案)
- Technical Director Sign-Off: **2026-09-20 — APPROVED WITH CONDITIONS**(条件 C1–C4 见下)
- Lead Programmer Feasibility: **LP-FEASIBILITY skipped — Lean mode**(非 PHASE-GATE,按门规 `lean` 下跳过)

> **条件(C1–C4,全部已在本文件内有落点;不阻塞交接到下一阶段,但阻塞其对应 ADR 的撰写)**
>
> | # | 条件 | 落点 |
> |---|---|---|
> | **C1** | ~~先修 ADR-008 的后补边~~ **✅ 2026-09-20 已执行**(三条,非两条 —— 见 §5.2 复算注):`adr-008:34` 的 `Depends On` 清空为 `{005,006,007}`,三段后补内容移入 `Ordering Note` 标「引用/修订记录(非前置)」;Tarjan 复算**全盘无环** | §ADR Audit 5.2 · QQ-04(**已结**)|
> | **C2** | ~~写 Required ADR #1(渲染与场景加载)与 #2(契约程序集清单)之前不开工~~ **✅ 2026-09-20 结案**:ADR-023(#1,用户照准 ①⑥ 转 Accepted·附条件 = S1–S4 实测回填归实现故事前置)· ADR-025(#2,全件照准转 Accepted) | §Required ADRs #1 / #2 · QQ-01 / QQ-03(**均已裁**)|
> | **C3** | ~~三流 `Kind` 单一登记真源须在第一个 `IEventSink` 实现之前裁定~~ **✅ 2026-09-20 结案**:ADR-024 六项全收转 Accepted(真源 = `entities.yaml`;Amendment 通道退役;补齐 + 生成器断言) | §ADR Audit 5.5 D-1 · §Required ADRs #3 · QQ-05(**已结**)|
> | **C4** | ~~8 条「借绿」条目须转 `partial` + `BLOCKED-BY`~~ **✅ 2026-09-20 已执行**(8/8;两文件计数已复算一致 243/51/93) | §ADR Audit 5.5 D-5 · QQ-09(**已结**)|
>
> **C1–C4 的共同性质**:四条**全部是「登记面与裁决面不一致」**,**零数值冲突、零设计分歧** ——
> 即它们不改变任何已裁的技术方向,只把记账对齐。
> **✅ 2026-09-20:四条全部结案**(C1/C4 = 同日执行;C2/C3 = 同日逐份裁定轮,ADR-023/024/025 转 Accepted)。
>
> **本文件的撰写状态 = v1.0 全节落盘**(节 §Document Status … §Open Questions 共 11 节)。

---

## Engine Knowledge Gap Summary

> 依据:`docs/engine-reference/unity/VERSION.md`(project pinned 2026-02-13)· `breaking-changes.md`
> · `deprecated-apis.md` · `current-best-practices.md` · `modules/`。

| 项 | 值 |
|---|---|
| **引擎** | Unity 6.3 LTS(2025-12 发布) |
| **LLM 训练覆盖** | 约至 2022 LTS(2022.3) |
| **知识断代** | 整个 Unity 6 系(原 2023 Tech Stream) |

### HIGH RISK 域(须先对引擎文档核验再落地)

| 域 | 关键变化 | 本蓝图触及处 |
|---|---|---|
| **URP + RenderGraph** | 自定义渲染通道从 `Execute(ScriptableRenderContext, ref RenderingData)` 换为 `RecordRenderGraph(RenderGraph, ContextContainer)`;GPU Resident Drawer 新增 | 🔴 2 · 42 · 39 · 43 · 48(及 10 的直读通道呈现路径) |
| **场景管理 / 生命周期** | `Application.LoadLevel()` 已删;`SceneManager.LoadScene()` 为唯一入口;Addressables 6.2+ 加载失败**抛异常**(不再静默 null) | 🔴 6 · 7a · 54;波及全部经 Addressables 取 `*.cooked` 的 sim 装载路径 |
| **DOTS / Entities** | Entities 1.0+ API 全面重写 | ✅ **已由 ADR-017 结构性排除**(sim 侧永不;表现层留复评门,阈值 2026-09-20 裁定 = ≥100 实体 / ≥8 ms) |
| **Input System** | Legacy Input Manager 弃用,新 Input System 为默认 | ✅ **已由 ADR-011 承接**(action-based 资产 + 急救直读通道) |
| **UI Toolkit 运行时** | 运行时 UI 宣称 production-ready;焦点桥 / world-space / XR **仍须 spike** | 🔴 42(三项 spike 为前置;`ADR-013 §Engine Compatibility` 自陈「半可信」) |

### MEDIUM RISK 域

| 域 | 关键变化 | 触及处 |
|---|---|---|
| **Addressables** | 6.2+ 加载失败抛异常 ⇒ 启动期硬失败设计 | 7a · 6 · 54(烘焙产物装载),经 ADR-014 §五 |
| **音频 / AudioMixer** | `modules/audio.md:4` 自陈 6.3 mixer improvements 为知识缺口 | 44(`ADR-018 §Engine Compatibility` 唯一 post-cutoff 悬置) |
| **PhysX** | `defaultSolverIterations` 行为变化 | 1(`CharacterController` **不**参与 PhysX 求解 —— 风险已结构性降低)· 掉落表现态 |

### LOW RISK 域

| 域 | 说明 |
|---|---|
| **C# / BCL** | ADR-006 / 007 / 010 / 012 / 015 / 016 / 017 / 019 / 020 / 021 / 022 均自陈**不引入任何 post-cutoff API** |
| **UGUI** | deprecated-but-supported(ADR-013 只在 world-space / XR 补位使用) |
| **NavMesh** | 仅驱动表现态位移(ADR-016 §四),不进 sim |

> **本节的纪律**:凡本文件后续出现 🔴 标记处,均附 `docs/engine-reference/unity/` 的原文摘录;
> **未附摘录的 🔴 不得进入实现**。

---

## Technical Requirements Baseline

> 基线 = `docs/architecture/tr-registry.yaml`(**500 条稳定 TR ID**;2026-09-21 D-R3 批次前为 387,批次后 499,回写轮 +1 = 500)。
> **不新编一套编号** ——
> `docs/CLAUDE.md` 禁重编号,且 `/architecture-review` 是本注册表的唯一所有者。

```
500 条 TR  |  337 covered  |  75 partial  |  75 gap + 13 no-adr-by-design  (✅ 2026-09-23 ADR-009 TR 复核轮后 registry 实测;同日前序 ADR-028 小裁批后值 335/76/76/◆13;同日 QQ-08 清账批后值 334/76/77/◆13;同日 #4/#5 兑现轮后值 328/76/94/◆2;回写轮后 318/77/103/◆2;D-R3 批次后 314/79/104;批次前 387/245/51/89/◆2)
按运行期层(本蓝图 §System Layer Map 的 Axis B 归并)
```

> **⚠️ 本节的数于 2026-09-20 随 TD 条件 C4 变动**:8 条「借绿」由 `covered` 转 `partial`
> ⇒ **251 → 243 / 43 → 51**(gap 93 不变;ID 恒 387)。
> **同日回写轮再动**:`TR-randomevents-010` / `-031` 两条 gap → covered(挂 ADR-024[+025],
> 各带禁借绿注)⇒ **243 → 245 / 93 → 91**(partial 51 不变;ID 恒 387)。
> **2026-09-21 第四次动**(用户裁定 gate-check 门规格):`TR-concept-003` / `-004` 两条**范围声明**
> 由 `gap` 转新第四态 **`no-adr-by-design`** ⇒ **91 → 89**(covered / partial 均不变;ID 恒 387;
> 同批 `TR-itemdb-031` 只补 `adr:` 指针**不翻状态**。判据与涟漪见 `traceability-index.md` 变更历史 2026-09-21 行)。
> **2026-09-21 第五次动(D-R3 专门批次 · ID 追加 +112)**:12 项 P0 零 TR 系统批量回填
> ⇒ **387 → 499 / 245 → 314 / 51 → 79 / 89 → 104**(既有条目零翻转零改动,append-only;
> 本批 112 条的 `domain` 分布无 Foundation ⇒ **Foundation 门判据不受影响,仍残 2 条**)。
> **2026-09-21 第六次动(第二十六批 · D-R3 处置批 —— 用户四项裁定落地)**:A 组三处回写不一致全批
> (ADR-010 校验和字段位 / ADR-012 §五 补 7a 忘词令扫描义务 / 7b GDD ModalId 字面统一)
> + player_id 裁决落 ADR-006 Amendment B 注记 ⇒ **314 → 316 / 79 → 78 / 104 → 103**
> (`TR-persist-004/006` partial→covered · `TR-casebook-002` gap→partial;**ID 恒 499**;
> 8 条未来 ADR 候选 = 登记不立件裁定入注,零状态翻转;Foundation 仍 2 条)。
> **2026-09-23 第七次动(Required ADR #4/#5 兑现轮 —— 补登,此前漏入本表)**:adr-026 技能成长定点化
> + adr-027 病人 AI 写路径 ⇒ **计数翻转 10 条:318 → 328 / 94 → 94 基础上 gap 94**(`TR-skill-001…007`
> → covered(007 为 partial→covered)· `TR-skill-008` gap→covered · `TR-patient-021/022` gap→covered;
> **ID 恒 500**(回写轮已 +1 至 500);该轮后值 328/76/94/◆2。
> **2026-09-23 第八次动(QQ-08 + QQ-02 清账批 · 用户裁定路线 [A])**:21a 簇 **17 条翻转** ——
> A 簇 6 条 gap→covered(`TR-itemdb-014/018/019/020/021/028`,带 ADR 指针 + 禁借绿注)·
> B 簇 11 条 gap→◆ `no-adr-by-design`(逐簇裁定降级,**非** covered+备注 —— D-5 反模式规避)·
> C 簇 1 条 carve-out(`TR-itemdb-031` 不翻,承 2026-09-21 复核轮口径)· **QQ-02 同批结**
> (ADR-005 `IIdAuthority` 回填双方法,逐字承 ADR-010 §五)⇒ **328 → 334 / 94 → 77 / ◆2 → ◆13**
> (**ID 恒 500**;partial 76 不变;Foundation `domain` 口径 gap 残余恰 = `[TR-itemdb-031]`)。
> **2026-09-23 第九次动(ADR-028 小裁批 · 用户裁定「011 现裁,012 归 45 轮」)**:新立
> `adr-028-world-sound-source-ownership.md`(世界语境声源归属)⇒ **`TR-audio-011` 1 条翻转
> gap→covered**(挂 `adr: ADR-028`);**`TR-audio-012` 维持 gap 不翻**(归 45 的 GDD 轮,
> 与 `QQ-14`/`OQ-10-9` 同族,ADR-028 Ordering Note 明示排除)⇒ **334 → 335 / 77 → 76**
> (**ID 恒 500**;partial 76 不变;◆13 不变;011 `domain: Presentation` ⇒ Foundation `domain`
> 口径 gap 残余仍恰 = `[TR-itemdb-031]`)。**卫生同批**:头部 ADR 计数 `22 份`→`25 份`、
> 末号 `027(共 24)`→`028(共 25)`、Last Updated 刷日;`requirements-traceability.md`
> §Coverage Summary 三行漏刷订正(消除 334+76+94+2=506≠500 矛盾);本件 §5.4 标题 77→76。
> ⚠️ 下表分层数字**不随本批重算**(Axis B 归并口径的逐条重归属仍归 `/architecture-review`)。
> **2026-09-23 第十次动(ADR-009 TR 定向复核轮 · 用户裁定翻 031 + medcons-001,零新 ADR)**:
> 2 条 TR 翻转 —— `TR-itemdb-031` **gap→covered**(2026-09-21 QQ-08 轮 carve-out「随 ADR-009 TR 全量
> 复核轮重裁」的预挂口径**本轮兑现**:ADR-009 §五 + §GDD Requirements Addressed 表 `adr-009:876`
> 显式点名本条,ADR-015 §三 定位置侧 `spawn_anchor = WorldPos` 整数格)+ `TR-medcons-001`
> **partial→covered**(回写轮自陈「缺逐条复核执行体」,本复核执行后三源齐备 = ADR-005 主机唯一
> `Append` + ADR-009 世界流事件化边界 + `entities.yaml` `stream: world`/`author: 53` 唯一写者)
> ⇒ **335 → 337 / 76 → 75 / 76 → 75**(**ID 恒 500**;◆13 不变)。
> `TR-foraging-006` **维持 partial 不翻**(OQ-17-5 open ⇒ 禁借绿,用户裁定)。
> **Foundation `domain` 口径 gap 残余自此 = 0**(`TR-itemdb-031` 是 Foundation 域唯一 gap,翻 covered 后
> `domain: Foundation ∧ status: gap` 实测 = 0)⇒ gate-check 质量项「zero Foundation layer gaps」**门转绿**。
> **卫生同批**:头部 ADR 计数不动(仍 25 份,本批零新 ADR)、Last Updated 刷日、基线行刷 337/75/75、
> 本件 §5.4 标题 76→75 + 21a 簇行清零、§5.3 `itemdb` 行 gap 1→0 / cov 20→21;
> `requirements-traceability.md` / `traceability-index.md` 同批刷计数。
> **blocking 残余不变 = 2**(`TR-disease-022` / `TR-diag-013`,均 Core 域仍 gap)。**零新 ADR、零数值改动**。
> ⚠️ 下表分层数字**不随本批重算**(Axis B 归并口径的逐条重归属仍归 `/architecture-review`)。
> 下表的分层数字**尚未按 C4 重算** —— 8 条按注册表 `domain` 落 Core ×5 / Feature ×2 / Presentation ×1,
> 而本表用的是 **Axis B 归并口径**(≠ `domain` 字段),逐条重归属归 `/architecture-review`。
> 因此**下表的 covered/partial 两列每层各多算 0–2 条**,gap 列不受影响。

| 层 | 总数 | covered | partial | gap |
|---|---|---|---|---|
| Foundation | 61 | 27 | 5 | **29** |
| Core | 197 | 134 | 30 | **33** |
| Feature | 113 | 77 | 8 | 28 |
| Tooling | 8 | 8 | 0 | 0 |
| (concept 级,无系统归属) | 8 | 5 | 0 | 3 |

**⚠️ 门禁相关性**:Pre-Production 门的硬判据是「**Foundation 层零 gap**」。
当前 Foundation 有 **29 gap + 5 partial**,其中 **29 条 gap 里有 19 条 `adr: null`**(即无任何 ADR 触及):

| 缺口簇 | TR | 性质 |
|---|---|---|
| **21a itemdb schema / 边界**(原列 18 个 ID,行文误标「19 条」) | `TR-itemdb-007/008/009/011/012/014/015/016/017/018/019/020/021/024/028/029/030/031` | ⭑ **已裁(2026-09-23,QQ-08 结案,路线 [A])**:A 簇 6 条 gap→covered(014/018/019/020/021/028,带 ADR 指针)· B 簇 11 条 gap→◆ `no-adr-by-design`(007/008/009/011/012/015/016/017/024/029/030,逐簇裁定 + 归属件登记)· `031` 单条 carve-out 留 gap(承 2026-09-21「随 ADR-009 TR 复核轮重裁」未撤)。**本行自此不再是缺口簇** —— 残余仅 `031` |
| **30 技能成长**(原 7 条,现 0) | `TR-skill-001…007` | ⭑ **已结(2026-09-23,Required ADR #4 = ADR-026)**:`TR-skill-001…008` 八条全 covered(含原 B-6 定点纪律真缺口),本行**清零** |
| **concept 层**(3 条) | `TR-concept-003/004`(MVP 8 条 / P0 排除项)+ `TR-concept-006`(帧预算) | ⭑ **2026-09-21 已裁(QQ-07 结案)**:`003/004` → ◆ 范围声明摘除;`006` 不降级,仍 ❌ 待硬件定稿 |
| **3 输入 residual**(4 gap + 3 partial) | `TR-input-013/015/016/017/019/020/021` | `016` / `017` / `019` 已明标「**非 ADR 缺口,是实测前置**」(绑 `/test-setup`) |

> **本基线的一处结构性发现**:`traceability-index.md` 覆盖 **21 个 TR 组**,
> 而 P0 有 **31 项系统** ⇒ **12 项 P0 系统没有任何 TR 组**:
> **7a · 7b · 17 · 23 · 24 · 29 · 39 · 42 · 44 · 48 · 51 · 53**。
> **这 12 项全部已有 GDD**(2026-09-18/19/20 落盘),但**全部未回溯挂载 TR** ——
> 这是「**GDD 落盘 ≠ 需求已登记**」的**整批**欠账,归 `/architecture-review` Phase 8。
> 最典型的一例 = **17 采集**:`foraging.md` 已含 §Formulas 与规则表,却**零 TR 行**
> (它的 TR 只以 `TR-input-*` 一类旁证出现在别组)。

---

## System Layer Map

### 为什么是**两轴**,不是一张表

`design/gdd/systems-index.md` §3 的六类别(Foundation / Core / Feature / Presentation /
Polish / Tooling)是 **authoritative** —— 但它是**依赖档**(「谁依赖谁」),**不是运行期层**
(「谁住哪个程序集、能不能碰引擎」)。本项目的 `门 A`(`"noEngineReferences": true`,
引用集恰 = BCL)把「依赖档」与「运行期层」切出了**真实的分叉**:

- **42 拟物 UI / 44 音频 / 1 玩家 / 2 摄像机** 在 §3 里分别是 Foundation / Foundation / Core / Core,
  但四者**全部住呈现侧**(引 `UnityEngine`);
- **4 交互 / 8 诊断 / 13 病人 AI** 在 §3 里是 Core / Core / Feature,
  但三者**全部明写住「边界层(呈现侧)」**(`interaction-system.md:70` · `patient-ai.md:20/134` · `diagnosis-system.md` AC-8-2);
- **6 世界 / 7a 持久化 / 54 关卡工具** 跨越两个层(6 有 sim 部分 + 边界部分;7a 有边界程序集 codec + 表现层 I/O;54 完全在编辑期)。

∴ **本蓝图给出两轴**:Axis A(依赖档,承 §3 原文,不改) × Axis B(运行期层,本文件新立)。

### Axis B — 运行期层(L0…L6)

```
┌──────────────────────────────────────────────────────────────────────────┐
│  L6  TOOLING        54 关卡工具                                          │
│      编辑期 / 构建期 · 可引 UnityEditor · **不进构建** · 门 A 不约束       │
├──────────────────────────────────────────────────────────────────────────┤
│  L5  PRESENTATION   42 44 39 43 7b 48 47 38 · 2 摄像机 · 1 玩家控制器      │
│      引 UnityEngine / URP · **只渲染、只触发,永不持有游戏状态**(ADR-013/018/020) │
├──────────────────────────────────────────────────────────────────────────┤
│  L4  边界层(呈现侧)  4 交互 · 8 诊断 · 13 病人 AI · 51 遥测 · 3 输入        │
│      可消费 float DTO(`VitalsDto`)· **不写三流** · 住门 A 外侧             │
├──────────────────────────────────────────────────────────────────────────┤
│  L3  BOUNDARY(边界程序集)  `WorldPos` + 六个 P0 抽象点 + `SimEvent`         │
│      + `VitalsDto` + `Fix→float` 门面 + 7a 的 codec                        │
│      **仅 BCL · 零 `UnityEngine`** · sim 与门两侧**共同引用**(白名单,非通道) │
├──────────────────────────────────────────────────────────────────────────┤
│  L2  SIM(门 A)  9 · 5 · 10 的 `Judge` · 17 · 18 · 20 · 25 · 30 · 37 · 11    │
│      · 23 · 24 · 29 结算 · 52 · 53 · 27 决策 · 6 的状态机 · 21a schema       │
│      `noEngineReferences: true` · **引用集恰 = BCL** · 门 B(IL 扫描,拒 float) │
├──────────────────────────────────────────────────────────────────────────┤
│  L1  FOUNDATION-CONTRACTS  21a 契约 · 45 `IReplayPipe` 抽象 · 40/41 内容层   │
│      纯数据 / 接口 / 政策 · 零运行期行为                                    │
├──────────────────────────────────────────────────────────────────────────┤
│  L0  PLATFORM       Unity 6.3 LTS(URP)· IL2CPP · OS / 硬件 · OpenXR        │
└──────────────────────────────────────────────────────────────────────────┘
```

> **⚠️ 层图的三条纪律**(均由 ADR 明文,不是本文件的发明):
> 1. **门的单向性** —— 门 A 只约束 L2。L3 被 L2 与 L4/L5 **共同引用**是**允许且必需**的
>    (`adr-005` Amendment F:「边界程序集是**白名单**,不是通道…新增一个跨门类型须**追加进本法**」)。
> 2. **L4 不是 L2 的子层也不是 L5 的子层** —— 它是「**决策住呈现侧**」这一裁决的产物
>    (2026-09-15 用户裁定:13 的决策住边界层;**4 / 8 同款豁免**)。
> 3. **L5 三件套同构** —— 42 / 44 / 2 相机**同一职责形状**:只渲染 / 只触发,永不持有游戏状态
>    (`ADR-013 §9 C3` → `ADR-018 §一` → `ADR-020 §五`)。

### 🔴 HIGH RISK 标注 —— 层图上的 URP/RenderGraph 与场景加载

> **✅ 2026-09-23 承接注**:①(RenderGraph 面)= ADR-023 ⑧(P0 默认零 custom Renderer Feature +
> 触发条款,`RecordRenderGraph` 新签名);②(场景加载面)= ADR-023 ①⑤⑥(三场景制 / 拆序六步 /
> chunk 激活权 = 系统 6,S1/S3/S4 spike 已实测通过)。本小节与 §3.4 [0]/[7] 均不再挂
> §Required New ADRs #1。

**① L5 的 URP Renderer Feature 面(2 / 42 / 39 / 43 / 48)**

```
docs/engine-reference/unity/modules/rendering.md
### RenderGraph API (Unity 6+)
自定义渲染通道改用 RenderGraph,不再用 CommandBuffer:

  // ✅ Unity 6+ (RenderGraph)
  public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
      using var builder = renderGraph.AddRasterRenderPass<PassData>("MyPass", out var passData);
      builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => { /* ... */ });
  }
  // ❌ Old (CommandBuffer - still works but deprecated)
  public override void Execute(ScriptableRenderContext context, ref RenderingData data) { }

`breaking-changes.md:54` 「URP/HDRP Renderer Feature API Changes … Migration:
Update custom render passes to RenderGraph API.」
`current-best-practices.md:178` 「Use RenderGraph API for Custom Passes (URP/HDRP)」
```

**含义**:本项目**当前没有任何 ADR 裁决「是否需要自定义渲染通道」**。若 39 脉案 / 43 纸质地图 /
42 拟物 UI 需要任何屏后处理(纸纹、墨迹描边、旧纸色调),其实现落点**只能是 RenderGraph**;
而 2022 时代的知识(用 `Execute(ScriptableRenderContext, ref RenderingData)`)在 6.3 上**已弃用**。
⇒ 本项登记为 **Required New ADR #1**(见 §Required ADRs)。
> **✅ 2026-09-23 已被 ADR-023 ⑧ 承接**(P0 默认零 custom Renderer Feature;仅当 ADR-013
> 假设 6 spike 失败时按触发条款走「RenderGraph pass 形状」新 ADR,用 `RecordRenderGraph` 新签名)。

**② L0/L1 的场景加载与生命周期(6 / 7a / 54)**

```
docs/engine-reference/unity/deprecated-apis.md:100
| `Application.LoadLevel()` | `SceneManager.LoadScene()` | Scene management |

docs/engine-reference/unity/plugins/addressables.md:263-276
### Load Addressable Scene
  AsyncOperationHandle<SceneInstance> handle =
      Addressables.LoadSceneAsync("MainMenu", LoadSceneMode.Additive);
  ... await Addressables.UnloadSceneAsync(handle).Task;   // 卸载

docs/engine-reference/unity/plugins/addressables.md:301  ### Cleanup on Scene Unload
```

**含义**:全案 `docs/architecture/` 目录内 **`SceneManager` / `LoadScene` / 「场景管理」零命中**
—— 即 **没有任何 ADR 裁决场景/世界的加载、卸载、常驻与切换策略**。
而本项目的世界是**手工烘焙的单一固定世界**(ADR-015),chunk 只作流式粒度
(「**chunk = 流式 / 脏块优化,不是坐标原点**」,ADR-015 §四);
`ADR-014 §五` 说 `data-core`「首次 `Step` 前常驻」,而 `ADR-022` 又补注「**该常驻只约束小体量几何数据,
不约束导航格**」(导航格按 chunk 切片 `world_nav_{chunk}.json`)。
⇒ **「玩家走出已装载 chunk 时发生什么」是一个悬空的架构问题** —— 它同时决定:
`ITickProvider` 与加载的时序关系、`CatchUp` 的触发面、以及 Addressables 的卸载点。
登记为 **Required New ADR #1 的另一半**(与 ① 合并为同一份 ADR)。
> **✅ 2026-09-23 该缺口已由 ADR-023 承接(§⑤ 拆序六步 + §⑥ chunk 激活权 = 系统 6 + §① 三场景制)**:
> · 「已装载 chunk 的流出」 = ⑤ 拆序第 5 步(`UnloadSceneAsync`,实现期归 `ISceneRouter`);
> · 「玩家走出已装载 chunk」的进/出权 = ⑥(**系统 6** 只读 `ActorCellEntered` 的格,激活为派生态不进流);
> · 惰性载入的粒度 = ADR-023 §⑥ 实测结论(S4:**单场景多根 `SetActive`**,场景 load/unload 仅世界整体换入换出)。
> 本小节保留作 Problem 出处。

### Axis A × Axis B 全表(54 行)

> **图例**:门 A 侧 —— `SIM` = L2(门 A 内)· `BDY` = L3 边界程序集 · `EDGE` = L4 边界层(呈现侧)
> · `PRES` = L5 · `TOOL` = L6 · `DATA` = L1
> **证据** —— ✅ 明文(有 ADR / GDD 原句)· ⚠️ 推断(无文可证;逐条见 §Module Ownership §2.6 读注)

| # | 系统 | Axis A(§3 依赖档) | Axis B(运行期层) | 门 A 侧 | 引擎风险 | 证据 |
|---|------|------|---|---|---|---|
| **21** | 物品与配方数据库 | Foundation | L1 契约 + **L2 schema** | DATA / SIM | — | ✅ 21a 契约落 ADR-006 |
| **3** | 输入与设备 | Foundation | **L4 边界层** | EDGE | MEDIUM(Input System) | ✅ `emergency-procedures.md:155` 表格「3 输入与设备 \| 边界层」 |
| **30** | 技能与熟练度 | Foundation | **L2 Sim** | SIM | — | ✅ `skill-system.md`(XP 公式定点域)· ✅ **ADR-026**(成长定点化与持久化 · 兑现 Required #4)|
| **42** | 拟物 UI 框架 | Foundation | **L5** | PRES | 🔴 HIGH(UI Toolkit 运行时 / 焦点桥) | ✅ ADR-013(三项 spike 为前置) |
| **44** | 音频系统 | Foundation | **L5** + L3 契约程序集 | PRES / BDY | MEDIUM(AudioMixer 6.3) | ✅ `audio-system.md:242` 「DTO 与 `IAudioCueSink` 住独立契约程序集(仅 BCL)」 |
| **40** | 史实资料库 | Foundation | L1 内容层 | DATA | — | ✅ P1a |
| **41** | 内容边界政策 | Foundation | L1 内容层 | DATA | — | ✅ P1a |
| **51** | 遥测与分析 | Foundation | **L4 边界层** | EDGE | — | ✅ ADR-019 §三「51 住边界层 —— 不进 sim 程序集、不写三流」 |
| **45** | 网络层与同步 | Foundation | L1 抽象 + L4 实现 | BDY / EDGE | — | ✅ ADR-001(P0 预埋 · P1b 实现) |
| **1** | 玩家控制器与移动 | Core | **L5** | PRES | LOW | ✅ `player-controller-and-movement.md:124` 「不引用 sim 实现程序集…只引用边界程序集」· ADR-020 |
| **2** | 摄像机与视角 | Core | **L5** | PRES | 🔴 HIGH(URP 相机栈) | ✅ ADR-020 §五「相机只读不持状态」 |
| **4** | 交互系统 | Core | **L4 边界层** | EDGE | — | ✅ `interaction-system.md:70` 「4 住在边界层(呈现侧),不是 Core / 门 A 程序集」 |
| **5** | 时间与天气 | Core | **L2 Sim** | SIM | — | ✅ `time-and-weather.md` AC-5-01(BLOCKING,门 A 白名单断言) |
| **6** | 世界与生态区 | Core | **L2 状态机 + L3 量化** | SIM / BDY | 🔴 HIGH(场景 / chunk 装载) | ✅ `world-and-ecozones.md:180`(`WorldPos` 住边界程序集)· ✅ ADR-023 ⑥(chunk 激活权 = 6,只读 `ActorCellEntered` 的格,激活为派生态不进流)|
| **7a** | 持久化服务 | Core | **L3 codec + L5 I/O** | BDY / PRES | 🔴 HIGH(文件 I/O / 线程 / 迁移) | ✅ `persistence-service.md:577` 「扫描集 = 门 A sim 程序集 + **边界层承载 codec 的程序集**」 |
| **8** | 诊断与体征揭示 | Core | **L4 边界层** | EDGE | — | ✅ AC-8-2(消费 `VitalsDto`)· `patient-ai.md:186` 「8 / 10 与 13 **同住边界层**」 |
| **9** | 疾病与伤情模拟 | Core | **L2 Sim** | SIM | — | ✅ `disease-simulation.md:1340`(独立 asmdef + `noEngineReferences: true`) |
| **10** | 急救动作模块 | Core | **L2(`Judge`)+ L5(手感)** | SIM / PRES | 🔴 HIGH(直读通道 → 渲染延迟) | ✅ `emergency-procedures.md:397` 「`Judge ∈ sim 程序集`(规则三)」 |
| **11** | 处方用药 | Core | **L2 Sim** | SIM | — | ✅ 写 `DrugTreatmentApplied`(病史流第 28 支) |
| **17** | 采集 | Core | **L2 Sim** | SIM | — | ✅ AC-17-05(反射扫描 sim 程序集的 `ItemInstanceId` 构造点) |
| **18** | 炮制 | Core | **L2 Sim** | SIM | — | ✅ 调 21a 烘焙管线 · ⚠️ 多条 TR gap |
| **20** | 库存与物品 | Core | **L2 Sim** | SIM | — | ✅ ⚠️ 消耗 / 转移 `Kind` 归属见 `OQ-20-1` |
| **25** | 格斗与武器线 | Core | **L2 Sim** | SIM | — | ⚠️ `combat-and-weapon-lines.md:399` R19b:「**先澄清 25 是否独立 asmdef,若与 9 同程序集则一行回指 AC-5 即可**」 |
| **12** | 药物槽 | Feature | **L2 Sim** | SIM | — | ⚠️ P1a |
| **13** | 病人 AI 与行为 | Feature | **L4 边界层** | EDGE | — | ✅ `patient-ai.md:20/134`(2026-09-15 用户裁定「13 不进 sim 程序集」) |
| **14** | 辨证 | Feature | **L2 Sim** | SIM | — | ⚠️ P1a |
| **15** | 针灸 | Feature | **L2 Sim** | SIM | — | ⚠️ P1a |
| **16** | 中药 | Feature | **L2 Sim** | SIM | — | ⚠️ P1a |
| **19** | 制作 | Feature | **L2 Sim** | SIM | — | ⚠️ P1a |
| **23** | 模块化建造 | Feature | **L2 Sim** | SIM | — | ✅ AC-23-12(BLOCKING:「无任何呈现 API 引用(无 Mesh / Transform / GameObject)」) |
| **24** | 医馆机器 | Feature | **L2 Sim** | SIM | — | ✅ 输出未钳制 `env_score`,钳制点在 21a F1 |
| **27** | 敌人 AI | Feature | **L2 决策 + L5 运动** | SIM / PRES | — | ✅ ADR-016 §二/§四「决策进 sim,运动表现态」 |
| **28** | 捕获与驯化 | Feature | — | — | — | ⚠️ P1a |
| **29** | 死亡与复活 | Feature | **L2 结算 + L5 呈现** | SIM / PRES | — | ✅ `death-and-respawn.md:401` 「结算逻辑…**sim 实现程序集(门 A 内)**」 |
| **34** | 公共卫生 | Feature | — | — | — | ⚠️ P1a |
| **35** | 时代事件 | Feature | — | — | — | ⚠️ P1a |
| **36** | NPC | Feature | — | — | — | ⚠️ P1a |
| **37** | 病例 | Feature | **L2 Sim** | SIM | — | ✅ 病例流写者 · ⚠️ 6 条 TR gap |
| **46** | 同步降级 | Feature | — | — | — | ⚠️ P1b(Networking) |
| **52** | 随机事件导演 | Feature | **L2 Sim** | SIM | — | ✅ 掷骰入口恒为 `IEventAuthority.Roll`(ADR-007);⚠️ 17 条 TR gap |
| **53** | 医疗后果与责任 | Feature | **L2 Sim** | SIM | — | ✅ AC-53-03(BLOCKING,门 A / ADR-006 零浮点) |
| **7b** | 存档 UI | Presentation | **L5** | PRES | — | ✅ 承 ADR-013 双栈 |
| **38** | 对话 | Presentation | — | — | — | ⚠️ P1a |
| **39** | 脉案 | Presentation | **L5** | PRES | 🔴 HIGH(URP + UI Toolkit 双层) | ✅ ADR-013 §一 |
| **43** | 地图与出诊箱 | Presentation | **L5** | PRES | 🔴 HIGH(URP) | ✅ ADR-013 §一 |
| **47** | 精神压力 | Presentation | — | — | — | ⚠️ P1a |
| **48** | 教学 | Presentation | **L5** | PRES | 🔴 HIGH(URP)—— 且 48 有**三 BLOCKING**同根因「世界内的纸」P0 无渲染形态 | ✅ `OQ-48-4/5` 须与 `OQ-42-5` 并批 |
| **22** | 商业 | Polish | — | — | — | ⚠️ P1a |
| **31** | 声誉 | Polish | — | — | — | ⚠️ P1a |
| **32** | 门槛 | Polish | — | — | — | ⚠️ P1a |
| **33** | 权限 | Polish | — | — | — | ⚠️ P1a |
| **49** | 无障碍 | Polish | — | — | — | ⚠️ P2 |
| **50** | 本地化 | Polish | — | — | — | ⚠️ P2 |
| **54** | 关卡工具 | **Tooling** | **L6** | TOOL | 🔴 HIGH(Unity Terrain / `com.unity.ai.navigation` 6.3 API) | ✅ ADR-022(编辑期 / 构建期,**运行期零存在**) |

> **表格的两处诚实标注**:
> 1. **P1a/P1b/P2 行的 Axis B 留空** —— 它们不在本蓝图承诺面内,填空即等于**发明未裁决的架构**。
>    待各自立项时补。
> 2. **⚠️ 推断行共 11 处**(12/14/15/16/19/28/34/35/36/46 + 25 的 asmdef 归属)——
>    Phase 2 逐系统落「Owns / Exposes / Consumes / Engine APIs」时会二次裁定,
>    与本节不符者以 Phase 2 为准。

### 本节引出的三条待裁问题(**不改 ADR,只登记**)

| 编号 | 问题 | 归属 |
|---|---|---|
| **A-1** | **程序集名未定** —— 全仓仅 `Sim.asmdef` 有一个具名(ADR-017:251)。`边界程序集` / `呈现侧` / `边界层` / `工具` **四个名字存在但从未被注册成一个 asmdef**;ADR-005 Amendment F 要求「新增跨门类型须追加进本法」,因而**边界程序集的成员清单是受控的**,却无名可引 | **✅ 2026-09-20 结案** —— Phase 4 + Required New ADR #2 已兑现为 **ADR-025 ① 具名清单(Accepted,不改名)**,见 §2.0 |
| **A-2** | **`门面程序集` 这个概念在 ADR-005:228 被命名,却从未定义** —— 原文 `public float ToFloat(); // 仅门面程序集可调用`,而「门面程序集」与「边界程序集」是否同一物无文可证(9 的 GDD 说「门面与 `VitalsDto` 住【边界程序集】」,倾向同一)<br>**✅ 2026-09-20 结案(ADR-025)**:三名收口 —— 「门面程序集」「独立契约程序集」**称谓作废**,内容并入 `Sim.Contracts`;「仅门面可调用 `ToFloat()`」改由 **② 甲案白名单断言**执法(用户裁定 = 甲,`internal`+IVT 案否决留档) | **已结 · ADR-025** |
| **A-3** | **`边界程序集`(仅 BCL 契约)与 `边界层(呈现侧)`(可引 UnityEngine 的决策层)是两个不同东西,却共用一个「边界」词根** —— 4 / 8 / 13 / 51 住后者,`WorldPos` / `VitalsDto` 住前者 | 本文件命名收窄:全文一律写 **`L3 BOUNDARY`** 与 **`L4 边界层(呈现侧)`** |

---

## Module Ownership

> **数据来源**:`docs/registry/architecture.yaml`(§`state_ownership` 8 条 · §`interfaces` 15 条
> 契约)· 各 ADR 的 `## Key Interfaces` · 各 GDD 的 §Dependencies / §Interactions 表。
> **本节的「Owns」列不是新发明** —— 它逐条回指上述登记件;凡无登记件背书的行标 ⚠️ 并注明待裁定处。

### 2.0 程序集清单(**本节最重要的产出**)

> ✅ **A-1 / A-2 终裁(2026-09-20 逐份裁定轮 #3):ADR-025 转 Accepted,清单与命名全照准、不改名。**
> 下表由「待填骨架」升为**已生效命名**;骨架原状(全案唯一具名 = `Sim`)保留在其上作为出处记录。

| 程序集(裁定名) | 层 | 引用集 | 状态与依据 |
|---|---|---|---|
| `Sim` | L2 门 A | **恰 = {BCL, `Sim.Contracts`}**(白名单升格,仍零引擎程序集) | ✅ ADR-017:251 + **ADR-025 ①**(⚠️ `references: []` 歧义订正 = ADR-025 V-6,**✅ 2026-09-23 回写轮已挂 ADR-017 §二** —— 该空集为示例简写非断言文本) |
| `Sim.Contracts` | L3 BOUNDARY | **恰 = BCL** | ✅ ADR-025 ① —— 收 `WorldPos` + 六抽象点 + `SimEvent` 族 + `Fix`/`FixParse` + `VitalsDto` + `AudioCueDto`/`IAudioCueSink` + **`ITeleportCommandSink`(QQ-01 ①′ 整数半)** |
| ~~「门面程序集」~~ | (L3) | — | **称谓作废**(ADR-025 ①)—— ADR-005:228 的「仅门面可调用 `ToFloat()`」改由 **② 甲案白名单断言**执法;原文回写加注归 V-5 |
| ~~「独立契约程序集」~~ | (L3) | — | **称谓作废** —— 内容并入 `Sim.Contracts`(同上,V-5 加注)|
| `Sim.Codec` | L3 | BCL only | ✅ ADR-025 ① —— 收 7a 三流 codec + 存档头 + `Fix` 自定义编码器(`persistence-service.md:577` 的「边界层承载 codec 的程序集」现名)|
| `Gameplay.Presentation` / `Gameplay.UI` | L4/L5 | UnityEngine · URP · Input System / + UI Toolkit | ✅ ADR-025 ① —— `IPlayerMotor` 的 `Vector3` 半 + `ICameraRig` 住前者(①′ 浮点半);分装配 = 焦点单栈门的编译期表达 |
| `Editor.Tools` 族 | L6 | UnityEditor 自由 | ✅ ADR-025 ① —— `tools/level/`(ADR-022)+ `tools/kindgen/`(ADR-024);`includePlatforms: ["Editor"]`,不进构建 |

> **⇒ 「全案唯一有名字的程序集是 `Sim`」这一状态自 2026-09-20 起终结。** 门 A 外侧的划分
> 由散文升为具名清单 + 封闭性断言(ADR-025 ④:未登记 asmdef = 构建失败)。

### 2.1 L2 SIM(门 A 内)—— 所有权表

> **共性**:本层**零引擎 API** —— 这不是「我们没用」,是**定义性结果**(`noEngineReferences: true`
> 使任何 `UnityEngine.*` 引用成为**构建失败**)。∴ 本表**不设「Engine APIs used」列**。

| 模块 | Owns(独占状态) | Exposes | Consumes | ADR |
|---|---|---|---|---|
| **9 疾病与伤情** | 病史事件流(唯一真源)· `PatientId` 发放 · `patient_seed` · `Step`/`CatchUp` 执行权 | `IVitalsQuery.GetVitals` · `Step(tick)` · `CatchUp(from,to)` · `Down(ActorId)` · `IsCombatant(entity)` · `QueryHurtLevel` | `ITickProvider` · `IEventSink` · `IIdAuthority` · `IEventAuthority` | ADR-005 · 009 |
| **5 时间与天气** | `TICKS_PER_DAY` 单一定义点 · `season_index` 产出 · 昼夜相位 | `season_index`(只交下标,**不做乘法**)| `ITickProvider`(**唯一时间输入**)| ADR-005(AC-5-01 BLOCKING)|
| **10 急救动作 · `Judge`** | 判定(`JudgeResult` 枚举)· `EmergencyAttempt` 的 `Judge` 执行 | `Judge(attempt)` | `EmergencyReading`(3 侧产,**全整数**)| ADR-011 Amendment B · ADR-009 Amendment I |
| **17 采集** | 采集事实事件 `ResourceHarvested` 的**写者** · `node_id` 稳定标识 | 世界流事件 | `IDataProvider`(烘焙产物)· `IIdAuthority` | ADR-009 §七 |
| **18 炮制** | 调 21a F1 一次拿三出参 | — | 21a `EFF`/`QualityMod` | ADR-014 |
| **20 库存与物品** | 库存槽形状 · 载重(`CanCarry`) | — | 21a Schema E | ⚠️ `OQ-20-1`(消耗/转移 `Kind` 归属)未裁 |
| **25 格斗与武器线** | 战斗效能**消费** · `IsSuppressed` | ✅ **已裁(2026-09-20):与 9 同程序集** —— 25 无独立呈现层;其公式与 9 的 F4 互为输入/输出 | 30 的输出档(定义权归 30) | ✅ 本次裁定;`combat-and-weapon-lines.md:399` R19b 的待澄清项就此关闭 |
| **30 技能与熟练度** | `QueryLevel` / `EmitGrowth` · `SKILL_CAP` · `DIAG_TIERS` | 两接口(全案唯一) | — | ✅ **ADR-026**(2026-09-23 Required #4 —— `TR-skill-001…008` 全 covered;原「无 ADR / 全 gap」注过期)|
| **11 处方用药** | `DrugTreatmentApplied` 写者 | 病史流第 28 支 | 9 的病种注册表(唯一真源) | `OQ-11-1` ✅ 已裁 |
| **23 模块化建造** | `Structure*` 三事件 | — | `world_buildslots.cooked` | ADR-015 §五 · AC-23-12(BLOCKING) |
| **24 医馆机器** | `env_score`(**未钳制**) | `env_score` | POI 邻接 | ✅ 钳制点归 21a F1 |
| **29 死亡与复活** | 结算逻辑 | — | `DeathCandidate` | ✅ `death-and-respawn.md:401`;⚠️ `Append` **调用点数 = 0**(写者 = 9) |
| **37 病例系统** | 病例事件流(第二条逻辑流) | `CaseOpened` / `CaseClosed` / `PatternRecognized` / `Judgment*` | 8 / 10 的动作信号 | ADR-008 |
| **52 随机事件导演** | 事件池 · tier 表 · `EncounterStarted` 写者 | — | `IEventAuthority.Roll`(**零其他随机源**)| ADR-007 · AC-52-06 |
| **53 医疗后果与责任** | — | — | `CaseClosed` / `PatternRecognized` | ADR-008/009 |
| **27 敌人 AI(决策)** | 行为程序求值 · `LogiPose` · 整数导航格路径 | `EncounterEnded` 写者 | 三源不变量 | ADR-016 §一 |
| **6 世界(状态机)** | `PoiStateChanged` 写者 · POI 状态 | 世界流 | `world_*.cooked` | ADR-021 |
| **21a(契约/schema)** | Schema A–E · `FixParse` 唯一入口 · `EFF`/`QualityMod` | 全程只读 | — | ADR-006 · 014 |

### 2.2 L3 BOUNDARY(边界程序集)—— 成员清单(**受控**)

> **⚠️ 本清单是白名单,不是目录** —— ADR-005 Amendment F:「新增一个跨门类型须**追加进本法**」。

| 成员 | 形状 | 消费者 | 依据 |
|---|---|---|---|
| `WorldPos` | `(i32 x, i32 y, i32 z)` 单一整数格 | 1 · 6 · 13 · 27 · 44 | ADR-015 §三 · Amendment F |
| `ITickProvider` | `long CurrentTick { get; }` | L2 全体 | ADR-005 §Key Interfaces |
| `IEventSink` | `void Append(in SimEvent)` | 主机侧全部写者 | 同上 |
| `IIdAuthority` | `PatientId Next()` + `ItemInstanceId Next()`(**双方法**;✅ QQ-02 已结 2026-09-23 —— ADR-005 已回填,逐字承 ADR-010 §五;原「缺 `ItemInstanceId.Next()`」注过期)| 9 · 17 · 20 · 21a | ADR-005 · ADR-010 §五 |
| `IVitalsQuery` | `VitalsDto GetVitals(PatientId)` | **8**(唯一) | 同上 · 9 的 §UI Requirements |
| `IEventAuthority` | `IsAuthority` · `Roll(RollRequest)` | 5 · 52 | ADR-007 §一 |
| `SimEvent` + `PatientId`/`StreamId`/`EventKind` | 值 struct + 整数枚举 | 三流全体 | ADR-006 Amendment A · ADR-009 Amendment E |
| `VitalsDto` | **唯一浮点出口** | 8 · 13 · 42 | 9 的三轮裁定(门面住边界程序集)|
| `Fix.ToFloat()` 门面 | 显式转换,**无 implicit operator** | 同上 | ADR-005:228 · ADR-006 §五 |
| `IDataProvider` | 读 `*.cooked` | 17 · 6 · 9 | ADR-014 §五(`foraging.md:450` **EXTERNAL**)|
| `AudioCueDto` + `IAudioCueSink` | 整数语义,**无 `disease_id`** | 44 | ⚠️ 44 称之为「独立契约程序集」—— 见 §2.0 |
| 三流 codec | 存档序列化 | 7a | `persistence-service.md:577` |
| `IPositionalChannel` | `WorldPosLatest`(第二 QoS)| 44 · 动画 · VFX | ADR-001 §一之二 |
| `IPlayerMotor` / `ICameraRig` | 表现态位移 / 只读机位 | 1 · 2 · 13 · 42 | ADR-020(⚠️ **含 `Vector3`** —— 见下注)|

> ⚠️ **`IPlayerMotor` / `ICameraRig` 是边界程序集的张力点**:二者签名含 `Vector3`(连续位置),
> 而 L3 的纪律是「仅 BCL」。二者**实际住 L5**(表现层),此处列出是因为 `architecture.yaml` 的
> `player_presentation_motion` / `camera_readonly_view` 两条所有权条目把它们登记为跨系统契约。
> **⇒ 这是 §2.0 那份 ADR 必须一并裁的三选一**:① 二者拆为 `(Cell 整数版)` 进 L3 + `(Vector3 版)` 留 L5;
> ② 承认 L3 允许 `UnityEngine.Vector3`(破「仅 BCL」);③ 自建 `Float3` 值 struct。
> **本文件不预先裁定**,登记为 §Open Questions **QQ-01**。

### 2.3 L4 边界层(呈现侧)—— 所有权表

| 模块 | Owns | Exposes | Consumes | Engine APIs | ADR |
|---|---|---|---|---|---|
| **4 交互系统** | 拾取判定(量化格 + 宽容半径)· 焦点导航的**意图**侧 | — | 13 的 `IPresentPatients` · 10 的 `Armed` 通道态 | ⚠️ **待定**(物理查询**禁** —— `modular-building.md:122`)| ADR-009 §五 |
| **8 诊断与体征揭示** | 「玩家能不能读出来」的**门槛形状**(`READ_FLOOR` / `BASE_READ` / 技能→精度曲线)| — | `VitalsDto`(9)| ⚠️ 待定(听诊层 → 44)| ADR-005 边界(8↔9)|
| **13 病人 AI 与行为** | 行为决策(**派生态**)| `IPresentPatients`(**只读在场视图**)| `VitalsDto` | NavMesh(仅驱动**表现态**位移)| ADR-016 §六 |
| **51 遥测与分析** | `JudgmentMetrics`(本地只读重算)| — | 三流(只读订阅)| 文件 I/O(本地导出)| ADR-019 |
| **3 输入与设备** | action 资产 · 绑重 · 结构哈希 | `EmergencyReading`(**全整数**,不含判定)· `iconKey` | Input System | `InputSystem` 包(6.3)| ADR-011 |

> **L4 的「Engine APIs」列是本节最弱的一列** —— 全案**没有一份 ADR 裁决过这几个「呈现侧决策层」
> 各自可以调什么引擎 API**,只裁决了它们**不可以**调什么(不写三流、不进 sim 程序集)。
> 这是**门 A 的单向性**的必然结果(门 A 只管内侧);它**不是缺陷**,但意味着
> §Required New ADRs #1(渲染与场景加载)必须把这一列**补齐**。
> **✅ 2026-09-23 已由 ADR-023 ⑧ 执行**(P0 零 custom Renderer Feature;是否引入走触发条款,
> 不再把整列留给「待补」)。

### 2.4 L5 PRESENTATION —— 所有权表

| 模块 | Owns | Exposes | Consumes | Engine APIs(🔴 见 §Engine Knowledge Gap)|
|---|---|---|---|---|
| **42 拟物 UI 框架** | 两栈分工 · 元件库契约 · **拒绝权** | `FOV_v`(**只读**,归 2)· `iconKey` 映射 | `VitalsDto`(只读)| UI Toolkit(UXML/USS)· UGUI world canvas · `NavigationMoveEvent` + `FocusController` |
| **44 音频系统** | 混音拓扑(7 总线)· 事件表 · 无提示音白名单 | `AudioCueDto` 消费 | `PositionalChannel`(空间化声源)| `AudioMixer` · `AudioListener`(单挂点 = 主相机 / VR 头显)|
| **39 脉案 / 43 地图出箱 / 7b 存档 UI / 48 教学** | 各自屏幕 | — | 42 的元件库 · 只读 DTO | UI Toolkit + UGUI 补位 |
| **2 摄像机** | 机位(`ICameraRig`)| **`YawBasis`**(只读,1 的唯一消费方)| 1 的 `Position` | URP 相机栈 · Volume(后处理)|
| **1 玩家控制器** | 连续位移 / 速度 / 朝向(**表现态**)| `WorldPos Cell` · `Position` · `ConsumeCellChanged()` | `MoveInput`(3)· `YawBasis`(2)| `CharacterController`(**不参与 PhysX 求解**)|

### 2.5 L6 TOOLING(54 关卡工具)

| Owner | Owns | Produces | Consumes | Engine APIs |
|---|---|---|---|---|
| **54 关卡工具** | **逻辑层整数的唯一作者权** · 六项一致性检查 C1–C6 | `world_{geometry,ecozones,poi,resources,buildslots,nav_{chunk},terrain}.json` | 第三方地形工具(**仅视觉层**)| 🔴 Unity Terrain · `com.unity.ai.navigation` 6.3 |

> **运行期零存在** —— 它**不进构建**,故门 A 不约束它,其引擎知识风险**不污染出货**(ADR-022 §Engine Compat)。

### 2.6 依赖图(ASCII)

```
                        ┌───────────────────────────────┐
  L6  TOOLING           │ 54 关卡工具(编辑期 · 不进构建) │
                        └───────────────┬───────────────┘
                                        │ world_*.json
                                        ▼   (ADR-014 两阶段烘焙)
  L1  DATA  ┌────────────────────────────────────────────────────────┐
            │ 21a Schema · 40 史料 · 41 政策 · *.cooked · 45 抽象      │
            └───────────────────────────┬────────────────────────────┘
                                        │ IDataProvider(唯一边界入口)
                                        ▼
  L2  SIM   ┌──────────────────────────────────────────────────────────────────┐
  (门 A)    │ 9 Step/CatchUp ──▶ 5 · 10.Judge · 11 · 17 · 18 · 20 · 25 · 23    │
            │       │         · 24 · 29 · 30 · 37 · 52 · 53 · 27决策 · 6状态机   │
            │       ▼                                                          │
            │  IEventSink.Append  ──▶ 三流(病史<病例<世界)                   │
            └───────┬──────────────────────────────────┬───────────────────────┘
                    │ IEventSink(写)                    │ IVitalsQuery / 只读订阅
                    ▼                                   ▼
  L3 BOUNDARY  ┌─────────────────────────────────────────────────────────────┐
  (仅 BCL)     │ WorldPos · 六抽象点 · SimEvent · VitalsDto · Fix.ToFloat()    │
               │ · IDataProvider · AudioCueDto · IPositionalChannel · codec   │
               └───────┬──────────────────────────────────┬──────────────────┘
                       │                                  │
                       ▼                                  ▼
  L4 边界层(呈现侧)  ┌──────────────────────────┐   ┌────────────────────────┐
  (可引 UnityEngine) │ 4 交互 · 8 诊断 · 13 病人AI │   │ 51 遥测 · 3 输入       │
                     └────────────┬─────────────┘   └───────────┬────────────┘
                                  │                             │
                                  ▼                             ▼
  L5 PRESENTATION    ┌───────────────────────────────────────────────────────┐
                     │ 42 UI · 44 音频 · 39/43/7b/48 屏幕 · 2 相机 · 1 玩家   │
                     │ 纪律:只渲染 / 只触发,永不持有游戏状态(三件套同构)    │
                     └───────────────────────────────────────────────────────┘
                                        │
  L4→L5 横向            1 ──YawBasis(只读)──▶ 2 ──Position──▶ 1   (反向,不成环)
```

> **图中三条读法**:
> ① **`IEventSink` 只有主机一侧的箭头** —— 表现层**没有**回写入口。玩家位移走 `IPlayerMotor` +
>    第二 QoS 上行,由**主机**统一 `Append`(ADR-020 Amendment B)。
> ② **L4 与 L5 之间没有「必须」的箭头** —— 4 / 8 / 13 / 51 消费 DTO、产出**意图**;
>    42 / 44 消费意图、产出**像素与声音**。这是 `ADR-013 §9 C3` / `ADR-018 §一` / `ADR-020 §五`
>    那条「只渲染不持有状态」纪律在拓扑上的形状。
> ③ **L2 内部**的 9 → 全体箭头被折叠成一行 —— 真实依赖图见 `systems-index.md` §5(本节不复制)。

## Data Flow

> **本节回答四个路径**:① 帧更新路径 ② 事件路径 ③ 存读档路径 ④ 初始化顺序。
> 每条路径下:数据名 → 生产者 → 消费者 → 同步调用 / 事件 / 共享状态 → 是否跨线程。

### 3.1 帧更新路径 —— **tick 与渲染帧的相位**

> 🔴 **这是全案最容易被实现错的一处,且错法是静默的。**

```
配置:  TICK_SECONDS = 0.05 (20 Hz · TICK_PERIOD = 50 ms)     [2026-09-20 裁定]
       平面 60 fps → 帧时间 16.67 ms
       ⇒ 50 ms / 16.67 ms = **3.0** —— 不整除,且每帧相位漂移
```

```
ADR-005:359 (逐字)
「⚠️ 20 Hz 与 60 fps(16.6 ms)**不整除** ⇒ `Step` 必须由 `ITickProvider` 驱动、
 **不得挂在渲染帧上**(相位差须由本 ADR 的 tick 边界吸收)。」

`technical-preferences.md`(tick 频率裁定条,逐字)
「③ **与 60 fps 帧时间(16.67 ms)不整除** ⇒ 一个 tick 跨约 3 帧:实现期须定**步相位**
(每 tick 恰好一次 `Step`,由 `ITickProvider` 驱动,**不由渲染帧驱动**)—— 登记为
**实现期义务**,不改本裁定值。」
```

**唯一正确的时序形状**:

```
 真实时间 ──────────────────────────────────────────────────────────────▶
 渲染帧    │F0 │F1 │F2 │F3 │F4 │F5 │F6 │F7 │F8 │F9 │ …      (16.67 ms 一拍)
 tick 边界     │t0        │t1        │t2        │t3             (50 ms 一拍)
 实际 Step     ▲F1 后检查  ▲F3 后检查  ▲F7 后检查  ▲F9 后检查
               └─ 每帧查「真实时间是否越过下一条 tick 边界」,**越过则恰好补跑一次**
                  **禁「每帧 Step(dt)」**、**禁「按帧计数每 3 帧 Step」**
```

| 数据 | 生产者 | 消费者 | 方式 | 跨线程 |
|---|---|---|---|---|
| `Step()` 调用 | **`ITickProvider`**(全案 tick 唯一来源) | 主机 `Sim` | 同步调用 | ❌ 主线程 |
| `CurrentTick` | `ITickProvider` | 5(唯一时间输入,`AC-5-01` BLOCKING)· 9 · 52 | 同步只读 | ❌ |
| 输入采样 | 3(`InputSystem` 回调,**非轮询**) | 10(直读通道)| 事件回调 | ❌ |
| `EmergencyReading` | 3(**全整数**,不含判定)| 10 的 `Judge` | 同步调用 | ❌ |
| 玩家位移 | 1(`CharacterController`)| — | — | ❌ |
| 渲染 | L5 全体 | — | Unity 渲染管线 | ❌ |

> **两条被 ADR 明文禁止的写法**(实现期最常见的近似):
> ① 「`Update()` 里 `accumulator += Time.deltaTime; while(accumulator >= 0.05) Step();`」——
>    在 `TICK_SECONDS` 与帧时间不整除时,这**恰是**一个合法的固定步长累加器,**但它不是本 ADR 的形状**:
>    ADR-005 要求「每 tick 恰好一次 `Step`」。累加器在极端帧时间抖动下**可能一帧跑两次**。
>    ⇒ 采用累加器时**必须补一条不变量断言**(单帧 `Step` 调用数 ≤ 1),否则 P0 的确定性面就开了口子。
> ② 「`Time.fixedDeltaTime = 0.05` + `FixedUpdate()`」—— **`FixedUpdate` 是 PhysX 的固定步,**与
>    本案的 `TICK_SECONDS` 是**两个不同的量**(ADR-020 已定 `CharacterController` **不参与 PhysX 求解**,
>    ⇒ 复用 `FixedUpdate` 会把模拟绑到物理子系统的调度上,违反「tick 不得挂渲染帧」的**同一条理由**)。

### 3.2 事件路径 —— 唯一的写入口

```
  写者(L2 Sim 内)                          主机侧
  ┌───────────────────────────┐
  │ 9(病史)· 37(病例)· 6/17/20 │
  │ /23/24/9/52/27(世界)        │
  └─────────┬─────────────────┘
            │ s.Append(in SimEvent e)      ← 唯一写入口
            ▼
  ┌──────────────────────────────────────────────┐
  │ IEventSink.Append —— **纯函数按 Kind 路由**   │
  │  病史类 Kind      → 病史流                     │
  │  Case*/Judgment*  → 病例流                     │
  │  Structure*/Drop*/Craft/ResourceHarvested/     │
  │  ActorCellEntered/PoiStateChanged/             │
  │  EncounterStarted/Ended/PlayerDied  → 世界流    │
  │  **不在白名单的 Kind ⇒ 构建期拒绝**            │
  └─────────┬────────────────────────────────────┘
            │ 三流并集 = 唯一真源(ADR-009 Amendment E)
            ▼
  ┌──────────────────────────┐
  │ 全序键 (Tick, StreamPriority, Patient, Seq) │
  │ StreamPriority: 病史 < 病例 < 世界          │
  │ Seq 发放域唯一 (Tick, Patient) —— 三流共享   │
  └─────────┬────────────────────────────────┘
            ├──▶ 7a 存档(§3.3)
            ├──▶ 45 `IReplayPipe.Publish`(P1b)
            └──▶ 51 只读订阅(不成为第四个写者)

  客户端一侧(反向):
  1 的跨格意图 ──▶ ADR-001 第二 QoS(unreliable latest-value)──▶ **主机** Append
  10 的 EmergencyAttempt ──▶ ⚠️ **OQ-10-9 未结**:走第二 QoS 会丢,而它是判定输入
```

| 数据 | 生产者 | 消费者 | 方式 | 跨线程 |
|---|---|---|---|---|
| `SimEvent` | L2 各写者 | `IEventSink` | 同步调用(`in` 只读引用)| ❌ |
| 三流 | `IEventSink` | 7a · 45 · 51 · 52 | 事件总线 / 只读订阅 | ❌ |
| 表现态位置 | 主机 | 44 · 动画 · VFX | **共享状态**(`IPositionalChannel` latest-value)| ❌ |
| `VitalsDto` | 9 | 8 · 13 · 42 | 共享状态(只读查询)| ❌ |

> **三处刻意的「零写者」**:29 死亡复活的 `Append` 调用点数 = **0**(写者 = 9);
> 8 诊断的 `Publish` 调用点数 = **0**(`AC-8-1` BLOCKING);2 相机的写入数 = **0**。
> 三者都不是「忘了写」,是**被分配的写入权 = 0** —— 判据落点 = 反射 / 程序集白名单断言。

### 3.3 存读档路径

> 依据:`adr-010` §一 布局 / §二 折叠 / §四 恢复 / §六 时机 / §七 迁移。

```
  ┌─ 写(checkpoint · 默认 5 分钟 / 退出 / 7b 手动槽)─────────────────┐
  │  ISaveService.Checkpoint(SaveSlot slot)                          │
  │   ① 序列化**主线程**(快照三流 + 头部)                            │
  │   ② 写盘**后台线程**(原子写 = tmp + flush + rename)              │
  │      ⚠️ 后台线程**禁调 Unity API**(ADR-010 Implementation G.7)   │
  └──────────────────────────────────────────────────────────────────┘
  ┌─ 文件布局(全二进制 · 显式小端)────────────────────────────────┐
  │  SaveHeader{ Magic "DYJQ" · SaveVersion · WorldSeed ·            │
  │              ConfigVersion · Tick · SnapshotOffset · SHA256 }    │
  │  + 三流(病史可折叠 / 病例流不折叠 / 世界流不折叠)                │
  │  + 快照段(加载加速,**非真源**)                                 │
  └──────────────────────────────────────────────────────────────────┘
  ┌─ 读 ─────────────────────────────────────────────────────────────┐
  │  ISaveService.Load(SaveSlot slot)                                │
  │   ① 校验和(SHA256)失败 ──▶ **自动回退 `bak`**                    │
  │   ② `SaveVersion` 不符 ──▶ **迁移链**(版本号 + 脚本)             │
  │   ③ `ConfigVersion` 比对(内容哈希 u32)──▶ 决定后续窗口配置       │
  │   ④ **三流重放**(不是「读快照恢复」—— 快照只加速)                │
  └──────────────────────────────────────────────────────────────────┘
  ┌─ 离线补算(在场才模拟的直接后果)──────────────────────────────┐
  │  读档 tick ≠ 存档 tick ⇒ 对**离屏病人**执行 `CatchUp(t_last, t_now)` │
  │  ⚠️ `AC-3` 的「逐位相同」判据**只在 `MAX_SCAN_STEPS` 未触发的子区间**成立 │
  │     —— 截断不削弱 `Step ≡ CatchUp` 的构造等价(9 的 GDD `:83-91`)     │
  └──────────────────────────────────────────────────────────────────┘
```

| 数据 | 生产者 | 消费者 | 方式 | 跨线程 |
|---|---|---|---|---|
| 存档字节流 | 7a codec(L3)| 磁盘 | 文件 I/O | ✅ **写盘在后台** |
| `ConfigVersion` | 21a 数据集内容哈希 | 存档头比对 | 共享状态 | ❌ |
| 序列化对象图 | 三流 + 头部 | `ISaveCodec` | 同步调用 | ❌(序列化在主线程)|

> **🔴 本路径的 HIGH RISK 点**:`Addressables` 6.2+ **加载失败抛异常**
> (`breaking-changes.md:71-89`,见 §Engine Knowledge Gap 摘录)。ADR-014 §五 已裁定
> 「加载失败 = **启动期硬失败**,绝不 null 解引用」——
> **但「启动期」与 `SceneManager` 的时序关系在 `docs/architecture/` 内零记载** ⇒ §Required New ADR #1。
> **✅ 2026-09-23 已承接**:ADR-014 §五 的「首次 `Step` 前预载」、ADR-023 §④ 序列
> (data-core 预载 → `data-core` → 存档重放 + CatchUp → World additive → 表现态重建 → tick driver 起相 →
> 首次 `Step`)→ §③(Boot 常驻)。§3.4 [0]/[7] 的 🔴 已由 ADR-023 ①③④⑦ 承接改判(见下)。

### 3.4 初始化顺序

> **依据**:`adr-014:354`「**启动预载**:首次 `Step` 前 `data-core` 常驻;加载失败 ⇒ 启动期硬失败」。

```
  [0] Unity 引擎启动 · URP Asset 加载                    ✅ ADR-023 ①③(Boot 常驻含相机 + AudioListener)
      │
      ▼
  [1] data-core 预载(Addressables 组)                  ADR-014 §五
      │   ① 装载 `*.cooked`(Fix = raw long,零 JSON 解析器)
      │   ② 失败 ⇒ **硬失败并给出清晰错误**(不 null 解引用)
      │   ⚠️ 门 A:sim **不得**直接触 Addressables —— 装载落 `IDataProvider`
      ▼
  [2] `IDataProvider` 就绪 ⇒ sim 可读烘焙数据             ADR-014 §五
      │
      ▼
  [3] tick 驱动器(`ITickProvider`)就绪                    ADR-005
      │   ⚠️ 必须早于**首次 Step**,且**独立于渲染帧**
      ▼
  [4] 存档判定:新游戏 / 载入                              ADR-010
      │   载入路径:校验和 → bak 回退 → 迁移链 → **三流重放**
      ▼
  [5] 三流重放至头部 `Tick` ⇒ sim 状态重建                ADR-005 · 009
      │   ⚠️ `max(patient_id)` 高水位 = **扫三流并集**重算(ADR-006 Amendment B)
      │   ⚠️ `ItemInstanceId` 同法(ADR-010 §五,义务 6)
      ▼
  [6] 离线 `CatchUp`(若 `当前 tick > 存档 tick`)         ADR-005 · 9 的 GDD
      │
      ▼
  [7] 表现层就绪(L4 → L5)                               ✅ ADR-023 ④⑦(重建次序 + 确定性格内偏移)
      │   8 / 13 开始消费 `VitalsDto`;42 / 44 开始订阅
      ▼
  [8] 首次 `Step` —— 主循环开始
```

> **⚠️ 本节的诚实标注(2026-09-23 改判)**:上表 **[0] 与 [7] 两步原标 🔴「零裁决」,现由
> **ADR-023**(Accepted,2026-09-20;S1/S3/S4 spike 已实测通过 2026-09-23)分别承接 ——
> **[0]** = ① 三场景制 + ③ Boot 常驻(相机 + `AudioListener` 住 Boot,全程不卸载);
> **[7]** = ④ 启动/读档序列(表现态重建在 tick driver 起相之前)+ ⑦ 确定性格内偏移(格锚点 + 已登记整数哈希派生)。
> 承接件权威出处:`docs/architecture/adr-023` ①③④⑦ + §④ 序列图。**本节自此不再挂 §Required New ADRs #1。**
> **P0 的一条已知约束**(9 的 GDD `:763`,逐字):「9 的 `Step` 与 `CatchUp` **在同一帧内完成**
> (同步模型,ADR-005);离线补算**可以跨帧分片**,但不可与帧占比预算脱钩」。

## API Boundaries

> 本节写**契约本身**,用项目语言(C#)。来源:`architecture.yaml` 的 15 条 `interfaces` ·
> ADR 的 `## Key Interfaces`(ADR-005 / 010 / 014 / 016 / 020)· GDD 的 §Interactions 表。
> **本文件不发明新契约** —— 凡未在 ADR / GDD 登记过的签名,本节一律标 ⚠️。

### 4.0 边界总则(三条,均来自 ADR-005 Amendment F)

```
① **边界程序集是白名单,不是通道** —— 「表现层能碰的只有上述列举的类型;
   新增跨门类型须**追加进本法**(Amendment F),不得就地塞进边界程序集。」
② **门 A 是单向约束** —— 它只约束 sim 实现程序集;边界程序集被两侧共同引用是**允许且必需**的。
③ **判据一律落到可执行形式** —— asmdef 引用白名单断言 / IL 反射扫描 / 构建期不变量。
   **不用 grep** —— ADR-020 §四 已明写:`ActorCellEntered` 的跨格检测**必须**读 `Vector3`
   才能算 `FloorToInt(p / LATTICE_SIZE)`,grep 会误杀正确实现。
```

### 4.1 L3 BOUNDARY —— 跨门契约(`Sim.Contracts` ✅ 名已裁 —— ADR-025 ①)

```csharp
// ══ 坐标:唯一空间类型(ADR-015 §三)══
public readonly struct WorldPos            // 地形格 / 建造槽 / 掉落锚点 / 资源点 /
{                                          // 导航格 / 医馆房间格 **共用同一套格**
    public readonly int X, Y, Z;           // i32 三元组;**禁第二套坐标**;**禁 chunk 局部坐标系**
}

// ══ 事件:唯一可全序的载体(ADR-006 Amendment A + ADR-009 Amendment E)══
public readonly struct SimEvent
{
    public readonly long      Tick;        // 逻辑 tick(非墙钟、非帧)
    public readonly PatientId Patient;     // PatientId.None = -1(世界级事件哨兵)
    public readonly long      Seq;         // ⚠️ long,不是 int —— 写 int 会让自定义编码器
                                           //    写出的流宽差 4 字节 ⇒ 读流错位(静默)
    public readonly EventKind Kind;        // 未登记的 Kind ⇒ **构建期拒绝**
    public readonly EventPayload Payload;
}
// 全序键(跨流): (Tick asc) → StreamPriority(病史<病例<世界) → (Patient asc) → (Seq asc)
// Seq 发放域唯一 = (Tick, Patient) —— 三流共享,零新全局计数器

// ══ 六个 P0 抽象点(ADR-005 §Key Interfaces + ADR-007 §一)══
public interface ITickProvider   { long CurrentTick { get; } }
public interface IEventSink      { void Append(in SimEvent e); }     // 主机唯一
public interface IIdAuthority    { PatientId Next();
                                   ItemInstanceId Next(); }          // ✅ QQ-02 已结(2026-09-23):ADR-005 已回填双方法,
                                                                     //    逐字承 ADR-010 §五(机制 A);原「ADR-005:235 仍标待办」过期
public interface IVitalsQuery    { VitalsDto GetVitals(PatientId p); } // **唯一浮点出口**
public interface IEventAuthority { bool IsAuthority { get; }
                                   EventRollResult Roll(in RollRequest r); }

// ══ 定点:无隐式浮点转换(ADR-006)══
public readonly struct Fix                 // Q16.16,内部 long
{
    private readonly long _raw;
    // **刻意不定义** implicit operator float —— 唯一出口是门面内的显式 ToFloat()
    public float ToFloat();                // ⚠️ 归属见 §Open Questions QQ-01
}
// 运行期**不存在** float → Fix 的路径(编译期不可表达);
// 唯一字符串入口 = FixParse.Parse(string)(导入期,拒浮点字面量);
// 运行期整数构造 = FixParse.FromRatio(long, long)。
// 舍入 = ROUND_HALF_AWAY_FROM_ZERO(禁 Math.Round 的 ties-to-even)。

// ══ 数据装载:sim 的唯一取数口(ADR-014 §五;门 A:sim 不碰 Addressables)══
public interface IDataProvider { TDataSet Load<TDataSet>() where TDataSet : struct; }
// 失败语义 = **启动期硬失败**(Addressables 6.2+ 抛异常,see §Engine Knowledge Gap);
// 禁 null 解引用。导航格按 chunk 按需激活,未驻留 chunk 视为全 block,**驻留与否不改变判定**。

// ══ 存档 codec(ADR-010 §一 / §五)══
interface ISaveCodec {
    void     WriteEvent(in SimEvent e, ref CodecWriter w);   // 按字段名/tag,**禁位置编码**
    SimEvent ReadEvent(ref CodecReader r);
    void     WriteFix(Fix v, ref CodecWriter w);             // _raw 的 long,显式小端
    Fix      ReadFix(ref CodecReader r);
}
interface ISaveService {
    void Checkpoint(SaveSlot slot);   // 序列化主线程 → 写盘后台
    void SaveOnExit();
    void Load(SaveSlot slot);         // 校验和 → bak 回退 → 迁移链 → **三流重放**
}
// 存档头(全二进制 · 显式小端 · 黄金夹具钉死):
//   SaveHeader{ Magic "DYJQ" · SaveVersion · WorldSeed · ConfigVersion · Tick
//             · SnapshotOffset · Checksum(SHA256) }
// **存档与事件流禁 float**(ADR-006 域边界纪律);
// **禁 UnityEngine.Hash128/JsonUtility/PlayerPrefs**(IL 扫描断言,ADR-010 §四)

// ══ 音频 cue:呈现层 DTO(ADR-018 §一)══
public readonly struct AudioCueDto
{
    // CueId · Intensity(byte) · Tier(byte) · WorldPos Cell · PatientId Source · bool Looped
    // **无 disease_id** —— PresentationDtoGuard **递归**扫描(AC-37-15 一致性)
    // 字段用**原语**(int3 / int),**不引用 sim 的类型** —— 契约程序集仅 BCL
}
public interface IAudioCueSink { void Emit(in AudioCueDto cue); /* + BeginLoop/EndLoop/SetTier */ }

// ══ 表现态位置:第二 QoS(latest-value,ADR-001 §一之二)══
public interface IPositionalChannel
{
    void PublishLatest(in WorldPosLatest v);
    bool TryReadLatest(int actorId, out WorldPosLatest v);
    IDisposable SubscribeActor(int actorId, Action<WorldPosLatest> onUpdate);
}
// WorldPosLatest = { ActorId:int, Cell:int3, ServerTick:uint, Flags:byte }
// **消费方只读,禁读其做判定**(违者 = 静默不可重建通道,ADR-016 §一)
```

### 4.2 L4 边界层(呈现侧)契约

```csharp
// ══ 玩家位移(ADR-020 §四)══
public interface IPlayerMotor
{
    void    Tick(float dt, in MoveInput input);   // ⚠️ float —— see QQ-01
    WorldPos Cell { get; }                        // 整数格:**sim 侧唯一可见的投影**
    Vector3 Position { get; }                     // 连续位置:表现态,**永不写流**
    bool    ConsumeCellChanged();                 // 跨格边沿;True 时由**主机**发 ActorCellEntered
}
// 不变量(调用方必须遵守):
//   ① 连续位置 / 速度 / 朝向 **不得**出现在任何 IEventSink.Append 调用点(AC-20-03 BLOCKING)
//   ② 事件率上界 = tick 频率(每 tick 至多一条),**与帧率 / 移动速度无关**(AC-1-03)
//   ③ 客户端 `Append` 调用点数 = **0**;其格经第二 QoS 上行(ADR-020 Amendment B)
// 判据形式:**反射断言载荷字段类型集合 ⊆ {int32, int64, 整数枚举}** —— 不是 grep

// ══ 相机(ADR-020 §五 / §六)══
public interface ICameraRig
{
    CameraMode Mode { get; }                      // Explore / Treatment / Casebook / FirstPerson(VR,P1a)
    void SetMode(CameraMode m);                   // 换档 = **只改参数**,禁第二处档位分支(R-2-1)
    void Tick(float dt);                          // 防抖 / 缓动吃**表现态 dt**,不入 sim
    Camera Camera { get; }
    (Vector3 fwd, Vector3 right) YawBasis { get; } // **只读** —— 1 的唯一消费方
}
// 不变量: ① 相机**零写入**(系统 1 对相机状态 / 变换的写入数 = 0,AC-2-10②)
//         ② 相机程序集**不引用 sim 实现程序集**(AC-2-04 BLOCKING)
//         ③ `YawBasis` 手性须逐分量等于 normalize(cross(worldUp, f̂))(防**静默镜像**,AC-2-07②)
//         ④ **禁 Normalize()** —— sin²+cos²=1 构造即单位,多一次归一化只引入浮点误差(R-2-7)

// ══ 在场视图(ADR-016 §六;37 读 13,13 不引用 37)══
public interface IPresentPatients
{
    // 只读: { PatientId, WorldPos Cell, 粗状态枚举 }
    // **`signs[]` 永不进本载荷**(AC-13-C1)—— 否则病种语义泄漏进呈现层
    // 13 不写三流、不产 SimEvent(只读不写,ADR-005/009)
}

// ══ 急救输入(ADR-011 Amendment B · 3 → 10)══
public interface IEmergencyInput
{
    // 交出**全整数**读数 —— 每一个字段都必须是整数语义:
    //   { EmergencyAction(枚举索引), hold_ticks(int), edge_ticks[](int), magnitude(int) }
    // ⚠️ 规则八:直读 **float** 只用于**当下这一帧的手感与表现**,**永不进流**;
    //    3 交给 10 的是 `EmergencyReading`,**不是判定**。
    // ⚠️ 熟练度一类的**分数**用 FixParse.FromRatio(long, long)(取整数入参,**无浮点路径**)
}
// 判据(AC-10-02 BLOCKING): **3 侧零 `Judge` / `JudgeResult` / `SimEvent` 引用**
//                          **10 侧判定零浮点字面量**(AC-10-03)
// 生产链(ADR-011 Amendment B 改判后):
//   客户端**:3 聚合为一条 `EmergencyAttempt` 全整数意图事件**上行
//   → **主机执行 `Judge` + `Append` + 发号 `Seq`**
//   → 本地判定**降级为预表现**(不再进流)
```

### 4.3 L2 SIM 内部契约(不进边界程序集)

```csharp
// ══ 模拟推进(9)══
void  Step(State s);                          // ≡ CatchUp(patient, t_last = t, t_now = t+1)
void  CatchUp(in Patient p, long tLast, long tNow);   // **同一求值器、同一套边界检测**
// ⚠️ `MAX_SCAN_STEPS` 截断后 Step ≢ CatchUp;AC-3 的「逐位相同」**只在未截断的子区间**成立
// ⚠️ 离线补算可以**跨帧分片**,但不可与帧占比预算脱钩(9 的 GDD :763)

// ══ 判定(10)══
JudgeResult Judge(in EmergencyAttempt a);     // 枚举 Applied / AppliedWeak / Missed
                                              // **非连续分值**
// ✅ OQ-10-1 已裁:`ResultMul[Missed] = 0.25` —— **非零**,『失败也留一笔』

// ══ 掷骰(5 / 52;零其他随机源)══
EventRollResult IEventAuthority.Roll(in RollRequest r);
// 判据(AC-5-03 BLOCKING): 反射扫描全部掷骰调用点,**入口类型恒为 IEventAuthority.Roll**;
//                          **禁 UnityEngine.Random / System.Random / 任何噪声源**

// ══ 时间(5)══
int season_index { get; }                     // **只交下标、不做乘法**;修正表 SEASON_MULT[] 归 17
long FMod(long, long);  long FDiv(long, long); // **取模与除法只准经这两个具名算子**(AC-5-13)
// TICKS_PER_DAY 单一定义点在 5,52 引用(**禁 const 副本**);两者不一致 = **构建期硬失败**
```

### 4.4 每份契约的「必须保证 / 调用方必须遵守」

| 契约 | 提供方**必须保证** | 调用方**必须遵守** | 判据形式 |
|---|---|---|---|
| `IEventSink.Append` | 按 `Kind` 纯函数路由;未登记 `Kind` **构建期拒绝**;主机唯一 | 只有主机调;`Kind` 必须在白名单内 | 构建期白名单 + 路由表断言 |
| `ITickProvider` | `CurrentTick` 单调;每 tick 恰好一次推进;**不挂渲染帧** | 5 是唯一时间输入;**禁 `Time.deltaTime` / 墙钟 / 帧数** | 反射断言(`AC-5-01` BLOCKING)|
| `IVitalsQuery.GetVitals` | 9 出**无条件真值**(`position` / `trend`);**门槛在 8** | 只读;产出的 `VitalsDto` 是**唯一浮点出口** | 双跑对比 + 静态守门 |
| `IIdAuthority.Next` | 计数器**永不复位 0**;迁移后 `next = max(id)+1`(三流并集) | **客户端零铸造**(D-21-27) | 反射扫构造点(`AC-17-05`)|
| `IEventAuthority.Roll` | 输入全可**从事件流重构**(核心不变量,ADR-007) | 是**唯一**随机源 | 反射扫调用点(`AC-5-03`)|
| `IPlayerMotor` | `Cell` 与 `Position` 一致;跨格边沿恰好一次 | 连续位置不入流;客户端零 `Append` | **反射断言载荷字段类型**(非 grep)|
| `ICameraRig.YawBasis` | 单位正交;手性 = `cross(worldUp, f̂)` | **只读**;禁 `Normalize()`;读取时刻 ≥ 本帧 yaw 更新时刻 | 逐分量断言(`AC-2-07②`)|
| `IPresentPatients` | 只读;`signs[]` 不在载荷内 | 13 不写三流;37 只读 | 递归 DTO 扫描(`PresentationDtoGuard`)|
| `IDataProvider` | 失败 ⇒ **启动期硬失败**;不 null 解引用 | sim 不触 Addressables | IL 扫描(`TryLoad` / `handle.Valid`)|
| `ISaveCodec` | 按字段名编码;**禁 float**;显式小端 | 不自行发明第二套 codec | 黄金字节夹具(ADR-012)|
| `IAudioCueSink` | **无 `disease_id`**;不反向查 | 上游产语义 cue,**44 只把 cue 变成声音** | 递归 DTO 扫描 + asmdef 白名单 |

### 4.5 本节暴露的问题

| 编号 | 问题 | 处置 |
|---|---|---|
| **QQ-01** | **`Vector3` 出在两个 L3/L4 契约里** —— `IPlayerMotor.Position` / `Tick(float dt)` 与 `ICameraRig`(`Vector3` 基 / `Camera` / `Tick(float dt)`)。「边界程序集仅 BCL」与「1 / 2 住表现层、可引 `UnityEngine`」两条同时成立时,**这两个接口该住哪里**无文可证 | §Required New ADRs #2(契约程序集清单)一并裁。三选一:① 拆 `(Cell 整数版 → L3)` + `(Vector3 版 → L5)`;② 承认 L3 允许 `Vector3`(破「仅 BCL」);③ 自建 `Float3` 值 struct<br>⚠️ **2026-09-20 事实核验(未裁定,只钉事实 —— 三个选项的取舍建立在此之上)**:<br>⑴ **二者住 §4.2「L4 边界层(呈现侧)契约」,不住 §4.1 的 L3 抽象点集** ⇒ 「门 A 程序集的引用集白名单」(ADR-017 §二,作用对象 = **仅 `Sim` 一个程序集**)与「这两个接口含 `Vector3`」**不矛盾** —— 所谓僵局**不是**引擎引用白名单破的。<br>⑵ 逐条查消费者后,真正跨门的调用点**只有一处**:`death-and-respawn.md:402`「触发 1 的传送 住**边界程序集**(`IPlayerMotor`)… **跨门契约类型**」⇒ 29 的**结算半边在门 A 内**却要调用含 `Vector3` 的接口。其余全为呈现侧互调(2 / 39 / 42 / 44 / 48 / 10)。<br>⑶ ⇒ **僵局被重述为**:门 A 程序集需不需要一个「能命令传送」的抽象?**选项 ① 拆两版正好覆盖它**(门 A 侧只见整数 `WorldPos` 版传送意图);**选项 ② 破的是 `death-and-respawn` 那一行,不破 ADR-017**;**选项 ③ 引入一个与 `WorldPos` 并立的浮点值类型**,代价是呈现侧全部向量数学要换类型或写转换。<br>⑷ **未决且不属本 QQ**:`OQ-2-6`(`ICameraRig.Camera` 单相机 vs VR 立体双眼)—— 归 ADR-020 修订轮 / #1 相机栈形状<br>**✅ 2026-09-20 裁定(ADR-025 ③ = ①′)**:传送契约拆两半 —— 整数半 `ITeleportCommandSink` 进 `Sim.Contracts`(门 A 可引),`Vector3` 半(`Position`/`Tick(dt)`/`ICameraRig.Camera`)留 `Gameplay.Presentation`,不再是跨门契约成员;②③两案否决留档。**本 QQ 结案。** |
| **QQ-02** | ~~**`IIdAuthority.Next()` 的重载形态** —— ADR-005:235 只声明 `PatientId Next()` 并注明「待办…尚无 ADR」;ADR-010 §五 已给出双方法版本~~ | ✅ **已结 2026-09-23(QQ-02 同批执行,不新开 ADR)**:ADR-005 `IIdAuthority` 就地回填双方法(`PatientId Next()` + `ItemInstanceId Next()`,逐字承 ADR-010 §五 :313-314;原「尚无 ADR」TODO 消解)+ 本文件 §2.1 L3 表行 + `:723` 图解注 + `entities.yaml` D-21-26 注释同步关闭;`TR-itemdb-019` 同批 gap→covered(`adr: ADR-010`)|
| **QQ-03** | **`Fix.ToFloat()` 的归属** —— ADR-005:228 写「仅**门面程序集**可调用」,而该程序集从未定义 | **✅ 2026-09-20 结案(ADR-025 ② = 甲案)**:`Fix` 保持 public 住 `Sim.Contracts`;「仅门面可调用」改由构建期 `ToFloat()` **调用点白名单断言**执法(`Sim` 内调用 = 构建失败);乙案(`internal`+`InternalsVisibleTo`)否决留档 |

> **引擎类型核验**(本节契约里出现的引擎 API):
> `UnityEngine.Vector3` / `Camera` / `CharacterController` / `Transform` —— **LOW RISK**
> (长期稳定 API);`CameraMode` 与 URP 相机栈的交互——✅ **ADR-023 ③(相机 + `AudioListener`
> 住 Boot 全程不销毁,平面/VR 档位走 `ICameraRig.SetMode`,相机只 disable-replaceable 禁 mutate);
> `InputSystem` 回调(`onAfterUpdate`,**非轮询**)—— ADR-011 已核验;`AudioMixer` —— MEDIUM
> (`modules/audio.md:4` 自陈知识缺口)。

## ADR Audit

> 方法:`docs/architecture/adr-*.md` **22 份全部机械扫描**(本表首跑时为 19 份,ADR-023/024/025 于 2026-09-20 同日落盘后重扫)—— 章节存在性 · 版本戳 · 依赖边 ·
> 弃用 API · 引擎版本一致性),再由人工复核歧义项。**不重读全文叙述** —— 判据一律落成可复算形式。

### 5.1 ADR 质量表(**22/22** —— 2026-09-20 含 023/024/025 重跑)

| ADR | Engine Compat | Version | GDD Linkage | Deps 段 | Conflicts | Valid | Engine Knowledge Risk |
|---|---|---|---|---|---|---|---|
| ADR-001 网络 pipe 抽象 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | HIGH(NGO/Fusion 知识盲区;结构面 LOW)|
| ADR-005 确定性模拟 | ✅ | ✅ 6.3 | ✅ | ✅(None) | 无 | ✅ | HIGH(IL2CPP 逐位;裁决不依赖)|
| ADR-006 定点域边界 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | MEDIUM |
| ADR-007 事件权威 / 掷骰 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | LOW |
| ADR-008 病例事件流 | ✅ | ✅ 6.3 | ✅ | ✅ | ✅ 环已解(2026-09-20,5.2)| ✅ | HIGH(post-cutoff;刻意不用)|
| ADR-009 世界状态边界 | ✅ | ✅ 6.3 | ✅ | ✅ | ✅ 环已解 · ⚠️ **Kind 双登记处**(见 5.5)| ⚠️ | MEDIUM(ADR-015 后降级)|
| ADR-010 持久化 / 存档 | ✅ | ✅ 6.3 | ✅ | ✅ | ✅ 环已解(2026-09-20,5.2)| ✅ | MEDIUM |
| ADR-011 输入架构 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | HIGH(接口层纯 C# 不受影响)|
| ADR-012 跨平台确定性 CI | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | HIGH(矩阵须 spike)|
| ADR-013 拟物 UI 框架 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | HIGH(焦点桥 / world-space 三 spike)|
| ADR-014 数据管线 / JSON | ✅ | ✅ 6.3 | ✅ | ✅ | ✅ 环已解(2026-09-20,5.2)| ✅ | MEDIUM(Addressables 6.2+ 抛异常)|
| ADR-015 世界几何 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | LOW |
| ADR-016 AI 架构 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | MEDIUM(仅剩表现层 NavMesh 一处)|
| ADR-017 DOTS 裁决 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | LOW |
| ADR-018 音频架构 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | MEDIUM(AudioMixer 6.3)|
| ADR-019 遥测与隐私 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | LOW |
| ADR-020 玩家控制器 / 相机 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | LOW |
| ADR-021 POI 状态所有权 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | LOW |
| ADR-022 关卡工具 | ✅ | ✅ 6.3 | ✅ | ✅ | 无 | ✅ | MEDIUM(工具实现面;不进构建)|

**四项全绿(2026-09-20 按 22/22 重跑,结论不变)**:**22/22 有 Engine Compatibility 段** · **22/22 版本戳 = Unity 6.3**(零陈旧版本)·
**22/22 有 GDD Requirements Addressed 段**(全部为表格 + 指向具体 GDD 章节)·
**22/22 有 ADR Dependencies 段**(含 `Depends On` / `Enables` / `Blocks` / `Ordering Note` 四行)。

**弃用 API 扫描**:对 `deprecated-apis.md` 的全表(`Input.Get*` / `Resources.Load` /
`Application.LoadLevel` / `Animation.Play` / `WWW` / `Rigidbody.velocity` / `CommandBuffer.DrawMesh` /
`OnPreRender` / `Camera.SetReplacementShader` / `Camera.SetReplacementShader` / DOTS v0 API)
扫 22 份 ADR —— **零真实命中**。4 处 grep 命中全是**禁止式陈述**(「禁 `UnityEngine.Random`」)或
**迁移对照行**(`Application.LoadLevel()` → `SceneManager.LoadScene()`),非使用。

### 5.2 ~~🔴 硬缺口~~ **ADR 循环依赖** —— ✅ **2026-09-20 已修复**(TD 条件 C1)

> **修复状态(2026-09-20)**:三条后补边已从 `adr-008:34` 的 `Depends On` 移入 `Ordering Note`。
> 复算(Tarjan 全盘 19 节点):**SCC size>1 = 空** ⇒ 门的 Circular Dependency Check 通过。
> ⚠️ 下列环分析**冻结为修复前快照**(它解释成因与最小修复集,不是当前状态)。

```
SCC(强连通分量,修复前)= { ADR-008, ADR-009, ADR-010, ADR-014 }   ← Tarjan,四节点一环

环内的边(全部可复核):
  ADR-008 → ADR-014   「🔴 2026-09-17 追加:ADR-014」
  ADR-014 → ADR-010   「ADR-010(§五 ConfigVersion)」
  ADR-010 → ADR-008   「ADR-008(病例流 · 折叠豁免)」
  ADR-010 → ADR-009   「ADR-009(世界流)」
  ADR-009 → ADR-008   「ADR-008(路由纯函数扩展)」
  ADR-008 → ADR-009   「ADR-009 Accepted 后追加依赖」
```

**成因(可复算)**:两条把 ADR-008 卷进环的边**都是在 ADR-008 正文定稿之后追加的**
(一条标 `2026-09-17 追加`,另一条标「ADR-009 Accepted 后追加」)。ADR-008 的原始三依赖
(`005 / 006 / 007`)是 DAG 的合法祖先;环是**后补边造成的**,不是原始设计。

**为什么仍须报 FAIL**:门规原文是「Neither can reach Accepted while the cycle exists」。
本环里**四份均已 `Accepted`** ⇒ 现实后果不是阻塞,而是**「谁是谁的前置」不可判定** ——
任何新 ADR 若要依赖其中之一,无法据依赖图推出可接受的撰写顺序。**且它恰好落在本轮
Required New ADRs 的落点上**(ADR-009 / 014 / 010 三份都被新 ADR 引用)。

**最小修复**:

| 边 | 现措辞 | 建议改为 |
|---|---|---|
| ADR-008 → ADR-014 | 「🔴 2026-09-17 追加:ADR-014」 | 「**引用**(非前置):`Judgment.lexicon_id` 的 ordinal 词表**经 ADR-014 管线产出**;ADR-014 已在 ADR-008 之后 Accepted ⇒ 本边标 **Informative**」 |
| ADR-008 → ADR-009 | 「ADR-009 Accepted 后追加依赖」 | 「**引用**(非前置):世界流为第三条流,ADR-009 **扩展** ADR-008 §一 的路由纯函数;两 ADR 的先后由 **Ordering Note** 记,不进 `Depends On`」 |
| ADR-008 → ADR-010 §七 | 「· **ADR-010 §七**(`ConfigVersion` 不匹配非致命 —— 本 ADR §三 初稿的「拒载」措辞已据此作废)」 | 「**修订记录**(非前置):`ConfigVersion` 口径由 **ADR-010 §七 就地修订本 ADR §三**,是「后件改前件」的**信息流**,不是「前件支撑后件」的**依赖流**;原「拒载」句已划除」 |

> ⚠️ **本件首轮(2026-09-20 签署前)只列了两条边,是为**误算**(登记为 D-6 同族缺陷)。
> 复算依据:`ADR-008` 的 `Depends On` 行(`adr-008:34`)有**三段后补内容**(`009` / `014` / `010 §七`),
> 首版只读了前两段。**Tarjan 实测**:
> - 只删 `008→009` 与 `008→014` ⇒ SCC 退化为 **`{008, 009, 010}` 三环**(`009→008` · `008→010` · `010→009`),**环仍在**;
> - 三条全删 ⇒ **环消失**,`008` 出边清空为纯祖先。
> 门的 FAIL 判据「零 ADR 循环依赖」只有**三条全改**才满足。**修复动作 = 一次文档编辑,不需要新 ADR**,
> 但它**必须在任何新 ADR 撰写之前落地**(否则新 ADR 会被拉进未修复的环)。

### 5.3 覆盖率表(21 个 TR 组 ↔ ADR)

| TR 组 | 系统 | 层 | 总数 | ✅ cov | ⚠️ part | ❌ gap | 承重 ADR |
|---|---|---|---|---|---|---|---|
| `itemdb` | 21 物品与配方数据库 | F | 32 | 21 | 0 | **0**(另 **◆11** —— 2026-09-23 QQ-08;cov 含 partial 1 条合并计;同日 ADR-009 复核轮 `031` gap→covered,残 ❌ 清零)| ADR-006 · ADR-010 · ADR-009 · ADR-008 · ADR-014(并列权威,按簇分工)|
| `skill` | 30 技能与熟练度 | F | 8 | 8 | 0 | 0 | **ADR-026**(2026-09-23 Required #4)|
| `input` | 3 输入与设备 | F | 21 | 17 | 0 | **4** | ADR-011(18 条)|
| `diag` | 8 诊断与体征揭示 | C | 25 | 14 | 0 | **11** | ADR-018(5)· ADR-013(2)+ |
| `disease` | 9 疾病与伤情模拟 | C | 22 | 20 | 0 | 2 | **ADR-005(27,全案最大)**|
| `case` | 37 病例系统 | F | 36 | 30 | 0 | **6** | **ADR-008(27)**|
| `randomevents` | 52 随机事件导演 | F | 32 | 15 | 0 | **17** | ADR-016 · ADR-007 · ADR-019 |
| `patient` | 13 病人 AI 与行为 | F | 24 | 19 | 0 | 5 | **ADR-016(20)**|
| `enemy` | 27 敌人 AI | F | 21 | 21 | 0 | 0 | ADR-016 |
| `combat` | 25 格斗与武器线 | C | 24 | 23 | 0 | 1 | **ADR-020(20)**|
| `emergency` | 10 急救动作 | C | 21 | 16 | 0 | 5 | ADR-011(6)· ADR-016 |
| `prescription` | 11 处方用药 | C | 19 | 15 | 0 | 4 | ADR-006 · ADR-014 |
| `processing` | 18 炮制 | C | 18 | 15 | 0 | 3 | **ADR-014(22)**|
| `inventory` | 20 库存与物品 | C | 16 | 11 | 0 | 5 | ADR-009 · ADR-014 |
| `interaction` | 4 交互系统 | C | 15 | 11 | 0 | 4 | ADR-013 · ADR-011 |
| `timeweather` | 5 时间与天气 | C | 15 | 13 | 0 | 2 | ADR-005 · ADR-012 |
| `worldeco` | 6 世界与生态区 | C | 9 | 8 | 0 | 1 | **ADR-015 · ADR-021 · ADR-022**|
| `leveltool` | 54 关卡工具 | T | 8 | 8 | 0 | 0 | **ADR-022** |
| `player` | 1 玩家控制器与移动 | C | 7 | 7 | 0 | 0 | **ADR-020** |
| `camera` | 2 摄像机与视角 | C | 6 | 6 | 0 | 0 | **ADR-020** |
| `concept` | (跨系统范围声明) | — | 8 | 5 | 0 | **1**(另 **◆2** —— `003` / `004` 已 2026-09-21 转 ◆,承 `traceability-index.md` 同组行的「另 ◆2」记法) | technical-preferences · 用户裁定 |

**⚠️ 表内 cov+part+gap 与 §Technical Requirements Baseline 的分层计数必须分开读**:
本表按 **TR 组**(= GDD 归属),上位表按 **runtime layer**。二者不同源 —— 例如 `input` 组在
`systems-index.md` 里是 **Core**,但在 §5 依赖图与 §System Layer Map 里都记 **Foundation**。
**这是 `systems-index.md` 自身的一处不一致**(行 38 写 Core / 行 509 写 Foundation),
已入 §5.5 的登记层缺陷清单。

### 5.4 75 条 gap 的分组与处置(**2026-09-23 ADR-009 TR 复核轮后**;本表建立时为 93,2026-09-21 ◆ 轮后为 89,同日 #4/#5 兑现轮与 QQ-08 合流后为 77,同日 ADR-028 小裁批再 −1(`TR-audio-011`)后为 76,同日 ADR-009 复核轮再 −1(`TR-itemdb-031`)后为 75 —— 逐簇残余见下,簇间合流勿纵向相加旧值)

| 簇 | 条数 | 代表 | 处置 |
|---|---|---|---|
| **21a schema / 边界** | **0**(原 18) | 已清零 —— `TR-itemdb-031` 于 2026-09-23 ADR-009 复核轮 gap→covered;原 18 ID 见左 | ⭑ **QQ-08 结案(2026-09-23,用户裁定路线 [A])**:6 条 gap→covered(014/018/019/020/021/028,带 ADR 指针 + 禁借绿注)· 11 条 gap→◆ `no-adr-by-design`(**逐簇裁定,非 covered+备注** —— 后者是 D-5 反模式)· `031` 单条 carve-out 留 gap → **同日 ADR-009 复核轮兑现翻 covered**(预挂口径「状态重裁随 ADR-009 TR 复核轮」已执行)⇒ **本簇清零**。**原「建议显式降级…status: gap 改 covered」建议就此作废**(仪器错误,实际 = ◆)|
| **52 事件导演** | **15** ↓(原 17 —— `010` / `031` 两条 2026-09-20 随 Required ADR #2/#3 兑现转 covered,见下行与 `requirements-traceability.md`) | `TR-randomevents-003/008/010/011/014/015/016/017/020/022/023/025/026/027/030/031` | 半数已由 ADR-007 / 016 / 019 定**边界**,未定的是**算法与表**(Hamilton 配额 / CDF walk / 事件池 / 冷却窗口)。`010` / `031`(**asmdef + 构建期校验体系**)是**跨系统 Foundation 项**,见 §Required ADRs |
| **8 诊断铁律** | 11 | `TR-diag-002/004/006/008/009/010/011/012/013/014/015` | 铁律①③⑤ 是**接口边界**(「8 与 11 无数据流」),机制已由 ADR-013 §三 `PresentationDtoGuard` + ADR-018 覆盖 ⇒ **缺的是 39 脉案 GDD 侧的落地**,非新 ADR |
| **30 技能成长** | **0**(原 7) | `TR-skill-001…007` | ⭑ **已清零(2026-09-23,Required ADR #4 = ADR-026)** —— 八条全 covered(含原认定的「唯一真正的 Foundation 级裸缺口」`TR-skill-002`)。本行留档不删 |
| **37 病例规则** | 6 | `TR-case-012/013/016/018/020/027` | 规则语义(立案两路径 / 链式串接 / 同源检测算法)—— **规则**归 37 的 GDD,`018`(同源检测)**算法**须定点化 ⇒ 可能与 #2 合并 |
| **13 病人 AI** | 5 | `TR-patient-005/021/022/023/024` | `021` / `022` 是**写路径归属**(13 只读 ⇒ 归 9 / 10);`023` / `024` 是设计结果 + 无障碍。**写路径归属须裁** |
| **10 急救动作** | 5 | `TR-emergency-006/010/017/018/019` | 判据形状已定(枚举三值 / 跳过 = `Applied`);`017` 的**四门阈值**与 `018`(纯键盘回退)是**数值轮**。**非 ADR 缺口** |
| **20 库存** | 5 | `TR-inventory-004/006/007/015/016` | `004/006/007` 是**不发明结算**纪律(已在 20 的 GDD 内);`015`(容器折重)待 21a 裁;`016`(联机粒度)归 P1b / 45 |
| **3 输入 residual** | 4 | `TR-input-016/017/019/021` | 三条已明标「**非 ADR 缺口,是实测前置**」⇒ 绑 `/test-setup`;`017` / `019` 归 10 的 GDD |
| **11 处方** | 4 | `TR-prescription-001/007/008/019` | `001` 是**支柱一的物理形状**(病名不给 11)—— 已由 8/11 的边界保证;其余是**量纲一致**判据 ⇒ 21a 侧 |
| **4 交互** | 4 | `TR-interaction-*`(残余)| 模态门已由 ADR-013 §十 `IModalState.Modal` 定;残余是 42 / 48 侧落地 |
| **开方外残余** | **5**(原 7) | `TR-concept-006` · `TR-disease-020/022` · `TR-timeweather-006/007` · `TR-combat-024` · `TR-worldeco-009` | ⚠️ **2026-09-21 用户已裁**:范围声明类**只 2 条转 ◆**(`TR-concept-003` MVP 8 条 / `-004` P0 排除清单)⇒ 本簇 7 → **5**;`TR-concept-006`(60/90 fps 帧预算)**不转 ◆** —— 它是**性能承诺**,判据上属「预算待定」而非「结构上无裁决可挂」,仍 ❌ 待最低目标硬件定后裁 |

### 5.5 本轮发现的登记层缺陷(6 项)

| # | 缺陷 | 证据 | 处置 |
|---|---|---|---|
| **D-1** | **三流 `Kind` 有三个登记处,且三个都不完整**(2026-09-20 实测订正 —— 本行原口径「两处 / 缺口 9 支」经逐条对集合后**为假**,见下「实测」列) | **实测**:`entities.yaml` **24** 支(穷举 `name: SimEvent.Kind.*`;**流别 = 世界 12 / 病史 7 / 病例 5**(按 registry 各条 `constraint:` 自述的「落X流」逐条归类;原注「病例 5 + 病史 3 + 世界 16」为误,**首轮订正为「5/12/7」亦误** —— 那版按名字粗分,把 `EnemyInjuryOnset` / `InjuryStateChanged` 记入病史,而两者自述落世界流)· ADR-009 **§三骨架 = 15 支**(世界流专属,**非 4 支**;该件 §二 的追加链自陈「共 15」与本行原口径互相矛盾)· ADR-007 §三 **5 支**(全零 registry 条目)。**并集 = 33 支**(数对、推导错)。**缺口是双向的**:↦ 只住骨架、零 registry 条目 **4 支** = `Craft`(Amendment J 已定稿载荷 · `processing.md:306` 已在生产使用)· `DropSpawned` · `DropClaimed` · `DropDespawned` —— **`CompoundTriggered` 失效模式第三次复发,且这次是 4 支一组**;↦ 只住 registry、三处 ADR 家规文本均未收 **4 支** = `CareApplied` / `CompoundTriggered` / `CompoundExpired`(病史流)· `ConsequenceResolved`(世界流,其 `referenced_by` 指向 `adr-009 §三骨架`,**而该节无它**;`medical-consequences.md:206` 自称走 Amendment 通道,**而该件最后一条是 L(2026-09-19)**,Amendment M 不存在);↦ 另有 **4 处陈旧计数** = `entities.yaml:2002/2043/2077/2107` 写「§三 的 **9-Kind** 骨架」(今日 15) | **须立单一真源**(见 §Required ADRs #3)。**根因非「数字错」而是「无落点件」**:§三骨架 = 世界流专属 ⇒ 装不下三流全集;registry = 全集但缺 4 支;§二 归属规则 = 具名清单 29 支但缺 4 支(= 上表第二行同一组;原写「缺 8 支」为首轮未实测值)。**三个候选真源各自不完整** —— #3 要裁的是这个形状,不是选一个现成的家。**构建期白名单若按 `entities.yaml` 生成 ⇒ 拒收 9 支(4 只住骨架 + 5 只住 ADR-007);若按 ADR-009 §三 生成 ⇒ 拒收 18 支**(两个方向都会死,与 `ActorCellEntered` / `CompoundTriggered` 同型;两数 2026-09-20 集合运算实测,原写「4 / 12」均偏小。<br>**✅ 2026-09-20 裁决面结案(ADR-024 Accepted)**:真源 = registry · Amendment 通道退役 · A1–A5 断言 + `tools/kindgen/`。**✅ 2026-09-23 回写轮:执行面四项全部落地** —— ① 补 9 条 registry 条目(`Craft` / `DropSpawned` / `DropClaimed` / `DropDespawned` + ADR-007 五支 `EventRolled` / `EventArrived` / `ThreatDeferred` / `ThreatDeferralCleared` / `HistoryFlagChanged`,均带 `stream:` / `author:` / `payload_schema:`;另 2026-09-21 第二十七批补 `SkillGrown`)· ② 4 处陈旧「9-Kind」计数(`entities.yaml`,现已注「ADR-024 前历史值,现由 registry 机读」)· ③ `ConsequenceResolved` 幽灵引据就地订正(`entities.yaml:2109` 条目头,原自述划线保留)· ④ ADR-009 §二/§三 节首降级注记已挂(`adr-009:206` / `:260`)。**本行结案。** |
| **D-2** | **`Craft` / `Drop*` 有定稿载荷却无 registry 条目** —— ADR-009 **Amendment J** 已把 `Craft` 载荷定稿(`actor_id / output_instance_ids[] / tool_cell / start_tick / ActualConsumed[]`),**载荷已定但登记未落** | `entities.yaml:1911` 注 vs §三 清单 | ✅ **2026-09-23 回写轮随 D-1 一并修**:`Craft`(`entities.yaml:2310`)/ `DropSpawned`(`:2325`)/ `DropClaimed`(`:2341`)/ `DropDespawned`(`:2355`)四条 registry 条目已建,均带 `stream: world` + `author:` + `payload_schema:`;原「有定稿载荷却无条目」缺口即 D-1 ① 的 4 支 |
| **D-3** | **`architecture.yaml` 三处顶层键重复** —— `performance_budgets` / `api_decisions` / `forbidden_patterns` 各出现 **两次**(一次 `X: []` 空占位,一次实体块) | `:486` + `:508` · `:529` + `:546` · `:838` + `:861` | ✅ **2026-09-20 已清理**(TD 条件 C1 同批):`:486` / `:529` / `:838` 三个空脚手架键删除;normalized-YAML diff 对删前快照 = **空**,证语义不变(解析取后者 ⇒ 删前者零影响)|
| **D-4** | **`tr-registry.yaml` 的 `adr:` 字段值域不纯** —— 应为 ADR ID,实际混入:**6 条整条路径**(`docs/architecture/adr-018-*.md` 等)· `technical-preferences.md` · **`用户裁定 2026-09-14`** · **`—` ×9** | 见 §5.1 扫描 | 归 `/architecture-review`(它是注册表唯一所有者)。**本文件只登记,不代改** |
| **D-5** | ~~8 条 `covered` 却无 ADR 引用~~ ✅ **2026-09-20 已按本行「处置」执行**(TD 条件 C4):8 条统一 `covered → partial`,各加 `blocked_by` + `was_status: covered`;`traceability-index.md` 的 21 行汇总与 8 条摘要行同步回退(实测 243/51/93 两文件一致)—— `TR-case-035` / `TR-case-036` / `TR-disease-021` / `TR-combat-014` / `TR-combat-019` / `TR-interaction-015` / `TR-emergency-016` / `TR-prescription-016`。其中 `TR-emergency-016`(**`ResultMul[Missed] = 0.25`**)与 `TR-prescription-016`(polarity 真源)是**由用户裁定翻转**的 —— 承项目纪律「**裁定 ≠ 验收**」 | §5.1 扫描 | 依项目既有的「**不得借绿**」判据:这 8 条应转 `partial` 并标 `BLOCKED-BY`(承载件是 GDD 的实现轮 / 尚未回写的 AC),**不得维持 `covered`** |
| **D-6** | **§Technical Requirements Baseline 的一句陈述不准确(本文件自身)** —— 原文称「12 项无 TR 组的 P0 系统中,8 项已有 GDD(7a / 23 / 24 / 29 / 42 / 44 / 51 / 53)」,**实际 12 项全部已有 GDD** | `design/gdd/` 逐件存在性核查 | **就地订正**(见下)|

**D-6 订正**(已就地应用):原文的「其中 7a / 23 / 24 / 29 / 42 / 44 / 51 / 53 已有 GDD」漏计
7b(`save-slot-ui.md`)/ 17(`foraging.md`)/ 39(`casebook.md`)/ 48(`tutorial-and-onboarding.md`)。
⇒ **12 项全部有 GDD、全部未挂 TR** ⇒ 这**不是「部分系统的欠账」,是一整批 GDD 的系统性欠账**
(`17` 的 `foraging.md` 有 15 条自述公式/规则却零 TR 行,是最典型的一例)。

## Required ADRs

> 来源 = ① Phase 1–4 中「无 ADR 可依」的裁决(§System Layer Map 🔴①② · §Module Ownership §2.0 ·
> §API Boundaries QQ-01/02/03)· ② §ADR Audit 5.4 的 gap 簇 · ③ 5.5 的登记层缺陷。
> **共 5 条。** ✅ **2026-09-20 逐份裁定轮:#1 / #2 / #3 三条全部兑现并转 Accepted**
> (= ADR-023 附条件 / ADR-025 / ADR-024;QQ-01 / QQ-03 / QQ-05 / C2 / C3 随之结案)。
> **未兑现:仅 #4(30 技能成长定点化)/ #5(13 写路径归属)—— 二者不再是开工阻塞项**
> (#4 归 30 的实现轮前;#5 归 13/10/4 的归属争议,均非 Foundation 关键路径)。
> **✅ 前置动作已完成(2026-09-20,TD 条件 C1)**:ADR-008 的三条后补边已移出
> `Depends On`(**两条**是首轮误算 —— `adr-008:34` 实有 `009` / `014` / `010 §七` 三段后补内容,
> 只删两条会剩 `{008,009,010}` 三环;复算见 §5.2)。全盘 Tarjan **无环** ⇒ 新 ADR 可推序。

### 🟥 必须在写任何代码之前(Foundation 层)

---

#### #1 —— 渲染与场景加载策略(URP + RenderGraph + Scene 生命周期)

> **✅ 兑现件 = ADR-023(2026-09-20 用户照准 ①⑥ 及全件,转 Accepted·附条件)**:
> `docs/architecture/adr-023-scene-lifecycle-rendering.md` —— 附条件 = **S1–S4 实测回填是
> 实现故事的前置,不是裁决效力条件**(S3 前提若被推翻,断言零成本保留,不改判;S1/S4 不可接受
> 则按该件 §Alternatives 另开修订)。本小节保留作 Problem 出处。**C2 的 #1 半边结案。**

**为什么是第 1 条**:这是全案**唯一零 ADR 覆盖的 HIGH-RISK 引擎域**。
`grep -rn "SceneManager|场景管理" docs/architecture/` = **0 命中** —— ADR-001…022 无一份触及(该缺口即 Required ADR #1 = 后立的 **ADR-023**,2026-09-20 注)
场景生命周期;而 `rendering.md` 与 `breaking-changes.md:54-65` 载明 URP 的
`ScriptableRenderPass.Execute(ScriptableRenderContext, ref RenderingData)` **已被 RenderGraph 取代**
(初版签名删改,不是新增重载)。

**必须裁决的四项**:

| # | 问题 | 现状 |
|---|---|---|
| ① | **场景加载 / 卸载生命周期** —— 开放世界无「菜单场景」,`SceneManager.LoadScene()` 与模拟状态边界如何切?单场景 + 流式附加?多场景 additive? | **无文**(grep 0 命中)|
| ② | **`ITickProvider` 与场景重建的次序** —— ADR-005 定「`Step` 不得挂在渲染帧上」,而场景加载会重置帧循环 | ADR-005 §三 定原则,未定**加载期**行为 |
| ③ | **相机栈**(`ICameraRig` 的 `CameraMode` ↔ URP 相机栈 ↔ VR 立体) | ADR-020 定机位,未定 URP 侧相机栈;VR 归 P1a 但**栈形状**须 P0 定 |
| ④ | **L4 边界层(呈现侧)的引擎 API 面** —— 18 ADR 无一条裁决「呈现侧决策层可以调什么」 | §Module Ownership §2.3 曾把该列标为「Engine APIs 栏偏弱 = 无文可证」|

**同时承载**:§System Layer Map 的 🔴①(`RenderGraph` 摘录)与 🔴②(`Addressables.LoadSceneAsync` 摘录);
§Data Flow §3.4 初始化序列的 **[0] Unity/URP 启动** 与 **[7] 呈现层就绪** 两步(现标 🔴 无 ADR)。
**Engine Knowledge Risk: HIGH**(post-cutoff URP + Addressables 6.2+ 抛异常行为均须实测)。

---

#### #2 —— 契约程序集清单与命名(边界程序集 / 门面程序集 / 独立契约程序集)

> **✅ 兑现件 = ADR-025(2026-09-20 用户全件照准,转 Accepted)**(`adr-025-contract-assembly-manifest.md`;
> 023 = #1 · 024 = #3 已占;编号按落盘时序,# 序不连续)。
> **三项裁定**:①具名装配清单**照准且不改名**(`Sim` / `Sim.Contracts` / `Sim.Codec` /
> `Gameplay.Presentation` / `Gameplay.UI` / `Editor.Tools` 族;`Sim` 引用集白名单升格为
> **恰 = {BCL, Sim.Contracts}**)· ②QQ-03 = **甲案**(`Fix` 保持 public,`ToFloat()` 调用点
> 走构建期白名单断言;乙案 `internal`+`InternalsVisibleTo` 否决留档)·
> ③QQ-01 = **①′**(传送契约拆两半:整数半 `ITeleportCommandSink` 进 `Sim.Contracts`,
> `Vector3` 半留 `Gameplay.Presentation`;流事件通道备选未取)。
> ④清单封闭性断言 · ⑤种子测试落 `Sim.Contracts.Tests` · ⑥**QQ-02 不入本件**(= 逐字回填
> ADR-005 的独立义务)。**本件无引擎实测前置。**
> 回写轮义务(不撤销 Accepted):V-6 向 ADR-017 挂 `"references": []` 订正 ·
> V-5「门面程序集 / 独立契约程序集」两称谓四处加注。
> 材料出处:QQ-01 已收窄(唯一跨门消费点 `death-and-respawn.md:402`)· QQ-03 两种读法已摊开。
> **C2 就此两侧全结。**

**为什么**:`grep -rn "asmdef"` 全仓**只有一个名字** —— `Sim.asmdef`(`adr-017:251`)。
另外三个程序集**被引用但从未定义**:

| 未具名的程序集 | 唯一出处 | 引用它的话 |
|---|---|---|
| 「门面程序集」 | `adr-005:228` | `ToFloat() // 仅门面程序集可调用` |
| 「独立契约程序集(仅 BCL)」 | `audio-system.md:242` · `emergency-procedures.md:155` | 「DTO 与 `IAudioCueSink` 住独立契约程序集」|
| 「边界层承载 codec 的程序集」 | `persistence-service.md:577` | 「codec 住边界程序集」|

**必须一并裁掉三个悬置项**:

- **QQ-01 `Vector3` 僵局**(§API Boundaries 4.5)——`IPlayerMotor.Position` / `Tick(float dt)` 与
  `ICameraRig`(`Vector3` / `Camera`)含引擎类型,而「契约程序集仅 BCL」与「1/2 住呈现层」同时成立时
  **两个接口该住哪里无文可证**。三选一:① 拆整数版(L3)+ 浮点版(L5);② 承认 L3 允许 `Vector3`;
  ③ 自建 `Float3` 值 struct。**本项不定,1 / 2 / 3 / 4 的代码无法开工。**
  ⚠️ **2026-09-20 核验后重述**(见 §Open Questions QQ-01 行 (1)~(3)):僵局**不是**引擎引用白名单破的
  (ADR-017 §二 白名单只作用 `Sim`);真跨门点**只有一处** = `death-and-respawn.md:402`。
  **裁定项由此收窄为**:门 A 需不需要一个「命令传送」的整数域抽象 —— 而非「三处家规谁让步」。
- **QQ-03 `Fix.ToFloat()` 的归属** —— `adr-005:228` 说「仅门面程序集可调用」,而该程序集从未定义。
  ⚠️ **与 §2.0 表的联动(2026-09-20)**:`adr-017:251` 给的 `Sim.asmdef` 是 `"references": []`
  (**显式空数组**),而门 A 程序集**必须**能看见 `SimEvent` / 六抽象点(`IEventSink.Append` 的
  实现方在门 A 内)⇒ **拟定名 `Sim.Contracts` 必然在 `Sim` 的引用集内** ⇒
  QQ-03 的「门面」有两种可能读法,须一并裁:
  **(i)** 门面 = 一个 L4 程序集,它是**唯一被允许 `references: [Sim]` 且调 `ToFloat()`** 的门面
  (则 `Sim` 须把 `ToFloat()` 设为 `internal` + `[assembly:InternalsVisibleTo]`,否则任何程序集都能调);
  **(ii)** 门面 = `Sim` **自身**的对外 facade(则「仅门面可调用」这句**无约束力**,`ToFloat()` 实为公开)。
  **两种读法的可验证产物不同**,(i) 能落一条反射断言,(ii) 落不出任何东西。
- **QQ-02 `IIdAuthority.Next()` 的重载形态** —— ADR-005 `:235` 只声明 `PatientId Next()`;
  ADR-010 §五 已给出双方法(`PatientId` + `ItemInstanceId`)。**口径统一 = 逐字回填 ADR-005**(小改)
  → ✅ **已执行 2026-09-23**(QQ-02 结案批;ADR-005 正文 + 本文件三处同步,见 §4.5 QQ-02 行)。

**Engine Knowledge Risk: LOW**(纯程序集边界,零引擎 API)。

---

#### #3 —— 三流 `Kind` 单一登记真源 + 构建期校验体系

> **✅ 兑现件 = ADR-024(2026-09-20 用户全部照准,转 Accepted)**:`docs/architecture/adr-024-kind-single-source.md`
> —— 六项裁定全收(真源 = registry / §三§二 降级注记 / Amendment 通道退役 / 补齐 9 支 + 修陈旧计数 +
> 幽灵引据订正 / `tools/kindgen/` 生成器不引 analyzer / 拒绝表执行体归 ADR-014 阶段 2)。
> **C3 就此结案。** 本小节保留作 Problem 出处;裁决细则以 ADR-024 为准。

**为什么**:`IEventSink.Append` 的**路由是纯函数白名单,未登记的 `Kind` = 构建期拒绝**
(`disease-simulation.md:183-184`)。而 §ADR Audit **D-1 / D-2** 证明
**今天的 `Kind` 有三个出处**:

```
entities.yaml          24 支(世界 12 + 病史 7 + 病例 5;按各条 `constraint:` 自述的「落X流」归类)  ← 2026-09-20 二次实测订正(原注「5+3+16」误;首轮改注「病例5/病史12/世界7」亦误 —— 那版按 Kind 名字粗分,把 EnemyInjuryOnset / InjuryStateChanged 记成病史,而两者 registry 自述**落世界流**)
ADR-009 §三 骨架        15 支(世界流专属:Structure*3 / Drop*3 / Craft / ResourceHarvested /
                             PoiStateChanged / ActorCellEntered / Encounter*2 /
                             EnemyInjuryOnset / InjuryStateChanged / PlayerDied)
                             —— ⚠️ 骨架**不是三流全集**,它只覆盖世界流一条流
ADR-007 §三              5 支(EventRolled / EventArrived / ThreatDeferred /
                             ThreatDeferralCleared / HistoryFlagChanged)—— 零 registry 条目
                         ─────────────────────────────────────────
                         并集 33 支 ≠ 任一处单读;且 §三 15 ∩ registry = 11(4 支缺)
```

**缺口是双向的(2026-09-20 逐条对集合实测 —— 原稿只算了「9 支会被拒收」一个方向,故原数错)**:

| 方向 | 支数 | 成员 | 后果 |
|---|---|---|---|
| 只住骨架、**零 registry 条目** | **4** | `Craft`(Amendment J 载荷**已定稿** · `processing.md:306` 已在生产使用)· `DropSpawned` · `DropClaimed` · `DropDespawned` | 白名单按 registry 生成 ⇒ **这 4 支构建期被拒收**(它们只是被拒集的一部分,全集 **9** 支,另 5 支见下条 ADR-007) —— 与 `CompoundTriggered` / `ActorCellEntered` **同型失效模式第三次复发,且首次成组** |
| 只住 registry、**三处 ADR 家规文本均未收** | **4** | `CareApplied` · `CompoundTriggered` · `CompoundExpired`(病史流)· `ConsequenceResolved`(世界流) | 白名单按 §三骨架生成 ⇒ 被拒集共 **18** 支(13 支 registry 非世界流成员含本行 4 支 + 5 支 ADR-007);本行 4 支的特殊性在**家规文本层面**:`ConsequenceResolved` **自称**经 Amendment 通道进 ADR-009,而**该件最后一条 Amendment 是 L**,通道上无此裁定 —— 引据指向一个不含它的节 |
| 计数陈旧 | **4 处** | `entities.yaml:2002 / :2043 / :2077 / :2107` 写「ADR-009 §三 的 **9-Kind** 骨架」 | 今日 15 支;后续读者会据「9」误判覆盖范围 |

**⇒ 本 ADR 要裁的不是「选哪个现成的家」,而是「三流全集该住哪」—— 三个候选各自不完整**:
§三骨架 = **世界流专属**,结构上装不下三流全集;registry = 全集但缺 4 支;ADR-009 §二
归属规则 = 有清单但缺 **4** 支(实测:§二 五条 bullet 的具名并集 = 世界 15 ∪ ADR-007 的 5 ∪ `InjuryOnset` ∪ `Emergency*2` ∪ `DrugTreatmentApplied` ∪ 病例 5 = **29**;33 − 29 = 4,即上表第二行的同一组)。

**失败模式(2026-09-20 集合运算实测 —— 原两个方向的数都偏小,因未把 ADR-007 的 5 支计入被拒集)**:
若构建期白名单**按 `entities.yaml` 生成**,拒收 **9** 支 = (骨架 ∪ ADR-007) − registry
= 4 支只住骨架 + 5 支只住 ADR-007 §三(全零 registry 条目);
若**按 ADR-009 §三骨架**生成,拒收 **18** 支 = (registry ∪ ADR-007) − 骨架
= 13 支 registry 非世界流成员 + 5 支 ADR-007 —— **两个方向都会死**。
而 `entities.yaml:1908-1911` 自注「骨架在 ADR-009 §三 **即其登记处**」,
把「**双家**」写成了**纪律**。这与项目已记录的 `ActorCellEntered` / `CompoundTriggered`
先例是**同一失败模式**(被自己的白名单拒绝)。

**本 ADR 同时承载两项 Foundation TR**:
- `TR-randomevents-010`(**asmdef / Roslyn 构建期校验体系**)与 `TR-randomevents-031`(**构建期校验:17 条拒绝表**)
  —— 二者是**跨系统 Foundation 项**(不只是 52 的事),现挂 `null` ADR。
- 与 §5.2 的**依赖环修复**同批(环内三份 ADR 034 都被本 ADR 引用)。

**Engine Knowledge Risk: LOW**(纯 C# 构建期工具面;Roslyn analyzer 若引入须另签,见下)。

---

#### #4 —— 30 技能成长的定点化与持久化契约

> **✅ 2026-09-23 已兑现 = ADR-026**(`docs/architecture/adr-026-skill-growth-fixed-point.md`,Accepted)。
> 用户裁定照准。`TR-skill-001…008` 八条全转 `covered`(`001/002` 亦去 `blocking` 注记 —— 定点域与幂运算形式已定);
> `P = 1.4` 作废(限死 `{整数, 整数+1/2}`);幂运算唯一整数实现 `Fix.Pow`(无 libm / 无 float);
> `OQ-7a-9` 结案(折叠豁免扩一类 Kind);义务 14 入 ADR-010 §三。**数值仍归用户**(数值轮,与 `OQ-25-7` 同批)。

**为什么**:`TR-skill-001…007` **7 条全 gap、零 ADR 引用** —— §5.4 认定的
**唯一真正的 Foundation 级裸缺口**。其中 `TR-skill-002`(熟练度成长在整数定点域内求值)
与 `TR-skill-008`(技能成长的存档持久化)是**真缺口**:ADR-005 只定义了 sim 侧的定点纪律泛则,
未把 30 的成长公式纳入;**ADR-010 的存档义务表也未列 30**(§5.3 的 `skill` 组 covered = 1)。

**必须裁决**:`XP_to_next(n) = C × n^P`(`P = 1.4`)的**整数域求值形式**
(幂运算在定点域的唯一实现 —— 与 `TR-diag-008`「禁 libm 超越函数;指数仅取整数或 1/2」同源)、
`CombatPower` 的定点求值、以及**运行时调参表的默认值定点化**。
**⚠️ 数值归用户**(`C` / `P` 的取值是机制数值,本 ADR 只定**求值形式与存储形状**)。

**Engine Knowledge Risk: LOW**(整数数学 + BCL 序列化)。

---

### 🟧 应在相关系统开工前(Core 层)

---

#### #5 —— 13 病人 AI 的写路径归属(只读消费者的副作用归谁)

> **✅ 2026-09-23 已兑现 = ADR-027**(`docs/architecture/adr-027-patient-ai-write-path.md`,Accepted)。
> 用户裁定照准:**两条写路径皆归 10 急救动作**(与 CPR / 止血同构的「玩家对病人的物理干预」);
> `TR-patient-021` / `TR-patient-022` 转 `covered`;`OQ-13-1` / `OQ-13-3` 结案(022 仍 P0 不实现,只登记方向);
> 形态承 ADR-009 §七 三段式 + ADR-020 §四;**13 只出表现 · 8 只声明存在**;新 Kind 归 10 的 GDD 轮(承 ADR-024)。

**为什么**:`TR-patient-021`(**「查体诱发痉挛」的写路径归属**)与 `TR-patient-022`
(**搬运昏迷病人的写路径归属**)两条 gap,根因同一条:
**13 是只读消费者(读 `VitalsDto` / `IPresentPatients`),物理上写不了 sim**
(它住边界层,§System Layer Map L4)。⇒ 两个玩家动作**在事件流里没有写者**。

**ADR-016 §一 的三源不变量**要求「效果进流,决策不进流」——
但这两项的效果**既不是 13 的决策也不是 9 的病程**,而是**玩家的物理干预**。
承 ADR-009 §七 的**三段式**(意图事件 + 当下判距 + 宽容半径)与 ADR-020 §四 的**对称落实**
(玩家跨格发事件),本项的形态**已被两者预置**,缺的是**裁决归属**:

| 候选 | 含义 |
|---|---|
| 归 **10 急救动作** | 与 CPR / 止血同构(玩家对病人的物理动作)|
| 归 **9 疾病模拟** | 痉挛是病程的产物,而非玩家的动作 |
| 归 **4 交互系统** | 与「拾取」同构(意图事件 + 主机判定)|

**Engine Knowledge Risk: LOW**(纯所有权裁决;若涉及动画 / 物理表现则归表现层,不进 sim)。

---

### 🟨 可推迟到实现期

| 项 | 说明 |
|---|---|
| ~~**21a itemdb 的 18 条 schema gap**~~ | ⭑ **QQ-08 结案(2026-09-23,用户裁定路线 [A] —— 本行就此划结)**:原建议「`status: gap` 改 `covered` + 备注」**仪器错误**(会复活 D-5 借绿反模式),实际执行 = **6 条带指针转 covered + 11 条转 ◆ `no-adr-by-design` + `031` 单条 carve-out 留 gap**;◆ 判据同步扩第二类(逐簇裁定的已登记归属件 schema / 边界形状)入 `traceability-index.md` 图例 + `gate-check` SKILL。**登记面与裁决面自此对齐,本行不再有待裁项。** |
| **52 事件导演的算法与表**(15 条 gap 的过半 —— 原 17,`010`/`031` 已于 2026-09-20 翻转)| Hamilton 配额 / CDF walk / 事件池 / 冷却窗口 ——
**边界已由 ADR-007 / 016 / 019 定死**,残余是**数据与算法**,归 52 的实现轮与数值轮。 |
| **`concept` 组的 3 条**(`TR-concept-003/004/006`)| ~~**须用户裁定是否降级为「不需 ADR」**~~
⭑ **已裁(2026-09-21,两批合起来覆盖三条)**:`003` / `004` 转 ◆ `no-adr-by-design`(范围声明,结构上无裁决可挂);
`006`(60/90 fps 帧预算)**显式不降级** —— 性能承诺属「预算待定」非「结构上无 ADR」,仍 ❌ 待最低目标硬件定稿。 |
| **`TR-inventory-016`**(联机库存同步粒度)| P1b 归 45;P0 单机不阻塞。 |
| **`TR-input-016/017/019/021`** | 已明标「**非 ADR 缺口,是实测前置**」⇒ 绑 `/test-setup`(**最低目标硬件定稿** + 两端可跑 player)。

### 🟦 不新开 ADR 的残留(须由既有件承接)

| 项 | 承载件 |
|---|---|
| **8 诊断铁律 ①③⑤**(11 条 gap 的主干)| **39 脉案 GDD + `PresentationDtoGuard` 实现** —— 机制已由 ADR-013 §三 定,缺的是落地 |
| **`TR-diag-010`(G-4 FMA 缓解)** | 改措辞为「**定表化**」(承 `traceability-index.md` 建议)—— ADR-006 的一行补注 |
| **`TR-timeweather-006/007`** | ADR-005 的 tick 纪律已覆盖,缺**构建期硬失败**断言 ⇒ 与 #3 的校验体系同批 |
| **`TR-disease-020/022`** | ADR-005 修正案(扫描单向性 / 中间类型)—— `traceability-index.md` 已如此登记 |
| **`TR-combat-024`**(致死伤形态 / Down 覆盖边界)| **数值轮另裁**(`OQ-25-1` / `OQ-25-3`);`TR-combat-023` 已随 2026-09-20 裁定翻转 |
| **`TR-worldeco-009`** | ✅ **已由 6 的 GDD 结清**(`PoiState` 三态)⇒ registry 待回写 —— ⚠️ **2026-09-21 注**:registry 该条(`tr-registry.yaml:1964-1972`)**现值已是 `status: covered`(`revised: 2026-09-16`)且带 GDD 指针**,「待回写」指的不是 status 字段,而是其 note 内**留白的那个条件**——「⚠️ 本状态由撰写方置为 `covered`,**待首轮 `/design-review` 确认**」。该确认**条件已满足**(6 首轮评审 2026-09-16 → 修订 → 用户裁定 Approved,`reviews/world-and-ecozones-review-log.md:3`);⚠️ **但 note 摘除属 `/architecture-review` Phase 8 职权,本行不代为执行**;且 note 另载**残留 `OQ-6-7`**(单调性是否全局不变量)未结 |

## Architecture Principles

> 五条。**每条都是从既有裁决里读出来的,不是新立的** —— 出处栏可逐条复核。
> 冲突时的优先级 = 序号(1 最高)。**任何新 ADR / 新代码若与 1–3 冲突,须先改本表。**

---

### P1 —— 事件流是唯一真源;派生态不得进流,模拟态不得只活在内存

**表述**:凡「真源 / 权威 / 可感知」三问答「是」的状态,必须有一条 `SimEvent` 承载;
凡可由「事件流 + 版本化烘焙数据 + 二者纯函数」重建的状态,**禁止**进流(那是第二真源);
凡两者皆非的状态属表现态,**禁止**用于任何判定。

**出处**:ADR-005(唯一真源)· ADR-009 §一(三问判据 / 三态分类)· ADR-016 §一(重建三源不变量)。

**为什么它在第一位**:项目已记录的三类静默失败,根因**全是**这一条被破坏 ——
「引用却无登记」(19 支事件类) · 「幽灵引用」(归属指向不存在的符号) ·
「借绿」(裁定翻转后无承载件)。三者都不报错,只在保存 / 重放 / 迁移时暴露。

**机械判据**:`IEventSink.Append` 的路由表必须在**构建期**可枚举且完整(§Required ADRs #3);
`presentation` 侧 DTO 的**递归**字段扫描(`PresentationDtoGuard`,ADR-013 §三)。

---

### P2 —— 模拟数学全在整数定点域;浮点只能出现在两个受控出口

**表述**:sim 的一切数学在 `int64` / Q16.16 内求值;
`float` 只允许出现在**两处**:`VitalsDto`(唯一浮点出口,供呈现层)· `Fix.ToFloat()`(门面内显式转换)。
其余任何位置出现 `single` / `double` 都是构建失败。

**出处**:ADR-005 · ADR-006(`FixParse` 唯一解析入口 · `ROUND_HALF_AWAY_FROM_ZERO` ·
守恒律在整数域求值)· ADR-012(双级黄金夹具)。

**机械判据**:`Sim.asmdef` 的 `"noEngineReferences": true` + 引用集白名单恰 = BCL(ADR-017 §二);
IL 拒集 = **类型谓词 + 操作码**(拒 `conv.r4` / `conv.r8` / `ldc.r4` / `ldc.r8` / `decimal` 运算),
**不是方法名黑名单** —— 后者会漏掉 `decimal.Parse` / `double.TryParse` 一类「签名碰浮点类型」的新增路径。

**一处刻意的例外**:13 病人 AI 的决策住边界层并消费 `VitalsDto`(float)——
它**物理上不可能**住门 A 的程序集,故它不是本原则的违反,是**结构性豁免**
(ADR-016 §一 表已补 13 例外注)。

---

### P3 —— 呈现层只渲染,永不持有游戏状态

**表述**:42 拟物 UI · 44 音频 · 2 相机 · 51 遥测 —— 四者同构:
**只读 DTO、只触发副作用、永不持有可影响判定的状态**,且**永不写三流**。

**出处**:ADR-013 §9 C3(42 · 原始裁决)· ADR-018 §一(**用户裁定 44 与 42 同构**,
是本原则的**首次推广**)· ADR-020 §五(相机只读)· ADR-019 §三(51 住边界层)。

**为什么它是第三条**:它是 **P2 的守门人** —— 浮点出口只有一个,正因为消费它的那一层
**没有任何回写通道**。若呈现层能写状态,`VitalsDto` 立刻变成第二条真源。

**机械判据**:反射扫「呈现侧程序集 → `IEventSink.Append` 的调用点数 = 0」
(29 的 Append 点数 = 0 · 8 的 `Publish` 点数 = 0 · 2 的相机写入数 = 0,三处已分别立 AC);
`disease_id` 不进任何呈现层 DTO(AC-37-15)。

---

### P4 —— 一个概念一个载体;禁第二真源、禁第二求解器、禁第二定义点

**表述**:凡「谁拥有这个值 / 谁算这个数 / 谁定义这个常量」,**全案恰有一个答案**。
第二条路径一律禁止 —— 即使它与第一条**当前等价**。

**出处**:本项目的**最高复发率**模式。已记录实例:
`LATTICE_SIZE` 四方互指的幽灵引用 · `SAFETY_MARGIN` 单文件三比一 · 21a F1 的**唯一求解器**
(18 / 19 / 20 各自重写算式 = 第二求解器)· `TICKS_PER_DAY` 单一定义点(禁 `const` 副本)·
W-1 的「9 + 37 须会签二选一」· 本轮新增的 `Kind` 三登记处(§5.5 D-1)。

**机械判据**:凡跨系统常量须有一条 `constants:` 条目让 `source:` 字段承担归属
(不能只活在公式的变量行内注释里);
凡「X 只引用我提供的 Y」,**Y 必须是本文档内可 grep 到的标识符**;
凡归属句,须 grep 确认被归属方的文档里**真的出现该符号**。

---

### P5 —— 引擎知识缺口以「不用它」消解,不以「知道它」消解

**表述**:凡训练数据未覆盖的引擎 API,默认动作是**结构性回避**(换用长期稳定路径),
而非「标注风险后照用」。若无法回避,则**降级为可替换的接口层 + 运行期最小面**。

**出处**:本项目的**一贯取向**,三次显式裁决:
ADR-017(门 A 使 DOTS **结构性**不可用)· ADR-020 §二(Cinemachine 的风险由「不使用它」消解)·
ADR-014 §三(自研绑定层,**禁** `JsonConvert.DeserializeObject<T>`,使解析器的逐位性问题
从运行期确定性面**消失**)。**注:「官方包」不是豁免** —— DOTS 亦为官方包,已被排除。

**代价(须显式记账)**:回避有成本,且成本落在**别处**。
`game-ci` 矩阵须 spike(ADR-012 F7)· UI Toolkit 焦点桥须 spike(ADR-013)·
Addressables 6.2+ 抛异常须实测(ADR-014)· 关卡工具的 Terrain / NavMesh API 须实测(ADR-022)。
**这些 spike 不是「风险提示」,是 P0 的实现前置** —— 见 §Open Questions。

---

## Open Questions

> 只收**架构层**未决项(机制数值 / 单系统内部规则不在本表 —— 那些归各自 GDD 的 OQ)。
> **优先级**:🔴 = 阻塞开工 / 🟠 = 阻塞所在系统的实现 / 🟡 = 可在实现期定。

| ID | 摘要 | 优先级 | 归谁解 |
|---|---|---|---|
| **QQ-01** | **`Vector3` 在 L3 契约里的归属** —— `IPlayerMotor.Position` / `Tick(float dt)` 与 `ICameraRig` 含引擎类型,与「契约程序集仅 BCL」冲突。三选一(拆两版 / 允许 `Vector3` / 自建 `Float3`)| ~~🔴~~ **✅ 已结 2026-09-20(ADR-025 ③ = ①′)** | §Required ADRs **#2 已兑现**:<br>⚠️ **2026-09-20 核验:本行摘要的「与 ADR-017 白名单冲突」为不成立**(白名单作用对象只有 `Sim`,这两接口在 L4)—— 真跨门点仅 `death-and-respawn.md:402` 一处。**完整事实与收窄后的裁定项见 §4.5 QQ-01 行**,本行不重复 |
| **QQ-02** | ~~**`IIdAuthority.Next()` 的重载口径** —— ADR-005 `:235` 单方法 vs ADR-010 §五 双方法~~ | ~~🟠~~ **✅ 已结 2026-09-23** | 逐字回填 ADR-005(**不新开 ADR**)—— 已执行:ADR-005 双方法回填 + architecture.md 三处 + entities.yaml D-21-26 + `TR-itemdb-019` → covered |
| **QQ-03** | **`Fix.ToFloat()` 的归属程序集** —— `adr-005:228` 称「门面程序集」,该程序集从未定义 | ~~🟠~~ **✅ 已结 2026-09-20** | §Required ADRs **#2 已兑现 = ADR-025 ②(甲案)**:`Fix` public 住 `Sim.Contracts` + 调用点白名单断言执法;乙案(internal+IVT)否决留档 |
| **QQ-04** | ~~ADR 循环依赖 `{008, 009, 010, 014}`~~ | ~~🔴~~ ✅ **已结(2026-09-20)** | ADR-008 `Depends On` 清空为 `{005,006,007}`,三条后补边移入 `Ordering Note`;全盘复算无环。⚠️ 修复过程另立一失:**本表与 §5.2 首版把它记成「两条边」,实为三条**(见 §5.2 复算注)|
| **QQ-05** | **`Kind` 单一登记真源** —— 并集 **33** 支 ≠ 任一处单读;**缺口双向**(4 支缺 registry 条目 + 5 支零 registry 的 ADR-007 支 + 4 支缺 ADR 家规文本 + 4 处陈旧计数)⇒ 按 `entities.yaml` 生成拒收 **9** 支,按 ADR-009 §三 生成拒收 **18** 支 | ~~🔴~~ **✅ 已结 2026-09-20(ADR-024 全件照准转 Accepted)**:真源 = `entities.yaml`(+`stream:`/`author:`/`payload_schema:` 必填)· Amendment 通道退役 · 补齐 9 支 + 4 支家规 + 修 4 处陈旧计数 + `ConsequenceResolved` 幽灵引据订正 · `tools/kindgen/` 生成器 A1–A5 断言。**2026-09-20 两轮订正**:① 原「9 支会被拒收」只算了一个方向且把骨架数当 4;② 首轮订正得「4 / 12」亦偏小 —— 未把 ADR-007 的 5 支(零 registry 条目)并入两侧被拒集。实测(集合运算 + registry 逐条 `constraint:` 归类)见 §5.5 D-1 与 §Required ADRs #3;registry 流别真值 = **世界 12 / 病史 7 / 病例 5** |
| **QQ-06** | **13 的两条写路径归属**(查体诱发痉挛 / 搬运昏迷病人)—— 13 只读,写者在事件流里不存在 | 🟠 | §Required ADRs **#5** |
| **QQ-07** | ~~`concept` 组 3 条范围声明是否降级为「不需 ADR」~~ | ~~🟡~~ **✅ 已结 2026-09-21(两项用户裁定覆盖全部三条)** | `TR-concept-003/004` → ◆ `no-adr-by-design`(门规格修订轮,2026-09-21 同日早批)· `TR-concept-006`(帧预算)**不降级** —— 系性能承诺,❌ 保留待最低目标硬件定稿后裁。**登记面与裁决面自此对齐,本行不再有待裁项** |
| **QQ-08** | ~~**21a 的 18 条 schema gap 是否显式降级**~~ | ~~🟡~~ **✅ 已结 2026-09-23(用户裁定路线 [A] 清账批)** | **A=6 covered(带指针)/ B=11 ◆ `no-adr-by-design`(逐簇裁定)/ C=1 carve-out(`031` 留 gap)**;原「改 covered + 备注」建议作废(D-5 反模式);◆ 判据扩第二类入 traceability 图例 + gate-check SKILL;**零新 ADR**。audio-011/012 另开小裁(真 ADR 候选,不在本 QQ)—— **✅ 同日已小裁 = ADR-028(011 结清);012 归 45 轮(维持 gap)**|
| **QQ-09** | ~~8 条「借绿」条目~~ ✅ **已结(2026-09-20)** —— 8 条转 `partial` + `blocked_by`(TD 条件 C4) | ~~🟠~~ | 残留:`TR-case-036` / `TR-interaction-015` 的 `adr` 两处不一致已标 `adr_divergence`,**对齐归 `/architecture-review`**(并流 QQ-11)|
| **QQ-10** | ~~`architecture.yaml` 三处顶层重复键~~ ✅ **已结(2026-09-20)** —— 三空键删除,diff 证语义不变(见 §5.1 D-3)| ~~🟡~~ | 已结 |
| **QQ-11** | **`tr-registry.yaml` 的 `adr:` 字段值域不纯**(6 条整路径 · `technical-preferences.md` · `用户裁定 2026-09-14` · `—` ×9)| 🟡 | `/architecture-review`(注册表唯一所有者)|
| **QQ-12** | ~~系统 3 的层分类自相矛盾~~ ✅ **已结(2026-09-20)** —— 订正方向 = **§3 :38 改 Foundation**(全仓三处 Foundation::165 类目表 / §6 :514 / 本文件 Axis A :233;Core 为孤例)· **非「择一」**:多数口径为权威。两轴消歧已就地写入 `systems-index.md` §6 注块 | ~~🟡~~ | 已结 |
| **QQ-13** | **12 项 P0 系统的 TR 组缺失** —— 与 QQ-08 不同,这是「GDD 已落盘但**从未挂 TR**」 | 🟠 | `/architecture-review` Phase 8(**批量回填**)|
| **QQ-14** | **OQ-10-9**(`EmergencyAttempt` 走第二 QoS 会**丢**,而它是判定输入)—— 须 ADR-001 的一次窄修订 | 🟠 | 45 的 GDD 轮(P1b 前);**ADR-011 打过补丁但未解决** |
| **QQ-15** | **W-1 会签**(9 + 37 须二选一,推荐挂 `DIS_*`) | 🟠 | 实现前会签(承 2026-09-20 裁定)|
