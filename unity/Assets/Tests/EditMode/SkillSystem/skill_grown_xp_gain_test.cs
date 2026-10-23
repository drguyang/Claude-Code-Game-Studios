// XP 余量可重建性判据 —— SkillGrownPayload.XpGain(2026-10-23 round38 · 用户裁定「丙」)。
//
// ── 病灶(本轮修的空白) ─────────────────────────────────────────────────────
// `SkillProgression.TryLevelUp(ref int currentLevel, ref Fix currentXp, ...)` 需要一个
// **可变 XP 累加器**,而全案零持有者。`SkillGrownPayload` 改前的全部字段 =
//   {ActorId, PatientId, SkillId, ObjectId, NoveltyClass, Level}
// ⇒ `XP_gain = BASE × K_difficulty × K_novelty` 的三因子里,载荷只覆盖两个:
//     BASE         ← SkillId 查 SkillRegistry   ✓ 可复得
//     K_novelty    ← NoveltyClass 查系数表       ✓ 可复得
//     K_difficulty ← **零字节**                  ✗ 不可复得
// 玩家可感后果:练到 99% 读档 ⇒ 回到该级 0%(今天不显形仅因 TryLevelUp 零生产调用)。
//
// ── 裁定(丙)与它不做什么 ────────────────────────────────────────────────────
// 用户 2026-10-23 裁定 **丙 = 扩载荷携带本次所得 XP**(而非独立快照 / 从三源重建)。
//   · 甲(独立快照)被否:违 ADR-026 §⑥「少一份可失步的状态」精神。
//   · 乙(从三源重建)被否:须新登记跨流关联键(「哪条 JudgmentRecorded /
//     DrugTreatmentApplied 对应哪条 SkillGrown」全案零定义),且重建 O(n)。
//   · 丙:改编码器 + schema,非纯加法 —— 已按 ADR-010 §七 评估;
//     因 **SaveService 零生产调用、存档尚未出货**,用户裁定 **不升 SaveVersion**
//     (同 ADR-010 `:428`「第一次就做对」口径)。
//
// ── 本文件判什么(每条都必须是「修前红 / 修后绿」的真判据) ─────────────────────
//   ① 编码往返后 XpGain **逐位保留**(修前:无此字段 ⇒ 编译不过,等价红)
//   ② **缺 tag 7 ⇒ 抛**(掩码从 6 位扩到 7 位 —— 防「旧解码器静默接受半截载荷」)
//   ③ 发射器 **8 参重载** 把调用方给的 xpGain **原样写入载荷**(不重算、不夹取)
//   ④ 发射器 **7 参便捷重载** 按基准难度 1.0 算出 BASE × K_novelty(丙-3 口径)
//   ⑤ 生产端口 `AssemblySkillGrowthPort` 真的**消费了 kDifficulty**(修前「算了又丢」)
//   ⑥ 负 xpGain **fail-loud**(不静默接受,防调用方算错时把脏值写进真源)
//
// ⚠️ 判据 ⑤ 是本次「算了又丢」的直接钉死 —— 它必须从**公开端口**驱动,
//    不能只测 SkillGrownEmitter(那样测不到端口的丢弃行为)。
//
// 权威来源:
//   design/gdd/skill-system.md `:347-353`(XP_gain 公式 / K_difficulty 区间 1.0~3.0)
//     `:494`(错诊 ×0.5 —— 丙-3 的具名债 `30-诊断错诊难度未落`)
//   ADR-026 §⑥(不独立快照 / 从流重构) · ADR-006 §五(Fix 经 FixCodec)
//   design/registry/entities.yaml `SimEvent.Kind.SkillGrown` 的 payload_schema
//   unity/Assets/Sim.Codec/PayloadCodec.History.cs(SkillGrown 编解码)

using System;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    /// <summary>
    /// `SkillGrownPayload.XpGain` 的往返 / 掩码 / 发射 / 生产消费判据(丙案 round38)。
    /// </summary>
    [TestFixture]
    internal sealed class SkillGrownXpGainTest
    {
        // ── 外部真值:一律经 FixParse(ADR-006 §五),不写浮点字面量 ──
        private static readonly Fix XpTen = FixParse.Parse("10");
        private static readonly Fix XpHalf = FixParse.Parse("1/2");

        // ══════════════════════════════════════════════════════════════════
        // ① 编码往返:XpGain 逐位保留
        // ══════════════════════════════════════════════════════════════════

        /// <summary>编解码往返后 <c>XpGain</c> 的 raw **逐位**相同(不是「非零」,是精确值)。</summary>
        [Test]
        public void test_xpGain_survivesCodecRoundTrip_bitExact()
        {
            var payload = new SkillGrownPayload(
                actorId: 7, patientId: 3, skillId: (int)SkillId.诊断, objectId: 12,
                noveltyClass: (int)NoveltyClass.First, level: 4, xpGain: XpTen);

            byte[] bytes = PayloadCodec.Encode(payload);
            SkillGrownPayload back = PayloadCodec.Decode<SkillGrownPayload>(
                EventKind.SkillGrown, bytes);

            Assert.That(back.XpGain.Raw, Is.EqualTo(XpTen.Raw),
                "XpGain 须逐位往返。真值 raw = " + XpTen.Raw + ",实测 = " + back.XpGain.Raw);
            // 其余 6 字段同时复核 —— 加字段不得挤坏既有布局
            Assert.That(back.ActorId, Is.EqualTo(7), "ActorId");
            Assert.That(back.PatientId, Is.EqualTo(3), "PatientId");
            Assert.That(back.SkillId, Is.EqualTo((int)SkillId.诊断), "SkillId");
            Assert.That(back.ObjectId, Is.EqualTo(12), "ObjectId");
            Assert.That(back.NoveltyClass, Is.EqualTo((int)NoveltyClass.First), "NoveltyClass");
            Assert.That(back.Level, Is.EqualTo(4), "Level");
        }

        /// <summary>小数级 XP(1/2)同样逐位往返 —— 防「只在大整数上碰巧对」。</summary>
        [Test]
        public void test_xpGain_fractionalValue_survivesRoundTrip()
        {
            var payload = new SkillGrownPayload(1, -1, 0, 1, 0, -1, XpHalf);
            SkillGrownPayload back = PayloadCodec.Decode<SkillGrownPayload>(
                EventKind.SkillGrown, PayloadCodec.Encode(payload));

            Assert.That(back.XpGain.Raw, Is.EqualTo(XpHalf.Raw),
                "1/2 的 raw 须逐位往返(真值 " + XpHalf.Raw + ",实测 " + back.XpGain.Raw + ")");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 掩码:缺 tag 7 ⇒ 抛(不是「缺省 0」)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 截掉 tag 7 的字节流 ⇒ <b>必须抛</b>。
        /// <para>这一条守的是「掩码从 6 位扩到 7 位」这件事本身 —— 若掩码没扩,
        /// 6 tag 的旧载荷会被当成**合法**解码,`XpGain` 静默变 0,
        /// 于是「读档后 XP 余量归零」这个病会以更隐蔽的形态复活。</para>
        /// </summary>
        [Test]
        public void test_payloadMissingXpGainTag_throws_insteadOfDefaultingToZero()
        {
            var full = new SkillGrownPayload(1, 2, 3, 4, 0, 5, XpTen);
            byte[] all = PayloadCodec.Encode(full);

            // tag 7 的编码 = 1 byte tag + 8 byte long(FixCodec 写 int64)⇒ 去掉末 9 字节
            const int Tag7FieldBytes = 1 + 8;
            byte[] truncated = new byte[all.Length - Tag7FieldBytes];
            Array.Copy(all, truncated, truncated.Length);

            Assert.Throws<System.IO.InvalidDataException>(
                () => PayloadCodec.Decode<SkillGrownPayload>(EventKind.SkillGrown, truncated),
                "缺 tag 7 的载荷必须判为损坏(掩码 7 位全在场),不得静默补 0");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 8 参重载:调用方给的 xpGain 原样写入(不重算、不夹取)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 显式传入一个**故意不等于** BASE×1.0×K_novelty 的值 ⇒ 载荷里必须是**传入值**。
        /// <para>若发射器偷偷重算或夹取,本条会红 —— 它钉死「发射器是搬运工,不是计算者」。</para>
        /// </summary>
        [Test]
        public void test_emitterWithExplicitXpGain_passesValueThroughUnchanged()
        {
            Fix weird = FixParse.Parse("123/2");
            SkillGrownPayload payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, skillId: (int)SkillId.诊断, objectId: 1,
                novelty: NoveltyClass.Normal, level: null, currentTick: 100,
                patientId: PatientId.None, xpGain: weird);

            Assert.That(payload.XpGain.Raw, Is.EqualTo(weird.Raw),
                "8 参重载须原样搬运 xpGain。传入 raw = " + weird.Raw +
                ",载荷 raw = " + payload.XpGain.Raw);
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 7 参便捷重载:按基准难度 1.0 求值(丙-3)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 不传 xpGain 的重载须给出 <c>BASE × 1.0 × K_novelty</c> 的**精确**值,
        /// 用 <see cref="XpGainCalculator"/> 独立求值作外部真值(不自引发射器的结果)。
        /// </summary>
        [Test]
        public void test_emitterWithoutXpGain_usesBaselineDifficultyOne()
        {
            const int skill = (int)SkillId.诊断;
            const NoveltyClass novelty = NoveltyClass.First;

            Fix expected = XpGainCalculator.ComputeXpGain(skill, novelty, Fix.One);
            SkillGrownPayload payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, skillId: skill, objectId: 1,
                novelty: novelty, level: null, currentTick: 100);

            Assert.That(payload.XpGain.Raw, Is.EqualTo(expected.Raw),
                "丙-3:缺省难度 = 1.0 ⇒ XP_gain = BASE × 1.0 × K_novelty。\n" +
                "独立求值 raw = " + expected.Raw + ",载荷 raw = " + payload.XpGain.Raw);

            // 且必须**非零** —— 若重载退化成 Fix.Zero,「诊断不涨经验」会静默复活
            Assert.That(payload.XpGain.Raw, Is.GreaterThan(0),
                "诊断成长必须真的涨经验(GDD `:238`「不论对错」);raw = " + payload.XpGain.Raw);
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 生产端口真的消费了 kDifficulty(修「算了又丢」)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>本文件最重要的一条</b>:同一个端口,喂两个不同的 <c>kDifficulty</c> ⇒
        /// 落进流的 <c>XpGain</c> 必须**不同**,且各自等于
        /// <c>XpGainCalculator.ComputeXpGain(skill, Normal, 该难度)</c>。
        /// </summary>
        /// <remarks>
        /// <b>修前应当红</b>:端口形参收下 <c>kDifficulty</c> 后**方法体零使用**
        /// (2026-10-23 round37 实测),两种难度产出的载荷**完全相同**
        /// (都退化成旧 6 参构造的 <c>XpGain = 0</c>)⇒ 本条红。
        /// <para>判据从**公开端口**驱动,不直调发射器 —— 只有这样才能测到「端口丢弃」这一行为。</para>
        /// </remarks>
        [Test]
        public void test_productionPort_consumesKDifficulty_intoXpGain()
        {
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);
            var stream = new EventStream(new SeqIdAuthority(), new AbsentPresence());
            var port = new AssemblySkillGrowthPort(stream, encoder, prescriptionLevel: 0);

            const int skill = (int)SkillId.处方用药;
            Fix easy = FixParse.Parse("1");
            Fix hard = FixParse.Parse("3");

            port.EmitGrowth(actorId: 1, skillId: skill, objectId: 5,
                noveltyHint: 0, kDifficulty: easy, tick: 10, patientId: PatientId.None);
            port.EmitGrowth(actorId: 1, skillId: skill, objectId: 5,
                noveltyHint: 0, kDifficulty: hard, tick: 20, patientId: PatientId.None);

            var grown = stream.Events
                               .Where(e => e.Kind == EventKind.SkillGrown)
                               .Select(e => decoder.Decode<SkillGrownPayload>(EventKind.SkillGrown, e.Payload))
                               .ToList();
            Assert.That(grown.Count, Is.EqualTo(2), "两条成长须各落一条 SkillGrown");

            Fix expectedEasy = XpGainCalculator.ComputeXpGain(skill, NoveltyClass.Normal, easy);
            Fix expectedHard = XpGainCalculator.ComputeXpGain(skill, NoveltyClass.Normal, hard);

            Assert.That(grown[0].XpGain.Raw, Is.EqualTo(expectedEasy.Raw),
                "难度 1.0 ⇒ XP_gain 应为 " + expectedEasy.Raw + ",实测 " + grown[0].XpGain.Raw);
            Assert.That(grown[1].XpGain.Raw, Is.EqualTo(expectedHard.Raw),
                "难度 3.0 ⇒ XP_gain 应为 " + expectedHard.Raw + ",实测 " + grown[1].XpGain.Raw);
            Assert.That(grown[1].XpGain.Raw, Is.GreaterThan(grown[0].XpGain.Raw),
                "难度 3.0 必须比 1.0 给得多 —— 若两者相等,说明 kDifficulty 仍被丢弃(修前形态)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑥ 负 xpGain fail-loud
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 负 XP 是调用方算错(XP_gain 三因子均非负)⇒ 必须抛,不得把脏值写进真源。
        /// </summary>
        [Test]
        public void test_negativeXpGain_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SkillGrownEmitter.EmitGrowth(
                    actorId: 1, skillId: 0, objectId: 1,
                    novelty: NoveltyClass.Normal, level: null, currentTick: 10,
                    patientId: PatientId.None, xpGain: FixParse.Parse("-1")),
                "负 xpGain 必须 fail-loud(不得静默写进病史流)");
        }

        // ── 辅助:最小桩(与 skill_level_never_rebuilt_from_stream_gap_test 同形)──────
        // EventStream 只有 (IIdAuthority, IPresenceQuery) 两参构造,无无参重载。

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private int _next;
            public PatientId NextPatientId() => new PatientId(_next++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }

        private sealed class AbsentPresence : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => false;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
            public System.Collections.Generic.IReadOnlyCollection<int> PresentPatientIds()
                => Array.Empty<int>();
        }
    }
}
