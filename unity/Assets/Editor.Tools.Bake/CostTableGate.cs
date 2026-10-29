// T1 · S9 —— 53 用药代价表的**构建前门**(`IPreprocessBuildWithReport`)。
//
// 权威来源:ADR-035 §Migration Plan **S9**(2026-10-30 用户裁定「加 S9」)· §Validation V-5 / V-6 / V-7
//          · CLAUDE.md §构建前门(BuildGate):绿 = 没报错 ≠ 门跑了 ⇒ 本门须走探针轮
//
// ⚠️ **为什么本件住在 `Editor.Tools.Bake` 而不是 `Editor.Tools.Gates`**(与 ADR-035 S9 行的字面
//   「`AssemblyGates` 新增 `CostTableGate`」**不一致**,属实现期发现的硬约束,已在 ADR 该行加注):
//   程序集边 `Editor.Tools.Bake → Editor.Tools.Gates` **已存在**(Bake 的 asmdef 引用了它)⇒
//   反向再引 = **循环引用**,Unity 直接拒绝编译。而本门必须调用 `CostTableBinder`(住 Bake)。
//   ⇒ 本门自带 `IPreprocessBuildWithReport`(Unity 按反射发现,与 `AssemblyGates.BuildGate` 并列),
//   **不**经 `AssemblyGates.RunAll()`。代价:门的清单位于两处 —— 见文件末尾「门的清单」注。
//
// ⚠️ **构建期做什么**:对**仓里的** `53_cost_table.json` + 9 的 `disease_registry.json` 跑
//   `CostTable53Bake`(它内含 `CostTableBinder` 的全部构建期校验)。任一条失败 ⇒ 聚合进
//   `BuildFailedException` ⇒ **构建中止**。这正是 V-5 / V-6 / V-7 字面要的「构建 Failure」。
//
// ⚠️ **它不做的事**:不写 cooked 产物(那是 `DataBakeMenu.BakeCostTable` 的职责)。
//   门 = 只读 + 判据;写盘留给菜单 ⇒ 构建门永不改工作树。

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>代价表构建前门:源表 + 注册表 → 全部构建期校验,任一失败 ⇒ 构建中止。</summary>
    public static class CostTableGate
    {
        /// <summary>跑门(可注入仓根;**测试与构建走同一函数**,反空转)。</summary>
        /// <param name="repoRoot">仓根(其下有 `assets/data/`)。</param>
        /// <returns>错误消息列表;空 = 通过。</returns>
        /// <remarks>⚠️ 本函数**永不抛**(`InvalidOperationException` 也折成错误串):
        /// 门的契约是「返回判据」,抛异常会把「门跑过」与「门没跑成」混成一种形状。</remarks>
        public static List<string> RunAll(string repoRoot)
        {
            var errs = new List<string>();
            if (string.IsNullOrEmpty(repoRoot))
            {
                errs.Add("[CostTableGate] 仓根为空 —— 门无法定位 assets/data/(扫描面丢失,按失败处理)");
                return errs;
            }

            string costPath = Path.Combine(repoRoot, CostTable53Bake.DataDirName, CostTable53Bake.SourceFileName);
            string regPath = Path.Combine(repoRoot, CostTable53Bake.DataDirName, CostTable53Bake.RegistryFileName);
            if (!File.Exists(costPath) || !File.Exists(regPath))
            {
                if (!File.Exists(costPath)) errs.Add("[CostTableGate] 源文件不存在:" + costPath);
                if (!File.Exists(regPath)) errs.Add("[CostTableGate] 注册表不存在:" + regPath);
                return errs;
            }

            return RunFromJson(File.ReadAllText(costPath), File.ReadAllText(regPath));
        }

        /// <summary>
        /// 判据入口(测试与菜单直用):直接喂两个 JSON 串,**不读盘**。
        /// <para>与 <see cref="RunAll"/> 走**同一**校验体(`CostTable53Bake.BakeFromJson`)——
        /// 测试因此不必落临时文件,也不会因「测试读的路径与构建读的路径不同」而假绿。</para>
        /// </summary>
        /// <param name="costJson">`53_cost_table.json` 源文本。</param>
        /// <param name="registryJson">`disease_registry.json` 源文本。</param>
        public static List<string> RunFromJson(string costJson, string registryJson)
        {
            var errs = new List<string>();
            try
            {
                CostTable53Bake.BakeOutput output = CostTable53Bake.BakeFromJson(costJson, registryJson);
                // 告警是**非致命**的(V-10 的 TD-sim-03 可见性告警就走这条)——
                // 放进日志,不放进 errs,否则「无合法行」会让每一次构建都红。
                if (output.Bound.Warnings != null)
                    foreach (string w in output.Bound.Warnings)
                        Debug.LogWarning("[CostTableGate] " + w);
                return errs;
            }
            catch (BakeValidationException ex)
            {
                if (ex.Errors != null)
                    foreach (string e in ex.Errors)
                        errs.Add("[CostTableGate] " + e);
                if (errs.Count == 0) errs.Add("[CostTableGate] 烘焙校验失败(错误集为空)");
                return errs;
            }
            catch (Exception ex)
            {
                // 解析 / 外键 / 编码任一侧的硬失败:折成判据,不吞
                errs.Add("[CostTableGate] " + ex.GetType().Name + ":" + ex.Message);
                return errs;
            }
        }

        /// <summary>默认仓根 = `Application.dataPath` 的上两级(`<repo>/unity/Assets` → `<repo>`)。</summary>
        public static string DefaultRepoRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>
        /// 构建前门。`callbackOrder = 10` —— 排在 <c>AssemblyGates.BuildGate</c>(0)之后,
        /// 以便装配 / IL 类门先报(它们的报错更靠底层,先看不然后者被表层错误淹没)。
        /// </summary>
        private sealed class PreprocessBuild : IPreprocessBuildWithReport
        {
            public int callbackOrder => 10;

            public void OnPreprocessBuild(BuildReport report)
            {
                List<string> errs = RunAll(DefaultRepoRoot());
                if (errs.Count == 0) return;

                foreach (string e in errs) Debug.LogError(e);
                throw new BuildFailedException(
                    "[CostTableGate] 53 用药代价表构建期校验失败(" + errs.Count + " 条,见日志红行)。" +
                    "ADR-035 V-5 / V-6 / V-7 —— 致死行 / 空 harm_kind_enabled / 过量支启用一律构建中止。");
            }
        }
    }
}
