// 权威来源:AC-20-21(BLOCKING,design/gdd/inventory-and-items.md:880)
//   「GIVEN 烘焙期 CARRY_CAP,WHEN 构建门求值,THEN CARRY_CAP > 0 且存在某状态 s:
//    CanCarry(s) == false 且 s 从开档经 DropClaimed 可达(自指谓词,不需要外部清单)」
//   · GDD :705-712(推荐 7 + 二轮 BL-24 改述 —— 原「< 一次完整出诊总重」全案无定义 ⇒
//     改自指谓词的**理由**)· :719(旋钮表的安全区间列)
//   · GDD :296-298(BL-6① `CARRY_CAP` 单源所有者 = 20)
//   · ADR-014 §五(装载期硬失败纪律)· ADR-006 D-21-17(cap 是 int 计数,不是 Fix)
//   · story-004 AC-20-21 / AC-20-13
//
// ⚠️ **本门与 `InventoryConstantsBinder`(Bake 侧)的分工**(两者判据面不重叠):
//   Binder = **数据合法性**(缺键 / 0 / 负 / 越 int / 非 int token / 未知键)⇒ AC-20-13;
//   本门   = **设计有效性**(这个 cap 值下「取舍」是否存在)⇒ AC-20-21。
//   ⚠️ 两者读同一份 `assets/data/inventory_constants.json`,但判据不同层,不是第二真源。
//
// ⚠️ **为什么本门不调用 Binder(装配方向约束)**:`Editor.Tools.Bake.asmdef` 引
//   `Editor.Tools.Gates`(**单向 Bake → Gates**)。Gates 侧反向引 Bake = 循环引用,
//   装配直接编不过。⇒ 本门自带一个**极小取数适配器**(见 <see cref="TryReadCarryCap"/> /
//   <see cref="TryReadItemWeights"/>),而**判据本体是纯函数**
//   (<see cref="Evaluate"/>,零 I/O)—— 测试直驱纯函数,门链只用适配器取数。
//   ⚠️ 适配器**只做取数,不做判据**:它读不出值 ⇒ **红**(fail-closed),不会静默跳过。
//
// ⚠️ **本门在最坏情况下是弱门 —— 这是 GDD 自己承认的,须如实登记**(GDD :705-712):
//   「存在某状态能使 CanCarry == false」在**领取次数无上界**的前提下,对任何有限 cap
//   都成立(多领几件就能装满)⇒ 本门**抓不到**「cap 太大以致带齐一切」那种设计缺陷。
//   它真正能抓的是三类**结构性**空洞:
//     ① cap ≤ 0(容量本身无意义)—— 与 Binder 同源判定,本门复查以自足;
//     ② 物品表为空(W_MIN 无定义,满档谓词无输入);
//     ③ 物品表全部 weight < 1(任何件数都加不出重量)。
//   ②③ 下「永不可能满」是**结构性的**,不是「cap 太大」那种程度性的。
//   ⇒ 「带齐一切」那类缺陷须等 GDD 登记的 **OQ-20-9**(具名 `ESSENTIAL_KIT`)才能立真门,
//     本门**不声称**覆盖它。
//
// ⚠️ **可达性是构造出来的,不是论证出来的**:本门**真的模拟**一串领取
//   (贪心领最重的一件直到装不下),而不是用「理论上总可以多领」这句散文顶替。
//   构造步数上界 = ⌈cap / W_MIN⌉ + 2(每步至少加 W_MIN)。承 `coding-standards.md`
//   §「某件有门」:**注释里声称有断言不构成门存在**。
//
// ⚠️ **判据必须经由被测件本身**:构造过程中调的是真 `CarryLoadSolver.CanCarry`,
//   不是重写一个 `load + w > cap`。否则「门绿」不证明「谓词对」。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>AC-20-21 存在性门 —— `CARRY_CAP > 0` ∧ 存在可达状态使 `CanCarry == false`。</summary>
    public static class CarryCapExistenceGate
    {
        /// <summary>源数据件的相对仓根路径(与 `InventoryConstantsSchema.FileName` 同源)。</summary>
        public const string DataFileRelativePath = "assets/data/inventory_constants.json";

        /// <summary>物品表相对仓根的路径(W_MIN 与可达性构造的真源)。</summary>
        public const string ItemsFileRelativePath = "assets/data/item_database_items.json";

        // ───────────────────────── 门链入口 ─────────────────────────

        /// <summary>门链入口(供 <c>AssemblyGates.RunAll</c> 调用)。
        /// <para><b>为什么进 RunAll 而不是挂 TestDriven</b>:AC-20-21 是 BLOCKING 级判据;
        /// `GateWiringManifestTest` 的封闭集判据禁止把 BLOCKING 判据判成无强制点档。</para>
        /// </summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/data/</c> 的那一层)。
        /// null / 空 ⇒ 从 <c>Application.dataPath</c> 上溯(与 `DiseaseConstantRegistryGate` 同法)。</param>
        /// <returns>违例清单(空 = 门绿)。</returns>
        public static List<string> RunAll(string repoRoot = null)
        {
            // ⚠️ `"."` 的语义歧义:门链各调用方传的是**工程根**(`unity/`),
            //    而本门要的是**仓库根**(含 `assets/data/` 的那层)。
            //    ⇒ 先试 `repoRoot` 下有没有 `assets/data/`;没有则按工程根上溯两级。
            //    ⚠️ 不做这个归一化会让构建门在生产路径上恒红(路径错 ≠ 数据错)。
            string root = ResolveDataRoot(repoRoot);

            string constantsPath = Path.Combine(root, DataFileRelativePath);
            string itemsPath = Path.Combine(root, ItemsFileRelativePath);

            // ① 面守卫:源件不在 ⇒ 红(不静默变绿;承 InventoryDtoFloatGate.CheckScanDirsExist 同口径)。
            var errs = new List<string>();
            if (!File.Exists(constantsPath))
                errs.Add($"[AC-20-21] 源件不存在:{constantsPath} —— 门无输入,拒以空集冒充绿");
            if (!File.Exists(itemsPath))
                errs.Add($"[AC-20-21] 物品表不存在:{itemsPath} —— W_MIN 与可达性构造无输入");
            if (errs.Count > 0) return errs;

            // ② 取 cap(fail-closed:取不出 ⇒ 红,不静默跳过)。
            if (!TryReadCarryCap(File.ReadAllText(constantsPath), out int cap, out string capErr))
            {
                errs.Add($"[AC-20-21] CARRY_CAP 取数失败:{capErr} —— 拒以空集冒充绿。" +
                         "⚠️ 数据合法性判据归 AC-20-13(Bake 侧 `InventoryConstantsBinder`)," +
                         "本门只声明「无输入不可求值」");
                return errs;
            }

            // ③ 取物品表重量(fail-closed 同上)。
            if (!TryReadItemWeights(File.ReadAllText(itemsPath), out List<int> weights, out string wErr))
            {
                errs.Add($"[AC-20-21] 物品表重量列取数失败:{wErr} —— 拒以空集冒充绿");
                return errs;
            }

            errs.AddRange(Evaluate(cap, weights));
            return errs;
        }

        // ───────────────────────── 判据本体(纯函数,测试直接驱动)─────────────────────────

        /// <summary>
        /// 判据本体 —— **纯函数**,零 I/O、零可变状态。门链与负向夹具共用**同一台机器**。
        /// <para>⚠️ 测试**必须**走本方法而不是复制一份逻辑,否则「门绿」与「测试绿」可以是两回事。</para>
        /// </summary>
        /// <param name="carryCap">`CARRY_CAP`(已从数据件取出)。</param>
        /// <param name="itemWeights">物品表**全部**条目的 `weight`(`ItemDef.weight`,int)。</param>
        /// <returns>违例清单(空 = 门绿)。</returns>
        public static List<string> Evaluate(int carryCap, IReadOnlyList<int> itemWeights)
        {
            var errs = new List<string>();

            // ── 判据 A:CARRY_CAP > 0(承 52 的构建期拒绝表)──
            if (carryCap <= 0)
            {
                errs.Add($"[AC-20-21/A] CARRY_CAP = {carryCap} 非正 —— 容量无意义," +
                         "存在性判据无从求值(GDD :719 安全区间 `> 0`)");
                return errs;   // 无容量 ⇒ 后续判据无输入,不再叠加噪音
            }

            // ── 判据 B:物品表非空(W_MIN 有定义)──
            if (itemWeights == null || itemWeights.Count == 0)
            {
                errs.Add("[AC-20-21/B] 物品表为空 —— W_MIN 无定义,「装得满」这一状态**结构性不可达**" +
                         "(取舍不存在 = 设计缺陷信号,GDD :705-712)");
                return errs;
            }

            // ── 判据 C:存在正重量条目(否则任何件数都加不出重量)──
            int wMin = int.MaxValue;
            int wMax = 0;
            for (int i = 0; i < itemWeights.Count; i++)
            {
                int w = itemWeights[i];
                if (w < 1)
                {
                    errs.Add($"[AC-20-21/C] 物品表第 {i} 条 weight = {w} < 1 —— " +
                             "域错误(21a AC-21a-15);非法重量不得参与可达性构造");
                    continue;
                }
                if (w < wMin) wMin = w;
                if (w > wMax) wMax = w;
            }
            if (wMax < 1)
            {
                errs.Add("[AC-20-21/C] 物品表**无任何** weight ≥ 1 的条目 —— " +
                         "任何领取序列都加不出重量 ⇒ 装满状态不可达");
                return errs;
            }

            // ── 判据 D:构造可达的满状态(真模拟,不是论证)──
            //    用真 `CarryLoadSolver.CanCarry` 而非重写一个 `load + w > cap`。
            var capacity = new CarryCapacity(carryCap);
            long load = 0L;
            int steps = 0;
            int stepBound = (int)Math.Min(int.MaxValue, ((long)carryCap / wMin) + 2);

            while (steps < stepBound)
            {
                // 候选 = 最重的一件(最容易装满;若连它都装不进 ⇒ 已满)。
                if (!CarryLoadSolver.CanCarry(load, wMax, 1, capacity))
                    return errs;   // ✅ 构造成功:找到了可达的「装不下」状态

                load = checked(load + wMax);
                steps++;
            }

            errs.Add($"[AC-20-21/D] 领取 {steps} 件最重物品(weight = {wMax})后仍 `CanCarry == true` " +
                     $"(载重 {load} ≤ cap {carryCap};步数上界 ⌈cap/W_MIN⌉+2 = {stepBound})—— " +
                     "「装满」状态不可达 ⇒ 取舍不存在 = 设计缺陷信号(GDD :705-712)。" +
                     "⚠️ 本判据是**弱门**:领取次数无上界时它几乎恒绿,真正能抓的只有物品表空洞;" +
                     "「cap 大到能带齐一切」须等 OQ-20-9 的具名 ESSENTIAL_KIT 才立得了真门");
            return errs;
        }

        // ───────────────────────── 取数适配器(fail-closed,不做判据)─────────────────────────

        /// <summary>从 <c>inventory_constants.json</c> 文本取 <c>carry_cap</c>。
        /// <para>⚠️ **只接受 JSON 整数形态**(可带负号)。字符串 / 浮点 / null / bool **一律取不出**
        /// ⇒ 返回 false ⇒ 门红。这个失败方向是对的:非整数形态本身就是 AC-20-13 的违例,
        /// 本门不必替它报错,但**绝不能**把它当默认值继续。</para>
        /// </summary>
        public static bool TryReadCarryCap(string json, out int cap, out string error)
        {
            cap = 0; error = null;
            if (string.IsNullOrWhiteSpace(json)) { error = "源文本为空"; return false; }

            Match m = Regex.Match(json, "\"carry_cap\"\\s*:\\s*(-?[0-9]+)(?![0-9.eE])");
            if (!m.Success)
            {
                error = "未找到形如 `\"carry_cap\": <整数>` 的字面量" +
                "(缺失 / 字符串 / 浮点 / null / bool 均落此分支)";
                return false;
            }
            if (!int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Integer,
                              System.Globalization.CultureInfo.InvariantCulture, out cap))
            {
                error = $"carry_cap 字面量 \"{m.Groups[1].Value}\" 越出 int 域";
                return false;
            }
            return true;
        }

        /// <summary>从 <c>item_database_items.json</c> 文本取全部条目的 <c>weight</c>。
        /// <para>⚠️ 完备性哨兵:取到的 weight 条数必须等于 `"ordinal"` 条数
        /// (每个物品条目恒带 ordinal;21a Schema A)。不等 ⇒ 返回 false。
        /// 这挡住「某个条目 weight 写成字符串 ⇒ 静默少一条 ⇒ W_MIN 偏大」的失败方向。</para>
        /// </summary>
        public static bool TryReadItemWeights(string json, out List<int> weights, out string error)
        {
            weights = null; error = null;
            if (string.IsNullOrWhiteSpace(json)) { error = "源文本为空"; return false; }

            int ordinalCount = Regex.Matches(json, "\"ordinal\"\\s*:").Count;
            MatchCollection wm = Regex.Matches(json, "\"weight\"\\s*:\\s*(-?[0-9]+)(?![0-9.eE])");

            if (ordinalCount == 0) { error = "未找到任何 `\"ordinal\"` —— 表结构不符或路径错"; return false; }
            if (wm.Count != ordinalCount)
            {
                error = $"weight 条目数 {wm.Count} ≠ ordinal 条目数 {ordinalCount} —— " +
                        "疑似某条 weight 非整数形态(域错误归 21a 的门)";
                return false;
            }

            var buf = new List<int>(wm.Count);
            for (int i = 0; i < wm.Count; i++)
            {
                if (!int.TryParse(wm[i].Groups[1].Value, System.Globalization.NumberStyles.Integer,
                                  System.Globalization.CultureInfo.InvariantCulture, out int w))
                {
                    error = $"weight 字面量 \"{wm[i].Groups[1].Value}\" 越出 int 域";
                    return false;
                }
                buf.Add(w);
            }
            weights = buf;
            return true;
        }

        /// <summary>把调用方给的根归一化成**仓库根**(含 <c>assets/data/</c> 的那一层)。
        /// <para>规则(全走文件系统实测,不猜):</para>
        /// <list type="number">
        /// <item>null / 空 ⇒ <see cref="ResolveRepoRoot"/>;</item>
        /// <item>该根下直接有 <c>assets/data/</c> ⇒ 就是仓库根,原样返回;</item>
        /// <item>该根是**工程根**(<c>…/unity</c>,有 <c>Assets/</c>)⇒ 上溯一级;</item>
        /// <item>其余 ⇒ 原样返回(让面守卫报「源件不存在」,错误信息指向真实路径)。</item>
        /// </list>
        /// </summary>
        /// <param name="repoRootOrProjectRoot">调用方给的根(可为 null/`"."`/相对/绝对)。</param>
        public static string ResolveDataRoot(string repoRootOrProjectRoot)
        {
            if (string.IsNullOrEmpty(repoRootOrProjectRoot)) return ResolveRepoRoot();

            string probe = Path.GetFullPath(repoRootOrProjectRoot);
            if (Directory.Exists(Path.Combine(probe, "assets", "data"))) return probe;

            // 工程根(`unity/`)上溯一级 = 仓库根。
            if (Directory.Exists(Path.Combine(probe, "Assets")))
            {
                string up = Path.GetDirectoryName(probe);
                if (up != null && Directory.Exists(Path.Combine(up, "assets", "data"))) return up;
            }

            return probe;
        }

        /// <summary>从仓根上溯(与 <c>DiseaseConstantRegistryGate.EntitiesYamlPath</c> 同法)。
        /// <para>⚠️ 只在 <paramref name="repoRoot"/> 未指定时用 —— 测试驱动宜显式传临时根,
        /// 否则夹具会依赖真实仓内容。</para>
        /// </summary>
        private static string ResolveRepoRoot()
        {
            string assetsDir = UnityEngine.Application.dataPath;   // …/unity/Assets
            string unityDir = Path.GetDirectoryName(assetsDir);    // …/unity
            return Path.GetDirectoryName(unityDir);                 // 仓库根
        }
    }
}
