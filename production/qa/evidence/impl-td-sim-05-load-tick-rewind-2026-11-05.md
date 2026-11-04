# 实现原件 — `TD-sim-05` 读档后体征链时间倒流(2026-11-05)

> **对象**:`DiseaseVitalsService` / `DiseaseCourseBook` / `ProcessingCompletionDriver` /
> `CraftCompletionSettler` / `CompositionRoot`(`afterRestore` 钩子)
> **债**:`docs/tech-debt-register.md` §`TD-sim-05`(`open` / P0 面 **Y** / R1)
> **用户指令**:「顺手把 TD-sim-05 也修了」

---

## 1. 缺陷

三件派生态 + 两个游标**都只进不退**,而读档会把事件流换成**更早**的内容:

| 件 | 只进不退的量 | 读档后 |
|---|---|---|
| `DiseaseVitalsService` | `_lastTick` / `_cursor` / 课程簿 / `_results` / `_outcomes` / `_outcomeHistory` | 下一个 tick 边沿抛「体征链 Step 时间倒流」,**每 tick 抛一次**直到时钟追平;恢复出来的事件一条都不再应用 |
| `ProcessingCompletionDriver` | `_lastTick` | 抛「非递增 tick」,不发完成 cue |
| `CraftCompletionSettler` | `_lastTick` | 抛「非递增 tick」,不起货 |

征兆与用户 2026-11-04 报的「再进入已有存档 console 刷屏」**同形不同因**:那条是启动期 tick 未定位
(已由 `LoadSequencer:156` 修),本条是**会话内读更早的档**。

## 2. 处置(两种,刻意不同)

| 件 | 做法 | 理由 |
|---|---|---|
| 体征链 | `ResetAfterRestore()`:**清空**派生态 + 游标归零 ⇒ 下一个正常边沿按新流**重放** | 它必须从流重建。**不能在此立即重放** —— 重放会走 `PublishDeathCandidateIfTransferred`(写流),等于把同一批转移**再产出一次** |
| 两个完成调度器 | `ResumeAt(restoredTick)`:把门**挪到存档时刻**,`tick ≤ 存档时刻` 视为已处理 | **不得清零重放** —— 重放会把已发过的完成 cue / **已落地的成品再落一次** |

接线位置 = `CompositionRoot` 的 `afterRestore` 闭包(`LoadSequencer` 在读档序列第 1.5 步已调过
`restoreTick`,`afterRestore` 是第 4 步 ⇒ 闭包内 `tickDriver.CurrentTick` 就是存档 tick)。
⚠️ 该闭包建在 `services` 构造**之前** ⇒ 用方法级空壳句柄 `servicesRef` 前向捕获,装配完成后赋值。

## 3. 判据

`unity/Assets/Tests/PlayMode/SkeuomorphicUI/adr041_bak_rescue_walkthrough.cs` 判据 ⑧:

- 场景:玩到 tick 70 → 存槽 3 → 打坏 final → 读回 tick 20 → **关册(模态会停 tick)** → 等时钟推进;
- 断言:`CurrentTick() ≥ 75`(越过读档前的 70),且**全程无任何未处理异常**
  (UTF 默认把 `LogType.Exception` / Error 判红 ⇒ 这条判据不需要额外写断言就有牙);
- 实跑:`[BAK] 读档后世界继续跑:tick=75(须越过读档前的 70)`。

**为什么这条能抓到**:它第一次让「会话内读一份**更早**的档,再让世界继续跑」这条路跑起来 ——
改前本仓没有任何夹具走过它(其它读档夹具都在启动期读,时钟还在 0)。

## 4. 证伪轮(2 变异,各自转红;窗口内只改 `CompositionRoot.cs`)

| # | 变异 | 结果 |
|---|---|---|
| M1 | 摘掉 `vitals.ResetAfterRestore()` | **红**:`InvalidOperationException: 体征链 Step 时间倒流:tick=21 < 上次 70` |
| M2 | 摘掉两处 `ResumeAt(restoredTick)` | **红**:`InvalidOperationException: [炮制] 完成调度收到非递增 tick(21 ≤ 上次 70)` |

还原 = 备份 `cp` 回 + **SHA-256 逐字节一致**
(`19d50eb58568182174e5fc6086d2231ede9de80b144b11cce76c5fde9479260b`),未用 `git checkout`;
窗口内未编辑任何其它文件;还原后 `grep -c FALSIFY` = 0。

## 5. 回归(三元组)

| 套件 | 结果 | 三元组 |
|---|---|---|
| EditMode | `6880/6843/0/37` | 截至 `2026-11-04T19:11:17Z` · sprint-24 TD-sim-05 轮 · `bcc29770` + 工作树 |
| PlayMode | `341/337/0/2/2` | 截至 `2026-11-04T19:12:25Z` · 同批次 |

与上一轮(`6880/6843/0/37` / `341/337/0/2/2`)逐位相同 ⇒ **零回归**(本件加的是断言,不是新夹具)。

## 6. 不借绿

- **「读档后世界能跑」只在走查件这一条路径上被证**:它读的是**手动槽**,且只跑 ~55 tick。
  退出保存 / 自动存档两条读档路径**未单独走查**(它们共用 `LoadSequencer`,`ResumeAt` 与
  `ResetAfterRestore` 都在同一个钩子里 ⇒ 结构上等价,但**没有各自的夹具**);
- **体征链在清空到重建之间会抛 `PatientCourseNotFoundException`**(既有契约):
  呈现侧走 `VitalsQueryOrNone`,窗口 ≤ 一个 tick。**未验证**该窗口内 42 的实际读数
  (走查里那一段时间模态开着,画面没刷新);
- **未做**:其它按实体存 `lastTick` 的量(`CombatAttackEvalPoint` 的冷却表、`EventBudget` 的
  冷却表)—— 它们是**逐 key 冷却**而非全局单调门,读档回退后只会让冷却判定偏向保守,
  **未验证、也未登记**;若将来出现症状,须另立条目。
