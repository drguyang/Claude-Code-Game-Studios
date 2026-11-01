// 29 的档位请求载体(story-007 · 2026-11-02)
//
// ── 为什么要有这个类(它不是新功能)────────────────────────────────────────────
// 这段逻辑此前是**内联在 `BootRoot` 的 `deathPresenter.Bind(...)` 里的一个 lambda**
// (`BootRoot.cs:556-575`,2026-11-01 `a5f766f6` 引入)。行为本身是对的 ——
// 问题是**没有任何东西能指向它**:`death-respawn` 内零份 story 提它,
// 改坏这条接线**没有一条 AC 会红**(只有 `CameraSetModeRequesterGate` 会)。
// 这是 `2026-11-02 §14 按 epic 扫描` 查出的**缺口 A**。
//
// ⇒ 本类 = 同一段逻辑的**具名载体**。**行为必须与提取前逐条等价**(story-007 R-7.2):
//
//     条件                        提取前 / 提取后(必须一致)
//     _cameraRig == null          return,零请求
//     rig.Mode == FirstPerson     return,零请求
//     downed == true              SetMode(Downed,  29)
//     downed == false             SetMode(Explore, 29)
//
// ── 逐条纪律 ─────────────────────────────────────────────────────────────────
//   · **只发意图,不持相机**(ADR-020 §五「相机只读不持状态」):除 VR 早退外,
//     本类不读 `rig` 的任何其它状态,更不缓存 `Mode`。
//   · **`FirstPerson` 早退不是「按状态决策」**,是「VR 下不得产生该意图」
//     (spec §9「平面两段式相机运动**不得平移到 VR**」)—— 两者的区别写在 ADR-020 Amd C ①。
//   · **边沿由调用方负责**(`DeathTransitionPresenter._downedRequested`):意图制只需在
//     状态变化时登记,每帧入队会让 `CameraModeMachine._pending` 无限增长。
//   · `RequesterId` **写死不参数化** —— 甲案「同一请求方可降档」靠它认出「谁开的档」,
//     传错号是**静默失效**(不崩、不报错,相机卡在倒下档)。
//
// ── 为什么住 `Gameplay.Presentation` ─────────────────────────────────────────
// 与 `DeathTransitionPresenter` 同层(呈现层)。⚠️ 本仓已有先例:`story-011` 把移动压制写面
// 放在装配层 `Gameplay.Boot`,被 `AC-1-23` 的机械门按程序集判红 —— **不要把新的相机关联逻辑
// 再塞回装配层**。`BootRoot` 只负责构造它并绑定机位访问器。
//
// 权威来源:
//   `design/ux/death-transition-29.md` §9(`:188` 两段式 / `:127` Φ1 / `:129` Φ3 / `:133` VR 全禁)
//   `design/gdd/camera-and-viewpoint.md` `R-2-5`(29 → `Downed` / 29 → `Explore`)
//   `docs/architecture/adr-020-player-controller-and-camera.md` §五 / Amendment C ①②

using System;
using DaYiJingCheng.Gameplay.Presentation.Camera;

namespace DaYiJingCheng.Gameplay.Presentation.DeathTransition
{
    /// <summary>
    /// 29(死亡与复活)的档位请求载体 —— 倒下进 <see cref="CameraMode.Downed"/>、退出回
    /// <see cref="CameraMode.Explore"/>,`requesterId` 恒 = <see cref="RequesterId"/>。
    /// </summary>
    public sealed class DownedCameraRequester
    {
        /// <summary>
        /// 29 的请求方系统号(`systems-index.md` 编号 29)。
        /// <para>⚠️ **不是配置项,是身份**:甲案「同一请求方可降档」的判据靠它认出「谁开的这个档」
        /// (`camera-and-viewpoint.md:256`「只能由请求方退出」)。</para>
        /// </summary>
        public const int RequesterId = 29;

        private readonly Func<ICameraRig> _rigOf;

        /// <summary>
        /// 构造请求器。
        /// </summary>
        /// <param name="rigOf">机位**懒访问器**。为什么是 `Func` 而不是 `ICameraRig` 实例:
        /// 机位在 `BootRoot` 启动序第 5 步才建,而本类构造更早;且拆卸段会把字段置 null,
        /// 闭包可能在下一帧才求值 ⇒ 直取会 NRE(2026-11-01 已有 94 条 PlayMode 红的先例)。
        /// 允许返回 null(拆卸后 / 逻辑层)⇒ 该次请求静默跳过。</param>
        /// <exception cref="ArgumentNullException"><paramref name="rigOf"/> 为 null。</exception>
        public DownedCameraRequester(Func<ICameraRig> rigOf)
        {
            _rigOf = rigOf ?? throw new ArgumentNullException(nameof(rigOf));
        }

        /// <summary>是否已装配机位访问器(观测面;漏装由 PlayMode 启动序断言守)。</summary>
        public bool HasCameraRig => _rigOf != null;

        /// <summary>
        /// 发一条档位意图。
        /// <para>⚠️ 这是**请求**不是**控制**:相机是否接受、如何转场由 `CameraModeMachine.Settle`
        /// 决定,本类**不读回** `Mode`(唯一例外见方法体注)。</para>
        /// </summary>
        /// <param name="downed">true = 进入倒下档(Φ1/Φ2);false = 退出回探索档(Φ3 起)。</param>
        public void Request(bool downed)
        {
            ICameraRig rig = _rigOf?.Invoke();
            if (rig == null)
                return;   // 机位未建 / 拆卸段 —— 呈现层缺席不抛

            // ⚠️ **VR 全禁**(spec §9「平面两段式相机运动不得平移到 VR」)。
            //    这是**唯一**允许读 `rig.Mode` 的地方,且读它是为了「**不产生**意图」,
            //    不是为了「按相机状态决策」—— 两者的区别是 ADR-020 §五 的边界。
            if (rig.Mode == CameraMode.FirstPerson)
                return;

            // ⚠️ **必须双参且传真实系统号** —— 单参 / -1 会让甲案的「同一请求方可降档」静默失效
            // (相机卡在倒下档),且机械门 `CameraSetModeRequesterGate` 判红。
            rig.SetMode(downed ? CameraMode.Downed : CameraMode.Explore, RequesterId);
        }
    }
}
