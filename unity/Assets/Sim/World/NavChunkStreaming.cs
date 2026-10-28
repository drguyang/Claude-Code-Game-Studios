// 系统 6 的 **chunk 驻留驱动**(ADR-023 ⑥ · ADR-022 §五 · ADR-016 §三)。
//
// 权威来源:
//   ADR-023 ⑥ —— 运行期 chunk 激活权 = 系统 6;**只读 `ActorCellEntered` 的格**,
//                不读表现态位置 ⇒ 激活是**派生态**,不进流,重建期由格重推
//   ADR-022 §五 —— 导航格按 chunk 切片;未驻留 chunk = 全 `block`(保守判定)
//   ADR-016 §三 —— AI 读粗粒度整数格;禁读表现态位置(第二 QoS 到达时序不确定 ⇒ 决策非确定)
//
// ═══════════════════════════════════════════════════════════════════════════
// §为什么「由格驱动」而不是「每帧按玩家位置重算」
// ═══════════════════════════════════════════════════════════════════════════
//   后者的输入是 `transform.position` —— 那是**表现态**。ADR-016 §三 明确指出:
//   表现态位置的到达时序不确定(第二 QoS / 插值),把它当输入会让「同一事件流重放出
//   不同的驻留序列」⇒ 决策不可重放。
//   前者的输入是 `ActorCellEntered` 的**格**(整数、进世界流、tick 有界)⇒ 重放可复现。
//
//   ⚠️ 本类**不做任何装载 I/O**:它只把「哪几块该驻留」写进 `NavLattice`。
//      切片字节的获取(Addressables / 磁盘兜底)是装配侧的事 —— `Sim` 的引用集
//      恰 = {BCL, `Sim.Contracts`}(ADR-025 §①),物理上做不了 I/O。
//
// ═══════════════════════════════════════════════════════════════════════════
// §⚠️ 初始驻留为什么必须显式给
// ═══════════════════════════════════════════════════════════════════════════
//   死锁链:可走性依赖驻留 → 驻留依赖玩家跨格 → 跨格依赖能走得动 → 走得动依赖可走性。
//   ⇒ 装配期必须**先**用出生格喂一次(与运行时同一条路径,不另立「初始装载」API)。
//   若忘了喂,症状是「玩家出生就动不了」,而这个驱动本身毫无异常 —— 所以装配侧的
//   调用点由夹具钉住(见 `nav_streaming_wiring_test`)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>
    /// 把 <see cref="ChunkActivator"/> 的纯函数激活集写进 <see cref="NavLattice"/> 的驻留集。
    /// </summary>
    public sealed class NavChunkStreaming
    {
        private readonly NavLattice _lattice;
        private readonly ChunkActivator _activator;

        /// <summary>本驱动管理的导航格。</summary>
        public NavLattice Lattice => _lattice;

        /// <summary>底层的 chunk 激活器(纯函数;<see cref="ChunkActivator"/> 的既有实现)。</summary>
        public ChunkActivator Activator => _activator;

        /// <summary>最近一次喂入的格(诊断用;`null` 语义由 <c>HasCell</c> 表达)。</summary>
        public WorldPos LastCell { get; private set; }

        /// <summary>是否已被喂过至少一次(`false` ⇒ 除构造默认外无驻留变化)。</summary>
        public bool HasCell { get; private set; }

        /// <summary>构造。</summary>
        /// <param name="lattice">目标导航格(chunk 划分须与激活器一致 —— 两者都出自
        /// <see cref="ChunkTopology.ForWorld"/>,此处再核一次)。</param>
        /// <param name="streamingRadius">流式半径(chunk 单位;`0` = 只驻留玩家所在块)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="lattice"/> 为 null。</exception>
        public NavChunkStreaming(NavLattice lattice, int streamingRadius)
        {
            _lattice = lattice ?? throw new ArgumentNullException(nameof(lattice));
            _activator = new ChunkActivator(lattice.Topology, streamingRadius);
        }

        /// <summary>
        /// 收到一条 `ActorCellEntered` 的格:重推激活集并写入驻留。
        /// <para>纯函数语义:同一格序列重放 ⇒ 同一驻留序列(不依赖调用时刻 / 调用次数)。</para>
        /// </summary>
        /// <param name="cell">事件里的整数格(**不是**表现态位置)。</param>
        public void OnActorCellEntered(WorldPos cell)
        {
            _lattice.SetResidency(_activator.ComputeActiveChunks(cell));
            LastCell = cell;
            HasCell = true;
        }
    }
}
