// 权威来源:GDD `random-events.md` 规则二(池条目 schema,`:138-150`)· 规则十一(P0 逐档内容)
//          · ADR-039(运行期消费)· ADR-016 §一(决策可重建:病种必须是**定值**外键,不得运行时随机挑)
//          · `TD-randomevents-03`(52 事件 → 37 病种的映射)
//
// ⚠️ **本类存在的理由**:三个闭包(`requiresPatient` / `canSpawnPatient` / `spawnPatient`)
//    改前是 `CompositionRoot` 里的内联 lambda。内联 lambda **无法被夹具直接执行**
//    ⇒ 夹具只能**照抄一遍**同样的判据 ⇒ 生产侧改坏了夹具照样绿(自造断言顶替 AC 语义层)。
//    提成具名纯函数后,装配与夹具**共用同一份**,分叉只可能发生在「有没有调用它」那一个点上。
//
// ⚠️ `spawnPatient` 里的 `presence.AddPresent` **不可省**:`EventStream.Append` 的 AC-15
//    在场检查会拒收未登记的病人 ⇒ 省掉它则链报 `Spawned`、流里有 `DiseaseOnset`,
//    但 13 看不见、9 不 Step、37 不开案:**链「成功」而世界毫无变化**。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>52 池表 → 病人出现的三条判据(装配与夹具的**单一出处**)。</summary>
    public static class EventPoolPatientSpawner
    {
        /// <summary>「这条要不要病人」——`disease_id &gt; 0` 即要。</summary>
        /// <remarks>
        /// ⚠️ **查不到 key 时返回 `true`(保守)** —— 宁可让下游拒(具名零写入),
        /// 不可悄悄少一个病人(那是静默的数据缺失)。
        /// </remarks>
        public static Func<int, bool> RequiresPatient(EventPoolDataSet pool)
        {
            if (pool.Entries == null) throw new ArgumentNullException(nameof(pool));
            return key =>
            {
                int id = pool.DiseaseIdOfKey(key);
                if (id == EventPoolDataSet.KeyAbsent) return true;
                return id > 0;
            };
        }

        /// <summary>「这条**能不能**产病人」——池表给了病种外键才可以。</summary>
        /// <param name="pool">池表数据集。</param>
        /// <param name="isDiseaseAdmissible">投放资格(T4 · `PatientDrawEligibility.Decide` 的
        /// `Admitted` 位,按病种 id 求值)。<c>null</c> = 未接线 ⇒ 不拦
        /// (与 `ITreatableByLookup` 的 null 语义同源)。
        /// <para>⚠️ 与 `<c>disease_id &gt; 0</c>` 是**两道独立判据**,不合并:
        /// 前者问「这条池条目有没有指定病种」,后者问「那个病种此刻能不能投放」。
        /// 合并后「池表漏填外键」与「技法未解锁」会有同一个返回值,归因就断了。</para></param>
        public static Func<int, bool> CanSpawnPatient(EventPoolDataSet pool,
                                                      Func<int, bool> isDiseaseAdmissible = null)
        {
            if (pool.Entries == null) throw new ArgumentNullException(nameof(pool));
            return key =>
            {
                int diseaseId = pool.DiseaseIdOfKey(key);
                if (diseaseId <= 0) return false;
                return isDiseaseAdmissible == null || isDiseaseAdmissible(diseaseId);
            };
        }

        /// <summary>产病人:按池表外键取病种 → 创建 → **登记在场**。</summary>
        /// <param name="spawnCore">真正的创建器(生产 = `PatientSpawner.SpawnNext`;
        /// 夹具可换成记录器 —— 但判据本身仍由本方法持有)。</param>
        /// <param name="presence">在场写面(AC-15 前置,见文件头)。</param>
        /// <param name="anchorOf">生成锚点(位置解析归 6;P0 = 具名占位)。</param>
        public static Func<int, long, PatientId> SpawnPatient(
            EventPoolDataSet pool,
            Func<int, long, PatientId> spawnCore,
            IPresenceWriter presence,
            Func<long, WorldPos> anchorOf)
        {
            if (pool.Entries == null) throw new ArgumentNullException(nameof(pool));
            if (spawnCore == null) throw new ArgumentNullException(nameof(spawnCore));
            if (presence == null) throw new ArgumentNullException(nameof(presence));
            if (anchorOf == null) throw new ArgumentNullException(nameof(anchorOf));

            return (key, tick) =>
            {
                int diseaseId = pool.DiseaseIdOfKey(key);
                // 上游 `CanSpawnPatient` 应已拦下;走到这里说明两处判据分叉(同源禁令)。
                if (diseaseId <= 0)
                    throw new InvalidOperationException(
                        $"[52] spawnPatient 被要求为 key={key} 产病人,但该条目的 disease_id={diseaseId}");
                PatientId spawned = spawnCore(diseaseId, tick);
                presence.AddPresent(spawned, anchorOf(tick));
                return spawned;
            };
        }
    }
}
