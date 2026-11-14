// 敌人位姿桥(2026-11-14 · 「完整可见」轮)
//
// 权威:
//   GDD enemy-ai.md 规则二十二(六态可读 / 归零 = 不动了 + 呼吸层 / 可取材高亮)
//   GDD enemy-ai.md §Visual 二①(2026-11-14 裁定:倒地野兽 = 墨色轮廓微光,低频呼吸)
//   ADR-013 §9 C3(呈现层只渲染)
//   ADR-015 §三(整数格 → 连续坐标换算归表现层)
//
// ═══ 本件做什么 ═══════════════════════════════════════════════════════════
//   每帧把一帧 `EnemyPose` 落到 Transform 与材质属性上:
//   · 位置 / 朝向
//   · 六态各一档姿态(Patrol / Alert / Chase / Flank / Engage / Disengage)
//   · 倒地 = 躺倒 + 低频呼吸起伏(按原型两套:人形侧卧 / 野兽侧翻)
//   · 出生淡入 0.3s(⚠️ 实现 = 颜色从背景墨色插值 + 轻微缩放,**不是** alpha 混合 ——
//     占位材质是不透明 URP Lit;换真资产时可改回真 alpha,本类只在 `ApplyAlpha` 一处)
//   · 倒地野兽可取材 = 墨色轮廓微光(低频呼吸;**禁**数字/进度/悬停)
//
// ⚠️ **不持游戏状态**:`Apply` 只读快照;时钟只用于呼吸相位(表现态,他不是游戏真值)。
// ⚠️ **六态姿态今天是死代码的诚实形态**:生产上 27 的 AI 尚未接线 ⇒ 恒 Patrol。
//   姿态档在这里**可证伪地**存在(EditMode 夹具逐态断言),不是「写了但没人读」的假绿。

using DaYiJingCheng.Sim;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Enemies
{
    /// <summary>敌人视觉根的位姿驱动(挂在视觉根上;由 factory 配置部件)。</summary>
    public sealed class EnemyPoseRig : MonoBehaviour
    {
        /// <summary>倒地时的呼吸频率(Hz 量级;低频)。</summary>
        private const float DownBreathRate = 2.2f;

        /// <summary>存活时的轻微起伏频率。</summary>
        private const float AliveBreathRate = 1.6f;

        /// <summary>淡入时长(秒)。</summary>
        public const float FadeInSeconds = 0.3f;

        /// <summary>淡入起点缩放(从 0.85 张到 1.0)。</summary>
        private const float FadeStartScale = 0.85f;

        /// <summary>淡入起点的墨色(背景墨;不透明插值,见头注)。</summary>
        private static readonly Color InkShadow = new Color(0.12f, 0.11f, 0.10f, 1f);

        /// <summary>可取材微光的墨色基色。</summary>
        private static readonly Color SalvageInk = new Color(0.20f, 0.18f, 0.15f, 1f);

        private Transform _poseRoot;
        private Transform _torso;
        private Transform _head;
        private Transform _armL;
        private Transform _armR;
        private Transform _legL;
        private Transform _legR;
        private Transform _tail;

        private Renderer[] _bodyRenderers;
        private Color[] _baseColors;
        private Renderer[] _outlineRenderers;

        private MaterialPropertyBlock _mpb;
        private Vector3 _torsoBaseScale;
        private Vector3 _headBaseLocal;
        private float _clock;
        private bool _isBeast;

        /// <summary>由 factory 配置部件(只调一次;重复调 = 编程错误)。</summary>
        public void Configure(bool isBeast, Transform poseRoot,
                              Transform torso, Transform head,
                              Transform armL, Transform armR,
                              Transform legL, Transform legR, Transform tail,
                              Renderer[] bodyRenderers, Color[] baseColors,
                              Renderer[] outlineRenderers)
        {
            _isBeast = isBeast;
            _poseRoot = poseRoot;
            _torso = torso;
            _head = head;
            _armL = armL; _armR = armR; _legL = legL; _legR = legR;
            _tail = tail;
            _bodyRenderers = bodyRenderers ?? new Renderer[0];
            _baseColors = baseColors ?? new Color[0];
            _outlineRenderers = outlineRenderers ?? new Renderer[0];
            _mpb = new MaterialPropertyBlock();
            _torsoBaseScale = torso != null ? torso.localScale : Vector3.one;
            _headBaseLocal = head != null ? head.localPosition : Vector3.zero;
        }

        /// <summary>应用一帧姿态。</summary>
        /// <param name="pose">快照(只读)。</param>
        /// <param name="deltaTime">表现时钟步长(秒;≤0 ⇒ 不推进呼吸相位)。</param>
        public void Apply(in EnemyPose pose, float deltaTime)
        {
            if (deltaTime > 0f) _clock += deltaTime;

            transform.position = pose.Position;
            transform.rotation = Quaternion.Euler(0f, pose.YawDegrees, 0f);

            ApplyStatePose(pose);
            ApplyBreath(pose);
            ApplyAlpha(pose.Alpha);
            ApplySalvageGlow(pose);
        }

        // ── 六态姿态 ──────────────────────────────────────────────────────
        private void ApplyStatePose(in EnemyPose pose)
        {
            if (_poseRoot == null) return;

            // 倒地优先于六态:倒地后**零状态转移**(GDD 规则二十二),姿态只由 `Downed` 决定。
            if (pose.Downed)
            {
                _poseRoot.localRotation = _isBeast
                    ? Quaternion.Euler(80f, 0f, 0f)     // 野兽侧翻
                    : Quaternion.Euler(0f, 0f, 90f);    // 人形侧卧
                _poseRoot.localPosition = new Vector3(0f, _isBeast ? 0.30f : 0.25f, 0f);
                SetLimbs(armL: 6f, armR: -6f, legL: 4f, legR: -4f);
                return;
            }

            _poseRoot.localPosition = Vector3.zero;
            _poseRoot.localRotation = Quaternion.identity;

            float bob = Mathf.Sin(_clock * AliveBreathRate) * 0.015f;
            _poseRoot.localPosition = new Vector3(0f, bob, 0f);

            switch (pose.State)
            {
                case EnemyStateMachine.State.Alert:
                    // 停步、抬头、双臂微张
                    SetLimbs(armL: 28f, armR: -28f, legL: 0f, legR: 0f);
                    if (_head != null) _head.localPosition = _headBaseLocal + new Vector3(0f, 0.05f, 0f);
                    break;

                case EnemyStateMachine.State.Chase:
                    // 前倾、摆臂、跨步
                    _poseRoot.localRotation = Quaternion.Euler(15f, 0f, 0f);
                    SetLimbs(armL: 38f, armR: -38f, legL: 18f, legR: -18f);
                    break;

                case EnemyStateMachine.State.Flank:
                    // 侧身、外侧臂展开
                    _poseRoot.localRotation = Quaternion.Euler(0f, 0f, 10f);
                    SetLimbs(armL: 55f, armR: -10f, legL: 0f, legR: 0f);
                    break;

                case EnemyStateMachine.State.Engage:
                    // 压低重心、双臂前伸
                    _poseRoot.localPosition = new Vector3(0f, -0.05f + bob, 0f);
                    SetLimbs(armL: -65f, armR: 65f, legL: 8f, legR: -8f);
                    break;

                case EnemyStateMachine.State.Disengage:
                    // 后退、松臂
                    _poseRoot.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                    SetLimbs(armL: 4f, armR: -4f, legL: -12f, legR: 12f);
                    break;

                default: // Patrol
                    SetLimbs(armL: 8f, armR: -8f, legL: 0f, legR: 0f);
                    break;
            }
        }

        private void SetLimbs(float armL, float armR, float legL, float legR)
        {
            if (_armL != null) _armL.localRotation = Quaternion.Euler(0f, 0f, armL);
            if (_armR != null) _armR.localRotation = Quaternion.Euler(0f, 0f, armR);
            if (_legL != null) _legL.localRotation = Quaternion.Euler(legL, 0f, 0f);
            if (_legR != null) _legR.localRotation = Quaternion.Euler(legR, 0f, 0f);
            if (_tail != null) _tail.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        }

        // ── 呼吸起伏(倒地也必须在 —— 归零 ≠ 死亡)────────────────────────
        private void ApplyBreath(in EnemyPose pose)
        {
            if (_torso == null) return;

            float amplitude = pose.Downed ? 0.030f : 0.008f;
            float rate = pose.Downed ? DownBreathRate : AliveBreathRate;
            float s = 1f + amplitude * Mathf.Sin(_clock * rate);

            _torso.localScale = new Vector3(
                _torsoBaseScale.x * s,
                _torsoBaseScale.y,
                _torsoBaseScale.z * s);
        }

        // ── 出生淡入(颜色插值 + 缩放;见头注)──────────────────────────────
        private void ApplyAlpha(float alpha)
        {
            float a = Mathf.Clamp01(alpha);
            transform.localScale = Vector3.one * Mathf.Lerp(FadeStartScale, 1f, a);

            if (_mpb == null || _bodyRenderers == null) return;
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                if (_bodyRenderers[i] == null) continue;
                Color baseColor = i < _baseColors.Length ? _baseColors[i] : Color.gray;
                _mpb.SetColor("_BaseColor", Color.Lerp(InkShadow, baseColor, a));
                _bodyRenderers[i].SetPropertyBlock(_mpb);
            }
        }

        // ── 可取材微光(仅倒地野兽;低频呼吸)────────────────────────────────
        private void ApplySalvageGlow(in EnemyPose pose)
        {
            bool show = pose.Downed && pose.Salvageable;
            if (_outlineRenderers != null)
                for (int i = 0; i < _outlineRenderers.Length; i++)
                    if (_outlineRenderers[i] != null)
                        _outlineRenderers[i].enabled = show;

            if (!show || _mpb == null || _outlineRenderers == null) return;

            float pulse = 0.55f + 0.45f * Mathf.Sin(_clock * DownBreathRate);
            Color c = Color.Lerp(SalvageInk, SalvageInk * 1.8f, pulse);
            c.a = 1f;
            for (int i = 0; i < _outlineRenderers.Length; i++)
            {
                if (_outlineRenderers[i] == null) continue;
                _mpb.SetColor("_BaseColor", c);
                _outlineRenderers[i].SetPropertyBlock(_mpb);
            }
        }
    }
}
