// 权威来源:production/epics/audio-system/story-008-spatialization-world-breath.md(AC-44-D8 ② · 优先级公式)
// GDD:design/gdd/audio-system.md :845-846(`cell_jitter = SplitMix64(ActorId, Cell) 派生 u16 偏移`)
// ADR-023 ⑦ / S7 同族(确定性格内偏移)· ADR-015 §三(整数格)
//
// ⚠️ **存在理由**:同格 N 个病人(整数锚点距离**恒等**)在 `rank_key` 的 distance 键上无法分出主次。
//    `cell_jitter` 是**破平键** —— 让「同格多源按距离分别空间化」可断言(AC-44-D8 ②)。
// ⚠️ **只叠渲染坐标,永不回写**(GDD :846 逐字「仅作渲染,不回写」)—— 本类纯函数,
//    无状态、无副作用,不持有任何格坐标。

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>确定性格内偏移(破平键)—— `SplitMix64(ActorId, Cell)` 派生 u16。
    /// <para>同格 N 源的整数锚点距离恒等 ⇒ `rank_key` 的第二键失效;本值给出一个
    /// **确定性**(同输入恒同输出)的第三键,使同格多源可稳定分出主次。</para>
    /// <para>⚠️ 消费纪律:只叠加到**渲染坐标**(如 <see cref="LatticeToWorld"/> 的产物),
    /// **永不回写**逻辑格 —— 回写会破 ADR-015 §一 两层世界纪律。</para></summary>
    public static class CellJitter
    {
        /// <summary>取 (ActorId, Cell) 的确定性格内偏移。</summary>
        /// <param name="actorId">声源实体 id(与 `IIdAuthority` 同空间)。</param>
        /// <param name="cellX">该源所在整数格 X。</param>
        /// <param name="cellY">该源所在整数格 Y。</param>
        /// <param name="cellZ">该源所在整数格 Z。</param>
        /// <returns>u16 偏移(0–65535);同输入恒同输出,不同 ActorId 同格时以压倒性概率不同。</returns>
        /// <remarks>⚠️ 格三轴以 <c>int</c> 承载而非 <c>Int3</c> —— 44 的公开方法签名受 AC-44-B3
        /// 入口白名单约束(`assembly_boundary_test` b3)。同型先例 = <c>PatientCueEmit.Emit</c>
        /// (`cellX/cellY/cellZ` 三轴入参,体内装配)。</remarks>
        public static ushort Compute(int actorId, int cellX, int cellY, int cellZ)
        {
            ulong state = SplitMix64.Hash(actorId, cellX, cellY, cellZ);
            return (ushort)(state & 0xFFFF);
        }
    }
}
