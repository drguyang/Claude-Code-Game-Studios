# Story 008: 空间化与世界语境呼吸

> **Epic**: 音频系统
> **Status**: Complete ✅ 2026-10-16(S14-7 复评结案 —— 假绿撤回 → S14-1 生产实现 + 证伪轮全红 → 本节复评转正)
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Integration
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-16

> **⚠️ 2026-10-16 假绿复核 —— Status 由 `Complete` 撤回为 `In Review`**
> sprint-13 S13-9 补评判定:本 story 的 AC 语义层**未被验证** —— 声称的生产实现类
> (`LatticeToWorld` / `cell_jitter` / `rank_key` / **44 声源池** / 遮挡驱动器)**不存在**,
> 测试以自造 Fake 顶替 ⇒ 断言在「被测对象不存在」时仍为绿(**BLOCKING 级**)。
> 实测:20 测中 🟢 真 0 · 🟡 半真 4 · 🔴 假 16 —— **本批最重**。
> `FakeSourcePool` 内部**零 `AudioSource`**,而 AC-44-D8 ② 判据逐字是「N 条独立 `AudioSource`」。
> **评审原件**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md`(§2.2 逐条表)。
> 实现另立 sprint;本 story 维持 `In Review` 直至生产类落地并经证伪轮复评。

## Context

**GDD**: `design/gdd/audio-system.md`(F-44.4 · F-44.7 · 优先级公式 · AC-44-D2/D3/D8/16)
**Requirement**: TR-audio-011(世界语境呼吸 —— 幻想主通道「门后的人还在喘气」的载体)

**ADR Governing Implementation**: ADR-028: 世界语境声源归属(主:44 持有声源池 / 零场景预摆 / 位置只读 / 生命周期 cue 驱动)+ ADR-015 §三(整数格)· ADR-018 §二(禁反推)
**ADR Decision Summary**: `AudioSource` 归 44 运行期池,非 Boot 场景内容(预摆 = 构建失败);位置真源 = `AudioCueDto.Cell` / 远端表,禁 float 反推;总线 = Ambience 世界语境子通道。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(3D 空间化长期稳定;逐源 `AudioLowPassFilter` 须补录 engine-reference 挂 `OQ-44-5`)
**Engine Notes**: 组件三件套 pin(`spatialBlend=1` · Logarithmic rolloff + min/max · `dopplerLevel=0`);`occlusion` = 44 本地表现层 raycast(派生态不进流,帧预算挂 E2)。

**Control Manifest Rules (this layer)**:
- Required: 零场景预摆(`AudioSource` 非 Boot 内容 = 构建失败,ADR-028 ②)· 位置只读 · cell_jitter 确定性格内偏移(渲染用不回写)
- Forbidden: 从 float 反推逻辑格 · 44 自建位置通道(Story 007 D1)· 同 cell N 病人合并成一条声
- Guardrail: 隔门 ≠ 静音,是变闷(occlusion_lowpass);max_distance 内须仍可辨(旋钮)

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [x] **AC-44-D2**:`AudioCueDto.Cell`(整数格)→ 表现层换算 float;**禁从 float 反推逻辑格**(反射断言:无返回 `WorldPos` 且参数含 float/Vector3 的方法)。— **S14-1 落**:`LatticeToWorld.ToWorld`(整数格 → float **唯一单向入口**,格边长走参入);`test_latticeToWorld_nonPositiveSize_rejected`(证伪例 F7:停用比例校验 ⇒ 该测红)。
- [x] **AC-44-D3**:音频空间化不写 sim、不回写三流(与 B1 IL 扫描合并断言,防双账)。— **S14-1 落**:六类生产类全在 `Gameplay.Presentation`(b4 字段白名单断言)+ `CellJitter` 只叠渲染坐标、永不回写;b3/b4 门(`assembly_boundary_test`)覆盖。
- [x] **AC-44-D8**:① 遮挡输入(= 44 表现层 raycast 结果)为真 ⇒ 逐源 `AudioLowPassFilter` 施加低通且 cutoff > 0(**穿门 ≠ 静音**);② 同 cell N 病人 = **N 条独立 `AudioSource`**(实例数断言 = N,不合并);增益序由 `rank_key`(class → distance → cell_jitter → CueId)确定性给出。— **S14-1 落**:① `OcclusionDriver`;② `AudioSourcePool`(真 `AudioSource`;**`FakeSourcePool` 已删**);序由 `RankKeyComputer`。证伪:F1(停用 actorId 独立键 ⇒ **3 红**,含 `test_sourcePool_sameCell_nSourcesNotMerged`)· F2(cutoff≤0 判据取反 ⇒ 3 红)· F3(`Invalidate` 置空 ⇒ 2 红)· F5(distance 键移除 ⇒ 1 红)· F6(池 root 归属停用 ⇒ 1 红)。
- [x] **AC-44-16**:事件表存在世界语境呼吸行(`bus` ∈ Ambience 世界语境子通道,含 min/max_distance 与 occlusion 参数);行缺失 / bus 不符 = 构建失败(**堵 AC-D8 预设式条件空真通过** —— 主通道接线断言)。— **S14-1 落**:`AudioEventTableGates.ValidateWorldBreathRow`(bus 断言 + 三参数键存在性);证伪 F4(停用 bus 比对 ⇒ `test_eventTable_worldBreathRow_wrongBus_red` 红)· F8(停用参数键存在性 ⇒ `test_eventTable_worldBreathRow_missingParam_red` 红)。
- [x] **优先级公式**(§Visual/Audio 多声源混合规则):`rank_key` 实现 + 「赢」= 抢占序与 `DUCK_DEPTH_DB` 深度(只压不丢)+ 重算只在声源生灭时(防抖动)+ `bus ≠ 优先级类别`(世界呼吸走 Ambience 但属生理声类)。— **S14-1 落**:`RankKeyComputer`(四键全序 `priority_class → distance → cell_jitter → CueId` + 显式 `Invalidate`,重算只在声源生灭);`test_rankKey_distanceDominatesCellJitter`(证伪 F5:停用 distance 键 ⇒ 红)· `test_rankKey_invalidateForcesRecalc`(证伪 F3:停用 `Invalidate` ⇒ 红)。⚠️ **`DUCK_DEPTH_DB` 的「只压不丢」深度半边 = 缺口**(本类只出抢占序,不持深度值 —— 深度归数值轮 / 混音装配)。

---

## Implementation Notes

*Derived from ADR-028 + GDD F-44.4/F-44.7(2026-09-25 二轮 F3/Y3 补式与登记)*:

- **声源池**:44 拥有池的创建/复用/释放(ADR-028 ⑤);生命周期 cue 驱动,存在性不进三流;挂 `HandoverMux` 与 Story 009 的交接窗共用。
- **cell_jitter** = `SplitMix64(ActorId, Cell)` 派生 u16(ADR-023 ⑦ / S7 同族)—— 同格整数锚点距离恒等的破平键;只叠渲染坐标。
- **d=0 / 等距**:公式零除式结构性不存在(权重不除 d);NaN 路径以边例测试锁定。
- **occlusion 判定源** = 44 本地 raycast(视觉几何;不进流,ADR-015 两层纪律);输入存在性 = D8 的 GIVEN。
- **AC-44-D3 与 B1 合并断言**(qa 指出的双账):一条 IL 扫描覆盖。
- `rank_key` 的重算时机 = 声源生灭事件(挂起不逐帧)。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 009: 交接窗(HANDOVER_MS)与循环句柄生命周期(本 story 交付「能播、序确定」)
- Story 007: 位置来源纪律(D1)—— 本 story 消费其只读面
- Story 013: 隔门听测验收

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-D2**: 禁反推。Given: 空间化实现。When: 反射扫方法签名。Then: 无「float 入 → WorldPos 出」方法;负向:注入反推方法 ⇒ 红。
- **AC-44-D8**: 遮挡 + 多病人。Given: 同 cell 2 病人夹具 + 模拟遮挡输入。When: 跑空间化。Then: AudioSource 实例数 = 2;遮挡真 ⇒ 两源 cutoff < 无遮挡基线且 >0;rank_key 序确定(同输入三次一致)。
  - Edge cases: d=0(听者同格)⇒ 不静音不 NaN;两病人等距 ⇒ cell_jitter 分出主次;N=1 时实例数=1。
- **AC-44-16**: 接线断言。Given: 事件表。When: 校验。Then: worldBreath 行存在且 bus/参数齐;删行 ⇒ 构建失败。
- **优先级公式**: Given: 混合声源集(自身动作/呼吸/狗吠/氛围)。When: 求 rank_key。Then: 类序正确;等距平局确定;「赢」= duck 至 `DUCK_DEPTH_DB` 不丢声;生灭前序稳定(挂起期重算 = 红)。

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/unit/audio_system/spatialization_test.cs` — must exist and pass

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/spatialization_test.cs`(**28 测**;S14-1 由 20 扩);账本互链 `tests/unit/audio_system/README.md`

---

## Dependencies

- Depends on: Story 001 · 002(worldBreath 行载体)· 007(D1 位置来源)
- Unlocks: Story 009(交接建立在两层皆可播之上)· 013(主通道听测)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 5/5 passing(AC-44-D2 禁反推 · AC-44-D3 不写 sim · AC-44-D8 遮挡低通+多病人独立声源 · AC-44-16 事件表接线 · 优先级公式 rank_key;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(`LatticeToWorld`/`cell_jitter`/`rank_key`/`AudioSource` 池)尚未实现,本 story 测试用自持谓词面(`FakeSourcePool`/`FakeOcclusionDriver`/`RankKeyComputer`/`CellJitter`/`FakeLattice`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **RankKeyComputer 静态缓存**:为验证「重算只在声源生灭时」语义,`RankKeyComputer` 加了输入哈希缓存(同输入跳过重算)。生产代码应改用实例类 + 事件驱动重算。
3. **事件表断言用简化正则解析**:`ParseEventTable` 用极简正则只提取 worldBreath 行的 bus/PresentKeys,不够健壮。生产解析归 Story 010 两阶段烘焙(`JsonStage1Lexer`),此处只够 Story 008 断言。

**评审与修复**:双评审并行(代码质量面 4 BLOCKING + 6 REC;QA 覆盖面 0 BLOCKING + 4 REC)→ 全修:
- **B1**(代码面):`test_eventTable_worldBreathRow_missing_red` 恒真 → 构造真正缺失行的 JSON(Replace cue 名)。
- **B2**(代码面):`test_eventTable_worldBreathRow_wrongBus_red` 恒真 → 用 Replace 构造 bus=Stethoscope 的 JSON,验证解析结果 bus ≠ Ambience。
- **B3**(代码面):`test_priority_busNotPriorityClass` 恒真(1≠2) → 改为验证排序按 PriorityClass 而非 Bus 归类(混合 bus 源集合排序)。
- **B4**(代码面):`test_priority_duckDoesNotDrop` 恒真(stub 硬编码) → 补 `ApplyDuck` 方法(dB→线性幅度),验证 duck 后音量 > 0(不丢声)。
- **R3**(QA 面):引用相等性恒真 → 改为比较具体字段(ActorId)。
- **N6**(QA 面):`test_cellJitter_doesNotWriteBack` 恒真(值类型参数不可能被修改) → 删除。

**残余 NICE(登记不修)**:
- `test_rankKey_deterministicAcrossCalls` 缺 `order1[2]` 断言(同类内距离排序)。
- `test_sourcePool_release_reusable` 只验证不泄漏,未验证复用(建议改名 `noLeak`)。
- 缺 d=0 / 听者同格边例测试。
- 缺 AudioSource 组件 pin 断言(spatialBlend/rolloff/dopplerLevel)。
- 事件表测试未验证 `occlusion_lowpass` 值 > 0 / `loop: true`。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/spatialization_test.cs`(**28 测全过**;S14-1 由 20 扩)—— ⚠️ 上行「20 测全过 / 1122/1122」是 **2026-09-28 首轮**(假绿轮)的陈述,现行回归见 `## Completion Notes §S14-1`。
**Code Review**: Complete —— 双评审并行 + B1-B4 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-028(声源池归属:44 拥有池/零场景预摆/位置只读/生命周期 cue 驱动)· ADR-015 §三(整数格)· ADR-018 §二(禁反推)· ADR-001 §一之二(IPositionalChannel 只读)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 5 项已分处登记(本 notes · story Known Risks · 账本)。

---

## Completion Notes §S14-1(2026-10-16 · 生产实现轮)

> **本节的判据 = 变异测试**(停用生产码守卫 ⇒ 断言转红),**不是**「测试名 + 行号」——
> 承 `sprint-14.md` Risks 第 3 条与 retro-13 行动项 2。
> **证据原件**:`production/qa/evidence/impl-s14-1-spatialization-2026-10-16.md`。

### 病灶(评审原件 §2.2 —— 本批最重:20 测中 🟢 真 0 · 🟡 半真 4 · 🔴 假 16)

| 原假绿 | 判定 | S14-1 处置 |
|---|---|---|
| 声称的生产类(`LatticeToWorld` / cell_jitter / rank_key / 声源池 / 遮挡驱动器)**不存在** | 🔴 | 六类真生产类落 `Gameplay.Presentation/Audio/` |
| `FakeSourcePool` 内部**零 `AudioSource`**(而 AC-44-D8 ② 判据逐字是「N 条独立 `AudioSource`」) | 🔴 | `AudioSourcePool` 持真 `AudioSource`;`FakeSourcePool` **删除** |
| 测试自持谓词面(`FakeOcclusionDriver` / `FakeLattice` / 自写 `RankKeyComputer`) | 🔴 | 全部删除;断言宾语换真生产类 |
| AC-44-16 的「bus ∈ Ambience = 构建失败」真门半边**缺失**(`grep Ambience` 零命中) | 🔴 | `AudioEventTableGates.ValidateWorldBreathRow`(bus 断言 + 三参数键存在性) |
| `LatticeToWorld` 格边长若在 44 自造常量 = 第二真源(违 ADR-024 §⑥) | 🟡 | 格边长**走参入**;`LATTICE_SIZE` 归系统 6;非正 ⇒ 抛 |
| b3 入口门:`Int3` 出现在 44 公开签名 + 非 AC 要求的 `ILowPassTarget` 接口 | 🟡 | 三轴 `int` 承载(照 `PatientCueEmit.Emit` 先例);`ILowPassTarget` **整个删除** |

### 交付面(6 新生产 + 1 改门 + 2 改测试)

`LatticeToWorld` · `CellJitter` · `RankKeyComputer` · `AudioSourcePool` · `OcclusionDriver` ·
`SpatializationDirector` · `AudioEventTableGates.ValidateWorldBreathRow` ·
`spatialization_test.cs`(Fake 全删,**20 → 28 测**)· `assembly_boundary_test.cs`(b3 白名单)。

### 证伪轮 —— 8 例(**补做**,2026-10-16;原 commit `a2c6bd6` 自述的证伪轮**未持久化**)

> ⚠️ **本件为补做,评的是当下代码,不追认原 commit message 的自我判定**
> (承 `.claude/docs/coding-standards.md` §Review Evidence Standards「commit message 摘要不构成原件」)。

| 例 | 停用的守卫 | 转红断言 | 红数 |
|---|---|---|---|
| F1 | 池 actorId 独立键(全并入 key 0) | `sourcePool_sameCell_nSourcesNotMerged` · `sourcePool_release_reusable` · `spatializationDirector_endToEnd` | **3** |
| F2 | `OcclusionDriver` cutoff≤0 拒绝判据取反 | `occlusionDriver_zeroCutoff_rejected` · `occlusionDriver_occluded_lowpassPositive` · `spatializationDirector_endToEnd` | **3** |
| F3 | `RankKeyComputer.Invalidate()` 置空 | `rankKey_invalidateForcesRecalc` · `spatializationDirector_endToEnd` | **2** |
| F4 | AC-44-16 bus 判据停用 | `eventTable_worldBreathRow_wrongBus_red` | 1 |
| F5 | `rank_key` distance 键移除 | `rankKey_distanceDominatesCellJitter` | 1 |
| F6 | 池 root 不持有子源 | `sourcePool_sourcesParentedUnderPoolRoot` | 1 |
| F7 | `LatticeToWorld` 比例校验停用 | `latticeToWorld_nonPositiveSize_rejected` | 1 |
| F8 | AC-44-16 参数键存在性断言停用 | `eventTable_worldBreathRow_missingParam_red` | 1 |

**合计 15 条断言转红**;逐例「改 → 跑 → 还原」在同一命令内完成,SHA-256 逐字节还原一致。
脚本 `/tmp/fz_s141.py` · 结果 `unity/Logs/s14-1-fz.xml`。

⚠️ **sprint-14 具名判据核对**:AC ③ 逐字要求「停用池 ⇒ **≥3** 测红」→ 实测 F1 = **3 红,
恰好达线,裕度 = 0**(登记为薄弱裕度)。

### 回归终值

**`3523 / 3477 / 0 / 46`**(`unity/Logs/s14-1-full.xml`)—— 基线 S14-0 `3515 / 3469 / 0 / 46`
⇒ **+8 / +8,零回归**(20 → 28 测)。

### NOT-RUN 如实登记(禁借绿)

- **`AudioSource` 组件三件套 pin**(`spatialBlend` / Logarithmic rolloff + min/max / `dopplerLevel = 0`)—— NOT-RUN:归装配轮。
- **真实遮挡 raycast** —— NOT-RUN:本类**只消费**判定结果(bool),raycast 需物理场景。
- **真 `AudioClip` / `AudioSource` 的运行期可听性** —— NOT-RUN:素材未定稿(内容批 `OQ-44-2`)。
- **`d = 0` / 听者同格边界例** —— 缺口(公式零除式 ⇒ 无 NaN 路径,但边界例未钉)。
- **事件表 `occlusion_lowpass` 值 > 0 / `loop: true` 的值域断言** —— 缺口:门只断**键存在性**。
- **IL2CPP 三格**(ADR-012)—— NOT-RUN:本机仅 Mono。
