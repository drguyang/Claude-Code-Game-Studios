// 权威来源:design/gdd/item-database.md §Schema F(Recipe 八行字段表 + D-21-15 / D-21-30)
//          · TR-itemdb-007(配方 schema:输入/产出/工时 —— ◆ 归属件 = GDD 自身)
//
// ⚠️ inputs[].qty / outputs[].qty = **基数**,非实耗(D-21-15:实耗 = Ceil(qty/EFF),运行期派生,
//    只随该次 Craft 历史事件走 —— 实耗冻进静态数据 = 技能一变即与重算不符,AC-21a-52)。
// ⚠️ owner 是配方子集划分的唯一显式判据(D-21-30);缺字段装载硬失败 = AC-21a-66(归 Story 007)。
// ⚠️ skill_gate 的 [0, SKILL_CAP] 区间校验 = AC-21a-19(归 Story 006);SKILL_CAP 引用自 30,
//    本系统只存 int,不在此硬编码常量(AC-21a-48 纪律)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>配方的一条投入或产出项 <c>{item_key, qty&gt;0}</c>(§Schema F)。</summary>
    public readonly struct RecipeEntry
    {
        /// <summary>物品复合主键(外键 → 物品表;悬空拒收 = AC-21a-17,归 Story 006)。</summary>
        public readonly ItemKey Key;

        /// <summary><c>qty &gt; 0</c> —— 投入侧是**消耗基数**,产出侧是**产出基数**
        /// (逐条即 F1 的 <c>outputs_i.qty</c>;零/负 qty 拒收 = AC-21a-7/16,归 Story 006)。</summary>
        public readonly int Qty;

        /// <summary>构造配方项。</summary>
        /// <param name="key">物品复合主键。</param>
        /// <param name="qty">基数(&gt; 0;实耗是运行期派生量,不落此)。</param>
        /// <example><c>new RecipeEntry(new ItemKey("willow_bark", ProcessingState.Raw), 3)</c></example>
        public RecipeEntry(ItemKey key, int qty)
        {
            Key = key;
            Qty = qty;
        }
    }

    /// <summary>配方表的一条(§Schema F)。同一配方可被多个系统读取 —— 差异只在 <c>owner</c>(D-21-30)。
    /// 跨 base 转化合法(柳树皮 → 水杨酸是 P0 核心形态,§Edge Cases)。</summary>
    public struct Recipe
    {
        /// <summary>稳定标识。</summary>
        public string RecipeId { get; set; }

        /// <summary>配方归属系统(process / craft / build)—— 子集划分唯一判据(D-21-30)。</summary>
        public RecipeOwner Owner { get; set; }

        /// <summary>投入项(基数;<b>非空</b> —— 空输入 = 凭空造物)。非空性校验归 Story 006/007。</summary>
        public RecipeEntry[] Inputs { get; set; }

        /// <summary>产出项(基数;<b>非空</b> —— 销毁不归配方表)。逐条即 F1 的 <c>outputs_i.qty</c>。</summary>
        public RecipeEntry[] Outputs { get; set; }

        /// <summary>工时 —— 逻辑 tick 数(int &gt; 0;<b>单位是 tick 不是秒</b>,TICK_SECONDS 量纲归
        /// <c>entities.yaml</c> 非本系统所有)。≤0 拒收 = AC-21a-18(归 Story 006)。</summary>
        public int DurationTicks { get; set; }

        /// <summary>技能门槛(int ∈ [0, SKILL_CAP];低于此不可发起)。区间校验 = AC-21a-19(归 Story 006)。
        /// <c>SKILL_CAP</c> 引用自 30 技能系统,本系统不拥有该常量。</summary>
        public int SkillGate { get; set; }

        /// <summary>准入闸 —— 输入品级下限(int ∈ [1, MAX_QUALITY];**非品级出口** —— 出口见 F5)。
        /// 区间校验 = AC-21a-20(归 Story 006)。</summary>
        public int MinQuality { get; set; }

        /// <summary>本配方声明的合法 state 通路(解析后的 state 对形;
        /// 与物品 <c>legal_transitions</c> 的符合性校验 = AC-21a-24,归 Story 006)。</summary>
        public ProcessingTransition[] BoundaryState { get; set; }
    }
}
