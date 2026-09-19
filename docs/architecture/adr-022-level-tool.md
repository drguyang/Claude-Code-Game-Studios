# ADR-022: 关卡工具(内容产出的正式系统 · 两层世界单一版权威)

## Status

Accepted

> **2026-09-16 起草并转 Accepted。** 一条核心用户裁定已锁(用户 2026-09-16 照准):
> **关卡工具须**另开 ADR 立新系统** —— **不**并入既有系统行**(裁定原文见
> `design/gdd/reviews/world-and-ecozones-review-log.md` §「用户裁定」表第 ③ 项)。
> **本 ADR 结清 6 世界与生态区的 GDD 登记义务 `O-6-9` 与 `R-6-16`**(首轮 `/design-review`
> 8 条根因之第 4 条「关卡工具无系统归属」)。
> **内容不新裁** —— 全部技术面**派生自已 Accepted 的 ADR-015 / ADR-016 / ADR-014 / ADR-021 /
> ADR-009 的既有裁决**;本 ADR 只做一件事:**把已经存在、但分散在四处、且无拥有者的「关卡工具」
> 收拢成一个正式系统,并指定其输出契约、一致性检查与归属**。

## Date

2026-09-16

## Last Verified

2026-09-16

## Decision Makers

dr_guyang(用户 · **2026-09-16 裁定「另开 ADR 立新系统」**)· technical-director(起草与裁决)
· 6 世界与生态区(全部输入的**消费者** —— 逻辑层整数数据)· 1 玩家控制器(可走性同源的另一端,
`AC-1-33` ②)· 13 / 27 AI(导航格消费者)· 23 模块化建造(同格 / 占用覆盖层)· 52 随机事件导演
(`spawn_anchor` 读 POI 定义)· level-designer(工具的主要使用者)· tools-programmer(工具实现方)

## Summary

系统 **6 世界与生态区**的**全部输入**(生态区整数多边形 · 导航格 · POI 格 · 资源点 · 建造槽位 ·
`TERRAIN_TABLE` · `slopeLimit` / `stepOffset`)由**一件「关卡工具」手工产出**(承 `ADR-015 §一`
的两层世界),而该工具**无系统行、无 ADR、无 GDD** —— `ADR-015:222` 只把逻辑层写成「由关卡工具
独立编写」,`ADR-009:537` 只把它记作「自研关卡工具」,`ADR-016:231` 只把它当导航格来源。
本 ADR 裁决:**关卡工具 = 一个正式系统,归 Tooling 层(编辑期 / 构建期,不运行期)**;
它是**两层世界的唯一作者**,导出 `assets/data/world_*.json` 给 ADR-014 烘焙;
它拥有**六项一致性检查**(CI 可跑),其中可走性同源 / 多边形合法性 / 边界单一源为**硬失败**。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Tooling(Editor)· Core 数据边界(产出的整数逻辑层) |
| **Knowledge Risk** | **MEDIUM** —— **裁决本身**(工具拥有什么 · 导出什么 · 检查什么)是**纯数据边界规格**,**不依赖任何引擎 API**(**LOW**);抬到 MEDIUM 的是**工具实现面**的两处:① Unity 6.x Terrain API(`TerrainData` / `Terrain.CreateTerrainGameObject` / heightmap-splat 写入)在 6.3 中的形态;② 导航格 ↔ NavMesh 一致性检查要用的 **`com.unity.ai.navigation` 烘焙 API**(承 ADR-016 §Engine Compatibility 的同一处 post-cutoff 悬置) |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` · `docs/engine-reference/unity/modules/navigation.md`(NavMesh 烘焙)· `adr-015-world-geometry-fixed-world-lattice.md` · `adr-016-ai-architecture.md` · `adr-014-data-pipeline-and-json-parser.md` · `adr-021-poi-state-ownership.md` · `adr-009-world-state-event-boundary.md` |
| **Post-Cutoff APIs Used** | **工具本身**:Unity Editor / Terrain / `NavMeshBuilder` 的 6.3 形态须实测(工具住 `tools/`,**不进构建**,故其 API 面**不污染出货**)。**产出面**:`*.cooked` 走 ADR-014 的**自研 binder**(零 runtime 解析器、零 post-cutoff 依赖) |
| **Verification Required** | ① **两层一致性检查在 CI 中对已提交关卡可跑通**(承 `ADR-015 §Validation` 的同名判据);② **导航格 ↔ NavMesh 非矛盾**实测(承 `ADR-016 §Validation:442` 的同名判据,表现层);③ 大关卡下导航格的 **chunk 切片打包体积 / 装载预算**(承 §五) |

> **门 A 不约束本工具** —— 关卡工具住 `tools/`、`UnityEngine` / UnityEditor 引用**自由**;
> 门 A(`"noEngineReferences": true`)只约束 **sim 实现程序集**(`ADR-005` / `ADR-017 §一`)。
> 但**本工具产出的逻辑层数据必须过门 A 的通道**:产出是 `*.cooked` 整数,经边界程序集
> `IDataProvider` 交给 sim(**ADR-014 §五**)—— **工具的自由度止于导出契约**。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-015**(Accepted —— 两层世界 · 单一整数格 · §一之补 可走性契约 · §六 第三方仅编辑期)· **ADR-014**(Accepted —— `assets/data/*.json` 作者态 → `*.cooked` 烘焙管线)· **ADR-016**(Accepted —— §五 导航格来源与 NavMesh 分工)· **ADR-021**(Accepted —— POI 定义 = 派生态)· **ADR-009**(Accepted —— 世界流边界)· **ADR-005**(Accepted —— 边界程序集 / `IDataProvider`) |
| **Enables** | **6 的 GDD 义务 `O-6-9` / `O-6-11`** · **P0 单区内容的产出路径**(此前在架构上无主)· `O-6-12`(导航格驻留表态的落地者)· `O-6-14`(`terrain_id` 表的产出方) |
| **Blocks** | **P0 关卡内容开工** —— 在 ADR-022 落盘前,6 的全部输入由一件**无拥有者**的工具产出;本 ADR 落盘后该路径**有主** |
| **Ordering Note** | 本 ADR **不阻塞**任何运行期系统 —— 关卡工具的产物**已**被 ADR-015 / ADR-016 消费(那些 ADR 已 Accepted)。本 ADR 补的是**产出侧的归属与执行点**,不是新的运行期裁决。**顺序上晚于 ADR-021**;是 ADR-021 之后的下一条新 ADR(触发登记于 `world-and-ecozones.md` §Cross-References 的「待建 ADR」行,**本 ADR 落盘后该行已改「✅ 已立 ADR」**) |

## Context

### Problem Statement

系统 **6 世界与生态区**的**全部输入是内容**,不是另一个系统(6 的 GDD 明写「6 没有系统级上游」,
`world-and-ecozones.md:386`)。这些内容由**一件「关卡工具」手工产出**:

- `ADR-015:117,325` —— 逻辑层(**生态区多边形 · POI 锚点 · 资源点 · 导航格 · 建造槽位骨架**)
  由**关卡工具**同时编辑两层并**导出逻辑层整数数据**给 ADR-014 烘焙;
- `ADR-015:143,318` —— `slopeLimit` / `stepOffset` **由关卡工具在「同一处」定义**,
  烘焙进逻辑层并同步为引擎参数,**两侧不一致 = 装载失败**(`ADR-015 §Validation` 的
  「可走性同源 BLOCKING」);
- `ADR-015:290,302,317` —— **两层一致性检查**「在**关卡工具**中存在」,能在 CI 里对已提交关卡跑;
- `ADR-016:231,442` —— **逻辑导航格由关卡工具产出**,与 NavMesh 的对齐检查「在**关卡工具**中存在」;
- `ADR-009:537`(Negative consequences)—— 「世界几何的逻辑层编辑 = **自研关卡工具**」;
- `ADR-016:380` —— NavMesh 漂移「由**关卡工具**的一致性检查兜住」。

⇒ **该工具产出的数据是 6 的全部输入,却无人拥有其正确性**:它**无系统行**、**无 ADR**、
**无 GDD**、**不出现在 `systems-index`**。这不是「少写一个文档」—— 它使得
**`ADR-015 §Validation` 与 `ADR-016 §Validation` 的同源判据无执行点**:
「两层一致性检查在关卡工具中存在」这句话**在架构上悬空**,因为**没有任何东西被指定为「关卡工具」**。

### Current State

- `systems-index.md §2` 枚举 **1–53** 全部是**运行期玩法系统** —— 关卡工具**不在其中**,
  且**没有任何一层**能容纳它(Foundation / Core / Feature / Presentation / Polish 全是运行期)。
- 三份 ADR(`015` / `016` / `009`)各自**引用**关卡工具,但**没有一份定义它** ——
  典型「引用却无登记」(`world-and-ecozones.md:1067` 的失败模式记账:与 `CompoundTriggered` /
  `ActorCellEntered` 同型)。
- 6 的 GDD 首轮 `/design-review`(2026-09-16)把此登记为**第 4 条根因**(「关卡工具无系统归属」,
  🔴),并落义务 **`O-6-9`**;`R-6-16` 明写:**ADR-022 落盘前,P0 单区内容的产出路径在架构上无主**。

### Constraints

- **工具不运行期** —— 它的产物是**烘焙数据**(`*.cooked`),运行期只读产物(ADR-014 §五)。
  ⇒ 关卡工具**不进构建、不进依赖图的运行期部分**;它的 API 面**不污染**门 A。
- **逻辑层永远整数、永远可审**(`ADR-015:222`)—— **第三方地形工具(GAIA / MapMagic 2)的产物
  只进视觉层**,**绝不自动导出逻辑层**(`ADR-015 §六` 已裁)。逻辑层**手工编写**(在关卡工具内)。
- **单版边界**(`O-6-11` / `world-and-ecozones.md:284-290`)—— 视觉层边界**不得**由美术手画第二份;
  必须由**整数多边形生成**。这是**静默**失败类:画面与判定各自成立,只是不一致。
- **可走性同源**(`ADR-015 §一之补`)—— `slopeLimit` / `stepOffset` 是**世界的属性**不是手感旋钮;
  取值由用户在关卡工具中调(数值用户自己调),但**两侧必须同源**。
- **数值用户自己调** —— 本 ADR 交付**形状与检查**,不替用户拍任何几何值。

### Requirements

- 把「关卡工具」指定为**一个有名字、有归属、有输出契约的系统**(用户裁定:立新系统)。
- 定义其**导出契约**(产出哪些数据集 · 什么格式 · 走哪条烘焙管线)。
- 定义其**六项一致性检查**,并**指名哪几项是硬失败**(可在 CI 判定)。
- 声明其**运行期零存在**(工具本身不进构建;出货的只有烘焙产物)。
- 让 `ADR-015 §Validation` / `ADR-016 §Validation` 中「在关卡工具中存在」的判据**有执行点**。

## Decision

**裁决:关卡工具 = 一个正式系统,归 **Tooling 层**(编辑期 / 构建期,**不运行期**);
它是两层世界**逻辑层全部整数数据的唯一作者**,导出 `assets/data/world_*.json` 给 ADR-014 烘焙;
它拥有**六项一致性检查**(CI 可跑),并对其中的**硬失败**三项负执行责任。**

### Architecture

```
                    ┌─────────────────────────────────────────────┐
                    │  关卡工具 (Level Tool)  ——  Tooling 层       │
                    │  编辑期 / 构建期 · 不进构建 · 门 A 不约束      │
                    │                                             │
        ┌───────────┤  · 同时编辑两层(ADR-015 §一)              │
        │           │  · 六项一致性检查 C1–C6(↓ §一致性检查)      │
        │           └──────────────┬──────────────────────────────┘
        │                          │ 导出(唯一作者)
        ▼                          ▼
┌──────────────────┐   ┌─────────────────────────────────────────┐
│ 视觉层 (float)    │   │ 逻辑层作者态  assets/data/world_*.json   │
│ Unity Terrain    │   │ (整数格 / 枚举 / Fix 写字符串)            │
│ 高度图 / splat    │   └──────────────┬──────────────────────────┘
│ / 植被 / 摆件     │                  │  ADR-014 两阶段管线
│ + 生态区遮罩      │                  │  (JsonTextReader 词法 + 自研 binder)
│  ▲ 由整数多边形生成│                  ▼
│  │ (C5 单一源)   │          world_*.cooked  (deterministic 二进制)
│  │               │                  │
└──┼───────────────┘                  ▼
   │                     data-core (Addressables 单一组 · 启动预载)
   │                                  │
   │                                  ▼
   │                    ┌──────────────────────────────┐
   │                    │ 6 世界与生态区(运行期消费者)  │
   │                    │ 装载期校验 LATTICE_SIZE /     │
   │                    │ TERRAIN_TABLE 上界 → 硬失败    │
   │                    └──────────────────────────────┘
   │
   └──► 1 玩家控制器(slopeLimit / stepOffset 同源 · AC-1-33 ②)
```

### Key Interfaces

```
// ① 导出契约 —— 关卡工具产出、ADR-014 烘焙的作者态(命名遵 data-files.md 的 [system]_[name].json)
assets/data/
  world_geometry.json     // LATTICE_SIZE(取值容器;6 拥有)· 世界原点
  world_ecozones.json     // 生态区:{ ecozone_id, name, vertices: WorldPos[] }(整数多边形,开集语义)
  world_poi.json          // POI 定义:{ poi_id, cell: WorldPos, type, guard_ref }(派生态,ADR-021)
  world_resources.json    // 资源点分布:整数格 + 生态区倾向
  world_buildslots.json   // 建造槽位骨架:整数格 + 槽位约束
                          //   ⚠️ 2026-09-17 补(承 modular-building.md 规则三 / B3 裁定):
                          //   含 **初始占用标记 `BakedInitial`**(开档既有家具 / 外壳)
                          //   —— Overlay(0) := BakedInitial;外壳必须走初始占用路径
                          //   (否则 P1a 拆墙要改 6 的静态 Nav,overlay 模型做不到)
  world_nav_{chunk}.json  // 导航格:按 chunk 切片(键 = ADR-015 §四 的同一 chunk)
                          //   { cell, walkable: bool, cost: int }
  world_terrain.json      // TERRAIN_TABLE(地貌材质 → K)· terrain_id 表 · slopeLimit · stepOffset

// ② CI 检查入口 —— 对已提交关卡跑(承 ADR-015:317 / ADR-016:442 的「在关卡工具中存在」)
tools/level/Check.cs  →  LevelTool.ConsistencyCheck(LevelSource src) → CheckReport
    C1 两层漂移 · C2 可走性同源 · C3 导航格↔NavMesh · C4 多边形合法性 · C5 边界单一源 · C6 切片完整性

// ③ 工具的输出是**构建期失败通道**,不是运行期契约
//    (工具不进构建;失败在烘焙期显式 throw,非 Debug.Assert —— 承 world-and-ecozones.md 通用口径)
```

### 一、关卡工具 = 正式系统,归 **Tooling 层**

`systems-index.md §2` 的 1–53 全部是**运行期玩法系统**;关卡工具**不属于任何运行期层**
(它不是 Foundation / Core / Feature / Presentation / Polish 的任何一个)。⇒ **新立类别 `Tooling`**:

| 属性 | 值 |
|------|-----|
| **类别** | **Tooling(编辑期 / 构建期 —— 不出货为运行期依赖)** |
| **程序集** | `tools/level/`(`UnityEngine` / UnityEditor 引用**自由** —— **门 A 不约束**,因其不进构建) |
| **运行期存在** | **零** —— 出货的只有 `*.cooked`(ADR-014);构建产物不含本工具程序集 |
| **P0 计数** | **不计入 P0 的 31 项** —— 它不是玩法系统,是**产出玩法内容的前置工具** |
| **依赖** | 逻辑上产出 6 / 1 / 13 / 27 / 23 / 52 的输入;架构上**不构成运行期依赖边** |

> **不并入既有系统行**(用户裁定明写):6 是**容器**(运行期消费者),关卡工具是**作者**
> (编辑期产出者)—— 二者职责正交。把工具塞进 6 的行会掩盖「6 没有系统级上游」这一事实
> (`world-and-ecozones.md:386`)。

### 二、单一版:关卡工具是逻辑层全部整数数据的**唯一作者**

承 `ADR-015 §一`「两层在编辑期同步(同一关卡工具里一起编)」—— **本 ADR 把它升为契约**:

- **逻辑层**(生态区多边形 · POI 锚点 · 资源点 · 导航格 · 建造槽位骨架 · `TERRAIN_TABLE` ·
  `slopeLimit` / `stepOffset`)**只能由关卡工具编写并导出**。
- **第三方地形工具(GAIA / MapMagic 2)的产物只进视觉层** —— **绝不自动导出逻辑层**
  (`ADR-015:222` 已裁)。美术若用第三方工具雕地形,**手工整理进视觉层**;
  逻辑层**独立手工编写**(保证逻辑层永远是整数、永远可审)。
- **任何以 seed 生成地形 / 多边形的代码都是回归**(`ADR-015:335` 的 grep 守卫)—— 地形不是
  `WorldSeed` 的函数(`ADR-015 §二`)。

### 三、导出契约:`assets/data/world_*.json` → ADR-014 烘焙

- **作者态命名**:`world_[name].json`(承 `ADR-014:139` 的 `[system]_[name].json` 规范);
- **数值一律整数**(格坐标 / 枚举);`Fix` 字段(本工具**不产出 `Fix` 空间量** ——
  空间一律整数格,`ADR-015 §三`)如出现非空间量,写**字符串**(`"3/4"`),
  **禁 JSON 数字**(`ADR-014 §二` 的反浮点纪律);
- **烘焙**:走 `ADR-014` 两阶段管线 → `world_*.cooked`(deterministic 二进制);
  **玩家构建零 JSON 解析器、零 `FixParse`**;
- **分组**:归 `data-core`(ADR-014 §五),首次 `Step` 前预载常驻;
  `ConfigVersion` 由 `assets/data/world_*.json` 的内容哈希派生(改关卡 ⇒ 自动改版本号)。

### 四、六项一致性检查(C1–C6;工具拥有,CI 可跑)

> 前三项**承重** —— 它们把 `ADR-015 §Validation` / `ADR-016 §Validation` 里「在关卡工具中存在」
> 那句悬空判据**落到执行点**。硬失败 **必须显式 `throw`**(非 `Debug.Assert` —— 后者在非 Dev 构建
> 被剥离,`world-and-ecozones.md` 通用口径)。

| # | 检查 | 判据 | 失败等级 | 上游 |
|---|------|------|---------|------|
| **C1** | **两层漂移** | 逻辑占用格 ↔ 视觉地形不矛盾 | **告警** | `ADR-015:302`(漂移**只影响表现**,不改 sim 判定) |
| **C2** | **可走性同源** | `slopeLimit` / `stepOffset` 两侧一致(整数量化后 `\|engineParam − Dequantize(bakedInt)\| ≤ 1 量子`) | **硬失败** | `ADR-015 §一之补` / `§Validation:318`(**BLOCKING**)· 执行落 `AC-1-33` ② |
| **C2'** | **模板 collider 足迹同源**(⚠️ 2026-09-17 补,承 `modular-building.md` 规则八 / 义务 `O-23-9`) | 模块模板的 collider 足迹**从同一占用格派生**;collider 高度 > `stepOffset`(**除非模块显式声明可走过**,如地毯)—— 否则「这里能不能走」对玩家(PhysX)与 AI(整数格)出现**两套答案** | **硬失败** | 玩家不读整数格(评审裁定 B2),走感全在 collider ⇒ 两套可走性必须一致 |
| **C3** | **导航格 ↔ NavMesh 非矛盾** | 「逻辑可走」与「NavMesh 可走」不矛盾(不要求逐格相等) | **告警** | `ADR-016:233,442`(漂移**只影响表现**,表现层 snap) |
| **C4** | **多边形合法性** | 5 项退化:重叠 / 顶点<3 / 自交 / 零面积 / 重合顶点 | **硬失败** | 6 的 `EC-6-7` / `AC-6-19`(烘焙期失败集) |
| **C5** | **边界单一源** | 视觉层生态区遮罩**由整数多边形生成**(非手画第二份) | **硬失败** | `O-6-11` / `world-and-ecozones.md:284-290`(静默失败类) |
| **C6** | **切片完整性** | 导航格按 chunk 切片,每格**恰好覆盖一次**(键 = `ADR-015 §四` 的同一 chunk) | **硬失败** | `O-6-12` / `ADR-015 §四③`(脏块失效) |

> **C2 / C4 / C5 / C6 = 硬失败**(编辑者/CI 可判定,**不静默降级**);
> **C1 / C3 = 告警**(漂移只影响表现,sim 判定不变 —— 与 `ADR-015 §Risks` 同口径)。

### 五、导航格驻留:关卡工具是切片者(`O-6-12` 的落地方)

`O-6-12` 指出一处 ADR 冲突:ADR-014 §五 要求 `data-core` **首次 `Step` 前常驻**,
而 ADR-015 §四 允许 chunk **流式/不驻留** —— 对一块大整数导航格**两者不能同时成立**。
6 的 GDD 已表态:**导航格按 chunk 切片打包,按需经 Addressables 加载**,
**驻留与否不改变判定**(未驻留 ⇒ 视为全 `block`,由 27 的寻路自然绕开 —— 表现态降级)。

**本 ADR 把「切片」这一动作归**关卡工具**:**`world_nav_{chunk}.json` 的 `{chunk}` 键
= `ADR-015 §四` 的同一 chunk;**C6 保证切片完整**。⇒ `O-6-12` 的**工具侧**由此闭环;
`O-6-12` 的两处**就地补注**已同日落盘(否则冲突仍在):① **ADR-015 §四** 补「导航格按 chunk 切片,
产出于 ADR-022」;② **ADR-014 §五** 补「『常驻』只约束小体量启动前置数据,逻辑导航格除外(按需激活)」。
⇒ `O-6-12` **完全闭合**,不再需要新系统。

### 六、与 6 的边界(锁死)

| 面 | 关卡工具(编辑期) | 6 世界与生态区(运行期) |
|----|-------------------|------------------------|
| **生态区多边形** | **编写**(整数顶点) | **消费**(`EcozoneOf` 点→区) |
| **POI 定义** | **编写**(派生态) | **消费**(加载期重建) |
| **POI 状态** | ✖ 不碰 | **拥有 + 唯一写者**(`ADR-021`) |
| **导航格静态部分** | **编写** | **拥有**(`INavLattice`) |
| **建造占用** | 只给**槽位骨架** | ✖ 不持(占用归 23,`O-6-10`) |
| **`LATTICE_SIZE` 取值** | **用户在此调** | **拥有该常量**(F-6-1 所有权反转) |
| **`slopeLimit` / `stepOffset`** | **同源定义**(C2)+ 同步引擎参数 | 消费(1 是执行者) |

> **一句话**:关卡工具产出**派生态输入**(`ADR-021` 的「定义」侧);6 拥有**运行期状态**
> (`ADR-021` 的「状态」侧)。**二者不重叠**。

## Alternatives Considered

### Alternative 1: 把关卡工具并入 6 系统行

- **Description**:6 是「世界与生态区」,把关卡工具记为它的一个子模块。
- **Pros**:零新系统行,与 `ADR-021` 的「不新立系统」取向表面一致。
- **Cons**:6 的 GDD **明写「6 没有系统级上游」**(`world-and-ecozones.md:386`)—— 工具是 6 的**作者**,
  不是 6 的**部分**;把它塞进 6 会制造一条**假依赖边**(6 依赖自己),并掩盖「6 的输入是内容」这一事实。
  且 6 是**运行期**系统,工具是**编辑期** —— 两轴不同。
- **Estimated Effort**: 低
- **Rejection Reason**: 职责正交(容器 vs 作者)· 层级不同(运行期 vs 编辑期)。**用户裁定「不并入既有系统行」直接否。**

### Alternative 2: 关卡工具 = 非系统(仅工具,登记进 `tools/README`)

- **Description**:承认它存在,只写个 README,不立 ADR、不进 systems-index。
- **Pros**:零 ADR 成本。
- **Cons**:这正是**当前状态**(`O-6-9` 的病灶)—— 三份 ADR 引用它却无人定义它与 `ADR-015 §Validation`
  的「在关卡工具中存在」判据**无执行点**。**不立 ADR = 保持静默缺口**。
- **Estimated Effort**: 零
- **Rejection Reason**: 首轮 `/design-review` 第 4 条根因(🔴)判其为**架构无主**;`O-6-9` 是
  **P0 内容开工硬前置**。不裁决即继续无主。

### Alternative 3: 用第三方关卡 / 地形工具统一两层(GAIA / MapMagic 2)

- **Description**:让第三方工具的导出直接充当逻辑层,免去自研。
- **Pros**:省自研成本。
- **Cons**:违反 `ADR-015 §六`(第三方仅编辑期辅助,运行期零第三方)**且**违反 `ADR-015:222`
  (逻辑层**永远整数、永远可审** —— 第三方导出非整数几何)。且第三方工具**不产出** `slopeLimit` /
  `stepOffset` 的同源定义(C2)、不做 C4/C5/C6。
- **Estimated Effort**: 低(但不满足需求)
- **Rejection Reason**: `ADR-015` 已裁「运行期零第三方」;本 Alternative 会让**确定性的唯一真源**
  落在一个不受控的外部导出上。

### Alternative 4: 关卡工具产出**运行期可读**的场景数据(不烘焙)

- **Description**:工具直接导出 Unity 场景 / 资产,运行期读取,不走 `*.cooked`。
- **Pros**:编辑即所见。
- **Cons**:破坏 `ADR-014 §一`(作者态与出货态分离)· `ADR-015 §一`(视觉层采样**禁入 sim**)·
  `ADR-006`(float 入 sim)· `ADR-012`(跨平台逐位性 —— 场景资产在 IL2CPP 下不保证逐位)。
- **Estimated Effort**: 中
- **Rejection Reason**: 结构性错误 —— 会把浮点场景几何喂进 sim,破四条 ADR。

## Consequences

### Positive

- **`O-6-9` 闭合**:关卡工具有了系统行、ADR、输出契约与执行点;P0 单区内容的产出路径**有主**。
- **两条悬空判据落地**:`ADR-015 §Validation:317` 与 `ADR-016 §Validation:442` 的
  「在关卡工具中存在」从此**指向一个被定义的系统**与一组被命名的检查(C1–C6)。
- **C5 消解一个静默失败类**:视觉边界 → 整数多边形**一次定义、两处派生**(`O-6-11`),消除
  「画面与判定各自成立但不一致」的可感知谎言。
- **`O-6-12` 工具侧闭环**:导航格切片者有了名字;ADR-015 §四 的修订退化为一句补注。
- **`O-6-14` 的产出方明确**:`terrain_id` 表由本工具产出,1 的表现层解析 `terrain_id → CueId`。

### Negative

- **一个新系统行** —— 与 `ADR-021` 的「不加 #54」取向表面相悖。**本 ADR 明写差异**:
  ADR-021 拒绝的是**运行期玩法子系统**(POI 生命周期不值得一个玩法系统行);
  本 ADR 立的是**编辑期工具系统**,它**不是玩法系统**,且**用户已明确裁定「立新系统」**。
- **工具实现是一块独立工作量** —— 它不产生玩法,但没有它**任何关卡内容都出不来**。
  ⇒ **P0 内容开工的硬前置**(与 `OQ-6-1` 同级)。
- **C1 / C3 是告警而非硬失败** —— 漂移仍可能进构建(只影响表现)。这是**刻意的**:
  把表现层漂移升为构建失败会让美术不可迭代(`ADR-015 §Risks` 同口径)。

### Neutral

- **`systems-index.md`**:新增类别 `Tooling` + 一行(编号 54);
  优先级分层表标注「工具,不计入 P0 的 31」。
- **`tr-registry.yaml` / `traceability-index.md`**:新增 `system:` slug `level-tool`
  (`TR-leveltool-001…008`)。
- **`architecture.yaml`**:新增一条 `level_tool_authoring` 契约(产出契约 + 六项检查)条目。
- **无运行期涟漪** —— 不新增 Kind、不改三流、不改任何运行期接口。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 工具实现拖延 ⇒ P0 内容无产出路径 | 中 | **高** | 本 ADR 落盘后**工具即 P0 关键路径首项**;可先用**手工 JSON** 直编 `assets/data/world_*.json` 走通管线(编辑器未就绪也能出内容),工具随后替换作者态的前端 |
| Terrain / `com.unity.ai.navigation` 6.3 API 形态须实测 | 中 | 中 | 工具住 `tools/`,API 面**不污染出货**;实测失败只影响工具效率,不影响确定性判据(承 ADR-016 同一处悬置) |
| C2 整数量化容差定得过紧 ⇒ 假阳性硬失败 | 中 | 低 | 容差「≤ 1 量子」为初始口径,进实现时按实测调(数值用户自己调) |
| C5 检查无法机器判定「遮罩是否手画」 | 中 | 中 | C5 的落地是**生成式**:遮罩**由多边形生成**(而非校验是否手画);「禁手画」靠**流程**(遮罩是生成产物,不进作者态) |
| 工具与 6 的边界漂移(工具开始持状态) | 低 | 中 | §六 边界表锁死;工具**无运行期存在**,物理上无法持运行期状态 |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | — | **零**(工具不进构建) | 急救 16.6 ms / VR 11.1 ms **不受影响** |
| Memory | — | 导航格 `*.cooked` 驻留(按 chunk 切片可流式) | 待实测(承 `O-6-12`) |
| Load Time | — | `data-core` 启动预载含 `world_*.cooked`(ADR-014 §五) | 首次 `Step` 前常驻 |
| Network | — | 零 | — |
| **编辑期** | — | 六项检查在 CI 对已提交关卡跑一遍 | 编辑器期预算,不进运行期账 |

## Migration Plan

**无既有实现**(6 未开始,关卡内容未开工)。本 ADR 是**产出侧的首个架构约束**,
在写任何关卡内容之前生效。

1. 落地本 ADR 的涟漪:`systems-index.md`(新类别 `Tooling` + 行 54)· `tr-registry.yaml` +
   `traceability-index.md`(slug `level-tool`)· `architecture.yaml`(产出契约条目)·
   6 的 GDD 的 `O-6-9` / `R-6-16` / `O-6-11` / `O-6-12` / `O-6-14` / `OQ-6-8` 状态回填,§Cross-References 的「待建 ADR」行 → **「✅ 已立 ADR」**。—— **本次即完成**
2. **ADR-015 §四 补注**(承 `O-6-12`):导航格按 chunk 切片,产出于 ADR-022(退化为一句补注)。
   —— **✅ 2026-09-16 本次即完成**(`adr-015 §四` 已补注;并同步 §Validation:325/326 的判据执行点)。
3. 实现关卡工具(`tools/level/`):先出**导出契约**(§三)+ **C1–C6**(§四),
   再补编辑器 UI。
4. 若工具未就绪即需内容:**手工直编** `assets/data/world_*.json` 走通 ADR-014 管线(风险表兜底)。

**Rollback plan**:若关卡工具被证明应由**多个工具**分担(如几何编辑器 + 导航烘焙器分离),
以**新 ADR 拆分**;不得就地改本 ADR 的系统边界(同 `ADR-017 §三` 的「另开 ADR」纪律)。

## Validation Criteria

- [ ] 关卡工具**存在于 `systems-index.md`** 且归 **Tooling 层**(不计入 P0 的 31)。
- [ ] **构建产物不含关卡工具程序集**(运行期零存在;`grep` 构建清单无 `tools/level`)。
- [ ] **逻辑层只能由关卡工具导出** —— 全仓无第三方工具(GAIA / MapMagic 2)自动导出逻辑层的代码路径;
      **无以 `WorldSeed` 生成地形 / 多边形的代码**(grep 守卫,`ADR-015:335`)。
- [ ] **导出契约成立**:`assets/data/world_*.json` 经 ADR-014 烘成 `world_*.cooked`,
      **玩家构建零 JSON 解析器**(`ADR-014 §Validation` 同判据)。
- [ ] **C2(可走性同源)存在且为硬失败** —— 与 `AC-1-33` ② 同判据;两侧不一致即构建失败。
- [ ] **C2'(模板 collider 足迹同源)存在且为硬失败**(2026-09-17 补)—— collider 足迹从同一占用格派生;
      collider 高度 > `stepOffset` 除非模块显式可走过;违反即构建失败(`modular-building.md` 义务 `O-23-9`)。
- [ ] **C4(多边形合法性)存在且为硬失败** —— 5 项退化全部进烘焙期失败集(与 `AC-6-19` 同源)。
- [ ] **C5(边界单一源)存在且为硬失败** —— 视觉遮罩是**整数多边形的生成产物**,不进作者态。
- [ ] **C6(切片完整性)存在且为硬失败** —— 导航格每格恰好覆盖一次,chunk 键 = `ADR-015 §四`。
- [ ] **C1 / C3 为告警**(不静默升为硬失败 —— 漂移只影响表现)。

## GDD Requirements Addressed

<!-- MANDATORY -->

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/world-and-ecozones.md` **`O-6-9` / `R-6-16`** | 6 世界与生态区 | 「6 的几何由关卡工具手工烘焙产出,该工具**当前无系统归属**」—— 须**立新系统** | 本 ADR 把关卡工具定为 **Tooling 层正式系统**,定义输出契约(§三)+ 六项检查(§四) |
| `design/gdd/world-and-ecozones.md` **`O-6-11`** | 6 世界与生态区 | 视觉层边界须与整数生态区多边形**同源**(一次定义、两处派生) | **C5**(§四)—— 遮罩由整数多边形**生成**,非手画第二份;硬失败 |
| `design/gdd/world-and-ecozones.md` **`O-6-12`** | 6 世界与生态区 | 导航格驻留:ADR-014 §五 与 ADR-015 §四 对同一数据集冲突 | §五 把**切片者**归关卡工具(`world_nav_{chunk}.json`);C6 保证切片完整 |
| `design/gdd/world-and-ecozones.md` **`O-6-14`** | 6 世界与生态区 | `terrain_id` 表须有产出方(1 的表现层解析 `terrain_id → CueId`) | §三 导出 `world_terrain.json`(含 `terrain_id` 表)由本工具产出 |
| `docs/architecture/adr-015-...md` **§Validation:317-320** | 世界几何 | 「两层一致性检查**在关卡工具中存在**,CI 可跑」+「可走性同源 BLOCKING」 | 本 ADR 让该判据**有执行点**:§四 的 **C1–C6**(C2 = 可走性同源) |
| `docs/architecture/adr-016-...md` **§Validation:442** | AI 架构 | 「导航格 ↔ NavMesh 一致性检查**在关卡工具中存在**」 | 本 ADR §四 **C3**(+ C6 切片) |

> ⚠️ **6 无独立 GDD 的「需求代生」一节不适用** —— 6 的 GDD **已落盘**(`world-and-ecozones.md`),
> 本 ADR 的需求文本直接引其**登记义务 `O-6-9` / `O-6-11` / `O-6-12` / `O-6-14`**,非代生。

## Related

- **结清** `world-and-ecozones.md` 的 **`O-6-9`**(关卡工具 → ADR-022)· 部分 **`O-6-11`** /
  **`O-6-12`**(工具侧)· **`O-6-14`**(产出方)。
- **承** `ADR-015`(两层世界 · 单一整数格 · §一之补 可走性 · §六 第三方仅编辑期)·
  `ADR-014`(烘焙管线)· `ADR-016`(§五 导航格来源)· `ADR-021`(POI 定义 = 派生态)·
  `ADR-009`(世界流边界)· `ADR-005`(边界程序集 `IDataProvider`)。
- **落地判据** `ADR-015 §Validation:317` · `ADR-016 §Validation:442`(「在关卡工具中存在」)。
- **失败模式记账** `world-and-ecozones.md:1067` —— `CompoundTriggered` / `ActorCellEntered` 同型
  (引用却无登记);**本 ADR 结清「关卡工具」这一处**。
- **上游触发** `design/gdd/reviews/world-and-ecozones-review-log.md` 第 4 条根因(🔴)
  + 用户裁定第 ③ 项(「另开 ADR 立新系统」)。
- **涟漪** `systems-index.md`(新类别 `Tooling` + 行 54)· `tr-registry.yaml`(`TR-leveltool-*`)·
  `traceability-index.md` · `architecture.yaml`(产出契约条目)。
