// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则二(四源装载 + 玩家格取路 (a))/ F-4.1b
//   ADR-016 §三 / ADR-020 §四(玩家格 = **经流确立的格**;`pending_cell` 只用于预表现)
//   ADR-009 §四(候选集 = 派生态:重建,不进流、不存档)
//   ADR-006(structure_id / instance_id / patient_id / poi_id 皆 int64 标量空间)
//
// ⚠️ 本类**只装载**(`→ IReadOnlyList<Candidate>`),**不选择**(argmin 归 story 002 的
//   `InteractionSelector`)、**不裁剪**(R_INTERACT 邻域过滤归 story 004 —— 半径过滤发生在
//   装载之后、argmin 之前)。
//
// ⚠️ 玩家格取路 (a)(规则二):`PlayerCell` 由**调用方**以「经流确立的格」注入 ——
//   本类**没有** `pending_cell` 形参,结构上不可能读到表现态本地格。
//   (判据 = 参数表只有确立格;AC-4-20 的两客户端夹具即抓此项。)
//
// ⚠️ 零结算、零流写入:本类不持 `IEventSink`(AC-4-02),不引任何结算侧类型(AC-4-01/05)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 四源候选集装载器(规则二)。
    /// <para>把四个整数格源合并成 F-4.1 argmin 的输入行 `Candidate`。候选集是**派生态**
    /// (ADR-009 §四):由外生源确定性重建,不进流、不存档。</para>
    /// <para><b>四源</b>(GDD 规则二):① 世界流 `DropSpawned.spawn_anchor`;
    /// ② 6 烘焙逻辑层(POI / 采集点 / 建造槽位);③ 13 `IPresentPatients` 只读在场视图;
    /// ④ 23 `BakedInitial`(诊所开档自带的门 / 柜 / 工作台 / 面板 / 开关)。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>无状态</b>:本类只有 <c>readonly</c> 实例字段(源引用),无 static 可变状态。
    /// 同 (源, 玩家格) ⇒ 同候选序列。</para>
    /// <para><b>玩家格取路</b>:选择计算用「经流确立的格」(规则二 (a))。本地 <c>pending_cell</c>
    /// 只用于预表现(UI 高亮),**不得**进入本装载器的输入 —— 否则同一 tick 两客户端各用各的格
    /// ⇒ 选择分叉,而选择分叉又不在流里,无从重放(ADR-020 §四 的读方半边)。</para>
    /// </remarks>
    public sealed class CandidateSetLoader
    {
        private readonly CandidateSources _sources;

        /// <summary>构造装载器。</summary>
        /// <param name="sources">四源依赖包(显式注入 —— 禁服务定位器)。</param>
        public CandidateSetLoader(CandidateSources sources)
        {
            _sources = sources;
        }

        /// <summary>
        /// 装载候选集(四源合并)。
        /// <para>⚠️ <b>不做半径裁剪</b> —— 裁剪发生在 argmin **之前**、归 story 004;
        /// 本方法返回**全量**四源候选,由调用方按需预裁(或交给 story 004 的裁剪器)。</para>
        /// </summary>
        /// <returns>候选行只读列表;四源皆空时返回空列表(**不是** null —— 调用方据空集走
        /// <c>InteractionSelector.Select</c> 的零候选门 ⇒ <c>None</c>,见 AC-4-06)。</returns>
        public IReadOnlyList<Candidate> Load()
        {
            var candidates = new List<Candidate>();

            // ① 世界流掉落锚格(身份进流 / 位置表现 —— 取 spawn_anchor 整数格)
            if (_sources.Drops != null)
                foreach (var d in _sources.Drops.Rows)
                    candidates.Add(new Candidate(d.Anchor, InteractableKind.Drop, d.InstanceId,
                                                 StableIdSource.InstanceId));

            // ② 6 烘焙逻辑层(POI / 采集点 / 建造槽位)
            if (_sources.WorldBaked != null)
            {
                foreach (var p in _sources.WorldBaked.PoiCells)
                    candidates.Add(new Candidate(p.Cell, InteractableKind.PoiCell, p.PoiId,
                                                 StableIdSource.PoiId));
                foreach (var f in _sources.WorldBaked.ForageSpots)
                    candidates.Add(new Candidate(f.Cell, InteractableKind.ForageSpot, f.BakedResourceIndex,
                                                 StableIdSource.BakedResourceIndex));
                foreach (var b in _sources.WorldBaked.BuildSlots)
                    candidates.Add(new Candidate(b.Cell, InteractableKind.BuildSlot, b.SlotLinearKey,
                                                 StableIdSource.SlotLinearKey));
            }

            // ③ 13 在场病人视图(只出格 + patient_id)
            if (_sources.Patients != null)
                foreach (var p in _sources.Patients.Rows)
                    candidates.Add(new Candidate(p.Cell, InteractableKind.Patient, p.PatientId,
                                                 StableIdSource.PatientId));

            // ④ 23 BakedInitial(开档自带 —— 无任何流事件;漏此则开档门柜静默不可交互)
            if (_sources.BakedInitial != null)
            {
                foreach (var u in _sources.BakedInitial.Utensils)
                    candidates.Add(new Candidate(u.Cell, InteractableKind.Utensil, u.StructureId,
                                                 StableIdSource.StructureId));
                foreach (var c in _sources.BakedInitial.ClinicPanels)
                    candidates.Add(new Candidate(c.Cell, InteractableKind.ClinicPanel, c.StructureId,
                                                 StableIdSource.StructureId));
                foreach (var d in _sources.BakedInitial.Doors)
                    candidates.Add(new Candidate(d.Cell, InteractableKind.Door, d.StructureId,
                                                 StableIdSource.StructureId));
                foreach (var s in _sources.BakedInitial.Switches)
                    candidates.Add(new Candidate(s.Cell, InteractableKind.Switch, s.StructureId,
                                                 StableIdSource.StructureId));
            }

            return candidates;
        }

        /// <summary>
        /// 装载 + **按 `R_INTERACT` 邻域裁剪**(GDD 规则二:裁剪在装载之后、argmin 之前)。
        /// <para><b>为什么在这里而不是在调用方各自写一遍</b>:`d∞` 必须与
        /// <see cref="InteractionSelector"/> 的第一键**同一式**(先拓宽 int64 再取绝对值 ——
        /// story 002 承重纪律)。⚠️ 算式本身在本命名空间仍有私有副本
        /// (`HarvestInteractionDriver.Chebyshev` —— 既有先例:跨程序集不便共享,且各处均已注)。
        /// 本方法的作用是让**路由**不必再写第三份。</para>
        /// <para>⚠️ 玩家格 = **经流确立的格**(ADR-020 §四);本方法不在乎它从哪来,但调用方不得传表现态连续位置。</para>
        /// </summary>
        /// <param name="playerCell">玩家经流确立格。</param>
        /// <param name="radius">交互半径(单源 <see cref="InteractionRadius"/>.Value)。</param>
        /// <returns>半径内的候选;全部在半径内时返回原列表(零分配)。</returns>
        public IReadOnlyList<Candidate> LoadWithin(in WorldPos playerCell, int radius)
        {
            IReadOnlyList<Candidate> all = Load();
            if (all == null || all.Count == 0) return all;

            List<Candidate> kept = null;
            for (int i = 0; i < all.Count; i++)
            {
                Candidate c = all[i];
                if (Chebyshev(in playerCell, in c.Cell) <= radius)
                {
                    kept?.Add(c);
                }
                else if (kept == null)
                {
                    kept = new List<Candidate>(i);
                    for (int j = 0; j < i; j++) kept.Add(all[j]);
                }
            }
            return kept ?? all;
        }

        /// <summary>两格 Chebyshev 距离 <c>d∞ := max(|Δx|,|Δy|,|Δz|)</c>(F-4.1 第一键同式)。
        /// <para>⚠️ 先拓宽 int64 再取绝对值(int 域相减会在 <c>int.MinValue</c> 处回绕)。</para></summary>
        private static long Chebyshev(in WorldPos a, in WorldPos b)
        {
            long dx = System.Math.Abs((long)a.X - b.X);
            long dy = System.Math.Abs((long)a.Y - b.Y);
            long dz = System.Math.Abs((long)a.Z - b.Z);
            long m = dx > dy ? dx : dy;
            return m > dz ? m : dz;
        }
    }
}
