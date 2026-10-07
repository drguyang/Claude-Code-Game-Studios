// diagnosis-system Story 005 —— 快照采样(S-8.1 持续型 vs 快照型 · 采样点 = 动作完成 tick)。
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md §S-8.1(持续型跟 tick 实时;快照型按**动作完成那一
//     tick** 采样,非开始时刻)· AC-8-25(快照冻结 vs 持续刷新)· AC-8-48(采样点逐位一致)
//   Implementation Note 2:采样点实现 = 动作完成回调携带 completion_tick,以该 tick
//     从取数口取一次值并物化为快照;**禁缓存「当前 tick 猜测值」**
//
// ⚠️ **结构性防错**:<see cref="SampleOnCompletion"/> 的签名里**没有 currentTick 参数** ——
//    实现物理上无法拿「当前 tick」猜测(AC-8-48「采样时刻不定死即测试失败」的机器侧根)。
// ⚠️ **零字段**:本类型静态零缓存(AC-8-3 反射扫描);快照 = 值 struct,物化后源怎么变
//    都不影响(冻结的机器语义)。
// ⚠️ 取数口以 <c>Func&lt;long, Outcome&gt;</c> 注入:生产接线时包 DiagnosisVitalsFacade
//    ([D-FACADE] IL 门:IVitalsQuery.GetVitals 唯一调用点在 Facade);P0 状态机本体不直接
//    碰体征口,9 的 Progress 端点接线归后续接线 story(本 story 只锁采样语义)。

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>一张已物化的读数快照(快照型通道 —— 冻结,AC-8-25)。</summary>
    public readonly struct ReadingSnapshot
    {
        /// <summary>快照时刻的读数形态(冻结)。</summary>
        public readonly SignReadState State;

        /// <summary>快照时刻的词(冻结 —— 之后 Progress 怎么变都不影响,AC-8-25)。</summary>
        public readonly string DisplayWord;

        /// <summary>快照时刻的可读性。</summary>
        public readonly bool IsReadable;

        /// <summary>采样 tick = <b>completion_tick</b>(动作完成那一 tick,非开始 —— AC-8-48)。</summary>
        public readonly long SampledAt;

        public ReadingSnapshot(SignReadState state, string displayWord,
                               bool readable, long sampledTick)
        {
            State = state;
            DisplayWord = displayWord;
            IsReadable = readable;
            SampledAt = sampledTick;
        }
    }

    /// <summary>读数取数口(以 tick 为自变量 —— 模拟 9 的 Progress 历史;生产 = Facade 包装)。</summary>
    /// <param name="tick">要读取的逻辑 tick。</param>
    /// <returns>该 tick 上的求值结果。</returns>
    public delegate DiagnosisReadFloorEvaluator.Outcome ReadingAtTick(long tick);

    /// <summary>快照型 / 持续型两通道的采样器(S-8.1)—— **纯静态零字段**。</summary>
    public static class DiagnosisSnapshotSampler
    {
        /// <summary>
        /// <b>快照型采样</b>(触 / 叩 / 听 / 问):以 <paramref name="completionTick"/>
        /// (动作**完成**回调携带)从取数口取**一次**并物化为快照。
        /// <para>冻结语义(AC-8-25):返回后源再变,快照字段逐字不变 —— 快照是值,
        /// 不回源、无懒求值。</para>
        /// <para>逐位一致(AC-8-48):两次独立调用(「两进程」结构面)喂同一 completion_tick
        /// ⇒ 快照逐字相同;中途 tick 上源的变化不影响(实现无 currentTick 可拿)。</para>
        /// </summary>
        /// <param name="completionTick">动作完成 tick(非开始 tick)。</param>
        /// <param name="readAt">取数口(tick → Outcome;测试以 Progress 历史注入)。</param>
        /// <returns>冻结快照。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="readAt"/> 为 null。</exception>
        public static ReadingSnapshot SampleOnCompletion(long completionTick, ReadingAtTick readAt)
        {
            if (readAt == null) throw new ArgumentNullException(nameof(readAt));
            DiagnosisReadFloorEvaluator.Outcome o = readAt(completionTick); // 唯一一次取值
            return new ReadingSnapshot(o.State, o.DisplayWord, o.IsReadable, completionTick);
        }

        /// <summary>
        /// <b>持续型刷新</b>(视诊):跟当前 tick 实时取值 —— **不物化冻结**,每 tick 重算。
        /// <para>AC-8-25 的另一侧:同一源在不同 tick 取 ⇒ 结果可不同(跟 tick)。</para>
        /// </summary>
        /// <param name="currentTick">当前逻辑 tick。</param>
        /// <param name="readAt">取数口。</param>
        /// <returns>当前 tick 的求值结果。</returns>
        public static DiagnosisReadFloorEvaluator.Outcome RefreshContinuous(
            long currentTick, ReadingAtTick readAt)
        {
            if (readAt == null) throw new ArgumentNullException(nameof(readAt));
            return readAt(currentTick);
        }
    }
}
