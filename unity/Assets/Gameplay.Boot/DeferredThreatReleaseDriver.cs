// 延迟威胁的**收尾驱动** —— 补发 / 作废两支(AC-52-29 ② · GDD 规则六之三 · T22 批五 · 2026-11-11)
//
// ── 它接的是哪一段 ──────────────────────────────────────────────────────────
// 入槽那一半已由 `RandomEventSpawnChain` 接上(威胁档 ∧ 医馆内 ⇒ 抽一次、落 `EventRolled`、
// 把 key 记进 `DeferredThreatSlot`,**不落 `EventArrived`**)。本件管**槽里那条威胁后来怎样**:
//
//   GDD 规则六之三逐字:
//     当玩家离开医馆地块(当日之内)→ **立即补发**该威胁事件(消费当日预算)
//     若跨过 TICKS_PER_DAY 边界仍未补发 → 该 slot **作废,不结转**
//     补发时若当日预算已耗尽 → 则**丢弃**
//
// ── 两条设计的「为什么」──────────────────────────────────────────────────────
// ① **跨日作废不靠内存日界标志**:GDD 说「无 `DayBoundary` 事件」是真话,但没有日界事件
//    ≠ 判不了 —— 每条 `ThreatDeferred` 的**事件头 `Tick`** 就是它的挂起时刻,`Tick / TICKS_PER_DAY`
//    就是它的挂起日。⇒ 每边沿从流折叠一次「槽 → (key, 挂起日)」,凡挂起日 < 当前日即作废。
//    这样**读档后自动正确**(槽集与挂起日全在流里,零内存态),与 ADR-039 D2 的
//    「状态全部从事件流重构」同一条纪律。**不用**「记住上一边沿是第几天」的写法 ——
//    那是读档后不可重建的内存态(同一类错已在 `TD-sim-05` 上踩过)。
// ② **补发与作废都不另立状态机**:两条收尾都只是 `DeferredThreatSlotWriter.TryClear` 的调用,
//    槽表由 `EventBudget.FoldDeferEvent` 折叠流而更新。本件**不持槽表** ——
//    第二真源在那儿已被结构性拆过一次(`TD-randomevents-01`),不得从驱动后门回来。
//
// ── 求值顺序(每条都影响可观测结果)───────────────────────────────────────────
//   ① **先作废跨日槽,再补发** —— 否则昨天挂起的威胁会在今早出门时被补发,正是
//      「跨日不结转」要禁止的那件事。
//   ② **补发按槽位升序** —— 同一时刻多条待补时给确定性序列(不依赖字典枚举序)。
//   ③ **预算耗尽 ⇒ 逐条作废**(GDD「则丢弃」)。⚠️ `reason` 闭集只有 {0=补发,1=作废},
//      「因预算丢掉」没有第三个值 ⇒ 记 `ReasonVoided`,并在本行登记这个**词表缺口**:
//      若将来要区分「跨日作废」与「预算丢弃」,须扩 `reason` 值域(写者 + 折叠器 + registry 三处)。
//
// ── 与出诊启动共用同一次跨格判定 ────────────────────────────────────────────
// 「离开医馆」用 `PlayerCellCrossing.At` —— 与 `HouseCallStartedWriter` **同一个函数**,
// 不是照抄(`TD-randomevents-08` 解除动作 ② 的逐字要求;照抄会造出第二份判据)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一次边沿的收尾读数(观测接缝;全零 = 本边沿无事发生)。</summary>
    public readonly struct DeferredThreatReleaseReport
    {
        /// <summary>因跨日被作废的槽数。</summary>
        public readonly int VoidedCrossDay;
        /// <summary>被补发的威胁条数。</summary>
        public readonly int Reissued;
        /// <summary>因当日预算耗尽被丢弃的槽数。</summary>
        public readonly int VoidedBudgetExhausted;

        /// <summary>构造。</summary>
        public DeferredThreatReleaseReport(int voidedCrossDay, int reissued, int voidedBudgetExhausted)
        {
            VoidedCrossDay = voidedCrossDay;
            Reissued = reissued;
            VoidedBudgetExhausted = voidedBudgetExhausted;
        }

        /// <summary>本边沿是否真的写了东西。</summary>
        public bool WroteAnything => VoidedCrossDay + Reissued + VoidedBudgetExhausted > 0;
    }

    /// <summary>
    /// 延迟威胁收尾驱动(52;补发 / 跨日作废 / 预算耗尽丢弃)。
    /// </summary>
    public sealed class DeferredThreatReleaseDriver
    {
        /// <summary>`EventArrivedPayload.causeClueKey` 的「无」值(与本链入槽侧同)。</summary>
        public const int NoCauseClue = 0;

        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly IPayloadDecoder _decoder;
        private readonly IClinicCellSource _clinicCells;
        private readonly DeferredThreatSlotWriter _writer;
        private readonly Func<long, WorldPos> _spawnAnchorOf;
        /// <summary>玩家实体 id(晚绑定 —— 与导演 `BindPlayer` 同一时机;未绑定 ⇒ 不补发)。</summary>
        private int _playerActorId = -1;
        private readonly int _deferMax;
        private readonly long _ticksPerDay;
        private readonly int _baseEvents;
        private readonly int _budgetMax;

        /// <summary>构造。</summary>
        /// <param name="events">事件流只读视图(经闭包 —— 读档 `Restore` 换流后自动指向新内容)。</param>
        /// <param name="sink">事件写入通道(补发写 `EventArrived`;清槽经写者)。</param>
        /// <param name="encoder">载荷编码器(ADR-029)。</param>
        /// <param name="decoder">载荷解码器(ADR-031;折叠槽集与跨格读数都要它)。</param>
        /// <param name="clinicCells">医馆格判据源(**必须与服务袋同一实例** —— 同源义务)。</param>
        /// <param name="writer">挂起槽写者(清槽唯一出口)。</param>
        /// <param name="spawnAnchorOf">补发时的生成锚(tick → 整数格;与急召链同一张表)。</param>
        /// <param name="deferMax">挂起槽上界 `DEFER_MAX`。</param>
        /// <param name="ticksPerDay">日长(补发窗口的边界)。</param>
        /// <param name="baseEvents">密度预算 `BASE`。</param>
        /// <param name="budgetMax">密度预算 `BUDGET_MAX`。</param>
        /// <exception cref="ArgumentNullException">任一必需依赖为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">数值越界。</exception>
        public DeferredThreatReleaseDriver(
            Func<IReadOnlyList<SimEvent>> events,
            IEventSink sink,
            IPayloadEncoder encoder,
            IPayloadDecoder decoder,
            IClinicCellSource clinicCells,
            DeferredThreatSlotWriter writer,
            Func<long, WorldPos> spawnAnchorOf,
            int deferMax,
            long ticksPerDay,
            int baseEvents,
            int budgetMax)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _clinicCells = clinicCells ?? throw new ArgumentNullException(nameof(clinicCells));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _spawnAnchorOf = spawnAnchorOf ?? throw new ArgumentNullException(nameof(spawnAnchorOf));
            if (deferMax <= 0)
                throw new ArgumentOutOfRangeException(nameof(deferMax), deferMax, "DEFER_MAX 必须 > 0");
            if (ticksPerDay <= 0)
                throw new ArgumentOutOfRangeException(nameof(ticksPerDay), ticksPerDay,
                    "TICKS_PER_DAY 必须 > 0(日界判据的除零)");
            if (baseEvents < 0 || budgetMax < 0)
                throw new ArgumentOutOfRangeException(nameof(baseEvents),
                    $"BASE({baseEvents}) / BUDGET_MAX({budgetMax}) 须 ≥ 0");
            _deferMax = deferMax;
            _ticksPerDay = ticksPerDay;
            _baseEvents = baseEvents;
            _budgetMax = budgetMax;
        }

        /// <summary>玩家 actorId 是否已绑定(未绑定 ⇒ **跨日作废照跑**,补发不跑)。</summary>
        public bool IsPlayerBound => _playerActorId >= 0;

        /// <summary>
        /// 绑定玩家 actorId(晚绑定 —— 玩家 id 在装配之后才铸造,与导演 `BindPlayer` 同源同时机)。
        /// </summary>
        /// <param name="playerActorId">玩家实体 id(≥ 0)。</param>
        /// <exception cref="ArgumentOutOfRangeException">id 为负。</exception>
        /// <exception cref="InvalidOperationException">重复绑定(玩家 id 单调铸造,只许一次)。</exception>
        public void BindPlayer(int playerActorId)
        {
            if (playerActorId < 0)
                throw new ArgumentOutOfRangeException(nameof(playerActorId), playerActorId,
                    "玩家实体 id 不得为负 —— PatientId.None(-1)是世界级事件哨兵,不是玩家");
            if (_playerActorId >= 0)
                throw new InvalidOperationException("玩家 actorId 重复绑定 —— 只许一次(换源须换新装配)");
            _playerActorId = playerActorId;
        }

        /// <summary>
        /// 本 tick 边沿的收尾:先作废跨日槽,再在「离开医馆」这一刻补发。
        /// </summary>
        /// <param name="tick">当前逻辑 tick。</param>
        /// <returns>本边沿的写入读数。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        /// <exception cref="InvalidDataException">流损坏(槽集折叠时同槽双占 / 清未占用槽)。</exception>
        public DeferredThreatReleaseReport OnTickEdge(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");

            IReadOnlyList<SimEvent> events = _events() ?? Array.Empty<SimEvent>();

            // 槽 → (key, 挂起 tick)。全部从流折叠(见文件头注 ①)。
            Dictionary<int, DeferredEntry> open = FoldOpenSlots(events);
            if (open.Count == 0) return default;

            long today = tick / _ticksPerDay;

            // ① 跨日作废(升序,确定性)
            var slots = new List<int>(open.Keys);
            slots.Sort();
            int voidedCrossDay = 0;
            foreach (int slot in slots)
            {
                if (open[slot].DeferTick / _ticksPerDay >= today) continue;
                _writer.TryClear(tick, slot, DeferredThreatSlotWriter.ReasonVoided, _deferMax);
                open.Remove(slot);
                voidedCrossDay++;
            }
            if (open.Count == 0)
                return new DeferredThreatReleaseReport(voidedCrossDay, 0, 0);

            // ② 补发:只在「本 tick 从医馆格跨出」这一刻。**未绑玩家 ⇒ 不补发**(不是错误态:
            //    玩家 id 在装配之后才铸造,这段间隙里既没有跨格事件也没有可补发的对象)。
            if (!IsPlayerBound)
                return new DeferredThreatReleaseReport(voidedCrossDay, 0, 0);

            PlayerCellCrossing crossing = PlayerCellCrossing.At(events, _decoder, _playerActorId, tick);
            if (!crossing.CrossedOutOf(_clinicCells))
                return new DeferredThreatReleaseReport(voidedCrossDay, 0, 0);

            int spawnsToday = CountArrivedToday(events, tick);
            int budgetPerDay = EventBudget.ComputeDailyBudget(_baseEvents, null, _budgetMax);
            int reissued = 0, voidedBudget = 0;

            slots.Clear();
            slots.AddRange(open.Keys);
            slots.Sort();
            foreach (int slot in slots)
            {
                if (spawnsToday < budgetPerDay)
                {
                    int key = open[slot].EventKey;
                    WorldPos anchor = _spawnAnchorOf(tick);
                    _sink.Append(new SimEvent(tick, PatientId.None, -1, EventKind.EventArrived,
                        _encoder.Encode(EventKind.EventArrived,
                            new EventArrivedPayload(key, (int)EventTier.Threat, anchor, NoCauseClue))));
                    _writer.TryClear(tick, slot, DeferredThreatSlotWriter.ReasonReissued, _deferMax);
                    spawnsToday++;
                    reissued++;
                }
                else
                {
                    // GDD:「补发时若当日预算已耗尽,则丢弃」—— 槽必须清掉,不能留到明天(不结转)。
                    _writer.TryClear(tick, slot, DeferredThreatSlotWriter.ReasonVoided, _deferMax);
                    voidedBudget++;
                }
            }

            return new DeferredThreatReleaseReport(voidedCrossDay, reissued, voidedBudget);
        }

        private readonly struct DeferredEntry
        {
            public readonly int EventKey;
            public readonly long DeferTick;
            public DeferredEntry(int eventKey, long deferTick) { EventKey = eventKey; DeferTick = deferTick; }
        }

        /// <summary>
        /// 从流折叠出**仍挂起**的槽(槽 → key + 挂起 tick)。
        /// </summary>
        /// <remarks>
        /// 逐条解 `ThreatDeferred` / `ThreatDeferralCleared`,与 `EventBudget.FoldDeferEvent` 的
        /// 状态机同构;差别只在本方法**额外保留挂起 tick**(折叠器只保 key —— 那对预算账够用,
        /// 对「跨日」不够)。⚠️ 不在这里重新发明占用校验:同槽双占 / 清未占用槽由
        /// `EventBudget.ReconstructDeferSlots` 的 fail-loud 负责,**本方法先跑一遍它**。
        /// </remarks>
        private Dictionary<int, DeferredEntry> FoldOpenSlots(IReadOnlyList<SimEvent> events)
        {
            // 先让生产折叠器把「流是否损坏」判一遍(同槽双占 / 清未占用槽 ⇒ 抛)。
            // 判完丢弃它的结果 —— 本方法要的是同一状态机**外加**挂起 tick。
            EventBudget.ReconstructDeferSlots(events, _decoder, _deferMax);

            var open = new Dictionary<int, DeferredEntry>();
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind == EventKind.ThreatDeferred)
                {
                    ThreatDeferredPayload p =
                        _decoder.Decode<ThreatDeferredPayload>(e.Kind, e.Payload);
                    open[p.SlotIndex] = new DeferredEntry(p.EventKey, e.Tick);
                }
                else if (e.Kind == EventKind.ThreatDeferralCleared)
                {
                    ThreatDeferralClearedPayload p =
                        _decoder.Decode<ThreatDeferralClearedPayload>(e.Kind, e.Payload);
                    open.Remove(p.SlotIndex);
                }
            }
            return open;
        }

        /// <summary>当日已降临条数(`EventArrived`,预算口径与急召链同一算法)。</summary>
        private int CountArrivedToday(IReadOnlyList<SimEvent> events, long tick)
        {
            long dayStart = tick - (tick % _ticksPerDay);
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.EventArrived) continue;
                if (e.Tick < dayStart || e.Tick >= dayStart + _ticksPerDay) continue;
                count++;
            }
            return count;
        }
    }
}
