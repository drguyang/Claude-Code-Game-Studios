// 权威来源:production/epics/telemetry-analytics/story-010-out-of-bounds-rejection.md
//   (AC-51-E1…E5 — 越界拒绝:无玩家可见统计界面 / 不写回数值 / 非主机侧拒绝 /
//    无因果字段 / 无开关)
//   · E1:无玩家可见统计界面 —— 不存在任何入口(菜单项/快捷键/控制台命令)与资产
//        (.uxml/.uss/Canvas prefab/图表)
//   · E2:不写回数值 —— 无写回 assets/data/** 的代码路径;schema 中无「建议值/proposed/
//        推荐改动」字段名;assets/data/** 在 51 运行前后字节不变
//   · E3:非主机侧拒绝 —— 非主机客户端请求 Compute() 时拒绝,不产出空/截断指标
//        (ADVISORY:依赖 OQ-51-5 未裁 ⇒ 现不可签核)
//   · E4:R10 报告无因果/归因字段 —— schema 中无 cause_of/attribution/recommendation
//        类字段与枚举(ADVISORY:正文层不可机器判定 = 人工走查)
//   · E5:无开关 —— assets/data/telemetry_analytics.json 的 schema 中无
//        enabled/sample_rate/endpoint/experiment/consent 字段
// ADR-019 §五(无玩家可见 UI)+ §六(不代调数值)· ADR-019 §四(隐私面结构性满足)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/telemetry/out_of_bounds_rejection_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具自持,不依赖文件系统/网络/数据库。
// ⚠️ 假绿防护(B1 修复):空命名空间 ⇒ Assert.Ignore(当前阶段正确行为);
//    非空命名空间 ⇒ 执行扫描断言(实现后有效)。
// ⚠️ QA 评审修复:E1 目录不存在 = 无资产 = 通过(非 Ignore);
//    E2 添加字节不变性测试;E2 扩展文件写入 API 检查;E5 直接验证 JSON 文件不存在。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DaYiJingCheng.Gameplay.Boot;      // TelemetryReporter(AC-51-E3 生产主语)
using DaYiJingCheng.Gameplay.PatientAI; // PresenceRegistry
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;           // ITelemetrySink
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class OutOfBoundsRejectionTest
    {
        // ══════════════ 基础设施 ══════════════

        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        /// <summary>获取 51 的全部面类型 —— **命名空间 + 报告生产者**。
        /// <para>⚠️ **2026-10-19 扫描面订正(第十六片)**:原实现只取
        /// <c>DaYiJingCheng.Telemetry</c> 命名空间,而 51 的**报告生产者**
        /// (<c>TelemetryReporter</c> / <c>TelemetryDeveloperView</c>)住在 <c>Gameplay.Boot</c>
        /// ⇒ <c>E1 / E2 / E4 / E5</c> 四条判据对真正的出数路径**视而不见**。
        /// 这是与 <c>AC-19-02</c>(第十三片)同一个坑 —— 当时只补了那一处,没回头补这四条。</para>
        /// <para>⚠️ 生产者按 **FullName 逐个点名**,不按整个 <c>Gameplay.Boot</c> 程序集取:
        /// 该程序集还住启动序 / 玩家搬运等非 51 类型,对它们套 51 的越界判据是**判据越权**。
        /// 生产者若改名 / 迁出,<see cref="RequireNonEmptyScan"/> 会先红 —— 不会静默失去扫描面。</para></summary>
        private static readonly string[] ProducerTypeNames =
        {
            "DaYiJingCheng.Gameplay.Boot.TelemetryReporter",
            "DaYiJingCheng.Gameplay.Boot.TelemetryDeveloperView",
        };

        private static List<Type> GetTelemetryTypes()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();

            foreach (string producer in ProducerTypeNames)
            {
                Type t = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .FirstOrDefault(x => x.FullName == producer);
                if (t != null && !all.Contains(t)) all.Add(t);
            }

            return all;
        }

        /// <summary>扫描面非空守卫 —— 取代原来的 <c>Assert.Ignore</c>。
        /// <para>⚠️ **为什么不再 Ignore**:原守卫的理由是「51 未实现,空命名空间是正确行为」,
        /// 但 51 现已实现(第十三~十五片),空集只可能意味着**反射扫不到装配** ——
        /// 那正是「假绿」最隐蔽的形态(全绿但什么都没测)。守卫须**转红**,不是跳过。</para>
        /// <para>同时点名断言两个生产者都在扫描集里 —— 生产者改名 / 移走时先红。</para></summary>
        private static void RequireNonEmptyScan(List<Type> types)
        {
            Assert.That(types.Count, Is.GreaterThan(0),
                "51 扫描集为空 —— 反射未命中装配,判据无效(不得以跳过冒充通过)");
            foreach (string producer in ProducerTypeNames)
                Assert.That(types.Any(t => t.FullName == producer), Is.True,
                    $"报告生产者 {producer} 不在扫描集内 —— 扫描面失效(改名 / 移出?须同步本表)");
        }

        // ══════════════ AC-51-E1:无玩家可见统计界面 ══════════════

        /// <summary>AC-51-E1:51 资产目录无 .uxml/.uss/.prefab/.unity 文件。
        /// QA 修复:目录不存在 = 无资产 = 通过(非 Ignore)。</summary>
        [Test]
        public void test_e1_noPlayerUi_noPresentationAssets()
        {
            // Arrange:扫描 51 资产目录
            string telemetryDir = Path.Combine(repoRoot(), "unity", "Assets", "Telemetry");

            // Act:检查目录是否存在
            if (!Directory.Exists(telemetryDir))
            {
                // 目录不存在 = 无呈现层资产 = 通过(QA 修复:非 Ignore)
                Assert.Pass("51 资产目录不存在 ⇒ 无呈现层资产(AC-51-E1:无玩家可见统计界面)");
                return;
            }

            // Assert:目录存在时扫描呈现层资产
            var presentationAssets = Directory.GetFiles(telemetryDir, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".uxml") || f.EndsWith(".uss") ||
                           f.EndsWith(".prefab") || f.EndsWith(".unity"))
                .ToList();

            Assert.That(presentationAssets, Is.Empty,
                "51 侧不得有 .uxml/.uss/Canvas/场景资产(AC-51-E1:无玩家可见统计界面):\n" +
                string.Join("\n", presentationAssets));
        }

        /// <summary>AC-51-E1:51 命名空间无菜单项/快捷键/控制台命令入口。
        /// 扫描 51 命名空间下的类型,验证无 MonoBehaviour / EditorWindow / 菜单特性。
        /// B1 修复:空命名空间 ⇒ Assert.Ignore(假绿防护)。</summary>
        [Test]
        public void test_e1_noPlayerUi_noMenuEntries()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = GetTelemetryTypes();

            // 空集守卫(第十六片):原 Ignore 已撤销 —— 51 已实现,空集 = 扫描面失效,须转红
            RequireNonEmptyScan(telemetryTypes);

            // Act:扫描公开方法签名中的 UI 入口特性
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
            {
                // 检查 MonoBehaviour 派生
                if (t.BaseType != null && t.BaseType.Name == "MonoBehaviour")
                    violations.Add($"{t.Name} 派生自 MonoBehaviour");

                // 检查 MenuItem 特性
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    var attrs = m.GetCustomAttributes(true);
                    foreach (var attr in attrs)
                    {
                        if (attr.GetType().Name.Contains("MenuItem"))
                            violations.Add($"{t.Name}.{m.Name} 含 MenuItem 特性");
                    }
                }
            }

            // Assert:零 UI 入口(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "51 不得有菜单项/快捷键/控制台命令入口(AC-51-E1:无玩家可见统计界面):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-51-E2:不写回数值 ══════════════

        /// <summary>AC-51-E2:51 命名空间无文件写入 API 引用。
        /// QA 修复:扩展检查模式,覆盖 File.WriteAllText/StreamWriter/BinaryWriter 等。
        /// B1 修复:空命名空间 ⇒ Assert.Ignore(假绿防护)。</summary>
        [Test]
        public void test_e2_noWriteBack_noFileWriteApis()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = GetTelemetryTypes();

            // 空集守卫(第十六片):原 Ignore 已撤销 —— 51 已实现,空集 = 扫描面失效,须转红
            RequireNonEmptyScan(telemetryTypes);

            // Act:扫描公开方法签名中的文件写入类型
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    // QA 修复:扩展检查模式
                    var returnTypeName = m.ReturnType.Name;
                    var returnFullName = m.ReturnType.FullName ?? "";
                    if ((returnTypeName.Contains("Stream") && returnTypeName.Contains("Write")) ||
                        returnTypeName == "StreamWriter" || returnTypeName == "BinaryWriter" ||
                        returnTypeName == "FileStream" || returnTypeName == "Stream" ||
                        returnFullName.Contains("System.IO.File") ||
                        returnFullName.Contains("System.IO.StreamWriter") ||
                        returnFullName.Contains("System.IO.BinaryWriter"))
                        violations.Add($"{t.Name}.{m.Name} 返回文件写入类型");
                    foreach (var p in m.GetParameters())
                    {
                        var paramTypeName = p.ParameterType.Name;
                        var paramFullName = p.ParameterType.FullName ?? "";
                        if ((paramTypeName.Contains("Stream") && paramTypeName.Contains("Write")) ||
                            paramTypeName == "StreamWriter" || paramTypeName == "BinaryWriter" ||
                            paramTypeName == "FileStream" || paramTypeName == "Stream" ||
                            paramFullName.Contains("System.IO.File") ||
                            paramFullName.Contains("System.IO.StreamWriter") ||
                            paramFullName.Contains("System.IO.BinaryWriter"))
                            violations.Add($"{t.Name}.{m.Name} 参数含文件写入类型");
                    }
                }

            // Assert:零文件写入 API(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "51 不得引用文件写入 API(AC-51-E2:不写回数值):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-51-E2:报告 schema 中无「建议值/proposed/推荐改动」字段名。
        /// 扫描 51 命名空间下的类型,验证无 proposed/recommendation/suggested 字段。
        /// B1 修复:空命名空间 ⇒ Assert.Ignore(假绿防护)。</summary>
        [Test]
        public void test_e2_noWriteBack_noProposedFields()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = GetTelemetryTypes();

            // 空集守卫(第十六片):原 Ignore 已撤销 —— 51 已实现,空集 = 扫描面失效,须转红
            RequireNonEmptyScan(telemetryTypes);

            // Act:扫描字段名
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static))
                {
                    var name = f.Name.ToLower();
                    if (name.Contains("proposed") || name.Contains("recommendation") ||
                        name.Contains("suggested") || name.Contains("recommended"))
                        violations.Add($"{t.Name}.{f.Name} 是建议值字段");
                }

            // Assert:零建议值字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "51 不得有 proposed/recommendation/suggested 字段(AC-51-E2:不写回数值):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-51-E2:assets/data/** 在 51 运行前后字节不变。
        /// QA 修复:添加字节不变性测试(当前阶段 assets/data/ 不存在 = 通过)。</summary>
        [Test]
        public void test_e2_noWriteBack_dataDirectoryUnchanged()
        {
            // Arrange:目录存在性
            string dataDir = Path.Combine(repoRoot(), "assets", "data");
            if (!Directory.Exists(dataDir))
            {
                Assert.Pass("assets/data/ 目录不存在 ⇒ 无数据文件 ⇒ 字节不变(AC-51-E2:不写回数值)");
                return;
            }

            // Act ①:**运行 51 之前**的全目录字节指纹(SHA-256,按相对路径排序后连带路径一起哈希)
            string before = FingerprintDirectory(dataDir);

            // Act ②:真跑一遍 51 —— 全指标汇总 + 落 sink(sink 写的是调用方给的路径策略,
            //          不指向 assets/data/;这里只为让 51 真的执行一遍全部取数路径)
            var (reporter, _) = NewReporter();
            var sink = new CountingSink();
            reporter.ComputeAndSink(sink, _ => new[] { reporter.AllMetricsReport(out int _) });

            // Act ③:**运行 51 之后**的同一指纹
            string after = FingerprintDirectory(dataDir);

            // Assert:字节完全一致(不是「目录可读」这种恒真断言)
            Assert.That(after, Is.EqualTo(before),
                "assets/data/** 在 51 运行前后须字节不变(AC-51-E2:不写回数值)");
        }

        /// <summary>目录指纹:逐文件 SHA-256,按相对路径序拼接,再整体哈希。
        /// <para>⚠️ 用 BCL `System.Security.Cryptography.SHA256`(51 本身不引 IO;
        /// 本条是**测试**侧的观测手段,不违反 AC-51-E2 的「无文件写入 API」判据 ——
        /// 那条判据扫的是 51 的**生产类型**,不是夹具)。</para></summary>
        private static string FingerprintDirectory(string dir)
        {
            var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            using var sha = System.Security.Cryptography.SHA256.Create();
            var combined = new List<byte>();
            foreach (string f in files)
            {
                combined.AddRange(System.Text.Encoding.UTF8.GetBytes(
                    Path.GetRelativePath(dir, f) + "\n"));
                combined.AddRange(sha.ComputeHash(File.ReadAllBytes(f)));
            }
            // ⚠️ 不用 `Convert.ToHexString` —— 它是 .NET 5+ API,Unity 6.3 的 netstandard2.1 没有
            byte[] digest = sha.ComputeHash(combined.ToArray());
            var hex = new System.Text.StringBuilder(digest.Length * 2);
            foreach (byte b in digest) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }

        /// <summary>计数 sink(E2 只要求 51 能跑完,不关心落哪)。</summary>
        private sealed class CountingSink : ITelemetrySink
        {
            public int Calls;
            public void WriteLocalReport(string fileName, string content) => Calls++;
            public void LogDeveloperSummary(string summary) => Calls++;
        }

        /// <summary>真装配:空 <c>EventStream</c> + 真 <c>PayloadDecoder</c> + 单病种注册表。</summary>
        private static (TelemetryReporter reporter, EventStream stream) NewReporter()
        {
            var pool = new InMemoryBlobPool();
            var stream = new EventStream(new SeqIdAuthority(), new PresenceRegistry());
            var reporter = new TelemetryReporter(stream, new PayloadDecoder(pool),
                new[] { new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 1, DiseaseKey = "d1" } });
            return (reporter, stream);
        }

        // ══════════════ AC-51-E3:非主机侧拒绝 ══════════════

        /// <summary>AC-51-E3:非主机客户端请求出指标时**拒绝**,不产出空 / 截断指标。
        /// <para>TD-epic-01 第十三片(2026-10-19):`OQ-51-5` **已于 2026-10-06 裁定**
        /// (`telemetry-analytics.md:706` — 51 只在主机或离线运行,非主机侧拒绝),
        /// 「未裁 ⇒ NOT-RUN」的理由**已失效**;生产侧 `TelemetryReporter.RequireHost` 亦已落。
        /// 本条由 `Assert.Ignore` 改为真驱动。</para>
        /// <para><b>判别力</b>:删掉 `RequireHost()` 里的 `throw` ⇒ 本条红(会正常返回报告)。</para>
        /// <para>⚠️ <b>只覆盖「注入了非主机 authority 时拒」</b>。`IEventAuthority` 全仓**零生产实装**
        /// (`SeenDiseaseProjector.cs:23-26` 自陈归 P1b)⇒ 「真联机时被拒」这半边**仍 NOT-RUN**,
        /// 见下方 <see cref="test_e3_realOnline_nonHostRejected_NOT_RUN"/>。</para></summary>
        [Test]
        public void test_e3_nonHostAuthority_rejectsAllReports()
        {
            // Arrange:真 reporter + 注入「非主机」authority
            var pool = new InMemoryBlobPool();
            var stream = new EventStream(new SeqIdAuthority(), new PresenceRegistry());
            var reporter = new TelemetryReporter(stream, new PayloadDecoder(pool),
                new[] { new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 1, DiseaseKey = "d1" } },
                knobs: null, eventAuthority: new FakeEventAuthority(isHost: false));
            var sink = new NullSink();

            // Act + Assert:每一条出口都必须拒(不留「某一条忘了设门」的缝)
            Assert.Throws<InvalidOperationException>(() => reporter.JudgmentReport(out _), "F1");
            Assert.Throws<InvalidOperationException>(() => reporter.MisdiagnosisReport(out _), "F2");
            Assert.Throws<InvalidOperationException>(() => reporter.SkipRateReport(out _, out _), "F3");
            Assert.Throws<InvalidOperationException>(() => reporter.SkillGrowthReport("1", (0, 1), out _), "F4");
            Assert.Throws<InvalidOperationException>(() => reporter.DifficultyCurveReport(0, 5, 5, out _), "F5");
            Assert.Throws<InvalidOperationException>(() => reporter.SessionRhythmReport(3, out _), "F6/F7");
            Assert.Throws<InvalidOperationException>(() => reporter.AllMetricsReport(out _), "汇总");
            Assert.Throws<InvalidOperationException>(() => _ = reporter.ScorableCount, "ScorableCount");
            Assert.Throws<InvalidOperationException>(
                () => reporter.RenderDeveloperPreview(new TelemetryPreviewRequest(null, null, null, null, null, null, null), sink),
                "调试视图入口");
        }

        /// <summary>AC-51-E3 负向:主机侧照常出数 —— 门不是「一律拒」。
        /// <para><b>判别力</b>:把 `RequireHost` 的判据写成恒拒 ⇒ 本条红。</para></summary>
        [Test]
        public void test_e3_hostAuthority_producesReports()
        {
            // Arrange
            var pool = new InMemoryBlobPool();
            var stream = new EventStream(new SeqIdAuthority(), new PresenceRegistry());
            var reporter = new TelemetryReporter(stream, new PayloadDecoder(pool),
                new[] { new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 1, DiseaseKey = "d1" } },
                knobs: null, eventAuthority: new FakeEventAuthority(isHost: true));

            // Act
            string report = reporter.JudgmentReport(out _);

            // Assert
            Assert.That(report, Does.Contain("F1"), "主机侧须正常出指标");
            Assert.DoesNotThrow(() => _ = reporter.ScorableCount, "主机侧 ScorableCount 须可读");
        }

        /// <summary>AC-51-E3 无 authority = 单机/离线,不设门(如实登记的设计)。
        /// <para>为什么是「不设门」而不是「默认当主机」:`IEventAuthority` 零生产实装 ⇒
        /// 注入 null 只可能发生在单机 / 离线重算(结构上恒为主机或从主机存档)。
        /// 但本类**不自造**一个恒 true 的占位实现来假装门存在 —— 那会把「门还没接」伪装成「门已实现」。</para></summary>
        [Test]
        public void test_e3_nullAuthority_isSinglePlayer_noGate()
        {
            // Arrange
            var pool = new InMemoryBlobPool();
            var stream = new EventStream(new SeqIdAuthority(), new PresenceRegistry());
            var reporter = new TelemetryReporter(stream, new PayloadDecoder(pool),
                new[] { new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 1, DiseaseKey = "d1" } });

            // Act + Assert
            Assert.That(reporter.EventAuthority, Is.Null, "未注入 ⇒ EventAuthority 保持 null(不自造占位)");
            Assert.DoesNotThrow(() => reporter.JudgmentReport(out _), "单机/离线不设门");
        }

        /// <summary>AC-51-E3 的**未覆盖半边** —— 真联机环境下非主机侧被拒。
        /// <para>⚠️ 2026-10-19 第十六片:本条由 `Assert.Ignore`(跳过)**改为缺口哨兵**(硬断言)。</para>
        /// <para><b>为什么不再跳过</b>:`Assert.Ignore` 让「环境不具备」以**跳过**的形式长期存活,
        /// 而跳过既不会红、也不会计入失败 —— 正是 `TD-epic-01` 普查里「第五形态」的载体
        /// (症状是跳过,连红的机会都没有)。哨兵反过来:环境**一旦就绪**它立刻转红,
        /// 逼着把真判据补上,而不是让缺口悄悄留在绿里。</para>
        /// <para><b>当前判据(用户 2026-10-19 裁定:半边推到 P1b)</b>:P0 断言
        /// `IEventAuthority` 在 `unity/Assets/` 下**零生产实装**(排除 `Sim.Contracts` 契约面与测试树)。
        /// 该事实成立 ⇒ 本条绿(缺口已登记,归 P1b 联机轮);
        /// 一旦出现生产实装 ⇒ 本条**红** ⇒ 须改为真驱动「真非主机进程被拒」。</para>
        /// <para>🔁 **2026-10-30 订正(ADR-036 S4 装配轮)**:生产树里出现了一个
        /// <c>P0HostEventAuthority</c> —— 但它是**P0 单机占位**(恒 `IsHost = true`,
        /// 无对端、无角色切换),**不是**「真联机权威就绪」。若照原判据直接转红,
        /// 逼出来的是「给占位写一条假的非主机路径」—— 那是把哨兵变成摆设。
        /// ⇒ 现判据二分:**实现 <see cref="IP0AuthorityPlaceholder"/> 的占位不计入**;
        /// 且**排除集必须非空**(否则这条排除条件将来会静默失效 —— 占位被删掉后,
        /// 本测又会退化成「扫到空集就绿」的原形态,且没人会发现)。
        /// 真正的转红条件不变:**出现不实现该标记的生产实装**。</para></summary>
        [Test]
        public void test_e3_realOnline_nonHostRejected_gapSentinel()
        {
            string[] all = FindProductionEventAuthorityImplementations();
            string[] placeholders = all
                .Where(n => n != null && IsP0Placeholder(n))
                .ToArray();
            string[] real = all.Except(placeholders).ToArray();

            // 排除条件**自身**须可证伪:占位必须在场且恰是 P0 那个 —— 否则将来占位被删/改名,
            // 「排除」会静默失效,本测退化成「扫到空集就绿」。
            Assert.That(placeholders, Is.Not.Empty,
                "未扫到任何 P0 占位权威 —— 占位被删/改名了?本测的排除条件必须同批订正" +
                "(否则它会静默退化为『空集即绿』)。");
            Assert.That(placeholders, Has.All.Contains("P0HostEventAuthority"),
                "排除集里出现非 P0 占位的东西:" + string.Join(", ", placeholders));

            Assert.That(real, Is.Empty,
                "AC-51-E3 的「真联机」半边仍登记为 P1b 缺口,但检测到**非占位**的 `IEventAuthority` " +
                "生产实装:" + string.Join(", ", real) +
                " —— 缺口已就绪 ⇒ 本条哨兵必须改为真驱动(构造真非主机进程断言被拒)," +
                "不得继续以「环境不具备」为由留绿。");
        }

        /// <summary>该类型是否声明为 P0 占位(实现 <c>IP0AuthorityPlaceholder</c>)。</summary>
        private static bool IsP0Placeholder(string fullName)
        {
            Type t = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType(fullName); } catch { return null; } })
                .FirstOrDefault(x => x != null);
            return t != null && typeof(IP0AuthorityPlaceholder).IsAssignableFrom(t);
        }

        /// <summary>扫描 `unity/Assets/` 下**生产树**里实现 <c>IEventAuthority</c> 的类型。
        /// <para>排除面:① `Sim.Contracts`(契约面 —— 接口本身住那里,不是实装);
        /// ② 任何名字含 `Tests` 的装配(测试替身不算生产实装)。</para></summary>
        private static string[] FindProductionEventAuthorityImplementations()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name != null && !a.GetName().Name.Contains("Tests"))
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t != null && typeof(IEventAuthority).IsAssignableFrom(t)
                            && t.IsClass && !t.IsAbstract)
                .Select(t => t.FullName)
                .ToArray();
        }

        private sealed class FakeEventAuthority : IEventAuthority
        {
            public FakeEventAuthority(bool isHost) { IsHost = isHost; }
            public bool IsAuthority => IsHost;
            public bool IsHost { get; }
            public EventRollResult Roll(in RollRequest r) => default;
        }

        private sealed class NullSink : ITelemetrySink
        {
            public void WriteLocalReport(string fileName, string content) { }
            public void LogDeveloperSummary(string summary) { }
        }

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private long _n = 100;
            public PatientId NextPatientId() => new PatientId((int)_n++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_n++);
        }

        // ══════════════ AC-51-E4:无因果字段(ADVISORY)══════════════

        /// <summary>AC-51-E4:R10 报告无因果/归因字段。
        /// 扫描 51 命名空间下的类型,验证无 cause_of/attribution/recommendation 字段。
        /// ⚠️ ADVISORY:正文层(自然语言归因)不可机器判定 = 人工走查。
        /// B1 修复:空命名空间 ⇒ Assert.Ignore(假绿防护)。</summary>
        [Test]
        public void test_e4_noCausation_noCausalFields()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = GetTelemetryTypes();

            // 空集守卫(第十六片):原 Ignore 已撤销 —— 51 已实现,空集 = 扫描面失效,须转红
            RequireNonEmptyScan(telemetryTypes);

            // Act:扫描字段名
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static))
                {
                    var name = f.Name.ToLower();
                    if (name.Contains("cause_of") || name.Contains("attribution") ||
                        name.Contains("recommendation"))
                        violations.Add($"{t.Name}.{f.Name} 是因果/归因字段");
                }

            // Assert:零因果字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "51 不得有 cause_of/attribution/recommendation 字段(AC-51-E4:无因果字段):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-51-E5:无开关 ══════════════

        /// <summary>AC-51-E5:telemetry_analytics.json schema 中无
        /// enabled/sample_rate/endpoint/experiment/consent 字段。
        /// <para>⚠️ <b>2026-10-19 重写(TD-epic-01 第十二片,用户裁定「文件必须存在 + 扫描禁止字段」)</b>:
        /// 原实现「文件不存在 ⇒ <c>Assert.Pass</c>」是**恒真分支** —— 而该文件当时**从未存在**
        /// (见 `review-cross-epic-fake-green-2026-10-18.md:58` 的登记),本条对变异零判别力。
        /// 现判据:文件**必须存在**(缺失 = 红,点名 GDD 声明的落地位置);存在 ⇒ 扫禁止字段,
        /// 并把<b>白名单</b>顶层的结构性执法也断言上 —— 白名单外键在烘焙期即硬失败,这比
        /// 黑名单扫描更强(黑名单只挡已知的)。</para></summary>
        [Test]
        public void test_e5_noSwitch_noSwitchFields()
        {
            // Arrange
            string configPath = Path.Combine(repoRoot(), "assets", "data", "telemetry_analytics.json");

            // Act + Assert ①:文件必须存在(不存在 ⇒ 无对象可测 = 红,不再 Pass)
            Assert.That(File.Exists(configPath), Is.True,
                $"telemetry_analytics.json 必须存在(GDD §Tuning Knobs 的落地位置)—— {configPath}。" +
                "文件缺失时本条**不得**以「无 schema ⇒ 无开关」通过:那是对变异的零判别力(假绿)。");

            // Act ②:黑名单扫描(逐 token,避免子串误命)
            string jsonContent = File.ReadAllText(configPath);
            var switchFieldNames = new[] { "enabled", "sample_rate", "endpoint", "experiment", "consent" };
            var violations = new List<string>();
            foreach (string field in switchFieldNames)
                if (jsonContent.Contains($"\"{field}\"", StringComparison.Ordinal))
                    violations.Add($"telemetry_analytics.json 含禁止字段: {field}");

            Assert.That(violations, Is.Empty,
                "telemetry_analytics.json 不得含 enabled/sample_rate/endpoint/experiment/consent 字段(AC-51-E5:无开关):\n" +
                string.Join("\n", violations));

            // Assert ③:白名单执法(binder 对文件里的禁止字段必须**拒收**,而不仅仅是「扫不到」)
            var binderErrors = new List<string>();
            try
            {
                DaYiJingCheng.EditorTools.Bake.TelemetryAnalyticsBinder.BindFromSourceText(jsonContent);
            }
            catch (DaYiJingCheng.EditorTools.Bake.BakeValidationException ex)
            {
                binderErrors.AddRange(ex.Errors);
            }
            Assert.That(binderErrors, Is.Empty,
                "真文件必须经 binder 白名单无错通过(否则烘焙管线本身就拒收它):\n" + string.Join("\n", binderErrors));
        }

        /// <summary>AC-51-E5:51 命名空间无开关字段。
        /// 扫描 51 命名空间下的类型,验证无 enabled/sample_rate/endpoint/experiment/consent 字段。
        /// B1 修复:空命名空间 ⇒ Assert.Ignore(假绿防护)。</summary>
        [Test]
        public void test_e5_noSwitch_noSwitchFieldsInNamespace()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = GetTelemetryTypes();

            // 空集守卫(第十六片):原 Ignore 已撤销 —— 51 已实现,空集 = 扫描面失效,须转红
            RequireNonEmptyScan(telemetryTypes);

            // Act:扫描字段名
            var violations = new List<string>();
            var switchFieldNames = new[] { "enabled", "sample_rate", "endpoint", "experiment", "consent" };
            foreach (var t in telemetryTypes)
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static))
                {
                    var name = f.Name.ToLower();
                    if (switchFieldNames.Any(s => name.Contains(s)))
                        violations.Add($"{t.Name}.{f.Name} 是开关字段");
                }

            // Assert:零开关字段(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "51 不得有 enabled/sample_rate/endpoint/experiment/consent 字段(AC-51-E5:无开关):\n" +
                string.Join("\n", violations));
        }
    }
}
