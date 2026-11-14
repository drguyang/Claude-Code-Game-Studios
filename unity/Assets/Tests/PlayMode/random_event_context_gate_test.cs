// 52 上下文门的**真值**判据(2026-11-14 · 结 `TD-randomevents-05` 的「真的会响」那一半)。
//
// 权威:`design/gdd/random-events.md` 规则六之二 · `unity/Assets/Sim/F1ContextEvaluator.cs` 类首
//
// ═══ 为什么这条夹具必须有 ═════════════════════════════════════════════════
//   `TD-randomevents-05` 的病是「三项旗标写死 false ⇒ 门恒假 ⇒ 链恒 `NoContext`」。
//   它在 2026-11-05 已 closed,但**此后没有任何夹具证明它没退回**:
//   · EditMode 侧测的是 `F1ContextEvaluator` 这个纯函数 —— 喂什么算什么,**生产闭包是否真喂了真值**它看不见;
//   · 链的错误日志只在**异常**时打,正常路径静默。
//   ⇒ 若有人把闭包改回 `isInClinic: false`,全量 EdiMode 与 PlayMode **都会绿**。
//   本件走真启动序 + 真玩家格,**把「写死」这种退化变成红**。
//
// ═══ 判据为什么是「把玩家搬进医馆」════════════════════════════════════════
//   写死 false 与真值 false 在出生点(教堂)完全同形 —— 分不开。
//   只有**把玩家放进医馆**,真值才为 true:此时若仍读到 false,就是写死(或有第二份判据)。
//
// ⚠️ 断言只到「旗标真值」这一层,不假装链条已经**抽过牌**:
//   `ROLL_INTERVAL = 6000 tick`(= 5 分钟 @20 Hz),PlayMode 夹具跑不到 ⇒ 那是另一件事。

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    public class random_event_context_gate_test
    {
        private const float BootTimeoutSeconds = 15f;

        private GameObject _root;
        private BootRoot _boot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyAllNamed("Player");
            if (_root != null) UnityEngine.Object.Destroy(_root);
            yield return null;
        }

        /// <summary>
        /// 把玩家搬进医馆格 ⇒ 威胁档链读到的 `IsInClinic` 必须为 **true**。
        /// <para>这条只要读不到 true 就说明闭包没接真值(写死 false 的退化形态)。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_threatChain_contextReadsRealPlayerCell()
        {
            yield return Boot();

            object services = Services();
            var clinic = (ClinicCellSource)Prop(services, "ClinicCells");
            Assert.IsNotNull(clinic, "服务袋须发放 `ClinicCells`(同源义务)");
            Assert.Greater(clinic.DerivedCellCount, 0,
                "[前置] 医馆格须非空 —— 甲案把 18 槽平移进营地,开档即有壳");

            WorldPos clinicCell = FindClinicCell(clinic);
            Assert.IsTrue(clinic.IsClinicCell(clinicCell), "[前置] 找到的格须真是医馆格");

            PlayerController player = Player();
            Assert.IsNotNull(player, "[前置] 启动序须建出 `PlayerController`");

            // 搬到医馆格里(ADR-020 §四:sim 侧只认 `ActorCellEntered`,故要走传送命令而非改 Transform)
            player.Teleport(new Vector3(clinicCell.X + 0.5f, clinicCell.Y + 0.5f, clinicCell.Z + 0.5f));

            // 玩家格经流确立需要几个 tick;再让链跑过至少一个边沿。
            for (int i = 0; i < 240; i++) yield return null;

            Assert.AreEqual(clinicCell, player.LastCommittedCell,
                "[前置] 传送后玩家格须已就位 —— 否则下面的读数测的是别的格");

            object director = Prop(services, "RandomEventsDirector");
            Assert.IsNotNull(director, "服务袋须发放 `RandomEventsDirector`");

            object threatChain = Field(director, "_threatSpawnChain");
            Assert.IsNotNull(threatChain,
                "威胁档链须已装配(2026-11-11 T22 批五)—— 为 null ⇒ 威胁档在整局里一次都不抽");

            // ── 判据 ① 链**被驱动**:真跑帧下,每 tick 都会得到 ① 号门的结果 ──────────
            //   `ROLL_INTERVAL = 6000` tick(= 5 分钟 @20 Hz),真跑帧永远到不了边界
            //   ⇒ 正常结果是 `NotAtBoundary`。**只要不是 null,就证明导演每个 tick 都在驱动它**。
            PropertyInfo outcomeProp = director.GetType().GetProperty("LastThreatSpawnOutcome",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(outcomeProp, "导演须有 `LastThreatSpawnOutcome` 观测缝");
            object outcome = outcomeProp.GetValue(director);
            Assert.IsNotNull(outcome,
                "威胁档链须被驱动过 —— 为 null 说明导演没调它(装配了但没接上)");
            Assert.AreEqual("NotAtBoundary", outcome.ToString(),
                "真跑帧未到 `ROLL_INTERVAL` 边界 ⇒ 应是 `NotAtBoundary`;其它值说明时序或节奏门变了");

            // ── 判据 ② 上下文读**真格**:用一个显式边界 tick 让链走到 F1 ─────────────
            //   ⚠️ 这一步**显式**传 tick(不是真跑出来的),它证明的是「闭包读真值」,
            //      **不**证明「游戏里每天真的会抽」—— 后者要等到 `ROLL_INTERVAL` 边界,
            //      不属本件面内。假话只说到这里为止。
            MethodInfo trySpawn = threatChain.GetType().GetMethod("TrySpawnAtTick",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(trySpawn, "链须有公开 `TrySpawnAtTick`");
            try { trySpawn.Invoke(threatChain, new object[] { 6000L }); }
            catch (TargetInvocationException ex)
            {
                Assert.Fail("显式 tick=6000 调 `TrySpawnAtTick` 抛了:" + ex.InnerException?.Message);
            }

            PropertyInfo f1 = threatChain.GetType().GetProperty("LastF1Input",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f1, "`LastF1Input` 须是公开观测缝(链类已声明;<b>属性</b>,不是字段)");

            object raw = f1.GetValue(threatChain);
            Assert.IsNotNull(raw,
                "显式 tick=6000 之后 `LastF1Input` 仍为 null ⇒ 链在 F1 之前就返回了" +
                "(池空 / 抽数上限 / 预算),上下文门根本没被求值");
            var input = (F1PipelineInput)raw;

            Assert.IsTrue(input.IsInClinic,
                "玩家站在医馆格里,链读到的 `IsInClinic` 必须为 true —— " +
                "读到 false 只有两种可能:① 闭包又被写死(就是 `TD-randomevents-05` 的病);" +
                "② 医馆格判据与服务袋不是同一实例(同源义务破了)。");

            // 流水记忆:出诊目标今天恒无(池里 `is_house_call` 全 false)⇒ 这个 false 是**正确的当下值**,
            // 不是占位。它同时证明我们读的是求值结果,而不是一个恒定 true。
            Assert.IsFalse(input.IsOnExpedition,
                "`IsOnExpedition` 今日应为 false(无活跃出诊目标)—— 若为 true,说明有人给它造了假来源");
        }

        // ── 夹具管道 ────────────────────────────────────────────────────────

        private IEnumerator Boot()
        {
            _root = new GameObject("BootRoot_RandomEventGate");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveRootOverride = PlayModeTempSaveRoot.New("random_event_context_gate_test");

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");
        }

        private bool ReadBooted()
        {
            if (_boot == null) return false;
            FieldInfo f = typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null && (bool)f.GetValue(_boot);
        }

        private object Services() => typeof(BootRoot)
            .GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_boot);

        private static object Prop(object o, string name)
        {
            PropertyInfo p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(p, "服务袋缺属性 `" + name + "` —— 改名即须同步本夹具");
            return p.GetValue(o);
        }

        private static object Field(object o, string name) => o.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        private static PlayerController Player()
        {
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "Player" && all[i].scene.IsValid())
                    return all[i].GetComponent<PlayerController>();
            }
            return null;
        }

        /// <summary>在营地范围里找一个真医馆格(扫描面刻意小:医馆 18 槽平移到 x 97…102)。</summary>
        private static WorldPos FindClinicCell(ClinicCellSource clinic)
        {
            for (int x = 95; x <= 106; x++)
                for (int z = 0; z <= 20; z++)
                {
                    var c = new WorldPos(x, 0, z);
                    if (clinic.IsClinicCell(c)) return c;
                }
            Assert.Fail("营地范围里找不到医馆格 —— `ClinicCellSource` 未绑定结构注册表");
            return default;
        }

        private static void DestroyAllNamed(string name)
        {
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go != null && go.name == name && go.scene.IsValid())
                    UnityEngine.Object.Destroy(go);
            }
        }
    }
}
