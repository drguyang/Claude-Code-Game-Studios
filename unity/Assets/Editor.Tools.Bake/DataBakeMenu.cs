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
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Gameplay.Presentation;
using Stopwatch = System.Diagnostics.Stopwatch; // 别名引入,避免 System.Diagnostics.Debug 与 UnityEngine.Debug 歧义

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
                // 仓根 = Assets 上两级(unity/Assets → 仓根,含 assets/ —— BakeFromRepo 契约)。
                // 只上一级会停在 unity/,拼出 unity/assets/data/ 假路径(Linux 区分大小写直接炸)。
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

                // AC-26:旧 .asset 遗留产物先行扫描(数据产物层扫描面,不只查新增 JSON)。
                IReadOnlyList<string> assetViolations = StateEncodingScanner.ScanAssetProducts(Application.dataPath);
                if (assetViolations.Count > 0)
                    throw new BakeValidationException(new List<string>(assetViolations));

                BakeResult result = ItemDatabaseBaker.BakeFromRepo(repoRoot);

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

        /// <summary>
        /// E-13 自检(ADR-014 §五:装载失败 = 带 [E-13] 上下文的启动期硬失败,绝不 null 解引用)。
        /// <para>流程:① 反向 —— 把 items 产物文件+meta 暂移到 <c>Library/E13Hold/</c>
        /// (资产从 AssetDatabase 消失 ⇒ 经地址解析到的 GUID 载不到)→ 新 <see cref="AddressablesDataProvider"/>
        /// 取数,期望抛 <see cref="InvalidOperationException"/> 且消息含 [E-13];<c>finally</c> 必移回 +
        /// Refresh;② 正向复验 —— 新 provider 载 items + recipes,期望成功。</para>
        /// <para>⚠️ <b>不用「改 address」方案</b>(2026-09-24 两次实测,含重启后单跑,均「反向未触发」):
        /// 运行期 locator 在域初始化时按 settings 建好,<c>SetAddress(postEvent)</c> 只通知编辑器 UI、
        /// **不重建 locator** —— 陈旧发生在菜单内 SetAddress 与 Load 之间,重启不解决。
        /// 文件暂移不改地址映射,失败发生在 GUID→资产 解析层,绕开 locator。</para>
        /// <para>每次点菜单都用<b>全新</b> provider(绕开 <see cref="DataCorePreloader"/> 幂等)。
        /// ⚠️ 本会话内若已成功载过该资产,活句柄会被直接复用(与磁盘无关)⇒ 拉取后的自动域重载
        /// (或重启)清句柄后,只点本菜单一次。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/E-13 自检(反路径+正复验)")]
        public static void E13SelfCheck()
        {
            // 产物绝对路径 + 暂存位(Library 在仓外纪律面:.gitignore,永不提交)
            string itemsAbs = Path.Combine(Application.dataPath, CookedDirName, ItemsCookedAssetName);
            string itemsMetaAbs = itemsAbs + ".meta";
            string holdDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "E13Hold"));
            string holdItems = Path.Combine(holdDir, ItemsCookedAssetName);
            string holdMeta = holdItems + ".meta";

            // 救援:上次会话中途崩溃致产物滞留 hold → 先归位(否则正向必失败)
            if (!File.Exists(itemsAbs) && File.Exists(holdItems))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(itemsAbs));
                File.Move(holdItems, itemsAbs);
                if (File.Exists(holdMeta)) File.Move(holdMeta, itemsMetaAbs);
                AssetDatabase.Refresh();
                Debug.Log("[E-13 自检] 已将滞留 Library/E13Hold 的产物归位。");
            }

            // ── ① 反向:暂移 items 产物(文件 + meta 一起动,保 GUID 一致)──
            if (!File.Exists(itemsAbs))
            {
                Debug.LogWarning(
                    $"[E-13 自检] 反向跳过:产物不存在({ItemsCookedAssetName})—— " +
                    "先跑「烘焙 item-database」再执行本菜单(正向仍继续)。");
            }
            else
            {
                bool movedOut = false;
                try
                {
                    Directory.CreateDirectory(holdDir);
                    if (File.Exists(holdItems)) File.Delete(holdItems); // 异常残留清理
                    if (File.Exists(holdMeta)) File.Delete(holdMeta);
                    File.Move(itemsAbs, holdItems);
                    if (File.Exists(itemsMetaAbs)) File.Move(itemsMetaAbs, holdMeta);
                    AssetDatabase.Refresh();
                    movedOut = true;

                    try
                    {
                        var negative = new AddressablesDataProvider();
                        negative.Load<ItemDataSet>();
                        Debug.LogWarning(
                            "[E-13 自检] 反向未触发:产物已移出仍载成功 —— 两种可能:" +
                            "① 本会话此前已成功载过该资产(活句柄直接复用,与磁盘无关)→ 等脚本重编译/重启后只点本菜单一次;" +
                            "② Play Mode Script 走了已建 bundle(源文件移出不影响 bundle 内容)→ " +
                            "Groups 窗口确认 Use Asset Database,或 Addressables > Clean Built Content。");
                    }
                    catch (InvalidOperationException ex) when (
                        ex.Message.IndexOf("[E-13]", StringComparison.Ordinal) >= 0)
                    {
                        Debug.Log($"[E-13 自检] 反向通过:硬失败带 [E-13] 上下文、非 null 解引用 ✓ —— {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"[E-13 自检] 反向异常但缺 [E-13] 包裹(违 ADR-014 §五):" +
                            $"{ex.GetType().Name}: {ex.Message}");
                    }
                }
                finally
                {
                    if (movedOut)
                    {
                        if (File.Exists(holdItems) && !File.Exists(itemsAbs))
                            File.Move(holdItems, itemsAbs);
                        if (File.Exists(holdMeta) && !File.Exists(itemsMetaAbs))
                            File.Move(holdMeta, itemsMetaAbs);
                        AssetDatabase.Refresh(); // 归位 + 重导入(GUID 随 meta 原样回来)
                    }
                }
            }

            // ── ② 正向复验(全新 provider 真取数)──
            try
            {
                var positive = new AddressablesDataProvider();
                ItemDataSet items = positive.Load<ItemDataSet>();
                RecipeDataSet recipes = positive.Load<RecipeDataSet>();
                Debug.Log(
                    $"[E-13 自检] 正向复验通过:items={items.Items?.Length ?? 0}, " +
                    $"recipes={recipes.Recipes?.Length ?? 0}, ConfigVersion=0x{items.ConfigVersion:X8} ✓");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[E-13 自检] 正向复验失败(应可载):{ex.Message}");
            }
        }

        /// <summary>
        /// AC-21a-47 性能冒烟一键计时(方法与阈值见 production/qa/smoke-2026-09-24.md):
        /// ① 首次装载 = 全新 provider 的 <c>Load&lt;ItemDataSet&gt;</c> 单发;
        /// ② 缓存命中 = 同 provider 1000 次 <c>Load</c> 中位数;
        /// ③ 单次求解 = <see cref="RecipeSettlementSolver.Solve"/> 以首条配方 1000 次中位数。
        /// 阈值:三项各 &lt; 1 ms(超阈 = 调优信号,非测试失败)。结果同时打进 Console 与剪贴板。
        /// <para>前置:先「烘焙 item-database」+「确保 data-core Addressables 组」。
        /// 想要相对干净的「首次」,重启编辑器后先跑本菜单(先于 E-13 自检)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/性能冒烟:AC-47 计时")]
        public static void Ac47Timing()
        {
            const int iterations = 1000;
            const int probeSkill = 30;   // 域中值(skill_cap=60;承 RecipeSettlementRequest 构造器示例)
            const int probeQuality = 3;  // 域中值(max_quality=5)

            var timed = new AddressablesDataProvider();
            var clock = Stopwatch.StartNew();

            // ① 首次装载(单发;会话内 Addressables 若已缓存会偏低 —— 日志注明运行态)
            clock.Restart();
            ItemDataSet items = timed.Load<ItemDataSet>();
            clock.Stop();
            long firstUs = ToMicros(clock.ElapsedTicks);

            // ② 缓存命中(同 provider 1000 次中位)
            var samples = new long[iterations];
            for (int i = 0; i < samples.Length; i++)
            {
                clock.Restart();
                timed.Load<ItemDataSet>();
                clock.Stop();
                samples[i] = clock.ElapsedTicks;
            }
            long cachedUs = MedianMicros(samples);

            // ③ 单次求解(首条配方构 request;1000 次中位)
            RecipeDataSet recipes = timed.Load<RecipeDataSet>();
            if (recipes.Recipes == null || recipes.Recipes.Length == 0)
            {
                Debug.LogError("[AC-47] 配方表为空 —— 先跑「烘焙 item-database」。");
                return;
            }
            Recipe template = recipes.Recipes[0];
            var request = new RecipeSettlementRequest(
                template.Outputs, template.Inputs, probeSkill, probeQuality,
                default(Fix), default(Fix), default(Fix));
            var constants = recipes.Settlement;
            for (int i = 0; i < samples.Length; i++)
            {
                clock.Restart();
                RecipeSettlementSolver.Solve(in request, in constants);
                clock.Stop();
                samples[i] = clock.ElapsedTicks;
            }
            long solveUs = MedianMicros(samples);

            string line =
                $"[AC-47] 首次装载 = {firstUs} μs;缓存命中 = {cachedUs} μs;单次求解 = {solveUs} μs" +
                $"(阈值各 <1000 μs;缓存/求解 = {iterations} 次中位数,首次 = 单发;" +
                $"items={items.Items?.Length ?? 0}, recipes={recipes.Recipes.Length};" +
                $"{(Application.isPlaying ? "PlayMode" : "EditMode")};已复制到剪贴板)";
            Debug.Log(line);
            EditorGUIUtility.systemCopyBuffer = line;
        }

        /// <summary>tick → 微秒(Stopwatch 原始 tick 直除,避免浮点)。</summary>
        private static long ToMicros(long elapsedTicks) =>
            elapsedTicks * 1_000_000L / Stopwatch.Frequency;

        /// <summary>样本中位微秒(排序后取中;1000 偶数取偏右中点,与 smoke 方法「1000 次中位数」对齐)。</summary>
        private static long MedianMicros(long[] tickSamples)
        {
            Array.Sort(tickSamples);
            return ToMicros(tickSamples[tickSamples.Length / 2]);
        }
    }
}
