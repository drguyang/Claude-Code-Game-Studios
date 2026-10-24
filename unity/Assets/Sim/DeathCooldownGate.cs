// story-004 · GDD death-and-respawn.md F-29-2 —— 死亡冷却门(从世界流重算 · 零计数器)。
//
// 权威来源:
//   design/gdd/death-and-respawn.md:297 —— `DeathAllowed(actor, t) ⟺ (t − last_death_tick(actor)) ≥ DEATH_COOLDOWN`
//   design/gdd/death-and-respawn.md:299 —— `last_death_tick(actor) := max{ Tick(e) : e ∈ 世界流 ∧ Kind(e) = PlayerDied ∧ actor(e) = actor }`
//   design/gdd/death-and-respawn.md:304 —— `DEATH_COOLDOWN = 2400` tick(= 120 s @ 20 Hz,2026-10-06 代拍,值可调);**须 > 0**
//   design/gdd/death-and-respawn.md:336 —— EC 行「冷却内再次致死」:免罚但继续,**零事件、连 `PlayerDied` 都不发**(OQ-29-4 已裁 2026-10-07)
//   production/epics/death-respawn/story-004-death-cooldown-from-stream.md —— AC-29-15 / 谓词边界 / 空流首死 / 实现注 1·4·5
//   design/registry/entities.yaml —— `DEATH_COOLDOWN` 常量块(2026-10-24 登记)
//   ADR-005 —— 状态从流重算(零独立计数器)
//   ADR-016 §一 —— 三源不变量(本类的全部输入 = 事件流 + 注入的配置值,无第四来源)
//
// ── 核心机制 ──────────────────────────────────────────────────────────────
//   **零计数器**:本类**不持有** `last_death_tick` 的任何缓存 —— 每次求值都现读世界流,
//   按 `Kind = PlayerDied` 过滤、按载荷 `actor_id` 归属、取 `e.Tick` 的最大值。
//   缓存一份 = 与真源失步的第二载体(ADR-005 单一真源),且读档 / 主机迁移后不可重构。
//   本类实例字段只有三样:**流视图委托 / 解码器 / 冷却长度(配置标量)** —— 前两者是
//   查询面,后者是注入值,没有一样是「某个 actor 的死过没死」。
//
//   **空流 = 首死恒允许**:`last_death_tick` 取「不存在」语义(不是某个哨兵数值),
//   故 `TryLastDeathTick` 返回 false 时 `DeathAllowed` 恒 true —— 且**不发任何哨兵事件**
//   (story-004 AC 逐字:「空流(首死):`last_death_tick = −∞` 语义 ⇒ 首个 candidate 恒允许
//   (不发哨兵事件)」)。
//
//   **边界是 `≥`**(`== DEATH_COOLDOWN` 放行,`== DEATH_COOLDOWN − 1` 拒绝)——
//   GDD `:297` 的 `⟺` 逐字;story-004 AC 亦逐字「谓词边界:`t − last == DEATH_COOLDOWN` 放行;
//   `−1` 拒绝」。
//
// ⚠️ **本类只回答「允许不允许」,不执行「不允许时做什么」** ——
//   拒绝路径的**静默性**(零事件、零代价结算、零传送)由调用方保证:
//   `PlayerDiedWriter.TryWrite` 见 `deathAllowed == false` 即返回 `RejectCooldown` 且零写入,
//   而「不结算 / 不传送」是 story-005 的调用点收敛义务。本类若自行发「被拒绝」事件,
//   就等于在冷却拒绝路径上制造事件 —— 那正是 GDD `:336` 明文禁止的形态。
//
// ⚠️ **`e.Tick`(事件头)而非载荷 `tick`**:GDD `:299` 逐字写 `max{ Tick(e) }`。
//   载荷里的 `tick` 由 `PlayerDiedWriter` 同批写入、当前恒等于事件头 ——
//   但它是**同一量的第二处存储**,本类不拿它做判据(否则两处一旦分叉,冷却读数静默漂移)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 死亡冷却门 —— F-29-2 的谓词实现(<c>DeathAllowed(actor, t)</c>)。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么住 <c>Sim</c></b>:它是 ADR-005 意义上的「状态从流重算」纯谓词,
    /// 与 <see cref="FoldedPredicate"/> / <see cref="CaseStreamQuery"/> 同族;
    /// 引用集仍恰 = {BCL, <c>Sim.Contracts</c>}(门 A 不受影响)。</para>
    /// <para><b>非线程安全</b>:与 sim 的单线程 tick 模型一致(ADR-005 主机唯一 Step)。</para>
    /// </remarks>
    public sealed class DeathCooldownGate
    {
        /// <summary>
        /// `DEATH_COOLDOWN` 的**实现期取值** = 2400 tick(= 120 s @ 20 Hz)。
        ///
        /// <para>✅ 2026-10-06 代拍裁定,值可调(`design/gdd/death-and-respawn.md:304`);
        /// 登记真源 = <c>design/registry/entities.yaml</c> 的 <c>DEATH_COOLDOWN</c> 常量块
        /// (2026-10-24 补登记 —— 此前它只活在该 GDD 正文与 `PlayerDied` 的有界性措辞里)。</para>
        ///
        /// <para>⚠️ <b>为什么本处也有一个字面量</b>(与 `StateMachine.TREATMENT_WINDOW` 同格):
        /// 常量块是**跨文档一致性登记**,本 const 是**实现取值**;两者同批落盘、语义同一。
        /// 不是第二真源 —— 但改值时**必须两处同改**,否则「登记说 2400、代码跑别的」
        /// 会静默分叉。</para>
        ///
        /// <para>⚠️ <b>ADR-014 的配置注入路径尚未接</b>(story-004 实现注 5 要求
        /// `DEATH_COOLDOWN_TICKS` 走 ADR-014 的 int 字段):当前无 <c>death_cooldown</c>
        /// 的 JSON 作者态 / 烘焙产物,故本值以 ctor 默认参数的形式提供注入缝 ——
        /// **构造方可以给别的值**(测试用符号化值),生产装配取默认。
        /// 数据面接线归数值轮(与「值可调」同源)。</para>
        /// </summary>
        public const long DEATH_COOLDOWN = 2400L;

        private readonly Func<IReadOnlyList<SimEvent>> _worldStream;
        private readonly IPayloadDecoder _decoder;
        private readonly long _cooldownTicks;

        /// <summary>
        /// 构造冷却门。
        /// </summary>
        /// <param name="worldStream">事件流只读视图(每次求值现取 —— 流会增长,**不缓存**)。</param>
        /// <param name="decoder">载荷解码器(ADR-031 第八抽象点 —— <c>Sim</c> 读载荷的唯一合法路径)。
        /// <c>PlayerDied</c> 的事件头 <c>Patient = PatientId.None</c>(ADR-007 §四)⇒
        /// 归属只能从**载荷**的 <c>actor_id</c> 取,故本类必须能解码。</param>
        /// <param name="cooldownTicks">冷却长度(tick);默认 <see cref="DEATH_COOLDOWN"/>。
        /// 测试用符号化值(如 10)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="cooldownTicks"/> ≤ 0 —— **显式配置错误,非静默**。
        /// GDD `:304` 硬约束:`须 > 0,否则「无限刷死」= 掉落经济漏洞`
        /// (0 或负值 ⇒ 谓词恒真 ⇒ 冷却退化为不存在,而外观上「门还在」)。
        /// </exception>
        public DeathCooldownGate(
            Func<IReadOnlyList<SimEvent>> worldStream,
            IPayloadDecoder decoder,
            long cooldownTicks = DEATH_COOLDOWN)
        {
            _worldStream = worldStream ?? throw new ArgumentNullException(nameof(worldStream));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));

            if (cooldownTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(cooldownTicks), cooldownTicks,
                    "DEATH_COOLDOWN 须 > 0(design/gdd/death-and-respawn.md:304)—— " +
                    "0 或负值会让 DeathAllowed 恒真,冷却静默退化为不存在,而门的外观完好");

            _cooldownTicks = cooldownTicks;
        }

        /// <summary>本实例使用的冷却长度(tick)—— 观测面(装配 / 测试读它,不参与判据)。</summary>
        public long CooldownTicks => _cooldownTicks;

        /// <summary>
        /// `DeathAllowed(actor, t) ⟺ (t − last_death_tick(actor)) ≥ DEATH_COOLDOWN`。
        /// </summary>
        /// <param name="actor">受伤实体 id(走 ADR-016 §二 与病人 / 敌人共用的 id 空间)。</param>
        /// <param name="tick">求值时刻(逻辑 tick,20 Hz 量纲)。</param>
        /// <returns>
        /// 该 actor 在 <paramref name="tick"/> 是否**允许**死亡结算。
        /// 该 actor 在世界流里**从未**有过 <c>PlayerDied</c> ⇒ 恒 <c>true</c>(首死恒允许)。
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="actor"/> &lt; 0 —— fail-loud,不猜。<c>PatientId.None(-1)</c> 是
        /// 「世界级事件」哨兵,不是可冷却对象;当作某个 actor 会静默给出错误答案
        /// (与 <see cref="FoldedPredicate.Folded"/> 对负 id 的处置同格)。
        /// </exception>
        public bool DeathAllowed(int actor, long tick)
        {
            if (actor < 0)
                throw new ArgumentOutOfRangeException(nameof(actor), actor,
                    "冷却谓词的 actor 不得为负 —— PatientId.None(-1)是「世界级事件」哨兵,不是可冷却对象");

            if (!TryLastDeathTick(actor, out long lastDeathTick))
                return true; // 空集 = 首死恒允许(不发哨兵事件)

            return tick - lastDeathTick >= _cooldownTicks;
        }

        /// <summary>
        /// `last_death_tick(actor) := max{ Tick(e) : e ∈ 世界流 ∧ Kind(e) = PlayerDied ∧ actor(e) = actor }`。
        /// </summary>
        /// <param name="actor">受伤实体 id(非负)。</param>
        /// <param name="lastDeathTick">该 actor 最近一次 <c>PlayerDied</c> 的事件头 tick;
        /// 返回 <c>false</c> 时该出参为 <see cref="long.MinValue"/>(调用方**不应**依赖它 ——
        /// 「不存在」由返回的布尔承载,不由某个数值哨兵承载)。</param>
        /// <returns>该 actor 在世界流里是否存在至少一条 <c>PlayerDied</c>。</returns>
        /// <remarks>
        /// <para><b>为什么按 <c>Kind</c> 过滤而不按流别再筛一遍</b>:<c>Kind → StreamId</c> 是
        /// 纯函数且 A1 断言唯一流别(ADR-024)—— <c>PlayerDied</c> 只可能在世界流里。
        /// 再筛一次流别 = 同一事实的第二份判据,两处可分叉。</para>
        /// <para><b>为什么不缓存</b>:缓存流前缀 = 一份可与真源失步的副本,且读档 / 主机迁移后
        /// 不可重构(ADR-005)。本谓词不在热路径上(每 tick 至多一次 per 玩家)。</para>
        /// </remarks>
        public bool TryLastDeathTick(int actor, out long lastDeathTick)
        {
            if (actor < 0)
                throw new ArgumentOutOfRangeException(nameof(actor), actor,
                    "冷却谓词的 actor 不得为负 —— PatientId.None(-1)是「世界级事件」哨兵,不是可冷却对象");

            lastDeathTick = long.MinValue;

            IReadOnlyList<SimEvent> events = _worldStream();
            if (events == null) return false;

            bool found = false;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.PlayerDied)
                    continue;

                PlayerDiedPayload payload =
                    _decoder.Decode<PlayerDiedPayload>(EventKind.PlayerDied, e.Payload);

                if (payload.ActorId != actor)
                    continue;

                // 取事件头 tick(GDD :299 逐字 `max{ Tick(e) }`),不取载荷同名字段。
                if (!found || e.Tick > lastDeathTick)
                {
                    lastDeathTick = e.Tick;
                    found = true;
                }
            }

            return found;
        }
    }
}
