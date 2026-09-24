// 权威来源:ADR-014 §一(运行期读烘焙产物)· ADR-025 §①(RecipeSettlementConstants 住 Sim)
//          · Story 008(配方表 + 全局常量表的数据集形状)
//
// ⚠️ 常量表并入**配方侧产物**(item_database_recipes.cooked.bytes):两者同为配方域逻辑数据、
//    同一内容哈希覆盖集,拆成第三份产物无消费方差异(ADVISORY 偏差已在 Story 008 报告登记)。

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>配方表 + 全局结算常量 数据集(烘焙产物 <c>item_database_recipes.cooked.bytes</c> 的解码形)。
    /// <para>加载唯一入口 = <see cref="IDataProvider.Load{TDataSet}"/>;
    /// 失败 = 启动期硬失败(E-13),绝不返回默认值。</para></summary>
    public struct RecipeDataSet
    {
        /// <summary>全量配方(§Schema F 绑定完成、全部门通过后的行集)。</summary>
        public Recipe[] Recipes;

        /// <summary>全局结算常量表(F1/F2 跨条目量;值经烘焙注入,源码零字面量 —— AC-21a-48)。</summary>
        public RecipeSettlementConstants Settlement;

        /// <summary>烘焙时从源 JSON 字节派生的内容版本号(u32;与 ItemDataSet 同一哈希)。</summary>
        public uint ConfigVersion;
    }
}
