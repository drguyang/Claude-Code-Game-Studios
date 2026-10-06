// Story 004 —— Prescribe 流程编排(域检查 · 原子扣减 · 成长门)
//
// 权威来源:
//   GDD 规则十 五步 Pseudocode(design/gdd/prescription-and-medication.md)
//   GDD F-11.4(11 恒 Applied,名义路径不入载荷链)· F-11.5(成长门 GateHit)
//   GDD §Edge Cases(库存不足整体拒绝 / 对象离场 / dose_range 空 ⇒ 整剂 / 混堆最低档 /
//                    K_difficulty 缺失 ⇒ 不发成长 + 断言硬失败 / 同 tick 两剂各得不同 Seq)
//   ADR-005(主机唯一 Step / Append;客户端上行意图)
//   ADR-009 §七(意图 → 权威结算的一般形状;⚠️ 11 与 10 的区别 = **无判定步**,意图即效果)
//   ADR-020 §四 AC-20-03 先例(判据 = 反射断言非 grep)
//   ADR-026(SkillGrown 落病史流;省料**不**放大药效)
//   ADR-029 §③(载荷编码唯一路径 = IPayloadEncoder;Sim/ 目录内禁 `new PayloadRef(`)
//   OQ-11-13(已裁 2026-10-06):K_difficulty 由 11 自表派生,**不读 severity**
//   OQ-11-10 / D-21-29(已裁 2026-10-06):portions = dose × portions_per_dose(21a)
//
// ⚠️ 本文件是 Sim 程序集(门 A:noEngineReferences),引用集 = {BCL, Sim.Contracts}。
//    与 20 / 30 的接缝以**本文件内的端口接口**表达(11 侧只声明形状,实现归兄弟 epic),
//    与 HostEmergencyProcessor 消费 IEventSink / IIdAuthority 同型。

using System;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Sim.Prescription
{
    /// <summary>处置极性(11 处方表声明;规则五 R-6 收窄 = 按 action 声明单一值,不按病种索引)。
    /// <para>ordinal 进载荷 —— <c>DrugTreatmentAppliedPayload.Polarity</c> 是 int
    /// (Sim.Contracts 不造枚举,防 ordinal 表第二真源;本枚举是 11 的作者态声明)。</para></summary>
    public enum PrescriptionPolarity : int
    {
        /// <summary>对症。</summary>
        Symptomatic = 0,
        /// <summary>对因。</summary>
        Causal = 1,
    }

    /// <summary>11 处方表在运行期的**只读视图**(由 Story 001 的 <c>prescription_actions.cooked</c> 装载)。</summary>
    public readonly struct PrescriptionEntry
    {
        /// <summary>处置 id 枚举序数(载荷「处置_id」项)。</summary>
        public readonly int ActionId;

        /// <summary>极性(载荷「polarity」项)。</summary>
        public readonly PrescriptionPolarity Polarity;

        /// <summary>解锁所需「处方用药」等级;<c>0</c> = 无门槛。
        /// <para>F-11.5 的 GateHit ② 用(<c>QueryLevel ≥ UnlockLevel</c>)。
        /// 具体门槛值 = 11 作者态数据(值归数值轮)。</para></summary>
        public readonly int UnlockLevel;

        public PrescriptionEntry(int actionId, PrescriptionPolarity polarity, int unlockLevel)
        {
            ActionId = actionId; Polarity = polarity; UnlockLevel = unlockLevel;
        }
    }

    /// <summary>
    /// 药效刻度 → 库存份数换算(<c>D-21-29</c>,字段 = 21a <c>drug_profile.portions_per_dose</c>)。
    /// <para>11 **只求值、不定义**该表(17 / 18 结构性否决,规则十)。</para>
    /// </summary>
    /// <remarks>
    /// ⚠️ **影子 schema(story-004 impl note 2)**:字段产出方 21a 侧尚未落 C# 类型
    /// (<c>DrugProfile.cs</c> 无 <c>portions_per_dose</c>)⇒ 本 story 以本端口 + 合成夹具走通,
    /// 真实产出方落位后仅换实现,零代码改动。负测登记 BLOCKED-BY-OQ-11-10 机制半边。
    /// </remarks>
    public interface IPortionsConversion
    {
        /// <summary>每剂份数(= 21a <c>drug_profile.portions_per_dose</c>;字段缺 ⇒ 1 恒等)。</summary>
        /// <remarks>
        /// ⚠️ **端口只查表,不做乘法** —— <c>portions = dose × portions_per_dose</c> 的乘法住在
        /// <see cref="PrescribeFlow.Prescribe"/> 步骤①(GDD `:408`「求值在 11」的字面兑现)。
        /// 若把乘法推给兄弟 epic,11 的 <c>portions &lt; 1</c> 下界守卫就由**实现方契约**提供,
        /// 本侧测试无法证该式(结构评审 m1)。
        /// </remarks>
        /// <returns>每剂份数(≥ 1;0 / 负 = 数据错误,由调用方下界守卫拦)。</returns>
        int PortionsPerDose(ItemKey itemKey);
    }

    /// <summary>20 库存的验货 / 扣减面(实现归 inventory-items epic;11 不自持账本)。</summary>
    public interface IPortionsStore
    {
        /// <summary>验货:该玩家是否持有 ≥ <paramref name="portions"/> 份。</summary>
        bool HasPortions(int playerId, ItemKey itemKey, int portions);

        /// <summary>被耗实例集的**确定性最低品级档**。
        /// <para>⚠️ 契约:结果 = <c>f(多重集)</c>,**与遍历顺序无关**(混堆确定性判据)。</para>
        /// <para><c>portions = 0</c> ⇒ 返回 <see cref="PrescribeFlow.NoQuality"/>。</para></summary>
        int PeekLowestQuality(int playerId, ItemKey itemKey, int portions);

        /// <summary>原子扣减。返回 <c>false</c> = 失败(并发等)⇒ 调用方必须零事件。</summary>
        bool ConsumePortions(int playerId, ItemKey itemKey, int portions);
    }

    /// <summary>30 技能与熟练度的**只读 + 成长入口**(实现归 skill-system epic)。</summary>
    /// <remarks>
    /// ⚠️ <c>kDifficulty</c> 是**可空**的,存在理由 = 让「缺参」在类型上可表达 ——
    /// F-11.5 要求「缺 ⇒ 不发成长 **且** 断言硬失败(非静默跳过)」,若不可空则该判据不可测。
    /// </remarks>
    public interface ISkillGrowthPort
    {
        /// <summary>只读等级查询(P0 只读)。</summary>
        int QueryLevel(int actorId, int skillId);

        /// <summary>成长入口(30 的 <c>EmitGrowth</c>)。11 只在 <c>GateHit</c> 为真时调用。</summary>
        /// <param name="noveltyHint">新颖度**提示** —— 非判定(真源在 30 的词典)。</param>
        /// <param name="kDifficulty">难度系数(<c>Fix</c>)。</param>
        void EmitGrowth(int actorId, int skillId, int objectId, int noveltyHint,
                        Fix kDifficulty, long tick, PatientId patientId);
    }

    /// <summary>一次 Prescribe 的完整入参(全整数域)。</summary>
    public readonly struct PrescribeRequest
    {
        /// <summary>药(复合主键)。</summary>
        public readonly ItemKey ItemKey;

        /// <summary>11 处方表条目(处置 id / 极性 / 解锁门槛 —— 规则四 的作者态映射)。</summary>
        public readonly PrescriptionEntry Entry;

        /// <summary>医师(玩家)id。</summary>
        public readonly int ActorId;

        /// <summary>施治对象。</summary>
        public readonly PatientId PatientId;

        /// <summary>玩家选择的剂量整数档。域可空时被 <see cref="DoseCalculator.ResolveEffectiveDose"/> 置 1。</summary>
        public readonly int SelectedDose;

        /// <summary>21a 药物档案(<c>dose_range</c> 可空 ⇒ 整剂路径 AC-11-17)。</summary>
        public readonly DrugProfile Profile;

        /// <summary>被耗实例集的最低品级档。域可空时被置 <see cref="PrescribeFlow.NoQuality"/>。</summary>
        public readonly int SelectedQuality;

        /// <summary>是否主机上下文。非主机 ⇒ 零 Append(客户端上行意图,ADR-005)。</summary>
        public readonly bool IsHost;

        public PrescribeRequest(ItemKey itemKey, PrescriptionEntry entry, int actorId, PatientId patientId,
                                int selectedDose, DrugProfile profile, int selectedQuality, bool isHost)
        {
            ItemKey = itemKey; Entry = entry; ActorId = actorId; PatientId = patientId;
            SelectedDose = selectedDose; Profile = profile; SelectedQuality = selectedQuality;
            IsHost = isHost;
        }
    }

    /// <summary>一次 Prescribe 的结果(只读回执 —— ⚠️ 失败原因**不回传呈现层**,零提示纪律 AC-11-12)。</summary>
    /// <remarks>
    /// <para><see cref="Applied"/> 的语义 = **本上下文完成了它该做的事** ——
    /// 主机 = 走完五步并落流;客户端 = 本地编排跑完 + 返回 <c>Applied/GateHit/Portions</c> 回执
    /// (**本地零扣减零 Append**)。⚠️ **上行意图的物化不在本流程** —— 客户端回执**不含**任何
    /// 可传输对象(<see cref="TreatmentEvent"/> 恒 null),意图构造与传输归 45,BLOCKED-BY-45。</para>
    /// <para>被拒路径(步骤① 或 ③ 失败)⇒ <c>Applied = false</c> 且四个字段全零。</para>
    /// </remarks>
    public readonly struct PrescribeOutcome
    {
        /// <summary>是否进入并完成(主机五步 / 客户端本地编排)。</summary>
        public readonly bool Applied;

        /// <summary>四步后的处置事件(**仅主机**非空)。</summary>
        public readonly SimEvent? TreatmentEvent;

        /// <summary>成长门是否命中(⑤ 的准入)。</summary>
        public readonly bool GateHit;

        /// <summary>实际消耗份数(0 = 未进入)。</summary>
        public readonly int Portions;

        public PrescribeOutcome(bool applied, SimEvent? treatmentEvent, bool gateHit, int portions)
        {
            Applied = applied; TreatmentEvent = treatmentEvent; GateHit = gateHit; Portions = portions;
        }
    }

    /// <summary>
    /// 11 的 Prescribe 五步流程(规则十)。**纯编排** —— 求值委托
    /// <see cref="DoseCalculator"/> / <see cref="HalfLifeCalculator"/>,写入委托
    /// <see cref="IEventSink"/> / <see cref="IPayloadEncoder"/>。
    /// </summary>
    /// <remarks>
    /// <para><b>五步顺序不可调</b>(先验再扣):① 域检查 → ② 求值 + 换算 → ③ 交 20 扣减 →
    /// ④ **无条件**发 <c>DrugTreatmentApplied</c> → ⑤ <c>GateHit</c> 门控的 <c>EmitGrowth</c>。</para>
    /// <para><b>①失败 ⇒ 不进入 ②</b> —— 零求值、零扣减、零事件、零成长(AC-11-04)。</para>
    /// <para><b>11 无判定步</b>:恒 <c>Applied</c>(F-11.4),<c>ResultMul[Applied] = 1.0</c> 是
    /// **空运算** ⇒ 不入载荷链(AC-11-10 —— 指标称路径,<c>Sim/Prescription/</c> 零乘子消费点)。</para>
    /// </remarks>
    public static class PrescribeFlow
    {
        /// <summary>处方用药技能 id(ADR-024 registry ordinal)。</summary>
        public const int PrescriptionSkillId = (int)SkillId.处方用药;

        /// <summary>无品级哨兵(零份数 / 空被耗集)。与 21a 的品级档不冲突(档位从 1 起)。
        /// <para>按 <see cref="IPortionsStore.PeekLowestQuality"/> 的契约返回;
        /// 正常路径(<c>portions ≥ 1</c>)永不取到它。</para></summary>
        public const int NoQuality = 0;

        /// <summary>新颖度提示的缺省值 —— 「无提示」(真源在 30 的词典,11 不判)。</summary>
        public const int NoveltyHintNone = (int)NoveltyClass.Normal;

        /// <summary>
        /// 步骤⑤ 的成长门(F-11.5,BL-3 改判后口径)。
        /// <para><c>GateHit = 剂量域内合法 ∧ 该药 ∈ 已解锁子集</c> —— **只读 11 自己持有的量**,
        /// 不读病人的任何病种级布尔(<c>treatable_by</c> / <c>disease_id</c> / <c>tier_named</c>)。</para>
        /// <para>⚠️ <b>整剂路径</b>(<c>doseRange == null</c>):域内合法性 = **恒真**
        /// (AC-11-17「另判点」,显式锁定 —— 否则「空域 ⇒ 永不成长」会静默吞掉整类药的养成)。</para>
        /// </summary>
        /// <param name="doseRange">该药剂量域(可空)。</param>
        /// <param name="effectiveDose">有效剂量档(整剂路径 = 1)。</param>
        /// <param name="entry">11 处方表条目(取解锁门槛)。</param>
        /// <param name="actorId">医师 id。</param>
        /// <param name="skills">30 只读等级查询。</param>
        public static bool EvaluateGateHit(DoseRange? doseRange, int effectiveDose,
                                           in PrescriptionEntry entry, int actorId, ISkillGrowthPort skills)
        {
            if (skills == null) throw new ArgumentNullException(nameof(skills));

            // ① 剂量域内合法:域可空 ⇒ 恒真(整剂,AC-11-17);否则须落在 [min, max]。
            //    ⚠️ 不做运行期 clamp(AC-11-18 的限位在 42 侧);越界 = 数据/接缝错误,如实判 false。
            bool doseLegal = doseRange == null
                          || (effectiveDose >= doseRange.Value.Min && effectiveDose <= doseRange.Value.Max);

            // ② 该药 ∈ 已解锁子集(30 的等级查询,只读)。
            int level = skills.QueryLevel(actorId, PrescriptionSkillId);
            bool unlocked = level >= entry.UnlockLevel;

            return doseLegal && unlocked;
        }

        /// <summary>
        /// 由 11 自表派生 <c>K_difficulty</c>(<c>OQ-11-13</c> 2026-10-06 裁定)。
        /// <para><b>来源域 = 11 自己的表</b> —— 取 <c>drug_potency / DOSE_BASE</c> 作难度刻度
        /// (匹药越猛 ⇒ 处置难度越高)。<b>刻意不读 severity</b> —— 它是病种级量,读它破规则一。</para>
        /// <para><b>派生量的具体属性与映射归数值轮(用户)</b>;本方法只落实「来源域 + 求值形状」,
        /// 不再是空壳:公式与量纲已钉死,数值档位可后续回改。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <c>drug_profile.drug_potency</c> 缺失 —— **硬失败**(缺入参不凑数,承 30 的 AC-51-D2 不旁路纪律)。
        /// </exception>
        public static Fix DeriveKDifficulty(in DrugProfile profile, int doseBase)
        {
            if (doseBase <= 0)
                throw new ArgumentOutOfRangeException(nameof(doseBase), $"DOSE_BASE 必须 > 0,实际 = {doseBase}");

            if (!profile.DrugPotency.HasValue)
                throw new InvalidOperationException(
                    "prescribe-flow: drug_profile.drug_potency 缺失 —— K_difficulty 无从派生(OQ-11-13 来源域必有值)。" +
                    "硬失败,不静默跳过。");

            // K_difficulty = |drug_potency| ÷ DOSE_BASE —— **域保持的 Q16.16 除法**
            // (两个 Q16.16 量相除,经 Fix.operator/(已核验的 128 位路径 + half-away 舍入),
            //  零浮点)。取绝对幅值:难度是量级概念,与药效的正负(对症 / 对因链)无关。
            long raw = profile.DrugPotency.Value.Raw;
            long magnitude = raw < 0 ? unchecked(-raw) : raw;
            return new Fix(magnitude) / new Fix(doseBase);
        }

        /// <summary>
        /// Prescribe 五步(规则十)。**主机与客户端同跑编排,仅写入面不同**(ADR-005)。
        /// </summary>
        /// <param name="req">全整数入参。</param>
        /// <param name="tick">主机当下 tick(<see cref="ITickProvider"/>)。</param>
        /// <param name="ports">20 换算 / 库存 + 30 技能 的接缝(兄弟 epic 实现)。</param>
        /// <param name="eventSink">事件写入通道(主机唯一 Append 的落点)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法载荷构造路径)。</param>
        /// <returns>只读回执(失败原因不回传 —— 零提示纪律)。</returns>
        public static PrescribeOutcome Prescribe(in PrescribeRequest req, long tick, PrescribePorts ports,
                                                 IEventSink eventSink, IPayloadEncoder encoder)
        {
            if (ports == null) throw new ArgumentNullException(nameof(ports));
            if (eventSink == null) throw new ArgumentNullException(nameof(eventSink));
            if (encoder == null) throw new ArgumentNullException(nameof(encoder));

            // ── ① 域检查(三个合取项)—— 失败 ⇒ **不进入** ②(AC-11-04 / Edge Cases)──────
            //
            // 1a. 剂量域内合法(域可空 ⇒ 整剂,恒真)。先做,因为它决定 1b 的份数。
            bool doseLegal = req.Profile.DoseRange == null
                          || (req.SelectedDose >= req.Profile.DoseRange.Value.Min
                              && req.SelectedDose <= req.Profile.DoseRange.Value.Max);
            if (!doseLegal)
                return NotApplied();

            // 1b. 换算后份数有货(portions = dose × portions_per_dose;换算表值缺 ⇒ 1)。
            //     ⚠️ **乘法住在 11**(GDD `:408`「求值在 11」)—— 端口只查表,不代算。
            int effectiveDoseEarly = DoseCalculator.ResolveEffectiveDose(req.Profile.DoseRange, req.SelectedDose);
            int portionsPerDose = ports.Conversion.PortionsPerDose(req.ItemKey);
            long portionsWide = (long)effectiveDoseEarly * portionsPerDose;   // 宽算防溢出
            if (portionsWide < 1 || portionsWide > int.MaxValue)
                return NotApplied();     // 不得出现「给药不耗药」(下界 = 1 份);上界 = 装配错误
            int portions = (int)portionsWide;
            if (!ports.Store.HasPortions(req.ActorId, req.ItemKey, portions))
                return NotApplied();     // 无货 ⇒ 零事件零成长,库存零变化(整体拒绝,无半剂)

            // 1c. 对象在场(经 9 的 IPresenceQuery,不读表现态位置 —— ADR-016 §三)。
            if (!ports.Presence.IsPresent(req.PatientId))
                return NotApplied();

            // ── ② 求值:F-11.1(药效)+ F-11.2(半衰期)+ D-21-29 换算 ────────────────
            var doseResult = DoseCalculator.CalculateForDrug(
                req.Profile.DrugPotency ?? default,
                req.Profile.DoseRange,
                req.SelectedDose,
                ports.DoseBase);

            // F-11.2 的输入 = 被耗实例集的**最低品级档**(混堆确定性:结果 = f(多重集),与顺序无关)。
            // ⚠️ 该标量**必须向 20 索取** —— 11 不自选实例集、不遍历实例列表(禁读列表序,
            //    承 ADR-016 三源不变量);`req.SelectedQuality` 只是呈现回执,不是本流程的输入。
            // ⚠️ 与 `dose_range` 无关:品级是**药材实例**的属性,整剂路径同样有实例被耗。
            int quality = ports.Store.PeekLowestQuality(req.ActorId, req.ItemKey, portions);

            var halfLifeResult = HalfLifeCalculator.CalculateForDrug(req.Profile, quality);

            // ── ③ 交 20:Apply(消耗 portions 份)──────────────────────────────────
            // ⚠️ 验货(1b)与扣减之间**无事件写入**(TR-prescription-012 原子性)。
            // ⚠️ 客户端上下文**不扣减**(本地零状态)—— 扣减权在主机(ADR-005);
            //    客户端上行意图,本流程只跑编排并回执(传输半边 BLOCKED-BY-45)。
            if (req.IsHost)
            {
                if (!ports.Store.ConsumePortions(req.ActorId, req.ItemKey, portions))
                    return NotApplied();     // 扣减失败(并发等)⇒ 零事件
            }

            // ── ④ 发 DrugTreatmentApplied —— **无条件**(F-11.4 恒 Applied,流程内零失败分支)──
            var payload = new DrugTreatmentAppliedPayload(
                tick:        tick,
                treatmentId: req.Entry.ActionId,
                actorId:     req.ActorId,
                polarity:    (int)req.Entry.Polarity,
                drugPotency: doseResult.DosePotency,
                halfLife:    halfLifeResult.AxisEffective.Raw,
                seq:         0);          // 载荷 Seq = 占位;header Seq 由主机 Append 时发号(承 10 的同一现状)

            SimEvent? treatmentEvent = null;
            if (req.IsHost)
            {
                var e = new SimEvent(tick, req.PatientId, 0, EventKind.DrugTreatmentApplied,
                                     encoder.Encode(EventKind.DrugTreatmentApplied, payload));
                eventSink.Append(e);
                treatmentEvent = e;
            }

            // ── ⑤ 成长 —— **须先过 GateHit**(F-11.5);缺 K_difficulty ⇒ 不发 + 硬失败 ─────
            bool gateHit = EvaluateGateHit(req.Profile.DoseRange, doseResult.EffectiveDose,
                                           req.Entry, req.ActorId, ports.Skills);
            if (gateHit && req.IsHost)
            {
                // ⚠️ 派生先于发出 —— 缺参 ⇒ 抛(事件**不回滚**:它已进流 = 真源,story-004 impl note 4)。
                Fix kDifficulty = DeriveKDifficulty(req.Profile, ports.DoseBase);
                ports.Skills.EmitGrowth(
                    actorId:     req.ActorId,
                    skillId:     PrescriptionSkillId,
                    objectId:    req.Entry.ActionId,   // 对象表 = 11 处方表(由 SkillId 决定读哪张表)
                    noveltyHint: NoveltyHintNone,      // 11 只传提示;真源在 30 的词典
                    kDifficulty: kDifficulty,
                    tick:        tick,
                    patientId:   req.PatientId);
            }

            return new PrescribeOutcome(true, treatmentEvent, gateHit, portions);
        }

        private static PrescribeOutcome NotApplied() => new PrescribeOutcome(false, null, false, 0);
    }

    /// <summary>Prescribe 的兄弟 epic 接缝集合(一次性注入,避免五参构造)。</summary>
    public sealed class PrescribePorts
    {
        /// <summary>20 的换算表(<c>D-21-29</c>;影子 schema,见 <see cref="IPortionsConversion"/>)。</summary>
        public IPortionsConversion Conversion { get; }

        /// <summary>20 的库存面。</summary>
        public IPortionsStore Store { get; }

        /// <summary>9 的在场查询。</summary>
        public IPresenceQuery Presence { get; }

        /// <summary>30 的等级 / 成长面。</summary>
        public ISkillGrowthPort Skills { get; }

        /// <summary>剂量基准(11 的作者态常量,承 Story 001 的 <c>dose_const.DOSE_BASE</c>)。</summary>
        public int DoseBase { get; }

        public PrescribePorts(IPortionsConversion conversion, IPortionsStore store,
                              IPresenceQuery presence, ISkillGrowthPort skills, int doseBase)
        {
            Conversion = conversion ?? throw new ArgumentNullException(nameof(conversion));
            Store = store ?? throw new ArgumentNullException(nameof(store));
            Presence = presence ?? throw new ArgumentNullException(nameof(presence));
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
            // ⚠️ 装配期 fail-fast(结构评审 m4)—— 否则误配 doseBase = 0 会被推迟到**步骤⑤**
            //    (`DeriveKDifficulty` 抛),而此时事件**已进流**,一个装配错误伪装成「缺参不回滚」。
            if (doseBase <= 0)
                throw new ArgumentOutOfRangeException(nameof(doseBase),
                    $"DOSE_BASE 必须 > 0,实际 = {doseBase}(装配错误,须在注入期硬失败)");
            DoseBase = doseBase;
        }
    }
}
