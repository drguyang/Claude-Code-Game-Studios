# Story 004: `world_poi` / `world_resources` 导出 + 运行期定义装载

> **Epic**: 关卡工具(54 · Tooling 层)
> **Status**: Complete
> **Layer**: Tooling(工具侧)+ Core(运行期装载面)
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(POI 定义 = 派生态 · 资源点分布)· `design/gdd/foraging.md`(资源点)
**Requirement**: 兑现 S20-20 挂标 **#5「世界几何仍是合成」**的 **POI / 资源半边**
(`production/sprints/sprint-20.md:970`)

**ADR Governing Implementation**: **ADR-022 §三**(导出)· **ADR-021**(POI 定义 = 派生态,
状态归 6)· **ADR-015 §一**(两层世界)
**ADR Decision Summary**: `world_poi.json` 承载 POI **定义**
(`{poi_id, cell, type, guard_ref}`,派生态,不进流);`world_resources.json` 承载资源点分布
(整数格 + 生态区倾向)。**状态归 6,工具不碰**(ADR-022 §六 锁死)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: **LOW**
**Engine Notes**: 纯数据边界与装载装配,**零 post-cutoff API**。
⚠️ 这是本 epic 里 Engine Risk 最低的一件 —— 它的难点在**边界纪律**,不在引擎。

**Control Manifest Rules (Tooling + Core 层)**:
- Required: POI **定义** = 派生态;状态 = 模拟态(ADR-021)
- Required: `spawn_anchor` 读**定义**不读**状态**(ADR-021 裁定⑤)
- Forbidden: **工具碰 POI 状态**(ADR-022 §六:277 —— 「✖ 不碰」)
- Forbidden: 逻辑层出现浮点(ADR-015 §三)
- Guardrail: 资源点分布须能被 17 采集消费

---

## §病灶 —— 「POI / 资源格全 -1」的确切含义

`MinimalWorldFactory.cs:106-113` 逐字:

```csharp
// POI / 资源格全 -1(= 无)。长度须与 terrain 齐,否则 WorldGeometry 装载期抛。
var poiGrid = new int[total];
var resourceGrid = new int[total];
for (int i = 0; i < total; i++)
{
    poiGrid[i] = -1;
    resourceGrid[i] = -1;
}
```

⇒ **每一个格**的 POI id 与资源 id 都是 `-1`。这不是「POI 很少」,是**一个都没有**。

**连带后果(实测)**:

| # | 后果 | 依据 |
|---|---|---|
| ① | `PoiStateMachine` 的 `_poiIdSet` **零成员** | 它从 `IEnumerable<int>` 构造;生产侧无来源 |
| ② | `PoiStateChanged` 事件**永不发出** | 无 POI 可转移 |
| ③ | 52 随机事件的 `spawn_anchor` **无定义可读** | ADR-021 裁定⑤:读定义;定义集为空 |
| ④ | 17 采集的资源点查询**恒「无」** | 资源格全 `-1` |
| ⑤ | `world-ecozones/story-005` 的「走近 POI 被发现」走查**无对象** | 零 POI |

---

## Acceptance Criteria

- [x] **AC-1(BLOCKING)** —— **`world_poi.json` 由工具导出**:
      形状 = `{ poi_id, cell: WorldPos, type, guard_ref }`。
      **判据**:① `poi_id` 唯一且 **≥ 0**;② `cell` 全整数;③ **同一格不声明两个 POI**
      (否则「取哪一条」成为隐式规则 —— 与 `world_buildslots` 的重复格检查同型)。
- [x] **AC-2(BLOCKING)** —— **`world_resources.json` 由工具导出**:
      形状 = 资源点(整数格 + 资源 id + 生态区倾向)。
      **判据**:① 格全整数且在声明范围内;② 资源 id ≥ 0;
      ③ **格不与 POI 格冲突**(同一格既是 POI 又是资源点是数据错配)。
- [x] **AC-3(BLOCKING)** —— **两份产物烘焙 + 进组**:`world_poi.cooked` /
      `world_resources.cooked` 落 `unity/Assets/DataCooked/`,进 `data-core` 组。
      **判据**:组条目数 **+2**。
- [x] **AC-4(BLOCKING)** —— **POI 定义装载面接通**:
      `WorldGeometry` 的 `PoiGrid` 来自产物(**不是**全 `-1`);
      `PoiStateMachine` 的 `_poiIdSet` 来自产物。
      **判据**:真产物在库时,① 某 POI 格的 `PoiGrid[i]` **== 该 poi_id**(不是 -1);
      ② `PoiStateMachine` 能对**已登记**的 poi_id 发 `PoiStateChanged`。
- [x] **AC-5(BLOCKING)** —— **资源格装载面接通**:
      `ResourceGrid` 来自产物。
      **判据**:某资源格的 `ResourceGrid[i]` **== 该资源 id**;非资源格仍 `-1`。
      **两个方向都要** —— 只测有资源的格,「全填同一个 id」会逃逸。
- [x] **AC-6** —— **工具不碰状态**:`Editor.Tools.Level` 程序集内
      **零引用** `PoiState` / `PoiStateMachine`。
      **判据**:`grep -c "PoiState" unity/Assets/Editor.Tools.Level/*.cs` == **0**。
      ⚠️ 这是 ADR-022 §六 锁死边界的**机械判据** —— 不是「我们小心不碰」,是**扫得出来**。

---

## §Implementation Notes

### `guard_ref` 的语义本 story 须先定

ADR-022 §三 只写 `guard_ref` 四个字,没定义它是什么。三种可能:

| 可能 | 含义 | 后果 |
|---|---|---|
| **甲** | 守卫敌人 id(引用 27 的敌人定义) | 需 27 的定义集存在 |
| **乙** | 守卫**强度档位**(整数枚举) | 自足;但丢失具体敌人 |
| **丙** | 字符串引用(资产名) | ⚠️ **违反整数域纪律** |

⚠️ **本 story 取乙**(整数档位),并在 doc 里写明「具体敌人由 52 抽池时决定」——
否则会引入一个**未定义的跨系统引用**(27 的敌人定义今日不存在)。
若用户裁定取甲,须先有 27 的定义集 —— 那不在本 story 边界内。

### 为什么 AC-6 用 grep 而不是「纪律」

ADR-022 §六 的边界表逐字写「POI 状态:✖ 不碰」。但**纪律靠不住** ——
本仓已记录多次「自述有门其实没门」。
⇒ 用一个**扫得出来**的判据:工具程序集里**零** `PoiState` 符号。
⚠️ 该判据是**文本级**的,不是语义级 —— 若将来工具真的需要读状态(例如导出「初始已清」),
它**会红**,那时须回 ADR-022 追加裁决,不是改判据。

### 资源点与生态区倾向的耦合

`world_resources.json` 的「生态区倾向」依赖 S3 的 `world_ecozones`。
⇒ **本 story 须在 S3 之后**(或至少 S3 的 `ecozone_id` 闭集已定)。
若倾向字段只是**引用** `ecozone_id`(不校验存在性),则两者可并行 ——
**本 story 取「只引用不校验」**,存在性检查归 S3 的 C4 邻域(或作为本 story 的一条附加门)。

---

## Out of Scope

- **POI 状态机本体 / `PoiState` 转移** —— 归 **6**(ADR-021);工具只出**定义**
- **52 的 `spawn_anchor` 抽池逻辑** —— 归 52;本 story 只让「定义可读」
- **17 采集的资源消费逻辑** —— 归 17;本 story 只让「资源格有真值」
- **C1 / C2 / C2' / C3 / C4 / C5 / C6** —— 归 S3 / S5 / S6
- **`guard_ref` 指向的具体敌人** —— 若取甲案须 27 的定义集,不在本 story
- **导航格** —— 归 S5

---

## QA Test Cases

- **AC-1**:POI 导出。
  - Given: 若干 POI 定义。
  - When: 导出。
  - Then: `poi_id` 唯一且 ≥ 0;同格不重复。
  - Negative fixture: 两 POI 同格 ⇒ 抛(报文点名重复格)。

- **AC-4**:POI 装载两方向。
  - Given: 真产物,含至少一个 POI 格。
  - When: 读 `PoiGrid`。
  - Then: POI 格 == poi_id;**非 POI 格 == -1**。
  - Negative fixture: 若实现「全填同一个 id」⇒ 非 POI 格那条红。

- **AC-5**:资源装载两方向(同上形状)。

- **AC-6**:工具不碰状态。
  - Given: `Editor.Tools.Level` 全量源文本。
  - When: grep `PoiState`。
  - Then: 零命中(排除注释/字符串 —— 须用 AST 或至少剥注释)。
  - ⚠️ **剥注释这一步是判据的一部分** —— 否则注释里提到 `PoiState` 就误红。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `unity/Assets/Tests/EditMode/LevelTool/poi_and_resource_load_test.cs` — must exist and pass
- ⚠️ **既有 `poi_state_machine_test.cs`(19 例)须保持全绿** —— 本 story 只加装载面
- ⚠️ **既有 `world_lattice_test.cs` 须保持全绿**

### AC → 载体映射(一 AC 可多载体)

| AC | 载体 | 文件 |
|---|---|---|
| AC-1 | 5 条 `[Test]`(导出形状 / 负 id / 重复 id / 同格 / 越界) | `Tests/EditMode/LevelTool/poi_and_resource_load_test.cs` |
| AC-2 | 4 条(导出形状 / 负 id / 越界 / 与 POI 格冲突) | 同上 |
| AC-3 | 2 条 + 漂移门参数化用例 ×2 + **真 batch 实跑** | 同上 · `Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs` · 证据件 §五.3 |
| AC-4 | 3 条(`PoiGrid` 两方向 / 定义集装载 / 状态机可发事件) | 同上 |
| AC-5 | 2 条(`ResourceGrid` 两方向 / 读侧同格门) | 同上 |
| AC-6 | 1 条(剥注释后扫全部 `.cs`,字符串也计入) | 同上 |

### 反证记录(S12-4 强制)`反证:停用 <守卫> ⇒ <测试名> 红`

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| 定义域收窄 ×8 | id 值域 / 唯一性 / 同格 / 越界 / 地址登记 | 停 `PoiId < 0` ⇒ `test_ac1_negativePoiId_throws` · 停 id 唯一 ⇒ `…_duplicatePoiId_throws` · 停同格 ⇒ `…_twoPoisOnSameCell_throws` · 停 POI 越界 ⇒ `…_poiCellOutOfLevelRange_throws` · 停 `ResourceId < 0` ⇒ `…_negativeResourceId_throws` · 停资源越界 ⇒ `…_cellOutOfLevelRange_throws` · 停 POI 格交叉 ⇒ `…_resourceOnPoiCell_throws` · 删地址表条目 ⇒ `…_bothAddressesInPreloadList_…` |
| 一次性/幂等 | 同源两次烘焙逐字节相同 | `world_poi` 行掺 `Guid` ⇒ `test_ac3_bakeTwice_bothProductsBitIdentical` 红 |
| 阈值边界 | `PoiGrid` / `ResourceGrid` 两个方向 | 填 `-1` ⇒ `test_ac4_poiGrid_…` / `test_ac5_resourceGrid_…` 各红 |
| 共存/互斥 | 「已登记」与「空集」互斥;「同格两行」非法 | `LoadPoiIds` 恒空 ⇒ `…_poiStateMachine_…` + `…_loadPoiIds_matchesProductDefinitions` 2 红;停读侧同格门 ⇒ `…_codec_rejectsDuplicateCell` 红 |

**全轮记录**:15 条变异 / 预判 16 红 / **实得 17 红** / **零假绿**;一条超判(M4)已独立论证。
原件:`production/qa/evidence/impl-level-tool-story-004-2026-10-29.md` §三。

---

## Dependencies

- Depends on: **S2**(几何 + 烘焙骨架)· **S3**(生态区 id 闭集,用于资源倾向字段)
- Unlocks: `world-ecozones/story-005` 的走查(有 POI 可走近)· 52 的 `spawn_anchor`(有定义可读)

---

## §诚实边界(本 story 证明不了的事)

1. **POI 状态转移是否真的发生** —— 本 story 只证明「定义可读、状态机可登记」;
   实际转移需要玩家走近,那是 `world-ecozones/story-005` 的走查。
2. **资源点是否真的可采** —— 本 story 只证明「资源格有真值」;采集链归 17 的实现轮。
3. **`guard_ref` 的档位是否合理** —— 数值归**用户数值轮**。
4. **AC-6 是文本级判据** —— 它证明「工具源码里没有 `PoiState` 符号」,
   **不证明**「工具在语义上真的不需要状态」。若将来需要,该 AC 会红,那时须回 ADR-022。

---

## Completion Notes

**Completed**: 2026-10-29
**Criteria**: **6/6 通过**
**Test Evidence**: Integration —— `unity/Assets/Tests/EditMode/LevelTool/poi_and_resource_load_test.cs`
(17 条 `[Test]`)+ 漂移门参数化用例 ×2。原件
`production/qa/evidence/impl-level-tool-story-004-2026-10-29.md`。
**Deviations**: **无 BLOCKING**。ADVISORY 三条,均如实登记:
1. **`PoiStateMachine` 未接进 `CompositionRoot.Assemble`** —— 构造需要 `IEventAuthority`,
   而全仓零生产实装(已登记为 P1b 缺口,且有夹具断言「零生产实装」)。本轮只接定义侧
   (`CompositionRoot.LoadPoiIds`),机器在夹具里由测试替身构造。**「生产装配里存在一台状态机」未证**。
2. **AC-3 的「组条目数 +2」无专打变异** —— 判据在 Addressables 资产上,EditMode 无 locator;
   实跑证据 = `BuildAddressablesContent` 输出「31 个条目已入组」+ 盘上 31 份产物(证据件 §五.3)。
3. **`ecozone_id` 的存在性未校验** —— 刻意如此(跨数据集门归 S6 的 C 系列)。
**Regression**:
EditMode `5789 / 5754 / 0 / 0 / 35`(@ `2026-10-29 02:58:16 CST` · S4 收口终跑 · `b05d57d2`+工作树);
基线 `5770 / 5735` ⇒ 等式 `5789 − 5770 = 19` = 17 条新 `[Test]` + 2 条漂移门参数化用例,**消失 0**。
PlayMode `238 / 234 / 0 / 2 / 2`,与基线**逐项相同**(零回归)。
**Code Review**: **Pending**(`/code-review` 未跑,归用户触发 —— 与 story-001/002/003 同)。
**Falsifiability Gate**: **PASS** —— 15 条变异全部转红,零假绿。
