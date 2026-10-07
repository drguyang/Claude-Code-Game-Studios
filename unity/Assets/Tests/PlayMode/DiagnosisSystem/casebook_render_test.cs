// diagnosis-system Story 006 —— 脉案渲染结构与反幻想护栏(PlayMode · **Required evidence**)。
//
// 权威来源:production/epics/diagnosis-system/story-006-casebook-presentation-routing-anti-fantasy.md
//   · GDD UI-8.1(五通道布局铁律)· UI-8.2(焦点序,2026-10-06 订正面)· UI-8.4(负向声明)
//   · OQ-8-9(问诊栏第 6 行)· OQ-CB-5(置信度不落独立控件)· ADR-013 §9 C3(42 只渲染)
//
// 覆盖(自动化静态/结构半边;[V]/[U] 截图与签核半边归
//   production/qa/evidence/diagnosis-system/story-006-walkthrough-2026-10-08.md,禁借绿):
//   · AC-8-23 五行固定成序 + 焦点序(措辞以 GDD UI-8.2 订正面为准 —— 卡文旧序差异登记卡面)
//   · AC-8-39 「?」只在病名/置信度处(绝不溢出体征栏)
//   · AC-8-43 UI-8.4 负向声明(树遍历:零进度条/计数/补全/等级提示)
//   · AC-8-52② IModalState.Modal 只读(Gameplay.UI 在本装配,EditMode 看不到)
//   · AC-8-19 泄漏像素的**结构代理**(同数据两实例逐节点序列化等价;
//     截图逐像素哈希腿 = batch 无渲染面 ⇒ NOT-RUN,归走查件)
//   · AC-8-42 / 8-36 / 8-37 / 8-38 / 8-40 / 8-41 / 8-44 等 [V] 项:走查签核位
//   · 双真源交叉(评审 S-3/q1):test_cross_uxmlSpec_buildUi_nameSequence_equivalent ——
//     UXML spec 面 ≡ BuildUI 代码面 name 序,改一面不同步另一面即红
// ⚠️ **禁词集同步(评审 q9/q15)**:ForbiddenUiTokens 与 EditMode casebook_lexicon_test
//    的同名集合是同一集合的两面(运行树 ↔ UXML 属性/文本),改任一侧须同步对侧。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.DiagnosisSystem
{
    internal sealed class CasebookRenderTest
    {
        /// <summary>UI-8.2 焦点序(GDD 2026-10-06 订正面;树序 = 焦点序)。</summary>
        private static readonly string[] ExpectedFocusOrder =
        {
            "channel-complexion",  // 面色
            "channel-voice",       // 语声
            "channel-posture",     // 姿态
            "channel-breathing",   // 呼吸
            "channel-palpation",   // 触感
            "channel-inquiry",     // 问诊栏(OQ-8-9 第 6 行)
            "channel-disease-name",// 病名(置信度在其中循环,OQ-CB-5 K=6)
        };

        /// <summary>五行(体征区;问诊栏单列,病名归右栏)。</summary>
        private static readonly string[] FiveChannelRows =
        {
            "channel-complexion", "channel-voice", "channel-posture",
            "channel-breathing", "channel-palpation",
        };

        private static (CasebookScreen screen, VisualElement root) Build()
        {
            var root = new VisualElement();
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            return (screen, root);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-23:五行固定成序 + 问诊栏 + 病名归右栏;永不隐藏(display ≠ None)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_23_fiveRowsPlusInquiry_fixedOrder_neverHidden()
        {
            var (_, root) = Build();

            // 五行在 five-channels 内,成序、齐全
            var channels = root.Q<VisualElement>("five-channels");
            Assert.IsNotNull(channels, "five-channels 存在");
            CollectionAssert.AreEqual(
                FiveChannelRows.Concat(new[] { "channel-inquiry" }).ToArray(),
                channels.Children().Select(c => c.name).ToArray(),
                "五行 + 问诊栏固定成序(UI-8.1/OQ-8-9 初始树序);" +
                "「不因已查集合重排」的数据喂入场景 = NOT-RUN(39 接线前无数据 API," +
                "归走查件截图半边)");

            // 五行永不隐藏:display ≠ None(未显式设置 ⇒ 默认 Flex)
            foreach (string rowName in FiveChannelRows)
            {
                var row = root.Q<VisualElement>(rowName);
                Assert.IsNotNull(row, $"{rowName} 存在(永不留空 = 行在)");
                Assert.AreNotEqual(DisplayStyle.None, row.style.display.value,
                    $"{rowName} 永不隐藏(AC-8-23)");
            }

            // 问诊栏第 6 行同纪律
            var inquiry = root.Q<VisualElement>("channel-inquiry");
            Assert.IsNotNull(inquiry, "问诊栏存在");
            Assert.AreNotEqual(DisplayStyle.None, inquiry.style.display.value,
                "问诊栏永不隐藏(OQ-8-9 同格线纪律)");

            // 病名归右栏(UI-8.1 铁律③:与体征物理分离),不在 five-channels 内
            var right = root.Q<VisualElement>("right-column");
            Assert.IsNotNull(right, "right-column 存在");
            Assert.IsNotNull(root.Q<VisualElement>("channel-disease-name"),
                "病名行存在");
            Assert.IsNull(FindUnder(channels, "channel-disease-name"),
                "病名不在五通道区内(归属不同栏,AC-8-23/UI-8.1)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-23 焦点序:面色 → 语声 → 姿态 → 呼吸 → 触感 → 问诊栏 → 病名
        // (GDD UI-8.2 2026-10-06 订正面 —— 卡文旧序「…→ 病名 → 置信度」差异登记卡面)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_23_focusOrder_matchesGddUi82_noConfidenceStop()
        {
            var (_, root) = Build();

            var focusables = root.Query().Where(e => e.focusable).ToList()
                .Select(e => e.name).ToArray();

            CollectionAssert.AreEqual(ExpectedFocusOrder, focusables,
                "焦点序 = UI-8.2 订正面(树序即焦点序;置信度不落独立控件)");

            // 置信度标记不聚焦(OQ-CB-5 K=6:病名焦点内循环确定/疑似)
            var confidence = root.Q<VisualElement>("confidence-mark");
            Assert.IsNotNull(confidence, "confidence-mark 存在(右栏)");
            Assert.IsFalse(confidence.focusable, "置信度零焦点位");

            // 病名可聚焦(循环载体)
            Assert.IsTrue(root.Q<VisualElement>("channel-disease-name").focusable,
                "病名可聚焦(K=6 循环落点)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-23 空行纪律:五行只有行头,零占位文字(空行 = 显式「我还没做」)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_23_emptyRows_headerOnly_zeroPlaceholderText()
        {
            var (_, root) = Build();
            string[] headers = { "面色", "语声", "姿态", "呼吸", "触感", "问诊" };

            var channels = root.Q<VisualElement>("five-channels");
            int i = 0;
            foreach (var row in channels.Children())
            {
                var texts = row.Query<Label>().ToList().Select(l => l.text).ToArray();
                CollectionAssert.AreEqual(new[] { headers[i] }, texts,
                    $"行 {row.name} 只有行头「{headers[i]}」—— 读数格留空由 39 喂值," +
                    "零灰字/零「未查」占位/零计数");
                i++;
            }
            Assert.AreEqual(headers.Length, i, "六行全走查");

            // 右栏文本面 ⊆ {病名, ?}(零裸数字 badge —— 评审 q16,AC-8-39/43)
            var rightTexts = root.Q<VisualElement>("right-column")
                .Query<Label>().ToList().Select(l => l.text ?? "");
            foreach (string text in rightTexts)
            {
                Assert.IsFalse(text.Any(char.IsDigit),
                    $"右栏零裸数字 badge(AC-8-43)—— 命中「{text}」");
                Assert.IsTrue(text == "病名" || text.Contains("?"),
                    $"右栏文本面 ⊆ {{病名, ?}} —— 命中「{text}」");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-39:「?」只在病名/置信度处,绝不溢出体征栏
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_39_questionMarkOnlyInConfidenceArea()
        {
            var (_, root) = Build();

            // TextElement 全载体(评审 q14:Label 之外的文本载体不漏判)
            var questionTexts = root.Query().ToList().OfType<TextElement>()
                .Where(te => te.text != null && te.text.Contains("?")).ToList();
            Assert.AreEqual(1, questionTexts.Count, "「?」全屏恰一次");

            // 祖先链:必经右栏(病名/置信度区 —— GDD「只在病名栏」放宽到右栏),
            // 必不经 five-channels(不进体征栏,AC-8-39)
            bool inRight = false, inVitals = false;
            VisualElement cur = questionTexts[0];
            while (cur != null)
            {
                if (cur.name == "right-column") inRight = true;
                if (cur.name == "five-channels") inVitals = true;
                cur = cur.parent;
            }
            Assert.IsTrue(inRight, "「?」在病名/置信度栏(右栏)");
            Assert.IsFalse(inVitals, "「?」不进体征栏(AC-8-39)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-43:UI-8.4 负向声明(树遍历 —— 自动化树遍历 + 走查双签的树遍历半边)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_43_negativeDeclarations_treeWalk_allAbsent()
        {
            var (_, root) = Build();

            // 禁元件类型 / 禁名与 class token / 禁文本 token
            // (文本集 = ForbiddenUiTokens —— 与 EditMode casebook_lexicon_test 同集合,
            //  评审 q9/q15 要求的单源同步:改此处须同步对侧)
            string[] forbiddenTypeTokens = { "ProgressBar", "Slider", "Gauge" };
            string[] forbiddenNameTokens =
                { "progress", "percent", "count", "completion", "badge",
                  "autofill", "hint", "tooltip", "meter", "level", "read_floor" };
            string[] forbiddenTextTokens = ForbiddenUiTokens
                .Concat(new[] { "等级", "Lv" }).ToArray();

            var violations = new List<string>();
            foreach (var e in root.Query().ToList())
            {
                string typeName = e.GetType().Name;
                foreach (string t in forbiddenTypeTokens)
                    if (typeName.Contains(t))
                        violations.Add($"type:{typeName}");

                foreach (string t in forbiddenNameTokens)
                    if ((e.name ?? string.Empty).ToLowerInvariant().Contains(t))
                        violations.Add($"name:{e.name}");

                if (e is Label l && l.text != null)
                {
                    foreach (string t in forbiddenTextTokens)
                        if (l.text.Contains(t))
                            violations.Add($"text:{l.text}");
                }
            }
            Assert.IsEmpty(violations,
                "UI-8.4 负向声明:零进度条/计数/补全/等级提示(AC-8-43)—— " +
                $"命中:{string.Join("; ", violations)}");

            // 无进度类样式 class(遍历 classList)
            foreach (var e in root.Query().ToList())
            {
                foreach (string cls in e.GetClasses())
                {
                    Assert.IsFalse(cls.ToLowerInvariant().Contains("progress"),
                        $"零 progress class({e.name}.{cls})");
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-52②:IModalState.Modal 只读 + 脉案 = Casebook(Gameplay.UI 在本装配)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_52_2_imodalState_readonly_casebookIdentity()
        {
            var prop = typeof(IModalState).GetProperty(nameof(IModalState.Modal));
            Assert.IsNotNull(prop, "IModalState.Modal 存在");
            Assert.IsTrue(prop.CanRead, "可读");
            Assert.IsFalse(prop.CanWrite, "只读 —— 42 永不写模态(裁决输入只读已落盘态)");

            var (screen, _) = Build();
            Assert.AreEqual(ModalId.Casebook, screen.Modal, "脉案页 Modal = Casebook");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 构建确定性冒烟(评审 q10 降格):同构两实例序列化等价 + 变异敏感性。
        //   ⚠️ **不是 AC-8-19 的落点** —— AC-8-19 像素级不可区分 = [V] 截图腿,
        //      batch 无渲染面 ⇒ NOT-RUN,归走查件签核(禁以本测借绿)。
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_smoke_buildDeterminism_twoInstances_identicalTreeSerialization()
        {
            var (_, rootA) = Build();
            var (_, rootB) = Build();

            string serA = Serialize(rootA);
            string serB = Serialize(rootB);
            Assert.IsNotEmpty(serA, "序列化非空(机器活性)");
            Assert.AreEqual(serA, serB,
                "同输入两实例逐节点序列化等价(构建确定性冒烟 —— " +
                "AC-8-19 像素腿 NOT-RUN 归走查件,本测不承担其落点)");

            // 影子敏感性:改一个名字 ⇒ 序列化必不同(判别力自证)
            rootB.Q<VisualElement>("channel-voice").name = "channel-voice-X";
            Assert.AreNotEqual(serA, Serialize(rootB),
                "结构变更 ⇒ 序列化变化(等价断言非恒真)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 双真源交叉断言(评审 S-3/q1):UXML spec 面 ≡ BuildUI 代码面(name 序等价)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_cross_uxmlSpec_buildUi_nameSequence_equivalent()
        {
            // UXML 面(PlayMode 可达:Application.dataPath = <repo>/unity/Assets)
            string path = Path.Combine(Application.dataPath,
                "Gameplay.UI", "Skeuomorphic", "Screens", "Casebook39.uxml");
            Assert.IsTrue(File.Exists(path), $"UXML spec 面存在:{path}");
            XDocument doc = XDocument.Parse(File.ReadAllText(path));
            string[] uxmlNamed = doc.Descendants()
                .Where(e => e.Attribute("name") != null)
                .Select(e => (string)e.Attribute("name"))
                .ToArray();

            // BuildUI 代码面(root = 挂载点,命名与 UXML 根对齐)
            var root = new VisualElement { name = "casebook-root" };
            _ = new CasebookScreen(root, new SkeuoElementLibrary());
            var treeNamed = new List<string>();
            CollectNamed(root, treeNamed);

            Assert.IsNotEmpty(uxmlNamed, "UXML 具名元素非空(机器活性)");
            CollectionAssert.AreEqual(uxmlNamed, treeNamed,
                "UXML spec 面 ≡ BuildUI 代码面(有 name 元素按文档序/先序)—— " +
                "改一面不同步另一面即红(双真源守护,评审 S-3/q1)");
        }

        private static void CollectNamed(VisualElement e, List<string> acc)
        {
            if (!string.IsNullOrEmpty(e.name))
                acc.Add(e.name);
            foreach (var c in e.Children())
                CollectNamed(c, acc);
        }

        /// <summary>
        /// UI-8.4 / AC-8-43 负向禁词集(**与 EditMode casebook_lexicon_test 同集合的
        /// 运行树面** —— 两处头注互指,改一处须同步另一处;评审 q9/q15)。
        /// </summary>
        private static readonly string[] ForbiddenUiTokens =
        {
            "ProgressBar", "progress-bar", "进度", "完成度", "未查", "已查",
            "X/5", "百分", "%", "图鉴", "词条", "已发现", "对错",
            "精度", "READ_FLOOR", "技能等级", "补全",
        };

        // ══════════════════════════════════════════════════════════════════════════
        // helpers
        // ══════════════════════════════════════════════════════════════════════════

        private static VisualElement FindUnder(VisualElement scope, string name)
            => scope.Query<VisualElement>(name).ToList().FirstOrDefault();

        /// <summary>深度优先序列化:name + class + 文本(结构等价判据)。</summary>
        private static string Serialize(VisualElement root)
        {
            var sb = new System.Text.StringBuilder();
            Walk(root);
            return sb.ToString();

            void Walk(VisualElement e)
            {
                sb.Append(e.name).Append('[')
                  .Append(string.Join(",", e.GetClasses().OrderBy(c => c)));
                if (e is Label l)
                    sb.Append('|').Append(l.text);
                sb.Append("]\n");
                foreach (var c in e.Children())
                    Walk(c);
            }
        }
    }
}
