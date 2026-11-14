// 脚本产病人通道夹具(2026-11-14)。
//
// 权威:`design/gdd/content/chapter-plan.md` §0.2 + §0.4 · `ScriptedPatientSpawner` 文件头
//
// ═══ 它钉的三件事 ═════════════════════════════════════════════════════════
//   ① **真的写进流、真的登记在场** —— 用**真的** `PatientSpawner` + **真的** `PresenceRegistry`,
//      不用替身。「事件有了、世界没变」(漏 `AddPresent`)是本类最危险也最安静的失败。
//   ② **没有节奏门** —— 同一 tick 连产两人必须都成立。`PatientAppearedDriver` 会拒第二次
//      (`SpawnIntervalTicks`),故这条同时证明本通道**不是**那条路径的别名。
//   ③ **参数原样送达** —— 病种 id 与 tick 不许被截断/替换(用记录器直接看形参)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>脚本产病人通道:创建 + 登记 + 无节奏门 + 参数直送。</summary>
    [TestFixture]
    public class scripted_patient_spawn_test
    {
        private sealed class RecordingSink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
        }

        // ── ① 真的写进流 + 真的登记在场 ─────────────────────────────────────

        /// <summary>产出一个病人:`DiseaseOnset` 落流,且在场登记到指定格。</summary>
        [Test]
        public void test_spawn_writesOnset_andRegistersPresence()
        {
            var pool = new InMemoryBlobPool();
            var sink = new RecordingSink();
            var spawner = new PatientSpawner(new IdAuthority(), sink, new PayloadEncoder(pool), 12345UL);
            var presence = new PresenceRegistry();
            var scripted = new ScriptedPatientSpawner(spawner.SpawnNext, presence, () => 10L);

            var cell = new WorldPos(106, 0, 15);
            PatientId id = scripted.Spawn(3, cell);

            Assert.That(sink.Events.Count, Is.EqualTo(1), "须恰好写一条病史流事件");
            Assert.That(sink.Events[0].Kind, Is.EqualTo(EventKind.DiseaseOnset));
            Assert.That(sink.Events[0].Patient.Value, Is.EqualTo(id.Value), "事件与返回值须是同一个病人");

            Assert.That(presence.IsPresent(id), Is.True, "漏 `AddPresent` ⇒ 13 看不见、9 不 Step、37 不开案");
            Assert.That(presence.TryGetCell(id, out WorldPos got), Is.True);
            Assert.That(got, Is.EqualTo(cell));
        }

        // ── ② 没有节奏门 ────────────────────────────────────────────────────

        /// <summary>同一 tick 连产两人 ⇒ 两个病人都成立(环境路径会拒第二次)。</summary>
        [Test]
        public void test_spawn_twiceInSameTick_bothAppear()
        {
            var pool = new InMemoryBlobPool();
            var sink = new RecordingSink();
            var spawner = new PatientSpawner(new IdAuthority(), sink, new PayloadEncoder(pool), 12345UL);
            var presence = new PresenceRegistry();
            var scripted = new ScriptedPatientSpawner(spawner.SpawnNext, presence, () => 10L);

            PatientId a = scripted.Spawn(3, new WorldPos(106, 0, 15));
            PatientId b = scripted.Spawn(4, new WorldPos(108, 0, 16));

            Assert.That(sink.Events.Count, Is.EqualTo(2), "脚本通道不得有节奏门 —— 同一 tick 两人都要出现");
            Assert.That(a.Value, Is.Not.EqualTo(b.Value), "两名病人须拿到不同 id");
            Assert.That(presence.PresentCount, Is.EqualTo(2));
        }

        /// <summary>各人登记在各自的格上(不是恒同一格)。</summary>
        [Test]
        public void test_spawn_registersEachAtItsOwnCell()
        {
            var presence = new PresenceRegistry();
            var cells = new List<WorldPos>();
            var scripted = new ScriptedPatientSpawner(
                (d, t) => { cells.Add(new WorldPos(100 + d, 0, 15)); return new PatientId(100 + d); },
                presence, () => 1L);

            var c1 = new WorldPos(106, 0, 15);
            var c2 = new WorldPos(108, 0, 16);
            scripted.Spawn(1, c1);
            scripted.Spawn(2, c2);

            Assert.That(presence.TryGetCell(new PatientId(101), out WorldPos g1), Is.True);
            Assert.That(presence.TryGetCell(new PatientId(102), out WorldPos g2), Is.True);
            Assert.That(g1, Is.EqualTo(c1));
            Assert.That(g2, Is.EqualTo(c2));
        }

        // ── ③ 参数直送 ──────────────────────────────────────────────────────

        /// <summary>显式 tick 重载:病种 id 与 tick 原样交给创建器。</summary>
        [Test]
        public void test_spawn_forwardsDiseaseId_andTick()
        {
            var seen = new List<(int Disease, long Tick)>();
            var presence = new PresenceRegistry();
            var scripted = new ScriptedPatientSpawner(
                (d, t) => { seen.Add((d, t)); return new PatientId(7); },
                presence, () => -999L);          // 若误用时钟,下面会看到 -999

            scripted.Spawn(5, new WorldPos(1, 0, 2), 42L);

            Assert.That(seen.Count, Is.EqualTo(1));
            Assert.That(seen[0].Disease, Is.EqualTo(5));
            Assert.That(seen[0].Tick, Is.EqualTo(42L), "显式 tick 须原样送达,不得被时钟覆盖");
        }

        /// <summary>省略 tick 时读时钟(节拍动作没有 tick 形参)。</summary>
        [Test]
        public void test_spawn_usesClock_whenTickOmitted()
        {
            long seenTick = -1;
            var presence = new PresenceRegistry();
            var scripted = new ScriptedPatientSpawner(
                (d, t) => { seenTick = t; return new PatientId(1); },
                presence, () => 77L);

            scripted.Spawn(0, new WorldPos(0, 0, 0));

            Assert.That(seenTick, Is.EqualTo(77L));
        }

        // ── ④ fail-closed ───────────────────────────────────────────────────

        /// <summary>负病种 / 负 tick 拒收 —— 调用方给的不是用户输入,越界即抛。</summary>
        [Test]
        public void test_spawn_invalidArgs_throw()
        {
            var presence = new PresenceRegistry();
            var scripted = new ScriptedPatientSpawner(
                (d, t) => new PatientId(1), presence, () => 1L);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => scripted.Spawn(-1, new WorldPos(0, 0, 0)), "负病种 id 须拒");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => scripted.Spawn(0, new WorldPos(0, 0, 0), -5L), "负 tick 须拒");
        }

        /// <summary>任一依赖为 null ⇒ 构造期就炸(装配漏传须可见)。</summary>
        [Test]
        public void test_ctor_rejectsNulls()
        {
            var presence = new PresenceRegistry();
            Assert.Throws<ArgumentNullException>(
                () => new ScriptedPatientSpawner(null, presence, () => 1L));
            Assert.Throws<ArgumentNullException>(
                () => new ScriptedPatientSpawner((d, t) => new PatientId(1), null, () => 1L));
            Assert.Throws<ArgumentNullException>(
                () => new ScriptedPatientSpawner((d, t) => new PatientId(1), presence, null));
        }

        /// <summary>
        /// 创建器抛 ⇒ 在场登记**不得**发生(不留下「在册但流里没有」的幽灵)。
        /// <para>⚠️ 这条防的是把 `AddPresent` 挪到 `SpawnNext` **之前**的改法:
        /// 那样创建失败会留下一个只存在于在场表里的 id,而 `EventStream` 永远不会有它的 event。</para>
        /// </summary>
        [Test]
        public void test_coreThrows_noPresenceRegistered()
        {
            var presence = new PresenceRegistry();
            var scripted = new ScriptedPatientSpawner(
                (d, t) => throw new InvalidOperationException("CAP 满"),
                presence, () => 1L);

            Assert.Throws<InvalidOperationException>(
                () => scripted.Spawn(0, new WorldPos(1, 0, 1)));
            Assert.That(presence.PresentCount, Is.EqualTo(0), "创建失败时不得登记在场");
        }
    }
}
