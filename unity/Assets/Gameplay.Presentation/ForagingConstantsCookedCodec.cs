// `foraging_constants.cooked.bytes`(17 采集调参表)的**运行期解码器**。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物 · `ConfigVersion` 内容哈希)
//   · `CookedFormat.cs`(20 B 头契约)
//   写方严格镜像:`Editor.Tools.Bake/ForagingConstantsCookedWriter.cs`(字段序逐字对齐)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件存在的原因(S22-9 · 2026-11-01)
// ─────────────────────────────────────────────────────────────────────────────
// 产物 `foraging_constants.cooked.bytes` **一直在**(烘焙链完整、Binder 有 8 条校验),
// 但**全仓没有一个运行期读方** —— 实测 `new ForagingConstants(` 生产构造点 = **0**
// ⇒ `NodeRemaining.CanHarvest` / `GatherQualityRoll.HarvestQty` / `OutQuality` 在生产里
// **没有任何调用者能拿到表**。这正是采前闸「接通」所缺的第一块。
//
// ⚠️ **`maxQuality` 不在产物里,由调用方注入**:它是 21a 的 `MAX_QUALITY`
//   (`item_database_constants.json`),真源归 21a —— 写进 17 的产物 = 第二真源。
//   运行期调用方从 `RecipeDataSet.Settlement.MaxQuality` 取(同一份 21a 常量,已有运行期通道)。
//
// ⚠️ 零 float / double:全部 `Fix` 读 raw `i64`(ADR-005/006)。
//
// ⚠️ 住 `Gameplay.Presentation`(边界装载件),与其余 cooked codec 同侧;`Sim` 侧只见
//   `ForagingConstants` 这个领域类型,不见本类。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Foraging;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>`foraging_constants.cooked.bytes` 的运行期解码器。</summary>
    public static class ForagingConstantsCookedCodec
    {
        /// <summary>产物名(与写方 `DataBakeMenu.ForagingConstantsCookedAssetName` 同值)。</summary>
        public const string ProductName = "foraging_constants";

        /// <summary>
        /// 读调参表并构造 <see cref="ForagingConstants"/>。
        /// </summary>
        /// <param name="bytes">cooked 字节(含 20 B 头)。</param>
        /// <param name="maxQuality">21a 的 `MAX_QUALITY`(调用方从 `RecipeDataSet.Settlement` 取)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 载荷长度不符 / 尾部有未消费字节 —— 启动期硬失败。</exception>
        /// <exception cref="ArgumentException">
        /// 表值违反 `ForagingConstants` 构造期的四条硬拒收 —— **原样透出**(单一真源,本类不重写判据)。</exception>
        public static ForagingConstants Read(byte[] bytes, int maxQuality)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out _))
            {
                int seasonsPerYear = r.ReadInt32();

                int capCount = r.ReadInt32();
                if (capCount != ForagingConstants.CapTableLength)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} CapTable 元素数 = {capCount},期望 " +
                        $"{ForagingConstants.CapTableLength}(= SKILL_CAP+1) —— 写读契约漂移");
                var capTable = new int[capCount];
                for (int i = 0; i < capCount; i++) capTable[i] = r.ReadInt32();

                int seasonCount = r.ReadInt32();
                if (seasonCount < 1)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} SEASON_MULT 元素数 = {seasonCount} —— 须 ≥ 1");
                var seasonMult = new Fix[seasonCount];
                for (int i = 0; i < seasonCount; i++) seasonMult[i] = new Fix(r.ReadInt64());

                long regrowWindowTicks = r.ReadInt64();
                int baseCapacity = r.ReadInt32();
                var decayRate = new Fix(r.ReadInt64());
                var decayFloor = new Fix(r.ReadInt64());
                var gatherMulCap = new Fix(r.ReadInt64());

                CookedReadUtil.AssertFullyConsumed(r, ProductName);

                // 形状/值域判据全部在 `ForagingConstants` 构造期(AC-17-08b / 14 / 16),
                // 本类**不重写** —— 重写一遍就是第二真源。
                return new ForagingConstants(capTable, seasonMult, seasonsPerYear,
                                             regrowWindowTicks, baseCapacity,
                                             decayRate, decayFloor, gatherMulCap, maxQuality);
            }
        }
    }
}
