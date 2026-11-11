// 52 **急召生成链**(抽池 → 掷骰 → F1 → `EventArrived`)—— S22-4 / smoke `#14` 的载体
//
// 权威来源:
//   GDD random-events.md `:106` —— 「时机轴 → 抽池(内容轴,按 key 过滤) → 强度轴定档 →
//     密度轴查预算 → 生成」
//   ADR-007 §三 —— 掷骰四项进流(`EventRolled`),读档可重建
//   ADR-030 —— 病人出现 = `DiseaseOnset`,写者 = 9(`PatientSpawner`)
//   ADR-008 §六 / TR-disease-021 —— `PATIENT_APPEARANCE_CAP`(24)是**硬界**
//   `RandomEventsDirectorDriver` 头注「急召生成链」具名槽 —— 本件即那一槽的落地
//
// ▍本件钉住的一条性质(卡面要的「CAP 两侧」)
//   **超界 ⇒ 零副作用 + 具名报错**:CAP 预检排在**任何 Append 之前**
//   ⇒ 被拒时流里既没有 `EventRolled` 也没有 `EventArrived`。
//   若把预检放到 Append 之后,`EventRolled` 会先落流,再被 `EventStream` 的 AC-15 拒掉后半截
//   ⇒ 流里留下「掷过骰但事件没到」的孤儿行,读档重放时会**凭空多一次掷骰**。
//   ⚠️ 这是本件**唯一**的原子性主张;病人创建本身仍由 `PatientSpawner` 走 `EventStream.Append`,
//      那道 AC-15 是**第二道**门(纵深),不是本件依赖的那一道。
//
// 🔁 **2026-11-03 ADR-039 —— 52 运行期消费落地**:四个数据集值自此有真实读方 ——
//    `ROLL_INTERVAL`(子区间边界门)· `WINDOW_SIZE`(F1 配额总数 + 当日抽取上限)·
//    `BASE` / `BUDGET_MAX`(`EventBudget.ComputeDailyBudget` 的当日降临上限)。
//    状态**全部从事件流重构**(当日 `EventRolled` / `EventArrived` 计数)——
//    删除原 `_lastWindow` / `_ordinalInWindow` 两个读档后不可重建的内存字段。
//    ⚠️ ~~生产今日候选池仍为空 ⇒ **恒 `NoPool`**~~ **已过期(2026-11-03 池条目件落地)**:
//    候选池非空;~~恒 `NoContext`~~ **亦已过期(2026-11-05 `TD-randomevents-05`)** ——
//    上下文门改为档级后,链在生产里**真的会落流**;今天挡在最后一步的是
//    **`medical` 条目的病种映射不存在**(`PatientSpawnUnavailable`,`TD-randomevents-03`)。
//
// ▍今天**没做**的(逐条具名,不借绿)
//   · **密度预算的 mods 输入**(F2 的 `Mod_Eco` / `Mod_Rep`)—— P0 无输入源 ⇒ 今日 `mods = ∅`。
//   · **权重 / 历史标记乘子** —— 见 `EventDraw` 头注。
//   · **生成点解析** —— `spawnAnchorOf` 由装配方注入(6 的出生锚;测试传合成格);
//     生产今日是 `(0,0,0)` 具名占位(6 的出生锚解析归 P1a 关卡)。
//   · **`medical` 条目的病种映射** —— 归 37 内容件(`TD-randomevents-03`)。
//     ⇒ 生产今日:`encounter` 条目**真落两条事件**;`medical` 条目走
//     `PatientSpawnUnavailable`(**零写入**,不是孤儿行)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>急召生成链的单次结果(失败面全部**具名**,不返回 bool)。</summary>
    public enum SpawnChainOutcome
    {
        /// <summary>已掷骰、已落 `EventRolled` + `EventArrived`;需要病人的条目已请求病人出现
        /// (`handler = encounter` 的条目不产病人 —— 2026-11-05)。</summary>
        Spawned = 0,

        /// <summary>候选池为空(生产今日)⇒ 零写入(诚实 fail-safe,不是缺陷)。</summary>
        NoPool = 1,

        /// <summary>F1 上下文门未过 ⇒ 零写入。</summary>
        NoContext = 2,

        /// <summary>F1 该档本窗口配额为 0 ⇒ 零写入。</summary>
        NoQuota = 3,

        /// <summary>未到 `ROLL_INTERVAL` 子区间边界 ⇒ 本 tick 不求值(ADR-039 D1)。</summary>
        NotAtBoundary = 4,

        /// <summary>当日抽取条数已达 `WINDOW_SIZE` ⇒ 零写入(ADR-039 D1 / F1 步骤①)。</summary>
        DrawCapReached = 5,

        /// <summary>当日降临数已达 `clamp(BASE,∅,BUDGET_MAX)` ⇒ 零写入(ADR-039 D1 / F2)。</summary>
        OverBudget = 6,

        /// <summary>该档 `ΣW = 0`(全部条目权重归零)⇒ 跳过本轮、**不掷骰、不报错**
        /// (GDD Edge Cases `:1065`;`KeyGate` 全封或全档 `TODMult = 0` 都会触发)。</summary>
        ZeroWeight = 7,

        /// <summary>
        /// **威胁档 ∧ 医馆内 ⇒ 延后**(AC-52-29 ② / GDD 规则六之三):本 tick **抽了骰**、
        /// 落了 `EventRolled`,但**不落 `EventArrived`、不产病人**,改把抽中的 key 记进
        /// `DeferredThreatSlot`(一条 `ThreatDeferred`)。
        /// <para>⚠️ 这与 <see cref="Spawned"/> 的区别是「到没到」,不是「抽没抽」——
        /// 用于观测「延后支真的走了」。</para>
        /// </summary>
        ThreatDeferred = 9,

        /// <summary>
        /// 该延后而 `DEFER_MAX` 个挂起槽全被占 ⇒ **零写入**(连 `EventRolled` 都不落)。
        /// <para>排空槽这一步**必须**在掷骰之前 —— 否则留下「掷过骰、没入槽、也没降临」的孤儿行
        /// (与本类 ⑥ CAP 预检同一条纪律)。</para>
        /// </summary>
        DeferSlotsFull = 10,

        /// <summary>
        /// 抽中的是**需要病人**的条目,而「病人出现」这条通道今日不可用
        /// (`handler = medical` → 37 病种映射表尚不存在,`TD-randomevents-03`)⇒ **零写入**。
        /// <para>🔁 2026-11-05(`TD-randomevents-05` 同批):改前该形态是**先 Append 两条、
        /// 再在 `spawnPatient` 抛** —— 流里留下「掷过骰、降临了、没人」的孤儿行,违反本类
        /// ⑥ CAP 预检立下的同一条纪律(「失败必须排在第一次 `Append` 之前」)。</para>
        /// </summary>
        PatientSpawnUnavailable = 8,
    }

    /// <summary>52 急召生成链:抽池 → 掷骰 → F1 → `EventArrived` → 病人出现。</summary>
    public sealed class RandomEventSpawnChain
    {
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly IEventAuthority _authority;
        private readonly Func<int, long, IReadOnlyList<WeightedKey>> _candidatesOfTier;
        private readonly IPresenceQuery _presence;
        private readonly Func<long, WorldPos> _spawnAnchorOf;
        private readonly Func<int, long, PatientId> _spawnPatient;
        private readonly Func<long, F1PipelineInput> _contextOf;
        private readonly Func<int, bool> _requiresPatient;
        private readonly Func<int, bool> _canSpawnPatient;
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly int _tier;
        private readonly long _rollInterval;
        private readonly int _windowSize;
        private readonly int _baseEvents;
        private readonly int _budgetMax;
        private readonly long _ticksPerDay;
        private readonly int _causeClueKey;

        // ── 延迟威胁(AC-52-29 ②;三者任一为 null / ≤ 0 ⇒ 「未接线」,行为与接线前逐位相同)──
        private readonly DeferredThreatSlotWriter _deferWriter;
        private readonly IPayloadDecoder _deferDecoder;
        private readonly int _deferMax;

        /// <param name="sink">事件写入通道。</param>
        /// <param name="encoder">载荷编码器(ADR-029)。</param>
        /// <param name="authority">掷骰权威(生产 = <see cref="SeedEventAuthority"/>)。</param>
        /// <param name="candidatesOfTier">(档, roll tick) → **加权**候选集(抽池的「按 key 过滤 + F1 步骤② 权重」面)。
        /// <para>⚠️ 权重由装配方按 `EventWeighting.CandidatesOfTier` 算好再交进来 ——
        /// 本链**不自己算权重**:它需要昼夜与历史,而那两个是别处的状态(与 `contextOf` 同一条纪律)。</para></param>
        /// <param name="presence">在场读面 —— CAP 预检用,**必须与 `EventStream` 同一实例**。</param>
        /// <param name="spawnAnchorOf">生成点解析(tick → 整数格);6 的出生锚,测试传合成格。</param>
        /// <param name="spawnPatient">病人出现(tick → 新病人 id);生产 = `PatientSpawner.SpawnNext`。</param>
        /// <param name="contextOf">F1 上下文输入(**本次掷骰 tick** → 上下文)—— **由装配方给**
        /// (夜 / 出诊路径 / 医馆 / 战斗冷却 / 季节全是别处的状态,本链不自己猜;
        /// 内部硬编码 = 造假的上下文)。
        /// <para>🔁 2026-11-05(`TD-randomevents-05`):实参由**窗口起点**改为**当前 tick**
        /// —— 玩家格必须按此刻算(拿一天前的起点 = 一天前的位置)。</para>
        /// <para>⚠️ 上下文「给不全」的判决已随同批修正改为**档级**:医馆只压威胁档
        /// (`Quotas[威胁] == 0` ⇒ `NoQuota`),机会 / 反应档照常(AC-52-29);
        /// 链路恒 `NoContext` 只在**战斗冷却**时成立。</para></param>
        /// <param name="tier">本链抽取的档 id。</param>
        /// <param name="config">52 的烘焙配置(`ROLL_INTERVAL` / `WINDOW_SIZE` / `BASE` / `BUDGET_MAX` / `TICKS_PER_DAY`)。</param>
        /// <param name="events">事件流只读视图 —— 当日计数与掷骰序号的**唯一来源**(ADR-039 D2)。</param>
        /// <param name="causeClueKey">因由线索 key(`EventArrivedPayload.causeClueKey`;0 = 无)。</param>
        public RandomEventSpawnChain(
            IEventSink sink,
            IPayloadEncoder encoder,
            IEventAuthority authority,
            Func<int, long, IReadOnlyList<WeightedKey>> candidatesOfTier,
            IPresenceQuery presence,
            Func<long, WorldPos> spawnAnchorOf,
            Func<int, long, PatientId> spawnPatient,
            Func<long, F1PipelineInput> contextOf,
            Func<IReadOnlyList<SimEvent>> events,
            int tier,
            in RandomEventsDataSet config,
            int causeClueKey = 0,
            Func<int, bool> requiresPatient = null,
            Func<int, bool> canSpawnPatient = null,
            DeferredThreatSlotWriter deferWriter = null,
            IPayloadDecoder deferDecoder = null,
            int deferMax = 0)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            _candidatesOfTier = candidatesOfTier ?? throw new ArgumentNullException(nameof(candidatesOfTier));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _spawnAnchorOf = spawnAnchorOf ?? throw new ArgumentNullException(nameof(spawnAnchorOf));
            _spawnPatient = spawnPatient ?? throw new ArgumentNullException(nameof(spawnPatient));
            _contextOf = contextOf ?? throw new ArgumentNullException(nameof(contextOf));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            // 两者皆可 null(= 旧语义:每条都算「需要病人」,且不预检)——
            // 既有夹具不传它们,行为一字不变。
            _requiresPatient = requiresPatient;
            _canSpawnPatient = canSpawnPatient;
            if (config.RollInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(config), config.RollInterval,
                    "ROLL_INTERVAL 须 > 0(边界门除零)");
            if (config.WindowSize < 1)
                throw new ArgumentOutOfRangeException(nameof(config), config.WindowSize,
                    "WINDOW_SIZE 须 ≥ 1(=0 则永不抽取,功能死亡)");
            if (config.TicksPerDay <= 0)
                throw new ArgumentOutOfRangeException(nameof(config), config.TicksPerDay,
                    "TICKS_PER_DAY 须 > 0(预算窗口除零)");
            if (config.BudgetMax < 0 || config.Base < 0)
                throw new ArgumentOutOfRangeException(nameof(config),
                    $"BASE({config.Base}) / BUDGET_MAX({config.BudgetMax}) 须 ≥ 0");
            _tier = tier;
            _rollInterval = config.RollInterval;
            _windowSize = config.WindowSize;
            _baseEvents = config.Base;
            _budgetMax = config.BudgetMax;
            _ticksPerDay = config.TicksPerDay;
            _causeClueKey = causeClueKey;
            // 三个一起给才算「接线」—— 缺一个就整体关掉,不做半接(半接会让「延后」在某些
            // 分支悄悄退化成 `NoQuota`,而那种退化**没有报文**)。
            if (deferWriter != null && deferDecoder != null && deferMax > 0)
            {
                _deferWriter = deferWriter;
                _deferDecoder = deferDecoder;
                _deferMax = deferMax;
            }
        }

        /// <summary>
        /// 本 tick 尝试一次急召生成。**超界 ⇒ 抛**(抛前零写入)。
        /// </summary>
        /// <param name="tick">当前逻辑 tick。</param>
        /// <returns>成功 / 三种**零写入**的具名失败。</returns>
        /// <exception cref="InvalidOperationException">
        /// 在场病人数已达 <see cref="EventStream.PATIENT_APPEARANCE_CAP"/> —— 报文与 `EventStream`
        /// 的 AC-15 同款(同一个界,同一个说法),但**发生在任何 Append 之前**。
        /// </exception>
        public SpawnChainOutcome TrySpawnAtTick(long tick)
        {
            // ① 子区间边界门(ADR-039 D1 · GDD 规则一「隔多久看一眼」)
            //    每个 ROLL_INTERVAL 求值一次,不是每帧/每 tick。
            if (tick % _rollInterval != 0L) return SpawnChainOutcome.NotAtBoundary;

            // ② 当日计数 —— **全部从事件流重构**(ADR-039 D2 · ADR-007 §三)
            long dayStart = tick - (tick % _ticksPerDay);
            IReadOnlyList<SimEvent> events = _events() ?? Array.Empty<SimEvent>();
            int rollsToday = 0;
            int spawnsToday = 0;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Tick < dayStart || e.Tick >= dayStart + _ticksPerDay) continue;
                if (e.Kind == EventKind.EventRolled) rollsToday++;
                else if (e.Kind == EventKind.EventArrived) spawnsToday++;
            }

            // ③ 抽数上限(WINDOW_SIZE · F1 步骤①)与密度预算(F2 · 规则六)
            //    ⚠️ 两道门都排在**掷骰之前** —— 与 CAP 预检同一理由:
            //    `EventRolled` 先落流、再被预算拒掉后半截 = 孤儿行(ADR-039 D3)。
            if (rollsToday >= _windowSize) return SpawnChainOutcome.DrawCapReached;

            int budgetPerDay = EventBudget.ComputeDailyBudget(_baseEvents, null, _budgetMax);
            if (spawnsToday >= budgetPerDay) return SpawnChainOutcome.OverBudget;

            // ④ 抽池:空池 ⇒ 零写入(fail-safe)
            IReadOnlyList<WeightedKey> candidates = _candidatesOfTier(_tier, tick);
            if (candidates == null || candidates.Count == 0) return SpawnChainOutcome.NoPool;

            // ⑤ F1:上下文门 + 档配额(排在掷骰之前 —— 配额为 0 就不该掷)
            //    窗口 = **预算窗口起点**(一天),不是 tick 子窗口(ADR-039 D2)。
            // 🔁 2026-11-05(`TD-randomevents-05`):闭包实参由**窗口起点**改为**当前 tick**。
            //    玩家格与昼夜都必须按「此刻」算 —— 拿一天前的起点会得到一天前的位置,
            //    而 `IsInClinic` 正是玩家位置。`Window` 仍由下方按 `dayStart` 覆盖(ADR-039 D2)。
            F1PipelineInput raw = _contextOf(tick);
            var input = new F1PipelineInput(
                dayStart, _windowSize, raw.IsNight, raw.IsOnExpedition, raw.IsInClinic,
                raw.InCombatCooldown, raw.SeasonIndex);
            LastF1Input = input;
            F1PipelineOutput f1 = EventF1Pipeline.Execute(input, minTier: 0, maxTier: 3);
            if (!f1.ContextGatePassed) return SpawnChainOutcome.NoContext;

            // ⑤之二 **AC-52-29 ② · 医馆内威胁档:压制之后「延后」,不是「丢弃」**
            //   GDD 规则六之三:`若 威胁档到期 且 玩家在医馆地块内:该抽取被 ContextMult = 0 压制,
            //   不生成;记入 DeferredThreatSlot`。⇒ 判据顺序上,**这一支必须排在 `NoQuota` 之前**:
            //   入槽本身是一条事件(`ThreatDeferred`),而 `NoQuota` 是**零写入**分支 ——
            //   先返回 NoQuota 就永远走不到入槽(`TD-randomevents-08` 解除动作①)。
            //
            //   ⚠️ **抽中的 key 从哪来**:载荷 `{slot_index, event_key}` 要求一个具体 key,
            //      而配额被压成 0 就不该抽 ⇒ 两者表面冲突。本实现取「**仍抽一次、只是不降临**」:
            //      「该抽取被压制」这句的主语是**那一次抽取**,压的是它的**结果落地**。
            //      代价 = 该次抽取占当日 `WINDOW_SIZE` 的一个名额(它确实掷了骰,`EventRolled` 可重建)。
            //      → 这是本件**首定**的口径,若 52 后续裁定「压制时不应占用抽数」,改这一处。
            //
            //   ⚠️ `isSameDay: true` 是**由构造保证**的:槽在本 tick 写入,而跨日作废由
            //      `DeferredThreatReleaseDriver` 按「槽的 tick 日 < 当前日」逐槽发
            //      `ThreatDeferralCleared(作废)`(无 `DayBoundary` 事件,故走流内 tick 日比较)。
            bool clinicSuppressedThreat =
                _tier == (int)EventTier.Threat
                && EventBudget.ShouldDefer(EventTier.Threat, raw.IsInClinic, isSameDay: true);

            int deferSlot = -1;
            if (f1.Quotas[_tier] <= 0)
            {
                // 未接线 / 非该形态 ⇒ 维持原语义(零写入)
                if (!clinicSuppressedThreat || _deferWriter == null) return SpawnChainOutcome.NoQuota;

                // 空槽必须在**掷骰之前**确认 —— 否则留下「掷过骰、没入槽、也没降临」的孤儿行
                if (!TryAllocateDeferSlot(events, out deferSlot)) return SpawnChainOutcome.DeferSlotsFull;
            }

            // ⑥ **CAP 预检 —— 必须排在第一次 Append 之前**
            //    失效模式(不这么做):`EventRolled` 先落流,后半截被 AC-15 拒 ⇒ 流里留下
            //    「掷过骰、事件没到」的孤儿行,读档重放时凭空多一次掷骰。
            if (_presence.PresentCount >= EventStream.PATIENT_APPEARANCE_CAP)
                throw new InvalidOperationException(
                    $"AC-15: 在场病人数已达上限 {EventStream.PATIENT_APPEARANCE_CAP}，拒收新病人");

            // ⑦ 掷骰(序号 = 当日第 n 次,**从流计数**;四项全部落流,可重建)
            //    ⚠️ `ΣW = 0`(该档全部条目权重归零)⇒ 本档**跳过本轮、不掷骰、不报错**
            //    (GDD Edge Cases `:1065`)。判定排在掷骰之前 —— 与预算门同一理由(孤儿行)。
            long totalWeight = 0L;
            for (int i = 0; i < candidates.Count; i++) totalWeight += candidates[i].Weight;
            if (totalWeight <= 0L) return SpawnChainOutcome.ZeroWeight;

            //    `Tick` = 本次掷骰的 tick —— 权重的昼夜项按它算(四参形),且它已可从
            //    落流的 `EventRolled`(`SimEvent.Tick`)重建 ⇒ 不引入不可重构输入。
            EventRollResult roll = _authority.Roll(new RollRequest(dayStart, _tier, rollsToday, tick));

            // ⑦之二 **病人通道预检 —— 必须在第一次 Append 之前**(与 ⑥ 同一条纪律)。
            //    🔁 2026-11-05(`TD-randomevents-05`):`EvaluateContextGate` 改成档级之后,
            //    链路能走到这里了,而旧序是「先 Append 两条、再在 `spawnPatient` 抛」
            //    ⇒ 流里留下「掷过骰、降临了、没人」的孤儿行。现改为**先判后写**。
            //    ⚠️ 掷骰本身是**纯函数**(`SeedEventAuthority.Roll` 无状态),在此早退不改随机序。
            //    ⚠️ `requiresPatient == null` ⇒ 沿用旧语义(每条都要病人);生产恒传。
            bool needsPatient = _requiresPatient == null || _requiresPatient(roll.ChosenKey);
            if (needsPatient && _canSpawnPatient != null && !_canSpawnPatient(roll.ChosenKey))
                return SpawnChainOutcome.PatientSpawnUnavailable;

            // ⑦之三 **延后支**:只落骰,不落降临、不产病人,改把 key 记进挂起槽。
            //   ⚠️ 未绑病人预检(⑦之二)在这里**同样生效** —— 挂起一条「永远补发不出来」的威胁
            //      只是把失败推迟到补发那一刻,且那时玩家已经离开医馆、更难解释。
            if (clinicSuppressedThreat)
            {
                _sink.Append(new SimEvent(tick, PatientId.None, -1, EventKind.EventRolled,
                    _encoder.Encode(EventKind.EventRolled,
                        new EventRolledPayload((int)roll.Win, roll.Tier, roll.Ordinal, roll.ChosenKey))));

                DeferredThreatWriteResult deferred =
                    _deferWriter.TryDefer(tick, deferSlot, roll.ChosenKey, _deferMax);
                if (deferred != DeferredThreatWriteResult.Applied)
                    // 预检已保证空槽存在 ⇒ 走到这儿是**不变量被破坏**(流损坏 / 并发写)。
                    // 不静默:孤儿 `EventRolled` 已经落流了,吞掉会让「延后」变成「丢失」。
                    throw new InvalidOperationException(
                        $"[52] 延后入槽失败({deferred}):预检选中的 slot {deferSlot} 在写入时不可用 —— " +
                        "流可能被并发改动(本链假定主机单线程写)");
                return SpawnChainOutcome.ThreatDeferred;
            }

            // ⑧ 落两条事件 —— 顺序固定(先骰后降临;AD-52-822「一次降临恰一条 EventArrived」)
            _sink.Append(new SimEvent(tick, PatientId.None, -1, EventKind.EventRolled,
                _encoder.Encode(EventKind.EventRolled,
                    new EventRolledPayload((int)roll.Win, roll.Tier, roll.Ordinal, roll.ChosenKey))));

            WorldPos anchor = _spawnAnchorOf(tick);
            _sink.Append(new SimEvent(tick, PatientId.None, -1, EventKind.EventArrived,
                _encoder.Encode(EventKind.EventArrived,
                    new EventArrivedPayload(roll.ChosenKey, roll.Tier, anchor, _causeClueKey))));

            // ⑨ 病人出现(9 的写者;自身仍受 `EventStream` 的 AC-15 把守 = 第二道门)
            //    🔁 2026-11-05:只有**需要病人**的条目才走这一步 —— `handler = encounter` 的
            //    条目(27 人形遭遇 / 野兽)不产病人。此前无条件调用是「病人通道」张冠李戴,
            //    也正是 `medical` 映射缺失时会让**遭遇条目**一起被那道刻意抛拖死的原因。
            if (needsPatient) _spawnPatient(roll.ChosenKey, tick);

            return SpawnChainOutcome.Spawned;
        }

        /// <summary>
        /// 找一个**空闲挂起槽**(升序取第一个空闲位)。
        /// </summary>
        /// <param name="events">事件流只读视图(槽集从流折叠 —— ADR-007 §三 唯一真源)。</param>
        /// <param name="slot">选中的槽位;无空槽时为 -1。</param>
        /// <returns>有空槽 ⇒ true。</returns>
        /// <remarks>
        /// ⚠️ 折叠用 `EventBudget.ReconstructDeferSlots`(**生产同一个折叠器**,不另写一套):
        /// 槽的占用 / 释放由 `ThreatDeferred` / `ThreatDeferralCleared` 两条事件承重,
        /// 本方法只读结果。流损坏(同槽双占 / 清未占用槽)时折叠器**抛** —— 不在这里吞。
        /// </remarks>
        private bool TryAllocateDeferSlot(IReadOnlyList<SimEvent> events, out int slot)
        {
            slot = -1;
            BudgetState state = EventBudget.ReconstructDeferSlots(events, _deferDecoder, _deferMax);
            for (int i = 0; i < _deferMax; i++)
            {
                if (!state.DeferSlots.ContainsKey(i)) { slot = i; return true; }
            }
            return false;
        }

        /// <summary>最近一次求值用的 F1 输入(观测接缝;`null` = 还没走到 F1)。</summary>
        /// <remarks>判据 V-6 用它钉「`WindowSize` 取自数据集,不是 tick 数」(ADR-039)。</remarks>
        public F1PipelineInput? LastF1Input { get; private set; }
    }
}
