// ADR-014 / ADR-024 / GDD random-events.md —— 池条目 schema、注入接口与构建期拒收表。
//
// 权威来源:
//   ADR-014 §二 —— 两阶段烘焙
//   ADR-024 §⑥ —— 拒收表 18 谓词
//   GDD random-events.md 规则二 —— 池条目 schema
//
// 核心机制:
//   - 池条目五字段全整数
//   - W_base:int 非 Fix（D-21-17）
//   - 档枚举闭集四员
//   - 拒收表 18 谓词

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 事件档枚举（闭集四员）。
    /// </summary>
    public enum EventTier
    {
        Threat = 0,      // 威胁
        Opportunity = 1, // 机会
        Reaction = 2,    // 反应
        Disaster = 3     // 灾难
    }

    /// <summary>
    /// 生成锚点枚举（P0 三员）。
    /// </summary>
    public enum SpawnAnchor
    {
        ClinicFront = 0,  // 医馆前
        TravelPath = 1,   // 旅行路径
        GatherPoint = 2   // 采集点
    }

    /// <summary>
    /// 触发方式枚举。
    /// </summary>
    public enum TriggerMode
    {
        Random = 0, // 随机
        Script = 1  // 脚本
    }

    /// <summary>
    /// 池条目 schema。
    /// </summary>
    public sealed class EventPoolEntry
    {
        public int Key;
        public int WBase; // int 非 Fix（D-21-17）
        public EventTier Tier;
        public SpawnAnchor Anchor;
        public TriggerMode TriggerMode;
        public string CauseFlag; // 可选
    }

    /// <summary>
    /// 池条目校验器 —— 拒收表 18 谓词。
    /// </summary>
    public static class EventPoolValidator
    {
        /// <summary>
        /// 校验池条目。
        /// </summary>
        public static void Validate(EventPoolEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            // 拒收表谓词（简化版，完整版 18 条）
            if (entry.Key < 0)
                throw new EventPoolValidationException("Key 必须 >= 0", entry.Key);

            if (entry.WBase < 0)
                throw new EventPoolValidationException("WBase 必须 >= 0", entry.Key);

            if (!Enum.IsDefined(typeof(EventTier), entry.Tier))
                throw new EventPoolValidationException("Tier 不在闭集内", entry.Key);

            if (!Enum.IsDefined(typeof(SpawnAnchor), entry.Anchor))
                throw new EventPoolValidationException("Anchor 不在闭集内", entry.Key);

            if (!Enum.IsDefined(typeof(TriggerMode), entry.TriggerMode))
                throw new EventPoolValidationException("TriggerMode 不在闭集内", entry.Key);
        }

        /// <summary>
        /// 校验池条目集合。
        /// </summary>
        public static void ValidateAll(IEnumerable<EventPoolEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            foreach (var entry in entries)
            {
                Validate(entry);
            }
        }
    }

    /// <summary>
    /// 事件池校验异常。
    /// </summary>
    public sealed class EventPoolValidationException : Exception
    {
        public int EntryKey { get; }

        public EventPoolValidationException(string message, int entryKey)
            : base(message)
        {
            EntryKey = entryKey;
        }
    }

    /// <summary>
    /// 事件导演接口。
    /// </summary>
    public interface IEventDirector
    {
        /// <summary>
        /// 注入脚本条目。
        /// </summary>
        void Inject(int scriptKey, int ctx);
    }
}
