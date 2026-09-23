// 权威来源:design/gdd/item-database.md §Schema E(ItemInstance 闭集纯 POD)
//          · Story 002 AC-21a-27(递归类型图静态断言:零 UnityEngine 字段 —— 扫描器在
//            Editor.Tools.Gates.PodTypeScanner;违例夹具 invalid_instance_unity_ref.cs 在测试程序集)
//
// ⚠️ 零 UnityEngine 字段是**闭集契约**:Unity 序列化乐意把 SO/Sprite/GameObject 引用存成引用,
//    会静默违反「存快照不存引用」(§Schema E)。
// ⚠️ D-21-18(归 Story 010):实例若日后增 Fix 字段,禁经 Unity 序列化器,须自定义编码器。
// ⚠️ instance_id 用裸 long(GDD 字面形)。强类型 ItemInstanceId 已住本程序集,
//    与 7a 快照 / D-21-26 权威的接线归后续故事 —— 本故事不改 GDD 字面。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>运行时物品实例(§Schema E:闭集纯 POD)。
    /// <c>{ instance_id:long, item_key, quality:int, qty:int, children:long[] }</c> —— 恰五字段,零引擎引用。</summary>
    public readonly struct ItemInstance
    {
        /// <summary>实例 id(由 <c>IIdAuthority</c> 单调发放,ADR-010 §五;必须入快照,否则重复写入无法去重)。</summary>
        public readonly long InstanceId;

        /// <summary>物品复合主键(外键 → 物品表)。</summary>
        public readonly ItemKey Key;

        /// <summary>品级(int ∈ [1, MAX_QUALITY] 的档位;容器实例恒 1 —— §Schema E)。
        /// 必须入快照。</summary>
        public readonly int Quality;

        /// <summary>数量(物化存储,不在读档时重算;容器实例恒 1 —— §Schema E)。必须入快照。</summary>
        public readonly int Qty;

        /// <summary>子实例 id 列表(容器用「子实例 id 列表」表达,**不得嵌套实例** —— 防递归引用图)。
        /// **非容器 = 空数组,不是 null**(禁可空,减一个静默状态);容器图闭环校验归 7a(AC-21a-58 族)。</summary>
        public readonly long[] Children;

        /// <summary>构造实例。<paramref name="children"/> 为 null 时抛
        /// <see cref="ArgumentNullException"/> —— 空数组语义须由调用方显式给出(<c>Array.Empty&lt;long&gt;()</c>),
        /// 不做静默兜底(§Schema E「非空禁 null」)。</summary>
        /// <param name="instanceId">实例 id(<c>IIdAuthority</c> 发放)。</param>
        /// <param name="key">物品复合主键。</param>
        /// <param name="quality">品级档位(容器恒 1)。</param>
        /// <param name="qty">数量(容器恒 1)。</param>
        /// <param name="children">子实例 id 列表;非容器传 <c>Array.Empty&lt;long&gt;()</c>。</param>
        /// <example><c>new ItemInstance(42L, new ItemKey("willow_bark", ProcessingState.Raw), 1, 5,
        /// Array.Empty&lt;long&gt;())</c></example>
        public ItemInstance(long instanceId, ItemKey key, int quality, int qty, long[] children)
        {
            if (children == null)
                throw new ArgumentNullException(nameof(children),
                    "children 非空禁 null —— 非容器实例传 Array.Empty<long>()(§Schema E)");

            InstanceId = instanceId;
            Key = key;
            Quality = quality;
            Qty = qty;
            Children = children;
        }
    }
}
