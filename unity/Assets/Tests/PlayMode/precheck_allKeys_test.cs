// 预检「缺一条 ⇒ 短路」的行为判据(2026-10-17 · 残留修复)。
//
// 为什么必须住 PlayMode:预检跑在 `DataCorePreloader.TryPreload()` 里,而它读的是**进程内**
// `Addressables.ResourceLocators` —— EditMode 能换 catalog 设置,却换不了「当前已注册的 locator 集」;
// 只有 Play 里的真实启动路径能走到。故本测在 Play 开始后注入一条**必然缺失**的地址,直驱预检。
//
// 它同时钉住两件事:
//   ① 缺失项被**逐条列出**进 `LastPreloadDiagnostic`(承 TD-audio-01f:失败面必须可观察,
//      不得靠「数 [Error] 日志条数」);
//   ② 失败时**静态状态复位** —— `IsPreloaded` 不得残留为 true(PlayMode 9 红的第三处机制)。
// ⚠️ ② 本测**不覆盖**(夹具已在每条测前复位 ⇒ 两者不可区分);其源码存在性另由 EditMode
//    接线断言 grep 钉住,行为半边如实登记为 NOT-RUN。
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Settings;
using DaYiJingCheng.Gameplay.Boot;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>预检全检行为的 PlayMode 判据。</summary>
    internal sealed class PrecheckAllKeysTest
    {
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>地址表里混入一条不存在 ⇒ 预检 false + 诊断点名该条。</summary>
        /// <remarks>
        /// <b>判别力</b>:把 `AllDataCoreKeysLocatable` 的 `if (!KeyLocatable) return false` 删掉(恒 true)⇒ 首条断言红;
        /// 把诊断恒置 `"missing=[]"` ⇒ 第二条红。
        /// <para>⚠️ <b>覆盖不到</b>:「失败时静态标志复位」在本测里与夹具(每条测前已复位)不可区分 ——
        /// 该半边的判别力由 `tests` 外的证伪轮记录(回退 `TryPreload` 失败分支的 `ResetForPlayModeTests()`
        /// ⇒ 本条**仍绿**),不得据此声称已钉住。</para>
        /// </remarks>
        [Test]
        public void test_precheck_missingAddress_returnsFalse_andDiagnosesIt()
        {
            Type t = typeof(DataCorePreloader);
            string[] table = (string[])t.GetField("DataCoreAddresses", AnyStatic).GetValue(null);
            Assert.That(table, Is.Not.Null.And.Not.Empty, "扫描键非空守卫");

            var withGhost = new string[table.Length + 1];
            table.CopyTo(withGhost, 0);
            const string ghost = "zz_precheck_probe.never.exists.cooked.bytes";
            withGhost[table.Length] = ghost;

            MethodInfo locate = t.GetMethod("AllDataCoreKeysLocatable", AnyStatic);
            MethodInfo diagnose = t.GetMethod("DiagnoseDataCoreKeys", AnyStatic);
            Assert.That(locate, Is.Not.Null, "AllDataCoreKeysLocatable 缺失 —— 预检面被删?");
            Assert.That(diagnose, Is.Not.Null, "DiagnoseDataCoreKeys 缺失 —— 失败诊断面被删?");

            // Arrange:清静态状态,确保失败路径的「复位」断言测的是**本方法内发生的事**。
            t.GetMethod("ResetForPlayModeTests", AnyStatic).Invoke(null, null);
            Assert.That(DataCorePreloader.IsPreloaded, Is.False);
            // Act
            bool accepted = (bool)locate.Invoke(null, new object[] { withGhost });
            string diag = (string)diagnose.Invoke(null, new object[] { withGhost, null });

            // Assert
            Assert.That(accepted, Is.False, "地址表含一条不可解析的地址 ⇒ 预检必须判死(否则装载中途抛、[Error] 已打出)");
            Assert.That(diag, Is.EqualTo("missing=[" + ghost + "]"), "诊断须点名缺失的那条,且真地址全部可解析");
        }

        /// <summary>真实 catalog 下的正向半边:全部真地址(2026-10-20 补接线后 **11** 条)全部可解析
        /// (与负向同一次运行内对照)。
        /// <para>⚠️ 本测是「组条目 ↔ 真 catalog」的唯一行为面 —— 11 条地址里**任一条**未进
        /// `data-core` 组,本测即红(且报文点名诊断)。2026-10-20 新接线的 2 份
        /// (`interaction_kinds` / `diagnosis_negative_confidence`)若组条目漏登,红的就是本测。</para></summary>
        [Test]
        public void test_precheck_liveCatalog_allRealAddressesLocatable()
        {
            Type t = typeof(DataCorePreloader);
            var table = (string[])t.GetField("DataCoreAddresses", AnyStatic).GetValue(null);
            bool ok = (bool)t.GetMethod("AllDataCoreKeysLocatable", AnyStatic).Invoke(null, new object[] { table });
            string diag = (string)t.GetMethod("DiagnoseDataCoreKeys", AnyStatic).Invoke(null, new object[] { table, null });
            Assert.That(ok, Is.True, "当前 Play 内 catalog 应解析全部 data-core 地址 —— 不等即本机需重建 catalog,诊断:" + diag);
        }

        /// <summary>接线半边:<c>preloaded = true</c> 但 <c>provider = null</c> ⇒ 不注入(回退行)。</summary>
        /// <remarks>
        /// 这是 PlayMode 9 红第三处机制留下的一半:修法把 <c>BootRoot.Start</c> 的抛形属性求值
        /// 摘进三元短路,摘出来的接线本身仍须证明「null 不注入」。
        /// <b>判别力</b>:删掉 `ApplyAudioSettingsProvider` 里的 `|| provider == null` ⇒ 首条断言红;
        /// 删掉 `!preloaded` ⇒ 第二条红。
        /// </remarks>
        [Test]
        public void test_bootRoot_applyAudioSettingsProvider_nullOrNotPreloaded_doesNotInject()
        {
            Func<IReadOnlyList<DaYiJingCheng.Gameplay.Presentation.Settings.SettingsRegistryRow>> saved =
                AudioSettingsRegistry.Provider;
            try
            {
                AudioSettingsRegistry.Provider = null;
                BootRoot.ApplyAudioSettingsProvider(preloaded: true, provider: null);
                Assert.That(AudioSettingsRegistry.Provider, Is.Null,
                    "preloaded=true 但 provider=null ⇒ 必须不注入(否则 Rows 取到失效 provider)");

                AudioSettingsRegistry.Provider = null;
                BootRoot.ApplyAudioSettingsProvider(preloaded: false, provider: null);
                Assert.That(AudioSettingsRegistry.Provider, Is.Null,
                    "preloaded=false ⇒ 必须不注入");
            }
            finally
            {
                AudioSettingsRegistry.Provider = saved;
            }
        }
    }
}
