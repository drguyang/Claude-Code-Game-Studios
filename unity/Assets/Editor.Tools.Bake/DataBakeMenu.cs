// 权威来源:ADR-014 §五(Addressables 单一 data-core 组 + 首次 Step 前预载)· ADR-023(构建期断言纪律)
//          · Story 008(编辑器菜单:烘焙 + 确保 data-core 组)
//
// ⚠️ AddressableAssetsData/ 是机器生成物 —— **永不提交**(本菜单改它只在本机生效;
//    组结构由桌面首次执行菜单时生成,进 .gitignore 纪律,不手改、不入库)。
// ⚠️ Addressables 6.2+ / UnityEditor.AddressableAssets API 为 post-cutoff —— 桌面首跑须实测
//    (ADR-014 Engine Knowledge Risk MEDIUM;本文件只在编辑器菜单触发,不进玩家构建)。

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>数据管线编辑器菜单(烘焙产物 + Addressables 组维护)。</summary>
    public static class DataBakeMenu
    {
        /// <summary>Addressables 组名(ADR-014 §五:单一 data-core 组)。</summary>
        public const string DataCoreGroup = "data-core";

        /// <summary>产物目录(Assets 下;*.cooked.bytes 以此为 Addressables 条目)。</summary>
        public const string CookedDirName = "DataCooked";

        /// <summary>产物文件名(与 CookedWriter / 运行期 AddressablesDataProvider.Address 常量一致)。</summary>
        public const string ItemsCookedAssetName = "item_database_items.cooked.bytes";

        /// <summary>产物文件名(配方 + 常量)。</summary>
        public const string RecipesCookedAssetName = "item_database_recipes.cooked.bytes";

        /// <summary>
        /// 烘焙 item-database:仓库 assets/data 三源 → 校验(聚合 throw)→ 产物写入 Assets/DataCooked/。
        /// <para>写盘前额外跑 AC-26 产物扫描(全 Assets 下 *.asset,含旧遗留文件)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 item-database")]
        public static void BakeItemDatabase()
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

                // AC-26:旧 .asset 遗留产物先行扫描(数据产物层扫描面,不只查新增 JSON)。
                IReadOnlyList<string> assetViolations = StateEncodingScanner.ScanAssetProducts(Application.dataPath);
                if (assetViolations.Count > 0)
                    throw new BakeValidationException(new List<string>(assetViolations));

                BakeResult result = ItemDatabaseBaker.BakeFromRepo(projectRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string itemsPath = Path.Combine(cookedDir, ItemsCookedAssetName);
                string recipesPath = Path.Combine(cookedDir, RecipesCookedAssetName);
                File.WriteAllBytes(itemsPath, result.ItemsCooked);
                File.WriteAllBytes(recipesPath, result.RecipesCooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:item_database_items.cooked.bytes = {result.ItemsCooked.Length} B, " +
                    $"item_database_recipes.cooked.bytes = {result.RecipesCooked.Length} B, " +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}({DataCoreGroup} 组条目须经下一菜单项确保)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 确保 Addressables 存在单一 <c>data-core</c> 组,并把 DataCooked 下两个产物移入、打上 data-core 标签。
        /// <para>⚠️ 本操作会改动本地 <c>Assets/AddressableAssetsData/</c> —— 机器生成物,
        /// <b>永不提交</b>(每台机器桌面首次执行即可)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/确保 data-core Addressables 组")]
        public static void EnsureDataCoreGroup()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError(
                    "[大医精诚] Addressables 设置不存在 —— 先经 Window > Asset Management > Addressables > Groups " +
                    "创建一次设置(一次性);之后本菜单负责组与条目。");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(DataCoreGroup);
            if (group == null)
            {
                // 签名承 Addressables 2.10 官方文档:第 5 参 = schemasToCopy(List,可 null),
                // 其后 params Type[] = 要新建的 schema 类型。
                group = settings.CreateGroup(
                    DataCoreGroup, false, false, false,
                    null,
                    typeof(AddressableAssetGroupSchema), typeof(BundledAssetGroupSchema));
            }

            if (!settings.GetLabels().Contains(DataCoreGroup))
                settings.AddLabel(DataCoreGroup, postEvent: true);

            string cookedDirRel = $"Assets/{CookedDirName}";
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { cookedDirRel });
            int moved = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileName(path);
                if (!fileName.EndsWith(".cooked.bytes", StringComparison.Ordinal))
                    continue;

                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: true);
                entry.SetAddress(fileName);
                entry.SetLabel(DataCoreGroup, true, force: true, settings);
                moved++;
            }

            if (moved == 0)
            {
                Debug.LogWarning(
                    $"[大医精诚] {cookedDirRel} 下无 *.cooked.bytes —— 先跑「烘焙 item-database」再执行本菜单。");
                return;
            }

            Debug.Log(
                $"[大医精诚] {DataCoreGroup} 组就绪:{moved} 个条目已入组并打标签。" +
                "(AddressableAssetsData 为机器生成物,永不提交)");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
