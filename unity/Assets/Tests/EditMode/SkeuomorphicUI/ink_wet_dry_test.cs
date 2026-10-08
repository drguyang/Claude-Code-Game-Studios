using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    /// <summary>M2 形态件② · 墨乾湿两态(story-022 · AC-022-1/2/3)。
    /// <para>判据权威 = art-bible §4.5 墨龄(湿墨边缘洇开 → 干墨收干色沉,
    /// **只走明度轴不色相漂移** —— 色盲安全)+ G4 手感本体。</para>
    /// <para>⚠️ 门式读仓(例外声明,照 texture_binding_gate / story-021 focus 测试先例):
    /// 判据对象 = USS/theme/冻结记录/贴图文件本体,必须读仓;不依赖网络 / 数据库 /
    /// 可变外部状态。测试标准「Unit tests must not depend on external state」的
    /// 文件系统例外面 = 仓库内**版本化**只读文件(story-021 同口径)。</para></summary>
    [Category("SkeuomorphicUI")]
    public class ink_wet_dry_test
    {
        private static string RepoRoot =>
            TextureBindingGates.DefaultRepoRoot;

        private static string SkeuoDir =>
            Path.Combine(RepoRoot, "Assets", "Gameplay.UI", "Skeuomorphic");

        // 冻结件 freeze-v1 登记的两图 guid(与 .meta 同源;悬空 = AC-022-2 红)
        private const string InkWetGuid = "4ccb158dd9726284480349d2a168e05b";
        private const string InkDryGuid = "fbae6e469acaf9fb2b9966cd8a276877";

        /// <summary>面积覆盖率的墨像素判别带宽(L &lt; 此值 = 墨)【提案·归数值轮】。</summary>
        private const double InkPixelLuminanceBand = 0.2;

        /// <summary>「不色相漂移」的 HSV 色相差上限(度)【提案·归数值轮】。
        /// <para>⚠️ 8bit RGB 量化噪声的结构界:同色相降明度对实测 ΔH ≈ 3–7°,
        /// 阈值须高于量化噪声否则误红;具体值归数值轮。</para>
        /// <para>⚠️ 近灰 LSB 灵敏度(评审登记):干色 delta = 5/255,单 LSB(g 23→22)
        /// 可移色相约 12° ⇒ 阈值贴噪声上沿;数值轮微调 dry 若遇本断言假红,
        /// 先做 ΔH 分解(色相真漂 vs 量化抖动)再判违例,勿直接放宽本阈值。</para></summary>
        private const double HueDriftMaxDegrees = 12.0;

        // ── helpers(精简自 focus_visual_and_accessibility_test 同名基建;公式锚定见 AC-022-1 ①)──

        private static double SrgbToLinear(double c) =>
            c <= 0.04045 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);

        private static double LinearLuminance(Color32 p) =>
            0.2126 * SrgbToLinear(p.r / 255.0) +
            0.7152 * SrgbToLinear(p.g / 255.0) +
            0.0722 * SrgbToLinear(p.b / 255.0);

        /// <summary>从 theme 文件现抽 hex(改色判据跟随,防第二真源)。
        /// <para>⚠️ 评审 BLOCKING 修复(2026-10-08):**先剥注释 + 钉唯一声明** ——
        /// 否则「注释掉声明保留原文」会抽到死文本(判据绿、运行期字色失效),
        /// 残留同形旧声明时首匹配会读旧值。与同文件 022-2 剥注释口径一致。</para></summary>
        private static Color32 ExtractThemeColor(string themeText, string varName)
        {
            // 剥块注释 + 行注释(照 022-2;USS 官方仅块注释,行注释防御性同剥)
            string body = Regex.Replace(themeText, @"/\*.*?\*/", "",
                RegexOptions.Singleline);
            body = Regex.Replace(body, @"//[^\r\n]*", "");
            var ms = Regex.Matches(body,
                Regex.Escape(varName) + @":\s*#([0-9a-fA-F]{6})\s*;");
            Assert.AreEqual(1, ms.Count,
                $"主题变量 {varName}: 须恰 1 处活声明(实见 {ms.Count})—— " +
                "0 处 = 声明被注释/删除(运行期 var() 失效);>1 处 = 残留旧声明(读首匹配即第二真源)。");
            var m = ms[0];
            return new Color32(
                System.Convert.ToByte(m.Groups[1].Value.Substring(0, 2), 16),
                System.Convert.ToByte(m.Groups[1].Value.Substring(2, 2), 16),
                System.Convert.ToByte(m.Groups[1].Value.Substring(4, 2), 16),
                255);
        }

        /// <summary>HSV 色相(度,0–360)。手写 —— BCL 无 HSV,禁引 System.Drawing。</summary>
        private static double HueDegrees(Color32 p)
        {
            double r = p.r / 255.0, g = p.g / 255.0, b = p.b / 255.0;
            double max = System.Math.Max(r, System.Math.Max(g, b));
            double min = System.Math.Min(r, System.Math.Min(g, b));
            double delta = max - min;
            if (delta < 1e-9) return 0.0; // 无饱和 ⇒ 色相无定义,按 0(ΔH 与灰比较恒 0 —— 本判据只用于墨对,两值均有饱和)
            double h;
            if (max == r) h = 60.0 * ((g - b) / delta % 6.0);
            else if (max == g) h = 60.0 * ((b - r) / delta + 2.0);
            else h = 60.0 * ((r - g) / delta + 4.0);
            return h < 0 ? h + 360.0 : h;
        }

        private static double HueDelta(double a, double b)
        {
            double d = System.Math.Abs(a - b) % 360.0;
            return d > 180.0 ? 360.0 - d : d;
        }

        /// <summary>读 PNG 为像素表(Texture2D.LoadImage —— 长期稳定 BCL 面)。</summary>
        private static List<Color32> LoadPixels(string path)
        {
            Assert.IsTrue(File.Exists(path), $"前置失败:{path} 不存在。");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)),
                    $"LoadImage 失败:{path}。");
                return new List<Color32>(tex.GetPixels32());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        // ── AC-022-1:两态主题色明度轴(art-bible §4.5 机械化)──

        [Test]
        public void test_ac022_1_ink_wet_dry_theme_colors_luminance_axis_only()
        {
            // Arrange:theme 现抽(改色判据跟随)
            string themePath = Path.Combine(SkeuoDir, "SkeuoThemeVariables.uss");
            Assert.IsTrue(File.Exists(themePath), $"前置失败:{themePath} 不存在。");
            string theme = File.ReadAllText(themePath);

            // Act
            var wet = ExtractThemeColor(theme, "--skeuo-ink-fg-wet");
            var dry = ExtractThemeColor(theme, "--skeuo-ink-fg-dry");
            double lw = LinearLuminance(wet);
            double ld = LinearLuminance(dry);
            double dh = HueDelta(HueDegrees(wet), HueDegrees(dry));

            // Assert ① 公式锚定(防 sRGB/Rec.709 公式退化;评审修复:白/黑/128灰 三连
            //    = focus_visual_and_accessibility_test 先例 + 纯通道权重三锚 ——
            //    白/黑钉不住 gamma,128灰钉 gamma,纯通道钉 0.2126/0.7152/0.0722 权重互换)
            Assert.AreEqual(1.0, LinearLuminance(new Color32(255, 255, 255, 255)),
                1e-6, "公式锚定:白 L 应 = 1.0。");
            Assert.AreEqual(0.0, LinearLuminance(new Color32(0, 0, 0, 255)),
                1e-6, "公式锚定:黑 L 应 = 0。");
            Assert.AreEqual(0.21586, LinearLuminance(new Color32(128, 128, 128, 255)),
                1e-4, "公式锚定:128 灰 L ≈ 0.21586 —— gamma 退化(如直用 128/255)即失守。");
            Assert.AreEqual(0.2126, LinearLuminance(new Color32(255, 0, 0, 255)),
                1e-6, "公式锚定:纯红 L = 0.2126 —— Rec.709 r 权重退化/互换即失守。");
            Assert.AreEqual(0.7152, LinearLuminance(new Color32(0, 255, 0, 255)),
                1e-6, "公式锚定:纯绿 L = 0.7152 —— g 权重退化/互换即失守。");
            Assert.AreEqual(0.0722, LinearLuminance(new Color32(0, 0, 255, 255)),
                1e-6, "公式锚定:纯蓝 L = 0.0722 —— b 权重退化/互换即失守。");
            // HueDegrees 锚(评审修复:恒返 0 会让 ΔH 断言恒过 ⇒ 钉两极值)
            Assert.AreEqual(0.0, HueDegrees(new Color32(255, 0, 0, 255)),
                1e-6, "色相锚定:hue(纯红) 应 = 0° —— HueDegrees 恒 0 退化即失守。");
            Assert.AreEqual(120.0, HueDegrees(new Color32(0, 255, 0, 255)),
                1e-6, "色相锚定:hue(纯绿) 应 = 120° —— 分支选择/公式退化即失守。");

            // Assert ② 湿态 = art-bible §4.1 权威浓墨(非提案,可锚 —— 改权威值须先改 art-bible)
            Assert.AreEqual(new Color32(0x26, 0x24, 0x1F, 255), wet,
                "湿态墨色须 = art-bible §4.1 权威浓墨 #26241F(湿笔洇开的本色)。");

            // Assert ③ 不色相漂移(§4.5「只走明度轴」;阈值提案,归数值轮)
            Assert.LessOrEqual(dh, HueDriftMaxDegrees,
                $"湿/干墨色色相差 {dh:F2}° > {HueDriftMaxDegrees}° —— 出现色相漂移," +
                "违 art-bible §4.5「只走明度轴」(墨龄色盲安全的结构前提)。");

            // Assert ④ 色沉方向:干比湿深(§4.5「边缘收干、色沉」;方向判据,无阈值)
            Assert.Less(ld, lw,
                $"干墨 L({ld:F4}) 应 < 湿墨 L({lw:F4}) —— 「色沉」方向失守(§4.5)。");
        }

        // ── AC-022-2:两态类接冻结图(C8 一致 + 单槽语义)──

        [Test]
        public void test_ac022_2_ink_wet_dry_classes_bound_to_frozen_textures()
        {
            // Arrange:SkeuoInk.uss 剥注释(USS 官方仅块注释;行注释防御性同剥)
            string ussPath = Path.Combine(SkeuoDir, "SkeuoInk.uss");
            Assert.IsTrue(File.Exists(ussPath), $"前置失败:{ussPath} 不存在。");
            string body = Regex.Replace(File.ReadAllText(ussPath), @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            body = Regex.Replace(body, @"//[^\r\n]*", "");

            // Act:两状态类选择器块
            var wetBlock = Regex.Match(body, @"\.ink-wet\s*\{[^}]*\}");
            var dryBlock = Regex.Match(body, @"\.ink-dry\s*\{[^}]*\}");
            Assert.IsTrue(wetBlock.Success, "缺 `.ink-wet` 声明块(AC-022-1 形态对的湿态)。");
            Assert.IsTrue(dryBlock.Success, "缺 `.ink-dry` 声明块(AC-022-1 形态对的干态)。");

            // Assert ① 各绑冻结件 guid(评审修复:锚 `background-image: url(...)` 全形 ——
            //    只 Contains guid 时块内他处提到 guid 也过,且选择器改造的假红面见文件头登记)
            Assert.IsTrue(Regex.IsMatch(wetBlock.Value,
                    $@"background-image:\s*url\(\s*""guid:{InkWetGuid}""\s*\)"),
                ".ink-wet 须以 background-image 绑 ink_wet-final.png 冻结 guid。");
            Assert.IsTrue(Regex.IsMatch(dryBlock.Value,
                    $@"background-image:\s*url\(\s*""guid:{InkDryGuid}""\s*\)"),
                ".ink-dry 须以 background-image 绑 ink_dry-final.png 冻结 guid。");

            // Assert ② 颜色走 theme(湿 = 权威浓墨变量 / 干 = 提案变量;C2 门禁内联色)。
            //    评审修复:`color:` 恰 1 次 + 即该 var —— 防块内后置覆盖(换 var / 具名色)假绿
            foreach (var (name, block, varName) in new[]
            {
                (".ink-wet", wetBlock.Value, "--skeuo-ink-fg-wet"),
                (".ink-dry", dryBlock.Value, "--skeuo-ink-fg-dry"),
            })
            {
                var colorDecls = Regex.Matches(block, @"color:\s*[^;]+;");
                Assert.AreEqual(1, colorDecls.Count,
                    $"{name} 块内 `color:` 须恰 1 处(实见 {colorDecls.Count})—— " +
                    "后置覆盖可让存在性断言假绿。");
                Assert.IsTrue(Regex.IsMatch(colorDecls[0].Value,
                        $@"var\({Regex.Escape(varName)}\)"),
                    $"{name} 色须走 var({varName})(C2 门)。");
            }

            // Assert ③ 两块均无 -unity-slice-*(冻结件 slice = 0 —— 明示不走九宫格;
            //    C8 门第 4 节同口径,SkeuoInk.uss 整文件零 slice 行)
            foreach (var (name, block) in new[] { (".ink-wet", wetBlock.Value), (".ink-dry", dryBlock.Value) })
                Assert.IsFalse(block.Contains("-unity-slice-"),
                    $"{name} 不得有 -unity-slice-* 行(冻结件 slice = 0,C8 门)。");

            // Assert ④ guid 真实存在且 .meta 内 guid == 常量(评审修复:光查 .meta 存在时,
            //    const 与 USS 同步漂移可整体假绿;比对 .meta 把 const 钉回第二真源)
            foreach (var (name, guid) in new[] { ("wet", InkWetGuid), ("dry", InkDryGuid) })
            {
                string metaPath = Path.Combine(SkeuoDir, "Textures",
                    $"ink_{name}-final.png.meta");
                Assert.IsTrue(File.Exists(metaPath),
                    $"guid {guid} 对应 .meta 不存在(悬空引用)。");
                string metaText = File.ReadAllText(metaPath);
                Assert.IsTrue(Regex.IsMatch(metaText, $@"^guid:\s*{guid}\s*$",
                        RegexOptions.Multiline),
                    $"ink_{name}-final.png.meta 内 guid ≠ 常量 {guid} —— " +
                    "const/图三方必有一漂移,须同批回填。");
            }

            // Assert ⑤ 冻结记录同步(USS 落点已登记 —— 防「引用却未登记」失效模式)。
            //    评审修复:限定 freeze-v1 围栏内断言(照 C8 解析口径 —— 全文件 Contains 会
            //    被表格外散文误触,围栏内才是机器真源)
            // 路径跟随门常量(防与 C8 门两处漂移 —— DefaultRepoRoot = 含 Assets/ 的 unity 项目根,
            // 冻结件在其上一级,故相对路径以 ".." 起)
            string freezePath = Path.GetFullPath(Path.Combine(RepoRoot,
                TextureBindingGates.FreezeRecordRelPath));
            Assert.IsTrue(File.Exists(freezePath), $"前置失败:{freezePath} 不存在。");
            string freeze = File.ReadAllText(freezePath);
            // 围栏正则照 C8 门同款(TextureBindingGates.cs:303-304);
            // 行匹配容忍 `| 0 |` 行内空格(C8 Trim+Split 同宽容 ⇒ 不比 C8 脆,防假红)
            var blockMatch = Regex.Match(freeze,
                @"```freeze-v1\s*\r?\n(?<body>.*?)\r?\n```", RegexOptions.Singleline);
            Assert.IsTrue(blockMatch.Success, "冻结记录缺 freeze-v1 机器块(前置失败)。");
            string freezeBody = blockMatch.Groups["body"].Value;
            foreach (var (file, uss) in new[]
            {
                ("ink_wet-final.png", "SkeuoInk.uss"),
                ("ink_dry-final.png", "SkeuoInk.uss"),
            })
                Assert.IsTrue(Regex.IsMatch(freezeBody,
                        $@"^\s*{Regex.Escape(file)}\s*\|\s*0\s*\|\s*{Regex.Escape(uss)}\s*$",
                        RegexOptions.Multiline),
                    $"freeze-v1 围栏内须登记 `{file}|0|{uss}`(引用却未登记 = 失效模式;" +
                    "或 slice/USS 落点被改)。");

            // Assert ⑥ 状态类零注册守卫(边界裁定 1:两态非注册变体;误注册后 C3/C7 全绿不可见)
            string registryPath = Path.Combine(SkeuoDir, "SkeuoComponentRegistry.cs");
            Assert.IsTrue(File.Exists(registryPath), $"前置失败:{registryPath} 不存在。");
            string registry = File.ReadAllText(registryPath);
            Assert.IsFalse(Regex.IsMatch(registry,
                    @"""ink-(wet|dry)"""),
                "SkeuoComponentRegistry 不得注册 ink-wet/ink-dry —— " +
                "状态类非注册变体(边界裁定 1,同 .focus-visible 时间状态语义)。");
        }

        // ── AC-022-3:洇开语义(贴图级方向锁)──

        [Test]
        public void test_ac022_3_wet_ink_spread_covers_more_than_dry()
        {
            // Arrange:真贴图(冻结件本体)
            string texDir = Path.Combine(SkeuoDir, "Textures");
            var wetPixels = LoadPixels(Path.Combine(texDir, "ink_wet-final.png"));
            var dryPixels = LoadPixels(Path.Combine(texDir, "ink_dry-final.png"));

            // Act:墨像素覆盖率(L < 带宽【提案·归数值轮】)
            double wetCover = Coverage(wetPixels, "wet");
            double dryCover = Coverage(dryPixels, "dry");

            // Assert:方向锁 ——「边缘洇开」⇒ 湿覆盖大;「边缘收干」⇒ 干覆盖小(§4.5)。
            //    **只锁方向不锚数值**(两图是形态差非色差;数值随资产轮变动,方向是语义)。
            //    评审修复:`Greater` 严格大(实测余量 0.969 vs 0.646)—— `GreaterOrEqual`
            //    在两图退化同覆盖率时假绿,与方法名 covers_more 和 AC-022-1 严格 Less 口径不一。
            Assert.Greater(wetCover, dryCover,
                $"湿墨覆盖率({wetCover:F3})应 > 干墨({dryCover:F3}) —— " +
                "「边缘洇开 vs 收干」的形态方向失守(art-bible §4.5)。");

            double Coverage(List<Color32> px, string label)
            {
                // 评审修复:fail-loud 于非不透明像素 —— RGBA 图若带透明底,
                // (0,0,0,0) L=0 会被计为墨、双图覆盖率齐趋 1(系统性假绿)
                foreach (var p in px)
                    if (p.a != 255)
                        Assert.Fail($"ink_{label}-final.png 含非不透明像素(a={p.a}) —— " +
                            "覆盖率口径假定全不透明;资产轮改 RGBA 须先重裁本判据。");
                int ink = 0;
                foreach (var p in px)
                    if (LinearLuminance(p) < InkPixelLuminanceBand) ink++;
                return (double)ink / px.Count;
            }
        }
    }
}
