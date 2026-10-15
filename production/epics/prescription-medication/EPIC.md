# Epic: 处方用药

> **Layer**: Core(结算写流)× Feature(开方流程)× Presentation(戥子/本草呈现)
> **GDD**: design/gdd/prescription-and-medication.md
> **Architecture Module**: L3 Gameplay + L2 Sim 接缝(11 = `DrugTreatmentApplied` 唯一写者)
> **Status**: **Complete ✅ 2026-10-06**(5/5 story 已收口;filtered **155/155** · 全量 EditMode **2916/2869/0红**;评审原件五份齐备)· ⚠️ **未闭项按登记 NOT-RUN / BLOCKED-BY,禁借绿 —— 详见下方 §Epic Status**
>
> ⚠️ **状态口径(2026-10-15 补 —— 消同文件自相矛盾)**:**「story 层 5/5 Complete」** 与 **「epic DoD 未达」**(§Epic Status 明写)是**两件事**,不得读成冲突。未闭项**均结构性归属其他 epic / 外部件**(ADR-012 三格矩阵 · emergency-procedures 载荷双向绿 · 21a 内容轮 · 人工走查 / 医学从业 / 音频 lead 签核),**本 epic 无单方可闭项** ⇒ **不阻塞 story 收口**,但**阻塞 epic DoD 第 2 / 第 4 条**。同口径先例 = `emergency-procedures/EPIC.md` §未闭项登记(「均**结构性归属其他 epic**,**不阻塞本 epic**」)。
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
| TR-prescription-018 | 写者独占:`DrugTreatmentApplied` **写入路径**构造点仅在 11 程序集(解码重建**具名豁免** `Sim.Codec.PayloadCodec`;⚠️ **2026-10-15 TD 裁「收窄」**);7a 序列化白名单两支齐 | ADR-024/010 ✅ |
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
| 001 | 处方表与本草词表:双表 polarity 硬门 | Config-Data | ✅ Complete 2026-10-06(DC-2 / DC-6 NOT-RUN) | ADR-014/024/005 |
| 002 | F-11.1 剂量舍入与 F-11.2 F5 求值点 | Logic | ✅ Complete 2026-10-06(评审原件已补:2026-10-06 补做轮;AC-11-11④ / AC-11-19 断言本体 / TR-007 21a 半边 NOT-RUN) | ADR-006/005/012 |
| 003 | DrugTreatmentApplied 构造、零病名与写者独占 | Integration | ✅ Complete 2026-10-06(评审原件 + 双代理一轮修复;~~7a 白名单~~ ✅ **已闭 2026-10-15**(S11-4 落判据);~~AC-11-22 文本背离~~ ✅ **已闭 2026-10-15**(TD 裁「收窄」,AC/registry/index/门注四处同批订正);AC-11-15 矩阵 / 影子装配半边 / tag 序 NOT-RUN) | ADR-009/024/013 |
| 004 | Prescribe 流程:库存、扣减与成长门 | Integration | ✅ Complete 2026-10-06(AC-11-16 正式对拍 / 换算表真源 / 非主机半边 NOT-RUN) | ADR-009/026/010 |
| 005 | 戥子输入与方笺呈现 | UI | ✅ Complete 2026-10-06(结构半边;走查半边 NOT-RUN) | ADR-011/013 |

## Epic Status

> ✅ **5/5 story 已收口(2026-10-06)**:story-003 的 AC-11-01① / AC-11-22 / AC-11-10 三条 BLOCKING
> 现有**真判据**(新建门 `PrescriptionWriterGates.cs` + `drug_event_test.cs` 26 测;
> 门已接 `AssemblyGates` 的**菜单与构建前门**两处)。
> 评审原件:`production/qa/evidence/review-prescription-story-00{1..5}-2026-10-06.md`(五份齐备)。
>
> ⚠️ **但 epic 未达 DoD** —— DoD 第 2 条「AC-11-01…22 全部 verified」与第 4 条
> 「与 emergency-procedures 载荷交叉校验**双向**绿」**均未满足**(后者对面 epic 仍 `In Review`)。
> ✅ **2026-10-15 登记回刷(S11-4)**:下表 story-001 的两项(DC-2 / DC-6)**实现侧早在 2026-10-07 已闭环**
> —— 此前**登记未回刷**,非「未实现」;另 **AC-11-22 的 7a 白名单半边**已由 S11-4 落判据
> (白名单 = `EventKind` 35 支闭集 ∩ `PayloadCodec` / `PayloadEncoder` 分派表)。
> 详见下表订正行与 §2026-10-07 增量段末。
> 下列项**按原口径 NOT-RUN / BLOCKED-BY,禁借绿**,等待前置件回写:

| 归属 | 未闭项 | 阻塞源 |
|---|---|---|
| story-001 | ~~DC-2(`action_id` 闭集)~~ ✅ **已闭 2026-10-07**(master = `disease_action_axis.json`,`2a7764f`)· ~~DC-6(`NOISE_BAND_9` 无主)~~ ✅ **已闭 2026-10-07**(9 侧立 `NOISE_BAND_POTENCY_9` ⇒ 判据 RUN) | ~~OQ-11-2 · BL-2~~ ⇒ **残余** = DC-6 对当前数据集**零求值** + BL-7(21a `drug_potency` 域未声明) |
| story-002 | AC-11-11④ 宽度分支 · AC-11-19 **断言本体** · TR-prescription-007 的 21a 半边 | BL-7 / BL-2 / BL-1(均 O-11→21a) |
| story-002/003 | AC-11-15 三格矩阵 + 跨进程半边 | ADR-012 矩阵未激活 |
| story-003 | 影子装配注入半边 · `CheckPayloadPairing` 不覆盖 codec tag 序 · ~~AC-11-22 的 7a 白名单本体~~ ✅ **已闭 2026-10-15**(S11-4 落判据:白名单 = `EventKind` 35 支闭集 ∩ codec 分派表) | 需动态生成程序集 · 归 ADR-014 阶段 2 ~~· BLOCKED-BY-7a~~ |
| story-003 | ~~**AC-11-22 / TR-prescription-018 与实现的文本背离**(解码点在 11 之外构造载荷)~~ ✅ **已闭 2026-10-15**(TD 裁「**收窄**」并落盘:AC 文本 · `tr-registry.yaml` · `traceability-index.md` · 门注 **四件同批订正**;解码豁免**具名**为 `Sim.Codec.PayloadCodec`) | ~~门已显式记账;收窄裁定归 producer / TD~~ ⇒ **裁定记录** `production/qa/evidence/td-ruling-ac11-22-narrowing-2026-10-15.md` |
| story-004 | AC-11-16 正式对拍 · 换算表真源 `D-21-29` · 非主机传输半边 | OQ-11-13(`K_difficulty` 无主)· OQ-11-10 · BLOCKED-BY-45 |
| story-005 | **走查半边 AC-11-12 / AC-11-13 / AC-11-21 全 NOT-RUN**(须人工执行 + 主创 / 医学从业 / 音频 lead 签核)· 手柄面 | 走查文档与截图未产出 · ADR-013 假设 6(UI Toolkit 手柄焦点导航) |

> ⚠️ **AC-11-10 的口径降级**(非静默):「双路径对拍」降为 AC-10-06b 逐位字段比较
> —— 两条路径载荷形状不同,已在 story-002/003 文件头登记。

### 2026-10-07 增量:DC-2 / DC-6 **校验机制**落地(判据本体已于同日随真源落地转 RUN —— 2026-10-15 登记回刷)

> 承 story-001 卡面授权「校验机制 + 负夹具以影子枚举证明可跑」,本轮把两条**从未有执行体**的
> 构建期判据建成可跑的机器。**判据本体维持 `NOT-RUN`**(真源仍缺席,见下表)——
> 本轮证的是「机制可跑、可红、非静默」,**不是**「11 的表已合规」。
>
> - **DC-2 此前零校验**(`action_id` 读入后从不校验,任何 int 放行);
>   **DC-6 的比较器此前从未接进烘焙门**(只在 Sim 有实现)。
> - 新增 `PrescriptionActionIdRegistry.cs`(影子闭集 + 影子地板)+ binder 接线 +
>   **覆盖率显式记账** + 菜单侧**生产路径报出**(此前影子诊断在非测试代码中零消费者)。
> - 评审原件:`production/qa/evidence/review-prescription-dc26-shadow-2026-10-07.md`
>   (双代理一轮;代码面 CHANGES REQUIRED 8 条 + 测试面 4 条必须修,全部处置或显式登记)。
> - 测试:filtered **174/174/0** · 全量 EditMode **2935/2888/0 红**。
>   **5 条新守卫经突变验证有抓错力**(注入改坏点 ⇒ 实测红 ⇒ 还原)。
>
> ✅ **2026-10-15 登记回刷(S11-4)**:上列前两项**已由真源落地消解** ——
> DC-2 的 master **已登记**(`disease_action_axis.json`,`2a7764f`)· DC-6 的常量**已具名**
> (`NOISE_BAND_POTENCY_9`,`entities.yaml:1484`)⇒ 两条判据本体 **RUN**;
> 另 **AC-11-22 的 7a 白名单半边**由 S11-4 落判据(白名单 = `EventKind` 闭集 ∩ codec 分派表,
> 载体识别 —— AC 文本一字未动;原「7a 存档白名单**表**未落地」的读法已订正)。
> ✅ **2026-10-15 同日追加**:AC-11-22 的**另一半边(半边甲 · 文本背离)**已由 **TD 裁「收窄」**并落盘 ——
> AC 文本 · `tr-registry.yaml:TR-prescription-018` · `traceability-index.md` · 门注**四件同批订正**(收窄 + 具名豁免)。
> ⚠️ **仍未闭**(禁借绿):**DC-6 对当前数据集零求值**(`salicylic_acid` 的 `dose_range = null`,已由覆盖率记账逐字声明)·
> BL-7(21a `drug_potency` 域未声明)· 两条已知测试弱点(M1 反守卫用干净夹具 / M4 正向判别力过窄,已登记不静默)。
> ~~AC-11-22 半边甲(文本背离)~~ ✅ **已闭 2026-10-15**(TD 裁「收窄」· 见上 §Epic Status 表行与订正框)。
