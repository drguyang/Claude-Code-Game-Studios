// 模态开合的输入驱动(2026-10-31 · 模态开启器轮)。
//
// 权威来源:
//   · ADR-013 §十(`ModalId` 闭集 + `IModalState`;42 是模态真源)
//   · ADR-011 §一(输入 action-based;本条取「设备直读过渡最小面」,与 `PlayerInputReader` 同纪律)
//   · design/ux/save-slots-7b.md:189(正式入口 = 暂停壳 / 主菜单)
//   · ADR-013 §十-C Amendment C(2026-10-31:Pause 入闭集,闭集第八员)
//
// 🔁 **2026-10-31 晚(Amendment C 之后)**:开合键的落点由 `SaveSlots` 改为 **`Pause`**。
//   昨日那版是**临时入口**(直开存档位,因暂停壳不在闭集内开不出来);暂停壳成屏后,
//   7b 的路径回到 GDD 写的形状:Escape → 暂停壳 → 「存档位」行 → 存档位 → 载入。
//   接口成员随之改名(`IsSaveSlotsOpen` → `IsPauseOpen` 等),以免名字继续骗人。
//   · design/gdd/interaction-system.md 规则七(模态期交互意图被丢弃 —— 由 4 的门读 42 的开合)
//
// ── 为什么单开一个驱动,而不并进 `InteractionInputDriver` ──────────────
// 后者把「E 查体 / F 急救」翻译成 **sim 侧的调用**(ExamineDriver / EmergencyChannelDriver);
// 本类是**纯呈现层开关**(只调 42 的 `OpenModal` / `CloseModal`,零 sim 参与)。
// 混在一起会让「查体驱动」多出一个与 sim 无关的职责,也会让它的边沿状态机
// (interactHeld / emergencyHeld)多一个无关分支。
//
// ⚠️ **边沿化在此**:`PlayerInputReader.PauseTogglePressed()` 是纯读面(按住为 true),
//    「上一帧按住」由本类持有 —— 与 `InteractionInputDriver` 同款(持键不重复触发)。

using System;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>模态开合驱动:按键沿 → 42 的 `OpenModal` / `CloseModal`。</summary>
    public sealed class ModalInputDriver
    {
        private readonly Func<bool> _togglePressed;
        private readonly ModalOpener _opener;
        private readonly Func<bool> _canOpenPause;
        private bool _held;

        /// <summary>输入读面 + 打开面(均为注入 —— 夹具可直驱,不必碰设备)。</summary>
        /// <param name="togglePressed">开合键是否按住(生产 = `PlayerInputReader.PauseTogglePressed`)。</param>
        /// <param name="opener">42 侧的开关面(生产 = `SkeuoRuntimeDriver` 的适配)。</param>
        /// <exception cref="ArgumentNullException">任一为 null(静默空转 = 按键没反应且无记录)。</exception>
        public ModalInputDriver(Func<bool> togglePressed, ModalOpener opener,
                                Func<bool> canOpenPause = null)
        {
            _togglePressed = togglePressed ?? throw new ArgumentNullException(nameof(togglePressed));
            _opener = opener ?? throw new ArgumentNullException(nameof(opener));
            _canOpenPause = canOpenPause;
        }

        /// <summary>至今成功开 / 关的次数(观测面;夹具据此断言「一次按下 = 恰好一次动作」)。</summary>
        public int OpenCount { get; private set; }

        /// <summary>同上,关闭侧。</summary>
        public int CloseCount { get; private set; }

        /// <summary>每帧调用一次。</summary>
        public void OnFrame()
        {
            bool pressed = _togglePressed();
            bool rising = pressed && !_held;
            _held = pressed;
            if (!rising) return;

            // 🔁 **2026-11-04(`TD-skeuo-03` 结案轮)**:判据由「逐员 if」收敛成**一个可比较的量**
            //   —— `CurrentModal`。规则只有一句:**有模态 ⇒ 关,无模态 ⇒ 开暂停壳**。
            //
            //   ▍为什么必须收敛(而不是再补几条 else-if)
            //   闭集有 9 员,旧写法只认 `Casebook` / `Pause` 两个身份 ⇒ 另外 7 员
            //   (存档位 / 库存容器 / 设置壳 / 教学 / 医馆面板 / 纸近景 / 炮制屏)按同一个 toggle
            //   会落进第三支 `OpenPause()`,也就是**换屏而不是关闭** —— 玩家以为关掉了,
            //   实际被弹到暂停壳,再按一次又回世界,来回打转。逐员补分支只是把闭集复制一遍,
            //   而闭集**还会增员**(它已经增过三次);收敛后闭集增员时本处**零改动**。
            //
            //   ▍与 39 的关系
            //   脉案的「关册 toggle」(`casebook-39.md` §7)现在是这条规则的一个实例 ——
            //   不再需要「脉案先于暂停壳判」的次序:任何模态开着都走同一条关。
            if (_opener.CurrentModal != UI.Skeuomorphic.ModalId.None)
            {
                _opener.Close();
                CloseCount++;
            }
            else
            {
                // 🔁 2026-11-05:**第一层只关不开**。本驱动已提到 `_booted` 早退之前
                //    (菜单期也要能关掉设置壳),但菜单期「无模态时按 ESC」**不得**摊开暂停壳
                //    —— 那是世界内的东西,压在场景级主菜单上就是第二个层级被凭空造出来。
                //    生产注入 `() => _booted`;null = 恒可开(夹具的既有形态)。
                if (_canOpenPause != null && !_canOpenPause()) return;
                _opener.OpenPause();
                OpenCount++;
            }
        }
    }

    /// <summary>
    /// 开合面的**唯一生产实现**:把 <see cref="UI.Skeuomorphic.SkeuoRuntimeDriver"/> 的
    /// `OpenModal` / `CloseModal` 收成三项。
    /// <para>⚠️ 适配器必须住装配层:42 侧看不见 `ModalOpener` 这个 Boot 侧契约
    /// (ADR-025 §① 装配方向不可倒),而本层两件都看得见(<c>CasebookCommandRouter</c> 同型)。</para>
    /// </summary>
    public sealed class DriverModalOpener : ModalOpener
    {
        private readonly UI.Skeuomorphic.SkeuoRuntimeDriver _driver;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentNullException">driver 为 null。</exception>
        public DriverModalOpener(UI.Skeuomorphic.SkeuoRuntimeDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc/>
        public UI.Skeuomorphic.ModalId CurrentModal => _driver.CurrentModal;

        /// <inheritdoc/>
        public void OpenPause() => _driver.OpenModal(UI.Skeuomorphic.ModalId.Pause);

        /// <inheritdoc/>
        public void Close() => _driver.CloseModal();
    }

    /// <summary>
    /// 开合面(42 侧 `SkeuoRuntimeDriver` 的**窄接口**)。
    /// <para>⚠️ 刻意不把整个 `SkeuoRuntimeDriver` 交给驱动:后者是 `MonoBehaviour`,
    /// 夹具里造不出来;窄接口让「按下 ⇒ 开 / 再按 ⇒ 关」这条判据不依赖引擎对象。</para>
    /// <para>⚠️ 开合对象是**暂停壳**(`ModalId.Pause`),不是存档位 —— 自 Amendment C 起
    /// 存档位是暂停壳里的一行,没有自己的按键。</para>
    /// </summary>
    public interface ModalOpener
    {
        /// <summary>
        /// 当前模态身份(<see cref="UI.Skeuomorphic.ModalId.None"/> = 没有模态摊开)。
        /// <para><b>契约形状本身就承载了判据</b>:toggle 面只需要「有没有模态、是哪一员」,
        /// 而**不是**「暂停壳开没开、脉案开没开」。故本接口刻意**不**提供
        /// `IsXxxOpen` 这类逐员布尔 —— 那种形状必然要求调用方也逐员分支,
        /// 闭集增员时两处一起改、且总有人漏改(本接口 2026-11-02 的形态就这样漏过 7 员)。</para>
        /// <para>🔁 2026-11-04(`TD-skeuo-03`):由两个逐员布尔收成本项。</para>
        /// </summary>
        UI.Skeuomorphic.ModalId CurrentModal { get; }

        /// <summary>打开暂停壳。</summary>
        void OpenPause();

        /// <summary>关闭当前模态。</summary>
        void Close();
    }
}
