// case-system Story 001 测试 —— 病例流 Kind 登记与病例身份
//
// AC-37-01:病例流任意 CaseOpened 的 (Tick,Patient,Seq) 全流唯一;任何被引用的 case_id 等于某条 CaseOpened 三元组
// AC-37-02:两病人同 tick 各立案 ⇒ 两个 case_id 不同(两人各有 Seq=0,Patient 区分,不撞号)
// AC-37-03:kindgen 断言批 A1–A5 全过
// AC-37-04:载荷形状断言 —— CaseOpened/CaseClosed 载荷中无 opened_tick 字段;PatternRecognized 载荷含 salted_key
// AC-37-05:跨流全序复验 —— (Tick,StreamPriority,Patient,Seq) 排序确定且无平局(哨兵 -1 行可排序不崩)
// AC-37-06:高水位纯净 —— 含哨兵行的流上 max(patient_id) 重构不受 -1 污染(ADR-007 §四)
//
// 权威来源:
//   ADR-008(病例事件流;五 Kind 落病例流;case_id 三元组;opened_tick 删除;跨流全序;PatientId.None 哨兵)
//   ADR-024(Kind 单一登记真源 = entities.yaml;kindgen 生成 StreamRouting.g.cs + 断言 A1–A5)
//   ADR-007 §四(PatientId.None = -1 世界级事件不污染 max(patient_id) 高水位)
//   ADR-006(全序键 (Tick, StreamPriority, Patient, Seq);少一份可失步的状态)
//
// ⚠️ 本测试为纯数据层:不触引擎 API,不触 codec,不触持久化。
//    被测对象 = Sim.Contracts 的 CaseId / DiseaseIdSet / EventOrderKey / StreamRouting / EventKind。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary>case-system Story 001 —— 病例流 Kind 登记与病例身份。</summary>
    [TestFixture]
    public sealed class CaseIdentityRoutingTest
    {
        // ── 辅助:构造 SimEvent(header 层,PayloadRef 用零值 —— 本故事不测载荷编码)──

        private static SimEvent MakeEvent(long tick, PatientId patient, long seq, EventKind kind)
        {
            return new SimEvent(tick, patient, seq, kind, default(PayloadRef));
        }

        private static CaseId MakeCaseId(long tick, int patient, long seq)
        {
            return new CaseId(tick, patient, seq);
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-01:case_id 三元组全流唯一
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-01:同 (Tick,Patient) 两条 CaseOpened(Seq 0/1)+ 另一 Patient 同 Tick(Seq 0)
        /// ⇒ 三者两两不同;任何 Seq 单独取值会撞(断言测试故意展示)。</summary>
        [Test]
        public void test_ac3701_caseIdTriple_uniqueAcrossStream()
        {
            var opened = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(1), 1, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseOpened),
            };

            var caseIds = opened
                .Select(e => MakeCaseId(e.Tick, e.Patient.Value, e.Seq))
                .ToList();

            // 三者两两不同
            Assert.That(caseIds[0], Is.Not.EqualTo(caseIds[1]), "同 (Tick,Patient) Seq 0/1 须不同");
            Assert.That(caseIds[0], Is.Not.EqualTo(caseIds[2]), "不同 Patient 同 Tick Seq 0 须不同");
            Assert.That(caseIds[1], Is.Not.EqualTo(caseIds[2]), "不同 Patient + 不同 Seq 须不同");

            // 任何 Seq 单独取值会撞(断言测试故意展示)
            var seqOnly = opened.Select(e => e.Seq).ToList();
            Assert.That(seqOnly.Distinct().Count(), Is.LessThan(seqOnly.Count),
                "Seq 单独取值会撞号 —— 证明 case_id 必须是三元组,不能只用 Seq");
        }

        /// <summary>AC-37-01:任何被引用的 case_id 等于某条 CaseOpened 三元组。</summary>
        [Test]
        public void test_ac3701_referencedCaseId_equalsSomeOpenedTriple()
        {
            var opened = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(1), 1, EventKind.CaseOpened),
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseOpened),
            };

            var openedSet = opened
                .Select(e => MakeCaseId(e.Tick, e.Patient.Value, e.Seq))
                .ToHashSet();

            // 模拟 CaseClosed 引用的 case_id
            var referenced = MakeCaseId(100, 1, 1);
            Assert.That(openedSet.Contains(referenced), Is.True,
                "被引用的 case_id 必须等于某条 CaseOpened 三元组");

            // 引用一个不存在的 case_id 须失败
            var nonExistent = MakeCaseId(999, 9, 9);
            Assert.That(openedSet.Contains(nonExistent), Is.False,
                "不存在的 case_id 不在 opened 集中");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-02:两病人同 tick 各立案 ⇒ 不撞号
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-02:两病人同 tick 各立案 ⇒ 两个 case_id 不同(两人各有 Seq=0,Patient 区分)。</summary>
        [Test]
        public void test_ac3702_twoPatientsSameTick_caseIdsDiffer()
        {
            var opened = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseOpened),
            };

            var caseIds = opened
                .Select(e => MakeCaseId(e.Tick, e.Patient.Value, e.Seq))
                .ToList();

            Assert.That(caseIds[0], Is.Not.EqualTo(caseIds[1]),
                "两病人同 tick 各立案 ⇒ Patient 区分,不撞号");
            Assert.That(caseIds[0].Patient, Is.EqualTo(1));
            Assert.That(caseIds[1].Patient, Is.EqualTo(2));
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-03:kindgen 断言批 A1–A5
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-03 A1:五 Case Kind 的 stream 全为 case。</summary>
        [Test]
        public void test_ac3703_a1_fiveCaseKinds_routeToCaseStream()
        {
            var caseKinds = new[]
            {
                EventKind.CaseOpened,
                EventKind.CaseClosed,
                EventKind.PatternRecognized,
                EventKind.JudgmentRecorded,
                EventKind.JudgmentRevised,
            };

            foreach (var kind in caseKinds)
            {
                Assert.That(StreamRouting.Of(kind), Is.EqualTo(StreamId.Case),
                    $"{kind} 须路由到病例流(ADR-008 §一)");
            }
        }

        /// <summary>AC-37-03 A2:五 Case Kind 的 payload_schema ∈ 整数域(无 float/double)。
        /// 本测试从 entities.yaml 反射读取 payload_schema 并扫描 float/double 字样。</summary>
        [Test]
        public void test_ac3703_a2_casePayloadSchemas_integerDomainOnly()
        {
            var schemas = GetCasePayloadSchemas();

            Assert.That(schemas.Count, Is.EqualTo(5), "五 Case Kind 须有 payload_schema");

            foreach (var (kind, schema) in schemas)
            {
                // 扫描 float/double 字样(否定语境豁免:前 4 字内含 无/禁/非/零/不)
                foreach (System.Text.RegularExpressions.Match match in
                    System.Text.RegularExpressions.Regex.Matches(schema, @"[Ff]loat|[Dd]ouble"))
                {
                    string ctx = schema.Substring(Math.Max(0, match.Index - 4),
                        Math.Min(4, match.Index));
                    bool negation = System.Text.RegularExpressions.Regex.IsMatch(ctx, "[无禁非零不]");
                    Assert.That(negation, Is.True,
                        $"{kind} 的 payload_schema 出现非否定语境的 float/double —— {schema}");
                }
            }
        }

        /// <summary>AC-37-03 A3:Case Kind 名无重复（从 entities.yaml 读取真源验证）。</summary>
        [Test]
        public void test_ac3703_a3_caseKindNames_unique()
        {
            var names = GetCaseKindNamesFromRegistry();

            Assert.That(names.Count, Is.EqualTo(5), "registry 须有 5 个 case 流 Kind");
            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count),
                "Case Kind 名无重复(ADR-024 A3)");
        }

        /// <summary>AC-37-03 A4:五 Case Kind 的 author 必填且首整数 = 37。</summary>
        [Test]
        public void test_ac3703_a4_caseKindAuthors_all37()
        {
            var authors = GetCaseAuthors();

            Assert.That(authors.Count, Is.EqualTo(5), "五 Case Kind 须有 author");

            foreach (var (kind, author) in authors)
            {
                Assert.That(author, Does.StartWith("37"),
                    $"{kind} 的 author 须以 37 开头(37 病例系统)");
            }
        }

        /// <summary>AC-37-03 A5:registry ↔ EventKind.cs 双向差集归零。
        /// 正向:registry 中每个 case Kind 在 EventKind 枚举中存在;
        /// 反向:EventKind 中每个 case 流成员在 registry 中存在。</summary>
        [Test]
        public void test_ac3703_a5_caseKinds_presentInEventKindEnum()
        {
            var registryNames = GetCaseKindNamesFromRegistry();

            // 正向:registry → EventKind 枚举
            var eventKindType = typeof(EventKind);
            foreach (var name in registryNames)
            {
                Assert.That(eventKindType.GetField(name, BindingFlags.Public | BindingFlags.Static),
                    Is.Not.Null, $"{name} 须在 EventKind 枚举中存在(ADR-024 A5)");
            }

            // 反向:EventKind 枚举 → registry（双向差集归零）
            var enumCaseKindNames = new List<string>();
            foreach (var field in eventKindType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.Name.StartsWith("Case") || field.Name.StartsWith("PatternRecognized") ||
                    field.Name.StartsWith("Judgment"))
                {
                    enumCaseKindNames.Add(field.Name);
                }
            }

            var registrySet = new HashSet<string>(registryNames);
            var enumSet = new HashSet<string>(enumCaseKindNames);

            var registryOnly = registrySet.Except(enumSet).ToList();
            var enumOnly = enumSet.Except(registrySet).ToList();

            Assert.That(registryOnly, Is.Empty,
                $"registry 有但 EventKind 枚举无: {string.Join(", ", registryOnly)}");
            Assert.That(enumOnly, Is.Empty,
                $"EventKind 枚举有但 registry 无: {string.Join(", ", enumOnly)}");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-04:载荷形状断言
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-04:CaseOpenedPayload / CaseClosedPayload 载荷中无 opened_tick 字段
        /// (别名纪律 —— tick 只在 SimEvent.Tick 一处)。</summary>
        [Test]
        public void test_ac3704_payloads_noOpenedTickField()
        {
            var openedType = typeof(CaseOpenedPayload);
            var closedType = typeof(CaseClosedPayload);

            Assert.That(openedType.GetField("OpenedTick", BindingFlags.Public | BindingFlags.Instance),
                Is.Null, "CaseOpenedPayload 不得有 opened_tick 字段(别名纪律)");
            Assert.That(closedType.GetField("OpenedTick", BindingFlags.Public | BindingFlags.Instance),
                Is.Null, "CaseClosedPayload 不得有 opened_tick 字段(别名纪律)");

            // tick 只在 SimEvent.Tick 一处
            var simEventType = typeof(SimEvent);
            Assert.That(simEventType.GetField("Tick", BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, "SimEvent.Tick 须存在(全案唯一 tick 来源)");
        }

        /// <summary>AC-37-04:PatternRecognizedPayload 载荷含 salted_key(AC-37-14 的载体半边在此建)。</summary>
        [Test]
        public void test_ac3704_patternRecognizedPayload_containsSaltedKey()
        {
            var patternType = typeof(PatternRecognizedPayload);

            Assert.That(patternType.GetField("SaltedKey", BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, "PatternRecognizedPayload 须含 SaltedKey 字段(AC-37-14 载体)");

            // salted_key 类型 = u64(ulong)
            var field = patternType.GetField("SaltedKey", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field.FieldType, Is.EqualTo(typeof(ulong)),
                "SaltedKey 类型须为 ulong(u64)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-05:跨流全序复验
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-05:构造病史/病例交错流,(Tick,StreamPriority,Patient,Seq) 排序确定且无平局
        /// (哨兵 -1 行可排序不崩)。</summary>
        [Test]
        public void test_ac3705_crossStreamOrder_deterministicNoTies()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),      // case
                MakeEvent(100, new PatientId(1), 0, EventKind.InjuryOnset),     // history
                MakeEvent(100, PatientId.None, 0, EventKind.PatternRecognized), // case, 哨兵 -1
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseOpened),      // case
                MakeEvent(100, new PatientId(1), 0, EventKind.ActorCellEntered),// world
                MakeEvent(99,  new PatientId(1), 0, EventKind.CaseOpened),      // case, 更早 tick
            };

            var keys = events.Select(e => EventOrder.KeyOf(e)).ToList();

            // 排序确定:相邻两两不同(无平局)
            var sorted = keys.OrderBy(k => k).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                Assert.That(sorted[i], Is.LessThan(sorted[i + 1]),
                    "跨流全序键须无平局(ADR-008 §二)");
            }

            // 哨兵 -1 行可排序不崩
            var sentinelKey = EventOrder.KeyOf(events[2]);
            Assert.That(sentinelKey.Patient, Is.EqualTo(PatientId.None));
            Assert.That(sentinelKey.Patient.Value, Is.EqualTo(-1));

            // 验证排序:tick 99 最早
            Assert.That(sorted[0].Tick, Is.EqualTo(99), "tick 99 最早");
        }

        /// <summary>AC-37-05:StreamPriority 序 = History(0) &lt; Case(1) &lt; World(2)。</summary>
        [Test]
        public void test_ac3705_streamPriority_orderHistoryCaseWorld()
        {
            Assert.That((int)StreamId.History, Is.LessThan((int)StreamId.Case),
                "History(0) < Case(1)");
            Assert.That((int)StreamId.Case, Is.LessThan((int)StreamId.World),
                "Case(1) < World(2)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-06:高水位纯净
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-06:含哨兵行的流上 max(patient_id) 重构不受 -1 污染(ADR-007 §四)。</summary>
        [Test]
        public void test_ac3706_highWaterMark_sentinelMinusOne_noPollution()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(5), 0, EventKind.CaseOpened),
                MakeEvent(100, PatientId.None, 0, EventKind.PatternRecognized), // 哨兵 -1
                MakeEvent(100, new PatientId(3), 0, EventKind.CaseOpened),
            };

            // max(patient_id) 重构:扫三流并集内全部 patient id
            var maxPatientId = events
                .Where(e => !e.Patient.IsNone)
                .Select(e => e.Patient.Value)
                .Max();

            Assert.That(maxPatientId, Is.EqualTo(5),
                "max(patient_id) 须忽略哨兵 -1(ADR-007 §四)");

            // 哨兵 -1 不得被当作合法 id
            var sentinel = events[2].Patient;
            Assert.That(sentinel.IsNone, Is.True);
            Assert.That(sentinel.Value, Is.EqualTo(-1));
        }

        // ════════════════════════════════════════════════════════════════════
        // 辅助:从 entities.yaml 读取五 Case Kind 的 payload_schema / author
        // ════════════════════════════════════════════════════════════════════

        /// <summary>从 entities.yaml 读取所有 stream: case 的 Kind 名。</summary>
        private static List<string> GetCaseKindNamesFromRegistry()
        {
            var result = new List<string>();
            var yamlPath = FindRepoPath("design/registry/entities.yaml");
            var lines = System.IO.File.ReadAllLines(yamlPath);

            string currentKind = null;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("- name: SimEvent.Kind."))
                {
                    currentKind = trimmed.Substring("- name: SimEvent.Kind.".Length).Trim();
                }
                else if (currentKind != null && trimmed.StartsWith("stream:"))
                {
                    var stream = trimmed.Substring("stream:".Length).Trim();
                    if (stream == "case")
                        result.Add(currentKind);
                    currentKind = null;
                }
            }
            return result;
        }

        private static List<(string Kind, string Schema)> GetCasePayloadSchemas()
        {
            var result = new List<(string, string)>();
            var yamlPath = FindRepoPath("design/registry/entities.yaml");
            var lines = System.IO.File.ReadAllLines(yamlPath);

            string currentKind = null;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("- name: SimEvent.Kind."))
                {
                    currentKind = trimmed.Substring("- name: SimEvent.Kind.".Length).Trim();
                }
                else if (currentKind != null && trimmed.StartsWith("payload_schema:"))
                {
                    var schema = trimmed.Substring("payload_schema:".Length).Trim().Trim('"');
                    if (currentKind.StartsWith("Case") || currentKind.StartsWith("PatternRecognized") ||
                        currentKind.StartsWith("Judgment"))
                    {
                        result.Add((currentKind, schema));
                        currentKind = null;
                    }
                }
            }
            return result;
        }

        private static List<(string Kind, string Author)> GetCaseAuthors()
        {
            var result = new List<(string, string)>();
            var yamlPath = FindRepoPath("design/registry/entities.yaml");
            var lines = System.IO.File.ReadAllLines(yamlPath);

            string currentKind = null;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("- name: SimEvent.Kind."))
                {
                    currentKind = trimmed.Substring("- name: SimEvent.Kind.".Length).Trim();
                }
                else if (currentKind != null && trimmed.StartsWith("author:"))
                {
                    var author = trimmed.Substring("author:".Length).Length > 0
                        ? trimmed.Substring("author:".Length).Trim().Trim('"')
                        : "";
                    if (currentKind.StartsWith("Case") || currentKind.StartsWith("PatternRecognized") ||
                        currentKind.StartsWith("Judgment"))
                    {
                        result.Add((currentKind, author));
                        currentKind = null;
                    }
                }
            }
            return result;
        }

        private static string FindRepoPath(string relativePath)
        {
            // EditMode 测试的 AppDomain.BaseDirectory 不在仓库根;
            // 用 Application.dataPath(unity/Assets)向上两级到仓库根。
            var assetsDir = UnityEngine.Application.dataPath; // .../unity/Assets
            var unityDir = System.IO.Path.GetDirectoryName(assetsDir); // .../unity
            var repoRoot = System.IO.Path.GetDirectoryName(unityDir); // 仓库根
            var candidate = System.IO.Path.Combine(repoRoot, relativePath);
            if (System.IO.File.Exists(candidate))
                return candidate;
            throw new System.IO.FileNotFoundException($"找不到 {relativePath} (尝试: {candidate})");
        }
    }
}
