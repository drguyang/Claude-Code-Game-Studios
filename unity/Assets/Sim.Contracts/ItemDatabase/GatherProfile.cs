// 权威来源:design/gdd/item-database.md §Schema C(gather_profile 五行)+ §Schema B2(D-21-16 quality_character)
//          · TR-itemdb-017(21a ↔ 17 采集的数据边界:本块是 17 的只读输入契约)

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary><c>quality_distribution</c> 块占位 —— **逐字段形状由 17 采集系统的 GDD 定**
    /// (§Schema C 明文「形状由 17 定」)。本故事只登记类型名与挂点,不发明字段
    /// (GDD 纪律:「本节之外出现的字段一律视为未定义」)。支撑 ⊆ [1, MAX_QUALITY] 的
    /// 校验 = AC-21a-14(归 Story 006)。</summary>
    public readonly struct QualityDistribution
    {
        // P0 占位:字段表由 17 的 GDD 落定后回填本型;空 struct 合法且不携带任何未定义字段。
    }

    /// <summary><c>gather_profile</c> 块(扩 17 采集)。仅 <c>category = material</c> 非空
    /// (反向校验归 Story 006)。TR-itemdb-017:本块是 21a → 17 的数据边界。</summary>
    public struct GatherProfile
    {
        /// <summary>产地生态区(外键 → 6 世界与生态区的生态区 id)。</summary>
        public string Ecosystem { get; set; }

        /// <summary>可采部位。</summary>
        public string[] Parts { get; set; }

        /// <summary>品级分布(形状由 17 定 —— 见 <see cref="QualityDistribution"/> 占位注)。</summary>
        public QualityDistribution QualityDistribution { get; set; }

        /// <summary>单次采量基数(21a 给基数,17 给动作)。int &gt; 0;越界校验归 Story 006。</summary>
        public int QtyPerNode { get; set; }

        /// <summary>原料侧品级定性修饰(D-21-16,长度 = <c>MAX_QUALITY</c>,P0 可空;
        /// 42 以外观/药签呈现,禁数字刻度 U-1/U-2)。长度校验 = AC-21a-50b
        /// (执法体 <c>Editor.Tools.Gates.DrugProfileGates.ValidateGatherQualityCharacterLength</c>,Story 004)。</summary>
        public string[] QualityCharacter { get; set; }
    }
}
