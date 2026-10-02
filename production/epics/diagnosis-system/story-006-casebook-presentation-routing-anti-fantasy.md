# Story 006: 呈现层 —— 脉案五通道、动作词表路由与反幻想护栏

> **Epic**: 诊断与体征揭示
> **Status**: Ready
> **Layer**: Feature
> **Type**: UI
> **Estimate**: 10h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/diagnosis-system.md`(V-8.x 呈现规格 · UI-8.2 焦点导航 · UI-8.4 负向声明 · S-8.4 动作词表(路线甲模态分流,AC-8-51/52)· 规则六 落笔=手写病名+置信度循环(OQ-CB-5 甲:置信度不落独立控件,K=6 焦点元素)· 规则九 主角侧渗墨)
**Requirement**: TR-diag-019(disease_id 不进呈现层)· TR-diag-020(脉案 DTO 静态检查)· TR-diag-006(每人一本脉案的呈现面)· TR-diag-015(D-8-10)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-013(UI Toolkit 主;`ModalId` 闭集 7 员含脉案模态;焦点官方桥 `NavigationMoveEvent`/`FocusController`,**不自实现焦点算法**;焦点单栈门;两栈共用同一 EventSystem; USS 元件库纸纹/墨迹/九宫格)· ADR-011(S-8.4 输入侧:查体 = 脉案模态**行级动作**;急救 = 10 Armed 直读;**裸 Interact = 就诊 → 37**;施治 = 方笺落笔 → 11)· ADR-018(无提示音;「旧」不重复 sting)· ADR-020 §五(相机只读不持状态;档位意图命令)
**ADR Decision Summary**: 脉案 = 纸质拟物面板,五行固定成序**永不隐藏永不留空**;焦点顺序恒为 `面色→语声→姿态→呼吸→触感→病名→置信度`(K=6 为病名/置信度循环,置信度不落独立控件);精度档差异**只在局部层**(唇/甲床/结膜/啰音/三凹征/相对缓脉/肝脾界),整体层不因档位变;把握度不足**只由墨色/笔迹承载,不加任何文字或标记**;阴性形态**不触碰世界层渲染**(硬禁令);主角侧走渗墨语汇且**不进脉案**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH(承 ADR-013 §6.6 **假设 6「UI Toolkit 运行时手柄焦点导航可用」= ⚠️半可信**,原型 spike 为前置;记忆库裁定:手柄焦点缓办,集中到桌面调试轮 —— 本 story 的手柄 [U] 子项按 NOT-RUN 如实登记,禁借绿)
**Engine Notes**: UXML/USS 元件复用 skeuomorphic-ui epic 的自建元件库(纸纹/墨迹/卷轴九宫格 `-unity-slice-*` + 主题变量);VR/world-space 推 P1a 不在本 story。

**Control Manifest Rules (this layer)**:
- Required: 42 侧只渲染,永不持游戏状态(只读 `VitalsDto`/判断态哨兵);负向声明全项为零;渗墨语汇仅主角侧
- Forbidden: 血条/数值/百分比/进度条/图鉴/自动补全(含手写输入框的自动补全)/「X/5 已查」计数/灰按钮/「?」出现在体征栏/镜头效果报状态(ADR-020 §六)
- Guardrail: [V]/[U] 判据 = 截图 + 主创/医学从业签核,不可自动化抵;自动化只判静态半边(DTO/字段)

---

## Acceptance Criteria

*From GDD `design/gdd/diagnosis-system.md`, scoped to this story:*

- [ ] **AC-8-23**[U] BLOCKING(显式半边):未查必须留下一行 —— 只做视诊 ⇒ 触诊所辖行为**空行**;五行固定成序、永不隐藏、永不留空;焦点顺序恒 `面色→…→置信度` 不因已查集合重排;零进度条/零计数/零完成度
- [ ] **AC-8-36**[V] BLOCKING:精度档 = 解析带宽不是遮挡开关 —— Lv1 vs Lv50 两图:低档画面**完整干净诚实**;零全屏模糊/马赛克/雾遮/灰色未解锁/「技能不够」提示;差异只在局部层;整体层不变
- [ ] **AC-8-37**[V] BLOCKING(须医学从业签核):最危险三点不许「装傻」—— `sign_orthopnea`/`sign_retraction`/`sign_pallor`:要么整体层本可见(如实给粗档词如「躺不平」),要么**根本不在低档世界里成形**;绝不呈现教科书征象配含糊词
- [ ] **AC-8-38**[V] BLOCKING(渲染路径断言半边 [L]):阴性绝不抹掉世界上的体征 —— `Sign ≥ FLOOR` 且持续型而精度不足指名 ⇒ 世界侧照旧渲染(病人看起来就是不对的),只有纸上是阴性形态;断言阴性渲染路径不触碰世界层(13 动画/shader)
- [ ] **AC-8-39**[U] ADVISORY:「?」不许溢出 —— 体征栏零「?」/零标签/零 badge/零变相数值;「?」只在病名栏(区分确定/疑似)
- [ ] **AC-8-40**[V]:主角侧不进脉案 —— 主角体征不出现在任何脉案/面板/HUD;全走世界层(视野边缘**水墨渗边**、呼吸、步态);无未查/阳性/阴性状态机;渗边非干净暗角、无脉冲闪烁;任何诊断等级下呈现不变
- [ ] **AC-8-41**[V]:两套语汇不互染 —— 渗墨只用于主角侧;病人侧永不使用该语言(病人侧区别只由体征本身承担)
- [ ] **AC-8-42**[V] BLOCKING:V-8.6《他回来了》—— 神经衰弱病人:(a) 五行**实笔「无异常」五行俱在**(非空行);(b) 世界上有生命体征(呼吸起伏/眨眼/视线跟随/可交谈);(c) 病名栏空白且可写性可见。⚠️ 「真的没事」与「系统没告诉你」物理可分。依赖注:曾标 `D-8-1`(9 R3 八病种含神经衰弱)—— 按 disease epic story 003 的 R3 表兑现判,**若该病种行缺失则记 BLOCKED-BY-disease-story-003,禁借绿**
- [ ] **AC-8-43**[U] BLOCKING:UI-8.4 负向声明 —— 全部诊断相关界面:零数值/百分比/进度条;零疾病名录/图鉴/**词条自动补全(含手写输入框)**;零 `READ_FLOOR`/技能等级/精度档显性提示;零「你已发现 N 个病例」计数;零诊断对错提示
- [ ] **AC-8-44**[U](手柄半边 NOT-RUN 缓办):UI-8.2 无指针焦点导航 —— 键鼠半边本 story 走查;手柄(无指针)面:焦点走完五通道+病名+置信度且顺序固定、落空行有反馈(纸面压痕声/行高亮)且无提示文案、无灰按钮、「旧」无 hover 可读(页边铅笔勾不靠颜色)、病名手写输入手柄下可完成 —— **登记 BLOCKED-BY 桌面调试集中轮(记忆库裁定)与 ADR-013 假设 6 spike**
- [ ] **AC-8-13**[L/I] BLOCKING(端到端呈现半边):Lv15 → 阴性形态+把握不足(**只由墨色/笔迹承载,零文字零标记**,不构成排除);Lv20 → 细档词且构成排除(V-8.3 / 阻断 #8 口径)
- [ ] **AC-8-19**[V] BLOCKING:「?」不泄漏(表现层)—— 有体征但低于门槛 vs 确实无体征,两病人同通道同低熟练度两张脉案**像素级不可区分**(同词/墨浓度/收锋/形态/页边标记)
- [ ] **AC-8-51**[I] BLOCKING:S-8.4 词表闭合 —— 裸 `Interact` 打病人四种粗状态(未立案/已立案未查/已落笔/10 Armed 期)语义**恒为「就诊」**不漂移;`Armed` 期该意图被压制(story 005 of emergency 联动);未立案 ⇒ 37 `CaseOpened`
- [ ] **AC-8-52**[A] BLOCKING:边界 B-1/B-2 —— ① 结案前后 4 的输出形状不变:4 仍只交 `(病人, InteractIntent)` 全整数零语义字段(词表在 8 侧,4 无 `TreatmentIntent` 枚举膨胀);② 裁决输入集不扩大 —— 只读已落盘 `IModalState.Modal`(反射扫描断言)
- [ ] **TR-diag-019/020**[A] BLOCKING:脉案全部 DTO 静态检查 + `PresentationDtoGuard` 递归(disease_id 不进呈现层;story 001 挂入门禁,本 story 判呈现字段集实际内容)
- [ ] **AC-8-29 截图半边**[V] ADVISORY:改写留痕可被看见(划痕),痕不进任何评分输入
- [ ] **AC-8-24/28 走查半边**[U]:态数无第四态、无「未查」文案;空白病名栏 = 可写性可见的纸
- [ ] **AC-8-46 走查半边**[U] ADVISORY:掉级不提示不解释
- [ ] **AC-8-31 [U] 互证**:无灰按钮(P0 无不可用动作)与「等级不锁手段」两向走查

---

## Implementation Notes

*Derived from V-8.x / UI-8.x + ADR-013/011:*

1. 脉案 = UI Toolkit(UXML/USS)屏;元件全部取自 42 的元件库(story 若发现缺元件 ⇒ 回写 skeuomorphic-ui epic 登记,不私加)。
2. 把握度不足的实现**只有两个通道**:墨浓度 USS 变量 + 笔迹字形选择器;**禁止**新增文字节点/badge/图标(AC-8-13/22 的呈现侧铁律)。
3. 「旧」的页边铅笔勾 = 独立装饰层,不依赖 hover、不依赖颜色(无障碍:承 `design/accessibility-requirements.md` 档位 Standard + L-1/L-2)。
4. 焦点:键鼠/手柄共用官方桥路径;本 story 先交付键鼠 + 焦点顺序固定性,手柄子项留 spike(缓办裁定)。**同键双触发禁止**(ADR-013 承)。
5. AC-8-51 的路由表 = S-8.4 路线甲实现:词表住 8,交互系统(4)只见 `InteractIntent` 整数;结案不改 4 输出形状(词表不反灌)。
6. 像素级不可区分(AC-8-19)的自动化:截图后哈希/差分断言(PlayMode + 截图基线);医学从业签核项(AC-8-37/42)须证据文件留签核位,不得以开发自审抵。
7. `production/qa/evidence/diagnosis-system/` 为 [V]/[U] 证据落点(目录全仓从未建立,首批建目录带 README)。

## Out of Scope

- Stories 001–005:边界、数据、公式、状态机(本 story 只呈现与路由)
- VR/world-space 脉案(ADR-013 推 P1a)
- 42 元件库本体建设(skeuomorphic-ui epic;本 story 是消费方)
- 手柄 spike 排程(生产侧统一插档,记忆库裁定)
- 53 结算呈现(奇遇/后果 epic)

## QA Test Cases

*Written at story creation(lean mode).*

- **空行五行在**: 只查视诊跑一遍 ⇒ 截图五行俱全,触/叩/听/问行零字零标记(AC-8-23)。
- **不遮挡**: Lv1/Lv50 截图对 ⇒ 全图无模糊雾遮(零遮挡滤镜节点断言 + 截图);差异限定在局部层词与墨(AC-8-36)。
- **世界不被抹**: 阴性形态夹具跑 13 渲染层 ⇒ 动画/shader 输入未变(AC-8-38)。
- **泄漏像素测**: 两病人截图逐像素哈希相等(AC-8-19)。
- **词表闭合**: 四态各按一次 Interact ⇒ 语义恒就诊;Armed 期计数 0(AC-8-51)。
- **4 形状不变**: 结案前后反射扫 4 出境载荷 ⇒ 字段集相同(全整数)(AC-8-52)。
- **负向声明清单**: 遍历界面节点:无进度条/计数/补全/等级提示控件(自动化树遍历 + 走查双签)(AC-8-43)。

## Test Evidence

**Story Type**: UI
**Required evidence**: `unity/Assets/Tests/PlayMode/DiagnosisSystem/casebook_render_test.cs`(自动化半边)+ 走查与截图 `production/qa/evidence/diagnosis-system/story-006-*.md`(医学签核位:AC-8-37/42)
**Status**: [ ] Created — NOT STARTED;AC-8-44 手柄子项 NOT-RUN(缓办+假设 6 spike);AC-8-42 视 disease story 003 兑现判

---

## Dependencies

- Depends on: Story 001(DTO 门禁)、Story 003/004(词/哨兵/把握度值)、Story 005(四态/三态状态机输出)、skeuomorphic-ui epic(元件库 + ModalId/焦点桥)、casebook epic(39 脉案档与两 tick)、interaction-system epic(4 的 InteractIntent)、case-system epic(37 就诊/结案)、emergency-procedures epic story 005(Armed 压制对偶)
- Unlocks: EPIC DoD;AC-42-F1 式全 UI 走查批次;ux-review 轮

## Completion Notes
