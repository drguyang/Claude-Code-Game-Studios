# Sprint 10 — 急救直读通道运行期装配(2026-10-14 开)

> **目标**:story-009(急救直读通道运行期装配 + 跳过入口接线)—— M2 Exit `:169`
> 形态件③ 的功能半;急救链运行期可达性 0% → 100%。
> **承接**:sprint-09 S3 复评登记的真缺口(`:169` 不勾,有主 = story-009)。

## 范围

| Story | 状态 | 判据 |
|---|---|---|
| **story-009**(emergency-procedures) | **Complete ✅**(2026-10-14 实现轮 + 当日评审收口) | 11 AC:9 BLOCKING 全勾 + 2 ADVISORY 欠账登记(归人工走查轮) |

## 进展

### 2026-10-14 · story-009 实现轮(当日立件 + 开工)

**四项用户裁定(开工前,全按建议)**:① R-9.5 装载 = `InputSystem.actions`(原稿
Addressables 被实测推翻)② F 键 = 按住 = 一次动作 ③ `EmergencyChannelDriver` 顺带实现
`IArmedState` ④ AC-9-05 带最小 Judge 分支。

**代码落点**(9 文件改 + 3 新,详见 story-009 Implementation Notes):
- 新 `EmergencyChannelDriver`(Boot):相位搬运 + 聚合映射 + Idle 门 + `IArmedState`
- `EmergencyManualDriver.TryAggregated` + `HostEmergencyProcessor` method 分支 +
  `ModalPhaseEvaluator.AbortIdleTicks` 提公开
- `CompositionRoot`(inputActions 可选参 + 装配)/ `BootRoot`(fail-loud + Attach +
  skip 注入 + UI 回调 + 中止检查 + Detach)/ `InteractionInputDriver`(按住语义)
- 42 侧:接口增员 `SubmitEmergencySkip` + 路由实现 + 候选列表 focusable/回调/洪流守护
  + 候选项 `.skip-entry`→`.candidate-entry` 勘误
- ADR-025 §① Gameplay.Boot 行窄修订(refs +`Gameplay.Input`,依据 R-9.2)

**验证(BatchMode · 6000.3.24f1)**:
- EditMode **3279/0**(基线 3270,+9 新测,零回归;1 inconclusive 既有)
- PlayMode **185/0**(基线 175,+8 新测;interaction_chain ② / e2e ⑥ 改判按住语义,非回归)
- 日志:`unity/Logs/story009_{editmode,playmode}.{log,xml}`

**评审收口(2026-10-14 同日)**:评审原件 `production/qa/evidence/review-story-009-2026-10-14.md`
—— 查获 **F-1 BLOCKING**(中止判据 × 按住语义致命交互:合法长按 ≥ 2.5 s 被误杀)
⇒ 判据改**采样活跃度看门狗**(`_lastActiveTick` 随 Feed 推进)+ 回归测试
(`test_longPressWithContinuousFeeding_notAborted`,120 tick 持续采样不中止);
F-3 minor(消息笔误)已修;F-2 ADVISORY 登记(稳度门单键不可达,归烘焙轮)。
修复后 **PlayMode 186/186 · EditMode 3279/0** 零回归。**story-009 → Complete ✅**。

**M2 Exit `:169` 翻勾(2026-10-14 同日)**:sprint-09 S3 复评预设的翻勾判据(「story-009 落地」)
兑现 ⇒ 复评原件 `production/qa/evidence/m2-exit-recheck-sprint10-169-2026-10-14.md`
(判据 A–E 逐条实测:交互绑定命中 / SetVisible 生产链 / 通道生产实例化 / 端到端链 /
双套件)⇒ `README:169` 翻 `[x]`,**sprint-09 S3 三条勾选滞后全部结清**
(M2 §三 内 `:252` 五族切图冻结为独立推进项,不随本翻勾联动)。

**剩余(非本 sprint 阻塞)**:AC-9-10/11 人工走查批(ADVISORY 欠账,需用户操作编辑器);
sprint-09 收尾小件(`review-mode.txt` 未落 · 2 张 2048² 图片重生 · round-7 证据两文件待提交)。
