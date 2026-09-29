# Gate Check: Technical Setup → Pre-Production（二轮）

**Date**: 2026-09-29
**Checked by**: gate-check skill（review mode = **lean**；四门全跑）
**Previous verdict**: FAIL → 用户承接 → CONCERNS（2026-09-21）
**Current verdict**: **CONCERNS**（无 NOT READY 级阻塞）

---

## 一轮遗留项销账

| 一轮缺口 | 状态 | 证据 |
|----------|------|------|
| `production/` 零管理产物 | ✅ 已销 | committed 0e553e1 |
| 7a/7b missing epics | ✅ 已销 | `production/epics/persistence-service/` / `save-slot-ui/` 已创建 |
| `epics/index.md` stale | ✅ 已销 | 7 GDD paths 已修复 + 2 新 epic 行 |
| CI EditMode 4 failures | ✅ 已销 | 921 passed / 0 failed（桌面实测 2026-09-29） |
| OQ-1-12 / OQ-10-12 未裁 | ✅ 已销 | 用户裁定 2026-09-29，写入 `player-controller-and-movement.md` / `emergency-procedures.md` |

---

## Required Artifacts: 13/13 present

| # | 判据 | 实测 |
|---|------|------|
| 1 | CLAUDE.md 技术栈非 `[CHOOSE]` | ✅ Unity 6.3 LTS / URP / OpenXR |
| 2 | `.claude/docs/technical-preferences.md` 已填 | ✅（含 20 Hz / CAP 24 / DOTS 门 / 命名约定 / 性能预算） |
| 3 | `design/art/art-bible.md` §1–4 | ✅ 1113 行，9 节全部成稿 |
| 4 | ≥3 ADR 覆盖 Foundation | ✅ **25 份** ADR 全部 Accepted |
| 5 | `docs/engine-reference/unity/` | ✅ VERSION + breaking-changes + deprecated-apis + modules/ |
| 6 | `tests/unit/` + `tests/integration/` | ✅ 存在 |
| 7 | `.github/workflows/tests.yml` | ✅ 187 行，含 Mono/IL2CPP/ARM64 矩阵 |
| 8 | 至少一个示例测试文件 | ✅ EditMode 921 passed / 0 failed |
| 9 | `docs/architecture/architecture.md` | ✅ v1.0 Approved（2026-09-23 TD/LP 签字） |
| 10 | `docs/architecture/requirements-traceability.md` | ✅ |
| 11 | `/architecture-review` 报告存在 | ✅ 三份（2026-09-15 / 09-20 / 09-21） |
| 12 | `design/accessibility-requirements.md` 有档位 | ✅ Tier = Standard + L-1/L-2 |
| 13 | `design/ux/interaction-patterns.md` | ✅ 158 行 |

---

## Quality Checks

### 架构质量

- ✅ 25/25 ADR 均有 Engine Compatibility 段并钉版本 Unity 6.3 LTS
- ✅ 25/25 ADR 均有 GDD Requirements Addressed 段
- ✅ ADR 依赖图**无环**（24 ADR / 106 边 / 零循环，仅 Depends On 表格）
- ✅ Foundation 层 TR gap = 0（20 条：17 covered / 2 no-adr-by-design / 1 partial）
- ✅ 废弃 API 引用：零处（ADR-012 一处为诊断性引用，非使用建议）
- ✅ 引擎版本一致性：24/25 标注 "Unity 6.3"，ADR-023 标注精确版本 "6000.3.24f1"
- ⚠️ TR-concept-006（帧预算）仍为 gap，pending 最低目标硬件定稿

### CI / 测试质量

- ✅ EditMode 921 passed / 0 failed（桌面实测）
- ⚠️ 全量 921 条产物未 committed（仓内 XML 为小套件 33/37/37/13/12 条）
- ⚠️ CI 两处占位（`Unit-level golden hashes` / `guard-assertions` = `exit 0`）
- ⚠️ IL2CPP parity jobs = 故意 `exit 1`（F7 spike 前置）
- ⚠️ `UNITY_LICENSE` secret 未配置（用户已获个人授权，待手动配置）

---

## Director Panel Assessment（lean 模式下四门全跑）

| Director | Verdict | 核心依据 |
|----------|---------|----------|
| **Creative Director** | ⚠️ CONCERNS | G1(29/30 死亡豁免互斥) / G2(9 SelfLimited 未交付) / G3(OQ-10-13 未裁) |
| **Technical Director** | ⚠️ CONCERNS | F7 spike 未执行 / 焦点导航未实测 / CI 占位 / TR-concept-006 帧预算未定 |
| **Producer** | ⚠️ CONCERNS | Sprint 01 不可执行 / Milestone 1 两条出口永不可达 / 139 Ready 无排序 / 921 无产物 / stage.txt 超前 |
| **Art Director** | ⚠️ CONCERNS | Art Bible 完整到位 / 水墨×黄铜方向到位 / AB-6 待 40 考据 |

**Escalation rule**: 一 NOT READY ⇒ 终判最低 FAIL。本轮四门均为 CONCERNS，无 NOT READY。

---

## Verdict: CONCERNS（8 条，无阻塞级）

### 必须处理的落地缺口

| # | 缺口 | 归口 | 时限 |
|---|------|------|------|
| G1 | 29/30 死亡惩罚「判断类技能豁免」互斥 | 29 或 30 实现故事开工前裁 | 首批 |
| G2 | 9 的 `SelfLimited` 落点未交付 | 9 下一轮修订 | 首批 |
| G3 | OQ-10-13 未裁，学习闭环缺最后一环 | 42/44 确认呈现条目 | 首批 |
| F7 | IL2CPP 有符号溢出 UB spike 未执行 | ADR-012 F7 | 跨平台认证前 |
| 焦点导航 | ADR-013 assumption 6 未实测 | 桌面调试 | 39/43/48 实现前 |

### 生产管理待修

| # | 项 | 动作 |
|---|------|------|
| P1 | Sprint 01 非可执行形态 | 重做：日期区间 + 容量 + 依赖排序 |
| P2 | Milestone 1 出口 #4/#8 永不可达 | #4 收成「Mono 格绿 + UNITY_LICENSE」；#8 移入实现故事 BLOCKED-BY |
| P3 | 921 绿无 committed 产物 | 补全量 EditMode XML |
| P4 | stage.txt = "Production" 超前 | 回退到 `Pre-Production`，待本门 PASS 后由 gate-check 写 |

### 已接受的风险

- R-9（31 systems × solo dev）— 用户显式接受
- ADR-013 assumption 6 缓办 — 用户裁定集中一轮桌面调试
- Vertical Slice 缺位 — gate-check 规则「Slice 未构建 → 降为 CONCERNS」
- 32 条 GDD 扫描未覆盖项 — 归实现期处理

---

## 排序策略裁定

用户裁定：**逐个消耗**（不做总账，按 epic 消化，范围风险归 R-9 Accepted）。

---

## 下一步

1. 处理 G1/G2/G3 三条落地缺口（首批）
2. 重做 sprint-01 + 修 Milestone 1 出口
3. 补 921 全量产物 + 回退 stage.txt
4. 配置 `UNITY_LICENSE` GitHub secret（用户手动）
5. Pre-Production 阶段开始实现不依赖 pending spike 的系统
