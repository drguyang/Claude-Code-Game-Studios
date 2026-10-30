---
paths:
  - "tests/**"
---

# Test Standards

- Test naming: `test_[system]_[scenario]_[expected_result]` pattern
- Every test must have a clear arrange/act/assert structure
- Unit tests must not depend on **mutable** external state (writes to filesystem, network, database)
  - 🔁 **2026-10-30 措辞订正(`TD-clinic-16` 结案 · 用户裁定 A)**:原文写
    「must not depend on external state (filesystem, network, database)」——
    **字面把「读」也禁掉了**,而本项目**普遍读**作者态夹具
    (`assets/data/*.json` / `tests/unit/*/fixtures/`),规则与执行早已脱节。
    ⇒ 收窄为「**可变**的外部状态」:**只读**作者态夹具**是允许的**,条件见下条。
    ⚠️ **为什么不禁掉只读**(用户裁定的理由):把「读作者态 JSON」改成内联字面量会让夹具
    **失去「测的就是真表」这个价值** —— 表改坏了夹具仍绿。二者是真实张力,不是谁写错了。
  - **只读外部夹具的具名声明义务**:凡测试读仓库内文件(`assets/**` / `tests/**/fixtures/**`),
    须在该文件**头部注释具名声明**「读哪些路径、为什么必须读真表而非内联」。无声明 ⇒ 视为违反本条。
    ⚠️ **本条暂无机械门**(与写盘那半不同);登记为**待补门**(见 `TD-clinic-16` 的结案段)。
  - ⚠️ **写盘那半由机械门执行(S16-4 · 2026-10-20)**:`FsWriteBoundaryGate`
    (`unity/Assets/Editor.Tools.Gates/`,接进 `AssemblyGates.RunAll`)。扫 `Assets/Tests/**/*.cs`:
    **R1** 写盘目标直接是相对字面量 ⇒ 红;**R2** 文件含写盘调用且含仓库代码锚点
    (`Application.dataPath` / `Directory.GetCurrentDirectory` / `Application.streamingAssetsPath`)
    ⇒ 须在 `Declarations` 里**具名声明**(带理由),无声明 ⇒ 红。声明过期或悬空同样红。
  - 立门缘由:`retro-sprint-14-2026-10-18.md` §Action Items #4 —— 同类事故已三发
    (S14-6 `assets/audio/` · S15-5 `docs/audio/` 日期副本 · 更早的 `Directory.CreateDirectory`),
    而**规则与执行脱节三轮**。**测试写盘一律落 `Path.GetTempPath()` / `persistentDataPath`**;
    唯一例外是 `unity/Logs/`(gitignored 指定暂存区),且须具名声明。
- Integration tests must clean up after themselves
- Performance tests must specify acceptable thresholds and fail if exceeded
- Test data must be defined in the test or in dedicated fixtures, never shared mutable state
- Mock external dependencies — tests should be fast and deterministic
- Every bug fix must have a regression test that would have caught the original bug

## Examples

**Correct** (proper naming + Arrange/Act/Assert):

```gdscript
func test_health_system_take_damage_reduces_health() -> void:
    # Arrange
    var health := HealthComponent.new()
    health.max_health = 100
    health.current_health = 100

    # Act
    health.take_damage(25)

    # Assert
    assert_eq(health.current_health, 75)
```

**Incorrect**:

```gdscript
func test1() -> void:  # VIOLATION: no descriptive name
    var h := HealthComponent.new()
    h.take_damage(25)  # VIOLATION: no arrange step, no clear assert
    assert_true(h.current_health < 100)  # VIOLATION: imprecise assertion
```
