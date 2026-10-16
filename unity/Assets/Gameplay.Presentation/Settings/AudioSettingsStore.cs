// 权威来源:production/epics/audio-system/story-011-settings-exposure-reset.md(AC-44-19 归零三步)
// GDD:design/gdd/audio-system.md AC-44-19(:1153-1159)· §Tuning Knobs 落点裁决(:707-716)
//   · 「GIVEN 设置 store 与 44 出厂默认集(ADR-014 烘焙的 44 出厂分区),WHEN 执行归源
//      (页级归源钮 → store 三步:写默认 → 推 mixer → **落 sidecar**),THEN ① store == 出厂集;
//      ② **重启后仍 == 出厂集**(缺第三步则坏值被 sidecar 推回 = 归源无效);③ 负向:跳过 sidecar 落盘 ⇒ 断言失败」
// ADR-018 §一(44 只渲染不持值 —— store 单向推送 mixer)· ADR-013(控件呈现归 42)
// OQ-SS-5=甲(端本地 sidecar)· OQ-SS-7=甲(归零三步 = 写默认 → 推 mixer → 落 sidecar)
//
// ⚠️ **持有者指定**(OQ-SS-5 涟漪):设置值的跨会话持有者 = **边界层设置 store** ——
//    42 壳不持(零缓存宪法 AC-42-G2)、44 不持(只渲染)、sim 无关。
//    ⇒ 本类住 `Gameplay.Presentation/Settings/`(边界层),**禁住 `Sim`**。
// ⚠️ **「写默认」的真源 = 注册表**(ADR-014 烘焙分区),**不是 mixer** ——
//    OQ-SS-7 ①逐字:「默认值真源 = ADR-014 烘焙的 44 出厂分区(mixer 非真源)」。
//    本类一律经 <see cref="AudioSettingsRegistry"/> 取默认,禁硬编码数值。
// ⚠️ **「推 mixer」是单向推送**(ADR-018 §一):store 写 mixer 参数,mixer 永不回写 store。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Settings
{
    /// <summary>归零三步的执行结果(AC-44-19 的可诊断面;任一步失败都须可归因,不静默)。</summary>
    public readonly struct ResetOutcome
    {
        /// <summary>三步是否**全部**成功(AC-44-19:缺一步 = 归源无效)。</summary>
        public readonly bool Succeeded;

        /// <summary>失败原因(null = 成功);成功时为空列表。</summary>
        public readonly IReadOnlyList<string> Errors;

        /// <summary>构造。</summary>
        public ResetOutcome(bool succeeded, IReadOnlyList<string> errors)
        {
            Succeeded = succeeded;
            Errors = errors ?? Array.Empty<string>();
        }
    }

    /// <summary>「推 mixer」的注入缝(承 <c>PlayerBusVolumeInitializer.MixerResolver</c> 同款模式)。
    /// <para>入参 = (参数名, dB 值);返回错误列表(空 = 全部推送成功)。
    /// **未注入 = 推 mixer 这一步不可执行** —— 归零据此**判失败**(不假装成功:
    /// AC-44-19 逐字「归零三步缺一无效」)。</para></summary>
    public delegate IReadOnlyList<string> MixerPush(IReadOnlyList<KeyValuePair<string, double>> dbValues);

    /// <summary>设置 store —— 设置值的**运行期真源**(OQ-SS-5 涟漪的持有者指定)。
    /// <para>本类持值(<c>_values</c>)+ 落盘(委派 <see cref="AudioSettingsSidecar"/>)+ 推 mixer
    /// (委派注入的 <see cref="MixerPush"/> 缝)。**42 壳读它、44 渲染它** —— 两侧都不持值。</para></summary>
    public sealed class AudioSettingsStore : ISettingsStore
    {
        private readonly AudioSettingsSidecar _sidecar;
        private readonly Dictionary<string, SettingsValue> _values = new Dictionary<string, SettingsValue>(StringComparer.Ordinal);

        /// <summary>推 mixer 的注入缝(story-010 装载面接线前为 null ⇒ 归零判失败)。</summary>
        public MixerPush MixerPushSink;

        /// <summary>以 sidecar 目录构造;初始值 = 出厂默认集(注册表)。</summary>
        /// <param name="sidecarDirectory">sidecar 目录(空 / null 抛 <see cref="ArgumentException"/>)。</param>
        public AudioSettingsStore(string sidecarDirectory)
        {
            SidecarDirectory = sidecarDirectory;
            _sidecar = new AudioSettingsSidecar(sidecarDirectory);
            LoadFactoryDefaults();
        }

        /// <summary>sidecar 目录(诊断用)。</summary>
        public string SidecarDirectory { get; }

        /// <summary>闭集签名(注册表参数名派生;供 sidecar 头比对)。</summary>
        public static string RegistrySignature() =>
            AudioSettingsSidecar.ComputeRegistrySignature(AudioSettingsRegistry.ParamNames);

        /// <summary>把全部注册表行**重置为出厂默认**(归零第一步的纯内存半;不落盘、不推 mixer)。
        /// <para>⚠️ 只遍历**注册表行集** —— 遍历顺序 = 注册表序(确定性)。</para></summary>
        public void LoadFactoryDefaults()
        {
            _values.Clear();
            IReadOnlyList<SettingsRegistryRow> rows = AudioSettingsRegistry.Rows;
            for (int i = 0; i < rows.Count; i++)
                _values[rows[i].Param] = rows[i].FactoryDefault;
        }

        /// <summary>读一个设置值;未登记参数 ⇒ false(调用方据此判「越集」)。</summary>
        public bool TryGet(string param, out SettingsValue value) => _values.TryGetValue(param, out value);

        /// <summary>写一个设置值(**仅限注册表内的参数** —— 越集抛
        /// <see cref="ArgumentOutOfRangeException"/>:壳直写非注册表参数是控制清单的 Forbidden 项)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">参数 ∉ 注册表闭集。</exception>
        public void Set(string param, SettingsValue value)
        {
            if (!AudioSettingsRegistry.TryGetRow(param, out _))
                throw new ArgumentOutOfRangeException(nameof(param),
                    $"「{param}」∉ settings_visible 闭枚举 —— 壳消费集 ⊆ 注册表(注册表 AC①:越集 = 断言失败)");
            _values[param] = value;
        }

        /// <summary>当前值集(**只读快照**;按参数序数排序 ⇒ 确定性比对)。</summary>
        public IReadOnlyDictionary<string, SettingsValue> Snapshot()
        {
            var sorted = new SortedDictionary<string, SettingsValue>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, SettingsValue> pair in _values)
                sorted[pair.Key] = pair.Value;
            return sorted;
        }

        /// <summary>当前值集是否**逐键等于**出厂默认集(AC-44-19 ① 的判据本体)。</summary>
        public bool EqualsFactoryDefaults()
        {
            IReadOnlyList<SettingsRegistryRow> rows = AudioSettingsRegistry.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!_values.TryGetValue(rows[i].Param, out SettingsValue current)) return false;
                if (!current.Equals(rows[i].FactoryDefault)) return false;
            }
            // 反向:不得有注册表外的残留键(否则「== 出厂集」是半真)
            foreach (string key in _values.Keys)
                if (!AudioSettingsRegistry.TryGetRow(key, out _)) return false;
            return true;
        }

        /// <summary>**归零三步**(AC-44-19 / OQ-SS-7 ②):写默认 → 推 mixer → 落 sidecar。
        /// <para>⚠️ **缺一无效** —— 任何一步失败 ⇒ <see cref="ResetOutcome.Succeeded"/> = false,
        /// 且**不掩盖**失败(不静默、不部分成功)。尤其:第三步(sidecar)失败时,
        /// 内存态虽已归零,但下次启动会被 sidecar 的坏值推回 ⇒ 归源**无效**。</para></summary>
        /// <returns>三步执行结果。</returns>
        public ResetOutcome ResetToFactory()
        {
            var errors = new List<string>();

            // ── 第一步:写默认(真源 = 注册表,非 mixer)──
            LoadFactoryDefaults();
            if (!EqualsFactoryDefaults())
                errors.Add("归零第一步失败:写入默认后 store ≠ 出厂集(注册表行集与写回路径不一致)");

            // ── 第二步:推 mixer(单向;未注入缝 = 该步不可执行 ⇒ 失败)──
            MixerPush push = MixerPushSink;
            if (push == null)
            {
                errors.Add("归零第二步失败:推 mixer 缝未注入(step-2 不可执行)—— " +
                           "AC-44-19「归零三步缺一无效」,不以「跳过」冒充成功");
            }
            else
            {
                IReadOnlyList<string> pushErrors = push(BuildDbPushList());
                if (pushErrors != null && pushErrors.Count > 0)
                    errors.AddRange(pushErrors);
            }

            // ── 第三步:落 sidecar(缺它则坏值被 sidecar 推回 = 归源无效)──
            bool written = _sidecar.Save(_values, RegistrySignature());
            if (!written)
                errors.Add("归零第三步失败:sidecar 未落盘(" +
                           (_sidecar.LastWriteError ?? "未知原因") +
                           ")—— AC-44-19 ②:缺第三步则坏值被 sidecar 推回,归源无效");

            return new ResetOutcome(errors.Count == 0, errors);
        }

        /// <summary>从 sidecar 载入(启动路径)。签名失配 / 半截 ⇒ 维持出厂默认 + 陈旧件改名备份
        /// (承 3 规则四/五)。<c>NoSidecar</c>(首次启动)也是正常路径,不报错。</summary>
        /// <remarks>⚠️ **先校验后落值**(原子性):载荷在**候选表**上完整校验通过才整体换入
        /// <c>_values</c> —— 否则半截载荷会让 store 停在「一半旧值一半默认」的中间态,
        /// 且失配出口**不再维持出厂默认**(2026-10-16 由 `test_sidecar_truncatedPayload_rejected`
        /// 当场抓到的实现缺陷;原式先 <c>Clear</c> 再逐键写入,缺键时才 return ⇒ 已写进去的不回滚)。</remarks>
        /// <returns>读序出口(见 <see cref="SettingsLoadResult"/>)。</returns>
        public SettingsLoadResult LoadFromSidecar()
        {
            SettingsLoadResult result = _sidecar.Load(RegistrySignature(), out IReadOnlyDictionary<string, SettingsValue> loaded);
            if (result != SettingsLoadResult.Applied) return result;

            // ── 候选表:先在**不动 _values** 的前提下做两项完整性校验 ──
            var candidate = new Dictionary<string, SettingsValue>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, SettingsValue> pair in loaded)
            {
                // 只接受注册表内的键(旧版 sidecar 可能含已删参数 ⇒ 静默丢弃会掩盖结构漂移,
                // 故一并按失配处理:值集 ≠ 注册表 = 结构已变)
                if (!AudioSettingsRegistry.TryGetRow(pair.Key, out _)) return SettingsLoadResult.Mismatch;
                candidate[pair.Key] = pair.Value;
            }
            // 键集须恰 = 注册表集(缺键 = 半截 sidecar)
            foreach (string param in AudioSettingsRegistry.ParamNames)
                if (!candidate.ContainsKey(param)) return SettingsLoadResult.Mismatch;

            // ── 校验全过 ⇒ 整体换入(原子) ──
            _values.Clear();
            foreach (KeyValuePair<string, SettingsValue> pair in candidate)
                _values[pair.Key] = pair.Value;

            return SettingsLoadResult.Applied;
        }

        /// <summary>把当前总线音量值换算成 dB 推送列表(线性 → dB 的换算单点在
        /// <see cref="VolumeUnitMapping"/>;本方法不自行做 <c>Math.Log10</c>)。</summary>
        private IReadOnlyList<KeyValuePair<string, double>> BuildDbPushList()
        {
            IReadOnlyList<SettingsRegistryRow> rows = AudioSettingsRegistry.Rows;
            var list = new List<KeyValuePair<string, double>>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Kind != SettingsValueKind.Float) continue;   // mono 非 mixer 参数(见缺口登记)
                if (!_values.TryGetValue(rows[i].Param, out SettingsValue v)) continue;
                list.Add(new KeyValuePair<string, double>(rows[i].Param, VolumeUnitMapping.LinearToDb(v.Float)));
            }
            return list;
        }
    }
}
