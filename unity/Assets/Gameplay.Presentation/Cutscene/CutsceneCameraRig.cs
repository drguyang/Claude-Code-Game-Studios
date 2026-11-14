// 过场演出的**独立机位**(N3 · 2026-11-14)。
//
// 权威:`design/levels/prologue-beat-script.md` §2 相机行 ——
//   「过场期间用**独立机位**;**不得**用 `CameraMode` 的五档」
//   (Explore / Casebook / Treatment / Downed 是玩法档位,拿它演过场 = 让玩法档位承载叙事)。
//
// ═══ 与 `ICameraRig` 的关系 ═══════════════════════════════════════════════
//   **零关系**。本类不实现 `ICameraRig`、不读 `CameraMode`、不改主相机的任何字段:
//   它自己建一台 `Camera`(depth 高于主相机 + 纯黑清屏),播出期间**盖住**主相机;
//   播完 `End()` 关掉自己,主相机原样接管。⇒ 演出结束不需要「恢复」任何东西
//   (没有状态被改写过,就没有状态会忘还原)。
//
// ═══ 时间基准 ═════════════════════════════════════════════════════════════
//   运镜是**补间**(每帧平滑),按墙钟 `deltaTime` 推进 —— 与 `FadeInOutDriver` 同口径;
//   「该不该发生」的时序仍归 `CutscenePlayer`(tick)。本类只回答「第几秒的机位在哪」。
//
// ═══ 姿态 ═════════════════════════════════════════════════════════════════
//   绕焦点的**圆弧**(半径 / 高度常量,俯视),`smoothstep` 缓入缓出:
//   `Begin(focus, startYaw, sweep, duration)` ⇒ 从 `startYaw` 扫过 `sweep` 度。
//   ⚠️ 刻意不做路径编辑 / 关键帧 —— 灰盒期一条弧就够;真运镜设计归关卡走查后再定。

using System;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Cutscene
{
    /// <summary>过场专用的一台相机:圆弧运镜,播完自动关闭。</summary>
    public sealed class CutsceneCameraRig : IDisposable
    {
        /// <summary>相机物体名(走查 / 夹具按名定位)。</summary>
        public const string ObjectName = "CutsceneCamera";

        private GameObject _root;
        private readonly float _radius;
        private readonly float _height;

        private readonly float _depth;

        private Vector3 _focus;
        private float _startYawDegrees;
        private float _sweepDegrees;
        private float _durationSeconds;
        private float _elapsedSeconds;

        /// <summary>本机位的相机(`null` = 已销毁)。</summary>
        public UnityEngine.Camera Camera { get; private set; }

        /// <summary>是否在播。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>构造(建好相机,但**先关着** —— 没在演出时它不该渲染)。</summary>
        /// <param name="radius">绕焦点的水平半径(米)。</param>
        /// <param name="height">相机离地高度(米)。</param>
        /// <param name="depth">渲染深度(**须高于主相机**;主相机默认 0)。</param>
        /// <exception cref="ArgumentOutOfRangeException">半径 / 深度 ≤ 0,或高度 &lt; 0。</exception>
        public CutsceneCameraRig(float radius = 10f, float height = 6f, float depth = 100f)
        {
            if (radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius), radius, "半径须 > 0");
            if (height < 0f) throw new ArgumentOutOfRangeException(nameof(height), height, "高度不得为负");
            if (depth <= 0f) throw new ArgumentOutOfRangeException(nameof(depth), depth, "深度须 > 0(须盖住主相机)");

            _radius = radius;
            _height = height;
            _depth = depth;
            CreateRoot();
        }

        /// <summary>开播:吸附到起始姿态,然后由 <see cref="Step"/> 推进。</summary>
        /// <param name="focus">环绕中心(世界坐标;通常 = 帐篷格 + 胸口高度)。</param>
        /// <param name="startYawDegrees">起始方位角(0° = 正南向,按 `sin/cos` 口径)。</param>
        /// <param name="sweepDegrees">扫过的角度(正 = 顺时针;半圈 ≈ 180)。</param>
        /// <param name="durationSeconds">时长(秒;须 &gt; 0)。</param>
        /// <exception cref="ArgumentOutOfRangeException">时长 ≤ 0。</exception>
        /// <exception cref="InvalidOperationException">已经在播(重叠 = 先 <see cref="End"/>)。</exception>
        public void Begin(Vector3 focus, float startYawDegrees, float sweepDegrees, float durationSeconds)
        {
            if (durationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), durationSeconds, "时长须 > 0");
            if (IsPlaying)
                throw new InvalidOperationException("过场机位已经在播 —— 重叠须先 End()");
            if (_root == null) CreateRoot();   // 世界切换销毁过 ⇒ 自愈(同一 BootRoot 内可能再演一场)

            _focus = focus;
            _startYawDegrees = startYawDegrees;
            _sweepDegrees = sweepDegrees;
            _durationSeconds = durationSeconds;
            _elapsedSeconds = 0f;

            IsPlaying = true;
            _root.SetActive(true);
            Camera.enabled = true;
            Apply(0f);
        }

        /// <summary>推进一帧(墙钟秒)。没在播 ⇒ 纯 no-op(可以每帧无条件调)。</summary>
        /// <param name="deltaSeconds">本帧时长(秒;≤ 0 不推进)。</param>
        public void Step(float deltaSeconds)
        {
            if (!IsPlaying || deltaSeconds <= 0f) return;

            _elapsedSeconds += deltaSeconds;
            float t = Mathf.Clamp01(_elapsedSeconds / _durationSeconds);
            Apply(t);
            if (t >= 1f) End();
        }

        /// <summary>停播(关相机;**幂等**)。播完 / 中断都走这里。</summary>
        public void End()
        {
            IsPlaying = false;
            if (Camera != null) Camera.enabled = false;
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>销毁(连物体;幂等,双路径同全仓纪律)。</summary>
        public void Dispose()
        {
            IsPlaying = false;
            if (_root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
            else UnityEngine.Object.DestroyImmediate(_root);
            Camera = null;
        }

        private void CreateRoot()
        {
            _root = new GameObject(ObjectName);
            Camera = _root.AddComponent<UnityEngine.Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Color.black;
            Camera.depth = _depth;
            Camera.enabled = false;
            _root.SetActive(false);
        }

        private void Apply(float t)
        {
            float eased = t * t * (3f - 2f * t);   // smoothstep:两端速度为 0
            float yaw = (_startYawDegrees + _sweepDegrees * eased) * Mathf.Deg2Rad;
            Vector3 position = _focus + new Vector3(Mathf.Sin(yaw) * _radius, _height, Mathf.Cos(yaw) * _radius);
            Camera.transform.position = position;
            Camera.transform.LookAt(_focus);
        }
    }
}
