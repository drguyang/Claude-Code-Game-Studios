// story-016 · AC-44-E5(无提示音铁律 · **语义面**)—— **PlayMode 手跑走查**(用户 2026-10-17 裁定)。
//
// 权威来源:
//   GDD design/gdd/audio-system.md:66-78(规则二 · 行为反馈白名单 vs 状态播报黑名单)
//   ADR-018 §六(无提示音铁律机械化 = 白名单断言;AC-44-09 BLOCKING)
//   design/assets/specs/audio-material-manifest.md §四(逐项「替代读法」)
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ **本件 = 走查件,机器半已接齐并**已接通触发**;人半未做。当下 E5 仍是 ✗(禁借绿)。**
//
// 2026-10-17 三道阻塞的现状(① 已解;② 判据已落、本机未跑;③ 未解):
//
// ① **文本半够不着词法器** —— ✅ **已解**。根因不是 asmdef(我首版把 `internalsVisibleTo` 填进 asmdef,
//    两次纯文本替换都没生效且第二次弄出重复键,Unity 静默忽略);本工程的成例是**源码里的
//    `AssemblyInfo.cs`**(`Sim.Codec` / `Editor.Tools.Spike` 先例)。已补
//    `Editor.Tools.Bake/AssemblyInfo.cs`,文本半现与门同源(`JsonStage1Lexer`)。
// ② **Play() 在本批次是否真跑起来** —— 判据已落 = `playback_firstCue_reachesPlayingWithoutDriverFallback`
//    (`isPlaying` 须真的进入过 true + clip 绑定 + Play 后设备不回退)。
//    2026-10-17 本机 batch **实测**:`isPlaying` = true · clip 绑定 = true · Play 后设备 = **真驱动**;
//    走查 15 cue 全部跑完,14/15 占空比 ≥96%,`Music_Encounter_Layer` 流式 ⇒ 占空比**未测**(非 0)。
//    ⇒ **「`Play()` 在本批次被静默丢弃」这一形态可判为不成立。**
//    ⚠️ 但本条绿**仍不证明**「有人听到了」:batch 进程是否接上了**真人所在的输出端**无判据。
//    故 ② 只收窄到「播放路径通」,**不**升格为「已走查」。
// ③ **人半** —— 走查须人在**交互式编辑器**里跑(要听得见),判的是耳朵;batch 只能跑机器半。
//    触发已接通:设备门改为**运行期**判定(`AssertAudioDriverIsUsable`)—— 静态配扬声器配置
//    回答不了②,且跨机不自足。
//
// ⚠️ 设备回退为 Mono 的机器上:`execGate_` / `playback_` / `walkthrough_` **红** ——
//    红即「本机未走查」这一事实,不得被当回归修掉。E5 记 ✗。
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ **S15-7 reconcile 后的人半口径(2026-10-18 用户裁定「E5 选 A」)**:
//    E5 的签核格是 `[ ] 通过`,与该文件自订判据「未勾选 = E5 未通过」**一致** ⇒ E5 = ✗。
//    **且人半比「缺一个勾」更弱一层(本批实测)**:谓词文件里的 15 条 `not_reporting`
//    **与 probe 预填的建议稿逐字相同** —— 指令 ④ 自陈「这是**默认判定**,不是你听过的结论」,
//    而 `ReplayPredicatesFromText` 对 `not_reporting` **只校验非空**(无任何机制要求它必须被改过)。
//    ⇒ `status_words: null` 既可能是「听完确实没有」,也可能是「没人填」;
//     记录表 15 行判定列的「✅ 不是状态播报」是机器从 null 推出的,**不是人写的**。
//    **出路**(用户裁 A):走查人真听一遍,把 15 条 `not_reporting` **改写成自己的话**
//   (哪怕只改语气、加听到的具体质地),`status_words` 保持 null,重跑本 probe 生成新记录 ⇒
//    人半才有「人听过」的物证。**不需要新机制,现有判据就够。**
// ⚠️ **同批治法(S15-7 · 用户裁定「副本选 A + 顺手做 C」)**:清单 / 记录的文件名
//    从「当天日期」改为「**内容哈希**」,`WriteOnce` 判据从「当天已存在」改为「**内容相同**」
//    —— 原实现的失效形态实测过两次(10-18 两份副本与 10-17 原件逐字节相同且已入库;
//    record 侧还是无条件 `File.WriteAllText` ⇒ 同天二次重跑会静默改写已签核记录)。
//    判据 = 本节末三条 `fileName_isContentAddressed_*` / `contentHash_*` / `writeOnce_*` 夹具。
// ═══════════════════════════════════════════════════════════════════════════════
//
// 走查设计的意图:
//   ① 把事件表**全部** cue 真播一遍,产出走查清单(含逐项「替代读法」——人单靠听判不出归属,
//      须对着 manifest §四 核);
//   ② 报告实际**出声**的 cue(占空比 0 ⇒ 该 cue「听到」无从成立,E5 判定无效);
//   ③ 机器判**文本先后**(判据器 = `Gameplay.Presentation.Audio.CueTextOrderingRules`,
//      已有 EditMode 夹具,证其判别力);
//   ④ 走查人把听到的落成谓词(JSON)→ 重跑判定 → 写可归档的走查记录(未签字 = E5 未通过)。
//
// ⚠️ **本件证明不了什么**:它**不证明**「播放出来的声音不是在报状态」—— 那是**耳朵**的判定。
//    机器半全绿而耳朵判「在报状态」⇒ E5 不通过;机器半红 ⇒ E5 必不通过。
//
// ⚠️ **cooked 表不承载 `subtitle_text`** ⇒ 表取 cooked 真产物、文本取作者态源
//    (`assets/data/audio_events.json`)—— 两处都以真源为准,不另抄 schema。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace DaYiJingCheng.Tests.EditMode.Audio
{
    /// <summary>
    /// AC-44-E5 语义面的 PlayMode 手跑走查(机器半:清单 / 出声 / 文本先后;人半:耳朵判定)。
    /// </summary>
    [TestFixture]
    public class StateReportWalkthroughProbe
    {
        private const int SchemaVersion = 1;
        private const string AudioDirRel = "Assets/Audio";
        private const float ClipTimeoutSeconds = 30f;
        private const float PlaySecondsPerCue = 1.0f;
        private const float PlayVolume = 0.8f;

        /// <summary>作者态事件表(相对仓根;文本半的唯一出处)。</summary>
        private const string EventsSourceRel = "assets/data/audio_events.json";

        /// <summary>走查产物的目录(相对仓根)。</summary>
        private const string WalkthroughDirRel = "docs/audio";

        /// <summary>谓词文件名(人填;不存在 ⇒ 判定只跑机器半)。</summary>
        private const string PredicatesFileName = "statereport-walkthrough-predicates.json";

        private string _repoRoot;

        private static string RepoRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        // ══════════════ 真产物 → 清单 ══════════════

        /// <summary>真 cooked 表(缺失 ⇒ 红,不 Skip)。</summary>
        private static AudioEventDataSet LoadRealTable()
        {
            string path = Path.Combine(Application.dataPath, "DataCooked", AudioEventDataSet.CookedFileName);
            Assert.That(File.Exists(path), Is.True,
                $"真烘焙产物缺失:{path} —— 先跑菜单「大医精诚/数据管线/烘焙 audio_events」。");
            return AudioEventCookedCodec.Read(File.ReadAllBytes(path));
        }

        /// <summary>
        /// 源文件 `assets/data/audio_events.json` 的 `subtitle_text`(cue → 文本;无该键 = null)。
        /// <para>与门**同源**:用 `Editor.Tools.Bake.JsonStage1Lexer`(即 `AudioRequirementsBake` 走的那个),
        /// 不另抄解析器。可见性 = `Editor.Tools.Bake/AssemblyInfo.cs` 的 `InternalsVisibleTo("Sim.Contracts.Tests")`
        /// (先例 Sim.Codec / Editor.Tools.Spike)。</para>
        /// </summary>
        private static Dictionary<string, string> ReadSourceSubtitles(string repoRoot)
        {
            string path = Path.Combine(repoRoot, EventsSourceRel);
            Assert.That(File.Exists(path), Is.True, $"作者态源缺失:{path}");
            string json = File.ReadAllText(path);
            Assert.That(JsonStage1Lexer.TryParse(json, out JsonNode root, out string error), Is.True,
                $"作者态源词法失败:{error}");
            Assert.That(root.TryGet("rows", out JsonNode rows) && rows?.Items != null, Is.True,
                "作者态源缺 rows[]");

            var outp = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonNode row in rows.Items)
            {
                if (row == null || !row.TryGet("cue", out JsonNode cue) || cue?.Str == null) continue;
                string text = null;
                if (row.TryGet("subtitle_text", out JsonNode st) && st != null && st.Kind != JsonNodeKind.Null)
                    text = st.Str;
                outp[cue.Str] = text;
            }
            return outp;
        }

        /// <summary>按素材文件名取真导入 clip(取不到 ⇒ 红)。</summary>
        private static AudioClip LoadRealClip(string assetFileName)
        {
            string assetPath = AudioDirRel + "/" + assetFileName;
            AudioClip clip = null;
#if UNITY_EDITOR
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
#endif
            Assert.That(clip, Is.Not.Null, $"取不到真 clip:{assetPath} —— 素材须已落盘并被导入");
            return clip;
        }

        /// <summary>
        /// clip 的**占空比**(0 = 全零 = 不可闻)。读不到采样 ⇒ <c>null</c>(**不是** 0)。
        /// <para>⚠️ 这两者必须分开:流式素材(乐层按 `AudioImportSettingsGate` 规则就是 Streaming)
        /// 的 `GetData` 在 Unity 里**硬性不可用** —— 把它当 0 会被下游误判成「静音 ⇒ E5 判定无效」。
        /// 改为 null ⇒ 报文记「未测」,与该 cue 的真实可闻性无关。证伪 M3 钉住这一点。</para>
        /// </summary>
        private static float? Occupancy(AudioClip clip)
        {
            if (clip == null || clip.samples <= 0 || clip.channels <= 0) return null;
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded) return null;
            // 流式 clip 的采样只能边播边解,GetData 不可用 —— 读之前先判,不去触发那条 Error。
            if (clip.loadType == AudioClipLoadType.Streaming) return null;
            long plan = (long)clip.samples * clip.channels;
            int take = (int)Math.Min(plan, 4L * 65536);
            var buf = new float[take];
            if (!clip.GetData(buf, 0)) return null;
            int nonZero = 0;
            for (int i = 0; i < buf.Length; i++)
                if (buf[i] != 0f) nonZero++;
            return buf.Length == 0 ? (float?)null : (float)nonZero / buf.Length;
        }

        /// <summary>播一条 cue 并等它结束(一次性 = 整条;循环 = 固定窗口后停)。</summary>
        private static IEnumerator PlayOne(AudioSource src, AudioClip clip, bool loop)
        {
            src.clip = clip;
            src.loop = loop;
            src.volume = PlayVolume;
            src.Play();
            if (loop)
            {
                yield return new WaitForSeconds(PlaySecondsPerCue);
                src.Stop();
            }
            else
            {
                float deadline = Time.realtimeSinceStartup + clip.length + 1f;
                while (src.isPlaying && Time.realtimeSinceStartup < deadline)
                    yield return null;
            }
            src.clip = null;
            yield return null;
        }

        // ══════════════ ① 执行门:有没有耳朵可用的设备 ══════════════

        /// <summary>
        /// 执行门:无音频设备 ⇒ 红。走查的前提是**有人能听到**;设备缺失时 Unity 静默回退到
        /// Mono 虚拟设备 —— 此时跑出来的「机器全绿」对耳朵毫无意义,不得记 E5 通过。
        /// <para>⚠️ 本门回答的是「有没有设备」,**不是**「`Play()` 在 EditMode 批次里出不出声」——
        /// 后者 = 阻塞 ②,由 <see cref="playback_firstCue_reachesPlayingWithoutDriverFallback"/> 回答;
        /// 再往后「耳朵真听到了」只有人能答(阻塞 ③)。三个问题的判据各不相同,不得互相顶替。</para>
        /// </summary>
        [Test]
        public void execGate_audioDevice_isPresent()
        {
            AudioConfiguration cfg = AudioSettings.GetConfiguration();
#if UNITY_EDITOR
            bool speakers = cfg.speakerMode != AudioSpeakerMode.Mono;
            Assert.That(speakers, Is.True,
                $"配置回退为 {cfg.speakerMode} —— 编辑器未持有音频设备;本机未走查,E5 不得记通过。" +
                "请在**有声卡**的会话里重跑。");
#else
            var caps = AudioSettings.driverCapabilities;
            Assert.That((caps & AudioSpeakerMode.BuiltInSpeakers) != 0, Is.True,
                $"无内置扬声器(能力 = {caps})—— E5 手跑走查不可执行。");
#endif
        }

        // ══════════════ ② 阻塞:Play() 在本批次是否真跑起来(不是「有没有设备」)══════════════

        /// <summary>
        /// 阻塞 ② 的判据:**第一 cue 用真实走查路径播放,`isPlaying` 须真的进入过 true 且音源
        /// 持有的正是那条 clip**。
        /// <para>⚠️ 这条回答的是「`AudioSource.Play` 在**本批次**里跑没跑」,不是「扬声器出没出声」。
        /// 两边差在哪:`Play()` 即便输出端是虚拟设备也会把 `isPlaying` 置 true —— 所以本条**绿**
        /// **不构成**「有人听到了」;它绿只证明「走查件不会在没播的情况下假称播过」。
        /// 反过来,本条若红/Inconclusive(设备经运行期回退)⇒ 走查机器半无效,E5 不得通过。</para>
        /// <para>本条的"红"有两种可辨形态,须分别读:`isPlaying` 一次都没 true = 播放被静默丢弃;
        /// 设备回退 = 输出端不可信。两者报文不同。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator playback_firstCue_reachesPlayingWithoutDriverFallback()
        {
            AssertAudioDriverIsUsable("阻塞 ② 的判据");
            AudioEventDataSet table = LoadRealTable();
            AudioEventRow first = table.Row(0);
            AudioClip clip = LoadRealClip(first.Assets[0]);

            var go = new GameObject("E5Block2Probe");
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            bool sawPlaying = false;
            bool sawClipBound = false;
            try
            {
                src.clip = clip;
                src.loop = false;
                src.volume = 0f;           // 本条只证「跑起来了」,不制造噪音
                src.Play();
                for (int i = 0; i < 10 && !sawPlaying; i++)
                {
                    if (src.isPlaying) sawPlaying = true;
                    if (ReferenceEquals(src.clip, clip)) sawClipBound = true;
                    yield return null;
                }
                bool driverAfter = AudioSettings.GetConfiguration().speakerMode != AudioSpeakerMode.Mono;
                Debug.Log($"[E5 · ②] {first.Cue}: isPlaying 进入过 = {sawPlaying} · clip 绑定 = {sawClipBound}" +
                          $" · Play 后设备 = {(driverAfter ? "真驱动" : "回退(Mono)")}");
                Assert.That(sawClipBound, Is.True,
                    "Play() 后音源未持有该 clip —— 播放路径没走到,走查机器半无意义");
                Assert.That(sawPlaying, Is.True,
                    "Play() 之后 isPlaying 从未为 true —— 本批次把播放静默丢弃了(阻塞 ② 成立),走查机器半无意义");
                Assert.That(driverAfter, Is.True,
                    "Play() 之后设备回退为 Mono 虚拟设备 —— 输出端不可信,阻塞 ② 未解");
            }
            finally
            {
                src.Stop();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 走查 / 阻塞 ② 判据共用的运行期设备门:回退为 Mono ⇒ 红,报文点名是哪一项被阻塞。
        /// </summary>
        private static void AssertAudioDriverIsUsable(string what)
        {
            AudioConfiguration cfg = AudioSettings.GetConfiguration();
            Assert.That(cfg.speakerMode != AudioSpeakerMode.Mono, Is.True,
                $"{what}:音频配置回退为 Mono(编辑器未持有真音频设备)—— E5 不得记通过;" +
                "请在**有声卡**的会话里重跑。");
        }

        /// <summary>
        /// **真素材**的占空比判据:非流式素材须给得出数(≥0),流式素材须给 `null` 而**不是** 0。
        /// <para>前提用真表逐行取的 `assets[0]` + 真导入 clip(不走自建 clip);表里没有流式素材
        /// ⇒ 前提断言红(不许空真)。</para>
        /// <para>⚠️ 判据只钉「读不到 ⇒ null」这一条。`null` **不得**被下游当静音 —— 见走查件的
        /// `unmeasured` 分支与 <see cref="BuildRowString"/> 的 `occupancy: null`。</para>
        /// </summary>
        [Test]
        public void occupancy_streamingClip_isNullNotZero()
        {
            AudioEventDataSet table = LoadRealTable();
            int streamed = 0;
            int measured = 0;
            for (int i = 0; i < table.RowCount; i++)
            {
                AudioEventRow row = table.Row(i);
                if (row.Assets == null || row.Assets.Length == 0) continue;
                AudioClip clip = LoadRealClip(row.Assets[0]);
                float? occ = Occupancy(clip);
                if (clip.loadType == AudioClipLoadType.Streaming)
                {
                    streamed++;
                    Assert.That(occ, Is.Null,
                        $"流式素材「{row.Assets[0]}」的占空比被读成了 {occ} —— 应判「未测」");
                }
                else
                {
                    measured++;
                    Assert.That(occ, Is.Not.Null,
                        $"非流式素材「{row.Assets[0]}」的占空比应读得出(占空比未测 = 判别面缺失)");
                    Assert.That(occ.Value, Is.GreaterThanOrEqualTo(0f));
                }
            }
            Assert.That(streamed, Is.GreaterThan(0),
                "真表里没有流式素材 —— 本判据空真(乐层按 AudioImportSettingsGate 应为 Streaming)");
            Assert.That(measured, Is.GreaterThan(0), "真表里没有非流式素材 —— 反面前提不成立");
        }

        /// <summary>
        /// **素材本体在盘上**:真表每行的 `assets[0]` 须落在 `unity/Assets/Audio/`,且文件非空。
        /// <para>⚠️ 本判据**只读文件**,不经 `AssetDatabase` —— 新 clone 的机器上 Unity 可能还没导入素材,
        /// 此时走查会以「取不到 clip」红;本条把失败点提前到「文件在不在」,报文直接点名缺哪个。</para>
        /// <para>⚠️ 本件**不判** `docs/audio/` 那类**本地生成产物**目录 —— 实测(2026-10-17 M2)
        /// 写清单会自动建目录,拿它当「跨机前置」是空判(删掉目录后 13/13 全绿)。</para>
        /// </summary>
        [Test]
        public void realTable_assetsExistOnDisk()
        {
            AudioEventDataSet table = LoadRealTable();
            string audioDir = Path.Combine(Application.dataPath, "Audio");
            var missing = new List<string>();
            var empty = new List<string>();
            int checkedCount = 0;
            for (int i = 0; i < table.RowCount; i++)
            {
                AudioEventRow row = table.Row(i);
                if (row.Assets == null || row.Assets.Length == 0)
                {
                    missing.Add($"{row.Cue}: assets[] 为空");
                    continue;
                }
                foreach (string asset in row.Assets)
                {
                    checkedCount++;
                    string path = Path.Combine(audioDir, asset);
                    if (!File.Exists(path)) missing.Add($"{row.Cue}: {asset}");
                    else if (new FileInfo(path).Length == 0) empty.Add($"{row.Cue}: {asset}");
                }
            }
            Assert.That(checkedCount, Is.GreaterThan(0), "真表一行素材都没得查 —— 本判据空真");
            Assert.That(missing, Is.Empty, "素材文件缺失:\n" + string.Join("\n", missing));
            Assert.That(empty, Is.Empty, "素材文件为空(0 字节):\n" + string.Join("\n", empty));
        }

        // ══════════════ ② 走查清单 + 实际播放 + 文本先后 ══════════════

        /// <summary>
        /// 全 15 cue 的走查:整理清单(含逐项替代读法)、逐条真播、报告出声数、判定文本先后,
        /// 最后(谓词文件存在时)重跑判定并落走查记录。
        /// </summary>
        [UnityTest]
        public IEnumerator walkthrough_allCues_manifestPlaybackAndOrdering()
        {
            _repoRoot = RepoRoot();
            AssertAudioDriverIsUsable("走查");
            AudioEventDataSet table = LoadRealTable();
            Dictionary<string, string> subtitles = ReadSourceSubtitles(_repoRoot);

            var strings = new List<string>(table.RowCount);
            var clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            var audible = new List<string>();
            var silent = new List<string>();
            var unmeasured = new List<string>();
            var emptySubtitle = new List<string>();

            var go = new GameObject("E5WalkthroughSource");
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            try
            {
                for (int i = 0; i < table.RowCount; i++)
                {
                    AudioEventRow row = table.Row(i);
                    Assert.That(row.Assets, Is.Not.Null.And.Not.Empty, $"'{row.Cue}' 无 assets[]");
                    AudioClip clip = LoadRealClip(row.Assets[0]);
                    clips[row.Cue] = clip;

                    float? occ = Occupancy(clip);
                    if (occ == null) unmeasured.Add(row.Cue);
                    else if (occ > 0f) audible.Add(row.Cue);
                    else silent.Add(row.Cue);
                    Debug.Log($"[E5] 播放 {i + 1}/{table.RowCount}: {row.Cue} · {row.Assets[0]}" +
                              $" · loop={row.Loop} · {clip.length.ToString("0.###", CultureInfo.InvariantCulture)} s" +
                              $" · 占空比 {(occ == null ? "未测(流式 / 未驻留 —— 与可闻性无关)" : occ.Value.ToString("P0", CultureInfo.InvariantCulture))}");

                    yield return PlayOne(src, clip, row.Loop);

                    subtitles.TryGetValue(row.Cue, out string subtitle);
                    if (row.Loop && !string.IsNullOrEmpty(subtitle)) emptySubtitle.Add(row.Cue);

                    strings.Add(BuildRowString(i, row, subtitle, occ));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            Debug.Log($"[E5] 出声 {audible.Count}/{table.RowCount}:" +
                      (audible.Count == 0 ? "(无)" : string.Join(", ", audible)));
            if (silent.Count > 0)
                Debug.Log($"[E5] ⚠️ 静音(占空比 0,走查人无从「听到」):" +
                          string.Join(", ", silent) + " —— 这些 cue 的 E5 判定**无效**,须换素材或记 NOT-RUN");
            if (unmeasured.Count > 0)
                Debug.Log($"[E5] 占空比未测(流式 / 未驻留 — 不判可闻性,交耳朵):" + string.Join(", ", unmeasured));
            if (emptySubtitle.Count > 0)
                Debug.Log($"[E5] ⚠️ 循环行带 subtitle_text(文本半的前提受损):" + string.Join(", ", emptySubtitle));

            Assert.That(audible.Count + unmeasured.Count, Is.GreaterThan(0),
                "全部 cue 静音 —— 机器半无法支撑人耳走查,判定无效");

            // ① 清单(内容哈希命名 + 同内容不重复生成 —— S15-7 治法:拆掉「跨天必然产副本」的根因)
            string manifestJson = BuildManifestJson(table, strings);
            string manifestPath = Path.Combine(_repoRoot, WalkthroughDirRel, ManifestFileName(manifestJson));
            WriteOnce(manifestPath, manifestJson);

            // ② 文本先后(机器半的必要条件;判据器 = CueTextOrderingRules,判别力由 EditMode 夹具证)
            //    ⚠️ 逐 cue 对**事件表全部行**判,遍历 cooked 表(而非 subtitles 字典)—— 源里有、表里没有的
            //    行不得被静默跳过;二者行集合不一致另报错。
            var problems = new List<string>();
            for (int i = 0; i < table.RowCount; i++)
            {
                AudioEventRow row = table.Row(i);
                if (!subtitles.TryGetValue(row.Cue, out string subtitle))
                {
                    problems.Add($"[E5 · 源表] cue「{row.Cue}」在作者态源内找不到 —— 两表行集合不一致");
                    continue;
                }
                if (string.IsNullOrEmpty(subtitle)) continue;
                problems.AddRange(CueTextOrderingRules.TextOrderingProblems(
                    row.Cue, subtitle, emptySubtitle));
            }
            Assert.That(problems, Is.Empty,
                "文本先后判定失败(文字先于声音 = 音频在给已读出的结论加提示音):\n" + string.Join("\n", problems));

            // ③ 记录(仅当谓词文件存在)
            // ⚠️ S15-7 治法:原为**无条件** `File.WriteAllText` ⇒ 同天第二次重跑会静默改写当天记录。
            //    现统一走 `WriteOnce`(内容判等)+ 内容哈希命名 —— 与 manifest 同一条规则。
            string predicatesPath = Path.Combine(_repoRoot, WalkthroughDirRel, PredicatesFileName);
            if (!File.Exists(predicatesPath))
            {
                Debug.Log($"[E5] 未发现谓词文件({predicatesPath})—— 走查记录**未生成**。" +
                          "机器半到此为止;E5 仍待耳朵判定。");
                yield break;
            }

            var verdictProblems = new List<string>();
            string markdown = ReplayPredicates(predicatesPath, table, clips,
                silent, subtitles, verdictProblems);
            Assert.That(verdictProblems, Is.Empty,
                "走查判定失败:\n" + string.Join("\n", verdictProblems));

            string recordPath = Path.Combine(_repoRoot, WalkthroughDirRel, RecordFileName(markdown));
            WriteOnce(recordPath, markdown);
            Debug.Log($"[E5] 走查记录已写:{Path.Combine(WalkthroughDirRel, RecordFileName(markdown))}" +
                      "(待走查人签字 —— 未签字 = E5 未通过)");
        }

        // ══════════════ 夹具:小表(不落盘)══════════════

        /// <summary>单行表(夹具;`assets[]` 给个占位名 —— 谓词重跑不看素材,只看 cue / 判定)。</summary>
        private static AudioEventDataSet SingleRowTable(string cue, string category, string trigger)
        {
            var rows = new[]
            {
                new AudioEventRow(cue, 1, false, "SFX", category, trigger, null,
                    null, null, new[] { "fixture.wav" }, null, false, null, null),
            };
            return new AudioEventDataSet(rows, null, 1, 0);
        }

        // ══════════════ ②′ 文本半的接线判据(走查触发停用期间也须可执行)══════════════

        /// <summary>
        /// 源字幕真读得出来,且**真源上文本先后判定为空**。
        /// <para>⚠️ 本测试补的是「走查触发停用 ⇒ ③ 文本先后那段从不执行」的洞:把读入 + 判定的调用
        /// 单独抽出来对**真源**跑一次,否则那段接线删掉也没人知道(同 F1 型假绿)。</para>
        /// </summary>
        [Test]
        public void textHalf_realSource_readsSubtitlesAndPassesOrdering()
        {
            Dictionary<string, string> subtitles = ReadSourceSubtitles(RepoRoot());
            Assert.That(subtitles, Is.Not.Empty, "源字幕一个都没读出来 —— 读入路径坏了");

            int withSubtitle = 0;
            var problems = new List<string>();
            foreach (KeyValuePair<string, string> kv in subtitles)
            {
                if (string.IsNullOrEmpty(kv.Value)) continue;
                withSubtitle++;
                problems.AddRange(CueTextOrderingRules.TextOrderingProblems(kv.Key, kv.Value, new string[0]));
            }
            Assert.That(withSubtitle, Is.GreaterThan(0),
                "真源里一个带 subtitle_text 的 cue 都没有 —— 判据空真(前提不成立,须先确认数据)");
            Assert.That(problems, Is.Empty,
                "真源文本先后判定失败:\n" + string.Join("\n", problems));
        }

        /// <summary>
        /// 真事件表里的乐层 cue 须被归入 `music_layer`(2026-10-17 用户裁定:算在 E5 射程内)——
        /// 逐条拿**真源**的 `whitelist_category` 过判定器,而不是拿一个自造林类别名。
        /// </summary>
        [Test]
        public void realTable_musicLayerCue_isJudgedUnderE5()
        {
            AudioEventDataSet table = LoadRealTable();
            var musicCues = new List<string>();
            for (int i = 0; i < table.RowCount; i++)
            {
                AudioEventRow row = table.Row(i);
                if (!string.Equals(row.WhitelistCategory, "MusicLayer", StringComparison.Ordinal)) continue;
                musicCues.Add(row.Cue);
                Assert.That(CueTextOrderingRules.Reason(row.Cue, row.WhitelistCategory, null),
                    Does.StartWith("music_layer:"), $"真源乐层 cue「{row.Cue}」未落在 music_layer 归属");
            }
            Assert.That(musicCues, Is.Not.Empty,
                "真事件表里一条 MusicLayer 都没有 —— 本判据空真(前提不成立,须先确认数据)");
        }

        /// <summary>
        /// 谓词重跑的**全部拒绝分支**逐条可触发(输入用 JSON 文本夹具,**不落盘** —— 承
        /// `test_discipline_noFilesystemWrites` 的纪律,本文件零文件系统写)。
        /// <para>覆盖:词法坏 / 缺 verdicts / 缺 cue 判定 / 缺 reviewer / date 格式坏 /
        /// status_words 类型坏 / 缺 not_reporting / 听到状态结论(黑名单命中)。</para>
        /// </summary>
        [TestCase("{ not json", "词法失败")]
        [TestCase("{\"reviewer\":\"qa\",\"date\":\"2026-10-17\"}", "缺 verdicts")]
        [TestCase("{\"reviewer\":\"qa\",\"date\":\"2026-10-17\",\"verdicts\":[]}", "无判定 —— 走查须逐条")]
        public void replayPredicates_rejectsMalformedInput_case(string json, string expectedFragment)
        {
            var problems = new List<string>();
            ReplayPredicatesFromText(json, SingleRowTable("Pulse_Contact", "PlayerExamAction", "PlayerExamAction"),
                new Dictionary<string, AudioClip>(), new List<string>(),
                new Dictionary<string, string>(), problems);
            Assert.That(problems, Is.Not.Empty, $"「{expectedFragment}」应被拒,实际零问题");
            Assert.That(string.Join("\n", problems), Does.Contain(expectedFragment));
        }

        /// <summary>谓词**全部合格**时产出含签字表的记录,且逐 cue 判定为「不是状态播报」。</summary>
        [Test]
        public void replayPredicates_allVerdictsClean_producesSignedRecord()
        {
            var table = SingleRowTable("Pulse_Contact", "PlayerExamAction", "PlayerExamAction");
            var clips = new Dictionary<string, AudioClip> { { "Pulse_Contact", null } };
            string json =
                "{\"reviewer\":\"qa\",\"date\":\"2026-10-17\",\"verdicts\":[" +
                "{\"cue\":\"Pulse_Contact\",\"not_reporting\":\"布料摩擦\",\"status_words\":null}]}";

            var problems = new List<string>();
            string markdown = ReplayPredicatesFromText(json, table, clips, new List<string>(),
                new Dictionary<string, string> { { "Pulse_Contact", null } }, problems);

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(markdown, Is.Not.Null.And.Contain("[ ] 通过"), "记录须含未签字的签字行");
            Assert.That(markdown, Does.Contain("✅ 不是状态播报"));
        }

        /// <summary>
        /// 素材**静音**(出声判据里占空比 0)时,即便谓词写「不是状态播报」也**判无效** ——
        /// 走查人无从听到的东西不能记为「听过且没问题」。
        /// <para>补 W4 暴露的洞:静音分支原本无夹具,停用拦截则零红(2026-10-17 实测)。</para>
        /// </summary>
        [Test]
        public void replayPredicates_silentAsset_rejectsVerdictAsInvalid()
        {
            var table = SingleRowTable("Pulse_Contact", "PlayerExamAction", "PlayerExamAction");
            var clips = new Dictionary<string, AudioClip> { { "Pulse_Contact", null } };
            string json =
                "{\"reviewer\":\"qa\",\"date\":\"2026-10-17\",\"verdicts\":[" +
                "{\"cue\":\"Pulse_Contact\",\"not_reporting\":\"布料摩擦\",\"status_words\":null}]}";

            var problems = new List<string>();
            string markdown = ReplayPredicatesFromText(json, table, clips, new List<string> { "Pulse_Contact" },
                new Dictionary<string, string> { { "Pulse_Contact", null } }, problems);

            Assert.That(string.Join("\n", problems), Does.Contain("静音"), "静音 cue 的判定须被判无效");
            Assert.That(markdown, Is.Null, "有无效判定时不得产出记录");
        }

        /// <summary>谓词里听到状态结论 ⇒ **判红**(黑名单第 4 条),记录不产出。</summary>
        [Test]
        public void replayPredicates_statusWordHeard_isRejected()
        {
            var table = SingleRowTable("Voice_Cough_Damp", "ContinuousPhysiology", "ContinuousPhysiology");
            var clips = new Dictionary<string, AudioClip> { { "Voice_Cough_Damp", null } };
            string json =
                "{\"reviewer\":\"qa\",\"date\":\"2026-10-17\",\"verdicts\":[" +
                "{\"cue\":\"Voice_Cough_Damp\",\"not_reporting\":\"闷咳\",\"status_words\":[\"病危\"]}]}";

            var problems = new List<string>();
            string markdown = ReplayPredicatesFromText(json, table, clips, new List<string>(),
                new Dictionary<string, string> { { "Voice_Cough_Damp", "咳…咳咳…" } }, problems);

            Assert.That(string.Join("\n", problems), Does.Contain("黑名单第 4 条命中"), string.Join("\n", problems));
            Assert.That(markdown, Is.Null, "判定红时不得产出记录");
        }

        // ══════════════ 清单 / 记录 ══════════════

        // ⚠️ **S15-7 治法(用户裁定「副本选 A + 顺手做 C」· 2026-10-18)**:
        //    原实现 = **按当天日期命名** + manifest 走 `WriteOnce`(当天已存在则不覆盖)、
        //    record 走**无条件** `File.WriteAllText`。两种语义各有一个失效形态:
        //    ① 跨天重跑 ⇒ 日期不同 ⇒ 必然产出**与旧件逐字节相同**的新副本
        //       (实测:10-18 两份副本与 10-17 入库原件 md5 全同,且已随 S15-5 `910aa79` 入库);
        //    ② record 无条件覆盖 ⇒ 同一天第二次重跑会**静默改写**当天记录。
        //    ⇒ 改为**内容哈希命名 + 按内容判等**:
        //       · 同内容 ⇒ 任何一天都命中同一路径,不重复生成(副本的根因被拆掉);
        //       · 不同内容 ⇒ 路径不同 ⇒ 旧件保留(可追溯),不被静默覆盖。
        //    ⚠️ 副作用如实登记:文件路径**不再含日期**。日期信息在 manifest / 记录的正文里
        //       (`BuildManifestJson` 的 `event_table_config_version` 与记录的标题行),
        //       不靠文件名承载 —— 这与「不许 chunk 局部坐标系」同型:**不用位置编码时间**。

        /// <summary>清单文件名 = 内容哈希(不再是当天日期 —— 见上方 S15-7 治法注)。</summary>
        internal static string ManifestFileName(string content) => $"statereport-walkthrough-manifest-{ContentHash(content)}.json";

        /// <summary>记录文件名 = 内容哈希(同上)。</summary>
        internal static string RecordFileName(string content) => $"statereport-walkthrough-{ContentHash(content)}.md";

        /// <summary>内容的确定性短哈希(16  hex;FNV-1a 64 位,禁 `UnityEngine.Hash128` —— ADR-010 同款纪律)。</summary>
        internal static string ContentHash(string content)
        {
            ulong h = 1469598103934665603UL;
            foreach (char c in content)
            {
                h ^= (byte)(c & 0xFF);
                h *= 1099511628211UL;
                h ^= (byte)((c >> 8) & 0xFF);
                h *= 1099511628211UL;
            }
            return h.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static bool SameContent(string path, string content)
            => File.Exists(path) && File.ReadAllText(path) == content;

        // ══════════════ S15-7 治法的判据(内容哈希命名 + 同内容不重复生成)══════════════

        /// <summary>文件名只由内容决定 —— 与「哪一天跑」无关。
        /// <para>这正是「跨天重跑必然产副本」的根因判据:若名字里含日期,两次同内容跑必得两个路径。</para></summary>
        [Test]
        public void fileName_isContentAddressed_notDateAddressed()
        {
            const string a = "同内容甲";
            Assert.That(ManifestFileName(a), Is.EqualTo(ManifestFileName(a)),
                "同内容两次命名必须同一路径 —— 否则副本根因仍在");
            Assert.That(RecordFileName(a), Is.EqualTo(RecordFileName(a)), "同上(记录)");
            Assert.That(ManifestFileName(a), Is.Not.EqualTo(ManifestFileName("同内容乙")),
                "不同内容须得不同路径 —— 否则旧件被静默覆盖");
        }

        /// <summary>哈希确定性 + 不含日期戳:同一字符串跨进程 / 跨日恒同,且形态是 16 hex。</summary>
        [Test]
        public void contentHash_isDeterministic_andHex16()
        {
            Assert.That(ContentHash("abc"), Is.EqualTo(ContentHash("abc")), "确定性(可复算)");
            Assert.That(ContentHash("abc"), Does.Not.EqualTo(ContentHash("abd")), "异内容异值");
            Assert.That(ContentHash("abc"), Does.Match("^[0-9a-f]{16}$"), "16 位小写 hex");
        }

        /// <summary>`WriteOnce` 同内容不重复生成、异内容**拒写**(不覆盖)。
        /// <para>⚠️ 本条是本治法的**执行判据**,且它首轮就是红的 —— 首版实现只用「内容哈希命名」
        /// 保证异内容换路径,`WriteOnce` 本身**仍会覆盖**。命名纪律 ≠ 断言兜底:本条把
        /// 「不会静默覆盖已签核记录」从偶然变成不变式。</para>
        /// <para>反证:把 `WriteOnce` 的 `File.Exists` 分支改回无条件 `File.WriteAllText` ⇒ 本条红。</para></summary>
        [Test]
        public void writeOnce_sameContent_isNotRewritten_differentContent_isRefused()
        {
            string dir = Path.Combine(Path.GetTempPath(), "e5-writeonce-" + Guid.NewGuid().ToString("N"));
            try
            {
                string path = Path.Combine(dir, "probe.md");
                WriteOnceForTest(path, "第一版");
                WriteOnceForTest(path, "第一版");          // 同内容 —— 不得重复产出
                Assert.That(File.ReadAllText(path), Is.EqualTo("第一版"), "同内容不得改写");

                // 拒写路径会打 `Debug.LogError` —— 那是**设计内的告警**,不是失败。
                // 若不声明,UTF 的 Unhandled log 检查会把「正确地拒写」判成红(假绿反向形态)。
                LogAssert.Expect(UnityEngine.LogType.Error,
                    new System.Text.RegularExpressions.Regex("拒写.*内容不同"));
                WriteOnceForTest(path, "第二版");          // 异内容 —— 必须拒写
                Assert.That(File.ReadAllText(path), Is.Not.EqualTo("第二版"),
                    "异内容不得静默覆盖旧件(旧件是已签核走查记录,覆盖 = 销毁证据)");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        /// <summary>`WriteOnce` 是 private static,测试经此薄封装驱动(不改生产可见性)。</summary>
        private static void WriteOnceForTest(string path, string content) => WriteOnce(path, content);

        private static void WriteOnce(string path, string content)
        {
            // 首次生成:目录尚不存在 ⇒ 建之。
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(path))
            {
                string existing = File.ReadAllText(path);
                if (existing == content)
                {
                    Debug.Log($"[E5] 清单已存在且内容相同,不重复生成:{path}");
                    return;
                }
                // ⚠️ **拒写,不是覆盖** —— 旧件是已签核的走查记录/清单,覆盖 = 销毁证据。
                //    内容哈希命名让「同路径异内容」在生产里**结构上不可能**(路径 = f(内容)),
                //    故这条分支正常情况下永不命中;它存在是为了把「不可能」从**命名纪律**升级为
                //    **断言兜底** —— 万一将来有人传错路径,静默覆盖不会发生。
                Debug.LogError($"[E5] 拒写:目标已存在且内容不同(不覆盖已签核件):{path}");
                return;
            }
            File.WriteAllText(path, content, new UTF8Encoding(false));
            Debug.Log($"[E5] 清单已写:{path}");
        }

        private string BuildRowString(int ordinal, AudioEventRow row, string subtitle, float? occ) =>
            "{ \"ordinal\": " + ordinal.ToString(CultureInfo.InvariantCulture) +
            ", \"cue\": " + Quote(row.Cue) +
            ", \"first_asset\": " + Quote(row.Assets[0]) +
            ", \"loop\": " + (row.Loop ? "true" : "false") +
            ", \"occupancy\": " + (occ == null ? "null" : occ.Value.ToString("0.0000", CultureInfo.InvariantCulture)) +
            ", \"whitelist_category\": " + Quote(row.WhitelistCategory) +
            ", \"trigger_source\": " + Quote(row.TriggerSource) +
            ", \"subtitle_text\": " + (subtitle == null ? "null" : Quote(subtitle)) +
            ", \"reason\": " + Quote(CueTextOrderingRules.Reason(row.Cue, row.WhitelistCategory ?? "<缺>", subtitle)) +
            " }";

        private static string BuildManifestJson(AudioEventDataSet table, IReadOnlyList<string> rows)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"schema_version\": ").Append(SchemaVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"_note\": ").Append(Quote(
                "AC-44-E5 手跑走查清单 —— 由 StateReportWalkthroughProbe 播放**全部**事件表 cue 生成。" +
                "reason 前缀 = 归属四选一(physiology / player_action / world_ambience / music_layer);" +
                "走查据此**逐条**对照 design/gdd/audio-system.md:66-78 黑名单四禁判定,再在谓词文件里落结果。" +
                "本文件不承载任何判定 —— 判定只存在于谓词文件 + 记录文件。")).Append(",\n");
            sb.Append("  \"event_table_schema_version\": ")
              .Append(table.SchemaVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"event_table_config_version\": ")
              .Append(table.ConfigVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"rows\": [\n");
            for (int i = 0; i < rows.Count; i++)
                sb.Append("    ").Append(rows[i]).Append(i + 1 < rows.Count ? ",\n" : "\n");
            sb.Append("  ]\n}\n");
            return sb.ToString();
        }

        /// <summary>重跑谓词 → 走查记录(Markdown)。所有判定问题收进 <paramref name="problems"/>。</summary>
        private string ReplayPredicates(
            string predicatesPath, AudioEventDataSet table, Dictionary<string, AudioClip> clips,
            List<string> silent, Dictionary<string, string> subtitles, List<string> problems)
        {
            return ReplayPredicatesFromText(File.ReadAllText(predicatesPath), table, clips,
                silent, subtitles, problems);
        }

        /// <summary>谓词重跑本体(纯文本入口 —— 夹具直驱,免落盘)。</summary>
        private static string ReplayPredicatesFromText(
            string json, AudioEventDataSet table, Dictionary<string, AudioClip> clips,
            List<string> silent, Dictionary<string, string> subtitles, List<string> problems)
        {
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string error))
            {
                problems.Add($"[E5 · 谓词] 词法失败:{error}");
                return null;
            }

            string reviewer = Str(root, "reviewer");
            string date = Str(root, "date");
            if (string.IsNullOrWhiteSpace(reviewer)) problems.Add("[E5 · 谓词] 缺 reviewer(走查人)");
            if (!DateTime.TryParseExact(date ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
                problems.Add($"[E5 · 谓词] date 缺失或非 YYYY-MM-DD:「{date}」");

            // cue → 听到的状态结论列表(null status_words = 空列表 = 不是状态播报)
            var verdicts = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            if (!root.TryGet("verdicts", out JsonNode verdictsNode) || verdictsNode?.Items == null)
            {
                problems.Add("[E5 · 谓词] 缺 verdicts[]");
            }
            else
            {
                foreach (JsonNode v in verdictsNode.Items)
                {
                    string cue = v == null ? null : Str(v, "cue");
                    if (string.IsNullOrEmpty(cue)) { problems.Add("[E5 · 谓词] 存在缺 cue 的判定"); continue; }

                    if (string.IsNullOrWhiteSpace(Str(v, "not_reporting")))
                        problems.Add($"[E5 · 谓词] 「{cue}」缺 not_reporting(参考解读)");

                    var words = new List<string>();
                    if (!v.TryGet("status_words", out JsonNode sw) || sw == null || sw.Kind == JsonNodeKind.Null)
                    {
                        // null = 未听到任何状态结论 —— 合法判定
                    }
                    else if (sw.Kind == JsonNodeKind.Array && sw.Items != null)
                    {
                        foreach (JsonNode w in sw.Items)
                            if (w?.Str != null && w.Str.Trim().Length > 0) words.Add(w.Str.Trim());
                    }
                    else problems.Add($"[E5 · 谓词] 「{cue}」status_words 须为 null 或字符串数组");

                    if (!verdicts.TryAdd(cue, words)) problems.Add($"[E5 · 谓词] cue「{cue}」重复判定");
                }
            }

            var lines = new List<string>();
            for (int i = 0; i < table.RowCount; i++)
            {
                AudioEventRow row = table.Row(i);
                if (!verdicts.TryGetValue(row.Cue, out List<string> words))
                {
                    problems.Add($"[E5 · 谓词] 清单 cue「{row.Cue}」无判定 —— 走查须逐条");
                    continue;
                }
                if (silent.Contains(row.Cue))
                    problems.Add($"[E5 · 谓词] 「{row.Cue}」素材**静音**,走查人无从听到 —— 该判定无效");

                subtitles.TryGetValue(row.Cue, out string subtitle);
                foreach (string w in words)
                {
                    bool inSubtitle = subtitle != null && subtitle.IndexOf(w, StringComparison.Ordinal) >= 0;
                    if (!CueTextOrderingRules.ContainsConclusion(w) && !inSubtitle)
                        problems.Add($"[E5 · 谓词] 「{row.Cue}」状态词「{w}」既不在字幕内也不在结论词形内" +
                                     " —— 听到的结论须能落到文本或黑名单句式");
                    problems.Add($"[E5 · 判定] 「{row.Cue}」听到状态结论「{w}」—— 黑名单第 4 条命中");
                }

                lines.Add($"| {i} | `{row.Cue}` | {row.WhitelistCategory} | " +
                          $"{(clips.TryGetValue(row.Cue, out AudioClip c) && c != null ? c.length.ToString("0.###", CultureInfo.InvariantCulture) : "?")} s | " +
                          $"{(subtitle == null ? "—" : "`" + subtitle + "`")} | " +
                          $"{CueTextOrderingRules.Reason(row.Cue, row.WhitelistCategory ?? "<缺>", subtitle).Replace("|", "\\|")} | " +
                          $"{(words.Count == 0 ? "无" : "「" + string.Join("」「", words) + "」")} | " +
                          $"{(words.Count == 0 ? "✅ 不是状态播报" : "❌ 是状态播报")} |");
            }

            if (problems.Count > 0) return null;

            var md = new StringBuilder();
            md.Append("# AC-44-E5 无提示音铁律 · 手跑走查记录(").Append(date).Append(")\n\n");
            md.Append("> 机器半:`StateReportWalkthroughProbe.walkthrough_allCues_manifestPlaybackAndOrdering`");
            md.Append("(全 ").Append(table.RowCount.ToString(CultureInfo.InvariantCulture)).Append(" cue 真播)。\n");
            md.Append("> 人半:走查人 `").Append(reviewer).Append("` 逐条耳朵判定(谓词文件重跑通过)。\n\n");
            md.Append("**判定依据**:`design/gdd/audio-system.md:66-78` 规则二黑名单四禁" +
                      "(sting / jingle 宣告状态变化 · ducking 报状态 · 素材切换报状态 · 音频表达该由读数得出的结论)。\n\n");
            md.Append("| # | cue | whitelist_category | 时长 | 字幕 | 参考解读 | 听到的状态结论 | 判定 |\n");
            md.Append("|---|---|---|---|---|---|---|---|\n");
            foreach (string l in lines) md.Append(l).Append('\n');
            md.Append("\n**总计**:机器半全 ").Append(table.RowCount.ToString(CultureInfo.InvariantCulture))
              .Append(" cue 均在谓词文件内 · 人半 ").Append(lines.Count.ToString(CultureInfo.InvariantCulture))
              .Append(" 条判定。\n\n");
            md.Append("## 签字\n\n");
            md.Append("| 角色 | 姓名 | 日期 | 判定 |\n|---|---|---|---|\n");
            md.Append("| 音频 / QA(人耳走查) | ").Append(reviewer).Append(" | ").Append(date)
              .Append(" | [ ] 通过 |\n");
            md.Append("\n> ⚠️ **未勾选 = E5 未通过**。机器半全绿只说明「清单齐、都出了声、文字没先于声音」;" +
                      "「声音本身不是在报状态」只能由此表的耳朵判定给出。\n");
            return md.ToString();
        }

        private static string Str(JsonNode obj, string key)
            => obj != null && obj.TryGet(key, out JsonNode n) && n != null && n.Kind == JsonNodeKind.String
                ? n.Str : null;

        private static string Quote(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
