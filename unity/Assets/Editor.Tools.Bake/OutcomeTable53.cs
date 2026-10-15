// S12-9 · story-002 —— 53 判定表两阶段烘焙的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定 + 编码)。
//
// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · OutcomeTableBinder(唯一校验点)· OutcomeTableCookedWriter(编码面)
//          · Outcome53(运行期表数据集 —— 菜单与测试共用同一台机器,反空转)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码
//   (承 DiagnosisReadFloorBaker 同纪律)⇒ EditMode 测试可直接驱动。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Consequences;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`53_outcome_table.json` 烘焙装载器(Bind + cooked 字节 + 运行期 Table 直产)。</summary>
    public static class OutcomeTable53
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string SourceFileName = "53_outcome_table.json";

        /// <summary>源文件所在目录(相对仓根;承既有先例 `assets/data/`)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙输出(cooked 字节 + 绑定结果)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(行集 / 档数 / default;测试直用)。</summary>
            public readonly OutcomeTableBinder.BindResult Bound;

            public BakeOutput(byte[] cooked, uint configVersion, OutcomeTableBinder.BindResult bound)
            {
                Cooked = cooked; ConfigVersion = configVersion; Bound = bound;
            }
        }

        /// <summary>从仓根烘焙(读源 → 绑定 + 四条校验 → 编码)。</summary>
        /// <param name="repoRoot">仓根(含 `assets/data/`)。</param>
        /// <exception cref="BakeValidationException">词法 / 四条校验任一失败(聚合;
        /// 异常类型 = Bake 目录统一顶层类 —— 嵌套重名类会被顶层遮蔽,已删)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, SourceFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(
                    new List<string> { $"源文件不存在:{path}" });

            string json = File.ReadAllText(path);
            var bound = OutcomeTableBinder.Bind(json);

            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(SourceFileName, json),
            });

            byte[] cooked = OutcomeTableCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }

        /// <summary>绑定入口(测试负例直接喂 JSON 串 —— 不落盘;与 BakeFromRepo 同一校验体)。</summary>
        /// <exception cref="BakeValidationException">任一校验失败(聚合)。</exception>
        public static OutcomeTableBinder.BindResult BindForTest(string json)
            => OutcomeTableBinder.Bind(json);

        // ════════════════════════════════════════════════════════════════════
        // 运行期 Table 直产(测试用 —— 反空转;生产装载经 IDataProvider 读 cooked,归装配轮)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>烘焙**并**物化运行期 <see cref="Outcome53.Table"/>(测试驱动同一台机器)。</summary>
        public static Outcome53.Table BakeTableFromRepo(string repoRoot)
        {
            BakeOutput output = BakeFromRepo(repoRoot);
            return Materialize(output.Bound);
        }

        /// <summary>绑定结果 → 运行期表。</summary>
        internal static Outcome53.Table Materialize(OutcomeTableBinder.BindResult bound)
        {
            var rows = new Dictionary<int, Outcome53.Outcome>();
            foreach (var kv in bound.Rows)
            {
                int key = Outcome53.RowKeyOf(kv.Key.P, kv.Key.C, kv.Key.D, kv.Key.UBand);
                rows[key] = kv.Value;
            }
            return Outcome53.CreateTable(rows, bound.UBandCount, true, bound.DefaultOutcome);
        }
    }
}
