// TD-epic-01 第十二片 · 51 遥测 F4–F7 六旋钮的**烘焙管线 + 装载门 + 消费缝**夹具。
//
// 权威来源:
//   GDD `telemetry-analytics.md` §Tuning Knobs(旋钮落地位置 = assets/data/telemetry_analytics.json)
//   ADR-014 §二(同源两次烘焙逐位一致)· §三(两阶段烘焙)· §五(ConfigVersion 内容哈希 · E-13)
//   AC-51-B14(`W < 1` / `S < 1` ⇒ 构建期 **与** 加载期硬失败,非运行期退化、非静默取默认值)
//   AC-51-E5(无开关 ⇒ 白名单执法)
//
// ⚠️ **数值不拍**:本夹具只断言「JSON == 烘焙字节 == 读回值」三方同值 + 安全范围,
//    **不**断言任何绝对数值(值归用户数值轮;2026-10-19 用户批准了建议值,但仍是临时值)。
// ⚠️ 确定性:零随机 / 零墙钟 / 不写盘 —— sink 一律住内存。
// ⚠️ 判别力锚点(证伪轮逐一打):① 列序(i64 t_0 的**绝对偏移**)② 头槽 schemaVersion
//    ③ 加载期范围门(不是只有构建期)④ 白名单(AC-51-E5)⑤ B14 的范围谓词本身。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;   // PresenceRegistry(EventStream 的 AC-15 有界性依赖)
using DaYiJingCheng.Gameplay.Presentation.Telemetry;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    /// <summary>遥测旋钮「JSON → 烘焙 → 装载 → 取值缝」全链夹具(TD-epic-01 第十二片)。</summary>
    [TestFixture]
    public sealed class TelemetryAnalyticsWiringTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        // ══════════════ 真往返 + 三方同值 ══════════════

        /// <summary>真往返:写方字节 → 读方解码(含加载期门)→ 六个旋钮逐项等值 + 头契约。</summary>
        [Test]
        public void test_bake_materialize_roundTripExact()
        {
            TelemetryAnalyticsBake.BakeOutput output = TelemetryAnalyticsBake.BakeFromRepo(RepoRoot);
            TelemetryAnalyticsDataSet dataSet = TelemetryAnalyticsBake.Materialize(output);

            Assert.That(output.Cooked.Length, Is.EqualTo(CookedFormat.HeaderSize + TelemetryAnalyticsCookedCodec.PayloadBytes),
                "载荷 = u32 + i32×5 + i64 = 32 B");
            Assert.That(dataSet.SchemaVersion, Is.EqualTo(TelemetryAnalyticsCookedCodec.ExpectedSchemaVersion));
            Assert.That(dataSet.ConfigVersion, Is.EqualTo(output.ConfigVersion));
        }

        /// <summary>**JSON == 烘焙字节 == 读回值** 三方逐项同值(判别:任何一侧手改 ⇒ 红)。</summary>
        [Test]
        public void test_bake_boundEqualsReloaded_sixKnobs()
        {
            TelemetryAnalyticsBake.BakeOutput output = TelemetryAnalyticsBake.BakeFromRepo(RepoRoot);
            TelemetryAnalyticsDataSet dataSet = TelemetryAnalyticsBake.Materialize(output);

            Assert.That(dataSet.Tuning.DifficultyCurve.WindowTicks, Is.EqualTo(output.Bound.DifficultyCurve.WindowTicks), "W");
            Assert.That(dataSet.Tuning.DifficultyCurve.StepTicks, Is.EqualTo(output.Bound.DifficultyCurve.StepTicks), "S");
            Assert.That(dataSet.Tuning.DifficultyCurve.FirstWindowTick, Is.EqualTo(output.Bound.DifficultyCurve.FirstWindowTick), "t_0");
            Assert.That(dataSet.Tuning.GapThresholdTicks, Is.EqualTo(output.Bound.GapThresholdTicks), "GAP");
            Assert.That(dataSet.Tuning.GrowthWindowTicks, Is.EqualTo(output.Bound.GrowthWindowTicks), "GROWTH");
            Assert.That(dataSet.Tuning.MinSample, Is.EqualTo(output.Bound.MinSample), "MIN_SAMPLE");
        }

        /// <summary>位置断言:载荷列序固定(W → S → t_0 → GAP → GROWTH → MIN_SAMPLE)。
        /// <para>⚠️ 只靠读方回读**测不出**写方列序对调(写读两侧同序换位 ⇒ 往返仍等值,除非
        /// 两字段恰好异值 —— W=600/S=300 是异值,但也只是「这一次」判别)。故按**绝对偏移**钉:
        /// t_0(i64)在 `HeaderSize + 4 + 4 + 4` = 32,当前为 0;W 在 `HeaderSize + 4` = 24。</para></summary>
        [Test]
        public void test_bake_payloadColumnOrder_byAbsoluteOffset()
        {
            TelemetryAnalyticsBake.BakeOutput output = TelemetryAnalyticsBake.BakeFromRepo(RepoRoot);

            int w = BitConverter.ToInt32(output.Cooked, CookedFormat.HeaderSize + 4);
            int s = BitConverter.ToInt32(output.Cooked, CookedFormat.HeaderSize + 8);
            long t0 = BitConverter.ToInt64(output.Cooked, CookedFormat.HeaderSize + 12);

            Assert.That(w, Is.EqualTo(output.Bound.DifficultyCurve.WindowTicks), "偏移 24 处必须是 W");
            Assert.That(s, Is.EqualTo(output.Bound.DifficultyCurve.StepTicks), "偏移 28 处必须是 S");
            Assert.That(t0, Is.EqualTo(output.Bound.DifficultyCurve.FirstWindowTick), "偏移 32 处必须是 t_0(i64)");
        }

        /// <summary>头槽 schemaVersion 不得硬写 0(承 DelayTableCookedWriter 的 R-1 教训)。</summary>
        [Test]
        public void test_bake_headerSchemaSlot_notZero_equalsContract()
        {
            TelemetryAnalyticsBake.BakeOutput output = TelemetryAnalyticsBake.BakeFromRepo(RepoRoot);
            uint headerSchema = ReadU32LE(output.Cooked, 8);
            Assert.That(headerSchema, Is.EqualTo(CookedFormat.SchemaVersion));
            Assert.That(headerSchema, Is.Not.EqualTo(0u), "硬写 0 是被既往轮次抓过的缺陷形态(R-1)");
        }

        /// <summary>同源两次烘焙**逐位一致**(ADR-014 §二)。</summary>
        [Test]
        public void test_bake_deterministic_sameSourceTwiceBitwiseEqual()
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot, TelemetryAnalyticsBake.DataDirName,
                TelemetryAnalyticsBake.SourceFileName));
            byte[] first = TelemetryAnalyticsBake.BakeFromJson(json).Cooked;
            byte[] second = TelemetryAnalyticsBake.BakeFromJson(json).Cooked;
            Assert.That(second, Is.EqualTo(first), "同源两次烘焙字节不等 ⇒ 编码路径含非确定性来源");
        }

        /// <summary>源内容改动 ⇒ ConfigVersion 变(内容哈希派生,ADR-014 §五)。</summary>
        [Test]
        public void test_bake_configVersion_followsContent()
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot, TelemetryAnalyticsBake.DataDirName,
                TelemetryAnalyticsBake.SourceFileName));
            uint before = TelemetryAnalyticsBake.BakeFromJson(json).ConfigVersion;
            uint after = TelemetryAnalyticsBake.BakeFromJson(json.Replace("\"window_ticks\": 600", "\"window_ticks\": 601")).ConfigVersion;
            Assert.That(after, Is.Not.EqualTo(before), "改源未改 ConfigVersion ⇒ 哈希没覆盖内容");
        }

        // ══════════════ AC-51-B14:构建期门 ══════════════

        /// <summary>AC-51-B14 构建期半边:`W = 0` ⇒ 烘焙硬失败,报文点名 W。</summary>
        [Test]
        public void test_binder_windowZero_buildTimeReject()
        {
            AssertBinderRejects(Mutate("window_ticks", 600, 0), "window_ticks");
        }

        /// <summary>AC-51-B14:`S = 0` ⇒ 烘焙硬失败。</summary>
        [Test]
        public void test_binder_stepZero_buildTimeReject()
        {
            AssertBinderRejects(Mutate("step_ticks", 300, 0), "step_ticks");
        }

        /// <summary>AC-51-B14 加载期半边:**绕过 binder** 直接改字节里的 W = 0 ⇒ 读方 E-13 拒收。
        /// <para>这是「构建期 / 加载期两半各有一道」的第二道 —— 只有加载期门存在时本测才绿。</para></summary>
        [Test]
        public void test_codec_windowZero_loadTimeReject()
        {
            TelemetryAnalyticsBake.BakeOutput output = TelemetryAnalyticsBake.BakeFromRepo(RepoRoot);
            byte[] cooked = (byte[])output.Cooked.Clone();
            WriteI32LE(cooked, CookedFormat.HeaderSize + 4, 0);   // W
            var ex = Assert.Throws<InvalidOperationException>(() => TelemetryAnalyticsCookedCodec.Read(cooked));
            StringAssert.Contains("[E-13][telemetry_analytics]", ex.Message);
            StringAssert.Contains("window_ticks", ex.Message, "加载期失败报文须点名越界旋钮");
        }

        /// <summary>其余四个旋钮的越界值同样在加载期被拒(逐项)。</summary>
        [Test]
        public void test_codec_otherOutOfRange_loadTimeReject()
        {
            TelemetryAnalyticsBake.BakeOutput output = TelemetryAnalyticsBake.BakeFromRepo(RepoRoot);

            AssertLoadRejects(output, CookedFormat.HeaderSize + 8, 0, "step_ticks");            // S = 0
            AssertLoadRejectsLong(output, CookedFormat.HeaderSize + 12, -1L, "first_window_tick"); // t_0 = -1
            AssertLoadRejects(output, CookedFormat.HeaderSize + 20, -1, "gap_threshold_ticks");
            AssertLoadRejects(output, CookedFormat.HeaderSize + 24, 0, "growth_window_ticks");
            AssertLoadRejects(output, CookedFormat.HeaderSize + 28, 0, "min_sample");
        }

        // ══════════════ AC-51-E5:白名单执法 ══════════════

        /// <summary>`enabled` 顶层键 ⇒ binder 拒收(白名单外,结构性执法 —— 不靠黑名单)。</summary>
        [Test]
        public void test_binder_switchField_rejectedByWhitelist()
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot, TelemetryAnalyticsBake.DataDirName,
                TelemetryAnalyticsBake.SourceFileName));
            string withSwitch = json.Replace("\"sample\":", "\"enabled\": true,\n  \"sample\":");
            AssertBinderRejects(withSwitch, "enabled");
        }

        /// <summary>组内未知键(`sample.sample_rate`)⇒ 拒收(嵌套白名单同款)。</summary>
        [Test]
        public void test_binder_nestedUnknownKey_rejected()
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot, TelemetryAnalyticsBake.DataDirName,
                TelemetryAnalyticsBake.SourceFileName));
            AssertBinderRejects(json.Replace("\"min_sample\": 30", "\"min_sample\": 30, \"sample_rate\": 1"), "sample_rate");
        }

        /// <summary>缺组 ⇒ 拒收(四组是 GDD 结构,不是可选)。</summary>
        [Test]
        public void test_binder_missingGroup_rejected()
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot, TelemetryAnalyticsBake.DataDirName,
                TelemetryAnalyticsBake.SourceFileName));
            int start = json.IndexOf("\"session_rhythm\"", StringComparison.Ordinal);
            int end = json.IndexOf("\"skill_growth\"", StringComparison.Ordinal);
            AssertBinderRejects(json.Remove(start, end - start), "session_rhythm");
        }

        // ══════════════ 消费缝 ══════════════

        /// <summary>取值缝:provider 非空 ⇒ 取烘焙表(真源)。</summary>
        [Test]
        public void test_resolveKnobs_withProvider_takesBakedTable()
        {
            var tuning = new TelemetryAnalyticsTuning(
                new DifficultyCurveTuning(111, 22, 33), 44, 555, 66);
            var provider = new SingleTableProvider(tuning);

            TelemetryAnalyticsTuning? resolved = BootRoot.ResolveTelemetryKnobs(provider);

            Assert.That(resolved.HasValue, Is.True);
            Assert.That(resolved.Value.DifficultyCurve.WindowTicks, Is.EqualTo(111));
            Assert.That(resolved.Value.DifficultyCurve.FirstWindowTick, Is.EqualTo(33L));
            Assert.That(resolved.Value.MinSample, Is.EqualTo(66));
        }

        /// <summary>取值缝:provider 为 null ⇒ **null**(不造常量兜底 —— 报告维持「未给」)。</summary>
        [Test]
        public void test_resolveKnobs_nullProvider_returnsNull_noConstantFallback()
        {
            Assert.That(BootRoot.ResolveTelemetryKnobs(null).HasValue, Is.False,
                "51 不替用户转旋钮:provider 缺失时不得凭空兜底默认值");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>调试视图默认预览:注入旋钮 ⇒ F5/F7 算出来(不再是「未给」)。</summary>
        [Test]
        public void test_developerSummary_withKnobs_computesF5AndF7()
        {
            TelemetryAnalyticsTuning tuning = TelemetryAnalyticsBake.BindFromRepo(RepoRoot);
            var reporter = new TelemetryReporter(NewEmptyStream(), NewInMemoryDecoder(),
                new List<DiseaseRegistryEntry>(), tuning);
            var sink = new CapturingSink();

            string text = reporter.RenderDeveloperSummary(sink);

            Assert.That(text, Does.Not.Contain("F5 未算"), "注入旋钮后 F5 必须被算(否则消费缝没接上)");
            Assert.That(text, Does.Not.Contain("F6/F7 未算"), "注入旋钮后 F6/F7 必须被算");
            Assert.That(sink.Last, Is.EqualTo(text), "写入 sink 的正文须与返回值一致");
        }

        /// <summary>未注入旋钮 ⇒ 维持「未给」形态(禁代填默认值)。</summary>
        [Test]
        public void test_developerSummary_withoutKnobs_keepsNotGiven()
        {
            var reporter = new TelemetryReporter(NewEmptyStream(), NewInMemoryDecoder(),
                new List<DiseaseRegistryEntry>());

            string text = reporter.RenderDeveloperSummary(new CapturingSink());

            Assert.That(text, Does.Contain("F5 未算"));
            Assert.That(text, Does.Contain("F6/F7 未算"));
        }
#endif

        // ══════════════════════════════ 辅助 ══════════════════════════════

        private static string Mutate(string key, int from, int to)
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot, TelemetryAnalyticsBake.DataDirName,
                TelemetryAnalyticsBake.SourceFileName));
            return json.Replace($"\"{key}\": {from}", $"\"{key}\": {to}");
        }

        private static void AssertBinderRejects(string json, string expectedFragment)
        {
            var ex = Assert.Throws<BakeValidationException>(() => TelemetryAnalyticsBinder.BindFromSourceText(json));
            StringAssert.Contains(expectedFragment, string.Join("\n", ex.Errors), "失败报文须点名具体旋钮 / 键(可诊断)");
        }

        private static void AssertLoadRejects(TelemetryAnalyticsBake.BakeOutput output, int offset, int value,
                                              string knobFragment)
        {
            byte[] cooked = (byte[])output.Cooked.Clone();
            WriteI32LE(cooked, offset, value);
            var ex = Assert.Throws<InvalidOperationException>(() => TelemetryAnalyticsCookedCodec.Read(cooked));
            StringAssert.Contains(knobFragment, ex.Message, $"加载期失败须点名 {knobFragment}");
        }

        private static void AssertLoadRejectsLong(TelemetryAnalyticsBake.BakeOutput output, int offset, long value,
                                                  string knobFragment)
        {
            byte[] cooked = (byte[])output.Cooked.Clone();
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, cooked, offset, 8);
            var ex = Assert.Throws<InvalidOperationException>(() => TelemetryAnalyticsCookedCodec.Read(cooked));
            StringAssert.Contains(knobFragment, ex.Message, $"加载期失败须点名 {knobFragment}");
        }

        private static EventStream NewEmptyStream() => new EventStream(new IdAuthority(), new PresenceRegistry());

        /// <summary>真空载荷解码器(池为空)。空流上 F4/F5/F7 本不解码任何载荷 —— 用真解码器
        /// 而不是会抛的替身:哪怕报告路径将来开始解码,本夹具也不会因此红成误导性的失败。</summary>
        private static IPayloadDecoder NewInMemoryDecoder() => new PayloadDecoder(new InMemoryBlobPool());

        private sealed class CapturingSink : ITelemetrySink
        {
            public string Last;
            public void LogDeveloperSummary(string text) => Last = text;
            public void WriteLocalReport(string fileName, string text) { }
        }

        private sealed class SingleTableProvider : IDataProvider
        {
            private readonly TelemetryAnalyticsDataSet _dataSet;
            public SingleTableProvider(in TelemetryAnalyticsTuning tuning)
            {
                _dataSet = new TelemetryAnalyticsDataSet(tuning, TelemetryAnalyticsCookedCodec.ExpectedSchemaVersion, 0);
            }

            public TDataSet Load<TDataSet>() where TDataSet : struct
            {
                object boxed = _dataSet;
                return (TDataSet)boxed;
            }
        }

        private static uint ReadU32LE(byte[] b, int offset)
            => (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        private static void WriteI32LE(byte[] b, int offset, int value)
        {
            b[offset] = (byte)(value & 0xFF);
            b[offset + 1] = (byte)((value >> 8) & 0xFF);
            b[offset + 2] = (byte)((value >> 16) & 0xFF);
            b[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
