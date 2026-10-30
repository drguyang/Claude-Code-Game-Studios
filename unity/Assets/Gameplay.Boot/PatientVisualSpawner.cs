// sprint-05 T2.0 · 病人最小可见实体(2026-10-10)
//
// 权威来源:
//   ADR-016 §二(13 病人 AI 出只读视图 IPresentPatients;13 不引用 37)
//   ADR-009 §一(在场集 = 派生态:由进出登记重建,不落盘不写流)
//   ADR-013 §9 C3(呈现层只渲染不持状态)
//   ADR-015 §三(`WorldPos` 单一整数格;格 → 连续坐标换算归表现层)
//   playtest-2026-10-10 §6(核心循环 NOT-RUN:病人运行期不可见 ⇒ 人工面 0% 覆盖)
//
// 设计口径(最小可见实体,P0 接线载体):
//   - 驱动 = 每 tick 边沿对比 PresenceRegistry 在场集与已 spawn 的视觉实体字典,
//     新增病人 ⇒ 让 factory 建一个视觉根,摆到 anchor 给的格;本驱动**不删实体**
//     (离场归 6 的 chunk 激活)。
//   - 视觉形态 = **`IPatientVisualFactory`**(占位实现 = 胶囊 + 中性灰)。
//   - 位置 = **`IPatientVisualAnchorSource`**(占位实现 = 围绕玩家的环)。
//   - 颜色 = 中性灰(零语义;不映射体征/病种/九态 —— 那是 13 的 `Material(p)` 与 8 的词表)。
//
// ⚠️ 本类**只读** `IPresenceQuery`,不写流、不持游戏状态(承 BootRoot 同纪律)。
//
// 🔁 **2026-10-30 重构(`TD-prescription-06` 的「灰盒做好,等真实资产兑换」轮)**:
//   原版本把「建什么」(胶囊 + 中性灰材质)与「建在哪」(围绕玩家的环)两件**写死在本类**。
//   真资产来了要换的是两件:①视觉模型(归 13 / 美术轮)②位置来源(归 6 世界生态区 +
//   13 空间侧)。而位置还被 `InteractionInputDriver.NearestPresentPatient` 消费
//   ⇒ 写死意味着**换资产要改三处**(本类 + 交互驱动 + 夹具)。
//
//   ⇒ 现结构:**本类降级为纯编排**,两件各抽一个接缝 ——
//     · `IPatientVisualFactory`(住表现层,签名必须出现 `GameObject`)
//     · `IPatientVisualAnchorSource`(住 `Sim.Contracts`,只用 BCL 类型 `WorldPos`)
//   ⇒ **兑换真资产 = 在 `CompositionRoot` 换掉两行 `new`**,本类 / 交互 / 夹具**一字不动**。
//
//   ⚠️ **位置换算留在本类**(格 → 连续坐标 + `y` 偏移)是刻意的:承 ADR-015 §三
//   「空间真值一律整数格,换算归表现层」⇒ 真位置与占位环**共用同一条换算**,
//   兑换时只有锚点来源变,换算逻辑不动。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Patients;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 病人最小可见实体驱动 —— 给运行期病人生成视觉实体(sprint-05 T2.0)。
    /// <para><b>只渲染,不持游戏状态</b>(ADR-013 §9 C3 同纪律):病人在场真源 = sim 侧
    /// `PresenceRegistry`,本类只按在场集 spawn/维持视觉实体。</para>
    /// <para><b>纯编排</b>(2026-10-30 重构):「建什么」归 <see cref="IPatientVisualFactory"/>,
    /// 「建在哪」归 <see cref="IPatientVisualAnchorSource"/>。本类只负责两者的**时序**与**簿记**。</para>
    /// </summary>
    public sealed class PatientVisualSpawner
    {
        /// <summary>视觉实体在 Hierarchy 下的父节点名(便于 playtest 走查)。</summary>
        private const string VisualRootName = "PatientVisuals";

        /// <summary>视觉实体的世界坐标 y 偏移(让视觉根底部贴地,不穿模)。</summary>
        private const float VisualYOffset = 1.0f;

        private readonly IPresenceQuery _presence;
        private readonly IPatientVisualFactory _factory;
        private readonly IPatientVisualAnchorSource _anchors;
        private readonly Dictionary<int, GameObject> _visuals = new Dictionary<int, GameObject>();

        /// <summary>视觉实体根节点(懒建)—— 全部病人视觉的父节点,<see cref="DestroyAll"/> 连根清。
        /// <para>⚠️ **不挂 BootRoot 之下**:PlayMode 各测试的 teardown 只显式销毁自己知道的对象,
        /// 靠层级继承会被漏(2026-10-10 全量跑实测:前一测试残留 5 个实体 ⇒ 断言 1 ≠ 6)。
        /// 生产路径同理会漏(Boot 场景卸载时视觉不下来)⇒ 由本类自持根节点 + BootRoot
        /// <c>OnDestroy</c> 显式调 <see cref="DestroyAll"/>。</para></summary>
        private GameObject _visualRoot;

        /// <param name="presence">在场查询(病人在场真源的唯一合法入口)。</param>
        /// <param name="factory">视觉构造器(决定「长什么样」)。</param>
        /// <param name="anchors">锚点来源(决定「站在哪一格」)。</param>
        public PatientVisualSpawner(IPresenceQuery presence,
                                    IPatientVisualFactory factory,
                                    IPatientVisualAnchorSource anchors)
        {
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
        }

        /// <summary>
        /// tick 边沿驱动 —— 对比在场集与视觉字典,为新病人 spawn 视觉实体。
        /// </summary>
        /// <param name="tick">本边沿自己的逻辑 tick(由帧泵回推,非末 tick)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");

            foreach (var patientId in _presence.PresentPatientIds())
            {
                if (_visuals.ContainsKey(patientId))
                    continue;

                // ⚠️ 无锚点 ⇒ **不 spawn**(不是「spawn 到原点」)。
                //    占位实现恒返回 true,故该分支今天不可达;真实锚点会用到它
                //    (病人未进入世界 / 无空间分配)。如实登记为「今日不可达」,不假装测过。
                if (!_anchors.TryGetAnchorCell(patientId, out WorldPos cell))
                    continue;

                GameObject visual = _factory.Create(patientId);
                if (visual == null) continue;   // factory 返回 null = 它选择不建(契约允许)

                // 格 → 连续坐标(承 ADR-015 §三:换算归表现层,不进契约面)
                visual.transform.position = CellToWorld(cell);

                // 挂到自持根节点(见 _visualRoot 头注:teardown 显式连根清)
                visual.transform.SetParent(VisualRoot.transform, false);

                _visuals[patientId] = visual;
            }
        }

        /// <summary>整数格 → 世界连续坐标(含贴地 y 偏移)。
        /// <para>⚠️ 占位期取 **1 m/格**(真值归 ADR-015 §三 的世界格定义)。
        /// 真资产兑换时若格边长不同,只改这一个常量。</para></summary>
        private static Vector3 CellToWorld(WorldPos cell)
            => new Vector3(cell.X, cell.Y + VisualYOffset, cell.Z);

        /// <summary>销毁全部视觉实体(连根;PlayMode 测试 teardown 用)。</summary>
        public void DestroyAll()
        {
            foreach (var kv in _visuals)
                _factory.Destroy(kv.Value);   // 释放归 factory(它知道怎么建,就知道怎么释放)
            _visuals.Clear();

            if (_visualRoot != null)
            {
                UnityEngine.Object.Destroy(_visualRoot);
                _visualRoot = null;
            }
        }

        /// <summary>视觉根节点(懒建;全部病人视觉的父节点)。</summary>
        private GameObject VisualRoot
        {
            get
            {
                if (_visualRoot == null)
                {
                    _visualRoot = new GameObject(VisualRootName);
                }
                return _visualRoot;
            }
        }

        /// <summary>当前已 spawn 的视觉实体数(测试接缝 —— 可证伪「病人生成了脸」)。</summary>
        public int VisualCount => _visuals.Count;

        /// <summary>
        /// 病人视觉实体位置查询(sprint-07 S2.2 接线面 · 只读观测面)—— 占位环位置;
        /// 未 spawn / 已清 ⇒ false。⚠️ 占位(归 6 世界生态区 + 13 空间侧真格)。
        /// </summary>
        public bool TryGetVisualPosition(int patientId, out Vector3 position)
        {
            if (_visuals.TryGetValue(patientId, out var go) && go != null)
            {
                position = go.transform.position;
                return true;
            }
            position = default;
            return false;
        }
    }
}
