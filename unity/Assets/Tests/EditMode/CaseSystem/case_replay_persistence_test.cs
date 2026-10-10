// case-system Story 006 —— 重放持久化与跨系统边界义务。
//
// 权威来源:production/epics/case-system/story-006-replay-persistence-boundary.md
//   · GDD design/gdd/case-system.md 规则十一 重放/持久化边界 · EC-37-6 主机迁移 · D-37-B
//   · AC-37-06(Mono 半边)· 不折叠验证 · 高水位联查 · 53 边界两条 · D-37-B 转登
//   · ADR-010(序列化/折叠)· ADR-005(逐位重放)· ADR-008(不折叠裁决)· ADR-029(载荷编码)
//
// 被测对象 = 生产码(非测试私有副本):
//   · 序列化:Sim.Codec.SaveCodec(:ISaveCodec,ADR-010 §一)+ SimEventCodec(header)
//   · 载荷:Sim.Codec.PayloadCodec.Case.cs 五支真编码 + InMemoryBlobPool(ADR-029 测试池)
//   · 高水位:Sim.EventStream.GetNextPatientId()(生产唯一实现)
//   · 判据接口:Sim.CaseStreamQuery.HasOpenCase(ADR-008 §六 条件 ③ 的 37 侧载体)
//
// 覆盖(Mono 半边;IL2CPP 半边 BLOCKED-BY-ADR-012 矩阵批,禁借绿):
//   · AC-37-06 病例流 round-trip 字节级相等 + 载荷经 blob 池可还原
//   · 不折叠验证:同一 patient_id 既往全部病例行完整可数(具体值断言,非自比)
//   · 高水位联查:EventStream.GetNextPatientId() = 三流并集 max+1,哨兵不污染
//   · 53 边界:37 出站载荷无延迟/归因语义字段
//   · D-37-B 转登:GDD 登记存在(转 9/7a 半边 NOT-RUN,见 story 回填)
//
// ⚠️ 载体未建齐的条目记 NOT-RUN,禁空集绿(卡 Guardrail):
//   · 7a `Folded(p)` 生产谓词不存在(grep 零命中)⇒ 本测只验 37 侧判据接口 HasOpenCase
//   · D-37-B 转登 9/7a:两 GDD 零命中 ⇒ 该半边 NOT-RUN

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.CaseSystem
{
    [TestFixture]
    public sealed class CaseReplayPersistenceTest
    {
        private const long WorldSeed = 0x5EED_1234_5678_9ABCL;

        // ══════════════════════════════════════════════════════════════════════════
        // AC-37-06:病例流事件 round-trip 字节级相等 + 载荷可还原
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_ac37_06_caseStreamEvents_roundTripBytesEqual()
        {
            // Arrange:真实载荷经 PayloadEncoder/PayloadCodec 入 blob 池
            var pool = new InMemoryBlobPool();
            var codec = new SaveCodec();
            var events = BuildCompositeCaseStream(pool);

            // Act:encode → decode → encode
            byte[] encoded1 = EncodeStream(events, codec);
            var decoded = DecodeStream(encoded1, codec);
            byte[] encoded2 = EncodeStream(decoded, codec);

            // Assert:字节级相等(AC-37-06 Mono 半边)
            Assert.AreEqual(encoded1, encoded2, "病例流 round-trip 字节级相等(AC-37-06 Mono 半边)");
            Assert.AreEqual(events.Count, decoded.Count, "重放后事件数逐位同");

            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreEqual(events[i].Tick, decoded[i].Tick, $"事件[{i}].Tick 逐位同");
                Assert.AreEqual(events[i].Patient.Value, decoded[i].Patient.Value, $"事件[{i}].Patient 逐位同");
                Assert.AreEqual(events[i].Seq, decoded[i].Seq, $"事件[{i}].Seq 逐位同");
                Assert.AreEqual(events[i].Kind, decoded[i].Kind, $"事件[{i}].Kind 逐位同");
                // PayloadRef 三元组逐位同(ADR-006 G-2:引用进头 / 内容进池)
                Assert.AreEqual(events[i].Payload.BlobId, decoded[i].Payload.BlobId, $"事件[{i}].Payload.BlobId 逐位同");
                Assert.AreEqual(events[i].Payload.Offset, decoded[i].Payload.Offset, $"事件[{i}].Payload.Offset 逐位同");
                Assert.AreEqual(events[i].Payload.Length, decoded[i].Payload.Length, $"事件[{i}].Payload.Length 逐位同");
            }

            // 载荷内容经 blob 池可还原(证明引用非悬垂)
            Assert.IsTrue(PayloadCodec.TryGetPayload(decoded[0], pool, out CaseOpenedPayload opened),
                "CaseOpened 载荷经池可还原");
            Assert.AreEqual(1, opened.PatientId, "还原的 CaseOpened.PatientId 逐位同");
        }

        [Test]
        public void test_case_ac37_06_emptyCaseStream_roundTrip()
        {
            // 空病例流合法(计数 0)
            var codec = new SaveCodec();
            var emptyEvents = new List<SimEvent>();

            byte[] encoded = EncodeStream(emptyEvents, codec);
            var decoded = DecodeStream(encoded, codec);

            Assert.IsEmpty(decoded, "空病例流 round-trip 后仍为空");
            Assert.AreEqual(4, encoded.Length, "空病例流编码 = 4 字节计数前缀(int32 0)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 不折叠验证:同一 patient_id 既往全部病例行完整可数
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_noFold_historicalRowsComplete()
        {
            // Arrange:病人 1 两案全结(若终态折叠,历史 CaseOpened/CaseClosed 会被吸收)
            var pool = new InMemoryBlobPool();
            var codec = new SaveCodec();
            var events = BuildCompositeCaseStream(pool);

            int expectedPatient1Rows = events.Count(e =>
                e.Patient.Value == 1 &&
                (e.Kind == EventKind.CaseOpened || e.Kind == EventKind.CaseClosed));
            Assert.AreEqual(4, expectedPatient1Rows,
                "病人 1 病例行数 = 2 开案 + 2 结案(具体值断言,非自比)");

            // Act:重放
            var decoded = DecodeStream(EncodeStream(events, codec), codec);

            // Assert:历史行完整(ADR-008 §六:病例流不物理折叠)
            int decodedPatient1Rows = decoded.Count(e =>
                e.Patient.Value == 1 &&
                (e.Kind == EventKind.CaseOpened || e.Kind == EventKind.CaseClosed));
            Assert.AreEqual(4, decodedPatient1Rows,
                "不折叠:重放后病人 1 病例行数仍为 4(无历史行被终态吸收)");
            // 开案/结案各自计数,防「一对抵消」掩盖丢失
            Assert.AreEqual(2, decoded.Count(e => e.Patient.Value == 1 && e.Kind == EventKind.CaseOpened),
                "病人 1 开案行 2 条俱在");
            Assert.AreEqual(2, decoded.Count(e => e.Patient.Value == 1 && e.Kind == EventKind.CaseClosed),
                "病人 1 结案行 2 条俱在");
        }

        [Test]
        public void test_case_noFold_openCasePredicate_bothDirections()
        {
            // ADR-008 §六 条件 ③「无未结案病例」的 37 侧判据接口 = CaseStreamQuery.HasOpenCase。
            // ⚠️ 7a 的 Folded(p) 生产谓词不存在(grep 零命中)⇒ 折叠执行半边 NOT-RUN(见 story 回填)。
            var pool = new InMemoryBlobPool();

            // 方向一:有未结案病例 ⇒ HasOpenCase = true ⇒ Folded 必为 false(条件 ③ 不满足)
            var withOpen = new List<SimEvent>
            {
                MakeCaseEvent(10, 1, 0, EventKind.CaseOpened, pool),
                // 无 CaseClosed ⇒ 该案仍开
            };
            Assert.IsTrue(CaseStreamQuery.HasOpenCase(withOpen, 1),
                "有未结案病例 ⇒ HasOpenCase = true ⇒ Folded(p) = false");

            // 方向二:全部结案 ⇒ HasOpenCase = false ⇒ 条件 ③ 满足(不折叠与否取决于 ①②,归 7a)
            var allClosed = new List<SimEvent>
            {
                MakeCaseEvent(10, 1, 0, EventKind.CaseOpened, pool),
                MakeCaseEvent(20, 1, 1, EventKind.CaseClosed, pool),
            };
            Assert.IsFalse(CaseStreamQuery.HasOpenCase(allClosed, 1),
                "全部结案 ⇒ HasOpenCase = false(条件 ③ 满足)");

            // 反向:结案后重开 ⇒ 又有未结案
            var reopened = new List<SimEvent>
            {
                MakeCaseEvent(10, 1, 0, EventKind.CaseOpened, pool),
                MakeCaseEvent(20, 1, 1, EventKind.CaseClosed, pool),
                MakeCaseEvent(30, 1, 2, EventKind.CaseOpened, pool),
            };
            Assert.IsTrue(CaseStreamQuery.HasOpenCase(reopened, 1),
                "结案后重开 ⇒ 又有未结案病例");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 高水位联查:病人 id 高水位 = 三流并集(生产 EventStream.GetNextPatientId)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_highWaterMark_threeStreamsUnion()
        {
            // Arrange:三流事件并入同一 EventStream(生产唯一真源)
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            var pool = new InMemoryBlobPool();

            // ⚠️ BCD-码-3(2026-10-10):真 EventStream 现按 Tick 非降断言(生产写路径
            // 恒时间序 Append)—— 三流事件须**按 tick 升序**入流,不再按流分组;
            // 高水位扫描与入流序无关,断言语义不变。注释标各条所属流:
            stream.Append(MakeEvent(3, PatientId.None.Value, 0, EventKind.ActorCellEntered, pool)); // 世界(哨兵)
            stream.Append(MakeEvent(5, 1, 0, EventKind.InjuryOnset, pool));                          // 病史
            stream.Append(MakeEvent(7, 5, 0, EventKind.ActorCellEntered, pool));                     // 世界(病人 5 = 全局最大)
            stream.Append(MakeEvent(8, 2, 0, EventKind.InjuryOnset, pool));                          // 病史
            stream.Append(MakeCaseEvent(10, 1, 0, EventKind.CaseOpened, pool));                      // 病例
            stream.Append(MakeEvent(11, 4, 0, EventKind.InjuryOnset, pool));                         // 病史
            stream.Append(MakeCaseEvent(12, 2, 0, EventKind.CaseOpened, pool));                      // 病例
            stream.Append(MakeCaseEvent(14, 3, 0, EventKind.CaseOpened, pool));                      // 病例
            stream.Append(MakeEvent(50, PatientId.None.Value, 0, EventKind.PatternRecognized, pool)); // 病例(哨兵)

            // Act:生产高水位函数
            PatientId next = stream.GetNextPatientId();

            // Assert:三流并集 max = 5(世界流)⇒ next = 6;哨兵 -1 不污染
            Assert.AreEqual(6, next.Value,
                "高水位 = 三流并集 max(5) + 1 = 6(哨兵 -1 不污染)");
        }

        [Test]
        public void test_case_highWaterMark_sentinelOnly_returnsZero()
        {
            // 只含哨兵行的流 ⇒ 无合法 id ⇒ next = 0(-1 + 1),证明哨兵确被忽略
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            var pool = new InMemoryBlobPool();
            stream.Append(MakeEvent(3, PatientId.None.Value, 0, EventKind.ActorCellEntered, pool));
            stream.Append(MakeEvent(50, PatientId.None.Value, 0, EventKind.PatternRecognized, pool));

            Assert.AreEqual(0, stream.GetNextPatientId().Value,
                "只含哨兵(-1)的流 ⇒ 高水位 next = 0,哨兵未被计入");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 53 边界:37 出站仅事件流本身(载荷面 = 无延迟/归因语义字段)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_53Boundary_payloadsCarryNoLatencySemantics()
        {
            // AC-37-25:图样触达玩家的时机/延迟/不可归因全归 53 ⇒ 37 发出物无延迟语义字段
            // (Implementation Notes #4:断言写法 = 「37 发出物无延迟语义字段」(载荷面))
            var payloads = new[]
            {
                typeof(CaseOpenedPayload), typeof(CaseClosedPayload),
                typeof(PatternRecognizedPayload), typeof(JudgmentRecordedPayload),
                typeof(JudgmentRevisedPayload),
            };
            var forbidden = new[] { "latency", "delay", "arrival", "attribution", "causation", "visible" };

            foreach (var payload in payloads)
            {
                var fields = payload.GetFields(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotEmpty(fields, $"{payload.Name} 字段集非空(空集绿守卫)");

                foreach (var f in fields)
                {
                    foreach (var token in forbidden)
                    {
                        Assert.IsFalse(
                            f.Name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0,
                            $"{payload.Name}.{f.Name} 含延迟/归因语义 token「{token}」(53 边界:该语义归 53)");
                    }
                }
            }
        }

        [Test]
        public void test_case_53Boundary_simAssemblyReferencesOnlyContracts()
        {
            // AC-37-25 架构面:37 出站仅事件流本身(无直接接口调用 53)。
            // ADR-025 §①:Sim 程序集引用集恰 = {BCL, Sim.Contracts} ⇒ 结构性无 53 侧程序集引用。
            var refs = typeof(EventStream).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name).ToList();
            Assert.IsNotEmpty(refs, "Sim 程序集引用集非空(空集绿守卫)");

            foreach (var r in refs)
            {
                Assert.IsFalse(
                    r.IndexOf("Consequence", StringComparison.OrdinalIgnoreCase) >= 0,
                    $"Sim 程序集不得引用 53(后果系统)侧程序集,实测引用:{r}");
            }
            Assert.IsTrue(refs.Any(r => r.IndexOf("Sim.Contracts", StringComparison.Ordinal) >= 0),
                "Sim 程序集须引用 Sim.Contracts(七抽象点载体,ADR-025 §①)");
        }

        [Test]
        public void test_case_53Boundary_patternRecognizedOutOfOrder_reorderedByTickInvariant()
        {
            // AC-37-28:PatternRecognized 按 Tick 聚合非到达序(哨兵 -1 前向悬垂)
            // 夹具:两条 PatternRecognized 以**乱序**到达,按 Tick 重排后解读不变
            var pool = new InMemoryBlobPool();
            var arrived = new List<SimEvent>
            {
                MakePatternEvent(60, 0xBBB, pool),   // 后 tick 先到
                MakePatternEvent(50, 0xAAA, pool),   // 先 tick 后到
            };
            Assert.AreEqual(2, arrived.Count(e => e.Kind == EventKind.PatternRecognized),
                "夹具含 2 条 PatternRecognized(非零迭代,防空集绿)");

            // 按 Tick 升序重排(生产全序键首字段)
            var reordered = arrived.OrderBy(e => e.Tick).ToList();

            // 重排后 salted_key 序列确定(与到达序无关)
            var arrivedKeys = arrived.Select(e => ExtractSaltedKey(e, pool)).ToList();
            var reorderedKeys = reordered.Select(e => ExtractSaltedKey(e, pool)).ToList();
            Assert.AreEqual(new ulong[] { 0xAAA, 0xBBB }, reorderedKeys.ToArray(),
                "按 Tick 重排后 salted_key 序 = 时间序(解读不变)");
            Assert.AreNotEqual(arrivedKeys.ToArray(), reorderedKeys.ToArray(),
                "到达序 ≠ 时间序(证明夹具确为乱序,断言非恒真)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // D-37-B 转登:GDD 登记存在(转 9/7a 半边 NOT-RUN)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_d37b_gddRegistrationPresent()
        {
            // D-37-B 在 37 GDD 已登记(design/gdd/case-system.md §Open Questions)
            string gdd = System.IO.File.ReadAllText(FindRepoPath("design/gdd/case-system.md"));

            Assert.IsTrue(gdd.Contains("D-37-B"),
                "37 GDD 含 D-37-B 登记条目");
            Assert.IsTrue(gdd.Contains("病史流有界性的行为学前提"),
                "D-37-B 标题 = 病史流有界性的行为学前提");
            Assert.IsTrue(gdd.Contains("9 / 7a / technical-director 会签"),
                "D-37-B 登记了转登目标(9/7a/technical-director)");
            // ⚠️ 转登 9/7a 的实际落盘半边 NOT-RUN(两 GDD 零命中)—— 见 story 回填
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 辅助:真实载荷 + 生产编解码
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>用生产 SaveCodec 编码事件流(计数前缀 + 逐事件长度前缀帧)。</summary>
        private static byte[] EncodeStream(List<SimEvent> events, ISaveCodec codec)
        {
            var w = new CodecWriter();
            w.WriteInt32LittleEndian(events.Count);
            foreach (var e in events)
                codec.WriteEvent(e, ref w);
            return w.ToArray();
        }

        /// <summary>用生产 SaveCodec 解码事件流。</summary>
        private static List<SimEvent> DecodeStream(byte[] data, ISaveCodec codec)
        {
            var r = new CodecReader(data);
            int count = r.ReadInt32LittleEndian();
            var list = new List<SimEvent>(count);
            for (int i = 0; i < count; i++)
                list.Add(codec.ReadEvent(ref r));
            return list;
        }

        /// <summary>构建复合病例流:病人 1 两案(含改写)+ 病人 2/3 各一案 + 两条图样。</summary>
        private static List<SimEvent> BuildCompositeCaseStream(InMemoryBlobPool pool)
        {
            var events = new List<SimEvent>();

            // 病人 1:案一 开 → 判断 → 结
            events.Add(MakeCaseEvent(10, 1, 0, EventKind.CaseOpened, pool));
            events.Add(MakeJudgmentEvent(15, 1, 1, EventKind.JudgmentRecorded, pool));
            events.Add(MakeCaseEvent(20, 1, 2, EventKind.CaseClosed, pool));
            // 病人 1:案二 开 → 判断 → 改写 → 结
            events.Add(MakeCaseEvent(25, 1, 3, EventKind.CaseOpened, pool));
            events.Add(MakeJudgmentEvent(30, 1, 4, EventKind.JudgmentRecorded, pool));
            events.Add(MakeJudgmentEvent(35, 1, 5, EventKind.JudgmentRevised, pool));
            events.Add(MakeCaseEvent(40, 1, 6, EventKind.CaseClosed, pool));

            // 病人 2 / 3:各一案
            events.Add(MakeCaseEvent(12, 2, 0, EventKind.CaseOpened, pool));
            events.Add(MakeCaseEvent(22, 2, 1, EventKind.CaseClosed, pool));
            events.Add(MakeCaseEvent(14, 3, 0, EventKind.CaseOpened, pool));
            events.Add(MakeCaseEvent(24, 3, 1, EventKind.CaseClosed, pool));

            // 图样(哨兵 -1)
            events.Add(MakePatternEvent(50, 0xAAA, pool));

            return events;
        }

        /// <summary>构造 CaseOpened / CaseClosed 事件(真实载荷)。</summary>
        private static SimEvent MakeCaseEvent(long tick, int patient, long seq, EventKind kind, InMemoryBlobPool pool)
        {
            if (kind == EventKind.CaseOpened)
            {
                var p = new CaseOpenedPayload(patient, new DiseaseIdSet(0b101UL));
                byte[] bytes = PayloadCodec.Encode(p);
                return new SimEvent(tick, new PatientId(patient), seq, kind, Store(bytes, pool));
            }
            else
            {
                var p = new CaseClosedPayload(patient, new CaseId(tick, patient, seq), new DiseaseIdSet(0b101UL), true);
                byte[] bytes = PayloadCodec.Encode(p);
                return new SimEvent(tick, new PatientId(patient), seq, kind, Store(bytes, pool));
            }
        }

        /// <summary>构造 Judgment 事件(真实载荷;freehand_text 经具名重载,ADR-006 G-3)。</summary>
        private static SimEvent MakeJudgmentEvent(long tick, int patient, long seq, EventKind kind, InMemoryBlobPool pool)
        {
            var caseId = new CaseId(tick, patient, seq);
            byte[] bytes = kind == EventKind.JudgmentRecorded
                ? PayloadCodec.Encode(new JudgmentRecordedPayload(patient, caseId, 0, 7, 3), "")
                : PayloadCodec.Encode(new JudgmentRevisedPayload(patient, caseId, 0, 7, 3), "");
            return new SimEvent(tick, new PatientId(patient), seq, kind, Store(bytes, pool));
        }

        /// <summary>构造 PatternRecognized 事件(哨兵 -1,真实载荷)。</summary>
        private static SimEvent MakePatternEvent(long tick, ulong saltedKey, InMemoryBlobPool pool)
        {
            var p = new PatternRecognizedPayload(
                PatientId.None.Value, new CaseId(tick, PatientId.None.Value, 0),
                saltedKey, new DiseaseIdSet(0b101UL), 1);
            byte[] bytes = PayloadCodec.Encode(p);
            return new SimEvent(tick, PatientId.None, 0, EventKind.PatternRecognized, Store(bytes, pool));
        }

        /// <summary>构造非病例载荷事件(病史/世界流,供高水位联查)。</summary>
        private static SimEvent MakeEvent(long tick, int patient, long seq, EventKind kind, InMemoryBlobPool pool)
        {
            // 载荷内容与高水位无关(高水位只读 Patient 字段);用最小合法载荷占位
            byte[] bytes = new byte[] { 0x01, 0x00 };
            return new SimEvent(tick, new PatientId(patient), seq, kind, Store(bytes, pool));
        }

        private static PayloadRef Store(byte[] bytes, InMemoryBlobPool pool)
        {
            int blobId = pool.Store(bytes);
            return new PayloadRef(blobId, 0, bytes.Length);
        }

        /// <summary>从 PatternRecognized 事件经 blob 池还原 salted_key。</summary>
        private static ulong ExtractSaltedKey(SimEvent e, InMemoryBlobPool pool)
        {
            Assert.IsTrue(PayloadCodec.TryGetPayload(e, pool, out PatternRecognizedPayload p),
                "PatternRecognized 载荷经池可还原");
            return p.SaltedKey;
        }

        private static string FindRepoPath(string relative)
        {
            var dir = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                string candidate = System.IO.Path.Combine(dir.FullName, relative);
                if (System.IO.File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
            throw new System.IO.FileNotFoundException($"未找到 {relative}(从测试目录向上搜索)");
        }

        // ── 测试桩(生产 EventStream 的依赖)────────────────────────────────────

        private sealed class FakeIdAuthority : IIdAuthority
        {
            private int _nextPatientId;
            private int _nextItemInstanceId;
            public PatientId NextPatientId() => new PatientId(_nextPatientId++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_nextItemInstanceId++);
        }

        private sealed class FakePresenceQuery : IPresenceQuery
        {
            private readonly HashSet<int> _present = new HashSet<int>();
            public bool IsPresent(PatientId patientId) => _present.Contains(patientId.Value);
            public int PresentCount => _present.Count;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => _present.ToArray();
            public void Add(PatientId patientId) => _present.Add(patientId.Value);
        }
    }
}
