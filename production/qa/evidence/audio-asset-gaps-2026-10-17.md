# 音频素材 / 接入 缺口清单(2026-10-17 · 供用户补齐接入用)

> 本件为**只读勘察结论**,回答两件事:① **素材本身缺什么** ② **接入链缺什么(导致 Play 里没声音)**。
> 勘察方式:WAV 二进制读数(峰值 / RMS / 静音%)· 源码 grep · meta 检查。**未跑 PlayMode**。

---

## 一、素材本身:不缺文件,已经是真内容

| 项 | 实测 |
|---|---|
| `assets/data/audio_events.json` 引用 | **37 个 wav** |
| `unity/Assets/Audio/` 实有 | **37 个 wav** |
| **缺文件** | **0** |
| **多余文件** | **0** |
| 格式 | 全部 44100 Hz / **单声道** / PCM |
| 是否静音占位 | **否** —— 37 个全部有真实波形(峰值 0.48–0.89,RMS 0.02–0.30) |
| 混音器 | `unity/Assets/Audio/DaYiJingCheng.mixer` 在位 |

**⇒ 素材面没有缺口。** 「理清素材缺什么」的答案是:**什么都不缺**。

---

## 二、接入链:这才是「Play 里没声音」的真正原因

### 2.1 已就位的零件(全部是「库」,不是「装配」)

`Gameplay.Presentation/Audio/` 下已有约 33 个类:`CueRenderSink`(真 `IAudioCueSink` 实现)、
`AudioSourcePool`、`SpatializationDirector`、`AnchorGate`、`CoughChannel`、`CueEmitDeriver`、
`EndLoopWatchdog`、`TierFilterDriver`、`SnapshotDirector`、`PlayerBusVolumeInitializer` 等。

⚠️ **但这些类的 `new X(` 只在文档注释(`/// var gate = new AnchorGate(...)`)里出现** ——
`grep -rn "new <类>(" --include=*.cs unity/Assets | grep -v Tests/` 对上述类**全部零命中生产装配点**
(唯一例外:`SpatializationDirector` 内部自建 `AudioSourcePool` / `OcclusionDriver`)。

### 2.2 缺口(按阻塞顺序)

| # | 缺口 | 证据 | 后果 |
|---|---|---|---|
| **A-1** | **`CueRenderSink` 从未在运行期被构造** | `CueRenderSink.cs:56` 是唯一生产实现;全仓零 `new CueRenderSink(` | 没有任何东西把 cue 变成声音 |
| **A-2** | **`ICueRenderTarget` 只有 `NullRenderTarget`** | `CueRenderSink.cs:130`;无真实现(接 `AudioSourcePool` 的那层) | 即便构造了 sink,也是静默空转 |
| **A-3** | **cue 源侧未接线** | `PatientCueEmit.Emit` / `CoughChannel.Dispatch` / `CueEmitDeriver` 在 `Gameplay.Boot` 与患者生成路径中**零调用** | 病人不会咳、呼吸层不出声 |
| **A-4** | **`Boot.unity` 零 `AudioSource`**(受 ADR-028 ② 约束,这是对的) | 池须运行期自建;但 `AudioSourcePool` 无生产构造点(同 A-1) | 世界声源池根本不生成 |
| **A-5** | **`PlayerBusVolumeInitializer` 零构造点** | 玩家总线(Master/Music/SFX…)从未按设置推给 mixer | 音量设置不生效 |
| **A-6** | **wav 的 `.meta` 未指定 `m_audioMixerGroup`** | `amb_clinic_room_loop.wav.meta` 等 grep `m_audioMixerGroup` **零命中** | 所有声音走 Master 直通,ADR-018 的六总线拓扑形同虚设 |

### 2.3 结论

**「素材缺什么」= 不缺。**
**「为什么 Play 里没声音」= A-1 ~ A-6 六处装配缺失**,不是素材问题。
⇒ 用户选「我来补齐接入」时,要补的**不是 wav 文件,而是装配代码**。

---

## 三、建议的最小接入路径(供决策,尚未实现)

按依赖序,最小可听闭环 = **3 处**:

1. **`AudioSourcePool` 真 `ICueRenderTarget` 实现**(~1 个新类):
   把 `CueRenderSink` 的渲染动作转到 `AudioSourcePool.Spawn` + `AudioSource.clip/Play`。
2. **`CompositionRoot` 装配**:`pool → new CueRenderSink(target) → 交给 PatientCueEmit / HandoverMux`。
3. **`BootRoot` 启动序第 6 步**:构造 `PlayerBusVolumeInitializer` 并推总线。

A-6(混音器分组)可用 Unity Inspector 批量选中 37 个 wav 指定 AudioMixer Group,或写一个编辑器脚本批量设。

---

## 四、未及核查

- **未跑 PlayMode / 未实机点 Play** —— 本件全为静态勘察,「真的没声音」以用户 2026-10-16 走查实测为准。
- **未查 `assets/data/audio_settings_registry.json`** 与 cooked 表的实际内容一致性。
- **未核 `SpatializationDirector` 是否在某个未被 grep 到的路径里被反射构造**。
