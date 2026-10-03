# 对账件:player-controller 判据修复轮(2026-10-03)

> **性质**:这是**对账件(reconciliation)**,不是评审原件。
> 它验证「本轮所述修复是否真在代码里」,**不**验证「原判定是否完备」。
> 原判定原件 = `review-player-controller-2026-10-03.md`(首轮)+ `review-player-controller-round2-2026-10-03.md`(第二轮)。
> **本件落盘 ⇒ 本 Epic 的两项转 Complete 前置(评审原件 + 判据缺陷修复)均已满足。**

**对账对象**:`production/epics/player-controller` 全 epic(6 story)
**对账时点**:2026-10-03 · HEAD = `f965ef8`(工作树含本批改动)
**验证命令**:见每节末尾(均可复跑)

---

## 一、前置对账(第二轮评审「转 Complete 的前置 1–5」)

| 前置 | 评审要求 | 实测 | 证据 |
|---|---|---|---|
| **#1** | `motor_lease` 白名单自指空转(`foreach(AllowedCallerAssemblies)` ⇒ 排他半边永假) | ✅ **已修** | `motor_lease_test.cs:151-196` —— 改为遍历 `AppDomain.CurrentDomain.GetAssemblies()`,白名单外调用者真会被检出(源码注释自陈「我自己重犯」) |
| **#2** | story 件文本同步(5 处 `Pending`/`_待填_`) | ✅ **已修,且实测范围更大** | 见下 §二 —— 实为 **6 份文件、12 处**,评审只点名 5 处 |
| **#3** | 复跑并落数 | ✅ **已跑** | 见下 §三 |
| **#4** | `AC-1-18` 补真跨格夹具 | ✅ **已修** | `locomotion_chain_test.cs:272-322` |
| **#5** | `CanonicalLatticeMm` 改读真实 `WorldLatticeParams.LatticeSizeMm` | ✅ **已修** | `controller_foundation_test.cs:230-237` + 新 helper `CanonicalLatticeSizeMm()` |

---

## 二、前置 #2 的对账:**实测范围大于评审判定**

评审判定「5 处文本不同步」。实测为 **6 份文件、12 处**,且存在评审判定**未提及**的更重形态:
**同一 story 件内,文件头 `Status: Complete` 与文件体 `Status: [ ] Pending` 直接矛盾**。

| story | 原缺陷(文件体) | 修法 | 复验 |
|---|---|---|---|
| 001 | **两个 `## Completion Notes`**(前者 `_待填_` 占位,后者 2026-09-30 实记);四栏 `_待填_`;`:169` 陈旧的「AC-1-10② 是 `Assert.Pass` 简化版」 | 重复节合并为一并回刷;四栏填实;`:169` 划删并标已闭 | `grep -c '^## Completion Notes'` = **1** |
| 002 | `Test Evidence` 节 `[ ] Pending`;四栏 `_待填_` | 改 `[x] Done`(16/16);四栏填实 | `grep '_待填_'` = 0 |
| 003 | 头行与 Criteria 记 **22/23 + 1 NOT-RUN**(与实跑 **21+2** 不符);Deviations 两条「完整版需要…」已失效 | 头行/Criteria 订正为 21+2;Deviations 重定为 `AC-1-06a/b/c` 与 `1-18` 的**实际修法** | 见 §三 |
| 004 | `Test Evidence` 节 `[ ] Pending`;**14 条 AC 零勾** | `[x] Done`(18+1 / 2);14 条 AC 勾选,`AC-1-04` 标 **NOT-RUN** | `grep -c '^- \[ \]'` = 0 |
| 005 | `Test Evidence` 节 `[ ] Pending`;AC 零勾 | `[x] Done`(6/6);2 条勾选,**`AC-1-22` EXTERNAL 保持未勾** | `grep -c '^- \[ \]'` = 1(= EXTERNAL) |
| 006 | `Test Evidence` 节 `[ ] Pending`;AC 零勾 | `[x] Done`(14/14);4 条勾选,`AC-1-29` 标 **NOT-RUN** | `grep -c '^- \[ \]'` = 0 |

**口径声明(承「不得借绿」)**:AC 勾选 = 「判据已落 / 已登记」,**不**等于「该子条已通过」。
NOT-RUN / EXTERNAL / BLOCKED 的子条一律**保持未勾或就地标注**,不做静默勾选。

**EPIC / index 连带订正**:`EPIC.md` 头行 `In Review` → `Complete ✅ 2026-10-03`;§Epic Status 重写(两项前置均满足 + 逐 fixture 计数 + 未闭项登记);新增 §状态漂移第二轮回刷表;§Next Step 改为「本 Epic 已 Complete」。`epics/index.md` 第 33 行同步。

---

## 三、前置 #3 的对账:复跑计数

**命令**:
```bash
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath unity -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.PlayerController" \
  -logFile unity/Logs/pc-group2.log -resultFile /tmp/pc-group2.xml
```

**结果**(`unity/TestResults-639266635057280820.xml`):`PlayerController` **91 例 = 88 Passed + 3 Skipped + 0 Failed**。

| fixture | story | Passed | Skipped |
|---|---|---|---|
| `ControllerFoundationTest` | 001 | 11 | 0 |
| `InputContractTest` | 002 | 16 | 0 |
| `LocomotionChainTest` | 003 | 21 | 2 |
| `CellTransitionTest` | 004 | 18 | 1 |
| `StreamBoundTest` | 004 | 2 | 0 |
| `HostAuthorityTest` | 005 | 6 | 0 |
| `MotorLeaseTest` | 006 | 14 | 0 |
| **合计** | | **88** | **3** |

**3 例跳过的归属(逐条,禁借绿)**:
- `AC-1-06c`(story 003)—— **NOT-RUN**:AST 初始化式判据的载体 = Roslyn 分析器,ADR-024 §⑤ 明令本仓不引 ⇒ 无载体,判据面在 6 侧的 `AC-6-07` 值级守卫。
- `AC-1-21`(story 003)—— **BLOCKED-BY-OQ-1-12**:接地 spike 未跑。
- `AC-1-04`(story 004)—— **NOT-RUN**:P0 无 VR 主语(P1a)。

> ⚠️ **计数订正留痕**:story 003 原记「22/23 + 1 跳过」、EPIC 原记「全 epic 92 例」。
> 前者是 2026-10-02 快照(其时 `AC-1-06c` 是假绿 pass);本批该条由假绿转**显式 skip**(评审 A3 的预期结果)⇒ 21+2。
> 后者 92 与实测 91 差 1,同源于该条状态变更。**两处均已订正**。

---

## 四、前置 #4 的对账:`AC-1-18` 真跨格夹具

**原缺陷**:初版注释称「第二帧:跨到格 1」,但实际调用传的仍是 `currentCellIndex = 0`(**与第一帧同一索引**)
⇒ 所谓「跨格那一帧」只是同索引调了两次,「用旧格乘数」是**重言式**;第三帧才传 1 ⇒ **从未模拟「跨格发生的那一帧」**。

**修法**(`unity/Assets/Tests/EditMode/PlayerController/locomotion_chain_test.cs:272-322`):改为**位置驱动**夹具 ——
帧 2 的 `Move` **真的**把玩家从 `x=0.4`(格 0)送进 `x=1.6`(格 1),格索引经
`CellTransitionDetector.CellFromPosition`(与生产**同源**,非测试自选索引)由位置派生。

**三条断言**:帧 1 格 0 ⇒ 5.0;帧 2 **真跨格**但 `v_target` 仍 = 5.0(旧格乘数);帧 3 才 = 2.5(新格)。

**可证伪性**:夹具自带前提断言 `Assert.AreEqual(1, cellAfterMoveF2, "第 2 帧的 Move 确实把玩家送进了格 1(真跨格,非重言)")` ——
若位置未真跨格,该断言**先红**,判据不可能空转成立。

---

## 五、前置 #5 的对账:`CanonicalLatticeMm` 硬编码

**原缺陷**:`controller_foundation_test.cs` 把「规范格边长」写成**局部字面量 `1000`**(两处),与生产
`WorldLatticeParams.LatticeSizeMm` 各持一份 ⇒ 生产改格边长,本测**静默测旧值**。

**修法**:新增 helper `CanonicalLatticeSizeMm()` **构造真实 `WorldLatticeParams` 并读回 `LatticeSizeMm`**
(同源纪律,与 `AC-1-12` 的 `MAX_DT` 同型);两处调用点均改读该值。
负向夹具的 `hugeRadius` 同步改为「按读出值派生」,不再与格边长取值耦合。

⚠️ **实现期一处真实发现(留痕)**:首次修法的夹具参数(`latticeSizeMm: 1000` + `speedModeMax: 5`)被
`WorldLatticeParams` 自身的 **F-6-1 防隧穿守卫**拒收 ——
`LATTICE_SIZE(1000) < SPEED_MAX(10) × MAX_DT(100) × SAFETY(2) = 2000 mm`。
⇒ 夹具改为 `latticeSizeMm: 10000`(自洽:10 m 格)。**该守卫按设计工作**,非缺陷。

---

## 六、本轮**未**修改的项(如实登记,不主张已闭)

- **`OQ-1-12` 接地 spike** —— P0 开工前须由用户裁决;`AC-1-21` 与 `AC-1-17` 的接地进入条件半边挂此。
- **`O-9`** —— ADR-015 §一 尚未点名 `slopeLimit`/`stepOffset` 几何值 ⇒ `AC-1-33②` 记 BLOCKED。
- **`O-4`** —— 45 侧登记行(P1b,45 无 GDD);未出现在任何 PR 描述中。
- **`AC-1-22`(EXTERNAL)** —— 主语 = 29/45,不进本 Epic 验收面。
- **`AC-1-04`** —— VR,P1a。
- **story-004 的 codec 绕行** —— `CellTransitionDetector` 走 `PayloadRef` 三整数字段,不经 `Sim.Codec`;
  因 `Gameplay.Presentation.asmdef` 未引用 `Sim.Codec`,接线须先加程序集依赖边。**已在 story-004 §Deviations 记账,未登记为 TODO 注释**。

---

## 七、结论

**本 Epic 的两项转 Complete 前置均已满足**:① 评审原件已落盘(两轮);② 判据缺陷已修并复跑 0 红。
**EPIC 已转 `Complete ✅ 2026-10-03`**。

⚠️ **本件不主张**:NOT-RUN / EXTERNAL / BLOCKED 的子条已通过 —— 它们**显式保持未勾**,
其中 `OQ-1-12`(接地 spike)是本 Epic 唯一真正待用户裁决的开工前置。
