// 权威来源:ADR-006 §Decision 三(:178)· §Decision 一(:114 签名)
//
// ⚠️ ADR-006 内部两处口径需实现轮对齐:
//   · :114 把 mode 写成 Parse 的**带默认值参数**(`= RoundMode.HalfAwayFromZero`);
//   · :178 却明写「RoundMode 是**全局常量**,不是逐调用点参数 —— 逐调用点会让两处公式不一致而无人察觉」。
// 本文件**两条都落地**:枚举只有 HalfAwayFromZero 一个成员 ⇒ 即使有人传参也传不出别的模式,
// 从而把 :178 的「全局唯一」用类型系统而非纪律钉死。:114 的签名面因此保持可编译。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>全案唯一舍入模式(ADR-006 §Decision 三)。**禁** Math.Round 默认的 ties-to-even。</summary>
    public enum RoundMode
    {
        /// <summary>ROUND_HALF_AWAY_FROM_ZERO —— 中点远离零。全案唯一合法值。</summary>
        HalfAwayFromZero = 0,
    }
}
