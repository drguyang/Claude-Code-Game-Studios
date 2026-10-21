// `IPlacedStructureReadView` 的**生产实现**(`TD-building-01` 读侧的另一半)。
//
// 权威来源:
//   `TD-building-01`(docs/tech-debt-register.md:2097)的「解除动作 · 读侧」逐字:
//     「给 `IPlacedStructureReadView` 一个生产实现(**由 `World.Structures` 包一层**)」
//   `Sim/Clinic/IPlacedStructureReadView.cs` 的接口契约(三成员 + 两条实测陷阱)
//   `Sim/World/StructureKinds.cs` 的 `StructureInstanceRegistry.EnumerateInstances()`(本轮新增)
//
// ⚠️ **本文件为什么住在 `Gameplay.Presentation`**:接口住 `Sim.Clinic`(门 A 侧),
//   数据源是 `Sim.World.StructureInstanceRegistry` —— 二者**同住 `Sim` 装配**,
//   理论上可以把实现放进 `Sim`。**但本实现还要 `IModuleCatalog` 才能把
//   `module_id`(i32)桥成 `GetModuleId()` 要的 `string`**,而目录的生产实现
//   (`BuildModulesCatalog`)住 `Gameplay.Presentation`(它读 cooked 字节)。
//   ⇒ 实现必须与目录同侧。这也让 `Sim` 保持零「读文件」依赖(门 A 的实质)。
//
// ⚠️ **`module_id`(i32) → `string` 的桥,是跨层词汇,不可推导**:
//   · `StructureInstance.ModuleId` = **i32**(`entities.yaml` / `WorldPayloads` / `StructureKinds` 一致)
//   · `IPlacedStructureReadView.GetModuleId()` = **`string`**(24 侧的 `type(c) ∈ FURN_TYPES` 判据)
//   · 桥 = `build_modules.json` 的 **`furn_type`** 列(`null` = 非家具)
//   ⇒ 两套表示**不能互相推导**(id 序数与家具词汇无关),必须经目录。
//   `furn_type == null` 的模块 ⇒ 本实现返回 **null**(接口契约:「无模块标识 ⇒ 按不在任何
//   家具闭集内处理」),**不是**返回 `module_id.ToString()`(那会让每个模块都「有标识」,
//   使非家具被误判为家具)。
//
// ⚠️ **陷阱 ① 的落实**:本实现按**实例**报 `GetOccupiedCells`(不逐格查 `GetInstanceAt`),
//   多格结构的非锚点格因此完整可见。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;   // StructureInstance / StructureInstanceRegistry

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>
    /// `IPlacedStructureReadView` 的生产实现:把 23 的 <see cref="StructureInstanceRegistry"/>
    /// 包成 24 要的**只读**结构视图。
    /// </summary>
    /// <remarks>
    /// <para>⚠️ **快照语义**(与 `ClinicFurnitureTraitsSource` 同款):构造期把注册表摊平成数组,
    /// 之后**不监听**任何事件。布局变更(建造 / 拆除)后须**重建**本对象 ——
    /// 自动跟随需要订阅 23 的写路径,那会让 24 从读方变成监听方(违 GDD 规则五)。</para>
    /// <para>⚠️ **枚举序不作契约**:内部 `Dictionary` 桶序经本类的摊平后**保持原序**,
    /// 但 24 侧的析出 / 匹配都已做规范排序,不依赖本序。测试若断言「第 0 件是哪件」,
    /// 那是**测试自身的脆弱性**,不是本类的契约。</para>
    /// </remarks>
    public sealed class StructureInstanceReadView : IPlacedStructureReadView
    {
        private readonly WorldPos[][] _cells;
        private readonly string[] _moduleIds;

        /// <summary>构造:一次性把注册表摊平成两个平行数组。</summary>
        /// <param name="registry">23 的结构实例表(构造期读取,之后不持有其引用)。</param>
        /// <param name="catalogue">
        /// 模块目录 —— **唯一**能把 `module_id`(i32)桥成 `furn_type`(string)的来源。
        /// 可传 null = 「无目录」⇒ 全部实例的 `GetModuleId()` 返回 null(按「无模块标识」处理)。
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="registry"/> 为 null。</exception>
        public StructureInstanceReadView(StructureInstanceRegistry registry,
                                         BuildModulesCatalog catalogue = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            int count = registry.Count;
            _cells = new WorldPos[count][];
            _moduleIds = new string[count];

            int i = 0;
            foreach (StructureInstance inst in registry.EnumerateInstances())
            {
                // 足迹:注册表已存**完整占用格**(F-23-2b 旋转后的世界格;`StructureWriter.Place`
                // 经 `PlaceableChecker.ComputeOccupiedCells` 算得并传入)。本类不重算 ——
                // 重算会产生第二真源,且与放置判定的足迹可能漂移。
                IReadOnlyList<WorldPos> occupied = inst.OccupiedCells;
                if (occupied == null || occupied.Count == 0)
                {
                    _cells[i] = Array.Empty<WorldPos>();
                }
                else
                {
                    var copy = new WorldPos[occupied.Count];
                    for (int c = 0; c < occupied.Count; c++) copy[c] = occupied[c];
                    _cells[i] = copy;
                }

                _moduleIds[i] = ResolveModuleId(inst.ModuleId, catalogue);
                i++;
            }
        }

        /// <inheritdoc />
        public int StructureCount => _cells.Length;

        /// <inheritdoc />
        public IReadOnlyList<WorldPos> GetOccupiedCells(int structureIndex) => _cells[structureIndex];

        /// <inheritdoc />
        public string GetModuleId(int structureIndex) => _moduleIds[structureIndex];

        /// <summary>
        /// `module_id`(i32)→ `furn_type`(string)。无目录 / 不在目录 / `furn_type == null`
        /// ⇒ 一律 **null**(= 「无模块标识」,接口契约的排除语义)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 返回 null 而**不是** `moduleId.ToString()`:后者会让每个模块都「有标识」,
        /// 使 `ClinicFurnitureTraitsSource` 把非家具误判成「在闭集里查不到」——
        /// 契约明文写「无模块标识 ⇒ null」,本实现照契约,不发明第三种表示。
        /// </remarks>
        private static string ResolveModuleId(int moduleId, BuildModulesCatalog catalogue)
            => catalogue?.GetFurnType(moduleId);
    }
}
