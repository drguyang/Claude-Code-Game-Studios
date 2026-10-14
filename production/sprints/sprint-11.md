# Sprint 11 — 判断回路前台化 + 治理收口(2026-10-14 规划)

> **规划依据**:三专家研判(技术总监 / 创意总监 / QA 负责人,2026-10-14 Opus 级并行研判)
> **头条订正**:四系统(13/8/37/11)epic 均已于 2026-10-06→10-08 story-Complete ——
> M2 README「16/31」为 2026-10-03 旧快照,实测 ≈**22/31 epic Complete**。
> **sprint-11 实为「补体验 + 治理收口」,非「照 GDD 实现新系统」。**
> **用户裁定(2026-10-14)**:范围 = 草案全收(Must×3 + Should×1 + Nice×2);
> S11-3 处置方向 = **修 8 测**(CI 门保持「退出码 = 0」严格口径)。

## Sprint Goal

把判断回路从「流里通」变成「玩家眼前通」——9 查体 revealed 接线让脉案值区运行期非空
(支柱一「判断为骨」首次可玩),同时清掉三笔承重治理债(W-1 会签 / 退出码 2 / 处方 DoD 残留)。

## Capacity

- Total days: 8(solo)
- Buffer (20%): 1.6 天
- Available: ≈6.4 天(草案 8-11pd,超容部分自然顺延 sprint-12)

## Tasks

### Must Have (Critical Path)

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S11-1 | **9 查体 revealed 接线 → 8 前台回路**(脉案值区运行期非空;顺手结 O-13-7 `OnExamSessionChanged` 发出侧登记) | unity-specialist + Claude | 2-3 | M2 链已闭(e2e_full_loop 绿) | 脉案值区运行期显示查体产出(`RevealedRows` 非空);`e2e_full_loop` + 五 wiring 测零回退;O-13-7 结案登记 脉案值区运行期显示查体产出(`RevealedRows` 非空);`e2e_full_loop` + 五 wiring 测零回退;O-13-7 结案登记 | **QA**:Integration 测 `examine_revealed_wiring_test`(~4-6 测)+ 人工连走(查体→显示→落笔→开方) |
| S11-2 | **W-1 会签裁决**(ScriptedChain 三案链 9+37 二选一;图样可辨识度 × 8 可读性互斥性验证) | game-designer + user | 0.5 | 无(纯决策轮) | 裁决落盘(case-system.md W-1 行 + 决策记录);「不裁不得实现联调」解除;若裁互斥 ⇒ 支柱四 P0 落点重裁 裁决落盘(case-system.md W-1 行 + 决策记录);「不裁不得实现联调」解除;若裁互斥 ⇒ 支柱四 P0 落点重裁 | **QA**:会签记录(互斥性可证伪结论;user+designer+qa-lead 三签) |
| S11-3 | **EditMode 退出码 2 处置 = 修 8 测**(8 个旧测试 `new MonoBehaviour` ⇒ 真夹具) | Claude | 0.5-1 | 无 | 全量 EditMode **退出码 = 0** 且 XML failed=0 双达标;判决口径在 sprint 文件登记(两信号不再混读) 全量 EditMode **退出码 = 0** 且 XML failed=0 双达标;判决口径在 sprint 文件登记(两信号不再混读) | **QA**:XML 实证订正 = 7 测/2 文件/8 处 new(host_authority + o6_actor_cell_payload);纯测试面修复,禁动产品代码 |

### Should Have

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S11-4 | **prescription DoD 残留**(AC-11-22 7a 白名单本体 + DC-2 `action_id` master 登记(OQ-11-2) + DC-6 `NOISE_BAND_9` 复评——9 侧常量 10-07 已立,OQ-11-13 10-06 已裁,前置已解锁) | Claude | 3-4 | S11-3 顺(同仓测试基线) | prescription EPIC §Epic Status 三项逐项闭合或显式结转;若动 7a 须带 case↔7a 折叠语义集成夹具(TD 风险 1 缓解) prescription EPIC §Epic Status 三项逐项闭合或显式结转;若动 7a 须带 case↔7a 折叠语义集成夹具(TD 风险 1 缓解) | **QA**:EditMode 断言 ×3(AC-11-22 IL 扫描/DC-2 闭集 fail-loud/DC-6 引用链闭环) |

### Nice to Have

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S11-5 | **53 medical-consequences 起首 story**(支柱四载体,机械自足 P0 水龙头,纯 C# + SplitMix64,Engine Risk LOW) | Claude | 1-2 | 9+37 已 Complete ✓ | story 立件 + 判据面成型;不强求当轮完成 story 立件 + 判据面成型;不强求当轮完成 | **QA**:随立件定测试路径(Logic 级) |
| S11-6 | **11 人工走查 AC-11-12/13/21**(前置:先修 Story009WalkthroughDriver `passed` 汇总 bug) | user + Claude | 0.5-1 | Driver bug 修复 | 三 AC 走查证据落 playtests/;Driver 汇总判定修复登记 三 AC 走查证据落 playtests/;Driver 汇总判定修复登记 | **QA**:人工走查三签(主创/医学/音频);Driver passed 汇总修后自证 |

## Carryover from Previous Sprint

| Task | Reason | New Estimate |
|------|--------|--------------|
| 用户数值轮(8 门槛族 / 37 半径/就诊触发 / 11 EFF 逐档 / 13 分档) | 承「数值用户自己调」——四系统旋钮批成一轮,不逐一阻塞 | 未估(用户轮) |
| story-008 资产半(止血/CPR 素材 + 手势 VFX) | BLOCKED-BY batch-03 外部链,维持 P2 | — |
| 六屏 UX 走查余五屏 | 桌面集中轮缓办(AC-42-F1 NOT-RUN 已登记);脉案屏随 S11-1 顺带 | — |
| B-6-2 焦点可见性(独立 story) | sprint-08 唯一 open,未入本轮排期 | — |

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| case↔7a 折叠语义集成未验证(TD 风险 1,LIVE) | 中 | 存档正确性/高水位重构 | S11-4 动 7a 必带集成夹具(未结案存档折叠 + 三流并集 max 验证) |
| UI Toolkit 焦点桥假设 6 半可信 | 中 | S11-6 走查面失真 | 走查前小 spike;失败则登记另裁 |
| case-system 001-004 无评审原件 | 低 | 治理缺口(不可追补条款) | QA 计划前置批核:核当年有无 BLOCKING 判定,有则补做评审,无则登记结论 |
| W-1 裁决出「互斥」 | 低 | 支柱四 P0 落点重裁(设计层) | S11-2 预案:互斥则升级用户裁定,不擅自改设计框架 |

## Dependencies on External Factors

- 无外部阻塞(ADR-023 S1-S7 与 45 联机夹具经 grep 证实对四系统 P0 机械面**零阻塞**——TD 研判 C 节)
- batch-03 外部素材链(story-008 资产半)持续 BLOCKED,不在本轮解除面

## Definition of Done for this Sprint

- [ ] All Must Have tasks completed
- [ ] All tasks pass acceptance criteria
- [ ] QA plan exists (`production/qa/qa-plan-sprint-11.md`)——**开工前必落**
- [ ] All Logic/Integration stories have passing unit/integration tests
- [ ] Smoke check passed (`/smoke-check sprint`)
- [ ] QA sign-off report: APPROVED or APPROVED WITH CONDITIONS (`/team-qa sprint`)
- [ ] No S1 or S2 bugs in delivered features
- [ ] Design documents updated for any deviations
- [ ] Code reviewed and merged
- [ ] **M2 链零回退**(sprint-11 特有):e2e_full_loop + vertical_slice + 五 wiring 测全绿——四系统正压在已闭 M2 Exit 链上,破链 = 重开 M2
- [ ] **NOT-RUN 显式结转**(sprint-11 特有):未闭 AC(AC-11-12/13/21 · case W-1 后续 · story-005 等)逐项带状态+归属,禁借绿
- [ ] **退出码口径结清**(sprint-11 特有):EditMode 判决以「退出码 + XML failed」双信号一致为准,S11-3 后两信号不得再混读

## 三专家研判归档

- 技术总监:四系统全 Complete 的头条订正 + 轨 A 治理/轨 B 53 双轨建议 + 风险 top3(7a 折叠/W-1/处方 DoD 歧义)
- 创意总监:8 查体前台回路为体验主轴(`RevealedRows` 恒空 = 支柱一不可玩)+ W-1 互斥性承重债 + 体验债分级进
- QA 负责人:测试债清单(退出码 2/Driver bug/case 评审原件核查)+ story-009 当日模式 + 整 sprint 一份 QA plan + DoD 三条特有判据
