# 实现证据 —— S26-2 `casebook-003` 分册键与改写痕读时投影

**日期**: 2026-11-08
**Story**: `production/epics/casebook/story-003-volume-key-and-revision-trace-projection.md`
**权威**: `design/gdd/casebook.md` 规则二 · **F-39.2** · **AC-39-13** · AC-39-04 ①②⑥ ·
`ADR-008 §三` · `ADR-031`(读侧解码口)
**落点**: `unity/Assets/Gameplay.UI/Casebook/CasebookVolumeProjection.cs`

---

## 0. 结论

| 项 | 结果 |
|---|---|
| `J(c,p)` 末笔折叠 / 痕零存储 / 能力面 / 4 人隔离 | ✅ 14 条判据全绿 |
| 证伪轮 M1 / M2 / M3 | ✅ 各自单红(4 / 2 / 1 条),SHA-256 逐字节还原 |
| `AC-39-04 ③` 跨机 `player_id` 稳定 | 🔴 **`NOT-RUN` · `BLOCKED-BY-45`** |

---

## 1. 一条刻意的设计选择(先说,免得被当成 bug)

**页的存在性跟病例流走,不跟「我落过笔」走。**

零落笔的病例照样出一页(`VolumePage.IsBlank == true`),因为 GDD 规则七要求**空行可聚焦** ——
「页在、痕无」与「这病人不在我的册子里」是两件不同的事。把两者合一,玩家还没写过的病例
会从册子里**直接消失**,而它本该是一张能翻到、能落笔的空白页。

判据:`test_ac1_blankPageForCaseWithoutMyJudgment`。

## 2. 能力面的落实位置(数据侧,不是渲染侧)

作者过滤发生在**解码之后、入册之前**:

```csharp
if (author != playerId) continue;
```

别人的笔**从不进入本册的容器**。⇒ 后面没有任何「渲染时忘了筛」的机会 ——
这是把 AC-39-04 ① 从「界面不显示」抬成「数据里没有」。
配合签名级约束(无「全部作者」重载),两面向同时关死。

## 3. 判据(14 条)

| 组 | 覆盖 |
|---|---|
| AC-1 折叠 | 末笔 `Current` / 前笔 `Stale` · 他人笔不出现 · 空白页 · 同 tick 按 `Seq` · 入参乱序不改结果 |
| AC-2 零落盘 | 反射扫四处面(`CasebookVolumeRecord` / `CasebookPageRecord` / `CasebookVolumeSnapshot` / `CaseClosedPayload`),`trace`/`stale`/`revision` 零命中;**负控**证明词表会中 |
| AC-3 重构不变(单机) | 同流同 id 重投影逐位相同 · 新人加入不影响既有分册 |
| AC-4 能力面 | 每个 `ProjectVolume` 重载都有 `playerId` · 未发号 id ⇒ 全空白(**不退化为全量**) |
| AC-6 4 人隔离 | 同一病例流 4 个作者,各自 1 笔、互不可见 |
| 语义 | `JudgmentTraceState` 是整数枚举(0 / 1)· `null` decoder 拒收 · 空流 ⇒ 空册 |

## 4. 证伪轮(3 条)

| # | 变异 | 期望 | 实测 |
|---|---|---|---|
| **M1** | 关掉作者过滤 | 一切「他人笔不得出现」的判据 | ✅ **4 红**:AC-1 末笔 / AC-3 新人 / AC-4 未发号 / AC-6 四人 |
| **M2** | 末笔不再标 `Current` | AC-1 两条 | ✅ **2 红** |
| **M3** | 加「全部作者」重载 | AC-4 反射断言 | ✅ **恰 1 红** |

还原 = 备份 + `cp` 回 + **SHA-256 逐字节**(`4a09cf1790d414d1…`);`grep -rn FALSIFY unity/Assets` = **0**。

## 5. 首轮全量的一条红:既有门撞上 enum(处置如实记)

`test_casebookTypes_haveNoMutableState` 把 `JudgmentTraceState.value__` 判成「可变字段」——
那是 C# 编译器为 enum 生成的**值存储字段**,不是状态。

**处置 = 把该门的扫描面限定到非 enum 类型**,并补两条守卫:
1. 排除 enum 后类型集为空 ⇒ 红(不许空转);
2. 命名空间里若**没有** enum ⇒ 红(「上面那行排除是死代码,应删掉而不是留着」)。

⚠️ **没有放宽判据**:非 enum 类型的扫描面逐字未变。此门此前从未见过 enum,所以这条从未显形。

## 6. 回归三元组

| 套件 | 计数 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7121 / 7084 / 0 / 0 / 37` | `2026-11-07T23:26:27Z` | sprint-26 S26-2 | `bdd7524c` + 工作树 |
| PlayMode | `365 / 361 / 0 / 2 / 2` | `2026-11-07T23:27:37Z` | 同上 | 同上 |

**基线**(S26-1 收口后)`7107 / 7070` ⇒ **+14 / +14,零回归**。
**等式判据**:`7121 − 7107 = 14` = `casebook_volume_projection_test.cs` 的 `[Test]` 计数。

```bash
grep -c "\[Test\]" unity/Assets/Tests/EditMode/Casebook/casebook_volume_projection_test.cs   # ⇒ 14
```

## 7. 未做(不得借绿)

1. **`AC-39-04 ③` 跨机稳定 = `NOT-RUN · BLOCKED-BY-45`。**
   单机夹具只证「同流同 id 重投影逐位相同」与「新人加入不影响既有分册」,**不证跨机**。
2. **不接加载路径** —— 与 `CaseListOrdering` 同款:入参是「已加载的只读事件列表」,
   本件不读 `IEventSink`、不做流加载。生成本册的调用点归 story-005 / 007。
3. **视觉** —— 铅笔对勾的形态归 42(AC-39-12 `[L]`),本件只交整数值。
