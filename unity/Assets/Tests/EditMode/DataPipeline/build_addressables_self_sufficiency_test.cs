// `TD-ci-02` 的判据:Unity batchmode 只执行第一条 `-executeMethod`,其余静默丢弃。
//
// 债的本体不是「有个 bug 要修」,而是**两条命令的调用形态**会让第二步静默不跑、
// 退出码仍为 0。用户 2026-10-28 裁定修法 = ②自检 + ④Build 自足:
//   ④ `BuildAddressablesContent` 自己先调 `EnsureDataCoreGroupCore`(单条命令就够);
//   ② 构建前断言「组内条目数 == 盘上 *.cooked.bytes 件数」,不等即 throw。
//
// ⚠️ 本文件**只测可直测的那一半**:
//   - ④ 的「Ensure 真的被调用」由 `BuildAddressablesContentCore` 在**组缺失**时先抛
//     `EnsureDataCoreGroupCore` 的报文来证明 —— 该分支可注入不存在组名,零资产写入。
//   - ② 的「不等必抛 / 相等不抛」由纯谓词 `ThrowIfEntryCountMismatched` 直测。
//   ⚠️ **不测**「BuildPlayerContent 真的被调用了」—— 那会真构建,且落 Library/。
//     该边界如实登记,不借绿。

using System;
using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;
using UnityEditor.AddressableAssets;          // AddressableAssetSettingsDefaultObject(注入真设置)
using UnityEditor.AddressableAssets.Settings; // AddressableAssetSettings

namespace DaYiJingCheng.Tests.DataPipeline
{
    /// <summary>`TD-ci-02` 的修法判据(②自检 + ④Build 自足)。</summary>
    [TestFixture]
    internal sealed class build_addressables_self_sufficiency_test
    {
        // ══════════════ ② 自检:纯谓词两条分支 ══════════════

        /// <summary>条目数 ≠ 产物数 ⇒ 抛,且报文点名两个数。</summary>
        /// <remarks><b>判别力</b>:把 `ThrowIfEntryCountMismatched` 的 `if (inGroup == onDisk) return;`
        /// 删掉(或改成 `if (true) return;`)⇒ 本测红(不再抛)。</remarks>
        [Test]
        public void test_entryCountMismatch_throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => DataBakeMenu.ThrowIfEntryCountMismatched(3, 5, "Assets/DataCooked"),
                "组内 3 条 vs 盘上 5 件 ⇒ 必须抛");

            Assert.That(ex.Message, Does.Contain("3"), "报文须点名组内条目数");
            Assert.That(ex.Message, Does.Contain("5"), "报文须点名盘上产物数");
            Assert.That(ex.Message, Does.Contain("只执行第一条"),
                "报文须点名真因(batchmode 只跑第一条 executeMethod)—— 否则读者会以为是别的问题");
        }

        /// <summary>条目数 == 产物数 ⇒ 静默返回(不抛)。</summary>
        /// <remarks><b>判别力</b>:把 `if (inGroup == onDisk) return;` 删掉 ⇒ 本测红(变成无条件抛)。</remarks>
        [Test]
        public void test_entryCountMatch_doesNotThrow()
        {
            Assert.DoesNotThrow(
                () => DataBakeMenu.ThrowIfEntryCountMismatched(26, 26, "Assets/DataCooked"),
                "相等 ⇒ 不得抛(否则每次正常构建都会被拦)");
        }

        /// <summary>「组比盘上少」与「组比盘上多」**两侧**都抛(不是只拦一侧)。</summary>
        /// <remarks><b>判别力</b>:把判据写成 `if (inGroup &gt; onDisk) throw;` ⇒ 本测红。
        /// 两侧都要拦 —— 「组多了」意味着有条目指向不存在的产物,catalog 同样会坏。</remarks>
        [Test]
        public void test_entryCountMismatch_bothDirectionsThrow()
        {
            Assert.Throws<InvalidOperationException>(
                () => DataBakeMenu.ThrowIfEntryCountMismatched(2, 5, "Assets/DataCooked"),
                "组少于盘上 ⇒ 抛(catalog 缺条目)");

            Assert.Throws<InvalidOperationException>(
                () => DataBakeMenu.ThrowIfEntryCountMismatched(9, 5, "Assets/DataCooked"),
                "组多于盘上 ⇒ 抛(有条目指向不存在的产物)");
        }

        /// <summary>真种子读数:盘上 26 件,故 (26,26) 必须不抛、(26,25) 必须抛。</summary>
        /// <remarks><b>判别力</b>:若将来产物数变了而有人把常量写死,本测会红 —— 那是**预期的**,
        /// 因为它证明判据用的是**传入的数**而非内置常量。</remarks>
        [Test]
        public void test_entryCountMismatch_usesPassedValuesNotConstants()
        {
            Assert.DoesNotThrow(() => DataBakeMenu.ThrowIfEntryCountMismatched(7, 7, "x"),
                "任意相等的两个数都不该抛 —— 判据不得内置 26");
            Assert.Throws<InvalidOperationException>(
                () => DataBakeMenu.ThrowIfEntryCountMismatched(7, 8, "x"),
                "任意不等的两个数都该抛");
        }

        // ══════════════ ④ Build 自足:Build 会先跑 Ensure ══════════════

        /// <summary>
        /// 组缺失时,`BuildAddressablesContentCore` 抛的是 **Ensure 的报文** ⇒ 证明 ④ 真的先跑了 Ensure。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么这条能证 ④</b>:`BuildAddressablesContentCore` 自身**不检查组存在性** ——
        /// 它对组的唯一动作是 `CountGroupEntries`(组缺失时返回 0,不抛)。所以「组缺失 ⇒ 抛
        /// 『组定义已随仓库入库…git checkout』」这条报文**只可能来自 `EnsureDataCoreGroupCore`**
        /// ⇒ Ensure 确实在 Build 之前被执行了。</para>
        /// <para><b>判别力</b>:把 `BuildAddressablesContentCore` 里那行
        /// `EnsureDataCoreGroupCore(settings, cookedDirRel, groupName);` 删掉 ⇒ 本测红
        /// (不再抛 Ensure 的报文,而是走 ② 自检抛「条目数不等」,报文里没有 `git checkout`)。</para>
        /// <para>⚠️ <b>零资产写入</b>:注入一个**不存在**的组名 ⇒ Ensure 的组缺失分支在**任何写入之前**
        /// 就抛(该分支的「先验后改」性质由 `pipeline_gate_test::test_dcore_menu_groupMissing_throwsHard`
        /// 另行钉住)。本测不建组、不打标签。</para>
        /// </remarks>
        [Test]
        public void test_buildCore_runsEnsureFirst_groupMissing_throwsEnsureMessage()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null, "本机 Addressables 设置缺失(机器前置)");

            const string absent = "data-core__no_such_group_td_ci_02__";
            Assert.That(settings.FindGroup(absent), Is.Null, "本测要求该组名确不存在");

            var ex = Assert.Throws<InvalidOperationException>(
                () => DataBakeMenu.BuildAddressablesContentCore(
                    settings, $"Assets/{DataBakeMenu.CookedDirName}", absent),
                "组缺失时 Build 必须先撞 Ensure 的硬失败");

            Assert.That(ex.Message, Does.Contain("git checkout"),
                "报文须是 Ensure 那条(指向 git checkout)—— 若这里是 ② 自检的报文,说明 Ensure 没先跑");
            Assert.That(ex.Message, Does.Not.Contain("组条目数 ="),
                "不得是 ② 自检的报文 —— 那意味着 Ensure 没执行(④ 被删或顺序反了)");
        }

        /// <summary>设置缺失时,`BuildAddressablesContentCore` 自己就抛(不依赖 Ensure)。</summary>
        /// <remarks>
        /// <para>🔁 <b>2026-10-28 首版断言作废(自捕假绿)</b>:首版只断
        /// 「抛 <c>InvalidOperationException</c> 且报文含 <c>git checkout</c>」。那是**不够**的 ——
        /// `EnsureDataCoreGroupCore` 的分支 ① 会对同一个 `null` 抛**同类型**异常,报文也含
        /// <c>git checkout</c>。⇒ 把 Build 自己那段 <c>if (settings == null) throw</c> 删掉,
        /// 首版**照绿**。这正是「只断异常类型会被下层同类型异常冒充」那个失效模式。</para>
        /// <para><b>现判据 = 报文区分</b>:Build 的守卫报文止于「…恢复。」;Ensure 分支 ① 在其后
        /// 还有「;⚠️ 不要手建设置:…」。断言 <c>Does.Not.Contain("不要手建设置")</c>
        /// ⇒ 报文只可能来自 Build 自己的守卫。</para>
        /// <para><b>判别力</b>:删掉 Build 那段 <c>if (settings == null) throw</c> ⇒ Ensure ① 接管,
        /// 报文含「不要手建设置」⇒ 本测红。</para>
        /// </remarks>
        [Test]
        public void test_buildCore_settingsMissing_throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => DataBakeMenu.BuildAddressablesContentCore(
                    null, "Assets/DataCooked", "data-core"),
                "设置缺失必须抛");

            Assert.That(ex.Message, Does.Contain("git checkout"), "报文须给恢复路径");
            Assert.That(ex.Message, Does.Not.Contain("不要手建设置"),
                "不得是 Ensure 分支 ① 的报文 —— 那意味着 Build 自己的 null 守卫没执行(被删了)");
        }

        /// <summary>
        /// **顺序判据**:④ 必须排在 ② 之前 —— 空产物目录 + 真组名时,
        /// 应先撞 Ensure 的「无 *.cooked.bytes」而不是 ② 的「条目数不等」。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么需要这条</b>:若把 ② 挪到 ④ 前面,则在「盘上新烘了产物、组还没登记」
        /// 这个**最常见**情形下会直接抛自检 —— 而那恰恰是 Ensure 存在的理由。
        /// 顺序错了不会让任何其他夹具变红(两者都抛),所以必须专门钉。</para>
        /// <para><b>判别力</b>:交换两段 ⇒ 本测红(报文变成 ② 的「组条目数 =」)。</para>
        /// <para>⚠️ 注入空目录 ⇒ Ensure 的分支 ② 在**任何写入之前**抛,零资产写入。</para>
        /// </remarks>
        [Test]
        public void test_buildCore_ensureRunsBeforeSelfCheck_emptyDirThrowsEnsureMessage()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null, "本机 Addressables 设置缺失(机器前置)");

            const string emptyDir = "Assets/__no_such_cooked_dir_td_ci_02__";
            var ex = Assert.Throws<InvalidOperationException>(
                () => DataBakeMenu.BuildAddressablesContentCore(settings, emptyDir, "data-core"),
                "空产物目录 ⇒ Ensure 必须先抛");

            Assert.That(ex.Message, Does.Contain(emptyDir),
                "报文须点名出错的目录(Ensure 分支 ② 的形态)");
            Assert.That(ex.Message, Does.Not.Contain("组条目数 ="),
                "不得是 ② 自检的报文 —— ④ 必须排在 ② 之前");
        }
    }
}
