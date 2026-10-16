# S14-3 实现证据 —— story-009(声源生命周期与贴耳交接)· 2026-10-16

> **证据性质**:实现轮原件(`.claude/docs/coding-standards.md` §Review Evidence Standards ——
> 原判定 → 修复落点 → 验证命令,可证伪)。
> **对象**:`production/epics/audio-system/story-009-source-lifecycle-handover.md`
> **上游判定**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md` §2.3
> (009 = 12 测中 🔴 假 9 · 🟡 半真 2 · 🟢 真 1)
> **sprint**:`production/sprints/sprint-14.md` S14-3(Must Have · L 件)
> **用户裁定(2026-10-16,AskUserQuestion ×2)**:① **交付面收敛** —— 三类中两类已由 S14-2
> 交付(`EndLoopWatchdog` / `CueRenderSink`)⇒ 本件只落 `HandoverMux` + `LoopSelfTerminationDriver`,
> 兜底谓词与句柄面**改指 S14-2 的生产类、不重复造** · ② **AC 编号** = 补 story 局部
> `AC-009-1…4` + 就地订正幽灵 ID `AC-44-D10`(不改 GDD)。

---

## §1 原判定 → 修复落点

| # | 原判定(评审原件 §2.3) | 严重度 | 修复落点 | 验证命令 |
|---|---|---|---|---|
| 1 | 兜底 3 测打自造 `FakeLoopWatchdog`(判据是测试自己写的) | 🔴 | 判据改调生产 `EndLoopWatchdog.ShouldSelfTerminate`;执行面落 `LoopSelfTerminationDriver` | 证伪例 ①②③ |
| 2 | 交接 5 测打自造 `FakeHandoverFsm`(FSM 不存在) | 🔴 | `Gameplay.Presentation/Audio/HandoverMux.cs`(生产 `HandoverPhase` 四相闭集) | 证伪例 ④④b⑤⑥⑦⑧ |
| 3 | 句柄 2 测打自造 `FakeHandleManager` | 🔴 | 改指 **S14-2 已交付**的 `CueRenderSink`(不重复造 —— ADR-024 §六) | 证伪例 ③ |
| 4 | `test_endLoopWatchdog_noWallClockRefs` 扫真门但被测类不存在 | 🟡 空转真扫 | 生产类落地 ⇒ 扫描面非空(同 S14-2 的 D1 收窄处置) | 该测自身即判据 |
| 5 | 4 条 AC 无 `AC-44-xx` 编号(用中文描述名) | 独立瑕疵 | 补 **`AC-009-1…4`**(用户裁定;不用 `AC-44-D10` —— 见 §7) | `grep -c "AC-009-"` |

## §2 交付面

| 类别 | 文件 | 行数 | 内容 |
|---|---|---|---|
| 新 | `unity/Assets/Gameplay.Presentation/Audio/HandoverMux.cs` | 234 | `HandoverPhase` 四相 · 两态 + 两过渡窗 · 窗内线性交叉(sim tick 计)· 窗满对**自有句柄** `EndLoop` · 幂等重入 · 听诊优先 · 硬切构造期拒绝 |
| 新 | `unity/Assets/Gameplay.Presentation/Audio/LoopSelfTerminationDriver.cs` | 144 | 登记活跃循环 + 收快照 + 每 tick 调 `EndLoopWatchdog.ShouldSelfTerminate` 判停 ⇒ 自行 `EndLoop`(**判据不复制**) |
| 改 | `unity/Assets/Gameplay.Presentation/Audio/AudioTuning.cs` | +10 | `HandoverTicks = 10`(500 ms;GDD :777 安全范围 ≥ 50 ms) |
| 改 | `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs` | +1 | `EntryAllowlist` += `HandoverPhase` |
| 改 | `unity/Assets/Tests/EditMode/Audio/loop_lifecycle_test.cs` | 12 → **16** 测 | `FakeLoopWatchdog` / `FakeHandoverFsm` / `FakeHandleManager` 全数删除 |

**范围收敛的依据**(sprint-plan Phase 1 硬前置 · 实测):

| sprint-14 原计划 | 实测状态 | 处置 |
|---|---|---|
| EndLoop 自评看门狗 | **谓词已由 S14-2 交付**(`EndLoopWatchdog`);**执行器缺** | 补 `LoopSelfTerminationDriver`(不重写谓词) |
| 句柄生命周期管理器 | **已由 S14-2 交付**(`CueRenderSink`) | 测试改指生产类,**不重复造** |
| `HandoverMux` 交接 FSM | 全库零命中 | **本件唯一新件** |

## §3 证伪轮 —— 9 例(停用生产码守卫 ⇒ 断言转红)

**纪律**:每例**改 → 跑 → 还原**在**同一命令内**完成(`/tmp/fz143.py`):
① 先把干净文件复制到 `/tmp/s143src-{tag}.cs`;② 改生产码;③ 跑 Unity EditMode(音频过滤);
④ `finally` 块还原;⑤ 还原后**逐字节校验**。

**还原核验**(9 例备份与生产码逐字节一致,`cmp -s` 全 IDENTICAL):

```
IDENTICAL: 1_selftick       -> LoopSelfTerminationDriver.cs
IDENTICAL: 2_execute        -> LoopSelfTerminationDriver.cs
IDENTICAL: 3_unregister     -> LoopSelfTerminationDriver.cs
IDENTICAL: 4_worldrelease   -> HandoverMux.cs
IDENTICAL: 4b_stethrelease  -> HandoverMux.cs
IDENTICAL: 5_hardcut        -> HandoverMux.cs
IDENTICAL: 6_priority       -> HandoverMux.cs
IDENTICAL: 7_idempotent     -> HandoverMux.cs
IDENTICAL: 8_crossfade      -> HandoverMux.cs
```

**残留扫描**:`grep -rn "disabled \*/" unity/Assets/Gameplay.Presentation/Audio/` ⇒ **零命中**。
守卫行在场确认:`_sink.EndLoop` 三处(`LoopSelfTerminationDriver.cs:136` ·
`HandoverMux.cs:211` · `HandoverMux.cs:226`)。

| 例 | 停用的守卫(生产码) | 转红断言 | 红数 |
|---|---|---|---|
| ① selftick | `EndLoopWatchdog.ShouldSelfTerminate(...)` → `false` | `staleStream_stopsWithinBound` · `resumeNoDoubleStop` · `zeroBoundStopsImmediately` | **3** |
| ② execute | `_sink.EndLoop(rec.Handle);` 停用 | 同 ① 三条(**修夹具后**) | **3** |
| ③ unregister | `EndLoop` 注销体(`_active.Remove`)清空 | `normalEndLoop_noSelfTermination` | **1** |
| ④ worldrelease | `_sink.EndLoop(_worldHandle);` 停用 | `enterStethoscope_worldFadesOut` | **1** |
| ④b stethrelease | `_sink.EndLoop(_stethoscopeHandle);` 停用 | `exitStethoscope_worldFadesIn` | **1** |
| ⑤ hardcut | `handoverTicks <= 0` 拒绝 → `false` | `hardCut_rejected` | **1** |
| ⑥ priority | 听诊优先守卫 → 无 | `worldCueArrives_noRestart` | **1** |
| ⑦ idempotent | 幂等重入守卫 → 无 | `idempotentReentry` | **1** |
| ⑧ crossfade | 交叉淡化公式 → 恒 1 | `midWindow_crossfadeStrictlyBetween` | **1** |

**原始输出**(`/tmp/fz143.out`,首轮;例 ② 为修复前的 0 红):

```
[反证 1_selftick]      failed=3 total=324 还原OK
   RED: test_endLoopWatchdog_resumeNoDoubleStop
   RED: test_endLoopWatchdog_staleStream_stopsWithinBound
   RED: test_endLoopWatchdog_zeroBoundStopsImmediately
[反证 2_execute]       failed=0 total=324 还原OK        ← ⚠️ 假绿!见 §3.1
[反证 3_unregister]    failed=1 total=324 还原OK
   RED: test_endLoopWatchdog_normalEndLoop_noSelfTermination
[反证 4_worldrelease]  failed=1 total=324 还原OK
   RED: test_handover_enterStethoscope_worldFadesOut
[反证 5_hardcut]       failed=1 total=324 还原OK
   RED: test_handover_hardCut_rejected
[反证 6_priority]      failed=1 total=324 还原OK
   RED: test_handover_worldCueArrives_noRestart
[反证 7_idempotent]    failed=1 total=324 还原OK
   RED: test_handover_idempotentReentry
[反证 8_crossfade]     failed=1 total=324 还原OK
   RED: test_handover_midWindow_crossfadeStrictlyBetween
```

**修复后复跑例 ② 与新增例 ④b**:

```
[反证 2_execute]       failed=3 total=324 还原OK
   RED: test_endLoopWatchdog_resumeNoDoubleStop
   RED: test_endLoopWatchdog_staleStream_stopsWithinBound
   RED: test_endLoopWatchdog_zeroBoundStopsImmediately
[反证 4b_stethrelease] failed=1 total=324 还原OK
   RED: test_handover_exitStethoscope_worldFadesIn
```

### §3.1 ⚠️ 证伪轮抓到**我自己写的假绿夹具**(BLOCKING 级 · 已修)

**现象**:例 ② 停用 `_sink.EndLoop(rec.Handle);`(驱动器**实际执行**停止的唯一一行)
⇒ **红 0 条**。

**根因**:我的三条兜底测试只断言驱动器**内部计数器**(`ActiveLoopCount` /
`SelfTerminatedCount`)。驱动器完全可以「停止跟踪」却**从不调用 sink**,计数器照样正确 ⇒
**没有任何断言观测 sink 是否真的收到 `EndLoop`**。这正是 S11-9 / S13-9 那类假绿
(`story-done` §Falsifiability Gate 明列「一次性/幂等类」命中形态)。

**同一弱点在交接测试**:`WorldHandleReleased` / `StethoscopeHandleReleased` 是 `HandoverMux`
的**自述标志**,不是副作用观测 —— 例 ④ 只靠标志转红(标志恰好与调用同处一个 `if` 块),
但「标志为真」与「真调 sink」是两件事,判据强度不足。

**修复**:五条测试改为持有 `RecordingRenderTarget` 并断言 **`target.Stopped` 含句柄 id**
(可观测副作用):

```csharp
int worldHandleId = target.Started[0];
…
Assert.That(target.Stopped, Does.Contain(worldHandleId),
    "世界层句柄必须**真的**被 44 停止(不是只翻了个标志)");
```

命中测试:`staleStream_stopsWithinBound` · `resumeNoDoubleStop` ·
`zeroBoundStopsImmediately` · `enterStethoscope_worldFadesOut` · `exitStethoscope_worldFadesIn`。

**判据**:修复后例 ② 由 **0 红 → 3 红**,例 ④b(新增,专测听诊层释放)由 N/A → **1 红**。

> **登记理由**:证伪轮的价值正在于**证伪我自己的夹具**。把「例 ② 红 0」写成
> 「例 ② 通过」就是自述失真。此处保留原始 0 红并记录修复前后对照。

## §4 回归终值

**`3542 / 3496 / 0 / 46`**(`unity/Logs/s14-3-final.xml`,2026-10-16,EXIT=0)

- 基线(S14-2,commit 未提交,`unity/Logs/s14-2-final.xml`)= `3538 / 3492 / 0 / 46`
- ⇒ **+4 / +4 / 0 / 46 不变,零回归**(`loop_lifecycle_test` 12 → 16 测,+4)
- `Audio` 过滤套件 = **324 / 323 / 0**

**验证命令**:
```bash
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s14-3-final.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
# ⇒ 3542 3496 0 46
```

## §5 门保持

| 门 | 判据 | 结果 |
|---|---|---|
| b5 | 含 `AudioCueDto`/`IAudioCueSink`/`AudioCueHandle`/`TierSource` token 的文件须声明 44 命名空间 | ✓ 2 新文件全在 `DaYiJingCheng.Gameplay.Presentation.Audio` |
| b3 | 44 公开签名里的项目类型须白名单 | ✓ `HandoverPhase` 经用户裁定增列 |
| b4 | 字段类型白名单 | ✓ 全落 {BCL, Sim.Contracts.*, 44 自身} |
| 零墙钟 | `AssemblyGates.CheckAudioClockTokens` 扫两新类方法体 | ✓ 无 `DateTime`/`Time` 引用(测试 `test_endLoopWatchdog_noWallClockRefs` 覆盖) |
| ADR-029 §③ | `Sim/` 内 `new PayloadRef(` | ✓ 真代码零命中(本件不涉) |

**分层纪律**(写进 `HandoverMux.cs` 文件头):交接窗内增益交叉 = 本类**音量输出**;
bus 路由 / 贴耳抬升归脚本动作(story-003)—— 两者刻意分离,因快照会插值它存储的**全部**参数
(GDD :193-197)。本类**不碰快照**。

## §6 幽灵 ID 订正(同批 · 用户裁定)

实测 **`AC-44-D10` 是幽灵 ID**:story-007 与账本曾用它指「锚点纪律」,而 GDD
`design/gdd/audio-system.md` **零定义**(`grep -c "AC-44-D10"` = **0**;`AC-44-D` 系列止于 **D9**)。

**订正**(就地,2026-10-16):
- `story-007-net-divergence-remote-derivation.md` §Criteria:「AC-44-D10 锚点纪律」→
  「AC-44-D1 锚点纪律(GDD 无编号 —— 原写 AC-44-D10 系幽灵 ID,2026-10-16 订正)」
- `tests/unit/audio_system/README.md` story-007 段:5 处 `AC-44-D10` → `AC-44-D1`
- `production/qa/qa-plan-sprint-14-2026-10-16.md` 1 处

⚠️ **评审原件 `review-audio-epic-fake-green-2026-10-16.md` 不改写** —— 它是历史判定记录,
事后改写 = 伪造评审(承 `.claude/docs/review-workflow.md` §不可追补)。

## §7 NOT-RUN 如实登记(禁借绿)

- **AC-009-2 的听测半边**(交接窗 500 ms 是否「听觉上不拖沓」)—— **NOT-RUN**:
  听测需素材本体(story-015/016,`Ready`)+ 真人听审。
- **IL2CPP 三格**(ADR-012 矩阵:Linux-ARM64-IL2CPP / Windows-x64-IL2CPP / Apple Silicon)——
  **NOT-RUN**。本机仅 Mono;CI 真 runner 环境未配(`UNITY_LICENSE`)。
- **联机面** —— 本件不涉(交接是 44 本地操作,ADR-028 ⑤ 零网络依赖)。

## §8 登记项(本批不做)

- **story-009 转 `Complete`** —— 归 **S14-7**(6 条复评批,`sprint-14.md` DoD 逐字要求)。
- **bus 路由 / 贴耳抬升接线**(`HandoverMux.WorldVolume` / `.StethoscopeVolume` →
  mixer 两级组)—— 归 **story-003**(快照两级组与调用点白名单)。
- **素材接入**(真实音频播放在 `ICueRenderTarget` 之后)—— 归 story-015/016。
- **S14-1 / S14-2 的账本与 evidence 缺口** —— S14-1(commit `a2c6bd6`)**未落** evidence 原件、
  未更新账本 story-008 段(仍写「测试用自持谓词面(FakeSourcePool/...)」,而 S14-1 已删 Fake)。
  **本批不追补** —— 评审不可事后追补;登记归 **S14-7** 一并处置。
