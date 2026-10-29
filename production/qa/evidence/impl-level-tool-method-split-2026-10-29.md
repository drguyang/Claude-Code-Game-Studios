# 实现证据 —— `Editor.Tools.Level` 全树方法拆分轮(纯重构 · 批 2)

> **对象**:`unity/Assets/Editor.Tools.Level/` 下 **9 个文件、19 个方法**
> **日期**:2026-10-29 · **类型**:纯重构(无可观测行为变更)
> **前一批**:`impl-level-tool-check-split-2026-10-29.md`(`LevelConsistencyCheck.Check` 163 → 11 方法)
> **由来**:用户指令「那 18 个方法也一并拆了」;基准 = story-006 评审 `M-5` 的 ≤40 行硬界

---

## §一 拆分结果(**实测**)

复算命令:

```bash
python3 - <<'PY'
import re,glob,os
for p in sorted(glob.glob("unity/Assets/Editor.Tools.Level/*.cs")):
    lines=open(p,encoding="utf-8").read().split("\n")
    for a,l in enumerate(lines):
        m=re.match(r'^        (public|private|internal|protected)(\s+static)?\s+[\w\.<>\[\],\?]+(\s+[\w\.<>\[\],\?]+)*\s+(\w+)\s*\(', l)
        if not m: continue
        d=0;o=False;e=None
        for j in range(a,len(lines)):
            d+=lines[j].count("{")-lines[j].count("}")
            if "{" in lines[j]: o=True
            if o and d==0: e=j;break
        if e and e-a+1>40: print(os.path.basename(p), a+1, m.group(4), e-a+1)
PY
```

| 文件 | 方法 | 拆前 | 拆后 |
|---|---|---:|---:|
| `EcozoneSource.cs` | `EcozonePolygonCheck.TryCheck` | 47 | **14** |
| `EcozoneSource.cs` | `CheckOneZone` | 55 | **9** |
| `LayerDriftCheck.cs` | `TryCheck` | 56 | **16** |
| `LevelCiRunner.cs` | `Render` | 43 | **20** |
| `LevelExporter.cs` | `ExportBuildSlots` | 51 | **25** |
| `LevelExporter.cs` | `ExportTerrain` | 50 | **35** |
| `LevelExporter.cs` | `ExportResources` | 59 | **33** |
| `LevelExporter.cs` | `ExportNavChunks` | 41 | **21** |
| `LevelSource.cs` | 构造器 | 88 | **37** |
| `NavChunkSource.cs` | `NavChunkSlicer.Slice` | 50 | **30** |
| `NavChunkSource.cs` | `NavChunkSliceCheck.TryCheck` | 69 | **21** |
| `NavMeshConsistencyCheck.cs` | `TryCheck` | 50 | **25** |
| `NavMeshProbe.cs` | `Run` | 51 | **13** |
| `TerrainApiSpike.cs` | `ProbeA_HeightPrecisionAndCoordinates` | 62 | **15** |
| `TerrainApiSpike.cs` | `ProbeA2_CoordinateConvention` | 75 | **11** |
| `TerrainApiSpike.cs` | `ProbeB_SlopeLimitOwnership` | 41 | **6** |
| `TerrainApiSpike.cs` | `ProbeC_EditModeAssetReadability` | 61 | **14** |
| `TerrainSource.cs` | 构造器 | 90 | **24** |
| `ColliderFootprintCheck.cs` | `TryCheck` | 56 | **12** |

**全树终态**(同一条复算命令):**168 个方法,最长 40 行,超 40 行 = 零。**

> ⚠️ **`ColliderFootprintCheck.TryCheck` 是第 19 个,原清单里没有它** —— 我第一版扫描器
> 只认「声明行以 `(` 结尾」,它跨行声明没被认出。评审 `M-5` 当时写的「两个 `TryCheck` 各 56」
> 指的就是 `LayerDriftCheck` + 这一个。**扫描器修准之后才看见** —— 这本身就是
> 「自述的清单不等于普查」的一例,如实记在这里。

---

## §二 等价性论证

通用手法:把线性方法体按**已有的注释分段**切成私有方法,每段的语句、次序、报文原文
**逐字搬运**,不改任何判据。

| # | 原形态 | 新形态 | 为什么等价 |
|---|---|---|---|
| 1 | 一个方法体 | 若干私有方法顺序调用 | 调用次序 = 原语句次序(`Violations` / `Warnings` / `NotRun` 的**追加次序**不变) |
| 2 | `continue` 跳过后续判据 | 私有方法里 `return` | 同一位置跳出同一段 |
| 3 | 局部 `reported` / `total` / `truncated` | `ref` 形参传入 | 跨段共享的计数仍是同一个变量 |
| 4 | `readonly` 字段在 ctor 内赋值 | **校验**下放到私有方法,**赋值仍留 ctor** | C# 只允许 ctor 里给 `readonly` 赋值(CS0191);拆开后语义不变 |
| 5 | `nameof(rows)` | 字面量 `"rows"`(helper 里 `rows` 不在作用域) | `nameof` 的结果就是那个字符串 —— `ParamName` 逐字相同 |

### 2.1 本轮**自己抓到并修掉**的一处可观测变更

`TerrainSource.ValidateOneRow` 初稿把 `nameof(rows)` 写成了 `nameof(byId)`
⇒ `ArgumentException.ParamName` 从 `"rows"` 变成 `"byId"`,**报文尾巴也会跟着变**
(`.NET` 的默认 `Message` 会追加 `(Parameter 'rows')`)。这是重构里最容易被当成"无所谓"的
一类差异。已改回字面量 `"rows"`,并在源码就地留注释说明为何不能写成 `nameof(byId)`。

**复核命令**(对比重构前后 `nameof` 的出现):

```bash
git diff -U0 -- unity/Assets/Editor.Tools.Level/ | grep '^-' | grep -o 'nameof([a-zA-Z]*)' | sort | uniq -c
git diff -U0 -- unity/Assets/Editor.Tools.Level/ | grep '^+' | grep -o 'nameof([a-zA-Z]*)' | sort | uniq -c
```

实跑:减号侧 `6×nameof(rows) · 1×nameof(terrain) · 2×nameof(walkable)`;
加号侧 `1×nameof(byId)(**仅出现在注释里**)· 1×nameof(rows) · 1×nameof(terrain) · 2×nameof(walkable)`。
净变化 = **5 处 `nameof(rows)` → 字面量 `"rows"`**,字符串不变。

---

## §三 回归(**三元组:截至时刻 · 批次 · commit**)

| 套件 | 结果 | 对照 |
|---|---|---|
| 编译 | **0 error** | 首轮曾有 3 条 `CS0191`(`readonly` 字段在 helper 里赋值)→ 改成「校验下放、赋值留 ctor」后清零 |
| EditMode 全量 | **`5850 / 5813 / 0 / 0 / 37`** @ 2026-10-29 07:59 CST · 全树拆分轮 · `5599f630`+工作树 | 拆前 `5850 / 5813 / 0 / 37` ⇒ **逐项相同** |
| LevelTool 子集 | **`126 / 124 / 0 / 2`** @ 同批 | 拆前 `126 / 124 / 0 / 2` ⇒ 逐项相同 |
| PlayMode | 见 §五 | 基线 `238 / 234 / 0 / 2 / 2` |

---

## §四 如实边界(**本件证明不了的事**)

1. **纯重构没有「新属性」可证伪。** 旧写法与新写法值相等时,任何夹具都分不出区别。
   本轮**没有新增夹具**(上一批为 `Check` 的早退补过一条,本批无同类结构风险)。
   等价性的依据 = **逐段搬运 + §二 的按构造论证 + 全量回归逐项相同**,
   **不是机器判据**。按本仓 `feedback_refactor_falsifiability_ceiling`:**Proven 记 ✗**。
2. **`TerrainApiSpike` 的四个探针是勘察工具,不是判据。** 拆分的唯一风险是 **`Emit` 次序漂移**
   (本件日志本身就是 spike 证据)。本批保序的依据是**逐条对照原文**,没有机器判据 ——
   因为该 spike 的产出是一次性 log,没有回归夹具钉住它的行序。
3. **未跑 `/code-review`。**
4. **行数 ≠ 可读性。** 本批只满足 `coding-standards` 的 ≤40 硬界,不做职责再设计;
   若某个方法拆完仍觉得别扭,那是设计问题,不是本批的目标。

---

## §五 PlayMode

| 套件 | 结果 | 对照 |
|---|---|---|
| PlayMode | **`238 / 234 / 0 / 2 / 2`** @ 2026-10-29 08:01 CST · 同批 · 同 commit | 与基线**逐项相同**;进程退出码 2 由 2 条既有 **Inconclusive** 引起(`U1Assump6GamepadFocusSpikeTest` 的两条 `*_notRun`),XML `failed = 0` |
