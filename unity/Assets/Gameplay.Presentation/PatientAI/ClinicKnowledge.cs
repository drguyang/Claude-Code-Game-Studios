// patient-ai Story 002 —— F-13.2 的就诊知识判据(`KnowsClinic` 与 `HomeRegion`)。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Formulas F-13.2(2026-09-18 R4 重写)
//   ADR-021 §一(POI 一分为二:定义 = 派生态 / 状态进流)—— 本式读**定义**侧
//   TR-patient-006 / TR-patient-010(空间量一律 WorldPos 整数格)
//
// ⚠️ **F-13.2 R4 的两个真缺陷(本件按重写后的口径落)**:
//   ① 原稿 `ClinicKnownByRegion` 是一张**没人产出**的烘焙表 ⇒ 悬空符号。
//      修法:`KnowsClinic` = **两个已登记事实的析取**(静态 POI 定义 ∪ 世界流 StructurePlaced)
//      —— **无需新 Kind、无需新烘焙表**。
//   ② `HomeRegion` 原稿**未定义** ⇒ 现 = `EcozoneOf(spawn_anchor(p))` 纯函数。
//
// ⚠️ **为什么不用「距离」**(GDD F-13.2 注):病人「知不知道有医馆」是**世界知识**,
//   不是感知结果。走距离会让「回家的病人突然想起来有医馆」变成位置相关抖动,
//   且会把 `Map()` 绑上积分量 `p.Cell`(违规则七)。
//
// ⚠️ **`spawn_anchor` 病人侧未登记(`O-13-5`)** —— 52 目前只在遭遇体路径带该字段。
//   本件**只消费** `spawn_anchor`,**不代 52 定其载荷**;接口由注入的委托承载,接口未定型
//   记为欠债而非既定(GDD F-13.2 末注)。

using System;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>F-13.2 的只读事实源 —— 13 消费,**不拥有**这些事实。
    /// <para>三个输入**都在三源内**(烘焙数据 / 事件流),故 `KnowsClinic` 满足规则六重建性。</para></summary>
    public interface IClinicKnowledgeSource
    {
        /// <summary>`EcozoneOf(spawn_anchor(p))` —— 病人「从哪来」的生态区(静态定义)。
        /// <para>实装时**直接转调** 6 的 <c>EcozoneRegistry.EcozoneOf</c>(不另立第二套生态区表)。</para></summary>
        EcozoneId EcozoneOf(WorldPos cell);

        /// <summary>`∃ poi ∈ POI_DEF : poi.type == CLINIC ∧ EcozoneOf(poi.cell) == R`
        /// —— 该区**静态**存在医馆 POI(6 的烘焙定义,ADR-021 §一 定义侧)。</summary>
        bool HasClinicPoiIn(EcozoneId region);

        /// <summary>`∃ e ∈ 世界流 : e.Kind == StructurePlaced ∧ e.cell ∈ CLINIC_ROOM_CELLS(R)`
        /// —— 该区**玩家建了**医馆(23 的 `StructurePlaced`,世界流)。</summary>
        bool HasPlayerBuiltClinicIn(EcozoneId region);
    }

    /// <summary><see cref="IClinicKnowledgeSource"/> 的**纯函数装配器** —— F-13.2 逐字落地。
    /// <para>不持有状态(全部事实由注入源提供),故自身是纯函数,可重放。</para></summary>
    public sealed class ClinicKnowledge
    {
        private readonly IClinicKnowledgeSource _source;

        public ClinicKnowledge(IClinicKnowledgeSource source)
            => _source = source ?? throw new ArgumentNullException(nameof(source));

        /// <summary>`HomeRegion(p) := EcozoneOf(spawn_anchor(p))`(GDD F-13.2)。
        /// <para>⚠️ **只在初始化时求一次** —— `spawn_anchor` 是静态定义,病人当前位置变化**不回改**
        /// 本值(AC 第 6 条)。调用方须缓存首次结果。</para></summary>
        public EcozoneId HomeRegion(WorldPos spawnAnchor)
            => _source.EcozoneOf(spawnAnchor);

        /// <summary>`ClinicKnownByRegion(R)`(GDD F-13.2)—— 两个事实的析取。
        /// <para>静态 POI 定义 ∨ 玩家建造事件。**无新 Kind、无新烘焙表**。</para></summary>
        public bool ClinicKnownByRegion(EcozoneId region)
            => _source.HasClinicPoiIn(region) || _source.HasPlayerBuiltClinicIn(region);

        /// <summary>`KnowsClinic(p)` —— `HomeRegion` 所在的区是否知道有医馆。
        /// <para>等价于 `ClinicKnownByRegion(HomeRegion(spawnAnchor))`。</para></summary>
        public bool KnowsClinic(WorldPos spawnAnchor)
            => ClinicKnownByRegion(HomeRegion(spawnAnchor));
    }
}
