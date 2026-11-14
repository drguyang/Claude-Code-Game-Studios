// 敌人伤情 → `VitalsDto` 投影(2026-11-14 · 「完整可见」轮)
//
// 权威:
//   ADR-005(抽象点之四:`IVitalsQuery` / `VitalsDto` = 全案唯一浮点出口)
//   ADR-016 §二(敌人复用 9 的伤情模型;id 经 `IIdAuthority` 与病人共空间)
//   GDD enemy-ai.md 规则二十二(归零 = 不动了,不是死了 —— 档位读数不是「血条」)
//   GDD skeuomorphic-ui.md V-10(敌人读数条;值读既有 VitalsDto)
//
// ═══ 为什么需要本件 ═══════════════════════════════════════════════════════
//   42 的敌人读数条(V-10)要求「值读既有 `VitalsDto`」,而今天 `DiseaseVitalsService.GetVitals`
//   只服务**病人 course**:对未登记的 id **fail-loud**(`PatientCourseNotFoundException` ——
//   全零 DTO 与「真的一点症状没有」不可区分,故不返回默认值)。
//   ⇒ 敌人若直接走 `DiseaseVitalsService`,**每一帧都会抛**。
//   本件给敌人 id 提供同形状的投影:Position = Σ `EnemyInjuryOnset.magnitude`(饱和到 1),
//   Trend = 0(敌人的量是**累计伤量**,不是带符号趋势),signs = 0(敌人无主观词条)。
//
// ⚠️ **档位不塞进 `VitalsDto`**(AC-20 字段白名单):九态档位是**派生态**,
//   本件用 `BandOf` 把 9 的纯分类函数 `StateMachine.ComputeTransition` 暴露给**装配层**,
//   由 42 只读「算好的档」—— 42 侧**不得**自己比较阈值(那会变出第二份真源)。
//
// ⚠️ **本类不写流、不缓存**:与 `EnemyRoster` 同格,每次现算。
//
// ⚠️ **落点 = 边界侧(`Gameplay.Boot`),不是 `Assets/Sim`**(2026-11-14 首跑即红,两条门):
//   1. AC-10-22 静态门:10 的程序集**不得**含 `IVitalsQuery` 实现(`EnemyVitalsQuery` 一度住在
//      `Assets/Sim` ⇒ 10 的程序集被扫出实现体)。
//   2. AC-21a-30 静态门:`Assets/Sim/**` 语法级**零 `float`**(ADR-005 存储零 float)——
//      `ToVitals` 的 `(float)` 转换只能在边界侧写。
//   ⇒ 与 `DiseaseVitalsService` / `VitalsQueryOrNone` 同址(`Sim.Contracts/VitalsDto.cs` 头注:
//   「门面实现住边界侧」)。**不改门借绿**。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>敌人伤情 → `VitalsDto` / 九态档位的投影(纯函数)。</summary>
    public static class EnemyVitalsProjection
    {
        /// <summary>`Position` 的满值 = 1.0(`VitalsDto` 的 [0,1] 域)。</summary>
        public const int FullPositionRaw = 65536;

        /// <summary>
        /// 累计伤量 → 九态档位(9 的纯分类函数;`diseaseKey` 被本函数体忽略,传空串即可)。
        /// <para>⚠️ 档位**不是**「血量百分比」:Healthy / Mild / … / Coma / Deceased 是
        /// 伤情词汇,与 GDD 九态的映射见 `StateMachine.InjuryState` 的订正注。</para>
        /// </summary>
        public static InjuryState BandOf(Fix hurt)
            => StateMachine.ComputeTransition(InjuryState.Healthy, hurt, 0, 0, string.Empty);

        /// <summary>
        /// 累计伤量 → `VitalsDto`(Position 饱和到 [0,1];Trend / signs 恒 0)。
        /// </summary>
        public static VitalsDto ToVitals(Fix hurt)
        {
            long raw = hurt.Raw;
            if (raw < 0) raw = 0;
            if (raw > FullPositionRaw) raw = FullPositionRaw;

            // AC-20:Position 是原始量 [0,1];Trend 是带符号趋势(敌人无此概念 ⇒ 0)。
            return new VitalsDto(raw / (float)FullPositionRaw, 0f, 0, 0);
        }

        /// <summary>一名敌人行 → `(VitalsDto vitals, InjuryState band)`。</summary>
        public static VitalsDto VitalsOf(in EnemyActor actor) => ToVitals(actor.Hurt);

        /// <summary>一名敌人行 → 档位。</summary>
        public static InjuryState BandOf(in EnemyActor actor) => BandOf(actor.Hurt);
    }

    /// <summary>
    /// 敌人 id 的 `IVitalsQuery` 实装(装配层用;按 `EnemyRoster` 的当前投影查)。
    /// <para>⚠️ **未知 id 必须抛**(与 `DiseaseVitalsService` 同款 fail-loud):
    /// 返回全零 = 把「不是敌人」伪装成「健康」,正是 AC-20 前提要挡的形态。</para>
    /// </summary>
    public sealed class EnemyVitalsQuery : IVitalsQuery
    {
        private readonly Func<IReadOnlyList<EnemyActor>> _rows;

        /// <param name="rows">当前敌人投影的来源(每次调用重算 —— 与 `EnemyRoster` 同格)。</param>
        /// <exception cref="ArgumentNullException">rows 为 null。</exception>
        public EnemyVitalsQuery(Func<IReadOnlyList<EnemyActor>> rows)
        {
            _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        /// <inheritdoc />
        /// <exception cref="KeyNotFoundException">id 不在当前敌人投影里(不是敌人 / 尚未生成)。</exception>
        public VitalsDto GetVitals(PatientId p)
        {
            IReadOnlyList<EnemyActor> rows = _rows();
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].ActorId == p.Value)
                    return EnemyVitalsProjection.VitalsOf(rows[i]);

            throw new KeyNotFoundException(
                $"[enemy-vitals] 实体 {p.Value} 不在当前敌人投影里 —— " +
                "敌人读数条只服务遭遇成员;不是敌人的 id 应走病人侧查询。");
        }
    }
}
