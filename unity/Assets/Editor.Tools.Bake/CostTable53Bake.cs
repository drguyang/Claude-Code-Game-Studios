// T1 · S9 —— 53 用药代价表两阶段烘焙的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定 + 编码)。
//
// 权威来源:
//   ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//   ADR-035 §Decision ②(效果一律外键)③(L3 运行期视图)⑥(过量支挂起)
//   CostTableBinder(唯一构建期校验点)· CostTableCookedWriter(编码面)· CostTable53(Sim 侧读形)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码
//   (承 DelayTable53Bake / OutcomeTable53 同纪律)⇒ EditMode 测试可直接驱动。
//
// ⚠️ **两处输入**:本表**同时**依赖 `53_cost_table.json` 与 9 的 `disease_registry.json`
//   (N-1 / N-3 / V-10 的宾语都在后者里)⇒ `ConfigVersion` 必须**两个源一起**派生,
//   否则「只改注册表」不会让 ConfigVersion 变 ⇒ 与存档头的比对(ADR-010 §七)静默失真。
//
// ⚠️ **本件负责把作者态 `injury_id` 字符串解析成整数**:`CostTableBinder` 只**验**外键存在
//   (S3 已结案,不改它的判据面),而 cooked 产物按 ADR-014「玩家构建零解析器」必须自带
//   解析结果 ⇒ 解析落在这里。解析用的注册表绑定**复用 `DiseaseRegistryBinder`**
//   (与 Binder 内部同一入口),不另写第二个解析器。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim;              // DiseaseRegistryEntry
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`53_cost_table.json` 烘焙装载器(Bind + 外键解析 + cooked 字节)。</summary>
    public static class CostTable53Bake
    {
        /// <summary>作者态源文件名(单一出处 = <see cref="CostTableBinder.FileName"/>)。</summary>
        public const string SourceFileName = CostTableBinder.FileName;

        /// <summary>9 的病种注册表源文件名(外键解析目标)。</summary>
        public const string RegistryFileName = "disease_registry.json";

        /// <summary>源文件所在目录(相对仓根;承既有先例 `assets/data/`)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙输出(cooked 字节 + 绑定结果)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32;**两个源一起**派生)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(行集 / schema_version / 告警;测试直用)。</summary>
            public readonly CostTableBinder.BindResult Bound;

            /// <summary>构造烘焙输出(写方 / 测试直用)。</summary>
            public BakeOutput(byte[] cooked, uint configVersion, CostTableBinder.BindResult bound)
            {
                Cooked = cooked; ConfigVersion = configVersion; Bound = bound;
            }
        }

        /// <summary>从仓根烘焙(读两源 → 绑定 + 全部构建期拒收 → 外键解析 → 编码)。</summary>
        /// <exception cref="BakeValidationException">词法 / 任一条校验失败(聚合)。</exception>
        /// <exception cref="InvalidOperationException">外键在注册表绑定结果里找不到 / 重复键。
        /// 两者都属「Binder 与写方口径脱钩」,fail-loud 而不是写出坏产物。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string costPath = Path.Combine(repoRoot, DataDirName, SourceFileName);
            string regPath = Path.Combine(repoRoot, DataDirName, RegistryFileName);

            var missing = new List<string>();
            if (!File.Exists(costPath)) missing.Add($"源文件不存在:{costPath}");
            if (!File.Exists(regPath)) missing.Add($"注册表不存在:{regPath}");
            if (missing.Count > 0) throw new BakeValidationException(missing);

            return BakeFromJson(File.ReadAllText(costPath), File.ReadAllText(regPath));
        }

        /// <summary>从两个 JSON 串烘焙(不落盘;**与 <see cref="BakeFromRepo"/> 同一校验 / 编码路径**)。</summary>
        /// <exception cref="BakeValidationException">词法 / 任一条校验失败(聚合)。</exception>
        public static BakeOutput BakeFromJson(string costJson, string registryJson)
        {
            if (registryJson == null) throw new ArgumentNullException(nameof(registryJson));

            // ① 全部构建期校验(唯一执行体 = CostTableBinder;本件不复制任何一条判据)
            CostTableBinder.BindResult bound = CostTableBinder.Bind(costJson, registryJson);

            // ② 外键解析(作者态键 → 9 注册表整数 id)
            Dictionary<string, int> injuryIds = ResolveInjuryIds(registryJson);

            // ③ ConfigVersion:**两个源一起**派生(见文件头)
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(SourceFileName, costJson ?? string.Empty),
                new KeyValuePair<string, string>(RegistryFileName, registryJson),
            });

            byte[] cooked = CostTableCookedWriter.Write(bound, injuryIds, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }

        /// <summary>注册表绑定结果 → `DiseaseKey` → `DiseaseId`。</summary>
        /// <exception cref="BakeValidationException">注册表本身绑定失败(聚合透传)。</exception>
        /// <exception cref="InvalidOperationException">同键异 id(重复键未被上游拒收 ⇒ 解析结果取决于迭代序 = 非确定)。</exception>
        private static Dictionary<string, int> ResolveInjuryIds(string registryJson)
        {
            DiseaseRegistryBinder.BindResult reg = DiseaseRegistryBinder.Bind(registryJson);

            var byKey = new Dictionary<string, int>(StringComparer.Ordinal);
            var conflicts = new List<string>();
            if (reg.Rows != null)
            {
                for (int i = 0; i < reg.Rows.Count; i++)
                {
                    DiseaseRegistryEntry e = reg.Rows[i];
                    if (e == null || string.IsNullOrEmpty(e.DiseaseKey)) continue;
                    if (byKey.TryGetValue(e.DiseaseKey, out int existing))
                    {
                        if (existing != e.DiseaseId)
                            conflicts.Add($"「{e.DiseaseKey}」同时映射到 id {existing} 与 {e.DiseaseId}");
                        continue;
                    }
                    byKey[e.DiseaseKey] = e.DiseaseId;
                }
            }

            if (conflicts.Count > 0)
                throw new InvalidOperationException(
                    "病种注册表存在**同键异 id** —— 代价表外键解析结果将取决于迭代序(非确定):" +
                    string.Join(";", conflicts));

            return byKey;
        }
    }
}
