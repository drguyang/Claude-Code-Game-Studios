// 过场演出播放器(N3 · 2026-11-14)。
//
// 权威:
//   `design/levels/prologue-beat-script.md` §1 N3(「播放器:冻结输入(`IInputFreeze` 已有)
//       + 时间线(逐件出现)+ 相机运镜 + 结束回调」)
//   同文件 §2 相机行(「过场期间用独立机位;**不得**用 `CameraMode` 的五档」)
//
// ═══ 本类做什么 / 不做什么 ════════════════════════════════════════════════
//   **做**:把一段演出拆成**按 tick 计时**的一串拍,逐拍进出;播完回调。
//         这是「时间线」那一半。
//   **不做相机** —— 承 §2:过场用**独立机位**,而 `ICameraRig` 的五档是玩法档位
//         (Explore / Casebook / Treatment / Downed),拿它演过场就是让玩法档位承载叙事。
//         过场机位是另一件东西(运镜曲线 + 起止姿态),本类不发明它。
//   **不做冻结** —— 冻结的唯一判定点已存在(`ModalInputFreeze`),本类只暴露 `IsPlaying`,
//         由装配层把它并进那个谓词。**两处各写一份"冻结中"= 第二真源**。
//
// ═══ 为什么按 tick 而不是按秒 ════════════════════════════════════════════
//   全部节拍的时长以 tick 计(`prologue-beat-script` 与 5 的日长同量纲),
//   按墙钟走会让「同一段演出在两台机器上长短不一」,而它要与字幕停留、`EventArrived`
//   的时间戳对齐。墙钟只归呈现层的补间(`FadeInOutDriver`),不归时序。
//
// ⚠️ 与 `PrologueBeatDriver` 的分工:节拍驱动管「**该不该发生**」(条件 + 一次性),
//   本类管「**发生之后怎么演**」(时序 + 结束)。两者都不写三流。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>演出里的一拍:持续 <see cref="DurationTicks"/> 个 tick。</summary>
    public readonly struct CutsceneBeat
    {
        /// <summary>拍 id(非空;走查与夹具按它定位)。</summary>
        public readonly string Id;

        /// <summary>持续 tick 数(**须 &gt; 0**;0 会让这一拍在同一 tick 里进出)。</summary>
        public readonly int DurationTicks;

        /// <summary>进入时的动作(`null` = 纯计时拍)。</summary>
        public readonly Action OnEnter;

        /// <summary>离开时的动作(`null` 表 = 无事;`Abort` 也会调它,保证成对)。</summary>
        public readonly Action OnExit;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentException">id 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">时长 ≤ 0。</exception>
        public CutsceneBeat(string id, int durationTicks, Action onEnter = null, Action onExit = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("演出拍 id 不得为空", nameof(id));
            if (durationTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationTicks), durationTicks,
                    "演出拍时长须 > 0 tick —— 0 会让它在同一 tick 里进出,等于不存在");
            Id = id;
            DurationTicks = durationTicks;
            OnEnter = onEnter;
            OnExit = onExit;
        }
    }

    /// <summary>按 tick 推进的一串演出拍。</summary>
    public sealed class CutscenePlayer
    {
        private IReadOnlyList<CutsceneBeat> _beats;
        private Action _onComplete;
        private int _index;
        private long _beatEndTick;
        private long _lastTick = -1;

        /// <summary>是否在播。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>当前拍的 id(`null` = 没在播)。</summary>
        public string CurrentBeatId => IsPlaying && _beats != null ? _beats[_index].Id : null;

        /// <summary>当前拍下标(0 起;没在播 = -1)。</summary>
        public int BeatIndex => IsPlaying ? _index : -1;

        /// <summary>当前拍到达的总拍数(诊断)。</summary>
        public int BeatCount => _beats != null ? _beats.Count : 0;

        /// <summary>开播。</summary>
        /// <param name="beats">拍序列(≥ 1 拍)。</param>
        /// <param name="startTick">起始 tick(第一拍从此刻开始计时)。</param>
        /// <param name="onComplete">播完回调(`null` = 不通知)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="beats"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="beats"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startTick"/> 为负。</exception>
        /// <exception cref="InvalidOperationException">**已经在播** —— 重叠演出须是一次显式决定,
        /// 不是静默替换(替换会让前一段的 `OnExit` 永不执行,世界停在半演状态)。</exception>
        public void Play(IReadOnlyList<CutsceneBeat> beats, long startTick, Action onComplete = null)
        {
            if (beats == null) throw new ArgumentNullException(nameof(beats));
            if (beats.Count == 0)
                throw new ArgumentException("演出至少要有 1 拍 —— 0 拍的「演出」等于什么都没发生", nameof(beats));
            if (startTick < 0)
                throw new ArgumentOutOfRangeException(nameof(startTick), startTick, "起始 tick 不得为负");
            if (IsPlaying)
                throw new InvalidOperationException(
                    "已经有一段演出在播(" + CurrentBeatId + ")—— 重叠须先 Abort,不得静默替换");

            _beats = beats;
            _onComplete = onComplete;
            _index = 0;
            _lastTick = startTick;
            IsPlaying = true;
            _beatEndTick = startTick + beats[0].DurationTicks;
            beats[0].OnEnter?.Invoke();
        }

        /// <summary>中断(调当前拍的 `OnExit`,使进出成对)。</summary>
        public void Abort()
        {
            if (!IsPlaying) return;
            _beats[_index].OnExit?.Invoke();
            IsPlaying = false;
            _index = 0;
            _beats = null;
            _onComplete = null;
        }

        /// <summary>
        /// 推进一个 tick。
        /// </summary>
        /// <param name="tick">当前 tick(同 tick 重复调用直接返回;tick 变小 = 读档 ⇒ 中断)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        /// <remarks>
        /// ⚠️ **tick 变小时中断而不是抛**:本类不持权威态(见文件头),读档把 tick 拨回去是
        /// 合法动作,而一段演出跨过读档没有意义 —— 继续播会让「搭建演出」在别人世界里重演。
        /// 与 `RegionTriggerDriver` / `PrologueBeatDriver` 同一口径。
        /// </remarks>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (!IsPlaying) return;
            if (tick == _lastTick) return;

            if (tick < _lastTick) { Abort(); return; }   // 读档
            _lastTick = tick;

            // 一拍可能不止跨过一个 tick 边沿(低帧率),故用 while 而不是 if。
            while (IsPlaying && tick >= _beatEndTick)
            {
                _beats[_index].OnExit?.Invoke();
                _index++;
                if (_index >= _beats.Count)
                {
                    Action done = _onComplete;
                    IsPlaying = false;
                    _index = 0;
                    _beats = null;
                    _onComplete = null;
                    done?.Invoke();
                    return;
                }
                // ⚠️ **按上一拍的终点累加,不是按当前 tick 重算**:
                //    `_beatEndTick = tick + 时长` 会让低帧率把整段演出**拉长**
                //    (一次跨过三拍时,后两拍各多等一整个时长)。演出总时长必须与帧率无关。
                _beatEndTick += _beats[_index].DurationTicks;
                _beats[_index].OnEnter?.Invoke();
            }
        }
    }
}
