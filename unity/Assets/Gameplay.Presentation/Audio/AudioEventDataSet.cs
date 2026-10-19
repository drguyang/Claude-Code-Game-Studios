// S14-6 · story-010 —— 音频事件表的**运行期载体**(烘焙产物的消费侧)。
//
// 权威来源:
//   AC-44-D4(事件表走 ADR-014 烘焙管线;玩家构建**零 JSON 解析器** —— 本类只吃 cooked 字节)
//   ADR-014 §五(cooked 产物经 Addressables `data-core` 组装载)
//   ADR-018 §一(44 只渲染不持游戏状态 —— 本类是**只读表**,不含任何可变状态)
//   GDD design/gdd/audio-system.md §Event Table Schema(:474-548)
//
// ⚠️ **cue ordinal 语义**(与 `Editor.Tools.Bake.AudioEventTableBinder` 同一裁定,两侧必须同读):
//   `AudioCueDto.Cue` 是 `int` 序号(`Sim.Contracts/PresentationDtos.cs`「解析在上游完成,
//   44 只收结果 cue」),而事件表行**无 ordinal 字段** ⇒ **ordinal = `rows[]` 行序**。
//   `Row(i)` / `CueAt(i)` / `TryGetOrdinal(name)` 三者是这层语义的唯一出入口。
//   ⚠️ 重排作者态 `rows[]` = 静默重编号 —— 缓解 = `ConfigVersion`(源内容哈希派生,ADR-014 §五)
//     随源变 ⇒ 存档头比对(ADR-010 §七)拒收跨版本流。**不是**本类能单独解决的问题。
//
// ⚠️ **本类零 `UnityEngine` 引用**(承 `AudioTuning` 同纪律)—— 纯数据载体,可在 EditMode
//   无引擎面直驱动;装载走 `AddressablesDataProvider`(那个类才碰 Addressables)。
// ⚠️ **本类不含 `AudioCueDto` / `IAudioCueSink` / `AudioCueHandle` / `TierSource` 四个 token**
//   (b5 逃逸段的扫描面 —— 不含即无「声明了 44 契约却住别处」的形态)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>事件表的一行(运行期只读形;字段与 <c>AudioEventTableRow</c> 同源但**不经 JSON**)。
    /// <para>null 语义与作者态一致:字段**缺失** = <c>null</c>;<c>SubtitleText</c> 空串 = 字段在但为空。</para></summary>
    public readonly struct AudioEventRow
    {
        /// <summary>cue id(如 <c>Voice_Cough_Damp</c>)。</summary>
        public readonly string Cue;

        /// <summary>行级 schema_version(0 = 缺失)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>是否循环 cue。</summary>
        public readonly bool Loop;

        /// <summary>混音总线字面量。</summary>
        public readonly string Bus;

        /// <summary>内容侧五类之一。</summary>
        public readonly string WhitelistCategory;

        /// <summary>时机侧闭枚举。</summary>
        public readonly string TriggerSource;

        /// <summary>节拍时钟引用(指向基础层行的 cue)。</summary>
        public readonly string ClockRef;

        /// <summary>字幕文本(null = 缺失,空串 = 在但空)。</summary>
        public readonly string SubtitleText;

        /// <summary><c>xfade_ms</c> 的原文(null = 缺失)。</summary>
        public readonly string XfadeRaw;

        /// <summary>素材文件名数组(null = 字段缺失)。</summary>
        public readonly string[] Assets;

        /// <summary>行级 per-tier 覆盖参数(键序 = 编码序;**禁含滤波列**由构建期门保证)。</summary>
        public readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> TierParams;

        /// <summary>是否带 <c>adventitious_policy</c>。</summary>
        public readonly bool HasPolicy;

        /// <summary><c>adventitious_policy.trigger_phase</c> 原文(null = 缺失)。</summary>
        public readonly string PolicyTriggerPhaseRaw;

        /// <summary>附加音层 <c>adventitious_policy.jitter</c> 原文(null = 缺失)。</summary>
        public readonly string PolicyJitterRaw;

        /// <summary><c>variant_axis</c>(null = **不是变体行**,<c>assets[]</c> 是并列素材而非变体集)。
        /// <para>story-020 重裁 ①(2026-10-18):只有声明了本字段的行才走变体选择;未声明的行
        /// 运行期恒取 <c>assets[0]</c>。合法值见 <c>AudioEventTableGates.VariantAxes</c>,
        /// 构建期由 <c>ValidateVariantAxis</c> 钉住。</para></summary>
        public readonly string VariantAxis;

        /// <summary>本行是否为**变体行**(<c>variant_axis</c> 已声明)。</summary>
        public bool IsVariantRow => VariantAxis != null;

        /// <summary>构造一行(读方唯一调用点)。</summary>
        public AudioEventRow(
            string cue, uint schemaVersion, bool loop, string bus,
            string whitelistCategory, string triggerSource, string clockRef,
            string subtitleText, string xfadeRaw, string[] assets,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tierParams,
            bool hasPolicy, string policyTriggerPhaseRaw, string policyJitterRaw,
            string variantAxis = null)
        {
            Cue = cue; SchemaVersion = schemaVersion; Loop = loop; Bus = bus;
            WhitelistCategory = whitelistCategory; TriggerSource = triggerSource; ClockRef = clockRef;
            SubtitleText = subtitleText; XfadeRaw = xfadeRaw; Assets = assets;
            TierParams = tierParams; HasPolicy = hasPolicy;
            PolicyTriggerPhaseRaw = policyTriggerPhaseRaw; PolicyJitterRaw = policyJitterRaw;
            VariantAxis = variantAxis;
        }
    }

    /// <summary>音频事件表的运行期只读表(烘焙产物的消费形)。
    /// <para><b>行序 = cue ordinal</b>;见文件头。</para></summary>
    public readonly struct AudioEventDataSet
    {
        /// <summary>cooked 产物文件名(= Addressables address;写方
        /// <c>Editor.Tools.Bake.DataBakeMenu</c> 与本常量共用 —— **单一出处**)。</summary>
        public const string CookedFileName = "audio_events.cooked.bytes";

        /// <summary>「无此 cue」哨兵(与 <c>PatientId.None = -1</c> 同款:负数不占用任何合法序号)。</summary>
        public const int NoOrdinal = -1;

        /// <summary>病人一次性语声 cue 的**名字**(story-018 · A-3 接线:按名解析 ordinal)。
        /// <para>⚠️ 名字的**存在性**由构建期门 <c>AudioEventTableGates.RequiredCues</c> 断言
        /// (该门逐字含此名);本常量只是运行期解析入口的入参,**不是**第二份白名单 ——
        /// 表里没有该名时 <see cref="TryGetOrdinal"/> 返回 false,调用方据此不发 cue(可观测)。</para></summary>
        public const string CoughCueName = "Voice_Cough_Damp";

        /// <summary>病人呼吸层基础循环 cue 的**名字**(同上;构建期门同款断言)。</summary>
        public const string BreathBaseCueName = "BreathLayer_Base_Calm";

        private readonly AudioEventRow[] _rows;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _tierMap;

        /// <summary>源 schema_version(头槽回声)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源数据集内容哈希派生的 ConfigVersion(ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造(读方唯一调用点)。</summary>
        public AudioEventDataSet(
            AudioEventRow[] rows,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tierMap,
            uint schemaVersion, uint configVersion)
        {
            _rows = rows; _tierMap = tierMap;
            SchemaVersion = schemaVersion; ConfigVersion = configVersion;
        }

        /// <summary>行数(= cue 序号的上界)。</summary>
        public int RowCount => _rows?.Length ?? 0;

        /// <summary>设备级滤波表(档位键 "0"/"1"/"2" → 列名 → 原文)。</summary>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> TierMap => _tierMap;

        /// <summary>按 ordinal 取行。</summary>
        /// <exception cref="ArgumentOutOfRangeException">ordinal 越界(调用方须先经
        /// <see cref="TryGetOrdinal"/> / 界内检查 —— 越界 = 上游解析错,静默返回 default 会藏 bug)。</exception>
        public AudioEventRow Row(int ordinal)
        {
            if (_rows == null || ordinal < 0 || ordinal >= _rows.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(ordinal), ordinal, $"cue ordinal 越界(表 {RowCount} 行)");
            return _rows[ordinal];
        }

        /// <summary>按 ordinal 取 cue 名(越界 ⇒ null —— 供跨侧等值断言报告差异,不抛)。</summary>
        public string CueAt(int ordinal)
            => _rows != null && ordinal >= 0 && ordinal < _rows.Length ? _rows[ordinal].Cue : null;

        /// <summary>cue 名 → ordinal(**线性扫描**:P0 表恒小;序号解析在**装载期一次性**完成,
        /// 不在每帧调用 —— 若将来表增大,改在装载期建索引,不改本契约)。</summary>
        /// <param name="cue">cue 名(Ordinal 比较)。</param>
        /// <param name="ordinal">出口:命中序号;未命中 = <see cref="NoOrdinal"/>。</param>
        public bool TryGetOrdinal(string cue, out int ordinal)
        {
            ordinal = NoOrdinal;
            if (string.IsNullOrEmpty(cue) || _rows == null) return false;
            for (int i = 0; i < _rows.Length; i++)
            {
                if (string.Equals(_rows[i].Cue, cue, StringComparison.Ordinal))
                {
                    ordinal = i;
                    return true;
                }
            }
            return false;
        }
    }
}
