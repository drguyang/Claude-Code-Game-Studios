// 权威来源:design/gdd/item-database.md §Schema A(ItemDef 十三行字段表)
//          · ADR-006 §Decision 一(weight / stack_max = int 计数,非 Fix —— D-21-17)
//          · Story 002(类型落定;写入期校验套件 = Story 006/007;绑定 = Story 008)
//
// ⚠️ 授权载体硬规定(D-21-13):ItemDef 的唯一合法作者态 = assets/data/*.json 文本 ——
//    禁做成 ScriptableObject / prefab / 任何 Unity 序列化承载(本型是纯 BCL 契约,天然满足)。
// ⚠️ stackable 是**派生只读属性**不落数据(AC-21a-59:显式写入数据文件即拒,
//    校验在 Editor.Tools.Gates.ItemDbValidation.FindStoredStackableKeys)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>物品表的一条记录(§Schema A)。复合主键 = <c>(base_id, processing_state)</c>
    /// (见 <see cref="ItemKey"/>;唯一性校验 = AC-21a-21)。
    /// <para>本型是解析后的运行时形;**原始 JSON 键集**的校验(未知键白名单 / stackable 拒收)
    /// 由校验函数单独接键集参数 —— struct 反序列化会丢未知键,两级缝隙见
    /// <c>ItemDbValidation.FindStoredStackableKeys</c> 文档与 Story 002 报告。</para></summary>
    public struct ItemDef
    {
        /// <summary>稳定标识(如 <c>willow_bark</c>);只增不删,废弃条目标 deprecated 不物理删除。</summary>
        public string BaseId { get; set; }

        /// <summary>炮制状态(P0 五值闭集;与 BaseId 合成复合主键)。</summary>
        public ProcessingState ProcessingState { get; set; }

        /// <summary>显示名(归 21 拥有;内容规范见 GDD §命名规范指针)。</summary>
        public string DisplayName { get; set; }

        /// <summary>类别(六值闭集;决定三个 profile 块的非空门)。</summary>
        public ItemCategory Category { get; set; }

        /// <summary>堆叠上限(int ≥ 1 计数,非 Fix —— D-21-17)。越界校验 = AC-21a-15(归 Story 006)。</summary>
        public int StackMax { get; set; }

        /// <summary>可堆叠 —— **派生只读属性,不是字段,不落数据**(<c>= StackMax &gt; 1</c>,§Schema A)。
        /// 显式写入数据文件 = 校验拒收(AC-21a-59)。</summary>
        public bool Stackable => StackMax > 1;

        /// <summary>单件重量(int &gt; 0,**整数最小单位个数,非 Fix** —— D-21-17;守恒律按它求值)。
        /// 数值冻结至 21b 单位考据落定。越界校验 = AC-21a-15(归 Story 006)。</summary>
        public int Weight { get; set; }

        /// <summary>废弃标记(只增不删政策;存档中的实例仍须可解析,AC-21a-35)。</summary>
        public bool Deprecated { get; set; }

        /// <summary>本条目**声明**的合法 <c>processing_state</c> 通路。
        /// ⚠️ GDD 类型字面为 <c>string[]</c>(作者态编码);解析成 state 对的绑定归 Story 008 ——
        /// 「配方 state 对不在 legal_transitions 内」的校验 = AC-21a-24(归 Story 006)。
        /// 必填、可为空数组(§Schema A)。</summary>
        public string[] LegalTransitions { get; set; }

        /// <summary>药物块 —— 仅 <c>category = drug</c> 非空(双向门归 Story 006)。可空。</summary>
        public DrugProfile? DrugProfile { get; set; }

        /// <summary>采集块 —— 仅 <c>category = material</c> 非空(双向门归 Story 006)。可空。</summary>
        public GatherProfile? GatherProfile { get; set; }

        /// <summary>中药块 —— **P0 恒空**(字段表 P1a 补)。可空(P0 恒 null)。</summary>
        public TcmProfile? TcmProfile { get; set; }

        /// <summary>外键 → 9 的 injury_id 集合,**仅 <c>category = weapon</c> 非空**。
        /// ⚠️ 2026-09-17 语义降级(25 · R13/A20):读作「该武器所属线**可产生的伤情集合**」
        /// (约束校验字段,非逐次命中真源);单值/列表的作者态取舍归数据轮,绑定层把单值
        /// 收敛为单元素数组。存在性与类别门 = AC-21a-25(归 Story 006)。可空。</summary>
        public string[] InflictsInjury { get; set; }
    }
}
