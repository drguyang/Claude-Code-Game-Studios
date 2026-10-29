// M2 接线轮阶段 2 · 批次 C(体征链核心)—— 三断点闭合件:
//   ① apply 层的**驱动侧**(状态持有侧 = Sim/DiseaseSimulation/DiseaseCourseBook)
//   ② 投影桥 `ProgressionResult → VitalsDto`(边界出 sim,唯一浮点出口)
//   ③ `IVitalsQuery` 生产实装(此前仅测试 Fake)
//
// 权威来源:
//   ADR-005 —— 病史事件流是唯一真源;**主机唯一执行 Step**;sim 程序集门 A(BCL only)。
//   ADR-016 §一 —— 13 消费 `VitalsDto`(float,唯一浮点出口)。
//   ADR-025 §① —— `IVitalsQuery` / `VitalsDto` ∈ `Sim.Contracts`;本实装落**装配层**
//     (`Gameplay.Boot` = ADR-025 §① 登记行,案 A 的组合根)。
//   ADR-025 §② 甲案 —— `Fix.ToFloat()` 调用点白名单 = {Sim.Codec, Gameplay.*}
//     ⇒ 本文件的 `ToFloat()` 是**合法出口**(Sim 内出现才是构建失败)。
//   ADR-029 ① —— 载荷编码唯一路径 = `IPayloadEncoder`;**解码**唯一路径 = `PayloadCodec`
//     ⇒ 解码必须住同时可见 Sim 与 Sim.Codec 的装配,这正是本装配存在的理由(案 A)。
//   GDD disease-simulation.md F2 —— `position = clamp(Progress / SCALE_病种, 0, 1)`。
//   GDD disease-simulation.md F0「唯一投影点」—— `GetVitals(patient) → VitalsDto`。
//   ADR-020 §五 / ADR-013 §9 C3 —— 呈现侧只读不持状态;本类是**取数门面**,
//     不持有任何表现态。
//
// ── 主循环次序(不可改,见 MovementFeed.PumpFrame 头注)───────────────────────
//   每个 tick 边沿:`PlayerController.OnTickEdge()` → `DiseaseVitalsService.OnTickEdge(tick)`
//   ⇒ 疾病 Step 排在同边沿玩家提交**之后**:本边沿写入的病史事件在**本 tick** 的体征里
//   立刻可见(零额外延迟);次序颠倒 ⇒ 处置/跨格恒晚一 tick 才进求值(表现与流撕裂)。
//   「体征在 tick 边沿后可见」的语义自本批起成立。
//
// ── HostAuthorityMode 纪律 ───────────────────────────────────────────────────
//   本类是**主机侧**服务(ADR-005:主机唯一执行 Step / Append)。BootRoot 现阶段恒
//   `SimAuthorityMode.Host`;Client 模式不装配 / 不驱动本服务(上行链归 45 / P1b)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 体征链核心服务 —— 每 tick 从病史流推进病人病程(applies 层)+ 求值 + 投影到
    /// <see cref="VitalsDto"/>(<see cref="IVitalsQuery"/> 生产实装)。
    /// </summary>
    /// <remarks>
    /// <para><b>零第二真源</b>:本类**不保存**病程,只缓存「最近一次 tick 的求值结果」
    /// (派生态);病程档案由事件流推进(游标单向前进)。存档 / 回放须能从流重建它。</para>
    /// <para><b>不写流</b>:本类只读 `EventStream`(读面 <c>Events</c>)与 blob 池(读面),
    /// 零 <c>IEventSink.Append</c> —— 写者仍是 9 / 10 / 11 / 25 各自的调用点。</para>
    ///
    /// <para><b>死亡候选产出(story-008 · 2026-10-24)</b>:<see cref="StepAll"/> 在 F4
    /// **转移进** <c>Deceased</c> 的那一 tick 调 <see cref="IDeathCandidateSink.Publish"/>
    /// 一次(去重判据 = 旧值 != <c>Deceased</c> && 新值 == <c>Deceased</c>)。
    /// 候选是**进程内通知**,不是流事件(ADR-009 §二 三问判据)。</para>
    ///
    /// <para><b>⚠️ 类别闸 —— 为什么必须有</b>:本类在 story-008 之前把 F4 的
    /// <c>actorClass</c> 恒传 <c>ActorClass.Patient</c>。若照字面「判出 <c>Deceased</c>
    /// 就 <c>Publish</c>」,被 publish 的**必然是病人**(<see cref="Courses"/> 只由
    /// <c>DiseaseOnset</c> 建档),而 29 的 <c>OnDeathCandidate(playerId, tick)</c> 语义是
    /// 「**玩家**应死」⇒ 会铸出一条 <c>actor_id = 病人 id</c> 的 <c>PlayerDied</c>。
    /// ⇒ 产出点对 <see cref="ActorClass.Player"/> 设闸,类别由注入的
    /// <c>actorClassOf</c> 查询面给出(未接线时回退 <c>ActorClass.Patient</c>,
    /// 与接线前逐位一致)。</para>
    ///
    /// <para><b>⚠️ 诚实边界(AC-9-28,不得被读成「玩家会死」)</b>:
    /// 玩家 actorId 由 <c>BootRoot.cs:252</c> 从同一 <c>IIdAuthority</c> 取,但**玩家不发
    /// <c>DiseaseOnset</c>** ⇒ 玩家永不进 <see cref="StepAll"/> 的遍历面 ⇒
    /// **生产上 <c>Publish</c> 恒不因玩家触发**。本类交付的是「产出路径存在且正确」,
    /// 不是「死亡链已可用」。后者需要玩家染病路径(GDD <c>AC-35</c> 结构半边 +
    /// 玩家侧 <c>DiseaseOnset</c> 入口),不在 story-008 面内。</para>
    /// </remarks>
    public sealed class DiseaseVitalsService : IVitalsQuery
    {
        private readonly EventStream _stream;
        private readonly IBlobPool _blobPool;
        private readonly ulong _worldSeed;
        private readonly Dictionary<int, DiseaseRegistryEntry> _registry =
            new Dictionary<int, DiseaseRegistryEntry>();
        private readonly Dictionary<int, ProgressionResult> _results =
            new Dictionary<int, ProgressionResult>();

        /// <summary>
        /// 每病人最近一次 tick 的 F4 九态(派生态,不写流、不入档)。
        /// <para>⚠️ 与 <see cref="_results"/> 同批写入 ⇒ 二者恒同 tick,不构成第二真源。</para>
        /// </summary>
        private readonly Dictionary<int, InjuryState> _outcomes =
            new Dictionary<int, InjuryState>();

        /// <summary>游标:已应用到 `Stream.Events` 的下标(只进不退)。</summary>
        private int _cursor;

        /// <summary>已驱动到的最近 tick(单调;拒绝时间倒流)。</summary>
        private long _lastTick = -1;

        /// <summary>
        /// <c>W</c>(趋势差分窗口,tick)—— **全局**配置(GDD Tuning §四 <c>:1743</c>)。
        /// <para><c>0</c> = <b>未配置</b>(数值轮未裁)⇒ <see cref="RecoveryWindowInput.IsConfigured"/>
        /// 为 <c>false</c> ⇒ 痊愈支 / 迁延支**不求值**。本类不自己找值 —— 由装配方从
        /// 烘焙数据面灌入(<c>DiseaseRegistryBinder.BindResult.TrendWindowTicks</c> 是作者态来源;
        /// cooked 产物尚未携带它,见 <c>DiseaseRegistryCookedWriter</c>)。</para>
        /// </summary>
        private readonly long _trendWindowTicks;

        /// <summary><c>TREND_EPS</c>(趋势死区,Q16.16 raw)—— 见 <see cref="_trendWindowTicks"/>。</summary>
        private readonly long _trendEpsRaw;

        /// <summary>
        /// <c>treatable_by</c> 查表面(2026-10-23 接真,结清 <c>D-9-P</c>)。
        /// <para><c>null</c> = <b>未接线</b> ⇒ F1 的离牌门退回条目内联数组(真表上恒 null
        /// ⇒ 恒放行),与接线前**逐位一致**。生产路径由 <c>CompositionRoot</c> 从已加载的
        /// <c>DiseaseActionAxisDataSet</c> 造实现灌入。</para>
        /// <para>⚠️ 与 <see cref="_registry"/> 同为装配期注入的只读依赖,运行期不变。</para>
        /// </summary>
        private readonly ITreatableByLookup _treatableBy;

        /// <summary>
        /// 实体类别查询面(story-008 AC-9-26 接真,2026-10-24)。
        /// <para><b>此前是硬编码</b>:<see cref="EvaluateOutcome"/> 把 F4 的 <c>actorClass</c>
        /// 恒传 <c>ActorClass.Patient</c>。那个硬编码在「只有病人进 F4」的现状下**结论正确**,
        /// 但它让「这个 course 属于谁」**无载体** ⇒ 死亡候选无法区分玩家与病人。
        /// 详见本类头注的「类别闸」节。</para>
        /// <para><c>null</c> = <b>未接线</b> ⇒ 退回 <c>_ => ActorClass.Patient</c>
        /// (与接线前**逐位一致**)。生产路径由 <c>CompositionRoot</c> 注入真查询面。</para>
        /// </summary>
        private readonly Func<int, ActorClass> _actorClassOf;

        /// <summary>
        /// 死亡候选产出侧(story-008 · <c>S18-AC5</c> 前半句)。
        /// <para><c>null</c> = <b>未接线</b> ⇒ 不产生任何候选(与接线前逐位一致)。</para>
        /// <para>⚠️ <b>只在「转移进 <c>Deceased</c> 的那一 tick」调一次</b>
        /// (<c>Deceased</c> 是吸收态,每 tick 发会让事件率退化成「tick 数 × 玩家数」)。</para>
        /// </summary>
        private readonly IDeathCandidateSink _deathCandidates;

        /// <summary>
        /// 构造(fail-loud:任一依赖缺失 = 具名 <see cref="ArgumentNullException"/>,无半装配)。
        /// </summary>
        /// <param name="stream">病史事件流(只读消费)。</param>
        /// <param name="blobPool">blob 池读面(载荷字节寻址)。</param>
        /// <param name="registry">病种注册表(直传条目;批次 B 裁定无 id 查找接口,
        /// 本类在装配期自建 id → 条目的字典,**不**向 Sim 侧新增查找面)。</param>
        /// <param name="worldSeed">世界种子(Noise 纯函数入参;Noise 现为 tripwire 恒 0)。</param>
        /// <param name="trendWindowTicks"><c>W</c>(tick);<c>0</c> = 未配置(默认 —— 数值轮未裁)。</param>
        /// <param name="trendEpsRaw"><c>TREND_EPS</c>(Q16.16 raw);<c>0</c> = 未配置(默认)。</param>
        /// <param name="treatableBy"><c>treatable_by</c> 查表面;<c>null</c> = 未接线(默认,
        /// 与接线前逐位一致)。生产路径由 <c>CompositionRoot</c> 从处置轴产物构造。</param>
        /// <param name="actorClassOf">实体类别查询面(story-008);<c>null</c> = 未接线
        /// ⇒ 恒 <c>ActorClass.Patient</c>(与接线前逐位一致)。</param>
        /// <param name="deathCandidates">死亡候选产出侧(story-008);<c>null</c> = 未接线
        /// ⇒ 零候选(与接线前逐位一致)。</param>
        /// <exception cref="ArgumentNullException">任一引用依赖为 null(参数名具名)。</exception>
        /// <exception cref="ArgumentException">注册表条目为 null / 病种 id 重复(参数名具名)。</exception>
        public DiseaseVitalsService(EventStream stream, IBlobPool blobPool,
                                    IReadOnlyList<DiseaseRegistryEntry> registry,
                                    ulong worldSeed,
                                    long trendWindowTicks = 0, long trendEpsRaw = 0,
                                    ITreatableByLookup treatableBy = null,
                                    Func<int, ActorClass> actorClassOf = null,
                                    IDeathCandidateSink deathCandidates = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _blobPool = blobPool ?? throw new ArgumentNullException(nameof(blobPool));
            _worldSeed = worldSeed;

            // 实体类别查询面(story-008 AC-9-26)。
            //   ⚠️ null 是**合法入参**(未接线),不是错误 —— 与 treatableBy 同格。
            //   回退值取 `ActorClass.Patient`:那是接线前**硬编码**的那个值,
            //   退回它 = 与接线前逐位一致(既有 9 侧测试全部覆盖这条路径)。
            _actorClassOf = actorClassOf ?? (_ => ActorClass.Patient);

            // 死亡候选产出侧(story-008 AC-9-23)。null = 未接线 ⇒ 零候选。
            _deathCandidates = deathCandidates;

            // treatable_by 查表面(2026-10-23 接真)。
            //   ⚠️ null 是**合法入参**(未接线),不是错误 —— 与 W / TREND_EPS 的 0 同格:
            //   「未接线」与「接线了但查不到」是两种状态,前者不拦(见 ITreatableByLookup 头注)。
            _treatableBy = treatableBy;

            // 趋势窗口(W / TREND_EPS)—— 全局配置,原样收下,不做任何代拍。
            //   ⚠️ 此处**不**校验奇偶 / 负值:值域守卫归注册表写入期(R1-36,
            //   `RegistrySchemaValidator.ValidateTrendWindow`),装配期只搬运。
            //   0 是合法入参(未配置),不是错误。
            _trendWindowTicks = trendWindowTicks;
            _trendEpsRaw = trendEpsRaw;

            if (registry == null) throw new ArgumentNullException(nameof(registry));
            for (int i = 0; i < registry.Count; i++)
            {
                DiseaseRegistryEntry entry = registry[i];
                if (entry == null)
                    throw new ArgumentException(
                        $"registry[{i}] 为 null —— 组合根拒绝带空条目的注册表", nameof(registry));
                if (_registry.ContainsKey(entry.DiseaseId))
                    throw new ArgumentException(
                        $"注册表病种 id 重复:{entry.DiseaseId} —— R1-01 应在写入期拦截,此处 fail-loud",
                        nameof(registry));
                _registry.Add(entry.DiseaseId, entry);
            }
        }

        /// <summary>病程档案簿(建档 / 处置 / 判定输入的落点;测试与调试的观测面)。</summary>
        public DiseaseCourseBook Courses { get; } = new DiseaseCourseBook();

        /// <summary>已求值并可查询的病人数。</summary>
        public int EvaluatedCount => _results.Count;

        /// <summary>
        /// 取某病人最近一次 tick 的 F4 九态(派生态;**本类不写流**,终态落流归 9 的写者轮)。
        /// </summary>
        /// <param name="p">病人 id。</param>
        /// <returns>最近一次 <see cref="StepAll"/> 求得的九态。</returns>
        /// <exception cref="PatientCourseNotFoundException">该病人尚未建档 / Step 未驱动到它。</exception>
        /// <remarks>
        /// <b>为什么与 <see cref="GetVitals"/> 分开</b>:<c>VitalsDto</c> 是**呈现面**
        /// (float,唯一浮点出口,ADR-025 §②);九态是**整数域判据面**,且 GDD 明确
        /// 「13 不重建九态」(<c>:528</c>/<c>:646</c>)⇒ 九态**不得**混进 <c>VitalsDto</c>。
        /// 本入口只供调试视图 / 后续 9 的写者消费,不经呈现层。
        /// </remarks>
        public InjuryState GetOutcome(PatientId p)
        {
            if (!_outcomes.TryGetValue(p.Value, out InjuryState state))
                throw new PatientCourseNotFoundException(p);
            return state;
        }

        /// <summary>
        /// 一个 tick 边沿的体征链驱动:`应用本 tick 及之前的全部新事件 → 对每个在册病人求值`。
        /// </summary>
        /// <remarks>
        /// <b>调用时机</b> = <see cref="MovementFeed.PumpFrame"/> 的 tick 边沿序列内,
        /// 排在 <c>PlayerController.OnTickEdge()</c> **之后**(见文件头次序)。
        /// <b>非线程安全</b> —— 与 sim 的单线程 tick 模型一致(ADR-005 主机唯一 Step)。
        /// </remarks>
        /// <param name="tick">本边沿的逻辑 tick(须 ≥ 上一次;时间倒流 = 数据错误)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        /// <exception cref="InvalidOperationException">tick 单调性被破坏,或事件载荷解码失败。</exception>
        /// <exception cref="ArgumentException"><c>DiseaseOnset</c> 声明的病种 id 不在注册表内。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");
            if (tick < _lastTick)
                throw new InvalidOperationException(
                    $"体征链 Step 时间倒流:tick={tick} < 上次 {_lastTick} —— tick 由 ITickProvider 单调推进," +
                    "倒流意味着同一病人会被用旧时刻覆盖新求值");
            _lastTick = tick;

            ApplyNewEvents(tick);
            StepAll(tick);
        }

        /// <inheritdoc />
        /// <remarks>
        /// <b>fail-loud</b>:未建档病人抛 <see cref="PatientCourseNotFoundException"/> ——
        /// 全零 `VitalsDto` 与「真的一点症状没有」不可区分,不返回默认值(AC-20 前提)。
        /// </remarks>
        /// <exception cref="PatientCourseNotFoundException">该病人尚未建档 / Step 未驱动到它。</exception>
        /// <exception cref="InvalidOperationException">注册表条目的 `Scale ≤ 0`(F2 分母不可用;
        /// `Curve != null` 的条目已由 R1-23 校验,`Curve == null` 的既有占位条目可能踩中)。</exception>
        public VitalsDto GetVitals(PatientId p)
        {
            if (!_results.TryGetValue(p.Value, out ProgressionResult result))
                throw new PatientCourseNotFoundException(p);

            DiseaseCourse course = Courses.Get(p);          // 与 _results 同批建立,失败即内部不一致
            if (!_registry.TryGetValue(course.DiseaseId, out DiseaseRegistryEntry entry))
                throw new InvalidOperationException(
                    $"patient_id={p.Value} 的病种 id={course.DiseaseId} 不在注册表内 —— Step 阶段已 fail-loud,此处为兜底");

            if (entry.Scale.Raw <= 0)
                throw new InvalidOperationException(
                    $"病种 {entry.DiseaseId}({entry.DiseaseKey}) 的 SCALE ≤ 0({entry.Scale.Raw})—— " +
                    "F2 position = Progress / SCALE 的分母不可用;R1-23 只对 Curve != null 的条目生效," +
                    "本条目疑似批次 B 前的占位形态,拒绝返回除零 / NaN");

            // ── 投影桥(F2):position = clamp(Progress / SCALE_病种, 0, 1)──
            // 全程整数域,最后一步才 `ToFloat()`(ADR-025 §② 甲案白名单出口)。
            Fix position = result.Position / entry.Scale;
            if (position.Raw < Fix.ZeroRaw) position = Fix.Zero;
            if (position.Raw > Fix.OneRaw) position = Fix.One;

            // ── trend:`trend_backward`(后向差分)—— **不是** `result.Trend`(中心差分)──
            //
            // 🔁 **2026-10-29(`TD-audio-025` · 用户裁定甲案)**:原文写
            //   「trend:tripwire 恒 0(ComputeTrend 未接;阶段 0 口径保留占位)」
            //   ⇒ `result.Trend` 恒 0 ⇒ 13 的 `EvaluateTerminal` 痊愈判据 `trend ≤ 0` **恒真**
            //   ⇒ 每个新出现病人(position ≈ 0)首拍即锁存「痊愈」⇒ 呼吸层永不启动。
            //
            // **为什么换成后向差分,而不是去实现中心差分**:
            //   · GDD `disease-simulation.md:923-924` 的分工是 —— 中心差分 `trend(t)` = **8 的展示读数**
            //     (读未来 `t + W/2`,非因果);后向 `trend_backward(t)` = **状态转移用**(只读过去,因果)。
            //   · 13 用这个量做的是**终态锁存**(`TerminalFlag`),那是一次**不可逆**的状态转移
            //     (`patient-ai.md` 规则:一旦锁存不再重新求值)⇒ 它必须吃**因果**的量。
            //     用「读未来的展示量」做不可逆判定,本质上就是把非因果引进表现层。
            //   · `ComputeTrendBackward` **已实现且已接线**(`DiseaseVitalsService` 的 F4 路径,2026-10-22),
            //     本处只是把**同一个实现**再喂给 13,不新增第二套趋势算法(禁第二真源)。
            //
            // ⚠️ **`W` / `TREND_EPS` 仍为 0(数值轮未裁)** ⇒ 本函数返回 0 ⇒ 与改动前**行为相同**
            //   (13 仍看到 trend = 0)。这不是「没修」—— 是「管道通了、数值未填」:
            //   数值一填,13 的痊愈锁存立即按 GDD 字面生效,无需再改代码。
            //   承 `DiseaseVitalsService` 的 F4 路径同款口径(它也已通管、同样等数值)。
            Fix trend = ProgressionEvaluator.ComputeTrendBackward(
                entry, course.OnsetTick, _lastTick, course.Doses, _worldSeed, course.Patient,
                windowTicks: _trendWindowTicks,
                trendEpsRaw: _trendEpsRaw,
                doseClampBound: null,
                treatableBy: _treatableBy);

            // ── signs:tripwire —— 恒空 ⇒ mask = 0 / count = 0 ──
            // F2 的 `Project(Progress, 病种)ⱼ` 需要**阈值**,而词表内容与阈值归 8
            // (§Visual/Audio 二「9 只保证体征是离散词」)⇒ 9 侧当前不可能产出 signs。
            // 若有人绕过该边界真产出了词条,静默丢通道位 = 通道掩码说谎 ⇒ 显式 fail-loud。
            int[] signs = result.Signs ?? Array.Empty<int>();
            if (signs.Length > 0)
                throw new NotSupportedException(
                    $"F2 投影未接线但求值器产出了 {signs.Length} 条 signs —— " +
                    "词键 → 通道位映射归 8 的烘焙表(design/gdd/diagnosis-system.md §Visual/Audio 二)," +
                    "9 侧没有阈值可判活跃词条;拒绝静默丢弃通道掩码(AC-21)");

            return new VitalsDto(position.ToFloat(), trend.ToFloat(),
                                 signChannelMask: 0, signCount: 0);
        }

        // ── apply 层:解码 + 归档 ─────────────────────────────────────────────

        /// <summary>
        /// 把游标推进到 `tick`(含):解码并应用 `[cursor, …]` 内 `Tick ≤ tick` 的新事件。
        /// </summary>
        private void ApplyNewEvents(long tick)
        {
            IReadOnlyList<SimEvent> events = _stream.Events;
            while (_cursor < events.Count)
            {
                SimEvent e = events[_cursor];
                if (e.Tick > tick) break;        // 未来事件留到它自己的边沿(入流序不越界)
                Apply(in e);
                _cursor++;
            }
        }

        /// <summary>
        /// 单事件应用(按 Kind 白名单路由;其余 31 支 Kind 与本链无关,原样跳过)。
        /// </summary>
        /// <exception cref="InvalidOperationException">载荷解码失败(池寻址不到 / schema 违例)。</exception>
        private void Apply(in SimEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.DiseaseOnset:
                {
                    if (!PayloadCodec.TryGetPayload(in e, _blobPool, out DiseaseOnsetPayload p))
                        throw DecodeFailure(in e, nameof(DiseaseOnsetPayload));
                    Courses.ApplyOnset(p, e.Patient);
                    break;
                }
                case EventKind.DrugTreatmentApplied:
                {
                    if (!PayloadCodec.TryGetPayload(in e, _blobPool, out DrugTreatmentAppliedPayload p))
                        throw DecodeFailure(in e, nameof(DrugTreatmentAppliedPayload));
                    Courses.ApplyTreatment(e.Patient,
                        new TreatmentDose(p.Tick, p.TreatmentId, p.Polarity,
                                          p.DrugPotency, p.HalfLife));
                    break;
                }
                case EventKind.EmergencyTreatmentApplied:
                {
                    if (!PayloadCodec.TryGetPayload(in e, _blobPool, out EmergencyTreatmentAppliedPayload p))
                        throw DecodeFailure(in e, nameof(EmergencyTreatmentAppliedPayload));
                    Courses.ApplyTreatment(e.Patient,
                        new TreatmentDose(p.Tick, p.TreatmentId, p.Polarity,
                                          p.DrugPotency, p.HalfLife));
                    break;
                }
                case EventKind.EmergencyAttempt:
                {
                    if (!PayloadCodec.TryGetPayload(in e, _blobPool, out EmergencyAttemptPayload p))
                        throw DecodeFailure(in e, nameof(EmergencyAttemptPayload));
                    Courses.RegisterEmergencyAttempt(e.Patient, p);
                    break;
                }
                case EventKind.CareApplied:
                {
                    if (!PayloadCodec.TryGetPayload(in e, _blobPool, out CareAppliedPayload p))
                        throw DecodeFailure(in e, nameof(CareAppliedPayload));
                    Courses.ApplyCare(e.Patient, p.Tick, p.Phase);
                    break;
                }
                default:
                    break;   // 其余 Kind:与体征链无关(战伤累积 / compounds / 护理动作归各自轮)
            }
        }

        private static InvalidOperationException DecodeFailure(in SimEvent e, string typeName)
            => new InvalidOperationException(
                $"事件 Kind={e.Kind}(tick={e.Tick}, patient={e.Patient.Value})的载荷无法解码为 " +
                $"{typeName} —— blob 池寻址失败或 schema 违例;体征链拒绝以空载荷继续");

        // ── Step:对每个在册病人按 F1 求值 ───────────────────────────────────

        private void StepAll(long tick)
        {
            IReadOnlyList<DiseaseCourse> courses = Courses.InOrder;   // 入流序,确定性
            for (int i = 0; i < courses.Count; i++)
            {
                DiseaseCourse course = courses[i];
                if (!_registry.TryGetValue(course.DiseaseId, out DiseaseRegistryEntry entry))
                    throw new ArgumentException(
                        $"DiseaseOnset 声明的 disease_id={course.DiseaseId}" +
                        $"(patient_id={course.Patient.Value})不在注册表内 —— " +
                        "体征链拒绝为未知病种求值(组合根装配的注册表须覆盖全部在流病种)",
                        "registry");

                ProgressionResult result = ProgressionEvaluator.Evaluate(
                    entry, course.OnsetTick, tick, course.Doses, _worldSeed, course.Patient,
                    doseClampBound: null, treatableBy: _treatableBy);
                _results[course.Patient.Value] = result;

                // ══ F4 求值(2026-10-22 第三轮接线)══
                // 用户裁定:「接 F4 求值,**但不写流**」—— 本段只求值并缓存,
                // 零 `IEventSink.Append`(本类全程不写流的既有纪律不变)。
                //
                // ══ 死亡候选产出(story-008 · 2026-10-24)══
                // AC-9-25 的**去重判据在这里**:`Deceased` 是吸收态(StateMachine:61-73)
                // ⇒ 只认「旧值 != Deceased && 新值 == Deceased」这一次转移。
                // ⚠️ **必须先读旧值再写 `_outcomes`** —— 写后再读恒等于新值,转移永不成立。
                int pid = course.Patient.Value;
                InjuryState previous;
                bool hadPrevious = _outcomes.TryGetValue(pid, out previous);

                InjuryState current = EvaluateOutcome(
                    course, entry, result, tick, _worldSeed, _trendWindowTicks, _trendEpsRaw,
                    _treatableBy, _actorClassOf(pid));
                _outcomes[pid] = current;

                PublishDeathCandidateIfTransferred(
                    pid, hadPrevious ? previous : (InjuryState?)null, current, tick);
            }
        }

        /// <summary>
        /// 死亡候选的**唯一产出点**(story-008 AC-9-23 / AC-9-24 / AC-9-25)。
        /// </summary>
        /// <param name="actorId">本 course 归属的实体 id(病人或玩家 —— 同一 id 空间)。</param>
        /// <param name="previous">上一 tick 的九态;<c>null</c> = 本 tick 首次求值(无转移可言)。</param>
        /// <param name="current">本 tick 的九态。</param>
        /// <param name="tick">本 tick。</param>
        /// <remarks>
        /// <para><b>三道闸,缺一即错</b>:</para>
        /// <list type="number">
        /// <item><b>转移闸</b>(AC-9-25):只认「进入 <c>Deceased</c>」那一次。
        /// <c>Deceased</c> 是吸收态 ⇒ 不加此闸则每 tick 发一次候选,29 侧每 tick 收一次。</item>
        /// <item><b>类别闸</b>(AC-9-24):只对 <see cref="ActorClass.Player"/> 发。
        /// ⚠️ <b>这条是防 bug 不是防回归</b> —— 本服务的 <c>Courses</c> 只由 <c>DiseaseOnset</c>
        /// 建档,而玩家**不发** <c>DiseaseOnset</c> ⇒ 现状下走到这里的**必然是病人**。
        /// 若照字面「判出 Deceased 就发」,29 会把病人 id 当玩家 id 铸出一条
        /// <c>actor_id = 病人 id</c> 的 <c>PlayerDied</c>。</item>
        /// <item><b>接线闸</b>(AC-9-23):<c>_deathCandidates == null</c> ⇒ 零候选
        /// (未接线的构建与接线前逐位一致)。</item>
        /// </list>
        /// <para><b>为什么不判 <c>SelfLimited</c> / <c>LethalFor</c></b>:那两个门已在
        /// <see cref="F4OutcomeEvaluator.Evaluate"/> 的死亡支里判过(<c>:170-171</c>)——
        /// 能走到 <c>Deceased</c> 就说明两门已放行。在此重判 = 同一判据的第二份拷贝。</para>
        /// <para><b>为什么不在这里写 <c>PlayerDied</c></b>:写者归 29
        /// (<c>PlayerDiedWriter</c>),本类全程零 <c>IEventSink.Append</c>。候选是**进程内
        /// 通知**(ADR-009 §二 三问判据:候选不满足「可感知」)。</para>
        /// </remarks>
        private void PublishDeathCandidateIfTransferred(
            int actorId, InjuryState? previous, InjuryState current, long tick)
        {
            // 闸 ③:未接线 ⇒ 零候选(与接线前逐位一致)。
            if (_deathCandidates == null) return;

            // 闸 ①:只在**转移进** Deceased 的那一 tick 发一次。
            //   previous == null(本 tick 首次求值)也算转移 —— 冷启动即判死的情形必须发,
            //   否则「读档时病人已死」永远不会产生候选。⚠️ 但那与「吸收态不重复发」不冲突:
            //   首次求值只会发生一次。
            if (current != InjuryState.Deceased) return;
            if (previous == InjuryState.Deceased) return;

            // 闸 ②:类别闸 —— 只对玩家发。
            //   ⚠️ `ActorClass.Unspecified` 会让 `EntityDeathGates.IsCombatant` 抛;
            //   而那个门已在 F4 求值里跑过(能到 Deceased 说明类别合法)⇒ 此处不会遇 Unspecified。
            if (_actorClassOf(actorId) != ActorClass.Player) return;

            _deathCandidates.Publish(actorId, tick);
        }

        /// <summary>
        /// 对一个病人求 F4 九态 —— 把 F1/F5 的读数 + 三通道 + 窗口喂给
        /// <see cref="F4OutcomeEvaluator"/>。
        /// </summary>
        /// <remarks>
        /// <para><b>⚠️ 本方法在「接线面」上的三处诚实边界</b>(逐条列出,不掩盖):</para>
        /// <list type="number">
        /// <item><b><c>position_agg</c> 退化 = 单病种投影。</b>
        /// <see cref="DiseaseCourseBook.ApplyOnset"/> 对同一 <c>patient_id</c> 的第二次
        /// <c>DiseaseOnset</c> 是 **fail-loud 拒绝**的(「病人出现是单次事件」),
        /// 故 <see cref="DiseaseCourseBook.InOrder"/> 里每个病人**恰有一个** course
        /// ⇒ 本批的 <c>D</c> 恒为单元素集,<c>max_d</c> 退化为恒等。
        /// ⇒ 本方法**刻意不调** <see cref="DiseaseAggregator.Aggregate"/>:
        /// 对单元素集调它只是绕一圈拿回同一个值,且会让「多病聚合」看起来已经接线。
        /// **多病聚合(F5)的接线前提 = 档案簿支持一病人多 course**,那是另一条改动。
        /// <c>dominant d</c> 在此恒 = <see cref="DiseaseCourse.DiseaseId"/>。</item>
        /// <item><b><c>trend_backward_agg</c> —— 本轮已接真,但 <c>W</c> 未配置 ⇒ 恒 0</b>
        /// 2026-10-22 第四轮起调 <see cref="ProgressionEvaluator.ComputeTrendBackward"/>
        /// (GDD <c>:921</c> 的后向差分),**不再**误传 <c>result.Trend</c>(那是 F2 的中心差分,
        /// 读未来,只供 8 的展示读数)。但 <c>W</c> / <c>TREND_EPS</c> 归数值轮
        /// (GDD Tuning <c>:1743-1744</c> 均标 <c>*待定*</c>)⇒ 本批传 0 ⇒ 函数返回 0。
        /// ⚠️ 这意味着痊愈支的「非上升期门」当前**恒放行** —— 但它仍被
        /// <c>window</c> 未配置挡在外面(见第 3 条),不会静默判愈。</item>
        /// <item><b><c>window</c> = <c>default</c>(<c>W</c> / <c>TREND_EPS</c> 未裁定)</b> ——
        /// ⇒ <b>痊愈支与迁延支本批不求值</b>,退回纯阈值分级。这不是遗漏:
        /// 两值归数值轮(GDD Tuning <c>:1743-1744</c> 均标 <c>*待定*</c>),
        /// 本批不代拍。求值器对 <c>default</c> 的行为已有测试钉住
        /// (<c>test_unconfiguredWindow_recoveredNotEvaluated</c>)。</item>
        /// </list>
        ///
        /// <para><b>已接通的三通道</b>:</para>
        /// <list type="bullet">
        /// <item><b>有效对因处置</b> —— 经 <see cref="ProgressionEvaluator.IsEffectiveCausalDose"/>
        /// 逐剂量筛(F1 与 F4 共用**同一实现**,不复制判据)。</item>
        /// <item><b>照护(2026-10-22 第五轮接真)</b> —— 经
        /// <see cref="CareLeverApplicability.AppliesTo"/> 判适用集(**破伤风不在内**,K8),
        /// 再从本病人的 <see cref="DiseaseCourse.LastCareStopTick"/> /
        /// <see cref="DiseaseCourse.LastCareActionTick"/> 取两条时刻。
        /// <para>🔁 **原文(第五轮)写「写者仍未落 … 生产流里目前零护理事件 ⇒ 本通道在生产路径上
        /// 仍恒 <c>None</c>」—— 前半句已失效(原文不删)**。2026-10-22 写者落地:
        /// <c>Sim/CareAppliedWriter.cs</c>,且 `WriterExistenceGate` 的 `CareApplied` 豁免已撤
        /// (撤表 = 该 Kind 转为被门正面盯住,判据① 强制同批)。</para>
        /// <para>⚠️ **但「生产流里有护理事件」仍不成立**,原因是**接线**而非**写者** ——
        /// 本类在构造期注入写者之前无从产生事件,而生产触发点(24 的护理交互 / 医馆护理台 /
        /// 4 的交互装配)尚未接线(与 `CaseOpenWriter` 同格:有写者 ≠ 已接线)。
        /// ⇒ **结论不变(恒 None),理由变了**:从「没人写」变成「没人调」。
        /// 读取面仍完整:接线一落即自动生效。</para></item>
        /// <item><b><c>onset_tick</c> 哨兵</b> —— 由 <see cref="LastInterventionAggregator.Evaluate"/>
        /// 在三条通道全空时显式返回(不是靠初值巧合)。</item>
        /// </list>
        /// </remarks>
        private static InjuryState EvaluateOutcome(
            DiseaseCourse course, DiseaseRegistryEntry entry, ProgressionResult result, long tick,
            ulong worldSeed, long trendWindowTicks, long trendEpsRaw,
            ITreatableByLookup treatableBy, ActorClass actorClass)
        {
            // 注:本方法**静态** ⇒ 不持有任何状态;照护通道的时刻由 `course` 上的两个字段提供
            // (2026-10-22 第五轮接真,见下通道 ②)。
            // 🔁 2026-10-22 铺管轮:`W` / `TREND_EPS` 以**形参**传入而非读实例字段 ——
            //    本方法静态,而两值住在 `DiseaseVitalsService` 的 readonly 字段上
            //    (B 代理原稿直接引用 `_trendWindowTicks`,四个 CS0120)。
            //    取形参而非把本方法改非静态:静态是刻意的(注释见上),改它会放开
            //    「求值体偷偷读服务状态」的口子。
            // 🔁 原文(第三轮)写「照护通道的『恒空』由调用点决定,将来接上 `CareApplied` 时
            //    把摘要作为第 4 个入参传进来即可」—— **已失实,就地加注不删**:实际落地取的是
            //    「档案持有两个时刻字段」而不是「第 4 个入参」,因为 `Apply()` 已经解码并归档,
            //    再往求值体传一份摘要就是同一事实的两个载体(易分叉)。求值式本身确实未变。
            // ── F2 投影:position = clamp(Progress / SCALE, 0, 1) ──
            // ⚠️ 与 GetVitals 的投影桥**同式**,但**不复用同一行代码**是刻意的:
            //    GetVitals 在 SCALE ≤ 0 时 fail-loud 抛,那是「取数面」的契约;
            //    本方法在 tick 循环里,一个坏条目不该让整条 Step 崩。
            //    ⇒ 此处 SCALE ≤ 0 时把 position 压成 0(不判死),并**不**静默 ——
            //    条目坏由 R1-23 在写入期拦,这里只是不在 tick 内抛。
            Fix position = Fix.Zero;
            if (entry.Scale.Raw > 0)
            {
                position = result.Position / entry.Scale;
                if (position.Raw < Fix.ZeroRaw) position = Fix.Zero;
                if (position.Raw > Fix.OneRaw) position = Fix.One;
            }

            // ── 通道 ①:有效对因处置(F1 与 F4 同一判据)──
            long[] causalTicks = CollectEffectiveCausalTicks(entry, course, treatableBy);

            // ── 通道 ②:照护(2026-10-22 第五轮接真)──
            //   GDD `:1126-1133`:照护支 = max(最后一条护理动作止, 最后一条护理动作事件 + CARE_GAP)。
            //   ⚠️ **适用集门在这里**(不在档案里):破伤风护理**不刷新**死亡窗口(K8),
            //      故先问 `CareLeverApplicability.AppliesTo(entry)`,不适用就传 None ——
            //      护理事件仍进流、仍进档(它是有效动作),只是不参与 F4 的窗口。
            //   🔁 **原文(第五轮)写「`CareApplied` 的写者未落 … 生产流里目前仍无护理事件」
            //      —— 已失效(原文不删)**。2026-10-22 写者落地:`Sim/CareAppliedWriter.cs`;
            //      `WriterExistenceGate` 的 `CareApplied` 豁免同批撤除。
            //   ⚠️ **但本通道在生产路径上仍恒 None,理由已换**:不再是「没人写」,而是
            //      **「没人调」** —— 生产触发点(24 的护理交互 / 医馆护理台 / 4 的交互装配)
            //      尚未接线(与 `CaseOpenWriter` 同格)。读取面仍完整:接线一落即自动生效。
            //      这与第五轮「恒 None 且无从读取」不是同一状态,也与「写者未落」不是同一状态。
            var care = CareLeverApplicability.AppliesTo(entry)
                ? new LastInterventionAggregator.CareChannelInput(
                    course.LastCareStopTick, course.LastCareActionTick)
                : LastInterventionAggregator.CareChannelInput.None;

            // ── 通道 ③:trend_backward(2026-10-22 第四轮接真)──
            //   ⚠️ **不是 `result.Trend`** —— 那是 F2 的**中心差分**(读未来,只供 8 的展示),
            //   而 F4 的状态转移按 GDD `:921`/`:936-937` 必须吃**后向**差分 `trend_backward`
            //   (只读过去,否则「在 tick t 上用未来时刻决定状态 = 非因果」)。
            //   本轮之前这里传的是 `result.Trend`,名字写着 `trendBackwardAgg` 而值是中心差分
            //   —— 两者当时都是 tripwire 恒 0,所以看不出来;语义从一开始就是错的。
            //
            //   `W` / `TREND_EPS` 的来源(2026-10-22 铺管):**全局配置**,经构造参数
            //   `trendWindowTicks` / `trendEpsRaw` 从数据面灌入(作者态键
            //   `trend_window_ticks` / `trend_eps`,见 `disease_registry.json`)。
            //   ⚠️ **当前值仍为 0**(数值轮未裁)⇒ 本函数返回 0 ⇒ 痊愈支 / 迁延支
            //   **仍不求值**。「管道已通」≠「功能可用」:数值一填即生效,填之前两支是死的。
            Fix trendBackward = ProgressionEvaluator.ComputeTrendBackward(
                entry, course.OnsetTick, tick, course.Doses, worldSeed, course.Patient,
                windowTicks: trendWindowTicks,
                trendEpsRaw: trendEpsRaw,
                doseClampBound: null,
                treatableBy: treatableBy);

            // 趋势窗口 —— W / TREND_EPS 从全局配置灌入(见构造参数注释)。
            //   🔁 **原文(2026-10-22 铺管轮)写「`LowPositionHeldTicks` 恒传 0,累计器尚未实现,
            //      ⇒ 即使 W > 0 痊愈支仍不会成立」—— 已失效(原文不删)**。
            //   2026-10-22 B2 修复:第三参改由 `ProgressionEvaluator.ComputeLowPositionHeldTicks`
            //   求值 —— 它是 **t 的纯函数**(反查历史 tick 的 Progress),不是跨 tick 计数器。
            //   取舍理由见该方法的 remarks(积分量会让读档后同刻判出两种终态)。
            //   ⚠️ **仍未变的**:`W` / `TREND_EPS` 本身仍为 0(数值轮未裁)⇒ `IsConfigured = false`
            //      ⇒ 条件 ⑥ 与两支**都不求值**。「值填了才生效」这句话对**两支都成立**了,
            //      不再是「迁延支能生效、痊愈支不能」的偏斜状态。
            long lowPositionHeldTicks = ProgressionEvaluator.ComputeLowPositionHeldTicks(
                entry, course.OnsetTick, tick, course.Doses, worldSeed, course.Patient,
                windowTicks: trendWindowTicks,
                doseClampBound: null,
                treatableBy: treatableBy);
            var window = new RecoveryWindowInput(trendWindowTicks, trendEpsRaw, lowPositionHeldTicks);

            return F4OutcomeEvaluator.Evaluate(
                // 🔁 2026-10-24(story-008 AC-9-26):原写死 `ActorClass.Patient`(原文不删)。
                //   接线前它**结论正确**(只有病人进 F4),但让「这个 course 属于谁」无载体
                //   ⇒ 死亡候选无法区分玩家与病人。现由注入的查询面给出;
                //   未接线时回退值仍是 `ActorClass.Patient`(见构造)。
                actorClass,
                entry,
                position,
                trendBackward,               // trend_backward_agg(单病种 ⇒ 恒等;W 未配置 ⇒ 0)
                course.OnsetTick,
                tick,
                new LastInterventionAggregator.TreatmentChannelInput(causalTicks),
                care,
                window);                     // W / TREND_EPS 为 0 ⇒ IsConfigured=false ⇒ 两支不求值
        }

        /// <summary>
        /// 从本病人的剂量列表里筛出「有效对因处置」的 tick(升序不必 —— 聚合器取 max)。
        /// </summary>
        /// <remarks>
        /// 判据**唯一实现**在 <see cref="ProgressionEvaluator.IsEffectiveCausalDose"/>;
        /// 本方法只做遍历与装箱,不复制三条件(否则与 F1 的 Σ 静默分叉)。
        /// </remarks>
        private static long[] CollectEffectiveCausalTicks(
            DiseaseRegistryEntry entry, DiseaseCourse course, ITreatableByLookup treatableBy)
        {
            IReadOnlyList<TreatmentDose> doses = course.Doses;
            if (doses == null || doses.Count == 0) return Array.Empty<long>();

            // 两遍:先数,再填 —— 避免 List<long> 装箱与扩容抖动(每 tick 每病人一次)。
            // ⚠️ `IReadOnlyList<T>` 的索引器返回**值**,不是变量 ⇒ 不能写 `in doses[i]`
            //    (CS8156)。先落到局部变量再 `in` 传。
            int count = 0;
            for (int i = 0; i < doses.Count; i++)
            {
                TreatmentDose d = doses[i];
                if (ProgressionEvaluator.IsEffectiveCausalDose(entry, in d, treatableBy)) count++;
            }

            if (count == 0) return Array.Empty<long>();

            var ticks = new long[count];
            int w = 0;
            for (int i = 0; i < doses.Count; i++)
            {
                TreatmentDose d = doses[i];
                if (ProgressionEvaluator.IsEffectiveCausalDose(entry, in d, treatableBy))
                    ticks[w++] = d.Tick;
            }
            return ticks;
        }
    }
}
