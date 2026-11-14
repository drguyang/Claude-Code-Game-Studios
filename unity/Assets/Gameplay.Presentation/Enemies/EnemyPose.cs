// 敌人呈现的接缝类型(2026-11-14 · 「完整可见」轮)
//
// 权威:
//   ADR-013 §9 C3(呈现层只渲染,永不持有游戏状态)
//   ADR-016 §二(敌人复用 9 的伤情模型)
//   GDD enemy-ai.md 规则二十二(六态可读 / 归零 = 不动了 + 呼吸层 / 可取材高亮)
//   GDD skeuomorphic-ui.md V-10(敌人读数条:值读既有 VitalsDto)
//
// ═══ 接缝划分(与 `IPatientVisualFactory` 同法)═══════════════════════════════
//   · `IEnemyVisualFactory` —— 「建什么」:占位实现 = 组合体(primitive),真资产来了换实现。
//   · `IEnemyPoseSource`    —— 「站在哪 / 什么态」:今日 = 出生格 + 常量态;
//                              27 的 AI 接上后由它给格与六态,呈现侧**一字不改**。
//   · 本文件只有**值类型与接口**,零 `MonoBehaviour`、零场景依赖(可在 EditMode 夹具里构造)。
//
// ⚠️ 姿态数据是**每帧快照**,不是状态:呈现层不得回写、不得缓存跨帧(同 C3)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Enemies
{
    /// <summary>一帧的敌人姿态(值快照)。</summary>
    public readonly struct EnemyPose
    {
        /// <summary>受伤实体 id(与病人共用 `IIdAuthority` 空间)。</summary>
        public readonly int ActorId;

        /// <summary>遭遇原型(0 = 兵痞 / 1 = 野狗;`EnemyPrototypes` 同值)。</summary>
        public readonly int ProtoId;

        /// <summary>世界坐标(格 → 连续坐标的换算已由来源完成)。</summary>
        public readonly Vector3 Position;

        /// <summary>朝向(绕世界 Y 轴角度;0 = +Z)。</summary>
        public readonly float YawDegrees;

        /// <summary>六态(27 的状态机;今日无 AI ⇒ 恒 <see cref="EnemyStateMachine.State.Patrol"/>)。</summary>
        public readonly EnemyStateMachine.State State;

        /// <summary>是否已倒地(`is_down`;倒地后**零**状态转移、零位移)。</summary>
        public readonly bool Downed;

        /// <summary>倒地后的下游类是否可取材(`down_class = Salvageable`;人形恒 false)。</summary>
        public readonly bool Salvageable;

        /// <summary>出生淡入进度 0–1(0 = 刚出生,1 = 完全出现)。</summary>
        public readonly float Alpha;

        /// <summary>读数条填充 0–1(来自 `VitalsDto.Position`;仅读数条消费)。</summary>
        public readonly float VitalsFill;

        /// <summary>伤情档位(9 的纯分类结果;42 只读文案,不比较阈值)。</summary>
        public readonly InjuryState Band;

        /// <summary>构造一帧姿态。</summary>
        public EnemyPose(int actorId, int protoId, Vector3 position, float yawDegrees,
                         EnemyStateMachine.State state, bool downed, bool salvageable,
                         float alpha, float vitalsFill, InjuryState band)
        {
            ActorId = actorId;
            ProtoId = protoId;
            Position = position;
            YawDegrees = yawDegrees;
            State = state;
            Downed = downed;
            Salvageable = salvageable;
            Alpha = alpha;
            VitalsFill = vitalsFill;
            Band = band;
        }
    }

    /// <summary>敌人在场姿态来源(每帧现算;纯读)。</summary>
    public interface IEnemyPoseSource
    {
        /// <summary>当前场上的敌人姿态(按 `ActorId` 升序;空集 = 场上无敌人)。</summary>
        IReadOnlyList<EnemyPose> Snapshot();
    }

    /// <summary>敌人视觉构造器(决定「长什么样」;释放也归它 —— 知道怎么建就知道怎么拆)。</summary>
    public interface IEnemyVisualFactory
    {
        /// <summary>按原型建一个视觉根(不设位置 —— 位置由驱动随后写入)。返回 null = 不建(契约允许)。</summary>
        GameObject Create(in EnemyPose pose);

        /// <summary>销毁一个视觉根(幂等;null 容忍)。</summary>
        void Destroy(GameObject visual);
    }
}
