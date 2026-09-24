// 权威来源:Story 010 AC-21a-35(废弃条目仍可解析;物理删除 = 存档损坏,对照组须失败)
//          · GDD item-database §Schema A(只增不删政策;deprecated 标记不物理删除)
//          · ADR-010(读档解析侧的落点)
//
// 「解析」= 把存档实例的 item_key (base_id, state) 对回当前物品表:
//   · 命中(含 deprecated=true)⇒ 返回条目 —— 废弃只是标记,存档实例照常完整可读;
//   · 未命中 ⇒ 显式 throw —— 条目被物理删除 = 违反只增不删,存档**应当**损坏(硬失败),
//     不得静默跳过 / 不得降级成空条目(静默丢 = 物品凭空消失的静默失败)。
//
// ⚠️ 本函数**不读 quality / qty 的合法性**(那是写入期门 Story 006/007 的事);
//    也**不触发 F1 重算**(Schema E:qty 物化存储不重算 —— 解析只做键匹配)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>存档实例 → 物品表条目的解析(AC-21a-35 只增不删的读侧执法体)。</summary>
    public static class InstanceResolver
    {
        /// <summary>按复合主键解析实例的物品条目。deprecated 条目正常返回;缺失条目硬失败。</summary>
        /// <param name="instance">存档读出的实例。</param>
        /// <param name="data">当前物品表(烘焙产物解码形)。</param>
        /// <returns>命中的 <see cref="ItemDef"/>(可能 <c>Deprecated == true</c>)。</returns>
        /// <exception cref="InvalidOperationException">条目不存在 —— 物理删除违反只增不删政策,
        /// 该存档已损坏(AC-21a-35 对照组的预期失败)。</exception>
        /// <exception cref="ArgumentNullException">data.Items 为 null(未初始化数据集)。</exception>
        public static ItemDef Resolve(in ItemInstance instance, in ItemDataSet data)
        {
            if (data.Items == null)
                throw new ArgumentNullException(nameof(data), "物品表未初始化(Items == null)—— 启动期硬失败,不静默降级");

            ItemKey key = instance.Key;
            for (int i = 0; i < data.Items.Length; i++)
            {
                ItemDef def = data.Items[i];
                if (def.ProcessingState == key.State &&
                    string.Equals(def.BaseId, key.BaseId, StringComparison.Ordinal))
                {
                    return def;   // Deprecated 不影响解析(只增不删;表现/堆叠语义归 20)
                }
            }

            throw new InvalidOperationException(
                $"[存档解析] 实例 {instance.InstanceId} 引用的物品条目 {key} 不存在 —— " +
                "物品表政策 = 只增不删(废弃标 deprecated,不物理删除);" +
                "条目被物理删除即存档损坏,硬失败不静默丢(AC-21a-35)");
        }
    }
}
