# Sprint 25 — 2026-11-06 to 2026-11-12

> **Status**: **Planned**(2026-11-06 · 用户指令「结合未完成的内容,开启 sprint25」)
> **件集裁定**:用户 2026-11-06 选「**照此定稿 + 48 GDD 结案**」—— 教学链 4 件 + 炮制屏收口 1 件
> **前置**:`sprint-24` 已收口(2026-11-03,见 `sprint-24.md`);其「收口后追加件」三批已登记
> **QA 计划**:开工后立即跑 `/qa-plan sprint`(用户 2026-11-06 裁定);本文件成文时**尚无** QA 计划
> 🔁 **2026-11-06 晚订正**:QA 计划**已产出** = `production/qa/qa-plan-sprint-25-2026-11-06.md`。
> 上行原文不删 —— 成文时确实没有,它是成文之后才跑出来的。

## ⚠️ Phase 1.5 复核(2026-11-06 · 开工前必做)

| # | 发现 | 处置 |
|---|---|---|
| 1 | 路线图 `p0-remaining-roadmap:952` 的 sprint-25 行 = `tutorial-002…005`,并注「002/003 挂 `BLOCKED-BY-45`」 | **该挂标只覆盖每件里的一条「联机子项」**(002 `:38` / 003 `:39`),不是整件阻塞 —— 两件卡面均 `Status: Ready`,单机双 id 夹具合法 ⇒ **可以开工,联机子项如实 `NOT-RUN`** |
| 2 | `design/gdd/tutorial-and-onboarding.md` 是**唯一一份仍 `In Review` 的 P0 GDD**(其余 processing / case-system / inventory-and-items / skeuomorphic-ui 均 `Approved`);自 2026-09-19「10 项阻断当日全落盘 → 待用户裁定结案」挂账至今 | **用户 2026-11-06 裁定结案** ⇒ 同批把 GDD `Status` 改 `Approved` 并留日期注(原文不删)。本 sprint 的教学四件以此为开工前提 |
| 3 | `processing/story-005` 卡面 `Status: In Progress`,而 `sprint-status.yaml` 与卡面 Completion Notes 均为 `COMPLETE WITH NOTES` | **状态漂移** ⇒ 同批订正卡面为 `Complete`(不改任何 AC 面) |
| 4 | 路线图的 sprint-25 行**不含**炮制链收口 | 本 sprint **增列** S25-3(来源 = 2026-11-06 的未完成清点,见下「Why S25-3 is here」) |

### Why S25-3 is here(路线图未列的增件依据)

2026-11-06 的干净包走查已证「采集 → 容器选料 → 灶台屏出行」可走,但**点火之后的半程仍不可走**:
`design/ux/processing-18.md` 成文后暴露出**四条实现差**(二段确认 / 三种空态注 / 开屏落焦 / 点火后留屏),
其中**开屏落焦**已立债 `TD-skeuo-21`。炮制是 P0 七项熟练度之一,链在半程断着 = 支柱一在制程侧落空。
⇒ 它不是新特性,是**未完成内容的收口**,故进 Must。

---

## Sprint Goal

**把「能打开」的炮制链推到「能走通」,并落地教学链的前两件**(48 GDD 于本轮结案)。

## Capacity(按 `production/CAPACITY-MODEL.md` §3.2,**不估天数**)

| 元 | 值 |
|---|---|
| 计划件 | **5**(Must 3 · Should 2 · Nice 0) |
| 计划外追加(🔁 2026-11-06 晚) | **3** 散件(S25-6/7/8,均 S)⇒ **追加率 60%**,额度 1 ⇒ **超支 2** |
| 复杂度分布 | **M 4** · **L 1** |
| 并发上限 P | **3** ⇒ 本轮最多 3 路并行(教学链 002→003 是串行,炮制收口独立) |
| 追加额度(§3.3) | 散件 **1** + 链条 **1** |
| 判别力门槛 D | 每件须有「改坏生产 ⇒ 夹具转红」的**可证伪判据**,否则不计 done |

## Tasks

### Must Have

| ID | 任务 | Type | 复杂度 | 掀债 | 掀 story | 链 | 验收判据 |
|---|---|---|---|---|---|---|---|
| **S25-1** | `tutorial-002` 只读投影 Proj 与装配护栏 | Logic | M | 0 | 0 | 教学链 | 6 条 AC 各有载体;`AC-48-11` 双守(反射递归 + asmdef 白名单);**联机子项 `NOT-RUN`**(不借绿) |
| **S25-2** | `tutorial-003` Anchor/Completion 两相触发引擎 | Logic | M | 0 | 0 | 教学链 | `AC-48-08` 冷启动播种夹具;可跳步 / 可回头 / 不补播三条;闩**不可序列化**(类型断言) |
| **S25-3** | 炮制屏收口:`processing-18.md` 落地 + `TD-skeuo-21` 结案 + 真包点火→出丹 | Integration/UI | M | 0 | 0 | 炮制链 | spec §13 的 12 条 `[A]` 有载体;每条至少一条证伪变异;真包「点火 → 完成 → 起货」可见;`[L]` 半随如实 `NOT-RUN` |

### Should Have

| ID | 任务 | Type | 复杂度 | 掀债 | 掀 story | 链 | 验收判据 |
|---|---|---|---|---|---|---|---|
| **S25-4** | `tutorial-004` 三媒体调度与纸近景模态 | UI | **L** | 0 | 0 | 教学链 | 口述 / 示范 / 纸三路各有载体;近景模态走焦点单栈门;字幕子项若 `AC-44-15` 未就位 ⇒ **标 `BLOCKED-BY` 并如实登记** |
| **S25-5** | `tutorial-005` 零设门与零按键提示守卫及走查 | Integration | M | 0 | 0 | 教学链 | `AC-48-17` 反射断言(公开 API 面零 gate 类查询);禁止形态表全项扫描;`AC-48-05/07` 的 `[L]` 未跑即 `NOT-RUN` |

### Nice to Have

无 —— 承 §3.2「L 件单独占批」,不凑件数。

## 计划外追加件(2026-11-06 晚 · 用户当轮直接指派)

> ⚠️ 口径承 `production/CAPACITY-MODEL.md` §3.3:**散件追加额度 = 1**,本轮实际 **3** ⇒ **超支 2**。
> 三条都不是新特性,是**走查手段不足**连带查出来的缺口 —— 第一条补工具,后两条是它查出的缺陷。
> 详细登记见 `production/sprint-status.yaml` 的 S25-6/7/8。

| ID | 任务 | 复杂度 | 为什么进本轮 | 判据 |
|---|---|---|---|---|
| **S25-6** | `-tutorialdiag` 走查仪表 | S | 走查只能在 16×16 图里盲走;病人格是呈现态,**存档里读不到** | 旗标门控 + 默认零开销;真包里靠它把「E 没反应」定位到「没走到跟前」 |
| **S25-7** | 教学口述声源格恒为世界原点 | S | 口述出声但被空间化压低 **-33.3 dB**;根因两环,只修一环是假修 | 真实录音互相关 0.9199;增益 0.0216 → 0.6631(净恢复约 30 dB) |
| **S25-8** | 模态解冻补跑教学边沿 | S | 冻结期间进流的 `CaseOpened` 没有 tick 边沿可走 | 解冻与发火同一帧且只发一次;同 tick 幂等有夹具 + 证伪轮 |

⚠️ **S25-7 的教训写进本轮复盘素材**:第一轮只补听者缝,重跑增益**逐位不变**(0.0216→0.0218),
若不插仪表就会把假修当已修。同族形态见 `coding-standards.md` 假绿登记表 F6(自述顶替执行体)。

## Carryover from Previous Sprint

| 任务 | 原因 | 备注 |
|---|---|---|
| 炮制链后半程(点火 → 完成 → 起货) | S24-1 收口时「如实登记的三处没做完」之一;2026-11-06 走查推进到「灶台屏出行」即止 | 本轮 S25-3 收口 |
| `AC-42-F1` ⑨ 走查 | 无 UX spec 的阻塞已解除(2026-11-06 成文),但**未经 `/ux-review` + 手柄走查未执行** | S25-3 收口后仍须 `[L]`;不在本 sprint 的 Must 判据内 |

## Risks

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| **48 GDD 结案后仍有未落地的裁定**(10 项阻断虽当日全落盘,但未再经 `/design-review` 复核) | 中 | 中 | 结案注写明「结案 = 用户裁定,不重开评审」;实现中若撞裁定冲突 ⇒ 停手登记,不自行改 GDD |
| **`tutorial-004` HIGH engine risk**(近景模态焦点桥 / 字幕管线) | 中 | 高 | `paper-closeup-48.md` 已 Approved;焦点落点抄 `TD-skeuo-20` 的修法(按 id 还原 + 不悬空) |
| **炮制 `[L]` 端到端需要 `duration_ticks` 的真实值**(数值归用户数值轮) | 中 | 中 | 真包只断「调度落点 + 起货可见」;数值未定时用表内最短时配方 |
| 回归数与日前基线混批 | 低 | 中 | 收口必须带三元组(截至时刻 · 批次 · commit),且**等式判据** `total − 前次 = 新增测试数` |
| CI 仍零运行(`TD-ci-03`,P2) | 确定 | 低 | 本 sprint 不得在任何地方声称「CI 绿」 |

## Dependencies on External Factors

- **S25-1 / S25-2 的联机子项** = `BLOCKED-BY-45`(P1b;单机双 id 夹具**不替代**该 closure,禁借绿)
- **S25-4 的字幕子项** = `AC-44-15` 管线(若未就位 ⇒ 以「字幕接口 + 占位呈现」交付并标 `BLOCKED-BY`)
- **S25-5 的走查** = 需 S25-3 的链路可走(`OQ-C4` 三媒体节奏 spec 仍欠 ⇒ 走查可标 `BLOCKED-BY-SPEC`)

## Definition of Done for this Sprint

- [ ] Must 3 件交付(`[L]` / `BLOCKED-BY` 半随如实标 `NOT-RUN`,**不借绿**)
- [ ] QA 计划存在(`/qa-plan sprint` 产出 `production/qa/qa-plan-sprint-25-*.md`)
- [ ] 每件至少一条「改坏生产 ⇒ 夹具转红」的证伪轮,还原 SHA-256 逐字节
- [ ] 回归三元组(EditMode / PlayMode)写入收口节
- [ ] `python3 tools/debtcount/debtcount.py --check` exit 0
- [ ] `python3 tools/acid/acid_check.py --check` exit 0
- [ ] `processing/story-005` 状态漂移已订正
- [ ] GDD `tutorial-and-onboarding.md` 的结案注已落(原文不删)

## 复算(自包含,只读)

```bash
# 件面:五个载体的 Status 行
grep -m1 "Status" production/epics/tutorial-onboarding/story-00{2,3,4,5}*.md
grep -m1 "Status" production/epics/processing/story-005*.md

# 债面(开工基线)
python3 tools/debtcount/debtcount.py --print
# 2026-11-06 规划时实测:条目 199 | closed 166 | partial 9 | open 23 | done 1 | 未闭 P0 面 = 16

# GDD 结案状态
head -4 design/gdd/tutorial-and-onboarding.md
```
