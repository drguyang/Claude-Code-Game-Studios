// 关卡工具 —— **C2' 模板 collider 足迹同源**(硬失败)。ADR-022 §四。
//
// 权威来源:
//   ADR-022 §四 C1–C6 表:**C2' 模板 collider 足迹同源 = 硬失败**
//   ADR-022 §四 C2' 的理由逐字:「玩家不读整数格,走感全在 collider ⇒ 两套答案」
//   `modular-building.md` 规则八(义务 `O-23-9`)
//   `Sim.World/PlaceableChecker.cs` 的 `ModuleDefinition` / `IModuleCatalog`(占用格的唯一真源)
//
// ═══════════════════════════════════════════════════════════════════════════
// 取「乙路」(story-006 §Implementation Notes 已裁):定义形状 + 检查逻辑
// ═══════════════════════════════════════════════════════════════════════════
// 甲路要等 23 素材轮产出 prefab 再读 collider;乙路是「先把契约钉住,prefab 侧后补」。
// ⇒ 本件定义 `ModuleFootprintSpec`(**作者侧整数声明**),检查逻辑照常实现。
// ⚠️ **如实登记**:「prefab 的 collider 真的等于声明值」这一半**未跑** ——
//    今天全库零 prefab(`find unity/Assets -iname '*.prefab'` = 1,且是 spike 遗留)。
//    本件证明的是「**声明之间**互相一致」,不是「声明与 prefab 一致」。

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos
using DaYiJingCheng.Sim.World;       // ModuleDefinition / IModuleCatalog

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>
    /// 一个模块模板的**作者侧声明**足迹:占用格 + collider 高度 + 是否声明「可走过」。
    /// <para>⚠️ 全部是**整数** —— 高度用毫米,与 `stepOffset` 同量子(ADR-015 §三:
    /// 长度量纲全用整数毫米,不引入第二种单位)。</para>
    /// </summary>
    public readonly struct ModuleFootprintSpec
    {
        /// <summary>模块 id(须 ≥ 1,与 `ModuleDefinition.ModuleId` 同一空间)。</summary>
        public readonly int ModuleId;

        /// <summary>相对锚点的占用格(本地坐标;**须含 `(0,0,0)`**,否则锚点不在足迹内)。</summary>
        public readonly IReadOnlyList<WorldPos> LocalOccupancy;

        /// <summary>
        /// collider 的高度(毫米;`≥ 0`)。
        /// <para>⚠️ 它**不是**模块的视觉高度,是碰撞体的高度 —— C2' 问的是「玩家跨得过去吗」。</para>
        /// </summary>
        public readonly int ColliderHeightMm;

        /// <summary>
        /// 是否**显式声明**「这个模块可被走过」(如地毯 / 门槛)。
        /// <para>⚠️ 声明的语义是「我知道它高过 `stepOffset`,但玩家本来就不该跨过它 ——
        /// 它是要走**上去**的」。没有这条声明,「高 collider」一律判失败:
        /// 默认必须 fail-closed,不能让作者靠「忘了声明」蒙混。</para>
        /// </summary>
        public readonly bool WalkOverDeclared;

        /// <summary>构造一条足迹声明。</summary>
        /// <param name="moduleId">模块 id(≥ 1)。</param>
        /// <param name="localOccupancy">本地占用格(须含锚点)。</param>
        /// <param name="colliderHeightMm">collider 高度(毫米,≥ 0)。</param>
        /// <param name="walkOverDeclared">是否显式声明可走过。</param>
        public ModuleFootprintSpec(int moduleId, IReadOnlyList<WorldPos> localOccupancy,
            int colliderHeightMm, bool walkOverDeclared = false)
        {
            ModuleId = moduleId;
            LocalOccupancy = localOccupancy;
            ColliderHeightMm = colliderHeightMm;
            WalkOverDeclared = walkOverDeclared;
        }
    }

    /// <summary>C2':模块模板的 collider 足迹与 `stepOffset` / 占用格是否自洽。</summary>
    public static class ColliderFootprintCheck
    {
        /// <summary>判据关键词:高度超过 `stepOffset` 且未声明可走过。</summary>
        public const string HighColliderKey = "C2' 高 collider";

        /// <summary>判据关键词:足迹与目录占用格不一致。</summary>
        public const string FootprintKey = "C2' 足迹";

        /// <summary>
        /// 跑 C2'(不抛;失败项进 `violations`)。
        /// </summary>
        /// <param name="specs">作者侧足迹声明;`null` 或空 ⇒ 不判(返回 `true`,由调用方登记 NOT-RUN)。</param>
        /// <param name="stepOffsetMm">烘焙侧的台阶高度(毫米)。</param>
        /// <param name="catalog">模块目录(**占用格的唯一真源**);`null` ⇒ 只跑高度判据,足迹判据跳过。</param>
        /// <param name="violations">输出:失败项。</param>
        /// <param name="footprintSkipped">足迹判据是否因缺目录而跳过(供调用方登记 NOT-RUN)。</param>
        /// <returns>无失败项 ⇒ `true`。</returns>
        public static bool TryCheck(IReadOnlyList<ModuleFootprintSpec> specs, int stepOffsetMm,
            IModuleCatalog catalog, out List<string> violations, out bool footprintSkipped)
        {
            violations = new List<string>();
            footprintSkipped = false;
            if (specs == null) return true;

            for (int i = 0; i < specs.Count; i++)
                CheckOneSpec(specs[i], i, stepOffsetMm, catalog, violations, ref footprintSkipped);

            return violations.Count == 0;
        }

        // ── 单模块三段(2026-10-29 拆分:原 TryCheck 56 行 > 40)──────────────────
        // ⚠️ **各段的次序与报文原文逐字未变**;① 不合法时后面的比对**不跑**(会取到别人的定义)。

        /// <summary>单模块的 ① id 合法性 + ② 高 collider,然后转 ③。</summary>
        private static void CheckOneSpec(ModuleFootprintSpec spec, int i, int stepOffsetMm,
            IModuleCatalog catalog, List<string> violations, ref bool footprintSkipped)
        {
            string where = $"module {spec.ModuleId}";

            // ① 模块 id 合法性 —— 与 `LevelConsistencyCheck` 的 `module_id ≥ 1` 同一条契约
            //    (ADR-022:176)。这里再判一次是因为足迹声明可以独立于槽位存在。
            if (spec.ModuleId <= 0)
            {
                violations.Add(
                    $"{FootprintKey} [{i}] module id = {spec.ModuleId} 须 ≥ 1 —— " +
                    "0 / 负数不是合法的模块 id(ADR-022 `module_id ≥ 1`)");
                return;   // id 非法时后面的比对没有意义(会取到别人的定义)
            }

            // ② 高 collider ⇒ 除非显式声明可走过
            //    ⚠️ 判据是**严格大于**:等于 stepOffset 的模块正好是「一步能迈上去」的上限,
            //    把它判失败会让「恰好合规」的模块无法表达。
            if (spec.ColliderHeightMm > stepOffsetMm && !spec.WalkOverDeclared)
            {
                violations.Add(
                    $"{HighColliderKey} {where} collider 高 {spec.ColliderHeightMm} mm > " +
                    $"stepOffset {stepOffsetMm} mm,且**未声明**可走过 —— " +
                    "玩家会被它的碰撞体挡住,而整数格说这格可走 ⇒ 两套答案(ADR-022 §四 C2')");
            }

            CheckFootprintAgainstCatalog(spec, where, catalog, violations, ref footprintSkipped);
        }

        /// <summary>③ 足迹与目录占用格一致(缺目录 ⇒ 记 `footprintSkipped`,不是通过)。</summary>
        private static void CheckFootprintAgainstCatalog(ModuleFootprintSpec spec, string where,
            IModuleCatalog catalog, List<string> violations, ref bool footprintSkipped)
        {
            if (catalog == null)
            {
                footprintSkipped = true;
                return;
            }

            ModuleDefinition? defOpt = catalog.GetModuleDefinition(spec.ModuleId);
            if (!defOpt.HasValue)
            {
                violations.Add(
                    $"{FootprintKey} {where} 不在模块目录里 —— " +
                    "足迹声明引用了一个不存在的模块(改 id 后忘了同改另一边)");
                return;
            }

            string mismatch = CompareOccupancy(spec.LocalOccupancy, defOpt.Value.LocalOccupancy);
            if (mismatch != null)
                violations.Add($"{FootprintKey} {where} {mismatch}");
        }

        /// <summary>比较两份占用格集合(顺序无关;**重复项也算不一致** —— 重复即语义歧义)。</summary>
        private static string CompareOccupancy(IReadOnlyList<WorldPos> declared, IReadOnlyList<WorldPos> fromCatalog)
        {
            if (declared == null)
                return "足迹声明为 null(占用格是 C2' 的判据主体,不能省)";

            var d = new HashSet<(int, int, int)>();
            var c = new HashSet<(int, int, int)>();
            for (int i = 0; i < declared.Count; i++)
                if (!d.Add((declared[i].X, declared[i].Y, declared[i].Z)))
                    return $"足迹声明里格 ({declared[i].X},{declared[i].Y},{declared[i].Z}) 重复";
            for (int i = 0; i < fromCatalog.Count; i++)
                c.Add((fromCatalog[i].X, fromCatalog[i].Y, fromCatalog[i].Z));

            var onlyDeclared = new List<string>();
            foreach ((int x, int y, int z) in d)
                if (!c.Contains((x, y, z))) onlyDeclared.Add($"({x},{y},{z})");
            var onlyCatalog = new List<string>();
            foreach ((int x, int y, int z) in c)
                if (!d.Contains((x, y, z))) onlyCatalog.Add($"({x},{y},{z})");

            if (onlyDeclared.Count == 0 && onlyCatalog.Count == 0) return null;

            onlyDeclared.Sort();
            onlyCatalog.Sort();
            return "足迹与目录占用格不一致:" +
                   (onlyDeclared.Count > 0 ? $" 声明多出 [{string.Join(", ", onlyDeclared)}]" : "") +
                   (onlyCatalog.Count > 0 ? $" 目录多出 [{string.Join(", ", onlyCatalog)}]" : "");
        }
    }
}
