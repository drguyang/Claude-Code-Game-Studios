// ADR-020 / OQ-4-13 —— MotorSuppressed per-source lease(位图语义)
//
// 权威来源:
//   story-006 AC-1-23: 位图 + 幂等 + 只碰自己位;调用点白名单 = {4, 10, 25}
//   GDD 轴 4(:265-275, OQ-4-13 裁定): 单 bool → 每源一位 + OR 聚合
//   ADR-014 ordinal 纪律: LeaseSource append-only、禁重排、已用值永不复用
//
// 纪律:
//   ① 每源只碰自己那一位
//   ② 幂等 —— 同源重复 Acquire 不计数(位语义,非引用计数)
//   ③ 无到期 —— 压制期由持有者自己的规则说了算;本类零计时器/零超时字段
//   ④ 作用域 = 水平位移 + Jump,不含攻击(1 不得扩语义)

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>
    /// 压制源(ordinal = 系统号,既是位序也是审计标签;append-only 禁重排)。
    /// </summary>
    public enum LeaseSource
    {
        /// <summary>4 交互系统。</summary>
        Self = 4,

        /// <summary>10 急救动作。</summary>
        Emergency = 10,

        /// <summary>25 格斗与武器线。</summary>
        Combat = 25
    }

    /// <summary>
    /// MotorSuppressed per-source lease 位图(OR 聚合)。
    /// 作用域 = 水平位移 + Jump,不含攻击(纪律 ④)。
    /// </summary>
    public sealed class MotorLease
    {
        private int _bitmap;

        /// <summary>是否被压制(任一位为 1)。</summary>
        public bool IsSuppressed => _bitmap != 0;

        /// <summary>
        /// 置位指定源的 lease(纪律 ②:幂等,重复 Acquire 不计数)。
        /// </summary>
        public void Acquire(LeaseSource source) => _bitmap |= 1 << (int)source;

        /// <summary>
        /// 清除指定源的 lease(纪律 ①:只碰自己那一位;未持有时无操作,无下溢)。
        /// </summary>
        public void Release(LeaseSource source) => _bitmap &= ~(1 << (int)source);

        /// <summary>指定源是否持有 lease。</summary>
        public bool HasLease(LeaseSource source) => (_bitmap & (1 << (int)source)) != 0;
    }
}
