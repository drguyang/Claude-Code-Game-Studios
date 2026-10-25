// AC-7a-06 —— 崩溃注入 harness(OS 级 kill -9 + 屏障)。
//
// 权威来源:
//   GDD `persistence-service.md:496-505` —— AC-7a-06 原文
//   GDD `:500-505` 括注 —— 「崩溃注入**非 UTF 内可做** —— 须 **OS 级 kill**
//     (POSIX `kill -9` / Win `TerminateProcess`)的**子进程 + 屏障钩子 harness**
//     (测试进程开子进程跑 save,屏障点让子进程自曝位置后 `kill`)」
//   ADR-010 §四 —— 原子写 + 双档
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么不放 Unity 的 EditMode 测试里
// ─────────────────────────────────────────────────────────────────────────────
// 三条独立理由:
//   ① **GDD 自己写明了**「非 UTF 内可做」—— 这不是我的判断,是判据的原文;
//   ② 崩溃注入要**反复起停子进程**,而 Unity batch 需**独占工程**(`UnityLockfile`),
//      把 N 次进程起停塞进一次 batch 会把回归时间拉长到不可接受;
//   ③ `Sim.Contracts` + `Sim.Codec` 实测**纯 BCL**(零 `using UnityEngine`、零 `#if UNITY`,
//      asmdef `noEngineReferences: true`)⇒ 可直接以 net8.0 编译,无需 Unity 运行时。
//      2026-10-25 实测:同一份源文件,dotnet 8 编译 0 错 0 警告。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 屏障是**观测出来的**,不是在 SaveService 里插钩子
// ─────────────────────────────────────────────────────────────────────────────
// 三条候选:
//   ① 在 `SaveService.WriteAtomic` 里插 `if (env == "barrier") { signal; block; }`
//      —— **污染生产代码**。一个只被测试用的分支留在存档写入的关键路径上,
//      而它恰好是「玩家零打断」最要紧的那段。**否决**。
//   ② 用 `Debugger` / 断点 —— 需要附加调试器,CI 里不可用。**否决**。
//   ③ **父进程轮询文件系统状态**(本方案):`save_0.tmp` 出现 = tmp 已写窗口;
//      `save_0.bin` 大小变成新值 = rename 已完成。
//      —— 不碰生产代码一行,而且是**真实竞态**(父进程与子进程真的在抢时间),
//         比插桩更接近「断电 / 强杀」的实际形态。
//
// ⚠️ ③ 的代价:**窗口可能太窄而抓不到**。缓解 = 把存档体放大到 8 MiB
//    (`File.WriteAllBytes` + `Flush(true)` 在 SSD 上是数十 ms 量级),
//    使 1 ms 粒度的轮询可靠命中。**抓不到时如实记为「未命中该屏障」**,
//    不冒充「通过了该屏障的测试」。
//
// ─────────────────────────────────────────────────────────────────────────────
// 断言什么(AC-7a-06 逐句)
// ─────────────────────────────────────────────────────────────────────────────
//   ① 「重启后 `final` 或 `bak` 至少一份**通过校验和**」
//      ⇒ `AssertAtLeastOneValid()`
//   ② 「断言 `final` 哈希 ∈ {上一好档哈希, 新档哈希}(无『半写损坏的 final』,无第三哈希)」
//      ⇒ `AssertNoThirdHash()` —— 存在的每一份都必须 ∈ {A, B}
//      ⚠️ **final 不存在**时第 ② 条对 final 不适用(它没有哈希)—— 那属于
//         「`WriteAtomic` 先删 final 再 rename」的固有窗口,harness **如实统计**
//         该形态出现的次数,不掩盖。
//   ③ 「残留 `tmp` 被清理」
//      ⇒ `AssertTmpCleanedAfterNextSave()` —— 重启后做一次成功保存,tmp 须消失
//         (被 kill 当场 tmp 必然残留:没人能在进程已死时清理它;
//          GDD 的「被清理」指的是**恢复之后**,故判据设在恢复+一次成功写之后)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tools.SaveCrash
{
    /// <summary>AC-7a-06 崩溃注入 harness 的入口(父 / 子双模式)。</summary>
    public static class Program
    {
        /// <summary>存档体大小 —— 放大以拓宽可命中的写入窗口(见文件头)。</summary>
        private const int PayloadBytes = 8 * 1024 * 1024;

        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("用法: SaveCrashHarness <parent|child> ...");
                return 2;
            }

            switch (args[0])
            {
                case "child": return ChildMain(args);
                case "parent": return ParentMain(args);
                default:
                    Console.Error.WriteLine($"未知模式:{args[0]}");
                    return 2;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 子进程:写一次存档,然后**永久阻塞**等父进程来杀
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 子进程入口。
        /// <para>参数:<c>child &lt;basePath&gt; &lt;fillByte&gt;</c></para>
        /// <para>行为:组版一份 <see cref="PayloadBytes"/> 的存档 → `Checkpoint` 入队 →
        /// **主线程永久阻塞**(worker 在后台线程真写盘,父进程在此时机 kill -9)。</para>
        /// </summary>
        private static int ChildMain(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("child 需要 <basePath> <fillByte>");
                return 2;
            }

            string basePath = args[1];
            byte fill = byte.Parse(args[2]);

            var svc = new SaveService(
                basePath,
                new SaveCodec(),
                narrativeContext: () => true,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => BuildPayload(fill),
                // ⚠️ **生产形态**:真后台线程。这是本 harness 的意义所在 ——
                //    崩溃必须发生在**写盘进行中**,而不是组版阶段。
                writeScheduler: action => System.Threading.Tasks.Task.Run(action));

            // 通知父进程「worker 已就绪,随时可能开始写」
            Console.Out.WriteLine("ENQUEUED");
            Console.Out.Flush();

            // ⚠️ **循环写,不是写一次**。
            //
            // 缘由(2026-10-25 实测):8 MiB 的 `File.WriteAllBytes` + `Flush(true)` 在
            // 本机只占 **10 ms**,而父进程以 `SpinWait` + `FileInfo` 轮询的观测粒度
            // 远粗于此 ⇒ **抓不到中间态**。首版 harness 因此对「非原子写」这个
            // 明显错误的实现**仍然全绿**(反证失败)—— 判据没有判别力。
            //
            // 改法:让子进程**反复**写(每轮换填充字节 ⇒ 每份内容不同),
            // 父进程在随机时刻 kill。这样「写进行中」这个状态在时间轴上反复出现,
            // 命中概率随轮次上升,而不是赌一次 10 ms 的窗口。
            //
            // ⚠️ **对同一个槽反复覆盖**,不是每个槽写一次。
            //
            // 缘由(2026-10-25 第二版实测):每槽只写一次 ⇒ 那是**首次写**,
            // `WriteAtomic` 不会建 bak(没有上一好档)。此时崩溃 ⇒ 盘上零有效档,
            // harness 判 FAIL —— 但那是**合理的**:本来就没有数据可以保留。
            // AC-7a-06 的语义前提是「**有一份已有的好档要被覆盖**」,崩溃不得让它丢。
            //
            // ⇒ 必须先建立 final + bak,再触发崩溃。用 `SaveBytes`(本 harness 与
            //   `Sim.Codec` 是**同一程序集** —— 直接编源文件,故 `internal` 可见)
            //   绕过 `Checkpoint` 的「槽位只进不退」门,对同一槽反复写。
            // ⚠️ **载荷只建一次**(2026-10-25 第三版)。
            //
            // 缘由:首版每轮都 `BuildPayload` + `AddChecksum` —— 8 MiB 的分配与哈希
            // 各约 10–30 ms,而写盘本身只占 ~10 ms ⇒ **循环里大部分时间在组版,不在写盘**。
            // 实测后果:父进程随机时刻 kill,20 轮里只有 **1 轮**真的落在写盘窗口
            // (判别力 5%)。组版开销把写盘窗口稀释掉了。
            //
            // 改法:预先建好一份签好名的载荷,**循环只做写盘** ⇒ 写盘占循环时间的绝大部分,
            // kill 落在写盘窗口的概率随之升到接近 1。
            byte[] signed = SaveService.AddChecksum(BuildPayload((byte)0xBB));

            // ⚠️ **写盘标记文件**(2026-10-25 第三版)—— 这是「命中」判据的**可证化**。
            //
            // 首版的 `tmp-written` 屏障只是 `Thread.Sleep(2..8)` 后 `return true` ——
            // 它**没有观测任何东西**,「命中=是」是自造的(禁借绿:自造断言顶替 AC 语义层)。
            //
            // 现判据:每轮写盘**前后**各落一个标记:
            //   · 写前 → 建 `WRITING` 标记
            //   · 写后 → 删 `WRITING` 标记
            // 父进程见到 `WRITING` 存在即 kill;杀后标记**仍在盘上** ⇒ **证明它死在写盘区内**。
            // 标记消失 ⇒ 死在两轮之间 ⇒ 本轮 `Hit = false`,不计入判据面(如实登记)。
            string writingMark = Path.Combine(basePath, "WRITING");
            for (int i = 0; i < 20000; i++)
            {
                File.WriteAllBytes(writingMark, new byte[] { 1 });
                svc.SaveBytes(new SaveSlot(0), signed);
                File.Delete(writingMark);
            }

            Thread.Sleep(Timeout.Infinite);
            return 0;
        }

        /// <summary>
        /// 构造一份**不含校验和**的存档体(形如 `SaveService.AddChecksum` 的入参)。
        /// <para>头部 36 字节 + 填充到 <see cref="PayloadBytes"/>。填充字节由
        /// <paramref name="fill"/> 决定 ⇒ 两次写的**内容哈希必然不同**(用于区分「上一好档」与「新档」)。</para>
        /// </summary>
        private static byte[] BuildPayload(byte fill)
        {
            var b = new byte[PayloadBytes];
            b[0] = 0x44; b[1] = 0x59; b[2] = 0x4A; b[3] = 0x51;   // "DYJQ" magic
            for (int i = 36; i < b.Length; i++) b[i] = fill;
            return b;
        }

        // ══════════════════════════════════════════════════════════════════
        // 父进程:起子进程 → 在屏障处 kill -9 → 重启校验
        // ══════════════════════════════════════════════════════════════════

        private sealed class TrialResult
        {
            public string Barrier;
            public bool Hit;              // 屏障是否真的命中(抓不到窗口 = 未命中)
            public bool FinalExists;
            public bool FinalValid;
            public bool BakExists;
            public bool BakValid;
            public bool FinalIsOldOrNew;  // final 哈希 ∈ {A, B}(final 不存在时 N/A)
            public string Observed;       // 人类可读的现场描述
            public bool TmpCleanedAfterRecovery;
        }

        private static int ParentMain(string[] args)
        {
            int trials = args.Length > 1 ? int.Parse(args[1]) : 6;
            string root = Path.Combine(Path.GetTempPath(), "dyjq_savecrash_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            Console.WriteLine($"[AC-7a-06] 崩溃注入 harness");
            Console.WriteLine($"  工作目录 : {root}");
            Console.WriteLine($"  存档体    : {PayloadBytes / (1024 * 1024)} MiB");
            Console.WriteLine($"  试验轮数  : {trials}");
            Console.WriteLine();

            var results = new List<TrialResult>();
            int failures = 0;

            try
            {
                for (int i = 0; i < trials; i++)
                {
                    string dir = Path.Combine(root, "t" + i);
                    Directory.CreateDirectory(dir);

                    // ── 前置:写一份「上一好档」(内容 = 0xAA)──
                    byte[] oldHash = WriteBaseline(dir, fill: 0xAA);
                    if (oldHash == null)
                    {
                        Console.Error.WriteLine($"[t{i}] 前置写失败 —— 跳过本轮");
                        failures++;
                        continue;
                    }

                    // 屏障轮换:0 = tmp 窗口;1 = rename 后;2+ = 随机时刻
                    string barrier = i == 0 ? "tmp-written" : (i == 1 ? "post-rename" : "random");
                    TrialResult r = RunTrial(dir, barrier, oldHash, i);
                    results.Add(r);

                    // ⚠️ **未命中轮次不构成证据**(2026-10-25 第三版)—— 既不计 PASS 也不计 FAIL。
                    //    首版把未命中轮次也计入 `failures`,同时又宣称「未命中的轮次不计入通过」
                    //    ⇒ 分子分母口径不一致,读表的人无法判断到底验证了什么。
                    string mark;
                    if (!r.Hit) mark = "SKIP";
                    else if ((r.FinalValid || r.BakValid) && r.FinalIsOldOrNew) mark = "PASS";
                    else mark = "FAIL";
                    if (mark == "FAIL") failures++;
                    Console.WriteLine(
                        $"[t{i}] {mark} 屏障={barrier,-12} 命中={(r.Hit ? "是" : "否")}  " +
                        $"final={(r.FinalExists ? (r.FinalValid ? "有效" : "损坏") : "缺失")}  " +
                        $"bak={(r.BakExists ? (r.BakValid ? "有效" : "损坏") : "缺失")}  " +
                        $"final∈{{A,B}}={(r.FinalIsOldOrNew ? "是" : "否")}  " +
                        $"tmp恢复后清理={(r.TmpCleanedAfterRecovery ? "是" : "否")}");
                    if (!string.IsNullOrEmpty(r.Observed))
                        Console.WriteLine($"       现场:{r.Observed}");
                }
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { /* 清理失败不影响判决 */ }
            }

            Console.WriteLine();
            Console.WriteLine("── 汇总 ──");
            int hit = results.Count(r => r.Hit);
            var hits = results.Where(r => r.Hit).ToList();
            Console.WriteLine($"  屏障命中        : {hit}/{results.Count}" +
                              (hit < results.Count ? "  ⚠️ 未命中轮次计 SKIP,**不计入**任何判据分子/分母" : ""));
            // ⚠️ 分母 = **命中轮次数**,不是总轮次数(2026-10-25 第三版口径订正)。
            //    未命中的轮次没有验证任何东西,把它算进分母会让「至少一份有效 7/8」
            //    看起来像 1 次失败,实际是 1 次**未验证** + 7 次通过。
            Console.WriteLine($"  至少一份有效    : {hits.Count(r => r.FinalValid || r.BakValid)}/{hit}");
            Console.WriteLine($"  无第三哈希      : {hits.Count(r => r.FinalIsOldOrNew)}/{hit}");
            Console.WriteLine($"  恢复后 tmp 清理 : {hits.Count(r => r.TmpCleanedAfterRecovery)}/{hit}");
            Console.WriteLine($"  失败轮次        : {failures}");

            if (failures > 0)
            {
                Console.WriteLine();
                Console.WriteLine("❌ AC-7a-06 FAIL");
                return 1;
            }

            if (hit == 0)
            {
                // ⚠️ 全部未命中屏障 ⇒ **不得**判通过(禁借绿:那等于「什么都没测」)。
                Console.WriteLine();
                Console.WriteLine("⚠️ 无一轮命中屏障 —— 本 harness 未验证任何断点,判 **NOT-RUN**(不判通过)。");
                return 3;
            }

            // ⚠️ 命中率过低也须如实登记(2026-10-25 第三版)。
            //    首版 8/8「命中」是自造的,20 轮里真落在写盘窗口只有 1 次。
            //    现判据可证,但仍须报出命中率 —— 若远低于 100%,说明写盘窗口仍被稀释,
            //    判别力存疑,不得凭少量命中轮次宣称覆盖充分。
            if (hit * 2 < results.Count)
            {
                Console.WriteLine();
                Console.WriteLine($"⚠️ 命中率仅 {hit}/{results.Count} —— 多数轮次未落在写盘窗口。" +
                                  "已验证的轮次全部通过,但**样本偏少**,判别力存疑。");
            }

            Console.WriteLine();
            Console.WriteLine("✅ AC-7a-06 PASS(命中屏障的轮次全部满足三条断言)");
            return 0;
        }

        /// <summary>
        /// 写一份基线存档(内容固定),返回其**完整文件**的 SHA256(即「上一好档哈希 A」)。
        /// </summary>
        private static byte[] WriteBaseline(string dir, byte fill)
        {
            try
            {
                var svc = new SaveService(
                    dir,
                    new SaveCodec(),
                    narrativeContext: () => true,
                    replaySink: _ => { },
                    foldedRowSink: _ => { },
                    compose: _ => BuildPayload(fill),
                    writeScheduler: action => action());   // 基线用同步,确保写完

                svc.Checkpoint(new SaveSlot(0));
                string final = Path.Combine(dir, "save_0.bin");
                if (!File.Exists(final)) return null;
                return Sha256File(final);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("  基线写异常:" + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 一轮试验:起子进程 → 在屏障处 kill -9 → 重启校验。
        /// </summary>
        private static TrialResult RunTrial(string dir, string barrier, byte[] oldHash, int seed)
        {
            var r = new TrialResult { Barrier = barrier };

            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string self = System.Reflection.Assembly.GetEntryAssembly().Location;

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            // dotnet 运行 dll:exe 是 dotnet,第一个参数是 dll 路径。
            // 框架依赖发布下 MainModule 就是 dotnet ⇒ 须显式带上自身 dll。
            if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                psi.ArgumentList.Add(self);
            psi.ArgumentList.Add("child");
            psi.ArgumentList.Add(dir);
            psi.ArgumentList.Add("187");   // 0xBB —— 与基线 0xAA 不同 ⇒ 新档哈希必异

            using (var child = Process.Start(psi))
            {
                // 等子进程把工作项入队(它打印 ENQUEUED 后阻塞)
                string line = child.StandardOutput.ReadLine();
                bool enqueued = line != null && line.Contains("ENQUEUED");

                // ⚠️ 「命中」= **崩溃发生在写盘进行中**,判据见 WaitForBarrier。
                bool sawWriting = enqueued && WaitForBarrier(dir, barrier, seed);

                // ── OS 级强杀(AC-7a-06 逐字要求,不是 graceful shutdown)──
                try { child.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* 已自行退出 */ }
                child.WaitForExit(5000);

                // ⚠️ **kill 后复核标记仍在**(2026-10-25 第三版)—— 这才是证明。
                //    进程已死,没人能删这个标记 ⇒ 它仍在 = 崩溃确实发生在写盘区内。
                //    若已消失 ⇒ 子进程恰好写完并删了标记 ⇒ 本轮**未命中**,如实登记。
                string markAfterKill = Path.Combine(dir, "WRITING");
                r.Hit = sawWriting && File.Exists(markAfterKill);
                if (sawWriting && !r.Hit)
                    r.Observed = "kill 后 WRITING 标记已消失 —— 恰在两轮之间,未命中写盘窗口";
                // 清掉标记,免得干扰后续断言面(它不参与 AC 判据)
                try { File.Delete(markAfterKill); } catch { /* 无妨 */ }
            }

            // ── 重启校验(全新进程状态:新 SaveService,台账复位)──
            //
            // ⚠️ **判据面 = 崩溃时正在写的那一个槽位**,不是固定的 `save_0`。
            //    子进程循环写 slot 1..2000(见 ChildMain),崩溃可能落在任意一个上。
            //    ⇒ 找出**盘上最新的那个槽**(mtime 最大),对它做 AC-7a-06 的三条断言。
            //    这是「崩溃发生在写盘进行中」的自然落点 —— 固定槽位反而会绕开真窗口。
            // ⚠️ **判据槽的选择(第三版,前两版都错)**:
            //    ① 按 mtime 取 → 子进程几十 ms 内连写数百份,文件系统 mtime 精度不足
            //       ⇒ 取到任意槽,「该槽无 bak」被误报成「bak 缺失」;
            //    ② 按槽号最大取 → 崩溃可能落在**中间**某槽,更大的槽号根本没写到
            //       ⇒ 取到的槽根本没被碰过,同样误报。
            //    ③ 现判据 = **优先取「有 bak 但无 final」的槽** —— 那正是
            //       `WriteAtomic` 里「final → bak 已挪、tmp → final 未完成」的崩溃窗口,
            //       也是 AC-7a-06 最想覆盖的那一处。找不到则退回「槽号最大」。
            //    ⚠️ 子进程现已固定写 slot 0(见 ChildMain),故判据面就是 `save_0.*`。
            string crashSlot = Path.Combine(dir, "save_0");
            string latestFinal = crashSlot + ".bin";
            string latestBak = crashSlot + ".bak";
            string latestTmp = crashSlot + ".tmp";
            if (!File.Exists(latestFinal)) latestFinal = null;
            if (!File.Exists(latestBak)) latestBak = null;
            if (!File.Exists(latestTmp)) latestTmp = null;

            r.FinalExists = latestFinal != null;
            r.BakExists = latestBak != null;
            r.FinalValid = r.FinalExists && SaveService.ValidateChecksum(File.ReadAllBytes(latestFinal));
            r.BakValid = r.BakExists && SaveService.ValidateChecksum(File.ReadAllBytes(latestBak));

            if (r.FinalExists)
            {
                // 第 ② 条:final 哈希须 ∈ {上一好档, 新档}。
                // 「上一好档」= 该槽的 bak(若存在);「新档」= 崩溃前最后一次写的期望内容。
                // ⚠️ 用**该槽自己的 bak** 比对,而不是全局基线 —— 循环写下基线只对 slot 0 有效。
                byte[] fh = Sha256File(latestFinal);
                bool isBakOfSameSlot = r.BakExists && BytesEqual(fh, Sha256File(latestBak));
                bool isAnyExpectedNew = IsExpectedPayload(fh);
                r.FinalIsOldOrNew = isBakOfSameSlot || isAnyExpectedNew;

                if (!r.FinalIsOldOrNew)
                    r.Observed = $"final 哈希既非该槽 bak、也非任何期望内容 —— 第三哈希(半写)";
            }
            else
            {
                // final 不存在 ⇒ 第 ② 条对它不适用(它没有哈希)。
                // ⚠️ 这不是「通过」,是「不适用」—— 统计里单独可见。
                r.FinalIsOldOrNew = true;
                r.Observed = "final 缺失(`WriteAtomic` 先删 final 再 rename 的固有窗口)";
            }

            if (r.FinalExists && !r.FinalValid && !r.BakValid)
                r.Observed = "final 与 bak 双双校验失败 —— 半写损坏";

            // tmp 残留:崩溃当场必然留(没人能在进程已死时清理)。判据设在恢复之后。
            string tmpPath = latestTmp;

            // ── 恢复:对**同一个槽**再做一次成功保存,然后看 tmp 是否被清理 ──
            //
            // ⚠️ GDD `:503` 逐字「残留 `tmp` 被清理」—— 崩溃**当场**必然留残留
            //    (进程已死,没人能清理)。所以判据只能设在「恢复之后」:
            //    下一次成功的 `WriteAtomic` 走 `tmp → rename` 时会把同名 tmp 覆盖/移走。
            try
            {
                var svc = new SaveService(
                    dir,
                    new SaveCodec(),
                    narrativeContext: () => true,
                    replaySink: _ => { },
                    foldedRowSink: _ => { },
                    compose: _ => BuildPayload(0xCC),
                    writeScheduler: action => action());
                // 对崩溃时那个槽再写一次成功档 ⇒ 走 `tmp → rename`,残留 tmp 被移走
                svc.SaveBytes(new SaveSlot(0), SaveService.AddChecksum(BuildPayload(0xCC)));
                r.TmpCleanedAfterRecovery = tmpPath == null || !File.Exists(tmpPath);
            }
            catch (Exception e)
            {
                r.Observed = (r.Observed ?? "") + " | 恢复写异常:" + e.Message;
                r.TmpCleanedAfterRecovery = false;
            }

            return r;
        }

        /// <summary>从 <c>save_12.bin</c> / <c>save_12.tmp</c> 形态解析出槽位号;失败返回 0。</summary>
        private static uint ParseSlotOf(string path)
        {
            if (path == null) return 0;
            string name = Path.GetFileName(path);          // save_12.bin
            int us = name.IndexOf('_');
            int dot = name.LastIndexOf('.');
            if (us < 0 || dot <= us + 1) return 0;
            return uint.TryParse(name.Substring(us + 1, dot - us - 1), out uint v) ? v : 0;
        }

        /// <summary>
        /// 等屏障命中。返回 <c>false</c> = 窗口太窄没抓到(**如实登记,不冒充通过**)。
        /// </summary>
        private static bool WaitForBarrier(string dir, string barrier, int seed)
        {
            string finalPath = Path.Combine(dir, "save_0.bin");
            string writingMark = Path.Combine(dir, "WRITING");
            var sw = Stopwatch.StartNew();
            var rnd = new Random(seed);

            // ⚠️ **判据口径(2026-10-25 第三版 —— 前两版都是自造的)**:
            //
            //    第一版:「观测到 tmp 文件」/「观测到 final 大小变化」。
            //      反证实测:8 MiB 写盘只占 ~10 ms,`FileInfo` 轮询粒度远粗于此
            //      ⇒ 抓不到中间态 ⇒ 非原子写仍然全绿(零判别力)。
            //
            //    第二版:「命中 = 崩溃时刻确有写盘正在进行」,但实现只是
            //      `Thread.Sleep(2..8)` 后 `return true` —— **它没有观测任何东西**。
            //      「命中=是」是自造的。这是本项目抓过的典型假绿形态:
            //      **自造断言顶替 AC 语义层**。20 轮里只有 1 轮真落在写盘窗口
            //      (判别力 5%),而报告却写「屏障命中 8/8」—— 那 8 个「命中」全是假的。
            //
            //    现版:**用子进程落的标记文件做真实观测**。
            //      子进程每轮写盘前后建/删 `WRITING`(见 ChildMain)。
            //      父进程等到 `WRITING` **存在**才 kill ⇒ 崩溃必然落在写盘区内。
            //      杀后**复核标记仍在**(进程已死,没人能删它)⇒ 这才构成证明。
            //      ⚠️ 若标记在 kill 后消失 ⇒ 本轮 `Hit = false`,如实登记,不计入判据面。

            // 先等子进程进入写盘循环(标记首次出现),给一个上限防挂死
            bool sawWriting = false;
            while (sw.ElapsedMilliseconds < 5000)
            {
                if (File.Exists(writingMark)) { sawWriting = true; break; }
                Thread.SpinWait(20);
            }
            if (!sawWriting) return false;   // 子进程没进入写盘循环 ⇒ 未命中

            switch (barrier)
            {
                case "tmp-written":
                    // 早窗口:见到标记**立即**动手(此时正处于某次写的起始附近)
                    return File.Exists(writingMark);

                case "post-rename":
                    // 晚窗口:等 final 已出现过至少一次(说明至少完成过一份写),
                    // 再等下一次写盘开始 ⇒ 崩溃落在「有旧档可保护」的写盘中间
                    while (sw.ElapsedMilliseconds < 5000)
                    {
                        if (File.Exists(finalPath)) break;
                        Thread.SpinWait(20);
                    }
                    // 等下一轮写盘开始
                    var sw2 = Stopwatch.StartNew();
                    while (sw2.ElapsedMilliseconds < 5000)
                    {
                        if (File.Exists(writingMark)) return true;
                        Thread.SpinWait(20);
                    }
                    return false;

                default:
                    // 随机窗口:在当前这次写盘内随机挑一个时刻(标记存在即为写盘中)
                    Thread.Sleep(rnd.Next(0, 6));
                    return File.Exists(writingMark);
            }
        }

        // ── 工具 ──

        /// <summary>
        /// 找崩溃窗口所在的那个槽(返回不带后缀的基路径,如 <c>…/save_17</c>)。
        /// <para><b>优先</b>:有 <c>.bak</c> 但无 <c>.bin</c> 的槽 —— 正是
        /// `WriteAtomic` 里「final → bak 已挪、tmp → final 未完成」的崩溃窗口。</para>
        /// <para><b>退回</b>:槽号最大的槽(崩溃落在别处时的最佳猜测)。</para>
        /// </summary>
        private static string FindCrashSlot(string dir)
        {
            string bestGap = null;
            uint gapSlot = 0;
            string bestAny = null;
            uint anySlot = 0;

            foreach (string f in Directory.GetFiles(dir, "save_*"))
            {
                string name = Path.GetFileName(f);
                // ⚠️ 排除 `save_exit.bin` —— 它不参与本 harness 的判面
                if (name.StartsWith("save_exit", StringComparison.Ordinal)) continue;

                int dot = name.LastIndexOf('.');
                if (dot < 0) continue;
                string suffix = name.Substring(dot);
                if (suffix != ".bin" && suffix != ".bak" && suffix != ".tmp") continue;

                string basePath = f.Substring(0, f.Length - suffix.Length);
                uint slot = ParseSlotOf(f);

                if (slot > anySlot) { anySlot = slot; bestAny = basePath; }

                // 窗口形态:有 bak、无 final
                if (File.Exists(basePath + ".bak") && !File.Exists(basePath + ".bin")
                    && slot > gapSlot)
                {
                    gapSlot = slot; bestGap = basePath;
                }
            }

            return bestGap ?? bestAny;
        }

        /// <summary>
        /// 该哈希是否等于**任何一个**期望的新档内容(子进程按 slot 算填充字节)。
        /// <para>槽位范围与 <see cref="ChildMain"/> 的循环一致(1..2000)。</para>
        /// </summary>
        /// <summary>
        /// 判 <paramref name="fileHash"/> 是否等于某份**期望内容**的完整哈希。
        /// <para>⚠️ <b>2026-10-25 第三版重写</b>:首版循环 slot 1..2000 逐个
        /// <c>BuildPayload</c> + <c>AddChecksum</c> + SHA256 —— 每次调用要哈希
        /// **2000 × 8 MiB ≈ 16 GB**。它在 8 轮 × 每轮 1 次的调用量下勉强跑完,
        /// 但纯属浪费,且掩盖了「子进程已固定写 0xBB」这一事实。</para>
        /// <para>现口径:harness 只有三处产生 final 内容 —— 基线 <c>0xAA</c> /
        /// 子进程 <c>0xBB</c> / 恢复 <c>0xCC</c>。逐一比对即可。</para>
        /// </summary>
        private static bool IsExpectedPayload(byte[] fileHash)
        {
            foreach (byte f in new byte[] { 0xAA, 0xBB, 0xCC })
            {
                if (BytesEqual(fileHash, Sha256Of(SaveService.AddChecksum(BuildPayload(f)))))
                    return true;
            }
            return false;
        }

        private static byte[] Sha256Of(byte[] data)
        {
            using (var sha = SHA256.Create()) return sha.ComputeHash(data);
        }

        private static byte[] Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return sha.ComputeHash(fs);
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
