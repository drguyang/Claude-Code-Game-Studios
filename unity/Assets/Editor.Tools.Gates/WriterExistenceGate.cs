// 写者存在性门(b7)—— M2 接线轮阶段 2 · 批次 D 新立。
//
// 权威来源:
//   ADR-024 §①(entities.yaml = Kind 单一登记真源;stream / author / payload_schema 三必填)
//   ADR-005(主机唯一 Append)· ADR-009 §二(进流义务)
//   entities.yaml 每支 `author:` 声明 —— 写者归属的登记面
//
// 门逻辑(核心不变量):
//   对 entities.yaml 中**每一个带 author 声明的 Kind**:
//       该 Kind ∈ 已写者集(扫描面内存在生产 `Encode(EventKind.X` 写入点)
//                 ∪ 具名豁免表(写者系统尚未实现,理由 + 归属轮逐条登记)
//   否则红。
//
// 三条附带判据(任一 = 红):
//   ① **豁免 = 红**:已存在写者的 Kind 出现在豁免表 ⇒ 红(豁免是「尚未实现」的可见债务,
//      不是「永久不管」;写者落地同批必须撤豁免 —— 否则豁免表会把已修面重新遮蔽)。
//   ② **陈旧豁免 = 红**:豁免表条目在 registry 无对应 author 条目(Kind 被删/改名)⇒ 红
//      (防止豁免表腐烂成永久后门)。
//   ③ **空集 = 红**:yaml 解析出 0 支、或扫描面目录全不存在 ⇒ 红(不以空集冒充绿)。
//
// 为什么需要这道门:ADR-024 让 registry 成为 Kind 的单一登记真源,但**登记不等于有写者**。
// 实测(2026-10-09 阶段 0 勘察 + 本批复测):35 支 Kind 的 codec / 载荷 / 路由全齐,
// 却有 26 支零生产 Append 调用点 —— 其中 `CaseOpened` / `ResourceHarvested` 属
// 「系统已有 GDD、写者可即刻落地」却无人写,故本批补两支 + 余 24 支显式豁免。
// 没有这道门,「新 Kind 忘了写写者」「写者被删没人发现」都不可见(与 kindgen 的
// A5 差集断言互补:A5 管 registry ↔ 生成物,本门管 registry ↔ **生产写入点**)。
//
// 落点理由:编辑期断言,住 Editor.Tools 族(不进构建,门 A 不约束 —— ADR-022 §① 同构)。
// 执行体 = ① EditMode 测试(本批新立 writer_existence_gate_test)② 菜单 / 构建前门
//   经 `AssemblyGates.RunAll()` 接入(2026-10-10)—— 承 review-workflow「门须有强制点,
//   零调用方的门形同虚设」。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>写者存在性门(b7)—— registry 声明的每个 Kind 必须有生产写者或具名豁免。</summary>
    public static class WriterExistenceGate
    {
        // ══════════════════════════════════════════════════════════════════
        // 扫描面 = 生产写者可能落脚的装配目录(.cs 源文本级)
        // ══════════════════════════════════════════════════════════════════
        // 判据形态:`Encode(EventKind.X` —— 即 IPayloadEncoder 的生产调用点
        //   (ADR-029 §③:唯一合法编码路径 ⇒ 有写者必经此形;手搓 PayloadRef 被 b6 门另禁)。
        // **不含 Assets/Sim.Codec**:那里的 `case EventKind.X: return PayloadCodec.Encode(...)`
        //   是编码器分派体,不是写者(模式本身也不匹配,列入是为防日后误扩)。
        // **不含 Tests / Editor.Tools**:测试桩与编辑期工具不构成生产写者。
        // BCD-码-5(2026-10-10):补 `Assets/Sim.Contracts` —— ADR-025 §① 装配清单内
        //   的**契约面**(SkillGrownEmitter 等已住此);写者若落此面,原五面扫不到 =
        //   零门。实测该面当前零 `Encode(EventKind.` 命中 ⇒ 补面不改当前绿态,只扩可见性。
        // 测试断言本数组恰含**六面**:Sim / Sim.Contracts / Gameplay.Presentation /
        //   Gameplay.Boot / Gameplay.Input / Gameplay.UI
        //   (b6 同款:从数组删任一面 = 该面写者退回零门 = 结构测试红)。
        internal static readonly string[] WriterScanDirs =
        {
            "Assets/Sim",
            "Assets/Sim.Contracts",
            "Assets/Gameplay.Presentation",
            "Assets/Gameplay.Boot",
            "Assets/Gameplay.Input",
            "Assets/Gameplay.UI",
        };

        // ══════════════════════════════════════════════════════════════════
        // 具名豁免表 —— 「写者系统尚未实现」的显式债务清单
        // ══════════════════════════════════════════════════════════════════
        // 每条 = (Kind, 理由 + 归属轮)。纪律(门逻辑强制):
        //   · 已存在写者的 Kind **绝不**在此表(出现 = 红,判据 ①);
        //   · 表内条目须在 registry 有 author 声明(无 = 陈旧,红,判据 ②);
        //   · 写者落地的那一批**同批撤条目**(撤表 = 该 Kind 转为被门正面盯住)。
        // 2026-10-10 勘察基线:35 支 author 声明 = 11 支有生产写者(9 支既有 + 本批
        //   CaseOpened / ResourceHarvested 两支)+ 24 支豁免。
        private static readonly (string Kind, string Reason)[] ExemptKinds =
        {
            // ── 37 病例系统(本批只落 CaseOpened;其余四支归 37 实现轮)──
            ("CaseClosed", "37 结案写者未落 —— 处置证据窗口 / 幂等拒收的写入面归 37 实现轮;CaseCloseDecider 现为纯函数(2026-10-10)"),
            ("PatternRecognized", "37 同源检测写者未落 —— PatternDetector 现为纯函数,盐派生 + 冻结三元组写入归 37 实现轮(2026-10-10)"),
            ("JudgmentRecorded", "37 判断记录写者未落,且 IPayloadEncoder 明确不承载 freehand_text(ADR-029 §① 修正 ④)—— 具名编码通道待 37/编码轮裁定(2026-10-10)"),
            ("JudgmentRevised", "同 JudgmentRecorded:写者未落 + freehand_text 编码通道未裁(ADR-029 §① 修正 ④),归 37 实现轮(2026-10-10)"),

            // ── 9 疾病与伤情模拟 ──
            ("PlayerDied", "9 致死判据写者未落 —— registry author 自述「Append 调用点 = 0」(entities.yaml PlayerDied 条);归 9 实现轮(2026-10-10)"),
            ("CompoundTriggered", "9 并发症触发写者未落 —— 归 9 实现轮(disease-simulation 并发症节)(2026-10-10)"),
            ("CompoundExpired", "9 并发症过期写者未落 —— 归 9 实现轮(同上)(2026-10-10)"),
            ("InjuryStateChanged", "9 伤情状态真值写者未落 —— 归 9 实现轮;25 只出 onset(entities.yaml author 注)(2026-10-10)"),

            // ── 25 格斗与武器线 ──
            ("InjuryOnset", "25 格斗伤害结算写者未落 —— 归 25 实现轮(伤害施加方 = 25,entities.yaml author 注)(2026-10-10)"),
            ("EnemyInjuryOnset", "25 敌人伤情 onset 写者未落 —— 归 25 实现轮(落世界流,不污染病史流)(2026-10-10)"),

            // ── 52 随机事件导演 / 27 敌人 AI ──
            ("EventRolled", "52 掷骰写者未落 —— 归 52 实现轮(ADR-007 §一 掷骰权落地)(2026-10-10)"),
            ("EventArrived", "52 事件降临写者未落 —— 归 52 实现轮(2026-10-10)"),
            ("ThreatDeferred", "52 威胁推迟写者未落 —— 归 52 实现轮(2026-10-10)"),
            ("ThreatDeferralCleared", "52 威胁解除写者未落 —— 归 52 实现轮(2026-10-10)"),
            ("HistoryFlagChanged", "52 病史旗标写者未落 —— 归 52 实现轮(2026-10-10)"),
            ("EncounterStarted", "52 遭遇开启写者未落 —— 归 52 实现轮(ADR-016 §八:52 决定来不来)(2026-10-10)"),
            ("EncounterEnded", "27 敌人 AI 遭遇结束写者未落 —— 归 27 实现轮(entities.yaml author 注:非 52)(2026-10-10)"),

            // ── 17 / 18 / 20 世界流(本批只落 17 的 ResourceHarvested)──
            ("Craft", "18 炮制加工写者未落 —— 归 18 实现轮(P0 唯一发出方,entities.yaml author 注)(2026-10-10)"),
            ("DropSpawned", "20 落地写者未落 —— 且它是 instance_id 的**铸造点**(foraging 规则三);归 20 实现轮(2026-10-10)"),
            ("DropClaimed", "20 拾取归属写者未落 —— 拾取三段式(ADR-009 §七)判距与写入归 20/4 装配轮(2026-10-10)"),
            ("DropDespawned", "20 掉落消亡写者未落 —— 归 20 实现轮(2026-10-10)"),

            // ── 24 / 30 / 53 ──
            ("CareApplied", "24 医馆与案头写者未落(以入向事件写入)—— 归 24 轮(entities.yaml author 注)(2026-10-10)"),
            ("SkillGrown", "30 只出载荷(SkillGrownEmitter 明写「不调 Append」)—— Append 由调用方 {8/17/11} 完成,调用面未接;归各调用轮 + 30 装配(2026-10-10)"),
            ("ConsequenceResolved", "53 医疗后果与责任未实现 —— 归 53 轮(P0 未排;entities.yaml author 注)(2026-10-10)"),
        };

        // ══════════════════════════════════════════════════════════════════
        // 执行体
        // ══════════════════════════════════════════════════════════════════

        /// <summary>对真工程跑门(yaml 真源 + 真扫描面)。</summary>
        public static List<string> RunAll()
            => Check(EntitiesYamlPath(), WriterScanDirs);

        /// <summary>
        /// 门核心(yaml 路径 + 扫描面皆形参 —— 测试可注入**工程外探针**做行为级负向验证)。
        /// </summary>
        /// <param name="yamlPath">entities.yaml 绝对路径。</param>
        /// <param name="scanDirs">扫描面目录(相对工程根或绝对路径)。</param>
        /// <returns>错误清单(空 = 绿)。</returns>
        public static List<string> Check(string yamlPath, string[] scanDirs)
        {
            var errs = new List<string>();

            // ── 前置:真源可读(缺文件 = 红,不以空集冒充绿)──
            if (string.IsNullOrEmpty(yamlPath) || !File.Exists(yamlPath))
            {
                errs.Add($"[b7] entities.yaml 不可读(尝试:{yamlPath})—— 写者门拒绝在无真源状态下判绿。");
                return errs;
            }

            var authored = ParseAuthoredKinds(yamlPath, errs);
            if (authored.Count == 0)
            {
                errs.Add($"[b7] {yamlPath} 解析出 0 支带 author 的 Kind —— 解析面丢失(假绿面)。");
                return errs;
            }

            var missingDirs = scanDirs.Where(d => !Directory.Exists(d)).ToArray();
            if (missingDirs.Length == scanDirs.Length)
            {
                errs.Add("[b7] 扫描面目录全部不存在 —— 扫描面丢失(假绿面): "
                         + string.Join(", ", scanDirs));
                return errs;
            }

            var written = ScanWrittenKinds(scanDirs, errs);
            if (written.Count == 0)
            {
                errs.Add("[b7] 扫描面内零 `Encode(EventKind.` 写入点 —— 假绿面(扫描体或面坏了)。");
                return errs;
            }

            var exempt = new HashSet<string>(ExemptKinds.Select(e => e.Kind));

            // ── 判据:每个 author 声明的 Kind ∈ 已写者 ∪ 豁免表 ──
            foreach (var (kind, author) in authored)
            {
                if (written.Contains(kind) || exempt.Contains(kind)) continue;
                errs.Add($"[b7] Kind「{kind}」在 entities.yaml 声明 author=「{author}」," +
                         "但扫描面内无生产 `Encode(EventKind." + kind + "` 写入点,且不在豁免表 —— " +
                         "写者缺失(补写者,或按纪律登记具名豁免:理由 + 归属轮)。");
            }

            // ── 判据 ①:已写者不得豁免(豁免 = 红)──
            foreach (var (kind, reason) in ExemptKinds)
            {
                if (written.Contains(kind))
                    errs.Add($"[b7] Kind「{kind}」已在扫描面内有生产写者,却仍挂在豁免表 —— " +
                             $"豁免 = 红(写者已落地须同批撤表)。豁免理由:{reason}");
            }

            // ── 判据 ②:陈旧豁免 = 红(registry 已无该 author 条目)──
            var authoredSet = new HashSet<string>(authored.Select(a => a.Kind));
            foreach (var (kind, reason) in ExemptKinds)
            {
                if (!authoredSet.Contains(kind))
                    errs.Add($"[b7] 豁免表条目「{kind}」在 entities.yaml 无对应 author 声明 —— " +
                             $"陈旧豁免(Kind 被删 / 改名后未同步)。原理由:{reason}");
            }

            return errs;
        }

        /// <summary>解析 entities.yaml:取每支 Kind 的 (Kind, author);无 author 的条目跳过。</summary>
        public static List<(string Kind, string Author)> ParseAuthoredKinds(string yamlPath, List<string> errs)
        {
            var result = new List<(string Kind, string Author)>();
            string current = null;
            try
            {
                foreach (var raw in File.ReadAllLines(yamlPath))
                {
                    var t = raw.Trim();
                    if (t.StartsWith("- name: SimEvent.Kind.", StringComparison.Ordinal))
                    {
                        current = t.Substring("- name: SimEvent.Kind.".Length).Trim();
                    }
                    else if (current != null && t.StartsWith("author:", StringComparison.Ordinal))
                    {
                        var author = t.Substring("author:".Length).Trim().Trim('"');
                        if (author.Length > 0) result.Add((current, author));
                        current = null;
                    }
                    else if (current != null && t.StartsWith("- name:", StringComparison.Ordinal))
                    {
                        current = null; // 下一条目起点(author 之前)—— 防跨条目串位
                    }
                }
            }
            catch (Exception ex)
            {
                errs.Add($"[b7] 解析 {yamlPath} 失败:{ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// 扫描面内的**生产写者**集合:源文本(已剥注释)出现 `Encode(EventKind.X` 的 Kind 名。
        /// </summary>
        public static HashSet<string> ScanWrittenKinds(string[] dirs, List<string> errs)
        {
            var written = new HashSet<string>();
            var pattern = new Regex(@"Encode\(\s*EventKind\.(\w+)", RegexOptions.Compiled);
            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string code;
                    try { code = StripCommentsForScan(File.ReadAllText(f)); }
                    catch (Exception ex)
                    {
                        errs.Add($"[b7] 读取 {f} 失败:{ex.Message}");
                        continue;
                    }
                    foreach (Match m in pattern.Matches(code))
                        written.Add(m.Groups[1].Value);
                }
            }
            return written;
        }

        /// <summary>剥行注释与块注释 —— 判据扫代码,不扫文档里对规则本身的引用(与 b6 同口径)。</summary>
        private static string StripCommentsForScan(string src)
        {
            var noBlock = Regex.Replace(src, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//.*?$", "", RegexOptions.Multiline);
        }

        /// <summary>entities.yaml 绝对路径(经 Application.dataPath 上溯仓库根,与测试面同法)。</summary>
        public static string EntitiesYamlPath()
        {
            var assetsDir = Application.dataPath;                 // …/unity/Assets
            var unityDir = Path.GetDirectoryName(assetsDir);      // …/unity
            var repoRoot = Path.GetDirectoryName(unityDir);       // 仓库根
            return Path.Combine(repoRoot, "design", "registry", "entities.yaml");
        }
    }
}
