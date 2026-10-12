// sprint-05 T2.1 / T2.2 · 42 运行时装配器接线验证(2026-10-10)
//
// 判据(可证伪):
//   ① T2.2 Screen 实例化 —— 七具名屏逐个可实例化;未知名 fail-loud(具名异常,
//      不 fallback);实例化产物挂到 driver 的 panel 上(不在虚空中建树)
//   ② T2.1 DTO 绑定 —— 合成 fixture(零真实 sim)注入后,脉案页**出现读数格**、
//      词与极性来自 fixture、形态类按 story-024 AC-024-2 阳性 / 阴性分化
//   ③ 空行铁律 —— 零词条 ⇒ 读数格**零个**(未查 = 空行,`:329` 非 badge);
//      42 零占位文字 / 零灰字 / 零「未查」标签(AC-8-23 / AC-8-43)
//   ④ BuildUI 未变形 —— story-011 双真源:既有 `casebook_render_test` 六条 +
//      交叉断言零回归(本测试不重复其判据,只钉「数据不新建行」)
//
// ⚠️ 门式读仓例外:本测试读仓库内版本化 USS / UXML 文件(判据对象 = 形态类名),
//    不依赖网络 / 数据库 / 可变外部状态。
// ⚠️ 禁假数据:fixture 的读数来自**手写 fixture 词条**(八字段形状),不是生产真值。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DiagnSignChannel = DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel;
using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>SkeuoRuntimeDriver 接线验证(sprint-05 T2.1 / T2.2)。</summary>
    public class skeuo_runtime_driver_test
    {
        private static readonly string[] AllScreenNames =
        {
            "Casebook", "ClinicPanel", "Inventory", "SaveSlots",
            "Settings", "Tutorial", "PaperCloseup",
        };

        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
                _host = null;
            }
        }

        // ── 合成 fixture(42 侧只读源的测试实现;零 sim 依赖) ──────────────

        private sealed class FakeCasebookSource : ICasebookDtoSource
        {
            private readonly int _signCount;
            private readonly List<SignLexemeRow> _rows;
            private readonly int _slot;
            private readonly long _inkAge;
            private readonly List<int> _seenLexiconIds;

            public FakeCasebookSource(int signCount,
                                      IReadOnlyList<SignLexemeRow> rows, int slot,
                                      long inkAgeTicks = 0L,
                                      IReadOnlyList<int> seenLexiconIds = null)
            {
                _signCount = signCount;
                _rows = rows != null ? new List<SignLexemeRow>(rows) : new List<SignLexemeRow>();
                _slot = slot;
                _inkAge = inkAgeTicks;
                _seenLexiconIds = seenLexiconIds != null
                    ? new List<int>(seenLexiconIds) : new List<int>();
            }

            public int ActiveSignCount => _signCount;
            public IReadOnlyList<SignLexemeRow> RevealedRows => _rows;
            public int PrecisionSlot => _slot;
            public long InkAgeTicks => _inkAge;
            public IReadOnlyList<int> SeenLexiconIds => _seenLexiconIds;

            /// <summary>待落笔档位(sprint-08 S1.1 件 E2;测试可注入)。</summary>
            public byte _pendingConfidence;
            public byte PendingConfidence => _pendingConfidence;

            public string TextOfLexicon(int lexiconId)
                => _lexiconText != null && _lexiconText.TryGetValue(lexiconId, out var t) ? t : null;

            /// <summary>词条文本表(测试可注入;默认空 ⇒ TextOfLexicon 恒 null)。</summary>
            public Dictionary<int, string> _lexiconText = new Dictionary<int, string>();

            /// <summary>方笺页戥子档数(sprint-08 S2;默认 0 ⇒ 方笺页戥子留空)。</summary>
            public int DoseDetentCount { get; set; }

            public void SetRows(IReadOnlyList<SignLexemeRow> rows)
            {
                _rows.Clear();
                if (rows != null) _rows.AddRange(rows);
            }
        }

        private static SignLexemeRow Row(string id, string[] words, DiagnSignChannel channel,
                                         SignPolarity polarity = SignPolarity.Positive)
            => new SignLexemeRow(id, words, channel,
                                 Array.Empty<RevealMethod>(), 1, polarity, null);

        /// <summary>门开的体征数(&gt; 0 ⇒ 允许 revealed 进值区)。</summary>
        private const int GateOpen = 1;

        /// <summary>门关的体征数(0 ⇒ 空行,与 F2 signs tripwire 现形同构)。</summary>
        private const int GateClosed = 0;

        // ══════════════════════════════════════════════════════════════════
        // T2.2: Screen 实例化(七具名 + 未知名 fail-loud)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_t2_2_allNamedScreens_instantiable()
        {
            foreach (string name in AllScreenNames)
            {
                var (driver, _) = BootDriver(name, null);
                Assert.IsNotNull(driver.Screen,
                    $"Screen「{name}」应可实例化(七具名闭集,零反射查找)。");
                Assert.IsInstanceOf<IPresentationRoot>(driver.Screen,
                    $"「{name}」实例须实现 IPresentationRoot。");
            }
        }

        [Test]
        public void test_t2_2_unknownScreenName_failsLoud()
        {
            // 经反射调 Awake ⇒ 异常被 TargetInvocationException 包一层;
            // 内碥(InnerException)才是判据对象(也必须恰一个:无「先吞后建」路径)。
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(
                () => BootDriver("NoSuchScreen", null));
            var inner = ex.InnerException;
            Assert.IsInstanceOf<InvalidOperationException>(inner,
                "未知名 Screen 须以 InvalidOperationException 拒绝(fail-loud,不 fallback)。");
            Assert.That(inner.Message, Does.Contain("NoSuchScreen"),
                "异常消息须点名未知屏名(排障面);fail-loud,不 fallback。");
        }

        [Test]
        public void test_t2_2_screenAttachedToDriverPanel()
        {
            var (driver, panel) = BootDriver("Casebook", null);
            var screen = driver.Screen as PresentationRoot;
            Assert.IsNotNull(screen, "CasebookScreen 继承 PresentationRoot");
            Assert.AreSame(panel, screen.Root,
                "Screen 必须挂在 driver 的 panel 上(不在虚空中建树 ——" +
                "否则 driver 装了屏但玩家什么也看不到)。");
        }

        // ══════════════════════════════════════════════════════════════════
        // T2.1: DTO 绑定(读数格 + 形态类分化 + 禁零读数假绿)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_t2_1_boundSource_drivesReadingCellsIntoRows()
        {
            var source = new FakeCasebookSource(GateOpen, new[]
            {
                Row("s_face", new[] { "面色词" }, DiagnSignChannel.FaceColor),
                Row("s_voice", new[] { "语声词" }, DiagnSignChannel.Voice),
            }, slot: 0);

            var (driver, panel) = BootDriver("Casebook", source);
            var root = panel;

            var face = root.Q<VisualElement>("channel-complexion");
            var voice = root.Q<VisualElement>("channel-voice");
            Assert.IsNotNull(face, "面色行存在");
            Assert.IsNotNull(voice, "语声行存在");

            var faceReading = face.Q<VisualElement>("reading-channel-complexion");
            Assert.IsNotNull(faceReading, "面色行应出现读数格(T2.1 判据本体)");
            Assert.IsTrue(faceReading.ClassListContains("channel-reading"),
                "阳性词条走 `.channel-reading`(ink-fg 墨字,AC-024-2)");
            Assert.AreEqual("面色词", faceReading.Q<Label>()?.text,
                "读数文本来自绑定的 fixture 词条(42 零自定义映射)");

            var voiceReading = voice.Q<VisualElement>("reading-channel-voice");
            Assert.IsNotNull(voiceReading, "语声行应出现读数格");
            Assert.AreEqual("语声词", voiceReading.Q<Label>()?.text);
        }

        [Test]
        public void test_t2_1_negativePolarity_usesNegativeFormClass()
        {
            // 阴性词 ⇒ `.channel-reading-negative`(ink-faded 降档,`:1185` 把握不足形态)
            var source = new FakeCasebookSource(GateOpen, new[]
            {
                Row("s_neg", new[] { "无异常词" }, DiagnSignChannel.Touch, SignPolarity.Negative),
            }, slot: 0);

            var (_, panel) = BootDriver("Casebook", source);
            var reading = panel.Q<VisualElement>("reading-channel-palpation");
            Assert.IsNotNull(reading, "触感行应有读数格");
            Assert.IsTrue(reading.ClassListContains("channel-reading-negative"),
                "阴性词条走阴性形态类(墨色降档,零徽章)");
            Assert.IsFalse(reading.ClassListContains("channel-reading"),
                "阴性 ≠ 阳性类(两类不可同时挂 —— 形态二选一)");
        }

        [Test]
        public void test_t2_1_unreadableWord_nullReading_negativeForm()
        {
            // F-8.2 全空档 ⇒ Word = null ⇒ 阴性形态(读不出不是阳性,:1005)
            var source = new FakeCasebookSource(GateOpen, new[]
            {
                Row("s_null", new string[] { null, null, null }, DiagnSignChannel.Posture),
            }, slot: 0);

            var (_, panel) = BootDriver("Casebook", source);
            var reading = panel.Q<VisualElement>("reading-channel-posture");
            Assert.IsNotNull(reading, "读不出 ⇒ 仍占一格(有词条只是取不到词)");
            Assert.IsTrue(reading.ClassListContains("channel-reading-negative"),
                "读不出 = 阴性形态(:1005,不是阳性)");
            Assert.AreEqual(string.Empty, reading.Q<Label>()?.text,
                "读不出 ⇒ 空文本(零「读不出」字样 / 零占位,`:1185`)");
        }

        [Test]
        public void test_t2_1_multipleWordsInSameChannel_parallelOrderPreserved()
        {
            // :1183 同通道多词条并列不相斥,按输入序
            var source = new FakeCasebookSource(GateOpen, new[]
            {
                Row("s_a", new[] { "甲词" }, DiagnSignChannel.FaceColor),
                Row("s_b", new[] { "乙词" }, DiagnSignChannel.FaceColor),
            }, slot: 0);

            var (_, panel) = BootDriver("Casebook", source);
            var face = panel.Q<VisualElement>("channel-complexion");
            var texts = face.Query<VisualElement>("reading-channel-complexion").ToList()
                .Select(r => r.Q<Label>()?.text).ToArray();
            CollectionAssert.AreEqual(new[] { "甲词", "乙词" }, texts,
                "同通道多词并列保序(:1183)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 空行铁律 + 门语义(零词条 / 门关 ⇒ 零读数格,不造未查徽章)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_t2_1_zeroRevealedRows_zeroReadingCells()
        {
            var source = new FakeCasebookSource(GateOpen, Array.Empty<SignLexemeRow>(), 0);
            var (_, panel) = BootDriver("Casebook", source);

            Assert.AreEqual(0, panel.Query<VisualElement>(null).ToList()
                .Count(e => (e.name ?? string.Empty).StartsWith("reading-")),
                "零词条 ⇒ 零读数格(未查 = 空行,`:329` 非 badge;42 不造占位)");

            // 行仍在(AC-8-23 永不隐藏),只剩行头 ⇒ 「我还没做」由空行本身说
            var face = panel.Q<VisualElement>("channel-complexion");
            Assert.IsNotNull(face, "空行 ≠ 删行(行仍在)");
            Assert.AreEqual(1, face.Children().Count(),
                "空行只剩行头 Label(零灰字 / 零「未查」标签 / 零占位)");
        }

        [Test]
        public void test_t2_1_gateClosed_vitalsSuppressesRevealedRows()
        {
            // SignCount == 0(tripwire 现形)⇒ 即便源里带 rows,42 也不挂
            // (体征数是门:0 时全行走空行;9 侧接线产出真词条后门自动开)
            var source = new FakeCasebookSource(GateClosed, new[]
            {
                Row("s_face", new[] { "不该出现的词" }, DiagnSignChannel.FaceColor),
            }, 0);

            var (_, panel) = BootDriver("Casebook", source);
            Assert.AreEqual(0, panel.Query<VisualElement>(null).ToList()
                .Count(e => (e.name ?? string.Empty).StartsWith("reading-")),
                "门关(SignCount=0)⇒ 零读数格 —— 拒绝把未接线真值画成已读状态");
        }

        [Test]
        public void test_t2_1_noSource_injected_staysEmpty_noFakeData()
        {
            var (_, panel) = BootDriver("Casebook", null);
            Assert.AreEqual(0, panel.Query<VisualElement>(null).ToList()
                .Count(e => (e.name ?? string.Empty).StartsWith("reading-")),
                "无源 ⇒ 空行(禁造假数据:不编造任何读数 / 阳性词)");
            var face = panel.Q<VisualElement>("channel-complexion");
            Assert.AreEqual(1, face.Children().Count(), "无源 ⇒ 行头 only");
        }

        // ══════════════════════════════════════════════════════════════════
        // T2.5 墨龄(story-022 形态件② · 规则十三):湿墨 → 干墨状态类
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_t2_5_freshInk_getsWetClass()
        {
            // 墨龄 0(刚落笔)⇒ `.ink-wet`(art-bible §4.5 湿墨边缘洇开)
            var source = new FakeCasebookSource(GateOpen, new[]
            {
                Row("s_face", new[] { "刚写的词" }, DiagnSignChannel.FaceColor),
            }, 0, inkAgeTicks: 0L);

            var (_, panel) = BootDriver("Casebook", source);
            var reading = panel.Q<VisualElement>("reading-channel-complexion");
            Assert.IsNotNull(reading, "读数格应存在");
            Assert.IsTrue(reading.ClassListContains("ink-wet"),
                "墨龄 0 ⇒ `.ink-wet`(刚落笔 = 湿墨)");
            Assert.IsFalse(reading.ClassListContains("ink-dry"),
                "湿态不得同时挂干类(两态互斥 —— 一值一态)");
        }

        [Test]
        public void test_t2_5_agedInk_getsDryClass()
        {
            // 墨龄超阈 ⇒ `.ink-dry`(干墨边缘收干、色沉 —— 只走明度轴)
            var source = new FakeCasebookSource(GateOpen, new[]
            {
                Row("s_face", new[] { "旧词" }, DiagnSignChannel.FaceColor),
            }, 0, inkAgeTicks: 100000L);

            var (_, panel) = BootDriver("Casebook", source);
            var reading = panel.Q<VisualElement>("reading-channel-complexion");
            Assert.IsTrue(reading.ClassListContains("ink-dry"),
                "墨龄超阈 ⇒ `.ink-dry`(干墨色沉)");
            Assert.IsFalse(reading.ClassListContains("ink-wet"),
                "干态不得同时挂湿类");
        }

        [Test]
        public void test_t2_5_inkStateClass_stacksWithPolarityClass()
        {
            // 阴性 + 干墨:两维可叠加(极性形态 × 墨龄状态 = 两个独立维)
            // ⚠️ 加载序冲突已登记(story-021 边界⑥):本测只钉「两个类都在」,
            //    颜色胜负归 stylesheet 加载序裁决项,不断言最终色。
            var source = new FakeCasebookSource(GateOpen, new[]
            {
                Row("s_neg", new[] { "旧阴性词" }, DiagnSignChannel.Touch, SignPolarity.Negative),
            }, 0, inkAgeTicks: 100000L);

            var (_, panel) = BootDriver("Casebook", source);
            var reading = panel.Q<VisualElement>("reading-channel-palpation");
            Assert.IsTrue(reading.ClassListContains("channel-reading-negative"),
                "阴性形态类在(墨色降档)");
            Assert.IsTrue(reading.ClassListContains("ink-dry"),
                "干墨状态类在(两维正交,可叠加)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 形状纪律(AC-024-2 两类全 var · 本测试只钉 42 侧挂类正确)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_t2_1_readingClasses_declaredInUss()        {
            string uss = File.ReadAllText(Path.Combine(
                Application.dataPath, "Gameplay.UI", "Skeuomorphic", "SkeuoPaper.uss"));
            string body = Regex.Replace(uss, @"/\*.*?\*/", "", RegexOptions.Singleline);
            body = Regex.Replace(body, @"//[^\r\n]*", "");

            Assert.IsTrue(Regex.IsMatch(body, @"\.channel-reading\s*\{"),
                "`.channel-reading` 须在 USS 声明(story-024 AC-024-2 已落)");
            Assert.IsTrue(Regex.IsMatch(body, @"\.channel-reading-negative\s*\{"),
                "`.channel-reading-negative` 须在 USS 声明");
        }

        [Test]
        public void test_t2_1_tickEdgeRefresh_rereadsSource_noStaleCells()
        {
            var source = new FakeCasebookSource(GateOpen, new[]
            {
                Row("s_face", new[] { "旧词" }, DiagnSignChannel.FaceColor),
            }, 0);
            var (driver, panel) = BootDriver("Casebook", source);

            Assert.AreEqual("旧词",
                panel.Q<VisualElement>("reading-channel-complexion")?.Q<Label>()?.text,
                "注入后即读一次(不给「上一帧空白」)");

            // 源内容变化 ⇒ 下一 tick 边沿重读(零缓存副本 = 零第二真相)
            source.SetRows(new[] { Row("s_voice", new[] { "新词" }, DiagnSignChannel.Voice) });
            driver.OnTickEdge();

            var faceReading = panel.Q<VisualElement>("reading-channel-complexion");
            Assert.IsNull(faceReading, "旧行读数格须被清掉(重读 ≠ 叠加)");
            Assert.AreEqual("新词",
                panel.Q<VisualElement>("reading-channel-voice")?.Q<Label>()?.text,
                "新词条进语声行(重读源,不缓存副本)");
        }

        // ══════════════════════════════════════════════════════════════════
        // helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>建宿主 + driver;返回 (driver, panel)。</summary>
        private (SkeuoRuntimeDriver driver, VisualElement panel) BootDriver(
            string screenName, ICasebookDtoSource source)
        {
            _host = new GameObject("SkeuoDriverHost_" + screenName);
            var driver = _host.AddComponent<SkeuoRuntimeDriver>();

            // 经反射注入私有序列化字段(与生产场景 / Prefab 的赋值路径等价)
            SetField(driver, "_screenName", screenName);

            // 先建 panel(uidocument 自建;panelSettings 空 = 仅逻辑树)
            var awake = typeof(SkeuoRuntimeDriver).GetMethod("Awake",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            awake.Invoke(driver, null);

            if (source != null)
                driver.Source = source;

            var panelField = typeof(SkeuoRuntimeDriver).GetField("_panel",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            var panel = (VisualElement)panelField.GetValue(driver);
            Assert.IsNotNull(panel, "driver 应已建 panel(UIDocument 自建路径)");
            return (driver, panel);
        }

        private static void SetField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"字段 {field} 应存在");
            f.SetValue(target, value);
        }
    }
}
