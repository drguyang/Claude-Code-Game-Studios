# Playtest Report — sprint-07 第五轮人工 playtest

> **Sprint**:sprint-07(内容解锁轮:S1 词表真装载 / S2 E·F 设备入口+42接线 / S3 候选列表)
> **Build**:提交 `865eb44`(C3 结案后;EditMode 3255 = 3208/0 · PlayMode 136/136)
> **上游**:playtest-2026-10-11-m2-round4.md(第四轮,5A;零自动急救 + 空行纪律)
> ⚠️ 本报告**不替代任何 Logic/Integration 门** —— 自动化判据见各 wiring 测试与
> `examine_driver_test` / `emergency_manual_driver_test` / `interaction_input_chain_test`。
> 模板权威 = `.claude/skills/playtest-report/SKILL.md`(9 节)。

## 1. Session Info

- **Date**:2026-10-12
- **Build**:提交 `865eb44`(Unity Editor `6000.3.24f1` Play 模式;输入入口接线后首轮人工)
- **Duration**:约 8 分钟(人工操作;答题面 Q1–Q7)
- **Tester**:dr_guyang(用户)
- **Platform**:PC(Linux 桌面机,Unity Editor)
- **Input Method**:KB+M(WASD 移动 · E 查体 · F 急救)
- **Session Type**:Targeted test —— 第五轮,覆盖 sprint-07 内容解锁后的行为面:
  **值区有词(真词表) · E/F 玩家入口 · 候选列表可见 · 病人出现 · 零自动急救回归**

## 2. Test Focus

| # | 面 | 上轮状态 | 本轮判据 | 结果 |
|---|---|---|---|---|
| Q1 | 值区有词(按 E 查体) | 空行(入口未接) | 按 E 近病人 ⇒ 值区出现**真词表词条** | 🔴 **C1**(根因 = 组合根 `queryLevel: 0` 占位 ⇒ 求值恒 0 词条 —— **已修 0→1 + 回归测**,见 §5) |
| Q2 | 急救手动链(按 F) | 零自动(第四轮) | 按 F ⇒ 双事件入流 + 可感知反馈 | ◐ **登记**:流侧链路自动化已绿(PlayMode 注入双事件);人工面**零可见反馈**(候选列表不随 F 变化 —— P2 story-008 施治真资产缺位的直接后果,非断链) |
| Q3 | 候选列表可见 | 载体就绪 | 运行期列表(止血/通气 + 跳过)可见 | ✅ A |
| Q4 | 病人出现 | 1 个 | 启动 1 个,时间推进后增殖 | ✅ A(启动 1 → WASD 后 3;spawn 间隔 400 tick + 接线期上限哨 3 = 预期) |
| Q5 | 移动/相机 | ✅ | WASD 正常、相机跟随 | ✅ A |
| Q6 | 纸面反馈 | ✅ | 脉案纸/线格/通道行结构 | ✅ A |
| Q7 | 零自动急救(回归) | ✅ 第四轮确认 | 站着不动零急救提示 | ✅ A |

**结果汇总**:A × 5 · C1 × 1(已修)· ◐ × 1(登记)—— 玩家入口**第一次真实可用**(E/F 有响应),
值区词条链**断在 queryLevel 占位**(本轮最大发现,已修复 + 回归测)。

## 3. First Impressions(前 5 分钟)

- **Understood the goal?**:Partial+ —— 病人 + 脉案 + 候选列表俱在,值区词条链差最后一步(已修)
- **Understood the controls?**:Yes —— WASD/E/F 全部有响应(E/F 是本轮首次人工可用)
- **Emotional response**:未口述(灰盒)
- **Notes**:
  - 不按 E 时值区可见 6 行**通道行标签**(面色/语声/姿态/呼吸/触感/问诊 = UXML 静态结构,
    预期形态;用户观察记为「6 行词条」—— 行标题非读数词条)
  - 按 E **零变化**(查体链静默失败在求值层:queryLevel=0 ⇒ `named = skill ≥ tier_named` 恒 false)
  - 按 F **零可见变化**(候选列表不响应;流侧事件已入但玩家不可感知)
  - Console 两条已知噪音:`font-family` USS 属性警告(9 处,UI Toolkit 不识别该属性)+
    `[SkeuoRuntimeDriver] ICasebookDtoSource 未注入`(Awake 早于 BootRoot.Start 注入的时序警告,
    注入后 Source setter 会重绑 —— 非断链)
  - 正常退出 Play

## 4. Gameplay Flow

### What worked well

- 启动序完整 → 病人出现(1 → 3,间隔节奏预期)→ 脉案纸 + 候选列表挂载可见 → 零自动急救(回归)
- **E/F 玩家入口首次人工实证**(第四轮「零入口」→ 本轮「有响应」—— 响应内容另论)
- 移动链 + 相机跟随无回归

### Pain points

- **按 E 值区无词条**(Q1 = 本轮头号痛点;根因已定位并修复 —— 见 §5)
- **按 F 零可见反馈**(Q2 = 玩家无法感知急救生效;P2 story-008 的立项依据被人工面坐实)

### Confusion points

- 「6 行不按 E 也在」= 通道行标题(UXML 结构恒在),与「词条」的区分未在 UI 上表达 ——
  行标题 vs 读数词条的视觉层级归 42(登记,非本轮范围)

### Moments of delight

- 无(灰盒;但 E/F 有响应本身 = 解锁感)

## 5. Bugs Encountered

| # | Description | Severity | Reproducible | 处置 |
|---|-------------|----------|-------------|------|
| 1 | **按 E 值区零词条** —— 根因 = `CompositionRoot.cs` 组合根 `queryLevel: 0` 占位 ⇒ 真词表双条件与门 `named = skill ≥ tier_named`(tier_named ∈ {1,10,20})恒 false ⇒ OnExamine 落空集快照 | **High**(核心判据断链) | 是(按 E 必现) | **✅ 已修(本轮)**:`queryLevel: 0 → 1`(tier-1 词 20 条可读)+ 回归测 `test_q1_compositionRoot_examine_yieldsWords_atInitialSkill`(走组合根生产驱动 + 真词表 + position 1.0 ⇒ SignCount>0;改回 0 恰红)。EditMode 10/10 全绿 · 全量 EditMode 3256 = 3209/0 · PlayMode 136/136 |
| 2 | **按 F 零可见反馈** —— EmergencyAttempt/TreatmentApplied 入流(自动化已绿),但候选列表/脉案无任何玩家可感知变化 | Medium(体验缺口,登记) | 是 | **登记非隐藏**:流侧链路 = `interaction_input_chain_test` PlayMode 注入双事件全绿;可见反馈缺位 = **P2 story-008(施治真资产)的直接后果** —— 止血/通气 SFX + 手势/节律 VFX 落地后 F 才有动作感。候选列表交互面(选中态/命中反馈)归 10 + 42 |
| 3 | USS `font-family` 属性警告(9 处) | Low(噪音) | 是 | UI Toolkit 不识别 `font-family`(应为 `-unity-font` / FontAsset 引用);登记 polish,不阻塞 |
| 4 | `[SkeuoRuntimeDriver] ICasebookDtoSource 未注入` 警告 | Low(时序噪音) | 是 | Awake(AddComponent 即跑)早于 BootRoot.Start 注入 ⇒ 警告必发一次;Source setter 注入后重绑。非断链,登记 polish(警告降级/时序注释) |

## 6. Feature-Specific Feedback

### 值区真词表(sprint-07 S1.2 + Q1)

- 词表/门槛真装载**已在**(EditMode 真表 34 行 + 漂移门全绿)—— 断点是**最后一厘米**:
  组合根技能档占位 0。修复后玩家初始档 1 ⇒ 20 条 tier_named=1 词可读
  (position ≥ floor(skill=1) ≈ 0.98 时;低位期仍空行 = 门槛语义,诚实呈现)。
- ⚠️ **数值面登记**:skill=1 的门槛 ≈ 0.98 意味着病人病程低位期查体仍读不出 ——
  这是 F-8.1 曲线语义(base_read=1, floor_min=1/4, gamma=2)非 bug;
  「初始档该是几」归数值轮(30 QueryLevel + 8 门槛曲线联调)。

### 急救手动链(sprint-07 S2.5 + Q2)

- 事件层可证(自动化双事件入流);体验层不可证(零反馈)。**这正是 P2 提前的论证闭环**:
  无真施治资产,「施治好玩」不可判(M5 核心假设)。Q2 的 ◐ 是 story-008 的人工面证据。

### 病人出现 / 零自动(回归)

- 启动 1 → WASD 后 3 = `PatientAppearedDriver` 间隔 + 上限哨 3 预期形态,无回归;
  站立零急救维持(第四轮 G4.1 红线持续生效)。

## 7. Quantitative Data

**人工操作**:约 8 分钟;核心循环七环节中**玩家可操作面首次真实**(E/F 有响应);
值区词条链断于 queryLevel(已修);急救可见反馈缺位(P2 立项依据)。死亡 0。

**自动化(本轮修复后复跑)**:
- `ExamineDriverTest` EditMode **10/10**(新增 Q1 回归测全绿)
- 全量 EditMode **3256 = 3209 passed / 0 failed / 46 skipped / 1 inc**(基线 3255 ⇒ +1,零回归)
- 全量 PlayMode **136/136**
- 证据:`unity/Logs/q1_examine_editmode.xml` · `q1_full_editmode.xml` · `q1_full_playmode.xml`

**用户选项汇总**:Q1=C(红,根因已修)· Q2=◐(登记)· Q3/Q4/Q5/Q6/Q7=A。

## 8. Overall Assessment

- **Would play again?**:N/A(灰盒,但 E/F 有响应 = 循环首次可操作)
- **Difficulty**:N/A · **Pacing**:N/A · **Session length**:N/A

**一句话**:「门开了,灯还没亮」—— E/F 玩家入口首次人工实证可用;值区词条链断在
技能档占位(本轮最大发现,已修 + 回归测);急救有事件无反馈(P2 提前的人工面证据)。
sprint-07 的「有内容的真循环」= 差 Q1 修复后的人工复验一锤。

## 9. Top 3 Priorities from this session

1. **Q1 修复后人工复验**(下一拍,用户方便时):按 E 值区出词条(病人病程推进后)
   —— 通过 ⇒ sprint-07 收口判据全绿
2. **P2 story-008 排期启动**(施治真资产):Q2 零反馈坐实「无真动作感不可判好玩」——
   止血/通气 SFX + 手势/节律 VFX(素材 = batch-03)
3. **Polish 登记**(不阻塞):`font-family` USS 警告 ×9 · SkeuoRuntimeDriver 时序警告 ·
   「6 行 vs 词条」视觉层级(42)

## Appendix:发现分类路由(承 SKILL Phase 3)

- **Bug reports**:Q1 queryLevel 占位(✅ 已修 + 回归测)
- **Design changes**:无(「初始档该是几」归数值轮,不改设计)
- **Balance adjustments**:skill=1 门槛 ≈0.98 ⇒ 低位期读不出(登记数值轮:30 初始档 × 8 门槛曲线)
- **Polish items**:font-family USS ×9 · SkeuoRuntimeDriver 时序警告 · 行标题 vs 词条视觉层级
- **CD-PLAYTEST**:skipped —— Lean mode(默认,`production/review-mode.txt` 未设 full)
