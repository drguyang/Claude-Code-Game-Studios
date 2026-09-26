// 权威来源:production/epics/audio-system/story-003-mixer-topology-snapshots.md(本 story 全部 AC 的执行体)
//   · AC-44-C1(BLOCKING)—— TransitionTo / TransitionToSnapshots 全部调用点(Cecil IL 扫描)∈
//     快照切换调用点白名单(玩家行为 / 世界可感知声两类);负向夹具 = 体征 handler 调
//     TransitionTo(DialogueFocus) ⇒ 红;DialogueFocus 字段归属 = 玩家对话入口
//   · AC-44-E3 —— .mixer(Force Text YAML)① 七总线组齐备 ② Aux/Reverb send 拓扑清单单一出处
//     (新增未登记 send = 构建失败)③ reverb preset 切换所有者 = 44
//   · 注册表 AC② —— 快照参数集 ∩ 玩家 exposed 集 = ∅(两级组结构)
//   · 注册表 AC③ —— bus_volume_* 常量集计数 = 7(含 Master)+ GDD 表对账(语义源 ↔ 常量)
// GDD:design/gdd/audio-system.md §States(两级组纪律 + 快照五员)· §UI Requirements 注册表
//   (:896-909,语义源)· AC-44-C1(:977)/ AC-44-E3(:1113)
// ADR-018 §三(七总线 · 快照禁播报 · Aux/Reverb send 单一定义)
// TR-audio-003 / TR-audio-004
//
// ⚠️ 形态 = **错误列表(空 = 通过)**;本文件**零 throw** —— 聚合非空列表 ⇒ 构建失败的 throw
//    归调用方(承 AudioEventTableGates 同一形态)。
// ⚠️ **注册表单一出处**:总线名 / bus_volume_* / 快照名住**运行期** `MixerRegistry`
//    (Gameplay.Presentation —— SetFloat 初始化面需要它);本门引用之(Editor.Tools.Gates →
//    Gameplay.Presentation,2026-09-26 增引用)。**禁在门内另抄一份**(两处执行 = 两处分叉)。
//    send 清单调用点白名单是构建期判断 ⇒ 住本文件(数据/GDD 不承载)。
// ⚠️ **IL 扫描 = Mono.Cecil + ReadingMode.Deferred**(承 AssemblyGates b5 实测:SRM /
//    System.Reflection.Metadata 在本工程编译不过 —— 故事原文「System.Reflection.Metadata」
//    为过时口径,2026-09-26 unity-specialist 复核改走 Cecil,勿回改)。
// ⚠️ **YAML 扫描 = 手写逐行状态机**(引号/fileID 配对;零正则首配、零第三方解析器,承
//    Story 002 纪律)。多文档 `--- !u!<classId> &<fileID>`;层级断言**沿 m_MasterGroup →
//    m_Children 的 fileID 树**,不用 m_Name 计数冒充层级(unity-specialist 裁定)。
// ⚠️ **2026-09-26 黄金样例校准完成**(unity/Assets/Audio/DaYiJingCheng.mixer 原文):
//    根类型 = `AudioMixerController`(!u!241)· 组 = `AudioMixerGroupController`(!u!243)·
//    效果 = `AudioMixerEffectController`(!u!244,send 的真表达 = `m_SendTarget`)·
//    快照 = `AudioMixerSnapshotController`(!u!245,捕获真字段 = `m_FloatValues` 流式 map)。
//    旧猜测口径(`AudioMixer` / `m_ValueMap` / `m_Sends` 条目)**全部作废**。
//    残余对齐点:非数字 capture 键形态(样例捕获为空,未实证)—— 只改提取函数,
//    夹具与判据语义不变。
// ⚠️ 范围锁(Out of Scope):reverb 素材存在性 = Story 010;贴耳 bus 路由 = Story 009;
//    注册表壳侧消费 = Story 011。

using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil; // Instruction / Code 在 Cil 子命名空间(AssemblyGates 只查 MemberRef operand,故无此 using)
using UnityEditor;
// 不加 using UnityEngine —— 避免 Object / Debug 与 Cecil、UnityEditor 同名冲突;用全限定名。

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>混音拓扑与快照纪律的构建期校验纯函数(Story 003 · 4 条 AC 的执行体)。
    /// <para>全部方法:纯函数 · 无静态可变态 · **零 throw** · 返回错误列表(空 = 通过)。</para>
    /// <example>
    /// string yaml = File.ReadAllText("Assets/Audio/DaYiJingCheng.mixer");
    /// var errors = MixerTopologyGates.ValidateMixerTopology(yaml);
    /// if (errors.Count &gt; 0) throw new Exception(string.Join("\n", errors));
    /// </example></summary>
    public static class MixerTopologyGates
    {
        // ══════════════ 闭集常量(枚举定义在代码)══════════════

        /// <summary>调用点白名单类别一:玩家行为(进入听诊 / 发起对话 / 暂停 —— 拟物让位)。</summary>
        public const string PlayerActionCategory = "PlayerAction";

        /// <summary>调用点白名单类别二:世界可感知声(reverb preset 随房间格切换 —— 世界语境,
        /// 非病人状态播报)。白名单**只有这两类**(AC-44-C1)。</summary>
        public const string WorldPerceptibleCategory = "WorldPerceptible";

        // ── 2026-09-26 黄金样例实测的文档类型名(unity/Assets/Audio/DaYiJingCheng.mixer 原文;
        //    此前猜测的「AudioMixer / AudioMixerGroup / AudioMixerSnapshot」全部作废)──
        /// <summary>根文档类型名(样例 <c>!u!241 AudioMixerController</c>)。</summary>
        public const string MixerClassName = "AudioMixerController";
        /// <summary>组文档类型名(样例 <c>!u!243 AudioMixerGroupController</c>)。</summary>
        public const string GroupClassName = "AudioMixerGroupController";
        /// <summary>快照文档类型名(样例 <c>!u!245 AudioMixerSnapshotController</c>)。</summary>
        public const string SnapshotClassName = "AudioMixerSnapshotController";
        /// <summary>效果文档类型名(样例 <c>!u!244</c> —— send 的真表达 = effect 的 <c>m_SendTarget</c>)。</summary>
        public const string EffectClassName = "AudioMixerEffectController";

        /// <summary>DialogueFocus 的快照字段(归属断言键:该字段只在玩家对话入口被读)。</summary>
        public const string DialogueSnapshotField = "_dialogueFocus";

        private const string SnapshotDirectorKey =
            "DaYiJingCheng.Gameplay.Presentation.Audio.SnapshotDirector";

        private const string ReverbSwitcherKey =
            "DaYiJingCheng.Gameplay.Presentation.Audio.ReverbPresetSwitcher";

        /// <summary>快照切换调用点白名单的一条登记项(调用点 = <c>Type::Method</c>;
        /// 类别 ∈ {玩家行为, 世界可感知声};<see cref="SnapshotCallsiteRule.AllowedSnapshotFields"/>
        /// = 该调用点允许读取的 <c>AudioMixerSnapshot</c> 字段,null = 经局部变量取快照、不校验字段)。</summary>
        public sealed class SnapshotCallsiteRule
        {
            /// <summary>调用点键(<c>全限定类型名::方法名</c>)。</summary>
            public string Callsite { get; }

            /// <summary>类别(<see cref="PlayerActionCategory"/> / <see cref="WorldPerceptibleCategory"/>)。</summary>
            public string Category { get; }

            /// <summary>允许读取的快照字段名;null = 不校验。</summary>
            public IReadOnlyList<string> AllowedSnapshotFields { get; }

            /// <summary>登记一条白名单项。</summary>
            public SnapshotCallsiteRule(string callsite, string category,
                                        IReadOnlyList<string> allowedSnapshotFields)
            {
                Callsite = callsite;
                Category = category;
                AllowedSnapshotFields = allowedSnapshotFields;
            }
        }

        /// <summary>快照切换调用点登记集(**单一出处**,AC-44-C1)。
        /// <para>玩家行为 5 点(听诊进出 / 对话起止 / 暂停)+ 世界可感知声 1 点(reverb 随房间格)。
        /// 新增 <c>TransitionTo</c> 调用点先登记于此,否则 IL 扫描必红。</para>
        /// <para><c>DialogueFocus</c> 仅限玩家主动发起对话 —— 规则三 2026-09-18 修订
        /// (病人自发呻吟 / 咳嗽不触发)。</para></summary>
        public static readonly IReadOnlyList<SnapshotCallsiteRule> TransitionCallsiteRules = new[]
        {
            new SnapshotCallsiteRule(SnapshotDirectorKey + "::EnterStethoscopeFocus",
                PlayerActionCategory, new[] { "_stethoscopeFocus" }),
            new SnapshotCallsiteRule(SnapshotDirectorKey + "::ExitStethoscopeFocus",
                PlayerActionCategory, new[] { "_defaultSnapshot" }),
            new SnapshotCallsiteRule(SnapshotDirectorKey + "::BeginDialogueFocus",
                PlayerActionCategory, new[] { DialogueSnapshotField }),
            new SnapshotCallsiteRule(SnapshotDirectorKey + "::EndDialogueFocus",
                PlayerActionCategory, new[] { "_defaultSnapshot" }),
            new SnapshotCallsiteRule(SnapshotDirectorKey + "::SetPaused",
                PlayerActionCategory, new[] { "_pausedSnapshot", "_defaultSnapshot" }),
            new SnapshotCallsiteRule(ReverbSwitcherKey + "::Apply",
                WorldPerceptibleCategory, null),
        };

        /// <summary><c>DialogueSnapshotField</c> 只允许出现在玩家对话入口调用点(AC-44-C1
        /// 「DialogueFocus 调用点仅来自玩家对话入口方法」的可执行形态)。</summary>
        private static readonly HashSet<string> DialogueOwnerCallsites =
            new HashSet<string>(StringComparer.Ordinal)
            {
                SnapshotDirectorKey + "::BeginDialogueFocus",
                SnapshotDirectorKey + "::EndDialogueFocus",
            };

        /// <summary>send 拓扑登记项(名称 + 源组 + 目标组)。</summary>
        public sealed class MixerSendRegistration
        {
            /// <summary>send 名(m_YAML 条目名)。</summary>
            public string Name { get; }

            /// <summary>源总线组名。</summary>
            public string SourceGroup { get; }

            /// <summary>目标组名(混响返回总线)。</summary>
            public string TargetGroup { get; }

            /// <summary>登记一条 send。</summary>
            public MixerSendRegistration(string name, string sourceGroup, string targetGroup)
            {
                Name = name;
                SourceGroup = sourceGroup;
                TargetGroup = targetGroup;
            }
        }

        /// <summary>Aux / Reverb send 拓扑**清单单一出处**(AC-44-E3 ②)。
        /// <para>⚠️ **缺口登记(2026-09-26 交付报告)**:story / GDD / ADR 均未列全「哪些组发往
        /// reverb bus」—— 此处按**最小可用**登记 1 条(Ambience → Reverb,世界语境呼吸的房间
        /// 感知主通道);后续扩条目 = 改本常量,**新增未登记 send = 构建失败**。</para></summary>
        public static readonly IReadOnlyList<MixerSendRegistration> RegisteredAuxSends = new[]
        {
            new MixerSendRegistration("reverb_send_ambience", "Ambience", "Reverb"),
        };

        /// <summary>扫描面:可引用引擎的运行期 gameplay 装配(AC-44-C1「全部调用点」)。</summary>
        public static readonly IReadOnlyList<string> TransitionScanAssemblies = new[]
        {
            "Gameplay.Presentation", "Gameplay.UI", "Gameplay.Input",
        };

        /// <summary>IL 扫描到的一个快照切换调用点。</summary>
        public sealed class TransitionCallsite
        {
            /// <summary>调用方全限定类型名。</summary>
            public string DeclaringType;

            /// <summary>调用方方法名(<c>.ctor</c> 等按 IL 原样)。</summary>
            public string Method;

            /// <summary>调用前最近一次读取的 <c>AudioMixerSnapshot</c> 类型字段名;
            /// 经局部变量 / 参数取快照时为 null。</summary>
            public string SnapshotField;

            /// <summary>调用点键(<c>Type::Method</c>,白名单比对键)。</summary>
            public string Key => DeclaringType + "::" + Method;
        }

        // ══════════════ 注册表 AC③ + 对账 ══════════════

        /// <summary>注册表 AC③:<c>bus_volume_*</c> 常量集与七总线名 —— 计数各 = 7(含 Master)、
        /// 1:1 对应(集基,与顺序无关)。</summary>
        /// <param name="busVolumeParams">玩家 exposed 总线参数常量集(断言对象)。</param>
        /// <param name="busNames">七总线组名常量集。</param>
        public static IReadOnlyList<string> ValidateBusRegistryCounts(
            IReadOnlyList<string> busVolumeParams, IReadOnlyList<string> busNames)
        {
            var errors = new List<string>();
            if (busVolumeParams == null || busNames == null)
            {
                errors.Add("[NOT-RUN 守卫] 注册表输入缺失(null)—— 注册表 AC③ 不可执行,不得静默通过");
                return errors;
            }

            if (busVolumeParams.Count != 7)
                errors.Add($"[注册表 AC③] bus_volume_* 常量集计数 = {busVolumeParams.Count},须 = 7" +
                           "(含 Master —— 原 Tuning 行漏 Master 已补)");
            if (busNames.Count != 7)
                errors.Add($"[注册表 AC③] 总线名常量集计数 = {busNames.Count},须 = 7(七总线,ADR-018 §三)");

            foreach (string bus in busNames)
            {
                string expected = "bus_volume_" + bus.ToLowerInvariant();
                if (!Contains(busVolumeParams, expected))
                    errors.Add($"[注册表 AC③] 总线「{bus}」缺对应参数「{expected}」(1:1 失配)");
            }

            foreach (string param in busVolumeParams)
            {
                bool mapped = false;
                foreach (string bus in busNames)
                    if (string.Equals(param, "bus_volume_" + bus.ToLowerInvariant(), StringComparison.Ordinal))
                        mapped = true;
                if (!mapped)
                    errors.Add($"[注册表 AC③] 参数「{param}」无对应总线名(1:1 失配)");
            }

            return errors;
        }

        /// <summary>注册表 AC③ 对账:GDD §UI Requirements 注册表(语义源,~:896)中出现的
        /// <c>bus_volume_*</c> 行集 == C# 常量集(7 行 ↔ 7 员)。
        /// <para>⚠️ story 裁定:AC③ 的**断言对象 = C# 常量集**(不让测试解析 GDD markdown 当
        /// 判据);本方法是**对账断言**(常量 ↔ 语义源一致性),两者分工不同。</para></summary>
        /// <param name="gddLines">GDD 全文按行(缺失解析面 ⇒ 错误,不静默)。</param>
        /// <param name="busVolumeParams">玩家 exposed 总线参数常量集。</param>
        public static IReadOnlyList<string> ValidateGddBusRegistryReconciliation(
            IEnumerable<string> gddLines, IReadOnlyList<string> busVolumeParams)
        {
            var errors = new List<string>();
            if (gddLines == null || busVolumeParams == null)
            {
                errors.Add("[NOT-RUN 守卫] 对账输入缺失(null)—— GDD ↔ 常量对账不可执行");
                return errors;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in gddLines)
            {
                if (line == null) continue;
                int from = 0;
                while (true)
                {
                    int idx = line.IndexOf("bus_volume_", from, StringComparison.Ordinal);
                    if (idx < 0) break;
                    int head = idx + "bus_volume_".Length;

                    // GDD 注册表把 7 行收成一行的花括号展开写法
                    // `bus_volume_{master,music,ambience,voice,sfx,stethoscope,uicue}` ——
                    // `{` 非标识符字符,原扫描会整条跳过 ⇒ 见到 0 条(2026-09-26 实测假红)。
                    if (head < line.Length && line[head] == '{')
                    {
                        int close = line.IndexOf('}', head);
                        if (close > head)
                        {
                            foreach (string part in line.Substring(head + 1, close - head - 1).Split(','))
                            {
                                string suffix = part.Trim();
                                if (suffix.Length > 0)
                                    seen.Add("bus_volume_" + suffix);
                            }
                            from = close + 1;
                            continue;
                        }
                    }

                    int end = idx;
                    while (end < line.Length && IsIdentifierChar(line[end])) end++;
                    // 裸前缀(如通配写法 `bus_volume_*`)不算条目 —— 只收带后缀的完整参数名
                    if (end > head)
                        seen.Add(line.Substring(idx, end - idx));
                    from = end;
                }
            }

            if (seen.Count == 0)
            {
                errors.Add("[对账 · AC③] GDD 全文未解析出任何 bus_volume_* 条目 —— " +
                           "表格移动 / 改名?拒以空集冒充绿(「没检查」与「检查通过」必须可区分)");
                return errors;
            }

            foreach (string param in busVolumeParams)
                if (!seen.Contains(param))
                    errors.Add($"[对账 · AC③] GDD 注册表缺「{param}」行 —— GDD 7 行 ↔ 常量 7 员对账失败");

            foreach (string token in seen)
                if (!Contains(busVolumeParams, token))
                    errors.Add($"[对账 · AC③] GDD 出现注册表外条目「{token}」—— 常量集为单一出处," +
                               "扩条目先改 MixerRegistry.BusVolumeParameters");

            return errors;
        }

        /// <summary>两级组纪律(初始化面):玩家音量默认集必须覆盖全部 7 个注册参数,
        /// 且无未知键(拼写错静默漏 SetFloat = 首帧前仍受快照控制)。
        /// <para>**委托**运行期 <c>PlayerBusVolumeInitializer.ValidateDefaults</c>(判据单一出处,
        /// 门侧不复写一份 —— 两处执行 = 两处分叉)。</para></summary>
        /// <param name="providedKeys">调用方提供的默认集键。</param>
        public static IReadOnlyList<string> ValidatePlayerVolumeDefaults(IEnumerable<string> providedKeys)
            => Gameplay.Presentation.Audio.PlayerBusVolumeInitializer.ValidateDefaults(providedKeys);

        // ══════════════ AC-44-E3 + 注册表 AC②:.mixer YAML 扫描 ══════════════

        /// <summary>总门:七总线组树 + 两级组 + exposed 注册表 + 快照五员 + 交集 ∅ + send 清单。</summary>
        /// <param name="yamlText">.mixer 原文(Force Text YAML);null/空 ⇒ NOT-RUN 守卫。</param>
        public static IReadOnlyList<string> ValidateMixerTopology(string yamlText)
        {
            if (string.IsNullOrEmpty(yamlText))
                return new[] { "[NOT-RUN 守卫] .mixer 原文缺失(null/空)—— 拓扑校验不可执行,不得静默通过" };

            var errors = new List<string>();
            errors.AddRange(ValidateGroupTree(yamlText));
            errors.AddRange(ValidateSnapshotRoster(yamlText));
            // 2026-09-26 修:exposed 参数断言本体在 ValidateGroupTree 内(m_ExposedParameters
            // == 注册表 7 员 + 同名组承载)—— 原 ValidateExposedParameters 调用是未落名的悬空引用
            errors.AddRange(ValidateSnapshotExposedDisjoint(yamlText));
            errors.AddRange(ValidateSends(yamlText));
            return errors;
        }

        /// <summary>AC-44-E3 ① + 两级组结构:沿 fileID 树(不以 m_Name 计数冒充层级)断言
        /// ① 七总线齐备且从 Master 可达 ② 每总线拆「玩家音量组 + 快照 duck 组」两级
        /// ③ exposed 参数集 == 注册表且全部对应存在的组。</summary>
        public static IReadOnlyList<string> ValidateGroupTree(string yamlText)
        {
            var errors = new List<string>();
            List<MixerDoc> docs = ParseMixerYaml(yamlText, errors);
            if (errors.Count > 0 && docs.Count == 0)
                return errors;

            var groups = new Dictionary<long, MixerDoc>();
            foreach (MixerDoc doc in docs)
            {
                if (doc.ClassName != GroupClassName) continue;
                if (groups.ContainsKey(doc.FileId))
                    errors.Add($"[层级] fileID {doc.FileId} 重复({GroupClassName})—— 解析面歧义");
                else groups[doc.FileId] = doc;
            }

            MixerDoc mixer = null;
            foreach (MixerDoc doc in docs)
                if (doc.ClassName == MixerClassName) { mixer = doc; break; }
            if (mixer == null || mixer.MasterGroupId == 0)
            {
                errors.Add($"[NOT-RUN 守卫] 未解析到 {MixerClassName} / m_MasterGroup —— 层级断言不可执行");
                return errors;
            }
            if (!groups.TryGetValue(mixer.MasterGroupId, out MixerDoc root))
            {
                errors.Add($"[层级] m_MasterGroup fileID {mixer.MasterGroupId} 不存在 —— 悬空根");
                return errors;
            }

            // 可达集(沿 m_Children 的 fileID 树);悬空引用 = 错误
            var reachable = new HashSet<long>();
            var stack = new Stack<long>();
            stack.Push(root.FileId);
            while (stack.Count > 0)
            {
                long id = stack.Pop();
                if (!reachable.Add(id)) continue;
                MixerDoc node = groups[id];
                foreach (long child in node.Children)
                {
                    if (!groups.ContainsKey(child))
                        errors.Add($"[层级] 组「{node.Name}」的子引用 fileID {child} 不存在 —— 悬空");
                    else stack.Push(child);
                }
            }

            // ① 七总线 + 可达 + ② 两级组
            IReadOnlyList<string> buses = Gameplay.Presentation.Audio.MixerRegistry.BusNames;
            foreach (string bus in buses)
            {
                MixerDoc busNode = null;
                foreach (MixerDoc g in groups.Values)
                    if (string.Equals(g.Name, bus, StringComparison.Ordinal)) { busNode = g; break; }

                if (busNode == null)
                {
                    errors.Add($"[AC-44-E3 ①] 七总线缺「{bus}」组 —— 七总线组齐备(ADR-018 §三)");
                    continue;
                }
                if (!reachable.Contains(busNode.FileId))
                {
                    errors.Add($"[AC-44-E3 ①] 总线「{bus}」未从 Master 可达 —— 悬挂在树外");
                    continue;
                }

                var childNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (long child in busNode.Children)
                    if (groups.TryGetValue(child, out MixerDoc c) && c.Name != null)
                        childNames.Add(c.Name);

                string volumeGroup = Gameplay.Presentation.Audio.MixerRegistry.VolumeGroupForBus(bus);
                string duckGroup = Gameplay.Presentation.Audio.MixerRegistry.DuckGroupForBus(bus);
                if (!childNames.Contains(volumeGroup))
                    errors.Add($"[两级] 总线「{bus}」缺玩家音量组「{volumeGroup}」—— 每总线拆两级" +
                               "(滑块写玩家组,快照过渡不触碰)");
                if (!childNames.Contains(duckGroup))
                    errors.Add($"[两级] 总线「{bus}」缺快照 duck 组「{duckGroup}」—— 两级组结构:" +
                               "快照只捕获 duck 组,交集必须 = ∅");
            }

            // ③ exposed == 注册表 且每项对应存在的组
            if (mixer.Exposed.Count == 0)
            {
                errors.Add("[NOT-RUN 守卫] m_ExposedParameters 为空/缺失 —— 注册表断言不可执行");
            }
            else
            {
                IReadOnlyList<string> registry = Gameplay.Presentation.Audio.MixerRegistry.BusVolumeParameters;
                foreach (string param in registry)
                    if (!Contains(mixer.Exposed, param))
                        errors.Add($"[AC③] .mixer exposed 缺「{param}」—— 注册表 7 员须全暴露");
                foreach (string param in mixer.Exposed)
                    if (!Contains(registry, param))
                        errors.Add($"[AC③] .mixer exposed 含注册表外条目「{param}」—— 单一出处在 MixerRegistry");

                var groupNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (MixerDoc g in groups.Values)
                    if (g.Name != null) groupNames.Add(g.Name);
                foreach (string param in mixer.Exposed)
                    if (!groupNames.Contains(param))
                        errors.Add($"[AC③] exposed 参数「{param}」无同名组承载 —— 玩家音量组命名 = 参数名" +
                                   "(两级组结构约定)");
            }

            return errors;
        }

        /// <summary>AC-44-E3 附:快照五员表(GDD §States:Default / StethoscopeFocus /
        /// DialogueFocus / Paused / VRComfort)齐备;额外快照(reverb preset 等)不报错。</summary>
        public static IReadOnlyList<string> ValidateSnapshotRoster(string yamlText)
        {
            var errors = new List<string>();
            List<MixerDoc> docs = ParseMixerYaml(yamlText, errors);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (MixerDoc doc in docs)
                if (doc.ClassName == SnapshotClassName && doc.Name != null)
                    names.Add(doc.Name);

            if (names.Count == 0)
                return new[] { $"[NOT-RUN 守卫] 未解析到任何 {SnapshotClassName} 文档 —— 快照五员断言不可执行" };

            foreach (string expected in Gameplay.Presentation.Audio.MixerRegistry.SnapshotNames)
                if (!names.Contains(expected))
                    errors.Add($"[快照五员] 缺「{expected}」—— GDD §States 五员表(AC-44-E3 扫描面)");

            return errors;
        }

        /// <summary>注册表 AC②(正例半边的判据本体,可单跑):玩家 exposed 集 ∩ 快照捕获集 = ∅。
        /// <para>2026-09-26 黄金样例校准:捕获真字段 = <c>m_FloatValues: {fileId: dB, ...}</c>
        /// (内联流式,键为数字 ⇒ 按组 fileID 解析到组名;非数字键 ⇒ 按字面量与 exposed 求交)。
        /// 不能解析为组的数字键**跳过不报错**(真实键可能是参数哈希 —— 样例捕获为空,该形态
        /// 未实证,登记为对齐点)。</para></summary>
        public static IReadOnlyList<string> ValidateSnapshotExposedDisjoint(string yamlText)
        {
            var errors = new List<string>();
            List<MixerDoc> docs = ParseMixerYaml(yamlText, errors);

            var exposed = new HashSet<string>(StringComparer.Ordinal);
            foreach (MixerDoc doc in docs)
                if (doc.ClassName == MixerClassName)
                    foreach (string p in doc.Exposed) exposed.Add(p);
            if (exposed.Count == 0)
                return new[] { "[NOT-RUN 守卫] 未解析到 exposed 参数 —— 交集断言不可执行(拒空集冒充绿)" };

            var groupNames = new Dictionary<long, string>();
            foreach (MixerDoc doc in docs)
                if (doc.ClassName == GroupClassName && doc.Name != null)
                    groupNames[doc.FileId] = doc.Name;

            // 捕获键形态实测(2026-09-26):m_FloatValues 的键 = **组电平参数哈希**(32 hex),
            // 既不是组 fileID 也不是组名 —— 旧实现把它们当「字面量名」与 exposed 求交,永不可能命中
            // ⇒ AC② 对真资产**恒真空转**(QA 点名的假绿)。此处经各组 m_Volume 哈希反查组名。
            var volumeHashToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (MixerDoc doc in docs)
            {
                if (doc.ClassName != GroupClassName || doc.Name == null ||
                    string.IsNullOrEmpty(doc.VolumeHash))
                    continue;
                volumeHashToGroup[doc.VolumeHash] = doc.Name;
            }

            foreach (MixerDoc snap in docs)
            {
                if (snap.ClassName != SnapshotClassName) continue;
                foreach (long target in snap.CaptureTargets)
                {
                    if (!groupNames.TryGetValue(target, out string groupName))
                        continue;   // 非组 fileID 的键(参数哈希?)—— 对齐点,不误红
                    if (exposed.Contains(groupName))
                        errors.Add($"[AC②] 快照「{snap.Name}」捕获玩家 exposed 参数「{groupName}」—— " +
                                   "快照参数集 ∩ 玩家 exposed 集 ≠ ∅(进出快照会打回玩家滑块值," +
                                   "两级组结构纪律,GDD §States / story AC②)");
                }
                foreach (string literal in snap.CaptureNames)
                {
                    // 32-hex 捕获键 = 组电平参数哈希 ⇒ 经 m_Volume 反查组名后参与交集
                    if (volumeHashToGroup.TryGetValue(literal, out string byHash))
                    {
                        if (exposed.Contains(byHash))
                            errors.Add($"[AC②] 快照「{snap.Name}」捕获玩家 exposed 组「{byHash}」" +
                                       $"(键 {literal} = 该组 m_Volume 哈希)—— 快照参数集 ∩ 玩家 " +
                                       "exposed 集 ≠ ∅(进出快照会打回玩家滑块值,两级组结构纪律," +
                                       "GDD §States / story AC②)");
                        continue;
                    }
                    if (exposed.Contains(literal))
                        errors.Add($"[AC②] 快照「{snap.Name}」捕获玩家 exposed 参数「{literal}」—— " +
                                   "快照参数集 ∩ 玩家 exposed 集 ≠ ∅(AC②)");
                }
            }

            return errors;
        }

        /// <summary>**AC② 非空转守卫**:能解析到组名的捕获键数量(组 fileID 或 <c>m_Volume</c> 哈希)。
        /// <para>为 0 ⇒ <see cref="ValidateSnapshotExposedDisjoint"/> 的交集**恒 ∅(空转)** ——
        /// 解析失败会静默放过,与「检查通过」不可区分(2026-09-26 QA 点名的假绿形态);
        /// 真资产测试须断言本值 &gt; 0,否则 AC② 换一种方式继续空转。</para></summary>
        /// <param name="yamlText">`.mixer` 的 Force Text YAML 全文。</param>
        public static int CountResolvedCaptureKeys(string yamlText)
        {
            var errors = new List<string>();
            List<MixerDoc> docs = ParseMixerYaml(yamlText, errors);

            var byFileId = new Dictionary<long, string>();
            var byHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (MixerDoc doc in docs)
            {
                if (doc.ClassName != GroupClassName || doc.Name == null) continue;
                byFileId[doc.FileId] = doc.Name;
                if (!string.IsNullOrEmpty(doc.VolumeHash)) byHash[doc.VolumeHash] = doc.Name;
            }

            int resolved = 0;
            foreach (MixerDoc snap in docs)
            {
                if (snap.ClassName != SnapshotClassName) continue;
                foreach (long target in snap.CaptureTargets)
                    if (byFileId.ContainsKey(target)) resolved++;
                foreach (string key in snap.CaptureNames)
                    if (byHash.ContainsKey(key)) resolved++;
            }

            return resolved;
        }

        /// <summary>AC-44-E3 ②(2026-09-26 黄金样例校准):**send 的真表达 = effect 的
        /// <c>m_SendTarget</c> 指向目标组的 effect**(样例无 m_Sends 条目 —— 旧口径作废)。
        /// extracted(source → target)集 == 登记集(双向差集 = 错误)—— 新增未登记 send =
        /// 构建失败;登记项缺失同样红(清单 = 单一出处)。匹配键 = (源组, 目标组);
        /// 登记项的 <c>Name</c> 仅作诊断标签(真格式无 send 名字段)。</summary>
        public static IReadOnlyList<string> ValidateSends(string yamlText)
        {
            var errors = new List<string>();
            List<MixerDoc> docs = ParseMixerYaml(yamlText, errors);

            // 效果归属:effect fileID → 所在组名(经组的 m_Effects 反查);效果文档索引
            var effectOwner = new Dictionary<long, string>();
            var effectDocs = new Dictionary<long, MixerDoc>();
            bool anyGroup = false;
            foreach (MixerDoc doc in docs)
            {
                if (doc.ClassName == EffectClassName)
                    effectDocs[doc.FileId] = doc;
                if (doc.ClassName != GroupClassName) continue;
                anyGroup = true;
                foreach (long effectId in doc.Effects)
                    effectOwner[effectId] = doc.Name;
            }
            if (!anyGroup)
                return new[] { $"[NOT-RUN 守卫] 未解析到 {GroupClassName} —— send 清单断言不可执行" };

            // 抽取 send:每个 SendTarget ≠ 0 的 effect ⇒ (所在组, 目标组)
            var extracted = new List<MixerSendRegistration>();
            int sendIndex = 0;
            foreach (KeyValuePair<long, MixerDoc> pair in effectDocs)
            {
                MixerDoc effect = pair.Value;
                if (effect.SendTarget == 0) continue;
                sendIndex++;
                if (!effectOwner.TryGetValue(pair.Key, out string sourceGroup) ||
                    string.IsNullOrEmpty(sourceGroup))
                {
                    errors.Add($"[AC-44-E3 ②] 效果「{effect.EffectName}」(fileID {pair.Key})有 sendTarget " +
                               "但未挂在任何组的 m_Effects 下 —— send 源不可判定,不得静默通过");
                    continue;
                }

                string targetGroup = null;
                if (effectDocs.TryGetValue(effect.SendTarget, out MixerDoc targetEffect))
                    effectOwner.TryGetValue(effect.SendTarget, out targetGroup);
                else if (TryGetGroupName(docs, effect.SendTarget, out string directGroup))
                    targetGroup = directGroup;

                if (string.IsNullOrEmpty(targetGroup))
                {
                    errors.Add($"[AC-44-E3 ②] send(fileID {pair.Key}, 源组「{sourceGroup}」)的 " +
                               $"m_SendTarget {effect.SendTarget} 悬空 / 目标未挂组 —— 拓扑不可枚举");
                    continue;
                }
                extracted.Add(new MixerSendRegistration(
                    $"send#{sendIndex}", sourceGroup, targetGroup));
            }

            foreach (MixerSendRegistration send in extracted)
            {
                bool registered = false;
                foreach (MixerSendRegistration reg in RegisteredAuxSends)
                    if (string.Equals(reg.SourceGroup, send.SourceGroup, StringComparison.Ordinal) &&
                        string.Equals(reg.TargetGroup, send.TargetGroup, StringComparison.Ordinal))
                        registered = true;
                if (!registered)
                    errors.Add($"[AC-44-E3 ②] send({send.SourceGroup} → {send.TargetGroup})" +
                               "未登记 —— Aux/Reverb send 拓扑清单 = 单一出处,新增 send 须先登记 " +
                               "MixerTopologyGates.RegisteredAuxSends ⇒ 构建失败");
            }

            foreach (MixerSendRegistration reg in RegisteredAuxSends)
            {
                bool found = false;
                foreach (MixerSendRegistration send in extracted)
                    if (string.Equals(reg.SourceGroup, send.SourceGroup, StringComparison.Ordinal) &&
                        string.Equals(reg.TargetGroup, send.TargetGroup, StringComparison.Ordinal))
                        found = true;
                if (!found)
                    errors.Add($"[AC-44-E3 ②] 已登记 send「{reg.Name}」({reg.SourceGroup} → " +
                               $"{reg.TargetGroup})在 .mixer 中缺失/失配 —— 清单单一出处");
            }

            return errors;
        }

        /// <summary>fileID → 组名直查(send 目标直接指向组文档的兜底形态)。</summary>
        private static bool TryGetGroupName(List<MixerDoc> docs, long fileID, out string groupName)
        {
            foreach (MixerDoc doc in docs)
            {
                if (doc.ClassName == GroupClassName && doc.FileId == fileID && doc.Name != null)
                {
                    groupName = doc.Name;
                    return true;
                }
            }
            groupName = null;
            return false;
        }

        // ══════════════ AC-44-C1:TransitionTo 调用点白名单 ══════════════

        /// <summary>对编译产物做快照切换调用点扫描(Mono.Cecil,Deferred —— 承 b5 实测)。
        /// <para>谓词 = declaring type <c>UnityEngine.Audio.AudioMixerSnapshot</c> + 方法名
        /// <c>TransitionTo</c>,**并显式**含 <c>UnityEngine.Audio.AudioMixer.TransitionToSnapshots</c>
        /// (2026-09-26 unity-specialist 裁定:同一白名单面,不给旁路)。</para>
        /// <para>产物缺失 ⇒ 错误(拒以空集冒充绿);SnapshotField = 调用前最近读取的
        /// <c>AudioMixerSnapshot</c> 字段(回溯 ≤ 16 条指令)。</para></summary>
        public static IReadOnlyList<string> ScanTransitionCallsites(
            string dllPath, out List<TransitionCallsite> callsites)
        {
            callsites = new List<TransitionCallsite>();
            var errors = new List<string>();
            if (!File.Exists(dllPath))
            {
                errors.Add($"[AC-44-C1] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿");
                return errors;
            }

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                foreach (TypeDefinition type in AllTypes(asm.MainModule))
                {
                    if (type.Name == "<Module>") continue;
                    foreach (MethodDefinition method in type.Methods)
                    {
                        if (!method.HasBody) continue;
                        // 2026-09-26 修:Cecil 的 Body.Instructions 是 Collection<Instruction>
                        // (实现 IList<T>)—— 不能赋 List<T>(CS0029),以 IList 承载
                        IList<Instruction> instructions = method.Body.Instructions;
                        for (int i = 0; i < instructions.Count; i++)
                        {
                            Instruction instr = instructions[i];
                            if (!(instr.Operand is MethodReference mr)) continue;
                            if (!IsSnapshotSwitchMember(mr)) continue;
                            callsites.Add(new TransitionCallsite
                            {
                                DeclaringType = type.FullName,
                                Method = method.Name,
                                SnapshotField = FindSnapshotFieldRead(instructions, i),
                            });
                        }
                    }
                }
            }
            return errors;
        }

        /// <summary>项目扫描面:三个运行期 gameplay 装配;编译失败 ⇒ 拒扫陈旧产物(假绿防护,
        /// 承 b5 同格)。</summary>
        public static IReadOnlyList<string> ScanProjectTransitionCallsites(
            out List<TransitionCallsite> callsites)
        {
            callsites = new List<TransitionCallsite>();
            var errors = new List<string>();
            if (EditorUtility.scriptCompilationFailed)
            {
                errors.Add("[AC-44-C1] scriptCompilationFailed = true ⇒ 拒扫陈旧产物(读上一版 DLL = 假绿)");
                return errors;
            }
            foreach (string asmName in TransitionScanAssemblies)
            {
                string path = AssemblyGates.ScriptAssemblyPath(asmName);
                // 空装配(只有 asmdef、零 .cs)⇒ Unity 不产出 dll,也**没有任何调用点可扫** ——
                // 这不是覆盖缺口,跳过;反之 dll 缺失但源码在 ⇒ 仍走下方「拒以空集冒充绿」。
                if (!File.Exists(path) && !AssemblyHasAnySource(asmName))
                    continue;
                errors.AddRange(ScanTransitionCallsites(path, out List<TransitionCallsite> found));
                callsites.AddRange(found);
            }
            return errors;
        }

        /// <summary>该装配名下是否真有 <c>.cs</c> 源码(定位同名 <c>.asmdef</c> 所在目录树)。
        /// 用于区分「空装配不出产物」(跳过)与「有源码却缺产物」(红)。</summary>
        private static bool AssemblyHasAnySource(string asmName)
        {
            foreach (string asmdef in Directory.GetFiles(
                         UnityEngine.Application.dataPath, "*.asmdef", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(asmdef);
                if (!text.Contains("\"" + asmName + "\""))
                    continue;
                string dir = Path.GetDirectoryName(asmdef);
                if (dir != null &&
                    Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Length > 0)
                    return true;
            }

            return false;
        }

        /// <summary>AC-44-C1 判据:每个调用点 ∈ 登记集,类别 ∈ {玩家行为, 世界可感知声};
        /// 登记了字段约束的调用点,其快照字段读取必须匹配(不可解析 = 失败关闭)。</summary>
        public static IReadOnlyList<string> ValidateTransitionCallsites(
            IReadOnlyList<TransitionCallsite> callsites)
        {
            var errors = new List<string>();
            if (callsites == null)
                return new[] { "[NOT-RUN 守卫] 调用点集缺失(null)—— 白名单断言不可执行" };

            foreach (TransitionCallsite callsite in callsites)
            {
                SnapshotCallsiteRule rule = null;
                foreach (SnapshotCallsiteRule candidate in TransitionCallsiteRules)
                    if (string.Equals(candidate.Callsite, callsite.Key, StringComparison.Ordinal))
                    { rule = candidate; break; }

                if (rule == null)
                {
                    errors.Add($"[AC-44-C1] 调用点「{callsite.Key}」∉ 快照切换调用点白名单" +
                               "(玩家行为 / 世界可感知声两类)—— 非拟物状态变化触发快照 = 构建失败");
                    continue;
                }
                if (rule.Category != PlayerActionCategory && rule.Category != WorldPerceptibleCategory)
                {
                    errors.Add($"[AC-44-C1] 规则「{rule.Callsite}」类别「{rule.Category}」∉ 两类白名单 —— " +
                               "登记集本身的类别封闭性");
                }
                if (rule.AllowedSnapshotFields == null) continue;

                if (callsite.SnapshotField == null)
                {
                    errors.Add($"[AC-44-C1] 调用点「{callsite.Key}」的快照字段不可解析(经局部变量/参数" +
                               "取快照)—— 字段级判据失败关闭,请登记字段或改读字段");
                    continue;
                }
                bool allowed = false;
                foreach (string field in rule.AllowedSnapshotFields)
                    if (string.Equals(field, callsite.SnapshotField, StringComparison.Ordinal))
                        allowed = true;
                if (!allowed)
                    errors.Add($"[AC-44-C1] 调用点「{callsite.Key}」读取快照字段「{callsite.SnapshotField}」," +
                               $"∉ 允许集 {{{string.Join(", ", rule.AllowedSnapshotFields)}}}");
            }
            return errors;
        }

        /// <summary>AC-44-C1 DialogueFocus 半边:<c>DialogueSnapshotField</c> 只允许出现在
        /// 玩家对话入口调用点。</summary>
        public static IReadOnlyList<string> ValidateSnapshotFieldOwnership(
            IReadOnlyList<TransitionCallsite> callsites)
        {
            var errors = new List<string>();
            if (callsites == null)
                return new[] { "[NOT-RUN 守卫] 调用点集缺失(null)—— 字段归属断言不可执行" };

            foreach (TransitionCallsite callsite in callsites)
            {
                if (!string.Equals(callsite.SnapshotField, DialogueSnapshotField,
                        StringComparison.Ordinal))
                    continue;
                if (!DialogueOwnerCallsites.Contains(callsite.Key))
                    errors.Add($"[AC-44-C1] DialogueFocus 字段在非对话入口「{callsite.Key}」被读 —— " +
                               "DialogueFocus 仅限玩家主动发起对话(病人自发呻吟/咳嗽不触发,规则三)");
            }
            return errors;
        }

        /// <summary>源文本层(承 b5①b):反射字符串形态的快照切换字面量 = 红
        /// (IL 层只见 ldstr,拦不住 <c>GetMethod("TransitionTo")</c>)。</summary>
        /// <param name="fileLabel">诊断用文件标签。</param>
        /// <param name="lines">源行(Caller 已决定是否剥注释;字符串须保留)。</param>
        public static IReadOnlyList<string> ValidateTransitionReflectionStrings(
            string fileLabel, IEnumerable<string> lines)
        {
            var errors = new List<string>();
            if (lines == null)
                return new[] { "[NOT-RUN 守卫] 源行缺失(null)—— 反射字符串面不可执行" };

            int index = 0;
            foreach (string line in lines)
            {
                index++;
                if (line == null) continue;
                bool hits = line.Contains("\"TransitionTo\"") ||
                            line.Contains("\"TransitionToSnapshots\"");
                if (hits)
                    errors.Add($"[{fileLabel}:{index}] 反射字符串形态的快照切换(TransitionTo 字面量)" +
                               "—— AC-44-C1 旁路拒收(IL 层只是 ldstr)");
            }
            return errors;
        }

        /// <summary>两级组纪律源文本面:44 源出现 <c>ClearFloat</c> ⇒ 红(玩家参数一旦
        /// <c>SetFloat</c> 后禁交还快照控制 —— 否则出入快照打回玩家值)。</summary>
        /// <param name="fileLabel">诊断用文件标签。</param>
        /// <param name="lines">源行。</param>
        public static IReadOnlyList<string> ValidateNoClearFloat(string fileLabel, IEnumerable<string> lines)
        {
            var errors = new List<string>();
            if (lines == null)
                return new[] { "[NOT-RUN 守卫] 源行缺失(null)—— ClearFloat 面不可执行" };

            int index = 0;
            foreach (string line in lines)
            {
                index++;
                if (line != null && line.Contains(".ClearFloat("))
                    errors.Add($"[{fileLabel}:{index}] 出现 ClearFloat —— 玩家参数禁交还快照控制" +
                               "(unity-specialist 6.3:SetFloat 后 ClearFloat 会让快照重新接管," +
                               "出入快照即打回玩家滑块值)");
            }
            return errors;
        }

        /// <summary>AC-44-E3 ③:reverb preset 切换的所有者命名空间必须 = 44 前缀。</summary>
        /// <param name="declaringNamespace">切换实现所在命名空间。</param>
        public static IReadOnlyList<string> ValidateReverbSwitchOwner(string declaringNamespace)
        {
            if (string.Equals(declaringNamespace, AssemblyGates.AudioModuleNamespacePrefix,
                    StringComparison.Ordinal))
                return Array.Empty<string>();
            return new[]
            {
                $"[AC-44-E3 ③] reverb preset 切换所有者 = 「{declaringNamespace ?? "<null>"}」≠ 44 " +
                $"(「{AssemblyGates.AudioModuleNamespacePrefix}」)—— 触发输入 = 房间格,表现层派生,不进流",
            };
        }

        // ══════════════ 解析与小件 ══════════════

        /// <summary>一份 YAML 文档(按 <c>--- !u!classId &amp;fileID</c> 切分)的承载。</summary>
        private sealed class MixerDoc
        {
            public string ClassName;
            public long FileId;
            public string Name;
            public long MasterGroupId;
            public readonly List<long> Children = new List<long>();
            public readonly List<string> Exposed = new List<string>();
            public readonly List<long> CaptureTargets = new List<long>();
            public readonly List<string> CaptureNames = new List<string>();
            // 2026-09-26 黄金样例校准:send 不是组上的条目列表 —— 组挂 m_Effects(效果 fileID),
            // send = 效果文档的 m_SendTarget(指向目标组的 effect)。旧 Sends 字段已删。
            public readonly List<long> Effects = new List<long>();
            public string EffectName;
            public long SendTarget;
            /// <summary>组的电平参数哈希(<c>m_Volume: &lt;32 hex&gt;</c>)—— 快照捕获键的**解析桥梁**:
            /// 实测 <c>m_FloatValues</c> 的键就是这个哈希(非组 fileID、非组名)。仅组文档有值。</summary>
            public string VolumeHash;
        }

        /// <summary>手写逐行状态机:多文档切分 + 关键字段提取(零正则、零第三方解析器)。
        /// <para>字段口径 = 夹具约定(对齐点见文件头);解析级错误并入 <paramref name="errors"/>。</para></summary>
        private static List<MixerDoc> ParseMixerYaml(string yamlText, List<string> errors)
        {
            var docs = new List<MixerDoc>();
            if (string.IsNullOrEmpty(yamlText)) return docs;

            MixerDoc current = null;
            bool inChildren = false, inFloatValues = false, inExposed = false, inEffects = false;
            int lineNumber = 0;

            foreach (string rawLine in yamlText.Replace("\r\n", "\n").Split('\n'))
            {
                lineNumber++;
                string trimmed = rawLine.Trim();

                if (rawLine.StartsWith("--- ", StringComparison.Ordinal))
                {
                    inChildren = inFloatValues = inExposed = inEffects = false;
                    current = new MixerDoc
                    {
                        // 2026-09-26 修:类型名由下方「PendingClassName 回填段」从头行后的
                        // 首个类型名行取出 —— 头行本身只含 classId + fileID(原 ExtractTypeName
                        // 调用是重构残留的悬空引用)
                        ClassName = PendingClassName,
                        FileId = ExtractFileIdAfterAmp(rawLine),
                    };
                    docs.Add(current);
                    continue;
                }
                if (current == null) continue;

                // 类型名回填:头行 `--- !u!240 &2001` 之后的首个非 m_ 键行即文档类型名
                // (如 `AudioMixerGroup:`)—— 解析面不依赖 classId 数字表。
                if (current.ClassName == PendingClassName)
                {
                    if (trimmed.Length == 0) continue;
                    if (trimmed.EndsWith(":", StringComparison.Ordinal) &&
                        !trimmed.StartsWith("m_", StringComparison.Ordinal) &&
                        !trimmed.StartsWith("- ", StringComparison.Ordinal))
                    {
                        current.ClassName = trimmed.Substring(0, trimmed.Length - 1);
                        continue;
                    }
                    errors.Add($"[解析:L{lineNumber}] 文档头后未见类型名行(得到「{trimmed}」)—— " +
                               "解析面异常,不得静默通过");
                    continue;
                }

                // 列表态:先吃列表条目,遇到非条目行退出列表态(该行 fall-through 到键解析)
                if (inChildren)
                {
                    if (trimmed.StartsWith("- ", StringComparison.Ordinal) ||
                        trimmed.Equals("-", StringComparison.Ordinal))
                    {
                        long child = ExtractFileId(trimmed);
                        if (child != 0) current.Children.Add(child);
                        continue;
                    }
                    if (trimmed.Length == 0) continue;
                    inChildren = false;
                }
                if (inEffects)
                {
                    // 组的 m_Effects:与 m_Children 同形的 fileID 列表(黄金样例实测)
                    if (trimmed.StartsWith("- ", StringComparison.Ordinal) ||
                        trimmed.Equals("-", StringComparison.Ordinal))
                    {
                        long effectId = ExtractFileId(trimmed);
                        if (effectId != 0) current.Effects.Add(effectId);
                        continue;
                    }
                    if (trimmed.Length == 0) continue;
                    inEffects = false;
                }
                if (inFloatValues)
                {
                    // 快照捕获(黄金样例):`m_FloatValues: {}` 内联流式;块式多行键值同样吃
                    if (trimmed.StartsWith("m_", StringComparison.Ordinal))
                        inFloatValues = false;   // 下一个键,fall-through 到键解析
                    else
                    {
                        if (trimmed.Length > 0) ParseFloatValuesContent(trimmed, current);
                        continue;
                    }
                }
                if (inExposed)
                {
                    if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                    {
                        // 真格式两种:`- name: x`(内联)与 `- guid: <hex>`(两行一条的 map 列表)
                        int nameIdx = trimmed.IndexOf("name:", StringComparison.Ordinal);
                        if (nameIdx >= 0)
                            current.Exposed.Add(ValueAfter(trimmed, "name:").Trim());
                        continue;
                    }
                    if (trimmed.StartsWith("name:", StringComparison.Ordinal))
                    {
                        // 2026-09-26 七轮真资产校准:map 列表条目第二行才是 name ——
                        // 原状态机在此退出(把 `name:` 当块结束)⇒ 7 条全丢、报「为空/缺失」
                        current.Exposed.Add(ValueAfter(trimmed, "name:").Trim());
                        continue;
                    }
                    if (trimmed.Length == 0) continue;
                    inExposed = false;
                }

                if (trimmed.StartsWith("m_Children:", StringComparison.Ordinal)) { inChildren = true; continue; }
                if (trimmed.StartsWith("m_Effects:", StringComparison.Ordinal)) { inEffects = true; continue; }
                if (trimmed.StartsWith("m_FloatValues:", StringComparison.Ordinal))
                {
                    // 黄金样例实测:`m_FloatValues: {}` / `{fileId: dB, ...}`(内联流式)
                    string floatValueContent = ValueAfter(trimmed, "m_FloatValues:");
                    if (floatValueContent != null && floatValueContent.IndexOf('{') >= 0)
                        ParseFloatValuesContent(floatValueContent, current);
                    inFloatValues = true;   // 块式多行形态兜底(下一行非 m_ 键则继续吃)
                    continue;
                }
                if (trimmed.StartsWith("m_ExposedParameters:", StringComparison.Ordinal))
                {
                    string exposedContent = ValueAfter(trimmed, "m_ExposedParameters:");
                    if (exposedContent != null && exposedContent.Contains("[]"))
                        continue;                       // 空内联(黄金样例:`m_ExposedParameters: []`)
                    if (exposedContent != null && exposedContent.IndexOf('[') >= 0)
                        ParseExposedInline(exposedContent, current);   // 流式内联
                    else
                        inExposed = true;               // 块式多行 `- name: ...`
                    continue;
                }
                if (trimmed.StartsWith("m_EffectName:", StringComparison.Ordinal))
                {
                    current.EffectName = ValueAfter(trimmed, "m_EffectName:");
                    continue;
                }
                if (trimmed.StartsWith("m_SendTarget:", StringComparison.Ordinal))
                {
                    current.SendTarget = ExtractFileId(trimmed);   // {fileID: 0} = 无 send
                    continue;
                }
                if (trimmed.StartsWith("m_Volume:", StringComparison.Ordinal))
                {
                    // 组电平参数哈希 —— 快照捕获键的解析桥梁(实测 m_FloatValues 键 == 本值)
                    current.VolumeHash = ValueAfter(trimmed, "m_Volume:").Trim();
                    continue;
                }
                if (trimmed.StartsWith("m_MasterGroup:", StringComparison.Ordinal))
                {
                    current.MasterGroupId = ExtractFileId(trimmed);
                    continue;
                }
                if (trimmed.StartsWith("m_Name:", StringComparison.Ordinal))
                {
                    current.Name = ValueAfter(trimmed, "m_Name:").Trim().Trim('"');
                    continue;
                }
            }
            if (docs.Count == 0)
                errors.Add("[NOT-RUN 守卫] YAML 无任何 `--- !u!` 文档 —— 解析面为空,不得静默通过");
            return docs;
        }

        /// <summary>解析 <c>m_FloatValues</c> 内容(流式 <c>{k: v, k: v}</c> 或块式键值行):
        /// **数字键**按组 fileID 入 <c>CaptureTargets</c>(解析到组名后参与交集);
        /// 非数字键按字面量入 <c>CaptureNames</c>(直接与 exposed 求交)。值(dB)不参与判据。
        /// <para>黄金样例捕获为空 —— 非数字键 / 哈希键的真形态未实证,对齐点见
        /// <see cref="ValidateSnapshotExposedDisjoint"/> 注。</para></summary>
        private static void ParseFloatValuesContent(string content, MixerDoc doc)
        {
            if (doc == null || content == null) return;
            int i = 0;
            while (i < content.Length)
            {
                char c = content[i];
                if (!char.IsLetterOrDigit(c) && c != '_') { i++; continue; }
                int start = i;
                while (i < content.Length &&
                       (char.IsLetterOrDigit(content[i]) || content[i] == '_'))
                    i++;
                string token = content.Substring(start, i - start);
                int j = i;
                while (j < content.Length && (content[j] == ' ' || content[j] == '\t')) j++;
                if (j >= content.Length || content[j] != ':') continue;   // 裸值(如 -12 的数字部分)忽略
                if (long.TryParse(token, out long numeric) && numeric != 0)
                    doc.CaptureTargets.Add(numeric);
                else
                    doc.CaptureNames.Add(token);
                i = j + 1;
            }
        }

        /// <summary>解析 <c>m_ExposedParameters</c> 的流式内联形态(<c>[ {name: x}, ... ]</c>);
        /// 块式多行走 inExposed 状态机。</summary>
        private static void ParseExposedInline(string content, MixerDoc doc)
        {
            if (doc == null || content == null) return;
            int from = 0;
            while (true)
            {
                int idx = content.IndexOf("name:", from, StringComparison.Ordinal);
                if (idx < 0) break;
                int valueStart = idx + "name:".Length;
                while (valueStart < content.Length &&
                       (content[valueStart] == ' ' || content[valueStart] == '\t'))
                    valueStart++;
                int end = valueStart;
                while (end < content.Length &&
                       content[end] != ',' && content[end] != ']' && content[end] != '}')
                    end++;
                string value = content.Substring(valueStart, end - valueStart).Trim().Trim('"');
                if (value.Length > 0) doc.Exposed.Add(value);
                from = end;
            }
        }

        /// <summary>类型名占位(头行后的首个类型名行回填前)。</summary>
        private const string PendingClassName = "\u0000pending";

        private static long ExtractFileIdAfterAmp(string headerLine)
        {
            int amp = headerLine.IndexOf('&');
            if (amp < 0) return 0;
            int start = amp + 1;
            int end = start;
            while (end < headerLine.Length && (char.IsDigit(headerLine[end]) || headerLine[end] == '-'))
                end++;
            return long.TryParse(headerLine.Substring(start, end - start), out long id) ? id : 0;
        }

        private static long ExtractFileId(string line)
        {
            int idx = line.IndexOf("fileID:", StringComparison.Ordinal);
            if (idx < 0) return 0;
            int start = idx + "fileID:".Length;
            while (start < line.Length && char.IsWhiteSpace(line[start])) start++;
            int end = start;
            if (end < line.Length && line[end] == '-') end++;
            while (end < line.Length && char.IsDigit(line[end])) end++;
            return long.TryParse(line.Substring(start, end - start), out long id) ? id : 0;
        }

        private static string ValueAfter(string line, string key)
        {
            int idx = line.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return null;
            string value = line.Substring(idx + key.Length).Trim();
            return value;
        }

        private static bool IsIdentifierChar(char c)
            => char.IsLetterOrDigit(c) || c == '_';

        private static bool Contains(IReadOnlyList<string> set, string value)
        {
            if (value == null || set == null) return false;
            for (int i = 0; i < set.Count; i++)
                if (string.Equals(set[i], value, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>成员谓词:UnityEngine.Audio.AudioMixerSnapshot.TransitionTo +
        /// UnityEngine.Audio.AudioMixer.TransitionToSnapshots(同白名单面,不给旁路)。</summary>
        private static bool IsSnapshotSwitchMember(MethodReference mr)
        {
            string declaring = mr.DeclaringType?.FullName ?? "";
            if (declaring == "UnityEngine.Audio.AudioMixerSnapshot" && mr.Name == "TransitionTo")
                return true;
            if (declaring == "UnityEngine.Audio.AudioMixer" && mr.Name == "TransitionToSnapshots")
                return true;
            return false;
        }

        /// <summary>回溯调用点前最近一次 <c>AudioMixerSnapshot</c> 类型字段读取(≤ 16 条指令);
        /// 无则 null(经局部变量 / 参数取快照)。</summary>
        private static string FindSnapshotFieldRead(IList<Instruction> instructions, int callIndex)
        {
            int lowerBound = Math.Max(0, callIndex - 16);
            for (int i = callIndex - 1; i >= lowerBound; i--)
            {
                Instruction instr = instructions[i];
                if (instr.OpCode.Code != Code.Ldfld && instr.OpCode.Code != Code.Ldsfld) continue;
                if (!(instr.Operand is FieldReference fr)) continue;
                if (fr.FieldType != null && fr.FieldType.Name == "AudioMixerSnapshot")
                    return fr.Name;
            }
            return null;
        }

        private static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
        {
            foreach (TypeDefinition type in module.Types)
            {
                yield return type;
                foreach (TypeDefinition nested in NestedTypes(type))
                    yield return nested;
            }
        }

        private static IEnumerable<TypeDefinition> NestedTypes(TypeDefinition type)
        {
            foreach (TypeDefinition nested in type.NestedTypes)
            {
                yield return nested;
                foreach (TypeDefinition deeper in NestedTypes(nested))
                    yield return deeper;
            }
        }
    }
}
