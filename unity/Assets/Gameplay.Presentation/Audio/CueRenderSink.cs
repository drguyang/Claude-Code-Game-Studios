// 权威来源:production/epics/audio-system/story-007-net-divergence-remote-derivation.md
//   (AC-44-07 静态半 —— SetTier 默认语义 · IAudioCueSink 生产实现)
// GDD:design/gdd/audio-system.md §Edge Cases 联机与空间化(裁定 D-A:各设备按本机技能档,
//   「音与字都分叉,统一的只有事实」)· §Edge Cases 听诊与呼吸层(重复 BeginLoop 以最后一次为准 /
//   过期 EndLoop 忽略)
// ADR-018 §一(44 只触发 / 只渲染,永不持有游戏状态)· §二(IAudioCueSink 四方法形状)
//   · §五(联机:精度统一取主机技能的是**混音总线**,脉案仍各自分叉)
//
// ⚠️ **本类是 44 对上游的唯一界面 `IAudioCueSink` 的生产实现**(补评前该位置是测试自造的
//    `TierRecordingSink` —— 评审原件 §2.1 判 🟡 半真:接口真、实现假)。
// ⚠️ **句柄生命周期**:重复 `BeginLoop` 同源 ⇒ **以最后一次为准**,旧句柄释放(防「两层呼吸」
//    bug,GDD Edge Cases);过期 `EndLoop` ⇒ 忽略,不产生播放错误。
//    ⇒ 本类**不持游戏状态**(ADR-018 §一):持的是「哪些循环正在响」的**呈现层事实**。
// ⚠️ **`SetTier` 记录的是本机档**(裁定 D-A):本类**不接受**任何「主机档」来源 ——
//    `TierSource` 闭集只有 `Local`(契约层已钉死),本类不引入第二个来源。
// ⚠️ **渲染动作转发给 `ICueRenderTarget`**(注入 seam,与 `IMixerParameterSink` /
//    `IBreathLayerTransport` 同型)—— 真实播放(素材 / AudioSource / mixer)归 S14-3/S14-6
//    的实现面;本类负责**契约语义**(句柄唯一性 / 档位记录 / 只入不出),这部分 P0 可判。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>cue 渲染动作的**最小面**(44 自用的注入 seam;生产实现归素材接入轮)。
    /// <para>⚠️ 本接口是 44 **自用**的注入缝,不对外提供、不持游戏状态(与
    /// <c>IMixerParameterSink</c> / <c>IBreathLayerTransport</c> 同性质)⇒ 登记进 44 的
    /// <c>EntryInterfaceAllowlist</c>。</para></summary>
    public interface ICueRenderTarget
    {
        /// <summary>播一条一次性 cue(咳嗽 / 翻页 / 狗吠)。</summary>
        /// <param name="cue">cue 载荷(`Cell` 由空间化注入)。</param>
        void PlayOneShot(in AudioCueDto cue);

        /// <summary>起一条循环 cue,返回渲染侧句柄。</summary>
        /// <param name="cue">cue 载荷。</param>
        /// <param name="handleId">44 分配的句柄 id。</param>
        void StartLoop(in AudioCueDto cue, int handleId);

        /// <summary>停一条循环 cue。</summary>
        /// <param name="handleId">44 分配的句柄 id。</param>
        void StopLoop(int handleId);
    }

    /// <summary><c>IAudioCueSink</c> 的**生产实现**(AC-44-07 静态半)。
    /// <para>职责:① 一次性 / 循环 cue 的语义分派;② 句柄唯一性(重复 `BeginLoop` 以最后一次为准,
    /// 旧句柄释放);③ 过期 `EndLoop` 忽略;④ `SetTier` 记录本机档(裁定 D-A)。</para>
    /// <para>⚠️ **不持游戏状态**(ADR-018 §一):`_loops` 表是「哪些循环正在响」的呈现层事实。</para>
    /// <example>
    /// var sink = new CueRenderSink(new NullRenderTarget());
    /// sink.SetTier(TierSource.Local);
    /// AudioCueHandle h = sink.BeginLoop(breathCue);
    /// sink.EndLoop(h);
    /// </example></summary>
    public sealed class CueRenderSink : IAudioCueSink
    {
        private readonly ICueRenderTarget _target;

        // 声源 id → 当前存活句柄。⚠️ 键 = 声源(不是 cue id)—— 重复 BeginLoop 判据按**同源**
        // (GDD Edge Cases「同源重复循环」)。
        private readonly Dictionary<int, AudioCueHandle> _loops = new Dictionary<int, AudioCueHandle>();
        private int _nextHandleId = 1;

        /// <summary>构造生产 sink。</summary>
        /// <param name="target">渲染动作接收面(null ⇒ <see cref="ArgumentNullException"/>)。</param>
        public CueRenderSink(ICueRenderTarget target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        /// <summary>当前存活的循环句柄数(测试与调试读)。</summary>
        public int ActiveLoopCount => _loops.Count;

        /// <summary>最近一次收到的档位(裁定 D-A:恒为本机 `Local`)。</summary>
        public TierSource CurrentTier { get; private set; }

        /// <summary>`SetTier` 被调用的次数(「恰好一次」的观测面)。</summary>
        public int SetTierCallCount { get; private set; }

        /// <inheritdoc/>
        public void Emit(in AudioCueDto cue) => _target.PlayOneShot(cue);

        /// <inheritdoc/>
        public AudioCueHandle BeginLoop(in AudioCueDto cue)
        {
            int source = cue.Source;

            // 同源重复 BeginLoop ⇒ 以最后一次为准:先释放旧句柄(防两层呼吸并存)。
            if (_loops.TryGetValue(source, out AudioCueHandle old))
            {
                _target.StopLoop(old.Id);
                _loops.Remove(source);
            }

            var handle = new AudioCueHandle(_nextHandleId++);
            _loops[source] = handle;
            _target.StartLoop(cue, handle.Id);
            return handle;
        }

        /// <inheritdoc/>
        public void EndLoop(AudioCueHandle handle)
        {
            // 过期句柄 ⇒ 忽略(不产生播放错误):句柄必须仍在本表内,且键值对应。
            int key = -1;
            foreach (KeyValuePair<int, AudioCueHandle> kv in _loops)
            {
                if (kv.Value.Id != handle.Id) continue;
                key = kv.Key;
                break;
            }
            if (key < 0) return;   // 过期 / 已释放 ⇒ 静默忽略

            _loops.Remove(key);
            _target.StopLoop(handle.Id);
        }

        /// <inheritdoc/>
        public void SetTier(TierSource source)
        {
            CurrentTier = source;
            SetTierCallCount++;
        }
    }

    /// <summary>空渲染目标(无素材时的占位;玩家构建由 S14-6 的烘焙管线替换)。
    /// <para>⚠️ **不是静默降级**:它只吞渲染动作,**契约语义(句柄 / 档位)仍由
    /// <see cref="CueRenderSink"/> 保证** —— 素材缺失的拒收归构建期门(ADR-014 阶段 2)。</para></summary>
    public sealed class NullRenderTarget : ICueRenderTarget
    {
        /// <inheritdoc/>
        public void PlayOneShot(in AudioCueDto cue) { }

        /// <inheritdoc/>
        public void StartLoop(in AudioCueDto cue, int handleId) { }

        /// <inheritdoc/>
        public void StopLoop(int handleId) { }
    }
}
