// 权威来源:production/epics/audio-system/story-009-source-lifecycle-handover.md
//   (AC-009-1 EndLoop 自评兜底 —— AC 源:GDD §Edge Cases「EndLoop 永不到达」条 + §States ②)
// GDD:design/gdd/audio-system.md §States :206-222(播放句柄层 · EndLoop 可能永不到达)
//   · :219-222 「**P0 = 自评兜底(架构义务,不得留空)** —— 从最新快照 `Progress` 自行求值
//     是否该结束,零网络依赖;…**不构成对自评兜底的免除**」
//   · §Edge Cases :570-573 「循环 cue **必须自评兜底** … **不得**出现『呼吸层永不停止』」
// ADR-001 §一之三 裁决二(呈现侧循环终止必须自评兜底,升格**架构义务**;`EndLoop` 不作网络消息)
//   · ADR-028 ⑤(44 持有声源池生命周期 ⇒ 44 对**自有句柄**执行 EndLoop)
//   · ADR-018 §一(44 只触发 / 只渲染)
//
// ⚠️ **本类补的是 S14-2 留下的执行面缺口** —— S14-2 落了 `EndLoopWatchdog`(纯谓词
//    `ShouldSelfTerminate` / `IsStagnant`,零状态),但「每 tick 遍历活跃循环 → 判停 →
//    自行 `EndLoop`」的**驱动器**当时无生产落点。评审原件 §4.2 的「EndLoop 自评看门狗」
//    一行,读全 = 谓词(**S14-2 已交**)+ 驱动器(**本类**)。
// ⚠️ **判据不重复**:本类**不重写**判停逻辑,一律调 `EndLoopWatchdog.ShouldSelfTerminate`
//    (单一出处 —— 承 ADR-024 §⑥ 纪律:同一条判据两处实现 = 两处分叉)。
// ⚠️ **时钟源 = sim tick(参入),不是墙钟** —— 与 `EndLoopWatchdog` / AC-44-D7 ② 同纪律。
// ⚠️ **不持游戏状态**(ADR-018 §一):持的是「哪些循环正在响 + 它们最新快照」的呈现层事实。
//    ⚠️ 快照 `Progress` 是**上游推来**的只读值(本地流副本事实),不是本类自造的判据 ——
//    这正是 ADR-001 裁决二「从最新快照 Progress 自求值」的字面兑现。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>循环自评兜底的**执行驱动器**(AC-009-1 的生产实现)。
    /// <para>职责:① 登记活跃循环句柄;② 接收上游推来的最新快照 `Progress`;
    /// ③ 每 tick 调 <see cref="EndLoopWatchdog.ShouldSelfTerminate"/> 判停,
    /// 命中则对**自有句柄**执行 `EndLoop`(ADR-028 ⑤)。</para>
    /// <para>⚠️ **零网络依赖**(ADR-001 §一之三 裁决二):本类不订阅任何网络事件 ——
    /// 上游 `EndLoop` 到不达都与本类无关,这正是「自评兜底」的字面含义。</para>
    /// <para>⚠️ **停一次即注销**:句柄停后从活跃表移除 ⇒ 幂等(重复 tick 不会重复停)。</para>
    /// <example>
    /// var driver = new LoopSelfTerminationDriver(sink, AudioTuning.LoopEvalMaxTicks);
    /// driver.BeginLoop(handle, cueId);
    /// driver.UpdateSnapshot(cueId, progress, serverTick);   // 上游推来的最新快照
    /// int stopped = driver.Tick(tickProvider.CurrentTick);  // 每 tick
    /// </example></summary>
    public sealed class LoopSelfTerminationDriver
    {
        private readonly IAudioCueSink _sink;
        private readonly int _maxStagnantTicks;

        // 活跃循环:句柄 id → 记录。句柄 id 是唯一键(同源重复 BeginLoop 的旧句柄由
        // CueRenderSink 自行释放;驱动器只登记它收到的那些)。
        private readonly Dictionary<int, LoopRecord> _active = new Dictionary<int, LoopRecord>();

        // 句柄 id → 该循环的最新快照(上游推来;从未推过 ⇒ 不进判停面)。
        private readonly Dictionary<int, LoopProgress> _snapshots = new Dictionary<int, LoopProgress>();

        private readonly List<int> _toStop = new List<int>();

        private readonly struct LoopRecord
        {
            public readonly AudioCueHandle Handle;
            public readonly int CueId;
            public LoopRecord(AudioCueHandle handle, int cueId) { Handle = handle; CueId = cueId; }
        }

        /// <summary>构造驱动器。</summary>
        /// <param name="sink">句柄操作面(null ⇒ <see cref="ArgumentNullException"/>)。</param>
        /// <param name="maxStagnantTicks">停滞阈值(tick;透传
        /// <see cref="AudioTuning.LoopEvalMaxTicks"/>;`&gt; 0` 才有界 —— 非正值的
        /// 行为由 <see cref="EndLoopWatchdog"/> 定义,本类不另立判据)。</param>
        /// <exception cref="ArgumentNullException">sink 为 null。</exception>
        public LoopSelfTerminationDriver(IAudioCueSink sink, int maxStagnantTicks)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _maxStagnantTicks = maxStagnantTicks;
        }

        /// <summary>当前活跃循环数(测试与调试读)。</summary>
        public int ActiveLoopCount => _active.Count;

        /// <summary>累计自评停掉的循环数(幂等断言面)。</summary>
        public int SelfTerminatedCount { get; private set; }

        /// <summary>登记一个由 44 持有的循环句柄(开始跟踪它的停滞)。</summary>
        /// <param name="handle">`BeginLoop` 返回的本地句柄。</param>
        /// <param name="cueId">循环 cue 的语义 id(快照关联键)。</param>
        public void BeginLoop(in AudioCueHandle handle, int cueId)
        {
            _active[handle.Id] = new LoopRecord(handle, cueId);
        }

        /// <summary>注销一个循环句柄(**上游正常 `EndLoop` 时调用**)—— 自评兜底只处理
        /// 「收尾信号没来」的情形,正常收尾走本方法退出跟踪,不重复停。</summary>
        /// <param name="handle">待注销的本地句柄。</param>
        public void EndLoop(in AudioCueHandle handle)
        {
            _active.Remove(handle.Id);
        }

        /// <summary>接收上游推来的最新快照(**只读事实** —— ADR-001 裁决二的字面输入)。
        /// <para>⚠️ 非循环 cue(`loopingCue == 0`)不进兜底面(与
        /// <see cref="LoopProgress"/> 的哨兵口径一致,判据归 `EndLoopWatchdog`)。</para></summary>
        /// <param name="cueId">循环 cue 的语义 id。</param>
        /// <param name="progress">进度值(主机快照;`≤ 0` = 无进度可判)。</param>
        /// <param name="serverTick">快照的主机 tick 序号(停滞基准)。</param>
        public void UpdateSnapshot(int cueId, int progress, uint serverTick)
        {
            foreach (KeyValuePair<int, LoopRecord> kv in _active)
            {
                if (kv.Value.CueId != cueId) continue;
                _snapshots[kv.Key] = new LoopProgress(cueId, progress, serverTick);
            }
        }

        /// <summary>推进一 tick:对每个活跃循环求值判停,命中即自行 `EndLoop`(**由
        /// `ITickProvider` 驱动;禁墙钟**)。
        /// <para>⚠️ 判据一律走 <see cref="EndLoopWatchdog.ShouldSelfTerminate"/> ——
        /// 本类**不复制**停滞比较(单一出处)。</para></summary>
        /// <param name="currentTick">当前 sim tick。</param>
        /// <returns>本 tick 自评停掉的循环数。</returns>
        public int Tick(long currentTick)
        {
            if (_active.Count == 0) return 0;

            _toStop.Clear();
            foreach (KeyValuePair<int, LoopRecord> kv in _active)
            {
                if (!_snapshots.TryGetValue(kv.Key, out LoopProgress snap))
                    continue;   // 从未收到快照 ⇒ 无从判(与「无 Progress 快照 ⇒ 不判」同口径)

                if (EndLoopWatchdog.ShouldSelfTerminate(snap, currentTick, _maxStagnantTicks))
                    _toStop.Add(kv.Key);
            }

            for (int i = 0; i < _toStop.Count; i++)
            {
                int id = _toStop[i];
                if (!_active.TryGetValue(id, out LoopRecord rec)) continue;

                _sink.EndLoop(rec.Handle);   // 44 对**自有句柄**执行(ADR-028 ⑤)
                _active.Remove(id);
                _snapshots.Remove(id);
                SelfTerminatedCount++;
            }
            return _toStop.Count;
        }
    }
}
