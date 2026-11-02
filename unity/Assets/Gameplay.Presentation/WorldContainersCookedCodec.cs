// `world_containers.cooked.bytes`(开档容器物品实例)的**运行期解码器**。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物)· `CookedFormat.cs`(20 字节头契约)
//   · ADR-022 §三(导出契约;本数据集为加性第 8 类)· 2026-11-02 `AC-4-22`
//   写方严格镜像:`Editor.Tools.Bake/WorldContainersCookedWriter.cs`(字段序逐字对齐)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_containers.cooked
//     i32 world_size_x; i32 world_size_y; i32 world_size_z; i32 row_count
//     row_count × { i64 instance_id; i32 x; i32 y; i32 z }
//
// ⚠️ **本件为什么住在 `Gameplay.Presentation`**:与 `WorldBuildSlotsCookedCodec` 同因 ——
//    `Sim` 的引用集恰 = {BCL, Sim.Contracts},而 cooked 解码是**边界层**的事。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>一个开档容器物品实例(实例 id + 格)。</summary>
    public readonly struct WorldContainerRow
    {
        /// <summary>物品实例 id(≥ 0;与掉落共用 `IIdAuthority` 空间)。</summary>
        public readonly long InstanceId;

        /// <summary>所在整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>构造。</summary>
        public WorldContainerRow(long instanceId, WorldPos cell)
        {
            InstanceId = instanceId; Cell = cell;
        }
    }

    /// <summary>`world_containers.cooked.bytes` 的解码结果。</summary>
    public readonly struct WorldContainersDataSet
    {
        /// <summary>cooked 产物名(Addressables 地址同此名)。</summary>
        public const string CookedFileName = "world_containers.cooked.bytes";

        /// <summary>容器实例行集(行序 = `instance_id` 升序)。</summary>
        public readonly IReadOnlyList<WorldContainerRow> Containers;

        /// <summary>产物头声明的 schema 版本(ADR-014 §五)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造。</summary>
        public WorldContainersDataSet(IReadOnlyList<WorldContainerRow> containers,
                                      uint schemaVersion, uint configVersion)
        {
            Containers = containers; SchemaVersion = schemaVersion; ConfigVersion = configVersion;
        }

        /// <summary>全部实例 id(供 `IdAuthority.ReconstructItemHighWater` 抬升高水位)。</summary>
        public IEnumerable<long> InstanceIds()
        {
            for (int i = 0; i < Containers.Count; i++) yield return Containers[i].InstanceId;
        }
    }

    /// <summary>开档容器(<c>world_containers.cooked.bytes</c>)的运行期解码器。</summary>
    public static class WorldContainersCookedCodec
    {
        /// <summary>产物名(与写方 `WorldContainersBake.CookedFileName` 同值)。</summary>
        public const string ProductName = "world_containers";

        /// <summary>本产物的作者态 schema_version 期望值(per-product 口径)。</summary>
        /// <remarks>⚠️ 与 `Editor.Tools.Level.LevelExporter.ContainersSchemaVersion` **必须同值**。</remarks>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>读容器实例集。</summary>
        /// <param name="bytes">cooked 字节(含 20 字节头)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 空表 / 长度不符 / 值域越界 / 重复 id 或格 —— 启动期硬失败。</exception>
        public static WorldContainersDataSet Read(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                uint schemaVersion = ReadU32Le(bytes, 8);
                if (schemaVersion != ExpectedSchemaVersion)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} schemaVersion = {schemaVersion},期望 " +
                        $"{ExpectedSchemaVersion} —— 布局演进后不得按旧契约读(E-13)。" +
                        "重烘:大医精诚/数据管线/烘焙 world_containers");

                int worldW = r.ReadInt32();
                int worldH = r.ReadInt32();
                int worldD = r.ReadInt32();

                int count = r.ReadInt32();
                if (count < 1)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} row_count = {count} —— 容器集不得为空" +
                        "(空表与「未声明」不可分,且开档无柜 ⇒ `AC-4-22` 的柜半边永不可达;E-13)");

                var rows = new WorldContainerRow[count];
                var seenIds = new HashSet<long>();
                var seenCells = new HashSet<(int, int, int)>();

                for (int i = 0; i < count; i++)
                {
                    long instanceId = r.ReadInt64();
                    int x = r.ReadInt32();
                    int y = r.ReadInt32();
                    int z = r.ReadInt32();

                    if (instanceId < 0)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} containers[{i}].instance_id = {instanceId} —— 须 ≥ 0");
                    if (!seenIds.Add(instanceId))
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} instance_id = {instanceId} 重复 —— " +
                            "它是 F-4.1 的第三键,重复即「无胜者」");
                    if (!seenCells.Add((x, y, z)))
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} 格 ({x},{y},{z}) 出现两行 —— " +
                            "同格两个容器会让「这一格是哪一个」成为隐式规则");
                    if (worldW > 0 && worldH > 0 && worldD > 0 &&
                        (x < 0 || x >= worldW || y < 0 || y >= worldH || z < 0 || z >= worldD))
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} containers[{i}] 格 ({x},{y},{z}) 超出声明范围 " +
                            $"{worldW}×{worldH}×{worldD} —— 越界容器运行期永远选不中");

                    rows[i] = new WorldContainerRow(instanceId, new WorldPos(x, y, z));
                }

                CookedReadUtil.AssertFullyConsumed(r, ProductName);

                return new WorldContainersDataSet(Array.AsReadOnly(rows), schemaVersion, configVersion);
            }
        }

        /// <summary>读头部第 3 个 u32(offset 8 = 作者态 schema_version 的回声)。</summary>
        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }
}
