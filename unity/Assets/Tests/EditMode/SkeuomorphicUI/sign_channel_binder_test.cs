using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    /// <summary>M2 形态件④(story-024 · AC-024-1/2/3)—— 一条真实状态反馈通道:
    /// 体征词条(8 的 <c>SignLexemeRow</c> 真源)→ 脉案五通道分发 + 通道值区形态 + 真表端到端。
    /// <para>判据权威 = diagnosis-system 规则五四态 · F-8.2 空档回退(:1003-1011) ·
    /// §Edge Cases(:1178-1186 并列不相斥 / 阴性形态) · :329(未查 = 空行,非 badge)+
    /// skeuomorphic-ui.md:64-65(「看懂了没有」由纸面物理形态回答)。</para>
    /// <para>⚠️ 门式读仓例外声明(照 texture_binding_gate / story-021/022/023 先例):
    /// 判据对象 = USS 文件本体 + 版本化真表 <c>assets/data/diagnosis_signs.json</c>,必须读仓;
    /// 不依赖网络 / 数据库 / 可变外部状态;文件系统例外面 = 仓库内版本化只读文件。</para>
    /// <para>⚠️ 本工程未装 Newtonsoft(schema_types_primary_key_test.cs :745)——
    /// 真表一律手写正则抽取(照 recipe_validation_fixtures 同型)。</para></summary>
    [Category("SkeuomorphicUI")]
    public class sign_channel_binder_test
    {
        private static string RepoRoot =>
            TextureBindingGates.DefaultRepoRoot;

        private static string SkeuoDir =>
            Path.Combine(RepoRoot, "Assets", "Gameplay.UI", "Skeuomorphic");

        /// <summary>真表路径(DefaultRepoRoot = 含 <c>Assets/</c> 的 unity 工程根;
        /// 仓库根在上一层 —— 承 <c>FreezeRecordRelPath = "../design/…"</c> 同型)。</summary>
        private static string RealSignTablePath =>
            Path.GetFullPath(Path.Combine(RepoRoot, "..", "assets", "data", "diagnosis_signs.json"));

        /// <summary>剥 USS 注释(官方仅块注释;行注释防御性同剥 —— 022/023 同款)。</summary>
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

        /// <summary>块内属性名集(去空白;行首锚 + Multiline —— 023 修复:消值内冒号幻影)。</summary>
        private static HashSet<string> PropsOf(string block) =>
            new HashSet<string>(Regex.Matches(block, @"^\s*([\w-]+)\s*:",
                    RegexOptions.Multiline)
                .Cast<Match>().Select(m => m.Groups[1].Value));

        /// <summary>夹具词条(七字段;revealBy 不入分发面 —— binder 零消费,传空数组)。</summary>
        private static SignLexemeRow Row(
            string id, string[] words, SignChannel channel,
            SignPolarity polarity = SignPolarity.Positive)
            => new SignLexemeRow(id, words, channel,
                Array.Empty<RevealMethod>(), 1, polarity, null);

        // ── AC-024-1: 通道分发纯函数(四态语义机械化)──

        [Test]
        public void test_ac024_1_binder_dispatch_covers_four_states()
        {
            // Arrange:六通道各一行(五通道 + 病史例外 —— 通道闭集 cardinality = 6)
            var six = new[]
            {
                Row("s_face", new[] { "面色词" }, SignChannel.FaceColor),
                Row("s_voice", new[] { "语声词" }, SignChannel.Voice),
                Row("s_posture", new[] { "姿态词" }, SignChannel.Posture),
                Row("s_breath", new[] { "呼吸词" }, SignChannel.Breathing),
                Row("s_touch", new[] { "触感词" }, SignChannel.Touch),
                Row("s_history", new[] { "病史词" }, SignChannel.History),
            };

            // Act
            var result = SignChannelBinder.Bind(six, slot: 0);

            // Assert ① 六通道全在且各恰 1(五通道 + 病史例外分流,零漏零溢)
            Assert.AreEqual(SignChannelBinder.ChannelCardinality, 6,
                "通道闭集基数须为 6(五通道 + 病史例外,AC-8-32)。");
            Assert.AreEqual(SignChannelBinder.ChannelCardinality, result.Count,
                $"分发结果键集须 = 6 通道(实见 {result.Count})。");
            foreach (SignChannel ch in Enum.GetValues(typeof(SignChannel)))
            {
                Assert.IsTrue(result.ContainsKey(ch), $"结果缺通道桶 {ch}。");
                Assert.AreEqual(1, result[ch].Count, $"通道 {ch} 须恰 1 条读数。");
            }
            Assert.AreEqual("s_face", result[SignChannel.FaceColor][0].SignId);
            Assert.AreEqual("s_history", result[SignChannel.History][0].SignId,
                "病史例外(sign_purulent_stool 所在通道)须落 History 桶。");

            // Assert ② 空桶语义:输入空 ⇒ 全通道在结果中且空列表(渲染层据此走「未查 = 空行」,
            //    分发器不发明第四态 —— :329)
            var empty = SignChannelBinder.Bind(Array.Empty<SignLexemeRow>(), 0);
            Assert.AreEqual(SignChannelBinder.ChannelCardinality, empty.Count,
                "空输入仍须预建全通道空桶(未查态的可表达性)。");
            foreach (var kv in empty)
                Assert.IsEmpty(kv.Value, $"通道 {kv.Key} 空输入须为空列表(实见 {kv.Value.Count})。");

            // Assert ③ 档位取词直取(slot 落点词非 null)
            var slotWords = new[] { Row("s_slot", new[] { "粗词", "中词", "细词" }, SignChannel.FaceColor) };
            Assert.AreEqual("中词", SignChannelBinder.Bind(slotWords, 1)[SignChannel.FaceColor][0].Word,
                "slot=1 须取中档词。");
            Assert.AreEqual("细词", SignChannelBinder.Bind(slotWords, 2)[SignChannel.FaceColor][0].Word,
                "slot=2 须取细档词。");

            // Assert ④ F-8.2 向尾回退(slot 档空 ⇒ 向数组尾找第一个非空词;:1003 字面)
            var fallback = new[] { Row("s_fb", new string[] { null, "回退词", null }, SignChannel.FaceColor) };
            Assert.AreEqual("回退词",
                SignChannelBinder.Bind(fallback, 0)[SignChannel.FaceColor][0].Word,
                "slot 档为空须向尾回退(F-8.2:粗档无词 → 中档例)。");

            // Assert ④′ 空串档回退(测试面 A1:IsNullOrEmpty 语义 —— 空串按空处理,继续向尾;
            //    与 null 档同路径但入口不同,GDD 禁空串故真表不出现,此处守防御性语义)
            var blankSlot = new[] { Row("s_blank", new[] { "", "空串后词" }, SignChannel.FaceColor) };
            Assert.AreEqual("空串后词",
                SignChannelBinder.Bind(blankSlot, 0)[SignChannel.FaceColor][0].Word,
                "空串档须按空处理继续向尾回退(IsNullOrEmpty,非仅 != null)。");

            // Assert ⑤ 全空 ⇒ Word = null(读不出 ⇒ 阴性形态,:1005 非阳性)
            var allNull = new[] { Row("s_null", new string[] { null, null }, SignChannel.Voice) };
            Assert.IsNull(
                SignChannelBinder.Bind(allNull, 0)[SignChannel.Voice][0].Word,
                "全空档须回退到底 ⇒ Word = null(读不出,按阴性形态显示,不是阳性)。");
            var emptyArr = new[] { Row("s_empty", Array.Empty<string>(), SignChannel.Voice) };
            Assert.IsNull(
                SignChannelBinder.Bind(emptyArr, 0)[SignChannel.Voice][0].Word,
                "空词表须 ⇒ Word = null(读不出)。");

            // Assert ⑥ 同通道多词条并列保序(:1183 不相斥 —— Order = 输入序透传)
            var par = new[]
            {
                Row("p1", new[] { "甲词" }, SignChannel.Touch),
                Row("p2", new[] { "乙词" }, SignChannel.Touch),
                Row("p3", new[] { "丙词" }, SignChannel.Touch),
            };
            var touchBucket = SignChannelBinder.Bind(par, 0)[SignChannel.Touch];
            CollectionAssert.AreEqual(
                new[] { "p1", "p2", "p3" },
                touchBucket.Select(r => r.SignId).ToArray(),
                "同通道多词条须按输入序并列(:1183 不相斥)。");
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 },
                touchBucket.Select(r => r.Order).ToArray(),
                "Order 须透传输入序(渲染层稳定排版依据)。");

            // Assert ⑦ slot 越界 fail-loud(词表非空时;负档同)
            var threeSlot = new[] { Row("s_bound", new[] { "a", "b", "c" }, SignChannel.FaceColor) };
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SignChannelBinder.Bind(threeSlot, 3),
                "slot ≥ 词表长度须 fail-loud(不静默取 null —— 静默 null 与「读不出」同形,语义污染)。");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SignChannelBinder.Bind(threeSlot, -1),
                "slot 负值须 fail-loud。");

            // Assert ⑧ 非法枚举值 fail-loud(通道闭集之外的值拒收)
            var illegal = new[] { Row("s_bad", new[] { "词" }, (SignChannel)99) };
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SignChannelBinder.Bind(illegal, 0),
                "通道枚举闭集外的值须 fail-loud(五通道 + 病史例外,AC-8-32)。");

            // Assert ⑨ 极性透传(夹具双值 —— 阴性词条与阳性在呈现层同走降档类,但字段本身透传;
            //    测试面 A1:硬编码 Positive 突变在此失守)
            var polFixture = new[]
            {
                Row("pol_pos", new[] { "阳词" }, SignChannel.FaceColor, SignPolarity.Positive),
                Row("pol_neg", new[] { "阴词" }, SignChannel.FaceColor, SignPolarity.Negative),
            };
            var polBucket = SignChannelBinder.Bind(polFixture, 0)[SignChannel.FaceColor];
            Assert.AreEqual(SignPolarity.Positive, polBucket[0].Polarity, "阳性夹具极性须透传。");
            Assert.AreEqual(SignPolarity.Negative, polBucket[1].Polarity, "阴性夹具极性须透传。");

            // Assert ⑩ null 输入 fail-loud
            Assert.Throws<ArgumentNullException>(
                () => SignChannelBinder.Bind(null, 0),
                "null 输入须 fail-loud。");
        }

        // ── AC-024-2: 通道值区形态对(阳性墨字 / 阴性降档 / 零未查徽章)──

        [Test]
        public void test_ac024_2_value_area_forms()
        {
            // Arrange:Raw(头注锚用)+ 剥注释(块提取用)
            string raw = ReadUss("SkeuoPaper.uss");
            string body = StripComments(raw);

            // Act
            var posBlock = BlockOf(body, ".channel-reading");
            var negBlock = BlockOf(body, ".channel-reading-negative");

            // Assert ① 两态类块存在(阳性满墨 / 阴性降档 —— 形态对本体)
            Assert.IsTrue(posBlock.Success,
                "缺 `.channel-reading` 声明块 —— 已查阳性值区形态未落。");
            Assert.IsTrue(negBlock.Success,
                "缺 `.channel-reading-negative` 声明块 —— 阴性/读不出降档形态未落。");

            // Assert ② 值全走 theme(023 修复同款:属性集 ⊇ 点名 + 逐声明值含 var(--skeuo-
            //    —— 删行 / 具名色 / 异命名空间 / 裸数字 四分支全灭)
            foreach (var pair in new[] { (".channel-reading", posBlock), (".channel-reading-negative", negBlock) })
            {
                string cls = pair.Item1, decl = pair.Item2.Value;
                var props = PropsOf(decl);
                foreach (var r in new[] { "color", "font-family", "font-size" })
                    Assert.IsTrue(props.Contains(r),
                        $"{cls} 须声明 `{r}`(story AC 点名载体;删/改名即红)。");
                var decls = Regex.Matches(decl, @"([\w-]+)\s*:\s*([^;]+);")
                    .Cast<Match>().ToList();
                Assert.IsNotEmpty(decls, $"{cls} 声明块须非空。");
                foreach (var d in decls)
                {
                    string prop = d.Groups[1].Value, value = d.Groups[2].Value.Trim();
                    Assert.IsTrue(value.Contains("var(--skeuo-"),
                        $"{cls} `{prop}: {value}` 须走 var(--skeuo-*) —— " +
                        "具名色 / 裸数字 / 异命名空间 var 均在此失守。");
                }
            }

            // Assert ③ 阳性/阴性两档墨色可分且各钉死(降档 = :1185 承载形态传达把握不足)
            Assert.IsTrue(Regex.IsMatch(posBlock.Value, @"color:\s*var\(--skeuo-ink-fg\)"),
                ".channel-reading 阳性须钉 var(--skeuo-ink-fg)(满墨)。");
            Assert.IsTrue(Regex.IsMatch(negBlock.Value, @"color:\s*var\(--skeuo-ink-faded\)"),
                ".channel-reading-negative 须钉 var(--skeuo-ink-faded)(墨色降档)—— " +
                ":1185「把握不足只由承载形态传达,不以文字或标记出现」。");
            string posColor = Regex.Match(posBlock.Value, @"color:\s*([^;]+);").Groups[1].Value.Trim();
            string negColor = Regex.Match(negBlock.Value, @"color:\s*([^;]+);").Groups[1].Value.Trim();
            Assert.AreNotEqual(posColor, negColor,
                "阳性/阴性色值须可分(两档同值 = 降档丢失,阴性形态不可读)。");

            // Assert ④ 块内零数字 + 零 content(数字/标记形态禁入 —— :1185 不以文字或标记出现)
            foreach (var pair in new[] { (".channel-reading", posBlock.Value),
                                          (".channel-reading-negative", negBlock.Value) })
            {
                Assert.IsFalse(Regex.IsMatch(pair.Item2, @"\d"),
                    $"{pair.Item1} 块内禁数字。");
                Assert.IsFalse(Regex.IsMatch(pair.Item2, @"content\s*:"),
                    $"{pair.Item1} 块内禁 content: 属性(标记载体)。");
            }

            // Assert ⑤ 全库零「未查徽章」类(:329 硬禁 —— 未查是空行不是 badge/灰字/占位符;
            //    剥注释扫全库 Skeuo *.uss,命名面机械化)
            var badgeHits = new List<string>();
            foreach (var f in Directory.GetFiles(SkeuoDir, "*.uss", SearchOption.AllDirectories))
            {
                string stripped = StripComments(File.ReadAllText(f));
                string rel = f.Substring(SkeuoDir.Length).TrimStart(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (Match m in Regex.Matches(stripped,
                    @"\.(?:unchecked|unread|no-data|not-checked|not-observed|unexamined|placeholder|badge)[a-z0-9/-]*",
                    RegexOptions.IgnoreCase))
                    badgeHits.Add($"{rel}: `{m.Value}`");
            }
            Assert.IsEmpty(badgeHits,
                ":329 全库禁「未查徽章」命名(未查 = 空行,复用 .empty-row):\n" +
                string.Join("\n", badgeHits));

            // Assert ⑥ 头注规格锚(紧邻 .channel-reading 的注释组 —— 023 修复口径)
            var segMatch = Regex.Match(raw,
                @"/\*(?:[^*]|\*(?!/))*\*/\s*\.channel-reading\s*\{", RegexOptions.Singleline);
            Assert.IsTrue(segMatch.Success,
                "`.channel-reading` 块前无紧邻头注(规格锚无处安放)。");
            string headerSeg = segMatch.Value;
            StringAssert.Contains(":329", headerSeg,
                "头注须锚 :329(未查 = 空行非徽章 —— 四态语义权威)。");
            StringAssert.Contains("空行", headerSeg,
                "头注须锚「空行」(未查态 = 空行,复用 .empty-row,零新增未查类)。");
            StringAssert.Contains("归 8", headerSeg,
                "头注须声明映射归 8(体征 → 通道/词映射归 8,42 只画 —— 边界裁定②)。");

            // Assert ⑦ Registry 零注册守卫(语义样式类不进 Registry —— 022/023 同款边界)
            string registryPath = Path.Combine(SkeuoDir, "SkeuoComponentRegistry.cs");
            Assert.IsTrue(File.Exists(registryPath), $"前置失败:{registryPath} 不存在。");
            Assert.IsFalse(Regex.IsMatch(File.ReadAllText(registryPath), @"""channel-reading"""),
                "SkeuoComponentRegistry 不得注册 channel-reading —— 语义样式类不进 Registry。");

            // Assert ⑧ 全库声明块恰 1(每类;后置覆盖复活面 —— 023 修复口径)
            string allUss = StripComments(string.Join("\n",
                Directory.GetFiles(SkeuoDir, "*.uss", SearchOption.AllDirectories)
                    .Select(File.ReadAllText)));
            foreach (var cls in new[] { ".channel-reading", ".channel-reading-negative" })
            {
                var allBlocks = Regex.Matches(allUss,
                    Regex.Escape(cls) + @"\s*\{[^}]*\}");
                Assert.AreEqual(1, allBlocks.Count,
                    $"`{cls}` 声明块须全库恰 1(实见 {allBlocks.Count})—— " +
                    "第二处 = 后置覆盖复活(色档/数字可藏于此)。");
            }
        }

        // ── AC-024-3: 真表端到端判据(版本化 fixture 驱动 · 通道闭环可证伪)──

        [Test]
        public void test_ac024_3_real_sign_table_roundtrip()
        {
            // Arrange:读真表(门式读仓 —— 版本化只读 fixture)
            Assert.IsTrue(File.Exists(RealSignTablePath),
                $"前置失败:真表不存在 {RealSignTablePath}。");
            string json = File.ReadAllText(RealSignTablePath);

            // Assert ① schema_version = 1(版本化 fixture 形状)
            var sv = Regex.Match(json, @"""schema_version""\s*:\s*(\d+)");
            Assert.IsTrue(sv.Success, "真表缺 schema_version。");
            Assert.AreEqual("1", sv.Groups[1].Value,
                $"schema_version 须 = 1(实见 {sv.Groups[1].Value})。");

            // Assert ①′ 真表 channel 原始值集恰 = 闭集六值(评审 B1 修复:原「34 词条全落合法通道」
            //    实由下方 switch 保证,与 `Bind` 的非法枚举守卫**结构不等价**(真表行必落预建桶,
            //    守卫不可达)⇒ 此处独立扫原始中文值集,给「真表通道面零漏零溢」独立证据)
            var rawChannels = Regex.Matches(json, @"""channel""\s*:\s*""([^""]+)""")
                .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.IsNotEmpty(rawChannels, "真表零 channel 字段(前置失败)。");
            CollectionAssert.AreEquivalent(
                new[] { "面色", "语声", "姿态", "呼吸", "触感", "问诊(非体格通道)" },
                rawChannels.Distinct().ToList(),
                "真表 channel 原始值集须恰 = 六值闭集(五通道 + 病史例外,AC-8-32)—— " +
                "多值 = 真表新增通道而闭集未同步;少值 = 通道面被删。");

            // Act:按出现序切行(行体 = 本 sign_id 到下一 sign_id)
            var idMatches = Regex.Matches(json, @"""sign_id""\s*:\s*""([^""]+)""").Cast<Match>().ToList();
            Assert.IsNotEmpty(idMatches, "真表零行(前置失败)。⚠️ 行数是内容 —— 不锚固定值(数值/内容归 8 与数值轮)。");

            var rows = new List<SignLexemeRow>();
            var expectedPerSlot = new Dictionary<int, List<(string id, SignChannel ch, string word)>>();
            for (int s = 0; s < 3; s++) expectedPerSlot[s] = new List<(string, SignChannel, string)>();
            var expectedPolarity = new Dictionary<string, SignPolarity>(); // 测试面 A1:Polarity 透传断言

            for (int i = 0; i < idMatches.Count; i++)
            {
                int start = idMatches[i].Index;
                int end = i + 1 < idMatches.Count ? idMatches[i + 1].Index : json.Length;
                string bodySlice = json.Substring(start, end - start);
                string signId = idMatches[i].Groups[1].Value;

                // display_词 数组(词 | null 交错;空串非法 —— GDD 禁空串)
                var arrMatch = Regex.Match(bodySlice, @"""display_词""\s*:\s*\[([^\]]*)\]");
                Assert.IsTrue(arrMatch.Success, $"{signId} 缺 display_词。");
                var words = Regex.Matches(arrMatch.Groups[1].Value, @"""([^""]*)""|null")
                    .Cast<Match>()
                    .Select(t => t.Value == "null" ? null : t.Groups[1].Value)
                    .ToArray();
                Assert.IsNotEmpty(words, $"{signId} 词表为空。");
                // null 档合法(F-8.2 空白档回退的可表达性 —— sign_koplik 实有 null 档);
                // 空串才是非法(空串与阴性形态呈现层同构 —— GDD 禁)
                foreach (var w in words)
                    Assert.AreNotEqual("", w,
                        $"{signId} 出现空串词 —— 空串与阴性形态在呈现层同构,GDD 禁空串。");

                // 中文 channel → SignChannel 闭集映射(未知中文 fail —— 42 零自定义映射,
                // 中文值集由 8 的真表定;此处只做端到端映射,不发明新通道)
                var chMatch = Regex.Match(bodySlice, @"""channel""\s*:\s*""([^""]+)""");
                Assert.IsTrue(chMatch.Success, $"{signId} 缺 channel。");
                SignChannel channel = chMatch.Groups[1].Value switch
                {
                    "面色" => SignChannel.FaceColor,
                    "语声" => SignChannel.Voice,
                    "姿态" => SignChannel.Posture,
                    "呼吸" => SignChannel.Breathing,
                    "触感" => SignChannel.Touch,
                    "问诊(非体格通道)" => SignChannel.History,
                    _ => throw new AssertionException(
                        $"{signId} channel「{chMatch.Groups[1].Value}」超出闭集 " +
                        "(面色/语声/姿态/呼吸/触感/问诊(非体格通道))—— 中文映射断或真表新增通道,须同步闭集。"),
                };

                // polarity 中文 → SignPolarity
                var polMatch = Regex.Match(bodySlice, @"""polarity""\s*:\s*""([^""]+)""");
                Assert.IsTrue(polMatch.Success, $"{signId} 缺 polarity。");
                SignPolarity polarity = polMatch.Groups[1].Value switch
                {
                    "阳性体征" => SignPolarity.Positive,
                    "阴性体征" => SignPolarity.Negative,
                    _ => throw new AssertionException(
                        $"{signId} polarity「{polMatch.Groups[1].Value}」超出闭集(阳性体征/阴性体征)。"),
                };

                // tier_named(纯 int 计数 —— 移出 Fix 解析集,承 ADR-006 D-21-17)
                var tierMatch = Regex.Match(bodySlice, @"""tier_named""\s*:\s*(\d+)");
                Assert.IsTrue(tierMatch.Success, $"{signId} 缺 tier_named。");
                int tierNamed = int.Parse(tierMatch.Groups[1].Value);

                rows.Add(new SignLexemeRow(signId, words, channel,
                    Array.Empty<RevealMethod>(), tierNamed, polarity, null));
                expectedPolarity[signId] = polarity;

                // 每 slot 的期望词(独立复算 F-8.2:从 slot 起第一个非空)
                for (int s = 0; s < 3; s++)
                {
                    string expected = null;
                    if (s < words.Length)
                    {
                        for (int k = s; k < words.Length; k++)
                            if (!string.IsNullOrEmpty(words[k])) { expected = words[k]; break; }
                    }
                    expectedPerSlot[s].Add((signId, channel, expected));
                }
            }

            // Assert ② 34 行(实计)全落合法通道 —— 分发后总读数守恒(各 slot)
            foreach (int s in new[] { 0, 1, 2 })
            {
                var bound = SignChannelBinder.Bind(rows, s);

                // 键集 = 6 通道
                Assert.AreEqual(SignChannelBinder.ChannelCardinality, bound.Count,
                    $"slot={s} 分发键集须 = 6 通道。");

                // 总读数 = 行数(零漏零溢)
                int total = bound.Sum(kv => kv.Value.Count);
                Assert.AreEqual(rows.Count, total,
                    $"slot={s} 总读数须 = 行数({rows.Count}),实见 {total}。");

                // 键集恰 = 六枚举(评审 B1 修复:显式断言真表行经 Bind 后键集闭集 ——
                //    与上方「原始值集」互为独立证据:此断言看 Bind 产物,彼断言看真表原文)
                CollectionAssert.AreEquivalent(
                    Enum.GetValues(typeof(SignChannel)).Cast<SignChannel>().ToList(),
                    bound.Keys.ToList(),
                    $"slot={s} 分发键集须恰 = SignChannel 六枚举(真表行全落合法桶)。");

                // 逐行期望词复算对拍(F-8.2 向尾回退 + 直取,真表驱动)
                foreach (var (id, ch, expected) in expectedPerSlot[s])
                {
                    var hit = bound[ch].FirstOrDefault(r => r.SignId == id);
                    Assert.IsNotNull(hit, $"slot={s} 行 {id} 未落通道桶 {ch}。");
                    Assert.AreEqual(expected, hit.Word,
                        $"slot={s} 行 {id} 取词不符 F-8.2(期望「{expected ?? "null"}」" +
                        $"实得「{hit.Word ?? "null"}」)。");
                    // 测试面 A1:Polarity 透传零变换(硬编码 Positive 突变在此失守)
                    Assert.AreEqual(expectedPolarity[id], hit.Polarity,
                        $"slot={s} 行 {id} Polarity 须透传 8 的真表极性(期望 {expectedPolarity[id]})。");
                }
                // 真表极性两面都出现(闭集二值均有实况 —— 阳性 + 阴性各至少一行)
                Assert.IsTrue(expectedPolarity.Values.Contains(SignPolarity.Positive) &&
                              expectedPolarity.Values.Contains(SignPolarity.Negative),
                    "真表极性闭集二值须均有实况(否则透传断言只覆盖单值面)。");

                // 同通道并列保序(Order = 输入序;真表同通道多行实况)
                foreach (var kv in bound)
                {
                    var orders = kv.Value.Select(r => r.Order).ToArray();
                    for (int i = 1; i < orders.Length; i++)
                        Assert.Greater(orders[i], orders[i - 1],
                            $"通道 {kv.Key} 读数 Order 须随输入序递增(:1183 并列保序)。");
                }
            }

            // Assert ③ 病史例外真表实落 History(AC-8-32「五通道 + 病史例外」端到端)
            var hist = SignChannelBinder.Bind(rows, 0)[SignChannel.History];
            Assert.IsNotEmpty(hist, "真表病史例外行(sign_purulent_stool)须落 History 桶。");
            CollectionAssert.Contains(hist.Select(r => r.SignId).ToList(), "sign_purulent_stool");

            // Assert ④ 真表 null 档回退实证(sign_koplik:前两档 null → slot0 须向尾回退到细档词;
            //    F-8.2 的真表锚 —— 空档回退不是夹具专属路径。通道面值以真表 channel 字段为准,
            //    不硬编码通道假设)
            var koplikRow = rows.First(r => r.SignId == "sign_koplik");
            var koplikBound = SignChannelBinder.Bind(rows, 0)[koplikRow.Channel]
                .First(r => r.SignId == "sign_koplik");
            Assert.AreEqual("口腔黏膜斑(Koplik 斑)", koplikBound.Word,
                "sign_koplik 前两档 null ⇒ slot0 须向尾回退取细档词(F-8.2 真表实证,:1003)。");
        }
    }
}
