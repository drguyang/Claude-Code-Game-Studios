# Epic: 诊断与体征揭示

> **Layer**: Core(读侧判定)× Presentation(UI)
> **GDD**: design/gdd/diagnosis-system.md
> **Architecture Module**: L4 Presentation 接缝 + L2 Sim 只读消费(8 不在门 A 内,经 IVitalsQuery→VitalsDto)
> **Status**: Ready
> **Stories**: 6 stories — see table below

## Overview

诊断与体征揭示(8)把「望闻问切」做成**读数入口**而不是数值系统:病人身上只有体征,
**没有血条、没有病名、没有百分比**;玩家的诊断技能决定「多细多可信」,永不决定「有没有」。
三级结构 = 手段(视触叩听+问诊五法,P0 出生自带无锁)→ 通道(五条:面色/语声/姿态/呼吸/触感,
归 9 拥有)→ 体征(词条表 R-8.1 七字段,P0 阳性约 30 条 + 阴性 4 条,已过医学校验冻结)。
核心公式:F-8.1 可读地板 `READ_FLOOR(Skill)`(双条件「与」门:`Skill ≥ tier_named ∧ Sign ≥ READ_FLOOR`)、
F-8.2 精度档槽划分 `SLOT_BOUNDS=(10,20,50)`(粗/中/细/满,35=检验线 P1a 不参与)、
F-8.3 阴性把握度 `C_neg` 与构成排除(无随机三理由,F-8.4/8.5 不泄漏)。
8 的架构铁律:**只读不写不持状态**(`VitalsDto` 是全案唯一浮点出口,8 在定点域之外但受护栏
G-1…G-4 约束,`Math.Sqrt` 出现于 sim 程序集即违门 B);**8↔11 零数据流**(病名不给 11,故意割据);
**8 不回写 9、零持久化**;EmitGrowth 仅主机 + IIdAuthority 门控;联机脉案每人一本。
判断链三态(空⇄疑似⇄确定)住脉案 UI(S-8.3),落笔 = 手写病名 + 置信度循环;误诊不报错,
唯一反馈 = 病人未好转(规则七)。数值(DIAG_TIERS 阈值语义、曲线系数)归用户数值轮;
TR-diag-021…025 为 D-8-8 音频四条,归属 44 侧不在本 epic。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-005 | IVitalsQuery 抽象点;VitalsDto = 唯一浮点出口;病史流唯一真源(8 是只读消费者) | HIGH |
| ADR-006 | 定点域边界;8 侧浮点只在边界程序集之外存在,护栏 G-1…G-4 | MEDIUM |
| ADR-011 | 动作词表路由的输入侧(查体 = 脉案模态行级动作 → 8;S-8.4 路线甲模态分流) | HIGH |
| ADR-013 | UI Toolkit 主 + UGUI 补 world/XR;PresentationDtoGuard 递归扫描(disease_id 不进呈现层);焦点官方桥;ModalId 闭集 7 员 | HIGH |
| ADR-016 | 13 病人 AI 的决策住边界层消费 VitalsDto(8 与 13 同为只读方,37 读 13 单向无环) | MEDIUM |
| ADR-018 | 语声/呼吸两层声源 = 44 侧承载(TR-diag-021…025 映射,非 8 的实现故事);无提示音铁律 | MEDIUM |
| ADR-026 | DIAG_TIERS:数值归 30 层、义归 8(精度档);QueryLevel 只读消费;FixPow 先例撑「8 侧走预计算定表」(G-1) | LOW |
| ADR-025 | 装配落点:8 住 Gameplay.UI/Presentation 侧门面,零 Sim 程序集引用类型 | LOW |
| ADR-024 | 判断记录 Kind(落笔/改写史/读数)经 entities.yaml 登记(ADR-008),8 侧只产意图 | LOW |

**Engine Risk**: **HIGH**(UI Toolkit 焦点桥 = 假设 6 半可信;VR/world-space 推 P1a)。
纯判定逻辑(门槛曲线/把握度/状态机)为 C# 可 EditMode 全测,引擎面集中在 story 006。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-diag-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-28 实测):**25 条**;其中 TR-diag-021…025 = D-8-8 音频四条族映射,
> **实现归属 44(audio-system epic)**,本 epic 只保数据接口。需求原文评审时重读。
> `TR-diag-013` = `no-adr-by-design`(归属件 = 9 规则十 + F5,ADR 段不承载 —— 2026-09-23 Amendment G 登记)。

| TR-ID | Requirement(摘要;原文以 registry 为准) | ADR Coverage |
|-------|--------------------|--------------|
| TR-diag-001 | 8 位于唯一浮点出口之后(IVitalsQuery → VitalsDto) | ADR-005 ✅ |
| TR-diag-002 | 铁律①:8 与 11 之间无数据流 | ADR-013 ◆(刻意割据,GDD 裁) |
| TR-diag-003 | 铁律②:8 不向 9 写任何东西 | ADR-005 ✅ |
| TR-diag-004 | 铁律③:8 不拥有持久化 | ADR-010 ✅ |
| TR-diag-005 | 铁律④:EmitGrowth 仅主机侧由 IIdAuthority 门控 | ADR-005/026 ✅ |
| TR-diag-006 | 铁律⑤:每名玩家一本脉案(联机) | ADR-013 ✅ |
| TR-diag-007 | F-8.6:8 不在 ADR-005 的定点域内 | ADR-006 ✅ |
| TR-diag-008 | G-1:禁 libm 超越函数;指数仅取整数或 1/2 | ADR-026 ✅ |
| TR-diag-009 | G-3:途径整数等级的档位判定 | ADR-026 ✅ |
| TR-diag-010 | G-4:位宽固定 System.Single;禁 FMA 收缩依赖 | ADR-006/012 ✅ |
| TR-diag-011 | D-8-4:Project(Sign_j) → [0,1] 归 9(✅ 9 侧已办) | ADR-005 ✅ |
| TR-diag-012 | D-8-5(登记项) | ADR-005 ✅ |
| TR-diag-013 | D-8-6:共病体征合并(升级为 P0 阻塞) | **no-adr-by-design ◆**(归属件 = 9 规则十 + F5;2026-09-23 Amendment G 登记) |
| TR-diag-014 | D-8-9:SLOT_BOUNDS ← DIAG_TIERS 双向耦合 | ADR-026 ✅ |
| TR-diag-015 | D-8-10(登记项) | ADR-013 ✅ |
| TR-diag-016 | 读数存档的归属(39 或病例流) | ADR-008/010 ✅ |
| TR-diag-017 | AC-8-47 结案时冻结判断链 | ADR-008 ✅ |
| TR-diag-018 | AC-8-F5 表现层 float 跨平台一致 | ADR-012 ✅ |
| TR-diag-019 | disease_id 不进呈现层 | ADR-013 ✅ |
| TR-diag-020 | 脉案 DTO 的静态检查 | ADR-013 ✅ |
| TR-diag-021 | D-8-8 硬需求①:呼吸分两层渲染(通带+噪声底,非静音开关) | ADR-018 ✅(audio epic) |
| TR-diag-022 | D-8-8 硬需求②:语声「变体库混合」 | ADR-018 ✅(audio epic) |
| TR-diag-023 | D-8-8 硬需求③:接触噪声/信噪比参数 | ADR-018 ✅(audio epic) |
| TR-diag-024 | D-8-8 硬需求④:联机各设备按**本机技能档**(2026-09-18 裁定 D-A;原「统一取主机」作废) | ADR-018 ✅(audio epic;注:与 CLAUDE.md ADR-018 日志旧句「取主机技能」存在口径漂移,story 006 评审时以 registry 为准并回写) |
| TR-diag-025 | 无提示音铁律延续(sting/ducking 不报状态) | ADR-018 ✅(audio epic) |

## Definition of Done

This epic is complete when:
- All stories implemented, reviewed, closed via `/story-done`
- All AC from `design/gdd/diagnosis-system.md`(AC-8-1…52 + AC-8-F1…F5)verified
- Logic/Integration 故事有通过测试(真身 `unity/Assets/Tests/EditMode/DiagnosisSystem/`);UI/[V] 项有走查证据 + 主创签核
- `PresentationDtoGuard` 对 8 的全部呈现 DTO 递归绿;disease_id 零跨呈现边界
- AC-8-42(9 侧八病种含神经衰弱依赖)等跨件依赖项按 BLOCKED-BY 显式登记,禁借绿
- AC-42-F1 式的手柄走查按记忆库裁定缓办登记(桌面集中轮),NOT-RUN 状态如实保留

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 程序集边界与 VitalsDto 只读门面 | Integration | **Complete** | ADR-005/006/013/025 |
| 002 | 体征词条表 schema 与 P0 数据行 | Config-Data | Ready | ADR-014/009 |
| 003 | F-8.1 可读地板与 F-8.2 精度档槽 | Logic | Ready | ADR-005/006/026 |
| 004 | F-8.3 阴性把握度与不泄漏不变量 | Logic | Ready | ADR-005/006 |
| 005 | 读数状态机 S-8.1/8.2/8.3 与成长门控 | Integration | Ready | ADR-009/011/013 |
| 006 | 呈现层:脉案五通道、动作词表路由与反幻想护栏 | UI | Ready | ADR-011/013 |
