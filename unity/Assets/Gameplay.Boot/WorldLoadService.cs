// 世界流 → World 的装载服务(装配层胶水 · S20-18 · 2026-10-28)。
//
// 权威来源:
//   · `production/sprints/sprint-20.md` §十 S20-18
//   · ADR-009 §四(派生态:由事件流确定性重建,不持状态、不写流)
//   · ADR-025 §①(`Sim` 引用集恰 = {BCL, Sim.Contracts};本类住 **Boot**,是装配层胶水)
//   · ADR-029 / ADR-031 甲案(解码归看得见 `Sim.Codec` 的一侧 —— 即**本类**)
//   · 同型先例:`Gameplay.Boot/InventoryLoadService`(Boot 层重建服务;`LoadSequencer`
//     的 `afterRestore` 钩子的第一个消费者,本类是第二个)
//
// ⚠️ **为什么分两层**(`WorldLoadService` 解码 / `StructureRestorer` 重放):
//   `StructureRestorer` 住 `Sim`,`Sim` **够不着** `PayloadCodec`(ADR-025 §①:111 禁止 +
//   b2 门强制)⇒ 与 `PoiStateMachine.RebuildFromDecoded` 完全同型:解码在**看得见 codec
//   的一侧**完成,`Sim` 只收已解码的字段。写在 Sim 侧解码 = 破门 A。
//
// ⚠️ **本类不写流**:纯读 —— 从 `stream.Events` 重建,零 `IEventSink.Append`
//   (ADR-009 §四 派生态;ADR-019 §三「只读消费者」同纪律)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 世界装载服务 —— 把<b>世界流</b>重建为「结构实例表 + 占用表」。
    /// <para><b>这是 <see cref="StructureRestorer"/> 的唯一生产调用点</b>,也是
    /// <see cref="World.Reset"/> 的唯一生产调用链(重置发生在重放入口内部,二者不可分)。</para>
    /// <para><b>派生态</b>:零缓存、零增量 —— 每次 <see cref="Rebuild"/> 从流全量重算
    /// (ADR-009 §四;缓存会引入第二真源)。</para>
    /// </summary>
    public sealed class WorldLoadService
    {
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly StructureRestorer _restorer;

        /// <summary>构造装载服务。</summary>
        /// <param name="events">三流事件源(生产 = <c>() =&gt; stream.Events</c>;委托形式
        /// ⇒ 读档换流后自动适应,无需重装配 —— 同 <c>InventoryLoadService</c> / <c>LoadSequencer</c>
        /// 的既有形态)。</param>
        /// <param name="decoder">载荷解码器(<see cref="IPayloadDecoder"/>;非 null)。</param>
        /// <param name="world">被重建的世界(非 null)。</param>
        /// <param name="moduleCatalog">模块目录(非 null)—— 足迹按模块定义 + 朝向重算。</param>
        /// <exception cref="ArgumentNullException">任一形参为 null(fail-closed ——
        /// 空解码器 ⇒ 空投影 ⇒ 「世界全空」的静默失败)。</exception>
        public WorldLoadService(Func<IReadOnlyList<SimEvent>> events,
                                IPayloadDecoder decoder,
                                World world,
                                IModuleCatalog moduleCatalog)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(
                nameof(decoder), "未注入 IPayloadDecoder —— 拒绝以空投影冒充「世界为空」");
            _restorer = new StructureRestorer(world, moduleCatalog);
        }

        /// <summary>
        /// 从事件流重建世界。**唯一的重建入口**。
        /// <para>⚠️ <b>空流 ⇒ 世界被清空</b>(不是「保持原样」)—— 语义上正确:一个没有建造事件的
        /// 存档,其世界就是空的。若「什么都没发生就保持原样」,旧世界的残留会冒充新存档,
        /// 那正是本件要消灭的失败模式。</para>
        /// </summary>
        /// <returns>实际重放的建造事件条数(<c>0</c> = 流里没有建造事件)。</returns>
        /// <exception cref="InvalidOperationException">
        /// 见 <see cref="StructureRestorer.RebuildFromDecoded"/> —— 目录缺 moduleId /
        /// 拆除或修改指向不存在的 id。抛出时世界已回到**重置后的空态**(原子性)。
        /// </exception>
        public int Rebuild()
        {
            IReadOnlyList<SimEvent> events = _events() ?? Array.Empty<SimEvent>();

            // 一次预扫:先把流过滤成 op 序列(顺序 = 流的原序,**不重排** ——
            // 全序键的解释权归 ADR-008,不由本类二次发明),再交 Sim 侧重放。
            var ops = new List<StructureRestoreOp>();
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                switch (e.Kind)
                {
                    case EventKind.StructurePlaced:
                    {
                        var p = _decoder.Decode<StructurePlacedPayload>(e.Kind, e.Payload);
                        ops.Add(StructureRestoreOp.Placed(p.StructureId, p.Cell, p.ModuleId,
                                                          p.Orientation, p.Variant));
                        break;
                    }

                    case EventKind.StructureRemoved:
                    {
                        var p = _decoder.Decode<StructureRemovedPayload>(e.Kind, e.Payload);
                        ops.Add(StructureRestoreOp.Removed(p.StructureId, p.Cell, p.ModuleId));
                        break;
                    }

                    case EventKind.StructureModified:
                    {
                        // ⚠️ 载荷恒载**绝对新值**(写侧 `StructureWriter.Modify` 已把
                        //    `newOrientation ?? inst.Orientation` 解析后放进载荷)⇒
                        //    重放**不读** `ModifiedFields` 位掩码。若将来写侧改成只载增量,
                        //    本行是必须同步改的那一处。
                        var p = _decoder.Decode<StructureModifiedPayload>(e.Kind, e.Payload);
                        ops.Add(StructureRestoreOp.Modified(p.StructureId, p.NewOrientation, p.NewVariant));
                        break;
                    }

                    default:
                        // 其余 32 支与本投影无关 —— 显式跳过,不落任何状态。
                        break;
                }
            }

            return _restorer.RebuildFromDecoded(ops);
        }
    }
}
