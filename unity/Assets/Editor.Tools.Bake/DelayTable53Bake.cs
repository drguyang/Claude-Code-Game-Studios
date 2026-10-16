// S13 · story-003 —— 53 延迟表两阶段烘焙的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定 + 编码)。
//
// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · DelayTableBinder(唯一构建期校验点)· DelayTableCookedWriter(编码面)
//          · DelayTable53(Sim 侧整数域表 —— 菜单与测试共用同一台机器,反空转)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码
//   (承 OutcomeTable53 / DiagnosisReadFloorBaker 同纪律)⇒ EditMode 测试可直接驱动。
//
// ⚠️ 装载面与 53_outcome_table **完全同构**(用户 2026-10-16 代查确认):不进
//   AddressablesDataProvider / DataCorePreloader、cooked 不入库;运行期真装载归装配轮。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Consequences;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`53_delay_table.json` 烘焙装载器(Bind + cooked 字节 + Sim 侧 Table 直产)。</summary>
    public static class DelayTable53Bake
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string SourceFileName = "53_delay_table.json";

        /// <summary>源文件所在目录(相对仓根;承既有先例 <c>assets/data/</c>)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙输出(cooked 字节 + 绑定结果)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(行集 / schema_version;测试直用)。</summary>
            public readonly DelayTableBinder.BindResult Bound;

            /// <summary>构造烘焙输出(写方 / 测试直用)。</summary>
            public BakeOutput(byte[] cooked, uint configVersion, DelayTableBinder.BindResult bound)
            {
                Cooked = cooked; ConfigVersion = configVersion; Bound = bound;
            }
        }

        /// <summary>从仓根烘焙(读源 → 绑定 + 三条构建期拒收 → 编码)。</summary>
        /// <exception cref="BakeValidationException">词法 / 三条校验任一失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, SourceFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(
                    new List<string> { $"源文件不存在:{path}" });

            string json = File.ReadAllText(path);
            var bound = DelayTableBinder.Bind(json);

            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(SourceFileName, json),
            });

            byte[] cooked = DelayTableCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }

        /// <summary>绑定入口(测试负例直接喂 JSON 串 —— 不落盘;与 BakeFromRepo 同一校验体)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static DelayTableBinder.BindResult BindForTest(string json)
            => DelayTableBinder.Bind(json);

        /// <summary>从 JSON 串烘焙(不落盘;**与 <see cref="BakeFromRepo"/> 同一编码路径**)——
        /// 供测试验证「输出行序与输入行序无关」等写侧性质(写方 <c>internal</c>,测试经本入口驱动)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromJson(string json)
        {
            var bound = DelayTableBinder.Bind(json);
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(SourceFileName, json),
            });
            return new BakeOutput(DelayTableCookedWriter.Write(bound, configVersion), configVersion, bound);
        }

        // ════════════════════════════════════════════════════════════════════
        // Sim 侧整数域表直产(测试用 —— 反空转;生产装载归装配轮)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>烘焙**并**物化 <see cref="DelayTable53.Table"/>(测试驱动同一台机器)。</summary>
        public static DelayTable53.Table BakeTableFromRepo(string repoRoot)
            => Materialize(BakeFromRepo(repoRoot).Bound);

        /// <summary>绑定结果 → Sim 侧整数域表(键 = Outcome 枚举序数)。</summary>
        /// <remarks>⚠️ 先做**跨侧定义域等值断言**(B-W3):Binder 的字符串白名单表
        /// (<see cref="DelayTableBinder.DomainRows"/>)与 Sim 侧 ordinal 派生数组
        /// (<see cref="DelayTable53.DomainOutcomes"/>)是同一域的两处声明,分住两程序集 ——
        /// 无本断言时,GDD 增员 / ordinal 变更会让两侧**静默分叉**(唯一间接兜现是
        /// <c>RowCount</c> 类断言,接不住「改名但数量不变」)。</remarks>
        public static DelayTable53.Table Materialize(DelayTableBinder.BindResult bound)
        {
            AssertDomainDeclarationsMatch(bound);

            var rows = new Dictionary<int, long>();
            if (bound.Rows != null)
                foreach (var kv in bound.Rows)
                {
                    int key = (int)kv.Key;
                    // 键碰撞显式硬失败(承 OutcomeTable53.Materialize 的同一纪律:
                    // 静默覆写 = 键式缺陷的第二静默面,行数仍对得上)。
                    if (rows.ContainsKey(key))
                        throw new BakeValidationException(new List<string>
                        {
                            $"延迟表物化键碰撞:outcome「{kv.Key}」(ordinal={key})已存在 —— " +
                            "枚举序数到整数键的映射须单射(定义域三支互异 ⇒ 构面不可达)"
                        });
                    rows[key] = kv.Value;
                }
            return DelayTable53.CreateTable(rows);
        }

        /// <summary>跨侧定义域等值断言(B-W3 的构建期载体)。</summary>
        /// <exception cref="BakeValidationException">Binder 白名单集合 ≠ Sim 侧 <c>DomainOutcomes</c>
        /// (逐项序数比对:数量或成员任一不符即红 —— 两侧是同一域的两处声明,分叉即第二真源)。</exception>
        private static void AssertDomainDeclarationsMatch(DelayTableBinder.BindResult bound)
        {
            var binderDomain = new HashSet<int>();
            for (int i = 0; i < DelayTableBinder.DomainRows.Length; i++)
                binderDomain.Add((int)DelayTableBinder.DomainRows[i].Outcome);

            var simDomain = new HashSet<int>();
            for (int i = 0; i < DelayTable53.DomainOutcomes.Count; i++)
                simDomain.Add(DelayTable53.DomainOutcomes[i]);

            if (!binderDomain.SetEquals(simDomain))
                throw new BakeValidationException(new List<string>
                {
                    "定义域两处声明分叉(B-W3):Binder 白名单 {" +
                    string.Join(",", Sorted(binderDomain)) + "} ≠ Sim 侧 DomainOutcomes {" +
                    string.Join(",", Sorted(simDomain)) + "} —— 两侧是同一域的两处声明," +
                    "必须逐项等值(否则一侧增员而另一侧静默不变 = 第二真源)",
                });
        }

        /// <summary>集合升序展开(错误消息确定性 —— 不用 HashSet 迭代序)。</summary>
        private static int[] Sorted(HashSet<int> set)
        {
            var arr = new int[set.Count];
            set.CopyTo(arr);
            System.Array.Sort(arr);
            return arr;
        }
    }
}
