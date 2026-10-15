# Session State — 2026-10-14(**当前阶段 = Pre-Production · Sprint 10 🚧(story-009 ✅ Complete —— 实现轮 + 评审收口同日落;剩 ADVISORY 人工走查批)· Sprint 09 🚧(S1 第七轮 playtest 待用户执行)· Sprint 08 ✅ · M2 形态件 4/4 齐**)

## Sprint 10 进展(2026-10-14)

- **story-009 实现轮 ✅ 代码全落** —— 急救直读通道运行期装配 + 跳过入口接线:
  - **四项用户裁定**(开工前,全按建议):① R-9.5 装载 = `InputSystem.actions`
    (原稿 Addressables 被实测推翻:组 gitignore 不可版本化 + 第二实例违 AC-3-A1);
    ② F 键 = 按住 = 一次动作(按下沿 Arm / 松开沿 EndAction 聚合上行);
    ③ `EmergencyChannelDriver` 顺带实现 `IArmedState`(结清 ModalGate 方向①空挂);
    ④ AC-9-05 带最小 Judge 分支(method==1 ⇒ Skip(false).Result,强度仍走 F-10.4)
  - 新 `EmergencyChannelDriver`(相位搬运 + 聚合映射 + Idle 门 + IArmedState);
    `EmergencyManualDriver.TryAggregated`;`HostEmergencyProcessor` method 分支;
    `ModalPhaseEvaluator.AbortIdleTicks` 提公开常量;42 接口增员 `SubmitEmergencySkip`
    + 路由 + 候选列表 focusable/回调/洪流守护;ADR-025 §① Boot 行窄修订(+`Gameplay.Input`)
  - **验证**:EditMode **3279/0**(基线 3270,+9 新测)· PlayMode **185/0**(基线 175,
    +8 新测;interaction ② / e2e ⑥ 改判按住语义)· 日志 `unity/Logs/story009_*.xml`
  - AC 勾选:9 BLOCKING 自动化全勾(AC-9-08 结构性继承 + 走查面归人工);AC-9-10/11 NOT-RUN
  - **评审收口(同日)**:评审原件 `production/qa/evidence/review-story-009-2026-10-14.md`
    —— **F-1 BLOCKING** 查获并修复:中止判据 `tick - armedTick` 与按住语义致命交互
    (合法长按 ≥ 2.5 s 被误杀零事件)⇒ 改**采样活跃度看门狗**(`_lastActiveTick` 随 Feed
    推进)+ 回归测试(120 tick 持续采样不中止);F-3 笔误已修;F-2 登记(稳度门单键
    不可达,归烘焙轮)。修复后 **PlayMode 186/186 · EditMode 3279/0** 零回归
  - **story-009 → Complete ✅**(AC 9 BLOCKING 全勾;AC-9-10/11 ADVISORY 欠账归人工走查批)
  - **M2 Exit `:169` 翻 `[x]`(sprint-10 复评)**:story-009 落地兑现 sprint-09 S3 预设的
    翻勾判据 ⇒ 复评原件 `production/qa/evidence/m2-exit-recheck-sprint10-169-2026-10-14.md`;
    **sprint-09 S3 三条勾选滞后全部结清**(`:252` 切图冻结为独立推进项)
  - **登记数值轮**:聚合 EdgeTicks 量纲(样本序号 vs 逻辑 tick)与三乘子同批裁

---

## Sprint 05 进展(2026-10-10)

- **T1.1 ✅ Complete**(B 案修订后)—— `PatientSpawner` 运行期调用方接线完成:
  - 新驱动类 `PatientAppearedDriver`(首 tick 即 spawn + 每 400 tick 再一个 + 接线期上限哨 3)
  - 新抽象点 `IPresenceWriter`(Sim.Contracts,解装配方向)
  - `PresenceRegistry` 实现写面接口
  - 组合根注入驱动 + 最小内联病种注册表(1 病种,数值轮未开)
  - `BootRoot.Update` 在 PumpFrame 后调驱动
  - **验证测通过**:PlayMode 病史流含 `DiseaseOnset` + 在场 ≥1 + `SpawnedCount ∈ [1,3]`
  - 证据:`unity/Logs/s5_t1_1_bfix.xml`(首轮 `s5_t1_1_patient_wiring12.xml`)
    - **关键修复 1**:`EventStream.Append` 的 AC-15 在场检查要求 spawn 前 `AddPresent`;测试反射用 `GetField` 而非 `GetProperty`(readonly struct 字段)
    - **关键修复 2(B 案)**:playtest 人工实测「胶囊刷屏 + xyz 钉住」⇒ 病根 = 编辑器 tick 极慢,
      原 400-tick 节奏把可见性绑死在 tick 速率上 ⇒ 首 tick 即 spawn + 上限哨 3
      (不改已裁定的 `TICK_SECONDS = 0.05`;详见下方 B 案修订段)

- **T1.2 ✅ Complete** —— `CaseOpenWriter.TryOpen` 运行期入口接线完成:
  - 新驱动类 `CaseOpenedDriver`(tick 边沿驱动,对在场且未开案的病人自动开案)
  - 扩展 `IPresenceQuery` 添加 `PresentPatientIds()` 方法(sprint-05 T1.2 新增)
  - `PresenceRegistry` 实现新方法
  - 组合根装配 `CaseOpenWriter` + `CaseOpenedDriver`
  - `BootRoot.Update` 在病人出现驱动之后调用开案驱动
  - **验证测通过**:PlayMode 启动后病史流含 `CaseOpened` + 在场病人数 ≥1
  - 证据:`unity/Logs/s5_t1_2_case_open.xml`
  - **关键修复**:6 个测试文件的 `FakePresenceQuery` 需要实现 `PresentPatientIds()`(subagent 批量修复)

- **T1.3 ✅ Complete** —— 急救链接线完成:
  - 新驱动类 `EmergencyAttemptDriver`(tick 边沿驱动,对在场且未触发过急救的病人自动触发一次)
  - 组合根装配 `HostEmergencyProcessor` + `EmergencyAttemptDriver`
  - `BootRoot.Update` 在开案驱动之后调用急救驱动
  - **验证测通过**:PlayMode 启动后病史流含 `EmergencyAttempt` + `EmergencyTreatmentApplied` + 在场病人数 ≥1
  - 证据:`unity/Logs/s5_t1_3_emergency_verify.xml`
  - **病史流完整事件序列**:ActorCellEntered → DiseaseOnset → CaseOpened → EmergencyAttempt → EmergencyTreatmentApplied

- **T1.4 ✅ Complete** —— 边界语义(出界)修复完成:
  - `PlayerController.Move` 添加坠落重置逻辑(playtest bug #1 修复)
  - 坠落重置阈值 = -10f,出生点 = (0, 1, 0)
  - **验证测通过**:PlayMode 玩家传送到远处后 y 回到 1.08(≥0)
  - 证据:`unity/Logs/s5_t1_4_fall_reset.xml`

- **T2.0 ✅ Complete** —— 病人最小可见实体完成:
  - 新驱动类 `PatientVisualSpawner`(tick 边沿读在场集 → spawn 占位胶囊体 + 中性灰)
  - 自持 `PatientVisuals` 根节点(PlayMode 各测试 teardown 只显式销毁自己知道的对象)
  - `BootRoot.OnDestroy` 显式调 `DestroyAll()`(Boot 场景常驻,看不见 World 卸载)
  - **验证测通过**:PlayMode 启动后 Hierarchy 病人视觉实体数 = 在场病人数(1:1)
  - 证据:`unity/Logs/s5_t2_0_patient_visual2.xml`

- **⚠️⚠️ T1.5 引入的真 bug + 修复(AudioListener 泄漏 → Editor 进程被日志洪泛杀死)**:
  - **用户报告**:「程序无法关闭 play,点关闭程序也无法退出」
  - **日志实证**:Editor.log **31868 条** `There are 2 audio listeners in the scene`(一帧一条)
    + 尾部 `Application terminated due to an uncaught exception: Error: write EPIPE`
  - **病根**:T1.5 装配 CameraRig 时 `AddComponent<AudioListener>()`,而 Boot.unity 预摆
    `Main Camera`(**Transform + Camera + AudioListener + URP data**)已有一个 ⇒ 恒 2 个
    ⇒ Unity 每帧一警告;Console 管道超载 → EPIPE → 进程异常终止(主线程卡在写死管道,
    UI 输入进不来 =「点关闭无响应」)。**不是死循环,是日志洪泛**。
  - **顺带查实**:World.unity **无** AudioListener(只有 Directional Light);我先前说它「自带一个」是错的
  - **修复**:① BootRoot 启动序第 5 步**不再加** AudioListener,改为 `SetActive(false)`
    禁用预摆相机(它只持 listener 语义),运行时相机由 CameraRig 自持 ⇒ **单挂点**。
    ② 新增 `AssertSingleAudioListener()` fail-loud —— Unity 对多 listener **只警告不抛**,
    而一条不可抛的警告足以杀死进程 ⇒ 必须由我方变成具名异常。
  - ⚠️ **断言实现两处踩坑(实测订正)**:
    ① `Object.FindObjectsOfType<T>` 只查 **active** ⇒ 预摆已被禁用 ⇒ 得 0,
       把**正确形态**判成错误 → 改 `Resources.FindObjectsOfTypeAll<T>`(含 inactive,挂载数语义);
    ② `Object.FindObjectsInactiveByType` 在本工程 C# 语言版本**不存在**(CS0117)。
  - **PlayMode fixture 义务**:测试须自建预摆 `Main Camera`(Camera + AudioListener),
    否则断言按设计抛(listener=0);teardown 须恢复 activeState 防污染同套件。
  - **回归全绿**:PlayMode 105/105(camera 测试 `audio listeners` 警告条数 = **0**);
    EditMode 3146/0红/1inc/46skip。证据:`unity/Logs/s5_al_fix_camera4.xml` ·
    `unity/Logs/s5_al_fix_playmode_full.xml` · `unity/Logs/s5_al_fix_editmode_full.xml`
  - **残留**:Editor 崩溃后 `unity/Temp/UnityLockfile` 残留(空文件,非真占用,
    但会让下次打开误判)⇒ 已告知用户 `rm -f` 后重开
  - **病根**:编辑器里 tick 推进极慢(300 帧仅 +2 tick)⇒ 原「每 400 tick spawn 一个」把病人可见性
    绑死在 tick 速率上;观察窗口一拉长 tick 累积过 400 就又 spawn ⇒ 看着「越来越多」。
    xyz 钉住同源:tick 不动 ⇒ `MovementFeed` 位移驱动源不动。
  - **诊断探针实测**(已删):`BootRoot` 单实例 / spawner 单实例 / VisualCount 稳定 1 ⇒
    **不是重复装配、不是崩溃**,是 tick 慢。
  - **用户裁定 B 案**(不碰已裁定的 `TICK_SECONDS = 0.05`):① 首 tick 即 spawn 一个;
    ② 接线期上限哨 3(防刷屏,真节奏归 52 后删除)。A 案(改 tick 速率)被否 ——
    改 `TICK_SECONDS` = 9/5/25/7a 四份文档失效 + 重算全部历史事件时间戳与存档。
  - **修复落点**:`PatientAppearedDriver`(firstTick 分支 + `MaxPatientsDuringWiring = 3` + `SpawnedCount` 测试接缝)
  - **回归全绿**:PlayMode 105/105 · EditMode 3146/0红/1inc/46skip
  - 证据:`unity/Logs/s5_t1_1_bfix.xml` · `unity/Logs/s5_bfix_playmode_full.xml` · `unity/Logs/s5_bfix_editmode_full.xml`
  - `BootRoot.Start` 启动序第 5 步装配 `CameraRig`(ADR-020 自建机位,零 Cinemachine)
  - `BootRoot.Update` 每帧 `GetCameraPosition(player)` + `LookAt`
  - **验证测通过**:PlayMode 玩家移到 (3,1,3),相机跟到 (3.00, 3.59, 7.33)(水平偏移 4.33)
  - 证据:`unity/Logs/s5_t1_5_camera_follow2.xml`
  - **关键修复**:首轮红 = 送玩家到界外 (10,1,10),被 T1.4 坠落复位送回出生点 ⇒ 假阴性;改送界内 (3,1,3) 且每帧钉住位置

- **T3.1 ✅ Complete(Phase A 收口)** —— 全量门复跑:
  - PlayMode **104/104 全绿**(+5 新增接线测;T1.1/T1.2/T1.3/T1.4/T1.5 各一)
  - EditMode **3146 passed / 0 failed / 1 inconclusive / 46 skipped**(F-4 基线 3144/0/1/46, +2 = 新增两个 wiring 测在 PlayMode 侧,EditMode 侧 +2 来自既有基线漂移)
  - **修复**:`CompositionRoot` 原硬编码 fallback 注册表 ⇒ VerticalSliceTest 传合成 fixture(9001) 时体征链查不到病种 fail-loud(两条红)⇒ 改 `ResolveRegistry(caller)` caller 优先、fallback 让位
  - 证据:`unity/Logs/s5_t3_playmode_full2.xml` · `unity/Logs/s5_t3_editmode_full.xml`

**Phase A(T1.1–T1.5)全部收口 ✅** —— 病人在场 + 病例开账 + 急救链 + 出界重置 + 相机跟随,运行期链从 0% → 五写者齐备。下一步 = Phase B(T2.1–T2.5 形态件施加)或先提交本轮。

## 🎯 本轮 = M2 接线轮推进方案裁定(2026-10-09 · 四方专家评估 → 用户四裁定 · **方案已落 sprint-04 §Phase 3**)

**用户指令链**:「按甲来,开M2接线轮专项」→ 写 epic 中途改令「去掉 epic,送专家评估」→
四代理(TD/Producer/QA Lead/unity-specialist)并行评估 →「综合四份给方案」→
**「不要为了跑通而压缩任务,要尊重计划目标和里程碑」**(已存记忆 `feedback-milestone-integrity`:
方案以 Exit 判据全集为纲,裁剪只作显式裁定项,不许执行侧自行压缩)→ 用户四裁定:
① **载体 = sprint-04 Phase 3 扩行**(不新立 epic;触及的 epic 只加登记注,Complete 不重开)·
② **范围 = 全保留**(采集腿/019-b/形态件施加全做;超载只在 10-16 检查点显式顺延)·
③ **组合根 = 案 A 新装配 `Gameplay.Boot`** · ④ **质量门 = 5 项 Go/No-Go**。

**四方评估关键事实(独立实测,推翻/加深原认知)**:
- **`IVitalsQuery` 零生产实现**(仅 5 个测试 Fake)—— 「体征变化可测」的**管道终点生产机制不存在**,
  是最大剩余工程量;现有 vertical_slice_test 体征断言跑在 Fake 上 = **不能作证据**(假绿红旗);
- 全库关键符号生产调用点为零:`Initialize`/`new PatientSpawner`/`PrescribeFlow.Prescribe`/
  `DataCorePreloader`/`LoadSceneAsync`/`ITickProvider` 生产实现 —— 工程是「纯函数库+测试」形态;
- **b5 门硬冲突**:黑名单含 `"Sim.Codec"`(装配级无条件红)而 `PayloadEncoder` 住那里
  ⇒ 组合根塞 `Gameplay.Presentation` 永远构造不了编码器(案 A 新装配的依据);
- 缺写者仍 = `CaseOpened` + `ResourceHarvested`(codec/载荷/路由全齐,零 Append 调用者);
- ADR-023 spikes:S1/S3/S4 ✅;**S2(建 World 即触发)/ S6(tick driver 落地即触发)同批补跑**。

**执行序**(sprint-04 §Phase 3 #4–#7):阶段 0 体征链勘察(0.5–1 天,最大估算不确定度)+
双写者二选一 → 阶段 1 装配轮(~10-15)→ 阶段 2 链补全(~10-20)→ 阶段 3 形态件施加(~10-22)→
阶段 4 收口(灰盒表 → 5 项门 → 人工 playtest → M2 Exit 11 条复评 → 7 处登记回填,至 10-23)。

**✅ 阶段 0 体征链勘察已完成(2026-10-09,探索代理实测,要点)**:
- **无 sim 主循环**:`ITickProvider`/`DataCorePreloader` 调用点/`Initialize` 全零;
  `ProgressionEvaluator` 零生产调用 —— 病人出现后**没有任何东西推进疾病**;
- **求值器 = 简化占位**:`ComputeDrugContribution` 恒 0 · Noise/Trend 恒 0 · `signs` 恒空
  (有 tripwire 钉死)· `CatchUp` 只有排序壳;**定点 `Exp` 全库不存在**(GDD F1/F2 依赖,
  `Fix.Pow/Sqrt` 在);
- **投影链三断点**:① 事件→状态 apply 层无(`ProgressionEvaluator` 收 events 但不解码不用)·
  ② `ProgressionResult→VitalsDto` 桥不存在(`new VitalsDto` 仅 Tests)· ③ 查询面零生产实现;
  另:急救两事件 `Patient = PatientId.None`(`HostEmergencyProcessor:86,113`)⇒ 无法按病人归因;
- **数据面从零建**:`disease_registry.json` 不存在 · curve 字段结构在 `DiseaseRegistryEntry` 无家 ·
  无病种 cooked · `CookedCodec`/`AddressablesDataProvider` 无病种装载分支
  (`disease_action_axis` 有作者态+烘焙件但运行期装载面缺);**curve 数值冻结待用户数值轮**;
- **估算修正(承「登记不隐藏」)**:阶段 2 原估 ~3 天**偏乐观** ⇒ 实测最小可证链
  (体征禁 Fake)≈ **7–12 天**(数据面 2–4d + 真曲线/投影 4–6d + apply/域校正/归因 2–3d);
  M2 可玩级(1 病种真实曲线+signs+F4)另计 1.5–2.5 人周;**F3 CatchUp 数周级**,
  建议 M2 显式登记 NOT-RUN 背离。⇒ **10-16 检查点大概率触发**(承「全保留,溢出显式顺延」,
  裁剪不发生在执行侧)。
- **将至裁定项(阶段 2 开工前须裁)**:① 病种曲线数值 —— 合成 fixture(验收口径单独登记)
  vs 等用户数值轮;② 定点 `Exp` 手写 vs 简化 Decay 登记背离;③ F3 CatchUp M2 NOT-RUN 背离登记;
  ④ 急救事件病人归因(涉 10 侧上行链)。

**✅ 阶段 1「装配轮」首批已收口(2026-10-09 · 实现→双评审→13 项修复→复跑绿→1 发变异 · 未提交)**:
- **交付**:新装配 `Gameplay.Boot`(SimTickDriver double 累加器 + CompositionRoot 装配 + BootRoot
  四步启动序)· **全库首个生产 `Initialize` 调用点**(BootRoot 玩家生成,Host/真流/真 encoder/
  IdAuthority id)· Boot→World 最小 additive 场景(永不用 Single;玩家 `MoveGameObjectToScene`
  移入 Boot 防拆序误杀)· World.unity 零 gameplay(地面+光)· Addressable 注册(address=`world`,
  ⚠️ AddressableAssetsData 被 gitignore,注册是本地工作配置)· ADR-025 §① 六→八装配(含
  AssemblyGates Manifest 同批,b3 差集 0)· Boot.unity 挂 BootRoot · b6 扫描面扩至三面。
- **双评审**:代码面 APPROVE(0B+13A)· 测试面 FIX-THEN-APPROVE(2B+8A)⇒ 13 项修复全落。
- **测试**:过滤 10/10 · 全量 EditMode **3069/3022/0 红/46 跳**(基线 +10)·
  变异 `>=`→`>` 恰红 T1 等值边界 · 还原复跑绿。
- **登记不修(6 项)**:`OnPositionSample` 零调用点(跨格写者运行期不发,归 Phase 1 尾/Phase 2)·
  死亡螺旋丢余量 = 已知行为(非确定性背离)· 零 gameplay 构建期扫描(→S2)· b4 面同缺 ·
  命名统一轮 · **门④ PlayMode 冒烟 = 收口轮前置 NOT-RUN**(按门定义排期,禁借绿)。
- **评审原件**:`production/qa/evidence/review-m2-boot-phase1-2026-10-09.md`。
- ⚠️ **未验**:编辑器真机 Play(Boot 四步启动序首次运行)+ S6 spike 实跑 —— 归门④/收口轮。

**✅ 阶段 1 尾「输入→采样接线」已收口(2026-10-09 · 实现→双评审→F1–F8 修复→复跑绿→2 发变异恰中 · 未提交)**:
- **闭合对象**:阶段 1 评审 代码-4(`OnPositionSample` 零生产调用点 ⇒ ActorCellEntered 运行期恒不发)。
- **交付**:`MovementFeed`(帧泵:Move→采样→Advance→逐 OnTickEdge,次序钉死头注)·
  `MovementInputReader`(Keyboard/Gamepad 直读兜底;action 装载器缺失=未来故事,文件头登记)·
  `BootRoot.Update` 薄壳化 · asmdef +`Unity.InputSystem` · ADR-025 §① 引用集回写(补 InputSystem)·
  `movement_feed_test.cs` 4 条(真装配袋)+ 修复轮增 1 条自由落体回归测。
- **PlayerController 两处偏离(动了已 Complete epic,已登记)**:① CharacterController 懒绑定
  (EditMode 不跑 Awake);② **速度记账真 bug 修复**(原式以含 v.y 全模长当当前速 + 水平分量
  不回写 + 落地不清负 y ⇒ 落地残值漂移;修=水平速独立记账 + 落地清 y)。epic 登记注已落
  `production/epics/player-controller/EPIC.md`(Complete 不重开)。
- **双评审**:代码面 FIX-THEN-APPROVE(0B+10A,核过 15 项)· 测试面 FIX-THEN-APPROVE(1B+6 项)
  ⇒ F1–F8 修复全落(reader 出口钳制 · ToMoveInput 出口钳制 · DefaultConfig static 缓存 ·
  fixture 非零 id · 次序颠倒判别断言(复用 CellTransitionDetector 同源判格)· ∞ 分支 ·
  速度变异实证 + 回归测 · InputSystem 虚拟键盘注入)。**F3 连带**:AC-1-27 白名单增列
  `typeof(LocomotionConfig)`(纯常量配置,主会话核零游戏状态;门拒绝用例/守卫均绿)。
- **测试**:过滤 71/68/0/3(3 跳全既有 Ignore)· 全量 EditMode **3074/3027/0 红/46 跳**
  (基线 3073/3026 ⇒ +1=回归测)· **变异 2 发恰中**:旧速度式 3 红 · 次序颠倒恰红 ①③
  CommitTimeCell 断言 · python 恢复复跑绿。
- **登记不修(8 项)**:生产静置骑 y 格界(结构性,挂 EC-1/AC-1-21,门④ PlayMode 实测)·
  急停 Decel/瞬时反转/TurnRate 死配置/接地常数 -0.5 字面核对/MAX_DT(手感轮、调参轮)·
  BootRoot.Update 转发零测(门④)· 手柄虚拟设备零测。
- **评审原件**:`production/qa/evidence/review-m2-boot-p1tail-2026-10-09.md`。
- ⚠️ **未验**:生产配置(地面顶 0 + 出生 y=1.0)静置 Y 翻转噪声 —— 归门④ PlayMode。

**✅ 阶段 2 四项开工裁定已裁(2026-10-09 用户,均按推荐;已回写 sprint-04 #5 行)**:
① 曲线数值 = 合成 fixture(占位非最终,数值轮整表替换)· ② 定点 Exp = 手写整数域(照 FixPow/FixSqrt
先例)· ③ F3 CatchUp = M2 显式 NOT-RUN 背离(非静默)· ④ 急救归因 = 阶段 2 内修(HostEmergencyProcessor
两处 Append 传真实 patientId)。

**▶ 阶段 2「链补全轮」(2026-10-09 开工,「按你的建议开工阶段2」+「依次进行BCD」)**:
**✅ A/E/B/C/D 五批全部收口(2026-10-10 · 已提交推送 `f5879c3` → origin/main,同批携带
登记债码-1…5 + GDD 八项收口 + 登记册建册)**:

**✅ B/C/D 合批收口(2026-10-10 · 三批依序实现→合批双代理恰一轮评审(双 APPROVE 零必修)→
2 处窄修 + 3 发变异(M13 抓出双评审共误判并补测闭合)→ 终态全量 **3182/3135/0 红/46 跳**→ 原件
`production/qa/evidence/review-m2-stage2-bcd-2026-10-10.md`)**:
- **代码面 APPROVE**(0B+6L):核过 15 项(独立 grep 闭合 11 写者+24 豁免=35 支与 yaml author
  吻合 · b7 三红判据真实现 · 门 A/asmdef 全核)。登记:码-1 ε_MIN/R1-30(数值轮)·
  码-2 玩家事件 stamp 与边沿 tick 不同源(接线轮)· 码-3 游标单调无断言 · 码-4/5 b4/b7 门面
  缺 Gameplay.Boot/Sim.Contracts(gate 轮)· 码-6 GDD 注记归属(已修:补登 A+E 原件)。
- **测试面 APPROVED**(2S4 零阻塞):测-1 B 时点 XML 为过滤跑(已被 C/D 全量重证)·
  测-2 b7 豁免 Kind 零覆盖(三重防护设计可接受)。
- **⭐ M13 变异逃逸(本会话最重要发现)**:删游标门 `if(e.Tick>tick)break` 后测试仍全绿 ——
  双评审纸面推演均判「真判」被**证伪**;根因 = Decay 的 Δ<0 归零门冗余吸收(双层防护等价变异)。
  闭合:补 `test_vitalsChain_futureOnset_notRegisteredBeforeItsTick`(未来 onset 不得提前建档)→
  M13b 重放**恰红 1**。**教训:纸面推演对多层门冗余结构性盲,变异实测不可省**(本会话第 3 次实证)。
- **变异实数**:MUT-M10(泵次序反转)恰红 1 ✓ · MUT-M13 初次逃逸(0 红)· M13b 补测后恰红 1 ✓ ·
  恢复复跑 VitalsChain 13/13 绿零残留。
- **终态全量** 3182/3135/0 红/46 跳(+1=D 时点 3181+游标门补测,精确对账)。
- 登记不修 8 项 → **2026-10-10 同日回补**:码-1…5 全部修复 + C 批 GDD 缺口 8 项全落册
  (D-9-K…R)+ D 批豁免复核转监控,随 `f5879c3` 入库;仅剩测-1(措辞)/ 测-2(设计可接受)维持登记。

**✅ A/E 批已收口(2026-10-09 · 双批并行实现→合批双评审→5 项注记修复→合批终态全量
3125/3078/0 红→2 发变异恰中(MUT1 k-round→trunc 红 6 · MUT2 Append→None 红 1)→ 原件
`production/qa/evidence/review-m2-stage2-ae-2026-10-09.md`(2026-10-10 补登 GDD 归属)· 未提交)**:
- A:`Fix.Exp`(ln2 规约+10 阶 Taylor,全整数,MulRaw 唯一宽乘路径;域 Overflow>2135016 raw /
  Zero≤−772244 raw;全域穷举 290 万点标定 ε=4×2⁻¹⁶ 相对+3 LSB 绝对)· 48/48 · 全量回归 3125/3078/0 红。
  **F0 张力已裁**:每步舍入=FixPow 同构(F0 自标「精度取舍归 Gate 待标」),评审修复轮给 F0 加窄注记。
- E:`Process` 签名 +`PatientId`(无默认值)+ 入口 fail-loud(Value<0→AOORE,先于 Judge/Append)+
  两处事件头归因(被施救者,非施予者)· 急保守集 123/119/0 · PlayMode 4/4 · 全量 3077/3030/0 红。
  涟漪注记 3 条(AC-15 有界性真实流生效/高水位计入/45 上行链义务)。
登记不修 4 项(确定性复算测弱判别/10→9 盲区/施救离场语义归批次 C/Process 行长建议)。
**B/C 已交付(2026-10-09),D(最后一批)进行中**:
- B:`RegistrySchema.cs` +215 行(NaturalProgressCurve/RelapseCurve/Scale/SignEntry[]/六通道位域+
  校验 R1-20…29)· fixture DIS_SYNTH_FIXTURE(id 9001,合成占位)· 不造 IDiseaseRegistry(拒绝
  无消费者预留)· DiseaseSimulation 116/116 · 登记 9 项(cooked 装载面/DEATH_THRESHOLD 待数值轮等)。
- C:三断点+主循环闭合 —— apply 层拆两半(解码在 Gameplay.Boot/DiseaseVitalsService,
  状态在 Sim/DiseaseCourse+CourseBook 门 A 内)· 投影桥 position=clamp(Progress/SCALE)整数域·
  **全库首个 IVitalsQuery 生产实装**(未建档抛具名异常)· 泵第二驱动口(OnTickEdge 后逐边沿
  回推 tick)· DrugContribution 接真(Decay 走 Fix.Exp;noise/trend/signs tripwire 保留)·
  禁 Fake 真链测试 15 条 · 全量 **3160/3113/0 红**(+35=B20+C15 精确对账)· GDD 缺口 8 项
  如实登记未发明字段 · 判别力 15 发纸面 · 登记 7 项(b4 门补 Boot 面/9 GDD 轮/数值轮等)。
- D:`CaseOpenWriter.TryOpen`(GDD 规则二前置写者内复核:在场+未开案,具名结果码)·
  `ResourceHarvestWriter.Record`(五字段,Patient=None 不污染高水位)· **b7 WriterExistenceGate**
  (registry author ⊆ 5 面源码扫描写者集 ∪ 24 条具名豁免;三假绿防护:豁免=红/陈旧=红/空集=红)·
  **关键证伪:零生产写者实为 26 支非 2 支**(11 已有+24 豁免+2 新=35 闭合)· AssemblyGates.RunAll +4 行接线 ·
  D 批全量 3181/3134/0 红(+21 精确对账)。

**▶ 批次 F(收口轮)推进(2026-10-10,工作树未提交)**:
- ✅ **F-1** CatchUp NOT-RUN 三处同源登记(GDD §F3 节首 + AC-3/3b/3c 行 + `ProgressionEvaluator` 注)。
- ✅ **F-2** vertical_slice_test 禁 Fake 换真复评:真 `CompositionRoot` 装配 + 真写者 + 生产
  `IVitalsQuery`,**7/7 绿**;修三红(AC-26 潜伏期抑制含处置 ⇒ DoseTick = Incubation 边界恰自然曲线 0 ·
  doseBase=1 直通药力 · case 测漏算 spawn 事件);**变异实测**(`sum += Fix.Zero`)恰红 2(唯二体征断言)
  → python 还原 SHA-256 逐位一致 → 复跑 7/7 绿。
- ✅ **F-3** 灰盒登记表审计 —— 原件 `production/qa/evidence/graybox-register-audit-2026-10-10.md`:
  登记面完备(D1 无漏登 · 4 真形态件 4/4 · D3 预演过);**1 存量违例仍存**(§8.11.5 spike 7 件仍在
  `Scenes/`,10-03 已登记,处置归待裁 L-1)。
- ✅ **F-4 五门全绿**:① 全量 EditMode **3193/3146/0 红**(+2=新门测精确对账;1 inc/46 跳承基线)
  + PlayMode **99/99/0 红**;② b6+引用集 —— **新建 `assembly_gate_runall_test`(2/2 绿,补
  `AssemblyGates.RunAll` 测试侧零直调缺口 = b2 此前无测试证据)** + AssemblyGateB6Test 9/9;
  ③ WriterExistenceGateTest 10/10;④ **新建 `composition_root_smoke_test`(1/1 绿)** —— 根因排查:
  Addressables catalog=10-02 早于 world 注册 10-09 ⇒ InvalidKey,**BuildPlayerContent 刷新**(本地
  Library 产物,不入库 —— 他机/CI 复跑门④须先 build content,已为登记项);⑤ = F-2(vitals 7/7 真路径)。
- ◐ **F-7 6/7**:N-r2(重开条件已到·引用仍 0 待接线)· we+modular story-004(World.unity 已建·扫描归 S2)
  · u1-S2(触发已到)· u0 a7-5(§② 扫描触发待过)· AC-8-51(37 半边就位·[L]/[I] 仍 NOT-RUN)
  · O-1…O-6(复扫:O-1 全闭 · 余三条维持 · O-4 单机侧前置登记注);**余 skeuo 021-024 归 F-5/F-6 后**
  (sprint 行 7 时序:playtest → Exit 复评 → 回填)。
- ✅ **F-5 人工 playtest 完成(2026-10-10)**:报告 `production/playtests/playtest-2026-10-10-m2-manual.md`
  (9 节模板;§1/§3/§4/§7/§9 实质填写)。用户观察:Console 干净 · x/z 随 WASD 正常 ·
  **「y 不受控制地一直变化」已定性 = 走出 10×10 Ground(内置 Plane,±5m)边缘后无限下落**
  (可玩面不足,**非移动/落地 bug**)—— batch 8 版探针证据链:`f5_landing_probe{4..8}.xml`;
  决定性两发 = v6(capture 1/60 静置 240 帧 y 峰谷 0 / 接地 240/240,落地稳定)+ v8(单驱动出界,
  z>5.042 后 y 1.080→-10.088 自由落体)。**顺带落网**:① `LocomotionConfig`
  (SkinWidth=0.01/MinMoveDistance=0/Height=1.8)**未接线**,controller 裸用 Unity 默认
  (0.08/0.001/2)⇒ 浮空 8cm(登记,归移动实现轮);② **方法学**:PlayMode 测 CC 必须
  `Time.captureDeltaTime`(batch 快帧 minMoveDistance 吞帧 ⇒ v2/v5 伪绿),直调 `Move()` 须停
  `BootRoot.Update` 防双驱动互抢(v7 教训)。诊断探针按用户裁定已删(XML 证据留存)。
  Top 3 优先:① 边界语义 ② 相机跟随 ③ 核心循环可达性。
- ✅ **F-6 M2 Exit 复评完成(2026-10-10)**:原件 `production/qa/evidence/m2-exit-recheck-2026-10-10.md`。
  **计数订正**:Exit 实为 **13 条**(非 sprint 行 7 的「11 条」—— 2026-10-08 补的 3 条未计入)。
  **三条核心判决**:① **写者存在性转闭**(原文「9/25/37 写者不存在」是 2026-10-08 旧快照 ——
  实测 `PatientSpawner.cs:77`/`CaseOpenWriter.cs:135`/应急/处方写者齐);
  ② **#2 端到端仍开,但缺口迁移** —— 写者**零生产调用方**(`SpawnNext`/`CaseOpenWriter.TryOpen`
  全库唯一引用 = 测试)⇒ 运行期链可达性 0%(与 playtest §6 互证,归阶段 3 接线轮);
  ③ **playtest 口径裁明** = 至少一次人工执行,F-5 已满足。三条判决已就地回写 README(不删原文)。
- ✅ **F-7 收尾 7/7(2026-10-10)**:skeuo 021-024 回填 = **「4 项形态件」条的施加点口径锁定** ——
  实测**零个形态件在运行期被施加**(`SkeuoRuntimeDriver` 两 TODO 仍是空壳;playtest 人工面 0% 独立互证)
  ⇒ 回填后判据口径:**该条 `[ ]` 的唯一剩余内容 = 施加点接线**,形态半 4/4 **已闭不重开**
  (施加点归属 story-024→9 查体链+数据绑定轮 / story-023→10 急救轮,即 sprint 行 6「阶段 3 形态件施加轮」~2 天)。
  另同步回刷 sprint-04:行 7 阶段 4 → ✅ Complete(含「11 条→13 条」计数订正)+ §不得借绿段 Exit 读数整段重写。
- ✅ **批次 F 七项全闭并提交推送(2026-10-10 · `e38132a` · 24 文件 +1081/−366)**:F-1 ✅ · F-2 ✅ · F-3 ✅ · F-4 ✅ · F-5 ✅ · F-6 ✅ · F-7 ✅。
- ▶️ **阶段 3 开工(2026-10-10 · sprint-05 已落盘)**:范围裁定 = **合并做**(形态件施加 + 运行期接线)。
  `production/sprints/sprint-05.md`:Phase A 运行期接线(SpawnNext/TryOpen/急救链/出界重置/相机跟随)→
  Phase B 形态件施加(SkeuoRuntimeDriver × 2 + SignChannelBinder 挂真 + 023 载体 + 021/022 行级)→
  T3 验证(全门 + playtest 第二轮 + M2 Exit 剩余条)。**先接线后呈现**(施加点依赖运行时链路)。
  **下一步 = Phase A T1.1 PatientSpawner 运行期调用方**。

**✅ 提交推送完成(2026-10-10 · `6a01d73..f5879c3` → origin/main)**:提交前全量门
**3191/3144/0 红/1 inc/46 跳**(inc=Audio `test_monoOption…` + 46 跳均承基线,与本批无关);
55 文件,+4705/−102;排除项(.gitignore 本地改动 / .trae / mono_crash blob /
`__pycache__.meta`)留工作树不入库。
**下一步 = F-5 人工 playtest(请用户)→ F-6 Exit 复评 → F-7 收尾 → 批次 F 提交**。

**✅ 登记债总册已建(2026-10-10 ·「清理登记债」指令)**:`production/registration-debt-register.md`
—— 全案未结登记债唯一真源(A 类 M2 阶段 2 十四项 · B 类 O-6 一组 · C 类 GDD 内联四项 ·
D 类散落六项 ≈ 25 项),每项带归属轮;已结项留审计轨迹不删。GDD / active.md 的「登记债」
字样自此指向本册。

---

## ✅ 前轮 = O-6 观察项修复轮(2026-10-09 · 评审→修复→6 发变异全中→复跑全绿 · **已收口** `f148fab`)

**一条观察项**(O-4/O-5 轮代码面 F2 登记,本轮兑现):
- **O-6 两写者绕过 encoder + b6 门只扫 `Sim/`**: `PlayerController` / `CellTransitionDetector`
  手搓 `PayloadRef(cell.X,cell.Y,cell.Z)` 伪引用(零字节进池、无 actor 身份)⇒ O-4 条件键
  (`Seq<0` 补 `BlobId/Offset/Length`)下联机两 actor 同 tick 同格 **PayloadRef 同值**,
  第二条被 `EventStream` 去重吞掉。
  **修法三条**:① 载荷编码唯一路径 = `IPayloadEncoder`(ADR-029 §③ ——
  `ActorCellEnteredPayload{ActorId,Cell,Tick}` → 每次 `Encode` 发新 BlobId ⇒ 条件键可区分两 actor);
  ② asmdef 边界不破 —— `Gameplay.Presentation` **只持接口**、不引 `Sim.Codec`
  (`motor_lease_test` 白名单 + `IsInitOnly` 钉住);③ **b6 门扩扫 + 可测化** ——
  扫描面提为 `PayloadRefScanDirs = {Assets/Sim, Assets/Gameplay.Presentation}`(字段可反射断言),
  扫描体抽目录形参 `CheckPayloadRefCallsitesIn(errs, dirs)`(工程外临时目录负向探针,
  验递归深度 / 入口接线 / 剥注释三面)。

**双评审(恰一轮)已返回**:代码面 **APPROVE**(0B+9A,核过 12 项)·
测试面 **FIX-THEN-APPROVE**(1B+9A)⇒ **评审后 6 项修复 + 1 项双写消解全落**:
① BLOCKING `test_b6_entryUsesScanDirs_andDirsExist`(入口接线负向证明 + 扫描面目录存在性,
杀 C′「入口改硬编码却全绿」);② 探针改子目录 `tmp/Sub/`(兼验 `AllDirectories`);
③ controller 双 actor 同格对偶测;④ `_actorId.IsInitOnly` 结构断言;
⑤ client 池断言出口条件注(F1 聚合上行须换判据);⑥ b6 门头 O-6 扩面注(代码-1);
⑦ O6 扫描面断言改**不同维度**(恰两面 + `Distinct` 去重)消解与 b6 测试的判据双写。
**新观察项 O-7(登记,下轮)**:`occupancy_overlay_test.cs:37,45,126-139` **伪引用自洽环**
(测试自造 `PayloadRef(blobId: structureId,…)` + 自己按 `BlobId` 读回,只测夹具不测生产;
生产 `StructureKinds` 已走 encoder)⇒ 归 O-7 轮改真 `PayloadCodec` 解码。
**登记不修(11 项)**:扫描面未含 `Gameplay.UI` 等 · 块注释剥除行号下偏 · `EndsWith` 豁免偏松 ·
重编码重发去重 → 45 轮(承 O-4 F1)· 组合层零接线 → 接线轮 · 同 actor 双写者二选一 → 接线轮 ·
重复 `<summary>`(既有)· 文件系统探针 = **豁免型** · 多 actor×多 tick 真流有界性 ·
存档/网络 blob 重映射 round-trip(7a/45 轮)。

**测试证据(终态)**: 过滤 fix2 **79/78/0 红/1 跳** · 全量 EditMode **3059/3012/0 红/46 跳/1 inc**
(+10 于 O-4/O-5 基线 3049/3002,零回归)· PlayMode **98/98/0 红** · 变异 **6 发全中**
(A 11 红 · B 2 · C 2 · D 1 · E 3 · F 1 —— F = 题面 C′「入口改硬编码」正面闭合 BLOCKING)·
净态:三生产文件哈希匹配 + `MUT-` 残留 0 + postrun 过滤复跑 79/78 绿。
判定链已收口:**APPROVE**(原件 §六)。
**评审原件**: `production/qa/evidence/review-o6-fixes-2026-10-09.md`(§一 双判定 ·
§二 修复+设计论证 · §二之二 登记不修+O-7 · §三 验证命令 · §四 6 发变异 · §五 实数 · §六 APPROVE)。

---

## ✅ 前轮 = O-4/O-5 观察项修复轮(2026-10-09 · 评审→补测→5 发变异全中→复跑全绿 · **已收口** `18a6aad`)

**两条观察项**(上轮代码面评审 ADVISORY #4/#5 登记,本轮兑现):
- **O-4 去重键坍缩**: `EventStream` 未发号事件同 (Kind,Patient,Tick) 静默合并
  ⇒ 改**条件键**:未发号(Seq<0)补 `PayloadRef(BlobId,Offset,Length)` 身份;已发号维持
  四元组(Seq 即身份,重编码免疫)。**设计三条**:发号后键破幂等 · Sim 门 A 读不了
  payload 内容(只能取 ref)· 已发号补载荷会被重编码击穿(假阴性)。
- **O-5 CAP 误拒世界事件**: `PatientId.None` 世界事件(结构×3/POI/玩家跨格/急救两支)
  满 24 被拒 ⇒ CAP 分支加 `e.Patient != PatientId.None` 守卫;真实 id 写者
  (DiseaseOnset/DrugTreatmentApplied)语义不变。
  **登记边界**:真实 id 非病人实体(玩家/敌人共用 id 空间)满 CAP 仍被拒 —— 归 25 写者/接线轮。

**双评审(恰一轮)已返回**:代码面 **APPROVE**(0B+4A)· 测试面 **FIX-THEN-APPROVE**(2B 补测+2A)
⇒ **补测/注释全落**:① `test_dedup_explicitSeq_payloadBlind_andSeqDistinguished`(一测三面,
杀 M1 恒真/M2 丢 Seq);② `test_o5` 并入 CAP 满 × 在场病人断言(杀 M3 删 !IsPresent);
③ `test_clear_resetsDedupKeysAndSeq`;④ 文件头条件键订正 + CAP/去重次序警示行。
**边界声明(F1)**:未发号重发幂等边界 = 同一 PayloadRef;重编码重发归写者幂等/45 意图层。
**新观察项 O-6(登记)**:`ActorCellEntered` 两写者绕过 encoder 手搓伪 ref ⇒ 联机同格坍缩
(根因先于本轮;归 encoder 接线轮 + b6 门扩扫 Presentation)。
**登记不修**:测试 F-4(CAP/去重次序→45 轮)· dose_seq 五元组键未实现(处置去重轮)。

**测试证据(终态)**: 过滤 **49/49** · 全量 EditMode **3049/3002/0 红/46 跳/1 inc**
(+5 于基线 3044/2997,零回归)· PlayMode **98/98/0 红** · 变异 **5 发全中**
(评审前 O4/O5 + 评审后 M1/M2/M3,各恰红 1;净态零残留)。
判定链已收口:**APPROVE**(原件 §六)。
**评审原件**: `production/qa/evidence/review-o4o5-fixes-2026-10-09.md`(§一 双判定 ·
§二 修复+设计论证+F1 边界声明 · §二之二 评审后修复+O-6 · §四 5 发变异 · §五 实数 · §六 APPROVE)。

---

## ✅ 前轮 = O-1/O-2/O-3 观察项修复轮(2026-10-09 · 评审→修复→变异→复跑全绿 · **已收口** `4b5aae6`)

**用户指令**:「继续修复新登记观察项123」→「开这三个修复轮」。

**三条观察项与修法**(源自 review-disease-onset-writer §七):
- **O-1 Seq 哨兵冲突**: `EventStream` 的 `e.Seq == 0` 与 `_currentSeq` 首值 0 冲突
  ⇒ 哨兵改 **-1**(0 是合法首号值)。落点:`SimEvent.cs` doc · `EventStream.cs:90` ·
  **10 处生产调用方** Seq 实参 0→-1 · `event_stream_test` 4 组 + 新增 o1 哨兵测试 ·
  `vertical_slice` 2 处。**保留既有语义**:去重键用入流 Seq ⇒ 同 (kind,tick,patient)
  未发号重发幂等去重(既有行为,测试已按此落)。
- **O-2 IPresenceQuery 无生产实装**: 新建 `Gameplay.Presentation/PatientAI/PresenceRegistry.cs`
  (读面 IPresenceQuery + 写面 Add/Remove/Move/ResetForLoad;同格不夺格;Move 不隐式入场)。
  在场集 = 派生态,组合层灌入;13 侧接线归组合层(P0 无组合根,登记为后续义务)。
  新测试 9 条(含 **CAP 三方交互**:灌满 24→SpawnNext 抛→回滚→重发同号)。
- **O-3 先发号后 Append 空洞**: 新接口 `IRollbackableIdAuthority`(Sim.Contracts,
  可选能力)· `IdAuthority` 实现(仅最后号可回滚;与机制 A 不冲突 —— 回滚号从未进流)·
  `PatientSpawner` try/catch 回滚后 rethrow。新测试 3 条。

**双代理评审(恰一轮)已返回,均 FIX-THEN-APPROVE**:
- 代码面 1B + 8A · 测试面 3B + 8A(判定表已录原件 §一)
- **修复全落**:① BLOCKING 甲案 = `PresenceRegistry` 占格/在场分离(`SetOccupant`/
  `RemoveOccupant` 不计 CAP;`IPresenceQuery` 契约不动)+ 玩家占格用例;
  ② B1/B2 补 Move 两分支测 · B3 负哨兵守卫 `id.Value<0` + 测试;
  ③ A1/A2/A3 补测 · #2 原子性前提注 · #6 `event PresentChanged` + 测试 · #7 回执 Seq 注;
  ④ **登记不修**:新观察项 **O-4**(去重键发号前取 Seq,StructureKinds 同 tick 两结构坍缩 →
  事件流/建造轮)· **O-5**(CAP 判据对 None 世界事件生效,满 24 拒世界事件 → 组合层接线轮)。

**测试证据**:
- **终态**:过滤 **44/44** · 全量 EditMode **3044/2997/0 红/46 跳/1 inc**(+21 于基线
  3023/2976,零回归)· PlayMode **98/98/0 红**(= 基线)
- 变异 **9 发全中**:MUT-O1/O2/O3 各恰红 2;评审后 6 发(B1/B2/MUT-1/A1/B3/A3)恰红
  1-2 条(A1 超集 2);净态零残留(锚点各 1)
- 判定链已收口:**APPROVE**(原件 §六)

**⚠️ 硬教训(本轮踩坑)**: 手写 `.cs.meta` 用 64 位 hex guid ⇒ **Unity 静默忽略整个文件**
(Editor.log「does not have a valid GUID … ignored」)⇒ PresenceRegistry 9 条测试假缺席
(过滤 27 全绿的假象)。**meta guid 必须 32 位**(`uuid.uuid4().hex`);
判「是否被编译」看全量 XML 的 discovered 计数,不能只看绿。

**评审原件**: `production/qa/evidence/review-observation-fixes-o1o2o3-2026-10-09.md`
(§一 判定 · §二 修复落点 · §二之二 评审后修复+O-4/O-5 登记 · §四 变异 · §五 终态实数 · §六 判定链 APPROVE)。

---

## ✅ 前轮 = ADR-030 §Migration Plan 步 2/4 —— 9 的 DiseaseOnset 写者实现(2026-10-09 立 · 已跑绿 · 已收口)

**用户指令**:「继续」(承前轮「按建议来」选 A —— 9 的疾病模拟核心最小框架)。

**交付件**:
- `unity/Assets/Sim/DiseaseSimulation/PatientSpawner.cs` —— 9 的 `DiseaseOnset` 写者(ADR-030 §③)。
  注入 `IIdAuthority` + `IEventSink` + `IPayloadEncoder` + `ulong WorldSeed`;
  `SpawnNext(diseaseId, tick)`:入参校验 → 发号 → `SplitMix64.Hash(worldSeed, patientId)` 派生 seed →
  编码 → `Append`。**`Sim` 引用集一字未改**(只加 `Sim.Contracts` 的 `using System`)。
- `unity/Assets/Tests/EditMode/DiseaseSimulation/patient_spawner_test.cs` —— 7 条 EditMode 测试
  (5 原有 + 2 新增入参校验)。
- `unity/Assets/Tests/PlayMode/vertical_slice_test.cs` —— **病人腿由 NOT-RUN 桩转真验证**
  (驱动 `PatientSpawner` + 真 `EventStream`);`fullCoreLoop` 三条事件链(出现→诊断→治疗);
  补 header Seq 发号断言。

**双代理评审(恰一轮)**:代码面 APPROVE(0B+4A)· 测试面 APPROVE(0B+4A)⇒ 去重 6 项。
评审原件 `production/qa/evidence/review-disease-onset-writer-2026-10-09.md`。

**同批修复(6 项)**:A1(码)先发号后 Append → 注释+登记 O-3(空洞不可自愈,当前不可达)·
A2(码)载荷 seq 注释订正 · A3(码)入参校验 · A4(码)ctor doc comment ·
A1(测)patient_seed 注释订正 · A2(测)补 header Seq 断言 · A3(测)补 2 条校验测试。

**测试证据(终态 · 修复后净态)**:
- 过滤 `PatientSpawnerTest` **7/7 Passed/0 红** `unity/Logs/editmode_spawner_final.xml`(基线 5/5 +2)
- 变异 **4 发全中**:MUT-1(去掉 hash)→ 恰红 1 条;MUT-2(不写事件)→ 恰红 4 条;
  MUT-3(删入参校验)→ 恰红 2 条(修复前逃逸);MUT-4(删 Seq 断言)→ 恰红 1 条(修复前逃逸)。
  逐发 python 反向恢复,终态零残留。
- 垂直切片 **7/7 Passed/0 红/0 跳** `unity/Logs/playmode_slice_final.xml`(基线 7/7/0/0 +0)
- 全量 EditMode **3023/2976/0 红/46 跳/1 inc** `unity/Logs/editmode_full_20261009_spawner.xml`
  (基线 3021/2974 +2,零回归)
- 全量 PlayMode **98/98/0 红/0 跳** `unity/Logs/playmode_full_20261009_spawner.xml`
  (基线 98/98/0/0 +0,零回归)

**⚠️ 新登记的架构缺口(未结 · 待裁)**:
- O-1: `EventStream:90` 的 Seq 哨兵 `e.Seq == 0` 与 `_currentSeq` 首值 0 冲突 ⇒ 断言无法区分
  「发了 0 号」与「没发号」。候选 = EventStream 修复轮 / ADR-006 Amendment 轮。
- O-2: `IPresenceQuery` 只有读面、无写面 ⇒ 生产路径下 `PresentCount` 恒 0 ⇒
  `PATIENT_APPEARANCE_CAP`(24) 永不触发。测试侧以 `FakePresenceQuery.AddPresent` 代偿。
  候选 = 9 / 表现层注入点。
- O-3: `PatientSpawner.SpawnNext` 先发号后 Append ⇒ 若 Append 抛异常(如 CAP 满),
  ID 空洞且 `IdAuthority.NextPatientId = _nextPatient++`(不扫流)不可自愈。
  当前不可达(`PresentCount` 恒 0),真正修法 = 发号时机后移,需改 `IIdAuthority` 契约。
  候选 = 9 实现轮 / IIdAuthority 契约修订轮。

**⚠️ 未结(归各自轮)**:47 的 `causes[]` 落地 · kindgen 重跑 · O-1/O-2/O-3 归属。

---

## ✅ 前轮 = ADR-030 病程 onset / 病人出现的 Kind 归属(2026-10-09 立 · **已落盘 Accepted**)

**用户指令**:「走 A,先把「病人出现」该写什么 Kind 查透」→「**按建议来,落 ADR**」。

**勘察结论(已呈现并获裁定)**:34 支 `SimEvent.Kind` **无一表示「病人出现 / 病程 onset」**,
而三处权威件引用它(9 规则六 `:162` 病史流第一行 / `:174` 边界表「病程类(onset …)」/
7a F-7a-4 `:161` 折叠行首字段 `onset`)⇒ **真实 registry 缺口**(同 `CompoundTriggered` /
`ActorCellEntered` / ADR-022 关卡工具的「引用却无登记」失效模式)。旁证:垂直切片测试病人腿
(`vertical_slice_test.cs:6/78/83`)把「病人出现」**误标为 `InjuryOnset`** —— 而后者是 25 的
战斗伤害结算(`actor_id`/`target_id`/`injury_id`/`magnitude`/`dose_seq`),载荷与语义完全不同。

**四项裁定(用户「按建议来」)**:① 「病人出现」与「病程 onset」= **同一个事件**(甲案,不立第二个 Kind)·
② 立一个 Kind `SimEvent.Kind.DiseaseOnset` · ③ 写者(Append 调用者)= **9** · ④ 落**病史流**。

**交付件**:`docs/architecture/adr-030-disease-onset-kind.md`(329 行,格式对齐 ADR-027)。
**载荷** = `(onset_tick, disease_id, patient_id, patient_seed, seq)` —— 逐字对齐 9 规则六 `:162`
并**补 `patient_id`**(7a F-7a-4「`patient_id` 必留」;ADR-006 Amendment B 高水位重构依赖)。
**有界性** ≤ 病人创建率 ≤ `PATIENT_APPEARANCE_CAP`(24,`TR-disease-021`),与 tick 频率无关。
**47 的 onset 义务**(9 GDD `:436` 神经衰弱 `causes[]`)= 义务方/入向数据引用方,`Append` 调用者**恒为 9**。

**同批落盘(6 件涟漪)**:
- `design/registry/entities.yaml` 追加 `SimEvent.Kind.DiseaseOnset` 条目(三字段齐,ADR-024 ①)⇒ **Kind 34 → 35**
- `docs/architecture/tr-registry.yaml` 追加 `TR-disease-024`(covered,`adr: ADR-030`)⇒ **ID 501 → 502**
- `docs/architecture/traceability-index.md` + `requirements-traceability.md` 计数回刷
  ⇒ **344 ✅ / 73 ⚠️ / 68 ❌ / ◆17**(原 342/74/69/◆17)
- `docs/architecture/architecture.md` 第十四次动 · `control-manifest.md` ADRs Covered(26 → 27 份)
- `.claude/docs/technical-preferences.md` ADR 日志(001–030)
- `adr-024` V-2 / `adr-029` R4 加注(34 → 35,承 adr-006「加注不改写」先例)

**⚠️ 未结(不在本 ADR 裁决面,均归各自实现轮)**:9 的写者实现(`IIdAuthority.Next()` → `Append`)·
47 的 `causes[]` 落地 · 垂直切片测试病人腿由 `InjuryOnset` 订正为 `DiseaseOnset` · kindgen 重跑。
**未跑 kindgen / 未跑 Unity 测试** —— 本批纯文档 + registry 登记,零代码改动。

---


## ✅ 本轮 = story-024 M2 形态件④ 一条真实状态反馈通道(2026-10-09 立 · 双代理评审修复复跑全绿 · **收口提交中**)

**流程对账(用户指令:创建+unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送;评审恰一轮)**:
创建+测试 ✅(首跑 239/226 一红,修 null 档误断言后绿)→ 变异 6 发全中 ✅ → **双代理评审恰一轮 ✅**
(代码面 FIX-THEN-APPROVE 1B+2A · 测试面 APPROVE 2A)→ **5 项同批修复 ✅** → **补变异 2 发恰红 ✅**
(MUT-7 极性硬编码 / MUT-8 空串按 null)→ **复跑绿 ✅**(过滤 239/226/0/13 + 全量 EditMode 3016/2969/0/46/1inc
+ PlayMode 98/97/0/1,零回归)→ 提交推送(本步)。

**交付件**:`SignChannelBinder.cs`(体征词条→五通道分发纯函数:F-8.2 向尾回退 · 并列保序 · 非法枚举 fail-loud)·
`SkeuoPaper.uss` `.channel-reading`/`.channel-reading-negative`(阳性满墨/阴性降档,:1185 零标注)+ 头注 ·
`sign_channel_binder_test.cs` 三条 AC · 评审原件 `production/qa/evidence/review-story-024-form-item-4-2026-10-09.md`。

**关键裁定(边界五条)**:只交通道半(运行时挂行归 9/数据绑定轮,`AddChannel` 本轮不改)· 映射归 8 不越界 ·
文案归 8(未查复用 `.empty-row`,零新增未查态类)· 把握不足曲线归数值轮(只出类对)· 真表数值不动。

**⚠️ M2 形态件 4/4 齐 = 形态半齐,非可 playtest 真通道** —— 施加点(④ 的运行时挂行 = 9 查体链;
③ 的候选列表载体 = 10 轮)未接,milestones 整条**保持 `[ ]`**(禁借绿)。

---

## 前轮 = story-023 M2 形态件③ 急救零数字+可跳过(2026-10-09 立 · 已收口提交 `82486f8`)

**流程对账(用户指令:创建+unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送;评审恰一轮)**:
创建+测试 ✅(首跑即绿 236/223/0)→ 变异 4 发全中 ✅ → **双代理评审恰一轮 ✅**(代码面 1B+4A ·
测试面 1B+7A)→ **13 项同批修复 ✅** → **补变异 2 发恰红 ✅** → **复跑绿 ✅**(过滤 236/223/0 +
全量 EditMode 3013/2966/0/46/1inc + PlayMode 98/97/0/1,零回归)→ 提交推送(本步)。
评审原件 `production/qa/evidence/review-story-023-form-item-3-2026-10-09.md`。

**修复要点(13 项)**:BLOCKING ① story AC「全量零回归」时序虚报收窄(全量归 DoD,原全量 XML 系
022 时段产物)② 023-1 属性集 ⊇ 点名六属性 + 逐声明值 `var(--skeuo-`(原「≥5 计数」三分支假绿:
删 background-color 行 / `color: black` 具名色 / 异命名空间 var —— 补 MUT-5/6 恰红实证)·
头注锚改紧邻选择器注释组(原从全文件首 /* 起圈靠巧合收得紧)· 补 owner 关键词断言 ·
`PropsOf` 行首锚(消 `url(guid:)` 幻影属性)· X/N 扫描白名单口径登记(禁整体放宽)·
Registry 零注册守卫(边界④)· 全库声明块恰一(后置覆盖复活面)· AB-3 宽半归 10 轮登记 ·
删未用 using · 相对路径报错。

**判别力 6 发**:内联 hex→023-1+C4涟漪 · content:"3/5"→023-2+C4涟漪 · background-image→恰023-3 ·
border 交集→恰023-3 · **删行→恰023-1(BLOCKING 修复)** · **color:black→恰023-1(BLOCKING 修复)**;
逐发 python 反向恢复,终态零 MUT 残留,净态 +20 行。

> 判据权威 = art-bible **G2**(反数值化 —— 灰盒「色块+数字」自证反数值化不需要做)+
> emergency-procedures 规则六/六之甲(`O-10-1` 跳过入口 owner = 42)+ `milestones:84` ③。
> 边界裁定五条(勘察轮定):只交形态半(施加点/载体/Idle 时序归 10 轮,AB-3 宽半同归)/
> 零数字 = USS 结构面(文本内容层归 UXML/C# 后续轮,story 如实登记)/ VR = P1b 不进 /
> 不进 Registry(语义样式类同 .ruled)/ 焦点单槽铁律(零 background-image 零 border-*)。

### 交付物(三步)
1. **步① 跳过入口元件**:`SkeuoPaper.uss` +`.skip-entry`(aged 纸底 + ink 墨字 + 行高/缩进全 var;
   零新变量零新图);头注 = 规格本体(O-10-1 / owner=42 / 零施加点 / 单槽铁律 / 零数字)
2. **步② 零数字门(G2)**:`test_ac023_2` 剥注释后全库 Skeuo *.uss 零 `X/N`(\d+\s*/\s*\d+)
   + 零 `content:` 含数字(数字角标唯一 USS 载体;现状全库零 content ⇒ 新增即红)+
   `.skip-entry` 块复查;白名单口径登记(合法出现须显式白名单,禁放宽正则)
3. **步③ 焦点落点形态**:`test_ac023_3` min-height 锚 row-height(=44 AB-3 高半)+
   零 background-image(单槽铁律)+ 属性集 ∩ `.focus-visible` = ∅(动态解析;
   `.ruled` border 冲突 = story-021 已登记别案,本类不新增同类)

### 测试证据(终态)
- 过滤 **236/223/0 红/13 跳**(= 基线 233/220 +3)`unity/Logs/editmode_skeuo_023_final.xml`
- 全量 EditMode **3013/2966/0 红/46 跳/1 inc**(基线 3010/2963 +3,逐项一致)`editmode_full_20261009_form03.xml`
- 全量 PlayMode **98/97/0 红/1 跳**(逐数 = 基线)`playmode_full_20261009_form03.xml`
- 变异 6 发恰红(4 初轮 + 2 修复补验),零 MUT 残留

### 同批文档回刷
EPIC 五处(头部 Stories / Stories 表 023 行 / Counts 22→23 total(22 Complete+1◐) / Next Step 3/4 /
里程碑归属 023 行)· `milestones:131` 形态件条 ◐ 注加③(整条仍 `[ ]` 禁借绿)·
sprint-04 `形态件 3/4` · epics/index 行 11 加 023 · story-023 AC/DoD 实数回填。

## ✅ 上轮 = story-022 M2 形态件② 墨乾湿两态(2026-10-08 立 · 双代理评审修复复跑全绿 · 已提交 `a39d60b`)

**流程对账(用户指令:创建+unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送;评审恰一轮)**:
创建+测试 ✅ → 变异 4 发全中 ✅ → **双代理评审恰一轮 ✅**(代码面 FIX-THEN-APPROVE 1B+3A ·
测试面 FIX-THEN-APPROVE 1B+10A)→ **12 项同批修复 ✅** → **补变异 2 发恰红 ✅** →
**复跑绿 ✅**(过滤 233/220/0 + 全量 EditMode 3010/2963/0/46/1inc + PlayMode 98/97/0/1,零回归)→ 提交推送(本步)。
评审原件 `production/qa/evidence/review-story-022-form-item-2-2026-10-08.md`(原判定→修复落点→验证命令→变异 6 发表)。

**修复要点(12 项)**:BLOCKING ① 原件落盘 ② `ExtractThemeColor` **剥注释+钉唯一活声明**
(注释掉声明保留原文 = 抽死文本假绿,补 MUT-5 实证修复前假绿/修复后恰红)·
公式锚定扩 白/黑/128灰 + 纯通道 0.2126/0.7152/0.0722 + 色相 红0°/绿120° ·
`GreaterOrEqual`→`Greater`(story ≥→> 同步)· guid 锚 `background-image: url()` 全形 + `.meta` 内值比对 ·
冻结记录断言收 `freeze-v1` 围栏(照 C8 同款正则+容忍行内空格)· `color:` 恰 1 处防后置覆盖(补 MUT-6 实证)·
12° 注释补近灰 LSB ≈12°/LSB 分解口径 · `Coverage` fail-loud 全图 a==255 ·
Registry 零注册守卫(边界裁定 1)· story:52 `0.0093`→`0.0087` · 边界裁定 4 收窄(干不锚/湿锚 §4.1 权威)。

**判别力 6 发**:dry 色漂→022-1红 · dry 升明度→022-1红 · guid 对调→022-2红 · png 字节对调→022-3红 ·
注释掉 dry 声明→022-1红(BLOCKING 修复)· 后置 `color: red`→022-2红(恰一断言);
逐发 python 反向恢复,终态零 MUT 残留,复跑复绿。

> 判据权威 = art-bible **§4.5 墨龄**(湿洇开→干色沉,只走明度轴不色相漂移;两值 hex 系提案)
> + **G4** 手感本体;边界裁定五条(勘察轮定):状态类非注册变体 / 只交形态半(切换时机归数据绑定轮)/
> 洇开烘归资产轮 / 锚定口径分置(湿=§4.1 权威可锚,干=提案不锚) / 12° = uint8 量化噪声结构界(提案)。

### 交付物(三步)
1. **步① 两态主题色**:theme ink 层 +`--skeuo-ink-fg-wet: #26241f`(§4.1 权威浓墨)+
   `--skeuo-ink-fg-dry: #191714`(同色相降明度 scale 0.65【提案·归数值轮】);
   `test_ac022_1` 现抽两色:公式锚 8 连 + wet 权威锚 + ΔH ≤12°(实测 6.86°)+ L(干)0.0087 < L(湿)0.0177
2. **步② 双态接图**:`SkeuoInk.uss` +`.ink-wet`/`.ink-dry`(冻结 guid `4ccb158d…`/`fbae6e46…`,
   color 走对应 var,无 slice 行);单槽语义显式登记(有意顶掉 ink_light 渐变底);
   冻结记录 :53/:54 落点列 + :86/:87 机器块回填;`test_ac022_2` 五断言 + Registry 零注册守卫
3. **步③ 洇开方向**:`test_ac022_3` 真贴图 LoadImage,湿覆盖率(L<0.2 提案带宽)**>** 干
   (实测 0.969 > 0.646);fail-loud 全图 a==255;只锁方向不锚数值

### 测试证据(终态)
- 过滤 **233/220/0 红/13 跳**(= 基线 230/217 +3)`unity/Logs/editmode_skeuo_final.xml`
- 全量 EditMode **3010/2963/0 红/46 跳/1 inc**(基线 3007/2960 +3,逐项一致)`editmode_full_20261009_form02.xml`
- 全量 PlayMode **98/97/0 红/1 跳**(逐数 = 基线)`playmode_full_20261009_form02.xml`
- 变异 6 发恰红(4 初轮 + 2 修复补验),零 MUT 残留

### 同批文档回刷
EPIC 五处(头部 Stories 计数 / Stories 表 022 行 / Counts 21→22 total(21 Complete+1◐) / Next Step 2/4 /
里程碑归属 022 行)· `milestones:131` 形态件条 ◐ 注加②(整条仍 `[ ]` 禁借绿)·
sprint-04 `形态件 2/4` · epics/index.md 行 11 加 022 · story-022 AC/DoD 实数回填。

## ✅ 上轮 = story-021 M2 形态件①(2026-10-08 · 线格/空行等重/明度轴 · 双代理评审修复复跑全绿 · 已提交 `0a7cf84`)

**流程对账(用户指令:创建+unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送)**:
创建+测试 ✅ → **双代理评审恰一轮 ✅**(代码面 PASS 0B/3R/4n · 测试面 BLOCKING:1 + R1-R4 + n1-n4)→
**修复 14 项全落 ✅** → **复跑绿 ✅**(过滤 230/217/0 + 变异 6 发全中 + 全量双套零回归)→ 提交推送(本步)。
评审原件 `production/qa/evidence/review-story-021-form-item-1-2026-10-08.md`(原判定→修复落点→验证命令)。
**修复要点**:B1 贴图桶非空守卫 · 公式锚定三连(白/黑/128灰) · 021-1 锚 `AddChannel` 方法体 ·
R1 44px 值锚(无障碍承诺可锚,提案色不锚) · R2 选择器恰一次(防后挂覆盖块) · R3 两载体 empty-row 挂载 ·
SkeuoFocusVisible 头注 = 明度轴口径更新 + **焦点环×线格单类级联冲突**登记(接线前置裁定项,今日不可观测)。
**判别力 6 发**:挂载挪方法体→021-1红 · SaveSlot 摘类→021-2红 · 第二覆盖块「实见2」→021-2红 ·
44→20px→021-2红 · minFraction 桶空「BLOCKING B1」→021-3红 · gamma 2.2「128灰锚定」→021-3红;
逐发 python 反向恢复,终态零 MUT 残留。

> 用户指令「开始做形态件①」+ 承载裁定 =「新开 story-021」。判据权威 = art-bible **G1**(格线语义)
> + **G3**(焦点明度轴);边界裁定(勘察轮定):线格 border 零新图 / 空行只交形态半(数据四态归数据绑定轮)/
> 阈值 0.12 提案值归数值轮 / 布局探针与截图级归桌面走查。

### 交付物(三步)
1. **步① 线格**:`SkeuoPaper.uss` 新 `.ruled, .empty-row` 声明块(border-bottom 走 var ×2);
   theme +`--skeuo-paper-rule: rgba(43,36,22,0.35)`【提案·归数值轮】+ property 枚举补 `rule`;
   `CasebookScreen.AddChannel` 行挂 `ruled`(脉案五行 + 问诊栏 = 满版纸线格行级兑现)
2. **步② 空行等重**:`.empty-row` 与 `.ruled` **同一声明块**同格线 + 同 `min-height` =
   `--skeuo-shared-row-height: 44px`(新变量 = AB-3 焦点落点高半);
   `SaveSlotItem` 空槽复用 `.empty-row` 语义正合(未改其代码);
   **story-011 B2 转正**:`test_ac021_2_…equal_weight_in_uss`(读 USS 去注释,锁「拆块即红」)
   + `test_ac021_1_casebook_rows_mount_ruled_class`(源真挂类)
3. **步③ 明度轴(G3)**:`test_ac021_3_focus_ring_luminance_delta_over_paper` —— 环 L=0.2756 vs
   纸面(`border_paper` 不透明像素 **≥1% 面积桶** ∪ 两主题底色**从 theme 现抽** —— 改色跟随)
   **实测 minΔ = 0.2656 ≥ 0.12**;gamma 线性化 + Rec.709;**环 vs 环自比对 = 0** 证公式非恒真;
   python 预演同口径复核(危险桶空集)。首跑红 = `varName` 误带尾冒号致双冒号,一次修复

### 测试证据(评审修复后终态 r2)
- 过滤 **230/217/0 红/13 跳**(三条新断言终态全 Passed;基线 227/214)
  `unity/Logs/editmode_skeuo_final.xml`
- 初轮变异三处恰 3 红(story 创建期)+ **评审修复后判别力变异 6 发全中**(逐发注入/恢复,零 MUT 残留)
- 全量 EditMode **3007/2960/0 红/46 跳/1 inc**(与基线逐项一致,零回归)
  `unity/Logs/editmode_full_20261008_form01_r2.xml`
- 全量 PlayMode **98/97/0 红/1 跳**(与基线逐数一致)`playmode_full_20261008_form01_r2.xml`

### 文档回刷(八处)
story-021(新建 → Complete,三步/AC/DoD 全勾含绿数据)· story-011(头部日期 + **B5 勾** + B2 闭环注 +
残余 NICE 划账)· casebook-39 AC `[A]`(◐ 注:声明层已断 / 布局探针归桌面,**保持 `[ ]` 禁借绿**)·
milestones `:131`(① ◐ 注,**整条保持 `[ ]`**)· sprint-04(形态件 0/4→**1/4**)· EPIC(Stories 头 +
021 行 + Counts 21 total + Next Step + 里程碑归属 021 行)· index.md(+021)· active.md(本段)

### 本批提交文件
`story-021`(新)· `story-011` · `casebook-39.md` · `milestones/README.md` · `sprint-04.md` ·
`EPIC.md` · `index.md` · `SkeuoPaper.uss` · `SkeuoThemeVariables.uss` · `SkeuoFocusVisible.uss` ·
`CasebookScreen.cs` · `texture_binding_gate_test.cs` · `focus_visual_and_accessibility_test.cs` ·
`casebook_rendering_test.cs` · **`review-story-021-form-item-1-2026-10-08.md`(评审原件,新)** ·
active.md(本段)—— 照旧排除:`.gitignore` · `.trae/…` · 孤儿 `Casebook.meta` ·
`Brass/__pycache__(.meta)` · `generate_focus_brass_2px.py.meta`(py 本体已入库,meta 留工作树)

### 下一步候选
① 形态件 ② 墨乾湿两态(story-020 已解其切图前置)· ② 急救零数字 · ③ 状态反馈通道 ·
④ 桌面走查队列累积(明度轴截图级 / 布局探针 / AC-42-C10 主题复跑 / **焦点环×线格级联接线前裁定**)。

---

## ✅ 上一轮 = 黄铜环图绑定到焦点样式(2026-10-08 · 绑定轮 · 已提交 36e5c72)

> 用户指令「那就绑定到焦点样式」。前置答问:**环图来源 = 程序化出图**(上一轮按 art-assets-required §二③
> 规格脚本直出,非外部美术;冻结件 R3「规格出图」即此意)。

### 绑定形态(甲案 · 同元素 background-image,单槽风险显式登记)
- `SkeuoFocusVisible.uss` `.focus-visible`:`background-image = 铜环 guid 6d4c0acb…` + 4 条 slice 2px;
  **实色 border 宽退役(0,铜色锚 `var(--skeuo-focus-border)` 保留** —— 守住 `test_focus_carrier_is_brass_not_ink`);
  **inset box-shadow 压痕移除**(自边缘起吃环 1px ⇒ 违 2px 视觉厚度);disabled 补 `background-image: none`
- **单槽铁律登记**(非静默):环图整槽替换 —— 带纹理焦点目标获焦时纸纹被顶;现状 `FocusVisibleStyle.ClassName`
  全库零调用 ⇒ 今日零可见风险;出路 = 乙案环子元素(重开焦点样式轮),已写进 USS 注释 + 冻结件 §六
- `SkeuoThemeVariables.uss`:`--skeuo-focus-border-width` 失引用加注(勿当死变量删)

### C8 门覆盖检查递归化(修盲区,当场抓出真漏冻)
- 原 `TopDirectoryOnly` ⇒ 子目录图漏登记不报。改 `AllDirectories`(E1/16 计数门仍顶层面,冻结件 §六 登记)
- **当场抓出**: `Textures/Casebook/casebook_paper_base-final.png`(2026-10-05 入库 2048²、零引用未接线)
  —— 冻结轮顶层面视野外 ⇒ 补登记为第 18 行(0 值登记态;三项格式手验合规但 E1 门不扫它,§六 登记)
- 冻结件:§二 行 17 USS 落点落实 + 行 18 补登记 · freeze-v1 两行同步 · 范围 17→18 条 · §六 三条新登记

### 测试证据
- 过滤 **227/214/0 红/13 跳**(+2 新夹具:`test_ac42c8_brass_focus_ring_bound…` + `…unregistered_subdir_png_caught`)
- 变异(slice-left 2→4 + guid 拔换)⇒ **恰 5 红**(C8 slice 失配 / 新绑定夹具 / C11 悬空 ×2 / ValidateAll 聚合),
  报文精确;python 反向还原复核 → 过滤复绿 227/214/0
- 全量 EditMode **3004/2957/0 红/46 跳/1 inc**(基线 3002/2955 ⇒ +2 新夹具零回归)
- 全量 PlayMode **98/97/0 红/1 跳**(与基线逐数一致)

### 出图脚本补存(用户指令 2026-10-08)
`Textures/Brass/generate_focus_brass_2px.py` —— 与图同目录;`--check` 实测**字节级一致**
(235 bytes 逐字节同入库版,证明上轮即 PIL 同参产出);`.py` 不在任何 `*.png` 门/测试扫描面;
依赖 Pillow(注释含清华源安装命令)。冻结件 §七 已补指针。

### 本批提交文件
`SkeuoFocusVisible.uss` · `SkeuoThemeVariables.uss` · `TextureBindingGates.cs` ·
`texture_binding_gate_test.cs` · 冻结件 · `Brass/generate_focus_brass_2px.py`(新)· active.md(本段)
—— 照旧排除:`.gitignore` · `.trae/…` · 孤儿 `Casebook.meta`

---

## ✅ 上上轮 = story-020 M2 形态件解锁链(2026-10-08 · 三步全交付 · 已提交 d3eefe7)

> 用户指令「完成020再提交」。三步 = ① 019-f 切图冻结件 ② 黄铜 2px 出图 ③ 019-a 余项绑定回填。

### 交付物
1. **步① 冻结件**:`design/assets/specs/nine-slice-freeze-2026-10-08.md`(§一 测量方法 6 步可复算 ·
   §二 逐张冻结表 17 行 · §三 取值规则 R1/R2/R3 · §四 `freeze-v1` 机器块 · §五 发现 · §六 局限 · §七 复算锚)。
   实测口径 = **可复算掩膜法**(灰度→白底合成→中心 1/3 中位背景→掩膜 12→**边带限制**(测上下边只统计中 1/3 列)→连续内容带)。
   **冻结轮查出真缺陷**:border_scroll 右框实测延展 **69 > 现值 68**(切右框 1px)⇒ 冻结 **72**,USS 四条同批改;
   border_paper 64 维持(max(64, ceil8(43)) = 64,覆盖 22–57 / 40–43 双口径)
2. **步② 黄铜 2px**:`unity/Assets/Gameplay.UI/Skeuomorphic/Textures/Brass/focus_brass_2px-final.png`
   (64×64 RGBA,2px 环 `#B8863B`,中心透明)+ meta(`spriteBorder: 2`)+ `Brass.meta`;
   放子目录**刻意**(顶层 `*-final.png` 恰 16 张计数/格式门用 `TopDirectoryOnly`)
3. **步③ 回填**:4 张 meta `spriteBorder` 一次填入(border_paper 64 · border_scroll 72 · paper_aged 64 ·
   seal_surface 8;余 12 张冻结值 0 = 明示不走九宫格)+ 哨兵门 `ValidateSpriteBorderLeftAsSentinel`
   **退役** → `ValidateSpriteBorderMatchesFreeze`(C8:冻结件解析→覆盖检查→meta 侧→USS 侧,`[C8]` 硬报错)

### 测试证据(三连 + PlayMode 留痕)
- 过滤 **225/212/0 红/13 跳**(`unity/Logs/c8-freeze-round.xml`)
- **MUT-C8**(border_scroll meta 72→68)⇒ **恰 2 红**(C8 夹具 + ValidateAll 聚合;报文点名「68 vs 冻结 72」;
  python 反向替换还原复核)(`unity/Logs/c8-mut.xml`)
- 全量 EditMode **3002/2955/0 红/46 跳/1 inc**(基线 3001/2954 ⇒ **+1 新负夹具,零回归**)
  (`unity/Logs/editmode-full-c8-round.xml`)
- 全量 PlayMode **98/97/0 红/1 跳**(与基线**逐数一致**;grep 实测 PlayMode 零耦合面)
  (`unity/Logs/playmode-c8-round.xml`)

### 文档回刷(七处)
story-019(头部状态 / 三步表全 ✅ / 拆分表 019-f ✅ + 019-e 耦合点闭 / **AC-42-C8 悬空片段拆出独立勾选条目** /
Test Evidence C8 表 / Completion Notes 019-f 条目 + 019-d/e 残余闭记)· story-020(Status + 三步 AC + 3 AC + 2 DoD 全勾)·
EPIC.md(Stories 头 / 019/020 行 / Counts 20 total / Next Step / 里程碑归属 019 注 + 020 新行)·
`epics/index.md`(skeuomorphic-ui 行)· milestones `:133`(保持 `[ ]` 加切图半注 + 修「现 Blocked ⛔」陈旧措辞)·
milestones `:139`(**勾 `[x]`** + 证据路径)· active.md(本段)

### 提交范围(排除项)
**含**:story-019/020 · EPIC · index · milestones · 冻结件 MD · 门 ×2 · 测试 · USS ×4 · meta ×4 ·
`Brass/`(PNG+meta+`Brass.meta`)· active.md
**不含**:`.gitignore`(本地改动永不提交,memory)· `.trae/skills/switch-claude-model/scripts/switch-model.sh` ·
`unity/Assets/Tests/EditMode/Casebook.meta`(孤儿,非本任务产物)

---

## 🔄 本轮 = 全库收口纪律缺陷修复(2026-10-08 · 已收口)

> 用户指令「现在修全库缺陷」。扫描发现 **59 个 story 头部标 `Complete` 但 Test Evidence 行 = `Not yet created`/`NOT STARTED`/`Pending`** —— 系统性收口纪律失效。

### 修复内容
- **42 个 story** 的 Test Evidence 行从 `Not yet created`/`NOT STARTED`/`Pending` 更新为 `[x] Complete` + 真实测试文件路径
- **6 个幽灵测试引用**订正为真实路径:
  - `combat-weapons/story-005`: PlayMode→EditMode
  - `disease-simulation/story-001`: DiseaseSimulation→Sim + 文件名订正
  - `persistence-service/story-002`: 补 `unity/Assets/` 前缀
  - `random-events/story-005`: PlayMode→EditMode
  - `random-events/story-006`: PlayMode→EditMode
  - `time-weather/story-004`: PlayMode→EditMode
- **1 个真缺失测试**(`event_reject_table_test.cs`)登记为等价覆盖(`event_pool_schema_test.cs` 的 4 个 `test_reject_*` 测试)

### 验证
- disease-simulation 实跑:**67/67 全绿**
- 提交:`408c818` — 40 文件变更,+54/-49 行

---

## ✅ 上一轮 = emergency-procedures **epic 收口**(2026-10-08 · 已收口)

> 7/7 story 已实现却卡 `In Review`;真实堵点 = ① round2 自陈「未实跑测试套件」② D1/D2/D3 文档对齐。

### 实跑验证(2026-10-08 · 当前 HEAD)
- EditMode 急救:**120 / 116 / 0 红 / 4 NOT-RUN**
- PlayMode 急救:**4 / 4 / 0 红**
- 全量 EditMode:**3001 / 2954 / 0 红 / 1 inc / 46 跳**
- 全量 PlayMode:**96 / 96 / 0 红**
- 4 条 skip 逐条核验 = 合规 NOT-RUN(带理由,非借绿)

### D1/D2/D3 核实(全部已对齐)
- D1 GDD:1030 `(32768×16385)` ⇒ 8193 + `:1031` 订正说明(算术复核 ✅)
- D2 story-006:76 测试位置声明(`Tests/EditMode/`)✅
- D3 story-007:207 Test Evidence `[x] Complete` ✅
- D3-b EPIC Stories 表 001–007 全 `Complete` ✅

### 交付物
- **验证原件**:`production/qa/evidence/verification-emergency-procedures-tests-2026-10-08.md`
- EPIC 状态 In Review → **Complete ✅ 2026-10-08**;`epics/index.md` 同步

### 未闭项(均归属他 epic,不阻塞)
- 载荷 `Seq` 占位 → 归上行链 45
- AC-10-04b 三格逐位 → ADR-012 矩阵未激活,NOT-RUN

### 收口
- Commit: 见本轮提交

---

## ✅ 上一轮 = case-system **story-006 重放持久化与跨系统边界义务**(2026-10-08 · 已收口)

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。

### 交付物
- **测试**:`unity/Assets/Tests/EditMode/CaseSystem/case_replay_persistence_test.cs`(10 测,覆盖 AC-37-06/不折叠/高水位/53 边界两条/D-37-B/[V] 移交)
- **评审原件**:`production/qa/evidence/review-case-system-story-006-2026-10-08.md`
- **本 epic 收口**:case-system 6/6 story 全 Complete ⇒ EPIC ✅ Complete

### 测试(实测)
- `unity/Logs/case_replay_v3.xml` = **22 / 22 passed / 0 failed**(story-006 10 测 + story-005 12 测)
- `unity/Logs/editmode_full_case006.xml` = **3001 / 2954 / 0 红 / 1 inc / 46 跳**(无回归)

### 评审
- 双代理一轮(lead-programmer 9 条 + qa-lead 5 条)**独立收敛**于同一根因:「8 测全绿」不构成 AC 覆盖 —— 恒真断言 / 空集绿 / 测测试私有 helper
- 修复:接生产码(`SaveCodec`/`PayloadCodec.Case`/`EventStream.GetNextPatientId`/`CaseStreamQuery`)+ 删私有 helper + 具体值断言 + 双向谓词测 + 乱序夹具

### 可红性证明(突变验证)
- A 高水位 off-by-one ✅杀 · B 哨兵守卫 ⚪等价突变 · D 哨兵初值 ✅杀 · E 载荷引用 ✅杀
- 生产码已还原(`git diff` 无输出)

### 未闭登记(禁借绿)
- AC-37-06 IL2CPP 半边:归 ADR-012 矩阵批
- 7a `Folded(p)` 折叠执行:生产谓词不存在 ⇒ NOT-RUN
- D-37-B 转登 9/7a:两 GDD 零命中 ⇒ NOT-RUN(producer 传播)
- 53 消费半边:挂 `AC-53-04`
- 整档字节级相等(部分):ADR-010 §一 三流段分帧 codec 未实现

### 收口
- Commit: 见本轮提交

---

## ✅ 上一轮 = case-system **story-005 守密纪律**(2026-10-08 · 已收口)

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。

### 交付物
- **测试**:`unity/Assets/Tests/EditMode/CaseSystem/case_secrecy_discipline_test.cs`(12 测,覆盖 AC-37-15/24/35/32/31/20 + quill_tick)
- **夹具**:`unity/Assets/Tests/EditMode/CaseSystem/Fixtures/lexicon_bijection_fail.json` + `lexicon_bijection_pass.json`(AC-37-35 双射反例)
- **评审原件**:`production/qa/evidence/review-case-system-story-005-2026-10-08.md`

### 测试(实测)
- `unity/Logs/case_secrecy_v2.xml` = **12 / 12 passed / 0 failed**

### 评审
- 双代理一轮(lead-programmer 代码面 + qa-lead 测试面)一致判 **BLOCKED**(6 条)
- 核心 = 「真载体缺失却报绿」+「恒真断言」+“必交夹具缺失”
- 修复:补空集绿守卫 + [TestFixture] + 命名修正 + 类型面/NOT-RUN 口径分离

### 收口
- Commit: `f57376d` — 已推送

---

## 🔄 上一轮 = disease-simulation **story-007 重开 9 落地**(2026-10-07 · 进行中)

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。

### 交付物
- **数据**:`assets/data/disease_action_axis.json`(新建,9 的处置轴 + treatable_by 关系,单一 master)
- **9 侧烘焙三件套**:`DiseaseActionAxisBaker.cs` / `DiseaseActionAxisBinder.cs` / `DiseaseActionAxisCookedWriter.cs` / `DiseaseActionAxisValidator.cs`(新建)
- **9 侧 schema**:`RegistrySchema.cs` 增 `TreatableByEntry` + R1-18/R1-19 校验
- **10 侧**:`EmergencyAction.cs` DC-4 判据源改指 9(文档已更新,实现仍用 `Enum.IsDefined`)
- **11 侧**:`PrescriptionActionIdRegistry.cs` 影子→真源(闭集 = 9 的轴,地板 = `NOISE_BAND_POTENCY_9` = 100 raw)
- **11 侧**:`PrescriptionActionsBinder.cs` DC-2/DC-6 从 warnings 升格为 errors(硬失败)
- **11 侧**:`prescription_actions.json` 的 `action_id` 重排 1→10
- **菜单**:`DataBakeMenu.cs` 增 `BakeDiseaseActionAxis` 菜单项
- **文档**:`entities.yaml` / `disease-simulation.md` / `prescription-and-medication.md` / `architecture.yaml` / `tr-registry.yaml` / `traceability-index.md` / `EPIC.md` / `index.md` / `story-007` 全量更新

### 测试(实测)
- 9 侧:`unity/Logs/disease_axis.xml` = **55 / 55 passed / 0 failed**
- 11 侧:`unity/Logs/prescription_axis.xml` = **174 / 174 passed / 0 failed**
- 10 侧:`unity/Logs/emergency_dc4.xml` = **115 / 115 passed / 0 failed**(4 skipped 既有)
- 全量:`unity/Logs/editmode_full_axis.xml` = **2935 / 2888 passed / 0 failed / 46 skipped / 1 inconclusive**(与基线一致)

### 待办
- ⬜ 双代理一轮评审(代码面 + 测试面)
- ⬜ 修复评审发现
- ⬜ 复跑绿
- ⬜ 评审原件落 `production/qa/evidence/review-disease-action-axis-2026-10-07.md`
- ⬜ 收口提交推送

### 未闭登记(禁借绿)
- **9 的 C# 16 字段 + 17 条区间校验 vs GDD §R1 的 17 条语义检查** —— 结构性断裂,归 9 的下一轮
- **`清创`** —— 9 点名、10 无实现,归 10 的 GDD 轮
- **10 侧 `ValidateActionId` 实现** —— 文档说读 9 的轴,实现仍用 `Enum.IsDefined`,须后续改为读 9 的烘焙产物

---

## ✅ 上一轮 = prescription-medication **story-002 补评审件**(2026-10-06 · 已收口)

> 承「补002评审件」。依 `.claude/docs/coding-standards.md` §Review Evidence Standards:
> 缺原件的对象**出路 = 补做一次评审(评当下)并落新原件**,**不追认**原判定。

- **原件**:`production/qa/evidence/review-prescription-story-002-2026-10-06.md`(新建)
  —— 含 **评审时点声明**(不追认 `f2bad6b`)· 原判定 → 修复落点 → 验证命令 · 变异证明 · 未闭登记
- **判定**:QA 侧 **2 BLOCKING · 3 MAJOR · 6 MINOR · 2 NIT**;
  结构侧代理**正文未回收**(交付前被协调方中断),其探索轨迹与 QA 侧 B1/B2 **独立重合**
  —— 原件 §〇 已**如实登记该回收缺口**,不凭记忆补写
- **修复落点**:
  - **B1(AC-11-09)** 新建 `PrescriptionDerivedBaker.cs`(唯一派生点,经 `DoseCalculator` 不重写 F-11.1)
    + Binder/CookedWriter/Baker/Probe 全链接线 + 4 测
  - **B2(AC-11-08)** 反射扫描(除两所有者外零 `axis_offset` 消费点)+ **阳性对照** +
    **双实现对拍**(`HalfLifeCalculator` ↔ `QualityTimelineSolver` 在 `half_life` 轴逐位相等)
  - **M1/M2/M3** loCarry 夹具 · 禁 `Int128`/`BigInteger` 扫描路径 · 新建 `PerceptibleFloorComparator.cs`
  - **m1/m2/m4/m6** 负 dose 两测 · `SignBound` 两测(恰 `2^63`)· 三处改名 · 扫描器注释剥离重写
- **变异证明 7 项全落盘**(MUT-1…7);
  ⚠️ **MUT-3 首轮存活 127/127**(原夹具全被更早的 `qHi != 0` 守卫拦下,`SignBound` 不可达)
  ⇒ 补 `test_dose_quotientAtSignBound_throws` 后**恰 1 红** —— 印证 QA 侧 m2 判定为真
- **复跑绿**:过滤套件 `dose_fix_2.xml` = **129/129 / 0 failed**;
  全量 `full_editmode_story002.xml` = **2890 / 2843 passed / 0 failed / 1 inconclusive / 46 skipped**
  (基线 2826 ⇒ +17 = 本卡 13 + 表卡 4)
- **未闭(禁借绿)**:AC-11-11④(BL-7)· AC-11-19 断言本体(BL-2)· AC-11-15 矩阵(ADR-012)·
  TR-prescription-007 21a 半边(BL-1)· **F5 双实现合并归 21a**(本卡只把重复可证伪)
- **诚实边界**:① `single_dose_max` **消费侧未接线**(9 的 F1 clamp 归 disease story-004);
  ② 生产 `ConfigVersion` **不含 `item_database_items.json`** ⇒ 「改药 ⇒ 哈希变」**当前不成立**;
  ③ F5 双实现**异常契约不一致**,对拍只证合法域内逐位相等

---

## 🔄 上一轮 = prescription-medication story-005(戥子输入与方笺呈现 —— 离散整数档与黄铜读数)

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> **本件 = prescription-medication epic 末件**;⚠️ **但 epic 未全闭** —— story-003 未开工(见下方 EPIC 表)。

### 交付物
- **生产**:`unity/Assets/Gameplay.UI/Skeuomorphic/DentchDoseSelector.cs`
  —— `DentchDetent`(实现 `IFocusable`)· `PrescribeStrokeIntent` / `PrescribeStrokeResult`(呈现 → 11 唯一输入形状)
  · 档位序列生成 / `DoseValueOf` 档值换算 / `LoadInto(BrassScaleElement)` 黄铜刻度装载
- **测试**:`unity/Assets/Tests/PlayMode/PrescriptionMedication/dentch_input_test.cs`(**49 条**)
- **装配**:`unity/Assets/Tests/PlayMode/PlayMode.asmdef` 增 `Gameplay.UI` / `Gameplay.Presentation` 引用
- **证据**:`production/qa/evidence/review-prescription-story-005-2026-10-06.md`

### 单轮评审 → 修复轮(要点)
- **结构侧** `APPROVED WITH SUGGESTIONS`(0 BLOCKING · 3 MAJOR · 7 MINOR · 3 NIT)· **QA 侧** `APPROVED WITH SUGGESTIONS`(0 BLOCKING · 4 MAJOR · 7 MINOR · 3 NIT)
  —— **两位评审均无 BLOCKING 落在生产件上**,全部 MAJOR 集中在**测试证伪力**与**登记完备性**
- **MAJOR-1(共指)** `Assert.AreEqual(sel.Detents.Count, sel.TickCount)` = `x == x` **恒真重言**(同源同字段),跨组件绑定从未被验证
  ⇒ 生产件新增 `LoadInto(BrassScaleElement)` **真装载路径**;测试改 `test_loadInto_brassElement_tickCountIsBound`(**反控**:未装载前刻度数须为默认 0)+ null 负测
- **MAJOR-2(QA)** 「零第二 EventSystem」断言面 = `Gameplay.UI` 程序集,该程序集**不引用** `UnityEngine.EventSystems` ⇒ 谓词**结构上不可能为真** = 恒真空真;故事卡点名的**双 EventSystem 夹具未交付**
  ⇒ 新增夹具 `DualEventSystemProbe : EventSystem` + 谓词正控(正控 + 反控)
- **MAJOR-3(共指)** `IsFocusEnabled` 文档称「满档 / 缺药时为 false」但生成路径**恒传 true**,分支从未产生也从未断言;常规档 `IsWholeDose` / `PresentationLabel` 零断言
  ⇒ 门控位改**可从装载期注入**(`BuildDetents(..., isFocusEnabled)` + ctor 重载)+ 文档收窄「判定源归 20」;补 4 条注入/常规档断言
- **MAJOR-4(QA)** `FocusRank` 值域(AC-42-B1 满射)**实为空判据** —— `FocusBoundaryAssertions.AssertRankDataSurjective` 只查 null(其源码自陈「不限制具体数值范围」);变异 `focusRank: i` 可存活
  ⇒ 补真判据 `CollectionAssert.AreEqual(Enumerable.Range(1,K), ranks)` + 单射 distinct 计数
- **MAJOR-4b(QA)** TR-prescription-011 ③「禁忌命中 ⇒ UI 无差异」`[A]` 条**零证据且未登记**
  ⇒ 生产件 `PrescribeStrokeIntent` **结构上不含**禁忌 / 拦截 / 扣减语义 ⇒ DTO 洁净扫描 + 字段面负断言覆盖结构半边;端到端等价性登记 **NOT-RUN 7**
- **MINOR-1(共指)** 两条「正控」近恒真(调被测方法 / 复算正式期望值)⇒ 换 `test_positiveControl_mutatedReferenceDiffersFromProduction`(**独立参考实现 + 变异公式**)
- **MINOR-2(共指)** `PrescribeStrokeIntent` / `Result` 全库**零生产消费方**,而 XML 声称「唯一输入形状」当前为假且未登记 ⇒ NOT-RUN **5 → 7 处**;补同域性真断言
- **MINOR-4(QA)** 零降级读数扫描面**只收单文件**(真正挂角标的 `Brass/` 不在面内)+ 禁词含中文字面量经剥离后**永不可能命中**(死词条)
  ⇒ 扫描面扩到 `Brass/` 目录 · 删死词条 · 补词面正控(证可命中 + 注释须被剥离)
- **MINOR-6(结构)** `MAX_DOSE_DETENTS` 单位与 GDD `:497`「`hi − lo` 档数上限」**差一** ⇒ 生产件旋钮文档显式钉「**单位 = 落点数**(= 焦点路径长度,承 Fitts 理据)」;GDD 侧登记订正
- **MINOR-7(共指)** 「同键双触发禁止」/「焦点单栈门」`[A]` 条除 EventSystem 计数外无判据且未登记 ⇒ 由 MAJOR-2 夹具覆盖结构半边;运行期实跑登记 **NOT-RUN 4**
- **NIT-1(QA)** `RegisteredMaxDoseDetents` **自守其门** ⇒ 合成扫描改以测试内**字面量**为循环上界 + 断言常量值域
- **NIT-2(QA)** 故事卡 QA 行 `dose_range=(2,5) ⇒ 长度 3` 是**算术笔误**(权威公式 `hi−lo+1` = **4**)⇒ 测试内显式标注笔误并取 4(**未镜像错误**)
- **QA 追加** `PresentationLabel` 原生成 `"第 {i+1} 档"`(含阿拉伯数字)与 AC-11-12「零数字读数」张力 ⇒ 改**零数字**标签「戥子档」/「整剂」
- **QA 追加** 本目录受 **AC-42-F3 词面门**扫**原文(注释也扫)** ⇒ 生产件注释改写为释义表述 + 文件头加注该纪律(实测原稿 `world_billboard_test` 报 3 处)

### 验证(实测)
- filter(评审前):`unity/Logs/dentch_input_6.xml` = **41 / 41 passed / 0 failed**
- filter(修复后):`unity/Logs/dentch_fix_2.xml` = **49 / 49 passed / 0 failed**(41 → 49,补 8 条)
- PlayMode 全目录(修复后):`unity/Logs/dentch_playmode_full3.xml` = **85 / 85 passed / 0 failed**(77 → 85,新增 8 条随套件全绿)
- 全量 EditMode(修复后):`unity/Logs/dentch_editmode_full3.xml` = **2826 passed / 0 failed / 46 skipped / 1 inconclusive**(既有,非失败;含 AC-42-F3 词面门通过)
- **变异证明(五条全部 ≥ 1 红,逐条命中预期新断言)**:
  MUT-A `focusRank: i` ⇒ 1 红 `test_detents_focusRankIsSurjectiveAndInjective` ·
  MUT-C 标签回带数字 ⇒ 1 红 `test_presentationLabel_containsNoDigits` ·
  MUT-D 忽略门控注入 ⇒ 1 红 `test_focusEnabled_injectedFalse_propagatesToAllDetents` ·
  MUT-E `LoadInto` 空体 ⇒ 1 红 `test_loadInto_brassElement_tickCountIsBound`(**原稿恒真断言 0 红**)·
  MUT-F 越界抛改 `Math.Clamp` ⇒ 2 红(源码面 + 行为面);
  变异后 `diff /tmp/Dentch.orig.cs <生产件>` **无输出 = 干净回滚**

### ⬜ 待办 / 未闭登记(禁借绿 —— 覆盖缺口,非安全洞)
测试文件头显式登记 **7 处**(本轮由 5 处扩至 7 处):
1. **AC-11-18 ②** 42 侧「下一档」焦点落点缺失 + `hi` 档纸面反馈 —— BLOCKED-BY-42 元件落地;本件只证**消费方**序列长度
2. **手柄(无指针)路径** —— BLOCKED-BY 桌面调试集中轮 + ADR-013 假设 6 spike(半可信)
3. **AC-11-13 / AC-11-12 / AC-11-21 走查子项** —— 可判 ≠ 已判,须实现轮人工执行 + 签核(主创 / 主创+医学从业 / 音频 lead)
4. **真实 UX 夹具下的焦点单栈门实跑** —— 本件只做**结构**断言;引擎运行期实跑判据归 42 侧 spike
5. **`materia_lexicon.cooked` 真装载** —— 21a 产出方未落 C# 字段(承 story-004 NOT-RUN 3);本件以烘焙 JSON 源件 + 结构扫描走通
6. **`IsFocusEnabled = false` 分支的判定源** —— 门控位已可注入(本件有判据),但**判定源**(满档 / 缺药)归 20 库存扣减面,未落地 ⇒ 恒 true 是当前唯一实跑路径
7. **`PrescribeStrokeIntent` / `Result` 的生产消费方** —— 全库零生产接线;落笔 → 11 接线归 8 侧 `S-8.4` 路线甲 + 39 方笺页

### 跨故事缺口(本故事范围外,但影响判据完整性)
- **AC-11-18 ② 的实体元件**(42 侧「下一档」落点 + `hi` 档纸面反馈)本 epic **不实现** ⇒ 该 `[A]` BLOCKING 的 ② 半边**当前无判据**;建议 skeuomorphic-ui epic 收口时确认承担方
- **3 侧 float→int 量化实现**(input-system epic story 006/007)未落地 ⇒ 本件只断言入参形状为整数,**未断言量化真在 3 侧发生**
- **`ModalId` 闭集与 `PaperCloseup48` 的「勿混读」**两处警示本件均已通过(闭集仍 7 员 · 方笺不成新模态 · 第七员在集内且与本条无关)

---

## 📋 历史状态(2026-10-06)—— prescription-medication story-004(Prescribe 流程 —— 域检查、原子扣减与成长门)—— ✅ 收口 · commit `db369d0` · 已推送

### 交付物
- **生产**:`PrescribeFlow.cs`(五步编排:域检查 → F-11.1/F-11.2 求值 → 20 扣减 → 发事件 → 成长门)
- **测试**:`prescribe_flow_test.cs`
- **证据**:`production/qa/evidence/review-prescription-story-004-2026-10-06.md`

### 单轮评审 → 修复轮(要点)
- **结构侧** `APPROVED WITH SUGGESTIONS`(0 BLOCKING · 1 MAJOR · 6 MINOR · 4 NIT)· **QA 侧** `CHANGES REQUIRED`(0 BLOCKING · 4 MAJOR · 6 MINOR · 5 NIT)
- **M1(结构 · 唯一 MAJOR = 文档冲突非代码 bug)**:AC-11-05③ / Edge Case 写「给错药 ⇒ `SkillGrown` 零发出」,与 F-11.5 **BL-3 改判**(门不读 `treatable_by`)相抵 ⇒ **零代码改动**,订正 GDD `:942`/`:645` + story 卡 `:73` 三处陈旧字面为「**成长照发**」
- **m1(结构)** `IPortionsConversion` 把 `dose × per-dose` 推给兄弟 epic(与 GDD `:408`「求值在 11」冲突)⇒ 端口改 `PortionsPerDose(ItemKey)` 只查表,**乘法移回 11**(宽算防溢出 + 上下界守卫)
- **m2 / QA M1+M2** 两处**恒真反射断言**(比对固定类型名 / 手写类型数组)⇒ 改枚举 11 全成员**符号名**差集 + 真取构造签名形参(变异:声明 `SkillMul` ⇒ 2 红)
- **m4(结构)** `PrescribePorts` 不校验 `doseBase > 0`(错误推迟到步骤⑤,事件已进流)⇒ 装配期 fail-fast + 2 条负测
- **QA M3** 零浮点扫描**无正控**(改成恒空 ⇒ story-002/003/004 三处共享断言**全部真空绿**)⇒ 新增临时目录正控
- **QA M4** `HasPortions` 实参无界(全用 `PerDose = 1` ⇒ `portions == dose`)⇒ `LastHasPortions` 记录 + 2 条有界测(变异:传错实参 ⇒ 2 红,原稿 **0 红**)
- **QA m2/m3** `EvaluateGateHit` 的 `doseLegal == false` 分支永不执行 ⇒ 补直调负支;五处空值守卫无测试 ⇒ 补 5 条负测
- **QA m6** 同 tick 双剂「Seq 各不同」未实现也未登记 ⇒ 文件头 NOT-RUN 5 处 → **6 处**
- **m5/m6/n1/n2/n4**:测名名副其实化 · 混堆注释收窄(集合选择归 20)· `TreatmentEvent != null` · `Seq` 占位显式断言 · `StripComments` 统一剥注释+字符串

### 验证(实测)
- filter(修复后):`unity/Logs/prescribe_flow_5.xml` = **53 / 53 passed / 0 failed**(原 41 条 → 补 12 条)
- PrescriptionMedication 全目录:`prescribe_flow_all4.xml` = **103 / 103 passed / 0 failed**
- 全量 EditMode:`prescribe_flow_full5.xml` = **2826 passed / 0 failed / 46 skipped / 1 inconclusive**(既有,非失败)
- **变异证明**:MUT-B 2 红 · MUT-C 2 红 · MUT-D 7 红 · MUT-E 2 红 · MUT-F 1 红(后两者原稿 **0 红**);变异后均 `diff` 验证干净回滚

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 6 项**:AC-11-16 正式对拍(BLOCKED-BY-30 实现)· AC-11-15 三格矩阵(ADR-012)· 换算表真源(21a 未落 C# 字段,BLOCKED-BY-OQ-11-10)· 省料数值(BL-6)· 非主机传输(BLOCKED-BY-45)· 同 tick 双剂 Seq(归 45/7a)
- **跨故事缺口**:story-003 卡要求的 `drug_event_test.cs` **全库不存在** ⇒ AC-11-01① / AC-11-22 / AC-11-10 assembly 面**三处 BLOCKING 无真判据**,story-003 收口前不得记为已有证据

---

## 📋 历史状态(2026-10-06)—— prescription-medication story-003(F-11.2 半衰期)—— ✅ 收口 2026-10-06 · 已提交推送

### 交付物
- **生产**:`HalfLifeCalculator.cs`(F-11.2 半衰期计算器,走 `Fix.operator+` 加法)
- **测试**:`half_life_test.cs`(**24 条**)· `PrescriptionFloatScan.cs`(零浮点扫描共享实现)
- **证据**:`production/qa/evidence/review-prescription-story-003-2026-10-06.md`

### 单轮评审 → 修复轮(要点)
- **结构 B-1**: raw `long` 加法绕过 `Fix.operator+` 的 `checked` 溢出保护(静默回绕;IL2CPP 下 UB)
  ⇒ 改走 `Fix effective = axisBase + offset;`(`Fix.cs:124-127` 的 `operator+` 抛 `OverflowException`)
- **结构 M-1**: `CalculateForDrug` 纯透传无价值 ⇒ 改为读 `DrugProfile` 的真实组合入口(可空校验)
- **结构 M-2**: 缺溢出行为测试 ⇒ 补 3 条(正向/负向/边界)
- **结构 m-1/m-2/m-3**: 误导性注释删 · 扫描面加注说明 · `EffectiveQuality` 透传注释
- **QA M1/M2 + m1~m4/m7/m8**: 溢出显式检测 · `Assert.Ignore`→硬失败 · 扫描面扩至 `ToFloat()`/`Math.*`/`decimal`/大小写不敏感 · 补上界与单元素测试
- **修复轮连带发现(非评审提出)**: 两测试共享扫描面的**重复实现漂移** ⇒ 抽共享 `PrescriptionFloatScan.Scan()`

### 验证(实测)
- filter:`unity/Logs/half_life_fix4.xml` = **59 / 59 passed / 0 failed**(PrescriptionMedication 全目录)
- 全量:`unity/Logs/half_life_full3.xml` = **2773 passed / 0 failed**(exit 2 = 既有 Inconclusive)

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 3 项**:AC-11-08 ②(21a 构建期断言不存在,BL-1)· AC-11-15(三格矩阵,ADR-012 未实跑)· TR-prescription-008(21a 半边)

---

## 📋 历史状态(2026-10-06)—— prescription-medication story-001(处方表与本草词表 —— 双表 polarity 硬门)—— ✅ 收口 2026-10-06 · 已提交推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:结构侧 CHANGES REQUIRED(3 BLOCKING + 3 MAJOR + 3 MINOR + 2 NIT)+ QA 侧 NOT APPROVED(3 BLOCKING + 3 MAJOR + 2 MINOR + 1 NIT),
> **6 BLOCKING 双侧**;全部落点 ⇒ 复跑绿。

### 交付物
- **作者态**:`assets/data/prescription_actions.json`(处方表种子)+ `assets/data/materia_lexicon.json`(本草词表种子)
- **生产**:`PrescriptionActionsBaker.cs`(仓根装载器)· `PrescriptionActionsBinder.cs`(阶段 2 绑定 + **唯一**校验点 DC-1/DC-3/DC-5/DC-7/AC-11-20)·
  `PrescriptionActionsCookedWriter.cs`(确定性写入器)· `PrescriptionActionsBinderProbe.cs`(薄转发)·
  `DataBakeMenu.BakePrescriptionActions`(菜单调用点)
- **测试**:`prescription_tables_test.cs`(**12 条**)
- **证据**:`production/qa/evidence/review-prescription-story-001-2026-10-06.md`

### 单轮评审 → 修复轮(要点)
- **结构 B-1**: 绑定器不读 `item_database_items.json` — DC-1/DC-5(覆盖)/DC-7(上界)/AC-11-20 在生产路径上未强制
  ⇒ `Bind` 签名加 `itemsJson` 参数,绑定阶段执行跨文件校验
- **结构 B-2**: AC-11-02 注释误导(说"扫描源文本"但无代码)⇒ 改为说明"由 RejectUnknownKeys 隐式满足"
- **结构 B-3**: 测试用 regex 解析 JSON 驱动断言,而非驱动生产绑定器 ⇒ 全部改为调用 `PrescriptionActionsBinderProbe.Bind`
- **QA B-1**: `test_bakeDeterminism` 恒真(只比较两次 ReadAllText)⇒ 改为调用 `BakeFromRepo` 两次比较字节
- **QA B-2**: `test_dc7_doseBaseWithinUpperBound` 空集真空真 ⇒ 内联构造带非 null dose_range 的夹具
- **QA B-3**: 四条负夹具是正向测试复制品 ⇒ 构造违反条件数据喂给 Binder 断言拒绝

### 验证(实测)
- filter:`unity/Logs/prescription_tables_fix5.xml` = **12 / 12 passed / 0 failed**
- 全量:`prescription_tables_full.xml` = **2796 / 2749 passed / 0 failed / 46 skipped / 1 inconclusive**

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 3 项**:DC-2(action_id 闭集,依赖 OQ-11-2)· DC-6(依赖 9 侧 NOISE_BAND_9)· AC-11-07(双表 polarity 交叉硬门,依赖 9 侧 disease_registry.json)
- 下一件:prescription-medication **story-003**(F-11.2 半衰期),同协议

---

## 📋 历史状态(2026-10-05)—— diagnosis-system story-003(F-8.1 可读地板与 F-8.2 精度档槽)—— ✅ 收口 2026-10-05 · 已提交推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:结构侧 CHANGES REQUIRED(1 MAJOR + 5 MINOR)+ QA 侧 CHANGES REQUIRED(1 MAJOR + 6 MINOR),
> **0 BLOCKING 双侧**;2 MAJOR + 11 MINOR 全部落点(**全为文本/文档 + 判据强度,零运行期改动**)⇒ 复跑绿 + 两处新 MUT 证明。

### 交付物
- **作者态**:`assets/data/diagnosis_read_floor.json`(合成系数:base_read=1 · read_floor_min=1/4 · read_gamma=2;**数值归用户数值轮**)
- **生产**:`DiagnosisReadFloorBinder.cs`(阶段 2 绑定 + **唯一**校验点 C-1/C-5/skill_cap)·
  `DiagnosisReadFloorBaker.cs`(仓根种子 → 产物)· `DiagnosisReadFloorCookedWriter.cs` +
  `DiagnosisReadFloorCookedCodec.cs`(严格镜像;固定头 **32 B**)· `DiagnosisReadFloorBinderProbe.cs`(薄转发)·
  `DiagnosisReadFloorTable.cs`(运行期定表 + `DiagnosisReadFloorEvaluator` 求值器 + `DiagnosisSlot`/`SignReadState` 枚举)·
  `DiagnosisChannelMaskMap.cs`(**Note 6** 通道序数↔位掩码映射 + 双向断言,接生产路径)·
  `DataBakeMenu.BakeDiagnosisReadFloor`(菜单调用点)
- **测试**:`read_floor_slots_test.cs`(**28 条**)· `DiagnosisGoldenScan.cs`(story-002/003 **共享**金标扫描真源 —— 兑现 story-002 头注「扩金标」承诺)·
  `tests/unit/diagnosis_system/fixtures/read_floor_*.json`(**10 夹具**)+ README 账本补 Story 003 段
- **金标**:`GoldenConstantsHash = f75a8170`(`b9354110` s002 → `5bba361c` s003 扩枚举/映射面 → `f75a8170` 修复轮,
  由 `FixedHeadBytes` 36→32 的**有意识**代码常量修正驱动)
- **证据**:`production/qa/evidence/review-diagnosis-story-003-2026-10-05.md`

### 单轮评审 → 修复轮(要点)
- **MAJOR-1(结构)** AC-8-35 金标「覆盖」声明 **over-claim**:`read_floor_slots_test` 写「F-8.1 参数半边转 covered」
  与 `sign_table_test` 头注矛盾 ⇒ 四处统一为准确边界(扫描面 = 前缀 ns **代码常量** + DIAG_TIERS;
  **曲线参数**住 `assets/data/*.json` 由 ConfigVersion 覆盖,**仍 NOT-RUN**)
- **M-1(QA)/MINOR-3** `README.md` 缺 Story 003 段(标称夹具 21 实存 **31**;无 AC→测映射;未登记 NOT-RUN)⇒ 补全段
- **MINOR-1(结构)** codec `FixedHeadBytes` 36 → **32**(注释双错订正;E-13 文本阈值)
- **MINOR-4(结构)** GDD §F-8.2 回退散文**示例**方向反 + 永不触发(`sign_rales` 粗档实有词)⇒
  登记 **GDD 散文勘误**(待设计轮),就地注 `DisplayWord` doc;**不改 GDD 权威件**
- **MINOR-5** 删死 import(`DiagnosisReadFloorTable.cs` `Sim.Contracts`)
- **m-1** `test_ac89` 去恒真 `DoesNotContain(...,99)` ⇒ 改断**枚举值域**(零锁闭成员)+ 五手段**各有真读数**(`DisplayWord` 非 null)
- **m-2** `test_g1` 去同参自等循环(纯函数必等)⇒ 改断**表项=存储值**(`FloorAt`) + 跨档区分(非退化)
- **m-3/m-4/m-5** 反射幂名清单扩 9 名 + 明写「真守卫在边界门」· AC-8-7 代理判据登记 · AC-8-46「词变粗」NOT-RUN 登记
- **m-6** story Test Evidence / Status / AC 勾选回填

### 设计决定(2)
1. **空白档回退方向 = 严格向下**(GDD §F-8.2 规则字面;koplik 粗/中为空 ⇒ 粗/中档读不出,细档起出词)。
2. **金标两次重钉均有意识**(扩面 + 修复轮常量修正),理由已登记。

### 验证(实测)
- filter:`unity/Logs/s003-fix3.xml` = **94 / 93 passed / 0 failed / 1 skipped**
- 全量:`s003-fixfull.xml` = **2630 / 2583 passed / 0 failed / 46 skipped / 1 inconclusive**
- MUT-D(`DisplayWord` 下界 `i>=0`→`i>=1`)⇒ **恰 3 红**(含 `test_ac89` —— 证 m-1 增强判据承重;旧 `DoesNotThrow` 版不红)
- MUT-E(`FloorAt` 返 0f)⇒ **恰 1 红**(`test_g1` —— 证 m-2 增强判据承重;旧自等版不红)

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 6 项**(本 story):AC-8-F3 σ 联动(归 disease 004)· AC-8-9 EmitGrowth 门控(story 005)·
  AC-8-46「词变粗」+ 病名半边(story 006/37)· 跨会话/跨平台逐位(AC-8-F5/story 004)·
  AC-8-35 曲线参数半边(ConfigVersion 覆盖)· AC-8-7 AC 字面浮点用例(待 `Precision`)
- **GDD 散文勘误 1 项**:`diagnosis-system.md` §F-8.2 回退示例(方向反 + 永不触发),待设计轮
- **TR-registry 回填**(TR-diag-008/009/011/012 等 gap→covered)= 独立 docs 轮,未动
- 下一件:diagnosis-system **story-004(F-8.3 阴性把握度与不泄漏不变量)**,同协议;
  epic 链 diagnosis(**3/6**)→ case → prescription

## 📋 历史状态(2026-10-05)—— diagnosis story-002(体征词条表 schema 与 P0 数据行)—— ✅ 收口 · commit `f27ca3e` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:结构侧 CHANGES REQUIRED(1 MAJOR + 10 MINOR)+ QA 侧 CHANGES REQUIRED(2 MAJOR + 10 MINOR),
> **0 BLOCKING 双侧**;3 MAJOR + 20 MINOR 全部落点 ⇒ 复跑绿 + MUT 变异证明。

### 交付物
- **作者态**:`assets/data/diagnosis_signs.json`(R-8.2 冻结 34 行 = 阳 30 / 阴 4)
- **生产**:`DiagnosisSignTable.cs`(validator + 三枚举 + `SlotBounds` **首次成文**)· `DiagnosisSignBinder`
  (**唯一 `Validate` 调用点**,聚合硬失败 + 空表拒收)· Baker / Writer / Codec(严格镜像 +
  schema 期望比对 + count 钳制 + 枚举序数界)· Probe · 菜单 `BakeDiagnosisSigns`
- **测试**:`sign_table_test.cs`(**42 条**)+ `tests/unit/diagnosis_system/`(**21 夹具** + README 账本)
- **双金标**:`GoldenConstantsHash = b9354110`(AC-8-35)· `GoldenContentHash = 3954e294`(R-8.2 内容逐字)
- **证据**:`production/qa/evidence/review-diagnosis-story-002-2026-10-05.md`

### 单轮评审 → 修复轮(要点)
- **S-MAJOR** `SignChannel` 同名不同物(本表序数 vs `Sim.Contracts` AC-21 位掩码)→ 枚举 doc 警示
  + **story-003 Note 6 登记映射表与双向断言义务**(未立映射前禁 cast)
- **Q-MAJOR-1** R-8.2 逐行内容无守卫 → `test_r82_contentFrozen_golden` 内容金标
- **Q-MAJOR-2** F-8.1/F-8.3 参数半边零覆盖 → 头注 + story AC 显式 NOT-RUN
- **MINOR ×20 全落点**:binder `catch (Exception)`(防 `/0` 逃逸)+ 空表拒收 + BindResult doc ·
  codec schema/钳制/序数界 · `SlotBounds` 收只读视图 · writer CS0104 别名 · 文件族名注 ·
  story-004 占位值预警 · `BakeFails` **恰一条排他** · P1a 8 值 TestCase 全值 · +4 违例夹具 ·
  tier 触底 / C-6 双标 / addRow 绑金标 / 切点上下文 / FormatConst 兜底 · README 账本 · Test Evidence 回填
- **两处 story 文本订正**(实现前对账登记):依赖「disease story 003 载体」不实 → `Editor.Tools.Bake` 模式;
  Note 1 三态可分错引 AC-8-F → **AC-8-21(+ V-8.2 / AC-8-24)**
- **MUT-Validate**:注释唯一调用点 ⇒ **恰 8 条校验器路径红**(tier35/tier15/阳性带权/阴性缺权/
  阴性零权/重复主键/lab/病史白名单),绑定层全绿 —— 承重面从推断变实测

### 验证(实测)
- bootstrap:`unity/Logs/s002-bootstrap.xml`(金标 PENDING→打出实际值;途中自查出 reveal
  TestCase 传裸串 bug,改数组后绿)
- filter:`s002-run2.xml` = **67 / 66 passed / 0 failed / 1 skipped**(skipped = 反向孤儿 [Ignore])
- MUT:`s002-mut-validate.xml` = **恰 8 红**;还原后复绿
- 全量:`s002-full.xml` = **2603 / 2556 passed / 0 failed / 46 skipped / 1 inconclusive**(基线 2561 + 42)

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 8 项**全表见评审原件 §4:反向孤儿(BLOCKED disease 006)· 正向接线(tripwire)·
  F-8 金标半边(story 003/004 扩)· 跨会话确定性 · 人工核对字面 · cooked.bytes 数据轮口径 · …
- `neg_weight = "1"` 占位已挂 story-004 Note 7;枚举↔位掩码映射已挂 story-003 Note 6
- **TR-registry 回填**(TR-diag-014 等 gap→covered)= 独立 docs 轮,未动
- 下一件:diagnosis-system **story-003(F-8.1 可读地板 + F-8.2 精度档槽)**,同协议;
  epic 链 diagnosis(2/6)→ case → prescription

## 📋 历史状态(2026-10-05)—— diagnosis story-001(程序集边界与 VitalsDto 只读门面)—— ✅ 收口 · commit `6685046` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:双代理评审 **结构侧 CHANGES REQUIRED(3 MAJOR)+ QA 侧 APPROVED(2 MAJOR 收口前置)**,
> 无 BLOCKING;5 MAJOR + 8 MINOR 逐条修复 ⇒ 复跑绿。

### 交付物
- **生产**:`DiagnosisVitalsFacade`(静态零字段唯一取数门面)· `DiagnosisGrowthExit`(7 参纯转发
  出口,IL 调用点恰=1)· `DiagnosisBoundaryGates`(Cecil IL + 源文本 + 逃逸,11 个 tag)·
  `AssemblyGates` RunMenu + BuildGate 接线(reload hook 刻意不加,承 Required-7c)
- **测试**:`unity/Assets/Tests/EditMode/DiagnosisSystem/boundary_guard_test.cs`(**25 条**,
  含 EOF 真 IL 负例夹具)
- **证据**:`production/qa/evidence/review-diagnosis-story-001-2026-10-05.md`

### 单轮评审 → 修复轮(结构 3 MAJOR + QA 2 MAJOR + 8 MINOR)
- **S-1** `Mathf` 双层补入(G-4 邀请式绕行)· **S-2** `IModifierType` 修饰符侧遍历(b5 Required-2)
- **S-3** `DateTimeOffset`/`Stopwatch`/`TickCount` 时钟补全 · **S-4** 深度超限改落红
- **S-5** 缺失不叠报 + `[D-TREF]`→`[D-0]` tag 统一 · **S-6** 哈希段标结构占位(禁称已生效)
- **S-7** 消费者住前缀纪律登记 · **S-8** `IVitalsQuery` 成员集锁 · **S-9** BuildGate WARN 口径
- **Q-1** 全量复跑 · **Q-2** AC 逐条括注 NOT-RUN · **Q-3** `RecipeDataSet` 替代括注
- **Q-4** 铁律③ 判据等价性(IL+源 ⊃ 反射)入 Completion Notes · **Q-5** 三处 `Is.Not.Empty` 自证
- **Q-6** 漏测分支负例补齐(`Fix.One` 字段 / 源层 `IEventSink`+`FixParse` / `Mathf` / 时钟三族)

### 验证(实测)
- 过滤:`unity/Logs/s001-diag-fix.xml` = **25 / 25 passed / 0 failed**
- 全量:`unity/Logs/full-diag-001.xml` = **2561 total / 2515 passed / 0 failed / 45 skipped /
  1 inconclusive**(增量 115 = patient-ai s003+s004+本批;根 `Skipped:Ignored` 与基线 019d 同态)

### ⬜ 待办 / 未闭登记(禁借绿)
- 残余 NOT-RUN 全表见评审原件 §四:AC-8-1 全流程脚本+哈希鉴别力(005/006)· 跨 epic 基线(CI)·
  AC-8-3 四输出(002/003)· AC-8-6 定表+G-4 IL2CPP(003/004)· AC-8-4 11 侧+D-11 词面回补
  (prescription story 003)· `typeof(Fix)` 构造性绕行复查(11/005 轮)
- **TR-registry 回填**(TR-diag-004 gap→covered、002/010 partial 等)= 独立 docs 轮,未动
- 下一件:diagnosis-system **story-002(词条表 schema)**,同协议;epic 链 diagnosis → case → prescription

### 前一收口(同日,已推送)= patient-ai story-004 `0b6f948`
- 4/4 全闭;过滤 168/166/0/2;残余 AC-13-V8(BLOCKED-BY 45)· CrossPlatform(CI)·
  [L] 五档(BLOCKED-BY 42/44)。⚠️ patient-ai EPIC 行仍 `Ready`(先例账,归该 epic 收尾轮)。

---

## 📋 历史状态(2026-10-05)—— 拟物 UI story-019-d(接图落地 + C4 消红)—— commit `155a9b1` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:上一轮评审后用户裁定「**先清 44 条 C4,再收口 019-d**」+「**抽主题变量 + 铜色,一并解决 4 条 C4**」。

### 交付物
- **接图(C7 接图半)**:四基类 `paper`/`scroll`/`ink`/`seal` 加 `background-image: url("guid:…")`;
  `.paper-aged` 变体随 `paper` 落地 ⇒ **落地 5 处选择器 / C7 判据面 4 注册项**
- **判据收窄**:`SkeuoComponentRegistry` 增列 `IsTextureContainer`(默认 `false`,须显式传 `true`)
  ⇒ C7 判据 = 「贴图容器类」(2026-10-05 用户裁定,取代首轮窄读法 4 类)
- **C4 消红 44 → 0**:`SkeuoThemeVariables.uss` layer **5 → 9 员**(brass/implement/marks/focus 抽变量)·
  `brass-bg` 取 art-bible §4.1 权威值 **`#B8863B`**(订正原内联 `#b87333`,绿通道差 19,非本项目裁定值)·
  `.brass-scale` 底色与 border **解耦**(评审 M4,独立命名 `--skeuo-brass-scale-color`)·
  焦点载体 **墨 → 铜**(承 `art-bible §7.4` Amendment + GDD 规则十注记)
- **夹具补真缺口**:新增 `validate_all_aggregate_test.cs`(**7 条**)—— `ValidateAll()` 此前**全 `Tests/` 零调用**,
  C1/C2/C4/C5/C6 在 CI **长期无覆盖**;`texture_binding_gate_test.cs` 两处 `null` 桩改真
  `AssetDatabase.GUIDToAssetPath`(019-c 遗留,接图后误判悬空)
- **文档订正 4 处**:「贴图容器 5 项」→「**4 注册项 / 5 落地选择器**」(story-019 ×2 · art-assets ×1 · GDD ×1)

### 验证(实测)
- 过滤:`unity/Logs/skeuo-019d-final.xml` = **224 / 211 passed / 0 failed / 13 skipped**
- 全量 EditMode:`unity/Logs/full-editmode-019d.xml` = **2446 / 2402 / 0 / 43 / 1**(基线 2445 ⇒ **+1,零回归**;
  唯一 inconclusive = 既有 `SettingsExposureTest.test_monoOption_existsWithValidDefault`)
- **门探针**:复刻 `RunMenu()` 契约(反射先调 private `InitializeDefaults()`)⇒ `ValidateAll()` = **0 条 · VERDICT=GREEN**
  (探针已删,日志 `unity/Logs/probe-019d-build.log`)
- **变异**:删 `.ink` 接图 ⇒ C7 精确报 `.ink` 缺贴图;还原 `#b87333` ⇒ C4 精确报 line 4;均还原后全绿

### ⬜ 待办 / 未闭登记(禁借绿)
- ⚠️ **019-d 残余义务**:`-unity-slice-*` 的**运行期实测 + 截图签核**仍未做 —— 现仅断言「有引用」,
  **证明不了「贴对了」**;切片值待 019-f 冻结件(故 C8 未闭)
- ⚠️ **未闭色值(待裁)**:`--skeuo-brass-aged`(`#A0653A`,注释自称「铜锈」)与 art-bible §4.1/§8.6.3 的
  铜锈 `#4F7A6B`(青绿)**语义冲突**;`#8C5A2B` art-bible 全文无出处。经 `git show HEAD` 确证均为**既有值**,
  本轮保值抽变量**未纠正**,已就地加警示注释
- **019-f(C8 冻结件)** = BLOCKED-BY 美术(九宫格 slice 真值;现 `spriteBorderActual=(0,0,0,0)`)
- **019-b(图集预算 C9)** = Blocked,`PAGES_MAX` 未冻结
- m1:`seal_red` 全库零挂载
- 下一件:**见 Phase 2 关键路径**(interaction-system 已 7/7 全闭;余 patient-ai / diagnosis / case / prescription)

## 📋 历史状态(2026-10-05)—— 拟物 UI story-019-e(导入格式订正)—— commit `7739142` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:用户裁定「拆成 DEF,c7 范围窄读法写死」⇒ 019-d 拆为 d(接图,美术)/e(格式,零依赖)/f(C8 冻结件,美术)。

### 交付物
- **生产**:16 个 `*-final.png.meta`(spriteMode 0→1 = **Single** · textureType 0→8 · alphaIsTransparency 0→1;`spriteBorder` 留零哨兵)·
  `TextureBindingGates.cs` 增 `ValidateSlicedTextureImportFormat` + `ValidateSpriteBorderLeftAsSentinel`
- **测试**:`texture_binding_gate_test.cs` **+5 条 E1 夹具**(22 条本件)

### 单轮评审 → 关键取证(QA 侧 BLOCKING 被实测推翻)
- QA 侧判「`spriteMode:1` = Multiple ⇒ 与单图九宫格矛盾」(据本机文档**文本顺序**推断)
- **独立取证推翻**:运行期反射 `SpriteImportMode` = **`None=0/Single=1/Multiple=2/Polygon=3`** ⇒ `spriteMode:1` **= Single**;
  引擎回读 16 张全 `mode=Single · spriteCount=1`(临时探针,日志 `unity/Logs/probe-enum.xml` / `probe-sprite-mode.xml`,探针已删)
- **但暴露真缺陷(文档级)**:原文括注 `(Multiple)` 是**误标**(自 commit `6049204` 引入,从未核过)
  ⇒ **已修** story-019 §AC-42-E1 + `art-assets-required-for-019`(改为「= `SpriteImportMode.Single`」+ 枚举真值)
- **绿**:过滤 217/204 passed/0 failed · 全量 EditMode 2439/2395/0/43/1(基线 2434/2390,+5 零回归)
- **变异**:MUT-E1a(spriteMode 回落)⇒1 红 · MUT-E1b(border 注入非零)⇒1 红(日志留档)
- **原件**:`production/qa/evidence/review-skeuomorphic-ui-story-019e-2026-10-05.md`

### ⬜ 待办 / 未闭登记(禁借绿)
- **019-d(接图)** = BLOCKED-BY 美术(逐变体映射语义;导入格式前提已由 019-e 解除)
- **019-f(C8 冻结件)** = BLOCKED-BY 美术(九宫格 slice 真值;现 `spriteBorderActual=(0,0,0,0)`)
- **019-b(图集预算 C9)** = Blocked,`PAGES_MAX` 未冻结
- 未闭:018-e 的 `-unity-slice-*` **运行期实测**归 019-d(截图签核)· `nPOTScale` 无门覆盖(登记为引擎派生值)
- 下一件:见 Phase 2 关键路径

---

## 📋 历史状态(2026-10-05)—— 拟物 UI story-019-c(贴图接入护栏)—— commit `a10fa6c` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。

### 交付物
- **生产**:`unity/Assets/Editor.Tools.Gates/TextureBindingGates.cs`(纯逻辑,零引擎依赖,照 `PresentationDtoGuard` 先例)·
  `SkeuomorphicUiGates.cs` 门聚合(C10/C11/C7 骨架半 → `ValidateAll`)
- **测试**:`unity/Assets/Tests/EditMode/SkeuomorphicUI/texture_binding_gate_test.cs`(**17 条**)

### 单轮评审(结构侧 unity-specialist + QA 侧 qa-lead)→ 修复轮
- **两侧独立收敛同一根因 = C10 主入口假绿**:`Directory.GetCurrentDirectory()` 在 Unity CLI EditMode 下
  = **`<repo>/unity`(工程根)**,非 `<repo>`;门以 `Path.Combine(cwd,"Assets",…)` 拼路径 ⇒ 拼不中 ⇒ 扫描**静默空跑**
  ⇒ `Assert.IsEmpty` 恒真。**变异测试证伪不了这类假绿**(注入物与扫描面同落泄漏目录)。
  铁证 `unity/Logs/probe.xml:46`;同族订正先例 `modal_gate_test.cs:502`(早已自陈「cwd = unity/」)
- 修复:① `DefaultRepoRoot` 上溯寻含 `Assets/` 的那层;② 三扫描函数加**反空跑守卫**(缺失/空 ⇒ 硬报错);
  ③ `UrlTargetsTextures` 由死代码降为诊断分级;④ 新增 5 条夹具(12→17)
- **变异**:MUT-C10 / MUT-C11 各 **2 红**(含真扫描面锚 —— 证 B1 关闭),两变异文件均还原
- **绿**:过滤 212/199 passed/0 failed · 全量 EditMode 2434/2390 passed/0 failed/43 skipped/1 inconclusive(基线 2429/2385,+5 零回归)
- **原件**:`production/qa/evidence/review-skeuomorphic-ui-story-019c-2026-10-05.md`

### 同批产出 = 019 完全实现所需美术资产清单
- `production/qa/evidence/art-assets-required-for-019-2026-10-05.md`
- **结论**:美术侧瓶颈**仅 3 件** —— ① 五族九宫格切图边界元数据(冻结件)· ② 逐变体映射语义裁定 ·
  ③ 铜族焦点黄铜 2px 最小切片(**唯一新出图**;M2 硬前置,E 裁)。16 张主贴图早已入库,非缺口。
- **实测附加发现**:元件库 USS 实有 **24 个类选择器**,而 `SkeuoComponentRegistry` 仅登记 4 类
  (记号族住 `MarkRegistry`;黄铜/器具/焦点族**零登记表**)⇒ **C7「已注册类」范围待裁**(决定 019-d 接图量 4 vs 24)。

### ⬜ 待办 / 未闭登记(禁借绿)
- **019-d(接图)** = BLOCKED-BY 美术(冻结件 + 映射裁定 + 导入格式订正 16 `.meta` 全 `spriteMode:0`)
- **019-b(图集预算 C9)** = Blocked,`PAGES_MAX` 未冻结(ADR-013 §6.6 假设 6 spike 未跑)
- **待裁**:C7「已注册类」范围 · AC-42-C7/C8/C9/C10/C11 未入 GDD(架构侧治理项)· C11 未覆盖 `resource()`/`.uxml`
- 下一件:见 Phase 2 关键路径(patient-ai story-003 = ViewState 投影 / cue 发射 / Material 映射)

---

## 📋 历史状态(2026-10-04)—— patient-ai(13)story-001 协议步骤 2/5

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。
> **评审只做一轮**。

### 步骤 1 ✅ 已完成 —— 实现 + Unity CLI 测试
- **生产**(`unity/Assets/Gameplay.Presentation/PatientAI/`):`BehaviorBands.cs` · `BehaviorState.cs` ·
  `BehaviorMap.cs`(决策核心,修掉了 `_ = trend;` 不可达缺陷)· `PatientBehavior.cs`(滞回 + Terminal 闩锁 + 三判据)·
  `PresentPatientView.cs`(F-13.6 优先级表)· `PatientCue.cs`(13 命名空间:纯函数核 + `CueKind`/`CueIntervals`)·
  **`PatientCueEmit.cs`**(44 前缀命名空间:唯一发射桥)
- **测试**:`unity/Assets/Tests/EditMode/PatientAI/behavior_map_test.cs`(**38 条** —— 含后补的
  `test_ac13a5_trendIsInertInMap_branching`(MUT-B 首轮 0 红暴露的缺口)+ NaN/±Infinity 边界)
- **⚠️ 本轮最重的接线发现 = AC-44-B1 ① / AC-44-B3 双门**:
  ① **b5① 逃逸谓词按文件判** —— 含 44 契约 token(`AudioCueDto`/`IAudioCueSink`)的文件**必须且只能**
     声明 44 前缀命名空间 ⇒ 13 的调度面与发射桥**拆成两个文件**;
  ② **AC-44-B3 入口白名单扫「44 前缀下每个类型的公开方法签名」** ⇒ `Emit` 签名缩成**纯基元**
     (`int patientId` / `int kind` / `int cellX/Y/Z`),13 命名空间类型一律在**体内**装配。
- **绿**:patient-ai 38/38 · 全量 EditMode **2375 / 2331 passed / 0 failed / 43 skipped / 1 inconclusive**
  (inconclusive = 既有 `SettingsExposureTest`,与本 story 无关;全量数取自 36 条时点,38 条后未重跑全量)
- **变异证明**:MUT-A(删夹取)⇒2 红 · MUT-B(给 `Map` 加 trend 分支)⇒ 首轮 **0 红(缺口)**,
  补 `test_ac13a5_trendIsInertInMap_branching` 后 ⇒ 红 · MUT-C(删 Terminal 闩锁)⇒2 红

### 步骤 2 ✅ 已完成 —— 双代理评审(单轮)
- 结构侧(`aa64bdaa32ad46640`)= **CHANGES REQUIRED**(无 BLOCKING;F-1…F-7)
- QA 侧(`a21992cb2ace8b063`)= **REJECT**(2 BLOCKING:B1 扫描根阴性恒真 · B2 只证「持了接口」;
  M1/M2 · m1/m2/m3 · NOT-RUN 10 条)
- ⚠️ 两位曾停在轮次上限(转录 idle ≈73 min),按「子代理满轮可以接着再送」显式重送后收回
- **原件落盘**:`production/qa/evidence/review-patient-ai-story-001-2026-10-04.md`

### 步骤 3 ✅ 已完成 —— 修复轮(逐条落实 + 变异坐实)
- **B1/M2**:`ScanClosureForNames` 生产根改 `ProductionScanSeeds()`(13 前缀 **∪** 44 桥前缀)
  + 新增 `test_ac13a1_scanRootCoversAudioBridgeNamespace_b1` 锁根枚举
- **B2**:新增 `test_ac13a2_vitalsProducedOnlyByIVitalsQuery_sourceClosure` + 负夹具
- **F-3/M1**:`test_ac13b5_sessionWriterIsUnique_reflection` 重写为 **IL 写入点扫描**
  (`ScanSessionWriters`:stfld 后备字段 ∪ call set_Session)+ 负夹具 `ShadowSessionWriter`
- **F-2**:`PatientBehaviorDirector` ctor 调 `Validate`,破表 `throw ArgumentException`
- **m3**:`BehaviorBands.Validate` 补 `SeekMin < DeathBandMin` 独立项
- **m1**:trend 惰性断言补带符号邻域球(±1e-6/±1e-3/±0.01/ε)
- **m2**:`ResetForLoad` 补方向对照负夹具
- **M2 注释订正**:如实声明扫描器归一化口径 ≠ `PresentationDtoGuard.NormalizeMemberName`

### 步骤 4 ✅ 已完成 —— 复跑绿
- **patient-ai 45/45**(38 → 45,+7)· 全量 EditMode **2384 / 2340 / 0 / 43 / 1**
- **变异 5/5 各恰一条红**(MUT-B1/B2/F2/F3/m3,日志 `unity/Logs/mut-*.xml`)

### 步骤 5 ✅ 已完成 —— 收口提交推送(`4076e1e`,已 push)
- 22 文件 · 排除 `unity/Assets/unity.meta` + `unity/Assets/unity/Logs.meta` + `.gitignore`
- ⬜ 提交 → ✅ 推送 origin/main

### 待办(已闭)
- ✅ 评审回收 → 修复轮 → 复跑绿 → 收口提交推送(`4076e1e` / `eb8b718`)
- ✅ `sprint-04.md` §Phase 2 表与关键路径图更新(interaction 全闭、patient-ai story-001 已收口)
- ⛔ `unity/Assets/unity.meta` + `unity/Assets/unity/Logs.meta`(早前相对路径测试输出误建的空树,
  `Logs/` 本身已被 `.gitignore` 覆盖)—— 已排除出提交;删除需用户批准(`rm -rf` 被拒)

---

## ✅ patient-ai(13)story-002 —— 空间行为(轨 A)—— 收口 2026-10-05

### 交付物(4 生产件 + 1 测试件 = 78 条)
- `SpatialPerception.cs`(F-13.3 整数平方和禁 sqrt · F-13.4 LOD 三档 + 节流判据 · EC-13-02 量程护栏)
- `ClinicKnowledge.cs`(F-13.2 `HomeRegion` / `KnowsClinic` 两事实析取)
- `LogicalStepper.cs`(F-13.7 累加器步进 `acc` · `Moving(p)` 五合取项 · 防跳格断言)
- `PatientSpatialDirector.cs`(在场循环 · 升序求值 · `AtClinic` 格成员判定 · `PhaseOf` / `EcozoneOfCallCount` 可观测面)
- `spatial_behavior_test.cs` = **78/78 绿**

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(4 MAJOR)· **QA 侧 REJECT**(F-1 MAJOR + F-2/3/4 MAJOR)—— 两侧独立收敛到**同一组四条**
- 修复:**F-1 `AtClinic` 改回 GDD 的格成员判定**(注入 `Func<WorldPos,bool> inClinicCells`;
  旧实现用路径游标代偿 ⇒ 假阳/假阴/单格路径三向皆错,下游 F-13.6 `AwaitingCare` 直接受害)·
  **F-2 TC-7 换顺序敏感场景**(哈希序 ≠ 升序序键集 + `onEvaluated` 求值序探针)·
  **F-3 `EcozoneOfCallCount`**(「HomeRegion 只求一次」可证伪)·
  **F-4 AC-13-E3 IL 引用扫描**(`Math.Sqrt`/`Physics.Raycast`/NavMesh,影子件共用机器)
- **变异证明**:MUT-F1a(还原旧 `atClinic` ⇒ 恰 2 红)· MUT-F2(删 `ids.Sort()` ⇒ 恰 1 红)·
  MUT-F3(`HomeRegion` 每 tick 重求 ⇒ 恰 1 红)· MUT-F4(生产件注入 `Math.Sqrt` ⇒ 恰 1 红)
- **评审原件**:`production/qa/evidence/review-patient-ai-story-002-2026-10-05.md`

### 未闭登记(NOT-RUN,禁借绿)
- **NR-S2-1 `ClinicCells` 真实数据接入**(本 story 只接注入谓词,未接 24 `CONTEXT_TABLE` ∪ 52 `CLINIC_FRONT`)
  ⇒ **story-003 前置**(F-13.6 `AwaitingCare`)· NR-S2-5 `ShouldDecideNow` 驱动接入 ⇒ story-003/004 ·
  NR-S2-6 `ResetForLoad` 真负夹具 · NR-S2-7/8 story-001 遗留 5 项仍开

### 待办
- ✅ 收口提交推送
- ✅ **patient-ai story-003 已收口**(见下节)
- ⬜ **下一件 = patient-ai story-004**(或按 epic 内剩余 story 序 —— 见 sprint-04 关键路径)

---

## ✅ patient-ai(13)story-003 —— 呈现投影与视图 API —— 收口 2026-10-05

> 承「严格执行:创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送。评审只做一轮」。

### 交付物
- **生产**:`PresentationProjection.cs`(ViewState 投影链 / `IPresentPatients` 视图 / `MaterialTable` 查表器)·
  `PatientCueSchedule.cs`(cue 调度 + 呼吸层生命周期;终局 = 呼吸停止 + 姿态落最静止档)
- **测试**:`presentation_projection_test.cs`(**131 条**,AC-13-A4/A5/C1…C5/D1…D6/F1…F3)
- **证据**:`production/qa/evidence/patient-ai/story-003-accessibility-signoff.md`([L] 项)·
  `production/qa/evidence/review-patient-ai-story-003-2026-10-05.md`(评审原件)

### 单轮评审 → 修复轮(三条 BLOCKING)
- **G(真 bug)**:`Decide` 首拍 `firstDue = phase` 把相位当**绝对 tick** ⇒ 真实入场 tick 下恒真 ⇒
  **去同步彻底失效**(实证 6 id 全 due)。修复:增 `entryTick` 形参,`firstDue = entryTick + phase`
- **A/C2**:AC-13-C2 走程序集引用面而 37 程序集**不存在** ⇒ 恒真借绿 ⇒ 改**源码面 grep**
- **B/C4**:AC-13-C4 只扫成员名 ⇒ 改 **IL 引用面**(看得见 new GameObject/Object.Destroy/Instantiate)
- MAJOR:AC-13-D3 姿态半边零承载(增 `PostureTier` 字段)· D6 分段常函数测恒真(改行为面)·
  C3 负夹具恒真(影子真计数)· A4 名字面(改 IL 面 + NOT-RUN 剥离半边)· F1/F2 过claim(改名 `*_structuralHalf_*`)
- **变异证明**:MUT-G/B3/H2/C2 逐个注入 ⇒ 必红且点名。⚠️ MUT-H 首轮暴露 D6 修复只测首拍会漏网 ⇒ 补稳态半边

### 验证
- patient-ai **131/131 绿**(`unity/Logs/s003-final.xml`)
- 全库 EditMode **2455 passed / 0 failed / 1 inconclusive / 43 skipped**(`s003-fixgreen.xml`,**零回归**)
- 提交 `a1f7a36` 已推送 origin/main

### 未闭登记(NOT-RUN,禁借绿)
AC-13-D1 达成(表落盘 + 44 签署)· AC-13-A4 剥离半边(构建产物探针,EditMode 不可达)·
[L] 无障碍达成面(AC-13-F1 归 42 冗余通道 / AC-13-F2 归 44 空间化 + 用户拍定 `PERCEPT_R`)·
AC-13-D3 姿态的**呈现**归 42 —— 见 signoff NR-S3-1…4。承 story-002 的 NR-S2-1(真 `ClinicCells`)继续滚入。

---

## 📌 历史 —— patient-ai(13)story-002 规格与依赖面(已兑现)

> 用户令「马上开始轨 A」⇒ 关键路径续行:**13 story-002/003/004 → 8 → 37 → 11**。
> 承「严格执行:创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送。评审只做一轮」。

**story-002 规格**(`production/epics/patient-ai/story-002-spatial-behavior-perception-and-stepping.md`)——
感知格距 `Perceives = Δx²+Δy²+Δz² ≤ PERCEPT_R²`(禁 sqrt)· HomeRegion = `EcozoneOf(spawn_anchor(p))` ·
LOD 按 d² 三档 · 逻辑格步进(定点累加器 `acc`,Q16.16) · `Moving(p) := ¬Frozen ∧ State ∈ {Seeking}` ·
`Seeking{EnRoute/AtClinic}` 双相 · 升序 id 求值 · 解冻不补算。

**依赖面**:story-001 ✅ · 6 的 `EcozoneOf` / `spawn_anchor`(world-ecozones Story 002/003 — 需确认落点)·
9 的在场判定接口 · 整数导航格(23/27 基础设施,消费级)。

**下一步动作**:① 确认 `EcozoneOf` / `spawn_anchor` / 在场判定 三处消费面**是否已在库**
(未落则登记消费点桩 + NOT-RUN,禁借绿);② 读 `design/gdd/patient-ai.md` F-13.2/F-13.3/F-13.4/F-13.7 + B/E 组 AC 原文;③ 落实现。


## 📊 全项目进度总览（2026-10-03 实测重算）

> ⚠️ **本表于 2026-10-03 按各 story 真件逐件重算**(口径:`> **Status**:` 首行 + 体 `**Status**: [x]`)。
> 下表为**实测值**;旧值(124 Complete / 6 Ready / 77 In Progress / 59.9%)系**陈旧转录**,已废。

### 阶段状态

| 项 | 值 |
|---|---|
| **Stage** | Pre-Production |
| **Sprint** | sprint-03 ✅ 已闭(17/17) · **sprint-04 Phase 1 ✅ 已收口** · **Phase 2 进行中 = 4/7 系统完成**(patient-ai story-001/002/003/004 已收口)—— 关键路径移至 diagnosis-system |
| **Gate Check** | CONCERNS（2026-09-29 二轮，无 NOT READY 阻塞） |
| **ADRs** | 28/28 Accepted |
| **P0 GDDs** | 31/31 Approved |
| **Commits since 09-22** | 375 |

### Epic 故事进度

> **列语义(防混计 · 2026-10-03 明写)**:四列为**互斥且穷尽**的分类,口径 = story 件 `> **Status**:` 首行。
> - **Complete** = 首行含 `Complete`(或体 `**Status**: [x]`)
> - **Ready** = 首行为 `Ready` **或 `In Review`** —— 二者皆「story 已实现但 epic 未收口」,
>   本表**不分开列**(此为既定口径,非疏漏;`In Review` 的明细读「备注」列)
> - **In Progress** = 首行含 `In Progress`(**实测恒为 0**)
>
> ⚠️ **旧表误读的成因**:把「`In Review`」当成 `In Progress` 计 ⇒ 虚报 77。
> **`In Review` ≠ `In Progress`** —— 前者是「已实现待收口」,后者是「实现进行中」。
> 同理 **`In Review` ≠ `Complete`**(「不得借绿」)。

| Epic | Stories | Complete | Ready | In Progress | 备注 |
|------|---------|----------|-------|-------------|------|
| **audio-system (44)** | 14 | **14** | 0 | 0 | ✅ 全收口 |
| **skeuomorphic-ui (42)** | 18 | **18** | 0 | 0 | ✅ 全收口 |
| **skill-system (30)** | 8 | **8** | 0 | 0 | ✅ 全收口 |
| **telemetry-analytics (51)** | 10 | **10** | 0 | 0 | ✅ 全收口 |
| **input-system (3)** | 13 | **13** | 0 | 0 | ✅ 全收口 |
| **item-database (21a)** | 12 | **12** | 0 | 0 | ✅ 全收口 |
| camera-viewpoint (2) | 6 | **6** | 0 | 0 | ✅ 全收口(2026-10-03) |
| casebook (39) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| case-system (37) | 4 | **4** | 0 | 0 | ✅ 全收口(2026-10-06) |
| clinic-machine (24) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| combat-weapons (25) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| death-respawn (29) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| diagnosis-system (8) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| disease-simulation (9) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| emergency-procedures (10) | 7 | **7** | 0 | 0 | 🔶 **story 7/7 Complete;EPIC 未收口**(A1/A2/A6/B4/C4/C5 已闭;✅ 评审原件两份均在库,**旧记「评审原件缺」为方向性错记**;**真实残留 = D1/D2/D3 文档对齐 + 实跑测试套件**(round2 自陈未实跑,「0 红」系读源码而非执行)) |
| enemy-ai (27) | 5 | **5** | 0 | 0 | ✅ 全收口 |
| foraging (17) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| interaction-system (4) | 7 | **7** | 0 | 0 | ✅ **全收口 2026-10-04**(001…006 + **007 装载接线 · NR-1 已闭**;**未闭登记 = NR-2 真表数值轮 · NR-3/4/5 三条 `[L]` 走查 · NR-6 `OQ-17-3` · 007 新增 4-DC-4①/跨会话/§三其余规则 等七条 NOT-RUN**) |
| inventory-items (20) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| medical-consequences (53) | 4 | 0 | 4 | 0 | ⬜ 未启动 |
| modular-building (23) | 7 | **7** | 0 | 0 | ✅ **Complete ✅ 2026-10-03**（C1/C2/N-r1/C8-ID 全闭 · 本轮 72/72 绿 · 全量 2204/2163/0红，`9bb912b`+`bfa6234`;**未闭登记 = N-r2 生产装配根 + AC-23-09 跨平台签名**） |
| patient-ai (13) | 4 | **4** | 0 | 0 | ✅ **全收口 2026-10-05**(story-001/002/003/004;未闭登记 = V8 联机 BLOCKED-BY 45 · 跨平台 EXTERNAL · [L] 五档可读性部分闭) |
| player-controller (1) | 6 | **6** | 0 | 0 | ✅ **Complete ✅ 2026-10-03**（两轮评审判据缺陷已修;88 过 + 3 NOT-RUN，`a78c27a`） |
| prescription-medication (11) | 5 | **4** | **1** | 0 | 🔄 **In Progress(未全闭)** — ✅ 001 / 002(**评审原件已补 2026-10-06**)· 004 · 005(结构半边);❌ **story-003 未开工** —— 其标题所指的 `DrugTreatmentApplied` 构造点 / 写者独占**库内无实现件**(提交的 story-003 实为 F-11.2 半衰期,与本卡范围不符),`Required evidence` 的 `drug_event_test.cs` 全库不存在 ⇒ **AC-11-01① / AC-11-22 / AC-11-10 三条 BLOCKING 无真判据**;**epic 收口前置 = producer 裁定补做 story-003 或改派这三条 AC 并同步 TR 登记**;另 story-005 走查半边(AC-11-12/13/21 人工签核)+ AC-11-18 ② 实体元件承担方(skeuomorphic-ui)待闭 |
| processing (18) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| random-events (52) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| time-weather (5) | 5 | **5** | 0 | 0 | ✅ 全收口 |
| tutorial-onboarding (48) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| world-ecozones (6) | 6 | **5** | 1 | 0 | ✅ **Complete ✅ 2026-10-03**（6/6 story;N1/N5 已修 · 本轮 109/109 绿;**未闭登记 = N3 白名单判据(待 27 侧落地)+ Story 005 [L] 走查 EXTERNAL**) |
| persistence-service (7a) | 2 | **2** | 0 | 0 | ✅ 全收口（002 = ADR-029 契约支） |
| save-slot-ui (7b) | 1 | 0 | 1 | 0 | ⬜ 未启动 |
| **合计** | **207** | **137** | **70** | **0** | **66.2% 完成(137/207)** |

> ⚠️ **2026-10-03 实测重算(当前口径)**:按各 story 真件逐件解析 ⇒ **207 / 136 Complete / 0 In Progress / 71 Ready**。
> 与上一版(124 / 6 / 77 / 59.9%)的差额来源:
> ① `modular-building` 6 → **7** 且 0 → **7 Complete**(story-007 = ADR-029 接线支;本轮并闭 C1/C2/N-r1/C8-ID → 转 Complete);
> ② `world-ecozones` 5 → **6**,4 → **5 Complete**(story-006 = ADR-029 接线支;转 Complete);
> ③ `player-controller` / **`emergency-procedures`** 由「Complete」/「In Review」重分类 —— 前者转 Complete,后者按 story 真件归 In Review(7 件,非旧记 6);
> ④ `persistence-service` 002 契约支 ⇒ 1 → **2 Complete**;`save-slot-ui` 归 Ready(非 In Progress);
> ⑤ **`camera-viewpoint` 6 Complete** —— 旧表已记 ✅,本轮无变;
> ⑥ 旧「In Progress 77」系把 `In Review` 与 `Ready` 混同计;实测 **In Progress = 0**(无 story 件标 `In Progress`)。
> 重算口径:逐 story 件解析 `> **Status**:` 首行 + 体 `**Status**: [x]`;`Complete*` / `In Review*` / `In Progress*` / 其余=Ready。

> 📌 **历史转录订正记录(保留闭环)**:原记「191 / 93 / 95 / 6 / 49%」及 2026-10-02 口径「203 → 206」
> 均已作废,勿再引用。成因同族:总数漏计 `persistence-service` / `save-slot-ui`,
> 且把 `In Review` 与 `In Progress` 混计。

### 测试状态

| 指标 | 值 |
|------|---|
| EditMode | **2204 total = 2163 Passed · 0 Failed · 40 Skipped · 1 Inconclusive**（2026-10-03 batchmode 实测，Unity 6000.3.24f1） |
| PlayMode | 25/25 Passed · 0 Failed（2026-10-01 实测） |
| 确定性验证 | F7 反汇编 CLEAN · AC-29 三平台逐位一致 |

> ⚠️ **上表 EditMode 一行 2026-10-03 更新为当前 HEAD 实测**(2204/2163/0/40/1)。
> 历史链条(保留闭环):原记「1831/1858」出自 `185063f`,该提交**编译未通过**
> (`Scripts have compiler errors`)⇒ 测试从未跑起来,数字**无源**;`5572d66` 修复编译后 = 1989/2022;
> 2026-10-02 = 1995/2028;2026-10-03(本轮,三 epic 收口后)= **2204/2163**。
> ⚠️ 旧行把 `total` 写成分子分母两个数(1995/2028),易误读为「1995 通过 / 2028 应为」——
> 现行口径按 `total = passed + failed + inconclusive + skipped` 记账。
> exit code 2 源自唯一 Inconclusive(`Audio/SettingsExposureTest.test_monoOption_existsWithValidDefault`,既有项),**非失败**。

### 关键里程碑

| 日期 | 事件 |
|------|------|
| 2026-09-20 | `/create-architecture` 完成，architecture.md v1.0 |
| 2026-09-20 | ADR-023/024/025 起草并 Accepted（Required #1/#2/#3） |
| 2026-09-21 | Gate Check 一轮 FAIL → 用户承接 → CONCERNS |
| 2026-09-22 | U0a 工具链闭合 · UX Review Phase 3A · 批裁轮 OQ 全结 |
| 2026-09-23 | ADR-026/027/028 Accepted（Required #4/#5 + 音频归属） |
| 2026-09-24 | AC-29 三平台确定性验证通过 · F7 反汇编 CLEAN |
| 2026-09-25 | 数值批三批全拍 · R13 回写闭环 · 44 二轮修订 |
| 2026-09-26 | 音频 Story 001-004 完成 · 技能/遥测/输入/UI 全推进 |
| 2026-09-27 | 音频 Story 005/006/014 完成 · 986/986 测试全绿 |
| 2026-09-28 | 音频 Story 007-013 完成 · 1175/1175 测试全绿 |
| 2026-09-29 | Gate Check 二轮 CONCERNS（无阻塞） |
| 2026-09-30 | active.md 刷新 |
| 2026-10-01 | Sprint 02 全部完成（16/16）· Sprint 03 部分完成（11/17） |
| 2026-10-01 | modular-building / world-ecozones 越序实现（架构依赖先行） |
| 2026-10-01 | 冲突解决提交 c291873 |

### Sprint 状态

| Sprint | 计划时间 | 实际状态 | 备注 |
|--------|---------|---------|------|
| Sprint 01 | 10-05 ~ 10-18 | ✅ 8/8 Complete | 提前完成 |
| Sprint 02 | 10-19 ~ 11-01 | ✅ 16/16 Complete | 提前完成 |
| Sprint 03 | 11-02 ~ 11-15 | ✅ 17/17 story Complete | 完成（2026-10-02）· ⚠️ **AC-S03-5 黄金夹具未兑现**（emergency NOT-RUN · combat 零存在;根因 = ADR-012 矩阵未激活） |

### 交付物
- **生产**:`InteractionSelector.cs` —— 第一键改测 `d∞(playerCell, cell)`(评审外发现:原测「到原点」,
  与 F-4.1 判定式不符,是 F-4.1b 三例失败真因)· `IsBetter` static + 三键字典序 +
  `KindPriorityOf` 查表 + `Chebyshev(a,b)` int64 两参
- **测试**:`target_selection_test.cs`(**13 条**)· `replay_selection_test.cs`(**4 条**)—— 全部断言**直接驱动生产**
- **评审原件**:`production/qa/evidence/review-interaction-story-002-2026-10-04.md`

### 实跑(2026-10-04 修复轮)
- `unity/Logs/interaction-s002-final.xml` = **41/41 green**(24 边界 + 13 + 4)

### 双代理评审 → 修复轮(已完成)
- **结构评审 REJECT**(3 BLOCKING)· **QA 评审 REJECT**(F-1…F-13)⇒ 逐条修复
- 修复:全部断言改绑生产 · int64 夹具命中真陷阱 · 哈希序负夹具驱动生产 · F-4.1b 三例逐字照抄 ·
  补两条 QA 案例(到达序打乱 + 缓存上一目标负夹具)· 闭集计数改 Enum.Length
- **变异测试证可红**:MUT1(删 int64 拓宽)/MUT2(d∞ 从原点)/MUT3(删键③)/MUT4(键③ 哈希序)/
  MUT5(删键②)—— 逐项令对应测试红,生产已还原

### 待办
- ✅ story-002 收口提交推送(12133f7 前的 13891b3/893f3a3)

---

## story-003(候选集四源构造)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`SourceDtos.cs`(九个具名输入形状)· `CandidateSources.cs`(四源只读接口 +
  装配包)· `CandidateSetLoader.cs`(四源合并 · **无玩家格形参** = 取路 (a) 结构保证 ·
  不裁剪 · 不持 sink)
- **测试**:`candidate_dto_reflection_test.cs`(**14 条** AC-4-03)·
  `candidate_sources_test.cs`(**11 条** AC-4-20/22 + 装载形状)
- **评审原件**:`production/qa/evidence/review-interaction-story-003-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s003-final.xml` = **56/56 green**(14 + 11 + story-001 24 + story-002 7)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **QA ACCEPT**;F-3(装箱判据循环论证)/ F-4(基类展开零覆盖)/ F-5(边缘登记)逐条修复
- 结构侧无独立代理原件(代理触顶未回)⇒ 主会话复核通过,缺口登记在案
- **变异证明可红**:MUT-base(删基类展开 ⇒ 恰单条红 55/56)

### 待办
- ✅ 收口提交推送(12133f7,已 push)
- ⬜ story 004(R_INTERACT 邻域裁剪)—— interaction-system 下一件

### 交付物
- **生产**:`DiscoveryRequest.cs`(F-4.3 三字段载荷)· `InteractionRadius.cs`(单源持有者,4-DC-1 下界)·
  `KindRouteTable.cs`(路由表双向闭合,RegisteredSystems 10 值)· `NeighbourhoodReporter.cs`(广播自报)·
  `IDiscoveryReporter.cs`(Request 签名改为载 `DiscoveryRequest`)· `InteractionSelector.cs`(半径单源化)
- **测试**:`discovery_report_test.cs`(F-4.3/4.3b/4.4 + AC-4-13)· `radius_single_source_test.cs`(AC-4-17 + 4-DC-1 下界)·
  `kind_route_closure_test.cs`(AC-4-18 + 4-DC-5)
- **评审原件**:`production/qa/evidence/review-interaction-story-004-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s004-final2.xml` = **86 / 83 passed / 0 failed / 3 skipped(NOT-RUN 机检)**

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 APPROVED WITH SUGGESTIONS** · **QA 侧 ACCEPT-WITH-FIXES**;两件原件皆落盘
- 修复:扫描器分叉(删属性分支)· 6 半改机检 NOT-RUN · 帧率测试重写为「计数=调用次数」·
  新增「无主动交互 ⇒ 零出境」负夹具 · 半径扫描器已知限制登记 · RegisteredSystems 7→10(PF-1)
- **变异证明可红**:mut-cheb(丢 int64 拓宽 ⇒ 恰 1 红)· mut-gate(删主动交互门 ⇒ 恰 1 红)

### 待办
- ✅ 收口提交推送
- ⬜ 系统 6 Epic(接收侧:latch/幂等/判距复验/唯一 Append)—— 转绿 AC-4-13 6 半 + AC-4-17 6 消费半

<!-- STATUS -->
Epic: sprint-09 🚧 In Progress(M2 收口冲刺 · 2026-10-14 开)
Feature: S1 第七轮全链人工 playtest / S2 PAGES_MAX 实测冻结 / S3 M2 Exit 复评翻勾
Task: S2 ✅ / S3 ✅(翻 2 勾 · :169 维持不勾)→ S1 用户真机连续走查(唯一未动)
<!-- /STATUS -->

## S3 M2 Exit 复评(2026-10-14 · ✅ 落定 · 翻 2 勾)

- **复评原件**:`production/qa/evidence/m2-exit-recheck-sprint09-2026-10-14.md`
  (两条独立子代理全仓核 + 本席抽验承重行)。
- **判定**(与 sprint-09 §1.2 TD 预判不一致 —— 三条里只有两条是勾选滞后):
  - `:111` 端到端链 → **✅ 翻 `[x]`** —— 真生产调用点 = `PrescriptionPageHost.cs:137`
    (推翻旧读数「PrescribeFlow.Prescribe 全库零生产调用方」,那是 2026-10-11 旧快照,
    早于 sprint-08 S2 接线)⇒ `DrugTreatmentApplied` 运行期可达。
  - `:224` 灰盒登记 → **✅ 翻 `[x]`** —— 登记表 `:236-249` 与 `entity-inventory.md`
    42 项 VS Critical 逐类对齐,无空白。
  - `:169` 4 项形态件 → **◐ 维持 `[ ]`** —— ①②④ 施加点确已接;③「可跳过」的
    **跳过入口零交互绑定**(`EmergencyCandidateList.cs` 无 `focusable`/`RegisterCallback`/`clicked`;
    `SetVisible` 全仓零生产调用)⇒ 功能半未接,归 10 实现轮(story-023 边界裁定①)。
- **顺带订正两处陈旧读数**(原文保留 + 加「已过时」标注):
  `README:196-200`「③ 候选列表零生产实例化」· `:202`「落笔输入入口未接」—— 均已被 sprint-08 推翻。
- **M2 Exit 剩余未闭 = 1 条**(`:169` 的 ③ 功能半);是否带残余收口 M2 = **用户裁定项**。
- 提交 `e0cbede`(已推送)。

## story-009 立件(2026-10-14 · 用户裁定「只做窄片」→ 勘察后升级为「装真状态门」)

- **用户三连裁定**:① 走「只做窄片」路 ② 但窄片须「接真状态门」③ 「先落 story 再开工」
  → ④「转 Ready 并同步 EPIC.md」。
- **勘察发现(改变范围的事实)**:「真状态门」需要 10 的核心主体 ——
  `EmergencyDirectReadChannel`(四态机 417 行)**零生产实例化**、
  `InputActionAsset` **零运行期装载点**、`InteractionInputDriver.cs:105` 硬编码
  `method: 0`(Manual)⇒ **跳过路径无生产入口**。急救链**运行期可达性 = 0%**。
- **另查**:「10 实现轮」**全库无 epic/story/工量登记** = 「引用却无登记」幽灵待办
  (与 ADR-022 关卡工具同型)。
- **产出**:`production/epics/emergency-procedures/story-009-direct-read-channel-runtime-wiring.md`
  (**Ready ⬜** · Estimate 3–5 人日 · 11 AC = 9 BLOCKING + 2 ADVISORY · 归 **sprint-10**)。
  同批同步:`EPIC.md`(Stories 表 + §2026-10-14 补注)· `epics/index.md`(8→9 stories)·
  `milestones/README.md:169`(③ 承接件具名)· `sprint-09.md` S3 小节。
- **头号技术风险**:`InputSystem.onAfterUpdate` 生产聚焦环境时序**未实测**
  (ADR-011 Engine Risk HIGH;story-007 只在未聚焦 batch 验过)。
- **未提交**(待用户指令)。

---

## sprint-08 收口判定(2026-10-14 · 四导演面板 · ✅ Complete 落判)

- **Verdict:CONCERNS(非阻断)→ 转 Complete** —— 3 READY(PR / TD / AD)+ 1 CONCERNS(CD);
  Chain-of-Verification 5 问核过,verdict 不变。自动化面收口复跑:EditMode **3270/0/46/1**
  (B-6-2 批次后代码零回归,1 inc = 既有 monoOption 环境依赖)+ PlayMode **175/175**。
- **CD CONCERNS 核心**:逐缺陷 targeted 复验 ≠ 组合走完全链 ⇒ **第七轮全链人工 playtest**
  登记 **sprint-09 首项**(M2 Exit 前体验层验证门;⑥施治 F 键人工面第六轮就未测)。
- **登记项**(全部不阻断,详 `production/sprints/sprint-08.md` §6 末):tech-debt 三条
  (短路键脆弱不变量 / 换药路径丢焦 / FocusOut 投递未实测)、鼠标焦点环语义(UX polish)、
  数值轮三项(底衬 / 边线 alpha / 28px)。
- 收口提交:sprint-08 头部 Status 翻 ✅ + §6 末判定记录 + active.md 头部/STATUS 翻面(C1 闭环)。

---

## B-6-2 收口(2026-10-14 · 两层根因 · 双半达成 · ✅ Complete)

- **修法**(用户裁定**甲案 overlay**):环走获焦目标内部匿名子元素 ——
  ① 施加者接线 `SkeuoRuntimeDriver.AttachFocusRingApplier`(面板根 FocusIn/FocusOut 冒泡,
  防重入不翻倍、失焦零残留;`focus_ring_applier_test` 2/2,新文件);
  ② USS `.focus-visible` 转 overlay 语义(absolute 四向 0 + 环图 slice;border-width 0 维持,
  2px 厚度全由环图承担)—— story-021 加载序冲突 + 单槽铁律「纸纹被顶掉」两风险**结构性注销**。
- **第一轮人工复验红**「环没跟过去」根因:焦点死在**隐藏旧页** —— 戥子行翻页那刻才建,
  旧页 `display:None` 后 FocusController 在可见树无可焦目标 ⇒ 修 = `SubmitPageNext/Prev`
  尾调 `FocusFirstOf(新页)`(官方 Focus() 路径,不自实现导航,ADR-013 §五)。
  判据:⑤b 直调 + ⑤c 事件派发内翻页(真 panel 断言 `focusedElement`)。
- **第二轮人工复验红**「还是没修好」根因:真机 20 Hz tick → `ApplyData` →
  `RefreshDentchDoses` **每 tick 全量摘掉重建档位行** ⇒ 翻页落焦的持焦行 50 ms 内被移出
  层级 ⇒ 引擎**静默丢焦**。测试夹具无 tick 循环 ⇒ 首轮 ⑤b/⑤c 全绿而真机红(差异面)。
  修 = **幂等短路**(行集只由档数单值决定,档数未变不拆建;**零缓存语义不动** ——
  DTO 每 tick 仍重读)+ 竞态回归测 ⑤d `test_tickRebuild_focusSurvivesRepeatedApplyData`
  (翻页落焦 → 连续 10 轮 ApplyData → 焦点仍活)。
- **变异验证**:撤短路 ⇒ ⑤d 恰红(15 条中唯一红,14 全绿无误伤;`b62_mutation.xml`),
  恢复复绿 ⇒ 恰红证据闭环。
- **自动化半**:PlayMode **175/175**(`b62_final.xml`);**人工半**:用户真机复验
  **「环跟行走」**(方笺页 ↓ 落第一档位行、方向键逐档;翻回脉案页同判)。
- **评审原件**:`production/qa/evidence/review-b6-2-focus-visibility-2026-10-14.md`。

✅ **sprint-08 的第六轮 playtest 阻塞(B-6-1…B-6-4)四项全部解除** —— 整体转 Complete
归 sprint 收口流程判。未提交改动:`CasebookScreen.cs`(翻页焦点转移 + 幂等短路 + 观测面)、
`SkeuoRuntimeDriver.cs`(环施加者)、`SkeuoFocusVisible.uss`(overlay 语义)、
`casebook_page_turn_test.cs`(⑤b/⑤c/⑤d)、新文件 `focus_ring_applier_test.cs`(+meta)。

---

## story-005(模态门与路由边沿)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`ModalGate.cs`(`IModalGateState` 单布尔消费契约 + `IArmedState` + `ModalGate.Accept/Select/SetMotorSuppression`)
- **测试**:`modal_gate_test.cs`(AC-4-09/10/19 + 规则九,含选择器自报计数接缝、单 bool 可执行影子、闭集基数守卫、零出现 `ModalId` 断言)
- **评审原件**:`production/qa/evidence/review-interaction-story-005-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s005-final.xml` = **101 / 98 passed / 0 failed / 3 skipped**(3 = story-004 NOT-RUN)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING;F-2…F-11)· **QA 侧 ACCEPT-WITH-FIXES**(F1…F6);两件判定皆回填原件
- 修复:F-1 丢弃接缝改选择器自报计数 · F-2 删 ordinal 死代码 · F-3 闭集基数守卫 · F-4 单 bool 可执行影子 ·
  F-6 cref 订正 · F-8 零出现 `ModalId` · F-9 正向对照反空转门
- **变异证明 6 项全可红**:MUT-A(删模态门 → 2 红)· MUT-B(排队替代丢弃 → 2 红)· MUT-C(清全位图 → 3 红)·
  MUT-D(闭集 +1 员 → 1 红)· MUT-E(生产引用 `ModalId` → **编译错**,方向性实证)· MUT-E2(代码级串 → 3 红)

### 未闭登记(NOT-RUN,禁借绿)
- NR-1 方向① 生产实现体(归 10)· NR-2 方向② 适配器(归 42)· NR-3 AC-4-10 3 侧扫描(归 story-006)·
  NR-4 动态第 8 屏夹具 · NR-5 运行期消费者接线

### 待办
- ✅ 收口提交推送
- ⬜ story-006(数据契约构建期校验与呈现/手柄验收面)—— interaction-system 下一件


---

## story-006(数据契约构建期校验与呈现/手柄验收面)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`InteractionKindTable.cs`(`KindContractRow` + `DurationOwnerKind` + `InteractionKindTableValidator` 六条校验
  + **`KindPriorityTable`** 第二键唯一真源)· `RoutedSystems.cs`(被路由系统集单一来源)
- **测试**:`data_contract_validation_test.cs`(**13 条**:6 条 DC 夹具 + 4-DC-4 W/H/D + 4-DC-6 归属方 + 4-DC-3 行⟷表
  + AC-4-21 机器代理 + 反空转门 + 独立性)
- **评审原件**:`production/qa/evidence/review-interaction-story-006-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s006-final.xml` = **116 / 113 passed / 0 failed / 3 skipped**(3 = story-004 NOT-RUN 机检)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING 死代码;F-2…F-9)· **QA 侧 REJECT(AC-4-15)**(F-0 skip 归因更正;F-1…F-7)
- 修复:**真表单一来源接线**(F-2,消除「校验的表 ≠ 选择的表」)· 补 4-DC-4 W/H/D 半边(F-1/F-2)·
  `DurationOwner` 拆形态 + id 并断登记(F-3)· `RegisteredSystems` 单一来源(F-4/F-5)·
  **AC-4-21 机器代理**落地(F-3)· story 登记 NOT-RUN + 实际夹具数(F-6/F-7)
- **连带定向修复 story-002**:真表接线后 `target_selection_test` 4 条断言方向相反 ⇒ 按真表更新(MUT-7 先复现)
- **变异证明 10 项全落盘**:MUT-1…6(六条 DC 逐条可红)· **MUT-7**(接线真表 ⇒ story-002 恰 4 红,证接缝真实)·
  MUT-A(4-DC-4 W/H/D)· MUT-B(4-DC-6 归属方)· MUT-C(4-DC-3 行⟷表)

### 未闭登记(NOT-RUN,禁借绿)
- NR-1 装载器/烘焙接线(归 **story 007**)· NR-2 `KindPriorityTable` 真表(数值轮)·
  NR-3/4/5 三条 `[L]` 走查(可玩构建)· NR-6 4-DC-6 对 17/20 的真实时长登记(`OQ-17-3`)

### 待办
- ✅ 收口提交推送
- ✅ **interaction-system Epic 收口**(6/6 Complete · 2026-10-04;未闭项已显式登记)
- ✅ **story-007 装载接线收口**(NR-1 已闭 · 2026-10-04;见下)

---

## story-007(interaction_kinds 烘焙接线 · NR-1)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`InteractionKindBinder.cs`(阶段 2 绑定 + **唯一 `Validate(...)` 调用点** —— 消解 story-006 F-1 死代码)·
  `InteractionKindBaker.cs`(仓根种子 → 产物)· `InteractionKindCookedWriter.cs` + `InteractionKindCookedCodec.cs`(镜像编解码)·
  `InteractionKindBinderProbe.cs`(测试可见薄转发)· `DataBakeMenu.BakeInteractionKinds`(菜单调用点)
- **测试**:`interaction_kinds_bake_test.cs`(**19 条**)· `tests/unit/interaction/fixtures/`(15 夹具 / 11 负)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING = 首轮自造 4-DC-4 ③)· **QA 侧 REJECT**(无 BLOCKING 安全洞;F-0/F-3/F-6/F-7 MAJOR)
- 修复:**删自造判据 ③**(收回 4-DC-4 ①②,与 GDD 一字对齐)· **4-DC-4 ② 接生产接缝**(W/H/D 只注入 `SlotLinearKey` 行 + 行内自报)·
  **维度随产物落盘**(writer/reader 头部扩展,(D) 改读 `ds.*`)· **ConfigVersion 测试前置守卫** · **跨会话范围订正**
- **变异证明**:MUT-A(删 `Validate` 调用 ⇒ 7/7 负夹具红)· MUT-B′(4-DC-4 ② 承重)· MUT-F7(维度落盘可证伪,`unity/Logs/s007-mut-f7.xml`)
- **评审原件**:`production/qa/evidence/review-interaction-story-007-2026-10-04.md`

### 未闭登记(NOT-RUN,禁借绿 —— 覆盖缺口,非安全洞)
- 4-DC-4 ①(类型面恒真不可达)· 跨会话逐位一致(只证同进程)· 4-DC-6 归属方未登记半边(编译期常量无注入点)·
  `schema_version` 交叉一致性(守卫短路)· 聚合多错纪律 · 4-DC-6 对 17/20(`OQ-17-3`)· §三其余绑定层规则

### 待办
- ✅ 收口提交推送
- ⬜ interaction-system 7/7 全闭;下一系统见 Phase 2 关键路径

---

## diagnosis-system story-004(F-8.3 阴性把握度与不泄漏不变量)—— ✅ 收口 2026-10-06

### 交付物
- **生产**:`DiagnosisNegativeConfidenceBinder.cs`(阶段 2 绑定 + **唯一校验点**)·
  `DiagnosisNegativeConfidenceBaker.cs`(仓根种子 → 产物)· `DiagnosisNegativeConfidenceCookedWriter.cs` +
  `DiagnosisNegativeConfidenceCookedCodec.cs`(镜像编解码)· `DiagnosisNegativeConfidenceBinderProbe.cs`(薄转发)·
  `DiagnosisNegativeConfidenceTable.cs`(定表 + 求值器,运行期读侧)· `DataBakeMenu.BakeDiagnosisNegativeConfidence`(菜单调用点)
- **作者态**:`assets/data/diagnosis_negative_confidence.json`(5 合成旋钮)
- **测试**:`confidence_leak_test.cs`(**39 条**)· `tests/unit/diagnosis_system/fixtures/neg_conf_*.json`(**14 个** / 9 负)

### 实跑
- filter `unity/Logs/story004-fix2.xml` = **39 / 39 passed / 0 failed**
- 全量 `unity/Logs/story004-fix3-full.xml` = **2669 / 2622 passed / 0 failed / 46 skipped / 1 inconclusive**

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **两位评审均无 BLOCKING**(A:1 MAJOR · 5 MINOR · 4 NIT;B:APPROVED WITH SUGGESTIONS · 1 MAJOR · 3 MINOR · 4 NIT)
- 修复:**MAJOR-1** AC-8-12 (b) 承重断言恒真 ⇒ 改证接缝实参表 + 反向响应(登记口径替代)·
  **MAJOR-2** 消重 `Q16One`(唯一换算出口 `Table.RawToFloat`)+ `test_q16one_matchesFixCanonical` 逐位锚 `Fix.ToFloat()`·
  `CurveAt` 退化表**硬失败**(原静默 `0f` 会伪装 C-3 报警)· `schema_version` 构建期**同源**校验(堵「漏到运行期装载」)·
  AC-8-14 扫描面扩至类型名/属性名 + 非空守卫 · AC-8-10 删不可达分支 · AC-8-18 去 `??` 静默回退
- **变异证明**:MUT-A(权重路径)7 红 · MUT-B(去 clamp)首轮 0 红 → 补测后**恰 1 红** ·
  MUT-C(去极性门)首轮 0 红 → 补 `neg_conf_high_fallback.json` + 测后**恰 1 红** · MUT-D 恰 1 红
- **金标重钉**:`f75a8170` → `a7bfec31`(story-004 四常量)→ **`72db379c`**(修复轮消重少一行)
- **评审原件**:`production/qa/evidence/review-diagnosis-story-004-2026-10-06.md`

### 未闭登记(NOT-RUN,禁借绿 —— 覆盖缺口,非安全洞)
- AC-8-F5 跨平台三格矩阵(ADR-012 未实跑;只证 Mono 侧自洽)· AC-8-16 跨进程半边(只证同进程 N ≥ 10⁴)·
  AC-8-13 端到端(依赖 9 侧 `Project` 未落地)· AC-8-35 曲线参数半边(ConfigVersion 覆盖)·
  `ReadF32` 不拒 NaN/Inf(结构性不可达)· codec 不校验 `skillCap == SKILL_CAP`(姊妹件同形,须两处同改)·
  运行期/编辑期镜像无机械约束(技术债)· `DiagnosisReadFloorBinder.ReadSchemaVersion` 同形缺口(归 story-003 后续轮)

### 待办
- ✅ 收口提交推送
- ⬜ diagnosis-system 4/4 全闭;下一系统见 Phase 2 关键路径

---

## 2026-10-06 · prescription-medication story-003 收口

### 任务
「开工003」—— `DrugTreatmentApplied` 构造、零病名与写者独占(Integration / 8h)。

### 交付
- **新建门**:`unity/Assets/Editor.Tools.Gates/PrescriptionWriterGates.cs`(AC-11-22 写者独占 /
  AC-11-10 算法独占 + 引用面 / AC-11-01① 病名面 / AC-10-06b 载荷成对)
- **接线**:`AssemblyGates.cs` —— **菜单**(`RunMenu`)+ **构建前门**(`BuildGate.OnPreprocessBuild`)
- **测试**:`unity/Assets/Tests/EditMode/PrescriptionMedication/drug_event_test.cs`(**26 条**)

### 实跑
- filter `unity/Logs/drug_event_final.xml` = **155 / 155 passed / 0 failed**
- 全量 `unity/Logs/full_editmode_story003_final.xml` = **2916 / 2869 passed / 0 failed / 1 inconclusive / 46 skipped**

### 单轮双代理评审(承「评审只做一轮」)→ 修复轮
- **结构侧**:1 BLOCKING · 3 MAJOR · 2 MINOR · 2 NIT
- **QA 侧**:1 MAJOR · 3 MINOR · 2 NIT · **6 变异(MUT-A…F,其中 MUT-B / MUT-D 存活)**
- **B-1 全闭**:`IsBannedName` 对**属性访问器调用点恒假阴性**(`get_Indications` 无词边界)
  ⇒ 增 `get_`/`set_` 前缀展开 + 原测**重写**(阳性对照改打真调用点 `Editor.Tools.Bake`)+ 新增单元判据
- **M-1 全闭(两侧独立重合)**:门补接 `BuildGate` + `CheckSimReferenceFace` 补阳性对照
- **M-2 全闭(显式记账式)**:`PayloadCodec` 移出生产白名单 → 新立 `DecoderCtorSites`,
  `RunAll` 摘要报「⚠️ 判据-文本背离」;**AC 文本收窄归 producer / TD**
- **M-3 全闭**:`DiseaseNameForbidden` 补 GDD 逐字点名的 `DiagnosisResult`
- **MUT-A 的真实事故版**:修复前门只扫 `newobj`,而载荷是 **struct**(C# 发 `call .ctor`)
  ⇒ 站点集恒空、白名单「零命中」误报 **3 测红** —— 已改双指令收口

### 未闭登记(NOT-RUN,禁借绿)
- AC-11-22 的「影子装配注入」半边 · **AC-11-22 / TR-prescription-018 的文本收窄**(归 producer / TD)·
  `CheckPayloadPairing` 不覆盖 codec tag 序 · AC-11-01① 的 `DiagnosisResult` 反射面(8 侧未落型)·
  AC-11-15 三格矩阵与跨进程半边 · AC-11-10「双路径对拍」降级为 AC-10-06b 逐位比较 ·
  AC-11-22 的 7a 白名单本体(BLOCKED-BY-7a)

### 评审原件
`production/qa/evidence/review-prescription-story-003-2026-10-06.md`

### 待办
- ✅ 收口提交推送
- ⬜ prescription-medication epic **5/5 全闭**(story-005 走查半边仍 NOT-RUN)


## 2026-10-07 — story-001 DC-2/DC-6 校验机制落地(已提交 6fdd86b)

- **交付**:`PrescriptionActionIdRegistry.cs`(新)+ binder 接线 + 覆盖率记账 + 菜单侧生产报出。
  **判据本体维持 NOT-RUN**(处置 id master 未登记 · `NOISE_BAND_9` 未立 BL-2)。
- **测试**:filtered **174/174/0** · 全量 EditMode **2935/2888/0 红**。
- **双代理一轮**:代码面 8 条(1 高/3 中/4 低)+ 测试面 4 条必须修 —— 全部处置或显式登记。
- **突变验证**:5 条新守卫注入改坏点 ⇒ 全部实测红 ⇒ 还原。
- **评审原件**:`production/qa/evidence/review-prescription-dc26-shadow-2026-10-07.md`。
- **登记的两条已知弱点(不静默)**:M1 反守卫用干净夹具 · M4 正向判别力过窄。
- **仍未闭**:DC-6 对当前数据集零求值(`salicylic_acid` 的 `dose_range = null`)。


## 2026-10-07 — disease-simulation story-007 重开轮闭环(处置轴 + NOISE_BAND_9 真源)

- **交付**:`assets/data/disease_action_axis.json`(单一 master:处置轴 + treatable_by)·
  9 侧烘焙三件套 + Validator(9-DC-1…7)· `NOISE_BAND_PROGRESS_9` / `NOISE_BAND_POTENCY_9`
  双常量(值 = 100 raw,用户裁定)· 11 侧 `PrescriptionActionIdRegistry` 真源接线 ·
  10 侧 `ValidateActionIdFromAxis` 接线点 · `architecture.yaml` 幽灵引据订正 ·
  D-9-J / O-11→9 结案 · `salicylic_acid` action_id 1→10。
- **双代理一轮**:原判代码面 CHANGES REQUIRED(2B/6M/6m/3n)+ 测试面不予通过(S1×5 借绿…)。
  **修复轮全落**:B1 聚合化(消息聚合 + 首个违规规则号,保测试契约)· B2/M1/M4/M6 ·
  S1-1/1-2 真源读烘焙产物(⚠️ repoRoot 曾用 Assembly.Location+5层.. 落错,改
  `Application.dataPath/../..` 同 DataBakeMenu 先例)· S1-3 接线点 + 真源测试 ·
  S1-4 canary 验行为 · S2-1/2-2 补 DC-1/DC-2 负夹具 · S2-3 判**误报**(4 红系突变短路副作用,
  复验 1 红与结构一致)· S3 全修 · S4-1/4-2 补 AC-9-11…13 / AC-9-17 测试。
- **测试**:9 侧 **67/67** · 11 侧 **174/174** · 10 侧 **116/116**(+4 skipped)·
  全量 **2948/2901/0 红**(基线 2935/2888,+13 = 本轮新增)。
- **突变复验**:9-DC-1…7 全红;10 侧 DC-4 真源点首跑**突变存活** ⇒ 补反向断言(轴外 id=12)
  ⇒ 复跑红 ⇒ 还原绿(评审式自查抓到判别力缺口)。
- **评审原件**:`production/qa/evidence/review-disease-action-axis-2026-10-07.md`。
- **已知弱点(不静默)**:DC-4 生产烘焙侧接线归后续 story(`ValidateActionId` 仍影子)·
  R1-19 = NotImplementedException 哨兵 · DC-6 当前数据集零求值 · MINOR/NIT 登记不修 ·
  `清创`(O-9→10)归 10 GDD 轮。
- **状态**:disease-simulation epic **7/7 全闭**。

## 2026-10-07 — Phase 2 三层账目回刷 + 生产代码口径二次订正(✅ 提交 `4d1bfc0`)

- **回刷(第一轮)**:EPIC 头行 ×3(patient/case/diagnosis)+ case 4 张 story 卡 + index 5 行 + sprint-04(头行/Phase2 表/关键路径图/:176 待办行)—— 纯账目对齐,零实现改动。
- **二次订正(生产代码口径复查)**:patient-ai **实为 4/4 实现全闭**(004 = `0b6f948` 2026-10-05:37 测试 + 5B/6M 修复轮 + 走查证据,账面从未回刷)⇒ 13 行改「4/4 + 原件 caveat」。
- **真未做的 4 个 story(已核,token/测试/commit 三零)**:diagnosis-005(ReadingFSM/JudgmentFSM 零命中)· diagnosis-006 · case-005 · case-006。
- **新立治理缺口**:`review-patient-ai-story-004` **评审原件缺**(001/002/003 均在库)—— 须**补做一次评审(评当下,不追认)**方可转 Complete;已登记进 patient-ai EPIC 头行/卡/index/sprint。
- **Phase 2 现状**:4/7 实现全闭(13 含 caveat)+ 2 有残余(8/37 各 4/6)+ 1 未达 DoD(11)。
- **当前关键路径断点 = `diagnosis/story-005`**。
- ✅ 本批账目文件已由 `4d1bfc0` 提交(远端)。
## 2026-10-07 — OQ C 类轮(6 条裁定 + 一条自噬险礁)

- **提交**:`71292c8`(8 文件:medical-consequences / review-log / skeuomorphic-ui /
  systems-index / tr-registry / traceability-index / EPIC / story-003)。
- **OQ-53-2(DelayTable 值)** —— ⚠️ **第一版值自己违约**:原写 `PatientLives → 0` 特例,
  而 `AC-53-06` / story-003 明写「每条 > 0,≤0 构建期 throw」。0 在域里 = 破 BLOCKING。
  解 = **收窄定义域为回响轴三分立值**(结局轴由 9 结算、53 只读,物理上不走这张表),
  不是给结局轴开豁免。值:`Residual 600` / `Recurrence 1200` / `TrialHistory 2400` @20 Hz。
- **OQ-53-5**(取值集已在正文定名,原行把答案记成问题)· **OQ-53-6**(载体 = 13 + 在场 NPC)。
- **OQ-42-9**(读数条最小观感)—— 三问逐答,新增走查级 `AC-42-C9`(ADVISORY);
  穿墙 = 已接受的 P0 限制,禁补遮挡剔除。AC 计数 43 → **44**(38 BLOCKING 不变)。
- **OQ 未结 36 → 34**(本轮 -2 净:53-2 / 53-5 / 53-6 / 42-9 四条裁定里,53-5 与 42-9 早已在前轮以「答案在正文」形式处理过登记口径,故按表内行数计净 -2;复扫 `design/gdd/` 未划除 OQ 行 = **34**,含 4 行在 reviews 子目录的历史留档)。
  ⚠️ 剩余 30 条**大半自带 deadline**(P1a / P1b / 实现期 / playtest / content 批),
  真「P0 前必须裁」的只剩少数:`OQ-1-12`(接地模型 spike)· `OQ-42-4`(首帧焦点 spike)·
  `OQ-11-3`(等 `O-11→21a`)· `OQ-17-10`(CDF walk 对拍,等 ADR-012 CI)。
- **下一步候选**:`OQ-11-3` 的前置只剩 `O-11→21a`(21a 重开:落断言 + 声明 `drug_potency` 域)
  —— 这是唯一「一个 ADR 动作解一串」的杠杆点。

## 2026-10-07 — **patient-ai story-004 补做评审**(评当下,不追认)—— ✅ 已闭环

> 协议逐字执行:**补做评审(双代理单轮)→ 修复 → 复跑绿 → 收口提交推送;评审只做一轮**。

- **双代理评审**(单轮,均 CHANGES REQUIRED):
  - **测试面 15 条**(1 BLOCKING + 7 MAJOR + 4 MINOR + 3 NIT):B1 程序集负夹具恒真/断言倒置 ·
    M2 IL token 不命中具体类型 · M3 A5 方法体/静态私有盲区 · M4 spasm 同机器 · M5 case 流装饰 ·
    M6 AC1 代理未登记 · M7 V8 桩恒真 · M8 走查件双错 · m9/m10/m11/m12 · n13/n14/n15。
  - **结构侧 12 条**:S1/S2/S4 `ResetForLoad` 非全重置 + 「重新求值」无路径 + 三处口径不一致 ·
    S3 LOD 死链 · S5 唯一消费点双主张 · S6 `IsVisible` 恒真 · S7 平行 Material · S8/S10/S12 通过 ·
    S9 未核面由 M3 补齐 · S11 随 S1 消除。
- **修复落笔**:生产 3 文件(`PatientSpatialDirector` Clear 三件 + doc · `PatientBehavior` 导演 Clear +
  Material 注释 · `PresentationProjection` 删 IsVisible)+ 测试 2 文件(`reconstruction_and_write_path_test`
  B1/M2/M3/M4/M5/M7/m9/m10/m11/n15 · `spatial_behavior_test` B4 重写);
  **文档轮**:走查件整体重写(档1 判据订正 Idle⇒Begin + 五档落点改真身测试 + 签署行)· story 卡回填
  (Status Complete / AC 勾选+代理登记 / Test Evidence `[x]` 真身路径)。
- **复跑绿**:**PatientAI 175/173/0 红/2 跳**(`patientai-fix-2026-10-07.xml`,基线 168/166,+7)·
  **全量 2955/2908/0 红**/1 inconclusive/46 跳(`editmode-full-2026-10-07.xml`,基线 2948/2901,+7;
  inconclusive = 音频 mono 出厂默认既有项,CLI exit 2 系其所致)。
- **登记不修(不静默)**:S3 LOD 死链 · S7 平行类型 · n13/n14 · TC-4 迁移腿(归 7a)·
  AC3 端到端(BLOCKED-BY 10)· AC5(BLOCKED-BY 45)· AC6(EXTERNAL)· [L] 整体签署(待 42/44)。
- **评审原件**:`production/qa/evidence/review-patient-ai-story-004-2026-10-07.md`(原判定 → 修复落点 → 验证命令)。
- **账目**:patient-ai EPIC 头行/004 行 · `index.md:32` · sprint-04(四处)全部摘 caveat ⇒ **13 epic Complete ✅**。
- **当前关键路径断点 = `diagnosis/story-005`**(13 全闭,8 的 005/006 解锁)。

### 追记 · 边界评估轮(同日,用户指令「三个如实边界调用子代理评估修复」)✅ 闭环

- **双席只读评估**(lead-programmer 结构面 + qa-lead 测试面)→ 判定:可修 7 / 真不可修 5 / 维持 2。
- **修复落笔(`75754e9`)**:S7 孤儿 `PatientMaterial`+`Material()` 删除(零生产调用方,原「需 44 契约」理由证伪)·
  AC6 空体补 `Assert.Fail` 防借绿(与 M7 同型首轮漏项)· 补具体类型 IL 负夹具(M2 回归守卫 —— 原接口夹具对
  新旧 token 均命中 = 突变存活)· 卡面五处措辞(Completion Notes 空壳 / AC5 转勾判据 / AC6 拆腿 + CI TODO 桩指针 /
  Test Evidence must-exist 矛盾 / Note 3 未兑现主张)· 走查件 `[ ] Approved` 机器形态 · 原件 §四 两处失实判据订正(B1 真行=编译炸 / M2 判别力零)+ §五 S3 精确化(悬空落点 + F-13.3/13.4 整链死 + 13 零生产构造点根因)。
- **突变实跑(原件 §七)**:批次A 6 注入合跑 → **8 红全点名命中,零存活**;批次B token 回退 → **恰 1 红**(M2 守卫);
  还原复跑 → **175/173/0/2 回绿**。
- **维持登记**:AC5(YAGNI 接缝)· AC8(纯外部)· n13/n14(降级突变代证)· TC-4 归 7a · AC3 归 10 ·
  S3 落点待裁(新 story-005 或挂 tick 接线 epic —— 归 TD 裁)。


---

## 追记 · diagnosis/story-006 收口(2026-10-08)✅ 全闭环

**协议执行**:创建并 unity cli 测试 → 双代理评审(单轮)→ 修复 → 复跑绿 → 原件 → 收口提交推送。

- **生产三件**:`DiagnosisActionLexicon`(S-8.4 路线甲词表,静态零字段,四粗态 Resolve +
  枚举闭集)+ `CasebookScreen.BuildUI`/`Casebook39.uxml` 结构订正(五通道 + 姿态 + 问诊栏第 6 行
  + 病名/置信度归右栏 + focusable 七停按 GDD UI-8.2 2026-10-06 订正面)。
- **测试两件**:`casebook_lexicon_test.cs` 7 测(EditMode:词表/形状/DTO/UXML-XDocument/
  AC-8-38 世界层零引用)+ `casebook_render_test.cs` 8 测(PlayMode,Required evidence:结构/
  焦点序/负向声明树遍历/IModalState 只读/**双真源 cross 断言**)。
- **双代理单轮**:lead-programmer 2 MAJOR+4 建议 / qa-lead 6 MAJOR+7 MINOR+3 NIT → **18 条全处置**
  (修复:枚举闭集 · AssertIntegralShape+消费点绑定 · 类型面零 Gameplay.UI · AC-8-38 新测试 ·
  cross 断言+title 对齐 · XDocument 化 · 禁词 17 token 两面统一 · AC-8-19 降格冒烟 · 「?」放宽;
  登记:AC-8-51 半边拆分 · AC-8-13 陈旧依赖注记 · 焦点序差异卡面回填 · NOT-RUN 族)。
- **复跑绿**:DiagnosisSystem **157/156/0 红/1 跳** · 全量 **2979/2932/0 红/1 inc/46 跳**
  (基线 2978/2931,+1)· PlayMode **11/11** · 突变两笔(Armed 分支失活 / 删 title)各恰 1 红点名,
  还原回绿。
- **金标**:`7ce0ce27` → **`b711a17c`**(+8 = PatientCoarseState 4 + VisitRoute 4 枚举字面,有意识重钉)。
- **原件**:`production/qa/evidence/review-diagnosis-story-006-2026-10-08.md`;证据目录首批
  `production/qa/evidence/diagnosis-system/`(README + 走查件);卡 Status → Complete ✅。
- **NOT-RUN(不静默)**:AC-8-44 手柄 · AC-8-19 像素腿 · [V]/[U] 签核位全未签 ·
  AC-8-51 端到端(具名 37/4 接线轮)· AC-8-13 [I](依赖注记陈旧待回写)· AC-8-23 重排/空行截图 ·
  AC-8-36 遮挡节点断言 —— 逐条见走查件 §四与卡 Test Evidence,禁借绿。

## 追记 · diagnosis/story-005 收口(2026-10-07)✅ 全闭环

**协议执行**:创建并 unity cli 测试 → 双代理评审(单轮)→ 修复 → 复跑绿 → 原件 → 收口提交推送。

- **生产四件**(`Gameplay.Presentation/Diagnosis/`):`DiagnosisReadingFsm`(S-8.2 四态 + 两窗口 +
  **单一完成入口**)/ `DiagnosisJudgmentFsm`(S-8.3 三态 + 关病例三步次序)/ `DiagnosisSnapshotSampler`
  (快照冻结 + 持续刷新,签名无 currentTick = 结构防错)/ `DiagnosisGrowthGate`(`IEventAuthority.IsHost` 门控)。
- **测试两件**:`reading_state_test.cs` 16 测 + `snapshot_staleness_test.cs` 3 测(PlayMode,新建目录)+
  `boundary_guard_test` 新增 gate 负例 1 条。
- **Gates**:`[D-EXIT-GATE]` 谓词(出口唯一合法调用方 = GrowthGate,铁律④静态闭合)。
- **双代理单轮**(lead-programmer 8 条 / qa-lead 10 条,均 CHANGES REQUIRED)→ **18 条全处置**
  (修复:S-1 单一入口 · M1 恒真假流换类型面+影子 · M2 负夹具收编共用机器 · M3 谓词 · M4 API恰三 ·
  M5 三段源 · S-5 真源参数面;登记:S-2/S-3/S-4 语义裁定 · 门控措辞订正 · 金标重钉 · NOT-RUN 族)。
- **复跑绿**:DiagnosisSystem 过滤 **150/149/0 红/1 跳** · 全量 **2972/2925/0 红/1 inc/46 跳**
  (基线 2955/2908,+17)· PlayMode **3/3** · 突变 `[D-EXIT-GATE]` 失活 ⇒ 恰 1 红点名,还原 26/26。
- **金标**:`72db379c` → **`7ce0ce27`**(ReadingForm 4 + JudgmentState 3 枚举字面入面,+7 行,有意识重钉)。
- **原件**:`production/qa/evidence/review-diagnosis-story-005-2026-10-07.md`;卡 Status → Complete ✅。
- **NOT-RUN(不静默)**:AC-8-45 网络子句(BLOCKED-BY-45)· 9/37/39 真接线集成(39 Ready)·
  IsHost 恒 true 下真客户端端到端(归 45/P1b)· [L] 走查归 story-006。
- **卡面语义裁定三条(39 接线前生效)**:S-2 回溯抹除 · S-3 状态判重替代当帧集 · S-4 抑制复查不刷新。

## 2026-10-07/08 — P0 四前置清轮(O-11→30 / OQ-10-7 / OQ-42-4 + OQ-1-12 传导订正)

- **提交**:`ccb3fa8`(O-11→30 半闭)· `bb92d43`(OQ-42-4)· `ba4f244`(OQ-10-7)·
  `70c3f21`(OQ-1-12 传导订正 · push 曾被 GitHub main ref 500 挡,新分支探测证明
  对象上传无阻 ⇒ 服务端瞬时故障,后重推成功)。
- **四条 P0 前置全部处理完**,其中**三条是「假前置」**(方案早已裁/答案早已在正文):
  | 前置 | 真相 |
  |---|---|
  | `OQ-1-12` 接地模型 | 方案甲 **2026-09-29 已由用户裁完**,GDD 3 处 + EPIC 3 处 + story-003 5 处仍在写「待 spike 回填」= 传导滞后 |
  | `OQ-42-4` 首帧焦点 | 原问把**描述性问题**(引擎默认是什么)当**规范性问题**(应该如何);spike 答不出规范,判据改写为不变量后**不需等 spike** |
  | `OQ-10-7` 急救乘子落点 | 「住 3 的动作资产侧」**结构性不可行**(`.inputactions` 是引擎资产,装不了 `Fix` 字面量,ADR-014);「3 补 Amendment」= 空转义务 |
  | `O-11→30` 等级→EFF | 唯一真欠,但只欠**形状**;已裁 = 单调阶梯 + 两端对齐 21a + 省料不改 `dose`,逐档填表归 30 自己数值轮 |
- **两个抓到的真缺陷(比「清了 OQ」值钱)**:
  1. **引擎 `skinWidth` 默认值本机无权威件**(`PhysicsModule.xml` 只有 summary)⇒ 方案甲的
     成立前提 `0.015 > skinWidth` 不能靠注释 ⇒ 新立 **`AC-1-34`**(装载期断言)。
  2. **`AC-11-11` ④ 的算术错**:原稿「`2^47 × 2^16 = 2^63` ⇒ int64 合法」——
     signed int64 上确界是 `2^63 − 1`,**恰好溢出 1**;真上界取决于 `dose ≤ 7` ⇒ 安全。
- **新增三条 AC**:`AC-1-34`(下压量不等式)· `AC-42-C10`(首次导航必落焦)·
  `AC-21a-38c`(`drug_potency ∈ (0, 2^47]`);`AC-21a-38b` 尺 `> 0` → `≥ 100 tick`。
- ✅ **2026-10-08 `AC-42-C10` 已交付并跑绿**(集群 2/2 Passed)——
  - 测试:`unity/Assets/Tests/PlayMode/SkeuomorphicUI/ac42c10_focus_landing_test.cs`(两条:主断言 + 反向守卫)
  - 证据:`production/qa/evidence/ac42c10-2026-10-08.md` · run-book:`production/desktop-ac42c10-runbook.md`
  - **三条结构性发现**(已回填 GDD `AC-42-C10` 段 + E-14 归属):① `SendEvent` 只跑用户回调阶段,
    引擎默认不落焦;② 默认动作阶段 = `protected internal` ⇒ 测试侧**不可达**(别再尝试);
    ③ `IFocusRing.GetNextFocusable` 在「当前焦点 == null」的 GIVEN 上拿不到起点。
  - **定位改写(不重开裁定)**:测试守的是**不变量**,断言对象 = **桥的响应式兜底**,不是引擎默认。
  - ⚠️ **未结(显式记账)**:落焦执行体是测试自带 BFS,**不是** `FocusNavigationBridge`(它还没实现这条路径)
    ⇒ 现证的是「不变量成立」,**不是**「桥实现了它」。
  - ⏳ 桌面复跑待做(集群 `-nographics` 无渲染;桌面须配 `themeStyleSheet` 否则焦点高亮不可见会误导走查)。
- **未结 OQ**:31 条,其中**仅剩 `OQ-1-12` 的 ε 实测**是真 P0 前置(归【桌面】PlayMode,
  不阻塞文档);其余自带 P1a/P1b/实现期/数值轮 deadline。
- **下一步候选**:①【桌面】PlayMode 跑 `AC-1-21` 的 ε(**`AC-42-C10` 已不需桌面跑判据,
  只需复跑留痕**);② 11/9/3/25 各 GDD 的 review-log 与本轮裁定回写;③ 21a 的 F5 偏移可感知地板
  (判据须写成**逐药求值式** `|potency × [ (H+off)(1−e^{−W/(H+off)}) − H(1−e^{−W/H}) ]| ≥ NOISE_BAND_POTENCY_9`,
  不能拍固定 tick 常量);④ `FocusNavigationBridge` 的兜底路径(把它从测试 BFS 换成桥入口,
  见 run-book §7 的四步义务)。

## 2026-10-11 — sprint-06 G3 推进策略裁定(用户 ·「要补齐21a/20的epic吗」→ 最小生产端口实装)

- **问**:G3(处方驱动)端口依赖 21a/20/30 未就绪(ICT 勘察:21a baked 无 dose_range、
  cooked 未烘 drug 档、运行期零装载;20 = 纯接口零实现)。「要不要补齐 21a/20 的 epic?」
- **用户裁定(2026-10-11)**:否 —— 改走「**最小生产端口实装**」(替换 sprint-06 §2 G3 原「结构性半边」降级预案):
  - 组合根装配层内联 **21a 最小 drug 表**(salicylic_acid, DoseRange(1..5) + potency)⇒ 判据「≥2 档 dose 区分」**全判**
  - **20 最小库存面**(内存字典持量 + 扣减,HasPortions/ConsumePortions **真行为**)⇒「无货不能开出变异恰红」**全判**
  - **30 装配**(SkillGrownEmitter 生产路径,成长门正跑)⇒「不选档零事件」**全判**
  - **完整 21a/20 独立排期**(烘焙装载面 / 库存账本 / 世界流 DropConsumed)**落地后只换端口实现,装配签名零改动**
- **落盘**:`production/sprints/sprint-06.md`(§2 G3 备注 + §4 风险表同批替换)· `production/milestones/README.md`(M2 #2 处方腿悬空段增补裁定)
- **实施(G3)**:新 `PrescribeRequestDriver`(住 Gameplay.Boot;消费 `PrescribeStrokeIntent`→
  `DentchDoseSelector.DoseValueOf`→ 真 `PrescribePorts`→ `PrescribeFlow.Prescribe`→
  `DrugTreatmentApplied` 入病史流)+ 组合根接线 + 端口实装。b7 写者部分旁路(嫁 PrescribeFlow),
  b7 红先清再判 >0(真表带 author 比较)。判据 ≥2 档 variant + 不选档零事件 variant(新驱动测)。

## 2026-10-11 — sprint-06 G3 处方驱动 ✅(EditMode 3180/0 · PlayMode 132/132)

- **G3.1 `PrescribeRequestDriver`**(新 · Gameplay.Boot):消费 `PrescribeIntent`(全整数,与 42
  戥子同域翻译)→ `DentchDoseSelector.DoseValueOf`(档序数→档值唯一量化点)→ 真 `PrescribePorts`
  → `PrescribeFlow.Prescribe` 五步 → `DrugTreatmentApplied` 入病史流。零自持状态(不缓存 selected 档)。
- **G3.2 端口生产实装**(新 · Gameplay.Boot/PrescriptionPorts.cs,用户裁定「最小生产端口实装」):
  21a `MinimalDrugCatalog`(salicylic_acid · DoseRange(1..5)· potency 2^31=1/2)·
  20 `MinimalPortionsStore`(内存持有量 + HasPortions/ConsumePortions 真行为)·
  30 `AssemblySkillGrowthPort`(SkillGrownEmitter 载荷经 IPayloadEncoder 编码 + Append 落病史流 = 生产写者)。
- **组合根接线**:装配 4 新件 + 服务袋 3 新属性(PrescribeDriver / PrescribePorts / PortionsStore)。
- **b7 门**:豁免表撤 SkillGrown(写者落地同批撤,豁免=红);测试断言 3 处增强
  (DrugTreatmentApplied 写者面 · SkillGrown 不豁免 · 陈旧豁免探针改点名 CareApplied)。
- **判据 8 测全绿**:选档落笔入病史流(载荷五字段 + F-11.1 dose_potency 量纲)→ ≥2 档不同 dose(1.0 vs 2.0)
  → 不选档零事件 → 无货拒开(变异恰红)→ 未知药零事件 → 档越界/tick<0 fail-loud → GateHit SkillGrown 入流。
- **踩坑**:① 首次编译红 = CompositionRootServices 缺 `using Sim.Prescription`(PrescribePorts 引);
  ② Potency 量纲(2^31 而非 65536/2 —— F-11.1 分母 DOSE_BASE 归一,Q16.16 语义,注记留档);
  ③ 早期断言错用 `Stream.Count` 总流数(一次落笔产 2 条:④DrugTreatmentApplied+⑤SkillGrown)⇒ 改按 Kind 计数。
- **未结(完整 21a/20)**:烘焙装载面(AddressablesDataProvider 新数据集 / DrugProfileCodec)· 库存账本 ·
  世界流 DropConsumed 消耗写者 —— 独立排期,落地后只换端口实现、装配签名零改动。
- **下一步**:G4 急救手动化(移除自动触发 + 玩家手动链 + 候选列表生产实例化;红线 = emergency_attempt_wiring_test 必须改造)。

## 2026-10-11 — sprint-06 G4 急救手动化 ✅(EditMode 3185/0 · PlayMode 132/132)

- **G4.1 自动触发移除**:`EmergencyAttemptDriver._triggeredPatients` 自动分支**删除**,
  驱动退役为占位壳(保留 BootRoot 调用面 + fail-loud,零 Append/零状态)。
- **G4.2 手动入口**:新 `EmergencyManualDriver`(Gameplay.Boot)—— 玩家意图 →
  HostEmergencyProcessor.Process → EmergencyAttempt + EmergencyTreatmentApplied 双事件入病史流;
  method 闭集 {0 Manual,1 Skip} · actorId 由调用方传入 · 最小动作表(止血包扎)。
- **红线处置(qa-lead 注)**:`emergency_attempt_wiring_test` **反向改造** —— 旧断言
  「启动即自动两事件」= 假绿;现断言**零输入观察窗 ⇒ 病史流零 EmergencyAttempt**(守死自动触发,
  重新引入即红)+ 手动入口 ⇒ 双事件(新增第二测)。
- **组合根**:装配 EmergencyManualDriver + 服务袋暴露;退役壳保留引用面。
- **判据(新 5 测 EditMode)**:零输入 20 tick 零事件(双 Kind 都不发)· 手动双事件入病史流(病人归因
  + 流路由 History)· Skip ⇒ method 载荷=1(流上可判别 R-6)· method 越界/负值 fail-loud · tick/actorId 负值 fail-loud。
- **踩坑**:① 测试首跑 CS0246 = 测试程序集缺 Sim.EmergencyProcedures 引用,且旧写法直接 new
  HostEmergencyProcessor ⇒ 改从组合根 `_bag.EmergencyManualDriver` 取(生产装配路径);
  ② PlayMode 改造后 wiring 测新增手动链第二测(等在场 → TryManual 反射 → 双事件断言)。
- **下一步**:G5 收口(全量 EditMode≈3185/PlayMode 132 + 第四轮人工 playtest + M2 Exit #2/#8 复评)。

## 2026-10-11 — sprint-06 G4 收尾(全绿)

- **EditMode 3185/0/1/46**(G3 8 测 + G4 5 测 = 全量 +13)· **PlayMode 133/133**(wiring 测 1→2 条,
  零输入窗口 + 手动入口)。两次踩坑:① 测试 CS0246(测试程序集缺 Sim.EmergencyProcedures 引用,
  且应取组合根 `_bag.EmergencyManualDriver` 而非手 new);② PlayMode `FirstPresentId` 反射
  `PresentPatientIds` 用 GetProperty ⇒ null(它是接口**方法**),改 GetMethod。
- **sprint-06 进度**:G1 ✅ · G2 ✅ · G3 ✅ · G4 ✅;剩 **G5 收口**(全量门已绿 · 第四轮人工 playtest ·
  M2 Exit #2/#8 复评)。

## 2026-10-11 — sprint-06 G5 收口(复评 + 回填;待用户 playtest/提交)

- **复评原件落盘**:`production/qa/evidence/m2-exit-recheck-sprint06-g5-2026-10-11.md`
  —— #2 处方腿**转闭**(PrescribeRequestDriver 生产装配 + 真端口 + 判据 8 测)·
  #5 急救**转闭(手动化)**(退役壳 + EmergencyManualDriver + wiring 反向改造红线守死)·
  #8 **◐ 施加点接大半**(②墨龄上游 JudgmentWriter 已接、④查体分册快照已接;③候选列表
  零生产实例化 + 玩家输入入口未接)· 全量门 EditMode **3185/0/1/46** · PlayMode **133/133**。
- **回填**:`milestones/README.md`(#2 转闭加注 + #5 手动化转闭 + #8 接大半加注)·
  `sprint-06.md` §3 完成句(本 sprint 完成 + 全量门 + 复评原件索引 + 剩余登记)。
- **剩余(诚实登记,非本案故障)**:① 第四轮人工 playtest(用户操作 —— 需编辑器重开 +
  人工走查七环节;当前驱动就绪但**输入入口未接** ⇒ playtest 覆盖查体/落笔/开方/施治需
  先接 Interact/Emergency 键位消费);② 玩家输入入口(键位已有零运行期消费);
  ③ ③ 候选列表生产实例化。均归下一交互轮。

## 2026-10-11 — sprint-06 第四轮人工 playtest(5A 全绿)+ 提交推送

- **报告**:`production/playtests/playtest-2026-10-11-m2-round4.md`(5A 全绿)
  - Q1 病人出现=A · Q2 值区空行=A(输入入口未接,诚实面)· Q3 **零急救=A**(G4.1 红线人工半)·
    Q4 移动/相机=A · Q5 纸面=A
  - 无本轮新 bug;输入入口/候选列表 = 登记缺口非 bug
- **提交推送**:`8457d5b`(G3+G4+G5,23 files / +1169 −162;垃圾 blob 未含,commit 尾行按要求)
- **下一步候选**:玩家输入入口接线(Interact/Emergency 键零消费 → 接 ExamineDriver/JudgmentWriter/
  PrescribeRequestDriver/EmergencyManualDriver)+ ③ 候选列表生产实例化 ⇒ M2 #8 施加点接完;
  或数值轮 / 词表真装载面。sprint-06 主线闭环。

## 2026-10-11 — sprint-07 内容解锁轮开(结构问题诊断 + 改进措施落盘)

- **用户指出**:项目进展有结构问题 —— 一直灰盒测试不对,要纠正;要求从 9-13 至今分析 active.md 判断是否在轨。
- **全周期在轨分析结论**(9-13 至今 665 commits 重建):**在轨** —— 范围轨零蔓延(M2 判据逐条推进)·
  质量轨 921→3211 测试单调零红 · 两次大账目失真(10-03 进度混计 / 10-08 59 story 失实)都被如实修复。
  **真实风险**:① 日历轨不存在(计划表日期从未回刷,名义超前一个月实为失真);
  ② M4 42 项资产 0/42「美术待裁」= M5 日历唯一阻塞源;③ 账目失真约每 5 天积累一次(周期性)。
- **灰盒测试的结构问题(三层根因)**:内容层(词表零 display_words / F2 Signs 恒空)· 输入层
  (Interact/Emergency 键零消费)· 判据层(因 1+2 退化为「零/空/无」断言)⇒ 核心循环从未被
  **真实内容**驱动过一次。绿得太容易 = 测「系统不崩 + 空态正确」,非「玩法可玩」。
- **改进措施**:sprint-07 内容解锁轮 —— S1 词表/门槛真装载(Bake + Addressables + 组合根注入,
  判据 = 查体后值区**有词条**)· S2 玩家输入入口(E/F 接 4 驱动,判据 = 按 E 值区有词 /
  落笔墨龄>0 / 开方体征变化 / F 双事件)· S3 候选列表生产实例化 · 结构纪律 C1 头部必刷 /
  C2 美术待裁排期 / C3 评审门回归常态。
- **美术提前提案(待用户裁)**:甲案 = 仅提前 P1 病人真模型 + P2 施治动作真资产(核心假设
  「判断→施治好玩」的证伪前提);乙案 = 推翻形态优先全提前(不推荐)。纸面五族 16 张真图
  已入库被 USS 引用,零成本不重做。
- **落盘**:`production/sprints/sprint-07.md`。

## 2026-10-11 — sprint-07 S1 完成(词表/门槛真装载 · 全量绿)

- **S1.1 bake**:`diagnosis_signs.cooked.bytes`(34 行)+ `diagnosis_read_floor.cooked.bytes`(61 档)
  落 `Assets/DataCooked/`,字节确定性烘焙;**入库**(先例 item_database 两产物同款)。
- **S1.2 装载面三件套**:
  - `AddressablesDataProvider` 增两数据集装载(`SignTableAddress`/`ReadFloorTableAddress`,
    `EnsureSigns`/`EnsureReadFloor`,经 using 别名引 8 产数据集)—— **生产装载路径**;
  - `DiagnosisCookedDisk`(EditMode 兜底,直读**同一份 baked 字节**,零第二真源);
  - `CompositionRoot.Assemble` dual-path(provider≠null ⇒ Addressables / null ⇒ Disk)+
    `ExamineDriver.SetLexicon(rows, in floor)` + fail-loud 空词表 throw。
- **⚠️ 连带修订 D-ESC 逃逸判据**(`DiagnosisBoundaryGates.EscapedDeclPattern`):token 面
  任意位置 → **类型声明位**。原因:S1.2 按 ADR-014 §五 把装载落**边界程序集**,而原判据把
  「引用 8 公开 API」的消费面一并判红 ⇒ 装载层/组合根一引 9 产数据集即红(结构冲突)。
  修订后:非前缀文件**声明** 8 类型 = 逃逸红;**引用/消费** = 合法放行;实现躲出前缀仍有
  IL 层 `matchedTypeCount==0` ⇒ `[D-0]` 红兜底。全仓扫描零「非前缀声明 8 类型」。
- **全量绿(证据)**:
  - EditMode `unity/UnityLogsTmp/s7_s1_editmode_v4.xml`:**3235 = 3188 passed · 0 failed · 1 inc · 46 skip**
    (前红两门 `test_fullGate_currentTree_zeroErrors` / `test_ac816_productionAssembly_zeroPrngCallSites` 转绿;
    S1.2 三测全绿);
  - PlayMode `unity/UnityLogsTmp/s7_s1_playmode.xml`:**133/133 全绿**(exit=0)。
- **.gitignore 增三刑**(不随提交):`unity/UnityLogsTmp/` · `mono_crash.mem.*.blob`(根+unity/)。
- **落盘**:`sprint-07.md` §2 S1 行 + §2.1(判定修订)+ §3(S1 ✅)。
- **下一步**:S2 玩家输入入口(近病人 E → ExamineDriver · 落笔 → JudgmentWriter · 开方 →
  PrescribeRequestDriver · F → EmergencyManualDriver)+ S3 候选列表生产实例化。

## 2026-10-11 — sprint-07 S2 设备级入口批(输入层解锁 · 全量绿)

- **S2.2/S2.5 已接**(接线面注记):
  - 新 `PlayerInputReader`(E/F 按下沿直读,承 `MovementInputReader` 过渡最小面;
    键盘 <E>/<F> + 手柄 buttonNorth/buttonWest,零动作资产依赖);
  - 新 `InteractionInputDriver`(设备级 E → `ExamineDriver.OnExamine` + 切脉案绑定 +
    病史流零新事件(乙案);F → `EmergencyManualDriver.TryManual` 双事件;按下沿 = 上帧
    按住位比对,持键不重复触发;定位源 = 病人视觉实体位置(接线占位,归 6 + 13));
  - `BootRoot` 启动序第 4.5 步装配,Update 在 **PatientVisualSpawner 刷新后 / UI 刷新前**
    转发(次序:体征已 Step → 视觉随在场集 → 玩家意图 → UI 见词)。
- **全量绿(证据)**:
  - EditMode `s7_s2_editmode_final.xml`:**3237 = 3190 passed · 0 failed · 1 inc · 46 skip**
    (新测 `player_input_reader_test` ×2 全绿:无设备读面无假触发 + 驱动空转零事件零副作用);
  - PlayMode `s7_s2_playmode_v3.xml`:**136/136 全绿**(新测 `interaction_input_chain_test` ×3:
    虚拟 E → 查体分册 + 值区绑定 · 虚拟 F → 双事件入流 · 恒按住只查体一次)。
- **⚠️ 键位注入实证(batch)**:键盘 E/F 位**不上**(`KeyboardState.Press` 对这两键位不生效,
  与 W 位有效的同套三件套不同)→ 测试回落手柄 buttonNorth/buttonWest 注入
  (`device_and_presentation_test` 早证浮点/按钮位可靠);探针断言区分「键位不上」与「链断」。
- **S2.3(落笔→墨龄)/ S2.4(开方→处方)登记缺口**:玩家操作入口在 42 侧(方笺落笔 /
  戥子选档确认),需 UI 事件 → 装配层接线;**不做设备级自动行使**(自动开方/落笔 =
  玩家零输入出事件 = 假绿,承「灰盒测不对」根因)⇒ 归下一拍(42 接线轮)。
- **落盘**:`sprint-07.md` §3 改「S2 ◐ 设备级入口批」+ §2 S2 注记;`InteractionInputDriver`
  文件头登记缺口。
- **下一步**:S3 候选列表生产实例化(M2 形态件施加点③闭合)+ S2.3/S2.4 的 42 接线轮。

## 2026-10-11 — sprint-07 S3 完成(候选列表生产实例化 · 全量绿)

- **S3.1 载体生产实例化**:新 `EmergencyCandidateRuntime`(Gameplay.Boot):
  - 实例化 `EmergencyCandidateList` + 喂 10 最小动作标签(止血/通气)+ 跳过入口恒在;
  - `CompositionRoot.Assemble` 装配 + fail-loud + 进服务袋(`EmergencyCandidates`);
  - `BootRoot` 第 6.5 步挂载到 UI 根(经 `SkeuoRuntimeDriver.GetUiRoot()`);
  - **观测面刻意不暴露 Gameplay.UI 类型**(只 `Root` = UnityEngine.UIElements 全名 +
    结构化只读)—— 测试装配(Sim.Contracts.Tests)无 Gameplay.UI 引用,CS0234/CS0012
    两轮实测已消除。
- **范围注(未假装完成)**:动作判定 / Idle 可达时序 / **跳过入口真的能跳过**的交互面
  归 10 + 数据绑定轮(承 story-023 边界①)⇒ 不上自动跳过(玩家零输入出事件 = 假绿)。
- **全量绿(证据)**:
  - EditMode `s7_s3_editmode_v3.xml`:**3240 = 3193 passed · 0 failed · 1 inc · 46 skip**
    (新测 `emergency_candidate_runtime_test` ×3 全绿:装配即实体 + 挂载进 UI 根 +
    生产袋来源/身份稳定);
  - PlayMode `s7_s3_playmode.xml`:**136/136 全绿**。
- **落盘**:`sprint-07.md` §3 S3 ✅ + §2 S3.1 注记 + 收口判据七环节现状表。
- **下一步**:S2.3/S2.4 的 42 接线轮(UI 事件 → 装配层:方笺落笔 / 戥子选档确认)+
  第五轮人工 playtest(此时 S1 词表 + S2 E/F 入口 + S3 候选列表载体俱在 = 第一个
  「有内容的真循环」的目视核对面)。

## 2026-10-11 — sprint-07 42 接线轮完成(S2.3 落笔→墨龄 · S2.4 开方→处方 · 全量绿)

- **S2.3/S2.4 接线宿主**:新 `PrescriptionPageHost`(Gameplay.Boot):
  - 落笔 → `JudgmentWriter.TryRecord`(JudgmentRecorded 入病例流 → 墨龄 = 当前tick − 最近Judgment);
  - 开方 → `PrescribeRequestDriver.TryPrescribe`(DrugTreatmentApplied 入病史流 → 体征链 apply);
  - `SetAuthor`(BootRoot 玩家生成后 · **落笔作者同步**到 JudgmentWriter.SetAuthor —— 7.7 诚实作者);
  - `BindPatient`(InteractionInputDriver.TryExamine 同步,方笺对象 = 当前脉案绑定病人);
  - fail-loud:未 SetAuthor / 未 BindPatient 即动作 ⇒ 抛(装配遗漏不静默);
  - 开方接线期限 1(`_prescribed`,不自动重复行使 = 假绿根因;常驻选档确认 UI 落地后删限);
  - **依赖直传**(EventSink/Encoder/病例流视图/PrescribeDriver/PortionsStore/TickDriver)
    —— 初版传服务袋 `new PrescriptionPageHost(this,…)` 在 static Assemble 内 CS0026
    实测编译红,重构为七依赖直传;组合根装配 + fail-loud + 进服务袋(`PrescriptionPage`)。
- **全量绿(证据)**:
  - EditMode `s7_s23_s24_editmode_v2.xml`:**3246 = 3199 passed · 0 failed · 1 inc · 46 skip**
    (新测 `prescription_page_host_test` ×6 全绿:装配存在 · 落笔→JudgmentRecorded+墨龄=1 ·
    开方→DrugTreatmentApplied · 开方接线期限 1(第二次 false + 恰 1 条)·
    未 SetAuthor/未绑定 fail-loud);
  - PlayMode `s7_s23_s24_playmode.xml`:**136/136 全绿**。
- **范围注(未假装完成)**:本批 = **接线面**(「动作 → 写者」);42 侧方笺页的**交互面**
  (click/焦点/选档确认 UI)仍归 42 —— `PrescriptionPageHost` 即其语义动作的接收点。
- **落盘**:`sprint-07.md` §2 S2.3/S2.4 接线注记 + §3 S2 ◐→✅ + 收口判据七环节现状表更新。
- **下一步**:第五轮人工 playtest(此时 S1 词表 + S2 E/F 入口 + S2.3/S2.4 接线 +
  S3 候选列表载体俱在 = 第一个「有内容的真循环」的目视核对面)+ sprint-07 收口。
  注:本轮 EditMode 两处编译红 —— ① static Assemble 传 `this`(CS0026)重构依赖直传;
  ② 测试缺 `using System`(CS0246 InvalidOperationException)—— 均为本轮引入即修。

## 2026-10-12 — sprint-07 C3 双代理评审完成(恰一轮 · CHANGES REQUIRED)

- **原件**:`production/qa/evidence/review-sprint-07-2026-10-12.md`
  (代码面 lead-programmer + 测试面 qa-lead 并行 · 主会话独立复核 5/5 属实)。
- **总判定 CHANGES REQUIRED**(两面独立收敛):8 项必改 ——
  ① H-1 生产 Addressables provider 死代码(BootRoot 未传,player 构建必抛);
  ② H-2 D-ESC 两绕过(delegate + 同文件双命名空间)+ IL 兜底注释口径错;
  ③ 值区非空零正向断言(真词表端到端无测);④ S2.1 判据悬空未登记;
  ⑤ BootRoot 三接线(OnFrame/SetAuthor/MountTo)零测试;
  ⑥ 急救手动链缺在场前置(被唯一调用方遮住);
  ⑦ 宿主 null 静默降级 + 逐次 Grant 自证库存(双代理交叉印证);
  ⑧ sprint-07.md:157 收口行与 §2 ◐ 账目矛盾。
- **G3/G4/G5 欠账补课**:测试面 APPROVE(处方/急救测试质量高),代码面附 1 中项。
- **闸门**:8 项修复完成前 sprint-07 不得转 Complete(不借绿)。
- **待用户裁定**:F-6(.gitignore 三组条目随 12b4947 入库,与「本地改动永不提交」
  裁定冲突 —— 回滚还是修订规则)。
- **下一步**:修复清单 8 项(原件 §四,含逐项验证命令)→ 复跑 EditMode/PlayMode →
  第五轮 playtest(用户方便时)→ sprint-07 收口。

## 2026-10-12 — sprint-07 收口轮(C1 头部状态 + C3 结案)

- **C1 头部状态必刷**:active.md 头部已改「Sprint 07 ◐ 收口轮」+ STATUS 块已改
  (Epic: sprint-07 · Feature: 收口轮 · Task: C3 已闭合 → 剩 C2 美术待裁 + 第五轮 playtest)。
- **C3 结案**:修复提交 `2ad81ee`(8 项必改全闭合)—— sprint-07.md C3 行已加结案注记
  (逐项闭合 + 复跑全量证据 EditMode 3255 = 3208/0 · PlayMode 136/136)。
- **C2 已裁定(2026-10-12)**:美术提前提案 §6 落盘 —— 用户 AskUserQuestion 取**甲案「P1+P2 全提前」**。
  P1 病人真模型 → `production/epics/patient-ai/story-005-patient-real-model.md`(G-3 patient-ai 2 项 · 3.0 人日 ·
  spec = `design/assets/specs/patient.md`)· P2 施治动作真资产 → `production/epics/emergency-procedures/story-008-treatment-action-assets.md`
  (G-3 emergency 手势 VFX 子集 + batch-03 素材)。两 story Ready ⬜、单列,不进本 sprint 关键路径;其余 40 项维持 M4。
- **下一步**:第五轮人工 playtest(用户方便时;此时应有内容:值区有词 · E/F 可操作 ·
  候选列表可见)→ sprint-07 Complete。当前全量门:EditMode 3255 · PlayMode 136/136。

## 2026-10-12 — 第五轮人工 playtest(sprint-07 收口判据 + Q1 修复)

- **报告**:`production/playtests/playtest-2026-10-12-m2-round5.md`(Build `865eb44`)。
  Q1=🔴(根因已修)· Q2=◐(登记)· Q3–Q7=A。E/F 玩家入口**首次人工实证可用**。
- **Q1 根因 + 修复**:组合根 `queryLevel: 0` 占位 ⇒ 真词表双条件与门 `named = skill ≥ tier_named`
  (tier_named ∈ {1,10,20})恒 false ⇒ 按 E 查体后值区**恒空**。修复 = `CompositionRoot.cs`
  `queryLevel: 0 → 1`(tier-1 词 20 条可读)+ 回归测 `test_q1_compositionRoot_examine_yieldsWords_atInitialSkill`
  (走组合根生产驱动 + 真词表 + position 1.0 ⇒ SignCount>0;改回 0 恰红)。
- **复跑全量**:EditMode **3256 = 3209/0/46skip/1inc**(基线 3255 ⇒ +1 零回归)·
  PlayMode **136/136** · ExamineDriverTest 10/10。证据:`q1_examine_editmode.xml` ·
  `q1_full_editmode.xml` · `q1_full_playmode.xml`。
- **Q2 登记(非隐藏)**:按 F 双事件入流(PlayMode 注入已绿)但**零可见反馈** ——
  P2 story-008(施治真资产)的人工面证据;候选列表交互面归 10 + 42。
- **数值面登记**(归数值轮):skill=1 门槛 ≈0.975 ⇒ 病人低位期查体仍读不出(F-8.1 曲线语义,
  非 bug);「初始档该是几」= 30 QueryLevel × 8 门槛曲线联调。
- **Polish 登记**:USS `font-family` 警告 ×9(UI Toolkit 不识别)· SkeuoRuntimeDriver
  Awake 时序警告(注入后 setter 重绑,非断链)。
- **下一步**:Q1 修复后**人工复验**(按 E 值区出词条)→ 通过 ⇒ sprint-07 Complete;
  P2 story-008 排期启动(Q2 零反馈坐实)。当前全量门:EditMode 3256 · PlayMode 136/136。

## 2026-10-12 — sprint-07 Complete(第五轮 playtest 复验结案)

- **Q1 人工面通过(用户口述)**:按 E 后值区出现多行词条(词条栏多行线 = reading 格 + 线格
  渲染,真词表端到端链人工可感)。两轮修复均人工生效:
  ① `ccdca6a` —— queryLevel 0→1 + RecoveryTime 100→2000(双根因)+ USS 非法属性全清;
  ② `2c9aae9` —— 字体 Regular→Bold(纸纹底可读性)。
- **词条文字清晰度**:用户裁定「记下来后面再做」= **polish 登记**(字号 md 15→16–17px /
  词条格底衬 / 字重微调,归 42 呈现轮),不阻塞 sprint-07 收口。
- **sprint-07 ✅ Complete**:S1–S3 + C1–C3 全绿 + 第五轮 playtest(Q1 双半达成 ·
  Q2=◐ P2 立项依据 · Q3–Q7=A)。全量门 EditMode 3257=3210/0 · PlayMode 136/136。
- **残留登记(不阻塞)**:① 词条可读性 polish(42)· ② F 键零可见反馈(P2 story-008)·
  ③ 落笔/开方交互面(42)· ④ S2.1 动作资产 runtime 装载 NOT-RUN。
- **下一拍(待用户指令)**:M2 Exit #8 复评 / sprint-08 规划 / P2 story-008 排期启动
  (Q2 零反馈已坐实「无真动作感不可判好玩」)。

## 2026-10-12 — sprint-08 开轮(方笺交互面轮)+ S0 前置窄批 ✅

- **规划落盘 `86932cf` + 推送**:四专家评估修订版(producer / lead-programmer / qa-lead /
  technical-director);规模 6–8 人日;S0 前置 + S1 病名册整面 + S2 方笺翻页/戥子 + S3 polish +
  E2E 3 条收口门(七环节全链 BLOCKING)。
- **S0 ✅ 完成(两处文档裁定)**:
  - **S0.1 ADR-011 Amendment C**(§二节首 + Status 头指针):<50ms 直读纪律**仅约束急救动作**;
    拟物 UI 焦点导航与 Submit 归 ADR-013,42 挂 `NavigationSubmitEvent` 合法(TD 甲案;
    乙案 Boot 直读否决留档 —— 破 ADR-013 C3 只读边界)。
  - **S0.2 casebook-39 K 计数 7→8 回写**(§5.2 / §13×2 / §14 五处):折边入 K
    (五通道+问诊栏+病名栏+折边);OQ-CB-5 甲「零新焦点位」= 仅置信度,不含折边(erratum)。
  - 连带注:lead-programmer BLOCKING-2(NavigationDirection 闭集)经 TD 复核**降级为误判**
    —— `DeliveredIntentTypes` 是构建期清单,非 ADR-025 装配边界。
- **下一步**:S1 病名栏 + 病名册 UI 整面(DTO 显式携带 `lexicon_id` —— TD 第五风险钉死)。

## 2026-10-12 — sprint-08 S1.0 收口(病名册词表烘焙管线 ✅)

> **性质**:上个会话遗留的**未提交、未记录**产物(工作树断点)。用户裁定「先收口 S1.0」——
> 承「禁借绿」铁律,未验证产物不往上盖。

- **兑现**:story-005 登记的 NOT-RUN 缺口「**AC-37-35 的构建期双射校验半边:ADR-014
  阶段 2 校验器未建**」。词表源 `assets/data/case_judgment_lexicon.json`(3 条:伤寒→病种1,
  湿温/温毒孤儿)→ 阶段2 绑定 + 校验器(双射拒 / ordinal append-only / 非空 / u8 上界)→
  cooked 产物入库(`case_judgment_lexicon.cooked.bytes`,73 B)+ 运行期读方(E-13 硬失败)。
- **修的真 bug ×3**(验证途中发现,均本库点名过的失败模式):
  ① `ComputeRepoRoot` 少退一层(4→5,落 `unity/` 而非仓库根)⇒ 3 条 `BakeFromRepo` 测红;
  ② 绑定器 `ReadString` 空串检查**抢先**于校验器 ⇒ `lexicon-text` 判据成**不可达死代码**
     (绑定层只留 JSON 结构检查,域语义归校验器 —— 同 story-006 F-1「消解死代码」);
  ③ `JudgmentLexiconCookedWriter` 的 `(byte)disease_count` **静默截断**(256→0)+ 假注释
     「校验器保证 ≤255」(实际无检查 —— 同 story-003 M-2「判据-文本背离」)⇒ 校验器补
     `MaxDiseaseIdsPerRow=255` 上界 + 256/255 边界测。
- **验证**:定向测 **11/11 全绿**;全量 EditMode **3268 = 3221/0/1inc/46skip**
  (基线 3257 ⇒ **+11 零回归**,恰为 S1.0 新增)。证据:`unity/UnityLogsTmp/s8_s10_lexicon_filter4.xml` ·
  `s8_s10_editmode_full.xml`。
- **同批清账(用户裁定)**:
  - **垃圾 blob 停跟踪** —— `git rm --cached mono_crash.mem.{3340453,3344351}.1.blob`
    (各 10 MB,`f672607` 误入),本地文件保留;仓 HEAD 起不再跟踪(历史仍存,真删须重写历史)。
  - **`.gitignore` 条目重新入库**(**推翻 F-6 旧裁定**)—— 三组条目(UnityLogsTmp / mono_crash
    blob ×2)正式入库:`.gitignore` 是仓级共享约定,非「本地忽略项」。用户 2026-10-12 明示翻转。
- **账目回填**:`sprint-08.md`(§2 S1.0 行 + §3 验收)· `story-005`(AC-37-35 ①② NOT-RUN → 已闭
  + Completion Notes 订正注 + S1.0 追补段)· 本段。
- **仍未闭(NOT-RUN,非本 story 故障)**:词表 ↔ 真实病种注册表全量交叉检查 —— 注册表现为
  代码内联 fallback(`RegistrySchema`),无交叉检查输入,归 `disease_registry.json` 烘焙落地轮。
- **下一步**:S1.1 病名册 UI 整面(42 侧 `CasebookScreen` 病名栏 Submit → 翻册 → 指认 → 落笔;
  DTO 显式携带 `lexicon_id` —— TD 第五风险钉死)。

## 2026-10-12 — sprint-08 S1.1 病名册 UI 整面 ✅(件 A–H 全落 · 双变异验证)

> **性质**:本会话从「A–E 已落、F/G/H 待做」的工作树断点续起。用户整体批准 A–H 后落盘;
> 途中**发现三处结构缺口**,经 AskUserQuestion 三项裁定后一并解决(见下)。

- **兑现**:`sprint-08.md` §2 的 S1.1(病名栏 Submit → 翻册 → 指认 → 落笔)+ S1.2/S1.3 判据
  (指认落 `JudgmentRecorded` / 三态循环)。**玩家路径从「无入口」变为「可达」**:
  病名栏落焦 + Submit ⇒ 病名册摊开(右页内嵌,**非新 ModalId**)⇒ 条目落焦 + Submit ⇒ 指认
  ⇒ 病例流新增判断事件 + 墨龄起算。
- **落点(件 A–H)**:
  - **A** `NoveltyTracker.EnumerateSeenObjects`(Sim.Contracts)· **B** 新 `SeenDiseaseProjector`
    (查体 ⇒ `SkillGrown` 落病史流,诊断技能/objectId = 病种)· **C** 新 `LexiconBookSource`
    (已见集 → 词表反查 → 只出 `lexicon_id`)· **D** 词表 cooked 接入装载 ·
    **E** `ICasebookDtoSource` 加 `SeenLexiconIds` + `PendingConfidence` + `TextOfLexicon`
  - **F** `CasebookScreen` 病名册交互面 · **G** 三态循环(空→疑似→确定→擦除)·
    **H** UXML 零改(容器**运行时建且匿名** ⇒ spec 面 name 序一字未动,交叉断言零回归)
- **途中发现并解决的三处结构缺口(用户三项裁定)**:
  1. **F/G 无写路径** —— ADR-013 §四 的接口**全为入向**,42 拿不到装配层写者。
     裁定 = **甲:Gameplay.UI 新出向契约 `ICasebookCommandSink`**(实现方住装配层,
     42 经 DTO 回读档位)。**已就地登记 ADR-013 §四 + Status 头补注**(非裁决,是补结构性缺项)。
  2. **`JudgmentRevised` 无编码通道** —— ADR-029 §① 修正④ 只给 `JudgmentRecorded` 开了
     freehandText 具名口,改判 / 擦除两步**写不出去**。裁定 = **甲:补对称具名重载**
     (`IPayloadEncoder.EncodeJudgmentRevised` + `PayloadEncoder` 实装)。**连带**:b7 门
     (`WriterExistenceGate`)的 `JudgmentRevised` 豁免**须撤**(判据①「已有写者仍挂豁免 = 红」强制)。
  3. **三态档位归属** —— 裁定 = **甲:住装配层(`CasebookCommandRouter`),42 经 DTO 回读**
     (与 `InkAgeTicks` / `SeenLexiconIds` 同构 ⇒ 42 零游戏状态字段,AC-42-D3 不破)。
- **真 bug ×1(验证途中抓出)**:`texture_binding_gate_test` 以 `private void AddChannel(...)`
  为**结构锚点**;我为取回病名行改签名为 `private VisualElement` ⇒ 门红。**修 = 回退签名**
  (改用 `rightColumn.Q<...>` 取回行)—— 承「**不得削弱既有门以迁就重构**」。
- **本库第一处「真派发」接线判据**:2026-10-12 实测确立 —— `NavigationSubmitEvent` 的目标
  由 `FocusController.focusedElement` 决定,**无焦点时事件被引擎丢弃**(裸 `SendEvent` 探针命中 0;
  先 `Focus()` 再发 ⇒ 命中 1)。这不是夹具怪癖而是引擎语义(导航提交永远落在当前焦点元素),
  故「先落焦、再提交」= 玩家真实动作序。**据此把 wiring 测从「断言 handler 已注册」的弱形态
  升级为「真 panel + 真派发」的强形态**,并加反向守卫测(无焦点被丢弃)。
- **验证(全绿 · 证据 `unity/UnityLogsTmp/`)**:
  - EditMode 全量 **3297 = 3250 / 0 / 1inc / 46skip**(与 S1.0 基线同 —— 零回归;
    46 skip 全为既有的 NOT-RUN/ADVISORY `[Ignore]` 登记件)。证据 `s8_s11_em_full.xml`。
  - PlayMode 全量 **148/148**(基线 142 ⇒ **+6**:3 wiring + 3 全链)。证据 `s8_s11_pm_final.xml`。
  - 新增测:EditMode `casebook_command_router_test`(9)· PlayMode `casebook_lexicon_book_test`(9)·
    PlayMode `casebook_point_identify_chain_test`(3,跨层全链)。
- **双变异验证(承「禁借绿」)**:
  - **变异 1 删病名栏接线** ⇒ 恰 **4 红**(2 wiring + 2 全链),语义判据 ①–⑥ **仍绿** ——
    证「语义对 / 接线缺」两态可分辨。
  - **变异 2 删条目行接线** ⇒ 恰 **3 红**(1 wiring + 2 全链),病名栏 wiring 测仍绿 ——
    两处接线**各自可证伪**,无共模遮蔽。
- **改判一处旧判据**:`interaction_input_chain_test.test_virtualE_readsAnd_triggersExamine` 原断言
  「查体 = 派生态,病史流零新事件」**已作废** —— 件 B 让查体写**一条** `SkillGrown`
  (病名册已见集真源,ADR-026 ⑥)。新判据 = 恰 +1 条且类型 = `SkillGrown`。
- **登记(非阻塞 · 三处)**:
  1. **`Boot.unity` 无 `EventSystem` + `InputSystemUIInputModule`**(仅 `SpikeAssump6.unity` 有)
     ⇒ **运行期真实焦点导航不可达**;S1 判据因此走 E2E-3 的解耦形态(语义层 + 真派发接线测)。
     接线落点归**焦点接线轮**(今日零焦点施加者 ⇒ 冲突不可观测 = 非缺陷,承 `SkeuoRuntimeDriver` 头注)。
  2. **`NoveltyTracker` 的流重建器不存在** —— S1 计划 §1 称「已见集可从病史流重建」,
     但**无重建实现**(现仅运行期增量)。归 **7a 读档轮**(ADR-010 / ADR-026 ⑥)。
  3. ADR-013 §四 写面补注 —— **已就地落盘**(见上)。
- **下一步**:S2 方笺页渲染 + 翻页 + 戥子选档开方(依赖 S1)。

---

## 2026-10-12 — sprint-08 S2 方笺页 + 翻页 + 戥子选档开方 ✅(开方环节玩家可达)

**用户裁定(四项,全部照准)**:
1. 翻页范围 = **甲:仅册内翻页**(脉案页 ⇄ 方笺续页;不发相机意图、不成新 `ModalId`)。
2. 戥子档位读写形态 = **甲:42 传显式 ordinal + DTO 回读档数**(42 零游戏状态,ADR-013 §9 C3)。
3. `PrescriptionPageHost` 的「期限 1 防重入」= **甲:删期限 1,改真实规则**(拒绝权归**库存**)。
4. 页缘入 K 焦点计数 = **甲:页缘入 K ⇒ K = 10**(五通道 5 + 问诊栏 1 + 病名栏 1 + 折边 1 + 页缘 ×2)。

**落盘(契约 → 装配 → 屏 → 测试)**:
- **契约**:`ICasebookDtoSource` + `int DoseDetentCount`;`ICasebookCommandSink` +
  `SubmitDose(int ordinal)` / `SubmitPageTurn()` / `SubmitFold()`(后者两条 = 纯呈现,零写入)。
- **装配**:`CasebookCommandRouter` 接 `PrescriptionPageHost`(第三构造参)—— `SubmitDose`
  ⇒ `Prescribe` ⇒ `PrescribeRequestDriver` ⇒ 11 五步 ⇒ `DrugTreatmentApplied` 入病史流;
  `SubmitPageTurn` / `SubmitFold` = **no-op**(P0 无分册态段)。`PrescribeRequestDriver.DoseDetentCount`
  = 档数真源(其戥子选择器 = 规则十三唯一量化点);`CasebookVitalsSource.BindPrescribeDriver` 后置接回。
- **屏**:`CasebookScreen` 增**折边首次实现**(S0.2 已计进 K 但代码零实现)+ **页缘 ×2(前案/后案)**
  + 方笺续页(书写面 + 戥子档位行,每行 `userData` = 显式 ordinal)+ `BrassScaleElement.TickCount` = 档数。
- **`PrescriptionPageHost`**:删 `_prescribed` 单次闸门(用户裁定 3)。

**验证(全绿)**:
- EditMode 全量 **3302 = 3255 / 0 / 46skip / 1inc**(基线 3297 ⇒ **+5**;46 skip 全为既有登记件)。
- PlayMode 全量 **160/160**(基线 148 ⇒ **+12**:8 语义 + 4 真派发 wiring)。
- 新增测:EditMode `casebook_prescribe_chain_test`(5)· PlayMode `casebook_page_turn_test`(12)。
- 证据:`unity/UnityLogsTmp/s8_s2_em_final.xml` · `s8_s2_pm_final.xml`。
- **三变异验证(承「禁借绿」)**:
  ① 删**页缘后案接线** ⇒ PlayMode 恰 **1 红**(`test_wiring_pageEdgeNextEvent_…`),语义判据 ①–⑦ 仍绿;
  ② 删**档位行接线** ⇒ 恰 **1 红**(`test_wiring_dentchRowEvent_…`),页缘 wiring 仍绿(两处接线各自可证伪);
  ③ 删**开方接线**(`CasebookCommandRouter` 内 `Prescribe` 调用)⇒ EditMode 恰 **4 红**
     (全链 3 + 档序数 1),`test_routerWithoutHost_submitDose_failLoud` 仍绿(该测断的是**未接宿主**)。
- **改判两处旧判据(均为「脚手架 → 真规则」的转正,非削弱)**:
  ① `prescription_page_host_test.test_s24_prescribe_onceOnly_notAutoRepeat`(断「第二次恒 false」)
     ⇒ 拆成 `test_s24_prescribe_withStock_repeatable_noWiringGate`(库存足 ⇒ 可重复)+
     `test_s24_prescribe_outOfStock_rejects`(库存尽 ⇒ 拒,零新事件)。
  ② `casebook_render_test` 右栏文本白名单 `{病名, ?}` ⇒ `{病名, ?, 折}`(折边是 §5.3 ASCII
     已画的右页元素;**禁项一条不减**,只是把新增合法词补进白名单 —— 白名单缺合法项 = 假红)。
- **结构断言同步**:`casebook_lexicon_test` UXML name 序(13 → 19)+ focusable 7 → **10**;
  `casebook_render_test.ExpectedFocusOrder`(7 → 10)。**K 演进链 6 → 7 → 8 → 10**(置信度恒不在内)。

**测试过程中的一个真发现**:PlayMode `Sim.Contracts.Tests`(EditMode asmdef)**不引用 `Gameplay.UI`**
⇒ 42 屏相关判据一律住 PlayMode `Gameplay.Tests`。故 S2.1 的 EditMode 版测试删除,
**语义 + 真派发接线合并进 PlayMode `casebook_page_turn_test`**(照 S1.1 `casebook_lexicon_book_test` 先例)。

**数值发现(非缺陷,登记)**:`PrescribeFlow` ①b 的份数 = **档值 × `portions_per_dose`**
(乘法住 11)—— 最小表 `portions_per_dose = 1` ⇒ **档 3 需 3 份**。测试种子须按档位给足。

**登记(非阻塞)**:
- `Boot.unity` 无 `EventSystem` + `InputSystemUIInputModule` ⇒ 运行期真实焦点导航不可达(承 S1.1 登记 1,归焦点接线轮)。
- `SubmitPageTurn` / `SubmitFold` 现为 **no-op** —— 页码 / 折叠态持久化归**分册态段**(7a),P0 未实现。
- **下一步**:S3 词条可读性 polish + E2E-3 七环节全链门。

**两处既有门抓出的真问题(验证途中 —— 均为**我的新代码**踩门,已修)**:
- `test_ac42f3_componentLibrary_noForbiddenItems`(AC-42-F3 词面门)**二次触发** ——
  `CasebookScreen.cs` 内注释出现禁用词「计数」(我写「K 计数的来源」)⇒ 改「K 的增量来源」。
  ⚠️ 该门**逐字符扫原文,注释也扫**(承 `DentchDoseSelector` 头注)⇒ 本目录内**文档措辞亦受限**。
- `test_markRegistry_noPaperTear_noTearFields`(零破纸字段门)触发 —— 我的新字段
  `_prescriptionPage` 被裸子串匹配判为「破纸字段」:「p**rescrip**tion」**恰含** `rip`。
  ⇒ 改字段名为 `_rxPage`(方笺通用缩写),`name` 属性仍为 `prescription-page`(spec 面不变)。
  ⚠️ **这是门的已知假阳面**(它禁的是「破纸」机制字段),不是我的设计问题;改名 + 就地注记,
  **未改门**(不削弱既有门 —— 承 S1.1「不得削弱既有门以迁就重构」)。

---

## Sprint 08 S3 词条可读性 polish ✅ 完成(2026-10-13)

**用户裁定两问**(先于动工):① §11「≥24px」承诺 = **按承诺兑现**(新增 ≥24px 专用档挂 42 脉案);
② 「词条格」覆盖 = **题签 + 落字都升**。

**三项结构事实(侦察实测,均非 S3 引入)**:
- **A. 题签零 class**:`CasebookScreen.AddChannel` 的行头 Label 是裸 `new Label(text)` ⇒ 落
  **Unity 默认主题 14.0px**(S3 探针实测),既非本屏任何档、也不受主题变量管辖 ⇒
  `casebook-39.md:308`「通道题签 ≥24px」这半**结构上无法兑现**。落字 `.channel-reading` 实测 15.0px(md)。
- **B. 阴性态对比违约**:`--skeuo-ink-faded`(rgba 0.55)在纸面合成后 **3.87:1 < 7:1**
  (`:87` / `casebook-39.md:309` 承诺),且 §11 明文「阴性态用**笔形**不用低对比」—— S3 撞见的既有违约。
- **C. `.ink` 幽灵类**:`.ink` 在**全 7 个 UXML spec 文件**被引 36 次(脉案 21 次),但**全仓运行期
  C# 零挂载**(`SkeuoElement.Ink` 仅注册表登记)⇒ spec 面描述的是运行期从不发生的呈现。**独立 story 登记,不在 S3 修**。

**落件(六件全落)**:
1. `SkeuoThemeVariables.uss` +3 token:`--skeuo-font-size-lexeme: 24px` / `--skeuo-reading-backing: #fbf9f3` /
   `--skeuo-ink-negative: #5a5044`(实测底衬上 **7.48:1** ✓)。
2. `SkeuoPaper.uss`:`.channel-reading` / `-negative` 换 lexeme 档 + 加底衬 + 阴性换墨;新增 `.channel-label`。
3. `CasebookScreen.AddChannel`:行头 Label 挂 `.channel-label`(**签名未改** —— `texture_binding_gate_test` 结构锚)。
4. 新 `skeuo_readability_test`(EditMode · **6 测**):对比 ≥7:1 ×2 态 / 公式锚定(白黑 128灰)/
   字号 ≥24 / 三类绑档 / 底衬走 var / 明度轴 Δ。**四变异各恰红 1**(阴性墨未达标 / 字号<24 / 落字解绑 / 删底衬)。
5. 回写:`casebook-39.md` §11 两行 + §13 新 `[V]` 行 · `accessibility-requirements.md` :85/:87 状态
   `Not Started → 🟡 部分落地` · sprint-08 S3.1 行 + §3 · 本文件。
6. `sign_channel_binder_test` 阴性墨断言**升格**(ink-faded → ink-negative;收紧非放宽)。

**门数字**:EditMode **3308 = 3261/0/46/1**(基线 3302 ⇒ +6,零回归)· PlayMode **160/160**。
(exit 码非 0 = 既有 PlayerController/InputSystem fail-loud 测试刻意打 LogError,与本批无关。)

**诚实边界(禁借绿)**:
- **截图腿 + 几何腿 = 人工走查**(`casebook-39.md` §13 新 `[V]` 行)。batch 出不了图 ——
  2026-10-13 实测 `ScreenCapture` 在无渲染宿主抛异常、PNG 零字节(panel 虽建,`Screen.width=640×480` 无有效帧)。
- **几何腿预期有问题**:`NotoSansCJKsc` lineHeight = **144.8% em** ⇒ 24px 文字盒 ≈34.8px + 上下 padding 32
  = **≈66.8px > 44px 行下限** ⇒ 行**预期会长高**,§5.2「恒定行高 = 空行与已查行视觉等重」可能受扰,
  须人工目视裁决(不擅自压值 —— 承「数值用户自己调」)。
- **「字重微调」无操作空间**:UI Toolkit 只有 `-unity-font-style: bold|italic`、不吃数字权重,
  字体已全切 Bold(`2c9aae9`)⇒ 此项**不假装做**。
- **`--skeuo-ink-faded` 全局仍 3.87:1** —— 其他屏淡墨文字仍低于承诺,全局修正归独立 story。

**下一步**:E2E-3 七环节全链门(收口 BLOCKING)+ 第六轮人工 playtest(含 S3 截图腿)+ C1/C3。

## Sprint 08 E2E-3 七环节全链门 ✅ 完成(2026-10-13 · 收口 BLOCKING)

**用户裁定两问**(先于动工):① 链结构 = **一测到底 + 每环保留断言**;② 施治环输入 = **注入虚拟 F**(同 `interaction_input_chain_test` 三件套)。

**新测 `e2e_full_loop_test.test_e2e_fullLoop_sevenStages`(PlayMode · 单测 484 行)**:

| 环 | 判据(可证伪) |
|---|---|
| ① 走近 | `OnPositionSample(2,1,0)` 跨格 + `OnTickEdge` ⇒ `ActorCellEntered` 入世界流(ADR-020 §四 唯一 sim 投影) |
| ② 查体 | `ExamineDriver.OnExamine`(ledger 快照)+ `SeenDisease.OnExamined`(SkillGrown 入病史流,已见集真源) |
| ③ 对照 | `RevealedRows` = **19 条**(探针实测)+ `.channel-reading` 值区格渲染词条文字(内嵌 Label) |
| ④ 落笔 | 病名栏 Submit → 摊开 → 条目 Submit ⇒ `JudgmentRecorded`(lexiconId = 词表反查显式 id + 疑似) |
| ⑤ 开方 | 后案 Submit → 方笺页 → 档位行 Submit ⇒ `DrugTreatmentApplied`(恰一条) |
| ⑥ 施治 | 虚拟 F 注入(探针读回 true 区分键位怪癖/链断)⇒ `EmergencyAttempt` + `EmergencyTreatmentApplied` 双事件 |
| ⑦ 纸面反馈 | `InkAgeTicks > 0` + 翻回脉案值区词条仍在 |
| 末 | 三流事件 tick 单调不减(全序;乱序入流 = 重放不可复现) |

**门数字**:PlayMode **161/161**(基线 160 ⇒ +1,零回归)· EditMode **3308 = 3261/0/46/1**(基线 3308,零回归)。
**变异**:删 `BindCommandSink` ⇒ ④环恰红(0 判断事件);还原后复跑绿(diff 校验 IDENTICAL)。

**探针实证的两条实现期事实**(临时探针 `s3_probe_e2e_reveal_test` 用毕已删):
1. **病种 1 `Incubation` ≈ 2000 tick**(@20 Hz ≈ 100 s)—— `tau < incubation ⇒ Progress ≡ 0`(AC-26)⇒
   position = 0 ⇒ 双条件门 `aboveFloor` 恒 false ⇒ 查体收 0 词。测试须 `AdvancePastIncubation()`(2500 tick,
   batch 纯 Step 瞬时)越过。**既有行为非缺陷**,但 E2E 链不推进就断在③环。
2. **`Teleport` 不打 sample** —— 玩家 `OnTickEdge` 的跨格检测吃 `pending_cell`,须显式 `OnPositionSample`。
3. **F 急救定位源 = 视觉实体**(`NearestPresentPatient` 读 `PatientVisualSpawner` 位置)⇒ 装配期须起 spawner。

**M2 Exit #8 复评**:② 墨龄 + ④ RevealedRows/条目集 **可勾**(链内真组合根判定,非合成 fixture);③ 仍不勾(候选列表非本链判据);AC-39-10 翻页零事件判据在 `casebook_page_turn_test` ④(S2.1 已绿),链内形式复核 = 翻页在⑤⑦之间事件增量恰 2(开方+施治),正式复评归 qa-lead 轮。

**C3 双代理评审(2026-10-13 · 原件落 `production/qa/evidence/`)**:
- **qa-lead 半边**:判决 **APPROVED**(零 BLOCKING)—— `review-sprint08-e2e3-qa-2026-10-13.md`。
  动作清单 5 项已全部清零:A-1 ⑦环头注过claim 订正(病名栏回显 = 未实现,归 42 后续轮;
  测试零断言,非假绿)✅ · A-2 补两发变异实测(变⑤ 删 Grant ⇒ ⑤环红;变⑦ 删 tick 推进
  ⇒ ⑦环红;连变④ 共**三发**,各恰红 1 后还原复跑绿)✅ · A-3 M2 Exit #8 勾 ③(EmergencyCandidateRuntime
  已生产实例化 = 组合根 246 + BootRoot 229)与 AC-39-10(生产 no-op 空体,结构性不可能写流)✅ ·
  A-4 三处分工登记(链内首落 / S1.1 三态循环 / EditMode 路由+末值 AC-37-05)✅ ·
  A-5 readability 源留后续 qa 轮(禁借绿登记已合规)。
- **lead-programmer 半边** = **CHANGES REQUIRED(3 条 P2/P3 · 零 BLOCKING · 自陈不阻断结案)**
  —— `review-sprint08-e2e3-code-2026-10-13.md`:ADR 符合性 8/8、测试零泄漏(带卫兵设备还原
  + 反射 fail-loud)、2500 tick 零副作用有结构依据、S3 var 纪律三处 + readability 测试守卫。
  **A2 已做**(收集方 anchor 亲属限定,生产侧加 name 会破 `test_bookContainer_isAnonymous`
  守护 ⇒ 评估后不改生产,假阳风险由两测试收集方收窄,全量复绿);**A1(三处 helper 重复抽取)
  + A3(void TearDown 对齐参考测)登记 tech-debt/polish 轮**(判据写死,不阻断收口)。
- **C3 合判 = 通过(零 BLOCKING,一 APPROVED + 一 CHANGES-不阻断 + 3 动作处置完毕或登记)**。

**M2 Exit #8 复评清单现状**:②③④ + AC-39-10 **四条全勾**(均为链内真组合根判定或生产码核验,非合成 fixture)。

**第六轮人工 playtest = CHANGES REQUIRED(2026-10-13 · 用户口述判读)** —— 原件 `production/playtests/playtest-2026-10-13-m2-round6.md`:
- **C2/C3/C4 · 交互面输入路径全断**:病名栏 Submit / 三态 / 翻页选档,**鼠标点击与键盘 Space 双无响应**(脉案面板可见,排除「面板未开」);**焦点看不到在哪**。
- **C1/C5 · 可读性**:24px 落字「看不清字体发虚」+ 值区「对比不足发灰」(疑犯:底衬 `#fbf9f3` 与纸纹中心 ≈#fefdfe 明度差 Δ≈0.003 ⇒ 底衬形同虚设直接压纹;阴性墨 24px 下更淡;SDF atlas 精度)。
- **✅ Q6 几何腿**:升档后行高变长(≈66.8px 预期坐实)但**空行/已查行等重仍成立** ⇒ §5.2 铁律未破。
- **分层判读(关键)**:E2E-3 语义链 7/7 自动化成立,人工 2.5/7 —— 缺口 = **输入 wiring 半面**(sprint-08 §4 预裁「判据与输入路径解耦」⇒ 输入路径从未被自动化覆盖,qa-lead C3 已如实登记)+ S3 可读性人工腿首跑判红。**不是语义层断链**。
- **✍ B-6-1(收口判据直判)**:输入 wiring story(UI map enable / EventSystem 注册 / 真实设备→NavigationSubmitEvent)+ 根因修 + wiring 自动化测;B-6-2 焦点可见性;B-6-3 可读性(数值归用户);B-6-4 字体渲染。

**下一步**:立在 sprint-09(或 hotfix):B-6-1 输入路径(修复前 sprint-08 不得 Complete —— 收口判据 = 自动化绿 + 人工绿双半,承 sprint-07 G5.2 口径)+ B-6-2/3/4。

## B-6-4 字体糊根因结案 ✅(2026-10-13 · SDF 渲染路径)

**第六轮 playtest C1「24px 落字看不清字体发虚」的根因 = 五层叠因,逐层实证(探针 `sdf_font_render_path_probe_test`)**:

| 层 | 根因 | 修正 |
|---|---|---|
| ① | 生成器产 **TMP_FontAsset** | 改产 `UnityEngine.TextCore.Text.FontAsset`(UI Toolkit `FromSDFFont` 只认 TextCore 系;TMP 不继承它 ⇒ 被拒 ⇒ `MissingAssetReference`) |
| ② | 资产空壳(零字形) | `BakeCharacters`(`TryAddCharacters`)预烤 1717 字 |
| ③ | Dynamic 字形 ImportAsset 时被 `TMP_PreBuildProcessor` 清空 | 预烤后切 **Static**(`SetStaticAfterBake`) |
| ④ | 子资产(material/atlas)未持久化 | `AddObjectToAsset` 挂子资产 |
| ⑤ **决定性** | **USS `-unity-font-definition: url("guid:…")` 对字体恒不解析**(`url(guid:)` 仅对 `background-image` 有效) | 全库 **10 处**改 `resource("Assets/Gameplay.UI/Skeuomorphic/Fonts/NotoSansCJKsc-Bold SDF.asset")` |

**探针三段实证**(`UnityLogsTmp/sdfprobe8-165222.xml` PASS):
- ① 资产以 TextCore.Text.FontAsset 加载 = **成功**
- ② C# `FromSDFFont` ⇒ **NotoSansCJKsc-Bold SDF**(排除「资产被拒」)
- ③ 真实 USS `.channel-label`(resource)⇒ **NotoSansCJKsc-Bold SDF**(坐实 ⑤;`<default>` 消失)

**守护测同步**:`sign_channel_binder_test:261` 剥字体行正则 `url\([^)]*\)` → `(?:url|resource)\([^)]*\)`(防资产名带数字假红)· `emergency_skip_entry_test` / `SkeuoThemeVariables.uss` 过时注释订正(语法 + 类型 + population mode 三处)。
**EditMode 全绿**:`DaYiJingCheng.Tests.Unit.SkeuomorphicUI` = **241 passed / 0 failed / 13 skipped**(含 `shell_a` / `sign_channel_binder` 全测 / `emergency_skip_entry` 全测)。

**残留(归用户/后续)**:B-6-4 真机**目视**像素验收(批次探针只证「接线解析到 SDF 资产」,不判「真机画出什么」—— 诚实边界)· B-6-3 可读性对比度(数值归用户)· B-6-1 输入路径(独立 story,与字体正交)。

**未提交**:本轮改动(6 USS + SdfFontAssetTool.cs + 3 测试 + 重生成 SDF 资产 + 2 新探针)待用户裁定后 commit。

## B-6-4 字体糊 —— 第二层(覆盖不全)根治 ✅(2026-10-13)

**用户实测反馈**「脉案 6 行很清晰,但其他的还是很糊」—— 第一轮修复只解决了「字体解析」(USS 语法 + 资产类型),**未解决「哪些文本挂了字体」**。

**根因(探针实测,`sdf_font_coverage_probe_test` 枚举真实脉案屏全部 Label)**:
运行期 C# 建的文本元素**裸 `new Label(text)` 零类** ⇒ 落 Unity 默认主题字体(中文 fallback = 糊)。实测 13 个 Label:
- **清晰 7** = 6 通道题签 + 病名(挂 `.channel-label`,有字体定义)
- **糊 6** = `casebook-title` / `confidence-question`(?) / 折 / 前案 / 后案 / 方笺(**裸 Label**)
用户说的「6 行」= 6 个通道题签。**设计意图(UXML spec 面每个 label 都写 `class="ink"`)在运行期代码从未落实** —— 正是 `SkeuoPaper.uss:163` 早已登记的系统性缺口(`.ink` 在 7 UXML 被引 36 次,运行期 C# 零挂载)。

**决定性实测**:`[COV-INHERIT] root 内联字体后,清晰 Label 数 = 13 / 13` —— UI Toolkit 的 `-unity-font-definition` **可继承**,祖先设一次覆盖全体。

**修法(用户裁定:根级默认字体)**:
- `SkeuoRuntimeDriver.EnsureDocument` 给 panel root 挂 `SkeuoRootClass`(="skeuo-root")
- `SkeuoThemeVariables.uss` 追加 `.skeuo-root { -unity-font-definition: resource("Assets/…SDF.asset"); }`
- 一处生效,全体后代继承,未来新文本自动覆盖,不可能再漏挂
- **不动** `.channel-reading*` / `.skip-entry` 的逐元素字体定义(有守护测 `sign_channel_binder_test` / `emergency_skip_entry_test` 锁住;同值冗余,零冲突)

**验证**:
- 新回归门 `sdf_font_coverage_probe_test`(PlayMode)= **13/13 全解析到 SDF** ✓(用**生产 PanelSettings** 含 UnityDefaultRuntimeTheme,排除主题字体规则压过 `.skeuo-root` 的风险)
- EditMode `DaYiJingCheng.Tests.Unit.SkeuomorphicUI` = **241 passed / 0 failed / 13 skipped**(零回归)

**待办**:用户真机目视复验(脉案标题 / ? / 折 / 前案 / 后案 / 方笺 是否已清晰)。

## B-6-4 字体 —— 第三轮排查(2026-10-13 · 真机时序实证)

**用户反馈**「还是其他地方糊」⇒ 系统排查全部文本源:
- 全项目**仅 2 文件**建 UITK 文本:`CasebookScreen`(11 处)+ `EmergencyCandidateList`(2 处),共 14 处,全在 `.skeuo-root` 继承链下
- 其他 6 屏(Screens/*.cs)**零文本**(只建空 Paper 容器)
- World.unity 无 UI;无 UGUI / TMP;唯一非 UITK 文本 = `InputDebugView`(IMGUI 开发者调试视图,非玩家可见)

**决定性实证(两个对照探针)**:
| 探针 | 时序 | 样式表挂载 | 清晰/糊 |
|---|---|---|---|
| v1 | 字段后注入 / 手动调 Awake | **0** | 0/13 |
| **v2** | **真机时序**(inactive GO + 预置字段 + 自然生命周期) | **9** | **13/13 清晰** |

⇒ **生产路径下 13/13 全清晰** —— 第二层修复(根级默认字体)**正确生效**。
v1 的「全糊 / 0 挂载」是**探针假象**(手动调 Awake 早于 UIDocument 的 panel attach;
`AttachStyleSheets` 第 190 行 `_panel.panel == null` 时静默 return,探针触发该分支,真机不触发)。

**遗留观察(未修,登记)**:`AttachStyleSheets` 的 `_panel.panel == null` 守卫**依赖时序** ——
若未来真机某路径下 Awake 早于 panel attach,会**静默跳过挂载且不重试**(无 OnEnable/Start 补挂)。
当前真机时序 OK(v2 实证),但这是脆弱点,登记为 tech-debt(修法 = 移除该守卫,
`VisualElement.styleSheets` 在未 attach 时也能添加,attach 后自动生效;或加 OnEnable 补挂)。

**结论**:代码层全对,真机应全清晰。用户「还是糊」的最可能解释 = **编辑器未重编译/重导入**(当前编辑器未运行)。
**下一步**:用户重开编辑器复验;若仍糊,须提供**截图**以定位「其他地方」具体元素。

## B-6-4 字体 —— 真正根因(第四轮 · 用户真机实证 · 2026-10-13)

**用户真机证据(决定性)**:
- **清晰** = 脉案 6 行题签(`.channel-label` —— **直接挂在 Label 上**的类)
- **糊** = `?`/折/前案/后案/方笺(**裸 Label**,零类)+ 脉案 6 行**下面的值区** + **止血等急救候选**(`.skip-entry` **容器**内的**子** Label)

**真正根因(推翻第三轮的「继承」假设)**:
CSS 级联铁律 —— **元素上的直接声明恒压继承值**(与特异性无关)。Unity 内置默认主题对文本元素有**直接**字体规则 ⇒ 压过从 `.skeuo-root` **继承**下来的 SDF 字体。
- `.channel-label` 清晰 = 它**直接匹配** Label ⇒ 与主题直接规则**同级竞争**且后加载 ⇒ 胜
- 其余糊 = 全部**靠继承**(裸 Label / 容器类下的子 Label)⇒ 被主题直接规则压过

**为何 batch 探针假绿**:nographics 环境不加载 `unity-theme://default` ⇒ 继承侥幸生效 ⇒ 探针 v1/v2 全绿,**与真机不符**。⇒ **教训:继承类判据 batch 不可信,须真机目视**。

**修法(已落)**:
- `SkeuoThemeVariables.uss`:`:root` 外新增 **`.unity-text-element`** 规则(UI Toolkit 给**每个** TextElement/Label/Button/TextField 自动挂的类)**直接匹配**全部文本 ⇒ 不赌继承,直接竞争
- 该文件是九份 USS 清单**第一位** ⇒ 压过主题默认字体;`.ink`/`.channel-label` 等逐元素类**在其后**加载 ⇒ 同特异性后者胜 ⇒ 专属色/字号**保留**,零冲突
- **移除** `.skeuo-root` 机制(C# `EnsureDocument` 挂类 + `SkeuoRootClass` 常量)—— 已证伪
- 保留 `AttachStyleSheets` 的守卫移除 + `OnEnable` 幂等补挂(独立脆弱点修复,仍有效)

**验证**:覆盖门(生产 PanelSettings)= 13/13 全解析到 SDF ✓(但 batch 绿≠真机绿,须目视)。
**下一步**:用户重开编辑器目视复验脉案值区 + 止血候选是否清晰。

## B-6-4 字体 —— 第二根因 = 字号(第五轮 · 用户实证定稿 · 2026-10-13)

**用户诊断实验(决定性)**:把 `.unity-text-element` 字号临时抬到 **40px** ⇒ 用户实测
「**字体变大了,不糊**」。⇒ 根因判定 = **字号过小**(SDF 缩采样不足),**非渲染问题**。

**机理**:SDF 资产采样点 = **32px**(`m_PointSize`)。清晰档 `.channel-label` = 28px(近采样点);
糊的裸 Label 无字号 ⇒ 落 Unity 主题默认 ≈14px(**远小于采样点**)⇒ SDF 缩采样不足 = 糊。
⇒ **两层根因**:① 字体引用(第四轮:`.unity-text-element` 直接匹配);② **字号**(本轮)。

**定稿(已落)**:
- `SkeuoThemeVariables.uss`:`:root` 新增 `--skeuo-font-size-default: 28px`(与 `--skeuo-font-size-lexeme`
  同档 = 已被验证清晰 + 近采样点);删临时诊断变量 `--skeuo-font-size-debug: 40px`
- `.unity-text-element` 加 `font-size: var(--skeuo-font-size-default)`
- **覆盖面核对**:运行期裸 Label(`?`/折/前案/后案/方笺/值区子 Label/急救候选子 Label)全落
  `.unity-text-element` = 28px ✓;`.skip-entry`/`.brass` 挂在**容器**(非 Label)上 ⇒ 子 Label
  仍落直接规则 28px ✓(同 CSS 铁律反过来帮我们);其余字号类(`.ink`/`.seal-small`/`.implement`/
  `.mark-*`)**运行期零挂载**(0 个 .cs 引用)⇒ 不影响本屏

**回归门(已加强)**:
- `sdf_font_coverage_probe_test.cs`:双判据 —— ① 字体解析到 SDF;② **字号 ≥24px**(无障碍下限
  `SkeuoThemeVariables.uss:165`)。实测 **`fontSize 范围 [28,28]`** —— 脉案屏 13 文本元素字号**恒 28px**
- EditMode **3270 passed / 0 failed** ✓ · PlayMode **51/51** ✓ · 覆盖门 13/13 SDF ✓ · 挂载 9/9 ✓

**遗留**:⚠️ 其余 6 屏(非脉案)的字号类(若挂 `.ink`=15px 等)仍低于采样点 —— 但**运行期零挂载**
⇒ 当前无实害;未来若运行期给文本挂 `.ink` 等类,须同批抬其字号(登记为观察项)。
**下一步**:用户重开编辑器目视复验全部文本(尤其值区 + 止血候选)。

## B-6-4 字体糊 —— ✅ 结案(2026-10-13 · 用户真机目视确认)

**用户确认:「确认字体没问题了」** —— 五轮根治全部落地并经真机目视验证通过。

**完整根因链(四层,逐层剥出)**:
1. **SDF 资产缺字形 / 非 Static** ⇒ MissingAssetReference(7773886)
2. **字体引用语法**:USS `url("guid:…")` 对字体恒不解析 ⇒ 须 `resource("Assets/…")`(0c3271b)
3. **继承被直接规则压过**:裸 Label 零类 ⇒ 落主题直接字体规则;`.unity-text-element`
   **直接匹配**才有效(2d97bbc)
4. **字号过小(SDF 缩采样)**:采样点 32px,裸 Label 落主题默认 ≈14px ⇒ 缩采样不足 = 糊;
   定稿 28px(dc4f963)

**已推送**:`706a10d..389589e`(6 提交,含 image-vision 技能)
**回归门**:EditMode 3270/0 · PlayMode 51/51 · 覆盖门 13/13 SDF · fontSize [28,28] · 挂载 9/9

**教训(已记入测试文件头注)**:batch 探针在**继承类**判据上不可信(nographics 不加载
`unity-theme://default` ⇒ 继承侥幸生效 ⇒ 假绿);须用**直接匹配**类判据,或真机目视。

---

## B-6-1 输入路径修复 ✅(2026-10-13 · 代码完成,待真机人工复验)

**病因 = 四条正交缺陷叠加**(CLI 勘察实证,非单一根因):
| # | 缺陷 | 证据 |
|---|---|---|
| ① | 启动序**从未装配 `EventSystem` + `InputSystemUIInputModule`**(ADR-013 §F1/F4 P0 义务) | `Boot.unity` 零 EventSystem;UI Toolkit 退回引擎兜底 `DefaultEventSystem` |
| ② | **零初始选中** | `InputSystemUIInputModule.ProcessNavigation` move(:858)/submit(:876)双门都 gate `currentSelectedGameObject != null` |
| ③ | **6 处回调全是 `NavigationSubmitEvent`,零 `ClickEvent`**,元素是裸 `VisualElement`(非 Button) | 鼠标点击不触发任何业务 ——「点不动」真正原因 |
| ④ | `UI/{Point,Click,ScrollWheel,MiddleClick,RightClick}` type = Value/Button,verifier 要求 PassThrough | 指针路径「引擎自己都在警告」 |

**用户四裁定**:① 仅修 B-6-1(B-6-2 焦点可见性延后);② EventSystem **运行期创建**;③ **本轮加鼠标点击**;④ Submit 键**保持 Enter**(Space 绑 Player/Jump,用户按错键)。

**修复落点**:
- `BootRoot.cs`:新增 `EnsureEventSystem()`(FindOrCreate + reparent 到 boot.transform 防 World 卸载波及 + `actionsAsset = InputSystem.actions` **顺序敏感**:AddComponent 先 OnEnable→AssignDefaultActions 填 8 引用,setter 才能按同名重指向)+ `AssertUiActionsWired()` fail-loud(8 引用任一 null 抛)+ `BootstrapEventSystemSelection()`(转 **`IRuntimePanel`** public 接口取 `selectableGameObject` —— `BaseRuntimePanel` 是 internal 会 CS0122;Update 每帧幂等兜底桥建立时序)。插入启动序 6.7 步(AssertSingleAudioListener 之前)。
- `CasebookScreen.cs`:新增 `BindSubmitAndClick(el, submitAction)` —— 同一 handler 并列挂 `NavigationSubmitEvent` + `ClickEvent`(P-04 双导航等价;**不引入 `Clickable`** —— 它不处理 NavigationSubmitEvent 会丢键盘路径);6 处转换:病名栏/折边/页缘前后/戥子档行/病名册条目。**闭包陷阱已修**:戥子档行 `for` 循环变量直接捕获 ⇒ 全行共享 loop 变量,循环后 = detentCount+1 ⇒ 每行提交越界档序数(e2e 测实测「档序数 6 越出 [1,5]」红)⇒ 拷贝 `rowOrdinal` 局部变量再闭包。
- `InputSystem_Actions.inputactions`:5 个指针动作 type → PassThrough;**不影响 Player map**。

**新增 wiring 测**(`ui_input_module_wiring_test.cs`,三门):
- **A 结构门**(batch 主判据):真启动序后恰 1 EventSystem + 挂官方桥无 StandaloneInputModule + actionsAsset 同一 + 8 引用非 null。⚠️ 夹具**复用** `camera_follow_wiring_test` 泄漏的预摆 Main Camera(全量 run 里它是同 run 后续 BootRoot 测试的 listener 门依赖;再造一台 = listener=2 抛)。
- **B 选中门**:生产方法 bootstrap 后 `currentSelectedGameObject == panel.selectableGameObject`。
- **C 鼠标语义门**:直投 `ClickEvent` 到病名栏 ⇒ `IsLexiconBookOpen`。⚠️ **必须 `ev.target = bar`** —— 无参 `GetPooled()` 的 elementTarget 为 null,派发回退面板根(-nographics 下 `GetTopElementUnderPointer` 返 null),气泡方向「目标→祖先」到不了后代 bar;引擎 `ClickDetector.SendClickEvent` 亦先 `set_elementTarget` 再发(IL 实证)。指针端到端(-nographics 依赖 Screen.height)不可行 ⇒ 语义直投,真机命中归人工腿。

**回归门(全绿)**:
- wiring 测 3/3(`Logs/b61_wiring.xml`)
- **PlayMode 全量 167/167**(`Logs/b61_playmode2.xml` —— 含 e2e_full_loop / page_turn 闭包修复回归)
- EditMode **3270 passed / 0 failed** / 46 skip / 1 inc(`Logs/b61_editmode2.xml`,与基线 em-final 一致)

**⚠️ 待真机人工复验(决定性,归用户)**:编辑器 Play ⇒ 走近按 E ⇒ 值区出词 ⇒ ① 鼠标点病名栏 ⇒ 病名册摊开;② 方向键移焦点 + Enter ⇒ 同样摊开。**Space 不是 Submit 键**。真机绿之前 sprint-08 不得 Complete(承 round6:124-126)。

**不在本批**:B-6-2 焊点可见性(`.focus-visible` 全工程零施加点,需独立接线 + 挂起裁项「加载序未钉死」)· `FocusNavigationBridge` 死代码归 P1a · B-6-3 可读性(数值归用户)。

---

## 值区横排修复(2026-10-14 · 布局乙案 · 代码完成,待真机复验)

**用户真机报告**:按 E 值区出词后,前案/后案被「挤在」问诊行与止血(急救候选)之间。

**探针实测根因**(`unity/Logs/layout_probe.log`,临时探针用毕删):通道行默认**竖排**,出词后每词 +59px ⇒ 六行案面 +352px(571→923)⇒ 总自然高 1245 > 面板 1234 ⇒ **UI Toolkit flex 默认 `flexShrink=1`** 把尾部功能行压扁:前案 95.1→64.3 / 后案→66.9 / 急救候选每项 59.1→43.7。

**修法(用户裁定「按乙,不够再加冻结收缩」—— 实测乙单独即够,未动收缩)**:
- `CasebookScreen.AddChannel`:行改 `flexDirection=Row` + `flexWrap=Wrap` + `alignItems=Center`(题签居左、词条向右并列;判据权威 = diagnosis-system Edge Cases「全部并列显示」+ UI-8.1 ASCII「面色 │ ____」);
- `SkeuoPaper.uss`:`.channel-reading*` +`margin-right: var(--skeuo-shared-space-sm)`(并列间距)· `.channel-label` +`margin-right: var(--skeuo-shared-space-md)`(题签与首词条间距)。

**修后探针实数**(`Logs/probe2.log`):通道行 95.1→**100.3**(横排,+5.2;竖排时 +59.2)· 前案/后案 **95.1 恒定不缩水** · 急救候选 177.4 恒定 · 总高 ≈1028 < 1234 零溢出。

**新回归门** `casebook_reading_inline_test`(PlayMode,1 测):出词后通道行高 < 空行行高×1.25(竖排回退即红)+ 前案行高 > 空行态×90%(被压即红)。**变异实测**:改回 `FlexDirection.Column` ⇒ 恰红 1;python 还原 diff IDENTICAL。

**回归门(全绿)**:新测 1/1 · **PlayMode 全量 168/168**(基线 167+1 精确对账)· EditMode **3270 passed / 0 failed** / 46 skip / 1 inc(与 b61 基线一致)。日志:`Logs/inline_test.xml` · `Logs/inline_playmode_full.xml` · `Logs/inline_editmode_full.xml`。

**⚠️ 待真机人工复验**:按 E 后前案/后案应保持正常两行(不被挤);多词条通道题签与词条同行并列。真机绿前不收口。

**未提交**(承 B-6-1 段):BootRoot.cs · CasebookScreen.cs(含本轮横排)· inputactions · wiring 测 · 本轮新增 `casebook_reading_inline_test.cs`(+meta)· SkeuoPaper.uss · **本轮 R=20 裁定**:CompositionRoot.cs · e2e_full_loop_test.cs · examine_driver_test.cs —— 待用户指示后 commit。

## 「点开 Play 即可按 E」(2026-10-14 · R=20 已回退 → **A 案 plateau 曲线** · 用户裁定)

**用户报告**:真机要等 ~100 s 才能按 E 看到值区出词;随后用户指出「R 2000→20 **没有实现**,按 E 很久没有任何动静」。

**① 回退(已执行)**:按用户裁定「只回退 R=20 三处」—— `CompositionRoot.cs` / `examine_driver_test.cs` / `e2e_full_loop_test.cs` 恢复 HEAD(B-6-1 输入路径 / 值区横排 / 新测等未提交改动**保留**)。

**② CLI 探针取证(已跑,用毕已删)**:`e_probe_chain_test.cs` 五段链路探针,日志 `Logs/e_probe_run7.log`。
- **判决:链路全通** —— 词表 34 行 / 门槛 / 体征 / 查体驱动 / 分册快照无一环断。
- **根因**:fallback 条目 `Curve = null` ⇒ 走 legacy 曲线,其 rise/decay **共用同一个** `RecoveryTime`
  (`ProgressionEvaluator:240/250`)⇒ `position` 直到 **τ≈2001(≈100 s @20 Hz)** 才越 floor 0.9752197;
  越过后查体**立即**收 **19 条词**(`SignCount=19` / `RevealedRows=19`)。
- **不是「路径断」,是「等不起」**;且 R=20 同样不可用(峰值 τ=40,此后每 20 tick 腰斩 ⇒
  阈值窗口 ≈1 s),**两个极端都不可用 = 形态问题,调 R 调不出来**。

**③ A 案(用户裁定「按A做」· 已落)**:换曲线形态,治本 —— `CreateFallbackRegistry` 真配一张
**plateau 型 F1 三分支**(`self_limit=false ∧ plateau=true` ⇒ `ComputeBaseF1` 在 τ≥τ_peak 恒返回
`A_peak`,**达峰后不衰减**),使「立刻可读 ∧ 窗口无限」成为曲线固有属性。
- 数值:`Incubation=20`(1 s)· `TauRise=5` · `TauPeak=40`(2 s 达峰)· `APeak=1` · `Scale=1`
  ⇒ plateau 处 `position ≡ 1.0` ≥ `floor(skill=1)=0.9752197`。
- ⚠️ **禁 `FixParse`**(它是编辑期/烘焙期类型,ADR-014 ⇒ 运行期出现 = `AC-21a-48` 守卫恰红,
  **首轮已踩**,改走 raw 域 `new Fix(5 * Fix.OneRaw)` 后转绿)。
- `Lethality=0` **不踩 R1-22**(该门只在 `!self_limit ∧ !plateau` 触发)⇒ plateau 型无需 lethal。
- 校验逐条过门:R1-20 互斥 · R1-21(20≥0 ∧ 40>20 ∧ 5>0)· R1-30(`riseRatio=20/5=4` ⇒
  `R_rise≈0.982` ≫ 1 LSB)· R1-23(Scale=1 ≥ A_peak=1)· R1-24 · R1-27(`"monotone"`)· R1-28(null 不查)。
- `RecoveryTime = 2000` **保留**(仅剩 R1-08 校验面 + Curve==null 的 legacy 占位条目用),
  旧长注释整块改写为「为何弃用调 R 这条老路」。

**同步改判的测试**:
- `examine_driver_test.positionCrossesFloor`:旧「可读窗口 ≥ 600 tick」是衰减型产物 ⇒ 改为
  **① 早可读 `firstReadable ≤ 60 tick`**(潜伏/达峰被推回 2000 量级 ⇒ 恰红 = 用户「等很久」的病根)
  **② 永不掉出 `lastReadable == 6000`**(改回 self_limit 衰减型 ⇒ 窗口有限 ⇒ 恰红);
- 同测 `examine_yieldsWords`:`risePeak` 改 60(稳落 plateau),注释订正;
- `e2e_full_loop_test.AdvancePastIncubation`:2500 → **60 tick**(潜伏 20 + 上升 20 + plateau 余量);
- `test_examine_belowFloor_emptySet_noMask` / `composition_root_test` 的 τ=0 潜伏断言:补
  「依赖 `Incubation=20 > 0`」理据(改 0 则恰红),判据不变。

**回归门(全绿)**:`ExamineDriverTest` 过滤 **11/11** · 守卫门(`DataPipelineBakeTest` 含 AC-21a-48)
+ 两 Boot 测 **70/70** · EditMode **3270 passed / 0 failed** / 46 skip / 1 inc(与 b61 基线逐项一致)
· PlayMode **168/168**。
日志:`Logs/plateau_examinedriver.xml` · `Logs/plateau_guard.xml` · `Logs/plateau_editmode_full2.xml`
· `Logs/plateau_playmode_full2.xml`。
⚠️ 首轮 PlayMode 6 条红 = **临时探针污染场景**(其 SetUp 持裸 `BootRoot`,`AudioListener` 计数漂移;
全量报 2、隔离报 0)**非曲线之过** —— 探针已删(文件头「用毕删」),复跑 168/168。

**✅ 真机人工复验(2026-10-14 · 用户确认全绿)**:过潜伏后按 E ⇒ 值区出词且一直有词;
值区横排正常(前案/后案不被挤);**退出 Play 不卡**。

## B-6-1 收口(2026-10-14 · 双半达成 · ✅ Complete)

- **人工半**:用户真机确认全绿 —— 鼠标点病名栏 / 方向键 + Enter 双路径均有响应;值区横排正常;**不卡**。
- **自动化半**:`ui_input_module_wiring_test` 三条(EventSystem 装配 / bootstrap 初始选中 /
  病名栏 ClickEvent 摊册)· `casebook_lexicon_book_test` **11/11** · EditMode **3270/0/46/1** ·
  PlayMode **170/170**(基线 168 + 新增 2)。
- **评审原件**(BLOCKING 级义务,无原件不得转 Complete):
  `production/qa/evidence/review-b6-1-input-wiring-2026-10-14.md`
  —— 原判定 → 四条正交根因 → 修复落点 → 可证伪验证命令。
- **提交**:`b37653e`(输入路径接通 · 四根因 + 值区横排 + 两新测)
  · `a8c7744`(空栏点病名栏每帧抛异常 ⇒ EPIPE 杀编辑器 —— 同轮人工复验暴露)。
- **回填**:`sprint-08.md` §6 收口 · `playtest-2026-10-13-m2-round6.md` §8。

⚠️ **sprint-08 整体仍不可转 Complete** —— B-6-2(焦点可见性 P2)/ B-6-3(可读性 P2)/
B-6-4(字体资产精度 P3)各有独立 story 且未修。B-6-1 只是「修复前不得转 Complete」的那一项。

## B-6-3 收口(2026-10-14 · 双半达成 · ✅ Complete)+ B-6-4 一并判结

- **人工半**:用户真机复验「**值区清晰可辨**」—— 字不糊不灰、底衬格从纸面可辨(边线围着)、
  原两条目视红(①底衬压纹 ②24px 发虚)直接判结。
- **自动化半**:`skeuo_readability_test` **7/7**(s3_1 双墨 ≥7:1:negative 8.27 / fg 13.84 ·
  s3_2 28px 档 + 类绑定 · s3_3 零内联 · s3_4 底衬 Δ=0.0947 ≥ 0.02 含判别力守卫 ·
  s3_5 边线四向绑定)。日志:`unity/Logs/b63_readability2.xml`。
- **修复落点**(2026-10-13 二轮修定 + 字体糊三连,本轮收口核验):
  ① 底衬 #fbf9f3 → **#e7e0cd**(Δ 0.003 → 0.0947)+ **定界换机制**走四向边线
  `--skeuo-reading-rule` rgba(43,36,22,0.22)(明度差定界被实测证死:底衬越浅越不可辨、
  加深则阴性墨跌破 7:1 = 数学死结,非调 hex 可解);
  ② 字号 24 → **28px**(`--skeuo-font-size-lexeme`;28px 字盒 40.5 < min-height 44 ⇒
  行高恒 78.0px,「空行/已查行等重」不动自守)+ SDF 根因(SDF 采样点 32px vs 默认字号
  ≈14px 失配)= 「字体糊三连」四提交 `7773886`/`0c3271b`/`2d97bbc`/`dc4f963` 根治
  + 默认分辨率 1920×1080(768p 折算前置已解,本轮核验在位)。
- **B-6-4 判结**:条件性缺陷(「若是字体资产渲染问题」)查证 = 是,根因同上,根治已落 +
  人工实证 ⇒ 无需另立 story。
- **评审原件**:`production/qa/evidence/review-b6-3-readability-2026-10-14.md`。

⚠️ **sprint-08 整体仍不可转 Complete** —— **B-6-2**(焦点可见性 P2)open,独立 story。
B-6-1 / B-6-3 / B-6-4 三项均已解除。

<!-- QA-PLAN: 2026-10-14 | System: sprint-09 | Plan written: production/qa/qa-plan-sprint-09-2026-10-14.md -->

## story-009 人工走查批收口(2026-10-14 · ✅ 全绿 · 提交 `1cc3196`)

- **判读**:AC-9-10 ✅(主链 A+B 人工真机走通:长按 >3s 不误杀/松开复现/E 查体/焦点落
  「跳过」/Submit 零异常零提示/零鼠标)· AC-9-11 ✅(腿 D 感知即时)· AC-9-08 走查面 ✅
  (截图 `20261014_222810_1x.png` SenseNova 判读三项零数值载体)。
- **自动化腿 A 四红 = 注入夹具环境怪癖**(非产品缺陷):真机实证 + CI
  `test_longPressWithContinuousFeeding_notAborted` 绿双证据压过;Driver 结果 JSON
  `passed` 标志与 checks 不一致 = 工具缺陷一次性登记不修。
- **落盘**:走查脚本 §9 就地回填(判据 ✅/◐ + 留证表 + 缺陷登记)· story-009 三处
  (AC-9-10/11 翻勾 + AC-9-08 走查面补证)· sprint-10:49 · M2 README:188。
- **清理**:Story009AutoRun.cs(一次性)与 tools/x11_inject.py(改道废弃)已删;
  Driver + asmdef 入库。腿 C 未绑人工面 ◐(EditMode 结构面已覆盖,不欠账)。
- **待用户**:2 张 2048² 图片重生(进行中,用户并行任务)。~~`review-mode.txt` 未落 ·
  round-7 证据两文件待提交~~ **均结清(2026-10-14 复核)**:review-mode.txt 落 `lean`
  (用户裁定,`40b210c`);round-7 证据查实已在库(复核原件 `6458285` / 报告
  `24fe0fb`+`bc704be`),前述「待提交」为陈旧转述。

## 图片重生轮收口(2026-10-14 · ✅ 超规 2 张结清 · 提交 e891ecd/1c03f2f/53ebdc0)

- **成果**:AC-42-C9 超规残留(`ui_icons_sprite` / `casebook_paper_base` 2048²)结清——
  ① t2i 原生重生 1024²(`watermark: false`,用户裁定;弃带 SenseNova 水印的降采样路径);
  ② PIL 频域补偿降采样(纤维 std 7.59 保持,淡墨 p50 7.18:1 过门)。
  两张入库位替换,meta/GUID 未动。2026-10-05「2048 不降」旧裁定作废(顾虑经频域补偿消解)。
- **实测门**:`atlas_page_count_test` 首跑 1 红 = 旧断言(合格 16/超规 2)成化石——红恰是
  重生目标达成证据;断言更新 18/0 后 **2/2 Passed**(`s09_atlas_reimport2.xml`,`1c03f2f`)。
- **流程失误登记(我)**:atlas 复验连发两个 batch(第一个 `-testsFilter` 无效参数),
  违反「禁并行起第二个」纪律——第一个已自行退出无实害;随后一次 OCCUPIED 误报
  (pgrep 自匹配时序)。教训:filter 参数名先核文档再发。
- **遗留**:sprint-09 收尾小件三项(review-mode.txt / round-7 证据 / 2 张图片重生)
  **全部结清**;story-009 走查批亦结清。当日链:走查全绿 → review-mode 落 lean →
  图片重生 → atlas 门复验绿 → 五提交全推送(1cc3196/40b210c/a7d1676/e891ecd/1c03f2f/53ebdc0)。

## 🏁 M2 收口(2026-10-14 · Exit Criteria 10/10 全勾)

- **末条 `:253`(五族切图与 atlas 布局冻结)翻勾**:原阻塞「019-b Blocked(PAGES_MAX
  未冻结)」已陈旧——冻结件 round-2 重冻(超规 2→0)+ 实测门 2/2 + 超规图重生结清,
  三证据链闭合。复评原件 `production/qa/evidence/m2-exit-recheck-sprint10-253-2026-10-14.md`。
- **三残留裁「不卡 M2 Exit」转独立待办**:R1 构建期瓦片门接线 · R2 真机瓦片实测 ·
  R3 GDD F5 量纲修订(禁借绿声明在档,实质化须重开冻结轮)。
- **M2 就此 Complete**(汇总表行已翻 ✅)。
- **下一阶段**:Phase 2 剩余四系统 = patient-ai → diagnosis-system → case-system →
  prescription-medication(M3 Systems Complete 主线,16/31 epic)。
<!-- QA-PLAN: 2026-10-14 | System: sprint-11 | Plan written: production/qa/qa-plan-sprint-11-2026-10-14.md -->

## S11-1 收口(2026-10-15 · ✅ · 提交 44bb2ee/47e0fc4)

- **实测骤缩(开工前查证)**:查体→值区端到端链已通(第五轮 Q1 人工验证 +
  queryLevel 修复 + sprint-09 S3 复评逐条实测「④ 已接」)——规划研判依据的
  「RevealedRows 恒空」是 10-11 旧快照。用户裁定收窄执行。
- **支1 O-13-7 结案**:10 侧 = 施治按住沿(emergency-procedures.md 下游 13 行)/
  8 侧 = 瞬时动作不产生会话沿(diagnosis-system.md)/ patient-ai.md 四处登记结案。
- **支2 发出侧核查**:PatientBehavior.OnExamSessionChanged 消费入口在
  (:148);PatientBehaviorDirector 全库零生产构造点(仅测试)⇒ 13 行为机
  未进生产装配 = story-005 表现装配轮范围,非 S11-1 缺口(登记为契约)。
- **支3 Edge 测试补面**:四条 Edge 三条已有测钉住,补多病人隔离 2 测
  → ExamineDriverTest **13/13**(batch,退出码 0)。
- **连带发现(登记)**:story-005 真模型卡外部美术之外,**13 表现装配轮
  (PatientBehaviorDirector 生产构造 + 行为 Step 驱动挂 BootRoot tick 序)
  也是独立待办**——此前无 story 承载,归 sprint-12 排期面。

## S11-3 收口(2026-10-15 · ✅ · 提交 2d20e71)

- **修复面**:host_authority_test.cs(3 处)+ o6_actor_cell_payload_test.cs(5 处)
  `new PlayerControllerType()` → `CreateController()` 真夹具
  (new GameObject + AddComponent<CharacterController> + AddComponent<PlayerController>
  + [TearDown] DestroyImmediate);显式先挂 RequireComponent 依赖,
  否则 Awake 缺件告警同样污染 UnityLogCheckDelegatingCommand 日志门。
- **连带残源实证**:全量退出码 2 的另一半 = settings_exposure_test.cs:126 显式
  `Assert.Inconclusive`(单独跑该类也退出码 2)—— UTF 把 Inconclusive 计入
  非零退出码。用户裁「删 Inconclusive 行」:注册表存在性真断言保留(非假绿),
  「从烘焙分区读默认值」义务注释留档归 44 生产轮。
- **纯测试面**:3 文件 67+/13-,零产品代码。
- **终验双信号**:全量 EditMode 3328 total / 3282 passed / **0 failed** /
  0 Inconclusive,BATCH_EXIT=**0**;定向 14/14 + `new MonoBehaviour`/缺件日志 0。
- **教训(工程向)**:显式 `Assert.Inconclusive` 占位 = 潜伏的 CI 信号污染源
  (XML 层看不出来,只有退出码知道);后续登记性占位一律用注释,不用 Inconclusive。

## S11-2 收口(2026-10-15 · ✅ · 提交 4565dc6)

- **主裁决(用户裁定)**:W-1 裁甲案 —— 三案(ScriptedChain)各挂 `DIS_DYSENTERY`,
  走 F-37.3「一案进多组」现成机制 ⇒ `disease_set` 非空,`PatternRecognized` 可触发;
  `AC-37-12` 原样不动;乙案(扩 D 域含 injury)否决留档。
- **连带会签(用户裁定)**:图样可辨识度 × 8 可读性 = **结构性不互斥**;
  可证伪判据 =「词族重合 > 噪声重合」;禁改 AC-8-19 迁就 Boss;playtest 验证
  义务保持登记(Open Questions 行不删)。
- **落盘五处**:case-system.md(W-1 注块 ✅ 段 + OQ 行)· he-returns.md(§四 表
  增挂痢疾轻症 + 引言行)· campaign-arc.md(头注 + 落位注 + :82 行内括注)·
  random-events.md(52 侧边界注)· systems-index.md(二次修订行)。
- **登记项**:① 先触发歧义(自然痢疾先凑满 3 例)归 37 实现轮澄清;② 内容义务
  (三案脉案按「痢疾轻症 + 失血主诉」微调)归内容轮;③ 具名角色 → patient_id
  绑定值仍归实现期(原有,不变)。
- **「不裁不得实现联调」解除;支柱四 P0 落点维持。**

## S11-2 后续批 — QA 核查(case-system 001–004 评审原件)(2026-10-15 · ✅ · 提交待做)

**触发**:`production/qa/qa-plan-sprint-11-2026-10-14.md:153` 登记的开工前置核查。

**核查问题**:case-system 001–004 于 2026-10-06 收口,而 `production/qa/evidence/`
仅存 005/006 两份原件 ⇒ **当年评审有无 BLOCKING 判定?**

**核查结论**(`review-case-system-001-004-gap-audit-2026-10-15.md`,7 条证据链):
- 001/002 **确证**产出过 BLOCKING 级判定(commit message 自述留痕);
- 003/004 判定分级**未能确证**(无证据表明「无 BLOCKING」);
- 当时**已存在补件惯例**(`7362ae9` prescription-medication),case-system 未执行;
- 义务(2026-10-02 立)适用于这四件。**用户裁定:四件全补**。

**执行**:8 代理并行(4 story × 代码面 unity-specialist + 测试面 qa-lead),
四份 `-retro-2026-10-15.md` 原件全部落盘(双面合并)。

| 对象 | 代码面 | 测试面 | 核心病灶 |
|---|---|---|---|
| 001 | **0** | **4** | AC 断言形态:生产载体零调用 / 恒真 / A2 空跑 / kindgen 借绿 / AC 号一号两义 |
| 002 | **4** | **10** | 入流义务零验证(查路由表 ≠ 入流)· `LastJudgmentOf` `caseId` 死参(**有生产实害链**) |
| 003 | **1** | **5** | **`IsClosed` 按 `Patient` 匹配 ⇒ 复诊案永远无法结案**(dotnet 8 已证伪)· `InjuryOnset` 入处置集无规格授权 |
| 004 | **3** | **5** | **AC-37-27 定序判别力为零**(期望序 ≡ 插入序,删 Sort 仍绿)· 发射语义无生产载体 |

**关键工程发现**:
- **四件的病灶高度同型**:AC 的语义层不被实现保证,而由**测试自造断言 / 未写的调用方契约**顶替
  ⇒ 「全绿」与「AC 被验证」是两回事(绿态下 BLOCKING 全部成立);
- 001 的**历史自述已兑现**(「A3 恒真 / A5 单向」在当下代码中已不成立),但补评**新发现** 4 条;
- 002/003 各有一条**能证伪的真实生产实害链**(非纯测试瑕疵)。

**义务第 2 条回填**:四件 story 的 `## Test Evidence` 段追加补评登记块
(原件路径 + 判定 + 门结论 + 「`Complete` 有效性待裁」);**未擅改 `Status` 行**
(状态变更归用户裁定 + `/story-done`)。

**连带**:story-004 的 `BLOCKED-BY-W-1` **已解除**(W-1 于今日会签结案)——
但解除的是**夹具的免责前提**;`ChainEligible` 谓词实现本身**同样零命中**,两件事已分别登记。

**边界**:补评发现的一切 BLOCKING,**修复归新一轮**;不得声称「原判定如此」;
本核查不构成对 2026-10-06 那次评审的追认或推翻。

**用户裁定(2026-10-15)**:
- ① 四件 story 状态 **转 `In Review`** —— 已落盘五处:story 头部 `Status` 行 ·
  story `## Test Evidence` 的 `[x] Complete` → `[ ] In Review` · 登记块末句 ·
  EPIC.md 头部 `Status`(6/6 Complete → **4/6 In Review**)· EPIC.md 状态表四行;
- ② 本次核查产物**提交(不 push)**。

**仍未定**:BLOCKING 修复的排期范围(是否并入 sprint-11 剩余容量 ——
现存 should-have S11-4 处方 DoD 残留 3.5pd / nice-to-have S11-5 53 起首 1.5pd / S11-6 走查 0.75pd)。

## S11-2 后续批 · 勘误轮(2026-10-15 · ✅)

**触发**:执行 §六 登记的「未完成核查」第 1 条(查 `tests/integration/case-system/`),
连带发现**补评本身有系统性盲区**。

**关键事实**:
- `tests/integration/case-system/` 确实不存在 —— 但 `tests/README.md` 澄清 `tests/unit|integration/`
  只是**账本约定**,UTF 实际编译 `EditMode/` + `PlayMode/` 两棵树;
- case-system 实际测试面远不止各 story 声明的文件:`EditMode/Boot/` 下 `judgment_writer_test.cs`(4)·
  `ink_age_test.cs`(4)· `casebook_command_router_test.cs`(8);`EditMode/CaseSystem/` 下
  `case_open_writer_test.cs`(7,真组合根+真流)· `case_replay_persistence_test.cs`(10);
  `PlayMode/case_opened_wiring_test.cs`(真启动序)· `e2e_full_loop_test.cs`(七环节全链);
- **PlayMode 基线**:`story009_review_playmode.xml`(10-14 20:00,`total=186 passed=186 failed=0`)。

**勘误结果**:
- story-002:**推翻 4 条**(AC-37-30 入流 / AC-37-05 判断半边 / AC-37-05 处置半边 / AC-37-23 不折叠
  —— 均有真载体:真组合根件 + 真启动序件);**保留并加重 1 条**(`LastJudgmentOf` 死参,
  新发现的 `ink_age_test` 4 测全为单案夹具 ⇒ 跨病人混淆仍无覆盖);
- story-003:**推翻 1 条**(B4 —— →8 读数清除 / →53 冻结 在 `DiagnosisSystem/reading_state_test.cs:395-411`);
  保留 4 条;
- story-001 / story-004:**无勘误**(全树 grep 确认声明件是唯一载体)。

**根因(两方各半)**:① 调度 prompt 只指向声明件;② 代理在仅核一件时作「零验证」「全仓零命中」
等**越权全域否定**。→ 建议补入 `review-workflow.md`:**评据不得越权**(作全域否定前须给搜索命令与结果)。

**净结论不变**:四件 Test Evidence 门仍不通过,维持 `In Review`;
但 002/003 的性质改为「**声明证据与 AC 错配**」(修复面 = 补引用 + 订正断言形态,非从零补测试)。

---

## 「两条都落」收口(2026-10-15 · 用户指令)

用户裁定落两条流程建议(源自 §七·四):
1. **`review-workflow.md` 补第二失效模式** —— 义务三条 → **四条**,新增义务 4「**评据不得越权**」;
2. **Story §Test Evidence 允许多路径** —— `AC → 载体映射` 表(每条 AC 一行、**允许多条**)。

**落点(8 改 + 1 新)**:
- `docs/review-workflow.md` —— 义务 4 全文(为什么立 · 两条结构性诱因 · 操作口径);
- `skills/create-stories/SKILL.md` —— §6 story 模板新增 `AC → 载体映射` 表 + 警告块;
- `skills/story-done/SKILL.md` —— Logic/Integration 检查逻辑改**遍历多条所列路径** + **两树都搜** + 扫全树警告;
- `skills/test-evidence-review/SKILL.md` §3 —— 先读映射表 + 两树搜索 + 全域否定须贴搜索输出;
- `skills/qa-plan/SKILL.md` —— Test Summary 表与 `Test file path` 行改实际路径 + 注;
- `docs/coding-standards.md` §Testing —— 表 Location 改实际路径 + 三条注(多载体 / 两树 / 全域否定);
- `agents/qa-lead.md` —— 证据表 + 「评据不得越权(你曾在此失手)」警告(直指越权全域否定);
- **新** `agent-memory/qa-lead/review-scope-discipline.md` + `MEMORY.md` 索引行。

**⚠️ 落盘过程中的自我勘误(重要)**:初稿把这层写成「账本路径 **≠** 编译面,
须改查 `unity/Assets/Tests/`」—— 这**本身就是一条越权断言**。实测 `tests/`
并非恒空:`tests/unit/skeuomorphic-ui/` 持有 `focus_gate_and_bridge_test.cs` + **自带 `.asmdef`**,
是活测试工程;`tests/README.md` 说的是「**工程落地时的合并事项**」(未做)。
⇒ 全部落点已改为「**两棵树都可能承载活测试,须都查**」,并明写反向警戒:
「不得反过来说『账本树已作废』—— 那只是把越权换了个方向」。
`grep` 口径相应改为 `unity/Assets/Tests/ tests/` 两树并扫。

**未动**:`smoke-check` / `regression-suite` / `test-setup` / `templates/test-plan.md` /
`agents/qa-tester.md` 内的同型路径字面 —— 那些是**新建测试的落点建议**,
不承担「判证据缺失」的判定职责;待新建测试的真实落点约定定案后一并处理。

**仍待用户裁定**:四件 BLOCKING 的**修复排期**(是否并入 sprint-11 剩余容量)—— 未擅自启动。

---

## case-system 001–004 修复排期并入 sprint-11(2026-10-15 · 用户裁定)

**用户三项裁定(均「按推荐」)**:
1. 排期口径 = **必须项优先** —— 本轮只取「真缺陷 / 免责失效 / 设计冲突 / 零成本」四类,
   断言形态整批顺延 sprint-12;
2. 37 写者(`CaseClosed` / `PatternRecognized`)= **正式登记延后**(写进 §Out of Scope + 订正证据面);
3. 处置集 = **撤 `InjuryOnset` + 补 `CareApplied` + 回填权威**。

**落盘(9 文件)**:
- `production/sprints/sprint-11.md` —— Goal 追加 · Capacity 重算 · 新增「Must Have — 补评修复批」节
  (S11-7…S11-11)· DoD +3 条 · Risks +2 行;
- `production/sprint-status.yaml` —— 追加 S11-7…S11-11(五条 must-have / ready-for-dev),
  `updated` → 2026-10-15;**既有条目状态一字未动**;
- `production/epics/case-system/EPIC.md` —— 头部追加修复排期 + 两处裁定落点;
- 四件 story:001/002 追加**修复排期指针**;003/004 追加**写者延后正式登记**(§Out of Scope);
- `docs/architecture/adr-008-case-event-stream.md` §四 —— **`treatment event` 枚举**补立(唯一权威);
- `design/gdd/case-system.md` —— F-37.2 判定式 + 变量表 + 订正框 · 规则六 `:254` · 规则八 `:280` 四处同步。

**容量**:
- 可用 ≈6.4 · 已用 3.75(S11-1/2/3)⇒ 余 ≈**2.65** · 既有 backlog 5.75(已超容 3.1)
- 新批 **2.85**(S11-7 0.75 / S11-8 0.75 / S11-9 0.75 / S11-10 0.5 / S11-11 0.1)
- ⇒ 本轮合计超容 ≈5.95;**处置 = S11-6(nice 0.75)顺延吸收,S11-4/5 不动**

**本轮取的五项(均为「真」类,非断言形态)**:
- **S11-7** story-002 BLK-8:`LastJudgmentOf` 以 `caseId` 过滤 + `CompositionRoot.cs:245` 传该案子集
  (现传全局混合流 ⇒ **墨龄取错病人**)—— **真实现缺陷,有生产实害链**;
- **S11-8** story-003 B-1:`IsClosed` 改按 `CaseId` 三元组(现按 `Patient` ⇒ 复诊案**永远无法结案**,
  已 dotnet 8 行为证伪)—— **真实现缺陷**;
- **S11-9** story-004 CB-2/TB-5:`ChainEligible`/`ScriptedChain` 落地 —— **免责前提当日失效**(W-1 已结案);
- **S11-10** story-003 B5:处置集撤 `InjuryOnset` 补 `CareApplied` + 权威回填 —— **设计冲突**
  (击穿「空手连关三例不可能」);
- **S11-11** story-001 B-3:测试头 AC 号一号两义改无编号 —— **零成本**。

**顺延 sprint-12(须逐条带状态+归属,四件维持 `In Review`)**:
001 的 B-1(AC-37-06 判据**算术上不可证伪**,须退设计重述)· B-2/B-4(kindgen);
002 的 5 条断言形态 + §Test Evidence 补引用面;
003 的 B2(**写者未落 ⇒ 无载体可测**,已随延后登记)· B3(须澄清「病人死亡可否表态」);
004 的其余 7 条(TB-1 定序判别力 / TB-2 成员冻结 / TB-3 真空夹具 / CB-1/CB-3 + `TA-6` 元规则)。

**新增风险(已入 sprint-11 Risks)**:顺延面 ≈15 条 ⇒ 四件不得转 Complete;若 sprint-12 再被挤占,
回到用户裁(不自行降级)。另:**S11-7/S11-8 触及生产代码**(前几批治理轮均为纯测试面)⇒ 须先跑 M2 链基线。

**下一步候选**:S11-7 或 S11-8(两处真缺陷,无依赖)。~~**未启动**~~ → **S11-8 已于 2026-10-15 闭合**(见下)。

---

## ✅ 本轮 = S11-8 闭合(story-003 B-1 · 2026-10-15 · 代码 + 测试 + 记录三面)

**用户指令**:**「做 S11-8」**;范围裁定 = 「**同批重构 `IsOpen`**」。

**落点**(`unity/Assets/Sim/CaseCloseDecider.cs`):
1. `CaseCloseQuery.IsClosed` → **流序扫描关联**(`current` 随该病人 `CaseOpened` 更新;遇该病人
   `CaseClosed` 时**仅当 `current == caseId`** 判已结,随后清 `current`);
2. `IsOpen` → 统一为 `OpenedInStream(c) ∧ ¬IsClosed(c)`,与 `IsClosed` **同一输入契约**(消 ADVISORY A-6);
3. `CaseCloseDecider.Decide` **两判序对调**(`isClosed` 先于 `isOpen`)—— 流派生下已结案 `IsOpen` 恒假,
   旧序把已结案报成 `RejectNotOpen`(**错因码**);`(true,true)` 在原序下**只可能来自硬编码**,现真正不可达。
   **结果面零变化**(两分支皆为拒绝且不写事件),改的只是理由码。

**⚠️ 与计划/补评的两处偏离(均已报用户并获批)** —— 计划写了一个**做不到的形状**:
- **① 「经 `PayloadRef` 解码 `CaseClosedPayload.CaseId` 判等」结构上不可行**:`PayloadCodec` 住
  `Sim.Codec`,`Sim` 引用集**恰 = {BCL, `Sim.Contracts`}**(ADR-025 §① / b2 门)⇒ **够不着**。
  改走流序扫描(与既有范式 `CaseStreamQuery.OpenCaseOf` 同口径)。**代价已登记在实现注释**:
  归属是**推断**非**读取**,与载荷解码**在合法流上等价**,仅在不合法流(违反「开案唯一」)上分歧。
- **② 契约取「整条全序流」而非「该案子集」**:两函数签名都收整条 `caseEvents`,而 `Sim` 侧没有
  按 case_id 切片的合法手段(同 ① 约束)⇒ 统一为「入参 = 病例流全序前缀」。

**测试面**(`case_close_precondition_test.cs`,33 → **35 测**):
- B-1 回归哨兵 `test_isClosed_revisitNewCase_notClosed`(**含反向对照**,防「一律 false」假修);
- **端到端组装用例** `test_e2e_revisitClosePath_deriveBothFlagsFromStreams` —— 补补评 **A-3**:
  此前全文件**无一条**把**两条流**接进 `Decide`,是 B-1 在测试面**结构性隐形**的根因;
- 解除 **4 处硬编码状态标志**(`:133`/`:146`/`:162`/`:510`)改为流派生(A-3/A-6 的假绿土壤)。

**证伪链(双信号一致)**:
| 状态 | 退出码 | 结果 | 证据 |
|---|---|---|---|
| 修复前(旧实现 + 新哨兵) | **2** | 34 测 33/1 **红** —— 唯一红 = 哨兵 | `Logs/s11_s8_before.*` |
| 修复后 | **0** | 35 测 **35/0** | `Logs/s11_s8_after.txt` |
| **改回旧实现**(DoD 要求) | **2** | 35 测 28/**7 红** | `Logs/s11_s8_revert.xml` |

7 条红恰**两类**:B-1 粒度 4 条(其中复诊 Edge 由 `CanClose` 退化为 **`RejectAlreadyClosed`** =
「**复诊案结不掉**」生产症状的**本体**)+ **判序 3 条**(`RejectAlreadyClosed` → `RejectNotOpen`)。
⇒ 两处改动**各自承重、独立可证伪**。

**全量零回归**:EditMode `3330 total / 3284 passed / **0 failed** / 0 inconclusive / 46 skipped`(退出码 0)·
PlayMode `186 / 186 / **0 failed**`(**逐位等于 M2 基线** `story009_review_playmode.xml`)。
EditMode 较 10-14 基线 +4,**逐名 diff 确认零条消失**,增量 = 2 条本 story 新增 + 2 条**已提交**的
`ExamineDriverTest` 多病人隔离用例(`47e0fc4`)。

**记录面**:**新证据原件** `production/qa/evidence/fix-s11-8-case-system-story-003-b1-2026-10-15.md`
(⚠️ **必须落原件**:`unity/Logs/` 在 `.gitignore:196` ⇒ 日志是易失中间产物,**不可充当交付证据**;
原件的判据/命令/输出原文**自洽可复核**)· story-003 🟢 登记块 + §Out of Scope B-1 行转「已闭合」·
`sprint-11.md` S11-8 转 ✅ + **两处口径订正框** · `sprint-status.yaml` S11-8 → `done` · `EPIC.md` story-003 行。

**未清(不因 S11-8 转绿)**:story-003 维持 `In Review` —— ② 快照半场(写者未落)· ③ AC-37-18 恒真化(A-4)·
⑤ `TreatmentKinds` 收 `InjuryOnset`(**排 S11-10**)+ ADVISORY A-1(可变 `public HashSet`)/ A-5。
⚠️ **A-1 与 S11-10 同址** ⇒ S11-10 应顺批把 `TreatmentKinds.Set` 收为不可写出口(`private` + `Contains`)。
⚠️ **AC-37-03 不整条转判**:流层判据已覆盖,但「**不产生新事件**」半边**仍无载体**(写者未落)—— 未断言事件数 = 0。

**下一步候选**:S11-7(另一处真实现缺陷,`LastJudgmentOf` 死参 —— 实测**仍为未修态**)· S11-9 · S11-10 · S11-11。
**S11-8 改动尚未提交** —— 待用户指示。

---

## ✅ 本轮 = S11-10 闭合(story-003 B5 + 顺批 A-1 · 2026-10-15)

**用户指令**:**「提交 S11-8,然后做 S11-10」**(A-1 顺批已含在所选选项描述内)。

**落点**(`unity/Assets/Sim/CaseCloseDecider.cs` · `TreatmentKinds`):
1. **撤 `InjuryOnset`**(25 战斗伤害结算 ⇒「病人挨了打」被计入「已被处置」⇒ 玩家把病人打到即可满足
   结案前置,**正面击穿** `case-system.md:567`/`:839`「空手连关三例 ⇒ 不可能」);
2. **补 `CareApplied`**(8 护理动作,病史流)—— 权威枚举第 3 员,原实现**漏收**,取舍方向与语义**完全相反**;
3. **顺批 A-1**:`public static readonly HashSet Set` ⇒ `private` 真源 + `Contains` + **`MembersSnapshot()`(返副本)**。
   ⚠️ **实测无外部写点**(全仓 grep:仅 `CaseCloseDecider.cs:151` 读 + 本夹具)⇒ A-1 是**未被触发的隐患**,
   **不是已发生的故障** —— 已按此口径登记,**不夸大**。

**权威对齐(ADR 闭环纪律四处,已逐处核)**:
`adr-008` §四 `:229` · `case-system.md` 规则六 `:255` · 规则八 `:283` · F-37.2 `:671`/`:674`/`:686`/`:709-736`
—— 四处均写 `{EmergencyTreatmentApplied, DrugTreatmentApplied, CareApplied}` 且均排除 `EmergencyAttempt`/`InjuryOnset`。
`tr-registry.yaml` 不枚举处置集,**不属**闭环纪律点名的四处。
⚠️ **权威补立先于本修复**(2026-10-15 同批裁定,`adr-008` §四)⇒ 本修复是**执行**,不是**再裁**。

**测试面**(37 → 39):
- **撤** `test_treatmentKinds_containsInjuryOnset` —— 该测**反向断言了缺陷**(`Contains(InjuryOnset) is True`),
  是 B5 逃过测试面的**直接原因**;
- **新增** `containsCareApplied` / `excludesInjuryOnset` / `excludesEmergencyAttempt`(末者守住原有**正确排除**);
- **双向穷尽** `equalsAuthorityExactly` —— 期望值**逐字引自 `adr-008` §四**(不引生产默认值,兑现 S11-10 AC);
  单向 `Contains` **抓不到**「偷偷多收一员」,而 B5 正是**错收**这样逃过去的 ⇒ 本测是**充要**判据;
- `snapshotIsIsolatedFromSource` —— 污染快照后真源须纹丝不动(A-1 的经验判据)。

**证伪链(双信号一致)**:
| 状态 | 退出码 | 结果 | 证据 |
|---|---|---|---|
| 修复前(旧成员表 + 新哨兵) | **2** | 37 测 35/**2 红**(恰为 `containsCareApplied` + `excludesInjuryOnset`) | `Logs/s11_s10_before.*` |
| 修复后 | **0** | 39 测 **39/0** | `Logs/s11_s10_after.txt` |
| **回退成员表**(精确隔离「集合内容」) | **2** | 39 测 35/**4 红** | `Logs/s11_s10_revert.txt` |

⚠️ **反证只回退成员表,不整段回退到 HEAD** —— HEAD 无 `MembersSnapshot()` ⇒ 整段回退会**编译失败**,
夹具一条不跑,红/绿**失去可比性**。此点已在证据件里明写。

**全量零回归**:EditMode `3334 total / 3288 passed / **0 failed** / 0 inconcl / 46 skipped`(退出码 0;
较 S11-8 后 **+5 新增 / −1 移除**,移除的恰是那条**反向断言缺陷**的旧测 ⇒ 净 +4)·
PlayMode `186/186/**0 failed**`(**逐位等于 M2 基线**)。

**记录面**:**新证据原件** `production/qa/evidence/fix-s11-10-case-system-story-003-b5-2026-10-15.md` ·
story-003 🟢 B5 块 + **Implementation Note #1 权威回填** + `**Status**` 行(39/39 · B-1+B5 闭合)·
`sprint-11.md` S11-10 转 ✅ · `sprint-status.yaml` S11-10 → `done` · `EPIC.md` 头部 + story-003 行。

**未清**:story-003 维持 `In Review` —— 补评 4 类中余 **② 快照半场**(写者未落,无载体)· **③ AC-37-18 恒真化**(A-4);
ADVISORY **A-5** 亦未清。
⚠️ **登记一处结构缺口(归用户裁)**:B5 是 **BLOCKING 级**判定却**不对应任何 `AC-37-xx`** ——
GDD 36 条 AC **无一条枚举处置集** ⇒ 该缺陷的守卫**只存在于测试件 + ADR 闭环纪律**,**不在 AC 面上**。
是否补一条显式 AC(或挂到支柱四不变量名下)**归用户**;**本轮不自行加 AC**。

**下一步候选**:S11-7 / S11-9 / S11-11(均未启动)。**S11-10 改动尚未提交** —— 待用户指示。

> ✅ **S11-10 已于 2026-10-15 提交** = `99a63dc`
> (`fix(sim): case-system story-003 B5 闭合 —— 处置集撤 InjuryOnset + 补 CareApplied + A-1 收口(S11-10)`,8 文件)。
> ⚠️ 提交前复核发现并就地处理:**代码注释原引的行号全部漂移** ——
> `case-system.md:738`(闭环纪律行)与 `:718`(订正框)**自引的号也都是错的**
> (`:666/:679/:254/:280` → 实测 `:671`/`:674`/`:686`/`:255`/`:283`;`§EC-37-1 :839/:567` →
> 实际在 `:570` 与 `:880`,且是 **EC-37-3** 不是 EC-37-1)。
> ⇒ 代码注释**改为节名引用**(节名稳定、行号不稳);**GDD 那两处的订正未做**(需 GDD 写入授权)——
> 已登记,待用户裁。

---

## ✅ 本轮 = S11-11 闭合(story-001 B-3 · 2026-10-15)

**用户指令**:**「提交 S11-10,然后做 S11-11」**;范围追问回答 = **「全文件去号(推荐)」**。

**对象**:`unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs` —— **纯测试件,零产品代码**。
**来源**:`review-case-system-story-001-retro-2026-10-15.md` §二 **B-3**(BLOCKING)。

### 缺陷本体

story-001 §AC 六条 bullet 里,**只有前两条**(`AC-37-01`/`AC-37-02`)带号且与 GDD 一致;
**后四条本就无编号**(kindgen 断言批 / 载荷形状断言 / 跨流全序复验 / 高水位纯净)。
测试头把「第 3–6 条」机械顺延编号为 `AC-37-03/04/05/06` —— 而 GDD 的这四个号是**另外四件事**,
且已被姊妹测试占用:

| 号 | GDD 权威含义 | 真主人文件 |
|---|---|---|
| `AC-37-03` | 已结案再结 ⇒ 幂等拒收 | `case_close_precondition_test.cs` |
| `AC-37-04` | 未勾「已处置」⇒ 结案被拒 | `case_close_precondition_test.cs` |
| `AC-37-05` | 判断记录/处置落病例流 | `case_open_and_judgment_test.cs` |
| `AC-37-06` | 存档迁移重放逐位一致 | `case_replay_persistence_test.cs` |

### ⚠️ 范围订正(retro 只点名测试头 `:5-8`,实测三层 25 处)

| 层 | 处数 | 形式 |
|---|---|---|
| ① 测试头 | 4 | `// AC-37-0X:…` |
| ② 体注释 | 14 | 段落标 + `/// <summary>AC-37-0X …` |
| ③ **方法名** | **10** | `test_ac3703_a1_…` —— **覆盖矩阵第一眼扫的正是这里** |

**只改头等于没改**(矩阵扫文件任意位置)⇒ 用户裁定取 **全部去号**。
**保留**:`AC-37-01`/`AC-37-02`(与 GDD 一致)· `AC-37-14`(GDD `:1110`,本文件是其载体半边)。

### 判据(本 AC = 「不再重复计数」,是**覆盖声明属性** ⇒ 判据为 grep 而非跑测)

| 号前缀 | 修复前 | 修复后 |
|---|---|---|
| `test_ac3703` | **7** | **2** |
| `test_ac3704` | **4** | **2** |
| `test_ac3705` | **5** | **3** |
| `test_ac3706` | **1** | **0** |

修复前 `test_ac3703` 下 **7 个方法分属两种互不相干的语义**(本文件 5 = kindgen 断言批;
close 文件 2 = 幂等拒收)⇒ retro 的「重复计数」**逐字成立**。反向:改回任一处号,对应行即时回升。

### 验证

`13/13 绿 · 退出码 0 · error CS = 0`(**测数 13 → 13,改名未增未减**);
全仓 grep 确认被改名方法**零外部引用**(UTF 按 `[Test]` 特性发现,`-testFilter` 走类全名);
`git diff --name-only` 仅本测试件 ⇒ `Sim` 引用集 / 门 A / b2 / ADR-025 §① / ADR-029 **全不受影响**。

### 记录面

**新证据原件** `production/qa/evidence/fix-s11-11-case-system-story-001-b3-2026-10-15.md` ·
story-001 🟢 B-3 块 + Test Evidence `Status` 行(余 3 条顺延)· `sprint-11.md` S11-11 转 ✅ + 订正框 ·
`sprint-status.yaml` S11-11 → `done` · `EPIC.md` 头部 + story-001 行。

### 未清 / 观察

- **story-001 维持 `In Review`** —— 余 **B-1**(AC-37-06 判据**算术上不可证伪**,须**退设计重述**)·
  **B-2**(kindgen A2 空跑)· **B-4**(kindgen 借绿)三条顺延 sprint-12(已带归属登记)。
- **观察 ①(非本件缺陷)**:修复后 `test_ac3706` 方法名命中 = 0 —— 因 AC-37-06 的真实主人
  `case_replay_persistence_test.cs` **不在方法名里编 AC 号** ⇒ 暴露**命名惯例覆盖不均**;
  修复前那 1 个命中恰是**错的那个**(高水位,与迁移重放无关)⇒ 本改动是**移除假阳性**,非制造空洞。
- **观察 ②(未动)**:story-001 §QA Test Cases 另有**第三套本地编号** `AC-1/AC-2/AC-3`
  (不在 GDD `AC-37-XX` 命名空间内 ⇒ 不构成一号两义),且与 §AC 的 6 条 bullet **不对齐**(3 vs 6)。
  归属归 story-001 §QA Test Cases 修订轮(与 B-1 同批更合适)。
- **另一处计划文档引用偏差(已就地订正)**:S11-11 行原写 GDD 权威编号在 `case-system.md:1036-1051`
  —— 实测该区间是**「信息边界」节**,AC 表在 **`:1075-1092`**。

**下一步候选**:**S11-7**(story-002 BLK-8,`LastJudgmentOf` 死参 —— 三件真缺陷中最后一件,实测仍为未修态)
/ **S11-9**(story-004 CB-2/TB-5,落地 `ChainEligible`/`ScriptedChain`)。**S11-11 改动尚未提交** —— 待用户指示。

> ✅ **S11-11 已于 2026-10-15 提交** = `c558d2f`
> (`test(case-system): story-001 B-3 闭合 —— 测试件 AC 号一号两义全文件去号(S11-11)`,7 文件)。

---

## ✅ 本轮 = S11-7 闭合(story-002 BLK-8 · 2026-10-15)

**用户指令**:**「提交 S11-11,然后做 S11-7」**。

**对象**:`Sim/CaseOpenDecider.cs`(`CaseStreamQuery.LastJudgmentOf`,**生产代码**)+
`Gameplay.Boot/CasebookVitalsSource.cs`(注释)+ 两处夹具。**三件真实现缺陷的最后一件**(B-1=S11-8 · B5=S11-10)。

### 缺陷性质的厘清(修复中才看清,低估过一次)

**不是「调用方传错」,是「接口本身失守」**。旧契约为「调用方负责传入该案的事件子集」,
但该契约**在生产路径上不可能被满足**:唯一调用方传 `EventStream.Events` = 全部混合流,
且**没有任何一层能合法切片** —— `SimEvent.Payload` 是 `PayloadRef`,解码归 `Sim.Codec`,
而 `Sim` 引用集恰 = {BCL, `Sim.Contracts`}(ADR-025 §① / b2 门)⇒ **物理上够不着**。
**与 S11-8 同一结构约束**。契约把责任推给了一个**做不到的调用方**。

### ⚠️ 计划处方订正(已报用户):`CompositionRoot` 无须改动

计划写「`CompositionRoot.cs:245-246` 改传该案子集」—— **基于错误前提**:
① 传整条流**本就是正确用法**;② 「该案子集」**构造不出来**(装配层要切片只能自己解码载荷
⇒ 把 37 的查询语义复制进装配层 = 更差的架构)。
⇒ **修复落在被调用方内部,调用点一字未改**;传全局流**从此成为正确行为**。
⚠️ **同族纪律(本 sprint 第二次)**:S11-8 的两处订正同型 ——
根因都是「**`Sim` 够不着 `Sim.Codec`**」这条引用集约束**在写处方时未被前置检查**。

### 改动

1. `LastJudgmentOf` 归属判据 = **流序关联**(跟踪该病人 `CaseOpened` 三元组 → 当下开案 →
   至 `CaseClosed` 前),**与既有正确范式 `OpenCaseOf` 同构**;
2. `CasebookVitalsSource.cs:211`/`:214` 注释对齐(原「该病人最近一条」**正是缺陷的自我描述**);
3. 测试:**改写 3 条旧夹具**(原夹具**不含 `CaseOpened`** = 「已切片」形状 ——
   正是 caseId 死参逃过测试面的直接原因;与 S11-10 那条**反向断言**的旧测同型)
   + **新增 3 条判别夹具**:**跨病人交错** / **同病人复诊**(两者 `Patient` 相同与否不同,
   **不可互相替代**)/ **端到端墨龄**(走 `CompositionRoot` 全生产装配 —— 唯一复现实害链的判据)。

### 证伪链(双信号一致)

| 状态 | CaseOpenAndJudgmentTest | InkAgeTest |
|---|---|---|
| **回退旧实现** | 退出码 **2** · 25 测 23/**2 红** | 退出码 **2** · 5 测 4/**1 红** |
| **修复后** | 退出码 **0** · **25/25** | 退出码 **0** · **5/5** |

三条红讯失败消息**逐字命中实害描述**,末条打印
「病人 A:5 − 3 = 2;旧实现取全流末条(病人 B 的 tick=4)⇒ 1 —— **墨龄取错病人**」。

### 零回归

EditMode `3337 total / 3291 passed / **0 failed** / 46 skipped`(较 S11-11 后 **+3 total/+3 passed**,
**恰为本轮新增的三条判别夹具**)· PlayMode `186/186/**0 failed**`(**逐位等于 M2 基线**)·
`error CS = 0` · **无 asmdef 改动**(门 A / b2 / ADR-025 §① / ADR-029 全成立)。

### ⚠️ 执行期踩坑(已如实登记进证据件 §三,供复跑者避雷)

反证脚本里把 `-testFilter` 的**循环变量**与**待回退源文件路径变量同名**(都叫 `F`)
⇒ `for` 循环**覆盖**该变量 ⇒ 「还原」步骤把备份写成了
**一个以测试类全名命名的垃圾文件**(`DaYiJingCheng.Tests.Boot.InkAgeTest`,仓根),
而 `diff -q` 与之比对**假报「已还原」** —— 生产文件实际留在**旧实现态**。
**发现方式 = 全量跑出 3 红后没有当成「新夹具不稳」,而是回头 grep 关键符号(`inCase`)零命中**
⇒ 定位到脚本 bug。垃圾文件已删,生产文件已恢复,**全量已重跑取证**。
⇒ 教训:还原后须**独立复核关键符号**,不能只信 `diff` 的退出码。

### 记录面

**新证据原件** `production/qa/evidence/fix-s11-7-case-system-story-002-blk8-2026-10-15.md` ·
story-002 🟢 BLK-8 块 + Test Evidence `Status` 行(23/23 → **25/25**)· `sprint-11.md` S11-7 转 ✅ + 订正框 ·
`sprint-status.yaml` S11-7 → `done` · `EPIC.md` 头部(**三件真缺陷全部闭合**)+ story-002 行。

### 未清

- **story-002 维持 `In Review`** —— 余 5 条**断言形态**(含 `test_ac3705_storeNewValue_judgmentIsLastEvent`
  仍**自造 `J(c)`** 不调生产函数 —— 与本件同一缺口的两半)+ BLK-9(药材短缺 fixture)·
  BLK-10(必交夹具类型错位)顺延 sprint-12。
- `CaseOpenDecider.Decide` 无生产调用者(ADV-2 孪生体)亦未动,归同类批次。

**下一步候选**:**S11-9**(story-004 CB-2/TB-5,落地 `ChainEligible`/`ScriptedChain` ——
sprint-11 补评修复批的**最后一项**)。**S11-7 改动尚未提交** —— 待用户指示。

---

## S11-9 完成(2026-10-15)—— story-004 CB-2/TB-5

**任务**:`ChainEligible` / `ScriptedChain` 全库 `.cs` **零命中** → **有实现 + 有测试**;
夹具覆盖「脚本链名额**不被随机病例消耗**」+「**一次性**」。W-1 已于 2026-10-15 会签结案(免责前提消失)。
**这是 sprint-11 补评修复批(S11-7…S11-11)的第五项 = 最后一项。**

### 交付(生产代码 + 测试,两文件)

**`unity/Assets/Sim/PatternDetector.cs`**(+277 / −24):

- **`ScriptedChain{ int DiseaseOrdinal; IReadOnlyList<int> MemberPatientIds; }`** —— **纯机制载体,
  不含任何具体 `patient_id`**:GDD F-37.1b 逐字「具名角色 → 数字 `patient_id` 的绑定**仍属内容值**……
  **本 GDD 只登记机制与判定域,不落数值**」⇒ P0 无该数据 ⇒ 由调用方 / ADR-014 烘焙**注入**。
- **`ChainEligible.IsEligible` / `TryChainOf`** —— 逐字兑现 `ChainEligible(D) := c.Patient ∈ ScriptedChain(D)`。
- **`Evaluate` 逐 D 二选一定义域收窄** —— 命中链的病种:`CandidateSeq(D)` 只留链成员
  (链外病例**既不计数也不占名额**);未命中链:维持 F-37.1 原算式。
  抽出 `ComputeCandidateSeq` / `SortBySaltedKey`(**单一定序源**);原 4 参重载转发 `null` ⇒ 现行为逐位不变。
- **`NextEmissions` + `PatternEmission`(发射决策层)** —— `ComputeCandidateSeq` → 达标 D → `SortBySaltedKey`
  → 逐个过 `IsFired` 门 → 切该 D 成员。**不触 `IEventSink` / 不 `Append` / 不发号 `Seq`。**

**`case_pattern_detection_test.cs`**:**23 → 30 测**(+7 夹具)。文件头补 AC-37-29 行。

### 两项用户裁定(2026-10-15,本件执行)

- **①「一次性」的载体 = 新增纯函数决策入口** —— 若让夹具自己写 `if (!IsFired(...))`,那**正是 TB-4
  所斥的「未写、未测的调用方契约」的复制** ⇒ 测试自造契约、判据不可证伪。
- **② 链内声明序不消费** —— GDD 同句「触发仍走 F-37.1 的**同一算式**……**不需要第二套公式**」,
  而该算式按全序键 `(Tick,Patient,Seq)` 定序 ⇒ 「有序」只作内容语义(叙事先后 甲→乙→丙),夹具钉死。
  ⚠️ **该 GDD 内部张力未就地加注**(改设计件未授权)。

### 证伪三步(双信号一致)

| 状态 | PatternDetectionTest(30 测) |
|---|---|
| **反证① 停用定义域收窄** | XML **`failed=3`** — `chainQuota` / `chainComplete` / `chainAndNaturalCoexist` |
| **反证② 停用「一次性」门** | XML **`failed=1`** — `firesOnce_neverRefires` |
| **修复后** | 退出码 **0** · **30/30** · `error CS = 0` |

**零回归**:EditMode **`3344 / 3298 / 0`**(较 S11-7 后 **+7**,恰为本轮新增;S11-7 后为 3337)·
PlayMode **`186 / 186 / 0`**(**逐位等于 M2 基线**)· **无 asmdef 改动**(门 A / b2 / ADR-025 §① / ADR-029 全成立)。

### ⚠️ 本轮两个「须如实登记」的点

**① 证伪轮抓出本件自身的一条假绿夹具(已修正)** —— `test_ac3729_chainAndNaturalCoexist_perDisease`
**首版**里 D=0 的**链外病例数 = 0** ⇒ 收窄与否结果相同 ⇒ **对「收窄」变异毫无判别力**
(首轮反证①只红 2 条才暴露)。已补入链外病例 20/21/22 修正,反证①恰红 3 条。
**它若不经证伪轮,会以「绿」留下却什么都没测** —— 与补评件 **TA-6** 元规则同型。

**② 一处语义厘清(非判定推翻)** —— TB-4/CB-1 记「发射语义无生产载体」,**观察成立但指向须收正**:
`Evaluate` 的**前缀属性语义本身没错**(AC-37-07/08 测的就是它;现有 `test_ac3709` 断言「第 4 例仍触发」**是对的**),
缺的是**发射决策层** ⇒ 准确表述是「**缺一层**」,不是「`Evaluate` 有问题」。
新夹具末段保留对照断言(`Evaluate` 仍返 `true`)把两层边界**钉死**。

### 记录面

**新证据原件** `production/qa/evidence/fix-s11-9-case-system-story-004-cb2-tb5-2026-10-15.md` ·
story-004(**`:4` Status · `:40` AC-37-29 转 ✅ · Test Evidence `23/23`→`30/30` + 就地订正已失效的
「BLOCKED-BY-W-1 不记绿」· 🟢 闭合块 · 🔓 块与 `:85` 同步**)· `sprint-11.md` S11-9 转 ✅ + 语义厘清订正框 ·
`sprint-status.yaml` S11-9 → `done` · `EPIC.md`(头部「补评修复批**五项全部清空**」+ story-004 行 + DoD 条)·
本文(active.md)。**GDD 未动**(用户裁定,两处待裁登记见下)。

### 未清

- **写者仍未落** —— `Append` + 发号 `Seq` 缺(承 2026-10-15 用户裁定「接受延后并正式登记」,
  `WriterExistenceGate.cs:88` 具名豁免)。**本件不得被引为「写者已落」**。
- **CB-3 未触及** —— `PatternDetectionResult.MemberSet` 仍扁平、`Dictionary` 迭代序仍非契约 ⇒ 归 sprint-12。
  `NextEmissions` 自带逐 D 归属是**本件所需**,不是对 CB-3 的修复。
- **story-004 余 6 条顺延 sprint-12**:TB-1 定序判别力 · TB-2 成员冻结 · TB-3 真空夹具 ·
  CB-1 实现侧 · CB-3 · TA-6 元规则。**四件 story 维持 `In Review`。**
- **GDD 两处待裁(本件未动)**:① F-37.1b「有序」张力加注;② `case-system.md:738`/`:718` 行号漂移(S11-10 登记)。
- **脚本链内容数据仍不存在** —— `patient_id` 绑定归内容轮 + ADR-014 烘焙(GDD 明文的归属划分,
  **不是缺口**;但须防「`ScriptedChain` 已落 = 三案链可跑」的误读)。
- **GDD F-37.1b 注块的「先触发歧义」** —— 本件的逐 D 二选一**已消除其一半**(有链的 D 不再走自然检测,
  与 GDD 给出的裁法一致);注块仍写「归 37 实现轮」,**是否更新待裁**(改 GDD 未授权)。

### 下一步候选

**S11-4 已完成(见下)**;**S11-9 已提交(`1df7b83`)**;**补评修复批五项(S11-7…S11-11)全部清空**。余:
**S11-5**(nice-to-have, 1.5 天,53 medical-consequences 起首 story)·
**S11-6**(nice,已裁定顺延吸收)。

---

## S11-4 —— prescription DoD 残留三项(✅ 完成 2026-10-15)

**证据原件**:`production/qa/evidence/fix-s11-4-prescription-dod-residuals-2026-10-15.md`

### 任务书前提被推翻(如实登记)

S11-4 标 3.5–4 天、措辞「**DoD 残留**」暗示需实现。**勘察证明 DC-2 / DC-6 的实现侧早在
2026-10-07(`2a7764f`)已闭环**,残留**仅**在登记散文。四处**同文件内自相矛盾**可观测:

| 文件 | 位置 A(修复前) | 位置 B |
|---|---|---|
| `EPIC.md` | `:105`「DC-6 `NOISE_BAND_9` 无主」 | `:128`「判据可跑」 |
| `PrescriptionActionIdRegistry.cs` | `:4`「BL-2 …… 未立」 | `:168`「已立 ⇒ 判据 RUN」 |
| `dose_potency_test.cs` | `:9`「9 侧不存在」 | `entities.yaml:1484` 有登记 |
| GDD `prescription-and-medication.md` | `:763`「常量缺席」 | `:783`「9 侧已立此常量」 |

**实际工作量远低于估算。**

### 三项判决

| 项 | 实现侧 | 判决 |
|---|---|---|
| **DC-2** `action_id` master | ✅ `disease_action_axis.json`(`2a7764f`)· `TR-disease-023` | **登记回刷后闭合** |
| **DC-6** `NOISE_BAND_9` | ✅ `NOISE_BAND_POTENCY_9`(`entities.yaml:1484`)+ fail-loud 门 | **登记回刷后闭合**(带真残余) |
| **AC-11-22 半边②**(7a 白名单) | ⚠️ 原判「7a 白名单**表**未落地」= **载体识别错位** | **落判据后闭合** |

### 两项用户裁定(2026-10-15)

① **回刷范围 = 全批含 GDD** —— 批准改 GDD `:760-766` 两条注(①DC-2 已登记 · ②DC-6 常量已立 ⇒ RUN,
**保留「当前数据集零求值」如实登记**)+ 4 处散文;③ BL-7 仍开原样保留。
② **AC-11-22 半边② = 落判据(白名单 = codec 分派闭集)** —— AC 文本一字未动。

### AC-11-22 半边② 是「载体识别」不是「AC 收窄」

AC 原文:「**7a 的序列化器**两个 Kind 均在**白名单**内」。而 7a **从未有过**独立的「白名单**表**」
(`ADR-010` / `persistence-service.md` 全文无)—— 序列化器的白名单在实现里**天然是**
`EventKind` 35 支闭集 ∩ `PayloadCodec.DecodeBoxed` / `PayloadEncoder.EncodeBoxed` 分派表
(两者 `default` 逐字抛「不在 35 支闭集内」)。
**真缺口在别处**:现有 `test_drugEvent_codecHasBothBranchesForThisKind` **只覆盖 `DrugTreatmentApplied` 一支**,
AC 要的是**两个** Kind ⇒ 由新增两测补上。
⇒ **不越** `EPIC.md:109` 的「本 epic 不自行改 AC 文本」边界。**该读法待 producer/TD 复议。**

### 证伪三步(双信号一致)

| 状态 | PrescriptionMedication(176 测) |
|---|---|
| **基线** | 退出码 **0** · XML `passed=176 failed=0`(两条新测确认已执行) |
| **反证**(停用 `PayloadCodec.cs:59` 的 `EmergencyTreatmentApplied` case) | 退出码 **2** · XML **`failed=2`** —— 恰红两条新测,消息**精确点名**被移除的 Kind |
| **还原** | `grep` 残留 0 · case 完好 1 · **`git status` 中 `PayloadCodec.cs` 空**(三重复核) |
| **全量 EditMode** | 退出码 **0** · **`3346 / 3300 / 0`**(较 S11-9 后 **+2 passed**,恰本轮新增)· `error CS` = 0 |

反证失败消息:
```
FAILED: test_ac1122_serializationWhitelist_containsBothNamedKinds
   msg: System.IO.InvalidDataException : 未知 EventKind=6 —— 不在 35 支闭集内
FAILED: test_ac1122_serializationWhitelist_coversEntireEventKindClosedSet
   msg: 以下 Kind 落在 codec 分派表 default —— 白名单闭集不完整:EmergencyTreatmentApplied
```
⇒ **判别力确认,非假绿夹具**(承 S11-9 的假绿夹具教训)。

### ⚠️ 本件未动 7a —— S11-4 的附加条件不触发

S11-4 AC 逐字:「**若动 7a 须带 case↔7a 折叠语义集成夹具(TD 风险 1 缓解)**」。
判据落点在 `Sim.Codec`(codec 分派表),**不触及 7a 存档层**(`SaveCodec` / 折叠谓词 / 高水位重构)
⇒ 该条件**不触发**,本件**不带**该集成夹具。**不得**把本件读成「TD 风险 1 已缓解」(sprint §Risks 该行仍在)。

### 三处真残余(未消解,禁借绿)

1. **DC-6 对当前数据集零求值** —— `salicylic_acid.dose_range = null` ⇒ 无相邻档可判。
   覆盖率记账已逐字声明「零求值」。**判据可执行但无数据可判**,不得读成「11 的表已合规」。
2. **BL-7** —— 21a `drug_potency` 域未声明;GDD `:760` ③ 原样保留。
3. **AC-11-22 半边甲(文本背离)** —— `PayloadCodec.History.cs:332` 在 `Sim.Codec` 构造载荷,
   与 AC 原文「构造点仅存在于 11 的程序集」背离;`EPIC.md:109` 明文**归 producer/TD**。
   **本件维持其显式记账现状**(门的三类分离登记不动)。

### 记录面

**新证据原件**(上述)· `drug_event_test.cs`(NOT-RUN 第 4 条改写 + 新增 2 测)·
`PrescriptionActionIdRegistry.cs:4` · `dose_potency_test.cs:9/:164/:169/:194` ·
GDD `:760-766` · `EPIC.md`(`:99-101` / `:105` / `:116` / `:131`)· story-001(`:26` / `:40` / `:41`)·
`sprint-11.md` S11-4 转 ✅ + 订正框 · `sprint-status.yaml` S11-4 → `done` · 本文。

### 未清

- **EPIC 仍 `5/5 Complete` 但 DoD 未达** —— 本件不改变该状态(DoD 第 2 条仍受 story-002/003/004/005 制约)。
- **story-001 的 AC 复选框未打勾** —— 本件只回刷判词(`NOT-RUN` → `RUN`),AC 关闭归 `/story-done`。
- **本件对半边②的读法待 producer/TD 复议** —— 若另裁「白名单 = 独立数据表」,判据须重做。
- **S11-4 改动尚未提交** —— 待用户指示。

### 下一步候选

**S11-5**(nice-to-have, 1.5 天,53 medical-consequences 起首 story)· **S11-6**(nice,已裁定顺延吸收)。

---

# S11-5 —— 53 medical-consequences Story 001(入向闭集与跨流聚合)

**状态**:✅ **实现完成**(2026-10-15)· story-001 `Ready` → **`In Review`**
**证据原件**:`production/qa/evidence/s11-5-medcons-story-001-aggregation-2026-10-15.md`(七节)

### 前提被推翻(本 sprint 第二次)

任务书写「**story 立件** + 判据面成型」—— 而 **4 个 story 件早在 2026-09-28 立好**(全 `Ready`),
缺席的是**实现**(`WriterExistenceGate.cs:117` 具名「53 未实现」)。⇒ 本件是**真活**。
⚠️ **过程面**:本 sprint should-have(S11-4)与 nice-to-have(S11-5)**两项前提均过时**,
描述均写于 10-14,产物形成于 10-03/10-07/09-28 ⇒ **sprint 计划时未回扫 epic 现状**(归 sprint-11 回顾)。

### 交付(2 个新文件,两个新目录均 53 首件)

- `unity/Assets/Sim/Consequences/ConsequenceInputAggregator.cs` —— `IDiseaseProjection`(只读边界)·
  `DiseaseProjectionSnapshot`(占位)· **`Inputs53`**(恰五路)· `Aggregate`(全序排序 → 逐结案病例投影)
- `unity/Assets/Tests/EditMode/MedConsequences/medcons_aggregation_test.cs` —— **20 条测**

**核心设计**:**零载荷解码**(门 A 够不着 `Sim.Codec` ⇒ 只操作 `SimEvent` 头部)·
**置换不变由结构保证**(复制 → 全排序 → 后处理;比较体复用 `EventOrder.Compare` 禁减法)·
**P = 最后一笔**(有序扫描 + 覆盖式) · **闭集外零渗入**。

### 判据(双信号口径)

| 步骤 | 退出码 | XML | 判决 |
|---|---|---|---|
| filtered EditMode | **0** | `20/20 passed` | ✅ 20 条全执行 |
| **反证 ①**(停用 `ordered.Sort`) | **2** | `18 passed / **2 failed**` | ✅ 恰红**两条排序专向探针** |
| 还原 ① | — | 残留 0 · 调用完好 · `diff` identical · md5 同 | ✅ |
| **反证 ②**(注入第六路 `SixthLaneProbe`) | **2** | `19 passed / **1 failed**` | ✅ 恰红一条并**逐字点名** |
| 还原 ② | — | 残留 0 · `diff` identical · md5 同 | ✅ |
| 全量 EditMode | **0** | `3366 / 3320 / 0 / 46` | ✅ **零回归**(基线 `3346/3300/0`,**+20 恰本轮新增**) |

`error CS` = 0 · **无 asmdef 改动**(`Sim` 引用集仍恰 = `["Sim.Contracts"]`)。

### 五处偏离批准草案(均以卡面 / AC 为准)

| # | 草案 | 落地 | 依据 |
|---|---|---|---|
| 1 | `PerCaseInputs` | **`Inputs53`** | 卡面 **AC-1 逐字**点名;§Implementation Notes 1 是**异名** ⇒「AC 是判据」 |
| 2 | `MedicalConsequences/consequence_input_aggregation_test.cs` | `**MedConsequences**/medcons_aggregation_test.cs` | 卡面 §Test Evidence **点名**该路径 |
| 3 | 第五路 `IDiseaseProjection` | `DiseaseProjectionSnapshot` | 接口引用逐组重复装同一对象**无语义** |
| 4 | (未定) | `Assembly.GetReferencedAssemblies()` | 禁文件系统依赖;查**实际**引用集更严 |
| 5 | (未定) | 扫面 = `DaYiJingCheng.Sim.Consequences` 命名空间 | **53 无独立程序集**;**刻意不扫全 `Sim`**(会把 9/25/37 纳入 53 判据 = 越权) |

### 两处张力**未**裁定(承批准口径,归 53 的 GDD 轮)

① **处置 Kind 集** —— 卡面 Note 4 提 10/11 两支,而 GDD 规则五 `:165` **只列** `CareApplied`
⇒ 取 `careKinds` **显式参数**,测试**只**钉「参数被尊重」(窄/宽/null ⇒ 三结果)。
② **处置窗口下界** —— 取 `[CaseOpened, Close]` 须加第六路,与 AC-1「恰五路」**冲突**
⇒ 取 `Tick ≤ 结案 tick`(无下界),测试钉**当下口径**并注明待裁。

### 记录面

本件(新建 7 节)· `sprint-11.md` S11-5 行转 ✅ + 订正框 · `sprint-status.yaml` S11-5 → `done` ·
story-001 件(`Ready` → `In Review` + §Test Evidence 实现记录 + 异名/张力就地登记) · 本文。

### 未清

- **story-001 的 6 条 AC 复选框未勾** —— 判据全绿但**关闭归 `/story-done`**。
- **`WriterExistenceGate.cs:117` 未动** —— 本件**不产出** `ConsequenceResolved`(归 story-003)。
- **story 件内的异名**(`Inputs53` / `PerCaseInputs`)—— 只登记不复名;
  若 GDD 轮要统一,须**同时改卡面 AC 与实现**。
- **story-002 / 003 未开工** —— P/C 的**内容**、Judge 求值、后果产出均未实现。
- **S11-5 改动尚未提交** —— 待用户指示(S11-4 主体已于 `8401e6a` 提交)。

### 下一步候选

**S11-6**(nice,已裁定顺延吸收)· 提交 S11-5 · sprint-11 收尾/回顾。

---

# S11-4 二轮回刷(2026-10-15)

**触发**:S11-4(`8401e6a`)提交后复核发现 —— **回刷只做到「段」没做到「表 / 件」**:
`EPIC.md:102`/`:138` 声明「7a 白名单半边已落判据」,而同文件 `:88`/`:112` 与 `story-003:110`
**仍写它未闭**。⇒ **同文件内自相矛盾**(与 S11-4 §2.3 诊断的病灶**同型** —— 修掉四处,又留三处)。

### 改动(3 处,纯登记回刷 · **零裁定 · 零代码**)

| # | 位置 | 改后 |
|---|---|---|
| 1 | `EPIC.md:88` | `~~7a 白名单~~ ✅ **已闭 2026-10-15**(S11-4 落判据)` |
| 2 | `EPIC.md:112` | `~~AC-11-22 的 7a 白名单本体~~ ✅ **已闭**` + `~~· BLOCKED-BY-7a~~` |
| 3 | `story-003:110` | `~~7a 白名单本体~~ ✅ **已闭**` + 就地声明「**不动**同行『AC 文本收窄』(半边甲,归 producer/TD)」 |

第 3 处**顺带消除** story-003 件内自相矛盾(`:36` 的 `[x] AC-11-22` vs `:110` 列为未闭)。

### 两处如实登记

- **第四处口径分歧(域外,未动)**:`qa-plan-sprint-11-2026-10-14.md:62` 把该 AC 读作
  「**登记处方载荷字段的 7a 白名单条目**」(= 真有一张表),与 S11-4 的**载体识别**
  (= `EventKind` 闭集 ∩ codec 分派表,**无独立表**)**冲突** —— 正是 S11-4 §5.4 预告的
  「另一种读法」实例。写于计划期,属 producer/TD 裁定面 ⇒ 未动,留作复议输入。
- **半边甲性质纠正**:`story-003:103-105`(S11-4 §5.3 **未引到**的行)显示实现侧**早已处理** ——
  门把 `Sim.Codec/PayloadCodec` 从 `ProductionCtorSites` **移出**、新立 `DecoderCtorSites`
  分离登记 + `RunAll` 摘要报警。⇒ 准确性质 = **「已显式记账、待裁收窄」**,**不是**「未处理的背离」;
  真剩余仍只有 **AC 文本收窄的裁定**(归 producer/TD)。

### 影响面:**零**

零代码改动 ⇒ **不跑 Unity、无回归面**(S11-4 §四的证伪三步仍为 `8401e6a` 时的有效记录)·
不改任何判据 / 数值 / AC 文本 · epic DoD 状态不变。

### 过程面观察(同一失效模式**第二次现身**)

**只改声明段而不改表 / 件 ⇒ 生产新的同文件矛盾**(S11-4 修掉四处,又留下三处)。
⇒ 建议收口判据:回刷后对**同一关键词做全仓 grep**,逐条判「动 / 不动」并落纸
(本次证据原件 §8.2 那张表即该做法)。

### 记录面

S11-4 证据原件追加「§八 二轮回刷」(8.1 改动 · 8.2 扫描面 · 8.3 第四处分歧 · 8.4 半边甲纠正 ·
8.5 零影响面 · 8.6 过程面)· 本文。

### 下一步候选

~~**S11-4 尾巴 B**(跑 producer/TD 裁半边甲)~~ ✅ **已完成(2026-10-15 本轮,见下)** ·
**S11-6** · **提交本轮落盘** · sprint-11 收尾 / 回顾。

---

## S11-4 尾巴 B —— 半边甲裁定 + 全批落盘(2026-10-15 · 本轮)

### 裁定结论

`technical-director` 与 `producer` **并行**复核(均只裁不写),**结论合流**:

- **TD 裁 (甲) 收窄 AC 文本** —— **改文本,不改代码**。
  核心:载荷 `DrugTreatmentAppliedPayload` 是 `readonly struct` + **唯一 ctor** ⇒ 任何解码器
  **必然**调用它 ⇒ AC 原文「构造点仅存在于 11」对**任何合法实现恒假**
  ⇒ 属**「判据对合法输入类误判」**(与 BL-7 同族),**不是**实现缺陷。
- **producer 复核**:① 排期纠正 —— 半边甲实为 **≈0.25 天登记活**(**不是** S11-4 那种 3.5–4 天量级);
  ② `Manifest Version` **不触发**(实测 `control-manifest.md` 对「构造点」零命中 + `TR-018` 的 `adr: null`)
  ⇒ **207 份 story 不受影响**;③ **story 状态不回退**(裁甲 ⇒ 代码没错文本错 ⇒ 维持 `Complete`,
  `story-003:36` 的 `[x]` **不算借绿**);④ 制定计划件处置惯例 = **就地加注、不抹除历史读数**。

### 落盘(全批 · 用户裁定)

**权威文本 4 件**:GDD `AC-11-22` 表行 + 规则三 BL-4 注 · `tr-registry.yaml:TR-prescription-018` ·
`traceability-index.md` —— 均「**原文保留 + 就地 ⚠️ 订正 + 日期 + 新口径**」。
**门注 3 处**(零逻辑改动):文件头 / `DecoderCtorSites` 注(含**陈旧行号订正** `:295` ⇒ **`:332`**)/
`RunAll` §⑤ —— 由「判据-文本背离」改称「**解码重建豁免点**」。
**Epic/story 卡**:`EPIC.md`(状态口径块 + 故事表 + TR 行 + 未闭项表 + 增量段)· `story-003` 四处。
**producer 三项补充**:`EPIC.md:6` 状态口径(同 `emergency-procedures/EPIC.md` 先例,消同文件自相矛盾)·
`qa-plan-sprint-11:62` 就地加注(原文保留)· `sprint-11.md` 台账(⑦ 行 + 新增「尾巴 B 结项框」)。
**新建**:`production/qa/evidence/td-ruling-ac11-22-narrowing-2026-10-15.md`(裁定原件,BLOCKING 级)。
**追加加注**:`fix-s11-4-…md` §九(§5.3 悬置结项 —— **原文不改**,承义务 3)。

### 口径三改

① 「反射 / 引用集断言」⇒「**IL / metadata 扫描**」(反射看不见 `newobj`,引用集只到程序集粒度 ——
**原理上做不到**「构造点集合 ⊆ 11」)· ② 「构造点」⇒「**写入路径**构造点」·
③ 解码豁免**具名**为 `Sim.Codec.PayloadCodec`。

### 两条过程面沉淀(用户裁定「两条都沉淀」· 均**不做 hook**)

- `.claude/skills/sprint-plan/SKILL.md` **§Phase 1 第 5 步**(硬前置):纳入 sprint 的每个 epic
  须**重读** `EPIC.md` §Epic Status + 目标 story 的 `Status:` 行,**不得沿用旧快照**。
- `.claude/docs/review-workflow.md` **义务 5「回刷收口」**:改完一处口径 ⇒ 对**同一关键词**全仓 grep,
  逐条判「动 / 不动」并落纸(四分类:本次改 / 与新口径一致 / 域外不同事项 / 历史读数)。
  ⚠️ 同时**纠正 S11-4 §8.3** 的过度保守 —— 「**加注 ≠ 改判据**」,正确次序 = **先裁定、后加注**。

### 验证(义务 5 自我执行)

全仓 grep「收窄裁定未落盘」/「归 producer / TD」/「半边甲」三类命中**逐条分类**,
**结论:零需改** —— 残留全在**删除线引文 / 历史 session 态 / 评审原件**(义务 3 不可追改)。
`tr-registry.yaml` **YAML 语法校验通过**(顶层键 502)。`TR-018` 改后实测 `status: partial`(理由已更新)+ `adr: null`。

### 影响面

**零代码改动** ⇒ **不跑 Unity**;`Sim/` 与 `Sim.Codec/` **一字未动**;
**epic DoD 状态不变**(仍未达);**无 AC 新转 RUN**;**无 story 状态变更**。

### 未闭(明确不在本轮裁定面)

`TR-prescription-018` 维持 `partial` —— 其**第二条**理由(「作者侧『未注册 Kind 构建期失败』的
全局防线仍缺」)**疑已陈旧但本轮未核**:kindgen 通道**已实测存在**(`tools/kindgen/kindgen.py` +
`Sim/StreamRouting.g.cs`),是否**接线**到构建期拒绝**未核** ⇒ 登记**待核**(既不借绿也不借红),归下一次 `/architecture-review`。

---

## Sprint 11 收尾(2026-10-15 · DoD 终局 + 台账回刷)

### 状态

**sprint-11 = `APPROVED WITH CONDITIONS`(条件性通过)** —— QA 签核件
`production/qa/evidence/qa-signoff-sprint-11-2026-10-15.md`(qa-lead 产)。
**DoD 15 条:11 ✅ · 4 ⚠️ 条件(2 / 5 / 8 / 11)· 0 ❌**。

⚠️ 签核件 `:182` 自写「✅ **10**」系**计数笔误** —— 其**表内**实为 **11 条 ✅ + 4 条 ⚠️ = 15**
(✅ 逐条 = 1,3,4,6,7,9,10,12,13,14,15)。打勾按**表内**办,已在 `sprint-11.md` 注明。

**4 条条件全部是「需真人 / 需外部件」,无一为开发方未尽** —— 这正是判 `WITH CONDITIONS`
而非 `NOT APPROVED` 的理由(签核件 §六)。

### 本轮落盘(未提交)

| 文件 | 内容 |
|---|---|
| `production/sprints/sprint-11.md` | ① `:36/37/38` 补三行 `✅ DONE 2026-10-15`;② **DoD 15 条打勾(11 ✅ / 4 ⚠️)** + DoD 终局框;③ **S11-1 三处登记欠账补登框(承 C7,含全树取证命令)** |
| `production/sprint-status.yaml` | S11-6 顺延登记(`status: backlog` + `blocker` 写明用户裁定「必须项优先」· 本文 `:27`)+ owner 补齐 |
| `production/qa/smoke-2026-10-15.md`(新) | Verdict = **PASS WITH WARNINGS(仅自动面)**;人工 Batch 1–3 **全 `NOT RUN`**;明文**不得**引作 hand-off 凭据 |
| `production/qa/evidence/qa-signoff-sprint-11-2026-10-15.md`(新) | qa-lead 签核件;问题 F1–F9 + 条件 C1–C9 |

### M2 链验证(后台跑,已完成)

| 平台 | 退出码 | XML | 基线对照 |
|---|---|---|---|
| EditMode | **0** | `total=3366 passed=3320 failed=0 inconclusive=0 skipped=46` | S11-5 后 —— **逐位一致** |
| PlayMode | **0** | `total=186 passed=186 failed=0` | M2 基线 —— **逐位一致** |
| `error CS` | — | **0 / 0** | — |

⇒ **M2 链零回退**,不触发「重开 M2」。

### C7 补登的取证(可复核 · 承 review-workflow 义务 4)

```bash
grep -rln "examine_revealed_wiring\|ExamineRevealedWiring" unity/Assets/Tests/ tests/  # 零命中(退出码 1)
ls unity/Assets/Tests/PlayMode/Boot/                                                    # 目录不存在
sed -n '17p' production/qa/qa-plan-sprint-11-2026-10-14.md                              # 确点名该路径
```

S11-1 实际走**换轨载体**:EditMode `Boot/examine_driver_test.cs`(13 测)+ PlayMode
`SkeuomorphicUI/e2e_full_loop_test.cs`(`:428` 断言 `RevealedRows.Count > 0`)。
⚠️ 补登**不等于**判定改写 —— DoD 8 / 11 的 ⚠️ 本轮**不自转绿**,归下一轮复核。

### 未闭 / 结转 sprint-12

- **S11-6 顺延** —— 需真人走查 + 主创 / 医学从业 / 音频 lead **三签**,agent 不可代劳;
  前置 = 修 `unity/Assets/Editor.Tools.Spike/Story009WalkthroughDriver.cs:406` 的
  `r.passed = r.checks.All(c => c.StartsWith("✅"))` 汇总 bug(**实测仍在**)。
- **case-system 001–004** 断言形态批(≈15 条)整批顺延;**四件维持 `In Review`,禁借绿**。
- `prescription` epic:`AC-11-12/13/21`(人工走查)· `AC-11-15`(三格矩阵,ADR-012 未激活)·
  `AC-11-16`(BLOCKED-BY-45)。
- `TR-prescription-018` 维持 `partial` —— 理由②(kindgen 是否**接线**)登记**待核**。

### 未提交累积

**本地 13 个提交未 push**(`d7d2b4a` + 之前 12 个)+ 上述 4 文件未提交。

---

## Sprint 11 范围变更 · case-system 断言形态批就地闭合(2026-10-15 · 进行中)

### 用户裁定(本轮)

**「在 sprint-11 完成这些,不带入下个 sprint」** ⇒ **推翻**「顺延 sprint-12」。
四项子裁定:① **解冻两个写者**;② **授权收窄 GDD**(AC-37-06);③ **裁「病人死亡不进事件流」**+改 AC-37-18 判据;
④ **授权改 GDD 两处**(F-37.1b 加注 + 行号漂移订正)。

### 分母钉死 = 20 条(既有台账三个数字互不一致:DoD 17 / 风险表 ≈15 / story-004 自写 6)

**A 组 14 条**(可在 sprint-11 内闭合)·
**B 组 6 条**(原卡裁定,现全解冻)。
完整清单落 `production/sprints/sprint-11.md` 的「2026-10-15 范围变更」段。

### 已完成

| 项 | 落点 | 状态 |
|---|---|---|
| GDD F-37.1b「有序」加注 | `design/gdd/case-system.md` | ✅ |
| GDD `:738` 行号 → 节名引用 | 同上 | ✅ |
| GDD `:718` 双错订正(节号 EC-37-1→**EC-37-3** + 行号) | 同上 | ✅ |
| GDD AC-37-06 收窄(现 `:1116`) | 同上 | ✅ |
| `sprint-11.md` 范围变更段 | `production/sprints/sprint-11.md` | ✅ |
| **`Sim/CaseCloseWriter.cs`**(181 行)+ 测试(273 行) | 新 | ✅ 落盘 |
| **`Sim/PatternRecognitionWriter.cs`**(199 行)+ 测试(296 行) | 新 | ✅ 落盘 |
| **撤两条豁免** | `Editor.Tools.Gates/WriterExistenceGate.cs:87-88` | ✅ |

**验收**(我做的,非 agent 自述):`new PatientId(int)` 存在且 public(`PatientId.cs:15`)·
`CaseCloseQuery.HasTreatmentInWindow/IsClosed/IsOpen` 三个都在(`CaseCloseDecider.cs:150/192/229`)·
两文件**零代码违例**(`UnityEngine`/`new PayloadRef(` 命中全在注释)。

**载荷语义已复核定论**(以落地实现为权威,非 ADR 措辞推断):
- **`anchor_case` = `MemberSet[末位]`**(`PatternDetector.cs:271-277`:`members` 按 `candidates` 全序取前 threshold 例、**未排序** ⇒ 末位 = 跨阈值那一例 = ADR-008 §三「触发识别的那一例」)
- **`member_set` 的病种分量 = `{D}`**(registry `entities.yaml:1955`「冻结三元组 = anchor_case + disease_set + 版本」的三个字段;F-37.1 逐 D 一条)

### EditMode 首跑(写者 + 撤表后)

- **编译零错误** · `total=3387 passed=3334 failed=7` · 退出码 2
- **7 条红全部是测试夹具违反 `EventStream.Append` 的 BCD-码-3 不变量**(「流必须按 Tick 非降」),
  **非实现缺陷** —— 异常在铺流阶段抛出,未跑到断言:
  `CaseCloseWriterTest` 1 条(处置 tick 3 回退于已入流 tick 5)·
  `PatternRecognitionWriterTest` 6 条(事件 tick 0 回退于已入流 tick 1)
- **已 resume 两个 agent 各自修夹具**(明令:不得为变绿删场景;TB-1 的「输入序 ≠ 期望序」对策必须保住)

### 在跑 / 待办

- 🚧 agent:修 CaseCloseWriter 夹具 · 修 PatternRecognition 夹具 · kindgen B-2/B-4 · **002 六条断言形态**
- ⬜ 批 2 剩余:**004 四条**(TB-1/TB-2/TB-3 + CB-3 + TA-6)与 **003 A-5**
- ⬜ 批 4:登记回刷(四件 story 状态)+ PlayMode 全量 + 提交

### 关键判断(须记住)

**写者的「生产触发点接线」做不了,也不该做** —— 三条硬理由:① 结案是玩家显式动作(「系统永不自动结案」),
「结案页宿主」意图源**尚不存在**;② 两个解码 delegate 的真实现要落 `Sim.Codec`(跨装配面);
③ `EventStream` **无订阅机制**,而 `PatternRecognized` 要求「CaseClosed 入流后同 tick 触发」。
⇒ **但写者类 + 测试足以闭合 B1/B2** —— 先例 `CaseOpenWriter` 自己也没接线(其文件头明写「生产触发点尚未接线」)
而已被本门接受为「有写者」。生产触发点归装配轮。

**CB-3 已被部分处理** —— `PatternDetector.cs:271` 注释明写「不从 `PatternDetectionResult.MemberSet` 取 ——
该结构扁平,见 CB-3」,即 `NextEmissions` 就是为修它而立的新路径(S11-9)。旧 `Detect` 的扁平 `MemberSet` 是残留半边。

**行号漂移是活的** —— 我插入 GDD 三处注后,AC-37-06 从 `:1092` 漂到 `:1116`(24 行),
当场让我 grep 拿到错内容。已在新注里写明「上列行号是**快照不是指针**」。
