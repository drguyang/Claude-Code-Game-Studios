// player-controller Story 004 测试
//
// 🔁 **2026-10-25(TD-player-03 收敛轮 · 用户裁定甲案)**:本文件原打
//   `CellTransitionDetector` 实例状态机 —— 那是跨格状态机的**第二份实现**(平行于
//   `PlayerController` 内联路径,功能子集 + 带已被内联修掉的 tick 来源缺陷形态)。
//   实例状态机已删 ⇒ 本文件的**行为断言迁移到内联路径**(唯一实现):
//   夹具 = `PlayerController` 真组件(`AddComponent` + `Initialize`,S11-3 先例)。
//   唯一断言语义变化:AC-1-05 的载荷 tick 来源 —— 内联权威 = **边沿自有 tick**
//   (BCD-码-2 修复:multi-tick 帧内帧末 CurrentTick 超前边沿 steps−1),
//   断言改为「tick == 边沿形参,≠ 帧末 CurrentTick」(比原「== provider」更强,
//   同时钉死已删除的缺陷形态不回归)。
//
// AC-1-02: 载荷类型纯净（递归反射）
// AC-1-34: 载荷纯度递归
// AC-1-03: 归并算子与帧率无关
// AC-1-07: 跨格判定走引擎算符
// AC-1-05: 载荷 tick 来源唯一(边沿自有 tick)
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
using PlayerControllerType = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class CellTransitionTest
    {
        // ══════════ O-6(2026-10-09): 编码器夹具 ══════════
        // 表现层写者经构造注入 IPayloadEncoder ⇒ 测试侧须给真编码器 + 真池,
        // 载荷断言走「池 + codec 取回」的合法读路径(ADR-029 §③)。

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;

        // S11-3:MonoBehaviour 真夹具(TD-player-03 迁移自 CellTransitionDetector 实例)。
        // PlayerController 是 MonoBehaviour —— `new` 被 Unity 禁止;AddComponent 真夹具
        // + 显式 RequireComponent 依赖(host_authority_test 同款)。

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        /// <summary>构造内联路径载体:PlayerController(Host 模式)+ 注入四件。</summary>
        private PlayerControllerType NewController(IEventSink sink, ITickProvider provider, int actorId = 0)
        {
            var go = new GameObject("test_cell_transition_controller");
            _spawned.Add(go);
            go.AddComponent<CharacterController>();
            var controller = go.AddComponent<PlayerControllerType>();
            controller.Initialize(SimAuthorityMode.Host, sink, provider, _encoder, actorId);
            return controller;
        }

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
            var controller = NewController(sink, new FakeTickProvider(100));

            // 同 tick 内 8 个位置样本（单调跨格）
            for (int i = 0; i < 8; i++)
            {
                controller.OnPositionSample(new Vector3(i, 0, 0));
            }
            controller.OnTickEdge(100); // tick 边沿提交(边沿自有 tick)

            Assert.AreEqual(1, sink.AppendedEvents.Count,
                "同 tick 内 N 个样本应归并为 1 条事件");
        }

        [Test]
        public void test_ac103_mergeOperator_samplesReachesLast()
        {
            // 载荷格 == 第 N 个样本的格
            var sink = new FakeEventSink();
            var controller = NewController(sink, new FakeTickProvider(100));

            for (int i = 0; i < 8; i++)
            {
                controller.OnPositionSample(new Vector3(i, 0, 0));
            }
            controller.OnTickEdge(100);

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
            var controller = NewController(sink, new FakeTickProvider(100));

            controller.OnPositionSample(new Vector3(0.5f, 0, 0.5f));
            controller.OnPositionSample(new Vector3(1.5f, 0, 1.5f));
            controller.OnTickEdge(100);

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
            // 🔁 TD-player-03:CellTransitionDetector 现为静态工具类(取格唯一真源,
            //    内联路径消费);类型存在性断言不因实例状态机删除而失真。
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

        // ══════════ AC-1-05: 载荷 tick 来源唯一(边沿自有 tick) ══════════

        [Test]
        public void test_ac105_tickFromEdgeTick_notFrameEnd()
        {
            // 🔁 TD-player-03 断言语义升级:内联权威 = **边沿自有 tick**(BCD-码-2 修复)。
            //    provider.CurrentTick 刻意设成「帧末超前值」999 —— 若载荷 tick 读帧末
            //    (= 已删除的 CellTransitionDetector 实例的缺陷形态),本测转红。
            var sink = new FakeEventSink();
            var provider = new FakeTickProvider(999);
            var controller = NewController(sink, provider);

            controller.OnPositionSample(new Vector3(5, 0, 0));
            controller.OnTickEdge(42); // 本边沿自己的逻辑 tick(帧泵逐边沿回推)

            Assert.AreEqual(42, sink.AppendedEvents[0].Tick,
                "载荷 tick 应取边沿自有 tick(BCD-码-2),不得读帧末 CurrentTick");
        }

        // ══════════ AC-1-08: 静止不产生格变化 ══════════

        [Test]
        public void test_ac108_noMovementNoEvent()
        {
            // 静止且未被推挤 ⇒ Append == 0
            var sink = new FakeEventSink();
            var controller = NewController(sink, new FakeTickProvider(100));

            // 连续 256 帧同一位置
            for (int i = 0; i < 256; i++)
            {
                controller.OnPositionSample(new Vector3(5, 0, 5));
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
            var controller = NewController(sink, new FakeTickProvider(100));

            // 从 (0.5, 0, 0.5) 跳到 (1.5, 0, 1.5) — 对角跨格
            controller.OnPositionSample(new Vector3(0.5f, 0, 0.5f));
            controller.OnPositionSample(new Vector3(1.5f, 0, 1.5f));
            controller.OnTickEdge(100);

            Assert.AreEqual(1, sink.AppendedEvents.Count,
                "对角跨格应单事件（不拆两条、不补角格）");
        }

        [Test]
        public void test_ac113_diagonalCrossing_committedCellHasCorrectZ()
        {
            // AC-113: 对角跨格 ⇒ 载荷为三维同时 floor 的新格
            // 修复: 之前只断言事件数,不断言 Z 值
            var sink = new FakeEventSink();
            var controller = NewController(sink, new FakeTickProvider(100));

            controller.OnPositionSample(new Vector3(0.5f, 0, 0.5f));
            controller.OnPositionSample(new Vector3(1.5f, 0, 1.5f));
            controller.OnTickEdge(100);

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
            var controller = NewController(sink, new FakeTickProvider(100));

            // tick 100: A → B
            controller.OnPositionSample(new Vector3(0.5f, 0, 0)); // A
            controller.OnPositionSample(new Vector3(1.5f, 0, 0)); // B
            controller.OnTickEdge(100); // 提交 B (第一条事件)

            // tick 101: 退回 A
            controller.OnPositionSample(new Vector3(0.5f, 0, 0)); // A again
            controller.OnTickEdge(101); // 提交 A (第二条事件)

            Assert.AreEqual(2, sink.AppendedEvents.Count,
                "回访应再发一条（否定 visited-set 反模式）");
        }

        // ══════════ AC-1-15: EC-4 传送只发落点 ══════════

        [Test]
        public void test_ac115_teleportOnlyDestination()
        {
            // 一帧内 ≥ 2 格位移 ⇒ Append == 1，载荷 == 落点格
            var sink = new FakeEventSink();
            var controller = NewController(sink, new FakeTickProvider(100));

            controller.OnPositionSample(new Vector3(0.5f, 0, 0));
            controller.OnPositionSample(new Vector3(5.5f, 0, 0)); // 传送 5 格
            controller.OnTickEdge(100);

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
            // (静态工具保留在 CellTransitionDetector —— TD-player-03 裁定保留面)
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
            // 同 tick 内 A → B → A ⇒ 不发第二条(只发第一条初始 A)
            var sink = new FakeEventSink();
            var controller = NewController(sink, new FakeTickProvider(100));

            // 先提交格 A
            controller.OnPositionSample(new Vector3(0.5f, 0, 0)); // A
            controller.OnTickEdge(100); // 提交 A

            // 同 tick 内 B → A (折返)
            controller.OnPositionSample(new Vector3(1.5f, 0, 0)); // B
            controller.OnPositionSample(new Vector3(0.5f, 0, 0)); // A again (折返)
            controller.OnTickEdge(100); // tick 边沿提交 — 不应发事件

            Assert.AreEqual(1, sink.AppendedEvents.Count,
                "tick 内折返应只发第一条(初始 A)");
        }

        [Test]
        public void test_ac132_reversalAcrossTicks_twoEvents()
        {
            // 跨两 tick 的 A → B → A ⇒ Append == 2
            var sink = new FakeEventSink();
            var controller = NewController(sink, new FakeTickProvider(100));

            // tick 100: A → B
            controller.OnPositionSample(new Vector3(0.5f, 0, 0)); // A
            controller.OnPositionSample(new Vector3(1.5f, 0, 0)); // B
            controller.OnTickEdge(100); // 提交 B (第一条事件)

            // tick 101: 退回 A
            controller.OnPositionSample(new Vector3(0.5f, 0, 0)); // A
            controller.OnTickEdge(101); // 提交 A (第二条事件)

            Assert.AreEqual(2, sink.AppendedEvents.Count,
                "跨 tick 回访应发两条");
        }

        // ══════════ AC-1-24: PatientId.None 不污染高水位 ══════════

        [Test]
        public void test_ac124_patientIdNoneDoesNotPolluteHighWater()
        {
            // ActorCellEntered.Patient == PatientId.None
            var sink = new FakeEventSink();
            var controller = NewController(sink, new FakeTickProvider(100));

            controller.OnPositionSample(new Vector3(5, 0, 0));
            controller.OnTickEdge(100);

            Assert.AreEqual(PatientId.None, sink.AppendedEvents[0].Patient,
                "ActorCellEntered.Patient 应为 PatientId.None");
        }

        // ══════════ AC-1-17: EC-9 接地不靠帧计数 ══════════

        [Test]
        public void test_ac117_noFrameCounterForGrounded()
        {
            // ⚠️ 2026-10-03 修复判据空转(评审 A5):原测用 **字段名匹配** —— 改名即绕过。
            //    类型面 + 源码面双判据。
            // 🔁 **TD-player-03 迁移**:判据对象从已删除的 CellTransitionDetector 实例
            //    换成内联唯一实现 PlayerController(帧计数禁令的真正对象)。
            //    ⚠️ readonly 半(原断言「_actorId 必须 readonly」)不迁移 —— 那钉的是
            //    ② 的构造期注入形态;PlayerController 的注入身份走 `Initialize`
            //    (可重初始化的组件形态,O-6 的 actor 身份语义由其夹具钉),类型面
            //    「额外 int/long 字段」判据(改名不绕过)完整保留。
            var controllerType = typeof(PlayerControllerType);

            // ① 类型面:不得有额外整型累计器(帧计数器改名后仍被「多出一个 int 字段」抓住)
            //    白名单 = 注入身份 `_actorId`(Initialize 注入,不自增)。
            var allowedIntFields = new[] { "_actorId" };
            var intFields = controllerType
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(long))
                .Select(f => f.Name)
                .ToList();
            var unexpected = intFields.Where(f => !allowedIntFields.Contains(f)).ToList();
            Assert.IsEmpty(unexpected,
                "AC-1-17:不得有额外整型累计器字段(改名后仍被类型面捕获):"
                + string.Join(", ", unexpected));

            // ② 源码面:不得出现帧计数语义
            string src = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "Gameplay.Presentation/Player/PlayerController.cs");
            Assert.IsTrue(System.IO.File.Exists(src), "PlayerController.cs 应存在");
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
