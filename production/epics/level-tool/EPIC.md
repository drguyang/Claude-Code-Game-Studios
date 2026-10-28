# Epic: 关卡工具(54 · Tooling 层)

> **Layer**: **Tooling**(编辑期 / 构建期 —— **运行期零存在**,不进构建)
> **GDD**: **无** —— 它是工具不是玩法系统;产出契约 + 一致性检查 = **ADR-022**
> **Architecture Module**: `unity/Assets/Editor.Tools.Level/`(asmdef `Editor.Tools.Level`)
> **Status**: **In Progress** —— **S1 / S2 已 Complete(2026-10-29)**;S3–S6 **未实现**
> (本 epic **2026-10-28 立**,同日推翻 2026-10-21 的「不立 epic、不排期」裁定)
> **Stories**: 6 stories — see table below
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-28

---

## ⚠️ 立件缘由 —— 一次**显式推翻**(原文不删,就地加注)

**被推翻的裁定**:`production/milestones/p0-remaining-roadmap-2026-10-20.md` §七 **SP-10** 的
处置列逐字写着:

> **用户 2026-10-21 裁定:不立 epic、不排期**,只在本行登记缺口 ⇒ **取件时不会以为它已有人做**。

**推翻它的裁定**:用户 **2026-10-28** 在 S20-20 挂标 #3–#5 的处置上选定
**「推翻裁定,排期完整关卡工具」**。本 epic 即该裁定的落盘件。

**为什么必须在此写明**:SP-10 那一行**仍然在库**,且逐字写着「不立 epic」。若只在本文件
写「已排期」而不回指 SP-10,**下一个人读到 SP-10 会得到与本文件相反的结论** ——
这正是本仓反复记录的「引用却无登记」失效模式的**反向形态**(登记了却没人回指)。
⇒ SP-10 的处置列须同批加 🔁 注记(见 §涟漪)。

### 立件时 SP-10 的实测,哪些仍然成立

SP-10 的「实测」列**没有被推翻**,推翻的只有它的**处置**:

| SP-10 的实测陈述 | 2026-10-28 复算 | 结论 |
|---|---|---|
| 全库三处引用它(ADR-022 §三 · `systems-index` 登记 · `world-ecozones/story-005` EXTERNAL) | 三处均在 | **仍成立** |
| 它没有 epic、没有 story、不在 §6.5 的 sprint-16…29 表里 | 立件前: `production/epics/` 31 目录**无**关卡工具 | **立件前成立;本 epic 解除前两项** |
| `assets/data/` 26 份 JSON 无 `world_buildslots.json` | ~~26 份 JSON 无它~~ ⇒ **已变**:S20-20 起该文件**存在**(schema 2,13 格) | **已过期,就地订正** |
| 23 的 `IsInBuildSlotRegion` P0 简化为「有槽位即区域内」 | 仍在(`BuildSlotCatalog.cs:103`) | **仍成立** |
| `AC-23-08` 第三半边(占用格 ⊆ `BuildSlotRegion`)**零载体** | 仍零载体 | **仍成立** |
| `world-ecozones/story-005` 排在 sprint-29 批 C,判据 = 「素材与冻结到位才动」 | 仍在 | **仍成立** |

⚠️ **立件 ≠ 缺口已闭**。本 epic 只把「没有承接者」变成「有承接者」;
SP-10 列出的**每一处实测缺口**仍须逐条由本 epic 的 story 兑现。

---

## Overview

关卡工具(54)是**两层世界逻辑层全部整数数据的唯一作者**(ADR-022 §二)。它产出
`assets/data/world_*.json` → ADR-014 两阶段烘焙 → `world_*.cooked` → 运行期经
`data-core` 预载。它是**编辑期 / 构建期的正式系统**,**运行期零存在**、**不进构建**、
**门 A 不约束**(ADR-022 §一)。

它同时是 ADR-015 §Validation 与 ADR-016 §Validation 里「**在关卡工具中存在**」那句判据的
**执行点** —— 在那句话落地之前,那两处判据在架构上是**悬空**的。

**P0 的定位**:它不是玩法系统,**不计入 P0 的 31 项**(ADR-022 §一);但它是
**6 / 1 / 13 / 27 / 23 / 52 全部输入的上游**。它不出货为运行期依赖,但它缺席时
上述六者的数据面**只能是合成的**。

---

## 立件时的实测基线(2026-10-28,全部可复算)

> 这些数字是**本 epic 的起点**,不是计划值。每个 story 的 AC 都以它们为对照。

### 数据集覆盖面:ADR-022 §三 要求 7 类,实有 **1** 类

```bash
for n in geometry ecozones poi resources buildslots terrain; do
  printf "world_%-12s JSON=%s cooked=%s\n" "$n" \
    "$([ -f assets/data/world_$n.json ] && echo 有 || echo 无)" \
    "$([ -f unity/Assets/DataCooked/world_$n.cooked.bytes ] && echo 有 || echo 无)"
done
```

实跑输出(2026-10-28):

```
world_geometry      JSON=无 cooked=无
world_ecozones      JSON=无 cooked=无
world_poi           JSON=无 cooked=无
world_resources     JSON=无 cooked=无
world_buildslots    JSON=有 cooked=有     ← 唯一一类
world_terrain       JSON=无 cooked=无
world_nav_{chunk}   (按 chunk 切片,未实现)
```

### 一致性检查:C1–C6 七项,**全部未实现**

`LevelConsistencyCheck.UnimplementedChecks` 是**返回值里的数据**(不是注释):

```bash
grep -c '"C' unity/Assets/Editor.Tools.Level/LevelConsistencyCheck.cs   # 期望 7
```

实装的是**槽位子集**的 5 条硬失败(重复格 / 类型越界 / 格越界 / `true` 无 `module_id` /
`false` 有 `module_id`),它们**不在 ADR-022 §四 的 C1–C6 表内** —— 是本轮自立的域门。

### 关卡数:**1**(硬编码)

```bash
grep -n 'const int Width\|const int Height\|const int Depth\|LevelId =' \
  unity/Assets/Editor.Tools.Level/P0ClinicLevel.cs
```

`P0ClinicLevel` 是**硬编码的 16×1×16 / `level_id = "p0_clinic"`**;
`LevelExporter` 只有**一个** `FileName` 常量、**一个** `ExportP0ClinicToRepo()` 入口。
⇒ 加第二个关卡**没有任何入口**(不是「配置化程度低」,是**不存在**)。

### 世界几何:**合成**

`MinimalWorldFactory` 造 16×1×16 全 walkable 地形 + POI / 资源格全 `-1`,
并在每次调用时打 `LogWarning` 点名「几何仍是合成」(文件头 §「为什么是合成而不是装载」)。
**这条日志是诊断锚点,不是缺陷** —— 它防的是「世界变空」被误诊为逻辑 bug。

### 运行期消费侧:三个装载面**全未接**

| 消费面 | 现状 | 实测 |
|---|---|---|
| 建造槽位 | ✅ **已接**(S20-20) | `MinimalWorldFactory.LoadSlotCatalog` 走 `IDataProvider` |
| 生态区 | ❌ **未接** | `CompositionRoot.cs:1171` 自陈「生态区注册表在本装配内**尚未有装载面**」⇒ `EcozoneOf` 恒 `None` |
| POI / 建造查询缝 | ❌ **未接** | 同处自陈「两缝恒假」 |
| 导航格 | ❌ **无类型** | 全仓 `grep INavLattice` **零命中** —— 连承载类型都不存在 |
| `TERRAIN_TABLE` 消费者 | ❌ **零消费者** | `TerrainCell.KSpeedIdx` 的 doc 自陈「当前**全仓零消费者**」 |

---

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| **ADR-022: 关卡工具**(主) | Tooling 层正式系统 · 唯一作者 · 导出契约 7 类 · C1–C6 六项检查 · 导航格切片者 · 与 6 边界锁死 | **MEDIUM**(工具实现面 Terrain / `com.unity.ai.navigation` 6.3 API 须实测;**裁决本身 LOW**) |
| ADR-014: 数据管线 | `world_*.json` → 两阶段烘焙 → `.cooked`;`ConfigVersion` 由内容哈希派生;`data-core` 预载 | MEDIUM |
| ADR-015: 世界几何 | 单一整数格 `WorldPos`;手工烘焙固定世界;两层(逻辑 / 视觉)分离;视觉层采样禁入 sim | LOW |
| ADR-016: AI 架构 | 整数导航格寻路;`ADR-016 §Validation` 的「在关卡工具中存在」是本 epic 的执行点 | MEDIUM |
| ADR-021: POI 状态所有权 | 工具产出**定义**(派生态);状态归 6 —— **二者不重叠** | LOW |
| ADR-024: Kind 单一登记真源 | 与本 epic 同构的 Tooling 层先例(`tools/kindgen/`):编辑期 .NET 工具、不进构建、硬失败 `throw` | LOW |
| ADR-025: 契约程序集清单 | `Editor.Tools` 族 `includePlatforms: ["Editor"]`,**不进构建**;清单封闭性 = 未登记 asmdef = 构建失败 | LOW |

**Engine Risk**: **MEDIUM** —— 抬升项**全部在工具实现面**(Terrain API / `com.unity.ai.navigation`
6.3 为 post-cutoff;`NavMeshBuilder` 行为须实测)。**裁决面 LOW**:导出契约是纯数据边界规格,
且工具**不进构建** ⇒ 其 API 面**不污染出货**。

---

## 本 epic 的 Story 拆解

> **拆解原则**:**按 ADR-022 的裁决面切,不按文件切**。
> 每个 story 的 AC 必须能**独立证伪** —— 「停用 X ⇒ 该 story 的红」。
> ⚠️ **顺序是硬依赖,不是优先级**:S1 → S2 → (S3 ‖ S4) → S5 → S6。

| Story | 标题 | 兑现的挂标 | 依赖 | Type | 估 | 状态 |
|---|---|---|---|---|---|---|
| **001** | 关卡源模型泛化 —— 多关卡 + 数据集拆分 | **#4 关卡工具只服务一个关卡** | — | Logic | M | ✅ **Complete 2026-10-29**(原件 `production/qa/evidence/impl-level-tool-story-001-2026-10-29.md`) |
| **002** | `world_geometry` / `world_terrain` 导出 + 烘焙 + 运行期装载 | **#5 世界几何仍是合成**(地形半边) | 001 | Integration | L | ✅ **Complete 2026-10-29**(原件 `production/qa/evidence/impl-level-tool-story-002-2026-10-29.md`) |
| **003** | `world_ecozones` 导出 + C4 多边形合法性 + C5 边界单一源 | **#3 C1–C6**(C4/C5 两条) | 002 | Logic | L | 未实现 |
| **004** | `world_poi` / `world_resources` 导出 + 运行期定义装载 | **#5 世界几何仍是合成**(POI / 资源半边) | 002 | Integration | M | 未实现 |
| **005** | `world_nav_{chunk}` 切片 + C6 切片完整性 + C3 告警 | **#3 C1–C6**(C6/C3 两条)· **#4** 的 chunk 半边 | 002 | Logic | L | 未实现 |
| **006** | C1 两层漂移 + C2 可走性同源 + C2' collider 足迹同源 + CI 接线 | **#3 C1–C6**(C1/C2/C2' 三条) | 002 · 003 | Integration | M| 未实现 |

### 为什么 C1/C2/C2' 排在最后(S6)而不是最先

C2 的判据是「`slopeLimit` / `stepOffset` **两侧一致**(整数量化后
`|engineParam − Dequantize(bakedInt)| ≤ 1 量子`)」——
**「两侧」意味着逻辑侧**和**引擎侧**都必须先存在。
S2 之前 `world_terrain` 不存在 ⇒ 逻辑侧无值可比;S3 之前生态区多边形不存在 ⇒
C5 无对象。**先做 C2 会得到一个只能拿夹具自证的检查**(本项目抓过五次的假绿形态)。

### 为什么 S3 与 S4 可以并行

两者都只依赖 S2 的「数据集拆分 + 导出/烘焙骨架」,彼此**零共享状态**:
生态区是点→区查询(多边形),POI / 资源是格→id 查表。**互不改对方的导出形状**。

---

## Definition of Done

本 epic 完成须**同时**满足:

- [ ] **7 类数据集全部存在**(JSON + cooked),且 `data-core` 组条目齐
- [ ] **C1–C6 七项全部实现**,`UnimplementedChecks` **数组为空**(判据 = 该数组长度 == 0)
- [ ] **每个硬失败项(C2/C2'/C4/C5/C6)各有一条「注入缺陷 ⇒ 检查转红」的负向夹具**
      —— 只有正向绿不算(承本仓「假绿」纪律)
- [ ] **告警项(C1/C3)各有「注入漂移 ⇒ 报出告警但不失败」的夹具**
- [ ] **至少 2 个关卡**能导出(证明 S1 的泛化不是「把 1 改成 2」)
- [ ] **运行期装载面接通**:`world_geometry` / `world_ecozones` / `world_poi` /
      `world_resources` 各有生产装载路径,**且 `MinimalWorldFactory` 被装载路径替换**
      (它的 `LogWarning` 是本 epic 完成时应消失的诊断锚点)
- [ ] `world-ecozones/story-005` 的 EXTERNAL 前置解除(它的判据逐字是「关卡工具 CI」)
- [ ] SP-10 行的 🔁 注记与 `systems-index.md` / `production/epics/index.md` 三处口径一致

> ⚠️ **本 epic 不承诺 P0 时间内完成**。它是**被排期**,不是**被承诺**。
> 排期理由见 §立件缘由;完成时点归 sprint 计划,不在本文件断言。

---

## Out of Scope(明确不属于本 epic)

| 项 | 归属 | 理由 |
|---|---|---|
| **视觉层地形本体**(美术雕的 Terrain 资产) | 美术 / 关卡设计 | 本 epic 只产出**逻辑层**;视觉层由第三方工具 + 手工整理(ADR-022 §二) |
| **模块 prefab 的 collider 本体** | 23 素材轮 | C2' 检查的是「足迹同源」,**不产出 prefab** |
| **`LATTICE_SIZE` 的具体取值** | **用户数值轮** | ADR-022 §六:工具是**取值容器**,6 拥有该常量 |
| **`slopeLimit` / `stepOffset` 的数值** | **用户数值轮** | C2 检查的是「两侧一致」,**不裁定值** |
| **POI 状态机**(`PoiState` 转移) | 6 / ADR-021 | 工具产出**定义**,状态归 6 —— 锁死边界 |
| **建造占用**(实际占了哪些格) | 23 | 工具只给**槽位骨架**(`O-6-10`) |
| **NavMesh 运行期烘焙 / 寻路** | 27 / ADR-016 | 工具产出**导航格数据**;NavMesh 只驱动表现态位移 |
| **`world_buildslots` 的既有实现** | 已完成(S20-20) | 本 epic **不改**它,只把它纳入 7 类数据集的口径 |

---

## Risks

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| **Terrain / NavMesh 6.3 API 为 post-cutoff**,实测行为可能与 LLM 知识不符 | **高** | S2 / S5 的实现故事须实测 | ADR-022 Engine Risk 已登记 MEDIUM;S2 / S5 的第一条 AC = **spike**(先证 API 行为,再写实现) |
| **工具不进构建 ⇒ 门 A / b2 等门不覆盖它** ⇒ 工具侧可写出与运行期不兼容的数据 | 中 | 静默错配 | 导出契约的**读方门**(codec 的 schema / 值域校验)是第二道防线;每个数据集的 codec 须与其导出器**成对**落地 |
| **C1 / C3 是告警级** ⇒ 容易被实现成「永远不报」 | 中 | 检查形同虚设 | DoD 强制「注入漂移 ⇒ 报出告警」的夹具(不是「不报红」就算过) |
| 本 epic 体量大(L+L+M 混合),单个 sprint 装不下 | **高** | 排期失真 | story 拆到 **6 件**,每件独立可交付;**不合并** |
| 排期会挤占 P0 的 feature 交付 | 中 | 里程碑风险 | 见 §立件缘由 —— 用户已显式裁定接受该取舍 |

---

## §涟漪(立件须同批更新的登记点)

| # | 文件 | 改动 | 状态 |
|---|---|---|---|
| 1 | `production/milestones/p0-remaining-roadmap-2026-10-20.md` §七 **SP-10** | 处置列加 🔁 **2026-10-28 推翻**注记(原文不删);并订正「`world_buildslots.json` 全仓零存在」这条**已过期**的实测 | **✅ 2026-10-28 已落** — 复算:`grep -c "2026-10-28 处置被推翻" production/milestones/p0-remaining-roadmap-2026-10-20.md` ⇒ **1** |
| 2 | `design/gdd/systems-index.md:90`(54 行) | 状态列加「**2026-10-28 立 epic,排期**」 | **✅ 2026-10-28 已落** — 复算:`grep -c "2026-10-28 立 epic 并排期" design/gdd/systems-index.md` ⇒ **1** |
| 3 | `production/epics/index.md` | 新增 `level-tool` 行 | **✅ 2026-10-28 已落** — 复算:`grep -c "level-tool" production/epics/index.md` ⇒ **1** |
| 4 | `docs/tech-debt-register.md` `TD-building-01` | ⑨⑩ 两条(Region 简化 / `world_buildslots` 零存在)的处置指向本 epic | **✅ 2026-10-28 已落** — 复算:`grep -c "2026-10-28 ⑨⑩ 两条的处置指向新立 epic" docs/tech-debt-register.md` ⇒ **1**。⚠️ 该行同时登记 **⑩ 已过期**(文件自 S20-20 起存在) · **⑨ 仍成立** |
| 5 | `production/epics/world-ecozones/story-005` | EXTERNAL 前置的承接者由「无」改为本 epic | **✅ 2026-10-28 已落** — 复算:`grep -c "2026-10-28 —— 「ADR-022 关卡工具 CI」这个外部依赖的承接者变了" production/epics/world-ecozones/story-005-discovery-walkthrough-and-tooling-consistency.md` ⇒ **1** |

> ✅ **五条已于 2026-10-28 全部落盘**(逐条复算命令见上表「状态」列)。
> ⚠️ **但「登记已落」≠「缺口已闭」** —— 本节登记的是**承接者的存在性**,不是任何判据的通过。
> 本 epic 的 6 个 story 一条未实现;SP-10 列的实测缺口仍须逐条兑现(见 §立件时的实测基线)。

> ⚠️ **涟漪不是「顺手改」**:SP-10 那一行若不改,本 epic 的立件在库内**自相矛盾**。
> 这条登记是立件的**必要条件**,不是收尾工作。
