# Story 001: 动作资产 · 单实例纪律与 Legacy 零引用门

> **Epic**: 输入与设备
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-25

## Context

**GDD**: `design/gdd/input-system.md`
**Requirement**: TR-input-001(全案恰有一个动作资产;InputSystemUIInputModule 与 3 的输入服务引用同一对象)· TR-input-002(三套绑重同表共存;OpenXR 绑通用 XRController 布局)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
⚠️ `AC-3-A4`(Legacy 零引用)无专属 TR —— 直引 GDD `AC-3-A4` 与规则一 / 规则三。

**ADR Governing Implementation**: ADR-011(主): 输入架构 · ADR-012(载体注记,非治理件)
**ADR Decision Summary**: ADR-011 §一 —— action-based 官方路线:**一套动作资产**统一组织 K&M + Gamepad + OpenXR 三套绑重;Legacy Input Manager 禁用,输入层零旧输入依赖。§Risks-A S2 —— `Instantiate()` vs `Clone()` 引擎参考库零覆盖须 spike,**判据层只断「实例同一性」性质**,不依赖符号形状。载体注记:GDD §Dependencies 五「CI 载体」把 `AC-3-A4②` 列为「判据已定、载体未建」八条之一,**与 ADR-012 CI 门同批落地** —— 本故事交付分析器本体与其 EditMode 测试,CI job 挂账另轮。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: ADR-011 Knowledge Risk HIGH(Input System 6.3 行为 post-cutoff 须实测)。分层:`GetInstanceID()` = cut-off 前长期稳定 API,**不入** spike 册页;`InputSystemUIInputModule` = post-cutoff 零覆盖符号 ⇒ A1 的 UI 半条在 P0 **条件不适用**(UI Toolkit 为主,ADR-013 §Decision ①),P0 只签前半条;`PlayerSettings.GetPropertyInt("activeInputHandler")` 失败是「响的」(断言期暴露),不升 spike 项。

**Control Manifest Rules (this layer)**:
- Required: action-based 官方路线 —— Input System 动作资产,K&M + Gamepad + OpenXR 三套绑重 + `bindings overrides` 持久化(manifest Core · 输入(ADR-011))
- Forbidden: Legacy Input Manager / `Input.GetKey` 族 —— 新输入系统是 6.3 默认,输入层零旧输入依赖(ADR-011 §一 / manifest Forbidden 表)
- Guardrail: 资产实例唯一性与 Legacy 零引用均以**构建/断言失败**守住(白名单断言 + Roslyn),不靠约定;`Instantiate()` vs `Clone()` 归 ADR-011 §Risks-A S2 spike,判据层只断性质

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story(判据正文照录,评审沿革注见 GDD 原文):*

- [ ] **AC-3-A1(BLOCKING)**: 3 的输入服务内,**同一动作资产恰有一个实例** —— 断言**实例同一性**(比较 `GetInstanceID()`,**非**比较文件路径或文件计数);**UI 侧半条(条件适用)**:当下游存在 UGUI 画布并挂 `InputSystemUIInputModule`(P1a world-space / VR)时,该模块引用的资产必须与 3 的为同一实例。**P0 平面不适用**(UI Toolkit 为主 ⇒ 该模块可能根本不存在,本条不得以它为主语)—— **P0 签核 = 前半条**;P0 焦点载体前置门(①焦点载体是谁 ②由谁断言接通 ③spike 降级路径)归 42,登记于 GDD §Dependencies 五
- [ ] **AC-3-A4①(BLOCKING)**: `PlayerSettings.GetPropertyInt("activeInputHandler") == 1`(1 = Input System Package · 0 = Old · 2 = Both)—— **EditMode 断言且须位于 Editor-only 程序集**,**非构建产物 grep**;**不引用** `PlayerSettings.activeInputHandler` 或枚举成员 `InputSystemPackage`(存在性未经仓内核验的符号不进 BLOCKING 判据)
- [ ] **AC-3-A4②(BLOCKING)**: **Roslyn 分析器**在编译期拒绝任何 `UnityEngine.Input` 符号引用(**编译失败**,不是事后 grep —— 构建产物 grep 在符号被裁剪/内联后可能假阴性)

---

## Implementation Notes

*Derived from ADR-011 §Decision 一 / §Implementation Guidelines 1·6 / §Risks-A:*

- **先写动作资产 + 绑重持久化**:动作映射契约是输入层的锚点(Guideline 1)。全案**恰一个** `.inputactions`,三套绑重(K&M / Gamepad / OpenXR)同表共存(规则三);OpenXR 绑**通用 `XRController` 布局**,勿绑 OculusTouch/Index 专属(Guideline 8)
- **单实例纪律**:输入服务持唯一实例,断言用 `GetInstanceID()` 同一性;「文件计数 = 1」型判据**放过**「同文件被实例化两次」的分叉,已明文废弃(AC-A1 复审重写)
- **P1b 同机多玩家** = 每玩家 `Instantiate()`(非 `Clone()`),克隆不继承 overrides、**不得启用 UI map**(ADR-011 §一 / Amendment A ②)—— **P0 不实现**,本故事只保证资产结构不阻断该形状(`TR-input-005` 挂账,见 Out of Scope)
- **Legacy 零引用双门**:① Editor-only 程序集内的 `GetPropertyInt` 整数编码断言;② Roslyn 分析器拒 `UnityEngine.Input` 符号(编译期)
- **载体注记**:分析器的 CI job 形态(命名 job / 脚本)与 `.github/workflows/` 同批归 ADR-012 轮 —— 本故事交付分析器 + 可跑的 EditMode 测试,不自建 CI

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:F-3.1 轴处理与装载期断言(A9)—— 不同公式,不同落点
- Story 003:overrides sidecar 持久化(A2/A5/E3⑥)—— 资产结构 ≠ 绑重读写
- Story 005:schema hash 失配优雅清空(A3/A8)—— 本故事不碰 overrides 装载流程
- Story 007:急救直读通道与输入更新相位(B1a/B2)—— 通道另立
- P1b 同机克隆(`TR-input-005`)与 `PlayerInput` 取舍(`OQ-3-3`):P0 不实现,形状不阻断即可

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-3-A1**: 3 的输入服务内同一动作资产恰有一个实例(`GetInstanceID()` 同一性)。
  - Given: 输入服务初始化完成(含任意多次场景重载 / 服务重启)。
  - When: 取输入服务持有的动作资产实例,与再次解析出的资产实例比对。
  - Then: `GetInstanceID()` 相等;同一性断言对「同文件加载两次」的分叉**失败**(反例夹具:故意二次实例化同一 `.inputactions` ⇒ 测试红)。
  - Edge cases: 场景切换后实例不被重建为第二份;文件路径相同但实例不同 ⇒ 必须失败(证明判据不是路径比较)。
  - Negative fixture: 二次实例化同一资产(AC 明文点名的失效形态)。
- **AC-3-A4①**: `PlayerSettings.GetPropertyInt("activeInputHandler") == 1`,EditMode 断言、位于 Editor-only 程序集。
  - Given: 工程 Player Settings 的 Active Input Handling = Input System Package。
  - When: EditMode 测试调用 `GetPropertyInt("activeInputHandler")`。
  - Then: 返回 1;断言所在程序集为 Editor-only(asmdef `includePlatforms: ["Editor"]`);源码中不出现 `activeInputHandler` 属性访问与 `InputSystemPackage` 枚举成员名(grep 级辅助断言)。
  - Edge cases: 若值为 0/2 ⇒ 测试红(覆盖「Both」配置回退)。
  - Negative fixture: 无(配置值由工程设定;失败模式 = 值 ≠ 1)。
- **AC-3-A4②**: Roslyn 分析器编译期拒绝任何 `UnityEngine.Input` 符号引用。
  - Given: 测试用 C# 源串含 `UnityEngine.Input.GetKey(KeyCode.Space)`(及 `Input.GetAxis` 等变体)。
  - When: 以分析器跑该源的编译。
  - Then: 编译**失败**且诊断指向该引用;合法的 Input System 写法(`InputAction.ReadValue`)编译通过。
  - Edge cases: 全限定名 `UnityEngine.Input` 与 `using UnityEngine; Input.` 两种写法均拒;别名引用须覆盖(同义词表显式登记,防换名绕过)。
  - Negative fixture: 一条只 `using UnityEngine` 但不触 `Input` 的源 ⇒ 不得误报。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/input_system/action_asset_identity_test.cs` — must exist and pass(含 Editor-only 的 A4① 断言与分析器测试)

**Status**: [ ] Not yet created
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 真身落 `unity/Assets/Tests/EditMode/InputSystem/`(含分析器测试),文档路径 `tests/integration/input_system/` 为登记口径

---

## Dependencies

- Depends on: None(资产是输入层第一块砖)
- Unlocks: Story 002 / 003 / 006 / 007 / 008 / 009 / 010 / 012(全部消费本故事立住的单实例资产)
