// 技能等级**在生产侧从不从病史流重建**的判据(2026-10-23 round32)。
//
// ── 本文件为什么存在 ──────────────────────────────────────────────────────────
// ADR-026 §⑥ 裁的是「技能等级**从病史流的 `SkillGrown` 重构**、不独立快照」;
// story-008 交付了 `SkillStateRebuilder.RebuildLevels`(纯函数,6 条 AC 全绿);
// 两条生产写者(`SeenDiseaseProjector` / `AssemblySkillGrowthPort`)也都真把
// `SkillGrown` 落进病史流。
//
// **但把这三段接起来的那根线不存在**。实测(本文件即其可执行形式):
//
//   · `RebuildLevels` 生产调用点 = **0**(全仓 grep 只命中测试与自身);
//   · `SkillProgression.TryLevelUp` / `XpGainCalculator.ComputeXpGain` 生产调用点 = **0**;
//   · 两条生产写者**都传 `level: null`** ⇒ 流里每一条 `SkillGrown` 的 `Level`
//     恒为哨兵 `-1` ⇒ `RebuildLevels` 的 `if (e.Level == LevelNotGrown) continue;`
//     会把它们**全部跳过** ⇒ 即便现在把 `RebuildLevels` 接上生产,也重建出空字典;
//   · 生产 `QueryLevel` 回的是构造时注入的**固定值**(`CompositionRoot.cs:408`
//     硬编码 `prescriptionLevel: 0`),与流无关。
//
// ⇒ 现状 = **等级永远是 0**(处方用药)/ 永远是 1(诊断,`CompositionRoot.cs:318`)
// ⇒ 任何 `UnlockLevel > 0` 的处方条目**恒不解锁**。
//
// ── 为什么本文件不「修」它 ────────────────────────────────────────────────────
// 修它要动三处**各自被钉死**的东西,且缺一个只有用户能做的裁定:
//   ① `RebuildLevels` 的签名 —— story-008 §Implementation 1 逐字钉死,story = `Complete`;
//   ② `QueryLevel` 的签名 —— `ISkillGrowthPort` 由 11 的 GDD / `PrescribeFlow` 定义;
//   ③ **XP 谁持有、进不进流** —— ADR-026 只裁了 `XP_gain` / `XP_to_next` 的**求值形式**,
//      对「累计 XP 住哪、是否落流」**零字**。而 `TryLevelUp` 的签名是
//      `ref int currentLevel, ref Fix currentXp` ⇒ 它要求调用方**持有一个可变 XP 累加器**。
//      那个累加器住在 sim 还是表现层、要不要进流、要不要快照 —— 全是裁定空白。
//      在没有它之前把 `RebuildLevels` 接上生产,只会把一个「恒 0」换成另一个「恒 0」
//      (因为流里没有非哨兵 Level),**制造已修复的假象**。
//
// ⇒ 本文件把「当前是什么」钉成可执行的事实,让缺口无法被忽略,**不替用户做裁定**。
//
// ── 本文件做什么 ──────────────────────────────────────────────────────────────
// 用**真装配件**跑(真 `EventStream` + 真 `PayloadEncoder`/`PayloadDecoder` +
// 生产类 `AssemblySkillGrowthPort` 本体),而不是桩 —— 因为要钉的正是**生产装配的接线事实**:
//   ① 生产 `QueryLevel` 无视流中已有的成长行;
//   ② 生产 `QueryLevel` 只认「处方用药」一个技能,其余恒 0;
//   ③ 生产写者写出的 `SkillGrown` 的 `Level` 恒为哨兵 ⇒ `RebuildLevels` 全跳过;
//   ④ 前提:`RebuildLevels` 本身没坏(喂非哨兵行能重建出来)—— 否则 ③ 的空字典
//      会被误读成「重建器有 bug」;
//   ⑤ 后果:`UnlockLevel > 0` 的处方条目在真 port 下恒判未解锁。
//
// ⚠️ **本文件全绿不等于「等级已接通」** —— 恰恰相反,它记录的是**没接通**。
//    判据 ①③⑤ 断言的都是「当前实现下如何」这个**事实**,不是「这是对的」。
//    将来若有人把重建接上生产并让写者写非哨兵 Level,①③⑤ 会红 ——
//    那时应**改写**本文件为正向判据,而不是删掉它。
//
// 权威来源:
//   `docs/architecture/adr-026-skill-growth-fixed-point.md` §⑥(从流重构、不独立快照)
//     · §Decision 一/二(`XP_gain` / `XP_to_next` 求值形式)
//   `production/epics/skill-system/story-008-persistence-and-stream-rebuild.md`(签名)
//   `unity/Assets/Gameplay.Boot/PrescriptionPorts.cs`(`AssemblySkillGrowthPort` 本体)
//   `unity/Assets/Gameplay.Boot/CompositionRoot.cs:408`(`prescriptionLevel: 0` 硬编码)
//   `unity/Assets/Gameplay.Boot/SeenDiseaseProjector.cs:108`(`level: null`)
//   `unity/Assets/Sim.Contracts/SkillSystem/SkillProgression.cs:54`(`ref … currentXp`)

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Prescription;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    /// <summary>
    /// 「技能等级在生产侧从不从病史流重建」的**行为记录**。
    /// </summary>
    /// <remarks>
    /// ⚠️ 本类断言的是「当前生产装配的真实行为」,不是「期望的产品行为」。
    /// 它存在的目的是让**接线缺口**可执行、可发现,而非声明该行为正确。
    /// </remarks>
    [TestFixture]
    internal sealed class SkillLevelNeverRebuiltFromStreamGapTest
    {
        private const int Actor = 1;
        private const int PrescriptionSkill = (int)SkillId.处方用药;
        private const int DiagnosticSkill = (int)SkillId.诊断;
        private const int DrugActionId = 10;              // prescription_actions.json 的唯一行
        private static readonly Fix KDifficulty = new Fix(Fix.OneRaw);

        // ══════════════════════════════════════════════════════════════════
        // 真装配件夹具
        // ══════════════════════════════════════════════════════════════════

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private EventStream _stream;
        private AssemblySkillGrowthPort _port;

        /// <param name="prescriptionLevel">
        /// 与 <c>CompositionRoot.cs:408</c> 同形 —— 生产装配注入的**固定等级**。
        /// </param>
        private void Assemble(int prescriptionLevel)
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new SeqIdAuthority(), new AbsentPresence());
            _port = new AssemblySkillGrowthPort(_stream, _encoder, prescriptionLevel);
        }

        [SetUp]
        public void SetUp() => Assemble(prescriptionLevel: 0);

        /// <summary>把一条**已升级**的成长行直接推进流(模拟「别处已经练到 level 级」)。</summary>
        private void AppendGrownRow(int actorId, int skillId, int level, long tick)
        {
            var payload = new SkillGrownPayload(
                actorId: actorId, patientId: PatientId.None.Value, skillId: skillId,
                objectId: DrugActionId, noveltyClass: (int)NoveltyClass.Normal, level: level);
            PayloadRef r = _encoder.Encode(EventKind.SkillGrown, payload);
            _stream.Append(new SimEvent(tick, PatientId.None, -1, EventKind.SkillGrown, r));
        }

        /// <summary>解码流里全部 <c>SkillGrown</c> 行(真 codec,不经内存错觉)。</summary>
        private List<SkillGrownPayload> DecodedGrownRows()
            => _stream.Events
                       .Where(e => e.Kind == EventKind.SkillGrown)
                       .Select(e => _decoder.Decode<SkillGrownPayload>(EventKind.SkillGrown, e.Payload))
                       .ToList();

        // ══════════════════════════════════════════════════════════════════
        // ① 生产 QueryLevel 无视流中已有的成长行
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>核心判据</b>:流里明明有「Actor 1 的处方用药已到 5 级」,
        /// 生产 <c>QueryLevel</c> 仍回构造时注入的固定值。
        /// </summary>
        /// <remarks>
        /// <b>这条断言的是「当前不读流」这个事实</b>,不是「不读流是对的」。
        /// 将来若 <c>QueryLevel</c> 改为从流重建,本条会红 —— 那时应改写为正向判据。
        /// <para><b>为什么值得钉</b>:ADR-026 §⑥ 裁的是「等级从流重构」,
        /// 而生产 <c>QueryLevel</c> 与流之间**没有任何路径**。没有本条时,这个断点
        /// 只存在于读代码的人脑中;有了本条,任何改动只要碰到这条路径都会在这里显形。</para>
        /// </remarks>
        [Test]
        public void test_queryLevel_ignoresExistingGrowthRowsInStream_currentBehavior()
        {
            AppendGrownRow(Actor, PrescriptionSkill, level: 5, tick: 100);

            // 前提:流里那条行**确实**是非哨兵 5 级(否则本条会被误读成「流里没东西」)。
            List<SkillGrownPayload> rows = DecodedGrownRows();
            Assert.That(rows.Count, Is.EqualTo(1), "流里应恰有 1 条 SkillGrown");
            Assert.That(rows[0].Level, Is.EqualTo(5),
                "前提:推进流的行是非哨兵 5 级(真 codec round-trip 后仍在)");

            int level = _port.QueryLevel(Actor, PrescriptionSkill);

            Assert.That(level, Is.EqualTo(0),
                "**当前实现**:`AssemblySkillGrowthPort.QueryLevel` 回构造时注入的固定值(此处 0)," +
                "**完全不读流** —— 流里的 5 级对它不可见。\n" +
                "⚠️ 这不是期望行为,是**接线缺口**的现状(见文件头);" +
                "接通须先裁「XP 累加器住哪 / 进不进流」(ADR-026 对此零字)");
        }

        /// <summary>
        /// 反方向再钉一次:把注入值换成 3,`QueryLevel` 就回 3 —— 证明返回值**只由构造参数决定**,
        /// 与流内容无关。
        /// </summary>
        [Test]
        public void test_queryLevel_followsConstructorArg_notStreamContent()
        {
            Assemble(prescriptionLevel: 3);
            AppendGrownRow(Actor, PrescriptionSkill, level: 5, tick: 100);

            Assert.That(_port.QueryLevel(Actor, PrescriptionSkill), Is.EqualTo(3),
                "**当前实现**:回的是构造时注入的 3,不是流里的 5 ⇒ 返回值纯由装配参数决定。" +
                "同一个流、同一个 actor,换个装配参数就换个答案 —— 这正是「不读流」的直接后果");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 生产 QueryLevel 只认「处方用药」一个技能
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 非「处方用药」的技能恒回 <c>0</c>,即使注入值是 3。
        /// </summary>
        /// <remarks>
        /// 本条钉住 <c>QueryLevel</c> 的**第二个硬编码**:
        /// <c>skillId == PrescribeFlow.PrescriptionSkillId ? _prescriptionLevel : 0</c>。
        /// 诊断技能走的是**另一条**路(<c>ExamineDriver</c> 的构造参数,同样硬编码),
        /// 不在本端口内 —— 两条路互不相干,也都不读流。
        /// </remarks>
        [Test]
        public void test_queryLevel_nonPrescriptionSkill_alwaysZero_currentBehavior()
        {
            Assemble(prescriptionLevel: 3);

            Assert.That(_port.QueryLevel(Actor, PrescriptionSkill), Is.EqualTo(3),
                "前提:注入的技能确实回注入值");
            Assert.That(_port.QueryLevel(Actor, DiagnosticSkill), Is.EqualTo(0),
                "**当前实现**:非处方技能恒 0(诊断的等级由 `ExamineDriver` 的构造参数另给," +
                "两条路互不相干,且都不读流)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 生产写者写出的 Level 恒为哨兵 ⇒ RebuildLevels 全跳过
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>核心判据</b>:生产写者(<c>AssemblySkillGrowthPort.EmitGrowth</c>)落进流的
        /// <c>SkillGrown</c>,其 <c>Level</c> 恒为哨兵 <c>-1</c> ⇒
        /// <c>RebuildLevels</c> 把它们**全部跳过**,返回空字典。
        /// </summary>
        /// <remarks>
        /// <b>这条是「为什么不能只把 RebuildLevels 接上生产」的证据</b>:
        /// 流里每一条成长行的 Level 都是「未升级」哨兵,重建器按 ADR-026 的语义
        /// (「未升级 ⇒ 哨兵,不写 0」)必须跳过它们 ⇒ 接上也只能重建出空表。
        /// <para>⇒ 真正的缺口在**写者侧**(没人算等级、没人持有 XP),
        /// 不在重建器侧。只改接线 = 把一个恒 0 换成另一个恒 0。</para>
        /// </remarks>
        [Test]
        public void test_productionWriter_alwaysWritesSentinelLevel_rebuildYieldsEmpty()
        {
            _port.EmitGrowth(Actor, PrescriptionSkill, DrugActionId,
                             noveltyHint: (int)NoveltyClass.Normal,
                             kDifficulty: KDifficulty, tick: 100, patientId: PatientId.None);

            List<SkillGrownPayload> rows = DecodedGrownRows();
            Assert.That(rows.Count, Is.EqualTo(1), "生产写者应落 1 条 SkillGrown");
            Assert.That(rows[0].Level, Is.EqualTo(SkillGrownPayload.LevelNotGrown),
                "**当前实现**:生产写者恒传 `level: null` ⇒ 落流的是未升级哨兵 -1。" +
                "`PrescriptionPorts.cs:150` 与 `SeenDiseaseProjector.cs:108` 两处同款");
            Assert.That(rows[0].ActorId, Is.EqualTo(Actor), "前提:载荷其余字段是真值(不是空载荷)");

            Dictionary<int, Dictionary<int, int>> rebuilt = SkillStateRebuilder.RebuildLevels(rows);

            Assert.That(rebuilt, Is.Empty,
                "**当前实现**:流里唯一的成长行是哨兵 ⇒ 重建器按语义全部跳过 ⇒ 空字典。" +
                "⇒ 即便现在把 `RebuildLevels` 接上生产,也重建不出任何等级");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 前提:RebuildLevels 本身没坏
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 前提判据:喂**非哨兵**行时,<c>RebuildLevels</c> 确实能重建出等级。
        /// </summary>
        /// <remarks>
        /// <b>为什么单列</b>:若重建器本身有 bug(对任何输入都回空),判据 ③ 的空字典
        /// 会被误读成「重建器坏了」,而真因是「写者不写等级」—— 两个完全不同的修法。
        /// 本条把「重建器是好的,坏的是写者 + 接线」这个前提钉死。
        /// </remarks>
        [Test]
        public void test_rebuildLevels_worksOnNonSentinelRows_premise()
        {
            AppendGrownRow(Actor, PrescriptionSkill, level: 5, tick: 100);

            Dictionary<int, Dictionary<int, int>> rebuilt = SkillStateRebuilder.RebuildLevels(DecodedGrownRows());

            Assert.That(rebuilt[Actor].ContainsKey(PrescriptionSkill), Is.True,
                "前提:非哨兵行应被重建出来 —— 否则 ③ 的空字典说明不了「写者不写等级」");
            Assert.That(rebuilt[Actor][PrescriptionSkill], Is.EqualTo(5), "前提:等级值原样重建");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 后果:UnlockLevel > 0 的处方条目恒判未解锁
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 后果判据:真 port 下,<c>UnlockLevel &gt; 0</c> 的处方条目**恒**判未解锁。
        /// </summary>
        /// <remarks>
        /// 这是缺口最直接的**玩家可见**后果:门 = <c>QueryLevel ≥ UnlockLevel</c>,
        /// 而 <c>QueryLevel</c> 恒 0 ⇒ 任何门槛 &gt; 0 的药永远开不出来。
        /// <para>⚠️ 当前 <c>prescription_actions.json</c> 只有一行且门槛为 0
        /// (`BuildFromRows` 也恒填 0,产物无该字段)⇒ **今天**这个后果不显形。
        /// 但只要数值轮给任何一个条目填了非零门槛,它就立刻成立 —— 本条把这个
        /// 潜伏条件钉成可执行事实。</para>
        /// </remarks>
        [Test]
        public void test_gateHit_anyPositiveUnlockLevel_neverUnlocks_withProductionPort()
        {
            AppendGrownRow(Actor, PrescriptionSkill, level: 5, tick: 100);

            var gated = new PrescriptionEntry(
                actionId: DrugActionId, polarity: PrescriptionPolarity.Symptomatic, unlockLevel: 1);

            bool hit = PrescribeFlow.EvaluateGateHit(
                doseRange: null, effectiveDose: 1, entry: gated, actorId: Actor, skills: _port);

            Assert.That(hit, Is.False,
                "**当前实现**:门槛 1 的处方在真 port 下恒判未解锁 —— 因为 `QueryLevel` 恒 0," +
                "而流里的 5 级对它不可见。\n" +
                "⚠️ 今天不显形仅因数据里没有非零门槛(数值轮未填);填上即成立");

            // 对照:门槛 0 的条目照常通过 —— 证明 false 不是「剂量判定」等别的分支造成的。
            var ungated = new PrescriptionEntry(
                actionId: DrugActionId, polarity: PrescriptionPolarity.Symptomatic, unlockLevel: 0);
            Assert.That(PrescribeFlow.EvaluateGateHit(
                    doseRange: null, effectiveDose: 1, entry: ungated, actorId: Actor, skills: _port),
                Is.True,
                "对照:门槛 0 的条目应通过 —— 否则上面的 false 归因不成立");
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具桩
        // ══════════════════════════════════════════════════════════════════

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
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }
    }
}
