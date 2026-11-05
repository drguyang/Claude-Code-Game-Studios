// 「医馆地块集」判据源 —— **从 23 的结构表派生**(2026-11-03 契约改造)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 权威来源
// ─────────────────────────────────────────────────────────────────────────────
//   Sim.Contracts/IClinicCellSource.cs —— 谓词契约 + 同源义务(本谓词 /
//     规则十「无人」/ F1PipelineInput.IsInClinic 绑同一实现)
//   GDD random-events.md 规则十二 12.1 / 规则六之二 / 规则十
//   **用户三项裁定(2026-11-03)**:口径 = **甲「建筑占地」**(非可走区 / 非生态区)·
//     **动态量**(玩家可建槽位建成后算)· 占地 = **两者都算**(建筑件占格 + 围出内部)
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔁 2026-11-03 契约改造:静态注入 → **从 registry 派生**
// ─────────────────────────────────────────────────────────────────────────────
//   **旧形态**(原文不删):`Configure(IEnumerable<WorldPos>)` 由装配期喂一个静态格集,
//     生产调用方**为零** ⇒ `IsClinicCell` 恒 false ⇒ 规则十二 12.1 的「跨出」恒不满足。
//   **新形态**:持有 `StructureInstanceRegistry` 的**只读引用**,每次查询时从结构表派生 ——
//     因为裁定②要求**动态量**(玩家建成的结构也要算),静态集无法表达。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 「建筑件」判据 = **`furn_type == null`**,不是 `SlotType`
// ─────────────────────────────────────────────────────────────────────────────
//   实测(`build_modules.json`,14 行):`SlotType` 粒度**过粗** —— `Decor` 同时含家具
//   (`6 drying_rack` / `7 chamber_pot`)与非家具(`9`)。⇒ 用 `SlotType` 会把晾药架 / 夜壶
//   判成「建筑件」。正确判据 = 模块**没有** `furn_type`(即 8 Shell / 9 Decor / 10 Tent /
//   11 Door / 12 Utensil / 13 ClinicPanel / 14 Switch)。
//   来源 = `BuildModulesCatalog.GetFurnType(int)`(21a 的 21 表经 23 目录暴露)。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ P0 的实测边界(逐条,禁借绿)
// ─────────────────────────────────────────────────────────────────────────────
//   ① **「围出内部」在 P0 结构性不存在**(2026-11-03 实测):8 件建筑件的占格分成
//      **4 个互不相连的连通分量**(`{(2,4)}` · `{(10,5)}` · `{(1,1)…(5,1)}` · `{(5,9)}`)
//      ⇒ **没有任何闭合墙** ⇒ 内部空间 = 空集。本类因此**不实现**多边形填充 ——
//      实现它只会得到「188 格全算内部」的假象(首版 flood fill 用不可走的边界格当种子,
//      种子为空 ⇒ 空洞正控 F4;改用连通分量判定后结论才成立)。
//      ⚠️ **填充分支留待 23 实现轮**:P0 关卡补出闭合墙后,本类须同批加上那半边。
//   ② ⇒ **本类今日的产出 = 8 格**(`(1,0,1)` `(2,0,1)` `(2,0,4)` `(3,0,1)` `(4,0,1)`
//      `(5,0,1)` `(5,0,9)` `(10,0,5)`)。它**不等于**「玩家在医馆里」——
//      只覆盖墙 / 门 / 面板自身所在的那几格。**不得当成「已接真源」。**
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 同源义务的**实际状态**(2026-11-03 实测,比头注原写更差)
// ─────────────────────────────────────────────────────────────────────────────
//   接口头注写「三处必须绑同一实现」。实测:
//     · ✅ `HouseCallStartedWriter`(`Sim/HouseCallStartedWriter.cs:153-154`)
//     · ✅ `F1ContextEvaluator`(`Sim/F1ContextEvaluator.cs`,2026-11-05 `TD-randomevents-05`)
//          —— `CompositionRoot` 的 `contextOf` 闭包只调它,桩点不再硬编码
//     · ❌ 规则十「无人」检测器 —— **尚未存在**
//   ⇒ 「同源」今日是**两点**,不是三点。第三点随规则十的检测器落地时接同一实例。
//     ⚠️ 两点之间**仍是同一个实例**(服务袋单点发放)—— 这是「同源」的实质要求;
//     「三点齐」是覆盖度,不是同源本身。
//
// ⚠️ 单实例纪律不变:本类实例由组合根创建、经服务袋(`CompositionRootServices.ClinicCells`)
//   单点发放 —— 另立实例 = 多处对同一 tick 给出不同回答(接口头注的同源禁令)。

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 「医馆地块集」判据源 —— 从 <see cref="StructureInstanceRegistry"/> **派生**(动态量)。
    /// </summary>
    public sealed class ClinicCellSource : IClinicCellSource
    {
        /// <summary>结构表只读引用(`null` = 未接世界 ⇒ 恒 false,见类头注)。</summary>
        private StructureInstanceRegistry _structures;

        /// <summary>建筑件判据(模块 id → 是否建筑件;`null` ⇒ 恒 false)。</summary>
        private System.Func<int, bool> _isBuildingModule;

        /// <summary>
        /// 构造(**未绑定**形态)。
        /// <para>⚠️ 为什么分两步:本实例在 `CompositionRoot` 里必须**早于**
        /// `structures` / `modulesCatalog` 构造(它被 `RandomEventsDirectorDriver` 在
        /// 同一段里立刻消费,而那两者的解析在更后面)。⇒ 先出**单实例**,后 <see cref="Bind"/>。
        /// **不是第二实例** —— 服务袋单点发放不变(接口头注的同源禁令仍成立)。</para>
        /// </summary>
        public ClinicCellSource()
        {
        }

        /// <summary>
        /// 绑定数据源(**只许一次**)。
        /// <para>⚠️ 二次绑定 ⇒ 抛(fail-closed):两次绑定意味着有人想把谓词接到两个不同的
        /// 结构表 —— 那正是「多处对同一 tick 给出不同回答」的同源禁令要防的形态。</para>
        /// </summary>
        /// <param name="structures">23 的结构实例表。</param>
        /// <param name="isBuildingModule">
        /// 模块 id → 是否建筑件。**生产** = `id => modulesCatalog.GetFurnType(id) == null`
        /// (`furn_type == null` 即建筑件 —— `SlotType` 粒度过粗,`Decor` 混装家具与非家具)。
        /// ⚠️ 收**委托**而非目录:目录类型是实现细节,拿它做 `is` 判断会让夹具替身
        /// 恒 false(F8 行为等价掩盖结构分叉的反面 —— 本轮实测踩到过)。
        /// </param>
        /// <exception cref="System.InvalidOperationException">已绑定过。</exception>
        public void Bind(StructureInstanceRegistry structures, System.Func<int, bool> isBuildingModule)
        {
            if (_structures != null || _isBuildingModule != null)
                throw new System.InvalidOperationException(
                    "ClinicCellSource 已绑定过 —— 谓词只许接一个结构表(同源禁令);" +
                    "换源须换新装配,不得就地重绑");
            _structures = structures;
            _isBuildingModule = isBuildingModule;
        }

        /// <summary>
        /// 格 ∈ 医馆地块?(建筑件占格 ∪ 围出内部 —— 后者 P0 恒空,见类头注 ①)。
        /// <para>⚠️ 每次查询**全表扫描**(O(结构数))—— P0 结构数是 9,可忽略;
        /// 规模上来后须加缓存(缓存是纯派生量,与 ADR-009 派生态同构,不引入第二真源)。</para>
        /// </summary>
        public bool IsClinicCell(WorldPos cell)
        {
            if (_structures == null || _isBuildingModule == null) return false;

            foreach (StructureInstance inst in _structures.EnumerateInstances())
            {
                if (!_isBuildingModule(inst.ModuleId)) continue;

                IReadOnlyList<WorldPos> occupied = inst.OccupiedCells;
                if (occupied == null) continue;
                for (int i = 0; i < occupied.Count; i++)
                    if (occupied[i] == cell) return true;
            }
            return false;
        }

        /// <summary>
        /// 医馆地块的格数(测试 / 接线观测面)。
        /// <para>⚠️ 与 <see cref="IsClinicCell"/> 用**同一条派生规则**(不分叉)。</para>
        /// </summary>
        public int DerivedCellCount
        {
            get
            {
                if (_structures == null || _isBuildingModule == null) return 0;
                var set = new HashSet<WorldPos>();
                foreach (StructureInstance inst in _structures.EnumerateInstances())
                {
                    if (!_isBuildingModule(inst.ModuleId)) continue;
                    IReadOnlyList<WorldPos> occupied = inst.OccupiedCells;
                    if (occupied == null) continue;
                    for (int i = 0; i < occupied.Count; i++) set.Add(occupied[i]);
                }
                return set.Count;
            }
        }
    }
}
