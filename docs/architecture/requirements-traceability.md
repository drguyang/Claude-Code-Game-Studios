# Requirements Traceability Matrix (RTM)

> Last Updated: 2026-09-23
> Mode: /architecture-review rtm —— **本文件当前不处于 rtm 模式输出态**(见 §形态说明)
> Coverage: **0%** full chain complete (GDD → ADR → Story → Test)—— 非缺件,是阶段事实
> Engine: Unity 6.3 LTS
> 登记处(数据权威):`docs/architecture/tr-registry.yaml`(500 条,`status:` 字段为计数真源)
> 人读矩阵:`docs/architecture/traceability-index.md`(GDD → ADR 两级;**334 ✅ / 76 ⚠️ / 77 ❌ / ◆13 no-adr-by-design** —— 2026-09-23 QQ-08 清账批后实测;同日 #4/#5 兑现轮后值 328/76/94/◆2;回写轮值 318/77/103;第二十八批值 317/77/103;第二十六批值 316/78/103;D-R3 批次后值 314/79/104;更早 245/51/89 系分母不含 12 项零 TR 系统的偏高口径)
>
> **2026-09-23 更新(QQ-08 + QQ-02 清账批 —— 用户裁定路线 [A],21a 簇 17 条翻转,零新 ADR)**
> —— **328/76/94/◆2 → 334/76/77/◆13**(ID 恒 500,零新增;`yaml.safe_load` 复算自洽)。三簇拆账:
> A 簇 6 条 gap→covered(`TR-itemdb-014/018/019/020/021/028`,带 ADR 指针 + 禁借绿注)·
> B 簇 11 条 gap→◆ `no-adr-by-design`(`TR-itemdb-007/008/009/011/012/015/016/017/024/029/030`,
> 逐簇裁定 + 归属件登记;◆ 判据同步扩第二类入 `traceability-index.md` 图例 + `gate-check` SKILL)·
> C 簇 1 条 carve-out(`TR-itemdb-031` **不翻**,承 2026-09-21「随 ADR-009 TR 复核轮重裁」未撤)。
> **QQ-02 同批结**:ADR-005 `IIdAuthority` 回填 `ItemInstanceId Next()` 双方法(逐字承 ADR-010 §五)。
> **17 条全 `domain: Core` ⇒ Foundation 门判据零影响**(残余仍恰 = `TR-itemdb-031` 1 ❌)。
> **本批零数值改动、零新 ADR**(机制数值冻结);audio-011/012 另开小裁(真 ADR 候选)。
>
> **2026-09-23 更新(Required ADR #4/#5 兑现轮 —— adr-026 技能成长定点化 + adr-027 病人 AI 写路径)**
> —— **318/77/103 → 328/76/94**(ID 恒 500,零新增;`yaml.safe_load` 复算自洽)。`TR-skill-001…006 / 008`
> **gap→covered**、`TR-skill-007` **partial→covered**(`adr: ADR-026`,兑现 §Required #4);`TR-patient-021/022`
> **gap→covered**(`adr: ADR-027`,兑现 §Required #5,用户裁定两条写路径皆归系统 10)。ADR-026 结清 `OQ-7a-9`
> (折叠豁免 `SkillGrown` 行 = 案 1);ADR-027 结清 `OQ-13-1`/`OQ-13-3`。**本批零数值改动**(机制数值冻结)。
>
> **2026-09-23 更新(回写轮 —— U1 批出批:V-8 承接 + R-4 + D-1 执行面 + F7 降级字样订正 + 收尾批)**
> —— **317/77/103 → 318/77/103**(ID 499 → 500,append-only;新增 `TR-worldeco-010` = 运行期 chunk 激活权,
> `adr: ADR-023`,`covered`)。落点 18 件,含 ADR-005/008/014/022/023/024/025 回写 + `TR-medcons-001` 修正。
>
> **2026-09-21 更新(第二十七批 —— ADR-024 补齐轮:SkillGrown Kind + next_player_id 双项登记)**
> —— **计数零变动**(499 / 316 / 78 / 103 / ◆2);registry 两条新行不占 TR ID;
> `TR-death-005` 改挂 `OQ-7a-9` 不翻绿(禁借绿)。
>
> **2026-09-21 更新(第二十六批 —— D-R3 处置批:A 组回写全批 + player_id 裁决落 ADR-006 注记)**
> —— **316/78/103/◆2**(`TR-persist-004/006` partial→covered · `TR-casebook-002` gap→partial;
> ID 恒 499,`yaml.safe_load` 逐条复算自洽;8 条未来 ADR 候选 = 登记不立件裁定入注)。
>
> **同日前一更新(D-R3 专门批次 —— 12 项 P0 零 TR 系统回填,+112 条;387 → 499)**
> —— **314/79/104/◆2**,`yaml.safe_load` 逐条复算自洽 499(既有条目逐字未动,append-only);
> ⚠️ 覆盖率 63.0% → **62.9%** 是分母效应(账外收进账内),**非质量下降**(第二十六批回写后 63.3%)。
> **同日前一更新(`/architecture-review` full 复跑,报告 = `architecture-review-2026-09-21.md`)**
> —— 计数零变动(245/51/89/◆2,复算自洽 387);
> 本轮只修**两处条目文本**(`TR-diag-024.requirement` / `TR-patient-018.note` 的「联机 = 主机技能」
> 旧口径 → 「各设备按本机技能档」),**状态位零翻转**。8 条 RC + S-4 全部结案(落在 ADR 正文)。
> **本文件的 §阶段前提 第 2 条口径更新**:`sim_fixedpoint_test.cs` 的 asmdef 前置**已由 ADR-025
> (2026-09-20 Accepted)解除** —— 该测试**仍不被编译**的原因现只剩「Unity 工程本体不存在」,
> 不再是「命名未裁」。

## How to read this file

**本文件与 `traceability-index.md` 是两个不同产物,不是同一产物的两个名字。**
`/architecture-review` 的默认输出是三件套:评审报告 + **traceability index**(`SKILL.md:579`
格式,H1 = `# Architecture Traceability Index`)+ TR registry。而 **RTM** 只在 `rtm` 模式下
产出(`SKILL.md:24-26` / Phase 3b),它比 index 多两列 —— Story 与 Test File —— 因此
**它需要 `production/epics/` 里有故事文件才有内容可填**。

| Column | Meaning | 本阶段状态 |
|--------|---------|-----------|
| TR-ID | Stable requirement ID from tr-registry.yaml | ✅ 500 条已登记 |
| GDD | Source design document | ✅ 见 index 逐行 |
| ADR | Architectural decision governing implementation | ⚠️ 334 covered / 76 partial / **77 gap**(另 13 条 ◆ `no-adr-by-design`,2026-09-21 起为第四态,不计缺口;2026-09-23 QQ-08 扩第二类)|
| Story | Story file that implements this requirement | ❌ `production/epics/` 不存在 |
| Test File | Automated test file path | ❌ 仅 1 个种子测试,且未与任何 TR 绑定 |
| Test Status | COVERED / MISSING / NONE / NO STORY | 全量 = **NO STORY** |

## 阶段前提(为什么现在是空矩阵而不是缺件)

1. **项目阶段 = Technical Setup → Pre-Production 门前**,`/create-epics` 与
   `/create-stories` 是 **Pre-Production → Production 门**的产物 —— 故事文件在本阶段
   **按设计不存在**。
2. 唯一的测试文件 `tests/unit/sim/sim_fixedpoint_test.cs` 的自陈身份是**种子测试**:
   它验证 UTF/NUnit 管线可用 + 内联 ADR-006 三条边界口径,**不绑定任何 TR-ID**;
   且因 asmdef 未落地(待 Required ADR #2)它**尚不被编译**。把它记成任何 TR 的
   COVERED 都是**借绿** —— 本项目纪律明令禁止(`architecture.md` §5.5 D-5 / TD 条件 C4)。
3. 因此本文件在 rtm 真正跑起来之前的正确形态 = **本骨架 + 链前两段的指针**,
   而不是伪造 Story / Test 两列。

## Coverage Summary

| Status | Count | % | Source of truth |
|--------|-------|---|-----------------|
| COVERED — full chain complete | 0 | 0% | 本文件(Story 段不存在) |
| MISSING test — story exists, no test | 0 | 0% | 同上 |
| NO STORY — ADR exists, not yet implemented | 334 | 66.8% | registry `status: covered` 计数(2026-09-23 两批:QQ-08 清账 6 条 + #4/#5 兑现 10 条翻 ✅;partial→covered 另计)|
| NO ADR — architectural gap | 94 | 18.8% | registry `status: gap` 计数(兑现轮 `TR-skill-001…006/008` + `TR-patient-021/022` 8 条 gap→covered;D-R3 回填 +15;第二十六批 `TR-casebook-002` gap→partial;2026-09-21 曾由 91 减 ◆2)|
| NO ADR BY DESIGN — 范围 / 政策声明,结构上无裁决可挂 | 2 | 0.4% | registry `status: no-adr-by-design` 计数 —— **不计入缺口**;判据见 `traceability-index.md` 读法表 ◆ 行 |
| PARTIAL(链已断在 ADR 段)| 76 | 15.2% | registry `status: partial` 计数(兑现轮 `TR-skill-007` partial→covered;D-R3 回填 +28;第二十六批 −3/+1;第二十八批 −1)|
| **Total requirements** | **500** | **100%** | `tr-registry.yaml` |

> 计数口径:对 registry 的 `status:` 字段直接 `yaml.safe_load` 计数,与
> `traceability-index.md` §汇总「合计」行一致(**328 + 76 + 94 + ◆2 = 500**;2026-09-21 起四态;
> 2026-09-23 Required ADR #4/#5 兑现轮后 covered 318→328、partial 77→76、gap 103→94,total 恒 500)。
> ⚠️ **2026-09-20 `/architecture-review` 复跑时发现本文件 §Coverage Summary 的六处计数漏刷**(仍为回写轮前的
> 243/93)—— 已就地订正为 registry 实测值。Failure mode = 「同一文件内改了 §Uncovered 而没改 §Summary」,
> 与 `consistency-failures.md` 2026-09-20 批次二的「正文状态断言稳定滞后」同型。

## Full Traceability Matrix

**当前为空 —— 逐 TR-ID 展开归 `/architecture-review rtm` 轮。**
按本项目纪律,不以「先写空表占位」冒充完成:Story / Test 两列一旦填上不存在的路径,
下游会把它读成已验收。GDD→ADR 两段的逐条内容在 `traceability-index.md`(499 条逐行,
按 GDD 分节);本文件不复制它(单一真源纪律)。

## Uncovered Requirements (Priority Fix List)

### Foundation layer gaps —— ⚠️ 这是 Pre-Production 门的一条**质量门**,当前不为绿(**6 → 4 → 2 → 1 ❌**;2026-09-23 Required ADR #4/#5 兑现轮再缩 1;**同日 QQ-08 清账批 17 条全 `domain: Core` ⇒ 本节零变动,残余仍 = `TR-itemdb-031` 1 ❌**)

| TR-ID | 缺什么 | 建议归属 |
|-------|--------|---------|
| `TR-itemdb-031` | 掉落实体的世界状态事件化边界 | ✅ **`adr:` 已于 2026-09-21 回写**为 `ADR-009 + ADR-015`(原 null,与 note 自陈矛盾 = 登记层漏刷,与 `TR-interaction-015` 同型)。⚠️ **`status` 仍 `gap` 不翻**(该条「状态重裁随 ADR-009 TR 全量复核轮,本文不预判」原口径未撤回 —— **禁借绿**):缺的是逐条复核的执行体,不是裁决 |
| ~~`TR-randomevents-010`~~ | ~~asmdef / Roslyn 构建期校验体系~~ | ✅ **2026-09-20 gap → covered** —— Required ADR **#2 已兑现(ADR-025 §①④ asmdef 面)+ #3(ADR-024 §⑤ 校验体系面)**;⚠️ 「Roslyn」字面未采纳(ADR-024 Alt E),执行体归实现轮(**禁借绿**) |
| ~~`TR-randomevents-031`~~ | ~~构建期校验:17 条拒绝表~~ | ✅ **2026-09-20 gap → covered** —— Required ADR **#3 已兑现(ADR-024 §⑥,执行体归 ADR-014 阶段 2)**;⚠️ 17 vs 18 谓词差 1 已登记(**禁借绿**) |
| ~~`TR-skill-008`~~ | ~~技能成长的存档持久化~~ | ✅ **2026-09-23 partial → covered** —— Required ADR **#4 已兑现(ADR-026 §⑥:技能成长持久化契约)+ `adr-010 §三` 义务行 14 落点**;形状面 ADR-010 + 逐字段契约 ADR-026 双覆盖 |
| ~~`TR-concept-003`~~ | ~~MVP 的 8 条定义~~ | ◆ **2026-09-21 `gap → no-adr-by-design`**(用户裁定门规格修订)—— 范围声明,归属件 = `game-concept.md` MVP 节(已定),**结构上不可能有 ADR**;**不再计入本门缺口** |
| ~~`TR-concept-004`~~ | ~~P0 排除项清单~~ | ◆ 同上(范围 / 政策声明,2026-09-21 转 ◆) |

> **门影响显式登记**:gate-check 的质量项「Architecture traceability matrix has **zero
> Foundation layer gaps**」当前 **1 条不为绿**(2026-09-23:Required ADR **#4 兑现**后 `TR-skill-008`
> **partial→covered**,Foundation 由 2 ❌ 缩至 **1 ❌**;2026-09-21:◆ 态摘除 `TR-concept-003/004`
> 两条范围件 —— 该二条按新判据**结构上不可能有 ADR**,原判据把它们算作缺口属 mis-specified;
> `TR-itemdb-031` 补 `adr:` 指针但**仍计 ❌**,因 status 未翻)。历史轨迹:2026-09-20 回写轮由 6 降 4 ——
> 两条 Required ADR 兑现件 `TR-randomevents-010/-031` covered。**残 1 条须由实现轮兑现**:
> `TR-itemdb-031`(随 ADR-009 的 TR 全量复核轮逐条判)。
> ⚠️ **此质量门不为绿是事实**(gate-check 会据此判 Foundation 缺口);Required ADRs 的
> **#1/#2/#3/#4/#5 五条已全转 Accepted**(technical-preferences ADR 日志),但本门看的是
> **逐条 TR 的 `covered` 覆盖**,非 ADR 是否有日志 —— 二者不同判据,不互相豁免。

### Core / Feature / Presentation / Performance / Networking layer gaps

77 − 1 = **76 条** gap 分布在非 Foundation 层(2026-09-23 **QQ-08 清账批后值**:17 条翻转全属 Core 域,Foundation 分子不动;2026-09-23 **#4/#5 兑现轮后值**:94,该批 10 条翻转中 8 条属非 Foundation,`TR-skill-007` 属 partial→covered 非 gap;2026-09-21 **第二十六批后值**:87 + D-R3 新增 15 − `TR-casebook-002` 转 partial 1 ———
其中 53 的 3 条为 **P1a 主照登**,7b-007 带「或归 GDD 家规」分工疑点;Foundation 现 **1 条**
(`TR-itemdb-031`);
逐条清单在
`traceability-index.md` 各分节的 ❌ 行(该表按 GDD 分节,每行带 `adr:` 与建议落点注)。

### Tooling layer

`Tooling` 域 9/9 covered,`adr: null` 计数 0 —— 全 registry 唯一无缺口的域(ADR-022 单点覆盖 + D-R3 轮的 `TR-building-008` collider 足迹构建门,仍归 ADR-022 C2)。

## History

| Date | Full Chain % | Notes |
|------|-------------|-------|
| 2026-09-20 | 0% | 本文件建立。**建立原因**:`/gate-check Technical Setup → Pre-Production` 的必交件清单要求此路径存在,而全仓既有的同功能件叫 `traceability-index.md`(该词在仓内被 **36 处路径引用 / 22 个文件**引用)—— 改名的涟漪成本远高于建立本指针件。**本文件不复制索引内容**;rtm 模式真跑时(有 epics 之后)按 `SKILL.md:472` 格式就地扩写本文件。 |
| 2026-09-23 | 0% | **QQ-08 + QQ-02 清账批(用户裁定路线 [A])** —— covered 328→**334** / partial 76→**76** / gap 94→**77** / ◆2→**◆13**(total 恒 500)。21a 簇三拆:A=6 gap→covered(014/018/019/020/021/028)/ B=11 gap→◆(007/008/009/011/012/015/016/017/024/029/030,逐簇裁定)/ C=1 carve-out(031 留 gap)。QQ-02 结案(ADR-005 `IIdAuthority` 双方法回填,承 ADR-010 §五)。◆ 判据扩第二类(图例 + gate-check SKILL 同批)。**17 条全 Core 域 ⇒ Foundation 门零影响**(残 `TR-itemdb-031` 1 ❌)。零新 ADR、零数值改动。audio-011/012 另开小裁。 |
| 2026-09-23 | 0% | **Required ADR #4/#5 兑现轮** —— covered 318→**328** / partial 77→**76** / gap 103→**94**(total 恒 500)。10 条 TR 翻 ✅(`TR-skill-001…006/008` + `TR-patient-021/022` gap→covered;`TR-skill-007` partial→covered),兑现 `architecture.md` §Required #4(ADR-026)/ #5(ADR-027)。**Foundation 缺口 2 → 1 ❌**(`TR-skill-008` 转 covered;残 `TR-itemdb-031` 随 ADR-009 复核轮)。Full Chain 仍 0%(`production/epics/` 不存在,非缺件)。 |

## 何时把本文件变成真 RTM

三条前置齐了即可跑 `/architecture-review rtm` 并就地覆盖本骨架:

1. `production/epics/**` 有故事文件,且故事 Context 段带 `TR-ID`(由 `/create-stories` 产出)
2. 至少一个测试文件的头部声明它验证哪个 TR-ID(当前种子测试**刻意不声明**,理由见上)
3. asmdef 落地(Required ADR #2)⇒ 种子测试真正被编译,首次出现可执行的绿
