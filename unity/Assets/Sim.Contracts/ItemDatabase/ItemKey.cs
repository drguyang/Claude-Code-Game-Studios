// 权威来源:design/gdd/item-database.md §Schema A / §Schema E(item_key = (base_id, processing_state))
//          · §Edge Cases「两个不同 base_id 拥有同名 processing_state = 完全正常 —— (base_id, state) 才是主键」
//          · Story 002 AC-21a-21(复合主键唯一性;校验函数在 Editor.Tools.Gates.ItemDbValidation)
//
// ⚠️ base_id 比较 = 序数(大小写敏感)—— 稳定标识的字面量语义照 GDD;
//    「大小写不同字面量」对**枚举字段**归 AC-21a-22 拒,对 base_id 是两个不同键(命名规范归 21b)。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>物品复合主键 <c>(base_id, processing_state)</c>。物品表的唯一性单位;
    /// <c>ItemInstance.item_key</c> 与配方 inputs/outputs 的外键均用本型。</summary>
    public readonly struct ItemKey : IEquatable<ItemKey>
    {
        /// <summary>稳定标识(如 <c>willow_bark</c>);只增不删(§Schema A)。序数比较,大小写敏感。</summary>
        public readonly string BaseId;

        /// <summary>炮制状态(P0 五值闭集)。</summary>
        public readonly ProcessingState State;

        /// <summary>构造复合主键。</summary>
        /// <param name="baseId">稳定标识(序数比较;字段非空校验归 Story 006)。</param>
        /// <param name="state">炮制状态。</param>
        /// <example><c>new ItemKey("willow_bark", ProcessingState.Raw)</c></example>
        public ItemKey(string baseId, ProcessingState state)
        {
            BaseId = baseId;
            State = state;
        }

        /// <summary>序数逐位相等(base_id 大小写敏感 + 状态相等)。</summary>
        public bool Equals(ItemKey other) =>
            State == other.State &&
            string.Equals(BaseId, other.BaseId, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is ItemKey other && Equals(other);

        /// <summary>与 <see cref="Equals(ItemKey)"/> 同一相等性口径(可进 Dictionary/HashSet 键)。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)State;
                hash = (hash * 397) ^ (BaseId != null ? StringComparer.Ordinal.GetHashCode(BaseId) : 0);
                return hash;
            }
        }

        /// <summary>相等运算(同 <see cref="Equals(ItemKey)"/>)。</summary>
        public static bool operator ==(ItemKey left, ItemKey right) => left.Equals(right);

        /// <summary>不等运算。</summary>
        public static bool operator !=(ItemKey left, ItemKey right) => !left.Equals(right);

        /// <summary>诊断用可读形(非 JSON 序列化形 —— 落盘编码归 Story 008/010)。</summary>
        public override string ToString() => $"({BaseId}, {State})";
    }
}
