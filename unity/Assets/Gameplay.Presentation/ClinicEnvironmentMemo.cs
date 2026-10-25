// 权威来源:
//   GDD `design/gdd/clinic-machine.md`
//     · EC-24-05 `:291` —— 「缓存失效:以 `Structure*` 事件为失效键;失效后重导出,
//       **禁过期值混入**。缓存与否**不改结果**(AC:缓存前后逐位一致)」
//     · States and Transitions `:123-127` —— 「**缓存是允许的优化(= 表现层 Memoize),不是状态**」
//       「须以 `Structure*` 事件为失效键(ADR-002 / 45 的 `IReplayPipe` 通知),
//        且缓存只改性能不改结果」
//     · Dependencies `:149` / `:308` —— 45 → 24:`Structure*` 失效通知(方向 = 网络层通知 24 侧)
//   ADR-005(确定性模拟)· ADR-016 §一(三源不变量)
//   ADR-025 §① —— 本文件住 `Gameplay.Presentation`(边界层),`Sim` 侧零缓存
//   story `production/epics/clinic-machine/story-005-purity-replay-memoize.md`
//     §Implementation Notes 3 —— 「缓存实现落 `Gameplay.Presentation`:监听 `Structure*` 事件流,
//     键 = 事件全序键」
//
// ⚠️ **为什么本类住表现层而不是 `Sim`**:GDD `:123-127` 把缓存定性为**表现层 Memoize**,
//   「不是状态」。若住 `Sim`,它就成为 sim 侧的第二个真源 —— 违背「24 无自有状态」
//   (States 节首句)与 ADR-005 的「无状态 = 可重放」。**本类不存在时 24 照样正确**
//   (只是每次重算),这是它属表现层的结构性证据。
//
// ⚠️ **缓存值与「直接求值」逐位一致 = 结构性的,不是纪律**:
//   本类的重算**就是** `ClinicEnvironmentEvaluator.Evaluate(...)` 本身,零二次加工、
//   零增量更新、零「在旧值上打补丁」。⇒ 命中与未命中只差「算不算」,不差「算什么」。
//   EC-24-05 的「缓存与否不改结果」因此不可能被本类违反(除非重算路径本身被改坏,
//   那会被 `clinic_purity_replay_test` 的逐位对拍抓到)。
//
// ⚠️ **失效是单调的**(只清不置):任何 `Structure*` 事件都清 `_hasCache`,
//   不因「键更旧」而保留旧缓存。⇒ 乱序到达(第二 QoS 通道 / 重连拉流)只会**多算一次**,
//   不会让陈旧值复活。保守方向的错 = 性能损失,不是正确性损失。
//
// ⚠️ **本类不产生 `ClinicEnvDto`**:DTO 的两个字段需要**词表映射**
//   (`RoomNameId` = int 房号 ↔ `ClinicRoomId(High,Low)`;`ContextIds` = 词表 ordinal ↔
//   `ClinicContextRow.ContextId` 的 string),而两处映射的语义**全库未裁** ——
//   `production/qa/evidence/impl-s20-1-clinic004-2026-10-25.md:110` 已登记为 NOT-RUN。
//   发明映射 = 第二真源 ⇒ 本类缓存 `ClinicEnvEvaluation`(24 的纯输出),
//   投影到 DTO 归装配轮(映射裁定后)。**这是如实登记,不是遗漏。**

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>
    /// 系统 24 求值的**表现层 Memoize**(EC-24-05):缓存 `ClinicEnvEvaluation`,
    /// 以 `Structure*` 事件的**全序键**为失效键。
    /// <para>⚠️ <b>本类不是状态</b>(GDD `:123-127`):删掉它,24 的输出**一字不变**,
    /// 只是每次多算一次。任何「本类返回的值与直接求值不同」的情形都是缺陷。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>用法</b>(45 / 装配层的接线形):</para>
    /// <list type="number">
    /// <item>构造一次(注入 23 的注册表 + 四份 cooked 表句柄)</item>
    /// <item>事件流投递点调 <see cref="Apply"/> —— 只吸收 `Structure*` 三支,其余事件是 no-op</item>
    /// <item>读方调 <see cref="Get"/> —— 命中返回缓存,未命中重算并落缓存</item>
    /// </list>
    /// <para>⚠️ <b>布局源 = 23 的注册表(`World.Structures`)</b>,不是快照对象:
    /// 每次重算现建 <see cref="StructureInstanceReadView"/>(它是快照语义,持有实例 = 持有陈旧缓存)。</para>
    /// <para>⚠️ <b>可观测面</b>:<see cref="RecomputeCount"/> / <see cref="HitCount"/> /
    /// <see cref="InvalidationCount"/> / <see cref="HasCache"/> / <see cref="CachedKey"/> ——
    /// 「缓存真的生效了吗」与「失效真的发生了吗」都可判定,不靠读代码。</para>
    /// </remarks>
    public sealed class ClinicEnvironmentMemo
    {
        private readonly StructureInstanceRegistry _layout;
        private readonly BuildModulesCatalog _modules;
        private readonly ClinicFurnitureCatalogue _furniture;
        private readonly ClinicRoomTable _rooms;
        private readonly ClinicContextTable _contexts;
        private readonly ClinicAdjTable _adj;

        private ClinicEnvEvaluation _cached;
        private EventOrderKey _cachedKey;
        private bool _hasCache;

        /// <summary>构造:六份输入全部必填(fail-loud —— 缺任一件链只能给出「恒 ROOM_NONE」)。</summary>
        /// <param name="layout">23 的结构实例表(`World.Structures`)。</param>
        /// <param name="modules">模块目录(`module_id` → `furn_type` 的跨层桥)。</param>
        /// <param name="furniture">21a 家具目录。</param>
        /// <param name="rooms">24 `ROOM_TABLE`。</param>
        /// <param name="contexts">24 `CONTEXT_TABLE`。</param>
        /// <param name="adj">24 `ADJ_TABLE`。</param>
        /// <exception cref="ArgumentNullException">任一为 null(接线错误,非设计态)。</exception>
        public ClinicEnvironmentMemo(StructureInstanceRegistry layout,
                                     BuildModulesCatalog modules,
                                     ClinicFurnitureCatalogue furniture,
                                     ClinicRoomTable rooms,
                                     ClinicContextTable contexts,
                                     ClinicAdjTable adj)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _furniture = furniture ?? throw new ArgumentNullException(nameof(furniture));
            _rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            _adj = adj ?? throw new ArgumentNullException(nameof(adj));
        }

        /// <summary>缓存是否有效(有效 ⇒ 下次 <see cref="Get"/> 命中)。</summary>
        public bool HasCache => _hasCache;

        /// <summary>缓存条目对应的**全序键**(= 使其失效的那条 `Structure*` 事件的键)。
        /// <para>仅在 <see cref="HasCache"/> 为 true 时有意义;从未收到 `Structure*` 事件时
        /// 为 `default`(Tick = 0)。</para></summary>
        public EventOrderKey CachedKey => _cachedKey;

        /// <summary>重算次数(缓存未命中计数)。</summary>
        public int RecomputeCount { get; private set; }

        /// <summary>命中次数(缓存有效且被读取)。</summary>
        public int HitCount { get; private set; }

        /// <summary>失效次数(被吸收的 `Structure*` 事件条数)。</summary>
        public int InvalidationCount { get; private set; }

        /// <summary>
        /// 吸收一条事件(事件流投递点)。
        /// <para>⚠️ <b>只对 `Structure*` 三支生效</b>(`StructurePlaced` / `StructureRemoved` /
        /// `StructureModified`,EC-24-05 的失效键闭集)。其余 Kind 是 **no-op** ——
        /// 尤其 `ActorCellEntered`(玩家 / AI 跨格,ADR-016 §三)与 `PoiStateChanged`
        /// **不得**触发 24 重算:它们不改布局,让它们失效 = 把帧率 / 移动速度耦合进 24 的算量。</para>
        /// <para>⚠️ 失效**单调**:任何一支都清缓存,不比较键的新旧。乱序到达只会多算一次。</para>
        /// </summary>
        /// <param name="e">刚入流的事件。</param>
        public void Apply(in SimEvent e)
        {
            if (!IsStructureKind(e.Kind)) return;

            _cachedKey = EventOrder.KeyOf(e);
            _hasCache = false;
            InvalidationCount++;
        }

        /// <summary>
        /// 取当前布局的求值结果:命中返回缓存,未命中重算并落缓存。
        /// </summary>
        /// <returns>求值快照(结构体)。</returns>
        public ClinicEnvEvaluation Get()
        {
            if (_hasCache)
            {
                HitCount++;
                return _cached;
            }

            // 每次现建快照视图 —— 持有它 = 持有陈旧缓存(见类备注)。
            var view = new StructureInstanceReadView(_layout, _modules);
            _cached = ClinicEnvironmentEvaluator.Evaluate(view, _furniture, _rooms, _contexts, _adj);
            _hasCache = true;
            RecomputeCount++;
            return _cached;
        }

        /// <summary>
        /// 无条件重算并**丢弃**缓存(诊断 / 对拍用 —— 生产路径不该需要它)。
        /// <para>用途 = 让「缓存值与直接求值是否逐位一致」可被测试直接观测,
        /// 而不用为了对拍再建第二个 memo 实例。</para>
        /// </summary>
        /// <returns>当下的直接求值结果(与 <see cref="Get"/> 的命中值逐位一致)。</returns>
        public ClinicEnvEvaluation Recompute()
        {
            _hasCache = false;
            return Get();
        }

        /// <summary>`Structure*` 三支的闭集判定(EC-24-05 的失效键)。
        /// <para>⚠️ 写成穷举 switch 而非 `kind.ToString().StartsWith("Structure")`:
        /// 后者对将来新增的 Kind 会**静默**纳入 / 排除,而本类的失效面必须是**显式**闭集
        /// (承 ADR-024 的白名单纪律)。</para>
        /// </summary>
        private static bool IsStructureKind(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.StructurePlaced:
                case EventKind.StructureRemoved:
                case EventKind.StructureModified:
                    return true;
                default:
                    return false;
            }
        }
    }
}
