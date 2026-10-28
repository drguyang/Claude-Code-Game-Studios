// 权威来源:ADR-014 §五(Addressables 单一 data-core 组 + 首次 Step 前预载)· ADR-023(构建期断言纪律)
//          · Story 008(编辑器菜单:烘焙 + 确保 data-core 组)
//
// ⚠️ AddressableAssetsData/ 的**组定义已随仓库入库**(2026-10-17 · TD-audio-01a 收窄 .gitignore)。
//    仍被忽略的只有逐平台的 `*/addressables_content_state.bin`(真·机器本地产物)。
//    ⇒ 本菜单只负责**条目**;设置缺失 = 入库文件被删/损坏,恢复靠 git checkout,不靠重建。
//    (原文「永不提交」写于组定义入库之前,已失效 —— 保留此行作为口径沿革记录。)
// ⚠️ Addressables 6.2+ / UnityEditor.AddressableAssets API 为 post-cutoff —— 桌面首跑须实测
//    (ADR-014 Engine Knowledge Risk MEDIUM;本文件只在编辑器菜单触发,不进玩家构建)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Telemetry;   // TelemetryAnalyticsDataSet(第十二片:单一出处常量)
using DaYiJingCheng.EditorTools.Gates;   // AudioAssetGroupGates(S14-6:audio 组名 / 素材目录常量单一出处)
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

        /// <summary>产物文件名(交互数据契约行集;<c>4-DC-1…6</c> 的烘焙产物)。</summary>
        public const string InteractionKindsCookedAssetName = "interaction_kinds.cooked.bytes";

        /// <summary>产物文件名(8 词条表;R-8.1 / AC-8-32…35 的烘焙产物)。</summary>
        public const string DiagnosisSignsCookedAssetName = "diagnosis_signs.cooked.bytes";

        /// <summary>产物文件名(8 门槛定表;F-8.1 / AC-8-5…8-8 的烘焙产物)。</summary>
        public const string DiagnosisReadFloorCookedAssetName = "diagnosis_read_floor.cooked.bytes";

        /// <summary>F-8.3 阴性把握度定表产物名(story 004)。</summary>
        public const string DiagnosisNegativeConfidenceCookedAssetName =
            "diagnosis_negative_confidence.cooked.bytes";

        /// <summary>产物文件名(处方表 + 本草词表;DC-1…DC-7 / AC-11-02/20 的烘焙产物)。</summary>
        public const string PrescriptionActionsCookedAssetName = "prescription_actions.cooked.bytes";

        /// <summary>产物文件名(处置轴 + treatable_by 关系;9-DC-1…7 / AC-9-01…20 的烘焙产物)。</summary>
        public const string DiseaseActionAxisCookedAssetName = "disease_action_axis.cooked.bytes";

        /// <summary>产物文件名(病名册词表;37 Judgment / AC-37-35 / sprint-08 S1.0)。</summary>
        public const string JudgmentLexiconCookedAssetName = "case_judgment_lexicon.cooked.bytes";

        /// <summary>产物文件名(53 判定表;F-53.1 / story-002 的烘焙产物)。</summary>
        public const string OutcomeTableCookedAssetName = "53_outcome_table.cooked.bytes";

        /// <summary>产物文件名(53 延迟表;F-53.2 / story-003 的烘焙产物)。</summary>
        public const string DelayTableCookedAssetName = "53_delay_table.cooked.bytes";

        /// <summary>产物文件名(48 教学内容;S17-3 story-001 / AC-5 的烘焙产物)。
        /// <para>⚠️ 与运行期 <c>AddressablesDataProvider.TutorialContentAddress</c> /
        /// <c>TutorialContentTable.CookedFileName</c> 同值(边界程序集不引用本编辑期装配,
        /// 故那两处为字面串 —— 承 <c>InteractionKindsAddress</c> 同款注记)。</para></summary>
        public const string TutorialContentCookedAssetName = "48_tutorial_content.cooked.bytes";

        /// <summary>产物文件名(24 CONTEXT_TABLE;clinic-001 的三表之一)。
        /// <para>⚠️ 与运行期 <c>AddressablesDataProvider.ClinicContextAddress</c> 同值(边界程序集不引用本编辑期装配,
        /// 故那处为字面串 —— 承 <c>InteractionKindsAddress</c> 同款注记)。</para></summary>
        public const string ClinicContextCookedAssetName = "clinicmachine_context.cooked.bytes";

        /// <summary>产物文件名(24 ADJ_TABLE)。</summary>
        public const string ClinicAdjCookedAssetName = "clinicmachine_adj.cooked.bytes";

        /// <summary>产物文件名(24 ROOM_TABLE)。</summary>
        public const string ClinicRoomCookedAssetName = "clinicmachine_room.cooked.bytes";

        /// <summary>产物文件名(24 ROOM_NAMES 词表;2026-10-26 裁定 ①-C)。
        /// <para>⚠️ 与运行期 <c>AddressablesDataProvider.ClinicRoomNamesAddress</c> 同值(同一段注记理由)。</para></summary>
        public const string ClinicRoomNamesCookedAssetName = "clinicmachine_room_names.cooked.bytes";

        /// <summary>产物文件名(24 CONTEXT_NAMES 词表;2026-10-26 裁定 ②-C)。</summary>
        public const string ClinicContextNamesCookedAssetName = "clinicmachine_context_names.cooked.bytes";

        /// <summary>产物文件名(21a 家具目录 = <c>FURN_TABLE</c>;O-24-1 / <c>TD-clinic-01</c> 的兑现件)。
        /// <para>⚠️ 与运行期 <c>AddressablesDataProvider.FurnitureCatalogueAddress</c> 同值(同一段注记理由)。</para></summary>
        public const string FurnitureCatalogueCookedAssetName = "item_database_furniture.cooked.bytes";

        /// <summary>产物文件名(23 模块目录 = `build_modules.json`;<c>TD-itemdb-01</c> 的兑现件)。
        /// <para>⚠️ 本产物**今日无运行期消费方** —— `IModuleCatalog` 的生产实现为零
        /// (`TD-itemdb-01` 的实测 ⑦)。落产物的意义是:作者态表存在、过四道域门、字节确定性可复算;
        /// **接线归 23 的装配轮**,不在本件(承「补烘 ≠ 登记 ≠ 接线」三分)。</para></summary>
        public const string BuildModulesCookedAssetName = "build_modules.cooked.bytes";

        /// <summary>产物文件名(建造槽位骨架 = `world_buildslots.json`;ADR-022 §三导出契约的烘焙产物)。
        /// <para>⚠️ 作者态 JSON 的**唯一作者 = 关卡工具**(`Editor.Tools.Level.LevelExporter`),
        /// 手写该 JSON 违反 ADR-022 §二。</para></summary>
        public const string WorldBuildSlotsCookedAssetName = WorldBuildSlotsBake.CookedFileName;

        /// <summary>产物文件名(世界几何 = `world_geometry.json`;关卡工具 story-002 · 2026-10-29)。
        /// <para>⚠️ 作者态 JSON 的**唯一作者 = 关卡工具**,手写违反 ADR-022 §二。
        /// 运行期消费方 = `MinimalWorldFactory.Create()`(**无降级路径**:产物缺失即启动期硬失败)。</para></summary>
        public const string WorldGeometryCookedAssetName = WorldGeometryBake.CookedFileName;

        /// <summary>产物文件名(地形表 = `world_terrain.json`;关卡工具 story-002 · 2026-10-29)。
        /// <para>⚠️ 与 <see cref="WorldGeometryCookedAssetName"/> **必须同批烘焙**:两者的
        /// `world_size` 装载期互相断言;只烘一份 = 启动期硬失败。</para></summary>
        public const string WorldTerrainCookedAssetName = WorldTerrainBake.CookedFileName;

        /// <summary>产物文件名(生态区多边形 = `world_ecozones.json`;关卡工具 story-003 · 2026-10-29)。
        /// <para>⚠️ 作者态 JSON 的**唯一作者 = 关卡工具**,手写违反 ADR-022 §二。
        /// 运行期消费方 = `CompositionRoot` 的生态区注册表装配(`EcozoneOf` 的真值来源)。</para></summary>
        public const string WorldEcozonesCookedAssetName = WorldEcozonesBake.CookedFileName;

        /// <summary>`world_poi` 产物名(关卡工具 story-004)。</summary>
        public const string WorldPoiCookedAssetName = WorldPoiBake.CookedFileName;

        /// <summary>`world_resources` 产物名(关卡工具 story-004)。</summary>
        public const string WorldResourcesCookedAssetName = WorldResourcesBake.CookedFileName;

        /// <summary>产物文件名(17 采集调参表 = `foraging_constants.json`;S20-3 评审 W-3 甲案的兑现件)。
        /// <para>⚠️ 与运行期地址**尚无常量** —— 17 的 `IDataProvider` 消费方在 story-002/004 实现,
        /// 届时在此登记地址常量(承 <see cref="InteractionKindsCookedAssetName"/> 同款注记:
        /// 边界程序集不引用本编辑期装配,那侧为字面串)。</para>
        /// <para>⚠️ 本产物**今日无运行期消费方**(与 <see cref="BuildModulesCookedAssetName"/> 同态)。
        /// 落产物的意义 = AC-17-08b 的「构建期硬失败」在**生产烘焙路径**上真的被执行
        /// (承 `coding-standards.md` §「某件有门」:有校验代码 ≠ 校验在生产路径上执行)。
        /// **接线归 17 的装配轮**,不在本件。</para></summary>
        public const string ForagingConstantsCookedAssetName = "foraging_constants.cooked.bytes";

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
        /// 烘焙 interaction-system 的 `4-DC-1…6` 契约行集(NR-1 接线,story 007):
        /// 仓库 <c>assets/data/interaction_kinds.json</c> + `_dimensions.json` → 校验(**聚合 throw**)→
        /// 产物写入 Assets/DataCooked/。
        /// <para>⚠️ 本菜单是 <see cref="InteractionKindTableValidator"/> 的**唯一调用点** ——
        /// 删掉它就退回 story-006 评审 F-1 的「死代码」态,而 AC-4-15 的
        /// 「WHEN 阶段 2 烘焙 THEN throw」正是由本菜单兑现。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 interaction_kinds(4-DC)")]
        public static void BakeInteractionKinds()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                InteractionKindBaker.BakeOutput result = InteractionKindBaker.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, InteractionKindsCookedAssetName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{InteractionKindsCookedAssetName} = {result.Cooked.Length} B, " +
                    $"{result.Rows.Count} 行行集,ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 diagnosis-system 的 R-8.1 词条表(story 002):
        /// 仓库 <c>assets/data/diagnosis_signs.json</c> → 校验(**聚合 throw**)→
        /// 产物写入 Assets/DataCooked/。
        /// <para>⚠️ 本菜单是 <see cref="DiagnosisSignTableValidator"/> 的**唯一调用点链路**
        /// (菜单 → BakeFromRepo → DiagnosisSignBinder.Bind → Validate)——
        /// AC-8-32/33/34 的「构建期硬失败」由此兑现。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 diagnosis_signs(8 词条表)")]
        public static void BakeDiagnosisSigns()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                DiagnosisSignBaker.BakeOutput result = DiagnosisSignBaker.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, DiagnosisSignsCookedAssetName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{DiagnosisSignsCookedAssetName} = {result.Cooked.Length} B, " +
                    $"{result.Rows.Count} 行行集,ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 diagnosis-system 的 F-8.1 门槛定表(story 003):
        /// 仓库 <c>assets/data/diagnosis_read_floor.json</c> → 校验(**聚合 throw**)→
        /// 生成期定点求值 → 产物写入 Assets/DataCooked/。
        /// <para>⚠️ 本菜单是 <see cref="DiagnosisReadFloorBinder"/> 的**唯一调用点链路**
        /// (菜单 → BakeFromRepo → Bind → 定表生成)—— C-1 / C-5 的「构建期硬失败」由此兑现;
        /// 运行期 8 只见 float 定表值,零幂运算(G-1)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 diagnosis_read_floor(8 门槛定表)")]
        public static void BakeDiagnosisReadFloor()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                DiagnosisReadFloorBaker.BakeOutput result = DiagnosisReadFloorBaker.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, DiagnosisReadFloorCookedAssetName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{DiagnosisReadFloorCookedAssetName} = {result.Cooked.Length} B, " +
                    $"{result.Table.Count} 档定表,ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 diagnosis-system 的 F-8.3 阴性把握度定表(story 004):
        /// 仓库 <c>assets/data/diagnosis_negative_confidence.json</c> → 校验(**聚合 throw**)→
        /// 生成期定点求值 → 产物写入 Assets/DataCooked/。
        /// <para>⚠️ 本菜单是 <see cref="DiagnosisNegativeConfidenceBinder"/> 的**唯一调用点链路**
        /// (菜单 → BakeFromRepo → Bind → 定表生成)—— 值域 / C-5 / C-6 的「构建期硬失败」由此兑现;
        /// 运行期 8 只见 float 定表值,零幂运算(G-1)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 diagnosis_negative_confidence(8 阴性把握度定表)")]
        public static void BakeDiagnosisNegativeConfidence()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                DiagnosisNegativeConfidenceBaker.BakeOutput result =
                    DiagnosisNegativeConfidenceBaker.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, DiagnosisNegativeConfidenceCookedAssetName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{DiagnosisNegativeConfidenceCookedAssetName} = " +
                    $"{result.Cooked.Length} B, {result.Table.Count} 档定表, " +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙病名册词表(sprint-08 S1.0):`assets/data/case_judgment_lexicon.json` →
        /// 校验(**聚合 throw**,含 AC-37-35 词表侧双射候选拒)→ 产物写入 Assets/DataCooked/。
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 case_judgment_lexicon(37 病名册词表)")]
        public static void BakeJudgmentLexicon()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                JudgmentLexiconBaker.BakeOutput result =
                    JudgmentLexiconBaker.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, JudgmentLexiconCookedAssetName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{JudgmentLexiconCookedAssetName} = " +
                    $"{result.Cooked.Length} B, {result.Rows.Count} 行, " +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw;
            }
        }

        /// <summary>
        /// 烘焙 prescription-medication 的处方表 + 本草词表(story 001):
        /// 仓库 <c>assets/data/prescription_actions.json</c> + <c>materia_lexicon.json</c> →
        /// 校验(**聚合 throw**)→ 产物写入 Assets/DataCooked/。
        /// <para>⚠️ 本菜单是 <see cref="PrescriptionActionsBinder"/> 的**唯一调用点链路**
        /// (菜单 → BakeFromRepo → Bind → 校验)—— DC-1/DC-3/DC-5/DC-7 的「构建期硬失败」由此兑现。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 prescription_actions(11 处方表)")]
        public static void BakePrescriptionActions()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                PrescriptionActionsBaker.BakeOutput result = PrescriptionActionsBaker.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, PrescriptionActionsCookedAssetName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{PrescriptionActionsCookedAssetName} = {result.Cooked.Length} B, " +
                    $"{result.Rows.Count} 行行集,ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");

                // ⚠️ **真源期诚实性机制的生产出口**(2026-10-07 评审缺陷 1):
                //    此前 `ShadowWarnings` / `ShadowRegistryUsed` 在**非测试代码中零消费者**
                //    ⇒ 「判据非真判」这件事在生产路径上完全不可见,与
                //    `PrescriptionActionIdRegistry.cs` 自陈的「供构建日志显式报出」直接矛盾。
                //    ⇒ 菜单是 `BakeFromRepo` 的唯一生产调用点,此处**必须**报出。
                //    真源落地后本段与 `RealRegistryUsed` 一并撤除(禁借绿)。
                if (result.RealRegistryUsed)
                {
                    Debug.LogWarning(
                        "[大医精诚] ⚠️ 本次烘焙的 **DC-2 / DC-6 用的是真源** —— " +
                        "处置 id master 已登记(9 侧 `disease_action_axis.json` 存在)· " +
                        "`NOISE_BAND_9` 归 9 已立(BL-2 已闭)" +
                        "⇒ 两条判据本体 **RUN**,可据本次烘焙结果判「11 的表已合规」。");
                }
                foreach (string w in result.RealWarnings)
                    Debug.LogWarning("[大医精诚] 真源期诊断:" + w);
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 disease-simulation 的处置轴 + treatable_by 关系(story 007):
        /// 仓库 <c>assets/data/disease_action_axis.json</c> → 校验(**聚合 throw**)→
        /// 产物写入 Assets/DataCooked/。
        /// <para>⚠️ 本菜单是 <see cref="DiseaseActionAxisValidator"/> 的**唯一调用点链路**
        /// (菜单 → BakeFromRepo → Bind → Validate)—— 9-DC-1…7 的「构建期硬失败」由此兑现。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 disease_action_axis(9 处置轴)")]
        public static void BakeDiseaseActionAxis()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                DiseaseActionAxisBaker.BakeOutput result = DiseaseActionAxisBaker.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, DiseaseActionAxisCookedAssetName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{DiseaseActionAxisCookedAssetName} = {result.Cooked.Length} B, " +
                    $"{result.Actions.Count} 处置 + {result.TreatableBy.Count} 关系, " +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 53 判定表(story-002 · S12-9):仓库 <c>assets/data/53_outcome_table.json</c> →
        /// 校验(**聚合 throw**:覆盖 / 闭集 / 零浮点 / 极性列零 四条)→ 产物写入 Assets/DataCooked/。
        /// <para>⚠️ 本菜单是 <see cref="OutcomeTableBinder"/> 的**唯一调用点链路**
        /// (菜单 → BakeFromRepo → Bind → 四条校验)—— 卡面 Note 2 的「构建期硬失败」由此兑现。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 53_outcome_table(53 判定表)")]
        public static void BakeOutcomeTable()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                OutcomeTable53.BakeOutput result = OutcomeTable53.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, OutcomeTableCookedAssetName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{OutcomeTableCookedAssetName} = {result.Cooked.Length} B, " +
                    $"{result.Bound.Rows.Count} 行, U 档数 = {result.Bound.UBandCount}, " +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 53 延迟表(story-003 · S13-2):仓库 <c>assets/data/53_delay_table.json</c> →
        /// 校验(**聚合 throw**:定义域白名单单机制 / ≤0 拒收 / 缺行拒收 / 重复行拒收)→ 产物写入 Assets/DataCooked/。
        /// <para>⚠️ 本菜单是 <see cref="DelayTableBinder"/> 的**唯一调用点链路**
        /// (菜单 → BakeFromRepo → Bind → 三条构建期拒收)—— 卡面 AC-2 的「构建期硬失败」由此兑现
        /// (与 BakeOutcomeTable 同型先例;绕过本链路 = binder 永不执行 = 校验形同虚设)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 53_delay_table(53 延迟表)")]
        public static void BakeDelayTable()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                DelayTable53Bake.BakeOutput result = DelayTable53Bake.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, DelayTableCookedAssetName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{DelayTableCookedAssetName} = {result.Cooked.Length} B, " +
                    $"{result.Bound.Rows.Count} 行(定义域 = 回响轴三值), " +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 48 教学内容(tutorial-onboarding · S17-3 story-001 的 **AC-5 接线**):
        /// 仓库 <c>assets/data/48_tutorial_content.json</c> → 六项校验(**聚合 throw**)
        /// → deterministic 编码 → 产物写入 Assets/DataCooked/(入 data-core 组)。
        /// <para>⚠️ 本菜单是 <see cref="TutorialContentBinder"/> 的**唯一生产调用点链路**
        /// (菜单 → <c>BakeFromRepo</c> → Bind(六项校验)→ <c>TutorialContentCookedWriter.Write</c>)。
        /// 绕过它 = binder 永不执行 = 六项校验形同虚设(与 BakeDelayTable 同型先例)。</para>
        /// <para>⚠️ 运行期装载面在本菜单**之外**:<c>AddressablesDataProvider</c> 的
        /// <c>TutorialContentAddress</c> 分支 + <c>Preload()</c>(E-13 启动期硬失败,不降级)。</para>
        /// <para>⚠️ 校验 ④ 的扫描字典 = <see cref="TutorialDiseaseNameSource"/>(读
        /// <c>assets/data/disease_names.json</c>)。该件**缺席**时 ④ 记 NOT-RUN 并在日志里点名
        /// (不阻断烘焙);**存在但损坏 / 为空**时抛 <see cref="BakeValidationException"/> ⇒ 本菜单硬失败。
        /// 此处**显式注入**(而非依赖 <c>BakeFromRepo</c> 的默认值):菜单是唯一生产链路,注入点写死更易审计。</para>
        /// <para>⚠️ 校验 ② 的外部半边 = <see cref="TutorialMediaKeySource"/>(读 <c>audio_events.json</c> 的
        /// <c>rows[].cue</c> 与 <c>48_demonstration_clips.json</c> 的 <c>clips[].clipKey</c>)。
        /// 缺席语义同 ④;两个半边各自独立(一个缺席不把另一个降级)。2026-10-21 C-1 起本菜单显式注入。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 48_tutorial_content(48 教学内容)")]
        public static void BakeTutorialContent()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                TutorialContentBake.BakeOutput result = TutorialContentBake.BakeFromRepo(
                    repoRoot, new TutorialDiseaseNameSource(repoRoot),
                    new TutorialMediaKeySource(repoRoot));

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, TutorialContentCookedAssetName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{TutorialContentCookedAssetName} = {result.Cooked.Length} B, " +
                    $"{result.Bound.Steps.Count} 步 / {result.Bound.Narration.Count} 口述 / " +
                    $"{result.Bound.Demonstrations.Count} 示范 / {result.Bound.Papers.Count} 纸, " +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}, " +
                    $"校验④ = {DescribeDiseaseScan(result.Bound.DiseaseScan)}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>把校验 ④ 的三态翻成日志串 —— **NOT-RUN 必须肉眼可见**(否则「烘焙成功」会被误读成「四项全过」)。</summary>
        private static string DescribeDiseaseScan(TutorialTextScanResult scan)
        {
            switch (scan.Outcome)
            {
                case TutorialDiseaseScanOutcome.Clean:
                    return "执行 · 零命中";
                case TutorialDiseaseScanOutcome.Hits:
                    return $"执行 · {scan.HitCount} 处命中(硬失败,见上)";
                default:
                    return "NOT-RUN —— 词表缺席(assets/data/disease_names.json 不存在),本项未执行";
            }
        }

        /// <summary>
        /// 烘焙 21a 家具目录(<c>FURN_TABLE</c> —— `O-24-1` / `TD-clinic-01` 的兑现件):
        /// 仓库 <c>assets/data/item_database_furniture.json</c>
        /// → <see cref="ItemDatabaseFurnitureBinder"/>(三条域门:**聚合 throw**)
        /// → deterministic 编码 → <see cref="FurnitureCatalogueCookedAssetName"/> 写入 Assets/DataCooked/
        /// (入 data-core 组)。
        /// <para>⚠️ 本菜单是 <see cref="ItemDatabaseFurnitureBake"/> 的**唯一生产调用点链路**。
        /// 绕过它 = 三条域门永不执行(与 BakeItemDatabase / BakeClinicMachineTables 同型先例)。</para>
        /// <para>⚠️ **本表是 `FURN_TYPES` 闭集的唯一作者态**。24 的 <c>ROOM_TABLE</c> 成员资格门
        /// (`AC-24-12` 的另半边)读的是**本次**烘焙结果,不是上一次落盘的产物 ⇒
        /// 「先改家具目录、忘了重烘」会在 24 的烘焙期表现为闭集不匹配(而不是静默沿用旧闭集)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 item-database 家具目录(21a · FURN_TABLE)")]
        public static void BakeFurnitureCatalogue()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                ItemDatabaseFurnitureBake.BakeOutput result =
                    ItemDatabaseFurnitureBake.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                File.WriteAllBytes(Path.Combine(cookedDir, FurnitureCatalogueCookedAssetName), result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{FurnitureCatalogueCookedAssetName} = {result.Cooked.Length} B," +
                    $"家具类型 {result.Bound.Rows.Count} 件(FURN_TYPES 闭集)," +
                    $"tier_max = {result.Bound.TierMax}," +
                    $"e_env 界 = ±{ItemDatabaseFurnitureBinder.EnvContributionMax.Raw} raw(C_max = 1/8)," +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// <c>BakeClinicMachineTables</c> 的**计算段**(不落盘、不碰 Addressables、不打印日志)。
        /// <para>⚠️ **为什么要有这一层**:菜单方法带 <see cref="MenuItemAttribute"/>,在 batch 模式下
        /// **不会被调用**(只有 `-executeMethod` 点名才跑)⇒ 若把「先烘 21a 取闭集 → 注入 24」这段
        /// 直接写在菜单体里,测试就只能**同构复刻**一遍再断言 —— 而复刻品与菜单本体一旦漂移,
        /// 断言打的就不是生产链路(C-1 轮「栈帧 ≠ 接线」的同型风险)。抽到本函数后,
        /// **菜单与测试调的是同一份代码**:菜单失败 ⇒ 本函数失败 ⇒ 测红。</para>
        /// <para>⚠️ 本函数**只做计算**;落盘 / <c>AssetDatabase.Refresh</c> / 日志仍归菜单 ——
        /// 测试因此天然不写盘(承 <c>.claude/rules/test-standards.md</c>)。</para>
        /// </summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/</c> 的目录)。</param>
        /// <param name="furn">21a 家具目录的烘焙结果(闭集的来源)。</param>
        /// <exception cref="BakeValidationException">任一域门 / 成员资格门失败(聚合)。</exception>
        public static ClinicMachineTablesBake.BakeOutput BakeClinicMachineTablesCore(
            string repoRoot, out ItemDatabaseFurnitureBake.BakeOutput furn)
        {
            // O-24-1 已兑现:先烘 21a 家具目录取得 FURN_TYPES 闭集,再注入 24 的 ROOM_TABLE 成员资格门。
            // ⚠️ 顺序不可颠倒 —— 闭集来自**本次**烘焙的家具目录,不是上一次落盘的产物(防「改了目录却没重烘」)。
            furn = ItemDatabaseFurnitureBake.BakeFromRepo(repoRoot);

            return ClinicMachineTablesBake.BakeFromRepo(
                repoRoot, ItemDatabaseFurnitureBake.AsFurnTypeSource(furn.Bound));
        }

        /// <summary>
        /// <c>BakeBuildModules</c> 的**计算段**(不落盘、不碰 Addressables、不打印日志)。
        /// <para>⚠️ 与 <see cref="BakeClinicMachineTablesCore"/> 同款分层理由:菜单带
        /// <see cref="MenuItemAttribute"/>,batch 下不触发 ⇒ 计算段抽出来,菜单与测试调**同一份代码**。</para>
        /// <para>⚠️ **跨表一致性门(本函数的核心判据,不是附属检查)**:`build_modules.json` 的每个
        /// 非 null `furn_type` 必须 ∈ `item_database_furniture.json` 的 `FURN_TYPES` 闭集。
        /// 这是 `clinic-machine.md:297` / `item-database.md:859` 那句「**与 `cost(m)` 同源**」的
        /// **可执行形态** —— 没有这道门,「同源」就只是两句散文,两表各写各的照样全绿。
        /// **反向不查**:家具目录里允许有不在模块目录里的类型(家具目录是 24 的词汇表,
        /// 模块目录是 23 的件表,后者可以是前者的真子集)。</para>
        /// </summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/</c> 的目录)。</param>
        /// <param name="furn">21a 家具目录的烘焙结果(闭集的来源)。</param>
        /// <exception cref="BakeValidationException">词法 / 四条域门 / **跨表同源门**失败(聚合)。</exception>
        public static BuildModulesBake.BakeOutput BakeBuildModulesCore(
            string repoRoot, out ItemDatabaseFurnitureBake.BakeOutput furn)
        {
            // 闭集来自**本次**烘焙(与 24 同款理由:不用上一次落盘的产物)。
            furn = ItemDatabaseFurnitureBake.BakeFromRepo(repoRoot);

            BuildModulesBake.BakeOutput result = BuildModulesBake.BakeFromRepo(repoRoot);

            // 跨表同源门 —— 逐 `furn_type` 对照本次烘出的闭集。
            var known = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < furn.Bound.Rows.Count; i++) known.Add(furn.Bound.Rows[i].Type);

            var errors = new List<string>();
            for (int i = 0; i < result.Bound.Rows.Count; i++)
            {
                BuildModulesBinder.ModuleRow row = result.Bound.Rows[i];
                if (row.FurnType == null) continue;
                if (!known.Contains(row.FurnType))
                {
                    errors.Add(
                        $"{BuildModulesBinder.FileName}:modules[{i}](module_id={row.ModuleId}) " +
                        $"furn_type=\"{row.FurnType}\" 不在 FURN_TYPES 闭集内 —— " +
                        "两表漂移(clinic-machine.md:297 / item-database.md:859 要求「与 cost(m) 同源」);" +
                        "要么在家具目录里补该类型,要么把本行的 furn_type 置 null(若它真不是家具)");
                }
            }

            // ── 跨表外键门(2026-10-26,TD-inventory-01 前置)──
            // build_item_key 须指向物品表里一条**真实存在**且 category = build_part 的记录。
            // 与上面 furn_type 门同一位置、同一理由:跨表判据只有在能同时看见两张表的调用点才成立。
            // 物品集同样取**本次**烘焙结果(不读上次落盘产物)。
            BakeResult items = ItemDatabaseBaker.BakeFromRepo(repoRoot);
            ItemDataSet itemSet = CookedCodec.ReadItemDataSet(items.ItemsCooked);

            // ordinal → category(物品表本轮已过 ordinal 表自洽门 ⇒ ordinal 唯一)
            var categoryByOrdinal = new Dictionary<int, ItemCategory>();
            for (int i = 0; i < itemSet.Items.Length; i++)
                categoryByOrdinal[itemSet.Items[i].Ordinal] = itemSet.Items[i].Category;

            for (int i = 0; i < result.Bound.Rows.Count; i++)
            {
                BuildModulesBinder.ModuleRow row = result.Bound.Rows[i];
                if (!categoryByOrdinal.TryGetValue(row.BuildItemKey, out ItemCategory cat))
                {
                    errors.Add(
                        $"{BuildModulesBinder.FileName}:modules[{i}](module_id={row.ModuleId}) " +
                        $"build_item_key={row.BuildItemKey} 在物品表里**不存在** —— " +
                        "建造扣料会指向一个不存在的物品(20 的 fold 找不到该 instance 从而静默少扣)。" +
                        "要么修正该 ordinal,要么在 item_database_items.json 里补该条目");
                }
                else if (cat != ItemCategory.BuildPart)
                {
                    errors.Add(
                        $"{BuildModulesBinder.FileName}:modules[{i}](module_id={row.ModuleId}) " +
                        $"build_item_key={row.BuildItemKey} 指向的物品 category = {cat}," +
                        "要求 build_part(modular-building.md:250「模块目录挂在 category = build_part 的物品表」)");
                }
            }

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return result;
        }

        /// <summary>
        /// 烘焙 23 模块目录(<c>build_modules.json</c> —— <c>TD-itemdb-01</c> 的兑现件):
        /// 仓库 <c>assets/data/build_modules.json</c>
        /// → <see cref="BuildModulesBinder"/>(四条域门:**聚合 throw**)
        /// → **跨表同源门**(`furn_type ⊆ FURN_TYPES`)
        /// → deterministic 编码 → <see cref="BuildModulesCookedAssetName"/> 写入 Assets/DataCooked/。
        /// <para>⚠️ 本菜单是 <see cref="BuildModulesBake"/> 的**唯一生产调用点链路**。
        /// 绕过它 = 四条域门与跨表门永不执行(与 BakeFurnitureCatalogue / BakeClinicMachineTables 同型先例)。</para>
        /// <para>⚠️ **本表今日无运行期消费方** —— `IModuleCatalog` 的生产实现为零。
        /// 本菜单的判据是「作者态表存在且过门」,**不是**「23 能建造了」。
        /// 接线(装载 + `IModuleCatalog` 实现 + `ModuleDefinition.Cost`)归 23 的装配轮。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 build_modules(23 模块目录 · 21a)")]
        public static void BakeBuildModules()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

                ItemDatabaseFurnitureBake.BakeOutput furn;
                BuildModulesBake.BakeOutput result = BakeBuildModulesCore(repoRoot, out furn);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                File.WriteAllBytes(
                    Path.Combine(cookedDir, BuildModulesCookedAssetName), result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{BuildModulesCookedAssetName} = {result.Cooked.Length} B," +
                    $"模块 {result.Bound.Rows.Count} 件," +
                    $"furn_type 去重 {BuildModulesBake.DeclaredFurnTypes(result.Bound).Count} 个" +
                    $"(全部 ⊆ FURN_TYPES 闭集 {furn.Bound.Rows.Count} 类型 ✓)," +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙建造槽位骨架(<c>world_buildslots.json</c> —— ADR-022 §三导出契约):
        /// 仓库 <c>assets/data/world_buildslots.json</c>(**由关卡工具导出,不得手写**)
        /// → <see cref="WorldBuildSlotsBinder"/>(五条域门:**聚合 throw**)
        /// → deterministic 编码 → <see cref="WorldBuildSlotsCookedAssetName"/> 写入 Assets/DataCooked/。
        /// <para>⚠️ 本方法是 <see cref="WorldBuildSlotsBake"/> 的**唯一生产调用点链路**
        /// (菜单 → Bake → Binder → CookedWriter)。绕过它 = 五条域门永不执行。</para>
        /// <para>⚠️ 运行期消费方 = <c>MinimalWorldFactory.Create()</c>(经
        /// <c>DataCookedDisk.LoadBuildSlots()</c> 读**同一份**字节)。生产 build 的
        /// Addressables 装载面**尚未接**(见交付报告「没做的事」)。</para>
        /// <para>⚠️ <b>2026-10-28 订正(原文不删,留作闭环记录)</b>:上一句的「尚未接」**已过期** ——
        /// S20-20 装载面修复轮接上了 `AddressablesDataProvider.EnsureWorldBuildSlots()`
        /// (进 `Preload()` + `DataCorePreloader.DataCoreAddresses`),`MinimalWorldFactory.Create(provider)`
        /// **provider 优先**、磁盘只在 `null` 时兜底。⇒ 本菜单烘完**必须**再跑
        /// 「确保 data-core Addressables 组」**和**「构建 Addressables 内容」两个菜单,
        /// 否则玩家构建的 `Preload()` 查不到该 address(E-13 启动期硬失败)。</para>
        /// </summary>
        public static WorldBuildSlotsBake.BakeOutput BakeWorldBuildSlotsCore(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            return WorldBuildSlotsBake.BakeFromRepo(repoRoot);
        }

        /// <summary>烘焙建造槽位骨架并落盘(菜单入口)。</summary>
        [MenuItem("大医精诚/数据管线/烘焙 world_buildslots(建造槽位骨架 · ADR-022)")]
        public static void BakeWorldBuildSlots()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

                WorldBuildSlotsBake.BakeOutput result = BakeWorldBuildSlotsCore(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                File.WriteAllBytes(
                    Path.Combine(cookedDir, WorldBuildSlotsCookedAssetName), result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{WorldBuildSlotsCookedAssetName} = {result.Cooked.Length} B," +
                    $"槽位 {result.Bound.Rows.Count} 格," +
                    $"声明范围 {result.Bound.Width}×{result.Bound.Height}×{result.Bound.Depth}," +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙世界几何(<c>world_geometry.json</c> —— 关卡工具 story-002):
        /// 仓库 <c>assets/data/world_geometry.json</c>(**由关卡工具导出,不得手写**)
        /// → <see cref="WorldGeometryBinder"/>(四条域门:**聚合 throw**)
        /// → deterministic 编码 → <see cref="WorldGeometryCookedAssetName"/> 写入 Assets/DataCooked/。
        /// <para>⚠️ 运行期消费方 = <c>MinimalWorldFactory.Create()</c>,且**无降级路径** ——
        /// 产物缺失 / 损坏 = 启动期硬失败。⇒ 烘完**必须**再跑「确保 data-core Addressables 组」
        /// 与「构建 Addressables 内容」两个菜单。</para>
        /// </summary>
        public static WorldGeometryBake.BakeOutput BakeWorldGeometryCore(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            return WorldGeometryBake.BakeFromRepo(repoRoot);
        }

        /// <summary>烘焙世界几何并落盘(菜单入口)。</summary>
        [MenuItem("大医精诚/数据管线/烘焙 world_geometry(世界几何 · 关卡工具)")]
        public static void BakeWorldGeometry()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                WorldGeometryBake.BakeOutput result = BakeWorldGeometryCore(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                File.WriteAllBytes(
                    Path.Combine(cookedDir, WorldGeometryCookedAssetName), result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{WorldGeometryCookedAssetName} = {result.Cooked.Length} B," +
                    $"声明范围 {result.Bound.Width}×{result.Bound.Height}×{result.Bound.Depth}," +
                    $"LATTICE_SIZE = {result.Bound.LatticeSizeMm} mm," +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙地形表(<c>world_terrain.json</c> —— 关卡工具 story-002):
        /// 仓库 <c>assets/data/world_terrain.json</c>(**由关卡工具导出,不得手写**)
        /// → <see cref="WorldTerrainBinder"/>(六条域门:**聚合 throw**)
        /// → deterministic 编码 → <see cref="WorldTerrainCookedAssetName"/> 写入 Assets/DataCooked/。
        /// <para>⚠️ 与 <see cref="BakeWorldGeometryCore"/> **必须同批** —— 两份产物的
        /// <c>world_size</c> 在装载期互相断言。</para>
        /// </summary>
        public static WorldTerrainBake.BakeOutput BakeWorldTerrainCore(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            return WorldTerrainBake.BakeFromRepo(repoRoot);
        }

        /// <summary>烘焙地形表并落盘(菜单入口)。</summary>
        [MenuItem("大医精诚/数据管线/烘焙 world_terrain(地形表 · 关卡工具)")]
        public static void BakeWorldTerrain()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                WorldTerrainBake.BakeOutput result = BakeWorldTerrainCore(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                File.WriteAllBytes(
                    Path.Combine(cookedDir, WorldTerrainCookedAssetName), result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{WorldTerrainCookedAssetName} = {result.Cooked.Length} B," +
                    $"TERRAIN_TABLE {result.Bound.Rows.Count} 行," +
                    $"声明范围 {result.Bound.Width}×{result.Bound.Height}×{result.Bound.Depth}," +
                    $"slopeLimit = {result.Bound.SlopeLimitDegX100}(0.01°)," +
                    $"stepOffset = {result.Bound.StepOffsetMm} mm," +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// **P0 出货的导航格切片产物名**(4 份;story-005 · 用户裁定 chunk = 8×1×8 ⇒ 2×1×2)。
        /// <para>⚠️ 为什么**写死四个字面量**而不是全部由 `WorldNavChunkBake.CookedFileNameFor(i)` 拼:</para>
        /// <para>① 盘上**到底有哪几份产物**是一个**出货事实**,不是运行期推导 —— 它与「怎么切」是两件事,
        /// 混成一个表达式会让「改了 chunk 尺寸」悄悄改变产物集合,而组条目 / 地址 / 漂移门都不会跟着动。</para>
        /// <para>② 漂移门 `test_sweep_declaredArtifactNames_allTriaged` 按**字面量**
        /// （`"&lt;名&gt;.cooked.bytes"`)扫源码枚举「声明的产物名」。拼接式名字在那条正则下**不存在**
        /// ⇒ 首跑实测「声明 31 ≠ 钉定 35」。**修的是名字的形态,不是判据**。</para>
        /// <para>③ `BakeWorldNavChunksCore` 会断言「几何产物推出的 chunk 数 == 本表长度」,
        /// 所以本表变旧时**先红在烘焙**而不是先红在打包。</para>
        /// </summary>
        public static readonly string[] WorldNavChunkCookedAssetNames =
        {
            "world_nav_0.cooked.bytes",
            "world_nav_1.cooked.bytes",
            "world_nav_2.cooked.bytes",
            "world_nav_3.cooked.bytes",
        };

        /// <summary>
        /// 烘焙**全部导航格切片**(<c>world_nav_{chunk}.json</c> —— 关卡工具 story-005)。
        /// <para>chunk 数**不写死**:从 <c>world_geometry.json</c> 的 `world_size` + `chunk_size`
        /// 经 <see cref="DaYiJingCheng.Sim.World.ChunkTopology.ForWorld"/> 推出 —— 与切片器、
        /// 运行期 codec **同一个函数**(story-005 AC-2②)。写死一个「4」会让「换关卡 / 换 chunk 尺寸」
        /// 之后少烘几片或多烘几片,而症状是运行期「那几格永远查不到」(静默)。</para>
        /// <para>⚠️ 依赖 <c>world_geometry.json</c> 先导出 —— 它是 chunk 划分的唯一声明处。</para>
        /// </summary>
        public static IReadOnlyList<WorldNavChunkBake.BakeOutput> BakeWorldNavChunksCore(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string geometryPath = Path.Combine(repoRoot, "assets", "data", WorldGeometryBinder.FileName);
            if (!File.Exists(geometryPath))
                throw new BakeValidationException(new List<string>
                {
                    $"作者态源不存在:{geometryPath} —— 导航格切片的 chunk 数由几何产物的 " +
                    "world_size + chunk_size 决定,故须先导出 / 存在于该路径",
                });

            WorldGeometryBinder.BindResult geometry =
                WorldGeometryBinder.Bind(File.ReadAllText(geometryPath, System.Text.Encoding.UTF8));
            DaYiJingCheng.Sim.World.ChunkTopology topo =
                DaYiJingCheng.Sim.World.ChunkTopology.ForWorld(
                    geometry.Width, geometry.Height, geometry.Depth,
                    geometry.ChunkSizeX, geometry.ChunkSizeY, geometry.ChunkSizeZ);

            // 出货产物集合必须与几何推出的划分**一致** —— 不一致时在这里先红。
            if (topo.ChunkCount != WorldNavChunkCookedAssetNames.Length)
                throw new BakeValidationException(new List<string>
                {
                    $"几何产物推出 {topo.ChunkCount} 个 chunk,而 P0 登记的导航格产物有 " +
                    $"{WorldNavChunkCookedAssetNames.Length} 份 —— 两组不一致。\n" +
                    "改了 chunk 尺寸就必须同批:① 本表(WorldNavChunkCookedAssetNames)" +
                    "② data-core 组 ③ 漂移门的 BakedArtifacts / pinnedDeclared。",
                });

            var outputs = new List<WorldNavChunkBake.BakeOutput>(topo.ChunkCount);
            for (int i = 0; i < topo.ChunkCount; i++)
                outputs.Add(WorldNavChunkBake.BakeFromRepo(repoRoot, i));
            return outputs;
        }

        /// <summary>烘焙全部导航格切片并落盘(菜单入口)。</summary>
        [MenuItem("大医精诚/数据管线/烘焙 world_nav(导航格切片 · 关卡工具)")]
        public static void BakeWorldNavChunks()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                IReadOnlyList<WorldNavChunkBake.BakeOutput> results = BakeWorldNavChunksCore(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                var sb = new System.Text.StringBuilder();
                sb.Append("[大医精诚] 烘焙完成:world_nav 共 ").Append(results.Count).Append(" 片");
                for (int i = 0; i < results.Count; i++)
                {
                    // 名字取自登记表(而不是 `CookedFileNameFor` 现场拼)——
                    // 让「盘上写了哪几份」与「登记了哪几份」出自同一处。
                    string name = WorldNavChunkCookedAssetNames[results[i].Bound.ChunkIndex];
                    File.WriteAllBytes(Path.Combine(cookedDir, name), results[i].Cooked);
                    sb.Append("\n  ").Append(name).Append(" = ").Append(results[i].Cooked.Length).Append(" B, ")
                      .Append("chunk = ").Append(results[i].Bound.ChunkIndex).Append(", ")
                      .Append("ConfigVersion = 0x").Append(results[i].ConfigVersion.ToString("X8"));
                }
                AssetDatabase.Refresh();
                sb.Append($"\n({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
                Debug.Log(sb.ToString());
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙生态区多边形(<c>world_ecozones.json</c> —— 关卡工具 story-003):
        /// 仓库 <c>assets/data/world_ecozones.json</c>(**由关卡工具导出,不得手写**)
        /// → <see cref="WorldEcozonesBinder"/>(结构门 + **C4 五项几何门**:聚合 throw)
        /// → deterministic 编码 → <see cref="WorldEcozonesCookedAssetName"/> 写入 Assets/DataCooked/。
        /// <para>⚠️ 运行期消费方 = `CompositionRoot.LoadEcozoneRegistry`,且**无降级路径**。</para>
        /// </summary>
        public static WorldEcozonesBake.BakeOutput BakeWorldEcozonesCore(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            return WorldEcozonesBake.BakeFromRepo(repoRoot);
        }

        /// <summary>烘焙生态区多边形并落盘(菜单入口)。</summary>
        [MenuItem("大医精诚/数据管线/烘焙 world_ecozones(生态区多边形 · 关卡工具)")]
        public static void BakeWorldEcozones()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                WorldEcozonesBake.BakeOutput result = BakeWorldEcozonesCore(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                File.WriteAllBytes(
                    Path.Combine(cookedDir, WorldEcozonesCookedAssetName), result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{WorldEcozonesCookedAssetName} = {result.Cooked.Length} B," +
                    $"生态区 {result.Bound.Zones.Count} 个," +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 POI **定义**(<c>world_poi.json</c> —— 关卡工具 story-004):
        /// 仓库 <c>assets/data/world_poi.json</c>(**由关卡工具导出,不得手写**)
        /// → <see cref="WorldPoiBinder"/>(结构门 + 越界门:聚合 throw)
        /// → deterministic 编码 → <see cref="WorldPoiCookedAssetName"/> 写入 Assets/DataCooked/。
        /// <para>⚠️ 运行期消费方 = `MinimalWorldFactory`(填 `PoiGrid`)+ `CompositionRoot`(喂
        /// `PoiStateMachine` 的初始 id 集),且**无降级路径**。</para>
        /// </summary>
        public static WorldPoiBake.BakeOutput BakeWorldPoiCore(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            return WorldPoiBake.BakeFromRepo(repoRoot);
        }

        /// <summary>烘焙 POI 定义并落盘(菜单入口)。</summary>
        [MenuItem("大医精诚/数据管线/烘焙 world_poi(POI 定义 · 关卡工具)")]
        public static void BakeWorldPoi()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                WorldPoiBake.BakeOutput result = BakeWorldPoiCore(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                File.WriteAllBytes(
                    Path.Combine(cookedDir, WorldPoiCookedAssetName), result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{WorldPoiCookedAssetName} = {result.Cooked.Length} B," +
                    $"POI {result.Bound.Pois.Count} 个," +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙资源点(<c>world_resources.json</c> —— 关卡工具 story-004):
        /// 仓库 <c>assets/data/world_resources.json</c>(**由关卡工具导出,不得手写**)
        /// → <see cref="WorldResourcesBinder"/>(结构门 + 越界门 + 同格门:聚合 throw)
        /// → deterministic 编码 → <see cref="WorldResourcesCookedAssetName"/> 写入 Assets/DataCooked/。
        /// <para>⚠️ 运行期消费方 = `MinimalWorldFactory`(填 `ResourceGrid`),**无降级路径**。</para>
        /// </summary>
        public static WorldResourcesBake.BakeOutput BakeWorldResourcesCore(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            return WorldResourcesBake.BakeFromRepo(repoRoot);
        }

        /// <summary>烘焙资源点并落盘(菜单入口)。</summary>
        [MenuItem("大医精诚/数据管线/烘焙 world_resources(资源点 · 关卡工具)")]
        public static void BakeWorldResources()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                WorldResourcesBake.BakeOutput result = BakeWorldResourcesCore(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                File.WriteAllBytes(
                    Path.Combine(cookedDir, WorldResourcesCookedAssetName), result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{WorldResourcesCookedAssetName} = {result.Cooked.Length} B," +
                    $"资源点 {result.Bound.Resources.Count} 个," +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 17 采集调参表(S20-3 评审 W-3 甲案):
        /// 仓库 <c>assets/data/foraging_constants.json</c> + 21a 常量
        /// → <see cref="ForagingConstantsBinder"/> 阶段 2 绑定(**聚合 throw**)
        /// → deterministic 编码 → <see cref="ForagingConstantsCookedAssetName"/> 写入 Assets/DataCooked/。
        /// <para>⚠️ 本菜单是 <see cref="ForagingConstantsBinder"/> 的**唯一生产调用点链路**
        /// (菜单 → <see cref="ForagingConstantsBaker.BakeFromRepo"/> → Binder → CookedWriter)。
        /// 绕过它 = Binder 的 8 条构建期校验永不执行 —— 这正是评审 W-3 指出的缺口:
        /// 补烘前 Binder 是 25 个 Binder 中**唯一零生产调用方**的
        /// (承 `coding-standards.md` §「某件有门」:有校验代码 ≠ 校验在生产路径上执行)。</para>
        /// <para>⚠️ 本 Baker **跨表读 21a 的 MAX_QUALITY**(D-21-7 双向耦合)—— 故 21a 三表
        /// 缺失或绑定失败时本菜单**同样硬失败**,报错前缀为「21a 三表绑定失败」。
        /// 这是刻意的:「17 的表 × 21a 的常量」这一对必须在**生产烘焙路径**上被联合校验。</para>
        /// <para>⚠️ **本产物今日无运行期消费方**(与 <see cref="BuildModulesCookedAssetName"/> 同态)——
        /// 17 的 `IDataProvider` 消费方在 story-002/004 实现。**补烘 ≠ 接线**。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 foraging_constants(17 采集调参表)")]
        public static void BakeForagingConstants()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

                ForagingConstantsBaker.BakeOutput result = ForagingConstantsBaker.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                File.WriteAllBytes(
                    Path.Combine(cookedDir, ForagingConstantsCookedAssetName), result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{ForagingConstantsCookedAssetName} = {result.Cooked.Length} B," +
                    $"CapTable {result.Bound.CapTable.Length} 档," +
                    $"SEASON_MULT {result.Bound.SeasonMult.Length} 项" +
                    $"(= seasons_per_year {result.Bound.SeasonsPerYear} ✓)," +
                    $"MAX_QUALITY = {result.MaxQuality}(读自 21a)," +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 24 医馆三表(clinic-machine · S17-2 的**前置** —— 求值器要吃的 cooked 表):
        /// 仓库 <c>assets/data/clinicmachine_{context,adj,room}.json</c> + 21a 常量
        /// → 三个 Binder(**聚合 throw**)→ 跨表断言(O-24-4 / base_env 域)→ deterministic 编码
        /// → 四份产物写入 Assets/DataCooked/(入 data-core 组;第四份 = 21a 家具目录,
        /// 它先被烘焙以取得闭集,故同批落盘 —— 见函数内注释)。
        /// <para>⚠️ 本菜单是 <see cref="ClinicMachineTablesBake"/> 的**唯一生产调用点链路**
        /// (菜单 → <see cref="BakeClinicMachineTablesCore"/> → 三个 Binder → ClinicMachineCookedWriter)。
        /// 绕过它 = 三个 Binder 永不执行(与 BakeTutorialContent / BakeDelayTable 同型先例)。</para>
        /// <para>~~⚠️ **生产路径刻意传 <c>null</c> 家具类型闭集来源** —— `O-24-1`(21a 家具目录扩展
        /// `type`/`tier`/`e_env`)尚未兑现(TD-clinic-01,sprint-18 的 G-8 解除)⇒
        /// `Room.FurnMembershipEnforced == false`。~~ **✅ 2026-10-21 已兑现**:经
        /// <see cref="BakeClinicMachineTablesCore"/> 先烘 21a 家具目录取闭集,再注入
        /// <see cref="ItemDatabaseFurnitureBake.AsFurnTypeSource"/> ⇒ `FurnMembershipEnforced == true`。
        /// 日志仍保留「已强制 / NOT-RUN」双态打印(防将来再次退化时静默)。</para>
        /// <para>⚠️ **闭集强制这件事由测试钉住,不靠本菜单的日志**:
        /// <c>clinic_furniture_catalogue_test.test_productionMenuPath_coreForcesFurnClosure</c> 直调
        /// <see cref="BakeClinicMachineTablesCore"/>(菜单与它**同一份代码**),断言
        /// <c>FurnMembershipEnforced == true</c> 且每个子句 type ∈ 闭集。
        /// 日志只是人眼可读的副产品 —— 它不构成判据。</para>
        /// <para>⚠️ 运行期装载面在本菜单**之外**:`AddressablesDataProvider` 的四条 Address +
        /// `Preload()`(E-13 启动期硬失败,不降级)。**补烘 ≠ 接线**,四处闭合由
        /// `provider_wiring_gate_test` 守。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 clinic-machine 三表(24 医馆)")]
        public static void BakeClinicMachineTables()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

                ItemDatabaseFurnitureBake.BakeOutput furn;
                ClinicMachineTablesBake.BakeOutput result =
                    BakeClinicMachineTablesCore(repoRoot, out furn);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                File.WriteAllBytes(Path.Combine(cookedDir, ClinicContextCookedAssetName), result.ContextCooked);
                File.WriteAllBytes(Path.Combine(cookedDir, ClinicAdjCookedAssetName), result.AdjCooked);
                File.WriteAllBytes(Path.Combine(cookedDir, ClinicRoomCookedAssetName), result.RoomCooked);
                File.WriteAllBytes(
                    Path.Combine(cookedDir, ClinicRoomNamesCookedAssetName), result.RoomNamesCooked);
                File.WriteAllBytes(
                    Path.Combine(cookedDir, ClinicContextNamesCookedAssetName), result.ContextNamesCooked);
                // ⚠️ **同一份烘焙结果也落盘**:闭集已经在这里烘出来了,若不落产物,就会出现
                // 「24 的烘焙期强制了闭集,而运行期 `AddressablesDataProvider` 装载不到它」的半烘态
                // (那是 E-13 启动期硬失败,而且本菜单的日志会显示「已强制」—— 假绿)。
                // 独立菜单 `BakeFurnitureCatalogue` 仍保留(21a 单独改目录时只想重烘它),两者字节同源。
                File.WriteAllBytes(
                    Path.Combine(cookedDir, FurnitureCatalogueCookedAssetName), furn.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{ClinicContextCookedAssetName} = {result.ContextCooked.Length} B, " +
                    $"{result.Context.Rows.Count} 情境行(K_CONTEXT_MAX raw = {result.Context.KContextMax.Raw});" +
                    $"{ClinicAdjCookedAssetName} = {result.AdjCooked.Length} B, " +
                    $"{result.Adj.Pairs.Count} 邻接对(n_max = {result.Adj.NMax});" +
                    $"{ClinicRoomCookedAssetName} = {result.RoomCooked.Length} B, " +
                    $"{result.Room.Rooms.Count} 房间类型;" +
                    $"{ClinicRoomNamesCookedAssetName} = {result.RoomNamesCooked.Length} B, " +
                    $"{result.RoomNames.Rows.Count} 房间名;" +
                    $"{ClinicContextNamesCookedAssetName} = {result.ContextNamesCooked.Length} B, " +
                    $"{result.ContextNames.Rows.Count} 情境名;" +
                    $"{FurnitureCatalogueCookedAssetName} = {furn.Cooked.Length} B, " +
                    $"{furn.Bound.Rows.Count} 家具类型; " +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}, " +
                    $"FURN_TYPES 闭集 = {(result.Room.FurnMembershipEnforced ? "已强制" : "NOT-RUN —— 闭集来源注入被摘掉")}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 44 音频事件表(阶段 1 词法 → 阶段 2 白名单/域门 → **D6 素材存在性门** → deterministic 编码)
        /// → 产物写入 Assets/DataCooked/(**入 data-core 组**,与素材组 `audio` 分家 —— AC-44-D5)。
        /// <para>⚠️ 本菜单是 <see cref="AudioEventTableBinder"/> 的**唯一调用点链路**
        /// (菜单 → BakeFromRepo → Bind → 三道门)。绕过本链路 = 门永不执行。</para>
        /// <para>⚠️ D6 的素材根 = <c>unity/Assets/Audio/</c>(<see cref="AudioAssetExistence.ResolveRoot"/>)
        /// —— 素材缺失即本菜单**硬失败**(不是警告)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 audio_events(44 事件表)")]
        public static void BakeAudioEvents()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                AudioEventTableBake.BakeOutput result = AudioEventTableBake.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, AudioEventTableBake.CookedFileName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{AudioEventTableBake.CookedFileName} = {result.Cooked.Length} B, " +
                    $"{result.Bound.Table.Rows.Count} 行(行序 = cue ordinal), " +
                    $"素材根 = {result.AssetRoot},ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单;" +
                    $"素材本身入「确保 audio Addressables 组」)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 44 设置暴露面注册表(阶段 1 词法 → 阶段 2 名字/值双向对账 + 域检查 → deterministic 编码)
        /// → 产物写入 Assets/DataCooked/(入 data-core 组)。
        /// <para>⚠️ 本菜单是 <see cref="AudioSettingsRegistryBinder"/> 的**唯一调用点链路**;
        /// 消费者 = `AudioSettingsRegistry.Provider`(由 `BootRoot` 注入)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 audio_settings_registry(44 设置注册表)")]
        public static void BakeAudioSettingsRegistry()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                AudioSettingsRegistryBake.BakeOutput result = AudioSettingsRegistryBake.BakeFromRepo(repoRoot);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, AudioSettingsRegistryBake.CookedFileName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{AudioSettingsRegistryBake.CookedFileName} = {result.Cooked.Length} B, " +
                    $"{result.Bound.Rows.Count} 行(名字/值双向对账过),ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙设备切换迟滞常量表(阶段 1 词法 → 阶段 2 两列绑定 + 区间断言 → deterministic 编码)
        /// → 产物写入 Assets/DataCooked/(入 data-core 组)。
        /// <para>⚠️ TD-epic-01 第十一片:此前六旋钮只有 <c>assets/data/input_device_switch.json</c>
        /// + <see cref="InputDeviceSwitchBinder"/>(构建期校验)+ <c>CompositionRoot.EmergencyDeviceSwitchSeed*</c>
        /// 六个手抄常量 —— **零 cooked 制品、零运行期装载路径**,JSON 与常量表无机械对账。
        /// 本菜单是 <see cref="InputDeviceSwitchBake"/> 的**唯一调用点链路**(菜单 → BakeFromRepo → Bind → 编码)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 input_device_switch(3 设备切换旋钮)")]
        public static void BakeInputDeviceSwitch()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                InputDeviceSwitchBake.BakeOutput result = InputDeviceSwitchBake.BakeFromRepo(repoRoot);

                // 真往返:编码 → 解码 → 跨侧逐项等值(布局分叉的唯一自动兜现)
                InputDeviceSwitchBake.Materialize(result);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, InputDeviceSwitchBake.CookedFileName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{InputDeviceSwitchBake.CookedFileName} = {result.Cooked.Length} B, " +
                    $"pointer(阈值 {result.Bound.Pointer.DeviceSwitchThreshold.Raw} / 容差 " +
                    $"{result.Bound.Pointer.DriftTolerance.Raw} / 驻留 {result.Bound.Pointer.DwellTicks})·" +
                    $"axis(阈值 {result.Bound.Axis.DeviceSwitchThreshold.Raw} / 容差 " +
                    $"{result.Bound.Axis.DriftTolerance.Raw} / 驻留 {result.Bound.Axis.DwellTicks})," +
                    $"ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙遥测参数旋钮表(阶段 1 词法 → 阶段 2 四组白名单 + schema + 范围断言 → deterministic 编码)
        /// → 产物写入 Assets/DataCooked/(入 data-core 组)。
        /// <para>⚠️ TD-epic-01 第十二片:GDD 声明的落地位置 <c>assets/data/telemetry_analytics.json</c>
        /// 此前**从未存在** ⇒ AC-51-B14 的「配置加载」半边无对象、AC-51-E5 只能靠「文件不存在」恒真通过。
        /// 本菜单是 <see cref="TelemetryAnalyticsBake"/> 的**唯一调用点链路**。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 telemetry_analytics(6 遥测旋钮)")]
        public static void BakeTelemetryAnalytics()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                TelemetryAnalyticsBake.BakeOutput result = TelemetryAnalyticsBake.BakeFromRepo(repoRoot);

                // 真往返:编码 → 解码(含加载期范围门)→ 跨侧逐项等值(布局分叉的唯一自动兜现)
                TelemetryAnalyticsBake.Materialize(result);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, TelemetryAnalyticsBake.CookedFileName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{TelemetryAnalyticsBake.CookedFileName} = {result.Cooked.Length} B, " +
                    $"W={result.Bound.DifficultyCurve.WindowTicks} S={result.Bound.DifficultyCurve.StepTicks} " +
                    $"t0={result.Bound.DifficultyCurve.FirstWindowTick} " +
                    $"GAP={result.Bound.GapThresholdTicks} GROWTH={result.Bound.GrowthWindowTicks} " +
                    $"MIN_SAMPLE={result.Bound.MinSample},ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 烘焙 9 病种注册表(阶段 1 词法 → 阶段 2 绑定 + R1 校验 → deterministic 编码)
        /// → 产物写入 Assets/DataCooked/(入 data-core 组)。
        /// <para>⚠️ **为什么要有这一项**:<c>disease_registry.cooked.bytes</c> 此前**从未产出过** ——
        /// 9 的注册表没有烘焙入口,生产路径只能拿 <c>CreateFallbackRegistry()</c> 的单条占位
        /// (`DiseaseId = 1`),而真表从 id 2 起。后果 = 全局 <c>W</c> / <c>TREND_EPS</c> 恒 0,
        /// F4 的痊愈支 / 迁延支在生产里**永不求值**。</para>
        /// <para>本菜单是 <see cref="DiseaseRegistryBaker"/> 的**唯一调用点链路**(batch 下
        /// <c>[MenuItem]</c> 不触发 ⇒ 计算段抽在 Baker 里,菜单与测试调同一份代码)。</para>
        /// <para>⚠️ **烘焙后必须**:① 经「确保 data-core Addressables 组」把条目纳入组
        /// ② 经「构建 Addressables 内容」重建 catalog,否则 PlayMode 下该 address 查不到。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/烘焙 disease_registry(9 病种注册表)")]
        public static void BakeDiseaseRegistry()
        {
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                DiseaseRegistryBaker.BakeOutput result = DiseaseRegistryBaker.BakeFromRepo(repoRoot);

                // 真往返:编码 → 解码(含加载期 R1-36 趋势窗口门)→ 跨侧逐项等值
                // (写方在 Editor.Tools.Bake、读方在 Gameplay.Presentation,两份手写布局,
                //  这里是唯一自动兜现)
                DiseaseRegistryBaker.Materialize(result);

                string cookedDir = Path.Combine(Application.dataPath, CookedDirName);
                Directory.CreateDirectory(cookedDir);
                string path = Path.Combine(cookedDir, DiseaseRegistryBaker.CookedFileName);
                File.WriteAllBytes(path, result.Cooked);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[大医精诚] 烘焙完成:{DiseaseRegistryBaker.CookedFileName} = {result.Cooked.Length} B, " +
                    $"行数 = {result.Rows.Count},W = {result.TrendWindowTicks}, " +
                    $"TREND_EPS = {result.TrendEpsRaw}(raw),ConfigVersion = 0x{result.ConfigVersion:X8}" +
                    (result.UnboundKeys.Count > 0
                        ? $";未绑定键 {result.UnboundKeys.Count} 个:{string.Join(", ", result.UnboundKeys)}"
                        : "") +
                    $"({DataCoreGroup} 组条目须经「确保 data-core Addressables 组」菜单)");
            }
            catch (BakeValidationException ex)
            {
                foreach (string e in ex.Errors)
                    Debug.LogError("[大医精诚] 烘焙失败:" + e);
                throw; // 硬失败(校验失败绝不降级为警告)
            }
        }

        /// <summary>
        /// 构建 Addressables 内容(<c>BuildPlayerContent</c>)—— **改过组条目之后必须跑**。
        /// <para>⚠️ <b>为什么需要这个菜单</b>(2026-10-17 实测):PlayMode 下 Addressables 解析的是
        /// <c>unity/Library/com.unity.addressables/aa/&lt;platform&gt;/catalog.bin</c> 这份**预建 catalog**。
        /// 新增条目(如 <c>audio_events.cooked.bytes</c>)后若不重建,该 address **查不到 Location** ——
        /// 实测症状 = `InvalidKeyException: No Location found for Key=…` ⇒ 启动序
        /// (<c>AddressablesDataProvider.Preload()</c>)整条挂掉,9 条 PlayMode 启动序测试全红。</para>
        /// <para>⚠️ 本菜单此前**不存在** —— 全库唯一调用 <c>BuildPlayerContent</c> 的是
        /// <c>U1SpikeSetup</c>(它顺带切 Play Mode 到 Existing Build,是另一个坑)。
        /// 本菜单只做构建,不动任何设置。</para>
        /// <para>🔁 <b>2026-10-28 结清 `TD-ci-02`(用户裁定 ②自检 + ④Build 自足)</b>:</para>
        /// <para><b>④ 自足</b> —— 本方法**自己先调 <see cref="EnsureDataCoreGroupCore"/></b>,
        /// 再构建。缘由:Unity batchmode 写**多条** <c>-executeMethod</c> 时**只跑第一条**,
        /// 其余静默丢弃、退出码仍 0(六格判别矩阵见 <c>TD-ci-02</c>)。此前「先 Ensure 再 Build」
        /// 的最自然写法(两条 <c>-executeMethod</c>)**只跑第一步** ⇒ 组建好了、catalog 没重建
        /// ⇒ 新条目 address 查不到 Location ⇒ PlayMode 启动序整条挂,而进程**报绿**。
        /// 现在两步合成一条命令,写两条也不会漏 —— 因为 Build 自己就会 Ensure。</para>
        /// <para><b>② 自检</b> —— 构建前断言「组内条目数 == <c>DataCooked</c> 下
        /// <c>*.cooked.bytes</c> 件数」,不等即 <c>throw</c>。缘由:这是「组漏了某个产物」
        /// 这一整类缺陷的**通用**判据 —— 不依赖人记得先跑 Ensure,也不依赖 Ensure 实现正确。
        /// 二者叠加:④ 给正确路径,② 兜住任何走错路的人。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/构建 Addressables 内容(BuildPlayerContent)")]
        public static void BuildAddressablesContent()
        {
            BuildAddressablesContentCore(
                AddressableAssetSettingsDefaultObject.Settings,
                $"Assets/{CookedDirName}",
                DataCoreGroup);
        }

        /// <summary>
        /// <see cref="BuildAddressablesContent()"/> 的**可注入实现体** —— 设置对象 / 产物目录 /
        /// 组名为形参,使 ② 的自检分支可被夹具命中(与 <see cref="EnsureDataCoreGroupCore"/> 同款两半分离)。
        /// <para>顺序**固定**:① 设置缺失硬失败 → ④ Ensure(先验后改,失败则设置零改动)→
        /// ② 条目数自检 → 构建。</para>
        /// <para>⚠️ <b>④ 排在 ② 之前</b>:若先自检再 Ensure,则在「盘上新烘了产物但组还没登记」
        /// 这个**最常见**的情形下会直接抛 —— 而那恰恰是 Ensure 存在的理由。先 Ensure 让自检
        /// 面对的是「修好之后」的状态,自检于是只负责抓「Ensure 也修不好的」那一类
        /// (如 Ensure 实现回归、或组被手工改坏)。</para>
        /// </summary>
        /// <param name="settings">Addressables 设置(注入 <c>null</c> ⇒ 命中设置缺失分支)。</param>
        /// <param name="cookedDirRel">产物目录(工程相对路径;指向空目录 ⇒ 自检必然不等)。</param>
        /// <param name="groupName">组名(注入一个不存在的名字 ⇒ Ensure 的组缺失分支先命中)。</param>
        /// <exception cref="InvalidOperationException">设置缺失 / 组缺失 / 产物目录空 / 条目数与产物数不等。</exception>
        public static void BuildAddressablesContentCore(
            AddressableAssetSettings settings, string cookedDirRel, string groupName)
        {
            if (settings == null)
                throw new InvalidOperationException(
                    "[大医精诚] Addressables 设置缺失 —— 组定义已随仓库入库" +
                    "(unity/Assets/AddressableAssetsData/)。" +
                    "跑 `git checkout -- unity/Assets/AddressableAssetsData` 恢复。");

            // ④ 自足:先确保组(内部含三条硬失败;失败则在任何写入前抛出)
            EnsureDataCoreGroupCore(settings, cookedDirRel, groupName);

            // ② 自检:组内条目数须 == 盘上产物数(先 Ensure 后自检,理由见方法注释)
            int onDisk = CountCookedProducts(cookedDirRel);
            int inGroup = CountGroupEntries(settings, groupName);
            ThrowIfEntryCountMismatched(inGroup, onDisk, cookedDirRel);

            // 签名 = `static void BuildPlayerContent(out AddressablesPlayerBuildResult)`
            // (2.10.3 实读;先例 U1SpikeSetup.cs:242)—— 成败只能读 `result.Error`。
            // 用 `var` 免引 UnityEditor.AddressableAssets.Build 命名空间(与先例一致)。
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result?.Error))
                throw new InvalidOperationException(
                    "[大医精诚] BuildPlayerContent 失败:" + result.Error);

            Debug.Log(
                "[大医精诚] Addressables 内容已构建 —— catalog 现含全部组条目。" +
                $"({inGroup} 条,与盘上 {onDisk} 个产物一致)" +
                "⚠️ 本产物落 unity/Library/(gitignored,机器本地)⇒ 新检出后须重跑本菜单。");
        }

        /// <summary>数产物目录下的 <c>*.cooked.bytes</c> 件数(② 自检的左侧)。</summary>
        private static int CountCookedProducts(string cookedDirRel)
        {
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { cookedDirRel }))
                if (Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid))
                        .EndsWith(".cooked.bytes", StringComparison.Ordinal))
                    n++;
            return n;
        }

        /// <summary>
        /// ② 自检的**纯谓词半边**(承 `EnsureDataCoreGroupCore` 同款两半分离形态):
        /// 条目数 ≠ 产物数 ⇒ <c>throw</c>;相等 ⇒ 静默返回。
        /// <para>⚠️ 抽成谓词的理由:<c>CountGroupEntries</c> 需要一个真 <c>AddressableAssetSettings</c>,
        /// 而夹具**建临时组会污染入库的 <c>AddressableAssetSettings.asset</c>**
        /// (2026-10-17 已发生过:失败的测试把假标签写进了入库资产)。本谓词只吃两个 <c>int</c>,
        /// 夹具可直测「不等必抛」与「相等不抛」两条分支,零资产写入。</para>
        /// </summary>
        /// <param name="inGroup">组内条目数。</param>
        /// <param name="onDisk">产物目录下 <c>*.cooked.bytes</c> 件数。</param>
        /// <param name="cookedDirRel">产物目录(仅用于报文)。</param>
        /// <exception cref="InvalidOperationException">两数不等。</exception>
        public static void ThrowIfEntryCountMismatched(int inGroup, int onDisk, string cookedDirRel)
        {
            if (inGroup == onDisk) return;

            throw new InvalidOperationException(
                $"[大医精诚] data-core 组条目数 = {inGroup},而 {cookedDirRel} 下有 " +
                $"{onDisk} 个 *.cooked.bytes —— 不相等 ⇒ catalog 会缺条目," +
                "新产物的 address 查不到 Location(PlayMode 启动序整条挂)。" +
                "⚠️ 别用两条 `-executeMethod` 拼「先 Ensure 再 Build」:" +
                "Unity batchmode **只执行第一条**,其余静默丢弃且退出码仍为 0。" +
                "本菜单已自足(自己会 Ensure),单条调用即可。");
        }

        /// <summary>数组内条目数(② 自检的右侧;组缺失返回 0 —— 但那时 Ensure 已先抛)。</summary>
        private static int CountGroupEntries(AddressableAssetSettings settings, string groupName)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null) return 0;
            int n = 0;
            foreach (AddressableAssetEntry _ in group.entries) n++;
            return n;
        }

        /// <summary>
        /// 确保 Addressables 存在单一 <c>data-core</c> 组,并把 DataCooked 下两个产物移入、打上 data-core 标签。
        /// <para>⚠️ <b>组定义已随仓库入库</b>(2026-10-17 · `TD-audio-01a`)—— 本菜单只负责条目。
        /// (原文「机器生成物,永不提交」写于入库之前,已失效 —— 保留为口径沿革记录。)</para>
        /// <para>⚠️ <b>三处硬失败</b>(2026-10-17 加固;此前为静默 <c>LogError</c>/<c>LogWarning</c> + <c>return</c>,
        /// 以 <c>-executeMethod</c> 作 CI 前置步时**报绿而组实际缺失** = 假绿)。见
        /// <see cref="EnsureDataCoreGroupCore"/>。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/确保 data-core Addressables 组")]
        public static void EnsureDataCoreGroup()
        {
            EnsureDataCoreGroupCore(
                AddressableAssetSettingsDefaultObject.Settings,
                $"Assets/{CookedDirName}",
                DataCoreGroup);
        }

        /// <summary>
        /// <see cref="EnsureDataCoreGroup()"/> 的**可注入实现体** —— 设置对象与产物目录为形参,
        /// 使三条硬失败分支可被夹具命中(与 <see cref="EnsureAudioGroupCore"/> 同款两半分离形态)。
        /// <para>⚠️ <b>先验后改</b>:三条硬失败都在**任何写入之前**判定 ⇒ 失败时设置零改动。
        /// (原实现在 <c>moved == 0</c> 时**已先建组、先打标签**才 <c>LogWarning</c> ——
        /// 现已把「扫描 .cooked.bytes」提到写入之前。)</para>
        /// <para>⚠️ <b>刻意不叫 <c>EnsureDataCoreGroup</c></b> —— 与菜单方法重名会让
        /// `test_wiring_bakeMenuEntriesExist` 的按名反射抛 `AmbiguousMatchException`。</para>
        /// <para>⚠️ <b>不建组</b>(W-5②,2026-10-17):与 <see cref="EnsureAudioGroupCore"/> 同因 ——
        /// 组定义已入库,<c>CreateGroup</c> 的默认 schema 与入库的
        /// <c>data-core_BundledAssetGroupSchema</c>(<c>m_UseDefaultSchemaSettings: 0</c>)分叉。</para>
        /// </summary>
        /// <param name="settings">Addressables 设置(注入 <c>null</c> ⇒ 命中分支 ①)。</param>
        /// <param name="cookedDirRel">产物目录(工程相对路径;指向空目录 ⇒ 命中分支 ②)。</param>
        /// <param name="groupName">组名(注入一个不存在的名字 ⇒ 命中分支 ③)。</param>
        /// <exception cref="InvalidOperationException">设置缺失 / 产物目录下零 *.cooked.bytes / 组缺失。</exception>
        public static void EnsureDataCoreGroupCore(
            AddressableAssetSettings settings, string cookedDirRel, string groupName)
        {
            // ① 设置缺失 —— 硬失败(2026-10-17)。组定义已入库 ⇒ 缺失 = 入库文件被删/损坏。
            if (settings == null)
                throw new InvalidOperationException(
                    "[大医精诚] Addressables 设置缺失 —— 组定义已随仓库入库" +
                    "(unity/Assets/AddressableAssetsData/)。" +
                    "跑 `git checkout -- unity/Assets/AddressableAssetsData` 恢复;" +
                    "⚠️ 不要手建设置:新建的只含默认组,与入库定义分叉。");

            // ② 产物缺失 —— 硬失败(2026-10-17)。先验后改:此处尚未写入任何东西。
            var guids = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { cookedDirRel }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).EndsWith(".cooked.bytes", StringComparison.Ordinal))
                    guids.Add(guid);
            }

            if (guids.Count == 0)
                throw new InvalidOperationException(
                    $"[大医精诚] {cookedDirRel} 下无 *.cooked.bytes —— " +
                    "组会是空的。先跑「烘焙 item-database」等烘焙菜单再执行本菜单" +
                    "(ADR-014 §五 的启动期预载依赖这些条目)。");

            // ③ 组缺失 —— 硬失败(2026-10-17)。与分支 ① 同构,恢复靠 git,不靠 CreateGroup。
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
                throw new InvalidOperationException(
                    $"[大医精诚] {groupName} 组缺失 —— 组定义已随仓库入库" +
                    "(unity/Assets/AddressableAssetsData/AssetGroups/)。" +
                    "跑 `git checkout -- unity/Assets/AddressableAssetsData` 恢复;" +
                    $"⚠️ 不要手建 {groupName} 组:CreateGroup 只给 Addressables 默认 schema," +
                    "与入库定义分叉。");

            // ④ 打标签 / 登记条目(此后才写)
            if (!settings.GetLabels().Contains(groupName))
                settings.AddLabel(groupName, postEvent: true);

            int moved = 0;
            foreach (string guid in guids)
            {
                string fileName = Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid));
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: true);
                entry.SetAddress(fileName);
                entry.SetLabel(groupName, true, force: true, settings);
                moved++;
            }

            Debug.Log(
                $"[大医精诚] {groupName} 组就绪:{moved} 个条目已入组并打标签。");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 确保 Addressables 存在 <c>audio</c> 素材组,并把 <c>Assets/Audio/</c> 下的音频素材移入、打上 audio 标签。
        /// <para>⚠️ <b>为什么必须与 <c>data-core</c> 分家</b>(AC-44-D5):素材是**流式**资源
        /// (按需加载 / 可卸载),数据烘焙产物是**启动期全量预载**的小体量二进制。混组 ⇒
        /// 流式素材被打进数据包(内存与加载时序双失);组区分由
        /// <c>AudioAssetGroupGates.ValidateSettings()</c> 构建期断言。</para>
        /// <para>⚠️ <b>组定义已随仓库入库</b>(2026-10-17 · `TD-audio-01a` 收窄 `.gitignore`)——
        /// 本菜单只负责**条目**。设置缺失 = 入库文件被删/损坏 ⇒ <b>硬失败并指向
        /// <c>git checkout</c> 恢复,不静默重建</b>(重建的 settings 只含默认组,与入库定义
        /// **分叉** —— 那是假绿:看着绿,组是错的)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/确保 audio Addressables 组")]
        public static void EnsureAudioGroup()
        {
            EnsureAudioGroupCore(
                AddressableAssetSettingsDefaultObject.Settings,
                $"Assets/{AudioAssetGroupGates.AudioAssetDirName}",
                AudioAssetGroupGates.AudioGroupName);
        }

        /// <summary>
        /// <see cref="EnsureAudioGroup()"/> 的**可注入实现体** —— 设置对象与素材目录为形参,
        /// 使两条硬失败分支可被夹具命中(承 <c>AudioAssetGroupGates.ValidateEntries</c> 的
        /// 「纯谓词 + 调用方 throw」两半分离形态)。
        /// <para>⚠️ <b>先验后改</b>:三条硬失败都在**任何写入之前**判定 ⇒ 失败时设置零改动,
        /// 夹具可安全注入而不污染工程的 Addressables 状态。</para>
        /// <para>⚠️ <b>刻意不叫 <c>EnsureAudioGroup</c></b> —— 与菜单方法重名会让
        /// `test_wiring_bakeMenuEntriesExist` 的 `GetMethod(name, Public|Static)` 抛
        /// `AmbiguousMatchException`(该测按名反射查菜单项存在性)。</para>
        /// <para>⚠️ <b>不建组</b>(W-5②,2026-10-17):组定义已入库 ⇒ 组缺失 = 入库文件被删/损坏,
        /// 恢复靠 <c>git checkout</c>。原 <c>CreateGroup</c> 分支已删 —— 它建出的 schema 是
        /// Addressables 默认值,与入库的 <c>m_UseDefaultSchemaSettings: 0</c> 态**分叉**。</para>
        /// </summary>
        /// <param name="settings">Addressables 设置(注入 <c>null</c> ⇒ 命中分支 ①)。</param>
        /// <param name="audioDirRel">素材目录(工程相对路径;指向空目录 ⇒ 命中分支 ②)。</param>
        /// <param name="groupName">组名(注入一个不存在的名字 ⇒ 命中分支 ③)。</param>
        /// <exception cref="InvalidOperationException">设置缺失 / 素材目录下零素材 / 组缺失(分支 ③)。</exception>
        public static void EnsureAudioGroupCore(
            AddressableAssetSettings settings, string audioDirRel, string groupName)
        {
            // ① 设置缺失 —— 硬失败(TD-audio-01b/01e,2026-10-17)
            //    组定义已入库 ⇒ 缺失 = 入库文件被删/损坏;恢复靠 git,不靠本菜单重建。
            if (settings == null)
                throw new InvalidOperationException(
                    "[大医精诚] Addressables 设置缺失 —— 组定义已随仓库入库" +
                    "(unity/Assets/AddressableAssetsData/)。" +
                    "跑 `git checkout -- unity/Assets/AddressableAssetsData` 恢复;" +
                    "⚠️ 不要手建设置:新建的只含默认组,与入库定义分叉。");

            // ② 素材缺失 —— 硬失败(TD-audio-01e ②,2026-10-17)。先验后改:此处尚未写入任何东西。
            var guids = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { audioDirRel }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(AudioAssetGroupGates.AudioAssetExtension, StringComparison.Ordinal))
                    guids.Add(guid);
            }

            if (guids.Count == 0)
                throw new InvalidOperationException(
                    $"[大医精诚] {audioDirRel} 下无 {AudioAssetGroupGates.AudioAssetExtension} 素材 —— " +
                    "组会是空的。先放入素材再执行本菜单" +
                    "(AC-44-D6 的素材存在性门也依赖它们)。");

            // ③ 组缺失 —— 硬失败(TD-audio-01 W-5②,2026-10-17)。先验后改:此处尚未写入任何东西。
            //    组定义已入库 ⇒ 「组缺失」= 入库文件被删/损坏,与分支 ① 完全同构,恢复靠 git。
            //    ⚠️ 不建组:CreateGroup(schemasToCopy: null) 给的是 **Addressables 默认字段值**,
            //    而入库的 audio_BundledAssetGroupSchema 是 m_UseDefaultSchemaSettings: 0 的**脱离默认**态
            //    ⇒ 新建的 schema 与入库定义**分叉**(看着绿,组是错的)。
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
                throw new InvalidOperationException(
                    $"[大医精诚] {groupName} 组缺失 —— 组定义已随仓库入库" +
                    "(unity/Assets/AddressableAssetsData/AssetGroups/)。" +
                    "跑 `git checkout -- unity/Assets/AddressableAssetsData` 恢复;" +
                    $"⚠️ 不要手建 {groupName} 组:CreateGroup 只给 Addressables 默认 schema," +
                    "与入库定义分叉。");

            // ④ 打标签 / 登记条目(此后才写)
            if (!settings.GetLabels().Contains(groupName))
                settings.AddLabel(groupName, postEvent: true);

            int moved = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: true);
                entry.SetAddress(Path.GetFileName(path));   // address = bare 文件名(与 assets[] 同形,零映射)
                entry.SetLabel(groupName, true, force: true, settings);
                moved++;
            }

            Debug.Log(
                $"[大医精诚] {groupName} 组就绪:{moved} 个素材已入组并打标签" +
                $"(与 {DataCoreGroup} 分家 —— AC-44-D5)。");

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// E-13 自检(ADR-014 §五:装载失败 = 带 [E-13] 上下文的启动期硬失败,绝不 null 解引用)。
        /// <para>流程:<b>⓪ Play Mode 归位</b> Use Asset Database → ① 反向 —— <b>反射直呼</b>
        /// <c>FetchBytes</c>(私有静态)传一个<b>从未存在过的 key</b>,走生产同款失败路径
        /// (Locator 解析不到 ⇒ Addressables 报错 ⇒ catch 包成 [E-13] IOE);期望
        /// <see cref="TargetInvocationException"/> 的 Inner 是含 [E-13] 的
        /// <see cref="InvalidOperationException"/>;② 正向复验 —— 新 provider 载 items + recipes,期望成功。</para>
        /// <para>⚠️ <b>⓪ 为什么必须归位</b>(2026-09-24 第四跑实测):U1 spike
        /// (<c>大医精诚/Spike/Setup U1 Spikes</c>)把 Play Mode 切 <b>Use Existing Build</b> 并
        /// <c>BuildPlayerContent</c> 过,Teardown 不复原;此刻已建 bundle 路径已失效
        /// (<c>Invalid path … data-core_assets_all_*.bundle</c>)⇒ 正向复验在 Existing Build 下必死。
        /// 归位 Use Asset Database(日常开发默认)后,正向直接从 AssetDatabase 载,不依赖 bundle。</para>
        /// <para>⚠️ <b>反向不改 address、不移文件</b>(前两版机制均已实测失败,留档):
        /// ① 改 address —— <c>SetAddress(postEvent)</c> 不重建运行期 locator,菜单内改完旧地址仍能解析,重启无效;
        /// ② 文件暂移 —— Existing Build 下 bundle 里有内容载不失败,且归位 + Refresh 踩「same files already loaded」。
        /// 反射探针零状态变更:bad key 在 Locator 层就失败,不触碰任何 bundle。</para>
        /// <para>每次点菜单都用<b>全新</b> provider(绕开 <see cref="DataCorePreloader"/> 幂等)。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/E-13 自检(反路径+正复验)")]
        public static void E13SelfCheck()
        {
            // 救援:文件暂移版若中途崩溃致产物滞留 hold → 先归位(一次性兼容,新机制已不移文件)
            string itemsAbs = Path.Combine(Application.dataPath, CookedDirName, ItemsCookedAssetName);
            string itemsMetaAbs = itemsAbs + ".meta";
            string holdDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "E13Hold"));
            string holdItems = Path.Combine(holdDir, ItemsCookedAssetName);
            string holdMeta = holdItems + ".meta";
            if (!File.Exists(itemsAbs) && File.Exists(holdItems))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(itemsAbs));
                File.Move(holdItems, itemsAbs);
                if (File.Exists(holdMeta)) File.Move(holdMeta, itemsMetaAbs);
                AssetDatabase.Refresh();
                Debug.Log("[E-13 自检] 已将滞留 Library/E13Hold 的产物归位(文件暂移版残留救援)。");
            }

            // ── ⓪ Play Mode 归位(须在任何 Addressables 调用之前)──
            EnsureAssetDatabasePlayMode(AddressableAssetSettingsDefaultObject.Settings);

            // ── ① 反向:反射直呼 FetchBytes(从未存在的 key)—— 生产同款失败路径,零状态变更 ──
            MethodInfo fetchBytes = typeof(AddressablesDataProvider).GetMethod(
                "FetchBytes", BindingFlags.NonPublic | BindingFlags.Static);
            if (fetchBytes == null)
            {
                Debug.LogError(
                    "[E-13 自检] 反向跳过:反射未找到 AddressablesDataProvider.FetchBytes" +
                    "(签名/可见性被改?)—— 契约测试失效,正向仍继续。");
            }
            else
            {
                const string neverKey = "__dyjc_e13_never__.missing";
                try
                {
                    fetchBytes.Invoke(null, new object[] { neverKey });
                    Debug.LogWarning(
                        "[E-13 自检] 反向未触发:从未存在的 key 竟然取数成功 —— 不应发生," +
                        "检查 FetchBytes 是否被短路或 Addressables 返回了非空默认值。");
                }
                catch (TargetInvocationException tie)
                {
                    Exception inner = tie.InnerException;
                    if (inner is InvalidOperationException ioe &&
                        ioe.Message.IndexOf("[E-13]", StringComparison.Ordinal) >= 0)
                    {
                        Debug.Log($"[E-13 自检] 反向通过:硬失败带 [E-13] 上下文、非 null 解引用 ✓ —— {ioe.Message}");
                    }
                    else
                    {
                        Debug.LogError(
                            $"[E-13 自检] 反向异常但缺 [E-13] 包裹(违 ADR-014 §五):" +
                            $"{inner?.GetType().Name ?? "null"}: {inner?.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"[E-13 自检] 反向异常但缺 [E-13] 包裹(违 ADR-014 §五):" +
                        $"{ex.GetType().Name}: {ex.Message}");
                }
            }

            // ── ② 正向复验(全新 provider 真取数;反向只碰 bad key,不加载真实 bundle)──
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

        /// <summary>纯谓词半边:在 DataBuilders 名单里找「Use Asset Database」builder 的索引;
        /// 名单为空 / 无匹配 ⇒ 返回 <c>-1</c>。
        /// <para>⚠️ DataBuilders 声明类型是 List&lt;ScriptableObject&gt;(2.10.3 实读),
        /// <c>Name</c> 是 IDataBuilder 成员 ⇒ 必须走 <c>GetDataBuilder(i)</c>(承 U1SpikeSetup 同款警告)。</para>
        /// <para><b>为何单独拆出</b>:夹具可注入 builder 名单直断谓词,不必经真设置
        /// (两半分离 —— 承 <c>AudioAssetGroupGates.ValidateEntries</c> 先例)。</para></summary>
        public static int FindAssetDatabaseBuilderIndex(AddressableAssetSettings settings)
        {
            if (settings == null) return -1;
            int count = settings.DataBuilders.Count;
            for (int i = 0; i < count; i++)
            {
                string name = settings.GetDataBuilder(i)?.Name ?? "<null>";
                if (name.IndexOf("Asset Database", StringComparison.OrdinalIgnoreCase) >= 0) return i;
            }
            return -1;
        }

        /// <summary>Play Mode 归位到 Use Asset Database。<b>返回是否真的发生了改动</b>。</summary>
        /// <remarks>⚠️ <b>为什么这件事必须显式做(2026-10-17 · TD-audio-01f 根因)</b>:
        /// Play Mode Script **不在**入库的 <c>AddressableAssetSettings.asset</c> 里 ——
        /// 它住 <c>ProjectConfigData.ActivePlayModeIndex</c>,落
        /// <c>unity/Library/AddressablesConfig.dat</c>(**机器本地、gitignored**,
        /// 包源码 <c>ProjectConfigData.cs:390</c>)。默认值 = <c>0</c>(Use Asset Database)
        /// ⇒ **全新检出本来就没有这个问题**;坏的是**被改过的那台机器**。
        /// <para>改它的两处:① U1 spike 的 <c>ConfigurePlayMode</c>(切到 Existing Build);
        /// ② 本菜单。⚠️ <b>① 已于 2026-10-17 补上复原</b> —— <c>U1SpikeSetup.Teardown</c>
        /// 的 <c>finally</c> 现在委派到本方法(判据单一出处);但
        /// <b>「Setup 跑了却从不跑 Teardown」的机器仍会漂移</b> ⇒ 兜底仍是本菜单 +
        /// 机器前置门 <c>test_playModeScript_isAssetDatabase</c>。留在 Existing Build 时 PlayMode 解析的是
        /// <c>Library/com.unity.addressables/aa/&lt;platform&gt;/catalog.bin</c> 这份**预建 catalog**,
        /// 组条目一变就陈旧 ⇒ <c>InvalidKeyException: No Location found for Key=…</c>,
        /// 启动序整条挂掉(2026-09-24 第四跑 / 2026-10-17 补口轮两次同症状)。</para>
        /// <para><b>先验后改</b>:找不到目标 builder 或已在目标 ⇒ 零写入直接返回 <c>false</c>。</para>
        /// </remarks>
        /// <param name="settings">Addressables 设置(调用方给出,便于夹具注入)。</param>
        public static bool EnsureAssetDatabasePlayMode(AddressableAssetSettings settings) =>
            EnsureAssetDatabasePlayMode(settings, writeIndex: null);

        /// <summary>同 <see cref="EnsureAssetDatabasePlayMode(AddressableAssetSettings)"/>,
        /// 但索引写入走可注入的 <paramref name="writeIndex"/>(夹具面:只在进程内改、不落盘)。
        /// <para>⚠️ <b>为何要这个重载(2026-10-17)</b>:<c>settings.ActivePlayModeDataBuilderIndex</c>
        /// 的 setter 内联 <c>ProjectConfigData.SaveData()</c> ⇒ 真实路径必然写
        /// <c>Library/AddressablesConfig.dat</c>。夹具若走真实路径,「本机已漂移」时会把漂移**固化**回盘;
        /// 哪怕盘值不变,文件也被重写。抽一个<b>写入缝</b>后,夹具断言的是<b>判据逻辑本身</b>
        /// (找到目标 / 先验后改 / 改到目标),落盘与否则由注入者决定。</para>
        /// <para>null ⇒ 真实路径(<see cref="EnsureAssetDatabasePlayMode(AddressableAssetSettings)"/> 的默认)。</para>
        /// </remarks>
        internal static bool EnsureAssetDatabasePlayMode(AddressableAssetSettings settings, Action<int> writeIndex)
        {
            if (settings == null) return false;

            int target = FindAssetDatabaseBuilderIndex(settings);
            if (target < 0)
            {
                Debug.LogWarning(
                    "[大医精诚] 未找到名含「Asset Database」的 DataBuilder —— Play Mode 未改。" +
                    "手动:Window > Asset Management > Addressables > Groups → Play Mode Script。");
                return false;
            }

            int current = settings.ActivePlayModeDataBuilderIndex;
            if (target == current) return false;

            string currentName = settings.GetDataBuilder(current)?.Name ?? "<null>";
            if (writeIndex != null)
                writeIndex(target);
            else
                settings.ActivePlayModeDataBuilderIndex = target;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[大医精诚] Play Mode → Use Asset Database(DataBuilder[{target}]" +
                $"「{settings.GetDataBuilder(target)?.Name}」;原「{currentName}」)。" +
                "—— 该设置住 Library/AddressablesConfig.dat(机器本地),不入库。");
            return true;
        }

        /// <summary>菜单:<b>Play Mode 归位(Use Asset Database)</b> —— 修 <c>TD-audio-01f</c> 的机器侧漂移。
        /// <para>症状:PlayMode 启动序整条红,报文 <c>No Location found for Key=…</c>。
        /// 跑本菜单即可,<b>不必</b>重建 Addressables 内容(那是另一件事,见同目录菜单)。</para></summary>
        [MenuItem("大医精诚/数据管线/Play Mode 归位(Use Asset Database)")]
        public static void ResetPlayModeToAssetDatabase()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException(
                    "[大医精诚] Addressables 设置缺失 —— 组定义已随仓库入库" +
                    "(unity/Assets/AddressableAssetsData/)。" +
                    "跑 `git checkout -- unity/Assets/AddressableAssetsData` 恢复。");

            if (!EnsureAssetDatabasePlayMode(settings))
                Debug.Log("[大医精诚] Play Mode 已是 Use Asset Database —— 零改动。");
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
