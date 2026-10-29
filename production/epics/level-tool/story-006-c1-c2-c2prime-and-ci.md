# Story 006: C1 两层漂移 + C2 可走性同源 + C2' collider 足迹同源 + CI 接线

> **Epic**: 关卡工具(54 · Tooling 层)
> **Status**: **Complete ✅ 2026-10-29(评审轮已结)** —— AC 6/6;证伪 **23/23 红**;
> CI 端到端探针成立(`0` 干净 / `1` 注入 / `2` 链坏);回归 `5849 / 5812 / 0 / 37`。
> 评审 CHANGES REQUIRED 的 1 族 BLOCKING(`B-1`/`B-2`/`B-3`)**已修并复评结账**,
> 原件 = `production/qa/evidence/review-level-tool-story-006-2026-10-29.md` §八。
> ✅ **原「未结」项已结(2026-10-29 单开轮)**:`Check` **163 行 → 拆为 11 个方法,最长 35 行**。
> 纯重构;另补 1 条夹具钉住拆分引入的唯一结构风险。原件 =
> `production/qa/evidence/impl-level-tool-check-split-2026-10-29.md`。
> ✅ **同日后继批**:`Editor.Tools.Level/` **全树**另拆 **19 个** >40 行方法
> (9 个文件,含两个构造器),终态 **168 个方法 / 最长 40 行 / 超 40 行 = 零**。
> 原件 = `production/qa/evidence/impl-level-tool-method-split-2026-10-29.md`。
> 🔁 **留痕**:本 story 曾于评审前误标 `Complete`,评审判 CHANGES REQUIRED 后退回修复,现重新判定。
> **Layer**: Tooling
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(两层世界)· `design/gdd/modular-building.md` 规则八(义务 `O-23-9`)
· `design/gdd/player-controller-and-movement.md`(`AC-1-33②` `slopeLimit` / `stepOffset`)
**Requirement**: 兑现 S20-20 挂标 **#3「C1–C6 七项未实现」**中的 **C1 / C2 / C2'**,
并把整个检查链**接进 CI**

**ADR Governing Implementation**: **ADR-022 §四 C1 / C2 / C2'** · **ADR-015 §一之补**
· **ADR-016 §Validation**(「在关卡工具中存在」)
**ADR Decision Summary**: **C2 可走性同源 = 硬失败**(`|engineParam − Dequantize(bakedInt)| ≤ 1 量子`);
**C2' 模板 collider 足迹同源 = 硬失败**(collider 高度 > `stepOffset` 除非显式声明可走过);
**C1 两层漂移 = 告警**(漂移只影响表现,不退化为构建失败)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: **MEDIUM**
**Engine Notes**: C2 / C2' 须读**引擎侧**真值(`CharacterController` 的 `slopeLimit` /
`stepOffset`;模块 prefab 的 collider)。`CharacterController` 是长期稳定 API(LOW);
**模块 prefab 的 collider 读取**须实测(MEDIUM)。

**Control Manifest Rules (Tooling 层)**:
- Required: C2 / C2' = **硬失败**(`throw`);C1 = **告警**
- Required: 「在关卡工具中存在」是 ADR-015 / ADR-016 两处 Validation 的**执行点**
- Forbidden: **`Debug.Assert` 代替 `throw`**(C2 / C2' 是硬失败)
- Forbidden: **视觉层采样回逻辑判定**(ADR-015 §一:两层纪律)
- Forbidden: **C1 升格为构建失败**(漂移只影响表现,ADR-015 §Risks 同口径)

---

## §病灶 —— 「悬空判据」的确切含义

ADR-022 的立件缘由逐字写着:

> `ADR-015 §Validation:317` 与 `ADR-016 §Validation:442` 的
> 「两层 / 导航格一致性检查**在关卡工具中存在**」这句判据**在架构上悬空** ——
> **没有任何东西被指定为「关卡工具」**。

本 epic 立件后,「关卡工具有了」;但 **C1 / C2 / C2' 三条仍零实现**:

```
"C1 两层漂移(需视觉层数据 —— 本批不产出)",
"C2 可走性同源(需 world_terrain.json —— 本批不产出)",
"C2' 模板 collider 足迹同源(需模块 prefab —— 归 23 素材轮)",
```

⇒ 悬空判据**仍未落地**。S2 让 `world_terrain.json` 存在(C2 的逻辑侧有了),
本 story 才让**比对本身**存在。

**连带后果(实测)**:

| # | 后果 | 依据 |
|---|---|---|
| ① | `AC-1-33②` 无值可比 | 债册 `TD-player-01` 明写该项「归**关卡工具**,系统 1 只是执行者、**无权填值**」 |
| ② | 「两套可走性」风险未受检 | ADR-022 §四 C2' 的理由逐字:玩家不读整数格,**走感全在 collider** ⇒ 两套答案 |
| ③ | 两层漂移无人发现 | C1 是唯一的发现面,且它是**告警**级 —— 现在连告警都没有 |

---

## Acceptance Criteria

- [x] **AC-1(BLOCKING)** —— **C2 可走性同源 = 硬失败**:
      `|engineParam − Dequantize(bakedInt)| ≤ 1 量子`,对 `slopeLimit` **与** `stepOffset` **各一条**。
      **判据**:① 注入一处超 1 量子的偏差 ⇒ **抛**;② 注入恰 1 量子的偏差 ⇒ **不抛**(边界);
      ③ 注入超 1 量子 +1 单位 ⇒ **抛**。
      ⚠️ **三条缺一不可** —— 只测「超了会抛」时,一个「阈值写得很松」的实现也能通过。
      ⚠️ `slopeLimit` 与 `stepOffset` **各一条夹具** —— 两者是不同的量,不得合并。
- [x] **AC-2(BLOCKING)** —— **C2' 模板 collider 足迹同源 = 硬失败**:
      collider 高度 > `stepOffset` ⇒ **抛**,**除非**模块显式声明可走过(如地毯)。
      **判据**:① 高 collider 无声明 ⇒ 抛;② 高 collider **有**声明 ⇒ 不抛
      (证明「例外路径可达」,不是「恒抛」);③ 足迹与占用格不一致 ⇒ 抛。
- [x] **AC-3(BLOCKING)** —— **C1 两层漂移 = 告警**:
      逻辑占用格 ↔ 视觉地形不矛盾。
      **判据**:① 注入漂移 ⇒ **报出告警**;② **不**使构建失败。
      ⚠️ **必须验「报出」** —— 「永远不报」也满足 ②,那是告警类检查的经典假绿。
- [x] **AC-4(BLOCKING)** —— **CI 接线**:
      整个 C1–C6 检查链可在 **CI 里跑**(不需要打开编辑器 GUI)。
      **判据**:① 一条命令跑完 C1–C6;② 硬失败项以**非零退出码**体现;
      ③ 告警项以**零退出码 + 可读输出**体现。
      ⚠️ **判据须给 `file:line` + 一条能证明它会红的命令**(承
      `coding-standards.md` §「某件有门」的自述纪律)。
- [x] **AC-5(BLOCKING)** —— **`UnimplementedChecks` 长度 == 0**:
      C1–C6 七项全部实现。**判据**:数组为空 ⇒ 本 epic 的 DoD 达成。
- [x] **AC-6** —— **两条 Validation 判据落地**:
      `ADR-015 §Validation:317` 与 `ADR-016 §Validation:442` 的「在关卡工具中存在」
      可被指到**具体检查**(C1 / C3)。
      **判据**:两处 ADR 文本各加一条回指注记(指向本 story 的检查实现)。

---

## §Implementation Notes

### 顺序:C2 / C2' 先,C1 后

C2 / C2' 是**硬失败**,它们的判据是**精确的**(≤ 1 量子 / 高度比较);
C1 是**告警**,判据是**模糊的**(「不矛盾」)。
⇒ 先做判据精确的那两条 —— 它们的夹具容易写、判别力容易证。
C1 的「不矛盾」若一开始就写,很容易写成「永远不报」。

### 量化函数必须**已经存在**(S2 定义)

AC-1 的判据用 `Dequantize(bakedInt)`。该函数归 **S2** 定义(逻辑侧存整数)。
若 S2 未定义它,本 story **须先补** —— 但那是**越界**,应回 S2 补。
⚠️ **本 story 不得自行发明第二个量化函数** —— 否则两侧各有一个,「同源」就成了空话。

### C2' 需要模块 prefab —— 而 prefab 归 23 素材轮

`ModuleDefinition` 今日只有五字段(`ModuleId` / `SlotType` / `LocalOccupancy` /
`ValidOrientations` / `Refundable`),**没有 collider 信息**。
⇒ C2' 需要 23 侧提供 collider 足迹。
**两条路**:

| 路 | 做法 | 代价 |
|---|---|---|
| **甲** | 等 23 素材轮产出 prefab,再读 collider | 本 story 被阻塞 |
| **乙** | 本 story 定义「collider 足迹」的**数据形状**(整数),prefab 侧后补 | 可先行;但数据与 prefab 可能漂移 |

⚠️ **本 story 取乙** —— 定义形状 + 检查逻辑,**读 prefab 的那一半登记为未跑**。
若 23 素材轮已产出 prefab,则甲路可跑。**须在完成时如实说明走的是哪条**。

### CI 接线的判据不能只写「能跑」

`coding-standards.md` 的 BLOCKING 规则:「『某件有门』的自述须给 `file:line` +
一条能证明它**真的会红**的命令」。⇒ AC-4 的验收**必须**包含:
① 门在哪(`file:line`);② 一条命令;③ 该命令在**注入缺陷**时的输出。

---

## Out of Scope

- **`slopeLimit` / `stepOffset` 的具体数值** —— **用户数值轮**(本 story 只检「两侧一致」)
- **模块 prefab 本体** —— 归 23 素材轮(本 story 只检「足迹同源」)
- **视觉层地形资产本体** —— 美术 / 关卡设计
- **C3 / C4 / C5 / C6** —— 归 S3 / S5
- **`CharacterController` 的参数应用** —— 归系统 1(债册 `TD-player-01` 已修)
- **量化函数的定义** —— 归 S2(本 story 只用,不定义)

---

## QA Test Cases

- **AC-1**:C2 三条边界,**每个量各一套**。
  - Given: 逻辑侧整数 `slopeLimit` + 引擎侧 float。
  - When: 构造偏差 0 / 1 量子 / 1 量子 + ε。
  - Then: 不抛 / 不抛 / 抛。
  - Negative fixture: 把阈值从 `≤ 1 量子` 改成 `≤ 2 量子` ⇒ 第二条应仍绿、第三条仍红;
    改成 `≤ 0` ⇒ 第一条红。**三条合起来才钉住阈值**。
  - ⚠️ **`slopeLimit` 与 `stepOffset` 各一套** —— 停用其中一条的检查,另一条不受影响。

- **AC-2**:C2' 三条。
  - Given: 一个模块的 collider 高度 + `stepOffset` + 「可走过」声明。
  - When: 三种组合。
  - Then: 抛 / 不抛 / 抛(足迹不一致)。
  - Negative fixture: 若实现「恒抛」⇒ 第二条(有声明)红。

- **AC-3**:C1 告警。
  - Given: 一处逻辑 / 视觉漂移。
  - When: 跑检查。
  - Then: 报出告警,退出码 0。
  - Negative fixture: ① 停用检测 ⇒ 不报 ⇒ 红;② 改成抛 ⇒ 退出码非零 ⇒ 红。

- **AC-4**:CI 门会红。
  - Given: 注入一个 C2 缺陷。
  - When: 跑 CI 命令。
  - Then: 非零退出码 + 报文点名是哪一条。
  - ⚠️ **本条必须贴出实跑输出** —— 「命令存在」不等于「命令会红」。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `unity/Assets/Tests/EditMode/LevelTool/consistency_c1_c2_c2prime_test.cs` — must exist and pass
- **CI 门证据**:一条可跑命令 + 注入缺陷时的实跑输出(落证据件)
- ⚠️ **既有 `locomotion_chain_test.cs` 的 `test_ac133_*` 须保持全绿** ——
  本 story 给 `AC-1-33②` 提供**参数来源**,不得改它的判据

**✅ 2026-10-29 交付**:
- 夹具 `consistency_c1_c2_c2prime_test.cs` **20 条**,全绿(LevelTool 子集 `118 / 116 / 0 / 2`)。
- **CI 门**:`tools/level/run_ci.sh` → `LevelCiRunner.RunBatch`;**端到端实跑**干净 ⇒ 退出码 **0**、
  注入 `SlopeLimitDegX100 4500→3000` ⇒ 退出码 **1** 且报文点名 `C2 slopeLimit`;
  还原 **SHA-256 `d34be19b…8be90` 逐字节一致**。
- **证伪轮 15 条全红**(含一条首轮假存活 F13,修夹具后转红)。
- `test_ac133_*` **未改动**,随全量 EditMode 跑绿。
- 原件 = `production/qa/evidence/impl-level-tool-story-006-2026-10-29.md`。

**证伪反证(S12-4 模板)**:

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| 阈值边界(C2) | `\|engine−baked\| ≤ 1 量子` | F1 停用 slope 判据 · F2 停用 step 判据 · F3 阈值放宽到 2 · **各 1 条红** |
| 阈值边界(C2' 等高) | `高度 > stepOffset`(严格) | F4 改 `>=` ⇒ 等高夹具红 |
| 定义域收窄(C2' 例外) | 「有声明 ⇒ 不抛」 | F5 删例外分支 ⇒ 该条红 |
| 共存互斥(C1 双向) | 方向① / 方向② / 干净基线 | F8 停方向① · F9 停方向② · F10 恒报 ⇒ **各 1 条红** |
| 一次性/幂等(CI 三态) | 0 / 1 / 2 三个退出码 | F11 恒 0 · F12 合并 1 与 2 ⇒ 各 1 条红 |
| 量化口径(ADR-006) | 半量子 away-from-zero | F13 换 `Math.Round` ⇒ 红(**首轮假存活,修夹具后成立**) |

---

## Dependencies

- Depends on: **S2**(`world_terrain` 存在 + 量化函数已定义)· **S3**(生态区存在,C1 的邻域)
- Unlocks: **本 epic 的 DoD**(`UnimplementedChecks` 归零)· `AC-1-33②` 转可判
  · `world-ecozones/story-005` 的 EXTERNAL 前置解除

---

## §诚实边界(本 story 证明不了的事)

1. **C2' 的 prefab 侧若未产出** —— 本 story 只定义形状 + 检查逻辑;
   「prefab 的 collider 真的与占用格一致」**未跑**(须如实登记)。
2. **C1 的「不矛盾」是主观判据** —— 告警级,由人判读;本 story 只保证「漂移会被报出」。
3. **CI 门在真实 CI 环境(而非本机)的行为** —— 本 story 只证明本机命令会红。
4. **`slopeLimit` / `stepOffset` 的数值合理性** —— 用户数值轮。
5. **量化函数的正确性** —— 归 S2;本 story 只用不验。

---

## Completion Notes

**Completed**: 2026-10-29
**Criteria**: **6 / 6 passing**(AC-1 … AC-6 全勾;AC-5 的口径见下)
**Deviations**: 无 blocking;三处旧夹具随判据迁移改了形态(见下 §2)
**Test Evidence**: Integration — `unity/Assets/Tests/EditMode/LevelTool/consistency_c1_c2_c2prime_test.cs`(20 条)
+ CI 门实跑证据(原件 `production/qa/evidence/impl-level-tool-story-006-2026-10-29.md`)
**Code Review**: 未跑 `/code-review`(lean 模式;建议在 epic 收口时补一轮)

### 1. AC-5 的口径(**必须这么读**)

「`UnimplementedChecks` 长度 == 0」证明的是 **C1–C6 七项都有实现**,
**不证明**「七项在生产上今天都跑出了结论」。今日真跑的只有 **C2** 与 **C3**;
**C1 / C2' 在生产上必然进 `CheckReport.NotRun`**(第三态)—— 因为全库零视觉层资产、零模块 prefab。
这三态在**类型上可分**(`Violations` / `Warnings` / `NotRun`),CI 每次都打印,不静默跳过。

### 2. 三处旧夹具的形态变化(原文保留 + 加注,未删史)

| 夹具 | 原判据 | 现形态 | 理由 |
|---|---|---|---|
| `level_tool_consistency_test::test_levelTool_realSeed_…` | `Unimplemented.Count == 3` | `== 0` | 三条已实现 |
| `nav_chunk_slicing_test::test_ac6_…leavingThree` | 恰 3 条 | **`Assert.Ignore`** | **宾语消失** —— 不是跳过失败 |
| `level_source_generalization_test::test_ac4_…` | 清单非空守卫 | **`Assert.Ignore`** | 同上 |

⇒ 全量 EditMode 的 `skipped` 因此 35 → **37**。

### 3. 本轮自身的三处错误(如实登记)

1. `if (TryCheck(...)) AddRange(...)` —— **C2 恒绿**,被夹具当场抓到;
2. 证伪驱动器首轮 `-testFilter` **漏类名** ⇒ 15 条全部 `total=0`、整轮「全绿」实为**一条没跑**;
   已给驱动器补 `FILTER-MISS(未跑到)` 判据;
3. F13 夹具的 `0.0125f × 1000` **不在中点**(实为 `12.500000186`)⇒ 对舍入模式零判别力;
   改用 `0.5f × 1` 后转红。**修夹具,不改实现。**

### 4. 回归

- EditMode **`5842 / 5805 / 0 / 37`** @ 2026-10-29 07:04 CST · story-006 实现轮 · `5eba3a8e`+工作树
  (接线轮基线 `5822 / 5787 / 0 / 35` ⇒ total **+20** = 新增 `[Test]` 恰 20)
- PlayMode **`238 / 234 / 0 / 2 / 2`**(与基线逐项相同;进程退出码 2 由那 2 条 **Inconclusive** 引起,
  XML `failed = 0`,**与本轮无关**,如实登记)

### 5. 评审轮(2026-10-29 同日 · 评审后退回再修)

| 项 | 内容 |
|---|---|
| 判定 | `/code-review` 双专家 → **CHANGES REQUIRED**(1 族 BLOCKING:导出唯一门丢弃 `NotRun`/`Warnings`) |
| 修复 | `CheckOrThrow` 改为**落日志 + 回传报告**;`terrain==null` 改 fail-closed(抛);C3 补 `NotRun`;导出器 7 处接 `ProbeCurrent()`;探针加 domain 级缓存并去重;日志改每类一条聚合行 |
| 证伪 | 修复轮 **F16–F18** + 加固轮 **F19–F23**,连同首轮 F1–F15 ⇒ **23/23 红** |
| 复评 | Phase 7b 拆 A(核原判据)/ B(找新缺陷) 并行;B 的唯一 BLOCKING 被**实跑反证**驳回(它自陈「静态分析,未实跑」) |
| 回归 | EditMode `5849 / 5812 / 0 / 37`(首轮基线 `5842 / 5805 / 0 / 37` ⇒ +7 = 新增夹具) |
| **已结**(2026-10-29 单开轮) | `Check` **163 行 → 11 个方法,最长 35 行**。纯重构,零可观测行为变更(一处空表 vs 空数组的不可观测微差已就地登记);**另补 1 条夹具**钉住拆分引入的唯一结构风险(③ 命中后的早退),反证 **`10 / 9 / 1`** 唯一红即该夹具。原件 = `production/qa/evidence/impl-level-tool-check-split-2026-10-29.md` |
| 原件 | `production/qa/evidence/review-level-tool-story-006-2026-10-29.md`(§一 原判定 · §八 结账表) |
