// 「这个病人此刻能不能出现」的**生产判据装配** —— T18 乙(2026-11-10)
//
// 权威来源:
//   design/gdd/reviews/technique-unlock-map-2026-11-09.md §5.1 / §5.2(技法门 · 钩子病例)
//   `PatientDrawEligibility.Decide`(判据本体)· `HookCasePolicy`(钩子准入)· `HookCaseLedger`(章内配额)
//   ADR-043 §D7(解锁查询 = 病史流折叠)· ADR-016 §一(判据的全部输入 ∈ 三源之纯函数)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件补的是什么断点(T17 实测登记,`impl-t17-e2e-day-2026-11-10.md` §6.1)
// ─────────────────────────────────────────────────────────────────────────────
//   「投放资格」的**判据本体**早就有名字(`PatientDrawEligibility` / `HookCasePolicy` /
//   `HookCaseLedger`),但把它们**接起来**的那三段(钩子配额怎么算 · 等级传什么 ·
//   玩家未绑定时怎么办)此前是 `CompositionRoot` 里的**内联 λ**,没有落点:
//
//       Func<DiseaseRegistryEntry, PatientDrawDecision> admitEntry = row => { … };
//       Func<int, bool> isDiseaseAdmissible = id => … admitEntry(candidate).Admitted;
//
//   ⇒ 任何夹具想问「生产里这个病此刻会不会出现」,都只能**照抄一遍**那段 λ。
//     照抄的生产逻辑一旦改坏,夹具照样绿 —— 这正是 `EventPoolPatientSpawner` 类头注
//     点名的「自造断言顶替 AC 语义层」形态。T17 的端到端夹具明知如此也只能照抄,
//     并把差异写进证据件;本件把那处差异**收掉**。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么是「一个类」而不是「一个静态函数」
// ─────────────────────────────────────────────────────────────────────────────
//   三个输入是**装配期建成、运行期只读**的引用(索引 / 解锁查询 / 玩家绑定槽),
//   另一个(章内钩子配额)每次判定现算。把它们打包成实例,调用方拿到的就是一个
//   `Func<DiseaseRegistryEntry, PatientDrawDecision>` —— 与既有 `admit:` 形参同形,
//   **零接口变更**,装配层只是换了个取函数的地方。
//
// ⚠️ 本类**不缓存**任何判定结果:解锁状态随事件流变化,缓存会引入「何时失效」这个
//    新判据(与 `TechniqueUnlockProjection` 头注同一立场)。
//
// ⚠️ 本类**不写流、不发事件** —— 纯查询。写者仍分别归 9(`DiseaseOnset`)/ 52(`EventArrived`)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 病人投放资格的**生产装配**(技法门 + 等级门 + 钩子放行 + 章内配额)。
    /// </summary>
    /// <remarks>
    /// <para>装配期建一次,运行期只读地判定;判据本体全部在
    /// <see cref="PatientDrawEligibility.Decide"/> 与 <see cref="HookCasePolicy"/> 里,
    /// 本类只负责**把它们按生产的口径接起来**。</para>
    /// </remarks>
    public sealed class PatientAdmissionPolicy
    {
        private readonly TechniqueDrawIndex _index;
        private readonly Func<int, bool> _isTechniqueUnlocked;
        private readonly PlayerActorBinding _playerActor;
        private readonly HookCaseLedger _hookCases;
        private readonly int _prescriptionLevel;

        /// <summary>装配一次。</summary>
        /// <param name="index"><c>treatable_by</c> 反向索引;<c>null</c> = 未接线(无数据 provider)⇒
        /// 判定一律 <see cref="PatientDrawVerdict.NotWired"/>(放行),与接线前逐位相同。</param>
        /// <param name="isTechniqueUnlocked">「玩家会不会这门技法」的谓词(生产 = 流折叠投影)。
        /// 玩家未绑定期间调用方自己会短路,但本类仍按实参判定 —— 与
        /// <see cref="PatientDrawEligibility.Decide"/> 的 <c>playerBound</c> 参数分家。</param>
        /// <param name="playerActor">玩家 id 的迟绑定槽(**同一个实例** —— 装配期建、玩家生成后绑定一次)。</param>
        /// <param name="hookCases">钩子病例账本(章内计数);<c>null</c> = 未接线 ⇒ `hookAllowed` 恒 false,
        /// 钩子层整体不生效(与 T4 的门逐位相同)。</param>
        /// <param name="playerPrescriptionLevel">「处方用药」等级(P0 = 装配常量;真等级归 30)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="isTechniqueUnlocked"/> 或
        /// <paramref name="playerActor"/> 为 null。
        /// <para>⚠️ 刻意**不**容忍 null 谓词(与 `PatientDrawEligibility.Decide` 同款):
        /// 装配漏了谓词应当当场炸,而不是让所有病种静默走「未接线放行」。</para></exception>
        public PatientAdmissionPolicy(
            TechniqueDrawIndex index,
            Func<int, bool> isTechniqueUnlocked,
            PlayerActorBinding playerActor,
            HookCaseLedger hookCases,
            int playerPrescriptionLevel)
        {
            _index = index;   // 可 null = 未接线(见参数说明)
            _isTechniqueUnlocked = isTechniqueUnlocked ?? throw new ArgumentNullException(nameof(isTechniqueUnlocked));
            _playerActor = playerActor ?? throw new ArgumentNullException(nameof(playerActor));
            _hookCases = hookCases;   // 可 null = 未接线
            _prescriptionLevel = playerPrescriptionLevel;
        }

        /// <summary>
        /// 判定一个病种此刻能否作为病人出现(生产口径)。
        /// </summary>
        /// <param name="row">注册表行(非 null)。</param>
        /// <returns>判定结果;`Admitted` 位是唯一「能出现」的依据。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="row"/> 为 null。</exception>
        /// <remarks>
        /// <para>钩子放行**只在技法全锁这一种拒收上生效**,且要过章内配额与可诊断性 ——
        /// 判据本体在 <see cref="HookCasePolicy.AllowsInjection"/>,本方法只负责取配额。</para>
        /// <para>⚠️ 玩家未绑定时**不判配额**(那时不知道按谁的解锁算),`hookAllowed` 保持 false。</para>
        /// </remarks>
        public PatientDrawDecision Decide(DiseaseRegistryEntry row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            bool hookAllowed = false;
            if (_hookCases != null && _index != null && _playerActor.IsBound
                && _index.IsDeclared(row.DiseaseKey))
            {
                // 章内口径(`ChapterAdvanced` 立在 B1-b);无生产者时章恒 0 ⇒ 本章 = 整局。
                hookAllowed = HookCasePolicy.AllowsInjection(
                    row, _hookCases.CountHookCasesInCurrentChapter(_playerActor.ActorId));
            }

            return PatientDrawEligibility.Decide(
                row, _index, _prescriptionLevel,
                _isTechniqueUnlocked, _playerActor.IsBound,
                asHookCase: false, hookAllowed: hookAllowed);
        }

        /// <summary>
        /// 52 路径的适配形:池表只给**病种 id**,所以要先查表再判定。
        /// </summary>
        /// <param name="registryById">病种 id → 注册表行(装配层的同一张表,不另建)。</param>
        /// <returns>`id → 该病种此刻能否出现`。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="registryById"/> 为 null。</exception>
        /// <remarks>
        /// <para>⚠️ **查不到的 id ⇒ 放行**(`true`):那不是投放门能判的对象,而下游 9 的
        /// 「病种不在注册表内」fail-loud 会接手 —— 在此判否则会把**配置错误**伪装成
        /// 「今天没有病人」。</para>
        /// </remarks>
        public Func<int, bool> DiseaseAdmissible(IReadOnlyDictionary<int, DiseaseRegistryEntry> registryById)
        {
            if (registryById == null) throw new ArgumentNullException(nameof(registryById));

            return diseaseId =>
                !registryById.TryGetValue(diseaseId, out DiseaseRegistryEntry row) || Decide(row).Admitted;
        }
    }
}
