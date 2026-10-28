// 关卡工具 —— **资源点声明**(ADR-022 §三 `world_resources.json`)。
//
// 权威来源:
//   ADR-022 §三(导出契约 `world_resources.json`)· §二(唯一作者)
//   ADR-015 §三(逻辑层只有整数格;禁浮点)
//   GDD `foraging.md`(17 采集消费资源点)· `world-and-ecozones.md` F-6-3(生态区)
//   `production/epics/level-tool/story-004-world-poi-and-resources.md`(AC-2 / AC-5)
//
// ⚠️ **「生态区倾向」只引用 `ecozone_id`,不校验存在性**(story-004 §Implementation Notes 末段)。
//    理由:校验存在性会把 `world_resources` 与 `world_ecozones` **焊死在同一份导出快照**上,
//    而两者本是独立数据集。存在性是一条**跨数据集**判据 —— 它归 S6 的 C 系列(那里
//    「两层世界一致性」才是主题),不是本工具的逐集导出责任。
//    ⚠️ 本 story 只做**一件事**:不校验。若将来要校验,须先裁定它归哪一条 C 检查 ——
//    **在导出器里顺手加一条跨数据集门,会让「单独重导一个数据集」变得不可能**。
//
// ⚠️ **资源 id 是裸 `int`,不建资源类型闭集** —— 具体取值归 17 采集的 GDD
//    (`foraging.md` 已有资源类型表,但那是**运行期**语义;工具侧引入镜像 = 第二真源,
//    本仓反复清过这类形态)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>一个资源点(整数格 + 资源 id + 生态区倾向)。</summary>
    public readonly struct ResourceSpec
    {
        /// <summary>资源 id(≥ 0)。</summary>
        public readonly int ResourceId;

        /// <summary>所在整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>生态区倾向(`-1` = 无倾向;否则引用 <c>world_ecozones</c> 的 `ecozone_id`)。</summary>
        public readonly int EcozoneId;

        /// <summary>「无生态区倾向」的哨兵值(与 `Sim.World.EcozoneId.None` 同值)。</summary>
        public const int NoEcozone = -1;

        /// <summary>构造一个资源点。</summary>
        public ResourceSpec(int resourceId, WorldPos cell, int ecozoneId = NoEcozone)
        {
            ResourceId = resourceId; Cell = cell; EcozoneId = ecozoneId;
        }
    }

    /// <summary>资源点集合(声明式源;`null` = 本关卡未声明)。</summary>
    public sealed class ResourceSource
    {
        /// <summary>资源点集(声明序;导出时按 (resource_id, 格) 升序重排)。</summary>
        public readonly IReadOnlyList<ResourceSpec> Resources;

        /// <summary>构造资源点集。</summary>
        /// <exception cref="ArgumentException">`resource_id` 为负 / `ecozone_id` 为负但非 `-1` 哨兵。</exception>
        public ResourceSource(IReadOnlyList<ResourceSpec> resources)
        {
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));

            var seenCells = new HashSet<(int, int, int)>();

            for (int i = 0; i < resources.Count; i++)
            {
                ResourceSpec r = resources[i];
                if (r.ResourceId < 0)
                    throw new ArgumentException(
                        $"resources[{i}] 的 resource_id = {r.ResourceId} —— 须 ≥ 0。" +
                        "`-1` 是「本格无资源」的哨兵(`GetResourceId` 的返回值)", nameof(resources));

                if (r.EcozoneId < ResourceSpec.NoEcozone)
                    throw new ArgumentException(
                        $"resources[{i}] 的 ecozone_id = {r.EcozoneId} —— 只接受 `-1`(无倾向)或 ≥ 0",
                        nameof(resources));

                // ⚠️ **一格一条**(与 `PoiSource` 同款,但理由不同):
                //    运行期 `WorldGeometry.ResourceGrid` 是 **每格一个 `int`**
                //    (`ResourceGrid[i] == 该格的 resource_id`)。同一格声明两条资源时,
                //    填格只能**留下一条、丢掉其余的** —— 而且是静默的(没有任何报错)。
                //    ⇒ 在**构造期**就拒,而不是等产物与运行期各说各话。
                //    ⚠️ 若将来真要「一格多种药材」,那是 **ADR 级**改动:
                //    `ResourceGrid` 的 `int` 语义要换成集合 / 位域,连同 `GetResourceId`
                //    的返回类型与全部消费者一起改。加一条 `HashSet` 挡不住那个改动。
                if (!seenCells.Add((r.Cell.X, r.Cell.Y, r.Cell.Z)))
                    throw new ArgumentException(
                        $"格 ({r.Cell.X},{r.Cell.Y},{r.Cell.Z}) 声明了两条资源(resources[{i}])—— " +
                        "`ResourceGrid` 每格只有一个 int ⇒ 第二条会被静默丢弃。" +
                        "「一格多种药材」是 ADR 级改动,不是本类能表达的", nameof(resources));
            }
        }

        /// <summary>按 `(resource_id, x, y, z)` 升序返回(确定的产物行序)。</summary>
        /// <remarks>
        /// ⚠️ 排序键**必须**含格坐标:同一 `resource_id` 会出现在多个格上(资源 id 是**种类**,
        /// 不是实例)。只按 `resource_id` 排序时相等元素的次序取决于 `List.Sort` 的实现
        /// (不稳定排序)⇒ 两次烘焙字节可能不同,违反 ADR-014 §二。
        /// </remarks>
        public List<ResourceSpec> SortedByKey()
        {
            var copy = new List<ResourceSpec>(Resources);
            copy.Sort((a, b) =>
            {
                int c = a.ResourceId.CompareTo(b.ResourceId);
                if (c != 0) return c;
                c = a.Cell.X.CompareTo(b.Cell.X); if (c != 0) return c;
                c = a.Cell.Y.CompareTo(b.Cell.Y); if (c != 0) return c;
                c = a.Cell.Z.CompareTo(b.Cell.Z); if (c != 0) return c;
                return a.EcozoneId.CompareTo(b.EcozoneId);
            });
            return copy;
        }

        /// <summary>校验每个资源点落在关卡声明范围内(导出期调用)。</summary>
        /// <exception cref="InvalidOperationException">任一格越界。</exception>
        public ResourceSource ValidateAgainstLevel(int width, int height, int depth)
        {
            var bad = new List<string>();
            for (int i = 0; i < Resources.Count; i++)
            {
                ResourceSpec r = Resources[i];
                if (r.Cell.X < 0 || r.Cell.X >= width ||
                    r.Cell.Y < 0 || r.Cell.Y >= height ||
                    r.Cell.Z < 0 || r.Cell.Z >= depth)
                {
                    bad.Add($"resources[{i}] resource_id={r.ResourceId} 的格 " +
                            $"({r.Cell.X},{r.Cell.Y},{r.Cell.Z}) 超出 {width}×{height}×{depth}");
                }
            }
            if (bad.Count > 0)
                throw new InvalidOperationException(
                    "[关卡工具] 资源点越界:" + string.Join(";", bad));
            return this;
        }
    }
}
