// 权威来源:production/epics/audio-system/story-008-spatialization-world-breath.md(AC-44-D2)
// GDD:design/gdd/audio-system.md F-44.4(:376「worldPos = LatticeToWorld(Cell) —— 表现层换算,禁反推」)
// ADR-015 §三(WorldPos = 单一整数格)· ADR-018 §二(禁从 float 反推逻辑格)
//
// ⚠️ **格→米比例(`latticeSize`)走参入,本类不自持常量** —— 该量归系统 6
//    (`world-and-ecozones.md:504` 的 `LATTICE_SIZE`;`combat-and-weapon-lines.md`
//    的 `OQ-25-5` 已裁「`cell_size` 上界归 6,25 不裁」)。44 若在此自造比例 = 第二真源
//    (承 ADR-024 §⑥ 单一出处纪律);调用方(装配层)从 6 侧取值后传入。
// 🔁 **2026-10-29(`TD-audio-035` ①)**:此处原文写「`LATTICE_SIZE` **至今无值**」——
//    已过期。6 侧自关卡工具 story-002 起在 `world_geometry.json` 里带 `lattice_size_mm`
//    (经 ADR-014 烘成 `world_geometry.cooked`),并经
//    `MinimalWorldFactory.Create(…, out int latticeSizeMm)` 出到运行期;
//    装配层用 `AudioAssemblySeams.ApplyLatticeFromWorldGeometry` 换算并灌入。
//    ⚠️ 数值本身仍是 6 侧的用户数值轮占位 —— 换源 ≠ 定值。
// ⚠️ **单向**:整数格 → float 世界坐标。反向(float → 逻辑格)在本类**结构性不存在**,
//    且全 44 命名空间由 `test_latticeToWorld_noFloatToWorldPosMethod` 反射守(AC-44-D2)。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>整数格 → 表现层 float 世界坐标的**唯一换算入口**(F-44.4)。
    /// <para>两层世界纪律(ADR-015 §一):逻辑层整数格是唯一真相,视觉层 float 是表现。
    /// 本类是两者之间**单向**的桥 —— 只出 float,不回收 float。</para>
    /// <para>⚠️ 联机下远端实体的锚点来自 ADR-001 §一之二 `IPositionalChannel` 的
    /// latest-value 表;44 **只读取**该锚点,不自建通道(承 Story 007 D1)。</para></summary>
    public static class LatticeToWorld
    {
        /// <summary>把逻辑格换算为世界坐标。</summary>
        /// <param name="cellX">整数格 X(ADR-015 §三 单一格)。</param>
        /// <param name="cellY">整数格 Y。</param>
        /// <param name="cellZ">整数格 Z。</param>
        /// <param name="latticeSize">格边长(米)—— **由调用方供给**,归系统 6 所有(见文件头注)。
        /// ≤ 0 ⇒ <see cref="System.ArgumentOutOfRangeException"/>(比例非正是换算的前提)。</param>
        /// <returns>世界坐标(表现层 float)。</returns>
        /// <remarks>⚠️ 格三轴以 <c>int</c> 承载而非 <c>Int3</c> —— 44 的公开方法签名受 AC-44-B3
        /// 入口白名单约束(`assembly_boundary_test` b3);先例 = <c>PatientCueEmit.Emit</c>。</remarks>
        public static Vector3 ToWorld(int cellX, int cellY, int cellZ, float latticeSize)
        {
            if (!(latticeSize > 0f))
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(latticeSize), latticeSize,
                    "格边长必须 > 0(比例非正是「格 → 世界坐标」可逆理解的前提;" +
                    "0 会让全部格塌到原点,负值会镜像世界)");
            }

            return new Vector3(cellX * latticeSize, cellY * latticeSize, cellZ * latticeSize);
        }
    }
}
