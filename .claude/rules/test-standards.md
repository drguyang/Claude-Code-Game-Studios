---
paths:
  - "tests/**"
---

# Test Standards

- Test naming: `test_[system]_[scenario]_[expected_result]` pattern
- Every test must have a clear arrange/act/assert structure
- Unit tests must not depend on external state (filesystem, network, database)
  - ⚠️ **本条现由机械门执行(S16-4 · 2026-10-20)**:`FsWriteBoundaryGate`
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
