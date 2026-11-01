// 相机「倒下」两段式相位机(2 侧 · 表现态;2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 权威来源(逐字)
// ─────────────────────────────────────────────────────────────────────────────
//   `design/ux/death-transition-29.md` §9 转场表「相机(Φ1→Φ2)」:
//     「**两段式**:先失去支撑(**下沉 + 微倾**)→ 再躺定(**禁甩镜**)」
//   `design/ux/death-transition-29.md` §6 状态表:
//     Φ1「相机开始**两段式第一段**(失支撑,ADR-020 自建机位)」
//   `design/gdd/death-and-respawn.md:491` Feel 表「Animation Feel Targets」:
//     「倒下 = **两段式**(先失去支撑 → 再躺下);醒来 = 分段」
//   ⚠️ **规格出处订正(2026-11-01 实测)**:`death-and-respawn.md:491` 记作「承 25 `:1639`」,
//      但 `disease-simulation.md` 的第 1639 行是伤害曲线表的**历史注记**,
//      **不含两段式**。真实出处 = `combat-and-weapon-lines.md:1665`
//      (「两段式倒下:**先失去支撑 → 再躺下**」,那是**敌人**的呈现契约)。
//      本件按 **29 spec §9 的玩家相机规格**实现;`combat:1665` 与 `29:491` 的
//      「先失支撑 → 再躺下」**语义同构**,故两处引据对本件指向同一形状。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 三条 spec 硬禁(逐条落进形状,不靠纪律)
// ─────────────────────────────────────────────────────────────────────────────
//   ① **禁甩镜**(29 §9 逐字):⇒ 位置/角度**只做单调插值**,全程无速度突变;
//      第二段(躺定)是**保持**,不是「再动一下」。
//   ② **禁视场旋转**(29 §9 晕动评估:「小幅度机位运动 + 全屏 2D 叠层,非视场旋转」)
//      ⇒ 微倾只改 **pitch(俯仰角)**,**不动 yaw**(yaw 变了就是绕着人转 = 视场旋转)。
//   ③ **VR 全禁**(29 §9「VR 变体(P1a)| 全禁镜头效果」+「平面两段式相机运动
//      **不得平移到 VR**」)⇒ 本机**只在平面档生效**;`FirstPerson` 在位时调用方
//      必须不请求 `Downed`(由 `CameraModeMachine` 的 VR 早退路径保证)。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 全部数值 = **具名占位**,归用户数值轮(承 technical-preferences「数值用户自己调」)
// ─────────────────────────────────────────────────────────────────────────────
//   spec 只给了**形状**(下沉 + 微倾、两段、禁甩镜),**一个数都没有**。
//   下列常量是**占位值**,每个都标了取值范围与含义;数值轮改这里一处即可,
//   改后须同步 `design/ux/death-transition-29.md` §14 的调参旋钮表。
//   ⚠️ **不得**把占位值当成「已裁定数值」在下游引用(本仓已五次抓过「自述顶替裁定」)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么是**纯结构**(不带 MonoBehaviour / 不引 UnityEngine 时间)
// ─────────────────────────────────────────────────────────────────────────────
//   与 `DeathTransitionPhases` 同构:`Step(dt)` 只吃显式时长,不读 `Time.deltaTime`
//   ⇒ EditMode 可逐帧断言相位与插值,**不需要渲染帧**(batch 下也能跑)。

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Camera
{
    /// <summary>两段式的视觉相位(spec §9 的「先失支撑 → 再躺定」)。</summary>
    public enum CameraDownedPhase
    {
        /// <summary>未倒下(零偏移 —— 相机回到常态机位)。</h2>
        None = 0,

        /// <summary>**第一段 · 失支撑**:下沉 + 微倾,单调推进。</summary>
        Sagging = 1,

        /// <summary>**第二段 · 躺定**:保持第一段末态,不再动(禁甩镜)。</summary>
        Settled = 2,
    }

    /// <summary>
    /// 相机倒下两段式的相位推进器(可变结构;调用方持有,逐帧 <see cref="Step"/>)。
    /// <para>输出 = <see cref="SagFraction"/>(下沉进度 0→1)与 <see cref="TiltFraction"/>(
    /// 微倾进度 0→1);`CameraRig` 用它们乘上各自的幅度常量。</para>
    /// </summary>
    public struct CameraDownedDriver
    {
        /// <summary>当前相位。</summary>
        public CameraDownedPhase Phase;

        /// <summary>段内已过时长(秒)。</summary>
        public float Elapsed;

        /// <summary>下沉进度 `∈ [0,1]`(0 = 常态机位高度;1 = 完全下沉)。</summary>
        public float SagFraction;

        /// <summary>微倾进度 `∈ [0,1]`(0 = 常态 pitch;1 = 完全微倾)。</summary>
        public float TiltFraction;

        /// <summary>未倒下态(零偏移)。</summary>
        public static CameraDownedDriver None =>
            new CameraDownedDriver(CameraDownedPhase.None, 0f, 0f, 0f);

        public CameraDownedDriver(CameraDownedPhase phase, float elapsed, float sag, float tilt)
        {
            Phase = phase;
            Elapsed = elapsed;
            SagFraction = sag;
            TiltFraction = tilt;
        }

        /// <summary>
        /// 推进一步(spec §9 的两段式)。**幂等且单调**:同一输入重复调用不会回退。
        /// </summary>
        /// <param name="downed">本帧是否处于倒下态(由调用方读 `ICameraRig.Mode == Downed`)。</param>
        /// <param name="dt">表现态帧时长(秒;负值按 0 处理,不抛 —— 帧时长来自引擎)。</param>
        /// <remarks>
        /// <para>⚠️ <b>两段式的方向语义</b>:`downed == true` ⇒ 前进(下沉 → 躺定);
        /// `downed == false` ⇒ **立即归零**(醒来 / 复活后相机必须马上回常态,
        /// 不该有「缓慢爬起」——spec 把「醒来」归 §9 的另一行「相机第二段(床格视角)」,
        /// 那是 29 的**另一条**相机线,不属本机)。</para>
        /// <para>⚠️ <b>为什么「归零」不插值</b>:本机只管**倒下**;醒来段的机位是
        /// 「床格视角睁眼」(29 §6 Φ3),它由复活后的**另一套机位**承载(归机位轮)。
        /// 在这里插值会与那条线**叠加**,产生 spec 没写的第三个机位。</para>
        /// </remarks>
        public void Step(bool downed, float dt)
        {
            if (!downed)
            {
                // 醒 / 复位:立即归零(见上注「为什么归零不插值」)。
                Phase = CameraDownedPhase.None;
                Elapsed = 0f;
                SagFraction = 0f;
                TiltFraction = 0f;
                return;
            }

            if (dt < 0f) dt = 0f;

            switch (Phase)
            {
                case CameraDownedPhase.None:
                    // 进入第一段(同帧消费 dt —— 首帧不丢,承 DeathTransitionPhases 同款)。
                    Phase = CameraDownedPhase.Sagging;
                    Elapsed = 0f;
                    Step(downed, dt);
                    return;

                case CameraDownedPhase.Sagging:
                    Elapsed += dt;
                    // 单调插值 + 缓出(禁甩镜 ⇒ 无速度突变;ease-out 让收尾柔和)。
                    float t = Math.Min(1f, Elapsed / SagSeconds);
                    float eased = EaseOutSag(t);
                    SagFraction = eased;
                    TiltFraction = eased;   // 下沉与微倾**同步**推进(spec 未分先后)
                    if (t >= 1f)
                    {
                        Phase = CameraDownedPhase.Settled;
                        Elapsed = 0f;
                        SagFraction = 1f;
                        TiltFraction = 1f;
                    }
                    return;

                case CameraDownedPhase.Settled:
                    // 第二段 = **保持**(禁甩镜:这里不得再动)。
                    SagFraction = 1f;
                    TiltFraction = 1f;
                    return;
            }
        }

        /// <summary>
        /// 第一段缓动(ease-out cubic):起步快、收尾缓 —— 摹「失去支撑」的失重感。
        /// <para>spec 只要求「两段式 + 禁甩镜」,**没定曲线形状**;本函数是可替换的占位实现。</para>
        /// <para>⚠️ 单调递增且 `f(0)=0, f(1)=1` —— 两条性质由夹具钉住(证伪轮打的就是它们)。</para>
        /// </summary>
        private static float EaseOutSag(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            float u = 1f - t;
            return 1f - u * u * u;
        }

        // ══════════════════ 数值占位(归用户数值轮)══════════════════

        /// <summary>
        /// **第一段时长**(秒)。占位值;**归用户数值轮**。
        /// <para>取值范围建议:与 29 spec Φ1 的水墨渐入(`FallSeconds = 1.2 s`)同量级 ——
        /// 机位下沉与水墨渐入**同步发生**(spec §6 Φ1「水墨自四边渐入;**相机开始两段式第一段**」),
        /// 二者时长差异过大时会出现「墨已经满了但相机还在往下掉」的割裂感。</para>
        /// </summary>
        public const float SagSeconds = 1.2f;

        /// <summary>
        /// **下沉幅度**(米;正 = 相机沿世界 **-Y** 方向下沉)。占位值;**归用户数值轮**。
        /// <para>⚠️ 数值轮须与 `ARM` 距离联动 —— 幅度过大时相机穿地(spec §9 禁项虽未明写
        /// 「不得穿地」,但它是「小幅度机位运动」的隐含下界)。</para>
        /// </summary>
        public const float SagDepth = 0.45f;

        /// <summary>
        /// **微倾幅度**(度;pitch 的增量,正 = 俯角变大 = 视线更向下)。占位值;**归用户数值轮**。
        /// <para>⚠️ <b>只改 pitch,不改 yaw</b> —— spec §9 晕动评估明写「非视场旋转」;
        /// yaw 变了就是绕着人转,那是视场旋转。</para>
        /// </summary>
        public const float SagTiltDegrees = 6f;
    }
}
