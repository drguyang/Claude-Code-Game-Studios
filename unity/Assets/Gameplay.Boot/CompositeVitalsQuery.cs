// 体征查询的复合门面(2026-11-14 · 「完整可见」轮)
//
// 权威:
//   ADR-005(抽象点之四:`IVitalsQuery` = 全案唯一浮点出口)
//   GDD skeuomorphic-ui.md V-10(敌人读数条:值读既有 VitalsDto)
//
// 为什么需要复合:
//   病人侧 `DiseaseVitalsService.GetVitals` 对**未登记 course 的 id** fail-loud(不返回全零,
//   见其 `:418` 注)。敌人不是 course 持有者 ⇒ 敌人 id 若落到病人侧会**每帧抛**。
//   本门面按「该 id 是否在 `EnemyRoster` 投影里」分派:是 ⇒ 敌人投影;否 ⇒ 病人侧。
//
// ⚠️ 分派依据只有一条 = 投影成员关系,不引入第二份「谁是敌人」的判据。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>病人 / 敌人双域的 `IVitalsQuery` 复合门面。</summary>
    public sealed class CompositeVitalsQuery : IVitalsQuery
    {
        private readonly IVitalsQuery _patients;
        private readonly IVitalsQuery _enemies;
        private readonly Func<IReadOnlyList<EnemyActor>> _rows;

        /// <param name="patients">病人侧(9 的生产实装)。</param>
        /// <param name="enemies">敌人侧(<see cref="EnemyVitalsQuery"/>)。</param>
        /// <param name="rows">敌人投影来源(用于判「该 id 是不是敌人」)。</param>
        public CompositeVitalsQuery(IVitalsQuery patients, IVitalsQuery enemies,
                                    Func<IReadOnlyList<EnemyActor>> rows)
        {
            _patients = patients ?? throw new ArgumentNullException(nameof(patients));
            _enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        /// <inheritdoc />
        public VitalsDto GetVitals(PatientId p)
        {
            IReadOnlyList<EnemyActor> rows = _rows();
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].ActorId == p.Value)
                    return _enemies.GetVitals(p);

            return _patients.GetVitals(p);
        }
    }
}
