// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则二 / 规则十 / 规则十一 / 4-DC-4
//   ADR-015 §三(单一整数格 WorldPos —— 所有格的唯一坐标类型)
//   ADR-016 §三(读粗粒度整数格,禁读表现态位置)
//
// ⚠️ AC-4-05 的守卫对象。本类型的**全部字段类型**是反射闭包扫描的目标:
//   禁入 = VitalsDto / disease_id / tier_named / drug_profile / EnvMod。
//   这正是驳回 K_difficulty 移交(规则十二)的机械守卫 —— 该类字段一旦出现即红。
//
// 本类型的字段刻意只有:整数格 + 种类 + 稳定 id + 距离平方(整数)。
// 无 float、无引用类型、无任何 9 / 11 / 8 的 sim 侧类型。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 候选目标的稳定序来源(4-DC-4)。<see cref="Candidate.StableId"/> 的取数口径由此决定。
    /// <para>构建期校验 4-DC-4:本枚举为主要求 <c>W/H/D</c> 在场(仅 <see cref="SlotLinearKey"/> 用)。</para>
    /// </summary>
    public enum StableIdSource : byte
    {
        /// <summary>掉落实体 / 容器:<c>instance_id</c>(21 <c>ItemInstanceId</c> 空间)。</summary>
        InstanceId = 0,

        /// <summary>采集点:烘焙资源索引(6 的逻辑层序)。</summary>
        BakedResourceIndex = 1,

        /// <summary>病人:<c>patient_id</c>(ADR-006 Amendment B 高水位空间)。</summary>
        PatientId = 2,

        /// <summary>POI 格:<c>poi_id</c>(ADR-021)。</summary>
        PoiId = 3,

        /// <summary>建造槽位:线性键 <c>x + W·(y + H·z)</c>(需 <c>W/H/D</c> 在场)。</summary>
        SlotLinearKey = 4,

        /// <summary>器具 / 面板 / 门 / 开关:<c>structure_id</c>(23 <c>BakedInitial</c>)。</summary>
        StructureId = 5
    }

    /// <summary>
    /// 候选目标(F-4.1 全序的输入行,规则二)。
    /// <para><b>纯数据载体,零方法,零可变字段。</b>所有量在整数域:</para>
    /// <list type="bullet">
    ///   <item><see cref="Cell"/> —— 整数格(ADR-015),第一键 <c>d∞</c> 的输入;</item>
    ///   <item><see cref="Kind"/> —— 闭集种类,第二键 <c>KindPriority</c> 的索引;</item>
    ///   <item><see cref="StableId"/> —— 第三键,<b>单一 int64 标量,禁析取</b>;</item>
    /// </list>
    /// <para>⚠️ <b>本类型是 AC-4-05 的反射扫描对象</b> —— 禁入字段见 <c>Candidate</c> 家族登记表
    /// (测试侧)。任何 <c>VitalsDto</c> / 病种名 / 档位 / 药性 / 环境修饰符类型字段出现即红。</para>
    /// </summary>
    /// <remarks>
    /// 4 只**选择**目标,永不结算(规则一)。<c>Candidate</c> 里没有「能否承受」「疗效」「容量」
    /// 这类结算侧字段 —— 那是 11 / 8 / 10 的事(AC-4-01)。
    /// </remarks>
    public readonly struct Candidate
    {
        /// <summary>候选所在整数格(ADR-015 §三)。</summary>
        public readonly WorldPos Cell;

        /// <summary>候选种类(闭集)。</summary>
        public readonly InteractableKind Kind;

        /// <summary>第三键稳定 id —— <b>int64 单一标量</b>(F-4.1:禁析取;取数口径见 <see cref="StableIdSource"/>)。</summary>
        public readonly long StableId;

        /// <summary><see cref="StableId"/> 的取数口径(4-DC-4 构建期校验用)。</summary>
        public readonly StableIdSource StableIdSource;

        /// <summary>构造候选。<b>无 float 形参,无结算侧类型形参</b>(AC-4-01/05)。</summary>
        public Candidate(WorldPos cell, InteractableKind kind, long stableId, StableIdSource stableIdSource)
        {
            Cell = cell;
            Kind = kind;
            StableId = stableId;
            StableIdSource = stableIdSource;
        }
    }

    /// <summary>
    /// 玩家交互**意图**(规则一:输入是意图源,不直接驱动模拟 —— ADR-005)。
    /// <para><b>纯数据载体。</b>4 消费它做选择;选择结果经 <c>InteractTarget</c> 出境。</para>
    /// <para>⚠️ <b>这不是上行通道</b> —— 客户端 → 主机的上行缺口见 <c>OQ-4-10</c>
    /// (归 ADR-001 窄修订 / 45 的 GDD 轮,P1b)。本类型只承载「玩家这一帧按了交互」。</para>
    /// </summary>
    public readonly struct InteractIntent
    {
        /// <summary>玩家当前所在整数格(主动交互的判据锚点;非表现态位置)。</summary>
        public readonly WorldPos PlayerCell;

        /// <summary>本帧是否有主动交互输入(键 / 手柄 / VR 触发器)。无 = 不选择。</summary>
        public readonly bool Pressed;

        /// <summary>意图发生的逻辑 tick(由 <c>ITickProvider</c> 驱动,与整 tick 对齐)。</summary>
        public readonly long Tick;

        public InteractIntent(WorldPos playerCell, bool pressed, long tick)
        {
            PlayerCell = playerCell;
            Pressed = pressed;
            Tick = tick;
        }

        /// <summary>空意图(无交互输入)。</summary>
        public static InteractIntent None { get; } =
            new InteractIntent(new WorldPos(0, 0, 0), false, 0);
    }

    /// <summary>
    /// 选择结果(规则一:输出 <c>(目标, 种类)</c>,零 gameplay 结算)。
    /// <para><see cref="HasTarget"/> = false 时其余字段无意义(「无目标」是合法结果,不是错误)。</para>
    /// <para>⚠️ 出境面 = 本类型 + <see cref="IDiscoveryReporter.Request"/>;
    /// <b>绝不</b>经 <c>IEventSink</c>(AC-4-02)。</para>
    /// </summary>
    public readonly struct InteractTarget
    {
        /// <summary>是否有选中目标。</summary>
        public readonly bool HasTarget;

        /// <summary>选中目标的种类(有目标时有效)。</summary>
        public readonly InteractableKind Kind;

        /// <summary>选中目标的稳定 id(有目标时有效;与 <see cref="Candidate.StableId"/> 同空间)。</summary>
        public readonly long StableId;

        /// <summary>选中目标所在格(有目标时有效)。</summary>
        public readonly WorldPos Cell;

        private InteractTarget(bool hasTarget, InteractableKind kind, long stableId, WorldPos cell)
        {
            HasTarget = hasTarget;
            Kind = kind;
            StableId = stableId;
            Cell = cell;
        }

        /// <summary>无目标(合法结果:附近没有可交互物,或模态摊开,或无输入)。</summary>
        public static InteractTarget None { get; } =
            new InteractTarget(false, InteractableKind.Drop, 0L, new WorldPos(0, 0, 0));

        /// <summary>构造一个有目标的选择结果。</summary>
        public static InteractTarget Of(InteractableKind kind, long stableId, WorldPos cell)
            => new InteractTarget(true, kind, stableId, cell);
    }
}
