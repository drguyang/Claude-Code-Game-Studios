// 权威来源:design/gdd/item-database.md §Schema B(quality_axis 行,D-21-14 / D-21-23)
//          · Story 002 schema 落型(AC-21a-60「P0 只许 half_life」的**校验**归 Story 004,
//            执法体 Editor.Tools.Gates.DrugProfileGates.ValidateP0QualityAxis)。
//
// ⚠️ 四轴枚举**定义**在此(闭集词汇),但 P0 收窄是**数据期断言**不是类型期断言 ——
//    类型允许四个成员编译通过;构建期拒 onset/peak/elimination 的逻辑属 AC-21a-60
//    (执法体住 Editor.Tools.Gates,不进玩家构建)。
// ⚠️ 本故事不提供本枚举的字符串字面量解析器(轴解析属 drug_profile 绑定 = Story 008)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>F5 品级偏移的作用轴(D-21-14:品级只调时间轴,永不调幅值)。
    /// JSON 字面量:<c>half_life</c> / <c>onset</c> / <c>peak</c> / <c>elimination</c>。</summary>
    public enum QualityAxis
    {
        /// <summary>半衰期(JSON: <c>"half_life"</c>)—— P0 唯一有落点的轴(D-21-23)。</summary>
        HalfLife = 0,
        /// <summary>起效(JSON: <c>"onset"</c>)—— P1a(P0 拒 = AC-21a-60,Story 004)。</summary>
        Onset = 1,
        /// <summary>达峰(JSON: <c>"peak"</c>)—— P1a(同上)。</summary>
        Peak = 2,
        /// <summary>消除(JSON: <c>"elimination"</c>)—— P1a(同上)。</summary>
        Elimination = 3,
    }
}
