// camera-viewpoint Story 005 —— 相机求值的**单一相位驱动方**(AC-2-27③ 的生产接线)
//
// 权威来源:
//   GDD camera-and-viewpoint.md AC-2-27①②③ · R-2-5(档位结算)· EC-2-11(冻结档不查询)
//   ADR-011(onAfterUpdate 固定相位;禁 Script Execution Order 隐式排序)
//   ADR-020 §五(相机只读不持状态)
//
// 🔴 **2026-10-03 评审修复(承 TD §3/§4/§5 · 用户裁定「本轮补齐生产接线」)**:
//   此前 `CameraArmSolver.Step` / `CameraModeMachine.Settle` / `CountingArmQuery`
//   **在生产侧零调用点** ⇒ AC-2-27 的三条「每帧恰一次 / 冻结不查询 / 单一相位」
//   全都在**桩**上断言(测试自己调 `Cast` 再断言计数为 1 —— 同义反复)。
//   本类即那个**缺席的驱动方**:每帧**恰一次**驱动结算 + 求值,顺序与相位显式落码。
//
// ⚠️ 帧边沿纪律:`CountingArmQuery.BeginFrame(frameId)` 要求**严格递增帧号**
//   ⇒ 计数**证明**「每帧恰一次」,而非靠调用方自律(TD §3)。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Camera
{
    /// <summary>
    /// 相机一帧的求值驱动 —— **单一显式调用点**(AC-2-27③)。
    /// </summary>
    /// <remarks>
    /// <para><b>相位纪律</b>:由**外部**固定相位驱动(生产 = `InputSystem.onAfterUpdate`,
    /// 承 ADR-011;测试 = 显式 `EvaluateFrame`),**不得**依赖 `MonoBehaviour.Update/LateUpdate`
    /// 的裸调用或 `Script Execution Order` 面板隐式排序(跨 prefab 不传递 ⇒ 静默失效)。
    /// 本类刻意**不**继承 `MonoBehaviour` —— 结构上就没有 `Update/LateUpdate` 入口。</para>
    /// <para><b>调用顺序(每帧恰一次,顺序不可换)</b>:
    /// ① 帧边沿(`BeginFrame(frameId)`)→ ② 档位结算(`Settle`)→ ③ 仅当**未冻结**时求值臂
    /// (`ArmSolver.Step`)。冻结档(`Casebook` / `FirstPerson`)⇒ **跳过物理查询**(AC-2-27②)。</para>
    /// </remarks>
    public sealed class CameraEvaluationDriver
    {
        private readonly CameraModeMachine _machine;
        private readonly CameraArmSolver _arm;
        private readonly CountingArmQuery _counting;
        private int _frameId = -1;

        /// <summary>最近一帧是否发起了物理查询(供夹具断言「冻结档 == 0」)。</summary>
        public bool LastFrameQueried { get; private set; }

        /// <summary>最近一帧的查询数(冻结档须为 0)。</summary>
        public int LastFrameQueryCount => _counting.FrameCount;

        /// <summary>已驱动帧数。</summary>
        public int FrameId => _frameId;

        public CameraEvaluationDriver(
            CameraModeMachine machine,
            CameraArmSolver arm,
            CountingArmQuery counting)
        {
            _machine = machine ?? throw new System.ArgumentNullException(nameof(machine));
            _arm = arm ?? throw new System.ArgumentNullException(nameof(arm));
            _counting = counting ?? throw new System.ArgumentNullException(nameof(counting));
        }

        /// <summary>
        /// 驱动**一帧**(由单一固定相位调用,每帧恰一次)。
        /// </summary>
        /// <param name="dt">表现态帧时长(非 tick)。</param>
        /// <param name="transitionDuration">档位转场时长(组 5;数值留白)。</param>
        /// <param name="shoulder">肩位(F-2-3)。</param>
        /// <param name="viewDir">`ê_view`(单位)。</param>
        public void EvaluateFrame(float dt, float transitionDuration, Vector3 shoulder, Vector3 viewDir)
        {
            _frameId++;
            _counting.BeginFrame(_frameId);              // ① 帧边沿(严格递增帧号)

            _machine.Settle(dt, transitionDuration);     // ② 每帧**恰一次**结算

            LastFrameQueried = false;
            if (!_machine.IsFrozen)                      // ③ 冻结档 ⇒ 不查询(AC-2-27②)
            {
                _arm.Step(shoulder, viewDir, dt);        // 非冻结档 ⇒ 恰一次 SphereCast(AC-2-27①)
                LastFrameQueried = true;
            }
        }
    }
}
