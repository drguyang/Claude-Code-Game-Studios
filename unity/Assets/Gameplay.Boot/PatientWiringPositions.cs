// 接线期病人格的**单一出处**(2026-11-06 · P0 空间解)。
//
// ── 本类补的是什么 ───────────────────────────────────────────────────────────
// 此前两条生产出生路径都把病人格写成字面量 `new WorldPos(0, 0, 0)`:
//   · `PatientAppearedDriver.cs:113`(驱动路径)
//   · `CompositionRoot.cs` 的 `spawnAnchorOf: _ => new WorldPos(0, 0, 0)` 与
//     `spawnPatient: ... (_ => new WorldPos(0, 0, 0))`(52 池路径)
// ⇒ **所有病人被钉在同一格**上:彼此重叠、且与玩家的交互判定无关(0,0,0) 是
//   `IPatientVisualAnchorSource` 明文禁止当哨兵的**合法格**。
//
// 用户 2026-11-06 裁定(P0 空间解):**病人至多同时 2 个,分居两个角;灶台占第三个角**。
// 本类持有那两格。真位置(6 世界生态区 + 13 病人 AI 空间侧)到位后整条退役。
//
// ⚠️ **为什么是「内一圈」的角**:字面角 `(0,0,0)` / `(15,0,15)` 在外圈上,而
//   `world_geometry.json` 的 `walkable` 只覆盖 `x,z ∈ [1,14]`(= 196 格)⇒ 外圈不可走。
//
// ⚠️ **确定性**:本表是常量,不读 PRNG / 墙钟 / 几何 ⇒ 满足 ADR-016 §一
//   (同序号的病人恒同格)。
//
// ⚠️ **不是设计值**:这是接线期占位,不进任何 GDD 数值面。

using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>接线期病人格(两个内角;与灶台所在的第三角互不重叠)。</summary>
    public static class PatientWiringPositions
    {
        /// <summary>接线期同时在场病人数的上限(用户 2026-11-06 裁定 = 2)。</summary>
        public static int Count => Cells.Length;

        /// <summary>两个内角 —— 分居医馆对角的两个角落。</summary>
        /// <remarks>
        /// 🔁 **2026-11-12(P1-b2)**:原值 `(1,0,14)` / `(14,0,1)` 属于 P0 16×16 医馆;
        /// 序章换绑后 `(14,0,1)` 落在**区外不可走格**(`walkable = 0`),真包走查实测
        /// 病人 #2 站在那里。现值 = 营地帐篷 `(108,16)` 两侧的空格 —— 同时满足
        /// 「可走」「非建造槽」「非容器格」(三者均以烘焙产物为准逐格核过)。
        /// <para>笔架:**病人只应在帐篷建成之后出现**(用户 2026-11-12 裁定「甲」)——
        /// 那道门不在本类,在 `PatientAppearedDriver` 的 `canSpawn` 与 52 的
        /// `canSpawnPatient`(见 `CompositionRoot`)。本类只管「站哪」。</para>
        /// </remarks>
        private static readonly WorldPos[] Cells =
        {
            new WorldPos(106, 0, 15),
            new WorldPos(110, 0, 20),
        };

        /// <summary>第 <paramref name="ordinal"/> 个病人的格(按上限取模,永不越界)。</summary>
        /// <param name="ordinal">病人在本会话内的出现序号(0 起)或掷骰 tick —— 只用于分散,不承载语义。</param>
        public static WorldPos CellFor(long ordinal)
        {
            int i = (int)(ordinal % Cells.Length);
            if (i < 0) i += Cells.Length;
            return Cells[i];
        }
    }
}
