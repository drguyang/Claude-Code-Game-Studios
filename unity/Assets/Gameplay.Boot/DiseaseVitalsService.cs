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
        /// 构造(fail-loud:任一依赖缺失 = 具名 <see cref="ArgumentNullException"/>,无半装配)。
        /// </summary>
        /// <param name="stream">病史事件流(只读消费)。</param>
        /// <param name="blobPool">blob 池读面(载荷字节寻址)。</param>
        /// <param name="registry">病种注册表(直传条目;批次 B 裁定无 id 查找接口,
        /// 本类在装配期自建 id → 条目的字典,**不**向 Sim 侧新增查找面)。</param>
        /// <param name="worldSeed">世界种子(Noise 纯函数入参;Noise 现为 tripwire 恒 0)。</param>
        /// <exception cref="ArgumentNullException">任一引用依赖为 null(参数名具名)。</exception>
        /// <exception cref="ArgumentException">注册表条目为 null / 病种 id 重复(参数名具名)。</exception>
        public DiseaseVitalsService(EventStream stream, IBlobPool blobPool,
                                    IReadOnlyList<DiseaseRegistryEntry> registry,
                                    ulong worldSeed)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _blobPool = blobPool ?? throw new ArgumentNullException(nameof(blobPool));
            _worldSeed = worldSeed;

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

            // trend:tripwire 恒 0(ComputeTrend 未接;阶段 0 口径保留占位)
            Fix trend = result.Trend;

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
                    entry, course.OnsetTick, tick, course.Doses, _worldSeed, course.Patient);
                _results[course.Patient.Value] = result;

                // ══ F4 求值(2026-10-22 第三轮接线)══
                // 用户裁定:「接 F4 求值,**但不写流**」—— 本段只求值并缓存,
                // 零 `IEventSink.Append`(本类全程不写流的既有纪律不变)。
                _outcomes[course.Patient.Value] = EvaluateOutcome(course, entry, result, tick);
            }
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
        /// <item><b><c>trend_backward_agg</c> 恒 0</b> —— <see cref="ProgressionEvaluator.ComputeTrend"/>
        /// 当前是 tripwire(恒 <c>Fix.Zero</c>,见其注释)。故传入的是 <c>result.Trend</c>
        /// (现恒 0),不是真差分。⚠️ 这意味着痊愈支的「非上升期门」当前**恒放行** ——
        /// 但它仍被 <c>window</c> 未配置挡在外面(见第 3 条),不会静默判愈。</item>
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
        /// <item><b>照护</b> —— 经 <see cref="CareLeverApplicability.AppliesTo"/> 判适用集
        /// (**破伤风不在内**,K8),再取本病人已归档的护理动作摘要。
        /// ⚠️ 当前恒 <c>None</c>:<c>CareApplied</c> 的写者未落
        /// (<c>WriterExistenceGate</c> 具名登记「24 医馆与案头写者未落」)⇒ 流里**没有**护理事件。
        /// 本段不伪造该通道 —— 写者落地后在此接上即可。</item>
        /// <item><b><c>onset_tick</c> 哨兵</b> —— 由 <see cref="LastInterventionAggregator.Evaluate"/>
        /// 在三条通道全空时显式返回(不是靠初值巧合)。</item>
        /// </list>
        /// </remarks>
        private static InjuryState EvaluateOutcome(
            DiseaseCourse course, DiseaseRegistryEntry entry, ProgressionResult result, long tick)
        {
            // 注:本方法**静态** ⇒ 不持有任何状态。照护通道的「恒空」由调用点决定(见下),
            // 将来接上 `CareApplied` 时,把摘要作为第 4 个入参传进来即可,求值式本身不变。
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
            long[] causalTicks = CollectEffectiveCausalTicks(entry, course);

            // ── 通道 ②:照护 —— 本批**恒空**,且理由是结构性的,不是遗漏 ──
            //   `CareApplied` 的写者未落(`WriterExistenceGate` 具名登记
            //   「24 医馆与案头写者未落」)⇒ 流里**零**护理事件 ⇒ 无从算 `CareChannelInput`。
            //   本段**不伪造**该通道(伪造 = 让「照护续命」在数据面不存在时看起来已接线)。
            //   ⚠️ 破伤风的双约束(K8)由 `CareLeverApplicability.AppliesTo` 保证;
            //   写者落地后在此处按 `AppliesTo(entry)` 分流即可,求值侧不必改。
            var care = LastInterventionAggregator.CareChannelInput.None;

            return F4OutcomeEvaluator.Evaluate(
                ActorClass.Patient,          // 病人侧 F4 —— 不走玩家专属的战伤破例
                entry,
                position,
                result.Trend,                // trend_backward_agg(当前恒 0,见 remarks 第 2 条)
                course.OnsetTick,
                tick,
                new LastInterventionAggregator.TreatmentChannelInput(causalTicks),
                care,
                default(RecoveryWindowInput));   // W / TREND_EPS 未裁定 ⇒ 痊愈支/迁延支不求值
        }

        /// <summary>
        /// 从本病人的剂量列表里筛出「有效对因处置」的 tick(升序不必 —— 聚合器取 max)。
        /// </summary>
        /// <remarks>
        /// 判据**唯一实现**在 <see cref="ProgressionEvaluator.IsEffectiveCausalDose"/>;
        /// 本方法只做遍历与装箱,不复制三条件(否则与 F1 的 Σ 静默分叉)。
        /// </remarks>
        private static long[] CollectEffectiveCausalTicks(
            DiseaseRegistryEntry entry, DiseaseCourse course)
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
                if (ProgressionEvaluator.IsEffectiveCausalDose(entry, in d)) count++;
            }

            if (count == 0) return Array.Empty<long>();

            var ticks = new long[count];
            int w = 0;
            for (int i = 0; i < doses.Count; i++)
            {
                TreatmentDose d = doses[i];
                if (ProgressionEvaluator.IsEffectiveCausalDose(entry, in d))
                    ticks[w++] = d.Tick;
            }
            return ticks;
        }
    }
}
