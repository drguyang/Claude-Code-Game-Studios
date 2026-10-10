# Playtest Report — sprint-05 第二轮人工 playtest

> **Sprint**:sprint-05(Phase A + T2.0 后)
> **Build**:`7f49ff7`(T2.0 病人可见实体 + B 案 spawn + AudioListener 单挂点修复)
> **上游**:playtest-2026-10-10-m2-manual.md(第一轮,三项 NOT-RUN / 未验面)
> ⚠️ 本报告**不替代任何 Logic/Integration 门** —— 自动化判据见各 wiring 测试。
> 模板权威 = `.claude/skills/playtest-report/SKILL.md`(9 节);落盘路径 =
> `production/playtests/`(承 M2 Exit Criteria;原 `production/qa/playtests/` 为措辞错)。

## 1. Session Info

- **Date**:2026-10-10
- **Build**:`7f49ff7`(Unity Editor `6000.3.24f1` Play 模式)
- **Duration**:约 5 分钟(人工操作;答题面 Q1–Q5)
- **Tester**:dr_guyang(用户)
- **Platform**:PC(Linux 桌面机,Unity Editor)
- **Input Method**:KB+M(键盘 WASD)
- **Session Type**:Targeted test —— 第二轮,覆盖第一轮 NOT-RUN / 未验面:
  **病人在场可见(T2.0)· 相机跟随(T1.5)· 出界坠落重置(T1.4)** · 退出 Play(AudioListener 修复)

## 2. Test Focus

| # | 面 | 上轮状态 | 本轮判据 | 结果 |
|---|---|---|---|---|
| 1 | 病人在场可见 | NOT-RUN(零视觉实体) | 启动后即看到 ≥1 个病人胶囊 | ✅ |
| 2 | 病人稳定性 | — | 走远走回病人原地不动(接线期占位格) | ✅ |
| 3 | 相机跟随 | 未验(零生产调用点) | 玩家位移相机跟随 | ✅(上轮已人工确认) |
| 4 | 出界坠落重置 | 未验(无限下落) | 出界后 y 回 ≥0 | ✅(上轮已人工确认) |
| 5 | Console 健康 | 干净(上轮) | 无红错/黄警告/刷屏 | ✅ |
| 6 | 退出 Play | **异常**(上轮 31868 条警告 → EPIPE 崩溃) | 正常退出 | ✅ |

**不在本次可玩面内**(如实登记,不借绿):拟物 UI 两 TODO 未开 ⇒ 脉案 / 体征 /
诊断/治疗界面仍不可见;看病流程可玩性**仍 NOT-RUN**(归 T2.1–T2.5)。

## 3. First Impressions(前 5 分钟)

- **Understood the goal?**:Partial —— 仍无 UI/HUD 目标提示,但「世界里有病人了」
- **Understood the controls?**:Yes —— WASD 移动正常,相机跟随
- **Emotional response**:未口述(灰盒阶段,零反馈系统)
- **Notes**:
  - 启动后 1–2 秒内即看到病人胶囊(首 tick spawn 生效)
  - 胶囊为占位视觉(Standard shader 在 URP 下 fallback 呈紫色,非中性灰 —— 已登记,
    归 44 表现层真材质);站在玩家附近(几米内)
  - 走远走回病人原地不动(接线期占位格,真实位置归 6 生态区 + 13 AI,未实现)
  - Console 干净(无红错、无黄警告)
  - **能正常退出 Play**(AudioListener 单挂点修复生效 —— 上轮此面是 EPIPE 崩溃)

## 4. Gameplay Flow

### What worked well

- 启动序完整(Addressables → 装配 → World additive → 玩家 → CameraRig → 病人视觉)
- 病人在场可见:启动后即出现,稳定不动,不随 WASD 刷屏(B 案上限哨 3 生效)
- 相机跟随 + 出界坠落重置(上轮人工确认,本轮未复验 —— 同一 commit)
- **退出 Play 正常**(上轮此面崩)

### Pain points

- 病人胶囊紫色(占位视觉;Standard→URP fallback = magenta) —— Low
- 病人原地不动,无任何「为什么站在这」线索(接线期占位,无 AI) —— Low,预期内

### Confusion points

- 无(本轮无「y 一直变化」类误读 —— 出界已被坠落重置兜住)

### Moments of delight

- 无(灰盒,零内容反馈)

## 5. Bugs Encountered

| # | Description | Severity | Reproducible |
|---|-------------|----------|-------------|
| 1 | **病人胶囊呈紫色** —— `Shader.Find("Standard")` 在 URP 管线下降级到 magenta fallback,中性灰设置未生效 | Low(占位视觉,不影响判定) | Yes —— 与管线有关,非逻辑 bug |
| 2 | ~~出界无限下落~~ **已修复**(T1.4 坠落重置,上轮人工确认) | — | 关 |
| 3 | ~~多 AudioListener 刷屏 → Editor EPIPE 崩溃~~ **已修复**(单挂点 + fail-loud,本轮 A5 确认正常退出) | — | 关 |

**定性(bug #1)**:占位视觉不映射任何语义(中性灰是接线期约定,紫 = fallback)。
真材质归 44 表现层(13 的 `Material(p)` 词表 + 8 的表),T2.x 轮或 44 实现轮替换;
**不阻塞本轮判据**(「病人可见」不依赖颜色)。

## 6. Feature-Specific Feedback

### 病人出现 + 可见实体(sprint-05 T2.0)

- **Understood purpose?**:Yes —— 世界里有病人了,核心循环入口首次可人工观察
- **Found engaging?**:未口述(占位胶囊零行为)
- **Suggestions**:真实位置与移动归 6 + 13;颜色/形态归 44 表现层

### 相机跟随(sprint-05 T1.5)

- **Understood purpose?**:Yes —— 玩家位移相机跟随(上轮已确认)
- **Found engaging?**:未口述
- **Suggestions**:无(自建机位,ADR-020)

### 出界坠落重置(sprint-05 T1.4)

- **Understood purpose?**:Yes —— 出界不再无限下落
- **Found engaging?**:未口述
- **Suggestions**:完整边界语义(墙/软边界)归 6 世界扩展

### 核心循环(病人 → 诊断 → 治疗)

- **入口已可达**:病人运行期出现(本报告 §2 #1)。**后半段仍 NOT-RUN**:
  诊断/治疗 UI 未开(T2.1–T2.5 未施加),**不得据此判断其可玩性**。

### 退出 Play(AudioListener 单挂点)

- **Understood purpose?**:N/A(玩家不可见,工程侧)
- **Suggestions**:修复已生效 —— 上轮 EPIPE 崩溃面本轮 A5 关闭

## 7. Quantitative Data

**人工操作**:约 5 分钟;核心循环覆盖:入口(病人出现)首次可观察;后半段(诊断/治疗)0%。
死亡 0(出界即重置,无死亡系统)。

**T2.0 wiring 测**(自动化,不替代人工):PlayMode 105/105 全绿;camera 测试
`audio listeners` 警告条数 = **0**(修前每帧一条);EditMode 3146/0 红/1 inc/46 skip。
证据:`unity/Logs/s5_al_fix_playmode_full.xml` · `unity/Logs/s5_al_fix_editmode_full.xml`。

**用户选项汇总**:Q1=A(立刻看到)· Q2=A(胶囊体在附近)· Q3=A(原地不动)·
Q4=A(Console 干净)· Q5=A(正常退出)。

## 8. Overall Assessment

- **Would play again?**:N/A(灰盒无可玩内容;未口述)
- **Difficulty**:N/A(无目标系统)
- **Pacing**:N/A
- **Session length preference**:N/A

**一句话**:「世界里有病人了」—— 核心循环入口从 0% 到可人工观察;相机、出界、
退出 Play 三个上轮痛点全部关闭;剩余可玩性差诊断/治疗 UI(T2.1–T2.5)。

## 9. Top 3 Priorities from this session

1. **拟物 UI 施加(T2.1–T2.5)** —— 核心循环后半段(诊断/治疗)仍 0% 人工覆盖,
   是 M2 Exit 主体;病人可见已为 UI 提供可绑数据
2. **病人空间/AI 归位(T2.0 占位)** —— 真实位置归 6 生态区 + 13 AI;胶囊紫色归 44 真材质
3. **上限哨 3 到期追踪** —— 真节奏(52 随机事件导演)落地后删除
   `MaxPatientsDuringWiring`(代码注释已写明删除条件)

## Appendix:发现分类路由(承 SKILL Phase 3)

- **Bug reports**:bug #1(紫胶囊)→ 占位视觉,归 44 表现层真材质,不新开单;
  bug #2/#3 已关闭(T1.4 / AudioListener 修复)
- **Design changes**:无 —— 本轮零设计变更(病人占位/位置/色彩均已知排期项)
- **Balance adjustments**:无(数值轮另行)
- **Polish items**:紫胶囊 → polish;病人无行为 → 归 13 AI
- **CD-PLAYTEST**:skipped —— Lean mode(默认,`production/review-mode.txt` 未设 full)
