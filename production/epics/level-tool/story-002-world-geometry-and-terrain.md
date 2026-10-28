# Story 002: `world_geometry` / `world_terrain` 导出 + 烘焙 + 运行期装载

> **Epic**: 关卡工具(54 · Tooling 层)
> **Status**: Complete
> **Layer**: Tooling(工具侧)+ Core(运行期装载面)
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(F-6-1 `LATTICE_SIZE` · F-6-2 `TERRAIN_TABLE`)
**Requirement**: 兑现 S20-20 挂标 **#5「世界几何仍是合成」**的**地形半边**
(`production/sprints/sprint-20.md:970`)

**ADR Governing Implementation**: **ADR-022 §三**(导出)· **ADR-014 §三/§五**(烘焙 / `data-core`)
· **ADR-015 §一**(两层世界)· **ADR-015 §一之补**(`slopeLimit` / `stepOffset` 同源)
**ADR Decision Summary**: 世界几何的真源 = 关卡工具产出的 `world_geometry.json` /
`world_terrain.json` → 两阶段烘焙 → `world_*.cooked` → `data-core` 预载。
**逻辑层只有整数格**;视觉层(Terrain,float)由第三方工具 + 手工整理,**采样禁入 sim**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: **MEDIUM**(ADR-022 Engine Risk)
**Engine Notes**: ⚠️ **Terrain API 为 post-cutoff**(LLM 知识截止 2025-05,引擎 6.3 LTS 2025-12)。
本 story 的**第一条 AC 是 spike** —— 先证 API 行为,再写实现。
见 `docs/engine-reference/unity/VERSION.md` §Knowledge Gap Warning。

**Control Manifest Rules (Tooling 层 + Core 层)**:
- Required: 逻辑层**只有整数格**(ADR-015 §三);`Fix` 字段写**字符串**(ADR-014 §二)
- Forbidden: **任何以 seed 生成地形 / 多边形的代码**(ADR-022 §二:210)——「**都是回归**」
- Forbidden: **视觉层采样进 sim**(ADR-015 §一)
- Forbidden: **`WorldGeometry` 的空白构造入口**(`WorldLattice.cs:296` 已明文拒绝)
- Guardrail: `TERRAIN_TABLE` 空表 = **装载期硬失败**(F-6-2)

---

## §病灶 —— 「合成世界」的确切含义

`MinimalWorldFactory` 造的是 **16×1×16 全 walkable** 地形,POI / 资源格全 `-1`。
它的文件头 §「为什么是合成而不是装载」逐字写着三样全无:

```bash
ls assets/data/world_geometry.json assets/data/world_terrain.json 2>/dev/null || echo "两份都不存在"
ls unity/Assets/DataCooked/world_geometry.cooked.bytes \
   unity/Assets/DataCooked/world_terrain.cooked.bytes 2>/dev/null || echo "两份 cooked 都不存在"
```

**后果不是「世界不好看」**,是四条判据同时失去观察面:

| # | 失去的判据 | 实测依据 |
|---|---|---|
| ① | **`K_TERRAIN_MAX` 无值** | `TerrainCell.KSpeedIdx` 的 doc 自陈「当前**全仓零消费者**」⇒ `SPEED_MAX = SPEED_MODE_MAX × K_TERRAIN_MAX × K_CONTEXT_MAX` 的不等式**无参数可写** |
| ② | **`AC-1-33②`(`slopeLimit`/`stepOffset`)无值** | 债册 `TD-player-01` 明写:该项「GDD F-1-9 + ADR-015 §一之补归**关卡工具**,系统 1 只是执行者、**无权填值**」 |
| ③ | **可走性判定恒真** | 全 walkable ⇒ `IsEffectivelyWalkable` 对任何格都回 true ⇒ 「哪里走不了」这类判据**无观察面** |
| ④ | **C1 / C2 无对象** | 两者都是「两侧一致」类检查;逻辑侧无值 ⇒ 无可比 |

⚠️ **`MinimalWorldFactory` 的那条 `LogWarning` 不是缺陷** —— 它防的是
「世界变空 / 建造永远合法」被误诊为逻辑 bug。**本 story 完成时它应消失**,
因为世界不再合成。

---

## Acceptance Criteria

- [x] **AC-0(spike · BLOCKING)** —— **Terrain API 行为实测**:
      在 6.3 LTS 上实测 `TerrainData` / `Terrain` 的以下三项,**结果写入证据件**:
      ① `TerrainData.GetHeight` 的返回精度与坐标系(世界格 ↔ 归一化);
      ② `TerrainCollider` 与 `TerrainData` 的可走性关系(`slopeLimit` 在哪一侧生效);
      ③ `TerrainData` 能否在**编辑期**被关卡工具读取并转成整数格(不依赖运行期)。
      **判据**:三条各有**实测输出**(日志 / 探针件),**不是文档引用**。
      ⚠️ **本 AC 未过则本 story 不得继续** —— post-cutoff API 的假设是 MEDIUM 风险的来源。

- [x] **AC-1(BLOCKING)** —— **`world_geometry.json` 由工具导出**:
      形状 = 世界尺寸 + **每格可走性(整数布尔)** + `LATTICE_SIZE`。
      **判据**:导出的产物里**零浮点字面量**(JSON 里所有数字都能被整数解析器读入)。
- [x] **AC-2(BLOCKING)** —— **`world_terrain.json` 由工具导出**:
      形状 = `TERRAIN_TABLE`(`terrain_id → {K_speed, K_accel, K_decel}`,**`Fix` 写字符串**)
      + `terrain_id` 表 + `slopeLimit` / `stepOffset`(**整数量化后的值**)。
      **判据**:`K_speed` 等字段在 JSON 里是**字符串**(`"3/4"`)而非 JSON 数字 ——
      承 ADR-014 §二的反浮点纪律。
- [x] **AC-3(BLOCKING)** —— **两份产物经 ADR-014 烘焙成 `.cooked`**:
      `world_geometry.cooked` / `world_terrain.cooked` 落 `unity/Assets/DataCooked/`,
      **进 `data-core` 组**。
      **判据**:`data-core` 组条目数**增加 2**;catalog 重建后两条目可查。
- [x] **AC-4(BLOCKING)** —— **运行期装载面接通**:
      `MinimalWorldFactory` 被**装载路径替换**(或改为「有产物则装载,无产物则合成 + LogWarning」)。
      **判据**:真产物存在时,**`IsSyntheticWorld` 为 false**,且
      `WorldGeometry` 的尺寸 / 可走性来自产物(**不是** 16×1×16 全 true)。
- [x] **AC-5(BLOCKING)** —— **`TERRAIN_TABLE` 空表 = 装载期硬失败**:
      构造一个空 `TERRAIN_TABLE` 的产物 ⇒ 装载**抛**(不是静默降级为全 walkable)。
      **判据**:F-6-2 的「空表硬失败」有夹具。
- [x] **AC-6** —— **`K_TERRAIN_MAX` 有消费者**:
      `TerrainCell.KSpeedIdx` 的「全仓零消费者」状态解除 —— 至少一处读它做量程校验。
      **判据**:`grep -c KSpeedIdx` 的非定义侧 ≥ 1。
      ⚠️ **本 AC 只要求「有消费者」,不要求「消费得对」** —— 后者归系统 1 的实现轮。

---

## §Implementation Notes

### 顺序不能颠倒:AC-0 是门,不是第一步

`Terrain` API 是 **post-cutoff**(6.3 LTS 于 2025-12 发布,LLM 知识截止 2025-05)。
本仓已多次记录「按文档假设写代码 ⇒ 实测不符」的代价。
⇒ **先跑 spike,拿到实测输出,再写实现**。若 spike 推翻 ADR-022 的假设
(例如 `TerrainData` 在编辑期不可读),**须回 ADR-022 追加裁决**,不得自行绕路。

### 为什么 `slopeLimit` / `stepOffset` 要「整数量化」

ADR-022 §四 **C2** 的判据是 `|engineParam − Dequantize(bakedInt)| ≤ 1 量子`。
⇒ 逻辑层存的是**整数**,引擎侧是 **float**,两者通过**一个固定的量化函数**关联。
本 story 只负责**导出整数侧**;两侧一致的**检查**归 **S6**。
⚠️ 但**量化函数本身必须在本 story 定义** —— 否则 S6 无对象可比。
它归 `Sim.Contracts`(整数域)还是工具侧(编辑期)?**本 story 须明确并登记**。

### 装载路径替换的两种形状

| 形状 | 行为 | 代价 |
|---|---|---|
| **甲** | `MinimalWorldFactory` **删除**,改为 `WorldLoadService` 装载 | 干净;但既有夹具全依赖工厂 |
| **乙** | 工厂保留,**有产物走装载、无产物走合成 + LogWarning** | 兼容;但「两条路」本身是第二真源风险 |

⚠️ **本 story 取乙**(兼容既有夹具),但**须在 doc 里写明「甲是终态」** ——
否则「合成世界」会以「兼容路径」的名义永久存活。`LogWarning` 是判据:
**真产物在库时它不该出现**。

---

## Out of Scope

- **生态区多边形 / C4 / C5** —— 归 **S3**
- **POI / 资源点** —— 归 **S4**(本 story 的 `world_geometry` 里 POI / 资源格**仍可全 `-1`**)
- **导航格 / C6 / C3** —— 归 **S5**
- **C1 / C2 / C2' 的检查实现** —— 归 **S6**(本 story 只导出整数侧,不做比对)
- **视觉层 Terrain 资产本体**(美术雕的地形) —— 美术 / 关卡设计;本 story 只产出逻辑层
- **`LATTICE_SIZE` / `K_speed` 的具体数值** —— **用户数值轮**

---

## QA Test Cases

- **AC-0**:spike。
  - Given: 6.3 LTS 编辑器。
  - When: 跑探针脚本读 `TerrainData` 三项。
  - Then: 三条实测输出落证据件。
  - ⚠️ **本条不可用夹具自动化** —— 它是勘察,产物是**实测日志**。

- **AC-1 / AC-2**:导出形状。
  - Given: 一个手造关卡的几何 + 地形表。
  - When: 导出两份 JSON。
  - Then: ① 零浮点字面量(`regex` 扫 `\d+\.\d+` 零命中);
    ② `K_speed` 等字段是字符串(`"3/4"`)。
  - Negative fixture: 若把 `Fix` 写成 JSON 数字 ⇒ 红。

- **AC-5**:空表硬失败。
  - Given: 一个 `TERRAIN_TABLE` 为空的产物。
  - When: 装载。
  - Then: **抛**(`InvalidDataException` 或 ADR-014 口径的等价异常)。
  - Negative fixture: 若实现静默降级为全 walkable ⇒ 不抛 ⇒ 红。
  - ⚠️ **判据须断「抛之前下游零调用」** —— 只断异常类型会被下层同类型异常冒充
    (承 `feedback_fail_fast_needs_sideeffect_assert`)。

- **AC-4**:装载面替换。
  - Given: 真产物在库。
  - When: 装配。
  - Then: `IsSyntheticWorld == false`;`WorldGeometry` 尺寸 = 产物声明的尺寸。
  - Negative fixture: 若仍走合成 ⇒ `IsSyntheticWorld == true` 且尺寸恒 16×1×16 ⇒ 红。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `unity/Assets/Tests/EditMode/LevelTool/world_geometry_load_test.cs` — must exist and pass
- **spike 证据**: `production/qa/evidence/spike-terrain-api-6.3-<date>.md`(AC-0 的实测输出)
- ⚠️ **既有 `world_lattice_test.cs` / `world_test.cs` 须保持全绿**

---

## Dependencies

- Depends on: **S1**(数据集拆分骨架 —— 没有它,`world_geometry` / `world_terrain`
  无处挂载)
- Unlocks: **S3** · **S4** · **S5** · **S6**(全部依赖「几何 + 地形」存在)

---

## §诚实边界(本 story 证明不了的事)

1. **视觉层是否真的与逻辑层对齐** —— 那是 **C1**(告警级,S6)与 `world-ecozones/story-005`
   的人工走查;本 story 只证明**逻辑层有真数据**。
2. **`K_TERRAIN_MAX` 的数值是否正确** —— 本 story 只让它**有消费者**;
   数值归**用户数值轮**。
3. **Terrain 资产本体是否存在** —— 本 story 的产物是**整数逻辑层**;
   美术地形由第三方工具 + 手工整理(ADR-022 §二),不在本 story 边界内。
4. **多 chunk 的世界** —— 本 story 的几何是**单块**;chunk 切片归 **S5**(且只切导航格)。

---

## Completion Notes

**Completed**: 2026-10-29
**Criteria**: **7/7 passing**(AC-0 spike + AC-1 … AC-6)
**Test Evidence**: Integration —— `unity/Assets/Tests/EditMode/LevelTool/world_geometry_load_test.cs`
(14 条 `[Test]`,全绿)+ spike 原件 `production/qa/evidence/spike-terrain-api-6.3-2026-10-29.md`
+ 实现原件 `production/qa/evidence/impl-level-tool-story-002-2026-10-29.md`
**Falsifiability Gate**: **PASS** —— 13 条变异(一属性一条),预判 15 红 / 实得 **15 红**,
逐条命中预判用例,零多余红零漏红;六次还原 SHA-256 逐字节回到原值
**Deviations**:
- ⚠️ **§Implementation Notes 的「乙案」被收窄为「甲案」** —— 合成支**整体删除**,
  `MinimalWorldFactory` 成为纯装载器。三条理由见实现原件 §六①
  (与 AC-5 语义冲突 / 「产物缺失」与「产物损坏」类型上不可分 / 合成支零可达)。
  **请用户裁定**:接受收窄,还是要求补回乙的降级支。
- **量化约定住 `Sim.Contracts` 且只定整数侧**(不提供 float 反量化函数)—— 理由见该类型文件头;
  C2 的比对归 S6,届时直接引用 `LatticeParamQuantization.IsWithinOneQuantum`,不另抄公式。
- **P0 医馆的几何 / 地形数值全是占位**:外墙环 1 格不可走(60/256)、单行地形表
  (`K_TERRAIN_MAX` 恒 1)、`LATTICE_SIZE` 1000 mm、`slopeLimit` 4500 / `stepOffset` 300
  (取 AC-0 实测的引擎默认值)。**数值归用户数值轮**。

**Code Review**: 未跑(`/code-review` 归用户触发,与 story-001 同)。

### 本轮顺带抓到的三处真回归(全是我自己引入的,已在原件 §4.2 逐条登记)

1. 新增两份 `*.cooked.bytes` 却未登记产物漂移门 ⇒ `noGhostArtifact` + `pinnedDeclared` 双红
   (**本门第六次抓到同类漏登记**)。补齐后 `pinnedDeclared` **26 → 28**,
   顺带把 ADR-022 §②「无第二作者」钉到了几何 / 地形上。
2. `MinimalWorldFactory` 一条异常报文里写了作者态目录路径 ⇒ `RuntimeSourceGuard`(AC-21a-48)红。
   改文案后绿;已在该文件头写明「注释安全、**报文**会红」。
3. `WorldLattice.cs` 里 `TerrainCell.KSpeedIdx` 的注释仍声称「全仓零消费者」—— **现在是假的**。
   已就地加注三道消费者(烘焙期 binder / 装载期 codec / 装配期 `ValidateTerrainSpeedIndices`)。
