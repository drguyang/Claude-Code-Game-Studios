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

        /// <summary>
        /// 掷骰发生的 tick。
        /// <para>⚠️ **2026-11-03 新增**(ADR-039 后续轮):`W_i = W_base × TODMult × HistoryMult`
        /// 里的 `TODMult` 是**昼夜**的函数,而昼夜在**一天之内会翻转** ⇒ 光有 `Win`(日起点)
        /// 不足以定权重。掷骰发生在某个具体 tick,该值**已可从流重建**
        /// (`EventRolled` 事件的 `SimEvent.Tick` 就是它)⇒ 不引入第四个不可重构输入。</para>
        public readonly long Tick;

        public RollRequest(long win, int tier, int ordinal, long tick)
        {
            Win = win; Tier = tier; Ordinal = ordinal; Tick = tick;
        }

        /// <summary>兼容形(旧三参)。`Tick` 取 `Win` —— **仅供不关心昼夜的夹具**;
        /// 生产必须用四参形,否则 `TODMult` 会按日起点的昼夜算。</summary>
        public RollRequest(long win, int tier, int ordinal)
        {
            Win = win; Tier = tier; Ordinal = ordinal; Tick = win;
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
