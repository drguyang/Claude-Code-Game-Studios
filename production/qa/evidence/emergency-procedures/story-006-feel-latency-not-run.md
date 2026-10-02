# Story 006 Evidence — 手感/预表现/键鼠回退

**Story**: 006 — 手感、预表现与键鼠回退
**Epic**: emergency-procedures
**Date**: 2026-10-02
**Status**: NOT-RUN (ADVISORY)

---

## AC-10-08 [L] ADVISORY: L_input < 50 ms 实测

**Status**: NOT-RUN
**Reason**: 依赖 OQ-10-12 原型门（2 动作垂直原型实测）
**Blocking**: OQ-10-12 未执行
**Sign-off**: 待主创签核
**Evidence**: 无（实测前 NOT-RUN）

---

## AC-10-19 [L] ADVISORY: 联机非主机 L_input

**Status**: NOT-RUN
**Reason**: 依赖 45 联机夹具（P1b）
**Blocking**: 45 网络层未落地
**Sign-off**: 待主创签核
**Evidence**: 无（实测前 NOT-RUN）

---

## AC-10-23 [L] ADVISORY: 录屏走查

**Status**: NOT-RUN
**Reason**: 手感不可自动化测，需主创走查
**Blocking**: 桌面调试轮未排程
**Sign-off**: 待主创签核
**Evidence**: 无（走查前 NOT-RUN）

---

## 自动化半边（BLOCKING 逻辑测）

| AC | 测试 | 结果 |
|----|------|------|
| AC-10-21 | 键鼠回退幅度门恒过 + 双门照评 | ✅ 3 tests passed |
| AC-10-09 | DTO 无评价/分数/进度/完成度字段 | ✅ 1 test passed |
| AC-10-17 | 音频 cue 不因 JudgeResult 而异 | ✅ 2 tests passed |
| F-10.6 | 不存在合并延迟表述 | ✅ 1 test passed |

---

## 签核记录

| 日期 | 签核人 | AC | 结论 |
|------|--------|-----|------|
| — | — | AC-10-08 | 待签核 |
| — | — | AC-10-19 | 待签核 |
| — | — | AC-10-23 | 待签核 |

---

## 备注

- 本 story 的 [L] ADVISORY 项以主创签核结
- 自动化半边（静态扫描）可 BLOCKING
- 手感不可自动化测（GDD 原文）
