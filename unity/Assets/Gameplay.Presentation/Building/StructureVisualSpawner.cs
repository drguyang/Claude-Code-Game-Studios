// 建造槽位视觉的**生成器**(2026-11-10 · T22 批一)—— 与 `PatientVisualSpawner` 同构:
// 「谁 / 在哪」由数据给,「长什么样 / 怎么释放」归 factory。
//
// 权威:ADR-015 §一(视觉层不进 sim)· ADR-009 §四(几何 = 派生态,加载期重建)· ADR-023 §②
// (场景零预摆,运行期生成)· `TD-building-05` 的解除判据(「真包走到灶台能看见可辨识体块」)。
//
// ═══ 与病人视觉的两处刻意不同 ═══════════════════════════════════════════════
// ① **不在 tick 边沿驱动**:建造槽位是**静态几何**(派生态,加载期就定),没有「进场 / 离场」;
//    ⇒ 一次 `SpawnAll` 建完,没有 `OnTickEdge`。挂 tick 只会每帧空转。
// ② **幂等重建** —— `SpawnAll` 先 `DestroyAll` 再建。世界切换(ADR-040)会重装配,
//    重复调用不得叠出第二套体块(叠出来是**静默**的:画面上只是变亮一点)。
//
// ⚠️ 本类**不持游戏状态**:实例表只是 `GameObject` 的簿记,不映射任何 sim 概念;
//   销毁后不会「忘记」什么(下一次 `SpawnAll` 从同一份数据重建,结果逐位相同)。

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Building
{
    /// <summary>按 `WorldBuildSlotsDataSet` 一次性铺出全部槽位的占位体块。</summary>
    public sealed class StructureVisualSpawner
    {
        private readonly StructureVisualFactory _factory;
        private readonly List<GameObject> _visuals = new List<GameObject>();
        private GameObject _root;

        /// <summary>构造。</summary>
        /// <exception cref="System.ArgumentNullException">factory 为 null。</exception>
        public StructureVisualSpawner(StructureVisualFactory factory)
        {
            _factory = factory ?? throw new System.ArgumentNullException(nameof(factory));
        }

        /// <summary>已生成的体块数(<see cref="SpawnAll"/> 之后;销毁后归 0)。</summary>
        public int SpawnedCount => _visuals.Count;

        /// <summary>全部体块的父节点名(夹具与走查按名定位)。</summary>
        public const string RootName = "StructureVisuals";

        /// <summary>
        /// 按行集铺全部体块(幂等:先清后建)。
        /// </summary>
        /// <param name="data">槽位骨架(派生态数据)。</param>
        /// <returns>成功生成的体块数(&lt; 行数 = 有行被 factory 拒绝,见返回值差异)。</returns>
        public int SpawnAll(WorldBuildSlotsDataSet data)
        {
            DestroyAll();

            var rows = data.Rows;
            if (rows == null || rows.Count == 0) return 0;

            // ⚠️ 根节点**懒建**:全部行都是空槽时连根都不该有 ——
            //    一个空 `StructureVisuals` 节点在层级里会被误读成「结构视觉已铺好,只是看不见」。
            GameObject root = null;

            for (int i = 0; i < rows.Count; i++)
            {
                WorldBuildSlotRow row = rows[i];
                // ⚠️ **空槽不画**。`BakedInitial == false` 的含义是「这一格留给你放」,
                // 给它画个体块等于告诉玩家「这里已经站着东西了」—— 而 4 的选择器读到的是
                // 同一个 `baked_initial`,两边一旦不一致,「看得到的」与「选得中的」就分叉了。
                if (!row.BakedInitial) continue;

                GameObject visual = _factory.Create(row.AllowedMask,
                    $"{row.Cell.X}_{row.Cell.Y}_{row.Cell.Z}");
                if (visual == null) continue;   // factory 选择不建(契约允许:底材质取不到)

                if (root == null) { root = new GameObject(RootName); _root = root; }

                // ⚠️ **先挂后定位**(`worldPositionStays: false` 保留局部变换 —— 根在原点、
                //    单位变换,故先设后设在数值上等价;但先挂后设不依赖那条等价性)。
                visual.transform.SetParent(root.transform, false);
                visual.transform.position = CellToWorld(row.Cell, visual.transform.localScale);
                _visuals.Add(visual);
            }

            return _visuals.Count;
        }

        /// <summary>全部体块的父节点(<c>null</c> = 尚未铺 / 已销毁)。
        /// <para>⚠️ 夹具请**用本属性**找体块,不要 `GameObject.Find("StructureVisuals/…")` ——
        /// 后者会命中**别的测试遗留**的同名根(2026-11-10 全量跑实测踩到:单跑 7/7 绿、
        /// 全量跑 3 红,差的就是这一条)。</para></summary>
        public Transform Root => _root != null ? _root.transform : null;

        /// <summary>
        /// 铺容器柜的体块(2026-11-10 · T22 批二)。
        /// <para>⚠️ 与 `SpawnAll` **并列而非合并**:容器不是建造槽位(它有自己的产物
        /// `world_containers.cooked.bytes`),合并会把两条数据源塞进一个签名。</para>
        /// <para>⚠️ **不先清**:容器是要和槽位体块**并存**的。调用顺序 = 先 `SpawnAll`(它清场),
        /// 再 `SpawnContainers`。</para>
        /// </summary>
        /// <param name="data">容器实例行集。</param>
        /// <returns>成功生成的体块数。</returns>
        public int SpawnContainers(WorldContainersDataSet data)
        {
            var rows = data.Containers;
            if (rows == null || rows.Count == 0) return 0;

            int spawned = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                WorldContainerRow row = rows[i];
                GameObject visual = _factory.CreateContainer(row.InstanceId);
                if (visual == null) continue;

                if (_root == null) _root = new GameObject(RootName);
                visual.transform.SetParent(_root.transform, false);
                visual.transform.position = CellToWorld(row.Cell, visual.transform.localScale);
                _visuals.Add(visual);
                spawned++;
            }
            return spawned;
        }

        /// <summary>销毁全部体块(连根;幂等)。</summary>
        public void DestroyAll()
        {
            for (int i = 0; i < _visuals.Count; i++)
                _factory.Destroy(_visuals[i]);   // 释放归 factory(它知道怎么建,就知道怎么释放)
            _visuals.Clear();

            if (_root != null)
            {
                if (!Application.isPlaying) Object.DestroyImmediate(_root);
                else Object.Destroy(_root);
                _root = null;
            }
        }

        /// <summary>释放 factory 自造的材质实例(只在彻底退役时调;`DestroyAll` 不动材质)。</summary>
        public void Dispose() => _factory.Dispose();

        /// <summary>整数格 → 世界坐标:**贴地**(体块中心上抬半个高度)。
        /// <para>⚠️ 1 m/格 的占位期口径与 `PatientVisualSpawner.CellToWorld` 同源
        /// (`StructureVisualFactory.MetersPerCell` 是那两处的共同常量)。</para></summary>
        internal static Vector3 CellToWorld(WorldPos cell, Vector3 scale)
            => new Vector3(cell.X * StructureVisualFactory.MetersPerCell,
                           cell.Y * StructureVisualFactory.MetersPerCell + scale.y * 0.5f,
                           cell.Z * StructureVisualFactory.MetersPerCell);
    }
}
