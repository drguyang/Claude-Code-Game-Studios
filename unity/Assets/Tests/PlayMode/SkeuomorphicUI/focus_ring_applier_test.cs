// B-6-2 · 焦点可见施加者接线门(PlayMode · 2026-10-14)
//
// ▍病因(第六轮 playtest `playtest-2026-10-13-m2-round6.md` §6 B-6-2 · P2)
//   键盘玩家「看不到焦点在哪」。焦点环样式与契约 story-009 已全做完
//   (SkeuoFocusVisible.uss + FocusVisibleStyle),但「焦点转移 → 施加/移除
//   .focus-visible」的施加者**从未接线**(story-009 注记「接线留待焦点接线轮」)。
//   B-6-1 接通导航后,引擎 FocusController 已在移动焦点 —— 缺的只是呈现翻译。
//
// ▍修法(甲案 · 用户裁定 2026-10-14):环走 overlay 子元素
//   SkeuoRuntimeDriver.AttachFocusRingApplier 监听面板根 FocusIn/FocusOut 冒泡,
//   获焦目标内部 append 环子元素(挂 .focus-visible,absolute 覆盖全周),
//   失焦移除。story-021 登记的「.focus-visible × .ruled border 加载序冲突」
//   与单槽铁律「纸纹被顶掉」两风险**结构性注销**(overlay 与目标永不同元素)。
//
// ▍判据(可证伪):
//   ① FocusIn ⇒ 目标出现环子元素;FocusOut ⇒ 移除;焦点转移 ⇒ 环随迁(旧摘新挂);
//   ② 防重入:同目标重复 FocusIn ⇒ 环不翻倍;
//   ③ 变异面:删 AttachFocusRingApplier 调用 ⇒ ①② 全零环 ⇒ 恰红。
//
// ⚠️ 不加 BootRoot / 不建 EventSystem —— 本门只测**施加者**(driver 内部),
//   焦点用 root.Focus() 直投(引擎真源;不经输入模块,与 B-6-1 门分工)。

using System.Collections;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>焦点环施加者判据(B-6-2)。</summary>
    internal sealed class focus_ring_applier_test
    {
        private GameObject _go;
        private PanelSettings _panelSettings;
        private UIDocument _doc;

        [TearDown]
        public void TearDown()
        {
            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _panelSettings = null; _go = null;
        }

        private IEnumerator SetUp()
        {
            _go = new GameObject("FocusRing_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            // driver 的 Awake 即含施加者接线(生产路径)—— 不反射、不走旁路
            _go.AddComponent<SkeuoRuntimeDriver>();
            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null)
            {
                if (Time.realtimeSinceStartup - t0 > 2f)
                {
                    Assert.Fail("[夹具红] rootVisualElement 2s 未就绪");
                    yield break;
                }
                yield return null;
            }
            yield return null;   // 等 Awake 完成回调注册
        }

        private static VisualElement RingOf(VisualElement target)
        {
            for (int i = 0; i < target.childCount; i++)
            {
                var child = target[i];
                if (child.ClassListContains(FocusVisibleStyle.ClassName))
                    return child;
            }
            return null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 焦点转移 ⇒ 环随迁(旧摘新挂)—— 主判据
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_focusMove_ringFollows_betweenTwoRows()
        {
            yield return SetUp();
            var root = _doc.rootVisualElement;

            var rowA = new VisualElement { name = "row-a" };
            var rowB = new VisualElement { name = "row-b" };
            rowA.focusable = true;
            rowB.focusable = true;
            root.Add(rowA); root.Add(rowB);
            yield return null;

            Assert.IsNull(RingOf(rowA), "前置:未获焦 ⇒ 无环(施加者不得无中生有)");
            Assert.IsNull(RingOf(rowB), "前置:未获焦 ⇒ 无环");

            // Act:焦点给 A
            rowA.Focus();
            yield return null;

            Assert.IsNotNull(RingOf(rowA),
                "① 红:获焦 ⇒ 目标内无环子元素 —— 施加者未接线(B-6-2 病根复现)" +
                "(变异:删 AttachFocusRingApplier 调用 ⇒ 本测恰红)");
            Assert.IsNull(RingOf(rowB), "① 红:焦点在 A,B 不得有环");

            // Act:焦点转 B
            rowB.Focus();
            yield return null;

            Assert.IsNull(RingOf(rowA),
                "① 红:焦点转走 ⇒ 旧目标环**必须摘除**(高亮残留 = 玩家看到两个焦点)");
            Assert.IsNotNull(RingOf(rowB), "① 红:新目标须有环(环随焦点转移)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 防重入 + ③ 零焦点 ⇒ 零残留(门关语义)
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_focusInRepeated_ringNotDuplicated_andBlurRemovesAll()
        {
            yield return SetUp();
            var root = _doc.rootVisualElement;

            var row = new VisualElement { name = "row" };
            row.focusable = true;
            root.Add(row);
            yield return null;

            // Act:重复 FocusIn(同目标两次)
            row.Focus();
            yield return null;
            row.Blur();
            yield return null;
            row.Focus();          // 第二次获焦
            yield return null;
            row.Focus();          // 第三次(已持焦的元素再 Focus 不重发 FocusIn,但防重入守卫仍须在)

            Assert.IsNotNull(RingOf(row), "防重入前置:获焦 ⇒ 有环");
            Assert.AreEqual(1, CountRings(row),
                "② 红:同目标重复获焦 ⇒ 环翻倍 —— FindFocusRing 防重入守卫失效");

            // Act:失焦
            row.Blur();
            yield return null;

            Assert.AreEqual(0, CountRings(row),
                "③ 红:失焦 ⇒ 环必须移除(无焦点 ⇒ 无高亮,不得残留 —— 契约原文)");
        }

        private static int CountRings(VisualElement target)
        {
            int n = 0;
            for (int i = 0; i < target.childCount; i++)
            {
                if (target[i].ClassListContains(FocusVisibleStyle.ClassName)) n++;
            }
            return n;
        }
    }
}
