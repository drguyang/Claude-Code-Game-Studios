// 序章遭遇编排驱动(2026-11-14)—— 把「剧本点名的一支遭遇」变成 `EncounterStarted`。
//
// 权威:
//   `design/levels/prologue-beat-script.md` §3.1(PA-04 兵痞 1 / PA-05 野狗 2)
//   `PR-27-4`:`EncounterStarted` 写者 = 52(本驱动只做**决定何时/在哪**,写入归
//     `EncounterStartWriter` —— 单点口径不变)
//   ADR-016 §二(id 经 `IIdAuthority` 铸造,与病人共空间)
//
// ⚠️ **为什么是脚本触发而不是 52 随机链**:52 的抽池链在生产上恒 `NoContext`
//   (`TD-randomevents-05`:出诊 / 在馆两个布尔硬编码为 false),再加上池条目表为空
//   ⇒ 端到端不会到「遭遇」这一步。序章是**编排内容**,不是随机内容 ——
//   与 `ScriptedPatientSpawner` 同一条理由(门只约束环境产病人,不约束剧情点名)。
//   52 拥有的是**写特权**(`EncounterStarted` 唯一写者),不是「只能随机产生」。
//
// ⚠️ **一次性**:每支遭遇在同一局内只开一次(读档后由写者的 `encounter_id` 去重兜底,
//   故本类的内存闩锁丢失不产生重复行)。
// ⚠️ **判定用玩家格**(与 ADR-016 §三 / ADR-020 §四 同源):进入矩形 ⇒ 触发。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一支脚本遭遇的声明。</summary>
    public readonly struct EncounterSpawnSpec
    {
        /// <summary>遭遇 id(全图唯一)。</summary>
        public readonly int EncounterId;

        /// <summary>原型 id(<see cref="EnemyPrototypes"/>)。</summary>
        public readonly int ProtoId;

        /// <summary>触发矩形(闭区间;玩家格落入即触发)。</summary>
        public readonly int X0, Z0, X1, Z1;

        /// <summary>成员出生格(每个成员一格;**同格多成员**由渲染侧错开,逻辑上允许)。</summary>
        public readonly IReadOnlyList<WorldPos> Cells;

        /// <summary>构造。</summary>
        public EncounterSpawnSpec(int encounterId, int protoId,
                                  int x0, int z0, int x1, int z1, IReadOnlyList<WorldPos> cells)
        {
            if (encounterId < 0) throw new ArgumentOutOfRangeException(nameof(encounterId));
            if (protoId < 0) throw new ArgumentOutOfRangeException(nameof(protoId));
            if (x1 < x0 || z1 < z0) throw new ArgumentException("触发矩形倒置");
            if (cells == null || cells.Count == 0) throw new ArgumentException("成员出生格不得为空");
            EncounterId = encounterId; ProtoId = protoId;
            X0 = x0; Z0 = z0; X1 = x1; Z1 = z1; Cells = cells;
        }

        /// <summary>该格是否在触发矩形内(闭区间)。</summary>
        public bool Contains(int x, int z) => x >= X0 && x <= X1 && z >= Z0 && z <= Z1;
    }

    /// <summary>序章两支脚本遭遇的编排驱动。</summary>
    public sealed class PrologueEncounterDriver
    {
        /// <summary>P0 的两支(序章):药铺废墟 1 兵痞 · 七里路 2 野狗。</summary>
        public static IReadOnlyList<EncounterSpawnSpec> PrologueTwo => new[]
        {
            new EncounterSpawnSpec(1, EnemyPrototypes.Thug, 20, 4, 27, 12,
                new[] { new WorldPos(23, 0, 7) }),
            new EncounterSpawnSpec(2, EnemyPrototypes.WildDog, 32, 14, 95, 25,
                new[] { new WorldPos(60, 0, 19), new WorldPos(61, 0, 20) }),
        };

        private readonly EncounterStartWriter _writer;
        private readonly IIdAuthority _ids;
        private readonly IReadOnlyList<EncounterSpawnSpec> _specs;
        private readonly Func<WorldPos> _playerCell;
        private readonly bool[] _fired;
        private long _lastTick = long.MinValue;

        /// <param name="writer">遭遇开启写者(52;唯一 Append 点)。</param>
        /// <param name="ids">发号器(敌人 id 与病人共空间,ADR-016 §二)。</param>
        /// <param name="specs">遭遇表(P0 = <see cref="PrologueTwo"/>)。</param>
        /// <param name="playerCell">玩家**经流确立**格。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public PrologueEncounterDriver(EncounterStartWriter writer, IIdAuthority ids,
                                       IReadOnlyList<EncounterSpawnSpec> specs,
                                       Func<WorldPos> playerCell)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _specs = specs ?? throw new ArgumentNullException(nameof(specs));
            _playerCell = playerCell ?? throw new ArgumentNullException(nameof(playerCell));
            _fired = new bool[specs.Count];
        }

        /// <summary>已开出的遭遇 id(观测面)。</summary>
        public IReadOnlyList<int> StartedIds
        {
            get
            {
                var list = new List<int>();
                for (int i = 0; i < _fired.Length; i++)
                    if (_fired[i]) list.Add(_specs[i].EncounterId);
                return list;
            }
        }

        /// <summary>每 tick 调一次(玩家格落入矩形 ⇒ 开一支)。</summary>
        /// <param name="tick">当前逻辑 tick(≥ 0)。</param>
        public void OnTickEdge(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (tick == _lastTick) return;
            // tick 回退 = 读档:清闩锁重试(写者的 encounter_id 去重兜住重复行)。
            if (tick < _lastTick) Array.Clear(_fired, 0, _fired.Length);
            _lastTick = tick;

            WorldPos cell = _playerCell();
            for (int i = 0; i < _specs.Count; i++)
            {
                if (_fired[i]) continue;
                EncounterSpawnSpec spec = _specs[i];
                if (!spec.Contains(cell.X, cell.Z)) continue;

                var actorIds = new int[spec.Cells.Count];
                for (int a = 0; a < actorIds.Length; a++)
                    actorIds[a] = _ids.NextPatientId().Value;

                if (_writer.TryStart(spec.EncounterId, spec.ProtoId, spec.Cells[0], actorIds, tick)
                    != EncounterStartResult.RejectInvalidArgument)
                    _fired[i] = true;
            }
        }
    }
}
