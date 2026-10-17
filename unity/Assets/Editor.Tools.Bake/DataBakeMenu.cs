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
        /// 构建 Addressables 内容(<c>BuildPlayerContent</c>)—— **改过组条目之后必须跑**。
        /// <para>⚠️ <b>为什么需要这个菜单</b>(2026-10-17 实测):PlayMode 下 Addressables 解析的是
        /// <c>unity/Library/com.unity.addressables/aa/&lt;platform&gt;/catalog.bin</c> 这份**预建 catalog**。
        /// 新增条目(如 <c>audio_events.cooked.bytes</c>)后若不重建,该 address **查不到 Location** ——
        /// 实测症状 = `InvalidKeyException: No Location found for Key=…` ⇒ 启动序
        /// (<c>AddressablesDataProvider.Preload()</c>)整条挂掉,9 条 PlayMode 启动序测试全红。</para>
        /// <para>⚠️ 本菜单此前**不存在** —— 全库唯一调用 <c>BuildPlayerContent</c> 的是
        /// <c>U1SpikeSetup</c>(它顺带切 Play Mode 到 Existing Build,是另一个坑)。
        /// 本菜单只做构建,不动任何设置。</para>
        /// </summary>
        [MenuItem("大医精诚/数据管线/构建 Addressables 内容(BuildPlayerContent)")]
        public static void BuildAddressablesContent()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException(
                    "[大医精诚] Addressables 设置缺失 —— 组定义已随仓库入库" +
                    "(unity/Assets/AddressableAssetsData/)。" +
                    "跑 `git checkout -- unity/Assets/AddressableAssetsData` 恢复。");

            // 签名 = `static void BuildPlayerContent(out AddressablesPlayerBuildResult)`
            // (2.10.3 实读;先例 U1SpikeSetup.cs:242)—— 成败只能读 `result.Error`。
            // 用 `var` 免引 UnityEditor.AddressableAssets.Build 命名空间(与先例一致)。
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result?.Error))
                throw new InvalidOperationException(
                    "[大医精诚] BuildPlayerContent 失败:" + result.Error);

            Debug.Log(
                "[大医精诚] Addressables 内容已构建 —— catalog 现含全部组条目。" +
                "⚠️ 本产物落 unity/Library/(gitignored,机器本地)⇒ 新检出后须重跑本菜单。");
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
