# Playtest Report — M2 阶段 2 收口轮人工 playtest

> **Sprint**:sprint-04 · 批次 F-5(M2 阶段 2 收口)
> ⚠️ 本报告**不替代任何 Logic/Integration 门** —— 那些条目由自动化测试承载,
> playtest 只覆盖「人工可感知面」(承 sprint-04 收口轮任务定义)。
> ⚠️ 模板权威 = `.claude/skills/playtest-report/SKILL.md`(9 节);落盘路径 =
> `production/playtests/`(承 M2 Exit Criteria;原 `production/qa/playtests/` 为措辞错)。

## 1. Session Info

- **Date**:2026-10-10
- **Build**:`f5879c3` + 工作树(批次 F 未提交改动;Unity Editor `6000.3.24f1`)
- **Duration**:约 10 分钟(编辑器 Play 模式人工操作 + batch 诊断并行)
- **Tester**:dr_guyang(用户)
- **Platform**:PC(Linux 桌面机,Unity Editor)
- **Input Method**:KB+M(键盘 WASD/方向键)
- **Session Type**:Targeted test(M2 阶段 2 收口 —— 可玩面首次人工走查)

## 2. Test Focus

当前**实际可玩面**(阶段 2 现状,非核心循环):

1. 启动序四步(CompositionRoot.Assemble → world 场景 Additive 加载 → 玩家生成 → 落地)
2. Console 健康度(红错/黄警告)
3. 玩家移动输入链(WASD → x/z 位移)
4. y 轴稳定性(静置落地 + 移动中)

**不在本次可玩面内**(如实登记,不借绿):病人出现(`PatientSpawner.SpawnNext` 零生产
调用点)· 诊断/治疗 UI(阶段 3 形态件未开)· 相机跟随(`ICameraRig` 零生产调用点)·
核心循环(病人 → 诊断 → 治疗)**可达性 = 0%**。

## 3. First Impressions(前 5 分钟)

- **Understood the goal?**:Partial —— 无 UI/HUD/目标提示,玩家只能观察到「能走动的灰色地面」
- **Understood the controls?**:Yes —— WASD/方向键响应正常(测试者口述:「按 ws 时 z 值会变,
  按 ad 时 x 值会变」)
- **Emotional response**:未口述(灰盒阶段,无反馈系统可供情绪采样)
- **Notes**:
  - 测试者口述:「Console 这里没有红错和黄警告」—— 启动序 + world 加载零报错
  - 测试者口述:「y 在不受控制的一直变化」—— **本报告核心发现**,定性见 §5 Bug #1
  - 玩家为无 mesh 的胶囊体,场景无边界提示,相机静止不动

## 4. Gameplay Flow

### What worked well

- 启动序四步全部正常:Addressables 初始化 → world 场景 additive 加载(灰色 Plane 可见)→
  玩家生成于 (0,1,0) → 落地静止(y = 1.080 = 静止浮空位)
- Console 全程干净(无红错、无黄警告)
- 键盘输入链健康:W/S 驱动 z、A/D 驱动 x,响应正确(测试者口述 + 阶段 2 已有自动化覆盖)
- **静置落地稳定**(batch capture 模式复证,见 §7:240 帧 y 峰谷 = 0、接地 240/240)

### Pain points

- **出界无底** —— 10×10m Plane(±5m)之外无限下落,无边界提示、无坠落重置(High)。
  SpeedWalk = 5 m/s,按住 W 1 秒即出界
- **相机零跟随**(High)—— 玩家走出视野相机不动,玩家感知不到自己走到边缘/正在下坠,
  空间反馈完全失真(ADR-020 已裁第三人称越肩,实现未开)
- **无任何目标/反馈 UI**(Medium,预期内)—— 阶段 3 形态件施加轮未开,灰盒无 HUD

### Confusion points

- 「y 持续变化」——玩家把「掉出世界」读成 bug(无视觉线索说明自己已出界;
  实为可玩面不足而非移动缺陷,定性见 §5)

### Moments of delight

- 无(灰盒阶段,零内容反馈)

## 5. Bugs Encountered

| # | Description | Severity | Reproducible |
|---|-------------|----------|-------------|
| 1 | **走出 10×10 Ground 边缘后无限下落** —— 无边界防护、无坠落重置;y 持续变小至无底。玩家视角 =「y 不受控制地一直变化」 | High(玩家感知)/ 定性 = **可玩面不足**(非移动 bug;静置落地稳定已证) | Yes —— batch 探针 v8 复现:`Time.captureDeltaTime=1/60` + Teleport(0,1,4.4) + 按住 +z,z 越过 5.042 后 y 从 1.080 按自由落体曲线坠至 **-10.088**(证据 `unity/Logs/f5_landing_probe8.xml`) |
| 2 | **`LocomotionConfig` 未接线** —— `SkinWidth=0.01 / MinMoveDistance=0 / Height=1.8` 从未应用到 `CharacterController`,controller 裸用 Unity 默认(0.08 / 0.001 / 2)⇒ 静止位浮空 8cm(y=1.08) | Low(视觉)—— 违 coding standard「Gameplay values must be data-driven」候选 | Yes —— v6/v8 实测静止位 y=1.080 = 1.0 + 默认 skinWidth 0.08;`PlayerController.Move` 仅消费 SpeedWalk/Accel/Decel |

**定性说明(bug #1)**:这不是移动/落地代码缺陷 —— v6 capture 模式静置 240 帧 y 峰谷 = 0、
接地 240/240、vy 恒 0,落地循环完全稳定;「y 一直变化」的唯一路径 = 走出 Plane 边缘后
自由落体。修复归属:① 边界防护/坠落重置 → 归阶段 3 世界扩展(6 世界与生态区);② 相机
跟随 → 归 ADR-020 实现轮。**均不阻塞 M2 收口**(M2 Exit 未要求可玩边界)。

**定性说明(bug #2)**:修复归移动系统实现轮(玩家控制器与移动,系统 1);skinWidth 默认
0.08 亦是探针方法学教训(batch 快帧下 minMoveDistance 吞帧造成 v2/v5 伪绿,详见 §7)。

## 6. Feature-Specific Feedback

### 启动序(Gameplay.Boot)

- **Understood purpose?**:N/A(玩家不可见)
- **Found engaging?**:N/A
- **Suggestions**:四步正常、Console 干净;无需改动

### 玩家移动(系统 1 / PlayerController)

- **Understood purpose?**:Yes —— WASD 即动,方向正确
- **Found engaging?**:未口述(灰盒无手感反馈;速度/加减速数值归数值轮)
- **Suggestions**:出界行为须有边界语义(墙/坠落重置/软边界,归世界扩展)

### 相机(ADR-020 自建机位)

- **Understood purpose?**:N/A(未实现)
- **Suggestions**:第三人称越肩跟随是**任何空间感知的前提** —— 不实现则「走到边缘」「
  面对谁」全部失真;建议优先于内容扩展

### 核心循环(病人 → 诊断 → 治疗)

- **NOT-RUN** —— 病人运行期不出现(`PatientSpawner.SpawnNext` 零生产调用点)、
  诊断 UI 未开。**本报告对核心循环零覆盖,不得据此判断其可玩性**

## 7. Quantitative Data

**人工操作**:约 10 分钟;死亡 0(无死亡系统,无限下落不计);核心循环覆盖 0%。

**batch 诊断探针**(8 版迭代,探针文件已按裁定删除,判决 XML 留 `unity/Logs/`):

| 跑 | 结果 | 关键数据 |
|---|---|---|
| v1 静置 2s 单点 | — | y = 1.080 = 1.0 + skinWidth(静止浮空位) |
| v2 快帧 210 帧 | 伪绿 | y 恒 1.000、接地 1/210 —— 快帧 Move 被 minMoveDistance 吞(观测伪象) |
| v4 三路 180 快帧 | 红(判据过严) | y 1.000→1.080(沉降);tick Δ=1;vy 累积被清零 |
| v5 真实时间 4s/26355 帧 | 绿(伪绿) | y 钉死 1.080;但 vy ∈ [-1.935,0]、接地 26/26355 —— 快帧下循环空转不推 transform |
| **v6 capture 1/60 静置 240 帧** | **绿** | **y 峰谷 0.0000、接地 240/240、vy 恒 0 —— 静置稳定实锤** |
| v7 capture + Move 300 帧 | 红(测试缺陷) | z 仅 0→0.833 —— BootRoot.Update 双驱动互抢(净速 = 单帧 accel 增量),非游戏 bug |
| **v8 capture 单驱动出界** | **绿** | **z 4.4→12.608;y 1.080→-10.088 自由落体曲线;z 过 5.042 起坠** |

**方法学结论(可复用)**:PlayMode 测 CharacterController 行为**必须** `Time.captureDeltaTime`
(batch 快帧下 minMoveDistance 会吞帧造成伪绿);测试直调 `Move()` 须停 `BootRoot.Update`
(防双驱动互抢)。证据 XML:`f5_landing_probe{4,5,6,7,8}.xml`。

## 8. Overall Assessment

- **Would play again?**:N/A(灰盒无可玩内容;未口述)
- **Difficulty**:N/A(无目标系统)
- **Pacing**:N/A
- **Session length preference**:N/A

**一句话**:启动序与移动链健康、Console 干净 —— M2 阶段 2 的接线底座成立;
但「可玩」还差三个前提:边界语义、相机跟随、核心循环可达性。

## 9. Top 3 Priorities from this session

1. **可玩面边界语义**(bug #1)—— 出界无底会被每个玩家读成 bug;最低成本 = 坠落重置
   或边界墙;完整方案归阶段 3 世界扩展(6)
2. **相机跟随**(ADR-020 第三人称越肩)—— 不实现则一切空间反馈失真(走到哪、面对谁、
   掉出世界),是内容扩展的前置而非并行项
3. **核心循环可达性**(M2 Exit 主体)—— 病人出现(`PatientSpawner` 接线)+ 诊断/治疗
   UI 通路(阶段 3 形态件);当前人工面 0% 覆盖,是下一批的主战场

## Appendix:发现分类路由(承 SKILL Phase 3)

- **Bug reports**:bug #1(出界)→ 归属已裁(世界扩展 + ADR-020 实现轮),不新开单;
  bug #2(配置未接线)→ 归移动系统实现轮,已在 §5 登记
- **Design changes**:无 —— 本次零设计变更(出界/相机均为已知排期项,非 GDD 冲突)
- **Balance adjustments**:无(数值轮另行)
- **Polish items**:静止浮空 8cm 视觉(bug #2 的表现侧)归 polish
- **CD-PLAYTEST**:skipped —— Lean mode(默认,`production/review-mode.txt` 未设 full)
