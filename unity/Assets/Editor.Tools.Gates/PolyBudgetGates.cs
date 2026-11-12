// 3D 面数门 —— §8.5 档位表 + §8.10.6.3 `budget_tier` 的**执行体**(2026-11-12 立)。
//
// ══════════════════════════════════════════════════════════════════════════
// 为什么需要它 —— 表里有一行 ≠ 有门
// ══════════════════════════════════════════════════════════════════════════
// `art-bible.md` §8.10.6.5 一直写着一张「构建期 fail-closed 门」的表,而实测
// `unity/Assets/Editor.Tools.Gates/` 下**零** mesh / 面数相关门
// (`grep -rn "vertexCount|sharedMesh|GetIndexCount"` 零命中)。
// ⇒ 3D 资产的面数上限从来没有执行体;2026-11-12 的特许(Seed3D 试件 ~50 万面)
// 是因为**没人会红**,不是因为「批过了」。
//
// ══════════════════════════════════════════════════════════════════════════
// 输入 = spec 里的 ```budget 块(单一真源)
// ══════════════════════════════════════════════════════════════════════════
// 不另建清单文件 —— §8.10.6.3 已把 `budget_tier` 定为**资产 spec 的字段**,
// 再造一份 JSON 就是第二真源。门只读 `design/assets/specs/*.md` 里形如
//
//   ```budget
//   asset: pending            # 或 Assets/Art/.../x.fbx
//   family: building-module
//   tier: hero
//   scene: city-ruins
//   ```
//
// 的行式块。块外的一切(散文、表格)不解析 —— 那会把排版当数据。
//
// ══════════════════════════════════════════════════════════════════════════
// 判据(逐条都能构造反例)
// ══════════════════════════════════════════════════════════════════════════
// P1 family 必属 §8.5 的 11 档;否则红(防拼写错误静默通过)。
// P2 tier ∈ {normal, hero};缺省 normal。
// P3 **hero 只对 building-module 合法**(§8.5 的 hero 档只开在建筑模块上)。
// P4 **每场景 hero ≤ 3**(§8.5 的防滥用名额);未写 scene 的 hero 直接红。
// P5 三角面 > 该档上界 ⇒ 红。`asset: pending` ⇒ **不算红**,记「未测」(warn)。
// P6 `asset: Assets/...` 但文件不存在 ⇒ 红(fail-closed;防笔误把测量静默跳过)。
// P7 同一 asset 被两块声明 ⇒ 红。
// P8 **声明数 = 0 ⇒ 红**(输入面丢失;与 InputBoundaryGates 的 roots==0 同族)。
//
// ⚠️ P5 的 "pending" 不是一个松弛项:它必须在报告里**显式计数**,
//    否则 2 条声明 → 0 条实测会被读成「2 件都合格」。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>3D 资产面数门(§8.5 + §8.10.6.3)。</summary>
    public static class PolyBudgetGates
    {
        /// <summary>资产 spec 目录(仓库相对)。</summary>
        public const string SpecRelDir = "design/assets/specs";

        /// <summary>块起始围栏(精确匹配,不认 `Budget` / `budget ` 变体)。</summary>
        public const string BlockOpen = "```budget";

        /// <summary>每个场景的 hero 名额(§8.5「每张场景至多 3 件」)。</summary>
        public const int HeroQuotaPerScene = 3;

        /// <summary>`asset:` 的「尚未入库」哨兵。**不是**宽松值 —— 见类头 P5 注。</summary>
        public const string PendingAsset = "pending";

        /// <summary>§8.5 面数上界(三角面/件)。**改这里之前先改 art-bible §8.5**。</summary>
        private static readonly Dictionary<string, int> Ceilings = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "character", 40000 },
            { "companion", 40000 },
            { "patient", 30000 },
            { "enemy-human", 25000 },
            { "enemy-beast", 50000 },
            { "prop-interactive", 5000 },
            { "prop-decor", 3000 },
            { "building-module", 8000 },   // normal;hero 见 HeroCeiling
            { "building-main", 20000 },
            { "vegetation", 3000 },
            { "ui-3d-paper", 6000 },
        };

        /// <summary>建筑模块的近景 hero 上界(§8.5 · 2026-11-12 新增档)。</summary>
        public const int BuildingModuleHeroCeiling = 20000;

        /// <summary>`asset_home` 缺省值:资产随仓库走,缺席即红(P6)。</summary>
        public const string AssetHomeRepo = "repo";

        /// <summary>
        /// `asset_home: local-only` —— 资产按**策略**不入库(第三方 ToS 未结 / 体积),
        /// 因此本机缺席**不是**故障。见 P9/P10。
        /// </summary>
        public const string AssetHomeLocalOnly = "local-only";

        /// <summary>一个 ```budget 块。</summary>
        public sealed class Declaration
        {
            public string SpecRelPath;
            public int Line;
            public string Asset;
            public string Family;
            public string Tier;
            public string Scene;

            /// <summary>`repo`(缺省)/ `local-only`。见 P9。</summary>
            public string AssetHome;

            public bool IsHero => string.Equals(Tier, "hero", StringComparison.Ordinal);

            public bool IsLocalOnly => string.Equals(AssetHome, AssetHomeLocalOnly, StringComparison.Ordinal);
        }

        /// <summary>门的一次判决:错 = 失败;警告 = 未测,不失败但必须可见。</summary>
        public sealed class Report
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();

            /// <summary>
            /// 逐件实测读数(仅记录,不参与判决)。
            /// ⚠️ 没有这一组,「实测 2 / 绿」只证明两件都 ≤ 上界,**报不出数字** ——
            /// 数字不可复核就等于没有数字(见 coding-standards §Review Evidence Standards)。
            /// </summary>
            public readonly List<string> Notes = new List<string>();

            /// <summary>声明条数。</summary>
            public int Declared;

            /// <summary>真的量到三角面的条数。</summary>
            public int Measured;

            /// <summary>`asset: pending` 的条数。</summary>
            public int Pending;

            /// <summary>
            /// `asset_home: local-only` 且本机缺席的条数 —— **按策略未测,不是通过,也不是故障**。
            /// ⚠️ 单列这个数,是为了让「未测」的两种来源(pending = 还没接 / 本地缺席 = 按策略不在库里)
            /// 在日志里可分辨;混成一个数会让「50 件都没测」看起来像「都合格」。
            /// </summary>
            public int LocalAbsent;
        }

        // ═══ 解析 ═══

        /// <summary>解析一份 spec 的全部 ```budget 块。块外文本不解析。</summary>
        public static List<Declaration> ParseSpec(string specRelPath, string text, List<string> errors)
        {
            var list = new List<Declaration>();
            if (text == null) return list;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                if (!string.Equals(lines[i].TrimEnd(), BlockOpen, StringComparison.Ordinal))
                {
                    i++;
                    continue;
                }

                int startLine = i + 1;
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                int j = i + 1;
                bool closed = false;
                for (; j < lines.Length; j++)
                {
                    string raw = lines[j].Trim();
                    if (raw == "```") { closed = true; break; }
                    if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal)) continue;

                    int colon = raw.IndexOf(':');
                    if (colon <= 0)
                    {
                        errors.Add($"[PolyBudget] {specRelPath}:{startLine} 块内行无法解析:`{raw}`(须 key: value)");
                        continue;
                    }

                    string key = raw.Substring(0, colon).Trim();
                    string val = raw.Substring(colon + 1).Trim();
                    if (key != "asset" && key != "family" && key != "tier" && key != "scene" && key != "asset_home")
                    {
                        errors.Add($"[PolyBudget] {specRelPath}:{startLine} 未知键 `{key}`(只认 asset/family/tier/scene/asset_home)");
                        continue;
                    }
                    if (map.ContainsKey(key))
                    {
                        errors.Add($"[PolyBudget] {specRelPath}:{startLine} 重复键 `{key}`");
                        continue;
                    }
                    map[key] = val;
                }

                if (!closed)
                {
                    errors.Add($"[PolyBudget] {specRelPath}:{startLine} ```budget 块未闭合");
                    break;
                }

                var d = new Declaration { SpecRelPath = specRelPath, Line = startLine };
                map.TryGetValue("asset", out d.Asset);
                map.TryGetValue("family", out d.Family);
                map.TryGetValue("tier", out d.Tier);
                map.TryGetValue("scene", out d.Scene);
                map.TryGetValue("asset_home", out d.AssetHome);
                if (string.IsNullOrEmpty(d.Tier)) d.Tier = "normal";
                if (string.IsNullOrEmpty(d.AssetHome)) d.AssetHome = AssetHomeRepo;

                // P9:asset_home 闭集。拼错的 `local_only` 若被静默当成 repo,会在新机器上把
                //     「按策略缺席」判成「文件丢失」—— 一个错字换来一条误导性的红。
                if (d.AssetHome != AssetHomeRepo && d.AssetHome != AssetHomeLocalOnly)
                {
                    errors.Add($"[PolyBudget] {specRelPath}:{startLine} `asset_home` 未知:`{d.AssetHome}`" +
                               $"(只认 {AssetHomeRepo} / {AssetHomeLocalOnly})");
                    // 不得 `continue`:那会跳过 `i = j + 1` ⇒ i 停在原块,同一个块被无限重解析,
                    // 每轮 errors.Add 一个新字符串 ⇒ 内存爆炸(v12…v21 卡死的根因,2026-11-12 实测定位)。
                    goto Advance;
                }
                list.Add(d);
            Advance:
                i = j + 1;
            }
            return list;
        }

        // ═══ 判决 ═══

        /// <summary>纯判决。<paramref name="triangleLookup"/> 返回 null ⇒ 文件不存在。</summary>
        public static Report Evaluate(IEnumerable<Declaration> declarations, Func<string, int?> triangleLookup)
        {
            var report = new Report();
            var list = (declarations ?? Enumerable.Empty<Declaration>()).ToList();
            report.Declared = list.Count;

            // P8:输入面丢失 ⇒ 红(空集上恒绿是本门最怕的形态)
            if (list.Count == 0)
            {
                report.Errors.Add("[PolyBudget] 扫描面为 0 —— 没有任何 spec 声明 ```budget 块;" +
                                  "门在空集上恒绿(假绿面),请查 spec 目录或块语法");
                return report;
            }

            var seenAssets = new Dictionary<string, string>(StringComparer.Ordinal);
            var heroPerScene = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var d in list)
            {
                string where = $"{d.SpecRelPath}:{d.Line}";

                // P1
                if (string.IsNullOrEmpty(d.Family) || !Ceilings.ContainsKey(d.Family))
                {
                    report.Errors.Add($"[PolyBudget] {where} family 未知:`{d.Family}`(须属 §8.5 的 11 档)");
                    continue;
                }

                // P2
                bool hero = d.IsHero;
                if (!hero && !string.Equals(d.Tier, "normal", StringComparison.Ordinal))
                {
                    report.Errors.Add($"[PolyBudget] {where} tier 未知:`{d.Tier}`(只认 normal / hero)");
                    continue;
                }

                // P3
                if (hero && d.Family != "building-module")
                {
                    report.Errors.Add($"[PolyBudget] {where} hero 档只对 building-module 合法,当前是 `{d.Family}`");
                    continue;
                }

                // P4
                if (hero)
                {
                    if (string.IsNullOrEmpty(d.Scene))
                    {
                        report.Errors.Add($"[PolyBudget] {where} hero 声明缺 `scene` ⇒ 名额无法计数");
                        continue;
                    }
                    heroPerScene.TryGetValue(d.Scene, out int n);
                    n++;
                    heroPerScene[d.Scene] = n;
                    if (n > HeroQuotaPerScene)
                        report.Errors.Add($"[PolyBudget] {where} 场景 `{d.Scene}` 的 hero 件数 {n} > {HeroQuotaPerScene}(§8.5 名额)");
                }

                // P7
                if (!string.IsNullOrEmpty(d.Asset) && d.Asset != PendingAsset)
                {
                    if (seenAssets.TryGetValue(d.Asset, out string first))
                        report.Errors.Add($"[PolyBudget] {where} asset `{d.Asset}` 重复声明(首次见 {first})");
                    else
                        seenAssets[d.Asset] = where;
                }

                // P5 / P6
                int ceiling = hero ? BuildingModuleHeroCeiling : Ceilings[d.Family];

                if (string.IsNullOrEmpty(d.Asset) || d.Asset == PendingAsset)
                {
                    // `pending`(还没接)与 `local-only`(已接但按策略不入库)互斥:
                    // 同时写意味着两套「缺席」口径叠在一件资产上,后续谁也说不清它到底有没有被量过。
                    if (d.IsLocalOnly)
                    {
                        report.Errors.Add($"[PolyBudget] {where} `asset: pending` 与 `asset_home: local-only` 互斥" +
                                          "(pending = 尚未接入;local-only = 已接入但按策略不入库)");
                        continue;
                    }
                    report.Pending++;
                    report.Warnings.Add($"[PolyBudget] {where} asset=pending ⇒ 三角面**未测**(不是通过);" +
                                        $"上界 {ceiling}({d.Family}{(hero ? "/hero" : "")})");
                    continue;
                }

                int? tris = triangleLookup != null ? triangleLookup(d.Asset) : null;
                if (!tris.HasValue)
                {
                    // P10:按策略不入库的资产,本机缺席 = 预期状态,**不是** fail-closed 的射程。
                    //      ⚠️ 但「文件在」的时候**照样量、照样红** —— 否则 local-only 会变成免检通道。
                    if (d.IsLocalOnly)
                    {
                        report.LocalAbsent++;
                        report.Warnings.Add($"[PolyBudget] {where} `{d.Asset}` 按策略不入库(asset_home: local-only)" +
                                            $"⇒ 本机缺席,三角面**未测**(不是通过);上界 {ceiling}" +
                                            $"({d.Family}{(hero ? "/hero" : "")})");
                        continue;
                    }
                    report.Errors.Add($"[PolyBudget] {where} 声明的 asset 不存在或不可导入:`{d.Asset}`" +
                                      "(fail-closed;未入库请写 `asset: pending`,按策略不入库请写 `asset_home: local-only`)");
                    continue;
                }

                report.Measured++;
                string verdict = tris.Value > ceiling ? "超" : "≤";
                report.Notes.Add($"[PolyBudget] {where} `{d.Asset}` 三角面 {tris.Value:N0} {verdict} 上界 {ceiling:N0}" +
                                 $"({d.Family}{(hero ? "/hero" : "")},用掉 {tris.Value * 100.0 / ceiling:F1}%)");
                if (tris.Value > ceiling)
                {
                    report.Errors.Add($"[PolyBudget] {where} `{d.Asset}` 三角面 {tris.Value:N0} > 上界 {ceiling:N0}" +
                                      $"({d.Family}{(hero ? "/hero" : "")},超 {tris.Value / (double)ceiling:F1}×)");
                }
            }

            return report;
        }

        // ═══ 生产入口 ═══

        /// <summary>扫描 `design/assets/specs/*.md`,跑全部门判据。返回错误列表(空 = 通过)。</summary>
        public static List<string> RunAll(string repoRoot)
        {
            var report = RunAllDetailed(repoRoot);
            return report.Errors;
        }

        /// <summary>同 <see cref="RunAll"/>,但把「声明 / 实测 / 未测」三数带出来。</summary>
        public static Report RunAllDetailed(string repoRoot)
        {
            var errors = new List<string>();
            // ⚠️ Unity 的 CWD 是 `unity/`,不是仓库根 ⇒ "." 不能直接当仓库根。
            //    与 DiseaseConstantRegistryGate / WriterExistenceGate 同法:从 Application.dataPath 上溯。
            string root = string.IsNullOrEmpty(repoRoot) || repoRoot == "." ? ResolveRepoRoot() : repoRoot;
            string dir = Path.Combine(root, SpecRelDir);
            var decls = new List<Declaration>();

            if (!Directory.Exists(dir))
            {
                errors.Add($"[PolyBudget] spec 目录不存在:`{dir}`(输入面丢失)");
                var r0 = new Report();
                r0.Errors.AddRange(errors);
                return r0;
            }

            foreach (string file in Directory.GetFiles(dir, "*.md").OrderBy(x => x, StringComparer.Ordinal))
            {
                string rel = file.Replace('\\', '/');
                decls.AddRange(ParseSpec(rel, File.ReadAllText(file), errors));
            }

            var report = Evaluate(decls, CountTrianglesInUnity);
            report.Errors.InsertRange(0, errors);
            return report;
        }

        /// <summary>仓库根(`…/unity/Assets` 上溯两级)。非 Editor 环境退回当前目录(仅测试用)。</summary>
        private static string ResolveRepoRoot()
        {
#if UNITY_EDITOR
            // …/unity/Assets → …/unity → 仓库根
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
#else
            return Directory.GetCurrentDirectory();
#endif
        }

        /// <summary>Unity 侧三角面计数(FBX / 任何可导入模型)。返回 null ⇒ 不存在或导入失败。</summary>
        private static int? CountTrianglesInUnity(string assetPath)
        {
#if UNITY_EDITOR
            if (!File.Exists(assetPath)) return null;
            var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(assetPath);
            if (go == null) return null;

            int total = 0;
            foreach (var mf in go.GetComponentsInChildren<UnityEngine.MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.subMeshCount; i++) total += (int)(mesh.GetIndexCount(i) / 3);
            }
            return total;
#else
            return null;
#endif
        }
    }
}
