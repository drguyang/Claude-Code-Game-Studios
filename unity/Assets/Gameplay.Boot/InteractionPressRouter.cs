// 2026-11-02 · 开册入口后续轮 —— 结清 `TD-interaction-01`(4 的确定性全序目标选择落成唯一一处)
//
// ── 病因(债册 `TD-interaction-01` 原文)──────────────────────────────────────
// `interaction-system.md` 规则三 / `F-4.1`:**全案唯一目标选择器**,
// `target = argmin ⟨ d∞, KindPriority, StableId ⟩`,候选来自**全部种类**。
// 实装却是两处互不知情的判据:
//   ① 采集 —— `HarvestInteractionDriver` 走真 `InteractionSelector`,但候选集只有资源点;
//   ② 就诊 —— `InteractionInputDriver.NearestPresentPatient` 自算「视觉位置平方距离最小」
//      (**浮点 · 表现态** —— 违 ADR-016 §三),没有任何全序键;
//   ③ 两者靠 `InteractClaimRoute` 的**外挂比距**仲裁(2026-11-02 加,只比 `d∞`)。
//
// ── 现口径(本类)────────────────────────────────────────────────────────────
// **一次 `Select` 定种类**,然后按 `InteractTarget.Kind` 分派:
//   Patient     → `_visit(patientId)`(39 的就诊:立案 → 查体 → 开册)
//   ForageSpot  → `HarvestInteractionDriver.TryHarvestTarget`(17)
//   Drop        → `DropClaimOrchestrator.TryClaimInstance`(20;结算被选中的 instance_id)
//   BuildSlot   → `_aimBuild(cell)`(23;进建造模式并对准该格 —— 见 `BuildInputDriver.AimAt`)
//   PoiCell     → **本类不分派**:4 的 `InteractionSelector` 在选中那一刻已向
//                `IDiscoveryReporter` 发请求(6 的 `PoiStateMachine.TryDiscover`),
//                故 `case PoiCell` 返回 true(按键确实被消费)
//   其它种类    → **不消费**(未接线种类,按键落空 —— 诚实降级,不静默转成别的动作)
//
// 🔁 2026-11-02(`TD-interaction-02`):**Drop 已进候选集** —— 第一源(`GroundDropSource`,
//   读「未归属地面实例 + 最近一次 `DropSpawned.spawn_anchor`」)已生产装配,
//   路由里「选择器空 ⇒ 拾取兜底」那一段**已删除**,Drop 由 `Select` 直接选中,
//   再交给 `DropClaimOrchestrator.TryClaimInstance(被指名的 instance_id)` 结算。
//   ⇒ 20 不再自己挑「第一可拾」(那曾是 4 之外的第二处判据)。
//
// 🔁 2026-11-02(`TD-interaction-03`):**第二源补齐三切片**(POI / 采集点 / 建造槽位),
//   POI 的消费面由生产 `PoiDiscoveryReporter` → 6 的 `PoiStateMachine` 接通,
//   建造槽位由 `BuildInputDriver.AimAt` 接通。
//
// ── 仍然不是 F-4.1 的全部(如实登记)──────────────────────────────────────────
// `Utensil` / `ClinicPanel` / `Door` / `Switch` / `Container` 五种**没有数据行**
// ( `build_modules` 只有 Bed/Table/Cabinet/Decor/Shell/Tent;`world_buildslots` 只有
// Decor/Shell/Tent )⇒ `BakedInitial` 源在生产装配里仍为 `null`,它们不进候选集。
// 要它们可交互得先有 23 的数据模型 —— 登记在 `TD-interaction-03` 的残余里。
//
// ⚠️ 本类**不持游戏状态**:只有 readonly 依赖 + 一个观测位(`LastRoutedKind`)。
// ⚠️ 玩家格 = **经流确立的格**(调用方注入;ADR-020 §四),本类不读 `transform.position`。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// E 按下沿的**统一路由**:一次 `InteractionSelector.Select` 决定「这一下打给谁」,
    /// 再按 <see cref="InteractableKind"/> 分派到 39 / 17 / 20。
    /// </summary>
    public sealed class InteractionPressRouter
    {
        private readonly Func<WorldPos> _playerCell;
        private readonly Func<long> _tick;
        private readonly int _actorId;
        private readonly CandidateSetLoader _candidates;
        private readonly InteractionSelector _selector;
        private readonly InteractionRadius _radius;
        private readonly DropClaimOrchestrator _drops;
        private readonly HarvestInteractionDriver _harvest;
        private readonly Action<PatientId> _visit;

        /// <summary>建造槽位的消费面(23;2026-11-02 · `TD-interaction-03`;可 null = 未接线)。
        /// <para>实参 = `BuildInputDriver.AimAt` —— 进建造模式并对准该槽位格。
        /// ⚠️ 本类**不放置**:E 只瞄准,放置键仍归 23。</para></summary>
        private readonly Action<WorldPos> _aimBuild;

        /// <summary>最近一次路由选中的种类(<c>null</c> = 未选中);观测 / 夹具面。</summary>
        public InteractableKind? LastRoutedKind { get; private set; }

        /// <summary>构造路由。</summary>
        /// <param name="playerCell">玩家**经流确立格**来源(禁表现态位置)。</param>
        /// <param name="tick">当前逻辑 tick 来源。</param>
        /// <param name="actorId">玩家 id(17 / 20 的归因面)。</param>
        /// <param name="candidates">候选集装载器(**含病人源**的那个 —— 与采集自用的不是同一个实例)。</param>
        /// <param name="selector">4 的目标选择器(F-4.1 三键全序)。</param>
        /// <param name="radius">交互半径单源持有者(裁剪用;与选择器同一实例)。</param>
        /// <param name="drops">拾取编排(可 null = 未装配)。</param>
        /// <param name="harvest">采集驱动(可 null = 未装配)。</param>
        /// <param name="visit">就诊回调(39 的立案 + 查体 + 开册;可 null = 未接线)。</param>
        /// <param name="aimBuild">建造槽位回调(23 的进建造模式 + 对准该格;可 null = 未接线 ——
        /// 那时选中 BuildSlot 返回 false,按键落空而不是被静默吞掉)。</param>
        /// <exception cref="ArgumentNullException">前三者或 candidates / selector / radius 为 null。</exception>
        public InteractionPressRouter(
            Func<WorldPos> playerCell,
            Func<long> tick,
            int actorId,
            CandidateSetLoader candidates,
            InteractionSelector selector,
            InteractionRadius radius,
            DropClaimOrchestrator drops,
            HarvestInteractionDriver harvest,
            Action<PatientId> visit,
            Action<WorldPos> aimBuild = null)
        {
            _playerCell = playerCell ?? throw new ArgumentNullException(nameof(playerCell));
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
            _actorId = actorId;
            _candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _radius = radius ?? throw new ArgumentNullException(nameof(radius));
            _drops = drops;
            _harvest = harvest;
            _visit = visit;
            _aimBuild = aimBuild;
        }

        /// <summary>
        /// 处理一次 E 按下沿。
        /// </summary>
        /// <returns>true = 按键被消费(病人 / 采集点 / 掉落);
        /// false = 未选中任何候选,或选中的种类尚未接线 —— 调用方不得再触发别的动作。</returns>
        public bool OnPress()
        {
            WorldPos cell = _playerCell();
            long tick = _tick();
            var intent = new InteractIntent(cell, true, tick);

            // 掉落族在**装载期**就套上 20 R7 的回捡半径(与结算同一道门、同一个数)——
            // 否则「在交互半径内但在回捡半径外」的掉落会赢得全序、结算时又被拒,
            // 把这一下按键静默吞掉。见 `CandidateSetLoader.LoadWithin` 第三形参。
            IReadOnlyList<Candidate> within =
                _candidates.LoadWithin(in cell, _radius.Value, _drops?.Tolerance);
            InteractTarget target = _selector.Select(in intent, within);

            LastRoutedKind = target.HasTarget ? target.Kind : (InteractableKind?)null;

            // 🔁 2026-11-02(`TD-interaction-02`):**兜底段已删** —— Drop 现在就在候选集里,
            //    「选择器一无所获」= 真的没东西可交互(不得再偷偷去摸一件掉落)。
            if (!target.HasTarget)
                return false;

            switch (target.Kind)
            {
                case InteractableKind.Patient:
                    if (_visit == null) return false;
                    _visit(new PatientId((int)target.StableId));
                    return true;

                case InteractableKind.ForageSpot:
                    if (_harvest == null) return false;
                    return _harvest.TryHarvestTarget(in target, in intent, _actorId).Attempted;

                case InteractableKind.BuildSlot:
                    // 23 的入口:E 进来 = 「我要在这一格建」⇒ 进模式 + 对准该格(不放置)。
                    if (_aimBuild == null) return false;
                    _aimBuild(target.Cell);
                    return true;

                case InteractableKind.PoiCell:
                    // 4 的出境已在 `Select` 内发生(发现请求 → 6);本类无事可做但要记「已消费」。
                    return true;

                case InteractableKind.Drop:
                    // 结算**被选中的那一件**(StableId = instance_id;不再「第一可拾」)。
                    return _drops != null
                           && _drops.TryClaimInstance(_actorId, tick, cell, target.StableId);

                default:
                    // 未接线种类:不消费、不转译成别的动作(静默降级是假绿的温床)。
                    return false;
            }
        }
    }
}
