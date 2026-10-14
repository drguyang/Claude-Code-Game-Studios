---
name: review-scope-discipline
description: 评审的「评据不得越权」—— 只对实际核过的范围下判定;作「零验证/全仓零命中」等全域否定前须给全树搜索命令与输出;声明件 ≠ 全证据面,账本路径 ≠ 编译面
metadata:
  type: feedback
---

复审时,凡要落笔写「**零验证**」「**全仓零命中**」「**无处可依**」「**无任何测试覆盖**」
这类**全域否定**,先自问:**我真的核过全域吗?** 拿不出全树搜索命令与输出 ⇒
**降级为「在 X 范围内未发现」并写明 X**。

**实例(2026-10-15 case-system 001–004 补评 · 5 条全域否定全被推翻)** ——
两名代理**只核了 story §Test Evidence 声明的那个文件**,却据此判定全域缺失:

| 原判(误) | 真载体 |
|---|---|
| AC-37-30 入流义务「**零验证**」 | `CaseSystem/case_open_writer_test.cs`(真组合根 + 真流,断言病例流恰增 1 条)<br>`PlayMode/case_opened_wiring_test.cs`(**真 BootRoot 启动序**,断言流含 CaseOpened) |
| AC-37-05 判断半边「扫错对象」 | `Boot/judgment_writer_test.cs`(真装配) · `Boot/casebook_command_router_test.cs`(8 测) |
| AC-37-05 处置半边「**全仓零命中**」 | 语义载体 = `CaseCloseDecider.HasTreatmentInWindow`;判据在 `case_close_precondition_test.cs`(真 `DrugTreatmentApplied`/`EmergencyTreatmentApplied` 事件)。「第二副本」只是**词面**零命中 ≠ 语义零覆盖 |
| AC-37-23 不折叠 | `CaseSystem/case_replay_persistence_test.cs`(`noFold_historicalRowsComplete` 等两测) |
| story-003「→8 读数清除 / →53 判断冻结**零测试**」 | `DiagnosisSystem/reading_state_test.cs:395-411`(`OnCaseClosed` 全状态清空 + `FrozenJudgment.Frozen` + `HandoffTo53`) |

**两条结构性诱因(下全域结论前必须先排除)**:

1. **声明件 ≠ 全证据面** —— 一个 AC 的载体常横跨「story 声明的那个件」与**后续 sprint 的接线件**
   (真装配件 / 真启动序件 / PlayMode 端到端)。接线件可能**晚于 story 收口数天**
   ⇒ 当年收口时确实不存在,**但补评口径是「评当下」,必须计入**。
2. **测试件横跨两棵树,须都查** —— `tests/unit|integration/[system]/` 是**账本约定**树
   (`tests/README.md`),`unity/Assets/Tests/{EditMode,PlayMode}/` 是工程落地后的**主体编译树**
   (256+ 件)。**两棵树都可能承载活测试** —— 账本树**并非恒空**
   (`tests/unit/skeuomorphic-ui/` 自带 `.asmdef`,是活测试工程)。
   按**单一**树判「目录不存在 ⇒ 缺失」**会系统性误判**。
   ⚠️ 反向亦然:不得因此断言「账本树已作废」—— 那只是把越权换了个方向。

**How to apply**:立「无证据」判定前,至少跑一次
`grep -rln "<被测符号>" unity/Assets/Tests/ tests/`(以及
`grep -rln "<符号>" unity/Assets --include=*.cs | grep -v /Tests/` 确认生产调用者),
并**在报告中原样贴出输出**。若贴不出 ⇒ 该判定只能写成「在 <已核范围> 内未发现」。

**Why**:本条与 [[placeholder-test-false-green]] 互补 —— 那一篇防「**假绿**」(有证据但无效),
本篇防「**假红**」(证据在别处却判缺失)。两者的共同敌人都是「报告写了它没核过的范围」。
纪律出处:`.claude/docs/review-workflow.md` 义务 4「评据不得越权」(2026-10-15 立)。
相关:[[ac-carrier-discipline]]
