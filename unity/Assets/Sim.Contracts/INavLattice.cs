// 导航格的**只读承载契约**(ADR-016 §三「AI 读粗粒度整数格」· ADR-015 §四「chunk 是流式粒度,
// 不是坐标原点」· ADR-022 §五「导航格按 chunk 切片,未驻留 = 全 block」)。
//
// 权威来源:
//   `production/epics/level-tool/story-005-world-nav-chunk-slicing.md` AC-1(BLOCKING)
//   · ADR-022 §五(`O-6-12` 工具侧)· ADR-015 §四(禁 chunk 局部坐标系)
//   · `world-ecozones/EPIC.md` 的 Governing ADRs 表逐字写「6 拥有 `INavLattice`」
//
// ═══════════════════════════════════════════════════════════════════════════
// §为什么本类型是**接口**而不是一个具体类
// ═══════════════════════════════════════════════════════════════════════════
//   消费方横跨两侧:27 的寻路决策住 `Sim`(门 A 侧),13 病人的运动表现住边界层。
//   把**契约**放 `Sim.Contracts`(= 两侧都能看见的最小公共面),把**实现**放 `Sim`
//   (`Sim/World/NavLattice.cs`)—— 与 `IPayloadEncoder`(ADR-029)/ `IPayloadDecoder`(ADR-031)
//   同款分层:**接口在契约程序集,句柄在实现程序集**。
//
// ⚠️ **本类型刻意只有读面**。写面(装载 / 卸载 chunk)归系统 6 的 chunk 激活器
//   (`ChunkActivator`,ADR-023 ⑥),不在本契约内 —— 与 `IBlobPool` 只有读面、
//   写面另立 `IBlobSink` 是同一条纪律(ADR-029 §②)。
//
// ⚠️ **`IsWalkable` 对越界格 / 未驻留 chunk 一律回 `false`(block)**。这不是「尽力而为」,
//   是 AC-1 的判据:未驻留 chunk 查询回 `block`(**不是** `walkable`)。
//   静默的另一种写法是「未驻留 ⇒ 默认可走」—— 那会让 AI 走进没有数据的区域,
//   而症状(敌人卡在墙里)离病根(代码没装载该 chunk)极远。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 导航格的只读查询面:某格**当前**是否可走。
    /// <para>⚠️ 「当前」= 在本进程已驻留的数据上判定。未驻留的 chunk **一律**回
    /// <c>false</c>(block,ADR-022 §五 / AC-1),不抛、不猜。</para>
    /// </summary>
    public interface INavLattice
    {
        /// <summary>
        /// 该格是否可走。
        /// <para>越界 / 未驻留 chunk / 该格在产物里标记为不可走 —— 三种情形**都**回 <c>false</c>。</para>
        /// </summary>
        /// <param name="cell">整数格坐标(ADR-015 §三 的单一格)。</param>
        bool IsWalkable(WorldPos cell);
    }
}
