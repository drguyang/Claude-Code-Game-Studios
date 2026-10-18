// 权威来源:production/epics/audio-system/story-018-runtime-audio-assembly.md
//   · AC-018-3(组合根装配)· AC-018-5(A-5/A-7 混音器装载面)· AC-018-1(clip 解析缝)
// ADR-014 §五(Addressables 单组 + 首次 Step 前预载;E-13 硬失败)· ADR-018 §三(七总线)
// engine-reference `docs/engine-reference/unity/modules/audio.md`(AudioClip / AudioMixer 装载)
//
// ⚠️ **本类是 44 里唯一碰 `Addressables` 的音频类** —— 目的:把「资产装载」与
//   「渲染翻译」(`AudioSourceRenderTarget`)与「总线解析」(`AudioBusResolver`)分开,
//   使后两者在 EditMode 用假件直驱(不需要 catalog)。
//
// ⚠️ **address = bare 文件名**(与 `assets/data/audio_events.json` 的 `assets[]` 同形)——
//   `DataBakeMenu.EnsureAudioGroupCore` 逐条 `SetAddress(Path.GetFileName(path))`,
//   零映射代码(承 story-010 的裁定 ②)。
//
// ⚠️ **E-13 硬失败口径**:装载失败**不吞**。但 44 是表现层,「一条 wav 载不到」不应
//   炸掉整个启动 —— 素材存在性已由构建期 D6 门拦下(37/37 实测在位),运行期 miss =
//   装配错。⇒ 本类**记录** miss 并计数(可观测),不抛;混音器例外(见下)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>Addressables 承载的音频资产库(A-2 / A-5 / A-7 的装载面)。
    /// <para>职责三件:① cue ordinal → <see cref="AudioClip"/>(走事件表 `assets[]` 的 bare 名);
    /// ② cue ordinal → bus 名(事件表 `bus` 列,**单一真源**);
    /// ③ 混音器资产解析(A-7:混音器必须先进 Addressables 组,否则解析不到)。</para>
    /// <para>⚠️ 本类只**读**事件表,不写、不缓存第二份 bus 映射(ADR-024 §⑥)。</para>
    /// <para>⚠️ **不实现任何自定义项目接口** —— AC-44-B3 入口门只放行 `IAudioCueSink`;
    /// clip 解析缝走委托(`Func&lt;int, AudioClip&gt;`),见 `AudioSourceRenderTarget` 头注。</para>
    /// </summary>
    public sealed class AudioClipLibrary : IDisposable
    {
        private readonly AudioEventDataSet _table;
        private readonly Dictionary<int, AudioClip> _clips = new Dictionary<int, AudioClip>();
        private readonly List<UnityEngine.Object> _handles = new List<UnityEngine.Object>();
        private readonly HashSet<int> _missing = new HashSet<int>();
        private bool _disposed;

        /// <summary>构造(不装载 —— 装载由 <see cref="Preload"/> 显式发起)。</summary>
        /// <param name="table">音频事件表(非 null;`assets[]` 与 `bus` 的唯一真源)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="table"/> 为 null。</exception>
        public AudioClipLibrary(in AudioEventDataSet table)
        {
            _table = table;
            if (_table.RowCount == 0)
                throw new ArgumentException(
                    "音频事件表为空 —— 零行表会让所有 cue 解析不到 clip(空集冒充绿;" +
                    "构建期 D6/D9 门本应拦下)。", nameof(table));
        }

        /// <summary>已装载的 clip 数。</summary>
        public int LoadedCount => _clips.Count;

        /// <summary>装载失败的 cue ordinal 集合(可观测;非空 = 装配错或素材缺失)。</summary>
        public IReadOnlyCollection<int> MissingCues => _missing;

        /// <summary>逐行装载 clip(**首拍前**调用;幂等)。</summary>
        /// <param name="onError">单条失败回调(可选;null = 只计数)。</param>
        /// <returns>本次新装载成功的条数。</returns>
        public int Preload(Action<int, string, string> onError = null)
        {
            ThrowIfDisposed();
            int added = 0;

            for (int ordinal = 0; ordinal < _table.RowCount; ordinal++)
            {
                if (_clips.ContainsKey(ordinal)) continue;

                AudioEventRow row = _table.Row(ordinal);
                if (row.Assets == null || row.Assets.Length == 0)
                {
                    _missing.Add(ordinal);
                    onError?.Invoke(ordinal, row.Cue, "该行 assets[] 为空");
                    continue;
                }

                // 一行多素材(咳嗽 12 变体 / 世界事件 4 变体)—— P0 取**首条**:
                // 变体选择(VoiceVariantLib / SelectVariant)归 13 的语义侧,本类是装载面。
                // ⚠️ 这不是「随机挑一条」—— 是**确定性的第 0 条**,可重建。
                string address = row.Assets[0];
                AudioClip clip = LoadClip(address, out string error);
                if (clip == null)
                {
                    _missing.Add(ordinal);
                    onError?.Invoke(ordinal, row.Cue, error);
                    continue;
                }

                _clips[ordinal] = clip;
                _handles.Add(clip);
                added++;
            }
            return added;
        }

        /// <inheritdoc/>
        public AudioClip Resolve(int cue)
            => _clips.TryGetValue(cue, out AudioClip clip) ? clip : null;

        /// <summary>cue ordinal → 事件表 `bus` 列(null / 空串 ⇒ null;未知 ordinal ⇒ null)。</summary>
        /// <param name="cue">语义 cue id。</param>
        public string BusOf(int cue)
        {
            if (cue < 0 || cue >= _table.RowCount) return null;
            string bus = _table.Row(cue).Bus;
            return string.IsNullOrEmpty(bus) ? null : bus;
        }

        /// <summary>解析混音器资产(A-7)。</summary>
        /// <param name="address">混音器 Addressables address(默认 = 资产文件名)。</param>
        /// <returns>混音器;**解析不到 ⇒ null**(调用方决定 fail-loud 口径)。</returns>
        public AudioMixer LoadMixer(string address = AudioMixerAddress)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(address)) return null;

            // 预检同 LoadClip:缺 key 直装会打 [Error] ⇒ PlayMode 判「Unhandled log message」。
            if (!TryLocate<AudioMixer>(address)) return null;

            try
            {
                var handle = Addressables.LoadAssetAsync<AudioMixer>(address);
                AudioMixer mixer = handle.WaitForCompletion();
                if (mixer != null) _handles.Add(mixer);
                return mixer;
            }
            catch (Exception)
            {
                // 混音器是**装配面**资产:解析不到 = A-7 未补(组未登记)⇒ 调用方 fail-loud。
                // 这里只把异常转成 null,让「装配」而不是「装载」决定是否终止启动。
                return null;
            }
        }

        /// <summary>混音器的 Addressables address(= 资产文件名;与 `EnsureAudioGroupCore` 的
        /// `SetAddress(Path.GetFileName(path))` 同形)。</summary>
        public const string AudioMixerAddress = "DaYiJingCheng.mixer";

        private static AudioClip LoadClip(string address, out string error)
        {
            error = null;
            // ⚠️ **先判存在再装载**(承 `DataCorePreloader.TryPreload` 的预检纪律):
            //    对缺 key 直接 `LoadAssetAsync` 会让 Addressables 打一条 [Error],
            //    Unity Test Framework 把 [Error] 判成「Unhandled log message」⇒ PlayMode 整条红。
            //    预检只在 Locator 层失败,零日志。
            if (!TryLocate<AudioClip>(address))
            {
                error = $"地址不在 catalog 内(address={address})—— Addressables 组未登记或未重建";
                return null;
            }
            try
            {
                var handle = Addressables.LoadAssetAsync<AudioClip>(address);
                AudioClip clip = handle.WaitForCompletion();
                if (clip == null) error = $"Addressables 返回 null(address={address})";
                return clip;
            }
            catch (Exception ex)
            {
                error = $"装载失败(address={address}):{ex.Message}";
                return null;
            }
        }

        /// <summary>地址是否可被 Addressables 解析(**预检**,零日志)。
        /// <para>与 <c>DataCorePreloader.KeyLocatable</c> 同一手法,但按目标资源类型判定 ——
        /// 音频条目在 catalog 里的资源类型是 <see cref="AudioClip"/>,用 <c>TextAsset</c> 判会假阴。</para></summary>
        private static bool TryLocate<TAsset>(string address) where TAsset : UnityEngine.Object
        {
            try
            {
                foreach (var locator in Addressables.ResourceLocators)
                {
                    if (locator.Locate(address, typeof(TAsset), out _)) return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        /// <summary>释放全部装载句柄(池 / 场景卸载后调用)。幂等。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < _handles.Count; i++)
            {
                UnityEngine.Object asset = _handles[i];
                if (asset != null) Addressables.Release(asset);
            }
            _handles.Clear();
            _clips.Clear();
            _missing.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioClipLibrary));
        }
    }
}
