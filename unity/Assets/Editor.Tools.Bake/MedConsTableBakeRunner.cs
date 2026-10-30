// 53 三张表的 **CLI / CI 烘焙入口**(`TD-medcons-11` 的解除件 · 2026-10-30)
//
// 权威:
//   ADR-014(两阶段烘焙管线:JSON → binder(校验)→ deterministic `*.cooked`)
//   ADR-036 §Context ⑥(表装载面已就绪:`AddressablesDataProvider.Ensure*` + 预载序列)
//   `tools/level/run_ci.sh` 的退出码约定(**三态**,与 `LevelCiRunner` 同源)
//   `coding-standards.md` §Review Evidence Standards(「某件有门」须给 `file:line` + 可跑命令)
//
// ═══════════════════════════════════════════════════════════════════════════
// 本条债解的是什么(`TD-medcons-11` 原判定)
// ═══════════════════════════════════════════════════════════════════════════
// 原判定:「`53_delay_table` 等表**无 CLI / CI 烘焙入口**(仅 `DataBakeMenu` 菜单项)」。
//
// 实测订正(**前提比原判定细,结论不变**):
//   · `[MenuItem]` 方法**能被** `-executeMethod` 点名(batch 下只是**不会自动触发**)——
//     见 `DataBakeMenu.cs:704-705` 的自陈。⇒ 不是「跑不了」。
//   · 真正的缺口有三层:
//     ① **没有批量入口** —— 三张表是三个独立 `[MenuItem]`,而 batchmode **只执行第一条**
//        `-executeMethod`(`TD-ci-02` 的六格实测)⇒ 想「一次烘三张」写成三条命令,
//        实际只跑第一条,**退出码仍 0**(假绿);
//     ② **没有退出码语义** —— 各 `Bake*` 方法校验失败时 `throw`,batch 下进程码由 Unity 决定,
//        与「校验通过」不可区分;
//     ③ **没有任何脚本 / CI 真的去跑** —— `grep -rn "BakeDelayTable" .github/ tools/` ⇒ 零命中。
//
// ⇒ 本类 + `tools/bake/run_ci.sh` 一起补这三层。**不新建第二套校验**:三张表的校验体
//   全在各自的 `BakeFromRepo`(binder)里,本类只负责「按序跑 + 归口退出码 + 打印结果」。
//
// ═══════════════════════════════════════════════════════════════════════════
// 退出码(**三态**,与 `LevelCiRunner` 逐字同约定)
// ═══════════════════════════════════════════════════════════════════════════
//   0 = 三张表全部烘焙成功
//   1 = **有硬失败**(某张表的 binder 拒收 / 写盘失败)—— 与「代码有缺陷」同侧
//   2 = **烘焙链本身跑不起来**(仓库根解析失败 / 未捕获异常)—— 与 1 **刻意分开**:
//       「数据不合规」与「工具坏了」的处置完全不同。**本仓已踩过「门没跑到」被读成
//       「门跑了且通过」的反向版本**(`TD-medcons-17` / `CLAUDE.md` §构建前门)。
//
// ⚠️ **三张表之外还有一个**输入**:`CostTable53Bake` 除读 `53_cost_table.json`,还要读
//   `disease_registry.json` 做**跨表断言**(「永不致死」:表里的 `injury_id` 须落在注册表的
//   伤情词表里)与**外键解析**(作者态键 → 9 的整数 id)。⇒ 本入口的依赖面是**四份源**,
//   不是三份 —— 名字里的「三表」指**产物**,不指输入。
//   (2026-10-30 由夹具首轮实测暴露:沙箱只拷三张表 ⇒ 第三张红、退出码 1。)
//
// ⚠️ **失败即止 + 全成功才落盘**(2026-10-30 证伪轮实测订正):三张表的 `ConfigVersion` 是
//   **各自**的内容哈希(ADR-014 §五),但存档头只记一个 ⇒ 半套新半套旧会让「存档头对得上、
//   某张表是旧的」这一形态**静默成立**。
//   初版是「边烘边写 + 失败时**打印**中间态」—— 证伪 M1 实测:第二张失败时第一张**已被覆盖**,
//   磁盘真处在混合态,而代码只打印、不阻止。**打印不是阻止。**
//   现改为:三张表**全部**烘成内存字节后才统一落盘(先写 `.tmp` 再 `File.Move` 覆盖)⇒
//   任一张失败时**磁盘一个字节都没动**。

using System;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using UnityEditor;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>53 三张表的批量烘焙 + CI 退出码(见文件头)。</summary>
    public static class MedConsTableBakeRunner
    {
        /// <summary>全部成功。</summary>
        public const int ExitGreen = 0;

        /// <summary>有硬失败(某张表被 binder 拒收 / 写盘失败)。</summary>
        public const int ExitViolation = 1;

        /// <summary>烘焙链本身跑不起来(仓库根解析失败 / 未捕获异常)。</summary>
        public const int ExitFatal = 2;

        /// <summary>三张表的产物名(与 `DataBakeMenu` 的常量**同一出处** —— 不复制字面量)。</summary>
        public static readonly string[] TableNames =
        {
            DataBakeMenu.OutcomeTableCookedAssetName,
            DataBakeMenu.DelayTableCookedAssetName,
            DataBakeMenu.CostTableCookedAssetName,
        };

        /// <summary>
        /// **CLI 入口**:`-executeMethod DaYiJingCheng.EditorTools.Bake.MedConsTableBakeRunner.RunBatch`。
        /// <para>⚠️ 调用方**必须**带 `-quit`(与 `-runTests` 相反 —— 见 `tools/bake/run_ci.sh`)。</para>
        /// </summary>
        public static void RunBatch()
        {
            int code = Run(out string report);
            Console.WriteLine(report);
            Debug.Log(report);
#if UNITY_EDITOR
            EditorApplication.Exit(code);
#else
            Environment.Exit(code);
#endif
        }

        /// <summary>烘焙三张表并归口退出码(生产路径:`Application.dataPath` 推导仓根与产物目录)。</summary>
        /// <param name="report">可读报告(逐表结果 + 中间态说明)。</param>
        /// <returns>0 / 1 / 2(见文件头)。</returns>
        public static int Run(out string report)
        {
            string repoRoot;
            try
            {
                repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            }
            catch (Exception ex)
            {
                report = $"[53 表烘焙 CI] 仓库根解析失败(退出码 2):{ex.GetType().Name}: {ex.Message}";
                return ExitFatal;
            }

            return Run(repoRoot, Path.Combine(Application.dataPath, DataBakeMenu.CookedDirName),
                       out report);
        }

        /// <summary>
        /// 可注入路径的烘焙(夹具走这一条 —— **全程在临时目录里**,不碰仓库源与产物)。
        /// </summary>
        /// <param name="repoRoot">仓根(须含 `assets/data/53_*.json`)。</param>
        /// <param name="cookedDir">产物落盘目录(不存在则创建)。</param>
        /// <param name="report">可读报告。</param>
        /// <returns>0 / 1 / 2。</returns>
        /// <remarks>
        /// ⚠️ **为什么把路径提成形参**:夹具若走生产路径,就必须**改写仓库源文件与已入库产物**
        /// —— 违反 `.claude/rules/test-standards.md`「Unit tests must not depend on external
        /// state (filesystem)」,且中途崩溃会把仓库留在脏状态。
        /// 注入路径后,夹具在 `Path.GetTempPath()` 下自建仓根副本。
        /// </remarks>
        public static int Run(string repoRoot, string cookedDir, out string report)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            if (cookedDir == null) throw new ArgumentNullException(nameof(cookedDir));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[53 表烘焙 CI] outcome_table · delay_table · cost_table");

            // ── ① 先全部烘进内存(此阶段**零磁盘写入**)──
            var baked = new byte[TableNames.Length][];
            for (int i = 0; i < TableNames.Length; i++)
            {
                try
                {
                    baked[i] = BakeOne(i, repoRoot);
                    sb.AppendLine($"  ✓ {TableNames[i]} = {baked[i].Length} B");
                }
                catch (BakeValidationException ex)
                {
                    // 校验失败 = **数据不合规**(退出码 1),不是工具坏。
                    foreach (string e in ex.Errors) sb.AppendLine($"  ✗ {e}");
                    sb.AppendLine($"  ✗ 烘焙中止于 {TableNames[i]}(退出码 1)");
                    sb.AppendLine("  磁盘中间态:**一个字节都没动**(本入口全成功才落盘)");
                    report = sb.ToString();
                    return ExitViolation;
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  ✗ 烘焙中止于 {TableNames[i]}(退出码 1):" +
                                  $"{ex.GetType().Name}: {ex.Message}");
                    sb.AppendLine("  磁盘中间态:**一个字节都没动**(本入口全成功才落盘)");
                    report = sb.ToString();
                    return ExitViolation;
                }
            }

            // ── ② 全部成功 ⇒ 才落盘(先 .tmp 再 Move,防「写一半崩」)──
            try
            {
                WriteAllAtomically(cookedDir, baked);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ✗ 落盘失败(退出码 1):{ex.GetType().Name}: {ex.Message}");
                sb.AppendLine("  磁盘中间态:可能已写一部分 `.tmp` —— 见上条报文;旧产物未被覆盖");
                report = sb.ToString();
                return ExitViolation;
            }

            // ⚠️ 只有**生产路径**(产物落在 `Assets/` 下)才需要刷新资产库;
            //    夹具走临时目录时调它只会白跑一次全库导入。
            if (cookedDir.StartsWith(Application.dataPath, StringComparison.Ordinal))
                AssetDatabase.Refresh();
            sb.AppendLine($"  三张表全部成功(退出码 0)—— 产物在 {cookedDir}");
            sb.AppendLine("  ⚠️ 产物入组与 catalog 重建**不在本入口**:那是 " +
                          "DataBakeMenu.BuildAddressablesContent(它自己会先 Ensure)");
            report = sb.ToString();
            return ExitGreen;
        }

        /// <summary>按序号烘一张表(顺序即 <see cref="TableNames"/>)。</summary>
        /// <remarks>刻意用 `switch` 而不是反射:`switch` 的**漏分支会被编译器拒绝**
        /// (所有路径须返回值),反射漏分支只会运行期抛。</remarks>
        private static byte[] BakeOne(int index, string repoRoot)
        {
            switch (index)
            {
                case 0: return OutcomeTable53.BakeFromRepo(repoRoot).Cooked;
                case 1: return DelayTable53Bake.BakeFromRepo(repoRoot).Cooked;
                case 2: return CostTable53Bake.BakeFromRepo(repoRoot).Cooked;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index), index,
                        $"表序号 {index} 越界(共 {TableNames.Length} 张)—— " +
                        "新增表须同批扩 TableNames 与本 switch(漏一处 = 该表永不烘焙)");
            }
        }

        /// <summary>
        /// 把三份字节**原子**落盘:先写同目录 `.tmp`,再逐个 `File.Move` 覆盖。
        /// </summary>
        /// <remarks>
        /// <b>为什么必须原子</b>:三张表的 `ConfigVersion` 各自独立,而存档头只记一个
        /// ⇒ 半套新半套旧会让「存档头对得上、某张表是旧的」静默成立。两步式(先全烘进内存、
        /// 再统一落盘)已消除「烘到一半失败」;本方法再消除「**写**到一半失败」。
        /// ⚠️ `File.Move` 在同一卷上是原子的(覆盖需先删目标或由框架处理);
        /// 三步仍可能在中途失败 —— 但那时**旧产物已被替换一部分**,
        /// 这与「数据不合规」是不同侧的风险,如实登记在报文里。
        /// </remarks>
        private static void WriteAllAtomically(string cookedDir, byte[][] baked)
        {
            Directory.CreateDirectory(cookedDir);

            var temps = new string[TableNames.Length];
            for (int i = 0; i < TableNames.Length; i++)
            {
                temps[i] = Path.Combine(cookedDir, TableNames[i] + ".tmp");
                File.WriteAllBytes(temps[i], baked[i]);
            }

            for (int i = 0; i < TableNames.Length; i++)
            {
                string finalPath = Path.Combine(cookedDir, TableNames[i]);
                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(temps[i], finalPath);
            }
        }
    }
}
