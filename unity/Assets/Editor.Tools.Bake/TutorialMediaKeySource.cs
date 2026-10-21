// STORY-001 (tutorial-onboarding) · 校验规则 ② 外部半边的**生产**媒体键来源。
//
// 权威来源:
//   design/gdd/tutorial-and-onboarding.md:252-253 —— narration[].cueId → 44 表;
//     demonstrations[].clipKey → 13 示范片段表(本件补的正是后者)。
//   design/gdd/tutorial-and-onboarding.md:266 —— 引用完整性:悬垂键 = 硬失败。
//   ADR-014 —— 作者态数据一律 `assets/data/[system]_[name].json`;构建期读源、运行期不读 JSON。
//
// 为什么需要本件(2026-10-21 · C-1):
//   `ITutorialMediaKeySource` 此前**生产路径零实现者**(只有测试里的 `FakeMediaKeys`)
//   ⇒ 校验规则 ② 的外部半边恒为 NOT-RUN,`CueIdTableReady` / `ClipKeyTableReady` 恒 false。
//   本件把两条键空间的来源打通:
//     · cueId   ← `assets/data/audio_events.json` 的 `rows[].cue`(44 侧真源)
//     · clipKey ← `assets/data/48_demonstration_clips.json` 的 `clips[].clipKey`(13 侧真源)
//
// ⚠️ 缺席语义(承接口注释与 `TutorialDiseaseNameSource` 同款):
//   **单个文件不存在 ⇒ 该半边返回 false**(记 NOT-RUN,不阻断烘焙)。
//   **但文件存在却损坏 / 结构不符 / 为空 ⇒ 抛 `BakeValidationException`** ——
//   损坏不得静默降级成「未就绪」,否则一张写坏的表会让该半边永远 NOT-RUN 而无人察觉。
//   空表同理:零键的闭集对任何引用都「零命中反例」⇒ 那是**假绿**,必须硬失败。
//
// ⚠️ **两个半边各自独立**:cue 表在而 clip 表缺席 ⇒ `CueIdTableReady = true` /
//   `ClipKeyTableReady = false`,不得因为一个缺席就把另一个也记成 NOT-RUN
//   (那会把「已就绪」的部分白白降级,是另一种假绿)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>媒体键闭集的生产来源:44 的 cue 表 + 13 的示范片段表(规则 ② 的外部半边)。</summary>
    public sealed class TutorialMediaKeySource : ITutorialMediaKeySource
    {
        /// <summary>44 侧 cue 表文件名(<c>assets/data/</c> 下)。</summary>
        public const string AudioEventsFileName = "audio_events.json";

        /// <summary>13 侧示范片段表文件名(<c>assets/data/</c> 下)。</summary>
        public const string DemonstrationClipsFileName = "48_demonstration_clips.json";

        private readonly string _repoRoot;

        /// <summary>构造。</summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/data/</c> 的目录)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="repoRoot"/> 为 null。</exception>
        public TutorialMediaKeySource(string repoRoot)
        {
            _repoRoot = repoRoot ?? throw new ArgumentNullException(nameof(repoRoot));
        }

        /// <summary>44 cue 表完整路径(诊断用)。</summary>
        public string CueTablePath =>
            Path.Combine(_repoRoot, TutorialContentSchema.DataDirName, AudioEventsFileName);

        /// <summary>13 片段表完整路径(诊断用)。</summary>
        public string ClipTablePath =>
            Path.Combine(_repoRoot, TutorialContentSchema.DataDirName, DemonstrationClipsFileName);

        /// <inheritdoc/>
        /// <exception cref="BakeValidationException">文件存在但损坏 / 结构不符 / 零 cue。</exception>
        public bool TryGetCueIds(out IReadOnlyCollection<string> cueIds)
        {
            cueIds = null;

            string path = CueTablePath;
            if (!File.Exists(path)) return false;   // 缺席 = NOT-RUN(文档化语义,非错误)

            string json = ReadOrThrow(path, "② cueId 表");
            cueIds = ParseCueIds(json, path);
            return true;
        }

        /// <inheritdoc/>
        /// <exception cref="BakeValidationException">文件存在但损坏 / 结构不符 / 零 clipKey。</exception>
        public bool TryGetClipKeys(out IReadOnlyCollection<string> clipKeys)
        {
            clipKeys = null;

            string path = ClipTablePath;
            if (!File.Exists(path)) return false;

            string json = ReadOrThrow(path, "② clipKey 表");
            clipKeys = ParseClipKeys(json, path);
            return true;
        }

        private static string ReadOrThrow(string path, string what)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                throw new BakeValidationException(new List<string>
                {
                    $"[②] {what}读取失败:{path} —— {ex.Message}",
                });
            }
        }

        /// <summary>
        /// 解析 44 cue 表文本(与读盘分离 —— 使「损坏 / 空表」判据可在**不落盘**的夹具里证明)。
        /// </summary>
        /// <param name="json">表 JSON 文本。</param>
        /// <param name="origin">错误报文里的来源标识。</param>
        /// <returns>去重保序的 cue 闭集;恒非空(空表 = 抛)。</returns>
        /// <exception cref="BakeValidationException">词法不合法 / 结构不符 / 零 cue。</exception>
        public static IReadOnlyCollection<string> ParseCueIds(string json, string origin = "内存字符串")
        {
            var errors = new List<string>();
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexError))
            {
                errors.Add($"[②] cueId 表 JSON 不合法:{origin} —— {lexError}");
                throw new BakeValidationException(errors);
            }

            IReadOnlyCollection<string> ids = ExtractCueIds(root, origin, errors);
            if (errors.Count > 0) throw new BakeValidationException(errors);
            return ids;
        }

        /// <summary>
        /// 解析 13 片段表文本。
        /// </summary>
        /// <param name="json">表 JSON 文本。</param>
        /// <param name="origin">错误报文里的来源标识。</param>
        /// <returns>去重保序的 clipKey 闭集;恒非空(空表 = 抛)。</returns>
        /// <exception cref="BakeValidationException">词法不合法 / 结构不符 / 零 clipKey。</exception>
        public static IReadOnlyCollection<string> ParseClipKeys(string json, string origin = "内存字符串")
        {
            var errors = new List<string>();
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexError))
            {
                errors.Add($"[②] clipKey 表 JSON 不合法:{origin} —— {lexError}");
                throw new BakeValidationException(errors);
            }

            IReadOnlyCollection<string> keys = ExtractClipKeys(root, origin, errors);
            if (errors.Count > 0) throw new BakeValidationException(errors);
            return keys;
        }

        /// <summary>从词法树取 <c>rows[].cue</c>(去重保序)。</summary>
        private static IReadOnlyCollection<string> ExtractCueIds(JsonNode root, string path, List<string> errors)
        {
            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"[②] cueId 表顶层必须是对象:{path}");
                return null;
            }
            if (!root.TryGet("rows", out JsonNode arr) || arr.Kind != JsonNodeKind.Array)
            {
                errors.Add($"[②] cueId 表缺 `rows` 数组:{path}");
                return null;
            }

            var ids = new List<string>();
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode entry = arr.Items[i];
                if (entry.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"[②] rows[{i}] 不是对象:{path}");
                    continue;
                }
                if (!entry.TryGet("cue", out JsonNode cue) || cue.Kind != JsonNodeKind.String ||
                    string.IsNullOrEmpty(cue.Str))
                {
                    errors.Add($"[②] rows[{i}] 缺非空 `cue` 字符串:{path}");
                    continue;
                }
                if (!ids.Contains(cue.Str)) ids.Add(cue.Str);
            }

            // 零 cue = 闭集对任何引用都「零命中反例」⇒ 假绿。硬失败(承文件头注)。
            if (errors.Count == 0 && ids.Count == 0)
                errors.Add($"[②] cueId 表为空(0 条)⇒ 规则 ② 会平凡通过 —— 这是假绿,构建期拒收:{path}");

            return ids;
        }

        /// <summary>从词法树取 <c>clips[].clipKey</c>(去重保序)。</summary>
        private static IReadOnlyCollection<string> ExtractClipKeys(JsonNode root, string path, List<string> errors)
        {
            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"[②] clipKey 表顶层必须是对象:{path}");
                return null;
            }
            if (!root.TryGet("clips", out JsonNode arr) || arr.Kind != JsonNodeKind.Array)
            {
                errors.Add($"[②] clipKey 表缺 `clips` 数组:{path}");
                return null;
            }

            var keys = new List<string>();
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode entry = arr.Items[i];
                if (entry.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"[②] clips[{i}] 不是对象:{path}");
                    continue;
                }
                if (!entry.TryGet("clipKey", out JsonNode key) || key.Kind != JsonNodeKind.String ||
                    string.IsNullOrEmpty(key.Str))
                {
                    errors.Add($"[②] clips[{i}] 缺非空 `clipKey` 字符串:{path}");
                    continue;
                }
                if (!keys.Contains(key.Str)) keys.Add(key.Str);
            }

            if (errors.Count == 0 && keys.Count == 0)
                errors.Add($"[②] clipKey 表为空(0 条)⇒ 规则 ② 会平凡通过 —— 这是假绿,构建期拒收:{path}");

            return keys;
        }
    }
}
