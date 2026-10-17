// story-016 · AC-44-E5(无提示音铁律 · **语义面**)—— **PlayMode 手跑走查**(用户 2026-10-17 裁定)。
//
// 权威来源:
//   GDD design/gdd/audio-system.md:66-78(规则二 · 行为反馈白名单 vs 状态播报黑名单)
//   ADR-018 §六(无提示音铁律机械化 = 白名单断言;AC-44-09 BLOCKING)
//   design/assets/specs/audio-material-manifest.md §四(逐项「替代读法」)
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ **本件 = 走查骨架,机器半已接齐;走查触发仍停用。当下 E5 仍是 ✗(禁借绿)。**
//
// 2026-10-17 三道阻塞的现状:
//
// ① **文本半够不着词法器** —— ✅ **已解**。根因不是 asmdef(我首版把 `internalsVisibleTo` 填进 asmdef,
//    两次纯文本替换都没生效且第二次弄出重复键,Unity 静默忽略);本工程的成例是**源码里的
//    `AssemblyInfo.cs`**(`Sim.Codec` / `Editor.Tools.Spike` 先例)。已补
//    `Editor.Tools.Bake/AssemblyInfo.cs`,文本半现与门同源(`JsonStage1Lexer`)。
// ② **真播放的批次语义未定** —— EditMode 批次内 `AudioSource.Play` 是否真出声**未实测**;
//    PlayMode 装配文件头的「真不出声」是**历史观察**而非判据。**未实测前不写进任何结论。**
// ③ **本机无交互式编辑器** —— 走查(听)必须人在场;batch 只能跑机器半。
//
// ⚠️ **走查触发仍停用**(`[UnityTest]` 首行 `Assert.Ignore`):②③ 未解前触发只会绿得毫无意义。
//    停用是**显式状态**,不是「跑过了」。
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
using UnityEngine;
using UnityEngine.TestTools;

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

        /// <summary>逐采样占空比(0 = 全零 = 不可闻;NaN/全零之外的读法见报文)。</summary>
        private static float Occupancy(AudioClip clip)
        {
            if (clip == null || clip.samples <= 0 || clip.channels <= 0) return 0f;
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            long plan = (long)clip.samples * clip.channels;
            int take = (int)Math.Min(plan, 4L * 65536);
            var buf = new float[take];
            if (!clip.GetData(buf, 0)) return 0f;
            int nonZero = 0;
            for (int i = 0; i < buf.Length; i++)
                if (buf[i] != 0f) nonZero++;
            return buf.Length == 0 ? 0f : (float)nonZero / buf.Length;
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
        /// </summary>
        [Test]
        public void execGate_audioDevice_isPresent()
        {
            AudioConfiguration cfg = AudioSettings.GetConfiguration();
#if UNITY_EDITOR
            bool speakers = cfg.speakerMode != AudioSpeakerMode.Mono;
            Assert.That(speakers, Is.True,
                $"无音频设备(回退为 {cfg.speakerMode} 虚拟设备)—— 本机未走查,E5 不得记通过。" +
                "请在有声卡的会话里重跑。");
#else
            var caps = AudioSettings.driverCapabilities;
            Assert.That((caps & AudioSpeakerMode.BuiltInSpeakers) != 0, Is.True,
                $"无内置扬声器(能力 = {caps})—— E5 手跑走查不可执行。");
#endif
        }

        // ══════════════ ② 走查清单 + 实际播放 + 文本先后 ══════════════

        /// <summary>
        /// 全 15 cue 的走查:整理清单(含逐项替代读法)、逐条真播、报告出声数、判定文本先后,
        /// 最后(谓词文件存在时)重跑判定并落走查记录。
        /// </summary>
        [UnityTest]
        public IEnumerator walkthrough_allCues_manifestPlaybackAndOrdering()
        {
            Assert.Ignore("走查触发已停用 —— 见文件头「剩余两道阻塞」(② 批次是否真出声未实测 · ③ 走查须人在场);触发前 E5 不记通过。");
            _repoRoot = RepoRoot();
            AudioEventDataSet table = LoadRealTable();
            Dictionary<string, string> subtitles = ReadSourceSubtitles(_repoRoot);

            var strings = new List<string>(table.RowCount);
            var clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            var audible = new List<string>();
            var silent = new List<string>();
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

                    float occ = Occupancy(clip);
                    (occ > 0f ? audible : silent).Add(row.Cue);
                    Debug.Log($"[E5] 播放 {i + 1}/{table.RowCount}: {row.Cue} · {row.Assets[0]}" +
                              $" · loop={row.Loop} · {clip.length.ToString("0.###", CultureInfo.InvariantCulture)} s" +
                              $" · 占空比 {occ.ToString("P0", CultureInfo.InvariantCulture)}");

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
            if (emptySubtitle.Count > 0)
                Debug.Log($"[E5] ⚠️ 循环行带 subtitle_text(文本半的前提受损):" + string.Join(", ", emptySubtitle));

            Assert.That(audible.Count, Is.GreaterThan(0),
                "全部 cue 静音 —— 机器半无法支撑人耳走查,判定无效");

            // ① 清单(首次生成;已存在则不覆盖 —— 第二版确认靠谓词文件 + 记录文件)
            string manifestPath = Path.Combine(_repoRoot, WalkthroughDirRel, ManifestFileName());
            WriteOnce(manifestPath, BuildManifestJson(table, strings));

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

            File.WriteAllText(Path.Combine(_repoRoot, WalkthroughDirRel, RecordFileName()), markdown,
                new UTF8Encoding(false));
            Debug.Log($"[E5] 走查记录已写:{Path.Combine(WalkthroughDirRel, RecordFileName())}" +
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

        internal static string ManifestFileName() => $"statereport-walkthrough-manifest-{TodayStamp()}.json";

        internal static string RecordFileName() => $"statereport-walkthrough-{TodayStamp()}.md";

        private static string TodayStamp() => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static bool FileFresh(string path)
            => File.Exists(path) && (DateTime.Now - File.GetLastWriteTime(path)).TotalDays <= 1.0;

        private static void WriteOnce(string path, string content)
        {
            // 首次生成:目录尚不存在 ⇒ 建之;已存在 ⇒ 不触碰(不覆盖旧清单,也不动目录)。
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (FileFresh(path))
            {
                Debug.Log($"[E5] 清单已存在,不覆盖:{path}");
                return;
            }
            File.WriteAllText(path, content, new UTF8Encoding(false));
            Debug.Log($"[E5] 清单已写:{path}");
        }

        private string BuildRowString(int ordinal, AudioEventRow row, string subtitle, float occ) =>
            "{ \"ordinal\": " + ordinal.ToString(CultureInfo.InvariantCulture) +
            ", \"cue\": " + Quote(row.Cue) +
            ", \"first_asset\": " + Quote(row.Assets[0]) +
            ", \"loop\": " + (row.Loop ? "true" : "false") +
            ", \"occupancy\": " + occ.ToString("0.0000", CultureInfo.InvariantCulture) +
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
                "reason 前缀 = 归属三选一(physiology / player_action / world_ambience);" +
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
