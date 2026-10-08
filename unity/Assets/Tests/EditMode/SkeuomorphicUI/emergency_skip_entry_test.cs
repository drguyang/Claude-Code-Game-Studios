using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    /// <summary>M2 形态件③(story-023 · AC-023-1/2/3)—— 急救零数字 + 可跳过。
    /// <para>判据权威 = art-bible <b>G2</b>(「反数值化」是否真的成立 —— 灰盒「色块+数字」
    /// 自证反数值化不需要做,恰恰绕开要证的东西)+ emergency-procedures 规则六 /
    /// 规则六之甲(跳过入口 = 候选动作列表上的一项,owner = 42,O-10-1)。</para>
    /// <para>⚠️ 门式读仓例外声明(照 texture_binding_gate / story-021/022 先例):
    /// 判据对象 = USS 文件本体,必须读仓;不依赖网络 / 数据库 / 可变外部状态;
    /// 文件系统例外面 = 仓库内版本化只读文件。</para></summary>
    [Category("SkeuomorphicUI")]
    public class emergency_skip_entry_test
    {
        private static string RepoRoot =>
            TextureBindingGates.DefaultRepoRoot;

        private static string SkeuoDir =>
            Path.Combine(RepoRoot, "Assets", "Gameplay.UI", "Skeuomorphic");

        /// <summary>剥 USS 注释(官方仅块注释;行注释防御性同剥 —— 022 同款)。</summary>
        private static string StripComments(string text)
        {
            var body = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return Regex.Replace(body, @"//[^\r\n]*", "");
        }

        private static string ReadUss(string fileName)
        {
            string path = Path.Combine(SkeuoDir, fileName);
            Assert.IsTrue(File.Exists(path), $"前置失败:{path} 不存在。");
            return File.ReadAllText(path);
        }

        /// <summary>提取单类声明块(剥注释后)。</summary>
        private static Match BlockOf(string strippedBody, string className) =>
            Regex.Match(strippedBody, Regex.Escape(className) + @"\s*\{[^}]*\}");

        /// <summary>块内属性名集(去空白)。
        /// <para>⚠️ 行首锚 + Multiline(评审修复):原 `([\w-]+)\s*:` 会把值内冒号计成
        /// 属性(如 `url("guid:…")` 的 `guid` 幻影)—— 交集基被污染,两块同带 url 即假红。</para></summary>
        private static HashSet<string> PropsOf(string block) =>
            new HashSet<string>(Regex.Matches(block, @"^\s*([\w-]+)\s*:",
                    RegexOptions.Multiline)
                .Cast<Match>().Select(m => m.Groups[1].Value));

        // ── AC-023-1: 跳过入口元件形态(主题变量纪律 + O-10-1 规格头注)──

        [Test]
        public void test_ac023_1_skip_entry_element_declared_with_theme_discipline()
        {
            // Arrange:Raw(头注锚用)+ 剥注释(块提取用)
            string raw = ReadUss("SkeuoPaper.uss");
            string body = StripComments(raw);

            // Act
            var block = BlockOf(body, ".skip-entry");

            // Assert ① 类块存在
            Assert.IsTrue(block.Success,
                "缺 `.skip-entry` 声明块 —— 跳过入口元件形态(owner = 42,O-10-1)未落。");

            // Assert ② 值全走 theme(评审 BLOCKING 修复 —— 原「≥5 计数」三个假绿分支:
            //    删 background-color 行(5≥5 绿)/ `color: black` 具名色(C4 不拦)/
            //    异命名空间 var(--paper-bg)(未定义仅运行期透明)。
            //    现口径:① 属性集 ⊇ 点名六属性(删行即红)② 逐声明值须含 `var(--skeuo-`
            //    (具名色/裸数字/异命名空间/内联 rgb 全灭 —— 一个前缀断言打多分支)。
            //    未来若须字面关键字值(如 display),在 ValueWhiteList 显式登记再放行。)
            string decl = block.Value;
            Assert.IsFalse(Regex.IsMatch(decl, @"#[0-9a-fA-F]{3,8}\b"),
                ".skip-entry 块内禁内联 hex(C2 门)。");
            var declProps = PropsOf(decl);
            var required = new[] { "background-color", "color", "min-height",
                "padding-left", "font-family", "font-size" };
            foreach (var r in required)
                Assert.IsTrue(declProps.Contains(r),
                    $".skip-entry 须声明 `{r}`(story AC 点名载体;删/改名即红)。");
            var valueWhiteList = new HashSet<string>(); // 空 = 全声明须走 var(--skeuo-*);字面值须先入此白名单
            var decls = Regex.Matches(decl, @"([\w-]+)\s*:\s*([^;]+);")
                .Cast<Match>().ToList();
            foreach (var d in decls)
            {
                string prop = d.Groups[1].Value, value = d.Groups[2].Value.Trim();
                if (valueWhiteList.Contains(prop)) continue;
                Assert.IsTrue(value.Contains("var(--skeuo-"),
                    $".skip-entry `{prop}: {value}` 须走 var(--skeuo-*) —— " +
                    "具名色 / 裸数字 / 异命名空间 var(未定义,运行期静默失效)均在此失守。");
            }

            // Assert ③ 头注规格锚(评审修复:钉**紧邻选择器**的注释组 ——
            //    原 `/\*.*?跳过入口.*?\*/` 从全文件首个 /* 起圈,巨型跨度靠巧合收得紧)
            var segMatch = Regex.Match(raw,
                @"/\*(?:[^*]|\*(?!/))*\*/\s*\.skip-entry\s*\{", RegexOptions.Singleline);
            Assert.IsTrue(segMatch.Success,
                "`.skip-entry` 块前无紧邻头注(规格锚无处安放)。");
            string headerSeg = segMatch.Value;
            StringAssert.Contains("O-10-1", headerSeg,
                "头注须锚 `O-10-1`(10 向 42 提规格的外向义务)。");
            StringAssert.Contains("owner", headerSeg,
                "头注须锚 `owner`(跳过入口 owner = 42 —— ADR-013 §三 归属)。");
            StringAssert.Contains("10 实现轮", headerSeg,
                "头注须声明零施加点(载体/Idle 时序归 10 实现轮)—— 否则冒充已交付实现面。");

            // Assert ④ 边界裁定④(Registry 零注册守卫 —— 照 story-022 先例:
            //    误注册静默,16→5 槽不触 C3、非 texture 容器不触 C7,全绿不可见)
            string registryPath = Path.Combine(SkeuoDir, "SkeuoComponentRegistry.cs");
            Assert.IsTrue(File.Exists(registryPath), $"前置失败:{registryPath} 不存在。");
            Assert.IsFalse(Regex.IsMatch(File.ReadAllText(registryPath),
                    @"""skip-entry"""),
                "SkeuoComponentRegistry 不得注册 skip-entry —— 语义样式类不进 Registry" +
                "(边界裁定④,同 .ruled 先例)。");

            // Assert ⑤ 全库声明块恰一(评审修复:第二处 `.skip-entry {` 复活面 ——
            //    首配断言对后置覆盖块全盲,恰是焦点环被顶的结构复发)
            var allBlocks = Regex.Matches(
                StripComments(string.Join("\n",
                    Directory.GetFiles(SkeuoDir, "*.uss", SearchOption.AllDirectories)
                        .Select(File.ReadAllText))),
                @"\.skip-entry\s*\{[^}]*\}");
            Assert.AreEqual(1, allBlocks.Count,
                $"`.skip-entry` 声明块须全库恰 1(实见 {allBlocks.Count})—— " +
                "第二处 = 后置覆盖复活(焦点冲突/内联色可藏于此)。");
        }

        // ── AC-023-2: 零数字角标门(art-bible G2 反数值化机械化)──

        [Test]
        public void test_ac023_2_emergency_presentation_zero_numeric_badge()
        {
            // Arrange:全库 Skeuo USS(含子目录)剥注释后扫
            var files = Directory.GetFiles(SkeuoDir, "*.uss", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files, "前置失败:Skeuo 目录零 USS 文件。");

            // Act/Assert ①② 全库零数字角标载体(剥注释 —— 否则注释内实测记录「40/39/43/43」误红;
            //    X/N 正则须排 url(guid) 无斜杠对,实测 guid 内无「数字/数字」形态)
            //    ⚠️ 白名单口径(评审登记):未来合法 `X/N` 出现(如 aspect-ratio: 16/9 型)
            //    须**显式白名单(文件:行 + 理由)**逐处放行,禁整体放宽正则
            //    (照 022 阈值「勿直接放宽,先分解」登记法 —— 放宽 = G2 门静默失效)。
            var badgeHits = new List<string>();
            var contentHits = new List<string>();
            foreach (var f in files)
            {
                string stripped = StripComments(File.ReadAllText(f));
                string rel = f.Substring(SkeuoDir.Length).TrimStart(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (Match m in Regex.Matches(stripped, @"\d+\s*/\s*\d+"))
                    badgeHits.Add($"{rel}: `{m.Value}`");
                foreach (Match m in Regex.Matches(stripped, @"content\s*:\s*""[^""]*\d"))
                    contentHits.Add($"{rel}: `{m.Value}`");
            }
            Assert.IsEmpty(badgeHits,
                $"G2 零数字:USS 出现 `X/N` 形态(数字角标字面,AC-42-F3 禁令):\n" +
                string.Join("\n", badgeHits));
            Assert.IsEmpty(contentHits,
                $"G2 零数字:USS `content:` 携带数字(数字角标唯一 USS 载体):\n" +
                string.Join("\n", contentHits));

            // Assert ③ `.skip-entry` 块内双模式复查(单类聚焦 —— 全库扫描的类级落点)
            string body = StripComments(ReadUss("SkeuoPaper.uss"));
            var block = BlockOf(body, ".skip-entry");
            Assert.IsTrue(block.Success, "前置失败:缺 `.skip-entry` 块(AC-023-1 范围)。");
            Assert.IsFalse(Regex.IsMatch(block.Value, @"\d+\s*/\s*\d+"),
                ".skip-entry 块内禁 X/N 数字角标形态。");
            Assert.IsFalse(Regex.IsMatch(block.Value, @"content\s*:"),
                ".skip-entry 块内禁 content: 属性 —— 数字角标的唯一 USS 载体,零数字即零载体。");
        }

        // ── AC-023-3: 焦点落点形态(可跳过的可达性形态半)──

        [Test]
        public void test_ac023_3_skip_entry_focus_landing_compatible()
        {
            // Arrange
            string body = StripComments(ReadUss("SkeuoPaper.uss"));
            var block = BlockOf(body, ".skip-entry");
            Assert.IsTrue(block.Success, "前置失败:缺 `.skip-entry` 块(AC-023-1 范围)。");
            string decl = block.Value;

            // Assert ① 焦点落点尺寸承诺:min-height 锚 row-height(=44 = AB-3 ≥44×44 高半)
            Assert.IsTrue(Regex.IsMatch(decl,
                    @"min-height:\s*var\(--skeuo-shared-row-height\)"),
                ".skip-entry 须以 var(--skeuo-shared-row-height) 承载行高 —— " +
                "它是候选动作列表的焦点落点,塌陷即违 AB-3(story-021 变量基建)。");

            // Assert ② 零 background-image(焦点单槽铁律 —— 环图整槽替换顶掉纸纹;
            //    SkeuoFocusVisible.uss 头注铁律,跳过入口获焦即闪烁的结构性预防)
            Assert.IsFalse(Regex.IsMatch(decl, @"background-image\s*:"),
                ".skip-entry 禁 background-image —— 焦点单槽铁律(获焦瞬间纸纹被环图顶掉)。");

            // Assert ③ 与 .focus-visible 声明属性集零交集(动态解析两块比交集)
            string focusBody = StripComments(ReadUss("SkeuoFocusVisible.uss"));
            var focusBlock = BlockOf(focusBody, ".focus-visible");
            Assert.IsTrue(focusBlock.Success, "前置失败:缺 `.focus-visible` 块。");
            var skipProps = PropsOf(decl);
            var focusProps = PropsOf(focusBlock.Value);
            skipProps.IntersectWith(focusProps);
            Assert.IsEmpty(skipProps,
                $".skip-entry 与 .focus-visible 声明属性集相交:{{{string.Join(", ", skipProps)}}} —— " +
                "同属性双写 = 单类级联冲突(焦点环被顶)。⚠️ `.ruled` 的 border 冲突是 " +
                "story-021:52 已登记别案,本断言守本类不新增同类,非追溯旧案。");
        }
    }
}
