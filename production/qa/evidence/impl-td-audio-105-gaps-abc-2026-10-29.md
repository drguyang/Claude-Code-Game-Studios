# 实现证据 —— `TD-audio-105` 缺口 A / B / C

- **日期**:2026-10-29
- **用户裁定**:① 完成 A + B ② 把缺口 C **提前到现在做**(不等 49 的 P2)
- **对象**:
  - `unity/Assets/Gameplay.Presentation/Audio/MonoOutputApplier.cs`(新)
  - `unity/Assets/Gameplay.Boot/AudioSettingsRuntime.cs`(新)
  - `unity/Assets/Gameplay.UI/Skeuomorphic/SubtitleLine.cs`(新)
  - `unity/Assets/Gameplay.UI/Skeuomorphic/SubtitleLineView.cs`(新)
  - `unity/Assets/Gameplay.Boot/PatientCueDriver.cs`(改)
  - `unity/Assets/Gameplay.Boot/BootRoot.cs`(改)· `CompositionRoot.cs`(改)
  - `unity/Assets/Tests/EditMode/EditMode.asmdef`(加 `Gameplay.UI` 引用)
  - `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs`(AC-44-B4 白名单放宽一条 · §4.5)
  - `production/epics/skeuomorphic-ui/story-026-subtitle-rendering.md`(新)

---

## 一、缺口 A —— `mono_enabled` 的真实承载面(推翻账面假设)

### 1.1 债册的假设是错的(实测推翻)

债册 `TD-audio-105` 写「`.mixer` 的 `m_ExposedParameters` 不含 `mono_enabled`」——
**读起来修法就是「往 mixer 加一个参数」**。实测推翻了它:

| 实测 | 结论 |
|---|---|
| `DaYiJingCheng.mixer` 的 12 个暴露参数**全是**音量/滤波类(guid 均指向 `AudioMixerGroupController.m_Volume`) | mono 不是同族参数 |
| `monodis` 反汇编 Unity 6.3.24f1 的 `UnityEngine.AudioModule.dll`:`AudioListener` **无** forceMono 成员 | 无 listener 级开关 |
| `AudioSettings.speakerMode` **已 deprecated**,官方修法 = `GetConfiguration` → 改 → `Reset` | 设备级配置 |
| `AudioSpeakerMode.Mono` 存在 | 有可用的目标值 |

⇒ **Unity 6.3 的 mono 是设备级 `speakerMode`,不是混音器参数。**
往 `.mixer` 加 `mono_enabled` 会造出一个**永远不生效的假开关**。

### 1.2 缺口比账面深:整条设置链生产零接线

| 节点 | 实测 |
|---|---|
| `AudioSettingsStore` | `grep "new AudioSettingsStore"` 全库**只命中测试** |
| `AudioSettingsStore.MixerPushSink` | 生产侧**零接线** |
| `mono_enabled` | 生产侧**零消费方**(只有注册表 + 常量两处声明) |

⇒ 玩家在 42 设置壳拨任何滑块 / 开关,**不产生任何声学变化**。

### 1.3 修复落点

| # | 落点 | 内容 |
|---|---|---|
| A-1 | `MonoOutputApplier`(新) | mono → `speakerMode` 的施加器;注入缝(`GetSpeakerMode`/`SetSpeakerMode`)供 EditMode 观测 |
| A-2 | `AudioSettingsRuntime`(新) | 把 store + 推 mixer + mono 接成一条链;**生产装配点** |
| A-3 | `BootRoot.Start` 2.65 段 | 构造 `AudioSettingsRuntime` 并跑 `Startup()` |
| A-4 | `BootRoot.AudioSettings` | 公开访问面(42 壳读它) |

### 1.4 两个设计判断(都不是照抄)

**① 关 mono 还原成「开之前的设备模式」,不硬编码 `Stereo`。**
硬编码会把玩家的 5.1 / 7.1 设备**静默降级** —— 那是另一种缺陷。
判据 `test_applyOff_restoresOriginalMode_notHardcodedStereo`。

**② 重复开 mono 不得覆盖还原目标。**
若第二次开 mono 把 `_preMonoMode` 记成 `Mono` 自身,关 mono 时「还原」到 Mono = **关不掉**。
判据 `test_applyOnTwice_isIdempotent_andKeepsRestoreTarget`。

---

## 二、缺口 B —— 字幕文本的出向半边

### 2.1 为什么不在 44 侧(关键纠正)

我最初打算「给 44 加一个出向口」。查 `AC-44-15` 原文后**推翻**:

> 「cue → 44 播音(**维持「只入不出」,44 不新增出向口**),
> 文本 → 42 呈现(**文本本体 = 44 维护的事件表 `subtitle_text` 字段,上游发射点解析**)」

且 ADR-018 §② 的 `IAudioCueSink` **只有入向方法**(Emit / BeginLoop / EndLoop / SetTier)。
⇒ **文本由上游发射点直投 42,不经 44 转发。** 这是已裁的架构形状,不是本轮的取舍。

### 2.2 落点

上游发射点 = `PatientCueDriver`(`PatientCueEmit.Emit` 的唯一生产调用方)。
它现在同时投两个面:

| 面 | 去向 | 变化 |
|---|---|---|
| 播音 | `PatientCueEmit.Emit` → 44 `IAudioCueSink` | **不变** |
| 文本 | `ISubtitleSink.Show(...)` → 42 | **新增** |

### 2.3 三个设计判断

**① 文本源 = 事件表 `subtitle_text` 列,不硬编码台词。**
`CompositionRoot.SubtitleTextOf(table, ordinal)` 只做查表。判据 = 文本真的来自表。

**② 空文本不 `Show`。** `Show` 是「替换当前行」语义 —— 空文本调用等于把屏幕清空、盖掉上一条真实字幕。

**③ 字幕不替代播音。** 判据 `test_subtitleDelivery_doesNotReplaceAudio` 防的是「为了投字幕跳过 `sink.Emit`」的退化形态。

### 2.4 零行为变化

`subtitleSink` / `subtitleTextOf` 默认 `null` ⇒ **62 处既有 EditMode 装配全不传** ⇒ 不投递。
判据 `test_withoutSubtitleSink_noTextDelivered`。

---

## 三、缺口 C —— 42 字幕元件(提前做)

### 3.1 与 `OQ-42-8` 的关系(明示改动,不悄悄扩大)

| 项 | 原裁(2026-10-06) | 本轮(用户 2026-10-29 裁定) |
|---|---|---|
| 42 侧交付 | 四处接口 | 四接口 **+ 字幕呈现元件** |
| 字幕**呈现规格** | 归 49(P2) | 本 story 给 **P0 最小规格** |
| 字幕**语义/时机** | 归 44 | **仍归 44**(不变) |

⚠️ **不解除 49 的 P2 细化权** —— 本件交付「P0 可用」,不是「最终形态」。

### 3.2 落点

| # | 落点 | 内容 |
|---|---|---|
| C-1 | `SubtitleLine.cs`(新) | `SubtitleLineData` DTO + `ISubtitleSink` 接收口(**无 `ShouldShow` 反问**) |
| C-2 | `SubtitleLineView.cs`(新) | UI Toolkit 元件;`Label` + USS;说话人**前置 + 方括号**(非颜色差异) |
| C-3 | `story-026-subtitle-rendering.md`(新) | story 卡(用户裁定「新立 42 字幕 story」) |

### 3.3 关键判据:42 不判时机

`E-12b` 逐字:「**42 不持有语声状态、不判何时显示**」。
机械判据 = **反射断言**:`SubtitleLineView` 不含 `Timer` / `Duration` / `AutoHide` / `Timeout` /
`Expiry` / `Seconds` 类成员。若日后有人加 `autoHideSeconds`,该测会红。

---

## 四、本轮我自己的五处错误(如实登记)

### 4.1 在证伪窗口内编辑被变异文件 ⇒ 污染 A 证伪轮

`缺口 A` 证伪轮 run 2 的 **M4「假存活」是假象** —— 我在窗口期内改了 `CompositionRoot.cs`
(缺口 B 的接线),违反了「窗口内禁编辑」纪律。那次结果**作废**,已重跑(见 §五)。

### 4.2 缺口 B 引入真实编译失败

给 `Assemble` 加 `ISubtitleSink` 形参后,多个测试程序集缺 `Gameplay.UI` 引用 ⇒ **21 条 CS0012**。
修法 = `EditMode.asmdef` 加 `"Gameplay.UI"`。
⚠️ **这是「加一个跨程序集形参 ⇒ 所有消费者都要跟」的连带面**,与 `TD-audio-032` ① 的
「装配每多读一张表,所有替身都要同步」**同型**。

### 4.3 三处 API 签名靠猜 ⇒ 三次编译失败

| 我写的 | 实际 |
|---|---|
| `AudioCueHandle.None` | 无此成员(`new AudioCueHandle(-1)`) |
| `EndLoop(in AudioCueHandle)` | `EndLoop(AudioCueHandle)`(无 `in`) |
| `PatientBehaviorDirector(vitals)` | `(vitals, in BehaviorBands)` |
| `presence.AddPresent(id)` | `AddPresent(id, WorldPos)` |

⇒ **写夹具前应查签名,不是凭记忆写。** 同族纪律:承 `feedback_verify_assertions_offline_first`
的「先查再写」。

### 4.4 夹具 `MakeDriver` 自建 presence ⇒ 与测试的登记簿是两个实例

首版 `MakeDriver()` 内部 `new PresenceRegistry()`,而测试在**另一个**实例上 `AddPresent`
⇒ 驱动看不到病人 ⇒ 零 cue ⇒ 4 条全红。修法 = 由调用方传入。

### 4.5 新字段类型撞 AC-44-B4 边界门(全量回归才抓到)

全量 EditMode 回归报 **1 红**:`test_fieldTypes_production44_withinWhitelist`。

```
[B4] DaYiJingCheng.Gameplay.Presentation.Audio.MonoOutputApplier.GetSpeakerMode
     的字段类型「UnityEngine.AudioSpeakerMode」∉ 白名单
     {BCL, Sim.Contracts.*, UnityEngine.Audio*, UnityEngine.Object 子类, 44 自身}
```

**这是真阳性,不是误报** —— 但**红的是规则,不是设计**:`AudioSpeakerMode` 的
**命名空间是根 `UnityEngine`**(不含 `.Audio` 段),程序集才是 `UnityEngine.AudioModule`;
门的前缀规则按**命名空间**写,天然漏掉它。该程序集只装声音类型,不含任何游戏状态。

⇒ 处置 = **按程序集放行**(`assembly_boundary_test.cs` 的 `IsAllowedFieldLeaf`
第四条),并在注释里写明放宽的**边界**:根 `UnityEngine` 的其它类型继续红
(既有负例 `test_fieldTypes_simStateAndEngineNonAudioField_reportsRed` 的 `Vector3` 仍红,
未因放宽而失效)。错误串同批更新。

**我的过失**:写 `MonoOutputApplier` 时只跑了**该件的夹具**,没跑**全量**才让它漏到收口。
⇒ 同族纪律:新增**字段类型**前先查目标程序集的边界门白名单(承
`feedback_verify_assertions_offline_first`「先查再写」的机械面)。

---

## 五、证伪轮

### 5.1 轮次设置

| 项 | 值 |
|---|---|
| 脚本 | `/home/gu/.claude/jobs/181888b1/tmp/falsify_abc.py`(12 变异,一次性原子跑) |
| 变异面 | 4 个生产件:`MonoOutputApplier.cs` / `AudioSettingsRuntime.cs` / `PatientCueDriver.cs` / `SubtitleLineView.cs` |
| 靶夹具 | 4 件 26 条(`MonoOutputApplierTest` 6 · `AudioSettingsRuntimeTest` 8 · `SubtitleDeliveryTest` 4 · `SubtitleLineViewTest` 8) |
| 还原判据 | 每轮 `finally` 内 `open(...,'w').write(原文本)` + **SHA-256 逐字节比对**;脚本末再整体比一次四件 |
| 基线(变异前) | `a`=6/0 · `r`=8/0 · `b`=4/0 · `c`=8/0 —— **四件全绿,零失败** |

### 5.2 结果 —— 12/12 全部转红

| # | 变异(停用什么守卫) | 靶夹具 | 红 | 转红的测试 |
|---|---|---|---|---|
| M1 | mono 恒不施加(删 `setter(Mono)`) | `MonoOutputApplierTest` | 4/6 | `test_applyOn_switchesDeviceToMono` · `test_applyOff_restoresOriginalMode_notHardcodedStereo` · `test_applyOnTwice_isIdempotent_andKeepsRestoreTarget` · `test_setFailure_returnsError_notThrow` |
| M2 | 关 mono 硬编码 `Stereo`(弃 `_preMonoMode`) | 同上 | 2/6 | `test_applyOff_restoresOriginalMode_notHardcodedStereo` · `test_applyOnTwice_isIdempotent_andKeepsRestoreTarget` |
| M3 | 重复开 mono 覆盖还原目标 | 同上 | 1/6 | `test_applyOnTwice_isIdempotent_andKeepsRestoreTarget` |
| M4 | `MixerPushSink` 不接线 | `AudioSettingsRuntimeTest` | 4/8 | `test_mixerPushSink_isWired` · `test_startup_pushesAllSevenBusVolumes` · `test_startup_pushesDbNotLinear` · `test_applyAfterChange_routesByParamKind` |
| M5 | 推送线性值不换算 dB | 同上 | 1/8 | `test_startup_pushesDbNotLinear` |
| M6 | mono 也走 mixer 面(不走设备面) | 同上 | 2/8 | `test_applyAfterChange_routesByParamKind` · `test_monoOff_restoresDevice` |
| M7 | 字幕不投递(`if (false && ...)`) | `SubtitleDeliveryTest` | 1/4 | `test_withSubtitleSink_bothAudioAndTextAreDelivered` |
| M8 | 空文本也 `Show` | 同上 | 1/4 | `test_emptyText_doesNotShow` |
| M9 | 字幕替代播音(有字幕就跳过 `Emit`) | 同上 | 3/4 | `test_subtitleDelivery_doesNotReplaceAudio` · `test_withSubtitleSink_bothAudioAndTextAreDelivered` · `test_emptyText_doesNotShow` |
| M10 | 说话人不前置(挪到正文后) | `SubtitleLineViewTest` | 2/8 | `test_show_putsSpeakerBeforeText` · `test_truncatesBeyondMaxLines` |
| M11 | 不截断(删 `lines.Length <= MaxLines` 早退) | 同上 | 1/8 | `test_truncatesBeyondMaxLines` |
| M12 | 初始就可见(构造里不 `Hide()`) | 同上 | 1/8 | `test_initiallyHidden` |

脚本末输出:`总判定: 全部转红`;四件 SHA **`OK` ×4**(还原成功,盘上无变异残留)。

### 5.3 按 S12-4 命中形态归类

| 命中形态 | 本轮的落点 | 反证记录 |
|---|---|---|
| **定义域收窄** | mono 的「已开则幂等」(M3) | 停用幂等守卫 ⇒ `test_applyOnTwice_isIdempotent_andKeepsRestoreTarget` 红 |
| **共存/互斥** | 字幕与播音**必须同时发生**(M9) | 停用「字幕不替代播音」⇒ 3 条红,含专钉该项的 `test_subtitleDelivery_doesNotReplaceAudio` |
| **一次性/幂等** | 重复开 mono 不覆盖还原目标(M3) | 同上 |
| **阈值边界** | 行数上限 `MaxLines = 2`(M11) | 停用截断 ⇒ `test_truncatesBeyondMaxLines` 红 |
| **定序** | 说话人须在正文**之前**(M10) | 停用前置 ⇒ `test_show_putsSpeakerBeforeText` 红 |
| —— | 其余(M1/M2/M4/M5/M6/M7/M8/M12) | 单值守卫,各红 ≥ 1 条 |

### 5.4 一条**首轮假存活**已修(如实登记)

M4 首轮(`AudioSettingsRuntime` 的 `MixerPushSink`)**全绿假存活** —— 原因是我在
`Startup()` / `ApplyAfterChange()` 里**直接调 `PushToMixer`**,绕过了 `_store.MixerPushSink`,
所以删掉接线测试仍然通过。修法:两条路径**改走 sink**,并补夹具
`test_mixerPushSink_isWired`。重跑 ⇒ M4 红 4 条。
**若不做这一修,缺口的核心接线(= 玩家拨滑块真的有声音)就等于没被钉住。**

> ⚠️ **同处还有第二个属性:`Startup` 须「经 sink 走」而非直调私有方法。**
> 它的夹具是 `test_startup_pushGoesThroughSink_notDirectCall`,但该夹具**对 M4 天生免疫**
> (它自己就把 sink 换成计数替身,所以「接线有没有做」不影响它)—— **它不由本轮任何变异覆盖**。
> 承 `feedback_falsify_one_guard_per_property`「一属性一条变异」,补跑 **M13**(见 §5.6)。

### 5.5 已知的判别力边界(**不是**假绿,是覆盖范围)

| 面 | 说明 |
|---|---|
| `BootRoot` 的接线(A-3/A-4)不在本轮变异面 | 它是 Unity 场景启动序,EditMode 无载体;其**存在性**由 `grep` 佐证,**有效性**未证 ⇒ 计入下方「本件不主张」 |
| `MonoOutputApplier` 的 `DefaultSetSpeakerMode`(真调 `AudioSettings.Reset`)不可在 EditMode 观测 | 故用注入缝 `SetSpeakerMode` 观测**路由与参数**;真设备切换是否成功属**听测/手工**面 |

### 5.6 补跑 M13(一属性一条变异 · 承 `feedback_falsify_one_guard_per_property`)

**变异**:`Startup()` 里 `MixerPush push = _store.MixerPushSink;` → `MixerPush push = PushToMixer;`
(即「直调私有方法」退化形态;`ApplyAfterChange` 的第 2 处不动,故只打 `Startup` 这一面)。

| 项 | 值 |
|---|---|
| 靶夹具 | `AudioSettingsRuntimeTest`(8) |
| 结果 | **RED 1/8** → `test_startup_pushGoesThroughSink_notDirectCall` |
| 还原 | SHA-256 `OK`(`AudioSettingsRuntime.cs` 逐字节复原) |

⇒ 该属性**有专属判别力**:红的那一条正是它的夹具,其余 7 条不受影响(说明变异打的是这一面,
不是泛泛打崩)。至此 `AudioSettingsRuntime` 的两个属性各有一条变异:
**「接线存在」= M4** · **「调用经 sink」= M13**。

---

## 六、回归三元组

| 套件 | total | passed | failed | inconclusive | skipped | 退出码 |
|---|---|---|---|---|---|---|
| **EditMode** | **5955** | **5918** | **0** | 0 | 37 | 0 |
| **PlayMode** | **238** | **234** | **0** | 2 | 2 | 2 |

- **截至时刻**:2026-10-29 19:14 CST
- **批次**:全量(非增量)
- **commit**:`e22d2bd8` + 工作树(本轮改动未提交)
- 复算命令 = `/home/gu/.claude/jobs/181888b1/tmp/regress_abc.py`(EditMode + PlayMode 顺序跑,
  各自 `-testResults` 落 XML,脚本从 XML 根属性读 total/passed/failed/inconclusive/skipped)

### 6.1 与基线的对照(等式必须成立)

| 项 | 基线 | 本次 | 差 | 等式 |
|---|---|---|---|---|
| EditMode total | 5929 | 5955 | **+26** | 新增夹具 **26** 条(6+8+4+8)= **恒等** |
| EditMode passed | 5892 | 5918 | **+26** | 同上 |
| EditMode skipped | 37 | 37 | 0 | 不变 |
| PlayMode | 238/234/0/2/2 | 238/234/0/2/2 | **逐位相同** | 本轮不触 PlayMode 面 |

> 基线出处:`production/session-state/active.md` 记录 @ 2026-10-29 · 全量 · `5178ceb8`+工作树
> (即上一批 `2dd83807` 之前的那次)。**新增夹具条数可独立复算**:
> `cat <四个夹具文件> | grep -c 'public void test_'` ⇒ **26**。

### 6.2 首轮回归的 1 红与收口(如实登记)

首轮全量 EditMode = `5955 / 5917 / 1 / 0 / 37`,唯一红 = `test_fieldTypes_production44_withinWhitelist`
(AC-44-B4 字段类型白名单门,因 `UnityEngine.AudioSpeakerMode`)。处置与理由见 **§4.5**;
修后复测 `AssemblyBoundaryTest` = **24 / 0** 全绿,再跑全量 = 上表 `5955 / 5918 / 0`。
⇒ **红是真阳性(规则漏项),不是误报,也未靠放宽掩盖设计问题** —— 放宽的边界由既有负例
`test_fieldTypes_simStateAndEngineNonAudioField_reportsRed`(`Vector3` 仍须红)持续看守。

### 6.3 `PlayMode exit=2` 的说明

PlayMode 进程退出码 **2** 而 XML 报 `failed=0`。**XML 是权威判据**(脚本按 XML 根属性判决),
两侧数据的全部分歧只有退出码这一项。

**已观测**:本会话**本轮之前**的那次全量回归(§6.2 首轮,同一脚本、同一工作树)PlayMode 同样是
`238/234/0/2/2` 且 `exit=2`,即该退出码**不是本轮引入的**。
⚠️ **未取证**:我没有拿到"基线那次(5178ceb8)的退出码",故**不主张**「与基线同款」——
这里是「与**本会话上一轮**同款」。

---

## 七、本件**不**主张(禁借绿)

| 项 | 状态 |
|---|---|
| `TD-audio-105` 可结案 | ❌ **不可** —— 缺口 D(13 的可见对应物,BLOCKED-BY 42 冗余通道)**未做** |
| `AC-44-12` 已绿 | ❌ ③-a 的载体已通,但 ①(视觉替代)仍断在缺口 D |
| `AC-44-15` 已绿 | ❌ `[A]` 双投递已通,但 `[L]` 的**人工听测**未跑 |
| 字幕的**最终**呈现规格 | ❌ 归 49(P2) |
