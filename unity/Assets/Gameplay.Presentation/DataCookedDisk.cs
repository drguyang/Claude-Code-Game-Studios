// 烘焙产物的**磁盘兜底装载**(EditMode / 测试装配用)—— 24 匹配链接线所需的三份。
//
// 权威来源与先例:
//   `Gameplay.Boot/DiagnosisCookedDisk.cs`(sprint-07 S1.2 · 2026-10-11)是**同一形态的首例**:
//   Addressables 组在本机是 gitignored 的本地产物 ⇒ 生产走 `DataCorePreloader`;
//   测试 / EditMode 无 Addressables 运行时 ⇒ 从 `Assets/DataCooked/` 直读**同一份**字节,
//   用**同一个** codec 解码。本类是那条先例的**去诊断专用化**版本(先例自陈只服务诊断三份)。
//
// ⚠️ **本类不是生产装载路径** —— 生产唯一 = `AddressablesDataProvider`(经 `BootRoot` 的
//   `DataCorePreloader`)。本类只补「测试环境无 Addressables」的读路径:
//   · 读的是**同一份烘焙产物**(bake 菜单产物),零合成、零 Fake;
//   · 解码器是**同一批** `*CookedCodec`(与 bake 写方镜像);
//   ∴ 无第二真源。
//
// ⚠️ E-13 同款硬失败:任何缺失 ⇒ 显式 throw,零默认值、零静默降级。

using System;
using System.IO;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>烘焙产物的磁盘兜底读取(EditMode / 测试装配专用;生产走 Addressables)。</summary>
    public static class DataCookedDisk
    {
        /// <summary>产物目录名(相对 `Assets/`;与 `Editor.Tools.Bake.DataBakeMenu.CookedDirName` 同值)。</summary>
        private const string CookedDirName = "DataCooked";

        /// <summary>产物目录绝对路径。</summary>
        public static string CookedDirectory()
            => Path.Combine(Application.dataPath, CookedDirName);

        /// <summary>读产物字节(缺失 ⇒ 抛,不返回空数组)。</summary>
        /// <param name="assetName">产物文件名(如 <c>build_modules.cooked.bytes</c>)。</param>
        /// <exception cref="InvalidOperationException">文件不存在 —— 须先跑 bake。</exception>
        public static byte[] ReadAll(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
                throw new ArgumentException("产物名为空", nameof(assetName));

            string path = Path.Combine(CookedDirectory(), assetName);
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    $"[E-13][disk] 烘焙产物不存在:{path} —— 须先跑 bake" +
                    "(DataBakeMenu / DiagnosisBakeCli.BakeAll)。拒绝返回空数据集。");
            return File.ReadAllBytes(path);
        }

        // ── 24 匹配链的三份(TD-building-01 读侧的装配输入)────────────────────
        // ⚠️ 产物名**引用 codec / provider 侧的常量**,不在此另抄字面量(ADR-024 §⑥ 同口径):
        //    `BuildModulesDataSet.CookedFileName` 是唯一声明处。

        /// <summary>23 模块目录(<c>build_modules.cooked.bytes</c>)。</summary>
        public static BuildModulesDataSet LoadBuildModules()
            => BuildModulesCookedCodec.Read(ReadAll(BuildModulesDataSet.CookedFileName));

        /// <summary>21a 家具目录(<c>item_database_furniture.cooked.bytes</c>)。</summary>
        public static ClinicFurnitureDataSet LoadClinicFurniture()
            => ClinicFurnitureCookedCodec.Read(ReadAll(AddressablesDataProvider.FurnitureCatalogueAddress));

        /// <summary>24 ROOM_TABLE(<c>clinicmachine_room.cooked.bytes</c>)。</summary>
        public static ClinicRoomDataSet LoadClinicRoom()
            => ClinicMachineCookedCodec.ReadRoom(ReadAll(AddressablesDataProvider.ClinicRoomAddress));

        /// <summary>24 ROOM_NAMES 词表(<c>clinicmachine_room_names.cooked.bytes</c>;裁定 ①-C)。</summary>
        public static ClinicRoomNameDataSet LoadClinicRoomNames()
            => ClinicMachineCookedCodec.ReadRoomNames(
                ReadAll(AddressablesDataProvider.ClinicRoomNamesAddress));

        /// <summary>24 CONTEXT_NAMES 词表(<c>clinicmachine_context_names.cooked.bytes</c>;裁定 ②-C)。</summary>
        public static ClinicContextNameDataSet LoadClinicContextNames()
            => ClinicMachineCookedCodec.ReadContextNames(
                ReadAll(AddressablesDataProvider.ClinicContextNamesAddress));

        // ── 9 病种注册表(2026-10-22 B1 接真)────────────────────────────────
        // ⚠️ 与上三份不同:本条的**消费方是生产装配**(`CompositionRoot` 的 dual-path),
        //    不只是测试。它在这里的意义 = 「产物已烘但 Addressables catalog 未重建」时的
        //    同一份字节兜底(同 codec、同校验,非降级)。

        /// <summary>9 病种注册表(<c>disease_registry.cooked.bytes</c>;含全局 <c>W</c> / <c>TREND_EPS</c>)。</summary>
        public static DiseaseRegistryDataSet LoadDiseaseRegistry()
            => DiseaseRegistryCookedCodec.Read(ReadAll(AddressablesDataProvider.DiseaseRegistryAddress));
    }
}
