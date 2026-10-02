// ADR-029 契约支测试 —— persistence-service Story 002
//
// AC-29-01: IPayloadEncoder 签名逐字
// AC-29-02: IBlobSink 住 Sim.Codec(池写面)
// AC-29-03: PayloadEncoder 实现 + 经构造注入 IBlobSink
// AC-29-04: EncodeBoxed 分派覆盖 32 支 + 2 支显式抛
// AC-29-05: Sim.Contracts 零 Sim.Codec 引用
// AC-29-06: Sim 引用集仍 = ["Sim.Contracts"]
// AC-29-07: Encode<T> 与 Decode<T> 形态对称
// AC-29-08: 32 支 Encode → Decode 逐字段往返一致

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class PayloadEncoderTest
    {
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
        }

        // ══════════ AC-29-01 / AC-29-07: 签名与对称性 ══════════

        [Test]
        public void test_ac2901_encoderSignature_matchesDecodeShape()
        {
            var enc = typeof(IPayloadEncoder).GetMethod("Encode");
            Assert.IsNotNull(enc, "IPayloadEncoder.Encode 应存在");
            Assert.IsTrue(enc.IsGenericMethodDefinition, "Encode 须为泛型方法");

            var gp = enc.GetGenericArguments();
            Assert.AreEqual(1, gp.Length, "Encode 恰一个泛型形参 T");

            // 约束 = struct 且**无接口约束**(与 Decode<T> 对称 —— ADR-029 评审修正 ②)
            var attrs = gp[0].GenericParameterAttributes;
            Assert.IsTrue((attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0,
                "T 须有 struct 约束");
            // ⚠️ `where T : struct` 在反射下产出 **1 条**约束 = System.ValueType(非 0 条)。
            //    真正的判据意图 = **不得有接口约束** —— 即否定初稿的 `IPayload` 标记接口。
            var constraints = gp[0].GetGenericParameterConstraints();
            Assert.IsFalse(constraints.Any(c => c.IsInterface),
                $"T 不得有接口约束(初稿的 IPayload 已撤);实测约束 = [{string.Join(", ", constraints.Select(c => c.Name))}]");

            // 形参 = (EventKind, in T) —— 共 2 个
            var ps = enc.GetParameters();
            Assert.AreEqual(2, ps.Length, "Encode 须收 (EventKind, in T) 两形参(T 无法反推 Kind)");
            Assert.AreEqual(typeof(EventKind), ps[0].ParameterType, "第一形参须为 EventKind");
            Assert.IsTrue(ps[1].ParameterType.IsByRef, "第二形参须为 in/ref 传参");
            Assert.AreEqual(typeof(PayloadRef), enc.ReturnType, "返回须为 PayloadRef(乙案,非 byte[])");
        }

        [Test]
        public void test_ac2907_encoderIsSymmetricWithDecoder()
        {
            // Encode<T> 与 PayloadCodec.Decode<T> 的泛型约束形态须一致(ADR-029 V-5)
            var dec = typeof(PayloadCodec).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Decode" && m.IsGenericMethodDefinition);
            Assert.IsNotNull(dec, "PayloadCodec.Decode<T> 应存在");

            var encT = typeof(IPayloadEncoder).GetMethod("Encode").GetGenericArguments()[0];
            var decT = dec.GetGenericArguments()[0];

            Assert.AreEqual(
                encT.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask,
                decT.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask,
                "Encode<T> 与 Decode<T> 的特殊约束须相同");
            // `where T : struct` 产出 1 条约束 = System.ValueType;判据意图 = 无**接口**约束
            Assert.IsFalse(encT.GetGenericParameterConstraints().Any(c => c.IsInterface),
                "Encode<T> 无接口约束");
            Assert.IsFalse(decT.GetGenericParameterConstraints().Any(c => c.IsInterface),
                "Decode<T> 无接口约束");
            Assert.AreEqual(
                encT.GetGenericParameterConstraints().Length,
                decT.GetGenericParameterConstraints().Length,
                "两条泛型方法的约束条数须一致(对称)");
        }

        // ══════════ AC-29-02: IBlobSink 住 Sim.Codec ══════════

        [Test]
        public void test_ac2902_blobSinkLivesInSimCodec()
        {
            Assert.AreEqual("Sim.Codec", typeof(IBlobSink).Assembly.GetName().Name,
                "IBlobSink 须住 Sim.Codec —— 刻意**不**住 Sim.Contracts(否则 Sim 可直接摸池)");
            Assert.AreEqual("Sim.Codec", typeof(IBlobPool).Assembly.GetName().Name,
                "IBlobPool 读面亦住 Sim.Codec");

            // IBlobSink 不得出现在 Sim.Contracts 装配内
            var inContracts = typeof(IPayloadEncoder).Assembly.GetTypes()
                .Any(t => t.Name == "IBlobSink");
            Assert.IsFalse(inContracts, "Sim.Contracts 内不得有 IBlobSink(旁路防护)");
        }

        [Test]
        public void test_ac2902_storeSignature_returnsBlobId()
        {
            var store = typeof(IBlobSink).GetMethod("Store");
            Assert.IsNotNull(store, "IBlobSink.Store 应存在");
            Assert.AreEqual(typeof(int), store.ReturnType, "Store 须返回 BlobId(int)");
            Assert.AreEqual(1, store.GetParameters().Length, "Store 恰一个形参");
        }

        // ══════════ AC-29-03: 实现 + 注入 ══════════

        [Test]
        public void test_ac2903_encoderInjectedWithSink()
        {
            Assert.IsInstanceOf<IPayloadEncoder>(_encoder, "PayloadEncoder 须实现 IPayloadEncoder");
            Assert.AreEqual("Sim.Codec", typeof(PayloadEncoder).Assembly.GetName().Name,
                "PayloadEncoder 须住 Sim.Codec(它看得见 PayloadCodec)");

            // 构造须经注入(非单例 —— coding-standards)
            var ctor = typeof(PayloadEncoder).GetConstructors().Single();
            Assert.AreEqual(typeof(IBlobSink), ctor.GetParameters().Single().ParameterType,
                "构造须注入 IBlobSink");
        }

        [Test]
        public void test_ac2903_nullSink_throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PayloadEncoder(null));
        }

        [Test]
        public void test_ac2903_bytesActuallyEnterPool()
        {
            // 🔴 本 story 的核心判据 —— 直接否证现状的假引用形态(PayloadRef(0,0,len),字节被丢弃)
            var payload = new PoiStateChangedPayload(7, 2);
            var reference = _encoder.Encode(EventKind.PoiStateChanged, payload);

            Assert.IsTrue(_pool.TryGetBlob(reference.BlobId, out var stored),
                "编码后字节须**真的**在池里可取回(现状的手搓法会在此红)");
            Assert.AreEqual(reference.Length, stored.Length,
                "池内字节长度须与 PayloadRef.Length 一致");
            Assert.Greater(stored.Length, 0, "载荷不得为空");
        }

        // ══════════ AC-29-04: 分派完备性(32 + 2)══════════

        /// <summary>可由 Encode&lt;T&gt; 表达的 32 支(34 − Judgment 两支)。</summary>
        private static readonly EventKind[] EncodableKinds =
            Enum.GetValues(typeof(EventKind)).Cast<EventKind>()
                .Where(k => k != EventKind.JudgmentRecorded && k != EventKind.JudgmentRevised)
                .ToArray();

        [Test]
        public void test_ac2904_dispatchCovers32Kinds()
        {
            Assert.AreEqual(32, EncodableKinds.Length,
                "可编码 Kind 须恰 32 支(34 总 − 2 Judgment)");

            // 与 PayloadCodec 的具名 Encode 重载集双向差集归零(承 ADR-024 A5 口径)
            var named = typeof(PayloadCodec).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Encode" && !m.IsGenericMethodDefinition)
                .ToList();

            // 32 支单参重载 + 2 支双参(Judgment)重载 = 34
            int singleArg = named.Count(m => m.GetParameters().Length == 1);
            int doubleArg = named.Count(m => m.GetParameters().Length == 2);
            Assert.AreEqual(32, singleArg, "单参具名 Encode 重载须恰 32");
            Assert.AreEqual(2, doubleArg, "双参(Judgment)具名 Encode 重载须恰 2");
        }

        [Test]
        public void test_ac2904_judgmentKinds_throwNotSupported()
        {
            // ADR-029 §① 修正 ④ —— 显式抛,非静默 default
            var recorded = new JudgmentRecordedPayload(1, new CaseId(1, 2, 3), 4, 5, 6);
            var ex1 = Assert.Throws<NotSupportedException>(
                () => _encoder.Encode(EventKind.JudgmentRecorded, recorded),
                "JudgmentRecorded 须抛 NotSupportedException(其载荷需 freehandText,不在 T 里)");

            var revised = new JudgmentRevisedPayload(1, new CaseId(1, 2, 3), 4, 5, 6);
            var ex2 = Assert.Throws<NotSupportedException>(
                () => _encoder.Encode(EventKind.JudgmentRevised, revised));

            // 可诊断性:错误串须指向具名重载(否则未来 37 轮的实现者无从下手)
            StringAssert.Contains("freehandText", ex1.Message, "异常消息须点明缺 freehandText");
            StringAssert.Contains("PayloadCodec.Encode", ex1.Message, "异常消息须指向具名重载");
            StringAssert.Contains("PayloadCodec.Encode", ex2.Message, "异常消息须指向具名重载");
        }

        [Test]
        public void test_ac2904_unknownKind_throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => _encoder.Encode((EventKind)9999, new PoiStateChangedPayload(1, 1)),
                "闭集外的 Kind 须抛(不静默 default)");
        }

        // ══════════ AC-29-05 / AC-29-06: 装配边界 ══════════

        [Test]
        public void test_ac2905_contractsHasNoCodecReference()
        {
            var contracts = typeof(IPayloadEncoder).Assembly;
            var refs = contracts.GetReferencedAssemblies().Select(r => r.Name).ToList();

            Assert.IsFalse(refs.Contains("Sim.Codec"),
                "Sim.Contracts 不得引用 Sim.Codec(ADR-025 §①:112)");

            // IPayloadEncoder 的签名面只出现 Sim.Contracts 类型
            var enc = typeof(IPayloadEncoder).GetMethod("Encode");
            foreach (var p in enc.GetParameters())
            {
                var t = p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType;
                Assert.AreEqual(contracts.GetName().Name, t.Assembly.GetName().Name,
                    $"形参 {p.Name} 的类型 {t.Name} 须住 Sim.Contracts");
            }
        }

        [Test]
        public void test_ac2906_simReferenceSetUnchanged()
        {
            string asmdefPath = Path.Combine(
                Application.dataPath, "../Assets/Sim/Sim.asmdef");
            Assert.IsTrue(File.Exists(asmdefPath), "Sim.asmdef 应存在");

            string content = File.ReadAllText(asmdefPath);
            Assert.IsFalse(content.Contains("Sim.Codec"),
                "Sim.asmdef 不得引用 Sim.Codec —— 本 story 一字不改该引用集(ADR-025 §①:111)");

            var sim = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Sim");
            if (sim != null)
            {
                var refs = sim.GetReferencedAssemblies().Select(r => r.Name).ToList();
                Assert.IsFalse(refs.Contains("Sim.Codec"), "Sim 运行时引用集不得含 Sim.Codec");
                Assert.IsTrue(refs.Contains("Sim.Contracts"), "Sim 须引用 Sim.Contracts");
            }
        }

        // ══════════ AC-29-08: 32 支往返一致 ══════════

        [Test]
        public void test_ac2908_allEncodableKinds_roundTrip()
        {
            int checkedCount = 0;
            foreach (var kind in EncodableKinds)
            {
                object payload = SamplePayloadFor(kind);
                if (payload == null) continue;

                var bytes = InvokeEncodeBoxed(kind, payload);
                Assert.IsNotNull(bytes, $"{kind}: 编码不得返回 null");

                // 经池往返:Encode → 池 → Decode
                int blobId = _pool.Store(bytes);
                Assert.IsTrue(_pool.TryGetBlob(blobId, out var stored), $"{kind}: 池须可取回");

                var decoded = InvokeDecode(kind, stored.ToArray());
                Assert.IsNotNull(decoded, $"{kind}: 解码不得为 null");
                Assert.AreEqual(payload.GetType(), decoded.GetType(), $"{kind}: 类型须一致");
                AssertPayloadEqual(payload, decoded, kind);

                checkedCount++;
            }

            Assert.GreaterOrEqual(checkedCount, 32,
                $"须覆盖 ≥32 支往返,实测 {checkedCount}");
        }

        [Test]
        public void test_ac2908_poiStateChanged_fieldOrderNotSwapped()
        {
            // 定向负例:现状手搓法把 poiId 当 blobId、newState 当 offset ⇒ 语义错位。
            // codec 路径须保证 (PoiId=7, NewState=2) 往返后仍为 (7, 2)。
            var reference = _encoder.Encode(EventKind.PoiStateChanged, new PoiStateChangedPayload(7, 2));
            Assert.IsTrue(_pool.TryGetBlob(reference.BlobId, out var stored));

            var decoded = PayloadCodec.Decode<PoiStateChangedPayload>(EventKind.PoiStateChanged, stored.Span);
            Assert.AreEqual(7, decoded.PoiId, "PoiId 须为 7(不得被当成 blobId)");
            Assert.AreEqual(2, decoded.NewState, "NewState 须为 2(不得被当成字节偏移)");
        }

        // ══════════ 辅助 ══════════

        private static byte[] InvokeEncodeBoxed(EventKind kind, object payload)
        {
            var m = typeof(PayloadEncoder).GetMethod("EncodeBoxed",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (byte[])m.Invoke(null, new[] { (object)kind, payload });
        }

        /// <summary>
        /// 编译期泛型过渡 —— 反射**无法**把 <c>byte[]</c> 转成 <c>ReadOnlySpan&lt;byte&gt;</c>
        /// (user-defined 隐式转换不参与 <c>Invoke</c> 的实参绑定)。故经本方法:
        /// 反射只传 <c>byte[]</c>,span 转换在本方法体内由编译器完成。
        /// </summary>
        private static T DecodeVia<T>(EventKind kind, byte[] bytes) where T : struct
            => PayloadCodec.Decode<T>(kind, bytes);

        private static object InvokeDecode(EventKind kind, byte[] bytes)
        {
            var payloadType = PayloadTypeFor(kind);
            if (payloadType == null) return null;
            var helper = typeof(PayloadEncoderTest)
                .GetMethod(nameof(DecodeVia), BindingFlags.NonPublic | BindingFlags.Static);
            return helper.MakeGenericMethod(payloadType)
                .Invoke(null, new object[] { kind, bytes });
        }

        private static readonly System.Collections.Generic.Dictionary<EventKind, Type> PayloadTypes =
            new System.Collections.Generic.Dictionary<EventKind, Type>();

        private static Type PayloadTypeFor(EventKind kind)
        {
            // 从具名 Decode 方法的返回类型反查 —— 避免手工维护第二份映射
            var name = "Decode" + kind;
            var m = typeof(PayloadCodec).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(x => x.Name.Equals(name, StringComparison.Ordinal));
            return m?.ReturnType;
        }

        private static object SamplePayloadFor(EventKind kind)
        {
            var t = PayloadTypeFor(kind);
            if (t == null) return null;

            // 3 支含数组字段的载荷不能用 default 构造 —— 其数组为 null,
            // 而 Encode 侧对 null 数组**先拒**(ArgumentException,「坏数据不进字节面」);
            // 且 Craft 有五数组等长约束、EmergencyAttempt 有 Edges==EdgeTicks.Length 约束。
            // ⇒ 这 3 支须造满足约束的样本。
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

                default:
                    // 余 29 支无数组字段 —— default 构造即合法(零值全部通过约束)
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
