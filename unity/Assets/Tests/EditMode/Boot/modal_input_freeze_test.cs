// 模态期输入冻结(2026-10-31)—— 规则七遗漏的**移动轴**那半边。
//
// 权威:design/gdd/interaction-system.md 规则七 · ADR-013 §十 / §十-C(42 是模态真源)
//
// ── 本件钉什么 ─────────────────────────────────────────────────────
// ① 无模态 ⇒ 移动轴原样透传(**正控**:否则「挡路就折零」在下述用例里恒真而不证明任何事);
// ② 有挡路模态 ⇒ 轴折零(暂停壳摊开时玩家不该还能走);
// ③ 提供者为 null ⇒ 不冻结(「无 UI ⇒ 无模态」的诚实降级);
// ④ **实时读,零缓存** —— 同一帧内两个方向各问一次,必须各自如实。
//
// ⑤ **接线判据**(2026-10-31 补):`BootRoot.Update` 的设备读面只许出现一次,且必须作为
//    `ResolveMoveAxis` 的实参 —— 扫源码守此点(同 `build_controller_test` 的 AC B-1 / B-7
//    源码扫描手法)。⚠️ 它证明的是**源码里接线在且唯一**,**不**证明「Unity 每帧调 Update」
//    (引擎契约,不是本仓代码)—— 两者不得混读。
//
// ⚠️ `BuildInputDriver` 的挡路分支**不在本件**:它要真 `BuildController` 才能构造驱动,
//    判据落在 `build_controller_test` 里(那棵树已有真装配,不复制第二套)。

using System.IO;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Boot;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>模态期移动轴的冻结面。</summary>
    [TestFixture]
    public sealed class ModalInputFreezeTest
    {
        private static readonly Vector2 Axis = new Vector2(0.7f, -0.7f);

        [Test]
        public void test_freeze_notBlocked_passesMoveAxisThrough()
        {
            var freeze = new ModalInputFreeze(() => false);

            Assert.That(freeze.IsBlocked, Is.False);
            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Axis),
                "无模态时不得改轴 —— 否则「冻结生效」这条判据在下一条里毫无判别力");
        }

        [Test]
        public void test_freeze_blocked_zeroesMoveAxis()
        {
            var freeze = new ModalInputFreeze(() => true);

            Assert.That(freeze.IsBlocked, Is.True);
            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Vector2.zero),
                "暂停壳摊开时玩家不该还能走(规则七漏掉的移动轴)");
        }

        [Test]
        public void test_freeze_nullPredicate_neverBlocks()
        {
            // 诚实降级:没有 UI ⇒ 没有模态 ⇒ 不冻结。
            // ⚠️ 与「假装有一条模态开着」相对 —— 后者会让玩家在世界里彻底动不了。
            var freeze = new ModalInputFreeze(null);

            Assert.That(freeze.IsBlocked, Is.False);
            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Axis));
        }

        // ══════════════════ ⑤ 接线判据 ══════════════════

        [Test]
        public void test_bootRoot_resolveMoveAxis_foldsOnlyWhenBlocked()
        {
            // 行为面(纯函数,不需要 BootRoot 实例)
            Assert.That(BootRoot.ResolveMoveAxis(Axis, new ModalInputFreeze(() => true)),
                Is.EqualTo(Vector2.zero), "挡路 ⇒ 折零");
            Assert.That(BootRoot.ResolveMoveAxis(Axis, new ModalInputFreeze(() => false)),
                Is.EqualTo(Axis), "正控:不挡路时必须原样透传");
            Assert.That(BootRoot.ResolveMoveAxis(Axis, null), Is.EqualTo(Axis),
                "无冻结面 ⇒ 诚实降级(不假装挡路)");
        }

        [Test]
        public void test_bootRoot_update_readsMoveAxisOnlyThroughResolveMoveAxis()
        {
            // ⚠️ 读的是**真源码**,不是内联一份 —— 本判据要证的正是「生产文件里接线在」。
            string root = RepoRoot();
            string code = StripComments(File.ReadAllText(
                Path.Combine(root, "unity/Assets/Gameplay.Boot/BootRoot.cs")));

            int reads = Regex.Matches(code, @"MovementInputReader\.ReadMoveAxis\(\)").Count;
            Assert.That(reads, Is.EqualTo(1),
                $"设备读面必须**恰出现一次**(经 `ResolveMoveAxis`),实测 {reads} 次 —— "
                + "多出来的那处就是绕过冻结面的第二条路");

            Assert.That(code, Does.Contain("ResolveMoveAxis(MovementInputReader.ReadMoveAxis()"),
                "唯一那次读数必须**作为 `ResolveMoveAxis` 的实参**(否则冻结面形同虚设)");

            // `PumpFrame` 的实参必须是折过零的局部 `moveAxis`,不得直接喂设备读数。
            int pump = code.IndexOf("MovementFeed.PumpFrame(", System.StringComparison.Ordinal);
            Assert.That(pump, Is.GreaterThanOrEqualTo(0), "PumpFrame 调用点不见了(装配形态变了?)");
            int pumpEnd = code.IndexOf(");", pump, System.StringComparison.Ordinal);
            string args = code.Substring(pump, pumpEnd - pump);
            Assert.That(args, Does.Contain("moveAxis"), "PumpFrame 必须喂冻结后的轴");
            Assert.That(args, Does.Not.Contain("ReadMoveAxis"), "PumpFrame 不得直接喂设备读数(绕过冻结)");
        }

        /// <summary>仓库根(`Application.dataPath` = `&lt;repo&gt;/unity/Assets`)。</summary>
        private static string RepoRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(src, @"//[^\n]*", " ");
        }

        [Test]
        public void test_freeze_readsLive_doesNotCache()
        {
            // 缓存 = 第二真源:模态在帧内开合时读到的是上一帧的答案。
            bool blocked = false;
            var freeze = new ModalInputFreeze(() => blocked);

            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Axis), "先问一次(未挡)");

            blocked = true;
            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Vector2.zero),
                "同一实例,状态下一次调用就必须变 —— 缓存过的实现会在这里给旧答案");

            blocked = false;
            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Axis), "关屏立刻恢复");
        }
    }
}
