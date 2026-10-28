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
//
// 🔁 **2026-10-28 ADR-033 §④之补 —— 上段「不写流」有一处**唯一例外**,原文不删**:
//   撞号存档(基线诞生前写下)的载入路径必须**同会话固化** —— 把平移后的 id **写回事件流**。
//   不固化的存档是**混合态**,而混合态在流层面**不可判别**(`StructureRemoved(1)` 在两种 id
//   空间里同为整数 `1`)⇒ 下次载入会**删错结构 + 基线家具复活**(ADR-033 §④之补 反例)。
//   ⇒ 本类的写只有这一处,且**只走 `cureStream` 委托**,不经 `IEventSink.Append`
//   (这不是「新增一条状态」,是**同号归一化**,ADR-033 §⑥ 已明写它改写已发生事件的 id)。
//   未注入 `cureStream` 而命中判据 ⇒ **抛**(fail-closed):静默跳过 = 允许写出混合态存档。

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
        private readonly IPayloadEncoder _encoder;
        private readonly Action<IReadOnlyList<SimEvent>> _cureStream;

        /// <summary>构造装载服务。</summary>
        /// <param name="events">三流事件源(生产 = <c>() =&gt; stream.Events</c>;委托形式
        /// ⇒ 读档换流后自动适应,无需重装配 —— 同 <c>InventoryLoadService</c> / <c>LoadSequencer</c>
        /// 的既有形态)。</param>
        /// <param name="decoder">载荷解码器(<see cref="IPayloadDecoder"/>;非 null)。</param>
        /// <param name="world">被重建的世界(非 null)。</param>
        /// <param name="moduleCatalog">模块目录(非 null)—— 足迹按模块定义 + 朝向重算。</param>
        /// <param name="encoder">
        /// 载荷编码器(<see cref="IPayloadEncoder"/>)—— 仅在**撞号固化**时用到:平移后的
        /// <c>structure_id</c> 必须重新编码成新的载荷引用(ADR-033 §④之补)。
        /// <c>null</c> ⇒ 未提供固化能力。
        /// </param>
        /// <param name="cureStream">
        /// 「整表替换」接缝 —— 把固化后的事件序列**整体**写回事件流(生产 = 调
        /// <c>EventStream.Restore(events, foldedRows)</c>)。
        /// <c>null</c> ⇒ 未提供固化能力。
        /// <para>⚠️ <b>折叠行原样传回即可,但依赖的是 <c>EventStream.Restore</c> 内的自引用守卫</b>:
        /// <c>FoldedRows</c> 返回的是<b>内部列表实例</b>,而 <c>Restore</c> 会先 <c>Clear()</c>
        /// (清空该列表)再 <c>AddRange(foldedRows)</c> ⇒ 没有守卫时传自身 = 每轮固化静默抹掉
        /// 全部折叠行(<c>GetNextPatientId</c> 高水位依赖它们 —— ADR-006 Amendment B)。
        /// 守卫已落在 <c>EventStream.Restore</c>(自引用 ⇒ 先物化快照);夹具
        /// <c>test_load_curePreservesFoldedRows</c> 钉住该形态。</para>
        /// </param>
        /// <exception cref="ArgumentNullException">前三形参为 null(fail-closed ——
        /// 空解码器 ⇒ 空投影 ⇒ 「世界全空」的静默失败)。</exception>
        /// <remarks>
        /// <para><b>后两形参是可选的一对</b>:只给其中一个没有意义(编码了没处写 / 写不了编码)。
        /// <b>两者皆缺而命中撞号判据 ⇒ <see cref="Rebuild"/> 抛</b>,不得静默跳过 ——
        /// 跳过意味着允许写出**混合态存档**(ADR-033 §④之补 的硬不变量)。</para>
        /// <para>⚠️ 未命中判据的路径(今日**全部**生产存档)完全不触碰这两者。</para>
        /// </remarks>
        public WorldLoadService(Func<IReadOnlyList<SimEvent>> events,
                                IPayloadDecoder decoder,
                                World world,
                                IModuleCatalog moduleCatalog,
                                IPayloadEncoder encoder = null,
                                Action<IReadOnlyList<SimEvent>> cureStream = null)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(
                nameof(decoder), "未注入 IPayloadDecoder —— 拒绝以空投影冒充「世界为空」");
            _restorer = new StructureRestorer(world, moduleCatalog);
            _encoder = encoder;
            _cureStream = cureStream;
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

            // ── ADR-033:基线诞生**之前**的存档,其结构 id 与基线撞号 ⇒ 整体平移 +N ──
            //
            // 判据 = ∃ Placed 且 id ∈ [1, N](只扫 Placed —— 放宽会让每一份拆过初始家具的
            // 正常存档被误判,ADR-033 §②)。基线件数**须在重放之前读**:`RebuildFromDecoded`
            // 内部 `World.Reset()` 会把它归零,末尾再按重放结果重定。
            int baselineCount = _restorer.BaselineStructureCount;
            bool collides = StructureIdRemap.Collides(ops, baselineCount);

            // 重放用的序列:未撞号即原样(零分配);撞号则是平移后的新序列。
            IReadOnlyList<StructureRestoreOp> toReplay = ops;
            if (collides)
            {
                if (_encoder == null || _cureStream == null)
                    throw new InvalidOperationException(
                        $"结构 id 撞号(存在 Placed.id ∈ [1, {baselineCount}]),但本实例未注入固化能力" +
                        "(encoder + cureStream)⇒ 拒绝**只平移不固化**。不固化的存档是**混合态**:" +
                        "`StructureRemoved(1)` 在旧 / 新 id 空间里同为整数 1,流层面不可判别 ⇒ " +
                        "下次载入会**删错结构 + 基线家具复活**(ADR-033 §④之补)");

                toReplay = StructureIdRemap.Apply(ops, baselineCount);
            }

            int applied = _restorer.RebuildFromDecoded(toReplay);

            // ⚠️ **重放成功之后**才固化。重放抛(目录缺 moduleId / 指向不存在的 id)时
            //    事件流保持原样 ⇒ 下次载入按同一判据重试;先固化会把「读档失败」变成
            //    「流已被改写过」,而且改写的还是**没通过校验**的那一版。
            if (collides) _cureStream(RemapStructureIds(events, baselineCount));

            return applied;
        }

        /// <summary>
        /// 把三条结构类事件的 <c>structure_id</c> 平移 <c>+baselineCount</c>,其余事件**原样**保留
        /// (ADR-033 §③:全部结构操作共用同一映射,<c>Removed</c> / <c>Modified</c> 必须跟着走)。
        /// </summary>
        /// <remarks>
        /// <para>事件头(<c>Tick</c> / <c>Patient</c> / <c>Seq</c> / <c>Kind</c>)一律不动 ——
        /// 只换载荷里的 id ⇒ 全序键不变,<c>EventStream.Restore</c> 的单调 / 去重前提不受影响。</para>
        /// <para>⚠️ 非结构事件**原样引用**(不重新编码)—— 重新编码会白造一批 blob,且
        /// 折叠病人的载荷本就不该被复活。</para>
        /// </remarks>
        private SimEvent[] RemapStructureIds(IReadOnlyList<SimEvent> events, int baselineCount)
        {
            var remapped = new SimEvent[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                switch (e.Kind)
                {
                    case EventKind.StructurePlaced:
                    {
                        var p = _decoder.Decode<StructurePlacedPayload>(e.Kind, e.Payload);
                        var q = new StructurePlacedPayload(checked(p.StructureId + baselineCount),
                                                           p.Cell, p.ModuleId, p.Orientation, p.Variant);
                        remapped[i] = new SimEvent(e.Tick, e.Patient, e.Seq, e.Kind,
                                                   _encoder.Encode(e.Kind, q));
                        break;
                    }

                    case EventKind.StructureRemoved:
                    {
                        var p = _decoder.Decode<StructureRemovedPayload>(e.Kind, e.Payload);
                        var q = new StructureRemovedPayload(checked(p.StructureId + baselineCount),
                                                            p.Cell, p.ModuleId);
                        remapped[i] = new SimEvent(e.Tick, e.Patient, e.Seq, e.Kind,
                                                   _encoder.Encode(e.Kind, q));
                        break;
                    }

                    case EventKind.StructureModified:
                    {
                        var p = _decoder.Decode<StructureModifiedPayload>(e.Kind, e.Payload);
                        var q = new StructureModifiedPayload(checked(p.StructureId + baselineCount),
                                                             p.Cell, p.ModuleId, p.NewOrientation,
                                                             p.NewVariant, p.ModifiedFields);
                        remapped[i] = new SimEvent(e.Tick, e.Patient, e.Seq, e.Kind,
                                                   _encoder.Encode(e.Kind, q));
                        break;
                    }

                    default:
                        remapped[i] = e;
                        break;
                }
            }
            return remapped;
        }
    }
}
