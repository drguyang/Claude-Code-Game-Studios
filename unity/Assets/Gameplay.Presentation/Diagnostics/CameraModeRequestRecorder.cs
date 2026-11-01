// 档位请求记录器(camera-viewpoint/story-007 · 2026-11-02)
//
// ── 它为什么存在 ─────────────────────────────────────────────────────────────
// `R-2-5` 的档位表(机器可读真源 = `docs/architecture/camera-mode-requests.yaml`)
// 与生产代码之间没有任何**完整性**判据:`CameraSetModeRequesterGate` 只判语法。
// 2026-11-02 实测 6 条有效行里有 2 条(39 的两行)**零生产调用点**而门全绿
// —— 「该发没发」只有**行为面**抓得到。本记录器就是那个行为面的采集点。
//
// ── 三条纪律(缺一条它自己就会变成第二真源)────────────────────────────────────
//   ① **不参与任何判定**:只在 `CameraModeMachine.SetMode` 入口追加一行;
//      零读回、零可写消费面。生产侧**不得**出现任何 `Snapshot()` / `TotalRecorded` 读取
//      (判据见 `camera_mode_request_matrix_test.cs` 的 S7-07)。
//   ② **有界**:环形,**容量 64**,溢出丢弃最旧 —— 不增长、不扩容。
//   ③ **不进发布构建**:整类由 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 包裹,
//      `CameraModeMachine` 的调用点在同一符号下 ⇒ `Release` 下**零代码、零字段、零引用**。
//
// ── 为什么不走依赖注入 ───────────────────────────────────────────────────────
// `CameraModeMachine` 是纯逻辑件(无 Unity 依赖、被 EditMode 直接构造),给它加一个
// 记录器构造参数会把**调试面**变成生产契约的一部分,而记录器在发布构建里根本不存在。
// ⇒ 静态 + 条件编译,是让「发布构建里它压根不存在」成为**结构性事实**而非纪律的唯一形态。
//
// ── 为什么**不**住在 `...Presentation.Camera` 命名空间 ──────────────────────────
// `AC-2-01a`(`camera_presentation_discipline_test.test_ac201a_noHiddenStaticState`)按
// **命名空间**判:`DaYiJingCheng.Gameplay.Presentation.Camera` 下的类型**不得有可变静态字段**
// —— 隐藏状态会破坏 `AC-2-01②` 的差分重算前提。
// 本记录器**恰好**两个可变静态字段(环形游标 / 累计数),2026-11-02 首次跑全量时被该门判红。
// ⇒ **处置是搬位置,不是豁免**:它不是相机状态,不参与任何相机计算
// (`SetMode` 只是把请求再入队一次,记录不回流);把它放进 `Diagnostics` 才符合它的身份。
// ⚠️ 「不参与计算」不是靠这句话成立的,是靠 **S7-07**(生产码零读点)那条判据守着的 ——
//    若哪天有人真拿它做判定,它也就同时不再是「诊断件」了,那时该另裁,不是改这里。
//
// 权威:`production/epics/camera-viewpoint/story-007-mode-request-coverage-matrix.md` R-7.4

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Camera;   // CameraMode / CameraModeRequest(搬出命名空间后须显式引)

namespace DaYiJingCheng.Gameplay.Presentation.Diagnostics
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// 档位请求的**只读采集面**(调试 / 夹具专用)。
    /// <para>⚠️ 生产逻辑**永不读它** —— 它只回答「这条请求进过 `SetMode` 没有」,
    /// 不回答「相机接受了没有」(后者归 `CameraModeMachine.Settle` 与各请求方的 L2)。</para>
    /// <para>⚠️ `Release` 构建下本类**不存在**(条件编译)⇒ 不存在「忘关开关」的形态。</para>
    /// </summary>
    public static class CameraModeRequestRecorder
    {
        /// <summary>环形容量。溢出丢弃最旧 —— 有界是纪律②,不是实现细节。</summary>
        public const int Capacity = 64;

        private static readonly CameraModeRequest[] Ring = new CameraModeRequest[Capacity];
        private static int _next;
        private static int _total;

        /// <summary>累计写入条数(可大于 <see cref="Capacity"/>;用于判「溢出」)。</summary>
        public static int TotalRecorded => _total;

        /// <summary>唯一写入点 —— 由 `CameraModeMachine.SetMode` 调用(全仓仅此一处)。</summary>
        public static void Record(CameraMode mode, int requesterId)
        {
            Ring[_next] = new CameraModeRequest(mode, requesterId);
            _next = (_next + 1) % Capacity;
            _total++;
        }

        /// <summary>清空(夹具在每次启动序之后调用,避免跨测试累积)。</summary>
        public static void Clear()
        {
            for (int i = 0; i < Capacity; i++) Ring[i] = default;
            _next = 0;
            _total = 0;
        }

        /// <summary>
        /// 按**写入顺序**取已记录条目(最多 <see cref="Capacity"/> 条;溢出时只保留最近的)。
        /// <para>⚠️ 仅供夹具与门读取(纪律①)。</para>
        /// </summary>
        public static List<CameraModeRequest> Snapshot()
        {
            int count = _total < Capacity ? _total : Capacity;
            var list = new List<CameraModeRequest>(count);
            int start = _total <= Capacity ? 0 : _next;   // 溢出后从最旧的一条开始
            for (int i = 0; i < count; i++)
                list.Add(Ring[(start + i) % Capacity]);
            return list;
        }
    }
#endif
}
