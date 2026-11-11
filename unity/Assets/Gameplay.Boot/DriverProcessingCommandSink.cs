// 炮制屏的意图出口实现(`TD-skeuo-06` · 2026-11-04)。
//
// 权威来源:
//   · `design/gdd/processing.md` 规则七(点火五步)· 规则八(发起即落流)
//   · ADR-013 §十-E(`ModalId.Processing` 第九员)· §9 C3(42 只渲染,永不持有游戏状态)
//   · ADR-025 §①(装配方向不可倒:42 看不见 Boot,故本实现必须住装配层)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本类修的是什么(债册原文)
// ─────────────────────────────────────────────────────────────────────────────
// `TD-skeuo-06`:`IProcessingCommandSink` 的**生产实现数为零** ⇒
// `Processing18Screen` 的 `_sink?.Ignite(...)` / `_sink?.Close()` 两次调用都是
// **`?.` 静默跳过**:玩家按下配方行,屏上无变化、无报错、无日志、**零 `Craft` 落流**。
// 本类是该接口的**第一个(也是唯一)生产实现**。
//
// ─────────────────────────────────────────────────────────────────────────────
// 两个动作
// ─────────────────────────────────────────────────────────────────────────────
// `Close()`  → `SkeuoRuntimeDriver.CloseModal()`(合上当前模态;与 `DriverPauseCommandSink.Resume` 同纪律)
// `Ignite()` → 组 `CraftIgnitionRequest` 并调 `CraftIgnitionTransaction.Process`
//              (18 的**唯一**点火事务入口;本类零结算逻辑 —— 五步判据全在 `Sim`)
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 三条如实登记(不借绿)
// ─────────────────────────────────────────────────────────────────────────────
// ① **环境 / 设备三分量:已接真值**(2026-11-11 `TD-processing-09` 结案轮;原文不删,保留旧态)。
//    旧态(2026-11-04 立本类时):三分量一律传增量中性 0,并打一条告警 —— 因为
//    24 的求值 Memoize 生产上零构造点、5 的 `EnvMod_raw` 无运行期取值链。
//    现态:`BootRoot` 经 `Func<WorldPos, ProcessingEnvMods>` 注入三件真值:
//      · `EquipMod` / `EnvMod_clinic` ← `ClinicEnvironmentMemo.GetDto()`(24)
//      · `EnvMod_climate` ← `EnvMod.ComputeEnvModDelta`(5;基线由 6 的生态区表查得)
//    ⚠️ 缝为 `null`(装配层没接世界 / 夹具未注入)时仍退回中性 0,并**当场告警** ——
//    降级必须是可观测的,不是静默的。
// ② **料位选中集不按配方过滤**:`ContainerSelectionStore` 只存「玩家点了哪几栈」,
//    并不知道这一剂要哪几味。准入门(`AdmissiblePredicate`)今日同样只看品级 min,
//    两者口径一致 —— 本条不在此处另造一份过滤(那就是第二真源),归 20 的规则六轮。
// ③ **单炉 / 容量 / 准入的业务拒绝不抛**:`Process` 以 `CraftIgnitionOutcome` 返回具名拒绝,
//    本类据此返回 `false`(屏上无变化 —— 正确语义:被拒的炉不该看起来像成功)。
//    只有**编程错误**(缺依赖 / 表版本不一致)才抛,那正是 fail-loud 该管的。

namespace DaYiJingCheng.Gameplay.Boot
{
    using System;
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
    using DaYiJingCheng.Sim;
    using DaYiJingCheng.Sim.Contracts;
    using DaYiJingCheng.Sim.Processing;
    using DaYiJingCheng.Sim.World;   // CraftEventWriter
    using UnityEngine;

    /// <summary>料位快照(实例 id 与品级**逐位对位**;两数组等长)。</summary>
    public readonly struct ProcessingMaterials
    {
        /// <summary>被点名的实例 id。</summary>
        public readonly long[] InstanceIds;

        /// <summary>与 <see cref="InstanceIds"/> 同序同长的品级集。</summary>
        public readonly int[] Qualities;

        /// <summary>构造。</summary>
        public ProcessingMaterials(long[] instanceIds, int[] qualities)
        {
            InstanceIds = instanceIds ?? Array.Empty<long>();
            Qualities = qualities ?? Array.Empty<int>();
        }
    }

    /// <summary>F1 的三个修正分量 —— 各自来自各自的系统(`TD-processing-09` 结案轮)。
    /// <para>三者都是**加法增量**(中性 0),与 `ProcessingInputs` 的形参一一对位;
    /// 18 侧**原样透传**,求和与钳制只在 21a F1(AC-18-06 / B-3)。</para>
    /// </summary>
    public readonly struct ProcessingEnvMods
    {
        /// <summary>24 的设备修正(`ClinicEnvDto.EquipMod`)。</summary>
        public readonly Fix EquipMod;

        /// <summary>5 的环境分量(`EnvMod.ComputeEnvModDelta`;cell = **炮制发生格**)。</summary>
        public readonly Fix EnvModClimate;

        /// <summary>24 的医馆分量(`ClinicEnvDto.EnvMod`)。</summary>
        public readonly Fix EnvModClinic;

        /// <summary>构造。</summary>
        public ProcessingEnvMods(Fix equipMod, Fix envModClimate, Fix envModClinic)
        {
            EquipMod = equipMod; EnvModClimate = envModClimate; EnvModClinic = envModClinic;
        }

        /// <summary>三分量中性(全 0)—— 与 F1 的加法域同数。
        /// <para>⚠️ 用 `default` 表达中性是**刻意**的:三个分量都是 `default(Fix) == 0`,
        /// 与构造出的中性逐位相同(承 F9:`default(struct)` 绕过构造器,但此处零值即中性,
        /// 不存在被绕过的不变量)。</para></summary>
        public static ProcessingEnvMods Neutral => default;
    }

    /// <summary>炮制屏的装配层实现(42 → 装配层的单向调用)。</summary>
    /// <remarks>
    /// <para>⚠️ <b>依赖面全部是委派</b>(器具格 / 料位 / 单件重量 / 载重):这些都住在别的系统里,
    /// 本类只消费其结果。委派同时让本类可在**不装载世界**的夹具里被驱动 ——
    /// 而「夹具能构造它」正是 `TD-skeuo-06` 当初逃逸的原因(接口零生产实现,夹具却全绿)。</para>
    /// </remarks>
    public sealed class DriverProcessingCommandSink : IProcessingCommandSink
    {
        private readonly SkeuoRuntimeDriver _driver;
        private readonly Func<RecipeDataSet> _recipeSource;
        private readonly Func<ProcessingMaterials> _materialsOf;
        private readonly Func<int, int, int> _unitWeightOfOutputLine;   // (recipeOrdinal, lineIndex) → weight
        private readonly Func<long> _carryLoad;
        private readonly Func<long, WorldPos?> _toolCellOf;
        private readonly ISkillLevelQuery _skillLevels;
        private readonly IReadOnlyList<SimEvent> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly CarryCapacity? _capacity;
        private readonly Func<long> _tick;
        private readonly int _actorId;
        private readonly CraftEventWriter _writer;

        /// <summary>三个修正分量的取值面(`null` ⇔ 未接 24 / 5 ⇒ 退回中性 0 并告警)。</summary>
        private readonly Func<WorldPos, ProcessingEnvMods> _envModsOf;

        /// <summary>本屏这次打开时选中的器具(structure_id);**未喂 ⇒ 点火拒发**。</summary>
        private long? _toolStructureId;

        /// <summary>三分量中性告警只打一次(避免每按一次刷一条)。</summary>
        private bool _neutralModsWarned;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentNullException">任一必填依赖为 null ——
        /// `null` 在这里等价于「静默空转」,而那正是本类要消灭的形态。</exception>
        public DriverProcessingCommandSink(
            SkeuoRuntimeDriver driver,
            Func<RecipeDataSet> recipeSource,
            Func<ProcessingMaterials> materialsOf,
            Func<int, int, int> unitWeightOfOutputLine,
            Func<long> carryLoad,
            Func<long, WorldPos?> toolCellOf,
            ISkillLevelQuery skillLevels,
            IReadOnlyList<SimEvent> events,
            IPayloadDecoder decoder,
            CarryCapacity? capacity,
            Func<long> tick,
            int actorId,
            CraftEventWriter writer,
            Func<WorldPos, ProcessingEnvMods> envModsOf = null)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _recipeSource = recipeSource ?? throw new ArgumentNullException(nameof(recipeSource));
            _materialsOf = materialsOf ?? throw new ArgumentNullException(nameof(materialsOf));
            _unitWeightOfOutputLine = unitWeightOfOutputLine
                ?? throw new ArgumentNullException(nameof(unitWeightOfOutputLine));
            _carryLoad = carryLoad ?? throw new ArgumentNullException(nameof(carryLoad));
            _toolCellOf = toolCellOf ?? throw new ArgumentNullException(nameof(toolCellOf));
            _skillLevels = skillLevels ?? throw new ArgumentNullException(nameof(skillLevels));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _capacity = capacity;
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
            _actorId = actorId;
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _envModsOf = envModsOf;
        }

        /// <summary>装配层在开屏时告知**这次**用的是哪件器具(`structure_id`)。</summary>
        /// <param name="structureId">器具的 `structure_id`(第四源 `Utensil` 的 stable id)。</param>
        public void SetTool(long structureId) => _toolStructureId = structureId;

        /// <inheritdoc/>
        public void Close() => _driver.CloseModal();

        /// <inheritdoc/>
        public bool Ignite(int recipeOrdinal)
        {
            if (!_toolStructureId.HasValue)
            {
                Debug.LogWarning("[炮制] 未告知器具 ⇒ 点火拒发(先 SetTool 再开屏)");
                return false;
            }

            WorldPos? toolCell = _toolCellOf(_toolStructureId.Value);
            if (!toolCell.HasValue)
            {
                Debug.LogWarning($"[炮制] 器具 structure_id={_toolStructureId.Value} 解析不出格 —— " +
                                 "点火拒发(F-5.4 的 ToolCell 无可解析格;不拿玩家格顶替)");
                return false;
            }

            if (!_capacity.HasValue)
            {
                Debug.LogWarning("[炮制] 容量上限未装配 ⇒ 点火拒发(不按「无限容量」放行)");
                return false;
            }

            RecipeDataSet set = _recipeSource();
            if (!TryFindRecipe(set.Recipes, recipeOrdinal, out Recipe recipe))
            {
                Debug.LogError($"[炮制] 配方 ordinal={recipeOrdinal} 不在配方表里 —— 屏与表版本不一致");
                return false;
            }

            // 料位 = 玩家在 20 屏点名的实例集;**对齐**的品级集来自同一趟遍历
            // (`ConsumedQualitiesOf` 单独调用会跳过已消失实例 ⇒ 两集合可能不等长)。
            ProcessingMaterials materials = _materialsOf();
            long[] instanceIds = materials.InstanceIds;
            int[] qualities = materials.Qualities;
            if (instanceIds.Length == 0 || instanceIds.Length != qualities.Length)
            {
                // 合法输入,但不是可发起的炉 —— 屏上不该有任何行才对吧?
                // 屏的过滤用的是同一份品级集,空集 ⇒ 零行;走到这里说明选中态在开屏后被清空。
                Debug.Log("[炮制] 选中集为空 ⇒ 点火拒发(未投料)");
                return false;
            }

            int minQuality = int.MaxValue;
            for (int i = 0; i < qualities.Length; i++)
                if (qualities[i] < minQuality) minQuality = qualities[i];

            // 🔁 2026-11-11(`TD-processing-09` 结案):三分量**真的来自各自系统**。
            //    `null` 缝(未接世界)才退回中性 0 —— 那是可观测的降级,不是常态。
            ProcessingEnvMods mods;
            if (_envModsOf != null)
            {
                mods = _envModsOf(toolCell.Value);
            }
            else
            {
                if (!_neutralModsWarned)
                {
                    _neutralModsWarned = true;
                    Debug.LogWarning("[炮制] 环境 / 设备三分量取不到值(装配层未接 24 的求值 Memoize " +
                                     "或 6 的生态区基线)⇒ 本次及后续点火一律按**增量中性 0** 传。");
                }
                mods = ProcessingEnvMods.Neutral;
            }

            var supply = new ProcessingInputs(
                recipe,
                _actorId,
                minQuality,
                mods.EquipMod,       // 24 的设备修正
                mods.EnvModClimate,  // 5 的环境分量(cell = 炮制发生格)
                mods.EnvModClinic);  // 24 的医馆分量

            long carryLoad = _carryLoad();

            int ordinal = recipeOrdinal;
            var request = new CraftIgnitionRequest(
                in supply,
                _tick(),
                ordinal,
                toolCell.Value,
                instanceIds,
                qualities,
                carryLoad,
                lineIndex => _unitWeightOfOutputLine(ordinal, lineIndex));

            CraftIgnitionOutcome outcome = CraftIgnitionTransaction.Process(
                in request,
                in set.Settlement,
                _skillLevels,
                _events,
                _decoder,
                _capacity.Value,
                ProcessingSettlementEntry.Solver,
                _writer);

            if (!outcome.Ignited)
            {
                // 业务拒绝是**正常结果**(不是异常):准入 / 单炉 / 容量。
                // 屏侧不改任何视觉状态 —— 这正是 `IProcessingCommandSink.Ignite` 的契约。
                Debug.Log($"[炮制] 点火被拒:recipe={recipe.RecipeId} 理由={outcome.Rejection}");
            }

            return outcome.Ignited;
        }

        /// <summary>按 `Ordinal` 找配方(找不到 ⇒ false;表为空 / null ⇒ false)。</summary>
        private static bool TryFindRecipe(Recipe[] recipes, int ordinal, out Recipe found)
        {
            found = default;
            if (recipes == null) return false;
            for (int i = 0; i < recipes.Length; i++)
            {
                if (recipes[i].Ordinal != ordinal) continue;
                found = recipes[i];
                return true;
            }
            return false;
        }
    }
}
