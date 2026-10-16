// 权威来源:production/epics/audio-system/story-008-spatialization-world-breath.md(优先级公式)
// GDD:design/gdd/audio-system.md §Visual/Audio 多声源混合规则(:832-860)
//    rank_key(source) = ( priority_class ASC, distance ASC, cell_jitter(ActorId) ASC, CueId ASC )
// ADR-018 §六(「赢」= 抢占序 + duck 深度,只压不丢;bus ≠ 优先级类别)· ADR-024 §⑥(单一出处)
//
// ⚠️ **重算时机是判据的一部分**(GDD :848):「rank_key 只在声源生灭时重算(挂起期间不逐帧重排
//    ⇒ 防增益抖动;跨格移动导致的名次变化并入生灭级事件窗,不逐帧)」⇒ 本类**持缓存**,
//    由调用方在声源生灭时调 <see cref="Invalidate"/> 显式失效。**不**做逐帧重排。
// ⚠️ **d = 0 / 等距**:公式**零除式**(权重不除 d)⇒ 听者与声源同格时无 NaN 路径;
//    等距由 <see cref="CellJitter"/> 与 CueId 兜底。
// ⚠️ **「赢」的语义**:rank 只决定抢占序与 duck 深度,**不丢声**(只压)。本类**不做** duck ——
//    duck 深度(`DUCK_DEPTH_DB`)归 Story 006 的执行体,本类只出序。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>参与排名的声源快照(输入面)。</summary>
    /// <remarks>⚠️ 格三轴以 <c>int</c> 承载而非 <c>Int3</c> —— 44 的公开方法签名受 AC-44-B3
    /// 入口白名单约束(`assembly_boundary_test` b3);先例 = <c>PatientCueEmit.Emit</c>。</remarks>
    public readonly struct RankSource
    {
        /// <summary>优先级类别(自身/检查 = 0 &lt; 世界≈生理 = 1 &lt; 氛围 = 2;GDD :833-837)。</summary>
        public readonly int PriorityClass;
        /// <summary>与听者的距离(同类内更近者优先)。</summary>
        public readonly float Distance;
        /// <summary>声源实体 id(`cell_jitter` 破平键的输入)。</summary>
        public readonly int ActorId;
        /// <summary>声源所在整数格 X(`cell_jitter` 破平键的输入)。</summary>
        public readonly int CellX;
        /// <summary>声源所在整数格 Y。</summary>
        public readonly int CellY;
        /// <summary>声源所在整数格 Z。</summary>
        public readonly int CellZ;
        /// <summary>语义 cue id(终极稳定键)。</summary>
        public readonly int CueId;

        /// <summary>构造一个排名输入。</summary>
        public RankSource(int priorityClass, float distance, int actorId,
                          int cellX, int cellY, int cellZ, int cueId)
        {
            PriorityClass = priorityClass; Distance = distance;
            ActorId = actorId;
            CellX = cellX; CellY = cellY; CellZ = cellZ;
            CueId = cueId;
        }
    }

    /// <summary>排名结果行(输出面)—— 携带全部输入 + 派生的 `cell_jitter`,
    /// 使断言可核对**每一个键**而不必反推。</summary>
    public readonly struct RankedSource
    {
        /// <inheritdoc cref="RankSource.PriorityClass"/>
        public readonly int PriorityClass;
        /// <inheritdoc cref="RankSource.Distance"/>
        public readonly float Distance;
        /// <inheritdoc cref="RankSource.ActorId"/>
        public readonly int ActorId;
        /// <inheritdoc cref="RankSource.CueId"/>
        public readonly int CueId;
        /// <summary>由 (ActorId, Cell) 派生的破平键(第三键)。</summary>
        public readonly ushort CellJitter;

        internal RankedSource(in RankSource s, ushort cellJitter)
        {
            PriorityClass = s.PriorityClass; Distance = s.Distance;
            ActorId = s.ActorId; CueId = s.CueId; CellJitter = cellJitter;
        }
    }

    /// <summary>`rank_key` 计算器(GDD :838-848 的唯一实现)。
    /// <para>四键全序:`priority_class ASC → distance ASC → cell_jitter ASC → CueId ASC`。
    /// 全序**无并列** ⇒ 同输入恒同序(确定性)。</para>
    /// <para>⚠️ **缓存语义**:<see cref="Order"/> 在缓存有效时**不重算**(直接返回上次结果);
    /// 调用方在声源生灭时调 <see cref="Invalidate"/>。<see cref="RecalculationCount"/> 是该
    /// 语义的可观测面 —— 「挂起期间不逐帧重排」由此可断言。</para></summary>
    public sealed class RankKeyComputer
    {
        private readonly List<RankedSource> _cache = new List<RankedSource>();
        private bool _valid;

        /// <summary>累计重算次数(自上次 <see cref="Invalidate"/> 后 <see cref="Order"/>
        /// 真正重排的次数)。可观测面 —— 供「重算只在声源生灭时」的断言使用。</summary>
        public long RecalculationCount { get; private set; }

        /// <summary>当前缓存是否有效。</summary>
        public bool IsCacheValid => _valid;

        /// <summary>显式失效缓存 —— **声源生灭时必须调用**(GDD :848)。
        /// 挂起期间不调 ⇒ <see cref="Order"/> 复用缓存,不重排(防增益抖动)。</summary>
        public void Invalidate() => _valid = false;

        /// <summary>按 `rank_key` 四键全序排列声源。</summary>
        /// <param name="sources">声源快照(null ⇒ <see cref="ArgumentNullException"/>)。
        /// ⚠️ 缓存按「自上次 <see cref="Invalidate"/> 以来」判有效,**不比对内容** ——
        /// 传入不同集合但未 <see cref="Invalidate"/> 是调用方违约(生灭必须显式声明)。</param>
        /// <returns>有序结果(缓存有效时 = 上次同一实例)。</returns>
        public IReadOnlyList<RankedSource> Order(IReadOnlyCollection<RankSource> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            if (_valid) return _cache;

            _cache.Clear();
            foreach (RankSource s in sources)
                _cache.Add(new RankedSource(s,
                    CellJitter.Compute(s.ActorId, s.CellX, s.CellY, s.CellZ)));

            _cache.Sort(Compare);

            _valid = true;
            RecalculationCount++;
            return _cache;
        }

        /// <summary>四键比较(全序,无并列返回 0 的路径 —— CueId 相等且 ActorId 相等即同一源)。</summary>
        private static int Compare(RankedSource a, RankedSource b)
        {
            int c = a.PriorityClass.CompareTo(b.PriorityClass);
            if (c != 0) return c;
            c = a.Distance.CompareTo(b.Distance);
            if (c != 0) return c;
            c = a.CellJitter.CompareTo(b.CellJitter);
            if (c != 0) return c;
            return a.CueId.CompareTo(b.CueId);
        }
    }
}
