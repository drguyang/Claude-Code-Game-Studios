// `inventory_constants.cooked.bytes`(20 库存调参表)的**运行期解码器**。
//
// 权威来源:ADR-014 §二/§五 · `CookedFormat.cs`(20 B 头)
//   写方严格镜像:`Editor.Tools.Bake/InventoryConstantsCookedWriter.cs`
//
// ⚠️ 载荷布局(全部 little-endian):
//   carry_cap : i32
//
// ⚠️ **本文件为什么存在**(S22-9 · 2026-11-01):`CarryLoadSolver.CanCarry` 的容量输入
//   在生产里**零来源** —— 20 的常量表只有 Binder(构建期)与编辑期门,
//   既无 cooked 产物也无运行期读方 ⇒ 采前闸的「满载」那一侧无从求值。
//
// ⚠️ 住 `Gameplay.Presentation`(边界装载件);`Sim` 侧只见 `CarryCapacity` 这个领域类型。

using System;
using System.IO;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>`inventory_constants.cooked.bytes` 的运行期解码器。</summary>
    public static class InventoryConstantsCookedCodec
    {
        /// <summary>产物名(与写方 `InventoryConstantsBake.CookedFileName` 同值)。</summary>
        public const string ProductName = "inventory_constants";

        /// <summary>
        /// 读 `CARRY_CAP`。
        /// </summary>
        /// <param name="bytes">cooked 字节(含 20 B 头)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 载荷长度不符 / 尾部有未消费字节。</exception>
        /// <exception cref="ArgumentException">`CARRY_CAP` 非正 —— 原样透出 `CarryCapacity` 构造期的判据。</exception>
        public static int ReadCarryCap(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out _))
            {
                int carryCap = r.ReadInt32();
                CookedReadUtil.AssertFullyConsumed(r, ProductName);

                // ⚠️ 非正值判据归 `CarryCapacity` 构造期(AC-20-13:未定值 ⇒ 装载期硬失败),
                //    本类不重写 —— 这里只把字节读出来。
                if (carryCap <= 0)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} carry_cap = {carryCap} —— 须 > 0" +
                        "(AC-20-13:容量未定值/损坏必须在装载期硬失败,禁 null 兜底)");
                return carryCap;
            }
        }
    }
}
