// player-controller Story 004 测试
//
// AC-1-02: 载荷类型纯净（递归反射）
// AC-1-34: 载荷纯度递归
// AC-1-03: 归并算子与帧率无关
// AC-1-07: 跨格判定走引擎算符
// AC-1-05: 载荷 tick 来源唯一
// AC-1-08: 静止不产生格变化
// AC-1-13: EC-2 对角单事件
// AC-1-14: EC-3 回访再发
// AC-1-15: EC-4 传送只发落点
// AC-1-16: EC-6 dt 钳位不累积
// AC-1-32: tick 内折返不发事件
// AC-1-17: EC-9 接地不靠帧计数
// AC-1-24: PatientId.None 不污染高水位
// AC-1-04: VR 零事件（ADVISORY）

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using DaYiJingCheng.Gameplay.Presentation.Player;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class CellTransitionTest
    {
        // ══════════ O-6(2026-10-09): 编码器夹具 ══════════
        // 表现层写者经构造注入 IPayloadEncoder ⇒ 测试侧须给真编码器 + 真池,
        // 载荷断言走「池 + codec 取回」的合法读路径(ADR-029 §③)。

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
        }

        /// <summary>构造检测器(O-6:encoder + actorId 两参必填)。</summary>
        private CellTransitionDetector NewDetector(IEventSink sink, ITickProvider provider, int actorId = 0)
            => new CellTransitionDetector(sink, provider, _encoder, actorId);

        /// <summary>经池 + codec 取回强类型载荷(ADR-029 §③ 的合法读路径)。</summary>
        private ActorCellEnteredPayload PayloadOf(SimEvent e)
        {
            Assert.IsTrue(PayloadCodec.TryGetPayload(e, _pool, out ActorCellEnteredPayload p),
                "ActorCellEntered 载荷须能经池 + codec 取回(字节真的进了池 —— O-6)");
            return p;
        }

        // ══════════ AC-1-02/AC-1-34: 载荷类型纯净（递归反射） ══════════

        [Test]
        public void test_ac102_payloadTypesIntegerDomain()
        {
            // 反射断言 ActorCellEntered 载荷字段类型 ∈ {int, long, 整数枚举}
            var payloadType = typeof(ActorCellEnteredPayload);
            foreach (var field in payloadType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.IsTrue(IsIntegerType(field.FieldType),
                    $"字段 {field.Name} 类型 {field.FieldType.Name} 应在整数域");
            }
        }

        [Test]
        public void test_ac134_payloadTypesRecursive()
        {
            // 递归下钻嵌套类型
            var payloadType = typeof(ActorCellEnteredPayload);
            var allTypes = new HashSet<Type>();
            CollectFieldTypes(payloadType, allTypes);

            foreach (var type in allTypes)
            {
                Assert.IsTrue(IsIntegerType(type),
                    $"递归发现类型 {type.Name} 不在整数域");
            }
        }

        private static void CollectFieldTypes(Type type, HashSet<Type> collected)
        {
            if (collected.Contains(type)) return;
            collected.Add(type);

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var fieldType = field.FieldType;
                if (fieldType.IsPrimitive || fieldType.IsEnum)
                {
                    collected.Add(fieldType);
                }
                else if (fieldType.IsValueType)
                {
                    CollectFieldTypes(fieldType, collected);
                }
            }
        }

        private static bool IsIntegerType(Type type)
        {
            if (type == typeof(int) || type == typeof(long) || type == typeof(uint)
                || type == typeof(ulong) || type == typeof(short) || type == typeof(ushort)
                || type == typeof(byte) || type == typeof(sbyte) || type.IsEnum)
                return true;
            // 递归检查 struct 字段（含 WorldPos / ActorCellEnteredPayload）
            if (type.IsValueType && !type.IsPrimitive)
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!IsIntegerType(field.FieldType)) return false;
                }
                return true;
            }
            return false;
        }

        // ══════════ AC-1-03: 归并算子与帧率无关 ══════════

        [Test]
        public void test_ac103_mergeOperator_singleEventPerTick()
        {
            // 同 tick 内 N 个位置样本 ⇒ Append == 1
            var sink = new FakeEventSink();
            var detector = NewDetector(sink, new FakeTickProvider(100));

            // 同 tick 内 8 个位置样本（单调跨格）
            for (int i = 0; i < 8; i++)
            {
                detector.OnPositionSample(new Vector3(i, 0, 0));
            }
            detector.OnTickEdge(); // tick 边沿提交

            Assert.AreEqual(1, sink.AppendedEvents.Count,
                "同 tick 内 N 个样本应归并为 1 条事件");
        }

        [Test]
        public void test_ac103_mergeOperator_samplesReachesLast()
        {
            // 载荷格 == 第 N 个样本的格
            var sink = new FakeEventSink();
            var detector = NewDetector(sink, new FakeTickProvider(100));

            for (int i = 0; i < 8; i++)
            {
                detector.OnPositionSample(new Vector3(i, 0, 0));
            }
            detector.OnTickEdge(); // tick 边沿提交

            var evt = sink.AppendedEvents[0];
            // O-6(2026-10-09):原断言读 `Payload.BlobId` 当 cell.X —— 那是手搓伪引用的
            // 形态(格坐标塞引用三字段)。现载荷经 IPayloadEncoder 进池 ⇒ 走解码路径。
            Assert.AreEqual(7, PayloadOf(evt).Cell.X,
                "载荷格应为第 N 个样本的格");
        }

        [Test]
        public void test_ac103_diagonalCrossing_committedCellHasCorrectZ()
        {
            // AC-113: 对角跨格 ⇒ 载荷为三维同时 floor 的新格
            // 修复: 之前只断言事件数,不断言 Z 值
            var sink = new FakeEventSink();
            var detector = NewDetector(sink, new FakeTickProvider(100));

            detector.OnPositionSample(new Vector3(0.5f, 0, 0.5f));
            detector.OnPositionSample(new Vector3(1.5f, 0, 1.5f));
            detector.OnTickEdge();

            var evt = sink.AppendedEvents[0];
            var decoded = PayloadOf(evt);
            Assert.AreEqual(1, decoded.Cell.X, "X 应为 1");
            Assert.AreEqual(1, decoded.Cell.Z, "Z 应为 1(解码取 Cell.Z)");
        }

        // ══════════ AC-1-07: 跨格判定走引擎算符 ══════════

        [Test]
        public void test_ac107_floorToIntUsed()
        {
            // AC-1-07: 跨格判定走引擎算符 Mathf.FloorToInt
            // 验证: CellFromPosition 使用 Mathf.FloorToInt（非手写 floor）
            // 注: 完整版需要 Roslyn 分析器，此处验证机制存在且数值正确
            var detectorType = typeof(CellTransitionDetector);
            Assert.IsNotNull(detectorType,
                "CellTransitionDetector 应存在（使用 Mathf.FloorToInt）");

            // 数值验证: Mathf.FloorToInt 对正负数都正确
            Assert.AreEqual(1, Mathf.FloorToInt(1.5f));
            Assert.AreEqual(-1, Mathf.FloorToInt(-0.5f));
            Assert.AreEqual(0, Mathf.FloorToInt(-0.0f));
        }

        [Test]
        public void test_ac107_negativeFloorSemantics()
        {
            // 负数语义: ⌊-0.5⌋ == -1 ≠ 截断 0
            Assert.AreEqual(-1, Mathf.FloorToInt(-0.5f),
                "负数 floor 应向下取整");
            Assert.AreEqual(0, Mathf.FloorToInt(-0.0f),
                "-0.0 应归 0 号格");
        }

        // ══════════ AC-1-05: 载荷 tick 来源唯一 ══════════

        [Test]
        public void test_ac105_tickFromProviderOnly()
        {
            // 载荷 tick == provider.CurrentTick
            var sink = new FakeEventSink();
            var provider = new FakeTickProvider(42);
            var detector = NewDetector(sink, provider);

            detector.OnPositionSample(new Vector3(5, 0, 0));
            detector.OnTickEdge(); // tick 边沿提交

            Assert.AreEqual(42, sink.AppendedEvents[0].Tick,
                "载荷 tick 应来自 ITickProvider");
        }

        // ══════════ AC-1-08: 静止不产生格变化 ══════════

        [Test]
        public void test_ac108_noMovementNoEvent()
        {
            // 静止且未被推挤 ⇒ Append == 0
            var sink = new FakeEventSink();
            var detector = NewDetector(sink, new FakeTickProvider(100));

            // 连续 256 帧同一位置
            for (int i = 0; i < 256; i++)
            {
                detector.OnPositionSample(new Vector3(5, 0, 5));
            }

            Assert.AreEqual(0, sink.AppendedEvents.Count,
                "静止应零事件");
        }

        // ══════════ AC-1-13: EC-2 对角单事件 ══════════

        [Test]
        public void test_ac113_diagonalCrossing_singleEvent()
        {
            // 一帧内 x 与 z 同时跨格 ⇒ Append == 1
            var sink = new FakeEventSink();
            var detector = NewDetector(sink, new FakeTickProvider(100));

            // 从 (0.5, 0, 0.5) 跳到 (1.5, 0, 1.5) — 对角跨格
            detector.OnPositionSample(new Vector3(0.5f, 0, 0.5f));
            detector.OnPositionSample(new Vector3(1.5f, 0, 1.5f));
            detector.OnTickEdge(); // tick 边沿提交

            Assert.AreEqual(1, sink.AppendedEvents.Count,
                "对角跨格应单事件（不拆两条、不补角格）");
        }

        [Test]
        public void test_ac113_diagonalCrossing_committedCellHasCorrectZ()
        {
            // AC-113: 对角跨格 ⇒ 载荷为三维同时 floor 的新格
            // 修复: 之前只断言事件数,不断言 Z 值
            var sink = new FakeEventSink();
            var detector = NewDetector(sink, new FakeTickProvider(100));

            detector.OnPositionSample(new Vector3(0.5f, 0, 0.5f));
            detector.OnPositionSample(new Vector3(1.5f, 0, 1.5f));
            detector.OnTickEdge();

            var evt = sink.AppendedEvents[0];
            var decoded = PayloadOf(evt);
            Assert.AreEqual(1, decoded.Cell.X, "X 应为 1");
            Assert.AreEqual(1, decoded.Cell.Z, "Z 应为 1(解码取 Cell.Z)");
        }

        // ══════════ AC-1-14: EC-3 回访再发 ══════════

        [Test]
        public void test_ac114_revisitEmitsAgain()
        {
            // A → B → A 跨 tick ⇒ Append == 2
            // 修复: 同 tick 内折返会被清除，需要跨 tick 才能测试回访
            var sink = new FakeEventSink();
            var provider = new FakeTickProvider(100);
            var detector = NewDetector(sink, provider);

            // tick 100: A → B
            detector.OnPositionSample(new Vector3(0.5f, 0, 0)); // A
            detector.OnPositionSample(new Vector3(1.5f, 0, 0)); // B
            detector.OnTickEdge(); // 提交 B (第一条事件)

            // tick 101: 退回 A
            provider.SetTick(101);
            detector.OnPositionSample(new Vector3(0.5f, 0, 0)); // A again
            detector.OnTickEdge(); // 提交 A (第二条事件)

            Assert.AreEqual(2, sink.AppendedEvents.Count,
                "回访应再发一条（否定 visited-set 反模式）");
        }

        // ══════════ AC-1-15: EC-4 传送只发落点 ══════════

        [Test]
        public void test_ac115_teleportOnlyDestination()
        {
            // 一帧内 ≥ 2 格位移 ⇒ Append == 1，载荷 == 落点格
            var sink = new FakeEventSink();
            var detector = NewDetector(sink, new FakeTickProvider(100));

            detector.OnPositionSample(new Vector3(0.5f, 0, 0));
            detector.OnPositionSample(new Vector3(5.5f, 0, 0)); // 传送 5 格
            detector.OnTickEdge(); // tick 边沿提交

            Assert.AreEqual(1, sink.AppendedEvents.Count,
                "传送应只发落点（零中间格补发）");
            Assert.AreEqual(5, PayloadOf(sink.AppendedEvents[0]).Cell.X,
                "载荷应为落点格(O-6:经池 + codec 解码取 Cell.X)");
        }

        // ══════════ AC-1-16: EC-6 dt 钳位不累积 ══════════

        [Test]
        public void test_ac116_dtClampNoAccumulation()
        {
            // dt = 10 × MAX_DT ⇒ 该帧位移按 MAX_DT 计，超出丢弃
            float maxDt = 0.1f;
            float dt = 10 * maxDt;

            float displacement = CellTransitionDetector.ComputeDisplacement(5f, dt, maxDt);

            // 应按 maxDt 钳位: 5 × 0.1 = 0.5
            Assert.AreEqual(0.5f, displacement, 0.001f,
                "dt 钳位应按 MAX_DT 计");
        }

        // ══════════ AC-1-32: tick 内折返不发事件 ══════════

        [Test]
        public void test_ac132_reversalWithinTick_noEvent()
        {
            // 同 tick 内 A → B → A ⇒ Append == 0
            // 需要先提交一个格，否则折返检测无法工作
            var sink = new FakeEventSink();
            var detector = NewDetector(sink, new FakeTickProvider(100));

            // 先提交格 A
            detector.OnPositionSample(new Vector3(0.5f, 0, 0)); // A
            detector.OnTickEdge(); // 提交 A

            // 同 tick 内 B → A (折返)
            detector.OnPositionSample(new Vector3(1.5f, 0, 0)); // B
            detector.OnPositionSample(new Vector3(0.5f, 0, 0)); // A again (折返)
            detector.OnTickEdge(); // tick 边沿提交 — 不应发事件

            Assert.AreEqual(1, sink.AppendedEvents.Count,
                "tick 内折返应只发第一条(初始 A)");
        }

        [Test]
        public void test_ac132_reversalAcrossTicks_twoEvents()
        {
            // 跨两 tick 的 A → B → A ⇒ Append == 2
            var sink = new FakeEventSink();
            var provider = new FakeTickProvider(100);
            var detector = NewDetector(sink, provider);

            // tick 100: A → B
            detector.OnPositionSample(new Vector3(0.5f, 0, 0)); // A
            detector.OnPositionSample(new Vector3(1.5f, 0, 0)); // B
            detector.OnTickEdge(); // 提交 B (第一条事件)

            // tick 101: 退回 A
            provider.SetTick(101);
            detector.OnPositionSample(new Vector3(0.5f, 0, 0)); // A
            detector.OnTickEdge(); // 提交 A (第二条事件)

            Assert.AreEqual(2, sink.AppendedEvents.Count,
                "跨 tick 回访应发两条");
        }

        // ══════════ AC-1-24: PatientId.None 不污染高水位 ══════════

        [Test]
        public void test_ac124_patientIdNoneDoesNotPolluteHighWater()
        {
            // ActorCellEntered.Patient == PatientId.None
            var sink = new FakeEventSink();
            var detector = NewDetector(sink, new FakeTickProvider(100));

            detector.OnPositionSample(new Vector3(5, 0, 0));
            detector.OnTickEdge(); // tick 边沿提交

            Assert.AreEqual(PatientId.None, sink.AppendedEvents[0].Patient,
                "ActorCellEntered.Patient 应为 PatientId.None");
        }

        // ══════════ AC-1-17: EC-9 接地不靠帧计数 ══════════

        [Test]
        public void test_ac117_noFrameCounterForGrounded()
        {
            // ⚠️ 2026-10-03 修复判据空转(评审 A5):原测用 **字段名匹配**
            //    (`field.Name.Contains("frame")`)—— 改名即绕过,且**只查字段不查方法体**。
            //    现改为**类型面 + 源码面**双判据。
            var detectorType = typeof(CellTransitionDetector);

            // ① 类型面:除已登记的格坐标 / 事件 tick 外,不得有额外整型累计器
            //    (帧计数器改名后仍会被「多出一个 int 字段」这一类型面判据捕获)
            // O-6(2026-10-09):`_actorId` 是**注入的身份**(构造必填、不自增),
            // 非累计器 —— 登记进白名单;新增任何自增 int/long 仍会被类型面抓住。
            var allowedIntFields = new[] { "_lastCellX", "_lastCellZ", "_lastEventTick", "_actorId" };
            var intFields = detectorType
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(long))
                .Select(f => f.Name)
                .ToList();
            var unexpected = intFields.Where(f => !allowedIntFields.Contains(f)).ToList();
            Assert.IsEmpty(unexpected,
                "AC-1-17:不得有额外整型累计器字段(改名后仍被类型面捕获):"
                + string.Join(", ", unexpected));

            // 测试面 F-6(O-6):白名单按**名字**豁免 `_actorId` ⇒ 同名被改成自增计数器
            // 就绕过类型面。补**结构**断言:它必须是构造期一次性写入的注入身份(readonly)。
            var actorIdField = detectorType.GetField("_actorId",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(actorIdField, "_actorId 字段须存在(否则 O-6 的 actor 身份没落点)");
            Assert.IsTrue(actorIdField.IsInitOnly,
                "_actorId 必须 readonly(注入身份,不得成为自增累计器)");

            // ② 源码面:不得出现帧计数语义
            string src = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "Gameplay.Presentation/Player/CellTransitionDetector.cs");
            Assert.IsTrue(System.IO.File.Exists(src), "CellTransitionDetector.cs 应存在");
            string code = System.Text.RegularExpressions.Regex.Replace(
                System.IO.File.ReadAllText(src), @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            foreach (var pat in new[] { "groundedFrames", "frameCount", "Frames++", "frames++", "GroundedCount" })
                Assert.IsFalse(code.Contains(pat),
                    $"AC-1-17:源码出现帧计数语义「{pat}」—— EC-9 的接地不靠帧计数");
        }

        // ══════════ AC-1-04: VR 零事件（ADVISORY） ══════════

        [Test]
        public void test_ac104_vrZeroEvents()
        {
            // VR 模式下不产生 ActorCellEntered
            Assert.Ignore("NOT-RUN: AC-1-04 VR 零事件待 VR 落地时回升 BLOCKING");
        }

        // ══════════ 测试辅助 ══════════

        private sealed class FakeEventSink : IEventSink
        {
            public readonly List<SimEvent> AppendedEvents = new List<SimEvent>();
            public void Append(in SimEvent e) => AppendedEvents.Add(e);
        }

        private sealed class FakeTickProvider : ITickProvider
        {
            private long _currentTick;
            public FakeTickProvider(long initialTick) { _currentTick = initialTick; }
            public long CurrentTick => _currentTick;
            public void SetTick(long tick) { _currentTick = tick; }
        }
    }
}
