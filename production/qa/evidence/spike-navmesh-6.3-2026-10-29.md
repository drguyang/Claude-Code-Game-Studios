# Spike:NavMesh 采样 API 实测(Unity 6.3 · `com.unity.ai.navigation` 2.0.14)

**对象**:`production/epics/level-tool/story-005-world-nav-chunk-slicing.md` AC-4(C3 导航格 ↔ NavMesh **告警**)
**日期**:2026-10-29
**目的**:ADR-022 把 NavMesh 相关 API 标为 **post-cutoff**(Engine Risk MEDIUM)。
story-005 的 Test Evidence 逐字要求「若 `com.unity.ai.navigation` 行为与假设不符,须落 spike 报告」。
**本件是勘察,不是判据** —— 它回答三个二选一,不回答「C3 该怎么实现」。

探针:`unity/Assets/Editor.Tools.Level/NavMeshProbe.cs`(菜单 `大医精诚/关卡工具/[spike] NavMesh 采样 API 实测`)
实跑:`unity/Logs/s5-spike.log`(`-executeMethod …NavMeshProbe.Run -quit`,2026-10-29)

---

## §一 实跑输出(逐字)

```
[SPIKE-NAV] ==== com.unity.ai.navigation 采样 API 实测 ====
[SPIKE-NAV] unity = 6000.3.24f1
[SPIKE-NAV] NavMesh.GetSettingsCount = 1
[SPIKE-NAV] ① SamplePosition(无 NavMesh) => false(无命中 —— 没有 baked NavMesh)
[SPIKE-NAV] ② maxDistance 口径 => 无 baked NavMesh ⇒ 本项判定不了(须有真实 NavMesh 资产才能测;本 spike 如实登记为 NOT-RUN)
[SPIKE-NAV] ③ CalculateTriangulation => ok(vertices=0, indices=0)
[SPIKE-NAV] ==== 结论 ====
[SPIKE-NAV] 空场景不命中 ⇒ C3 可在「无 NavMesh 数据」时退化为「无法判定」,不误报告警
```

---

## §二 三个问题的答复

| # | 问题 | 实测 | 对 C3 的后果 |
|---|---|---|---|
| ① | 无 baked NavMesh 时 `NavMesh.SamplePosition` 返回 false 还是抛 / 静默成功? | **false**(无命中) | **不抛、也不假成功** ⇒ 可用作「这一格达不达得到」的判据 |
| ② | `maxDistance` 是水平还是三维距离? | **未判**(无真实 NavMesh ⇒ NOT-RUN) | ⚠️ 见 §四:本 story **不**依赖这个口径 |
| ③ | batch 里 `NavMesh.CalculateTriangulation()` 能跑吗? | **能**,无数据时回 `vertices=0, indices=0` | ⇒ 可用作**「本场景到底有没有 NavMesh 数据」的存在性探针** |

**关键结论(③)**:`CalculateTriangulation().vertices.Length == 0` 是「无 NavMesh 数据」的**可靠判据**
(既不抛也不假成功)。C3 据此把每一格的采样结果分成**三态**:

- `Reachable` —— 有数据且采样命中
- `Unreachable` —— 有数据且采样不命中
- `Unknown` —— **本场景根本没有 NavMesh 数据**(`vertices.Length == 0`)

`Unknown` **不产生告警**。这一条不是完备性上的讲究,是**必需**:P0 至今没有任何 baked NavMesh
资产(P0 关卡是逻辑格 + 抽象表现),若把「无数据」当成「全部不可达」,C3 会对**每一个**
逻辑可走的格报一次告警 —— 上千条噪声会把真告警淹掉,而那种「跑一次就满屏警告」的检查
在实践中等于不存在。

---

## §三 与 ADR-022 假设的对照

ADR-022 §四 C3 逐字:「逻辑可走与 NavMesh 可走**不矛盾**(不要求逐格相等)= **告警**」。

**假设 1**(被证实):采样 API 在无数据时**不会**假成功。⇒ 三态划分成立。
**假设 2**(被证实):batch 下能判定「有没有数据」。⇒ `Unknown` 有可观测的构造路径。

⇒ **无需回 ADR-022 追加裁决**(story §诚实边界 5 的触发条件未成立)。

---

## §四 本件**没**回答的(禁借绿)

1. **`maxDistance` 的口径**(水平 / 三维)—— 无真实 NavMesh 资产,NOT-RUN。
   本 story 的 C3 **不依赖**该口径:采样半径是一个**注入参数**,夹具用合成 sampler 直接给三态,
   不与引擎的几何判定打交道。真实资产到位后若口径不同,影响的是**告警条数**,不是判据成立与否。
2. **NavMesh 烘焙质量 / tile 更新成本** —— 归 27 / 表现层(故事卡 Out of Scope)。
3. **真实 NavMesh 资产下的采样精度** —— P0 今日无此资产,如实登记 NOT-RUN。
