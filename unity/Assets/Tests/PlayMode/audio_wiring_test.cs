// 权威来源:production/epics/audio-system/story-018-runtime-audio-assembly.md
//   (AC-018-3 / AC-018-4 / AC-018-5 / AC-018-6 的**实机半边**)
// ADR-018 §一(44 只触发 / 只渲染)· §三(七总线)· ADR-028 ①(池唯一所有者 = 44)
// ADR-014 §五(首次 Step 前预载)· ADR-023 §①(Boot 常驻场景)
//
// ⚠️ **为什么必须住 PlayMode**:本 story 的四个缺口(A-1/A-2/A-4/A-5/A-7)全是
//    **启动序接线** —— 它们的可观测面(混音器解析、clip 装载、池根存在、cue 真发出)
//    只在**真实启动路径**(Addressables catalog + 场景 + tick 泵)里才存在。
//    EditMode 半边(`runtime_assembly_test`)已把纯逻辑判据钉住;本文件钉的是
//    「启动序真的把它们接起来了」。
//
// ⚠️ **形态承 `patient_appeared_wiring_test`**:真启动序 + 反射观测 + 显式卸载 World。
//    `captureDeltaTime` 是全局态 ⇒ `[UnityTearDown]` 恢复 0(防污染同套件其他测试)。
//
// ⚠️ **NOT-RUN 登记(禁借绿)**:
//    · **实机听测**(AC-018 的「至少一条 cue 可闻」)—— 自动化**不可判**(听感),
//      本文件只断言「cue 走到了渲染面且池内有活动源」;人耳判据落
//      `production/qa/evidence/audio-wiring-listening-<日期>.md`。
//    · **真实 `AudioClip.loadState` 的听诊预载半边** —— 承 story-010 裁定 ③ 的 NOT-RUN 哨兵,
//      不在本件面内(见 story §Out of Scope)。
//    · ⚠️ **生产路径「病人会咳/会呼吸」的上游阻塞(2026-10-18 已修,留作闭环记录)** ——
//      诊断 AC-018-6 实机半边时发现:9 侧 `Trend` 是已登记的 tripwire 占位(恒 0),
//      而 13 侧 `BehaviorMap.EvaluateTerminal` 的痊愈判据是 `position ≤ RecoverMax ∧ trend ≤ 0`
//      ⇒ **任何新出现的病人(position≈0)在首拍即被锁存「痊愈」终局** ⇒ 呼吸层不起、cue 恒不发
//      (实机取证:`tick=2..40 p=1 tier=Recover→Severe term=True emit=False breath=None`)。
//      用户裁定「本轮就修」⇒ 已加 `BehaviorMap.TrendWired` 门控(未接线期间**整条痊愈锁存停用**;
//      禁用 ≠ 误判),并同步 13 侧 NOT-RUN 哨兵 `behavior_map_test::test_ac13b1_...`。
//      ⇒ 本文件的 Assert ①b 自本轮起**可判**(在场病人须起呼吸层或发过一次性 cue)。

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>44 运行期音频链的启动序接线验证(story-018)。</summary>
    public class AudioWiringTest
    {
        private const float BootTimeoutSeconds = 20f;

        /// <summary>取证帧数(capture 1/60 下 ≈ 2 s;病人出现 + 首拍呼吸层在该窗内必到)。</summary>
        private const int SampleFrames = 120;

        private GameObject _root;
        private BootRoot _boot;
        private bool _captureActive;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_captureActive)
            {
                Time.captureDeltaTime = 0f;
                _captureActive = false;
            }

            // ⚠️ **不能用 `GameObject.Find`** —— 它只查 **active** 对象,而预摆 Main Camera
            //    已被启动序 `SetActive(false)`;`Find` 返回 null ⇒ 相机跨测累积 ⇒
            //    下一条测的 AudioListener 单挂点断言看到 2/3/4… 个(2026-10-17 实测:
            //    本文件首版 5 条里 4 条红于此)。走 `FindObjectsOfTypeAll` 连 inactive 一起收。
            DestroyAllNamed("Main Camera");
            DestroyAllNamed("CameraRig");
            DestroyAllNamed("Player");

            AsyncOperationHandle<SceneInstance> handle = default;
            bool valid = false;
            if (_boot != null)
            {
                try
                {
                    var field = typeof(BootRoot).GetField("_worldLoadHandle",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    handle = (AsyncOperationHandle<SceneInstance>)field.GetValue(_boot);
                    valid = handle.IsValid();
                }
                catch (Exception) { valid = false; }
            }

            if (valid)
            {
                var unload = Addressables.UnloadSceneAsync(handle);
                while (!unload.IsDone)
                    yield return null;
            }

            if (_root != null) UnityEngine.Object.Destroy(_root);
            yield return null;
        }

        // ══════════════ 观测面 ══════════════

        /// <summary>销毁场景内所有同名的 GameObject(含 **inactive** —— 见 <see cref="TearDown"/> 的说明)。</summary>
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

        private static object PrivateField(object target, string name)
        {
            Assert.That(target, Is.Not.Null, "观测对象为 null —— 启动序未完成?");
            FieldInfo f = target.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(f, Is.Not.Null, $"字段 {name} 缺失 —— 被重命名?夹具须随之订正");
            return f.GetValue(target);
        }

        private CompositionRootServices Services() => (CompositionRootServices)PrivateField(_boot, "_services");

        private bool ReadBooted()
        {
            if (_boot == null) return false;
            FieldInfo f = typeof(BootRoot).GetField("_booted",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null && (bool)f.GetValue(_boot);
        }

        /// <summary>真启动序(与门④冒烟 / 病人出现接线同源)。
        /// <para>⚠️ <b>必须先造预摆 Main Camera</b>(承 <c>camera_follow_wiring_test</c> 同纪律):
        /// 启动序第 5 步会禁用它并断言「恰好 1 个 AudioListener」;不补这个 fixture ⇒
        /// listener = 0 ⇒ 断言按设计抛(fail-loud 是对的,是测试环境缺了生产场景本有的东西)。
        /// 2026-10-17 实测:首版缺此 fixture,本文件 5 条全红于该断言。</para></summary>
        private IEnumerator BootAndSettle()
        {
            var presetCamObj = new GameObject("Main Camera");
            presetCamObj.AddComponent<Camera>();
            presetCamObj.AddComponent<AudioListener>();

            _root = new GameObject("BootRoot_AudioWiring");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            Time.captureDeltaTime = 1f / 60f;
            _captureActive = true;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-3 / AC-018-4 —— 启动后渲染面真装配 + 池根唯一
        // 判别力:删掉启动序 ②.6 的 `BuildAudioSeams` 调用(或 `audio:` 实参)⇒ 本测红。
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_ac018_3_bootAssemblesRenderChain_andSinglePoolRoot()
        {
            yield return BootAndSettle();

            CompositionRootServices services = Services();

            // Assert ①:A-1/A-2/A-4 —— 渲染面**真**装配(不是 NullRenderTarget 链)
            Assert.IsTrue(services.AudioRenderAssembled,
                "启动后 AudioRenderAssembled 必须为 true —— 为 false 说明 ②.6 未接或 catalog 缺表");
            Assert.IsNotNull(services.AudioSink, "A-1:CueRenderSink 生产构造点");
            Assert.IsNotNull(services.AudioPool, "A-4:AudioSourcePool 生产构造点");
            Assert.IsNotNull(services.AudioPool.Root, "池根须已建(ADR-028 ② 零场景预摆 ⇒ 运行期建)");

            // Assert ②:AC-018-4 单池不变量 —— 场景内名为 AudioSourcePool 的根恰一个
            // ⚠️ 走 `Resources.FindObjectsOfTypeAll`:池根带 `HideFlags.DontSave`,
            //    `Object.FindObjectsByType` 不返回 DontSave 对象(EditMode 半边已实测假红)。
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            int poolRootCount = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == AudioSourcePool.RootName && all[i].scene.IsValid())
                    poolRootCount++;
            Assert.AreEqual(1, poolRootCount, "单池不变量:运行期池根恰一个");

            Debug.Log($"[AudioWiring] AudioRenderAssembled=true · 池根数={poolRootCount} · " +
                      $"活动源={services.AudioPool.ActiveCount}");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-5 —— 混音器解析 + 玩家音量落到出厂默认
        // 判别力:删掉 `PlayerBusVolumeInitializer.MixerResolver` 赋值 ⇒ 首条断言红。
        // ⚠️ 「删掉 `ApplyDefaults` 显式调用」的判别力**不在本条**(值断言在 PlayMode 同样
        //    分辨不出:出厂默认 = 0 dB = 资产初值)—— 它由 EditMode 半边的
        //    `seams.BusVolumeErrors` 断言承载(`runtime_assembly_test.cs`)。
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_ac018_5_mixerResolved_andMasterVolumeIsFactoryDefault()
        {
            yield return BootAndSettle();

            // Assert ①:resolver 在生产路径被赋值(A-5/A-7)
            Func<AudioMixer> resolver = PlayerBusVolumeInitializer.MixerResolver;
            Assert.IsNotNull(resolver,
                "A-5:生产路径必须赋值 MixerResolver —— 否则 Bootstrap 静默让位,音量滑块全失效");
            AudioMixer mixer = resolver();
            Assert.IsNotNull(mixer,
                "A-7:混音器解析不到 —— 检查 audio Addressables 组是否登记 DaYiJingCheng.mixer 且已重建 catalog");

            // Assert ②:启动后 7 个玩家参数 = 出厂默认(证明显式 ApplyDefaults 跑过)
            foreach (string param in MixerRegistry.BusVolumeParameters)
            {
                Assert.IsTrue(mixer.GetFloat(param, out float actual),
                    $"混音器缺 exposed 参数「{param}」");
                float expected = float.NaN;
                for (int i = 0; i < PlayerBusVolumeInitializer.FactoryDefaults.Count; i++)
                    if (PlayerBusVolumeInitializer.FactoryDefaults[i].Key == param)
                        expected = PlayerBusVolumeInitializer.FactoryDefaults[i].Value;
                Assert.IsFalse(float.IsNaN(expected), $"出厂默认集缺「{param}」");
                Assert.AreEqual(expected, actual, 0.01f,
                    $"「{param}」未落到出厂默认 —— ApplyDefaults 那一步没跑(A-5)");
            }

            Debug.Log("[AudioWiring] 混音器已解析 · 7 个玩家总线参数 = 出厂默认");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-2 实机半边 —— clip 面真装载(0 缺)
        // 判别力:让 `AudioClipLibrary.Preload` 变成空实现 ⇒ LoadedCount=0 ⇒ 红。
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_ac018_2_clipsPreloaded_noMissingCues()
        {
            yield return BootAndSettle();

            var seams = (AudioAssemblySeams)PrivateField(_boot, "_audioSeams");
            Assert.IsNotNull(seams, "启动序须产出装配缝(provider 预载成功时)");
            Assert.IsNotNull(seams.Table, "装配缝须携带事件表(ordinal 解析面)");

            AudioClipLibrary library = seams.Clips;
            Assert.IsNotNull(library, "生产 clip 面 = AudioClipLibrary");

            int rows = seams.Table.Value.RowCount;
            Assert.Greater(rows, 0, "空表 ⇒ 断言恒真(禁借绿)");

            // Assert:0 缺 —— 素材存在性已由构建期 D6 门拦下;运行期 miss = 装配错 / catalog 陈旧
            Assert.IsEmpty(library.MissingCues,
                "有 cue 装载不到 clip —— 多半是 Addressables 内容未重建(跑「构建 Addressables 内容」)");
            Assert.AreEqual(rows, library.LoadedCount,
                "装载数须等于表行数(逐行一条 clip)");

            Debug.Log($"[AudioWiring] clip 装载 {library.LoadedCount}/{rows},缺 0");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-6 实机半边 —— 病人 cue 驱动在真实 tick 泵上跑
        // 判别力:删掉 `Update()` 里的 `PatientCueDriver.OnTickEdge` 调用 ⇒ 本测红。
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_ac018_6_patientCueDriver_runsOnRealTickPump()
        {
            yield return BootAndSettle();

            CompositionRootServices services = Services();
            PatientCueDriver driver = services.PatientCueDriver;
            Assert.IsNotNull(driver, "AC-018-6:病人 cue 驱动须在服务袋内");

            // Act:跑 2 s —— 病人在启动序末出现,首拍呼吸层在该窗内必到
            for (int i = 0; i < SampleFrames; i++)
                yield return null;

            Debug.Log($"[AudioWiring] cue 驱动:末驱动 tick={driver.LastDrivenTick} · " +
                      $"一次性发射={driver.EmitCount} · 存活呼吸层={driver.ActiveBreathCount} · " +
                      $"在场={services.Presence.PresentCount}");

            // Assert ①:A-3 的实机半边 —— 调度器**真的在 tick 泵上被驱动**。
            //  ⚠️ 判据 = `LastDrivenTick`,**不是** `EmitCount`/`ActiveBreathCount`。
            //     后两者是**下游结果**,会被上游条件压成 0(2026-10-18 实测:在场病人
            //     因 `Trend` tripwire=0 撞上 `EvaluateTerminal` 的 `trend<=0` 分支,
            //     在首拍即锁存「痊愈」⇒ 呼吸层/咳嗽恒不发 —— 见下方 NOT-RUN 登记)。
            //     单看它们**无法区分**「没接上 tick 泵」与「接上了但上游不产 cue」,
            //     故对本条 AC 的判别力必须落在「泵是否驱动过」这个直接面上。
            //  ⚠️ 判别力(证伪轮 P3):删掉 `Update()` 里的 `PatientCueDriver.OnTickEdge` 调用
            //     ⇒ `LastDrivenTick` 恒 −1 ⇒ 本断言转红。
            Assert.Greater(driver.LastDrivenTick, 0L,
                "cue 驱动从未被 tick 泵驱动过(LastDrivenTick 恒 −1)—— 说明 Update() 没调它(A-3 未接)");
            Assert.LessOrEqual(driver.LastDrivenTick, services.TickDriver.CurrentTick,
                "驱动 tick 不得超前于 tick 泵当前 tick");

            // Assert ①b:**真产物** —— 在场病人须已起呼吸层循环(或已发过一次性 cue)。
            //  ⚠️ 2026-10-18 起本断言**可判**:此前病人因 `Trend` tripwire=0 撞上
            //     `BehaviorMap.EvaluateTerminal` 的痊愈判据而在首拍即终局(呼吸层恒不起);
            //     该缺陷已修(`BehaviorMap.TrendWired` 门控痊愈锁存,见其注)⇒
            //     非终局病人在被驱动的那一刻就 `BreathAction.Begin`。
            //  ⚠️ 判别力:删掉 `ApplyBreath` 的 Begin 分支 / 删掉 `_breathCue` 解析 ⇒ 转红。
            if (services.Presence.PresentCount > 0)
            {
                Assert.IsTrue(driver.ActiveBreathCount > 0 || driver.EmitCount > 0,
                    $"在场 {services.Presence.PresentCount} 人,但呼吸层与一次性 cue 均为 0 —— " +
                    "上游未产出可发状态(终局锁存?建档门?)");
            }

            // Assert ②:池须常驻(装配产物)
            Assert.IsNotNull(services.AudioPool, "池须常驻");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-018-6 实机半边 —— cue 真走到渲染面(池内有活动源)
        // ⚠️ 「可闻」不可自动判(听感)⇒ 本测只判**渲染面被调用**,人耳判据落听测记录件。
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_ac018_6_cueReachesRenderTarget_poolBecomesActive()
        {
            yield return BootAndSettle();

            CompositionRootServices services = Services();
            var seams = (AudioAssemblySeams)PrivateField(_boot, "_audioSeams");
            AudioClipLibrary library = seams.Clips;

            // Arrange:直接经装配好的 sink 发一条**真装载过的** cue(取呼吸基础层 —— 必在表内)
            Assert.IsNotNull(library, "clip 面须已装配");
            Assert.IsTrue(library.BusOf(0) != null || seams.Table.Value.RowCount > 0,
                "事件表须非空");

            int cueOrdinal = 0;                       // 表第 0 行(Voice_Cough_Damp)
            AudioClip clip = library.Resolve(cueOrdinal);
            Assert.IsNotNull(clip,
                "表第 0 行的 clip 未装载 —— 跑「构建 Addressables 内容」后重试(A-2 实机半边)");

            int before = services.AudioPool.ActiveCount;

            // Act:经 **IAudioCueSink**(44 的唯一入口)发一条一次性 cue
            services.AudioSink.Emit(new AudioCueDto(
                cue: cueOrdinal, intensity: 128,
                cell: new Int3(0, 0, 0), source: 9001, looped: false));

            yield return null;

            // Assert:池内多了一条活动源 = cue 真的走到了引擎对象(不是 NullRenderTarget 空转)
            Assert.Greater(services.AudioPool.ActiveCount, before,
                "cue 未在池内造出活动源 —— 渲染面是空转的(A-2 未接)");
            Assert.AreEqual("Voice", services.AudioPool.BusForActor(9001),
                "总线归属须取自事件表 bus 列(A-6)");

            Debug.Log($"[AudioWiring] cue#{cueOrdinal} → 池活动源 {before}→{services.AudioPool.ActiveCount},bus=Voice");
        }
    }
}
