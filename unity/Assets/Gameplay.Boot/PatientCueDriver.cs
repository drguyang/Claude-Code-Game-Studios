// 权威来源:production/epics/audio-system/story-018-runtime-audio-assembly.md(AC-018-6)
//   · A-3「`PatientCueEmit` / `CoughChannel` / `CueEmitDeriver` 零生产调用」
// GDD design/gdd/patient-ai.md F-13.5(Emit / CueInterval 分段常函数 / 相位)· AC-13-D2/D3/D4
// ADR-018 §一(44 只触发)· §四(医学准确性:咳嗽不停呼吸层)· ADR-016 §一(决策进 sim,
//   13 的 cue 决策是**派生态**,由三源重建 —— 本类只读 `IVitalsQuery` + 在场册)
// ADR-005 抽象点 `ITickProvider`(本类**不**自己推进 tick —— 由 `BootRoot` 的 tick 边沿驱动)
//
// ⚠️ **为什么这个类是 A-3 的落点,而不是直接调 `PatientCueEmit`**:
//   `PatientCueEmit.Emit` 只是**发射桥**(把 kind/position 装成 `AudioCueDto` 后调 sink),
//   它不含**调度**(该不该发 / 发什么 / 呼吸层起停)。调度归 13 的 `PatientCueSchedule.Decide`。
//   A-3 的缺口正是「调度器与发射桥**都没有调用方**」—— 本类补的就是那个调用方:
//   每 tick 边沿,对每个在场病人跑一次 `Decide`,按结果驱动 sink。
//
// ⚠️ **呼吸层句柄归本类持有**(ADR-018 §一「由唯一持有者(上游)负责收尾」)——
//   本类即该持有者。句柄**不落盘、不进流**(呈现层事实)。
//
// ⚠️ **cue 种类选择**(GDD F-13.5 `CueKindOf(p, signs)`):P0 = **固定咳嗽**
//   (`CueKind.Cough`)。呻吟的选择规则住 8 的词条语义,真表到位前不得在 13 侧自造 ——
//   登记为本类的 P0 简化,见 `## Test Evidence`(禁借绿:不声称已实现 `CueKindOf`)。
//
// ⚠️ **cue ordinal 由装配期从事件表解析**(构造参数 `cueOrdinals`),**不**用
//   `(int)CueKind` 当 ordinal —— 后者今日恰好等于 `Voice_Cough_Damp` 的 ordinal 0,
//   那是**巧合不是契约**:重排 `assets/data/audio_events.json` 的 `rows[]` 就会让
//   「咳嗽」静默播成别的 cue(ADR-014 §五 已登记「ordinal = 行序」这一脆弱面)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{

    /// <summary>病人 cue 的运行期驱动(A-3):tick 边沿 → 13 调度 → 44 发射。
    /// <para>⚠️ 本类是**装配层**(`Gameplay.Boot`),不是 13 也不是 44 —— 它把两者的接口接起来,
    /// 自己不产生任何 cue 语义。</para>
    /// <para>⚠️ **停顿 vs 停止的音频分界**(story-019 · AC-019-4 / OQ-44-7):
    /// 13 只给三种呼吸动作(`Begin` / `None` / `End`),**`End` 本身不区分两态**
    /// (它只表达「终局锁存,呼吸层不该再响」;痊愈与死亡锁存位相同,13 刻意不报告是哪一种)。
    /// 故本驱动把分界补在**在场性**上:loop 仍活 + 病人**仍在场** ⇒「停顿」—— loop <b>存续</b>
    /// 并进入 <see cref="BreathTailRamp"/> 的增益衰减(极轻气流尾);病人**离场**(或被
    /// <see cref="StopAll"/> 收尾)⇒「停止」—— loop <c>EndLoop</c>,<b>完全无声</b>,不播任何尾音。
    /// 时刻表 = 停顿起拍 tick + <see cref="AudioTuning.BreathTailRampSeconds"/>(按 20 Hz —— ADR-005
    /// 裁定值 —— 换成整数 tick 数;不读墙钟)。分界依据 = 13 的终局锁存(`End` 只在锁存后出现)+
    /// 在场登记簿,装配层**不自造**终局判据。</para>
    /// </summary>
    public sealed class PatientCueDriver
    {
        private readonly IAudioCueSink _sink;
        private readonly IVitalsQuery _vitals;
        private readonly PresenceRegistry _presence;
        private readonly PatientBehaviorDirector _behavior;
        private readonly CueIntervals _intervals;
        private readonly int? _coughCue;
        private readonly int? _breathCue;
        private readonly Func<PatientId, bool> _hasCourse;
        // story-021:`KnowsClinic(p)` 的**注入缝**(F-13.2 真值源 = 13 的 HomeRegion 语义)。
        // ⚠️ 装配层**不得发明**「知不知道医馆」—— 此前本类逐字写 `knowsClinic: false`,
        //    等于把 F-13.2 的一个判定轴焊死在装配层(所有病人永不进 Seeking)。
        private readonly Func<PatientId, bool> _knowsClinic;

        // 每病人的发射 / 呼吸层簿记(呈现层事实,不落盘)。
        // ⚠️ `long`(S15-5 · TD-audio-032 ②):逻辑 tick 是单调 `long`;在这里截成 `int`
        //    会在约 2^31 拍后回绕,回绕的后果不是「差一拍」而是**一次性 cue 静默停发**
        //    (`tick >= firstDue` 恒假)。
        private readonly Dictionary<int, long> _lastEmitTick = new Dictionary<int, long>();
        private readonly Dictionary<int, long> _entryTick = new Dictionary<int, long>();
        private readonly Dictionary<int, AudioCueHandle> _breathHandles = new Dictionary<int, AudioCueHandle>();

        // 停顿态簿记(story-019):终局锁存但仍在场 ⇒ loop 存续 + 尾音衰减。
        //   _tailStartTick = **正在衰减**的段起点(到期即删 ⇒ 这个字典的 Count 就是「正在衰减的人数」)。
        //   _tailDone      = **已衰减完**的病人的封口增益(**不删** —— 每拍重写同值)。
        // ⚠️ **两表缺一不可**:13 的 `Decide` 在终局锁存期间**每一拍**都给 `End`(`patient-ai`
        //    `PatientCueSchedule.Decide`,终局 ⇒ `breathAlive ? End : None`)。若只有 `_tailStartTick`,
        //    到期删除后的下一拍 `End` 会**重开新段** ⇒ 尾音每 `BreathTailRampMs` 从满幅重来一次
        //    = **一阵一阵**(2026-10-18 实测坐实为结构性必然,非「风险」;用户裁定修实现)。
        //    两表合起来满足「**只衰减一次**」:**每病人至多一个段**(互斥不变量)。
        private readonly Dictionary<int, long> _tailStartTick = new Dictionary<int, long>();
        //   ⚠️ 封口值 = 线性增益 **double**(W-2 2026-10-20 接线轮):此前 `byte`(8-bit 量化,
        //      −36 dB 底值 ⇒ 4/255);现与 `BreathTailRamp` 的线性 volume 域同域。
        //   `_tailDone` 按值不进三流,封口值亦不进任何 sink —— 见 `IsBreathTailDone`。
        private readonly Dictionary<int, double> _tailDone = new Dictionary<int, double>();

        /// <summary>构造。</summary>
        /// <param name="sink">44 的 cue 界面(非 null;ADR-018 §一「只入不出」)。</param>
        /// <param name="vitals">9 的体征查询(唯一取数入口,AC-13-A2)。</param>
        /// <param name="presence">在场登记簿(取在场集 + 锚点格)。</param>
        /// <param name="behavior">13 的行为导演(症状档 / 终态锁存)。</param>
        /// <param name="intervals">cue 间隔表(数值归用户;null ⇒ <see cref="CueIntervals.Default"/>)。</param>
        /// <param name="coughCue">咳嗽 cue 的**事件表 ordinal**(null ⇒ 不发射一次性 cue)。
        /// <para>⚠️ 必须是装配期从事件表解析出的真 ordinal,不得写 `(int)CueKind.Cough`
        /// (见文件头注)。</para></param>
        /// <param name="breathCue">呼吸层 cue 的**事件表 ordinal**(null ⇒ 不起呼吸层循环)。</param>
        /// <param name="knowsClinic">`KnowsClinic(p)` 的注入缝(story-021)。**必填** ——
        /// 传 null 即 <see cref="ArgumentNullException"/>,**不提供「静默 false」的默认值**:
        /// 静默默认正是本缝要消灭的形态(装配层发明行为输入)。生产 = 13 的
        /// `ClinicKnowledge.KnowsClinic(spawnAnchor(p))`;测试可传恒真 / 恒假。</param>
        /// <param name="hasCourse">「该病人已被 9 建档」的探测面(null ⇒ 视为恒真)。
        /// <para>⚠️ <b>为什么需要</b>:在场登记先于建档 —— `PatientAppearedDriver` 在 tick T 把病人
        /// 写进 <see cref="PresenceRegistry"/> 并写一条 `DiseaseOnset`,而该事件要等 **T+1** 的
        /// `DiseaseVitalsService.OnTickEdge` 才被应用、病人才有档。若本类不加此门,新病人会在
        /// T 这一拍撞上 `IVitalsQuery.GetVitals` 的 fail-loud(`PatientCourseNotFoundException`)
        /// 而炸掉整个 tick 泵。生产 = <c>vitals.Courses.TryGet</c>;测试可传恒真。</para></param>
        /// <exception cref="ArgumentNullException">任一引用依赖为 null。</exception>
        public PatientCueDriver(IAudioCueSink sink, IVitalsQuery vitals, PresenceRegistry presence,
                                PatientBehaviorDirector behavior, CueIntervals? intervals = null,
                                int? coughCue = null, int? breathCue = null,
                                Func<PatientId, bool> knowsClinic = null,
                                Func<PatientId, bool> hasCourse = null)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _vitals = vitals ?? throw new ArgumentNullException(nameof(vitals));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
            _intervals = intervals ?? CueIntervals.Default;
            _coughCue = coughCue;
            _breathCue = breathCue;
            _hasCourse = hasCourse;
            _knowsClinic = knowsClinic ?? throw new ArgumentNullException(nameof(knowsClinic),
                "knowsClinic 注入缝必填(story-021)—— F-13.2 的真值源归 13 的 HomeRegion 语义," +
                "装配层不得写死 false / true(AC-021-1)。生产见 CompositionRoot 的接线。");

            // 构造期硬校验(与 PatientBehaviorDirector 的破表即抛同口径)。
            List<string> errs = CueIntervals.Validate(_intervals);
            if (errs.Count > 0)
                throw new ArgumentException(
                    "CUE_INTERVAL 破硬约束(GDD §Tuning 三)—— 档间有序不可辨:\n" +
                    string.Join("\n", errs), nameof(intervals));
        }

        /// <summary>一次性 cue 的累计发射次数(可观测面 —— 「病人真的会咳了」的判据)。</summary>
        public int EmitCount { get; private set; }

        /// <summary>最近一次被 tick 泵驱动的逻辑 tick(`-1` = 从未被驱动)。
        /// <para>⚠️ **这是「启动序真的把本驱动接上 tick 泵了吗」的唯一可观测面** ——
        /// <see cref="EmitCount"/> / <see cref="ActiveBreathCount"/> 都是**下游结果**,
        /// 会被上游条件(如终局锁存)压成 0,单看它们无法区分「没接上」与「接上了但没到该发的时候」。
        /// 生产价值:诊断 / 开发者视图读它即可判定装配是否成立。</para></summary>
        public long LastDrivenTick { get; private set; } = -1;

        /// <summary>装配层**实际注入**的 `KnowsClinic(p)` 判定(可观测面;story-021)。
        /// <para>⚠️ 存在的理由与 <see cref="LastDrivenTick"/> 同族:装配正确性必须**可判定**,
        /// 不能靠读源码猜。装配层若把注入缝换成写死常量,本方法立刻可观测到 —— 而不必等到
        /// 某个病人的体征恰好落进寻诊带才发现(那要一整套体征链夹具)。
        /// 本方法只**转发**注入的委托,不参与任何决策(决策仍在 `OnTickEdge` 内)。</para></summary>
        /// <param name="patientId">病人。</param>
        public bool KnowsClinicOf(PatientId patientId) => _knowsClinic(patientId);

        /// <summary>仍在簿记里的病人数(五本表的键并集大小)。
        /// <para>⚠️ 存在的理由(S15-5 · TD-audio-032 ④):「离场后簿记是否残留」此前**不可观测** ——
        /// 三本表是私有字典,残留只表现为「会话内单调增长」。本属性把该判据做成可读面;
        /// 稳态应等于在场病人数(`PruneAbsent` 每边沿跑一次)。</para></summary>
        public int TrackedPatientCount
        {
            get
            {
                var keys = new HashSet<int>();
                foreach (int k in _breathHandles.Keys) keys.Add(k);
                foreach (int k in _entryTick.Keys) keys.Add(k);
                foreach (int k in _lastEmitTick.Keys) keys.Add(k);
                foreach (int k in _tailStartTick.Keys) keys.Add(k);
                foreach (int k in _tailDone.Keys) keys.Add(k);
                return keys.Count;
            }
        }

        /// <summary>呼吸层当前存活的句柄数。</summary>
        public int ActiveBreathCount => _breathHandles.Count;

        /// <summary>当前**正在衰减**的停顿态病人数(终局锁存 + 病人仍在场 + loop 存续 + 尾音未到位)。
        /// <para>⚠️ 与 <see cref="ActiveBreathCount"/> 的分界 = 「停顿 vs 停止」的**机器面**:
        /// 停顿的 loop **存活**(`ActiveBreathCount` 含它),停止的 loop 已 <c>EndLoop</c>(两者都不含它)。
        /// 尾音衰减到位后本值归零,**loop 仍然存活**(病人还在,只是气流尾已经听不见)。
        /// ⇒ **尾音到位 ≠ 首拍:** 停顿态在首拍即入表,衰减走完后才归零(本值从 1 降到 0 需要
        /// <c>BreathTailRampMs</c> 那么久)。归零后**不再重开**(尾音只衰减一次)。
        /// 听测面归 AC-019-6。</para></summary>
        public int PausedBreathCount => _tailStartTick.Count;

        /// <summary>本驱动送达 sink 的**尾音增益更新次数**(观测面)。
        /// <para>⚠️ 只计**停顿态**的增益写入 —— 稳态(未锁存)行走的是既有的 `<see cref="AudioCueDto"/>`
        /// 定时发射路径,不经本计数。呼吸过渡期该值应随 tick 增长(连续),**不应**与一次性 cue
        /// 计数同节奏(AC-019-3 的判别补充面)。</para></summary>
        public int BreathGainUpdateCount { get; private set; }

        /// <summary>取某病人的尾音进度(秒;不在停顿态 ⇒ false)。
        /// <para>入参 <paramref name="tick"/> 由调用方给(禁读墙钟 —— 本驱动不持时钟)。</para></summary>
        /// <param name="patientId">病人 id。</param>
        /// <param name="tick">当前逻辑 tick。</param>
        /// <param name="elapsedSeconds">出参:已衰减时长(秒)。</param>
        /// <returns>该病人当前处于停顿态 ⇒ true。</returns>
        public bool TryGetBreathTail(int patientId, long tick, out double elapsedSeconds)
        {
            elapsedSeconds = 0.0;
            if (!_tailStartTick.TryGetValue(patientId, out long start)) return false;
            elapsedSeconds = (tick - start) * TickSeconds;
            if (elapsedSeconds < 0.0) elapsedSeconds = 0.0;
            return true;
        }

        /// <summary>取某病人是否**已走完**尾音衰减(尾音只衰减一次 ⇒ 终局锁存期一旦完成即恒 true)。
        /// <para>此时尾音已封口在底值增益上,loop 仍存续(停顿 ≠ 停止),本驱动**不再写增益**。</para></summary>
        /// <param name="patientId">病人 id。</param>
        /// <returns>该病人尾音已衰减完(且尚未离场 / 未 <see cref="StopAll"/>)。</returns>
        public bool IsBreathTailDone(int patientId) => _tailDone.ContainsKey(patientId);

        /// <summary>单个逻辑 tick 的秒长(ADR-005 裁定 20 Hz ⇒ `TICK_SECONDS = 0.05`)。
        /// <para>⚠️ **不新增第二个真源**:该值取自已裁定的 tick 频率常数;若将来改频,
        /// 两处须同批改(登记在 story-019 §实现轮记录)。</para></summary>
        public const double TickSeconds = 0.05;

        // 簿记清扫的复用 buffer(S15-5 · TD-audio-032 ④:禁每 tick / 每边沿分配)。
        private readonly List<int> _staleScratch = new List<int>();
        private readonly HashSet<int> _presentScratch = new HashSet<int>();

        /// <summary>tick 边沿驱动 —— 对每个在场病人跑一次调度。
        /// <para>⚠️ <b>只做编排</b>(S15-5 · TD-audio-032 ③):取数 / 簿记 / 调度 / 发射各拆成
        /// 私有方法,每个方法体都 ≤ 40 物理行 —— 由 `s15_5_hygiene_test` 按**源码物理行**钉界。</para></summary>
        /// <param name="tick">本边沿自己的逻辑 tick(由帧泵回推,非末 tick)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");

            LastDrivenTick = tick;
            // 在场集快照(登记簿快照只在成员变动时重建 ⇒ 稳态零分配,TD-audio-032 ④)。
            foreach (int patientId in _presence.PresentPatientIds())
                StepOnePatient(patientId, tick);

            // ⛔ **2026-10-29:`TD-listen-01` 的 L-1 尝试被撤回** —— 曾在此按
            //    `behavior.Terminal.Latched` 调 `_presence.RemovePresent(...)`,实测**6 条既有 AC 转红**:
            //    `test_ac019_6_breathTail_decaysOnce_neverRestarts_whileTerminalPersists` 等。
            //    根因:`AC-019-6` 的尾音协议要求终态病人**继续留在在场集**(尾音正好在此区间内
            //    衰减一次且不得重启);一旦移除,`PruneAbsent` 立即清掉 `_tailDone` 等簿记 ⇒
            //    协议无从观测。**这是判据冲突,不是实现细节** —— 见
            //    `production/qa/evidence/listen-run-pack-2026-10-29.md` §六。
        }

        /// <summary>单病人一拍:取数 → 簿记 → 13 调度 → 呼吸层 / 尾音 / 一次性 cue。</summary>
        /// <param name="patientId">病人 id(登记簿键)。</param>
        /// <param name="tick">本边沿的逻辑 tick。</param>
        private void StepOnePatient(int patientId, long tick)
        {
            var id = new PatientId(patientId);
            if (!_presence.TryGetCell(id, out WorldPos cell)) return;   // 刚离场 ⇒ 跳过
            // 已登记在场但**尚未建档**(DiseaseOnset 要下一拍才被应用)⇒ 跳过本拍。
            // ⚠️ 不是「静默失败」:下一拍就自然进入(见构造参数 hasCourse 的说明)。
            if (_hasCourse != null && !_hasCourse(id)) return;

            // ① 13 的行为态 —— 读一次 IVitalsQuery(唯一取数入口);behavior 是 13 持有的引用,只读。
            _behavior.Step(id, _knowsClinic(id));
            PatientBehavior behavior = _behavior.For(id);
            VitalsDto v = _vitals.GetVitals(id);


            CueDispatch dispatch = DecideFor(patientId, id, behavior, v, tick);
            ApplyBreath(patientId, id, dispatch.Breath, v.Position, cell, tick);

            // ⑤ 停顿态:loop 存续期间,每拍推进一次增益衰减(AC-019-5)。
            //    ⚠️ **零一次性 cue**(dispatch.EmitOneShot 在终局时恒 false,判决归 13),
            //    本步只写增益 —— 这是「无突变音效」在运行期的形态。
            if (_tailStartTick.TryGetValue(patientId, out long tailStart))
                AdvanceBreathTail(patientId, id, v.Position, cell, tick, tailStart);

            EmitOneShotIfDue(patientId, dispatch, v.Position, cell, tick);
        }

        /// <summary>13 的调度决策(纯函数;本类不改它的输入)+ 本类簿记的入场 / 上次发射 tick。</summary>
        private CueDispatch DecideFor(int patientId, PatientId id, in PatientBehavior behavior,
                                      in VitalsDto v, long tick)
        {
            // ② 入场 tick 的纪元(首拍相位相对入场时刻,F-13.5 ③)。
            if (!_entryTick.TryGetValue(patientId, out long entry))
            {
                entry = tick;
                _entryTick[patientId] = entry;
            }
            if (!_lastEmitTick.TryGetValue(patientId, out long lastEmit))
            {
                lastEmit = -1;
                _lastEmitTick[patientId] = lastEmit;
            }

            // ③ 13 的调度决策。
            return PatientCueSchedule.Decide(
                behavior.Current, behavior.Terminal, tick, lastEmit, id,
                behavior.Tier, v.Position, _intervals,
                breathAlive: _breathHandles.ContainsKey(patientId),
                kind: CueKind.Cough,             // P0 固定咳嗽(见文件头注)
                entryTick: entry);
        }

        /// <summary>一次性 cue(经 13 → 44 的唯一发射桥)。
        /// <para>⚠️ `coughCue` 为 null ⇒ 不发射(装配期未解析到事件表 / 测试只测调度)。
        /// 静默跳过而非抛:发射面缺一条 cue 不该炸掉整帧(见 AudioClipLibrary 同口径)。
        /// ⚠️ 但 `_lastEmitTick` **仍推进** —— 否则调度器每 tick 都判 due,
        /// 一旦装配补齐就会瞬间补发一整批(相位语义被打乱)。</para></summary>
        private void EmitOneShotIfDue(int patientId, in CueDispatch dispatch, float position,
                                      in WorldPos cell, long tick)
        {
            if (!dispatch.EmitOneShot) return;
            if (_coughCue.HasValue)
            {
                PatientCueEmit.Emit(_sink, patientId, _coughCue.Value, position,
                                    cell.X, cell.Y, cell.Z, looped: false);
                EmitCount++;
            }
            _lastEmitTick[patientId] = tick;
        }

        /// <summary>在场集收缩后清理簿记(离场 / 死亡病人的句柄不得残留)。</summary>
        /// <param name="tick">当前 tick(仅用于日志语义,不参与判定)。</param>
        /// <remarks>⚠️ 本方法即**「停止」形态的落点**:停顿态病人离场 ⇒ <c>EndLoop</c> +
        /// 尾音簿记清除,**不补播任何尾音**(GDD:571-573「停止 = 完全无声」)。
        /// <para>⚠️ S15-5(TD-audio-032 ④):簿记键 = **五本表的并集**且由在场集**白名单**过滤 ——
        /// 此前只跟呼吸句柄走,没有呼吸句柄但发过咳的病人其行永不清(会话内单调增长)。</para></remarks>
        public void PruneAbsent(long tick)
        {
            _staleScratch.Clear();
            _presentScratch.Clear();
            foreach (int patientId in _presence.PresentPatientIds()) _presentScratch.Add(patientId);

            PruneIfAbsent(_breathHandles.Keys);
            PruneIfAbsent(_entryTick.Keys);
            PruneIfAbsent(_lastEmitTick.Keys);
            PruneIfAbsent(_tailStartTick.Keys);
            PruneIfAbsent(_tailDone.Keys);
        }

        /// <summary>清理 <paramref name="keys"/> 中**已不在场**的全部行(下发即增即清,不跨表)。</summary>
        private void PruneIfAbsent(Dictionary<int, AudioCueHandle>.KeyCollection keys)
        {
            _staleScratch.Clear();
            foreach (int patientId in keys)
                if (!_presentScratch.Contains(patientId)) _staleScratch.Add(patientId);
            for (int i = 0; i < _staleScratch.Count; i++) PruneOneAbsent(_staleScratch[i]);
        }

        /// <inheritdoc cref="PruneIfAbsent(Dictionary{int, AudioCueHandle}.KeyCollection)"/>
        private void PruneIfAbsent(Dictionary<int, long>.KeyCollection keys)
        {
            _staleScratch.Clear();
            foreach (int patientId in keys)
                if (!_presentScratch.Contains(patientId)) _staleScratch.Add(patientId);
            for (int i = 0; i < _staleScratch.Count; i++) PruneOneAbsent(_staleScratch[i]);
        }

        /// <inheritdoc cref="PruneIfAbsent(Dictionary{int, AudioCueHandle}.KeyCollection)"/>
        private void PruneIfAbsent(Dictionary<int, double>.KeyCollection keys)
        {
            _staleScratch.Clear();
            foreach (int patientId in keys)
                if (!_presentScratch.Contains(patientId)) _staleScratch.Add(patientId);
            for (int i = 0; i < _staleScratch.Count; i++) PruneOneAbsent(_staleScratch[i]);
        }

        /// <summary>清理单个离场病人的**全部**簿记(先摘册后收尾,与 <see cref="StopAll"/> 同序)。</summary>
        private void PruneOneAbsent(int patientId)
        {
            if (_breathHandles.TryGetValue(patientId, out AudioCueHandle handle))
            {
                _sink.EndLoop(handle);
                _breathHandles.Remove(patientId);
            }
            _entryTick.Remove(patientId);
            _lastEmitTick.Remove(patientId);
            _tailStartTick.Remove(patientId);   // 停止 ⇒ 尾音簿记一并清除(不残留衰减进度)
            _tailDone.Remove(patientId);        // 同理:封口态也不得跨「停止」存活(再出现 = 新病人)
        }

        /// <summary>全部呼吸层收尾(场景卸载 / 读档前)。</summary>
        public void StopAll()
        {
            // ⚠️ 不就地遍历 `_breathHandles` —— `_sink.EndLoop` 是外部回调,若它同步反调本类
            //    (生产 sink 会),就地遍历即「集合已修改」异常。先快照句柄,再清册,最后逐个收尾
            //    (与 `PruneAbsent` 的「先摘册后收尾」同序)。
            var handles = new List<AudioCueHandle>(_breathHandles.Values);
            _breathHandles.Clear();
            _entryTick.Clear();
            _lastEmitTick.Clear();
            _tailStartTick.Clear();
            _tailDone.Clear();
            for (int i = 0; i < handles.Count; i++)
                _sink.EndLoop(handles[i]);
        }

        private void ApplyBreath(int patientId, PatientId id, BreathAction action, float position,
                                 in WorldPos cell, long tick)
        {
            switch (action)
            {
                case BreathAction.Begin:
                    // ⚠️ `breathCue` 为 null ⇒ 不起循环(装配期未解析到 `BreathLayer_Base_Calm`)。
                    //    绝不用咳嗽 ordinal 顶替 —— 那会让呼吸层播成咳嗽(素材语义错)。
                    if (!_breathCue.HasValue) break;
                    AudioCueDto cue = MakeBreathCue(patientId, position, cell, PatientCue.Intensity(position));
                    _breathHandles[patientId] = _sink.BeginLoop(cue);
                    break;

                case BreathAction.End:
                    // ⚠️ **停顿 vs 停止的分界就在这一格**。13 的 `End` 只表达「呼吸层不该再响」
                    //    (终局锁存),它**不区分**「病人还在(停顿)」与「病人已走(停止)」——
                    //    后者由 `PruneAbsent` 收尾。而 13 的判据本身把「痊愈」与「死亡」合并
                    //    (`BreathShouldLive` 只看锁存位,AC-13-D3「不向玩家报告是哪一种」)⇒
                    //    **不能**按锁存位二分为「痊愈 = 停 / 死亡 = 停顿」(两者锁存位相同)。
                    //    故本驱动的分界取**在场性**:loop 仍活 + 病人仍在场 ⇒ 转入停顿
                    //    (loop 存续 + 尾音衰减);离场 ⇒ `PruneAbsent` 的 `EndLoop`(完全无声,
                    //    不补播尾音 —— GDD:571-573「停止 = 完全无声」)。
                    if (!_breathHandles.ContainsKey(patientId)) break;
                    // ⚠️ **尾音只衰减一次**(用户 2026-10-18 裁定):13 在终局锁存期**每一拍**都给 `End`,
                    //    故这里必须同时挡住「衰减中」与「已衰减完」两种 —— 只在两者都没有时开新段。
                    //    否则到期删除后的下一拍就会重开满幅新段 ⇒ 一阵一阵(见字段区注释)。
                    if (!_tailStartTick.ContainsKey(patientId) && !_tailDone.ContainsKey(patientId))
                        _tailStartTick[patientId] = tick;
                    break;

                case BreathAction.None:
                    // 稳态维持:13 判呼吸层应活 + 已活 ⇒ 无动作。
                    // ⚠️ 尾音簿记若还挂着(未走完衰减),继续停表推进,由 ⑤ 推进 —— 本分支不碰它。
                    break;
            }
        }

        /// <summary>推进尾音衰减一步(AC-019-5):把本拍的增益写进 sink,并**不**发一次性 cue。</summary>
        private void AdvanceBreathTail(int patientId, PatientId id, float position,
                                       in WorldPos cell, long tick, long tailStart)
        {
            // ⚠️ 到位判定 = **整数 tick 数**(`tailTicks`),不按浮点秒比较 ——
            //    1.5 / 0.05 的末位误差会让停表差一拍。浮点秒只用于**取增益值**(并被钳制)。
            double elapsedSeconds = Math.Min((tick - tailStart) * TickSeconds, AudioTuning.BreathTailRampSeconds);
            if (elapsedSeconds < 0.0) elapsedSeconds = 0.0;

            // 到位:只封口(终点增益)并停表(`PausedBreathCount` 归零),但 loop 仍存续(停顿 ≠ 停止)。
            // ⚠️ 停表后病人进 `_tailDone`(**尾音只衰减一次**)⇒ 13 后续每拍的 `End` 不再重开新段。
            //    封口增益恰为终点值(带恒等式 `BreathTailRamp.ValueAt(1.0, floor, RAMP, RAMP) = floor`),
            //    故此后的「不再写」与「每拍重写同值」在 sink 侧等价 —— 只是不再每拍刷一次 DTO。
            int tailTicks = (int)Math.Round(AudioTuning.BreathTailRampSeconds / TickSeconds);
            bool finished = tick - tailStart >= tailTicks;   // 整数域比较,无末位误差

            double gain = TailGain(patientId, position, elapsedSeconds);
            AudioCueDto cue = MakeBreathCue(patientId, position, cell, GainByte(gain));
            _sink.Emit(cue);           // looped: true ⇒ 44 按 Source 路由到在响循环的增益(W-1)
            BreathGainUpdateCount++;   // ← 本段每拍一个增益步(= 段内步数;全生只一段 ⇒ = 尾音步数)

            if (finished)
            {
                _tailStartTick.Remove(patientId);        // 停表(loop 仍存续,直至停止形态)
                _tailDone[patientId] = gain;             // 封口(全精度 double):此后不再重开、不再写
            }
        }

        /// <summary>尾音本拍增益(线性 volume;`BreathTailRamp` = 曲线唯一出处)。
        /// <para>⚠️ **起点 = loop 实际增益**(W-1 第二处 2026-10-20):不是写死的 1.0 ——
        /// 稳态 loop 的强度是 <see cref="PatientCue.Intensity"/> / 255;起点若为 1.0,停顿首拍
        /// 增益**向上跳**,那是可听辨的「换了个声音」拐点,违 GDD :571-573。</para></summary>
        private double TailGain(int patientId, float position, double elapsedSeconds)
        {
            const double Full = 255.0;
            double floor = BreathTailRamp.DbToLinear(AudioTuning.BreathTailGainFloorDb);
            // 起点优先级:① 44 侧既有 loop 的**实际增益读数**(sink 读面,最忠实);② 无读面(测试替身)
            // ⇒ 本拍体征折算;③ 尚无读数(首拍早于任何读数)⇒ 本拍体征折算。**不写死 1.0**。
            double loopGain = _sink is IAudioCueSinkLoopGainRead read
                              && read.TryGetLoopGain(patientId, out int known) && known > 0
                ? known / Full
                : PatientCue.Intensity(position) / Full;
            double start = loopGain <= floor ? floor          // 稳态已低于底值 ⇒ 无衰减(setter 退化分支)
                : loopGain >= 1.0 ? 1.0 : loopGain;           // `PatientCue.Intensity` 上界 255 ⇒ 钳到 1
            return BreathTailRamp.ValueAt(start, floor, elapsedSeconds, AudioTuning.BreathTailRampSeconds);
        }

        /// <summary>线性增益 → <c>AudioCueDto.Intensity</c>(0..255)的**唯一**换算(W-1 接线轮)。
        /// <para>⚠️ 8-bit 空间本就在 DTO 契约里(<c>Intensity: byte</c>,ADR-018 §二;本轮**不改契约**)
        /// ⇒ 量化代价只在**每拍写入**上;封口簿记不再经它(W-2 修法:存 double)。
        /// −36 dB 底值映射为 4/255(≈ −36.1 dB,误差 &lt; 0.6 dB)。</para></summary>
        private static byte GainByte(double gain)
        {
            if (double.IsNaN(gain) || gain <= 0.0) return 0;
            if (gain >= 1.0) return 255;
            return (byte)Math.Round(gain * 255.0);
        }

        /// <summary>呼吸层 cue 的**唯一**构造点(稳态 / 尾音共用;强度由调用方给)。</summary>
        private AudioCueDto MakeBreathCue(int patientId, float position, in WorldPos cell, byte intensity)
        {
            return new AudioCueDto(
                cue: _breathCue.Value,
                intensity: intensity,
                cell: new Int3(cell.X, cell.Y, cell.Z),
                source: patientId,
                looped: true);
        }
    }
}
