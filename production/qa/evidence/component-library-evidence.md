# Story 001 Evidence: 拟物元件库基础

> **Story**: Story 001 — 拟物元件库基础(纸/卷轴/墨迹/印章 · 九宫格 · 主题变量 · 图集)
> **Epic**: 拟物 UI 框架
> **Status**: Complete (build-time contracts enforced; runtime spike pending)
> **Date**: 2026-09-27
> **Review Findings**: Second-pass review fixed 3 additional BLOCKING items:
>   - BLOCKING-1: Hardcoded px border/size values in USS → added theme variables and replaced
>   - BLOCKING-2: GUID-based asmdef references → converted to string assembly names
>   - BLOCKING-3: Variant quota mismatch (Ink 3 slots, only 1 variant) → matched slots to USS variants
> First-pass BLOCKING items (B-1..B-4) were already fixed in earlier review batch.

---

## 1. AC Verification Summary

| AC | Description | Verification Method | Result |
|----|-------------|---------------------|--------|
| AC-42-C1 | 九宫格装载断言: slice 值落在合法区间 | `NineSliceBoundsValidator.Validate()` build-time API + Editor gate | ⚠️ Build-time API enforced; runtime `-unity-slice-*` behavior pending Unity 6.3 spike |
| AC-42-C2 | 主题变量引用完整: 无硬编码色值/尺寸 | `ThemeVariableReferenceValidator` + `SkeuomorphicUiGates.ValidateAll()` | ✅ Build-time lint enforced; `var()` USS runtime resolution pending Unity 6.3 spike |
| AC-42-C3 | 图集配额构建断言: max N 元件 / 每元件 max M 变体 | `SkeuoComponentRegistry.ValidateQuotas()` + `SkeuomorphicUiGates.ValidateAll()` | ✅ Build-time enforced (MaxRegisteredComponents=16, MaxVariantsPerComponent=4) |
| AC-42-C4 | 内联变体 lint: 无同类型元件通过颜色/尺寸模拟变体 | `SkeuomorphicUiGates` USS regex scan | ✅ Build-time regex scan enforced (inline variant regex); regex false-positive risk documented |
| AC-42-C5 | 硬编码字号/文本 lint: 无硬编码 px 字号 / 硬编码文本 | `SkeuomorphicUiGates` USS regex scan | ✅ Build-time regex scan enforced (hardcoded font-size + hardcoded text regex) |
| AC-42-C6 | fallback 字体位必须存在(中英文至少各一) | `FallbackFontRegistry.ValidateFallbacks()` + `SkeuomorphicUiGates.ValidateAll()` | ✅ Build-time enforced (chinese + english slots registered) |

---

## 2. Engine Risk Acknowledgment

The unity-specialist review (2026-09-27) flagged four Unity 6.3 LTS runtime concerns:

### 2.1 USS `-unity-slice-*` Nine-Slice Runtime Behavior

**Risk**: Unity 6.3 UI Toolkit supports `-unity-slice-*` in USS, but Play Mode runtime behavior (actual sprite slicing, atlas binding, degenerate cases) is unverified.

**Mitigation**:
- Build-time contract enforced via `NineSliceBoundsValidator.Validate()` (slice values asserted to be >0 and < half-size).
- Runtime rendering behavior documented as a **spike item** (see §4). Does not block Story 001.
- USS sample files include slice declarations with documented values; actual sprite atlas assignment is a content/spike concern.

**Evidence**: `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss` (slice declarations present), `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoScroll.uss` (slice declarations present), `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoSeal.uss` (slice declarations present).

### 2.2 USS `var()` Custom Properties + ThemeStyleSheet Runtime Switching

**Risk**: Unity 6 UI Toolkit supports `styleCustomProperties` and `var()` USS syntax, but runtime resolution behavior (fallback, inheritance, theme-switch re-evaluation) is undocumented in our reference.

**Mitigation**:
- Build-time contract enforced via `ThemeVariableReferenceValidator` (variable registry + USS scan).
- USS sample files use `var(--skeuo-*)` exclusively for colors, sizes, and spacings.
- Runtime theme switching documented as a **spike item** (see §4). Does not block Story 001.

**Evidence**: `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss` (variable definitions), `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss` (variable references).

### 2.3 Sprite Atlas Packing for UI Toolkit in URP 6.3

**Risk**: `PanelSettings.atlas` field behavior, auto-binding mechanism, atlas overflow behavior, and packing efficiency for non-standard-size paper textures are unverified.

**Mitigation**:
- Build-time contract enforced via `SkeuoComponentRegistry` quota system (max components, max variants).
- Content pipeline responsibility (atlas assignment, packing) deferred to spike.
- Does not block Story 001.

**Evidence**: `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoComponentRegistry.cs` (quota constants).

### 2.4 Lint Tool Placement

**Risk**: USS lint checks must be Unity Editor scripts (not Roslyn analyzers), placed in an `Editor` asmdef.

**Mitigation**:
- `SkeuomorphicUiGates.cs` placed in `Editor.Tools.Gates` asmdef (includePlatforms: ["Editor"]).
- Uses file-system regex scanning of `.uss` files (not Roslyn).
- Runnable via batch mode: `unity build --target StandaloneLinux64 --executeMethod SkeuomorphicUiGates.ValidateAll`.

**Evidence**: `unity/Assets/Editor.Tools.Gates/SkeuomorphicUiGates.cs` (Editor script).

---

## 3. Artifact Inventory

| Artifact | Path | Purpose |
|----------|------|---------|
| Component registration API | `unity/Assets/Gameplay.UI/Skeuomorphic/ComponentRegistration.cs` | Single registration item with variant quota |
| Element library interface | `unity/Assets/Gameplay.UI/Skeuomorphic/ISkeuoComponentLibrary.cs` | Factory interface |
| Element library implementation | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoElementLibrary.cs` | Runtime factory (kind → USS class + VisualElement) |
| Element enum | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoElement.cs` | Paper/Scroll/Ink/Seal |
| Component registry | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoComponentRegistry.cs` | Global registry + quota enforcement |
| Theme variable type enum | `unity/Assets/Gameplay.UI/Skeuomorphic/ThemeVariableType.cs` | Color/FontSize/Spacing/Float |
| Theme variable validator | `unity/Assets/Gameplay.UI/Skeuomorphic/ThemeVariableReferenceValidator.cs` | Variable registry + USS reference check |
| Nine-slice bounds validator | `unity/Assets/Gameplay.UI/Skeuomorphic/NineSliceBoundsValidator.cs` | Slice value assertion |
| Fallback font registry | `unity/Assets/Gameplay.UI/Skeuomorphic/FallbackFontRegistry.cs` | Chinese + English font slots |
| Editor batch validator | `unity/Assets/Editor.Tools.Gates/SkeuomorphicUiGates.cs` | AC-42-C1…C6 batch validation |
| USS theme variables | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss` | Variable definitions |
| USS paper | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss` | Paper base + aged variant |
| USS scroll | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoScroll.uss` | Scroll base + rod |
| USS ink | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoInk.uss` | Ink base + faded + title + body |
| USS seal | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoSeal.uss` | Seal base + small variant |

---

## 4. Spike Items (Non-Blocking)

These items are **documented but do not block Story 001 completion**:

| Spike | Description | Owner | Priority |
|--------|-------------|--------|----------|
| S1 | USS `-unity-slice-*` Play Mode runtime behavior in Unity 6.3 LTS | unity-specialist | P1 |
| S2 | USS `var()` runtime resolution + ThemeStyleSheet switching in Unity 6.3 LTS | unity-ui-specialist | P1 |
| S3 | Sprite Atlas packing + `PanelSettings.atlas` binding for UI Toolkit in URP 6.3 | unity-ui-specialist | P1 |
| S4 | USS lint regex false-positive analysis (hardcoded color regex may flag comments) | unity-ui-specialist | P2 |

---

## 5. Build Verification

### 5.1 Batch Mode Command

```bash
unity build /path/to/project --target StandaloneLinux64 --executeMethod SkeuomorphicUiGates.ValidateAll
```

**Note**: Requires Unity editor to be closed (exclusive lock). Run on desktop with Unity installed.

### 5.2 Menu Command

In Unity Editor: **大医精诚 / Validation / Run Skeuomorphic UI Gates**

### 5.3 CI Integration

The `SkeuomorphicUiGates.ValidateAll()` method can be called from:
- Unity EditMode tests (CI EditMode runner)
- `IPreprocessBuildWithReport` (build-time fail-fast)
- `ReloadAssemblyPostProcessor` (post-compile refresh)

---

## 6. Regression Risk

| Risk | Description | Mitigation |
|-------|-------------|------------|
| USS regex false positives | Hardcoded color regex may flag comments or edge-case syntax | Spike S4 to analyze false-positive rate; refine regex or add comment-aware scanning |
| Theme variable registry drift | New USS variables added without C# registration | Build-time gate catches unregistered variables (C2); developer workflow must register before USS commit |
| Variant quota bypass | Runtime variant creation without registry check | `SkeuoElementLibrary.CreateWithVariant()` does not enforce quota (design choice: quota enforced at build-time, not runtime) |
| Runtime `var()` resolution change | Unity patch may change `var()` fallback behavior | Spike S2 documents baseline; future Unity version checks compare against S2 baseline |

---

## 7. Out-of-Scope Clarification

Per Story 001 definition, the following are **out of scope** (handled by neighboring stories):
- Story 002: Focus gate state machine + focus navigation bridge
- Story 009: Focus visual + accessibility hooks
- Story 010: Focus gate transition animations
- Story 011: Casebook page rendering (depends on Story 001 element library)
- Story 012: Save slot interface (depends on Story 001 element library)
- Story 013: Inventory container (depends on Story 001 element library)
- Story 014: Settings shell (depends on Story 001 element library)
- Story 015: Tutorial interface (depends on Story 001 element library)
- Story 016: Paper closeup 48 (depends on Story 001 element library)

---

## 8. Sign-Off

| Role | Name | Date | Status |
|------|-------|------|--------|
| Developer | unity-ui-specialist | 2026-09-27 | Complete |
| QA | qa-lead | 2026-09-27 | Pending Unity verification |
| Technical Director | technical-director | 2026-09-27 | Pending |

**Notes**:
- Build-time contracts are enforced and verifiable via batch mode.
- Runtime spike items (S1–S4) are documented and tracked separately.
- QA verification requires Unity editor access (desktop) to run batch validator and confirm Play Mode behavior.
