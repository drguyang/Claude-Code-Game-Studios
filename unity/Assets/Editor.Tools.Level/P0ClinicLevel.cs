// P0 医馆 —— 建造槽位骨架的**具体种子**(关卡工具的声明式数据)。
//
// 权威来源:
//   ADR-022 §二(唯一作者 = 关卡工具)· §三(导出契约含 `BakedInitial`)
//   ADR-015 §三(整数格)· GDD modular-building.md 规则一(槽位骨架)
//
// ⚠️ **规模刻意最小**:只铺 13 格(9 格的 Decor 3×3 区 + 4 格的 Shell 条),落在
//    `MinimalWorldFactory` 的 16×1×16 合成世界内,**其余 243 格一律非槽位** ——
//    「非槽位格」是既有夹具(`test_acB2_placeOnNonSlotCell_returnsSlotNotFoundWithZeroSlots` 类)
//    仍然有意义的必要条件。铺满 = 让「不在区域内」这条判据失去观察面。
//
// ⚠️ **`BakedInitial` 不是恒 `true`**(2026-10-28 收口轮订正,原稿恒 true —— 原文不删):
//    原稿把 13 格全标 `baked_initial = true`,读起来的语义是「开档时整间医馆**已建满**」
//    ⇒ 一旦运行期真的开始应用初始占用,玩家**没有一格可建**,这与本轮的目的
//    (「让玩家今天建得了东西」)直接相反。现种子按**语义**分:
//      · **Shell 4 格 = `true`** —— 外壳(墙 / 地基)在开档时**就存在**,这是外壳的定义;
//      · **Decor 9 格 = 1 格 `true` + 8 格 `false`** —— 一格里已摆着家具(证明 `true` 分支有真实取值),
//        其余 8 格是**空地**(证明 `false` 分支可建)。
//    ⚠️ ~~但 **运行期今日仍不应用初始占用**(`BuildSlotCatalog` 没有承载它的字段)——
//    `MinimalWorldFactory` 因此在遇到 `BakedInitial == true` 的行时**打一条 LogWarning**,
//    不让「数据声明了、运行期没落地」变成静默。该缺口登记在交付报告里。~~
//    **🔁 2026-10-28(S20-20 挂标 #2)该缺口已闭合,原文不删**:`BuildSlot` 现承载
//    `BakedInitial` + `ModuleId`(schema 2),`World.ApplyBakedInitial` 在生产装配路径上
//    应用基线(占足迹 + 进实例表 + 确定性发号)。`MinimalWorldFactory` 的那条 LogWarning
//    已随之删除(它报的正是这个已闭合的缺口)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.World;   // SlotType(唯一真源,2026-10-28 评审 A-21)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>P0 医馆的槽位骨架种子。</summary>
    public static class P0ClinicLevel
    {
        /// <summary>世界 X 格数(与 `MinimalWorldFactory.SizeX` 同值)。</summary>
        public const int Width = 16;

        /// <summary>世界 Y 格数(单层平面,与 `MinimalWorldFactory.SizeY` 同值)。</summary>
        public const int Height = 1;

        /// <summary>世界 Z 格数(与 `MinimalWorldFactory.SizeZ` 同值)。</summary>
        public const int Depth = 16;

        /// <summary>关卡 id(导出 JSON 的 `level_id`)。</summary>
        public const string LevelId = "p0_clinic";

        /// <summary>Decor 3×3 区的原点(x 起)。</summary>
        public const int DecorOriginX = 6;
        /// <summary>Decor 3×3 区的原点(z 起)。</summary>
        public const int DecorOriginZ = 6;

        /// <summary>Decor 区里**唯一一格初始占用**(开档已摆着家具);其余 8 格是空地。</summary>
        /// <remarks>⚠️ 语义见文件头:全 true = 医馆已建满 = 玩家无可建格,与本轮目的相反。</remarks>
        public const int DecorPreOccupiedDx = 0;
        /// <summary>见 <see cref="DecorPreOccupiedDx"/>。</summary>
        public const int DecorPreOccupiedDz = 0;

        /// <summary>
        /// 那格初始 Decor 的模块 id(`build_modules.json` 的 7 = `chamber_pot`)。
        /// <para>🔁 <b>2026-10-28(S20-20 挂标 #2)新增</b>(schema 1→2):`baked_initial = true`
        /// 现在**必须**指明是哪件家具 —— 运行期要按它算足迹、并给初始家具发 `structure_id`
        /// (GDD 规则三)。取 `chamber_pot` 的理由:它是单格足迹(`[[0,0,0]]`)、`Decor` 类型、
        /// `refundable = true` —— 玩家对它的第一次改动(第一条 `StructureRemoved`)能走通返还链,
        /// 是规则三「玩家对初始家具的第一次改动 = 第一条 StructureRemoved」的可达面。</para>
        /// </summary>
        public const int DecorPreOccupiedModuleId = 7;

        /// <summary>Shell 条带用的模块 id(`build_modules.json` 的 8 = `refundable: false`)。</summary>
        /// <remarks>⚠️ 外壳 **`refundable = false`** —— 与 GDD 规则三/§「P0 外壳不可拆」一致:
        /// `DemolishChecker` 会按该位拒拆,返还链根本不会启动。</remarks>
        public const int ShellModuleId = 8;

        /// <summary>Shell 条带的 x 起点。</summary>
        public const int ShellStripXStart = 1;
        /// <summary>Shell 条带的长度(格)。</summary>
        public const int ShellStripLength = 4;
        /// <summary>Shell 条带的 z(单行)。</summary>
        public const int ShellStripZ = 1;

        /// <summary>
        /// 产出 P0 医馆的声明式关卡源。
        /// <para>槽位集:① `x ∈ [6,8] × z ∈ [6,8]` 的 **9 格 Decor**(3×3,足以容纳一个 3×3 足迹);
        /// ② `x ∈ [1,4] × z = 1` 的 **4 格 Shell**(外壳分支可达)。</para>
        /// <para>开档基线 = 5 格(4 Shell + 1 Decor),各有真 `module_id`(schema 2)。</para>
        /// </summary>
        public static LevelSource Define()
        {
            var slots = new List<SlotSpec>();

            for (int dx = 0; dx < 3; dx++)
                for (int dz = 0; dz < 3; dz++)
                {
                    bool preOccupied = dx == DecorPreOccupiedDx && dz == DecorPreOccupiedDz;
                    slots.Add(new SlotSpec(
                        DecorOriginX + dx, 0, DecorOriginZ + dz, SlotType.Decor,
                        bakedInitial: preOccupied,
                        moduleId: preOccupied ? DecorPreOccupiedModuleId : SlotSpec.NoModule));
                }

            for (int i = 0; i < ShellStripLength; i++)
                slots.Add(new SlotSpec(
                    ShellStripXStart + i, 0, ShellStripZ, SlotType.Shell,
                    bakedInitial: true, moduleId: ShellModuleId));

            return new LevelSource(LevelId, Width, Height, Depth, slots);
        }
    }
}
