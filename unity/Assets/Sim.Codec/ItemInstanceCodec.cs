// 权威来源:Story 010 AC-21a-31(实例经全二进制 codec 往返,item_key/quality 值级相等;
//          qty 物化不重算 —— 本 codec 对 qty 只搬运不修改)· TR-itemdb-023(物品数据在存档中的编码)
//          · ADR-010 §一(全二进制 codec,禁 JSON / PlayerPrefs;按字段名 tag 编码禁位置直写)
//          · ADR-006 §五(tag 化:字段重排不改字节面 / 不静默错位)· Schema E(children = 叶子 id 列表)
//
// 与事件载荷 codec(PayloadCodec.*)的分界:ItemInstance 是**快照/存档实体**不是 SimEvent 载荷 ——
// ADR-010 存档布局 = 头 + 三流 + 快照,本 codec 承载快照段的逐实例编码;三流载荷仍归 PayloadCodec。
//
// tag 分配 = ItemInstance 字段声明序(同 PayloadCodec「tag = 声明序」纪律):
//   1 Id · 2 ItemKey.BaseId(UTF-8)· 3 ItemKey.State(枚举 ordinal)· 4 Quality · 5 Qty · 6 Children[]
// 严格性 = 全六 tag 恰各一(缺/重/未知 ⇒ 抛)+ State ∈ 枚举闭集 + Children 非 null;
// 跨字段约束:无(Schema E 的「children 只能是叶子」是闭包结构校验,归 ContainerClosure.Validate,
// 单实例编码层判不了 —— 分层不越权)。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>存档快照段 <see cref="ItemInstance"/> 的全二进制编解码(AC-21a-31)。</summary>
    public static class ItemInstanceCodec
    {
        private const byte TagId = 1;
        private const byte TagBaseId = 2;
        private const byte TagState = 3;
        private const byte TagQuality = 4;
        private const byte TagQty = 5;
        private const byte TagChildren = 6;
        private const uint ExpectedMask = (1u << 6) - 1;

        /// <summary>写出单实例(按 tag 升序 canonical)。<c>Children == null</c> 拒收 ——
        /// 空容器须是空数组(AC-31 边例:空 children = [],非 null),坏数据不进字节面。</summary>
        public static byte[] Encode(in ItemInstance instance)
        {
            if (instance.Children == null)
                throw new ArgumentException(
                    "ItemInstance.Children 不得为 null(空容器 = 空数组 —— AC-21a-31 边例)", nameof(instance));
            if (instance.Key.BaseId == null)
                throw new ArgumentException("ItemInstance.Key.BaseId 不得为 null(复合主键半边)", nameof(instance));

            var w = new CodecWriter();
            w.WriteFieldInt64(TagId, instance.InstanceId);
            w.WriteFieldUtf8(TagBaseId, instance.Key.BaseId);
            w.WriteFieldInt32(TagState, (int)instance.Key.State);
            w.WriteFieldInt32(TagQuality, instance.Quality);
            w.WriteFieldInt32(TagQty, instance.Qty);
            w.WriteFieldArrayInt64(TagChildren, instance.Children);
            return w.ToArray();
        }

        /// <summary>严格解码:全六 tag 恰各一、State ∈ 闭集、段恰好读尽。qty 原样读出(**不重算**,
        /// Schema E 物化存储 —— 读档路径禁止触碰求解器)。</summary>
        /// <exception cref="InvalidDataException">缺 / 重 / 未知 tag、State 越闭集、段未读尽。</exception>
        public static ItemInstance Decode(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);

            long id = 0;
            string baseId = null;
            int stateRaw = 0, quality = 0, qty = 0;
            long[] children = null;
            uint seen = 0;

            while (r.HasMore)
            {
                byte tag = r.ReadTag();
                if (tag < 1 || tag > 6)
                    throw new InvalidDataException($"ItemInstance 未知字段 tag={tag}");

                uint bit = 1u << (tag - 1);
                if ((seen & bit) != 0)
                    throw new InvalidDataException($"ItemInstance 字段 tag={tag} 重复");
                seen |= bit;

                switch (tag)
                {
                    case TagId:       id = r.ReadInt64LittleEndian(); break;
                    case TagBaseId:   baseId = r.ReadFieldUtf8(); break;
                    case TagState:    stateRaw = r.ReadInt32LittleEndian(); break;
                    case TagQuality:  quality = r.ReadInt32LittleEndian(); break;
                    case TagQty:      qty = r.ReadInt32LittleEndian(); break;
                    case TagChildren: children = r.ReadFieldArrayInt64(); break;
                }
            }

            if (seen != ExpectedMask)
                throw new InvalidDataException(
                    $"ItemInstance 缺字段(已见 mask=0x{seen:X},应为 0x{ExpectedMask:X})");
            r.EnsureFullyConsumed();

            if (!Enum.IsDefined(typeof(ProcessingState), stateRaw))
                throw new InvalidDataException($"ItemInstance.State={stateRaw} 不在 P0 五值闭集内");
            if (baseId == null)
                throw new InvalidDataException("ItemInstance.BaseId 缺失(空段 ≠ 缺字段,须恰一 tag)");
            if (children == null)
                throw new InvalidDataException("ItemInstance.Children 未读出 —— 不得以 null 顶替空数组");

            return new ItemInstance(
                id, new ItemKey(baseId, (ProcessingState)stateRaw), quality, qty, children);
        }
    }
}
