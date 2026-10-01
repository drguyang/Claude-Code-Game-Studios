// 权威来源:design/gdd/item-database.md §Schema C(gather_profile 五行)+ §Schema B2(D-21-16 quality_character)
//          · TR-itemdb-017(21a ↔ 17 采集的数据边界:本块是 17 的只读输入契约)

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary><c>gather_profile</c> 块(扩 17 采集)。仅 <c>category = material</c> 非空
    /// (反向校验归 Story 006)。TR-itemdb-017:本块是 21a → 17 的数据边界。</summary>
    public struct GatherProfile
    {
        /// <summary>产地生态区(外键 → 6 世界与生态区的生态区 id)。</summary>
        public string Ecosystem { get; set; }

        /// <summary>可采部位。</summary>
        public string[] Parts { get; set; }

        /// <summary>品级分布(形状由 17 定 —— 见 <see cref="QualityDistribution"/>)。</summary>
        public QualityDistribution QualityDistribution { get; set; }

        /// <summary>单次采量基数(21a 给基数,17 给动作)。int &gt; 0;越界校验归 Story 006。</summary>
        public int QtyPerNode { get; set; }

        /// <summary>原料侧品级定性修饰(D-21-16,长度 = <c>MAX_QUALITY</c>;
        /// **2026-09-25 R13 = 甲**:<c>MAX_QUALITY &gt; 1</c> 时最小非空(原「P0 可空」废止,
        /// C# 属性本身仍可 null —— 必填由烘焙门执行);
        /// 42 以外观/药签呈现,禁数字刻度 U-1/U-2)。长度/非空校验 = AC-21a-50b
        /// (执法体 <c>Editor.Tools.Gates.DrugProfileGates.ValidateGatherQualityCharacterLength</c>,Story 004)。</summary>
        public string[] QualityCharacter { get; set; }
    }
}
