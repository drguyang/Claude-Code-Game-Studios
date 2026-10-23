// 技能重建的**多玩家语义**判据 —— 正向(2026-10-23 用户裁定「改签名」后重写)。
//
// ── 本文件的前身与它为什么被改写 ──────────────────────────────────────────────
// round31 立了 `skill_state_rebuilder_multi_actor_gap_test.cs`,记录的是**缺口**:
// `RebuildLevels` 返回 `Dictionary<int,int>` 且实现 `result[e.SkillId] = e.Level`
// **完全不看 `e.ActorId`** ⇒ 两个玩家练同一技能互相覆盖,「谁赢」纯由流序决定。
// 那时判据 ①② 断言的是「当前会覆盖」这个**事实**(不是「覆盖是对的」),并在文件头
// 明写「将来若按 actor 分组实现,①② 会红 —— 那时应**改写**为正向判据,不是删掉」。
//
// **2026-10-23 用户裁定「改签名」** ⇒ 现在就是那个「将来」。本文件即改写后的正向版:
// 断言的是「两玩家各自独立、互不覆盖」这个**期望行为**。
//
// ── 裁定的依据(为什么改签名而不是新开方法)───────────────────────────────────
// 生产契约 `ISkillGrowthPort.QueryLevel(int actorId, int skillId)`(`PrescribeFlow.cs:99-107`)
// **本来就是 actor 维度的**,而 `RebuildLevels` 返回单层 `skillId → level` 把它丢了 ——
// 契约与重建**不匹配**。两条路径并存会让 7a 有机会用错那条,故取**唯一路径**。
// 形状参数序与 `QueryLevel(actorId, skillId)` 对齐 ⇒ 消费方 `levels[actorId][skillId]` 直接可喂。
//
// ── 判据清单(全部为「修前红 / 修后绿」的真判据)────────────────────────────────
//   ① 两玩家同技能 ⇒ 各自独立,互不覆盖
//   ② 流序**不再**决定结果(与 ① 互为反证:① 证明不覆盖,② 证明与序无关)
//   ③ 前提:`ActorId` 在载荷里确实承载区分信息(否则「分组」无意义)
//   ④ 单玩家语义**保持不变**(防「为了多玩家把单玩家改坏」)
//   ⑤ 某 actor 无有效成长 ⇒ 该 actor 不出现(消费方按「查无 = 0 级」,与 AC-6 一致)
//
// 权威来源:
//   `production/epics/skill-system/story-008-persistence-and-stream-rebuild.md`
//     §Implementation 1(签名 —— 2026-10-23 按裁定同步更新为双层字典)
//   `unity/Assets/Sim.Contracts/SkillSystem/SkillStateRebuilder.cs`
//   `unity/Assets/Sim/Prescription/PrescribeFlow.cs:99-107`(QueryLevel 已是 actor 维度)
//   `.claude/docs/technical-preferences.md:31`(1–4 人联机,架构从 P0 预留)
//   ADR-006 Amendment B(2026-09-21):`player_id` 复用 `IIdAuthority` 机制 A

using System.Collections.Generic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    /// <summary>
    /// 多玩家场景下 <see cref="SkillStateRebuilder"/> 的**期望行为**(2026-10-23 裁定后正向判据)。
    /// </summary>
    [TestFixture]
    internal sealed class SkillStateRebuilderMultiActorGapTest
    {
        private const int ActorA = 1;
        private const int ActorB = 2;
        private const int DiagnosticSkill = (int)SkillId.诊断;

        private static SkillGrownPayload Grown(int actorId, int skillId, int level)
            => new SkillGrownPayload(actorId, -1, skillId, 1, 0, level);

        // ══════════════════════════════════════════════════════════════════
        // ① 两玩家同技能 ⇒ 各自独立,互不覆盖
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>核心判据</b>:两个玩家练同一技能 ⇒ 结果里**两人各有一个键**,值互不影响。
        /// </summary>
        /// <remarks>
        /// <b>修前红</b>:旧实现返回单层字典 ⇒ <c>levels.Count == 1</c> 且 B 覆盖 A。
        /// 本判据当场红(编译期甚至不过 —— 索引形状已变)。
        /// </remarks>
        [Test]
        public void test_multiActor_sameSkill_eachActorKeepsOwnLevel()
        {
            var events = new[]
            {
                Grown(ActorA, DiagnosticSkill, level: 10),   // A 练到 10
                Grown(ActorB, DiagnosticSkill, level: 3),    // B 练到 3(流里靠后)
            };

            Dictionary<int, Dictionary<int, int>> levels = SkillStateRebuilder.RebuildLevels(events);

            Assert.That(levels.Count, Is.EqualTo(2),
                "两个玩家 ⇒ 两个 actor 键(修前:单层字典只有 1 个技能键,互相覆盖)");
            Assert.That(levels[ActorA][DiagnosticSkill], Is.EqualTo(10),
                "A 的等级 = 10 —— **不被 B 的靠后事件覆盖**");
            Assert.That(levels[ActorB][DiagnosticSkill], Is.EqualTo(3),
                "B 的等级 = 3 —— 与 A 各自独立");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 流序不再决定结果
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 调换流序 ⇒ 结果**完全相同**。
        /// <para>这是 ① 的反证面:修前「谁赢」纯由流序决定(同一技能同两玩家,
        /// A→B 得 3、B→A 得 10);修后两人各自成键,序无关。</para>
        /// </summary>
        [Test]
        public void test_multiActor_resultIndependentOfStreamOrder()
        {
            var aThenB = new[] { Grown(ActorA, DiagnosticSkill, 10), Grown(ActorB, DiagnosticSkill, 3) };
            var bThenA = new[] { Grown(ActorB, DiagnosticSkill, 3), Grown(ActorA, DiagnosticSkill, 10) };

            var r1 = SkillStateRebuilder.RebuildLevels(aThenB);
            var r2 = SkillStateRebuilder.RebuildLevels(bThenA);

            Assert.That(r1[ActorA][DiagnosticSkill], Is.EqualTo(10), "A→B 序:A = 10");
            Assert.That(r1[ActorB][DiagnosticSkill], Is.EqualTo(3), "A→B 序:B = 3");
            Assert.That(r2[ActorA][DiagnosticSkill], Is.EqualTo(10), "B→A 序:A 仍 = 10(序无关)");
            Assert.That(r2[ActorB][DiagnosticSkill], Is.EqualTo(3), "B→A 序:B 仍 = 3(序无关)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 前提:ActorId 确实有值
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 前提:`ActorId` 在载荷里**确实承载区分信息**,且经真 codec round-trip 后仍保留。
        /// <para>若 ActorId 恒 0,分组就无从谈起 —— 本条钉住「分组有真实依据」。</para>
        /// </summary>
        [Test]
        public void test_payload_actorId_carriesRealDistinction_premise()
        {
            var a = Grown(ActorA, DiagnosticSkill, 10);
            var b = Grown(ActorB, DiagnosticSkill, 10);

            Assert.That(a.ActorId, Is.Not.EqualTo(b.ActorId),
                "两个玩家的 ActorId 必须可区分 —— 否则分组无意义");
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
        // ④ 单玩家语义保持不变(防「为了多玩家把单玩家改坏」)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 单玩家语义**必须保持**:这是 story-008 AC-1…AC-6 的基础面。
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

            Dictionary<int, Dictionary<int, int>> levels = SkillStateRebuilder.RebuildLevels(events);

            Assert.That(levels.Count, Is.EqualTo(1), "单玩家 ⇒ 一个 actor 键");
            Assert.That(levels[ActorA][(int)SkillId.诊断], Is.EqualTo(2), "单玩家:取最新等级");
            Assert.That(levels[ActorA][(int)SkillId.急救], Is.EqualTo(1));
            Assert.That(levels[ActorA].Count, Is.EqualTo(2), "单玩家:两技能两键");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 某 actor 无有效成长 ⇒ 该 actor 不出现
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// A 只有哨兵行、B 有真实成长 ⇒ 结果**只含 B**。
        /// <para>消费方按「查无 = 0 级」处理(<c>QueryLevel</c> 语义 / AC-6),
        /// 故不出现与「0 级」等价,不额外造一个空内层字典。</para>
        /// </summary>
        [Test]
        public void test_actorWithoutValidGrowth_absentFromResult()
        {
            var events = new[]
            {
                Grown(ActorA, DiagnosticSkill, SkillGrownPayload.LevelNotGrown), // A 只有哨兵
                Grown(ActorB, DiagnosticSkill, 7),                               // B 真实成长
            };

            Dictionary<int, Dictionary<int, int>> levels = SkillStateRebuilder.RebuildLevels(events);

            Assert.That(levels.ContainsKey(ActorA), Is.False,
                "A 无有效成长 ⇒ 不出现(消费方按「查无 = 0 级」,与 AC-6 一致)");
            Assert.That(levels[ActorB][DiagnosticSkill], Is.EqualTo(7), "B 正常重建");
        }
    }
}
