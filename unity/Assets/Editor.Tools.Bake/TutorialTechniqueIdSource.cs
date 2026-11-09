// T8(2026-11-09)· 48 教学纸 ↔ 技法绑定的**生产**技法 id 来源。
//
// 权威来源:
//   · `design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2 —— 钩子病例的载体 = 手册缺页,
//     「病人身上能读出病名,翻手册时对应那页被撕掉,只剩一个标题」。
//   · ADR-043 §D5 —— **技法 id 空间 = `disease_action_axis.json` 的 action id**,零新 id 空间。
//   · ADR-014 —— 作者态一律 `assets/data/*.json`;构建期读源,运行期不读 JSON。
//
// 为什么需要本件(T8):
//   `papers[].teachTechnique` 是对 9 的处置轴的外部键。悬空 id 的后果**不是少一条数据**:
//   技法解锁查询对任何 actor 恒 false ⇒ 那一页**永久判为缺页**(读者:一本永远被撕过的书),
//   而构建 / 烘焙 / 夹具可以全绿。承 `LexiconRegistryCrossRefGate` 的同一次事故形态
//   (病名册 `disease_ids: [1]` 悬空 ⇒ 整本病名册在生产里恒空,而当时全部判据都是绿的)。
//
// ⚠️ 缺席语义(与 `TutorialDiseaseNameSource` 同款纪律):
//   **文件不存在 ⇒ 返回 false**(② 的这条半边记 NOT-RUN,不阻断烘焙);
//   **文件存在却损坏 / 无 `actions[]` / 零条 ⇒ 抛 `BakeValidationException`** ——
//   损坏不得静默降级成「未就绪」,那会让校验永远 NOT-RUN 而无人察觉。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>技法 id 闭集的生产来源:读 <c>assets/data/disease_action_axis.json</c> 的 <c>actions[].id</c>。</summary>
    public sealed class TutorialTechniqueIdSource : ITutorialTechniqueIdSource
    {
        /// <summary>数据文件名(`assets/data/` 下;与 9 的处置轴同源,不另立词表)。</summary>
        public const string FileName = DiseaseActionAxisBaker.AxisFileName;

        private readonly string _repoRoot;

        /// <summary>构造。</summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/data/</c> 的目录)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="repoRoot"/> 为 null。</exception>
        public TutorialTechniqueIdSource(string repoRoot)
        {
            _repoRoot = repoRoot ?? throw new ArgumentNullException(nameof(repoRoot));
        }

        /// <summary>数据件完整路径(诊断用)。</summary>
        public string SourcePath => Path.Combine(_repoRoot, TutorialContentSchema.DataDirName, FileName);

        /// <inheritdoc/>
        /// <exception cref="BakeValidationException">文件存在但损坏 / 结构不符 / 零条。</exception>
        public bool TryGetTechniqueIds(out IReadOnlyCollection<int> ids)
        {
            ids = null;

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
                    $"[②] 技法 id 闭集读取失败:{path} —— {ex.Message}",
                });
            }

            ids = ParseIds(json, path);
            return true;
        }

        /// <summary>
        /// 解析 <c>actions[].id</c>(与读盘分离 —— 使「损坏 / 空表」两条判据可在**不落盘**的夹具里证明)。
        /// </summary>
        /// <param name="json">处置轴 JSON 文本。</param>
        /// <param name="origin">错误报文里的来源标识(默认取「内存字符串」)。</param>
        /// <returns>去重保序的技法 id 闭集;恒非空(空表 = 抛,见下)。</returns>
        /// <exception cref="BakeValidationException">词法不合法 / 结构不符 / 零条。</exception>
        public static IReadOnlyCollection<int> ParseIds(string json, string origin = "内存字符串")
        {
            var errors = new List<string>();
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexError))
            {
                errors.Add($"[②] 处置轴 JSON 不合法:{origin} —— {lexError}");
                throw new BakeValidationException(errors);
            }

            IReadOnlyCollection<int> ids = ExtractIds(root, origin, errors);
            if (errors.Count > 0) throw new BakeValidationException(errors);
            return ids;
        }

        /// <summary>从词法树取全部 <c>actions[].id</c>(去重保序)。</summary>
        private static IReadOnlyCollection<int> ExtractIds(JsonNode root, string path, List<string> errors)
        {
            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"[②] 处置轴顶层必须是对象:{path}");
                return null;
            }
            if (!root.TryGet("actions", out JsonNode arr) || arr.Kind != JsonNodeKind.Array)
            {
                errors.Add($"[②] 处置轴缺 `actions` 数组:{path} —— 技法 id 闭集**无从判定**" +
                           "(缺席必须与「零条」分开:前者是 NOT-RUN,后者是假绿)");
                return null;
            }

            var seen = new HashSet<int>();
            var ids = new List<int>();
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode entry = arr.Items[i];
                if (entry.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"[②] actions[{i}] 不是对象:{path}");
                    continue;
                }
                if (!entry.TryGet("id", out JsonNode idNode) || idNode.Kind != JsonNodeKind.Integer)
                {
                    errors.Add($"[②] actions[{i}] 缺整数 `id`:{path}");
                    continue;
                }
                if (!int.TryParse(idNode.RawText, out int id))
                {
                    errors.Add($"[②] actions[{i}].id「{idNode.RawText}」不是整数:{path}");
                    continue;
                }
                if (seen.Add(id)) ids.Add(id);
            }

            if (errors.Count == 0 && ids.Count == 0)
            {
                // 零条 ⇒ 任何 teachTechnique 都会判悬空,而「无技法可教」不是合法状态。
                errors.Add($"[②] 处置轴 `actions` 零条:{path} —— " +
                           "零条闭集会让每一条 teachTechnique 都判悬空(假绿面)。");
            }

            ids.Sort();   // 稳定输出(报文可复现);集语义不变。
            return ids;
        }
    }
}
