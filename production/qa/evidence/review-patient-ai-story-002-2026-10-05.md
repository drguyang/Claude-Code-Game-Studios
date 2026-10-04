# 评审原件 —— patient-ai(13)story-002 空间行为

> **评审对象**: `unity/Assets/Gameplay.Presentation/PatientAI/{SpatialPerception,ClinicKnowledge,LogicalStepper,PatientSpatialDirector}.cs`
> + `unity/Assets/Tests/EditMode/PatientAI/spatial_behavior_test.cs`
> **权威件**: `design/gdd/patient-ai.md` §Formulas F-13.2/13.3/13.4/13.7 · §States `Seeking{EnRoute/AtClinic}` ·
> §Tuning 二 · ADR-016 §三/§六/§九 · ADR-027 · TR-patient-006/009/013/017
> **日期**: 2026-10-05 · **轮次**: 单轮(承「评审只做一轮」)
> **双代理**: 结构侧(`unity-specialist`)+ QA 侧(`qa-lead`),并行独立出报告

---

## 一、原判定(评审后、修复前)

**结构侧:CHANGES REQUIRED(无 BLOCKING;4 条 MAJOR)**
**QA 侧:REJECT(无 BLOCKING 安全洞;F-1 MAJOR + F-2/F-3/F-4 MAJOR)**

两侧独立收敛到**同一组四条 MAJOR**(编号各自独立,内容一一对应):

| 结构侧 | QA 侧 | 判据 | 依据(权威件原文) |
|---|---|---|---|
| **F-1** | **F-1** | `PatientSpatialDirector.cs:155` `atClinic := path.Count > 1 && Pose.PathCursor >= path.Count - 1` —— 用**路径游标抵达终点**(路径身份量)代偿 GDD 的**格集成员判定**(几何量) | GDD §States`:242`「`SeekingPhase(p) = AtClinic if p.Cell ∈ ClinicCells`」+ 变量表`:260`「`AtClinic` \| 病人格 ∈ `ClinicCells` \| **整数格成员判定**(禁 sqrt)」 |
| **F-2** | **F-2/B2** | `test_tc7_..._regardlessOfInsertion` **零判别力**:只打乱字典插入序,而 6 病人同速同路互不影响 ⇒ 升降序恒同 | TR-patient-017 / 实现注 1「打乱字典序输入不变」 |
| **F-3** | **F-3/B4** | 「`HomeRegion` 只在初始化时求一次」**不可观测** | story-002 AC 第 6 条 · GDD F-13.2 |
| **F-4** | **F-4/B1** | AC-13-E3「禁 `sqrt` / `Physics.Raycast` / NavMesh 采样」**无具名断言** | AC-13-E3 / ADR-016 §三 |

**根因(两侧措辞一致)**:story-001 的教训是「生产构造路径无调用 ⇒ 静默」;本 story 同类根因换面 ——
**性质断言了,但断言的接缝在生产代码里不存在可注入点**,夹具遂写成同义反复。

### 一处独立实现缺陷(非夹具问题)

**F-1 是真实规格缺陷,不是「没写够夹具」**:`atClinic` 的两个反例方向都有错、且零测试覆盖:

- **假阳** —— 路径终点非医馆格(巡逻/失败回退路径)时,走满仍判 `AtClinic`;
- **假阴** —— 路径**途经**门前锚点格而终点在其后时漏判;
- **单格路径** —— `path.Count > 1` 短路 ⇒ 病人**已站**在医馆格仍恒判 `EnRoute`,与 `:242` 直接冲突。

下游 `F-13.6` 的 `AwaitingCare`(立案优先候选,优先级 3)**直接吃这个分支**。

### 三处 MUT(评审期实测)

| MUT | 结果 | 归类 |
|---|---|---|
| 删运行期 `acc` 不变量检查 | 71/71 全绿 | 不可达防御(`LogicalStepper.Step` 自守)—— 如实登记,不判缺陷 |
| 删 `ids.Sort()` | 71/71 全绿 | 并入 F-2(对拍对象选值不敏感) |
| `HomeRegion` 每 tick 重求 | 71/71 全绿 | 并入 F-3(无可观测点) |

---

## 二、修复落点

### F-1 —— `AtClinic` 改回格成员判定

- `PatientSpatialDirector` ctor 新增注入 `Func<WorldPos, bool> inClinicCells`(与 `_pathOf` 同型只读谓词,零写 sim)。
- `Step` 内:`bool atClinic = _inClinicCells(state.Pose.Cell);`(逐字对 GDD §States`:242`)。
- `PatientSpatialState` 新增 `SeekingPhase Phase` 字段 + director 新增 `PhaseOf(PatientId)` ——
  相位此前只在 `Step` 局部,**无法从外部证伪**;现可观测(F-13.6 的输入面)。
- 回归夹具:`test_f13_7_atClinic_isCellMembership_notPathCursor`(格 ∈ ClinicCells 且路径远未走完 ⇒ 判 AtClinic)、
  `test_f13_7_atClinic_falseWhenPathEndsOutsideClinicCells`(尾格 ∉ ClinicCells ⇒ 判 EnRoute)、
  `test_f13_7_atClinic_singleCellPath_stillAtClinic`(单格路径 ⇒ 仍 AtClinic)。

### F-2 —— TC-7 换顺序敏感场景

- 新夹具 `test_tc7_sortIsLoadBearing_onHashOrderNonMonotonicKeys`:键集 `{8,17,33,64,128,129}`
  (int 哈希 = 自身 ⇒ 哈希序 ≠ 升序序),打乱插入;
- 加**求值序探针** `onEvaluated`(ctor 可选参,生产传 null 零开销)⇒ 求值序**直接可观测**,
  断言恒为升序,与插入序无关。

### F-3 —— `HomeRegion` 求值点可计数

- director 新增 `EcozoneOfCallCount`:`OnPresentEntered` 处 `++`(唯一求值点);
- 新夹具 `test_f13_2_homeRegion_evaluatedOnce_thenStableAcrossTicks`:3 人入表 ⇒ 恰 3 次;
  跑 10 tick 后**仍 = 3**(「只求一次」的可证伪判据)。

### F-4 —— AC-13-E3 具名断言(IL 引用扫描)

- 新增 `ScanProductionForForbiddenRefs`:读方法体 IL 字节,`call`/`callvirt`(opcode `0x28`/`0x6F`)
  后 4 字节 token 经 `Module.ResolveMethod` 解出 `声明类型全名::方法名`,比对禁项。
- 禁项逐字对 AC/ADR-016 §三:`System.Math::Sqrt` · `System.MathF::Sqrt` · `UnityEngine.Physics::Raycast` ·
  `UnityEngine.AI.NavMesh::SamplePosition` · `UnityEngine.AI.NavMesh::CalculatePath`。
- 扫描根**纪律**(承 story-001 B1):生产程序集 ⇒ 扫**全部类型**(被禁物的所在处必须被覆盖);
  测试程序集 ⇒ 只扫 `Shadow*` 影子件(否则测试自身探针自我命中)。
- 影子件 `ShadowWithSqrt`(真含 `Math.Sqrt`)与正测**共用同一台机器** ⇒ 必红且点名。

---

## 三、验证命令与结果

```
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.PatientAI" \
  --output "$PWD/unity/Logs/patientai-s002.xml"
```
- 修复前基线:`unity/Logs/patientai-s002.xml` = **71 / 0 失败**
- 修复后:`unity/Logs/s002-fix5.xml` = **78 / 0 失败**(新增 7 条夹具)
- 全量 EditMode:`unity/Logs/full-editmode-s002-fix.xml` = **2417 total / 2373 passed / 0 failed / 43 skipped / 1 inconclusive**
  (唯一 inconclusive = 既有 `SettingsExposureTest.test_monoOption_existsWithValidDefault`,与本 story 无关)

### 变异证明(四修毕各配 MUT 坐实「恰一条红」)

| MUT | 变异 | 结果 | 日志 |
|---|---|---|---|
| **MUT-F1a** | `atClinic` 还原旧「路径游标」判据 | 恰 2 红:`test_f13_7_atClinic_isCellMembership_notPathCursor` · `test_f13_7_atClinic_singleCellPath_stillAtClinic` | `unity/Logs/s002-mutf1a.xml` |
| **MUT-F2** | 删 `ids.Sort()` | 恰 1 红:`test_tc7_sortIsLoadBearing_onHashOrderNonMonotonicKeys` | `unity/Logs/s002-mutf2.xml` |
| **MUT-F3** | `Step` 内每 tick 重求 `HomeRegion` | 恰 1 红:`test_f13_2_homeRegion_evaluatedOnce_thenStableAcrossTicks` | `unity/Logs/s002-mutf3.xml` |
| **MUT-F4** | 生产件 `SqrDistance` 内注入真 `System.Math.Sqrt(2.0)` | 恰 1 红:`test_ac13e3_noSqrtNoRaycastNoNavMesh_ilReferenceScan` | `unity/Logs/s002-mutf4.xml` |

---

## 四、NOT-RUN 显式登记(禁借绿)

| # | 对象 | 归属 |
|---|---|---|
| NR-S2-1 | `ClinicCells` **真实数据**接入(24 `CONTEXT_TABLE` 房间格 ∪ 52 `CLINIC_FRONT` 锚点格)—— 本 story 只接**注入谓词**,未接真实格集 | **story-003**(F-13.6 `AwaitingCare` 前置) |
| NR-S2-5 | `ShouldDecideNow` 驱动接入(`Step` 每 tick 空跑,未真节流) | story-003/004 |
| NR-S2-6 | `ResetForLoad` 真负夹具(现负夹具只构造影子态,未注入「不重置的 Director」) | 本 story 或 story-003 |
| NR-S2-7 | story-001 遗留 NR-1(AC-13-A1 运行期半边)/ NR-8(`Emit` 签名类型信息) | 仍开 |
| NR-S2-8 | story-001 遗留 NR-5(band 空表)/ NR-6(终态 Tier)/ NR-7(Material 唯一消费点) | 仍开 |

**已闭合(修复轮)**:

| # | 对象 | 闭合证据 |
|---|---|---|
| NR-S2-2 | 升序求值判别力夹具 | MUT-F2 恰 1 红 |
| NR-S2-3 | `HomeRegion` 只求一次可观测夹具 | MUT-F3 恰 1 红 |
| NR-S2-4 | AC-13-E3 禁项具名断言 | MUT-F4 恰 1 红 |
| —— | `atClinic` = 格成员判定(实现缺陷) | MUT-F1a 恰 2 红 |

---

## 五、判定

**修复后:四条 MAJOR 全部闭合(MUT 坐实)。**
**无 BLOCKING。** 原判定 CHANGES REQUIRED / REJECT 的**实质内容已全部落地**;
两处独立实现缺陷之一(F-1 `atClinic`)已修,另一处(`LogicalStepper.Step` 路径耗尽消费时机)由**开发期测试**先于评审暴露并已修。

**残留**:NR-S2-1(真实 `ClinicCells` 数据)明确归 story-003,是本 story 的**已登记前置**,不借绿。
