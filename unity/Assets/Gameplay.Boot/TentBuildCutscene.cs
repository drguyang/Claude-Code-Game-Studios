// 帐篷搭建演出(N3 的**调用方** · 2026-11-14)—— 「放置成功」到「五件生长 + 镜头绕半圈」。
//
// 权威:`design/levels/prologue-beat-script.md` P3-02b
//   (「逐件生长(五件组件依次出现)→ 镜头绕半圈 → 解冻」)
//   §2 相机行(过场用**独立机位**;不得借用 `CameraMode` 五档)
//
// ═══ 为什么触发在**输入驱动**而不是节拍表 ═════════════════════════════════
//   节拍表里 P3-02b 的判据是「帐篷真的落了流」(`StructurePlaced` 计数 ≥ 1)——
//   读档**重放**同一条事件也会让它成立。若演出挂在节拍上,读一次档就重演一次搭建。
//   演出的正确触发是「**这一次**按下 Enter 且成功」:输入驱动的 `PlaceAttemptCount`
//   增量 + `LastPlaceResult == Success` 恰好就是这件事,而且不会在重放时出现。
//
// ═══ 时长 ═══════════════════════════════════════════════════════════════
//   五件 × 24 tick(1.2 s @ 20 Hz)= 120 tick 逐件生长;
//   镜头 60 tick(3 s)绕半圈(与实际运镜时长同值,两处只改一处会不同步 ——
//   故二者**同源**:`OrbitTicks * TICK_SECONDS`)。
//   ⇒ 总时长 180 tick = 9 s;期间世界冻结(承 `ModalInputFreeze` 的 `IsPlaying` 合流)。
//
// ⚠️ 五件生长各 0.45 s **短于**一拍(1.2 s):拍与拍之间留的是「看清这一件」的停顿,
//   不是「等它长完」。改短拍长会让生长串成一片(看不出逐件)。
//
// ⚠️ 视觉体块**播完保留**(见 `TentAssemblyVisual` 文件头:它是帐篷在画面上的唯一载体)。
//   相机的停播归末拍的 `OnExit` —— 正常播完与 `Abort`(读档回拨 tick)都走那里,成对。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Cutscene;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>P3-02b 的搭建演出:五件逐件生长 + 独立机位绕半圈。</summary>
    public sealed class TentBuildCutscene : IDisposable
    {
        /// <summary>帐篷在模块目录里的 id(`multi_material_cost_test` 的帐篷行)。</summary>
        public const int TentModuleId = 10;

        /// <summary>每件生长占用的拍长(tick;24 @ 20 Hz = 1.2 s)。</summary>
        public const int TicksPerPiece = 24;

        /// <summary>运镜拍长(tick;60 @ 20 Hz = 3 s)。</summary>
        public const int OrbitTicks = 60;

        /// <summary>一秒的 tick 数(本仓 20 Hz 口径;`PatientCueDriver` 等同一常量)。</summary>
        public const int TicksPerSecond = 20;

        /// <summary>运镜时长(秒)—— **由 <see cref="OrbitTicks"/> 派生**,不独立写死。</summary>
        public const float OrbitSeconds = OrbitTicks / (float)TicksPerSecond;

        /// <summary>运镜起止方位(度)与俯视高度(米)—— 灰盒期一条弧,见 rig 文件头。</summary>
        public const float OrbitStartYawDegrees = 35f;
        public const float OrbitSweepDegrees = 120f;
        public const float OrbitFocusHeight = 1.0f;

        private readonly CutscenePlayer _player;
        private readonly CutsceneCameraRig _camera;
        private readonly Material _baseMaterial;
        private readonly Func<long> _tick;
        private TentAssemblyVisual _tent;

        /// <summary>构造。</summary>
        /// <param name="player">节拍播放器(与序章**共用** —— 同一时刻只该有一段演出)。</param>
        /// <param name="camera">过场独立机位。</param>
        /// <param name="baseMaterial">占位底材质(<c>null</c> = 无视觉;演出仍可走计时)。</param>
        /// <param name="tick">当前 tick 读面(起始计时用)。</param>
        /// <exception cref="ArgumentNullException">任一必需参数为 null。</exception>
        public TentBuildCutscene(CutscenePlayer player, CutsceneCameraRig camera,
                                 Material baseMaterial, Func<long> tick)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _baseMaterial = baseMaterial;
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        }

        /// <summary>是否在演。</summary>
        public bool IsPlaying => _player.IsPlaying;

        /// <summary>当前帐篷视觉(<c>null</c> = 还没演过 / 已销毁)。</summary>
        public TentAssemblyVisual Visual => _tent;

        /// <summary>
        /// 开演(调用方 = 放置成功的帧)。
        /// </summary>
        /// <param name="cell">帐篷锚点格(世界格)。</param>
        /// <returns><c>true</c> = 已开演;<c>false</c> = 已有演出在播(不重叠,静默跳过)。</returns>
        public bool TryPlay(WorldPos cell)
        {
            if (_player.IsPlaying) return false;

            // 上一次的体块已被保留(它是那座帐篷的视觉)⇒ 再演一座是**新**帐篷,旧的不动 ——
            // 但演出只该为「第一座」的搭建而演:第二座帐篷的放置静默跳过。
            if (_tent != null) return false;

            var focus = new Vector3(cell.X, cell.Y, cell.Z)
                      + new Vector3(0.5f, 0f, 0.5f);   // 格中心(格线交角 + 半格)
            _tent = new TentAssemblyVisual(focus, _baseMaterial);

            var beats = new List<CutsceneBeat>(TentAssemblyVisual.PieceCount + 1);
            for (int i = 0; i < TentAssemblyVisual.PieceCount; i++)
            {
                int piece = i;   // 闭包捕获副本(循环变量在 C# 5+ 已是副本,显式化便于审阅)
                beats.Add(new CutsceneBeat($"tent-piece-{piece}", TicksPerPiece,
                    () => _tent?.Reveal(piece)));
            }

            // 末拍:镜头绕半圈。开播时吸附到起始姿态;停播归 OnExit(完成 / 中断成对)。
            Vector3 orbitFocus = focus + new Vector3(0f, OrbitFocusHeight, 0f);
            beats.Add(new CutsceneBeat("tent-orbit", OrbitTicks,
                () => _camera.Begin(orbitFocus, OrbitStartYawDegrees, OrbitSweepDegrees, OrbitSeconds),
                () => _camera.End()));

            _player.Play(beats, _tick());
            return true;
        }

        /// <summary>每帧推进(墙钟秒):生长补间 + 机位补间。没在演 ⇒ 纯 no-op。</summary>
        /// <param name="deltaSeconds">本帧时长(秒)。</param>
        public void OnFrame(float deltaSeconds)
        {
            if (!_player.IsPlaying) return;
            _tent?.Step(deltaSeconds);
            _camera.Step(deltaSeconds);
        }

        /// <summary>销毁视觉与机位(幂等;世界切换 / 退出)。</summary>
        public void Dispose()
        {
            _player.Abort();
            _camera.End();
            _camera.Dispose();
            _tent?.Dispose();
            _tent = null;
        }
    }
}
