// S13 · story-003 —— F-53.2 延迟发出与 ConsequenceResolved 世界流测试。
//
// 被测对象 = **生产代码**
//   `Sim/Consequences/ConsequenceEmitter.cs`(发射器)· `Sim/Consequences/DelayTable53.cs`(整数域表)
//   `Editor.Tools.Bake/DelayTableBinder.cs` + `DelayTableCookedWriter.cs` + `DelayTable53Bake.cs`(烘焙三件套)
//   `Editor.Tools.Gates/WriterExistenceGate.cs`(AC-7 门存在性)
//   —— **不是本文件的副本**(本文件只含夹具与断言)。
//
// 卡面:`production/epics/medical-consequences/story-003-delayed-emit-and-world-stream.md`
//   (§Test Evidence 点名本路径;QA AC-1..AC-5 + 补录 AC-6/AC-7 + 证伪轮五形态)
// 权威来源:
//   GDD design/gdd/medical-consequences.md F-53.2 `:268-294`(定义域恰 = 回响轴 · DelayTicks>0 硬约束)
//     · 规则六(不可归因:零 case_id,patient_id 允许)/ 规则八(P0 零订阅者仍照发)
//     · Edge Cases `:322`(延迟期内存档 ⇒ 重放按 EmitTick 重建,零独立状态)
//     · Edge Cases `:323`(延迟期内再次结案 ⇒ 各自发、**不合并、不覆盖**)
//     · `:259` / `:317` NoEcho 无痕 ⇒ 不回响(合法不发路径,非错误)
//   AC-53-06 / AC-53-07 / AC-53-14 · ADR-009 §一/§六 · ADR-008 §二 全序键 · ADR-024 §① · ADR-029 §③
//   ADR-012(零回绕 · 两次烘焙逐位一致)
//
// ⚠️ **数值口径(卡面 AC-6 逐字)**:延迟三值 600/1200/2400 **测试读表不抄值**(用户仍可调)
//   ⇒ 本文件不出现 600/1200/2400 字面量;断言一律用「表值本身」作被除/被减数。
//   `band_count > 1` 的 U 判别力**留槽归数值轮** ⇒ 本 story **不启动、不声称为其提供证明**(禁借绿)。
//
// ⚠️ **证伪轮五形态**(S12-4 BLOCKING)与「旧实现下该夹具会怎样」逐条注在各测注释内
//   (sprint-12 复盘 Process Improvement 1:F2/F15b 假绿的成因 = 只问新实现是否通过)。
//
// ⚠️ 纯数据层:不触引擎运行时、不启 Unity、不写文件(唯一读文件 = 烘焙经 `BakeFromRepo` 读
//   `assets/data/53_delay_table.json`,与 medcons_outcome_judge_test 同纪律)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>53 Story 003 —— 延迟发出(F-53.2)+ ConsequenceResolved 世界流。</summary>
    [TestFixture]
    public sealed class MedconsDelayedEmitTest
    {
        // ════════════════════════════════════════════════════════════════════
        // 夹具基建(与 medcons_aggregation_test / medcons_outcome_judge_test 同款)
        // ════════════════════════════════════════════════════════════════════

        private static readonly InMemoryBlobPool SharedPool = new InMemoryBlobPool();
        private static readonly PayloadEncoder Encoder = new PayloadEncoder(SharedPool);
        private static readonly PayloadDecoder Decoder = new PayloadDecoder(SharedPool);

        private static string RepoRoot()
            => MedConsRepoPaths.RepoRoot;

        /// <summary>延迟表(烘焙机产物;测试直接驱动 Binder —— 反空转)。</summary>
        private static DelayTable53.Table LoadDelayTable()
            => DelayTable53Bake.BakeTableFromRepo(RepoRoot());

        /// <summary>头层事件构造(**零载荷** —— 只用于不被解码消费的 Kind)。</summary>
        private static SimEvent Ev(long tick, int patient, long seq, EventKind kind)
            => new SimEvent(tick, new PatientId(patient), seq, kind, default(PayloadRef));

        /// <summary>世界级事件(Patient = -1,承 ADR-007 §四;52 的形态)。</summary>
        private static SimEvent Sentinel(long tick, long seq, EventKind kind)
            => Ev(tick, PatientId.None.Value, seq, kind);

        /// <summary>一条「合格结案」(A 案入参:CaseClosed + Outcome 枚举序数)。</summary>
        private static (SimEvent CaseClosed, int Outcome) Eligible(long closeTick, int patient,
            long seq, Outcome53.Outcome outcome)
            => (Ev(closeTick, patient, seq, EventKind.CaseClosed), (int)outcome);

        // ── 依赖桩(全部具名,零反射魔法)──────────────────────────────────

        /// <summary>主机侧写权桩(P0 IsHost = 可切;Roll / IsAuthority 与本 story 无关)。</summary>
        private sealed class StubAuthority : IEventAuthority
        {
            public bool Host { get; set; } = true;
            public bool IsAuthority => Host;
            public bool IsHost => Host;
            public EventRollResult Roll(in RollRequest r)
                => throw new NotSupportedException("story-003 不掷骰(F-53.2 无随机面)");
        }

        /// <summary>在场桩:恒在场(离本 story 的 CAP 分支,常规夹具用)。</summary>
        private sealed class AlwaysPresent : IPresenceQuery
        {
            public int Count { get; set; } = 1;
            public bool IsPresent(PatientId p) => true;
            public int PresentCount => Count;
            public bool IsPresentAt(WorldPos cell) => true;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }

        /// <summary>在场桩(**两段式**):<c>Arm=false</c> 时病人在场、在场数未封顶
        /// (放行前置 CaseClosed 入流);<c>Arm=true</c> 后目标病人转离屏且在场数打满 24。
        /// <para>🔁 <b>2026-10-29 改注</b>:旧注写「后果行入流被 AC-15 拒收、异常自 Emit
        /// 向上传播」—— 那是 `TD-medcons-14` 收窄**之前**的实况。现 CAP 只管出现类事件,
        /// 本桩的 <c>Arm=true</c> 用于证明「满 CAP + 离屏」**不再**拦住后果
        /// (见 `test_tdMedcons14_offscreenPatientDueConsequence_emitsDespiteFullCap`)。</para></summary>
        private sealed class CapFullAndAbsent : IPresenceQuery
        {
            public bool Arm { get; set; }
            public bool IsPresent(PatientId p) => !Arm;
            public int PresentCount => Arm ? EventStream.PATIENT_APPEARANCE_CAP : 1;
            public bool IsPresentAt(WorldPos cell) => !Arm;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }

        private sealed class StubIdAuthority : IIdAuthority
        {
            public PatientId NextPatientId() => new PatientId(1000);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(1000);
        }

        /// <summary>病程投影桩(AC-6 夹具用:<c>Tick &gt; 0</c> ⇒ BuildEligible 走
        /// D=NotRecovered 路;承卡面 Note 3「以接口占位 + 夹具实现」)。</summary>
        private sealed class StubProjection : IDiseaseProjection
        {
            public long Tick { get; set; }
            public bool TryProject(PatientId patient, long tick, out DiseaseProjectionSnapshot snapshot)
            {
                snapshot = new DiseaseProjectionSnapshot(Tick);
                return Tick > 0;
            }
        }

        /// <summary>真 EventStream + 真发射器(反空转:写侧走生产通道全链)。</summary>
        private static EventStream NewStream(IPresenceQuery presence = null)
            => new EventStream(new StubIdAuthority(), presence ?? new AlwaysPresent());

        private static ConsequenceEmitter NewEmitter(
            EventStream stream, DelayTable53.Table table = null, StubAuthority authority = null)
            => new ConsequenceEmitter(
                stream, authority ?? new StubAuthority(),
                Encoder, table ?? LoadDelayTable());

        /// <summary>后果行指纹(逐位可比;定序/合并/重发判据的取证面)。</summary>
        private static string ConsequenceFingerprint(IReadOnlyList<SimEvent> stream)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < stream.Count; i++)
            {
                SimEvent e = stream[i];
                if (e.Kind != EventKind.ConsequenceResolved) continue;
                var p = Decoder.Decode<ConsequenceResolvedPayload>(
                    EventKind.ConsequenceResolved, e.Payload);
                sb.Append($"(tick={e.Tick},head={e.Patient.Value},seq={e.Seq}," +
                          $"outcome={p.Outcome},payloadPid={p.PatientId})");
            }
            return sb.ToString();
        }

        /// <summary>流中后果条数。</summary>
        private static int ConsequenceCount(IReadOnlyList<SimEvent> stream)
            => stream.Count(e => e.Kind == EventKind.ConsequenceResolved);

        /// <summary>显式 little-endian u32 读(与写方 WriteU32Le / CookedCodec 同形,不用平台序)。</summary>
        private static uint ReadU32Le(byte[] buffer, int offset)
            => (uint)(buffer[offset]
                      | (buffer[offset + 1] << 8)
                      | (buffer[offset + 2] << 16)
                      | (buffer[offset + 3] << 24));

        /// <summary>FNV-1a u32(测试内自足,不用 UnityEngine.Hash128,承 ADR-010 纪律)。</summary>
        private static uint Fnv1a(byte[] bytes)
        {
            uint h = 2166136261;
            foreach (byte b in bytes) { h ^= b; h *= 16777619; }
            return h;
        }

        /// <summary>最小合法延迟表 JSON(三值全在;负例在此串上做定点替换)。</summary>
        private static string MinimalDelayJson(long residual = 1, long recurrence = 2,
                                               long trialHistory = 3)
            => "{\"schema_version\": 1, \"rows\": [" +
               "{\"outcome\": \"residual\", \"delay_ticks\": " + residual + "}," +
               "{\"outcome\": \"recurrence\", \"delay_ticks\": " + recurrence + "}," +
               "{\"outcome\": \"trial_history\", \"delay_ticks\": " + trialHistory + "}]}";

        // ════════════════════════════════════════════════════════════════════
        // AC-1 —— EmitTick 纯整数 + 重放一致(AC-53-06)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-1:`EmitTick = Tick(CaseClosed) + DelayTable[outcome]` **纯整数**,且
        /// 对定义域内每一条满足 `EmitTick > Tick(CaseClosed)`(AC-53-06 的双半)。
        /// <para>⚠️ 卡面 AC-6 数值口径:**测试读表不抄值** —— 延迟值从烘焙表取,不写 600/1200/2400。</para>
        /// </summary>
        [Test]
        public void test_ac5306_emitTick_isPureIntegerAndStrictlyAfterClose()
        {
            var table = LoadDelayTable();
            foreach (int outcome in DelayTable53.DomainOutcomes)
            {
                Assert.IsTrue(table.TryDelayTicks(outcome, out long delay),
                    $"定义域内 outcome {outcome} 须可查(F-53.2 `:285-291`)");
                Assert.Greater(delay, 0L, $"AC-53-06:{(Outcome53.Outcome)outcome} 的 DelayTicks 须 > 0");

                const long closeTick = 10_000L;   // 边界值夹具:整数值本身即断言点(允许)
                long emitTick = DelayTable53.EmitTick(closeTick, delay);
                Assert.Greater(emitTick, closeTick,
                    "AC-53-06 承 AC-37-25 ①:EmitTick 须**严格** > 结案 tick(= 同 tick ⇒ 可被归因)");
                Assert.AreEqual(closeTick + delay, emitTick, "EmitTick = 结案 tick + 延迟(F-53.2 逐式)");
            }
        }

        /// <summary>
        /// AC-1:`在线跑到 EmitTick` vs `从流 CatchUp 补算` ⇒ 发出 tick 与载荷**逐位一致**,漏发 = 0。
        /// <para>证伪轮 **④ 一次性/幂等** 的正半 + **① 定序** 的参照值。
        /// <para>旧实现下会怎样:若「待发表」被缓存进字段(卡面 Forbidden),
        /// 从**零字段的新实例**重放同一前缀会得**空待发集** ⇒ 本夹具红(两跑指纹不等)。</para></para>
        /// </summary>
        [Test]
        public void test_ac5306_replayFromStreamPrefix_isBitIdenticalToOnline()
        {
            var table = LoadDelayTable();
            long delay = ResidualDelay(table);
            var eligible = new List<(SimEvent CaseClosed, int Outcome)>
            {
                Eligible(100, 1, 0, Outcome53.Outcome.Residual),
                Eligible(200, 2, 0, Outcome53.Outcome.Residual),
            };

            // 在线:逐 tick 推进到第二个 EmitTick。
            var onlineStream = NewStream();
            var online = new ConsequenceEmitter(onlineStream, new StubAuthority(), Encoder, table);
            foreach (var e in eligible) onlineStream.Append(e.CaseClosed);
            for (long tick = 1; tick <= 200 + delay; tick++)
                online.Emit(onlineStream.Events, eligible, tick);

            // 补算:同一**前缀**(只有两条 CaseClosed)+ 新实例 + 一次性推到同 nowTick。
            var replayStream = NewStream();
            var replay = new ConsequenceEmitter(replayStream, new StubAuthority(), Encoder, table);
            foreach (var e in eligible) replayStream.Append(e.CaseClosed);
            replay.Emit(replayStream.Events, eligible, 200 + delay);

            Assert.That(ConsequenceFingerprint(onlineStream.Events),
                Is.EqualTo(ConsequenceFingerprint(replayStream.Events)),
                "AC-53-06:任意前缀重放 ⇒ 发出集(含载荷)与在线态逐位一致");
            Assert.AreEqual(2, ConsequenceCount(onlineStream.Events),
                "漏发 = 0(两条各一条后果)");
        }

        /// <summary>
        /// AC-1 Edge:**Delay 恰跨存档点** ⇒ 读档后补算**照发且不重发**(GDD `:322`)。
        /// <para>证伪轮 **④** 的主载体。旧实现下会怎样:幂等若读**内存标志**,
        /// 新实例(读档)的内存标志为空 ⇒ 同一条后果发**第二次** ⇒ 计数断言红。</para>
        /// </summary>
        [Test]
        public void test_ac5306_saveInsideDelayWindow_replayEmitsOnce_notTwice()
        {
            var table = LoadDelayTable();
            long delay = ResidualDelay(table);
            long closeTick = 100;
            long emitTick = closeTick + delay;

            // 存档点落在延迟窗内(结案已入流,后果未发)。
            var before = NewStream();
            var em1 = new ConsequenceEmitter(before, new StubAuthority(), Encoder, table);
            before.Append(Ev(closeTick, 7, 0, EventKind.CaseClosed));
            Assert.AreEqual(ConsequenceEmitResult.NothingPending,
                em1.Emit(before.Events, new[] { Eligible(closeTick, 7, 0, Outcome53.Outcome.Residual) },
                         closeTick + delay / 2),
                "窗内不发出(F-53.2 延迟语义)");
            Assert.AreEqual(0, ConsequenceCount(before.Events), "读档点后果仍零条");

            // 读档 = 真 EventStream **从零** 重建 + 同一入流(前缀含 CaseClosed,不含后果)。
            var after = NewStream();
            var em2 = new ConsequenceEmitter(after, new StubAuthority(), Encoder, table);
            foreach (SimEvent e in before.Events) after.Append(e);
            var eligible = new[] { Eligible(closeTick, 7, 0, Outcome53.Outcome.Residual) };
            em2.Emit(after.Events, eligible, emitTick);
            em2.Emit(after.Events, eligible, emitTick + 1);   // 下一 tick 再推一次
            em2.Emit(after.Events, eligible, emitTick + 100);

            Assert.AreEqual(1, ConsequenceCount(after.Events),
                "补算照发**且只发一次**(幂等判据读流,非内存标志)");
            SimEvent row = after.Events.First(e => e.Kind == EventKind.ConsequenceResolved);
            Assert.AreEqual(emitTick, row.Tick, "发出 tick = EmitTick(非 nowTick —— D-5)");
        }

        /// <summary>
        /// **AC-53-06 反射半**:发射器类型**零跨 tick 可变字段**(实例字段全 <c>readonly</c>,
        /// 无 static 可变字段)。卡面 Forbidden「缓存待发表进字段」的机制面守卫。
        /// </summary>
        [Test]
        public void test_ac5306_emitterType_hasZeroCrossTickMutableFields()
        {
            Type t = typeof(ConsequenceEmitter);

            var mutable = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => !f.IsInitOnly)
                .Select(f => f.Name).ToList();
            Assert.That(mutable, Is.Empty,
                "AC-53-06:53 不持任何跨 tick 可变态(实例字段须全 readonly —— 待发表是流的纯函数," +
                "每次现算);命中:" + string.Join(",", mutable));

            var statics = t.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => !f.IsLiteral && !f.IsInitOnly)
                .Select(f => f.Name).ToList();
            Assert.That(statics, Is.Empty, "零 static 可变字段(第二私有态通道)");

            // 判别力自检(D-4 口径的边界):**readonly 注入协作方是合法的**,故守卫须能点名
            // 「非 readonly」形 —— 对照类型即证。
            var shadow = typeof(ShadowEmitterWithPendingCache)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => !f.IsInitOnly).Select(f => f.Name).ToList();
            Assert.That(shadow, Is.Not.Empty,
                "判别力自检:守卫对「缓存待发表进字段」的对照类型必须命中,否则本测恒真(假绿)");
        }

        /// <summary>负夹具:把待发表缓存进字段(卡面 Forbidden 的形态)。</summary>
        private sealed class ShadowEmitterWithPendingCache
        {
#pragma warning disable CS0649
            private List<int> _pending;   // 违例:跨 tick 可变态承载延后
#pragma warning restore CS0649
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-2 —— DelayTable 构建期三条拒收(定义域收窄)
        // 证伪轮 **③ 定义域收窄**:三形态各一夹具;放宽任一拒收 ⇒ 对应夹具红。
        // ════════════════════════════════════════════════════════════════════

        /// <summary>正路径冒烟:现网源须通过全部构建期校验。</summary>
        [Test]
        public void test_ac2_bake_sourceJson_bindsAndPassesAllValidations()
            => Assert.DoesNotThrow(() => DelayTable53Bake.BakeFromRepo(RepoRoot()),
                "合法源 JSON 须通过三条构建期拒收");

        [Test]
        public void test_ac2_bake_delayZero_hardFails()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => DelayTable53Bake.BindForTest(MinimalDelayJson(residual: 0)),
                "AC-2:定义域内某值 = 0 ⇒ 烘焙 throw(AC-53-06;=0 会让后果与结案同 tick ⇒ 可被归因)");
            Assert.That(string.Join(";", ex.Errors), Does.Contain("delay_ticks=0"),
                "错误消息须点名违规值:" + string.Join("; ", ex.Errors));
        }

        [Test]
        public void test_ac2_bake_delayNegative_hardFails()
            => Assert.Throws<BakeValidationException>(
                () => DelayTable53Bake.BindForTest(MinimalDelayJson(residual: -5)),
                "AC-2:负值 ⇒ 烘焙 throw");

        [Test]
        public void test_ac2_bake_delayOne_isLegalBoundaryMin()
        {
            // 合规边界 min = 1 绿(卡面 AC-2 逐字)。
            var bound = DelayTable53Bake.BindForTest(MinimalDelayJson(residual: 1));
            Assert.AreEqual(1L, bound.Rows[Outcome53.Outcome.Residual], "min=1 放行");
        }

        [Test]
        public void test_ac2_bake_missingDomainRow_hardFails()
        {
            // 整行删除(行级零错误 ⇒ 直达覆盖校验分支;承 story-002 F12 的教训:
            // 「改 key 名」会在行解析层先报 ⇒ 覆盖校验本体从不执行)。
            string json = MinimalDelayJson()
                .Replace(",{\"outcome\": \"recurrence\", \"delay_ticks\": 2}", "");
            Assert.IsFalse(json.Contains("recurrence"), "前置:该行已整行删除");

            var ex = Assert.Throws<BakeValidationException>(
                () => DelayTable53Bake.BindForTest(json), "AC-2 Edge:表缺 outcome 行 = 也红");
            Assert.That(string.Join(";", ex.Errors), Does.Contain("缺 outcome 行").And.Contains("recurrence"),
                "错误消息须点名覆盖校验 + 缺失行:" + string.Join("; ", ex.Errors));
        }

        [Test]
        public void test_ac2_bake_lifeAxisAsKey_hardFails_schemaError()
        {
            // 卡面 AC-2 Edge 逐字:「结局轴两值不出现在夹具全集里(它们不是 key ——
            // 若出现 = schema 错,同样 throw)」。定义域用**白名单单机制** ⇒ 同一条规则。
            string json = MinimalDelayJson()
                .Replace("{\"outcome\": \"recurrence\", \"delay_ticks\": 2}",
                         "{\"outcome\": \"patient_loses\", \"delay_ticks\": 2}");
            var ex = Assert.Throws<BakeValidationException>(
                () => DelayTable53Bake.BindForTest(json), "结局轴作 key ⇒ schema 错(F-53.2 `:285-291`)");
            Assert.That(string.Join(";", ex.Errors), Does.Contain("patient_loses").And.Contains("定义域"),
                "错误消息须点名该 key 与定义域:" + string.Join("; ", ex.Errors));
        }

        [Test]
        public void test_ac2_bake_noEchoAsKey_hardFails_sameSingleMechanism()
        {
            // NoEcho 与结局轴走**同一条**白名单规则(用户裁定 2026-10-16:不另设分支)。
            string json = MinimalDelayJson()
                .Replace("{\"outcome\": \"recurrence\", \"delay_ticks\": 2}",
                         "{\"outcome\": \"no_echo\", \"delay_ticks\": 900}");
            Assert.Throws<BakeValidationException>(
                () => DelayTable53Bake.BindForTest(json),
                "NoEcho 作 key ⇒ 同一条 schema 错(GDD `:259`/`:317`:无痕不回响 ⇒ 不在定义域)");
        }

        [Test]
        public void test_ac2_bake_floatLiteralAndUnknownKey_hardFail()
        {
            Assert.Throws<BakeValidationException>(
                () => DelayTable53Bake.BindForTest(MinimalDelayJson().Replace(
                    "\"delay_ticks\": 1", "\"delay_ticks\": 1.5")),
                "浮点字面量 ⇒ 硬失败(ADR-006 边界;词法层接受 Float,拒收在类型检查 —— 承 N-9 口径)");

            Assert.Throws<BakeValidationException>(
                () => DelayTable53Bake.BindForTest(MinimalDelayJson().Replace(
                    "{\"outcome\": \"residual\"", "{\"lookback_ticks\": 900, \"outcome\": \"residual\"")),
                "未知键 ⇒ 硬失败(ADR-014 §三;lookback 是**旋钮非作者态字段**,卡面 Note 6 归数值轮)");
        }

        /// <summary>Sim 侧纵深防御(与 <see cref="Outcome53.Table"/> T-2 同口径):绕过 Bake 直构表亦拒。</summary>
        [Test]
        public void test_ac2_simTableRejectsDomainViolation_depthDefense()
        {
            Assert.Throws<ArgumentException>(
                () => DelayTable53.CreateTable(new Dictionary<int, long>
                {
                    { (int)Outcome53.Outcome.Residual, 0 },
                    { (int)Outcome53.Outcome.Recurrence, 2 },
                    { (int)Outcome53.Outcome.TrialHistory, 3 },
                }), "值 0 ⇒ 构造拒收");

            Assert.Throws<ArgumentException>(
                () => DelayTable53.CreateTable(new Dictionary<int, long>
                {
                    { (int)Outcome53.Outcome.PatientLoses, 9 },
                    { (int)Outcome53.Outcome.Recurrence, 2 },
                    { (int)Outcome53.Outcome.TrialHistory, 3 },
                }), "域外 key ⇒ 构造拒收");

            Assert.Throws<ArgumentException>(
                () => DelayTable53.CreateTable(new Dictionary<int, long>
                {
                    { (int)Outcome53.Outcome.Residual, 1 },
                }), "缺行 ⇒ 构造拒收(缺行 = 该回响静默不发)");

            Assert.DoesNotThrow(
                () => DelayTable53.CreateTable(new Dictionary<int, long>
                {
                    { (int)Outcome53.Outcome.Residual, 1 },
                    { (int)Outcome53.Outcome.Recurrence, 2 },
                    { (int)Outcome53.Outcome.TrialHistory, 3 },
                }), "合法域内三条 ⇒ 放行");
        }

        // ── 烘焙面确定性 + 头槽(story-002 R-1 的教训照守)──────────────────

        [Test]
        public void test_ac2_bake_cookedHeader_schemaVersionEchoesBound_notZero()
        {
            var output = DelayTable53Bake.BakeFromRepo(RepoRoot());
            Assert.AreEqual(CookedFormat.SchemaVersion, ReadU32Le(output.Cooked, 8),
                "头 offset 8 须为 schemaVersion 槽(story-002 的 BLOCKING R-1:写 0 = 启动期硬失败)");
            Assert.AreEqual(CookedFormat.FormatVersion, ReadU32Le(output.Cooked, 4));
            Assert.AreEqual(output.ConfigVersion, ReadU32Le(output.Cooked, 12));

            // 载荷布局 = row_count(i32) + row_count × {outcome(i32), delay_ticks(i64)}。
            int rowCount = output.Bound.Rows.Count;
            int expectedPayload = 4 + rowCount * 12;
            Assert.AreEqual(expectedPayload, output.Cooked.Length - CookedFormat.HeaderSize,
                "载荷长度按布局推导(可判别:多塞 schema_version 会 +4 ⇒ 本断言红)");
            Assert.AreEqual((uint)expectedPayload, ReadU32Le(output.Cooked, 16), "payloadLength 槽");
        }

        // 🔁 **2026-10-27 删除(S20-7 · `TD-medcons-16` 结案)**:此处原有
        //   `test_ac2_bake_cookedBytes_deterministic_sameSourceTwice` ——
        //   `BakeFromRepo()` 调两次比对 FNV 哈希。**它对行序回归零判别力**:
        //   两次调用走的是同一次 `Dictionary` 枚举,必得同序 ⇒ 停用行序排序它仍绿
        //   (S11-9 同型假绿)。其 doc 自称「已由下方夹具替换」,但**删除动作未落**,
        //   于是计数里长期含一条假绿。
        //   **本条已删,判据由下方 `…_rowOrderIndependentOfInputOrder` 独占**。
        //   ⚠️ 删除理由留档而非静默移除 —— 「这条为什么不能靠跑两次同源来证」本身是判据知识。

        /// <summary>
        /// ADR-014 §二 的**真判别**夹具(替换原「同函数同输入两次对拍」的假绿形制):
        /// 两份 **rows 顺序打乱、语义相同** 的 JSON ⇒ cooked 字节逐位相同。
        /// <para>⚠️ 原夹具(a/b 同输入两次调用)**停用行序排序仍绿** —— 两次调用走同一次
        /// Dictionary 枚举、必得同序,对行序回归零判别力(S11-9 同型)。本夹具才证
        /// 「输出行序与**输入**序无关」:若写方依赖 `Dictionary` 迭代序或输入序,
        /// 两份输入将产出不同字节 ⇒ 本测转红。</para>
        /// </summary>
        [Test]
        public void test_ac2_bake_cookedBytes_rowOrderIndependentOfInputOrder()
        {
            // 同一集合、两种输入序(第三支挪到首位;数值互异使「顺序」在字节里可观测)。
            string ascending = "{\"schema_version\": 1, \"rows\": [" +
                "{\"outcome\": \"residual\", \"delay_ticks\": 600}," +
                "{\"outcome\": \"recurrence\", \"delay_ticks\": 1200}," +
                "{\"outcome\": \"trial_history\", \"delay_ticks\": 2400}]}";
            string shuffled = "{\"schema_version\": 1, \"rows\": [" +
                "{\"outcome\": \"trial_history\", \"delay_ticks\": 2400}," +
                "{\"outcome\": \"residual\", \"delay_ticks\": 600}," +
                "{\"outcome\": \"recurrence\", \"delay_ticks\": 1200}]}";

            byte[] cookedA = DelayTable53Bake.BakeFromJson(ascending).Cooked;
            byte[] cookedB = DelayTable53Bake.BakeFromJson(shuffled).Cooked;

            // ⚠️ 只比**载荷区**:头 offset 12 的 configVersion = **源文本内容哈希派生**
            // (ADR-014 §五)⇒ 两份文本不同的输入**合法地**得不同 configVersion,
            // 头区因此必然不同,不属本夹具的判据面(载荷才是行序的载体)。
            byte[] payloadA = PayloadOf(cookedA);
            byte[] payloadB = PayloadOf(cookedB);

            Assert.AreEqual(Fnv1a(payloadA), Fnv1a(payloadB),
                "载荷行序须由**输出侧规则**(outcome 序数升序)决定,与输入 rows 顺序无关 —— " +
                "停用写方的显式排序(改依赖 Dictionary 迭代序 / 输入序)⇒ 本断言必红");
            Assert.AreEqual(payloadA.Length, payloadB.Length, "两份载荷长度相同(同一集合)");
        }

        /// <summary>取 cooked 的载荷区(跳 20 字节头)。</summary>
        private static byte[] PayloadOf(byte[] cooked)
        {
            var payload = new byte[cooked.Length - CookedFormat.HeaderSize];
            Array.Copy(cooked, CookedFormat.HeaderSize, payload, 0, payload.Length);
            return payload;
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-3 / AC-53-07 —— 反渗扫描(零 case_id · 零病例指针 · 零 salted_key)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>反渗禁名词族(去下划线 + 小写后的子串匹配)。</summary>
        private static readonly string[] BackflowFamily =
        {
            "caseid", "caseref", "caseptr", "casepointer", "anchorcase",
            "member_set", "memberset", "saltedkey", "salt", "casewriter", "caseclosed",
        };

        /// <summary>递归收集类型可及的名字(类型名 + 字段名 + 字段类型递归)。</summary>
        private static List<string> CollectReachableNames(Type t, HashSet<Type> seen, int depth = 0)
        {
            var names = new List<string>();
            if (t == null || depth > 5 || t.IsPrimitive || t == typeof(string)) return names;
            if (!seen.Add(t)) return names;

            names.Add(t.Name);
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.Instance | BindingFlags.Static))
            {
                names.Add(f.Name);
                names.AddRange(CollectReachableNames(f.FieldType, seen, depth + 1));
            }
            foreach (Type nested in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                names.AddRange(CollectReachableNames(nested, seen, depth + 1));
            return names;
        }

        private static List<string> BackflowHits(IEnumerable<Type> types)
        {
            var seen = new HashSet<Type>();
            var hits = new List<string>();
            foreach (Type t in types)
                foreach (string n in CollectReachableNames(t, seen))
                {
                    string flat = n.Replace("_", string.Empty).ToLowerInvariant();
                    foreach (string b in BackflowFamily)
                        if (flat.Contains(b)) hits.Add($"{t.Name}.{n}→{b}");
                }
            return hits;
        }

        /// <summary>
        /// AC-53-07(GDD 规则六):53 的**发出载荷 + 内部类型**递归反射 ⇒
        /// 零 <c>case_id</c> / 零病例指针 / 零 <c>salted_key</c>(AC-7 补全的词族)。
        /// <para><c>patient_id</c> **合法**(病人可指向,病例不可)。</para>
        /// </summary>
        [Test]
        public void test_ac5307_emittedPayloadAndInternalTypes_haveNoCasePointer_noSaltedKey()
        {
            var scanned = new[]
            {
                typeof(ConsequenceResolvedPayload),
                typeof(ConsequenceEmitter),
                typeof(DelayTable53),
                typeof(DelayTable53.Table),
            };

            Assert.That(BackflowHits(scanned), Is.Empty,
                "AC-53-07 反渗:后果的载荷与 53 内部类型零 case_id / 零病例指针 / 零 salted_key" +
                "(salted_key 是 F-53.1 的掷骰输入,回渗 = 可反推具体判断,破不可归因)");

            // 载荷面另证:构造器参数集**不含** case_id(Impl Note 4:物理不可传,非运行期置 null)。
            var ctorParams = typeof(ConsequenceResolvedPayload)
                .GetConstructors()[0].GetParameters().Select(p => p.Name).ToArray();
            Assert.That(ctorParams, Is.EquivalentTo(new[] { "outcome", "patientId" }),
                "载荷构造器参数集恰 = {outcome, patientId}(零 case_id 形参 = 编译面锁死)");
        }

        /// <summary>
        /// **判别力自检**(防反渗扫描恒真):同一扫描器对注入 <c>case_id</c> + <c>salted_key</c>
        /// 的对照类型必须点名,而对 <c>patient_id</c> **不得**误报(卡面 AC-7 Edge 逐字)。
        /// </summary>
        [Test]
        public void test_ac5307_guardHasDiscriminatingPower_flagsCaseAndSalt_notPatient()
        {
            List<string> hits = BackflowHits(new[] { typeof(ShadowPayloadWithCaseBackflow) });
            Assert.That(hits, Is.Not.Empty,
                "判别力自检:扫描器对注入的 case_id / salted_key 必须命中 —— 空结果 = 守卫恒真(假绿)");
            Assert.That(string.Join(",", hits), Does.Contain("case_id"), "须点名 case_id");
            Assert.That(string.Join(",", hits), Does.Contain("salted_key"),
                "须点名 salted_key(AC-7 补全的词族)");

            // 方向性:patient_id 合法 —— 同一扫描器不得把它报为违例。
            Assert.That(BackflowHits(new[] { typeof(ConsequenceResolvedPayload) }), Is.Empty,
                "patient_id **合法**(病人可指向)—— 负夹具不得把它误报为违例");
        }

        /// <summary>负夹具:回渗 case_id + salted_key 的对照载荷(仅供判别力自检)。</summary>
        private struct ShadowPayloadWithCaseBackflow
        {
            public readonly int patient_id;     // 合法(扫描不得点名)
            public readonly int case_id;        // 违例:AC-53-07 禁
            public readonly long salted_key;    // 违例:AC-7 补全词族
        }

        /// <summary>
        /// AC-3 Edge(卡面逐字):「同一病例重开(改写)」不产生第二条指向性事件。
        /// <para>机制:后果载荷无 case_id(上条已扫)+ 幂等差额按病人计 ⇒
        /// 未结案的**重开案**(无第二条 CaseClosed)永不出合格行;已发的亦不重复。</para>
        /// <para>证伪轮 **②** 的近邻形态。旧实现下会怎样:若幂等只在实例内存里记
        /// 「已发」,跨调用/跨重放会**漏发或重发**,本夹具的两条断言之一必红。</para>
        /// </summary>
        [Test]
        public void test_ac3_reopenAndRewrite_neverSecondPointerEvent()
        {
            var table = LoadDelayTable();
            long delay = ResidualDelay(table);
            var stream = NewStream();
            var emitter = new ConsequenceEmitter(stream, new StubAuthority(), Encoder, table);

            // 流构造须全程满足 BCD-码-3(非降):改写史 / 重开案先入流(tick 200/210 > 100),
            // 后果行(700)最后由 Emit 写入 —— 先前测试在 Emit 之后补入更早 tick 的事件 = 夹具
            // 自身乱序(2026-10-16 返工:F1 教训,生产无错)。
            stream.Append(Ev(100, 4, 0, EventKind.CaseClosed));
            stream.Append(Ev(200, 4, 1, EventKind.JudgmentRevised));
            stream.Append(Ev(210, 4, 2, EventKind.CaseOpened));
            var eligible = new[] { Eligible(100, 4, 0, Outcome53.Outcome.Residual) };
            emitter.Emit(stream.Events, eligible, 100 + delay);

            // 未结案的**重开案**(无第二条 CaseClosed)永不出合格行;后续 tick 再 Emit 亦不重发。
            emitter.Emit(stream.Events, eligible, 100 + delay + 50);

            Assert.AreEqual(1, ConsequenceCount(stream.Events),
                "每病例至多一条后果(Guardrail;重写/重开不产生第二条)");

            var p = Decoder.Decode<ConsequenceResolvedPayload>(
                EventKind.ConsequenceResolved,
                stream.Events.First(e => e.Kind == EventKind.ConsequenceResolved).Payload);
            Assert.AreEqual((int)Outcome53.Outcome.Residual, p.Outcome, "载荷 = 整数枚举");
            Assert.AreEqual(4, p.PatientId, "patient_id 允许(方向性:病人可指向)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-4 —— P0 零订阅者仍发出 + 跨流全序混流
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-4(规则八 水龙头纪律):**无任何消费者**时后果照写世界流;
        /// 无「没人听就不发」路径。路由真源 = kindgen 生成的 <see cref="StreamRouting"/>(ADR-024)。
        /// </summary>
        [Test]
        public void test_ac4_zeroSubscribers_stillAppendsAndRoutesToWorldStream()
        {
            var table = LoadDelayTable();
            long delay = RecurrenceDelay(table);
            // 零订阅环境:只有 sink + 流,无任何读方 / 无事件回调 / 无 UI。
            var stream = NewStream();
            var emitter = new ConsequenceEmitter(stream, new StubAuthority(), Encoder, table);
            stream.Append(Ev(50, 9, 0, EventKind.CaseClosed));

            emitter.Emit(stream.Events,
                new[] { Eligible(50, 9, 0, Outcome53.Outcome.Recurrence) }, 50 + delay);

            Assert.AreEqual(1, ConsequenceCount(stream.Events),
                "P0 零订阅者仍照发(世界流持久;呈现为空是 004 的事,不是不发)");
            SimEvent row = stream.Events.First(e => e.Kind == EventKind.ConsequenceResolved);
            Assert.AreEqual(StreamId.World, StreamRouting.Of(row.Kind),
                "经 registry 路由进**世界流**(AC-53-14:进流非派生缓存)");
            Assert.That(row.Seq, Is.GreaterThanOrEqualTo(0),
                "Seq 由主机 Append 发号(header 传 -1 哨兵,流给真号)");
        }

        /// <summary>
        /// AC-4(全序混流):53 的真 <c>patient_id</c> 与 52 的世界级 <c>Patient = -1</c>
        /// 同 tick 混流 ⇒ 全序键 <c>(Tick, StreamPriority, Patient, Seq)</c> **无并列歧义**。
        /// <para>证伪轮 **① 定序**。旧实现下会径:比较器若只比 (Tick, Seq) 而漏 StreamPriority
        /// / Patient,同 tick 混排的三事件会得**并列键** ⇒ 传递性/唯一性断言红。</para>
        /// </summary>
        [Test]
        public void test_ac4_worldSentinelMinusOneAndRealPatientSameTick_noTieAmbiguity()
        {
            var rows = new[]
            {
                Ev(300, 3, 0, EventKind.ConsequenceResolved),      // 53:真病人
                Sentinel(300, 0, EventKind.EventArrived),          // 52:世界级 -1
                Ev(300, 3, 1, EventKind.ConsequenceResolved),      // 53:同病人第二号
            };

            // 两两比较须**全序且无并列**(唯一键 ⇒ 排序算法稳定性无关)。
            for (int i = 0; i < rows.Length; i++)
                for (int j = 0; j < rows.Length; j++)
                {
                    int cmp = EventOrder.Compare(in rows[i], in rows[j]);
                    Assert.AreEqual(cmp == 0, i == j,
                        $"({i},{j}) 并列键只允许自反{i == j};实得 cmp={cmp} —— 混流全序有歧义");
                    if (i != j)
                        Assert.AreEqual(-cmp, EventOrder.Compare(in rows[j], in rows[i]),
                            $"({i},{j}) 反对称性破坏(全序比较器不可传递)");
                }

            // 病例流(CaseClosed)与世界流(ConsequenceResolved)同 tick:世界流**后置**(StreamPriority 2 > 1)。
            SimEvent caseRow = Ev(300, 3, 0, EventKind.CaseClosed);
            SimEvent worldRow = Ev(300, 3, 0, EventKind.ConsequenceResolved);
            Assert.That(EventOrder.Compare(in caseRow, in worldRow), Is.LessThan(0),
                "同 (Tick, Patient, Seq) 时 StreamPriority 破平:病例流先于世界流(ADR-008 §二)");
        }

        /// <summary>
        /// 证伪轮 **① 定序** 的写入面:多结案、延迟不同 ⇒ Append 序须为 **EmitTick 升序**
        /// (满足 <c>EventStream</c> 的 BCD-码-3「流按 Tick 非降」)。
        /// <para>停用升序 Sort ⇒ 本夹具红(真 EventStream 会对回退 tick 抛)。</para>
        /// </summary>
        [Test]
        public void test_ac4_pendingAppendsInAscendingEmitTickOrder()
        {
            var table = LoadDelayTable();
            long residual = ResidualDelay(table);
            long trial = TrialHistoryDelay(table);
            Assert.Greater(trial, residual, "前置:夹具依赖两档延迟互异(值可调 ⇒ 此处读表比较,不抄值)");

            var stream = NewStream();
            var emitter = new ConsequenceEmitter(stream, new StubAuthority(), Encoder, table);
            // 病人 1:延迟短但结案晚;病人 2: latency 长但结案早 ⇒ EmitTick 交错。
            var eligible = new List<(SimEvent CaseClosed, int Outcome)>
            {
                Eligible(100, 2, 0, Outcome53.Outcome.TrialHistory),
                Eligible(200, 1, 0, Outcome53.Outcome.Residual),
            };
            foreach (var e in eligible) stream.Append(e.CaseClosed);

            long now = Math.Max(100 + trial, 200 + residual);
            Assert.DoesNotThrow(() => emitter.Emit(stream.Events, eligible, now),
                "升序 Append 须满足 BCD-码-3(乱序 Append ⇒ 真流会 throw)");

            var ticks = stream.Events.Where(e => e.Kind == EventKind.ConsequenceResolved)
                                    .Select(e => e.Tick).ToList();
            Assert.AreEqual(2, ticks.Count, "两条各发一条");
            Assert.That(ticks, Is.Ordered.Ascending, "发出序 = EmitTick 升序");
        }

        /// <summary>
        /// 卡面 Edge Cases `:323`:延迟期内**再次结案** ⇒ 两个后果**各自**发、**不合并、不覆盖**。
        /// <para>证伪轮 **② 共存/互斥** 主载体。改成合并/覆盖 ⇒ 本夹具红(条数 2→1)。</para>
        /// </summary>
        [Test]
        public void test_edge_twoClosesWithinDelayWindow_eachEmits_notMerged()
        {
            var table = LoadDelayTable();
            long residual = ResidualDelay(table);
            long recurrence = RecurrenceDelay(table);
            Assert.AreNotEqual(residual, recurrence,
                "前置:两档延迟须互异(值可调;相等则本夹具失去判别力,须换档)");

            var stream = NewStream();
            var emitter = new ConsequenceEmitter(stream, new StubAuthority(), Encoder, table);
            var eligible = new[]
            {
                Eligible(100, 5, 0, Outcome53.Outcome.Residual),
                Eligible(150, 5, 0, Outcome53.Outcome.Recurrence),   // **同一病人**,延迟期内再结案
            };
            foreach (var e in eligible) stream.Append(e.CaseClosed);

            // ⚠️ **两阶段 Emit 是本夹具判别力的前提**(2026-10-16 证伪轮 ② 实测修):
            // 单次 Emit 时流中零后果 ⇒ `already = 0` ⇒ 「合并形态」变异(`already > 0 ⇒ skip`)
            // 与正确口径**行为相同** ⇒ 夹具假绿(实测:变异后 0 红)。先发第一条把 `already` 造出来,
            // 再喂全集 ⇒ 前缀口径补发第二条 vs 合并口径吞掉,两者才可分。
            emitter.Emit(stream.Events, new[] { eligible[0] }, 100 + residual);
            Assert.AreEqual(1, stream.Events.Count(e => e.Kind == EventKind.ConsequenceResolved),
                "阶段一:第一条已落地(为阶段二造出 already = 1)");

            emitter.Emit(stream.Events, eligible, 150 + Math.Max(residual, recurrence));

            var rows = stream.Events.Where(e => e.Kind == EventKind.ConsequenceResolved).ToList();
            Assert.AreEqual(2, rows.Count,
                "两个后果各自发(GDD `:323`「不合并、不覆盖」;幂等按**计数差**非 (EmitTick,Patient) 键)。" +
                "**停用升序前缀差额(改「每病人只发一条」)⇒ 本断言转红**(实测得 1)");
            Assert.AreEqual(100 + residual, rows[0].Tick);
            Assert.AreEqual(150 + recurrence, rows[1].Tick);

            // 内容级(非 ref 级)判据:两条载荷各自独立、不覆盖。
            var first = Decoder.Decode<ConsequenceResolvedPayload>(
                EventKind.ConsequenceResolved, rows[0].Payload);
            var second = Decoder.Decode<ConsequenceResolvedPayload>(
                EventKind.ConsequenceResolved, rows[1].Payload);
            Assert.AreEqual((int)Outcome53.Outcome.Residual, first.Outcome, "第一条载荷 outcome 未被覆盖");
            Assert.AreEqual((int)Outcome53.Outcome.Recurrence, second.Outcome, "第二条载荷 outcome 独立");
            Assert.AreEqual(5, first.PatientId);
            Assert.AreEqual(5, second.PatientId);
            Assert.AreNotEqual(first.Outcome, second.Outcome,
                "两条后果内容可分(合并/覆盖形态会让二者同值 ⇒ 本断言红)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-5 —— OQ-53-8 形状门的**枚举域部分**(用户裁定:本 story 不建代价表)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-5(W-4 ①):结局轴两值 + NoEcho 作 eligible 项 ⇒ <c>TryDelayTicks</c> false ⇒
        /// **不发射、不抛**(NoEcho 是合法不发路径;结局轴由 9 即时结算,53 不经本表发事件)。
        /// <para>「永不致死」的**代价表形状门**部分不在本 story(全仓无代价表载体)——
        /// 归 53/11 的 GDD + 数值轮待办(卡面 Out of Scope 与本 story AC-5 括注)。</para>
        /// </summary>
        [Test]
        public void test_ac5_lifeAxisAndNoEchoEligible_neverEmitted_andNoThrow()
        {
            var table = LoadDelayTable();
            foreach (var nonEchoDomain in new[]
                     {
                         Outcome53.Outcome.NoEcho,
                         Outcome53.Outcome.PatientLives,
                         Outcome53.Outcome.PatientLoses,
                     })
            {
                var stream = NewStream();
                var emitter = new ConsequenceEmitter(stream, new StubAuthority(), Encoder, table);
                stream.Append(Ev(100, 8, 0, EventKind.CaseClosed));

                Assert.IsFalse(table.TryDelayTicks((int)nonEchoDomain, out _),
                    $"{nonEchoDomain} ∉ 延迟表定义域(F-53.2 `:285-291` 定义域收窄)");

                ConsequenceEmitResult result = emitter.Emit(stream.Events,
                    new[] { Eligible(100, 8, 0, nonEchoDomain) }, long.MaxValue / 2, out int emitted);

                Assert.AreEqual(0, emitted, $"{nonEchoDomain} 不发任何后果");
                Assert.AreEqual(ConsequenceEmitResult.NothingPending, result,
                    "域外 = **合法不发**(非错误;结局轴由 9 即时结算)");
                Assert.AreEqual(0, ConsequenceCount(stream.Events));
            }
        }

        /// <summary>AC-5 补充:结局轴「即时性」由**不在定义域**保证,不靠一个 <c>= 0</c> 的特例行。</summary>
        [Test]
        public void test_ac5_lifeAxisImmediacy_byDomainExclusion_notZeroRow()
        {
            var table = LoadDelayTable();
            foreach (int outcome in DelayTable53.DomainOutcomes)
            {
                Assert.IsTrue(table.TryDelayTicks(outcome, out long d), "域内须可查");
                Assert.Greater(d, 0L, "域内须 > 0(AC-53-06 的全称量词只遍历回响轴 key 集)");
            }
            Assert.AreEqual(DelayTable53.DomainOutcomes.Count, table.RowCount,
                "表行数恰 = 定义域大小(无特例空行 —— 定义域收窄而非破约束)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-6 —— uBand 装配层单源(N-3 · 本 story 是 53 链第一个真实喂 uBand 的调用点)
        // 裁定:**零装载面改动** ⇒ helper 落在本文件的测试侧装配面(生产装配 helper
        //   属未授权路径 ⇒ 偏差已报)。
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 装配层 eligible 计算(F-53.1 → F-53.2 的接线面):U 档从 **cooked 表自带的
        /// <see cref="Outcome53.Table.UBandCount"/>** 派生,禁独立常量源。
        /// </summary>
        /// <remarks>⚠️ <b>夹具 seed/key 选值有唯一理由(T-M1)</b>:<see cref="Ac6WorldSeed"/> /
        /// <see cref="Ac6SaltedKey"/> 是**为使 uRaw 越出 span 而使 uBand = 1** 而选的
        /// (python 逐位复算 <c>SplitMix64(42, "case-salt", 7) &gt;&gt; 48 = 58704</c>;
        /// <c>BandOf(58704, 2) = 1</c>)。⚠️ 数值轮若改 <c>SplitMix64</c> 语义 / U 档位口径,
        /// 本组合会**静默退化**为 band = 0 ⇒ ② 段失去「越界前提」(③ 段硬传 <c>uBand:1</c> 仍兜住
        /// 守护本身)。改语义者须同批复算并更新本注释。</remarks>
        /// <param name="bandCountSource">档位源(测试据此注入「分歧的第二真源」)。</param>
        private static List<(SimEvent CaseClosed, int Outcome)> BuildEligible(
            Outcome53.Table judgeTable, IReadOnlyList<SimEvent> stream,
            IDiseaseProjection projection, IReadOnlyList<EventKind> careKinds,
            long worldSeed, int bandCountSource)
        {
            var groups = ConsequenceInputAggregator.Aggregate(stream, Decoder, projection, careKinds);
            var trial = TrialHistoryPredicate.HasTrialHistory(stream);
            var outRows = new List<(SimEvent CaseClosed, int Outcome)>();
            foreach (var g in groups)
            {
                var p = Outcome53.JudgmentLaneOf(g.JudgmentRecords);
                var c = Outcome53.CareLaneOf(g.CareRecords, matched: false);
                var d = g.DiseaseProjection.Tick > 0
                    ? Outcome53.RecoveryLane.NotRecovered : Outcome53.RecoveryLane.Recovered;
                ulong uRaw = OutcomeJudgeDerive.URaw(worldSeed, 7UL);
                // ⚠️ 单源判据:bandCountSource 由调用方给;生产须传 judgeTable.UBandCount。
                int uBand = Outcome53.BandOf(uRaw, bandCountSource);
                var outcome = Outcome53.Judge.Evaluate(
                    new Outcome53.JudgeInputs(p, c, d, trial, uBand, uRaw), judgeTable);
                outRows.Add((g.CaseClosed, (int)outcome));
            }
            return outRows;
        }

        /// <summary>
        /// AC-6:`bandCount` 用** cooked 表自带值** ⇒ 域内档位,判定与发出正常;
        /// 注入**分歧的第二真源**(表 1 档 / 源 2 档)⇒ uBand 越界 ⇒
        /// <see cref="Outcome53.Table.TryLookup"/> 域检查接住并回落 default 行
        /// (该夹具**必须转红**,否则 Sim/装配层守护失证)。
        /// <para>卡面 AC-6 Edge:`bandCount = 1`(现网值)下越界仍须被域检查接住;
        /// **不得硬编码 bandCount** —— 数值轮一扩档即踩同一失效模式。</para>
        /// <para>⚠️ 禁借绿:<c>band_count > 1</c> 的 U 判别力载体**留槽归数值轮**,本 story 不启动。</para>
        /// </summary>
        [Test]
        public void test_ac6_bandCountSingleSourceFromCookedTable_divergentSourceGoesRed()
        {
            var judgeTable = OutcomeTable53.BakeTableFromRepo(RepoRoot());
            Assert.AreEqual(1, judgeTable.UBandCount,
                "前置:现网 cooked 表 bandCount = 1(数值轮扩档后本前置须同步订正)");

            var stream = NewStream();
            stream.Append(EvC(20, 6, 0));
            stream.Append(EvJ(30, 6, 0, lexiconId: 1, confidence: 2));
            stream.Append(Ev(100, 6, 0, EventKind.CaseClosed));
            // 投影桩 → D=NotRecovered:格 (Confirmed, AppliedUnmatched, NotRecovered) 现网映射
            // = **recurrence**(回响轴)⇒ ① 有意义。先前传 null ⇒ D=Recovered ⇒ 该格 =
            // **no_echo**(无痕合法)⇒ ① 当场红 = 夹具输入选择错(非生产缺陷,2026-10-16 返工)。
            var projection = new StubProjection { Tick = 100 };

            // ① 单源(生产口径):bandCount 取自 cooked 表 ⇒ 域内档位 ⇒ 落回响轴(发出正常)。
            var singleSource = BuildEligible(judgeTable, stream.Events, projection,
                new[] { EventKind.CareApplied }, 42L, judgeTable.UBandCount);
            Assert.AreEqual(1, singleSource.Count, "恰一条合格结案");
            Assert.That(DelayTable53.DomainOutcomes, Does.Contain(singleSource[0].Outcome),
                "单源 ⇒ 判定落在回响轴(发出正常)");

            // ② 负夹具:独立常量源(= 2)与表(= 1)分歧 ⇒ uBand 可能越界。
            // ⚠️ 判据 = **不别名到第三回响行**:越界档被 TryLookup 域守护接住 ⇒
            //   divergent ∈ {single 同值(band 落 0), default 行(band 越界回落)}。
            //   **停用 Sim 侧守护 ⇒ band=1 会 key 到 no_echo 格(第三回响值)** ⇒ 本断言转红
            //   ⇒ 有判别力(F16 已落地守护的回归;卡面 AC-6「本条防装配层双源」)。
            //   注:bandCount>1 的真实别名(相邻 (P,C,D) 行)判据留槽数值轮 ⇒ 禁借绿。
            var divergent = BuildEligible(judgeTable, stream.Events, projection,
                new[] { EventKind.CareApplied }, 42L, bandCountSource: 2);
            Assert.That(new[] { singleSource[0].Outcome, (int)judgeTable.DefaultOutcome },
                Does.Contain(divergent[0].Outcome),
                "AC-6:分歧档位源**不得**静默别名到第三回响行(须同值或被域守护回落 default);"+
                $"单源={singleSource[0].Outcome} / default={judgeTable.DefaultOutcome} / 实得={divergent[0].Outcome}");

            // ③ 域检查本身的直接证据(A 案下 53 侧唯一暴露面):bandCount=1 传 uBand=1 ⇒ 未命中。
            Assert.IsFalse(judgeTable.TryLookup(
                Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.AppliedUnmatched,
                Outcome53.RecoveryLane.NotRecovered, uBand: 1, out _),
                "AC-6:越界档位被域检查接住(别名到相邻组合 = 静默失败)");
        }

        private static SimEvent EvJ(long tick, int patient, long seq, int lexiconId = 1, byte confidence = 2)
        {
            var payload = new JudgmentRecordedPayload(
                patient, new CaseId(0, patient, 0), authorPlayerId: 1, lexiconId, confidence);
            return new SimEvent(tick, new PatientId(patient), seq, EventKind.JudgmentRecorded,
                                Encoder.EncodeJudgmentRecorded(payload, string.Empty));
        }

        private static SimEvent EvC(long tick, int patient, long seq, int actionId = 1, int phase = 0)
        {
            var payload = new CareAppliedPayload(tick, actionId, caregiver: 1, phase);
            return new SimEvent(tick, new PatientId(patient), seq, EventKind.CareApplied,
                                Encoder.Encode(EventKind.CareApplied, payload));
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-7 —— 门存在性刷新(WriterExistenceGate)+ 双向判据
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-7:写者落地后 **①** 待办豁免条目须已移除,**②** 门须**正向证明写者已登记**
        /// (存在性判据是双向的 —— 防「删了条目但没人写」)。
        /// </summary>
        [Test]
        public void test_ac7_writerExistenceGate_entryRemovedAndWriterProvenPresent()
        {
            // ② 正向存在性:扫描面内确有生产 `Encode(EventKind.ConsequenceResolved` 写入点。
            // ⚠️ T-M2:扫描面用**门自身的** WriterScanDirs(六面),不再手写单面 ——
            //    否则写者迁面(如未来把 53 写者移出 Sim)后本测与门口径漂移,门红而测绿。
            var errs = new List<string>();
            var scanDirs = (string[])typeof(WriterExistenceGate)
                .GetField("WriterScanDirs", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);
            HashSet<string> written = WriterExistenceGate.ScanWrittenKinds(scanDirs, errs);
            Assert.That(errs, Is.Empty, "扫描面读取零错误:" + string.Join(";", errs));
            Assert.That(written, Does.Contain("ConsequenceResolved"),
                "AC-7:写者存在性须被**正向**证明(ConsequenceEmitter 经 IPayloadEncoder 写入)");

            // ① 豁免条目已撤(判据①「已存在写者仍挂豁免 = 红」的测试侧镜像)。
            var f = typeof(WriterExistenceGate).GetField("ExemptKinds",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(f, "豁免表字段须存在");
            var entries = ((string Kind, string Reason)[])f.GetValue(null);
            Assert.IsFalse(entries.Any(e => e.Kind == "ConsequenceResolved"),
                "写者落地同批**必须**撤豁免(卡面 AC-7;门自身判据① 亦会红)");
        }

        /// <summary>AC-7:整门跑真 registry + 真扫描面须绿(撤条目与写者同批的合取证据)。</summary>
        [Test]
        public void test_ac7_writerExistenceGate_runAllGreenAfterRefresh()
        {
            var errs = WriterExistenceGate.RunAll();
            Assert.That(errs, Is.Empty, "b7 门全绿(撤条目后由正向写者承接):" + string.Join("\n", errs));
        }

        // ════════════════════════════════════════════════════════════════════
        // 证伪轮 **⑤ 阈值边界** + 主机门 + CAP 接缝登记
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 证伪轮 **⑤**:<c>EmitTick</c> 落窗边界 —— <c>now == EmitTick</c> 发,
        /// <c>now == EmitTick - 1</c> 不发(开闭边界翻转 ⇒ 恰跨边界夹具红)。
        /// </summary>
        [Test]
        public void test_ac5306_windowBoundary_emitAtNowIncluded_beforeNowExcluded()
        {
            var table = LoadDelayTable();
            long delay = TrialHistoryDelay(table);
            const long closeTick = 60;
            long emitTick = closeTick + delay;

            var eligible = new[] { Eligible(closeTick, 11, 0, Outcome53.Outcome.TrialHistory) };

            var before = NewStream();
            var emBefore = new ConsequenceEmitter(before, new StubAuthority(), Encoder, table);
            before.Append(eligible[0].CaseClosed);
            emBefore.Emit(before.Events, eligible, emitTick - 1);
            Assert.AreEqual(0, ConsequenceCount(before.Events),
                "窗上界**开**侧(now &lt; EmitTick)不发 —— 翻转成闭侧即红");

            var at = NewStream();
            var emAt = new ConsequenceEmitter(at, new StubAuthority(), Encoder, table);
            at.Append(eligible[0].CaseClosed);
            emAt.Emit(at.Events, eligible, emitTick);
            Assert.AreEqual(1, ConsequenceCount(at.Events), "now == EmitTick ⇒ 发(窗为 (…, now])");
            Assert.AreEqual(emitTick,
                at.Events.First(e => e.Kind == EventKind.ConsequenceResolved).Tick);
        }

        /// <summary>主机门(Required:写流经 <see cref="IEventAuthority"/> 查权;客户端零写入)。</summary>
        [Test]
        public void test_required_clientAuthorityGate_nonHostWritesNothing()
        {
            var table = LoadDelayTable();
            var stream = NewStream();
            var authority = new StubAuthority { Host = false };
            var emitter = new ConsequenceEmitter(stream, authority, Encoder, table);
            stream.Append(Ev(10, 12, 0, EventKind.CaseClosed));

            var result = emitter.Emit(stream.Events,
                new[] { Eligible(10, 12, 0, Outcome53.Outcome.Residual) }, long.MaxValue / 2,
                out int emitted);

            Assert.AreEqual(ConsequenceEmitResult.NotHost, result,
                "非主机 ⇒ 具名码(不与「无待发」同码;承 PoiStateMachine C3 评审)");
            Assert.AreEqual(0, emitted, "客户端零写入(主机唯一 Append,ADR-005)");
            Assert.AreEqual(0, ConsequenceCount(stream.Events));
        }

        /// <summary>
        /// S-B1 负夹具(2026-10-17 补,**原判定所列三条夹具中唯一真缺的一条**):
        /// <c>Emit</c> 的调用方义务 = <c>eligible</c> 须含**全部** <c>EmitTick ≤ now</c> 的合格行。
        /// 若调用方按 lookback 裁掉队首到期行,幂等判据「按该病人升序前缀」会把余行误判「已发」
        /// ⇒ 该病人余下后果**永久不发**,而返回码仍是合法的 <c>NothingPending</c>(静默,破 AC-53-14)。
        /// <para>⚠️ 本夹具钉的是**前提被违反时的可观察后果**,不是一条函数边界 ——
        /// 前提本身在 <c>Emit</c> 的 doc 里("S-B1")。判别力由原判定给的判据证明:
        /// **删除该 doc 前置条件块 ⇒ 本测转红**(它对 <c>ConsequenceEmitter.cs</c> 的源码文本做断言,
        /// 是该前置条件在代码里的唯一可执行证据;删 doc 的同时也就删掉了「这是调用方义务」的声明)。</para>
        /// </summary>
        [Test]
        public void test_sb1_truncatedEligible_headRowDropped_silentlyWithholdsRest()
        {
            var table = LoadDelayTable();
            long dResidual = ResidualDelay(table);
            long dRecurrence = RecurrenceDelay(table);
            Assert.AreNotEqual(dResidual, dRecurrence,
                "前置:两案延迟须不同,否则两条 EmitTick 相同,裁队首 = 裁全部,夹具失去区分度");

            var all = new[]
            {
                Eligible(100, 7, 0, Outcome53.Outcome.Residual),
                Eligible(200, 7, 1, Outcome53.Outcome.Recurrence),
            };
            long firstEmit = 100 + dResidual;
            long secondEmit = 200 + dRecurrence;

            // ① 前提满足:全量 eligible ⇒ 第一次到期行落地(对照基线)。
            var stream = NewStream();
            var emitter = NewEmitter(stream);
            stream.Append(all[0].CaseClosed);
            stream.Append(all[1].CaseClosed);
            Assert.AreEqual(ConsequenceEmitResult.Emitted,
                emitter.Emit(stream.Events, all, firstEmit, out int firstCount), "① 全量入参 ⇒ 发");
            Assert.AreEqual(1, firstCount, "① 恰一条(CaseClosed 本身不计)");

            // ② 前提违反:调用方按 lookback 裁掉队首(早已发行)那条,只喂余行。
            var truncated = new[] { all[1] };
            var second = emitter.Emit(stream.Events, truncated, secondEmit, out int secondCount);

            // ③ 静默漏发的指纹:返回码**合法**、却一条不发。
            Assert.AreEqual(ConsequenceEmitResult.NothingPending, second,
                "S-B1:裁掉队首后返回码仍是**合法**的 NothingPending —— 这正是静默面");
            Assert.AreEqual(0, secondCount, "S-B1:该病人余下后果一条不发(静默、不可观察)");
            Assert.AreEqual(1, ConsequenceCount(stream.Events),
                "S-B1:流中 ConsequenceResolved 仍只有 ① 落地的那条 —— ② 本该发的那条丢了");

            // ④ 对照:同一输入不裁 ⇒ 第二条照常发。证明 ③ 的漏发**确由裁剪引起**。
            var control = NewStream();
            var controlEmitter = NewEmitter(control);
            control.Append(all[0].CaseClosed);
            control.Append(all[1].CaseClosed);
            Assert.AreEqual(ConsequenceEmitResult.Emitted,
                controlEmitter.Emit(control.Events, all, firstEmit), "④ 对照:第一次照发");
            Assert.AreEqual(ConsequenceEmitResult.Emitted,
                controlEmitter.Emit(control.Events, all, secondEmit), "④ 对照:不裁则第二次照发");
            Assert.AreEqual(2, ConsequenceCount(control.Events),
                "④ 对照:全量入参 ⇒ 两条后果都在流里 ⇒ ③ 的 1 条缺口确由裁剪造成");

            // ⑤ 前提的**可执行出处**:Emit 的 doc 必须声明该义务(删前置条件块 ⇒ 本断言红)。
            string src = File.ReadAllText(Path.Combine(RepoRoot(),
                "unity/Assets/Sim/Consequences/ConsequenceEmitter.cs"));
            StringAssert.Contains("S-B1", src, "⑤ 前置条件块须带 S-B1 标记");
            StringAssert.Contains("不得", src, "⑤ 前置条件块须写明「不得」裁剪");
            StringAssert.Contains("永久不发", src, "⑤ 须写明静默失效模式");
        }

        /// <summary>
        /// `TD-medcons-14`(**2026-10-29 用户裁定「CAP 收窄到出现类事件」后的正面判据**):
        /// 病人**离屏** + 在场数满 24 ⇒ 到期后果**照发**,不再撞 AC-15。
        /// </summary>
        /// <remarks>
        /// <para>🔁 <b>本条前身 = <c>test_w5_…_capsThrowPropagates_notSwallowed</c></b>,
        /// 当时断言的是**相反**行为(抛且上抛不吞)—— 它钉住的是 W-5 登记的接缝形态,
        /// 不是设计意图。用户裁定收窄后,该行为本身被判定为缺陷(离屏病人的后果被当成
        /// 「新病人」拒收 ⇒ 后果永久不发,破 AC-53-14),故本条**由红转绿**。
        /// 按「原文不删」纪律,旧断言在此以文字留档,不保留为可执行夹具 ——
        /// 留一条绿的旧断言就等于把缺陷钉成规格。</para>
        /// <para>⚠️ 存根须**两段式**:<c>Arm=false</c> 放行前置 CaseClosed 入流
        /// (真病人 13 当时在场),<c>Arm=true</c> 才把该病人转成「离屏 + 在场数满 24」
        /// —— 否则连 CaseClosed 本身就被 AC-15 拒收,异常源与断言对象错位(F1/F3 教训:
        /// 夹具构造的事件时序必须与流不变量 BCD-码-3 一致)。</para>
        /// <para><b>判别力</b>:把 <c>EventStream.IsPatientAppearance</c> 改回恒真(旧口径)⇒
        /// 本测红(重新抛 AC-15);改回恒假 ⇒ <c>event_stream_test.test_cap_rejectsBeyond24</c> 红。</para>
        /// </remarks>
        [Test]
        public void test_tdMedcons14_offscreenPatientDueConsequence_emitsDespiteFullCap()
        {
            var table = LoadDelayTable();
            long delay = ResidualDelay(table);
            var presence = new CapFullAndAbsent();   // Arm=false:放行入流
            var stream = new EventStream(new StubIdAuthority(), presence);
            var emitter = new ConsequenceEmitter(stream, new StubAuthority(), Encoder, table);
            stream.Append(Ev(10, 13, 0, EventKind.CaseClosed));

            presence.Arm = true;                    // 病人 13 离场 + 在场数打满 24
            Assert.AreEqual(EventStream.PATIENT_APPEARANCE_CAP, presence.PresentCount,
                "前置:CAP 须真的打满(否则本测无法区分「豁免」与「本来就不拦」)");

            Assert.DoesNotThrow(() => emitter.Emit(
                stream.Events, new[] { Eligible(10, 13, 0, Outcome53.Outcome.Residual) }, 10 + delay),
                "CAP 满 + 离屏 + 非出现类 ⇒ 不得抛(TD-medcons-14:后果不是新病人)");

            var rows = stream.Events.Where(e => e.Kind == EventKind.ConsequenceResolved).ToList();
            Assert.AreEqual(1, rows.Count, "后果须真的落地(静默丢弃同样破 AC-53-14)");
            Assert.AreEqual(10 + delay, rows[0].Tick, "发出 tick = EmitTick(不在发出侧被推迟)");
            Assert.AreEqual(13, rows[0].Patient.Value, "病人归因不被削成 None");
        }

        /// <summary>入参非法 fail-loud(eligible 行不是 CaseClosed ⇒ 不静默当「无痕」)。</summary>
        [Test]
        public void test_edge_eligibleRowNotCaseClosed_failsLoud()
        {
            var table = LoadDelayTable();
            var emitter = NewEmitter(NewStream(), table);
            var bad = new List<(SimEvent CaseClosed, int Outcome)>
            {
                (Ev(10, 1, 0, EventKind.CaseOpened), (int)Outcome53.Outcome.Residual),
            };
            Assert.Throws<ArgumentException>(() => emitter.Emit(
                new List<SimEvent>(), bad, 10 + ResidualDelay(table)),
                "合格行的触发事件须为 CaseClosed(F-53.2 自变量);静默接受 = 第二静默面");
        }

        // ════════════════════════════════════════════════════════════════════
        // 修复轮(R-1 · 2026-10-16):覆盖缺口负夹具 + 新增守卫的判别力载体
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_r1_negativeTick_throwsArgumentOutOfRange()
        {
            var emitter = NewEmitterOnSink(new CollectingSink(), LoadDelayTable());
            Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Emit(
                new List<SimEvent>(), new List<(SimEvent, int)>(), -1),
                "nowTick < 0 = 入参非法(与 CaseCloseWriter 同口径,编程错误须 fail-loud)");
        }

        [Test]
        public void test_r1_emitTickOverflow_throwsOverflow_notSilentWrap()
        {
            Assert.Throws<OverflowException>(() =>
                DelayTable53.EmitTick(long.MaxValue - 10, 100),
                "ADR-012 F7:溢出须 fail-loud,不得静默回绕成早于结案的 tick");
        }

        [Test]
        public void test_r1_emitTick_zeroOrNegativeDelay_throwsAtCallSite()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DelayTable53.EmitTick(100, 0),
                "AC-53-06 的调用点复述:DelayTicks ≤ 0 ⇒ 后果与结案同 tick ⇒ 可被归因");
            Assert.Throws<ArgumentOutOfRangeException>(() => DelayTable53.EmitTick(100, -1));
        }

        [Test]
        public void test_r1_tableConstructor_nullInput_stillRequiresFullCoverage()
        {
            // null 入参 ⇒ 空集 ⇒ 全覆盖校验拒收(而非 NullReferenceException 或静默空表)。
            Assert.Throws<ArgumentException>(() => DelayTable53.CreateTable(null),
                "null 字典 ⇒ 零行 ⇒ 全覆盖校验须拒(静默空表 = 永不发后果)");
        }

        [Test]
        public void test_r1_emptyOrNullEligible_returnsNothingPending_zeroWrites()
        {
            var sink = new CollectingSink();
            var emitter = NewEmitterOnSink(sink, LoadDelayTable());

            Assert.AreEqual(ConsequenceEmitResult.NothingPending,
                emitter.Emit(new List<SimEvent>(), null, 10_000), "null eligible ⇒ 无待发(不抛)");
            Assert.AreEqual(ConsequenceEmitResult.NothingPending,
                emitter.Emit(new List<SimEvent>(), new List<(SimEvent, int)>(), 10_000), "空 eligible ⇒ 无待发");
            Assert.AreEqual(0, sink.Count, "零写入分支须真的零写入");
        }

        [Test]
        public void test_r1_nullStream_countedAsNoEmittedYet()
        {
            var table = LoadDelayTable();
            var sink = new CollectingSink();
            var emitter = NewEmitterOnSink(sink, table);

            // stream = null ⇒ 视为「无任何已发行」⇒ 到期行照发(不得 NRE)。
            var result = emitter.Emit(null,
                new[] { Eligible(100, 5, 0, Outcome53.Outcome.Residual) }, 100 + ResidualDelay(table));

            Assert.AreEqual(ConsequenceEmitResult.Emitted, result, "null 流 = 无已发行 ⇒ 照发");
            Assert.AreEqual(1, sink.Count);
        }

        /// <summary>修复轮 S-W2 的判别力载体:上游重复喂同一结案 ⇒ 只发一条(卡面 Guardrail)。</summary>
        [Test]
        public void test_r1_duplicateEligibleRow_emitsExactlyOne()
        {
            var table = LoadDelayTable();
            var sink = new CollectingSink();
            var emitter = NewEmitterOnSink(sink, table);

            var row = Eligible(100, 3, 0, Outcome53.Outcome.Residual);
            var result = emitter.Emit(new List<SimEvent>(), new[] { row, row },
                100 + ResidualDelay(table), out int emitted);

            Assert.AreEqual(ConsequenceEmitResult.Emitted, result);
            Assert.AreEqual(1, emitted,
                "同一结案被喂两次 ⇒ 恰一条后果(流层去重键含 PayloadRef 而 blob 池不 intern ⇒ 抓不到;" +
                "**停用 BuildDueRows 的五元组去重 ⇒ 本断言转红**得 2)");
            Assert.AreEqual(1, sink.Count, "流中后果恰一条");
        }

        /// <summary>修复轮 S-W3 的判别力载体:世界级哨兵行须拒(fail-loud,不产幻影后果)。</summary>
        [Test]
        public void test_r1_noneSentinelPatientRow_rejected_notSilentlyEmitted()
        {
            var table = LoadDelayTable();
            var emitter = NewEmitterOnSink(new CollectingSink(), table);
            var none = new SimEvent(100, PatientId.None, 0, EventKind.CaseClosed, default(PayloadRef));

            Assert.Throws<ArgumentException>(() => emitter.Emit(
                new List<SimEvent>(), new[] { (none, (int)Outcome53.Outcome.Residual) },
                100 + ResidualDelay(table)),
                "PatientId.None 的合格行须拒 —— 后果逐例属某病人(None 会产无病人归因的世界级幻影后果," +
                "并绕过 AC-15 在场检查);**停用该守卫 ⇒ 本断言转红**(静默发射)");
        }

        /// <summary>修复轮 S-W4 的判别力载体:定义域白名单须运行期不可写。</summary>
        [Test]
        public void test_r1_domainOutcomes_notCastableToWritableArray()
        {
            Assert.IsFalse(DelayTable53.DomainOutcomes is int[],
                "DomainOutcomes 不得暴露为裸 int[] —— readonly 只锁引用,元素仍可写 ⇒ " +
                "调用方 cast 回 int[] 即可把结局轴 / NoEcho 加进定义域(拆掉 F-53.2 收窄护栏);" +
                "**改回 new[]{...} 直赋 ⇒ 本断言转红**");
            Assert.IsFalse(DelayTable53.IsInDomain((int)Outcome53.Outcome.PatientLives));
            Assert.IsFalse(DelayTable53.IsInDomain((int)Outcome53.Outcome.NoEcho));
        }

        /// <summary>修复轮 S-B2 的判别力载体:CAP 半提交 + 他写者推进流尾 ⇒ 补发须**具名**失败。</summary>
        [Test]
        public void test_r1_overdueRowAfterStreamTailAdvanced_failsLoud_notBcdBareThrow()
        {
            var table = LoadDelayTable();
            var stream = NewStream();
            var emitter = new ConsequenceEmitter(
                new SinkOver(stream), new StubAuthority(), Encoder, table);

            stream.Append(Ev(10, 4, 0, EventKind.CaseClosed));   // 在场 ⇒ 放行
            long due = 10 + ResidualDelay(table);

            // 他写者把流尾推进到 due 之后(现实:在场 24 人持续产生高 tick 事件)。
            stream.Append(Ev(due + 500, 4, 0, EventKind.ActorCellEntered));

            var ex = Assert.Throws<InvalidOperationException>(() => emitter.Emit(
                stream.Events, new[] { Eligible(10, 4, 0, Outcome53.Outcome.Residual) }, due),
                "到期行回退于流尾 ⇒ 须**具名**失败(指出半提交成因)," +
                "而非让 EventStream 的 BCD-码-3 裸抛;静默吞掉 = 永久漏后果(破 AC-53-14)");
            Assert.That(ex.Message, Does.Contain("回退于流尾"),
                "异常须来自本件的 S-B2 预检(具名指向成因),而非 EventStream 的裸 BCD-码-3");
        }

        /// <summary>修复轮 S-M1 的判别力载体:同 EmitTick 两行的破平序 + 不合并。</summary>
        [Test]
        public void test_r1_sameEmitTick_twoRows_bothEmitted_notMerged()
        {
            var table = LoadDelayTable();
            var sink = new CollectingSink();
            var emitter = NewEmitterOnSink(sink, table);

            long residual = ResidualDelay(table);
            long trial = TrialHistoryDelay(table);
            Assert.Greater(trial, residual, "前置:长延迟须大于短延迟(现网 2400 > 600)");

            // 短延迟晚结案 vs 长延迟早结案 ⇒ EmitTick 相等(破平面)。
            long closeA = 100;                          // residual ⇒ EmitTick = 100 + residual
            long closeB = closeA + residual - trial;    // trial    ⇒ EmitTick 与之相等

            var eligible = new[]
            {
                Eligible(closeB, 8, 0, Outcome53.Outcome.TrialHistory),
                Eligible(closeA, 7, 0, Outcome53.Outcome.Residual),
            };
            var result = emitter.Emit(new List<SimEvent>(), eligible,
                closeA + residual + 1, out int emitted);

            Assert.AreEqual(ConsequenceEmitResult.Emitted, result);
            Assert.AreEqual(2, emitted, "同 EmitTick 两行均须发出(不合并)");
            Assert.AreEqual(2, sink.Count);
        }

        /// <summary>修复轮:幂等全 skip(pending 为空)须返回具名 NothingPending。</summary>
        [Test]
        public void test_r1_alreadyEmitted_allPendingSkipped_returnsNothingPending()
        {
            var table = LoadDelayTable();
            var stream = NewStream();
            var emitter = new ConsequenceEmitter(
                new SinkOver(stream), new StubAuthority(), Encoder, table);

            stream.Append(Ev(100, 2, 0, EventKind.CaseClosed));
            var eligible = new[] { Eligible(100, 2, 0, Outcome53.Outcome.Residual) };
            long now = 100 + ResidualDelay(table);

            Assert.AreEqual(ConsequenceEmitResult.Emitted, emitter.Emit(stream.Events, eligible, now));
            Assert.AreEqual(ConsequenceEmitResult.NothingPending,
                emitter.Emit(stream.Events, eligible, now),
                "已发行 ⇒ 第二次同输入须 NothingPending(零写入分支具名)");
        }

        /// <summary>修复轮 B-M1 的判别力载体:ParseRows 拆分后行层错误仍逐条聚合(不首错即停)。</summary>
        [Test]
        public void test_r1_binder_aggregatesMultipleRowErrors_notFirstOnly()
        {
            // 两行同时违规:residual = 0(≤0)+ recurrence = -1(≤0)⇒ 两条错误均须报出。
            var ex = Assert.Throws<BakeValidationException>(
                () => DelayTable53Bake.BindForTest(MinimalDelayJson(residual: 0, recurrence: -1)));
            Assert.GreaterOrEqual(ex.Errors.Count, 2,
                "聚合 throw:一次报全部(首错即停会掩盖其余行的问题);" +
                "拆分 ParseRows/ParseRow 后本性质须保留");
        }

        /// <summary>修复轮 B-W3 的判别力载体:跨侧定义域两处声明须等值。</summary>
        [Test]
        public void test_r1_domainDeclarations_matchAcrossBinderAndSim()
        {
            var bound = DelayTable53Bake.BindForTest(MinimalDelayJson());
            var table = DelayTable53Bake.Materialize(bound);   // 内含跨侧等值断言

            Assert.AreEqual(DelayTable53.DomainOutcomes.Count, table.RowCount,
                "Sim 侧定义域大小 = 表行数");
            Assert.AreEqual(DelayTable53.DomainOutcomes.Count, bound.Rows.Count,
                "Binder 绑定行数 = Sim 侧定义域大小(两处声明等值 —— Materialize 的断言是硬门)");
        }

        // ── 修复轮专用 helper(全部具名;不复用/不遮蔽既有桩)────────────────

        /// <summary>直接收集 Append 的 sink(不查在场 / 不判重 —— 供窄面夹具用)。</summary>
        private sealed class CollectingSink : IEventSink
        {
            private readonly List<SimEvent> _events = new List<SimEvent>();
            public int Count => _events.Count;
            public void Append(in SimEvent e) => _events.Add(e);
        }

        /// <summary>把 Append 转发给真 EventStream 的 sink(保留 BCD-码-3 语义)。</summary>
        private sealed class SinkOver : IEventSink
        {
            private readonly EventStream _inner;
            public SinkOver(EventStream inner) { _inner = inner; }
            public void Append(in SimEvent e) => _inner.Append(e);
        }

        /// <summary>发射器 → 收集型 sink(窄面夹具用;不触 EventStream 的 CAP / 去重)。</summary>
        private static ConsequenceEmitter NewEmitterOnSink(IEventSink sink, DelayTable53.Table table)
            => new ConsequenceEmitter(sink, new StubAuthority(), Encoder, table);

        // ════════════════════════════════════════════════════════════════════
        // 表值读取 helper(**读表不抄值**)
        // ════════════════════════════════════════════════════════════════════

        private static long ResidualDelay(DelayTable53.Table t)
        {
            Assert.IsTrue(t.TryDelayTicks((int)Outcome53.Outcome.Residual, out long d), "residual 行须存在");
            return d;
        }

        private static long RecurrenceDelay(DelayTable53.Table t)
        {
            Assert.IsTrue(t.TryDelayTicks((int)Outcome53.Outcome.Recurrence, out long d), "recurrence 行须存在");
            return d;
        }

        private static long TrialHistoryDelay(DelayTable53.Table t)
        {
            Assert.IsTrue(t.TryDelayTicks((int)Outcome53.Outcome.TrialHistory, out long d),
                "trial_history 行须存在");
            return d;
        }
    }
}
