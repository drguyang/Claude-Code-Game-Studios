// 「剧情阶段门」的测试前置工具(2026-11-14)。
//
// ═══ 它解决什么 ══════════════════════════════════════════════════════════
//   `CompositionRoot.cs:572-575` / `:848-850` 有一道**剧情阶段门**:世界骨架里
//   **任一 `Tent` 槽被占用**之前,环境产病人两条路(`PatientAppearedDriver` 与 52 抽池)
//   都不产病人(用户 2026-11-12 裁定「甲」)。
//
//   P1-b2(`41e89496`)把 canonical 换成 `p0_prologue`(128×40)之后,关卡里出现了
//   **1 个 `Tent` 槽**(`(108,0,16)`,`baked_initial: false`)⇒ 门从 `null` 变成真门
//   ⇒ 「开机即有病人」的 13 条 PlayMode 用例全部转红(它们的前提还停在门之前)。
//
//   ⇒ 这些用例要测的不是门,而是门**之后**的接线(case / emergency / 视觉 / 手柄)。
//     它们需要的前置是「帐篷已经建好」,本工具就提供这一条。
//
// ═══ 它走的是不是生产路径 ════════════════════════════════════════════════
//   ✅ 走 `CompositionRootServices.StructureWriter.Place` —— 与
//      `BuildController.TryPlace` 的第 ③ 步(**落流 = 唯一事件 `StructurePlaced`**)是
//      **同一个写者、同一个实例**,世界占用表与结构注册表因此同步更新(门读的就是它)。
//   ⚠️ **不覆盖**材料门与槽位合法性门(`BuildMaterialPlanner` / `PlaceableChecker`)——
//      那两道由 `build_material_planner_test` / 放置判定件单独覆盖,**不在这里重复**。
//      本工具只负责「让门看见帐篷已建成」这一件事。
//
// ⚠️ 门本身的正向覆盖**不在**本工具里 —— 见 `patient_gate_test.cs`:
//    它断言「没有帐篷 ⇒ 在场病人 = 0」,是门的直接判据。

using System;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>「剧情阶段门」的测试前置:把 canonical 关卡里的 Tent 槽占上。</summary>
    internal static class PatientGateTestSupport
    {
        /// <summary>Tent 模块 id(`build_modules.json` 的 `module_id`)。</summary>
        public const int TentModuleId = 10;

        /// <summary>canonical 关卡的 Tent 槽(`assets/data/world_buildslots.json`)。</summary>
        public static readonly WorldPos TentCell = new WorldPos(108, 0, 16);

        /// <summary>
        /// 经生产写者落一条 <c>StructurePlaced</c>,让剧情阶段门看见「帐篷已建成」。
        /// </summary>
        /// <param name="boot">已启动完成的 <see cref="BootRoot"/>。</param>
        public static void EnsureTentBuilt(BootRoot boot)
        {
            Assert.IsNotNull(boot, "[门前置] BootRoot 为空 —— 启动序未完成?");

            var services = (CompositionRootServices)typeof(BootRoot)
                .GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(boot);

            Assert.IsNotNull(services, "[门前置] `_services` 为空 —— 启动序未完成");
            Assert.IsNotNull(services.StructureWriter,
                "[门前置] `StructureWriter` 未装配 —— 无世界实例?");

            long tick = services.TickDriver != null ? services.TickDriver.CurrentTick : 0L;

            try
            {
                services.StructureWriter.Place(TentCell, TentModuleId, orientation: 0, variant: 0, tick);
            }
            catch (Exception ex)
            {
                Assert.Fail(
                    "[门前置] 在 Tent 槽 " + TentCell + " 落 `StructurePlaced` 失败:" + ex.Message +
                    "\n  ⇒ canonical 关卡若改了 Tent 槽坐标,须同步 `PatientGateTestSupport.TentCell`" +
                    "(真源:`assets/data/world_buildslots.json` 的 `slot_type == \"Tent\"`)");
            }
        }
    }
}
