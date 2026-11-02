// 2026-11-02 · `TD-interaction-03` —— 发现上报的**生产实装**(4 → 6 的唯一出境通道)
//
// ── 病因 ────────────────────────────────────────────────────────────────────
// 生产装配里 `InteractionSelector` 收的是 `UnwiredDiscoveryReporter.Instance`(空实现),
// 而 6 的 `PoiStateMachine` **在组合根里从未被构造** ⇒ 「玩家主动交互 → 发现 POI」这条
// 全案唯一的出境路径在生产上**整条不存在**:选中 POI 什么也不会发生,声都不吭。
// (`AC-4-12` / `AC-4-13` 的夹具用的是注入替身,所以一直是绿的 —— 正是「生产类零命中」形态。)
//
// ── 现口径 ──────────────────────────────────────────────────────────────────
// 4 只发**请求**(`DiscoveryRequest` = poi_id + evidence_cell + tick),不写流;
// 状态转移与 `PoiStateChanged` 的写入归 6 的 `PoiStateMachine`(ADR-021 §②:6 是唯一写者)。
// 本类只是那条缝的搬运工:**不判状态、不判半径**(半径裁剪已在装载期做完,规则二)。
//
// ⚠️ **未绑定状态机 ⇒ 抛**,不静默丢弃:空实现正是本债的成因;
//   再给一个「悄悄丢掉请求」的分支,等于把同一个假绿换个位置。

using System;
using DaYiJingCheng.Gameplay.Interaction;   // IDiscoveryReporter / DiscoveryRequest
using DaYiJingCheng.Sim.World;              // PoiStateMachine

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 把 4 的发现请求转成 6 的状态机调用。
    /// <para><b>零状态</b>:只有两个 read-only 依赖(`Func` 取机 + 无缓存)。</para>
    /// </summary>
    public sealed class PoiDiscoveryReporter : IDiscoveryReporter
    {
        private readonly Func<PoiStateMachine> _machineOf;

        /// <param name="machineOf">
        /// 状态机来源。用 <see cref="Func{TResult}"/> 而非直接引用,是因为装配次序:
        /// 选择器在候选集段就要拿到 reporter,而 `PoiStateMachine` 依赖同一段里稍后才齐的件。
        /// ⚠️ 求值失败(null)⇒ 本类的 <see cref="Request"/> **抛**(见类注)。
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="machineOf"/> 为 null。</exception>
        public PoiDiscoveryReporter(Func<PoiStateMachine> machineOf)
        {
            _machineOf = machineOf ?? throw new ArgumentNullException(nameof(machineOf));
        }

        /// <inheritdoc />
        /// <remarks>
        /// 返回 `NotRegistered` / `AlreadyAtState` 都是**合法结果**(6 的定义侧 / 状态侧各自决定),
        /// 本类不解释、不转译、不重试 —— 一次交互一次请求。
        /// </remarks>
        /// <exception cref="InvalidOperationException">状态机未装配(装配缺陷,不是运行期条件)。</exception>
        public void Request(in DiscoveryRequest request)
        {
            PoiStateMachine machine = _machineOf();
            if (machine == null)
                throw new InvalidOperationException(
                    "发现上报时 PoiStateMachine 未装配 —— 4 → 6 的出境通道断了。" +
                    "这不是「无 POI」的合法结果:POI 候选只在定义侧装载成功时才存在。");

            // poi_id 在 4 的载荷里是 long(F-4.3 三字段逐字),6 的登记面是 int ——
            // 越界即畸形产物;checked 让它**响**,不静默截断成「另一个 POI」。
            machine.TryDiscover(checked((int)request.PoiId), request.Tick);
        }
    }
}
