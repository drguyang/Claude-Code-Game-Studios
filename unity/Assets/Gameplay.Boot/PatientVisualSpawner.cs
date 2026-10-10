// sprint-05 T2.0 · 病人最小可见实体(2026-10-10)
//
// 权威来源:
//   ADR-016 §二(13 病人 AI 出只读视图 IPresentPatients;13 不引用 37)
//   ADR-009 §一(在场集 = 派生态:由进出登记重建,不落盘不写流)
//   ADR-013 §9 C3(呈现层只渲染不持状态)
//   playtest-2026-10-10 §6(核心循环 NOT-RUN:病人运行期不可见 ⇒ 人工面 0% 覆盖)
//
// 设计口径(最小可见实体,P0 接线载体):
//   - 驱动 = 每 tick 边沿对比 PresenceRegistry 在场集与已 spawn 的视觉实体字典,
//     新增病人 ⇒ spawn 一个占位胶囊体;本驱动**不删实体**(离场归 6 的 chunk 激活)。
//   - 视觉形态 = GameObject.CreatePrimitive(Capsule)(零资产依赖;归 cape 前的占位)。
//   - 位置 = 出生点在玩家附近(接线占位;真实位置归 6 世界生态区 + 13 病人 AI 空间侧)。
//   - 颜色 = 中性灰(零语义;不映射体征/病种/九态 —— 那是 13 的 Material(p) 与 8 的词表)。
//
// ⚠️ 本类**只读** IPresenceQuery,不写流、不持游戏状态(承 BootRoot 同纪律)。
// ⚠️ 不引入 prefab / 不用 Addressables(接线期零资产;prefab 归 44/表现层实现轮)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 病人最小可见实体驱动 —— 给运行期病人生成占位视觉(sprint-05 T2.0)。
    /// <para><b>只渲染,不持游戏状态</b>(ADR-013 §9 C3 同纪律):病人在场真源 = sim 侧
    /// <see cref="PresenceRegistry"/>,本类只按在场集 spawn/维持视觉实体。</para>
    /// </summary>
    public sealed class PatientVisualSpawner
    {
        /// <summary>视觉实体在 Hierarchy 下的父节点名(便于 playtest 走查)。</summary>
        private const string VisualRootName = "PatientVisuals";

        /// <summary>占位胶囊体的世界坐标 y 偏移(让胶囊底部贴地,不穿模)。</summary>
        private const float CapsuleYOffset = 1.0f;

        /// <summary>出生点围绕玩家的半径(m;接线占位 —— 真实位置归 6 + 13)。</summary>
        private const float SpawnRingRadius = 2.5f;

        private readonly IPresenceQuery _presence;
        private readonly Dictionary<int, GameObject> _visuals = new Dictionary<int, GameObject>();

        /// <summary>视觉实体根节点(懒建)—— 全部病人视觉的父节点,<see cref="DestroyAll"/> 连根清。
        /// <para>⚠️ **不挂 BootRoot 之下**:PlayMode 各测试的 teardown 只显式销毁自己知道的对象,
        /// 靠层级继承会被漏(2026-10-10 全量跑实测:前一测试残留 5 个实体 ⇒ 断言 1 ≠ 6)。
        /// 生产路径同理会漏(Boot 场景卸载时视觉不下来)⇒ 由本类自持根节点 + BootRoot
        /// <c>OnDestroy</c> 显式调 <see cref="DestroyAll"/>。</para></summary>
        private GameObject _visualRoot;

        /// <param name="presence">在场查询(病人在场真源的唯一合法入口)。</param>
        public PatientVisualSpawner(IPresenceQuery presence)
        {
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        }

        /// <summary>
        /// tick 边沿驱动 —— 对比在场集与视觉字典,为新病人 spawn 占位胶囊体。
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

                // 出生点:围绕玩家按 id 稳布(纯函数确定性;禁 UnityEngine.Random)
                Vector3 anchor = SpawnAnchorFor(patientId);
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = $"PatientVisual_{patientId}";
                var collider = capsule.GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.Destroy(collider);   // 占位实体不吃物理
                capsule.transform.position = anchor;
                capsule.transform.localScale = new Vector3(0.6f, 1.0f, 0.6f);

                // 挂到自持根节点(见 _visualRoot 头注:teardown 显式连根清)
                capsule.transform.SetParent(VisualRoot.transform, false);

                // 中性灰 —— 零语义(不映射体征/病种;那归 13 Material(p) + 8 词表)
                var mat = new Material(Shader.Find("Standard")) { color = new Color(0.7f, 0.7f, 0.7f) };
                capsule.GetComponent<Renderer>().sharedMaterial = mat;

                _visuals[patientId] = capsule;
            }
        }

        /// <summary>
        /// 病人出生锚点(接线占位):围绕玩家按 id 稳布于半径 SpawnRingRadius 的环上。
        /// <para>⚠️ **纯函数确定性** —— 同 id 恒同点(禁 PRNG,承 ADR-016 §一 三源不变量);
        /// 真实位置归 6 世界生态区 + 13 病人 AI 的空间侧(LogiPose / HomeRegion)。</para>
        /// </summary>
        private static Vector3 SpawnAnchorFor(int patientId)
        {
            // 用稳定哈希把 id 折到 [0, 2π):SplitMix64 是 sim 侧哈希源,表现层可读(纯函数)
            ulong h = DaYiJingCheng.Sim.Contracts.SplitMix64.Hash(patientId, 0);
            float angle = (float)(h % 100000UL) / 100000f * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * SpawnRingRadius;
            float z = Mathf.Sin(angle) * SpawnRingRadius;

            var player = GameObject.Find("Player");
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            return new Vector3(origin.x + x, origin.y + CapsuleYOffset, origin.z + z);
        }

        /// <summary>销毁全部视觉实体(连根;PlayMode 测试 teardown 用)。</summary>
        public void DestroyAll()
        {
            foreach (var kv in _visuals)
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value);
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
    }
}
