// S14-6 · story-010 —— 素材 Addressables **组区分门**(AC-44-D5 的执行体)。
//
// 权威来源:
//   AC-44-D5:「素材本体 = `assets/audio/`(Addressables 流式),与 `data-core` 组区分
//              (组条目扫描断言;`Editor.Tools.Gates` 读 `AddressableAssetSettings`)」
//   ADR-014 §五(单一 `data-core` 组 + 首次 Step 前预载)· ADR-018 §五(素材流式,与数据烘焙组分家)
//   GDD design/gdd/audio-system.md :695「素材本体 = `assets/audio/`,与 data-core 数据烘焙组区分」
//
// ⚠️ **素材根 = `Assets/Audio/`(工程内)**,不是仓根 `assets/audio/`
//   (2026-10-16 用户裁定 ②;理由与订正记录见 `Editor.Tools.Bake.AudioAssetExistence` 文件头)。
//
// ⚠️ **两半分离**(承 `AudioEventTableGates` 的「纯函数 + 调用方 throw」形态):
//   ① <see cref="ValidateEntries"/> —— **纯谓词**,输入是 (组名, 资产路径) 对
//      ⇒ 负向夹具可注入混组条目,**不需要**真 Addressables 设置;
//   ② <see cref="ValidateSettings"/> —— 读**真** `AddressableAssetSettingsDefaultObject.Settings`
//      ⇒ 正向半边。story-010 原稿的 🔴 假绿正是「只做 ①、不做 ②」(自写谓词恒真)。
//
// ⚠️ **机器前置(与 `data-core` 同款)**:`unity/Assets/AddressableAssetsData/` 在
//   `.gitignore:85` 整目录忽略 ⇒ 每台机器首次须点一次菜单
//   `大医精诚/数据管线/确保 audio Addressables 组`。设置缺席时本门**报错**(不是跳过 ——
//   跳过 = 可选门 = 原稿 B1 的 `Assert.Ignore` 假绿翻版)。
//
// ⚠️ **判据是「分离」而不是「组叫什么名字」**:门断言 (a) 磁盘上的音频资产**各自**登记在
//   某个**非 `data-core`** 组;(b) `data-core` 组**零**条目指向 `Assets/Audio/`。
//   这比「断言存在一个叫 audio 的组」**更强且更不脆**(改名不假红;混组必红)。
//   <see cref="AudioGroupName"/> 只供菜单建组用,不参与判据。

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>一个 Addressables 条目(组名 + 资产路径)—— 纯谓词的输入形。</summary>
    public sealed class AudioGroupEntry
    {
        /// <summary>所属组名(如 <c>data-core</c> / <c>audio</c>)。</summary>
        public string GroupName;

        /// <summary>资产工程路径(如 <c>Assets/Audio/sfx_breath_base_loop_small.wav</c>)。</summary>
        public string AssetPath;

        /// <summary>构造。</summary>
        public AudioGroupEntry(string groupName, string assetPath)
        {
            GroupName = groupName; AssetPath = assetPath;
        }
    }

    /// <summary>素材 Addressables 组区分门(纯谓词 + 真设置读取两半)。</summary>
    public static class AudioAssetGroupGates
    {
        /// <summary>音频素材组名(菜单建组用;判据**不**依赖它)。</summary>
        public const string AudioGroupName = "audio";

        /// <summary>数据烘焙组名(与 `DataBakeMenu.DataCoreGroup` 同值 —— 门的判据面)。</summary>
        public const string DataCoreGroupName = "data-core";

        /// <summary>音频素材工程内目录(相对工程根;<see cref="UnityEngine.Application.dataPath"/> 的同级)。</summary>
        public const string AudioAssetDirName = "Audio";

        /// <summary>音频素材扩展名(扫盘用;P0 只有 wav)。</summary>
        public const string AudioAssetExtension = ".wav";

        // ══════════════ ① 纯谓词(负向夹具可注入)══════════════

        /// <summary>文件名是否为音频素材(**纯谓词,零 I/O**)。
        /// <para><b>本谓词是本仓 wav 扩展名判定的单一出处</b> ——
        /// `Editor.Tools.Bake.AudioImportSettingsGate.IsWavName` 委派到此处
        /// (装配引用为 `Bake → Gates` **单向**,故真源须住下层 `Gates`)。</para>
        /// <remarks>⚠️ <b>为什么存在(2026-10-17 实测,不是预防性设计)</b>:
        /// `Directory.GetFiles(dir, "*.wav")` 的 pattern **在 Linux 上区分大小写**。
        /// 独立 net8.0 探针实测:目录含 `lower.wav` / `UPPER.WAV` / `Mixed.Wav` 时,
        /// `GetFiles("*.wav")` **只返回 `lower.wav`**,另两个被**静默漏扫**。
        /// 而 CI 平台正是 Linux(ADR-012)⇒ 素材批若交付 `.WAV`,门会**少报**。
        /// <para>修法 = 枚举 `"*"` 后用本谓词按 <see cref="StringComparison.OrdinalIgnoreCase"/> 过滤。
        /// ⛔ **不得**改成 `"*.WAV"` —— 那只是把分叉挪到另一边。</para>
        /// <para>**拆谓词的第二收益**:夹具可注入文件名直断三形态,**不必碰文件系统**
        /// (承 `pipeline_gate_test.cs` 的「零文件写入」纪律)。</para></remarks>
        /// <param name="fileName">bare 文件名或路径(只取扩展名)。</param>
        /// <returns>扩展名等于 <see cref="AudioAssetExtension"/>(忽略大小写)⇒ true。</returns>
        public static bool IsAudioAssetName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            return string.Equals(Path.GetExtension(fileName), AudioAssetExtension,
                                 StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>磁盘路径集 → 工程内音频素材路径集(**纯函数,零 I/O**)。
        /// <para>职责三件:① 按 <see cref="IsAudioAssetName"/> 过滤(扩展名忽略大小写);
        /// ② 剔除编辑器临时文件(`.` 开头);③ 按 <see cref="StringComparer.Ordinal"/> 排序
        /// (错误顺序确定)。</para>
        /// <remarks>⚠️ <b>为何单拆(AC-44-D5b 的判据形态)</b>:该 AC 要的是
        /// 「同一目录在 Linux / Windows 下返回<b>同一集合</b>」—— 只有把映射做成纯函数,
        /// 夹具才能<b>注入 `.WAV` / `.Wav` 混写路径直断三形态均被返回</b>。
        /// 在盘上造副本做不到这件事:副本会同时进 D5 的期望集与 D6 的存在性判据(假红),
        /// 且违夹具「零文件写入」纪律(`test_discipline_noFilesystemWrites`)。</remarks></summary>
        /// <param name="filePaths">磁盘路径集(bare 名或全路径均可 —— 只取文件名)。</param>
        /// <returns>工程相对路径集(如 <c>Assets/Audio/x.wav</c>);null 入参 ⇒ 空集。</returns>
        public static IReadOnlyList<string> FilterAudioNames(IReadOnlyList<string> filePaths)
        {
            var found = new List<string>();
            if (filePaths == null) return found;

            var kept = new List<string>();
            for (int i = 0; i < filePaths.Count; i++)
                if (IsAudioAssetName(filePaths[i])) kept.Add(filePaths[i]);

            kept.Sort(StringComparer.Ordinal);                             // 确定性输出
            for (int i = 0; i < kept.Count; i++)
            {
                string name = Path.GetFileName(kept[i]);
                if (name.StartsWith(".", StringComparison.Ordinal)) continue;   // 编辑器临时文件
                found.Add("Assets/" + AudioAssetDirName + "/" + name);
            }
            return found;
        }

        /// <summary>组区分判据(空 = 通过)。**编排**:两半各自见下方私有方法。
        /// <para>(a) <paramref name="expectedAudioAssets"/> 的每一项必须登记在**非 `data-core`** 组;
        /// (b) `data-core` 组不得含任何 <c>Assets/Audio/</c> 下的条目。</para>
        /// <remarks>⚠️ **本方法刻意只做编排**(≤ 40 行 / 圈复杂度 &lt; 10,承 AC-44-D5c)——
        /// 两个判据半分别见 <see cref="CheckExpectedAssetsAreGrouped"/> 与
        /// <see cref="CheckDataCoreHasNoAudioEntries"/>。**拆分不得改变判定语义**(AC-44-D5d 钉死:
        /// 同一输入下错误集逐字相同)。</remarks></summary>
        /// <param name="entries">全部 (组名, 资产路径) 对。</param>
        /// <param name="expectedAudioAssets">期望已登记的音频资产工程路径集(见
        /// <see cref="AudioAssetsOnDisk"/>)。</param>
        public static IReadOnlyList<string> ValidateEntries(
            IReadOnlyList<AudioGroupEntry> entries, IReadOnlyList<string> expectedAudioAssets)
        {
            var errors = new List<string>();
            if (entries == null) { errors.Add("[D5] 条目集为 null —— 空集冒充绿"); return errors; }

            CheckExpectedAssetsAreGrouped(entries, expectedAudioAssets, errors);
            CheckDataCoreHasNoAudioEntries(entries, expectedAudioAssets, errors);
            return errors;
        }

        /// <summary>判据 (a):每个期望音频资产 ∈ 某个**非 `data-core`** 组。</summary>
        private static void CheckExpectedAssetsAreGrouped(
            IReadOnlyList<AudioGroupEntry> entries, IReadOnlyList<string> expectedAudioAssets,
            List<string> errors)
        {
            if (expectedAudioAssets == null) return;

            for (int i = 0; i < expectedAudioAssets.Count; i++)
            {
                string asset = expectedAudioAssets[i];
                string owner = null;
                bool inDataCore = false;

                for (int e = 0; e < entries.Count; e++)
                {
                    if (!string.Equals(entries[e].AssetPath, asset, StringComparison.Ordinal)) continue;
                    if (string.Equals(entries[e].GroupName, DataCoreGroupName, StringComparison.Ordinal))
                        inDataCore = true;
                    else if (owner == null) owner = entries[e].GroupName;
                }

                if (inDataCore)
                    errors.Add($"[D5] 音频素材「{asset}」登记在 `{DataCoreGroupName}` 组 —— " +
                               "AC-44-D5:素材与 `data-core` 数据烘焙组**必须区分**(混组 = 流式素材被打进数据包)");
                else if (owner == null)
                    errors.Add($"[D5] 音频素材「{asset}」未登记进任何 Addressables 组 —— " +
                               "AC-44-D5:素材须走 Addressables 流式(未登记 = 打进主包 / 不可寻址)。" +
                               $"跑菜单「确保 {AudioGroupName} Addressables 组」");
            }
        }

        /// <summary>判据 (b):`data-core` 组零 <c>Assets/Audio/</c> 条目。
        /// <para>与 (a) 的 <c>inDataCore</c> 互为冗余 —— 覆盖「不在期望集里、
        /// 但确实躺在 <c>Assets/Audio/</c> 下并被塞进 `data-core`」的**漏网条目**。</para></summary>
        private static void CheckDataCoreHasNoAudioEntries(
            IReadOnlyList<AudioGroupEntry> entries, IReadOnlyList<string> expectedAudioAssets,
            List<string> errors)
        {
            for (int e = 0; e < entries.Count; e++)
            {
                if (!string.Equals(entries[e].GroupName, DataCoreGroupName, StringComparison.Ordinal)) continue;
                if (!IsUnderAudioDir(entries[e].AssetPath)) continue;
                if (expectedAudioAssets != null && Contains(expectedAudioAssets, entries[e].AssetPath)) continue;
                errors.Add($"[D5] `{DataCoreGroupName}` 组含音频目录条目「{entries[e].AssetPath}」—— " +
                           "AC-44-D5:组区分(该文件不在期望集内,仍是混组)");
            }
        }

        /// <summary>资产工程路径是否位于音频素材目录下(接受 <c>Assets/Audio/...</c> 与绝对路径两种写法)。</summary>
        public static bool IsUnderAudioDir(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            string normalized = assetPath.Replace('\\', '/');
            return normalized.IndexOf("/Assets/" + AudioAssetDirName + "/", StringComparison.Ordinal) >= 0
                || normalized.StartsWith("Assets/" + AudioAssetDirName + "/", StringComparison.Ordinal);
        }

        // ══════════════ ② 真设置读取(正向半边)══════════════

        /// <summary>读**真** `AddressableAssetSettings` 并跑组区分判据(空 = 通过)。
        /// <para>设置不存在 / 音频目录为空 / 无任何 Addressables 条目 ⇒ 一条错误
        /// (**不跳过** —— 见文件头「机器前置」)。</para></summary>
        public static IReadOnlyList<string> ValidateSettings()
        {
            var errors = new List<string>();

            IReadOnlyList<string> expected = AudioAssetsOnDisk();
            if (expected.Count == 0)
                errors.Add($"[D5] 工程内音频目录无任何 {AudioAssetExtension} 素材 —— " +
                           "扫描键落空 = 假绿(AC-44-D6 的正向半边也依赖这些素材存在)");

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                errors.Add("[D5] AddressableAssets 设置不存在 —— 本机未初始化。" +
                           "跑一次 `Window > Asset Management > Addressables > Groups` 建设置," +
                           $"再跑菜单「确保 {AudioGroupName} Addressables 组」(与 `{DataCoreGroupName}` 同款机器前置;" +
                           "AddressableAssetsData 是 gitignored 的机器生成物)");
                return errors;
            }

            var entries = new List<AudioGroupEntry>();
            CollectEntries(settings, entries);
            if (entries.Count == 0)
            {
                errors.Add("[D5] Addressables 设置里零条目 —— 扫描键落空 = 假绿;" +
                           $"先跑菜单「确保 {DataCoreGroupName} Addressables 组」与「确保 {AudioGroupName} Addressables 组」");
                return errors;
            }

            errors.AddRange(ValidateEntries(entries, expected));
            return errors;
        }

        /// <summary>枚举真设置的全部 (组名, 资产路径) 对。</summary>
        public static IReadOnlyList<AudioGroupEntry> CollectEntries(AddressableAssetSettings settings)
        {
            var entries = new List<AudioGroupEntry>();
            CollectEntries(settings, entries);
            return entries;
        }

        private static void CollectEntries(AddressableAssetSettings settings, List<AudioGroupEntry> into)
        {
            if (settings?.groups == null) return;
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group?.entries == null) continue;
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry == null) continue;
                    into.Add(new AudioGroupEntry(group.Name, entry.AssetPath));
                }
            }
        }

        /// <summary>磁盘上 `Assets/Audio/` 的音频素材工程路径集(**真环境半边**:唯一做 I/O 的一行)。
        /// <remarks>⚠️ <b>W-2 修复(story-017 / AC-44-D5b,2026-10-17)</b>:原实现走
        /// <c>Directory.GetFiles(dir, "*" + AudioAssetExtension)</c>,而该 pattern
        /// <b>在 Linux 上区分大小写</b>(独立 net8.0 探针实测:目录含 `lower.wav` / `UPPER.WAV` /
        /// `Mixed.Wav` 时只返回 `lower.wav`)⇒ 素材批交付 `.WAV` 时本门<b>静默少报</b>。
        /// <para>现改为枚举 <c>"*"</c> 后交 <see cref="FilterAudioNames"/> 过滤
        /// (⛔ <b>不得</b>改成 <c>"*.WAV"</c> —— 那只是把分叉挪到另一边)。
        /// 映射半边是纯函数 ⇒ 混写夹具可**不经文件系统**直断(见该方法的 remarks)。</para>
        /// <para>同型缺陷的另一处在 <c>Editor.Tools.Bake/AudioImportSettingsGate.cs</c>,
        /// 已随本件一并修(判据真源 = <see cref="IsAudioAssetName"/>,该件委派)。</para></remarks></summary>
        public static IReadOnlyList<string> AudioAssetsOnDisk()
        {
            string dir = Path.Combine(UnityEngine.Application.dataPath, AudioAssetDirName);
            if (!Directory.Exists(dir)) return new List<string>();

            // ⚠️ 取 "*" 再交纯函数过滤 —— **不得**用 "*" + AudioAssetExtension(Linux 上区分大小写)。
            return FilterAudioNames(Directory.GetFiles(dir, "*"));
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], value, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
