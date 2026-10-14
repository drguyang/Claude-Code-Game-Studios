// B-6-1 · 脉案交互面输入路径接线门(PlayMode · 2026-10-13)
//
// ▍病因(第六轮 playtest `playtest-2026-10-13-m2-round6.md` · P1 收口判据未达)
//   脉案交互面(病名栏 Submit / 页缘翻页 / 戥子选档)**鼠标点击与键盘双路径全无响应**。
//   CLI 勘察后确证是**四条正交缺陷**的叠加,不是单一根因:
//     ① 生产启动序**从未装配 `EventSystem` + `InputSystemUIInputModule`**(ADR-013 §F1/F4
//        的 P0 义务)—— `Boot.unity` 全文零 `EventSystem`、`BootRoot.cs` 零装配;
//     ② **零初始选中** —— `InputSystemUIInputModule.ProcessNavigation` 的 move(:858)与
//        submit(:876)**都 gate 于 `eventSystem.currentSelectedGameObject != null`**,而它只在
//        元素已获焦时由 `PanelEventHandler.OnElementFocus` 设 ⇒ 鸡生蛋,须运行期 bootstrap;
//     ③ **元素无鼠标 handler** —— `CasebookScreen.cs` 6 处回调原**全是** `NavigationSubmitEvent`,
//        零 `ClickEvent`,而元素是裸 `VisualElement`(非 Button,无内建 `Clickable`)
//        ⇒ 鼠标点击不触发任何业务;
//     ④ **Pointer 动作 type 错** —— `UI/{Point,Click,ScrollWheel,MiddleClick,RightClick}`
//        原为 Value/Button,verifier 要求 PassThrough。
//
// ▍本测 = **接线门**(防回退),三个判据面:
//   A. **结构门**(batch 可靠 · 作主判据):生产启动序装配后,场景内
//      ① 恰一个 `EventSystem`(防双 EventSystem 回归 · ADR-013 §F4);
//      ② 它挂 `InputSystemUIInputModule`,**无 `StandaloneInputModule`**;
//      ③ `module.actionsAsset == InputSystem.actions`;
//      ④ **8 个动作引用皆非 null**(直接守缺陷①的隐式顺序依赖 —— 最关键判据:
//         `actionsAsset` setter 只**重指向**已有引用、**不填充**空引用)。
//   B. **初始选中门**(缺陷②的可证伪判据):走生产方法(`EnsureEventSystem` +
//      `BootstrapEventSystemSelection`)配真 panel ⇒ `currentSelectedGameObject != null`。
//      删 `BootstrapEventSystemSelection` 的调用/实现 ⇒ 本测红。
//   C. **鼠标语义门**(缺陷③的可证伪判据):给真 panel 里的病名栏直投 `ClickEvent`
//      ⇒ 断言 `IsLexiconBookOpen`。删 `RegisterCallback<ClickEvent>` ⇒ 本测恰红。
//      ⚠️ 指针**端到端**(真实鼠标位置 → PanelRaycaster → 命中)在 `-nographics` 下不可行
//      (`PanelRaycaster.Raycast` 依赖 `Screen.height` / `Display.displays[].systemHeight`)
//      ⇒ 走**语义直投**,真机命中归人工腿。
//      ⚠️ 直投**必须**显式 `ev.target = bar` —— 无参 `GetPooled()` 的 `elementTarget` 为 null,
//      派发回退到**面板根**(`-nographics` 下 `GetTopElementUnderPointer` 返 null),事件到不了
//      后代 `bar`。引擎 `ClickDetector.SendClickEvent` 亦先设 `elementTarget` 再发(IL 实证)。
//
// ▍机制依据(引擎 IL 实证 · 非纸面推演;`UnityEngine.UIElementsModule.dll`):
//   · `ClickEvent` 由 `ClickDetector` 在 `PointerDown/UpEvent.PostDispatch` 内发出,目标是
//     `FindCommonAncestor(down, up)` ⇒ **任意指针下元素**都收得到,**不需要 `Clickable`**
//     —— 故缺陷③的修法(裸元素 `RegisterCallback<ClickEvent>`)正确;`Clickable` 反而不处理
//     `NavigationSubmitEvent`(故未采用)。
//   · `EventSystem.UpdateModules()` **public**(`EventSystem.cs:131`)、
//     `InputSystemUIInputModule.Process()` **public override**(`:2382`)⇒ 可确定性驱动。
//
// ⚠️ 三条判读纪律(承 `ac42c10_focus_landing_test`):
//   ① **夹具红 ≠ 判据红** —— `rootVisualElement` / `panel` 任一为 null 都是夹具故障
//      (PanelSettings 未配 / panel 未建),消息前缀 `[夹具红]`。
//   ② 本测断言的是**不变量**(装配后必恰一个桥;bootstrap 后必非 null 选中),
//      不是「引擎默认恰好如此」。
//   ③ 结构门是**主判据**(batch 可靠);B/C 两门在无渲染宿主下若遇夹具红,转【桌面】重跑,不改判据。

using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>
    /// B-6-1 输入路径接线门 —— 官方桥装配 + 初始选中 bootstrap + 鼠标点击 handler。
    /// <para>形态承 `composition_root_smoke_test`(BootRoot 真启动序 + 反射观测)与
    /// `casebook_lexicon_book_test`(真 panel 三件套)。</para>
    /// </summary>
    internal sealed class ui_input_module_wiring_test
    {
        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private GameObject _bootGo;
        private BootRoot _boot;
        private GameObject _presetCam;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 预摆 Main Camera 被启动序 SetActive(false) ⇒ **恢复 active,不销毁**
            // (承 camera_follow_wiring_test:44-46 纪律 —— 全量 run 里同 run 后续的 BootRoot
            //  测试依赖这台泄漏相机过 listener 门;销毁它 = 把红转移给下一个测试)。
            // ⚠️ 必须用 _presetCam 直引:`GameObject.Find` 只找 active,而它已被启动序禁用
            //    ⇒ Find 恒 null(首版即踩:`Destroy(preset)` 是死代码)。
            if (_presetCam != null) _presetCam.SetActive(true);

            var player = GameObject.Find("Player");
            if (player != null) Object.Destroy(player);
            var cameraRig = GameObject.Find("CameraRig");
            if (cameraRig != null) Object.Destroy(cameraRig);

            // world 场景经 Addressables 卸载(承 composition_root_smoke_test:52-76 同纪律)
            // ⚠️ yield 不可在 try/catch 体内(CS1626)⇒ 先取句柄(可抛,放 try),再在 try 外 yield。
            AsyncOperationHandle<SceneInstance> handle = default;
            if (_boot != null)
            {
                try
                {
                    var fi = typeof(BootRoot).GetField("_worldLoadHandle",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    handle = (AsyncOperationHandle<SceneInstance>)fi.GetValue(_boot);
                }
                catch (System.Exception) { /* 拆卸路径不阻断 */ }
            }
            if (handle.IsValid())
            {
                var unload = Addressables.UnloadSceneAsync(handle);
                while (!unload.IsDone) yield return null;
            }

            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_bootGo != null) Object.Destroy(_bootGo);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _panelSettings = null; _bootGo = null; _boot = null; _go = null; _presetCam = null;
            yield return null;
        }

        /// <summary>
        /// 查场景内已有的「Main Camera」(含 inactive —— 启动序会 SetActive(false))。
        /// <para>全量 run 里 <c>camera_follow_wiring_test</c> 泄漏一台;`--filter` 单跑则无。</para>
        /// </summary>
        private static GameObject FindPresetCamera()
        {
            foreach (var cam in Object.FindObjectsOfType<Camera>(includeInactive: true))
            {
                if (cam != null && cam.gameObject.name == "Main Camera")
                    return cam.gameObject;
            }
            return null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // A. 结构门(batch 可靠 · 主判据)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A①–④:生产启动序装配后,场景内恰一个 EventSystem + 挂官方桥 + 8 动作引用齐。
        /// <para>变异:删 `BootRoot.Start` 里 `EnsureEventSystem()` 调用 ⇒ 本测红(零 EventSystem);
        /// 把 `actionsAsset` 赋值挪到 `AddComponent` 之前 ⇒ 8 引用全 null ⇒ 本测红。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_bootSequence_installsSingleEventSystemWithWiredActions()
        {
            // Arrange:预摆 Main Camera(承 Boot.unity 工厂形态 —— ADR-023 §①)。
            // ⚠️ 必须有:启动序第 5 步断言「恰好 1 个 AudioListener」,缺失 ⇒ listener=0
            //    ⇒ fail-loud 抛(按设计是对的,是测试环境缺了生产场景本有的东西)。
            // ⚠️ **复用优先**(2026-10-13 全量 run 实测红→绿):`camera_follow_wiring_test`
            //    的 teardown 只 `SetActive(true)` **不销毁**它的预摆 Main Camera(承其 :44-46
            //    纪律)⇒ 同 run 内后续所有 BootRoot 测试(case_opened / composition_root /
            //    emergency / fall_reset / patient_*)都**依赖这台泄漏的相机**过 listener 门。
            //    本测若无脑再 `new GameObject("Main Camera")` ⇒ listener=2 ⇒ 启动序抛
            //    (全量 167 测实测红;`--filter` 单跑绿 —— 单跑没有那台泄漏相机)。
            //    故:先查(含 inactive —— 启动序会把它 SetActive(false)),有则复用,无则自建。
            _presetCam = FindPresetCamera();
            if (_presetCam == null)
            {
                _presetCam = new GameObject("Main Camera");
                _presetCam.AddComponent<Camera>();
            }
            if (_presetCam.GetComponent<AudioListener>() == null)
                _presetCam.AddComponent<AudioListener>();   // 仅复用的相机恰好缺 listener 才补

            // 真启动序(与门④冒烟同源)—— BootRoot.Start 内第 6.7 步装配官方桥
            _bootGo = new GameObject("BootRoot_UiInputWiring");
            _boot = _bootGo.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + 15f;
            while (!ReadBool(_boot, "_booted") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBool(_boot, "_booted"),
                "[夹具红] BootRoot 启动序 15s 未完成(_booted=false)—— 非本门判据,修夹具后重跑。");

            var es = ReadField(_boot, "_eventSystem") as EventSystem;
            Assert.IsNotNull(es,
                "A① 红:生产启动序未装配 EventSystem —— 官方桥路径不存在," +
                "UI Toolkit 退回引擎内置 DefaultEventSystem(ADR-013 §F1/F4 要求官方桥)。");

            // A① 场景内恰一个(防双 EventSystem 回归)
            var all = Object.FindObjectsOfType<EventSystem>(includeInactive: true);
            Assert.AreEqual(1, all.Length,
                $"A① 红:场景内 EventSystem 应恰 1 个,实际 {all.Length} 个 —— " +
                "双 EventSystem = 焦点栈冲突(ADR-013 §F4 焦点单栈门)。");

            // A② 挂官方桥,无 StandaloneInputModule
            var module = es.GetComponent<InputSystemUIInputModule>();
            Assert.IsNotNull(module,
                "A② 红:EventSystem 未挂 InputSystemUIInputModule —— UI Toolkit 面板收不到指针/导航事件。");
            Assert.IsNull(es.GetComponent<StandaloneInputModule>(),
                "A② 红:EventSystem 挂了 StandaloneInputModule —— ADR-013 §F4 禁两输入模块并存。");

            // A③ 动作资产 = 项目级
            Assert.AreSame(InputSystem.actions, module.actionsAsset,
                "A③ 红:module.actionsAsset ≠ InputSystem.actions —— UI 输入未接项目动作资产。");

            // A④ 8 个动作引用皆非 null(直接守缺陷①的隐式顺序依赖)
            var missing = new List<string>();
            if (module.point?.action == null) missing.Add("Point");
            if (module.move?.action == null) missing.Add("Navigate");
            if (module.submit?.action == null) missing.Add("Submit");
            if (module.cancel?.action == null) missing.Add("Cancel");
            if (module.leftClick?.action == null) missing.Add("Click");
            if (module.middleClick?.action == null) missing.Add("MiddleClick");
            if (module.rightClick?.action == null) missing.Add("RightClick");
            if (module.scrollWheel?.action == null) missing.Add("ScrollWheel");
            Assert.IsEmpty(missing,
                $"A④ 红:InputSystemUIInputModule 动作引用缺 {string.Join(", ", missing)} —— " +
                "actionsAsset setter 只**重指向**已有引用、**不填充**空引用(UpdateReferenceForNewAsset:" +
                "oldAction==null ⇒ return null)⇒ 引用空 = 该动作永不派发(静默零输入)。");
        }

        // ══════════════════════════════════════════════════════════════════════
        // B. 初始选中门(缺陷②的可证伪判据)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// B:生产方法(`EnsureEventSystem` + `BootstrapEventSystemSelection`)配真 panel
        /// ⇒ `currentSelectedGameObject != null`(打破导航/提交死锁)。
        /// <para>变异:删 `BootstrapEventSystemSelection` 调用 ⇒ 选中恒 null ⇒ 本测红。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_bootstrapSelection_selectsPanelCarrier()
        {
            // Arrange:真 panel + driver(B 需要 driver 的 GetUiRoot,供生产 bootstrap 选中面板载体)
            SetUpPanel(withDriver: true, out var driver);
            yield return WaitForRoot(_doc);
            var root = _doc.rootVisualElement;
            Assert.IsNotNull(root, "[夹具红] rootVisualElement 为 null");
            Assert.IsNotNull(root.panel,
                "[夹具红] panel 为 null —— UIDocument 未真正挂到 panel(-nographics 下可能发生,归【桌面】)。");

            _bootGo = new GameObject("BootRoot_Bootstrap");
            var boot = _bootGo.AddComponent<BootRoot>();
            boot.enabled = false;   // 禁 Start(启动序含 Addressables / world 加载,与本门无关)
            SetField(boot, "_uiDriver", driver);

            // Act:经**生产方法**建官方桥(EnsureEventSystem 内部会把它 reparent 到 boot.transform)
            var es = InvokePrivate(boot, "EnsureEventSystem") as EventSystem;
            Assert.IsNotNull(es, "[夹具红] EnsureEventSystem 返回 null");
            SetField(boot, "_eventSystem", es);

            // 等 interop bridge 建面板载体 GO(它在 EventSystem 存在后某帧建)
            // ⚠️ 转 `IRuntimePanel`(public);`BaseRuntimePanel` 是 internal ⇒ CS0122。
            float deadline = Time.realtimeSinceStartup + 3f;
            GameObject carrier = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                carrier = (root.panel as IRuntimePanel)?.selectableGameObject;
                if (carrier != null) break;
                yield return null;
            }
            Assert.IsNotNull(carrier,
                "[夹具红] 面板载体 selectableGameObject 3s 未就绪 —— interop bridge 未建面板 GO。");

            // 经**生产方法**选中(bootstrap)
            InvokePrivate(boot, "BootstrapEventSystemSelection");
            yield return null;
            InvokePrivate(boot, "BootstrapEventSystemSelection");   // 幂等,二次调用兜底桥的建立时序

            Assert.IsNotNull(es.currentSelectedGameObject,
                "B 红:初始选中仍为 null —— InputSystemUIInputModule.ProcessNavigation 的 " +
                "move(:858)/submit(:876)双门都 gate 于 currentSelectedGameObject != null " +
                "⇒ 键盘导航/提交永不派发(缺陷②死锁)。");
            Assert.AreSame(carrier, es.currentSelectedGameObject,
                "B 红:初始选中 ≠ 面板载体 —— bootstrap 未选中 panel.selectableGameObject。");
        }

        // ══════════════════════════════════════════════════════════════════════
        // C. 鼠标语义门(缺陷③的可证伪判据)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// C:直投 `ClickEvent` 到病名栏 ⇒ 病名册摊开(鼠标路径与键盘路径同语义)。
        /// <para>变异:删 `CasebookScreen.BindSubmitAndClick` 里的
        /// `RegisterCallback&lt;ClickEvent&gt;` ⇒ 本测恰红。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_diseaseNameBar_clickEvent_opensBook()
        {
            // ⚠️ **不加 driver**:driver 的 Awake 会在同一 root 上再建一个 CasebookScreen
            //    ⇒ 两个 channel-disease-name,Q<> 取到 driver 那个 ⇒ 点它开的是 driver 的屏,
            //    不是本测的 screen(2026-10-13 实测:本门首版即踩此坑)。本门只需真 panel。
            SetUpPanel(withDriver: false, out _);
            yield return WaitForRoot(_doc);
            var root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null(-nographics 下可能发生,归【桌面】)。");

            var src = new FakeSource();
            src.Seen.Add(7); src.Texts[7] = "伤寒";
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(src);
            yield return null;

            Assert.IsFalse(screen.IsLexiconBookOpen, "前置:病名册未摊开");

            var bar = root.Q<VisualElement>("channel-disease-name");
            Assert.IsNotNull(bar, "[夹具红] 未取到病名栏 channel-disease-name");

            // ⚠️ **必须显式设 `ev.target = bar`**(2026-10-13 实测红→绿的关键)。
            //    机制(`UnityEngine.UIElementsModule.dll` IL 实证):
            //    `PointerEventBase<T>.Dispatch` → `DispatchToCapturingElementOrElementUnderPointer`
            //    →(无指针捕获时)`DispatchToElementUnderPointerOrPanelRoot`。后者读
            //    `evt.elementTarget`:**非 null 则直接以它为派发目标**;null 才回退
            //    `panel.GetTopElementUnderPointer(pointerId)` —— 而 `-nographics` 下无指针位置
            //    提交,该调用返回 null,再回退 `panel.visualTree`(面板根)。
            //    ⇒ 无参 `ClickEvent.GetPooled()` 的 `elementTarget` 为 null ⇒ 事件落在**面板根**,
            //      `bar` 是其后代,气泡方向是「目标 → 祖先」,永远到不了 `bar` ⇒ handler 不触发。
            //    引擎自己的 `ClickDetector.SendClickEvent` 正是**先 `set_elementTarget(commonAncestor)`
            //    再 SendEvent**(IL:`IL_008e: callvirt set_elementTarget` / `IL_009b: SendEvent(..,2)`)
            //    —— 本行照抄该序,故仍是**引擎派发管径**,非测试自造。
            using (var ev = ClickEvent.GetPooled())
            {
                ev.target = bar;
                bar.SendEvent(ev);
            }

            Assert.IsTrue(screen.IsLexiconBookOpen,
                "C 红:直投 ClickEvent 到病名栏后病名册未摊开 —— 鼠标路径 handler 缺失" +
                "(元素是裸 VisualElement,无内建 Clickable;须 RegisterCallback<ClickEvent>)。");
        }

        // ── 夹具辅助 ──

        /// <summary>
        /// 建真 panel 宿主;可选加 <see cref="SkeuoRuntimeDriver"/>。
        /// <para>加 driver(承 <see cref="SkeuoRuntimeDriver.EnsureDocument"/>):driver 认领本
        /// UIDocument ⇒ <c>GetUiRoot()</c> 返回本 panel,供生产 bootstrap 选中面板载体(B 门需要)。</para>
        /// <para>⚠️ 加 driver 会**顺带建一个 CasebookScreen**(driver 的 Awake)—— 故 C 门
        /// 必须**不加**(否则同 root 两个 channel-disease-name,Q&lt;&gt; 取错)。</para>
        /// </summary>
        private void SetUpPanel(bool withDriver, out SkeuoRuntimeDriver driver)
        {
            _go = new GameObject("UiInputWiring_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            driver = withDriver ? _go.AddComponent<SkeuoRuntimeDriver>() : null;
        }

        private IEnumerator WaitForRoot(UIDocument doc)
        {
            float t0 = Time.realtimeSinceStartup;
            while (doc.rootVisualElement == null)
            {
                if (Time.realtimeSinceStartup - t0 > 2f)
                {
                    Assert.Fail("[夹具红] UIDocument.rootVisualElement 2s 未就绪 —— panelSettings 未生效。");
                    yield break;
                }
                yield return null;
            }
        }

        private static object InvokePrivate(object target, string method)
        {
            var mi = target.GetType().GetMethod(method,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(mi, $"[夹具红] {target.GetType().Name}.{method} 未找到(私有方法名漂移)");
            return mi.Invoke(target, null);
        }

        private static void SetField(object target, string field, object value)
        {
            var fi = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(fi, $"[夹具红] {target.GetType().Name}.{field} 未找到(私有字段名漂移)");
            fi.SetValue(target, value);
        }

        private static object ReadField(object target, string field)
        {
            var fi = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return fi?.GetValue(target);
        }

        private static bool ReadBool(object target, string field) =>
            ReadField(target, field) is bool b && b;

        // ── 假件(承 casebook_lexicon_book_test 同型)──

        private sealed class FakeSource : ICasebookDtoSource
        {
            public readonly List<int> Seen = new List<int>();
            public readonly Dictionary<int, string> Texts = new Dictionary<int, string>();
            public int ActiveSignCount => 0;
            public IReadOnlyList<SignLexemeRow> RevealedRows => new List<SignLexemeRow>();
            public int PrecisionSlot => 0;
            public long InkAgeTicks => 0;
            public IReadOnlyList<int> SeenLexiconIds => Seen;
            public byte PendingConfidence => 0;
            public int DoseDetentCount => 0;
            public string TextOfLexicon(int id) => Texts.TryGetValue(id, out var t) ? t : null;
        }

        private sealed class FakeSink : ICasebookCommandSink
        {
            public void SubmitDiseaseName(int lexiconId) { }
            public void SubmitConfidence() { }
            public void SubmitDose(int doseOrdinal) { }
            public void SubmitPageTurn() { }
            public void SubmitFold() { }
        }
    }
}
