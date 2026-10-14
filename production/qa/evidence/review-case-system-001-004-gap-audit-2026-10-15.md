# 核查件 —— case-system 001–004 评审原件缺口

**核查时点**:2026-10-15
**触发**:`production/qa/qa-plan-sprint-11-2026-10-14.md:153`(开工前置:既有债核查 A 节)
**性质**:**核查件**,非评审原件,亦非对账件 —— 它登记**缺口的存在与处置路径**;
四份补评原件另落(见 §四),二者不可互相替代(承 `.claude/docs/review-workflow.md`
「对账件不可替代原件」同源纪律)。

---

## 一、核查问题

`case-system` 的 story-001~004 于 **2026-10-06** 收口(commit `e64efad` / `c4e3708` /
`31e3f9b` / `8a693fd`),而 `production/qa/evidence/` 中该 epic 仅存 **005/006** 两份原件
(`review-case-system-story-005-2026-10-08.md` / `-006-2026-10-08.md`)。

⇒ 核查:**当年评审有无 BLOCKING 判定?**
- 有 ⇒ 触发原件义务第 1/2 条,缺原件的对象**不得转 Complete**,出路 = 补做评审(不可追补)
- 无 ⇒ 登记「无 BLOCKING、无需原件」结论

---

## 二、证据链

| # | 证据 | 来源 | 判定 |
|---|------|------|------|
| 1 | case-system 原件仅 005/006 两份 | `ls production/qa/evidence/` | **001–004 无原件(确证)** |
| 2 | 「双代理评审:unity-specialist PASS / QA **FAIL**(B1 A3 恒真、B2 A5 单向)」+ 修复描述 + 复跑绿 | commit `e64efad`(story-001) | **001 产出 BLOCKING 级判定**(「恒真」属 BLOCKING 类别) |
| 3 | 「修复双代理评审 **3 BLOCKING**:B1 测试原测自定义函数 / B2 AC-37-30 入流义务 / B3 AC-37-05 存新值」 | commit `c4e3708`(story-002) | **002 产出 BLOCKING 级判定**(自述明写 BLOCKING) |
| 4 | 「核心修复:IsClosed/IsOpen 匹配用的是 Patient 而非 Tick/Seq」—— 无评审措辞 | commit `31e3f9b`(story-003) | 003 判定分级**未能确证**(有修复痕迹,无判级留痕) |
| 5 | 无评审措辞 | commit `8a693fd`(story-004) | 004 判定分级**未能确证** |
| 6 | 同日 prescription-medication story-002 有独立「**补评审件**」commit | `7362ae9`(2026-10-06) | 当时**已存在补件惯例**;case-system 未执行 |
| 7 | 原件义务三条(落盘 / 闸门 / 不可追补),立于 2026-10-02 | `.claude/docs/review-workflow.md` | **义务适用于 10-06 收口的四件**(在其生效之后) |

**前身案例对照**(义务文档 §现存缺口):`modular-building` / `world-ecozones` /
`player-controller` 三者为义务的**前身案例**(2026-10-02 发现,已补对账件)。
case-system 001–004 是**同一失效模式**在义务生效后的又一次实例。

---

## 三、结论

1. **001 / 002**:确证产出 BLOCKING 级判定 + 无原件 ⇒ 触发义务第 1/2 条 ——
   两件此前转入 `Complete` 的判定**不成立**(无报告路径可引用,「借绿」形态)。
2. **003 / 004**:无原件;判定分级**无证据** —— 但亦**无证据表明「无 BLOCKING」**。
   该不可判定性本身即「不可追补」条款要防的形态。
3. **出路(义务第 3 条)**:缺原件的对象唯一出路 = **补做一次评审(评当下)并落新原件**。
   **历史判定是否成立永久不可复核** —— 须在对象内如实登记(本件即该登记)。

---

## 四、处置(用户裁定 2026-10-15:S11-2 后续批)

**裁 = 四件全补**(不拆 001/002 与 003/004):

| 对象 | 补评原件 | 轮次 |
|------|---------|------|
| story-001 | `review-case-system-story-001-retro-2026-10-15.md` | 双代理(unity-specialist 代码面 + qa-lead 测试面) |
| story-002 | `review-case-system-story-002-retro-2026-10-15.md` | 同上 |
| story-003 | `review-case-system-story-003-retro-2026-10-15.md` | 同上 |
| story-004 | `review-case-system-story-004-retro-2026-10-15.md` | 同上 |

**补评口径(强制)**:
- 评的是 **2026-10-15 当下的代码**,**不得追认历史判定**(义务第 3 条);
- commit 自述中的「修复内容」是**待验证主张**,不是结论 —— 补评独立判定;
- 文件名带 `-retro-` 以区分于正常轮次的 `review-<对象>-<date>` 形态,防误读为原判定留痕。

**已知边界**:补评发现的一切 BLOCKING,其修复归**新一轮**(不得声称「原判定如此」),
story 文件的状态登记须反映「原件为补评件」的事实。

---

## 五、连带登记

- **story-002 的历史自述含「3 BLOCKING」而原件缺失** —— 这是义务文档所述
  「后续只能靠 commit message 自述判断修复是否成立」的活例;补评将独立复核该三条修复。
- **本核查不改变 005/006 的状态**(二者原件齐备,合规)。
- **EPIC 状态不动**:`production/epics/case-system/EPIC.md` 的 Complete 维持,
  但在 §四 四份补评落盘后须回填原件路径引用(义务第 2 条:Complete 判定须引用报告路径)。

---

## 六、执行结果(2026-10-15 同日收口)

### 六·一 四份补评原件全部落盘 ✅

| 对象 | 原件路径 | 判定 |
|---|---|---|
| story-001 | `review-case-system-story-001-retro-2026-10-15.md` | 代码面 **0** · 测试面 **4**(§六 复核:无勘误,全保留) |
| story-002 | `review-case-system-story-002-retro-2026-10-15.md` | 代码面 4 · 测试面 10 → **§七 勘误后核心 1 类**(`LastJudgmentOf` 死参·实害链),另 4 条被推翻 |
| story-003 | `review-case-system-story-003-retro-2026-10-15.md` | 代码面 **1** · 测试面 5 → **§七 勘误后 4**(B4 被推翻),最重 = `IsClosed` 按 `Patient` 匹配 |
| story-004 | `review-case-system-story-004-retro-2026-10-15.md` | 代码面 **3** · 测试面 **5**(§七 复核:无勘误,全保留) |

轮次:双代理(unity-specialist 代码面 + qa-lead 测试面),8 份报告全回。

### 六·二 核查问题定案

§一 的核查问题(**当年评审有无 BLOCKING 判定?**)定案:

- **001 / 002**:证据链(§二 第 2/3 条)**确证**产出过 BLOCKING 级判定 ⇒ 补评**独立复核**发现两件的判定**至今成立且更广**(001 的「A3 恒真 / A5 单向」在当下代码中**已不成立** —— 已修的确实修了;但补评**新发现** 4 条测试面 BLOCKING);
- **003 / 004**:当年判定分级**永久不可复核**(义务第 3 条);补评给出**当下新判定** —— 两件均有 BLOCKING;
- **四件一致结论**:`Test Evidence 门不通过`。

### 六·三 义务第 2 条回填 ✅

四件 story 的 `## Test Evidence` 段**均已追加补评登记块**,含:原件路径 · 判定摘要 · 门结论 · 「`Complete` 判定的有效性待用户裁定」。
**未擅自改动 `Status` 行本身**(状态变更归用户裁定 + `/story-done`,承 `sprint-status.yaml` 的「DO NOT edit manually」)。

### 六·四 连带登记

- **story-004 的 `BLOCKED-BY-W-1` 已解除**(W-1 于 2026-10-15 会签结案,`4565dc6`)—— 但解除的是**夹具的免责前提**;
  谓词实现 `ChainEligible` 本身**同样零命中**(story `:40` 自认「先落」的那半边)。两件事已分别登记;
- **未完成核查(禁借绿)**:story-002 的 `tests/integration/case-system/` 是否已有集成件**未查证完** —— 已在 story-002 登记块与故事面报告 §六·六 标注;
- **本核查不改变 005/006 的状态**(二者原件齐备,合规)。

### 六·五 边界(重申)

补评发现的一切 BLOCKING,**其修复归新一轮**;**不得声称「原判定如此」**。
本核查 + 四份补评件**不构成**对 2026-10-06 那次评审的追认或推翻 —— 它只提供 **2026-10-15 当日的独立判定**。

---

## 七、勘误轮(2026-10-15 落盘后复核)

### 七·一 触发与根因

§六·四 的「未完成核查」第 1 条(查 `tests/integration/case-system/` 是否存在集成件)执行后,
发现**补评本身存在系统性盲区**:

- `tests/integration/case-system/` **确实不存在** —— 但 `tests/README.md` 澄清:
  `tests/unit|integration/` 只是**账本约定**,UTF 实际编译的是 `EditMode/` 与 `PlayMode/` 两棵树;
- 实测 case-system 的测试件**远不止各 story 声明的那一个**:`EditMode/Boot/` 下有
  `judgment_writer_test.cs` / `ink_age_test.cs` / `casebook_command_router_test.cs` /
  `casebook_prescribe_chain_test.cs`,`PlayMode/` 下有 `case_opened_wiring_test.cs`(真启动序)与
  `e2e_full_loop_test.cs`(七环节全链),`EditMode/CaseSystem/` 下另有 `case_open_writer_test.cs`
  (真组合根 + 真流)与 `case_replay_persistence_test.cs`;
- 其中一批(尤其 `case_open_writer_test` / `judgment_writer_test` / `case_opened_wiring_test` /
  `e2e_full_loop_test`)**晚于 2026-10-06**(sprint-05/06/08,10-10 ~ 10-13)。
  按补评口径「**评当下代码**」,它们**必须计入**。

**根因(两方各半,如实登记)**:
1. **调度方**:评审 prompt 只指向 story §Test Evidence 声明的那个文件,未要求扫描全部测试树;
2. **代理**:在仅核一件的前提下作出「**零验证**」「**全仓零命中**」「**无处可依**」等
   超出其取证范围的全域否定表述 —— 这是「评审不可追补」之外的**第二种评审失效模式**,
   建议补入 `.claude/docs/review-workflow.md`(见 §七·四)。

### 七·二 影响面

| 原件 | 影响 |
|---|---|
| story-002 | **4 条被推翻**(AC-37-30 入流 / AC-37-05 判断半边 / AC-37-05 处置半边 / AC-37-23 不折叠);1 条保留并**加重**(`LastJudgmentOf` 实害链) |
| story-003 | **1 条被推翻**(B4 副作用序列);4 条保留 |
| story-001 | 无勘误(四条均为声明件自身断言形态) |
| story-004 | 无勘误(全树 grep 确认 `PatternDetector` 测试面只一处、生产面零调用者) |

### 七·三 净结论不变

四件的 **Test Evidence 门结论不变(均不通过)**,四件**维持 `In Review`**。
但 story-002/003 的**判定性质**由「AC 无证据」修正为「**声明证据与 AC 错配**」——
两者的修复面完全不同:后者主要是**补引用 + 订正声明件断言形态**,不是**从零补测试**。

### 七·四 建议(待用户裁定是否落盘)

1. **`review-workflow.md` 补第二失效模式** —— 在「不可追补」之侧增:
   「**评据不得越权**」:评审只能对其**实际核过的范围**作判定;
   作全域否定(「零命中」「全仓无」)前须**给出全树搜索命令与结果**,
   否则须降级为「在 X 范围内未发现」。
2. **Story §Test Evidence 应允许多路径** —— 本 epic 的教训是:一个 AC 的载体常横跨
   声明件 + 后续 sprint 的接线件;单路径声明是错配的结构性来源。
