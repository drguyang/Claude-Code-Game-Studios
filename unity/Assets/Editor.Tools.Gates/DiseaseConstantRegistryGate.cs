// 9 的调参常量 —— 登记完整性门(PRE-S18-B 的机械半边 · 2026-10-22 立)。
//
// 权威来源:
//   ADR-014 §三 —— 作者态数据的外部化与构建期校验
//   `design/gdd/disease-simulation.md` §五(Tuning Knobs 三阈值 + 干预窗口)
//   `design/registry/entities.yaml` —— 常量登记真源(constants 段)
//   `production/sprint-status.yaml` PRE-S18-B —— 本门的立门缘由
//
// ── 立门缘由(为什么这条不能只靠人看)──────────────────────────────────────
// PRE-S18-B 实测:`RECOVER_THRESHOLD` / `CARE_GAP` 有独立 `name:` 块,而同组的
// `CRITICAL_THRESHOLD` / `COMA_THRESHOLD` / `DEATH_THRESHOLD` / `TREATMENT_WINDOW`
// **只出现在 `9.疾病状态机` 条目的 `variables:` 列表里**(被引用),没有自己的块。
// 后果不是「少写四段 YAML」,而是 **「registry 是唯一真源」在这四个量上不成立** ——
// 有人改 `entities.yaml` 改不动它们(那儿没有它们),只能去改 `StateMachine.cs`
// 的 `const`,而那条路径**没有任何东西拦着**。
//
// ⚠️ 本门**只查登记存在性,不查数值** —— 数值归用户数值轮(GDD §五 default 全 `*待定*`)。
//    把「值没裁」判成红会让本门恒红,那正是「判据对合法输入类误判」的形态。
//
// ── 两条判据 ───────────────────────────────────────────────────────────────
//   D1 `name:` 块存在 —— 每个必登记常量须有独立顶层块(`^  - name: <X>$`)。
//      缺块 ⇒ 红(块可能只活在 `variables:` 列表里,那是引用不是登记)。
//   D2 块内必填字段齐 —— `status` / `source` / `referenced_by` / `value` /
//      `unit` / `constraint` 六项缺一即红。缺 `value` 尤其危险:读的人会以为
//      「没写 value = 用默认值」,而这里的语义是「还没裁」。
//
// ⚠️ **本门的盲区方向(如实登记,禁借绿)**:本门是**文本级**扫描,不建 YAML AST
//    (仓库的 `Editor.Tools.Gates` 装配无 YAML 解析依赖,引入它是架构级改动)。
//    ⇒ 缩进异常 / 块被写成 flow 样式 / 块挪进嵌套层,本门**看不见**。
//    ⚠️ 但看不见的后果是**转红**(报「缺块」),**不是静默通过** —— 失败方向是**响亮**的
//    (false red),而 false red 会立刻被人发现并改门;真正危险的是 false green。
//    本门**确有一处 false green**,如实列出:**`value:` 字段写了个新占位数它照样绿**
//    —— 判据是「字段在不在」,不是「值裁没裁」。数值裁没裁**不由本门判**(见上一条:
//    判了会恒红)。**⇒ 不得把本门绿读成「数值已裁定」。**

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>
    /// 9 的调参常量登记完整性门 —— `entities.yaml` 的 `constants` 段。
    ///
    /// <para><b>为什么只查这几个量</b>:它们是 PRE-S18-B 实测缺块的四个,加上两个
    /// 同组已有块但**必须持续存在**的(`RECOVER_THRESHOLD` / `CARE_GAP`)——
    /// 后者一并纳入是为了让「有人误删既有块」也转红,而不是只防新增面的回退。</para>
    /// </summary>
    public static class DiseaseConstantRegistryGate
    {
        /// <summary>必须带独立 <c>name:</c> 块的 9 侧调参常量(顺序稳定,便于错误可读)。</summary>
        public static readonly IReadOnlyList<string> RequiredConstants = new[]
        {
            // 三阈值(九态机) —— PRE-S18-B 实测缺块
            "CRITICAL_THRESHOLD",
            "COMA_THRESHOLD",
            "DEATH_THRESHOLD",
            // 干预窗口 —— PRE-S18-B 实测缺块,且连实现期占位值都没有
            "TREATMENT_WINDOW",
            // 同组既有块 —— 纳入以防误删(它们本来就有)
            "RECOVER_THRESHOLD",
            "CARE_GAP",
        };

        /// <summary>每个常量块必填的字段名(D2)。</summary>
        public static readonly IReadOnlyList<string> RequiredFields = new[]
        {
            "status", "source", "referenced_by", "value", "unit", "constraint",
        };

        /// <summary>
        /// 构建期 / 菜单入口 —— 经 <c>Application.dataPath</c> 上溯仓库根取 `entities.yaml`
        /// (与 <see cref="WriterExistenceGate.EntitiesYamlPath"/> 同法)。
        /// </summary>
        public static List<string> RunAll() => RunAll(EntitiesYamlPath());

        /// <summary>
        /// entities.yaml 绝对路径(经 Application.dataPath 上溯仓库根,与 WriterExistenceGate 同法)。
        /// </summary>
        public static string EntitiesYamlPath()
        {
            var assetsDir = Application.dataPath;                 // …/unity/Assets
            var unityDir = Path.GetDirectoryName(assetsDir);      // …/unity
            var repoRoot = Path.GetDirectoryName(unityDir);       // 仓库根
            return Path.Combine(repoRoot, "design", "registry", "entities.yaml");
        }

        /// <summary>
        /// 跑全部门判据。
        /// </summary>
        /// <param name="entitiesYamlPath">
        /// `entities.yaml` 的绝对路径。**必填** —— 判据本体刻意不自己推导路径:
        /// 推导要 <c>Application.dataPath</c>,那会让测试无法用临时文件驱动同一台机器。
        /// 生产路径由无参重载 <see cref="RunAll()"/> 提供。
        /// </param>
        /// <returns>违例串列表(空 = 全过)。</returns>
        public static List<string> RunAll(string entitiesYamlPath)
        {
            var errs = new List<string>();

            if (string.IsNullOrEmpty(entitiesYamlPath))
            {
                errs.Add("[DiseaseConstReg] entities.yaml 路径为空 —— 判据不可执行(非「通过」)");
                return errs;
            }
            if (!File.Exists(entitiesYamlPath))
            {
                errs.Add($"[DiseaseConstReg] entities.yaml 不存在:{entitiesYamlPath} —— " +
                         "登记面丢失(假绿面),判据不可执行");
                return errs;
            }

            string src;
            try
            {
                src = File.ReadAllText(entitiesYamlPath);
            }
            catch (Exception ex)
            {
                errs.Add($"[DiseaseConstReg] entities.yaml 读取失败:{ex.Message}");
                return errs;
            }

            foreach (string name in RequiredConstants)
            {
                errs.AddRange(CheckConstant(src, name));
            }

            return errs;
        }

        /// <summary>
        /// D1 + D2:单个常量的块存在性与其必填字段。
        /// </summary>
        public static List<string> CheckConstant(string yamlSource, string constantName)
        {
            var errs = new List<string>();
            if (yamlSource == null) throw new ArgumentNullException(nameof(yamlSource));
            if (string.IsNullOrEmpty(constantName))
                throw new ArgumentException("constantName 不能为空", nameof(constantName));

            string block = ExtractBlock(yamlSource, constantName);
            if (block == null)
            {
                errs.Add($"[DiseaseConstReg/D1] `entities.yaml` 缺 `{constantName}` 的独立 `name:` 块 —— " +
                         "该量可能只被 `variables:` 列表**引用**(那不是登记)。" +
                         "立门缘由见 PRE-S18-B:同组四个量曾全部只以引用形态存在," +
                         "导致「registry 是唯一真源」在该量上不成立。");
                return errs;
            }

            foreach (string field in RequiredFields)
            {
                if (!HasField(block, field))
                {
                    errs.Add($"[DiseaseConstReg/D2] `{constantName}` 块缺必填字段 `{field}:` —— " +
                             "六项(status / source / referenced_by / value / unit / constraint)缺一即红。" +
                             "⚠️ 缺 `value:` 尤其危险:读者会以为「没写 = 用默认值」," +
                             "而这里的真实语义是「尚未裁定」。");
                }
            }

            return errs;
        }

        /// <summary>
        /// 取某个 <c>- name: &lt;X&gt;</c> 顶层块的文本(到下一个同级 <c>- name:</c> 或文件尾)。
        /// 缺失返回 null。
        /// </summary>
        /// <remarks>
        /// ⚠️ 文本级,不建 AST —— 见文件头「本门的盲区方向」。锚串 = 行首恰两个空格 + <c>- name: </c>。
        /// </remarks>
        public static string ExtractBlock(string yamlSource, string constantName)
        {
            if (string.IsNullOrEmpty(yamlSource)) return null;

            // 锚定整行:`  - name: <X>` 且其后只能是行尾(防 `DEATH_THRESHOLD_X` 之类前缀误命中)。
            string head = @"^  - name: " + Regex.Escape(constantName) + @"[ \t]*\r?$";
            Match m = Regex.Match(yamlSource, head, RegexOptions.Multiline);
            if (!m.Success) return null;

            int start = m.Index;
            // 下一个顶层块起点(任意名字)
            Match next = Regex.Match(yamlSource.Substring(start + m.Length),
                @"^  - name: ", RegexOptions.Multiline);
            int end = next.Success ? start + m.Length + next.Index : yamlSource.Length;
            return yamlSource.Substring(start, end - start);
        }

        /// <summary>块内是否含某字段(行首若干空格 + <c>字段名:</c>)。</summary>
        private static bool HasField(string block, string fieldName)
        {
            string pattern = @"^[ \t]+" + Regex.Escape(fieldName) + @":[ \t]*";
            return Regex.IsMatch(block, pattern, RegexOptions.Multiline);
        }
    }
}
