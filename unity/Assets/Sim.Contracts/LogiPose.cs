// ADR-016 §二/§七 · GDD enemy-ai.md 规则十五/规则十九 —— 敌人逻辑位姿与驯化接口形状(P0)。
//
// 权威来源:
//   ADR-016 §二 —— 逻辑位姿在 sim,表现位姿受其单向驱动;`LogiPose = (WorldPos Cell, byte Facing)`
//   ADR-016 §七 —— P1a 驯化接口形状本次定型,P0 只交付「接口 + 数据字段占位」
//   GDD enemy-ai.md 规则十五(:225-227)—— `LogiPose` 逐 tick 整数推进;
//     **禁**从 `Transform.position` 反推逻辑格(ADR-016 §一;adr-015:157 同条纪律)
//   GDD enemy-ai.md 规则十九(:267-272)—— `ITameable` + `OwnerId` + `TameState`,
//     命令通道复用行为程序,不新开第二条命令通道
//   S19-3 判据 ①(落点裁定):`LogiPose` / `ITameable` / `TameState` 落 `Sim.Contracts`
//     —— 它是 13(表现层,引用集不含 `Sim`)与 27 共享的**跨装配契约数据**
//     (13 的投影链输入,`PresentationProjection.cs` 头注已把 `LogiPose` 列为投影输入);
//     按 ADR-025 §① 契约类型住 `Sim.Contracts`,无落 `Sim` 的偏离理由。
//
// ⚠️ **`LogiPose` 与任何表现层 `Transform` / `Vector3` 位姿不是一物**(TD-enemy-01 逐字):
//   本结构只含整数格 + 朝向字节,零 float / 零 Unity 引用(门 A 同款纪律 —— `Sim.Contracts`
//   的 `noEngineReferences: true` 使其结构上不可能引用 `UnityEngine`)。
//   表现层把 `LogiPose` 映射成连续位姿;**禁**反向(sim 永不读表现态位置)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 敌人逻辑位姿(27 的 sim 侧真值;派生态,从三流重建,不进流 —— ADR-016 §一)。
    /// </summary>
    /// <remarks>
    /// 逐 tick 整数推进:沿整数导航格路径(ADR-016 §五)按定点累加器步进;
    /// 推进算子 = `Sim.EnemyLocomotion.Advance`(GDD F-27-4 权威形态)。
    /// `Facing` 为 3D 六向朝向的离散索引:0..5 依次映射
    /// +X / −X / +Y / −Y / +Z / −Z(与 `EnemyPathing` 邻接生成顺序一致);
    /// 停止态沿 GDD F-27-4 的 `DirFrom` 语义取「路径上下一格」方向,无下一格时保持原值。
    /// </remarks>
    public readonly struct LogiPose
    {
        /// <summary>逻辑格(整数导航格,ADR-015 §三 单一整数格)。</summary>
        public readonly WorldPos Cell;
        /// <summary>朝向字节(0..5 = +X/−X/+Y/−Y/+Z/−Z;与邻接生成顺序一致)。</summary>
        public readonly byte Facing;

        public LogiPose(WorldPos cell, byte facing)
        {
            Cell = cell;
            Facing = facing;
        }

        public bool Equals(LogiPose other) => Cell.Equals(other.Cell) && Facing == other.Facing;
        public override bool Equals(object obj) => obj is LogiPose p && Equals(p);
        public override int GetHashCode() => unchecked(Cell.GetHashCode() * 31 + Facing.GetHashCode());
        public static bool operator ==(LogiPose a, LogiPose b) => a.Equals(b);
        public static bool operator !=(LogiPose a, LogiPose b) => !a.Equals(b);
        public override string ToString() => $"LogiPose(cell={Cell}, facing={Facing})";
    }

    /// <summary>
    /// 驯化状态(P1a 语义,P0 只落形状 —— ADR-016 §七「P0 交付:接口 + 数据字段占位」)。
    /// </summary>
    /// <remarks>
    /// P0 的动物实体恒为 <see cref="Wild"/>(未驯化);<see cref="Owned"/> 及其后序列
    /// 是 P1a(28 捕获·驯化·取材与护卫)的语义占位,P0 不得实现任何进入这些状态的代码路径
    /// (AC-27-33:「驯化行为代码路径为空集」)。
    /// </remarks>
    public enum TameState
    {
        /// <summary>野生(未驯化)—— P0 动物实体的恒定值。</summary>
        Wild = 0,
        /// <summary>驯化中(P1a 占位,P0 禁入)。</summary>
        Taming = 1,
        /// <summary>已驯化待归属(P1a 占位,P0 禁入)。</summary>
        Tamed = 2,
        /// <summary>已归属随从(P1a 占位,P0 禁入)。</summary>
        Owned = 3
    }

    /// <summary>
    /// 可驯化标记接口(P1a 驯化接口,P0 只落形状 —— ADR-016 §七)。
    /// </summary>
    /// <remarks>
    /// P0 约束(AC-27-33):实现动物实体的 `OwnerId` 恒为 <see cref="PatientId.None"/>,
    /// `TameState` 恒为 <see cref="TameState.Wild"/>,**驯化行为代码路径为空集**。
    /// 28(P1a)实现驯化与命令可接入而 27 无需重写;命令通道复用行为程序
    /// (`Follower / Guard / Stay` 作为行为程序可表达的目标态),不新开第二条命令通道。
    /// </remarks>
    public interface ITameable
    {
        /// <summary>驯化状态(P0 恒为 <see cref="TameState.Wild"/>)。</summary>
        TameState Tame { get; }

        /// <summary>归属玩家 id(P0 恒为 <see cref="PatientId.None"/>;ADR-007 §四 哨兵语义)。</summary>
        PatientId OwnerId { get; }
    }

    /// <summary>
    /// 动物实体的驯化数据字段占位(P0 —— ADR-016 §七「接口 + 数据字段占位」的字段半)。
    /// </summary>
    /// <remarks>
    /// ⚠️ **P0 不变式由类型结构保证,不靠纪律**:readonly struct 且两个属性恒返回
    /// <see cref="TameState.Wild"/> / <see cref="PatientId.None"/>,P0 侧**不存在**任何
    /// 把它们改成别值的代码路径(AC-27-33「驯化行为代码路径为空集」的结构化表达)。
    /// <para>⚠️ **零值陷阱(为什么不做成可变字段)**:<see cref="PatientId.None"/> = `-1`,
    /// 可变字段的 C# 默认值 `0` **不是** None —— 「漏初始化」会静默得到一个非法归属 id。
    /// readonly + 恒返回值使非法态**编译期不可表达**;P1a(28)实现真驯化时以可变实现
    /// 替换本占位(接口不变,27 无需重写 —— ADR-016 §七 逐字)。</para>
    /// </remarks>
    public readonly struct TameableFields : ITameable
    {
        /// <summary>P0 动物实体的唯一合法值(全部字段 = P0 恒定值)。</summary>
        public static readonly TameableFields P0 = default;

        /// <inheritdoc />
        public TameState Tame => TameState.Wild;

        /// <inheritdoc />
        public PatientId OwnerId => PatientId.None;
    }
}
