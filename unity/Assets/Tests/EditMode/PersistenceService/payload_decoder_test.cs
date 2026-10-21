// ADR-031 契约支测试 —— 载荷解码抽象点(`IPayloadDecoder`)
//
// 权威来源:
//   ADR-031 §① —— 第八个 P0 抽象点 `IPayloadDecoder`(住 Sim.Contracts)
//   ADR-031 §② —— 形状 = **抛形** `T Decode<T>(EventKind, in PayloadRef) where T : struct`
//                 与字节级 `Decode<T>` / `Encode<T>` 对称;与既有 `TryGetPayload` 刻意分家
//                 (后者 bool 只承载「查无」,损坏本就走抛 ⇒ 本接口把「未驻留」也显式化)
//   ADR-031 §③ —— 池依赖在**实现的构造函数**,不在接口签名(承 ADR-029 §② 模式)
//   ADR-031 §④ —— 接口**不含** freehand 路径(ADR-006 G-3:禁 sim 读自由文本)
//   ADR-031 §⑤ —— b5 门补两项(`IPayloadEncoder` + `IPayloadDecoder`)
//   ADR-031 §Validation V-1…V-10
//   镜像先例:Tests/EditMode/PersistenceService/payload_encoder_test.cs(ADR-029 契约支)
//
// ⚠️ 本测试**只**断言 ADR-031 的契约面;不改任何既有读访问签名
//    (`TryGetPayload` / `TryGetFreehandText` 保留不动,服务呈现侧)。

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using Gates = DaYiJingCheng.EditorTools.Gates.AssemblyGates;

namespace DaYiJingCheng.Tests.PersistenceService
{
    /// <summary>
    /// ADR-031 契约支 —— `IPayloadDecoder` + `PayloadDecoder` + b5 门补项。
    /// </summary>
    public class PayloadDecoderTest
    {
        private InMemoryBlobPool _pool;
        private PayloadDecoder _decoder;
        private PayloadEncoder _encoder;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _decoder = new PayloadDecoder(_pool);
            _encoder = new PayloadEncoder(_pool);
        }

        // ══════════ V-1 / V-5: 签名与对称性 ══════════

        [Test]
        public void test_adr031_v1_decoderSignature_isThrowShape()
        {
            var dec = typeof(IPayloadDecoder).GetMethod("Decode");
            Assert.IsNotNull(dec, "IPayloadDecoder.Decode 应存在");
            Assert.IsTrue(dec.IsGenericMethodDefinition, "Decode 须为泛型方法");

            var gp = dec.GetGenericArguments();
            Assert.AreEqual(1, gp.Length, "Decode 恰一个泛型形参 T");

            var attrs = gp[0].GenericParameterAttributes;
            Assert.IsTrue((attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0,
                "T 须有 struct 约束");
            Assert.IsFalse(gp[0].GetGenericParameterConstraints().Any(c => c.IsInterface),
                "T 不得有接口约束(与 Encode<T> / Decode<T> 对称 —— ADR-029 评审修正 ②)");

            // 形参 = (EventKind, in PayloadRef) —— 恰 2 个
            var ps = dec.GetParameters();
            Assert.AreEqual(2, ps.Length, "Decode 须收 (EventKind, in PayloadRef) 两形参");
            Assert.AreEqual(typeof(EventKind), ps[0].ParameterType, "第一形参须为 EventKind");
            Assert.AreEqual(typeof(PayloadRef), ps[1].ParameterType.GetElementType(),
                "第二形参须为 in PayloadRef(最小充分集 —— 不收整个 SimEvent)");
            Assert.IsTrue(ps[1].ParameterType.IsByRef, "第二形参须为 in/ref 传参");

            // **抛形**:返回 T 而非 bool(out 形)—— ADR-031 §② 用户裁定
            Assert.AreEqual(gp[0], dec.ReturnType,
                "Decode 须**直接返回 T**(抛形,非 bool + out 的 Try 形)");
            Assert.IsFalse(ps.Any(p => p.IsOut),
                "不得有 out 形参 —— 抛形不承载 out(与 TryGetPayload 刻意分家)");
        }

        [Test]
        public void test_adr031_v1_signatureHasNoPoolParameter()
        {
            // §③ 的核心结构断言 —— 池**不进**接口签名(否则 Sim.Contracts 须引用 Sim.Codec)
            var dec = typeof(IPayloadDecoder).GetMethod("Decode");
            foreach (var p in dec.GetParameters())
            {
                var t = p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType;
                Assert.AreNotEqual("IBlobPool", t.Name,
                    "接口签名**不得**出现 IBlobPool(§③:池在实现构造函数)");
                Assert.AreNotEqual("IBlobSink", t.Name, "接口签名不得出现 IBlobSink");
            }
            Assert.AreNotEqual("IBlobPool", dec.ReturnType.Name, "返回类型不得为池类型");
        }

        [Test]
        public void test_adr031_v5_symmetricWithEncoder()
        {
            var encT = typeof(IPayloadEncoder).GetMethod("Encode").GetGenericArguments()[0];
            var decT = typeof(IPayloadDecoder).GetMethod("Decode").GetGenericArguments()[0];

            Assert.AreEqual(
                encT.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask,
                decT.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask,
                "Encode<T> 与 Decode<T>(抽象点)的特殊约束须相同");
            Assert.AreEqual(
                encT.GetGenericParameterConstraints().Length,
                decT.GetGenericParameterConstraints().Length,
                "两条泛型方法的约束条数须一致(对称)");
        }

        // ══════════ V-7: 接口不含 freehand 路径(§④)══════════

        [Test]
        public void test_adr031_v7_noFreehandPath_interfaceHasExactlyOneMethod()
        {
            var methods = typeof(IPayloadDecoder)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName)      // 排除属性访问器
                .ToArray();

            Assert.AreEqual(1, methods.Length,
                $"IPayloadDecoder 须恰一个方法,实测 {methods.Length}: " +
                string.Join(", ", methods.Select(m => m.Name)));
            Assert.AreEqual("Decode", methods[0].Name, "唯一方法须为 Decode");

            // 具名 freehand 读取方法**不得**存在于接口面
            Assert.IsNull(typeof(IPayloadDecoder).GetMethod("DecodeJudgmentFreehand"),
                "接口不得含 freehand 路径(§④:G-3 禁 sim 读自由文本)");
            Assert.IsNull(typeof(IPayloadDecoder).GetMethod("TryGetFreehandText"),
                "接口不得含 freehand 路径");
        }

        [Test]
        public void test_adr031_v7_freehandStillUnreachableFromSimSide()
        {
            // 对照:既有 TryGetFreehandText 仍住 Sim.Codec 的 PayloadCodec(呈现侧专用),
            // 本件**不改它** —— 它不在 Sim.Contracts 的接口面里,故 Sim 侧够不着。
            Assert.AreEqual("Sim.Codec", typeof(PayloadCodec).Assembly.GetName().Name,
                "PayloadCodec 仍住 Sim.Codec(本件不改既有读访问签名)");
            Assert.IsFalse(
                typeof(IPayloadDecoder).Assembly.GetTypes().Any(t => t.Name == "PayloadCodec"),
                "Sim.Contracts 内不得有 PayloadCodec");
        }

        // ══════════ §③: 池在实现构造函数 ══════════

        [Test]
        public void test_adr031_s3_decoderLivesInSimCodec_andInjectsPool()
        {
            Assert.IsInstanceOf<IPayloadDecoder>(_decoder, "PayloadDecoder 须实现 IPayloadDecoder");
            Assert.AreEqual("Sim.Codec", typeof(PayloadDecoder).Assembly.GetName().Name,
                "PayloadDecoder 须住 Sim.Codec(它看得见 PayloadCodec)");

            var ctor = typeof(PayloadDecoder).GetConstructors().Single();
            Assert.AreEqual(typeof(IBlobPool), ctor.GetParameters().Single().ParameterType,
                "构造须注入 IBlobPool(§③:池在实现构造函数)");
        }

        [Test]
        public void test_adr031_s3_nullPool_throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PayloadDecoder(null));
        }

        [Test]
        public void test_adr031_s3_blobPoolStillLivesInSimCodec()
        {
            // §③ 的收益:池**不搬家**
            Assert.AreEqual("Sim.Codec", typeof(IBlobPool).Assembly.GetName().Name,
                "IBlobPool 须仍住 Sim.Codec(§③:一字不改、不搬家)");
            Assert.AreEqual("Sim.Codec", typeof(IBlobSink).Assembly.GetName().Name,
                "IBlobSink 亦仍住 Sim.Codec");
            Assert.IsFalse(
                typeof(IPayloadDecoder).Assembly.GetTypes().Any(t => t.Name == "IBlobPool"),
                "Sim.Contracts 内不得有 IBlobPool(§③:搬进契约 = 开第二条旁路)");
        }

        // ══════════ V-1: 装配边界(Sim.Contracts 零 Sim.Codec)══════════

        [Test]
        public void test_adr031_v1_contractsHasNoCodecReference()
        {
            var contracts = typeof(IPayloadDecoder).Assembly;
            var refs = contracts.GetReferencedAssemblies().Select(r => r.Name).ToList();

            Assert.IsFalse(refs.Contains("Sim.Codec"),
                "Sim.Contracts 不得引用 Sim.Codec(ADR-025 §①:112 / ADR-031 V-1)");

            // Decode 的签名面只出现 Sim.Contracts 类型
            var dec = typeof(IPayloadDecoder).GetMethod("Decode");
            foreach (var p in dec.GetParameters())
            {
                var t = p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType;
                Assert.AreEqual(contracts.GetName().Name, t.Assembly.GetName().Name,
                    $"形参 {p.Name} 的类型 {t.Name} 须住 Sim.Contracts");
            }
        }

        [Test]
        public void test_adr031_v2_v3_asmdefsUnchanged()
        {
            // V-2:Sim 引用集仍 = ["Sim.Contracts"]
            string simPath = Path.Combine(Application.dataPath, "../Assets/Sim/Sim.asmdef");
            Assert.IsTrue(File.Exists(simPath), "Sim.asmdef 应存在");
            string simContent = File.ReadAllText(simPath);
            Assert.IsFalse(simContent.Contains("Sim.Codec"),
                "Sim.asmdef 不得引用 Sim.Codec(ADR-031 V-2:一字不改)");

            // V-3:Sim.Contracts 的 references 仍为空数组
            string contractsPath = Path.Combine(
                Application.dataPath, "../Assets/Sim.Contracts/Sim.Contracts.asmdef");
            Assert.IsTrue(File.Exists(contractsPath), "Sim.Contracts.asmdef 应存在");
            string contractsContent = File.ReadAllText(contractsPath);
            Assert.IsFalse(contractsContent.Contains("Sim.Codec"),
                "Sim.Contracts.asmdef 不得引用 Sim.Codec(ADR-031 V-3)");
            StringAssert.Contains("\"references\": []", contractsContent,
                "Sim.Contracts 的 references 须仍为空数组(ADR-031 V-3)");
        }

        // ══════════ 功能:编码 → 池 → 解码 往返 ══════════

        [Test]
        public void test_adr031_decode_roundTrip_poiStateChanged()
        {
            // 定向:与 encoder 测试同一载荷 —— 证两侧共用同一 codec 语义
            var reference = _encoder.Encode(EventKind.PoiStateChanged, new PoiStateChangedPayload(7, 2));
            var decoded = _decoder.Decode<PoiStateChangedPayload>(EventKind.PoiStateChanged, reference);

            Assert.AreEqual(7, decoded.PoiId, "PoiId 须往返一致");
            Assert.AreEqual(2, decoded.NewState, "NewState 须往返一致");
        }

        [Test]
        public void test_adr031_decode_usesRefOffsetAndLength()
        {
            // 证实现**真的**按 PayloadRef 的 Offset/Length 切片,而非整段读
            byte[] bytes = { 0xAA, 0xBB, 0xCC };
            int blobId = _pool.Store(bytes);

            // Offset=1 / Length=2 只覆盖 {0xBB, 0xCC} —— 首字节 0xAA 须被排除
            var wrong = new PayloadRef(blobId, 1, 2);
            var ex = Assert.Throws<InvalidDataException>(
                () => _decoder.Decode<PoiStateChangedPayload>(EventKind.PoiStateChanged, wrong),
                "越界/非法段须抛 InvalidDataException(不得静默 default)");
            Assert.IsNotNull(ex.Message, "异常须带诊断信息");
        }

        [Test]
        public void test_adr031_decode_unresolvedBlob_throwsInvalidOperation()
        {
            // §② 的核心语义 —— 「未驻留」**显式化**为异常(与 TryGetPayload 的宽容形分家)
            var missing = new PayloadRef(9999, 0, 8);
            Assert.Throws<InvalidOperationException>(
                () => _decoder.Decode<PoiStateChangedPayload>(EventKind.PoiStateChanged, missing),
                "池未驻留该 blob ⇒ InvalidOperationException(§②:与「损坏」分两层)");
        }

        [Test]
        public void test_adr031_decode_outOfRangeRef_throwsInvalidOperation()
        {
            // 引用越界 = 同属「查无此数据」层
            int blobId = _pool.Store(new byte[] { 1, 2, 3 });
            var bad = new PayloadRef(blobId, 2, 99);   // 2 + 99 > 3
            Assert.Throws<InvalidOperationException>(
                () => _decoder.Decode<PoiStateChangedPayload>(EventKind.PoiStateChanged, bad),
                "引用越界 ⇒ InvalidOperationException(§②)");
        }

        [Test]
        public void test_adr031_decode_negativeOffset_throwsInvalidOperation()
        {
            int blobId = _pool.Store(new byte[] { 1, 2, 3 });
            var bad = new PayloadRef(blobId, -1, 1);
            Assert.Throws<InvalidOperationException>(
                () => _decoder.Decode<PoiStateChangedPayload>(EventKind.PoiStateChanged, bad));
        }

        [Test]
        public void test_adr031_decode_typeKindMismatch_throws()
        {
            var reference = _encoder.Encode(EventKind.PoiStateChanged, new PoiStateChangedPayload(7, 2));
            Assert.Throws<InvalidDataException>(
                () => _decoder.Decode<CaseOpenedPayload>(EventKind.PoiStateChanged, reference),
                "请求类型与 Kind 不匹配 ⇒ 抛(不静默 default)");
        }

        // ══════════ V-6: 分派完备性(与 EncodeBoxed 双向差集归零)══════════

        /// <summary>可由 Decode&lt;T&gt; 表达的 36 支(读侧无 Judgment 例外 —— §④;2026-10-22 ADR-032 起 35→36)。</summary>
        private static readonly EventKind[] DecodableKinds =
            Enum.GetValues(typeof(EventKind)).Cast<EventKind>().ToArray();

        [Test]
        public void test_adr031_v6_dispatchCoversAll36Kinds()
        {
            Assert.AreEqual(36, DecodableKinds.Length, "EventKind 须恰 36 支(ADR-032 起 35→36)");

            // 与 PayloadCodec 的具名 Decode 方法集双向差集归零(承 ADR-024 A5 口径)
            var named = typeof(PayloadCodec).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name.StartsWith("Decode", StringComparison.Ordinal)
                         && m.Name != "Decode"
                         && m.Name != "DecodeBoxed"
                         && m.ReturnType != typeof(object))
                .Select(m => m.Name.Substring("Decode".Length))
                .ToHashSet();

            var missing = DecodableKinds
                .Select(k => k.ToString())
                .Where(n => !named.Contains(n))
                .ToArray();
            Assert.IsEmpty(missing,
                "以下 Kind 无具名 Decode 实现(差集非零):" + string.Join(", ", missing));

            // 反向:具名 Decode 集合不得有闭集外的孤儿
            var kindNames = DecodableKinds.Select(k => k.ToString()).ToHashSet();
            var orphans = named.Where(n => !kindNames.Contains(n)).ToArray();
            Assert.IsEmpty(orphans, "具名 Decode 有闭集外孤儿:" + string.Join(", ", orphans));
        }

        [Test]
        public void test_adr031_allKinds_roundTripThroughDecoder()
        {
            int checkedCount = 0;
            foreach (var kind in DecodableKinds)
            {
                object payload = SamplePayloadFor(kind);
                if (payload == null) continue;

                var bytes = EncodeForRoundTrip(kind, payload);
                Assert.IsNotNull(bytes, $"{kind}: 编码不得返回 null");
                int blobId = _pool.Store(bytes);
                var reference = new PayloadRef(blobId, 0, bytes.Length);

                var decoded = InvokeDecodeViaInterface(kind, reference);
                Assert.IsNotNull(decoded, $"{kind}: 解码不得为 null");
                Assert.AreEqual(payload.GetType(), decoded.GetType(), $"{kind}: 类型须一致");
                AssertPayloadEqual(payload, decoded, kind);
                checkedCount++;
            }

            // 读侧**无** Judgment 例外(§④:接口不含 freehand ⇒ Decode 对 36 支一律可用);
            // 编码侧那 2 支经**具名重载**(freehandText="")—— 故此处覆盖 36/36,强于编码侧的 34。
            // (2026-10-22:35→36 / 33→34,ADR-032 加 PatientOutcomeChanged)
            Assert.AreEqual(36, checkedCount,
                $"须覆盖 36 支往返(读侧无 Judgment 例外),实测 {checkedCount}");
        }

        // ══════════ V-4 / V-10: b5 门补两项 ══════════

        [Test]
        public void test_adr031_v4_b5GateForbidsEncoderAndDecoder()
        {
            var field = typeof(Gates).GetField("AudioForbiddenContractTypes",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "b5 门禁用类型数组须存在");
            var arr = (string[])field.GetValue(null);

            CollectionAssert.Contains(arr, "IPayloadEncoder",
                "b5 门须含 IPayloadEncoder(ADR-031 §⑤:ADR-029 的漏项 —— 补前 44 可合法引用编码器)");
            CollectionAssert.Contains(arr, "IPayloadDecoder",
                "b5 门须含 IPayloadDecoder(ADR-031 §⑤)");

            // 既有的六抽象点 + SimEvent + VitalsDto 一项不得少(不得为补两项而删旧项)
            foreach (var t in new[]
            {
                "IEventSink", "IEventAuthority", "ITickProvider", "IIdAuthority",
                "IVitalsQuery", "ITeleportCommandSink", "SimEvent", "VitalsDto",
            })
            {
                CollectionAssert.Contains(arr, t, $"b5 门不得丢失既有项 {t}");
            }

            Assert.AreEqual(10, arr.Length, "b5 门须恰 10 项(原 8 + 2)");
        }

        [Test]
        public void test_adr031_v4_b5GateStillPassesOnRealTree()
        {
            // 补项后门须仍绿(44 侧实测未引用这两个类型)
            var errs = Gates.RunAll();
            Assert.That(errs, Is.Empty,
                () => $"AssemblyGates.RunAll 红行 {errs.Count} 条:\n" + string.Join("\n", errs));
        }

        // ══════════ 辅助(镜像 payload_encoder_test 的反射设施)══════════

        private static byte[] InvokeEncodeBoxed(EventKind kind, object payload)
        {
            var m = typeof(PayloadEncoder).GetMethod("EncodeBoxed",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (byte[])m.Invoke(null, new[] { (object)kind, payload });
        }

        /// <summary>
        /// 往返用编码 —— 对 Judgment 两支改走**具名重载**(它们多收 <c>freehandText</c>,
        /// 不在 <c>T</c> 里 ⇒ <c>EncodeBoxed</c> 按设计抛 <see cref="NotSupportedException"/>)。
        /// <para>读侧**无**此例外(ADR-031 §④:接口不含 freehand ⇒ <c>Decode</c> 对 35 支一律可用),
        /// 故本测试能覆盖 35/35 —— 比编码侧契约测试的 33 支更强。</para>
        /// </summary>
        private static byte[] EncodeForRoundTrip(EventKind kind, object payload)
        {
            if (kind == EventKind.JudgmentRecorded)
                return PayloadCodec.Encode((JudgmentRecordedPayload)payload, string.Empty);
            if (kind == EventKind.JudgmentRevised)
                return PayloadCodec.Encode((JudgmentRevisedPayload)payload, string.Empty);
            return InvokeEncodeBoxed(kind, payload);
        }

        /// <summary>
        /// 经**接口**解码(而非直接调 PayloadCodec)—— 本测试要验的正是接口路径。
        /// 反射无法把 <c>byte[]</c> 转 <c>ReadOnlySpan</c>,但 <c>in PayloadRef</c> 是值类型
        /// ⇒ 可直接装箱传参。
        /// </summary>
        private object InvokeDecodeViaInterface(EventKind kind, PayloadRef reference)
        {
            var payloadType = PayloadTypeFor(kind);
            if (payloadType == null) return null;
            var helper = typeof(PayloadDecoderTest)
                .GetMethod(nameof(DecodeVia), BindingFlags.NonPublic | BindingFlags.Instance);
            return helper.MakeGenericMethod(payloadType)
                .Invoke(this, new object[] { kind, reference });
        }

        private T DecodeVia<T>(EventKind kind, PayloadRef reference) where T : struct
            => _decoder.Decode<T>(kind, reference);

        private static Type PayloadTypeFor(EventKind kind)
        {
            var name = "Decode" + kind;
            var m = typeof(PayloadCodec).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(x => x.Name.Equals(name, StringComparison.Ordinal));
            return m?.ReturnType;
        }

        private static object SamplePayloadFor(EventKind kind)
        {
            var t = PayloadTypeFor(kind);
            if (t == null) return null;

            switch (kind)
            {
                case EventKind.EmergencyAttempt:
                    return new EmergencyAttemptPayload(
                        action: 1, holdTicks: 5, edges: 2, magPeak: 100, magLast: 80,
                        method: 0, actorId: 3, edgeTicks: new[] { 10, 12 });
                case EventKind.Craft:
                    return new CraftPayload(
                        actorId: 1, recipeId: 2, startTick: 100L, durationTicks: 50L,
                        inputInstanceIds: new long[] { 7L },
                        actualConsumed: new[] { 1 },
                        outputQty: new[] { 2 },
                        outputQuality: new[] { 3 },
                        outputInstanceIds: new long[] { 9L },
                        toolCell: new WorldPos(1, 0, 2));
                case EventKind.EncounterStarted:
                    return new EncounterStartedPayload(
                        encounterId: 1, protoId: 2, spawnCell: new WorldPos(3, 0, 4),
                        actorIds: new[] { 5, 6 });
                case EventKind.PatientOutcomeChanged:
                    // `Outcome = 0`(Unspecified)被解码期零值守卫有意拒收(ADR-032 ②)
                    // ⇒ 须造合法终态。守卫本身另有独立夹具覆盖
                    // (`PatientOutcomeKindTest.test_codec_unspecifiedOutcome_isRejectedAtDecode`)。
                    return new PatientOutcomeChangedPayload(
                        patientId: 11, diseaseId: 3,
                        outcome: (int)PatientOutcome.Recovered, seq: 21L);
                default:
                    return Activator.CreateInstance(t);
            }
        }

        private static void AssertPayloadEqual(object expected, object actual, EventKind kind)
        {
            foreach (var f in expected.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var a = f.GetValue(expected);
                var b = f.GetValue(actual);
                if (a is Array ea && b is Array eb)
                {
                    Assert.AreEqual(ea.Length, eb.Length, $"{kind}.{f.Name}: 数组长度");
                    for (int i = 0; i < ea.Length; i++)
                        Assert.AreEqual(ea.GetValue(i), eb.GetValue(i), $"{kind}.{f.Name}[{i}]");
                }
                else
                {
                    Assert.AreEqual(a, b, $"{kind}.{f.Name}");
                }
            }
        }
    }
}
