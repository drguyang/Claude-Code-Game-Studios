// 权威来源:design/gdd/skeuomorphic-ui.md V-10(敌人读数条 · 黄铜侧)
//   · production/epics/skeuomorphic-ui/story-017-enemy-vitals-bar.md
//   · AC-42-F3(黄铜侧读数条走蚀刻刻度)· AC-42-D3(42 不持 DTO 副本)
//
// 设计说明:
//   · 触发 / 状态归 27:`EncounterStarted` / `EncounterEnded` + **六态机**(27 的状态机);
//     42 只把「现在是哪个态、该出现还是淡出」映射成面片的一处观感。
//   · 值经既有 `VitalsDto`(`Position`),读取即弃,不缓存、不解释战力。
//   · 禁传统血条形态 —— 黄铜侧读数走**离散蚀刻槽**(见 `EnemyVitalsBillboard`)。
//   · 2026-11-14:「完整可见」轮把本骨架接上世界空间渲染体(`EnemyVitalsBillboard`)。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>敌人读数条(V-10)—— 黄铜侧哑光面片,只渲染不持状态。</summary>
    public sealed class EnemyVitalsBar
    {
        private readonly EnemyVitalsBillboard _view;

        /// <param name="actorId">实体 id(仅用于视觉根命名)。</param>
        public EnemyVitalsBar(int actorId)
        {
            _view = new EnemyVitalsBillboard(actorId);
        }

        /// <summary>是否可见(由 27 的遭遇态驱动,42 不自主决定)。</summary>
        public bool Visible { get; set; }

        /// <summary>当前六态(27 的六态机映射;42 只读不推进)。</summary>
        public EnemyState CurrentState { get; set; } = EnemyState.Healthy;

        /// <summary>渲染一次读数(值来自 `VitalsDto.Position`;读取即弃,不缓存)。</summary>
        /// <param name="position">读数 0–1。</param>
        /// <param name="bandText">档位文案(27 的词汇;由装配层给出)。</param>
        /// <param name="downed">是否倒地(压暗但仍可见 —— 归零 ≠ 已清除)。</param>
        public void RenderReading(float position, string bandText, bool downed)
        {
            // 读取即用 —— 不落字段,避免 AC-42-D3 的 DTO 副本
            _view.Apply(bandText, position, downed);
        }

        /// <summary>逐帧朝相机并求恒定屏幕尺寸(F6)。</summary>
        public void FaceCamera(Camera camera) => _view.FaceCamera(camera);

        /// <summary>面片的世界坐标(由驱动写入)。</summary>
        public Vector3 WorldPosition
        {
            get => _view.WorldPosition;
            set => _view.WorldPosition = value;
        }

        /// <summary>销毁(幂等)。</summary>
        public void Destroy() => _view.Destroy();
    }

    /// <summary>
    /// 敌人六态(27 的六态机 → 读数条渲染形态的**边界镜像**;42 侧只读呈现)。
    /// <para>⚠️ 值必须与 27 的六态机逐值对齐;42 的程序集不引用 Sim 的 AI 侧
    /// (ADR-013 分层),故在边界处镜像,由装配层负责映射。</para>
    /// </summary>
    public enum EnemyState
    {
        Healthy = 0,
        Injured = 1,
        Staggered = 2,
        Downed = 3,
        Unconscious = 4,
        Dead = 5,
    }
}
