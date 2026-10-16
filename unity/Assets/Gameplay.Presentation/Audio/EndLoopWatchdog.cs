// 权威来源:production/epics/audio-system/story-007-net-divergence-remote-derivation.md
//   (AC-44-D7 家族 · EndLoop 自评兜底 —— ADR-001 §一之三 裁决二)
//   + production/epics/audio-system/story-009-source-lifecycle-handover.md(AC-1「EndLoop 自评兜底」)
// GDD:design/gdd/audio-system.md §Edge Cases 听诊与呼吸层(「上游 EndLoop 因丢包 / 断线永不到达 ⇒
//   循环 cue 必须自评兜底 —— 从最新快照 Progress 自行求值是否该结束(**P0 架构义务**);
//   重连拉流重建 = P1b 增强;**不得**出现「呼吸层永不停止」」)
// ADR-001 §一之三 裁决二(呈现侧循环终止必须自评兜底,零网络依赖,升格**架构义务**)
//   · ADR-018 §一(44 只触发 / 只渲染)· ADR-028 ⑤(44 持有声源池生命周期)
//
// ⚠️ **时钟源 = sim tick(参入),不是墙钟** —— `currentTick` 由调用方从 `ITickProvider` 透传
//    (与 AC-44-D7 ② 的时钟面同纪律)。本类**零** `Time` / `DateTime` 引用,由
//    `AssemblyGates.CheckAudioClockTokens` 的 IL 扫描硬约束。
// ⚠️ **`EndLoop` 不作为网络消息存在**(ADR-001 §一之三 裁决二):`AudioCueDto` 不进任何网络通道
//    ⇒ 本类**不订阅任何网络事件**,只读本地快照 —— 这正是「零网络依赖」的字面兑现。
// ⚠️ **有界性是架构义务的量化**:阈值 `LOOP_EVAL_MAX_TICKS` 必须有**正有界值**,不得留空
//    (ADR-001 裁决二「二者至少其一,不得留空」的 P0 半边;P1b 的拉流重建是另一半,不在本类)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>一条循环 cue 的最新快照(裁决二「从最新快照 `Progress` 自求值」的输入形状)。
    /// <para>⚠️ 本类型是**纯数据载体**(不持状态、不进 sim、不经三流),故登记进 44 的
    /// `EntryAllowlist`(与 `RankSource` 同性质)。</para></summary>
    public readonly struct LoopProgress
    {
        /// <summary>循环 cue 的语义 id;**0 = 非循环** ⇒ 不进兜底面(哨兵口径见下)。</summary>
        public readonly int LoopingCue;
        /// <summary>该循环的进度值(主机快照;`≤ 0` = 无进度可判 ⇒ 不判停)。</summary>
        public readonly int Progress;
        /// <summary>该快照的主机 tick 序号(停滞时长的基准)。</summary>
        public readonly uint ServerTick;

        /// <param name="loopingCue">循环 cue id(0 = 非循环)。</param>
        /// <param name="progress">进度值。</param>
        /// <param name="serverTick">快照的主机 tick。</param>
        public LoopProgress(int loopingCue, int progress, uint serverTick)
        {
            LoopingCue = loopingCue; Progress = progress; ServerTick = serverTick;
        }
    }

    /// <summary>循环终止的**自评兜底谓词**(ADR-001 §一之三 裁决二 · P0 默认且唯一路径)。
    /// <para>判据 = 「快照停更超过有界阈值」 ⇒ 自行结束循环。纯函数、零网络依赖、零墙钟。</para>
    /// <para>⚠️ **不是**「有没有收到 EndLoop」—— 那是网络依赖,本类刻意不读。</para>
    /// <example>
    /// // 每 tick(由 ITickProvider 驱动):
    /// if (EndLoopWatchdog.ShouldSelfTerminate(snap, tickProvider.CurrentTick))
    ///     sink.EndLoop(handle);   // 44 对自有句柄执行(ADR-028 ⑤)
    /// </example></summary>
    public static class EndLoopWatchdog
    {
        /// <summary>判停谓词(阈值 = 登记常量 <see cref="AudioTuning.LoopEvalMaxTicks"/>)。
        /// <para>停滞时长 = `currentTick − snap.ServerTick`(**快照自己的 tick**,不是调用方自述)。</para></summary>
        /// <param name="snap">最新循环快照。</param>
        /// <param name="currentTick">当前 sim tick(来自 `ITickProvider`;禁墙钟)。</param>
        /// <returns>是否应自行终止该循环。</returns>
        public static bool ShouldSelfTerminate(in LoopProgress snap, long currentTick)
            => ShouldSelfTerminate(snap, currentTick, AudioTuning.LoopEvalMaxTicks);

        /// <summary>判停谓词(**阈值显式注入** —— 边界值测试与 P1b 调参面用)。
        /// <para>三条前置:非循环 cue(`LoopingCue == 0`)不进兜底面 · 无进度快照
        /// (`Progress ≤ 0`)无从判 · `currentTick` 不得早于快照(时间倒流 = 不判,防误杀)。</para></summary>
        /// <param name="snap">最新循环快照。</param>
        /// <param name="currentTick">当前 sim tick。</param>
        /// <param name="maxStagnantTicks">停滞阈值(tick;`&gt; 0` 才有界)。</param>
        /// <returns>是否应自行终止该循环。</returns>
        public static bool ShouldSelfTerminate(in LoopProgress snap, long currentTick,
                                               int maxStagnantTicks)
        {
            if (snap.LoopingCue == 0) return false;   // 非循环 cue 不进兜底面
            if (snap.Progress <= 0) return false;     // 无 Progress 快照 ⇒ 无从判

            long stagnant = currentTick - (long)snap.ServerTick;
            if (stagnant <= 0) return false;          // 时间未推进 / 倒流 ⇒ 不判(防误杀在推进的循环)
            return stagnant > maxStagnantTicks;
        }

        /// <summary>停滞谓词(拆出供陈旧面与测试单点断言)。</summary>
        /// <param name="currentTick">当前 sim tick。</param>
        /// <param name="snapshotTick">快照的 tick。</param>
        /// <param name="maxStagnantTicks">停滞阈值(tick)。</param>
        /// <returns>停滞是否**严格超过**阈值(等值不判 —— 边界归下一 tick)。</returns>
        public static bool IsStagnant(long currentTick, uint snapshotTick, int maxStagnantTicks)
        {
            long stagnant = currentTick - (long)snapshotTick;
            return stagnant > 0 && stagnant > maxStagnantTicks;
        }
    }
}
