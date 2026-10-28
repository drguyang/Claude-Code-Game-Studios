// 关卡工具 —— **地形表声明式源**(`world_terrain.json` 的作者侧形状)。
//
// 权威来源:
//   ADR-022 §三(导出契约:`world_terrain.json` = `TERRAIN_TABLE`(地貌材质 → K)· terrain_id 表
//     · `slopeLimit` / `stepOffset`)
//   GDD `world-and-ecozones.md` **F-6-2**(`K_TERRAIN_MAX := max over terrain_id of K_speed`;
//     空表 ⇒ **装载期硬失败**)· **AC-6-08**(每行三列 > 0;`K_speed == 1` **恰好一行**)
//   ADR-014 §二(承载 `Fix` 的字段在 JSON 里写**字符串**,禁 JSON 数字)
//   `docs/engine-reference/unity/VERSION.md`(本件是**纯整数声明**,零引擎 API)
//
// ═══════════════════════════════════════════════════════════════════════════
// 为什么坡度 / 台阶高度要**整数量化**
// ═══════════════════════════════════════════════════════════════════════════
// ADR-022 §四 **C2** 的判据是 `|engineParam − Dequantize(bakedInt)| ≤ 1 量子`。
// 逻辑层只有整数(ADR-015 §三),引擎侧是 float ⇒ 两者之间**必须**有一个固定的量化约定,
// 否则 S6 的比对没有对象。约定本身住在 `Sim.Contracts` 的
// `LatticeParamQuantization`(两侧都看得见的一个实体,不是两份相等字面量)。
//
// ⚠️ **数值归用户数值轮**(ADR-022 §Out of Scope)。本类**只承载容器**;
//    `P0ClinicLevel` 里填的那两个值取的是 **AC-0 spike 实测到的引擎默认值**
//    (slopeLimit 45° / stepOffset 0.3 m),目的是让 C2 在今天的默认控制器下**偏差恰为 0**。
//    用户改了控制器的脚本参数之后,这两个值须跟着改 —— 那正是 C2 要抓的漂移。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // Fix(ADR-014 §二:JSON 写字符串)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>`TERRAIN_TABLE` 的一行(**声明式**;与运行期 `TerrainSpeedRow` 同形但不同件 ——
    /// 本件还带 `TerrainId` 与名字,后者只用于作者可读性)。</summary>
    public readonly struct TerrainRowSpec
    {
        /// <summary>地貌材质 id(≥ 0;表内唯一;运行期 `TerrainCell.KSpeedIdx` 指向它)。</summary>
        public readonly int TerrainId;

        /// <summary>作者可读名(ASCII;导出到 JSON;cooked 里也带 —— 词表的单一出处)。</summary>
        public readonly string Name;

        /// <summary>速度档位乘数(无量纲整数,`> 0`;基准地貌 = 1,且**恰好一行**为 1)。</summary>
        public readonly int KSpeed;

        /// <summary>加速乘数(`> 0`;归 1 的 F-1-3 消费)。</summary>
        public readonly Fix KAccel;

        /// <summary>减速乘数(`> 0`;归 1 的 F-1-3 消费)。</summary>
        public readonly Fix KDecel;

        /// <summary>构造一行。</summary>
        public TerrainRowSpec(int terrainId, string name, int kSpeed, Fix kAccel, Fix kDecel)
        {
            TerrainId = terrainId;
            Name = name;
            KSpeed = kSpeed;
            KAccel = kAccel;
            KDecel = kDecel;
        }
    }

    /// <summary>
    /// 一个关卡的地形声明:表 + **每格 terrain_id** + 两条量化后的控制器参数。
    /// <para>⚠️ **每格的 `terrain_id` 是必需的**,不是可选装饰:`TERRAIN_TABLE` 只说「有哪些地貌、
    /// 各自多快」,说不出「这一格是哪种地貌」;而运行期的 <c>TerrainCell.KSpeedIdx</c> 逐格存索引。
    /// 缺了它,表就没有消费者(2026-10-29 前 `KSpeedIdx` 全仓零消费者正是这个形态)。</para>
    /// </summary>
    public sealed class TerrainSource
    {
        /// <summary>`TERRAIN_TABLE`(声明序 = `terrain_id` 升序;非空)。</summary>
        public readonly IReadOnlyList<TerrainRowSpec> Rows;

        /// <summary>每格 terrain_id(长度 = `W×H×D`,索引 = `(x*H + y)*D + z`)。</summary>
        public readonly IReadOnlyList<int> CellTerrainId;

        /// <summary>`slopeLimit` 的整数量化值(单位 = 0.01 度;见 `LatticeParamQuantization`)。</summary>
        public readonly int SlopeLimitDegX100;

        /// <summary>`stepOffset` 的整数量化值(单位 = 1 mm;见 `LatticeParamQuantization`)。</summary>
        public readonly int StepOffsetMm;

        /// <summary>构造并执行**全部作者期断言**(F-6-2 / AC-6-08 / 每格 id 有效)。</summary>
        /// <param name="rows">地貌表(非空;`terrain_id` 唯一且 ≥ 0;恰好一行 `K_speed == 1`)。</param>
        /// <param name="cellTerrainId">每格 terrain_id(长度须恰 = <paramref name="totalCells"/>;
        /// 每个值须在表中存在)。</param>
        /// <param name="slopeLimitDegX100">坡度上限(0.01 度量子,`> 0`)。</param>
        /// <param name="stepOffsetMm">台阶高度(1 mm 量子,`≥ 0`)。</param>
        /// <param name="totalCells">该关卡的格总数(`W×H×D`;由调用方从尺寸算出)。</param>
        /// <exception cref="ArgumentNullException">表或每格数组为 null。</exception>
        /// <exception cref="ArgumentException">任一断言失败(报文点名判据)。</exception>
        public TerrainSource(
            IReadOnlyList<TerrainRowSpec> rows,
            IReadOnlyList<int> cellTerrainId,
            int slopeLimitDegX100,
            int stepOffsetMm,
            int totalCells)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (cellTerrainId == null) throw new ArgumentNullException(nameof(cellTerrainId));

            // ① 空表 = F-6-2 的硬失败面(max over 空集无定义)。作者期就拦,别等装载。
            if (rows.Count == 0)
                throw new ArgumentException(
                    "TERRAIN_TABLE 行数为 0 —— F-6-2:`K_TERRAIN_MAX := max over terrain_id`," +
                    "max over 空集**无定义** ⇒ 装载期硬失败。不可用「全 walkable」代替", nameof(rows));

            var byId = new HashSet<int>();
            int baselineRows = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                TerrainRowSpec r = rows[i];
                if (r.TerrainId < 0)
                    throw new ArgumentException(
                        $"TERRAIN_TABLE 第 {i} 行 terrain_id = {r.TerrainId} 须 ≥ 0", nameof(rows));
                if (!byId.Add(r.TerrainId))
                    throw new ArgumentException(
                        $"TERRAIN_TABLE 第 {i} 行 terrain_id = {r.TerrainId} 重复 —— " +
                        "同一 id 两行会让「取哪一行」成为隐式规则", nameof(rows));
                if (string.IsNullOrEmpty(r.Name))
                    throw new ArgumentException(
                        $"TERRAIN_TABLE 第 {i} 行 name 为空 —— 名字是 authoring 可读性的一半,空名让报文无从指认",
                        nameof(rows));
                if (r.KSpeed <= 0)
                    throw new ArgumentException(
                        $"TERRAIN_TABLE 第 {i} 行 K_speed = {r.KSpeed} 须 > 0(AC-6-08;" +
                        "不可通行用 walkable = false 表达,不用速度 0 —— 速度 0 会让 F-6-1 的隧穿断言恒绿)",
                        nameof(rows));
                if (r.KAccel.Raw <= 0)
                    throw new ArgumentException(
                        $"TERRAIN_TABLE 第 {i} 行 K_accel = {r.KAccel.Raw} raw 须 > 0(AC-6-08)", nameof(rows));
                if (r.KDecel.Raw <= 0)
                    throw new ArgumentException(
                        $"TERRAIN_TABLE 第 {i} 行 K_decel = {r.KDecel.Raw} raw 须 > 0(AC-6-08)", nameof(rows));
                if (r.KSpeed == 1) baselineRows++;
            }

            // ② AC-6-08:`K_speed == 1`(基准地貌)**恰好一行**。
            if (baselineRows != 1)
                throw new ArgumentException(
                    $"TERRAIN_TABLE 中 K_speed == 1(基准地貌)须**恰好一行**,实际 = {baselineRows} —— " +
                    "零行 ⇒ 没有基准可参照;多行 ⇒ 「哪一行是基准」成为隐式规则", nameof(rows));

            // ③ 每格 id 必须指向表里存在的行,且长度与几何一致。
            if (cellTerrainId.Count != totalCells)
                throw new ArgumentException(
                    $"每格 terrain_id 数组长度 = {cellTerrainId.Count},须等于世界格数 {totalCells} —— " +
                    "长度不齐 = 地表与几何不是同一张图纸", nameof(cellTerrainId));
            for (int i = 0; i < cellTerrainId.Count; i++)
            {
                if (!byId.Contains(cellTerrainId[i]))
                    throw new ArgumentException(
                        $"每格 terrain_id 第 {i} 项 = {cellTerrainId[i]} 不在 TERRAIN_TABLE " +
                        "{" + string.Join(", ", SortedIds(byId)) + "} 内 —— 运行期查表会取出越界行",
                        nameof(cellTerrainId));
            }

            // ④ 控制器参数的量程 —— 判据**不在这里**,在 `LatticeParamQuantization`(单一出处)。
            //    本节只负责把它执行出来:量纲(量子)与上下界都由那个类型给出,
            //    读侧 codec 调的是同一对谓词 ⇒ 「作者侧放行的值」与「装载侧放行的值」不可能是两套。
            if (!LatticeParamQuantization.IsValidSlopeLimit(slopeLimitDegX100))
                throw new ArgumentOutOfRangeException(
                    nameof(slopeLimitDegX100),
                    $"slopeLimit 量化值 = {slopeLimitDegX100}(0.01 度)超出 " +
                    $"(0, {LatticeParamQuantization.SlopeLimitMaxUnits}] —— 即 (0°, 90°]");
            if (!LatticeParamQuantization.IsValidStepOffset(stepOffsetMm))
                throw new ArgumentOutOfRangeException(
                    nameof(stepOffsetMm),
                    $"stepOffset 量化值 = {stepOffsetMm} mm 超出 " +
                    $"[0, {LatticeParamQuantization.StepOffsetMaxUnits}]");

            var copy = new TerrainRowSpec[rows.Count];
            for (int i = 0; i < rows.Count; i++) copy[i] = rows[i];
            var cells = new int[cellTerrainId.Count];
            for (int i = 0; i < cellTerrainId.Count; i++) cells[i] = cellTerrainId[i];

            Rows = Array.AsReadOnly(copy);
            CellTerrainId = Array.AsReadOnly(cells);
            SlopeLimitDegX100 = slopeLimitDegX100;
            StepOffsetMm = stepOffsetMm;
        }

        /// <summary>`terrain_id → 表内下标`(**单一出处**;导出、绑定、装载共用同一张表推导)。</summary>
        /// <param name="terrainId">地貌 id。</param>
        /// <param name="index">表内下标(未命中 = -1)。</param>
        public bool TryIndexOf(int terrainId, out int index)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].TerrainId == terrainId) { index = i; return true; }
            }
            index = -1;
            return false;
        }

        private static int[] SortedIds(HashSet<int> ids)
        {
            var list = new List<int>(ids);
            list.Sort();
            return list.ToArray();
        }
    }
}
