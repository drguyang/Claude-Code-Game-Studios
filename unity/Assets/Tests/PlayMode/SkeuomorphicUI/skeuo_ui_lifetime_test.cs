// 2026-11-02 · `TD-skeuo-02` 收口 —— 42 的 UI 载体必须随 `BootRoot` 一同销毁。
//
// 债:`BootRoot.FindOrCreateSkeuoUi` 建 / 取到的 `SkeuoUI` **不归 BootRoot 层级**,
//     而 Boot.unity 的预摆壳是与 BootRoot 平级的**根对象**(`m_Father: 0`)⇒ BootRoot
//     销毁后它存活 ⇒ 下一件 PlayMode 夹具经 `FindFirstObjectByType` 复用**同一只驱动** ⇒
//     其 `CurrentModal` / 屏 / 注入面原样带入 ⇒ **偶发红**(依赖用例执行序)。
//
// 判据(逐条可证伪):
//   ① **归位**:预摆壳(场景根)被取用后,其父级 = 该 `BootRoot`,且**是同一实例**(不重建
//      —— 预摆壳持有 UIDocument + PanelSettings,重建只会得到 panel == null 的空壳);
//   ② **销毁闭合**:销毁 `BootRoot` ⇒ **零** `SkeuoRuntimeDriver` 存活(含 inactive 扫描);
//   ③ **新实例 + 干净态**:新建 `BootRoot` 拿到的是**新**驱动,`CurrentModal == None`
//      且 `BlocksPlay == false` —— 上一件的 `Pause` 不带入。
//
// ⚠️ 前置断言(F4 空洞正控):③ 的「None」只有在「上一件确实处在 `Pause`」时才具判别力,
//   故先断言 `OpenModal(Pause)` 成功且 `CurrentModal == Pause`。缺它的话,
//   把 `OpenModal` 删掉本条照样绿 —— 什么都没测。
//
// ⚠️ **本件不跑启动序**:`BootRoot` 全程 `SetActive(false)`(`Awake` / `Start` 不执行)⇒
//   不触 Addressables、不建世界。被测面**只有** `FindOrCreateSkeuoUi` 的归属与生命周期;
//   生产里 BootRoot 是 active 的,但那不改变该方法的层级与创建逻辑。
//
// ⚠️ 与旧论断的差异(如实登记):债册原文举的失效观测是「前一件把模态关成 `None` 后,
//   后一件读到启动态 `Casebook`」。2026-11-02 起启动形态改为**无常驻屏**(`_screenName`
//   为空 ⇒ `CurrentModal = None`)⇒ 本件改用反向同构的脏态(`Pause`),判据的性质不变:
//   **非 None 的模态态不得跨 `BootRoot` 带入**。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>`SkeuoUI` 载体的归属与销毁闭合(`TD-skeuo-02`)。</summary>
    internal sealed class skeuo_ui_lifetime_test
    {
        private const float PanelTimeoutSeconds = 5f;
        private const string SelfBuiltWarning =
            "[BootRoot] Boot.unity 未预摆 SkeuoUI(UIDocument + PanelSettings)—— " +
            "自建空壳 ⇒ UI 逻辑树在但不渲染(panel == null,2026-10-10 实测)。" +
            "生产场景应保留预摆;测试环境可忽略本警告。";

        private GameObject _shell;
        private PanelSettings _panelSettings;
        private UIDocument _doc;
        private SkeuoRuntimeDriver _driver;
        private readonly List<GameObject> _roots = new List<GameObject>();

        /// <summary>
        /// 清场:本件断言「销毁后**零**驱动存活」是**全局**判据,而前一夹具若用普通
        /// `[TearDown]` 的 `Object.Destroy`,销毁**延迟到帧末** ⇒ 断言当刻它还活着。
        /// 2026-11-02 全量 PlayMode 实测踩到过一次(`skeuo_stylesheet_mount_probe_test`
        /// 先跑 ⇒ 本件第二条红,而单独过滤跑是绿的)。⇒ 开局先清空并让出一帧,
        /// 使判据**与执行序无关**(本债的病灶正是「判据依赖执行序」,夹具自己不能犯)。
        /// </summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ClearAllDrivers();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject root in _roots)
                if (root != null) UnityEngine.Object.Destroy(root);
            _roots.Clear();

            if (_shell != null) UnityEngine.Object.Destroy(_shell);
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);
            _shell = null; _panelSettings = null; _doc = null; _driver = null;

            yield return null;

            // 兜底:本件失败时可能留下壳 —— 不清会串到后面所有 PlayMode 用例(正是本债的形态)。
            ClearAllDrivers();
            yield return null;
        }

        /// <summary>拆掉场上所有 `SkeuoRuntimeDriver`(含 inactive)。</summary>
        private static void ClearAllDrivers()
        {
            foreach (SkeuoRuntimeDriver d in UnityEngine.Object.FindObjectsByType<SkeuoRuntimeDriver>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (d != null) UnityEngine.Object.Destroy(d.gameObject);
        }

        // ══════════════ ① 归位 ══════════════

        [UnityTest]
        public IEnumerator test_tdSkeuo02_preplacedShell_isParentedUnderBootRoot()
        {
            yield return MakePreplacedShell();
            SkeuoRuntimeDriver original = _driver;

            BootRoot boot = NewInactiveBootRoot("BootRoot_Lifetime_A");
            SkeuoRuntimeDriver got = FindOrCreate(boot);

            Assert.AreSame(original, got,
                "同一层级内复用 —— 预摆壳是 UIDocument + PanelSettings 的唯一来源,不得重建");
            Assert.AreSame(boot.transform, got.transform.parent,
                "预摆壳必须归 BootRoot 层级 —— 否则 BootRoot 销毁后它存活(TD-skeuo-02 的失效形态)");
        }

        // ══════════════ ② + ③ 销毁闭合 / 新实例干净态 ══════════════

        [UnityTest]
        public IEnumerator test_tdSkeuo02_destroyingBootRoot_leavesNoDriver_andNextBootGetsFreshState()
        {
            yield return MakePreplacedShell();

            // ── 前置(F4):先让驱动进入一个**非 None** 的脏态 ──
            Assert.IsTrue(_driver.OpenModal(ModalId.Pause), "前置:暂停壳开得出来");
            Assert.AreEqual(ModalId.Pause, _driver.CurrentModal, "前置:上一件确实处在 Pause");
            SkeuoRuntimeDriver first = _driver;

            BootRoot bootA = NewInactiveBootRoot("BootRoot_Lifetime_B1");
            Assert.AreSame(first, FindOrCreate(bootA), "前提:bootA 取用的就是预摆那只");

            UnityEngine.Object.Destroy(bootA.gameObject);
            yield return null;
            yield return null;

            // ── ② 结构性闭合:一只都不许活 ──
            SkeuoRuntimeDriver[] alive = UnityEngine.Object.FindObjectsByType<SkeuoRuntimeDriver>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(0, alive.Length,
                "BootRoot 销毁后不得有驱动存活 —— 存活即跨夹具串场(模态态 / 屏 / 注入面原样带入)");
            Assert.IsTrue(first == null, "被销毁的驱动应已成 Unity 空引用");

            // ── ③ 新 BootRoot ⇒ 新实例 + 干净态 ──
            BootRoot bootB = NewInactiveBootRoot("BootRoot_Lifetime_B2");
            // 自建空壳是**已知**警告(无预摆),显式认领 —— 不让它当噪声或意外失败源。
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(SelfBuiltWarning)));
            SkeuoRuntimeDriver second = FindOrCreate(bootB);

            Assert.IsFalse(ReferenceEquals(first, second),
                "新 BootRoot 必须拿到**新实例**(不得复用上一件已销毁的驱动)");
            Assert.AreEqual(ModalId.None, second.CurrentModal,
                "新驱动的模态态必须是 None —— 上一件的 Pause 不得带入");
            Assert.IsFalse(second.BlocksPlay, "新驱动不得带着上一件的「挡世界」态");
            Assert.AreSame(bootB.transform, second.transform.parent,
                "自建件同样归 BootRoot 层级(否则下一次销毁照样泄漏)");
        }

        // ══════════════ 辅助 ══════════════

        /// <summary>按 Boot.unity 的预摆形状造壳:根对象 + UIDocument + PanelSettings + 驱动。</summary>
        private IEnumerator MakePreplacedShell()
        {
            _shell = new GameObject("SkeuoUI");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _shell.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _driver = _shell.AddComponent<SkeuoRuntimeDriver>();

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > PanelTimeoutSeconds)
                    Assert.Fail($"{PanelTimeoutSeconds}s 内面板未 attach —— 本门要**真面板**才开得出模态");
                yield return null;
            }
        }

        /// <summary>建一个**不启动**的 BootRoot(inactive ⇒ `Awake` / `Start` 都不跑)。</summary>
        private BootRoot NewInactiveBootRoot(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _roots.Add(go);
            return go.AddComponent<BootRoot>();
        }

        /// <summary>调私有装载缝 `FindOrCreateSkeuoUi`(本债的被测面)。</summary>
        private static SkeuoRuntimeDriver FindOrCreate(BootRoot boot)
        {
            MethodInfo m = typeof(BootRoot).GetMethod(
                "FindOrCreateSkeuoUi", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, "前提:私有装载缝 `FindOrCreateSkeuoUi` 存在");
            var driver = m.Invoke(boot, null) as SkeuoRuntimeDriver;
            Assert.IsNotNull(driver, "前提:`FindOrCreateSkeuoUi` 必须返回驱动(不得 null)");
            return driver;
        }
    }
}
