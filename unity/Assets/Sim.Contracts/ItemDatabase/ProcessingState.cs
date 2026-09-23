// 权威来源:design/gdd/item-database.md §Schema A(processing_state 行)
//          · §States and Transitions(全局词汇表:P0 五值 + P1a 两值分界)
//          · Story 002 AC-21a-22(枚举闭合)/ AC-21a-23(P0 拒 P1a,校验归 Story 006)
//
// ⚠️ P0 闭集 = 恰五值。P1a 的 honey_fried / dry_fried **刻意不加**(unity-specialist 约束⑦):
//    枚举字面层 TryParse 天然拒 P1a 字面量 ⇒「P0 数据不得静默放行 P1a」在类型层即成立;
//    P1a 扩枚举属后续故事(届时 append-only,不得重排既有 ordinal —— D-21-13)。
// ⚠️ 作者态 JSON 里本字段是**明文字符串**非 int(D-21-13;int 编码的拒收 = AC-21a-26,归 Story 006)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>炮制状态(全局词汇表)。JSON 字面量映射:
    /// <c>raw</c> / <c>dried</c> / <c>extracted</c> / <c>tincture</c> / <c>pill</c>
    /// (精确大小写 —— 逐字面量解析见 <c>Editor.Tools.Gates.ItemDbValidation.TryParseProcessingState</c>,
    /// AC-21a-22 枚举闭合)。</summary>
    public enum ProcessingState
    {
        /// <summary>生品(JSON: <c>"raw"</c>)。</summary>
        Raw = 0,
        /// <summary>干燥(JSON: <c>"dried"</c>)。</summary>
        Dried = 1,
        /// <summary>提取(JSON: <c>"extracted"</c>)。</summary>
        Extracted = 2,
        /// <summary>浸制(JSON: <c>"tincture"</c>)。</summary>
        Tincture = 3,
        /// <summary>成型(JSON: <c>"pill"</c>)。</summary>
        Pill = 4,
    }
}
