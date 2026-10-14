# Coding Standards

- All game code must include doc comments on public APIs
- Every system must have a corresponding architecture decision record in `docs/architecture/`
- Gameplay values must be data-driven (external config), never hardcoded
- All public methods must be unit-testable (dependency injection over singletons)
- Commits must reference the relevant design document or task ID
- **Commit messages**: Use Conventional Commits format — `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`. Reference the story or task ID in the body (e.g., `Story: EPIC-001-S02`).
- **Verification-driven development**: Write tests first when adding gameplay systems.
  For UI changes, verify with screenshots. Compare expected output to actual output
  before marking work complete. Every implementation should have a way to prove it works.

# Review Evidence Standards

- **评审报告原件落盘(BLOCKING 级 · 2026-10-02 立)**:任何产出 **BLOCKING 判定**的评审,
  其报告原件须落 `production/qa/evidence/review-<对象>-<YYYY-MM-DD>.md`,
  含 **原判定 → 修复落点 → 验证命令**(可证伪)。**无原件 ⇒ 该对象不得转 `Complete`**。
  - ⚠️ **commit message 摘要不构成原件** —— 它是索引,不是报告。
  - ⚠️ **对账件(`reconciliation-*.md`)不可替代原件** —— 对账件验证的是
    「自述的修复是否真在代码里」,**不验证「原判定是否完备」**。
  - ⚠️ **评审不可事后追补** —— 事后重做评的是**当下**的代码,得到的是**新**判定,
    **不追认**原判定。缺原件的对象,出路是**补做一次评审**(评当下)并落新原件。
  - 完整义务与现存缺口见 `.claude/docs/review-workflow.md`;
    收口闸门见 `.claude/skills/story-done/SKILL.md` §评审报告原件要求。

# Design Document Standards

- All design docs use Markdown
- Each mechanic has a dedicated document in `design/gdd/`
- Documents must include these 8 required sections:
  1. **Overview** -- one-paragraph summary
  2. **Player Fantasy** -- intended feeling and experience
  3. **Detailed Rules** -- unambiguous mechanics
  4. **Formulas** -- all math defined with variables
  5. **Edge Cases** -- unusual situations handled
  6. **Dependencies** -- other systems listed
  7. **Tuning Knobs** -- configurable values identified
  8. **Acceptance Criteria** -- testable success conditions
- Balance values must link to their source formula or rationale

# Testing Standards

## Test Evidence by Story Type

All stories must have appropriate test evidence before they can be marked Done:

| Story Type | Required Evidence | Location | Gate Level |
|---|---|---|---|
| **Logic** (formulas, AI, state machines) | Automated unit test — must pass | `unity/Assets/Tests/EditMode/[System]/` | BLOCKING |
| **Integration** (multi-system) | Integration test OR documented playtest | `unity/Assets/Tests/PlayMode/`(或同树 `EditMode/[System]/` 真装配件) | BLOCKING |
| **Visual/Feel** (animation, VFX, feel) | Screenshot + lead sign-off | `production/qa/evidence/` | ADVISORY |
| **UI** (menus, HUD, screens) | Manual walkthrough doc OR interaction test | `production/qa/evidence/` | ADVISORY |
| **Config/Data** (balance tuning) | Smoke check pass | `production/qa/smoke-[date].md` | ADVISORY |

> ⚠️ **路径写明实际落地的那棵树,且允许一条 AC 多载体(2026-10-15 立)** ——
> 测试件横跨**两棵树**:`tests/unit|integration/[system]/` 是**账本约定**(见 `tests/README.md`),
> `unity/Assets/Tests/{EditMode,PlayMode}/` 是工程落地后的**主体编译树**;
> **两棵都可能承载活测试**(账本树非恒空:`tests/unit/skeuomorphic-ui/` 自带 `.asmdef`)。
> ⇒ ① 上表 Location 写**实际落地路径**,另一棵可并列注明;
> ② **一个 AC 的载体允许多条**(story §Test Evidence 的 `AC → 载体映射` 表须列全)——
> 后续 sprint 补的真装配件 / 真启动序件同样是该 AC 的证据;
> ③ 判「无证据」前须两树都扫(`grep -rln "<符号>" unity/Assets/Tests/ tests/`)并贴出输出,
> 否则只能写「在 X 范围内未发现」。详见 `.claude/docs/review-workflow.md` 义务 4「评据不得越权」。

## Automated Test Rules

- **Naming**: `[system]_[feature]_test.[ext]` for files; `test_[scenario]_[expected]` for functions
- **Determinism**: Tests must produce the same result every run — no random seeds, no time-dependent assertions
- **Isolation**: Each test sets up and tears down its own state; tests must not depend on execution order
- **No hardcoded data**: Test fixtures use constant files or factory functions, not inline magic numbers
  (exception: boundary value tests where the exact number IS the point)
- **Independence**: Unit tests do not call external APIs, databases, or file I/O — use dependency injection

## What NOT to Automate

- Visual fidelity (shader output, VFX appearance, animation curves)
- "Feel" qualities (input responsiveness, perceived weight, timing)
- Platform-specific rendering (test on target hardware, not headlessly)
- Full gameplay sessions (covered by playtesting, not automation)

## CI/CD Rules

- Automated test suite runs on every push to main and every PR
- No merge if tests fail — tests are a blocking gate in CI
- Never disable or skip failing tests to make CI pass — fix the underlying issue
- Engine-specific CI commands:
  - **Godot**: `godot --headless --script tests/gdunit4_runner.gd`
  - **Unity**: `game-ci/unity-test-runner@v4` (GitHub Actions)
  - **Unreal**: headless runner with `-nullrhi` flag
