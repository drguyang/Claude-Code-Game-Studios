// 权威来源:ADR-014 §五(Addressables 装载落边界程序集 IDataProvider;E-13 加载失败 = 启动期硬失败)
//          · IDataProvider(Sim.Contracts):「无错误返回通道 —— 实现方 try/catch 后直接 throw,不得返回默认值」
//          · Story 008(运行期取数唯一实现)
//
// ⚠️ E-13:Unity 6 Addressables 6.2+ 可能抛异常而非返回 null(post-cutoff,须桌面实测)——
//    全路径 try/catch 后带上下文 rethrow;任何 null / 短于头 / 魔数错同样 throw;绝不 null 解引用。
// ⚠️ 本程序集零 JSON 解析、零 FixParse(AC-48 结构守卫 RuntimeSourceGuard 扫描面内)。

using System;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Casebook;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.Presentation.Input;
using DaYiJingCheng.Gameplay.Presentation.Settings;
using DaYiJingCheng.Gameplay.Presentation.Telemetry;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using SignDataSet = DaYiJingCheng.Gameplay.Presentation.Diagnosis.DiagnosisSignDataSet;
using ReadFloorDataSet = DaYiJingCheng.Gameplay.Presentation.Diagnosis.DiagnosisReadFloorDataSet;
using LexiconDataSet = DaYiJingCheng.Gameplay.Presentation.Casebook.JudgmentLexiconDataSet;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>data-core 组 cooked 产物的 <see cref="IDataProvider"/> 实现(Addressables 装载 + 解码 + 缓存)。
    /// <para>失败语义:任何装载 / 校验失败 ⇒ 带 [E-13] 上下文的 <see cref="InvalidOperationException"/>
    /// (启动期硬失败),<b>绝不</b>返回默认值或 null。</para>
    /// <example><code>var provider = new AddressablesDataProvider();
    /// ItemDataSet items = provider.Load&lt;ItemDataSet&gt;();</code></example>
    /// </summary>
    public sealed class AddressablesDataProvider : IDataProvider
    {
        /// <summary>字节来源注入缝 —— <c>null</c> 表示走生产路径 <see cref="FetchFromAddressables"/>。</summary>
        private readonly Func<string, byte[]> _byteSource;

        /// <summary>生产构造:字节一律取自 Addressables(data-core 组)。</summary>
        public AddressablesDataProvider()
            : this(null)
        {
        }

        /// <summary>可注入构造 —— <b>只给 EditMode 夹具用</b>(生产零调用点)。
        /// <para><b>为什么需要(2026-10-21 · S17-3 AC-5 装载测)</b>:<see cref="FetchBytes"/> 原为
        /// <c>private static</c> 直调 Addressables ⇒ <c>EnsureTutorialContent</c> 的解码分支在 EditMode
        /// 里<b>只可观测到失败</b>(Addressables 无 catalog),正向半边不可达。抽出的唯一目的 = 让
        /// 「provider 装载分支 → 真 cooked 字节 → 解码出表」成为可执行判据,承
        /// <see cref="DataCorePreloader.TryPreloadGated"/> 的同款先例。
        /// <para>⚠️ <b>判据不弱化</b>:生产构造传 <c>null</c> ⇒ 仍走 Addressables + E-13 硬失败,
        /// 零行为变化。本缝只改「字节从哪来」,不改「失败怎么报」。</para></summary>
        /// <param name="byteSource">地址 → 字节;返回 <c>null</c> / 空 / 短于头部 ⇒ 走与原实现同款的 E-13 抛出。</param>
        internal AddressablesDataProvider(Func<string, byte[]> byteSource)
        {
            _byteSource = byteSource;
        }

        /// <summary>物品种子表 Addressables address(= DataCooked 文件名,菜单写入时设置)。</summary>
        public const string ItemsAddress = "item_database_items.cooked.bytes";

        /// <summary>配方表 Addressables address。</summary>
        public const string RecipesAddress = "item_database_recipes.cooked.bytes";

        /// <summary>8 词表装载常量(cooked 产物文件名,与 bake / DataCooked 目录同值)。</summary>
        private static class SignsAddr
        {
            /// <summary>词条表文件名(字面串 —— 不是型名 token)。</summary>
            public const string Signs = "diagnosis_signs.cooked.bytes";

            /// <summary>门槛定表文件名(同上)。</summary>
            public const string Floor = "diagnosis_read_floor.cooked.bytes";

            /// <summary>病名册词表文件名(sprint-08 S1.1 件 D;S1.0 已烘产物)。</summary>
            public const string Lexicon = "case_judgment_lexicon.cooked.bytes";
        }

        /// <summary>词条表 address(S1.2;bake 产物同目录)。</summary>
        public const string SignTableAddress = SignsAddr.Signs;

        /// <summary>门槛定表 address(S1.2;bake 产物同目录)。</summary>
        public const string ReadFloorTableAddress = SignsAddr.Floor;

        /// <summary>病名册词表 address(sprint-08 S1.1 件 D;S1.0 已烘未接)。</summary>
        public const string JudgmentLexiconAddress = SignsAddr.Lexicon;

        /// <summary>音频事件表 address(S14-6;AC-44-D4 —— 表走 ADR-014 烘焙管线,运行期零 JSON 解析器)。
        /// <para>⚠️ 单一出处 = <see cref="AudioEventDataSet.CookedFileName"/>(菜单写盘名与 address 共用同一常量)。</para></summary>
        public const string AudioEventsAddress = AudioEventDataSet.CookedFileName;

        /// <summary>设置暴露面注册表 address(S14-6;story-011 §登记项 —— 运行期装载面归本件)。
        /// <para>⚠️ 单一出处 = <see cref="AudioSettingsDataSet.CookedFileName"/>。</para></summary>
        public const string AudioSettingsAddress = AudioSettingsDataSet.CookedFileName;

        /// <summary>设备切换迟滞常量表 address(TD-epic-01 第十一片;原「临时种子」的还债面)。
        /// <para>⚠️ 单一出处 = <see cref="DeviceSwitchTuningDataSet.CookedFileName"/>。</para></summary>
        public const string DeviceSwitchTuningAddress = DeviceSwitchTuningDataSet.CookedFileName;

        /// <summary>遥测参数旋钮表 address(TD-epic-01 第十二片;F4–F7 六旋钮的还债面)。
        /// <para>⚠️ 单一出处 = <see cref="TelemetryAnalyticsDataSet.CookedFileName"/>。</para></summary>
        public const string TelemetryAnalyticsAddress = TelemetryAnalyticsDataSet.CookedFileName;

        /// <summary>交互种类契约表 address(2026-10-20 补接线;AC-4-15 的装载半边)。
        /// <para>⚠️ 产物名单一出处 = `DataBakeMenu.InteractionKindsCookedAssetName`;本处为字面串
        /// (边界程序集不引用 Editor.Tools.Bake —— 那是编辑期装配,不进玩家构建)。</para></summary>
        public const string InteractionKindsAddress = "interaction_kinds.cooked.bytes";

        /// <summary>阴性把握度定表 address(2026-10-20 补接线)。
        /// <para>⚠️ 产物名单一出处 = `DataBakeMenu.DiagnosisNegativeConfidenceCookedAssetName`;同上为字面串。</para></summary>
        public const string DiagnosisNegativeConfidenceAddress =
            "diagnosis_negative_confidence.cooked.bytes";

        /// <summary>处方表 address(2026-10-20 第二轮接线)。
        /// <para>⚠️ 产物名单一出处 = `DataBakeMenu.PrescriptionActionsCookedAssetName`;同上为字面串。</para></summary>
        public const string PrescriptionActionsAddress = "prescription_actions.cooked.bytes";

        /// <summary>处置轴契约表 address(2026-10-20 第二轮接线)。
        /// <para>⚠️ 产物名单一出处 = `DataBakeMenu.DiseaseActionAxisCookedAssetName`;同上为字面串。</para></summary>
        public const string DiseaseActionAxisAddress = "disease_action_axis.cooked.bytes";

        /// <summary>53 判定表 address(2026-10-20 第二轮接线)。
        /// <para>⚠️ 产物名单一出处 = `DataBakeMenu.OutcomeTableCookedAssetName`;同上为字面串。</para></summary>
        public const string OutcomeTableAddress = "53_outcome_table.cooked.bytes";

        /// <summary>53 延迟表 address(2026-10-20 第二轮接线)。
        /// <para>⚠️ 产物名单一出处 = `DataBakeMenu.DelayTableCookedAssetName`;同上为字面串。</para></summary>
        public const string DelayTableAddress = "53_delay_table.cooked.bytes";

        /// <summary>48 教学内容 address(S17-3 · AC-5「运行期只读 .cooked」的装载半边)。
        /// <para>⚠️ 单一出处 = <see cref="TutorialContentTable.CookedFileName"/>(解码器与 address
        /// 共用同一常量;菜单写盘名在编辑期装配里,边界程序集不引用它)。</para></summary>
        public const string TutorialContentAddress = TutorialContentTable.CookedFileName;

        /// <summary>24 CONTEXT_TABLE address(S17-2 前置:clinic-001 三表落 cooked 后补接线)。
        /// <para>⚠️ 产物名单一出处 = `DataBakeMenu.ClinicContextCookedAssetName`;同上为字面串
        /// (边界程序集不引用编辑期装配)。</para></summary>
        public const string ClinicContextAddress = "clinicmachine_context.cooked.bytes";

        /// <summary>24 ADJ_TABLE address(同上)。</summary>
        public const string ClinicAdjAddress = "clinicmachine_adj.cooked.bytes";

        /// <summary>24 ROOM_TABLE address(同上)。</summary>
        public const string ClinicRoomAddress = "clinicmachine_room.cooked.bytes";

        /// <summary>21a 家具目录 address(O-24-1 兑现 · `FURN_TYPES` 闭集的运行期载体)。
        /// <para>⚠️ 产物名单一出处 = `DataBakeMenu.FurnitureCatalogueCookedAssetName`;同上为字面串
        /// (边界程序集不引用编辑期装配)。</para></summary>
        public const string FurnitureCatalogueAddress = "item_database_furniture.cooked.bytes";

        /// <summary>23 模块目录的 cooked 地址(`TD-building-01` 读侧 · 2026-10-21)。</summary>
        /// <remarks>与写方 `BuildModulesBake.CookedFileName` / 读方 `BuildModulesDataSet.CookedFileName` 同值。</remarks>
        public const string BuildModulesAddress = BuildModulesDataSet.CookedFileName;

        /// <summary>data-core 组标签(ADR-014 §五:单组预载)。</summary>
        public const string DataCoreLabel = "data-core";

        private ItemDataSet? _items;
        private RecipeDataSet? _recipes;
        // 8 侧两数据集型经文件头 **using 别名**(SignDataSet / ReadFloorDataSet)引入。
        private SignDataSet? _signs;
        private ReadFloorDataSet? _readFloor;
        // 病名册词表(sprint-08 S1.1 件 D):struct(2026-10-12 改)⇒ 可空承载。
        private LexiconDataSet? _lexicon;
        // 音频侧两数据集(S14-6):struct ⇒ 可空承载。
        private AudioEventDataSet? _audioEvents;
        private AudioSettingsDataSet? _audioSettings;
        // 设备切换常量表(TD-epic-01 第十一片):struct ⇒ 可空承载。
        private DeviceSwitchTuningDataSet? _deviceSwitchTuning;
        // 遥测参数旋钮表(TD-epic-01 第十二片):struct ⇒ 可空承载。
        private TelemetryAnalyticsDataSet? _telemetryAnalytics;
        // 交互种类契约表(2026-10-20 补接线):struct ⇒ 可空承载。
        private InteractionKindDataSet? _interactionKinds;
        // 阴性把握度定表(2026-10-20 补接线):struct ⇒ 可空承载。
        private DiagnosisNegativeConfidenceDataSet? _negativeConfidence;
        // 处方表 / 处置轴 / 53 判定表 / 53 延迟表(2026-10-20 第二轮接线):均 struct ⇒ 可空承载。
        private PrescriptionActionsDataSet? _prescriptionActions;
        private DiseaseActionAxisDataSet? _diseaseActionAxis;
        private OutcomeTable53DataSet? _outcomeTable;
        private DelayTable53DataSet? _delayTable;
        // 48 教学内容(S17-3 · AC-5):struct 只读表 ⇒ 可空承载。
        private TutorialContentTable? _tutorialContent;
        // 24 医馆三表(S17-2 前置):均 struct 解码形 ⇒ 可空承载。
        private ClinicContextDataSet? _clinicContext;
        private ClinicAdjDataSet? _clinicAdj;
        private ClinicRoomDataSet? _clinicRoom;
        // 21a 家具目录(O-24-1):struct 解码形 ⇒ 可空承载。
        private ClinicFurnitureDataSet? _clinicFurniture;
        // 23 模块目录(TD-building-01 读侧 · 2026-10-21):struct 解码形 ⇒ 可空承载。
        private BuildModulesDataSet? _buildModules;

        // ══════════ 8 词表/门槛装载(sprint-07 S1.2)══════════
        // 逃逸判据(S1.2 修订 2026-10-11):CheckSourceEscape 只判「非 8 前缀文件**声明**了
        // 8 类型」;**引用/消费** 8 公开 API = 合法面 ⇒ 本文件直接引用,无需绕 token。

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">装载失败 / 未知数据集类型(启动期硬失败)。</exception>
        public TDataSet Load<TDataSet>() where TDataSet : struct
        {
            if (typeof(TDataSet) == typeof(ItemDataSet))
            {
                EnsureItems();
                return (TDataSet)(object)_items.Value;
            }
            if (typeof(TDataSet) == typeof(RecipeDataSet))
            {
                EnsureRecipes();
                return (TDataSet)(object)_recipes.Value;
            }
            // ── S1.2(2026-10-11):8 侧两数据集经 codec 装载(逃逸判据只拦「声明」,引用放行)──
            if (typeof(TDataSet) == typeof(SignDataSet))
            {
                EnsureSigns();
                return (TDataSet)(object)_signs.Value;
            }
            if (typeof(TDataSet) == typeof(ReadFloorDataSet))
            {
                EnsureReadFloor();
                return (TDataSet)(object)_readFloor.Value;
            }
            // ── S1.1 件 D(2026-10-12):病名册词表经 codec 装载 ──
            if (typeof(TDataSet) == typeof(LexiconDataSet))
            {
                EnsureLexicon();
                return (TDataSet)(object)_lexicon.Value;
            }
            // ── S14-6(2026-10-16):44 侧两数据集经 codec 装载 ──
            if (typeof(TDataSet) == typeof(AudioEventDataSet))
            {
                EnsureAudioEvents();
                return (TDataSet)(object)_audioEvents.Value;
            }
            if (typeof(TDataSet) == typeof(AudioSettingsDataSet))
            {
                EnsureAudioSettings();
                return (TDataSet)(object)_audioSettings.Value;
            }
            // ── TD-epic-01 第十一片(2026-10-19):设备切换常量表 ──
            if (typeof(TDataSet) == typeof(DeviceSwitchTuningDataSet))
            {
                EnsureDeviceSwitchTuning();
                return (TDataSet)(object)_deviceSwitchTuning.Value;
            }
            // ── TD-epic-01 第十二片(2026-10-19):遥测参数旋钮表 ──
            if (typeof(TDataSet) == typeof(TelemetryAnalyticsDataSet))
            {
                EnsureTelemetryAnalytics();
                return (TDataSet)(object)_telemetryAnalytics.Value;
            }
            // ── 2026-10-20 补接线:4 交互契约表 / 8 阴性把握度定表 ──
            if (typeof(TDataSet) == typeof(InteractionKindDataSet))
            {
                EnsureInteractionKinds();
                return (TDataSet)(object)_interactionKinds.Value;
            }
            if (typeof(TDataSet) == typeof(DiagnosisNegativeConfidenceDataSet))
            {
                EnsureDiagnosisNegativeConfidence();
                return (TDataSet)(object)_negativeConfidence.Value;
            }
            // ── 2026-10-20 第二轮接线:11 处方表 / 9 处置轴 / 53 判定表 / 53 延迟表 ──
            if (typeof(TDataSet) == typeof(PrescriptionActionsDataSet))
            {
                EnsurePrescriptionActions();
                return (TDataSet)(object)_prescriptionActions.Value;
            }
            if (typeof(TDataSet) == typeof(DiseaseActionAxisDataSet))
            {
                EnsureDiseaseActionAxis();
                return (TDataSet)(object)_diseaseActionAxis.Value;
            }
            if (typeof(TDataSet) == typeof(OutcomeTable53DataSet))
            {
                EnsureOutcomeTable();
                return (TDataSet)(object)_outcomeTable.Value;
            }
            if (typeof(TDataSet) == typeof(DelayTable53DataSet))
            {
                EnsureDelayTable();
                return (TDataSet)(object)_delayTable.Value;
            }
            // ── S17-3(2026-10-21):48 教学内容(AC-5 运行期只读)──
            if (typeof(TDataSet) == typeof(TutorialContentTable))
            {
                EnsureTutorialContent();
                return (TDataSet)(object)_tutorialContent.Value;
            }
            // ── S17-2 前置(2026-10-21):24 医馆三表(cooked 落盘 + 装载面)──
            if (typeof(TDataSet) == typeof(ClinicContextDataSet))
            {
                EnsureClinicContext();
                return (TDataSet)(object)_clinicContext.Value;
            }
            if (typeof(TDataSet) == typeof(ClinicAdjDataSet))
            {
                EnsureClinicAdj();
                return (TDataSet)(object)_clinicAdj.Value;
            }
            if (typeof(TDataSet) == typeof(ClinicRoomDataSet))
            {
                EnsureClinicRoom();
                return (TDataSet)(object)_clinicRoom.Value;
            }
            // ── O-24-1(2026-10-21):21a 家具目录(FURN_TYPES 闭集)──
            if (typeof(TDataSet) == typeof(ClinicFurnitureDataSet))
            {
                EnsureClinicFurniture();
                return (TDataSet)(object)_clinicFurniture.Value;
            }
            // ── TD-building-01 读侧(2026-10-21):23 模块目录(cost / furn_type / 足迹)──
            if (typeof(TDataSet) == typeof(BuildModulesDataSet))
            {
                EnsureBuildModules();
                return (TDataSet)(object)_buildModules.Value;
            }
            throw new InvalidOperationException(
                $"[E-13][data-core] 未知数据集类型 {typeof(TDataSet).FullName} —— 启动期硬失败;" +
                "IDataProvider 承载 ItemDataSet / RecipeDataSet + 三份 8/37 侧产物(S1.2 + S1.1 件 D 扩)" +
                " + 两份 44 侧产物(S14-6:AudioEventDataSet / AudioSettingsDataSet)" +
                " + 设备切换常量表(TD-epic-01 第十一片:DeviceSwitchTuningDataSet)" +
                " + 遥测参数旋钮表(TD-epic-01 第十二片:TelemetryAnalyticsDataSet)" +
                " + 交互种类契约表(2026-10-20:InteractionKindDataSet)" +
                " + 阴性把握度定表(2026-10-20:DiagnosisNegativeConfidenceDataSet)" +
                " + 处方表 / 处置轴 / 53 判定表 / 53 延迟表(2026-10-20 第二轮:PrescriptionActionsDataSet / " +
                "DiseaseActionAxisDataSet / OutcomeTable53DataSet / DelayTable53DataSet)" +
                " + 48 教学内容(S17-3:TutorialContentTable)" +
                " + 24 医馆三表(S17-2 前置:ClinicContextDataSet / ClinicAdjDataSet / ClinicRoomDataSet)" +
                " + 21a 家具目录(O-24-1:ClinicFurnitureDataSet)" +
                " + 23 模块目录(TD-building-01 读侧:BuildModulesDataSet)");
        }

        /// <summary>首次 Step 前的全量预载(调用契约见 <see cref="DataCorePreloader"/>;幂等,重复调用走缓存)。</summary>
        /// <exception cref="InvalidOperationException">任一产物装载失败(启动期硬失败)。</exception>
        public void Preload()
        {
            EnsureItems();
            EnsureRecipes();
            EnsureSigns();
            EnsureReadFloor();
            EnsureLexicon();
            EnsureAudioEvents();
            EnsureAudioSettings();
            EnsureDeviceSwitchTuning();
            EnsureTelemetryAnalytics();
            EnsureInteractionKinds();
            EnsureDiagnosisNegativeConfidence();
            EnsurePrescriptionActions();
            EnsureDiseaseActionAxis();
            EnsureOutcomeTable();
            EnsureDelayTable();
            EnsureTutorialContent();
            EnsureClinicContext();
            EnsureClinicAdj();
            EnsureClinicRoom();
            EnsureClinicFurniture();
            EnsureBuildModules();
        }

        private void EnsureItems()
        {
            if (_items.HasValue) return;
            byte[] bytes = FetchBytes(ItemsAddress);
            try
            {
                _items = CookedCodec.ReadItemDataSet(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 物品表 cooked 解码失败(address={ItemsAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureRecipes()
        {
            if (_recipes.HasValue) return;
            byte[] bytes = FetchBytes(RecipesAddress);
            try
            {
                _recipes = CookedCodec.ReadRecipeDataSet(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 配方表 cooked 解码失败(address={RecipesAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureSigns()
        {
            if (_signs.HasValue) return;
            byte[] bytes = FetchBytes(SignTableAddress);
            try
            {
                _signs = DiagnosisSignCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 8 词条表 cooked 解码失败(address={SignTableAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureReadFloor()
        {
            if (_readFloor.HasValue) return;
            byte[] bytes = FetchBytes(ReadFloorTableAddress);
            try
            {
                _readFloor = DiagnosisReadFloorCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 8 门槛定表 cooked 解码失败(address={ReadFloorTableAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureLexicon()
        {
            if (_lexicon.HasValue) return;
            byte[] bytes = FetchBytes(JudgmentLexiconAddress);
            try
            {
                _lexicon = JudgmentLexiconCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 病名册词表 cooked 解码失败(address={JudgmentLexiconAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureAudioEvents()
        {
            if (_audioEvents.HasValue) return;
            byte[] bytes = FetchBytes(AudioEventsAddress);
            try
            {
                _audioEvents = AudioEventCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 音频事件表 cooked 解码失败(address={AudioEventsAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureAudioSettings()
        {
            if (_audioSettings.HasValue) return;
            byte[] bytes = FetchBytes(AudioSettingsAddress);
            try
            {
                _audioSettings = AudioSettingsRegistryCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 设置注册表 cooked 解码失败(address={AudioSettingsAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureDeviceSwitchTuning()
        {
            if (_deviceSwitchTuning.HasValue) return;
            byte[] bytes = FetchBytes(DeviceSwitchTuningAddress);
            try
            {
                _deviceSwitchTuning = DeviceSwitchTuningCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 设备切换常量表 cooked 解码失败(address={DeviceSwitchTuningAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureTelemetryAnalytics()
        {
            if (_telemetryAnalytics.HasValue) return;
            byte[] bytes = FetchBytes(TelemetryAnalyticsAddress);
            try
            {
                _telemetryAnalytics = TelemetryAnalyticsCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 遥测参数旋钮表 cooked 解码失败(address={TelemetryAnalyticsAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureInteractionKinds()
        {
            if (_interactionKinds.HasValue) return;
            byte[] bytes = FetchBytes(InteractionKindsAddress);
            try
            {
                _interactionKinds = InteractionKindCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 交互种类契约表 cooked 解码失败(address={InteractionKindsAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureDiagnosisNegativeConfidence()
        {
            if (_negativeConfidence.HasValue) return;
            byte[] bytes = FetchBytes(DiagnosisNegativeConfidenceAddress);
            try
            {
                _negativeConfidence = DiagnosisNegativeConfidenceCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 阴性把握度定表 cooked 解码失败(address={DiagnosisNegativeConfidenceAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsurePrescriptionActions()
        {
            if (_prescriptionActions.HasValue) return;
            byte[] bytes = FetchBytes(PrescriptionActionsAddress);
            try
            {
                _prescriptionActions = PrescriptionActionsCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 处方表 cooked 解码失败(address={PrescriptionActionsAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureDiseaseActionAxis()
        {
            if (_diseaseActionAxis.HasValue) return;
            byte[] bytes = FetchBytes(DiseaseActionAxisAddress);
            try
            {
                _diseaseActionAxis = DiseaseActionAxisCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 处置轴契约表 cooked 解码失败(address={DiseaseActionAxisAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureOutcomeTable()
        {
            if (_outcomeTable.HasValue) return;
            byte[] bytes = FetchBytes(OutcomeTableAddress);
            try
            {
                _outcomeTable = OutcomeTableCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 53 判定表 cooked 解码失败(address={OutcomeTableAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureDelayTable()
        {
            if (_delayTable.HasValue) return;
            byte[] bytes = FetchBytes(DelayTableAddress);
            try
            {
                _delayTable = DelayTableCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 53 延迟表 cooked 解码失败(address={DelayTableAddress})—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureTutorialContent()
        {
            if (_tutorialContent.HasValue) return;
            byte[] bytes = FetchBytes(TutorialContentAddress);
            if (!TutorialContentTable.TryDecode(bytes, out TutorialContentTable table, out string error))
            {
                // 解码器返回 false 而非抛(它不知道调用方语境)⇒ 在此包成 E-13 启动期硬失败。
                throw new InvalidOperationException(
                    $"[E-13][data-core] 48 教学内容 cooked 解码失败(address={TutorialContentAddress})" +
                    $"—— 启动期硬失败,绝不降级:{error}");
            }
            _tutorialContent = table;
        }

        // ── S17-2 前置(2026-10-21):24 医馆三表 ──
        // 解码器一律**抛形**(表构造拒收已在 codec 内包成 InvalidDataException)⇒ 此处统一包 E-13。

        private void EnsureClinicContext()
        {
            if (_clinicContext.HasValue) return;
            byte[] bytes = FetchBytes(ClinicContextAddress);
            try
            {
                _clinicContext = ClinicMachineCookedCodec.ReadContext(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 24 CONTEXT_TABLE cooked 解码失败(address={ClinicContextAddress})" +
                    $"—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureClinicAdj()
        {
            if (_clinicAdj.HasValue) return;
            byte[] bytes = FetchBytes(ClinicAdjAddress);
            try
            {
                _clinicAdj = ClinicMachineCookedCodec.ReadAdj(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 24 ADJ_TABLE cooked 解码失败(address={ClinicAdjAddress})" +
                    $"—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        private void EnsureClinicRoom()
        {
            if (_clinicRoom.HasValue) return;
            byte[] bytes = FetchBytes(ClinicRoomAddress);
            try
            {
                _clinicRoom = ClinicMachineCookedCodec.ReadRoom(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 24 ROOM_TABLE cooked 解码失败(address={ClinicRoomAddress})" +
                    $"—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        // ── O-24-1(2026-10-21):21a 家具目录(FURN_TYPES 闭集)──

        private void EnsureClinicFurniture()
        {
            if (_clinicFurniture.HasValue) return;
            byte[] bytes = FetchBytes(FurnitureCatalogueAddress);
            try
            {
                _clinicFurniture = ClinicFurnitureCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 21a 家具目录 cooked 解码失败(address={FurnitureCatalogueAddress})" +
                    $"—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        // ── TD-building-01 读侧(2026-10-21):23 模块目录 ──

        private void EnsureBuildModules()
        {
            if (_buildModules.HasValue) return;
            byte[] bytes = FetchBytes(BuildModulesAddress);
            try
            {
                _buildModules = BuildModulesCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 23 模块目录 cooked 解码失败(address={BuildModulesAddress})" +
                    $"—— 启动期硬失败,绝不降级:{ex.Message}",
                    ex);
            }
        }

        /// <summary>取字节的唯一出口:注入缝优先,否则走 Addressables。
        /// 一切失败路径带 [E-13] 上下文 rethrow,绝不返回 null。</summary>
        private byte[] FetchBytes(string address) =>
            _byteSource == null ? FetchFromAddressables(address) : FetchFromByteSource(address);

        /// <summary>注入路径(EditMode 夹具)。校验与生产路径<b>同款</b> ——
        /// null / 空 / 短于头部一律 E-13 抛出,差别仅在于字节不来自 Addressables
        /// ⇒ 无「无 catalog」的 [Error] 日志污染。</summary>
        private byte[] FetchFromByteSource(string address)
        {
            byte[] bytes;
            try
            {
                bytes = _byteSource(address);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 启动期硬失败:装载 \"{address}\" 失败 —— 注入字节源抛出:{ex.Message}", ex);
            }

            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw new InvalidOperationException(
                    $"[E-13][data-core] 启动期硬失败:装载 \"{address}\" 失败 —— " +
                    $"产物字节为空或短于头部 {CookedFormat.HeaderSize} B");
            return bytes;
        }

        /// <summary>经 Addressables 取 TextAsset 字节;一切失败路径带 [E-13] 上下文 rethrow,绝不返回 null。</summary>
        private static byte[] FetchFromAddressables(string address)
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<TextAsset>(address);
                TextAsset asset = handle.WaitForCompletion(); // 启动期同步等(E-13:6.2+ 可能抛而非 null)
                if (asset == null)
                    throw new InvalidOperationException($"Addressables 返回 null(address={address})");
                byte[] bytes = asset.bytes;
                if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                    throw new InvalidOperationException(
                        $"产物字节为空或短于头部 {CookedFormat.HeaderSize} B(address={address})");
                return bytes;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"[E-13][data-core] 启动期硬失败:装载 \"{address}\" 失败 —— " +
                    "ADR-014 §五:装载异常不得 null 解引用、不得降级,直接终止启动。原因:" + ex.Message,
                    ex);
            }
        }
    }
}
