// 权威来源:production/epics/audio-system/story-018-runtime-audio-assembly.md
//   · AC-018-3(组合根装配)· AC-018-5(A-5/A-7 混音器装载面)· AC-018-1(clip 解析缝)
// ADR-014 §五(Addressables 单组 + 首次 Step 前预载;E-13 硬失败)· ADR-018 §三(七总线)
// engine-reference `docs/engine-reference/unity/modules/audio.md`(AudioClip / AudioMixer 装载)
//
// ⚠️ **本类是 44 里唯一碰 `Addressables` 的音频类** —— 目的:把「资产装载」与
//   「渲染翻译」(`AudioSourceRenderTarget`)与「总线解析」(`AudioBusResolver`)分开,
//   使后两者在 EditMode 用假件直驱(不需要 catalog)。
//
// ⚠️ **变体选择(story-020,2026-10-18 重裁)** —— 一行多素材时**全部**装,但**只有声明了
//   `variant_axis` 的行才是变体行**,取哪条由 `VoiceVariantLib.SelectVariantCube`(三维立方体)决定;
//   未声明的行恒取 `assets[0]`(它们的 assets[] 是**不同素材**,不是同一声音的变体)。
//   变体索引 i ↔ `assets[i]`(见 `Resolve(int,int,int,int)` 的 remarks)。本类仍是装载面。
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
        // 行序 → 该行**全部**已装变体(位置 = 变体索引;缺的素材留 null)。
        private readonly Dictionary<int, AudioClip[]> _clips = new Dictionary<int, AudioClip[]>();
        private readonly List<UnityEngine.Object> _handles = new List<UnityEngine.Object>();
        // 经注入装载缝装进来的 clip 的 `Addressables.Release` 包装(
        // ⚠️ Addressables.Release 收到它不认识的对象会打 [Error] ⇒ 测试判 Unhandled log;
        // 注入缝装载的对象本就不归 Addressables 管,释放只在缝内 `internalRelease` 里回调)。
        private readonly List<Action> _loaderReleases = new List<Action>();
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

        /// <summary>已装载的**行**数(每行至少一条素材装成才计;行内多素材只算一行)。</summary>
        public int LoadedCount => _clips.Count;

        /// <summary>装载失败的 cue ordinal 集合(可观测;非空 = 装配错或素材缺失)。</summary>
        public IReadOnlyCollection<int> MissingCues => _missing;

        /// <summary>逐行装载 clip(**首拍前**调用;幂等)。
        /// <para>⚠️ **行内全部** <c>assets[]</c> 都装(story-020):变体选择在
        /// <see cref="Resolve(int,int,int,int)"/> 里经 <see cref="VoiceVariantLib.SelectVariant"/>
        /// 按 (CueId × Tier × 桶 × 患者身份) 落定;本类仍是**装载面**,只负责「把可选的那些都装进来」。</para></summary>
        /// <param name="onError">单条失败回调(可选;null = 只计数)。一行内部分素材失败时
        /// 该行只要**至少一条**装载成功即不算缺(仍计数,便于观测)。</param>
        /// <param name="loader">地址 → clip 的装载缝(可选;null = 生产路径
        /// <see cref="LoadClip"/> → Addressables)。EditMode 无 catalog ⇒ 测选择面时注入假件,
        /// 使 <c>clip.name</c> 可断言「选中了哪条变体」。<b>经该缝装来的 clip 归缝方所有</b>:
        /// 本类在 <see cref="Dispose"/> 时调 <paramref name="internalRelease"/> 而非
        /// <c>Addressables.Release</c>。</param>
        /// <param name="internalRelease">释放缝方 clip 的回调(可选;null = 交由缝方自行释放)。</param>
        /// <returns>本次新装载成功的条数。</returns>
        public int Preload(Action<int, string, string> onError = null,
                           Func<string, AudioClip> loader = null,
                           Action<AudioClip> internalRelease = null)
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

                // 一行多素材(咳嗽 12 变体 / 世界事件 4 变体)—— 逐条装,**位置顺序即变体索引**
                // (variant index i ↔ assets[i];与 `VoiceVariantLib` 的整数索引同形,ADR-014)。
                // 缺的素材留 null 位置:选择器落到它时 `Resolve` 回退到行内首个已装变体
                // (素材存在性已由构建期 D6 门拦下,运行期 miss = 装配错,记数可观测)。
                var loaded = new AudioClip[row.Assets.Length];
                for (int v = 0; v < row.Assets.Length; v++)
                {
                    AudioClip clip;
                    string error;
                    if (loader != null)
                    {
                        clip = loader(row.Assets[v]);
                        error = $"装载缝返回 null(address={row.Assets[v]})";
                    }
                    else
                    {
                        clip = LoadClip(row.Assets[v], out error);
                    }
                    if (clip == null)
                    {
                        onError?.Invoke(ordinal, row.Cue, error);
                        continue;
                    }
                    loaded[v] = clip;
                    if (loader == null) _handles.Add(clip);
                    else if (internalRelease != null) _loaderReleases.Add(() => internalRelease(clip));
                    added++;
                }

                if (FirstLoaded(loaded) == null)
                {
                    _missing.Add(ordinal);
                    continue;
                }

                _clips[ordinal] = loaded;
            }
            return added;
        }

        /// <inheritdoc/>
        /// <remarks><b>语义(story-020 起)</b>:等价于 <c>Resolve(cue, cue, 0, 0)</c> ——
        /// 身份维取 0 的确定性选择。**单素材行**恒为该条;**变体行**不保证是 <c>assets[0]</c>;
        /// **未声明变体轴的多元素材行**恒为 <c>assets[0]</c>。
        /// 生产路径按条目的声源实体选变体,见 <see cref="ResolveCue(in AudioCueDto,int)"/>。</remarks>
        public AudioClip Resolve(int cue)
            // ⚠️ 非 DTO 查询路径(不解 cue 的强度语义)⇒ 强度取 0 = 弱桶,与接线前「恒第一个桶」同形。
            => Resolve(cue, cue, 0, 0, 0);

        /// <summary>cue × Tier × 患者身份 → clip(story-020 · AC-44-02 运行期半边)。
        /// <para><b>只有 <c>variant_axis</c> 已声明的行才走选择器</b>(story-020 重裁 ①):
        /// 变体行按三维立方体 <c>(桶 × 层内变体)</c> 选(<see cref="VoiceVariantLib.SelectVariantCube"/>,
        /// 纯整数确定性映射,无墙钟 / 无 <c>UnityEngine.Random</c>);<b>未声明的行恒取 <c>assets[0]</c></b>
        /// —— 世界声 / 脚步 / 听诊 / 翻页的 <c>assets[]</c> 是不同素材,按身份挑一条会播错东西。</para>
        /// <para>⚠️ <b>变体索引 → 素材位置</b>:事件表 <c>assets[]</c> 无索引列 ⇒
        /// 变体索引 i ↔ <c>assets[i]</c>(作者序即变体轴序;重排 = 换声音,构建期门管)。</para>
        /// <para>⚠️ 单素材行 / 越界 cue / 未装载 ⇒ null 或该条(非静默:调用方计 miss)。</para></summary>
        /// <param name="cue">cue ordinal(<c>AudioCueDto.Cue</c>;条目的循环 / 一次性身份)。</param>
        /// <param name="tableCueId">该 cue 在变体库里的 CueId(生产 = <paramref name="cue"/>;
        /// 单测可分家以复现「表里无行」路径)。</param>
        /// <param name="tier">精度档(0–2;设备级,本机恒 0)。</param>
        /// <param name="identity">患者身份(确定性输入;环境音取 cue 自身)。</param>
        /// <param name="intensity">强度(0..255;`AudioCueDto.Intensity`,决定 F-44.6 的桶 —— `TD-audio-034` ①)。</param>
        public AudioClip Resolve(int cue, int tableCueId, int tier, int identity, int intensity)
        {
            if (cue < 0 || cue >= _table.RowCount) return null;
            if (!_clips.TryGetValue(cue, out AudioClip[] loaded)) return null;

            AudioEventRow row = _table.Row(cue);
            AudioClip first = FirstLoaded(loaded);

            // ⚠️ story-020 重裁 ①(2026-10-18):**只有声明了 variant_axis 的行才是变体行**。
            // 未声明的多素材行(世界声 / 脚步 / 听诊 / 翻页 / 柜具)—— 它们的 assets[] 是
            // **不同的素材**,不是同一声音的变体:狗吠与锣声不是换了个说法,石头与雪不是。
            // 对它们做「按身份哈希挑一条」= 患者听到马而不是狗、翻页往后翻、雪地上响石头脚步。
            // ⇒ 恒取 assets[0](= 本类改造前的行为),语义轴的选择归上游(52 的 cue 解析 / 1 的地表)。
            if (row.Assets == null || row.Assets.Length <= 1 || !row.IsVariantRow) return first;

            // 变体行:三维立方体 (性别层 × 三桶 × 层内变体)。构建期 ValidateVariantAxis 已保证
            // 素材数整除三桶 ⇒ 每个格子非空 ⇒ **无死变体、无空桶坍陷**(重裁 ③)。
            int size = VoiceVariantLib.VariantSlotWidth(loaded.Length);
            int variant = VoiceVariantLib.SelectVariantCube(cue, tier, identity, size, intensity);

            AudioClip clip = variant >= 0 && variant < loaded.Length ? loaded[variant] : null;
            return clip ?? first;
        }

        /// <summary>条目的 clip 解析(<see cref="AudioSourceRenderTarget"/> 的说明路径):
        /// 身份取条目声源实体(环境音 <c>Source == -1</c> 取 cue 自身),Tier 取本机档 0。</summary>
        /// <param name="cueDto">上游条目(唯一下行入口 <c>IAudioCueSink</c> 的同一形状)。</param>
        /// <param name="tier">本机精度档(0–2)。</param>
        public AudioClip ResolveCue(in AudioCueDto cueDto, int tier = 0)
            => Resolve(cueDto.Cue, cueDto.Cue, tier,
                       cueDto.Source >= 0 ? cueDto.Source : cueDto.Cue,
                       cueDto.Intensity);

        // ══════════ 变体选择辅助(story-020 重裁,2026-10-18)══════════

        private static AudioClip FirstLoaded(AudioClip[] loaded)
        {
            for (int i = 0; i < loaded.Length; i++)
                if (loaded[i] != null) return loaded[i];
            return null;
        }

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
            for (int i = 0; i < _loaderReleases.Count; i++) _loaderReleases[i]();
            _loaderReleases.Clear();
            _clips.Clear();
            _missing.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioClipLibrary));
        }
    }
}
