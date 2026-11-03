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
//    ⚠️ 生产今日候选池仍为空 ⇒ **恒 `NoPool`**,预算/抽数判据只有夹具可达(ADR-039 §R3)。
//
// ▍今天**没做**的(逐条具名,不借绿)
//   · **密度预算的 mods 输入**(F2 的 `Mod_Eco` / `Mod_Rep`)—— P0 无输入源 ⇒ 今日 `mods = ∅`。
//   · **权重 / 历史标记乘子** —— 见 `EventDraw` 头注。
//   · **生成点解析** —— `spawnAnchorOf` 由装配方注入(6 的出生锚;测试传合成格)。
//   · **生产池内容** —— 52 的池条目归 37 内容件;生产今日候选集为空 ⇒ `NoPool`,
//     **链路真跑但零写入**(与 `RandomEventsDirectorDriver` 的「通电待信」同一形态)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>急召生成链的单次结果(失败面全部**具名**,不返回 bool)。</summary>
    public enum SpawnChainOutcome
    {
        /// <summary>已掷骰、已落 `EventRolled` + `EventArrived`、已请求病人出现。</summary>
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
    }

    /// <summary>52 急召生成链:抽池 → 掷骰 → F1 → `EventArrived` → 病人出现。</summary>
    public sealed class RandomEventSpawnChain
    {
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly IEventAuthority _authority;
        private readonly Func<int, IReadOnlyList<int>> _keysOfTier;
        private readonly IPresenceQuery _presence;
        private readonly Func<long, WorldPos> _spawnAnchorOf;
        private readonly Func<long, PatientId> _spawnPatient;
        private readonly Func<long, F1PipelineInput> _contextOf;
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly int _tier;
        private readonly long _rollInterval;
        private readonly int _windowSize;
        private readonly int _baseEvents;
        private readonly int _budgetMax;
        private readonly long _ticksPerDay;
        private readonly int _causeClueKey;

        /// <param name="sink">事件写入通道。</param>
        /// <param name="encoder">载荷编码器(ADR-029)。</param>
        /// <param name="authority">掷骰权威(生产 = <see cref="SeedEventAuthority"/>)。</param>
        /// <param name="keysOfTier">档 → 候选 key 集(抽池的「按 key 过滤」面)。</param>
        /// <param name="presence">在场读面 —— CAP 预检用,**必须与 `EventStream` 同一实例**。</param>
        /// <param name="spawnAnchorOf">生成点解析(tick → 整数格);6 的出生锚,测试传合成格。</param>
        /// <param name="spawnPatient">病人出现(tick → 新病人 id);生产 = `PatientSpawner.SpawnNext`。</param>
        /// <param name="contextOf">F1 上下文输入(window → 上下文)—— **由装配方给**(夜 / 出诊路径 /
        /// 医馆 / 战斗冷却 / 季节全是别处的状态,本链不自己猜;内部硬编码 = 造假的上下文)。
        /// ⚠️ `EventF1Pipeline.EvaluateContextGate` 的现行判据 = `IsOnExpedition &amp;&amp; !IsInClinic`
        /// ⇒ 上下文给不全时链路恒 `NoContext`(这是判决,不是 bug)。</param>
        /// <param name="tier">本链抽取的档 id。</param>
        /// <param name="config">52 的烘焙配置(`ROLL_INTERVAL` / `WINDOW_SIZE` / `BASE` / `BUDGET_MAX` / `TICKS_PER_DAY`)。</param>
        /// <param name="events">事件流只读视图 —— 当日计数与掷骰序号的**唯一来源**(ADR-039 D2)。</param>
        /// <param name="causeClueKey">因由线索 key(`EventArrivedPayload.causeClueKey`;0 = 无)。</param>
        public RandomEventSpawnChain(
            IEventSink sink,
            IPayloadEncoder encoder,
            IEventAuthority authority,
            Func<int, IReadOnlyList<int>> keysOfTier,
            IPresenceQuery presence,
            Func<long, WorldPos> spawnAnchorOf,
            Func<long, PatientId> spawnPatient,
            Func<long, F1PipelineInput> contextOf,
            Func<IReadOnlyList<SimEvent>> events,
            int tier,
            in RandomEventsDataSet config,
            int causeClueKey = 0)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            _keysOfTier = keysOfTier ?? throw new ArgumentNullException(nameof(keysOfTier));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _spawnAnchorOf = spawnAnchorOf ?? throw new ArgumentNullException(nameof(spawnAnchorOf));
            _spawnPatient = spawnPatient ?? throw new ArgumentNullException(nameof(spawnPatient));
            _contextOf = contextOf ?? throw new ArgumentNullException(nameof(contextOf));
            _events = events ?? throw new ArgumentNullException(nameof(events));
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

            // ④ 抽池:空池 ⇒ 零写入(fail-safe,生产今日即此支)
            IReadOnlyList<int> keys = _keysOfTier(_tier);
            if (keys == null || keys.Count == 0) return SpawnChainOutcome.NoPool;

            // ⑤ F1:上下文门 + 档配额(排在掷骰之前 —— 配额为 0 就不该掷)
            //    窗口 = **预算窗口起点**(一天),不是 tick 子窗口(ADR-039 D2)。
            F1PipelineInput raw = _contextOf(dayStart);
            var input = new F1PipelineInput(
                dayStart, _windowSize, raw.IsNight, raw.IsOnExpedition, raw.IsInClinic,
                raw.InCombatCooldown, raw.SeasonIndex);
            LastF1Input = input;
            F1PipelineOutput f1 = EventF1Pipeline.Execute(input, minTier: 0, maxTier: 3);
            if (!f1.ContextGatePassed) return SpawnChainOutcome.NoContext;
            if (f1.Quotas[_tier] <= 0) return SpawnChainOutcome.NoQuota;

            // ⑥ **CAP 预检 —— 必须排在第一次 Append 之前**
            //    失效模式(不这么做):`EventRolled` 先落流,后半截被 AC-15 拒 ⇒ 流里留下
            //    「掷过骰、事件没到」的孤儿行,读档重放时凭空多一次掷骰。
            if (_presence.PresentCount >= EventStream.PATIENT_APPEARANCE_CAP)
                throw new InvalidOperationException(
                    $"AC-15: 在场病人数已达上限 {EventStream.PATIENT_APPEARANCE_CAP}，拒收新病人");

            // ⑦ 掷骰(序号 = 当日第 n 次,**从流计数**;四项全部落流,可重建)
            EventRollResult roll = _authority.Roll(new RollRequest(dayStart, _tier, rollsToday));

            // ⑧ 落两条事件 —— 顺序固定(先骰后降临;AD-52-822「一次降临恰一条 EventArrived」)
            _sink.Append(new SimEvent(tick, PatientId.None, -1, EventKind.EventRolled,
                _encoder.Encode(EventKind.EventRolled,
                    new EventRolledPayload((int)roll.Win, roll.Tier, roll.Ordinal, roll.ChosenKey))));

            WorldPos anchor = _spawnAnchorOf(tick);
            _sink.Append(new SimEvent(tick, PatientId.None, -1, EventKind.EventArrived,
                _encoder.Encode(EventKind.EventArrived,
                    new EventArrivedPayload(roll.ChosenKey, roll.Tier, anchor, _causeClueKey))));

            // ⑨ 病人出现(9 的写者;自身仍受 `EventStream` 的 AC-15 把守 = 第二道门)
            _spawnPatient(tick);

            return SpawnChainOutcome.Spawned;
        }

        /// <summary>最近一次求值用的 F1 输入(观测接缝;`null` = 还没走到 F1)。</summary>
        /// <remarks>判据 V-6 用它钉「`WindowSize` 取自数据集,不是 tick 数」(ADR-039)。</remarks>
        public F1PipelineInput? LastF1Input { get; private set; }
    }
}
