# Directory Structure

```text
/
├── CLAUDE.md                    # Master configuration
├── .claude/                     # Agent definitions, skills, hooks, rules, docs
├── src/                         # Game source code (core, gameplay, ai, networking, ui, tools)
├── assets/                      # Game assets (art, audio, vfx, shaders, data)
├── design/                      # Game design documents (gdd, narrative, levels, balance)
├── docs/                        # Technical documentation (architecture, api, postmortems)
│   └── engine-reference/        # Curated engine API snapshots (version-pinned)
├── tests/                       # Test suites (unit, integration, performance, playtest)
├── tools/                       # Build and pipeline tools (ci, build, asset-pipeline)
├── prototypes/                  # Throwaway prototypes (isolated from src/)
└── production/                  # Production management (sprints, milestones, releases)
    ├── session-state/           # 会话状态(3 件**已入库** —— 非 gitignored,见下方订正)
    └── session-logs/            # Session audit trail(gitignored —— `.gitignore:17`)
```

> ⚠️ **2026-10-28 订正**:本文件原写 `session-state/` 为 `(active.md — gitignored)`,**与实况不符**。
> 实测:
> - `git ls-files production/session-state` 命中 **3 件** —— `.gitkeep` / `active.md` /
>   `review-prompts-2026-09-18.md`;`git check-ignore -v production/session-state` **零命中**。
>   ⇒ `active.md` 是**入库**文件,改动须随 commit 落下(否则工作树留脏)。
> - `session-logs/` 一侧原描述**正确**:`git ls-files` 零命中,被 `.gitignore:17` 的
>   `**/production/session-logs/` 覆盖(盘上 1.1 G,纯本地审计留档)。
>
> 复算:
> ```bash
> git ls-files production/session-state | wc -l      # 期望 3
> git check-ignore -v production/session-state       # 期望无输出(未忽略)
> git ls-files production/session-logs | wc -l       # 期望 0
> git check-ignore -v production/session-logs        # 期望命中 .gitignore:17
> ```
