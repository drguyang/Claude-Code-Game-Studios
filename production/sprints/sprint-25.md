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
| **S25-6** | `-tutorialdiag` 走查仪表(+ 深夜追加 `-tutorialnarrcheck` 口述复查旗标) | S | 走查只能在 16×16 图里盲走;病人格是呈现态,**存档里读不到**。复查旗标是它的第二个面:脉案 UI 走不通时,用生产解析面直接回答「四条 cue 播得响吗」 | 旗标门控 + 默认零开销;真包定位到「E 没反应 = 没走到跟前」;**四条口述互相关 0.88–0.998,全部出声** |
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

- [x] Must 3 件交付(`[L]` / `BLOCKED-BY` 半随如实标 `NOT-RUN`,**不借绿**)—— 2026-11-07,见 §收口 一
- [x] QA 计划存在(`production/qa/qa-plan-sprint-25-2026-11-06.md`)
- [x] 每件至少一条「改坏生产 ⇒ 夹具转红」的证伪轮,还原 SHA-256 逐字节 —— 见 §收口 二
- [x] 回归三元组(EditMode / PlayMode)写入收口节 —— 见 §收口 三
- [x] `python3 tools/debtcount/debtcount.py --check` exit 0 —— 条目 200 · 未闭 P0 面 15
- [x] `python3 tools/acid/acid_check.py --check` exit 0
- [x] `processing/story-005` 状态漂移已订正(卡面 `Complete`,与 S25-3 对齐)
- [x] GDD `tutorial-and-onboarding.md` 的结案注已落(原文不删)

> 🔁 **2026-11-07 收口轮**:上表八条逐条有载体。**未做的是「遗留」不是「判据」** ——
> 四条 `[L]` 走查 · smoke · 联机面 · S25-5 结转,全部列在 §收口 六,不得据本节读成「全部做完」。

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

---

## 收口(2026-11-07)

> **Status**: **Closed with carryover** —— **Must 3 件全部交付**;Should 2 件中 S25-4 交付、
> **S25-5 整件结转 sprint-26**;计划外追加 3 件同日完成(超支 2,见 §七)。

### 一、逐件判定

| ID | 件 | Verdict | 判据 / 残留(如实) |
|---|---|---|---|
| **S25-1** | `tutorial-002` 只读投影 + 装配护栏 | **COMPLETE WITH NOTES** | `AC-48-03` 存档字节逐字节 · `AC-48-04` 递归反射(负向夹具证明会红)· **`AC-48-11` 补构建期门 `b2-exact`**(D1 探针 / P2 真负例 / N 编译负例三条观测);**残留**:联机 `BLOCKED-BY-45` · 7a checkpoint 回执口零生产实现 |
| **S25-2** | `tutorial-003` Anchor/Completion | **COMPLETE WITH NOTES** | 18 条判据;原两条「未做」(零生产调用点 / 播种时机)由 **S25-4c** 关闭(`BootRoot:848` → `TutorialChainDriver` 构造期 `Seed`);**残留**:联机 `BLOCKED-BY-45` · `archive` 步依赖 7a 回执(恒 false) |
| **S25-3** | 炮制屏收口 | **COMPLETE WITH NOTES** | 判据 5 → **13** 条全绿;真包「开屏落焦 → 二段 → 落火留屏 → 3 s 后产物入账 → 空态」;**残留**:「无方可起」真包未复现 · `AC-18-24` 手柄走查 · `AC-42-F1` ⑨ |
| **S25-4** | `tutorial-004` 三媒体调度 | **COMPLETE WITH NOTES** | **口述一路闭环**:真包字幕上屏(= 事件表口径)+ 音频互相关 0.6032;跨表文案门 `TD-tutorial-01` 结案。**另两路的生产触发面不存在**:示范 `BLOCKED-BY` 13+42、纸 `BLOCKED-BY` 6+4;`AC-48-06`/`AC-48-15` 的 `[L]` 走查对象不全 |
| **S25-5** | `tutorial-005` 零设门守卫 | **CARRYOVER → sprint-26** | 未开工(`Status: Ready`);自动化守卫可先行,`[L]` 走查依赖 `OQ-C4` 三媒体节奏 spec |
| **S25-6** | `-tutorialdiag` 走查仪表 | DONE | 旗标门控、默认零开销 |
| **S25-7** | 教学口述声源格恒为原点 | DONE | 两环同修;增益 0.0216 → 0.6631(净恢复约 30 dB) |
| **S25-8** | 模态解冻补跑教学边沿 | DONE | 同 tick 幂等有夹具;归因订正:体感 2.4 s 是**冻结本身** |

### 二、证伪轮(逐件,还原 = SHA-256 逐字节)

| 件 | 变异 | 命中 |
|---|---|---|
| S25-1 | M1–M5(作者轴 / archive 回执 / 空集真 / 析取支 / 禁引词表) | **5/5** |
| S25-1 | D1 构建探针 + P2 真负例(asmdef 加 `Sim.Contracts`)+ N 编译负例 | **3/3**(构建 `Failure` × 2 · `CS0234` × 1) |
| S25-2 | M1–M4(播种跑 Fire / 已闩复燃 / 串玩家 / 播种不置闩) | **4/4** |
| S25-3 | M1–M4(落焦 / 二段 / 产物入账 / 空态) | **4/4** |
| S25-4 | m1–m6(步序 / `Order` / 键回查 / 字幕成对 / 双路 / 纸屏页码) | **6/6**(首轮 m1/m2 假存活,判据已补) |
| S25-4d | M1(停用 C3 比较)· M2(删空集守卫) | **2/2** |

### 三、回归三元组(**终树**)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | **`7076 / 7039 / 0 / 0 / 37`** | `2026-11-07T07:20:04Z` | sprint-25 收口轮 | `27bced91` |
| PlayMode | **`364 / 360 / 0 / 2 / 2`** | `2026-11-07T07:21:12Z` | 同批 | `27bced91` |

**等式判据**:`7076 − 7067 = 9` = S25-4d 新增的跨表门夹具;PlayMode `364 = 364`。
⚠️ **commit 的读法**:测跑发生在提交**之前**,当时内容已在工作树;`git add -A` 后 `git status` 为空
⇒ 提交内容与测跑内容**逐字节相同**。中间那次 `7066 / 7028 / 1` 是 `gate_wiring_manifest` 抓到
「新接线未声明」—— 门在正常工作,处置为补声明。

```bash
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s25-4-em-final.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s25-4-pm-final.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
```

### 四、门

| 门 | 结果 |
|---|---|
| `python3 tools/debtcount/debtcount.py --check` | **exit 0**(条目 200 · 未闭 P0 面 **15**) |
| `python3 tools/acid/acid_check.py --check` | **exit 0** |
| `PlayerBuildCli.Build` | **Success**(`Logs/s25-4-build-final.log:8559`,起始 `2026-11-07T07:15:11Z`) |
| `/smoke-check sprint` | **未跑** —— 不声称 smoke 已过;`smoke-2026-11-06.md` 仍是 `PASS WITH WARNINGS`(`run_smoke.py` exit 3,3 条无载体) |
| CI | **零运行**(`TD-ci-03` P2)⇒ **本 sprint 不得在任何地方声称「CI 绿」** |

### 五、债面变化

| 项 | 开工(2026-11-06 规划时) | 收口(2026-11-07) |
|---|---|---|
| 条目 | 199 | **200** |
| closed / partial / open / done | 166 / 9 / 23 / 1 | **168 / 9 / 22 / 1** |
| **未闭 P0 面** | 16 | **15** |
| epic 分布 | — | 7/31(不变) |

复算:`python3 tools/debtcount/debtcount.py --print`。

- **结案 2 条**:`TD-skeuo-21`(S25-3 开屏落焦)· `TD-tutorial-01`(S25-4d,当日立当日结)。
- **本 sprint 未留下任何未闭新债** —— 唯一新增的 `TD-tutorial-01` 当天结案。

### 六、仍未做 / 结转(**禁止读成「已全通」**)

1. **S25-5 整件** → sprint-26(零设门守卫 + `AC-48-07` 无视教学零惩罚走查)。
2. **三媒体只通了一路**:口述 ✅;示范 = 13 片段资产 + 42 播放口皆无;纸近景 = `OpenModal(PaperCloseup48)`
   全仓**零生产调用点**(`BLOCKED-BY` 6 + 4)。⇒ 「教学会说话」成立,「教学会演、会摊纸」不成立。
3. **所有联机面** `BLOCKED-BY-45`(S25-1 / S25-2 / S25-4 各有一条)。
4. **四条 `[L]` 未跑**:`AC-48-06` / `AC-48-15`(走查对象不全)· `AC-18-24` 手柄走查 · `AC-42-F1` ⑨。
   **不代签**。
5. **`archive` 步生产恒不成立**:7a 无「已存过档」读面,`checkpointWritten` 传 `null`(不伪造恒真回执)。
6. **smoke 未跑**;`walkthrough-s25-4c` 的 §8 是**口述一路**的真包走查,**不是**三媒体走查。

### 七、产能(承 `production/CAPACITY-MODEL.md` §3.3)

| 元 | 值 |
|---|---|
| 计划件 | 5(Must 3 / Should 2)⇒ 完成 **4** · 结转 **1** |
| 计划外追加 | **3**(全 S,全部同日完成)⇒ 额度 1,**超支 2** |
| 复杂度分布 | M×4 · L×1(计划内) |
| 追加率 | 60% |

⚠️ 追加 3 件全是「走查手段不足」连带查出的缺口(S25-6 是工具,S25-7/8 是它查出的两个缺陷),
不是新特性 —— 但**按口径仍计超支**,作复盘素材。

---

## 收口后追加件(2026-11-07 · 承用户「继续完成」的三条遗留)

> 本节**不计入**本 sprint 的计划件数(与 sprint-24 的「收口后追加件」同口径)。

### 一、`/smoke-check sprint` 首次真跑

```bash
python3 tests/smoke/run_smoke.py --results unity/Logs/s25-4-em-final.xml unity/Logs/s25-4-pm-final.xml
```

| 读数 | 值 |
|---|---|
| 合计 | 22 条 |
| 自动 PASS | **16** |
| 自动 FAIL | **0** |
| 未跑(需结果件) | 0 |
| 具名人工 | 3(#19 / #20 性能与延迟、#3 启动序人看) |
| **无载体(无名)** | **3**(#9 / #18 / #22) |
| 退出码 | **3 = INCOMPLETE** |

⚠️ **这不是 PASS**:`exit 3` 按 skill 与 `TD-smoke-01` 的口径是「无失败,但有具名无载体条目」。
`smoke-2026-11-06.md` 的 `PASS WITH WARNINGS` 结论不变。

### 二、#9 查清:**前件不存在**,不是「没人写测试」

原登记理由写「需 42 接真 DTO 的运行时观测量」。**该判断是错的**(至少不完整)。

- 42 的画法**是完整的**:`CasebookScreen.ApplyData` → `SignChannelBinder.Bind` → `.channel-reading`;
- 真正缺的是**它的前件** —— 9 侧产出的体征。`ActiveSignCount > 0` 那道门今天永不放开;
- **实测**(PlayMode `casebook_entry_wiring_test.test_ce_05_casebookReadings_tripwire_signsAreStillEmpty`):
  把 8 个查体通道**全部**查一遍之后,分册快照 `SignCount` 仍 = **0**
  ⇒ `DiseaseVitalsService` 恒 `mask = 0`(`CasebookScreen.cs:750` 的注释已写「tripwire 语义」)。

处置:
1. 新增用例 = **跳闸线** —— 断言当前确实为零。**9 侧一旦产出体征它立即转红**,
   届时改成正向判据(断言读数出现且来自 DTO)并把 `critical_paths.json` #9 改成 `test`;
2. `critical_paths.json` 的 #9 `reason` 就地订正(状态仍 `none` —— 今天确实没有可执行正判据);
3. #18 / #22 维持 `none`:两者都是 **ADR-012 跨构建矩阵**,`TD-ci-01` EXTERNAL(凭证在用户侧)。

### 三、另两条遗留的**结论**(不是「没做」)

| 遗留 | 实测结论 | 缺什么 |
|---|---|---|
| 示范媒体 | `Presentation` / `UI` 里搜 `Demonstration|SegmentKey|clipKey`(排除 Tutorial 自身)**零命中** ⇒ 42 **没有一次性片段播放口** | 13 的片段资产 + 42 的播放口 |
| 纸近景 | `p_wangzhen` 只存在于 `48_tutorial_content.json`;**全仓无任何世界锚点**;`OpenModal(PaperCloseup48)` 零生产调用点 | 6 的纸道具格 + 4 的走近判定 + 插图资产 |

⇒ 这两条**不是补几行代码就能通**的:`纸` 缺的是**内容**(道具摆在哪一格),`示范` 缺的是**资产**。
在没有这两样之前把机制焊上去,只会得到一个「点不到」或「看不见」的功能。

### 四、回归三元组(本节)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| PlayMode | **`365 / 361 / 0 / 2 / 2`** | `2026-11-07T13:43:00Z` | 收口后追加件 | 工作树(`ed5baa3c` 之上) |
| EditMode | **`7076 / 7039 / 0 / 0 / 37`** | `2026-11-07T13:46:21Z` | 同批 | 同上 |

**等式判据**:PlayMode `365 − 364 = 1` = 新增的跳闸线用例;EditMode 不带改动(零回归)。

### 五、资产登记补登(2026-11-07 · 用户追问「缺的资产登记了吗」)

**答:此前只登记了「归属」,没登记「资产条目」。** 逐条核后补三处:

| 面 | 补了什么 |
|---|---|
| `design/assets/entity-inventory.md` | §Props 新增 **#9 教学纸·世界道具**;新增 **§Animation Clips(6)**(6 个 `mentor_*` 示范片段);Summary `Props FP 7→8` · 新增 Animation 6 · **FP 62→69 / Total 104→111** |
| 同上 · §Spec Files Index | 加缺口注:三族**资产条目已补,spec 仍未出** |
| `production/registration-debt-register.md` §E | 新立 5 条:`TUT-资产-1/2/3`(6 片段 · 纸道具 · 纸插图)+ `TUT-接口-1/2`(42 播放口 · 纸近景生产开口) |

**登记前的实况(逐条实测,不是印象)**:

| 缺的东西 | 之前登记在哪 | 资产册? | spec? | 批次? |
|---|---|---|---|---|
| 6 个示范片段 | 只有 `48_demonstration_clips.json` 的**键名**(自陈「只登记键名与片段语义」) | ❌ | ❌ | ❌ |
| 纸·世界道具 | 只在 `paper-closeup-48.md:238` 记「归 6 布置轮」 | ❌ | ❌ | ❌ |
| 纸·插图 | 只在 UX spec `:237` 记「归 art,AB §8.2 图集」 | 随图集走,不单列 | ❌ | ❌ |
| 42 one-shot 播放口 | 只在 story-004 的 `BLOCKED-BY` 注 | —(接口非资产) | ❌ | — |

⚠️ **补登 ≠ 补齐**:资产文件、spec、批次三样仍未出;`AC-48-06` / `AC-48-15` 的 `[L]` 走查**仍然跑不了**。
