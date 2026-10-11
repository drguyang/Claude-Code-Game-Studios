# Playtest Report — sprint-06 第四轮人工 playtest

> **Sprint**:sprint-06(G1 查体 / G2 落笔 / G3 处方 / G4 急救手动化 后)
> **Build**:提交 `8457d5b`(G3+G4+G5 收口;EditMode 3185/0/1/46 · PlayMode 133/133)
> **上游**:playtest-2026-10-11-m2-round3.md(第三轮,5A;T2.x 形态件真通道)
> ⚠️ 本报告**不替代任何 Logic/Integration 门** —— 自动化判据见各 wiring 测试与
> `prescribe_driver_test` / `emergency_manual_driver_test`。
> 模板权威 = `.claude/skills/playtest-report/SKILL.md`(9 节)。

## 1. Session Info

- **Date**:2026-10-11
- **Build**:提交 `8457d5b`(Unity Editor `6000.3.24f1` Play 模式;G4 手动化后首轮人工)
- **Duration**:约 5 分钟(人工操作;答题面 Q1–Q5)
- **Tester**:dr_guyang(用户)
- **Platform**:PC(Linux 桌面机,Unity Editor)
- **Input Method**:KB+M(键盘 WASD,方向键并入)
- **Session Type**:Targeted test —— 第四轮,覆盖 sprint-06 手动化后的行为面:
  **病人出现 · 值区空行纪律 · 急救手动化(零自动) · 移动/相机 · 纸面反馈**

## 2. Test Focus

| # | 面 | 上轮状态 | 本轮判据 | 结果 |
|---|---|---|---|---|
| 1 | 病人出现(胶囊) | ✅ 自动 spawn | 启动后出现(预期 1 个) | ✅ A |
| 2 | 值区空行纪律 | ✅ 空行 | **输入入口未接 ⇒ 仍空行**(查体/落笔驱动就绪零入口) | ✅ A |
| 3 | **急救手动化** | 自动(400 tick) | **零输入 ⇒ 零急救事件/动作提示**(G4.1 红线) | ✅ A |
| 4 | 移动/相机 | ✅ | WASD+方向键+摇杆正常、相机跟随 | ✅ A |
| 5 | 纸面反馈 | ✅ 纸/线格/空行 | 形态如第三轮(USS 九份挂载) | ✅ A |

**5A 全绿** —— 本轮确认 sprint-06 手动化后行为面:**零自动急救**(G4.1 生效)· 值区仍空行
(输入入口未接,禁假数据面诚实呈现)· 世界/移动/纸面健康。

## 3. First Impressions(前 5 分钟)

- **Understood the goal?**:Partial —— 世界里有病人 + 脉案纸 + **不再有自动急救**
- **Understood the controls?**:Yes —— WASD/方向键移动(并入集合同第三轮)
- **Emotional response**:未口述(灰盒,零内容反馈)
- **Notes**:
  - 启动即见病人胶囊 + 脉案纸页(六行 + 右栏病名/「?」,同第三轮)
  - **站着不动无任何急救提示 / 无急救事件**(G4.1 移除自动触发的人工面确认)
  - 六行读数区仍空行(输入入口未接 —— 查体/落笔驱动已就绪但 Interact 键零运行期消费)
  - WASD/方向键移动 + 相机跟随正常(同第三轮)
  - Console:无红错、无刷屏;无自动急救刷事件
  - 正常退出 Play

## 4. Gameplay Flow

### What worked well

- 启动序完整 → 病人出现(胶囊)→ 脉案纸页呈现 → **零自动急救**(行为面与第三轮的关键差异)
- 移动链 + 相机跟随稳定(第三轮已实证的虚拟键证据维持)
- 纸/线格/空行形态如常(USS 九份挂载)

### Pain points

- 无(灰盒,本轮无新增痛点;值区空行是预期面而非痛点)

### Confusion points

- 无新增。**输入入口缺失是预期登记,不是困惑**:Interact/Emergency 键位已存在
  (InputSystem_Actions.inputactions)但零运行期消费 ⇒ 查体/落笔/开方/施治暂无人手入口

### Moments of delight

- 无(灰盒,零内容反馈)

## 5. Bugs Encountered

| # | Description | Severity | Reproducible | 处置 |
|---|-------------|----------|-------------|------|
| 1 | **无自动急救已人工确认(G4.1 生效)** | — | — | 非 bug —— 手动化判据的正向人工半;自动化半 = `emergency_manual_driver_test` 5 测 + wiring 反向改造(零输入零事件,重新引入即红) |
| 2 | 输入入口未接(查体/落笔/开方/施治的 Interact/Emergency 键零消费) | Medium(功能缺口,登记) | — | **登记非隐藏,不禁借绿**:驱动就绪零入口;归下一交互轮(玩家输入入口接线)。自动化半 = `examine_driver_test` / `judgment_writer_test` / `prescribe_driver_test` / `emergency_manual_driver_test` 全绿(驱动级判据已全判) |
| 3 | ③ 急救候选列表零生产实例化 | Medium(形态件施加半,登记) | — | 载体形态就绪(story-023 交付),生产实例化归下一交互轮(M2 Exit #8 ◐ 诚实登记) |

## 6. Feature-Specific Feedback

### 急救手动化(sprint-06 G4)

- **Understood purpose?**:Yes —— 从「自动刷事件」到「玩家触发」的语义切换已人工可感
- **Found engaging?**:未口述
- **Suggestions**:零输入零事件是**预期行为**;候选列表 / 手动入口的 UI 呈现归输入入口轮后才有意义

### 值区空行纪律(输入入口未接的诚实呈现)

- 六行仍空行 = 「还没查」(输入入口未接)而非「查了读不出」—— 空行纪律人工面维持
- ⚠️ 不借此绿 Logic/Integration 门:驱动级判据已全绿,运行期**玩家入口**归下一交互轮

### 移动 / 相机 / 纸面

- 与第三轮一致,无回归;纸/线格/空行形态稳定

## 7. Quantitative Data

**人工操作**:约 5 分钟;核心循环入口(病人出现)+ 呈现(脉案纸)可人工观测;**急救零自动事件**(G4.1 生效);诊断/治疗交互仍 0%(输入入口轮未开)。死亡 0(出界重置)。

**自动化(本轮已提交推送,不替代人工)**:
- `prescribe_driver_test` EditMode **8/8**:选档落笔入病史流 / ≥2 档 dose 区分 / 无货拒开 / GateHit SkillGrown
- `emergency_manual_driver_test` EditMode **5/5**:零输入零事件 / 手动双事件 / Skip method / fail-loud
- 全量门:EditMode **3185/0/1/46** · PlayMode **133/133**(提交 `8457d5b`)

**用户选项汇总**:Q1=A(病人出现)· Q2=A(值区空行)· Q3=A(零急救)· Q4=A(移动/相机)· Q5=A(纸面)。

## 8. Overall Assessment

- **Would play again?**:N/A(灰盒无可玩内容)
- **Difficulty**:N/A · **Pacing**:N/A · **Session length**:N/A

**一句话**:「自动急救没了,世界还在」—— sprint-06 手动化后行为面 5A 全绿:零自动急救(红线人工半)、
值区诚实空行、移动/相机/纸面无回归。sprint-06 G1–G5 主链 + 全量门收口,剩玩家输入入口归下一轮。

## 9. Top 3 Priorities from this session

1. **玩家输入入口接线(下一交互轮)**:Interact/Emergency 键位已存在零运行期消费 —— 把
   `ExamineDriver.OnExamine` / `JudgmentWriter.TryRecord` / `PrescribeRequestDriver.TryPrescribe` /
   `EmergencyManualDriver.TryManual` 接进键位回调;③ `EmergencyCandidateList` 生产实例化
   ⇒ M2 Exit #8 施加点接完
2. **第四轮报告落盘 + 提交推送**(本报告;原文不删,闭环记录)
3. **下一 sprint 规划**(playtest 无新增 bug;数值轮 / 输入入口 / 候选列表 / ④ 词表真装载面)

## Appendix:发现分类路由(承 SKILL Phase 3)

- **Bug reports**:无本轮新 bug(G4.1 生效 + 空行纪律为预期面;输入入口/候选列表 = 登记缺口非 bug)
- **Design changes**:无(本轮零设计变更)
- **Balance adjustments**:无(数值轮另行)
- **Polish items**:无新增
- **CD-PLAYTEST**:skipped —— Lean mode(默认,`production/review-mode.txt` 未设 full)
