// 权威来源:GDD design/gdd/random-events.md 规则二(池条目 schema)· 规则十一(P0 逐档内容)
//          · F1 步骤②(`W_i = W_base_i × TODMult_i × HistoryMult_i`)· ADR-014 §三(阶段 2 绑定)
//          · ADR-039(运行期消费)· DC-2 / D-21-17(`W_base` 是 int 不是 Fix)
//
// ⚠️ **本产物是「池条目」,不是「医疗内容」** —— 承 GDD `:160` 的裁定:
//    52 拥有**池条目与调度**,37 拥有**医疗内容**。故本件里没有病种、没有敌人类型,
//    只有 key / 权重 / 档 / 锚点 / 触发方式 / 处理方 / 因果标记。
//
// ⚠️ **`Handler` 是 52 与内容系统之间的唯一接缝**(2026-11-03 新增,本件就地裁定):
//    · `Encounter` = 27 敌人 AI(兵痞 / 野兽)
//    · `Medical`   = 37 病例系统(急召出诊 / 药材短缺 / 半路拦你求救的村民)
//    52 **不决定来了是谁**,只决定「来不来、来几条、来哪一档」;`Handler` 只回答
//    「该问谁要内容」。⚠️ 两值的**具体落地**(27 的遭遇入口 / 37 的 event_key → 病种映射)
//    在各自系统侧,本件只持有这个枚举。
//
// ⚠️ 权重口径:`WBase` 是 **int(最小单位个数)**,不是 `Fix`(DC-2 / D-21-17)。
//    2026-11-03 用户裁定:P0 九条全部 = 100。
//
// ⚠️ 昼夜乘子:`NightThreatMult` = 3/2(仅**威胁档**逐条生效;其余档恒 1,AC-52-45)。
//    硬上界 `TodMultMax` = 2/1 —— 构建期拒收超上界(GDD Tuning Knobs `:1157`)。
//
// ⚠️ **本件零 `UnityEngine` 引用**(Sim.Contracts 的 `noEngineReferences` 不变)——
//    纯数据载体 + 一个枚举,EditMode 可直驱。

using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>池条目的处理方 —— 「该问谁要内容」(52 不持有内容)。</summary>
    public enum PoolHandler : byte
    {
        /// <summary>27 敌人 AI(人形遭遇 / 野兽)。</summary>
        Encounter = 0,

        /// <summary>37 病例系统(医疗事件模板)。</summary>
        Medical = 1,
    }

    /// <summary>一条池条目(烘焙产物的解码形;字段序 = 作者态 JSON 的字段序)。</summary>
    public readonly struct EventPoolEntryData
    {
        /// <summary>条目主键(整数 —— 抽取的确定性依赖整数比较)。</summary>
        public readonly int Key;

        /// <summary>基础权重(int,非 Fix)。</summary>
        public readonly int WBase;

        /// <summary>档 id(0 威胁 / 1 机会 / 2 反应 / 3 灾难)。</summary>
        public readonly int Tier;

        /// <summary>生成锚点(0 医馆前 / 1 旅行路径 / 2 采集点)。</summary>
        public readonly int SpawnAnchor;

        /// <summary>触发方式(0 随机 / 1 脚本)。脚本条目**不进抽池**。</summary>
        public readonly int TriggerMode;

        /// <summary>处理方(见 <see cref="PoolHandler"/>)。</summary>
        public readonly PoolHandler Handler;

        /// <summary>规则九的因果标记 key(0 = 无)。</summary>
        public readonly int CauseFlagKey;

        /// <summary>
        /// 本条是否**急召出诊**(GDD 规则十二 12.2 的「出诊类」判据;`TD-randomevents-06`)。
        /// <para>装配层据此派生 `ActiveHouseCallQuery` 的整数集 —— **不再传 `Array.Empty&lt;int&gt;()`**。
        /// 该集合的成员身份**归内容侧**(37 定稿时逐条回填),52 只持整数集(GDD `random-events.md:563`)。</para>
        /// </summary>
        public readonly bool IsHouseCall;

        public EventPoolEntryData(int key, int wBase, int tier, int spawnAnchor,
                                  int triggerMode, PoolHandler handler, int causeFlagKey,
                                  bool isHouseCall)
        {
            Key = key; WBase = wBase; Tier = tier; SpawnAnchor = spawnAnchor;
            TriggerMode = triggerMode; Handler = handler; CauseFlagKey = causeFlagKey;
            IsHouseCall = isHouseCall;
        }
    }

    /// <summary>52 池条目表(<c>52_event_pool.cooked.bytes</c> 的解码形)。</summary>
    /// <remarks>
    /// ⚠️ `readonly struct` + 只读列表(与 `DiseaseRegistryDataSet` 同格):构造器负责规范化
    /// (按 key 升序),**不留可变字段**。`default(EventPoolDataSet)` 是合法形态
    /// (`Entries == null` ⇒ 装配层当作「池未配置」,不是错误)。
    /// </remarks>
    public readonly struct EventPoolDataSet
    {
        /// <summary>产物文件名(写方 / 读方 address 单一出处)。</summary>
        public const string CookedFileName = "52_event_pool.cooked.bytes";

        /// <summary>全部条目(**已按 key 升序** —— F1 CDF walk 的游走序由它钉死,GDD `:687`)。</summary>
        public readonly IReadOnlyList<EventPoolEntryData> Entries;

        /// <summary>夜间威胁档的逐条乘子(分子;整数域,ADR-006 禁浮点)。</summary>
        public readonly int NightThreatMultNum;

        /// <summary>夜间威胁档的逐条乘子(分母)。</summary>
        public readonly int NightThreatMultDen;

        /// <summary>上述乘子的硬上界(分子)。构建期拒收 `num/den > max`。</summary>
        public readonly int TodMultMaxNum;

        /// <summary>上述乘子的硬上界(分母)。</summary>
        public readonly int TodMultMaxDen;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32,ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>烘焙时随产物落盘的 schema 版本。</summary>
        public readonly uint SchemaVersion;

        /// <summary>
        /// 急召出诊条目的 key 集合(按池表序 = **key 升序**;空表 ⇒ 空数组)。
        /// <para>GDD 规则十二 12.2 的「出诊类条目集」—— 装配层据此填
        /// `ActiveHouseCallQuery` 的整数集(`TD-randomevents-06`)。</para>
        /// <para>⚠️ 提成方法而不是在装配层写内联循环:那条内联循环**没有落点可测**,
        /// 而「集合恒空」正是本债要消灭的形态(改前是硬编码 `Array.Empty&lt;int&gt;()`)。</para>
        /// </summary>
        public int[] HouseCallKeys()
        {
            if (Entries == null || Entries.Count == 0) return System.Array.Empty<int>();

            int n = 0;
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].IsHouseCall) n++;
            if (n == 0) return System.Array.Empty<int>();

            var keys = new int[n];
            int w = 0;
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].IsHouseCall) keys[w++] = Entries[i].Key;
            return keys;
        }

        public EventPoolDataSet(IReadOnlyList<EventPoolEntryData> entries,
                                int nightThreatMultNum, int nightThreatMultDen,
                                int todMultMaxNum, int todMultMaxDen,
                                uint configVersion, uint schemaVersion)
        {
            Entries = entries;
            NightThreatMultNum = nightThreatMultNum;
            NightThreatMultDen = nightThreatMultDen;
            TodMultMaxNum = todMultMaxNum;
            TodMultMaxDen = todMultMaxDen;
            ConfigVersion = configVersion;
            SchemaVersion = schemaVersion;
        }
    }
}
