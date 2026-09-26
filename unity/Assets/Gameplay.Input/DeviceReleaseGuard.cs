// Story 008 · D3 设备移除合成 release(AC-3-D3 · TR-input-011)
//
// 权威来源:
//   GDD input-system.md §States 一(NoDevice 行:「合成 release(§Edge Cases 一)」)·
//   AC-3-D3(BLOCKING):设备移除 → 该设备全部 IsPressed 转 false,**无卡键**(同帧)。
//   Story 008 QA:断连时 composite 按住(一半)/ 断连设备 = 当前 Mixed 源(态迁移归
//   DeviceStateManager,本类只保证 pressed 释放)/ 无按住设备断连 = 无操作不抛。
//
// 引擎事实(1.20.0 源码 + EditMode 实测,com.unity.inputsystem@7a4e1a2a8194):
//   InputManager.RemoveDevice 先跑引擎自己的 InputActionState.OnDeviceChange(Removed)
//   (:1603)→ 重解绑重时对 active control 失效的动作 ResetActionState(ResolveBindings
//   else 分支)⇒ press 闩在**引擎侧自清**;本类 listener 随后跑(:1629)⇒ Removed
//   路径下通常已无 pressed 可清(幂等)。**原「引擎不清闩 ⇒ 卡键」前提已被实测证伪**
//   (Story 008 批跑 #5,test_deviceRemoved_withoutGuard_* 钉住该事实)。
//   同族事实(批跑 #7 取证,测试注释详):重解绑由 InputBindingResolver **重建** actionStates
//   (isPressed 默认 false)而 restore 回写清单不含 isPressed ⇒ **他设备**按住的 IsPressed()
//   闩同样在重解绑瞬间丢失且不可自愈(phase / activeControl 保留)——读侧注意:
//   本类不越权清的证明面是 phase + activeControl + SyntheticReleaseCount,不是 IsPressed。
//   本类保留理由:① 引擎自清是源码事实而非文档化契约,不能当游戏侧义务的依据;
//   ② GDD / 故事 D3 明文要求「游戏侧合成 release」—— 本类是该义务的实现载体
//   (公开 ReleaseDeviceNow 供恢复流程 / 装配路径直驱,QA 负例的对照面)。
//   机制真身由直驱接缝的测试证明(见 device_and_presentation_test.cs D3 段)。
//
// 设备归属判据(两级,缺一不 Reset —— us-008-1):
//   ① 静态面:资产绑重的 binding.path 设备段("<Gamepad>/…")与断连设备 layout 全等
//      (读资产而非运行期解析 —— Removed 回调时引擎已重解绑重,action.controls 里的
//      该设备控件已被摘掉,运行期面不可归属);
//   ② 运行期面:action.activeControl.device == 断连设备 —— **按住来源判定**。
//      真资产 InputSystem_Actions 中 Move/Sprint/Interact/Emergency 等皆 Kbm+Pad 双绑:
//      键盘按住而闲置手柄断电时,①命中但②≠ ⇒ 跳过(键盘按住不掉,回归钉
//      test_deviceRemoved_otherDeviceHeld_pressSurvives);activeControl 缺失而
//      IsPressed 的破损态 ⇒ 保守清(宁过不漏:卡键是 BLOCKING 违例)。
//   原「同布局两手柄会过释放」的登记限制由此两级判定消除(按住来自存活手柄 ⇒ ②≠ ⇒ 跳过)。
//   Removed 事件路径上引擎通常先自清(见文件头),守卫在该路径幂等;真实事件效果不可观测,
//   handler 逻辑由反射直驱测钉住(test_deviceGuard_onDeviceChange_*)。
//
// 零状态纪律:本类只观察 + 释放,**不持设备态** —— 态迁移由 DeviceStateManager 独立订阅
//   onDeviceChange(两观察者各挂各的,顺序无关)。

using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>设备移除合成 release 观察者(AC-3-D3 实现载体):订阅
    /// <c>InputSystem.onDeviceChange</c>,在 Removed / Disconnected 时对断连设备驱动过的
    /// 全部 pressed 动作执行 <see cref="InputAction.Reset"/> —— 同步在回调内完成(同帧),
    /// 读侧(action.IsPressed() / phase)即刻为 false / 非进行中。</summary>
    public sealed class DeviceReleaseGuard
    {
        private readonly List<InputAction> _actionBuffer = new List<InputAction>();
        private bool _attached;

        /// <summary>累计合成释放的动作次数(行为仪表 —— 测试据此断「release 真的发了」
        /// 而非引擎顺手清的;0 按住设备断连 ⇒ 本计数不动)</summary>
        public int SyntheticReleaseCount { get; private set; }

        /// <summary>是否已挂 onDeviceChange。</summary>
        public bool IsAttached => _attached;

        /// <summary>挂接(恰一次;重复挂抛)。装配层在动作资产 Enable 之后挂。</summary>
        /// <exception cref="InvalidOperationException">重复挂接。</exception>
        public void Attach()
        {
            if (_attached)
                throw new InvalidOperationException("DeviceReleaseGuard 已挂接 onDeviceChange,不可重复挂(恰一次)。");
            _attached = true;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        /// <summary>卸下(未挂 = 幂等)。</summary>
        public void Detach()
        {
            if (!_attached) return;
            _attached = false;
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected)
                return;
            ReleaseDeviceNow(device);
        }

        /// <summary>对一个(已或即将断连的)设备执行合成 release:凡 enabled 且
        /// 资产绑重命中该设备布局(静态面)、**且按住来自该设备**(activeControl 归属,
        /// 运行期面)、且当前 pressed / 进行中的动作 ⇒ <see cref="InputAction.Reset"/>。
        /// 公开以便脱离事件流的装配路径(恢复流程 / 测试)直接驱动。返回本次释放的动作数。</summary>
        /// <param name="device">断连设备(null = 0,不抛)。</param>
        public int ReleaseDeviceNow(InputDevice device)
        {
            if (device == null) return 0;
            int released = 0;
            _actionBuffer.Clear();
            InputSystem.ListEnabledActions(_actionBuffer);
            foreach (var action in _actionBuffer)
            {
                if (action == null) continue;
                if (!action.IsPressed() && action.phase != InputActionPhase.Started &&
                    action.phase != InputActionPhase.Performed)
                    continue;                          // 没在响 ⇒ 无需 release(Edge:无按住 = 无操作)
                if (!ActionBoundTo(action, device)) continue;
                var ac = action.activeControl;        // ② 按住来源判定(us-008-1)
                if (ac != null && ac.device != device) continue;   // 按住来自他设备 ⇒ 不越权清
                action.Reset();                        // 同帧:读侧即 false
                released++;
            }
            SyntheticReleaseCount += released;
            return released;
        }

        /// <summary>动作的资产绑重中是否有任何一条命中该设备(layout 段全等比较)。
        /// 复合绑重:本体路径与 part 路径逐条比对(part 同属该设备 ⇒ 命中,
        /// QA Edge「composite 按住一半」由此覆盖)。</summary>
        private static bool ActionBoundTo(InputAction action, InputDevice device)
        {
            var map = action.actionMap;
            if (map == null) return false;
            ReadOnlyArray<InputBinding> bindings = map.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (!string.Equals(b.action, action.name, StringComparison.Ordinal)) continue;
                if (BindingPathMatchesDevice(b.path, device)) return true;
            }
            return false;
        }

        /// <summary>"&lt;Layout&gt;/rest" 的 Layout 段与设备 layout(或其变体基名,取 '(' 前)
        /// 全等 ⇒ 命中;复合 part 路径逐条比对(part 同属该设备 ⇒ 命中,QA Edge
        /// 「composite 按住一半」由此覆盖)。无设备前缀的路径(组掩码 "*" / tag "{...}")
        /// 无法静态归属 ⇒ 静态面保守命中;真正是否释放仍由运行期 activeControl 归属
        /// (见 ReleaseDeviceNow ②)终裁 —— 静态面命中但按住来自他设备 ⇒ 跳过。</summary>
        private static bool BindingPathMatchesDevice(string bindingPath, InputDevice device)
        {
            if (string.IsNullOrEmpty(bindingPath)) return false;
            if (bindingPath[0] != '<') return true;    // 无设备前缀(掩码 / tag)⇒ 保守命中
            int close = bindingPath.IndexOf('>', 1);
            if (close < 0) return true;
            string layout = bindingPath.Substring(1, close - 1);
            if (layout == device.layout) return true;
            // 防御性分支:InputSystem 1.20.0 的 binding path 不解析变体语法
            // (包源码 InputControlPath 无 variant 解析)⇒ 真资产走不到此行。
            // 保留不删:若未来包版本引入变体,基名回落把「能匹配」方向守住 ——
            // 删掉它会把异形布局名推向欠释放/卡键方向(欠释放比过释放严重)。
            int paren = device.layout.IndexOf('(');
            return paren > 0 && layout == device.layout.Substring(0, paren);
        }
    }
}
