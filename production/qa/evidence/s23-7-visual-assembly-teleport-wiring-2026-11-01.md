# S23-7 实施证据 —— 42 侧水墨视觉装配 + 1 侧传送实装 + [L] 走查(2026-11-01)

| 字段 | 值 |
|---|---|
| **对象** | 用户指令「[L] 走查/听测(桌面批)、42 侧水墨视觉装配(表现资产批)、1 侧传送实装(其轮接线)。现在完成」 |
| **轮次** | S23-7(sprint-23 收口后的追加批)· 生产码三件 + 装配接线 + EditMode 扩展 + PlayMode 全链 |
| **结论** | 定向 EM `14/14` · PM `1/1` 全绿;证伪轮 **4/4 恰红**(含一条假绿已修);**[L] 视觉/听觉签核 = NOT-RUN 待用户桌面批**(如实登记,不借绿) |

---

## 1. 本批兑现的三件事

| 用户指令 | 兑现 | 状态 |
|---|---|---|
| **1 侧传送实装(其轮接线)** | `TeleportCommandSink` 生产实装 + 装配接线(29 revive → issuer → sink → `PlayerController.Teleport` → 跨格提交链发 `ActorCellEntered`) | ✅ 生产链通(PM 实测:落格事件实发) |
| **42 侧水墨视觉装配(表现资产批)** | `DeathTransitionPhases`(相位机)+ `DeathTransitionPresenter`(呈现器 + 程序化水墨纹理 + ΦE 纯黑兜底) | ✅ 程序化占位已落;**正式水墨素材仍归美术资产批**(art-bible §4.5) |
| **[L] 走查/听测(桌面批)** | 自动化面已产出(alpha 数值形状 + 相位序列 + 截图能力边界实测) | ⚠️ **人工签核 NOT-RUN** —— 见 §6 |

---

## 2. 落盘件

| 文件 | 内容 |
|---|---|
| `Gameplay.Presentation/Player/TeleportCommandSink.cs`(新) | `ITeleportCommandSink` 生产实装:整数格 → 格心连续坐标 → `PlayerController.Teleport`;**零直接 Append**(`ActorCellEntered` 由控制器跨格提交链发,写者 = 1,AC-29-09 不破);`BindPlayer` 迟绑定,未绑定/读数 null ⇒ **抛**(fail-loud) |
| `Gameplay.Presentation/DeathTransition/DeathTransitionPhases.cs`(新) | 相位机 `DeathTransitionDriver`{Phase, Alpha, Elapsed}:Φ0 Inactive → Φ1 Falling → Φ2 Lying → Φ3 Waking → Φ4 Fading → Φ0;**Φ3 只由 sim 复活落地触发**(不自行醒来);时长常量 Fall 1.2 / Lie 0.8 / Wake 1.5 / Fade 1.0 s |
| `Gameplay.Presentation/DeathTransition/DeathTransitionPresenter.cs`(新) | UGUI 全屏叠层(sortingOrder 900,`interactable=false` / `blocksRaycasts=false` 零交互);程序化水墨纹理(256² 径向渐变 + 确定性值噪声);**生成失败 ⇒ ΦE 纯黑兜底**;零写路径 |
| `Gameplay.Boot/CompositionRoot.cs` / `CompositionRootServices.cs`(改) | 死亡段装配:`TeleportSink` → `RespawnCommandIssuer(teleportSink, null, WorldSpawnCell)` → 消费者 `revive` 回调 |
| `Gameplay.Boot/BootRoot.cs`(改) | 4.3a `TeleportSink.BindPlayer(() => _player)`;4.3b 建 `DeathTransitionPresenter` 并 `Bind(流, 解码器, actorId)` |
| `Tests/EditMode/DeathRespawn/death_presentation_guard_test.cs`(改) | 白名单闭集扩员(相位机 + 呈现器);零写路径扫描扩 4 文件;**新增 6 测**(5 相位机 + 1 传送 sink 守卫) |
| `Tests/PlayMode/death_transition_playmode_test.cs`(新) | 真启动全链:BootRoot 启动 → 写 `PlayerDied` → 相位机渐入 → `RequestTeleport` → 断言 `ActorCellEntered` **实发**且落格 = 命令格 → 相位序列走完回零叠层 |

---

## 3. AC → 载体映射

| AC | 载体 |
|---|---|
| **AC-29-10**(复活来源 `ActorCellEntered` 实发数) | PM:`test_deathToRevive_realBoot_teleportEmitsCellEntered_andPresenterRunsPhases` —— 断言 death 后**存在**落格 = 命令格的实发事件(∃ 判据) |
| **AC-29-09**(29 零 Append) | sink 源码零 Append + 装配面 `revive` 只调 issuer;`ActorCellEntered` 写者恒 = 1 |
| **AC-29-06**(白名单闭集) | EM:`test_whitelist_closedSet_positive_andViolationRed`(闭集扩至 4 员) |
| **AC-29-11/12**([L] 节奏走查 / 听测) | ⚠️ **NOT-RUN** —— 人工执行体,见 §6 |

**PM 实测取证**(日志 `unity/Logs/s237-pm.log`):

```
[S237] teleport 后位置 = (2.50, 0.50, 2.50)(期望格心 (2.5, 0.5, 2.5))
[S237] cell 事件: tick=1 cell=(0,1,0)      ← 出生格
[S237] cell 事件: tick=2 cell=(0,0,0)      ← 重力沉降一格(正常物理)
[S237] cell 事件: tick=11 cell=(2,0,2)     ← 传送落格 ✅
[S237] 消散探针: t=0.0s driver=Waking Elapsed=0.15 测试侧Query.Phase=2 alpha=1
[S237] 消散探针: t=2.0s driver=Fading Elapsed=0.65 测试侧Query.Phase=2 alpha=0.349
[DeathTransition] 全链 OK:pos (0.00, 1.00, 0.00) → (2.50, 0.91, 2.50);格事件 2 条;
                 相位序列 Falling→Lying→Waking→Fading→Inactive 已走完
```

---

## 4. 证伪轮(S12-4 · 同命令 改→跑→还原 + SHA-256 逐字节)

| 变异 | 靶点 | 实测 |
|---|---|---|
| **M-A** 相位机「躺定自动醒来」(`if (simPhase == 2)` → `if (true)`) | `DeathTransitionPhases.cs` | ✅ 恰红 3 条:`test_phases_downed_fadesIn_thenLies` / `test_phases_lying_holdsUntilRevive_noAutoWake` / `test_phases_reviving_wakeThenFadeToInactive`;RESTORE-OK `a3af52b16542` |
| **M-B** 渐入**转位阈值**失守(`Elapsed >= FallSeconds` → `× 10f`) | `DeathTransitionPhases.cs` | ✅ 恰红 3 条:`test_phases_alphaClamped_noOvershoot` / `test_phases_downed_fadesIn_thenLies` / `test_phases_reviving_wakeThenFadeToInactive`;RESTORE-OK `a3af52b16542` |
| **M-C** 传送 sink「绑定读数 null 静默吞」(`if (player == null)` → `&& 1 == 0`) | `TeleportCommandSink.cs` | ✅ 恰红 1 条:`test_teleportSink_unboundRequest_throws_doubleBindThrows`;RESTORE-OK `4e3a8389320b` |
| **M-D** 呈现器源码长出 `IEventSink` 词面(using 别名) | `DeathTransitionPresenter.cs` | ✅ 恰红 1 条:`test_deathPresentation_zeroWritePath_sourceScan`;RESTORE-OK `1ce507497b1f` |

日志:`unity/Logs/s237-fals{A,B,C,D}.{xml,log}`。

> ⚠️ **本表是夹具定稿后的重跑**(2026-11-01 19:19–19:21 CST):首轮证伪跑于夹具初版,
> 其后夹具与生产码经九轮全量回归修出 8 处缺陷 ⇒ **判别力须在定稿夹具上重验**。
> 四变异仍全红,还原哈希为定稿版(见上表)。

### ⚠️ 一条**假绿已修**(F8 形态:行为等价掩盖结构分叉)

**首轮 M-B 变异改的是 `Alpha = Mathf.Clamp01(Elapsed / FallSeconds)` ⇒ 14 测全绿(假绿)。**
根因(结构性,非笔误):`Falling` 支算完 alpha 后,若 `Elapsed >= FallSeconds` **立刻转 `Lying` 并把
`Alpha` 满屏覆盖为 `1f`** ⇒ **钳位那行的取值从不参与终值判定**;且渐入段内
`Elapsed < FallSeconds ⇒ Elapsed/FallSeconds < 1` ⇒ 钳位在本实现里**本就不可越界**。
⇒ 该变异**改的是被覆盖的中间值**(F8),夹具对它零判别力。

**处置(不是改松断言)**:
① 夹具判据点从「转 Lying 之后」前移到**渐入段内**(逐帧取值,要求 alpha **严格递增**且恒 ≤ 1);
② 变异靶点从「钳位那行」改钉在**转位阈值**(`Elapsed >= FallSeconds`)—— 停用后渐入段被拖长 10 倍,
   新夹具的「计满即转 Lying」判据**恰红**。
⇒ 修后 M-B 转红(见上表)。**登记为 F8 实例**:「结构上不可越界的守卫」不能作为反证靶点。

---

## 5. 回归(三元组 · 终值)

| 轮 | 元素 | 值 |
|---|---|---|
| **截至时刻** | | 2026-11-01 **19:17:48 CST**(EditMode)/ **19:18:32 CST**(PlayMode) |
| **批次** | | S23-7(42 视觉装配 + 1 传送实装 + PlayMode 全链) |
| **commit** | | 本批提交(见 git log S23-7) |

| 套件 | 结果 | 前次(story-006 终值) | 等式 |
|---|---|---|---|
| EditMode | `total=6440 passed=6403 failed=0 skipped=37` | `6434 / 6397 / 0 / 37` | `6440 − 6434 = 6` ✅(本批新增 6 测:5 相位机 + 1 sink 守卫) |
| PlayMode | `total=274 passed=270 failed=0 skipped=2` | `273 / 269 / 0 / 2` | `274 − 273 = 1` ✅(新增 1 件全链) |

复算命令:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s237-reg-em.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s237-reg-pm.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
```

### ⚠️ 全量回归修复史(如实登记,不掩盖)

本批全量回归**跑了九轮才全绿**,前八轮各暴露一处**测试夹具/装配缺陷**(均非生产逻辑错误,但都是真实缺陷):

| 轮 | 红 | 根因 | 修法 |
|---|---|---|---|
| 1 | EM 1 + PM 94 | ① `revive` 回调在无玩家的纯 sim 装配里触发 ⇒ sink 未绑定抛;② Presenter 的 `Bind` 闭包在 `_services` 置 null 后求值 ⇒ NRE ×94 | ① `TeleportCommandSink.IsBound` + 装配层跳过发令;② 闭包判空 + Presenter 停更 |
| 2 | PM 洪泛(205 MB 日志) | TearDown 先激活预摆、后拆 CameraRig ⇒ **两 listener 并存窗口** ⇒ `AC-20-10` 每帧洪泛 | 先拆 BootRoot 层级 → 等一帧 → 再动预摆 |
| 3 | PM 洪泛(14 MB) | 自造预摆**只 `SetActive(true)` 不销毁** ⇒ 留到后续所有测试 | TearDown 销毁自造预摆 |
| 4 | PM 1 | `GetComponentInChildren<CanvasGroup>()` 默认不含未激活对象,Φ0 窗口取不到 | `includeInactive: true` + 轮询就绪 |
| 5 | PM 1 | `GameObject.Find("DeathTransitionPresenter")` 同名歧义 ⇒ 拿到别的实例 | 服务袋 `DeathPresenter` 引用(唯一真源) |
| 6 | PM 1 | 按**真实时间**轮询,而相位机吃 `Time.deltaTime`(被前序 `captureDeltaTime` 污染)⇒ 游戏时间推进不足 | 按 `Σ Time.deltaTime` 轮询 |
| 7 | PM 1 | 传送格事件与 `deathTick` **同 tick**,而 Query 判据是 `e.Tick > deathTick` ⇒ 投影恒 Downed | 显式等 tick 前进到 `deathTick + 1` |
| 8 | PM 1 | 清场 `FindObjectsOfType<AudioListener>()` 默认**不含未激活**,前序 CameraRig 漏清 | `(true)` 重载 + 连带清 `CameraRig` 宿主 |

**终轮(9)= 全绿**。⚠️ 这份历史**不是流水账**:它实证了「PlayMode 串跑与单跑**不等价**」——
前 7 轮全部**定向单跑绿、全量串跑红**,是测试隔离缺陷的典型形态。

---

## 6. 已知边界(不借绿)

- **[L] 视觉走查 / 听测 = NOT-RUN(待用户桌面批)**:
  本仓环境**无 Unity 桌面 / 无音频设备**。自动化只覆盖**数值形状**(alpha 序列、相位转移、
  落格实发),**不覆盖**「水墨晕染观感是否到位」「渐入/消散节奏是否舒服」「环境音是否得体」。
  ⇒ **AC-29-11 / AC-29-12 不记绿**,按 story-006 实施注 5 的口径维持 DEFERRED。
  **签核请求见交付报告**(需用户在 Unity 编辑器或桌面构建里走一遍死亡→复活并签核)。
- **截图在 batch 模式不产出**(2026-11-01 实测):`ScreenCapture.CaptureScreenshot` 依赖 game view
  backbuffer;配合 `WaitForEndOfFrame` 时,在无渲染帧的 batch 下**协程永不触发(挂死,单轮挂 10 分钟)**。
  ⇒ 自动化件**零截图**;若需截图取证,须在**桌面编辑器/构建**里人工截取(可落 `unity/Logs/shots/`,
  gitignored 暂存区)。
- **正式水墨素材未落**:当前为程序化纹理(径向渐变 + 确定性值噪声)占位;美术资产批按
  art-bible §4.5 出正式素材后替换 `InkWashTexture.Create()` 一处即可。
- **远端变体(局部水墨标识)未落** —— 归 45 的第二 QoS 装配轮(远端 `IsDowned` 通道)。
- **相机两段式未落** —— 归 `ICameraRig` 机位轮。
- **环境音渐入未落** —— 归 44 的音频装配轮。
- **床格端口 `clinicCellOf` 仍 = null** ⇒ 复活落点恒兜底世界出生点;**24 的运行期查询面未接线**
  (设计规则已落 24 GDD 规则六 —— 挂标性质 = 实现待落,BLOCKED-BY-24 维持)。
