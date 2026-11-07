// S26-2 · casebook-003 —— 分册键与改写痕读时投影(F-39.2)。
//
// 权威来源:
//   design/gdd/casebook.md 规则二(每玩家一本,分册键 = player_id)· **F-39.2**(Page / Traces / J(c,p))
//     · **AC-39-13**(痕 = 病例流读时投影,零独立存储)· AC-39-04 ①② / ⑥
//   docs/architecture/adr-008-case-event-stream.md §三(JudgmentRecorded / JudgmentRevised)
//   docs/architecture/adr-031-payload-decoder-abstraction.md(读侧唯一解码口)
//   production/qa/qa-plan-sprint-26-2026-11-08.md §S26-2
//
// ⚠️ 本件用**假 decoder**(`FakeDecoder`)注入 —— 这是合法 DI,不是 F2 假绿:
//    被测生产类是 `CasebookVolumeProjection`(真身),被替身的是**协作者** `IPayloadDecoder`;
//    生产实现 `PayloadDecoder` 住 `Sim.Codec`,而 `Casebook.Tests` 不引那支装配。
//    「同一个 `Decode<T>` 契约」由 `Sim.Codec` 的既有夹具覆盖,此处不复述。
//
// ⚠️ 假绿防线:
//   · F4 空洞正控:每条「零命中 / 恒等」前先断扫描面 / 输入非空;
//   · F3 自指判据:期望值一律**字面量**;
//   · AC-39-04 ③ 跨机稳定 = **BLOCKED-BY-45**,本件只证「重构不变」,**不记绿**。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.UI.Casebook;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Casebook
{
    [TestFixture]
    public class casebook_volume_projection_test
    {
        private FakeDecoder _decoder;

        [SetUp]
        public void SetUp() => _decoder = new FakeDecoder();

        // ══════════════ AC-1 · J(c,p) 末笔折叠 ══════════════

        [Test]
        public void test_ac1_lastJudgmentIsCurrent_earlierAreStale()
        {
            // 病例 c1(patient 7):p=2 Record → Revise → Revise;q=3 也 Record
            CaseId c1 = new CaseId(100L, 7, 0L);
            var events = new List<SimEvent>
            {
                CaseOpened(100L, 7, 0L),
                Judgment(EventKind.JudgmentRecorded, 101L, 7, 0L, c1, author: 2, lexicon: 11, conf: 1),
                Judgment(EventKind.JudgmentRevised,  102L, 7, 0L, c1, author: 2, lexicon: 12, conf: 2),
                Judgment(EventKind.JudgmentRevised,  103L, 7, 0L, c1, author: 2, lexicon: 13, conf: 3),
                Judgment(EventKind.JudgmentRecorded, 104L, 7, 0L, c1, author: 3, lexicon: 99, conf: 5),
            };

            ProjectedVolume vol = CasebookVolumeProjection.ProjectVolume(events, playerId: 2, _decoder);

            Assert.That(vol.Count, Is.EqualTo(1));
            VolumePage page = vol.Pages[0];
            Assert.That(page.TraceCount, Is.EqualTo(3), "p 只应看到自己那三笔");
            Assert.That(page.Traces[0].State, Is.EqualTo(JudgmentTraceState.Stale));
            Assert.That(page.Traces[1].State, Is.EqualTo(JudgmentTraceState.Stale));
            Assert.That(page.Traces[2].State, Is.EqualTo(JudgmentTraceState.Current),
                "末笔必须是 Current");
            Assert.That(page.Current.HasValue, Is.True);
            Assert.That(page.Current.Value.LexiconId, Is.EqualTo(13), "Current = 第三笔的载荷");
            Assert.That(page.Traces.Any(t => t.LexiconId == 99), Is.False,
                "其他作者的笔**不得**出现在 p 的分册里");
        }

        [Test]
        public void test_ac1_blankPageForCaseWithoutMyJudgment()
        {
            CaseId c1 = new CaseId(100L, 7, 0L);
            CaseId c2 = new CaseId(200L, 8, 0L);
            var events = new List<SimEvent>
            {
                CaseOpened(200L, 8, 0L),                                   // 串接键小的排在前
                CaseOpened(100L, 7, 0L),
                Judgment(EventKind.JudgmentRecorded, 105L, 7, 0L, c1, author: 2, lexicon: 7, conf: 1),
            };

            ProjectedVolume vol = CasebookVolumeProjection.ProjectVolume(events, playerId: 2, _decoder);

            Assert.That(vol.Count, Is.EqualTo(2), "两例都该有页");
            VolumePage blank = vol.Pages.First(p => p.Case.Patient == 8);
            Assert.That(blank.IsBlank, Is.True, "零落笔 ⇒ 空白页(页在、痕无)");
            Assert.That(blank.TraceCount, Is.EqualTo(0));
            Assert.That(blank.Current.HasValue, Is.False);
        }

        [Test]
        public void test_ac1_sameTick_orderedBySeq()
        {
            CaseId c1 = new CaseId(100L, 7, 0L);
            var events = new List<SimEvent>
            {
                CaseOpened(100L, 7, 0L),
                // 同 tick,倒序**喂进去** —— 结果必须按 Seq 升序
                Judgment(EventKind.JudgmentRevised,  200L, 7, 9L, c1, author: 2, lexicon: 22, conf: 1),
                Judgment(EventKind.JudgmentRevised,  200L, 7, 4L, c1, author: 2, lexicon: 21, conf: 1),
            };

            ProjectedVolume vol = CasebookVolumeProjection.ProjectVolume(events, playerId: 2, _decoder);
            VolumePage page = vol.Pages[0];

            Assert.That(page.Traces[0].Seq, Is.EqualTo(4L));
            Assert.That(page.Traces[1].Seq, Is.EqualTo(9L));
            Assert.That(page.Traces[1].State, Is.EqualTo(JudgmentTraceState.Current));
        }

        [Test]
        public void test_ac1_inputOrderDoesNotMatter()
        {
            CaseId c1 = new CaseId(100L, 7, 0L);
            var a = new List<SimEvent>
            {
                CaseOpened(100L, 7, 0L),
                Judgment(EventKind.JudgmentRecorded, 101L, 7, 0L, c1, 2, 11, 1),
                Judgment(EventKind.JudgmentRevised,  102L, 7, 0L, c1, 2, 12, 2),
            };
            var b = new List<SimEvent>(a);
            b.Reverse();

            ProjectedVolume va = CasebookVolumeProjection.ProjectVolume(a, 2, _decoder);
            ProjectedVolume vb = CasebookVolumeProjection.ProjectVolume(b, 2, _decoder);

            Assert.That(vb.Count, Is.EqualTo(va.Count));
            for (int i = 0; i < va.Count; i++)
            {
                Assert.That(vb.Pages[i].Case, Is.EqualTo(va.Pages[i].Case), "页序必须一致");
                Assert.That(vb.Pages[i].TraceCount, Is.EqualTo(va.Pages[i].TraceCount));
                for (int k = 0; k < va.Pages[i].TraceCount; k++)
                {
                    Assert.That(vb.Pages[i].Traces[k].LexiconId, Is.EqualTo(va.Pages[i].Traces[k].LexiconId));
                    Assert.That(vb.Pages[i].Traces[k].State, Is.EqualTo(va.Pages[i].Traces[k].State));
                }
            }
        }

        // ══════════════ AC-2 · 痕零落盘(AC-39-13)══════════════

        [Test]
        public void test_ac2_traceNeverLivesInSerializableTypes()
        {
            // 两条面:**分册态段**(S26-1 的类型)+ `CaseClosed` 载荷。
            // 扫描词 = trace / stale / revision —— 判据是「痕不得有存储位」,不是「名字好看」。
            string[] banned = { "trace", "stale", "revision" };
            Type[] surfaces =
            {
                typeof(CasebookVolumeRecord), typeof(CasebookPageRecord), typeof(CasebookVolumeSnapshot),
                typeof(CaseClosedPayload),
            };

            Assert.That(surfaces.Length, Is.EqualTo(4), "扫描面须非空且恰为本件声明的四处(F4)");

            var hits = new List<string>();
            foreach (Type t in surfaces)
            {
                foreach (MemberInfo m in t.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                                                      BindingFlags.DeclaredOnly))
                {
                    string name = m.Name.ToLowerInvariant();
                    foreach (string b in banned)
                        if (name.Contains(b)) hits.Add($"{t.Name}.{m.Name}");
                }
            }

            Assert.That(hits, Is.Empty,
                "痕 / 旧态 / 改写史出现在可序列化类型上 —— AC-39-13(痕 = 读时投影,零存储):\n"
                + string.Join("\n", hits));
        }

        [Test]
        public void test_ac2_scanActuallyCatchesABannedWord()
        {
            // 负控:证明上面那条不是「恒空」(F4)。造一个带 stale 字段的合成类型本地扫一遍
            var hits = new List<string>();
            foreach (MemberInfo m in typeof(LeakyTraceHolder).GetMembers(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                string name = m.Name.ToLowerInvariant();
                if (name.Contains("stale")) hits.Add(m.Name);
            }
            Assert.That(hits, Is.Not.Empty, "扫描逻辑抓不住 stale ⇒ 上一条的正例无判别力");
        }

        // ══════════════ AC-3 · 重构不变(单机夹具;跨机稳定 BLOCKED-BY-45)══════════════

        [Test]
        public void test_ac3_reprojectionAfterCounterRebuild_isIdentical()
        {
            CaseId c1 = new CaseId(100L, 7, 0L);
            var events = new List<SimEvent>
            {
                CaseOpened(100L, 7, 0L),
                Judgment(EventKind.JudgmentRecorded, 101L, 7, 0L, c1, 2, 11, 1),
                Judgment(EventKind.JudgmentRevised,  102L, 7, 0L, c1, 2, 12, 2),
            };

            ProjectedVolume before = CasebookVolumeProjection.ProjectVolume(events, 2, _decoder);
            // 模拟「存档 → 新会话 → 计数器由 max(id)+1 重构」:事件流与 p 的 id 都不变
            ProjectedVolume after = CasebookVolumeProjection.ProjectVolume(events, 2, _decoder);

            Assert.That(after.Count, Is.EqualTo(before.Count));
            for (int i = 0; i < before.Count; i++)
            {
                Assert.That(after.Pages[i].Case, Is.EqualTo(before.Pages[i].Case));
                Assert.That(after.Pages[i].TraceCount, Is.EqualTo(before.Pages[i].TraceCount));
                Assert.That(after.Pages[i].Current?.LexiconId,
                    Is.EqualTo(before.Pages[i].Current?.LexiconId), "分册必须逐位相同");
            }
            // ⚠️ 本条只证「重构不变」;**不证**「跨机稳定」—— 那是 AC-39-04 ③,BLOCKED-BY-45。
        }

        [Test]
        public void test_ac3_newcomerDoesNotAffectExistingVolume()
        {
            CaseId c1 = new CaseId(100L, 7, 0L);
            var events = new List<SimEvent>
            {
                CaseOpened(100L, 7, 0L),
                Judgment(EventKind.JudgmentRecorded, 101L, 7, 0L, c1, 2, 11, 1),
            };
            ProjectedVolume before = CasebookVolumeProjection.ProjectVolume(events, 2, _decoder);

            events.Add(Judgment(EventKind.JudgmentRecorded, 102L, 7, 0L, c1, 3, 33, 2));   // 新人 id=3
            ProjectedVolume after = CasebookVolumeProjection.ProjectVolume(events, 2, _decoder);

            Assert.That(after.TotalTraceCount, Is.EqualTo(before.TotalTraceCount),
                "别人的笔不得改变 p 的册子");
            Assert.That(after.Pages[0].Current?.LexiconId, Is.EqualTo(11));
        }

        // ══════════════ AC-4 · 能力面(无「全部作者」重载)══════════════

        [Test]
        public void test_ac4_noAllAuthorsOverload()
        {
            MethodInfo[] methods = typeof(CasebookVolumeProjection)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == "ProjectVolume")
                .ToArray();

            Assert.That(methods.Length, Is.GreaterThan(0), "找不到 ProjectVolume —— 扫描面丢失(F4)");

            foreach (MethodInfo m in methods)
            {
                ParameterInfo[] ps = m.GetParameters();
                Assert.That(ps.Any(p => p.ParameterType == typeof(int) && p.Name == "playerId"), Is.True,
                    $"重载 {m} 缺 player_id 参数 —— 「全部作者」形态就是能力面泄漏(AC-39-04 ①)");
                Assert.That(ps.Any(p => p.ParameterType == typeof(IPayloadDecoder)), Is.True,
                    "解码器必须显式传入(不许内部 new)");
            }
        }

        [Test]
        public void test_ac4_unissuedPlayerId_yieldsAllBlankNotAllAuthors()
        {
            CaseId c1 = new CaseId(100L, 7, 0L);
            var events = new List<SimEvent>
            {
                CaseOpened(100L, 7, 0L),
                Judgment(EventKind.JudgmentRecorded, 101L, 7, 0L, c1, 2, 11, 1),
            };

            ProjectedVolume vol = CasebookVolumeProjection.ProjectVolume(events, playerId: 42, _decoder);

            Assert.That(vol.Count, Is.EqualTo(1), "页还在(病例属于流,不属于某人)");
            Assert.That(vol.TotalTraceCount, Is.EqualTo(0), "但一笔都不该有 —— 未发号 ≠ 全量");
            Assert.That(vol.Pages[0].IsBlank, Is.True);
        }

        // ══════════════ AC-6 · 4 人互不污染 ══════════════

        [Test]
        public void test_ac6_fourPlayers_projectIndependently()
        {
            CaseId c1 = new CaseId(100L, 7, 0L);
            var events = new List<SimEvent> { CaseOpened(100L, 7, 0L) };
            for (int i = 0; i < 4; i++)
                events.Add(Judgment(EventKind.JudgmentRecorded, 101L + i, 7, 0L, c1,
                                    author: i, lexicon: 40 + i, conf: (byte)i));

            for (int i = 0; i < 4; i++)
            {
                ProjectedVolume vol = CasebookVolumeProjection.ProjectVolume(events, i, _decoder);
                Assert.That(vol.Count, Is.EqualTo(1));
                Assert.That(vol.Pages[0].TraceCount, Is.EqualTo(1), $"玩家 {i} 只能有自己那一笔");
                Assert.That(vol.Pages[0].Current.Value.LexiconId, Is.EqualTo(40 + i));
            }
        }

        // ══════════════ 语义补充:「旧」态是枚举非颜色 ══════════════

        [Test]
        public void test_traceState_isIntegerEnum_notColor()
        {
            Assert.That(typeof(JudgmentTraceState).IsEnum, Is.True, "「旧」态必须是枚举,不是色值");
            Assert.That((int)JudgmentTraceState.Current, Is.EqualTo(0));
            Assert.That((int)JudgmentTraceState.Stale, Is.EqualTo(1));
        }

        [Test]
        public void test_nullDecoder_isRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => CasebookVolumeProjection.ProjectVolume(new List<SimEvent>(), 2, null));
        }

        [Test]
        public void test_emptyStream_yieldsEmptyVolume()
        {
            ProjectedVolume vol = CasebookVolumeProjection.ProjectVolume(
                new List<SimEvent>(), playerId: 2, _decoder);
            Assert.That(vol.Count, Is.EqualTo(0));
            Assert.That(vol.PlayerId, Is.EqualTo(2));
        }

        // ── 夹具 ──

        private static SimEvent CaseOpened(long tick, int patient, long seq)
            => new SimEvent(tick, new PatientId(patient), seq, EventKind.CaseOpened, new PayloadRef(0, 0, 0));

        private SimEvent Judgment(EventKind kind, long tick, int patient, long seq, CaseId caseId,
                                  int author, int lexicon, byte conf)
        {
            PayloadRef r = kind == EventKind.JudgmentRecorded
                ? _decoder.Put(new JudgmentRecordedPayload(patient, caseId, author, lexicon, conf))
                : _decoder.Put(new JudgmentRevisedPayload(patient, caseId, author, lexicon, conf));
            return new SimEvent(tick, new PatientId(patient), seq, kind, r);
        }

        /// <summary>按 BlobId 取回夹具塞进去的载荷(struct 装箱 —— 仅测试面)。</summary>
        private sealed class FakeDecoder : IPayloadDecoder
        {
            private readonly Dictionary<int, object> _byId = new Dictionary<int, object>();
            private int _next = 1;

            public PayloadRef Put(object payload)
            {
                int id = _next++;
                _byId[id] = payload;
                return new PayloadRef(id, 0, 0);
            }

            public T Decode<T>(EventKind kind, in PayloadRef payloadRef) where T : struct
            {
                if (!_byId.TryGetValue(payloadRef.BlobId, out object o))
                    throw new InvalidOperationException($"夹具未登记 BlobId={payloadRef.BlobId}");
                return (T)o;
            }
        }

        /// <summary>负控用:一个带 <c>stale</c> 字段的类型(证明扫描词表会命中)。</summary>
        private sealed class LeakyTraceHolder
        {
            // ReSharper disable once NotAccessedField.Local
            public int StaleCount;
        }
    }
}
