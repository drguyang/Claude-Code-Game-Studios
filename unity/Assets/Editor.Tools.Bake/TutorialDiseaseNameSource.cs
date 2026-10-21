// STORY-001 (tutorial-onboarding) · 校验规则 ④ 的**生产**病名闭集来源。
//
// 权威来源:
//   design/gdd/tutorial-and-onboarding.md:270 / :550 —— AC-48-13:扫描字典 = 9 的病种名表。
//   design/gdd/disease-simulation.md:386-405(§R3 病名枚举,P0 · 8 项,优先冻结)· :429-435(逐病行)。
//   ADR-014 —— 作者态数据一律 `assets/data/[system]_[name].json`;构建期读源、运行期不读 JSON。
//
// 为什么需要本件(2026-10-21):
//   `ITutorialDiseaseNameSource` 此前**生产路径零实现者** ⇒ 校验规则 ④ 恒为 `TableNotReady`,
//   AC-48-13 只能记 NOT-RUN。本件把「词表从哪来」这条路径打通,使 ④ 在词表就位时**真跑**。
//
// ⚠️ 缺席语义(承接口注释):**文件不存在 ⇒ 返回 false**(第 ④ 项记 NOT-RUN,不阻断烘焙)。
//   **但文件存在却损坏 / 为空 ⇒ 抛 `BakeValidationException`** —— 损坏不得静默降级成
//   「未就绪」,否则一张写坏的词表会让 ④ 永远 NOT-RUN 而无人察觉(管辖「借绿」的同族风险)。
//   空表同理:零词条的扫描对任何文本都「零命中」⇒ 那是**假绿**,必须硬失败。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>病名闭集的生产来源:读 <c>assets/data/disease_names.json</c>(规则 ④ 的扫描字典)。</summary>
    public sealed class TutorialDiseaseNameSource : ITutorialDiseaseNameSource
    {
        /// <summary>数据文件名(<c>assets/data/</c> 下)。</summary>
        public const string FileName = "disease_names.json";

        private readonly string _repoRoot;

        /// <summary>构造。</summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/data/</c> 的目录)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="repoRoot"/> 为 null。</exception>
        public TutorialDiseaseNameSource(string repoRoot)
        {
            _repoRoot = repoRoot ?? throw new ArgumentNullException(nameof(repoRoot));
        }

        /// <summary>数据件完整路径(诊断用)。</summary>
        public string SourcePath => Path.Combine(_repoRoot, TutorialContentSchema.DataDirName, FileName);

        /// <inheritdoc/>
        /// <exception cref="BakeValidationException">文件存在但损坏 / 结构不符 / 零词条。</exception>
        public bool TryGetDiseaseNames(out IReadOnlyCollection<string> names)
        {
            names = null;

            string path = SourcePath;
            if (!File.Exists(path)) return false;   // 缺席 = NOT-RUN(文档化语义,非错误)

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                throw new BakeValidationException(new List<string>
                {
                    $"[④] 病名闭集读取失败:{path} —— {ex.Message}",
                });
            }

            names = ParseNames(json, path);
            return true;
        }

        /// <summary>
        /// 解析词表文本(与读盘分离 —— 使「损坏 / 空表」两条判据可在**不落盘**的夹具里证明)。
        /// </summary>
        /// <param name="json">词表 JSON 文本。</param>
        /// <param name="origin">错误报文里的来源标识(默认取「内存字符串」)。</param>
        /// <returns>去重保序的病名闭集;恒非空(空表 = 抛,见下)。</returns>
        /// <exception cref="BakeValidationException">词法不合法 / 结构不符 / 零词条。</exception>
        public static IReadOnlyCollection<string> ParseNames(string json, string origin = "内存字符串")
        {
            var errors = new List<string>();
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexError))
            {
                errors.Add($"[④] 病名闭集 JSON 不合法:{origin} —— {lexError}");
                throw new BakeValidationException(errors);
            }

            IReadOnlyCollection<string> names = ExtractNames(root, origin, errors);
            if (errors.Count > 0) throw new BakeValidationException(errors);
            return names;
        }

        /// <summary>从词法树取全部 <c>diseases[].displayNames[]</c>(去重保序)。</summary>
        private static IReadOnlyCollection<string> ExtractNames(JsonNode root, string path, List<string> errors)
        {
            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"[④] 病名闭集顶层必须是对象:{path}");
                return null;
            }
            if (!root.TryGet("diseases", out JsonNode arr) || arr.Kind != JsonNodeKind.Array)
            {
                errors.Add($"[④] 病名闭集缺 `diseases` 数组:{path}");
                return null;
            }

            var names = new List<string>();
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode entry = arr.Items[i];
                if (entry.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"[④] diseases[{i}] 不是对象:{path}");
                    continue;
                }
                if (!entry.TryGet("displayNames", out JsonNode list) || list.Kind != JsonNodeKind.Array)
                {
                    errors.Add($"[④] diseases[{i}] 缺 `displayNames` 数组:{path}");
                    continue;
                }
                for (int j = 0; j < list.Items.Count; j++)
                {
                    JsonNode item = list.Items[j];
                    if (item.Kind != JsonNodeKind.String || string.IsNullOrEmpty(item.Str))
                    {
                        errors.Add($"[④] diseases[{i}].displayNames[{j}] 不是非空字符串:{path}");
                        continue;
                    }
                    if (!names.Contains(item.Str)) names.Add(item.Str);
                }
            }

            // 零词条 = 扫描对任何文本都「零命中」⇒ 假绿。硬失败(承文件头注)。
            if (errors.Count == 0 && names.Count == 0)
                errors.Add($"[④] 病名闭集为空(0 个词条)⇒ 规则 ④ 会平凡通过 —— 这是假绿,构建期拒收:{path}");

            return names;
        }
    }
}
