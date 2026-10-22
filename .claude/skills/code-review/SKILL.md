---
name: code-review
description: "Performs an architectural and quality code review on a specified file or set of files. Checks for coding standard compliance, architectural pattern adherence, SOLID principles, testability, and performance concerns."
argument-hint: "[path-to-file-or-directory]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash, Task, AskUserQuestion
model: sonnet
agent: lead-programmer
---

## Phase 1: Load Target Files

Read the target file(s) in full. Read CLAUDE.md for project coding standards.

---

## Phase 2: Identify Engine Specialists

Read `.claude/docs/technical-preferences.md`, section `## Engine Specialists`. Note:

- The **Primary** specialist (used for architecture and broad engine concerns)
- The **Language/Code Specialist** (used when reviewing the project's primary language files)
- The **Shader Specialist** (used when reviewing shader files)
- The **UI Specialist** (used when reviewing UI code)

If the section reads `[TO BE CONFIGURED]`, no engine is pinned — skip engine specialist steps.

---

## Phase 3: ADR Compliance Check

**Argument:** `/code-review [file(s)]` may optionally include a story file path as the last argument (e.g., `/code-review src/combat/attack.gd production/epics/combat/story-001.md`). If a story path is provided, read it to extract the governing ADR reference.

Search for ADR references in, in priority order:
1. The story file (if provided as argument)
2. Header comments at the top of the implementation files
3. Commit messages referencing these files (`git log --oneline -- [file]`)

Look for patterns like `ADR-NNN` or `docs/architecture/ADR-`.

If no ADR references found, note: "No ADR references found — ADR compliance check skipped. For full ADR compliance review, provide the story path: `/code-review [files] [story-path]`."

For each referenced ADR: read the file, extract the **Decision** and **Consequences** sections, then classify any deviation:

- **ARCHITECTURAL VIOLATION** (BLOCKING): Uses a pattern explicitly rejected in the ADR
- **ADR DRIFT** (WARNING): Meaningfully diverges from the chosen approach without using a forbidden pattern
- **MINOR DEVIATION** (INFO): Small difference from ADR guidance that doesn't affect overall architecture

---

## Phase 4: Standards Compliance

Identify the system category (engine, gameplay, AI, networking, UI, tools) and evaluate:

- [ ] Public methods and classes have doc comments
- [ ] Cyclomatic complexity under 10 per method
- [ ] No method exceeds 40 lines (excluding data declarations)
- [ ] Dependencies are injected (no static singletons for game state)
- [ ] Configuration values loaded from data files
- [ ] Systems expose interfaces (not concrete class dependencies)

---

## Phase 5: Architecture and SOLID

**Architecture:**
- [ ] Correct dependency direction (engine <- gameplay, not reverse)
- [ ] No circular dependencies between modules
- [ ] Proper layer separation (UI does not own game state)
- [ ] Events/signals used for cross-system communication
- [ ] Consistent with established patterns in the codebase

**SOLID:**
- [ ] Single Responsibility: Each class has one reason to change
- [ ] Open/Closed: Extendable without modification
- [ ] Liskov Substitution: Subtypes substitutable for base types
- [ ] Interface Segregation: No fat interfaces
- [ ] Dependency Inversion: Depends on abstractions, not concretions

---

## Phase 6: Game-Specific Concerns

- [ ] Frame-rate independence (delta time usage)
- [ ] No allocations in hot paths (update loops)
- [ ] Proper null/empty state handling
- [ ] Thread safety where required
- [ ] Resource cleanup (no leaks)

---

## Phase 7: Specialist Reviews (Parallel)

Spawn all applicable specialists simultaneously via Task — do not wait for one before starting the next.

### Engine Specialists

If an engine is configured, determine which specialist applies to each file and spawn in parallel:

- Primary language files (`.gd`, `.cs`, `.cpp`) → Language/Code Specialist
- Shader files (`.gdshader`, `.hlsl`, shader graph) → Shader Specialist
- UI screen/widget code → UI Specialist
- Cross-cutting or unclear → Primary Specialist

Also spawn the **Primary Specialist** for any file touching engine architecture (scene structure, node hierarchy, lifecycle hooks).

### QA Testability Review

For Logic and Integration stories, also spawn `qa-tester` via Task in parallel with the engine specialists. Pass:
- The implementation files being reviewed
- The story's `## QA Test Cases` section (the pre-written test specs from qa-lead)
- The story's `## Acceptance Criteria`

Ask the qa-tester to evaluate:
- [ ] Are all test hooks and interfaces exposed (not hidden behind private/internal access)?
- [ ] Do the QA test cases from the story's `## QA Test Cases` section map to testable code paths?
- [ ] Are any acceptance criteria untestable as implemented (e.g., hardcoded values, no seam for injection)?
- [ ] Does the implementation introduce any new edge cases not covered by the existing QA test cases?
- [ ] Are there any observable side effects that should have a test but don't?

For Visual/Feel and UI stories: qa-tester reviews whether the manual verification steps in `## QA Test Cases` are achievable with the implementation as written — e.g., "is the state the manual checker needs to reach actually reachable?"

Collect all specialist findings before producing output.

---

## Phase 7b: Agent Briefing Discipline(评审 / 重评 prompt 拆小 · S13-6)

> **立规缘由(sprint-12 复盘行动项 3,Med)**:sprint-12 的重评轮要求每位专家
> 「**14 项逐条核 + 硬约束逐条实测**」,结果**两位专家均撞轮次上限未出报告**,
> 须 `SendMessage` 续跑才交付(`retro-sprint-12-2026-10-16.md:80-81 / :91`)。
> **轮次上限不是能力问题,是 prompt 结构问题** —— 把「核账」与「查新」压进一个 agent,
> 它会把轮次全烧在勘察上,报告还没写就超限。

**三条纪律,按轮次类型择一或并用(③ 必用):**

### ① 首轮评审:预声明「先出报告,再补查」(默认)

给每位专家的 brief **结尾必须**加这段(逐字,可改写措辞但三项不可省):

```
⚠️ 轮次纪律(硬约束):
1. **先交报告,再做补充勘察** —— 勘察到足以支撑每条判定的**最小**证据即停,
   立刻产出报告;报告交付**优先于**证据完备。
2. 报告须含「未及核查项」一节,把没跑完的项**如实列出**(禁借绿 —— 没跑就写没跑)。
3. 硬预算:≤ 12 次工具调用产出报告;若逼近上限,**立即交付当前报告**,
   在「未及核查项」里写明剩余项。
```

### ② 重评轮:拆成**两个** agent(重评时默认)

重评轮面对的是「原判定是否成立 + 修复是否真到位 + 有无新缺陷」三重负担 ——
**必须拆**:

| Agent | 职责(只此一项) | 交付物 |
|---|---|---|
| **A · 原判据核账** | 逐条核对**原 BLOCKING / WARNING** 的修复落点是否真在代码里(读代码 + 跑原验证命令) | 「原判定 → 修复落点 → 验证命令」三列结账表 |
| **B · 新缺陷 + 硬约束** | 只看**新引入**的问题 + 硬约束(门 A / 循环复杂度 / 行长 / 证伪轮形态) | 新缺陷表 + 硬约束逐条实测 |

两个 agent **并行** spawn(Phase 7 的并行纪律不变),各自独立出报告 ⇒
任一超限**不影响**另一份交付。

### ③ 增量落盘(2026-10-17 立 · **TD-s13-01 的机制修复**,用户裁定)

> **立规缘由**:① 与 ② 落地后,首次实战(story-003 评审)仍**两位专家撞 20 / 10 轮零报告**
> (`review-medcons-story-003-2026-10-16.md:20-22`)⇒ **拆小 + 预声明「先出报告」不足以**保证交付,
> 「先出报告」依赖 agent 对自己的轮次做自我监控,**这个前提本身没有夹具**。

**纪律:勘察与写作交替,不攒到最后写。**

- 每位专家 spawn 时**必须**收到一个**草稿路径**(编排方指定,如
  `production/qa/evidence/tmp-review-<对象>-<agent>.md`),并在 brief 里写明:
  ```
  每完成一项勘察(读完一个文件 / 跑完一条命令 / 核完一条判定)，
  **立即 Append 一条到草稿文件**，格式：`| ID | 判定 | 落点 | 证据 |`。
  不要等全部勘察完再写 —— 草稿 = 报告的分析段，轮次耗尽时它就是交付物。
  ```
- 交付 = **草稿文件已存在且非空**,而非「agent 在对话里贴出完整报告」。
  即使 agent 超限被停,草稿仍在盘上,再由编排方读草稿补成报告。
- 草稿合格后由编排方合并为原件(`review-<对象>-<日期>.md`),草稿随后删除
  (不留两份并行的分析段)。

### 判据(S13-6 AC · 2026-10-17 收窄)

> **旧判据(已作废)**:下一次重评两位专家均在**轮次上限内**交付报告
> —— 首次实战实测为**红**(两位均超限零报告),见 `TD-s13-01`。
>
> **新判据(现行)**:下一次真实重评时,**每位专家的草稿文件在轮次耗尽前已非空,
> 且覆盖其「判定清单过半」(≥ 清单条数的一半有结论)**。
> 可复算:`wc -l <草稿路径>` > 0 且 草稿内结论行数 ≥ 应核条数 / 2。
> ⚠️ 仍为**前瞻性判据** —— 须在下次真实重评实测,当下不可自证(禁借绿)。
> 本节**存在性**可证伪(改本节 ⇒ skill 行为变),**有效性**待下次重评。

---

## Phase 8: Output Review

```
## Code Review: [File/System Name]

### Engine Specialist Findings: [N/A — no engine configured / CLEAN / ISSUES FOUND]
[Findings from engine specialist(s), or "No engine configured." if skipped]

### Testability: [N/A — Visual/Feel or Config story / TESTABLE / GAPS / BLOCKING]
[qa-tester findings: test hooks, coverage gaps, untestable paths, new edge cases]
[If BLOCKING: implementation must expose [X] before tests in ## QA Test Cases can run]

### ADR Compliance: [NO ADRS FOUND / COMPLIANT / DRIFT / VIOLATION]
[List each ADR checked, result, and any deviations with severity]

### Standards Compliance: [X/6 passing]
[List failures with line references]

### Architecture: [CLEAN / MINOR ISSUES / VIOLATIONS FOUND]
[List specific architectural concerns]

### SOLID: [COMPLIANT / ISSUES FOUND]
[List specific violations]

### Game-Specific Concerns
[List game development specific issues]

### Positive Observations
[What is done well -- always include this section]

### Required Changes
[Must-fix items before approval — ARCHITECTURAL VIOLATIONs always appear here]

### Suggestions
[Nice-to-have improvements]

### Verdict: [APPROVED / APPROVED WITH SUGGESTIONS / CHANGES REQUIRED]
```

This skill is read-only — no files are written.

---

## Phase 8b: Close-Out Table Standard(修复轮结账表 —— 已落 / 已证 分列)

> **立规缘由(sprint-12 复盘行动项 1,High)**:`review-*.md` 原件的 §六「修复轮结账」表
> 历来单列「状态」,把**两件事混填** —— ①「代码改了」(动作)与 ②「改对了且被夹具钉住」(证明)。
> 实例:story-002 评审原件 J 项(`Bind` ≈110 行超 40 行标准)被标 `✅ 已落 / 结案证据:
> ParseRows/ValidateCoverage 拆分` —— 拆分动作**只兑现一半**(`ParseRows` 仍 69 行,
> ≤40 界无任何夹具钉),「已落」被当成了「已证」。**把「我做了动作」当成「我做对了并被证明」。**

评审产出 BLOCKING / WARNING / MINOR 后,**修复轮**须在
`production/qa/evidence/review-<subject>-<date>.md` 的结账表中逐条结清,每行**必须**
把完成度拆成两个正交列:

```
| ID | Landed(已落) | Proven(已证) | Branch(分支覆盖) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|---|
| R-1 | ✅ | ✅ | ✅ 3/3 | …Writer.cs:64 | §四 F1 RED:停用头槽写入 ⇒ test_ac53_cookedHeaderSchemaSlot 红 |
| R-2 | ✅ | ✅ | ✗ 2/3 | …Predicate.cs:123 | 三条件合取,只证了 `终态` / `¬豁免`;`¬未结案` 无夹具 |
| J   | ✅ | ✗  | — | …Binder.cs:210 | 拆分已落,但 ParseRows 仍 69 行 —— ≤40 界无夹具 ⇒ 不得标已证 |
```

- **Landed(已落)** = 改动在盘上。仅陈述事实,**不主张任何正确性**。
- **Proven(已证)** = 改对了**且被可复算判据钉住**。末列必填三选一:
  ① 测试名 + `file:line` ② 反证记录「停用 X ⇒ <测试名> 红」(S12-4 形态)
  ③ 第三方可重跑的命令(含预期输出)。
  ⚠️ 在 Proven / Proven-by 列写**动作描述**(「拆了方法 / 改了措辞 / 已重构 / 见下行清单」)
  = **不合格** —— 那是把 Landed 伪装成证明。
- **Branch(分支覆盖)** = 该判定的**每个条件分支各有一例夹具**。列 `✅ n/m` / `✗ n/m` 并给出
  未覆盖分支名(见上方 `R-2` 例)。**这是「Proven ✅」的加强门,不是替代**:
  - 单分支判定(无 `if` / 无合取 / 无白名单分支)填 `—` 或 `✅ 1/1`,不强制;
  - **多分支判定而 `Branch` 缺分支 ⇒ 该行 `Proven` 不得标 ✅**(降为 `Landed=✅ / Proven=✗`)。
  - 立规缘由(sprint-18 实证):S18-2 的折叠谓词是**四条件合取**,证伪轮 4/4 全红却**没有一条**
    打「抹哪些行」—— 即条件③④ 的分支有反证,而**产出侧分支零反证**。当时若有此列,
    「折叠产出面已验证」这句话根本写不出来(签核件 D-5 是事后才抓到的)。
- 一行**两列皆 ✅ 才算结清**。`Landed=✅ / Proven=✗` 是合法的诚实状态
  (部分修 / standards 类无界检查项),**须暴露而非隐藏**。
- **本轮未运行其判据 ⇒ 不得标 Proven ✅**(禁借绿同族)。
- 本节只定**表格式**;产生「已证」证据的**机制**归 S12-4 Falsifiability
  (`/story-done` 门),两者配合:证伪轮产生反证 ⇒ 反证记录抄入 Proven-by 列。

**向前生效**:本规不追改已结案的评审原件(改历史 = 销毁当时记录);
旧原件单列「状态」的形态作为历史保留,新评审起一律双列。

> 🔁 **2026-10-22(sprint-18 收口 · retro-14 行动项 3,欠两轮后落地)**:结账表增 **`Branch(分支覆盖)`** 列。
> 该项由 retro-14 提出、sprint-15 与 sprint-17 两轮未做(全仓 `grep -rn "分支覆盖" .claude/skills/ .claude/docs/`
> 当时零命中)。**不追改已结案原件** —— 上方「向前生效」条款同样适用于本列。

---

## Phase 9: Next Steps

Use `AskUserQuestion`:
- Prompt: "Code review complete — verdict: [APPROVED / CHANGES REQUIRED / MAJOR REVISION]. How would you like to proceed?"
- Options (adjust based on verdict):
  - If APPROVED:
    - `[A] Run /story-done to mark the story complete`
    - `[B] Stop here`
  - If CHANGES REQUIRED or MAJOR REVISION:
    - `[A] Fix the issues and re-run /code-review`
    - `[B] Run /story-done anyway with noted exceptions`
    - `[C] Stop here`

If an ARCHITECTURAL VIOLATION is found:
- If the violation contradicts an **existing ADR**: fix the implementation to comply with `docs/architecture/[adr-file].md`. If the design has legitimately changed, run `/architecture-decision` to formally *revise* the existing ADR — do not create a competing one.
- If **no ADR exists** for the pattern that was violated: run `/architecture-decision` to document the correct approach before fixing the code.
