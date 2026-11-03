// 权威来源:ADR-038 §D2(跨表引用形式)/ §D4(52 四值)· ADR-014 §二/§五(烘焙产物取数形状)
//          · GDD design/gdd/random-events.md §F1 / §规则六 / DC-3(TICKS_PER_DAY 同值契约)
//
// ⚠️ **`TicksPerDay` 在本产物里是「解析后的值」**,不是作者态的字面量:
//    作者态写的是 `{"$ref": "5_time.ticks_per_day"}`(零字面副本,AC-5-05),
//    烘焙期由 `RandomEventsBinder` 解析成整数再落盘。⇒ 运行期读到的**必然**等于 5 的同值,
//    除非有人绕过烘焙直接改产物(codec 的头部校验会拒)。
//
// ⚠️ 「四值」(WINDOW_SIZE / ROLL_INTERVAL / BASE / BUDGET_MAX)由用户 2026-11-03 裁定
//    (`6 / 6000 / 2 / 6`),写进 `assets/data/52_random_events.json`;
//    以后调值**不需要改代码**。
//
// ⚠️ **今日本产物零生产消费**(`EventRng.ComputeWindowStart` 无生产调用点,
//    `RandomEventsDirectorDriver` 尚未读配置)⇒ 它是「引用形式的载体」,
//    不是「52 已数据驱动」。见 ADR-038 §D7,不得借绿。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>52 随机事件导演的烘焙配置(<c>52_random_events.cooked.bytes</c> 的解码形)。</summary>
    public struct RandomEventsDataSet
    {
        /// <summary>日长(由 `5_time.ticks_per_day` 解析而来 —— 与 5 **同值**,DC-3)。</summary>
        public long TicksPerDay;

        /// <summary>子区间长度(tick)。须整除 <see cref="TicksPerDay"/>。</summary>
        public long RollInterval;

        /// <summary>每预算窗口的抽取条数上限。</summary>
        public int WindowSize;

        /// <summary>每日基础事件数。</summary>
        public int Base;

        /// <summary>每日硬上限。</summary>
        public int BudgetMax;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32,ADR-014 §五)。</summary>
        public uint ConfigVersion;

        /// <summary>烘焙时随产物落盘的 schema 版本。</summary>
        public uint SchemaVersion;
    }
}
