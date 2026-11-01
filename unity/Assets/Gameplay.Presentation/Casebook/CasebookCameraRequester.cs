// 39(脉案)的档位请求载体(story-006 · 2026-11-02)
//
// ── 它补的是什么 ─────────────────────────────────────────────────────────────
// `camera-and-viewpoint.md` 的 `R-2-5` 档位表写着「39 打开脉案 ⇒ 请求 `Casebook`」,
// 而生产侧此前**一条都没有**:`grep -rn "CameraMode.Casebook" unity/Assets --include=*.cs`
// 只命中相机内部的优先级表与冻结判据,**零个请求点** —— 即 2026-11-02 查出的「该发没发」。
// 现有门 `CameraSetModeRequesterGate` 只判 arity 与字面 `-1`,它不看那张表,所以全绿。
//
// ── 与 29 的同构关系 ─────────────────────────────────────────────────────────
// 与 `DeathTransition/DownedCameraRequester` 结构完全相同(只发意图 / 不持相机 /
// `RequesterId` 写死为 const / `Func` 懒求值 + 判空)。两者差异只有两处:
//   · 请求的档:`Casebook`(开册)/ `Explore`(合册)
//   · 边沿的**判定位置**:29 的边沿在呈现器内(`_downedRequested`),39 的边沿在
//     `SkeuoRuntimeDriver` 的模态开关处(`NotifyCasebookCamera`)——
//     因为「开 / 合册」本身就是模态状态迁移,那里才是唯一入口。
//
// ── 逐条纪律 ─────────────────────────────────────────────────────────────────
//   · **只发意图,不持相机**(ADR-020 §五):除 VR 早退外不读 `rig` 任何状态,更不缓存 `Mode`。
//   · **`FirstPerson` 早退**不是「按状态决策」,是「VR 下不得产生该意图」
//     (`FirstPerson` 是头显驱动、不经本链;平面近景档不得平移到 VR,承 ADR-020 §六)。
//     ⚠️ 本条是 story-006 的**就地裁定**(原卡未写),未经用户逐条确认。
//   · **必须双参且传 39** —— 甲案「同一请求方可降档」靠它认出「谁开的这个档」:
//     传错号,39 开的 `Casebook` 连自己都退不掉(不崩、不报错,相机卡在脉案档)。
//     单参 / `-1` 会被 `CameraSetModeRequesterGate` 判红。
//   · **边沿由调用方负责**:同态重复开合不发意图(意图制只需状态变化)。
//
// ── 为什么住 `Gameplay.Presentation` ─────────────────────────────────────────
// 与 `DownedCameraRequester` 同层。⚠️ `story-011` 已实测过把新写面塞进装配层
// (`Gameplay.Boot`)会被按**程序集**判白的机械门打红 —— 不要把相机关联逻辑再放回装配层。
//
// 权威来源:
//   `design/gdd/casebook.md` 规则五「摊开与相机」
//   `design/gdd/camera-and-viewpoint.md` `R-2-5`(39 → `Casebook` / 39 → `Explore`)
//   `docs/architecture/adr-020-player-controller-and-camera.md` §五 / Amendment C ①②
//   `production/epics/casebook/story-006-camera-mode-intent.md`

using System;
using DaYiJingCheng.Gameplay.Presentation.Camera;

namespace DaYiJingCheng.Gameplay.Presentation.Casebook
{
    /// <summary>
    /// 39(脉案)的档位请求载体 —— 开册进 <see cref="CameraMode.Casebook"/>、
    /// 合册回 <see cref="CameraMode.Explore"/>,`requesterId` 恒 = <see cref="RequesterId"/>。
    /// </summary>
    public sealed class CasebookCameraRequester
    {
        /// <summary>
        /// 39 的请求方系统号(`systems-index.md` 编号 39)。
        /// <para>⚠️ **不是配置项,是身份**:甲案「同一请求方可降档」的判据靠它认出
        /// 「谁开的这个档」。传错号是**静默失效**(相机卡在脉案档,不崩不报错)。</para>
        /// </summary>
        public const int RequesterId = 39;

        private readonly Func<ICameraRig> _rigOf;

        /// <summary>
        /// 构造请求器。
        /// </summary>
        /// <param name="rigOf">机位**懒访问器**。为什么是 `Func` 而不是实例:机位在
        /// `BootRoot` 启动序第 5 步才建,而本类构造更早;且拆卸段会把机位字段置 null,
        /// 闭包可能在下一帧才求值。允许返回 null(拆卸后 / 逻辑层)⇒ 该次请求静默跳过。</param>
        /// <exception cref="ArgumentNullException"><paramref name="rigOf"/> 为 null(装配遗漏,构造期 fail-loud)。</exception>
        public CasebookCameraRequester(Func<ICameraRig> rigOf)
        {
            _rigOf = rigOf ?? throw new ArgumentNullException(nameof(rigOf));
        }

        /// <summary>是否已装配机位访问器(观测面;漏装由 PlayMode 启动序断言守)。</summary>
        public bool HasCameraRig => _rigOf != null;

        /// <summary>
        /// 发一条档位意图。
        /// <para>⚠️ 这是**请求**不是**控制**:相机是否接受、如何转场由 `CameraModeMachine.Settle`
        /// 决定,本类不读回 `Mode`(唯一例外见方法体注)。</para>
        /// </summary>
        /// <param name="open">true = 开册(进 `Casebook`);false = 合册(回 `Explore`)。</param>
        public void Request(bool open)
        {
            ICameraRig rig = _rigOf?.Invoke();
            if (rig == null)
                return;   // 机位未建 / 拆卸段 —— 呈现层缺席不抛

            // ⚠️ **VR 全禁**:`FirstPerson` 是头显驱动、不经本链。
            //    这是**唯一**允许读 `rig.Mode` 的地方,且读它是为了「**不产生**意图」,
            //    不是为了「按相机状态决策」—— 两者的区别是 ADR-020 §五 的边界。
            if (rig.Mode == CameraMode.FirstPerson)
                return;

            rig.SetMode(open ? CameraMode.Casebook : CameraMode.Explore, RequesterId);
        }
    }
}
