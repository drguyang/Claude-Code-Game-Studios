# ADR-013: 拟物 UI 框架(UI Toolkit 主 + UGUI 补 world/XR 的呈现层)

## Status

Accepted

> **2026-09-15 起草并转 Accepted。** 三条用户裁定已锁:① **UI Toolkit 为主 + UGUI 补 world-space/XR**
> (平面拟物 UI 全走 UI Toolkit UXML/USS;世界空间与 VR 急救 UI 走 UGUI world canvas,补 E-16 空白);
> ② **P0 定两栈接口与元件库契约,VR / world-space 的立体适配推到 P1a**(VR 急救不在 P0 关键路径);
> ⚠️ **2026-09-16 就地修订**:本条原写「VR / world-space **具体实现**推到 P1a」,**已收窄** ——
> **world-space 的最小实现(敌人读数条 = 世界锚点 → billboard 面片)提到 P0**(阅 §二 修订块);
> ③ **拟物视觉 = 自建 USS 拟物元件库**(纸纹 / 墨迹 / 卷轴九宫格 + 主题变量)+ UXML 组合;
> UGUI 侧仅 world / XR 用(TMP + 贴图),两层同构件库语义对齐。
> 引擎侧经 unity-specialist lean 复核(2026-09-15):**无引擎侧 blocker**;结论并入 §Risks
> (F1 官方桥单源 · F2 焦点引擎自动 + E-16 成立 · F3 VR 必须 World Space · F4 两栈共用 EventSystem +
> 焦点单栈门 · **F5 UI Toolkit 自定义 shader 受限(HIGH)** · **F6 图集阈值(HIGH)** ·
> F7 DTO 反射扫描递归 · F8 无障碍四钩子)。
> 独立评审由下一轮 `/architecture-review` 进行。

## Date

2026-09-15

## Last Verified

2026-09-21(**Amendment B**:`ModalId` 第七员 `PaperCloseup48` 登记 + 方笺归 `Casebook` 判定;
§一–§九 与 §十 的裁决面零改动 —— 本节只执行 §十 预置的枚举同步义务)

## Decision Makers

dr_guyang(用户 · **2026-09-15 三条裁定,均照准**)· technical-director(起草与裁决)
· unity-ui-specialist(UI 框架复核)· ux-designer(焦点路径与无障碍)· unity-specialist(引擎复核,2026-09-15)
· 系统 42 拟物 UI 框架 / 39 脉案 / 43 纸质地图与出诊箱 / 7b 存档位 UI / 48 教学

## Summary

**R-6 是架构复核 §10 中唯一「HIGH 引擎风险 + Foundation 层」的 UI 缺口**,且 **E-16 明写
「UI Toolkit 无原生 world-space / XR 支持 ⇒ 同一 UI 两套栈要早决」**。ADR-011 已把焦点导航的
**接口**侧落定(动作 → `FocusNavigationIntent`,R-6 用官方桥),把**呈现**侧明确留给本 ADR。
本 ADR 裁决:**UI Toolkit 为主栈**(平面拟物 UI:脉案 / 出诊箱 / 纸质地图 / 存档位),
**UGUI 补 world-space / XR**(敌人读数条 P0 最小实现 · VR 站定式急救 UI P1b);**P0 定义两栈接口 + USS 拟物元件库契约,
world-space 最小面 + 立体 / XR 适配推 P1a**;**拟物视觉 = 自建 USS 元件库(纸纹 / 墨迹 / 卷轴九宫格 + 主题变量)+ UXML 组合**,
UGUI 侧语义对齐。42 / 39 / 43 / 7b / 48 的呈现层契约由此落定。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | UI(UI Toolkit / UGUI / XR 呈现) |
| **Knowledge Risk** | **HIGH** —— UI Toolkit 与 UGUI 在 6.3 的具体行为(运行时焦点导航 / 官方桥 / 自定义材质支持 / 图集 / 运行时数据绑定 / `UnityEngine.Accessibility`)均属 post-cutoff 知识,须 spike;引擎参考库(`modules/ui.md`)只含基础教程,**无焦点 / world-space / XR / 无障碍任何小节** |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`(UI Toolkit「Production-ready for runtime UI」)· `modules/ui.md` · `deprecated-apis.md`(UGUI → UI Toolkit 迁移)· `architecture-review-2026-09-15.md`(R-6 · E-16 · §6.6 假设 6)· `adr-011-input-architecture.md` · `design/gdd/systems-index.md` §9 C3 · `design/gdd/disease-simulation.md` §UI Requirements · `technical-preferences.md` |
| **Post-Cutoff APIs Used** | **None 承诺** —— 官方桥(`NavigationMoveEvent`)· 焦点系统 · 无障碍挂点均为 Unity 6 新增,标「须 spike」;接口层(两栈边界 / 元件库契约)为纯 C# / USS 约定,不受影响 |
| **Verification Required** | ① **焦点导航原型 spike**(报告 §6.6 假设 6「半可信」):UI Toolkit 运行时手柄焦点导航质量 + `NavigationMoveEvent` 官方桥,复用 ADR-011 的 spike;② **VR world-space spike**(UGUI World Space 在 XR 下的立体 / 深度);③ **UI Toolkit 自定义材质 spike**(F5 墨迹晕染能否落 UI Toolkit);④ **图集阈值 spike**(F6 纸纹撑爆 atlas 的规模);⑤ `UnityEngine.Accessibility` API spike(F8) |

> **Note**: Knowledge Risk HIGH —— UI Toolkit / UGUI 的 6.3 具体行为升级时须重读;两栈边界契约不受影响。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-011**(Accepted —— 焦点导航接口侧:动作 → `FocusNavigationIntent`;R-6 用官方桥)· **ADR-005**(Accepted —— 表现层 float 隔离于 `IVitalsQuery → VitalsDto`)· **ADR-008**(Accepted —— `disease_id` 不进呈现层 / AC-37-15 DTO 静态检查)· **ADR-009**(Accepted —— 表现态与模拟态分离)· **ADR-010**(Accepted —— 7b 存档位 UI) |
| **Enables** | **42 / 39 / 43 / 7b / 48 的实现**(呈现层契约)· `TR-concept-008`(gap → covered)· `design/ux/` 的撰写(`/ux-design` 预门控) |
| **Blocks** | 系统 42 / 39 / 43 / 7b 的实现;凡依赖 UI 栈选型的 presentational AC |
| **Ordering Note** | 本 ADR 是 **R-6 的落点**。**ADR-011 的先决已满足**(接口侧已 Accepted);**先 Accepted 本 ADR,再写 42 / 39 的 UI 代码**。**不阻塞**模拟侧(9 / 37 / 52 / 21a)。**P0 落两栈接口 + 元件库契约 + world-space 最小实现(敌人读数条);立体 / XR 适配推 P1a**(VR 急救不在 P0 关键路径)。⚠️ 2026-09-16 修订 |

## Context

### Problem Statement

**R-6 是复核列出的唯一 UI 缺口,且 E-16 是硬约束。** ADR-011 已把焦点导航的接口侧落定,
但呈现侧**完全空白**:

1. **UI Toolkit 无原生 world-space / XR 支持**(E-16):VR 急救模式与任何世界空间 UI
   (敌人读数条等世界锚点提示)须走 UGUI world canvas 或网格 ⇒ **同一 UI 两套栈**;
2. **拟物 UI 铁律**(systems-index §9 C3):**42 只渲染,永不持有游戏状态** —— 「体征 → 呈现形态」
   映射表归 8 诊断(那是医学知识);42 只是渲染器;
3. **`disease_id` 不进呈现层**(ADR-008 / AC-37-15):唯一落点是 DTO 静态检查,归 39 脉案;
4. **拟物 UI 大量纸 / 墨 / 卷轴类元件**(脉案 / 出诊箱 / 纸质地图 / 存档位),须有统一元件体系,
   否则各界面手写必然重复与漂移;
5. **手柄焦点导航是硬约束**(technical-preferences):手柄没有指针,全部拟物界面须焦点路径。

**不裁决的代价**:P0 中期发现 VR 急救 UI 与平面 UI 抢 EventSystem / 焦点流双触发,
或拟物元件无体系各写各的 —— 前者是运行时静默 bug(一次按键两次焦点移动),后者是
每加一个界面就多一份重复样式。E-16 已明说「**两套栈要早决**」。

### Current State

- `systems-index.md`:42 拟物 UI 框架 | Foundation | P0 | 未开始;39 脉案 | Presentation | P0;
  43 纸质地图与出诊箱 | Presentation | P1a;7b 存档位 UI | Presentation;48 教学 | P0。
- `technical-preferences.md`:拟物 UI(无血条 / 无小地图)**必须同时支持键鼠与手柄导航**;
  UI Specialist = `unity-ui-specialist`。
- `tr-registry.yaml`:`TR-concept-008`(拟物 UI 手柄焦点导航)= `partial`,adr「R-6 (待撰写)」;
  `TR-diag-019`(`disease_id` 不进呈现层)= `partial`;`TR-diag-020`(脉案 DTO 静态检查)= `gap`;
  `TR-case-019`(规则九)= `partial`(落点 AC-37-15)。
- `design/ux/` **不存在**;`design/accessibility-requirements.md` **不存在**(预门控两项 ❌)。
- 引擎参考库:`VERSION.md` 明写 UI Toolkit「Production-ready for runtime UI (replaces UGUI for
  new projects)」;`modules/ui.md` 只有基础教程,**无焦点 / world-space / XR / 无障碍小节**。
- 复核 §6.6 假设 6:**「UI Toolkit 运行时手柄焦点导航可用」= ⚠️ 半可信,质量未定**。

### Constraints

- 不得破坏 ADR-011:焦点导航接口侧已定(R-6 用官方桥,**禁 Navigation 动作重复绑定**);
  本 ADR 只定「导航意图 → UI 焦点移动」。
- 不得破坏 42 铁律(§9 C3):**42 只渲染,永不持有游戏状态**;映射表归 8。
- 不得破坏 ADR-008 / ADR-005:`disease_id` 不进呈现层;表现层 float 隔离于 `IVitalsQuery`。
- **VR 必须 World Space**(引擎复核 F3):Overlay 不参与立体渲染,Camera 模式立体易错位 / 深度冲突。
- **两栈须共享同一 EventSystem**(引擎复核 F4):双 EventSystem / 双输入模块 = 真冲突。
- 手柄不作一等公民,但**必须完整可用**(焦点导航硬约束)。

### Requirements

- 平面拟物 UI(脉案 / 出诊箱 / 纸质地图 / 存档位)有统一框架与元件体系。
- 世界空间 / VR UI 有明确归属栈(E-16 的「两套栈」早决)。
- 焦点导航呈现侧:导航意图 → UI 焦点移动,与 ADR-011 接口侧对接,**无同键双触发**。
- 拟物元件可复用、可主题化(纸 / 墨 / 卷轴),不各界面手写。
- 满足 `TR-concept-008`(gap → covered)、`TR-diag-019/020`、`TR-case-019` 的呈现层落点。
- 为 `design/ux/` 与无障碍要求(`/ux-design` 预门控)留接口。

## Decision

**裁决:UI Toolkit 为主 + UGUI 补 world-space / XR · P0 定接口与元件库契约 + world-space 最小实现
(立体 / XR 适配推 P1a)· 拟物视觉 = 自建 USS 元件库 + UXML 组合。**

### 一、UI 栈分工(两栈早决)

| 用途 | 栈 | 理由 |
|------|-----|------|
| **平面拟物 UI**(脉案 / 出诊箱 / 纸质地图 / 存档位 / 教学) | **UI Toolkit**(UXML + USS) | 引擎参考推荐新项目;保留模式渲染,USS 拟物样式;官方焦点桥 |
| **World-space UI**(敌人读数条 · 世界锚点提示) | **UGUI world canvas** | UI Toolkit 无 world-space(E-16,F2 确认成立) |
| **VR 急救 UI**(站定式小游戏) | **UGUI world canvas**(**必须 World Space**) | VR 立体渲染须 World Space;Overlay 不参与立体(F3) |

> **⚠️ 2026-09-16 就地修订(第 2 行主语)**:原文写「世界空间**体征提示**」。
> **主角自身的体征世界层呈现不归本 ADR** —— 已由 **ADR-020 §六** 判给「8 语义 + **2 摄像机 / shader**
> 实现」(主角视野边缘是**水墨渗边**,不是一条 UI 元件,`diagnosis-system.md:1641`)。
> 本行改指 **42 真正拥有的 world-space 面 = 敌人读数条 · 世界锚点提示**(`game-concept.md:776`「丙」)。

**边界**:两栈共用同一 `EventSystem`(UGUI 走 `GraphicRaycaster`,UI Toolkit 走 `PanelRaycaster`)——
**禁双 EventSystem / 双输入模块**(F4)。
**⚠️ z 序条款 2026-09-17 就地修订**:原文写「z 序**须对齐**(`Canvas.sortingOrder` vs
`PanelSettings.sortOrder` 两套)」—— 该措辞**暗示两个数可比**,而它们分属**两个合成域**
(UGUI world canvas 在**相机内**排序;UI Toolkit **Overlay** 面板在**全部相机之后**合成),
**不可比**。**修正口径**:42 拥有的是**两张域内序表**(覆盖层内 / 相机内),**各自域内不得撞号**;
**跨域序是引擎的结构性事实**(覆盖层恒在相机之上),**不是配置项**。
GDD 42 的 §Formulas F4 已就地重写(三层口径:相机内 / 跨域二值 / 覆盖层内),
原「`φ` 严格单调 + 单射」的装载期断言**已废**(它断的是一个不存在的对象)。

### 二、P0 范围:接口与元件库契约 + world-space 最小实现(立体 / XR 适配推 P1a)

> **⚠️ 2026-09-16 就地修订(本标题与下方 P1a 行)** —— 原文为「P0 范围:接口与元件库契约
> (**VR 实现推 P1a**)」,并把「**VR / world-space 的两栈适配**」整体推到 P1a。
> **该措辞与 `game-concept.md:776` 的「丙」裁决相抵** —— 敌人**有**黄铜质读数条、**挂在敌人身上**,
> 而 25 格斗是 **P0**,P0 开场遭遇即兵痞。原口径 ⇒ **P0 的战斗没有状态反馈**。
> **用户裁定(2026-09-16)**:**world-space 栈的最小实现提到 P0**。修订后:
> - **P0** = 「一组世界锚点 → 一张 billboard 面片」**这一条路径**;**不做**立体渲染、**不做**深度冲突处理;
> - **P1a** = 立体适配 + VR 立体渲染(栈的**完整**适配)。

- **P0 定义**:
  - **两栈接口契约**(呈现层与模拟层解耦;UI 只读 DTO,不持状态 —— §三);
  - **USS 拟物元件库**(纸纹 / 墨迹 / 卷轴九宫格 + 主题变量);
  - **焦点导航呈现侧接线**(导航意图 → 官方桥 → 焦点移动);
  - **平面拟物 UI 的框架实现**(42 / 39 / 7b);
  - **world-space 最小实现**(**敌人读数条**:世界锚点 → billboard 面片;**不含**立体 / 深度 / XR)。
- **P1a 实现**:world-space 栈的**立体适配 + VR 立体渲染**(UGUI world canvas + stereo);
  **VR 急救不在 P0 关键路径**,P0 只留接口,避免为小功能付双栈早投成本。43 纸质地图与出诊箱本身即 P1a。
- **理由**:E-16 要求「两套栈**要早决**」—— 指的是**架构方向早决**(本 ADR 已决),
  不是**两栈实现都要 P0 完成**。P0 定契约 + 平面实现 + world-space 最小面,
  立体 / VR 实现 P1a 承接,方向不推翻。

> **⚠️ 三档消歧(常被混为一谈,`docs/consistency-failures.md` 已记录一次冲突)** ——
> **栈适配**(本 ADR:P0 最小 / P1a 立体)· **VR 急救这个功能本身**(`game-concept.md:720` 范围阶梯 = **P1b**)
> 是**两件不同的事**。本 ADR 只定**前者**;**「VR 何时有」不归本 ADR 定**。

### 三、拟物铁律与数据边界(承 ADR-008 / 005 / §9 C3)

- **42 只渲染,永不持有游戏状态**(§9 C3):42 是纯渲染器;**「体征 → 呈现形态」映射表归 8**
  (医学知识,不是渲染知识)。
- **UI 只读 DTO,不持模拟态**:UI 经 `IVitalsQuery → VitalsDto`(ADR-005 唯一 float 出口)读数据;
  **UI 层零模拟写**。
- **`disease_id` 不进呈现层**(ADR-008 / 规则九):唯一落点 = **DTO 静态检查**(AC-37-15)。
  本 ADR 定为 **EditMode 反射扫描**(F7):扫 UI DTO 类型树,断言无 `disease_id` 字段 ——
  **须递归**(`List<DTO>` 元素、私有 / 继承字段),仅扫顶层会漏。
- **UI 不写模拟域**(ADR-011 意图化):UI 交互产出**意图**(经 ADR-011 意图流),不直写状态。

### 四、拟物视觉:自建 USS 元件库 + UXML 组合

- **元件库**(UI Toolkit 侧):USS 拟物元件(`.scroll` 卷轴 / `.paper` 纸面 / `.ink` 墨迹 /
  `.seal` 印章)+ **九宫格** `-unity-slice-*`(F5 确认为真实 USS 专有属性);
  纸纹 / 墨迹作 `background-image`;主题走 **USS 自定义属性**(`--paper-tone` + `var()`)+
  TSS `:root`(F5 确认可行)。
- **UGUI 侧**(仅 world / XR):TMP + 贴图;元件库**语义对齐**(同名元件,两栈观感一致)。
- **⚠️ 引擎复核 F5(限制)**:UI Toolkit **运行时自定义 shader / 材质支持受限**(不像 UGUI)⇒
  墨迹晕染等**程序化效果或须烘焙进贴图,或落 UGUI 层**(须 spike)。这是拟物观感的真实约束:
  **若某拟物效果必须程序化,该元件可能须迁到 UGUI 栈** —— 本 ADR 允许个别元件跨栈,
  但**语义仍在同一元件库**。

### 五、焦点导航呈现侧(与 ADR-011 对接)

- **导航意图 → 焦点移动**:ADR-011 定义动作 → `FocusNavigationIntent`(单向意图流);
  本 ADR 定义 `FocusNavigationIntent` → UI 焦点移动。
- **用官方桥**(ADR-011 裁定):`InputSystemUIInputModule` 把 UI 动作映射翻成
  UI Toolkit 的 `NavigationMoveEvent`;`PanelEventHandler` 随 `UIDocument` 自动挂(F1)。
- **焦点移动是引擎自动**(F2):官方桥接通后,方向键 / 摇杆的焦点移动由 UI Toolkit
  `FocusController` 消费 `NavigationMoveEvent` + 空间选邻居**自动完成**;R-6 **不重复实现焦点算法**。
  **约束面收窄(2026-09-21 · 承 `architecture-review-2026-09-20.md` RC-1)**:本条约束的**对象 =
  42 的对外契约面**(不导出「邻居枚举 / 方向投影 / 几何查询」类入口),**不是**「42 内部不得有
  焦点算法代码」—— 降级期自实现焦点算法受 `AC-42-B4` 类型面断言约束(42 侧收窄已存在,
  `skeuomorphic-ui.md` §AC-42-B4;本条随其对齐,非新裁决)。
- **禁同键双触发**(ADR-011 F1 / 本 ADR F4):三种引擎侧冲突源 —— ① 同一控件(D-pad / 左摇杆)
  同时绑 Input System 默认 **`UI` action map 的 `Navigate`** 与自建焦点动作;
  ② 同 EventSystem 并存 `InputSystemUIInputModule` + `StandaloneInputModule` 或双 EventSystem;
  ③ 模块「自动启用 UI 动作」与手动 action asset 同时生效。
  **裁决:冻单一来源** —— 只用官方 `Navigate`,自建焦点动作不绑同控件。
- **焦点单栈门**(F4):**ADR-011 的导航意图流不可同时喂两栈**;同一时刻仅一个栈接收焦点导航
  (平面 UI 活跃时 UI Toolkit 独占;VR 模式时 UGUI 独占)。

### 六、无障碍钩子(为 `/ux-design` 预门控留接口)

`design/accessibility-requirements.md` 不存在(预门控 ❌)。本 ADR **只留四钩子,不实现**:

1. **焦点元素无障碍命名约定**(`visualElement.accessibilityNode` 挂点,F8 post-cutoff 须 spike);
2. **文本缩放主题变量层**(USS 主题变量承载字号,无引擎自动支持,须自建);
3. **焦点可见样式契约**(USS 类的焦点态样式,拟物风格的「高亮」须非纯色 —— 如墨色加深);
4. **动效缩放挂点**(**2026-09-17 新增**)—— 翻页 / 淡出 / 墨迹渗开等**时长型**观感走一个
   缩放系数,不写死时长;与钩子 2 同构(**装载 / 变更时求值**,不缓存最终时长)。

> **⚠️ 钩子由三改四的理由(2026-09-17,系统 42 首轮 `/design-review` 的 accessibility 发现)** ——
> 前三钩子(命名 / 文本缩放 / 焦点可见样式)覆盖了「看得见(命名)」「看得清(字号)」「知道焦点在哪(可见样式)」,
> **唯独没覆盖「动得快不快」**。而 42 是拟物动效的**唯一来源**(翻页 / 淡出 / 墨迹渗开全在它手里)——
> 若 P0 不定这个挂点,`reduce motion` 类需求到来时 42 的**每一个动效都要返工**。
> **`GDD 42` 侧已落 `AC-42-G4`**(动效缩放挂点断言)与 §Core Rules 规则十的四钩子表(含生命周期)。

> **边界**:无障碍的具体要求(对比度阈值 / 屏幕阅读器覆盖范围 / 色盲模式)归
> `design/accessibility-requirements.md`(`/ux-design`);本 ADR 只保证**四处**接口存在。
> **对比度是例外** —— 「**任何主题 / 纹理档下均须满足**」这条**义务归 42**(阈值仍归 49),
> 见 `GDD 42` 的 `AC-42-G3`。

### Architecture Diagram

```
                    ┌─────────────────── 呈现层(只读 DTO,零模拟写)───────────────────┐
                    │                                                                  │
  8 诊断 ──映射表──▶│  42 拟物 UI 框架(只渲染,永不持有状态)                            │
  9 / 37 / 52 ──────▶│        │                                                         │
  (IVitalsQuery→DTO)│        ├── 平面拟物 UI:UI Toolkit(UXML + USS 元件库)             │
                    │        │     脉案 / 库存容器 / 纸质地图 / 存档位 / 教学              │
                    │        │     └── 焦点:NavigationMoveEvent(官方桥)                │
                    │        └── World/XR:UGUI world canvas(VR 必须 World Space)        │
                    │              敌人读数条(P0 最小)· VR 急救(P1b)                    │
                    └───────────────┬──────────────────────────────┬───────────────────┘
                                    │                              │
   ADR-011 导航意图流 ──────────────┤ 焦点单栈门(仅一栈接收)        │ 共用同一 EventSystem
   (FocusNavigationIntent)          │                              │ (禁双 EventSystem)
                                    ▼                              ▼
                          UI Toolkit FocusController       UGUI GraphicRaycaster
                          (引擎自动焦点移动)               (PanelRaycaster 另路)

  数据边界:UI 只读 VitalsDto(ADR-005 唯一 float 出口);disease_id 不进 DTO(AC-37-15 反射扫描)
  拟物元件库:USS 纸纹/墨迹/卷轴九宫格 + 主题变量;UGUI 侧语义对齐(F5:程序化效果或落 UGUI)
```

### Key Interfaces

```csharp
// ── 两栈接口契约(呈现层与模拟层解耦)──
// 铁律:UI 只渲染,不持有游戏状态(systems-index §9 C3)
public interface IPresentationRoot
{
    void Bind(IDtoSource source);   // 只读 DTO 源;UI 不写
    void SetFocusGate(bool active); // 焦点单栈门:同一时刻仅一栈接收焦点导航(F4)
    // ⚠️ 注意:SetFocusGate 不是"是否有模态打开"。后者是第三个量,见 §十 IModalState。
}

// ── 模态开集只读契约(2026-09-18 Amendment A · 供 4 / 10 读"是否有模态界面摊开")──
public interface IModalState
{
    ModalId Modal { get; }   // None ⇒ 无模态。只读,零写侧外暴;详见 §十
}

// ── 拟物元件库契约(两栈语义对齐)—— ──
public enum SkeuoElement { Paper, Scroll, Ink, Seal }   // 纸面 / 卷轴 / 墨迹 / 印章

public interface ISkeuoElementLibrary
{
    VisualElement Create(SkeuoElement kind);   // UI Toolkit 侧:USS 类 + 九宫格
    // UGUI 侧同语义:同名元件,TMP + 贴图(P1a)
}

// ── 焦点导航呈现侧(与 ADR-011 对接)──
// ADR-011 产出 FocusNavigationIntent;本层 = 导航意图 → 焦点移动
public interface IFocusNavigationPresenter
{
    // 用官方桥 NavigationMoveEvent;焦点移动由 UI Toolkit FocusController 自动完成(F2)
    // 不重复实现焦点算法;禁同键双触发(只用官方 Navigate action)
    bool IsFocusActive { get; }
}

// ── 无障碍钩子(F8,只留接口不实现)──
public interface IAccessibilityHooks
{
    void SetAccessibleName(VisualElement e, string name);  // 屏幕阅读器命名约定
    void ApplyTextScale(float scale);                       // 文本缩放主题变量层
    // 焦点可见样式 = USS 类契约(.focus-visible)
    // 动效缩放挂点(2026-09-17 第四钩子):时长型观感的统一缩放系数,
    //   与 ApplyTextScale 同构 —— 装载 / 变更时求值,不缓存最终时长
    void ApplyMotionScale(float scale);
}

// ── DTO 静态检查(F7:AC-37-15 落地)──
// EditMode 反射扫描:递归扫 UI DTO 类型树,断言无 disease_id 字段
public static class PresentationDtoGuard
{
    public static void AssertNoDiseaseId(Type dtoRoot);  // 须递归(List<DTO> 元素 / 私有 / 继承字段)
}
```

### Implementation Guidelines

1. **先建焦点导航原型 spike**(报告 §6.6 假设 6「半可信」):UI Toolkit + 官方桥的焦点质量,
   复用 ADR-011 的 spike;质量不达预期则回落自实现焦点算法(接口不变)。
2. **一栈一栈落**:先 UI Toolkit 平面拟物 UI(42 / 39 / 7b)+ world-space 最小实现(敌人读数条);
   立体 / XR 适配留到 P1a。
3. **USS 元件库先行**:纸纹 / 墨迹 / 卷轴九宫格 + 主题变量是拟物观感的锚点,先建库再建界面。
4. **焦点单栈门**:`IPresentationRoot.SetFocusGate` 保证 ADR-011 意图流不双喂(F4)。
5. **EventSystem 唯一**:两栈共用;禁 `StandaloneInputModule` 与 `InputSystemUIInputModule` 并存。
6. **`disease_id` 反射扫描**(`PresentationDtoGuard`,递归)进 EditMode 门。
7. **F5 程序化效果 spike**:测试墨迹晕染能否落 UI Toolkit;不能则该元件迁 UGUI(语义仍同库)。
8. **图集护栏**(F6):控纸纹贴图尺寸与数量,调 `PanelSettings` atlas;防 spill 断批。
9. **无障碍四钩子**留接口;具体要求归 `design/accessibility-requirements.md`(`/ux-design`)。

### 十、Amendment A(2026-09-18 · 承 #4 交互系统首轮 `/design-review` · 用户裁定 D)—— 新立「模态开集」只读契约 `IModalState`

> **本节是 Amendment,不重开 §一–§九 的任何裁决。它只补一个此前不存在、却被下游需要的只读面。**

**病因(为何既有两个 getter 都不够)**:系统 4 的 `F-4.2` 需要在**任一 UI 模态打开时拒收世界交互意图**。
42 侧现有两个只读布尔**都不是这个意思**:

| 既有成员 | 语义 | 为何不能给 4 用 |
| --- | --- | --- |
| `IPresentationRoot.SetFocusGate(bool)`(`:265`) | **单栈导航门** —— 当前哪一栈接收焦点导航(F4) | 「门开着」≠「没有模态」;它管的是**导航意图喂给谁**,不是**是否有界面摊在玩家面前** |
| `IFocusNavigationPresenter.IsFocusActive`(`:283`) | **焦点导航是否激活** —— 键盘/手柄导航模式在不在跑 | 鼠标点进界面时它可为 `false` 而模态仍打开;且它是**导航态**不是**模态持有** |

**「任一栈持有模态」是第三个量,本 ADR 此前从未定义它** —— 这是「引用却无登记」的又一形态:
4 的 GDD 原稿引用了一个**语义上需要、契约上不存在**的旗,并把 `SetFocusGate` 当它用。

**裁决(用户裁定:42 新立「模态开集」只读态,推荐项)**:

```csharp
// ── 模态开集只读契约(呈现层,零写侧;供 4 / 10 / 其他消费方读「是否有模态界面摊开」)──
// 铁律:这是"读",不是"状态"。42 仍然"只渲染、永不持有游戏状态"(§9 C3)——
//   Modal 成员的真源是"哪个界面当前可见",本就是 42 渲染职责内的量,不落游戏模拟态。
public enum ModalId { None = 0, Casebook,          // ① 脉案 39(+ 方笺同页 —— 2026-09-21 裁定,见本节末 Amendment B)
                              SaveSlots,           // ② 存档位 7b
                              InventoryContainer,  // ③ 库存容器 20
                              SettingsShell,       // ④ 设置界面壳 42
                              Tutorial,            // ⑤ 教学 48(纸堆翻页走查屏)
                              ClinicPanel,         // ⑥ 医馆面板 24
                              PaperCloseup48 }     // ⑦ 教学纸近景(2026-09-21 Amendment B 增员)
// ⚠️ 成员集 = AC-42-F1 的"七屏闭集"的镜像,一处声明。新增第八屏须同步本枚举。

public interface IModalState
{
    ModalId Modal { get; }   // None ⇒ 无模态;非 None ⇒ 有(消费方据此拒收世界意图)
    // 只读。42 不对外暴露 setter;模态的开合由 42 自身渲染层维护(它本就知道谁可见)。
}
```

**三条约束**:

1. **消费方「引用而非复制」闭集** —— 4 / 10 等不得在自己代码里维护一份「哪些界面算模态」的清单;
   须引用 `ModalId` 枚举。⇒ 未来加第七屏时消费方**零改动**即覆盖(否则「设置壳里按 South 顺手拾取 / 误触就诊」的漏洞会随每次加屏复发)。
2. **不得把 `ModalOpen` 落为进流的边沿** —— 一旦「谁摊开了脉案」写成 `SimEvent`,
   即令 42 **持有游戏状态**,直接破 §9 C3 铁律。模态门是**纯表现侧的即时本地拒收**(承 ADR-011:本地判定/呈现,不穿模拟)。
3. **单值 `Modal`,非集合** —— 同一时刻至多一个模态(承 §五焦点单栈门的同构假设)。
   若未来需要叠模态(如库存里开确认框),`Modal` 改栈是**本契约的一次修订**,须另判,**不在此预留**。

**Enables**:4 的 `F-4.2` 第二门(`¬ModalOpen(42)`)与 `AC-4-09` 自此有实现依据。
**残留义务落点**:`design/gdd/skeuomorphic-ui.md` 须把 `IModalState` 写进其接口节,
并把 4 加为 42→4 的只读门下游(见该文件同日注)。**4 的 `AC-4-09` 在本契约落 `skeuomorphic-ui.md` 前记 `NOT-RUN`。**
**Engine Knowledge Risk**:LOW(纯 C# 只读契约,不触及任何 post-cutoff API)。

### 十-B、Amendment B(2026-09-21 · 42 修订轮 · 第二十八批 · 用户裁定两项)—— 闭集第七员 `PaperCloseup48` + 方笺归 `Casebook`

> **本节是 Amendment,不重开 §一–§九 / §十 的任何裁决。它只执行 §十 预置的同步义务并澄清一员的语义边界。**

**增员 `PaperCloseup48`(第七员)** —— 承 `OQ-48-4`(2026-09-19 已裁:教学纸 = 纯装饰不可拾,
走近 → 42 近景模态,措辞即「闭集第七员」)+ gate 报告(2026-09-21)行 6 选页 +
`design/ux/paper-closeup-48.md`(2026-09-21 `/ux-review` APPROVED,0 BLOCKING)。
该页成文使 `AC-42-F1` 原「⑤ 教学界面(48)」的空主语落地(= `OQ-48-8` / `OQ-C1` 的执行体)。
**成员 ⑤ `Tutorial` 与 ⑦ `PaperCloseup48` 并存不合并**:⑤ = 纸堆翻页走查屏(48 既有口径),
⑦ = 世界内单张教学纸的近景模态 —— 合并等于改判 ⑤ 的既有含义,不允许。

**方笺判定(第二裁)**:**方笺 = 39 脉案同一本书**,`Casebook` 员的语义扩至「脉案 + 方笺」,
**不增枚举值、不开新模态**。三条理据:① 医学顺序即输入顺序(诊→脉案→方笺同册连写,承 8 侧
S-8.4 路线甲「施治 = 方笺落笔」已把施治挂在脉案模态内);② 11 规则七自陈「剂量 = 戥子 / 药包,
承 39 的『线订成叠』同款纪律」—— 载体同源;③ 零新裁决输入(复用 `Casebook` 相机档,
2 的 `R-2-5` 档位表**不增行**,39 仍是该档唯一请求方 —— 11 的开方动作不发档位意图,
`TR-prescription-014` 的「不请求」从**条件式默认态**升为**已裁终态**)。
⇒ 11 侧 `OQ-11-7` / `OQ-11-9` 同批结清;`casebook.md` 原「`OQ-11-7` 已裁」的**未背书措辞**
自此有裁可引(订正为引用本 Amendment)。

**同步义务**(§十 约束 1 的兑现):`design/gdd/skeuomorphic-ui.md` `AC-42-F1` 闭集点名六 → 七;
`design/accessibility-requirements.md` :83 归档行;`design/ux/interaction-patterns.md` P-02 状态行。
**Enables**:`design/ux/paper-closeup-48.md` 的 AC 区自「预测量」转「可实测」(NOT-RUN 前置解除);11 的 `AC-11-13`(档位读数可辨性)自「义务未认领」转「可判」—— 同批裁定该元件落 **42 元件库黄铜侧**
(錾刻刻度 / 机械位移 = 戥杆倾角;禁数字角标 —— 两栈皆禁组无例外;材质侧归属见 `skeuomorphic-ui.md`
§Visual 二 注)。**2026-09-22 OQ-TUT-2 批裁补注**:教学纸近景 / 「可重听」口述的投递经 4 的交互判定(经 `Submit`,复用既有通道不新造),触发方式归 48 × 4 实现轮 —— 详见 `tutorial-and-onboarding.md` 规则四注 + `interaction-system.md` OQ-4-19 + `input-system.md` 规则二 `Submit` 行注。
**Engine Knowledge Risk**:LOW(纯枚举成员 + 归属澄清,不触及任何 post-cutoff API)。


## Alternatives Considered

### Alternative 1: 全 UGUI 单栈

- **Pros**:单栈,无 E-16 适配风险;world-space / XR 原生支持。
- **Cons**:逆引擎参考「新项目用 UI Toolkit」;UGUI 改一元素脏整 Canvas;失去保留模式渲染与 USS 拟物样式。
- **Rejection Reason**:用户裁定① —— UI Toolkit 为主,UGUI 只补 world / XR 的空白。

### Alternative 2: 全 UI Toolkit + 自绘 world-space

- **Pros**:单栈;与引擎推荐一致。
- **Cons**:world-space / XR 无官方支持;须网格 + TextMeshPro 自绘,VR 无官方焦点 / 输入桥,
  且与 ADR-011 的官方桥裁定冲突。
- **Rejection Reason**:E-16 + F2/F3 确认 UI Toolkit 无 world-space;自绘成本高且偏离官方路线。

### Alternative 3: P0 同期双栈实现

- **Pros**:VR 与平面同时就绪。
- **Cons**:VR 急救在 P0 占比小,早期双栈 = 为小功能付大成本;VR spike 未做前双栈有返工风险。
- **Rejection Reason**:用户裁定② —— P0 定接口与契约,**立体 / XR 实现推 P1a**(2026-09-16 修订:world-space **最小面**已在 P0)。E-16 要求「早决」的是方向,不是双栈都在 P0 完工。

### Alternative 4: 纯 USS 手写,不建元件库

- **Pros**:最轻,无库维护成本。
- **Cons**:拟物 UI 元件多(脉案 / 出诊箱 / 地图 / 存档位),手写必然重复与样式漂移;
  与 §9 C3「42 是渲染器」的复用诉求相反。
- **Rejection Reason**:用户裁定③ —— 自建 USS 元件库 + UXML 组合;元件体系是拟物 UI 复用的锚点。

## Consequences

### Positive

- **系统 42 / 39 / 43 / 7b 呈现层契约落定**(零覆盖 → 有权威件)
- **E-16 的「两套栈」早决**(方向确定,不推翻)
- **焦点导航呈现侧接线完成**:导航意图 → 官方桥 → 焦点移动(ADR-011 接口侧对接)
- **拟物元件库统一**(纸 / 墨 / 卷轴),界面复用而非各写各的
- **焦点单栈门**(F4)消除同键双触发与双 EventSystem 冲突
- **P0 不为 VR 付早投成本**(接口先行,VR 立体实现在 P1a;world-space 最小面在 P0)

### Negative

- **两栈维护成本**(UI Toolkit + UGUI):世界空间与平面 UI 两套元件观感须同步
- **UI Toolkit 自定义材质受限**(F5):程序化墨迹晕染可能落 UGUI,拟物观感有真实约束
- **多个 post-cutoff spike**(焦点桥 / world-space / 自定义材质 / 图集 / 无障碍)
- **VR 立体实现推 P1a**:P0 只有接口 + world-space 最小面(敌人读数条),VR 急救 UI 的实际观感到 P1a 才验证

### Neutral

- 拟物元件库是 UI Toolkit 优先,UGUI 侧语义对齐(个别元件可跨栈)
- 无障碍只留四钩子(2026-09-17 由三改四),具体要求归 `/ux-design`

## Risks

| Risk | Probability | Impact | Mitigation |
| --- | --- | --- | --- |
| **UI Toolkit 运行时手柄焦点导航质量不达预期**(§6.6 假设 6「半可信」) | 中 | **高** | 焦点导航原型 spike(P0 早期,两天量级);不达预期回自实现焦点算法(接口不变);复用 ADR-011 spike |
| **同键双触发 / 双 EventSystem**(R-4 接口 + 官方桥) | 中 | 中 | 冻单一来源(只用官方 `Navigate`);禁 `StandaloneInputModule` 并存;焦点单栈门(F4) |
| **UI Toolkit 自定义 shader 受限**(程序化墨迹晕染) | 中 | 中 | F5 spike;不能则烘焙进贴图或落 UGUI(语义仍同库) |
| **图集撑爆断批**(大量纸纹贴图) | 中 | 中 | F6 护栏:控贴图尺寸数量 + 调 `PanelSettings` atlas;spike 阈值 |
| **两栈观感漂移**(UI Toolkit vs UGUI 拟物效果不一致) | 中 | 中 | 元件库语义对齐(同名元件);两栈共主题变量;个别元件跨栈须审 |
| **VR world-space 立体 / 深度问题** | 中 | 中 | VR 必须 World Space(F3);Overlay 避用;P1a spike 定案 |
| **`disease_id` 泄漏进呈现层**(反射扫描漏 `List<DTO>` / 继承字段) | 低 | **高** | `PresentationDtoGuard` **递归**扫描;AC-37-15 BLOCKING 门(F7) |
| **无障碍接口留而不用**(钩子成摆设) | 中 | 低 | 四钩子进 `/ux-design` 的输入;具体要求落 `accessibility-requirements.md` |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU(UI 帧时间) | — | UI Toolkit 保留模式(仅脏元素重绘);UGUI world canvas 仅 VR 模式 | 16.6 ms 平面 / 11.1 ms VR |
| Draw Calls | — | UI Toolkit 按 batch(受唯一纹理 / 材质数限,F6);UGUI world canvas 双眼翻倍(VR) | 待定 |
| VR 帧时间 | — | UGUI world canvas 须极小(急救 UI 本就小) | 11.1 ms(VR 硬性) |
| Memory | — | USS 元件库 + 纸纹图集 + 两栈 | 待定 |

## Migration Plan

**本项目尚无 UI 实现 —— 无迁移,本 ADR 是「第一次就做对」。(同 ADR-005 / 010 / 011 / 012)**

1. **焦点导航原型 spike**(复现报告 §6.6 假设 6 的验证;复用 ADR-011 spike)。
2. **建 USS 拟物元件库**(纸纹 / 墨迹 / 卷轴九宫格 + 主题变量)。
3. **写 UI Toolkit 平面拟物 UI 框架**(42)+ 焦点导航呈现侧接线。
4. **写 39 脉案 / 7b 存档位 UI**(消费元件库)。
5. **P1a:world-space 栈的立体适配 + VR 立体渲染**(UGUI world canvas + stereo)+ 43 纸质地图。
   (P0 已交付 world-space 最小面 = 敌人读数条。)
6. **无障碍四钩子接入** `/ux-design` 的产物。

**Rollback plan**:若 UI Toolkit 焦点导航 spike FAIL(质量不达预期),降级 = 自实现焦点算法
(ADR-011 接口 `FocusNavigationIntent` 不变,只换呈现侧实现)—— **两栈边界与元件库契约不变**。
若 F5(自定义材质)使拟物观感不可接受,该元件迁 UGUI —— 也不改两栈边界。

## Validation Criteria

- [ ] **焦点导航原型 spike 通过**:UI Toolkit + 官方桥(`NavigationMoveEvent`)手柄焦点质量达标;
      无同键双触发(只用官方 `Navigate` action)
- [ ] **焦点单栈门生效**:ADR-011 意图流同一时刻仅喂一栈;无双 EventSystem / 双输入模块(F4)
- [ ] **UI Toolkit 平面拟物框架**:脉案 / **库存容器** / 存档位用 USS 元件库渲染,42 不持有游戏状态
      (43 出诊箱 = **P1a**,不在 P0 这一项内 —— 2026-09-16 订正)
- [ ] **world-space 最小实现(P0)**:敌人读数条 = 世界锚点 → billboard 面片,可读、可淡出;
      **不含**立体 / 深度冲突处理(2026-09-16 新增;与其立体适配 P1a 分开签核)
- [ ] **USS 拟物元件库**:纸纹 / 墨迹 / 卷轴九宫格 + 主题变量;`-unity-slice-*` 生效(F5)
- [ ] **`disease_id` 反射扫描门**:`PresentationDtoGuard` **递归**扫 UI DTO 树,无 `disease_id`(AC-37-15)
- [ ] **UI 只读 DTO**:UI 经 `IVitalsQuery → VitalsDto` 读数据;UI 层零模拟写(ADR-005 / ADR-011)
- [ ] **VR world-space spike(P1a)**:UGUI World Space 在 XR 下立体 / 深度正确(Overlay 未用)
- [ ] **图集护栏**:纸纹贴图规模下无 spill / 断批(F6)
- [ ] **无障碍四钩子留接口**:命名 / 文本缩放 / 焦点可见样式 / **动效缩放**(`UnityEngine.Accessibility` spike)
- [ ] **`TR-concept-008` 覆盖** —— registry 状态更新为 covered;`TR-diag-019/020`、`TR-case-019` 加引用
- [ ] `design/ux/interaction-patterns.md`、`design/accessibility-requirements.md` 撰写(`/ux-design` 预门控)

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Addresses It |
|--------------|--------|-------------|--------------------------|
| `design/gdd/systems-index.md` | 42 拟物 UI 框架 | 只渲染,永不持有游戏状态(§9 C3) | 图 §架构;42 是纯渲染器,映射表归 8 |
| `design/gdd/systems-index.md` | 39 脉案 | 呈现形态(体征词 / 未查标记 / 病名栏) | UI Toolkit 平面拟物 UI;`disease_id` 不进 DTO |
| `design/gdd/systems-index.md` | 43 纸质地图与出诊箱 | P1a 起承接地图可视化 | P1a 两栈适配;同元件库 |
| `design/gdd/systems-index.md` | 7b 存档位 UI | 存档位界面 | UI Toolkit 平面拟物 UI |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | 脉案是焦点导航界面 · 无 hover-only · VR 不做浮窗 | 焦点导航呈现侧 + 单栈门;VR 走 UGUI world space |
| `design/gdd/diagnosis-system.md` | 8 诊断与体征 | 体征 → 呈现形态映射表归 8;42 只渲染 | 数据边界:42 是渲染器,映射表归 8 |
| `design/gdd/case-system.md` | 37 病例系统 | 规则九 `disease_id` 不进呈现层(AC-37-15) | `PresentationDtoGuard` 递归反射扫描 |
| `design/gdd/game-concept.md` | 全案 | 拟物 UI 须同时支持键鼠与手柄焦点导航 | UI Toolkit 焦点桥 + UGUI 单栈门 |
| `design/gdd/interaction-system.md` | 4 交互系统 | **4 需读「是否有模态界面摊开」以拒收世界交互意图**(`F-4.2` 第二门 · `AC-4-09`) | **§十 Amendment A 新立 `IModalState.Modal`**(承既有两 getter 语义不符 —— 详见该节病因表) |

## Related

- **ADR-011 输入架构**(Accepted)—— 焦点导航接口侧;本 ADR 是呈现侧的 R-6 落点
- **ADR-005 确定性模拟**(Accepted)—— UI 只读 `VitalsDto`(唯一 float 出口)
- **ADR-006 定点域边界**(Accepted)—— 表现层 float 隔离;UI 不接触定点域
- **ADR-008 病例流**(Accepted)—— `disease_id` 不进呈现层 / AC-37-15
- **ADR-009 世界状态边界**(Accepted)—— 表现态与模拟态分离
- **ADR-010 持久化**(Accepted)—— 7b 存档位 UI
- **R-6 拟物 UI 框架**(architecture-review 2026-09-15)—— 本 ADR 是其落点;E-16 由本 ADR 早决
- **`/ux-design`**(预门控)—— `design/ux/interaction-patterns.md`、`design/accessibility-requirements.md`;四钩子接入口
- `docs/registry/architecture.yaml` —— 本 ADR 新增 ui_stack_boundary 契约 + API 裁决 + 禁令
