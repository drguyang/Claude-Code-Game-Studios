# Session State — 2026-10-02(**当前阶段 = Pre-Production · Sprint 04 Phase 1 收尾 · Phase 2 未启动**)

## 📊 全项目进度总览（2026-10-02 刷新）

### 阶段状态

| 项 | 值 |
|---|---|
| **Stage** | Pre-Production |
| **Sprint** | sprint-03 ✅ 已闭（17/17 story）· sprint-04 Phase 1 收尾中（Phase 2 未启动） |
| **Gate Check** | CONCERNS（2026-09-29 二轮，无 NOT READY 阻塞） |
| **ADRs** | 28/28 Accepted |
| **P0 GDDs** | 31/31 Approved |
| **Commits since 09-22** | 375 |

### Epic 故事进度

| Epic | Stories | Complete | Ready | In Progress | 备注 |
|------|---------|----------|-------|-------------|------|
| **audio-system (44)** | 14 | **14** | 0 | 0 | ✅ 全收口 |
| **skeuomorphic-ui (42)** | 18 | **18** | 0 | 0 | ✅ 全收口 |
| **skill-system (30)** | 8 | **8** | 0 | 0 | ✅ 全收口 |
| **telemetry-analytics (51)** | 10 | **10** | 0 | 0 | ✅ 全收口 |
| **input-system (3)** | 13 | **13** | 0 | 0 | ✅ 全收口 |
| **item-database (21a)** | 12 | **12** | 0 | 0 | ✅ 全收口 |
| camera-viewpoint (2) | 6 | 0 | 6 | 2 | 🔵 进行中 |
| casebook (39) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| case-system (37) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| clinic-machine (24) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| combat-weapons (25) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| death-respawn (29) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| diagnosis-system (8) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| disease-simulation (9) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| emergency-procedures (10) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| enemy-ai (27) | 5 | **5** | 0 | 0 | ✅ 全收口 |
| foraging (17) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| interaction-system (4) | 6 | 0 | 6 | 2 | 🔵 进行中 |
| inventory-items (20) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| medical-consequences (53) | 4 | 0 | 4 | 0 | ⬜ 未启动 |
| modular-building (23) | 6 | 0 | 0 | 6 | 🔶 **In Review**（双评 REQUEST_CHANGES 5B(`da04f41`)· 51/51 **逐例复跑绿** 2026-10-02 · **逐 BLOCKING 对账件未落 evidence** ⇒ 依「不得借绿」不记 Complete;⚠️ 6 个 story 件**自身**即标 `In Review`,非 Complete） |
| patient-ai (13) | 4 | 0 | 4 | 0 | ⬜ 未启动 |
| player-controller (1) | 6 | **6** | 0 | 0 | 🔶 **In Review**（6/6 story Complete;仅因 evidence 对账件缺口未转 Complete —— 与 modular-building / world-ecozones 同口径） |
| prescription-medication (11) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| processing (18) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| random-events (52) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| time-weather (5) | 5 | **5** | 0 | 0 | ✅ 全收口 |
| tutorial-onboarding (48) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| world-ecozones (6) | 5 | **4** | 1 | 0 | 🔶 **In Review**（双评 REQUEST_CHANGES 4B(`43400dc`,**B1 未修降级 TODO**)· 87/87 **逐例复跑绿** 2026-10-02 · Story 005 [L] Pending · **对账件缺 + B1 降级 TODO(codec 已在库)+ host gate 无负向夹具**） |
| persistence-service (7a) | 1 | **1** | 0 | 0 | ✅ 全收口（EPIC 未在旧表列出） |
| save-slot-ui (7b) | 1 | 0 | 0 | 1 | ⬜ 未启动（EPIC 未在旧表列出） |
| **合计** | **206** | **121** | **6** | **79** | **58.7% 完成** |

> ⚠️ **2026-10-02 再算**:ADR-029 实现轮立 3 条新 story ⇒ 总数 203 → **206**(Complete 数不变)。
> `persistence-service` 由「Complete 1/1」回退为「**In Progress 1/2**」—— 002 为契约支新增。

> ⚠️ **上表于 2026-10-02 状态回填轮按各 story 真件重算**。原记「191 / 93 / 95 / 6 / 49%」三处失实:
> ① 总数 191 **漏计** `persistence-service`(1) 与 `save-slot-ui`(1),且各 epic 计数有出入 ⇒ 实测 **203**;
> ② `modular-building` 原记「6 Complete」为**误** —— 其 6 个 story 件**自身**标 `In Review`(EPIC 依「不得借绿」未转 Complete),故归 In Review 而非 Complete;
> ③ 原「In Progress 6」的 6 个计数(`camera-viewpoint` 2 · `interaction-system` 2 · `player-controller` 2)在 story 件中**均标 `Ready`**,无 `In Progress` ⇒ 归 Ready。
> 重算口径:逐 story 件解析 `> **Status**:` 首行,`Complete*` / `In Review*` / `In Progress*` / 其余=Ready。

### 测试状态

| 指标 | 值 |
|------|---|
| EditMode | **1995/2028 Passed · 0 Failed · 32 Skipped · 1 Inconclusive**（2026-10-02 batchmode 实测，Unity 6000.3.24f1） |
| PlayMode | 25/25 Passed · 0 Failed（2026-10-01 实测） |
| 确定性验证 | F7 反汇编 CLEAN · AC-29 三平台逐位一致 |

> ⚠️ **上表 EditMode 一行于 2026-10-02 订正**。原记「1831/1858」出自 `185063f`,而该提交**编译未通过**
> (`Scripts have compiler errors`)⇒ 测试从未跑起来,数字**无源**。`5572d66` 修复编译后实测 = 1989/2022。
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

### 待办 / 阻塞项

| 类别 | 事项 | 优先级 |
|------|------|--------|
| **Sprint 03 缺口** | ~~emergency-procedures 6 story 未实现~~ ✅ 已闭(2026-10-02 17/17) | — |
| **AC-S03-5** | 跨平台黄金夹具未兑现（emergency NOT-RUN · combat 零存在;根因 = ADR-012 矩阵未激活,需 UNITY_LICENSE） | 中 |
| ~~**player-controller 006**~~ | ✅ 已闭(2026-10-02 判据修复轮;AC-1-27 改类型白名单 · AC-1-23 补调用点扫描;14/14 通过) | — |
| ~~**evidence 对账件**~~ | ✅ 已闭(2026-10-02 对账轮) —— 三份逐 BLOCKING 对账件已落 `qa/evidence/`(modular · world-ecozones · player-controller);**对账结论 ≠ 修复全部成立**:modular B4 部分修 · we B1 未修 | — |
| **ADR-029** | ✅ **Accepted 2026-10-02**(评审修正三处后转正)—— `IPayloadEncoder` 第七抽象点;解 modular B4 + we B1 的**共同根因**(Sim 写者无合法编码路径) | — |
| **ADR-029 实现轮** | ✅ 三条 story 已立(2026-10-02):`persistence-service/story-002`(契约支)· `world-ecozones/story-006`(闭合 B1)· `modular-building/story-007`(闭合 B4)。**契约支是两条接线支的硬前置** | 高 |
| **收口批** | §③ 手搓门(须待两接线支都完成)· ADR-005 计数订正五处 · ADR-010 义务 15 | 中 |
| ~~**we 缺口 ①c**~~ | ✅ 已闭(2026-10-02) —— 6 例负向夹具,`PoiStateMachineTest` 19/19;突变测试坐实(删门 ⇒ 恰 4 例红)。新登记:gate 返回码 `PoiNotFound` 与「POI 不存在」混同(未修) | — |
| **联机** | 45 联机夹具（P1b） | 非阻塞 |
| **设计面 OQ** | OQ-SS-3（归 44）· OQ-IC-6（playtest）· OQ-24-1..6（数值轮） | 低 |
| **走查** | 七屏走查 NOT-RUN（45 联机夹具是硬前置） | 中 |
| **Spikes** | ADR-023 S1/S3/S4（渲染/场景加载） | 中 |
| **CI 矩阵** | ARM64 交叉格 / 发版前两格 / 三格常驻 job（需 UNITY_LICENSE secret） | 低 |
| **Milestone 1** | UNITY_LICENSE secret · 性能预算 · ADR-023 spikes | 中 |

### 下一步推荐

1. **Sprint 03 已闭**(17/17 story);残余 = AC-S03-5 黄金夹具(待 ADR-012 矩阵)
2. **闭合 Sprint 04 Phase 1 三条缺口** ⇒ modular-building / world-ecozones 转 Complete:
   ①a 逐 BLOCKING 对账件 · ①b we 的 B1 接 `Sim.Codec` · ①c host gate 负向夹具。
   **Phase 1 未收口前不启动 Phase 2**(`sprint-04.md` §下一步 明令)
3. **修 player-controller Story 006 两条判据缺陷** ⇒ 该 epic 方可转 Complete
4. **或处理待办**：45 联机夹具 / ADR-023 spikes / 七屏走查 / CI 矩阵激活

---

## 🔵 本任务(2026-09-20 · `/create-architecture` —— 已完成,含 Phase 7b 签署与 Phase 8 交接)

**产出**:`docs/architecture/architecture.md` **v1.0,1255+ 行,11 节全落盘**
(§Document Status · §Engine Knowledge Gap Summary · §Technical Requirements Baseline ·
§System Layer Map · §Module Ownership · §Data Flow · §API Boundaries · §ADR Audit ·
§Required ADRs · §Architecture Principles · §Open Questions)。

**本件是全案唯一整系统蓝图** —— 把 31 项 P0 GDD(387 条 TR)+ 19 份 ADR 翻译成可实现骨架。
**不新编 TR 编号、不发明新契约** —— 凡未在 ADR / GDD 登记过的签名一律标 ⚠️。

### 本轮六项实质发现(全部可复算,零数值冲突)

| # | 发现 | 落点 |
|---|---|---|
| 1 | **两轴层模型** —— `systems-index §3` 是**依赖档**、`门 A` 切出**运行期层**,二者真实分叉(42/44 依赖上是 Foundation 但住呈现侧)。新增 Axis A × Axis B 表(54 行,逐行标 ✅ 明文 / ⚠️ 推断) | §System Layer Map |
| 2 | **三个程序集名从未定义** —— 全仓只有 `Sim.asmdef` 一个名字;「门面程序集」/「独立契约程序集(仅 BCL)」/「边界层承载 codec 的程序集」被 4 处引用却无定义 ⇒ Required ADR **#2** | §Module Ownership §2.0 |
| 3 | **🔴 ADR 循环依赖 `{008,009,010,014}`** —— Tarjan SCC 实算;两条把 008 卷入环的边**都是正文定稿后追加的** ⇒ 门的 FAIL 判据。修复 = 改两条边措辞,**不占 ADR 名额** | §ADR Audit 5.2 · QQ-04 |
| 4 | **🔴 三流 `Kind` 有三个登记处** —— `entities.yaml` 24 支 / ADR-007 §三 5 支 / ADR-009 §三 骨架 4 支(含 `Craft`,载荷已由 Amendment J 定稿仍无条目);**并集 33 ≠ 任一处单读** ⇒ 构建期白名单会**拒收 9 支合法 Kind** | §ADR Audit 5.5 D-1/D-2 · Required ADR #3 |
| 5 | **8 条「借绿」条目** —— `status: covered` 但 `adr:` 为 null(含 2 条用户裁定翻转的);承项目纪律「裁定 ≠ 验收」⇒ 应转 `partial` + `BLOCKED-BY` | §ADR Audit 5.5 D-5 · QQ-09 |
| 6 | **12 项 P0 系统零 TR 组,而 12 项全部已有 GDD** —— 不是「部分系统欠账」,是**整批**欠账(`17 采集` 最典型) | §Technical Requirements Baseline · QQ-13 |

### 交接后的续做(2026-09-20 同日,承用户「继续,没有需要我选择的时候不要停」)

**① `/test-setup` 落地(9 件)** —— 承 ADR-012 而非模板默认形状:
`tests/README.md` · `EditMode/README.md` · `PlayMode/README.md` · `unit/README.md` ·
`integration/README.md` · `evidence/README.md` · `unit/sim/sim_fixedpoint_test.cs`(种子测试:
Q16.16 编解码往返 + ROUND_HALF_AWAY_FROM_ZERO vs Math.Round 判别 + 守恒律量纲)·
`smoke/critical-paths.md`(22 条,按初始化序分组,全标 N/A)· `.github/workflows/tests.yml`
(三格常驻 + builder→独立对拍 job + guard job + 发版前两格 workflow_dispatch;
**多个 job 故意 `exit 1` / 未接入,禁假绿**)。
⚠️ **`.asmdef` 刻意不生成** —— 程序集命名归 Required ADR #2。
本机无 `dotnet/mono`,种子测试**未经编译验证**(交付状态如实记为「未跑过」)。

**② TD 条件 C1 已执行 + 一处自纠**:
- 复算发现 §5.2 的「两条边」是**误算** —— `adr-008:34` 有三段后补内容(`009`/`014`/`010 §七`);
  只删两条剩 `{008,009,010}` 三环。**三条全移入 `Ordering Note` 标「引用/修订记录(非前置)」**
  ⇒ 全盘 Tarjan **无环**,门的 Circular Dependency Check 通过。§5.2 / 5.1 表 / C1 / QQ-04 已就地注记。

**③ TD 条件 C4 已执行**:8 条借绿 `covered → partial` + `blocked_by` + `was_status`(registry);
`traceability-index.md` 8 条摘要行标回退 + 21 行汇总与合计改 **243 / 51 / 93**(两文件实测一致);
`architecture.md` §Baseline 头部数字同步 + 分层表「未重算」警示。
⚠️ 新暴露:`TR-case-036` / `TR-interaction-015` 的 `adr` **注册表 null vs 摘要列 ADR-013** 两处不一致
⇒ 注册表侧加 `adr_divergence`,对齐归 `/architecture-review`(并流 QQ-11)。

**未 commit(无用户指令)。**

### TD 签署(Phase 7b)

- **Technical Director Sign-Off: 2026-09-20 — APPROVED WITH CONDITIONS**
  - **LP-FEASIBILITY skipped — Lean mode**(非 PHASE-GATE;`production/review-mode.txt` 不存在 ⇒ 默认 lean)
- **四项条件(零数值冲突、零设计分歧,全部是「登记面与裁决面不一致」)**:
  - **C1** 先修 ADR-008 两条后补边,解 `{008,009,010,014}` 环
  - **C2** 写 Required ADR #1(渲染与场景加载)/ #2(契约程序集清单)之前不开工
  - **C3** 三流 `Kind` 单一真源须在第一个 `IEventSink` 实现前裁定
  - **C4** 8 条借绿条目转 `partial` + `BLOCKED-BY`
- **C1–C4 不阻塞交接**(`/test-setup` 与 `/ux-design` 均不依赖 ADR 依赖图 / Kind 白名单 / registry 状态位)

### Required ADRs(5 条,详见 §Required ADRs)

- **#1 渲染与场景加载策略(URP + RenderGraph + Scene 生命周期)** —— 全案**唯一零 ADR 覆盖的 HIGH 风险域**(`grep SceneManager` = 0 命中);载 🔴① RenderGraph 摘录 / 🔴② Addressables 摘录 / §Data Flow 初始化序列 [0] 与 [7]
- **#2 契约程序集清单与命名** —— 同裁 QQ-01(`Vector3` 在三层契约里的归属)/ QQ-02 / QQ-03
- **#3 三流 Kind 单一登记真源 + 构建期校验体系** —— 载 `TR-randomevents-010` / `031`
- **#4 30 技能成长的定点化与持久化契约** —— `TR-skill-001…007` 唯一真正的 Foundation 级裸缺口
- **#5 13 病人 AI 的写路径归属**(查体诱发痉挛 / 搬运昏迷病人)—— 13 只读,写者在事件流里不存在

### 残留(不代裁,已登记)

- **QQ-07**:`concept` 组 3 条范围声明是否降级为「不需 ADR」→ **待用户裁定**
- **QQ-08**:21a 18 条 schema gap 是否显式降级(`traceability-index.md` 已自注「不需 ADR,但没别处可登记」)→ 建议降级
- **QQ-10/11/12**:`architecture.yaml` 三处顶层重复键 · `tr-registry.yaml` 的 `adr:` 字段值域不纯 · `systems-index.md` 系统 3 的层分类自相矛盾(Core vs Foundation)
- **既有残留全部保留**:W-1 会签 · OQ-10-9/10-6 · OQ-4-10/4-13 · OQ-42-5/48-4/5 · OQ-1-12 接地 spike · H1/H3→P1a
- **未 commit(无用户指令)**

---

## 🔵 本任务(2026-09-19 · `D-8-12` 动作词表结案 —— 用户裁定路线甲,全量落盘完成)

**来源**:4 三审收尾 widget 裁定「[C] 承接 8 的动作词表行 `D-8-12`」→ 摊开甲(模态分流)/ 乙(病人粗状态分流)
→ 用户裁 **「[A] 取路线甲,全量落盘」**。

**核心裁决**:世界空间裸 `Interact` 打到病人 = **就诊,单义**;查体 / 施治的岔口不在按下去是哪一义,
在**进模态后选哪一行** ⇒ 三义岔口取消。四行路由:**就诊→37 · 查体=脉案 `ModalId.Casebook` 行级动作→8 ·
施治=方笺落笔→11 · 急救=10 直读通道(不经 4)**。边界 B-1(4 出境载荷逐位不变)/ B-2(与 `AC-4-09` 正交)/
B-3(路由表非数值件)/ **B-4(P1a 望闻问切只加行;改回空间分流 = 重开触发)**。

**落盘(6 处)**:`diagnosis-system.md`(S-8.4 新节 + `AC-8-51`/`AC-8-52` + `D-8-12` 行 🔴→✅ + 头部最小行集注;
8 的 AC 50→52,既有机制/数值逐位不变)· `interaction-system.md`(14 处 `OQ-4-1` 结案回刷)·
`systems-index.md`(row 4 / row 8 / §6 / §11 ×4)·
`tr-registry.yaml`(**翻转 2 条,不新增 ID 恒 387**:`TR-interaction-015` ⚠️→✅ · `TR-case-036` ❌→✅;
实测累计 **248 ✅ / 44 ⚠️ / 95 ❌**,YAML 校验通过)· `traceability-index.md`(表头注 / 旧叙事补注 /
合计表 / per-group 两行 / §15 表头注 + `-015` 行 / 37 节 `case-036` 行 / Core 缺口表划线退出 / 变更历史结案行)· 本文件。

**收尾裁定(用户,同日)**:① **4 → ✅ Approved**(免四轮复核 = 显式风险接受;重开触发条件四条已写入
`reviews/interaction-system-review-log.md` 结案补充;残留 `OQ-4-6`/`4-7`/`4-11`/`4-17` 非阻塞带入实现期);
② `emergency-procedures.md:744` **最小行回刷已做**(指向 S-8.4,不动机制);
③ git 未 commit(无用户指令)。

**本会话任务至此全部结案**:P0 的 31 项 GDD **全部有 GDD 且全部 Approved**(4 为末项)。

---

## 🟡 上一任务(2026-09-19 · 4 交互系统三审 `--stage lean` —— 用户裁 [A] 就地修订,已全部落盘)

**三审结论**:二轮 3 阻断 + 5 推荐的**机制面全部落盘**;三审**未发现任何新机制问题**,
三处阻断**全部是前两轮修法自身的文书残留**(「修订未闭环」,非设计缺陷)。
**三轮收敛曲线**:首轮 15 条**机制** → 二轮 3 条**记账** → 三审 3 条**文书一致性** ⇒ 4 的机制面自首轮后即稳定。

**三审三项阻断(均已修)**:
- **BL-1 陈旧引用**:四处仍写「42 侧**须新立** `IModalState`」,而契约已落 `adr-013:326 §十 Amendment A`
  ⇒ 四处回刷为 ✅ 已落 + 逐条坐标(依赖行 `:927` · §Cross-Ref 两行 · §待补 ③ · UX Flag ③)。
- **BL-2 文内两个上界**:同一 `R_INTERACT` 上界 `F-4.1` 写 `max(W,H,D)−1`、Tuning/UX Flag 写 `min(W,H,D)−1`。
  **用户裁定取 `min(W,H,D)−1`**(防「候选集=全世界」,只有 `min` 达成)⇒ 四处对齐 + `4-DC-1` 补上界校验。
- **BL-3 类型误述**:伪码块把 `ModalId \| None` 当并列第二类型(`None` 实为 `= 0` 成员)+ 复制体名漂移
  ⇒ 改「引用 ADR-013,不复制成员名」。

**三审三项推荐(均已修)**:规则八表补 `Utensil`/`ClinicPanel` 两行 + 明写该表非权威源(JSON 才是)·
`AC-4-13` 反引号格式 · `F-4.1` 两行误合并拆回。

**落盘文件(5 处)**:`interaction-system.md`(三审修订)· `reviews/interaction-system-review-log.md`(三审条)·
`systems-index.md`(row 4 / §6 / §11 三处)· `tr-registry.yaml`(`-002` 三源→四源 · `-010` 焦点门→模态开集,**状态不变**)·
`traceability-index.md`(§15 表头注 + `-002`/`-010` 行 + 三审变更历史行;**ID 恒 387,无翻转**)。

**4 的转 Approved 判据(三审后)**:① ~~**8 的动作词表行**(`OQ-4-1` / `D-8-12`)~~ **✅ 已于同日结案**(见上节 S-8.4;`IModalState` 已于二轮/三审**两次核实**已落)⇒ **判据 ① 已兑现**;② 未裁项 `OQ-4-6/7/11/17` 至少给出方向。
**⚠️ 三审局限**:lean = 单会话,与二轮隐含同一复核者;三处阻断均点对点 grep 可证,不依赖判断力差异。

---

## 🟢 上一任务(2026-09-19 · 11 处方用药二轮 `--stage lean` —— ✅ Approved,免三次复核)

首轮 6 根因当日落盘;二轮 `--stage lean` = **MAJOR REVISION NEEDED(7 BLOCKING + 11 Rec)**,
当日全量落盘 + 四用户裁定(BL-1 开 21a 重开条件 · BL-2 请 9 立噪声带常量 · BL-3/4/5 成长门 11 可自判 ·
BL-6 只认 21a 的 EFF)+ 涟漪四件(21a/9/30 三类最小行不动机制)。**11 首次成文 §数据契约 `11-DC`**。
新命名失效模式:「判据对合法输入类误判」(BL-7)。TR 385→387(新增 `TR-prescription-018`/`-019`)。
用户裁「接受二轮修订、免三次复核」⇒ **✅ Approved(显式风险接受;重开触发条件见 `reviews/prescription-and-medication-review-log.md`)**。

---

## ✅ 已结案(2026-09-19 · 系统 18 炮制 —— 免二轮 ⇒ Approved)

> **(18 已结案 ⇒ ✅ Approved 2026-09-19(免二轮 = 显式风险接受);四裁定与阻断落点供后续轮次与实现期对照,重开触发条件权威在 `reviews/processing-review-log.md`,副本见下 ⑤)**:
> ① 首轮四裁定(均 [甲],2026-09-19)已定,二轮**不得重开裁定本身**,只核对修订是否忠实执行:
>    **Q-1/D-21-28** `Craft` 沿用三流全序键 `(Tick, StreamPriority, None, Seq)` + 主机发号 → ADR-009 **Amendment J**;
>    **Q-2** 起货溢出 = CompleteTick 当场由 20 从 Craft 事件派生折叠 `DropSpawned`(18 不写流;20 前置 6b);
>    **Q-3/OQ-18-1** `Recipe.owner` 显式字段回 21a(**D-21-30**,AC-21a-66;R-18-B);
>    **Q-4/OQ-18-3** 单炉、不可取消(跨玩家并发允许;器具互斥剥出 `OQ-18-8` 归 24)。
>    另有 R-18-A 点火时机升格(creative-director 判词直接并入,非用户裁定)。
> ② 首轮 11 阻断(7 条同根「载荷三缺」)的落点:BL-1 三流键 · BL-2 溢出容量 · BL-3 子集判据 · BL-4 并发/取消 ·
>    BL-5 `output_instance_ids[]`+`tool_cell`+`actor_id`(载荷三位) · BL-6 EnvMod 钳制归属(**D-21-31**) ·
>    BL-7 支柱挂载 · BL-8 配方可知性 · BL-9 守恒律(**D-21-32**,修法 = 逐条同形极值式 **AC-21a-65**) ·
>    BL-10 音频发射方(`sfx_process_*`,44:697 改判 18) · BL-11 AC 全表可执行化(25 条:BLOCKING 20 / ADVISORY 1 / EXTERNAL·BLOCKED-BY 4)。
> ③ **二轮须重点抽查的易错处**:AC-18-03 的 spy 恒等断言(在 21a `SkillLevel` newtype = **D-21-33** 落地前不得漏测,
>    也不得误判为类型断言)、AC-18-18/19 的 BLOCKED-BY 标注(21a owner 字段/守恒实现均未落地 ⇒ 不得记绿,
>    同「借来的绿」失效模式)、边例「守恒律被击穿」行(原稿「不可达」已撤,以反例 `w_in=10/w_out=6/QM=1.5/qty=1` 为准)、
>    `CompleteTick` 的墙钟 vs tick 口径(AC-18-21 只断 18 侧输出,渲染具名形态 = `OQ-18-9` 不影响本条)。
> ④ 涟漪六件已全部落盘:`item-database.md`(D-21-28 结案 + 30/31/32/33 + AC-65/66) ·
>    `inventory-and-items.md`(前置 2/6 认领 + 新增 6b + `mint` 例外注) · `time-and-weather.md`(AC-5-19 收窄注) ·
>    `audio-system.md`(`sfx_process_*` 发射方改判 + 上游 cue 表 18 行) · `skeuomorphic-ui.md`(真空清单 +O-18-R6 接缝行) ·
>    `adr-009`(**Amendment J** 全文) + `entities.yaml`(D-21-28 结案注) + `systems-index.md`(row 18 / §11) +
>    `tr-registry.yaml`(TR-processing 003/004/005/007/015 翻转 + 其余同步 note) + `traceability-index.md`(§19 + 汇总 + 变更历史行)。
> ⑤ **重开触发条件**(免二轮已裁,权威文本在 `reviews/processing-review-log.md`):四者任一 —— (a) `OQ-18-4` 内容轮改写 `duration_ticks` 量级 /
>    链深定义;(b) `OQ-18-7` 在 ADR-001 窄修订轮被裁定为「客户端逐帧上行」而非聚合意图;(c) 21a 未落 D-21-30/31/32
>    而 18 侧 AC-18-18/19 被记绿;(d) `OQ-18-8`(24 的器具互斥)与 18 规则八「跨玩家并发允许」相撞时改写并发模型表。

<!-- CONSISTENCY-CHECK: 2026-09-19 | GDDs checked: 31 | Conflicts found: 0 | Report: docs/consistency-report-2026-09-19.md -->

## ✅ 上一任务(2026-09-19 · 17 采集的评审轮 —— 已结案:用户裁定 [B] 接受修订、免二轮 ⇒ Approved)

**状态 = ✅ Approved(2026-09-19)** —— 首轮 `MAJOR REVISION NEEDED`(scope **L**,边界偏 XL · **7 根因**);
用户先选 [A] 现在修订 + 4 项分组裁定,**当日全量修订落盘(含一条 ADR 修订 + 涟漪六件)** → 用户裁定 **[B] 免二轮** ⇒
**✅ Approved**。全文见 `reviews/foraging-review-log.md`。

**CD 判词**:锚点问题「幻想是否交付」= **否**。定位裁定:「『薄系统、无新机制』是首动词的**正确**支柱邻近最小值;
错的是由此推出『无新机制 ⇒ 无新契约』」。

- **七条根因(两条为新变体)**:**P0-1「避涟漪式改判」(新)** —— 为少改 ADR 而孤立 `ResourceHarvested` + 静默改判已 Accepted 的 ADR-009 §二 + 丢失自身所需事实;**P0-2「外抛即结案」(第 4 次复发)** —— ADR-001 无上行意图通道,10/20/4/18 均已登记,17 为零;**P0-3「幻想代持」(新)** —— 17 只拥有第 3 层(隐性熟练度),第 1 层归 21a+42、第 2 层归 18;P0-4 定点域未闭合 · P0-5 借来的绿 + 记账面 · P0-6 资源点驻留面 · P0-7 呈现事实 + 拒绝反馈不可分辨。
- **四项用户裁定(均照准,已落盘)**:① [A] 现在就修订(含全部阻断);② **[B] 复活 `ResourceHarvested`**(→ ADR-009 **Amendment K**);③ 幻想第 1 层收口 = **登记 42 的阻断呈现规格**(补可读**采前**线索);④ ADR-001 欠债 = **登记并并批**(与 `OQ-10-9` / 20-BL-4 同一修订)。
- **核心契约(采集一次动作 = 三条世界流事件)**:`ResourceHarvested { instance_id, node_id, gather_seq, qty, out_quality }`(**17 的 Kind**,承载采集事实) · `DropSpawned { instance_id, spawn_anchor = node.cell, item_key, qty }`(身份出生) · `DropClaimed { instance_id, claimer }`(立即归属)。**`raw_quality` 不进流**(可重算),**`out_quality` 必须进流**;**`gather_seq` 只计 `ResourceHarvested`**。
- **幻觉层重写**:17 拥有第 3 层 + **推断回路**(药 → 18 → 服 → 见效 → 逆推品级低)+ 三必要条件 + `min(各输入 quality)` 链断前置(`OQ-17-12`);另两层显式 **EXTERNAL**。
- **涟漪六件(全部落盘)**:`adr-009`(Amendment K:§三 骨架注 · §六 **有界性就地修正** —— 资源点可再生 ⇒ 事件数上界 = 玩家动作率 × 会话时长,**非**资源点数)· `entities.yaml`(+`SimEvent.Kind.ResourceHarvested`)· `inventory-and-items.md`(fold 谓词并入 `ResourceHarvested`,否则 `(item_key, quality)` 堆叠键重建不出 quality;**R11 已闭合**)· `skill-system.md`(novelty 采集行 2→3 补金鸡纳树皮)· `systems-index.md`(row 17 / 517 / 计数块)· `foraging.md` 本体全量重写。
- **修订量**:本体重写 —— 6 规则 · **4 公式**(F-17-1 品级抽取整数化 `U = h >> 16` + CDF 严格 `<` + `quality_distribution[]` 定 `int`;F-17-2 + `GATHER_MUL_CAP`;**F-17-3 形状改判** 拆「滑窗再生」+「`YieldDecay` 单调衰减」;F-17-3b 边界探针)· **27 AC 四组**(BLOCKING / ADVISORY / **EXTERNAL 3**;`AC-17-02` = `EXTERNAL · BLOCKED-BY-ADR-012`;`AC-17-05` 改**反射断言**)· **12 OQ** · §Edge Cases 补**失败收敛**(三命名失败 × 三载体)。
- **⚠️ 免二轮 = 显式风险接受,不是「已核对」**。重开触发条件四者任一(权威 = `reviews/foraging-review-log.md`):① `OQ-17-1`(深水线 `Capacity` / `RegrowWindow` / `SEASON_MULT[]` **值**)裁定落地;② `OQ-17-6`(ADR-001 意图通道)由 45 的 GDD 轮裁定(与 `OQ-10-9` / 20-BL-4 同批);③ 42 的**采前线索呈现规格**实现期证明不可读;④ `OQ-17-12`(`min(quality)` 链断)在 21a 侧被证伪。
- **二轮抽查点(如重开)**:`AC-17-02` EXTERNAL 不得记绿 · `AC-17-05` 须确为反射断言(非 grep)· `YieldDecay` 单调非增是否在 `DECAY_FLOOR` 处被误写为「可恢复」· `ResourceHarvested` 与 20 的 fold 谓词是否**双向**同字段名。

---

## ✅ 已结案(2026-09-19 · 系统 20 库存与物品 —— **用户裁定「接受修订、免三轮」⇒ Approved**)

**进展链**:首轮(2026-09-18)= NEEDS REVISION(7 BL)→ 修订落盘(2026-09-19)→
**二轮 `/design-review`(2026-09-19)= 🔴 MAJOR REVISION NEEDED(12 BL:BL-21…BL-32)** →
二轮修订全部落盘 → **用户裁定 [B] 接受修订、直接标 Approved(免三轮)** ⇒ ✅ **结案**。
CD 判词:「有契约面 ≠ 有可验证的契约面」;头号结构性结论 = **「外抛即结案」= 失效模式第 3 变体**。
全文见 `reviews/inventory-and-items-review-log.md`(结案段在末)。

**⭐ 四项落地裁定(2026-09-19,均照准)**:
① **BL-27 = 「声明单调契约,不新增形状」**(AC-20-24 自证半边 + R9 ④ 归 42;「药箱视觉容量」判为不存在的量);
② **BL-28 = 「P0 `build_part` 的 `quality` 恒 1」**(规则三;P1a 范围债);
③ **BL-23 = 取 ④**(消耗意图直接点名实例集 `[(instance_id, qty)]`,id 升序降为兜底;落地共编义务 = **R14**,与 11 深水区同批);
④ **R13 = 「暂不裁,挂 21a 下一轮」**(对 21a D-21-16「P0 可空」的「最小非空」改判请求保留、不即时生效;
   R13 行 owner 已改「21a 下一轮」;若裁方向不同 ⇒ 触发重开条件 ②)。

**⚠️ 免三轮 = 显式风险接受,不是「已核对」**。重开触发条件四者任一:① R14 载荷形状在 11/45/21 侧被拒或改判;
② R13 在 21a 轮被裁方向不同;③ `OQ-20-10` 被 R2 按单事件定稿(跨栈截断成真);④ 三轮/实现期判 AC-20-24/25 不可判伪。

**文档终态**:`inventory-and-items.md` 880 行 · AC 25 条(7 条挂 BLOCKED-BY)· OQ 10 行 · R 14 行(含 AC 依赖列)· 前置 7 件。
`systems-index.md` 已同步(row 20 / §7 / §10 / §11;已 Approved = 24 项)。**未随结案消解的残留全部在 R1–R14 / OQ 名下**,
随各件下一轮落地;「外抛即结案」判据回写 design-review skill 清单一项**仍只是登记建议,未代改**。

- **评审积压(下一节候选,均须新会话)**:**待二轮 = 4 交互 / 11 处方**(11 的深水区轮须同批核 R14 载荷形状);
  **待首轮 = 17 采集 / 18 炮制(本会话修订落盘,待二轮)/ 29 死亡复活**。~~53~~ 已 ✅ Approved(2026-09-19 免二轮)。

---

## ✅ 上一任务(2026-09-19 · 53 的评审轮 —— 已结案:用户裁定 [A] 免二轮 ⇒ Approved)

**状态 = ✅ Approved(2026-09-19)** —— 首轮 `MAJOR REVISION NEEDED`(scope **L** · 6 阻断);
用户先选 [A] 现在修订 + 5 项分组裁定,**当日全部落盘** → 用户裁定 **[A] 免二轮结案** ⇒
**✅ Approved**。

**CD 判词**:「**裁量者失据**」—— 53 拿 9 的结局冒充自己的裁决,而自己的裁量函数还是个黑箱;
「谁不判」立住了(37 侧),「判什么、拿什么比」没立住。

- **五项用户裁定(均照准,已落盘)**:① **🔴 OQ-53-4 双轴拆开** —— 结局轴归 **9**(病人活/走由 9 病程结算,**53 只读、永不上裁决面**);回响轴归 **53**(判据 = 判断误差的物质痕迹:后遗/复现/试药史;真值源 = `D` 病程投影/`treatable_by` + `C` 处置适切性,**非** correctness 字段;**「误诊自愈」= 无物质痕迹 ⇒ 不回响**)· ② **⚠️ OQ-53-8 药代价 P0 开、限定伤/后遗**(永不作死亡代价 —— 致死者医者杀生打穿支柱二;11 的 contraindications 有牙齿)· ③ **OQ-53-9 试药史形状本轮定**(同一 patient 连续 `DrugTreatmentApplied` 无 `JudgmentRecorded`;数值归平衡轮)· ④ **OQ-53-7 新增世界流 Kind `ConsequenceResolved`**(无 case_id;**OQ-53-1 标 P1a**)· ⑤ **37 NOT-RUN 只列 AC-53-05/06 成立**(AC-53-04/07/08 须复核;37 侧 AC-37-20/28/31 载体已建、AC-37-25 ① 已核②待复核)。
- **六项阻断分类**:B1(判据空洞 → 双轴拆除)/ B2(死亡无触发器 → 触发集不含死亡 + 支柱二锚点)/ B3(OQ-53-8/9 到期 → 两裁落盘)/ B4(枚举混层 → Region 移出 P0 · F-53.3 标 P1a · 禁「不归纳即免疫」)/ B5(AC 不可执行 → 全表补级别+载体 · 置换不变性 · case_id/patient_id 分 · 反射扫描 · +AC-53-15)/ B6(37 NOT-RUN 翻案过度 → 载体已建≠可跑)。
- **修订量**:F-53.1 重写(双轴枚举 + 真值源登记 + 示例)· 规则二/六/七/八 ⭑(触发集不含死亡 · 支柱二锚点 · 可复盘缝 · ≥1 在场者前置 · ConsequenceResolved Kind)· OQ-53-4/8/9/7 已裁 + **OQ-53-1 标 P1a** · AC 14→15(+AC-53-15 黄金夹具)- 修复 04/11 Gherkin。
- **涟漪 = 全量落盘**:`entities.yaml`(+`SimEvent.Kind.ConsequenceResolved` 世界流 · 无 case_id)**· `case-system.md`**(AC-37-20/28/31 补「载体已建」· AC-37-25 注补「① 已核 / ② 待复核」)**· `systems-index.md`**(row 53 重写 · §9 #53 注 + 2026-09-19)· **待 52 侧**(`random-events.md` 划界三案链不触发 52 世界级事件,**未越界直改**)。
- **⚠️ 免二轮 = 显式风险接受,不是「已核对」**。重开触发条件四者任一(权威 = `reviews/medical-consequences-review-log.md`):① 双轴语义在实现期被折叠回单轴(var 53 重上裁决面 / 回响读 correctness 字段);② OQ-53-8 代价侧被篡改为致死;③ AC-53-04/07/08 的复核被跳过而 37 NOT-RUN 被翻;④ OQ-53-7(Kind)在 45 网络层侧被改判走可靠通道。
- **✋ 45 侧义务登记(未越界直改)**:**ADR-001 窄修订批**(45 GDD 轮,OQ-10-9 同批):`EmergencyAttempt` 判定输入须可靠通道 —— 与 53 的 `ConsequenceResolved` 同批评估。
- **未结(不阻塞,带至实现期)**:`OQ-53-2`(DelayTable 值 → 平衡轮)· `OQ-53-3`(`t_bucket` → P1a)· `OQ-53-5`(Outcome 取值集 → 数值轮)· `OQ-53-6`(在场者载体 → 实现前)· `OQ-53-1`(疫区载体 → P1a)。

---

## 上一节(系统 53 医疗后果与责任 · **✅ Approved 2026-09-19 免二轮结案**)

**首个由首轮评审直接追加世界流 Kind 的系统**(ConsequenceResolved)。**五大用户裁定均照准**、
六阻断全落盘(见上方 ✅ 上一任务)。**涟漪全量落盘**:entities.yaml / case-system.md / systems-index.md。
**结案方式用户裁定 [A] 免二轮 ⇒ Approved**。**未结(不阻塞)**:OQ-53-2/3/5/6/1 · 52 划界待补 · AC-53-04/07/08 待 53 修版后复核。

---

## 上一节(系统 48 教学与引导 · **✅ Approved 2026-09-19 免二轮结案**)

**状态 = ✅ **Approved(2026-09-19)**:用户裁定 [A] 免二轮 ⇒ 结案。后续处理已全部落盘:**review-log 结案段回填** · `systems-index` row 48 = ✅ Approved(9 上游扩展:42/37/3/39/6/4/44/7b/13)。

## 上一节(系统 39 脉案 · **✅ Approved 2026-09-19 免二轮结案**)

**状态 = ✅ **Approved(2026-09-19)** —— 首轮 = MAJOR REVISION NEEDED(scope L)· 12 条阻断 + 7 处涟漪当日修订落盘 → 用户裁定 [B] 接受修订、免二轮结案。**

> **⭐ 免二轮 = 显式风险接受,不是「已核对」。** 重开触发条件五者任一(权威 = `reviews/casebook-review-log.md` 顶部):
> ① ADR-008 作者轴在 37/45 侧兑现时被改判;② ADR-001 窄修订与 39 登记的「主机 Append + 发号 `Seq`」假设冲突;
> ③ `CasesOf` 具名接口被证伪;④ `OQ-39-6`/`OQ-39-2` 表现层外推在 `/ux-design` 前被当已裁使用;⑤ `AC-39-05` 载体未兑现。

- **CD 判词**:39 被写成「呈现层」文档,却实际承担**存档层**系统的职责 —— 「持久化 + 分册」在
  8 / 37 / ADR-008 三处权威件里都被显式推给它,而 ADR-010 义务表无 39 行、存档布局无 39 槽。
  **身份未裁之前,逐条改 AC 是徒劳的。** 12 条阻断**全部是身份 / 契约 / 判据三类**,呈现内容
  (纸 · 翻页 · 零提示 · 零归纳)**无一被推翻**。
- **五项用户裁定(均照准)**:① **层籍 = [C] 升格为分册态层**(三流之外第四类数据的持有者;ADR-010
  §三 补义务 13 + §一 补「分册态段」槽)· ② **移动 = [C] 不抑制**(OQ-39-4;维持 2 的 EC-2-8,1 白名单不加 39)·
  ③ **`player_id` 发号 = [A] 只登记契约,机制归 45**(与 ADR-001 窄修订同批;P0 单机 = 常量 0)·
  ④ **判断分册语义 = [A] ADR-008 窄修订:载荷补作者轴**(`J(c,p)` 按作者分轴;痕 = 流读时投影,零独立存储)·
  ⑤ **相机请求方 = [A] 锁 39 唯一请求方**(8 不再发;`AC-39-07` 改幂等 + 转场计数口径)。
- **CD 下调两处专家主张**:「三重存储矛盾」实为两重 + 一处措辞(采 CD 口径)· 相机双请求方
  **功能可接受(同档幂等)、坏的只是判据**(合并为 BL-5)。
- **7 处涟漪 = 全量落盘**:`adr-010`(义务 13 + 分册态段槽)· `adr-008`(作者轴 + `J(c,p)` + 有界性重证)·
  `camera-and-viewpoint.md`(唯一请求方共 7 处)· `casebook.md`(12 阻断 + 9 推荐 + AC 级列 + 载体欠账表 +
  新 `OQ-39-6`)· `diagnosis-system.md`(RECHECK_WINDOW 口径 + 逐位一致收窄)·
  `case-system.md`(陈旧符号修复:39/53/48 无 GDD → 已成稿)· `systems-index.md`(row 39 全量重写 + Approved)。
- **落盘量**:`casebook.md` **493 行 · 14 AC(全补级列)· 6 OQ**;新建 `design/gdd/reviews/casebook-review-log.md`
  (结案段 + 首轮段,含重开触发条件 5 条 + 二轮原核 5 项 + 涟漪表)。
- **残留未结项(不阻塞,随结案带入实现期)**:`OQ-39-2`(纸页容量,外推 `/ux-design`)· `OQ-39-5`(VR 形态,P1a)·
  `OQ-39-6`(落笔输入,与 11 同批)· `player_id` 发号 → 45 · `AC-48-09` → 48 · `CasesOf` 具名接口 → 37 修订轮。
- **低优先登记(此前会话遗留)**:20 的 `AC-20-03` 编号撞车(`inventory-and-items.md:479` 重建逐位 vs
  ADR-020 反射判据)建议并入下一次涟漪批订正;qa-lead 对 20 评审日志两条 superseding items 未回填。

## 🔴 上一任务(2026-09-18 · 4 的评审轮 —— ✅ 本轮全部落盘完毕)

- **CD 判词**:39 被写成「呈现层」文档,却实际承担**存档层**系统的职责 —— 「持久化 + 分册」在
  8 / 37 / ADR-008 三处权威件里都被显式推给它,而 ADR-010 义务表无 39 行、存档布局无 39 槽。
  **身份未裁之前,逐条改 AC 是徒劳的。** 12 条阻断**全部是身份 / 契约 / 判据三类**,呈现内容
  (纸 · 翻页 · 零提示 · 零归纳)**无一被推翻**。
- **五项用户裁定(均照准)**:① **层籍 = [C] 升格为分册态层**(三流之外第四类数据的持有者;ADR-010
  §三 补义务 13 + §一 补「分册态段」槽)· ② **移动 = [C] 不抑制**(OQ-39-4;维持 2 的 EC-2-8,1 白名单不加 39)·
  ③ **`player_id` 发号 = [A] 只登记契约,机制归 45**(与 ADR-001 窄修订同批;P0 单机 = 常量 0)·
  ④ **判断分册语义 = [A] ADR-008 窄修订:载荷补作者轴**(`J(c,p)` 按作者分轴;痕 = 流读时投影,零独立存储)·
  ⑤ **相机请求方 = [A] 锁 39 唯一请求方**(8 不再发;`AC-39-07` 改幂等 + 转场计数口径)。
- **CD 下调两处专家主张**:「三重存储矛盾」实为两重 + 一处措辞(采 CD 口径)· 相机双请求方
  **功能可接受(同档幂等)、坏的只是判据**(合并为 BL-5)。
- **7 处涟漪 = 全量落盘**:`adr-010`(义务 13 + 分册态段槽)· `adr-008`(作者轴 + `J(c,p)` + 有界性重证)·
  `camera-and-viewpoint.md`(唯一请求方共 7 处)· `casebook.md`(12 阻断 + 9 推荐 + AC 级列 + 载体欠账表 +
  新 `OQ-39-6`)· `diagnosis-system.md`(RECHECK_WINDOW 口径 + 逐位一致收窄)·
  `case-system.md`(陈旧符号修复:39/53/48 无 GDD → 已成稿)· `systems-index.md`(row 39 全量重写)。
- **落盘量**:`casebook.md` **493 行 · 14 AC(全补级列)· 6 OQ**;新建 `design/gdd/reviews/casebook-review-log.md`
  (首条,含二轮必查 5 项 + 被下调的专家主张 + 涟漪表)。
- **转 Approved 的三个硬前置**(不在 39 内解决):① **ADR-008 作者轴在 37 / 45 侧接受**(载荷形状 + `J(c,p)` +
  有界性重证)· ② **ADR-001 窄修订**(落笔意图上行通道,与 `OQ-10-9` / `OQ-4-10` 同批,P1b 前)·
  ③ **37 补 `CasesOf` 具名接口**(否则 F-39.1 仍是「引用却无登记」)。
- **跨系统债**:`player_id` 发号 → 45 的 GDD 轮 · `OQ-39-6` 落笔输入 → 与 11 同批 · `AC-48-09` 同类契约 → 48 侧登记。
- **低优先登记(此前会话遗留)**:20 的 `AC-20-03` 编号撞车(`inventory-and-items.md:479` 重建逐位 vs
  ADR-020 反射判据)建议并入下一次涟漪批订正;qa-lead 对 20 评审日志两条 superseding items 未回填。

## 🔴 上一任务(2026-09-18 · 4 的评审轮 —— ✅ 本轮全部落盘完毕)

**状态 = 🟡 In Review · 首轮 = MAJOR REVISION NEEDED(scope L)· 修订与涟漪均已当日落盘 · 待二轮(⬛须新会话⬛)。**

- **四条用户裁定(均照准)**:① **改幻想不改架构** —— 「身位即光标」(瞄准 = 移动,不是转头;`F-4.1` 三键无朝向项,
  朝向进流须开 ADR-020/009 修订,**不采纳**)· ② **动作词表归 8** —— `OQ-4-1` 的 ✅ **撤回**,登记 `D-8-11` ·
  ③ **`K_difficulty` 驳回**,退回 11 / 30 ⇒ **成长出口现无主** = `OQ-11-13` ·
  ④ **模态门 = 42 新立只读「模态开集」**(`IModalState`)—— `SetFocusGate` / `IsFocusActive` **都是别的旗标**。
- **⭐ 本轮新命名三类失效模式**(可复用于后续评审):
  **判据空转**(断言恒真,如 `Kind(c) ≠ Player` 是空谓词)· **假闭合**(单方宣布别人的债已还)·
  **边沿产物 vs 纯函数身份**(每帧 `Request` ⇒ `Seq`/`Tick` 依赖帧时序,破 `ADR-016 §三` 事件率上界)。
- **程序集归属改判**(唯一动到「身份声明」的一项):原稿用「零 `Append`」= **写侧判据**;本仓判据是**引用集 / 读侧**
  (`adr-017` §二)⇒ **4 住边界层**,继承 13 的豁免 ⇒ `AC-4-14` 收窄为只覆盖 `F-4.1`,`Accept` 另立 `AC-4-20`。
- **7 处涟漪 = 全量落盘**(用户裁定「全部 7 处」):`ADR-013 §十 Amendment A` · `skeuomorphic-ui.md`(⭐注 + 42→4 行)·
  `player-controller-and-movement.md`(`AC-1-23` 下 🔴 块,**是待裁义务不是已改机制**)· `world-and-ecozones.md`
  (三义务 + `EC-6-2` 🔶 **刻意不宣告闭合**)· `diagnosis-system.md`(`D-8-11`)· `prescription-and-medication.md`
  (`OQ-11-13` + 来源行 🔴)· `foraging.md`(上游 4 行 + **注④** + 规则六「不经过 42 ≠ 不经过 4」)·
  `systems-index.md`(row 4 重写 · §6 行 11 `M → L` · 积压清单加 4 的二轮注)。
- ⚠️ **落盘时的一处 ID 冲突**:`OQ-11-11` 已被 `SkillGrown` 的 30 侧登记占用 ⇒ 另立 **`OQ-11-13`**,
  4 侧三处引用已回改。**教训:转记债务前必须 grep 目标表,不能按名称占用。**
- **落盘量**:`interaction-system.md` **1156 行 · 22 AC · 16 OQ · §数据契约 4-DC(10 项 kind 闭集 + 6 校验)**;
  新建 `design/gdd/reviews/interaction-system-review-log.md`(首条,含二轮必查 5 项 + 4 处被下调的专家主张)。
- **转 Approved 的三个硬前置**(不在 4 内解决):① **8 的动作词表行**(`OQ-4-1`)· ② **42 的 `IModalState` 落地**
  (契约本体已在 ADR-013,42 侧 AC 待其二轮)· ③ `OQ-4-11` / `4-6` / `4-7` 至少给方向。
  **跨系统债**:`OQ-4-10` → ADR-001 窄修订(与 `OQ-10-9` + 20 的 BL-4 **同批**)· `OQ-4-13` → 系统 1(4 的 `AC-4-19` 在 1 落地前**必红**)· `OQ-4-12` → 22 导出契约。

## 🔴 上一任务(2026-09-18 · 20 的评审轮)

**状态 = 🟡 In Review · 首轮裁 NEEDS REVISION —— 用户裁定「新会话修订 20」,本会话只交文献。**
**⇒ 修订须在⬛新会话⬛进行**,本会话上下文已用于 6 路专家 + 大量实证核证。

- **裁决**:`NEEDS REVISION · Scope M`(本体仍 S;**上游涟漪 L** —— 触及 5 份 ADR + 3 份未写 GDD)。
  6 路专家并行 haiku(game-designer · systems-designer · economy-designer · ux-designer · qa-lead ·
  network-programmer)+ creative-director 串行 opus 终裁。
- **⭐ 系统性根因首次命名:「形名失配」—— 名字对上了,形状没对上。**
  20 逐条核对了 Kind / 接口 / 通道的**名字**是否存在(而这些名字**确实都在**),却**从未核对载荷形状能否装下
  它要装的事实**。四条 BLOCKING 是同一动作的四个实例:`IPositionalChannel` 在但载荷无 `instance_id`(BL-4)·
  `DropClaimed`/`DropDespawned` 在但无「进/出容器」位(BL-3)· `DropDespawned` 在但是实例级,装不下「qty 5 只耗 3」(BL-5)·
  `IIdAuthority` 在但签名只有 `PatientId Next()`(BL-1)。
  **这是既有模式「引用却无登记」的姊妹** —— 那条引用了**不存在**的东西,这条引用了**存在**的东西却没验装不装得下。
  **`OQ-10-9`(10 急救动作)是同一根因的第一次发生,20 是第二次。**
  **建议写进评审清单**:凡「复用既有 Kind / 通道」的裁决,必须**逐字段**列出「要承载的事实」与「载荷现字段」。
- **CD 对「薄」的判词(决定裁决等级)**:**薄 ≠ 问题,薄到没有契约面 = 问题**。20 无自有机制**架构上应当如此**
  (发明机制或持第二本账即确定性违约),但它把「不发明机制」推成了「**不定义契约**」——
  它**必须交出的三样**(`fold` 谓词集 / 容器语义 / 读数 DTO)**只留了名字**。
  **纯投影比机制系统更需要精确契约,因为没有机制可以吸收歧义。** ⇒ NEEDS REVISION(**契约面做加法**),
  **不是** MAJOR(不必重估 20 的定位)。药箱 = 42 六屏闭集 **③**(已实证**未偷加第 7 屏**)⇒ 不增屏幕数、不增系统数。
- **7 条 BLOCKING**:BL-1 `instance_id` 生命周期三处互斥(含 `structure_id`/`BakedInitial` 漏算)·
  BL-2 **`fold` 域缺 `StructureRemoved` ⇒ 自己的 `AC-20-03` 必红**(P0 拆除**全额返还**)·
  BL-3 容器契约真空(成员无载体 + 无摆放函数 ⇒ 手柄无路径 + 遍历无上界无翻页,撞 `AC-3-F1a ②`)·
  BL-4 拾取意图装不进第二 QoS 载荷(与 `OQ-10-9` 同根因,须 ADR-001 窄修订)·
  BL-5 用药消耗无量纲(**`D-21-29`**,20 对此**完全沉默**)· BL-6 `CARRY_CAP` **双主**且载重读数**双向甩锅** ·
  BL-7 药箱栈归属(20 裁「世界内道具」vs ADR-013 判 UI Toolkit 平面 ⇒ **手柄可能操作不了**)。
- **🟡 待用户裁(3 项)**:① 药箱栈「世界内道具」= **语义还是字面 world-space**(推荐语义)·
  ② **死亡掉落 = 免费清负重**(`death-and-respawn.md:100-104/216`,29 自认「掉落经济漏洞」;P0 载重轴被下游架空;
  **超 20 权限,须与 29 共同裁**)· ③ **P0 是否保留嵌套容器**(推荐 P0 禁、P1a 放开)。
- **4 类专家夸大已剔除**(勿照抄进 BLOCKING):A「承重接口不存在」(**夸大** —— `adr-010` §五 义务 6 已 Accepted,
  真缺陷是**假绿 ✅ + 三处不同步**)· B「引错权威件」(**归属失准**,真洞在 `structure_id`,已并入 BL-1)·
  L 经济层三条(**多为夸大或已裁** —— 23 已明写「材料经济进 P1a」;真残留仅**死亡掉落**与**旋钮缺上界**两条)·
  K「品级不可辨」(属实但降级 = 断言建在**可空**字段上)。
- **⤴ 转技术线窄修订(非用户裁)**:ADR-001 意图上行第三通道(**与 `OQ-10-9` 同批**)·
  ADR-009/R-2 载荷(`reason` 已登记 + `qty`)· ADR-010 义务 12(`structure_id` 域与扫描集)·
  ADR-005 `:236` 过时注释 · ADR-021 通道(容器成员载体,若裁「追加 Kind」)。
- **⭐ 新建** `design/gdd/reviews/inventory-and-items-review-log.md`(本文件首条 —— **新会话修订的唯一交接件**,含全部实证 grep 与行号)。
- **未回刷**:`systems-index.md` row 20 与 §6 设计序第 8 行仍写「🟡 Draft,待首轮」——
  **用户选择不写台账**(只选了写评审记录)。

---

## 上一节(2026-09-18 · 11 的评审轮)

**状态 = 🟡 修订已落盘,待二轮** —— **与 10 / 42 / 51 的「免二轮」不同,本轮用户未裁定免二轮**
(用户裁定为「**[A] 现在就整体修订,方向问题按 creative-director 推荐先落稿**」)。⇒ 二轮**必须跑**,**须新会话**。

- **裁决**:`MAJOR REVISION NEEDED · Scope L`(5 路专家并行 haiku + creative-director 串行 opus)。
- **6 条根因(5 BLOCKING)**:R-1「引用却无登记」**第 10 次** · R-2「语义挪用 / 劫持」· R-3「记账不对称」·
  R-4「静默失败 / 无门」· R-5「契约空洞」· R-6「载荷归属错挂」。
- **🔴 三处结构性方向 = 🔶 已写入正文但未经用户最终确认**:① **熟练度出口 = 省料 + 解锁**
  (~~药效放大~~ 删 —— `SkillMul` 在 10 的 A6 后是**稳度容差乘子**,11 恒 `Applied` 无合法消费点;`SKILL_MUL_ONE` 是**死符号**)·
  ② **载荷收窄 = `action → 单一 polarity`**(⚠️ **不推翻用户 `OQ-11-1` 裁定**)· ③ **戥子四条边界**(规则十三新立)。
- **涟漪六处已落盘**:`skill-system.md:60` · `item-database.md`(新 **`D-21-29`**)· `systems-index.md`(row 11 重写 + **C5 重新解释**)·
  `medical-consequences.md`(`OQ-53-8`/`OQ-53-9`)· `skeuomorphic-ui.md` · `interaction-system.md`(`K_difficulty` 代入方)。
- **未结**:`OQ-11-1…12` · `D-21-29` · `AC-11-16` = `NOT-RUN` · **相机档位 / 六屏闭集**(BLOCKING,须 39/42/2 联合裁定)。

---

## 上一节(2026-09-18 · 10 的评审轮)

**状态 = ✅ Approved,已结案(2026-09-18 同日 · 用户裁定「接受修订,标 Approved」—— 免二轮)。**
⚠️ **免二轮 = 显式风险接受,不是「已核对」**:二轮从未发生,原「交二轮」条目一律读作「交实现轮 / 用户」;
**重开评审触发条件四条**记在 `reviews/emergency-procedures-review-log.md` 结案条与 `systems-index.md:46` 行末。

- **裁决**:`MAJOR REVISION NEEDED · Scope L`(6 路专家并行 + creative-director 串行终裁)。
  ⚠️ **口径订正**:GDD Status 行原写 `NEEDS REVISION`,已就地标为 `MAJOR` 并留记账注。
- **三项轮内用户裁定**:**[A] 现在改(阻断项一并处理)** · **R-6 拆两层**
  (`Skip ⇒ AppliedWeak` 默认弱一档,`SkipEquivalence` = 49 侧开关可置等值;
  「零惩罚」的锚从「= 满成功」改为「**≥ 失败档**」)· **R-1 取 C 路**
  (聚合意图事件 + **主机 `Judge`** + 本地**预表现**)。
- **七条根因全部落盘**:R-1 判定输入不在三源内(新规则十一 + F-10.5 聚合 + `Judge` 签名收敛)·
  R-2 三处悬空外键(三 Kind 定名 / `half_life_ticks[action]` / F-10.4 单一舍入)·
  R-3 稳度门无指称(增 `edge_ticks[]` + F-10.3b 改相对偏差式)· R-4 跳过不可达(规则六之甲/乙)·
  R-5 键鼠无模拟量 ⇒ 幻想在第一平台不成立(回退形状 `magnitude ≡ MAG_MAX` 走双门)·
  R-6 跳过支配(机制层)· R-7 未兑义务 + 状态陈旧(注⑤ + 规则十二)。
- **计数面**:AC **16 → 29**(24 BLOCKING + 5 ADVISORY,脚本核对)· OQ **7 → 13** · 规则十二 + 六之甲/乙。
- **涟漪六处已落盘**:`entities.yaml`(`SimEvent.Kind` **18 → 21**,`last_updated` 2026-09-18)·
  `adr-009` **Amendment I**(⚠️ **走 §二 域归属表,不进 §三 世界流骨架块** —— 本批是病史流)·
  `disease-simulation.md:173` 9 侧白名单(`O-10-4` 办结)· `adr-011` **Amendment B** 四处(`O-10-5` 办结)·
  `input-system.md`(结案 `OQ-3-5` + 新增 `OQ-10-7` + 四条委派义务标结)·
  `technical-preferences.md`(OQ-25-8 块 + ADR-009 / ADR-011 两条日志条目)·
  `systems-index.md:46` 重写 · **新建** `design/gdd/reviews/emergency-procedures-review-log.md`。
- **⭐ 系统性防线议题(本轮最大产出,待下一次 `/architecture-review`)**:
  「引用却无登记」在 `adr-009` 已**第 5 次**发生 ⇒ 现行防线只有**消费侧兜底**(9 的白名单构建期拒收),
  缺**作者侧 Kind 引用闭包检查**(未注册 Kind → 构建期失败;纯文本 / AST 级,不依赖引擎,
  可与 ADR-012 CI 门同批)。**五次同型失效 = 已具备升 BLOCKING 的经验依据。不在 ADR 打补丁。**
- **未闭合(交用户 / 其他轮次)**:`OQ-10-4`(P0 两动作是哪两个)· `OQ-10-6`(`EmergencyAction` 枚举归属 =
  **数据契约前置**)· `OQ-10-5`(外力打断)· `OQ-10-9`(**判定输入的 QoS 会丢 ⇒ 须 ADR-001 窄修订**,归 45 轮)·
  `OQ-10-10`(`TICK_PERIOD` 无量纲,与 `OQ-8` / `OQ-25-8` 同批)· `OQ-10-11` ·
  **`OQ-10-12`(原型门 —— 全案唯一需要手感的系统却无原型门)**· `OQ-10-13`(42 / 44 须各自确认「过程物理量」呈现)·
  `O-10-1`→42 · `O-10-2`→48 · **全部数值冻结**。
- **落盘事故(已修复;该处的二轮核对已随免二轮放弃)**:一次 F-10.2 重写的 `old_string` 越界吞掉了 F-10.5 表(静默删除),
  已重插 ⇒ **F-10.5 现物理位于 F-10.4 与 F-10.6 之间**,编号与文件顺序轻微错位,未再重排(风险 > 收益)。
- **CD 声称写的 memory 文件 `feedback-blocking-vs-ditem-criterion.md` 经核**`ls` **不存在**
  (子代理报「已写」而实际未写 —— 承既有 memory 规则,已核对)。

### 评审积压现状(承上一批)

**已成稿待首轮评审**:4 交互 · 5 时间与天气 · 11 处方 · 18 炮制 · 20 库存(10 本轮已跑)。
**待二轮**:25(✅ 已结案 2026-09-18)· 27(✅ 重开评审已结案)· 10(**✅ 已结案 2026-09-18,免二轮**)⇒ **无待二轮项**。
**待复审(Needs Revision)**:30 技能与熟练度 · 3 输入与设备(新增 `OQ-10-7` 的 Amendment 义务)。

---

## 上一批(2026-09-18 收尾批 · **P0 的 31 项至此全部有 GDD**)

**用户裁定:`「开下一批6项,一次性写完」`** —— 六份**全部成稿落盘**,状态 = 🟡 **Draft**。
**这是 P0 设计序的**最后一批** —— `systems-index.md` §11 现记「P0 已设计 **31 / 31** … **剩 0 项**」。

### 为什么这批可以最后写(承上一批的判据)

- **全部上游已 Approved** —— 9 / 21a / 6 / 52 / 24 / 42 / 44 / 7a / 1 / 2 / 3 均已结案;
- **六份之间无环** —— 20 是 4 / 10 / 11 / 18 的共同下游(不是上游),故写 20 时其反向义务已可回填;
- **六份互为注①/注②的回填端** —— 5 ↔ 18 的 `EnvMod` 边、11 ↔ 21a 的 F5 边、10 ↔ 3 的幅度通道边。

### 六份 GDD(均 `design/gdd/`)

| # | 文件 | 行数 | 承重落点 | AC / OQ |
| --- | --- | --- | --- | --- |
| **4** | `interaction-system.md` | 585 | **纯目标选择器**三条铁律:① 只选目标不结算(路由给 20/6/37/8/24/23/18);② 候选集全是**派生态**(世界流锚格 + 6 烘焙逻辑层 + 13 `IPresentPatients`);③ **不写三流**(零 `Append`)。目标 = 确定性全序(`d∞` 切比雪夫 → `KindPriority` → 稳定 id)。**核心交付**:兑现 `O-6-7` / 闭合 `EC-6-2` —— 「已发现」触发 = **玩家主动交互**(非碰撞进入) | 15 / 9 |
| **5** | `time-and-weather.md` | 505 | **世界的节拍器**。第一铁律 = 与 ADR-005 的分界:`ITickProvider` 是全案 tick 唯一来源,**5 只拥有 tick 的语义**。天气 = `(WorldSeed, tick, EcozoneOf(cell))` 纯函数(**派生态**,不进流)。`TICKS_PER_DAY` **单一定义点在 5**,52 引用(兑现 52 的 DC-3)。**规则八:天气不影响移动 —— P0 不启用** | 15 / 7 |
| **10** | `emergency-procedures.md` | 530 | P0 唯一需要「手感」的系统(`L_input < 50 ms`)。**结清 `OQ-3-5`** —— `EmergencyReading` 增**独立模拟量通道** `magnitude`(浮点只活在 3 的手感层,交出的是全整数)。判定全归 10(3 不判、不构造 `SimEvent`)。**跳过路径落定**:`Skip ⇒ JudgeResult = Applied`(≠ `Missed`)= 无障碍入场券 | 15 / 7 |
| **11** | `prescription-and-medication.md` | 586 | 三条铁律:① **病名不给 11**(8 与 11 刻意无数据流 —— 支柱一在代码里的物理形状);② 11 不发明结算(药值全来自 21a `drug_profile`);③ 11 不判对错(离牌门归 9)。**兑现 `D-21-11`** —— 品级 → 时间轴作用点的求值点落 **F-11.2**,11 是 21a F5 的**唯一调用点**。新增两条跨文档硬门:polarity 双表构建期交叉校验(AC-11-07)+ `single_dose_max` 烘焙期派生常量 | 15 / 9 |
| **18** | `processing.md` | 451 | 核心纪律 = **「不发明结算」** —— F1 唯一求解器住 21a(`item-database.md:450`),18 只**筛子集 / 供料 / 调 F1 / 交 20 执行**。**刻意零新数学**(F-18.1 只是调用契约)。`ActualConsumed` 是运行期派生量,随 `Craft` 落世界流 | 15 / 6 |
| **20** | `inventory-and-items.md` | 476 | **不是第二真源** —— 库存 = 世界流纯投影 `InventoryOf(player) = fold(世界流, 谓词 ∈ {DropClaimed, 消耗, 转移})`。`instance_id` 铸造权 = 主机唯一(兑现 `D-21-27`)。**载重呈现 = 拟物器具(药箱满溢感)** —— 机制侧只有一条布尔 `CanCarry`。**它是上一批 17 / 29 / 39 三份的共同上游回填端** | 15 / 5 |

### 已在 `systems-index.md` / `tr-registry.yaml` / `traceability-index.md` 落盘的更新

- **§11 P0 队列**:「剩 6 项」→ **「剩 0 项」**;「P0 已设计 25/31」→ **「31 / 31」**;
- **六行状态** → 🟡 Draft(待首轮评审);
- **`tr-registry.yaml`**:新增 **六条 `system:` slug**(`interaction` / `timeweather` / `emergency` / `prescription` / `processing` / `inventory`)—— **ID 增补 102 条(283 → 385)**,状态 = **61 ✅ / 12 ⚠️ / 29 ❌**(按代生约定,同 25 / 27 / 13 的法 —— 六份**均无单一权威 ADR**);
- **`traceability-index.md`**:新增 **§15–§20** 六节 + 汇总表六行 + **合计 385** + 优先修复清单增补 + 一条变更历史。
- **2026-09-18 六条硬前置裁定后**:上表 6 项由 ❌ 迁出 ⇒ **实测总数 = 385(✅ 245 · ⚠️ 40 · ❌ 100)** —— `tr-registry.yaml` 六条状态翻转 + `traceability-index.md` 汇总/明细/优先清单/变更历史同批回填。

### ✅ 六条 🔴 硬前置 —— 已于 2026-09-18 全部裁定并落盘(4 ✅ 结清 · 2 ⚠️ 形状定值未定)

| OQ | 裁定 | 落点 | 结清状态 |
| --- | --- | --- | --- |
| **`OQ-10-1`** | **`ResultMul[Missed] = 0.25`(非零 —— 「失败也留一笔」)** —— 用户**覆盖**了推荐的归零 | `emergency-procedures.md` F-10.4 + 值表 + **AC-10-16** | ✅ 已结清(代价已明写:Must-hit 紧张感下降) |
| **`OQ-11-1`** | **9 的病种注册表 = `polarity` 唯一真源**;11 = 镜像视图 + 构建期硬校验 | `prescription-and-medication.md` 规则五 + **AC-11-07** | ✅ 已结清(备选「真源归 11」否决,结构性,须另开 ADR) |
| **`OQ-20-1`** | **全部复用既有 Kind**(消耗走 `DropDespawned{reason}`;转移走 `DropSpawned⟶DropClaimed`;**零新增 Kind**) | `inventory-and-items.md` 规则一 + **AC-20-16/17** | ✅ 已结清(⇒ **零 ADR 涟漪**;残留 = R-2 须给 `DropDespawned` 补 `reason` 枚举) |
| **`OQ-18-5`** | **发起即落流 —— 完成 = 纯派生**(`Craft` 落点火 tick;存档**无进程态可存**) | `processing.md` 规则八 + **AC-18-16/17** | ✅ 已结清(⚠️ 副作用:**`D-21-28`(Craft 总序键缺失)严重度上升**,归 ADR-005 / 19 / 45) |
| **`OQ-4-1`** | 病人四路由语义**归 8 / 10 的动作词表** | `interaction-system.md` 病人四路由段 | ⚠️ **形状已定,义务未兑** —— **8 的动作词表行仍缺**(10 已落盘);`TR-interaction-015` = partial |
| **`OQ-5-1`** | **逐季乘子表 —— 5 出 `season_index`,17 查 `SEASON_MULT[]`**(表归 17,不归 5) | `time-and-weather.md` F-5.2 + `foraging.md` F-17-3(**涟漪已落**) + AC-5-16/17 + AC-17-16/17 | ⚠️ **形状已定,值未定** —— `SEASON_MULT[]` / `Capacity` / `RegrowWindow` 归数值轮 |

**跨文档涟漪**(已落盘):① `foraging.md` F-17-3 补 `× SEASON_MULT[season_index]` + 两参数回填注;
② `processing.md` 注④(D-21-28 严重度)+ 注⑤(完成零事件);③ 三处 `MUL_ONE` / 字符串分数写法注(ADR-006/014 整数域纪律)。

**台账已回填**:`tr-registry.yaml` 六条 TR 状态翻转 · `traceability-index.md` 汇总表 + §15–§20 明细 + 优先清单 + 变更历史。
**实测总数 = 385 条(✅ 245 · ⚠️ 40 · ❌ 100)** —— 6 项由 ❌ 迁出(4 → ✅ · 2 → ⚠️)。

### ⚠️ 下一步(必须)

- **六份均待首轮 `/design-review`,且须独立会话**(撰稿上下文不得自评);
- **评审起手 = 10 急救动作**(用户已选);**⚠️ `OQ-4-1` 时序前置**:**8 的动作词表须在 4 的首轮评审之前落盘**(10 的已落,GDD 缺的是 8 的行);
- **数值轮(不阻塞评审,须单独一批)** —— `OQ-10-2`(四判定门阈值,含最承重的 `JITTER_MAX`)· `OQ-10-3`(纯键盘的幅度回退)· `OQ-20-2`(`CARRY_CAP`)· `OQ-11-3` / `OQ-11-4`(剂量域)· `OQ-5-3/4/5`(天气分布 / `EnvMod` 映射 / 季节长度)· **`SEASON_MULT[]` / `Capacity` / `RegrowWindow`**(`OQ-5-1` / `OQ-17-1` 的值);
- **残留评审队列**(更早批次,仍待独立会话):25 的二轮 + 27 的重开(同批)· 30 的复审(`Needs Revision`)· `OQ-3-5` ✅ 已结。

### 上一批(2026-09-18:零涟漪批六份 GDD 撰写)

**用户裁定:`「一口气写完,17 采集 · 29 死亡与复活 · 39 脉案 · 48 教学与引导 · 53 医疗后果 · 7b 存档位 UI」`**

### 选择上一批的判据(「零涟漪」= 独立评审产生的修改不外溢)

1. **无反向依赖者** —— 没有任何系统声明依赖这六项(依赖扫描已跑);
2. **全部上游已 Approved** —— 6 / 9 / 7a / 37 / 42 均已结案。

⇒ 这六份可**各自独立评审**,发现的问题**不会反向污染已 Approved 的系统**。

### 六份 GDD(均 `design/gdd/`)

| # | 文件 | 承重落点 | AC / OQ |
| --- | --- | --- | --- |
| **17** | `foraging.md` | 深水线承载者之一(采集衰减);资源点 = **派生态**,复用既有 `DropSpawned`/`DropClaimed`(**零新 Kind**);`instance_id` 主机唯一(**兑现 D-21-27**) | 15 / 5 |
| **29** | `death-and-respawn.md` | 玩家侧死亡(**触发源无关** —— `OQ-25-1` 三路只影响布尔来源);掉落**清单从世界流 / 位置从快照**;**结清 `TR-randomevents-027`**;登记复活传送频次上界(**结清 1 的 `O-3`/`OQ-1-8`**) | 15 / 5 |
| **39** | `casebook.md` | 支柱四的**物理载体**;**结清 `O-12` 的 39 侧**(打开/关闭发 `Casebook`/`Explore`);分册键 = `player_id`;回答 42 的 `OQ-42-5`(独立系统) | 14 / 5 |
| **48** | `tutorial-and-onboarding.md` | **`AC-3-F1a` 第一责任人**(反幻想守门);零浮层,教 = 口述/示范/纸上的图;引导进度 = **世界状态纯函数(零持久化)**;承接 37 的「第一个病例」锚点 | 14 / 6 |
| **53** | `medical-consequences.md` | **水龙头**(31 = 蓄水池);**53 是唯一判对错的系统**;**⭐ 承接 37 的四条 `NOT-RUN` 义务**(AC-37-20/25/28/31 → AC-53-05/06/07/04/08) | 14 / 7 |
| **7b** | `save-slot-ui.md` | 7a **唯一露出面**;兑现 7a **规则十四**的呈现侧(只进不退 / 读档即锁 / 叙事事件上下文);三层分离 7a 逻辑 · 7b 语义 · 42 渲染 | 13 / 5 |

- **上一批的共同上游缺口**:其中 **17 / 29 / 39** 都依赖 **20 库存与物品(未写)** —— 三份均已登记其反向义务,**20 撰写时须回填**(本轮 20 已落盘,回填义务待核)。

### 更早一批(2026-09-18 前半场:3 项评审队列)

**进展:`patient-ai.md`(13)✅ 结案 → `audio-system.md`(44)✅ Approved → `concept-benchmark.md` ✅ Accepted —— 队列清空。**

### ✅ 第 1 项:`patient-ai.md`(13 病人 AI)首轮 `/design-review`(`full`)→ NEEDS REVISION → 全部修订 → **用户裁定 [B] 接受修订、免二轮 → Approved**
- **评审**:8 名 haiku 专家并行(`game-designer` / `systems-designer` / `ai-programmer` / `qa-lead` /
  `ux-designer` / `accessibility-specialist` / `audio-director` / `network-programmer`)+ `creative-director`
  opus 终裁(**串行**)+ `unity-specialist` lean 引擎复核。**Scope L**。
- **9 阻断 + 8 推荐** —— 全为**文档级规格漏洞**(口径过期 / 公式与状态机不同步 / 悬空符号进决策分支 /
  AC 不可验收)。**零机制重裁、零新真源、零新增 Kind**。评审**确认** 13 的架构姿态合格。
- **四大用户裁定**:R1(`signs[]` 只进表现映射、永不进决策)· V2(场外死亡必须留痕,归 52/37)·
  V8(13 只主机运行、客户端不重算;位姿走 ADR-001 第二 QoS,与 ADR-020 §四 对称)· 无障碍(具名
  `AC-13-F1/F2/F3` + 显式 P0 非目标)。
- **最大三处结构修订**:R3 状态机拆两个正交维度(`BehaviorState` × `SessionState`,原六态表三态不可达)·
  R6 新立 F-13.7 逻辑格步进(`acc` 纪律,照搬 27 的 F-27-4 独立一份)· R9 AC 全组重写(18 → **29 条**,
  `[B]` 22 · `[A]` 7,新增 F 组无障碍)。
- **已落盘**:`patient-ai.md`(1192 行,文件头 → Approved)· **新建** `design/gdd/reviews/patient-ai-review-log.md`
  (+ 结案追记)· `systems-index.md` row 49(→ **Approved**)+ §10 已有 GDD 行。
- **⭐ 免二轮 = 显式风险接受,不是「已核对」**。重开触发四条件见评审日志收尾节。
- **残留外向义务 5 条**(不阻断结案,落对应下游 / 实现期门):`O-13-5`(52 `spawn_anchor` 病人路径)·
  `O-13-6`(ADR-016 §九 补 13 行)· `O-13-7`(8/10 会诊接口发出侧)· `O-13-8`(9 `trend` 对账)·
  `O-13-9`(无障碍文档引 `AC-13-F3`)。**新增待解 1 条**:`OQ-13-5`(场外死亡留痕形态)。
  **新增 AC 的 TR-ID 待补录**。

### ✅ 第 2 项:`audio-system.md`(44 音频)首轮 `/design-review`(`full`)→ MAJOR REVISION NEEDED(Scope L · 8 阻断)→ **同日全修并落盘 → 用户裁定接受修订、免二轮 → ✅ Approved**

- **评审**:`--depth full`,8 位 adversarial 专家 + `creative-director` 综合终裁。**Scope L**。
- **核心结构洞察**(CD):「设计对『**不得做什么**』的规格远严于『**必须做什么**』。幻想的主通道
  (门后呼吸)**没有总线、没有学习路径**、被埋在优先级表里。这个反转就是整个裁决。」
- **三条系统性根因**:① 幻想主通道缺载体;② `AudioListener` 单实例约束被误读为**总线数约束**
  (伪前提)⇒ 否掉了「各按本机技能档」这条听觉成长轴;③ 事件表 schema 缺 `trigger_source` ⇒
  **基于时序**的状态播报从 prose 白名单缝隙漏过。
- **8 项阻断**:F-44.2 量纲破裂 · AC-44-09 不可执行且可绕过 · 「单 Listener ⇒ 每设备一条总线」为假 ·
  幻想主通道无总线 · TierMap 列集自相矛盾 · Intensity 无声学映射 · 远程 cue 来源 / 病人锚点未声明 ·
  DTO 引用 sim 类型 + AC-B1 grep 退步。**同日全修** + 16 项推荐同批。
- **四项用户 D 裁决**(均照准):**D-A** 重开 2026-09-14 联机裁决 → **本地档**(`SetTier(TierSource.Local)`)·
  **D-B** 优先级 tie-break 改**距离归一化** · **D-C** 接受无障碍 P0 非目标(ADVISORY)·
  **D-D** **现改 ADR-001**。
- **跨 ADR 涟漪全落盘**:`adr-018`(§三 七总线 · §五 理由重写 · §六 加 `trigger_source` · §七 P1a→P1b ·
  §一 删 `VitalsDto` · **Alt-4 由驳回改采纳** · References)· **`adr-001`(新增 §一之二 `IPositionalChannel`**
  —— per-`ActorId` latest-value 表 + 消费者登记 + 病人/受伤实体锚点发布者 + cue 非复制;原文零处提及音频)·
  `tr-registry.yaml`(`TR-concept-002` 修订 · `TR-diag-024` 改判 · `TR-diag-025` 硬化)· `traceability-index.md` ·
  `architecture.yaml`(**新 `positional_channel` 契约** + `replay_pipe` 缩窄)· `systems-index.md`(row 44 / row 45 / §2 / 设计序 20)。
- **主线**:AC 27 → **32** 条 + `[A]`/`[L]` 证据类型 · 新增 **F-44.6 / F-44.7** · **§Event Table Schema**(`OQ-44-4` 升格)·
  `OQ-44-3` 消解 / `OQ-44-8` 新增 · 资产族补全。
- **已落盘**:`audio-system.md`(文件头 → **✅ Approved**)· **新建** `design/gdd/reviews/audio-system-review-log.md` ·
  `systems-index.md` row 80(**✅ Approved**)+ row 287 + row 530 + §设计序 20 摘要。
- **用户裁定:接受修订、免二轮 → ✅ Approved**(与 42 / 51 / 13 / 30 同款;
  **⚠️ 免二轮 = 显式风险接受,不是「已核对」** —— 8 项阻断由撰写 + 修订的同一上下文判修,未经独立复核清点)。
- **重开触发条件**(任一命中须开新会话跑二轮):① 门 A / 契约程序集实证失败(`AudioCueDto` 引 sim 类型)·
  ② `IPositionalChannel` 被 45 实现证伪 · ③ 白名单断言在时序化状态播报上 false-negative ·
  ④ `SetTier(TierSource.Local)` 在 4 人同场产生不可接受分叉。
- **残留未还清判据**(非阻断):`TICK_SECONDS`(D-8-7)· `Project(Sign_j) → [0,1]`(D-8-4)·
  声学方向 / sound-bible 实例(与 `/art-bible` 同批)。`OQ-44-8` 发布者实现随 45 走 P1b。

### ✅ 第 3 项:`concept-benchmark.md`(非系统 GDD)—— 首轮评审 → 4 阻断 + 7 推荐全部落盘 → **Accepted**

- 见本文件上方「本会话新增」节;非 P0 系统 GDD,不占 P0 设计序。**评审队列就此清空。**

---

> **2026-09-18 后半场追加(/consistency-check 复跑)**:30 Approved 后的全量一致性检查
> **数值层 PASS(0 冲突)**;发现 4 处过时状态叙述(30 已 Approved,25/51 仍写 Needs
> Revision)+ 1 处登记义务闭环(`case-system.md:838` ↔ `skill-system.md:460` 回填兑现)。
> 用户裁定 **[A] 修四处(推荐)** → 全部就地修复 ✅(报告见 `docs/consistency-report-2026-09-18.md`)。

## 上一节(2026-09-18 后半场 · 账本对齐)

**「先修复状态与实际脱节 → 8 / 21a / 52 状态收尾 → 最后评审 GDD 3 项」—— 前两步 ✅ 完成,第三步待启。**

### 第一步:状态 vs 实际对齐(✅ 全量落盘)
| # | 文件 | 原状态(错) | 现值(对) |
|---|------|-----------|---------|
| 7a | `persistence-service.md` | In Design | ✅ Approved(2026-09-17) |
| 9 | `disease-simulation.md` | In Design(「待五轮验证闭合」)| ✅ Approved(2026-09-16 免五轮) |
| 25 | `combat-and-weapon-lines.md` | In Design | ✅ Approved(2026-09-18 免三审) |
| 37 | `case-system.md` | 🟡 Revised | ✅ Approved(2026-09-17 免二轮) |
| 13 | `patient-ai.md` | ✅ Approved | ✅ Approved(2026-09-18 免二轮) |
| 44 | `audio-system.md` | ✅ Approved | ✅ Approved(2026-09-18 免二轮) |

- 前四项 = 各评审日志已结案、正文已修订落盘,但**文件头未回填** ⇒ 就地改为 Approved(口径与 systems-index / review-log 一致)。
- 13 / 44 = 当时**从未跑过 `/design-review`**(`reviews/` 下无对应 log)· 但 systems-index §10「已有 GDD」行曾以 ✅ 列出 ⇒ 歧义。**当日晚些时候两者均已跑首轮并 Approved**(13 免二轮 / 44 免二轮)。
- 同步 `systems-index.md`:row 49(13)· row 80(44)· row 287(44 概述)· §10 已有 GDD 行(13 / 44 口径)· §11 撰写队列行(「已有 GDD 16 → **18**」·「剩 15 → **13**」· 25 → 结案)· §10 P0 已设计行(25 待二轮 → **已结案**)。

### 第二步:8 / 21a / 52 状态收尾(✅ 用户裁定)
- **8 诊断** → **Approved**(接受首轮修订,不重跑)· 实现期门:`D-8-4` ✅ / `D-8-6` ✅ / **`D-8-7`(`TICK_SECONDS`,归用户数值轮)**。
- **21a 物品库** → **Approved**(接受三轮修订 —— **覆盖评审日志「仍 In Review,不得标 Approved」结论**,显式风险接受;重开条件四条 + 实现期门 `D-21-21/24/25/26/27/28` 已登记)。
- **52 随机事件** → **Approved**(接受两轮修订 —— **覆盖评审日志「仍 In Review」结论**,显式风险接受;⚠️ **承重问题仍 OPEN**,门控在 37)。
- 三份文件头 + systems-index(rows 44 / 57 / 88)+ 三份 review-log 结案追记 **均已落盘**。

### 第三步:评审 GDD 3 项(🟡 范围已定 · 待逐份新会话执行)

**用户裁定(2026-09-18):三份全纳入 · 逐份新会话跑(每份评审者独立于撰写上下文)。**
`/design-review` 每次调用 = 一簇独立子代理 → 其 agent 上下文即等于「新会话」,满足独立性要求。

| 序 | 目标 | 权威件 | 体量 | 重点靶子 |
|----|------|--------|------|---------|
| 1 | `patient-ai.md`(13 病人 AI · Feature/P0) | ADR-016 | 11 节 · 51 个小节 | **13↔37 接缝**(`IPresentPatients` 只读视图)· 决策 = 派生态住边界层 · 感知读粗粒度整数格(**禁读表现态位置**)· 单套行为程序 × 两类参数 · `O-27-`/`O-1-` 反向义务 |
| 2 | `audio-system.md`(44 音频 · Foundation/P0) | ADR-018 | 11 节 · 27 AC | **无提示音铁律机械化**(AC-44-09 白名单断言)· V-8.7 四条硬需求(AC-44-01…08)· 细湿啰音**非连续水声**(AC-44-08 医学准确性)· 混音拓扑与快照不得报状态 |
| 3 | `concept-benchmark.md`(竞品对标 · **Draft**) | `game-concept.md` | 8 节 + 3.1/3.2/3.3 子节 · 176 行 | **非系统 GDD** ⇒ 按「设计理论 / 定位一致性」审,不套 8 必备节的机制可测性标尺;靶子 = 对标断言是否与 `game-concept.md` 支柱一致 · 「背离项」风险是否诚实记账 |

**执行纪律**(承既有先例):
- 📌 **三份的逐份评审提示词便签已写**:`production/session-state/review-prompts-2026-09-18.md`
  (含每份的命令 / `--depth` 理由 / 重点靶子 / 跑后收尾清单)。
- 每份跑完 → 用户裁定(改 / 免二轮 / 保持)→ 再回**本会话或新会话**做收尾记账(文件头 + systems-index + review-log)。
- 13 / 44 是 P0 系统 ⇒ 跑完须按先例**新建 `reviews/[name]-review-log.md`**。
- concept-benchmark 是 Draft 非系统 GDD ⇒ 评审记录可并入其文件本身或 `docs/`,**不必然建系统 review-log**。

---

## 上一节(2026-09-18 后半场 · 系统 30 首轮评审结案)

**系统 30 技能与熟练度 首轮 `/design-review`(lean · 独立会话)—— ✅ 结案:判 NEEDS REVISION → 4 BLOCKING + 7 Recommended 全部落盘 → 用户裁定接受修订、免二轮 → Approved。**

### 评审结论
- lean 模式(全部阶段,不委托 specialist)。**架构复核 2026-09-15 的两条判定被确认**:
  全文零 ADR 引用 · `P = 1.4` 违反 8 的 G-1。
- 判 **NEEDS REVISION**:4 BLOCKING(B1 定点域 · B2 `P` 取值集 · **B3 `SkillGrown` 落病史流**(机制级,经用户裁定)· B4 冷却期 tick 化)+ 7 Recommended。

### 修订落盘(4 BLOCKING + 7 Recommended)
- **B1** 公式浮点泄入 sim → 三处公式 + 旋钮全补 **Q16.16 定点域**(`FixParse`/`Mul`/`Div`/`ROUND_HALF_AWAY_FROM_ZERO`)
- **B2** `P = 1.4` 违 G-1 → `P` 标 ⚠️ `*待裁*`,取值集 `{整数, 1/2}`(`{1, 1.5, 2}` 例值);§8 新增 AC
- **B3** 成长事件无流载体 → **§3.2 规则二新增:`SkillGrown` 事件落病史流**(主机唯一 Append · 载荷 `(skill_id, object_id, novelty_class)` + `Level`)—— 兑现 51 的 `AC-51-D2`;残留 = `entities.yaml` 登记 + ADR-009 §三 补记(外部兑现)
- **B4** 冷却期墙钟 → `NOVELTY_COOLDOWN` 改 **tick 计**,tick 频率归 `ITickProvider`
- **R1** §6 依赖表补 37 / 51 / 25 三行(37 侧义务双向化) · **R2** `Level` 随 `SkillGrown` 携带 · **R3** 达 60 后事件仍发经验丢弃 · **R4** `CombatPower` 以 `Fix` 传入 25 · **R5** JSON 承载字段类型注 · **R6** 新颖度判定 = 30 侧字典 + tick 水位 · **R7** `K_difficulty` 入参注

### 收尾记账(2026-09-18 已落)
- `skill-system.md` 文件头:Last Updated → 2026-09-18 · 首轮评审结案 blockquote + 文末「首轮评审结案」表(Status → **Approved**)+ 重开触发条件四则
- `systems-index.md` row 30(§1 表)→ **✅ Approved(2026-09-18 用户裁定接受修订、免二轮)**;§10 已有 GDD 行同步
- `reviews/skill-system-review-log.md` **新建**首轮条目 + **结案追记(2026-09-18)置顶**
- **登记义务(兑现侧在外部)**:`entities.yaml` 追加 `SkillGrown`(病史流 Kind)+ `ADR-009 §三` 骨架补记;`TR-skill-007` 待 30 定值后转 covered

**带进实现期(不阻塞)**:`P` 终值(`{1, 1.5, 2}`)· `NOVELTY_COOLDOWN` tick 数 · `BASE[skill]`/`C`/
`NOVELTY_FIRST`/`NOVELTY_DECAY`/`DEATH_LOSS`/`MED_COMBAT_MOD`/`WeaponMultiplier[line]`(数值全归用户,与 `OQ-25-7` 同批)。

---

## 本会话新增(2026-09-18 · `concept-benchmark.md` 首轮评审 + 修订结案)

**`design-review design/gdd/concept-benchmark.md --depth lean`(独立会话)→ 判 `NEEDS REVISION`(scope S)→ 4 阻断 + 7 推荐当日全部落盘 → 用户裁定接受修订、免二轮 → ✅ Accepted。**

- **性质**:Draft 竞品对标(**非系统 GDD**),不套 8 必备节机制可测性标尺;靶子 = 对标断言 vs 支柱一致性 + 背离项记账。
- **4 阻断均属内部一致性 / 载体,非定位错误**:
  - **B1** `ReuseScore` 无载体 → §4 补 **54 行全系统评分表**(+档位阈值 + `W_structure=0.7` rationale);AC-1 改可机械核对。
  - **B2** §6 依赖伪双向(`skill-system.md` 零引用 / `/art-bible` 文件不存在)→ 重写为信息依赖口径,逐行核实反向引用,删虚挂、补真实消费方(`random-events` / `world-and-ecozones` / `adr-015`)。
  - **B3** ⚠️「把杀掉换成判」被读成「本作无战斗」,与 `game-concept.md` 格斗线冲突 → 全文口径校正为「**反转的是致命性,不是战斗**」(§1/§2/§3.1/§5)。
  - **B4** AC 含不可测项 → §8 重写为 A 文档一致性(AC-1…4)+ B 对外呈现纪律(AC-5…9)。
- **用户裁定**:B1 = 全系统评分(无法对标的去英灵神殿以外找参考;分数标「**初稿,可重算**」)· B3 = [A] 判=主动词、明写格斗保留 · 跟踪记录 = 建 review-log + 回填 Status、**暂不动下游 GDD 双向回填**。
- **收尾记账已落**:`concept-benchmark.md`(文件头 → **Accepted** + 文末「首轮评审结案」表)· **新建** `reviews/concept-benchmark-review-log.md` · 本文件。
- ⚠️ **未追踪的技术债(诚实记账)**:本次修订使 `concept-benchmark.md` **行号整体位移**(现 328 行,原 176 行)—— 外部按行号引用的锚点(`:37/:46/:58/:60/:80`)现已全部错位。**下游回填本次按裁定跳过**;日后若需修正,受影响文件 = `systems-index.md` · `world-and-ecozones.md` · `random-events.md` · `game-concept.md` · `adr-009` · `adr-015` · `entities.yaml`(机制性内容未变,仅为指针漂移)。

---

- **用户裁定(2026-09-18)**:三份全纳入 · **逐份新会话**跑(每份评审者独立于撰写上下文)。
  ① **13 病人 AI** —— ✅ **Approved(2026-09-18 首轮 `NEEDS REVISION · Scope L` → 9 BLOCKING + 8 Recommended 全落盘 → 用户裁定免二轮)**。
  ② **44 音频** —— ✅ **Approved(2026-09-18 首轮 `MAJOR REVISION NEEDED · Scope L` → 8 BLOCKING + 16 Recommended 全落盘 + ADR-018/ADR-001 涟漪 → 用户裁定免二轮)**。
  ③ **concept-benchmark.md** —— ✅ **已完成(2026-09-18):首轮评审 → 4 阻断 + 7 推荐全部落盘 → Accepted**;非系统 GDD,见本文件上方「本会话新增」节。
- **用户的裁决堆**:`OQ-25-1`/`3`/`7`/`8` · `D-4/6/7/10/12/15/16` · `OQ-27-1…7` · `OQ-51-1/4/5/7/9` · **`P` 终值(`{1, 1.5, 2}`)± `NOVELTY_COOLDOWN` tick 数**(数值轮)。

7a 持久化 / 42 拟物 UI / 27 敌人 AI / 23 建造 / 24 医馆 / 37 病例 / 51 遥测 / 30 技能 / 8 诊断 / 21a 物品库 / 52 随机事件 / **concept-benchmark(对标文档)** 均已 Approved/Accepted。
**⚠️ 评审者须独立于撰写上下文** —— 13 / 44 的首轮评审应在新会话进行。

---

## 上一节(2026-09-18 前半:系统 27 重开评审结案)

**系统 27 重开 `/design-review`(lean · 独立会话)—— ✅ 结案:用户裁定 [A] 现在就修订 + [甲] 遭遇级硬时限 → 4 项文档级阻断全部落盘 → Status 保持 Approved。**
- **触发条件命中**(2026-09-18 `O-27-3` 结案 = 25 载荷定稿 ≠ provisional 假设:档位折叠归 9、27 改读 `QueryHurtLevel`)。lean 模式(全部阶段,不委托 specialist)。
- 判 **NEEDS REVISION**:4 项阻断,全部**文档级/规格空洞**,零机制重裁(冻结纪律:机制触发发现 → 登记为 D/OQ,不静默修)。

### 修订落盘(4 BLOCKING,全零机制风险)
- **R1** 「超时」无机械定义 → **规则二十之二** `ENCOUNTER_TIMEOUT`(遭遇级硬时限,用户裁定 [甲]:自 `EncounterStarted` 起最长 tick 数,到点 → `EncounterEnded{reason=超时}`,与状态无关,只写一次,结束 ≠ 实体消失)+ **Tuning knob 五之三**(`> DISENGAGE_DELAY`)+ **OQ-27-7** 登记 + **AC-27-35** 可达性判据注
- **R2** A24 联动未在 27 侧登记 → **`O-27-10`**(烘焙器两表同批可见,不新增运行期接口)+ **R_CONTACT 参数行交叉引用**(§Tuning Knobs 一)
- **R3** 同 tick 求值顺序未钉死 → **Formulas 节 blockquote**:① actor_id 升序逐个求值六态机(纯函数,读上 tick 快照,无链式反应)② 遭遇级检查 ③ 写输出;两条 `EncounterEnded` 互斥
- **R4** 陈旧标记 → 规则十八 provisional **解除**(Kind 三元组已定名,27 侧零改动兑付);`HurtLevel` 折叠改归 **9**;「世界流(9 / 25)」→「世界流(9)」;`O-27-3` → ✅ 已结案

### 收尾记账(2026-09-18 已落)
- `enemy-ai.md` 文件头:Last Updated → 2026-09-18 · 重开评审结案 blockquote · Status 保持 **Approved**
- `systems-index.md` row 27(§1 表)→ 23 规则 / `O-27-1…10` + 重开评审结案;§11 队列行 15 同步
- `reviews/enemy-ai-review-log.md` 重开评审条目置顶(NEEDS REVISION → 4 阻断落盘 → Status 保持 Approved)
- `tr-registry.yaml`:TR-enemy-017 note ⚠️「待 25 二轮确认」→ **✅ 2026-09-18 独立确认移除** + 陈旧注释「唯一 P0 硬前置 = O-27-3」补结案标记(YAML 校验通过)

**带进实现期(不阻塞)**:`O-27-8`(对侧回填:1/23/6 单边)· `O-27-2`(44 反向边缺失)· `O-27-9`(导航格代价字段 6/23/27 对账)· `OQ-27-1…7`(数值轮,`OQ-27-7` = 半规格有正确性面,写遭遇生命周期 / A* 前须标定)。

---

## 上一节(2026-09-18 前半:25 二轮评审执行)

**系统 25 二轮 `/design-review`(full · 新会话 = 独立于首轮撰写上下文)**
- Phase 1-3 主评审 ✅;二轮三件专项核对 **全部通过**(O-27-3 口径 / 9 侧 F1·R11·F4·R12·R17·R18 回填实核落盘 / 27 provisional 契约差已消 —— 证据行号见评审会话)。
- 两处记账残留(player-controller `O-1` 行 ×2 + `OQ-1-8` 击退注)✅ **本轮已划结实核**。
- Phase 3b:7 路专家全部返回(systems-designer / qa-lead / game-designer / ai-programmer / unity-specialist(补救)/ gameplay-programmer / audio-director);发现池已去重合并(A 契约 5 · B 数学 5 · C 验收 4 · D 时钟 2 · E 音频 4 · F 设计意图 6 · G 记账 5)。
- 独立实核补充:game-designer F1 的「承诺 2 无载体」需订正 —— **H3 已在 systems-index §11 登记**(救治/审讯/捆绑归 10,ADR 推迟 P1a),缺的是 **25 承诺 2 正文内的 ⚠️ 现场注**(承诺 4 有 C2 注、承诺 2 没有 = 同罪不同判);F5 手术耦合 P0 不活(`skill-system.md:355-357` 属实)。
- **Phase 3 Step 3:creative-director 终审运行中(串行,唯一 Opus)** → 返回后进 Phase 4 报告(只读)→ Phase 5 widgets。
- 冻结纪律不变:触机制者(A1 Down 归属 / A5 QueryHurtLevel 代价 / F3 对角可击 / E3 音强旋钮 / F4后半 压制回报)= **登记 D-14+/OQ-25-9+ 提案**,不静默修。

---

## 上一节(2026-09-17:系统 25 首轮评审修订 + 涟漪全部落盘)

**系统 25 格斗与武器线 —— `/design-review`(full)首轮判 MAJOR REVISION NEEDED(scope L)→ 修订全量落盘 → R1–R18 + V1–V7 涟漪全量落盘**
文件:`design/gdd/combat-and-weapon-lines.md`
进度:**评审 = 7 路 haiku 专家 + creative-director 终审(✅ 全部返回)**;
**修订 = ✅ 已全量落盘**(用户裁定 [A] + 四 fork 均 [甲]:A3=只读查询 `IsSuppressed` ·
D2=敌/兽 `CP:=0` 退化式 · B2=逐 bar 组合上界 · 范围=全量落盘、**机制与数值冻结**);
**涟漪 = ✅ 全部落盘**(见下表;25 = 🟡 **修订完成,待二轮评审**,未标 Approved)。
**本轮 = 收尾完成,无待执行的回填义务。**

### 修订落盘清单(本轮实际改动,全部为口径/文本级)
- **规则〇 新增**:意图→sim 求值通道 + **占用门** `Occupied(a)`(三边界:压制不占用 / Natural 占用 / 占用不发事件)+ 不排队 + tick 频率归 `ITickProvider`(R16)
- **规则一**:判定式补全五合取项(占用门 + 两个 `¬Down`);Overview/规则二/Interactions 的 `inflicts_injury` → `maps_to_injury` 真源订正 ×4(H1)
- **§五 压制**:`IsSuppressed` 公开只读查询 + 判据区分 sim 侧/呈现侧 + 反轮询理由(A3)
- **F-25-2**:`BASE_STEP_MAX` 派生定义 · +14.3% 订正 · ⑥ 敌/兽退化式 · ⑦ A21(B3/B5/D2/B4)
- **F-25-4**:可执行 act 集合 = 当前线(E3 分裂结清)
- **F-25-5**:压制生效时刻 = 求值 tick,调用者 = 25 自己(F5)
- **F-25-6**:④ Natural `range_override` 必填(A22/A23)· ⑤ A24 跨文档联动(D1/D3)
- **F-25-8 重写**:(a) 逐动作 + (b) 逐 bar;乘法形式判据(零除法路径);A25a/b + A26 ulp 带(B1/B2/H4)
- **规则五(H2)**:`nearby/downed_allies` ≠ 9 查询的订正(防回填教错)
- **§六 音频**:乐层 = 白名单第 5 类 + **G1 禁帧对齐 / G2 去标注盲测 / G3 本地触发** + §6.5 触发方登记(V7)
- **§6.6 新增**:三时钟时间总图(T1/T2/T3)+ 承重理由(AC-25-V-01…03)
- **§九 做不到 +1**(C2:OQ-25-1=[丙] 时玩家死亡纯叙事);**§十 +D-12 / D-13**
- **AC 大改**:[V] 阈值 T-1…T-3 + 拆静态半(7-01b/7-02b)+ 7-06/7-08 升 [P];
  新 §〇 组 0-01…05(占用门);1-09(A8 独立,杀假映射);2-04 AST 化;2-08 双路订正;2-09 退化式;
  3-06 重写 + 3-07;4-04 双重判据(grep 枚举 + 扫流,承 37 先例);4-08/4-09/5-11/5-12/6-10/6-11;
  5-01 改判据(同 tick 两条**都收**,E1);5-03 措辞;六节头 NOT-RUN 前置(E6);八节计数表重写(76 条);九节 A↔AC 对照全重写
- **断言表**:A1–A26(A1 `>0` 收紧;A13 拆 a/b;A21–A26 新增;求值点 {动作,bar}×{CP=0,CP_MAX})
- **OQ**:OQ-25-1 路甲/丙代价细化;OQ-25-3 并苏醒态;**OQ-25-4 结清**(默认=9 查询,R17 回填);OQ-25-7 补旋钮;**OQ-25-8 新增**(tick 频率,与 OQ-8 同批前置);汇总表更新
- **R 表 +4**:R15(27→25 意图通道成文)/ R16(tick 频率标定)/ R17(9 定义 HurtLevel 折叠+查询)/ R18(9 承担 InjuryStateChanged 置序义务)

### 25 落盘后未复核的判定(2026-09-17 本轮新增)
| # | 裁定 |
|---|------|
| D-1 | **9 的通道五 → 六**(新增「伤口」通道)—— 伤口语义归 9,25 只持「冲击瞬效 + 伤口标点」 |
| D-2 | **表现层可订阅 onset / `InjuryStateChanged`** —— 不走 `VitalsDto` 差分(whiplash 处置才落得了地) |
| D-3 | **音乐层可随遭遇态切换**(连续织体),jingle / sting / ducking 仍禁 —— ⚠️ **须回填 ADR-018 §六 显式例外(V4)** |
| D-5 | **禁结果标注类全部画面反馈**(闪白 / 定格 / 提示音)—— 主动放弃的手感预算 |
| D-8 | **兽类攻击必须预告**(无掷骰 ⇒ 公平性只能来自可读性) |

### 25 的回填义务 —— ✅ 涟漪落盘完成表(2026-09-17 本轮)
| 义务 | 内容 | 落点 | 状态 |
|------|------|------|------|
| R1 | 9 的邻居表 25 行措辞(真源 / 执行者 / writer 拆分)| `disease-simulation.md:641` + 反查表 | ✅ |
| R2 | 三 `Kind` 登记 | `entities.yaml`(YAML 校验过) | ✅ |
| R3 | ADR-009 §二 路由枚举 + §三 骨架(第五、六 Kind) | `adr-009` | ✅ |
| R4 | `O-27-3` 口径(临时契约解除) | `enemy-ai.md` §四 O-27-3 | ✅ |
| R5 | (被 R11 取代) | — | ✅ |
| R6/R7 | 27 依赖表(25 行 + 44 乐层 + V2 订阅) | `enemy-ai.md` | ✅ |
| R8 | 1 的依赖表 + `OQ-1-7` 结案 + `O-3` 25 行撤销 | `player-controller-and-movement.md` | ✅ |
| R9 | 9 的流归属表(病史流/世界流两行 + 表头口径) | `disease-simulation.md` | ✅ |
| R10 | 30 的 `TR-skill-007` | `tr-registry.yaml` gap→partial | ✅ |
| R11 | 9 的 F1 加性阶跃项 + TRAUMA_CAP 单侧 + trauma_half_life | `disease-simulation.md` F1 | ✅ |
| R12 | 9 的 F4 `LethalFor(entity,d)`(注册表 lethal 语义收窄) | `disease-simulation.md` F4 | ✅ |
| R13 | 21a 的 `inflicts_injury` 降级为集合约束 + A20 漂移门 + 双 fixture | `item-database.md` | ✅ |
| R14 | 1 的 `AC-1-23` 负边界(压制止于位移 + Jump) | `player-controller-and-movement.md` | ✅ |
| R15 | 27→25 意图通道成文(「有入向意图、无出向数值」) | `enemy-ai.md:1094` + 依赖表 | ✅ |
| R16 | tick 频率标定登记(`OQ-25-8` 与 `OQ-8` 同批) | `technical-preferences.md` + `adr-005` 性能表注 | ✅ |
| R17 | 9 定义 `HurtLevel` 折叠 + `QueryHurtLevel` 公开查询 | `disease-simulation.md` F4 节专注 | ✅ |
| R18 | 9 承担 `InjuryStateChanged` 置序 + Step≡CatchUp 序 | `disease-simulation.md` F4 节专注 | ✅ |
| V1/V3 | 9 的通道表五→六(伤口)+ 刃背绝不出血构建期断言 + AC-21 重写 | `disease-simulation.md` §二 | ✅ |
| V2 | 13/27 补「可订阅 onset」(表现映射侧) | `patient-ai.md` + `enemy-ai.md` | ✅ |
| V4 | ADR-018 §六 音乐层例外 **+ G1–G3 护栏全文** | `adr-018-audio-architecture.md` | ✅ |
| V5/V6 | 三件套放弃 + whiplash 不可归零 = **已知代价**(评估期不当 bug 报) | `production/known-costs.md`(新建 KC-25-1/2) | ✅ |
| V7 | 遭遇态→乐层触发方登记(27 订阅行 + 44 第 5 类行) | `enemy-ai.md` + `audio-system.md` | ✅ |
| TR | `TR-combat-001…024`(18 ✅ / 4 ⚠️ / 2 ❌)+ 翻转 enemy-017 / skill-007 + 计数校正 259→283 | `tr-registry.yaml`(脚本实测过)+ `traceability-index.md` §14/汇总/变更历史 | ✅ |
| 登记 | row 25 🟡 修订完成待二轮 · 27 行 🔴 前置划结 · §10 18/31 · §11 真空 10→9 · review-log 建档 | `systems-index.md` · `reviews/combat-and-weapon-lines-review-log.md` | ✅ |

**⚠️ 残留(留二轮核对,均非阻塞)**:① 本轮**所有 TR / 状态变更由撰写方置入,未经独立确认**;
② `player-controller-and-movement.md` §Dependencies 的 `O-1` 行(~:1097)25 半边未划结(仅 §二
权威账本已更新)+ `OQ-1-8` 提及击退处未加注 —— 两处纯记账一致性问题,二轮顺清。

### 25 未闭合 Open Question(修订后 7 未闭合 + 1 结清 + 1 新增)
OQ-25-1 🔴 玩家致死伤情形态(9 侧)· OQ-25-2 🟠 割断装备带归属(21a)· OQ-25-3 🟠 `Down` 覆盖边界(含苏醒 D-13)·
~~OQ-25-4~~ ✅ 结清(默认=9 查询,**R17 已回填**)· OQ-25-5 🟡 `cell_size` 比例 · OQ-25-6 🟡 蛇咬牙印 ·
OQ-25-7 🟢 数值旋钮(单独一轮)· **OQ-25-8 🔴 tick 频率标定**(R16 已登记,值待用户);另 §十 待裁 **D-12 / D-13**

### 开工前已裁的接缝(用户 2026-09-17 裁定,五项)
| # | 问题 | 裁定 |
|---|------|------|
| S1 | 逐实例伤害量的载体 | **[A] 25 出载荷 · 9 加加性项** —— 命中发带 `magnitude` 的 onset 事件;9 的 F1 新增加性阶跃 `Σ magnitude × Decay_injury(Δ)`;归零 = `position_agg` 自然越 `COMA_THRESHOLD`。**✅ 已回填 9(R11/R12/R17/R18 落盘)** |
| S2 | `HurtLevel` 档位定义方 | ~~[甲] 归 25 的载荷~~ → **评审后改判(2026-09-17):档位折叠归 9**(义务 R17);25 的载荷**不**携带 HurtLevel;OQ-25-4 按默认结清 = 27 走 9 的公开查询 |
| S3 | 击退注入权(`OQ-1-7`) | **[A] 不注入** —— 击退只做动画/受击反应,位置不变。**结清 `OQ-1-7` · 撤 `O-3` 的 25 一行** |
| S4 | 「生命归零 → `INJ_COMA`」执行者 | **执行者 = 9** —— 9 的九态机自己判;C1 的「25 登记」读作「归零在 9 的模型里被记为 `INJ_COMA` 而非 death」。接口铁律二完整 |
| S5 | `INJ_COMA` 在 9 里的角色 | **状态投影,不进 D 集** —— `State = 昏迷 ⇔ position_agg ≥ COMA_THRESHOLD ∧ 存活`;无曲线、不进 `max`、救治路径自动复用 |

### 25 必须兑付的既有登记义务
`O-27-3`(世界流伤情 `Kind` 名 + 载荷 + `HurtLevel` 档位投影 — 🔴 P0 硬前置)·
`OQ-1-7`(已裁) · `O-3`(传送频次上界 — 已裁为不注入) · `O-1`(压制权反向依赖补登)·
52 的 `StrengthCap = f(最高格斗线, 急救)` 形状 · `MIN_tier` 进度缩放 · `TR-skill-007`(30↔25 数据边界)

---

## 附:上一节(系统 37 病例 · ✅ Approved 2026-09-17)

**系统 37 病例 —— ✅ Approved(2026-09-17 结案)**
二轮 `/design-review design/gdd/case-system.md`(depth `full`)判 **NEEDS REVISION**(scope **L**);
用户裁定 **[A] 现在就修订** ⇒ **22 项阻断(去重 9 条 BLOCKER 级)当日全部落盘**;
随后用户裁定 **[B] 接受修订、免二轮**,直接标记 Approved。
`design/gdd/case-system.md`(**36 条 AC** · 12 节零占位符)。

**⚠️ 免二轮 = 显式风险接受,不是「已核对」**。重开触发条件四者任一:
① **`D-21-13` 收窄**或 **ordinal append-only 构建门**在实现期被证伪 / 需改判;
② `F-37.1b` 三案链成员判定域被后续系统(52 / 31)修改;
③ 规则八「37 沉默 ≠ 世界沉默」三层表与 53 的实际反馈**时序约束对不上**;
④ `case_judgment_lexicon.json` 的 ordinal 映射在实现期需要**改义**(而非追加)。

**四轮承重面经逐条实核均未被推翻**:病例 = 记录非实体 · 病例流不折叠 · 处置为证 · 守口如瓶。
2026-09-15 的 12 项 blocking **可验证地全部已解**。

### 四项用户裁定(2026-09-17,均照准)
① **P0 反馈真空**:**接受时序泄漏、改写措辞**(机制不变)—— 支柱四「系统永不报你答对了」
→ **「37 沉默 ≠ 世界沉默」**;53 受两条硬约束(① 延迟 ② 不可归因);
② **三案链成员判定域**:**「Boss 击败后不可重现」**(用户自由文本裁定)—— 脚本链一次性,
`ScriptedChain(D)` 显式指定成员;
③ **Judgment 词表所有权**:**进 ADR-014 烘焙词表** —— `assets/data/case_judgment_lexicon.json`,
版本化 ordinal,构建期与 9 的病种注册表做非一一对应校验;
④ **载荷内枚举口径**:**ordinal + 进 `ConfigVersion`**(修 `FixSet` 型错 + 收窄 D-21-13)。

### 🔴 三条系统性根因(六路专家收敛)
1. **`FixSet` 类型本身是错的,不只是没定义** —— `disease_id` 在 9 是 `DIS_*` 枚举,
   塞进 Q16.16 算术域正是 **ADR-006 D-21-17 修掉的同一类错误**;破坏面含
   `D ∈ disease_set` 退化为**定点相等比较** —— 语义已错却逐位可回放(静默失败类)。
   **修**:`DiseaseIdSet`(版本化整数 ordinal,走 `int`/`u16` 编码路径)。
2. **`PatternRecognizedPayload` 根本没有 `salted_key` 字段** ⇒ ADR-008 §一/三/五 三处都宣称
   「用加盐哈希键」而 struct 里没有 ⇒ ① AC-37-14 成**空断言**(恒真),
   ② **53 无法定位是哪个 `D` 触发**(共病时 `anchor_case.disease_set` 不唯一)。
3. **跨域窗口公式方向写反** ⇒ 该式恰好使「复诊第二例凭首诊的处置通过前置」成为
   **唯一可通过路径** —— 紧接下一行自我宣称要堵的后门在公式层实际未堵。
   长期未被发现的原因:2026-09-15 的修正只改了**字段名**、**未改区间方向**。

### 🔴 本轮自行查出的 ADR 级冲突(非专家提出)
ADR-008 §三 / 37 反复写「表变更 ⇒ **旧存档拒载**」,而 **ADR-010 §七 与 ADR-014 §五
明写 `ConfigVersion` 不匹配「非致命」** —— 两者不可同时成立。
**处置(不推翻 ADR-010)**:`ConfigVersion` 只负责「**改动可追溯**」;
**静默错读另起一道更严的门** —— **ordinal 映射表 append-only**(既有条目号永不重用、
永不改义 + 构建期对 `data/_ordinal_baseline.json` 基线断言 ⇒ 改号 = **构建期硬失败**),
落 `adr-014 §五 D-21-13 承接条 ③`。**两者不可互相替代**。

### 落地清单(均已落盘)
- `case-system.md` —— 前言 AC 计数订正(32 → **36**)· `F-37.1` 互异筛选 + `PatternFired` 类型 ·
  **新 `F-37.1b` 三案链触发与成员判定域** · `F-37.2` 窗口方向订正 · `Judgment` 形状 ·
  规则八 **「37 沉默 ≠ 世界沉默」三层表** · 规则九第六处泄漏面 · **§载体欠账表 16 行** +
  `NOT-RUN` 纪律 · `quill_tick` 登记 · `D-37-B` / `D-37-C` 登记 · 3 处「旧存档拒载」订正
- `adr-008` —— §三 载荷订正三处 + `Judgment` 形状 + `freehand_text` 例外 · §四 方向订正 ·
  §五 盐改**逐 `D` 派生** · §一/§三 盐键表达式同步 · Dependencies 补 ADR-014 / ADR-010 §七
- `adr-006` —— **D-21-13 口径收窄**(两行分流表) + **Amendment A 例外登记**(`freehand_text`) ·
  Validation 两条门 · GDD Requirements 补 37 / ADR-008 / ADR-014 三行
- `adr-014` —— **`case_judgment_lexicon` 登记**(§一) + **D-21-13 承接条 ①②③④**(§五:
  覆盖集 / append-only 硬门 / `PATTERN_THRESHOLD`)+ Validation 两条 + GDD Requirements 三行
- `adr-010` —— §七 补注(覆盖集含 ordinal 表,**语义一字未动**,并写明「改成致命 = 另开 ADR」)

### 涟漪已落盘
`tr-registry.yaml`(**36 条 TR-case**,总数 250 → **259**;TR-case-005/008/023/025 就地订正 +
新增 TR-case-028…036;YAML 校验通过)·
`traceability-index.md`(基线 250 → 259 · 汇总行 37 → `36 / 25 / 4 / 7` · 合计 → `259 / 161 / 22 / 76` ·
详情行 + 2026-09-17 变更日志)·
`entities.yaml`(五条 `SimEvent.Kind` 约束全重写;YAML 校验通过)·
`architecture.yaml`(`case_event_stream` 载荷订正 + `case_stream_payload_encoding` ordinal 口径 +
`case_stream_salt` 逐 D 派生 + `data_authoring_and_cooking` lexicon 登记 +
`forbidden_patterns` 新增 **`ordinal_renumbering`**;YAML 校验通过)·
`systems-index.md`(row 37 → ✅ Approved + §11 队列结案 + 重开触发条件)·
`reviews/case-system-review-log.md`(✅ 结案条目置顶,含四条重开条件)。

## 下一步

**✅ 29 死亡与复活已结案(2026-09-19)** —— 首轮 `/design-review`(full · 6 专家并行 + opus 串行)裁
`MAJOR REVISION NEEDED · Scope L`(9 阻断 · 6 根因)→ 当日全量修订落盘 + 四类用户裁定 →
**用户裁定接受修订、免二轮 ⇒ ✅ Approved**。⚠️ 免二轮 = **显式风险接受**;
重开触发条件四条见 `reviews/death-and-respawn-review-log.md`。

**涟漪已落盘(方案 B = 6 落盘 + 2 小回写)**:`death-and-respawn.md`(全面重写,17 AC · 4 公式 ·
新增世界流 Kind `PlayerDied`)· `entities.yaml` · `adr-009`(**Amendment L** + §二/§三/§六)· 
`architecture.yaml` ×3 · `inventory-and-items.md`(R7 兑现 + `AC-20-19` 解除阻塞)· 
`systems-index.md`(row 29 / row 16 / §11 三处)· 新建 `reviews/death-and-respawn-review-log.md`。

**挂下游义务(未改其正文,各归下一轮)**:**9**(`SelfLimited(entity, d)` 实现 + 反向列 29)·
**30**(§4.3 公式改整数截断 + `SkillGrown` 登记 —— ⚠️ **`AC-29-16` 挂 `BLOCKED-BY` 于此,登记前不得记绿**)·
**24**(床格选定规则 + 反向列 29)· **47**(上游列 29)· **25**(`AC-25-6-08` 随 `OQ-25-1` 裁甲而解除阻塞)。

**P0 队列余量(2026-09-19 收口后)**:
- **待二轮 2 份**:`interaction-system.md` 4 🟡 · `prescription-and-medication.md` 11 🟡(均须 /clear 后新会话)。
- **已 Approved 29 项**(含本轮 29)。
- ⚠️ **29 方新增待裁**:`OQ-29-4`(冷却期内再次致死语义)· `OQ-29-5`(`DEATH_COOLDOWN` 值)·
  `OQ-29-6`(**回程载体的 P0 形态** —— 若 playtest 显示纯空间记忆不可行,是否把 43 的最小纸地图视图提进 P0)。

**用户的裁决堆(可攒批)**:`OQ-17-1`(深水线值)· `OQ-17-6` + `OQ-10-9` + 20-BL-4(ADR-001 上行意图通道,同批)· `OQ-25-8` + `OQ-8`(tick 频率,写第一个 `Step` 前必须)· `OQ-10-1` 已结 · `OQ-42-11` + `OQ-17-11`(采前线索呈现规格)· 19 的 `OQ-4-11` playtest 门。

---

## 附:上一节(系统 7b 存档位 UI · **✅ Approved 2026-09-19 免二轮结案**)

首轮 `/design-review`(full)于 **2026-09-19**:5 专家并行(haiku)+ creative-director 串行(opus)
掐出 **6 条 BLOCKING**(CD 判词:39 的病 = 呈现层文档承担存档层职责;**7b 正相反** ——
一份呈现层文档替存档层承诺了存档层从未答应的不逆性)。三项用户裁定已锁:
① **载入安全上下文门**(BL-1 —— 读档与写同钩,未决医疗动作中载入 = 7a 拒绝;锁字段不进存档体,
跨会话复位,防 scum 靠门而非格式锁);
② **不抑制移动**(对齐 39,`AC-1-23` 白名单不加 7b);
③ **42 屏幕空间模态**(`ModalId.SaveSlot7b` 六屏闭集成员,不发档位意图承 39 的 `O-12`)。
修订落盘 4 文件:7b 本体(F-7b.2 拆两投影 `Writable`/`Loadable` · AC 13 条重写 + 载体欠账表 ·
Game Feel 合册非写入门 + 删 ≤1 帧)· **7a**(规则十四载入安全门 bullet + 加载状态机 `Gating` +
`OQ-7a-8` 槽位上限)· **ADR-010**(`struct SaveSlot { uint slot_seq }` + 签名回填 `Checkpoint(SaveSlot)` +
§六 锁字段不进存档体)· **48**(下游表补 7b 行,断锚修复)。**结案:用户裁定 [B] 接受修订、免二轮 ⇒
✅ Approved(2026-09-19)** —— 重开触发条件四者任一:`OQ-7a-8` 被篡改 / 载入安全门与 AC-7a-19 冲突 /
`OQ-7b-6` 在 45 轮被改判 / AC-7b-03·09·13 载体未兑现(详见 `reviews/save-slot-ui-review-log.md`)。
未结随实现期:`OQ-7b-1/2/4/5` + **`OQ-7b-6`(合作分册 → 45 GDD 轮,ADR-001 窄修订同批)** +
7a 的 **`OQ-7a-8`**。

---

## 附:上一节(系统 42 拟物 UI · ✅ Approved 2026-09-17)

首轮 `MAJOR REVISION NEEDED`(scope L)→ 17 项阻断 + 14 项推荐修当日落盘 →
用户裁定**接受修订、免二轮** → Approved。
**⭐ 免二轮 = 显式风险接受,不是「已核对」**。重开触发条件四者任一:
① 三组 spike(焦点桥 / 自定义材质 / 图集)结论落盘;② `AC-42-B4` 降级路径在实现期被启用;
③ F4 三层口径被实现证伪;④ `OQ-42-1` / `OQ-42-12` 用户裁定落地。
未结:`OQ-42-1` / `OQ-42-3` / `OQ-42-4` / `OQ-42-5` / `OQ-42-12` / `OQ-42-14` + 三组 spike。
> **✅ 后续补记(2026-09-22 批裁轮)**:本行所列 `OQ-42-1` / `OQ-42-3` / `OQ-42-12` **三者已裁**
> (均甲;`OQ-42-1` = 恒定屏幕尺寸 · `OQ-42-12` = 取 `min` · `OQ-42-3` = 不虚拟化翻页制);
> `OQ-42-4` / `OQ-42-5` / `OQ-42-14` **仍开放**。**重开触发条件 ④ 已触发**(见下方专段)。

<!-- CONSISTENCY-CHECK: 2026-09-19 | GDDs checked: 31 | Conflicts found: 0 | Report: docs/consistency-report-2026-09-19.md -->

---

## 附:上一节(系统 5 时间与天气 · ✅ Approved 2026-09-19 免二轮结案)—— 自文件头归档

首轮 `MAJOR REVISION NEEDED`(scope L · 9 BLOCKING)→ 当日全量修订 + 四裁定 R-5-A/B/C/D +
九档涟漪(1/9/18/17/52/42/44/13/index)→ **用户裁定免二轮 ⇒ ✅ Approved(2026-09-19)**。
重开触发条件在 `reviews/time-and-weather-review-log.md`。四裁定:**R-5-A** 块哈希(`WEATHER_BLOCK_TICKS`)·
**R-5-B** cell 三调用点钉死 · **R-5-C** 天气→52 强度轴降级 P1a(`OQ-5-8`)+ AC-5-18 正向可感知 ·
**R-5-D** 真·`TICKS_PER_SEASON` + `SEASONS_PER_YEAR ≥ 2`。AC 15 → 21 条(BLOCKING 17 / ADVISORY 1 / EXTERNAL 4)。
二轮抽查点(如重开):AC-5-07 EXTERNAL 不得记绿(BLOCKED-BY-ADR-012 三格 CI + F7)· AC-5-17 拆条 ·
`skylight(t)`「仅呈现禁入判定」是否被 42 误读为判定输入。

---

## SESSION EXTRACT — 2026-09-20 `/review-all-gdds`(full 模式)

**任务**:P0 全 31 项 GDD 的跨件评审(Phase 2 一致性 / Phase 3 设计整体论 / Phase 4 跨系统走查)。
**输入方式**:未全文读 34,583 行,而是建 `/tmp/ragdd/` 抽取件(163 个文件:`*.deps/knobs/fantasy/formulas/ac.md`
+ `ALL.*` 聚合 + `PILLARS.md`/`INDEX.md`/`REGISTRY.md`),4 组子代理只读抽取件路径。
**实体注册表状态**:`design/registry/entities.yaml` 的 `entities: 0` / `items: 0` ⇒ 注册表加速不可用,
一致性核对靠全文抽取件。**待办**:本评审结案后跑 `/consistency-check` 回填注册表。

**已交付的子代理报告(3/4)**:
- G1(2a/2b/2c/2f)`acdefcb2ecd2b29c5` —— 🔴3 / ⚠️6 / ℹ️4
- G2(2d/2e)`a08700202aa1910f4` —— 🔴0 / ⚠️3 / ℹ️3
- G3(3a–3g)`a79912f12f8b709aa` —— 🔴4 / ⚠️11
- **G4(4a–4c 场景走查)`a154ab51b492f6df7` —— 尚未回**(报告时须标注为「未覆盖」)

**主会话独立核实结论(逐条 file:line 复核)**:
- ✅ 证实:**B-1** `input-system.md:279/:731/AC-3-B4` + `audio-system.md:895` 仍是 ADR-011 **改判前**口径
  (全文零命中 `Amendment B`/`EmergencyAttempt`/`预表现`),与 `adr-011:15-16` 及 `emergency-procedures.md:421` 互斥 ⇒ 纯回刷缺口。
- ✅ 证实:**B-3** `player-controller-and-movement.md:1512-1526` 的注块**自证** AC-1-23(单 bool + 三写者互不知晓)
  与 4 的 `AC-4-19` 不可能同真,「1 未裁前视为已知缺陷」⇒ 真缺口,归 **OQ-4-13**(归 1)。
- ✅ 证实:**B-2**(25/29/9 玩家致死通路)9 侧 F4 只有 `self_limit=false` 门,无 29/25 宣布的逐实体 `SelfLimited(entity,d)`
  (`disease-simulation.md:1062` vs `death-and-respawn.md:110`「未结」+ `combat-and-weapon-lines.md:2073` AC NOT-RUN)。
- ✅ 证实:**W-6** `skill-system.md` §4.3 **无「判断类豁免」**(只 `floor(level×0.95)` 全线一致),
  豁免只住 `death-and-respawn.md:329` ⇒ 29 的 AC-29-14「唯一出处 = 30 §4.3」为**无源引用**。
- ✅ 证实:**G2-W-1 是修订未闭环**:`item-database.md:478-483`(D-21-31「唯一钳制点 = F1 正文」)已回刷 5/18/21a,
  **24 侧零命中**(`clinic-machine.md:176/:184/:245/:260/:349` 仍自家 clamp;`Last Updated: 2026-09-17`)⇒ 双重钳制口径回归。
- ⚠️→🔴 改写:**G3 的 B-1**(索引「唯一真环 30↔37」`systems-index.md:541-543` vs `case-system.md:369` +
  AC-37-32 `:1062` 反射禁数值奖励字段)—— 已核三处原文,成立。
- ❌ 降级:**G1-W-5(51「10 尚无 GDD」)** —— 实文件 `telemetry-analytics.md:689` 的「10 的 GDD(尚无)」是
  **OQ 表内陈旧短语**,而 AC-51-B9(`:573`)**已写成「落地后」条件式**,不判红 ⇒ 降为 ℹ️ 回刷。
- ❌ 证伪/更正:**G3-W-10** 的 `OQ-27-3` 并非全开 —— `enemy-ai.md:1494` 已「✅ 2026-09-17 解除与 O-27-3 耦合」,
  **余「与 1 的速度同批核」仍待数值轮**(W-10 须按此改写,不得称「三处一致宣布待数值轮」)。
- 核实为真但属**已登记 D-项**(不重复立案):D-21-29 / D-21-32(AC-18-19 不得记绿)/ D-21-33 / D-21-34 / D-9-B /
  `OQ-20-7`(CarryLoad 双口径)/ `OQ-29-5`(DEATH_COOLDOWN 未定)/ `O-11→10`+`OQ-11-12`(single_dose_max 不含 10)。

**用户中途提出的独立问题(已答复,并已升格为评审发现)**:
「P0 有没有『事件串联推剧情』?」⇒ **没有**。证据:`grep 剧情/故事/串联/结局/通关/编排/他回来`
在 `tr-registry.yaml`(387 条)**零命中**;`adventure-catalog.md:24` 自陈「P0 不含本目录任何一件内容」
且 `grep -c 他回来了` = **0** ⇒ `case-system.md:584` 的 `ScriptedChain` 指针**悬空**(引用却无登记)。

**待用户裁定的收尾动作**:是否把本报告写入 `design/gdd/gdd-cross-review-2026-09-20.md`;
是否把被点名的 GDD 在 `systems-index.md` 标 Needs Revision。

## 2026-09-20 回刷批完成注(追加)

处置次序 4/5/6 已执行完毕,报告 §5 表已标:
- **处置 4(R-2 回刷)✅** — OQ-25-1 口径(combat-and-weapon-lines 5 处)· 陈旧「无 GDD」批量订正
  (world-and-ecozones / input-system / player-controller / enemy-ai O-27-8 / foraging / skeuomorphic-ui)
  · 外抛义务落点(7b→44 audio-system 行 · 字幕→42 E-12b · O-16→3 规则三 + AC-2-22⑥)。
- **处置 5(R-3 落点)🔶** — 29 侧 8 条 AC(01/02/03/04/05/08/10/17)挂 BLOCKED-BY-9;25 侧
  AC-25-6-08 维持 NOT-RUN 并标 R-3。9 侧 SelfLimited 落点归 9 的 GDD 轮(未动)。
- **处置 6(R-5 纪律回填)✅** — 9 文件 11 处标 BLOCKED-BY-OQ-8 / OQ-25-8(详见报告 §5 回刷完成注)。
- **处置 1/2/3/7 仍待用户**:OQ-4-13 · 唯一真环 · R-1 甲/乙/丙 · R-6 各 OQ。
- **Phase 4(G4 走查)仍未回投** —— 追加后报告不重写整份。

## 2026-09-20 裁定落地批完成注(追加)——处置 1/2/3 全部已裁已落

**R-1 = 甲(补充完整剧情内容)** —— 剧情骨架由**用户口述**(教堂神父留下听诊器 → 郊区帐篷医馆
→ 返城寻医书 → 坍塌救助站得《野战应急手册》[人工呼吸法/止血草/草木灰]→ 回营),评审侧按
catalog 语域扩展为**序幕 + 三章 + 终幕**,三案链成员点名 = 甲教书先生 / 乙挑夫 / 丙守夜老人
(均 `INJ_HEMORRHAGE`,与「急召出诊 / 城市遭遇 / 夜间急召」的调度节拍同构)。
**新件 `design/gdd/content/he-returns.md`** + 回刷:`case-system.md`(ScriptedChain 指针 + 残留注闭合
+ 出向 30 行)· `random-events.md`(规则十一后注:52 侧四项脚本条目;OQ 行转部分已定)·
`systems-index.md`(§11 内容债勾 [x] · row 37 转 Approved)· 报告 §5 行 3。
**残留(不阻塞)**:`patient_id` 绑定值 → 7a/ADR-014 实现期;§八 史实断言交 40 复核;
目录 §10「教会医院学徒」措辞差异 = P1a 同步注;药材短缺内容仍待考据。

**OQ-4-13 = per-source lease** —— 1 侧轴 4 表重写(位图 + OR 聚合 + `Acquire/Release(LeaseSource)`,
闭集 Self/Emergency/Combat,append-only 禁重排;29 不扩位)· `AC-1-23` 注块「已裁」·
§Interactions 三行调用面 · 4 侧 OQ 行转 ✅(AC-4-19 转可运行,载体未建前 NOT-RUN)·
索引 row 1 转 Approved。

**唯一真环 = 37 开成长触发** —— 调和形:**37 的流事件(CaseClosed / PatternRecognized)= 30
同源图样类新颖度的派生数据源,30 只读病例流**(与 51 同构,ADR-019 §三);`EmitGrowth`
调用方仍是 8,`AC-37-32` 不松动。三处同步:索引 §7 裁定注 · case-system 出向/环表 ·
skill-system §6 37 行。

**余下待办**:处置 5 的 9 侧 SelfLimited 落点(归 9 的 GDD 轮)· 处置 7 各归其 OQ ·
G4 走查回投(若回,追加报告 Phase 4 节)· TR 注册表暂不为本件追加(内容件无 ADR 可挂,
逐条 gap 无增益;结案后 `/consistency-check` 一并核)。未获授权:commit。

## 2026-09-20 剧情正典二次修订批(追加)

用户口述「全剧情总谱(从帐篷到医馆,P0→P2 八章)」→ 主会话评审(3 真冲突 + 5 引用错)→
Opus 裁决(verdict 成立但须修订,新增承重项 W-1 + 反对 4 项)→ **用户三项范围裁定**:
① 「三案链不触发没关系」= W-1 显式风险接受(复诊走 52 脚本条目不挂 PatternRecognized);
② 帐篷作搭建生存起点 = **P0 允许这一次新建**(game-concept.md:695 原句改判注);
③ **止血草 + 草木灰加入 P0**(foraging 规则一 + AC-17-01b 四者 · prescription 收窄表注 ·
processing 烧焦边择案注;21a/9/11 数据行归实现轮,AC-17-01b 不记绿)。

**落盘**:新件 `content/campaign-arc.md`(八章正典 · 三案嵌序章~第一幕 · 甲复诊=切片终态 ·
幕→章 · 引用五错已修 · 覆盖表 §9 假覆盖已拆 · H3 拍 BLOCKED-BY-H3-ADR)。
回刷:he-returns(§二/三让位注,§四~十有效,四处下游账不悬空)· game-concept(红线限定词 + :695 改判)·
case-system(ScriptedChain 注 + **W-1 注块:9+37 须会签二选一,① 挂 DIS_* 推荐**)·
random-events(:432 落位注)· systems-index §11 · emergency-procedures(OQ-10-4 输入①②③)·
foraging ×2 · prescription · processing · 评审报告 §5 行 3。

**残留/新欠**:W-1 会签(实现前)· 止血草/草木灰的 21a item_key + 9 注册表行(实现轮)·
processing「烧焦」择案(新边 vs 副产物)· campaign-arc §二 白细胞/§八 史实 → 40 ·
「通气 vs 心肺复苏」年代措辞 → 40 + OQ-10-4。**未获授权:commit。**

<!-- CONSISTENCY-CHECK: 2026-09-20 | GDDs checked: 35 | Conflicts found: 2 | Report: docs/consistency-report-2026-09-20.md -->

<!-- VISION-REG 2026-09-20 | 三板块梳理(剧情/非剧情/经济)落 campaign-arc.md「非剧情内容与经济愿景(P1a+ 登记节)」:P0=单向赠送、P1a 起以物易物+技能/声誉门槛、条件货币三型与 is_industrial=21a P1a 输入、帐篷不进 24 评分(P1a 房屋起评)。:31 故事句判为一致无需改。写回 52 OQ/22/24 = 各件下轮(登记在传播账)。 -->

<!-- VISION-REG-2 2026-09-20 | 用户原稿《时长估算与非剧情内容》逐条回录 campaign-arc.md ### 四~### 十(Opus 裁决形态:重排表+归属列,关键原话逐字块;〔〕占位已用原稿实值填满)。登记 D 项:① 本件「章 vs 幕」标题自相矛盾统一方向归用户;② 15-20 病种 = P1a+ 愿景,加行须先裁移出/提级上限;③ TR-disease-021 季节↔病种落点仍 gap 未解界。全部数字标愿景锚非承诺;tick 换算零登记(OQ-8/OQ-25-8 纪律不破)。 -->

<!-- CONSISTENCY-CHECK: 2026-09-20 (batch2 验证 20Hz/CAP24 批次) | GDDs checked: 35 | Conflicts found: 4 (🔴2 正文裁前措辞 + ⚠️2 stale 引用;数值零冲突) | Report: docs/consistency-report-2026-09-20-batch2.md -->

### 续做 · 2026-09-20(第二批:QQ-10 / QQ-12 / D-3 结案)

**QQ-10 ✅ 已结**:`docs/registry/architecture.yaml` 三个空脚手架键(`:486/:529/:838`)删除,
normalized-YAML diff 对删前快照 = 空(语义不变)。`architecture.md` D-3 行 + QQ-10 行已就地结案。

**QQ-12 ✅ 已结**:系统 3 的层分类矛盾(`systems-index.md` §3 :38 写 Core vs §6 :514 / 类目表 :165 /
`architecture.md` Axis A :233 写 Foundation)。订正方向 = **§3 改 Foundation**(三处多数口径为权威,
非「择一」);两轴消歧注块写入 §6 表头上方。`architecture.md` QQ-12 行结案。
⚠️ **操作事故(已当场修复,如实记)**:第一次编辑脚本用 `s.index("### ")` 定位注块尾,误吞 §6
推荐设计顺序全表(85 行);已自 HEAD 原样恢复,恢复后 `git diff` 只剩两处预期改动(9 行)。
教训:**按标记切片替换时,结束锚点必须是被替换文本自己的特征串,不能是「下一个任意标题」。**

**active.md 刷新**:本节即。QQ 台账残留(未结):QQ-07(用户裁定)· QQ-08/11/13(`/architecture-review`)·
QQ-14(OQ-10-9,45 轮)· QQ-15(W-1 会签)· `adr_divergence` 两条并流 QQ-11。

**下一步(不变)**:`/test-setup` Phase 6 报告(见会话输出)→ ② `/ux-design`(先裁无障碍路径冲突:
`docs/WORKFLOW-GUIDE.md` 根 vs `design/ux/accessibility-requirements.md`)→ ③④⑤ ADR #1/#2/#3 →
⑥ requirements-traceability 更名 → ⑦ `/architecture-review` → ⑧ `/art-bible` → ⑨ `/create-control-manifest` →
`/gate-check pre-production`。**未 commit(无用户指令)。**

### 续做 · 2026-09-20(第三批:/ux-design 完成 —— 门 ② 项落地)

**档位裁定(用户)**:**Standard + 两条显式限制**。

**新建 2 件**:
1. `design/accessibility-requirements.md` —— 路径冲突裁决 = **根 `design/`**(WORKFLOW-GUIDE:532 /
   gate-check:114 / ux-design:92 三处一致;`design/ux/` 下不建薄壳)。四轴矩阵 + 色-only 审计
   (面色通道 = 峰值问题,登记 `OQ-A1`)+ 测试计划 + **L-1**(P0 改键 UI 推迟,承 2026-09-16 裁定)·
   **L-2**(急救判定层 hold 不做 toggle,承 ADR-011 Amendment B;替代 = 判定宽容倍率,不碰 `L_input`)。
   `AC-13-F3` 的「无障碍件成文时须引用本条」义务**已兑现**(Platform 表读屏行)。
2. `design/ux/interaction-patterns.md` —— P-01…P-10 十条模式(只收**已由 Accepted ADR / Approved GDD
   裁出**的;每条带权威源 / 双导航路径 / 反例 / 未结 OQ)+ Gaps G-1…G-4(方笺第七张纸 / 建造手柄等价 /
   世界内的纸 / 被拒态的纸面表达)+ OQ-P1/P2。

**涟漪 4 处**(stale 路径引用清理):`design/CLAUDE.md:36` 订正 · `skeuomorphic-ui.md:1212` 改「已成文」·
`case-system.md:933` 的 `NOT-RUN` 改判「已裁:不承诺」(**引用 ≠ 验收**注已写)· `input-system.md:1309`
记录处置并保留「判据不引无障碍件」的原裁定。

**门的剩余距离(Required Artifacts 现为 11/14)**:③ ADR #1(渲染与场景加载 —— 全案唯一零覆盖 HIGH 域)
④ ADR #2(契约程序集清单,解 QQ-01 `Vector3` 僵局 / QQ-03)⑤ ADR #3(三流 Kind 单一真源,解 QQ-05)
→ 均走 `/architecture-decision`(逐条用户裁定流,不由我代拍)⑥ `requirements-traceability.md` 更名/建档
⑦ 重跑 `/architecture-review` ⑧ `/art-bible`(§1-4)⑨ `/create-control-manifest`。**未 commit(无用户指令)。**

### 续做 · 2026-09-20(第四批:门 ⑥ 落地 + ADR #1/#2/#3 事实核验)

**TR 台账陈旧值订正**:`traceability-index.md` 前言三条「裁定方就地翻转」所得 251/43 已被 C4 借绿回退覆盖
→ 改实测 **243 ✅ / 51 ⚠️ / 93 ❌**(registry `status:` 直接计数,与 §汇总「合计」行一致)。

**门 ⑥ `requirements-traceability.md` 落地**:**新建指针件而非改名**(index 一词被 36 处路径引用 / 22 文件,
涟漪 >> 建档)。RTM 诚实态 = **全链 0%**(Story 列前提 `production/epics/` 按阶段设计不存在;种子测试刻意
不绑 TR)。§Uncovered 内建 **6 条 Foundation gap 归属表**(`TR-itemdb-031`→回写 · `TR-randomevents-010`→ADR#2 ·
`-031`→ADR#3 · `TR-skill-008`→7a · `TR-concept-003/004`→范围件)+ 「门质量项当前 6 条不为绿」显式登记。
`traceability-index.md` 加消歧头(两产物两职责;数据真源两处都是 registry)。

**ADR #3 三处 Kind 登记全量核验**(集合运算实测,见 `architecture.md` §5.5 D-1 / §Required ADRs #3 / QQ-05):
并集 33 支;骨架 15 支 ∩ registry 24 = 11;ADR-007 的 5 支**零 registry 条目**;§二 具名清单 = 29。
**三处原口径经核验均为假**:①「缺口 9 支 / 单向」→ 双向(4 只住骨架 + 13 registry 非世界流 + 5 只住 007);
②骨架「4 支」→ 15(同件 §二 自陈「共 15」与自家矛盾);③ registry 流别注「5+3+16」→ 误。
另:`ConsequenceResolved` 自称经 Amendment 通道进 009,**该件末条 = L,通道无此裁**(Amendment M 不存在);
`entities.yaml:2002/2043/2077/2107` 四处「§三 9-Kind 骨架」陈旧。
**失败模式 = 两向都会死:按 registry 生成白名单拒收 9;按 §三 生成拒收 18。**

**ADR #2 核验**:QQ-01 原称的「门 A 引擎引用白名单冲突」**不成立**(ADR-017 §二 断言范围 = `Sim` 程序集;
两接口住 §4.2 L4)—— 收窄为唯一跨门消费点 `death-and-respawn.md:402`(`IPlayerMotor` 传送)。
QQ-03 因 `adr-017:251` `"references": []` 暴露**两种读法**(L4 唯一门面 + `InternalsVisibleTo` vs
`Sim` 自身即门面 —— 后者使「仅门面可调用」无约束力),交 #2 裁。QQ-02 = 逐字回填 ADR-005,不新开 ADR。

**ADR #1 引擎复核(unity-specialist lean 已回)**:无 blocker;7 项 spike 清单入 §Validation
(RenderGraph 迁移 / `InstantiateAsync` 在激活场景中卸载的引用陷阱 / 相机与 `AudioListener` 归 Boot /
`CatchUp`→`Step` 术语 / World.unity 零 gameplay GO / 未存 float 位置的呈现态生成规则 / 菜单未缩放时钟)。

**状态**:门 Required Artifacts **12/14**(余 ⑧ art-bible ⑨ control-manifest;③④⑤ = 三份 ADR 待写)。
**未 commit(无用户指令)。**

### 续做 · 2026-09-20(第五批:第四批订正 —— 二轮实测)

第四批的三处数**再被自己核验推翻**(同型失效:首轮订正用了名字粗分/单向计数,未做完整集合运算):
- **registry 流别**:世界 **12** / 病史 **7** / 病例 **5**(逐条按 `constraint:` 自述「落X流」归类;
  首轮订正的「病例5/病史12/世界7」把 `EnemyInjuryOnset` / `InjuryStateChanged` 记错流 —— 两者自述落**世界流**)。
- **§二 归属清单** = **29** 支具名(非「缺 8」的旧算):33 − 29 = **4**,与 #3 表第二行同组。
- **两侧被拒集**:registry 生成 → **9**(4 只住骨架 + 5 只住 ADR-007);§三 生成 → **18**
  (13 registry 非世界流 + 5 ADR-007)。原文「4 / 12」偏小,已就地改,D-1 行 / #3 块 / QQ-05 三处同步。
- **17 条拒绝表**已亲验为真且**实为 18 个谓词**(`random-events.md:1071-1079` 逐条数,「17」是文内自陈)——
  ADR #3 的 TR-randomevents-031 承载面成立,谓词数差 1 在 ADR 内如实登记。

**ADR-024 起草完成**(`docs/architecture/adr-024-kind-single-source.md`,**Status: Proposed**):
①真源 = `entities.yaml`(必填 `stream:`/`author:`/`payload_schema:`)②ADR-009 §三/§二 降级为注记
③追加通道改道(Amendment 通道退役)④补齐 9 支 + 4 支家规 + 4 处陈旧计数 + `ConsequenceResolved` 幽灵引据订正
⑤构建期生成器 `tools/kindgen/`(A1–A5 断言,不进构建;Roslyn analyzer 明确**不引入** = Alt E)
⑥17/18 条拒绝表落点登记(执行体归 ADR-014 阶段 2,不改 TR 文本)。
编号说明:**024 = 落盘时序,# 序 = Required ADR #3**;ADR-023 留给 #1(渲染与场景加载,随后起草)。
`architecture.md` #3 小节已加兑现件指针。**C3 在用户照准转 Accepted 前不结。**

**ADR-023 起草完成**(`adr-023-scene-lifecycle-rendering.md`,**Proposed**,#1 兑现件):
三场景制(Boot 常驻含相机+AudioListener / MainMenu / World additive **零 gameplay 对象**)·
拆序六步(S3 前提:UnloadSceneAsync 不销毁 InstantiateAsync 产物 → 断言引用归零)·
tick 驱动住 Boot 与场景解耦 + 加载期交互意图冻结门 · 表现态生成 = 格锚点 + 确定性整数哈希偏移 ·
P0 零 custom Renderer Feature + ADR-013 假设6 失败时的触发条款 · chunk 激活权 = 6(待用户照准)。
**复核修正的 3 处口径(Boot 相机矛盾 / CatchUp 术语 / 实例泄漏)已作为「复核已改的口径」小节如实入件。**
S1–S7 全未跑;S1/S3/S4 是转 Accepted 硬前置。**编号规则确立:023=#1 · 024=#3 · 025 将=#2(落盘时序编号)。**
`architecture.md` #1/#2 小节已挂指针。**未 commit。**

### 续做 · 2026-09-20(第六批:ADR-025 起草 = Required ADR #2 兑现件)

**先做出处置**:压缩快照里 `adr-025-contract-assembly-manifest.md` 出现在文件列表但可见记录无创建动作
(「验证 subagent 写入」教训的自家变体)。经 transcript 核查:line 9952 = **本会话压缩前自己的
Write 调用**,内容与盘上件同源(同款 `Performance Impactices` 笔误在盘上已修)—— 非幽灵写入,
是压缩切掉了记录。文件**出处确认,保留**。

**ADR-025 起草完成**(`adr-025-contract-assembly-manifest.md`,**Proposed**,#2 兑现件):
① 具名装配清单六项 + 工具族(`Sim` 引用集白名单升格为「恰 = {BCL, Sim.Contracts}」,承 ADR-017
构建失败级而不放松)② QQ-03 拆除「门面」二义 —— 推荐甲(`Fix` 保持 public + `ToFloat()` 调用点
白名单断言;乙案 `internal`+`InternalsVisibleTo` 列备选)③ QQ-01 推荐 ①′:传送契约拆两半 ——
整数半 `ITeleportCommandSink` 进 `Sim.Contracts`,浮点半留 `Gameplay.Presentation`(②破「仅 BCL」
不取;③自建 `Float3` 造第二浮点域不取)④ 清单封闭性断言(未登记 asmdef = 构建失败,与 ADR-024 A1 同构)
⑤ 测试装配落点 `Sim.Contracts.Tests` —— 种子测试编译前提 ⑥ QQ-02 **明示不入本件**(防清单 ADR
越权裁流内契约,ADR-007 教训)。
**如实登记的疑点**:Risks 第 1 行 —— `adr-017:251` 的 `"references": []` 在「Sim 必须见 SimEvent」
下不可能字面成立,Accepted 时须向 ADR-017 挂订正(V-6),不偷改。
称谓作废涟漪:「门面程序集」「独立契约程序集」两词的回写加注归回写轮(V-5 为完成判据)。
`architecture.md` #2 小节指针已从「未起草」改为「已起草 · 三待裁项」。

**Required ADRs #1/#2/#3 至此全部有裁决文本(三份全 Proposed)。C2/#3 结案 = 用户照准转 Accepted;
#1 另有 S1/S3/S4 spike 硬前置。** 剩余门项:⑦ `/architecture-review` 重跑(欠 `adr_divergence`×2 +
ADR 计数 19 文件 vs 22 登记 —— 023/024/025 落盘后此数已变,重跑时以盘上实测为准)⑧ `/art-bible` §1-4
⑨ `/create-control-manifest`。**未 commit(无用户指令)。**

### 续做 · 2026-09-20(第七批:逐份裁定轮 —— 三 ADR 全部转 Accepted)

用户「逐份裁定」,三 widget 轮全部照准:
- **ADR-023(#1)= Accepted·附条件**(①三场景拓扑照准 · ⑥chunk 激活权=6 照准 · 其余随全件;
  附条件口径 = S1–S4 实测回填是**实现故事**的前置,非裁决效力条件;S3 若被推翻断言零成本保留不改判)。
- **ADR-024(#3)= Accepted 无条件**(六项全收:真源 registry / §三§二降级 / Amendment 通道退役 /
  补齐+订正 / kindgen 生成器 / 拒绝表归 ADR-014 阶段2。无引擎前置)。
- **ADR-025(#2)= Accepted 无条件**(①清单照准**不改名** · ②QQ-03 = **甲案**白名单断言,
  乙案 internal+IVT 否决留档 · ③QQ-01 = **①′**拆两半,`ITeleportCommandSink` 进 Sim.Contracts,
  流事件通道备选未取 · ④⑤⑥随全件)。

**architecture.md 涟漪已落**:C2/C3 行改「✅ 结案」+ C1–C4 注改「四条全部结案」· #1/#2/#3 小节指针
改判 Accepted(含裁定内容摘要)· §2.0 骨架表**升为已生效命名列**(三旧称谓划线作废)·
A-1/A-2 行结案 · §4.1 标题「名已裁」· QQ-01/03/05 两表行全部改「已结」(QQ-05 曾叠出双 🔴 单元格,
已去重;A-1 行曾溢出第 4 单元格,已并回)· Required ADRs 头部:「#1/#2/#3 全兑现,**未兑现仅 #4/#5,
且二者不再是开工阻塞项**」· D-1 处置列加「裁决面结案;执行面 = Migration 待回写轮」。

**未做的回写义务(须用户批准批次,见会话尾)**:
① `entities.yaml` 按 ADR-024 Migration 补 9 条 Kind registry(+stream/author/payload_schema 必填字段)
+ 修 4 处陈旧计数(2002/2043/2077/2107)+ `ConsequenceResolved` 幽灵引据订正;
② ADR-009 §三/§二 降级注记;③ adr-017 挂 `"references": []` 订正(ADR-025 V-6);
④ 「门面程序集/独立契约程序集」四处原文加注(V-5:adr-005:228 · audio-system.md:242 ·
emergency-procedures.md:155 · persistence-service.md:577);
⑤ CLAUDE.md ADR 日志补 023/024/025 三条 + systems-index §11 注;
⑥ tr-registry 回写(TR-randomevents-010/031 的 adr: 字段挂 ADR-024/025 —— 回写轮逐条判,不预挂)。
**未 commit(无用户指令)。**

### 续做 · 2026-09-20(第八批:回写轮 ①–⑥ 全部执行 —— 用户 widget「现在全做」)

第七批登记的六项回写义务全部落盘(逐项 diff 已呈用户):

- **① `entities.yaml`** —— ADR-024 Migration 步 1–3:`SimEvent.Kind.*` 补 **9 支**(世界流 `Craft` /
  `DropSpawned` / `DropClaimed` / `DropDespawned` + 病史流 ADR-007 五支),载荷**逐字搬出处件**(V-3 diff 为空);
  **首批**带 `stream:`/`author:`/`payload_schema:` 三必填字段。`ConsequenceResolved` **幽灵引据就地订正**
  (原断言划线保留 · 有效身份 = 直接 registry 登记)。4 处陈旧「9-Kind」计数订正(:1941/:2002/:2081/:2112,
  :2002 保留被引字面 ⇒ V-4 grep 判据按「更正性引用」读)。YAML 实测 **33 支**。
- **② ADR-009** —— §二/§三 节首**降级声明**(「自 ADR-024 起不再是登记处…以 registry 为准 + V-1 断言失败」);
  世界流清单补 `ConsequenceResolved` 具名 + 「共 15」改历史计数注;**病史流补 3 支反向具名**
  (`CareApplied` / `CompoundTriggered` / `CompoundExpired` —— 此前只住 registry)。Amendment 追加通道退役(③)。
- **③ `adr-017`** —— :255 `"references": []` 挂 **V-6 订正注**(示例简写;现裁 = 恰 {BCL, `Sim.Contracts`};§一 结论不受影响)。
- **④ V-5 四处称谓加注** —— `adr-005:228`(`ToFloat()` · 现名 `Sim.Contracts` · 甲案白名单执法)·
  `audio-system.md:242` · `disease-simulation.md:772` · `persistence-service.md:577`(现名 `Sim.Codec`)·
  `emergency-procedures.md:155`(现名 `Gameplay.Presentation`)。**「门面程序集」「独立契约程序集」自此作废。**
- **⑤ ADR 日志 + 索引** —— `technical-preferences.md` 补 **ADR-023/024/025 三条** + 尾注改
  「001–025 全有条目 · C1–C4 全结 · Required ADRs 仅剩 #4/#5 非阻塞」;`systems-index.md` §11 加
  Required ADRs 兑现勾选条(三 ADR 裁定摘要 + 下一步 = `/gate-check pre-production`)。
- **⑥ tr-registry 两条翻转** —— `TR-randomevents-010` gap→covered(`adr: ADR-024 + ADR-025`,
  ⚠️「Roslyn」字面未采纳 = Alt E)· `-031` gap→covered(`adr: ADR-024`,⚠️ 17 vs 18 差 1 登记不改文本);
  两条均带**禁借绿**注(执行体归实现轮)。**连带计数同步(实测自洽)**:
  registry `status:` 计数 = **245 ✅ / 51 ⚠️ / 91 ❌**(恒 387)⇒ `traceability-index.md`
  (randomevents 行 13→15 ✅ / 17→15 ❌ · 合计行 · 头部实测注 · 变更历史新行)+
  `requirements-traceability.md`(Foundation gaps **6 → 4**,表内两行划线结案 + 门影响注改写为
  「此质量门当前不为绿 = 事实」)+ `architecture.md` §追踪基线块同步。

**残留(登记,未做)**:① 既有 24 支的三字段回填 = **kindgen A1–A4 编译前置,实现轮义务**;
② `tools/kindgen/` 生成器本体未写(实现轮);③ ADR-023 S1–S7 spike 全未跑(Validation 表未勾,禁借绿);
④ QQ-11 的 `adr_divergence` 两条(`TR-case-036` / `TR-interaction-015`)对齐归 ⑦ `/architecture-review` 复跑轮。
**未 commit(无用户指令)。**

**下一批(用户 widget 已批「⑦→⑧→⑨ 全推」,无逐步停)**:⑦ `/architecture-review` 复跑 →
⑧ `/art-bible` §1–4(门要件 `design/art/art-bible.md`)→ ⑨ `/create-control-manifest`
(`docs/architecture/control-manifest.md`)→ 终点的 `/gate-check pre-production`。

---

## 第九批 —— ⑦ `/architecture-review` 复跑(full)· 2026-09-20 · 判定 **CONCERNS**

**报告落盘**:`docs/architecture/architecture-review-2026-09-20.md`(第二份;上份 2026-09-15 = FAIL)。

### 已执行的文件改动(全部,含本批之前的 QQ-11 对齐)

| 文件 | 改动 | 性质 |
|------|------|------|
| `tr-registry.yaml` | `TR-case-036` `adr_divergence` 结案(**`adr: null` 保留 = 事实**);`TR-interaction-015` **`adr: null` → `ADR-011 + ADR-013`** | 2 个 `adr:` 值 + 2 条结案注;**零新增 / 零删除 / 零状态翻转**(恒 387 · 245/51/91)|
| `traceability-index.md` | 两行摘要列对齐 + :116 注块补 QQ-11 结案段 + **变更历史新行**(第九批)| 登记同步 |
| `architecture.md` | **7 处计数漏刷**(19 份→22 · 19/19→22/22 · §5.4 标题 93→91 · 52 簇 17→15 带翻转说明 · :1035 SceneManager 注 = 已由 ADR-023 兑现 · :866 扫描方法)**+ RC-8 标签就地修**(§5.4「4 交互」行前缀 `TR-interaction-*` → 真实 = processing 3 + combat 1) | 纯登记,无裁决面 |
| `requirements-traceability.md` | **§Coverage Summary 6 处计数**(243/93 → 245/91,百分比 63.0/13.2/23.5,合计行)+ 失效模式注 | 同上 |
| `diagnosis-system.md` | **8 处 D-A 追加注**(:10 · V-8.7④ · 前提注体 · :1315 · :1664 · :1961 · :1975 · :2005)—— 「联机精度取主机技能」在 2026-09-18 裁定 D-A 改判后正文仍留裁前断言 | 追加注,**零机制 / 零数值改动**;划线保原文 |
| `patient-ai.md:872` · `systems-index.md:647` | 同 D-A 注 | 同上 |
| `architecture-review-2026-09-20.md` | **新立**(本报告,11 节 + History) | 报告本体 |

### 判定与关键数字

- **CONCERNS**(非 FAIL):上轮 FAIL 两支柱均不成立 —— ① 22 份 ADR 间**零阻塞性跨件冲突**;② 依赖图**无环**,拓扑序覆盖 22/22。Required ADR **#1/#2/#3 全转 Accepted**(= ADR-023/025/024),TD 条件 **C1–C4 结案**。
- **不为绿的三件事**:① Foundation **4 条 gap**(`TR-itemdb-031` 可回写 · `TR-skill-008` 保持 · `TR-concept-003/004` = 范围声明**不该由 ADR 承接** ⇒ 要门绿须新增 `no-adr-by-design` 状态 = **gate-check 规格变更,用户裁**);② **12 项 P0 系统零 TR** ⇒ 「245 covered / 63.0%」是**偏高估计**;③ **3 项承重引擎风险**(RC-1 / RC-3 / RC-4)。
- **91 条 gap 的真相**:逐条分类后**真正需要新 ADR 的只有 12 条**(30 技能 7 + 13 写路径 5)。其余 75 条 = 数值轮 / 实测前置 / GDD 家规 / 登记未同步。

### ⚠️ 待用户裁 8 条(本轮一律未执行,详见报告 §9)

RC-1 `adr-013:203` 铁律 vs `:432` 自实现焦点回退(spike 最可能失败,失败即违规)·
RC-2 `adr-023` 状态串 `Accepted(附条件,见下)` + `:53`/:228` vs `:10` 效力口径 ·
RC-3 `adr-017:165` `noEngineReferences` **机制论据很可能为假**(结论不动,执法体只剩白名单断言)·
RC-4 `adr-012` F7 → 改住 `ulong`/`unchecked`,**BLOCKING spike 塌缩为表示选择** ·
RC-5 `adr-025`「恰 = {BCL,…}」是**断言非清单事实** · RC-6 `adr-023` ② 扫描挡不住 ③ 的 AudioListener ·
RC-7 `adr-014` 未 pin `DateParseHandling/FloatParseHandling = None`(⚠️ `"3/4"` 是合法 M/d 日期)·
S-4 `adr-023` S3 未测 **bundle refcount 泄漏**(第 6 步断言查不到它)。

### 两条自纠记录(报告内亦已写)

1. **撞号**:首稿用 C-11/12/13,与上轮 §13.2 **同号异义** ⇒ 改族 **RC-n**;撞号本身登记为 D-R1
   (报告 ID 族**无中央登记** ⇒ 每轮都可能撞)。
2. **数错**:§5.4 的「93 vs 91 差 2 归因不明」疑点**撤销** —— 表本来加总 91,是我第一版把
   `↓(原 17)` 注记数字也计了进去。**代之以 RC-8(簇名标签错)**。
   ⚠️ 教训 = `grep -c` 粗筛不可信,**一律 `yaml.safe_load` 逐条集合运算**(承「Verify Subagent File Writes」)。

### QQ-11 结案的方式推翻了一条元规则

两条 `adr_divergence` **判得相反**(`-036` = 摘要列过度归属 / `-015` = registry 漏登)
⇒ **「以 registry 为准」被证伪**;正确元规则 = **逐条回 ADR 的需求表核验,不预设哪边是真的**。
**现开放 `adr_divergence` = 0。**

### 下一步(承「⑦→⑧→⑨ 全推」)

⑧ `/art-bible` §1–4 → `design/art/art-bible.md`(门要件 3)→ ⑨ `/create-control-manifest`
→ `docs/architecture/control-manifest.md` → 终点 `/gate-check pre-production`。
**Foundation 门 4 条不为绿 = 届时如实报,不粉。** 8 条 RC 与 ①–⑥ 的 diff 清单**在本批向用户摊开**。
**未 commit(无用户指令)。**

---

## 第十批 —— ⑧ `/art-bible` §1–4 落盘 `design/art/art-bible.md`(2026-09-20)

### 本批性质与授权边界

- **Phase 2 only**(§1–4 = Visual Identity Foundation)= `gate-check Technical Setup →
  Pre-Production` 的必交件判据原文要求的**最低量**(「at least Sections 1–4」)。
  §5–9 **刻意留 `[To be designed]`**,不假装完成。
- **AD-ART-BIBLE 未跑** —— 该门在 §1–9 全完成后才跑(Phase 5)⇒ 本件**零签署**,
  文件头已写明。**不借 §1–4 的完成去声称美术圣经已签。**
- 草稿来源 = `art-director` 子代理。**子代理的草稿不是用户批准** ⇒ 凡超出锚点原文的
  主张一律带 `【本稿提案】` 标签,并在文末列 **AB-1…AB-8** 八项待裁。

### ⚠️ 本批最重要的自纠:我给自己的子代理下错了简报

**简报里写「four pillars」—— 本作有五根支柱。** `art-director` 反过来质疑了这一点,
复核 `game-concept.md:192-279` 后确认:**支柱五 = 史实为骨,架空为肉**(`:273`)。
落盘前已做的修正:① 前置声明表补齐第五行;② **`§1 P4「黄铜必须有来历」的服务对象由
「支柱四」改为「支柱四 ∩ 支柱五」**,并写明支柱五是**本件的硬天花板**(任何材质/器物/商路
存否的断言都须过 `O-6-13` 同型核验);③ 新增 `AB-5` 把《双材》的**支柱归属**显式呈用户
(`casebook.md` 与 `camera-and-viewpoint.md:101` 挂五,本件采「主归属三,来历判据归四∩五」)。
> **失败模式记账**:引擎/设计专家子代理会**反推简报前提**。简报里的**事实性断言**
> (数量、编号、归属)必须与原始权威件同屏给出,而不是我方转述。

### 落盘前逐条核验结果(全部命中,含三处口径订正)

| 引用 | 核验 |
|---|---|
| `game-concept.md:524-544` 锚点 / `:543-544` 色彩哲学 / `:721` P0 范围 / `:282` 反支柱 | ✅ 逐行命中 |
| `skeuomorphic-ui.md:43` 四元件(纸面/卷轴/墨迹/印章)· `:255` 焦点不得纯色 · `:295` 规则十三墨龄 | ✅ |
| `skeuomorphic-ui.md:1104` 灰阶 luminance Δ + 黑白截图夹具 | ✅(已在 §3.3 标为**比本稿形状义务更硬**) |
| `accessibility-requirements.md:98` 面色五通道 + 姿态/呼吸/脉案文字备份 + `OQ-A1` | ✅ |
| `accessibility-requirements.md:84` ≥4.5:1 + 「纸纹是纹理不是纯色」 | ✅ |
| `accessibility-requirements.md:85` 「不引入后处理全屏滤镜(撞 ADR-020 §六)」 | ✅ —— **§4 节首原把此口径误记在 :98 行,已就地订正**(该事实属 :85) |
| `diagnosis-system.md:1474` V-8.0「《双材》是机制语法,不是美术皮」 | ✅ —— 已按此在 §1.1 加一致性引注 |
| `world-and-ecozones.md:846` `O-6-13` 四区名待考据 · `:67` 四区完整布局 = P1a | ✅ |
| `technical-preferences.md` Draw Calls / Memory Ceiling 「待定」 | ✅ —— 已作为 §8 的显式前置 |

**唯一"像事实但不是事实"的一条**:`44×44` 手柄命中区底线 ——
`accessibility-requirements.md` **只有最小字号与对比度,无目标尺寸条目**。
草稿本已标 `【本稿提案】`,落盘时**加强**为脚注(写明系行业基线 + 本仓未登记 + 须进承诺表才生效),
并单列 **AB-3**。⇒ **未被伪装成既有约束。**

### 计数与口径纪律

- `SAT_BLOOM_COOLDOWN` 的秒→tick 换算按已裁 **20 Hz**(`TICK_SECONDS=0.05`)给出
  `≥8 秒 = 160 ticks`,并注明**该换算依赖 OQ-25-8 裁定**(改频率即失效)。
- 全部 hex 标 **sRGB · 提案值** + **禁直接抄进代码常量**(URP linear 工作流未定稿前);
  四旋钮只给范围不给定值 —— 承「数值用户自己调」。

### 文件

- **新增** `design/art/art-bible.md`(§1–4 成文 + §5–9 占位 + AB-1…AB-8 待裁表)
- `production/session-state/active.md` 本条

### 下一步

⑨ `/create-control-manifest` → `docs/architecture/control-manifest.md`(门要件)
→ 终点 `/gate-check pre-production`(**Foundation 4 条不为绿 = 如实报**)。
**未 commit(无用户指令)。**

---

## 第十一批(2026-09-20)—— ⑨ control-manifest 填满 + ⑧ 的 AD 门判处置 + 终点门开跑

### ⑨ `/create-control-manifest` —— 结案

- **发现**:该件**本轮之前已存在但是骨架**(11372 B / 21:45 落盘),Global Rules 段完整,
  **五个层的规则段全 `[待填]`**;**其创建过程未记入任何批次 = 出处缺口**(与 ADR-022
  「引用却无登记」同族,此处是「已存在而无记载」)。
- **提取方式**:先量工作面(22 份 ADR / 10217 行 / **430 条承载规则的行**),再派 fork
  `adr-rule-extract` 做**带行号的逐字提取**(约束:宁少勿造 · 行号必须实际读到 · 重复优于合并 ·
  冲突只打标签不裁决)。首轮回来的报告把**本已完整的 Foundation 段也重做了一遍** ⇒ 只取四层,
  并用其新补的 Foundation 证据**校验**既有段(结论:无实质遗漏,未重写)。
- **已填四层的规则量**:Core(tick / ADR-001 / 011 / 014 / 015 / 023 + Forbidden 11 条 + 护栏 3)·
  Feature(016 / 021 + Forbidden 6 + 护栏 3)· Presentation(013 / 018 / 019 / 020 + Forbidden 8 + 护栏 3)·
  Tooling(022 / 024 + Forbidden 5)。全文 **531 行**,`[待填]` 计数 = **0**(grep 实测)。
- **家规 4/5 的落地方式**:凡只有 ADR 小节标题行、无正文行证据者,**明标「源:`technical-preferences.md`
  ADR 条目(无 ADR 行号可引)」**;ADR-023 的 `[未验证前置:S<n>]` **不按条拆分**(件内未钉死规则↔S 编号映射)
  ⇒ 改为**整块前言**,并写明这是比逐条贴标更诚实的处理。
- **两条自相矛盾随条带进,不得单边引用**:① `adr-011:187` 强制 `FixParse` 在同件 `:190` **自评为不可执行**;
  ② `adr-013:203` 铁律 vs `:432` 自实现回退 = **RC-1**(未结案,`⚠️CONTESTED`)。
- **「Open Items Blocking Rule Promulgation」已填**(此前也是 `[待填]`):A = RC-1…RC-8 逐条 + 对本件的影响;
  B = 缺执法体项(kindgen 本体未写 / ADR-023 ② 扫描归属未定 / VR 90 fps **无引擎权威件** /
  **候选 RC-9** = `adr_divergence` 字面 2 vs 语义 0);C = 三件待用户裁(**不代拍**)。

### ⑧ 的 `AD-PHASE-GATE` 判词 → 6 条已处置

判词 = **CONCERNS,无 NOT READY 阻塞**(模型输出,**不是用户批准**)。处置:
1. **提案标签不完整(中)**⇒ 「数值纪律」前言改为**覆盖全部自造数值**(§2 的 K 值与墨↔铜比 /
   §3.1 的 32px 与 ≥60% / §4.3 的 −20/−40/−30% / §4.6 的 ≥12%),AB-2 清单同步扩全。
   ⇒ 此前 blanket 只覆盖 hex,**确有一片「被写成已定而其实未定」的残留面**。
2. **「人 / 兽」同格(中)**⇒ §3.1 **拆两行** + 立法作用域收窄为**人形敌人**;依据
   `game-concept.md:205`「野兽会吃你」+ `:212`「**不杀只约束人**」(独立核到原文,该判断成立)。
   新增 **AB-9**(兽是否允许被读成「可憎」/ 允许巨兽化 = 用户裁,**先于 25 / 27 敌人资产**)。
   ⚠️ 同批暴露我一处次生错误:原脚注「本表第二行与第四行同判」**按号不按名**,加行即漂移
   ⇒ 改为**行名引用**并留注(登记为失效样本)。
3. **滤镜句缺 ADR-023 ⑧ 闸(中)**⇒ §4 前言补「P0 零 custom Renderer Feature ⇒ 换表 + 禁滤镜
   **不是**其他滤镜效果已获准的许可」。
4. **44×44 声明复核(轻)**⇒ 独立核对为**真**(`accessibility-requirements.md` 只有 :82 字号 ≥24px
   与 :84 对比 ≥4.5:1,无目标尺寸行);另**独立复算 §4.5 的 11.8:1 = 11.81 通过**。
5. **三处 P0 不可达行(轻)**⇒ §2 附注补齐:联机同伴 / VR 急救 / 四区色温,并写明
   「**不是作废,是当前无验收面**;slice 用不到即不得先做资产」。
6. **「建筑尺度」防误读(轻)**⇒ §3.1 加注:弥漫性的形,**不是**楼一样大的遭遇对象;
   出现「巨兽型 Boss」需求即回裁支柱四。
7. **§8 留空 vs P0 基线(轻)**⇒ 新增 **AB-10**(建议先于内容启动补 §8,或为 slice 显式灰盒豁免)。
- 门判词已写进件头,并标「**门判词 ≠ 签署**」+「模型输出,不是用户批准」。

### 前一轮的 `:85` 误标 —— 已按实测改回 `:98`

上一批记的「正确行 = `:85`」**本身是错的**(本轮 `sed` 实测 `:85` = 「亮度 / gamma 控制」行)。
「不引入后处理全屏滤镜」的真实位置 = **`:98`**(色盲模式行)⇒ art-bible 现指 `:98`,
**上一批的记录不成立,以此条为准**。另 `:181` 实测 = 「纸纹上文本对比」行,art-bible 引用正确。

### 终点门 `/gate-check pre-production` 的机械谓词(全部自行实测,不靠推断)

- 门要件 **13/14 齐**;`src/` 只有 `CLAUDE.md`(本门不要求);**无 `.asmdef`** ⇒ 种子测试
  **存在但不被编译**(如实记)
- **Engine Compatibility**:22/22 有该节;**版本戳字面命中 21/22** —— `adr-024` 全文无「6.3」字面,
  但其节内 `References Consulted` 指 `VERSION.md` ⇒ 记「**引用式钉版,非字面钉版**」,**不改 ADR**(归用户裁)
- **弃用 API**:22 份内**零**作为选型使用;`deprecated-apis.md:28` 的 `Canvas` 与 ADR-013 双栈的分歧
  已在 manifest 以 **ADR-013 为准**登记为 deviation(是否回写 engine-reference = 用户裁)
- **依赖图**:程序化重抽(正则 + DFS)⇒ **无环**(78 条边)
- **Foundation 域 20 条 = 15 covered / 1 partial / 4 gap**(`TR-itemdb-031` 掉落实体世界状态事件化边界 ·
  `TR-skill-008` 技能成长存档持久化 · `TR-concept-003` MVP 的 8 条定义 · `TR-concept-004` P0 排除项清单)
  ⇒ **判据「zero Foundation layer gaps」不为绿**;评审件 §2.1(`:102-109`)已逐条定性
  (登记回写 / 保持 gap = Required ADR #4 的症状 / 两条是**范围声明**,要绿须 `no-adr-by-design`
  新状态 = **gate-check 规格变更**)
- `production/stage.txt` **不存在**;`production/review-mode.txt` **不存在 ⇒ lean**(四门全跑,TD-MANIFEST 跳过)

### 门判词派单纪律(本轮偏差,登记)

记忆条 = **Opus 复核一次只起一个**(并行 429)。本轮在 `TD-PHASE-GATE`(opus)未归时起了
`CD-PHASE-GATE`(opus)⇒ **两条 opus 并行,是偏差**。AD 用 sonnet(不占该条)。
**`PR-PHASE-GATE` 待前两者归队再起。**

### 文件

- **填满** `docs/architecture/control-manifest.md`(骨架 → 531 行,`[待填]` = 0)
- **修订** `design/art/art-bible.md`(AD 门判 6 条处置 + `:85`→`:98` + AB-9 / AB-10 + 件门头注)
- 本条

### 下一步

收齐 CD / TD / PR / AD 四判 → 出 `Gate Check: Technical Setup → Pre-Production` 报告
(**CONCERNS 预期;Foundation 4 条如实报不粉**;Phase 5a 反自查 ≥2 条 `[TOOL ACTION]` 的证据已备好)
→ **不写 `production/stage.txt`**(仅 PASS + 用户确认可写)→ 把 ①–⑥ diff 清单 + RC-1…RC-8 +
AB-1…AB-10 作为**用户裁事项**摊出(标注:建议 ≠ 批准)。**未 commit(无用户指令)。**

---

## 第十二批(2026-09-20 夜,终点门·上)—— TD 判词落地 + 门谓词全量实测补录

### TD-PHASE-GATE 已归:**CONCERNS(无 FAIL 项)** —— 要点

- Foundation「zero gaps」判据被 TD 判为 **mis-specified,非项目不达标**:用户若拒绝改判据,
  该条**永久 CONCERNS-by-construction,仍非 FAIL**。
- TD 要求**转段前清两条**:**RC-1**(评审件与 registry 文本对齐)+ **RC-2**(status 串归一)——
  RC-2 的机理本轮实测坐实:`create-control-manifest` SKILL.md:29-30 是
  「Filter to only Accepted ADRs (**Status: Accepted**)」的**字面**判据,而
  `adr-023` 的 Status 行实测为 `Accepted(附条件,见下)` ⇒ 字面精确匹配**掉这一份**
  (manifest 已含 ADR-023 是因为本轮手写,不是技能重跑能复现)。**这是下一道门的真实失效模式。**
- 三件单看非阻塞的事实(art-bible §1–4 only / 种子测试不可编译 / 无 Unity 工程)
  **合并为一个后果**:过门时**仓库从未执行过一行代码** ⇒ 垂直切片前须插**工装周**
  (工程根 + ADR-025 六装配 + ADR-012 矩阵 + R-A/R-C spike),且 **R-A(手柄焦点桥)须先于切片** ——
  它决定「无血条/无小地图」支柱是否根本可实现。
- TD 的最小到 PASS 拆分:**改门规格 1**(`no-adr-by-design`)/ **改 ADR 1**(Required #4,`TR-skill-008`)/
  **仅回写 registry 1**(`TR-itemdb-031` 的 `adr:` 字段)。

### 本轮新实测(全部 `[TOOL ACTION]` 级证据,供 5a 反自查)

- **Status 串逐份抽取**(awk 取 `## Status` 后首个非空行):22 份中 **21 份恰为 `Accepted`**,
  唯一例外 `adr-023 = Accepted(附条件,见下)` ⇒ 上条 RC-2 坐实。
- **`deprecated-apis.md` 全文读毕**:其 UI 节把 `Canvas (UGUI)` 列为 deprecated(注「still works,
  recommended 是 UI Toolkit」)—— 与 **ADR-013 双栈裁决**(world-space/VR 必须 UGUI)冲突;
  **行号按本件实测 = `:28`**(旧记录同值,核毕)。ADR 侧 grep:除 UGUI 外**零弃用 API 作选型**;
  `Application.LoadLevel` 仅在 adr-023 以「**不用**」身份出现(`:51`/`:54`,自陈弃用检查通过)。
- **Foundation gap 四条逐条 dump**(yaml.safe_load):全部 `adr: None`;`TR-itemdb-031` 的 note
  自陈「边界由 ADR-009 §五 + ADR-015 §三 定,状态重裁待下轮」⇒ **实质有覆盖、登记无链接**,
  即 TD「仅回写 registry 1」那条;另三条 note 空。
- **registry 复算**:387 条(id+status 节点),**245 covered / 51 partial / 91 gap**,零重复 id
  ⇒ `requirements-traceability.md` 头部数字与实测一致。⚠️ 但 `architecture.md:23`(C4 行)
  自陈「实测 **243**/51/**93** 两文件一致」—— 与现值差 2(方向:两条 gap→covered 的后续回写未同步该行)。
  登记为报告注脚,不改文件。
- **Knowledge Risk 分布**:6 HIGH / 6 MEDIUM / 10 LOW(22 份);HIGH = adr-001/005/011/012/013/023,
  全部在 `architecture.md:44` §HIGH RISK 域与各自 spike 条款有落点。
- **UX 逐屏规格**:`design/ux/` 仅 `interaction-patterns.md`(P-01…P-10 + Gaps 节,155 行)——
  **无任何逐屏 spec**(主菜单 / 脉案屏均无)⇒ 质量判据「at least one screen's UX spec started」**不为绿**。
  注:本作 HUD 是**刻意不存在**(diegetic),缺的是主菜单档。
- **8 节结构件**:Engine Compatibility 22/22 · GDD Requirements Addressed 22/22 · ADR Dependencies 22/22。
- **`.asmdef` = 0**(`find` 复测)· `tests/` 十件含种子测试 `tests/unit/sim/sim_fixedpoint_test.cs` ·
  workflow `tests.yml` 在。

### 派单时序

TD ✅ 归 → AD ✅ 归(sonnet)→ **CD 在跑** → PR 待 CD 归队后起(承「一次一个 opus」;
第十一轮已记两条并行的偏差,本批不再犯)。

---

## 第十三批(2026-09-20 深夜)—— CD 判 **NOT READY** · 两条阻塞经我逐字复核(各漏读下半句)

CD(催报后交付,opus)判词 = **NOT READY**,四条支柱面核验为**通过**(支柱一的机械保障
`AC-44-09` 白名单 / 支柱二的 `INJ_COMA` 归零非死亡 / 支柱四「建筑尺度」防误读 /
支柱五「已声明待核验」的纪律成立)。阻塞两条 + 体验面若干。**两条我都 `sed` 读了原文**:

| CD 的断言 | 实测 | 差在哪 |
|---|---|---|
| `skill-system.md:552` 「仍写跳过=同等经验」 | **字面为真**,勾选项主文确实未改口 | `:553-557` 紧邻回刷块**已**写明它与 R-6 冲突、「否则实现期按本行 = 复刻被裁掉的『永远跳过』最优解」,并落「**待 30 侧落盘 / 或就地改口 —— 待裁定**」⇒ 不是「无人发现」,是**已登记、待用户裁改法** |
| `combat-and-weapon-lines.md:1218-1220` 「`¬Down` fail-open,`OQ-25-3` 自标不阻塞」 | **两条都为真**(`:1186` 确写「不阻塞本 GDD」) | 但 `:1220` 后半句正是「**须在实现顺序里登记为前置**」⇒ CD 要求的动作**文档已要求自己登记**;真缺口 = 全仓**没有承载「实现顺序」的物件**(`production/epics/`、`sprints/` 均不存在)⇒ 属**排期缺陷**,非「支柱默认态被定义为违反自身」无人知晓 |

**我的处置口径**:两条**不就地改文件**(① 改法本身是待裁事项「就地改口 vs 等 30 轮」;
② 把 `OQ-25-3` 的「不阻塞」翻成阻塞 = 替用户改判据)。报告里按 `gate-check` 规则
**NOT READY ⇒ 最低 FAIL**,并把「用户可以显式承接」的路径摊开,**不自行降级、也不自行升级**。

### 门谓词的最终计数(逐条实测,已定稿)

- **Required Artifacts:13 / 13 全在件**(技能内该门该节恰 13 条,`awk` 枚举);
  第 8 项「example test 功能性」带 ⚠️:文件在、**零 `.asmdef` + 无工程根 ⇒ 从未执行过**。
- **Quality Checks:8 / 10**(清单 10 条)
  ✅ 核心系统覆盖 · 无障碍档位 · Engine Compat 22/22 · GDD Requirements 22/22 · HIGH RISK 域有落点
  ❌ **Foundation zero-gaps**(4 条)· ❌ **逐屏 UX spec 一份都没有**
  ⚠️ 性能预算(Draw Calls / Memory = 「待定」)· ⚠️ **弃用 API**(ADR-013 正当地用了
  `deprecated-apis.md:28` 列的 `Canvas (UGUI)`,engine-reference 与 ADR 互斥)
- **Engine Validation:2.5 / 3** —— Knowledge Risk 标注 ✅(6 HIGH 全在件)/ 弃用引用
  ⚠️(同上一条,评审件 `:453` 自称「零弃用」= **与 engine-reference 的口径不一致**)/
  版本一致 ✅(21/22 字面 + adr-024 引用式)
- **C4 行的 243/51/93 不是陈旧,是历史事实** —— 我先前判错了:`:1003` 明写「**8 条统一 covered→partial**」
  ⇒ 8 条全进 partial ⇒ 91 gap = 93 − **2**(第八批 `TR-randomevents-010` / `-031` 两条 gap→covered)。
  两数分属两个时点,恒 387 不变。**报告里不再记为不一致。**

### 下一步

PR(opus,已起,**未归**)→ 收齐后出 `Gate Check: Technical Setup → Pre-Production`
(机械侧 **CONCERNS**;CD 的 NOT READY 依技能规则把地板抬到 **FAIL**,除非用户显式承接)→
**不写 `production/stage.txt`** → Phase 7 收尾 widget(FAIL 形态)。**未 commit(无用户指令)。**


---

## 第十四批(2026-09-21 〆)—— PR 判词归(CONCERNS)· 四判齐 · 终点门报告已出

### PR-PHASE-GATE = **CONCERNS**(首跑 400 崩,重跑交付;其自报「本机无 Unity」我已独立复测坐实)

- 复测:`unity/unity-editor/dotnet/mono/msbuild` **全部 MISSING**、`/opt/Unity*` 无、
  全仓无 `ProjectVersion.txt` / `Packages/manifest.json`、`tests.yml:28` = **占位版本
  `6000.3.TBDf1`** ⇒ PR 的 **U0a(工具链核验)先于一切** 是实测事实,不是推测。
- PR 引用逐条抽验:①「25 与 9 同程序集」**为真**(adr-025:106 `Sim` 行自陈「25 与 9 同程…」;
  出处 = combat:399 R19b 的「若同程序集则一行回指 AC-5」)⇒ 其「#2 在构建层近乎不可能」的
  归位成立。② `OQ-1-12` = 接地模型 spike、原文「P0 开工前须裁决」(player-controller:6/:216)为真。
  ③ 21b 考据量「未估算」为真(systems-index:213)。④ OQ-48-8 / OQ-42-5 六屏闭集为真。
- PR 对 TD 工装周的修正:**量**从 1 周改口为「真债 10–20 pd / 含 CI+原型门 18–33 pd ≈ 4–7 周」
  (三因:六装配真编译 / 许可证日历 / 硬门四组非两组)⚠️ **规范值非实测值**(零速度数据)。
- PR 把 **OQ-10-12 归它推荐** = 「做,并入 U1 第 4 条(3–5 pd),判据须可证伪
  (`L_input < 50 ms` 实测 + 抖动门 11× 分档),**不得判『手感好』**」。
- PR 留一道**解释题**给用户:工装块是否占 6–9 月窗口(占内 ⇒ 31 项只剩 4.3–8 月;不占 ⇒ 时钟未起跑)。

### 四判汇总与终判

TD CONCERNS · PR CONCERNS · AD CONCERNS · **CD NOT READY** ⇒ 按 gate-check 规则
(NOT READY → 最低 FAIL,用户可显式承接)终判 = **FAIL(可承接)**;
承接 CD 两条后落 **CONCERNS**(残 = Foundation 2 ❌ + 预算/弃用 2 ⚠️ + UX spec ❌)。
CD 两条我均已复测:各漏读紧邻下半句(552 回刷注 / 1220 前置义务自陈)⇒ 实质 =
「已登记、待裁改法 + 无排期物件」,报告如实并陈,不代降不代升。

### 未写 / 未做

`production/stage.txt` **未写**(非 PASS);**未 commit**;未改任何 GDD/ADR 文本。

### 下一步(等用户裁)

①承接/清零 CD 两条 → ②RC-2 归一(`adr-023` Status 串,否则下一道 create-control-manifest 静默丢 ADR-023)
→ ③OQ-10-12 原型门照准/否 → ④6–9 月窗口解释题择一 → ⑤⑥⑦…(RC-1…RC-8 / AB-1…AB-10 /
门规格 no-adr-by-design / deprecated-apis UGUI 回写 / Required #4/#5)按报告「用户裁事项」清单摊出。

---

## 第十五批(2026-09-21 · 用户四项裁定中的三项落地 —— RC-2 / 门规格 ◆ 态 / 排期口径)

### 已落地的裁定

1. **RC-2 状态串归一** ✅:`adr-023` Status `Accepted(附条件,见下)` → 字面 `Accepted`
   (「附条件」只留正文)。awk 实测**全仓 22/22 ADR 状态串均字面 `Accepted`**。
   修的是 `create-control-manifest/SKILL.md:29-30` 字面过滤**静默丢弃 ADR-023** 的工具链破坏。
2. **gate-check 门规格新增第四态 ◆ `no-adr-by-design`** ✅ 端到端:
   - `.claude/skills/gate-check/SKILL.md` Foundation 条目加注(◆ 不计缺口;记「判据 mis-specified,不改实质裁决」)
   - `tr-registry.yaml`:`TR-concept-003` / `-004` `status: gap → no-adr-by-design` + 注(永不因补 ADR 转 ✅;归属件被推翻则转 ❌ 重裁)
   - 计数四处同步(registry 实测**自洽 245 ✅ / 51 ⚠️ / 89 ❌ / 2 ◆ = 387**):
     `traceability-index.md`(读法表 ◆ 行 + header 注 + concept 行 8|5|0|1|◆2 + 合计行 + 431/432 明细行 + 变更历史新行)·
     `requirements-traceability.md`(:8 :22 覆盖表 :46 新 ◆ 行 :52 恒等式 Foundation 章节 6→4→2 与 89−2=87)·
     `architecture.md`(:81 计数块 + 追加第四次变动注 + §5.4 标题 91→89 + 「开方外残余」7→5,**`TR-concept-006` 明示不转 ◆**)
   - **Foundation 层实测** = 15 ✅ / 1 ⚠️ / **2 ❌**(`TR-itemdb-031` / `TR-skill-008`)/ 2 ◆
     ⇒ gate-check 的「zero Foundation layer gaps」质量项**残 2 条不为绿**(两条均「有裁决面缺执行体」)。
3. **同批 TD/PR 均指为 0 复利的登记层漏刷**:`TR-itemdb-031` `adr: null → ADR-009 + ADR-015`
   —— **只补指针不翻 status**(其原口径「状态重裁随 ADR-009 TR 全量复核轮」未撤回,禁借绿)。
4. **排期口径裁定 = 「占窗口」** ✅ 落 `systems-index.md` §P0 工期基线块:工装块(U0a+U0+U1,PR 估 18–33 pd ≈ 4–7 周)
   计入 6–9 个月内 ⇒ **31 项内容窗口 ≈ 4.3–8 个月**。**明写这是口径澄清不是重算**,基线值未动、PR 估算不作承诺。

### 曾开的唯一实质裁决:CD NOT READY 两条 —— ✅ 已于同日照裁(见第十六批)

opus 辅助判断已回(用户 widget 选「交 opus 辅助判断」= 只买判断辅助,不含裁决);
**后续裁定 = 甲案「承接 + 三条件」**,三条件已落文件 ⇒ 终判 **FAIL → 承接 → CONCERNS**。

### 未写 / 未做

`production/stage.txt` 未写(非 PASS);**未 commit**;未改任何 GDD 机制数值;未勾任何 spike / Validation 框。

---

## 第十六批(2026-09-21 · CD 承接裁定落地 + gate 报告落盘 —— 本门收口)

### 用户裁定(widget 两问)

1. **CD 两条 NOT READY = 甲案「承接 + 三条件」** ⇒ 面板终判 **FAIL →(显式承接)→ CONCERNS**。
2. **收尾 = 落盘并收口**(不 commit;下一批待裁事项在报告末表)。
   ⚠️ opus 的「建议甲附三条件」只是判断辅助,**裁决权与裁决文本在用户**;条件本身经用户照准。

### 三条件逐条落地(均已写文件,机制数值零改动)

| 条件 | 落点 | 要点 |
|---|---|---|
| ① `OQ-10-12` 限期收窄 | `emergency-procedures.md:1043` 行 | 限期列 `P0 排期时` → **「写 10 的第一行代码前」**;判据 = `L_input<50ms` 实测 + 抖动门 11× 分档;**原型不判「手感好」** |
| ② 30 的「同等经验」改口 | `skill-system.md` :468 边例 / :552 勾选项 / 文件头「最后更新」 | 两处**就地改口 + 留原文**(被裁掉的最优解留痕);**两处勾选项不记绿**(禁借绿) |
| ③ `¬Down(g)` 升为断言 | `combat-and-weapon-lines.md:1221+` 注体 | 「登记进实现顺序」物化为 **`Sim` 构建期存在性断言**(缺 `Down` 成员 = 编译失败,禁 `Debug.Assert`);⚠️ 只锁机制,**接口具名归 9 下一轮**;**不结案 `OQ-25-3`** |

### 报告落盘

`production/gate-reports/2026-09-21-technical-setup-to-pre-production.md` —— 13/13 artifacts ·
8/10 quality · 四判汇总(TD/PR/AD CONCERNS + CD NOT READY→承接)· Blockers 3 条 ·
U0a→U0→U1 建议序 · **Phase 5a Chain-of-Verification 5 问(3 问走 TOOL ACTION)**,
其中 **Q3 实测推翻我自己早先草稿的「Foundation 全 covered」**(registry `domain: Foundation`
真值 = 15 ✅ / 1 ⚠️ / **2 ❌** / 2 ◆)⇒ 终判如实写「revised」。

### 仍未写

`production/stage.txt` **未写**(本门非 PASS 直落 + 规则要求 PASS + 用户确认);**未 commit**。

---

## 第十七批(2026-09-21 · 用户 widget 四组全做 —— gate 报告六行裁项表行 4 之后)

承 gate 报告收尾 widget:「① RC-1/3–8 + S-4 全批修 · ② AB-2/3/4/5/9/10 六条 · ③ UGUI 回写 + a11y 解块复查 · ④ 四收尾小件」—— **四组全部执行完毕,本轮落盘、不 commit**。

### ① RC-1/3–8 + S-4 全批修(7 处 ADR 正文修订 —— 承 architecture-review-2026-09-20 §9 建议,零新裁决)

| ID | 落点 | 修法 |
|---|---|---|
| RC-1 | `adr-013` | 焦点铁律从「全栈禁自实现」收窄到**对外契约面**(spike 最大失败点不再天生违规) |
| RC-3 | `adr-017` | `noEngineReferences` 机制论据归因订正(结论不动,执法体 = 白名单断言) |
| RC-4 | `adr-012` | F7 BLOCKING spike **降级为 `ulong`/`unchecked` 表示选择**(splitmix/Q16.16 乘积过 UB 线的风险被表示层吸收) |
| RC-5 | `adr-025` | 「恰 = {BCL,…}」标注为**断言非清单事实**(不假装已机械核验) |
| RC-6 + S-4 | `adr-023` | ② 扫描判据**收紧 + camera/AudioListener 禁令显式并入**;③ S3 **bundle refcount 归零断言**(第 6 步断言改查 refcount,查得到 S3 型泄漏) |
| RC-7 | `adr-014` | pin `DateParseHandling`/`FloatParseHandling = None` + **负向夹具**(`"3/4"` 是合法 M/d 日期 ⇒ 不收) |
| RC-8 | `architecture.md` §5.4 | 簇名标签错(文本级,前数已修) |

a11y 解块同步:2 行一次性修正(见 ③)。

### ② AB-2/3/4/5/9/10 六条 —— **全部已裁并落盘**(widget 逐条照准)

| AB | 用户裁 | 落点 |
|---|---|---|
| AB-2 | **全批为基线方向** —— 定性判据立即升为已裁方向;全部自造数值归数值轮保持提案 | `art-bible.md` header 裁定块 |
| AB-3 | 44×44 手柄焦点落点底线**升为无障碍承诺**(Standard 档,高于 WCAG 2.2 AA 的 24×24) | `accessibility-requirements.md` §Motor 新行 + `art-bible.md` §3.3 命中区行 |
| AB-4 | 纸面正文对比承诺 **≥4.5:1 → ≥7:1** | `accessibility-requirements.md` :84/:181 + `art-bible.md` §4.5 |
| AB-5 | 《双材》**主归属三、来历判据归四∩五** —— 确认并加注 | `art-bible.md`(:19 裁定块)+ `casebook.md`(:6 后)+ `camera-and-viewpoint.md`(:101 后)三处 |
| AB-9 | **巨物化门对兽不设禁** —— 兽另立立法,「失序非可憎」对兽不同判;人形四行不动 | `art-bible.md` §3.1 兽行注块 + AB 表 |
| AB-10 | §8 Asset Standards **前置补写** = Pre-Production 内容启动前置强制项;slice 灰盒豁免不再隐式宽限 | `art-bible.md` header + AB 表 |

### ③ UGUI 回写 + a11y 解块复查

- `docs/engine-reference/unity/deprecated-apis.md` UI 段重写:UGUI 由「待迁移」回写为 **P0 第二栈**(UI Toolkit 主 + UGUI 补 world-space/XR,承 ADR-013);注明「只影响本项目筛选器,通用迁移建议保留」。
- `design/accessibility-requirements.md` :83 过度声明订正(「42 已立七员闭集」→「第七员登记 = `OQ-48-8` 待办」)+ :84/:181 提档 + §Motor 新行。

### ④ 四收尾小件(三件已落 + 一件建议登记)

1. **Required #4/#5 非开工阻塞登记** —— `systems-index.md`:归 30 实现轮(#4)/ 13·10·4 归属争议(#5);不新立系统、不进本轮门面,Disposition = 纳入实现轮。
2. **D-21b-3 考据量补估结案** —— `systems-index.md`:`0.5–1 pd/单人一次性`,S 档,不重算 6–9 月基线。
3. **per-screen UX spec 首件选页建议** —— `skeuomorphic-ui.md:1477` 注 + `tutorial-and-onboarding.md` OQ-48-8 行:教学纸近景(`ModalId.PaperCloseup48`)。
4. **OQ-48-8 保持 open**(42 修订轮义务;`AC-42-F1` 闭集仍六项,`PaperCloseup48` 第七员登记 = 待办,未裁前不开闭集)。**NOT-RUN 纪律不变**。

### 清理项(本批收尾)

- `tutorial-and-onboarding.md` :502「✅ 2026-09-21 赌注」行 —— **笔误「赌注」**且位于表格行后打断 OQ 表结构,已修订:改为独立 blockquote「选页建议(承 gate 报告行 6)」注块,置于 OQ 表之后、与 OQ-48-8 行互补(行内仍保留短建议)。

### 文件清单(本批全部落盘)

RC 七件(`adr-013/017/012/025/023/014` + `architecture.md`)+ `deprecated-apis.md` +
`accessibility-requirements.md`(4 edits)+ `art-bible.md`(5 edits)+ `casebook.md` +
`camera-and-viewpoint.md` + `systems-index.md`(2 edits)+ `skeuomorphic-ui.md` +
`tutorial-and-onboarding.md`(2 edits)。

### 下一步

gate 报告六行裁项表至此**行 4 全清**。未 commit(无用户指令)。

---

## 第十八批(2026-09-21 · 用户「继续下批,没有需要我选择的时候不要停」—— art-bible §8 前置补写)

**判据**:AB-10 已裁「§8 待补」= Pre-Production 内容启动的**前置强制项**,是越过已全清的
六行裁项表之后的唯一已裁定未交付件。§8 自述须 `art-director` + `technical-artist` 并行。

### 执行形态

- 两员均为 **sonnet**(核对 frontmatter),**并行**起草不撞「sequential Opus」记忆;
  后台异步起稿,等待期间不做重复工作。
- 两稿返回后**核验子代理未写文件**(`git status` + `ls design/art/`,确认仅 `art-bible.md`
  被改、无幽灵资产文件)。
- 然后于 `art-bible.md` 就地落盘 §8(标题散点逐个 Edit,非整节替换)。

### art-bible.md §8 落盘内容(合稿)

- **§8.0 强制项登记**:硬闸一 = ADR-023 ⑧(P0 零 custom Renderer Feature + 下位禁则:禁
  自定义/Shader Graph shader · 墨铜禁实时材质融合);硬闸二 = AB-1 校准前置;不可为未验收面
  先做资产。
- **§8.1 文件格式与单位坐标系**—— .tga/.png/.fbx/.exr/POT 图集 · BC7(color)/BC5(normal)
  (压缩定值归构建面)· 1 unit=1m · sRGB/linear/HDR 色空间 · 几何级禁则栅栏。
- **§8.2 命名规范**—— `[族]_[语义]_[变体?_nn]_vNN` 六族前缀 · 禁中文/空格/混排 · 后缀标记。
- **§8.3 纹理分辨率分档**(提案)—— 角色 2K / 建造 1K / 医馆 2K / 道具 512 / UI-纸面 1K 图集
  ≤2K / 粒子 512 POT;UI 纸面关 mip · 最亮像素验收 · VR 规格不在此表。
- **§8.4 LOD 期望**—— 切换哲学 = 缩略可读性;LOD 禁动「形状语法」(剪影测试灰盒测不了)。
- **§8.5 导出设置哲学**—— 枢轴/朝向/法线/UV2/格对齐(容差 ≤1mm)/接缝/S²O 拒收判据表。
- **§8.6 材质槽位与 shader 分配**—— URP Lit 唯一主 shader · 禁 Simple Lit/自定义/Shader
  Graph;SRP Batcher 硬规则(禁 new Material · 禁小块差异复制变体);光照预算;墨↔铜材质图
  裁向(**两稿冲突就地吸收**:槽位差异 角色≤3/病人≤2 采最低共项,已呈注「可回裁」)。
- **§8.6A AB-1 校准前置**(仅登记不执行)—— 两步走 + 「待校准/已校准/进常量日期」三栏。
- **§8.7 灰盒政策(承 AB-10)**—— 定义 + 5 判断点 + 判定者 + 「灰盒≠免检」。
- **§8.8 灰盒替换验收清单**—— 可省/不可省下限 + 替换三查(对比/性能/命名残留)。
- **§8.9 引擎硬约束档位索引**—— §8-9A 面数 / §8-9B 显存+图集 / §8-9C SRP 约束 /
  §8-9D 导入器硬规则 / §8-9E 定稿判据(唯一 = 最低目标硬件 + 病人群集 slice 实测)。

**§8 节首纪律声明**:全节档位/上限/区间 = 提案值,定稿判据唯一 = 最低目标硬件清单 +
≥1 个含病人群集场景 slice 实测帧时间;两者同时满足才触发修订;此前任何数值不得进代码
常量/导入预设/烘焙数据。承「数值用户自己调」,本件不代拍。

### 清理项

- 落盘过程中自纠三处结构问题:`§8-9E` 行内 `^` 符号 → 直述;**8.6AB → 8.6A**(删除「B」
  使标题/正文引用/AB 表三处同名牌);§9 尾残留的重复「本批未决」表头与占位行两处删除。

### 一致性扫描(落盘后 Bash 核验)

- technical-preferences 两条待定预算(:54-55)保持「待定」未动;
- 印泥红/灰褐阴影/皮肤铜绿/7:1 对比 全件与 §1–4 既有口径一致;
- ADR-023 ⑧ / ADR-014 烘焙换表引用正确。

### 文件清单(本批)

`design/art/art-bible.md`(头版 0.1→0.2 + §8 全节 + AB-1/AB-8/AB-10 三行状态)。

### 下一步(候选,待用户指示)

gate ❌ 项「per-screen UX spec 未开工」的补件(教学纸近景 = 已建议首件)· U0a/U0/U1
工具链(本机无 Unity 环境,不可行)· /consistency-check 复跑。**未 commit**(无用户指令)。

---

## 第十九批(2026-09-21)per-screen UX spec 首件 —— 教学纸近景 PaperCloseup48

### 判据(从哪里来)

- gate 报告(2026-09-21 technical-setup→pre-production)最后一个 ❌ 品质项:「design/ux/
  只有模式库,无 per-screen spec」—— 本批 = 该 ❌ 的补件。
- 同批收窄 `OQ-42-5`(42 只渲染的一次具体执行)+ `OQ-48-8`(闭集点名再验证)。

### 用户裁定(1 项,2026-09-21)

- **等比纸本**呈现形态 = 教学纸近景 + **P0 纸面近景族默认**(39 脉案 / 6 纸质地图 / 20 出诊箱
  同族;各屏布局语义仍归属主系统,42 只渲染)。备选「铺满全屏」已摊开被否。

### 执行形式

- 上下文读取(recon)→ AskUserQuestion(1 项真设计判断:纸面形态)→ 一次成稿 15 节 →
  Bash 核验(引用行号 / ⑲→⑱ 错字 / 表格 66 行 / 占位符)→ 落盘 + P-02 状态行同批涟漪 →
  `/ux-review`(APPROVED · 0 BLOCKING · 4 ADVISORY)。

### /ux-review 判决(2026-09-21)

- **APPROVED**。4 条 ADVISORY 已全修:① 头文件补 Platform Target;② §4 出入口转两表式
  (采纳为「唯一 toggle + 走近步进」两条表);③ §7 补「无自动消失 / 无超时关闭」(:129 铁律);
  ④ AC #2 类型 `[A→L]` → `[A]` 双标签注明。
- ⚠️ 评审附带验收前置(非缺陷):AC 区可测性以 `PaperCloseup48` 第七员登记(42 修订轮,
  OQ-C1 / OQ-48-8)为前置 —— 登记前 AC 属 NOT-RUN 预测量;「APPROVED」≠ AC 已验收。

### 文件清单(本批)

1. `design/ux/paper-closeup-48.md`(新,~11.6KB,15 节 + 8 AC + 6 OQ);
2. `design/ux/interaction-patterns.md`(P-02 状态行 1 行 Edit:⚠️未定 → ✅已裁 · 等比纸本近景 ·
   第七员登记归 42 修订轮)。

### 落盘纪律核验

- 不宣称闭集改动(spec §1 显式声明;AC-42-F1 仍六项,`ModalId` 成员不动);
- `accessibility:83` 状态行(预期可解)未动 —— 其最终收缩 = OQ-C3,归 42 修订轮;
- 零 SimEvent / 零遥测(§8)承 ADR-013 §十 ③ + AC-48-03 / AC-19-01;
- 未 commit(无用户指令)。

### 下一步(候选,待用户指示)

- ~~教学纸作者态内容载体固化(OQ-48-1 依烘焙假设书写,载体落定归 48 实现轮)~~ → **✅ 已裁 2026-09-21(批 20)**
- `/consistency-check` 复跑(跨批一致性安全网)
- 批 19 后第二轮(如 6 布置约束 / art 插图规格细化 / 42 修订轮准备)
- 提交已积压批次(批 18 + 批 19 + 批 20 未 commit)

---

## 第二十批(2026-09-21)教学纸作者态内容载体固化(OQ-48-1 闭合)

### 用户裁定(2 项,2026-09-21)

- **载体切分 = [A] 单文件合一**:`assets/data/48_tutorial_content.json`(顶层 `steps` 索引 +
  `narration` / `demonstrations` / `papers` 三分区;一张 schema · 一次烘焙 · 一个 ConfigVersion)。
- **口述文本 = 自含**:48 JSON 内 `text` 字段(44 变体表只管语音 cue;字幕语义归 44、呈现归 42,
  承 AC-44-15)。备选 [B] 文本在 44 变体表(48 只引 cueId)已摊开被否。

### 执行形式

- recon → AskUserQuestion(2 项真设计判断)→ 写 `assets/data/48_tutorial_content.json`(全案首件
  作者态数据文件,AC-48-01 交付物)→ Python 核验(JSON 合法性 / 引用完整性零孤儿 / AC-48-13 病名
  扫描零命中)→ 落盘 3 处涟漪。

### 文件清单(本批)

1. `assets/data/48_tutorial_content.json`(新,全案首件作者态数据;schema 种子 = 六步 + 4 口述 +
   6 示范 + 1 纸,17 内容债单位;文本为示例文案,终稿归 narrative);
2. `design/gdd/tutorial-and-onboarding.md`(新增 §规则八「作者态数据载体」:文件结构表 + 6 条
   校验规则 + 内容债口径 + archive 步判据例外 + OQ-48-1 行回写「已裁」);
3. `design/ux/paper-closeup-48.md`(§10 教学纸 DTO 行:烘焙假设注记 → 「载体已裁 2026-09-21」)。

### 落盘纪律核验

- **不越权声明**:① 不登 `entities.yaml`(教学纸 DTO 形状归 48 内容,无跨系统数值事实 ——
  consistency-check 的「仅跨系统事实入册」判据不满足);② 不动 `tr-registry.yaml`(无 TR-tutorial
  组 = /architecture-review 轮缺口,非本轮职权);③ 不 commit(无指令)。
- **AC-48-01 点名对齐**:文件名前缀 `48_` + 描述名 `tutorial_content`(承 data-files
  `[system]_[name].json` + 21a 先例)。
- **AC-48-13 满足**:seed 文案扫描零病名 / 零诊断结论;扫描器为构建期契约(规则八校验 4)。
- **archive 步判据例外**已显式声明:`Checkpoint` 非 SimEvent.Kind(7a 存档头不落流),开白名单例外。
- **含数字段零启用**::139 文本(示例文案,归 narrative)+ 1 个 `Checkpoint` 例外字面。

### 下一步(候选,待用户指示)

- `/consistency-check` 复跑(跨批一致性安全网 —— 批 19 + 批 20 后)✅ **已执行,见下节**
- 42 修订轮准备(OQ-C1 第七员登记 · OQ-C3 accessibility 回刷 · OQ-48-8 闭集点名)
- 6 布置约束 / art 插图规格细化(OQ-C5)
- 提交已积压批次(批 18 + 批 19 + 批 20 未 commit)

**未 commit**(无用户指令)。**stage.txt 未写入**(结论 CONCERNS,gate 未过)。

---

## 第二十一批(2026-09-21)一致性检查 —— 批次三(批 19 + 批 20 后)

### 扫描面

- 模式 `since-last-review`,锚 = `design/gdd/gdd-cross-review-2026-09-20.md`
- 注册表条目:3 实体 / 2 物品 / 25 公式 / 103 常量(合计 133);**16 件 GDD 扫描面**
- **注册表主体零冲突**:四值裁定批(`TICK_SECONDS=0.05` / `PATIENT_APPEARANCE_CAP=24` /
  DOTS 门 100 & 8 ms / 病种预裁)**逐字一致**;上一批「常量刷了、复述点没刷」**零复发**。

### 检出:3 🔴 + 4 ⚠️(全部为「裁定已落地,复述点未刷」型,无设计/数值分歧)

| # | 内容 | 状态 |
|---|------|------|
| C-1 | `patient-ai.md:599/:785` 仍称联机音频「取主机技能」(背离 ADR-018 §五 2026-09-18 裁定 D-A);同文件 `:872` 已被批 19 刷对 ⇒ **文件内自相矛盾** | **✅ 已修** |
| C-2 | `design/accessibility-requirements.md:174` 引据字符串为假(结论「分叉的只有音」仍成立) | **✅ 已修** |
| C-3 | `patient-ai.md:1077/:1147/:1168` accessibility 误载路径 `design/ux/` + 义务已兑现仍记「成文时须」未来时 | **✅ 已修** |
| S-3 | `audio-system.md:971` ADR-025 V-5 加注漏网(同文件 `:242` 有注 ⇒ 文件级 grep 谎报完成) | **✅ 已修** |
| S-1 | `tr-registry.yaml:1310` `TR-diag-024.requirement` 与同条目 `:1315` note 自相矛盾 | ⏸ 归 `/architecture-review` |
| S-2 | `tr-registry.yaml:1814` `TR-patient-018.note` 同款旧口径(C-1 的注册表侧孪生) | ⏸ 归 `/architecture-review` |
| S-4 | `tests/README.md:38-41` 称程序集命名「未裁决」而 ADR-025 已 Accepted | ⏸ 归 `/test-setup` |

### 修复动作(7 处编辑 / 4 件)

`patient-ai.md` ×5(两处改口 + 两处路径/时态 + `OQ-13-4`) · `accessibility-requirements.md` ×2
(引据 + `:159` 时态订正)· `audio-system.md` ×1(V-5 加注)· `docs/consistency-failures.md`
(5 行账本 + 1 条叙事条目)。

### 落盘纪律核验

- **不越权**:未动 `tr-registry.yaml` / `entities.yaml`;机制数值零变动;**`AC-13-F3` 承
  「引用 ≠ 验收」仍不记绿**(只改时态);不 commit(无指令)。
- **新检出模式(值得记账)**:① 「同一文件内自相矛盾」是复述遗漏的最强检出信号(可自证);
  ② 首次出现**方向相反**的滞后 ——「权威件自陈的义务状态落后于实际」(无障碍件说某回写「归下一轮」,
  而该轮已经做完)⇒ 补判据:凡「归 XX 轮 / 下一轮 / 成文时」类未来时义务句,该轮完成后须回扫;
  ③ V-5 判据的量词是「命中**处**」,落实时**不得降格为按文件计数**。

<!-- CONSISTENCY-CHECK: 2026-09-21 | GDDs checked: 16 | Conflicts found: 3 (🔴3 + ⚠️4;其中 4 项同日已修 · 3 项归后续轮次;数值零冲突) | Report: docs/consistency-report-2026-09-21.md -->

---

## Session Extract — /architecture-review 2026-09-21

- **Verdict**: **CONCERNS**(零阻塞 —— 三理由:Foundation 残 2 条 / 12 项 P0 零 TR / 三项承重 spike 未跑)
- **Requirements**: 387 total —— **245 covered · 51 partial · 89 gaps · ◆2 no-adr-by-design**(ID 恒 387,状态位**零翻转**)
- **New TR-IDs registered**: **None**(本轮只改两条条目**文本**:`TR-diag-024.requirement` · `TR-patient-018.note`)
- **GDD revision flags**: **None**(上轮的 1 项已由批次三结案;七条 RC 全属 ADR 内部一致性,未外溢设计面)
- **Top ADR gaps**: ① `TR-skill-008`(技能成长存档持久化,无裁决件 → 7a 逐字段 + Required ADR #4)·
  ② `TR-itemdb-031`(掉落实体事件化边界,有裁决面缺执行体)·
  ③ 30 的 `TR-skill-001…007`(同 ①,Required ADR #4)
- **Report**: `docs/architecture/architecture-review-2026-09-21.md`

### 本轮实质进展(逐条实测,非自述)

- **8 条 RC(`RC-1…RC-8`)+ S-4 全部结案** —— 逐条定位修复坐标,**全部落在 ADR 正文,无一条靠改状态位**:
  `adr-013:204-207` · `adr-023:5` · `adr-017:170` · `adr-012` ×7 · `adr-025:116-119` ·
  `adr-023:138-141`+`:276` · `adr-014:173-176`+`:366` · `adr-023:278-281`。
- **三项承重引擎风险净变化**:R-C(IL2CPP 有符号溢出)**消除**(F7 降级为表示选择);
  R-A(UI Toolkit 焦点桥)/ R-B(`noEngineReferences` 机制)**降级但未实测**。
- **Foundation 层 4 → 2**(◆ 不计缺口):残 `TR-itemdb-031` / `TR-skill-008`。
- **Knowledge Risk 对账**:本轮实测 **HIGH 7 / MEDIUM 7 / LOW 8**;⚠️ **上轮的 6/8/8 系 ADR-008
  未计入 HIGH** —— 本值正确,已在报告 §5.1 显式对账(**未静默重述**)。
- **本机不可跑**:三项 spike(R-A/R-B/R-C)+ T-5 —— 无 Unity 编辑器,归实现轮第一件事。

### 落盘清单(7 处 / 6 文件,全部经用户 [A] 放行)

新建 `docs/architecture/architecture-review-2026-09-21.md`(第三份报告)· 改 `tr-registry.yaml`(S-1/S-2)·
改 `tests/README.md`(S-4)· 改 `traceability-index.md`(头部 + 变更历史追加)· 改 `requirements-traceability.md`
(头部三行)· 改 `architecture-review-2026-09-20.md` §9(追加结案注)· 改 `control-manifest.md` §Open Items A
(追加结案注)· 追加 `docs/consistency-failures.md`(1 条 Reflexion 条目)。

### 落盘纪律核验

- **不越权**:机制数值零变动;**未翻任何 `status`**(两条注册表改口**不充当验收** —— 承禁借绿);
  `tests/README.md` **决定不变、不生成任何文件**;**未 commit**(无用户指令)。
- **本件刻意不夹带三项**:D-R1(报告 ID 族登记表)/ D-R2(`§5.4` 生成器)/ D-R3(12 项零 TR 回填)
  —— 均须专门批次;建议见报告 §8.1(D-R1 建议建表但**明写不作为门控判据**,避免长出第三个计数真源;
  D-R2 建议与 `tools/kindgen/` 同批立,不零散开工具面)。
- **新登记失效模式**:「未结项清单」会**自发过期且不通知消费者** —— 上轮报告 §9 与 control-manifest §A
  在同日内双双过期(8 条 RC 已全结)。与批次三登记的「权威件自陈义务状态落后于实际」**同一方向**,
  载体不同(过去时的未结断言 vs 未来时的义务句)。⇒ 建议把「上轮报告的未结项」列入
  `/consistency-check` 的 `since-last-review` 必扫面(本件只登记,**不代改 skill**)。
- **D-R3 的分母效应(须记账)**:245 `covered` 的分母**不含** 12 项零 TR P0 系统 ⇒ §1 的 **63.0% 是偏高估计**;
  若①类 6 项(已有 GDD + 已有 ADR)按预期回填,分母 → ~470,覆盖率降至 **~53%** ——
  **不是质量下降,是把已覆盖之物从账外收进账内**。

---

## 第二十二批(2026-09-21)D-R1 报告 ID 族登记表落盘

**用户裁定**:D-R1 = 「建表(按上稿全文)」。

**落盘(1 文件 / 2 处)**:
- `docs/architecture/traceability-index.md` 新增 §「报告 ID 族登记表」(插于 §优先修复清单 与
  §变更历史 之间,现 `:950-1002`)+ 头部一条指针行(`:84` 区块内)+ 变更历史追加 1 行。

**与上稿(报告 §8.1 建议)的差异 —— 族集合 8 族 → 14 族**:
全库扫描实测上稿遗漏的 **G / N / T / V / W / BL** 六族**全部存在活跃撞号** ⇒ 按上稿照抄会使本表
**建立当日即不完备**(与本节要治的失效模式同型)。

**七处撞号实测(带坐标)**:
| # | 族 | 实例 |
|---|---|---|
| ① | C | `architecture-review-2026-09-15.md:106` C-1(`PatientId` 编译级)vs `consistency-report-2026-09-21.md:40` C-1(联机音频口径)|
| ② | **S** | `architecture-review-2026-09-21.md:127`/`:215` S-4(bundle refcount)**vs 同件** `:244` S-4(`tests/README.md` 陈旧)—— **同件双义,中央台账拦不住** |
| ③ | S | 批三 S-1/S-2/S-4 被 review 件整族沿用(同号同义 = 借号,无机制保证持续)|
| ④ | **B** | 报告级 B-3 vs `time-and-weather.md:220` B-3 vs `gdd-cross-review` 组 G3 B-3(**三重叠**)|
| ⑤ | W | `gdd-cross-review:163` W-1(告警)vs `campaign-arc.md:62` W-1(**用户风险接受裁定**)|
| ⑥ | **G** | `architecture-review-2026-09-15.md:466` 引「8 的 G-7」而该件 G 族**只到 G-4** —— 真身 = `diagnosis-system.md:1608` **V-8.7**(**幽灵引据**)|
| ⑦ | N | `-2026-09-15.md:593` N-3/N-4 vs `-2026-09-21.md:138` N-3 |

**纪律核验**:
- **本表不做门控判据** —— 表头明写;它不产生计数,`tr-registry.yaml` 的 `status:` 仍是唯一计数真源。
- **ID 零增删 · status 零翻转** —— 恒 387 / 245 ✅ / 51 ⚠️ / 89 ❌ / ◆2,纯文档增量。
- **不越权** —— 未改任何 ADR / GDD / registry / skill;**未 commit**(无用户指令)。
- **D-R2 / D-R3 同批未做**(如实登记在本表与变更历史行内):D-R2 推后至 `tools/kindgen/` 落地同批;
  D-R3 须专门批次。

**本表自陈的局限(须留给下轮)**:
它**只记录已发生者,不能阻止新撞号**(无生成器、无构建期断言 —— 与 ADR-024 对 `Kind` 的
「单一真源 + A1–A5」**不同级**)。结构性消除须另立**作者期命名空间纪律**(每件报告自带族前缀,
如 `AR21-C-1` / `CR21b-S-3`)或把报告 ID 纳入 `tools/kindgen/` 生成器族 —— **登记为待用户裁,
与 D-R2 同批**。

---

## 第二十三批(2026-09-21)stale「N 无 GDD」残留清理 —— 全仓扫尾完成

**任务性质**:纯文本陈旧清理,**不需要用户裁决** —— P0 的 31 项 GDD 已于 2026-09-19 全部
Approved,而全仓(19 份 GDD + 12 份 ADR + `technical-preferences.md` + `entities.yaml`)
仍有大量文本断言「系统 N 无 GDD」。**不裁决、不翻案、不动数值** —— 只把「陈旧前提」与
「仍成立的前提」分开逐条标注。

**三分类口径(本轮全部编辑遵守)**:
| 类别 | 判据 | 处置 |
|---|---|---|
| **(a) 已闭合** | 对侧 GDD 存在 **且** 反向引用边已核实落地 | 就地注「✅ 已闭合」+ 证据坐标,原句不删 |
| **(b) 前提陈旧、欠账真实** | 对侧 GDD 存在 **但** 反向引用/测试载体/裁决仍未落 | **改写归因、保留缺口** —— ⚠️ 严禁把「对方无 GDD」误升为「已结清」(**裁定 ≠ 验收 / 禁借绿**) |
| **(c) 历史记录** | 带日期的评审留档行、结案 OQ 的题面、ADR 的 Problem Statement | **只追加「历史注」或不动** —— 不重写动机文本 |

**真「无 GDD」的例外清单(本轮刻意不订正,共 22 项)**:
12 · 14 · 15 · 16 · 19 · 22 · 31 · 32 · 33 · 34 · 35 · 36(P1a)· 38 · 40 · 41 · 43 · **45(P1b)** · 46 · 47 · 49 · 50 · **54(Tooling,刻意无)** · 21b · 28。
`systems-index.md:90/170`(54)、`world-and-ecozones.md:1309/1334`(45)、
`modular-building.md:476`(45)、`player-controller` O-3/O-4 的 45 半边、
`random-events.md:1032`(六家)、`time-and-weather.md:601/688`(49)、`input-system.md:773`(49)
—— **均判定为「属实,不订正」**。

**本会话(续轮)落点 —— ADR append-notes(9 份 / 13 处)**:
- `adr-006:87` · `adr-007:71(+Negative :330)` · `adr-008:108` · `adr-010:26/:33/:70/:446`
- `adr-011:73`(Problem Statement)
- `adr-016:51`(Summary,**一处总注覆盖 Context/Current State 各处同源陈旧**)· `:85` Ordering Note(27 的 TR 已同法追加)
- `adr-018:49`(一处总注覆盖 `:110` Current State)
- `adr-019:30`(一处总注覆盖 `:80/:100`)· `:488` Foundational decision 行
- `adr-020:139` Current State · `:607` Foundational decision 行
- **`adr-022:35/:84` 不动**(关卡工具 = 设计如此的真「无 GDD」)

**技术债台账的本轮实际位移(未翻案,只改归因)**:
| 项 | 原口径 | 现口径 |
|---|---|---|
| `player-controller` **O-5** | 悬空,因「44 无 GDD」 | **仍悬空**,理由改为「44 已成稿而全文零处『落地冲击/垂直速度』refs ⇒ 反向登记未落」 |
| `AC-51-B9` | 前提「51/10 无 GDD」 | 前提陈旧 ⇒ **NOT-RUN 保留**,新理由 = `tests/integration/51/` 不存在;升级路径改为 **BLOCKED-BY-`OQ-10-8`** |
| `TR-concept-008`(42 手柄导航) | 「42 无 GDD」 | 42 已成稿;gap 归因改指 `input-system.md` 自己的 **P0 焦点载体前置门 D-A 行**(三项未决先于 42 成文) |
| `AC-2-05` / `AC-2-22`① | 「主语系统无 GDD」 | ① 22① 可签(10 有 GDD);② `AC-2-05` 主语 = 相机程序集自己,卡点 = **测试载体未建** ⇒ NOT-RUN 不得记绿 |
| `enemy-ai O-27-8` | 三件并列「对侧未回填」 | **6 那件已完成**(2026-09-20 回刷)⇒ 仍悬空只剩 1 / 23 两件 |
| 53 → 13 反向引用 | 「13 无 GDD」 | 13 已成稿;`patient-ai.md:903` 分档 —— **53 的反向引用仍未落**,须 53 下轮补 |

**两处文书事故的自纠(登记以备复现)**:
① `adr-010` Summary 的历史注首次**落在句子中间**(把「…009」与「**五份 ADR 集体委派义务**」
切断),已按 `camera-and-viewpoint.md:1330` 的同一修法修复(先补完整句,注作独立 `>` 行)。
② `input-system.md:824` 首次订正误指 `OQ-42-5`;核验后 `OQ-42-5` = 39 层籍问题(已闭合),
二次订正改指本件 D-A 行并明写「与本条无关」。
**教训**:Edit 的 `old_string` 不可采信摘要里的标点/缩进(全角逗号 vs 半角、2 vs 3 空格)——
须 `cat -A` / `repr()` 取原字节;行号会随编辑漂移,**每簇重跑 grep,不靠记忆**。

**最终核验(全仓 Python 复扫,±2 行窗口内无注记者为零)**:
逐条人工分诊后残余命中**全部**属于 (b) 类已改归因 / (c) 类已就地总注 / 22 项真空白名单 ——
`entities.yaml` 的 YAML 结构 `yaml.safe_load` 已验;两处表格行管道数平衡(5 / 3)。

**纪律核验**:
- **未动 `tr-registry.yaml`**(registry 正文 = `/architecture-review` Phase 8 权限);
  `entities.yaml` 只加 YAML 注释,**未改 `source:` 字段**。
- **未动任何数值 / 未翻任何案** —— 全部 `NOT-RUN` / `BLOCKED` / `EXTERNAL` 判级保留。
- **未 commit**(无用户指令)。

**下一步(同族收尾)**:本批后 stale「无 GDD」维度**全仓无裸露项**。
待用户裁的推迟项不变:**D-R2**(`architecture.md` §5.4,与 `tools/kindgen/` 同批)·
**D-R3**(12 个零 TR 的 P0 系统回填,专门批次,分母 387 → ~470)· 报告 ID 命名空间纪律 ·
42 修订轮准备 · commit backlog。

---

## 第二十四批(2026-09-21)—— 用户裁定三项落地 + 工作区分三批提交

**触发**:用户问「哪些需要我裁决」⇒ 摊出真·用户项清单(widget 三问)⇒ 裁定:
① **批准 4 的承接方案(OQ-6-1)**;② **QQ-07 残项 + D-R2/报告 ID 照建议办**;③ **现在提交**。

**T-1 · OQ-6-1 / O-6-7 结案**:「已发现」触发 = 玩家主动交互(非碰撞进入)· 自报方 = `{4,25,37}`
冻结集(27 移出)· 6 仍是唯一写者(ADR-021 写者裁决不动,本裁补其留白的触发方)。
`EC-6-2` 由 6 侧**正式**闭合 —— 闭合的是**归属洞**,三项实现义务(判距复验 / per-tick latch /
`IDiscoveryReporter`)与 4 侧 `AC-4-12` **不随裁翻绿**;`R_INTERACT` 取值归数值轮(用户自己调);
25 / 37 各自自报细则不自动结清。回刷 8 件:world-and-ecozones(头部门 / 转移表头 / T1 表下注 /
EC-6-2 注 / O-6-7 行 / OQ-6-1 行 / OQ 计数注)· interaction-system 注① · systems-index #4/#6 行 ·
6 评审日志两处 · adr-022 §后果类比 · traceability-index §4 组注。

**T-2 · QQ-07 全结**:早批 ◆ 已收 `TR-concept-003/004`;本批补收 `-006` —— 帧预算是性能承诺
**不降级**,保持 ❌ 待最低目标硬件。architecture.md 四处追平(QQ-07 行转 ✅ / §Baseline concept 行 /
§5.4 簇行 / **concept 组计数行 3 → 1 + 另◆2**)。⚠️ 计数行订正的性质 = **登记面追平**(◆ 流转在
2026-09-21 早批已发生,该行一直挂 3 ❌),registry `status:` 真源零变动(245/51/89/◆2,ID 恒 387)。

**T-3 · D-R2 / 报告 ID(照建议办)**:D-R2 推后至 `tools/kindgen/` 同批(§5.4 不动);报告 ID 采
**作者期命名空间纪律**(新报告自带件前缀 `AR21-C-1` 式;存量不重编号;不入 kindgen 族;
登记表不加机制,由后续 review 件自查)。落点:traceability-index §报告 ID 族登记表尾注 +
review-2026-09-21 §8.1「不代拍」解除注。

**提交(用户指令「现在提交」)**——工作区先按归属拆两批、裁定批第三批:
- `19f5c05` docs(design):设计层 21 件(OQ-48-1 载体 JSON + 规则八 + stale「无 GDD」订正批)
- `7676c95` docs(architecture):架构层 19 件(09-21 review 报告 + 登记层回刷 + ADR 历史注)
- (本批)docs:第二十四批三裁定落地件
提交前核验:`assets/` 为未跟踪新目录,内容为 OQ-48-1 裁定的作者态 JSON(无 secret 风险面);
`session-state` 按约定 gitignored。

**下一步**:真·待用户项清零 ⇒ 剩余全归 owner-not-user 队列:**D-R3**(12 项零 TR 回填,专门批次,
分母 387→~470,归 `/architecture-review` Phase 8 职权 + 用户排期)· 42 修订轮准备(OQ-C1 第七员
登记 / OQ-C3 / OQ-48-8)· engine-reference `Canvas` 回写与 RC-9 两件残留仍挂 control-manifest §C
2/3(前者 owner=用户但未列为本轮 widget 项 —— 待下轮询问)· R-A/R-B/R-C spikes(无 Unity 编辑器,
归实现轮)。

---

## 第二十五批(2026-09-21)—— D-R3 专门批次:12 项零 TR 系统批量回填(+112 条)

**触发**:用户排期指令「排期 D-R3 专门批次(分母 387→~470」(承第二十四批收尾报告的
owner-not-user 队列首项)。**无提交指令 ⇒ 本批未提交**。

**流程**:五路并行提取子代理(按 review §7.1 分类:A=7a/7b · B=42/44 · C=23/24 ·
D=17/39/29/48 · E=51/53)+ 主会话逐条证据审计装配(禁借绿口径:「已裁但执行体/夹具/
回写未落」一律 partial;无 ADR 文本一律 gap)。子代理 partial/gap 判定除非 ADR 原文证明
covered,一律保留。

**落盘(5 件,均未提交)**:
1. `tr-registry.yaml` —— **+112 条**(append-only,既有 387 条逐字未动,`yaml.safe_load`
   前缀比对守住)。12 组按 7a 11 / 7b 7 / 17 8 / 23 10 / 24 10 / 29 8 / 39 8 / 42 12 /
   44 14 / 48 8 / 51 8 / 53 8;新前缀 persist/saveslot/foraging/building/clinic/death/
   casebook/skeuoui/audio/tutorial/telemetry/medcons;`system:` = GDD slug;每条 note 带
   `D-R3 回填(2026-09-21)` + 锚点。**实测本批 69 ✅ / 28 ⚠️ / 15 ❌** ⇒ 合计
   **314 / 79 / 104 / ◆2(499)**。
2. `traceability-index.md` —— §21–§32 十二节(表格 124 行核验)+ §汇总 12 行 + 合计行
   387→499 + 头部 Last Updated 注 + §变更历史第二十五批行。
3. `requirements-traceability.md` —— 头部/汇总/分层 gap 分解/Tooling 行全部刷至 499 口径
   (Foundation 仍 2 条:本批零 Foundation 域条目)。
4. `architecture-review-2026-09-21.md` —— §7 D-R3 / §8 T-2 结案注(原文不删)+ §7.1 结案注
   (「① 类预计全 covered」被实测推翻:7a 3⚠️ / 7b 1❌ / 23 3⚠️ / 24 4⚠️2❌ / 42 4⚠️ /
   44 1⚠️2❌)+ §1 冻结值指针注。
5. `architecture.md` —— Technical Requirements Baseline 第五次动注(387→499;分层表不随批重算)。

**覆盖率**:63.3% → **62.9%**(分母效应,账外收进账内,非质量下降;降幅远小于预估 ~53%,
因 51 组实测 6✅2⚠️ 全绿、53 的 P1a gap 仅 3 条)。

**登记面收获(供用户处置,本批不代拍)**:
- **三处 GDD↔ADR 回写不一致**(不代改 ADR 正文,承 24 批「本件登记,不代改」先例):
  ① `TR-persist-004` 校验和覆盖范围(ADR-010:127/:287 vs persistence-service.md:480);
  ② `TR-persist-006` 忘词令扫描已裁归 ADR-012 CI 门但其全文零记载(grep=0 实测);
  ③ `TR-saveslot-004` ModalId 字面 `SaveSlot7b` vs ADR-013:352 `SaveSlots`。
- **player_id 发号权威全库零定义**(`TR-casebook-002` gap,连带 `TR-tutorial-008` 不能记绿)——
  挂 45。
- **ADR-001 客户端→主机意图通道的消费者成簇**:`TR-foraging-008` / `TR-casebook-004` /
  `TR-tutorial-006` 加入既有 10/4/20 三处 ⇒ 由单点欠账升级为 **P1b 前硬前置**。
- **未来 ADR 候选 8 条**(gap 中无 ADR 着落的纯机制):24-007 房间连通 / 24-009 语义不泄漏 /
  44-011 世界语境呼吸 / 44-012 EndLoop 兜底 / 48-002 Anchor-Completion 两相 /
  48-007 零设门断言 / 39-002 player_id / 39-008 CasesOf;另有 53 三条 P1a 主照登 +
  7b-007(反幻想呈现,疑归 GDD 家规,42 修订轮复核)。
- **SkillGrown 在 entities.yaml 零登记**(`TR-death-005` partial)—— ADR-024 补齐轮义务。
- **订正声明**:装配前主会话曾误报「F-42-5 口径漂移」,grep 复核后**撤回**(该编号不存在,
  :49-50 措辞与 RC-1 收窄兼容)—— 未进入任何条目。

**验证**:registry `yaml.safe_load` 499 条 · 状态计数逐位自洽 · 前 387 条与备份逐字段相等 ·
ID 全局唯一无撞号 · index 124 表格行 pipe-count 0 异常 · 112 个新 ID 全部在 index 出现。

**下一步(队列不变)**:42 修订轮准备(OQ-C1 第七员 / OQ-C3 / OQ-48-8,与 OQ-42-5 并批)·
上述三处回写不一致的处置(待用户)· D-R2 随 tools/kindgen 同批(24 批已裁)·
R-A/R-B/R-C spikes(归实现轮)· **本批提交待用户指令**。

---

## 第二十六批:D-R3 处置批(用户四项裁定落地)—— 2026-09-21

**触发**:用户问「哪些需要我处置?」→ 处置队列四簇 + 提交问题 → 用户四项裁定
(AskUserQuestion,全部照推荐项):① 第二十五批单批提交(已提交 `7619bf2`);
② A 组三处回写不一致**照三条建议全批**;③ C 组 8 条未来 ADR 候选 + 53 三条 P1a gap
= **登记不立件**;④ player_id **复用 IIdAuthority**(机制 A)。

**裁定落地(回写面)**:
1. **不一致①**(校验和范围,GDD 为准):ADR-010 四处统一口径「Checksum 字段位 = 头部之首
   (magic 之后)+ 覆盖域 = 其后全部字节(头部其余字段 + 三流 + 快照)」——
   `:127` ASCII 块字段序调整 · §四 覆盖范围行 · `struct SaveHeader` 字段序挪位 + 注释 ·
   Guidelines 6 重写 + 回写注(带 TR-persist-004 引据)。
2. **不一致②**(忘词令零记载,补记载):ADR-012 §五 末新增硬义务条目
   「7a 忘词令符号扫描在本门执行」(三格矩阵同流水线 · grep-IL + 反射断言互补 · qa-lead 只评审判据)。
3. **不一致③**(ModalId 字面,ADR-013 为准):`save-slot-ui.md` 五处 + `systems-index.md` 一处
   `ModalId.SaveSlot7b` → `ModalId.SaveSlots`(python replace + assert 零残留);
   `interaction-system.md:507-510` 是**三审订正引据**(记录漂移史)不动;reviews/ 历史不动。
4. **player_id**:ADR-006 Amendment B 末追加「2026-09-21 注记 —— id 空间语义第二次扩大(玩家 id)」
   —— 复用 IIdAuthority · 机制 A 三条不变量适用面「受伤实体 → +玩家」· 铸造 = 建世界/加入时主机
   发号 · 与病人共用同一 id 空间(max+1 高水位仍单点)· None=-1 不占用;**残留三项执行义务**
   (entities.yaml 增列归 ADR-024 补齐轮 / 45 铸造契约归 45 GDD 轮+ADR-001 窄修订同批 /
   7a 显式声明不独立快照计数器)。technical-preferences.md ADR-006 日志条目同步补注。
5. **登记不立件**:8 候选(clinic-007/009 · audio-011/012 · casebook-002/008 · saveslot-007 ·
   tutorial-002/007)+ medcons-005/006/008 P1a 确认 —— 裁定注全部入条目 note。

**registry 状态净变动**(禁借绿口径全程守住):
- `TR-persist-004` partial→**covered**(裁决+回写两面齐;实现期按新字段位编码注)
- `TR-persist-006` partial→**covered**(落点有正文记载;夹具脚本本体待 /test-setup,注内明写不借绿)
- `TR-saveslot-004` covered 不变(note 结案 + revised)
- `TR-casebook-002` gap→**partial**(裁决已落 ADR-006 正文;`adr:` null→ADR-006;三项执行义务残)
- `TR-tutorial-008` partial 不变(note 更新:方向已裁已回写,存储面随 45 轮)
- **⇒ 499 条 / 316 ✅ / 78 ⚠️ / 103 ❌ / ◆2**(yaml.safe_load 实测自洽;覆盖率 62.9%→63.3%)

**涟漪文件(7 件)**:adr-006 · adr-010 · adr-012 · save-slot-ui.md · systems-index.md ·
tr-registry.yaml · traceability-index.md(头部注/§21/§22/§27 行+计数/§汇总 3 行/变更历史第二十六批行)·
requirements-traceability.md(计数+新头部注+101 条分解)· architecture.md(ASCII 行+第六次动注)·
architecture-review-2026-09-21.md(§7.1 处置结案注+§1 指针)· technical-preferences.md(ADR-006 日志补注)。

**仍开放(未代拍)**:C 组中 **ADR-001 意图通道窄修订的排期**(归 45 GDD 轮 or 现在开)——
本批只裁「8 候选不另立 ADR」,该项是裁定问题措辞里「不另立 ADR 本身」的保留件,
与 player_id 的 45 铸造契约同批,P1b 前硬前置不变。

**下一步队列**:42 修订轮准备(OQ-C1/OQ-C3/OQ-48-8 与 OQ-42-5 并批)· D-R2 随 tools/kindgen
同批(已裁)· R-A/R-B/R-C spikes(归实现轮)· ADR-001 窄修订排期(待用户,随 45 GDD 轮自然到点)。

---

## 2026-09-21 · 第二十七批 —— ADR-024 补齐轮 · 双项登记(14 文件)

**范围**:第二十六批裁定明示「归 ADR-024 补齐轮」的两项残留 + ADR-006 注记三项执行义务中
本批可落的两项。**零 TR ID 增减 · 零状态翻转**(禁借绿全程守住)。

**① `SimEvent.Kind.SkillGrown` 入 registry**(`entities.yaml` constants,V-2 条目数 **33→34**):
- `stream: history` · `author: "30(主机经 EmitGrowth 统一物化;调用方 ∈ {8/17/11})"`(37 不调用 = R-3 口径)
- 载荷权威 = `skill-system.md` §3.2 规则二,逐字对齐:`(skill_id, object_id, novelty_class)` + **绝对 `level`**
- ⚠️ 口径分歧以登记消解:29 的 F-29-4「携带 `ΔLevel`」= **读流派生量**(相邻 level 差),30 载荷无该字段
  —— 换算已就地写进 death-and-respawn(机制数值零改动)
- 原义务的「ADR-009 §三 骨架补记」一项随 **ADR-024 ②**(骨架降级为路由注记)**消解**,不补
- **新发现 latent 缺口(登记不代拍)**:7a F-7a-4 折叠行形状不含成长 ⇒ 病人终态后 SkillGrown 在
  折叠视图消失,与 29 F-29-4 / AC-29-16 的「参与重放折叠」前提冲突 ⇒ **新立 `OQ-7a-9`**
  (三案:折叠豁免扩 Kind / 折叠行扩列 / 读侧从快照基线;`OQ-7a-6/7/8` 已占,故取 9)
- `TR-death-005` **不翻绿**:甲面闭合,改挂 `OQ-7a-9`
- `AC-11-16` 双因缩至乙:甲(Kind 契约面)✅,`K_difficulty` 形参须待 `OQ-11-13` ⇒ 仍 NOT-RUN

**② `next_player_id` 计数器条目入 registry**(ADR-006 2026-09-21 注记 残留① ✅):
- 机制 = IIdAuthority 机制 A 三条硬不变量(永不复位 / max(id)+1 事件流重构 / 折叠谓词不适用)
- 共用 id 空间 ⇒ 高水位单点守卫;None=−1 不占用;铸造 = 建世界 / 加入会话(主机)
- **残留③ ✅**:persistence-service 规则九末段显式「不独立快照玩家 id 计数器」
- **残留② 仍 open**:45 铸造契约(随 45 GDD 轮 + ADR-001 窄修订同批)—— ADR-006 注记已标注 ①③✅②open
- ⚠️ 连带订正:`casebook.md` 契约③「P0 单机 = 常量 0」与注记「建世界走发号」矛盾 ⇒ 就地改口径
  (0 是合法发号值非哨兵);39 契约①②不变

**涟漪件(14)**:entities.yaml(+SkillGrown +next_player_id +last_updated)· adr-006(①③✅标注)·
adr-024(:120/:173/V-2 → 34)· control-manifest(:121/:125 → 34 + 版本戳 2026-09-21)·
skill-system(义务兑现注 + 重开② 核过 + ⑤(b) 甲闭状态)· death-and-respawn(F-29-4 换算 +
前置注 + AC-29-16 改挂 OQ-7a-9 + 陈旧行号引据→§锚)· persistence-service(规则八 OQ-7a-9 注 +
规则九补段 + OQ 表新行)· prescription-and-medication(注⑥甲闭 + AC-11-16 缩因 + deps 表)·
casebook(契约③ 订正)· systems-index(行 30 残留划账)· tr-registry(death-005/casebook-002/
tutorial-008 三 note + 两条 revised)· traceability-index(头注 + 两行 + 变更历史)·
requirements-traceability(头注)· technical-preferences(ADR-006 日志补注)。

**核验**:registry `SimEvent.Kind.*` = **34**(V-2 新值可复算);`yaml.safe_load` 两 YAML 均解析通过;
TR 复算 **499 / 316 ✅ / 78 ⚠️ / 103 ❌ / ◆2**(零翻转);陈旧「33 支」字面只剩 architecture.md:1139
ASCII 审计图(历史注,不改);architecture.md:1012 同理。

**待提交**:14 文件(+121/−29 实测 diffstat)。

**仍开放(未代拍)**:`OQ-7a-9`(新立,7a×29×30 三案择一)· `OQ-11-13`(K_difficulty 代入方,乙)·
ADR-001 意图通道窄修订排期 · 45 铸造契约(残留②)· 既有 24 支 Kind 的三字段回填(kindgen 前置 = 实现轮)。

**下一步队列**:42 修订轮准备(OQ-C1/OQ-C3/OQ-48-8 与 OQ-42-5 并批)· D-R2 随 tools/kindgen 同批 ·
R-A/R-B/R-C spikes(归实现轮)。

---

## 2026-09-21 · 第二十八批:42 修订轮(模态闭集 6→7 员 + 方笺模态裁定 · 用户裁定两项)

**任务**:兑现 `ADR-013 §十` 预置残留 + 结 `OQ-C1` / `OQ-C3` / `OQ-48-8` 耦合簇(其耦合的
`OQ-42-5` = 「39 独立系统 vs 界面」**本批未裁**,2026-09-21 续批订正) +
结 `OQ-11-7` / `OQ-11-9`(方笺模态,BLOCKING 对)与戥子材质侧主张(解 `AC-11-13`)。

**两项用户裁定**(AskUserQuestion,照准):
① **方笺 = 39 脉案「同一本书」** —— 复用 `ModalId.Casebook` 档,语义扩展覆盖方笺;
   **不成新模态、不增员、不发相机意图**;2 的 `R-2-5` 档位表**零新增行**。
② **戥子档位读数元件 = 42 元件库「黄铜侧」** —— 錾刻刻度 / 机械位移合法(承 2026-09-17 收窄),
   **禁降级为数字角标 / `X/N`**(两栈皆禁组,无例外)。

**核心结构性事实(⚠️ 反直觉,全仓已落「勿混读」警示)**:闭集**确实**增至 7 员,
**但不是因为方笺** —— 第 7 员是 ⑦ `PaperCloseup48`(教学纸近景 spec);方笺裁定方向为「不增员」。
两问同批结清且方向相反。

**Registry 计数**:**316/78/103/◆2 → 317/77/103/◆2**(ID 恒 499,`yaml.safe_load` 复算自洽):
- `TR-prescription-014` **partial→covered**(条件式取「维持不请求」分支)+ `revised:`
- `TR-skeuoui-009` **状态不变(已 ✅)**,仅 `requirement` 文本「6 员」→「7 员」+ `revised:`

**禁借绿三处**(前置解除 ≠ 验收):`AC-42-F1` 七屏走查(⑦ 有 spec、①–⑥ 均无 ⇒ 整体 NOT-RUN)·
`AC-11-13`(由「不可判」→「可判但未判」)· `accessibility-requirements.md` 近景实测(Not Started)。

**机制数值零变动**(冻结,承「数值用户自己调」)。

**检出并登记的失效模式**:`casebook.md:174` **假引据(phantom ruling)** —— 39 曾自陈「方笺模态
已裁(`OQ-11-7` 结案)」而全库零权威、11 侧该问仍 open 三日。结论碰巧与最终裁定一致,
但当时无可打开的出处。已就地改引 `ADR-013 §十-B` + 自陈留痕 ⇒ `docs/consistency-failures.md`
(该件 gitignored,不入提交)。

**落点 15 件(+consistency-failures 本地件)**:adr-013(§十-B + 枚举 7 员)· skeuomorphic-ui
(规则十四 `IModalState` + `AC-42-F1` 七员点名 + 11 认领 + 黄铜侧注 + 教学纸近景行)·
prescription-and-medication(规则十二结案 + `AC-11-13` + OQ 表划除)· camera-and-viewpoint
(11 行 → 排除行 + 可证伪守卫)· casebook(:174 订正)· paper-closeup-48(Approved + OQ-C1/C3)·
interaction-patterns(G-1/G-3)· accessibility-requirements(:82/:83)· tutorial-and-onboarding
(「随本轮同批」承诺逾期 4 日的诚实标注 + OQ-48-8 + 陈旧行号→§锚)· tr-registry(2)·
traceability-index(头注 + 2 行 + §汇总 2 行 + 变更历史)· requirements-traceability(头注 + 5 计数)·
systems-index(行 7b/11/42/48)· technical-preferences(ADR-013 日志 Amendment B)· 本件。

**仍开放(未代拍)**:`OQ-7a-9` · `OQ-11-13`(乙,`AC-11-16` 双因之一)· ADR-001 意图通道窄修订
排期 · 45 铸造契约(残留②)· 既有 24 支 Kind 三字段回填(kindgen = 实现轮)。

**下一步队列**:D-R2 随 `tools/kindgen` 同批 · R-A/R-B/R-C spikes(实现轮)·
①–⑥ 六屏 per-screen UX spec(`AC-42-F1` 走查的前置,归 `/ux-design` 轮)。

---

## 2026-09-21 · 第二十九批:AC-42-F1 三屏 UX spec(A ① / B ② / C ④)+ ③ 阶段落盘

**任务源**:用户「依次完成②ABC③」= 写 ① 脉案页(39)/ ② 存档位(7b)/ ④ 设置壳(42)三份
per-screen UX spec,随后写 `production/stage.txt`。三份均 **Status: Draft**(待 `/ux-review`),
全部 AC 逐条 `[ ]`(**禁借绿**:spec 成文 ≠ 走查执行)。

**落点**:
- `design/ux/casebook-39.md`(A · ~300 行 · 13 AC · OQ-CB-1…6;§15.2 登记 `systems-index.md:84`
  陈旧 outlier 订正 + 门报告 :108 前提过期诚实注)
- `design/ux/save-slots-7b.md`(B · 9 AC · OQ-SB-1…6;册页容量 6 行 = ux-design 外推权裁定)
- `design/ux/settings-shell-42.md`(C · 8 AC · OQ-SS-1…6;OQ-42-6 半①「簿子」以 42 归属权裁、
  半② 进架时间表仍 open;规则五材质分界落地 = 音量控件归**黄铜侧**(滑杆/拨栓),禁水墨侧墨杠)
- `production/stage.txt` = **Pre-Production**(gate 判 CONCERNS,用户「依次完成…③」= 接受并指示落盘;
  偏移记录在本块,不写进 stage.txt)
- 涟漪:`skeuomorphic-ui.md` AC-42-F1 NOT-RUN 注回刷(①②④ 已成文 Draft,整体仍 NOT-RUN)·
  `OQ-42-6` / `OQ-42-10` 行各加分半注 · `systems-index.md` 行 48 末 OQ-42-5「仍未裁」**划除订正**
  (陈旧 outlier,四处权威 09-19 已闭合;失效模式挂 `docs/consistency-failures.md` 本地件)
- registry 核对(禁造词纪律):spec A 的 `JudgmentRecorded` / `JudgmentRevised` **实核已登记**
  (`entities.yaml:1913/:1925`,source = ADR-008 §三)⇒ 非造词,§8 加核对注;同批以已定型
  `judgment` struct(`lexicon_id:u16` + `confidence:u8` + `freehand_text:string`)回刷
  **OQ-CB-1 / OQ-CB-5 = 收窄为纯 UI 形态题**(机制承载已在,输入路径仍待裁;不代答)

**仍开放(未代拍)**:OQ-CB-1(键鼠病名输入形态,须用户显式接受自由文本的击穿代价)·
OQ-CB-5(置信度输入位)· OQ-42-10(三屏口径分散,占位非裁决)· OQ-SS-2(无障碍档位条目的
设置宿主时点 = P0 开工前须裁)· OQ-SS-4(黄铜控件三新件)· OQ-42-6 半②。

**下一步队列**:三份 Draft 的 `/ux-review` · 余三屏 spec(③ 库存容器 20 / ⑤ 教学界面 48 纸堆 /
⑥ 医馆面板 24)补齐方可谈 AC-42-F1 签核 · D-R2 随 `tools/kindgen` 同批 · R-A/R-B/R-C spikes ·
U0a(桌面 Unity 6.3 + license)→ U0(六装配骨架)。

---

## 2026-09-21 · 第三十批:AC-42-F1 余三屏 UX spec(D ③ / E ⑤ / F ⑥)—— 七员 spec 齐员

**任务源**:用户「先清三spec」= 补齐 ③ 库存容器(20)/ ⑤ 教学界面(48,「纸堆翻页走查」)/
⑥ 医馆面板(24)三份 per-screen UX spec,顺序 D→E→F。三份均 **Status: Draft**(待 `/ux-review`),
全部 AC 逐条 `[ ]`(**禁借绿**:spec 成文 ≠ 走查执行)。七份 spec 自此**全在**(①–⑦),
`AC-42-F1` 走查项**整体仍 NOT-RUN**(走查未跑 + 各员 BLOCKED-BY 前置未解)。

**落点**:
- `design/ux/inventory-container-20.md`(D · 269 行 · 10 AC · OQ-IC-1…6;③ 员)—— 行使 20 `UI-20.3`
  明载外推权自裁布局(`COLUMNS=4` / `ROWS_PER_PAGE=3`)与四档整数判据(半满 `2×load≤cap` /
  近满 `10×load>5×cap∧≤9×cap` / 满 `10×load>9×cap`),挂 BL-27 单调契约;摆放 = id 升序蛇形 +
  确定性哈希参差(呈现本地,不进流不存档);P0 禁嵌套(BL-7③)⇒ 零二层模态;`[L]` 走查条挂 R8
  (3 消费者表补 20 未落笔 ⇒ 不可签,已按禁借绿标注)
- `design/ux/tutorial-48.md`(E · 273 行 · 9 AC · OQ-TUT-1…5;⑤ 员)—— **核心发现 =「纸堆」孤儿引据**:
  §0 证据空洞声明(5 行核账:48 全文 grep「堆」0 命中 + `UI-48.1` 自陈零 UI + 无开屏动作 +
  规则一「重看教学不存在」+ 三媒体无一要求 `ModalId.Tutorial`)。处置 = 写成「**调度壳**」最小具形
  (走近 Anchor 熄灭过的锚点物 + 复用世界 Interact + 字幕带 + ⑦ 目录 + 墨点钮),**不发明纸堆内容、
  不删员**;OQ-TUT-1 三选(甲壳 / 乙字面册子须 48 先裁可重看 / 丙退休须重开 ADR-013 §十-B)**均不代拍**;
  本员 `[L]` 走查条 `BLOCKED-BY OQ-TUT-1`。零进度渲染禁令(§1 从 `AC-48-17` 推导:画步列表 = 用 UI
  绕过被禁进度查询);§5.3 铜钮→墨点钮改判留痕 + 自查注(刻意,非遗漏)
- `design/ux/clinic-panel-24.md`(F · 275 行 · 10 AC · OQ-CP-1…5;⑥ 员)—— **核心发现 = OQ-CP-1 张力**:
  24 幻想「改布局→立刻给我不同读数」× 模态开 = 世界意图冻结 ⇒ `AC-42-F5` 的单机 GIVEN 不可达
  (事件源被门冻结),三选(甲摆完开读 · 乙降非模态壁面须重开 ADR-013 · 丙选择性放行破铁律)**按甲书写不代拍**;
  形态主语 = 24 出口表既载「纸质账本式读数」(非发明);双戥子(左 EnvMod 可负 / 右 EquipMod 只上扬,
  下限未定义 = OQ-CP-5)+ 情境账格(词 ≤4 字 + 小印 / 印卸 = 不成立)+ 单层读数原则(大小只进秤);
  「印」= 新形状禁借四形状(OQ-CP-3);开启动作 / 具名 DTO 两处悬空(OQ-CP-2 / OQ-CP-4);
  `AC-42-F5` 执行体 `BLOCKED-BY-45` + `BLOCKED-BY-OQ-CP-1`

**涟漪(本批已做)**:
1. `skeuomorphic-ui.md` `AC-42-F1` NOT-RUN 注回刷(第二十九批→第三十批):「①–⑥ 六员 spec 全部成文
   Draft…『七份 spec 全在』的条件自此满足,但走查项整体仍记 NOT-RUN,不得记 ✅」+ 硬前置清单
   (OQ-TUT-1 / OQ-CP-1 / OQ-CP-2 / R8 / 45)+ 纸堆孤儿引据注(点名文本不删,挂 consistency-failures)
2. `docs/consistency-failures.md`:汇总表加一行(纸堆孤儿引据,Status = Open(登记))+ 详细条目
   `### [2026-09-21] — 第三十批…`(失效模式 =「引用却无登记」家族**第三变体:点名不存在的形态**;
   Pattern 行给出通用判据「闭集点名 = 立宪行为,点名的修饰语须能在被点名系统正文 grep 到同义承载」)
3. 三件 §15 涟漪清单相互对得上(skeuomorphic-ui 注点名的 BLOCKED 清单 = 三件 §14 的 OQ 集)

**机制数值零变动**(冻结,承「数值用户自己调」;三件的判据全为比值式 / 单调函数形状,量纲随 cap 走)。

**仍开放(未代拍)**:新硬题 **OQ-TUT-1**(⑤ 形态甲乙丙)、**OQ-CP-1**(面板读法甲乙丙)—— 两案的乙/丙
都触 ADR-013,非 spec 可自决面;继承项 OQ-CB-1 / OQ-CB-5 / OQ-42-10 / OQ-SS-2 / OQ-SS-4 / OQ-42-6 半② /
OQ-IC-2(医馆储具柜是否复用模态)/ OQ-CP-2 / OQ-CP-3 / OQ-CP-4 / R8(3 消费者表补 20)。

**下一步队列**:七屏(①–⑦,①–⑥ 全 Draft)的 `/ux-review` 轮 · **攒裁清单待用户拍**(OQ-TUT-1 /
OQ-CP-1 两组甲乙丙优先)· D-R2 随 `tools/kindgen` 同批 · R-A/R-B/R-C spikes(实现轮)·
U0a(桌面 Unity 6.3 + license,挂起)→ U0(六装配骨架)· git commit(仅用户指示)。

**同批追加裁定(2026-09-21 用户,widget 双问)**:**OQ-TUT-1 = 甲(调度壳)** ·
**OQ-CP-1 = 甲(摆完开读)** —— 两案均不触 ADR-013(乙/丙作废)。结案涟漪已落 6 件:
① `tutorial-and-onboarding.md` UI-48.1 后加「⑤ 呈现落点注」(48 规则正文零改写,走近再说界面化 =
壳,规则一不变)② `skeuomorphic-ui.md` AC-42-F1 注块回刷(两 OQ 划除标已裁,「纸堆翻页」六字读法
定型 = 按调度壳走查,点名不删)③ `tutorial-48.md` §0 裁定注 + §1/§3 措辞 + §13 BLOCKED 前置由
OQ-TUT-1 **转移至 OQ-TUT-2**(三侧路由登记仍欠)+ §14 结案行 + §15 之 2 兑现 ④
`clinic-panel-24.md` §0 之 6 + §2 限定段转正式口径 + §13 执行体残余阻塞 = 仅 `BLOCKED-BY-45` +
§14/§15 结案行 ⑤ `consistency-failures.md` 表行 Open→Resolved + 详细条目追记 ⑥ 本块。

---

## /ux-review 轮(2026-09-22 · 六屏 Draft spec 首轮评审 · 五份报告全收 · 修复全落)

**范围**:`design/ux/` 六屏 Draft(① casebook-39 / ② save-slots-7b / ③ inventory-container-20 /
④ settings-shell-42 / ⑤ tutorial-48 / ⑥ clinic-panel-24)+ patterns(⑦ paper-closeup-48 已
Approved 免审,本轮仅 `:185` 行号注式直修)。**纪律**:每条子代理结论主会话独立 grep 复算后才动手;
事实/措辞/引用级直修(注式留痕);BLOCKING 属真设计冲突/跨件机制面 → 登记不代拍。

**逐份判定**:
- ② save-slots-7b / ④ settings-shell / ⑤ tutorial:报告无 BLOCKING 设计冲突,事实级已自修(④ 的
  OQ-SS-7 即上轮登记式处置的先例件)。
- ⑥ clinic-panel:EnvMod BLOCKING-1 → **OQ-CP-6 新立**(左秤读源甲/乙 + 映射域,归 24 修订轮),
  §5.2/§10/§13 三处挂 BLOCKED-BY;记号纪律 / 焦点声明 / 三条 AC 重写全落。
- ③ inventory:四 BLOCKING 全修 —— B1 满档改取 `AC-20-18` 具名谓词 `> cap − W_MIN`(原稿用了被该
  AC 明令禁止的 `== cap` 简化式);B2 `:221` 改挂 `BLOCKED-BY-R13`(原稿把未生效改判请求读成已立义务);
  B3 ≤6 字改自裁 + §15 之 5 新立 21a schema 回写涟漪;B4 锚 `:1224`/`:147` 订正;A1 判据优先序;
  A3 补 AC-20-23;A4「引用 vs OQ-IC-4」矛盾改「版式=引用 / 材质档=扩员请求」。
- ① casebook:B1 色觉变体归属改 42/烘焙侧(`accessibility:88` + `AC-8-21`);B2 锚 `:771`(「规则八」
  次断言复算**在册非失实**,不动);A3 `:132` ×2;A4 删冗;A5 §13 补开册渲染 `[L]` 条(`paper-closeup-48:281` 先例)。

**登记批**:`docs/consistency-failures.md` 表行 + 详细条目(六族失效模式:幽灵引据扩散 / §十 off-by-one /
行号漂移族 / **引用即绿型**(= 幽灵引据的对偶失效,Pattern 已泛化:抄权威件结论必须连禁令/裁定状态一起抄)/
出处漂移 / 执行体缺位);`skeuomorphic-ui.md:1527` 陈旧括注「①–⑥ 无 UX spec」回刷;
`technical-preferences.md` ADR-013 日志残留行(前段已落)。

**机制数值零变动**(冻结,承「数值用户自己调」;`W_MIN` 是 20 侧既载常量非新数值;比值阈值仍是本 spec 可调域)。

**仍开放(未代拍)**:OQ-CP-6b(数值轮)· OQ-CB-5 · OQ-IC-6(变形未消失)/R13 挂钩 ·
OQ-SS-3/5 · OQ-42-6 半②(进壳上界)· OQ-42-4/5 · OQ-42-14 · R8 · 45 联机夹具 ——
`AC-42-F1` 闭集走查整体仍 NOT-RUN。

**⚠️ 42 重开触点已触发**(2026-09-22):重开触发条件 ④(`OQ-42-1`/`OQ-42-12` 裁定落地)已达成,
**是否据此重开 42 的 `/design-review` 由用户决定**(未代裁);其余三条件(spike 落盘 / `AC-42-B4` 降级启用 / F4 三层口径被证伪)均未触发。

**下一步队列**:42 重开与否待用户点头 · git commit(仅用户指示)·
D-R2 随 tools/kindgen 同批 · R-A/R-B/R-C spikes(实现轮)· U0a(挂起)→ U0 六装配骨架。

<!-- UX-REVIEW-COMMIT: 2026-09-22 | commit 7e44395 (12 files, +1838/-11, 未 push) | 六屏 Approved 翻转与 /ux-review 修复轮入库 | consistency-failures.md 按 .gitignore 设计不入库(条目仍在其内,工作树保留) | 下一步:D-R2 随 tools/kindgen · R-A/R-B/R-C spikes · 设计面 OQ 待裁(OQ-CP-6 优先) -->
<!-- OQ-CP-6-RULED: 2026-09-22 | 用户裁定 OQ-CP-6 = 甲(左秤读 24 本机未钳制 EnvMod_clinic;乙案作废) | 映射域形态 = 固定显示域 + 超界钉满角(spec 外推权) | 域端点定值拆出 OQ-CP-6b 归 24 修订案数值轮 | 落地:clinic-panel-24.md §5.2 之 1 / §10 / §13 / §14 + consistency-failures.md 跟进注 | BLOCKING-1 解除,§13 倾角单调条改「性质可测、点位禁刻蚀」 | 下一步:设计面 OQ 批裁轮(剩余:OQ-CP-2/3/4/5 · OQ-CB-1/3/4/5 · OQ-IC-2/4/6 · OQ-SS-2/3/4/5 · OQ-TUT-2 · OQ-24-1..6 · R8 · R13) -->
<!-- OQ-TUT-2-RULED: 2026-09-22 | 批裁结案 OQ-TUT-2(开屏路由登记缺口) | 不再需要新动作:纸近景经 4 交互判定投递(复用 Submit/Interact)、可重听 = 呈现层本地回放零流事件、48 侧零新登记义务 | 残余真未裁点「触发方式(近距离自动 vs Submit 边沿)」拆给 48 × 4 实现轮 = 新立 OQ-4-19(首版误排 OQ-4-18,与 P1b 拾取失败反馈撞号,已改排) | 落地:tutorial-48.md §2/§10/§13/§14 · interaction-system.md OQ-4-19 · input-system.md 规则二两行注 · tutorial-and-onboarding.md 规则四注 · adr-013 §十-B 补注 · consistency-failures.md 跟进注 | 零新机制零新 AC | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-2/3/4/5 · OQ-CB-1/3/4/5 · OQ-IC-2/4/6 · OQ-SS-2/3/4/5 · OQ-24-1..6 · R8 · R13 · OQ-CP-6b) -->
<!-- OQ-CP-2-RULED: 2026-09-22 | 用户裁定 OQ-CP-2 = 甲「案头账本 + Interact」 | 面板载体 = 医馆案头账本(呈现物,与教学纸同族);开 = 走近 → Interact → 4 投递 → 42 开 ModalId.ClinicPanel;关 = 屏内 toggle;零新动作;站外拒开 = 载体摆点域外无载体天然成立 | 乙(专用动作资产)/丙(复用 OpenInventory)作废 | 三侧登记:① 1 的 GDD 面板开关行撤回(4 是投递方,1 只被 MotorSuppressed 压制)② 3 消费者表 Interact 行补 24(经 4)③ 42 表行改 4 交互投递 + 载体摆点归 6 | 落地:clinic-panel-24.md §0/§1/§3/§4/§7/§13/§14/§15 · player-controller-and-movement.md UI Requirements 行 · input-system.md Interact 行补注 · skeuomorphic-ui.md 屏清单行 · 六屏 Status 头行划除 · technical-preferences.md ADR-013 日志回刷 · consistency-failures.md 跟进注 | 机制数值零变动 | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-3/4/5 · OQ-CP-6b · OQ-CB-1/3/4/5 · OQ-IC-2/4/6 · OQ-SS-2/3/4/5 · OQ-24-1..6 · R8 · R13) -->
<!-- OQ-CP-3-RULED: 2026-09-22 | 用户裁定 OQ-CP-3 = 甲「新登记『印』」 | 「印」登记为记号登记表第五形状(语义 = 计入/卸出,拥有者 = 24 内容定域 + 42 呈现) | 履行规则②(新语义须新形状):印章元件本就库内在册(规则五四件),新增的是记号语义非元件缺失 ⇒ §5.3「同名不同物」澄清收窄为「元件在册、语义新登记」 | 退化形全作废:乙(行墨字加粗)不再候选;朱圈改墨圈(色-only 通道)本就不行 | 落地:skeuomorphic-ui.md 记号登记表新行 + 注 · clinic-panel-24.md §5.2/§5.3/§13(印非勾)/§14 结案行/§15 涟漪 4 | OQ-IC-4(器具材质档)仍独立待裁,互不吞并 | 机制数值零变动 | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-4/5 · OQ-CP-6b · OQ-CB-1/3/4/5 · OQ-IC-2/4/6 · OQ-SS-2/3/4/5 · OQ-24-1..6 · R8 · R13) -->
<!-- OQ-CP-4-RULED: 2026-09-22 | 用户裁定 OQ-CP-4 = 甲「ClinicEnvDto」 | 具名 = ClinicEnvDto;查询接口 = IClinicEnvQuery.GetEnv(room) → ClinicEnvDto;字段集 = roomName(词表索引)/ contexts[](情境词表索引,可空集)/ envMod / equipMod(均 Q16.16 raw,envMod 未钳制房间分量、equipMod 非负);零 disease_id、零 float 字段;住 Sim.Contracts(与 VitalsDto 同程序集同浮点出口纪律) | 乙(Summary 后缀)/ 丙(留 U0 装配轮)作废 | 落地:clinic-machine.md O-24-3 补注 + 三处表行回刷 · adr-025 §① Sim.Contracts 成员列 · clinic-panel-24.md §0 之 5/§6/§10/§13/§14 结案行 | 程序集归属已同步 ADR-025 | 机制数值零变动 | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-5 · OQ-CP-6b · OQ-CB-1/3/4/5 · OQ-IC-2/4/6 · OQ-SS-2/3/4/5 · OQ-24-1..6 · R8 · R13) -->
<!-- OQ-CB-1-RULED: 2026-09-22 | 用户裁定 OQ-CB-1 = 甲「病名册指认,键鼠与手柄同路径」 | 键鼠侧病名栏 = 同一本病名册翻到并「指认」(统一 lexicon_id 通道);键盘自由文本停用 ⇒ freehand_text 通道 P0 无任何设备 UI 入口(机制层字段保留,登记残留) | 裁定效力:反幻想二全守(「见过」耦合)/ 双导航等价天然成立 / AC-39-11 判据原样保留可签 / 8 裁定⑥键鼠侧形态位收窄 | 乙(手写板另立承诺)/丙(键盘自由文本)作废 | 注册面:39 GDD OQ-39-6 结案(「自由文本」作废)+ systems-index + casebook-review-log 残留未结清单随删 | 落地:casebook-39.md §6/§7/§13/§14 · diagnosis-system.md 裁定⑥归回写注 · casebook.md OQ-39-6 结案行 · systems-index.md 行 39 两处 · casebook-review-log.md 残留未结+核点②⑤ · consistency-failures.md 新条目 | 机制数值零变动;freehand_text 例外字段 ADR-006/008 登记不动 | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-5 · OQ-CP-6b · OQ-CB-3/4/5 · OQ-IC-2/4/6 · OQ-SS-2/3/4/5 · OQ-24-1..6 · R8 · R13 · 45 联机夹具) -->
<!-- OQ-CP-5-RULED: 2026-09-22 | OQ-CP-5 前提证伪结案(非用户 widget —— 24 F-24-2 clamp 下界字面写死 0,EquipMod ∈ [0, EQUIP_MOD_CAP] 恒成立且与 cap 定值无关) | 右秤「只上扬/平」从「本件读法」升格为机制保证;补对称分支结构性不存在 | 落地:clinic-panel-24.md §5.2 之 1(注式改机制保证)/§14 结案行 | 非 24 修订案数值轮;若未来改 clamp 下界须先改 24 GDD | 机制数值零变动 | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-6b(数值轮)· OQ-CB-3/4/5 · OQ-IC-2/4/6 · OQ-SS-2/3/4/5 · OQ-24-1..6 · R8 · R13 · 45 联机夹具) -->
<!-- OQ-IC-2-RULED: 2026-09-22 | 用户裁定 OQ-IC-2 = 甲「P0 不立柜/架模态」 | 「储物箱取存」不在任何 P0 授权 GDD 承诺内(grep 实证零命中);③ 箱模态 = P0 唯一容器屏(玩家随身);柜/架 = 世界器物(23/24 家具件,视觉仅存、无取存屏)⇒ 闭集 7 员不增员 · 零新走查面 | 登记残留:若 P1a 立「医馆储物取存」,走 ⑦ 并入式扩充先例(ADR-013 §十-B,并入既有闭集员非增员)+ 4 world-space 路由 | 乙(立第二形态)/ 丙(专用动作资产)作废 | 落地:inventory-container-20.md §14 结案行 + Status 头划除 · clinic-panel-24.md §15 之 4 收窄注(P0 不触发) · consistency-failures.md 新条目 | 机制数值零变动 | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-6b(数值轮)· OQ-CB-3/4/5 · OQ-IC-4/6 · OQ-SS-2/3/4/5 · OQ-24-1..6 · R8 · R13 · 45 联机夹具) -->
<!-- OQ-42-10-RULED: 2026-09-22 | 用户裁定 OQ-42-10 = 甲「无通用恢复」 | 42 不持「上次焦点位置」(零新持久化义务);重开任何模态 = 焦点落首可聚焦元素;39 的恢复 = 分册态段既成事实(例外非能力,不升格为通用机制);7b/settings 的「无恢复」占位转正为已裁口径 | 乙(42 提供恢复能力:新机制+新AC+走查面扩,与「焦点位置不在持久集」既载口径冲突)/丙(屏级自裁)作废 | 同批结案:OQ-SB-1(7b)+ OQ-SS-6(settings);OQ-IC-3(20,同族更窄)随批结案 | 零新字段·零新AC·走查面不扩 | 落地:skeuomorphic-ui.md OQ-42-10 行 · save-slots-7b.md §1/§7.1/§14 OQ-SB-1 三处 · settings-shell-42.md §14 OQ-SS-6/§15 之4 两处 · casebook-39.md §7.1/§15 之4(39 主体不动只加结案注) · inventory-container-20.md OQ-IC-3 · consistency-failures.md 新条目 | 机制数值零变动 | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-6b(数值轮)· OQ-CB-3/4/5 · OQ-IC-4/6 · OQ-SS-2/3/4/5 · OQ-24-1..6 · R8 · R13 · 45 联机夹具) -->
<!-- OQ-ELEMLIB-RULED: 2026-09-22 | 用户裁定 42 元件库三项扩员边界(同批) | ① 折角/折边(OQ-CB-4 + OQ-SB-4)= 甲「纸面手法变体」—— 承 §二 墨侧允许手法「纸的压痕与折角」,走九宫格偏移 + USS 类切换(同 V-1/V-3 压痕先例),不增第五元件;7b 折角承载语义 ⇒ 补入 §二 记号登记表新行(单折 = 不可写 / 双叠 = Locked)+ 跨屏同族形状可分义务(vs 21 叠角) | ② 黄铜控件(OQ-SS-4)= 甲「收黄铜控件为黄铜侧子类」—— 黄铜侧二分读数类/控件类,settings 壳滑杆/拨栓/系绳牌三件入库,AC-42-F3 纳入 | ③ 器具材质档(OQ-IC-4)= 甲「收器具第三材质档(木/皮/铜)」—— 库由双材质语言扩为三档,20 箱屏四件入库;AC-42-F3 分支枚举 2→3;⚠️「器具档自身禁不禁刻度条」本次不裁(只立档) | 乙/丙案全作废 | 落地:skeuomorphic-ui.md 规则五 ⭑ 2026-09-22 注块(新)+ §二 记号登记表新行 + 表注形状可分义务 + AC-42-F3 分支枚举注 · casebook-39.md §5.3/§14 OQ-CB-4 · save-slots-7b.md §5.3/§10/§14 OQ-SB-4 · settings-shell-42.md §5.3 三行/§10/§14 OQ-SS-4 · inventory-container-20.md §5.2/§5.3 两行/§14 OQ-IC-4/§15 之3 · clinic-panel-24.md §14 OQ-CP-3 注/§15 之4 · consistency-failures.md 新条目 | 机制数值零变动 · 零新 AC 条(仅分支枚举扩) | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-6b(数值轮)· OQ-CB-3/5 · OQ-IC-6 · OQ-SS-2/3/5 · OQ-24-1..6 · R8 · R13 · 45 联机夹具) -->
<!-- OQ-42-1-42-12-RULED: 2026-09-22 | 用户裁定两项(均照准) | ① OQ-42-1 = 甲「恒定屏幕尺寸」—— F6 单一化 scale_world = k_screen × 2·d × tan(FOV_v/2)(屏幕占比恒定);乙「恒定世界尺寸」作废,k_world 撤出旋钮表(算例留作裁决依据) | 后果:42 新增对 2 的 FOV_v 依赖边(无条件成立,原「⚠️ 半」解除);2 侧反向义务 O-15 闭合;42 装载期断言 ④ 由条件断言升常驻 | ② OQ-42-12 = 甲「取 min」—— F7 单一化 s = min(W_screen/W_ref, H_screen/H_ref)(一侧留黑);乙「加权 match」作废,权重 w 不再定义;F7 装载期断言 s>0 自此可写 | **重开触发条件 ④ 已触发**(recorded,未重开):是否据此重开 42 的 /design-review 由用户决定(不代裁) | 其余三条件(① spike 落盘 ② AC-42-B4 降级启用 ③ F4 三层口径被证伪)均未触发 | 落地:skeuomorphic-ui.md F6/F7 段+算例表+文件头④+§Dependencies §一/§三+§六 风险表+§Tuning Knobs §二+OQ 表两行 · camera-and-viewpoint.md 42 下游行+O-15 两处+FOV_v 旋钮注+验收前提⑤+OQ-2-2 · systems-index.md row 42 两处 · skeuomorphic-ui-review-log.md 新增「裁定落地注记」段+未结项/Unresolved 划除 · consistency-failures.md 新条目 | 机制数值零变动(k_screen 仍旋钮,值归数值轮) | 下一步:设计面 OQ 批裁轮 -->
<!-- OQ-SS-2-CB-3-RULED: 2026-09-22 | 用户裁定两项(均照准) | ① OQ-SS-2 = 甲「验收随 49(P2),P0 不早进」—— 规则八「P0 唯一设置项 = 44 音量/mono」保持;无障碍 Standard 承诺(色盲 :88 / UI 缩放 :91 / 减动效 :93)的档位验收随 49(P2),不早于 49 进架;乙(P0 早进架)作废;残余 OQ-42-6 半②(条目进壳上界)仍 open(本裁只定下界) | ② OQ-CB-3 = 甲「转『一案两摊』(零滚动)」—— 150% 缩放 × 恒定行高溢出改由续页组织吸收,不引入滚动;§5.2 恒定行高判据不变;§11 文本缩放行「待裁」转正;乙(允许滚动)作废;与 ②册/③箱同族 | 落地:settings-shell-42.md §1 张力注/§11 textScale 行/§14 OQ-SS-2 结案行 · casebook-39.md §5.2 纸页容量行/§11 文本缩放行/§14 OQ-CB-3 结案行 · consistency-failures.md 新条目 | 机制数值零变动 | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-6b(数值轮)· OQ-CB-5 · OQ-IC-6 · OQ-SS-3/5 · OQ-24-1..6 · R8 · R13 · 45 联机夹具) -->
<!-- OQ-42-3-RULED: 2026-09-22 | 用户裁定 OQ-42-3 = 甲「不虚拟化(翻页制)」 | 全案 UI 列表/多页签一律走翻页制(零滚动),不引入虚拟化 | 依据 = 六屏实证全部落翻页形态(②册每页6行转册续页/③箱每屏≤12件翻页/⑦教学纸逐条/④设置壳150%压页拆页/⑥医馆面板/①脉案一案两摊)⇒ 无「无限长滚动列表」成文义务;滚动=破纸面物理感+破恒定行高判据(承 OQ-CB-3 甲) | 后果四处连锁解除:① F5 的 Pages_frame 语义定为「单页可视元素集的函数」(非滚动位置)⇒ §F5 接缝注消解、公式无需回填 ② LIST_VIRT_THRESHOLD 旋钮撤出(该量不存在),代之以单页可视元素数上限 ③ E-13 失效模式收窄为「单页元素数超 F5 阈值即断批」,应对=压页/拆页/转册续页、零滚动条 ④ §二 信息真空表 ④ 缺口 + OQ-42-11 ④ 归 20 的翻页制(上界=每屏件数非 K,COLUMNS=4/ROWS_PER_PAGE=3) | 乙(虚拟化:新旋钮+新滚动空白/焦点跳页验收面+与纸面物理族纪律相抵)/丙(按屏分治,Pages_frame 双语义并存)作废 | 落地:skeuomorphic-ui.md §F5 接缝注/E-13/§六 风险表行/§Tuning Knobs 三 LIST_VIRT_THRESHOLD 行/§二 信息真空表 ④ 行/OQ-42-3 结案行/OQ-42-11 行 · inventory-and-items.md 规则十 2/Dependencies 注④ · consistency-failures.md 新条目 | 机制数值零变动(PAGES_MAX 仍由 spike 定、COLUMNS/ROWS_PER_PAGE 仍归 42 布局参数) | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-6b(数值轮)· OQ-CB-5 · OQ-IC-6 · OQ-SS-3/5 · OQ-24-1..6 · R8 · R13 · 45 联机夹具) -->

<!-- U0A-CLOSED: 2026-09-22 | U0a 工具链核验闭合 | 编辑器 Unity 6000.3.24f1 (changeset 4e7b9b5b6244) · Ubuntu 22.04 · IL2CPP 模块已装 · Personal license 已激活 · 空 URP 工程 Play Mode 已验 | 用户提供补丁号 + changeset(ProjectVersion.txt m_EditorVersionWithRevision) | 落地四处:① VERSION.md Engine Version/Editor Revision(changeset 新行)/Pinned On(新行)/Project Pinned/Last Docs Verified 五行刷 2026-09-22 ② tests.yml:28 UNITY_VERSION 6000.3.TBDf1 → 6000.3.24f1(注释转「已回填」) ③ u0a-toolchain-checklist.md Status OPEN→CLOSED + 五步全勾 + 新「本机环境登记」表 + 回填块三勾 ④ 门报告 2026-09-21 开工序第 1 条加闭合注(原文未改,承「加复核注不改写」惯例) | ⚠️ 事实澄清:changeset 全仓无消费者(tests.yml 只吃版本号,game-ci 自解析),新增 VERSION.md 行给它落点 | Release Date 保持 December 2025(6.3 产品线发布日;24f1 补丁点日期无权威件,不代填) | 残项:CI UNITY_LICENSE secret 仍待手工配(不属本卡) | 下一步:U0(src/ 工程根 + ADR-025 六装配 asmdef + 种子测试转绿)→ U1 spike 批 -->
<!-- OQ-CB-5-RULED: 2026-09-22 | 用户裁定 OQ-CB-5 = 甲「循环落笔(零控件)」 | 置信度档位 = 病名栏的重复落笔循环:指认落笔 ⇒ 疑似 · 同栏再 Submit ⇒ 确定 · 第三落 ⇒ 擦除回空(恰三态 = S-8.3,零第四档);零新焦点位 / 零新动作 / 零新机制;档位经已有落笔 / 改判事件载荷 judgment.confidence:u8 承载,不产独立流事件 | 乙(时长映射)/丙(显式置信位)作废 | ⚠️ 甲裁暴露 8 / 42 陈旧面:diagnosis-system.md 规则六(「勾一档置信度」)· UI-8.2 焦点序 · AC-8-23 · AC-8-44 · skeuomorphic-ui.md F3 算例(K = 7) 均仍把置信度当第 7 个可聚焦元素 ⇒ 甲下脉案可聚焦元素 = 五通道 + 病名栏(K = 6);8 侧已加归回写注(焦点序语义订正须走 8 修订轮,本裁不代改) | 落地:casebook-39.md §5.2/§7/§13/§14/§15 + Status 头 · diagnosis-system.md 规则六归回写注 · casebook-review-log.md 批裁轮补记 · systems-index.md row 39 · consistency-failures.md 新条目 | 机制数值零变动 | 下一步:设计面 OQ 批裁轮(剩余真实决策点 = OQ-CP-6b(数值轮)· OQ-IC-6 · OQ-SS-3/5 · OQ-24-1..6 · R8 · R13 · 45 联机夹具) -->

<!-- AC29-DONE-F7-PENDING: 2026-09-24 | item-database Story 011 AC-21a-29 补挂账 VERIFIED | 双腿实测:编辑器 PlayMode(Mono)3/3 + StandaloneLinux64 UTF player(IL2CPP)3/3,外部三方 diff golden/Mono/IL2CPP 19/19 逐位相同,F7 峰值向量 player 侧已知值全过 | 新增:Tests/PlayMode/determinism_golden_crossplatform_test.cs(3 测,增引 Sim.Codec GUID)· Editor.Tools.Spike/Ac29Il2CppSwitch.cs(后端切换,已归位)· production/qa/evidence/ac-29-il2cpp-crosscheck-2026-09-24.md + ac29-hashes-{mono,il2cpp}.txt | 回归:EditMode 463/463 + PlayMode 15/15 | 过程订正:后端标记首版 Mono.Runtime 判据为假(改 FrameworkDescription)· unity run -quit 吞 -runTests(改直调 Unity 二进制) | **待完成任务:F7 反汇编验无 FMA**(ADR-012 §三:objdump 抽 GameAssembly.so 的 Avalanche/MulRaw 符号验无 vfmadd + --compiler-flags= 语义 spike;本机可跑,材料 unity/Library/Bee/artifacts/.../GameAssembly.so;纵深防御,sim 全整数域零杠杆)· 其余残余归 CI 矩阵轮(ARM64 交叉格/发版前两格/三格常驻 job,需 UNITY_LICENSE secret) | Story 012 同日完成(21a 登记面,0/6 勾,六 AC 按 Guardrail 全不勾) | 21a epic 12/12 全 Complete | 下一步:F7 反汇编(用户点头即跑)· git commit + push(本批) -->

<!-- F7-DISASM-DONE: 2026-09-25 | F7 反汇编验无 FMA 跑毕(CLEAN) | 对象 = AC-29 的 GameAssembly.so(553MB, C/Link_Linux_x64_Clang) | Fix_MulRaw(0x3df3730,73 条)与 SplitMix64_Avalanche(0x3df4cf0,15 条)按 mnemonic 正则扫 FMA 家族 + xmm/cvt 全 0 命中;旁证:MulRaw 纯 imul/adc/shld hi-lo 结构、Avalanche 内嵌 Mul1/Mul2 金标准常量字面;SetAdditionalIl2CppArgs/--compiler-flags/-ffp-contract 日志 0 命中(默认旗标) | 证据落 production/qa/evidence/ac-29-il2cpp-crosscheck-2026-09-24.md §F7 反汇编(残余表 F7 行勾销) | 仍挂账:ARM64/发版前两格/CI 三格常驻/逐目标旗标登记表 | 下一步:ADR-023 S1/S3/S4 Validation 回填 -->

<!-- R13-SS5-RULED: 2026-09-25 | 批裁轮 A 组两条结案(用户拍板) | R13 = 甲「MAX_QUALITY>1 最小非空」含成药侧对称 —— 21a item-database.md D-21-16/D-21-24 改判注 + §B2 prose(可空行划废)+ Schema 两行 + D-21-6 块级例外 + AC-21a-50b/62 空列硬失败扩充全落;inventory-and-items.md 头注触发②未成立(20 不重开)+ R 表行闭合 + :766 注;inventory-container-20.md:232 回刷(BLOCKED-BY-R13 转待回写) | OQ-SS-5 = 甲「端本地 sidecar」—— settings-shell-42.md OQ-SS-5 行结案(持有者=边界层设置 store,单向推 AudioMixer;乙随档/丙 PlayerPrefs 否决)+ OQ-SS-7 升实质必需注 + :164 注;audio-system.md Tuning Knobs 注 + 已结案表行 | consistency-failures.md 跟进结案双条 | 回写执行待实现轮(云端故事面):P0 数据填词(原料 willow_bark x5 + 成药条目 x5)+ 门扩(空列硬失败)+ 负向 fixture —— 未兑现前 AC-20-22/23 不记绿 | 实查留档:P0 数据 quality_character 键不存在 / drug_quality_character:null / max_quality=5 | 批裁队列余:OQ-CP-6b + OQ-24-1..6(数值轮) · OQ-SS-3(归44) · OQ-IC-6(playtest) · 45夹具(P1b) · R8=执行活(3 消费者表补 20,可随 input-system 轮) | 未提交,等用户指令 -->

<!-- R8-REWROTE: 2026-09-25 | R8 执行活完成 —— input-system.md 规则二 PagePrev/PageNext 消费者行补 20(经 42 焦点头投递,箱内翻页/末页,BL-3) | 涟漪:inventory-and-items.md R8 行闭合(✅ 已回写)+ AC-20-20 头改 BLOCKED-BY-R9(R8 半边解除)· inventory-container-20.md :255 走查条 + :278 涟漪注更新 · skeuomorphic-ui.md :1563 AC-42-F1 残余前置划除 R8 · 六屏 spec 头注「残余硬前置 = R8 / 45」×7 处统一刷为 ~~R8~~✅ / 45 | 残余硬前置余:45 联机夹具(P1b)· R9(页可见集归 42)· 七屏走查 NOT-RUN | 批裁队列余:OQ-CP-6b + OQ-24-1..6(数值轮) · OQ-SS-3(归44) · OQ-IC-6(playtest) · 45夹具 | 改动 10 文件未提交,等用户指令 -->

<!-- NUMROUND-2026-09-25-RULED: 2026-09-25 | 数值轮三批全拍(用户;Opus 案卷辅助) | 第1批 21a 常量:QTY_MULT_MAX=2 · SKILL/QUAL_MOD_CAP=1/10 · EQUIP_MOD_CAP=1/4 · ENV_MOD_MIN=-1/2 · ENV_MOD_MAX=+1/2 | 第2批 24 表:ADJ W域=[-1/4,+1/4] · K_speed档集={1,7/8,3/4} ⇒ K_CONTEXT_MAX 派生=1(零抬 LATTICE_SIZE) · TIER_MAX=3(待 FURN_TABLE) | 第3批 OQ-CP-6b=甲:DISPLAY=[-1/2,+1/2](复用 ENV 带,零新常量) | 强制配套(锁逼出):配方 extract_salicylic 输入 qty 1→2(守恒上界 QMAX≤2 · AC-21a-8);AC-21a-9 下界实算 1.95 ≤ 2 ✓ | Opus 结构发现随拍记账:CAP<1 ⇒ F-24-2 二值开关语义,P0 接受(渐变须改公式=机制改动) | 落盘:constants.json 6 字段 + _note 回刷 · recipes.json 单行 · item-database.md Tuning 5 行 · clinic-machine.md OQ-24-1/2/6 + Tuning 表 + F-24-2 二值注 + AC-24-01 夹具注 · clinic-panel-24.md OQ-CP-6b/读界行/倾角 AC/:116 · player-controller :461 K_CONTEXT_MAX=1 | 验证:烘焙绿(ConfigVersion 0x53DD5D0F→0x960516D9,AC-21a-8/9 两门过)· data-core 组 2 条 · EditMode 477/477 · PlayMode 15/15 | 仍待定(正确保留):QTY_MULT_MIN · RETAIN_MIN/MAX · e_env/base_env/C_max(内容轮 OQ-24-3/5)· K_TERRAIN_MAX(6) | 未提交,等用户指令 -->

<!-- PLAYER-SUITE-W2-2026-09-25: 2026-09-25 | 桌面 player 套件两跑 + W2 复验 PASS | 动因 = 云端 577ce7f(analyzer Editor-only 收敛)+ ADR-012 挂账注「player 套件执行归桌面轮」 | 第1跑未注入金标准:exit 0 · 14/14 + 1 设计 Skip;第2跑注入 AC29_GOLDEN:exit 0 · 15/15 全绿(player 内金标准就地断言) | W2 原炸点(UnityLinker AssemblyResolutionException/exit 3)0 命中 | AC-29 第三腿:Mono player 19 条四方对拍(golden/编辑器Mono/IL2CPP/Mono-player)= ALL_IDENTICAL,证据入库 281552e(ac29-hashes-player-2026-09-25.txt + 判决书第三腿章节) | 结果 XML 在 unity/Logs(gitignore 不入库) -->

<!-- R13-REWROTE-DONE: 2026-09-25 | R13 回写执行闭环(裁定→改判→执行三段全落) | 数据:willow_bark/raw quality_character 5 档(枯脆细碎/皮薄色暗/条匀皮厚/条肥色正/皮厚丝丰)+ salicylic_acid/extracted drug_quality_character 5 档(浑浊沉淀/色浊欠匀/清亮尚匀/澄明匀净/澄澈晶莹)—— 用户审定,过 OQ-17-9 筛(零采后词) | 门:DrugProfileGates.ValidateCharacterTableLength 扩 maxQuality>1 空列/null 硬失败 + 逐档非空(maxQuality<=1 留旧口径边界自证);契约注 GatherProfile/DrugProfile 同步 | fixture:invalid_gather/drug_char_empty.json ×2 + 空列/空白/max1 测试组(空槽 new string[N] 踩坑已修 —— null 槽被逐档非空拦,改真词) | 烘焙:items cooked 444→604B,ConfigVersion 0x12F85A18,门全过;data-core 2 条 | 回归:EditMode 484/484 + PlayMode 15/15 | 回填:foraging ×6(OQ-17-9 结案+废例注+AC-17-05c+过渡关闭)· item-database ×4(§B2 废例注+回写义务三勾+AC-50b/62)· inventory-and-items ×3 · inventory-container-20 ×1 · consistency-failures 跟进(工作树) | 残余:AC-20-22/23/AC-17-05c 本体执行归实现轮(不借回写记绿) | 14 文件未提交,等用户指令 -->

<!-- NUMBATCH2-2026-09-25: 2026-09-25 | 数值批 2(F1/F2 常量收尾)全按主推拍板 | QTY_MULT_MIN=0 · RETAIN_MIN=1/2 · RETAIN_MAX=1 · EFF_MIN=1/2 · EFF_MAX=1 —— 五值全 = 种子(数据值零改动,仅 constants.json _note 回刷为「两批全拍」) | GDD:item-database Tuning 5 行翻已定 + MAX_QUALITY 行补推迟理由(golden-v1 耦合改须重签)+ 两 character 行落值回刷并**清除残留「陈放」禁词示例**(§B2 表内,自检漏网第二处) | 仍推迟:MAX_QUALITY/SKILL_CAP(golden-v2 重签件)· perceptible_floor(与 9 噪声带同批)· quality_axis/axis_offset(逐条数据+D-21-23 既裁) | 验证:重烘 ConfigVersion 0x12F85A18→0x1661EBE7(retain/eff/cap 门全过)+ data-core 2 条 + EditMode 484/484 + PlayMode 15/15 | 4 文件未提交,等用户指令 -->

<!-- AUD-44-R2-REVISED: 2026-09-25 | 44 音频 GDD 二轮评审 → MAJOR → 现在就修(批次0+1+2 已落) | 评审:9 专家并行对抗 + CD(Opus)终裁;首轮 2026-09-18 MAJOR→免二轮的风险接受本轮兑现(3 残留 + 4 新缺口) | CD 归并 14 阻断 F1-F14 | 批次0 七拍板(全取推荐):F1=①黑名单+IL扫描 · mono=甲升a11y · F4=甲TierMap一张 · F7=甲改:188 · F9=乙上游广播两消费方 · OQ-SS-3=甲数据七路分组归42 · OQ-SS-7=甲页级归源钮 | 修订落笔(4 文件):audio-system.md ×50+ 处(Edge Cases 标题+校验规则5-8 · 快照:188/滑块两级组 · EndLoop 随 ADR-001 裁决二收口 · F-44.1 6列含signal_db · F-44.2 分析/渲染域合成规则+构建期钳位 · SelectVariant 轴对齐 · F-44.7 bus定Ambience/交接契约/组件pin/occlusion raycast登记 · 优先级 rank_key 公式+cell_jitter · Schema 字段增补(tier_map/clock_ref/adventitious_policy/xfade_ms/subtitle_text) · AC 重写(B1/B3/C1/01/05/06/09/14/15/D7/D8/E2/E3)+新增16接线/17听测/18音乐护栏/19归源 · AC计数32→36 · OQ-SS-3/7 登记 · 依赖表三处过期回刷 · trigger_source 降调 · Tuning 新4旋钮)· accessibility(mono升/L-3隔墙/误置恢复行/听觉Test Plan行)· patient-ai AC-13-F1非色相子句 · settings-shell OQ-SS-3/7结案 | 剩余(批次3/排期):qa 15条含糊词AC的具体化残项 · 素材债/预载/spike(OQ-44-5) · 壳镜像注极简已并入OQ行 | 4 文件未提交,等用户指令;复审建议 /clear 后新会话(上下文已深) -->

<!-- ISOLATE-DIAG-2026-09-25: 2026-09-25 | 编辑器启动「编译出错」报告诊断结案(取 A = 记档不修) | 判决:编译绿 —— unity run batch 复现 Tundra build success · error CS 0 命中 · git status 无脚本改动 | 用户所报 = scripting_class_is_subclass_of was called with a NULL parameter × 21 行,紧贴 Unloading broken assembly LegacyInputAnalyzer.dll 之前 = 同一噪声两半:meta Editor:enabled=1 ⇒ 域重载当普通插件装载 net6.0 产物 ⇒ 类型解析 NULL ⇒ 21 警告 + broken assembly 卸载;双日志(Editor.log/Editor-prev.log)数量位置一致 | 无害性三证:① 非编译错误 ② DLL Editor-only 不进 player(W2 已实证)⇒「outside of the editor」前提不成立 ③ DY0001 门走 RoslynAnalyzer label 编译管线,不涉本装载路径 | 落地:adr-012 挂账注 :397 就地扩充(原文保留,2026-09-25 补记 21 行 warning 级半边,维持无动作)· 选项 B(全平台关试修)/ C(重编 netstandard2.0)否决留档 | 1 文件未提交,等用户指令 -->

<!-- ISOLATE-DIAG-B-FIXED: 2026-09-25 | 方案 B 实验全过 → 用户批 1+2+3+4a 落地 = 噪声闭合 | 实验三跑:① SetAllOff(Any=off/Editor=off)平台位落盘复查过 ② 探针 UnityEngine.Input.mousePosition → DY0001 拒编 3 处(label 消费不依赖平台位,W2 Editor=on 前提不再需要) ③ 删探针干净轮 = 警告 21+1→0 · broken assembly 0 · error CS 0 · Tundra build success | 四件落地:① RoslynAnalyzerLabel.cs ConfigurePluginImporter 二修(Editor=on→off + editorOnlyOk→platformOffOk + 文件头/摘要注改写) ② adr-012 挂账注改判(维持无动作→B 修复闭合) ③ 本块 ④ spike BProbeAnalyzerPlatform.cs 删(职责归位 RoslynAnalyzerLabel) | .meta enabled 1→0 = 修复本体已在工作树 | 残留:探针 .cs+.meta+目录已全清;下轮可顺手跑 SetLabel 幂等入口验其不再翻回 Editor=on | 4 文件未提交(.gitignore / adr-012 / .meta / RoslynAnalyzerLabel.cs),等用户指令 -->

<!-- ISOLATE-REGRESSION-TEST: 2026-09-25 | B 修复的常驻回归测试补落 | 新测 test_legacy_input_gate_analyzer_platform_all_off(action_asset_identity_test.cs)—— 双断言:Any=off(W2 player 炸点)+ Editor=off(B 噪声改判),翻回任一位即红;修复入口指注 RoslynAnalyzerLabel.SetLabel | 动因 = 避免流程纪律(枚举配置位消费者)失效时无人兜底:platformOffOk 只在有人跑 SetLabel 时检查,测试才是常驻 | 验证:filter 单测 1/1 Passed · 全量 EditMode 511/511 Passed(unity/Logs/btest-full-editmode.xml) | 待提交 5 文件:.gitignore(本地项跳过)· adr-012 · LegacyInputAnalyzer.dll.meta · RoslynAnalyzerLabel.cs · action_asset_identity_test.cs,等用户指令 -->

<!-- DEV-STORY-001-DONE: 2026-09-26 | audio story-001 装配边界与 DTO 护栏实现完成(待 code-review/story-done) | 交付:AssemblyGates b5 三段门(IL=Cecil双层:元数据+源文本 · baseline手写{Sim}=工程债WARN · 黑名单Sim.Codec/DOTS无条件红 · GUID形asmdef解析 · 只认Library/ScriptAssemblies+编译失败即拒)+ PresentationDtoGuard 首建(visited终止/跳过表展开防白名单叶子假绿)+ Tests/EditMode/Audio/assembly_boundary_test.cs(22测三类负例)+ 账本README + manifest钉mono-cecil 1.11.6(lock已刷为直接依赖) | 顾问(unity-specialist只读)纠偏两处硬错:b4非metadata扫描(SRM编译不过→Cecil)/守卫只有规格;采纳基线手写/产物路径/前缀钉DaYiJingCheng.Gameplay.Presentation.Audio/B3B4收窄 | 编排修两个真bug:ScriptAssemblyPath少Library段(门+测试两处,测试改单一出处调门helper)· 顾问另flag:Cecil Immediate→Deferred(实现者修) | 验证:过滤22/22 · 全量EditMode 557/557 · .meta三件已生成 | 偏离:SRM字样与现实不符已注门头(故事QA的using⇒红由源文本层兑现)· B4 Vector3会红留实现期扩白名单 | 待:code-review → story-done;10+文件未提交等用户指令 | 编辑器曾占锁(PID 2380719用户关),stale lockfile已清 -->

<!-- S001-REVIEW-FIXED: 2026-09-26 | 代码审查 CHANGES REQUIRED → 7 条 Required 全修 → 568/568 复验 | 修:①前缀逃逸 BLOCKING(CheckAudioEscapeText 逃逸子段 + CheckAudioSourceFiles 接线 + 头注「②③兜底为假」订正 + 逃逸负例测;顺手修 \b 在非逐字串=退格符的正则 bug)②IL 三面(CallSite/ExceptionHandlers.CatchType/IModifierType + 忽略面登记;Cecil 此版 CallSite 无泛型面 CS1061 实测→登记忽略)③DOTS 黑名单 +Collections/Physics/Transforms + ADR-017 窄注 ④守卫词面 {diseas,diagnos,symptom} + diagnosisId/symptom 负例 ⑤ADR-025 §① Editor.Tools 族行补 Gates/Spike/Bake ⑥故事回刷(SRM→Cecil 双层 · QA 负例形态/反射分层/B3B4 执行面/DiseaseState 澄清 · Test Evidence [x] 24 测)⑦域重载降噪(只跑 b3/b2 —— 用户贴的 b5-W 刷屏即此)+ fullGate WARN 断言 + 拒扫豁免注 | 验证:过滤 24/24 + 全量 EditMode 568/568 | REC 榜单(排期):长方法拆 · 公有 doc · 深度护栏统一 · ReaderParameters 工厂 · 基线按装配×文件 · nameof 专测 · IL 引擎不误杀正例 | 未提交,等用户指令 → 下一步 /story-done
## Session Extract — /story-done 2026-09-26
- Verdict: COMPLETE WITH NOTES
- Story: production/epics/audio-system/story-001-assembly-boundary-dto.md — 装配边界与 DTO 护栏
- Tech debt logged: None(REC 榜单 7 项已在 Completion Notes + 审查报告双登记,未立 tech-debt 文件)
- Next recommended: production/epics/audio-system/story-002-event-table-schema-gate.md(依赖 001 已 Complete)
- 未提交文件:7 改 + 4 新(含 .meta)+ manifest/lock + EPIC + 故事 —— 等用户提交指令

<!-- S002-REVIEW-FIX: 2026-09-26 | audio story-002 审查 6 修批的测试侧续跑完成(上一会话断在半路) | 起因:门 10:03 落了 unity-specialist CHANGES REQUIRED 的 1 BLOCKING + 4 REC(TierParams 扁平→嵌套 等),测试侧批次 10:33 排好后遭 login 断 + 两次 /compact 失败 + 三轮 python heredoc 原子回滚/截断/锚点 ═ 计数错 → 全部零写入,会话 10:51 死 → 门与测试类型签名分叉,本次启动编译报 CS0266 | 本轮修:①ParseRow tier_params per-tier 嵌套解析 ②夹具 invalid_tiermap_filter_key.json 改嵌套形状 + 断言钉 TierParams["1"] + 新增「含滤波列」分支断言 ③ReadMembers 截断哨兵(**原脚本条件 bug**:i>=inner.Length 对正常解析恒真会全表误注入 __truncated__,改成只在显式结构异常 break 置 truncated)④测试 doc 措辞 ⑤七新测 + 新夹具 invalid_tier_params_flat_shape.json(扁平/嵌套两分支各一测)⑥两处按实测改判:原稿「原地改名」连带触发规则 7 必需 cue 缺失致 3≠2 假红,改**追加改名副本** / **追加同名副本**才能单因归因 ⑦AnyError helper(不依赖门的错误次序) | 文档面:GDD 幽灵引据结案 —— 用户裁定**补 §校验规则 第 9 条**(同名 cue 唯一;门 :682 节头 + :703 错误文本都指着不存在的「GDD 校验规则 9」)+ 全库刷 规则 1–8→1–9 / :531-545→548 / :474-548 共 12 处(门 6 · 测试 4 · story 1 · README 1)· story Status Ready→In Progress · QA 补测条 · Test Evidence [x] · README 账本(夹具表 + AC→测映射审查补测行)· 删残留 scratch_s002_fix.py | 验证:过滤 51/51(44+7)· 全量 EditMode **636/636**(unity/Logs/s002-b3.xml · s002-b3-full.xml) | 未提交,等用户指令 → 下一步 /code-review → /story-done -->

<!-- S002-REVIEW-R2: 2026-09-26 | story-002 第二轮 /code-review → CHANGES REQUIRED(0 BLOCKING · 5 REC)→ 5 REC 全修 | 双专审并行:unity-specialist(代码/ADR)+ qa-tester(可测性;首轮 10 轮上限中断,SendMessage 续跑出报告) | 评审结论:ADR-014 §三 + ADR-018 §六三集合常量**逐字一致** · ADR-025 装配 ✓ · 首轮 5 修复全闭合 · 单因归因与夹具逐字核过 · 唯一收口无旁路 | 5 REC 落地:①截断哨兵窄漏报(Balanced/ReadQuoted/ReadValue 加 out truncated,嵌套值未闭合也上报;原只在显式 break 置位)②**全案无规则拒收缺 `cue` 的行** → 门新增 `ValidateRowCuePresence`(归规则 3 NOT-RUN;三字段齐但无 cue 的行此前过完 13 段)+ GDD 规则 3 文本补 `cue`(不新增条号,免二次刷 12 处计数)③零覆盖八支补测 ④`errors[0]/[1]` 顺序耦合改 `AnyError` ⑤扁平夹具内层标量不再经 `ReadMembers`(原被当结构截断注入 __truncated__,夹具红由解析器哨兵引起 = 耦合隐患;改回标量自键占位) | **过程中更正一处我自己的误判**:原以为「全部 13 段对 null 行都 continue ⇒ 零错误过门」,实测规则 1/2/AC-44-D9 用 `row == null || 缺字段` 合并条件会各报一条(措辞对 null 行是「缺 whitelist_category」)→ 门注释改写为准确表述,测试**不锁错误总数**只钉身份级那条 | 新增 11 测(行身份 3 + 零覆盖 8) | 验证:过滤 62/62 · 全量 EditMode **647/647**(s002-r2.xml · s002-r2-full.xml) | 账本回刷:README AC→测两新行 · story Test Evidence 62/647 · QA 复审补测条 · GDD 规则 3 | NICE 榜单(排期未做):category 侧大小写 · `Fmt` 字面量断言 · 测试命名缺 expected 段 · 魔法串 "ContinuousPhysiology" · 同因双报去重 · `renamed.PresentKeys` 交叉校验 | 未提交,等用户指令 → 下一步复审 → /story-done -->

## Session Extract — /story-done 2026-09-26
- Verdict: COMPLETE WITH NOTES
- Story: production/epics/audio-system/story-002-event-table-schema-gate.md — 音频事件表 schema 与白名单门(BLOCKING)
- Tech debt logged: 未立文件 —— 3 条 ADVISORY 写入 Completion Notes(账本/真身路径歧义 · GDD 规则 3 补 cue · 复审两条非阻塞残留)+ NICE 榜单 6 项,一并留待排期
- Next recommended: production/epics/audio-system/story-003-mixer-topology-snapshots.md(001/002 均 Complete,002 的 Unlocks 含 003)
- 未提交文件:门 + 测试 + InspirePhaseMapping + 种子表 + 9→10 夹具 + 账本 + EPIC story-002 + GDD 规则 3 —— 等用户提交指令

## Session Extract — /dev-story 2026-09-26
- Story: production/epics/audio-system/story-003-mixer-topology-snapshots.md — 混音拓扑与快照纪律
- Files changed: NEW unity/Assets/Editor.Tools.Gates/{MixerTopologyGates,MixerAssetGenerator}.cs · NEW unity/Assets/Gameplay.Presentation/Audio/{MixerRegistry,SnapshotDirector,PlayerBusVolumeInitializer,ReverbPresetSwitcher,ReverbPreset}.cs · NEW unity/Assets/Tests/EditMode/Audio/mixer_topology_test.cs · NEW unity/Assets/Audio/DaYiJingCheng.mixer(生成)· NEW tests/integration/audio_system/fixtures/{valid_mixer_topology,invalid_missing_bus,invalid_unregistered_send,invalid_snapshot_captures_player_volume,invalid_single_level_bus}.yaml · MOD Editor.Tools.Gates.asmdef(+Gameplay.Presentation 引用)· MOD tests/integration/audio_system/README.md(Story 003 段)· MOD story 文件(readiness 两裁定回填 + In Progress)
- Test written: unity/Assets/Tests/EditMode/Audio/mixer_topology_test.cs — 21 test_*;**过滤 21/21 · 全量 EditMode 716/716**(unity/Logs/s003-b6.xml · s003-full.xml)
- Blockers: 无(四条 AC 全实现 + 全测)
- Key decisions (2026-09-26): ① `.mixer` 由本 story 创建(readiness 裁定,全项目原零命中)② 注册表载体 = C# 闭枚举常量,不解析 GDD markdown(对账断言守一致)③ `.mixer` 走 Editor 脚本 batch 生成(6000.3 无公开创作 API;内部 API `CreateMixerControllerAtPath` 实测可用)④ 快照创建走 **YAML 文本合成**(`CloneNewSnapshotFromTarget` batch 下返回空 —— 需编辑器窗口态,反射此路已证不通)⑤ send 走 `CopyEffect + set_sendTarget + InsertEffect`(**实例方法在 `AudioMixerGroupController`,非 Controller**)⑥ IL 扫描用 **Cecil**(SRM 本工程编译不过,承 AssemblyGates 实测)
- 我亲改的 6 处(实现者报告外):`Mono.Cecil.Cil` using · 测试 `.Name`→`.name` · `repoRoot` 4→5 层(拖红 9 条)· 5 处 `Has.Count`→`.Count`(数组无公开 Count)· 空装配 `Gameplay.UI`(0 个 .cs)跳过 · GDD 花括号展开 `bus_volume_{...}` 对账 · `AmpFileId` 漏负号(孤儿 effect 裁不掉)· 新增 `RepairDanglingSnapshotSlots`(悬空槽位无条件修复)
- Next: /code-review(双评审并行中)→ /story-done

<!-- S003-REVIEW-FIX: 2026-09-26 | story-003 双评审(unity-specialist 代码面 + qa-tester 可测性)→ CHANGES REQUIRED(3 BLOCKING · 两评审独立命中 2 条)→ 用户裁定「全部现在修」→ 三件全落地 | **B1 快照捕获空转**:真资产 5 快照 m_FloatValues 全 {} ⇒ 门 AC② 交集恒 ∅ 空转,且 F7=甲「StethoscopeFocus 只压 Ambience/Music」资产半边缺失;生成器 ApplySnapshotCaptures 死代码(m_ValueMap 口径已废)→ 重写为 **ApplySnapshotCapturesNative**(内部 API `AudioMixerGroupController.SetValueForVolume(mixer, snapshot, dB)` —— Unity 自己序列化,零格式猜测;捕获计划闭集:Stethoscope 只 duck ambience/music · DialogueFocus +sfx 不碰 voice · Paused 全 7 · Default/VRComfort 刻意留空;CaptureSeedDb=-6 常量注「种子值,用户调」;写后 VerifyCapturesWritten 硬断言) | **关键实测:m_FloatValues 键 = 组 m_Volume 参数哈希**(32 hex,既非 fileID 也非组名)⇒ 门原把它们当字面量名与 exposed 求交永不可能命中 = QA 预言的假绿成真 → 门加 `MixerDoc.VolumeHash` 解析 + 经 m_Volume 反查组名再求交 + 新增公开 `CountResolvedCaptureKeys` 作**非空转守卫**,真资产测试断言 >0 | **B2 ApplyDefaults 零调用者** → `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 接线,路③登记依赖 010(Addressables 装载面),`MixerResolver` Func 缝注入,FactoryDefaults 7×0dB 独立常量注「种子值,Story 011 接管」;**B3 生成器零测试** → 新建 mixer_asset_generator_test.cs 7 测(野快照裁剪/悬空槽位重指/**负 fileID 孤儿**/GUID 规整幂等/reverb 前缀不误删) | 顺带:PruneForeignSnapshots 允许集补 reverb_preset_ 前缀(防将来补建快照被当野员删)· YAML 门 LogWarning→硬 throw · 我修 VerifyCapturesWritten 只认 `{}` 内联(块式被误判为空)· 修新测试 ExtractGuids 只匹配裸 `guid:`(真形态是 `- guid:`)导致恒 0 条 | 验证:过滤 Mixer 28/28 · **全量 EditMode 723/723**(s003-b8.xml · s003-full2.xml) | 残余 REC 未修:DialogueFocus 字段归属负向零测 · 反射字符串面无真树扫描 · 白名单只断言 Count>=6 · 7 处措辞脆弱断言 · 死代码链(TryAddSend/CreateAndNameGroup/CreateEffectInstance) | 未提交,等用户指令 -->

## Session Extract — /story-done 2026-09-26
- Verdict: COMPLETE WITH NOTES
- Story: production/epics/audio-system/story-003-mixer-topology-snapshots.md — 混音拓扑与快照纪律
- Tech debt logged: 未立文件 —— 5 条 ADVISORY(.mixer 本 story 创建 · asmdef 增引用 · 快照 YAML 合成 · ApplyDefaults 依赖 010 · 生成器无端到端测)+ 残余 REC 6 项 + NICE 4 项,全写入 Completion Notes
- Next recommended: production/epics/audio-system/story-004-breath-layers-precision-tiers.md(001/002/003 全 Complete;003 的 Unlocks 含 004)
- 未提交文件:门×2 + 生成器 + 5 表现层 + 测试×2 + .mixer + 5 夹具 + asmdef + 账本 + story —— 等用户提交指令

## Session Extract — /dev-story 2026-09-26
- Story: production/epics/audio-system/story-004-breath-layers-precision-tiers.md — 听诊呼吸两层与精度档
- Files changed: NEW unity/Assets/Gameplay.Presentation/Audio/{AudioTuning,FilterRamp,BreathPhaseGate,BreathLayerDriver,TierFilterDriver,CoughChannel}.cs · MOD Audio/MixerRegistry.cs(+TierFilterParameters 闭集)· MOD Editor.Tools.Gates/{AudioEventTableGates,MixerTopologyGates}.cs · MOD tests/EditMode/Audio/{breath_layers_test(新,26测),assembly_boundary_test(+B3允许面)}.cs · MOD docs/engine-reference/unity/modules/audio.md(+Per-Source Filters & Ramp Evidence)· MOD tests/unit/audio_system/README.md(Story 004 段)· NEW production/qa/evidence/breath-layers-listen-evidence.md(骨架)· MOD design/gdd/audio-system.md(AC-44-01 ① 换载体)· MOD story 文件
- Test written: unity/Assets/Tests/EditMode/Audio/breath_layers_test.cs — 26 test_*,零 Assert.Ignore;**过滤 26/26 · 全量 EditMode 778/778**(unity/Logs/s004-b2.xml · s004-full2.xml)
- Blockers: 无
- Key decisions (2026-09-26):
  ① **readiness 修 GDD**:AC-44-01 ① 原「增益字段>0(读 tier_map)」断言对象全链路不存在(F-44.1 已废 gain 列、gain_scale 不在事件表 schema)→ 用户裁定**换载体**(六列齐+附加层行 loop:true),GDD/story/QA 三处回刷
  ② **附加层形态 = loop + 逐周期相位门控**(非 one-shot,因 002 规则 8 强制 loop:true);**去掉 PlayScheduled**(unity-specialist 判 Knowledge Gap、重启 loop 不可证伪)
  ③ **门控音量必须 ramp**(裸 0/1 必 click;GDD:312/562)—— 底座归 006,消费与断言归 004
  ④ **不新立 sink 接口**,复用 ADR-018 `IAudioCueSink`(新接口=契约漂移,且牵动 assembly_boundary_test 四方法契约面)
  ⑤ **B3 允许面扩容 = 用户裁定 A 案**:`EntryInterfaceAllowlist`(+IBreathLayerTransport/IMixerParameterSink)+ `EntryAllowlist`(+FilterRamp.State);理由注释:44 自用注入 seam、不持游戏状态、不对外提供,BadInterfaceFixture 负例不受影响
- 我亲改的:GDD AC-44-01 ① 换载体 + story AC/QA/Test Evidence 三处回刷 + B3 允许面三处(补 using / EntryInterfaceAllowlist / ① 检查 consult 它)
- 未解决跨 story 待办:**`TierFilterParameters` 五名尚未进 `.mixer`**(003 生成器目前只暴露 7 个 bus_volume_*)—— 需单独裁定是否回改 003 生成器;实现者未越界,已登记
- Next: /code-review(双评审并行中)→ /story-done

<!-- S004-REVIEW-FIX: 2026-09-26 | story-004 双评审(unity-specialist 代码面 + qa-tester)→ 代码面 0 BLOCKING / QA 3 BLOCKING · 4 REC → 全修 | QA 第 4 条([L] 骨架预填通过结论)**我独立核否**:breath-layers-listen-evidence.md 实测 38 个 `[ ]` / 0 个 `[x]` / 四处「听测未执行·签署前不得记为已通过」声明(:3/:4/:26/:90) | **B1(两评审独立命中,最重)**:AC-44-01 ② 脚本半从未扫真实源码 —— ValidateNoMuteCallSites 调用点只有测试内联字符串,真源写出 SetFloat(…,-90)/.mute=true 也 788 全绿 → 新测 `test_muteCallSites_realRuntimeTrees_zeroViolations` 扫**五棵运行期装配树**(Gameplay.Presentation/Sim/Sim.Contracts/Gameplay.Input/Gameplay.UI),0 文件 = 空转红;**排除 Editor.Tools.***(不进构建 + 门自身错误串含判据字面量,扫自己恒红)与 Tests(内联负例即判据本体);**定性 = 真源零违规**(唯一 SetFloat 负数字面量命中是注释里的日期 2026-09,`//` 行跳过;`.mute` 运行期树 grep 零命中) | **判据歧义已交回并写进测试 doc**:源码硬编码 ≤-80 = 拉到底硬静音 = AC 要禁;合法 ≤-80 字面量写入若未来出现,**须先改判据再落码** | B2 `.mute=true` 负例 · B3 捕获 ≤-90 红 + **-6 不红**(证明 -80 阈值未误伤 duck)· R1 `ValidateNoTierMuteFlags` 并入 `ValidateMixerTopology`(doc 注记夹具面核验:五份 YAML 夹具捕获 -60…-6 > -80、无 m_Mute:1 ⇒ Is.Empty 面与 Count 断言面**不位移**,788 全绿实证)· R2 FilterRamp 四条 LogError/dt=0 负例 · R3 BreathPhaseGate 非法分母两路 + inspireFraction∉(0,1) · R4 幽灵引据「MUST DO 1/2」→ 真实小节名 | 验证:过滤 36/36 · **全量 EditMode 788/788**(s004-f1.xml · s004-f2.xml) | 残余 NICE 未修:ShouldFire 写而不读 · WindowCycleFraction 钳1非F · EndedHandles 下标依赖保序 · 空表 new string[0] 无直测 | 未提交,等用户指令 -->

<!-- S004-EXPOSED-DEFECT: 2026-09-26 | **Story 003 回溯缺陷:exposed 通路不通(已定性,按裁定停手)** | 起因:为兑现 tier 五名 exposure 写探针 test_busVolumeExposedParam_actuallyAcceptsSetFloat | 三轮探测:① 我先猜 AudioMixer.exposedParameters —— **错,非公开**(Discover 里是内部 AudioMixerController::get_exposedParameters)② SerializedObject.FindProperty("m_ExposedParameters") —— guid 是 **Generic/ GUID 结构非 string**,stringValue 打 7 条 "type is not a supported string value",boxedValue NRE、无子属性 ⇒ **读法未决** ③ **反射读内部面(依据 Discover 实测,非猜测)** ⇒ **决定性**:GetType()=UnityEditor.Audio.AudioMixerController · 读到 7 条 · **field guid(GUID)=765acee8… 与 YAML 写入逐条一致** · 但 **SetFloat 仍 false** ⇒ **出口1(=原候选4):公开面无法判定**「配置缺一环」vs「EditMode 无活跃 DSP 图」;对照判别不可行(.mixer 无 Unity 自产 exposed 参数) | **过程中的自我纠错记录**:guid 语义我最初误判为「任意标识符」,Story 003 收口前我修「GUID 全零」时写的 b500…条序合成值 **正是本缺陷的源头**;后改对齐组 m_Volume 哈希(现在是对的),但通路仍不通 ⇒ guid 不是唯一一环 | **已做**:`NormalizeExposedGuids` 重写为对齐同名组 m_Volume 哈希(合成条序路径退役、查不到组 = 硬失败不写假值)· 探针保留三证断言 | **已完成**:探针转**条件** `if (!allSetTrue) Assert.Ignore(...)`

<!-- S004-HB-CONFIRMED: 2026-09-27 | exposed 通路根因**已定性(H-B)**,探针改判据,全量 822/822 Passed | 定性链(实验 1–7,每步都被下一步证伪):① 合成 GUID → 已修成组 m_Volume 哈希(仍 false)② Unity 没保留条目(反射读到 7 条且一致)③ AddExposedParameter 补救(cache 实测仍 0,与 IL 矛盾)④ ResolveExposedParameterPath 注入 cache(0→1 但 SetFloat 仍 false)⑤ **二分对照 SetFloat("___this_param_does_not_exist___") = false** ⇒ SetFloat 对**任何名字**都 false(EditMode),GetFloat 对同一真实名字 true ⇒ **H-B:EditMode 无活跃 DSP 图,按名查表 native 路径本身不通,与 exposed 配置无关** | 探针判据改为**配置正确性(反射读回 7 条 guid 与 YAML 逐条一致)+ 读路径可用(GetFloat true)**,测试名 test_busVolumeExposedConfig_matchesGroupVolumeHash_andGetFloatReadable | **过程中发现的生成器两洞已修**:① 删 .mixer 后无法重建 —— CreateMixerAsset 只查 Static,但 CreateDefaultAsset 是实例方法 ⇒ 永远匹配不到;PruneForeignSnapshots 把基底 "Snapshot" 也裁了 ⇒ 无改名基底 throw(修:搜索范围加 Instance + 允许集加 "Snapshot")② 删后重建七 GUID 又回全零(修:基底前置 + 全零也要覆盖不许跳过) | **MyExposedParam 判别实验**:删 .mixer 重跑后**没再现** ⇒ 是外部注入(非生成器所写),已消除;它曾污染 003 的 test_realMixerAsset_* | 验证:**独立复核 7 条 GUID == 同名组 m_Volume 逐条一致 · MyExposedParam 0 · 快照 5 员齐 · 全量 EditMode 822/822 Passed 0 红 0 跳过**(s004-final3.xml) | 未提交,等用户指令(实现者的刻意偏离、**我判定正确并接受** —— 我原话「修好即自动转红」在无条件 Ignore 下逻辑不可能成立,因其排在断言前;条件版两语义兼得:缺陷在场=Skipped,修好=自动恢复验收)+ **三处登记齐**(MixerRegistry.BusVolumeParameters 🚨 段 · 账本新行与 tier 缺口行并列 + 顺手修两处因出口1 过时的旧文案 · 本 active.md)· story-004 AC/Test Evidence **零改动**(四条 AC 不依赖该通路) | **影响面**:ApplyDefaults 7×SetFloat 落空(003 的 BLOCKING 2 实际未生效)· TierFilterDriver 档位滤波静默不生效 · **阻断 AC-44-02 将来听测** | 004 四条 AC 不依赖该通路 ⇒ 不阻塞 004 收口 -->
<!-- S004-SKIP-VERIFY: 2026-09-26 | 探针 Skip 形态验证通过 · 过滤 37 = 36 过 + 1 Skipped(带 KNOWN DEFECT 理由)+ 0 失败 · **全量 EditMode 822 = 821 过 + 1 Skipped + 0 失败**(unity/Logs/s004-skip.xml · s004-final.xml;822 含远程 Story 007 的 +34) | remote AssemblyGates.cs 改动未破 001 边界测试 -->

## Session Extract — /story-done 2026-09-26
- Verdict: COMPLETE WITH NOTES
- Story: production/epics/audio-system/story-004-breath-layers-precision-tiers.md — 听诊呼吸两层与精度档
- Criteria: 3/4 passing · 1 DEFERRED = AC-44-02 [L] 听测未执行(骨架已建,38 `[ ]`/0 `[x]`);0 条 UNTESTED 属可自动化面
- Tech debt logged: 未立文件 —— 5 条 ADVISORY(AC-44-01① 换载体 · B3 允许面扩容 · engine-reference 补录 · 跨 story 改 001 边界测试 · **🚨 Story 003 exposed 通路不通**)+ 并列缺口 **tier 五名无 filter effect 可挂** + NICE 5 项,全写入 Completion Notes 并分处登记
- Tests: 37(36 过 + 1 Skipped=KNOWN DEFECT 守卫)· 全量 EditMode **822 = 821 过 + 1 Skipped + 0 失败**
- Next recommended: production/epics/audio-system/story-005-snr-analysis-noise-floor.md(001-004 全 Complete;004 的 Unlocks 含 005)
- 未提交文件:12 改 + 12 新(含 .meta)+ GDD + engine-reference + 账本 + story —— 等用户提交指令

## Session Extract — /story-done 2026-09-27
- Verdict: COMPLETE WITH NOTES
- Story: production/epics/audio-system/story-004-breath-layers-precision-tiers.md — 听诊呼吸两层与精度档
- Criteria: 3/4 passing · 1 DEFERRED = AC-44-02 [L] 听测未执行(骨架已建,38 `[ ]`/0 `[x]`);0 条 UNTESTED 属可自动化面
- Tech debt logged: 未立文件 —— 5 条 ADVISORY(AC-44-01① 换载体 · B3 允许面扩容 · engine-reference 补录 · 跨 story 改 001 边界测试 · **exposed 通路 H-B 定性**)+ 并列缺口 **tier 五名无 filter effect 可挂** + NICE 5 项,全写入 Completion Notes 并分处登记
- Tests: 37 全过 · 全量 EditMode **822/822 Passed 0 红 0 跳过**
- Next recommended: production/epics/audio-system/story-005-snr-analysis-noise-floor.md(001-004 全 Complete;004 的 Unlocks 含 005)
- 未提交文件:12 改 + 12 新(含 .meta)+ GDD + engine-reference + 账本 + story + .mixer —— 等用户提交指令

## Session Extract — /story-readiness (Story 014) 2026-09-27
- Story: production/epics/audio-system/story-014-tier-filter-carrier-exposed-params.md — tier 滤波载体与 mixer 暴露参数(**2026-09-27 新立**,非 /create-stories 生成)
- 缘起: Story 005 readiness 发现 AC-44-05 ② 断言对象不存在 → 用户裁定**立独立 story 解决载体**
- 缺口定性:「谁承载 tier 滤波」= 全案未认领的资产拓扑裁定 —— GDD F-44.1 说 tier 驱动通带/噪声底 · Story 004 Impl Notes 说「本 story 消费列做滤波/噪声底驱动」· **无任何 story 认领「往 mixer 加 filter effect」**;`.mixer` 25 个 effect 全 `Attenuation`、零 filter,`noise_floor_db`/`contact_noise_floor_db` 无组参数承载
- 连带: Story 004 的 `TierFilterParameters` 五名同样无真实参数可挂(当时登记为「不做」)
- 关键裁定: 载体形态(组级 filter effect vs 每参数独立组)实现期二选一并写注;**禁用逐源 `AudioLowPassFilter`**(unity-specialist 判「设备级 ⇒ 组级」);暴露名 == 组名(两级组结构约定)
- 已知风险已登记: ① 五名各需同名组,须确认不与 22 组重名 ② `AudioMixerController` 内部 API 无公开文档,须 Discover ③ **AC-44-T3 的 SetFloat 断言在 EditMode 不可判(H-B)**,应改 GetFloat 可读或在播放态验证
- Dependencies: Depends on 003 · 004 / Unlocks 005 · 013 / Blocked by None → **014 落在 013 关键路径上**
- 未提交文件:story-014 + EPIC.md(014 行 + 故事数 14)· story-005 回刷(AC② 前置引用 / QA / Test Evidence 真身 / Dependencies Blocked by 014)—— 等用户提交指令
- Next: /dev-story production/epics/audio-system/story-014-tier-filter-carrier-exposed-params.md

## Session Extract — /story-done 2026-09-27
- Verdict: COMPLETE WITH NOTES
- Story: production/epics/audio-system/story-014-tier-filter-carrier-exposed-params.md — tier 滤波载体与 mixer 暴露参数
- Criteria: 5/5 passing · 0 deferred · 0 UNTESTED
- Tech debt logged: 未立文件 —— 4 条 ADVISORY(本 story 新立缘起 · 载体形态选乙 · EPIC 故事数 13→14 · 顺带修生成器两洞)+ NICE 3 项,全写入 Completion Notes
- Tests: tier_filter_carrier_test 11 全过 · 全量 EditMode **872/872 Passed 0 红 0 跳过**
- 顺序纠错记录:首轮我把「评审修复」排在「主体提交」之前(e43eb73),致主体在修复之后才入库 → revert(e43eb73) + 主体重提(de93819,含已修测试文件);`git show de93819:tier_filter_carrier_test.cs` 与工作树 diff=0 行 ⇒ B1 修复从未遗漏,无需补提交
- Next recommended: production/epics/audio-system/story-005-snr-analysis-noise-floor.md(014 已 Complete,解除 005 的 Blocked by)
- 工作树:仅 .gitignore(本地项) + test-results.xml(临时产物)—— 均按纪律不入批

## Session Extract — /story-done 2026-09-27
- Verdict: COMPLETE WITH NOTES
- Story: production/epics/audio-system/story-006-voice-variants-intensity-bucket.md — 语声变体库与 Intensity 分桶
- Criteria: 4/4 passing · 0 deferred · 0 UNTESTED
- Tech debt logged: 未立文件 —— 4 条 ADVISORY(本 story 新立缘起 · 载体形态选乙 · EPIC 故事数 13→14 · 顺带修生成器两洞)+ NICE 3 项,全写入 Completion Notes
- Tests: voice_variants_test 21 全过 · 全量 EditMode **986/986 Passed 0 红 0 跳过**
- Next recommended: production/epics/audio-system/story-007-net-divergence-remote-derivation.md(006 已 Complete)
- 工作树:仅 .gitignore(本地项)—— 按纪律不入批
