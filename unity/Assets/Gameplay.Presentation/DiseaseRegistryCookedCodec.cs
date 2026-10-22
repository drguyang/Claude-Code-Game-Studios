// 9 病种注册表 cooked 读方 —— **与 `Editor.Tools.Bake/DiseaseRegistryCookedWriter` 严格镜像**
// (2026-10-22 B1 补立;此前只有写方,产物无人消费)。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析器 · E-13 = 启动期硬失败,零默认值)
//   ADR-006 §四(`Fix` = raw long;本件不做任何 `FixParse`)
//   `CookedFormat.cs`(20 字节头 + 载荷内字符串 = i32 字节长 + UTF-8)
//   写方镜像 = `Editor.Tools.Bake/DiseaseRegistryCookedWriter.cs`
//
// ⚠️ **布局(全部 little-endian;固定字段顺序)**:
//     u32 schema_version        (载荷首字段,与头槽 offset 8 回声 —— 两者必须相等)
//     i64 trend_window_ticks    (全局 W;0 = 未配置)
//     i64 trend_eps             (全局 TREND_EPS,raw;0 = 未配置)
//     i32 row_count             (-1 = null 标记 ⇒ 拒收)
//     逐行:见 ReadRow(字段序 = WriteRow 的书写序)
//
// ⚠️ **加载期门在解码之后、返回之前**:即使上游绕过 binder 写出越界值,
//    read 也会经 `RegistrySchemaValidator.ValidateTrendWindow` 拒收(R1-36)。
//    这是「构建期 + 加载期两道门」的**第二道**,落在生产代码里。
// ⚠️ 零 `System.IO` 词面 —— 本件不做文件 I/O,字节由 Addressables / 磁盘兜底喂入。
// ⚠️ 改任一侧布局必须同改另一侧(唯一自动兜现 = `DiseaseRegistryBaker.Materialize` 真往返)。
// ⚠️ **不校验**病种行的 17 条 R1 规则 —— 那是**构建期** binder 的活(ADR-014 §三)。
//    运行期重复校验 = 同一判据两份实现,两者可分叉;且 `Validate` 抛的是
//    `RegistryValidationException`,与 E-13 的失败语义混在一起会模糊归因。
//    本件只查**形状**(长度 / 魔数 / 版本 / 枚举域 / 载荷耗尽)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>病种注册表烘焙产物的读方(写方 = <c>Editor.Tools.Bake.DiseaseRegistryCookedWriter</c>)。</summary>
    public static class DiseaseRegistryCookedCodec
    {
        /// <summary>读方认领的 schema_version(不匹配 = E-13 拒收)。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>产物基名(仅用于错误报文;真名单一出处 = <see cref="DiseaseRegistryDataSet.CookedFileName"/>)。</summary>
        private const string ProductName = "disease_registry";

        /// <summary>读注册表(全局趋势窗口 + 逐行条目)。</summary>
        /// <param name="bytes">产物全字节(含 20 字节头)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 版本不匹配 / 载荷截断 / 载荷有剩余 —— E-13 硬失败。</exception>
        /// <exception cref="InvalidOperationException">趋势窗口值域越界(R1-36 加载期门)。</exception>
        public static DiseaseRegistryDataSet Read(byte[] bytes)
        {
            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                // ── 载荷首字段:schema_version 回声(与头槽 offset 8 必须相等)──
                uint payloadSchemaVersion = r.ReadUInt32();
                uint headerSchemaVersion = ReadHeaderSchemaVersion(bytes);
                if (payloadSchemaVersion != headerSchemaVersion)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} 载荷 schema_version={payloadSchemaVersion} " +
                        $"≠ 头槽 {headerSchemaVersion} —— 同一真值两处存且已分叉(写方契约:两者必须相等)");
                if (headerSchemaVersion != ExpectedSchemaVersion)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} schemaVersion={headerSchemaVersion} ≠ " +
                        $"{ExpectedSchemaVersion} —— 版本不匹配(E-13)");

                // ── 全局趋势窗口(W / TREND_EPS,GDD Tuning §四 :1743-1744)──
                long trendWindowTicks = r.ReadInt64();
                long trendEpsRaw = r.ReadInt64();

                // ── 行集 ──
                int rowCount = r.ReadInt32();
                if (rowCount < 0)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} row_count={rowCount}(null 标记)—— 产物损坏,启动期硬失败(E-13)");
                if (rowCount == 0)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} row_count=0 —— R1-17 要求至少 1 个病种;" +
                        "空表会让体征链对任何 DiseaseOnset fail-loud,拒绝以空注册表启动(E-13)");

                var rows = new List<DiseaseRegistryEntry>(rowCount);
                for (int i = 0; i < rowCount; i++)
                    rows.Add(ReadRow(r, i));

                CookedReadUtil.AssertFullyConsumed(r, ProductName);

                // ── 加载期门(R1-36 第二半)──
                //  ⚠️ 抛 `InvalidOperationException`(E-13 口径)而不是让
                //     `RegistryValidationException` 冒出去:调用方(provider / 磁盘兜底)
                //     只认 E-13,别的异常类型会被 `catch (Exception ex) when (!(ex is InvalidOperationException))`
                //     二次包装,报文变两层且归因模糊。
                try
                {
                    RegistrySchemaValidator.ValidateTrendWindow(trendWindowTicks, trendEpsRaw);
                }
                catch (RegistryValidationException ex)
                {
                    throw E13("趋势窗口越界 —— 加载期硬失败,不得静默取默认值:\n  " + ex.Message, ex);
                }

                return new DiseaseRegistryDataSet(rows, trendWindowTicks, trendEpsRaw,
                                                  headerSchemaVersion, configVersion);
            }
        }

        /// <summary>读一行(字段序 = <c>DiseaseRegistryCookedWriter.WriteRow</c> 的书写序)。</summary>
        private static DiseaseRegistryEntry ReadRow(BinaryReader r, int index)
        {
            string where = $"rows[{index}]";

            var e = new DiseaseRegistryEntry
            {
                DiseaseId = r.ReadInt32(),
                DiseaseKey = CookedReadUtil.ReadString32(r, $"{ProductName}.{where}.key"),
                Kind = ReadKind(r.ReadInt32(), where),
                Polarity = r.ReadInt32(),
                Severity = r.ReadInt32(),
                Contagion = r.ReadInt32(),
                Lethality = r.ReadInt32(),
                TreatmentDifficulty = r.ReadInt32(),
                RecoveryTime = r.ReadInt32(),
                RelapseChance = r.ReadInt32(),
                ComorbidityFactor = r.ReadInt32(),
                SeasonalMod = r.ReadInt32(),
                AgeMod = r.ReadInt32(),
                GenderMod = r.ReadInt32(),
                OccupationMod = r.ReadInt32(),
                RegionMod = r.ReadInt32(),
                ClimateMod = r.ReadInt32(),
                Scale = new Fix(r.ReadInt64()),      // Fix = raw long
                TraumaHalfLife = r.ReadInt32(),
            };

            // ── curve(hasCurve 标志 + 条件块)──
            bool hasCurve = r.ReadBoolean();
            if (hasCurve)
            {
                var c = new NaturalProgressCurve
                {
                    Incubation = r.ReadInt32(),
                    APeak = new Fix(r.ReadInt64()),
                    TauRise = new Fix(r.ReadInt64()),
                    TauPeak = r.ReadInt32(),
                    TauFall = new Fix(r.ReadInt64()),
                    Plateau = r.ReadBoolean(),
                    SelfLimit = r.ReadBoolean(),
                };
                bool hasSigma = r.ReadBoolean();
                c.Sigma = hasSigma ? new Fix(r.ReadInt64()) : (Fix?)null;
                c.BoundaryMode = CookedReadUtil.ReadString32(r, $"{ProductName}.{where}.boundary_mode");
                e.Curve = c;
            }

            // ── 未编码的三块(写方亦不写)──
            //  ⚠️ 显式置 null 而**不是**留默认值:读者须一眼看出「本产物不携带这三块」,
            //    而不是以为「它们恰好为空」。与写方文件尾 §未实现登记 ② 一一对应。
            e.TreatableBy = null;
            e.Relapse = null;
            e.Signs = null;
            e.Handle = null;

            return e;
        }

        /// <summary>枚举域守卫 —— `DiseaseKind` 的 0 值(`Unspecified`)是零值陷阱,一律拒收。</summary>
        private static DiseaseKind ReadKind(int raw, string where)
        {
            if (raw != (int)DiseaseKind.Disease
                && raw != (int)DiseaseKind.Injury
                && raw != (int)DiseaseKind.Poison)
                throw new InvalidDataException(
                    $"[cooked] {ProductName}.{where}.kind={raw} 不在 {{1,2,3}} 内 —— " +
                    "产物损坏或写方引入了新枚举值而读方未同步(E-13)");
            return (DiseaseKind)raw;
        }

        /// <summary>取头槽 offset 8 的 schema_version(字节已由 <see cref="CookedReadUtil.OpenChecked"/> 验过长度)。</summary>
        private static uint ReadHeaderSchemaVersion(byte[] b)
            => (uint)(b[8] | (b[9] << 8) | (b[10] << 16) | (b[11] << 24));

        private static InvalidOperationException E13(string detail, Exception inner = null)
            => new InvalidOperationException(
                "[E-13][disease_registry] cooked 读取失败 —— 启动期硬失败,绝不降级:" + detail, inner);
    }
}
