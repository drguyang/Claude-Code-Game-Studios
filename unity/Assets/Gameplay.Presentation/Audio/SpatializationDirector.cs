// 权威来源:production/epics/audio-system/story-008-spatialization-world-breath.md(AC-44-D2/D8 装配面)
// ADR-028 ①③⑤(池归 44 · 位置真源 = 只读 AudioCueDto.Cell · 生命周期 cue 驱动)
// GDD:design/gdd/audio-system.md F-44.4(空间化锚点)· F-44.7(世界语境呼吸)· 优先级公式
//
// ⚠️ **本类是 008 的装配入口**,把四个原语(池 / 换算 / 破平键 / 遮挡)接成一条链:
//    cue(整数格)→ LatticeToWorld(比例参入)→ 池中逐源 AudioSource → 遮挡低通 → rank_key 序。
// ⚠️ **位置只读**(ADR-028 ③):锚点**唯一来源** = 传入的 <c>Int3</c>(生产路径上溯
//    `AudioCueDto.Cell`)。本类**不**从 float 反推格、**不**回写、**不**做判定。
// ⚠️ **不持游戏状态**(ADR-018 §一):持有的是「哪些源在响、怎么排」的呈现层事实。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>世界语境空间化的**装配入口**(AC-44-D2 / AC-44-D8 的接线面)。
    /// <para>组合:<see cref="AudioSourcePool"/>(源)· <see cref="LatticeToWorld"/>(锚点)·
    /// <see cref="CellJitter"/>(破平)· <see cref="OcclusionDriver"/>(遮挡)·
    /// <see cref="RankKeyComputer"/>(序)。</para>
    /// <example>
    /// using var dir = new SpatializationDirector(latticeSize: 2f);
    /// dir.Spawn(cue, occluded: false, occlusionLowpassHz: 1200f);
    /// IReadOnlyList&lt;RankedSource&gt; order = dir.Order;
    /// </example></summary>
    public sealed class SpatializationDirector : IDisposable
    {
        private readonly AudioSourcePool _pool;
        private readonly OcclusionDriver _occlusion = new OcclusionDriver();
        private readonly RankKeyComputer _rank = new RankKeyComputer();
        private readonly List<RankSource> _pending = new List<RankSource>();
        private readonly float _latticeSize;

        /// <summary>建装配入口。</summary>
        /// <param name="latticeSize">格边长(米)—— 归系统 6(见 <see cref="LatticeToWorld"/> 文件头注)。</param>
        /// <param name="rootParent">池 root 的父节点;null = 场景根(生产装配用 null,ADR-028 ②)。</param>
        public SpatializationDirector(float latticeSize, Transform rootParent = null)
            : this(latticeSize, new AudioSourcePool(rootParent))
        {
        }

        /// <summary>用**既有池**建装配入口(story-018 · AC-018-4 单池不变量)。
        /// <para>⚠️ 装配轮**必须**走本重载:池的唯一所有者是 44(ADR-028 ①),
        /// 而组合根已建了一个池给 <see cref="AudioSourceRenderTarget"/> 用 ——
        /// 再让本类自建第二个 ⇒ 运行期两个 `"AudioSourcePool"` 根,
        /// 「同一实体两条源 / 两套空间化」的静默分裂。</para></summary>
        /// <param name="latticeSize">格边长(米)。</param>
        /// <param name="pool">共享池(非 null)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="pool"/> 为 null。</exception>
        public SpatializationDirector(float latticeSize, AudioSourcePool pool)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            _latticeSize = latticeSize;
            _pool = pool;
        }

        /// <summary>当前在响声源数。</summary>
        public int ActiveCount => _pool.ActiveCount;

        /// <summary>池中某源的当前锚点格(三轴出参)。</summary>
        public bool TryGetCell(int actorId, out int cellX, out int cellY, out int cellZ)
            => _pool.TryGetCell(actorId, out cellX, out cellY, out cellZ);

        /// <summary>池中某源当前持有的 <c>AudioSource</c>(未生 / 已释放 ⇒ null)。</summary>
        public AudioSource SourceForActor(int actorId) => _pool.SourceForActor(actorId);

        /// <summary>按 `rank_key` 排定的当前序(生灭时才变)。</summary>
        public IReadOnlyList<RankedSource> Order => _rank.Order(_pending);

        /// <summary>当前累计重算次数(「重算只在声源生灭时」的可观测面)。</summary>
        public long RecalculationCount => _rank.RecalculationCount;

        /// <summary>生一条声源:池取源 → 换算锚点 → 施加遮挡低通 → 登记进排名输入。</summary>
        /// <param name="cue">音频 cue(整数格锚点 = <c>cue.Cell</c>;`Source` = 声源实体 id)。</param>
        /// <param name="priorityClass">优先级类别(自身/检查 0 &lt; 世界≈生理 1 &lt; 氛围 2)。</param>
        /// <param name="occluded">遮挡判定(44 本地 raycast 结果;不进流)。</param>
        /// <param name="occlusionLowpassHz">事件表 `occlusion_lowpass` 参数值(Hz)。</param>
        /// <param name="distance">与听者距离(同类内排序键)。</param>
        public void Spawn(in AudioCueDto cue, int priorityClass, bool occluded,
                          float occlusionLowpassHz, float distance)
        {
            // ⚠️ `in` 参数不可被 lambda 捕获(CS1628)⇒ 先落到局部变量。
            int actorId = cue.Source;
            Int3 cell = cue.Cell;          // 体内装配(公开签名受 B3 约束,见类注)
            int cueId = cue.Cue;

            AudioSource src = _pool.Spawn(actorId, cell.X, cell.Y, cell.Z, cueId);

            // 锚点:整数格 → float(单向)。cell_jitter 只叠渲染坐标,不回写。
            // ⚠️ jitter 是**格内**偏移(GDD :845「确定性格内偏移」/ ADR-023 ⑦「格锚点 +
            //    确定性格内偏移」)⇒ 归一化到 [0, latticeSize) 的**格内**位移,不是世界单位。
            //    u16 满量程映射到一格 ⇒ 同格 N 源散布在各自格内,永不越格(越格会破坏
            //    「同格多源」的空间语义,也让 d 计算失真)。
            Vector3 world = LatticeToWorld.ToWorld(cell.X, cell.Y, cell.Z, _latticeSize);
            ushort jitter = CellJitter.Compute(actorId, cell.X, cell.Y, cell.Z);
            float inCellOffset = jitter / 65536f * _latticeSize;
            src.transform.position = new Vector3(
                world.x + inCellOffset, world.y, world.z + inCellOffset);

            // 遮挡:逐源 AudioLowPassFilter(组件按需挂,与源同 GameObject —— 引擎强制)。
            _occlusion.Apply(src, occlusionLowpassHz, occluded);

            // 排名输入:生灭 ⇒ 显式失效(重算只在生灭时,GDD :848)。
            _pending.RemoveAll(s => s.ActorId == actorId);
            _pending.Add(new RankSource(priorityClass, distance, actorId,
                                        cell.X, cell.Y, cell.Z, cueId));
            _rank.Invalidate();
        }

        /// <summary>收一条声源:池归还 → 从排名输入移除 → 失效缓存(生灭级重算)。</summary>
        public void Release(int actorId)
        {
            _pool.Release(actorId);
            _pending.RemoveAll(s => s.ActorId == actorId);
            _rank.Invalidate();
        }

        /// <summary>销毁池与全部声源。幂等。</summary>
        public void Dispose() => _pool.Dispose();
    }
}
