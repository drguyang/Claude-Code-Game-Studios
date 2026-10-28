// **脚手架关卡** —— 不为玩法服务,只为证明关卡工具**真的泛化**。
//
// 权威来源:
//   `production/epics/level-tool/story-001-level-source-generalization.md` AC-2
//     (「新增第二个关卡定义(尺寸与 `p0_clinic` 不同)」;反例明确写着
//      「把 `P0ClinicLevel` 复制一份改个名字**不算** —— 判据要求两关尺寸不同」)
//   ADR-022 §二(逻辑层数据只能由关卡工具导出 —— 本文件是该口径下的**合法**声明式源)
//
// ⚠️ **这不是设计产物,不要把它当第二间医馆。**
//   它的全部用途 = 让「加第二个关卡」这件事有一条**可跑的路径**,并让「尺寸来自数据」
//   有一个**与 P0 不同**的取值。取 `8×2×8` 而不是 `8×8×8`:非立方 ⇒ 任一轴被写死成另一个轴的
//   值,`world_size` 立刻对不上(AC-1 的边界:「三个轴各自独立,不是同一个值」)。
//   真实第二关卡(设计、布局、模块配置)归**关卡设计轮**;届时本定义**整个替换**,
//   不是「在它上面加槽位」。
//
// ⚠️ **`Y = 2` 是刻意的**:`p0_clinic` 是 `16×1×16`(单层)。第二关若同样是「单层 + 只改 X/Z」,
//    `Height` 会永远等于 1 ⇒ 「三维尺寸各自独立」这条**在数据上不可观测**。
//   本关卡的槽位因此分布在 `y = 0` 与 `y = 1` 两层。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.World;   // SlotType(唯一真源)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>第二关卡的**脚手架**声明式源(见文件头:不是设计产物)。</summary>
    public static class ScaffoldSecondLevel
    {
        /// <summary>关卡 id(导出 JSON 的 `level_id`;**不含**基准关卡的名字)。</summary>
        public const string LevelId = "scaffold_second";

        // ⚠️ 尺寸**只在这里出现一次**,且**只**作为 LevelSource 的构造实参 ——
        //    不留 `const int Width` 之类的「另一条路」(AC-1 的病灶正是那条路)。
        //    `8 / 2 / 8` 与 `p0_clinic` 的 `16 / 1 / 16` 在 Y 轴上直接分家(X/Z 亦不同值),
        //    且本关非立方 ⇒ 「尺寸来自数据」在**两个轴**上都有观察面。

        /// <summary>y = 0 层的 Decor 空地(可建)。</summary>
        public const int DecorFreeX = 2;
        /// <summary>见 <see cref="DecorFreeX"/>。</summary>
        public const int DecorFreeZ = 2;

        /// <summary>y = 1 层的 Decor 初始占用格。</summary>
        public const int DecorOccupiedX = 5;
        /// <summary>见 <see cref="DecorOccupiedX"/>。</summary>
        public const int DecorOccupiedZ = 5;

        /// <summary>y = 0 层的 Shell 初始占用格(外壳)。</summary>
        public const int ShellX = 6;
        /// <summary>见 <see cref="ShellX"/>。</summary>
        public const int ShellZ = 1;

        /// <summary>初始占用 Decor 用的模块 id(`build_modules.json` 的 7 = `chamber_pot`)。</summary>
        public const int DecorModuleId = 7;

        /// <summary>外壳用的模块 id(`build_modules.json` 的 8 = `refundable: false`)。</summary>
        public const int ShellModuleId = 8;

        /// <summary>
        /// 产出脚手架的声明式关卡源。
        /// <para>槽位集(4 条,横跨 y = 0 / y = 1 两层):
        /// ① `(2,0,2)` Decor 空地(`baked_initial = false`);
        /// ② `(5,1,5)` Decor 初始占用(`module_id = 7`);
        /// ③ `(6,0,1)` Shell 初始占用(`module_id = 8`);
        /// ④ `(7,1,7)` Decor 空地。</para>
        /// <para>⚠️ 「空地与初始占用都有、两层都有」不是凑数:第二个关卡的导出若只有
        /// 一种取值,`baked_initial` 的两条域门(④/⑤)在这一关上**没有观察面**。</para>
        /// </summary>
        public static LevelSource Define()
        {
            var slots = new List<SlotSpec>
            {
                new SlotSpec(DecorFreeX, 0, DecorFreeZ, SlotType.Decor, bakedInitial: false),
                new SlotSpec(DecorOccupiedX, 1, DecorOccupiedZ, SlotType.Decor,
                             bakedInitial: true, moduleId: DecorModuleId),
                new SlotSpec(ShellX, 0, ShellZ, SlotType.Shell,
                             bakedInitial: true, moduleId: ShellModuleId),
                new SlotSpec(7, 1, 7, SlotType.Decor, bakedInitial: false),
            };

            return new LevelSource(LevelId, 8, 2, 8, slots);
        }
    }
}
