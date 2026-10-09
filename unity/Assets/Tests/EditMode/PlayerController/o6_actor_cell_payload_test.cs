// O-6 观察项修复测试(2026-10-09)—— ActorCellEntered 两写者的载荷编码路径
//
// O-6 登记来源: production/qa/evidence/review-o4o5-fixes-2026-10-09.md §二之二(代码面 F2)
//   PlayerController / CellTransitionDetector 原绕过 `IPayloadEncoder`,坐标手搓
//   `PayloadRef(cell.X, cell.Y, cell.Z)` 伪引用(零字节进池、无 actor 身份)
//   ⇒ 联机两 actor 同 tick 同格的两条事件 PayloadRef 同值
//   ⇒ 被 O-4 条件键(未发号补 BlobId/Offset/Length 身份)吞成一条。
//
// 判据(逐条可证伪):
//   ① test_o6_twoActorsSameTickSameCell_bothKeptInStream —— 两 actor 同 tick 同格
//      双条入真 EventStream(O-4 本例;修复前恒 Count==1);
//   ② test_o6_payloadRoundTrip_actorCellTick —— 载荷经池 round-trip 解码出
//      正确 actor_id / cell / tick(字节真进池;修复前 TryGetPayload 必 false);
//   ③ test_o6_negativeActorId_rejected —— actorId < 0 拒(ADR-006 Amendment B id 空间);
//   ④ test_o6_b6Gate_catchesHandRolledRef —— b6 门扩面后的行为级负向证据
//      (注入临时目录探针,不污染工程)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using DaYiJingCheng.Gameplay.Presentation.Player;
using NUnit.Framework;
using AssemblyGates = DaYiJingCheng.EditorTools.Gates.AssemblyGates;
using UnityEngine;
using PlayerControllerType = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class O6ActorCellPayloadTest
    {
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private FakeSink _sink;
        private FakeTickProvider _provider;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _sink = new FakeSink();
            _provider = new FakeTickProvider(100);
        }

        // ══════════ O-6 核心:两 actor 同 tick 同格不坍缩 ══════════

        [Test]
        public void test_o6_twoActorsSameTickSameCell_bothKeptInStream()
        {
            // Arrange: 两个 actor 各自的跨格检测器,喂**同 tick、同格**样本,
            // Append 到同一条真 EventStream(而非 FakeSink —— 去重只在真流上发生)。
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            var detector1 = new CellTransitionDetector(stream, _provider, _encoder, actorId: 1);
            var detector2 = new CellTransitionDetector(stream, _provider, _encoder, actorId: 2);

            const float X = 3.5f, Z = 4.5f;
            detector1.OnPositionSample(new Vector3(X, 0f, Z));
            detector2.OnPositionSample(new Vector3(X, 0f, Z));

            // Act: 同一 tick 边沿
            detector1.OnTickEdge();
            detector2.OnTickEdge();

            // Assert: 两条都入流(O-4 条件键区分 PayloadRef;修复前伪引用同值 ⇒ 第二条被吞)
            Assert.AreEqual(2, stream.Count,
                "O-6:两 actor 同 tick 同格的两条 ActorCellEntered 须都入流" +
                "(修复前手搓伪引用同值 ⇒ 被 O-4 条件键吞成一条)");

            // 且各自身份可解码区分(不是「碰巧两条」而是「两条不同的 actor」)
            var p1 = PayloadOf(stream.Events[0]);
            var p2 = PayloadOf(stream.Events[1]);
            Assert.AreEqual(1, p1.ActorId, "第一条载荷须载 actor 1");
            Assert.AreEqual(2, p2.ActorId, "第二条载荷须载 actor 2");
            Assert.AreEqual(new WorldPos(3, 0, 4), p1.Cell, "两 actor 落同一格");
            Assert.AreEqual(new WorldPos(3, 0, 4), p2.Cell, "两 actor 落同一格");

            // 反向:两事件的 PayloadRef 必不同(O-4 键可区分的根据)
            Assert.AreNotEqual(stream.Events[0].Payload, stream.Events[1].Payload,
                "两事件载荷引用必须不同(每次 Encode 发新 BlobId)—— 同值 = 退回伪引用");
        }

        [Test]
        public void test_o6_twoControllersSameTickSameCell_bothKeptInStream()
        {
            // 测试面 F-3:O-6 登记原文点名**两写者**,原专测只在 detector 写者上验;
            // PlayerController 支(独立实现)须有对偶 —— 否则「只坏 controller 侧」的
            // 形态(如共享 pending / 侧去重)无测试红。
            var stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            var c1 = new PlayerControllerType();
            var c2 = new PlayerControllerType();
            c1.Initialize(SimAuthorityMode.Host, stream, _provider, _encoder, actorId: 1);
            c2.Initialize(SimAuthorityMode.Host, stream, _provider, _encoder, actorId: 2);

            c1.OnPositionSample(new Vector3(3.5f, 0f, 4.5f));
            c2.OnPositionSample(new Vector3(3.5f, 0f, 4.5f));
            c1.OnTickEdge();
            c2.OnTickEdge();

            Assert.AreEqual(2, stream.Count,
                "O-6:两个 PlayerController 同 tick 同格的两条事件须都入流(与 detector 支对偶)");
            Assert.AreEqual(1, PayloadOf(stream.Events[0]).ActorId, "第一条载 actor 1");
            Assert.AreEqual(2, PayloadOf(stream.Events[1]).ActorId, "第二条载 actor 2");
            Assert.AreNotEqual(stream.Events[0].Payload, stream.Events[1].Payload,
                "两事件载荷引用必须不同 —— 同值 = 退回伪引用");
        }

        // ══════════ O-6 载荷 round-trip:字节真进池 ══════════

        [Test]
        public void test_o6_payloadRoundTrip_actorCellTick()
        {
            // Arrange: 真编码器 + 真池
            var detector = new CellTransitionDetector(_sink, _provider, _encoder, actorId: 7);

            // Act
            detector.OnPositionSample(new Vector3(2.5f, 1.5f, 8.25f));
            detector.OnTickEdge();

            // Assert: 事件形态
            Assert.AreEqual(1, _sink.AppendedEvents.Count);
            var evt = _sink.AppendedEvents[0];
            Assert.AreEqual(EventKind.ActorCellEntered, evt.Kind);
            Assert.AreEqual(PatientId.None, evt.Patient, "AC-1-24:不污染高水位");
            Assert.AreEqual(100, evt.Tick, "tick 来自 ITickProvider");

            // Assert: 载荷可经池 + codec 取回(修复前伪引用 ⇒ TryGetPayload false)
            var p = PayloadOf(evt);
            Assert.AreEqual(7, p.ActorId, "载荷须载 actor_id(O-6:无身份 = 本条红)");
            Assert.AreEqual(new WorldPos(2, 1, 8), p.Cell, "载荷须载三维格");
            Assert.AreEqual(100, p.Tick, "载荷 tick 须 == 事件 tick(AC-1-05)");

            // Assert: 引用三字段指向真字节(Offset/Length 非「格坐标伪装」)。
            // ⚠️ 修复前形态 = (cell.X, cell.Y, cell.Z) = (2,1,8):BlobId 2 在空池上
            //    TryGetBlob 即 false ⇒ 本组断言红(与 TryGetPayload 双保险)。
            Assert.IsTrue(_pool.TryGetBlob(evt.Payload.BlobId, out var bytes),
                "BlobId 须指向池内真字节(非格坐标伪装)");
            Assert.AreEqual(bytes.Length, evt.Payload.Length, "Length 须 = 字节长(非 Z 坐标)");
            Assert.AreEqual(0, evt.Payload.Offset, "Offset = 0(整 blob 起始)");
        }

        [Test]
        public void test_o6_playerController_payloadRoundTrip()
        {
            // PlayerController 支(独立于 detector 的第二写者)
            var controller = new PlayerControllerType();
            controller.Initialize(SimAuthorityMode.Host, _sink, _provider, _encoder, actorId: 42);

            controller.OnPositionSample(new Vector3(6.5f, 0f, 6.5f));
            controller.OnTickEdge();

            Assert.AreEqual(1, _sink.AppendedEvents.Count, "主机模式须发 1 条");
            var p = PayloadOf(_sink.AppendedEvents[0]);
            Assert.AreEqual(42, p.ActorId, "PlayerController 支须载 actor_id");
            Assert.AreEqual(new WorldPos(6, 0, 6), p.Cell);
            Assert.AreEqual(100, p.Tick);
        }

        [Test]
        public void test_o6_clientMode_zeroAppend_noEncoderSideEffect()
        {
            // 客户端模式:零 Append(AC-1-30①)—— 编码器注入不得改变该语义
            var controller = new PlayerControllerType();
            controller.Initialize(SimAuthorityMode.Client, _sink, _provider, _encoder, actorId: 42);

            controller.OnPositionSample(new Vector3(1.5f, 0f, 1.5f));
            controller.OnUplinkSample(new Vector3(2.5f, 0f, 2.5f));
            controller.OnTickEdge();

            Assert.AreEqual(0, _sink.AppendedEvents.Count, "客户端模式零 Append");
            // ⚠️ 出口条件:本断言守的是「**OnTickEdge** 不泄漏编码」。若将来 ADR-011 F1
            //    的 client 聚合上行(ADR-001 判定输入类走可靠通道)改为**编码后上行**,
            //    本条须随该裁定改判(改判归那一轮,不得由本条静默放行)。
            Assert.AreEqual(0, _pool.Count, "客户端模式 OnTickEdge 不得编码入池(零上行载荷字节)");
        }

        // ══════════ O-6 边界:actorId 非负 ══════════

        [Test]
        public void test_o6_negativeActorId_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CellTransitionDetector(_sink, _provider, _encoder, actorId: -1),
                "actorId 走 ADR-006 Amendment B 计数器 id 空间,不得为负(detector)");

            var controller = new PlayerControllerType();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => controller.Initialize(SimAuthorityMode.Host, _sink, _provider, _encoder, actorId: -1),
                "同上(controller)");

            Assert.Throws<ArgumentNullException>(
                () => new CellTransitionDetector(_sink, _provider, null, actorId: 0),
                "encoder 必填(缺注入 = 退回手搓的入口)");
        }

        // ══════════ b6 门扩面(2026-10-09):行为级负向证据 ══════════

        [Test]
        public void test_o6_b6Gate_catchesHandRolledRef()
        {
            // 在 **Assets 外**的临时目录造探针 —— 不污染工程、不触发 Unity 导入。
            // 判据:门对含 `new PayloadRef(` 的真代码报违例;仅注释里引用则不报。
            string tmp = Path.Combine(Path.GetTempPath(), "o6_b6_probe_" + Guid.NewGuid().ToString("N"));
            // 探针放**子目录**:生产违例在 Gameplay.Presentation/Player/(深度 2),
            // 平铺探针抓不住「改 SearchOption.TopDirectoryOnly」的逃逸变异。
            string sub = Path.Combine(tmp, "Sub");
            Directory.CreateDirectory(sub);
            try
            {
                // ① 真代码违例 ⇒ 必报
                File.WriteAllText(Path.Combine(sub, "Bad.cs"),
                    "class Bad { void M() { var r = new PayloadRef(1, 2, 3); } }");
                var errs = new List<string>();
                InvokeGate(errs, tmp);
                Assert.AreEqual(1, errs.Count,
                    "门须对真代码 `new PayloadRef(` 报违例(实测 " + errs.Count + " 条):\n"
                    + string.Join("\n", errs));
                StringAssert.Contains("Bad.cs", errs[0]);

                // ② 仅注释引用 ⇒ 不报(剥注释在跑,规则文档不自伤)
                Directory.Delete(tmp, true);
                Directory.CreateDirectory(sub);
                File.WriteAllText(Path.Combine(sub, "Doc.cs"),
                    "// 不得出现 new PayloadRef(\n/* 也不得出现 new PayloadRef( */\nclass Doc { }");
                errs.Clear();
                InvokeGate(errs, tmp);
                Assert.IsEmpty(errs, "注释内的引用不得误报:\n" + string.Join("\n", errs));
            }
            finally
            {
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            }
        }

        [Test]
        public void test_o6_b6Gate_scanDirsExactlyThreeFaces()
        {
            // ⚠️ 测试面 F-7(与 assembly_gate_b6_test 的 Contains 判据**不同维度**,
            //    刻意不双写):本条断言**恰三面 + 无重复** —— 增面/删面/重复登记
            //    一律红,须显式复核(清单封闭性,承 ADR-024/025 的本法)。
            //    删 `Assets/Gameplay.Presentation` ⇒ 长度 2 ⇒ 红(逃逸变异 C 的第二网)。
            //    2026-10-09 F3-②:第三面 = `Assets/Gameplay.Boot`(新装配同型扩面,
            //    防组合根侧手搓而门不可见)—— 原「恰两面」判据随扩面同步为恰三面。
            var dirsField = typeof(AssemblyGates).GetField("PayloadRefScanDirs",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(dirsField, "扫描面数组须存在");
            var dirs = (string[])dirsField.GetValue(null);

            Assert.AreEqual(3, dirs.Length,
                "b6 扫描面恰三面(Sim + Gameplay.Presentation + Gameplay.Boot)—— " +
                "增删面须显式复核,实测: " + string.Join(", ", dirs));
            Assert.AreEqual(dirs.Length, dirs.Distinct().Count(),
                "扫描面不得有重复目录(重复 = 门以为扫了多遍): " + string.Join(", ", dirs));
            CollectionAssert.Contains(dirs, "Assets/Sim");
            CollectionAssert.Contains(dirs, "Assets/Gameplay.Presentation");
            CollectionAssert.Contains(dirs, "Assets/Gameplay.Boot");
        }

        // ══════════ 测试辅助 ══════════

        private static void InvokeGate(List<string> errs, string dir)
        {
            var m = typeof(AssemblyGates).GetMethod("CheckPayloadRefCallsitesIn",
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(m, "b6 扫描体(目录形参)须存在 —— 缺失 = 负向证据无强制点");
            m.Invoke(null, new object[] { errs, new[] { dir } });
        }

        /// <summary>经池 + codec 取回强类型载荷(ADR-029 §③ 的合法读路径)。</summary>
        private ActorCellEnteredPayload PayloadOf(SimEvent e)
        {
            Assert.IsTrue(PayloadCodec.TryGetPayload(e, _pool, out ActorCellEnteredPayload p),
                "ActorCellEntered 载荷须能经池 + codec 取回(字节真的进了池 —— O-6)");
            return p;
        }

        private sealed class FakeSink : IEventSink
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

        private sealed class FakeIdAuthority : IIdAuthority
        {
            private int _next;
            public PatientId NextPatientId() => new PatientId(_next++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }

        private sealed class FakePresenceQuery : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => false;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
        }
    }
}
