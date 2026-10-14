# M2 Exit 复评 —— `:169` 4 项形态件翻勾(story-009 落地判据兑现)

- **复评对象**:`production/milestones/README.md:169`「4 项形态件交付」的 ③ 半边
  (急救零数字 + **可跳过** —— 功能半)
- **复评日期**:2026-10-14
- **复评性质**:按证据翻勾(承 sprint-09 S3 复评原件
  `m2-exit-recheck-sprint09-2026-10-14.md` 判决 B 的白纸黑字判据:
  **「本条的③项翻勾判据 = story-009 落地(端到端直读通道 + 跳过入口接线 + Idle 门);
  在此之前维持 `[ ]`(禁借绿)」**——前置今日已兑现,本件即依判据复核)
- **判定**:**✅ 翻 `[x]`** —— `:169` 全条(4/4 形态件 + 施加点)就此闭合。
  **口径收窄**:本判定覆盖 sprint-09 S3 复评核过的三条(`:111` / `:169` / `:224`)所属的
  **勾选滞后面 = 全部结清**;M2 §三 内另有独立推进项 `:252`(五族切图与 atlas 布局冻结,
  story-020/019 链,◐ 半闭 —— **非勾选滞后,有主推进中**),不在本复评判据面内。

---

## 翻勾判据逐条实测(2026-10-14 · 对当下代码)

### 判据 A —— 跳过入口交互绑定(旧判据:零命中 ⇒ 现命中)

```
$ grep -n "focusable\|RegisterCallback" unity/Assets/Gameplay.UI/Skeuomorphic/EmergencyCandidateList.cs
120:            _skipEntry.focusable = true;
121:            _skipEntry.RegisterCallback<NavigationSubmitEvent>(_ => SubmitSkip());
122:            _skipEntry.RegisterCallback<ClickEvent>(_ => SubmitSkip());
```
双路径(键盘/手柄 NavigationSubmit + 鼠标 Click)同投 SubmitSkip;
UiExceptionGuard 同款洪流守护(CasebookScreen 先例,30 帧窗口)。

### 判据 B —— `SetVisible` 生产调用(旧判据:仅测试 ⇒ 现有生产链)

```
$ grep -rn "\.SetVisible(" unity/Assets --include="*.cs" | grep -v Tests
EmergencyCandidateRuntime.cs:88:  public void SetVisible(bool visible) => _list.SetVisible(visible);
CompositionRoot.cs:275:           onIdleGate: visible => emergencyCandidates.SetVisible(visible));
```
Idle 门闭环:`EmergencyChannelDriver`(Arm ⇒ false / End·Abort ⇒ true)→
组合根 onIdleGate 回调 → `SetVisible` 透传(R-9.3)。

### 判据 C —— 直读通道生产实例化(旧判据:全仓唯一 new 在测试 ⇒ 现有生产构造)

```
$ grep -rn "new EmergencyDirectReadChannel(" unity/Assets --include="*.cs" | grep -v Tests
CompositionRoot.cs:271:  var emergencyChannel = new EmergencyDirectReadChannel(...)
```
装载 = `InputSystem.actions`(story-009 R-9.5 订正;BootRoot 启动序 2.5 fail-loud 校验),
`BootRoot.cs:197` `Attach()` 挂 onAfterUpdate(AC-9-01)。

### 判据 D —— 端到端链(意图 → 判域 → 流)

```
F 按下沿 → InteractionInputDriver.BeginEmergency      InteractionInputDriver.cs(裁定②按住语义)
  → EmergencyChannelDriver.Arm(0, target)             EmergencyChannelDriver.cs
  → (Armed 逐帧采样)EmergencyDirectReadChannel.onAfterUpdate
  → F 松开沿 → EndAction → EmergencyManualDriver.TryAggregated
  → HostEmergencyProcessor.Process → EmergencyAttempt + EmergencyTreatmentApplied 双事件入病史流
跳过:UI Submit → EmergencyCandidateList.SubmitSkip → CasebookCommandRouter.SubmitEmergencySkip
  → EmergencyManualDriver.TryManual(method:1) → 同一双事件(AppliedWeak 档,AC-9-05)
```
fail-loud 面:未接线 skip ⇒ 抛(装配遗漏);未绑病人/不在场 ⇒ 零事件零提示。

### 判据 E —— 自动化判据(评审套件,当日在盘)

| 套件 | 结果 | 日志 |
|---|---|---|
| PlayMode | **186/186,failed=0** | `unity/Logs/story009_review_playmode.xml` |
| EditMode | **3279/0**(skip wiring 九测全过) | `unity/Logs/story009_review_editmode.xml` |

承重测试(全 Passed):`test_armFeedEnd_writesBothEvents_withAggregatedShape`(端到端)·
`test_idleGate_*`(Idle 门两向)· `test_armedIdleBeyondAbortTicks_tickEdgeAborts_zeroEvents`
(中止≠跳过)· `test_longPressWithContinuousFeeding_notAborted`(评审 F-1 回归)·
`test_ac907_skipEntry_focusable_true` · `test_candidateRuntime_skipCallback_invokedThroughRouter`
(UI→路由→写者闭环)· `test_ac905_*`(skip 路径 + 强度判别)· `test_router_*` ×4。

**story-009 评审原件**:`production/qa/evidence/review-story-009-2026-10-14.md`
(1 BLOCKING F-1 当场修复 + 回归测试;双套件零回归)。

---

## 边界与诚实登记(禁借绿声明)

- 本翻勾覆盖 **③ 的「可跳过」功能半 + 直读通道装配**(形态件交付判据 + 施加点接线判据)。
- **AC-9-10 / AC-9-11(ADVISORY 人工走查)NOT-RUN 欠账在案**(story-009),不构成本条判据
  (`:169` 判据 = 交付 + 施加,非走查),但已登记归人工走查批。
- **稳度门单键不可达**(评审 F-2)为裁定②设计后果,归 21a/10 烘焙轮,不影响本条(形态件判据不含玩法深度)。
- 候选 entry 动作语义(止血/通气点击行为)归真动作表轮 —— ③ 的「零数字」半不受影响
  (story-023 已闭)。

## 结论

`production/milestones/README.md:169` 条目翻 `[x]`:
- 4 项形态件 **4/4 交付**(①story-021 ②story-022 ③story-023 ④story-024,2026-10-08/09 已闭)
- 施加点 **4/4 已接**(①②④ 于 2026-10-10/11 复评闭合;③ 于 2026-10-14 经 story-009 闭合)
- sprint-09 S3 核过的三条勾选滞后**全部结清**;M2 §三 内的 `:252`(五族切图冻结,
  story-020/019 链)为独立推进项(◐ 半闭),不随本翻勾联动。
