// sprint-08 S1.1 件 B · 「查体 ⇒ 见过病 D」投影器(2026-10-12)
//
// 权威来源:
//   sprint-08 S1 计划 §2 件 B(甲案 30 novelty 最小链 —— 用户 2026-10-12 裁定)
//   diagnosis-system.md:227 / :238 —— 成长触发源 = 「**完成一次查体 / 问诊**并作出诊断」
//     「**用**驱动,不论对错」;「做一次查体就涨」= 本件触发时机(用户裁定「查体时」)
//   ADR-026 ⑥ —— SkillGrown 落病史流 · Level int 承载 · 不独立快照 · 从流重构
//   ADR-010 §三 / ADR-026 ⑦ —— 已见集可从病史流重建(SkillGrown 载荷含 ActorId + ObjectId)
//   ADR-009 §一 Q1 —— 派生态(病名册条目集)由三源重建,不独立快照
//   casebook-39.md §5.2 / §12(OQ-CB-1 甲)—— 病名册 = 词表「已见过」过滤视图;
//     「见过」的判定 = 本件写入的 SkillGrown(诊断, 病种 D)
//
// ⚠️ **本件为何住装配层(Gameplay.Boot)**:
//   取病人病种 D 须读 `DiseaseVitalsService.Courses`(Sim 侧病程档案,装配层可见);
//   8 侧(Gameplay.Presentation)物理上够不着 `Courses`(只读 `VitalsDto`,而 VitalsDto
//   **不含 disease_id** —— AC-37-15 守密精神)⇒ 投影只能在装配层做(与 ADR-025 §① 案 A 同理由)。
//
// ⚠️ **写路径 = 装配层写者形态(用户 2026-10-12 裁定「复用装配层写者」)**:
//   与 `PrescriptionPorts.cs` 的 `AssemblySkillGrowthPort.EmitGrowth` **逐行同构** ——
//   `SkillGrownEmitter.EmitGrowth` 构造载荷 → `IPayloadEncoder.Encode` 编码入池 →
//   `IEventSink.Append` 落病史流。**不共用其实例**:处方侧 novelty 恒 `Normal`(处方非首见),
//   本件 novelty = `NoveltyTracker` 真值(首见 ⇒ First);语义不同故形态复用、实例独立。
//   ⚠️ **不走 8 的 `DiagnosisGrowthGate`**:该门需要 `IEventAuthority` 实例,而**全仓无生产实装**
//   (ADR-007 §277 只写「P0 应写本地占位」,`Roll` 抽取逻辑归 52 story-002)—— 走它 = 把 52 的
//   活拉进 S1.1。铁律④ 门控在装配层缺席,但 P0 恒主机(客户端不装配本层,`SimAuthorityMode.Host`)
//   ⇒ 结构上仍是主机唯一写者。**登记为已知缺口**:联机轮(P1b)须补 `IEventAuthority` 生产占位 +
//   把本件写路径收进 `DiagnosisGrowthGate`。
//
// ⚠️ **本件是生产写者,不是第二真源**:`NoveltyTracker` 只是运行期加速缓存;真源 = 病史流
//   (ADR-026 ⑥「不独立快照,从流重构」)。读档重建路径 = 扫病史流 SkillGrown 行回填 NoveltyTracker。

using System;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 「查体 ⇒ 见过病 D」投影器 —— 把查体事实投影为一条 <see cref="EventKind.SkillGrown"/>
    /// (诊断技能,对象 = 病种 D)落病史流,并同步 <see cref="NoveltyTracker"/>。
    /// <para><b>病名册条目集的数据源(sprint-08 S1.1)</b>:病名册 = 词表中
    /// <c>disease_ids ∩ 已见集 ≠ ∅</c> 的条目(见件 C `LexiconBookSource`)。</para>
    /// <para>⚠️ 守密:本件在装配层读 disease_id,**只写进病史流载荷**;呈现层(42)永不见
    /// disease_id(只收 `lexicon_id` 列表)—— AC-37-15 / 件 E。</para>
    /// </summary>
    public sealed class SeenDiseaseProjector
    {
        /// <summary>诊断技能 ordinal(`SkillId.诊断 = 0`;本件固定写诊断侧成长)。</summary>
        private static readonly int DiagnosisSkillId = (int)SkillId.诊断;

        private readonly DiseaseVitalsService _vitals;
        private readonly NoveltyTracker _novelty;
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;

        /// <param name="vitals">体征链服务(取病人病种 D —— 读 <c>Courses</c>,装配层可见)。</param>
        /// <param name="novelty">新颖度追踪器(首见 ⇒ First;运行期加速缓存,真源在流)。</param>
        /// <param name="sink">主机事件写入通道(唯一 Append 落点;ADR-005 主机唯一)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 唯一路径;SkillGrown 走具名 <c>Encode&lt;T&gt;</c>)。</param>
        public SeenDiseaseProjector(DiseaseVitalsService vitals, NoveltyTracker novelty,
                                    IEventSink sink, IPayloadEncoder encoder)
        {
            _vitals = vitals ?? throw new ArgumentNullException(nameof(vitals));
            _novelty = novelty ?? throw new ArgumentNullException(nameof(novelty));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        }

        /// <summary>新颖度追踪器(件 C `LexiconBookSource` 读已见集的数据源;只读暴露)。</summary>
        public NoveltyTracker Novelty => _novelty;

        /// <summary>
        /// 一次查体完成 ⇒ 记一条「见过病 D」(诊断成长)。
        /// </summary>
        /// <remarks>
        /// <b>调用时机</b> = 查体完成之后(<c>ExamineDriver.OnExamine</c> 的调用点旁);
        /// 「查体时」由用户 2026-10-12 裁定。
        /// <b>触发粒度</b> = 每次查体一条(与诊断成长触发源「做一次查体就涨」逐字吻合);
        /// 重复查同一病人 ⇒ novelty 冷却内 = Stale(仍写一条,novelty 载荷承载冷却态)。
        /// </remarks>
        /// <param name="patient">被查病人(须已建档 —— 未建档 = 装配/时序错误,fail-loud)。</param>
        /// <param name="tick">查体完成 tick(来自 <c>ITickProvider</c>)。</param>
        /// <param name="actorId">玩家 actorId(机制 A id 空间;成长施予者)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负或超出
        /// <see cref="NoveltyTracker"/> 的 int tick 域(诚实失败,不静默截断)。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="actorId"/> 为负。</exception>
        /// <exception cref="Sim.DiseaseSimulation.PatientCourseNotFoundException">病人未建档
        /// (查体前须已 <c>DiseaseOnset</c>;调用方先判在场)。</exception>
        public void OnExamined(PatientId patient, long tick, int actorId)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "查体 tick 不得为负");
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "玩家 actorId 不得为负(机制 A 计数器 id 空间)");

            // ① 取病种 D(装配层可见;VitalsDto 不含 disease_id ⇒ 只能在此读)
            int diseaseId = _vitals.Courses.Get(patient).DiseaseId;

            // ② novelty 真值(首见 ⇒ First;冷却内 ⇒ Stale;冷却外 ⇒ Normal)
            //    ⚠️ NoveltyTracker 用 int tick 域 —— 超界 fail-loud(不静默截断,承 ADR-006 舍入纪律精神)
            int tickInt = checked((int)tick);
            NoveltyClass novelty = _novelty.Query(actorId, DiagnosisSkillId, diseaseId, tickInt);

            // ③ 构造载荷(SkillGrownEmitter 唯一出口;level = null ⇒ 未升级哨兵)
            //    ⚠️ 2026-10-23(round38 · 用户裁定 丙-3):本重载**缺省 xpGain**,
            //    内部按「基准难度 Fix.One」算 XP_gain = BASE × 1.0 × K_novelty。
            //    取 1.0 而非猜一个难度的理由:查体这一刻玩家**尚未落笔诊断**,「对错」无从得知
            //    (GDD `:494` 错诊 ×0.5 的因子在此时点不存在)⇒ 不伪造。
            //    错诊 ×0.5 已登记具名债 `30-诊断错诊难度未落`(归 8 的 GDD 轮,
            //    须把发射点后移到「落笔诊断」时刻)。
            SkillGrownPayload payload = SkillGrownEmitter.EmitGrowth(
                actorId: actorId, skillId: DiagnosisSkillId, objectId: diseaseId,
                novelty: novelty, level: null, currentTick: tick, patientId: patient);

            // ④ 编码 + 入池(ADR-029 唯一路径;写者零手搓 PayloadRef —— b6 门)
            PayloadRef payloadRef = _encoder.Encode(EventKind.SkillGrown, payload);

            // ⑤ Append 落病史流(Seq = -1,由 EventStream 发号 —— 与 AssemblySkillGrowthPort 同款)
            _sink.Append(new SimEvent(tick, patient, -1, EventKind.SkillGrown, payloadRef));
        }

        /// <summary>某玩家已见病种集(升序;病名册条目集的反查输入 —— 件 C 用)。
        /// <para>直接转发 <see cref="NoveltyTracker.EnumerateSeenObjects"/>;不缓存(零第二真源)。</para></summary>
        public int[] SeenDiseaseIds(int actorId) => _novelty.EnumerateSeenObjects(actorId, DiagnosisSkillId);
    }
}
