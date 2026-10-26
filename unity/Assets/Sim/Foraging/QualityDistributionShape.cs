// S20-3 · story-001 —— `quality_distribution[]` 的**形状校验器**(F-17-1 的装载期校验 ①)。
//
// 权威来源:
//   GDD design/gdd/foraging.md
//     · F-17-1 `:297` —— 类型定死为 **int 数组(权重,个数)**(承 D-21-17:weight 是整数
//       最小单位**个数**,移出 `Fix` 解析集);支撑 ⊆ `[1, MAX_QUALITY]`;`Σw ≥ 1`
//     · 装载期校验 ① `:306-309` —— `Σw ≥ 1` 须**硬失败**(显式 throw,非 `Debug.Assert`),
//       否则 `CDFWalk` 除零 / 静默返回最大档
//     · 边界探针 `:397` —— `quality_distribution = [0, 0, 0]` ⇒ 装载期硬失败
//   21a `design/gdd/item-database.md`
//     · §Schema C `:378` —— `quality_distribution` 支撑必须 ⊆ `[1, MAX_QUALITY]`(D-21-7 双向耦合)
//     · §Schema B2 / D-21-16 —— `quality_character[]` 长度 = `MAX_QUALITY`
//   story production/epics/foraging/story-001-variety-closure-and-data-load-validation.md
//     AC-17-08b(`Σw` 全零 ⇒ 硬失败)
//
// ⚠️ **为什么校验器在 17 而数据在 21a**:GDD 六处一致地把 `quality_distribution` 的**数据**
//   归 21a(`gather_profile`),把**形状**归 17(21a 的 `item-database.md:378` 原话:
//   「形状由 17 定」;`GatherProfile.cs` 注释:「本块不持有该类型」)。
//   ⇒ 本文件只做**形状校验**,不持有任何数据、不定义任何表。
//
// ⚠️ **本文件存在的原因 = 一个已登记的实质缺口**:截至 2026-10-26,
//   `assets/data/item_database_items.json` 的四行 `gather_profile` **都不含**
//   `quality_distribution` 字段,而 21a 的 binder(`ItemDatabaseBinder.cs:491-503`)
//   对**该对象的任何字段**都报「未登记字段 —— 形状归系统 17 的 GDD,P0 不得携带」。
//   ⇒ **数据通道全库不存在**:既没写进去、也没读出来。
//   ⇒ 本校验器因此**当前无生产数据可校验**(只有夹具驱动),这是**已登记缺口**,
//     不是本文件的缺陷。缺口归 21a 的 `quality_distribution` 形状落地轮
//     (须同时改:21a binder 白名单 + `GatherProfile` 结构 + 四行数据)。
//   ⚠️ **不得**因为「没有数据」就把本校验器删掉 —— 它承载的 `Σw ≥ 1` 是 GDD `:306-309`
//     点名的**唯一**防线(21a 只校验支撑 ⊆ 域,全零分布**双向皆过**)。
//
// ⚠️ 住 Sim 程序集 `DaYiJingCheng.Sim.Foraging`(门 A:零引擎引用)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Foraging
{
    /// <summary>
    /// `quality_distribution[]` 的**形状校验器**(F-17-1 装载期校验 ① —— `Σw ≥ 1` 等**五条**判据:
    /// null / `MAX_QUALITY ≥ 1` / 长度 = `MAX_QUALITY` / 权重非负 / `Σw ≥ 1`)。
    /// <para>⚠️ 2026-10-26 订正(评审 M-1):原写「四条」,实为五条 —— 计错的是注释,不是执行体;
    /// `maxQuality &lt; 1` 这一条被漏数(见 <see cref="Validate"/> 第 58-60 行)。</para>
    /// <para>纯静态、无状态、无数据 —— 只回答「这个权重数组能不能用来做 CDFWalk」。</para>
    /// </summary>
    public static class QualityDistributionShape
    {
        /// <summary>
        /// 校验一份 `quality_distribution[]`(权重数组)。
        /// </summary>
        /// <param name="weights">各档权重(int 计数,**非 Fix** —— D-21-17)。
        /// 下标 0 对应品级 1(即 `weights[q-1]` = 品级 q 的权重)。</param>
        /// <param name="maxQuality">21a 的 `MAX_QUALITY`(支撑上界;D-21-7 双向耦合)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="weights"/> 为 null。</exception>
        /// <exception cref="ArgumentException">任一条形状判据不满足(逐条具名,便于一轮修完)。</exception>
        public static void Validate(IReadOnlyList<int> weights, int maxQuality)
        {
            if (weights == null)
                throw new ArgumentNullException(nameof(weights),
                    "quality_distribution 为 null —— 缺分布 = CDFWalk 无输入(F-17-1)");
            if (maxQuality < 1)
                throw new ArgumentException(
                    $"MAX_QUALITY = {maxQuality} < 1 —— 21a 的品级上界非法", nameof(maxQuality));

            // 判据 ①:长度 = MAX_QUALITY(D-21-16 的 quality_character 同款长度契约;
            // 分布与定性词表逐档对齐 —— 长度不等会让「第 q 档的权重」与「第 q 档的修饰词」错位)
            if (weights.Count != maxQuality)
                throw new ArgumentException(
                    $"quality_distribution 长度 {weights.Count} ≠ MAX_QUALITY = {maxQuality} —— " +
                    "分布与 quality_character[] 逐档对齐(D-21-16);长度不等 = 档位错位");

            long sum = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                // 判据 ②:权重非负(负权重会让累积和倒退 ⇒ CDFWalk 的单调前提破)
                if (weights[i] < 0)
                    throw new ArgumentException(
                        $"quality_distribution[{i}] = {weights[i]} < 0 —— 权重是计数(D-21-17),负值非法;" +
                        "负权重会让累积和倒退,CDFWalk 的单调前提破");

                // 判据 ③:支撑 ⊆ [1, MAX_QUALITY] 由长度 = MAX_QUALITY + 非负共同蕴含;
                // 此处不另设区间判据(两套判据 = 两处真源)。下标 i 对应品级 i+1 ∈ [1, MAX_QUALITY]。

                sum += weights[i];
            }

            // 判据 ④:Σw ≥ 1(GDD `:306-309` 点名的唯一防线)
            // —— 全零分布:21a 只校验支撑 ⊆ 域,**双向皆过** ⇒ 必须在此硬失败,
            //    否则 CDFWalk 除零 / 静默返回最大档(GDD `:397` 边界探针)
            if (sum < 1)
                throw new ArgumentException(
                    $"quality_distribution 的 Σw = {sum} < 1 —— GDD `:306-309` / `:397`;" +
                    "全零分布会让 CDFWalk 除零或静默返回最大档(21a 只校验支撑 ⊆ 域,全零双向皆过)");
        }

        /// <summary>
        /// 非抛形判定(供需要「先问再决」的调用方;生产装载路径应用 <see cref="Validate"/> 硬失败)。
        /// </summary>
        /// <returns>合法 ⇒ <c>true</c>;<c>false</c> 时 <paramref name="reason"/> 为具名原因。</returns>
        public static bool IsValid(IReadOnlyList<int> weights, int maxQuality, out string reason)
        {
            try
            {
                Validate(weights, maxQuality);
                reason = null;
                return true;
            }
            // ArgumentNullException 派生自 ArgumentException ⇒ 单 catch 已覆盖两者(评审 M-3)。
            // 不写 `ex is ArgumentException || ex is ArgumentNullException` —— 后半恒假,是冗余分支。
            catch (ArgumentException ex)
            {
                reason = ex.Message;
                return false;
            }
        }
    }
}
