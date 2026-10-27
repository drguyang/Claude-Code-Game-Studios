// S20-4 · processing story-001 —— F-18.2 准入谓词 / 候选集 / 同源调用点
//
// 权威来源:
//   design/gdd/processing.md
//     · F-18.2(:437-456)两道门 · BL-9 补注①(:449)min 的对象是**被点名实例集**
//     · BL-9 补注②(:451)预答 = 呈现门 / 终裁 = 权威门,**同一份实现,禁两份**
//     · 规则三(:167)`min_quality` 是准入闸,**不是品级出口**(出口只有 21a F5)
//     · AC-18-05(:721)· AC-18-10(:723)· AC-18-18 · AC-18-20(:724)
//   ADR-011 Amendment B —— 判定输入进流、主机终裁;本地判定降级为预表现
//   ADR-016 §一 —— 谓词输入须 ∈ {事件流, 烘焙数据, 二者的纯函数}
//   ADR-025 §① —— `Sim` 引用集恰 = {BCL, Sim.Contracts}
//   story production/epics/processing/story-001-recipe-subset-and-admissible-predicate.md
//
// ─────────────────────────────────────────────────────────────────────────────
// AC 记分(禁把「本体跑通」读成「四条全绿」)
// ─────────────────────────────────────────────────────────────────────────────
//   ✅ AC-18-05 源码面符号扫描:`MinQuality` 在**全部运行时装配**内的使用点
//               ⊆ {`AdmissiblePredicate.cs`, 白名单 2 处}**零幅值调制**
//               (不读 Inputs/Outputs、`MinQuality` 不出现在乘除式)。每个扫描面配**注入式正控**。
//               ⚠️ 扫描面 = **由 asmdef 的 `includePlatforms` 派生**(空 ⇒ 全平台 ⇒ 进构建),
//                  不是手写目录清单 —— 新增运行时装配自动纳入。见 §A1 订正。
//   ✅ AC-18-10 iff 矩阵:**2/3 真 + 炉半边桩**。8 组合(门 × 品级 × 炉)逐组合**双向**断;
//               owner 反向;边界恰等;min 取**实例集**而非行;空集具名短路;per-player 独立;
//               批量路径与逐条一致。
//               ⚠️ **第三条件「单炉空闲」是 `ISingleFurnaceQuery` 的 Spy 桩**,从未触及真实现
//                  `FurnaceState.IsBusy`(签名 `(events, decoder, actorId, tick)`,与接口不同形)。
//                  且 `FurnaceState.cs:154-159` 自陈空闲区间开闭是**可改口径** ⇒ 真实现换口径时
//                  本矩阵**不会跟红**。**故本条不得被读成「AC-18-10 全绿」。**
//                  真装配归 Story 004;本条只钉「合取结构」与「谓词两门」。
//   ✅ AC-18-20 IL 面:两个调用点 `call` 的是**同一个 MethodInfo**;两个包装方法体内
//               **零比较指令**;`Judge` 体内**有**比较指令(正控 —— 证明扫描器看得见比较)。
//               ⚠️ **结构性证明,不是端到端证明**:`PreviewVerdict` / `TerminalVerdict` 当前
//                  **零生产调用点**(全仓仅本夹具引用)。本条证明的是「**若**调用则同源」;
//                  把两个包装从调用图里摘掉,本组夹具**仍全绿**。
//                  真接线:预答半边归 42(呈现)、终裁半边归 S20-8(processing-002)。
//   ⚠️ AC-18-18 **不在本文件记分** —— 真身在 `recipe_owner_partition_test.cs`(6 条),
//               本文件只做**存在性与归属**校验,防两份判据分叉。
//
// ─────────────────────────────────────────────────────────────────────────────
// 三条自我约束(为什么这样写)
// ─────────────────────────────────────────────────────────────────────────────
// ① **零字面量调参** —— 门槛 / 品级下限一律用夹具常量并标注「夹具值」,不抄真表数值。
// ② **判据不引用被测常量** —— 断言里不拿 `AdmissiblePredicate.CraftSkillId` 当期望值,
//    改用字面量 7 与 `(int)SkillId.炮制` **双向钉**;否则变异「改 CraftSkillId」时两边一起动
//    ⇒ 零红逃逸(自指判据形态,本项目已抓过)。
// ③ **扫描器必须自证有判别力** —— 每个「零命中」断言都配一条注入式正控;
//    没有正控的零命中与「扫描器什么都没扫到」逐位不可区分。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘(本文件只有 `File.ReadAllText`,不触 FsWriteBoundaryGate)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Processing;

namespace DaYiJingCheng.Tests.EditMode.Processing
{
    /// <summary>processing story-001 —— AC-18-05 / 10 / 20 的 EditMode 夹具。</summary>
    [TestFixture]
    internal sealed class AdmissiblePredicateTest
    {
        // ══════════════════════════════════════════════════════════════════
        // 夹具常量(全部为**夹具值** —— 不是真表数值,也不抄 GDD 示例)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>夹具值:技能门槛。</summary>
        private const int Gate = 5;

        /// <summary>夹具值:品级下限。</summary>
        private const int MinQ = 3;

        /// <summary>夹具值:玩家 id。</summary>
        private const int Actor = 42;

        /// <summary>夹具值:另一名玩家(用于「跨玩家互不影响」)。</summary>
        private const int OtherActor = 43;

        /// <summary>炮制技能 id 的**字面量**形 —— 与 <c>(int)SkillId.炮制</c> 双向钉;
        /// 刻意不引用 <c>AdmissiblePredicate.CraftSkillId</c>(自指判据禁令)。</summary>
        private const int CraftSkillLiteral = 7;

        // ══════════════════════════════════════════════════════════════════
        // 测试替身
        // ══════════════════════════════════════════════════════════════════

        /// <summary>等级查询替身 —— 记录调用次数与查询过的技能 id(供纯函数性 / 技能 id 断言)。</summary>
        private sealed class SpySkillLevels : ISkillLevelQuery
        {
            private readonly int _level;
            private readonly Dictionary<int, int> _perActor;

            public int CallCount { get; private set; }
            public List<int> QueriedSkillIds { get; } = new List<int>();

            public SpySkillLevels(int level) { _level = level; }

            public SpySkillLevels(Dictionary<int, int> perActor) { _perActor = perActor; }

            public int QueryLevel(int actorId, int skillId)
            {
                CallCount++;
                QueriedSkillIds.Add(skillId);
                if (_perActor != null)
                    return _perActor.TryGetValue(actorId, out int lv) ? lv : 0;
                return _level;
            }
        }

        /// <summary>单炉查询替身 —— 按 actor 分别设定忙 / 闲。</summary>
        private sealed class SpyFurnace : ISingleFurnaceQuery
        {
            private readonly HashSet<int> _busy;

            public int CallCount { get; private set; }

            public SpyFurnace(params int[] busyActors) => _busy = new HashSet<int>(busyActors);

            public bool IsBusy(int actorId)
            {
                CallCount++;
                return _busy.Contains(actorId);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具工厂
        // ══════════════════════════════════════════════════════════════════

        /// <summary>造一条配方(夹具值;owner / 门槛 / 品级下限可指定)。</summary>
        private static Recipe MakeRecipe(string id, RecipeOwner owner, int gate, int minQuality)
            => new Recipe
            {
                RecipeId = id,
                Owner = owner,
                SkillGate = gate,
                MinQuality = minQuality,
                DurationTicks = 10,
                Inputs = new[] { new RecipeEntry(new ItemKey("willow_bark", ProcessingState.Raw), 2) },
                Outputs = new[] { new RecipeEntry(new ItemKey("salicylic", ProcessingState.Extracted), 1) },
                BoundaryState = new[] { new ProcessingTransition(ProcessingState.Raw, ProcessingState.Extracted) },
            };

        private static Recipe DefaultProcessRecipe()
            => MakeRecipe("fx_process", RecipeOwner.Process, Gate, MinQ);

        private static CandidateQuery Query(int actorId, int[] qualities, int level, params int[] busyActors)
            => new CandidateQuery(actorId, qualities, new SpySkillLevels(level), new SpyFurnace(busyActors));

        // ══════════════════════════════════════════════════════════════════
        // AC-18-05 · min_quality 使用点 ⊆ 谓词,且零幅值调制
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// **进构建的全部装配**的源文件(AC-18-05 / AC-18-20 的扫描面)。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ <b>2026-10-27 评审 A1/A2 订正 —— 扫描面由 asmdef 派生,不是手写目录清单</b>:
        /// 初版只枚举 `Assets/Sim/**`,而 AC-18-05 原文(`processing.md:721`)写的是
        /// 「`min_quality` 的**全部使用点**……零幅值调制路径」,**没有限定程序集**。
        /// 实测 `Gameplay.Presentation/CookedCodec.cs:203` 有一处运行期写入 ——
        /// 初版扫描面**看不见它**。若将来有人在 `Gameplay.Presentation` 写
        /// `damage *= recipe.MinQuality`,初版夹具**全绿**。</para>
        /// <para><b>判据 = `includePlatforms` 为空</b>(Unity asmdef 语义:空 ⇒ 全平台 ⇒ 进构建)。
        /// `Editor.Tools.*` 的 `includePlatforms: ["Editor"]` 与 `Tests/**` 自动排除 ——
        /// 这不是「豁免」,是它们**物理上不在玩家构建里**(承 ADR-022「运行期零存在」)。
        /// 新增运行时装配**自动**纳入,无需改本方法 ⇒ 与 ADR-025 §④ 的清单封闭性同构。</para>
        /// <para>⚠️ 扫描面自证:下面两条断言各查 `scannedFiles` 下限,防「一个文件都没扫到」
        /// 时「零命中」变成恒真。</para>
        /// </remarks>
        private static List<string> SimAssemblySources()
        {
            string unityRoot = Path.Combine(RepoRoot(), "unity");
            string assetsRoot = Path.Combine(unityRoot, "Assets");

            var files = new List<string>();
            foreach (string asmdef in Directory.GetFiles(assetsRoot, "*.asmdef",
                                                         SearchOption.AllDirectories))
            {
                if (!IsBuiltForPlayer(asmdef)) continue;   // Editor-only / 不参与构建 ⇒ 不在面内

                string dir = Path.GetDirectoryName(asmdef);
                foreach (string cs in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    files.Add(cs.Substring(unityRoot.Length)
                                .TrimStart(Path.DirectorySeparatorChar)
                                .Replace(Path.DirectorySeparatorChar, '/'));
                }
            }
            return files;
        }

        /// <summary>asmdef 是否进玩家构建 —— `includePlatforms` 为空 ⇒ 全平台 ⇒ 进。</summary>
        /// <remarks>
        /// 不引 JSON 库:asmdef 的 `includePlatforms` 是扁平字符串数组,取方括号内的内容判空即可。
        /// ⚠️ 找不到该键 ⇒ **当作进构建**(fail-closed:宁可多扫,不可漏扫 ——
        /// 漏扫会让「零命中」变成假绿,多扫最多产生一条需要白名单的命中)。
        /// </remarks>
        private static bool IsBuiltForPlayer(string asmdefPath)
        {
            string text = File.ReadAllText(asmdefPath);
            int key = text.IndexOf("\"includePlatforms\"", StringComparison.Ordinal);
            if (key < 0) return true;                       // 缺键 ⇒ 全平台

            int open = text.IndexOf('[', key);
            if (open < 0) return true;
            int close = text.IndexOf(']', open);
            if (close < 0) return true;

            // 方括号内除空白与逗号外还有内容 ⇒ 有平台限制(Unity 里只有 "Editor" 会这样)
            for (int i = open + 1; i < close; i++)
            {
                char c = text[i];
                if (!char.IsWhiteSpace(c) && c != ',') return false;
            }
            return true;
        }

        /// <summary>允许触碰 `MinQuality` 的**唯一**文件(相对 `unity/`)。</summary>
        private const string MinQualitySoleOwner = "Assets/Sim/Processing/AdmissiblePredicate.cs";

        /// <summary>
        /// `MinQuality` 的**白名单**(相对 `unity/`)—— 不是使用点,是**形状声明与装载**。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ 白名单是**具名 + 带理由**的,且**过期即红**:下面
        /// <see cref="test_ac1805_whitelist_isLiveAndJustified"/> 断言每一条白名单文件
        /// 仍真的含 `MinQuality` —— 文件被删 / 字段被改名后白名单条目变成**免检通行证**,
        /// 那是比没有白名单更坏的形态(它静默扩大豁免面)。</para>
        /// </remarks>
        private static readonly (string Path, string Why)[] MinQualityWhitelist =
        {
            ("Assets/Sim.Contracts/ItemDatabase/Recipe.cs",
             "字段**声明**本身 —— 类型定义处,不是使用点(没有它就没有 min_quality 这个量)"),
            ("Assets/Gameplay.Presentation/CookedCodec.cs",
             "构建期烘焙产物的**反序列化写入** —— 把 cooked 字节还原成 `Recipe` 字段。"
             + "它只赋值,不读、不比较、不参与任何算式;是 `MinQuality` 得以存在的装载路径"),
        };

        /// <summary>白名单条目必须是**活的**(文件在盘且真含该符号),否则豁免面被静默扩大。</summary>
        [Test]
        public void test_ac1805_whitelist_isLiveAndJustified()
        {
            string root = RepoRoot();
            var dead = new List<string>();

            foreach (var (rel, why) in MinQualityWhitelist)
            {
                string full = Path.Combine(root, "unity", rel);
                if (!File.Exists(full)) { dead.Add($"{rel} —— 文件不存在(理由:「{why}」)"); continue; }

                string code = StripComments(File.ReadAllText(full));
                if (!IdentifierOccurrences(code, "MinQuality").Any())
                    dead.Add($"{rel} —— 文件在,但已不含 `MinQuality`(理由:「{why}」已失效)");
            }

            Assert.That(dead, Is.Empty,
                "AC-18-05 白名单里有**失效条目** —— 它此刻是一条免检通行证," +
                "静默扩大了豁免面(比没有白名单更坏):\n  " + string.Join("\n  ", dead));
        }

        [Test]
        public void test_ac1805_minQuality_usedOnlyInsideAdmissiblePredicate()
        {
            string root = RepoRoot();
            var hits = new List<string>();
            int scannedFiles = 0;
            var exempt = new HashSet<string>(StringComparer.Ordinal) { MinQualitySoleOwner };
            foreach (var (rel, _) in MinQualityWhitelist) exempt.Add(rel);

            foreach (string rel in SimAssemblySources())
            {
                scannedFiles++;
                if (exempt.Contains(rel)) continue;   // 唯一合法落点 + 具名白名单

                string code = StripComments(File.ReadAllText(Path.Combine(root, "unity", rel)));
                foreach (int at in IdentifierOccurrences(code, "MinQuality"))
                    hits.Add($"{rel}:{LineOf(code, at)}");
            }

            // ⚠️ 扫描面自证:一个文件都没扫到时,下面的「零命中」无意义。
            Assert.That(scannedFiles, Is.GreaterThan(50),
                $"只扫到 {scannedFiles} 个进构建的源文件 —— 扫描面塌了,「零命中」不可信");

            Assert.That(hits, Is.Empty,
                "AC-18-05:`min_quality` 的使用点越出 `Admissible` 谓词 —— " +
                "品级出口只有 21a F5,出现在别处即违规则三(:167):\n  "
                + string.Join("\n  ", hits));
        }

        [Test]
        public void test_ac1805_scannerPositiveControl_detectsInjectedUse()
        {
            // ⚠️ 正控:注入一份**不在盘上**的合成源码,扫描器必须扫出来。
            //    没有这条,上面那条「零命中」与「扫描器失效」不可区分。
            const string synthetic = @"
namespace Fake {
    public static class Bad {
        public static int Modulate(Recipe r) { return r.MinQuality * 2; }
    }
}";
            var found = IdentifierOccurrences(StripComments(synthetic), "MinQuality").ToList();

            Assert.That(found, Is.Not.Empty,
                "扫描器对注入的 `MinQuality` 使用**零命中** —— 判别力为零,AC-18-05 不可信");
        }

        [Test]
        public void test_ac1805_minQuality_neverEntersAmplitudeExpression()
        {
            // 幅值调制形态 = `MinQuality` 与乘 / 除同处一行。只读比较与赋值是合法的。
            string root = RepoRoot();
            string code = StripComments(
                File.ReadAllText(Path.Combine(root, "unity", MinQualitySoleOwner)));

            var offenders = AmplitudeOffenders(code);

            Assert.That(offenders, Is.Empty,
                "AC-18-05:`MinQuality` 出现在乘除表达式中 —— 那是幅值调制路径:\n  "
                + string.Join("\n  ", offenders));

            // ⚠️ 正控**必须调用与上方完全相同的判据**(`AmplitudeOffenders`)。
            //    2026-10-27 评审 T1:初版正控只在字面量上重跑了一遍 `string.IndexOf`,
            //    **从未调用被判据的那个循环** —— 把判据的 `'*'` 改成 `'+'`(真幅值调制
            //    从此漏检),初版正控**依旧绿**。那不是正控,是「自造断言」。
            Assert.That(AmplitudeOffenders("int m = recipe.MinQuality * 2;"), Is.Not.Empty,
                "正控失败:本判据看不见注入的乘式 —— 上方的「零命中」不可信");
            Assert.That(AmplitudeOffenders("int m = recipe.MinQuality / 2;"), Is.Not.Empty,
                "正控失败:本判据看不见注入的除式 —— 上方的「零命中」不可信");
            // 反向正控:合法的比较式**不得**被误报(否则判据过宽,真表会假红)。
            Assert.That(AmplitudeOffenders("if (minQuality < recipe.MinQuality) return X;"), Is.Empty,
                "反向正控失败:判据把合法的比较式误报成幅值调制 —— 判据过宽");
        }

        /// <summary>`MinQuality` 与乘 / 除同处一行的那些行(幅值调制的形态判据)。</summary>
        /// <remarks>
        /// ⚠️ **单独成方法是为了让正控能调它**(2026-10-27 评审 T1)。内联循环时正控只能
        /// 另写一份等价逻辑,而「等价」恰恰是没有夹具保证的那件事 —— 两份判据会各自漂移,
        /// 正是 AC-18-20 要防的形态。抽出来以后,正控与被控断言**物理上是同一条代码**。
        /// </remarks>
        private static List<string> AmplitudeOffenders(string code)
        {
            var offenders = new List<string>();
            string[] lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf("MinQuality", StringComparison.Ordinal) < 0) continue;
                if (lines[i].IndexOf('*') >= 0 || lines[i].IndexOf('/') >= 0)
                    offenders.Add($"第 {i + 1} 行:{lines[i].Trim()}");
            }
            return offenders;
        }

        [Test]
        public void test_ac1805_predicateNeverReadsRecipeInputsOrOutputs()
        {
            // AC-18-05 的实质:谓词碰产出侧就是幅值调制。判据 = 18 侧源文件里
            // 不出现 `Inputs` / `Outputs` 的成员访问。
            string root = RepoRoot();
            var offenders = new List<string>();

            foreach (string rel in new[]
                     {
                         "Assets/Sim/Processing/AdmissiblePredicate.cs",
                         "Assets/Sim/Processing/ProcessingCandidateSet.cs",
                     })
            {
                string code = StripComments(File.ReadAllText(Path.Combine(root, "unity", rel)));
                foreach (string member in new[] { "Inputs", "Outputs" })
                    foreach (int at in IdentifierOccurrences(code, member))
                        offenders.Add($"{rel}:{LineOf(code, at)} 出现 `{member}`");
            }

            Assert.That(offenders, Is.Empty,
                "18 侧读了配方的投入 / 产出侧 —— 谓词只许看 `SkillGate` / `MinQuality`:\n  "
                + string.Join("\n  ", offenders));

            // 正控:同一标识符匹配对注入文本必须命中。
            Assert.That(
                IdentifierOccurrences("var x = recipe.Outputs.Length;", "Outputs"), Is.Not.Empty,
                "正控失败:标识符匹配器看不见 `Outputs` —— 上方的「零命中」不可信");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-10 · 候选集 iff 双向(8 组合矩阵 + 反向 + 边界)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1810_iffMatrix_allEightCombinations()
        {
            // 三个布尔维:门过/不过 × 品级过/不过 × 炉闲/忙。逐组合**双向**断言 ——
            // 「三条件全真却未进」与「有假却进了」都会红。
            Recipe recipe = DefaultProcessRecipe();

            for (int bits = 0; bits < 8; bits++)
            {
                bool gateOk = (bits & 1) != 0;
                bool qualityOk = (bits & 2) != 0;
                bool furnaceIdle = (bits & 4) != 0;

                int level = gateOk ? Gate : Gate - 1;        // 恰等也算过(≥)
                int quality = qualityOk ? MinQ : MinQ - 1;   // 恰等也算过(≥)

                CandidateQuery q = furnaceIdle
                    ? Query(Actor, new[] { quality }, level)
                    : Query(Actor, new[] { quality }, level, Actor);

                bool expected = gateOk && qualityOk && furnaceIdle;
                bool actual = ProcessingCandidateSet.IsCandidate(recipe, q);

                Assert.That(actual, Is.EqualTo(expected),
                    $"组合 bits={bits}(gateOk={gateOk} qualityOk={qualityOk} furnaceIdle={furnaceIdle})" +
                    " —— iff 有一向不成立");
            }
        }

        [Test]
        public void test_ac1810_iffReverse_nonProcessOwnerNeverCandidate()
        {
            // 反向核心:owner ≠ process ⇒ **任何**玩家状态都不进候选集。
            // 用「全部条件都满足」的上下文(最容易被误放行的形态)。
            CandidateQuery q = Query(Actor, new[] { MinQ + 5 }, Gate + 5);

            foreach (RecipeOwner owner in new[] { RecipeOwner.Craft, RecipeOwner.Build })
            {
                Recipe r = MakeRecipe("fx_" + owner, owner, Gate, MinQ);
                Assert.That(ProcessingCandidateSet.IsCandidate(r, q), Is.False,
                    $"owner={owner} 的配方进了炮制候选集 —— 子集划分被击穿(D-21-30)");
            }

            // 正控:同上下文下 process 配方本应进候选集 —— 否则上面的 False 无判别力。
            Recipe ok = MakeRecipe("fx_process", RecipeOwner.Process, Gate, MinQ);
            Assert.That(ProcessingCandidateSet.IsCandidate(ok, q), Is.True,
                "正控失败:process 配方在满条件下也未进候选集");
        }

        [Test]
        public void test_ac1810_boundary_equalityPasses()
        {
            // 边界:level 恰等 gate、quality 恰等 minQ ⇒ **过**(GDD 是 ≥,不是 >)。
            Recipe r = DefaultProcessRecipe();
            Assert.That(ProcessingCandidateSet.IsCandidate(r, Query(Actor, new[] { MinQ }, Gate)),
                Is.True,
                "level == skill_gate 或 quality == min_quality 被判不过 —— F-18.2 是 ≥");
        }

        [Test]
        public void test_ac1810_minIsOverNamedInstances_notPerInputRow()
        {
            // F-18.2 补注①:min 的对象是**被点名实例集**。
            // 配方 minQ=3;实例 {5,1} ⇒ min=1 ⇒ 不过。若实现取 max 或首元素,本测红。
            Recipe r = DefaultProcessRecipe();

            Assert.That(
                ProcessingCandidateSet.IsCandidate(r, Query(Actor, new[] { 5, 4 }, Gate)), Is.True,
                "全部实例都 ≥ minQ 却被拒");

            Assert.That(
                ProcessingCandidateSet.IsCandidate(r, Query(Actor, new[] { 5, 1 }, Gate)), Is.False,
                "有一个实例品级 1 < minQ=3 却放行 —— min 取错了对象(可能取了 max 或首元素)");
        }

        [Test]
        public void test_ac1810_emptyInstances_shortCircuitsFalseWithNamedReason()
        {
            // 空实例集:min over ∅ 无定义 ⇒ 不通过,**且理由具名**(不是笼统的 MinQuality)。
            Recipe r = DefaultProcessRecipe();
            var levels = new SpySkillLevels(Gate);

            Assert.That(AdmissiblePredicate.Judge(r, Actor, Array.Empty<int>(), levels),
                Is.EqualTo(AdmissionRejection.NoConsumedInstances),
                "空实例集未给出具名理由 —— 「料不够好」与「上层没传实例」将不可区分");

            Assert.That(
                ProcessingCandidateSet.IsCandidate(r, Query(Actor, Array.Empty<int>(), Gate)), Is.False,
                "空实例集进了候选集 —— min over ∅ 被当成了「无下限」");
        }

        [Test]
        public void test_ac1810_skillGateRejectedBeforeQuality_soReasonIsSkillGate()
        {
            // 两道门都不过时理由须是 SkillGate(GDD 书写次序)。
            // ⚠️ 本条钉的是**可诊断性**,不是判定结果(两者都不过 ⇒ 结果相同)。
            Assert.That(
                AdmissiblePredicate.Judge(DefaultProcessRecipe(), Actor,
                                          new[] { MinQ - 1 }, new SpySkillLevels(Gate - 1)),
                Is.EqualTo(AdmissionRejection.SkillGate),
                "两道门都不过时未先报技能门 —— 面板会提示「换好料」而实际该「先练级」");
        }

        [Test]
        public void test_ac1810_crossActorIndependence()
        {
            // 单炉是 per-player(OQ-18-3 裁 [甲]):甲忙不影响乙的候选集。
            Recipe r = DefaultProcessRecipe();
            var levels = new SpySkillLevels(new Dictionary<int, int>
            {
                [Actor] = Gate + 1,
                [OtherActor] = Gate + 1,
            });
            var furnace = new SpyFurnace(Actor);   // 只有甲忙

            var qA = new CandidateQuery(Actor, new[] { MinQ }, levels, furnace);
            var qB = new CandidateQuery(OtherActor, new[] { MinQ }, levels, furnace);

            Assert.That(ProcessingCandidateSet.IsCandidate(r, qA), Is.False,
                "甲已有一炉在烧却仍进候选集");
            Assert.That(ProcessingCandidateSet.IsCandidate(r, qB), Is.True,
                "甲忙导致乙也被拒 —— 单炉被实现成了全局单炉");
        }

        [Test]
        public void test_ac1810_evaluatePreservesOrderAndAgreesWithIsCandidate()
        {
            // 批量路径必须与逐条 IsCandidate 逐位一致(防批量成为第二实现),且**保序**
            // (排序归 42,O-18-R6 未裁 —— sim 不得固化一个未裁的呈现决策)。
            var recipes = new List<Recipe>
            {
                MakeRecipe("a_process_ok",   RecipeOwner.Process, Gate,     MinQ),
                MakeRecipe("b_craft",        RecipeOwner.Craft,   Gate,     MinQ),
                MakeRecipe("c_process_gate", RecipeOwner.Process, Gate + 3, MinQ),
                MakeRecipe("d_process_ok",   RecipeOwner.Process, Gate,     MinQ),
            };
            CandidateQuery q = Query(Actor, new[] { MinQ }, Gate);

            List<string> expected = recipes
                .Where(r => ProcessingCandidateSet.IsCandidate(r, q))
                .Select(r => r.RecipeId).ToList();
            List<string> got = ProcessingCandidateSet.Evaluate(recipes, q)
                .Select(r => r.RecipeId).ToList();

            Assert.That(got, Is.EqualTo(expected),
                "Evaluate 与逐条 IsCandidate 结果不一致 —— 批量路径成了第二实现");
            Assert.That(got, Is.EqualTo(new[] { "a_process_ok", "d_process_ok" }),
                "候选集内容或**顺序**不符");
        }

        [Test]
        public void test_ac1810_nullRecipes_throwsNotSilentEmpty()
        {
            // fail-closed:null 配方表 ≠ 零候选(空面板与无配方在玩家侧不同处境)。
            CandidateQuery q = Query(Actor, new[] { MinQ }, Gate);
            Assert.Throws<ArgumentNullException>(() => ProcessingCandidateSet.Evaluate(null, q));
        }

        [Test]
        public void test_ac1810_defaultQuery_throwsArgumentNull_notNullReference()
        {
            // ⚠️ 2026-10-27 评审 AR1:`CandidateQuery` 是 `readonly struct`,编译器**隐式提供
            //    无参构造器** ⇒ `default(CandidateQuery)` 合法且三字段全 null,**绕过**其构造器
            //    的三道检查。初版注释称「任一引用参数为 null ⇒ fail-closed」—— 那句话对
            //    `default` 不成立。若消费侧不补验,`default` 会落到
            //    `query.Furnace.IsBusy(...)` 抛 **NullReferenceException**。
            //
            //    两者不可合并:NRE 说「解引用了一个本不该为 null 的东西」(实现 bug),
            //    ArgumentNullException 说「调用方漏传」(接线 bug)。现场归因方向相反。
            Recipe r = DefaultProcessRecipe();

            Assert.Throws<ArgumentNullException>(() =>
                ProcessingCandidateSet.IsCandidate(r, default(CandidateQuery)),
                "`default(CandidateQuery)` 未在入口被拦 —— 会以 NRE 形态炸在更深的地方");

            // 逐条:三个引用字段各自缺失都必须被点名(不是只有「第一个」被检查)。
            Assert.Throws<ArgumentNullException>(() =>
                ProcessingCandidateSet.IsCandidate(r, new CandidateQuery(Actor, null,
                    new SpySkillLevels(Gate), new SpyFurnace())));
            Assert.Throws<ArgumentNullException>(() =>
                ProcessingCandidateSet.IsCandidate(r, new CandidateQuery(Actor, new[] { MinQ },
                    null, new SpyFurnace())));
            Assert.Throws<ArgumentNullException>(() =>
                ProcessingCandidateSet.IsCandidate(r, new CandidateQuery(Actor, new[] { MinQ },
                    new SpySkillLevels(Gate), null)));
        }

        [Test]
        public void test_ac1810_emptyRecipeTable_stillValidatesQuery()
        {
            // ⚠️ 空配方表时循环不执行 —— 若把 query 校验只留在 `IsCandidate` 里就**漏检**。
            //    而「空表 + null query」恰是最容易出现的接线错误(新玩家还没有任何配方)。
            Assert.Throws<ArgumentNullException>(() =>
                ProcessingCandidateSet.Evaluate(Array.Empty<Recipe>(), default(CandidateQuery)),
                "空配方表跳过了 query 校验 —— 校验必须在循环之前");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-20 · 同一函数零第二实现(IL 面) + 纯函数性
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1820_bothCallSites_callTheSameMethodReference()
        {
            // 判据 = IL 里 `call` 的方法引用**逐位相同**,不是「行为相同的两份」。
            // 预答若内联复制一份判据,两条路径的方法引用会不同 ⇒ 本测红。
            string dll = typeof(AdmissiblePredicate).Assembly.Location;
            MethodReference preview = FindCalled(dll, nameof(AdmissiblePredicate.PreviewVerdict),
                                                 nameof(AdmissiblePredicate.Judge));
            MethodReference terminal = FindCalled(dll, nameof(AdmissiblePredicate.TerminalVerdict),
                                                  nameof(AdmissiblePredicate.Judge));

            Assert.That(preview, Is.Not.Null,
                "预答方法体内**没有**对 `Judge` 的 call —— 它可能内联了判据(第二实现)");
            Assert.That(terminal, Is.Not.Null,
                "终裁方法体内**没有**对 `Judge` 的 call —— 它可能内联了判据(第二实现)");

            Assert.That(preview.FullName, Is.EqualTo(terminal.FullName),
                $"两条路径调用了**不同的**方法:\n  预答 → {preview.FullName}\n" +
                $"  终裁 → {terminal.FullName}\nAC-18-20 要求同一函数,不是两份行为相同的实现");
        }

        [Test]
        public void test_ac1820_bothCallSites_shareTheSameAdmittedGate()
        {
            // 「通过」的判定也须同源 —— 否则两条路径可以共用 Judge 却各自定义「算不算过」。
            string dll = typeof(AdmissiblePredicate).Assembly.Location;
            MethodReference preview = FindCalled(dll, nameof(AdmissiblePredicate.PreviewVerdict),
                                                 nameof(AdmissiblePredicate.IsAdmitted));
            MethodReference terminal = FindCalled(dll, nameof(AdmissiblePredicate.TerminalVerdict),
                                                  nameof(AdmissiblePredicate.IsAdmitted));

            Assert.That(preview, Is.Not.Null, "预答未转调 `IsAdmitted` —— 它自带了「算不算过」");
            Assert.That(terminal, Is.Not.Null, "终裁未转调 `IsAdmitted` —— 它自带了「算不算过」");
            Assert.That(preview.FullName, Is.EqualTo(terminal.FullName),
                "两条路径对「算不算过」用了不同实现");
        }

        [Test]
        public void test_ac1820_bothCallSites_haveNoComparisonOfTheirOwn()
        {
            // 加强:两个包装方法体内**零比较指令** —— 否则「同源」只是表面。
            // ⚠️ 这条正是 `IsAdmitted` 存在的理由:若包装里直接写
            //    `Judge(...) == AdmissionRejection.None`,Debug 构建会 emit `ceq`,
            //    本测必红 —— 而那个 `ceq` 就是第二处判据。
            string dll = typeof(AdmissiblePredicate).Assembly.Location;

            foreach (string name in new[]
                     {
                         nameof(AdmissiblePredicate.PreviewVerdict),
                         nameof(AdmissiblePredicate.TerminalVerdict),
                     })
            {
                List<string> comparisons = FindComparisonsIn(dll,
                    nameof(AdmissiblePredicate), name);
                Assert.That(comparisons, Is.Empty,
                    $"`{name}` 方法体内出现比较指令({string.Join(", ", comparisons)}) —— " +
                    "包装方法只许转调,不得自带判据。方法体全部指令:"
                    + string.Join(" ", OpcodeInventory(dll, nameof(AdmissiblePredicate), name)));
            }
        }

        [Test]
        public void test_ac1820_ilScannerPositiveControl_seesComparisonInJudge()
        {
            // ⚠️ 正控:`Judge` 本体**应当**有比较指令(两道门 + 空集检查)。
            //    没有这条,上面那条「零比较」与「扫描器看不见比较」不可区分。
            string dll = typeof(AdmissiblePredicate).Assembly.Location;
            List<string> comparisons = FindComparisonsIn(dll,
                nameof(AdmissiblePredicate), nameof(AdmissiblePredicate.Judge));

            Assert.That(comparisons, Is.Not.Empty,
                "扫描器在 `Judge` 体内**没扫到任何比较指令** —— 判别力为零。方法体全部指令:"
                + string.Join(" ", OpcodeInventory(dll, nameof(AdmissiblePredicate),
                                                    nameof(AdmissiblePredicate.Judge))));
        }

        [Test]
        public void test_ac1820_admittedIsTheSoleDefinitionPoint_acrossSimAssembly()
        {
            // 「`AdmissionRejection.None` 的比较只有一处」—— `IsAdmitted` 的头注这样写。
            // ⚠️ 这条断言是**补的**:2026-10-27 自查发现 `ProcessingCandidateSet` 曾自写
            //    `Judge(...) == AdmissionRejection.None`,与头注直接矛盾(第二个定义点),
            //    而当时 8 组合矩阵全绿 —— **行为等价掩盖了结构分叉**。
            //    ⇒ 判据必须落在**符号面**,不能只靠行为面。
            string root = RepoRoot();
            var offenders = new List<string>();
            int scannedFiles = 0;

            foreach (string rel in SimAssemblySources())
            {
                scannedFiles++;
                if (rel == MinQualitySoleOwner) continue;   // 唯一定义点所在文件
                string code = StripComments(File.ReadAllText(Path.Combine(root, "unity", rel)));
                foreach (int at in IdentifierOccurrences(code, "AdmissionRejection.None"))
                    offenders.Add($"{rel}:{LineOf(code, at)}");
            }

            // ⚠️ 扫描面自证(与 AC-18-05 同款):扫描面塌了时「零命中」是恒真。
            Assert.That(scannedFiles, Is.GreaterThan(50),
                $"只扫到 {scannedFiles} 个进构建的源文件 —— 扫描面塌了,「零命中」不可信");

            Assert.That(offenders, Is.Empty,
                "`AdmissionRejection.None` 在 `AdmissiblePredicate.cs` 之外被比较 —— " +
                "「算不算过」出现了第二个定义点(AC-18-20 同族):\n  "
                + string.Join("\n  ", offenders));

            // 正控:同一匹配器对注入文本必须命中。
            Assert.That(
                IdentifierOccurrences("if (r == AdmissionRejection.None) return true;",
                                      "AdmissionRejection.None"), Is.Not.Empty,
                "正控失败:匹配器看不见 `AdmissionRejection.None` —— 上方的「零命中」不可信");
        }

        [Test]
        public void test_ac1820_candidateSet_goesThroughIsAdmitted()
        {
            // 行为面的补强:`IsCandidate` 的 IL 里必须有对 `IsAdmitted` 的 call。
            // 若它自写 `== AdmissionRejection.None`,本测红(即便行为暂时等价)。
            string dll = typeof(AdmissiblePredicate).Assembly.Location;
            MethodReference call = FindCalledInType(
                dll, typeof(ProcessingCandidateSet).FullName,
                nameof(ProcessingCandidateSet.IsCandidate),
                nameof(AdmissiblePredicate.IsAdmitted));

            Assert.That(call, Is.Not.Null,
                "`IsCandidate` 未转调 `IsAdmitted` —— 「算不算过」在候选集里自成一处");
        }

        [Test]
        public void test_ac1820_pureFunction_sameInputSameOutput_noCrossCallState()
        {
            // 纯函数性:同输入求值两次逐位同;每次恰好查询一次等级(无跨调用缓存)。
            Recipe r = DefaultProcessRecipe();
            var levels = new SpySkillLevels(Gate);
            var qualities = new[] { MinQ };

            AdmissionRejection a = AdmissiblePredicate.Judge(r, Actor, qualities, levels);
            int afterFirst = levels.CallCount;
            AdmissionRejection b = AdmissiblePredicate.Judge(r, Actor, qualities, levels);

            Assert.That(b, Is.EqualTo(a), "同输入两次求值结果不同 —— 谓词不是纯函数");
            Assert.That(levels.CallCount - afterFirst, Is.EqualTo(1),
                "第二次求值未恰好查询一次等级 —— 出现跨调用缓存(缓存键不纯即违 ADR-016 §一)");
        }

        [Test]
        public void test_ac1810_furnaceQueriedExactlyOncePerEvaluation()
        {
            // 候选集三条件里,「单炉空闲」是**唯一**会随时间变的那个(等级与品级集在一次
            // 面板求值内是常量)。它每求值被查**多次**不会改变答案,却意味着实现在循环里
            // 反复穿透到 `FurnaceState.IsBusy`(那里要折叠事件流)——
            // 面板 N 个配方就是 N 次全流折叠。
            // ⇒ 钉「恰一次」:既是性能判据,也是「单炉判定不在逐配方路径上」的结构判据。
            Recipe r = DefaultProcessRecipe();
            var furnace = new SpyFurnace();
            var q = new CandidateQuery(Actor, new[] { MinQ }, new SpySkillLevels(Gate), furnace);

            ProcessingCandidateSet.IsCandidate(r, q);

            Assert.That(furnace.CallCount, Is.EqualTo(1),
                $"一次候选判定查了 {furnace.CallCount} 次单炉 —— 三条件合取里它只该出现一次");
        }

        [Test]
        public void test_ac1820_skillLevelReadFace_isSingleDeclaredSource()
        {
            // ⚠️ 2026-10-27 评审 AR2 的判据化:接口隔离(谓词只收 `ISkillLevelQuery`)
            //    初版做法是「另立接口 + 将来写适配器」,代价是 `QueryLevel` 有**两处逐字相同的
            //    声明**(`ISkillLevelQuery` + `ISkillGrowthPort`)⇒ 同一份等级有两条可各自漂移的
            //    读路径,而「它们必须一致」只靠纪律。
            //    现改为 `ISkillGrowthPort : ISkillLevelQuery`。本测钉住这条继承关系:
            //    有人把它改回「两个互不相干的接口」,这里转红。
            Type levelQuery = typeof(DaYiJingCheng.Sim.Contracts.ISkillLevelQuery);
            Type growthPort = typeof(DaYiJingCheng.Sim.Prescription.ISkillGrowthPort);

            Assert.That(levelQuery.IsAssignableFrom(growthPort), Is.True,
                "`ISkillGrowthPort` 不再继承 `ISkillLevelQuery` —— " +
                "`QueryLevel` 又变成两处独立声明,等级读面重新分叉(AR2 回归)");

            // 反向:隔离方向未变 —— 只读面**不得**反向拿到写能力。
            Assert.That(growthPort.IsAssignableFrom(levelQuery), Is.False,
                "`ISkillLevelQuery` 反向继承了 `ISkillGrowthPort` —— " +
                "纯谓词将持有 `EmitGrowth` 写通道(接口隔离被击穿)");
            Assert.That(
                levelQuery.GetMethods().Any(m => m.Name == "EmitGrowth"), Is.False,
                "`ISkillLevelQuery` 上出现了 `EmitGrowth` —— 只读面被污染");

            // `QueryLevel` 在本接口上只声明**一次**(继承不会重复计数,这条同时防
            // 「继承后又 shadow 一份新声明」)。
            Assert.That(
                levelQuery.GetMethods().Count(m => m.Name == "QueryLevel"), Is.EqualTo(1),
                "`QueryLevel` 在 `ISkillLevelQuery` 上不是恰一处声明");
        }

        [Test]
        public void test_ac1820_craftSkillId_matchesLiteralSeven()
        {
            // 双向钉:字面量 7 与 `(int)SkillId.炮制` 与 `CraftSkillId` 三者一致。
            // 变异任一侧都会红;断言里不拿 `CraftSkillId` 当期望值(防自指)。
            Assert.That((int)SkillId.炮制, Is.EqualTo(CraftSkillLiteral),
                "`SkillId.炮制` 不再是 7 —— 30 的技能号被改过,须同步本系统");
            Assert.That(AdmissiblePredicate.CraftSkillId, Is.EqualTo(CraftSkillLiteral),
                "`AdmissiblePredicate.CraftSkillId` 不等于 7 —— 谓词查错了技能");
        }

        [Test]
        public void test_ac1820_judge_queriesTheCraftSkill_notAnyOther()
        {
            // 行为面钉:实际传给 QueryLevel 的 skillId 必须是炮制(7)。
            var levels = new SpySkillLevels(Gate);
            AdmissiblePredicate.Judge(DefaultProcessRecipe(), Actor, new[] { MinQ }, levels);

            Assert.That(levels.QueriedSkillIds, Is.EqualTo(new[] { CraftSkillLiteral }),
                "谓词查询的技能 id 不是炮制(7)");
        }

        [Test]
        public void test_ac1820_nullInputs_failClosed()
        {
            // 三道 fail-closed:品级集 null / 等级查询 null / 炉查询 null(构造期)。
            Recipe r = DefaultProcessRecipe();
            var levels = new SpySkillLevels(Gate);

            Assert.Throws<ArgumentNullException>(
                () => AdmissiblePredicate.Judge(r, Actor, null, levels),
                "品级集 null 未抛 —— 会被当成空集,而空集语义是 NoConsumedInstances(不同的事)");

            Assert.Throws<ArgumentNullException>(
                () => AdmissiblePredicate.Judge(r, Actor, new[] { MinQ }, null),
                "等级查询 null 未抛 —— 会被当成零级继续判");

            Assert.Throws<ArgumentNullException>(
                () => new CandidateQuery(Actor, new[] { MinQ }, levels, null),
                "炉查询 null 未抛 —— 会被当成「永远空闲」放行(AC-18-10 第三条件静默失效)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-18 · 存在性与归属校验(**不重写判据**)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1818_partitionJudgementLivesInItsOwnFixtureOnly()
        {
            // AC-18-18 的真身 = `recipe_owner_partition_test.cs`(6 条,两段记分)。
            // 本文件**不重写**它 —— 重写会造出两份判据,正是 AC-18-20 要防的形态的邻居。
            string root = RepoRoot();
            string partitionPath = Path.Combine(
                root, "unity/Assets/Tests/EditMode/Processing/recipe_owner_partition_test.cs");

            Assert.That(File.Exists(partitionPath), Is.True,
                "AC-18-18 的真身文件不在盘 —— 本文件不替代它,须先恢复");

            // ⚠️ 2026-10-27 评审 T2:初版判据 = `[Test]` 计数 >= 6 —— 把 6 个方法体**全部清空**
            //    只留属性,本条仍绿。那是「存在性」不是「语义」。改为断**方法体非空**:
            //    每条 `[Test]` 之后必须存在一对花括号,且括号内除空白外还有内容。
            string partitionCode = StripComments(File.ReadAllText(partitionPath));
            var emptyTests = EmptyTestMethodNames(partitionCode);

            Assert.That(emptyTests, Is.Empty,
                "AC-18-18 真身里有**空方法体的 `[Test]`** —— 属性在、断言没了," +
                "覆盖已被削空却仍显示为绿:\n  " + string.Join("\n  ", emptyTests));

            // 正控:本判据必须能认出注入的空测试 —— 否则上面的「零命中」不可信。
            Assert.That(
                EmptyTestMethodNames("[Test]\npublic void test_x() { }\n"), Is.Not.Empty,
                "正控失败:判据看不见空的 `[Test]` 方法体 —— 上方的「零命中」不可信");
        }

        /// <summary>找**方法体为空**的 `[Test]` 方法名(`[Test]` 之后第一对花括号内无实质内容)。</summary>
        /// <remarks>
        /// ⚠️ 这是形态判据不是语法分析:它抓的是「方法还在、断言被删光」这一种退化,
        /// 不判断断言**内容**是否正确(那归 `recipe_owner_partition_test.cs` 自己)。
        /// 目的是让「删空实现骗绿」这种最省事的退化**至少**要写成一句无意义断言才行 ——
        /// 抬高作弊成本,不宣称完备。
        /// </remarks>
        private static List<string> EmptyTestMethodNames(string code)
        {
            var result = new List<string>();
            int at = code.IndexOf("[Test]", StringComparison.Ordinal);
            while (at >= 0)
            {
                int bodyOpen = code.IndexOf('{', at);
                if (bodyOpen < 0) break;
                int bodyClose = code.IndexOf('}', bodyOpen);
                if (bodyClose < 0) break;

                bool empty = true;
                for (int i = bodyOpen + 1; i < bodyClose; i++)
                    if (!char.IsWhiteSpace(code[i])) { empty = false; break; }

                if (empty)
                {
                    // 方法名 = `{` 之前最近的标识符(跳过 `public void` 之类修饰)
                    string head = code.Substring(at, bodyOpen - at);
                    int nameEnd = head.LastIndexOf('(');
                    int nameStart = nameEnd > 0 ? head.LastIndexOfAny(
                        new[] { ' ', '\t', '\n', '\r' }, nameEnd) + 1 : 0;
                    result.Add(nameEnd > nameStart
                        ? head.Substring(nameStart, nameEnd - nameStart).Trim()
                        : "(未具名)");
                }

                at = code.IndexOf("[Test]", at + 1, StringComparison.Ordinal);
            }
            return result;
        }

        // ══════════════════════════════════════════════════════════════════
        // 扫描辅助(与 S20-9 同款先例;本文件自持,不跨测试类引用)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>找 `name` 以**标识符边界**出现的全部位置(不是带尾空格的子串匹配)。</summary>
        private static IEnumerable<int> IdentifierOccurrences(string code, string name)
        {
            int at = code.IndexOf(name, StringComparison.Ordinal);
            while (at >= 0)
            {
                int after = at + name.Length;
                bool leftOk = at == 0 || !IsIdentChar(code[at - 1]);
                bool rightOk = after >= code.Length || !IsIdentChar(code[after]);
                if (leftOk && rightOk) yield return at;
                at = code.IndexOf(name, at + 1, StringComparison.Ordinal);
            }
        }

        private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        /// <summary>找 `AdmissiblePredicate`.`methodName` 方法体内**第一条**对 `calleeName` 的调用。</summary>
        private static MethodReference FindCalled(string dll, string methodName, string calleeName)
            => FindCalledInType(dll, typeof(AdmissiblePredicate).FullName, methodName, calleeName);

        /// <summary>找 `typeFullName`.`methodName` 方法体内**第一条**对 `calleeName` 的调用。</summary>
        private static MethodReference FindCalledInType(string dll, string typeFullName,
                                                        string methodName, string calleeName)
        {
            using (ModuleDefinition mod = ModuleDefinition.ReadModule(dll))
            {
                TypeDefinition t = mod.GetTypes().FirstOrDefault(x => x.FullName == typeFullName);
                if (t == null) return null;

                foreach (MethodDefinition m in t.Methods)
                {
                    if (m.Name != methodName || !m.HasBody) continue;
                    foreach (Instruction ins in m.Body.Instructions)
                    {
                        if (ins.OpCode.Code != Code.Call && ins.OpCode.Code != Code.Callvirt)
                            continue;
                        if (ins.Operand is MethodReference mr && mr.Name == calleeName)
                            return mr;
                    }
                }
            }
            return null;
        }

        /// <summary>收集 `typeName`.`methodName` 方法体内的比较类指令(诊断用短名)。</summary>
        private static List<string> FindComparisonsIn(string dll, string typeName, string methodName)
        {
            var found = new List<string>();
            using (ModuleDefinition mod = ModuleDefinition.ReadModule(dll))
            {
                TypeDefinition t = mod.GetTypes().FirstOrDefault(x => x.Name == typeName);
                if (t == null) return found;

                foreach (MethodDefinition m in t.Methods)
                {
                    if (m.Name != methodName || !m.HasBody) continue;
                    foreach (Instruction ins in m.Body.Instructions)
                        if (IsComparison(ins))
                            found.Add(ins.OpCode.Name);
                }
            }
            return found;
        }

        /// <summary>比较类 IL 指令闭集 —— 按**助记符前缀**判,不按 <see cref="Code"/> 枚举值穷举。
        /// <para>两类必须都算:</para>
        /// <para>① <b>值形</b>(<c>ceq</c> / <c>cgt</c> / <c>clt</c>):结果作为 <c>bool</c> 值继续流动
        /// (如 <c>IsAdmitted</c> 的 <c>rejection == AdmissionRejection.None</c>)。</para>
        /// <para>② <b>分支形</b>(<c>beq</c> / <c>bne</c> / <c>bge</c> / <c>bgt</c> / <c>ble</c> /
        /// <c>blt</c>):<c>if (a &lt; b) return …;</c> 直接编译成条件跳转,**不产生** <c>clt</c>。</para>
        /// <para>⚠️ <b>为什么用前缀而不是 <see cref="Code"/> 白名单</b>:Cecil 把长/短两种形做成
        /// **不同枚举值**(<c>Code.Blt</c> vs <c>Code.Blt_S</c>),Debug 构建大量使用短形。
        /// 白名单漏掉 <c>_S</c> 变体即**静默假绿** —— 本条正是被正控抓出来的:初版只列了值形 +
        /// 长形分支,<c>Judge</c> 体内的两道 <c>&lt;</c> 全部漏掉,「包装方法零比较」随之空转绿。</para>
        /// </summary>
        private static bool IsComparison(Instruction ins)
        {
            string n = ins.OpCode.Name;
            return n.StartsWith("ceq", StringComparison.Ordinal)
                || n.StartsWith("cgt", StringComparison.Ordinal)
                || n.StartsWith("clt", StringComparison.Ordinal)
                || n.StartsWith("beq", StringComparison.Ordinal)
                || n.StartsWith("bne", StringComparison.Ordinal)
                || n.StartsWith("bge", StringComparison.Ordinal)
                || n.StartsWith("bgt", StringComparison.Ordinal)
                || n.StartsWith("ble", StringComparison.Ordinal)
                || n.StartsWith("blt", StringComparison.Ordinal);
        }

        /// <summary>某方法体内**全部**指令助记符(诊断用 —— 失败时贴出来,免得再猜)。</summary>
        private static List<string> OpcodeInventory(string dll, string typeName, string methodName)
        {
            var found = new List<string>();
            using (ModuleDefinition mod = ModuleDefinition.ReadModule(dll))
            {
                TypeDefinition t = mod.GetTypes().FirstOrDefault(x => x.Name == typeName);
                if (t == null) return found;
                foreach (MethodDefinition m in t.Methods)
                {
                    if (m.Name != methodName || !m.HasBody) continue;
                    foreach (Instruction ins in m.Body.Instructions)
                        found.Add(ins.OpCode.Name);
                }
            }
            return found;
        }

        private static string RepoRoot()
            => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));

        /// <summary>剥除 `//` 与 `/* */` 注释(字符串 / 字符字面量内容保留)。</summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, esc = false;

            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>字符偏移 → 行号(1 起;诊断用)。</summary>
        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }
    }
}
