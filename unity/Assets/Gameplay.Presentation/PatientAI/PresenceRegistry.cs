// O-2 修复(2026-10-09)—— IPresenceQuery 的生产实装(在场登记簿)。
//
// 权威来源:
//   IPresenceQuery 头注(提供方 = 表现层注入;sim 侧只读整数在场标志)
//   ADR-008 §六(在场才模拟)· AC-15(EventStream 有界性读 PresentCount / IsPresent)
//   PatientSpatialDirector / PresentPatientsView 的「在场册由 9 侧重新灌入」义务注
//   ADR-009 §一(在场集 = 派生态:由进出登记重建,不落盘不写流)
//   ADR-016 §二(玩家 / 敌人 / 病人共用同一实体 id 空间)
//
// 语义(评审 BLOCKING #1 修正 · 2026-10-09 甲案:占格集与在场集分离):
//   - **在场集** `_present` = 在场**病人** ⇒ PresentCount = AC-15 CAP 的分母;
//     由组合层(9 的出现 / 6 的 chunk 激活)经 AddPresent / RemovePresent 灌入;
//   - **占格集** `_cellOf` / `_occupiedCells` = 任意实体(病人 + 玩家 + 敌人)⇒ IsPresentAt;
//     玩家 / 敌人经 **SetOccupant / RemoveOccupant** 写入,**不入在场集、不计 CAP**
//     (否则玩家吃掉 24 个病人名额,或 PlaceableChecker 条件⑥ 对玩家恒 false);
//   - 病人经 AddPresent / RemovePresent / MovePresent(同时维护两集);
//   - ⚠️ 病人与玩家 / 敌人的写面**勿混用**(对病人调 RemoveOccupant 会留下
//     「在场但无格」态;对玩家调 AddPresent 会虚耗 CAP)。
//
// ⚠️ 本件**不自行推导**在场集(那是第二真源)—— 谁在场由组合层说了算,本件只是登记簿。
//    13 侧的 PresentPatientsView / PatientSpatialDirector 经 PresentChanged 事件同步
//    (防组合层双写漂移 —— 评审 ADVISORY #6)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>在场登记簿 —— <see cref="IPresenceQuery"/> 的生产实装(O-2)。
    /// <para>写面给组合层;读面给 sim(EventStream 的 AC-15 有界性等)。
    /// 在场 = 病人(计 CAP);占格 = 任意实体(IsPresentAt,不计 CAP)。</para></summary>
    public sealed class PresenceRegistry : IPresenceQuery
    {
        private readonly HashSet<int> _present = new HashSet<int>();
        private readonly Dictionary<int, WorldPos> _cellOf = new Dictionary<int, WorldPos>();
        private readonly HashSet<WorldPos> _occupiedCells = new HashSet<WorldPos>();

        /// <summary>在场集变更(进 / 出)通知,载荷 = 实体 id。
        /// <para>组合层订阅以同步 13 侧视图(PresentPatientsView / PatientSpatialDirector),
        /// 避免「只调了登记簿、漏调视图」的双写漂移(评审 ADVISORY #6)。
        /// 幂等重复 Add / 不存在的 Remove / MovePresent **不触发**。</para></summary>
        public event Action<int> PresentChanged;

        /// <summary>病人是否在场(AC-15 有界性检查读此)。</summary>
        public bool IsPresent(PatientId patientId) => _present.Contains(patientId.Value);

        /// <summary>当前在场病人数(AC-15 与 PATIENT_APPEARANCE_CAP 比较;玩家 / 敌人不计)。</summary>
        public int PresentCount => _present.Count;

        /// <summary>指定格上是否有实体(玩家 / 敌人 / 病人)。</summary>
        public bool IsPresentAt(WorldPos cell) => _occupiedCells.Contains(cell);

        /// <summary>病人进入在场范围(组合层调用;幂等:重复进入不叠加、**不改格**
        /// —— 跨格须走 <see cref="MovePresent"/>;评审 ADVISORY A2 钉死此语义)。</summary>
        public void AddPresent(PatientId id, WorldPos cell)
        {
            if (!_present.Add(id.Value)) return; // 幂等:已在场则不重复占格
            Occupy(id.Value, cell);
            PresentChanged?.Invoke(id.Value);
        }

        /// <summary>病人离开在场范围(组合层调用;出册 + 让格)。</summary>
        public void RemovePresent(PatientId id)
        {
            if (!_present.Remove(id.Value)) return;
            VacateCell(id.Value);
            PresentChanged?.Invoke(id.Value);
        }

        /// <summary>病人跨格(组合层在 ActorCellEntered 等时机调用)。</summary>
        public void MovePresent(PatientId id, WorldPos toCell)
        {
            if (!_present.Contains(id.Value)) return; // 不在场则忽略(禁隐式入场)
            if (!_cellOf.TryGetValue(id.Value, out var fromCell)) return;
            if (fromCell.Equals(toCell)) return;      // 自格短路(评审 B1:防独占格被误让)

            MoveCell(id.Value, fromCell, toCell);
        }

        /// <summary>玩家 / 敌人占格(组合层调用;**不入在场集 ⇒ 不计 CAP**,
        /// 评审 BLOCKING #1 甲案)。已占格时 = 跨格(让旧格);未占格时 = 入册占格。</summary>
        public void SetOccupant(int id, WorldPos cell)
        {
            if (_cellOf.TryGetValue(id, out var old))
            {
                if (old.Equals(cell)) return;
                MoveCell(id, old, cell);
                return;
            }
            Occupy(id, cell);
        }

        /// <summary>玩家 / 敌人离格(组合层调用;只让格,不动在场集 —— 本就不在)。</summary>
        public void RemoveOccupant(int id) => VacateCell(id);

        /// <summary>加载后重置(承 PresentPatientsView.ResetForLoad 同型:派生态重新灌入)。</summary>
        public void ResetForLoad()
        {
            _present.Clear();
            _cellOf.Clear();
            _occupiedCells.Clear();
        }

        /// <summary>入册占格(id 必须不在 _cellOf)。</summary>
        private void Occupy(int id, WorldPos cell)
        {
            _cellOf[id] = cell;
            _occupiedCells.Add(cell);
        }

        /// <summary>出册让格:仅当无他人占同格才让出(同格多实体是合法态)。</summary>
        private void VacateCell(int id)
        {
            if (!_cellOf.TryGetValue(id, out var cell)) return;
            _cellOf.Remove(id); // 评审 A1:必须除册,否则残留幽灵占格 ⇒ 格永不释放
            if (!CellOccupiedByOther(id, cell))
                _occupiedCells.Remove(cell);
        }

        /// <summary>跨格:占新格 + 仅当旧格无他人时让出(评审 B2:共享格守卫)。</summary>
        private void MoveCell(int id, WorldPos from, WorldPos to)
        {
            _cellOf[id] = to;
            _occupiedCells.Add(to);
            if (!CellOccupiedByOther(id, from))
                _occupiedCells.Remove(from);
        }

        private bool CellOccupiedByOther(int selfId, WorldPos cell)
        {
            foreach (var kv in _cellOf)
                if (kv.Key != selfId && kv.Value.Equals(cell)) return true;
            return false;
        }
    }
}
