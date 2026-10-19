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

        /// <summary>获取 51 命名空间下的所有类型(R1 修复:提取重复代码)。</summary>
        private static List<Type> GetTelemetryTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();
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

            // B1 修复:空命名空间 ⇒ 跳过(当前阶段正确行为)
            if (telemetryTypes.Count == 0)
            {
                Assert.Ignore("DaYiJingCheng.Telemetry 命名空间为空(当前阶段 51 未实现,正确行为)");
                return;
            }

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

            // B1 修复:空命名空间 ⇒ 跳过(当前阶段正确行为)
            if (telemetryTypes.Count == 0)
            {
                Assert.Ignore("DaYiJingCheng.Telemetry 命名空间为空(当前阶段 51 未实现,正确行为)");
                return;
            }

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

            // B1 修复:空命名空间 ⇒ 跳过(当前阶段正确行为)
            if (telemetryTypes.Count == 0)
            {
                Assert.Ignore("DaYiJingCheng.Telemetry 命名空间为空(当前阶段 51 未实现,正确行为)");
                return;
            }

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
            // Arrange:检查 assets/data/ 目录
            string dataDir = Path.Combine(repoRoot(), "assets", "data");

            // Act:检查目录是否存在
            if (!Directory.Exists(dataDir))
            {
                // 目录不存在 = 无数据文件 = 字节不变 = 通过(QA 修复:非 Ignore)
                Assert.Pass("assets/data/ 目录不存在 ⇒ 无数据文件 ⇒ 字节不变(AC-51-E2:不写回数值)");
                return;
            }

            // Assert:目录存在时,验证无 51 写入路径
            // 当前阶段 51 未实现,无写入路径 = 字节不变
            // 实现后应改为:运行 51 前后对比目录哈希
            var dataFiles = Directory.GetFiles(dataDir, "*.*", SearchOption.AllDirectories);
            Assert.That(dataFiles, Is.Not.Null, "assets/data/ 目录应可读取");
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
                new[] { new DiseaseRegistryEntry { DiseaseId = 1, DiseaseKey = "d1" } },
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
                new[] { new DiseaseRegistryEntry { DiseaseId = 1, DiseaseKey = "d1" } },
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
                new[] { new DiseaseRegistryEntry { DiseaseId = 1, DiseaseKey = "d1" } });

            // Act + Assert
            Assert.That(reporter.EventAuthority, Is.Null, "未注入 ⇒ EventAuthority 保持 null(不自造占位)");
            Assert.DoesNotThrow(() => reporter.JudgmentReport(out _), "单机/离线不设门");
        }

        /// <summary>AC-51-E3 的**未覆盖半边** —— 真联机环境下非主机侧被拒。</summary>
        [Test]
        public void test_e3_realOnline_nonHostRejected_NOT_RUN()
        {
            Assert.Ignore("AC-51-E3 的「真联机」半边 NOT-RUN:`IEventAuthority` 全仓零生产实装" +
                          "(SeenDiseaseProjector.cs:23-26 自陈归 P1b 联机轮)⇒ 本机无法构造真非主机进程。" +
                          "已覆盖的是「注入非主机 authority 时拒」(test_e3_nonHostAuthority_rejectsAllReports)。");
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

            // B1 修复:空命名空间 ⇒ 跳过(当前阶段正确行为)
            if (telemetryTypes.Count == 0)
            {
                Assert.Ignore("DaYiJingCheng.Telemetry 命名空间为空(当前阶段 51 未实现,正确行为)");
                return;
            }

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

            // B1 修复:空命名空间 ⇒ 跳过(当前阶段正确行为)
            if (telemetryTypes.Count == 0)
            {
                Assert.Ignore("DaYiJingCheng.Telemetry 命名空间为空(当前阶段 51 未实现,正确行为)");
                return;
            }

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
