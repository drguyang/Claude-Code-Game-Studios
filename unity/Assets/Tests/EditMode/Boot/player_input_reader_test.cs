// sprint-07 S2.2/S2.5 · 输入读面 EditMode 判据(2026-10-11)
//
// 判据(可证伪):
//   ① 无设备(batch/EditMode)⇒ Interact(E)/Emergency(F) 恒「未按下」—— 读面零依赖、
//     不抛、不假触发(「按 F 零事件」的静态半);
//   ② 驱动空转(无按键)⇒ 零副作用(不查体 / 不急救 / 零事件)—— 与 G4.1 红线同族
//     (「零输入观察窗 ⇒ 病史流零事件」)。

using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="PlayerInputReader"/> 无设备读面 + <see cref="InteractionInputDriver"/>
    /// 空转判据(sprint-07 S2)。</summary>
    internal sealed class player_input_reader_test
    {
        [Test]
        public void test_noDevice_readsNotPressed_noThrow()
        {
            // 无设备 ⇒ 两键恒 false(不抛;batch 下 InputSystem 无设备是常态)
            Assert.IsFalse(PlayerInputReader.InteractPressed(),
                "无设备 ⇒ Interact 未按下(E 按下沿在设备缺席时不许假触发)");
            Assert.IsFalse(PlayerInputReader.EmergencyPressed(),
                "无设备 ⇒ Emergency 未按下");
        }

        [Test]
        public void test_driver_noInput_noSideEffects_zeroEvents()
        {
            // 装配真链(CompositionRoot 自带 fallback 注册表 + 真词表 S1.2),但不注入按键
            var bag = CompositionRoot.Assemble();
            int actorId = bag.IdAuthority.NextPatientId().Value;
            var driver = new InteractionInputDriver(bag, () => new Vector3(0f, 1f, 0f), actorId);

            int streamCountBefore = bag.Stream.Count;
            long tickBefore = bag.TickDriver.CurrentTick;

            // 空转 3 帧(无设备 ⇒ 无按键 ⇒ 只刷新边沿位,不得触发任何动作)
            for (int i = 0; i < 3; i++)
                driver.OnFrame();

            Assert.AreEqual(streamCountBefore, bag.Stream.Count,
                "零输入 ⇒ 病史流零新事件(读面无假触发 —— S2.5 红线静态半)");
            Assert.AreEqual(PatientId.None, driver.LastExaminedPatient,
                "零输入 ⇒ 未查体");
            Assert.AreEqual(PatientId.None, driver.LastEmergencyPatient,
                "零输入 ⇒ 未急救");
            Assert.AreEqual(tickBefore, bag.TickDriver.CurrentTick,
                "驱动不推进 tick(纯输入边沿转发,零时钟)");
        }
    }
}