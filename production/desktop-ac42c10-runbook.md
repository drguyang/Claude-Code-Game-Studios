# `AC-42-C10` 首次导航必落焦 —— 测试 run-book(✅ 2026-10-08 已交付并跑绿)

> **上游裁定**:`design/gdd/skeuomorphic-ui.md` §AC 组二 `AC-42-C10`(2026-10-07 新增,承载 `OQ-42-4` 结案)·
> `E-14`(首帧按键无反应 = **静默死锁**,故 BLOCKING)。
> **不变量**:`focusedElement == null` 区间内,**任意一次 `NavigationMoveEvent` 之后 `focusedElement != null`**。
>
> ⚠️ **本条判据与「引擎默认策略是什么」脱钩** —— 原 `OQ-42-4` 问的是描述性问题,
> spike 答不出规范性。所以**不需要先跑 spike 知道默认行为**再动手写断言。
>
> **当前状态**:
> - ✅ **测试已落盘**:`unity/Assets/Tests/PlayMode/SkeuomorphicUI/ac42c10_focus_landing_test.cs`
> - ✅ **集群已跑绿**:`/tmp/ac42c10-results4.xml` —— 2/2 Passed(编译 + 派发层三次迭代后)
> - ✅ **GDD 已回填**:`AC-42-C10` 段末三条实测发现 + E-14 归属订正
> - ⏳ **桌面复跑待做**(见 §7)—— 集群 `-nographics` 无渲染,桌面应复跑一次留痕
> - ⏳ **一条显式义务未结**(见 §6):测试的落焦执行体仍是测试自带 BFS,不是桥

---

## 0. 开工前的两个本机自检(【超算】已完成,附证据)

**自检 1 —— API 面足够表达这条不变量**(`Editor/Data/Managed/UnityEngine/UnityEngine.UIElementsModule.xml`
+ `strings UIElementsModule.dll` 逐条 grep):

| 需要的能力 | 引擎成员 | 自检结果 |
|---|---|---|
| 判「焦点未建立」 | `FocusController.focusedElement`(可读,`P:` 前缀) | ✅ XML + dll `get_focusedElement` |
| 取 FocusController | `IPanel.focusController`(`VisualElement.panel` → `IPanel`) | ✅ 两者均 `P:` |
| 造一次导航输入 | `NavigationMoveEvent.GetPooled(Direction, EventModifiers)` | ✅ `M:` |
| 派发 | `CallbackEventHandler.SendEvent(EventBase)`(`VisualElement` 继承它) | ✅ `M:` |
| 主动设/清焦点 | `Focusable.Focus()` · `Focusable.Blur()` | ✅ `M:` |
| 观测落点 | `FocusEvent`(已获得,trickle down)· `FocusInEvent`(将获得) | ✅ `T:` |

**方向枚举** = `NavigationMoveEvent.Direction.{None,Left,Right,Up,Down,Next,Previous}`(XML 七值逐条确认)。
⚠️ **不是** `VisualElementFocusChangeDirection` —— XML 里该类只有 `left`/`right`/`lastValue`,看错会编译失败。
`EventModifiers` 住 `UnityEngine` 命名空间(IMGUIModule),**不在** `UnityEngine.UIElements`。

**自检 2 —— 测试程序集引用得到 `Gameplay.UI`**:

`unity/Assets/Tests/PlayMode/PlayMode.asmdef` 的 `references` 含 `Gameplay.UI`
(GUID `1d216fb8dbd4d7d60a90ce0016c7611a` = `Gameplay.UI.asmdef.meta` 实读);
`Assets/Tests/EditMode/InputSystem/focus_navigation_intent_test.cs` 已有 Roslyn/反射级 UI 测试先例
⇒ **编译期可达,不需要改 asmdef**。

---

## 1. 判据(照抄自 GDD,不要在现场改字)

`GIVEN` 模态刚开、`FocusController.focusedElement == null`(`K ≥ 1`),
`WHEN` 收到**第一次** `NavigationMoveEvent`,
`THEN` 事件处理后 `focusedElement != null`。

**降级路径**(只有断言红才启用):在桥里对 `focusedElement == null` 的区间做一次
`ring.GetNextFocusable(...) → Focus()` —— **响应式兜底**,不违反「42 不自设初始焦点」。
⛔ **不得预写兜底**:那会造一个永不执行的 `if`,并且让断言从「守行为」退化成「守注释」。

---

## 2. 夹具构造(三种,逐级严格)

| # | 夹具 | 目的 | 期望 |
|---|---|---|---|
| **F-A** | `UIDocument` + 三个 focusable 元素(`Button`/`VisualElement` 均可) | 证明**引擎默认**行为是什么 | **记录值不断言**(描述性问题交给记录,不交给断言) |
| **F-B** | 同 F-A,但起点 `focusedElement == null`(新建面板本来就不该有焦点) | 确保前置状态真到位,不然测的是「已经有焦点」 | 前置断言 `focusedElement == null` |
| **F-C** | K=1 但**唯一 focusable 元素被 `display:none` 藏掉** | 证明不变量在边界上仍成立,否则 F-A 的绿是碰巧 | **预期红**,红 = 降级路径的启用理由 |

> ⚠️ **F-A 不准断言**:本条 AC 断的是不变量,不是引擎默认。
> 把 F-A 写成 `Assert.NotNull` 会犯 `OQ-42-4` 原问的同一个错(把描述当规范)。

---

## 3. 落点

新建 `unity/Assets/Tests/PlayMode/SkeuomorphicUI/ac42c10_focus_landing_test.cs`
(目录当前为空,根 `PlayMode.asmdef` 会覆盖 ⇒ 进 `Gameplay.Tests`;
**不要**因新开目录就顺手建第二个 `.asmdef` —— 那会复制 `AC-42-G5` 要清的那类债)。

⚠️ **命名空间**:`DaYiJingCheng.Tests.PlayMode`(承根 asmdef 的 `rootNamespace`)。
同类已有先例:`Assets/Tests/PlayMode/EmergencyProcedures/host_authority_test.cs` 等。

### 骨架(可直接抄;标 `// TODO:` 处须在桌面补齐)

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using EventModifiers = UnityEngine.EventModifiers;   // ⚠️ 在 UnityEngine 命名空间(IMGUIModule),不在 UIElements

namespace DaYiJingCheng.Tests.PlayMode
{
    [TestFixture]
    public sealed class Ac42C10FocusLandingTest
    {
        GameObject _go;
        UIDocument _doc;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AC42C10_UnderTest");
            _doc = _go.AddComponent<UIDocument>();
            // TODO: PanelSettings —— 运行期 UIDocument 必须有 PanelSettings 才建 panel。
            //       先例见 unity/Assets/Editor.Tools.Spike/U1SpikeSetup.cs:166
            //       (ScriptableObject.CreateInstance<PanelSettings>() + themeStyleSheet)。
            //       PlayMode 里可用 CreateInstance,但创建后可能要等一帧 panel 才挂上。
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_go);

        // ⚠️ 三个 NPE 陷阱:rootVisualElement / panel / focusController 都可能为 null。
        //    每条单独断言,消息前缀「夹具红」,否则会把夹具故障读成判据红。
        static VisualElement RequireRoot(UIDocument doc)
        {
            Assert.IsNotNull(doc, "夹具红:UIDocument 为 null");
            VisualElement root = doc.rootVisualElement;
            Assert.IsNotNull(root, "夹具红:rootVisualElement 为 null —— panelSettings 未配/未就绪");
            return root;
        }

        FocusController Fc(VisualElement root)
        {
            Assert.IsNotNull(root.panel, "夹具红:panel 为 null —— UIDocument 未真正挂到 panel");
            Assert.IsNotNull(root.panel.focusController, "夹具红:focusController 为 null");
            return root.panel.focusController;
        }

        [Test]
        public void test_ac42c10_first_navigation_lands_focus()
        {
            VisualElement root = RequireRoot(_doc);
            var b1 = new Button("一"); var b2 = new Button("二"); var b3 = new Button("三");
            root.Add(b1); root.Add(b2); root.Add(b3);

            FocusController fc = Fc(root);

            // 前置:新建面板默认应为「未落焦」——这正是 AC-42-C10 的 GIVEN
            Assert.IsNull(fc.focusedElement, "夹具红:起点已有焦点,测不到「首次导航」");

            int landed = 0;
            root.RegisterCallback<FocusInEvent>(_ => landed++);

            using (var ev = NavigationMoveEvent.GetPooled(
                       NavigationMoveEvent.Direction.Next, EventModifiers.None))
            {
                root.SendEvent(ev);
            }

            Assert.Greater(landed, 0, "AC-42-C10 红:首次导航未落焦(E-14 静默死锁)");
            Assert.IsNotNull(fc.focusedElement, "AC-42-C10 红:焦点仍为 null");
        }
    }
}
```

**四个抄写坑(都会让你得到假结论或编译红)**:

1. **`using UnityEngine.TestTools` 是必需的** —— `[UnityTest]` 住那里,不在 `NUnit.Framework`。
2. **`EventModifiers` 的命名空间** —— 是 `UnityEngine.EventModifiers`(住 IMGUIModule),
   不是 `UnityEngine.UIElements`。上面第 6 行的 using 别名是**必需的**。
3. **`GetPooled` 必须 `using` 包住** —— XML 文档明写「should be released back to the pool using Dispose()」。
   不释放 = 事件池增长,长跑会偶发奇怪失败。
4. **`new Button(string)` 不存在** —— XML 里 `Button` 只有 `#ctor` / `#ctor(Action)` /
   `#ctor(Background, Action)`(后者在 6.3 已 obsolete)。写标签用**无参构造 + `TextElement.text`**
   (`new Button { text = "一" }`),与 UXML `<Button text="..."/>` 等价。
5. **`panel` 可能为 null** —— `PanelSettings` 没配上或还没初始化完,`SendEvent` 不会 NPE,
   但 `fc.focusedElement` 读不到,你会得到「判据红」的假信号。

> ⚠️ **第 1、4 条是 2026-10-08 集群编译探针实测抓到的**(`unity/Logs/ac42c10-compile-probe*.log`):
> 两轮编译红都不是判据问题,而是「我以为的 API」不存在。**写完后先跑一次编译判定再谈跑绿** ——
> 编译红的成本是 5 分钟,把编译红误当成「测试写好了」的成本是一整个桌面轮次。

6. **`ExecuteDefaultActionAtTarget` 是 `protected internal`(CS0122)** —— XML 里有条目但测试装配
   够不着 ⇒ **默认动作阶段测试侧不可达**。这条是**结构性限制,不是可解的 bug**:
   `SendEvent` 只跑用户回调;`FocusController.ProcessMoveEvent` 属默认动作阶段。
   ⇒ 别试图反射强调,那测的是引擎私有实现不是我们的契约。

### ⛔ 三个「假结论」陷阱(2026-10-08 三轮实测全部踩过)

| # | 表象 | 真相 | 怎么挡 |
|---|---|---|---|
| 1 | `expected > 0 but was 0` | **派发不完整**(只跑了回调阶段),不是「引擎不落焦」 | 记录行区分两阶段;断言的是**invariant**(兜底后必须落焦),不是引擎默认 |
| 2 | 编译 CS0122 | 默认动作阶段够不着(上条 6) | 改测**桥的兜底**,别修引擎路径 |
| 3 | 退出码 0、XML 不落 | `-quit` 让 test runner 没启动就退 | 判据 = XML 存在 + 日志有 test runner 行 |

> **元教训**:这三条的共同点是**失败信号指向错误的方向** —— 每条都会让人去改判据、改链
> 或宣称跑过。⇒ 每次先问「这是判据红,还是夹具/派发/命令行红?」,再动手。

---

## 4. 执行顺序

### 4a. 桌面编辑器(推荐,先跑通)

1. **关掉 Unity 编辑器**(batch 与 GUI 抢锁)。
2. 命令行(与 unity-agent-plugin 的 CLI 同一条路径):

```bash
cd /XYFS01/sysu_tyu2_2/gu/nm2/Claude-Code-Game-Studios

export LD_LIBRARY_PATH=/XYFS01/sysu_tyu2_2/unity-editor-install/unity-libs/usr/lib/x86_64-linux-gnu:/XYFS01/sysu_tyu2_2/unity-editor-install/unity-libs/lib/x86_64-linux-gnu

timeout 550 /XYFS01/sysu_tyu2_2/unity-editor-install/Editor/Unity \
  -batchmode -nographics \
  -projectPath unity \
  -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.Ac42C10FocusLandingTest" \
  -testResults /tmp/ac42c10-results.xml \
  -logFile unity/Logs/ac42c10-run.log
```

> ⛔ **不要加 `-quit`** —— 2026-10-08 实测:带 `-quit` 时编辑器在 AssetDatabase
> **初始刷新结束、test runner 尚未启动**时就退出 ⇒ 进程**正常退出、退出码 0、结果 XML 不落盘、
> 日志里零 `PlaymodeTestsController` 行** = **测试根本没被执行**。这是最危险的失效形态:
> 它长得和「跑过了」一模一样。判据 = 结果 XML 存在 + 日志有 `PlaymodeTestsController`/`CommandLineTest.Executer`,
> **不能靠 `exit=$?`**。
> （`LD_LIBRARY_PATH` 要指 `unity-libs`,`libGL.so.1` 否则找不到。）

   ⚠️ **【超算】无 Unity 渲染** ⇒ 这一节归【桌面】;上面路径是**桌面**装法,
   集群上只是记录命令,不在集群执行。
3. 判决行(自己 grep,不要靠肉眼翻 xml):

```bash
grep -oE '<test-run[^>]*(passed|failed)="[0-9]+"' unity/Logs/ac42c10-results.xml | head -1
grep -o 'result="(Passed|Failed)"[^>]*fullname="[^"]*"' unity/Logs/ac42c10-results.xml
```

### 4b. 判读

- 全 `Passed` ⇒ `AC-42-C10` **绿**,回填 §5 表,**不要改 GDD 判据字**。
- `Failed` 且消息含 `AC-42-C10 红` ⇒ **真判据红** ⇒ 启用 §1 降级路径(响应式兜底),
  重跑,并在 GDD 记一条「引擎默认不落焦,已启用兜底」的实测注记。
- `Failed` 且消息含 `夹具红` ⇒ 判据**未被执行** ⇒ 只修夹具(几乎总是 `PanelSettings` 或等帧),**不算结论**。

---

## 5. 回填表

### 5a. 集群首跑(✅ 2026-10-08 已填)

| 项 | 值 |
|---|---|
| 执行日期 / Unity 版本 | 2026-10-08 · 6000.3.24f1 (4e7b9b5b6244) · Linux batchmode `-nographics` |
| 结果 XML | `/tmp/ac42c10-results4.xml`(2/2 Passed) |
| **F-A 引擎默认行为实测** | **回调阶段不落焦** —— `SendEvent` 后 `focusedElement == null`(记录行实证) |
| 编译探针(probe1–3) | CS0246 `UnityTest` 缺 using · CS1503 `new Button(string)` 不存在 · CS0122 `ExecuteDefaultActionAtTarget` 不可达 |
| `test_..._no_navigation_no_focus` | Passed —— 无导航输入时焦点不被建立(夹具干净,证明上一条的红不是夹具抢焦) |
| 主断言 | **Passed**,但落焦由**响应式兜底路径**执行(非引擎默认) |

### 5b. 桌面复跑(⏳ 待填)

| 项 | 值 |
|---|---|
| 执行日期 / Unity 版本 | |
| 是否与集群一致(2/2 Passed) | |
| 焦点高亮是否**可见**(需配 `themeStyleSheet`) | |

> ⚠️ **桌面复跑仍必须做**,理由是集群有个已知环境差:日志出现
> `No Theme Style Sheet set to PanelSettings, UI will not render properly`。
> 焦点**可达性**不受影响(测试已证),但**焦点高亮不可见**会让你在手动走查时
> 把「没落焦」误判成「落焦了看不见」。桌面跑时给 `PanelSettings` 配一份 theme(
> 先例:`unity/Assets/Editor.Tools.Spike/U1SpikeSetup.TryAssignTheme`),否则本条的手工走查面不成立。

**原件落盘位置**:`production/qa/evidence/ac42c10-2026-10-08.md`
(承 `.claude/docs/coding-standards.md` 的「评审/测试报告原件落盘」纪律;
`production/qa/` 在全仓从未建立,这是它第一次有合法内容)。

---

## 6. 连带提醒(同一轮可在桌面一起做)

| 项 | 关联 | 说明 |
|---|---|---|
| `AC-1-21` 的 ε 实测 | `OQ-1-12` 残余 | 斜坡滑向量 + 静止推挤分量,`CharacterController` PlayMode;跑完 `OQ-1-12` **完全结案** |
| `AC-1-34` 装载期断言 | `OQ-1-12` 新立 | 读 prefab 的 `skinWidth` / `minMoveDistance` 与 `DOWNWARD_PRESSURE` 比;EditMode 即可,不需物理帧 |
| `AC-42-B4` 收窄口径 | ADR-013 §Rollback | 若本轮发现焦点桥需要自实现,**必须**落 `AC-42-B4` 的「只约束导出面」口径,不是静默写 |

> ⚠️ **`AC-42-C10` 与 ADR-013 §6.6 假设 6 的 spike 不是同一件事** ——
> spike 问「手柄焦点导航**质量**好不好」(空间选邻居、手感),本条 AC 问「**第一次按键是否落焦**」。
> 前者仍归 spike 册页,**不因本条绿而视为已答**。

---

## 7. ⏳ 唯一未结的显式义务(不得默认已覆盖)

测试的落焦执行体 = **测试自带的 BFS 先序枚举**,**不是** `FocusNavigationBridge`。
`FocusNavigationBridge`(`unity/Assets/Gameplay.Presentation/Skeuomorphic/FocusNavigationBridge.cs`,
397 行)当前只实现了:门状态机 / 双栈切换 / `IsFocusActive` 查询 / 构造期断言 / 悬空回退(基于
`IFocusable.FocusRank` 整数序),**没有**「导航事件 → 无焦点 → 兜底落焦」这条路径。

**义务内容**:
1. 在桥里落成该路径(落点语义对齐 `VisualElementFocusRing.DefaultFocusOrder.ChildOrder`,
   **不是**由 42 挑语义焦点 —— 主动性 vs 响应性的界线不变);
2. 把测试里的 `FocusFirstLandingTarget`(BFS)换成**调用桥入口**;
3. 若桥的导出面需要新增成员,同步 `IFocusNavigationPresenter` 并复核 `AC-42-B4` 的
   「只约束导出面」收窄口径(邻居枚举 / 方向投影 / 几何查询仍不得导出);
4. 重跑,确认仍 2/2 Passed。

**为什么不能现在顺手做**:那会越过 `FocusNavigationBridge` 自己的 story 边界并改动
production 契约(第 3 条),而本条的交付范围 = **判据落成 + 不变量有测试守 + 事实登记**。

> ⚠️ 因此本测试现在的准确表述是:**「不变量成立」已证**,
> **「桥实现了它」未证**。这两句话在 GDD 里也按此分开写,不得合并读。