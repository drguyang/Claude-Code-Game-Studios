# Test Evidence

**Purpose**: Screenshots, video clips, and manual test sign-off records for stories that require visual or interactive verification.

## Directory Layout

```
tests/evidence/
  [system]-[story-id]-[description].png    # Screenshots
  [system]-[story-id]-[description].mp4    # Video clips (optional)
  [system]-[story-id]-signoff.md           # Sign-off record
```

## When to Use

Per `coding-standards.md`, these story types require evidence here:

| Story Type | Required Evidence | Gate |
|---|---|---|
| Visual/Feel | Screenshot + lead sign-off | ADVISORY |
| UI | Manual walkthrough OR interaction test | ADVISORY |

Logic and Integration stories are covered by automated tests in `tests/unit/` and `tests/integration/`.

## Sign-off Record Format

Each `-signoff.md` file must contain:

```markdown
# [System] Story [ID] — Test Evidence

**Story**: [title]
**Date**: [YYYY-MM-DD]
**Tester**: [name]

## Screenshots

| # | Description | File |
|---|-------------|------|
| 1 | [what the screenshot shows] | `[filename].png` |

## Observations

[What was verified, any issues found]

## Result

- [ ] PASS — meets acceptance criteria
- [ ] FAIL — issues documented in [link to bug/note]
```

## Naming Convention

- Files: `[system]-[story-id]-[short-descriptor].[ext]`
- Example: `skeuomorphic-ui-005-world-anchor-face.png`

## CI

Evidence files are **not** validated by automated CI.
Sign-off records are reviewed during `/gate-check` by the relevant lead.
