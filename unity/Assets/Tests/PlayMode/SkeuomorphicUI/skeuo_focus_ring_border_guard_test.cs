// `TD-skeuo-01` 丁案 —— 代码灌注 sprite 的 **fail-loud 守卫**(PlayMode · 2026-11-02)
//
// ▍病因(实测归因)
//   **代码灌注 sprite** 路径上,当元素**两轴都**小于 sprite 的 border 之和时,
//   渲染成**纯色黑块** —— 零报错、零夹具转红(元素尺寸不在任何断言面上)。
//   12 格同屏探针把三个变量全部分离,结论两条:
//     ① 黑块**只出现在代码灌注 sprite** 路径(USS 路径**结构性免疫** ——
//        `url("guid:…")` 解析成 Texture2D、`sprite == null` ⇒ slice 被纹理尺寸钳住);
//     ② 触发条件是「border 之和 > 元素尺寸」且**两轴同时**成立。
//   证据:`production/qa/evidence/skeuomorphic-ui/nineslice-probe-2026-10-31*`。
//
// ▍本件钉什么(丁案 = 只护住**生产唯一的那个**灌注点,不造通用门)
//   生产里唯一的代码灌注点 = `SkeuoRuntimeDriver.CreateFocusRing`(焦点环,border = 2)。
//   守卫把「将来 slice 调大 / 环缩小」造成的**静默黑块**变成**抛**。
//
// ▍为什么是运行期守卫而不是静态门(必须读,否则会以为本件偷懒)
//   元素尺寸**由布局决定**,编译期读不到 ⇒ 静态门在这里不可靠。
//   债册原文的两条候选落点(让 `NineSliceBoundsValidator` 有生产调用方 / 给 42 元件库
//   `Create` 加尺寸下限)**都会改动 42 元件库的对外形状,须裁** ⇒ 未取。
//
// ▍判据(可证伪)
//   ① border 之和 ≤ 下界(正常态,border = 2)⇒ **不抛**,且环上真有 inline 图;
//   ② border 之和 > 下界 ⇒ **抛** `InvalidOperationException`,报文点名「黑块」与「TD-skeuo-01」。
//   ⇒ 判别力:把守卫整段删掉 ⇒ ② 转红(它不再抛,静默接受)。
//
// ▍本件的边界(先声明,防借绿)
//   · **不测渲染** —— `-nographics` 下没有像素,「黑块」本身**不在面内**(它已由探针截图定死)。
//     本件测的是**消费方的反应**(抛 / 不抛)。
//   · **不测 USS 路径** —— 那条路径结构性免疫,没有可触发的守卫(且它不是本债的面)。
//   · **不测真实环尺寸** —— 装置里环的 worldBound 由测试给(测试不加载样式表;
//     USS 侧的 44 来自 `--skeuo-shared-row-height`,本件不依赖它,只用常量下界)。
//   · **不造通用门** —— 只护住这一个灌注点;新加灌注点仍会静默黑块。
//     该残余已在债册明写,且「加通用门」保留为下一次触发条件。

using System.Collections;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>焦点环代码灌注 sprite 的 border 守卫(`TD-skeuo-01` 丁案)。</summary>
    internal sealed class skeuo_focus_ring_border_guard_test
    {
        private GameObject _go;
        private PanelSettings _panelSettings;
        private UIDocument _doc;
        private SkeuoRuntimeDriver _driver;

        [TearDown]
        public void TearDown()
        {
            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _panelSettings = null; _go = null; _driver = null;
        }

        private IEnumerator SetUp()
        {
            _go = new GameObject("FocusRingBorderGuard_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            // 走生产路径:driver 的 Awake 即注册施加者,不反射、不走旁路。
            _driver = _go.AddComponent<SkeuoRuntimeDriver>();

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > 3f)
                {
                    Assert.Fail("[夹具红] rootVisualElement / panel 3s 未就绪");
                    yield break;
                }
                yield return null;
            }
            yield return null;   // 等 Awake 完成回调注册
        }

        /// <summary>造一张带指定 border 的 sprite(PlayMode 无 AssetDatabase,不引真资产)。</summary>
        private static Sprite MakeSprite(int borderPx)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            return Sprite.Create(tex, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect,
                new Vector4(borderPx, borderPx, borderPx, borderPx));
        }

        private static VisualElement RingOf(VisualElement target)
        {
            for (int i = 0; i < target.childCount; i++)
                if (target[i].ClassListContains(FocusVisibleStyle.ClassName)) return target[i];
            return null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 正常态(border = 2,与生产同值)⇒ 不抛,且图真进到环上
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_tdskeuo01_smallBorder_doesNotThrow_andInjectsImage()
        {
            yield return SetUp();
            var root = _doc.rootVisualElement;

            var row = new VisualElement { name = "row-normal" };
            row.focusable = true;
            root.Add(row);
            yield return null;

            // 生产值:border = 2(见 SkeuoRuntimeDriver 的 `_focusRingSprite` doc)
            var small = MakeSprite(borderPx: 2);
            _driver.FocusRingSprite = small;

            row.Focus();
            yield return null;

            VisualElement ring = RingOf(row);
            Assert.IsNotNull(ring, "① 前置:获焦 ⇒ 环子元素须在(施加者已接线)");
            Assert.That(ring.style.backgroundImage.value.sprite, Is.SameAs(small),
                "① border = 2 未越界 ⇒ 守卫**不得**抛,且图须真灌注到环上" +
                "(变异:把守卫改成无条件抛 ⇒ 本测红)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 越界态(border 之和 > 下界)⇒ fail-loud,且报文可诊断
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_tdskeuo01_oversizedBorder_throwsLoudly()
        {
            yield return SetUp();
            var root = _doc.rootVisualElement;

            var row = new VisualElement { name = "row-oversized" };
            row.focusable = true;
            root.Add(row);
            yield return null;

            // 200px border ⇒ 和 = 200 > 下界(44)⇒ 命中黑块触发条件
            var huge = MakeSprite(borderPx: 200);
            _driver.FocusRingSprite = huge;

            // `FocusIn` 在 `Focus()` 内**同步**派发 ⇒ 异常从这里直接冒出来。
            var ex = Assert.Throws<System.InvalidOperationException>(() => row.Focus(),
                "② 越界(border 和 200 > 下界 44)⇒ 须 fail-loud,不得静默接受");
            StringAssert.Contains("黑块", ex.Message, "② 报文须点名失败形态(黑块)");
            StringAssert.Contains("TD-skeuo-01", ex.Message, "② 报文须点名债号(可追溯)");
            StringAssert.Contains("400", ex.Message, "② 报文须带上实见值(可诊断)—— border=200 四向 ⇒ 和 = 400");

            yield return null;

            // 副作用判据:抛之前不得已 append(半成品环留在树上是静默残留形态)
            Assert.IsNull(RingOf(row),
                "② 越界 ⇒ 须在**灌注/append 之前**抛 ⇒ 树上不得留半成品环");
        }
    }
}
