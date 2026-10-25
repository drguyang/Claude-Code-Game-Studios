// 52 随机事件导演的 tick 边沿主循环壳 —— 生产接线轮(2026-10-25 · 三裁全取推荐)。
//
// 权威来源:
//   GDD random-events.md 规则十二 12.1 —— 出诊启动触发 = 活跃出诊目标 ∧ 本 tick 首次
//     从医馆格跨出;触发判定全部在写者内复核(HouseCallStartedWriter,TD-randomevents-02)
//   GDD 规则十二 12.2 —— 「出诊类」判据 = event_key ∈ 出诊类条目集(构建期配置;
//     **哪些 event_key 是急召出诊 = 37 内容件定稿时回填,52 侧只持整数集**)
//   TD-randomevents-02 二十九订 ⚠ 接线义务① —— 「52 导演主循环逐 tick 调 TryStartAtTick」
//     归本件;本件落地即义务①结清(结案不覆盖项的最后一笔 52 侧)
//   PatientAppearedDriver / CaseOpenedDriver —— per-tick 边沿驱动的既有范式
//     (BootRoot.Update 的边沿序列;本驱动插在急救通道之后、checkpoint 兜底之前)
//   ADR-005 —— 主机唯一执行;本驱动只被 BootRoot 的边沿泵驱动,零墙钟、零渲染帧语义。
//
// 2026-10-25 接线轮三裁(用户全取推荐):
//   ① **范围 = 仅出诊启动一节** —— 主循环壳立起来,其余 52 节留具名槽(见下);
//   ② **医馆格源 = 空集占位**(ClinicCellSource,真源归 23/24);
//   ③ **出诊类 event_key 集 = 生产空集**(37 内容件定稿时回填)。
//
// ⚠️ **双重空集 ⇒ 本轮接线在生产上是「通电待信」态**:出诊启动链每 tick 真跑
//   (查询 + 写者 + 前置复核全链执行),但两个数据真空(空 key 集 ⇒ 恒 NoActiveTarget;
//   即便有急召,空地块集 ⇒ 恒 NotCrossingOut)使 HouseCallStarted 生产恒零写入。
//   这是**诚实的 fail-safe**:两处数据各自回填(37 的 key 集 / 23-24 的地块集)后
//   链路自动激活,**零代码改动**。playtest 里「没见出诊触发」不是缺陷登记。
//
// 其余 52 节的具名槽(本件不接,各归其轮):
//   - 延迟威胁:入槽 TryDefer(威胁档 ∧ 医馆 ∧ 同日 —— 依赖抽池/掷骰生成链)
//     / 补发 TryClear(玩家离开医馆 —— 与本节共用跨格判定,接时复用本件的边沿)
//     / 跨日作废 TryClear(日边界遍历 —— 无 DayBoundary 事件,须另裁驱动形态);
//   - 遭遇开启/结束(EncounterStarted / EncounterEnded —— 写者尚未落地,b7 豁免在册);
//   - 急召生成链(抽池 → 掷骰 → F1 管线 → EventArrived)—— F1PipelineInput 的
//     生产构造方随此节诞生,届时**必须**消费服务袋的同一 IClinicCellSource(同源义务)。
//
// 复杂度登记:每边沿调用 TryStartAtTick ⇒ 内部三次全流扫描(查询 + 本 tick 跨格 +
// 已启动检查),量级 O(流长)/边沿。playtest 流量级下无感;**游标/增量优化归性能轮**
// (流为追加型,增量扫有清晰缝,但本轮不预优化 —— 流小、可读性优先)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 52 随机事件导演主循环壳(tick 边沿)—— 本轮只接出诊启动一节(类头注具名槽)。
    /// </summary>
    public sealed class RandomEventsDirectorDriver
    {
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly IPayloadDecoder _decoder;
        private readonly IClinicCellSource _clinicCells;
        private readonly IReadOnlyCollection<int> _houseCallEventKeys;

        private HouseCallStartedWriter _writer;
        private long _lastDrivenTick = -1;

        /// <param name="events">事件流只读视图的取用闭包(生产 = <c>() => stream.Events</c>;
        /// 经闭包而非直持 —— 读档 <c>Restore</c> 换流后自动指向新内容)。</param>
        /// <param name="sink">事件写入通道(转发给写者)。</param>
        /// <param name="encoder">载荷编码器(ADR-029)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <param name="clinicCells">医馆格判据源 —— **必须取自服务袋同一实例**(同源义务)。</param>
        /// <param name="houseCallEventKeys">出诊类 event_key 闭集(生产 = 空集,裁定③)。</param>
        public RandomEventsDirectorDriver(
            Func<IReadOnlyList<SimEvent>> events,
            IEventSink sink,
            IPayloadEncoder encoder,
            IPayloadDecoder decoder,
            IClinicCellSource clinicCells,
            IReadOnlyCollection<int> houseCallEventKeys)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _clinicCells = clinicCells ?? throw new ArgumentNullException(nameof(clinicCells));
            _houseCallEventKeys = houseCallEventKeys ?? throw new ArgumentNullException(nameof(houseCallEventKeys));
        }

        /// <summary>玩家 actorId 是否已绑定(装配后、玩家生成前的间隙 = 未绑定)。</summary>
        public bool IsPlayerBound => _writer != null;

        /// <summary>
        /// 本驱动消费的医馆格源(**同源钉的观测面** —— 组合根测试断言它 == 服务袋实例)。
        /// </summary>
        public IClinicCellSource ClinicCells => _clinicCells;

        /// <summary>已写入的 HouseCallStarted 条数(测试/观测接缝)。</summary>
        public int StartedCount { get; private set; }

        /// <summary>最近一次边沿的写者结果码(测试/观测接缝;未绑定期为 None 值域外哨 -1)。</summary>
        public HouseCallStartResult LastResult { get; private set; } = (HouseCallStartResult)(-1);

        /// <summary>
        /// 绑定玩家 actorId(装配期后置 —— id 在装配之后才铸造,BootRoot 生成玩家处调用;
        /// 承 <c>NoPendingMedicalActionQuery.Bind</c> 晚绑定先例)。
        /// </summary>
        /// <param name="playerActorId">玩家实体 id(≥ 0;与花名册登记同一 id)。</param>
        /// <exception cref="ArgumentOutOfRangeException">id 为负。</exception>
        /// <exception cref="InvalidOperationException">重复绑定(只许一次 ——
        /// 玩家 id 单调铸造,二次绑定 = 装配错误)。</exception>
        public void BindPlayer(int playerActorId)
        {
            if (playerActorId < 0)
                throw new ArgumentOutOfRangeException(nameof(playerActorId), playerActorId,
                    "玩家实体 id 不得为负 —— PatientId.None(-1)是世界级事件哨兵,不是玩家");
            if (_writer != null)
                throw new InvalidOperationException(
                    "玩家 actorId 重复绑定 —— 玩家 id 单调铸造,只许一次(换源须换新装配)");
            _writer = new HouseCallStartedWriter(
                _sink, _encoder, _decoder, _clinicCells, _houseCallEventKeys, playerActorId);
        }

        /// <summary>
        /// tick 边沿驱动 —— 调出诊启动写者的本 tick 判定(规则十二 12.1)。
        /// </summary>
        /// <param name="tick">本边沿自己的逻辑 tick(由帧泵回推)。</param>
        /// <remarks>未绑定玩家 ⇒ no-op(fail-safe:装配未完成 = 不驱动,不是错误态);
        /// 同 tick 二次调用被写者的 AlreadyStartedThisTick 与本处 <c>_lastDrivenTick</c> 双面拒。</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");
            if (_writer == null) return;
            if (tick == _lastDrivenTick) return;   // 同 tick 边沿只驱动一次(帧泵语义上不会发生,防御)
            _lastDrivenTick = tick;

            var result = _writer.TryStartAtTick(_events(), tick);
            if (result == HouseCallStartResult.Applied)
                StartedCount++;
            LastResult = result;
        }
    }
}
