// 权威来源:ADR-007 §Key Interfaces(:231-243)· §三(掷骰输入必须可从事件流重构)
//
// 逐字段整数域(ADR-006 §五)。RollRequest 三个输入 + EventRollResult 的 ChosenKey
// 共同构成「骰子可重构」的最小闭包:EventRolled 落流时把这几项全记下来(ADR-007 §三)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>一次掷骰请求:哪个窗口、哪一档、该档第几次。</summary>
    public readonly struct RollRequest
    {
        /// <summary>窗口起点 tick。</summary>
        public readonly long Win;

        /// <summary>档 id。</summary>
        public readonly int Tier;

        /// <summary>本窗口本档第 n 次(区分同窗重复掷骰)。</summary>
        public readonly int Ordinal;

        public RollRequest(long win, int tier, int ordinal)
        {
            Win = win; Tier = tier; Ordinal = ordinal;
        }
    }

    /// <summary>掷骰结果:回显输入三元组 + 命中的池条目 key(全整数,可落流可重放)。</summary>
    public readonly struct EventRollResult
    {
        public readonly long Win;
        public readonly int Tier;
        public readonly int Ordinal;

        /// <summary>池条目 key(事件表的稳定标识)。</summary>
        public readonly int ChosenKey;

        public EventRollResult(long win, int tier, int ordinal, int chosenKey)
        {
            Win = win; Tier = tier; Ordinal = ordinal; ChosenKey = chosenKey;
        }
    }
}
