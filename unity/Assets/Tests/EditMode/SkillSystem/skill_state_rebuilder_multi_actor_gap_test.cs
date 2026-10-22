// 技能重建的**多玩家语义空白**判据(2026-10-23 round31)。
//
// ── 本文件为什么存在 ──────────────────────────────────────────────────────────
// story-008(`SkillStateRebuilder.RebuildLevels`)的 6 条 AC **全部按单玩家书写**,
// 而本项目是 **1–4 人联机合作**(`technical-preferences.md:31`「联机从 P0 起预留」)。
//
// `SkillGrownPayload` **带 `ActorId`**(`HistoryPayloads.cs:251`「施予者 = 玩家,
// 与 player_id 共用 id 空间」),但 `RebuildLevels` 的实现是:
//
//     result[e.SkillId] = e.Level;      // ← 完全不看 e.ActorId
//
// ⇒ **两个玩家练同一技能,等级会互相覆盖**。谁的事件在流里靠后,谁的值胜出。
//
// ── 为什么本文件不「修」它 ────────────────────────────────────────────────────
// 修 = 改 `RebuildLevels` 的返回形状(`Dictionary<int,int>` → 按 actor 分组)。
// 而该签名在 story-008 §Implementation 第 1 条被**逐字钉死**,且 story 状态 = `Complete`。
// 改它 = 推翻一条已完成 story 的接口契约,且 ADR 对「多玩家技能重建」**零字**
// (`adr-026` 全文无 multi-player 相关表述;`adr-006` 只定义 `player_id` 发号机制,
//  未定义技能重建的 actor 维度)。
// ⇒ 这是**裁定空白**,不是实现缺陷 —— 归用户裁,不由实现方自行消解。
//
// ── 本文件做什么 ──────────────────────────────────────────────────────────────
// 把「当前行为是什么」钉成**可执行的事实**,而不是留在散文里:
//   ① 证明覆盖确实发生(红不了,但**能读到具体值**,将来改了就红);
//   ② 证明 `ActorId` 在载荷里**确实有值**(不是恒 0 的摆设 —— 否则「忽略它」无害);
//   ③ 把「单玩家下结果正确」也钉住,防止有人为了多玩家把单玩家改坏。
//
// ⚠️ **本文件全绿不等于「多玩家已支持」** —— 恰恰相反,它记录的是**不支持**。
//    判据 ① 的断言写的是「当前实现下 B 覆盖 A」这个**事实**,不是「这是对的」。
//    将来若有人实现了按 actor 分组,① 会红 —— 那时应**改写**本文件为正向判据,
//    而不是删掉它。
//
// 权威来源:
//   `production/epics/skill-system/story-008-persistence-and-stream-rebuild.md`
//     §Implementation 1(签名)· §AC-1…6(全单玩家)
//   `unity/Assets/Sim.Contracts/SkillSystem/SkillStateRebuilder.cs`
//   `unity/Assets/Sim.Contracts/Payloads/HistoryPayloads.cs:248-263`(ActorId 字段)
//   `.claude/docs/technical-preferences.md:31`(1–4 人联机,架构从 P0 预留)
//   ADR-006 Amendment(2026-09-21):`player_id` 复用 `IIdAuthority` 机制 A

using System.Collections.Generic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    /// <summary>
    /// 多玩家场景下 <see cref="SkillStateRebuilder"/> 的**行为记录**。
    /// </summary>
    /// <remarks>
    /// ⚠️ 本类断言的是「当前实现的真实行为」,不是「期望的产品行为」。
    /// 它存在的目的是让**语义空白**可执行、可发现,而非声明该行为正确。
    /// </remarks>
    [TestFixture]
    internal sealed class SkillStateRebuilderMultiActorGapTest
    {
        private const int ActorA = 1;
        private const int ActorB = 2;
        private const int DiagnosticSkill = (int)SkillId.诊断;

        private static SkillGrownPayload Grown(int actorId, int skillId, int level)
            => new SkillGrownPayload(actorId, -1, skillId, 1, 0, level);

        // ══════════════════════════════════════════════════════════════════
        // ① 覆盖确实发生(把缺口钉成事实)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>核心判据</b>:两个玩家练同一技能 ⇒ 结果里**只剩一个值**,且是流里靠后的那个。
        /// </summary>
        /// <remarks>
        /// <b>这条断言的是「当前会覆盖」这个事实</b>,不是「覆盖是对的」。
        /// 将来若按 actor 分组实现,本条会红 —— 那时应改写为正向判据。
        /// <para><b>为什么值得钉</b>:没有本条时,「多玩家互相覆盖」只存在于读代码的人脑中;
        /// 有了本条,任何改动只要碰到这条路径都会在这里显形。</para>
        /// </remarks>
        [Test]
        public void test_multiActor_sameSkill_laterActorOverwritesEarlier_currentBehavior()
        {
            var events = new[]
            {
                Grown(ActorA, DiagnosticSkill, level: 10),   // A 练到 10
                Grown(ActorB, DiagnosticSkill, level: 3),    // B 练到 3(流里靠后)
            };

            Dictionary<int, int> levels = SkillStateRebuilder.RebuildLevels(events);

            Assert.That(levels.ContainsKey(DiagnosticSkill), Is.True, "技能键存在");
            Assert.That(levels.Count, Is.EqualTo(1),
                "**当前实现**:一个技能只有一个等级槽 —— 两个玩家共用 ⇒ 只剩 1 个键");
            Assert.That(levels[DiagnosticSkill], Is.EqualTo(3),
                "**当前实现**:靠后的事件胜出 ⇒ B(3) 覆盖了 A(10)。" +
                "⚠️ 这不是期望行为,是**语义空白**的现状(见文件头);" +
                "多玩家支持须先裁 ADR(改返回形状 = 改 story-008 已钉死的签名)");
        }

        /// <summary>
        /// 反方向再钉一次:调换流序 ⇒ 结果跟着变。证明「谁赢」纯粹由**流序**决定,
        /// 与 actor 身份无关 —— 这正是「忽略 ActorId」的直接后果。
        /// </summary>
        [Test]
        public void test_multiActor_resultFollowsStreamOrder_notActorIdentity()
        {
            var aThenB = new[] { Grown(ActorA, DiagnosticSkill, 10), Grown(ActorB, DiagnosticSkill, 3) };
            var bThenA = new[] { Grown(ActorB, DiagnosticSkill, 3), Grown(ActorA, DiagnosticSkill, 10) };

            Assert.That(SkillStateRebuilder.RebuildLevels(aThenB)[DiagnosticSkill], Is.EqualTo(3),
                "A→B 序 ⇒ B 的值胜出");
            Assert.That(SkillStateRebuilder.RebuildLevels(bThenA)[DiagnosticSkill], Is.EqualTo(10),
                "B→A 序 ⇒ A 的值胜出。" +
                "**同一个技能、同两个玩家,仅因流序不同就得到不同结果** —— " +
                "这是「按 SkillId 单槽」的必然结果,不是 bug 修复能掩盖的");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② ActorId 确实有值(否则「忽略它」无害)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 前提:`ActorId` 在载荷里**确实承载区分信息**,不是恒 0 的摆设。
        /// <para>若 ActorId 恒 0,「RebuildLevels 忽略它」就是无害的;正因为它是真值,
        /// 忽略才构成覆盖。本条把「忽略是有代价的」这个前提钉住。</para>
        /// </summary>
        [Test]
        public void test_payload_actorId_carriesRealDistinction_premise()
        {
            var a = Grown(ActorA, DiagnosticSkill, 10);
            var b = Grown(ActorB, DiagnosticSkill, 10);

            Assert.That(a.ActorId, Is.Not.EqualTo(b.ActorId),
                "两个玩家的 ActorId 必须可区分 —— 否则「忽略 ActorId」无害,本文件也就没有存在意义");
            Assert.That(a.ActorId, Is.EqualTo(ActorA), "ActorId 原样保留(ADR-006:与 player_id 共用 id 空间)");
            Assert.That(b.ActorId, Is.EqualTo(ActorB));

            // 且编码/解码后仍保留 —— 不是内存里的错觉
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);

            PayloadRef r = encoder.Encode(EventKind.SkillGrown, a);
            SkillGrownPayload back = decoder.Decode<SkillGrownPayload>(EventKind.SkillGrown, r);

            Assert.That(back.ActorId, Is.EqualTo(ActorA),
                "ActorId 经真 codec round-trip 后仍保留 ⇒ 它在**流里**有值,不是只在内存里");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 单玩家下结果正确(防「为了多玩家把单玩家改坏」)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 单玩家语义**必须保持**:这是 story-008 AC-1…AC-6 的基础面。
        /// 将来实现多玩家分组时,本条的绿不得被牺牲。
        /// </summary>
        [Test]
        public void test_singleActor_semanticsUnchanged_guard()
        {
            var events = new[]
            {
                Grown(ActorA, DiagnosticSkill, 1),
                Grown(ActorA, DiagnosticSkill, 2),
                Grown(ActorA, (int)SkillId.急救, 1),
            };

            Dictionary<int, int> levels = SkillStateRebuilder.RebuildLevels(events);

            Assert.That(levels[(int)SkillId.诊断], Is.EqualTo(2), "单玩家:取最新等级");
            Assert.That(levels[(int)SkillId.急救], Is.EqualTo(1));
            Assert.That(levels.Count, Is.EqualTo(2), "单玩家:两技能两键");
        }
    }
}
