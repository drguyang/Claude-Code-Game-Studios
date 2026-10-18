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

        // 每病人的发射 / 呼吸层簿记(呈现层事实,不落盘)。
        private readonly Dictionary<int, int> _lastEmitTick = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _entryTick = new Dictionary<int, int>();
        private readonly Dictionary<int, AudioCueHandle> _breathHandles = new Dictionary<int, AudioCueHandle>();

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

        /// <summary>呼吸层当前存活的句柄数。</summary>
        public int ActiveBreathCount => _breathHandles.Count;

        /// <summary>tick 边沿驱动 —— 对每个在场病人跑一次调度。</summary>
        /// <param name="tick">本边沿自己的逻辑 tick(由帧泵回推,非末 tick)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");

            LastDrivenTick = tick;

            // 在场集快照(登记簿的 PresentPatientIds 已返回数组,遍历安全)。
            foreach (int patientId in _presence.PresentPatientIds())
            {
                var id = new PatientId(patientId);
                if (!_presence.TryGetCell(id, out WorldPos cell)) continue;   // 刚离场 ⇒ 跳过
                // 已登记在场但**尚未建档**(DiseaseOnset 要下一拍才被应用)⇒ 跳过本拍。
                // ⚠️ 不是「静默失败」:下一拍就自然进入(见构造参数 hasCourse 的说明)。
                if (_hasCourse != null && !_hasCourse(id)) continue;

                // ① 13 的行为态(读一次 IVitalsQuery —— 唯一取数入口)。
                _behavior.Step(id, knowsClinic: false);
                PatientBehavior behavior = _behavior.For(id);
                VitalsDto v = _vitals.GetVitals(id);

                // ② 入场 tick 的纪元(首拍相位相对入场时刻,F-13.5 ③)。
                if (!_entryTick.TryGetValue(patientId, out int entry))
                {
                    entry = (int)tick;
                    _entryTick[patientId] = entry;
                }
                if (!_lastEmitTick.TryGetValue(patientId, out int lastEmit))
                {
                    lastEmit = -1;
                    _lastEmitTick[patientId] = lastEmit;
                }

                // ③ 13 的调度决策(纯函数;本类不改它的输入)。
                CueDispatch dispatch = PatientCueSchedule.Decide(
                    behavior.Current, behavior.Terminal, (int)tick, lastEmit, id,
                    behavior.Tier, v.Position, _intervals,
                    breathAlive: _breathHandles.ContainsKey(patientId),
                    kind: CueKind.Cough,             // P0 固定咳嗽(见文件头注)
                    entryTick: entry);

                // ④ 呼吸层生命周期(句柄归本类,ADR-018 §一)。
                ApplyBreath(patientId, dispatch.Breath, v.Position, cell);

                // ⑤ 一次性 cue(经 13 → 44 的唯一发射桥)。
                // ⚠️ `coughCue` 为 null ⇒ 不发射(装配期未解析到事件表 / 测试只测调度)。
                //    静默跳过而非抛:发射面缺一条 cue 不该炸掉整帧(见 AudioClipLibrary 同口径)。
                //    ⚠️ 但 `_lastEmitTick` **仍推进** —— 否则调度器每 tick 都判 due,
                //    一旦装配补齐就会瞬间补发一整批(相位语义被打乱)。
                if (dispatch.EmitOneShot)
                {
                    if (_coughCue.HasValue)
                    {
                        PatientCueEmit.Emit(_sink, patientId, _coughCue.Value, v.Position,
                                            cell.X, cell.Y, cell.Z, looped: false);
                        EmitCount++;
                    }
                    _lastEmitTick[patientId] = (int)tick;
                }
            }
        }

        /// <summary>在场集收缩后清理簿记(离场 / 死亡病人的句柄不得残留)。</summary>
        /// <param name="tick">当前 tick(仅用于日志语义,不参与判定)。</param>
        public void PruneAbsent(long tick)
        {
            var stale = new List<int>();
            foreach (KeyValuePair<int, AudioCueHandle> kv in _breathHandles)
                if (!_presence.IsPresent(new PatientId(kv.Key))) stale.Add(kv.Key);

            for (int i = 0; i < stale.Count; i++)
            {
                int patientId = stale[i];
                _sink.EndLoop(_breathHandles[patientId]);
                _breathHandles.Remove(patientId);
                _entryTick.Remove(patientId);
                _lastEmitTick.Remove(patientId);
            }
        }

        /// <summary>全部呼吸层收尾(场景卸载 / 读档前)。</summary>
        public void StopAll()
        {
            foreach (KeyValuePair<int, AudioCueHandle> kv in _breathHandles)
                _sink.EndLoop(kv.Value);
            _breathHandles.Clear();
            _entryTick.Clear();
            _lastEmitTick.Clear();
        }

        private void ApplyBreath(int patientId, BreathAction action, float position, in WorldPos cell)
        {
            switch (action)
            {
                case BreathAction.Begin:
                    // ⚠️ `breathCue` 为 null ⇒ 不起循环(装配期未解析到 `BreathLayer_Base_Calm`)。
                    //    绝不用咳嗽 ordinal 顶替 —— 那会让呼吸层播成咳嗽(素材语义错)。
                    if (!_breathCue.HasValue) break;
                    var cue = new AudioCueDto(
                        cue: _breathCue.Value,
                        intensity: PatientCue.Intensity(position),
                        cell: new Int3(cell.X, cell.Y, cell.Z),
                        source: patientId,
                        looped: true);
                    _breathHandles[patientId] = _sink.BeginLoop(cue);
                    break;

                case BreathAction.End:
                    if (_breathHandles.TryGetValue(patientId, out AudioCueHandle handle))
                    {
                        _sink.EndLoop(handle);
                        _breathHandles.Remove(patientId);
                    }
                    break;
            }
        }
    }
}
