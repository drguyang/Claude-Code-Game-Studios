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

                _results[course.Patient.Value] = ProgressionEvaluator.Evaluate(
                    entry, course.OnsetTick, tick, course.Doses, _worldSeed, course.Patient);
            }
        }
    }
}
