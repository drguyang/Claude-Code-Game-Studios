# S14-2 实现证据 —— story-007(联机分叉与远端派生 · 静态半)· 2026-10-16

> **证据性质**:实现轮原件(`.claude/docs/coding-standards.md` §Review Evidence Standards ——
> 原判定 → 修复落点 → 验证命令,可证伪)。
> **对象**:`production/epics/audio-system/story-007-net-divergence-remote-derivation.md`
> **上游判定**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md` §2.1
> (007 = 19 测中 🔴 假 10 · 🟡 半真 5 · 🟢 真 4)
> **sprint**:`production/sprints/sprint-14.md` S14-2(Must Have · L 件 · 依赖 S14-1)
> **用户裁定(2026-10-16,AskUserQuestion ×4)**:① 交付粒度 = 一次全做 ·
> ② D1 扫描面 = 收窄为具名派生面 + 反空转守卫 · ③ 白名单 = 批准增列 4 项 ·
> ④ `cell_jitter` 读法 = 确认(jitter 是渲染散布、`distance` 是独立 rank 输入,两者不相导)

---

## §1 原判定 → 修复落点

| # | 原判定(评审原件 §2.1) | 严重度 | 修复落点 | 验证命令 |
|---|---|---|---|---|
| 1 | AC-44-D7 核心判据(重排差分哈希)的被测物 = 测试自写 `DeriveCueHash` | 🔴 | `Gameplay.Presentation/Audio/CueEmitDeriver.cs`(`Derive` + `CanonicalHash`);测试改调生产类 | 证伪例 ①②③ |
| 2 | `IAudioCueSink` 生产实现不存在(测试自造 `TierRecordingSink`) | 🔴 | `Gameplay.Presentation/Audio/CueRenderSink.cs`(+ `ICueRenderTarget` seam) | 证伪例 ⑦ |
| 3 | 锚点门无生产实现(测试自造 `FakeAnchorGate` + 自造 `GateDecision`) | 🔴 | `Gameplay.Presentation/Audio/AnchorGate.cs`(生产 `GateDecision` 闭集) | 证伪例 ④⑤ |
| 4 | EndLoop 看门狗无生产实现(测试自造 `FakeEndLoopWatchdog` / `FakeLoopProgress`) | 🔴 | `Gameplay.Presentation/Audio/EndLoopWatchdog.cs`(生产 `LoopProgress`) | 证伪例 ⑥ |
| 5 | **「空转真扫」** —— D1 门是真门、扫描面非空,但被断言类不存在 ⇒ 零违例对空集平凡成立 | 🔴 | `AssemblyGates.CheckAudioDerivationSurfaceRefs` 具名面 + **反空转守卫** | 自证测 `test_derivationSurfaceScan_absentNamedType_isRed` |

## §2 交付面

| 类别 | 文件 | 行数 | 内容 |
|---|---|---|---|
| 新 | `unity/Assets/Gameplay.Presentation/Audio/CueEmitDeriver.cs` | 143 | `CueEmitInput` · `Derive` · `CanonicalHash` · `DomainSeparator` |
| 新 | `unity/Assets/Gameplay.Presentation/Audio/AnchorGate.cs` | 138 | `GateDecision` · `Dispatch` · `Update` · `IsStale` |
| 新 | `unity/Assets/Gameplay.Presentation/Audio/EndLoopWatchdog.cs` | 91 | `LoopProgress` · `ShouldSelfTerminate` ×2 · `IsStagnant` |
| 新 | `unity/Assets/Gameplay.Presentation/Audio/CueRenderSink.cs` | 141 | `ICueRenderTarget` · `CueRenderSink` · `NullRenderTarget` |
| 改 | `unity/Assets/Gameplay.Presentation/Audio/AudioTuning.cs` | +10 | `LoopEvalMaxTicks = 40` |
| 改 | `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` | D1 收窄 | 具名面 + 反空转守卫(公开签名保留,委派重载) |
| 改 | `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs` | +4 | 白名单 4 项(用户裁定批准) |
| 改 | `unity/Assets/Tests/EditMode/Audio/net_derivation_test.cs` | 19→34 测 | 自造 Fake 全数删除 |

## §3 证伪轮 —— 7 例全红(停用生产码守卫 ⇒ 断言转红)

**纪律**:每例**改 → 跑 → 还原**在**同一命令内**完成(`/tmp/fz_one.py`):
① 先把干净文件复制到 `/tmp/fzsrc-{tag}.cs`;② 改生产码;③ 跑 Unity EditMode(音频过滤);
④ `finally` 块还原;⑤ 还原后**逐字节校验**。

**还原核验**(全 4 文件与 `/tmp/fzsrc-*.cs` diff 一致):
```
✓ CueEmitDeriver 与干净备份一致
✓ AnchorGate     与干净备份一致
✓ EndLoopWatchdog 与干净备份一致
✓ CueRenderSink  与干净备份一致
```
**残留扫描**:`grep -rn "停用全序排序\|停用 Intensity 折叠\|停用 Looped 折叠\|停用 SetTier\|停用锚点\|if (false)\|/\* MUT \*/" unity/Assets/Gameplay.Presentation/Audio/` ⇒ **零命中**。

| 例 | 停用的守卫(生产码) | 转红断言 | 红数 |
|---|---|---|---|
| ① sort | `keyed.Sort(static (a, b) => a.Key.CompareTo(b.Key));` | `test_reorderInvariance_arrivalOrderPreservesCueHash` | 1 |
| ② intensity | `state = SplitMix64.Fold(state, c.Intensity);` | `test_reorderInvariance_intensityFeedsHash` | 1 |
| ③ loopfold | `state = SplitMix64.Fold(state, c.Looped ? 1UL : 0UL);` | `test_reorderInvariance_allPayloadFieldsFeedHash` | 1 |
| ④ pending | `if (cue.Looped)` → `if (false)` | `test_anchorAbsent_duplicatePending_lastWins` · `test_anchorAbsent_oneShotDropped_loopCuePending` · `test_anchorArrived_pendingLoopReleased` | 3 |
| ⑤ stale | `=> candidateTick < latestTick;` → `=> false;` | `test_anchorStale_refused` | 1 |
| ⑥ threshold | `return stagnant > maxStagnantTicks;` → `return false;` | `test_endLoop_selfAssessPredicate_pureAndBounded` · `test_endLoop_thresholdBoundary_zeroAndEqual` | 2 |
| ⑦ settier | `SetTierCallCount++;` | `test_setTier_defaultLocalRecorded` | 1 |

**原始输出**(`/tmp/fz_all.out`):
```
[反证 1_sort]      failed=1 total=320 还原OK
   RED: test_reorderInvariance_arrivalOrderPreservesCueHash
[反证 2_intensity] failed=1 total=320 还原OK
   RED: test_reorderInvariance_intensityFeedsHash
[反证 3_loopfold]  failed=1 total=320 还原OK
   RED: test_reorderInvariance_allPayloadFieldsFeedHash
[反证 4_pending]   failed=3 total=320 还原OK
   RED: test_anchorAbsent_duplicatePending_lastWins
   RED: test_anchorAbsent_oneShotDropped_loopCuePending
   RED: test_anchorArrived_pendingLoopReleased
[反证 5_stale]     failed=1 total=320 还原OK
   RED: test_anchorStale_refused
[反证 6_threshold] failed=2 total=320 还原OK
   RED: test_endLoop_selfAssessPredicate_pureAndBounded
   RED: test_endLoop_thresholdBoundary_zeroAndEqual
[反证 7_settier]   failed=1 total=320 还原OK
   RED: test_setTier_defaultLocalRecorded
```

### §3.1 我的预测与实际不符两处(如实登记,不修饰)

| 例 | 我的预测 | 实际 | 归因 |
|---|---|---|---|
| ② intensity | 2 红 | **1 红** | `allPayloadFieldsFeedHash` 只断言 `Source` / `Looped`,**不断言** `Intensity` ⇒ 不该红。**我的预测过宽,非夹具缺陷**(该测的职责是「全字段」里 Source/Looped 两项,Intensity 由 ② 专测) |
| ④ pending | 2 红 | **3 红** | 多出 `test_anchorArrived_pendingLoopReleased` —— `Pending` 分支停用后该路径不可达,断言随之失败(合理) |

> **登记理由**:证伪轮的价值正在于**证伪我自己的预期**。把「预测 2 红实得 1 红」写成
> 「1 红(符合预期)」就是 S13-9 那类自述失真。两处均保留原始预测值。

## §4 回归终值

**`3538 / 3492 / 0 / 46`**(`unity/Logs/s14-2-final.xml`,2026-10-16 06:30Z,EXIT=0,duration 51.9 s)

- 基线(S14-1,commit `a2c6bd6`)= `3523 / 3477 / 0 / 46`
- ⇒ **+15 / +15 / 0 / 46 不变,零回归**
- `NetDerivationTest` = **34 / 34 Passed**(逐条核于 XML)
- `AssemblyBoundaryTest` = **24 / 24 Passed**(白名单增列生效)

**验证命令**:
```bash
grep -o 'total="3538" passed="3492" failed="0" skipped="46"' unity/Logs/s14-2-final.xml
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s14-2-final.xml').getroot();print(sum(1 for t in r.iter('test-case') if 'NetDerivationTest' in t.get('fullname','')), sum(1 for t in r.iter('test-case') if 'NetDerivationTest' in t.get('fullname','') and t.get('result')=='Failed'))"
# ⇒ 34 0
```

## §5 D1 门收窄的细节(用户裁定 ②)

**病因**(评审原件 §1.3):原 D1 扫描面 = 整个 44 生产装配。落地 `AnchorGate`(必须持
`IPositionalChannel` —— 它是位置面)后,**原门必红**;而若不落 `AnchorGate`,扫描面里
被断言的派生类**不存在** ⇒ 零违例对空集平凡成立(「空转真扫」)。

**处置**:扫描面收窄为**具名派生面** `{CueEmitDeriver, CueEmitInput}` + **反空转守卫**:

```csharp
// 反空转守卫
if (namedSeen != null)
    foreach (string want in named)
        if (!namedSeen.Contains(want))
            errs.Add($"[D1] 具名派生面类型「{want}」... 未找到 —— 扫描面为空,
                      「零违例」是对空集的平凡成立(反空转守卫;评审原件 §1.3「空转真扫」)。");
```

**自证**:`test_derivationSurfaceScan_absentNamedType_isRed` —— 传一个不存在的具名类型 ⇒ 必须 RED。

**分层语义**(写进 `AnchorGate.cs` 文件头):**派生面零 `IPositionalChannel` 引用**
(IL 扫描硬约束)· **位置面持它** —— 这正是 AC-44-D1「44 不自建位置通道 / 只读取锚点」的结构表达。

## §6 门保持

| 门 | 判据 | 结果 |
|---|---|---|
| b5 | 含 `AudioCueDto`/`IAudioCueSink`/`AudioCueHandle`/`TierSource` token 的文件须声明 44 命名空间 | ✓ 4 新文件全在 `DaYiJingCheng.Gameplay.Presentation.Audio` |
| b3 | 44 公开签名里的项目类型须白名单 | ✓ 4 项经用户裁定批准增列 |
| b4 | 字段类型白名单 | ✓ 全落 {BCL, Sim.Contracts.*, 44 自身} |
| ADR-029 §③ | `Sim/` 内 `new PayloadRef(` | ✓ 真代码零命中(仅注释/doc 提及) |
| AC-44-D7 ② | 派生面禁 `Random`/`Time`/`DateTime`/`IPositionalChannel` | ✓ `CueEmitDeriver` 剥注释后真代码零命中 |

**验证命令**:
```bash
sed 's|//.*||' unity/Assets/Gameplay.Presentation/Audio/CueEmitDeriver.cs \
  | grep -n "UnityEngine\.Random\|DateTime\|IPositionalChannel\|Time\."   # ⇒ 空
sed 's|//.*||' unity/Assets/Gameplay.Presentation/Audio/AnchorGate.cs \
  | grep -c "IPositionalChannel"                                          # ⇒ 2(位置面,应持有)
```

## §7 NOT-RUN 如实登记(禁借绿)

- **AC-44-D7 / D1 的 4 人双实例运行面对拍** = **BLOCKED-BY-45**(P1b;
  `IPositionalChannel` 的发布者/代码实现未写,契约已定)。本 story 只声称**静态半**已证。
- **IL2CPP 三格**(ADR-012 矩阵:Linux-ARM64-IL2CPP / Windows-x64-IL2CPP / Apple Silicon)——
  **NOT-RUN**。本机仅 Mono;CI 真 runner 环境未配(`UNITY_LICENSE`)。挂 ADR-012 矩阵。

## §8 登记项(本批不做)

- **story-007 转 `Complete`** —— 归 **S14-7**(6 条复评批,`sprint-14.md` DoD 逐字要求)。
- **运行面接线**(`AnchorGate` ← 45 的真实 `IPositionalChannel` 实现)—— 归 P1b。
- **素材接入**(真实音频播放在 `ICueRenderTarget` 之后)—— 归 story-015/016。
- **S14-1 的账本/evidence 缺口** —— commit `a2c6bd6` **未落** `production/qa/evidence/` 原件、
  亦未更新 `tests/unit/audio_system/README.md` 的 story-008 段(仍写「测试用自持谓词面
  (FakeSourcePool/...)」,而 S14-1 已删 Fake)。**本批不追补** —— 评审不可事后追补
  (`.claude/docs/review-workflow.md` §不可追补);登记归 **S14-7** 的 6 条复评批一并处置。
