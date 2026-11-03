// 权威来源:ADR-038 §D3(存档 v4 记录三个时间参数)
//
// ⚠️ 本类型是**写档时的入参**,不是时间系统的载体(`Sim.TimeParams` 才是)。
//    分开的理由:`Sim.Codec` 不引用 `Sim`(它只依赖 `Sim.Contracts`)⇒
//    不能把 `TimeParams` 拖进来;而且写档只需要三个数,不需要一整类参数。

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>写入存档头 v4 的三个时间参数(ADR-038 §D3)。</summary>
    /// <remarks>
    /// ⚠️ **刻意不提供「默认值 / Legacy 常量」** —— 本仓历史上唯一存在过的
    /// <c>36000/27000/9000</c> 只允许出现在**迁移脚本**(`SaveMigrationV3ToV4`,
    /// 那里写它是记录事实)与**夹具**里。生产写档若拿不到真值,应当**抛**,
    /// 而不是回落到一个恰好正确的常数 —— 那会让「配置改错」与「一切正常」不可分。
    /// </remarks>
    public readonly struct SaveTimeParams
    {
        /// <summary>一个世界日的 tick 数(写档时须 &gt; 0)。</summary>
        public readonly long TicksPerDay;

        /// <summary>夜的起点(日内相位)。</summary>
        public readonly long NightStart;

        /// <summary>夜长。</summary>
        public readonly long NightSpan;

        /// <summary>构造。</summary>
        public SaveTimeParams(long ticksPerDay, long nightStart, long nightSpan)
        {
            TicksPerDay = ticksPerDay;
            NightStart = nightStart;
            NightSpan = nightSpan;
        }

        /// <summary>是否已给出可用值(<c>TicksPerDay &gt; 0</c>)。</summary>
        public bool IsSpecified => TicksPerDay > 0L;
    }
}
