// sprint-05 T2.4 · story-023 形态件③ 运行时载体验证(2026-10-10)
//
// 判据(可证伪):
//   ① 候选列表**零候选**也实例化,且**恒带跳过入口**(可跳过是结构承诺)
//   ② 跳过入口用 `.skip-entry` 类(规则六之甲:跳过 = 列表上的一项)
//   ③ 跳过入口在**最后**(不选也是一种选择,放在末尾)
//   ④ AB-3 宽半:列表项宽 ≥44(`.candidate-entry` 挂 min-width var)——
//      story-023-3 只锁高半,宽半由 T2.4 的载体承接(见 USS 头注)
//   ⑤ 零数字:列表项文本零 `content:` / `X/N`(G2 反数值化结构面)
//   ⑥ 可见性门存在但不自判时序(Idle 态可达归 10 的状态机)
//
// ⚠️ 门式读仓例外:判据对象 = 仓库内版本化 USS / UXSD 文件,不依赖网络 / 数据库 /
//    可变外部状态(承 story-021/022/023/024 测试先例)。

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>story-023 跳过入口 / 候选列表运行时载体验证(sprint-05 T2.4)。</summary>
    public class emergency_candidate_list_test
    {
        private const string UssFileName = "SkeuoPaper.uss";

        private static string UssBody
        {
            get
            {
                string path = Path.Combine(Application.dataPath, "Gameplay.UI", "Skeuomorphic", UssFileName);
                Assert.IsTrue(File.Exists(path), $"前置失败:{path} 不存在");
                string body = Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", "",
                                            RegexOptions.Singleline);
                return Regex.Replace(body, @"//[^\r\n]*", "");
            }
        }

        // ── ① 载体实例化:零候选也建,跳过入口恒在 ──────────────────────

        [Test]
        public void test_t2_4_zeroCandidates_stillInstantiates_withSkipEntry()
        {
            var list = new EmergencyCandidateList();
            list.Build(System.Array.Empty<string>());

            Assert.IsNotNull(list.Root, "列表容器应实例化");
            Assert.IsTrue(list.HasSkipEntry,
                "零候选 ≠ 零列表 —— 可跳过是结构承诺,不随候选数消失");
            Assert.AreEqual(0, list.EntryCount, "零候选");
        }

        [Test]
        public void test_t2_4_candidates_renderedInOrder()
        {
            var list = new EmergencyCandidateList();
            list.Build(new[] { "胸外按压", "开放气道", "人工呼吸" });

            Assert.AreEqual(3, list.EntryCount);
            var labels = list.Root.Children()
                .Where(e => e.name.StartsWith("candidate-"))
                .Select(e => e.Q<Label>()?.text).ToArray();
            CollectionAssert.AreEqual(new[] { "胸外按压", "开放气道", "人工呼吸" }, labels,
                "候选项按喂入序渲染(42 零自定义排序 —— 动作集归 10)");
        }

        // ── ②③ 跳过入口形态:类 + 位置 ──────────────────────────────────

        [Test]
        public void test_t2_4_skipEntry_usesSkipEntryClass_atTail()
        {
            var list = new EmergencyCandidateList();
            list.Build(new[] { "胸外按压" });

            var skip = list.Root.Q<VisualElement>(EmergencyCandidateList.SkipEntryName);
            Assert.IsNotNull(skip, "跳过入口应实例化");
            Assert.IsTrue(skip.ClassListContains("skip-entry"),
                "跳过入口挂 `.skip-entry`(story-023 AC-023-1 纸签形态;O-10-1 owner = 42)");

            // 规则六之甲:跳过 = 列表上的一项 ⇒ 它在列表内,且是最后一项
            var children = list.Root.Children().ToList();
            Assert.AreEqual(EmergencyCandidateList.SkipEntryName,
                children[children.Count - 1].name,
                "跳过入口在列表末尾(「不选也是一种选择」)"
                + "—— 不在末尾 = 变成列表外的旁路按钮,违规则六之甲");
        }

        [Test]
        public void test_t2_4_skipEntry_hasText_zeroNumericBadge()
        {
            var list = new EmergencyCandidateList();
            list.Build(new[] { "胸外按压" });

            var skip = list.Root.Q<VisualElement>(EmergencyCandidateList.SkipEntryName);
            string text = skip.Q<Label>()?.text ?? string.Empty;

            Assert.IsFalse(text.Any(char.IsDigit),
                $"跳过入口零数字(G2 反数值化)—— 命中「{text}」");
            Assert.IsNotEmpty(text, "跳过入口有可读文字(不是空图标)");
        }

        // ── ④ AB-3 宽半(`.candidate-entry` min-width 走 var) ────────────

        [Test]
        public void test_t2_4_candidateEntry_minWidth_bindsRowHeightVar()
        {
            var list = new EmergencyCandidateList();
            list.Build(new[] { "胸外按压" });

            var entry = list.Root.Q<VisualElement>("candidate-0");
            Assert.IsNotNull(entry, "候选项应实例化");
            Assert.IsTrue(entry.ClassListContains("candidate-entry"),
                "候选项挂 `.candidate-entry`(宽半承接类,story-023 警示②登记)");

            // USS 侧:`.candidate-entry` 的 min-width 必须走 var(禁内联数值)
            Match block = Regex.Match(UssBody,
                Regex.Escape(".candidate-entry") + @"\s*\{[^}]*\}");
            Assert.IsTrue(block.Success, "`.candidate-entry` 须在 SkeuoPaper.uss 声明");
            Match minWidth = Regex.Match(block.Value, @"min-width\s*:\s*([^;]+);");
            Assert.IsTrue(minWidth.Success, "`.candidate-entry` 须声明 min-width(AB-3 宽半)");
            Assert.IsTrue(minWidth.Groups[1].Value.Trim().StartsWith("var(--skeuo-"),
                $"宽半须走主题变量(禁内联 px)—— 实见「{minWidth.Groups[1].Value}」");

            // 值 = `--skeuo-shared-row-height`(44 = AB-3 高半,宽半同源承诺)
            Assert.IsTrue(minWidth.Groups[1].Value.Contains("--skeuo-shared-row-height"),
                "宽半锚 `--skeuo-shared-row-height`(44px = AB-3 焦点落点承诺;承 story-021 变量)");
        }

        // ── ⑤ 全库零数字角标(与 AC-023-2 门同口径,不进本测试重复其全集) ──

        [Test]
        public void test_t2_4_uss_zeroNumericBadgeInNewBlocks()
        {
            // 只锁本 story 新增两块(`.skip-entry` / `.candidate-entry`),
            // 全库零数字门归 AC-023-2(story-023 测试),此处不重复其判据面。
            foreach (string className in new[] { ".skip-entry", ".candidate-entry" })
            {
                Match block = Regex.Match(UssBody, Regex.Escape(className) + @"\s*\{[^}]*\}");
                Assert.IsTrue(block.Success, $"{className} 块应存在");
                Assert.IsFalse(Regex.IsMatch(block.Value, @"content\s*:"),
                    $"{className} 块零 `content:`(数字角标唯一 USS 载体,G2)");
                Assert.IsFalse(Regex.IsMatch(block.Value, @"\d+\s*/\s*\d+"),
                    $"{className} 块零 `X/N` 形态(G2)");
            }
        }

        // ── ⑥ 可见性门:存在,但零时序判定 ──────────────────────────────

        [Test]
        public void test_t2_4_visibilityGate_exists_butNoTimingLogic()
        {
            var list = new EmergencyCandidateList();
            list.Build(new[] { "胸外按压" });

            list.SetVisible(false);
            Assert.IsFalse(list.IsVisible, "门可关(display: None)");
            Assert.IsTrue(list.HasSkipEntry, "关灯不拆表 —— 结构不随可见性消失");

            list.SetVisible(true);
            Assert.IsTrue(list.IsVisible, "门可开");

            // 零时序:本类无任何时钟字段(Idle 可达归 10 的状态机 —— 反射面兜底)
            var timingFields = typeof(EmergencyCandidateList)
                .GetFields(System.Reflection.BindingFlags.Instance |
                           System.Reflection.BindingFlags.NonPublic |
                           System.Reflection.BindingFlags.Public)
                .Where(f => f.FieldType == typeof(float) || f.FieldType == typeof(double) ||
                            f.FieldType == typeof(long) || f.Name.ToLowerInvariant().Contains("timer") ||
                            f.Name.ToLowerInvariant().Contains("clock"))
                .ToArray();
            Assert.IsEmpty(timingFields,
                "本类零计时字段(Idle 态可达时序归 10 —— 42 不自判何时该弹列表)");
        }

        [Test]
        public void test_t2_4_emptyLabel_failsLoud()
        {
            var list = new EmergencyCandidateList();
            // 空标签 = 隐形项 = 焦点落点不可见(破 AB-3)⇒ fail-loud
            Assert.Throws<System.ArgumentException>(
                () => list.Build(new[] { "胸外按压", "" }),
                "空标签须具名拒绝 —— 隐形焦点落点破 AB-3");
        }
    }
}
