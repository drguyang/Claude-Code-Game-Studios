# Story 008: 设备态与呈现契约(QueryBinding · 合成 release · Mixed 迟滞)

> **Epic**: 输入与设备
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/input-system.md`(§States 设备态 · §Tuning Knobs 二 · §Visual/Audio 二)
**Requirement**: TR-input-011(设备移除合成 release + Mixed 来源迟滞,不卡键不抖切)· TR-input-013(`QueryBinding` 只回键名不回素材)· TR-input-015(⚠️ partial:P0 只落震动通道**接口**,`OpenXRInput` spike 挂账)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主): 输入架构
**ADR Decision Summary**: ADR-011 §一 / §Implementation Guidelines 4 —— 设备态(KbmOnly / PadOnly / Mixed / XrActive / NoDevice)由**最近一次有效输入**决定,不叠加不双活;呈现契约走 `QueryBinding(device, bindingPath)` 类型化查询,返回结构**不含**素材与时机字段(ADR-013 §9 C3「42 只渲染不持状态」的输入侧对称 —— 输入层给数据,渲染层给画)。迟滞三旋钮归 §Tuning Knobs 二,数值归用户。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: 设备热插拔事件(`InputSystem.onDeviceChange` 族)与摇杆漂移模拟均 post-cutoff 零覆盖,ADR-011 HIGH 评级的直接落点;震动通道 `OpenXRInput` spike(`OQ-3-4` / GDD §Visual/Audio 二)是 P0 悬置项 —— spike 失败则通道接口保留、实现挂账,不改本故事判据。`QueryBinding` 返回结构与热插拔是纯类型/事件面,MEDIUM;HIGH 来自 spike 面。

**Control Manifest Rules (this layer)**:
- Required: 设备态取「最近一次有效输入」,不叠加不双活(States 设备态)· 迟滞三参数按设备类分两列入 ADR-014 常量表(manifest Core · 输入(ADR-011))
- Forbidden: 呈现结构携带素材(`Sprite`/`Texture`/字体)或出现时机/可见性字段(AC-D2)· 用逐分量比较做 Mixed 切换判定(GDD AC-D4 明禁 —— 必须径向模长)
- Guardrail: 迟滞三旋钮(`DEVICE_SWITCH_THRESHOLD` / `DRIFT_TOLERANCE` / `DWELL_DURATION`)数值归用户(旋钮表),本故事只交机制与区间断言

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [x] **AC-3-D1(BLOCKING)**: `QueryBinding` → `{device, bindingPath, iconKey}`;**未绑定返回明确枚举**(非空指针、非异常)
- [x] **AC-3-D2(BLOCKING)**: **无素材、无时机** —— 构建报告断言交付物无 `Sprite`/`Texture`/字体(**非 grep 源码**);返回类型**无**出现时机/可见性字段(类型断言)
- [x] **AC-3-D3(BLOCKING)**: **设备移除 → 合成 release** —— 全部 `IsPressed` 转 `false`,无卡键(EditMode 模拟断连)
- [x] **AC-3-D4(BLOCKING)**: **Mixed 迟滞** —— 三条件**同时满足**才切源:① `> DEVICE_SWITCH_THRESHOLD` ② `> DRIFT_TOLERANCE` ③ 持续 `> DWELL_DURATION`;比较域 = **径向 `‖Δ‖`**(鼠标侧指针增量模长单列,轴设备侧轴幅度;**禁逐分量**);三参数按设备类分两列(ADR-014 常量表)。EditMode:鼠标静止 + 摇杆漂移在容差内 ⇒ 来源保持 Kbm。**具体数值已移出 AC 入旋钮表**
- 附注(**无 AC** · `TR-input-015` partial):震动通道 P0 **只落接口**;`OpenXRInput` 能力 spike(`OQ-3-4`)结果记入 Completion Notes,失败则接口保留、实现挂账

---

## Implementation Notes

*Derived from ADR-011 §Implementation Guidelines 4 + GDD §States / §Tuning Knobs 二 / §Visual/Audio 二:*

- **设备态五值**(States):`KbmOnly` / `PadOnly` / `Mixed`(**最近一次有效输入**,不叠加不双活)· `XrActive` / `NoDevice`。态转移由直读通道观察到的有效输入驱动
- **`QueryBinding` 契约**(D1):入参 `(device, bindingPath)`;返回 `{device, bindingPath, iconKey}`;**未绑定 = 显式枚举**(如 `NotBound`),不是 null、不是异常 —— 消费方(42 / 20)按枚举分支
- **`iconKey` = 键名非素材**(GDD §Visual/Audio 三):字符串键,素材图集归 42/美术;D2 的两道断言(构建报告 + 类型断言)把「输入层交数据、呈现层给画」机械锁死
- **D3 合成 release**:设备断连事件(`onDeviceChange` Removed/Disconnected)→ 对该设备全部处于 pressed 的绑定注入合成 release(等效 `IsPressed=false`),**同帧**内完成;EditMode 测试用可控断连模拟,不依赖真硬件
- **D4 三条件迟滞**(`§Tuning Knobs 二`):切源需**同时**满足幅度阈 + 容差 + 持续时长(DWELL);**比较域 = 径向模长** —— 鼠标侧 `‖Δpointer‖` 单列、轴设备侧 `‖Δaxis‖` 单列(两列分设备类,常量表走 ADR-014);逐分量比较(禁)会把对角漂移误判为切换
- **EditMode 黄金例**:鼠标完全静止 + 摇杆漂移向量在 `DRIFT_TOLERANCE` 内 ⇒ 来源**保持** Kbm(不因漂移抖切到 Pad)—— 这是迟滞存在的全部理由
- **震动接口**:P0 定义 `IHaptics` 通道接口(强度/通道枚举),`OpenXRInput` spike 评估可行性;数值与波形归用户与后续轮

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 009:焦点导航(Navigate)的意图视图 —— 设备态 ≠ 焦点态;`Navigate` 的动作结构归 009
- Story 010:热路径零成本(E1/E4/E5)—— `QueryBinding` 的调用频度约束归 010
- 呈现层图集 / Sprite 实际渲染(D2 明确排除素材)—— 42 的 GDD
- 迟滞三旋钮**数值**(阈值/容差/DWELL 具体值)—— 用户数值轮,本故事只交机制与「分两列」结构

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-3-D1**: `QueryBinding` 形状与未绑定枚举。
  - Given: 一组 `(device, bindingPath)` 查询,含已绑定与未绑定各若干。
  - When: 调 `QueryBinding`。
  - Then: 已绑定返回 `{device, bindingPath, iconKey}` 三字段;未绑定返回明确枚举值(非 null、不抛异常)。
  - Edge cases: `device` 未知枚举值;`bindingPath` 空串;复合绑重的 part 路径。
  - Negative fixture: 实现用 null 表未绑定 ⇒ 红;用异常 ⇒ 红。
- **AC-3-D2**: 无素材、无时机 —— 构建报告 + 类型断言。
  - Given: 本 Epic 全部呈现向交付物与 `QueryBinding` 返回类型。
  - When: ① 构建报告扫描(非源码 grep)② 返回类型字段反射。
  - Then: 构建产物中无 `Sprite`/`Texture`/字体资源由本层引入;返回类型字段 ∈ {`device`, `bindingPath`, `iconKey`} + 枚举,**无** `visible`/`showAt`/`duration` 类时机字段。
  - Edge cases: 经常量字符串引用素材路径(构建报告按资产引用扫,不按源码字面扫);`iconKey` 值本身含素材扩展名(键名不约束)。
  - Negative fixture: 返回类型加一个 `float fadeDuration` ⇒ 类型断言红。
- **AC-3-D3**: 设备移除 → 合成 release,全部 `IsPressed` 转 false。
  - Given: 模拟设备按住若干键(`IsPressed=true`)。
  - When: EditMode 触发该设备断连。
  - Then: 该设备全部绑定 `IsPressed` 变 false(同帧);无残留 pressed;后续帧不复跳 true。
  - Edge cases: 断连时正处于 composite 按住(方向键一半);断连的设备是当前 Mixed 源(态随迁移到存活设备);无按住键的设备断连(无操作,不抛)。
  - Negative fixture: 实现只改内部状态不发 release 事件 ⇒ 读侧仍 true ⇒ 红。
- **AC-3-D4**: Mixed 迟滞三条件 —— 漂移不抖切。
  - Given: 来源 = Kbm;鼠标静止(`‖Δpointer‖=0`);摇杆漂移向量幅值 < `DRIFT_TOLERANCE`。
  - When: 推进 `DWELL_DURATION` 相关帧步。
  - Then: 来源**保持** Kbm(漂移不构成有效输入);三个条件任一不满足 ⇒ 不切。
  - Edge cases: 漂移幅值恰 = `DRIFT_TOLERANCE`(边界,应不切);持续时长恰 = `DWELL_DURATION`(边界开闭侧须与旋钮表口径一致,测试内明写);三条件全满足 ⇒ 切到 Pad;**径向 vs 逐分量** —— 构造对角漂移(每分量 < 阈但 `‖Δ‖ > 阈`)⇒ 径向判定**应**切,若实现逐分量则漏切(负例自证)。
  - Negative fixture: 关掉 DWELL 条件(漂移一到就切)⇒ 来源 Kbm→Pad 抖动 ⇒ 红。
- **附注(`TR-input-015`,无 AC)**:震动通道 —— Verify:`IHaptics` 接口存在且被 10 的调用点引用;`OpenXRInput` spike 结果(可用/不可用)记入 Completion Notes;不可用 ⇒ 接口保留、实现挂账,不改其他 AC 判定。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/input_system/device_and_presentation_test.cs` — must exist and pass(含 D1–D4 四组)

**Status**: [x] Created — EditMode 39 测全绿(见 Completion Notes)
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 真身落 `unity/Assets/Tests/EditMode/InputSystem/device_and_presentation_test.cs`,文档路径 `tests/integration/input_system/` 为登记口径

---

## Completion Notes
**Completed**: 2026-09-27
**Criteria**: 4/4 passing(AC-3-D1 / D2 / D3 / D4,零 UNTESTED;附注 IHaptics 接口测 1 测)
**Deviations**: 零缺陷性偏差;ADVISORY 项全入本节。
**Test Evidence**: Integration —— EditMode `unity/Assets/Tests/EditMode/InputSystem/device_and_presentation_test.cs`(39 测全绿);批跑 1/1 → 823/823 → 861/861 exit 0
**Code Review**: Complete —— `/code-review` 双代理并行:unity-specialist **us-F1~F8 全修** + qa-tester **F1~F15 全修**(全部 findings 含 minor);复跑批跑 #9 = 861/861 exit 0
**Manifest**: 2026-09-21 无陈旧
**提交**: `9150cd2`(实现+评审修复批,已 push)

### Test-Criterion Traceability
| AC | 测试映射 | Status |
|---|---|---|
| AC-3-D1 QueryBinding | `test_queryBinding_bound_returnsThreeFieldsAndKeyNames` / `test_queryBinding_unbound_returnsExplicitEnum_notNullNotThrow` / `test_queryBinding_unknownDeviceEnumValue_reportsUnknownDevice` / `test_queryBinding_emptyBindingPath_reportsNotBound` / `test_queryBinding_compositePartPath_resolvesAsPart` / `test_queryBinding_xrTagPath_resolvesXrClass`(6 测) | COVERED |
| AC-3-D2 无素材/无时机 | `test_assetTypeRefsIl_realProduct_zeroErrors` / `test_assetTypeRefsIl_spriteBearingTestProduct_reportsRed` / `test_bindingResultFields_realType_zeroErrors` / `test_bindingResultFields_fadeDurationFixture_reportsRed` / `test_bindingResultFields_visibleFixture_reportsRed` / `test_bindingResultFieldsIl_fadeDurationFixture_reportsRealIlRed` / `test_bindingResultGates_missingOrEmptyFaces_reportRed` / `test_runAll_currentTree_d2FacesZeroErrors`(8 测) | COVERED |
| AC-3-D3 合成 release | `test_deviceRemoved_withoutGuard_engineSelfClearsPress_measured` / `test_deviceRemoved_guardAttached_allPressedReleasedSameFrame` / `test_deviceRemoved_compositeHalfPressed_released` / `test_deviceRemoved_nothingPressed_noOpNoThrow` / `test_deviceGuard_onDeviceChange_directDriven_semanticsPerChange` / `test_deviceRemoved_otherDeviceHeld_pressSurvives`(6 测) | COVERED |
| AC-3-D4 Mixed 迟滞 | `test_mixedGolden_mouseStill_plusStickDriftWithinTolerance_staysKbm` / `test_hysteresis_toleranceAboveThreshold_condition2EnforcedIndependently` / `test_hysteresis_aboveToleranceBelowThreshold_noSwitch` / `test_hysteresis_magExactlyEqualsTolerance_noSwitch_untilOnePast` / `test_hysteresis_durationExactlyEqualsDwell_noSwitch_untilOnePast` / `test_hysteresis_streakInterrupted_restartsFromZero` / `test_hysteresis_dwellDisabled_flappingProof` / `test_hysteresis_diagonalAboveThreshold_radialMustSwitch` / `test_hysteresis_twoColumns_neverShareARow` / `test_deviceStateManager_allConditionsMet_switchesSource` / `test_deviceStateManager_absentCandidate_neverSwitches`(11 测) | COVERED |
| 附注 IHaptics | `test_iHaptics_interfaceExists_channelAndIntensityOnly`(1 测) | COVERED(接口测;实现挂账 P1b) |

### Deviations(ADVISORY,零 BLOCKING)
1. **引擎重解析丢 isPressed 闩**(本窗新事实):`InputBindingResolver` 重建 `actionStates` + `RestoreActionStatesAfterReResolvingBindings` 回写清单**不含 `isPressed`** ⇒ 任意在跑动作的 `IsPressed()` 闩在重解绑瞬间丢失且不可自愈(phase / activeControl 保留)。**us-008-1 回归钉**改用三面判据(phase + activeControl + 守卫计数),不测 RemoveDevice 后 `IsPressed`。`DeviceReleaseGuard.cs` 文件头与 doc comment 双处写明引擎事实。`device_and_presentation_test.cs` 测试注释详记。
2. **引擎自清 flip**:原 engine-gap 登记「引擎不清闩 ⇒ 卡键」,**批跑 #5(`test_deviceRemoved_withoutGuard_engineSelfClearsPress_measured`)证伪** —— `InputManager.RemoveDevice` → `OnDeviceChange(Removed)` → `ResetActionState` 先于 listener 跑,引擎自清 press 闩(同帧)。`DeviceReleaseGuard.cs` 文件头注释就地修订。守卫保留理由 = GDD 义务载体 + 公开 ReleaseDeviceNow 恢复接口(不依赖引擎自清)。
3. **us-008-1 MAJOR → 已修**:`DeviceReleaseGuard.ReleaseDeviceNow` 原对全部 pressed 动作一律 `Reset`,跨设备类过释放。修 = 两级判据(① 资产绑重静态面 `ActionBoundTo` / ② 运行期 `activeControl.device == 断连设备`);`us-008-1 回归钉`钉住「键盘按住 + 闲置手柄断电」三面(计数 0 / phase Performed / activeControl 仍是键盘)。
4. **us-008-5 MINOR**:`DeviceSwitchHysteresis.cs:16` 溢出注释算错(「2^24 像素内平方 < 2^49」漏乘 Q16 缩放因子)。修 = 注释订正(代码本身整数域平方比较无误)。
5. **us-008-6 MINOR**:`DeviceReleaseGuard.BindingPathMatchesDevice` 的防御性括号分支(变体基名回落)经包源核实(`InputControlPath` 无 variant 解析),真资产走不到。保留不删 = 未来包引入变体时守住「能匹配」方向;删掉推向欠释放/卡键(欠释放比过释放严重)。
6. **us-008-7 MINOR + ⑤ 口径差**:`BindingQuery.cs:10` 注记「故事文本为准」—— registry (`TR-input-013` / `entities.yaml`) 签名 `QueryBinding(actionId)` vs 实现 `QueryBinding(BindingDeviceClass device, string bindingPath, InputActionAsset actions)`。**本故事实现与 GDD §Visual/Audio 三一致**,registry 待 GDD 轮同步(不改实现)。
7. **us-008-8 MINOR(Handover 登记)**:`DeviceStateManager` / `DeviceReleaseGuard` / `BindingQuery` / `IHaptics` 四件新 API 零生产调用点。与 Story 007 先例同构;`Observe*`/`Attach` 归 Story 010(热路径零成本) / Boot 装载流;IHaptics 实现挂账 P1b。**Completion Notes 点名,防永久悬空**。
8. **qa-008-8 MINOR(BLOCKED-BY-10)**:`IHaptics` 被 10 的调用点引用 Verify —— 10 故事当前 Ready 未实现;不借绿,TR-input-015 维持 `partial`。
9. **qa-008-11 ADVISORY**:Guard `Disconnected` 路径(`DeviceChange.Disconnected`)同 `Removed` 走同一 `ReleaseDeviceNow`(幂等),无独立 seam ⇒ **accepted no-seam** 登记(不可测)。
10. **XrActive 近似**:P0 未装 `com.unity.xr.openxr` ⇒ `XrActive = XRController 布局设备在联`(零 OpenXR 依赖可测近似);真 OpenXR session 事件接线归 P1b VR 轮(TR-input-015 partial)。
11. **None 免 DWELL**:`DeviceSwitchHysteresis.Observe` 中 `currentSource == None` ⇒ ①② 过即成源、免 DWELL(Read `§States「最近一次有效输入」` / Mixed 新立;`:106-109` 注释登记)。
12. **us-008-4 评审修复**:`.gitignore` 补兜底 `unity/Assets/production/` + `unity/Assets/production.meta`(防止 hook cwd 漂移再写进 Assets);hooks 四件 cwd 锚定(`SessionStartHook.cs` / `PostToolUseHook.cs` / `StopHook.cs` / `UserPromptSubmitHook.cs` 均由 `Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)` 改为 `Assembly.GetExecutingAssembly().Location`)。

### Spike / 环境依赖
- **P0 无 OpenXR 环境**(本机未装 `com.unity.xr.openxr`) ⇒ IHaptics 接口保留、实现挂账 P1b;TR-input-015 维持 `partial`。
- **XrActive 近似**同框。

### Traceability
- **TR-input-011**: `covered`(D3 合成 release + D4 Mixed 迟滞 + DeviceReleaseGuard + DeviceSwitchHysteresis)
- **TR-input-013**: `partial`(D1 QueryBinding 接口已实现;`entities.yaml` registry 签名 `actionId` vs 实现 `(device, bindingPath)` 口径差,待 GDD 轮同步;42/48 侧 `OQ-3-2` 未裁)
- **TR-input-015**: `partial`(IHaptics 接口已落;OpenXRInput spike 挂账 P1b)

### Manifest
- 故事 Manifest Version `2026-09-21` = 当前 manifest(`docs/architecture/control-manifest.md` 2026-09-21),无陈旧。

---

## Dependencies

- Depends on: Story 001(设备态读的是唯一动作资产上的绑重)
- Unlocks: Story 013(反幻想走查消费 `iconKey` 键名 + 设备态),Story 009(焦点导航与设备态解耦的对侧)
