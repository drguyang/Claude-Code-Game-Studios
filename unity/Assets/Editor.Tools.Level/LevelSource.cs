// 关卡工具 —— **声明式关卡源**(ADR-022 §二:逻辑层整数数据的唯一作者面)。
//
// 权威来源:
//   ADR-022 §二(逻辑层「只能由关卡工具编写并导出」)· §三(导出契约 `assets/data/world_*.json`
//     含 **初始占用标记 `BakedInitial`**)· ADR-015 §三(全部坐标为整数格 `WorldPos(i32,i32,i32)`)
//
// ⚠️ **槽位类型 = `Sim.World.SlotType` 本身,不建镜像**(2026-10-28 代码评审 A-21 修复)
//    ~~本程序集刻意零引用(`references: []`)⇒ 下面那个 `SlotType` 是 Sim 侧枚举名的镜像,
//    镜像若与 Sim 漂移,表现为烘焙期硬失败,不是静默错配。~~
//    **那段自述对「名字漂移」成立,对「序数漂移」不成立** —— 烘焙期曾按「名字表下标 + 1」
//    自产序数,与运行期 `BitMask.Single(type) => 1 << (int)type` 之间**没有任何门**;
//    Sim 侧在枚举中间插值 ⇒ 掩码与类型位整体错位、五条域门全绿 ⇒ **静默错配**。
//    现改为**直接引用 `Sim`**(`Editor.Tools.Level.asmdef` 的 `references: ["Sim"]`),
//    全仓只剩**一个** `SlotType` 定义,序数由 CLR 枚举给出,漂移在**编译期**即不可能。
//    ⚠️ 依据:ADR-022 §①「关卡工具 `UnityEngine` / UnityEditor 引用**自由** —— 门 A 不约束,
//    因其**不进构建**」;工具族引用集变更按 ADR-025 §① 登记(同 `Editor.Tools.Gates` +
//    `Unity.Addressables` 的先例)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.World;   // SlotType(唯一真源;ADR-022 §① 允许编辑期工具引用)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>一条槽位声明(整数格 + 类型 + 初始占用标记 + 初始家具模块 id)。</summary>
    public readonly struct SlotSpec
    {
        /// <summary>格 x(i32)。</summary>
        public readonly int X;
        /// <summary>格 y(i32)。</summary>
        public readonly int Y;
        /// <summary>格 z(i32)。</summary>
        public readonly int Z;
        /// <summary>槽位类型(**不得**为 <see cref="SlotType.None"/>)。</summary>
        public readonly SlotType Type;

        /// <summary>初始占用标记(ADR-022 §三:`BakedInitial`;开档既有家具 / 外壳)。</summary>
        public readonly bool BakedInitial;

        /// <summary>
        /// 开档既有家具的模块 id(`BakedInitial == true` 时必填;否则须为
        /// <see cref="NoModule"/>)。
        /// <para>🔁 <b>2026-10-28(S20-20 挂标 #2)新增</b>(schema 1→2):此前只声明「这格开档
        /// 已占用」,不声明「被什么占用」⇒ 运行期即使应用基线也算不出足迹、更发不出
        /// `structure_id`(GDD 规则三要求初始家具进实例表)。</para>
        /// </summary>
        public readonly int ModuleId;

        /// <summary>「本槽位开档无既有家具」的哨兵值(与 `Sim.World.BuildSlot.NoModule` 同值)。</summary>
        public const int NoModule = -1;

        /// <summary>构造一条槽位声明。</summary>
        /// <param name="moduleId">`BakedInitial == true` 时须 ≥ 1;否则留默认
        /// <see cref="NoModule"/>(导出器与绑定器两侧都会拒「true 但无 module_id」)。</param>
        public SlotSpec(int x, int y, int z, SlotType type, bool bakedInitial, int moduleId = NoModule)
        {
            X = x; Y = y; Z = z; Type = type; BakedInitial = bakedInitial; ModuleId = moduleId;
        }

        /// <summary>格坐标(整数三元组)。</summary>
        public (int X, int Y, int Z) Cell => (X, Y, Z);
    }

    /// <summary>声明式关卡源:尺寸 + 槽位表(纯整数,零 float)。</summary>
    public sealed class LevelSource
    {
        /// <summary>关卡 id(导出 JSON 的 `level_id`;非空)。</summary>
        public readonly string LevelId;

        /// <summary>格数 X(≥ 1)。</summary>
        public readonly int Width;
        /// <summary>格数 Y(≥ 1)。</summary>
        public readonly int Height;
        /// <summary>格数 Z(≥ 1)。</summary>
        public readonly int Depth;

        /// <summary>槽位表(声明序;导出时按 (x,y,z) 升序重排)。</summary>
        public readonly IReadOnlyList<SlotSpec> Slots;

        /// <summary>构造关卡源。尺寸非法 / 槽位表为 null ⇒ 抛(作者错误,不当默认值吞掉)。</summary>
        public LevelSource(string levelId, int width, int height, int depth, IReadOnlyList<SlotSpec> slots)
        {
            if (string.IsNullOrEmpty(levelId))
                throw new ArgumentException("level_id 不得为空", nameof(levelId));
            if (width < 1 || height < 1 || depth < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(width), $"世界尺寸须 ≥ 1(实得 {width}×{height}×{depth})");
            LevelId = levelId;
            Width = width; Height = height; Depth = depth;
            Slots = slots ?? throw new ArgumentNullException(nameof(slots));
        }
    }
}
