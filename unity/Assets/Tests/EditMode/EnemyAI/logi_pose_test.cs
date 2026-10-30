// S19-3 测试面 —— LogiPose + ITameable/TameState 形状 + 逐 tick 整数推进(TD-enemy-01 兑现)。
//
// 权威来源:
//   ADR-016 §二(逻辑位姿在 sim,表现位姿受其单向驱动;禁反推)· §五(整数导航格)
//     · §七(驯化接口 P0 只落形状)· §九(冻结须同冻 acc)
//   GDD enemy-ai.md 规则十五(:225)· 规则十九(:267)· F-27-4(:672,推进公式权威形态)
//   sprint-19 §S19-3 判据 ①②③④
//
// 判据 → 测试映射:
//   ① 落点 = Sim.Contracts → test_logipose_landsInSimContracts / test_tameable_landsInSimContracts
//   ② 禁从 Transform 反推(源码判 + 反射判)→ test_locomotion_noTransformOrVector3_sources /
//      test_locomotion_noUnityEngineReference / test_logipose_noFloatFields(反射)
//   ③ ITameable/TameState 只落形状,驯化行为空集 → test_tameable_p0FieldsArePlaceholder /
//      test_tameable_noMutableTamePaths(反射:无 setter / 无驯化方法)
//   ④ 决策不进流、效果进流 → test_advance_pureFunction_noStreamWrites +
//      test_crossCellEffect_goesToStream_notDecision(推进零 Append,跨格效果显式写流可解码)
//
// 测试命名: test_[system]_[scenario]_[expected](test-standards)。
// 夹具纪律:纯 EditMode 逻辑测,零写盘、零外部状态(S16-4 门)。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EnemyAI
{
    public class LogiPoseTest
    {
        private PayloadEncoder _encoder;
        private InMemoryBlobPool _pool;
        private RecordingSink _sink;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _sink = new RecordingSink();
        }

        /// <summary>记录 Append 调用次数的 sink(判据④:推进算子调用它必须为 0)。</summary>
        private sealed class RecordingSink : IEventSink
        {
            public int AppendCount;
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) { AppendCount++; Events.Add(e); }
        }

        private static readonly WorldPos A = new WorldPos(3, 0, 3);
        private static readonly WorldPos B = new WorldPos(4, 0, 3);
        private static readonly WorldPos C = new WorldPos(5, 0, 3);
        // AC-27-19 性质测试的 7 格直线路径(B/C 为既有;D–G 本轮补齐)
        private static readonly WorldPos D = new WorldPos(6, 0, 3);
        private static readonly WorldPos E = new WorldPos(7, 0, 3);
        private static readonly WorldPos F = new WorldPos(8, 0, 3);
        private static readonly WorldPos G = new WorldPos(9, 0, 3);

        // ══════════ 判据 ①:落点 = Sim.Contracts ══════════

        [Test]
        public void test_logipose_landsInSimContracts()
        {
            // S19-3 判据 ①(落点裁定 2026-10-25):LogiPose 住 Sim.Contracts ——
            // 13(表现层,引用集不含 Sim)要读它是 27→13 的投影输入(ADR-025 §①)。
            Assert.AreEqual("Sim.Contracts", typeof(LogiPose).Assembly.GetName().Name,
                "LogiPose 须住 Sim.Contracts(跨装配契约:27 写、13 读)");
            Assert.IsTrue(typeof(LogiPose).IsValueType, "LogiPose 须是值类型(整数数据,零引用语义)");
        }

        [Test]
        public void test_tameable_landsInSimContracts()
        {
            Assert.AreEqual("Sim.Contracts", typeof(ITameable).Assembly.GetName().Name,
                "ITameable 须住 Sim.Contracts(ADR-025 §① 契约类型)");
            Assert.AreEqual("Sim.Contracts", typeof(TameState).Assembly.GetName().Name,
                "TameState 须住 Sim.Contracts");
            Assert.AreEqual("Sim.Contracts", typeof(TameableFields).Assembly.GetName().Name,
                "TameableFields(数据字段占位)须住 Sim.Contracts");
            Assert.IsTrue(typeof(ITameable).IsInterface, "ITameable 须是接口");
            Assert.IsTrue(typeof(TameableFields).IsValueType, "TameableFields 须是 readonly struct 占位");
        }

        // ══════════ 判据 ②:禁从 Transform/Vector3 反推(源码判 + 反射判双面) ══════════

        [Test]
        public void test_locomotion_noTransformOrVector3_sources()
        {
            // S19-3 判据 ② 源码判:推进算子与 LogiPose 的**代码**(剥注释与字符串字面量后)
            // 零 Transform / Vector3 / UnityEngine。
            // ADR-016 §二逐字「反向不可信 —— 禁从 Transform.position 反推逻辑格」。
            // ⚠️ 必须剥注释:头注里「零 Transform」这句自我声明本身含该词(实测第一版夹具
            //   未剥注释被自己文件的头注误伤 —— 词面在注释 ≠ 代码在引用)。
            AssertNoBannedTokensInCode(typeof(EnemyLocomotion), "Transform", "Vector3", "UnityEngine");
            AssertNoBannedTokensInCode(typeof(LogiPose), "Transform", "Vector3", "UnityEngine");
        }

        [Test]
        public void test_logipose_noFloatFields()
        {
            // S19-3 判据 ② 反射判:LogiPose 全部实例字段均为整数域(Cell=WorldPos, Facing=byte),
            // 零 float/double/decimal —— 结构上不可能承载表现态连续位姿。
            var fields = typeof(LogiPose).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.AreEqual(2, fields.Length, "LogiPose 恰两个字段(Cell + Facing)");
            foreach (var f in fields)
            {
                Assert.IsFalse(f.FieldType == typeof(float) || f.FieldType == typeof(double)
                    || f.FieldType == typeof(decimal) || f.FieldType == typeof(System.Numerics.Vector3),
                    $"LogiPose 字段 {f.Name} 类型 {f.FieldType} 非整数域(禁)");
            }
            var cell = fields.First(f => f.Name == "Cell");
            var facing = fields.First(f => f.Name == "Facing");
            Assert.AreEqual(typeof(WorldPos), cell.FieldType, "Cell = WorldPos(整数格)");
            Assert.AreEqual(typeof(byte), facing.FieldType, "Facing = byte(离散朝向)");
        }

        // ══════════ 判据 ③:驯化只落形状,行为路径空集 ══════════

        [Test]
        public void test_tameable_p0FieldsArePlaceholder()
        {
            // AC-27-33:动物实体 OwnerId = None 且 TameState = Wild(P0 恒定)。
            var fields = TameableFields.P0;
            Assert.AreEqual(TameState.Wild, fields.Tame, "P0 占位:Tame 恒 Wild");
            Assert.AreEqual(PatientId.None, fields.OwnerId, "P0 占位:OwnerId 恒 None(-1)");
            Assert.IsTrue(PatientId.None.IsNone, "哨兵语义:None.IsNone = true(ADR-007 §四)");
        }

        [Test]
        public void test_tameable_noMutableTamePaths()
        {
            // AC-27-33「驯化行为代码路径为空集」的反射面:
            // ① 接口只有两个只读属性(get、无 set accessor);
            // ② TameableFields 占位零可变实例字段、零方法(无 EnterTaming/Adopt/Tame 等入口);
            // ③ TameState 枚举成员数恰 4(P0 闭集)。
            var props = typeof(ITameable).GetProperties();
            Assert.AreEqual(2, props.Length, "ITameable 恰两属性");
            foreach (var p in props)
            {
                Assert.IsNotNull(p.GetMethod, $"{p.Name} 须有 get");
                Assert.IsNull(p.SetMethod, $"{p.Name} 不得有 set(P0 只落形状,禁可变驯化路径)");
            }

            Assert.AreEqual(0, typeof(TameableFields)
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Length,
                "TameableFields 占位零实例字段(恒定值由属性返回,防零值陷阱静默非法态)");
            Assert.AreEqual(0, typeof(TameableFields)
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName)
                    .Count(),
                "TameableFields 占位零实例方法(无驯化行为入口)");

            var names = Enum.GetNames(typeof(TameState));
            Assert.AreEqual(4, names.Length, "TameState P0 闭集恰 4 值");
            Assert.AreEqual(TameState.Wild, (TameState)0, "0 = Wild(默认值 = 合法 P0 态,与 PatientId 的零值陷阱方向相反)");
            Assert.AreEqual(new[] { "Wild", "Taming", "Tamed", "Owned" }, names,
                "枚举成员名按 ADR-016 §七 形状");
        }

        // ══════════ F-27-4 推进公式(权威形态,非 StepAccumulator 直线逼近) ══════════

        [Test]
        public void test_advance_oneCellPerOverflow_alongPath()
        {
            // F-27-4:acc 溢出一次 ⇒ 沿路径恰好前进一格(非直线逼近)。
            var path = new List<WorldPos> { A, B, C };
            var acc = Fix.Zero;
            int cursor = 0;
            var pose = new LogiPose(A, 0);

            // speed = 1.0 格/tick ⇒ 每 tick 溢出一次,前进一格。
            var speed = Fix.One;
            pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor, moving: true, speed: speed);
            Assert.AreEqual(B, pose.Cell, "tick 1:acc 溢出 ⇒ A→B(沿路径,恰一格)");
            Assert.AreEqual(1, cursor, "cursor 随推进前进");
            Assert.AreEqual(0, acc.Raw, "溢出后 acc 余数归零(1.0 恰好消费)");

            pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor, moving: true, speed: speed);
            Assert.AreEqual(C, pose.Cell, "tick 2:B→C");
            Assert.AreEqual(2, cursor);
        }

        [Test]
        public void test_advance_fractionalAccumulation_carriesRemainder()
        {
            // 定点余数跨 tick 存续:speed = 0.5 ⇒ 两个 tick 溢出一次(0.5 → 1.0)。
            var path = new List<WorldPos> { A, B, C };
            var acc = Fix.Zero;
            int cursor = 0;
            var pose = new LogiPose(A, 0);
            var half = Fix.FromRational(1, 2);

            pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor, true, half);
            Assert.AreEqual(A, pose.Cell, "tick 1:acc=0.5 未溢出 ⇒ 原地");
            Assert.AreEqual(half.Raw, acc.Raw, "acc 余数 0.5 跨 tick 保持");

            pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor, true, half);
            Assert.AreEqual(B, pose.Cell, "tick 2:acc=1.0 溢出 ⇒ A→B");
            Assert.AreEqual(0, acc.Raw);
        }

        [Test]
        public void test_advance_frozenKeepsAcc_notZeroing()
        {
            // F-27-3/F-27-4 纪律:Moving=false ⇒ acc 保持不累加**不归零** ——
            // 归零是 F-27-3 同型穿墙错误的反面(Engage 站定攒的余数不得凭空消失或爆发)。
            var path = new List<WorldPos> { A, B, C };
            var acc = Fix.FromRational(3, 4); // 站定前攒下的余数
            int cursor = 0;
            var pose = new LogiPose(A, 0);

            pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor, moving: false, speed: Fix.One);
            Assert.AreEqual(A, pose.Cell, "冻结 ⇒ 位姿不动");
            Assert.AreEqual(Fix.FromRational(3, 4).Raw, acc.Raw,
                "冻结 tick acc 保持(不累加不归零 —— 规则十/F-27-4 冻结分支)");

            // 解冻首 tick:余数照常消费,不「吐累积量」(speed=0.25 ⇒ 0.75+0.25=1.0 恰溢出)。
            var quarter = Fix.FromRational(1, 4);
            pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor, true, quarter);
            Assert.AreEqual(B, pose.Cell, "解冻首 tick 正常消费余数,零瞬移");
        }

        // ══════════ AC-27-19 性质测试(F-27-4 权威载体) ══════════
        // 🔁 **2026-10-25(TD-enemy-02 收敛轮)**:原 `deterministic_pathing_test.test_steppingProperty`
        //    打在 `StepAccumulator`(直线逼近,已删)上 —— **载体打错算子**(AC 原文引 F-27-4)。
        //    性质测试现住本文件,打 `Advance`。穷举速度 × Moving 序列(**确定性枚举,非随机**):
        //    含冻结进入/退出(EC-27-23 的触发路径 = 冻结期攒 acc 解冻后吐出)。

        [Test]
        public void test_ac2719_property_speedBelowOne_andMovingSequences()
        {
            // AC-27-19:任意 ENEMY_SPEED < FIX_ONE × 任意 Moving(e) 序列 ⇒
            //   ① 0 ≤ acc < FIX_ONE 恒成立(判据用字面量 65536,不自指被测代码);
            //   ② LogiPose.Cell 整数性(WorldPos 为 int 域 struct;断言 cell ∈ path 前缀);
            //   ③ 每 tick 位移 ≤ 1 格(EC-27-04 前提:speed < FIX_ONE ⇒ while 至多溢出一次)。
            // 速度样本:0.0000152 / 0.0078 / 0.5 / 0.99998 —— 全部 raw < 65536(FIX_ONE)。
            var speeds = new[]
            {
                new Fix(1), new Fix(511), Fix.FromRational(1, 2), new Fix(65535),
            };
            // Moving 序列样本:匀速前进 / 冻结攒余后解冻(EC-27-23 触发路径)/ 逐 tick 交替。
            var sequences = new[]
            {
                new[] { true, true, true, true, true, true, true, true, true, true, true, true },
                new[] { true, true, true, false, false, false, false, false, true, true, true, true },
                new[] { true, false, true, false, true, false, true, false, true, false, true, false },
            };
            var path = new List<WorldPos> { A, B, C, D, E, F, G };   // 7 格直线,短于 12 tick ⇒ 覆盖路径耗尽分支

            foreach (var speed in speeds)
            {
                Assert.Less(speed.Raw, 65536, "样本前置:速度 < FIX_ONE(AC GIVEN)");
                foreach (var seq in sequences)
                {
                    var acc = Fix.Zero;
                    int cursor = 0;
                    var pose = new LogiPose(A, 0);
                    for (int t = 0; t < seq.Length; t++)
                    {
                        WorldPos before = pose.Cell;
                        pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor,
                            moving: seq[t], speed: speed);

                        // ① acc 不变式(字面量判据 —— 与 Fix.OneRaw 同值但不引用被测常量表达)
                        Assert.GreaterOrEqual(acc.Raw, 0L,
                            $"t={t} speed={speed.Raw} seq[{t}]:acc 不得为负");
                        Assert.Less(acc.Raw, 65536L,
                            $"t={t} speed={speed.Raw} seq[{t}]:acc 不得 ≥ FIX_ONE(隧穿前兆,EC-27-23)");
                        // ② 整数性:cell 必为路径上的格(整数域 struct 的运行时面)
                        CollectionAssert.Contains(path, pose.Cell,
                            $"t={t}:cell 须为整数路径格(整数性)");
                        // ③ 每 tick 位移 ≤ 1 格(曼哈顿;moving=false 须为 0)
                        int dist = Math.Abs(pose.Cell.X - before.X) + Math.Abs(pose.Cell.Z - before.Z);
                        Assert.LessOrEqual(dist, 1,
                            $"t={t} speed={speed.Raw}:单 tick 位移 {dist} > 1 格(隧穿)");
                        if (!seq[t]) Assert.AreEqual(before, pose.Cell, $"t={t}:冻结 tick 位姿不动");
                    }
                }
            }
        }

        [Test]
        public void test_advance_pathExhausted_staysAndClampsAcc()
        {
            // **GDD 规格**:`enemy-ai.md` F-27-4 的「`Path` 耗尽 ⇒ `acc := 0`」分支
            // (2026-10-30 `TD-enemy-03` 结案时由用户裁定追认为规格 —— 原文对该情形零字,
            //  而实现替规格做了决定)。
            // 路径耗尽:cursor 停在终点,`acc` **归零**(不是「保持」也不是「钳到 FIX_ONE−ε」);
            // 调用方此后应置 `moving=false`(GDD 同节末的调用方义务)。
            var path = new List<WorldPos> { A, B };
            var acc = Fix.Zero;
            int cursor = 1; // 已在终点 B
            var pose = new LogiPose(B, 0);

            pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor, true, Fix.One);
            Assert.AreEqual(B, pose.Cell, "路径耗尽 ⇒ 原地");
            Assert.AreEqual(1, cursor, "cursor 不越界");
            Assert.AreEqual(0, acc.Raw, "acc 归零(GDD F-27-4 的 Path 耗尽分支)");
        }

        /// <summary>
        /// **`Path` 耗尽时 `acc` 精确归零** —— 与「保持」/「钳到 `FIX_ONE − ε`」两者都不同。
        /// <para>⚠️ <b>为什么单列这一条</b>(2026-10-30 `TD-enemy-03`):上一条的 `acc` 从 **0** 起,
        /// 加上一个整格后恰为 `FIX_ONE` ⇒ 「归零」得 0、「保持」得 `FIX_ONE` —— **可区分但只差一格**;
        /// 而「钳到 `FIX_ONE − ε`」在这种输入下也会落在 0 附近,判别力弱。
        /// 本条让 `acc` 从**非零小数**起步,三种收口给出**三个不同值**,判别力最强:
        /// 归零 ⇒ `0` · 保持 ⇒ `acc₀ + speed`(≥ `FIX_ONE`)· 钳到 `FIX_ONE − 1` ⇒ `65535`。</para>
        /// </summary>
        [Test]
        public void test_advance_pathExhausted_accIsExactlyZero_notHeldNotClamped()
        {
            var path = new List<WorldPos> { A, B };
            var acc = new Fix(12345);   // 非零小数余数
            int cursor = 1;             // 已在终点 B
            var pose = new LogiPose(B, 0);

            pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor, true, Fix.One);

            Assert.AreEqual(B, pose.Cell, "路径耗尽 ⇒ 原地");
            Assert.AreEqual(0, acc.Raw,
                "`acc` 须**精确归零** —— 保持会得 " + (12345 + 65536) + ",钳到 FIX_ONE−1 会得 65535");
        }

        [Test]
        public void test_advance_facingTracksNextPathCell()
        {
            // F-27-4 DirFrom:朝向 = 路径上「下一格」方向;终点格保持上一段方向。
            var path = new List<WorldPos> { A, B, C };
            var acc = Fix.Zero;
            int cursor = 0;
            var pose = new LogiPose(A, 5 /* 随意初值 */);

            pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor, true, Fix.One);
            Assert.AreEqual(0, pose.Facing, "A→B 后朝向取 B 的下一格方向 B→C = +X = 0");

            // 直接到达终点(游标=终点):无下一格 ⇒ Facing 保持。
            int endCursor = 1;
            var atEnd = new LogiPose(B, 2);
            atEnd = EnemyLocomotion.Advance(atEnd, ref acc, path, ref endCursor, false, Fix.One);
            Assert.AreEqual(2, atEnd.Facing, "冻结 tick Facing 保持");

            // 终点溢出(acc 收口路径)不更新朝向。
            int terminalCursor = 1;
            var atTerminal = new LogiPose(B, 3);
            var afterTerminal = EnemyLocomotion.Advance(atTerminal, ref acc, path, ref terminalCursor, true, Fix.One);
            Assert.AreEqual(3, afterTerminal.Facing, "终点格无下一格 ⇒ Facing 保持(上段方向)");
        }

        [Test]
        public void test_facingBetween_sixDirectionsMatchAdjacencyOrder()
        {
            // 0..5 = +X/−X/+Y/−Y/+Z/−Z(与 EnemyPathing 邻接生成顺序一致)。
            var o = new WorldPos(0, 0, 0);
            Assert.AreEqual(0, EnemyLocomotion.FacingBetween(o, new WorldPos(1, 0, 0)), "+X = 0");
            Assert.AreEqual(1, EnemyLocomotion.FacingBetween(o, new WorldPos(-1, 0, 0)), "−X = 1");
            Assert.AreEqual(2, EnemyLocomotion.FacingBetween(o, new WorldPos(0, 1, 0)), "+Y = 2");
            Assert.AreEqual(3, EnemyLocomotion.FacingBetween(o, new WorldPos(0, -1, 0)), "−Y = 3");
            Assert.AreEqual(4, EnemyLocomotion.FacingBetween(o, new WorldPos(0, 0, 1)), "+Z = 4");
            Assert.AreEqual(5, EnemyLocomotion.FacingBetween(o, new WorldPos(0, 0, -1)), "−Z = 5");
        }

        // ══════════ 判据 ④:决策不进流、效果进流 ══════════

        [Test]
        public void test_advance_pureFunction_noStreamWrites()
        {
            // 判据④ 前半(决策不进流):推进算子是纯派生态求值 ——
            // 全程零 IEventSink.Append(夹具 sink 全程无人触碰)。
            // ADR-016 §一:AI 决策是派生态,效果才进流。
            var path = new List<WorldPos> { A, B, C };
            var acc = Fix.Zero;
            int cursor = 0;
            var pose = new LogiPose(A, 0);

            // 连续多 tick 推进(含跨格 tick),sink 计数恒 0。
            for (int i = 0; i < 4; i++)
            {
                pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor, true, Fix.One);
            }
            Assert.AreEqual(0, _sink.AppendCount,
                "决策推进全程零 Append(ADR-016 §一:决策不进流)");
            Assert.AreEqual(C, pose.Cell, "前提:推进确实发生了(夹具有效性)");
        }

        [Test]
        public void test_crossCellEffect_goesToStream_notDecision()
        {
            // 判据④ 后半(效果进流):跨格那一刻,「效果」以 ActorCellEntered 显式写世界流
            // (写者 = 调用方,非推进算子);写出的载荷可经编码器/解码器往返取回 ——
            // 证明效果进的是「流」而非内存可变态。
            var path = new List<WorldPos> { A, B, C };
            var acc = Fix.Zero;
            int cursor = 0;
            var pose = new LogiPose(A, 0);
            var prev = pose;

            pose = EnemyLocomotion.Advance(pose, ref acc, path, ref cursor, true, Fix.One);

            // 调用方形态:检测到跨格 ⇒ 显式构造效果事件并 Append(主机唯一写,ADR-005)。
            if (pose.Cell != prev.Cell)
            {
                var payload = new ActorCellEnteredPayload(
                    actorId: 27, cell: pose.Cell, tick: 100);
                var pref = _encoder.Encode(EventKind.ActorCellEntered, payload);
                _sink.Append(new SimEvent(100, PatientId.None, seq: 1,
                    EventKind.ActorCellEntered, pref));
            }

            Assert.AreEqual(1, _sink.AppendCount, "跨格 ⇒ 效果事件恰一条进流");
            Assert.AreEqual(StreamId.World, StreamRouting.Of(EventKind.ActorCellEntered),
                "效果事件路由到世界流(StreamRouting 纯函数,ADR-008 §一)");
            var decoded = new PayloadDecoder(_pool).Decode<ActorCellEnteredPayload>(
                EventKind.ActorCellEntered, _sink.Events[0].Payload);
            Assert.AreEqual(B, decoded.Cell, "流里的效果 = 推进算子算出的新格(决策→效果 单向)");
            Assert.AreEqual(27, decoded.ActorId);
            Assert.AreEqual(100L, decoded.Tick);
        }

        [Test]
        public void test_locomotion_routeDecisionAndEffectSeparation_mechanical()
        {
            // 机械面:EnemyLocomotion 的**代码**(剥注释后)零 IEventSink / Append / SimEvent ——
            // 决策算子与流的物理隔离(不只靠测试行为,连依赖都不存在)。
            AssertNoBannedTokensInCode(typeof(EnemyLocomotion), "IEventSink", "Append", "SimEvent");
        }

        // ══════════ 夹具 ══════════

        /// <summary>
        /// 源码判夹具:读类型源文件,**剥掉注释与字符串字面量后**断言零禁词。
        /// (词面出现在注释 ≠ 代码在引用 —— 第一版夹具未剥注释被头注的自我声明误伤,已修。)
        /// </summary>
        private static void AssertNoBannedTokensInCode(Type t, params string[] banned)
        {
            string src = System.IO.File.ReadAllText(AssetPathOf(t));
            string code = StripCommentsAndStrings(src);
            foreach (var token in banned)
            {
                Assert.IsFalse(code.Contains(token),
                    $"{t.Name}: 代码(剥注释/字符串后)含禁词 {token}(S19-3 判据 ②/④ 机械面)");
            }
        }

        /// <summary>线性扫描剥 // 与 /* */ 注释与 "..." 字符串字面量(保留结构;零依赖)。</summary>
        private static string StripCommentsAndStrings(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool inLineComment = false, inBlockComment = false, inString = false, inChar = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char next = i + 1 < src.Length ? src[i + 1] : '\0';
                if (inLineComment)
                {
                    if (c == '\n') { inLineComment = false; sb.Append(c); }
                    continue;
                }
                if (inBlockComment)
                {
                    if (c == '*' && next == '/') { inBlockComment = false; i++; }
                    continue;
                }
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && next == '/') { inLineComment = true; i++; continue; }
                if (c == '/' && next == '*') { inBlockComment = true; i++; continue; }
                if (c == '"') { inString = true; sb.Append('"'); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>定位类型源文件相对 unity/ 的 Assets 路径(源码判夹具;零写盘)。</summary>
        private static string AssetPathOf(Type t)
        {
            // EditMode 测试装配与生产装配同源于 Assets/;源文件路径 = Assets/<装配目录>/<文件>.cs。
            // 用类型所属装配名推导目录(与本仓布局一致:Sim → Assets/Sim/,Sim.Contracts → Assets/Sim.Contracts/)。
            var asmName = t.Assembly.GetName().Name;
            return $"Assets/{asmName}/{t.Name}.cs";
        }
    }
}
