# Epic: 病例系统

> **Layer**: Feature
> **GDD**: design/gdd/case-system.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ 病例流(第二逻辑流)+ Presentation(只读 DTO 侧)
> **Status**: ⚠️ **4/6 In Review**(006/005 合规 Complete)—— 001–004 于 2026-10-06 收口(`e64efad`/`c4e3708`/`31e3f9b`/`8a693fd`),**但当年评审原件缺失**;2026-10-15 QA 核查触发义务第 1/2 条,四件**补做评审(评当下,不追认历史判定)**后 **Test Evidence 门均不通过** ⇒ 用户裁定转 `In Review`。原件:`production/qa/evidence/review-case-system-story-00{1,2,3,4}-retro-2026-10-15.md`;核查件:`review-case-system-001-004-gap-audit-2026-10-15.md`。修复归**新一轮**。
>
> 📌 **修复排期(2026-10-15 用户裁定「并入 sprint-11 · 必须项优先」)**:净保留判定 **22 条 → 去重 ≈13 类**,
> 本 sprint 只取「**真缺陷 / 免责失效 / 设计冲突 / 零成本**」四类 = **S11-7…S11-11**(≈2.85 天);
> **断言形态整批顺延 sprint-12**,须逐条带状态 + 归属登记(**禁借绿**)。详见 `production/sprints/sprint-11.md`
> 「Must Have — 补评修复批」节。**四件维持 `In Review` 直至顺延面清空。**
> ⚠️ **本 epic 的两处裁定已同批落盘**:① **37 写者延后正式登记**(`CaseClosed` / `PatternRecognized` 写者 ——
> `WriterExistenceGate.cs:87-88` 具名豁免 ⇒ 已写入 story-003/004 的 §Out of Scope 并订正对应证据面);
> ② **`treatment_set` 枚举补立**(此前 `adr-008` §四 / GDD F-37.2 / 规则六 / 规则八 **四处皆不枚举**处置 Kind,
> 致实现自收 `InjuryOnset` ⇒ 击穿「空手连关三例不可能」;现权威定义落 `adr-008` §四)——
> ✅ **代码侧已于 2026-10-15 S11-10 对齐**(`TreatmentKinds` = `{Emergency, Drug, CareApplied}`,
> 含 `MembersSnapshot()` 双向穷尽判据 + 顺批 A-1;四处口径已核一致)。
> ✅ **排期面**:**S11-7 / S11-8 / S11-9 / S11-10 / S11-11 均已于 2026-10-15 闭合**(各带证据原件,
> 见 `production/qa/evidence/fix-s11-{7,8,9,10,11}-*.md`)—— **补评修复批五项全部清空**。
> ⚠️ **三件真实现缺陷(B-1 / B5 / BLK-8)已全部闭合**;**S11-9 的两处边界(防误读)**:
> `PatternRecognized` **写者仍未落**(承 2026-10-15 裁定「接受延后并正式登记」)·
> **CB-3 未触及**(`MemberSet` 仍扁平 ⇒ 归 sprint-12)⇒ **四件仍维持 `In Review`**。
> **Stories**: 6 stories — see table below

## Overview

病例系统(37)是「病例」这种东西的宿主 —— 玩家实际**从事**的游戏单元:接病人 → 看体征 → 在脉案上落(或空)一个病名 → 施治 → 显式结案。它把 9 的病史流包装成可开/可结的容器(病例只走两态 开/结,与病人九态**正交** —— 死亡不是病例的转移条件),并持有**支柱四「拼出来的图样」在 P0 的唯一载体**:F-37.1 同源检测 —— 判「结案」不判「诊断正确」,`FirstPerPatient(D)` 筛选使成员互异结构性成立,第 `PATTERN_THRESHOLD`(P0=3)例结案时发一条世界级 `PatternRecognized`(哨兵 `PatientId.None`,载荷 `salted_key = SplitMix64(WorldSeed,"case-salt",ordinal(D))` + `anchor_case` + `member_set`,冻结不重发)。承重 ADR-008:**病例流 = 第二条逻辑流**,五 Kind(`CaseOpened`/`CaseClosed`/`PatternRecognized`/`JudgmentRecorded`/`JudgmentRevised`)经 `entities.yaml` + kindgen 路由;`case_id = (Tick,Patient,Seq)` 三元组;跨流全序 `(Tick,StreamPriority,Patient,Seq)`;处置证据窗口 `[CaseOpened.Tick, CloseTick]`(**2026-09-17 方向订正**,复诊后门在公式层关闭)+ `treated`/`disease_set` 快照进 `CaseClosed`;病例流不物理折叠。规则九守密:`disease_id` 类型层面不得进 39/42/48 DTO(`PresentationDtoGuard` 递归扫描,AC-37-15 唯一落点;保密 = player 不可见非 client 不可知);第六泄漏面(玩家自书病名分组/词表一一对应)由 AC-37-24/35 + 烘焙期非同一性校验堵。37 全 int 零 Fix 零浮点数值、**P0 不发任何数值奖励**(AC-37-32 反射断言)。⚠️ 未裁项:**W-1**(三案链纯伤情 vs D 域口径 → 须 9+37 会签二选一,不裁不得实现 ScriptedChain 联调)、D-37-B(病史流有界性新增行为学前提须 9/7a 登记);36 条 AC 多为负存在断言,载体缺失时记 NOT-RUN 禁空集绿。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-008: 病例事件流(第二条逻辑流) | 按 Kind 路由;跨流全序键;「已处置」= 处置事件为证 + 窗口化快照;盐 = WorldSeed 派生(防御纵深);病例流不折叠 | HIGH |
| ADR-005: 确定性模拟 | 病例状态 = 流前缀纯函数;主机唯一 Append;SimEvent/Seq 复用 | HIGH |
| ADR-006: 定点域边界 | ordinal/枚举为整数计数,**移出 Fix 解析集**(FixSet 之错的裁定源);存档禁 float | MEDIUM |
| ADR-007: 事件权威与掷骰 | `PatientId.None = -1` 世界级哨兵,不污染高水位 | LOW |
| ADR-009: 世界状态事件化边界 | 病例 = 模拟态进流判据(三问);三流并集 max(id) 重构 | MEDIUM |
| ADR-010: 持久化与存档格式 | 病例流序列化;`Folded(p)` 含「无未结案病例」(7a 侧);非拒载口径 | MEDIUM |
| ADR-013: 拟物 UI 框架 | PresentationDtoGuard 递归扫描(AC-37-15 机制);42 承载走查;quill_tick 只读 | HIGH |
| ADR-014: 数据管线 | `case_judgment_lexicon` 烘焙 + 非一一对应校验 + ordinal append-only 基线门;ConfigVersion 覆盖集 | MEDIUM |
| ADR-024: Kind 单一登记真源 | 五 Kind 入 entities.yaml(stream/author/payload_schema);kindgen A1–A5 | LOW |

**Engine Risk**: **HIGH**(ADR-008/005 的刻意不用 post-cutoff API 但验证面挂 IL2CPP 逐位与折叠语义实测;本 epic 机制层为纯 C# 契约,风险不阻塞实现,复跑归 ADR-012 矩阵批)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-case-*`,共 36 条,与 GDD 36 条 AC 一一对应)。
> 计数(2026-09-28 实测):**36 条 = 26 covered + 6 partial + 4 gap**(gap = TR-case-012 立案两路径 / 013 串接语义 / 016 结案出口 / 018 同源算法 / 020 不拥有清单 / 027 图样可辨识度 —— 其中 012/013/016/018 的**机制层**由本 epic stories 兑现,ADR 侧无需新裁决;027 归 playtest)。partial 主因 = 借绿降级(TD C4)与 53 实现轮。

| TR-ID(代表) | Requirement(摘要) | Status |
|-------|--------------------|--------|
| TR-case-001…011 | case_id 三元组 / 病例流 / 不折叠 / 全序键 / 五 Kind 载荷 / 哨兵 / 判断事件化 | covered(ADR-008 主体) |
| TR-case-012 | 规则二:立案两条路径 | gap —— story-002 兑现机制 |
| TR-case-013 | 规则三:串接语义 | gap —— story-002/006(呈现归 39) |
| TR-case-016 | 规则六:结案唯一出口、显式、单例、不可撤销 | gap —— story-003 |
| TR-case-018 | 规则八:同源检测算法 | gap —— story-004 |
| TR-case-020 | 规则十:不拥有清单 | gap(边界负断言)—— story-005 |
| TR-case-027 | 图样可辨识度 playtest | gap —— 出本 epic(内容/playtest 轮) |
| TR-case-028…033 | ordinal/append-only/词表/ConfigVersion/ScriptedChain/开案唯一 | covered/partial 见注册表 |
| TR-case-034 | quill_tick 进判定记录 DTO | covered(ADR-013) |
| TR-case-035/036 | 「37 沉默≠世界沉默」/ 就诊动作词表归属 | partial(53/8 实现轮) |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- AC-37-01…36 中可自动化条目全部有通过测试;负存在断言按「闭集点名 + 载体是否已建」纪律执行,载体缺失记 **NOT-RUN** 禁空集绿
- ✅ `ScriptedChain(D)` 的 **W-1 前置已结案(2026-10-15 会签)**;机制层已由 **S11-9** 交付(含夹具 7 条)。
  ⚠️ 与 9 的**真联调**(真实 `patient_id`)仍待**内容数据** —— `patient_id` 绑定归**内容轮 + ADR-014 烘焙**,
  GDD F-37.1b 明写「本 GDD 只登记机制与判定域,**不落数值**」⇒ **该数据不存在不是缺口,是归属划分**。
- [V] 走查条目(AC-37-16/17/19)由 39/42 epic 的走查批承接并留档,本 epic 登记移交
- 数值(PATTERN_THRESHOLD 维持 3 / 立案半径 / 就诊触发条件)归用户数值轮;D-37-B 行为学前提已转 9/7a 登记

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 病例流 Kind 登记与病例身份 | Logic | **In Review ⚠️ 2026-10-15**(原 `Complete 2026-10-06`(`e64efad`)判定不成立;补评 4 BLOCKING·测试面 —— **③ B-3 AC 号一号两义 ✅ S11-11 闭合**(全文件去号 25 处);余 **3 条**顺延:判据不可证伪(B-1,须退设计)· kindgen A2 空跑(B-2)· kindgen 借绿(B-4)) | ADR-008/024/006 |
| 002 | 立案两路径 · 开案唯一 · 判断记录事件化 | Integration | **In Review ⚠️ 2026-10-15**(原 `Complete 2026-10-06`(`c4e3708`)判定不成立;补评 + §七 勘误后核心 **1 类** —— **`LastJudgmentOf` 死参·实害链「墨龄取错病人」✅ S11-7 闭合**(归属改流序关联;回退旧实现 ⇒ 2+1 红,端到端复现实害);余 **声明件 5 条断言形态** + BLK-9/BLK-10 顺延 sprint-12) | ADR-008/009/005 |
| 003 | 结案前置:处置证据窗口与快照 | Logic | **In Review ⚠️ 2026-10-15**(原 `Complete 2026-10-06`(`31e3f9b`)判定不成立;补评 + §七 勘误后核心 **4 类** —— **① `IsClosed` 按 `Patient` 匹配 ✅ S11-8 闭合** · **⑤ `TreatmentKinds` 错收 `InjuryOnset`漏收 `CareApplied` ✅ S11-10 闭合(+A-1 顺批)**,各带「改回旧实现即转红」反证;余 **2 类**未清:快照半场(写者未落,无载体)· AC-37-18 恒真化) | ADR-008/007 |
| 004 | F-37.1 同源检测与 PatternRecognized | Logic | **In Review ⚠️ 2026-10-15**(原 `Complete 2026-10-06`(`8a693fd`)判定不成立;补评 8 BLOCKING·双面 —— **CB-2/TB-5 AC-37-29 零覆盖 ✅ S11-9 闭合**(`ChainEligible`/`ScriptedChain` 从全库零命中转为有实现 + 有测试,测数 23 → 30);余 **6 条**顺延:TB-1 定序判别力 · TB-2 成员冻结 · TB-3 真空夹具 · CB-1/CB-3 实现侧 · TA-6 元规则) | ADR-008/007/012 |
| 005 | 守密纪律:DTO 守卫 · 词表烘焙 · 零奖励断言 | Integration | **Complete ✅ 2026-10-08** | ADR-013/014/006 |
| 006 | 重放持久化与跨系统边界义务 | Integration | **Complete ✅ 2026-10-08** | ADR-010/005/008 |
