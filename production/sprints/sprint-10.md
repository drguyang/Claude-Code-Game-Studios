# Sprint 10 — 急救直读通道运行期装配(2026-10-14 开)

> **目标**:story-009(急救直读通道运行期装配 + 跳过入口接线)—— M2 Exit `:169`
> 形态件③ 的功能半;急救链运行期可达性 0% → 100%。
> **承接**:sprint-09 S3 复评登记的真缺口(`:169` 不勾,有主 = story-009)。

## 范围

| Story | 状态 | 判据 |
|---|---|---|
| **story-009**(emergency-procedures) | **In Progress 🔨**(2026-10-14 实现轮) | 11 AC:9 BLOCKING 自动化 + 2 ADVISORY 人工走查 |

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

**剩余**:AC-9-10/11 人工走查(NOT-RUN)→ 评审原件 → 收口(M2 Exit `:169` 翻勾候选)。
