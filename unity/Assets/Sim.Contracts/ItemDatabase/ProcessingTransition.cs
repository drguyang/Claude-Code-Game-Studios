// 权威来源:design/gdd/item-database.md §Schema F(boundary_state[] =「state 对[]」)
//          · §States and Transitions(「枚举只给词汇不给通路」—— 通路是逐条数据)
//
// ⚠️ 物品侧对应字段 legal_transitions 在 GDD 中类型字面为 string[](作者态编码),
//    见 ItemDef.LegalTransitions 注;本型是**已解析的 state 对**形(双方都是 P0 闭集枚举)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>一条合法的炮制状态通路 <c>(From → To)</c>。
    /// 配方的 <c>boundary_state[]</c> 与物品声明的通路均归本型。</summary>
    public readonly struct ProcessingTransition
    {
        /// <summary>起点状态。</summary>
        public readonly ProcessingState From;

        /// <summary>终点状态。</summary>
        public readonly ProcessingState To;

        /// <summary>构造状态对。</summary>
        /// <param name="from">起点。</param>
        /// <param name="to">终点。</param>
        /// <example><c>new ProcessingTransition(ProcessingState.Raw, ProcessingState.Dried)</c>(干燥)</example>
        public ProcessingTransition(ProcessingState from, ProcessingState to)
        {
            From = from;
            To = to;
        }

        /// <summary>诊断用可读形(<c>raw -&gt; dried</c>)。</summary>
        public override string ToString() => $"{From} -> {To}";
    }
}
