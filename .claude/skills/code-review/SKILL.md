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
| ID | Landed(已落) | Proven(已证) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| R-1 | ✅ | ✅ | …Writer.cs:64 | §四 F1 RED:停用头槽写入 ⇒ test_ac53_cookedHeaderSchemaSlot 红 |
| J   | ✅ | ✗  | …Binder.cs:210 | 拆分已落,但 ParseRows 仍 69 行 —— ≤40 界无夹具 ⇒ 不得标已证 |
```

- **Landed(已落)** = 改动在盘上。仅陈述事实,**不主张任何正确性**。
- **Proven(已证)** = 改对了**且被可复算判据钉住**。末列必填三选一:
  ① 测试名 + `file:line` ② 反证记录「停用 X ⇒ <测试名> 红」(S12-4 形态)
  ③ 第三方可重跑的命令(含预期输出)。
  ⚠️ 在 Proven / Proven-by 列写**动作描述**(「拆了方法 / 改了措辞 / 已重构 / 见下行清单」)
  = **不合格** —— 那是把 Landed 伪装成证明。
- 一行**两列皆 ✅ 才算结清**。`Landed=✅ / Proven=✗` 是合法的诚实状态
  (部分修 / standards 类无界检查项),**须暴露而非隐藏**。
- **本轮未运行其判据 ⇒ 不得标 Proven ✅**(禁借绿同族)。
- 本节只定**表格式**;产生「已证」证据的**机制**归 S12-4 Falsifiability
  (`/story-done` 门),两者配合:证伪轮产生反证 ⇒ 反证记录抄入 Proven-by 列。

**向前生效**:本规不追改已结案的评审原件(改历史 = 销毁当时记录);
旧原件单列「状态」的形态作为历史保留,新评审起一律双列。

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
