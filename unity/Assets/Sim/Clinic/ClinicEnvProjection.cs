// 权威来源:
//   GDD `design/gdd/clinic-machine.md` 规则五(交付)· :340(字段集 = roomName 词表索引 /
//     contexts[] 情境词表索引)· O-24-3 补注(OQ-CP-4 裁定 = 甲:24 → 42 的只读源具名 = ClinicEnvDto)
//   `Sim.Contracts/ClinicEnvDto.cs`(契约;**本文件是它的唯一生产方**)
//   **2026-10-26 用户裁定 ①-C / ②-C / ③-B**(两张显式词表;`ROOM_NONE = 0`)
//
// ⚠️ **为什么本件住 `Sim.Clinic` 而不是表现层**:
//   「把求值结果投成 DTO」是 GDD 规则五(交付)**本身**,属 24 的输出面;
//   它只依赖 Sim 侧类型 + `Sim.Contracts`,零引擎 API ⇒ 住 Sim 侧不破门 A,
//   且 EditMode 可直驱(不需要表现层装配)。
//
// ⚠️ **本件是纯函数,零缓存**:`ToDto` 的输入恰三件(求值快照 + 两张词表),
//   词表是不可变构造产物 ⇒ 同一输入恒给同一输出。缓存归表现层(EC-24-05)。
//
// ⚠️ **查不到编号 ⇒ throw,不降级为 None**:
//   烘焙期的覆盖门(`ClinicMachineTablesBake.Assert{Room,Context}NameCoverage`)已保证
//   「词表 ⊇ 表内出现的全部键」⇒ 运行期查不到只可能是**两份产物不同批**(产物漂移)。
//   此时静默落 None = 把「产物错配」显示成「没有房间」—— 那是本项目反复抓过的静默失效形态。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>24 求值快照 → <see cref="ClinicEnvDto"/> 的投影(交付面的唯一实现)。
    /// </summary>
    public static class ClinicEnvProjection
    {
        /// <summary>把一次求值投影成交付 DTO。</summary>
        /// <param name="evaluation">求值快照(24 的纯输出)。</param>
        /// <param name="roomNames">房间名词表(裁定 ①-C)。</param>
        /// <param name="contextNames">情境词表(裁定 ②-C)。</param>
        /// <returns>DTO(<c>ContextIds</c> 为空数组而非 null —— 「可空集」= 空数组)。</returns>
        /// <exception cref="ArgumentNullException">任一入参为 null(接线错误)。</exception>
        /// <exception cref="InvalidOperationException">求值里的 room_type / context_id 在词表里无编号
        /// —— 两份产物不同批(烘焙覆盖门应已拦),**不降级为 None**。</exception>
        public static ClinicEnvDto ToDto(
            in ClinicEnvEvaluation evaluation,
            ClinicRoomNameTable roomNames,
            ClinicContextNameTable contextNames)
        {
            if (roomNames == null) throw new ArgumentNullException(nameof(roomNames));
            if (contextNames == null) throw new ArgumentNullException(nameof(contextNames));

            int roomNameId = ResolveRoomNameId(evaluation, roomNames);
            int[] contextIds = ResolveContextIds(evaluation, contextNames);

            return new ClinicEnvDto(roomNameId, contextIds, evaluation.EnvMod, evaluation.EquipMod);
        }

        /// <summary>房间名编号:未匹配 ⇒ <see cref="ClinicRoomNameTable.NoneId"/>(哨兵 0);
        /// 匹配 ⇒ 词表编号(缺项即抛)。</summary>
        private static int ResolveRoomNameId(
            in ClinicEnvEvaluation evaluation, ClinicRoomNameTable roomNames)
        {
            // 未匹配 = ROOM_NONE(EC-24-01/02/07 的中性出口)⇒ 哨兵 0。
            // ⚠️ 判据用 `Matched` 而非 `RoomType == null`:`Matched == true` 时 RoomType 必非空
            // (求值器的 matched 分支只在 TryMatchRoom 返回 true 时进入),但反过来把两者
            // 当同义词会让将来「匹配到空名类型」的缺陷静默落 None。
            if (!evaluation.Matched) return ClinicRoomNameTable.NoneId;

            if (evaluation.RoomType == null)
                throw new InvalidOperationException(
                    "[clinic] 求值 Matched=true 但 RoomType 为 null —— 24 内部不一致(不降级为 ROOM_NONE)");

            if (!roomNames.TryGetId(evaluation.RoomType, out int id))
                throw new InvalidOperationException(
                    $"[clinic] room_type「{evaluation.RoomType}」在 ROOM_NAMES 里无编号 —— " +
                    "两份产物不同批(烘焙覆盖门应已拦);拒绝降级为 ROOM_NONE(会把产物错配显示成「没有房间」)");

            return id;
        }

        /// <summary>情境编号集:按求值快照的规范序(ordinal 升序)逐个映射;缺项即抛。</summary>
        private static int[] ResolveContextIds(
            in ClinicEnvEvaluation evaluation, ClinicContextNameTable contextNames)
        {
            var ids = new int[evaluation.ContextIds.Count];
            for (int i = 0; i < ids.Length; i++)
            {
                string contextId = evaluation.ContextIds[i];
                if (contextId == null)
                    throw new InvalidOperationException(
                        $"[clinic] 求值的情境集第 {i} 项为 null —— 24 内部不一致");

                if (!contextNames.TryGetId(contextId, out int id))
                    throw new InvalidOperationException(
                        $"[clinic] context_id「{contextId}」在 CONTEXT_NAMES 里无编号 —— " +
                        "两份产物不同批(烘焙覆盖门应已拦);拒绝静默丢项(会让印卸误判为「不成立」)");

                ids[i] = id;
            }
            return ids;
        }
    }
}
