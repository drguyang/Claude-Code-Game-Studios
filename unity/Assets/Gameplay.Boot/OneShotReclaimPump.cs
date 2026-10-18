// 权威来源:S15-5(TD-audio-029 一次性 cue 归还 + TD-audio-032 ③)
// ADR-028 ①(声源池唯一所有者 = 44)· ADR-028 ⑤(cue 驱动生命周期)
//
// ⚠️ **为什么观测在 Boot 侧而不是池里**:池不持 clip(素材装载面 = AudioClipLibrary),
//   判「播完」只能问播放器本身(`AudioSource.isPlaying`)。本类即那个问的一方:
//   每帧对池报告的「待归还」池键轮询一次,不在播了就报 `MarkFinished`,由池执行归还。
//   ⇒ 池保持「不认识时间、不认识时长」;观测与执行二分。
//
// ⚠️ **为什么挂在池上而不是渲染目标上**:装配失败时渲染目标 = `NullRenderTarget` 链,
//   而池恒存在 ⇒ 挂池让归还路径在两种装配下都成立(不依赖「渲染目标是否真装配」)。
//
// ⚠️ **代价(如实登记)**:每帧对「待归还」集合做一次遍历 + 每帧一次 `isPlaying` 读;
//   集合上界 = 在响声源数(≤ PATIENT_APPEARANCE_CAP 24 + 玩家)⇒ 有界。
//   `isPlaying` 的真实时序语义(播放态 vs 编辑器非播放态)**EditMode 不可证** ⇒
//   归 PlayMode 夹具(`unity/Assets/Tests/PlayMode/`)+ 本类头注如实标 NOT-RUN。

using DaYiJingCheng.Gameplay.Presentation.Audio;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一次性 cue 的归还帧泵(S15-5 · TD-audio-029):观测「播完」并驱动池回收。
    /// <para>每帧:① 对池的待归还池键问 `isPlaying`,不在播 ⇒ <see cref="AudioSourcePool.MarkFinished"/>;
    /// ② <see cref="AudioSourcePool.ReclaimExpired"/> 归还探针面 / 兜底窗口已判完的源。</para></summary>
    public sealed class OneShotReclaimPump
    {
        private readonly AudioSourcePool _pool;

        /// <summary>构造。</summary>
        /// <param name="pool">44 的声源池(非 null —— 装配缝必须 fail-loud)。</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="pool"/> 为 null。</exception>
        public OneShotReclaimPump(AudioSourcePool pool)
            => _pool = pool ?? throw new System.ArgumentNullException(nameof(pool));

        /// <summary>当前观测到的待归还一次性源数(可观测面 == 池的待归还数)。</summary>
        public int PendingCount => _pool.PendingOneShotCount;

        /// <summary>推进一帧(由 `BootRoot.Update` 每个 tick 边沿调用,见 `BootRoot` 的注)。
        /// <para>次序 = 回收(消耗「新鲜」排除)→ 观测 → 回收。</para>
        /// <para>可被反复调用:无一次性源时只做两次空遍历,零分配。</para></summary>
        /// <returns>本帧归还的源数。</returns>
        public int Pump() => Pump(_pool);

        /// <summary>静态形态 —— 帧泵只依赖池、无每帧分配的状态,挂机处(`BootRoot`)不必持对像。</summary>
        /// <param name="pool">44 的声源池。</param>
        /// <returns>本帧归还的源数。</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="pool"/> 为 null。</exception>
        public static int Pump(AudioSourcePool pool)
        {
            if (pool == null) throw new System.ArgumentNullException(nameof(pool));
            // ⚠️ 两段式:先一次回收把「刚排队」标记消耗掉(该轮不碰新源),再观测,再回收。
            //    只做一段的话,新排队的源要么当轮被误回收,要么永远带着标记无法回收。
            int reclaimed = pool.ReclaimExpired();

            // 快照由池维护(只在待归还会员变动时重建)⇒ 本循环零分配。
            System.Collections.Generic.IReadOnlyList<int> pending = pool.PendingOneShotActors();
            for (int i = 0; i < pending.Count; i++)
            {
                int actor = pending[i];
                AudioSource source = pool.SourceForActor(actor);
                if (source == null || source.isPlaying) continue;   // 还在响 / 已被别处归还
                if (pool.MarkFinished(actor)) reclaimed++;          // 已不在播 ⇒ 报「播完」(幂等)
            }
            return reclaimed + pool.ReclaimExpired();   // 探针面 / 兜底窗口判完的源也计入
        }
    }
}
