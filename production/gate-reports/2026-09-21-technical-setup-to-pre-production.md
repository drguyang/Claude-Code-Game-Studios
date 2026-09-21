# Gate Check: Technical Setup → Pre-Production

**Date**: 2026-09-21
**Checked by**: gate-check skill(review mode = **lean** —— `production/review-mode.txt` 不存在 ⇒ 默认;
四门全部实跑,TD-MANIFEST 在 lean 下跳过)
**Verdict**: **FAIL → 用户显式承接 → CONCERNS**(见 §Verdict 与 §CD 承接)

---

## Required Artifacts: 13/13 present

| # | 判据 | 实测 |
|---|------|------|
| 1 | CLAUDE.md 技术栈非 `[CHOOSE]` | ✅ Unity 6.3 LTS / URP / OpenXR |
| 2 | `.claude/docs/technical-preferences.md` 已填 | ✅(含 20 Hz / CAP 24 / DOTS 门三项 2026-09-20 裁定) |
| 3 | `design/art/art-bible.md` §1–4 | ✅ 373 行,§1–4 成稿;**§5–9 刻意留空**(本门只要求 §1–4) |
| 4 | ≥3 ADR 覆盖 Foundation(场景管理 / 事件架构 / 存档) | ✅ **22 份**;ADR-023(场景)/ 005+008+009(事件)/ 010(存档) |
| 5 | `docs/engine-reference/unity/` | ✅ VERSION + breaking-changes + deprecated-apis + modules/ |
| 6 | `tests/unit/` + `tests/integration/` | ✅ 存在(`/test-setup` 已跑) |
| 7 | `.github/workflows/tests.yml` | ✅ 存在 |
| 8 | 至少一个示例测试文件 | ✅ `tests/unit/sim/sim_fixedpoint_test.cs` ⚠️ **不被编译**(见 Q-4) |
| 9 | `docs/architecture/architecture.md` | ✅ v1.0+ |
| 10 | `docs/architecture/requirements-traceability.md` | ✅ |
| 11 | `/architecture-review` 报告存在 | ✅ 两份(`architecture-review-2026-09-20.md` 为最新) |
| 12 | `design/accessibility-requirements.md` 有档位 | ✅ Tier = **Standard** + L-1/L-2 |
| 13 | `design/ux/interaction-patterns.md` | ✅ 155 行 |

## Quality Checks: 8/10 passing(修判据后)

- ✅ 架构覆盖渲染 / 输入 / 状态管理 · ✅ 命名约定 + 性能预算已设(标「临时值」见下)
- ✅ 无障碍档位已定 · ❌ **至少一个界面的 UX spec 未开工**(`design/ux/` 只有模式库,无 per-screen spec)
- ✅ 22/22 ADR 均有 Engine Compatibility 段并钉版本 · ✅ 22/22 均有 GDD Requirements Addressed 段
- ⚠️ 性能预算 = **临时值**(Draw Calls / Memory Ceiling 待最低目标硬件)
- ⚠️ 弃用 API 检查:**UGUI 口径冲突** —— `deprecated-apis.md` 把 UGUI 列「仍支持但新项目建议 UI Toolkit」,
  而 ADR-013 明文 **UGUI 补 world-space / XR**;`deprecated-apis.md:28` 待回写(不是违反,是参考件未跟进裁决)
- ❌ **Foundation 层零缺口**:残 **2 条 ❌**(`TR-itemdb-031` / `TR-skill-008`)
  —— ⚠️ 本项计数于本日随门规格修订变化:原 4 条,其中 `TR-concept-003/004` 两条按新 ◆ 态摘除。
- ✅ ADR 依赖图**无环**(22 份 `Depends On` 拓扑排序通过;无悬空 `Proposed` 依赖 —— 22/22 均 `Accepted`)
- Engine Validation ≈ **2.5/3**:版本一致性 ✅ / post-cutoff 标注 ✅ / 弃用 API 见上 ⚠️
- Knowledge Risk 分布:**6 HIGH / 6 MEDIUM / 10 LOW**(HIGH 项均已有 spike 登记,非纸面)

### ADR 状态串(本日修)
`adr-023` 原串 `Accepted(附条件,见下)` 是 22 份中唯一非字面者 ⇒ `create-control-manifest` 的字面
过滤会**静默丢弃本件**(RC-2)。2026-09-21 用户裁定「归一」,**awk 实测现值 = 22/22 字面 `Accepted`**;
「附条件」含义只留正文,**非新裁决**。

---

## Director Panel Assessment(lean 模式下四门全跑)

| Director | 判词 | 一句话理由 |
|---|---|---|
| **Technical Director** | **CONCERNS** | 技术侧无 blocker;两处「判据 mis-specified」(Foundation ◆ 态 / RC-2)+ 三处登记层漏刷 |
| **Art Director** | **CONCERNS** | §1–4 达门判;AB-1…AB-10 待裁,§8 留空与 P0 基线结构性竞争(AB-10) |
| **Producer** | **CONCERNS** | 排期可行性成立但**估算零 velocity 数据**;留一道解释题(工装块是否占窗口)+ U0a 工具链是总前置 |
| **Creative Director** | **NOT READY** → **用户承接** | 两条支柱执行缺口(下节) |

**判词均为模型输出,不是用户批准。** 面板规则:任一 NOT READY ⇒ 终判最低 **FAIL**,
「user may override with explicit acknowledgement」。

### CD 两条与其漏读的下半句(本会话逐条复测,如实并陈)

| # | CD 的断言 | 复测结果 |
|---|---|---|
| 1 | `skill-system.md:552` 勾选项仍字面写「跳过与手动完成获得**同等经验**」,与 10 的 R-6 裁定冲突 ⇒ 支柱一被最优解反噬 | **事实成立**,但漏读紧邻注体:该注体**自陈**「须改口…待裁定」⇒ 实质 = 已登记、待裁改法,非未发现的矛盾 |
| 2 | `combat-and-weapon-lines.md:1218-1220` 的 `¬Down(g)` 在 9 的 `Down` 未落地时**退化为恒真**(fail-open),而支柱二靠它 | **事实成立**,但漏读 :1220 后半句「**须在实现顺序里登记为前置**」+ `OQ-25-3`(:1186)自陈「不阻塞本 GDD」;⚠️ **25 与 9 同程序集**(`adr-025:106` 自述)⇒ 构建顺序错位近乎不可能 |

## Verdict: **FAIL →(用户承接)→ CONCERNS**

**2026-09-21 用户裁定 = 甲案「承接 + 三条件」** ⇒ 按面板规则终判从 FAIL 落 **CONCERNS**
(残 CONCERNS = Foundation 2 ❌ + 预算/弃用 2 ⚠️ + per-screen UX spec ❌ + AB / RC 待裁族)。

### CD 承接的三条条件(用户照准的形态,逐条已落)

| 条件 | 落点 | 状态 |
|---|---|---|
| **① `OQ-10-12` 两动作手感原型门的限期收窄为「写 10 的第一行代码前」**,判据 = `L_input < 50 ms` 实测 + 抖动门 11× 分档;**不得判「手感好」**(数值归用户) | `emergency-procedures.md` `OQ-10-12` 行 | ✅ 已改限期列 |
| **② `skill-system.md` 的「同等经验」改口须先于任何 30 的实现故事** | `skill-system.md` :468 边例 / :552 勾选项 + 文件头 | ✅ 已就地改口(两处均留原文、零机制数值、**不记绿**) |
| **③ `¬Down(g)` 写成断言而非排期门** —— 把「实现顺序前置」升为**构建期断言**(9 的 `Down` 查询通道缺成员 ⇒ **构建失败**,不许 fail-open),`OQ-25-3` 保持开放 | `combat-and-weapon-lines.md` 规则十注体(:1221 起) | ✅ 断言口径已登记(**不是验收**;⚠️ 只锁「缺成员 = 编译不过」,**具名接口与签名归 9 的下一轮**) |

> ⚠️ 条件 ③ 是**新增的实现期义务**,不是「矛盾已消除」——它把静默失败改为响亮失败,
> 但 `Down` 的覆盖边界(含 D-13 苏醒态)仍归 9 侧确认,**OQ-25-3 未结案**。

---

## Blockers(承接后已非阻塞,但逐条有归口)

1. ~~CD 两条~~ → 用户承接 + 三条件落盘(上表)。
2. **Foundation 层 2 条 ❌** —— 均「有裁决面缺执行体」:
   `TR-itemdb-031`(随 ADR-009 的 TR 全量复核轮;本日已补 `adr: ADR-009 + ADR-015` 指针,**status 不翻**)、
   `TR-skill-008`(7a 逐字段,形状面 ADR-010 已覆盖,数值落地 = **Required ADR #4**,非开工阻塞)。
3. **`tests/unit/sim/sim_fixedpoint_test.cs` 不被编译** —— 无 `.asmdef`(`find` 实测 **0**),
   且**本机无 Unity / dotnet / mono** ⇒ 该示例测试**从未运行过**,「框架可用性」判据实为**未证**。
   解法 = U0(建六装配骨架)之后在 U0a 有编辑器的机器上跑绿。

## Recommendations(排期口径已裁定)

**用户裁定 = 工装块「占窗口」**:U0a + U0 + U1(估 18–33 pd ≈ 4–7 周)计入 `6-9 个月` 内 ⇒
**31 项的内容窗口 ≈ 4.3–8 个月**。⚠️ 这是**口径澄清,非重算**(2026-09-14 基线裁定不动);
已落 `systems-index.md` §P0 工期基线块。PR 的 18–33 pd 是**标称值**(零 velocity 数据),不作承诺。

开工序(全部在窗口内):
1. **U0a 工具链核验** —— Unity 6.3 编辑器 + license + **确切补丁号**;门一切实测。**手工配置 `UNITY_LICENSE` secret;不自动化 license。**
2. **U0 工程根 + ADR-025 六装配** —— 种子测试编译转绿 + `Sim` 引用集恰 = {BCL, `Sim.Contracts`} 的**构建失败级**断言。
3. **U1 spike 批** —— R-A 手柄焦点桥(ADR-013 假设 6,**最可能失败**)/ R-B 门 A / R-C `int64` 溢出 UB(ADR-012 F7)/ `OQ-1-12` 落地 / ADR-023 S1/S3/S4 / 10 的两动作原型(条件 ①)。
4. 之后才进 vertical slice。

**并行建议**:PR 指出 **21b 考据量补估**(0.5–1 pd)在本门是最后窗口(此门后无排期抓手);
以及 per-screen UX spec 首件建议选**能同时结 `OQ-42-5` / `OQ-48-8` 的那一页**。

---

## Chain-of-Verification

5 问,≥2 走工具(标 **[TOOL ACTION]**),逐条独立作答后复核终判:

1. **「我有没有把 MANUAL CHECK 项当 PASS 记?」** [TOOL ACTION]
   重扫清单 → **没有**:per-screen UX spec 记 ❌(非 [?]);性能预算记 ⚠️(临时值自陈);
   示例测试记 ⚠️「不被编译」。**A1**:未放宽。
2. **「`find *.asmdef` 真为 0?22 份 ADR 状态串真全字面 Accepted?」** [TOOL ACTION]
   重跑:`find` = **0**;awk 扫 Status 首非空行 = **22 × `Accepted`**(唯一分组)。
   **A2**:两条计数断言可复算 ⇒ 本日 RC-2 的修复**确实生效**,不是声称。
3. **「Foundation『零缺口』我是否用『ADR 都已 Accepted』替代了逐条 TR 覆盖?」** [TOOL ACTION]
   `yaml.safe_load` registry 按 `domain: Foundation` 计数 = 15 ✅ / 1 ⚠️ / **2 ❌** / 2 ◆。
   **A3**:**曾错** —— 早先草稿一度写「Foundation 全 covered」,系把「Required #1/#2/#3 转 Accepted」
   当成逐条覆盖。现按 registry 实测记 **残 2 条**,且写明二者判据不同、不互相豁免。
4. **「有没有把 FAIL 条件软化成 CONCERNS 以躲开更难判?」**
   CD 两条**未软判**:如实记为「事实成立 + 各漏读紧邻下半句」,并**主动询问**用户,终判先落 **FAIL**
   再由用户显式承接才降。⚠️ 降级**有用户裁定文本背书**,不是我的判断。**A4**:无软化。
5. **「单看最不可信的一项是什么?」**
   = **示例测试文件是否真的能运行**(本机无引擎 ⇒ 零执行证据)。它支撑「测试框架 functional」判据,
   而该判据**只有存在性证据,无运行证据**。**A5**:故 Blocker 3 明写「实为未证」。

**答问 3 发现一处需改**:Foundation 项从「已覆盖」改回 **❌ 残 2 条**。
**Chain-of-Verification: 5 questions checked — verdict revised from "CONCERNS(Foundation 全绿)" to
"FAIL → 用户承接 → CONCERNS(Foundation 残 2 ❌)"**

---

## 未写 / 未做

- `production/stage.txt` **未写**(本门非 PASS 直落,且规则要求「PASS + 用户确认」才写)。
- **未 commit**(无用户指令)。
- 未改任何 ADR 的 Decision 条款;未勾任何 spike / Validation 框(全案实测 **214 个未勾框**,承「禁借绿」)。
- 机制数值零改动(条件 ② 只动验收文本口径)。

## 用户裁事项(下一批,按优先级)

| # | 事项 | 备注 |
|---|------|------|
| 1 | **RC-1 / RC-3…RC-8**(2026-09-20 报告新登的跨文档冲突,RC-2 本日已结) | RC-1 = `adr-013` 焦点铁律内部矛盾;RC-3 = 门 A 论据机制为假(结论不动) |
| 2 | **AB-1…AB-10**(art bible 待裁族;AB-9 先于敌人资产,AB-10 = §8 与基线竞争) | AD 判 CONCERNS 的全部实质 |
| 3 | **`deprecated-apis.md:28` 的 UGUI 回写** | 参考件未跟 ADR-013,非违规 |
| 4 | **Required ADR #4**(30 定点/持久化 → `TR-skill-008`)/ **#5**(13 写路径) | 二者 TD 已判**非开工阻塞** |
| 5 | **21b 考据量补估**(0.5–1 pd) | PR:本门是最后窗口 |
| 6 | **per-screen UX spec 首件选页** | 建议选同时结 `OQ-42-5` / `OQ-48-8` 的那一页 |
