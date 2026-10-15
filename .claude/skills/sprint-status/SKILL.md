---
name: sprint-status
description: "Fast sprint status check. Reads the current sprint plan, scans story files for status, and produces a concise progress snapshot with burndown assessment and emerging risks. Run at any time during a sprint for quick situational awareness. Use when user asks 'how is the sprint going', 'sprint update', 'show sprint progress'."
argument-hint: "[sprint-number or blank for current]"
user-invocable: true
allowed-tools: Read, Glob, Grep
model: haiku
---

# Sprint Status

This is a fast situational awareness check, not a sprint review. It reads the
current sprint plan and story files, scans for status markers, and produces a
concise snapshot in under 30 lines. For detailed sprint management, use
`/sprint-plan update` or `/milestone-review`.

**This skill is read-only.** It never proposes changes, never asks to write
files, and makes at most one concrete recommendation.

---

## 1. Find the Sprint

**Argument:** `$ARGUMENTS[0]` (blank = use current sprint)

- If an argument is given (e.g., `/sprint-status 3`), search
  `production/sprints/` for a file matching `sprint-03.md`, `sprint-3.md`,
  or similar. Report which file was found.
- If no argument is given, find the most recently modified file in
  `production/sprints/` and treat it as the current sprint.
- If `production/sprints/` does not exist or is empty, report: "No sprint
  files found. Start a sprint with `/sprint-plan new`." Then stop.

Read the sprint file in full. Extract:
- Sprint number and goal
- Start date and end date
- All story or task entries with their priority (Must Have / Should Have /
  Nice to Have), owner, and estimate

---

## 2. Calculate Days Remaining

Using today's date and the sprint end date from the sprint file, calculate:
- Total sprint days (end minus start)
- Days elapsed
- Days remaining
- Percentage of time consumed

If the sprint file does not include explicit dates, note "Sprint dates not
found — burndown assessment skipped."

---

## 3. Scan Story Status

**First: check for `production/sprint-status.yaml`.**

If it exists, read it directly — it is the authoritative source of truth.
Extract status for each story from the `status` field. No markdown scanning needed.
Use its `sprint`, `goal`, `start`, `end` fields instead of re-parsing the sprint plan.

**If `sprint-status.yaml` does not exist** (legacy sprint or first-time setup),
fall back to markdown scanning:

1. If the entry references a story file path, check if the file exists.
   Read the file and scan for status markers: DONE, COMPLETE, IN PROGRESS,
   BLOCKED, NOT STARTED (case-insensitive).
2. If the entry has no file path (inline task in the sprint plan), scan the
   sprint plan itself for status markers next to that entry.
3. If no status marker is found, classify as NOT STARTED.
4. If a file is referenced but does not exist, classify as MISSING and note it.

When using the fallback, add a note at the bottom of the output:
"⚠ No `sprint-status.yaml` found — status inferred from markdown. Run `/sprint-plan update` to generate one."

Optionally (fast check only — do not do a deep scan): grep `src/` for a
directory or file name that matches the story's system slug to check for
implementation evidence. This is a hint only, not a definitive status.

### Stale Story Detection

After collecting status for all stories, check each IN PROGRESS story for staleness:

- For each story that has a referenced file, read the file and look for a
  `Last Updated:` field in the frontmatter or header (e.g., `Last Updated: 2026-04-01`
  or `updated: 2026-04-01`). Accept any reasonable date field name: `Last Updated`,
  `Updated`, `last-updated`, `updated_at`.
- Calculate days since that date using today's date.
- If the date is more than 4 days ago, flag the story as **STALE**. (4-day threshold accounts for weekends — a story last touched on Friday won't appear stale until Wednesday.)
- If no date field is found in the story file, note "no timestamp — cannot check staleness."
- If the story has no referenced file (inline task), note "inline task — cannot check staleness."

STALE stories are included in the output table and collected into an "Attention Needed"
section (see Phase 5 output format).

**Stale story escalation**: If any IN PROGRESS story is flagged STALE (no progress in 4+ days), the burndown verdict
is upgraded to at least **At Risk** — even if the completion percentage is within the normal
On Track window. Record this escalation reason: "At Risk — [N] story(ies) with no progress in
[N] days."

---

## 4. Burndown Assessment

Calculate:
- Tasks complete (DONE or COMPLETE)
- Tasks in progress (IN PROGRESS)
- Tasks blocked (BLOCKED)
- Tasks not started (NOT STARTED or MISSING)
- Completion percentage: (complete / total) * 100

Assess burndown by comparing completion percentage to time consumed percentage:

- **On Track**: completion % is within 10 points of time consumed % or ahead
- **At Risk**: completion % is 10-25 points behind time consumed %
- **Behind**: completion % is more than 25 points behind time consumed %

If dates are unavailable **and** the sprint does not declare "不设结束日期",
skip the burndown assessment and report "On Track / At Risk / Behind: unknown —
sprint dates not found."

### 4b. 产能模型评估(sprint 无日期或声明「不设结束日期」时)

> **本节的判据**:`production/CAPACITY-MODEL.md` 是 sprint-12 `S12-1` 建立的新度量口径
> (`/sprint-status` 引用它 = 该模型的存在性证明;不引用即模型失败)。**不要**把它读成
> 「又一份文档」—— 它是本 skill 在无日期场景下的**替代度量源**。

For a sprint with no usable end date (dates missing, or the plan declares
「不设结束日期」), **read `production/CAPACITY-MODEL.md`** and replace the burndown
verdict with a capacity verdict built from its three knobs:

- **复杂度 C** — each story's `estimate_days: 0` in `sprint-status.yaml` is a
  **legacy field, not a duration** (sprint-12 起废止旧「solo 人日」口径). Read
  complexity from the story's `Est` column in the sprint plan (S / M / L), not
  from `estimate_days`.
- **并发上限 P** — 同时在跑 ≤ 3 件. Count stories that were `in-progress`
  simultaneously; > 3 is a process flag, not a schedule slip.
- **判别力门槛 D** — 每件须有可证伪判据(「回退即转红」)才计入 done.
  A `done` story whose tests were never falsified does **not** count as complete.

Then report, in place of the burndown line:

```markdown
### 产能(无结束日期 —— 依 production/CAPACITY-MODEL.md)
- 复杂度分布: S×[n] / M×[n] / L×[n]
- 判别力门槛 D: [n] 件有「回退即转红」记录 / [m] 件未证伪(不计完成)
- 基线对照: sprint-11 实测 16 件 / 2 执行日(治理债类);本 sprint = [n] 件 / [d] 执行日
- 结论: [与基线一致 / 高于基线 / 低于基线 —— 说明差异来源]
```

**不要**对「不设结束日期」的 sprint 输出 "unknown" —— 那是把**口径缺省**错报成
**信息缺省**;产能模型正是为填这个空而建立。

---

## 5. Output

Keep the output concise. The story status table is mandatory — do not truncate it. Aim for under 50 lines total; omit the Emerging Risks section if nothing notable was found. Use this format:

```markdown
## Sprint [N] Status — [Today's Date]
**Sprint Goal**: [from sprint plan]
**Days Remaining**: [N] of [total] ([% time consumed])

### Progress: [complete/total] tasks ([%])

| Story / Task         | Priority   | Status      | Owner   | Blocker        |
|----------------------|------------|-------------|---------|----------------|
| [title]              | Must Have  | DONE        | [owner] |                |
| [title]              | Must Have  | IN PROGRESS | [owner] |                |
| [title]              | Must Have  | BLOCKED     | [owner] | [brief reason] |
| [title]              | Should Have| NOT STARTED | [owner] |                |

### Attention Needed
| Story / Task         | Status      | Last Updated   | Days Stale | Note           |
|----------------------|-------------|----------------|------------|----------------|
| [title]              | IN PROGRESS | [date or N/A]  | [N days]   | [STALE / no timestamp — cannot check staleness / inline task — cannot check staleness] |

*(Omit this section entirely if no IN PROGRESS stories are stale or have timestamp concerns.)*

### Burndown: [On Track / At Risk / Behind]
[1-2 sentences. If behind: which Must Haves are at risk. If on track: confirm
and note any Should Haves the team could pull.]
*(Sprint 无可用结束日期时,本段整体替换为 §4b 的「产能」段 —— 依
`production/CAPACITY-MODEL.md`,而不是输出 unknown。)*

### Must-Haves at Risk
[List any Must Have stories that are BLOCKED or NOT STARTED with less than
40% of sprint time remaining. If none, write "None."]

### Emerging Risks
[Any risks visible from the story scan: missing files, cascading blockers,
stories with no owner. If none, write "None identified."]

### Recommendation
[One concrete action, or "Sprint is on track — no action needed."]
```

---

## 6. Fast Escalation Rules

Apply these rules before outputting, and place the flag at the TOP of the
output if triggered (above the status table):

**Critical flag** — if Must Have stories are BLOCKED or NOT STARTED and
less than 40% of the sprint time remains:

```
SPRINT AT RISK: [N] Must Have stories are not complete with [X]% of sprint
time remaining. Recommend replanning with `/sprint-plan update`.
```

**Completion flag** — if all Must Have stories are DONE:

```
All Must Haves complete. Team can pull from Should Have backlog.
```

**Missing stories flag** — if any referenced story files do not exist:

```
NOTE: [N] story files referenced in the sprint plan are missing.
Run `/story-readiness sprint` to validate story file coverage.
```

---

## Collaborative Protocol

This skill is read-only. It reports observed facts from files on disk.

- It does not update the sprint plan
- It does not change story status
- It does not propose scope cuts (that is `/sprint-plan update`)
- It makes at most one recommendation per run

For more detail on a specific story, the user can read the story file directly
or run `/story-readiness [path]`.

For sprint replanning, use `/sprint-plan update`.
For end-of-sprint retrospective, use `/retrospective`.
