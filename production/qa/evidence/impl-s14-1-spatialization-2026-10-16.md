# S14-1 实现证据 —— story-008(空间化与世界语境呼吸)· 2026-10-16

> **证据性质**:实现轮原件(`.claude/docs/coding-standards.md` §Review Evidence Standards ——
> 原判定 → 修复落点 → 验证命令,可证伪)。
> **对象**:`production/epics/audio-system/story-008-spatialization-world-breath.md`
> **上游判定**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md` §2.2
> (008 = 20 测中 🟢 真 0 · 🟡 半真 4 · 🔴 假 16 —— **本批最重**)
> **sprint**:`production/sprints/sprint-14.md` S14-1(Must Have · L 件)
>
> ## ⚠️ 本件的性质:**补做**,不追认原判定
>
> 本 story 的生产实现落于 commit `a2c6bd6`(2026-10-16 13:48),其 commit message **自述**
> 「证伪轮 8 条全红」并逐例列出了 ①–⑧ —— **但该证伪轮从未持久化**:
> 无脚本(`/tmp/fz_s141.py` 当时不存在)· 无结果 XML(`unity/Logs/` 下无 `s14-1-*fz*`)·
> 无证据原件(`production/qa/evidence/` 下无任何 S14-1 文件)。
>
> `.claude/docs/coding-standards.md` §Review Evidence Standards 逐字:
> **「commit message 摘要不构成原件 —— 它是索引,不是报告。」**
> ⇒ 本节 §3 的证伪轮为 **2026-10-16 重做**,评的是**当下代码**;
> **它不追认** commit message 里那 8 条的原始结果(方向吻合是巧合的印证,不是继承)。
> 承 `story-done` §「不可追补」纪律。

---

## §1 原判定 → 修复落点

| # | 原判定(评审原件 §2.2) | 严重度 | 修复落点 | 验证命令 |
|---|---|---|---|---|
| 1 | 声称的生产实现类(`LatticeToWorld` / `cell_jitter` / `rank_key` / 声源池 / 遮挡驱动器)**不存在** | 🔴 | 六类真生产类落 `Gameplay.Presentation/Audio/`:`LatticeToWorld` · `CellJitter` · `RankKeyComputer` · `AudioSourcePool` · `OcclusionDriver` · `SpatializationDirector` | 反证 F1–F7 |
| 2 | `FakeSourcePool` 内部**零 `AudioSource`**,而 AC-44-D8 ② 判据逐字是「N 条独立 `AudioSource`」⇒ 断言在被测对象不存在时仍绿 | 🔴 | `AudioSourcePool` 持真 `AudioSource`;`FakeSourcePool` 删除 | 反证 F1(3 红)· `test_sourcePool_*` |
| 3 | 测试自持谓词面(`FakeOcclusionDriver` / `FakeLattice` / 自写 `RankKeyComputer`) | 🔴 | 全部删除;断言宾语换真生产类 | 全表 |
| 4 | AC-44-16 的「bus ∈ Ambience = 构建失败」真门半边**缺失**(`grep Ambience` 零命中) | 🔴 | `AudioEventTableGates.ValidateWorldBreathRow`(bus 断言 + 三参数键存在性 + `PresentKeys` 缺失 = NOT-RUN 守卫) | 反证 F4 / F8 |
| 5 | `LatticeToWorld` 的格边长若在 44 自造常量 = 第二真源(违 ADR-024 §⑥) | 🟡 | 格边长**走参入**;`LATTICE_SIZE` 归系统 6(其 `world-and-ecozones.md:504` 至今无值);非正 ⇒ 抛 | 反证 F7 |
| 6 | b3 入口门:`Int3` 出现在 44 公开签名 + 非 AC 要求的 `ILowPassTarget` 接口 | 🟡 | 三轴 `int` 承载(照 `PatientCueEmit.Emit` 先例);`ILowPassTarget` **整个删除** | b3 门(`assembly_boundary_test`) |

---

## §2 交付面

| 类别 | 文件 | 内容 |
|---|---|---|
| 新(生产) | `Gameplay.Presentation/Audio/LatticeToWorld.cs` | 整数格 → float 的**唯一单向换算入口**(F-44.4);格边长**走参入**(禁自持常量);`≤ 0` ⇒ 抛 |
| 新(生产) | `Gameplay.Presentation/Audio/CellJitter.cs` | 确定性格内偏移(`SplitMix64(ActorId, Cell)` 派生 u16)· **破平键**;只叠渲染坐标,永不回写 |
| 新(生产) | `Gameplay.Presentation/Audio/RankKeyComputer.cs` | 四键全序(`priority_class → distance → cell_jitter → CueId`)+ **显式缓存失效**(重算只在声源生灭,GDD :848) |
| 新(生产) | `Gameplay.Presentation/Audio/AudioSourcePool.cs` | 真 `AudioSource` 池(ADR-028 ① 唯一所有者);自建隐藏 root(`HideFlags.DontSave`);**同 cell N 源不合并**;池内复用 |
| 新(生产) | `Gameplay.Presentation/Audio/OcclusionDriver.cs` | 逐源 `AudioLowPassFilter`;**cutoff ≤ 0 拒绝**(「穿门 ≠ 静音」);拒绝路径**先于**挂组件(不留半施用组件) |
| 新(生产) | `Gameplay.Presentation/Audio/SpatializationDirector.cs` | 装配链:池 → 换算 → 破平键 → 遮挡 → 序 |
| 改(门) | `Editor.Tools.Gates/AudioEventTableGates.cs` | `ValidateWorldBreathRow`(AC-44-16 真门半边;行缺失**刻意不报** —— 已由 `RequiredCues` 覆盖,避免撞既有 9 处 `errors.Count == 1`) |
| 改(测试) | `Tests/EditMode/Audio/spatialization_test.cs` | 自造 Fake 全删;**28 测**(原 20 —— 首轮数为 20,实现轮扩至 28) |
| 改(测试) | `Tests/EditMode/Audio/assembly_boundary_test.cs` | b3 入口白名单增列 |

---

## §3 证伪轮 —— 8 例(停用生产码守卫 ⇒ 断言转红)· **2026-10-16 补做**

**纪律**:每例**改 → 跑 → 还原**在**同一命令内**完成;`finally` 还原后做 **SHA-256 逐字节校验**
(不符即脚本崩)。脚本 = `/tmp/fz_s141.py`,结果原件 = `unity/Logs/s14-1-fz.xml`(最后一例)。
**过滤套件** = `DaYiJingCheng.Tests.Unit.Audio`,每例跑 **395 条**。

| 例 | 停用的守卫(生产码) | 转红断言 | 红数 |
|---|---|---|---|
| **F1** | `AudioSourcePool.Spawn` 的 **actorId 独立键**(改键为常量 `0` ⇒ 全部源合并成一条) | `test_sourcePool_sameCell_nSourcesNotMerged` · `test_sourcePool_release_reusable` · `test_spatializationDirector_endToEnd` | **3** |
| **F2** | `OcclusionDriver.Apply` 的 **cutoff ≤ 0 拒绝判据取反**(穿门 = 静音) | `test_occlusionDriver_zeroCutoff_rejected` · `test_occlusionDriver_occluded_lowpassPositive` · `test_spatializationDirector_endToEnd` | **3** |
| **F3** | `RankKeyComputer.Invalidate` 改为**空操作**(⇒ 缓存恒有效、永不复算) | `test_rankKey_invalidateForcesRecalc` · `test_spatializationDirector_endToEnd` | **2** |
| **F4** | AC-44-16 的 **bus 比对停用**(不比对 `WorldBreathBus`) | `test_eventTable_worldBreathRow_wrongBus_red` | **1** |
| **F5** | `RankKeyComputer.Compare` 的 **distance 键移除**(同类内不再「近者优先」) | `test_rankKey_distanceDominatesCellJitter` | **1** |
| **F6** | 池的 **root 归属停用**(子源不挂池 root) | `test_sourcePool_sourcesParentedUnderPoolRoot` | **1** |
| **F7** | `LatticeToWorld.ToWorld` 的**比例校验停用**(格边长非正被静默接受) | `test_latticeToWorld_nonPositiveSize_rejected` | **1** |
| **F8** | AC-44-16 的**空间化参数键存在性断言停用** | `test_eventTable_worldBreathRow_missingParam_red` | **1** |

**还原校验**:8 例跑完后 5 个被改文件的 SHA-256 **逐项等于**基线(`全部还原一致`)。
脚本在任一例还原不符时即中止(exit 3)。

### sprint-14 的具名判据核对

`sprint-14.md` 对 S14-1 的 AC ③ 逐字:**「证伪轮:停用池 ⇒ ≥3 测红」**。
**实测 F1(停用池的 actorId 独立键)= 3 红 —— 恰好达线,不构成「优于阈值」的裕度。**
⚠️ 若日后 `test_spatializationDirector_endToEnd` 或另两条被改写 / 删除,F1 会**跌破阈值**;
该判据的裕度只有 0。登记为**薄弱裕度**(非缺陷,但须在下次触碰该测时重新核对)。

### 与 commit 自述的对照(独立复现,非继承)

commit `a2c6bd6` 自述逐例为:① 池 actorId ⇒ 3 红 · ② cutoff≤0 ⇒ 1 · ③ rank_key 缓存 ⇒ 1 ·
④ AC-44-16 bus ⇒ 1 · ⑤ distance 键 ⇒ 1 · ⑥ 池 root ⇒ 1 · ⑦ 比例校验 ⇒ 1 · ⑧ 参数键 ⇒ 1。

| 例 | 自述 | 本次实测 | 说明 |
|---|---|---|---|
| ①/②/③ | 3 / 1 / 1 | **3 / 3 / 2** | 本次变异面**更广**(如 F2 直接取反判据而非只改一处阈值)⇒ 连带 `test_spatializationDirector_endToEnd` 等 |
| ④–⑧ | 1 / 1 / 1 / 1 / 1 | **1 / 1 / 1 / 1 / 1** | 逐例吻合 |

⇒ **方向与量级吻合**;差异全部可由「变异面宽窄」解释,无一处相左。

---

## §4 回归对账

| 轮次 | total | passed | failed | skipped | 说明 |
|---|---|---|---|---|---|
| 基线 S14-0(S14-1 前) | 3515 | 3469 | 0 | 46 | commit `a2c6bd6` 自述 |
| **本件全量 EditMode** | **3523** | **3477** | **0** | **46** | `unity/Logs/s14-1-full.xml`(**独立复核实测**) |
| 本件 Audio 过滤套件 | 303 | 302 | 0 | 1 | `unity/Logs/s14-1-audio.xml` |

**对账**:`3523 / 3477 / 0 / 46` —— 与 commit 自述**精确吻合** ⇒ `+8 / +8,零回归`。

⚠️ **一处订正**:commit message 写「**20 测**由自造 Fake 换真生产断言」;
`unity/Logs/s14-1-full.xml` 实测 `SpatializationTest` 用例 = **28**。
⇒ 「20」是**首轮(假绿轮)的测数**,实现轮把它扩到了 **28**;commit 摘要的措辞有误导
(读起来像「20 换 20」)。**已在 story-008 的 `## Test Evidence` 与账本 README 就地订正为 28。**

---

## §5 如实登记(禁借绿)

| # | 项 | 状态 | 解除条件 |
|---|---|---|---|
| 1 | **`AudioSource` 组件三件套 pin**(`spatialBlend` / Logarithmic rolloff + min/max / `dopplerLevel = 0`) | **NOT-RUN** | 归装配轮(池只保证「有源可用」,三件套是资产 / 调参面);story-008 首轮 `## Completion Notes` 已登记该 NICE 项 |
| 2 | **`d = 0` / 听者同格边界例测** | 缺口 | 公式**零除式**(权重不除 d)⇒ 无 NaN 路径,但边界例未钉;首轮已登记 |
| 3 | **事件表 `occlusion_lowpass` 值 > 0 / `loop: true` 的断言** | 缺口 | 门只断**键存在性**(AC-44-16 字面),不断**值域**;值域归数值轮 |
| 4 | **真实遮挡 raycast** | NOT-RUN | 本类**只消费**判定结果(bool),raycast 需物理场景 ⇒ 归装配轮 |
| 5 | 真 `AudioClip` / 真 `AudioSource` 的**运行期可听性** | NOT-RUN | 素材本体未定稿(内容批 `OQ-44-2`);承 `story-013`(主通道听测,`Complete` 只覆盖文档判据) |
| 6 | IL2CPP 三格(ADR-012 矩阵) | **NOT-RUN** | 本机仅 Mono;CI 矩阵 <br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。 |

---

## §6 落地清单(供 commit 参考)

- **新(生产)**:`LatticeToWorld` · `CellJitter` · `RankKeyComputer` · `AudioSourcePool` ·
  `OcclusionDriver` · `SpatializationDirector`
- **改(门)**:`AudioEventTableGates.cs`(`ValidateWorldBreathRow`)
- **改(测试)**:`spatialization_test.cs`(Fake 全删,20 → 28)· `assembly_boundary_test.cs`(b3 白名单)
- **改(账本)**:`production/epics/audio-system/story-008-*.md`(计数订正 20 → 28)·
  `tests/unit/audio_system/README.md`(story-008 段:生产类 / Fake 已删 / 证据原件链)

**⚠️ 未 commit** —— 无用户指令。
