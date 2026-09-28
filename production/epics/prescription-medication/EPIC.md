# Epic: 处方用药

> **Layer**: Core(结算写流)× Feature(开方流程)× Presentation(戥子/本草呈现)
> **GDD**: design/gdd/prescription-and-medication.md
> **Architecture Module**: L3 Gameplay + L2 Sim 接缝(11 = `DrugTreatmentApplied` 唯一写者)
> **Status**: Ready
> **Stories**: 5 stories — see table below

## Overview

处方用药(11)是支柱一「判断与施治之间那一步由玩家的手完成」的下半段:玩家在脉案上
**手写病名**(8 侧,零数据流向 11)→ 翻开方笺(= 39 脉案「同一本书」,复用 `Casebook` 档,
不成新模态)→ 用**戥子**称出离散整数档剂量 → 11 结算为一条 `DrugTreatmentApplied` 病史流事件。
三条铁律:**病名不给 11**(8↔11 刻意零数据流;11 只读玩家选药 `item_key` + `VitalsDto` + 20 库存,
禁读 `disease_id`/`tier_named`/39 病名)/ **11 不发明结算**(药效值唯一出处 = 21a `drug_profile`,
零重定义)/ **11 不判对错**(9 的离牌门记零贡献,不判错不惩罚)。与 10 的对照:11 难度在**判断**、
**恒 Applied**、无手部门槛;熟练度出口 = **省料 + 解锁,非药效放大**(R-2 改判,F-11.3 已删)。
核心计算:F-11.1 `dose_potency = ROUND_HALF_AWAY_FROM_ZERO(drug_potency × dose / DOSE_BASE)`
(单次舍入的除);F-11.2 F5 求值点 = 11(`Axis_effective = Axis_base + axis_offset_by_quality[quality−1]`,
AC-11-08 唯一求值点,兑现 D-21-11);`single_dose_max` 烘焙期派生常量供 9 的 F1 clamp;
双表 polarity 构建期交叉硬门(`polarity_11(a) == polarity_9(a,d)` ∀(a,d),违则 throw)。
11 **不调 Judge、不用 SkillMul**(共享面收窄 = 载荷形状/流语义/ResultMul 恒 1.0 名义路径不入载荷链)。
数值(DOSE_BASE、词表文案、K_difficulty 等)归用户数值轮;
多处前置跨件未落(O-11→21a BL-1/BL-7、O-11→9 BL-2、OQ-11-13 无主入参)—— 相应 AC 显式
NOT-RUN / BLOCKED-BY 登记,**禁借绿**。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-009 Amendment I | 病史流 `DrugTreatmentApplied`(11 写);域归属表单一定义源;有界性 | MEDIUM |
| ADR-005 / ADR-006 | 全整数载荷;`ROUND_HALF_AWAY_FROM_ZERO` 单一舍入;`Seq` 主机发号;FixParse 边界 | HIGH/MEDIUM |
| ADR-024 | Kind 单一真源 = `entities.yaml`(`DrugTreatmentApplied` 已登记,写者=11);kindgen 断言 | LOW |
| ADR-014 | 作者态 `prescription_actions.json` / `materia_lexicon.json` → 两阶段烘焙;Fix 字段 JSON 写字符串 | MEDIUM |
| ADR-026 | 熟练度门控(`EmitGrowth` 须过 `GateHit`);`SkillGrown` 折叠豁免先例;档位判定走整数等级 | LOW |
| ADR-013 | 方笺 = 39 同一本书(2026-09-21 Amendment B 裁定);`ModalId` 闭集不因方笺增员;戥子读数元件 = 黄铜侧(禁降级数字角标);焦点官方桥 | HIGH(假设 6) |
| ADR-020 | 11 不请求相机档位(开方非「动手」,不发档位意图;承规则十二) | LOW |
| ADR-011 | 戥子读数 = 离散整数档焦点序列(float→int 量化在 3 侧);禁手势 | HIGH |
| ADR-018 | 音频白名单(无提示音);省料/解锁的反馈不报状态 | MEDIUM |

**Engine Risk**: **HIGH** 集中在戥子输入与焦点导航面(UI Toolkit 假设 6 半可信;手柄缓办裁定);
结算与流语义面为纯 C#(LOW/MEDIUM)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-prescription-*`)。计数(2026-09-28 实测):**19 条**。
> 需求原文以 registry 为准,评审时重读(read fresh at review time)。
> 状态注:`TR-prescription-014` partial→covered(ADR-013 Amendment B,方笺裁定);`TR-prescription-016`
> = 本 GDD 裁定 polarity 真源归 9(镜像声明在 11)。

| TR-ID | Requirement(摘要) | ADR Coverage |
|-------|--------------------|--------------|
| TR-prescription-001 | 病名不给 11(零 diagnosis/disease_id/tier_named 引用;零 indications 匹配逻辑) | ADR-013/009 ✅ |
| TR-prescription-002 | 11 不发明结算(药效/半衰期/品级偏移/作用轴/剂量域全来自 21a) | ADR-014 + 21a GDD ✅ |
| TR-prescription-003 | 载荷七项齐备,与 10 同形 | ADR-009/005 ✅ |
| TR-prescription-004 | 作者态表 = (药→action_id)+ 呈现措辞键;21a 无 action_id | ADR-014 ✅ |
| TR-prescription-005 | polarity 双表一致性构建期硬门(throw) | ADR-014/024 ✅ |
| TR-prescription-006 | 11 = F5 唯一求值点(兑现 D-21-11) | ADR-005 ✅ |
| TR-prescription-007 | Axis_effective 下界 ≥ MIN_USABLE_HALF_LIFE(21a 侧升格 BL-1) | ADR-005 ✅(21a 前置) |
| TR-prescription-008 | 剂量整数档、无 hi+1 档(物理限位非运行期 clamp);不读严重度替玩家调剂量 | ADR-011/013 ✅ |
| TR-prescription-009 | 单次舍入 HALF_AWAY_FROM_ZERO;中间域 Q16.16 原始整数 | ADR-006 ✅ |
| TR-prescription-010 | single_dose_max 烘焙期派生常量零手填 | ADR-014/005 ✅ |
| TR-prescription-011 | indications/contraindications 只呈现不拦不扣;病种 id 不进呈现层 | ADR-013 ✅ |
| TR-prescription-012 | 与 20 的原子性:先验库存再扣,无货不发事件 | ADR-009 ✅ |
| TR-prescription-013 | 与 10 载荷/流语义唯一;零第二份 SkillMul/ResultMul/JudgeResult | ADR-009/026 ✅ |
| TR-prescription-014 | 11 不请求相机档位(方笺裁定) | ADR-013 Amendment B ✅(partial→covered) |
| TR-prescription-015 | 同输入跨平台重放处置事件逐位相同(输入集刻意不含技能等级) | ADR-012 ✅ |
| TR-prescription-016 | polarity 判定真源归 9(本 GDD 裁定,11 = 镜像) | ADR-024 ✅ |
| TR-prescription-017 | 熟练度 P0 出口 = 省料+解锁;省料唯一出处 = 21a EFF;30 只拥「等级→EFF」映射 | ADR-026 ✅(O-11→30 BL-6 前置) |
| TR-prescription-018 | 写者独占:`DrugTreatmentApplied` 构造点仅在 11 程序集;7a 序列化白名单两支齐 | ADR-024/010 ✅ |
| TR-prescription-019 | 可感知地板量纲一致(与 9 的噪声同量纲方可比较) | ADR-005 ✅(O-11→9 BL-2 前置) |

## Definition of Done

This epic is complete when:
- All stories implemented, reviewed, closed via `/story-done`
- All AC from `design/gdd/prescription-and-medication.md`(AC-11-01…22,含 [L] 项走查签核)verified
- 跨件前置按登记处置:BL-1/BL-7(O-11→21a)、BL-2(O-11→9)、BL-6(O-11→30)、OQ-11-13(`K_difficulty` 无主)—— 对应 AC 保持 NOT-RUN 直至前置件回写,**禁借绿**
- AC-10-06b 与 emergency-procedures epic 的载荷交叉校验双向绿;写者独占断言(AC-11-22 BLOCKING)绿
- 数值轮交付前,全部表以合成 fixture 走通管线(机制不依赖定值)

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 处方表与本草词表:双表 polarity 硬门 | Config-Data | Ready | ADR-014/024/005 |
| 002 | F-11.1 剂量舍入与 F-11.2 F5 求值点 | Logic | Ready | ADR-006/005/012 |
| 003 | DrugTreatmentApplied 构造、零病名与写者独占 | Integration | Ready | ADR-009/024/013 |
| 004 | Prescribe 流程:库存、扣减与成长门 | Integration | Ready | ADR-009/026/010 |
| 005 | 戥子输入与方笺呈现 | UI | Ready | ADR-011/013 |
