// 权威来源:ADR-019 §五(调试视图 = 开发者本地;不交付玩家可见统计 UI)·
//   production/epics/telemetry-analytics/story-009-blank-and-degraded.md
//   (AC-51-D4 Release 无调试视图 · AC-51-D8 部分流显式标注 · AC-51-D9 样本不足照常出数 + 标注)·
//   design/gdd/telemetry-analytics.md `OQ-51-1`(2026-10-06 裁:两处分立 · 51 侧纯文本)
//
// ⚠️ 本件是 TD-epic-01 **调试视图**的判据(TD 账里「EPIC 三项交付唯一整项未落」的那一项)。
//   · D4:入口整体 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` ⇒ 判据 = **源文件形态**
//     (整个类型在条件编译内)+ **反射面只由入口驱动**(入口不归 51 的契约程序集);
//     ⚠️ Release 构建侧的「入口不存在」**本机不可自动化**(须 Release player 构建)⇒ **NOT-RUN**,不记绿。
//   · D8:空流 / 无可打分样本 ⇒ 头部**显式口径行**,不是静默零。
//   · D9:样本不足 ⇒ 正文多一条标注,**数值字段逐字不变**。
//   · AC-51-A5 / E1:51 侧零呈现依赖(无 UI Toolkit / UGUI / 42 程序集引用)+ 零 `.uxml`/`.uss`/Canvas 资产。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    [TestFixture]
    internal sealed class TelemetryDeveloperViewTest
    {
        private InMemoryBlobPool _pool;
        private IPayloadEncoder _encoder;
        private IPayloadDecoder _decoder;
        private EventStream _stream;
        private TelemetryReporter _reporter;
        private long _tailTick;

        private sealed class RecordingSink : ITelemetrySink
        {
            public readonly List<string> Written = new List<string>();
            public readonly List<string> Logged = new List<string>();
            public void WriteLocalReport(string fileName, string content) => Written.Add(fileName);
            public void LogDeveloperSummary(string summary) => Logged.Add(summary);
        }

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new SeqIdAuthority(), new PresenceRegistry());
            _reporter = new TelemetryReporter(_stream, _decoder,
                new[] { new DiseaseRegistryEntry { DiseaseId = 1, DiseaseKey = "d1" } });
            _tailTick = 0;
        }

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private long _n = 100;
            public PatientId NextPatientId() => new PatientId((int)_n++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_n++);
        }

        private void AppendCaseClosed(long caseSeq, ulong bits)
        {
            PayloadRef r = _encoder.Encode(EventKind.CaseClosed,
                new CaseClosedPayload(patientId: 1, new CaseId(10, 1, caseSeq),
                    new DiseaseIdSet(bits), treated: true));
            _stream.Append(new SimEvent(_tailTick++, new PatientId(1), -1, EventKind.CaseClosed, r));
        }

        private void AppendJudgment(long caseSeq, int lexiconId)
        {
            PayloadRef r = _encoder.EncodeJudgmentRecorded(
                new JudgmentRecordedPayload(1, new CaseId(10, 1, caseSeq), 1, lexiconId, 2), "");
            _stream.Append(new SimEvent(_tailTick++, new PatientId(1), -1, EventKind.JudgmentRecorded, r));
        }

        private static string RepoRoot()
        {
            DirectoryInfo d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Assets"))) d = d.Parent;
            Assert.That(d, Is.Not.Null, "找不到仓库根(含 Assets/ 的上级)—— 判据无从跑");
            return d!.FullName;
        }

        /// <summary>去注释后的源码(只留代码面)—— 判据不得被注释里的字面量满足或打红。</summary>
        private static string CodeOnly(string source) => string.Join("\n", source
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"/\*.*?\*/", string.Empty)));

        private static string BootSource(string fileName) =>
            File.ReadAllText(Path.Combine(RepoRoot(), "Assets", "Gameplay.Boot", fileName));

        // ══════════════ D4 · Release 无入口(编译期剔除)══════════════

        /// <summary>AC-51-D4(可自动化半边):调试视图的**可执行面**整体包在条件编译内 ——
        /// `TelemetryDeveloperView.cs` 的 `namespace` 位于 `#if` 之后、`#endif` 之前;
        /// `RenderDeveloperPreview` 入口同样在 `#if` 内。
        /// <para>判别力:把 `#if` 删掉 ⇒ 本条红(= 玩家构建里会有入口)。</para></summary>
        [Test]
        public void test_d4_viewSource_entirelyInsideConditionalCompilation()
        {
            // 只扫**代码面**(去注释行)—— 头注里逐字写着 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
            // 作说明,把注释算进去 ⇒ 删掉真 `#if` 也命中 ⇒ 判据永绿(首版实测即此,见证据 §三·七)
            string src = CodeOnly(BootSource("TelemetryDeveloperView.cs"));
            int ifIdx = src.IndexOf("#if UNITY_EDITOR || DEVELOPMENT_BUILD", StringComparison.Ordinal);
            int endIdx = src.LastIndexOf("#endif", StringComparison.Ordinal) + "#endif".Length;
            // 收尾须只剩空白与花括号 —— `#if` 若被改成恒真,`#endif` 仍在 `}` 之前、断言全过,
            // 但类型面已进玩家构建(首轮证伪实测 M-dv-1 不红即此)⇒ 把「块尾」钉到 EOF。
            Assert.That(src.Substring(endIdx).Replace("}", string.Empty).Trim(), Is.Empty,
                "条件编译块须包到文件末尾(块后只允许 namespace/class 的收尾花括号)");
            // **条件表达式本身**也须钉住:`#if true` 形态下块尾仍合规、断言全过,但剔除已失效。
            string cond = src.Split('\n')[ifIdx == 0 ? 0 : src.Substring(0, ifIdx).Count(c => c == '\n')].Trim();
            Assert.That(cond, Does.Contain("UNITY_EDITOR").And.Contain("DEVELOPMENT_BUILD"),
                "条件须恰为 `UNITY_EDITOR || DEVELOPMENT_BUILD`,不得换成恒真式:" + cond);
            int nsIdx = src.IndexOf("namespace DaYiJingCheng.Gameplay.Boot", StringComparison.Ordinal);
            Assert.That(ifIdx, Is.GreaterThan(0), "文件须有开发期条件编译");
            Assert.That(nsIdx, Is.GreaterThan(ifIdx), "类型声明须在 #if 之后(否则玩家构建里有本类型)");
            Assert.That(endIdx, Is.GreaterThan(nsIdx), "#endif 须在类型之后");
            // `using` 归 #if 内(本件把它也包进去,判据无需单列)—— **不允许**在 #endif 之后出现任何声明
            Assert.That(src.Substring(endIdx).Trim(), Is.Empty,
                "#endif 之后不得再有代码(否则那部分进玩家构建)");

            string reporter = CodeOnly(BootSource("TelemetryReporter.cs"));
            int entry = reporter.IndexOf("public string RenderDeveloperPreview", StringComparison.Ordinal);
            Assert.That(entry, Is.GreaterThan(0), "入口须存在(Editor/DevBuild 编译)");
            int guard = reporter.LastIndexOf("#if UNITY_EDITOR || DEVELOPMENT_BUILD", entry, StringComparison.Ordinal);
            int close = reporter.IndexOf("#endif", entry, StringComparison.Ordinal);
            Assert.That(guard, Is.GreaterThan(0), "入口须在条件编译内(否则玩家构建里也有入口)");
            Assert.That(close, Is.GreaterThan(entry));

            int summary = reporter.IndexOf("public string RenderDeveloperSummary", StringComparison.Ordinal);
            Assert.That(summary, Is.GreaterThan(entry), "默认预览入口须存在(Editor/DevBuild 编译)");
            int summaryGuard = reporter.LastIndexOf("#if UNITY_EDITOR || DEVELOPMENT_BUILD", summary, StringComparison.Ordinal);
            Assert.That(summaryGuard, Is.EqualTo(guard), "默认预览入口须与显式入口同处一个条件编译块");
            Assert.That(reporter.IndexOf("#endif", summary, StringComparison.Ordinal), Is.EqualTo(close));
        }

        /// <summary>AC-51-D4(反射面):51 的**契约程序集**(`Telemetry`)里没有任何预览入口 ——
        /// 入口只在 `Gameplay.Boot` 且受条件编译。
        /// <para>判别力:把入口挪进 `Telemetry` 程序集 ⇒ 本条红。</para></summary>
        [Test]
        public void test_d4_contractAssembly_hasNoPreviewEntry()
        {
            Assembly telemetry = typeof(ITelemetrySink).Assembly;
            Assert.That(telemetry.GetName().Name, Is.EqualTo("Telemetry"));
            var offenders = telemetry.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                .Where(m => m.Name.Contains("Preview") || m.Name.Contains("DebugView"))
                .Select(m => m.DeclaringType!.Name + "." + m.Name)
                .ToList();
            Assert.That(offenders, Is.Empty,
                "51 契约程序集不得承载调试视图入口(它会被**所有**构建编译):\n" + string.Join("\n", offenders));
        }

        /// <summary>AC-51-D4 的**未覆盖半边** —— Release player 构建里入口不存在:本机只跑 EditMode
        /// (编译宏含 `UNITY_EDITOR`)⇒ **NOT-RUN**,不记绿。</summary>
        [Test]
        public void test_d4_releasePlayer_entryAbsent_NOT_RUN()
        {
            Assert.Ignore("AC-51-D4 的 Release 半边 NOT-RUN:须 Release player 构建后反射扫描(本机仅 EditMode,宏含 UNITY_EDITOR);不得以源码形态判据代替");
        }

        /// <summary>AC-51-D4 的**另一半** —— `GIVEN Development 构建,THEN 入口存在且输出指标`。
        /// <para>TD-epic-01 第十三片(2026-10-19)补:此前只钉了「Release 侧被条件编译包住」,
        /// 没钉「开发期这半边**确实活着**」—— 若 `#if` 条件写成永假,Release 侧判据照样全绿,
        /// 而开发期入口静默消失。</para>
        /// <para><b>判别力</b>:把条件改成 `#if false` ⇒ 本夹具编译失败(类型不存在)⇒ 红;
        /// 把 `RenderDeveloperPreview` 改成不调 sink ⇒ 第二条断言红;
        /// 把 `AllMetricsReport` 从正文里拿掉 ⇒ 第三条红。</para></summary>
        [Test]
        public void test_d4_developmentBuild_entryExists_andOutputsMetrics()
        {
            // Arrange:本夹具程序集编在 Editor ⇒ 宏含 UNITY_EDITOR,入口在编译面内。
            //          这里用**反射**取类型,而不是直接 `new` —— 若类型真被剔除,
            //          拿到的是「找不到类型」而非编译错误,红的理由更可读。
            Type view = typeof(TelemetryReporter).Assembly.GetType(
                "DaYiJingCheng.Gameplay.Boot.TelemetryDeveloperView", throwOnError: false);
            Assert.That(view, Is.Not.Null,
                "Development/Editor 构建里 TelemetryDeveloperView 必须存在(AC-51-D4 开发期半边)");

            // ⚠️ 形参是 `in TelemetryPreviewRequest` ⇒ 反射面是 `TelemetryPreviewRequest&`(ByRef),
            //    按值类型取会拿不到(首轮实测即此)。
            MethodInfo preview = typeof(TelemetryReporter).GetMethod(
                "RenderDeveloperPreview",
                new[] { typeof(TelemetryPreviewRequest).MakeByRefType(), typeof(ITelemetrySink) });
            Assert.That(preview, Is.Not.Null,
                "Development/Editor 构建里 RenderDeveloperPreview 入口必须存在");

            AppendCaseClosed(1L, 1UL << 0);
            AppendJudgment(1L, 1);
            var sink = new RecordingSink();

            // Act
            string text = _reporter.RenderDeveloperPreview(
                new TelemetryPreviewRequest("1", (0, 10), 0, 5, 5, 3, null), sink);

            // Assert:入口真活着且**输出指标**(不是「有入口但点了没用」)
            Assert.That(sink.Logged, Has.Count.EqualTo(1), "开发期入口须真把正文交给 sink");
            StringAssert.Contains("# F1 判断层指标", text, "开发期入口须输出真实指标段");
            Assert.That(text.Length, Is.GreaterThan(100), "输出不得为空壳");
        }

        // ══════════════ 入口行为 ══════════════

        /// <summary>入口把正文交 sink(ADR-019 §五「控制台 / 本地文件」),且正文含各段标题。</summary>
        [Test]
        public void test_entry_writesToSink_withAllSections()
        {
            AppendCaseClosed(1L, 1UL << 0);
            AppendJudgment(1L, 1);
            var sink = new RecordingSink();

            string text = _reporter.RenderDeveloperPreview(
                new TelemetryPreviewRequest("1", (0, 10), 0, 5, 5, 3, null), sink);

            Assert.That(sink.Logged, Has.Count.EqualTo(1), "须恰好交给 sink 一次");
            Assert.That(text.Split('\n').Count(l => l.StartsWith("# 流事件")), Is.EqualTo(1),
                "完整性头只许出现一次(重复头部 = 两份口径并排)");
            Assert.That(text.Split('\n').Count(l => l.Contains("部分指标")), Is.LessThanOrEqualTo(1));
            Assert.That(sink.Logged[0], Is.EqualTo(text));
            StringAssert.Contains("# F1 判断层指标", text);
            StringAssert.Contains("# F2 误诊分布", text);
            StringAssert.Contains("# F3 跳过率", text);
            StringAssert.Contains("# F4 熟练度成长", text);
            StringAssert.Contains("F5 History 密度序列", text);
            StringAssert.Contains("F6 SessionSpan", text);
        }

        /// <summary>**参数纪律**:没给的旋钮 ⇒ 正文显式「未给(未算)」,**不代填默认值**。
        [Test]
        public void test_notGiven_params_areStatedNotDefaulted()
        {
            AppendCaseClosed(1L, 1UL << 0);
            AppendJudgment(1L, 1);

            string text = _reporter.RenderDeveloperPreview(new TelemetryPreviewRequest(
                skill: null, growthWindow: null, firstWindowTick: null, step: null,
                window: null, gapThreshold: null, minSample: null), new RecordingSink());

            StringAssert.Contains("# F4 未算:技能筛键 — 未给", text);
            StringAssert.Contains("# F5 未算:t_0 / S / W — 未给", text);
            StringAssert.Contains("# F6/F7 未算:GAP_THRESHOLD — 未给", text);
            Assert.That(text, Does.Not.Contain("F5 History 密度序列"), "未给参数不得凭空算 F5");
            Assert.That(text, Does.Not.Contain("F6 SessionSpan"), "未给 GAP_THRESHOLD 时 F6/F7 整段不算");
        }

        // ══════════════ D8 · 空流 / 部分流显式标注 ══════════════

        /// <summary>AC-51-D1/D8:空流 ⇒ 头部显式「空流(零值合法)」,**不**把空输入读成测得 0。
        /// <para>判别力:去掉 banner ⇒ 本条红。</para></summary>
        [Test]
        public void test_d8_emptyStream_bannerSaysZeroIsLegit()
        {
            string text = _reporter.RenderDeveloperPreview(default, new RecordingSink());

            StringAssert.Contains("空流", text);
            StringAssert.Contains("Computed,非 Faulted", text);
            Assert.That(text, Does.Not.Contain("部分指标"), "空流 ≠ 部分指标(两者不同状态)");
        }

        /// <summary>AC-51-D8:流非空但**无 Scorable 病例** ⇒ 头部显式「部分指标」,不静默出数。
        /// <para>判别力:去掉 partial 判据 ⇒ 本条红。</para></summary>
        [Test]
        public void test_d8_nonEmptyWithoutScorable_flagsPartial()
        {
            AppendJudgment(1L, 1);      // 有判断、无 CaseClosed ⇒ 不可打分

            string text = _reporter.RenderDeveloperPreview(default, new RecordingSink());

            StringAssert.Contains("部分指标", text);
            StringAssert.Contains("无 Scorable 病例", text);
            Assert.That(text, Does.Not.Contain("空流"), "非空流不得自称空流");
        }

        // ══════════════ D9 · 样本不足:只加标注,数值不动 ══════════════

        /// <summary>AC-51-D9:样本量 &lt; `TELEMETRY_MIN_SAMPLE` ⇒ 正文**多一条标注**,
        /// 且数值字段**逐字不变**(不隐藏、不四舍五入掩盖)。
        /// <para>判别力:把「样本不足」实现成「数值打折 / 改报不可定义」⇒ 两版正文的 F1 行不同 ⇒ 本条红。</para></summary>
        [Test]
        public void test_d9_lowSample_addsBannerNumbersUnchanged()
        {
            AppendCaseClosed(1L, 1UL << 0);
            AppendJudgment(1L, 1);

            string withMin = _reporter.RenderDeveloperPreview(
                new TelemetryPreviewRequest(null, null, null, null, null, null, minSample: 99), new RecordingSink());
            string without = _reporter.RenderDeveloperPreview(default, new RecordingSink());

            StringAssert.Contains("样本不足", withMin);
            StringAssert.Contains("TELEMETRY_MIN_SAMPLE = 99", withMin);
            Assert.That(without, Does.Not.Contain("样本不足"), "未给门槛则不加标注");

            string f1 = withMin.Split('\n').First(l => l.StartsWith("F1a 判断准确率"));
            string f1b = without.Split('\n').First(l => l.StartsWith("F1a 判断准确率"));
            Assert.That(f1, Is.EqualTo(f1b), "样本不足只加标注,数值字段须逐字相同");
        }

        /// <summary>AC-51-D9 负向:样本量 ≥ 门槛 ⇒ 不加标注。</summary>
        [Test]
        public void test_d9_ampleSmpale_noBanner()
        {
            AppendCaseClosed(1L, 1UL << 0);
            AppendJudgment(1L, 1);

            string text = _reporter.RenderDeveloperPreview(
                new TelemetryPreviewRequest(null, null, null, null, null, null, minSample: 1), new RecordingSink());

            Assert.That(text, Does.Not.Contain("样本不足"), "样本量达标不得加「样本不足」");
        }

        // ══════════════ AC-51-A5 / E1 · 零呈现面 ══════════════

        /// <summary>AC-51-A5:51 侧**零呈现程序集引用**(无 UI Toolkit / UGUI / 42 / 44),且 asmdef
        /// `noEngineReferences: true` —— 51 靠「不呈现」而非脱敏。
        /// <para>判别力:给 `Telemetry` 加引擎引用 / 给 Boot 调试视图引 42 ⇒ 本条红。</para></summary>
        [Test]
        public void test_a5_telemetryAssembly_hasNoPresentationReferences()
        {
            string asmdefPath = Path.Combine(RepoRoot(), "Assets", "Telemetry", "Telemetry.asmdef");
            Assert.That(File.Exists(asmdefPath), Is.True, "Telemetry.asmdef 须存在(排除路径写错导致的空判)");
            string asmdef = File.ReadAllText(asmdefPath);
            StringAssert.Contains("\"noEngineReferences\": true", asmdef);

            string[] forbidden = { "UnityEngine.UI", "UnityEditor.UI", "Gameplay.UI", "Gameplay.Presentation", "Audio" };
            IReadOnlyList<string> refs = typeof(ITelemetrySink).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty).ToList();
            Assert.That(refs, Is.Not.Empty, "引用集读取不得为空(空集合会让断言恒真)");
            var bad = refs.Where(n => forbidden.Any(f => n.Contains(f))).ToList();
            Assert.That(bad, Is.Empty, "51 契约程序集不得引用呈现层:\n" + string.Join("\n", bad));
        }

        /// <summary>开发期默认预览(`RenderDeveloperSummary`,只收 sink)与显式入口同口径 ——
        /// 参数未给 ⇒ 三项「未给(未算)」齐,**不**因「默认入口」而代填。
        /// <para>判别力:默认入口代填任一旋钮 ⇒ 本条红(`M-dv-8`)。</para></summary>
        [Test]
        public void test_summaryEntry_defaultParamsAreAllNotGiven()
        {
            AppendCaseClosed(1L, 1UL << 0);
            AppendJudgment(1L, 1);
            var sink = new RecordingSink();

            string text = _reporter.RenderDeveloperSummary(sink);

            Assert.That(sink.Logged, Has.Count.EqualTo(1));
            Assert.That(sink.Logged[0], Is.EqualTo(text));
            StringAssert.Contains("# F4 未算:技能筛键 — 未给", text);
            StringAssert.Contains("# F5 未算:t_0 / S / W — 未给", text);
            StringAssert.Contains("# F6/F7 未算:GAP_THRESHOLD — 未给", text);
        }

        /// <summary>AC-51-E1:51 侧零 `.uxml` / `.uss` / Canvas 资产,零图表 —— 调试视图是纯文本。
        /// <para>判别力:往 `Assets/Telemetry/` 或调试视图文件加 UI 资产引用 ⇒ 本条红。</para></summary>
        [Test]
        public void test_e1_noPresentationAssets()
        {
            string telemetryDir = Path.Combine(RepoRoot(), "Assets", "Telemetry");
            string[] hits = Directory.GetFiles(telemetryDir, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".uss", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.That(hits, Is.Empty, "51 侧不得有呈现资产:\n" + string.Join("\n", hits));

            // 只扫**代码面**(去注释行)—— 注释里点名「Canvas」是文档,不是呈现依赖
            string view = CodeOnly(BootSource("TelemetryDeveloperView.cs"));
            foreach (string token in new[] { "UnityEngine.UIElements", "UIElements", "Canvas", "UnityEngine.UI." })
                Assert.That(view, Does.Not.Contain(token), "调试视图须纯文本,不得出现 " + token);
        }
    }
}
