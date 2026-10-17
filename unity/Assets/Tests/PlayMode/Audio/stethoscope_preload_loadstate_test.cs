// S14-6 补口(2026-10-17)—— 听诊预载的 **Unity 侧真值半边**(EditMode 结构上不可达的那一半)。
//
// 权威来源:
//   GDD design/gdd/audio-system.md §Edge Cases 素材段 :640
//     「一次性 cue 允许极小迟发,但**不得**在听诊中因加载迟发(听诊窗预载)」
//   AC「听诊窗预载」:进入 `StethoscopeFocus` 前听诊族 cue `loadState == Loaded`(未就绪不进听诊)
//   engine-reference `docs/engine-reference/unity/modules/audio.md:224-234`
//   ADR-018 §一(44 只渲染)—— 本件只**读**载入态,不改变任何声音状态
//
// ⚠️ **本件补的是哪一半**(承 EditMode `pipeline_gate_test.cs` 的原缺口哨兵):
//   EditMode 侧已覆盖「查询缝的四态语义 + 族集派生 + 接线 + 适配器纯映射」;
//   **未覆盖** = 「真 `AudioClip.loadState` 的过渡(`Unloaded→Loading→Loaded/Failed`)」——
//   它需要 PlayMode + 真导入 + 真 `LoadAudioData()`,EditMode 结构上跑不到。
//   本件即该半边的载体。**原哨兵已按自身口径转成真断言,不是被当回归修掉。**
//
// ⚠️ **为什么不自建假 clip**:承 story-010 的病根 —— 自建夹具恒真、生产链路一次没跑过。
//   本件全程打真物:
//     表   ← `Assets/DataCooked/audio_events.cooked.bytes`(**真烘焙产物**,由 `AudioEventCookedCodec` 解码)
//     素材 ← `Assets/Audio/*.wav`(**真导入素材**,`AssetDatabase.LoadAssetAtPath<AudioClip>`)
//     载入态 ← `AudioClip.loadState`(**Unity 真值**)
//
// ⚠️ **为什么刻意不经 Addressables**(2026-10-17 实测决定):
//   首版走 `AddressablesDataProvider.Load<AudioEventDataSet>()`,在本机**红**:
//   `InvalidKeyException: No Location found for Key=audio_events.cooked.bytes` ——
//   根因 = PlayMode 的 Addressables 解析的是 `unity/Library/com.unity.addressables/aa/Linux/catalog.bin`
//   这份**预建 catalog**(实测时间戳 2026-10-17 05:17,`strings` 显示含旧 5 条 cooked + 5 个 .wav,
//   **不含**新烘的两条)。即:走 Addressables 会把本件绑死在**机器本地的预建产物**上 ——
//   正是 `TD-medcons-09` 那一族的坑(测试依赖机器本地状态,CI 布局变化即红)。
//   ⇒ 改为**自足读法**:表读真产物文件,clip 读真导入资产。两者都在仓库里,任何 clone 都成立。
//   ⚠️ 代价如实登记:**本件不覆盖 Addressables 装载链**。那条链的载体是
//   `AddressablesDataProvider` 自己的测试 + `data-core` 组条目断言,不在本件面内。
//
// ⚠️ **前置**:`unity/Assets/DataCooked/audio_events.cooked.bytes` 须已烘焙(菜单「烘焙 audio_events」)。
//   缺失 ⇒ 本件**红**(不 Skip)—— 那是真缺陷(ADR-014 §五 的启动期预载依赖它),
//   不得以 Ignore 掩盖。承「禁借绿」。

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DaYiJingCheng.Tests.PlayMode.Audio
{
    /// <summary>
    /// 听诊窗预载 —— 真 <see cref="AudioClip.loadState"/> 的过渡与门响应。
    /// </summary>
    public class StethoscopePreloadLoadStateTest
    {
        /// <summary>载入态轮询超时(秒)。素材本体最大 2 MB,decompress-on-load 应远快于此。</summary>
        private const float LoadTimeoutSeconds = 30f;

        /// <summary>素材根(工程相对)。与 `AudioAssetGroupGates.AudioAssetDirName` 同源。</summary>
        private const string AudioDirRel = "Assets/Audio";

        // ══════════════ 夹具:真产物表 → 听诊族 → 真 clip ══════════════

        /// <summary>读**真烘焙产物**并解码(不经 Addressables —— 见文件头「为什么刻意不经 Addressables」)。</summary>
        private static AudioEventDataSet LoadRealTable()
        {
            string path = Path.Combine(
                Application.dataPath, "DataCooked", AudioEventDataSet.CookedFileName);

            Assert.That(File.Exists(path), Is.True,
                $"真烘焙产物缺失:{path} —— 先跑菜单「大医精诚/数据管线/烘焙 audio_events」。" +
                "本件**不 Skip**:ADR-014 §五 的启动期预载依赖该产物,缺失即真缺陷。");

            return AudioEventCookedCodec.Read(File.ReadAllBytes(path));
        }

        /// <summary>听诊族 cue → 素材文件名(取 `assets[]` 首项;真种子每行恰 1 个)。</summary>
        private static List<KeyValuePair<string, string>> StethoscopeRows(AudioEventDataSet table)
        {
            var rows = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < table.RowCount; i++)
            {
                AudioEventRow row = table.Row(i);
                if (!string.Equals(row.Bus, StethoscopePreloadGate.StethoscopeBus, StringComparison.Ordinal))
                    continue;
                Assert.That(row.Assets, Is.Not.Null.And.Not.Empty,
                    $"听诊族行 '{row.Cue}' 无 assets[] —— 无法定位素材");
                rows.Add(new KeyValuePair<string, string>(row.Cue, row.Assets[0]));
            }
            return rows;
        }

        /// <summary>按素材文件名取**真导入 clip**。</summary>
        private static AudioClip LoadRealClip(string assetFileName)
        {
            string assetPath = AudioDirRel + "/" + assetFileName;
            AudioClip clip = null;
#if UNITY_EDITOR
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
#endif
            Assert.That(clip, Is.Not.Null,
                $"取不到真 clip:{assetPath} —— 素材须已落盘并被 Unity 导入" +
                "(本件在编辑器 PlayMode 下运行;player 内 `AssetDatabase` 不可用,故本件是**编辑器腿**)");
            return clip;
        }

        /// <summary>cue → 真 clip 的解析表,供 <see cref="AudioClipLoadStateAdapter.Query"/>。</summary>
        private static Dictionary<string, AudioClip> LoadStethoscopeClips(
            List<KeyValuePair<string, string>> rows)
        {
            var byCue = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> row in rows)
                if (!byCue.ContainsKey(row.Key))
                    byCue[row.Key] = LoadRealClip(row.Value);
            return byCue;
        }

        private static StethoscopePreloadGate GateOver(Dictionary<string, AudioClip> clips,
            AudioEventDataSet table) =>
            StethoscopePreloadGate.FromTable(
                table,
                AudioClipLoadStateAdapter.Query(
                    cue => clips.TryGetValue(cue, out AudioClip c) ? c : null));

        /// <summary>轮询等所有 clip 到达期望态(逐帧 yield,不阻塞主线程)。</summary>
        private static IEnumerator WaitForState(
            IEnumerable<AudioClip> clips, AudioDataLoadState expected)
        {
            float deadline = Time.realtimeSinceStartup + LoadTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool all = true;
                foreach (AudioClip clip in clips)
                    if (clip.loadState != expected) { all = false; break; }
                if (all) yield break;
                yield return null;
            }
        }

        private static string Describe(IEnumerable<AudioClip> clips)
        {
            var parts = new List<string>();
            foreach (AudioClip clip in clips)
                parts.Add($"{clip.name}={clip.loadState}");
            return string.Join(", ", parts);
        }

        // ══════════════ 判据 ══════════════

        /// <summary>真 clip 的载入态**可观测**,且门的判据与 Unity 真值**逐字一致**。
        /// <para>这是「门不是自说自话」的判据:门的 <c>IsReady()</c> 必须等于
        /// 「全部真 clip 的 <c>loadState == Loaded</c>」。</para></summary>
        [UnityTest]
        public IEnumerator test_preload_gateAgreesWithRealClipLoadState()
        {
            AudioEventDataSet table = LoadRealTable();
            List<KeyValuePair<string, string>> rows = StethoscopeRows(table);
            Assert.That(rows.Count, Is.GreaterThan(0),
                "真产物表里听诊族为空 —— 族集派生或表内容有误");

            Dictionary<string, AudioClip> clips = LoadStethoscopeClips(rows);
            StethoscopePreloadGate gate = GateOver(clips, table);

            bool allLoaded = true;
            foreach (AudioClip clip in clips.Values)
                if (clip.loadState != AudioDataLoadState.Loaded) { allLoaded = false; break; }

            Assert.That(gate.IsReady(), Is.EqualTo(allLoaded),
                $"门的判据必须等于 Unity 真值 —— 实测 clip 态:[{Describe(clips.Values)}]");
            yield return null;
        }

        /// <summary>适配器接**真 clip**:<c>StateOf</c> 与真 <c>loadState</c> 逐条对应
        /// (纯映射已由 EditMode 钉;此处钉真对象上的取值)。</summary>
        [UnityTest]
        public IEnumerator test_preload_adapter_matchesRealClipState()
        {
            AudioEventDataSet table = LoadRealTable();
            Dictionary<string, AudioClip> clips = LoadStethoscopeClips(StethoscopeRows(table));

            foreach (KeyValuePair<string, AudioClip> pair in clips)
            {
                StethoscopePreloadGate.ClipLoadState mapped =
                    AudioClipLoadStateAdapter.StateOf(pair.Value);
                StethoscopePreloadGate.ClipLoadState expected =
                    AudioClipLoadStateAdapter.Map(pair.Value.loadState);

                Assert.That(mapped, Is.EqualTo(expected),
                    $"cue '{pair.Key}':StateOf 须等于 Map(clip.loadState)");
            }
            yield return null;
        }

        /// <summary>**决定性判据**:真 clip 经 `LoadAudioData()` 从 <c>Unloaded</c> 过渡到 <c>Loaded</c>,
        /// 门随之由**阻断**转**放行**。
        /// <para>先 <c>UnloadAudioData()</c> 把真 clip 打回 <c>Unloaded</c>(素材
        /// <c>preloadAudioData: 0</c>,本就允许此过渡)⇒ 得到一个**真·未载**的 clip 作阻断态,
        /// 不靠假夹具。</para></summary>
        [UnityTest]
        public IEnumerator test_preload_realClip_transitionsUnloadedToLoaded_andGateFlips()
        {
            AudioEventDataSet table = LoadRealTable();
            Dictionary<string, AudioClip> clips = LoadStethoscopeClips(StethoscopeRows(table));
            StethoscopePreloadGate gate = GateOver(clips, table);

            // ── Act ①:打回 Unloaded ──
            foreach (AudioClip clip in clips.Values) clip.UnloadAudioData();
            yield return WaitForState(clips.Values, AudioDataLoadState.Unloaded);

            foreach (AudioClip clip in clips.Values)
                Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Unloaded),
                    $"UnloadAudioData() 后应回 Unloaded —— 实测:[{Describe(clips.Values)}]");

            // ── Assert ①:未载 ⇒ 门阻断(真 clip 驱动,非注入)──
            Assert.That(gate.IsReady(), Is.False, "真 clip 未载 ⇒ 门必须阻断");
            Assert.That(gate.PendingCues(), Is.Not.Empty);

            // ── Act ②:真预载 ──
            foreach (AudioClip clip in clips.Values) clip.LoadAudioData();
            yield return WaitForState(clips.Values, AudioDataLoadState.Loaded);

            // ── Assert ②:过渡真的发生了(不是「本来就是 Loaded」)──
            foreach (AudioClip clip in clips.Values)
                Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded),
                    $"LoadAudioData() 后应达 Loaded —— 实测:[{Describe(clips.Values)}]" +
                    "(超时 = 真过渡未发生,须如实登记,不得弱化本断言)");

            // ── Assert ③:门随之放行 ──
            Assert.That(gate.IsReady(), Is.True, "全族真 Loaded ⇒ 门必须放行(否则门恒闭 = 功能死)");
            Assert.That(gate.PendingCues(), Is.Empty);
        }
    }
}
