# world-ecozones —— 当前 HEAD EditMode 复跑证据(2026-10-03)

> **性质**:这是**取证件**,不是评审件。
> 它补的是**两份评审共同点名的硬前置 P4** —— 「两轮评审均自陈**未运行 Unity EditMode**;
> 转 Complete 前须以当前 HEAD 重跑,**不得引用 2026-10-02 的 87/87 摘要借绿**」。
> 本件给出**本机以当前 HEAD 实跑**的逐 fixture 结果,并**点名 N2-1 修复后改写的三例**。
> 相关评审原件:`review-world-ecozones-2026-10-03.md`(一轮)· `review-world-ecozones-round2-2026-10-03.md`(二轮,判定基准)。

**复跑时点**:2026-10-03 14:53(UTC)· **HEAD = `a78c27a`**(工作树含本轮文档订正)
**被测对象**:`DaYiJingCheng.Tests.WorldEcozones` 全组

---

## 一、复跑命令(可复跑)

```bash
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath unity -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.WorldEcozones" \
  -logFile unity/Logs/eco-baseline.log \
  -resultFile /tmp/eco-baseline.xml
```

⚠️ **实现期留痕**:`-resultFile /tmp/...` **不被 Unity 采纳**,产物实际落
`unity/TestResults-639266647830825800.xml`(本仓已知行为,与 player-controller 轮同)。
**退出码 `0`**(0 = 全过;退出码 2 = 有跳过/inconclusive,**非**失败)。

---

## 二、结果

**套件**:`Passed` · `total 109 · passed 109 · failed 0 · skipped 0 · inconclusive 0`
**时长**:0.295 s(`start 2026-10-03 14:53:02Z` · `end 14:53:03Z`)

| fixture | story | Passed |
|---|---|---|
| `ChunkActivationTest` | 004 | 10 |
| `EcozoneQueryTest` | 002 | 25 |
| `PoiPayloadEncoderTest` | 006 | 13 |
| `PoiStateMachineTest` | 003 | 19 |
| `WorldLatticeTest` | 001 | 42 |
| **合计** | | **109** |

**零跳过、零 inconclusive** —— 与 player-controller 的 3 例 NOT-RUN 不同,
本 epic **不存在**「显式未跑」的子条(全部 109 例均实际执行并全绿)。
⚠️ 这不等于「P4 之外无缺口」:评审点名的 `[L]` 桌面走查(story-005)与
`World.unity` 构建期扫描(story-004:58)是**EXTERNAL / 未执行**项,
**不进 EditMode 套件**,故**不因本件 109/109 而转绿**(不得借绿)。

---

## 三、点名:**N2-1 修复后改写的三例已实跑并全绿**

二轮评审 N2-1 判「B2 的补测**形式已补、实质仍弱**」(三例未产生任何 `ActorCellEntered` 事件;
「同 tick」断言恒真)。该修复落于 `df9e781`(**晚于**二轮报告 13:42),改写为
`test_ac623_discoverGate_drivenByRealCellEntry` 等。**该三例此前从未被跑过** —— 本件补此绿证:

| 测试方法 | 结果 | 耗时 |
|---|---|---|
| `ChunkActivationTest.test_ac623_discoverGate_drivenByRealCellEntry` | **Passed** | 0.011240 s |
| `ChunkActivationTest.test_ac623_discoverGate_idempotentOnSecondEntry` | **Passed** | 0.000717 s |
| `ChunkActivationTest.test_ac623_discoverGate_blockedOnUnloadedChunk` | **Passed** | 0.011422 s |

⇒ 「同 tick」判据现由**真 `ActorCellEntered` 事件驱动**(事件由生产类
`Gameplay.Presentation/Player/CellTransitionDetector.cs` 产出,tick 取自事件而非测试硬编码),
**非恒真**;三例**已实跑通过**,形式补齐**有绿证**。

---

## 四、本件**不**主张(禁借绿)

- ❌ **不**主张 story-005 的 `[L]` 桌面走查已完成 —— 该件 `Status: Pending`,C1–C6 全 `Not-RUN`,
  是**真实未执行**的 EXTERNAL 项,非状态漂移。
- ❌ **不**主张 `World.unity` 构建期零 gameplay 对象扫描已验 —— 该 AC(story-004:58)已**诚实撤勾**
  (`World.unity` 文件本身尚不存在,见 ADR-023 三场景制)。
- ❌ **不**主张 N3(消费白名单 `{4,25,37}` 静态引用断言)已闭环 —— 该判据**零实现**,
  负向夹具对象(27 侧 `Sim/EnemyAI/`)**目录不存在**;story-004:50 已诚实撤勾并写明重开条件。
- ❌ **不**以本件 109/109 替代任何**行为签核**(playtest / 桌面走查)。

---

## 五、与 2026-10-02 摘要的关系

`editmode-full-rerun-2026-10-02.md` 记 world-ecozones **87/87**,`epics/index.md` 记 **106/106**,
`story-006` 自陈 **106/106** —— 三者计数**互相不一致**,且均为**转录摘要**
(原始 XML 未入仓)。本件以当前 HEAD 实测 **109**,并落**可复跑命令**;
**上列旧计数不再作为转 Complete 的依据**(承「不得借绿」)。
计数差异成因:107→109 含 `df9e781` 判据修复轮新增/改写的测试。
