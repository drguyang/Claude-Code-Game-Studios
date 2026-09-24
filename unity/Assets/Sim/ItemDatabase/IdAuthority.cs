// 权威来源:ADR-010 §五(ItemInstanceId.Next() 机制落点)· ADR-006 Amendment B 机制 A
//          (计数器永不复位 0;迁移后 next = max(id) + 1 由事件流/快照高水位重构)
//          · ADR-009 §Key Interfaces(IIdAuthority 双支)· ADR-007 §四(None = -1 哨兵口径)
//          · D-21-26/27(仅主机铸币;客户端本地铸造 = 迁移后重号 ⇒ 物品悄悄合并/丢失)
//          · Story 010 AC-21a-63(唯一铸造来源 + 高水位重构 + 负哨兵不进物品 id 空间)
//
// 计数器布局(ADR-006 Amendment B 适用面):
//   · 物品实例计数器 `_nextItem` —— 掉落 / 库存 / 建造件共用(ItemInstanceId 头注);
//   · 受伤实体计数器 `_nextPatient` —— 病人 / 敌人 / 玩家共用同一空间(ADR-016 §二 ·
//     第二十六批「不新开第二计数器」= 玩家 id 复用本计数器,非新立第三支)。
// 两支共用**同一机制**(机制 A 三条不变量),是两个**独立空间** —— 与 IIdAuthority 双方法对位。
//
// ⚠️ 主机侧唯一调用方(ADR-005 主机唯一 Append 同源);客户端获取 id 的唯一合法路径 =
//    主机铸造后经事件流下发(D-21-27)。执法体 = Story 010 的 id_authority 静态扫描。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="IIdAuthority"/> 的 P0 实现(机制 A:计数器 + 高水位可重构)。</summary>
    public sealed class IdAuthority : IIdAuthority
    {
        private long _nextItem;
        private int _nextPatient;

        /// <param name="nextItem">物品实例计数器初值(0 = 全新世界;读档路径应改走
        /// <see cref="ReconstructItemHighWater"/>,两者可叠加 —— 构造值只是下限)。</param>
        /// <param name="nextPatient">受伤实体计数器初值(同上;首个合法 id = 0,
        /// <see cref="PatientId.None"/> = -1 属事件侧哨兵不占号)。</param>
        /// <exception cref="ArgumentOutOfRangeException">初值为负(负数不是合法待发号)。</exception>
        public IdAuthority(long nextItem = 0L, int nextPatient = 0)
        {
            if (nextItem < 0L) throw new ArgumentOutOfRangeException(nameof(nextItem), "物品计数器初值不得为负");
            if (nextPatient < 0) throw new ArgumentOutOfRangeException(nameof(nextPatient), "实体计数器初值不得为负");
            _nextItem = nextItem;
            _nextPatient = nextPatient;
        }

        /// <summary>单调发放下一个物品实例 id(0, 1, 2, …)。永不复位(ADR-006 机制 A 不变量①)。</summary>
        public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_nextItem++);

        /// <summary>单调发放下一个受伤实体 id(0, 1, 2, …)。病人 / 敌人 / 玩家共用本空间。</summary>
        public PatientId NextPatientId() => new PatientId(_nextPatient++);

        /// <summary>读档 / 迁移后的物品 id 高水位重构:<c>next = max(next, max(已见) + 1)</c>。
        /// <para>**只升不降** —— 计数器永不复位 0(机制 A 不变量①);已多发的号不回收(不变量②)。</para>
        /// <para><b>负值一律忽略</b>:负 id 是哨兵语义(ADR-007 §四口径),不进物品 id 空间、
        /// 不污染高水位 —— 存档含 -1 不得把 next 拉向 0 方向,也不得被误读为超大无符号值。</para></summary>
        /// <param name="knownItemIds">存档 / 事件流中已见的全部物品实例 id(可含负哨兵)。</param>
        /// <exception cref="OverflowException"><c>max == long.MaxValue</c>(号空间耗尽,硬失败不回绕)。</exception>
        public void ReconstructItemHighWater(IEnumerable<long> knownItemIds)
        {
            if (knownItemIds == null) throw new ArgumentNullException(nameof(knownItemIds));
            long candidate = _nextItem;
            foreach (long id in knownItemIds)
            {
                if (id < 0L) continue;                       // 负哨兵:不进空间、不污染高水位
                if (id == long.MaxValue)
                    throw new OverflowException("物品实例 id 高水位 = long.MaxValue —— 号空间耗尽,拒绝重构(不静默回绕)");
                if (id + 1L > candidate) candidate = id + 1L;
            }
            if (candidate > _nextItem) _nextItem = candidate; // 只升不降
        }

        /// <summary>受伤实体 id 高水位重构(同 <see cref="ReconstructItemHighWater"/> 语义;
        /// <see cref="PatientId.None"/> = -1 同样被忽略)。</summary>
        /// <exception cref="OverflowException"><c>max == int.MaxValue</c>(同上)。</exception>
        public void ReconstructPatientHighWater(IEnumerable<int> knownPatientIds)
        {
            if (knownPatientIds == null) throw new ArgumentNullException(nameof(knownPatientIds));
            int candidate = _nextPatient;
            foreach (int id in knownPatientIds)
            {
                if (id < 0) continue;                        // None 哨兵:ADR-007 §四
                if (id == int.MaxValue)
                    throw new OverflowException("受伤实体 id 高水位 = int.MaxValue —— 号空间耗尽,拒绝重构(不静默回绕)");
                if (id + 1 > candidate) candidate = id + 1;
            }
            if (candidate > _nextPatient) _nextPatient = candidate;
        }
    }
}
