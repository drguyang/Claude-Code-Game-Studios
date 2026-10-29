// 权威来源:docs/tech-debt-register.md `TD-audio-031`(两半)
//   · ① 静态装配缝生产赋值后无回收:`AudioSettingsRegistry.Provider` /
//     `PlayerBusVolumeInitializer.MixerResolver` 皆 `public static`,而 `OnDestroy` 不置 null
//     ⇒ 跨 Play 轮悬垂指向已 `Addressables.Release` 的对象(今日靠**完整域重载**巧合兜底)
//   · ② `BuildAudioSeams` 异常路径泄漏预载资产:`Preload()` 成功之后若 `new AudioBusResolver`
//     (缺组)抛 ⇒ 已装载的 clip 无 `Dispose`
//   · ADR-023 §⑤(拆序六步:停 Step → flush → ReleaseInstance → Release handle → Unload → 断言)
//   · ADR-018 §一(44 只渲染;句柄由唯一持有者负责收尾)
//   · 裁定:2026-10-29(本债两半同批修)
//
// ⚠️ **本夹具钉三件事**:
//   ① 缺组抛路径上,**已预载的 clip 被释放**(逐个 clip 计数,不是布尔);
//   ② 成功路径上**不**释放(否则成功路径会把活的 clip 库销毁 —— 反向守卫);
//   ③ `OnDestroy` 把两条静态缝置 null —— 源文本判据(删那两行 ⇒ 红)。
//
// ⚠️ **① 的判别力来自 `clipLoader` / `clipRelease` 注入缝**:EditMode 无 catalog ⇒ 生产路径
//   下 `Preload()` 的 `_handles` 恒空、`Dispose` 是空操作 ⇒ **零判别力**(假绿)。
//   经缝装来的 clip 由夹具持有,释放走 `internalRelease` 通道,故「释放了几个」可数。
//
// ⚠️ **A-5 那条抛路径今日在 EditMode 不可达,本夹具不钉它**(如实登记):
//   `ApplyDefaults` 的 errors 来自 `ValidateDefaults(keys)` —— 它校验的是**默认集列表的键**,
//   而 `FactoryDefaults` 是**代码常量集**(键集固定且合法)⇒ 生产路径上该分支**恒不抛**。
//   在混音器上写错参数名**触发不了**它(`SetFloat` 的返回值被忽略)。
//   ⇒ 「A-5 抛路径也释放 clip」由 ① 的同一个 try/catch **结构上覆盖**(两条抛点在同一 try 里),
//     但**没有独立夹具**;如实记在这里,不假装钉过。
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 零写盘(只读一份源文件)。

using System;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Settings;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>`TD-audio-031`:静态装配缝回收 + 异常路径 clip 释放。</summary>
    [TestFixture]
    public sealed class AudioSeamReleaseTest
    {
        private const string MixerAssetPath = "Assets/Audio/DaYiJingCheng.mixer";

        // ══════════════ ① 缺组抛路径释放 clip ══════════════

        [Test]
        public void test_td031_2_busResolveFailure_releasesPreloadedClips()
        {
            // Arrange:总线解析**必抛**(等价于生产里「某条组漏登记」那个分支)。
            // ⚠️ 为什么用注入缝而不是真造一个缺组混音器:见文件头注 —— EditMode 下
            //    `AudioMixer` 不能 `CreateInstance`、`AudioMixerController` 不在公共 API,
            //    改盘上资产不可接受 ⇒ 缺组混音器**造不出来**,该 catch 只能经缝跑到。
            AudioMixer mixer = RealMixer();
            var clips = new ClipLedger();
            var provider = new TableProvider(BakedTable());

            // Act
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => BootRoot.BuildAudioSeams(provider, mixerLoader: () => mixer,
                                               clipLoader: clips.Load, clipRelease: clips.Release,
                                               busResolverFactory: _ => throw new InvalidOperationException(
                                                   "[A-6] 混音器内找不到注册总线组:Music —— 本夹具注入的必抛")));

            // Assert ①:确实是缺组那条(不是别的异常冒充)
            Assert.That(ex.Message, Does.Contain("A-6"),
                "抛的不是缺组那条 —— 本测的前提(缺组路径)不成立");

            // Assert ②:**已装进来的每一个 clip 都被释放**(逐个计数,不是布尔)
            Assert.That(clips.Loaded, Is.GreaterThan(0),
                "前置:clip 装载缝一个都没被调用 ⇒ 本测测的是空集(零判别力)");
            Assert.That(clips.Released, Is.EqualTo(clips.Loaded),
                $"已装载 {clips.Loaded} 个 clip,只释放了 {clips.Released} 个 —— " +
                "抛出去之后没人持有 clips 库,漏掉的句柄跨 Play 悬挂(TD-audio-031 ②)");
        }

        [Test]
        public void test_td031_2_successPath_doesNotRelease()
        {
            // 🔁 反向守卫:没有它,「无脑 Dispose」也能让上一条变绿 —— 而那会把
            //    成功路径上的活 clip 库销毁(下一次 Play 全静音)。
            AudioMixer goodMixer = RealMixer();
            var clips = new ClipLedger();
            var provider = new TableProvider(BakedTable());

            AudioAssemblySeams seams = BootRoot.BuildAudioSeams(
                provider, mixerLoader: () => goodMixer,
                clipLoader: clips.Load, clipRelease: clips.Release);

            Assert.That(seams, Is.Not.Null, "前置:成功路径应返回缝");
            Assert.That(clips.Released, Is.EqualTo(0),
                $"成功路径释放了 {clips.Released} 个 clip —— 把活的库销毁了" +
                "(下一次 Play 全静音,比泄漏更难查)");

            // 收尾:本测经缝装的 clip 归缝方所有,库 Dispose 时会经 internalRelease 归还;
            // 显式 Dispose 让夹具不留悬挂(与生产 `OnDestroy` 同款)。
            seams.Clips.Dispose();
        }

        // ══════════════ ③ OnDestroy 置 null 两条静态缝 ══════════════

        [Test]
        public void test_td031_1_onDestroy_nullsBothStaticSeams()
        {
            string code = StripComments(ReadProductionSource("Gameplay.Boot/BootRoot.cs"));

            int destroy = code.IndexOf("private void OnDestroy()", StringComparison.Ordinal);
            Assert.That(destroy, Is.GreaterThanOrEqualTo(0),
                "找不到 `OnDestroy` —— 判据的对象被移动/改名,须同改本测");

            // 方法体 = 从签名的 `{` 到配平的 `}`(与 s15_5_hygiene_test 的 ExtractMethodText 同口径)
            int open = code.IndexOf('{', destroy);
            int depth = 0, close = -1;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}' && --depth == 0) { close = i; break; }
            }
            Assert.That(close, Is.GreaterThan(open), "`OnDestroy` 方法体未闭合");

            string body = code.Substring(open, close - open);

            Assert.That(body.Contains("AudioSettingsRegistry.Provider = null;"), Is.True,
                "`OnDestroy` 未把 `AudioSettingsRegistry.Provider` 置 null —— " +
                "跨 Play 轮会悬垂指向已 Release 的 provider(TD-audio-031 ①)");
            Assert.That(body.Contains("PlayerBusVolumeInitializer.MixerResolver = null;"), Is.True,
                "`OnDestroy` 未把 `PlayerBusVolumeInitializer.MixerResolver` 置 null —— " +
                "跨 Play 轮会悬垂指向已 Release 的 mixer(TD-audio-031 ①)");
        }

        // ══════════════ 夹具 plumbing ══════════════

        /// <summary>装载 / 释放计数面(经 `clipLoader` / `clipRelease` 缝注入)。</summary>
        private sealed class ClipLedger
        {
            public int Loaded { get; private set; }
            public int Released { get; private set; }

            public AudioClip Load(string address)
            {
                Loaded++;
                // 只建内存对象,不进 Addressables(缝方持有 ⇒ 由 Release 回调记账)
                return AudioClip.Create("probe_" + Loaded, 1, 1, 44100, false);
            }

            public void Release(AudioClip clip)
            {
                Released++;
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        /// <summary>烘焙产物的运行期表(与 `runtime_assembly_test` 同款:真烘焙 + 真往返)。</summary>
        private static AudioEventDataSet BakedTable()
            => AudioEventTableBake.Materialize(AudioEventTableBake.BakeFromRepo(RepoRoot()));

        private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private sealed class TableProvider : IDataProvider
        {
            private readonly AudioEventDataSet _table;
            public TableProvider(in AudioEventDataSet table) { _table = table; }
            public TDataSet Load<TDataSet>() where TDataSet : struct
            {
                object boxed = _table;
                return (TDataSet)boxed;
            }
        }

        /// <summary>真混音器资产(组齐、参数齐 —— 成功路径用)。</summary>
        private static AudioMixer RealMixer()
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            Assert.That(mixer, Is.Not.Null,
                $"混音器资产装载失败:{MixerAssetPath}(跑 MixerAssetGenerator.Create batch 生成)");
            return mixer;
        }

        private static string ReadProductionSource(string relativePath)
        {
            string path = Application.dataPath.Replace('\\', '/') + "/" + relativePath;
            Assert.That(File.Exists(path), Is.True,
                $"找不到生产源文件 {relativePath} —— 判据的对象被移动/改名,须同改本测");
            return File.ReadAllText(path);
        }

        /// <summary>剥掉行注释与块注释(被测字面量也出现在注释里,不剥会把「注释里写了」当成「代码里做了」)。</summary>
        private static string StripComments(string text)
        {
            var sb = new System.Text.StringBuilder(text.Length);
            bool inBlock = false;
            int i = 0;
            while (i < text.Length)
            {
                if (inBlock)
                {
                    int close = text.IndexOf("*/", i, StringComparison.Ordinal);
                    if (close < 0) return sb.ToString();
                    inBlock = false;
                    i = close + 2;
                    continue;
                }

                int lineComment = text.IndexOf("//", i, StringComparison.Ordinal);
                int blockOpen = text.IndexOf("/*", i, StringComparison.Ordinal);

                if (blockOpen < 0 && lineComment < 0) { sb.Append(text, i, text.Length - i); break; }
                if (blockOpen < 0 || (lineComment >= 0 && lineComment < blockOpen))
                {
                    sb.Append(text, i, lineComment - i);
                    int newline = text.IndexOf('\n', lineComment);
                    if (newline < 0) break;
                    i = newline;
                    continue;
                }

                sb.Append(text, i, blockOpen - i);
                inBlock = true;
                i = blockOpen + 2;
            }
            return sb.ToString();
        }
    }
}
