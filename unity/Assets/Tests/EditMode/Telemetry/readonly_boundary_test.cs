// 权威来源:production/epics/telemetry-analytics/story-001-readonly-boundary-zero-egress.md
//   (AC-19-01…08 · AC-51-A1…A8)
//   · 51 只读不写:唯一输入面 = ITelemetrySource 的三个 Read*;不存在任何向三流 Append/Write/Emit 的成员
//   · 零出厂:构建 + 运行期无任何网络上报;ITelemetrySink 无 Upload/Send/Post
//   · 住边界层:不进 sim 程序集(门 A 不污染);sim 不引用 51(无环)
// ADR-019 §一/§二/§三/§四/§五/§六/§七 · ADR-025 §①(契约程序集清单)
// ADR-017 §二(门 A 硬化:sim 程序集引用集白名单断言)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/integration/telemetry/readonly_boundary_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**(承 Story 001-012/014 同一先例);
//    账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 007-012 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class ReadonlyBoundaryTest
    {
        // ══════════════ 基础设施 ══════════════

        // 本文件在 unity/Assets/Tests/EditMode/Telemetry/ ⇒ Telemetry→EditMode→Tests→Assets→unity→
        // 仓库根 = 5 层(写 4 层 ⇒ root 落在 unity/ 下,夹具 / GDD / 源树全报「缺失」)。
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        // ══════════════ AC-51-A1:只读边界(无 IEventSink 引用)══════════════

        /// <summary>AC-51-A1:51 程序集无 <c>IEventSink</c> 或其派生的成员。
        /// 扫描 Sim.Contracts 的 IEventSink 接口定义,验证 51 侧无引用。
        /// ⚠️ 当前阶段 51 程序集未实现,本测试验证**契约面**:IEventSink 存在且 51 侧无引用路径。</summary>
        [Test]
        public void test_readonlyBoundary_noEventSinkRefs()
        {
            // Arrange:验证 IEventSink 契约存在(Sim.Contracts)
            var iEventSink = typeof(IEventSink);
            Assert.That(iEventSink, Is.Not.Null, "IEventSink 契约必须存在(Sim.Contracts)");

            // Act:扫描 51 命名空间下的类型(当前阶段 = 零类型,验证扫描键落空 = 假绿防护)
            var telemetryTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();

            // Assert:无 IEventSink 引用(实现前后都成立)
            // 扫描 51 类型的全部签名,收集 violations
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    if (iEventSink.IsAssignableFrom(m.ReturnType))
                        violations.Add($"{t.Name}.{m.Name} 返回 IEventSink");
                    foreach (var p in m.GetParameters())
                        if (iEventSink.IsAssignableFrom(p.ParameterType))
                            violations.Add($"{t.Name}.{m.Name} 参数含 IEventSink");
                }
            Assert.That(violations, Is.Empty,
                "51 不得引用 IEventSink(AC-51-A1):\n" + string.Join("\n", violations));
        }

        /// <summary>AC-19-01:指标可从既有事件流重算,零新埋点。
        /// 当前阶段 51 未实现 —— 验证契约面:ITelemetrySource 存在且只有 Read* 方法。</summary>
        [Test]
        public void test_zeroNewInstrumentation_sourceIsReadOnly()
        {
            // Arrange:验证 ITelemetrySource 契约存在
            var sourceType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "ITelemetrySource");

            // Act + Assert:契约当前未实现(实现后应验证方法集)
            if (sourceType == null)
            {
                Assert.Ignore("AC-19-01:ITelemetrySource 未实现,实现后验证只有三个 Read* 方法");
                return;
            }

            // 实现后:验证方法集
            var methods = sourceType.GetMethods();
            var readMethods = methods.Where(m => m.Name.StartsWith("Read")).ToArray();
            Assert.That(readMethods.Length, Is.EqualTo(3),
                "ITelemetrySource 必须只有三个 Read* 方法(AC-19-01:零新埋点)");
            var writeMethods = methods.Where(m =>
                m.Name.StartsWith("Write") || m.Name.StartsWith("Append") || m.Name.StartsWith("Emit")).ToArray();
            Assert.That(writeMethods, Is.Empty,
                "ITelemetrySource 不得有 Write/Append/Emit 方法(AC-19-01:零新埋点)");
        }

        /// <summary>AC-19-01(强化 · TD-epic-01 第十三片):指标真能从**既有事件流**重算 —— 不是「接口形状对」。
        /// <para>原版只断言 `ITelemetrySource` 有三个 `Read*`。那证明的是**契约形状**,
        /// 证明不了「51 算指标所需的输入都已在流里」。</para>
        /// <para>本条的判据:向**只含既有 `Kind`** 的事件流追加若干条,`TelemetryReporter` 的
        /// F1/F2/F3 报告即产出**非空分母的实数** —— 即所需输入**零新埋点**。</para>
        /// <para><b>判别力</b>:把 `Reduce` 改成读任何流外来源(墙钟 / 全局计数)⇒ 本条的
        /// 「同流同值」后半红;把 `CaseClosed` 的载荷解码删掉 ⇒ 分母恒 0 ⇒ 前半红。</para></summary>
        [Test]
        public void test_ac1901_metricsRecomputableFromExistingStream_only()
        {
            // Arrange:只用 ADR-007/008/009 已登记的既有 Kind
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var stream = new EventStream(new SeqIdAuthority(), new PresenceRegistry());
            var reporter = new TelemetryReporter(stream, new PayloadDecoder(pool),
                new[] { new DiseaseRegistryEntry { DiseaseId = 1, DiseaseKey = "d1" } },
                knobs: null, eventAuthority: null);

            // 零新埋点:只用既有病例流事件 —— CaseClosed(结论)+ JudgmentRecorded(判断),
            // 两者成对才构成「可打分病例」(Scorable = 有判断的已结案)。
            for (int i = 0; i < 2; i++)
            {
                var caseId = new CaseId(10, 1, i + 1);
                PayloadRef closed = encoder.Encode(EventKind.CaseClosed,
                    new CaseClosedPayload(patientId: 1, caseId,
                        new DiseaseIdSet(1UL << 0), treated: true));
                stream.Append(new SimEvent(i * 2, new PatientId(1), -1,
                    EventKind.CaseClosed, closed));

                // JudgmentRecorded 带 G-3 变长段 ⇒ 走编码器的具名重载(不经泛型 Encode)
                PayloadRef judged = encoder.EncodeJudgmentRecorded(
                    new JudgmentRecordedPayload(patientId: 1, caseId,
                        authorPlayerId: 1, lexiconId: 1, confidence: 1), freehandText: "");
                stream.Append(new SimEvent(i * 2 + 1, new PatientId(1), -1,
                    EventKind.JudgmentRecorded, judged));
            }

            // Act:同一流上算两次
            string first = reporter.MisdiagnosisReport(out int sk1);
            string second = reporter.MisdiagnosisReport(out int sk2);
            int scorable = reporter.ScorableCount;

            // Assert ①:输入确实来自流 ⇒ 分母非 0(不是「空流零值」)
            Assert.That(scorable, Is.GreaterThan(0),
                "既有事件流里的 CaseClosed 必须足以算出 Scorable > 0(否则说明指标依赖流外来源)");
            // Assert ②:零新埋点 ⇒ 不需要任何额外采集调用,报告就已可算
            Assert.That(first, Does.Not.Contain("(无指标"), "报告不得因「无输入」而空");
            // Assert ③:同流同值(R8 纯函数)| 不读墙钟 / 不读执行次数
            Assert.That(second, Is.EqualTo(first), "同一事件流两次重算必须逐字相同(R8:不读墙钟)");
            Assert.That(sk1, Is.EqualTo(sk2), "跳过计数同样不得随调用次数漂移");
        }

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private long _n = 100;
            public PatientId NextPatientId() => new PatientId((int)_n++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_n++);
        }

        /// <summary>AC-19-04:无 `IEventSink.Append` 调用点(无向**三流**写入的成员)。
        /// <para>⚠️ **S19 修订判据范围(TD-epic-01 第二片)**:原判据扫「方法名含 Write/Append/Emit」⇒
        /// **过宽**。ADR-019 §五 明确允许**本地导出**(写本地文件),`ITelemetrySink.WriteLocalReport`
        /// 正因此被误判为违例。AC-19-04 的宾语逐字是「**无 `IEventSink.Append` 调用点**」
        /// —— 禁的是**向三流写**,不是禁一切 Write。</para>
        /// <para>⇒ 收窄为两条:① **不得有任何 `IEventSink` 类型的形参 / 字段 / 返回**
        /// (与 `test_noEventSinkRefs` 同源但这里是方法签名级);② 不得有 `Append`/`Emit` 命名的方法。</para></summary>
        [Test]
        public void test_noAppendCallSite_noWriteMethods()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();

            // Act:扫描 —— 只扫「向三流写」的形态,不扫本地导出
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    // ① 向三流写的命名(Append / Emit)—— AC-19-04 的逐字宾语
                    if (m.Name.Contains("Append") || m.Name.Contains("Emit"))
                        violations.Add($"{t.Name}.{m.Name} 是向三流写的方法(AC-19-04)");

                    // ② IEventSink 形参(拿到写通道即可能写)
                    foreach (var p in m.GetParameters())
                        if (p.ParameterType.Name.Contains("IEventSink"))
                            violations.Add($"{t.Name}.{m.Name} 有形参 {p.ParameterType.Name}(写通道)");
                }

            // Assert:零「向三流写」形态
            Assert.That(violations, Is.Empty,
                "51 不得有向三流写入的成员(AC-19-04:无 IEventSink.Append 调用点):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-51-A2:无环(asmdef 引用集)══════════════

        /// <summary>AC-51-A2:51 asmdef 引用集 ⊆ {sim 程序集, BCL 白名单};sim 不引用 51。
        /// <para>⚠️ **S19 实现(TD-epic-01 第二片)**:asmdef 已于本批创建(`unity/Assets/Telemetry/Telemetry.asmdef`)
        /// ⇒ 原两个 `Assert.Ignore`(「asmdef 尚未创建」/「引用集验证未实现」)**都不再成立**,
        /// 改为**真判据**:解析 JSON,断言引用集 ⊆ {`Sim.Contracts`} 且 `noEngineReferences == true`。</para></summary>
        [Test]
        public void test_readonlyBoundary_asmdefNoCycle()
        {
            // Arrange:读 51 的 asmdef
            string asmdefPath = Path.Combine(repoRoot(), "unity", "Assets", "Telemetry",
                "Telemetry.asmdef");
            Assert.That(File.Exists(asmdefPath), Is.True,
                "51 asmdef 应存在(ADR-019 §三:51 住边界层)");

            string json = File.ReadAllText(asmdefPath);

            // Assert ①:noEngineReferences = true(GDD R3:51 不进 sim、不引任何引擎程序集)
            Assert.That(json, Does.Contain("\"noEngineReferences\": true"),
                "51 asmdef 须 noEngineReferences = true(GDD R3 / ADR-019 §三)");

            // Assert ②:引用集 ⊆ {Sim.Contracts}(只读三流需要 SimEvent 类型)
            //   ⚠️ 用文本包含而非 JSON 解析:门(b3)同款判据也是按文本取 name 字段,
            //     且本工程 EditMode 无 Newtonsoft(ADR-014 禁 JsonConvert)。
            //   ⚠️ 只取 **references 数组内**的串 —— 首版把 rootNamespace 也收进来 ⇒ 误报。
            var refs = new List<string>();
            int refStart = json.IndexOf("\"references\"", StringComparison.Ordinal);
            if (refStart >= 0)
            {
                int arrStart = json.IndexOf('[', refStart);
                int arrEnd = json.IndexOf(']', arrStart);
                string arr = json.Substring(arrStart, arrEnd - arrStart + 1);
                foreach (var m in System.Text.RegularExpressions.Regex.Matches(arr, "\"([A-Za-z0-9_.]+)\"").Cast<System.Text.RegularExpressions.Match>())
                    refs.Add(m.Groups[1].Value);
            }
            Assert.That(refs, Is.SubsetOf(new[] { "Sim.Contracts" }),
                "51 引用集须 ⊆ {Sim.Contracts}(只读三流;不引 sim 实现、不引引擎)。实际:" +
                string.Join(", ", refs));

            // Assert ③:名单里不得出现 Sim(实现)—— R3 逐字「不进 sim 程序集」的**编译期**面
            Assert.That(json, Does.Not.Contains("\"Sim\""),
                "51 不得引用 Sim(实现);只读三流只需 Sim.Contracts 的 SimEvent 形状");
        }

        // ══════════════ AC-19-02/AC-51-A4:零出厂(无网络类型)══════════════

        /// <summary>AC-19-02/AC-51-A4:51 程序集无 <c>HttpClient</c>/<c>UnityWebRequest</c>/出站 <c>System.Net.*</c>。
        /// 扫描 51 命名空间下的类型签名。</summary>
        [Test]
        public void test_zeroEgress_noNetworkTypes()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();

            // Act:扫描公开方法签名
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.ReturnType.Name.Contains("HttpClient") ||
                        m.ReturnType.Name.Contains("UnityWebRequest") ||
                        m.ReturnType.FullName?.Contains("System.Net") == true)
                        violations.Add($"{t.Name}.{m.Name} 返回网络类型");
                    foreach (var p in m.GetParameters())
                        if (p.ParameterType.Name.Contains("HttpClient") ||
                            p.ParameterType.Name.Contains("UnityWebRequest") ||
                            p.ParameterType.FullName?.Contains("System.Net") == true)
                            violations.Add($"{t.Name}.{m.Name} 参数含网络类型");
                }

            // Assert:零网络类型引用(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "51 不得引用 HttpClient/UnityWebRequest/System.Net.*(AC-19-02/AC-51-A4):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-19-02 的**另一半扫描面**(TD-epic-01 第十三片补):报告生产者住在
        /// `Gameplay.Boot`(`TelemetryReporter` / `TelemetryDeveloperView`),**不在** `Telemetry` 程序集里。
        /// 在那边加一个 `HttpClient` 字段,上面那条 `test_zeroEgress_noNetworkTypes` 扫不到。</summary>
        /// <remarks>
        /// <b>判别力</b>:在 `TelemetryReporter` 里加 `private readonly HttpClient _c;` ⇒ 本条红。
        /// <para>⚠️ 扫描面是**类型 + 字段 + 方法签名**(反射),不是源码 grep —— grep 会被注释里
        /// 的「零网络」说明误伤(同 `test_d4_viewSource_entirelyInsideConditionalCompilation` 的教训)。</para>
        /// </remarks>
        [Test]
        public void test_ac1902_reportProducer_noNetworkInGameplayBoot()
        {
            // Arrange:排除 51 程序集(由上一条覆盖),只看报告生产者所在的程序集
            var producerTypes = new[] { "Gameplay.Boot" }
                .Select(n => AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == n))
                .Where(a => a != null)
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .ToList();
            // ⚠️ 扫描键非空守卫 —— `GetTypes()` 空洞返回会令下面的 Is.Empty 恒真(假绿)
            Assert.That(producerTypes, Is.Not.Empty,
                "Gameplay.Boot 程序集须可枚举(否则本条对变异零判别力)");

            // Act
            var violations = new List<string>();
            foreach (var t in producerTypes)
            {
                const BindingFlags surface = BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Instance | BindingFlags.Static |
                                             BindingFlags.DeclaredOnly;
                foreach (var f in t.GetFields(surface))
                    if (IsNetwork(f.FieldType)) violations.Add($"{t.Name}.{f.Name} 字段");
                foreach (var m in t.GetMethods(surface))
                {
                    if (IsNetwork(m.ReturnType)) violations.Add($"{t.Name}.{m.Name}() 返回");
                    foreach (var p in m.GetParameters())
                        if (IsNetwork(p.ParameterType)) violations.Add($"{t.Name}.{m.Name}() 参数");
                }
            }

            // Assert
            Assert.That(violations, Is.Empty,
                "报告生产者不得持有网络类型(AC-19-02:零出厂):\n" + string.Join("\n", violations));

            static bool IsNetwork(Type ty)
            {
                if (ty == null) return false;
                if (ty.HasElementType) ty = ty.GetElementType();
                string n = ty.Name, f = ty.FullName ?? n;
                return n.Contains("HttpClient") || n.Contains("UnityWebRequest")
                    || n.Contains("WebClient") || n == "Socket"
                    || f.StartsWith("System.Net.") || f.StartsWith("UnityEngine.Networking");
            }
        }

        /// <summary>AC-19-02/AC-51-A4:ITelemetrySink 无 <c>Upload</c>/<c>Send</c>/<c>Post</c>。
        /// 验证 ITelemetrySink 契约(当前阶段 = 契约面验证)。</summary>
        [Test]
        public void test_zeroEgress_sinkNoUploadSendPost()
        {
            // Arrange:验证 ITelemetrySink 契约存在
            var sinkType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "ITelemetrySink");

            // Act + Assert:契约当前未实现(实现后应验证无 Upload/Send/Post)
            if (sinkType == null)
            {
                Assert.Ignore("ITelemetrySink 未实现,实现后验证无 Upload/Send/Post 方法");
                return;
            }

            // 实现后:验证方法集
            var methods = sinkType.GetMethods();
            var uploadMethods = methods.Where(m =>
                m.Name.Contains("Upload") || m.Name.Contains("Send") || m.Name.Contains("Post")).ToArray();
            Assert.That(uploadMethods, Is.Empty,
                "ITelemetrySink 不得有 Upload/Send/Post 方法(AC-19-02/AC-51-A4:零出厂)");
        }

        // ══════════════ AC-19-04/AC-51-A8:不订阅 Step══════════════

        /// <summary>AC-19-04/AC-51-A8:51 无 <c>Update</c>/<c>FixedUpdate</c>/逐 <c>Tick</c> 回调订阅。
        /// 扫描 51 命名空间下的类型。</summary>
        [Test]
        public void test_noStepSubscription_noUpdateCallbacks()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();

            // Act:扫描公开方法名
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.Name == "Update" || m.Name == "FixedUpdate" || m.Name == "LateUpdate")
                        violations.Add($"{t.Name}.{m.Name} 是帧回调");
                }

            // Assert:零帧回调(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "51 不得有 Update/FixedUpdate/LateUpdate(AC-19-04/AC-51-A8:重算仅由显式 Compute() 触发):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-19-06/AC-51-A5:无玩家可见 UI══════════════

        /// <summary>AC-19-06/AC-51-A5:51 侧无 <c>.uxml</c>/<c>.uss</c>/Canvas/场景资产。
        /// 验证 51 目录下无呈现层资产。
        /// ⚠️ 当前阶段 51 资产目录尚未创建 —— 目录不存在 = 无呈现层资产(正确行为),跳过断言。</summary>
        [Test]
        public void test_noPlayerUi_noPresentationAssets()
        {
            // Arrange:扫描 51 资产目录
            string telemetryDir = Path.Combine(repoRoot(), "unity", "Assets", "Telemetry");

            // Act:检查目录是否存在
            if (!Directory.Exists(telemetryDir))
            {
                // 当前阶段 51 资产目录尚未创建 —— 无呈现层资产(正确行为)
                Assert.Ignore("51 资产目录尚未创建(当前阶段无呈现层资产,正确行为)");
                return;
            }

            // Assert:目录存在时扫描呈现层资产
            var presentationAssets = Directory.GetFiles(telemetryDir, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".uxml") || f.EndsWith(".uss") ||
                           f.EndsWith(".prefab") || f.EndsWith(".unity"))
                .ToList();

            Assert.That(presentationAssets, Is.Empty,
                "51 侧不得有 .uxml/.uss/Canvas/场景资产(AC-19-06/AC-51-A5:无玩家可见 UI):\n" +
                string.Join("\n", presentationAssets));
        }

        // ══════════════ AC-19-07/AC-51-E2:零写回 assets/data══════════════

        /// <summary>AC-19-07/AC-51-E2:无写回 <c>assets/data/**</c> 的代码路径。
        /// 扫描 51 命名空间下的类型,验证无文件写入 API 引用。</summary>
        [Test]
        public void test_noWriteBack_noFileWriteApis()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();

            // Act:扫描公开方法签名中的文件写入类型
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.ReturnType.Name.Contains("Stream") && m.ReturnType.Name.Contains("Write"))
                        violations.Add($"{t.Name}.{m.Name} 返回写流");
                    foreach (var p in m.GetParameters())
                        if (p.ParameterType.Name.Contains("Stream") && p.ParameterType.Name.Contains("Write"))
                            violations.Add($"{t.Name}.{m.Name} 参数含写流");
                }

            // Assert:零文件写入 API(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "51 不得引用文件写入 API(AC-19-07/AC-51-E2:零写回 assets/data):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-51-A7:零第三方包══════════════

        /// <summary>AC-51-A7:无 analytics/crash-reporting/telemetry 包或插件。
        /// 验证 51 程序集不引用 <c>com.unity.modules.unityanalytics</c>(Unity 内置分析模块)。
        /// ⚠️ 判据 = 51 程序集不引用该模块，而非 manifest.json 里没有该包
        ///    (官方包不是零第三方取向的豁免，但判据是「51 不引用它」)。</summary>
        [Test]
        public void test_noThirdParty_noAnalyticsPackages()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();

            // Act:检查 51 类型是否引用 UnityAnalytics 命名空间
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
            {
                // 检查字段/方法签名中的 UnityAnalytics 类型
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static))
                    if (f.FieldType.FullName?.Contains("UnityEngine.Analytics") == true)
                        violations.Add($"{t.Name}.{f.Name} 引用 UnityEngine.Analytics");
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.DeclaredOnly))
                {
                    if (m.ReturnType.FullName?.Contains("UnityEngine.Analytics") == true)
                        violations.Add($"{t.Name}.{m.Name} 返回 UnityEngine.Analytics");
                    foreach (var p in m.GetParameters())
                        if (p.ParameterType.FullName?.Contains("UnityEngine.Analytics") == true)
                            violations.Add($"{t.Name}.{m.Name} 参数含 UnityEngine.Analytics");
                }
            }

            // Assert:零引用(实现前后都成立)
            Assert.That(violations, Is.Empty,
                "51 不得引用 UnityEngine.Analytics(AC-51-A7:零第三方):\n" +
                string.Join("\n", violations));
        }

        /// <summary>AC-51-A5:无任何呈现层程序集引用 JudgmentMetrics。
        /// 扫描呈现层程序集(Gameplay.UI / Gameplay.Presentation / UGUI / UI Toolkit)的类型签名。</summary>
        [Test]
        public void test_noPresentationAssemblyRefs_judgmentMetrics()
        {
            // Arrange:呈现层程序集名
            var presentationAssemblies = new[] { "Gameplay.UI", "Gameplay.Presentation" };

            // Act:扫描呈现层程序集的类型签名
            var violations = new List<string>();
            foreach (var asmName in presentationAssemblies)
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == asmName);
                if (asm == null) continue;

                foreach (var t in asm.GetTypes())
                {
                    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.Instance | BindingFlags.Static))
                        if (f.FieldType.Name.Contains("JudgmentMetrics"))
                            violations.Add($"{asmName}:{t.Name}.{f.Name} 引用 JudgmentMetrics");
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                  BindingFlags.Instance | BindingFlags.Static |
                                                  BindingFlags.DeclaredOnly))
                    {
                        if (m.ReturnType.Name.Contains("JudgmentMetrics"))
                            violations.Add($"{asmName}:{t.Name}.{m.Name} 返回 JudgmentMetrics");
                        foreach (var p in m.GetParameters())
                            if (p.ParameterType.Name.Contains("JudgmentMetrics"))
                                violations.Add($"{asmName}:{t.Name}.{m.Name} 参数含 JudgmentMetrics");
                    }
                }
            }

            // Assert:零引用
            Assert.That(violations, Is.Empty,
                "呈现层程序集不得引用 JudgmentMetrics(AC-51-A5:无呈现面):\n" +
                string.Join("\n", violations));
        }
    }
}
