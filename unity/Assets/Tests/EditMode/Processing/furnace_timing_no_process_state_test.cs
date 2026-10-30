// S20-9 · processing story-004 —— 执行时序:单炉并发 / 无进程态 / 重放等值
//
// 权威来源:
//   design/gdd/processing.md
//     · 规则八(:300-370)时序 —— 发起即落流、完成 = 派生;并发模型表
//     · F-18.3(:458-461)`CompleteTick(craft) = craft.start_tick + recipe.duration_ticks`
//     · 规则七(:255-277)点火事务五步;R-18-D(:279-297)拒绝在时点
//     · AC-18-12 / 13 / 17 / 23(**AC-18-15 = EXTERNAL·BLOCKED-BY-ADR-012,本件不勾**)
//   ADR-005 / ADR-008(全序键)/ ADR-010 §三(7a 无 18 段)/ ADR-025 §①(门 A)/ ADR-031(解码路径)
//   story production/epics/processing/story-004-furnace-timing-and-no-process-state.md
//
// ─────────────────────────────────────────────────────────────────────────────
// AC 记分(禁把「本体跑通」读成「五条全绿」)
// ─────────────────────────────────────────────────────────────────────────────
//   ✅ AC-18-13 零浮点零墙钟:源码面(剥注释)+ IL 面(Cecil 扫 Processing 四类型)+
//               **正控**(扫描器对注入的墙钟调用必红 ⇒ 扫描不是恒绿摆设)。
//   ✅ AC-18-12 时点拒绝:准入谓词逐行增量 + **spy-sink 零 Append** +
//               库存投影 `CanonicalForm` 逐位不变 + **负控**(绕过准入的接线 ⇒ 两条断言必红)。
//   ✅ AC-18-23 单炉 / 跨玩家:忙区间边界(`tick == CompleteTick` 不忙)、第二意图、
//               跨玩家互不影响、同 tick 双玩家、`BusyActorCount`、乱序输入。
//   ✅ AC-18-17 无进程态三重:① 18 命名空间零 `[Serializable]`(含**负控**)·
//               ② 存档组版器零 18 类型引用 · ③ 重放重建 `CompleteTick` 与直算逐位相同(100 点)
//               + 独立重建等值(无隐藏状态)。
//   ⛔ AC-18-15:跨平台三格 ⇒ `EXTERNAL · BLOCKED-BY-ADR-012`,**不勾、不写桩**。
//               本件只交 Mono 侧重放等值(= 17③),那**不是** 15 的证据。
//
// ─────────────────────────────────────────────────────────────────────────────
// 三条必须读清的边界(与 AC 记分同读,单独看不成立)
// ─────────────────────────────────────────────────────────────────────────────
//  ① **`20.Apply` 不存在**(归 S20-14)—— 故本件的「容量先验」由
//     `IgnitionAdmission.CanAdmit` 承位,接线夹具用**测试内注入的 apply port** 模拟第 ④ 步。
//     该 port **不是生产代码**,是判据形态的载体;S20-14 落地时把真实 `Apply` 塞进同一 seam。
//  ② **「库存逐位不变」今天的判别力来自负控,不是来自正例** —— 生产侧尚无改动库存的路径,
//     正例天然恒真。⇒ 本件**同时**跑一个「绕过准入直接改库存」的负控接线,
//     证明该断言在**应当红的时候真的会红**。只跑正例 = 假绿(本项目已抓过五次的形态)。
//  ③ **AC-18-17 ② 的「段清单」是解释,不是常量** —— 7a 没有名为「段清单」的机器可读清单
//     (`SaveComposer` 布局 = 头部 + 事件段 + 折叠段,快照段只有 `SnapshotOffset = 0` 哨兵)。
//     ⇒ 断言改判为可机器执行的形式:**存档组版器与 codec 零 18 类型引用**。
//     这是**降级后的判据**,已在 story §Test Evidence 如实登记,禁读成「段清单已核对」。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘;全部 id / 数值写死。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Processing;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.EditMode.Processing
{
    /// <summary>processing story-004 的 EditMode 夹具(4 条 AC 全覆盖;AC-18-15 EXTERNAL 不测)。</summary>
    [TestFixture]
    public class FurnaceTimingNoProcessStateTest
    {
        // ══════════════════════════════════════════════════════════════════
        // 共享夹具
        // ══════════════════════════════════════════════════════════════════

        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void Setup()
        {
            var pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(pool);
            _decoder = new PayloadDecoder(pool);
        }

        // ── 事件构造(真编码器 + 真池 + 真解码器,零 mock)──────────────────

        private SimEvent Craft(int actorId, long startTick, long durationTicks,
                               long envelopeTick, long seq,
                               int recipeId = 1,
                               long[] inputIds = null,
                               int[] consumed = null,
                               int[] outQty = null,
                               int[] outQuality = null,
                               long[] outIds = null)
        {
            var payload = new CraftPayload(
                actorId, recipeId, startTick, durationTicks,
                inputIds ?? new long[] { 101 },
                consumed ?? new int[] { 1 },
                outQty ?? new int[] { 1 },
                outQuality ?? new int[] { 3 },
                outIds ?? new long[] { 201 },
                new WorldPos(4, 0, 7));
            return new SimEvent(envelopeTick, PatientId.None, seq, EventKind.Craft,
                                _encoder.Encode(EventKind.Craft, payload));
        }

        /// <summary>造一炉:actor <paramref name="actorId"/> 于 <paramref name="startTick"/> 点火,
        /// 时长 <paramref name="durationTicks"/>。</summary>
        /// <summary>造一条「库存里已有东西」的种子事件(TD-processing-01 正例的前置用)。</summary>
        private SimEvent SeedDrop(long instanceId, int itemKey, int qty)
            => new SimEvent(0L, PatientId.None, -1, EventKind.DropSpawned,
                _encoder.Encode(EventKind.DropSpawned,
                    new DropSpawnedPayload(instanceId, new WorldPos(0, 0, 0), itemKey, qty)));

        private SimEvent Burn(int actorId, long startTick, long durationTicks, long seq = 0)
            => Craft(actorId, startTick, durationTicks, startTick, seq);

        // ══════════════════════════════════════════════════════════════════
        // AC-18-13 · CompleteTick 路径零浮点、零墙钟(源码面 + IL 面 + 正控)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>18 的生产源文件 —— 扫描面的单一出处。</summary>
        private static readonly string[] FurnaceSourceFiles =
        {
            "Assets/Sim/Processing/FurnaceState.cs",
            "Assets/Sim/Processing/IgnitionAdmission.cs",
        };

        /// <summary>AC-18-13 的符号黑名单(GDD 明文三类 + 两个同族墙钟/环境时钟)。</summary>
        private static readonly string[] WallClockTypes =
        {
            "System.DateTime",
            "System.DateTimeOffset",
            "System.Diagnostics.Stopwatch",
            "UnityEngine.Time",
            "System.Environment",       // TickCount / TickCount64 = 墙钟的另一种拼写
        };

        /// <summary>AC-18-13 源码面的浮点类型名(标识符边界匹配,非子串)。</summary>
        private static readonly string[] FloatTypeNames = { "float", "double", "decimal" };

        /// <summary>找 `name` 在 `code` 里以**标识符边界**出现的全部位置。
        /// <para>⚠️ 为什么不用 `IndexOf("float ")`:带尾空格会漏 `float[]` / `(float)` /
        /// `float?` / `float,` —— 2026-10-26 评审 W5 的实测盲区。</para></summary>
        private static IEnumerable<int> IdentifierOccurrences(string code, string name)
        {
            int at = code.IndexOf(name, StringComparison.Ordinal);
            while (at >= 0)
            {
                int after = at + name.Length;
                bool leftOk = at == 0 || !IsIdentChar(code[at - 1]);
                bool rightOk = after >= code.Length || !IsIdentChar(code[after]);
                if (leftOk && rightOk) yield return at;
                at = code.IndexOf(name, at + 1, StringComparison.Ordinal);
            }
        }

        private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        [Test]
        public void test_ac1813_sourceSurface_hasNoFloatingPointTypes()
        {
            string root = RepoRoot();
            var hits = new List<string>();

            foreach (string rel in FurnaceSourceFiles)
            {
                string code = StripComments(File.ReadAllText(Path.Combine(root, "unity", rel)));
                // ⚠️ 用**标识符边界**匹配,不用 `"float "` 这种带尾空格的子串
                //    (2026-10-26 评审 W5):带空格的写法漏掉 `float[]` / `(float)x` / `float?` /
                //    `float,` —— 而那几种恰恰是声明浮点变量的常见形态。这里改成
                //    「前后都不是标识符字符」,覆盖全部出现位置。
                foreach (string bad in FloatTypeNames)
                {
                    foreach (int at in IdentifierOccurrences(code, bad))
                        hits.Add($"{rel}:{LineOf(code, at)} 出现 `{bad}`");
                }
            }

            Assert.That(hits, Is.Empty,
                "CompletionTick 路径出现浮点类型(ADR-006 §二:整数域求值):\n  "
                + string.Join("\n  ", hits));
        }

        [Test]
        public void test_ac1813_sourceSurface_hasNoWallClockIdentifiers()
        {
            string root = RepoRoot();
            var hits = new List<string>();

            foreach (string rel in FurnaceSourceFiles)
            {
                string code = StripComments(File.ReadAllText(Path.Combine(root, "unity", rel)));
                foreach (string bad in new[] { "DateTime", "Stopwatch", "Time.", "TickCount", "Environment" })
                {
                    int at = code.IndexOf(bad, StringComparison.Ordinal);
                    while (at >= 0)
                    {
                        hits.Add($"{rel}:{LineOf(code, at)} 出现 `{bad}`");
                        at = code.IndexOf(bad, at + 1, StringComparison.Ordinal);
                    }
                }
            }

            Assert.That(hits, Is.Empty,
                "CompletionTick 路径出现墙钟标识符(GDD 规则八:tick 计,非墙钟):\n  "
                + string.Join("\n  ", hits));
        }

        [Test]
        public void test_ac1813_ilSurface_hasNoFloatOpcodes()
        {
            string dll = AssemblyGates.ScriptAssemblyPath("Sim");
            Assert.That(File.Exists(dll), Is.True, $"Sim 装配不存在:{dll}");

            var targets = ProcessingTypeNames();
            var hits = new List<string>();
            int scanned = ScanMethods(dll, targets, (type, method, ins) =>
            {
                if (IsFloatOpcode(ins.OpCode))
                    hits.Add($"{type}.{method}: {ins.OpCode} @ IL_{ins.Offset:X4}");
            });

            Assert.That(scanned, Is.GreaterThan(0),
                "IL 扫描扫到 **零个** 方法体 —— 靶类型没找着(命名空间漂移?),拒以空集冒充绿");
            Assert.That(hits, Is.Empty,
                "CompletionTick 路径 IL 面出现浮点指令(反射看不见的常量折叠 / 库返回值落栈):\n  "
                + string.Join("\n  ", hits));
        }

        [Test]
        public void test_ac1813_ilSurface_hasNoWallClockSymbolReferences()
        {
            string dll = AssemblyGates.ScriptAssemblyPath("Sim");
            Assert.That(File.Exists(dll), Is.True, $"Sim 装配不存在:{dll}");

            var hits = new List<string>();
            int scanned = ScanMethods(dll, ProcessingTypeNames(), (type, method, ins) =>
            {
                string owner = ReferencedTypeFullName(ins.Operand);
                if (owner == null) return;
                foreach (string bad in WallClockTypes)
                {
                    if (owner == bad || owner.StartsWith(bad + ".", StringComparison.Ordinal))
                        hits.Add($"{type}.{method}: `{owner}` @ IL_{ins.Offset:X4}");
                }
            });

            Assert.That(scanned, Is.GreaterThan(0), "IL 扫描扫到零个方法体 —— 拒以空集冒充绿");
            Assert.That(hits, Is.Empty,
                "CompletionTick 路径引用了墙钟符号(GDD 规则八:完成 = tick 到达,非墙钟到达):\n  "
                + string.Join("\n  ", hits));
        }

        /// <summary>AC-18-13 的**正控** —— 扫描器对注入的墙钟调用必红。
        /// <para>⚠️ 没有这条,上面两条「零命中」无法区分「真的没有」与「扫描器什么都没扫到」。
        /// 与 `AssemblyGates` 的 IlScanNegativeFixture 同一先例(借测试装配编译,零新增 asmdef)。</para>
        /// </summary>
        [Test]
        public void test_ac1813_scannerPositiveControl_detectsInjectedWallClock()
        {
            string dll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts.Tests");
            Assert.That(File.Exists(dll), Is.True, $"测试装配不存在:{dll}");

            var targets = new HashSet<string>(StringComparer.Ordinal)
            {
                "DaYiJingCheng.Tests.EditMode.Processing.FurnaceWallClockNegativeFixture",
            };

            var hits = new List<string>();
            int scanned = ScanMethods(dll, targets, (type, method, ins) =>
            {
                string owner = ReferencedTypeFullName(ins.Operand);
                if (owner == null) return;
                foreach (string bad in WallClockTypes)
                {
                    if (owner == bad || owner.StartsWith(bad + ".", StringComparison.Ordinal))
                        hits.Add($"{type}.{method}: `{owner}`");
                }
            });

            Assert.That(scanned, Is.GreaterThan(0),
                "正控靶类型的方法体未被扫到 —— 扫描器本身失效,AC-18-13 的两条零命中不可信");
            Assert.That(hits, Is.Not.Empty,
                "正控注入的 `DateTime` / `Stopwatch` 调用**未被扫出** —— 扫描器判别力为零," +
                "AC-18-13 的「零命中」是假绿");
        }

        /// <summary>浮点指令扫的**正控**(2026-10-26 评审 W6 补)。
        /// <para>墙钟面早有正控,浮点面此前**没有** —— 也就是说「零浮点指令」那条断言
        /// 从来没被证明过它有判别力。本夹具注入真浮点运算,扫描器必须扫出来。</para>
        /// </summary>
        [Test]
        public void test_ac1813_ilFloatScan_positiveControl_detectsInjectedFloat()
        {
            string dll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts.Tests");
            Assert.That(File.Exists(dll), Is.True, $"测试装配不存在:{dll}");

            var targets = new HashSet<string>(StringComparer.Ordinal)
            {
                "DaYiJingCheng.Tests.EditMode.Processing.FurnaceFloatNegativeFixture",
            };

            var hits = new List<string>();
            int scanned = ScanMethods(dll, targets, (type, method, ins) =>
            {
                if (IsFloatOpcode(ins.OpCode))
                    hits.Add($"{type}.{method}: {ins.OpCode} @ IL_{ins.Offset:X4}");
            });

            Assert.That(scanned, Is.GreaterThan(0),
                "浮点正控靶类型的方法体未被扫到 —— 扫描器本身失效");
            Assert.That(hits, Is.Not.Empty,
                "正控注入的浮点运算**未被扫出** —— 浮点指令扫描判别力为零," +
                "AC-18-13 源码侧之外的「零浮点」是假绿");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-12 · 点火时点容量拒绝(零 Craft / 库存逐位不变 / 炉不点燃)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>记录型 sink(零静态状态 —— 每次测试 new 一个)。</summary>
        private sealed class SpySink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
        }

        /// <summary>规则七第 ④ 步的 **seam** —— 生产侧 `20.Apply` 归 S20-14,本件用接口占位。
        /// <para>⚠️ 本接口**不是** ADR 契约件,只是把「第 ④ 步会改库存」这件事变成可注入的,
        /// 从而让「拒绝时库存不变」这条断言**有判别力**(见下方负控)。</para>
        /// </summary>
        private interface IIgnitionApplyPort
        {
            bool TryApply(long currentLoad, IReadOnlyList<OutputLoadLine> outputs,
                          CarryCapacity capacity, IEventSink sink, long tick);
        }

        /// <summary>真接线:容量先验 = <see cref="IgnitionAdmission.CanAdmit"/>;**接受前零写入**。</summary>
        private sealed class GatedApplyPort : IIgnitionApplyPort
        {
            public int ApplyCalls;

            public bool TryApply(long currentLoad, IReadOnlyList<OutputLoadLine> outputs,
                                 CarryCapacity capacity, IEventSink sink, long tick)
            {
                ApplyCalls++;
                return IgnitionAdmission.CanAdmit(currentLoad, outputs, capacity).Admitted;
            }
        }

        /// <summary>**负控**接线:无视容量直接改库存(模拟「先扣后补」)。
        /// <para>它的唯一用途是证明下面那条「拒绝 ⇒ 库存逐位不变」的断言**在应当红时会红**。
        /// ⚠️ 绝不进生产树。</para>
        /// </summary>
        private sealed class AlwaysAcceptingBrokenPort : IIgnitionApplyPort
        {
            private readonly PayloadEncoder _encoder;
            private long _nextInstanceId;

            /// <summary>⚠️ <paramref name="encoder"/> 必须与测试解码器**共用同一个池** ——
            /// 负控自带私有池会让 `PayloadRef` 指进另一池,解码炸在 codec 里
            /// (2026-10-26 首轮实测就是这个错),那时红的是夹具而非被证明的断言。</summary>
            public AlwaysAcceptingBrokenPort(PayloadEncoder encoder, long firstInstanceId)
            {
                _encoder = encoder; _nextInstanceId = firstInstanceId;
            }

            public bool TryApply(long currentLoad, IReadOnlyList<OutputLoadLine> outputs,
                                 CarryCapacity capacity, IEventSink sink, long tick)
            {
                // 「部分成功」= 直接落一条产出实例 —— 无论容量。
                _nextInstanceId++;
                var p = new DropSpawnedPayload(_nextInstanceId, new WorldPos(4, 0, 7), 0, 1);
                sink.Append(new SimEvent(tick, PatientId.None, 0, EventKind.DropSpawned,
                                         _encoder.Encode(EventKind.DropSpawned, p)));
                return true;
            }
        }

        /// <summary>规则七的五步点火接线(**本件交付的判据形态**,不是 S20-14 的最终实现)。
        /// <para>② 单炉 → ④ 容量先验 → ⑤ 只在两步都过后 Append。</para>
        /// </summary>
        private bool TryIgnite(IReadOnlyList<SimEvent> stream, IEventSink sink,
                               IIgnitionApplyPort port, int actorId, long tick,
                               long currentLoad, IReadOnlyList<OutputLoadLine> outputs,
                               CarryCapacity capacity)
        {
            // ② 单炉占用检查(规则八)
            if (FurnaceState.IsBusy(stream, _decoder, actorId, tick))
                return false;

            // ④ 容量先验(规则七;时点裁决 —— 此刻尚无任何事件)
            if (!port.TryApply(currentLoad, outputs, capacity, sink, tick))
                return false;

            // ⑤ 只有两步都过了才落流
            CraftPayload payload = new CraftPayload(
                actorId, recipeId: 1, startTick: tick, durationTicks: 100,
                inputInstanceIds: new long[] { 101 }, actualConsumed: new int[] { 1 },
                outputQty: new int[] { 1 }, outputQuality: new int[] { 3 },
                outputInstanceIds: new long[] { 201 }, toolCell: new WorldPos(4, 0, 7));
            sink.Append(new SimEvent(tick, PatientId.None, 0, EventKind.Craft,
                                     _encoder.Encode(EventKind.Craft, payload)));
            return true;
        }

        [Test]
        public void test_ac1812_outputExceedingCapacity_isRejectedAtIgnitionTick()
        {
            var capacity = new CarryCapacity(10);
            var outputs = new[] { new OutputLoadLine(7, 2) };   // 14 > 10

            IgnitionAdmissionResult r = IgnitionAdmission.CanAdmit(0L, outputs, capacity);

            Assert.That(r.Admitted, Is.False, "产出 14 > 容量 10 ⇒ 必须拒绝(R-18-D 前半)");
            Assert.That(r.RejectedLineIndex, Is.EqualTo(0), "单行产出被拒 ⇒ 点名第 0 行");
        }

        [Test]
        public void test_ac1812_rejection_appendsZeroCraftAndLeavesInventoryBitIdentical()
        {
            var capacity = new CarryCapacity(10);
            var outputs = new[] { new OutputLoadLine(7, 2) };   // 14 > 10
            var sink = new SpySink();
            var port = new GatedApplyPort();

            // 🔁 TD-processing-01:原版用**空流**,「库存逐位不变」结构性恒真(空流折两次必然同值)。
            //    现改为先塞一条真实库存,让 `before` 非空 —— 断言才有东西可丢。
            var stream = new List<SimEvent> { SeedDrop(instanceId: 901, itemKey: 3, qty: 2) };
            string emptyForm = InventoryFold.Fold(new List<SimEvent>(), _decoder, InventoryFoldContext.None)
                                           .Projection.CanonicalForm();
            string before = InventoryFold.Fold(stream, _decoder, InventoryFoldContext.None).Projection.CanonicalForm();
            Assert.That(before, Is.Not.EqualTo(emptyForm),
                "前置不成立:种子没有改变库存投影 ⇒ 下面的「逐位不变」仍是结构性恒真(TD-processing-01 复现)");

            bool ignited = TryIgnite(stream, sink, port, actorId: 1, tick: 100,
                                     currentLoad: 0L, outputs: outputs, capacity: capacity);

            string after = InventoryFold.Fold(stream, _decoder, InventoryFoldContext.None).Projection.CanonicalForm();

            Assert.That(ignited, Is.False, "容量不足 ⇒ 点火不发生(AC-18-12)");
            Assert.That(port.ApplyCalls, Is.EqualTo(1),
                "第 ④ 步必须**被调用过**(拒绝发生在裁决里,不是跳过了裁决)");
            Assert.That(sink.Events, Is.Empty,
                "拒绝时 `Craft` 事件**零落流**(R-18-D:拒绝在时点,不存在中途回滚)");
            Assert.That(after, Is.EqualTo(before),
                "拒绝时库存投影**逐位不变**(20 的硬不变量不被本次点火触碰)");

            // 炉不点燃:拒绝后单炉谓词仍 false(时点拒绝无副作用)。
            stream.AddRange(sink.Events);
            Assert.That(FurnaceState.IsBusy(stream, _decoder, 1, 100), Is.False,
                "被拒的点火不得留下任何「在烧」痕迹(炉不点燃)");
        }

        [Test]
        public void test_ac1812_rejectionAssertion_hasDiscriminatingPower()
        {
            // ── 负控:同一套断言接在一个**绕过容量检查直接改库存**的接线后面。
            //    若这里仍然是绿的,说明「拒绝 ⇒ 零 Append / 库存不变」那两条断言**恒真**,
            //    即它们是自造断言而非判据(本项目已抓过五次的假绿形态)。
            var capacity = new CarryCapacity(10);
            var outputs = new[] { new OutputLoadLine(7, 2) };
            var sink = new SpySink();
            var broken = new AlwaysAcceptingBrokenPort(_encoder, firstInstanceId: 500);

            var stream = new List<SimEvent>();
            string before = InventoryFold.Fold(stream, _decoder, InventoryFoldContext.None).Projection.CanonicalForm();

            bool ignited = TryIgnite(stream, sink, broken, actorId: 1, tick: 100,
                                     currentLoad: 0L, outputs: outputs, capacity: capacity);

            var afterEvents = new List<SimEvent>(sink.Events);
            string after = InventoryFold.Fold(afterEvents, _decoder, InventoryFoldContext.None).Projection.CanonicalForm();

            Assert.That(ignited, Is.True, "负控接线恒接受 —— 它必须真的走到 Append 那一步");
            Assert.That(sink.Events, Is.Not.Empty,
                "负控接线**没有**写出事件 ⇒ 「零 Append」断言对「写不写」毫无判别力," +
                "AC-18-12 的零事件判据是假绿");
            Assert.That(after, Is.Not.EqualTo(before),
                "负控接线改了库存但投影逐位相同 ⇒ 「库存逐位不变」断言对「改没改」毫无判别力");
        }

        [Test]
        public void test_ac1812_exactBoundary_isAdmitted()
        {
            var capacity = new CarryCapacity(10);
            var outputs = new[] { new OutputLoadLine(5, 2) };   // 恰 10 == cap

            IgnitionAdmissionResult r = IgnitionAdmission.CanAdmit(0L, outputs, capacity);

            Assert.That(r.Admitted, Is.True,
                "恰等界**合法**(F-20.2 是 `≤` 而非 `<`):10 == CARRY_CAP 10 可点火");
        }

        [Test]
        public void test_ac1812_multiLineAccumulation_rejectsTheLineThatOverflows()
        {
            var capacity = new CarryCapacity(10);
            // 3 + 3 + 6 = 12 > 10;前两行各 3,第三行 6 令总量越界。
            var outputs = new[]
            {
                new OutputLoadLine(3, 1),
                new OutputLoadLine(3, 1),
                new OutputLoadLine(6, 1),
            };

            IgnitionAdmissionResult r = IgnitionAdmission.CanAdmit(0L, outputs, capacity);

            Assert.That(r.Admitted, Is.False, "逐行累加 12 > 10 ⇒ 整体拒绝");
            Assert.That(r.RejectedLineIndex, Is.EqualTo(2),
                "点名**越界那一行**(第 2 行);若报第 0 行说明谓词没做累加");
        }

        [Test]
        public void test_ac1812_multiLineAccumulation_usesFullRowWeightNotUnitWeight()
        {
            // ⚠️ 前一条 multiLine 夹具三行全 qty=1 ⇒ 对「累加时用了 `UnitWeight` 而非
            //    `InstanceWeight`」这个错**零判别力**(两者在 qty=1 时同值)。本夹具补这个面。
            var capacity = new CarryCapacity(7);
            var outputs = new[]
            {
                new OutputLoadLine(2, 2),   // 本行实重 4
                new OutputLoadLine(2, 2),   // 本行实重 4;累计 8 > 7
            };

            IgnitionAdmissionResult r = IgnitionAdmission.CanAdmit(0L, outputs, capacity);

            Assert.That(r.Admitted, Is.False,
                "逐行累计 4 + 4 = 8 > 7 ⇒ 必须拒绝;若按单件重量累加(2 + 4 = 6 ≤ 7)会被误放行");
            Assert.That(r.RejectedLineIndex, Is.EqualTo(1), "越界的是第 1 行");
        }

        [Test]
        public void test_ac1812_currentLoadIsCounted_soExistingWeightCanBlockIgnition()
        {
            var capacity = new CarryCapacity(10);
            var outputs = new[] { new OutputLoadLine(3, 1) };   // 本行只 3

            Assert.That(IgnitionAdmission.CanAdmit(0L, outputs, capacity).Admitted, Is.True,
                "空身 3 ≤ 10 ⇒ 可点火");
            Assert.That(IgnitionAdmission.CanAdmit(9L, outputs, capacity).Admitted, Is.False,
                "已有 9 + 产出 3 = 12 > 10 ⇒ 拒绝(容量判据看的是**后态**载重)");
        }

        [Test]
        public void test_ac1812_nullOutputs_failsLoud()
        {
            // 2026-10-26 评审 B1:「漏传输出」与「真的没有产出」必须分开。
            // 合并成「恒装得下」= 给 AC-18-12 开一条 fail-open 捷径。
            var capacity = new CarryCapacity(10);

            Assert.Throws<ArgumentNullException>(
                () => IgnitionAdmission.CanAdmit(0L, null, capacity),
                "outputs == null 必须抛 —— 不得当作「零产出 ⇒ 恒装得下」放行");
        }

        [Test]
        public void test_ac1812_negativeLoad_failsLoudEvenWithEmptyOutputs()
        {
            // 负载重守卫的位置:必须在循环**之前**判,否则空列表不进循环 ⇒ 静默放行。
            var capacity = new CarryCapacity(10);

            Assert.Throws<InvalidOperationException>(
                () => IgnitionAdmission.CanAdmit(-5L, Array.Empty<OutputLoadLine>(), capacity),
                "空产出 + 负载重:域错误仍须抛(旧版会静默返回 Admitted=true)");

            Assert.Throws<InvalidOperationException>(
                () => IgnitionAdmission.CanAdmit(-5L,
                    new[] { new OutputLoadLine(1, 1) }, capacity),
                "非空产出 + 负载重:同样抛");
        }

        [Test]
        public void test_ac1812_emptyOutputs_isTriviallyAdmitted()
        {
            var capacity = new CarryCapacity(1);
            // ⚠️ 只断**空列表**。null 现在抛 —— 见 test_ac1812_nullOutputs_failsLoud
            //    (2026-10-26 评审 B1:把「漏传」与「真的没有产出」合并是 fail-open 捷径)。
            //    空列表返回 true 是 `for` 零迭代的**自然结果**,不是被显式保证的语义:
            //    合法配方不可能零产出(item-database.md:399 烘焙期非空 + 逐条 max(1,·))。
            Assert.That(IgnitionAdmission.CanAdmit(0L, Array.Empty<OutputLoadLine>(), capacity).Admitted,
                Is.True, "空产出(非 null)不增加载重 ⇒ 循环零迭代 ⇒ 恒装得下");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-23 · 单炉 / 不可取消 / 跨玩家并发
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1823_secondIgnitionWhileBurning_isRejectedWithZeroEvents()
        {
            var capacity = new CarryCapacity(100);
            var sink = new SpySink();
            var port = new GatedApplyPort();
            var outputs = new[] { new OutputLoadLine(1, 1) };

            var stream = new List<SimEvent> { Burn(actorId: 1, startTick: 100, durationTicks: 50) };

            bool ignited = TryIgnite(stream, sink, port, actorId: 1, tick: 120,
                                     currentLoad: 0L, outputs: outputs, capacity: capacity);

            Assert.That(ignited, Is.False, "actor 1 在 tick 120 仍在烧(100+50=150)⇒ 第二意图必拒");
            Assert.That(sink.Events, Is.Empty, "拒绝 ⇒ 零事件(AC-18-23 前半)");
            Assert.That(port.ApplyCalls, Is.Zero,
                "单炉检查(第 ②步)**先于**容量先验(第 ④步)⇒ 被单炉拒时不该走到 Apply(规则七次序)");
        }

        [Test]
        public void test_ac1823_completionTickItself_isIdle()
        {
            // ⚠️ **本夹具钉的是实现口径,不是 GDD 明文**(2026-10-26 评审 W2 订正)。
            //    GDD 全文未定义「在烧」的开闭区间:`:308` 只给 CompleteTick 公式,
            //    `:480` 只写「已有一炉在烧 ⇒ 拒绝」。**「完成当拍可再点火」这句
            //    在 processing.md 里一处也找不到。**
            //    ⇒ 本夹具验证的是 `IsBusy` 取半开区间 `[start, start+duration)` 这个
            //      **实现选择**;它是自洽的(与 F-18.3「完成 = 时间到达」一致),
            //      但**不构成 GDD 判据**。若产品要的是闭区间,改 `IsBusy` 的 `<` 即可,
            //      本夹具随之改 —— 不涉及任何 AC。
            var stream = new List<SimEvent> { Burn(actorId: 1, startTick: 100, durationTicks: 50) };

            Assert.That(FurnaceState.IsBusy(stream, _decoder, 1, 149), Is.True,
                "tick 149(完成前一拍)⇒ 仍在烧");
            Assert.That(FurnaceState.IsBusy(stream, _decoder, 1, 150), Is.False,
                "tick 150 == CompleteTick ⇒ 不忙(半开区间 [start, start+duration) 的实现口径)");
            Assert.That(FurnaceState.IsBusy(stream, _decoder, 1, 151), Is.False,
                "tick 151 ⇒ 已完成");
        }

        [Test]
        public void test_ac1823_crossPlayerConcurrency_isAllowed()
        {
            var capacity = new CarryCapacity(100);
            var sink = new SpySink();
            var port = new GatedApplyPort();
            var outputs = new[] { new OutputLoadLine(1, 1) };

            var stream = new List<SimEvent> { Burn(actorId: 1, startTick: 100, durationTicks: 50) };

            // actor 2 同 tick 点火 ⇒ 必须成功(各玩家各自单炉,器具互斥归 24,18 不判)
            bool ignited = TryIgnite(stream, sink, port, actorId: 2, tick: 120,
                                     currentLoad: 0L, outputs: outputs, capacity: capacity);

            Assert.That(ignited, Is.True, "跨玩家并发**允许**(AC-18-23 后半)");
            Assert.That(sink.Events.Count, Is.EqualTo(1), "actor 2 的 Craft 正常落流");
            Assert.That(sink.Events[0].Kind, Is.EqualTo(EventKind.Craft));
            Assert.That(FurnaceState.IsBusy(stream, _decoder, 1, 120), Is.True,
                "actor 1 仍在烧 —— 别人的点火不改自己的炉态");
        }

        [Test]
        public void test_ac1823_sameTickTwoPlayers_bothBusy()
        {
            var stream = new List<SimEvent>
            {
                Burn(actorId: 1, startTick: 100, durationTicks: 40, seq: 0),
                Burn(actorId: 2, startTick: 100, durationTicks: 90, seq: 1),
            };

            Assert.That(FurnaceState.IsBusy(stream, _decoder, 1, 100), Is.True);
            Assert.That(FurnaceState.IsBusy(stream, _decoder, 2, 100), Is.True);
            Assert.That(FurnaceState.BusyActorCount(stream, _decoder, 100), Is.EqualTo(2),
                "同 tick 两玩家各一炉 ⇒ 两人都在烧(两条 Craft 均落流)");

            Assert.That(FurnaceState.BusyActorCount(stream, _decoder, 140), Is.EqualTo(1),
                "tick 140:actor 1 已完(100+40),actor 2 仍烧 ⇒ 计数 1");
            Assert.That(FurnaceState.BusyActorCount(stream, _decoder, 190), Is.Zero,
                "tick 190:两炉皆完 ⇒ 计数 0");
        }

        [Test]
        public void test_ac1823_noCancelApiExists()
        {
            // 取消 = **无路径**(GDD 规则八「不允许取消」)。
            // 判据 = 类型上不可表达:公开成员里没有任何名字暗示取消 / 撤销 / 中断。
            var cancelish = typeof(FurnaceState)
                .GetMethods(System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Static |
                            System.Reflection.BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .Where(n => n.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0
                         || n.IndexOf("Abort", StringComparison.OrdinalIgnoreCase) >= 0
                         || n.IndexOf("Reclaim", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            Assert.That(cancelish, Is.Empty,
                "`FurnaceState` 出现取消类 API —— 与「不允许取消」冲突(OQ-18-3 [甲]);" +
                "禁「预留但不断言」的灰色接口");
        }

        [Test]
        public void test_furnaceNonPositiveDuration_failsLoud()
        {
            // 域收窄:`duration_ticks` 归 21a 数据表,值 ≤ 0 是**坏数据**而非「立即完成」。
            // 静默放行会让 `CompleteTick == StartTick` ⇒ 点火当拍即可再点火 ⇒ 单炉判据失效。
            var stream = new List<SimEvent> { Burn(actorId: 1, startTick: 100, durationTicks: 0) };

            var ex = Assert.Throws<InvalidOperationException>(
                () => { var _ = FurnaceState.IsBusy(stream, _decoder, 1, 100); },
                "duration_ticks = 0 必须硬失败(21a 数据表硬失败族,规则八:18 只消费不猜)");
            Assert.That(ex.Message, Does.Contain("duration_ticks"),
                "失败信息须点名 duration_ticks —— 否则排障时看不出坏的是哪个字段");
        }

        [Test]
        public void test_furnaceStreamWithNonCraftEvents_isIgnored()
        {
            // 入参契约明文「可传全流」⇒ 非 `Craft` 的 Kind 必须被跳过,
            // 而不是被当成 `CraftPayload` 去解码(codec 会抛,表现为莫名其妙的数据损坏)。
            var pool = new InMemoryBlobPool();
            var enc = new PayloadEncoder(pool);
            var dec = new PayloadDecoder(pool);

            var stream = new List<SimEvent>
            {
                new SimEvent(50, PatientId.None, 0, EventKind.DropSpawned,
                             enc.Encode(EventKind.DropSpawned,
                                        new DropSpawnedPayload(900, new WorldPos(1, 1, 1), 0, 1))),
                new SimEvent(100, PatientId.None, 0, EventKind.ActorCellEntered,
                             enc.Encode(EventKind.ActorCellEntered,
                                        new ActorCellEnteredPayload(1, new WorldPos(2, 0, 3), 100))),
                new SimEvent(100, PatientId.None, 1, EventKind.Craft,
                             enc.Encode(EventKind.Craft,
                                        new CraftPayload(1, 1, 100, 50,
                                                         new long[] { 101 }, new int[] { 1 },
                                                         new int[] { 1 }, new int[] { 3 },
                                                         new long[] { 201 }, new WorldPos(4, 0, 7)))),
            };

            Assert.That(FurnaceState.IsBusy(stream, dec, 1, 120), Is.True,
                "流里混有非 Craft 事件时仍须正确判忙(域外 Kind 一律跳过)");
            Assert.That(FurnaceState.BusyActorCount(stream, dec, 120), Is.EqualTo(1),
                "非 Craft 事件不得被当成炉计入忙者数");
        }

        [Test]
        public void test_decoderNull_failsLoud()
        {
            // 空解码器若被宽容处理 ⇒ 「谁都不忙」的静默失效。
            var stream = new List<SimEvent> { Burn(actorId: 1, startTick: 100, durationTicks: 50) };

            Assert.Throws<ArgumentNullException>(
                () => FurnaceState.IsBusy(stream, null, 1, 120),
                "IsBusy 收 null 解码器必须抛,不得返回 false(那是「没人在烧」的静默谎报)");
            Assert.Throws<ArgumentNullException>(
                () => FurnaceState.TryGetLatest(stream, null, 1, 120, out _),
                "TryGetLatest 收 null 解码器必须抛");
            Assert.Throws<ArgumentNullException>(
                () => FurnaceState.BusyActorCount(stream, null, 120),
                "BusyActorCount 收 null 解码器必须抛,不得返回 0");
        }

        [Test]
        public void test_ac1823_futureEnvelopeEvent_doesNotLeakBackIntoPastPrefix()
        {
            // 2026-10-26 评审 W1 的反例:tick 前缀 = **存在时点**(envelope),不是载荷 StartTick。
            // 非法流 —— 信封 tick=200 才落流,但载荷自称 start_tick=50 / duration=30。
            // 问 tick=100 时这条事件**尚未发生** ⇒ 必须看不见它。
            //
            // 旧版(前缀用 p.StartTick)会把它计入「截至 100 的前缀」⇒ 算出已有炉
            // (CompleteTick=80,早已烧完)⇒ 回溯污染了过去时点。
            var stream = new List<SimEvent>
            {
                Craft(actorId: 1, startTick: 50, durationTicks: 30, envelopeTick: 200, seq: 0),
            };

            Assert.That(FurnaceState.IsBusy(stream, _decoder, 1, 100), Is.False,
                "tick 100 时该 Craft 尚不存在(envelope 200)⇒ 不得有炉在烧");
            Assert.That(FurnaceState.TryCompleteTickOf(stream, _decoder, 1, 100, out _), Is.False,
                "tick 100 处 «截至此刻是否点过火» 必须为 false —— 不得因载荷 StartTick 靠前而泄漏");

            // 到了 200 之后才该看得见。
            Assert.That(FurnaceState.TryCompleteTickOf(stream, _decoder, 1, 200, out long done), Is.True,
                "envelope 200 落流后,该炉才进入前缀");
            Assert.That(done, Is.EqualTo(80L), "F-18.3 仍用载荷 start+duration = 50+30");
        }

        [Test]
        public void test_ac1823_outOfOrderStream_givesSameAnswer()
        {
            var ordered = new List<SimEvent>
            {
                Burn(actorId: 1, startTick: 100, durationTicks: 30, seq: 0),
                Burn(actorId: 1, startTick: 130, durationTicks: 30, seq: 0),
            };
            var shuffled = new List<SimEvent> { ordered[1], ordered[0] };

            foreach (long t in new long[] { 99, 100, 129, 130, 159, 160 })
            {
                Assert.That(
                    FurnaceState.IsBusy(shuffled, _decoder, 1, t),
                    Is.EqualTo(FurnaceState.IsBusy(ordered, _decoder, 1, t)),
                    $"乱序输入在 tick {t} 给出了不同答案 —— 前缀判据依赖了列表顺序而非全序键");
            }

            // 「最后一炉」的判定按**全序键**,不按列表位置。
            Assert.That(FurnaceState.TryCompleteTickOf(shuffled, _decoder, 1, 200, out long complete),
                Is.True);
            Assert.That(complete, Is.EqualTo(160L),
                "最后一炉 = start 130 + duration 30 ⇒ CompleteTick 160(F-18.3)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-17 · 无进程态三重门
        // ══════════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════════
        // TD-processing-05 · `IgnitionAdmissionResult` 的两字段不得自相矛盾
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_td05_passFactory_hasCanonicalShape()
        {
            IgnitionAdmissionResult r = IgnitionAdmissionResult.Pass();
            Assert.That(r.Admitted, Is.True);
            Assert.That(r.RejectedLineIndex, Is.EqualTo(-1), "放行时被拒行下标必须是 -1");
        }

        [Test]
        public void test_td05_rejectAt_namesTheLine()
        {
            IgnitionAdmissionResult r = IgnitionAdmissionResult.RejectAt(2);
            Assert.That(r.Admitted, Is.False);
            Assert.That(r.RejectedLineIndex, Is.EqualTo(2));
        }

        [Test]
        public void test_td05_rejectAtNegative_throws()
        {
            Assert.That(() => IgnitionAdmissionResult.RejectAt(-1),
                Throws.TypeOf<ArgumentOutOfRangeException>(),
                "拒绝必须点名被拒行(≥ 0),-1 是「放行」的哨兵,不得复用");
        }

        [Test]
        public void test_td05_ctorRejectsSelfContradictoryCombinations()
        {
            Assert.That(() => new IgnitionAdmissionResult(true, 3),
                Throws.TypeOf<ArgumentOutOfRangeException>(),
                "(true, 3) = 放行却点名被拒行 —— 自相矛盾(TD-processing-05 的原始形态)");
            Assert.That(() => new IgnitionAdmissionResult(false, -1),
                Throws.TypeOf<ArgumentOutOfRangeException>(),
                "(false, -1) = 拒绝却不点名 —— 下游读 RejectedLineIndex 会拿到无意义的 -1");
        }

        [Test]
        public void test_ac1817a_processingNamespace_hasNoSerializableTypes()
        {
            var offenders = new List<string>();
            int scanned = 0;

            foreach (Type t in typeof(FurnaceState).Assembly.GetTypes())
            {
                if (t.Namespace == null ||
                    !t.Namespace.StartsWith("DaYiJingCheng.Sim.Processing", StringComparison.Ordinal))
                    continue;
                scanned++;
                if (t.IsDefined(typeof(SerializableAttribute), inherit: false))
                    offenders.Add(t.FullName);
            }

            Assert.That(scanned, Is.GreaterThan(0),
                "18 命名空间在 `Sim` 装配内 **零类型** —— 扫描面不存在,拒以空集冒充绿");
            Assert.That(offenders, Is.Empty,
                "18 命名空间出现 `[Serializable]` 类型 —— 「有进程态可存」的类型面证据(AC-18-17 ①):\n  "
                + string.Join("\n  ", offenders));
        }

        [Test]
        public void test_ac1817a_serializableScan_hasDiscriminatingPower()
        {
            // 负控:同一个反射谓词打到本测试件里刻意标了 [Serializable] 的类型上必须命中。
            // 否则上面那条「零命中」无法区分「真的没有」与「谓词什么都没扫到」。
            Type probe = typeof(FurnaceSerializableNegativeFixture);
            bool hit = probe.IsDefined(typeof(SerializableAttribute), inherit: false);

            Assert.That(hit, Is.True,
                "负控类型 `FurnaceSerializableNegativeFixture` 未被 `[Serializable]` 谓词识别 —— " +
                "AC-18-17 ① 的零命中是假绿");
        }

        [Test]
        public void test_ac1817b_saveComposerReferencesNoProcessingTypes()
        {
            // ⚠️ 判据降级登记(见文件头 ③):7a 没有名为「段清单」的机器可读清单
            //    ⇒ 改判为「存档组版器与 codec **零 18 类型引用**」——
            //      无引用 ⇒ 无处可写 18 记录。这是可机器执行的形式,不是原话。
            string root = RepoRoot();
            string[] families =
            {
                "Assets/Gameplay.Boot",
                "Assets/Sim.Codec",
            };
            string[] furnaceTypes =
            {
                "FurnaceState", "FurnaceOccupancy", "IgnitionAdmission",
                "IgnitionAdmissionResult", "OutputLoadLine",
            };

            var hits = new List<string>();
            var scanned = new List<string>();

            foreach (string dir in families)
            {
                string abs = Path.Combine(root, "unity", dir);
                if (!Directory.Exists(abs)) continue;
                foreach (string file in Directory.GetFiles(abs, "*.cs", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(Path.Combine(root, "unity"), file)
                                     .Replace('\\', '/');
                    scanned.Add(rel);
                    string code = StripComments(File.ReadAllText(file));
                    foreach (string t in furnaceTypes)
                    {
                        if (code.IndexOf(t, StringComparison.Ordinal) >= 0)
                            hits.Add($"{rel} 引用了 18 类型 `{t}`");
                    }
                }
            }

            Assert.That(scanned, Is.Not.Empty,
                "存档 / codec 源文件零命中 —— 扫描面不存在,拒以空集冒充绿");

            // ⚠️ 2026-10-27 修(评审 W8 / §三 L7):原断言是 `filesScanned > 0`,
            //    它只证「目录里至少有一个 .cs」,**不证存档组版器本身在扫描集内**。
            //    若扫描根写错、或 SaveComposer.cs 被改名 / 移走,该夹具**仍然绿** ——
            //    即「零引用」可能是「压根没扫到那个文件」。
            //    ⇒ 改为**按名点名**:下面这些文件必须出现在扫描集里,缺一即红。
            string[] requiredAnchors =
            {
                // 存档组版器 —— AC-18-17 ② 的判据正主(「18 记录不得进存档段」)
                "Assets/Gameplay.Boot/SaveComposer.cs",
            };
            foreach (string anchor in requiredAnchors)
            {
                Assert.That(scanned, Does.Contain(anchor),
                    $"扫描集里没有 `{anchor}` —— 该文件被改名 / 移走 / 扫描根写错 ⇒ "
                    + "下面那条「零引用」是**空扫描**得来的,不成立。"
                    + $"\n  实扫到 {scanned.Count} 个文件,前 5 个:"
                    + (scanned.Count == 0 ? "(无)" : "\n    " + string.Join("\n    ", scanned.GetRange(0, Math.Min(5, scanned.Count)))));
            }

            Assert.That(hits, Is.Empty,
                "存档侧引用了 18 类型 ⇒ 存在「把进程态写进存档」的路径(AC-18-17 ②):\n  "
                + string.Join("\n  ", hits));
        }

        /// <summary>TD-processing-02 负控:同一个「18 类型引用」源码谓词打到**必然含这些名字的目录**上必须命中。
        /// <para>缺它时,上面那条「零引用」无法区分「存档侧真的没引用」与「谓词什么都没扫到」。</para></summary>
        [Test]
        public void test_ac1817b_typeScan_hasDiscriminatingPower()
        {
            string[] furnaceTypes =
            {
                "FurnaceState", "FurnaceOccupancy", "IgnitionAdmission",
                "IgnitionAdmissionResult", "OutputLoadLine",
            };
            // 本测试件自己就在这个目录里,且**必然**引用上表全部名字 ⇒ 扫描器必须命中。
            string dir = Path.Combine(RepoRoot(), "unity", "Assets", "Tests", "EditMode", "Processing");
            Assert.That(Directory.Exists(dir), Is.True, $"负控扫描面不存在 —— {dir}");

            var hits = new List<string>();
            int scanned = 0;
            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                scanned++;
                string code = StripComments(File.ReadAllText(file));
                foreach (string t in furnaceTypes)
                    if (code.IndexOf(t, StringComparison.Ordinal) >= 0) hits.Add($"{Path.GetFileName(file)}:{t}");
            }

            Assert.That(scanned, Is.GreaterThan(0), "负控扫描面为空 —— 本负控自身无判别力");
            Assert.That(hits, Is.Not.Empty,
                "谓词在**必然含 18 类型名**的目录上零命中 ⇒ 上一条「存档侧零引用」是扫了个空(假绿)");
        }

        [Test]
        public void test_ac1817c_replayRebuiltCompleteTick_matchesDirectComputation()
        {
            // 100 点采样:同一条流,重放重建的 CompleteTick 与 F-18.3 直算**逐位**相同。
            var stream = new List<SimEvent>();
            for (int i = 0; i < 20; i++)
            {
                stream.Add(Burn(actorId: 1, startTick: 10 * i, durationTicks: 7 + i, seq: (long)i));
            }

            int checkedPoints = 0;
            for (int k = 0; k < 100; k++)
            {
                long tick = k * 4;                     // 0, 4, 8 … 396
                if (!FurnaceState.TryCompleteTickOf(stream, _decoder, 1, tick, out long fromStream))
                    continue;

                // 直算:取 start ≤ tick 的最后一炉,手算 start + duration。
                long expectStart = -1, expectDuration = 0;
                for (int i = 0; i < 20; i++)
                {
                    long start = 10L * i;
                    if (start > tick) continue;
                    expectStart = start;
                    expectDuration = 7 + i;
                }
                Assert.That(expectStart, Is.GreaterThanOrEqualTo(0),
                    $"tick {tick} 处直算也应有炉 —— 夹具自身的期望值算错了");
                Assert.That(fromStream, Is.EqualTo(expectStart + expectDuration),
                    $"tick {tick}:重放重建 {fromStream} ≠ 直算 {expectStart}+{expectDuration}");
                checkedPoints++;
            }

            Assert.That(checkedPoints, Is.GreaterThan(50),
                $"只有 {checkedPoints} 个采样点真的做了比较 —— 采样面塌了,等于没测");
        }

        [Test]
        public void test_ac1817c_independentRebuilds_areBitIdentical()
        {
            // 「从空重建等值」—— 无隐藏状态的结构性证明:
            // ① 同一份事件、两次独立调用 ⇒ 逐位同;
            // ② 用**全新池 + 全新编解码器**重新编码同一批载荷 ⇒ 结果仍逐位同
            //    (证明结论只依赖事件内容,不依赖编码器实例 / 池地址 / 调用序)。
            var original = new List<SimEvent>
            {
                Burn(actorId: 1, startTick: 100, durationTicks: 30, seq: 0),
                Burn(actorId: 2, startTick: 110, durationTicks: 45, seq: 1),
                Burn(actorId: 1, startTick: 130, durationTicks: 60, seq: 0),
            };

            var pool2 = new InMemoryBlobPool();
            var enc2 = new PayloadEncoder(pool2);
            var rebuilt = new List<SimEvent>();
            foreach (SimEvent e in original)
            {
                CraftPayload p = _decoder.Decode<CraftPayload>(EventKind.Craft, e.Payload);
                rebuilt.Add(new SimEvent(e.Tick, e.Patient, e.Seq, e.Kind,
                                         enc2.Encode(EventKind.Craft, p)));
            }

            foreach (long tick in new long[] { 0, 100, 120, 130, 145, 160, 200 })
            {
                foreach (int actor in new[] { 1, 2, 3 })
                {
                    Assert.That(
                        FurnaceState.IsBusy(rebuilt, _decoder, actor, tick),
                        Is.EqualTo(FurnaceState.IsBusy(original, _decoder, actor, tick)),
                        $"重编码后 tick {tick} / actor {actor} 的忙态不同 —— 求值依赖了编码器实例而非事件内容");

                    Assert.That(
                        FurnaceState.TryCompleteTickOf(rebuilt, _decoder, actor, tick, out long a),
                        Is.EqualTo(FurnaceState.TryCompleteTickOf(original, _decoder, actor, tick,
                                                                  out long b)),
                        $"重编码后 tick {tick} / actor {actor} 的有炉性不同");
                    Assert.That(a, Is.EqualTo(b),
                        $"重编码后 tick {tick} / actor {actor} 的 CompleteTick 不同");
                }
            }

            // ② 空流 ⇒ 必然「谁都不忙」(零隐藏初始状态的另一半)。
            Assert.That(FurnaceState.BusyActorCount(new List<SimEvent>(), _decoder, 0), Is.Zero,
                "空流下忙者数须为 0 —— 非零说明存在静态可变状态泄漏到求值里");
        }

        // ══════════════════════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════════════════════

        /// <summary>18 在 `Sim` 装配内的靶类型全名 —— 扫描面的单一出处。</summary>
        private static HashSet<string> ProcessingTypeNames() => new HashSet<string>(StringComparer.Ordinal)
        {
            "DaYiJingCheng.Sim.Processing.FurnaceState",
            "DaYiJingCheng.Sim.Processing.FurnaceOccupancy",
            "DaYiJingCheng.Sim.Processing.IgnitionAdmission",
            "DaYiJingCheng.Sim.Processing.IgnitionAdmissionResult",
            "DaYiJingCheng.Sim.Processing.OutputLoadLine",
        };

        /// <summary>对 dll 内命中 <paramref name="targets"/> 的方法体逐指令回调;返回扫过的方法数。</summary>
        private static int ScanMethods(string dll, HashSet<string> targets,
                                       Action<string, string, Instruction> onInstruction)
        {
            int scanned = 0;
            using (ModuleDefinition mod = ModuleDefinition.ReadModule(dll))
            {
                foreach (TypeDefinition t in mod.GetTypes())
                {
                    if (!targets.Contains(t.FullName)) continue;
                    foreach (MethodDefinition m in t.Methods)
                    {
                        if (!m.HasBody) continue;
                        scanned++;
                        foreach (Instruction ins in m.Body.Instructions)
                            onInstruction(t.FullName, m.Name, ins);
                    }
                }
            }
            return scanned;
        }

        /// <summary>指令操作数解析回「声明类型的全名」;非类型/成员操作数 ⇒ null。</summary>
        private static string ReferencedTypeFullName(object operand)
        {
            // ⚠️ 次序不可调:`MemberReference` 派生自 `TypeReference`,
            //    两者只留一个分支即可(分别取「声明类型」与「自身类型」两种语义)。
            if (operand is MemberReference mr) return mr.DeclaringType?.FullName;
            if (operand is TypeReference tr) return tr.FullName;
            if (operand is CallSite cs) return cs.ReturnType?.FullName;
            return null;
        }

        /// <summary>工程根(仓根)= <c>Application.dataPath</c> 的上两级。</summary>
        private static string RepoRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>剥除 <c>//</c> 与 <c>/* */</c> 注释(字符串字面量内容保留)。</summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, esc = false;

            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>字符偏移 → 行号(1 起;诊断用)。</summary>
        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }

        /// <summary>是否浮点相关 IL 指令(与 `InventoryDtoFloatGate` 的判据面同源)。</summary>
        private static bool IsFloatOpcode(OpCode op)
            => op.Code == Code.Conv_R4 || op.Code == Code.Conv_R8 || op.Code == Code.Conv_R_Un
            || op.Code == Code.Ldc_R4 || op.Code == Code.Ldc_R8
            || op.Code == Code.Ldind_R4 || op.Code == Code.Ldind_R8
            || op.Code == Code.Stind_R4 || op.Code == Code.Stind_R8;
    }

    // ══════════════════════════════════════════════════════════════════════
    // 负控夹具 —— 刻意**放在生产树之外**,只为证明两条扫描谓词会红。
    // 它们的「坏」是本测试件的正当内容,不是待修缺陷。
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>AC-18-13 正控:住 ScanMethods 靶名下的墙钟调用 —— 扫描器必须扫出来。</summary>
    internal static class FurnaceWallClockNegativeFixture
    {
        internal static long WallClockTick()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _ = System.DateTime.UtcNow;
            return sw.ElapsedTicks;
        }
    }

    /// <summary>AC-18-13 浮点面正控(2026-10-26 评审 W6):真声明浮点并做浮点运算 ——
    /// IL 里必须出现 `ldc.r8` / `conv.r8` / `mul` 一类的浮点指令。
    /// <para>⚠️ 它同时覆盖「只扫墙钟符号」的盲区:`Math.Sqrt` / `Math.Pow` 的 IL 是
    /// `call double System.Math::*`,**不产生** `conv.r4` / `ldc.r4`,且 `System.Math`
    /// 不在墙钟表内 ⇒ 光靠墙钟符号表抓不到浮点运算。本类型让浮点指令面有自己的正控。</para>
    /// </summary>
    internal static class FurnaceFloatNegativeFixture
    {
        internal static double FloatMath(double x, int n)
        {
            double acc = x;
            for (int i = 0; i < n; i++)
                acc = acc * 1.5 + System.Math.Sqrt(acc);   // 浮点常量 + 库返回 → 必有 r8 指令
            return acc;
        }

        internal static float[] FloatArray()
        {
            float[] buf = new float[2];                     // `float[]` —— 旧扫描的 `"float "` 漏扫形态
            buf[0] = 0.5f;
            return buf;
        }
    }

    /// <summary>AC-18-17 ① 负控:标了 `[Serializable]` —— 反射谓词必须识别。</summary>
    [Serializable]
    internal sealed class FurnaceSerializableNegativeFixture
    {
        public long StartTick;
        public long DurationTicks;
    }
}
