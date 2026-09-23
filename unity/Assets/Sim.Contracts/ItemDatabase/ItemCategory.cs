// 权威来源:design/gdd/item-database.md §Schema A(category 行)
//          · Story 002 AC-21a-22(枚举闭合:枚举外字面量 ⇒ 拒)
//
// ⚠️ 作者态 JSON 里本字段是**明文字符串**非 int(D-21-13;int 编码拒收 = AC-21a-26,归 Story 006)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>物品类别。JSON 字面量映射:
    /// <c>material</c> / <c>drug</c> / <c>tool</c> / <c>weapon</c> / <c>build_part</c> / <c>food</c>
    /// (精确大小写 —— 逐字面量解析见 <c>Editor.Tools.Gates.ItemDbValidation.TryParseItemCategory</c>,
    /// AC-21a-22 枚举闭合)。</summary>
    public enum ItemCategory
    {
        /// <summary>原料(JSON: <c>"material"</c>;仅此类可非空 <c>gather_profile</c>)。</summary>
        Material = 0,
        /// <summary>药物(JSON: <c>"drug"</c>;仅此类可非空 <c>drug_profile</c>)。</summary>
        Drug = 1,
        /// <summary>器械(JSON: <c>"tool"</c>)。</summary>
        Tool = 2,
        /// <summary>武器(JSON: <c>"weapon"</c>;仅此类带 <c>inflicts_injury</c>)。</summary>
        Weapon = 3,
        /// <summary>建造件(JSON: <c>"build_part"</c>)。</summary>
        BuildPart = 4,
        /// <summary>食物(JSON: <c>"food"</c>)。</summary>
        Food = 5,
    }
}
