# Accessibility Requirements: 大医精诚:破晓之剂

> **Status**: Committed(档位已裁,矩阵为初版)
> **Author**: ux-designer(经用户裁定 2026-09-20)
> **Last Updated**: 2026-09-20
> **Accessibility Tier Target**: **Standard**
> **Platform(s)**: PC(Steam)优先;VR(OpenXR)仅急救小游戏;主机延后至 P2
> **External Standards Targeted**:
> - WCAG 2.1 Level AA(UI 文本对比 / 字号)
> - AbleGamers CVAA Guidelines
> - Xbox Accessibility Guidelines (XAG):N/A —— 主机 P2 前不承诺;提级 P2 时须重审本档
> - PlayStation / Apple / Google:N/A
> **Accessibility Consultant**: None engaged
> **Linked Documents**: `design/gdd/systems-index.md`, `design/ux/interaction-patterns.md`,
> `docs/architecture/adr-011-input-architecture.md`, `docs/architecture/adr-013-skeuomorphic-ui-framework.md`,
> `docs/architecture/adr-018-audio-architecture.md`

> **路径权威声明(2026-09-20 冲突裁决)**:本文件的路径为 **`design/accessibility-requirements.md`**
> (根 `design/` 下,非 `design/ux/`)。三处独立口径一致:`docs/WORKFLOW-GUIDE.md:532`、
> `.claude/skills/gate-check/SKILL.md:114`、`.claude/skills/ux-design/SKILL.md:92`。
> 早期交接模板中出现的 `design/ux/accessibility-requirements.md` 为误载路径,**该文件不存在,也不创建**
> (不留薄壳 —— 一个可被误读的第二个权威件正是本声明要防的)。

> **本文件与其他权威件的关系**:无障碍承诺**不得覆盖**已 Accepted 的 ADR 裁决。
> 具体硬冲突两处(急救 `L_input < 50 ms` 预算、`hold_ticks` 长按判定语义,见 §Known Intentional
> Limitations L-2)已按「ADR 优先、限制显式化」处置;**若用户日后改判,改本文件,不改 ADR**。

---

## Accessibility Tier Definition

### Tier Definitions

| Tier | Core Commitment | Typical Effort |
|------|----------------|----------------|
| **Basic** | 关键文本可读;无功能仅靠颜色;三路音量独立;可无光敏风险通关。 | Low |
| **Standard** | Basic 全部,加:输入重映射(全平台)、字幕含说话人识别、可调字号、至少一种色盲模式、定时输入可延长或切换。 | Medium |
| **Comprehensive** | Standard 全部,加:菜单读屏器、mono 音频、难度辅助、HUD 重定位、减动效、关键音频的视觉指示。 | High |
| **Exemplary** | Comprehensive 全部,加:字幕完全自定义、高对比模式、认知辅助工具、触觉替代、第三方审计。 | Very High |

### This Project's Commitment

**Target Tier**: **Standard**(用户裁定 2026-09-20,含两条带日期的显式限制 → §Known Intentional Limitations)

**Rationale**:

1. **本项目的主导障碍是视觉与认知,不是动作**。拟物 UI(脉案 / 出诊箱 / 纸质地图)意味着
   大量阅读型界面信息,无血条 / 无小地图的拟物铁律把「状态读取」全部压到了**外观、姿态、
   呼吸等间接通道**上 —— 这既抬高低视力玩家的门槛,也抬高认知负荷门槛。Standard 的字号 /
   对比 / 色盲三项正面覆盖视觉轴。
2. **面色通道是病症判读的一部分**(8 诊断五通道:面色 / 语声 / 姿态 / 呼吸 / 触感)。
   色觉异常玩家失去的不只是「UI 好不好看」,而是**一个诊断信息通道** —— 这是全案无障碍
   问题的峰值,处置见 §Visual 的色-only 审计行(非色备份 = 其余通道 + 脉案文字读数)。
3. **手柄必须完整可用但不是一等公民**(`technical-preferences.md` 裁定),而拟物纸面 UI
   没有指针 —— 焦点导航路径的完整性是**可玩性级**要求(断头 = 不可玩,见 `input-system.md`
   :99),不是体验优化。
4. **急救动作是定时 + 长按输入的集合**,Standard 的「定时输入可延长」在本项目**不能**
   按模板字面实现(长按质量本身是判定输入,承 ADR-011 Amendment B)—— 已裁的替代形态
   见 L-2,这是档位承诺被项目自身裁决**收窄**的地方,显式记账。
5. 团队 = 单人 + agent 编队,无独立无障碍预算;Comprehensive 的读屏器与 HUD 重定位
   直接撞 ADR-013 未 spike 的 HIGH 风险面。**掉到 Basic 的代价**:手柄焦点路径与色盲
   面色通道无承诺 —— 对本项目是两个结构性缺口,不可接受。

**Features explicitly in scope (beyond tier baseline)**:
- **手柄焦点导航全栈完备**(脉案 / 出诊箱 / 纸质地图 / 存档位 / 教学 五类纸面界面均可纯手柄走通)
  —— 承 `TR-concept-008`,这是 Standard 基线之上的**本项目特有硬义务**。
- **无提示音机械化 = 音频永不单独承载状态**(ADR-018 AC-44-09 白名单)⇒ 模板中
  「audio-only 信息的视觉指示」一项在本项目由**设计本身结构性满足**(状态播报根本不存在
  音频通道,见 §Cognitive 该行的项目化改写)。

**Features explicitly out of scope**:
- P0 游戏内改键 **UI**(键位持久化机制存在,编辑界面推迟)→ L-1。
- 判定层长按的 toggle 替代(改判语义)→ L-2。
- 菜单 / 世界读屏器(Comprehensive 项)、HUD 重定位( Comprehensive 项且与拟物无 HUD 冲突)。

---

## Visual Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| 最小字号 —— 菜单 / 纸面 UI | Standard | 全部 UI Toolkit 屏(脉案 / 存档位 / 主菜单) | Not Started | ≥24px @1080p 基准;**纸纹底上的墨色字**对比另见下行。落点 = ADR-013 §三 自建 USS 元件库的主题变量(字号走 `--font-size-*` 变量,不散写) |
| 最小字号 —— 世界内纸面(48 教学 / 39 脉案近景) | Standard | UGUI world canvas + 42 的 `ModalId.PaperCloseup48` 近景模态 | **🚧 BLOCKED-BY → 预期可解(2026-09-21)** | ⚠️ 48 首轮评审三 BLOCKING 同根因「世界内的纸 P0 无渲染形态」;字号判据**待 OQ-48-4/5 与 OQ-42-5 并批裁定后**才可测。**✅ 二者均已闭合(2026-09-19 / 2026-09-19 上批)**:**48 侧 `OQ-48-4/5` 已裁**(纸 = 纯装饰不可拾 · 走近 → 42 近景模态 `ModalId.PaperCloseup48` 闭集第七员)·· **42 侧 `OQ-42-5` 已闭合**(39 = 独立系统,布局语义归 39,42 只渲染)。**剩实现前置 = `ModalId.PaperCloseup48` 的成员登记**(42 修订轮,与 `OQ-48-8` 耦合 · 该页已在 `interaction-patterns.md:65` 具名、`OQ-48-4` 已裁其为「闭集第七员」,但 `AC-42-F1` 的闭集点名文本目前仍是**六项** —— 第七员登记 = `OQ-48-8` 的**待办**而非已完成)。**判据仍不可测**(渲染形态未落地),故本行**仍不记绿** —— 状态改「预期可解」,阻塞根因已消。实现前置 = P0 平面下的 UGUI world canvas 渲染路径(ADR-013 2026-09-16 已把 world-space 最小面提到 P0) |
| 文本对比 —— 墨色字 on 纸纹底 | Standard | 全部拟物文字 | Not Started | **≥7:1**(2026-09-21 AB-4 提档;原 ≥4.5:1 WCAG AA 之上升了一档,与 art bible §4.5 底线合一)。⚠️ 拟物纸纹是**纹理不是纯色** ⇒ 对比须按纹理最亮像素测;自动化 = 对 USS 主题色值 + 纹理烘焙图跑对比器 |
| 色盲模式 | Standard | 面色通道 + 生态区地图渲染 + 病例墨水标 | Not Started | 本项目色觉依赖面窄但**深**(见下行审计);实现 = UI 调色板变体(烘焙数据换表,走 ADR-014 管线),**不引入后处理全屏滤镜**(撞 ADR-020 §六「镜头效果不得用于报状态」) |
| 色-only 指标审计 | Basic | 全 UI + 全「外观即状态」面 | Not Started | 见下表;面色一行是**判定信息通道**级,不是装饰级 |
| UI 缩放 | Standard | 全部 UI Toolkit 元素 | Not Started | 75–150%;UGUI world-space 侧同步(两栈语义对齐承 ADR-013 §三);缩放不破版 = 每张纸面布局的 AC |
| 亮度 / gamma 控制 | Basic | 全局 | Not Started | 图形设置暴露 + 校准参考图 |
| 闪烁 / 频闪审计 | Basic | 天气(5)/ 疫情 VFX / 急救失败呈现 | Not Started | Harding FPA:同屏 >3 次/秒的亮度过跨须整改;急救的「手稳」失败呈现**不得用闪屏** |
| 减动效模式 | Standard | UI 转场 / 相机抖动 / VFX | Not Started | 与 VR 舒适度是两个面:VR 侧站定式已裁(`technical-preferences.md`),平面侧相机效果承 ADR-020 §六(8 语义 + 2 实现);本开关 = 平面侧全量降档 |
| 字幕 —— 开关 | Basic | 语声内容(44 Voice 总线 / 语声变体库) | Not Started | 默认开(阅读型受众);44 无语音通道(ADR-018 §五 明写不做语音),字幕服务的是**语声变体的角色台词与病历朗读** |
| 字幕 —— 说话人识别 | Standard | 同上 | Not Started | 名字前置;颜色区分仅作假若**非颜色差异同时存在**(承色-only 铁律) |

### Color-as-Only-Indicator Audit(P0 已知项)

| Location | Color Signal | What It Communicates | Non-Color Backup | Status |
|----------|-------------|---------------------|------------------|--------|
| **面色**(8 诊断五通道之一) | 红 / 青 / 黄 / 白 / 晦暗 | 病症判读输入(可致死度误判) | 姿态 + 呼吸通道恒在;脉案文字读数(「Reading」事件流)复述已读出的面色词;⚠️ **色盲模式下面色词表是否仍可分辨 → `OQ-A1`(见 Open Questions)** | Not Started |
| 纸质地图墨点 / 领地渲染(6 / 39) | 深浅 | 生态区归属 | 区名落字 + 边界线型(虚 / 实);**P1a 才交付的「墨点」不在 P0 审计面**(`death-and-respawn` 裁定) | Not Started |
| 病例 / 脉案批注墨水 | 朱 / 墨 | 改写史 vs 初读史 | 笔迹字重 + 落笔时间戳(`ADR-008` 读数 / 落笔 / 改写史事件) | Not Started |
| 疗效反馈(处方见效) | —— | —— | **铁律:不用色不用音直接报状态**;见效只通过体征通道呈现(承 AC-44-09 同源铁律的视觉侧,ADR-020 §六)⇒ 本行**结构性无违规面** | N/A by design |

---

## Motor Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| 输入重映射(机制层) | Standard | 全部 action | **机制 Accepted · UI = L-1** | ADR-011 已裁:action 资产 + `bindings overrides` 持久化 + 绑重 GUID 稳定性;**P0 交付机制不交付编辑界面**(用户裁定 2026-09-16,`input-system.md` 四项裁定之一) |
| 输入方式热切换 | Standard | PC 键鼠 ↔ 手柄 | Not Started | 新 Input System 原生;提示图标随活动设备切换(`iconKey` 归 3,呈现归 42/48 —— 已裁) |
| Hold-to-press 替代 —— **表现 / 移动层** | Standard | 交互长按 / 拾取按住等 | Not Started | 一律提供 toggle 替代。**清单落点 = `input-system.md` 规则二的 action 资产**,新增 hold 动作时本文件同步 |
| Hold-to-press 替代 —— **急救判定层** | **受限** | `hold_ticks` 类输入(按压 / 通气节律) | **见 L-2** | 长按质量是 `EmergencyAttempt` 的**判定输入**(ADR-011 Amendment B;`TICK_PERIOD=50ms` vs CPR 周期 ≈550ms 的 11× 量化比已裁)⇒ toggle 化 = 改判语义。替代 = **判定宽容窗口倍率**(只扩判定窗,**不碰** `L_input < 50 ms` 预表现路径 —— 两条预算已由 OQ-25-8 裁定切分,禁止重并) |
| 快速输入替代 | Standard | 格斗线(25)连打 | Not Started | >3 次/秒的连打序列须提供单次 toggle / 自动重复替代;25 的非致命模型(昏迷)不受影响 |
| 输入时机调整 | Standard | QTE 式窗口 / 天气阻断(5) | Not Started | 0.5×–3.0× 倍率仅作用于**判定宽容**;急救的 `AC-10-08` 只测 `L_input`,倍率不得进入该测量路径 |
| 单手模式 | 受限 | 出诊箱开合 + 急救双手操作 | Not Started | P0 只承诺**界面层**单手可达(焦点导航单栈);急救双手动作的单手替代 = 判定层问题 → 与 L-2 同族,登记不承诺 |
| **手柄焦点目标尺寸**(2026-09-21 AB-3 新增) | Standard | 全部纸面界面可落点(脉案 / 出诊箱 / 纸质地图 / 存档位 / 教学 五类,承焦点导航全栈完备) | Not Started | 手柄焦点可落点 ≥ **44×44 逻辑像素 @720p 等效**(高于 WCAG 2.2 AA 的 24×24;承 art bible §3.3 命中区底线,2026-09-21 AB-3 升为承诺)。判据 = **焦点盒测量夹具**(自动化:焦点命中盒 bounding 尺寸下界断言);非按钮形态(字段 / 线格 / 留白栏)同等适用 ——「纸上没有按钮,只有栏」 |

---

## Cognitive Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| 难度选项 | Standard | 敌人强度 / 疫情事件率 | Not Started | ⚠️ 可调参数的**数值域归数值轮**(用户自调铁律);本文件只承诺「存在独立滑杆」这一形状,不代拍值 |
| 随处可暂停 | Basic | 全部状态 | Not Started | 联机暂停语义 = 本地暂停 UI + 主机 tick 不停(承 ADR-005 主机唯一执行 Step)—— 暂停不冻结他人模拟是**已裁事实**,须向玩家显式说明,避免「暂停失效」错觉 |
| 教学持久性 | Standard | 48 全部提示 | **BLOCKED-BY** | 「纸上的图」形态(42 `ModalId.PaperCloseup48`)已定,但渲染形态三 BLOCKING 未解(OQ-48-4/5)—— Help 召回路径的设计承诺成立,可测性待该批裁定 |
| 目标清晰度 | Standard | 主线 / 病例 | Not Started | **项目化改写**:无小地图铁律下,「2 次按键内看到完整目标」的载体 = **脉案 / 纸质地图本身**(打开即目标面),不走 HUD 任务追踪(那会破拟物支柱);验收 = 手柄从任意游戏态到脉案打开 ≤ 2 次按键 |
| 音频-only 信息的视觉指示 | Standard | —— | **N/A by design** | AC-44-09 白名单 ⇒ 状态不经音频播报,本项结构性满足(见 §Tier Commitment 第 4 点) |
| UI 阅读时间 | Standard | 纸面 tooltip / 批注弹窗 | Not Started | 含可操作信息的纸面提示**禁止自动消失**(拟物纸本就没有自动消失语义 —— 铁律与无障碍同向) |
| 认知负荷登记 | Comprehensive→**降为文档义务** | 每系统 | Not Started | 不承诺「认知辅助工具」(那是 Comprehensive),但每系统「同时追踪量」登记义务保留:`review-all-gdds` 3b 注意力预算审计即其读数器,>4 系统触发复审 |
| 导航辅助 | Standard | 开放世界 | Not Started | 快旅 / 路点 = **拟物形态内实现**(地图注记),不做屏幕路标 —— 与支柱一致;P0 交付面随 6 / 39 定 |

---

## Auditory Accessibility

| Feature | Target Tier | Scope | Status | Implementation Notes |
|---------|-------------|-------|--------|---------------------|
| 全部语声字幕 | Basic | 44 Voice 总线内容 | Not Started | 100% 覆盖;语声 = 变体库混合(AC-44 已裁),字幕源 = 变体表文本字段(ADR-014 烘焙数据) |
| 独立音量 | Basic | Master / Music / Ambience / Voice / SFX / Stethoscope / UICue | Not Started | **七路**(ADR-018 混音拓扑)≥ 模板要求的四路;Stethoscope 独立路是听诊玩法的事实,不是无障碍项 |
| 游戏关键 SFX 字幕 | Standard(自 Comprehensive **降级**) | 听诊音 / 天气阻断音 | Not Started | 降级理由:无提示音铁律使「音频携带的状态」几乎为空;残余 = **听诊器通道本身**(非连续水声的细湿啰音,AC-44-08)—— 听障玩家失去一个诊断通道,备份 = 脉案文字读数复述;逐条 SFX 审计表待 44 实现轮回填 |
| Mono 音频 | 不承诺(Comprehensive 项) | —— | N/A | PC Steam 侧以操作系统级折中缓解;P2 主机评审时重议 |

### Gameplay-Critical SFX Audit

> 结构性预期:**本表应当几乎为空** —— AC-44-09 白名单规定音频触发源 ∈ 行为反馈,
> 「报状态」的音效不存在。唯一预期常驻行 = 听诊通道(上表)。实现轮若入表新行,
> 即构成白名单断言的旁路证据 ⇒ 先查 AC-44-09,再填表。

---

## Platform Accessibility API Integration

| Platform | API / Standard | Features Planned | Status | Notes |
|----------|---------------|-----------------|--------|-------|
| Steam (PC) | Steam Input | 系统级手柄重映射(在 L-1 生效期间是**主要缓解路径**) | Not Started | 不豁免游戏内机制层重映射义务(ADR-011 bindings overrides) |
| VR (OpenXR) | 平台级 comfort / guard boundary | 站定式已裁;不做 locomotion | N/A 大部分 | 晕动症风险由**设计消解**(不承担开放世界移动),非由设置项消解 |
| Xbox / PS5 | XAG / Sony | —— | N/A · P2 | 提级 P2 时本文件须重审(Standard 是否仍够认证地板) |
| PC 读屏 (NVDA 等) | UI Toolkit 无障碍节点暴露 | **不承诺**(Standard 不含;UI Toolkit 运行时暴露面 = ADR-013 未 spike HIGH 风险) | Out of Scope | 若 spike 结果好,升级为超额交付,不作为承诺。**引用义务已兑现**:13 病人 AI 的 **`AC-13-F3`** 显式非目标 —— 「P0 不支持视障玩家独立定位不移动的重症病人」(`patient-ai.md:1077/1147`,其 `AC-13-F1/F2/F3` 即本行的 13 侧先手登记);`case-system.md:933` 原记 `NOT-RUN`(因本文件不存在)⇒ **本文件成文即为其解除条件,回写归 `/architecture-review` 轮** ⚠️ **2026-09-21 订正:该回写**已由批 19(`3bcb78e`)**就地完成**(`case-system.md:933` 现带「原引 `design/ux/` 误载路径」注 + `NOT-RUN` 改判为「已裁:不承诺」);此处原「归下一轮」的表述落后于实际。⚠️ 承项目纪律「**引用 ≠ 验收**」:该回写**不得**被读成 `AC-13-F3` 已结案 —— 它是文档判据,结案 = 本行存在且被引用(**本日达成**) |

---

## Per-Feature Accessibility Matrix

| System(组) | Visual | Motor | Cognitive | Auditory | Addressed | Notes |
|--------|----------------|---------------|-------------------|------------------|-----------|-------|
| 3 输入 / 42 UI / 39 脉案 | 纸纹对比 · 字号 | 手柄焦点路径完整 = 可玩性级 | 阅读密集 | 无(不经音频报状态) | Partial | 焦点单栈门(ADR-013)是承诺的执行体;改键 UI = L-1 |
| 8 诊断 + 9 病征 | **面色通道色觉依赖**(峰值问题) | —— | 五通道同时追踪(3b 审计对象) | 听诊通道 → SFX 审计表 | Not Started | `OQ-A1` 登记于下 |
| 10 急救 | —— | **hold 判定语义 vs toggle 替代 = L-2**;`L_input` 预算不受倍率触碰 | 时机压力下读体征 | —— | Partial | 与 ADR-011 Amendment B / OQ-25-8 ② 口径逐字一致 |
| 25 格斗线(非致命) | —— | 连打替代义务 | —— | 攻击前摇音 → 动画 telegraph 备份 | Not Started | 归零 = 昏迷,不存在「致死信号」类误读风险 |
| 20/21 库存 · 采集 · 炮制 | 稀有度不用色边框(拟物无稀有度边框,色-only 审计 N/A) | 拖拽 vs 焦点选取双路径 | 配方阅读 | —— | Not Started | —— |
| 5 时间天气 · 6 世界 | 地图深浅 = 色-only 候选(见审计表) | —— | 无小地图的方向记忆负荷 = 支柱自负成本,不做补偿承诺 | 风雨阻断音 | Not Started | 墨点 P1a(`death-and-respawn` 裁定) |
| 48 教学 | 世界内的纸 🚧 BLOCKED-BY → 预期可解 | —— | 教学持久性承诺在,可测性待裁 | —— | Partial | 阻塞根因已消(2026-09-21):`OQ-48-4/5` + `OQ-42-5` 均已闭合;剩余 = 渲染形态落地(42 修订轮 · `ModalId.PaperCloseup48` 第七员) |
| 45 联机(1-4  coop) | —— | 各端输入各自重映射 | 主机暂停语义须显式说明 | 精度取**各设备本机技能档**(ADR-018 §五,2026-09-18 裁定 D-A:原「主机技能」已作废)⇒ **听障玩家的远端读数不受影响,分叉的只有音** | Partial | 无障碍设置**不进流、不同步**(各端本地态)—— 此条须在 ADR-025 的 `Gameplay.Presentation` 实现时守住 |

---

## Accessibility Test Plan

| Feature | Test Method | Test Cases | Pass Criteria | Responsible | Status |
|---------|------------|------------|--------------|-------------|--------|
| 纸纹上文本对比 | 自动化 —— USS 主题色值 × 烘焙纹理最亮像素跑对比器 | 全 USS 语义色 × 全纸纹素材 | 正文 **≥7:1**(2026-09-21 AB-4 提档);大字 ≥3:1 | ux-designer | Not Started |
| 色盲走查 | 手动 —— Coblis 截图仿真 | 脉案五通道区 / 地图 / 急救呈现 各 × 三型 | 无判定信息仅靠色可分辨的面 | ux-designer | Not Started |
| 手柄焦点无断头 | 手动 —— 纯手柄完成 P0 主循环 | 主菜单 → 出诊 → 诊断 → 急救 → 脉案落笔 → 存档 | 每界面全部可交互元素可达;无同键双触发(ADR-013 单栈门) | qa-tester | Not Started |
| 焦点呈现两栈一致性 | EditMode —— `NavigationMoveEvent` 源计数 | 单栈门开 / 关两态 | 同一时刻恰一栈接收导航意图(承 `AC-42-A5` `SetFocusGate`) | qa-tester | Not Started |
| 判定宽容倍率 | PlayMode —— 0.5×/3× 两界跑急救夹具 | 同一 `EmergencyAttempt` 夹具 × 倍率 | 窗口线性变化且 **`L_input` 测量值不随倍率移动**(防预算重并,OQ-25-8 ②) | qa-tester | Not Started |
| 用户测试 —— 色觉异常 | 外招被试(一次性,预算内最小动作) | 完成一个完整病例 | 不因面色通道误读而卡单 | producer | Not Started |

---

## Known Intentional Limitations

| # | Feature | Tier Required | Why Not Included | Risk / Impact | Mitigation | 复审触发 |
|---|---------|--------------|-----------------|--------------|------------|----------|
| **L-1** | 游戏内**改键 UI**(P0) | Standard(字面含「全输入可重绑定」) | 用户裁定 2026-09-16(四项裁定之一):P0 不交付改键 UI;机制层(`bindings overrides` 持久化)在 ADR-011 已建 | 手部功能受限玩家 P0 内不能在游戏内改键 | ① Steam Input 系统级重映射可用;② 机制层已就绪,P1a 只补界面 | 日期:**P1a 评审**;或任一被试反馈因此卡单(即时提前) |
| **L-2** | 急救判定层长按的 **toggle 替代** | Standard(字面含「每个 hold 须有 toggle」) | `hold_ticks` 长按质量是 `EmergencyAttempt` 的判定输入(ADR-011 Amendment B 已 Accepted);toggle 化 = 改判定语义 = 重写 10 的求值域 + 存档事件形状(承 OQ-25-8:改 tick 域须四件同时失效重算) | 无法维持按压 / 无法测节律的玩家在急救小游戏满分路径受阻 | ① 判定宽容窗口倍率 0.5×–3.0×(不碰 `L_input`);② 急救整体**可降级跳过**(跳过路径已裁,归 10)⇒ 不构成不可通关;③ 表现 / 移动层 hold 全部有 toggle | 日期:**P1a**;或急救可跳过性被任何后续裁定取消(即时升级为本文件失效) |

> 登记口径:L-1 / L-2 是**档位承诺的显式收窄**,不是「Standard 未达标仍自称 Standard」的
> 遮掩 —— 若用户裁定撤销任一限制,对应项回到承诺面,工期账同步。

---

## Audit History

| Date | Auditor | Type | Scope | Findings Summary | Status |
|------|---------|------|-------|-----------------|--------|
| 2026-09-20 | ux-designer(本文件作者)| 初版承诺 | 全案 31 项 P0 系统 × 4 轴 | 两硬冲突显式化(L-1/L-2);面色通道为峰值问题(`OQ-A1`);48/39 两项 BLOCKED-BY 挂既有 OQ | Committed |

---

## Open Questions

| ID | Question | Owner | Deadline | Resolution |
|----|----------|-------|----------|-----------|
| OQ-A1 | 色盲调色板变体下,**面色词表**(青 / 黄 / 赤 …)是否仍可被玩家稳定分辨并与脉案文字读数对齐?若否,面色是否须改走**非色相参数**(明度 / 饱和度轴)? | 8 + 42 + technical-artist | 面色呈现实现前 | Unresolved(与 ADR-014 烘焙调色板评审并批) |
| OQ-A2 | 无障碍设置(倍率 / 缩放 / 字幕开关)**本地端持有、不进三流、不跨端同步** —— 该「不进流」断言的守卫测试落点? | ADR #2(边界程序集)| #2 Accepted 时 | Unresolved —— 已预写入本文件矩阵联机行 |
