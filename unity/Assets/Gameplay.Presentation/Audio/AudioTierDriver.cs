// 权威来源:user 裁定 2026-10-20(B 轮 · 「只接挡位 + 尾音」)· 承 story-004(AC-44-01 ②)
// GDD:design/gdd/audio-system.md F-44.1(:285-312)· 规则 5(:544-546 tier_map 键集 / 列集)
//   · :569 档位变化 ⇒ ramp ≥ 50 ms 平滑(F-44.1 注)· AC-44-01(:123 三档附加音层恒非零)
// ADR-018 §四 · TR-audio-005
//
// ⚠️ **本类是 `TierFilterDriver` 的「档位来源」接线(此前生产零构造 —— 接线空白)**:
//    每帧:读当前档(整数) → 档变了才 `SetTargets(tier_map 行)` → `Tick(Time.deltaTime)`。
//    `SetTargets` **只在档变化时调一次** —— 每帧调会把 FilterRamp 反复重置(永不到位)。
//
// ⚠️ **档位输入 = 只读委托(整数)**:本类**不读 sim、不推进任何状态**(ADR-018 §一 只渲染)。
//    诊断等级 → 档序由调用方给(组合根 = `DiagnosisReadFloorEvaluator.TierIndex(queryLevel)`)。
//
// ⚠️ **4 → 3 映射(✅ 2026-10-20 用户裁定 = 甲案「上钳」· `TD-audio-035` 子项结案)**:
//    诊断 `TierIndex` ∈ {0,1,2,3},`tier_map` 只有 "0"/"1"/"2"。GDD F-44.1 / 规则五原本**未写**
//    该映射;现裁定 = `min(TierIndex, 2)`:粗→低 · 中→中 · **细→高 · 满→高**。
//    **理据**:44 的「高档」定义就是「宽通带 + 低噪声底 + 全细节」,与 8 的「满 = 全通道满精度」
//    同义 ⇒ 满落高档不是信息损失。**设计后果(有意)**:听诊细节在诊断 Lv20(细档)饱和;
//    Lv50 的收益落在**判定**与脉案精度,不在耳朵上。GDD 补注见 `audio-system.md` F-44.1 段末。
//    **否决**:① 加第 4 条 `tier_map` 行(要改 GDD 三处硬规格 + 两条 AC 措辞);
//    ② 44 自设阈值(违 F-44.1 ②「44 不拥有数值」)。
//    ⚠️ 映射的**唯一落点** = `AudioTierMapping.ToMixerTier` —— 日后改判只改那一处。
//
// ⚠️ **缺列 / 解析失败 ⇒ 该档跳过 + 记错误**:不得用 0 值写进 mixer(0 Hz 通带 / 0 dB 是合法数值,
//    写进去会被当成真档位)。同档持续失败只记一次错误(每帧刷日志 = 日志淹没)。

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>档位驱动的 mixer 写入面(生产 = 包一层 <see cref="TierFilterDriver"/>;
    /// EditMode 注入记录型假件,可直接数 `SetTargets` 调用次数)。</summary>
    public interface ITierTargetSink
    {
        /// <summary>设置目标(<c>tier_map</c> 行五列;顺序见 <see cref="AudioTierDriver.SetTargets"/>)。</summary>
        void SetTargets(double centerHz, double widthHz, double noiseDb, double contactDb, double signalDb);

        /// <summary>推进 ramp。</summary>
        void Tick(double dtSeconds);

        /// <summary>是否仍有 ramp 在推进。</summary>
        bool IsRamping { get; }
    }

    /// <summary><see cref="ITierTargetSink"/> 的生产适配 —— 薄壳包 <see cref="TierFilterDriver"/>
    /// (不复写 ramp 逻辑;单一出处)。</summary>
    public sealed class TierFilterSinkAdapter : ITierTargetSink
    {
        private readonly TierFilterDriver _driver;

        /// <summary>包装既有档位滤波驱动(null ⇒ ArgumentNullException)。</summary>
        /// <param name="driver">滤波 / 噪声底 ramp 驱动。</param>
        public TierFilterSinkAdapter(TierFilterDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc/>
        public void SetTargets(double centerHz, double widthHz, double noiseDb, double contactDb, double signalDb)
            => _driver.SetTargets(centerHz, widthHz, noiseDb, contactDb, signalDb);

        /// <inheritdoc/>
        public void Tick(double dtSeconds) => _driver.Tick(dtSeconds);

        /// <inheritdoc/>
        public bool IsRamping => _driver.IsRamping;
    }

    /// <summary>诊断档序 → 听诊精度档(byte 0..2)的**单一落点**(映射已裁,见文件头注)。</summary>
    public static class AudioTierMapping
    {
        /// <summary>4 → 3:钳在高档(`min(TierIndex, 2)`)。
        /// <para>✅ **2026-10-20 用户裁定 = 甲案「上钳」**:粗→低 · 中→中 · 细→高 · 满→高。
        /// 理据与设计后果(GDD 补注)见文件头注;此处只记结论与落点唯一性。</para></summary>
        /// <param name="tierIndex">诊断档序(0..3;越界按边界钳)。</param>
        /// <returns>tier_map 档位(0..2)。</returns>
        public static int ToMixerTier(int tierIndex)
        {
            if (tierIndex < 0) return 0;
            return tierIndex > 2 ? 2 : tierIndex;
        }
    }

    /// <summary>听诊精度档位的**运行期驱动**(F-44.1 · AC-44-01 ②)。
    /// <para>每帧 <see cref="Tick"/>:档变 ⇒ `SetTargets(tier_map[档])`;之后每帧推进 ramp。</para></summary>
    public sealed class AudioTierDriver : IDisposable
    {
        private readonly Func<int> _tierIndexOf;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _tierMap;
        private readonly ITierTargetSink _sink;
        private readonly Action<string> _logError;

        private bool _started;
        private int _currentTier = -1;
        private int _failedTier = -1;
        private bool _disabled;

        /// <summary>构造。</summary>
        /// <param name="tierIndexOf">当前诊断档序(只读;整数 0..3;null ⇒ ArgumentNullException)。</param>
        /// <param name="tierMap">已烘焙表的 <c>tier_map</c>(键 "0"/"1"/"2";null ⇒ ArgumentNullException)。</param>
        /// <param name="sink">写入面(null ⇒ ArgumentNullException)。</param>
        /// <param name="logError">错误落点(委托,不是接口 —— 44 侧不新增项目接口面,AC-44-B3;null ⇒ <c>Debug.LogError</c>)。</param>
        public AudioTierDriver(Func<int> tierIndexOf,
                               IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tierMap,
                               ITierTargetSink sink, Action<string> logError = null)
        {
            _tierIndexOf = tierIndexOf ?? throw new ArgumentNullException(nameof(tierIndexOf));
            _tierMap = tierMap ?? throw new ArgumentNullException(nameof(tierMap));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _logError = logError ?? (m => Debug.LogError(m));
        }

        /// <summary>当前已生效的档(未启动 / 失败跳过 ⇒ -1)。</summary>
        public int CurrentTier => _currentTier;

        /// <summary>是否仍有 ramp 在推进(转发写入面)。</summary>
        public bool IsRamping => _sink.IsRamping;

        /// <summary><see cref="SetTargets"/> 被调用的次数(「档变一次、不是每帧」的观测面)。</summary>
        public int SetTargetsCallCount { get; private set; }

        /// <summary>是否已因 **mixer 句柄为 null** 被禁用(fail-loud 一次,不空转)。</summary>
        public bool IsDisabled => _disabled;

        /// <summary>被跳过的档解析失败次数(缺列 / 非数 / 非正)。</summary>
        public int FailedTierCount { get; private set; }

        /// <summary>每帧调:档变 ⇒ 设目标;恒定 ⇒ 只推 ramp。
        /// <para>⚠️ <b>不</b>每帧 `SetTargets` —— 那会重置 FilterRamp,ramp 永不到位。</para></summary>
        /// <param name="dtSeconds">帧时长。</param>
        public void Tick(double dtSeconds)
        {
            if (_disabled) return;

            int tier = AudioTierMapping.ToMixerTier(_tierIndexOf());
            if (tier != _currentTier)
            {
                if (!TryApply(tier, out int failed)) { _currentTier = -1; _failedTier = failed; }
                else _currentTier = tier;
            }
            _sink.Tick(dtSeconds);
        }

        /// <summary>由组合根在 mixer 就绪后调一次(之前 `Tick` 只推 ramp,不猜档、不写)。</summary>
        public void Start()
        {
            if (_disabled) return;
            _started = true;
        }

        /// <summary>mixer 句柄为 null ⇒ fail-loud **一次**并永久禁用(此后 `Tick` 空转)。</summary>
        /// <param name="reason">诊断文本(点名装配点)。</param>
        public void Disable(string reason)
        {
            if (_disabled) return;
            _disabled = true;
            _logError("[AudioTierDriver] " + reason + " —— 档位滤波驱动已禁用(不再写任何 mixer 参数)");
        }

        /// <summary>组合根读 `PlayerBusVolumeInitializer.MixerResolver` 后调:
        /// null ⇒ <see cref="Disable"/>;非 null ⇒ <see cref="Start"/> 并返回 `true`。</summary>
        /// <param name="mixerAvailable">mixer 句柄是否拿得到。</param>
        /// <returns>可继续驱动 ⇒ true。</returns>
        public bool BindMixer(bool mixerAvailable)
        {
            if (!mixerAvailable)
            {
                Disable("mixer 句柄为 null(PlayerBusVolumeInitializer.MixerResolver 未接 / 返回 null)");
                return false;
            }
            Start();
            return !_disabled;
        }

        /// <summary>解析 `tier_map[tier]` 五列并下发;失败 ⇒ 跳过 + 记错误(同档只记一次)。</summary>
        private bool TryApply(int tier, out int failed)
        {
            failed = -1;
            if (!_started) return true;              // 未启动:不算失败,也不写
            if (!TryReadRow(tier, out double center, out double width,
                            out double noise, out double contact, out double signal))
            {
                FailedTierCount++;
                if (_failedTier != tier)
                {
                    _failedTier = tier;
                    _logError("[AudioTierDriver] tier_map[\"" + tier + "\"] 缺列 / 解析失败 —— 本档跳过,不写 mixer" +
                               "(列集 = AudioEventTableGates.TierMapColumns)");
                }
                failed = tier;
                return false;
            }
            _sink.SetTargets(center, width, noise, contact, signal);
            SetTargetsCallCount++;
            return true;
        }

        /// <summary>读一行五列(列名**引用** <see cref="TierColumnNames"/>,不另抄字面量)。</summary>
        private bool TryReadRow(int tier, out double center, out double width,
                                out double noise, out double contact, out double signal)
        {
            center = width = noise = contact = signal = 0.0;
            if (!_tierMap.TryGetValue(tier.ToString(CultureInfo.InvariantCulture), out var row) || row == null)
                return false;
            return TryNum(row, TierColumnNames.Center, out center) && center > 0.0
                && TryNum(row, TierColumnNames.Width, out width) && width > 0.0
                && TryNum(row, TierColumnNames.Noise, out noise)
                && TryNum(row, TierColumnNames.Contact, out contact)
                && TryNum(row, TierColumnNames.Signal, out signal);
        }

        private static bool TryNum(IReadOnlyDictionary<string, string> row, string column, out double value)
        {
            value = 0.0;
            return row.TryGetValue(column, out string raw) && raw != null
                && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>第 6 列 <c>band_detail_count</c> 不进 mixer:F-44.1 的 mixer 暴露参数恰 5 个
        /// (<see cref="MixerRegistry.TierFilterParameters"/>),频段细节是听诊元件侧的计数输入。</summary>
        public static class TierColumnNames
        {
            /// <summary><c>passband_center_hz</c>。</summary>
            public const string Center = "passband_center_hz";
            /// <summary><c>passband_width_hz</c>。</summary>
            public const string Width = "passband_width_hz";
            /// <summary><c>noise_floor_db</c>。</summary>
            public const string Noise = "noise_floor_db";
            /// <summary><c>contact_noise_floor_db</c>。</summary>
            public const string Contact = "contact_noise_floor_db";
            /// <summary><c>signal_db</c>。</summary>
            public const string Signal = "signal_db";
        }

        /// <inheritdoc/>
        public void Dispose() => _disabled = true;
    }
}
