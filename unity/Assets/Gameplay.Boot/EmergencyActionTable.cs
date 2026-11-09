// 急救**动作表** —— 按 `ActionId` 取行(T13 · 2026-11-09)
//
// 权威:`design/gdd/emergency-procedures.md` 10-DC(动作表 schema · `EmergencyActionRow`)
//       · `technique-unlock-map-2026-11-09.md` §7 T7(动作 2/3 的 id 空间已开)
//       · ADR-005(主机唯一 Append)· AC-10-16(`ResultMul` 三档 = schema 默认表)
//
// ── 为什么要有这张表(病灶实测)─────────────────────────────────────────────
// 本件之前,`EmergencyManualDriver` 把动作 0 的行写成**私有静态字段**,并在结算尾
// **无条件**用它:
//
//     _processor.Process(attempt, BandageRow, ctx, patientId, tick, ...);
//
// 而 `attempt.Action` 是**真的会写进事件流**的字段(`EmergencyAttemptPayload.Action`)。
// ⇒ 只要有任何一条路径把 `Action` 设成 1/2/3,判定就会用**止血包扎的参数**
//   (potency 1.0 / 幅度门 50 / 半衰期 100 …),而流里记的是「清创缝合」或「蛇伤处置」。
//   **事件与判定各说各话,且完全静默。**
//
// 今天不可达(两个入口都硬编码 `action: 0`:`InteractionInputDriver.cs:189` 与
// `EmergencyManualDriver.TryManual` 的 `action: 0`)—— 所以它是**哑的隐患**,不是活的 bug。
// 但它正好卡在下一步上:T7 已经开了动作 2/3 的 id 空间,谁下一个去「给清创加一行数据」,
// 会**加完什么都不发生**,因为行根本不会被查(行是静态字段,不是按 id 取的)。
//
// ── 本件只改取行方式,不动任何数值 ──────────────────────────────────────────
// `ProductionDefault` 里动作 0 的参数与搬迁前**逐字段相同**(含 `ResultMul` 仍引用
// `EmergencyActionSchema.GetResultMulTiers()`,不写副本 —— AC-10-16 / `OQ-10-1`)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.EmergencyProcedures;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>急救动作表:动作 id → <see cref="EmergencyActionRow"/>。</summary>
    public sealed class EmergencyActionTable
    {
        private readonly Dictionary<int, EmergencyActionRow> _byActionId;
        private readonly int[] _ids;

        /// <summary>构造。</summary>
        /// <param name="rows">动作行集(每行的 <see cref="EmergencyActionRow.ActionId"/> 为键)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="rows"/> 为 null。</exception>
        /// <exception cref="ArgumentException">
        /// 某行为 null / `ActionId` 为负 / **同一 id 出现两次**。
        /// <para>重复 id 必须构造期拒收:后者静默覆盖后者,「这个动作用的哪一行」就取决于
        /// 迭代序 —— 那是典型的不可重放形态。</para></exception>
        public EmergencyActionTable(IEnumerable<EmergencyActionRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var map = new Dictionary<int, EmergencyActionRow>();
            foreach (EmergencyActionRow row in rows)
            {
                if (row == null)
                    throw new ArgumentException("动作表里出现 null 行", nameof(rows));
                if (row.ActionId < 0)
                    throw new ArgumentException(
                        "动作行的 ActionId 不得为负(实得 " + row.ActionId + ")", nameof(rows));
                if (map.ContainsKey(row.ActionId))
                    throw new ArgumentException(
                        "动作 id " + row.ActionId + " 出现两次 —— 重复会让「用哪一行」取决于迭代序(不可重放)",
                        nameof(rows));
                map[row.ActionId] = row;
            }

            _byActionId = map;
            _ids = new int[map.Count];
            map.Keys.CopyTo(_ids, 0);
            Array.Sort(_ids);
        }

        /// <summary>已注册的动作 id(升序)。</summary>
        public IReadOnlyList<int> RegisteredActionIds => _ids;

        /// <summary>已注册的动作数。</summary>
        public int Count => _byActionId.Count;

        /// <summary>查一行;无 ⇒ <c>false</c>(不抛,供「先问有没有」的调用方)。</summary>
        public bool TryGet(int actionId, out EmergencyActionRow row)
            => _byActionId.TryGetValue(actionId, out row);

        /// <summary>
        /// 查一行;**无 ⇒ 抛**。
        /// </summary>
        /// <param name="actionId">动作 id(= `EmergencyAction` 序数)。</param>
        /// <exception cref="InvalidOperationException">该动作没有表行。</exception>
        /// <remarks>
        /// ⚠️ **必须抛,不得回退动作 0**。回退正是本类要消灭的那个形态:
        /// 判定用 A 的参数、事件记 B 的动作,且没有任何报错。
        /// 今天生产表只有动作 0 —— 动作 1/2/3 走到这里就是**响亮的拒绝**,不是静默的错判。
        /// </remarks>
        public EmergencyActionRow Require(int actionId)
        {
            if (_byActionId.TryGetValue(actionId, out EmergencyActionRow row)) return row;

            throw new InvalidOperationException(
                "[10][动作表] 动作 " + actionId + " 没有表行 —— 判定该用谁的参数?没有答案 ⇒ 拒收。" +
                "已注册的动作 = {" + string.Join(",", _ids) + "}。" +
                "要启用这个动作,须先给它一行 EmergencyActionRow(T7 已开 id 空间;具体数值归数值轮)。");
        }

        /// <summary>
        /// 生产默认表:今日**只有动作 0(止血包扎)**。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ **动作 0 的参数与搬迁前逐字段相同** —— 本类只改「怎么取行」,不动任何数值。</para>
        /// <para>`ResultMul` 引用 <see cref="EmergencyActionSchema.GetResultMulTiers"/>,
        /// **禁在此另写副本**(AC-10-16 三档 `{65536, 32768, 16384}`;`OQ-10-1` 2026-09-18 已裁定非零)。
        /// 历史上这里曾有手写副本并漂移,判据 =
        /// `Tests/EditMode/EmergencyProcedures/emergency_result_mul_production_drift_test.cs`。</para>
        /// </remarks>
        public static EmergencyActionTable ProductionDefault { get; } =
            new EmergencyActionTable(new[]
            {
                new EmergencyActionRow
                {
                    ActionId = 0,
                    Polarity = 2,        // 平
                    BasePotency = 65536, // 1.0 in Q16.16
                    MagThreshold = 50,
                    MinEdges = 1,
                    MinHoldTicks = 5,
                    HalfLifeTicks = 100,
                    DurationTicks = 200,
                    ResultMul = EmergencyActionSchema.GetResultMulTiers(),
                },
            });
    }
}
