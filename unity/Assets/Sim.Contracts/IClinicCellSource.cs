// 规则十二 12.1 —— 「医馆格」判据的唯一注入面(52 出诊启动触发用)。
//
// 权威来源:
//   GDD random-events.md 规则十二 12.1(2026-10-25 52 GDD 轮裁定甲)——
//     HouseCallStarted 触发条件之一 = 玩家本 tick 从医馆格跨出到非医馆格;
//     「医馆格判据与 23 建造 / 24 医馆的地块占用同源」(与规则十「无人」判据、
//     规则六之二 ContextMult 的 `IsInClinic` 同一个量)。
//   ADR-015 §三 —— 整数格(`WorldPos`)是唯一空间判据域;禁读表现态位置。
//
// 核心机制:
//   - 单方法谓词:格 ∈ 医馆地块(建筑占地,非家具占用 —— 两口子不同源,
//     硬接家具口径会强迫 23 侧按家具实现地块,2026-10-25 实现轮裁定)
//   - 装配归 Boot(接线轮:接 23/24 的地块源);「同源」义务 = 本谓词、
//     规则十「无人」、`F1PipelineInput.IsInClinic` 三处须绑同一实现
//     (规则十四「一个量」同款纪律 —— 三个实例 = 三处对同一时刻给出不同回答)。
//   - 52 侧零场景知识:本接口是 Sim.Contracts 的纯契约,实现住装配侧。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 「医馆格」判据源 —— 格是否属于医馆地块(规则十二 12.1 的触发判据注入面)。
    /// </summary>
    /// <remarks>
    /// <b>同源义务(2026-10-25 实现轮登记)</b>:本谓词、规则十「无人」判据、
    /// <c>F1PipelineInput.IsInClinic</c> 三处在装配侧必须绑<b>同一</b>实现 ——
    /// 它们是同一个量(「玩家此刻是否在医馆地块内」)的三个消费点,
    /// 两个实例 = 导演对同一 tick 的两问得到不同回答(静默分叉,DC-1 警告同型)。
    /// 装配归 Boot 接线轮;P0 夹具用 stub,不构成「已同源」(禁借绿)。
    /// </remarks>
    public interface IClinicCellSource
    {
        /// <summary>
        /// 判定格是否属于医馆地块(建筑占地 —— 非「该格有家具」的家具占用口径)。
        /// </summary>
        /// <param name="cell">整数格坐标(ADR-015 §三;禁传表现态连续坐标取整)。</param>
        /// <returns>格 ∈ 医馆地块 = true。</returns>
        bool IsClinicCell(WorldPos cell);
    }
}
