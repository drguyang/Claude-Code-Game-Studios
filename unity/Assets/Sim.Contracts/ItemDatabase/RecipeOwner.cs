// 权威来源:design/gdd/item-database.md §Schema F(owner 行,D-21-30,2026-09-19 已裁落盘)
//          · Story 002 schema 落型;子集三分的装载校验(缺 owner 即硬失败)= AC-21a-66,归 Story 007。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>配方归属系统(D-21-30:子集划分的唯一显式判据,不靠 processing_state 约定)。
    /// JSON 字面量映射:<c>process</c>(18 炮制)/ <c>craft</c>(19 制作)/ <c>build</c>(23 建造)
    /// (精确大小写 —— 逐字面量解析见 <c>Editor.Tools.Gates.ItemDbValidation.TryParseRecipeOwner</c>)。</summary>
    public enum RecipeOwner
    {
        /// <summary>炮制子集(JSON: <c>"process"</c>,18 发起)。</summary>
        Process = 0,
        /// <summary>制作子集(JSON: <c>"craft"</c>,19 发起)。</summary>
        Craft = 1,
        /// <summary>建造子集(JSON: <c>"build"</c>,23 发起)。</summary>
        Build = 2,
    }
}
