# 实现证据 —— `TD-audio-031`:静态装配缝回收 + 异常路径 clip 释放

- **日期**:2026-10-29
- **批次**:音频债批 C · `TD-audio-031`(两半同批;全条 `open` → **`closed`**)
- **commit**:`cdcb9a8c` + 工作树(见 §四 三元组)
- **对象**:`unity/Assets/Gameplay.Boot/BootRoot.cs` ·
  `unity/Assets/Tests/EditMode/Audio/{audio_seam_release_test,s15_5_hygiene_test}.cs` ·
  `docs/tech-debt-register.md`

---

## 一、原判定(债册原文,节选)

> ① `BootRoot` 赋 `AudioSettingsRegistry.Provider`、`PlayerBusVolumeInitializer.MixerResolver`
> (两者皆 public static),而 `OnDestroy` 只 dispose 五件、**不置 null 两缝**
> ⇒ 跨 Play 轮悬垂指向已 `Addressables.Release` 的对象。
> ⚠️ 今天靠域重载 + Unity `==null` 重载**巧合**兜底;若该设置被关,即刻咬人。
> ② `BuildAudioSeams` 在 `clips.Preload()` **成功之后**若 `new AudioBusResolver` 抛(缺组)
> 或 `ApplyDefaults` errors 抛 ⇒ 已装载的 ≤15 个 clip **无 `Dispose()`**。

**复核(本轮)**:两半均成立,且 S15-7 的「两半均未修」在修复前实测为真。

---

## 二、修复落点

| # | 落点 | 内容 |
|---|---|---|
| R-1 | `BootRoot.OnDestroy` | 末尾新增两行置 null(`AudioSettingsRegistry.Provider` / `PlayerBusVolumeInitializer.MixerResolver`),顺序在 `_audioSeams = null` **之后**(上面那条 dispose 依赖 `_audioSeams`) |
| R-2 | `BootRoot.BuildBusFaceOrRelease`(新) | 把 ④ 总线解析 + ⑤ 玩家音量包进 try/catch:catch → `clips.Dispose()`(幂等)→ `throw;`(异常类型与报文**原样保留**) |
| R-3 | `BuildAudioSeams` 签名 | 新增 3 条注入缝:`clipLoader` / `clipRelease` / `busResolverFactory` |
| R-4 | `s15_5_hygiene_test` | 签名表加第 4 条 `BuildBusFaceOrRelease` |

**为什么用 try/catch 而不是「先判断再抛」**:两处抛点的判据都由**被调方**给出,在调用方复算一遍 = 第二真源。

**为什么必须加注入缝(这是本轮最花时间的一处)**:判据是「抛之前已预载的 clip 被 Dispose」。
三条路逐一实测被否:

| 尝试 | 实测 |
|---|---|
| `CreateInstance<AudioMixer>()` | ❌ `CS0311` —— `AudioMixer` **不是** `ScriptableObject` |
| `AudioMixerController.RemoveGroups()` | ❌ `CS0246` —— **不在公共 API** |
| 按名删组 / 改 `DaYiJingCheng.mixer` | ❌ 会改盘上的**作者态**资产,不可接受 |

⇒ EditMode 下**造不出缺组混音器**,那条 `catch` 只能经缝跑到。这与既有的 `mixerLoader` 是同一模式
—— 它的 doc 原文就写着「内联后只能经完整启动序才可达 ⇒ EditMode 无从断言,『接线是否存在』
退化为不可证伪的口头承诺」。

**`clipLoader` / `clipRelease` 同理**:生产路径下 `Preload()` 的 `_handles` 恒空(无 catalog)、
`Dispose` 是**空操作** ⇒ 零判别力。经缝装来的 clip 由夹具持有,释放走 `internalRelease` 通道,
「释放了几个」才可数。

---

## 三、证伪轮(S12-4 形态)

驱动脚本:`/home/gu/.claude/jobs/181888b1/tmp/falsify031.py`

| 变异 | 打靶 | 期望红 | 实测 |
|---|---|---|---|
| M1 | 删 `AudioSettingsRegistry.Provider = null;` | `test_td031_1_onDestroy_nullsBothStaticSeams` | **RED-命中** |
| M2 | 删 `PlayerBusVolumeInitializer.MixerResolver = null;` | 同上 | **RED-命中** |
| M3 | 删 catch 里的 `clips.Dispose()` | `test_td031_2_busResolveFailure_releasesPreloadedClips` | **RED-命中** |
| **M4** | **无脑 Dispose**(`Preload` 后立刻释放) | **`test_td031_2_successPath_doesNotRelease`** | **RED-命中** |

**4/4 全红;4/4 `RESTORE-OK`(`BootRoot.cs` SHA-256 回到 `676182a3…`)。**

**M4 是本轮最有价值的一条**:如果只写「抛路径要释放」,那么在生产代码里**无条件 Dispose** 也能让
M3 那条变绿 —— 而那会把成功路径上的活 clip 库销毁(下一次 Play 全静默,比泄漏更难查)。
M4 转红的恰是**反向守卫**那条,证明守卫方向正确。

---

## 四、回归三元组(S12-0 口径)

| 套件 | 截至时刻 | 批次 | commit | 结果 | 对照 |
|---|---|---|---|---|---|
| EditMode | 2026-10-29 13:47 CST | 全量(无 filter) | `cdcb9a8c` + 工作树 | **`5873 / 5836 / 0 / 0 / 37`**(exit 0) | `5870 + 3 = 5873` |
| PlayMode | 2026-10-29 13:47 CST | 全量(无 filter) | `cdcb9a8c` + 工作树 | **`238 / 234 / 0 / 2 / 2`**(exit 2) | 与基线**逐位相同** |

**EditMode 等式**:`5870`(上轮 `TD-audio-032` ③ 的全量,2026-10-29 13:27 CST)
`+ 3`(本轮 `audio_seam_release_test` 的三条)= **5873**。
`s15_5_hygiene_test` 只加了一条**签名**(在既有那条测试的循环里),`[Test]` 计数不变。

**PlayMode 两条 Inconclusive 是既有具名占位**(`…_notRun`);exit 2 与基线同形,不写成绿也不写成红。

---

## 五、本轮**不**证(如实登记)

| 项 | 状态 | 理由 |
|---|---|---|
| **A-5 抛路径也释放 clip** | **无独立夹具** | `ApplyDefaults` 的 errors 来自 `ValidateDefaults(keys)` —— 校验的是 `FactoryDefaults` 这个**代码常量集**的键 ⇒ 生产路径上**恒不抛**。在混音器上写错参数名触发不了它(`SetFloat` 返回值被忽略;且 EditMode 下 `SetFloat` 对任何名字都返回 false,见 `MixerRegistry` 的 H-B 实测)。它与 ① 共享同一个 `try/catch`(**结构上被覆盖**),但没有独立夹具 —— **不假装钉过** |
| 「关掉域重载后真不咬人」 | **NOT-RUN** | 需要改 `EditorSettings.asset` 跑一次 PlayMode;本轮未做(改编辑器设置属作者态改动,须单独裁定) |
| 跨 Play 轮真无悬挂 | **NOT-RUN** | 同因:EditMode 单进程内无法观测「跨 Play 轮」;`OnDestroy` 的置 null 由**源文本**判据钉,不是运行期观测 |

---

## 六、顺带修的一处

`BuildAudioSeams` 因新增 try/catch 一度回涨到 **48 行**(超 `TD-audio-032` ③ 的 40 行界)
⇒ 抽出 `BuildBusFaceOrRelease`(21 行),主方法回到 **31 行**。
`s15_5_hygiene_test::test_td032_3_buildAudioSeams_withinLineBudget` 的签名表加第 4 条。
**这不是可选的整理** —— 若不做,本轮会把上一条债刚钉好的界当场打破。
