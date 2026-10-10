# Session State — 2026-10-10(**当前阶段 = Pre-Production · Sprint 04 Phase 1 ✅ · Phase 2 ✅ · Phase 3 ◐ 扩行「集成接线+验证」(阶段 0 ✅ · 阶段 1 ✅ · 阶段 1 尾 ✅(已提交 `1621efd`/`6a01d73`)· **阶段 2 ◐ A–E 五批 + 登记债码-1…5/GDD 八项收口 已提交推送 `f5879c3`**(提交前全量 3191/3144/0 红/1 inc/46 跳,两份原件入册;剩批次 F 收口轮)· 阶段 3 形态件待开)· M2 形态件 4/4 齐(story-021…024)**)

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
- ✅ **批次 F 七项全闭(2026-10-10 · 工作树未提交)**:F-1 ✅ · F-2 ✅ · F-3 ✅ · F-4 ✅ · F-5 ✅ · F-6 ✅ · F-7 ✅。
  **下一步 = 批次 F 提交**(待用户指令)。

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
Epic: sprint-04 Phase 3(M2 接线轮)
Feature: 阶段 2 链补全轮
Task: A–E 五批 + 登记债收口已提交 f5879c3 → 下一步批次 F 收口轮
<!-- /STATUS -->

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
