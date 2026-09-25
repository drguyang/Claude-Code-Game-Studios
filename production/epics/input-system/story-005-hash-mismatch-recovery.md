# Story 005: schema hash 失配优雅清空与跨版本备份

> **Epic**: 输入与设备
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-25

## Context

**GDD**: `design/gdd/input-system.md`(规则五 · Edge Cases 一 / 二)
**Requirement**: TR-input-004(资产重建致 GUID 失效 → schema hash 失配 → 优雅清空,**不静默采用错位 overrides**)—— 本故事承载其**失配后果半边**(Story 004 承稳定性半边)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主): 输入架构
**ADR Decision Summary**: ADR-011 §一 / Amendment A ③ —— schema hash 必须含 `bindingId`,失配 = 「资产被重建/跨版本升级」的可检测信号;规则五四步防御:`Disable()` → `RemoveAllBindingOverrides()` → 头部存 slot-signature hash → **失配则改名备份 + 载入默认 + 日志**,不静默、不崩溃、不弹窗(P0 无改键 UI,弹窗无处去)。失效是**响的**,不是消失的。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 夹具法依赖内存构造 `InputActionAsset` / 改 `bindingId`(Input System 运行期 API,ADR-011 整体 Knowledge Risk HIGH 的一部分);MEDIUM —— 主路径(读文件、改名、写日志)是稳定 BCL,仅夹具构造涉及未实测的引擎行为,且失败是「响的」。

**Control Manifest Rules (this layer)**:
- Required: 失配路径 = 改名备份 + 载入默认 + 日志(manifest Core · 输入(ADR-011);GDD 规则五 step 4)
- Forbidden: 失配时静默沿用旧 overrides / 抛未捕获异常终止启动 / 弹模态窗(GDD 规则五明禁三态)
- Guardrail: 备份文件名须可诊断(含时间戳或版本后缀,不覆盖前次备份 —— GDD Edge Cases 二)

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [ ] **AC-3-A3(BLOCKING)**: hash 失配 ⇒ **改名备份 + 载入默认 + 日志**;**不静默、不崩溃、不弹窗**;须覆盖「重建后失配」场景。夹具法 = 独立测试资产副本(内存构造 `InputActionAsset`,改 `bindingId` 或前缀替换合成 id)⇒ 断言:**备份已生成** ∧ **生效绑定 = 默认** ∧ **未抛异常**
- [ ] **AC-3-A8(BLOCKING)**: **跨版本升级失配即备份** —— 资产重建(`bindingId` 全变)⇒ hash 变 ⇒ 陈旧文件**改名备份** + **日志可诊断** + **载入默认**;overrides 必然失效,本 AC 保「**失效是响的**」(备份 + 日志在,玩家/开发者能定位,而非静默清空无痕)
- [ ] **AC-3-E3③(BLOCKING)**: **`bindingId` 变化 ⇒ hash 必变** —— 构造两条 R,唯一差异 = `bindingId`,断言 hash **不同**(这是失配检测的**使能性质**:若 hash 对 `bindingId` 盲,重建永远不会被检测到,A3/A8 无从触发)

---

## Implementation Notes

*Derived from ADR-011 §一 / Amendment A ③ + GDD 规则五 / Edge Cases 一·二:*

- **四步防御**(规则五,顺序不可换):① `asset.Disable()` ② `asset.RemoveAllBindingOverrides()`(Disable 不清 override —— 单独① = 旧 override 静默叠加)③ 头部存 slot-signature schema hash ④ **失配** ⇒ 改名备份 + 载入默认 + 日志
- **三态禁令**:**不静默**(静默清空 = 玩家丢改键无痕)· **不崩溃**(失配是正常演进,非异常)· **不弹窗**(P0 无改键 UI,模态无处去 —— 且违 ADR-013 模态闭集纪律)
- **两条失败模式分开测**:① **改 hash 值**(合成失配)→ A3 断言;② **`bindingId` 全变**(真实重建)→ A8 断言。E3③ 证明 ② 必然触发 ① 的检测路径
- **备份命名**:改名而非删除(GDD Edge Cases 二:连续多次失配不得互相覆盖 ⇒ 备份名带时间戳/序号)
- **日志可诊断**:日志行须含旧 hash / 新 hash / 备份文件路径三要素(A8「可诊断」的最低口径)
- 本故事只做**失配分支**;「匹配则喂字节」的正向半边在 Story 003

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:两文件布局、正向装载(匹配分支)、字节往返(E3⑥)
- Story 004:hash 的计算与稳定性(①②④⑤⑦)—— 本故事只**消费** hash 比对结果
- 改键 UI(`OQ-3-1`)与「失配后引导玩家重新改键」的交互 —— P0 无 UI,载入默认即终点

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-3-A3**: 合成失配 ⇒ 备份已生成 + 生效绑定 = 默认 + 未抛异常。
  - Given: 合法 sidecar(overrides + 头部);独立测试资产副本(内存构造,改 `bindingId` 或前缀替换合成 id)使 hash 与头部不一致。
  - When: 走规则五四步防御装载。
  - Then: ① 旧文件**已改名备份**(存在,非删除);② 生效绑定全部回默认(override 未被采用);③ 无异常抛出;④ 有日志行。
  - Edge cases: 连续两次失配 ⇒ 两份备份并存(不覆盖);备份目录不可写 ⇒ 记日志跳过备份但**仍**载入默认、仍不崩(GDD Edge Cases 二)。
  - Negative fixture: 把失配分支改成「直接抛异常」⇒ ③ 红;改成「静默删文件」⇒ ① 红。
- **AC-3-A8**: 跨版本升级(`bindingId` 全变)⇒ 备份 + 可诊断日志 + 载入默认。
  - Given: 旧版资产 + 其 overrides 文件(有效);新版资产重建后 `bindingId` 全部不同。
  - When: 新版启动装载。
  - Then: 头部 hash 与文件失配 ⇒ 备份生成;日志含旧 hash / 新 hash / 备份路径;生效绑定 = 新版默认;**失效是响的**(三要素齐,非无痕清空)。
  - Edge cases: 旧文件根本不存在(首次启动)⇒ 走正常首载,不触发备份分支;文件损坏(hash 读不出)⇒ 同样走失配分支(视为失配)。
  - Negative fixture: 实现只清空不改名备份 ⇒ ①红;日志不含 hash ⇒ ④「可诊断」红。
- **AC-3-E3③**: `bindingId` 变化 ⇒ hash 必变。
  - Given: 两条 R,唯一差异 = `bindingId`。
  - When: 分别计算 `SchemaHash`。
  - Then: 两 hash **不同**。
  - Edge cases: 只改一个 binding 的 id(非全变)⇒ 亦不同;`bindingId` 相同但顺序变 ⇒ hash 相同(排序吸收)。
  - Negative fixture: 实现把 `bindingId` 排除出 R(回到 Amendment A ③ 之前的盲态)⇒ 本测红(负例自证)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/input_system/hash_mismatch_recovery_test.cs` — must exist and pass

**Status**: [ ] Not yet created
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 真身落 `unity/Assets/Tests/EditMode/InputSystem/hash_mismatch_recovery_test.cs`,文档路径 `tests/integration/input_system/` 为登记口径

---

## Dependencies

- Depends on: Story 004(hash 与其两条不变量就位,失配才可被检测)
- Unlocks: None(本故事是 overrides 持久化线的收口)
