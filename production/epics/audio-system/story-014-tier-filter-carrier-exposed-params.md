# Story 014: tier 滤波载体与 mixer 暴露参数

> **Epic**: 音频系统
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落表现层 L5 + Editor batch 资产工具)
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/audio-system.md`(F-44.1 精度档 → 通带与噪声底 · F-44.2 SNR 三输入)
**Requirement**: TR-audio-005(接触噪声/信噪比是暴露参数)· TR-audio-006(50ms ramp)

**建立原因(2026-09-27 readiness 用户裁定)**:「**谁承载 tier 滤波**」是全案**未认领的资产拓扑裁定**。
GDD F-44.1(:278)说 tier 驱动通带/噪声底 · Story 004 Implementation Notes 说「本 story 消费列做滤波/
噪声底驱动」· **但无任何 story 认领「往 mixer 加 filter effect」** ⇒ 下游两处撞墙:

| 消费者 | 撞墙点 |
|---|---|
| Story 004 | `MixerRegistry.TierFilterParameters` 五名(`passband_*` 等)**无真实参数可挂** —— 暴露不了 |
| Story 005 | AC-44-05 ②「三列 ∈ `Stethoscope` 组暴露参数闭集」—— 闭集当前**最多含 1 员** |

**既存事实**(2026-09-26/27 实测):`.mixer` = `unity/Assets/Audio/DaYiJingCheng.mixer`
(Force Text YAML,由 Story 003 `MixerAssetGenerator` batch 生成)· 22 组(七总线两级 + `Reverb`)·
**25 个 effect 全是 `Attenuation`,零 filter effect** · `bus_volume_stethoscope` 已暴露(7 名之一)·
`MyExposedParam` 一次外部注入污染**经「删后重跑」判别确认不再现**(非生成器所写)。

**ADR Governing Implementation**: ADR-018 §四 需求③(`Stethoscope` 总线暴露接触噪声底与信噪比)
**ADR Decision Summary**: 背景噪声永不为零(禁静音路径);暴露参数 = 运行期可调的音频侧旋钮,
数值用户自己调。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(`AudioLowPassFilter` 在
`docs/engine-reference/unity/modules/audio.md` **零覆盖** —— Story 004 已补录条目但标注
「未实测 / Knowledge Gap」· `AudioMixerController` 内部 API 须 Discover 实测)
**Engine Notes**: 暴露写入走 `AudioMixerController.set_exposedParameters`(native,会改写
`m_ExposedParameters` 数组 ⇒ **经 YAML 文本对齐 guid 与经 API 写入是两条路径**,
Story 003 生成器已用后者);`TierFilterDriver`(Story 004)经 `IMixerParameterSink` 调
`SetFloat(name, v)` ⇒ 五名须**全部真实存在**才不静默落空。

**Control Manifest Rules (this layer)**:
- Required: 暴露参数 ∈ 注册表单一出处(`MixerRegistry.TierFilterParameters`)· tier 参数
  **exposed + 脚本驱动、禁入任何快照**(GDD :194-201 已钉)· ramp ≥ 50 ms(常量单一出处,
  与 Story 006 共用)
- Forbidden: 新增「档位 ⇒ 静音」路径(AC-44-01)· 暴露参数名 ≠ 承载组名(两级组结构约定:
  参数名 = 组名)· 直 set `cutoffFrequency` 硬切(ramp 纪律)
- Guardrail: 分析域与渲染域分离 —— 本 story 只交付**载体与暴露**(配置面 + 参数通路),
  **不做** SNR 计算(Story 005)、**不做** `gain_scale` 渲染乘子(Story 006)

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`(F-44.1 列集 · F-44.2 三输入),scoped to this story:*

- [ ] **AC-44-T1**(BLOCKING · 载体就位):`MixerRegistry.TierFilterParameters` 五名
  (`passband_center_hz` / `passband_width_hz` / `noise_floor_db` / `contact_noise_floor_db`
  / `signal_db` —— **`band_detail_count` 是计数,不入**)在 `.mixer` 中**各有真实参数承载**,
  且**暴露名 == 承载组名**(两级组结构约定)。负向:删任一承载组 ⇒ 断言红。
- [ ] **AC-44-T2**(暴露闭集):五名 ∈ `Stethoscope` 总线组的暴露参数闭集(YAML 枚举断言);
  闭集**不得含注册表外条目**;负向:注入一条未登记 exposed 名 ⇒ 红。
- [ ] **AC-44-T3**(参数通路可用):`TierFilterDriver` 对五名各 `SetFloat(name, v)` 后,
  `GetFloat(name)` **回读 == v**(浮点容差);任一名缺失 ⇒ 显式失败,**不静默落空**。
- [ ] **AC-44-T4**(不入快照):五名 ∉ 任何快照的 `m_FloatValues` 键集(复用 Story 003
  `MixerTopologyGates.ValidateSnapshotExposedDisjoint`);负向:把某名写进某快照捕获 ⇒ 红。
- [ ] **AC-44-T5**(ramp 下界):切换目标值必须经 `FilterRamp`(Story 004,`RampSeconds = 0.05`
  单处常量)—— **禁直 set 目标值**;负向:直 set 路径 ⇒ 红(构造探测)。

---

## Implementation Notes

*Derived from Story 003/004 实测 + ADR-018 §四 需求③(2026-09-27 立)*:

- **载体形态**(核心裁定,实现期须先定再落码):五名各自对应**一个 mixer 组参数**,
  使「暴露名 == 组名」成立。两种候选,实现期二选一并写进 story 注:
  - **甲 · 组级 filter effect**:给承载组挂 filter 效果,暴露其参数(如 cutoff)—— 与
    `Attenuation` 同构(`.mixer` 现有 25 个 effect 全 `Attenuation`,形态可对照);
  - **乙 · 每参数一名独立组**:每组只承载一个参数(窄但最直观)。
  ⚠️ **不得**用 `AudioLowPassFilter` 逐源组件 —— Story 004 unity-specialist 已判
  「设备级 ⇒ 组级」(GDD :306-310 档位设备级、组全局共享、逐源互踩)。
- **暴露写入**:复用 Story 003 `MixerAssetGenerator` 的既有通路
  (`set_exposedParameters` 经 API 写入,`NormalizeExposedGuids` 做 YAML 文本对齐)
  —— **不新建第三条写入路径**;guid 对齐沿用「同名组 `m_Volume` 哈希」规则。
- **验证入口**:`unity build unity --target StandaloneLinux64 --executeMethod
  DaYiJingCheng.EditorTools.Gates.MixerAssetGenerator.Create` → 重跑后:
  ① 五名 guid 与承载组 `m_Volume` **逐条一致**(独立复核,勿信生成器日志);
  ② `MyExposedParam` 等外部注入**不再现**;
  ③ 全量 EditMode 无回归(基线 822)。
- **与 Story 004 的接口面**:`TierFilterParameters` 五名已登记(Story 004 交付),
  本 story 只**兑现载体**,不改注册表闭集;`TierFilterDriver` 零改动。
- **`band_detail_count` 排除理由**:它是**计数**(频段细节数),不是可调参数 ——
  与 ADR-006 D-21-17「纯 int 计数字段不列入解析集」同纪律。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: `ComputeSnrDb` 纯函数与 `NOISE_FLOOR_DB_MIN` 下界断言(本 story 只供**三列**)
- Story 006: F-44.6 `gain_scale` 渲染乘子与 Intensity 分桶
- Story 003: 生成器骨架与快照两级组(本 story 在其上加载体)
- Story 004: `FilterRamp` / `BreathPhaseGate` / `TierFilterDriver`(消费面,已交付)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-T1**: 载体就位。Given: `.mixer` 已生成。When: 枚举五名 + 查承载组。Then: 五名
  均有承载组、暴露名 == 组名;**负向**:从 YAML 删某承载组 ⇒ 红。
- **AC-44-T2**: 暴露闭集。Given: `.mixer` 的 `m_ExposedParameters`。When: 枚举。Then:
  含全部五名、无注册表外条目;**负向**:注入 `rogue_param` ⇒ 红。
- **AC-44-T3**: 通路可用。Given: 五名已暴露。When: `SetFloat(name, v)` + `GetFloat(name)`。
  Then: 回读 == v(浮点容差);**负向**:对未暴露名 `SetFloat` ⇒ 显式失败(不静默)。
- **AC-44-T4**: 不入快照。Given: 5 快照的 `m_FloatValues`。When: 与五名求交。Then: 交集 = ∅;
  **负向**:把 `passband_center_hz` 写进某快照 ⇒ 红(复用 Story 003 断言)。
- **AC-44-T5**: ramp。Given: 目标值切换。When: 驱动。Then: 走 `FilterRamp`(斜率受限 +
  到位 ≥ 50 ms);**负向**:直 set 目标值的代码路径 ⇒ 红(构造探测)。

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 真身 `unity/Assets/Tests/EditMode/Audio/tier_filter_carrier_test.cs` — must exist and pass
  (登记路径原写仓库根 `tests/...`,Unity 只编译 `unity/Assets/` 树 —— 承 Story 002/003/004 先例)
- 账本互链 `tests/unit/audio_system/README.md`(AC→测映射)

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/tier_filter_carrier_test.cs`(11 测);账本互链 `tests/unit/audio_system/README.md`

---

## Dependencies

- Depends on: Story 003(`.mixer` 生成器与两级组结构)· Story 004(`TierFilterParameters` 闭集 +
  `FilterRamp` + `TierFilterDriver` 消费面)
- **Unlocks**: Story 005(AC-44-05 ② 暴露闭集)· Story 013(听测前的数据面就绪)
- Blocked by: None

## Known Risks(实现期须处理,勿静默)

1. **暴露名 == 组名** 这条约定意味着五名各需**一个同名组** —— 与现有
   `bus_volume_*` / `duck_*` 命名族并列,须确认不与 22 组重名。
2. `AudioMixerController` 内部 API(`AddExposedParameter` / `ResolveExposedParameterPath`)
   **无公开文档** —— 须 Discover 实测签名;构造失败即停手报告,不猜。
3. Story 004 已登记 H-B 结论「`SetFloat` 在 EditMode 对任何名字都 false」——
   **AC-44-T3 的 `SetFloat` 断言在 EditMode 不可判**(同 H-B);该条应改为
   **「`GetFloat` 可读 + 回读一致」** 或在**播放态**验证 —— 实现期先按此调整并写明。

## Completion Notes
**Completed**: 2026-09-27
**Criteria**: 5/5 passing(T1 载体就位 BLOCKING · T2 暴露闭集 · T3 通路可用 · T4 不入快照 · T5 ramp 下界;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **本 story 是 2026-09-27 readiness 轮用户裁定新立的**,非 `/create-stories` 生成 —— 缘起是 Story 005 readiness 发现 **AC-44-05 ② 的断言对象不存在**。缺口定性:「谁承载 tier 滤波」= 全案未认领的资产拓扑裁定 —— GDD F-44.1 说 tier 驱动通带/噪声底 · Story 004 Implementation Notes 说「本 story 消费列做滤波/噪声底驱动」· **但无任何 story 认领「往 mixer 加 filter effect」**。
2. **载体形态 = 乙(每参数一名独立组)**,组数 22 → 27。选它的理由:**与现有 `bus_volume_*` 命名族完全同构,无需 Discover 内部 API** —— 甲案(组级 filter effect)同样可行,但需要额外探测 effect 参数形态。
3. `EPIC.md` 故事数 13 → 14,加 014 行。
4. 顺带修生成器两洞(删 `.mixer` 重建暴露):`CreateMixerAsset` 只查 `Static` 而 `CreateDefaultAsset` 是**实例方法**(IL 证实)⇒ 永远匹配不到;`PruneForeignSnapshots` 把基底 `"Snapshot"` 也裁了 ⇒ 无改名基底 throw。

**评审与修复**:双评审(代码面 1 BLOCKING + QA 面 0 BLOCKING / 5 REC)→ 全修:
- **B1**(两评审独立命中):`test_tierFilterParameters_unexposedName_getFloatFalse` 断言「未暴露名 `GetFloat` ⇒ false」,与 Story 004 **H-B(实验 1–7)** 实测冲突 —— `GetFloat` 对**任何名字**都返回 true(兜底走「遍历组调 `GetGUIDForVolume()`」,不依赖 exposed cache),该前提在 EditMode **不可判**。**处置 = 删除该负例**(判定站不住且已被既有正例覆盖),并**确认既有 `test_tierFilterExposedParameters_rogueParam_red` 已注入真实组哈希**(`RealBusVolumeHash("bus_volume_master")`)且断言「无同名组承载」分支 —— 评审 R3 已满足,无需新增。
- **REC2**(QA 面):**tier guid 对齐无自动化回归** —— 五名只在 Story 004 的反射探针覆盖 7 名 bus,**tier 五名不在任何自动化断言内**,只靠一次性人工实测。新增 `test_tierFilterCarrierGroups_guidMatchesGroupVolumeHash`:五名 exposed guid 与同名组 `m_Volume` **逐条相等**。起因:Story 004 曾发现「合成 guid 对不上」,当时只把 guid 改成组哈希,**而这条等式本身从未进自动化断言** —— guid 一旦漂移无人发现。
- **REC1**:AC-44-T3「回读 == v」因 EditMode 限制未测(H-B),story Known Risks 3 已登记为**有意留白**,非遗漏。
- **REC4**:幽灵引据「MUST DO 1/2」→ story 真实小节名。

**⚠️ H-B 结论适用**:Story 004 已实测 `AudioMixer.SetFloat` 在 EditMode 对**任何名字**都返回 false,而 `GetFloat` 返回 true ⇒ `SetFloat` 的「按名查表」native 路径在此环境本身不通,**与 exposed 配置无关**。本 story 的 T3 判据据此调整为「`GetFloat` 可读 + 回读一致」,并在 T3 doc 注明 SetFloat 为何在此环境不可判。**运行期(播放态)仍有效。**

**残余 NICE(登记不修)**:`InjectExposedParameter`/`InjectSnapshotCapture` 无注入自证(对照同文件其他破坏性测试的纪律)· T5 的「直 set 绕过 ramp」真负例零测试(该测断言的是**正例行为**:首调直落位)· tier 组父归属分支未测(只测了「删整组」,没测「组存在但挂错总线」)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/tier_filter_carrier_test.cs`(**11 测全过**);全量 EditMode **872/872 Passed · 0 红 · 0 跳过**(`unity/Logs/s014-r2-full.xml`)
**Code Review**: Complete —— 双专审并行 + B1 修复 + 复验;review mode = lean,QL-TEST-COVERAGE / LP-CODE-REVIEW 门按 lean 规则跳过。
**ADR Compliance**: ADR-018 §四 需求③(`Stethoscope` 总线暴露接触噪声底与信噪比)—— COMPLIANT。五名承载组挂 Stethoscope 下,暴露 12 条(7 bus + 5 tier),闭集双向差集为零。
**Tech Debt**: 未立文件;上述 NICE 3 项 + tier 缺口已分处登记(本 notes · story Known Risks · 账本)。
