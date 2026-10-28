# Story 003: `world_ecozones` 导出 + C4 多边形合法性 + C5 边界单一源

> **Epic**: 关卡工具(54 · Tooling 层)
> **Status**: Complete
> **Layer**: Tooling
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(F-6-3 `EcozoneOf` 整数几何 · `AC-6-19` 多边形退化 · `O-6-11` 边界单一源)
**Requirement**: 兑现 S20-20 挂标 **#3「C1–C6 七项未实现」**中的 **C4** 与 **C5**

**ADR Governing Implementation**: **ADR-022 §三**(导出)· **§四 C4 / C5**(硬失败)
· **ADR-015 §一**(两层世界)· **ADR-021**(POI 定义 = 派生态)
**ADR Decision Summary**: 生态区多边形是**整数顶点**的开集;C4 检查五项退化
(重叠 / 顶点<3 / 自交 / 零面积 / 重合顶点)= **硬失败**;C5 要求**视觉层生态区遮罩由
整数多边形生成**(非手画第二份)= **硬失败**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: **MEDIUM**(C5 涉视觉层遮罩生成)
**Engine Notes**: C4 是**纯整数几何**(零引擎 API,LOW);C5 须读 / 生成视觉层遮罩
(Texture / Terrain 层,post-cutoff,MEDIUM)。**两条的 Engine Risk 不同,不得合并评级**。

**Control Manifest Rules (Tooling 层)**:
- Required: 逻辑层**只有整数格**(ADR-015 §三);射线法全整数(F-6-3)
- Forbidden: **手画第二份边界**(C5 的判据正是「单一源」)
- Forbidden: **`Debug.Assert` 代替 `throw`**(C4 / C5 都是硬失败)
- Guardrail: 生态区是**开集**(边界点不属任何区),边界 min(id) 仲裁

---

## §病灶 —— C4 / C5 的「未实现」是什么意思

`LevelConsistencyCheck.UnimplementedChecks` 逐字登记(它是**返回值里的数据**,不是注释):

```
"C4 多边形合法性(需 world_ecozones.json —— 本批不产出)",
"C5 边界单一源(需视觉层遮罩 —— 本批不产出)",
```

⇒ 不是「实现得不好」,是**零实现**。而且**数据也不存在**:

```bash
ls assets/data/world_ecozones.json unity/Assets/DataCooked/world_ecozones.cooked.bytes 2>/dev/null \
  || echo "两份都不存在"
```

**连带后果(实测)**:

| # | 后果 | 依据 |
|---|---|---|
| ① | `EcozoneOf` 对任何格恒回 `None` | `CompositionRoot.cs:1163-1165` 自陈「生态区注册表在本装配内**尚未有装载面**」 |
| ② | `AC-23-08` 的生态区半边无对象 | 占用格 ⊆ 生态区?—— 生态区集合为空 |
| ③ | 5 时间天气的 `EcozoneOf(cell)` 输入恒 `None` | `TimeBase.cs:10` 逐字:`天气 = (WorldSeed, block(tick), EcozoneOf(cell))` |
| ④ | 13 病人 AI 的 `KnowsClinic` 恒假 | `CompositionRoot.cs:1172` 自陈「`EcozoneOf` 恒 `None` ⇒ `KnowsClinic` **恒假**,与 story-021 之前的硬编码 `false` **行为等价**」 |

---

## Acceptance Criteria

- [x] **AC-1(BLOCKING)** —— **`world_ecozones.json` 由工具导出**:
      形状 = `{ ecozone_id, name, vertices: WorldPos[] }`(整数多边形,**开集语义**)。
      **判据**:顶点坐标全部整数(零浮点);`ecozone_id` 唯一。
- [x] **AC-2(BLOCKING)** —— **C4 五项退化全部实现为硬失败**,各有一条负向夹具:
      ① **重叠**(两多边形内部相交)② **顶点 < 3** ③ **自交**(边相交)
      ④ **零面积** ⑤ **重合顶点**(同一多边形内重复顶点)。
      **判据**:五项各有一条「注入该退化 ⇒ 检查抛」的夹具,**`exactRed` 逐条**。
      ⚠️ **五项不得合并成一条夹具** —— 一个夹具验五项,停用任一分支时其余四项仍红,
      判别面不可分(承 `feedback_falsify_one_guard_per_property`)。
- [x] **AC-3(BLOCKING)** —— **C5 边界单一源实现**:
      视觉层生态区遮罩**由整数多边形生成**,并检测「存在手画第二份」的情形。
      **判据**:① 遮罩生成路径可复算(同输入两次生成**逐字节相同**);
      ② 注入一份「与多边形不一致的遮罩」⇒ 检查**抛**。
- [x] **AC-4(BLOCKING)** —— **烘焙 + 进组**:`world_ecozones.cooked` 落
      `unity/Assets/DataCooked/`,进 `data-core` 组。
      **判据**:组条目数 **+1**;catalog 重建后可查。
- [x] **AC-5(BLOCKING)** —— **运行期装载面接通**:
      `CompositionRoot.cs:1171` 的 `new EcozoneRegistry()`(空表)被**装载路径**替换。
      **判据**:真产物在库时,`EcozoneOf` 对一个**区内格**回**该区 id**(不是 `None`);
      对一个**区外格**回 `None`。**两个方向都要** —— 只测区内会让「恒回 0」逃逸。
- [x] **AC-6** —— **`UnimplementedChecks` 长度 7 → 5**(C4 / C5 两条移出)。
      **判据**:数组长度 == 5,且剩余五条仍是 C1 / C2 / C2' / C3 / C6。

---

## §Implementation Notes

### C4 的「重叠」判据须先定义清楚

ADR-022 §四 只写「重叠」两个字。三种可能的口径:

| 口径 | 判据 | 后果 |
|---|---|---|
| **甲** | 任意两多边形的**内部**相交 ⇒ 失败 | 严格;但相邻区共享边**不**失败(开集语义) |
| **乙** | 任意两多边形**闭包**相交 ⇒ 失败 | 相邻区共享边**会**失败 ⇒ 与开集语义冲突 |
| **丙** | 只检「重叠面积 > 0」 | 允许边界接触;但需面积计算(整数域可做) |

⚠️ **本 story 取甲**(与 F-6-3 的开集语义一致),**但须在实现里写明口径** ——
否则将来有人按乙实现,C4 会开始误报相邻区。

### C5 的「手画第二份」怎么检测

这是**静默失败类**判据(`world-and-ecozones.md:284-290` 已点名)。两条路:

| 路 | 判据 | 代价 |
|---|---|---|
| **甲** | 遮罩**只能由生成器产出**;生成器是唯一写入面 | 需改视觉层资产管线 |
| **乙** | 遮罩**生成后**与多边形**逐像素比对** | 可检已存在的遮罩;但比对成本高 |

⚠️ **本 story 取甲**(源头唯一),**乙作为兜底**在 CI 里跑一次(若视觉层已有遮罩资产)。
若两者都不可行,须**回 ADR-022 追加裁决** —— 不得把 C5 降级为告警。

### 为什么 AC-5 要求「两个方向都测」

`EcozoneOf` 的实现有三条分支(严格内部命中 / 边界 min(id) 仲裁 / `NONE` 哨兵)。
**只测区内格**时,一个「恒回 0」的实现会通过。必须同时测区外格回 `None`。

---

## Out of Scope

- **C1 / C2 / C2' / C3 / C6** —— 归 S6 / S5
- **POI 定义**(`world_poi`)—— 归 **S4**
- **`EcozoneOf` 的算法本体** —— **已实现**(`EcozoneRegistry.cs:286`),本 story 只接装载面
- **生态区的名称 / 叙事内容** —— 归 `world-ecozones` 的 GDD / 叙事轮
- **视觉层遮罩的美术质量** —— 本 story 只保证「由整数多边形生成」

---

## QA Test Cases

- **AC-2**:C4 五项,**逐项独立**。
  - Given: 一份合法多边形集。
  - When: 分别注入五种退化之一。
  - Then: 各**恰**触发对应失败项(报文点名是哪一项)。
  - Negative fixture: 停用 C4 的任一分支 ⇒ 该项夹具红,**其余四项不红**(判别面可分)。
  - Edge cases: **相邻区共享边不失败**(开集语义);**顶点恰为 3 合法**(< 3 才失败)。

- **AC-3**:C5。
  - Given: 整数多边形。
  - When: 生成遮罩两次。
  - Then: 逐字节相同。
  - Negative fixture: 注入一份与多边形不一致的遮罩 ⇒ 抛。

- **AC-5**:装载面。
  - Given: 真产物。
  - When: 查询区内格 / 区外格。
  - Then: 前者回该区 id,后者回 `None`。
  - Negative fixture: 若仍用空表 ⇒ 两者都回 `None` ⇒ 区内那条红。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/LevelTool/ecozone_polygon_check_test.cs` — must exist and pass
  (C4 五项各一条 + C5 两条 + 装载面两方向)
- ⚠️ **既有 `ecozone_registry_test.cs` 须保持全绿** —— 本 story 只加装载面,不改算法

### AC → 载体映射(一 AC 可多载体)

| AC | 载体 | 文件 |
|---|---|---|
| AC-1 | 3 条 `[Test]` | `Tests/EditMode/LevelTool/ecozone_polygon_check_test.cs` |
| AC-2 | 6 条(五项各一 + 贴边边界半条) | 同上 |
| AC-3 | 3 条(确定性 1 + 比对器 2) | 同上 |
| AC-4 | 1 条 + 漂移门参数化用例 `drift[world_ecozones.cooked.bytes]` + **真 batch 实跑** | 同上 · `Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs` · 证据件 §五.3 |
| AC-5 | 2 条(区内/区外两方向 + 装配根接线扫描) | `ecozone_polygon_check_test.cs` |
| AC-6 | 2 条 | `ecozone_polygon_check_test.cs`(更正:`level_source_generalization_test.cs` 的 `test_ac4_unimplementedChecks_…`)+ `level_tool_consistency_test.cs` 的 `test_levelTool_realSeed_…` |

### 反证记录(S12-4 强制)`反证:停用 <守卫> ⇒ <测试名> 红`

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| 定义域收窄 ×5 | C4 五项退化各被排除 | 停用 ①重叠 ⇒ `test_ac2_overlap_interiorsIntersect_throws` 红 · ②顶点<3 ⇒ `test_ac2_fewerThanThreeVertices_throws` 红 · ③自交 ⇒ `test_ac2_selfIntersection_throws` 红 · ④零面积 ⇒ `test_ac2_zeroArea_throws` 红 · ⑤重合顶点 ⇒ `test_ac2_coincidentAdjacentVertices_throws` 红(**各恰 1 条,五项判别面可分**) |
| 共存/互斥 | 「贴边合法」↔「内部重叠非法」 | 把边界点并入内部 ⇒ `test_ac2_sharedEdge_neighbouringZones_doNotFail` 红(并连带 13 条导出链夹具 —— C4 是每次导出的前置门) |
| 一次性/幂等 | 同输入两次生成逐字节相同 | 遮罩头掺 `Guid.NewGuid()` ⇒ `test_ac3_mask_generatedTwice_bitIdentical` 红 |
| 阈值边界 | `UnimplementedChecks` 恰 5 条 | 把 C4 / C5 加回 ⇒ 2 条红(`test_ac4_unimplementedChecks_…` + `test_levelTool_realSeed_…`) |
| 定义域收窄(接线) | 地址表 / 装载面 / 装配根 | 删地址表条目 ⇒ `test_ac4_addressInPreloadList_andProductExists` 红;装载面退回空表 ⇒ `test_ac5_loadEcozoneRegistry_…` 红;装配根退回 `new EcozoneRegistry()` ⇒ `test_ac5_compositionRoot_wires…` 红 |

**全轮记录**:15 条变异 / 预判 17 红 / **实得 31 红** / **零假绿**;两条超判已逐条独立论证。
原件:`production/qa/evidence/impl-level-tool-story-003-2026-10-29.md` §三。

---

## Dependencies

- Depends on: **S2**(数据集拆分骨架 + 烘焙骨架)
- Unlocks: **S6**(C2' / C5 相邻;C1 的生态区半边)

---

## §诚实边界(本 story 证明不了的事)

1. **生态区数量 / 形状是否合理** —— 本 story 只证明「有多边形且合法」;
   内容是关卡设计的事。
2. **C5 若视觉层遮罩资产尚不存在** —— 甲路可落地但**乙路无法验证**;
   须如实登记「乙路未跑」。
3. **`EcozoneOf` 的跨机器逐位一致性** —— 归 ADR-012 的黄金夹具矩阵,不在本 story。
4. **生态区与天气 / 病人 AI 的行为联动** —— 本 story 只让 `EcozoneOf` **有真值**;
   消费侧的行为是否正确归 5 / 13 的实现轮。

---

## Completion Notes

**Completed**: 2026-10-29
**Criteria**: **6/6 通过**(AC-1 … AC-6 全部有载体且实跑绿)
**Test Evidence**: Logic —— `unity/Assets/Tests/EditMode/LevelTool/ecozone_polygon_check_test.cs`
(15 条 `[Test]`)+ 漂移门参数化用例 `drift[world_ecozones.cooked.bytes]`。
原件 `production/qa/evidence/impl-level-tool-story-003-2026-10-29.md`。
**Deviations**: **无 BLOCKING**。ADVISORY 两条,均已如实登记、不改判据面:
1. **AC-4 的「组条目数 +1」无专打变异** —— 判据落在 Addressables 资产上,EditMode 无 locator。
   实跑证据 = `BuildAddressablesContent` 输出「29 个条目已入组」+ 盘上 29 份产物(证据件 §五.3)。
2. **C5 的「乙路」(视觉层遮罩逐像素比对)未跑** —— 视觉层资产尚不存在(美术轮)。
   甲路(生成器是唯一写入面)已落地。
**Regression**:
EditMode `5770 / 5735 / 0 / 0 / 35`(@ `2026-10-29 02:22:27 CST` · S3 收口终跑 · `3f446e70`+工作树);
基线 `5754 / 5719` ⇒ 等式 `5770 − 5754 = 16` = 15 条新 `[Test]` + 1 条漂移门参数化用例,**消失 0**。
PlayMode `238 / 234 / 0 / 2 / 2`,与基线**逐项相同**(零回归;退出码 2 系既有 `Inconclusive` 所致)。
⚠️ **首跑全量红 20 条**(`MultiTableProvider 无 WorldEcozonesDataSet`)—— 根因与修法见证据件 §八④。
**Code Review**: **Pending**(`/code-review` 未跑,归用户触发 —— 与 story-001 / story-002 同)。
**Falsifiability Gate**: **PASS** —— 15 条变异全部转红,零假绿。
