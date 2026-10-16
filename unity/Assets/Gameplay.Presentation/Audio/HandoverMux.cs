// 权威来源:production/epics/audio-system/story-009-source-lifecycle-handover.md
//   (AC-009-2 贴耳交接契约 —— AC 源:GDD §Edge Cases 交接条 / F-44.7)
// GDD:design/gdd/audio-system.md F-44.7 :456-462(2026-09-25 二轮 F7/G3 贴耳交接)
//   · :459 「进入听诊 ⇒ 世界语境层在**交接窗**(旋钮 `HANDOVER_MS`)内压出、听诊层同窗淡入
//     (两层并存 ≤ 交接窗,**禁同刻双呼吸稳态**);退出反向」
//   · :460-462 世界语境层声源与句柄归 44(ADR-028 ⑤ 生命周期 = cue 驱动,44 持有池)
//     ⇒ 交接停止**不违**「上游唯一收尾」(该规则管**上游发起**的 cue;交接是 44 对
//     **自己持有**的表现层声源的本地操作)
// ADR-018 §一(44 只触发 / 只渲染)· §三(快照只管电平 —— 本类**不碰快照**,F7=甲)
//   · ADR-028 ⑤(声源池生命周期 = cue 驱动)· ADR-001 §一之三 裁决二(零网络依赖)
//
// ⚠️ **本类是 S14-3 的唯一新生产件之一**(评审原件 §4.2 的 `HandoverMux` / 交接 FSM)。
//    补评前该位置是测试自造的 `FakeHandoverFsm`(评审原件 §2.3 判 🔴 ×5)。
// ⚠️ **时钟源 = sim tick(参入),不是墙钟** —— 与 `EndLoopWatchdog` / AC-44-D7 ② 同纪律,
//    由 `AssemblyGates.CheckAudioClockTokens` 的 IL 扫描硬约束。
// ⚠️ **本类不碰快照**(F7=甲):交接窗内的增益交叉 = 本类的**音量输出**;
//    bus 路由 / 贴耳抬升归脚本动作(Story 003 `SnapshotDirector`),两者刻意分离 ——
//    快照会插值它存储的**全部**参数,把脚本控制静默覆盖(GDD :193-197)。
// ⚠️ **不持游戏状态**(ADR-018 §一):持的是「哪一层正在响」的呈现层事实。
// ⚠️ **两段式装配同型**:本类**只算**两个层的目标音量(`WorldVolume` / `StethoscopeVolume`),
//    实际写入 transport 归装配层 —— 与 `CueEmitDeriver`(只算 `Cell=default`)同纪律。
//    句柄的 Begin / End 则**由本类执行**(AC-009-2 逐字「世界层停止由 44 对自有句柄执行」)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>贴耳交接的四相(两态 + 两个过渡窗)。</summary>
    public enum HandoverPhase
    {
        /// <summary>只有世界语境层响(未贴耳)。</summary>
        WorldOnly,
        /// <summary>窗内:世界层压出 · 听诊层淡入。</summary>
        HandingToStethoscope,
        /// <summary>只有听诊层响(已贴耳)。</summary>
        StethoscopeOnly,
        /// <summary>窗内:听诊层压出 · 世界层淡入(退出反向)。</summary>
        HandingToWorld,
    }

    /// <summary>贴耳交接状态机(AC-009-2 的生产实现)。
    /// <para>职责:① 两态 + 两过渡窗的相位机;② 窗内**线性**交叉淡化(以 sim tick 计);
    /// ③ 窗满时对**自有句柄**执行 `EndLoop`(ADR-028 ⑤);④ 幂等重入 / 听诊优先。</para>
    /// <para>⚠️ **禁硬切**:`handoverTicks ≤ 0` ⇒ 构造期抛异常(ramp 下界同 50 ms 族;
    /// GDD :777 `HANDOVER_MS` 安全范围 ≥ ramp 下界)。</para>
    /// <para>⚠️ **两层并存 ≤ 交接窗**:并存只发生在 `t ∈ (0,1)` 的开区间内;
    /// `t = 1` 那一刻世界层音量归零且句柄已释放 ⇒ **禁同刻双呼吸稳态**。</para>
    /// <example>
    /// var mux = new HandoverMux(sink, AudioTuning.HandoverTicks);
    /// mux.EnterWorldLayer(worldBreathCue, tick);
    /// mux.EnterStethoscope(stethoscopeCue, tick);   // 玩家贴耳
    /// mux.Tick(tick);                                // 每 tick 由 ITickProvider 驱动
    /// </example></summary>
    public sealed class HandoverMux
    {
        private readonly IAudioCueSink _sink;
        private readonly long _handoverTicks;
        private readonly StethoscopePreloadGate _preloadGate;

        private AudioCueDto _worldCue;
        private bool _hasWorldCue;

        private AudioCueHandle _worldHandle;
        private AudioCueHandle _stethoscopeHandle;
        private bool _hasWorldHandle;
        private bool _hasStethoscopeHandle;

        private long _phaseStartTick;

        /// <summary>构造交接状态机。</summary>
        /// <param name="sink">句柄操作面(44 对上游的唯一界面;null ⇒
        /// <see cref="ArgumentNullException"/>)。</param>
        /// <param name="handoverTicks">交接窗长度(tick;`≤ 0` ⇒
        /// <see cref="ArgumentException"/> —— 硬切被结构性拒绝)。</param>
        /// <param name="preloadGate">听诊窗预载门(**可选**;null = 不设门 = 旧行为)。
        /// <para>⚠️ 2026-10-16 S14-6(story-010)追加:AC「听诊窗预载」要求
        /// 「进入听诊前听诊族 cue `loadState == Loaded`(未就绪不进听诊)」——
        /// **贴耳的真实入口就是本类**,故门挂在这里,不是挂在快照切换上
        /// (快照只压 Ambience/Music 电平,与素材是否载入无关)。</para></param>
        /// <exception cref="ArgumentNullException">sink 为 null。</exception>
        /// <exception cref="ArgumentException">handoverTicks ≤ 0(硬切)。</exception>
        public HandoverMux(IAudioCueSink sink, long handoverTicks,
            StethoscopePreloadGate preloadGate = null)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            if (handoverTicks <= 0)
            {
                throw new ArgumentException(
                    "交接窗必须 > 0 —— 硬切(ramp = 0)违 GDD :777「HANDOVER_MS ≥ ramp 下界 50 ms」" +
                    "与 :562「禁『啪』一声切换」(爆音 / 相位抵消)", nameof(handoverTicks));
            }
            _handoverTicks = handoverTicks;
            _preloadGate = preloadGate;

            Phase = HandoverPhase.WorldOnly;
            WorldVolume = 1.0;
            StethoscopeVolume = 0.0;
        }

        /// <summary>当前相位。</summary>
        public HandoverPhase Phase { get; private set; }

        /// <summary>世界语境层的目标音量(0–1;装配层读它写 transport)。</summary>
        public double WorldVolume { get; private set; }

        /// <summary>听诊层的目标音量(0–1;装配层读它写 transport)。</summary>
        public double StethoscopeVolume { get; private set; }

        /// <summary>交接次数(**单调递增**;幂等重入不计 —— 「窗内重复触发只交接一次」的观测面)。</summary>
        public int HandoverCount { get; private set; }

        /// <summary>世界层句柄是否已由 44 自行释放(AC-009-2 的断言面)。</summary>
        public bool WorldHandleReleased { get; private set; }

        /// <summary>听诊层句柄是否已由 44 自行释放(退出反向的断言面)。</summary>
        public bool StethoscopeHandleReleased { get; private set; }

        /// <summary>世界层起循环(未贴耳时的常态入口)。
        /// <para>⚠️ **听诊优先**:已贴耳 / 正在贴耳时新到的世界 cue **不重启**世界层(AC-009-2);
        /// 但 cue 本身记下,供退出听诊时重建。</para></summary>
        /// <param name="worldCue">世界语境呼吸 cue。</param>
        /// <param name="currentTick">当前 sim tick(时钟源 = `ITickProvider`,禁墙钟)。</param>
        public void EnterWorldLayer(in AudioCueDto worldCue, long currentTick)
        {
            _worldCue = worldCue;
            _hasWorldCue = true;

            // 听诊优先:听诊中世界 cue 新到 ⇒ 不重启世界层(AC-009-2)
            if (Phase == HandoverPhase.StethoscopeOnly || Phase == HandoverPhase.HandingToStethoscope)
                return;

            _worldHandle = _sink.BeginLoop(worldCue);
            _hasWorldHandle = true;
            WorldHandleReleased = false;

            Phase = HandoverPhase.WorldOnly;
            WorldVolume = 1.0;
            StethoscopeVolume = 0.0;
            _phaseStartTick = currentTick;
        }

        /// <summary>玩家贴耳(玩家自因入口)。窗内重复触发 ⇒ **幂等**(不重复交接)。</summary>
        /// <param name="stethoscopeCue">听诊层 cue。</param>
        /// <param name="currentTick">当前 sim tick。</param>
        public void EnterStethoscope(in AudioCueDto stethoscopeCue, long currentTick)
        {
            // 2026-10-16 S14-6:门挂在 TryEnterStethoscope 内(本方法保留 = 旧调用方零改动)。
            TryEnterStethoscope(stethoscopeCue, currentTick);
        }

        /// <summary>玩家贴耳(**带结果**;AC「听诊窗预载」的判据面)。
        /// <para>返回 false = 听诊族 cue 未全部载入 ⇒ **不进听诊**(相位不变、句柄不建、
        /// 计数不增)。调用方据此保持世界层 / 提示载入中。</para>
        /// <para>幂等:已在贴耳 / 正在贴耳 ⇒ 返回 true 且不重复交接(AC-009-2 边条)。</para></summary>
        /// <param name="stethoscopeCue">听诊层 cue。</param>
        /// <param name="currentTick">当前 sim tick。</param>
        /// <returns>true = 已进入(或本就已进入);false = 预载未就绪,未进入。</returns>
        public bool TryEnterStethoscope(in AudioCueDto stethoscopeCue, long currentTick)
        {
            // 幂等:已在贴耳 / 正在贴耳 ⇒ 不重复交接(AC-009-2 边条)
            if (Phase == HandoverPhase.HandingToStethoscope || Phase == HandoverPhase.StethoscopeOnly)
                return true;

            // 听诊窗预载:门未就绪 ⇒ 不进听诊(GDD :640「不得在听诊中因加载迟发」)
            if (_preloadGate != null && !_preloadGate.IsReady())
                return false;

            _stethoscopeHandle = _sink.BeginLoop(stethoscopeCue);
            _hasStethoscopeHandle = true;
            StethoscopeHandleReleased = false;

            Phase = HandoverPhase.HandingToStethoscope;
            _phaseStartTick = currentTick;
            HandoverCount++;
            return true;
        }

        /// <summary>听诊窗预载门(null = 未设门 —— 预载未就绪不阻断)。</summary>
        public StethoscopePreloadGate PreloadGate => _preloadGate;

        /// <summary>玩家摘耳(退出反向)。已在退出 / 未贴耳 ⇒ 幂等返回。</summary>
        /// <param name="currentTick">当前 sim tick。</param>
        public void ExitStethoscope(long currentTick)
        {
            if (Phase == HandoverPhase.HandingToWorld || Phase == HandoverPhase.WorldOnly)
                return;

            // 退出 ⇒ 世界层重建(句柄窗末已释放;世界层生命周期 = cue 驱动,ADR-028 ⑤)
            if (_hasWorldCue)
            {
                _worldHandle = _sink.BeginLoop(_worldCue);
                _hasWorldHandle = true;
                WorldHandleReleased = false;
            }

            Phase = HandoverPhase.HandingToWorld;
            _phaseStartTick = currentTick;
        }

        /// <summary>推进一帧(**由 `ITickProvider` 驱动;禁墙钟**)。
        /// <para>窗内按 `t = clamp(elapsed / handoverTicks, 0, 1)` 线性交叉;`t = 1` 时
        /// 释放交出方的句柄并落到稳态相位。</para></summary>
        /// <param name="currentTick">当前 sim tick。</param>
        /// <returns>本 tick 完成的交接数(0 = 仍在窗内 / 不在过渡相)。</returns>
        public int Tick(long currentTick)
        {
            if (Phase != HandoverPhase.HandingToStethoscope && Phase != HandoverPhase.HandingToWorld)
                return 0;

            long elapsed = currentTick - _phaseStartTick;
            if (elapsed < 0) elapsed = 0;   // 时间倒流 ⇒ 视作窗首(不推进,不误判完成)

            double t = (double)elapsed / _handoverTicks;
            if (t > 1.0) t = 1.0;

            if (Phase == HandoverPhase.HandingToStethoscope)
            {
                WorldVolume = 1.0 - t;
                StethoscopeVolume = t;
                if (t >= 1.0) return CompleteHandoverToStethoscope();
            }
            else
            {
                StethoscopeVolume = 1.0 - t;
                WorldVolume = t;
                if (t >= 1.0) return CompleteHandoverToWorld();
            }
            return 0;
        }

        private int CompleteHandoverToStethoscope()
        {
            WorldVolume = 0.0;
            StethoscopeVolume = 1.0;

            // AC-009-2 逐字:「世界层停止由 44 对**自有句柄**执行」(ADR-028 ⑤)
            if (_hasWorldHandle)
            {
                _sink.EndLoop(_worldHandle);
                _hasWorldHandle = false;
                WorldHandleReleased = true;
            }
            Phase = HandoverPhase.StethoscopeOnly;
            return 1;
        }

        private int CompleteHandoverToWorld()
        {
            StethoscopeVolume = 0.0;
            WorldVolume = 1.0;

            if (_hasStethoscopeHandle)
            {
                _sink.EndLoop(_stethoscopeHandle);
                _hasStethoscopeHandle = false;
                StethoscopeHandleReleased = true;
            }
            Phase = HandoverPhase.WorldOnly;
            return 1;
        }
    }
}
