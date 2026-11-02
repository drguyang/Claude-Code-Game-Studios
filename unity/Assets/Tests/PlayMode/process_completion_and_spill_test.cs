// S24-1 · processing story-005 —— 完成时点调度 + cue 发射 + 溢出路径的**18 侧**判据
//
// 权威来源:
//   design/gdd/processing.md
//     · F-18.3(:458-461)`CompleteTick = craft.start_tick + recipe.duration_ticks`
//     · R-18-D[甲]②(:279-297)起货溢出 —— 成品 id 引用点火时**已铸**的 `output_instance_ids[]`
//     · AC-18-21 —— 「时间到达 CompleteTick ⇒ 器物状态变化可判起货 + `sfx_process_complete` 发射;
//       禁以墙钟 / 帧对齐为发射条件」
//   ADR-018 §一(44 只触发;18 是发射方)· ADR-005(tick 驱动,非墙钟)
//   ADR-009 §六/§七(溢出落地 = 世界流;写者 = 20)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件**刻意不做**什么(禁借绿)
// ─────────────────────────────────────────────────────────────────────────────
// ① **不做**溢出落地的「真 20 结算 + 真 fold」夹具 —— 那条链的**入口今日不存在**
//    (`InventoryFold.ApplyCraftOutputs` 直接把 `Craft` 的产出建进炮制者名下,
//    与完成 tick 无关;`DropSpawned` 的溢出发出者 = 20,**尚无完成时点的结算入口**)。
//    用 spy 顶替 fold 写一条绿夹具 = F1 型假绿(断言的是测试自己写的谓词)。
//    ⇒ 本文件只断 **18 侧**的溢出事实(id 原样携带 + `ToolCell` = `spawn_anchor` 来源),
//      落地半边登记为残余(见 story-005 §Completion Notes)。
// ② **不测** AC-18-09 / 18-22 / 18-24 / 18-08 —— 四条 `[L]`,人听 / 人走,机器绿不得顶替。
// ③ **不声称**「起货音已实现」—— 事件表今日无 `sfx_process_complete` 行,生产上不出声。
//    本文件测的是**发射时机与条数**(用注入的 ordinal 7),不是素材与混音。
//
// ─────────────────────────────────────────────────────────────────────────────
// 只读外部夹具声明(`.claude/rules/test-standards.md` 的具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 本文件读两处**仓库内源文件**,只读、不写:
//   · `unity/Assets/Sim/Processing/FurnaceCompletionSchedule.cs`
//   · `unity/Assets/Gameplay.Boot/ProcessingCompletionDriver.cs`
// **为什么必须读真文件而不是内联**:AC-18-21 断的是「**这条路径**没有墙钟符号」——
// 这是对**被测源本身**的断言。内联一段样例只能证明「样例里没有」,与生产文件无关(F1 型假绿)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Processing;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>processing story-005(AC-18-21 [I])的夹具 —— 18 侧输出:调度落点 + cue 发射。</summary>
    [TestFixture]
    public class ProcessCompletionAndSpillTest
    {
        // ══════════════════════════════════════════════════════════════════
        // 共享夹具
        // ══════════════════════════════════════════════════════════════════

        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        /// <summary>注入给驱动的 cue ordinal(表里没有该行,故用一个人造值 —— 见文件头 ③)。</summary>
        private const int InjectedCompleteOrdinal = 7;

        [SetUp]
        public void Setup()
        {
            var pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(pool);
            _decoder = new PayloadDecoder(pool);
        }

        // ── 事件构造(真编码器 + 真池 + 真解码器,零 mock)─────────────────

        private SimEvent Craft(int actorId, long startTick, long durationTicks,
                               long envelopeTick, long seq,
                               int recipeId = 1,
                               long[] outIds = null,
                               int[] outQty = null,
                               WorldPos? toolCell = null)
        {
            int n = (outIds ?? new[] { 201L }).Length;
            var qty = outQty ?? Enumerable.Repeat(1, n).ToArray();
            var quality = Enumerable.Repeat(3, n).ToArray();
            var payload = new CraftPayload(
                actorId, recipeId, startTick, durationTicks,
                new long[] { 101 },
                new[] { 1 },
                qty,
                quality,
                outIds ?? new[] { 201L },
                toolCell ?? new WorldPos(4, 0, 7));
            return new SimEvent(envelopeTick, PatientId.None, seq, EventKind.Craft,
                                _encoder.Encode(EventKind.Craft, payload));
        }

        private List<FurnaceCompletion> Collect(IReadOnlyList<SimEvent> events, long tick)
        {
            var dest = new List<FurnaceCompletion>();
            FurnaceCompletionSchedule.CollectAt(events, _decoder, tick, dest);
            return dest;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-21 · 判据是**等号**,不是区间
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1821_completionHitsExactlyOnEqualityTick()
        {
            var events = new List<SimEvent> { Craft(actorId: 1, startTick: 100, durationTicks: 20,
                                                    envelopeTick: 100, seq: 0) };

            Assert.That(Collect(events, 119), Is.Empty, "完成前 1 tick **不得**到点(区间口径会在此处误发)");
            Assert.That(Collect(events, 120).Count, Is.EqualTo(1), "`start + duration` 当拍必须到点");
            Assert.That(Collect(events, 121), Is.Empty, "完成后 1 tick **不得**再到点(`>=` 口径会重复发)");

            Assert.That(Collect(events, 120)[0].CompleteTick, Is.EqualTo(120L), "F-18.3 的算式");
        }

        [Test]
        public void test_ac1821_durationBoundary_oneTick_completesAtStartPlusOne()
        {
            var events = new List<SimEvent> { Craft(1, startTick: 100, durationTicks: 1,
                                                    envelopeTick: 100, seq: 0) };
            Assert.That(Collect(events, 100), Is.Empty, "点火当拍不完成(duration = 1)");
            Assert.That(Collect(events, 101).Count, Is.EqualTo(1), "duration = 1 ⇒ 下一拍完成");
        }

        [Test]
        public void test_ac1821_durationZero_throwsInsteadOfCompletingImmediately()
        {
            // ⚠️ **与 qa-plan 的措辞不一致,以本判据为准**:qa-plan 写「`duration_ticks` 边界 `= 0`
            //    (当拍即完成)」,而 `duration_ticks ≥ 1` 是 21a 数据表的硬约束
            //    (`FurnaceState.TryGetLatest` 同款先验)—— 0 是**坏数据**,不是「立即完成」。
            //    这里断的是 fail-closed,不是产品行为;若将来 21a 允许 0,应先改 21a 与本行。
            var events = new List<SimEvent> { Craft(1, startTick: 100, durationTicks: 0,
                                                    envelopeTick: 100, seq: 0) };

            Assert.Throws<InvalidOperationException>(() => Collect(events, 100),
                "duration_ticks = 0 必须 fail-closed 抛,不得静默当作「当拍完成」");
        }

        [Test]
        public void test_ac1821_prefixUsesEnvelopeTick_notPayloadStartTick()
        {
            // 坏事件:载荷说 start=100(duration 20 ⇒ 完成 120),但**信封**在 tick 200 才出现。
            // 承 `FurnaceState` 的 2026-10-26 W1 订正:存在时点 = envelope。
            // ⇒ 在 tick 120 询问时,这条事件**还不存在**,不得回填答案。
            var events = new List<SimEvent> { Craft(1, startTick: 100, durationTicks: 20,
                                                    envelopeTick: 200, seq: 0) };

            Assert.That(Collect(events, 120), Is.Empty,
                "前缀用了载荷 StartTick ⇒ 尚未发生的 Craft 会回溯污染过去的答案(W1 订正靶)");
            Assert.That(Collect(events, 200), Is.Empty, "信封当拍到点时,完成时点(120)已过去 —— 等号口径不发");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-21 · 重放幂等 + 每 actor 至多一条
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1821_sameStreamSameTick_sameAnswerTwice()
        {
            var events = new List<SimEvent>
            {
                Craft(1, 100, 20, 100, 0),
                Craft(2, 90, 30, 90, 0),
            };

            List<FurnaceCompletion> a = Collect(events, 120);
            List<FurnaceCompletion> b = Collect(events, 120);

            Assert.That(a.Select(c => c.ActorId).ToArray(), Is.EqualTo(new[] { 2, 1 }),
                "两 actor 同 tick 完成,次序 = 全序键升序(actor 2 的 Craft 更早)");
            Assert.That(b.Select(c => c.ActorId).ToArray(), Is.EqualTo(a.Select(c => c.ActorId).ToArray()),
                "同流同 tick 两次求值必须给出同一答案(重放幂等 —— 存档恢复半边)");
        }

        [Test]
        public void test_ac1821_sameActorTwoFurnaces_keepsEarliestByOrdering()
        {
            // 非法流(合法流里 IsBusy 拒同时两炉):两个不同 start 落在同一完成 tick。
            var events = new List<SimEvent>
            {
                Craft(actorId: 1, startTick: 100, durationTicks: 20, envelopeTick: 100, seq: 0),  // 完成 120
                Craft(actorId: 1, startTick: 115, durationTicks: 5,  envelopeTick: 115, seq: 1),  // 完成 120
            };

            List<FurnaceCompletion> got = Collect(events, 120);
            Assert.That(got.Count, Is.EqualTo(1), "每 actor 至多一条(否则同一炉会发两次 cue)");
            Assert.That(got[0].StartTick, Is.EqualTo(100L), "取全序键最小者 = 更早点火的那炉");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-21 · cue 发射(18 → 44 边界)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1821_cueEmittedExactlyOncePerCompletion()
        {
            var events = new List<SimEvent> { Craft(1, 100, 20, 100, 0) };
            var spy = new SpyCueSink();
            var driver = new ProcessingCompletionDriver(events, _decoder, spy, InjectedCompleteOrdinal);

            Assert.That(driver.CueResolved, Is.True, "注入了 ordinal ⇒ 已解析(否则本测无判别力)");

            Assert.That(driver.OnTickEdge(119), Is.EqualTo(0), "完成前不得到点");
            Assert.That(spy.Emitted.Count, Is.EqualTo(0), "完成前不得发 cue");

            Assert.That(driver.OnTickEdge(120), Is.EqualTo(1), "完成当拍到点 1 炉");
            Assert.That(spy.Emitted.Count, Is.EqualTo(1), "`sfx_process_complete` **恰 1 条**");
            Assert.That(spy.Emitted[0].Cue, Is.EqualTo(InjectedCompleteOrdinal), "cue = 注入的 ordinal");
            Assert.That(spy.Emitted[0].Looped, Is.False, "起货是一次性事件,不是声床");
            Assert.That(spy.Emitted[0].Source, Is.EqualTo(1), "声源实体 = 该炉的归属玩家");

            // 再推进一拍:同一炉**不得**再发一次。
            Assert.That(driver.OnTickEdge(121), Is.EqualTo(0), "完成后不再到点");
            Assert.That(spy.Emitted.Count, Is.EqualTo(1), "重复发射 ⇒ 红");
            Assert.That(driver.EmittedCueCount, Is.EqualTo(1));
        }

        [Test]
        public void test_ac1821_twoActorsSameTick_emitTwoCues_notMerged()
        {
            var events = new List<SimEvent>
            {
                Craft(1, 100, 20, 100, 0),
                Craft(2, 100, 20, 100, 1),
            };
            var spy = new SpyCueSink();
            var driver = new ProcessingCompletionDriver(events, _decoder, spy, InjectedCompleteOrdinal);

            Assert.That(driver.OnTickEdge(120), Is.EqualTo(2), "两玩家同 tick 完成 ⇒ 两条完成记录");
            Assert.That(spy.Emitted.Count, Is.EqualTo(2), "各发 1 条,**不得合并**");
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, spy.Emitted.Select(c => c.Source).ToArray(),
                "两条 cue 的声源须分别指向两个玩家(合并 = 少一个炉的声)");
        }

        [Test]
        public void test_ac1821_repeatedTickEdge_throwsInsteadOfDoubleEmitting()
        {
            var events = new List<SimEvent> { Craft(1, 100, 20, 100, 0) };
            var spy = new SpyCueSink();
            var driver = new ProcessingCompletionDriver(events, _decoder, spy, InjectedCompleteOrdinal);

            driver.OnTickEdge(120);
            Assert.That(spy.Emitted.Count, Is.EqualTo(1));

            Assert.Throws<InvalidOperationException>(() => driver.OnTickEdge(120),
                "同一 tick 边沿重入必须 fail-closed(静默再算一遍 = 双发)");
            Assert.That(spy.Emitted.Count, Is.EqualTo(1), "抛出前不得已经多发一条");

            Assert.Throws<InvalidOperationException>(() => driver.OnTickEdge(119),
                "tick 回退同样 fail-closed(与 EventStream 的 tick 单调前提同口径)");
        }

        [Test]
        public void test_ac1821_unresolvedCue_noEmission()
        {
            var events = new List<SimEvent> { Craft(1, 100, 20, 100, 0) };
            var spy = new SpyCueSink();
            var driver = new ProcessingCompletionDriver(events, _decoder, spy,
                                                        AudioEventDataSet.NoOrdinal);

            Assert.That(driver.CueResolved, Is.False);
            Assert.That(driver.OnTickEdge(120), Is.EqualTo(0), "未解析 ⇒ 不扫描、不报完成");
            Assert.That(spy.Emitted.Count, Is.EqualTo(0), "未解析 ⇒ 绝不发 cue(今日生产态)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 溢出路径的 **18 侧**半边:无第二铸造点 + spawn_anchor 来源
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1821_completionCarriesIgnitionCastIds_unchanged()
        {
            var ignitionIds = new[] { 201L, 202L };
            var cell = new WorldPos(4, 0, 7);
            var events = new List<SimEvent>
            {
                Craft(1, 100, 20, 100, 0, outIds: ignitionIds, outQty: new[] { 2, 1 }, toolCell: cell),
            };

            List<FurnaceCompletion> got = Collect(events, 120);
            Assert.That(got.Count, Is.EqualTo(1));

            Assert.That(got[0].OutputInstanceIds, Is.EqualTo(new[] { 201L, 202L }),
                "落地用的 id 必须逐位等于**点火时**铸造的 id —— 无第二铸造点");
            Assert.That(got[0].ToolCell.Equals(cell), Is.True,
                "溢出落地的 `spawn_anchor` 来源 = `Craft.ToolCell`");
            Assert.That(got[0].RecipeId, Is.EqualTo(1), "配方 ordinal 原样携带");
        }

        [Test]
        public void test_ac1821_scheduler_cannotMintIds()
        {
            // 结构面判据(不是值判据):`CollectAt` 的入参类型里**没有**任何发号器 ——
            // 18 的调度在类型上就不可能铸造新 id。
            // ⚠️ 诚实标注:本测证明的是**直接**铸造不可达,不是「未来任何间接铸造都抓得到」。
            var method = typeof(FurnaceCompletionSchedule)
                .GetMethod("CollectAt", new[] { typeof(IReadOnlyList<SimEvent>),
                                                typeof(IPayloadDecoder), typeof(long),
                                                typeof(List<FurnaceCompletion>) });
            Assert.That(method, Is.Not.Null, "CollectAt 的签名变了 ⇒ 本测的靶失效");

            foreach (var p in method.GetParameters())
            {
                Assert.That(p.ParameterType.Name, Does.Not.Contain("IIdAuthority"),
                    $"`CollectAt` 出现发号器入参({p.Name})—— 18 不得铸造 id(R-18-D 溢出半边的结构前提)");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 禁墙钟 / 禁帧对齐(AC-18-21 第三条)
        // ══════════════════════════════════════════════════════════════════

        private static readonly string[] EmissionSourceFiles =
        {
            "Assets/Sim/Processing/FurnaceCompletionSchedule.cs",
            "Assets/Gameplay.Boot/ProcessingCompletionDriver.cs",
        };

        private static readonly string[] WallClockTokens =
        {
            "DateTime", "DateTimeOffset", "Stopwatch", "Environment",
            "Time.deltaTime", "Time.time", "Time.realtimeSinceStartup",
            "Time.frameCount", "TimeSpan",
        };

        [Test]
        public void test_ac1821_emissionPath_hasNoWallClockSymbols()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var hits = new List<string>();

            foreach (string rel in EmissionSourceFiles)
            {
                string path = Path.Combine(root, "unity", rel);
                Assert.That(File.Exists(path), Is.True, $"扫描靶不存在:{path}");
                string code = StripComments(File.ReadAllText(path));
                foreach (string bad in WallClockTokens)
                {
                    if (code.IndexOf(bad, StringComparison.Ordinal) >= 0)
                        hits.Add($"{rel} 出现 `{bad}`");
                }
                // UnityEngine.Time 的裸用(`Time.xxx`)也在此一并命中(上面的 `Time.` 前缀项)。
                if (code.IndexOf("UnityEngine.Time", StringComparison.Ordinal) >= 0)
                    hits.Add($"{rel} 出现 `UnityEngine.Time`");
            }

            Assert.That(hits, Is.Empty,
                "完成调度的发射条件出现墙钟 / 帧计数符号(AC-18-21:只许 tick):\n  "
                + string.Join("\n  ", hits));
        }

        // ══════════════════════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════════════════════

        /// <summary>剥除 `//` 与 `/* */` 注释(字符串字面量内容保留)。</summary>
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

        /// <summary>44 入向界面的 spy(ADR-018 §一:只入不出,不持有状态)。</summary>
        private sealed class SpyCueSink : IAudioCueSink
        {
            public readonly List<AudioCueDto> Emitted = new List<AudioCueDto>();
            public int LoopBegins;
            public int LoopEnds;

            public void Emit(in AudioCueDto cue) => Emitted.Add(cue);

            public AudioCueHandle BeginLoop(in AudioCueDto cue)
            {
                LoopBegins++;
                return new AudioCueHandle(LoopBegins);
            }

            public void EndLoop(AudioCueHandle handle) => LoopEnds++;

            public void SetTier(TierSource source) { }
        }
    }
}
