// ADR-020 Amendment B —— ICameraRig 接口。
//
// 权威来源:
//   ADR-020 Amendment B —— YawBasis = (Vector3 fwd, Vector3 right) 水平化正交基,只读
//   ADR-020 §五 —— 相机只读不持状态
//   GDD camera-and-viewpoint.md —— AC-2-07/08/09/10
//
// AC-2-10②: 相机对 1 零 public 写入面
//   - YawBasis 返回不可变 struct
//   - 不暴露相机 Transform / Camera 句柄

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Camera
{
    /// <summary>
    /// 水平化正交基（不可变）。
    /// </summary>
    public readonly struct YawBasis
    {
        public readonly Vector3 Fwd;
        public readonly Vector3 Right;

        public YawBasis(Vector3 fwd, Vector3 right)
        {
            Fwd = fwd;
            Right = right;
        }
    }

    /// <summary>
    /// 相机机位接口 —— 只读，供系统 1 投影 MoveInput。
    /// </summary>
    /// <summary>
    /// 相机档(ADR-020 §Key Interfaces 的权威形状)。
    /// ⚠️ `FirstPerson`(VR)= **P1a**;P0 只落枚举与接口(TR-camera-002)。
    /// </summary>
    public enum CameraMode
    {
        Explore,      // 平面探索(第三人称越肩)
        Treatment,    // 处置态(10 急救)
        Casebook,     // 脉案(39)
        FirstPerson,  // VR(P1a)—— 头显驱动,不经本链;镜头效果全禁

        /// <summary>
        /// **倒下档**(2026-11-01 新增;29 死亡过渡 Φ1→Φ2 的相机两段式承载档)。
        /// <para>权威来源:`design/ux/death-transition-29.md` §9「相机(Φ1→Φ2)」——
        /// 「**两段式**:先失去支撑(**下沉 + 微倾**)→ 再躺定(**禁甩镜**)」;
        /// 该行的规格出处是 `combat-and-weapon-lines.md:1665`(「两段式倒下:先失去支撑 →
        /// 再躺下」),⚠️ 29 GDD `:491` 记作「承 25 `:1639`」是**引据错误**(见本枚举下方注)。</para>
        /// <para>⚠️ <b>优先级低于 `Treatment`、高于 `Explore`</b>(见 `CameraModePriority`):
        /// 倒下的玩家不该还能被「进入处置态」抢走机位(那会让濒死画面跳到急救视角),
        /// 但它**必须**能被 `Casebook` 压过(规则七的模态优先不变)。</para>
        /// <para>⚠️ <b>VR 全禁</b>(spec §9「VR 变体(P1a)| **全禁镜头效果**」+「平面两段式相机
        /// 运动**不得平移到 VR**」)⇒ `FirstPerson` 在位时本档请求被丢弃(承 R-2-9 平面链冻结)。</para>
        /// </summary>
        Downed,
    }

    public interface ICameraRig
    {
        /// <summary>
        /// 水平化正交基。f̂.y == 0 ∧ r̂.y == 0，与 pitch 无关。
        /// 构造式：f̂ := (sin yaw, 0, cos yaw)；r̂ := (cos yaw, 0, −sin yaw)。
        /// </summary>
        YawBasis YawBasis { get; }

        /// <summary>
        /// 当前 yaw（弧度）。
        /// </summary>
        float Yaw { get; }

        /// <summary>
        /// 当前 pitch（度，正 = 俯）。
        /// </summary>
        float Pitch { get; }

        // ── ADR-020 §Key Interfaces 的四成员(2026-10-03 评审修复 B-4/③ 补齐)──────
        //  🔴 初版仅落 YawBasis/Yaw/Pitch ⇒ ADR-020 明列的 Mode/SetMode/Tick/Camera **四成员缺失**,
        //     `OQ-2-6` 的 doc comment 亦未落 —— 而 `OQ-2-6` 自陈「P0 改接口近乎零成本,
        //     推到 P1a 则要动 ADR-020 定义」⇒ 零成本窗口正在关闭。本批偿付。

        /// <summary>当前档(ADR-020 §Key Interfaces)。</summary>
        CameraMode Mode { get; }

        /// <summary>
        /// 档位**请求**入口(意图制,R-2-5):请求方(10 / 39)登记意图,相机决定是否接受与如何转场。
        /// ⚠️ 这是**请求**不是**控制** —— 相机不因它立即切档(结算在单一相位,见 <see cref="Tick"/>)。
        /// </summary>
        void SetMode(CameraMode mode);

        /// <summary>
        /// 档位**请求**入口(带请求方系统号)—— 2026-11-01 甲案新增。
        /// <para>⚠️ <b>为什么必须带 requesterId</b>:甲案的「同一请求方可降档」判据
        /// (`camera-and-viewpoint.md:256`「只能由请求方退出」)需要知道**当前档是谁开的**。
        /// 用无参重载 ⇒ `CameraModeMachine` 只能记到 `0`,所有请求方看起来是同一个
        /// ⇒ 39 的脉案会被 10 的收尾请求顶掉(正是 `O-12` 要防的)。</para>
        /// <para>⚠️ 系统号取自 `systems-index.md` 的编号(10 = 急救 / 29 = 死亡过渡 / 39 = 脉案…)。
        /// 它是**审计与裁决**字段,不是状态 —— 相机不因此持有任何游戏事实(ADR-020 §五)。</para>
        /// <param name="mode">请求的目标档(意图制,相机决定是否接受)。</param>
        /// <param name="requesterId">请求方系统号(负值 = 无请求方,仅测试 / 默认档用)。</param>
        /// </summary>
        void SetMode(CameraMode mode, int requesterId);

        /// <summary>
        /// 每帧求值入口 —— **单一显式相位**(AC-2-27③;承 ADR-011 的固定相位,
        /// 禁 `Script Execution Order` 隐式排序)。跟随 / 越肩阻尼 / 档位结算都在此。
        /// </summary>
        void Tick(float deltaTime);

        /// <summary>
        /// 平面模式的主相机(44 的 `AudioListener` 挂点 —— ADR-020 §七 / AC-ADR020-10)。
        /// <para>🔴 **`OQ-2-6`(VR 立体语义,P1a 前置)**:本属性暴露**单个** `Camera`;
        /// VR 立体的双眼需要一对(或一个 XR 渲染路径)。本属性在 `FirstPerson` 档
        /// **代表哪只眼 / 还是那个「主」相机** —— **P0 未定**,归 P1a 开工前裁定。
        /// P0 只需保证该接口的扩容**不破坏平面链**(TR-camera-002)。</para>
        /// </summary>
        UnityEngine.Camera Camera { get; }
    }
}
