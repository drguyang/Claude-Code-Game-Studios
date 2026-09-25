# Story 003: 绑重 overrides sidecar 持久化

> **Epic**: 输入与设备
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-25

## Context

**GDD**: `design/gdd/input-system.md`(规则四 / 规则五 · Edge Cases 二)
**Requirement**: TR-input-003(绑重 overrides 持久化 = 输入层唯一落盘职责,不进存档不进三流)· TR-input-014(P0 不交付改键 UI,只交付契约 + overrides 读写 + 默认三套绑重)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主): 输入架构 · ADR-010(次): 7a 持久化与存档格式
**ADR Decision Summary**: ADR-011 §一 —— overrides 持久化 = 输入层**唯一存档职责**(ADR-010 之外);重载前须 `asset.Disable()` **且** `asset.RemoveAllBindingOverrides()`(`Disable()` 不移除已载入 override —— override 是叠加的,Amendment A ②);文件 = 出厂 `SaveBindingOverridesAsJson()` 输出**逐字节**保存 + 头部 `bindings.schema.txt`(schema hash · format version · asset id/version,规则四)。ADR-010 —— 存档与三流**刻意不含**绑重 overrides(`TR-input-003` 消费)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: `SaveBindingOverridesAsJson` / `LoadBindingOverridesFromJson` 是**引擎参考库已覆盖**的两个符号(`docs/engine-reference/unity/modules/input.md:269-274`)—— 本故事不触未核验符号;MEDIUM 承 ADR-011 整体评级。

**Control Manifest Rules (this layer)**:
- Required: overrides 持久化按 ADR-011 §一 流程 —— 重载前 `Disable()` + `RemoveAllBindingOverrides()`;schema hash 存头部(manifest Core · 输入(ADR-011))
- Forbidden: `PlayerPrefs` / `EditorPrefs`(ADR-013 / ADR-014 明禁,AC-A5 拒绝清单点名)· 任何 JSON 解析改写 overrides 载荷(3 不解析、不重写 —— 规则四)
- Guardrail: 落盘 API 调用点白名单断言 —— 仅 `bindings.overrides.json` 与 `bindings.schema.txt` 两处,其余全部拒绝(AC-A5 判据)

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [x] **AC-3-A2**: 绑重 overrides **往返一致** —— `SaveBindingOverridesAsJson()` → `LoadBindingOverridesFromJson()` → **逐键一致**(EditMode 测试,**含复合绑重**)
- [x] **AC-3-A5**: **3 的落盘面仅限 overrides sidecar** —— 代码路径中写盘调用**仅** `bindings.overrides.json`(出厂 `Save…` 输出逐字节保存)与其头部 `bindings.schema.txt` **两处**;**零三流写入、零 ADR-010 存档写入**;**拒绝清单须含 `PlayerPrefs` / `EditorPrefs`**。判据 = 白名单断言:落盘 API 调用点 ∈ {上述两文件},其余全部拒绝
- [x] **AC-3-E3⑥**: **sidecar 字节往返** —— `Save…` 写出的 overrides 文件**逐字节**喂 `Load…`(3 不解析、不重写);断言「喂入字节 == 保存字节」

---

## Implementation Notes

*Derived from ADR-011 §Decision 一 / §Implementation Guidelines 5 + GDD 规则四 / 规则五:*

- **两个文件,职责分离**:`bindings.overrides.json` = 出厂 `SaveBindingOverridesAsJson()` 的**逐字节**输出(`LoadBindingOverridesFromJson()` 读它**原样**;**3 永不解析/重写其载荷**);`bindings.schema.txt` = 头部,存 schema hash · format version · asset id/version
- **读取顺序**(规则四):读头部 → 比对 hash → 匹配:把字节喂 `Load…`;失配:走规则五 step 4(归 Story 005)
- **重载前两步**(ADR-011 Amendment A ②):`asset.Disable()` → `asset.RemoveAllBindingOverrides()` —— 单独 `Disable()` **不**清 override(叠加语义),漏第二步 = 旧 override 静默残留
- **落盘白名单机械判据**(AC-A5):以反射/静态扫描断言全部写盘调用点 ∈ {两文件};`PlayerPrefs` / `EditorPrefs` 出现 = 拒绝
- **与 ADR-010 的边界**:overrides **不进**三流、**不进**存档头/字节面 —— 它是输入层唯一落盘职责(ADR-010 §Neutral 明载)
- **P0 无改键 UI**(TR-input-014 / `OQ-3-1`):本故事只交付 `BindingsStore.Save/Load` 契约与默认三套绑重的读写,**不做界面** —— 补界面不返工

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004:schema hash 的计算与稳定性(F-3.5 / E3①②④⑤⑦)—— 本故事写头部,不算 hash 语义
- Story 005:hash 失配的优雅清空与备份(A3/A8)—— 本故事只实现「匹配则喂字节」的正向半边
- 改键 UI(`OQ-3-1`,归 49 / 42)—— P0 不交付
- 存档系统的 checkpoint / codec(ADR-010 面)—— overrides 刻意在其之外

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-3-A2**: Save → Load 往返逐键一致,含复合绑重。
  - Given: 动作资产含普通绑重与复合绑重(composite)各若干;构造一组 override(改 `path` / 增删 override)。
  - When: `SaveBindingOverridesAsJson()` 产出 JSON → `LoadBindingOverridesFromJson()` 载回干净资产。
  - Then: 每条绑定的 override 逐键一致(含复合的 parts);无 override 的绑定不受污染。
  - Edge cases: 空 override 集(首次启动);仅复合的一部分被改;同一动作两物理输入(`Interact` 绑 `E` + 手柄 `South`,规则三合法)。
  - Negative fixture: 损坏 JSON 半截 ⇒ 走失配路径不静默(交叉 Story 005)。
- **AC-3-A5**: 落盘面白名单断言 —— 仅两文件,拒绝 PlayerPrefs/EditorPrefs,零三流/零存档写入。
  - Given: 3 的输入程序集源码。
  - When: 静态扫描全部写盘 API 调用点(`File.*` / `StreamWriter` / `PlayerPrefs` / `EditorPrefs` / 存档接口)。
  - Then: 写盘调用点 ∈ {`bindings.overrides.json`, `bindings.schema.txt`};出现 `PlayerPrefs` / `EditorPrefs` / `IEventSink` / ADR-010 `Checkpoint` ⇒ 失败。
  - Edge cases: 路径经常量/拼接间接引用(同义词表登记,防换名绕过);写失败路径(只读目录)只记日志不换写点(GDD Edge Cases 二)。
  - Negative fixture: 夹具源码含一行 `PlayerPrefs.SetString` ⇒ 扫描红。
- **AC-3-E3⑥**: sidecar 字节往返 —— 喂入字节 == 保存字节。
  - Given: 一组 override 经 `Save…` 写出。
  - When: 读回文件原始字节,直接喂 `LoadBindingOverridesFromJson`。
  - Then: 生效绑定与保存前一致;**测试断言「文件字节 == Save 返回字节」**(证明中间无重排/重写);3 的代码路径中无任何对载荷的解析调用(扫描辅助)。
  - Edge cases: 非 ASCII 绑定路径;文件末尾无换行的字节保真;连续两次 Save 幂等。
  - Negative fixture: 有人在中间插入「美化/重排 JSON」的重写步骤 ⇒ 字节断言红。

**实现期登记(2026-09-25 实现批;不改 spec,只把实际用例面与原文的出入落账)**:
- A2 负例「损坏 JSON 半截」忠实拆两半:`..._neg_corruptPayload_returnsCorruptPayloadNotSilent`(载荷截断、头部完好 ⇒ `CorruptPayload` + 错误日志,不静默)+ `..._halfSidecar_treatedAsMismatch`(文件级半截:头部/载荷只存其一 ⇒ `Mismatch` + 警告)—— 对应 Edge Cases 二「损坏 / 半截 视同失配」两个分支。
- A2 读序负半边:`..._hashMismatch_doesNotFeedPayload_fileUntouched`(Save hashA → Load hashB ⇒ `Mismatch` + 警告 + 资产回默认 + 两文件字节不动)—— QA 只点名正向「匹配则喂」;失配不喂是 Implementation Notes 读序(比对失败走 step 4)的直接蕴含,恢复侧(改名备份)仍归 Story 005 不越界。
- A5 Then 第一支负例:`test_writeSurface_scan_nonWhitelistedWriteTarget_flagsRed`(写点指向白名单外文件 ⇒ 红)—— QA Negative 行只点名 PlayerPrefs 夹具;Then 主句「写盘调用点 ∈ {两文件}」的失败面同属 AC 判据。
- A2 Edge 手柄输入:QA 文本写 `South`,实际资产 Interact 第二绑是 `<Gamepad>/buttonNorth` —— 按**资产真源**执行(断言 buttonNorth 不受污染)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/input_system/overrides_sidecar_test.cs` — must exist and pass

**Status**: [x] Created + VERIFIED(2026-09-25 超算 batch)
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 真身落 `unity/Assets/Tests/EditMode/InputSystem/overrides_sidecar_test.cs`,文档路径 `tests/integration/input_system/` 为登记口径(README 落点说明同批)
- 执行 ✅ **VERIFIED 2026-09-25 超算 batch**:全套 EditMode **527/527 全绿 exit 0**(log `unity/Logs/build-story003-2.log`;基线 510 + 本故事 17);本故事 **17/17 Passed**(A2 6 + A5 5 + E3⑥ 6)

---

## Dependencies

- Depends on: Story 001(唯一动作资产是 overrides 的宿主)
- Unlocks: Story 004(hash 读写点),Story 005(失配清空读本故事的两文件布局)
