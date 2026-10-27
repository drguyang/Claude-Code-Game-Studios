// 最小合成世界 —— **装配期**的世界实例工厂(S20-17 · 2026-10-28)。
//
// ═══════════════════════════════════════════════════════════════════════════
// 为什么需要这个文件
// ═══════════════════════════════════════════════════════════════════════════
// `CompositionRoot.cs:787-796` 逐字记着:23 的 `StructureInstanceRegistry` 在生产树
// **零构造**(`new World` 的构造同样零构造),并指名「`BootRoot` 的 World 场景装配轮」。
// `impl-s20-2a-building01-2026-10-25.md:176` 的 N5 条把这件事记为「本件未做」,
// 而 `TD-building-01` 已于 2026-10-25 结案 ⇒ 该缺口今日**零载体**。
// 本文件即该轮的第一块:让 `World` 在生产装配路径上**存在**。
//
// ═══════════════════════════════════════════════════════════════════════════
// 为什么是「合成」而不是「装载」—— 这一条必须读懂再改
// ═══════════════════════════════════════════════════════════════════════════
// 世界几何的真源 = ADR-022 关卡工具产出的 `world_geometry.json` → ADR-014 烘焙 →
// `world_*.cooked`。今日**三样全无**:`assets/data/` 零份 `world_*` JSON、
// `unity/Assets/DataCooked/` 的 25 份 cooked 产物里零份世界数据,
// 而关卡工具已被裁定「不立 epic、不排期」(`p0-remaining-roadmap-2026-10-20.md:1088`)。
// ⇒ 装配轮**没有可装载的输入**。用户 2026-10-28 裁定:边界取「最小合成世界」。
//
// ⚠️ **本工厂不是在给 `WorldGeometry` 开后门**。`WorldLattice.cs:295-298` 的
//    `WorldGeometry` 文档逐字写着「刻意**没有**『按尺寸造一个空白世界』的入口:
//    未装载的世界若默认可走,烘焙缺数据就从『启动期硬失败』退化成『全世界默认可走』
//    的静默失败」。那条约束**仍然成立,本文件不修改它** —— `WorldGeometry` 依旧只从
//    成形数组构造,依旧在装载期做量程 / 长度校验。
//    差别在于**谁在承担「我知道这是合成的」这件事**:
//      · `WorldGeometry` 的空白入口会让**任何**调用方都悄悄拿到一个可走世界;
//      · 本工厂只服务**装配期这一处**,且强制配一条响亮日志(见下)。
//
// ⚠️ **响亮登记(禁静默)**:`Create()` 每次都打一条 `LogWarning`。理由 ——
//    一个全 walkable 的 16×1×16 世界与「真世界」在 `IsEffectivelyWalkable` 上不可区分,
//    若不打日志,将来「世界变成空的 / 建造永远合法」这类症状会被误诊为逻辑缺陷,
//    而不是「世界数据从未产出」。这条日志就是那个诊断锚点。
//    `BootRoot` 另置 `IsSyntheticWorld` 可观测属性(PlayMode 可断言)。
//
// ═══════════════════════════════════════════════════════════════════════════
// 边界(本卡不解除的,逐条如实)
// ═══════════════════════════════════════════════════════════════════════════
// · `BuildSlotCatalog` 是**空目录**(`RegisterSlot` 零调用)⇒ `IsInBuildSlotRegion`
//   恒 false ⇒ `AC-23-08` 第三半边(占用格 ⊆ `BuildSlotRegion`)**仍无载体**。
// · 地形全 walkable + POI / 资源格全 `-1` ⇒ `EcozoneOf` / 资源点查询在本世界上
//   只有「没有」这一个答案 —— 这不是「已实现」,是「没有数据」。
// · 本工厂**不**提供任何写入面:世界内容只能由后续关卡工具轮替换。

using System;
using DaYiJingCheng.Sim.World;
using UnityEngine;   // Debug.LogWarning(本文件属装配层,门 A 不约束)

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 装配期最小合成世界工厂(S20-17)。
    /// <para>产出 <see cref="World"/>:16×1×16 全 walkable 地形 + 空槽位目录 +
    /// 空结构实例表。用地取代「世界烘焙数据」—— 后者今日零产出,唯一作者
    /// ADR-022 关卡工具已被裁定不排期。</para>
    /// <para>⚠️ <b>每次调用都打一条 <see cref="Debug.LogWarning(string)"/></b>:合成世界
    /// 与真世界在可走性查询上不可区分,不登记就会被误诊为逻辑缺陷。详见文件头。</para>
    /// </summary>
    public static class MinimalWorldFactory
    {
        /// <summary>合成世界的 X 轴格数。</summary>
        /// <remarks>选 16 而非 1:单格世界会让「足迹格 / 非锚点格」类判据退化成
        /// 同义反复(`place` 与 `OCCUPY` 落在同一格)。16×16 容得下一个 2×2 模组的整足迹。</remarks>
        public const int SizeX = 16;

        /// <summary>合成世界的 Y 轴格数。</summary>
        /// <remarks>1 = 单层平面。P0 的医馆是平面为主形态(`technical-preferences.md` §XR
        /// 「平面为主形态」),不做多层世界。</remarks>
        public const int SizeY = 1;

        /// <summary>合成世界的 Z 轴格数。</summary>
        public const int SizeZ = 16;

        /// <summary>建造一个最小合成世界。</summary>
        /// <returns>新的 <see cref="World"/> 实例(每次调用都是独立实例 —— 两份注册表
        /// 就是两份真源,调用方须自己保证只建一次)。</returns>
        public static World Create()
        {
            int total = SizeX * SizeY * SizeZ;

            // 全 walkable 地形。kSpeedIdx 取 0(合法下标;本世界的 TerrainSpeedRow 表
            // 不参与任何判据 —— 没有消费者读它,见文件头「边界」)。
            var terrain = new TerrainCell[SizeX, SizeY, SizeZ];
            for (int x = 0; x < SizeX; x++)
                for (int y = 0; y < SizeY; y++)
                    for (int z = 0; z < SizeZ; z++)
                        terrain[x, y, z] = new TerrainCell(true, 0);

            // POI / 资源格全 -1(= 无)。长度须与 terrain 齐,否则 WorldGeometry 装载期抛。
            var poiGrid = new int[total];
            var resourceGrid = new int[total];
            for (int i = 0; i < total; i++)
            {
                poiGrid[i] = -1;
                resourceGrid[i] = -1;
            }

            var geometry = new WorldGeometry(terrain, poiGrid, resourceGrid);
            var slotCatalog = new BuildSlotCatalog();   // 空目录:RegisterSlot 零调用

            Debug.LogWarning(
                "[S20-17][合成世界] World 用**最小合成几何**构造 —— "
                + $"{SizeX}×{SizeY}×{SizeZ} 全 walkable,槽位目录为空。"
                + "原因:世界烘焙数据零产出(唯一作者 ADR-022 关卡工具已裁定不排期)。"
                + "⇒ 本世界不能证明任何「世界内容」正确性:AC-23-08 第三半边仍无载体,"
                + "建造槽位判定恒为「不在区域内」。横幅出现属**预期**,不是故障;"
                + "世界数据落地后本工厂应被装载路径替换。");

            return new World(geometry, slotCatalog);
        }
    }
}
