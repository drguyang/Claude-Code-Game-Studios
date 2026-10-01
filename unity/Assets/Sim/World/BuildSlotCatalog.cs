// 权威来源:ADR-015 §五(模块化网格)· ADR-022 §三(world_buildslots.json 导出契约)
//             modular-building.md 规则一(槽位骨架与模块目录)
//
// 核心机制:
//   - BuildSlotCatalog: 装载烘焙槽位骨架(派生态,加载期重建)
//   - BuildSlot: 单槽位(位置 + 允许的模块类型集合)
//   - SlotType: 槽位类型枚举(床 / 台 / 柜 / 圃 …)
//   - 全部整数域,无 float

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>槽位类型枚举(模块目录对齐)。</summary>
    public enum SlotType : int
    {
        None    = 0,  // 无效 / 未分配
        Bed     = 1,  // 床位类
        Table   = 2,  // 工作台类(手术台 / 诊桌)
        Cabinet = 3,  // 柜子类(药柜 / 存储)
        Plot    = 4,  // 种植圃类
        Decor   = 5,  // 装饰类(不影响数值)
        Shell   = 6   // 外壳类(墙 / 地基 —— P0 不可拆除)
    }

    /// <summary>单槽位定义(整数坐标 + 允许的模块类型)。</summary>
    public readonly struct BuildSlot
    {
        public readonly WorldPos Position;      // 锚点格(整数)
        public readonly int AllowedTypes;       // 允许的 SlotType 位掩码

        public BuildSlot(WorldPos position, int allowedTypes)
        {
            Position = position;
            AllowedTypes = allowedTypes;
        }

        /// <summary>检查指定类型是否允许。</summary>
        public bool Allows(SlotType type) => (AllowedTypes & (1 << (int)type)) != 0;
    }

    /// <summary>槽位骨架目录(装载自烘焙数据)。</summary>
    public sealed class BuildSlotCatalog
    {
        private readonly Dictionary<WorldPos, BuildSlot> _slots = new Dictionary<WorldPos, BuildSlot>();
        private BitMask _regionMask = new BitMask(0); // 骨架区域的位掩码(简化版:只记录是否有槽位)

        public int Count => _slots.Count;

        /// <summary>注册单槽位(装载期调用)。</summary>
        public void RegisterSlot(BuildSlot slot)
        {
            _slots[slot.Position] = slot;
        }

        /// <summary>查询槽位(不存在返回 false)。</summary>
        public bool TryGetSlot(WorldPos position, out BuildSlot slot)
        {
            return _slots.TryGetValue(position, out slot);
        }

        /// <summary>检查位置是否为有效槽位。</summary>
        public bool IsValidSlot(WorldPos position)
        {
            return _slots.ContainsKey(position);
        }

        /// <summary>获取指定位置的允许类型(无槽位返回0)。</summary>
        public int GetAllowedTypes(WorldPos position)
        {
            return _slots.TryGetValue(position, out var slot) ? slot.AllowedTypes : 0;
        }

        /// <summary>获取所有槽位位置(用于迭代)。</summary>
        public IReadOnlyCollection<WorldPos> GetAllSlotPositions()
        {
            return _slots.Keys;
        }

        /// <summary>检查位置是否在骨架区域内(简化版:有槽位即区域内)。</summary>
        public bool IsInBuildSlotRegion(WorldPos position)
        {
            return _slots.ContainsKey(position);
        }
    }

    /// <summary>位掩码类型(用于 SlotType 集合)。</summary>
    public readonly struct BitMask : IEquatable<BitMask>
    {
        public readonly int Value;

        public BitMask(int value) => Value = value;

        public bool Equals(BitMask other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BitMask m && Equals(m);
        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(BitMask a, BitMask b) => a.Equals(b);
        public static bool operator !=(BitMask a, BitMask b) => !a.Equals(b);

        public static BitMask operator |(BitMask a, BitMask b) => new BitMask(a.Value | b.Value);
        public static BitMask operator &(BitMask a, BitMask b) => new BitMask(a.Value & b.Value);

        /// <summary>创建单类型掩码。</summary>
        public static BitMask Single(SlotType type) => new BitMask(1 << (int)type);
    }
}
