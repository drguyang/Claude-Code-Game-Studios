// 24 → 29 的复活床格查询**生产实现**(`IClinicReviveCell` 的生产命中;2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 权威来源
// ─────────────────────────────────────────────────────────────────────────────
//   GDD `design/gdd/clinic-machine.md` **规则六**(候选集 / `(y,x,z)` 字典序 min / 无候选返回无)
//   `Sim/Clinic/ClinicReviveCellRule.cs` —— 规则六的**纯判定**(本类不含任何判定,只做接线)
//   `Sim/RespawnCommandIssuer.cs` —— `IClinicReviveCell` 契约(29 侧消费;对实现零假设)
//   `Gameplay.Presentation/ClinicRoomMatchingService.cs` —— 同款「装配胶水住本侧」先例
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 为什么本类住在 `Gameplay.Presentation`(与 `ClinicRoomMatchingService` 同因)
// ─────────────────────────────────────────────────────────────────────────────
//   链上两个零件只能住装配侧:`StructureInstanceReadView` 与 `BuildModulesCatalog`
//   (二者要读 cooked 字节 / 目录,而 `Sim` 的引用集恰 = {BCL, Sim.Contracts},ADR-025 §①)。
//   ⇒ 把胶水放在与它们同侧,`Sim` 侧保持纯逻辑(零文件依赖)。
//   本类**零判定逻辑** —— 判据全部在 `Sim/Clinic/ClinicReviveCellRule.cs`。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 快照语义 = 刻意的(与 `StructureInstanceReadView` / `ClinicRoomMatchingService` 同款)
// ─────────────────────────────────────────────────────────────────────────────
//   **每次 `TryGet` 都从当下的注册表重建视图**,不持有快照实例 —— 建造 / 拆除会改注册表,
//   持有快照 = 持有陈旧缓存。代价 = 一次 O(|实例| × |足迹|) 摊平 + 一次房间析出,
//   在「死亡结算段末步」这个**每死一次才调一次**的调用频率上完全可接受
//   (⚠️ 它不是每帧路径 —— 若将来被搬进 tick 热路径,须另裁缓存策略,本件不提前造接缝)。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 单机 P0 的 actorId 语义(如实登记,不假装联机)
// ─────────────────────────────────────────────────────────────────────────────
//   `TryGet(int actorId, out WorldPos cell)` 的 `actorId` 在**单机 P0** 下只有一个玩家,
//   医馆是**全局布局** ⇒ 床格与 actorId **无关**(同一布局 ⇒ 同一格,规则六的纯函数性)。
//   多玩家共床是**规则六的明文结果**(「联机共床:两玩家同床格 ⇒ 同格两条命令」,
//   `RespawnCommandIssuer` 文件头),不是缺陷。
//   ⇒ 本实现对 `actorId` 只做**非负校验**,不做逐玩家分叉 —— 那是 45 的会话 / 分册面(P1b)。
//   ⚠️ **不得**因为「参数没用」就删掉它:契约形状由 29 侧定,删了会破 45 的扩展点。

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>
    /// 24 的复活床格查询:`IClinicReviveCell` 的生产实现(规则六的装配接线)。
    /// </summary>
    /// <remarks>
    /// <para><b>链的形状</b>(每一步都是既有纯函数 / 只读视图,本类零新增判据):</para>
    /// <list type="number">
    /// <item><c>StructureInstanceRegistry</c> → <see cref="StructureInstanceReadView"/>
    /// (经 <see cref="BuildModulesCatalog"/> 把 `module_id` 桥成 `furn_type`)</item>
    /// <item>视图 + <c>FURN_TYPES</c> 闭集 → <see cref="ClinicRoomExtraction.Extract(IPlacedStructureReadView, System.Collections.Generic.IReadOnlyCollection{string})"/>
    /// (**真判据入口** —— 代理版会把非家具结构算成家具格,可能桥接两个房间)</item>
    /// <item>析出结果(<c>RoomCells</c>)+ 视图 → 逐格 `furn_type` 查询(经视图的 `GetModuleId` + 目录)</item>
    /// <item>房间格 + 逐格类型 → <see cref="ClinicReviveCellRule.Select"/>(规则六 ②③)</item>
    /// </list>
    /// <para>⚠️ <b>无缓存、无状态</b>:不持有求值结果,不读墙钟 / 表现态(ADR-016 §一 三源不变量)。</para>
    /// </remarks>
    public sealed class ClinicReviveCellQuery : IClinicReviveCell
    {
        private readonly StructureInstanceRegistry _registry;
        private readonly BuildModulesCatalog _modules;
        private readonly ClinicFurnitureCatalogue _furniture;

        /// <summary>构造:三份输入全部必填(fail-loud —— 缺任一件链只能恒返回「无床」)。</summary>
        /// <param name="registry">23 的结构实例表(`World.Structures`)。</param>
        /// <param name="modules">模块目录(`build_modules.cooked` 的生产实现;`module_id` → `furn_type`)。</param>
        /// <param name="furniture">21a 家具目录(`FURN_TYPES` 闭集 —— 房间析出的真判据输入)。</param>
        /// <exception cref="ArgumentNullException">任一为 null(接线错误,非设计态)。</exception>
        public ClinicReviveCellQuery(StructureInstanceRegistry registry,
                                     BuildModulesCatalog modules,
                                     ClinicFurnitureCatalogue furniture)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _furniture = furniture ?? throw new ArgumentNullException(nameof(furniture));
        }

        /// <inheritdoc />
        /// <remarks>
        /// <para>⚠️ <b>无床 / 无房间 / 无家具目录标识 ⇒ 返回 false</b>,<paramref name="cell"/> 置
        /// `default`。**不得**在这里填兜底出生点 —— 兜底是 29 的裁决面(GDD 规则六 ③:
        /// 「24 不定义兜底格」)。</para>
        /// <para>⚠️ <b>返回值可与「(0,0,0) 的合法床格」区分</b>:本方法用 `bool` 承载「有/无」,
        /// 而非拿零值当哨兵(GDD 规则六 ③ 明写禁零值哨兵)。</para>
        /// </remarks>
        public bool TryGet(int actorId, out WorldPos cell)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "玩家 id 走机制 A 计数器,不得为负");

            // 每次重建(见类备注「快照语义 = 刻意的」)。
            var view = new StructureInstanceReadView(_registry, _modules);

            // 房间析出:真判据入口(与 `ClinicRoomMatchingService` 选同一条,不是代理版)。
            ClinicRoomExtractionResult extraction =
                ClinicRoomExtraction.Extract(view, _furniture.FurnTypes);

            if (!extraction.HasRoom)
            {
                // 无房间 ⇒ 无候选格。**不兜底**(兜底归 29)。
                cell = default;
                return false;
            }

            // 逐格家具类型:视图按**实例**报 `module_id`,而规则六要的是**逐格**类型。
            // ⇒ 先摊平成「格 → furn_type」的字典(同一格只能属一个实例,规则一的布局前提)。
            var furnTypeByCell = BuildCellFurnTypeMap(view);

            WorldPos? revive = ClinicReviveCellRule.Select(
                extraction.RoomCells,
                c => furnTypeByCell.TryGetValue(c, out string t) ? t : null);

            if (revive == null)
            {
                cell = default;
                return false;   // 有房间但无床类家具格 ⇒ 「本布局给不出复活点」
            }

            cell = revive.Value;
            return true;
        }

        /// <summary>
        /// 把「按实例的只读视图」摊平成「格 → `furn_type`」。
        /// </summary>
        /// <remarks>
        /// ⚠️ 同一格被两个实例占用不可能(规则一的布局前提)⇒ 无需 tie-break;
        /// 若输入违约(重叠),**先写者胜**(不是契约,是防御性行为 —— 重叠本身已是布局错误)。
        /// ⚠️ `GetModuleId` 返回 null(无模块标识 / 不在目录)⇒ **跳过该格**(按「不在任何家具
        /// 闭集内」处理,接口契约明文;不是错误,但也不是床)。
        /// </remarks>
        private static System.Collections.Generic.Dictionary<WorldPos, string> BuildCellFurnTypeMap(
            StructureInstanceReadView view)
        {
            var map = new System.Collections.Generic.Dictionary<WorldPos, string>();
            for (int i = 0; i < view.StructureCount; i++)
            {
                string furnType = view.GetModuleId(i);
                if (furnType == null) continue;

                System.Collections.Generic.IReadOnlyList<WorldPos> cells = view.GetOccupiedCells(i);
                for (int c = 0; c < cells.Count; c++)
                {
                    if (!map.ContainsKey(cells[c])) map.Add(cells[c], furnType);
                }
            }
            return map;
        }
    }
}
