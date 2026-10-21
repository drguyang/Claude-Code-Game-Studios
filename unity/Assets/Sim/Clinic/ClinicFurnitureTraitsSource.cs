// 权威来源:
//   GDD design/gdd/clinic-machine.md `:161-162`(S 成员判据 `type(c) ∈ FURN_TYPES` · `tier(c)` · `e_env(type)`)
//   GDD `:297`(FURN_TABLE 归 21a)· O-24-1 / TD-clinic-01(O-24-1 的兑现件之一)
//   ADR-015 §三(整数格)· ADR-005(纯派生,可重放)
//
// ⚠️ **这是 `IClinicFurnitureTraitsSource` 的【生产实现】** —— 在它出现之前,求值器只能吃
//   测试注入的夹具源(`FurnTraitsProductionReady == false` 哨兵)。本件把那条缺口补上:
//   输入 = 23 的只读结构视图(`module_id` + 占用格),输出 = 三值格属性查询。
//
// ⚠️ **快照语义(如实登记)**:本类型在**构造时**把布局摊平成两张索引,之后不监听任何事件
//   ⇒ 它是「某一时刻布局」的快照。布局变化(建造 / 拆除)后须**重建**本对象。
//   不自动跟随是刻意的:自动跟随需要订阅 23 的写路径 = 从读方变成监听方,破坏
//   「24 只读不持占用真相」(GDD 规则五)。装配层负责在布局变更后重建。
//
// ⚠️ **三值语义(承接口文件头)**:`Unknown` **只在**「该格没有任何实例占用」时返回 ——
//   那是「我不知道这格是什么」,求值器会硬失败(禁静默跳过)。已占用但模块 id 不在目录里的格
//   = `NonFurniture`(确定「不是家具」),因为 `type(c) ∉ FURN_TYPES` 就是 GDD 的非格判据。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>
    /// 生产版 <see cref="IClinicFurnitureTraitsSource"/>:按 23 的只读结构视图 + 21a 家具目录
    /// 回答每格的 `type(c)` / `tier(c)` / `e_env(type)`。
    /// </summary>
    public sealed class ClinicFurnitureTraitsSource : IClinicFurnitureTraitsSource
    {
        private readonly Dictionary<WorldPos, ClinicFurnitureTraits> _furniture;
        private readonly HashSet<WorldPos> _occupiedNonFurniture;

        /// <summary>21a 家具目录(构造期已校验的行集)。</summary>
        public ClinicFurnitureCatalogue Catalogue { get; }

        /// <summary>构造:一次性把布局摊平成索引(同一输入 ⇒ 同一索引,无随机源)。</summary>
        /// <param name="view">23 的只读结构视图(构造期读取,之后不持有其引用 —— 只留摊平结果)。</param>
        /// <param name="catalogue">21a 家具目录(`FURN_TYPES` 闭集 + 逐件 tier / e_env)。</param>
        /// <exception cref="ArgumentNullException">注入缺失(接线错误,非设计态)。</exception>
        public ClinicFurnitureTraitsSource(IPlacedStructureReadView view, ClinicFurnitureCatalogue catalogue)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            Catalogue = catalogue;

            _furniture = new Dictionary<WorldPos, ClinicFurnitureTraits>();
            _occupiedNonFurniture = new HashSet<WorldPos>();

            int count = view.StructureCount;
            for (int i = 0; i < count; i++)
            {
                string moduleId = view.GetModuleId(i);
                IReadOnlyList<WorldPos> cells = view.GetOccupiedCells(i);
                if (cells == null) continue;

                bool isFurniture = catalogue.TryGet(moduleId, out ClinicFurnitureEntry entry);
                for (int c = 0; c < cells.Count; c++)
                {
                    WorldPos cell = cells[c];
                    if (isFurniture)
                    {
                        // 同一格被两件家具占用 = 23 的违约(占用互斥)。如实取**先者**会让结果依赖
                        // 实例报告序 ⇒ 抛,而不是静默择一。
                        if (_furniture.ContainsKey(cell) || _occupiedNonFurniture.Contains(cell))
                            throw new InvalidOperationException(
                                $"格 ({cell.X},{cell.Y},{cell.Z}) 被多件结构实例占用 —— " +
                                "23 的占用互斥违约;择一入库会让属性依赖实例报告序(不确定)");
                        _furniture.Add(cell, new ClinicFurnitureTraits(entry.Type, entry.Tier, entry.EEnv));
                    }
                    else
                    {
                        if (_furniture.ContainsKey(cell))
                            throw new InvalidOperationException(
                                $"格 ({cell.X},{cell.Y},{cell.Z}) 被家具与非家具实例同时占用 —— 同上");
                        _occupiedNonFurniture.Add(cell);
                    }
                }
            }
        }

        /// <inheritdoc />
        public ClinicCellLookup Lookup(WorldPos cell, out ClinicFurnitureTraits traits)
        {
            if (_furniture.TryGetValue(cell, out traits)) return ClinicCellLookup.Furniture;
            traits = default;
            if (_occupiedNonFurniture.Contains(cell)) return ClinicCellLookup.NonFurniture;
            // 该格没有任何实例占用 ⇒ **我不知道**(禁静默答「不是家具」)。
            return ClinicCellLookup.Unknown;
        }
    }
}
