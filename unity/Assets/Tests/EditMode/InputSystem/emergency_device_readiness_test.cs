// TD-epic-01 第八片 · 3 侧「模拟量通道有无」判据(GDD emergency-procedures 规则十二 / AC-10-21)
//
// 判据(可证伪):
//   ① 有手柄(Pad)/ XR 在场 ⇒ 有模拟量通道(IsAnalogChannelAbsent = false);
//   ② 仅键鼠 ⇒ 无模拟量通道(键鼠回退 ⇒ cause = 降级);
//   ③ 未注入 / 未挂接(设备态未知)⇒ 记「无模拟量」(降级如实可见,不静默记成玩家选择);
//   ④ Mixed(键鼠 + 手柄)⇒ 有模拟量(判**能力**而非最近输入来源)。
//
// ⚠️ 设备态用 `DeviceStateManager.Attach()` 真挂(与 story-008 判据同法):
//    编辑器自带键盘/鼠标在联 ⇒ Kbm 家族恒在场;Pad / Xr 由本文件临时补加并在 finally 摘掉。
// ⚠️ 旧名 `EmergencyAnalogChannel` 的初稿曾判 activeControl;已改为设备态判据(见
//    EmergencyDeviceReadiness 类注:设备态是 3 的单一真源,不另开第三份设备副本)。

using DaYiJingCheng.Gameplay.Input;
using NUnit.Framework;
using UnityEngine.InputSystem;
using InputApi = UnityEngine.InputSystem.InputSystem;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    internal sealed class EmergencyDeviceReadinessTest
    {
        private const int AxisThreshold = 32768, AxisTolerance = 8192, AxisDwell = 4;
        private const int PointerThreshold = 262144, PointerTolerance = 65536, PointerDwell = 4;

        private static DeviceStateManager NewAttached()
        {
            var mgr = new DeviceStateManager(AxisThreshold, AxisTolerance, AxisDwell,
                                             PointerThreshold, PointerTolerance, PointerDwell);
            mgr.Attach();
            return mgr;
        }

        [Test]
        public void test_keyboardMouseOnly_analogChannelAbsent()
        {
            var kbd = Keyboard.current == null ? InputApi.AddDevice<Keyboard>() : null;
            var mouse = Mouse.current == null ? InputApi.AddDevice<Mouse>() : null;
            try
            {
                var mgr = NewAttached();
                try
                {
                    Assert.AreEqual(DeviceState.KbmOnly, mgr.State, "前置:仅键鼠");
                    Assert.IsTrue(new EmergencyDeviceReadiness(mgr).IsAnalogChannelAbsent,
                        "仅键鼠 ⇒ 无模拟量通道(键鼠回退形状 ⇒ cause = 降级,AC-10-21)");
                }
                finally { mgr.Detach(); }
            }
            finally
            {
                if (kbd != null) InputApi.RemoveDevice(kbd);
                if (mouse != null) InputApi.RemoveDevice(mouse);
            }
        }

        [Test]
        public void test_gamepadPresent_analogChannelAvailable()
        {
            var gamepad = InputApi.AddDevice<Gamepad>();
            var kbd = Keyboard.current == null ? InputApi.AddDevice<Keyboard>() : null;
            var mouse = Mouse.current == null ? InputApi.AddDevice<Mouse>() : null;
            try
            {
                var mgr = NewAttached();
                try
                {
                    Assert.AreEqual(DeviceState.Mixed, mgr.State, "前置:键鼠 + 手柄");
                    Assert.IsFalse(new EmergencyDeviceReadiness(mgr).IsAnalogChannelAbsent,
                        "手柄在场(Mixed)⇒ 有模拟量通道 —— 判能力,不判最近输入来源");
                }
                finally { mgr.Detach(); }
            }
            finally
            {
                if (kbd != null) InputApi.RemoveDevice(kbd);
                if (mouse != null) InputApi.RemoveDevice(mouse);
                InputApi.RemoveDevice(gamepad);
            }
        }

        [Test]
        public void test_deviceStateUnknown_analogChannelAbsent()
        {
            // 未注入(3 无设备态实例)⇒ 降级如实可见(不是静默「玩家选择」)。
            Assert.IsTrue(new EmergencyDeviceReadiness(null).IsAnalogChannelAbsent,
                "无设备态 ⇒ 记无模拟量(降级可见;判据宁严勿猜)");
        }

        [Test]
        public void test_contractIsImplementedByProductionReader()
        {
            // 契约(3 侧 IEmergencyDeviceReadiness)由生产读数实现 —— 改名 / 改型即不编译;
            // 且经接口调用与直接调用同值(接口不是第二份判据)。
            IEmergencyDeviceReadiness contract = new EmergencyDeviceReadiness(null);
            Assert.IsTrue(contract.IsAnalogChannelAbsent,
                "经接口调用 = 直接调用(单一判据,接口只是转发形状)");
        }
    }
}
