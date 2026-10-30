// 权威来源:production/epics/telemetry-analytics/story-008-pure-function-recomputable.md
//   (AC-51-C1…C4 — 纯函数与可复算:同流两次重算逐位相同 / 单一折叠函数两入口同值
//    / 可进 ADR-12 黄金夹具(ADVISORY) / 不依赖墙钟帧序执行次数)
//   · R8:每条指标 = Fold(E) —— 不读墙钟/帧序/执行次数;同流同值
//   · R9:两种入口共用同一个折叠函数 —— 增量边读边折终值 == 离线整表折叠终值
//   · R11:指标定义必须写成可复算的谓词
// ADR-019 §一(回放即数据记录)· ADR-012(黄金夹具 + ROUND_HALF_AWAY_FROM_ZERO)
// ADR-006 §五(Fix 编码器 + 守恒律)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/telemetry/pure_function_recomputable_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 001-007 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ **2026-10-19 第十六片:夹具对象换锚(第二次假绿修复)**
//
// 原 C1/C2 打的是生产类 `FoldEngine` —— 它由第十五片「逐字上移」而来,但上移后实测
// **全仓零生产调用点**(只有本文件引用)⇒ 断言跑在一个**没人调用的类**上,而真正出数的
// `TelemetryReporter.JudgmentReport → JudgmentStreamReducer.Reduce → JudgmentMetrics.Compute
// → MetricsReport.Render` 一条都没测。这与第一节 `TickFoldEngine` 的病灶完全同型。
//
// 现口径:**夹具沿真实报告链跑** —— 真 `EventStream` + 真 `PayloadEncoder` / `PayloadDecoder`
// (载荷真入池真解出)+ 真 `TelemetryReporter`。`FoldEngine` 已删除。
// 两入口按 R9 原义取:① 局内增量(同一会话连续两次取数)② 离线整表(新建 reporter 从同一流重算)。
// ═══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI; // PresenceRegistry(EventStream 构造需要)
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class PureFunctionRecomputableTest
    {
        // ══════════════ AC-51-C1:同流两次重算逐位相同 ══════════════

        /// <summary>AC-51-C1:同流两次重算逐位相同 ——
        /// 在同一条真事件流上连续重算两次,报告正文(U+0000 起)与全部标量导出逐位相同。
        /// <para><b>生产主语</b>:<see cref="TelemetryReporter.JudgmentReport"/> 全链路
        /// (reducer 还原 → 整数对求值 → 定点渲染)。</para>
        /// <para><b>判别力</b>:任一环引入可变状态(跨调用缓存 / 累积计数 / 字典迭代序漏排)
        /// ⇒ 第二次与第一次不等 ⇒ 本条红。</para></summary>
        [Test]
        public void test_c1_sameStreamTwiceBitwiseIdentical()
        {
            // Arrange:真流 —— 三条判断(一案判对 / 一案判错 / 一案改写后判对)
            EventStream stream = BuildStream(out InMemoryBlobPool pool);

            // Act
            string first = TextReporter.Perform(stream, pool, out int skipped1);
            string second = TextReporter.Perform(stream, pool, out int skipped2);

            // Assert:正文逐位相同(序数比较,不做文化相关归一)
            Assert.That(second, Is.EqualTo(first), "同流两次重算,报告正文须逐位相同(char 序逐位)");
            Assert.That(skipped2, Is.EqualTo(skipped1), "跳过计数两次相同");

            // Assert:黄金值 —— 不仅测「两次相同」还测「值正确」
            //   真值:{a}={1}, {b}={2}, {c}={1}
            //   落笔:a → {1}(对) · b → {3}(错) · c → {2}(错,改写为 {1} 后对)
            Assert.That(skipped1, Is.EqualTo(0), "载荷全部真驻留真解出 ⇒ 零跳过");
            Assert.That(first, Does.Contain("= 2 / 3"), "F1a = 2/3(a、c 判对;b 判错)");
            Assert.That(first, Does.Contain("= 1 / 3"), "F1b = 1/3(仅 c 有改写史)");
        }

        /// <summary>AC-51-C1(标量导出半边):Q16.16 标量导出走整数域且两次相同。
        /// <para><b>生产主语</b>:<see cref="MetricsReport.ToQ1616"/> + <see cref="MetricsReport.RoundHalfAwayFromZero"/>。</para>
        /// <para><b>判别力</b>:把 `ToQ1616` 改成浮点中转 / ties-to-even ⇒ 本条红(2/3 与 1/3 的
        /// Q16.16 在 ties 边界上分叉)。</para></summary>
        [Test]
        public void test_c1_q1616ScalarExport_bitwiseIdenticalTwice()
        {
            // Arrange:真实求值得到的 (num, den)
            EventStream stream = BuildStream(out InMemoryBlobPool pool);
            List<JudgmentRecord> records = Reduce(stream, pool);
            JudgmentMetrics.Result r1 = JudgmentMetrics.Compute(records);
            JudgmentMetrics.Result r2 = JudgmentMetrics.Compute(records);

            // Act
            long a1 = MetricsReport.ToQ1616(r1.F1aNum, r1.F1aDen, out bool zero1);
            long a2 = MetricsReport.ToQ1616(r2.F1aNum, r2.F1aDen, out bool zero2);
            long b1 = MetricsReport.ToQ1616(r1.F1bNum, r1.F1bDen, out _);

            // Assert:两次逐位相同
            Assert.That(a2, Is.EqualTo(a1), "F1a 的 Q16.16 导出两次逐位相同");
            Assert.That(zero2, Is.EqualTo(zero1));

            // 诊断:暴露真实求值输入(1/1 表示流只还原出一条可打分记录 —— 入流或去重有问题)
            Assert.That(records.Count, Is.EqualTo(3),
                $"Scorable 病例数应为 3,实为 {records.Count};样本:" + Describe(records));

            // Assert:黄金值 —— 2/3 × 65536 = 43690.66… ⇒ ROUND_HALF_AWAY_FROM_ZERO = 43691
            Assert.That(a1, Is.EqualTo(43691L), "ToQ16.16(2,3) = 43691");
            Assert.That(b1, Is.EqualTo(21845L), "ToQ16.16(1,3) = 21845(21845.33…)");
        }

        // ══════════════ AC-51-C2:单一折叠函数(两入口同值) ══════════════

        /// <summary>AC-51-C2:单一折叠函数(两入口同值)——
        /// ① 局内增量(同一会话连续两次取数)② 离线整表(新会话从同一流重算)⇒ 终值逐位相同,
        /// 且 51 无跨调用持久状态(<see cref="TelemetryReporter"/> 的全部实例字段均为 `IsInitOnly`)。
        /// <para><b>生产主语</b>:<see cref="TelemetryReporter.JudgmentReport"/> 单一出数路径 +
        /// <see cref="JudgmentStreamReducer.Reduce"/>(无排序权 —— 不重排 = 不另立第二真源)。</para>
        /// <para><b>判别力</b>:若为「增量入口」另抄一份折叠 ⇒ 两入口随调用次序分叉 ⇒ 本条红。</para></summary>
        [Test]
        public void test_c2_twoEntriesSingleFold_sameValue_noPersistentState()
        {
            // Arrange:真流
            EventStream stream = BuildStream(out InMemoryBlobPool pool);

            // Act ①:局内增量 —— 同一 reporter 连续取两次
            var live = NewReporter(stream, pool);
            string inc1 = live.JudgmentReport(out int s1);
            string inc2 = live.JudgmentReport(out int s2);

            // Act ②:离线整表 —— 新 reporter(新 decoder / 新会话)从同一流重算
            var offline = NewReporter(stream, pool);
            string off = offline.JudgmentReport(out int s3);

            // Assert:两入口终值逐位相同
            Assert.That(inc2, Is.EqualTo(inc1), "局内增量:同会话第二次取数 == 第一次(无跨调用持久状态)");
            Assert.That(off, Is.EqualTo(inc1), "离线整表 == 局内增量(R9:同一折叠函数)");
            Assert.That(s3, Is.EqualTo(s1));
            Assert.That(s2, Is.EqualTo(s1));

            // Assert:无跨调用持久状态 —— reporter 的全部实例字段都是 readonly
            FieldInfo[] fields = typeof(TelemetryReporter)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(fields, Is.Not.Empty, "扫描面为空 ⇒ 判据无效");
            var mutable = fields
                .Where(f => !f.IsInitOnly && !f.IsLiteral)
                .Select(f => f.Name)
                .ToList();
            Assert.That(mutable, Is.Empty,
                "51 的出数入口不得有可写实例字段(跨调用持久状态即 R8/R9 违例):" + string.Join(", ", mutable));
        }

        // ══════════════ AC-51-C4:不依赖墙钟/帧序/执行次数 ══════════════

        /// <summary>AC-51-C4:不依赖墙钟/帧序/执行次数 ——
        /// 51 命名空间下无 DateTime.Now/Environment.TickCount/Stopwatch/Time.*/UnityEngine.Random。</summary>
        [Test]
        public void test_c4_noWallClockOrFrameDependencies()
        {
            // Arrange:扫描 51 的全部面类型 —— 命名空间 + 报告生产者
            // ⚠️ 2026-10-19 第十六片:原只扫 `DaYiJingCheng.Telemetry`,而报告生产者住
            //    `Gameplay.Boot` ⇒ 墙钟依赖可能藏在真正的出数路径里而不被发现
            //    (与 E1/E2/E4/E5 同一个坑)。
            var telemetryTypes = TypesInAssemblyContaining(typeof(TelemetryReporter), "DaYiJingCheng.Telemetry");
            var producerNames = new[]
            {
                "DaYiJingCheng.Gameplay.Boot.TelemetryReporter",
                "DaYiJingCheng.Gameplay.Boot.TelemetryDeveloperView",
            };
            foreach (string producer in producerNames)
            {
                Type t = AllTypes().FirstOrDefault(x => x.FullName == producer);
                Assert.That(t, Is.Not.Null,
                    $"报告生产者 {producer} 不在程序集里 —— 扫描面失效(改名 / 移出?)");
                if (!telemetryTypes.Contains(t!)) telemetryTypes.Add(t!);
            }
            Assert.That(telemetryTypes.Count, Is.GreaterThan(0), "扫描集为空 ⇒ 判据无效");

            // Act:扫描方法名与字段名中的禁用关键词
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
            {
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    if (m.Name.Contains("DateTime") || m.Name.Contains("Now") ||
                        m.Name.Contains("TickCount") || m.Name.Contains("Stopwatch") ||
                        m.Name.Contains("Time") || m.Name.Contains("Random"))
                        violations.Add($"{t.Name}.{m.Name}");
                }
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Instance | BindingFlags.Static |
                                             BindingFlags.DeclaredOnly))
                {
                    if (f.Name.Contains("DateTime") || f.Name.Contains("Now") ||
                        f.Name.Contains("TickCount") || f.Name.Contains("Stopwatch") ||
                        f.Name.Contains("Time") || f.Name.Contains("Random"))
                        violations.Add($"{t.Name}.{f.Name}");
                }
            }

            // Assert:零违例
            Assert.That(violations, Is.Empty,
                "51 不得依赖 DateTime.Now/Environment.TickCount/Stopwatch/Time.*/UnityEngine.Random:\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-51-C4 补充:同一流在两次不同「执行上下文」下重算 ⇒ 逐位相同。
        /// 用两条**独立构造**的等价事件流(不同对象、不同 blob 池、不同 reporter)验证不依赖外部状态。
        /// <para><b>判别力</b>:折叠里读入任何外部量(池序号 / 静态计数)⇒ 两条流分叉 ⇒ 本条红。</para></summary>
        [Test]
        public void test_c4_differentContextsBitwiseIdentical()
        {
            // Arrange:两条独立构造的等价流
            EventStream streamA = BuildStream(out InMemoryBlobPool poolA);
            EventStream streamB = BuildStream(out InMemoryBlobPool poolB);

            // Act
            string a = NewReporter(streamA, poolA).JudgmentReport(out int sA);
            string b = NewReporter(streamB, poolB).JudgmentReport(out int sB);

            // Assert:逐位相同(不依赖外部状态)
            Assert.That(b, Is.EqualTo(a), "两条等价流 ⇒ 报告逐位相同");
            Assert.That(sB, Is.EqualTo(sA));
        }

        // ══════════════ 夹具 ══════════════

        /// <summary>诊断用:把还原记录压成一行(仅失败信息里用)。</summary>
        private static string Describe(IReadOnlyList<JudgmentRecord> recs)
            => string.Join(" ; ", recs.Select(r =>
                $"case={r.CaseId} truth={{{string.Join(",", r.Truth.OrderBy(x => x, StringComparer.Ordinal))}}}" +
                $" judged={{{string.Join(",", r.Judged.OrderBy(x => x, StringComparer.Ordinal))}}}" +
                $" rev={r.HasRevision}"));

        /// <summary>真装配:真 `EventStream` + 真 `PayloadDecoder` + 单病种注册表。</summary>
        private static TelemetryReporter NewReporter(EventStream stream, InMemoryBlobPool pool)
            => new TelemetryReporter(stream, new PayloadDecoder(pool),
                new[] { new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 1, DiseaseKey = "d1" } });

        /// <summary>报告入口的转发(让 C1 的两行 Act 读起来对称)。</summary>
        private static class TextReporter
        {
            public static string Perform(EventStream stream, InMemoryBlobPool pool, out int skipped)
                => NewReporter(stream, pool).JudgmentReport(out skipped);
        }

        /// <summary>沿生产链取还原记录(真解码,不经 reporter 的私有路径)。</summary>
        private static List<JudgmentRecord> Reduce(EventStream stream, InMemoryBlobPool pool)
        {
            var decoder = new PayloadDecoder(pool);
            bool Decode(PayloadRef r, EventKind k,
                        out JudgmentStreamReducer.DecodedJudgment d,
                        out DecodeFailureKind why)
            {
                why = DecodeFailureKind.None;
                try
                {
                    if (k == EventKind.CaseClosed)
                    {
                        CaseClosedPayload p = decoder.Decode<CaseClosedPayload>(k, r);
                        var ids = new List<int>();
                        for (int bit = 0; bit < 64; bit++)
                            if ((p.DiseaseSet.Bits & (1UL << bit)) != 0UL) ids.Add(bit + 1);
                        d = new JudgmentStreamReducer.DecodedJudgment(checked((int)p.CaseId.Seq), ids, false);
                        return true;
                    }
                    if (k == EventKind.JudgmentRecorded || k == EventKind.JudgmentRevised)
                    {
                        // 两个 Kind 载荷类型不同(且 `Decode<T>` 带类型校验,错配必抛)——
                        // 与 `TelemetryReporter.DecodeJudgmentRecord` 同一分派口径。
                        int caseSeq;
                        int lexicon;
                        if (k == EventKind.JudgmentRevised)
                        {
                            JudgmentRevisedPayload p = decoder.Decode<JudgmentRevisedPayload>(k, r);
                            caseSeq = checked((int)p.CaseId.Seq);
                            lexicon = p.LexiconId;
                        }
                        else
                        {
                            JudgmentRecordedPayload p = decoder.Decode<JudgmentRecordedPayload>(k, r);
                            caseSeq = checked((int)p.CaseId.Seq);
                            lexicon = p.LexiconId;
                        }
                        d = new JudgmentStreamReducer.DecodedJudgment(
                            caseSeq, new List<int>(1) { lexicon }, k == EventKind.JudgmentRevised);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    // 与 TelemetryReporter.DecodeJudgmentRecord 同口径:不可用 ⇒ 跳过并分类。
                    why = DecodeFailure.Classify(ex);
                }
                d = default;
                return false;
            }

            IReadOnlyList<JudgmentRecord> recs =
                JudgmentStreamReducer.Reduce(stream.Events, Decode, out SkipBreakdown _);
            return recs.ToList();
        }

        /// <summary>真事件流:三条判断(判对 / 判错 / 改写后判对),载荷真编码入池。
        /// <para>病例流口径见 ADR-008:一案一条 `CaseClosed`(真值)+ 若干 `Judgment*`(落笔)。</para></summary>
        private static EventStream BuildStream(out InMemoryBlobPool pool)
        {
            pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var stream = new EventStream(new SeqIdAuthority(), new PresenceRegistry());
            Assert.That(stream.Count, Is.EqualTo(0), "前置:新流为空");
            // ⚠️ 每条事件独立成 blob(池按 Store 序发号)⇒ 去重键(含 BlobId)必然互异。

            // 案 a:真值 {1},落笔 {1} ⇒ 判对
            // (掩码 bit (disease−1):病种 1 ⇒ bit 0。见 Append 里关于位位置的口径注。)
            Append(stream, encoder, 10L, EventKind.CaseClosed,
                new CaseClosedPayload(1, new CaseId(1, 1, 1), new DiseaseIdSet(1UL << 0), true));
            AppendJudgment(stream, encoder, 11L, EventKind.JudgmentRecorded,
                new JudgmentRecordedPayload(1, new CaseId(1, 1, 1), 7, 1, 2));

            // 案 b:真值 {2},落笔 {3} ⇒ 判错
            Append(stream, encoder, 20L, EventKind.CaseClosed,
                new CaseClosedPayload(2, new CaseId(2, 1, 2), new DiseaseIdSet(1UL << 1), true));
            AppendJudgment(stream, encoder, 21L, EventKind.JudgmentRecorded,
                new JudgmentRecordedPayload(2, new CaseId(2, 1, 2), 7, 3, 2));

            // 案 c:真值 {1},先落 {2}(错)再改写 {1} ⇒ 全序最后一条判对,且有改写史
            Append(stream, encoder, 30L, EventKind.CaseClosed,
                new CaseClosedPayload(3, new CaseId(3, 1, 3), new DiseaseIdSet(1UL << 0), true));
            AppendJudgment(stream, encoder, 31L, EventKind.JudgmentRecorded,
                new JudgmentRecordedPayload(3, new CaseId(3, 1, 3), 7, 2, 2));
            AppendJudgment(stream, encoder, 32L, EventKind.JudgmentRevised,
                new JudgmentRecordedPayload(3, new CaseId(3, 1, 3), 7, 1, 3));

            return stream;
        }

        /// <summary>判断族事件的编码要走**具名重载**(ADR-006 G-3:载荷另带 `freehandText`
        /// 变长段,不在 `T` 里 ⇒ 泛型 `Encode&lt;T&gt;` 刻意不承载,会抛)。
        /// 这也是写者侧的真实形状 —— 夹具不得绕过它。</summary>
        private static void AppendJudgment(EventStream stream, PayloadEncoder encoder,
                                           long tick, EventKind kind,
                                           JudgmentRecordedPayload p)
        {
            PayloadRef r;
            if (kind == EventKind.JudgmentRevised)
            {
                // 改写是**独立载荷类型**(JudgmentRevisedPayload · ADR-008 §三 存新值)
                var p2 = new JudgmentRevisedPayload(p.PatientId, p.CaseId, p.AuthorPlayerId,
                    p.LexiconId, p.Confidence);
                r = encoder.EncodeJudgmentRevised(in p2, string.Empty);
            }
            else
            {
                r = encoder.EncodeJudgmentRecorded(in p, string.Empty);
            }
            int before = stream.Count;
            stream.Append(new SimEvent(tick, new PatientId(p.PatientId), -1L, kind, r));
            Assert.That(stream.Count, Is.EqualTo(before + 1),
                $"判断事件未入流(被去重拒收):tick={tick} kind={kind}");
        }

        private static void Append<TPayload>(EventStream stream, PayloadEncoder encoder,
                                             long tick, EventKind kind, TPayload payload)
            where TPayload : struct
        {
            PayloadRef r = encoder.Encode(kind, payload);
            int patient = ((CaseClosedPayload)(object)payload).PatientId;
            // Seq 传 -1 = 「未发号,交主机发放」(SimEvent.cs 哨兵口径;禁手填 0 冒充首号)
            // ⚠️ 疾病掩码**必须**走 `1UL << (CaseId.Seq - 1)`:位序契约是
            //    `bit index = DiseaseId − 1`(见 `Sim.Contracts/Payloads/PayloadCommon.cs`
            //    的 `DiseaseIdSet` 注释块),而 `TelemetryReporter.DecodeJudgmentRecord`
            //    把 bit index 还原成 `DiseaseId = bit + 1` ⇒ `1UL << 0` 对应 DiseaseId 1。
            //    夹具若自定 bit 位置,真值集合就会与判笔集合落在不同编号空间上,判对/判错全错位。
            //
            // 🔁 2026-10-22 订正:原文此处写「`PatternDetector.DiseaseOrdinalsOf` 同口径」——
            //    **那句是错的**。`PatternDetector.DiseaseBitIndicesOf`(原名 `DiseaseOrdinalsOf`)
            //    返回 **bit index,刻意不加 1**;`TelemetryReporter` 才做 `+1`。
            //    两者不是「同口径」,是「同一契约的两端」。这正是「ordinal」一词被停用的理由。
            int before = stream.Count;
            stream.Append(new SimEvent(tick, new PatientId(patient), -1L, kind, r));
            Assert.That(stream.Count, Is.EqualTo(before + 1),
                $"事件未入流(被去重拒收):tick={tick} kind={kind}");
        }

        private static List<Type> AllTypes()
            => AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .ToList();

        /// <summary>取 `marker` 所在程序集里、命名空间以 `ns` 开头的全部类型。</summary>
        private static List<Type> TypesInAssemblyContaining(Type marker, string ns)
            => SafeTypes(marker.Assembly)
                .Where(t => t.Namespace != null && t.Namespace.StartsWith(ns, StringComparison.Ordinal))
                .ToList();

        private static IEnumerable<Type> SafeTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
        }

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private long _n = 100;
            public PatientId NextPatientId() => new PatientId((int)_n++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_n++);
        }
    }
}
