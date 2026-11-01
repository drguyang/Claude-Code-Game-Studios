// `inventory_constants.json`(20 的调参表)的 deterministic cooked 写方。
//
// 权威来源:
//   ADR-014 §二/§五(两阶段烘焙 · ConfigVersion 内容哈希)· `CookedFormat.cs`(20 B 头)
//   · `InventoryConstantsBinder.BindResult`(数据唯一来源)
//
// ⚠️ **载荷布局(全部 little-endian)** —— 与运行期读方严格镜像:
//   carry_cap : i32     (`CARRY_CAP`,GDD :283 已裁 60;int 计数口径,非 Fix)
//
// ⚠️ **为什么现在才写**(S22-9 · 2026-11-01):Binder 早在(`AC-20-13` 的构建期执行体),
//   但**从来没有 cooked 产物** ⇒ 运行期拿不到 `CARRY_CAP` ⇒ `CarryLoadSolver.CanCarry`
//   在生产里**零调用者**(唯一一处 `new CarryCapacity(` 是编辑期门)。
//   采前闸的第一输入就是它。
//
// ⚠️ **本表刻意只有 `carry_cap` 一个键**(binder 白名单即契约):`W_MIN` 是
//   `min{ItemDef.weight}` 的派生量,真源 = `item_database_items.json` —— 写进本表即第二真值。

using System;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`inventory_constants`(20 的调参表)deterministic cooked 写入器。</summary>
    internal static class InventoryConstantsCookedWriter
    {
        /// <summary>编码调参表(含 20 字节头)。</summary>
        public static byte[] Write(
            in InventoryConstantsBinder.BindResult r, uint schemaVersion, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, System.Text.Encoding.UTF8))
            {
                w.Write(r.CarryCap);
                w.Flush();
                return ClinicMachineCookedWriter.FinishHeader(
                    payload.ToArray(), schemaVersion, configVersion);
            }
        }
    }
}
