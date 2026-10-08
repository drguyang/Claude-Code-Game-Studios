# 实跑验证记录 —— emergency-procedures epic 转 Complete 前置

**验证时点**:2026-10-08
**验证对象**:`production/epics/emergency-procedures/`(7/7 story 已实现,卡 `In Review`)
**验证动机**:EPIC §Epic Status ③ 登记「转 Complete 的真实前置」第 3 条 = **实跑 Unity 测试套件** ——
round2 评审自陈「**未实跑**,凡『测试通过』类结论均来自**读测试源码**而非执行结果」⇒
EPIC 现记的「EditMode 0 红 · PlayMode 36/36」**未经验证**。

---

## 一、实跑结果(2026-10-08 · 当前 HEAD)

| 范围 | 命令 | total | passed | failed | inc | skip |
|------|------|-------|--------|--------|-----|------|
| EditMode · 急救模块 | `--filter "DaYiJingCheng.Tests.EmergencyProcedures"` | 120 | 116 | **0** | 0 | 4 |
| PlayMode · 急救模块 | `--filter "DaYiJingCheng.Tests.PlayMode.EmergencyProcedures"` | 4 | 4 | **0** | 0 | 0 |
| EditMode · 全量 | `--mode EditMode` | 3001 | 2954 | **0** | 1 | 46 |
| PlayMode · 全量 | `--mode PlayMode` | 96 | 96 | **0** | 0 | 0 |

**结论**:EPIC 现记的「EditMode 0 红 · PlayMode 全绿」**实测成立**(0 红)。PlayMode 计数由声称的 36 增至实测 96(其他 epic 的 PlayMode 测试累积),同样全绿。

日志:`unity/Logs/emergency_editmode.xml` · `emergency_playmode.xml` · `editmode_full_case006.xml` · `playmode_full_ep10.xml`

### 4 条 skip 逐条核验(禁借绿)

| 测试 | 理由 | 判定 |
|------|------|------|
| `FeelLatencyTest.test_ac1008_lInput_notRun` | AC-10-08 L_input 实测待 `OQ-10-12` 原型门 | ✅ 合规 NOT-RUN |
| `FeelLatencyTest.test_ac1019_remoteLInput_notRun` | AC-10-19 联机 L_input 待 45 网络层 | ✅ 合规 NOT-RUN |
| `FeelLatencyTest.test_ac1023_playtestWalkthrough_notRun` | AC-10-23 录屏走查待桌面调试轮 | ✅ 合规 NOT-RUN |
| `JudgeTest.test_ac1004b_goldenFixture_notRun` | AC-10-04b 跨平台黄金夹具待 ADR-012 矩阵 | ✅ 合规 NOT-RUN |

⇒ 4 条**均为带理由的 NOT-RUN**,非静默跳过,无借绿。

---

## 二、D1/D2/D3 文档状态对齐(round2 未验证项 · 本轮核实)

| # | 目标 | 实测 | 判定 |
|---|------|------|------|
| **D1** | AC-10-04a 单元格数字同步 A8 勘误 | `emergency-procedures.md:1030` 已写 `(32768×16385)` ⇒ 8193;`:1031` 有订正说明「原写 `(32769×16384)`」 | ✅ **已对齐** |
| **D2** | story-006 测试位置声明 | `story-006:76` 已声明「原声明为 `Tests/PlayMode/`,文件真身在 `Tests/EditMode/`」 | ✅ **已对齐** |
| **D3** | story-007 Test Evidence 行 | `story-007:207` = `[x] Complete — 17/17 passed` | ✅ **已对齐** |
| **D3-b** | EPIC §Stories 表 002–006 标 `Ready` 与 story 件标 `Complete` 矛盾 | 实测 Stories 表 001–007 **全部标 `Complete`** | ✅ **已对齐** |

**D1 算术复核**:`32768 × 16385 = 536,903,680`;`÷ 65536 = 8192.5` 恰落半 ⇒ half-away ⇒ **8193** ✅。
原稿 `32769 × 16384 = 536,887,296 ÷ 65536 = 8192.25`,余数非半 ⇒ 无舍入分道 —— 订正正确。

---

## 三、story 头/体一致性复核(D3 同型缺陷)

| story | 头部 Status | 体 Test Evidence | 一致 |
|-------|-------------|------------------|------|
| 001 | Complete ✅ 2026-10-02(11/11) | — | ✅ |
| 002 | Complete ✅ 2026-10-02(21/21) | — | ✅ |
| 003 | Complete ✅ 2026-10-02(16/16) | — | ✅ |
| 004 | Complete ✅ 2026-10-02(EditMode 10/10 + PlayMode 4/4) | — | ✅ |
| 005 | Complete ✅ 2026-10-02(20/20) | — | ✅ |
| 006 | Complete ✅ 2026-10-02(10/10 + 3 NOT-RUN) | 位置已订正(D2) | ✅ |
| 007 | Complete ✅ 2026-10-03(17/17) | `[x] Complete`(17/17) | ✅ |

⇒ 无头/体矛盾残留。

---

## 四、转 Complete 前置结算

| # | 前置(EPIC §③) | 状态 |
|---|----------------|------|
| 1 | 报告已落盘(两份) | ✅ 2026-10-03(首轮 + round2) |
| 2 | D1/D2/D3 文档状态对齐 | ✅ **本轮核实全部已对齐** |
| 3 | **实跑 Unity 测试套件** | ✅ **本轮完成**(0 红;4 NOT-RUN 合规) |

### 未闭项(不阻塞本 epic,登记)

- ⏸️ **载荷 `Seq` 占位** —— 归上行链 45(现有测试已钉死占位事实,合规)
- ⏸️ **AC-10-04b 三格逐位** —— ADR-012 矩阵未激活,NOT-RUN,禁借绿

---

## 五、判定

**7/7 story 已实现 + 实跑 0 红 + 文档对齐 ⇒ 转 `Complete` 的前置全部满足。**
未闭两项均**结构性归属其他 epic**(45 / ADR-012),按项目惯例不阻塞本 epic 转 Complete(同 `modular-building` / `world-ecozones` 先例)。
