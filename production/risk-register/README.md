# Risk Register

**Project**: 《大医精诚:破晓之剂》

| ID | Risk | Likelihood | Impact | Mitigation | Status |
|-----|------|-----------|--------|-----------|--------|
| R-1 | EditMode 4 failures indicate deeper test issues | Medium | High | Re-run full suite on desktop; fix root cause | Open |
| R-2 | CI UNITY_LICENSE not configured → CI always red | High | Medium | Manual secret configuration (user action) | Open |
| R-3 | Performance budgets unfinalized → no profiling baseline | Medium | High | Finalize in sprint planning; defer to user | Open |
| R-4 | ADR-023 S2/S5/S6/S7 spikes unrun → scene loading unknown | Low | High | Run as part of pre-production spike sprint | Open |
| R-5 | ADR-013 assumption 6 (focus nav) unspiked → UI navigation break | Medium | High | Schedule desktop debugging session | Open |
| R-6 | OQ-1-12 接地 model unvalidated → physics mismatch | Low | High | Run validation before implementation | Open |
| R-7 | OQ-10-12 两动作 prototype unvalidated → emergency feel wrong | Low | Medium | Run prototype session | Open |
| R-8 | AB-6 生态区命名 unverified → environment art rework | Medium | Medium | Complete 40 考据 round before environment art | Open |
| R-9 | 31 systems × solo dev scope pressure | High | High | Strict P0/P1 separation; 6-9 month baseline | Accepted |
