// 53 判定链的**生产驱动** —— 每 tick 边沿:`Aggregate → Judge → Emit`(ADR-036 D2 · S3 · 2026-10-30)
//
// 权威:ADR-036 §Decision D2(全链一次装配)· D5(D-1 契约:读流与写流须同一引用)
//       · D6(去重:下层键剔除 outcome + 上层不一致即抛)
//       · §Implementation Guidelines(零跨 tick 态 · 边沿次序排在 `VitalsService` 之后 —— R4)
//
// ── 它解的是什么 ────────────────────────────────────────────────────────────
// ADR-036 §Context 实测:`ConsequenceEmitter` **零生产装配点**、`Judge.Evaluate` **零生产调用点**
// ⇒ 53 的整条判定链在生产侧从未存在。本类是那条链的第一个、也是唯一的驱动点。
//
// ── 三个「为什么」──────────────────────────────────────────────────────────
// ① **为什么住 `Gameplay.Boot`**:`Sim` 的引用集恰 = {BCL, `Sim.Contracts`}(ADR-025 §① / 门 A)
//    ⇒ 它看不见 9 的实现、也看不见烘焙产物的解码器。装配层是三者的唯一交汇点。
// ② **为什么不缓存跨 tick 状态**:承 AC-53-06「零跨 tick 态」。历史结案的 D 由
//    `DiseaseProjectionAdapter` 经 9 的**状态转移历史**回溯(用户裁定 2026-10-30)——
//    本类每一轮都从流 + 9 的派生态重算,**读档后同刻结果一致**。
// ③ **为什么 `eligible` 每轮是全量历史结案**:`ConsequenceEmitter.Emit` 只对**本轮给的**合格行
//    算到期集(`ConsequenceEmitter.cs:174`),它自己没有记忆。延迟窗内的结案必须每轮重新出现,
//    否则 `EmitTick` 那一轮永远等不到它。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 53 的判定链驱动:每个 tick 边沿把「已结案病例」投影成判定输入 → 求值 → 交发射器。
    /// </summary>
    /// <remarks>
    /// <para><b>单向</b>:只读事件流、只经 <see cref="IEventSink"/> 写;不持游戏状态
    /// (承 ADR-013 §9 C3 / ADR-018 §一 的同构纪律)。</para>
    /// <para><b>边沿次序(R4)</b>:本类的 <see cref="OnTickEdge"/> 必须排在
    /// <c>DiseaseVitalsService.OnTickEdge</c> **之后** —— 否则读到的是上一 tick 的 F4 态,
    /// 静默偏差一个 tick 且无报错。装配点见 <c>CompositionRoot</c> 的 `OnTicks` 注册。</para>
    /// <para><b>遇抛即终止本 tick</b>:不 catch、不重试、不回滚 —— 与 <see cref="ConsequenceEmitter"/>
    /// 的 precondition 同口径(Append 逐条即时提交,吞异常再试 = 撞 BCD-码-3)。</para>
    /// </remarks>
    public sealed class ConsequenceDriver
    {
        private readonly EventStream _stream;
        private readonly IPayloadDecoder _decoder;
        private readonly IDiseaseProjection _projection;
        private readonly Outcome53.Table _outcomeTable;
        private readonly ConsequenceEmitter _emitter;
        private readonly IReadOnlyDictionary<int, string> _diseaseKeyById;
        private readonly ITreatableByLookup _treatableBy;
        private readonly long _worldSeed;

        /// <summary>构造驱动。</summary>
        /// <param name="stream">事件流(**读面**;`Events`)。</param>
        /// <param name="writeFace">发射器的**写面**(生产 = 同一个 <see cref="EventStream"/> 实例)。</param>
        /// <param name="emitter">后果发射器(写权门 / 延迟表 / 幂等差额都在它里面)。</param>
        /// <param name="decoder">载荷解码器(ADR-031 第八抽象点)。</param>
        /// <param name="projection">9 的病程投影(ADR-036 D1)。</param>
        /// <param name="outcomeTable">判定表(18 行;ADR-014 烘焙产物)。</param>
        /// <param name="registry">病种注册表(id → key;C 轴命中位与 U 的 bit 判据都要用)。</param>
        /// <param name="treatableBy">处置轴查表面(`treatable_by`;**null = 未接线** ⇒ 命中位恒 false)。</param>
        /// <param name="worldSeed">`WorldSeed`(U 派生的混盐输入)。</param>
        /// <exception cref="ArgumentNullException">任一必需依赖为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="writeFace"/> 与 <paramref name="stream"/>
        /// **不是同一实例**(ADR-036 D5)。</exception>
        public ConsequenceDriver(
            EventStream stream,
            IEventSink writeFace,
            ConsequenceEmitter emitter,
            IPayloadDecoder decoder,
            IDiseaseProjection projection,
            Outcome53.Table outcomeTable,
            IReadOnlyList<DiseaseRegistryEntry> registry,
            ITreatableByLookup treatableBy,
            ulong worldSeed)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (writeFace == null) throw new ArgumentNullException(nameof(writeFace));
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _projection = projection ?? throw new ArgumentNullException(nameof(projection));
            _outcomeTable = outcomeTable ?? throw new ArgumentNullException(nameof(outcomeTable));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            // ── D5 · D-1 契约(读流与写流同一引用)──────────────────────────────
            // 差额判据(`SelectPending`)读的是 `stream` 参数里的已发后果,而写入落在
            // 注入的 `IEventSink`。两者若是**两条流**,就是「读一个世界、写另一个世界」:
            // 每轮都认为「还没发过」⇒ **每轮重复发**,且无任何报错。故构造期 fail-loud。
            // ⚠️ 判据放这里而不是放进 Emitter:Emitter 只拿到 `IEventSink`,看不到它的读面,
            //    「同源」在 Emitter 里**无法表达**(ADR-036 D5 原文)。
            if (!ReferenceEquals(stream, writeFace))
                throw new ArgumentException(
                    "读流与写流不是同一实例 —— 后果的幂等差额读的是写面之外的流," +
                    "会导致每 tick 重复发出(静默)。生产装配须把同一个 EventStream 同时作读面与写面" +
                    "(ADR-036 D5)。", nameof(writeFace));

            _diseaseKeyById = BuildKeyMap(registry);
            _treatableBy = treatableBy;          // null = 未接线:命中位恒 false(C 轴退化为 Unmatched)
            _worldSeed = unchecked((long)worldSeed);
        }

        /// <summary>
        /// 一个 tick 边沿的驱动:`Aggregate → Judge → Emit`。
        /// </summary>
        /// <param name="tick">本 tick(须 ≥ 已发出行的最大 tick;越界由发射器自守)。</param>
        /// <returns>发射器的结果(测试与遥测的观察面)。</returns>
        /// <exception cref="InvalidOperationException">同一 `CaseClosed` 被投影出**两个不同** outcome
        /// (ADR-036 D6 上层)—— 那是上游 bug,静默吞掉会让「为什么后果错了」永远查不出来。</exception>
        public ConsequenceEmitResult OnTickEdge(long tick)
        {
            IReadOnlyList<SimEvent> events = _stream.Events;

            // ── ① 输入聚合(纯函数;D 经 9 的投影按**结案 tick** 取)──
            IReadOnlyList<Inputs53> groups = ConsequenceInputAggregator.Aggregate(
                events, _decoder, _projection, ConsequenceInputAggregator.ProductionCareKinds);

            // ── ② 逐病例判定 ──────────────────────────────────────────────
            // ⚠️ **试药史每 tick 只算一次**(2026-10-30 V-7 实测后):逐例调 `HasTrialHistory`
            //   每次都会复制并全排序整条流 ⇒ 1000 例 × 2000 事件把单 tick 推到 **1135 ms**
            //   (预算 50 ms)。现改为每 tick 扫一遍得「有痕迹的病人集合」,逐例查集合。
            HashSet<int> trialPatients = TrialHistoryPredicate.PatientsWithTrialHistory(events);

            var eligible = new List<(SimEvent CaseClosed, int Outcome)>(groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                Inputs53 g = groups[i];
                Outcome53.Outcome outcome = Outcome53.Judge.Evaluate(
                    BuildJudgeInputs(g, events, trialPatients), _outcomeTable);
                eligible.Add((g.CaseClosed, (int)outcome));
            }

            // ── ③ D6 上层:同一结案不得有两个 outcome ────────────────────────
            AssertNoConflictingOutcomes(eligible);

            // ── ④ 发出(主机唯一写 / 延迟窗 / 幂等差额全在 Emitter 内)──────
            return _emitter.Emit(events, eligible, tick, out _);
        }

        /// <summary>
        /// 把一组入向闭集投影成判定输入(`P / C / D / U` 三轴 + 试药史位)。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ <b>这是 ADR-036 未写明的桥</b>:ADR 只写了「driver 内调 `Judge.Evaluate`」,
        /// 而 `Judge.Evaluate` 要的是 <see cref="Outcome53.JudgeInputs"/>、不是
        /// <see cref="Inputs53"/>。桥的三个取值口径由 2026-10-30 用户裁定:</para>
        /// <list type="number">
        /// <item><b>C 轴命中位</b>:`∃ care: axis.TryGetPolarity(病种 key, care.ActionId)`
        /// —— 按轴查(`D-9-K` 单一真源),不读载荷极性。</item>
        /// <item><b>U 的盐</b>:世界级 `PatternRecognized` 中「盐 == `ComputeSaltedKey(seed, bit)`
        /// 且 `bit ∈` 本病例 `DiseaseSet`」的第一条(bit 升序);无一命中 ⇒ 退化中点 32768
        /// (GDD 明文口径)。</item>
        /// <item><b>试药史位</b>:**逐病人**(<see cref="TrialHistoryPredicate.HasTrialHistory(IReadOnlyList{SimEvent}, PatientId)"/>)
        /// —— 流级口径会让甲的连续用药污染乙的结案。</item>
        /// </list>
        /// </remarks>
        /// <param name="g">一组入向闭集。</param>
        /// <param name="events">流快照(仅供逐例兜底计算试药史;正常路径用 <paramref name="trialPatients"/>)。</param>
        /// <param name="trialPatients">本 tick 的「有试药史痕迹的病人集」;
        /// <c>null</c> ⇒ 逐例现算(**O(n log n) / 例**,仅供单次调用 / 夹具)。</param>
        public Outcome53.JudgeInputs BuildJudgeInputs(
            Inputs53 g, IReadOnlyList<SimEvent> events, HashSet<int> trialPatients = null)
        {
            var closed = _decoder.Decode<CaseClosedPayload>(EventKind.CaseClosed, g.CaseClosed.Payload);

            Outcome53.JudgmentLane p = Outcome53.JudgmentLaneOf(g.JudgmentRecords);
            Outcome53.CareLane c = Outcome53.CareLaneOf(g.CareRecords, CareMatchesAxis(closed.DiseaseSet, g.CareRecords));
            Outcome53.RecoveryLane d = Outcome53.RecoveryLaneOf(g.DiseaseProjection);
            bool trial = trialPatients != null
                ? trialPatients.Contains(g.CaseClosed.Patient.Value)
                : TrialHistoryPredicate.HasTrialHistory(events, g.CaseClosed.Patient);

            ulong uRaw = TryResolveSaltedKey(closed.DiseaseSet, g.PatternRecognized, out ulong saltedKey)
                ? OutcomeJudgeDerive.URaw(_worldSeed, saltedKey)
                : OutcomeJudgeDerive.URawDegraded();
            int band = Outcome53.BandOf(uRaw, _outcomeTable.UBandCount);

            return new Outcome53.JudgeInputs(p, c, d, trial, band, uRaw);
        }

        /// <summary>
        /// D6 上层判据:同一 `CaseClosed` 若被投影出**两个不同** outcome ⇒ 抛。
        /// </summary>
        /// <remarks>
        /// ⚠️ **今日该分支零输入可达**:<c>Aggregate</c> 对每条 `CaseClosed` 只产一组
        /// (`ConsequenceInputAggregator.cs:218-312` 外层逐事件),而同一条事件在同一 tick 内
        /// 的 D 也不可能两个值 ⇒ **冲突在本轮驱动里构造不出来**。它防的是**将来**:
        /// 一旦有人让 driver 从第二个来源(缓存 / 跨流)补 outcome,这里会立刻红。
        /// ⇒ 按 ADR-035 N-4 的纪律**如实登记为「零输入可达的门」**,不得宣称「已兑现守卫」。
        /// 其载体 = 直接调本方法的夹具(见 `medcons_driver_test`),**不是** `OnTickEdge` 的路径。
        /// </remarks>
        /// <param name="eligible">本轮合格行(结案事件 + 其判定 outcome)。</param>
        /// <exception cref="InvalidOperationException">同一结案出现两个不同 outcome。</exception>
        public static void AssertNoConflictingOutcomes(IReadOnlyList<(SimEvent CaseClosed, int Outcome)> eligible)
        {
            if (eligible == null) return;
            var seen = new Dictionary<(long, int, long), int>(eligible.Count);
            for (int i = 0; i < eligible.Count; i++)
            {
                SimEvent close = eligible[i].CaseClosed;
                var key = (close.Tick, close.Patient.Value, close.Seq);
                int outcome = eligible[i].Outcome;
                if (seen.TryGetValue(key, out int previous) && previous != outcome)
                    throw new InvalidOperationException(
                        $"同一 CaseClosed(tick={close.Tick}, patient={close.Patient.Value}, seq={close.Seq})" +
                        $"被投影出两个不同 outcome({previous} / {outcome})—— 判定不是纯函数或上游重复喂了" +
                        "不同输入(ADR-036 D6 上层)。静默取其一会让「为什么后果错了」永远查不出来。");
                seen[key] = outcome;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 内部判据
        // ══════════════════════════════════════════════════════════════════

        /// <summary>C 轴命中位:任一处置的动作 id 在本病例病种的 `treatable_by` 轴行里。</summary>
        private bool CareMatchesAxis(DiseaseIdSet diseaseSet, IReadOnlyList<CareRow53> cares)
        {
            if (_treatableBy == null || cares == null || cares.Count == 0) return false;

            for (int i = 0; i < cares.Count; i++)
            {
                int actionId = cares[i].ActionId;
                for (int bit = 0; bit < 64; bit++)
                {
                    if ((diseaseSet.Bits & (1UL << bit)) == 0UL) continue;
                    if (!_diseaseKeyById.TryGetValue(bit + 1, out string key) || key == null) continue;
                    if (_treatableBy.TryGetPolarity(key, actionId, out _)) return true;
                }
            }
            return false;
        }

        /// <summary>解本病例的 `salted_key`(bit 升序第一条命中;无命中 ⇒ false ⇒ 退化 U)。</summary>
        private bool TryResolveSaltedKey(
            DiseaseIdSet diseaseSet, IReadOnlyList<SimEvent> patterns, out ulong saltedKey)
        {
            saltedKey = 0UL;
            if (patterns == null || patterns.Count == 0) return false;

            for (int bit = 0; bit < 64; bit++)
            {
                if ((diseaseSet.Bits & (1UL << bit)) == 0UL) continue;

                ulong expected = PatternDetector.ComputeSaltedKey(_worldSeed, bit);
                for (int i = 0; i < patterns.Count; i++)
                {
                    var payload = _decoder.Decode<PatternRecognizedPayload>(
                        EventKind.PatternRecognized, patterns[i].Payload);
                    if (payload.SaltedKey != expected) continue;

                    saltedKey = payload.SaltedKey;
                    return true;
                }
            }
            return false;
        }

        private static IReadOnlyDictionary<int, string> BuildKeyMap(IReadOnlyList<DiseaseRegistryEntry> registry)
        {
            var map = new Dictionary<int, string>(registry.Count);
            for (int i = 0; i < registry.Count; i++)
            {
                DiseaseRegistryEntry e = registry[i];
                if (e == null) continue;
                map[e.DiseaseId] = e.DiseaseKey;
            }
            return map;
        }
    }
}
