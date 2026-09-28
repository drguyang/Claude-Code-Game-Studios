# Story 005: 戥子输入与方笺呈现 —— 离散整数档与黄铜读数

> **Epic**: 处方用药
> **Status**: Ready
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 10h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/prescription-and-medication.md`(规则十三 戥子输入契约四条 · 规则十二 方笺 = 39 同一本书(已裁) · UI-11.1 归属切分 · UI-11.2 硬约束 + 两项对 42 的规格义务 · §Game Feel · AC-11-12/13/18/21 · 规则八 呈现 = 古籍功效词)
**Requirement**: TR-prescription-008(剂量整数档、无 `hi+1` 档 = 物理限位非运行期 clamp;不读严重度替玩家调剂量)· TR-prescription-011(indications/contraindications 只呈现不拦不扣;病种 id 不进呈现层)· TR-prescription-014(11 不请求相机档位;方笺裁定后为已裁终态)· TR-prescription-017(熟练度出口 = 省料+解锁,非药效 —— 影响呈现:无「药效放大」读数)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(规则十三:**float→int 量化在 3 / 表现层完成,11 只见整数**;禁手势连续拖拽 —— 两栈皆无实现路径;选剂 = 焦点移动 + 确认键)· ADR-013 **§十-B Amendment B**(义务 ② 已由 42 认领 = **黄铜侧元件**;禁降级数字角标,**两栈皆禁、无例外**;`AC-42-F3` 材质分支须归黄铜;`ModalId` 闭集 7 员**不因方笺增员**)· ADR-013 §五(焦点走官方桥 `NavigationMoveEvent`/`FocusController`,**不自实现焦点算法**;焦点单栈门;两栈共用同一 EventSystem)· ADR-020 §五/§六(相机只读不持状态;11 不发档位意图 —— 39 是 `Casebook` 档唯一请求方)· ADR-018(无提示音铁律;`AC-11-21` 与 `AC-44-09` 联合)· ADR-014(功效词 = 烘焙期转出字符串,零运行期查表)
**ADR Decision Summary**: 戥子档位 = **离散整数档的焦点序列**,`dose_range` 每一档 = 一个焦点落点;`hi` 档之后**不存在下一档**(限位由档位集合本身给出,非 clamp ⇒ 玩家不会「以为选了 hi+3」);非颜色读数 = **戥杆倾角 / 药包鼓胀**(离散档 ↔ 离散形态,黄铜侧錾刻刻度/机械位移合法,墨侧禁刻度条);档数上限旋钮 `MAX_DOSE_DETENTS`(建议 ≤ 9 防 Fitts 长路径,**值归用户**);方笺 = 脉案**同一本书内的一页** ⇒ 不成新模态、不发相机意图。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH(承 ADR-013 §6.6 **假设 6「UI Toolkit 运行时手柄焦点导航可用」= ⚠半可信**;记忆库裁定手柄焦点缓办,集中到桌面调试轮 —— 本 story 手柄 [U] 子项按 NOT-RUN 如实登记,禁借绿)
**Engine Notes**: 元件取自 42 自建 USS/UGUI 元件库(skeuomorphic-ui epic);戥子读数元件为**黄铜侧**(UGUI world-canvas 或 UI Toolkit 中的黄铜分支,实现期由 42 侧元件库落点决定);墨/黄铜双分支断言 = `AC-42-F3`。

**Control Manifest Rules (this layer)**:
- Required: 呈现层只渲染永不持游戏状态(承 ADR-013 §9 C3);剂量读数只由形态承载;功效词只取烘焙词表;焦点顺序在档位数变化时仍确定
- Forbidden: 数字/百分比/推荐排序/适应症命中勾选/禁忌弹窗/按键提示浮层(承 `AC-3-F1a`)/颜色作为唯一读数通道/运行期 clamp 反馈/数字角标或 `X/N` 计数(两栈皆禁无例外)/状态播报音(jingle/sting/ducking)
- Guardrail: [L] 走查判据(AC-11-12/13/21)不可被自动化抵;自动化只判结构半边(焦点落点数 == `hi−lo+1`、零 clamp 节点、DTO 洁净)

---

## Acceptance Criteria

*From GDD `design/gdd/prescription-and-medication.md`, scoped to this story:*

- [ ] **AC-11-18**[A] BLOCKING(两侧断言,分工写明):`GIVEN` `dose_range.hi`,`WHEN` 检查档位生成,`THEN` **不存在第 `hi+1` 档** —— ① **11 侧**:代码零 clamp 路径(静态断言:无 `Math.Min/Max`/`clamp` 于剂量链路;越界输入在 11 不可达,因焦点落点集合即档位集合);② **42 侧**:无「下一档」焦点落点(焦点序列长度 == `hi − lo + 1`;在 `hi` 档按「下一档」⇒ 焦点不动且有纸面反馈,无错误音)。⚠ 本条 ② 的实体元件归 skeuomorphic-ui epic 交付,本 story 为消费方走查 ⇒ **BLOCKED-BY-42 元件落地**,禁借绿
- [ ] **AC-11-13**[L] BLOCKING(可判 ≠ 已判):**色盲模拟 + 静音**下一次开方,人工走查 —— 玩家仅凭形态(戥杆倾角 / 药包鼓胀)分辨相邻两档。义务 ② 前置已由 42 于 2026-09-21 认领解除 ⇒ 本条**可判**;走查须实现轮执行,**当前状态 `NOT-RUN`**(禁借绿);`ELSE` 记 42 元件库缺口
- [ ] **AC-11-12**[L] BLOCKING:一次开方人工走查 —— 零数字 / 零推荐 / 零匹配提示 / 零禁忌弹窗;剂量由拟物载体可读(主创签核)。口径澄清照录:42 的 2026-09-17 收窄 = **墨侧禁刻度条、黄铜侧錾刻刻度合法**;本条不要求数值读数,故不与「零数字」冲突
- [ ] **无降级读数**[A]:戥子读数元件**不得**含数字角标 / `X/N` 计数节点 —— 自动化树遍历断言(42 侧 `§Visual 二` 两栈皆禁、无例外的镜像);`AC-42-F3` 材质分支须把该元件归**黄铜分支**(分支断言)
- [ ] **输入契约(规则十三)**[A]:11 侧只收**整数档序数**;float→int 量化住 3 / 表现层(反射断言:11 的 `Prescribe` 入参无 `float`/`double`;承 ADR-020 `AC-20-03` 「判据 = 反射断言,不是 grep」先例);禁在手柄/键鼠任一栈出现连续拖拽手势路径(42 不持跨源手势状态)
- [ ] **焦点官方桥**[A]:档位焦点移动经 `NavigationMoveEvent`/`FocusController` 自动邻居,**11/42 皆不自实现焦点算法**;**同键双触发禁止**;焦点单栈门(同一时刻仅一栈接收导航意图流);两栈共用同一 EventSystem(结构断言:场景/装配零第二 EventSystem)
- [ ] **`MAX_DOSE_DETENTS`**[A]:档数上限旋钮存在且值域可断言(建议 ≤ 9 为**登记值,最终值归用户数值轮**);超限 ⇒ 构建期/装载期告警或硬失败(实现期与 42 定级);测试以合成 `hi−lo` 扫 1…9 证明焦点序列随档数确定生成
- [ ] **方笺 = 同一本书**[A]:开方界面**不成新模态**、`ModalId` 闭集**仍 7 员**(不因方笺增员;第七员 `PaperCloseup48` 与本条**无关**,两处「勿混读」警示须同时通过)、11 **零相机档位意图**(反射断言:11 公开面无 `ICameraRig`/档位意图调用;`TR-prescription-014` 已裁终态)
- [ ] **词表呈现侧(TR-prescription-011)**[A]:方笺上的适应症/禁忌 = **古籍功效词**(取 `materia_lexicon.cooked`),`THEN` ① 呈现字段递归扫(`PresentationDtoGuard`)零 `disease_id`/病种键外露;② 零「命中」勾选/高亮/排序;③ 零拦截与零扣减联动(禁忌命中 ⇒ UI 无差异)
- [ ] **AC-11-21**[L] BLOCKING(原 `AC-11-13` 号位,R-3 重划):全部处方音效人工听测 ⇒ **零状态播报音**(无给药 jingle / 生效提示),与 `AC-44-09` 联合;白名单断言(触发源 ∈ 行为反馈白名单)为自动化半边,听测为签核半边
- [ ] **手柄路径(键鼠先交付)**[U]:键鼠焦点走完全部档位 + 确认键落笔可完成;手柄(无指针)面 —— 同一路径 + `hi` 限位处焦点不动反馈 —— **`NOT-RUN`**,BLOCKED-BY 桌面调试集中轮(记忆库裁定)与 ADR-013 假设 6 spike
- [ ] **零进程态可重建**[A]:11/呈现层无「当前剂量」游戏状态驻留(状态在表现层焦点上;重开方笺 ⇒ 由库存+已落流事件重建视图,不读缓存);承 `11 无进程态`(Edge Case「存档中途」行)

---

## Implementation Notes

*Derived from 规则十三/十二 + UI-11.2:*

1. 读数元件消费方写法:本 story **不实现**倾角/鼓胀元件,只声明需求 + 走查 + 结构断言;元件缺失 ⇒ 回写 skeuomorphic-ui epic 登记,不私加临时实现(与 diagnosis story 006 同一纪律)。
2. 档位焦点序列由 `dose_range` 生成:`lo…hi` 各一落点;`dose_range` 为空 ⇒ **单一落点「整剂」**(与 story 004 的 AC-11-17 整剂路径同形,呈现上不是「0 档」)。
3. 量化落点:3 侧(输入 epic)完成 float→int;本 story 只断言 11 入参整数 + 呈现层不产浮点读数。**禁**在 11 内做浮点量化(破 ADR-006 边界)。
4. 「限位可见」的表达手法(黄铜侧机械位移:戥杆到顶 / 挡针)属 42 元件语义,本 story 记录走查判据「玩家在 `hi` 档知道自己到头」,不指定实现。
5. 静音+色盲走查夹具:Unity 内置 Color Blindness 模拟(渲染侧)+ AudioMixer 静音快照(呈现侧,非状态播报通道,不违 ADR-018);两条件同时。
6. 方笺页在脉案模态内 ⇒ 与 39 共用 `Casebook` 档;施治意图承 8 侧 `S-8.4` 路线甲(查体=脉案行级动作 / 施治=方笺落笔 → 11),本 story 不新立词表(规则十三:11 不自立,登记为对 4 首轮的引用)。
7. 证据落点 `production/qa/evidence/prescription-medication/`(目录全仓从未建立,首批建目录带 README;签核位留主创 + 医学从业双栏)。

## Out of Scope

- Stories 001–004:表、求值、事件、流程(本 story 只输入与呈现)
- 42 元件库本体(戥子倾角/鼓胀、纸纹/墨迹/卷轴九宫格 —— skeuomorphic-ui epic 交付;本 story 是消费方)
- 3 侧 float→int 量化实现(input-system epic story 006/007;本 story 只断言 11 的入参形状)
- VR/world-space 方笺(ADR-013 推 P1a)
- 手感与音效素材(44 epic + audio 资产轮;本 story 只判白名单与零播报)
- 48 教学的开方段/tutorial-onboarding epic

## QA Test Cases

*Written at story creation(lean mode).*

- **无 hi+1 落点**: `dose_range=(2,5)` ⇒ 焦点序列长度 3;在 5 档按下一 ⇒ 焦点不动(AC-11-18 两侧)。
- **零 clamp**: 剂量链路静态扫无 clamp 调用;越界输入在 UI 不可达(AC-11-18 ①)。
- **入参整数**: 反射扫 `Prescribe` 签名 ⇒ 无浮点类型(规则十三,AC-20-03 先例)。
- **单落点整剂**: 空 `dose_range` 药 ⇒ 焦点序列恰 1 落点「整剂」,非 0 落点(联动 AC-11-17)。
- **降级读数零节点**: 树遍历无数字角标/`X/N`;材质分支 = 黄铜(无降级读数 + `AC-42-F3`)。
- **闭集仍 7**: `ModalId` 枚举成员数 == 7 且不含方笺项;11 零相机意图(方笺裁定)。
- **词表洁净**: 呈现字段递归扫无病种键;禁忌命中前后 UI 哈希相等(TR-prescription-011)。
- **单栈门**: 构造双 EventSystem 夹具 ⇒ 结构断言红(焦点官方桥)。
- **走查(人工)**: 色盲+静音分辨相邻档(AC-11-13 NOT-RUN 待实现轮)/ 零数字零推荐(AC-11-12)/ 静音听测(AC-11-21)。

## Test Evidence

**Story Type**: UI
**Required evidence**: `unity/Assets/Tests/PlayMode/PrescriptionMedication/dentch_input_test.cs`(结构半边:焦点落点数、入参类型、DTO 洁净、闭集与意图零调用)+ 走查文档与截图 `production/qa/evidence/prescription-medication/story-005-*.md`(主创签核 AC-11-12;主创+医学从业 AC-11-13;音频 lead AC-11-21)
**Status**: [ ] Created — NOT STARTED;AC-11-13 / AC-11-12 / AC-11-21 走查子项 `NOT-RUN`(可判 ≠ 已判);AC-11-18 ② 与手柄面分别 BLOCKED-BY-42 元件落地 / 桌面调试集中轮 + 假设 6 spike

---

## Dependencies

- Depends on: Story 001(功效词表 cooked)、Story 004(`Prescribe` 整数入参契约)、skeuomorphic-ui epic(戥子黄铜元件 + `ModalId`/焦点桥/单栈门 + `AC-42-F3` 分支)、input-system epic(3 侧量化 + 手柄绑定)、casebook epic(39 脉案档/同一本书的载体)、player-controller epic(2 档位表:11 不增行)、audio-system epic(44 白名单断言口径)、interaction-system epic(4 的 `InteractIntent` 词表先例,规则十三引用)
- Unblocks: EPIC DoD;`/ux-design` 处方 UX spec 的验收面(本节为上游输入);AC-42-F1 全 UI 走查批次

## Completion Notes
