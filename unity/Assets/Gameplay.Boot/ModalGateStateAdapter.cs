// 42 的模态开集 → 4 的消费面契约(`IModalGateState`)适配器(2026-10-31)。
//
// 权威来源:
//   · design/gdd/interaction-system.md 规则七(模态抑制,两个方向)· F-4.2(模态门 = 布尔合取)
//   · ADR-013 §十 Amendment A(`IModalState` / `ModalId` 闭集,42 侧权威声明)
//   · `ModalGate.cs` 头注的「⚠️ 程序集归属」段:消费面契约住 Presentation,
//     **适配器必须住 42 侧**(它两件都看得见),否则 `Gameplay.UI → Gameplay.Presentation` 成环
//   · ADR-025 §①(装配方向不可倒)
//
// ── 为什么这个适配器直到今天才有 ─────────────────────────────────────
// `ModalGate` 自 story-005 起就有夹具,但**生产零构造** —— 两个门各缺一半:
//   方向①(`IArmedState`)已由 `EmergencyChannelDriver` 补上(2026-10 急救接线轮);
//   方向②(`IModalGateState`)**全仓零实现** ⇒ 门构造不出来 ⇒ 规则七的两向抑制
//   在运行期**从未生效**。本类补的就是方向②这一半。
//
// ⚠️ 本类只做**一件事**:把 42 的 `CurrentModal != None` 折成那个布尔。
//    不做缓存(缓存 = 第二真源,模态开合时会读到上一帧的值)。

using System;                                    // Func<SkeuoRuntimeDriver>(提供者形态)
using DaYiJingCheng.Gameplay.Interaction;         // IModalGateState(4 侧消费面)
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;     // SkeuoRuntimeDriver / ModalId(42 侧真源)

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>42 的模态开集 → 4 的只读布尔(规则七 方向②)。</summary>
    public sealed class ModalGateStateAdapter : IModalGateState
    {
        private readonly Func<SkeuoRuntimeDriver> _driver;

        /// <summary>
        /// 构造(**驱动提供者**形态)。
        /// <para>⚠️ 取提供者而非实例:装配序里交互驱动(4 侧)先于 UI 驱动(42 侧)建成 ——
        /// 传实例就只能是 null,而 null 会让模态抑制**永远放行**且看不出是没接上。</para>
        /// </summary>
        /// <param name="driver">返回 42 驱动;返回 <c>null</c> ⇒ 恒 <c>false</c>
        /// (「无 UI ⇒ 无模态」是诚实的降级,不是「假装开着」)。</param>
        public ModalGateStateAdapter(Func<SkeuoRuntimeDriver> driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>便捷构造(夹具用:实例固定)。</summary>
        public ModalGateStateAdapter(SkeuoRuntimeDriver driver)
        {
            _driver = () => driver;
        }

        /// <summary>
        /// 是否有**挡路的**模态摊开(实时读,零缓存)。
        /// <para>⚠️ 读 <see cref="SkeuoRuntimeDriver.BlocksPlay"/> 而非 <c>IsModalOpen</c>:
        /// 后者含**场景配的常驻屏**(若有)⇒ 直接喂闸门会让规则七的门常闭、E 查体当场失效。
        /// <c>BlocksPlay</c> 只算经 <c>OpenModal</c> 显式打开的屏。
        /// <para>🔁 2026-11-02:启动默认已改为**无常驻屏**(`_screenName` 为空),故今日两者同值;
        /// 判据仍按 `BlocksPlay` 写 —— 不依赖启动形态,常驻屏被配回来也不出错。</para>
        /// </summary>
        public bool IsOpen
        {
            get
            {
                SkeuoRuntimeDriver d = _driver();
                return d != null && d.BlocksPlay;
            }
        }
    }
}
