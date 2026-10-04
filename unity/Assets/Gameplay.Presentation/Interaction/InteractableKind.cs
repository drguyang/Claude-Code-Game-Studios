// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则二 / 数据契约 4-DC-2(INTERACTABLE_KINDS 十项闭集)
//   ADR-009 §七(拾取三段式:意图事件 + 主机判距 + 效果进流 —— 4 永不结算)
//   ADR-020 §四(玩家位移纯表现态,唯一投影 = 跨格世界流事件)
//
// 本文件是 4 的**类型面**第一件:目标种类闭集。它刻意住在 4 自己的装配里 ——
//   · 不引任何结算侧类型(AC-4-01);
//   · 不引 9 / 11 / 8 的 sim 侧类型(AC-4-05);
//   · 不引 ICameraRig / 档位枚举(AC-4-11);
//   · 不引 IEventSink(AC-4-02)。
// 该闭集的唯一的「玩家」缺席是刻意的:玩家不是可交互目标(规则二注)。

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 可交互目标种类闭集(GDD 规则二 / 4-DC-2)。
    /// <para><b>十项,刻意不含 <c>Player</c></b> —— 玩家自己不是交互目标。</para>
    /// <para>顺序即 <c>KindPriority</c> 的登记序,但**优先级数值归数值轮**
    /// (F-4.1 三键的中间键;本故事只签闭集形状,不签任何取值)。</para>
    /// </summary>
    /// <remarks>
    /// 该枚举是 4 唯一的「种类」真源。F-4.1 全序的 `KindPriority[kind]` 查表
    /// 用它作索引;等距决胜归 story 002(AC-4-06)。
    /// </remarks>
    public enum InteractableKind : byte
    {
        /// <summary>掉落实体(世界流 <c>DropSpawned</c> 锚点格)。</summary>
        Drop = 0,

        /// <summary>采集点(6 的烘焙逻辑层资源点)。</summary>
        ForageSpot = 1,

        /// <summary>病人(13 的 <c>IPresentPatients</c> 只读视图)。</summary>
        Patient = 2,

        /// <summary>POI 格(6 的烘焙逻辑层;<c>PoiStateChanged</c> 由 6 写,4 只选目标)。</summary>
        PoiCell = 3,

        /// <summary>容器(23 的 <c>BakedInitial</c> 实例表)。</summary>
        Container = 4,

        /// <summary>建造槽位(6 的烘焙逻辑层建造槽位格)。</summary>
        BuildSlot = 5,

        /// <summary>器具 / 工具(23 的 <c>BakedInitial</c> 实例表;<c>structure_id</c> 定稳定序)。</summary>
        Utensil = 6,

        /// <summary>医馆面板(23 的 <c>BakedInitial</c> 实例表)。</summary>
        ClinicPanel = 7,

        /// <summary>门(23 的 <c>BakedInitial</c> 实例表)。</summary>
        Door = 8,

        /// <summary>开关(23 的 <c>BakedInitial</c> 实例表)。</summary>
        Switch = 9
    }
}
