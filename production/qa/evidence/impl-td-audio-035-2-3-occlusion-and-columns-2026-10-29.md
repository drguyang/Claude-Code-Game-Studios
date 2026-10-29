# 实现证据 —— `TD-audio-035` ②③:遮挡链接通 + 空间化四列全行承载

- **日期**:2026-10-29
- **批次**:音频债批 C 收尾 · `TD-audio-035` ②③(① 已于 `dc958f58` 结案,见
  `impl-td-audio-035-1-lattice-source-2026-10-29.md`)
- **对象**:`unity/Assets/Gameplay.Boot/{LatticeOcclusionProbe(新),AudioAssemblySeams,BootRoot,CompositionRoot}.cs` ·
  `unity/Assets/Gameplay.Presentation/Audio/{AudioSourceRenderTarget,AudioSourcePool,AudioEventDataSet,AudioEventCookedCodec}.cs` ·
  `unity/Assets/Editor.Tools.{Bake,Gates}/…` · `assets/data/audio_events.json` ·
  `tests/unit/audio_system/fixtures/*.json` · `unity/Assets/Tests/EditMode/Audio/{lattice_occlusion_test(新),event_table_gate_test,assembly_boundary_test,spatialization_wiring_test}.cs`

---

## 一、原判定(债册原文)与实测复核

### ② 遮挡判定今日恒 `false`

> `CompositionRoot` 传 `occludedFor: () => false`,**真 raycast 零实现**(全库无遮挡投射代码)
> ⇒ `OcclusionDriver` 的低通路径在运行期**从不触发**,「穿门变闷」的语义今天无承载。

**实测复核**:判定成立,且原文**低估了缺口** —— 不止「没实现」,是**缝形状不可用**:

| 原文断言 | 实测(2026-10-29) | 判 |
|---|---|---|
| 遮挡恒 `false` | 成立 —— `CompositionRoot` 传 `occludedFor: () => false` | ✅ |
| 真 raycast 零实现 | 成立 —— 全生产树无投射调用 | ✅ |
| 「补一个实现即可」 | **不成立** —— `Func<bool>` 无 cell / 无 listener 入参,且每次 `Spawn` **只求值一次** | ❌ **低估** |

⇒ 必须**先改签名**,不是只补实现。这是 ① 轮审计已记下、本轮兑现的部分。

### ③ `priority_class` / `min_distance` / `max_distance` 未由事件表承载

> 三列在 `assets/data/audio_events.json` 里存在且有构建期**键存在性**门,但 `AudioEventDataSet.AudioEventRow` **未编码**它们(cooked 里没有)

**实测复核**:原文前提**与实况不符**:

| 原文断言 | 实测(改动前) | 判 |
|---|---|---|
| 三列在表里存在 | **只在 `WorldBreath_Baseline` 一行**(19 行里 1 行),门 = `ValidateWorldBreathRow`(AC-44-16 专属) | ❌ 过期 |
| `priority_class` | **全表零行、零门** | ❌ 过期 |
| `AudioEventRow` 未编码 | 成立 | ✅ |

⇒ ③ 不是「透传已有列」,是**先有列**。

---

## 二、修复落点

### ② 遮挡链(用户裁定甲案:改双格签名 + 格级视线走查)

| # | 落点 | 内容 |
|---|---|---|
| R-1 | `LatticeOcclusionProbe.cs`(**新**) | 三维 Bresenham 整数格视线走查;**端点豁免**(声源在门口 / 听者站门框格是正常形态);越界格按 `WorldGeometry.GetTerrain` fail-closed = 阻挡 |
| R-2 | `AudioAssemblySeams.cs` | `OccludedFor: Func<Int3,Int3,bool>`(原 `Func<bool>`)+ `ListenerCell: Func<Int3>` + `BindOcclusion(seams, geometry)` |
| R-3 | `AudioSourceRenderTarget.cs` | `IsOccluded(in Int3 sourceCell)` —— 双格喂入;新增 `OcclusionEvaluations` 可观测面;**未接缝不计数** |
| R-4 | `BootRoot.cs` | 启动序 2.8 `BindOcclusion`;5.2 注入听者格(相机位置 → `FloorToInt(p / lattice)`) |
| R-5 | `assembly_boundary_test.cs` | `typeof(Int3)` 入 `EntryAllowlist`(用户裁定甲:扩允许面) |

**为什么不用 `Physics.Raycast`**(用户裁定核心):物理射线读**视觉层** float 几何,而 ADR-015 §一 明令视觉层采样禁入 sim;遮挡输入必须可复现 ⇒ 走逻辑层整数格。且 `world_geometry.cooked` 的 `walkable` **已有墙**(16×1×16 外环全 0)⇒ 输入已存在,不需要新数据。

**为什么 `LatticeOcclusionProbe` 住 `Gameplay.Boot` 而不是 44**:AC-44-B3 入口白名单禁 44 公开签名出现 `Sim` / `Sim.Codec` 类型,而走查输入是 `WorldGeometry`(住 `Sim.World`)。44 侧只收 `Func<Int3,Int3,bool>`。

### ③ 空间化四列全行承载(用户裁定乙案:加列 + 门,19 行全填)

| # | 落点 | 内容 |
|---|---|---|
| R-6 | `assets/data/audio_events.json` | 19 行各补 `min_distance: 1` / `max_distance: 12` / `occlusion_lowpass: 1200` / `priority_class: 1`(**纯追加**,格式保持) |
| R-7 | `AudioEventTableGates.cs` | `RowKeys` 加 `priority_class`;新门 `ValidateSpatialColumns`:每行**必带**四列 + 值域(`>0`、`min ≤ max`、`lowpass > 0`、`pc ∈ {0,1,2}`)+ `NOT-RUN` 守卫 |
| R-8 | `AudioEventTableBinder.cs` / `AudioEventTableCookedWriter.cs` / `AudioEventCookedCodec.cs` / `AudioEventDataSet.cs` | 四列透传全链;**写原文而非解析后的 int**(`null` 与 `0` 必须可分) |
| R-9 | `AudioSourceRenderTarget.cs` / `AudioSourcePool.cs` | `PriorityClassOf` / `OcclusionLowpassOf` / `ApplySpatialDistance` 改为查表;未注入 / 缺列 ⇒ 回落 `AudioTuning` 并计 `SpatialParamFallbacks` |
| R-10 | `CompositionRoot.cs` | `sources.SetTable(...)` + `audioPool.SetTable(...)` |

**同轮修掉一处真 bug**(原债行未记载):`AudioSourcePool` 的 `minDistance` / `maxDistance`
原先只在 `CreateSource`(**源创建点**)pin 一次,但源是**复用**的(闲置栈回流)⇒
第二次 `Spawn` 换了 cue 会**沿用上一条 cue 的距离**。改为每次 `SpawnCore` 按本行表列重设。

---

## 三、证伪轮(S12-4 形态:改 → 跑 → 原子还原 → SHA-256)

驱动脚本:`/home/gu/.claude/jobs/181888b1/tmp/falsify035c2.py`(每条变异独立 Unity 启动;
前置逐条核对盘上无变异串;`total==0 ⇒ FILTER-MISS` 中止;还原后逐文件 SHA-256 校验)。

| 变异 | 打靶判据 | 期望红 | 实测 |
|---|---|---|---|
| M1 | `priority_class` 闭集放宽 `2 → 9` | `test_spatialColumns_priorityClassOutOfClosedSet_reportsRed` | **RED-命中** |
| M2 | 删 `min > max` 倒置判据 | `…minGreaterThanMax_reportsRed` + `…overlappingViolations_reportBothErrors` | **RED-命中 2 条** |
| M3 | 缺列存在性判据停用 | `…missingColumn_reportsRed_perColumn` | **RED-命中** |
| M4 | `occlusion_lowpass ≤ 0` 判据停用 | `…lowpassNonPositive_reportsRed` | **RED-命中** |
| M5 | `rows == null` NOT-RUN 守卫停用 | `…rowsNull_reportsNotRunGuard` | **RED-命中** |
| M6 | 行本体 null NOT-RUN 守卫停用 | `…rowNull_reportsNotRunGuard` | **RED-命中** |
| M7 | 非整数原文判据停用 | `…nonInteger_reportsRed` | **RED-命中** |
| M8 | `min_distance ≤ 0` 判据停用 | `…minDistanceNonPositive_reportsRed` | **RED-命中** |

**8/8 全红;还原后两文件 SHA-256 逐字节 = 变异前**(脚本内断言 + 轮后 `sha256sum -c` 复核,均 OK)。

### ⚠️ 首轮 2/2 假存活 —— 根因不是夹具瞎,是靶场里没有靶

首轮证伪只打了 5 条变异,其中 **M1(闭集放宽)、M2(删倒置判据)双双假存活**。
根因:**新门只有真种子表与最小夹具这两个间接载体,而两者四列取值全同** ——
`priority_class` 19 行全是 `1`、距离全 `(1,12)` ⇒ 越界分支与倒置分支**无输入可达**。
这正是 `feedback_falsify_one_guard_per_property` 记的「**靶点对,但靶场里没有靶**」。

⇒ 本轮补 **12 条专门夹具**(`test_spatialColumns_*`,直调 `ValidateSpatialColumns` 做单因隔离),
补后重跑 8 变异全红。

---

## 四、可复算命令

```bash
# 1) 四列在真种子表里全行存在
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import json
d=json.load(open('assets/data/audio_events.json'))
rows=d['rows']
from collections import Counter
c=Counter(k for r in rows for k in ('min_distance','max_distance','occlusion_lowpass','priority_class') if k in r)
print(len(rows), dict(c))
"
# 实测: 19 {'min_distance': 19, 'max_distance': 19, 'occlusion_lowpass': 19, 'priority_class': 19}

# 2) 遮挡链生产接点(BootRoot 绑 + 组合根传)
cd unity && grep -n "BindOcclusion\|occludedFor" Assets/Gameplay.Boot/BootRoot.cs Assets/Gameplay.Boot/CompositionRoot.cs
# 实测: BootRoot:234 BindOcclusion;CompositionRoot:1150 Func<Int3,Int3,bool> occludedFor = audio?.OccludedFor

# 3) 新门夹具(逐夹具跑 —— Unity 只认最后一条 -testFilter,见 ① 轮证据件 §六)
#    -testFilter "DaYiJingCheng.Tests.Unit.Audio.EventTableGateTest"
#    实测:total=81 passed=81 failed=0
#    -testFilter "DaYiJingCheng.Tests.Unit.Audio.LatticeOcclusionTest"
#    实测:total=8 passed=8 failed=0
```

---

## 五、回归三元组(S12-0 口径)

| 套件 | 截至时刻 | 批次 | commit | 结果 |
|---|---|---|---|---|
| EditMode | 2026-10-29 17:31 CST | 全量(无 filter) | `699cbfc7` + 工作树 | **`5895 / 5858 / 0 / 0 / 37`**(exit 0) |
| PlayMode | 2026-10-29 17:46 CST | 全量(无 filter) | `699cbfc7` + 工作树 | **`238 / 234 / 0 / 2 / 2`**(exit 2,与基线逐位相同) |

**EditMode 等式(不是照抄旧数)**:`5883 + 12 = 5895`

- `5883` = 本轮改动前全量实测 @ 2026-10-29 15:07 CST(即 035③ 首跑,含 44 红 —— 该 44 红
  **根因是夹具没跟上**,非数据缺列;详见 §六);
- `+12` = 本轮新增的 `test_spatialColumns_*` 专门夹具。
  复算:`grep -c "public void test_spatialColumns" unity/Assets/Tests/EditMode/Audio/event_table_gate_test.cs` 实测 **12**。

**PlayMode 的 2 条 Inconclusive 是既有的具名占位**,不是新失败
(`U1Assump6GamepadFocusSpikeTest.…_notRun`,方法名自带 `_notRun`)。exit 2 与基线同形,
**不写成绿、也不写成红**。

---

## 六、本轮踩到的坑(如实登记)

### 1. 44 条红的真根因是**夹具没跟上**,不是数据缺列

首跑全量 EditMode:`5883 / 5795 / 51 failed`。其中 44 条全在 `EventTableGateTest`,
而**真种子表与最小夹具都报「缺列」** —— 看上去像数据缺列,实际是
`event_table_gate_test.cs` **自带一份 JSON 解析**(镜像生产 `AudioEventTableBinder`),
我只改了生产侧 ⇒ 测试侧 `ParseRow` 读不到四列。**两处须同步,漏一处就是 44 条假红。**

另 3 条是测试自构造行的归因污染:
- `test_ac0191_*`:内联追加行没带四列 ⇒ 门多报 4 条 ⇒ `Count == 1` 失效;
- `test_rule8_adventitiousAssetEntry_*`:改名副本没继承四列 ⇒ 同上;
- `test_rowNullElement_*`:新门的 null 行报文缺 `NOT-RUN` 字样 ⇒ 既有「每条 error 都须含 NOT-RUN」断言失效。

### 2. 停轮时变异残留漏查

第一次停证伪轮时,我用 `grep` 只查了 `pc > 9` / `if (false)` / `rows == null` 三种形态,
**漏了 M3 的 `if (raw == null) return;`** ⇒ 报「无残留」是错的,该变异一直在盘上。
后果:后续一条夹具（缺列用例）报 0 条错误,我还以为是夹具问题。

⇒ **纪律**:停轮后须**逐条**核对**每一条**变异的原文/变异串,不能只查子集。
本轮的补救 = 第二版脚本加**前置校验**(开跑前逐条核对盘上无变异串,不干净则中止)。

### 3. 我自己写的两条夹具断言有缺陷(离线复算发现)

- `max=0` 用例取 `min=1` ⇒ `min > max` 同时成立 ⇒ 门报 **2 条**,断言 `Count == 1` 过窄;
- 改取 `min=0` 做「隔离」⇒ 又触发「`min ≤ 0`」,仍 2 条。

**离线复算结论**:`max ≤ 0` 的**单因隔离输入不存在** ——
`min > 0` ⇒ 必触发倒置;`min ≤ 0` ⇒ 必触发 min 值域。
⇒ 改为断言「该条存在」(`Has.Some.Matches`),重叠面的可分性另由
`test_spatialColumns_overlappingViolations_reportBothErrors` 钉住(它断言**恰好 2 条**)。

---

## 七、本轮**不**证(边界,防读成「空间化已完整」)

| 项 | 状态 | 归属 |
|---|---|---|
| `priority_class` 的**类序真的分档** | **NOT-RUN / 未做** | 19 行**全填 1**(用户裁定乙案的值)—— 列**承载**闭合,但所有 cue 仍同档 ⇒ `rank_key` 第二三键仍退化为纯距离 + jitter。**要真分档须按 cue 语义逐行定值**(0 自身/检查 · 1 世界≈生理 · 2 氛围),那一步**没做** |
| `min_distance` / `max_distance` / `occlusion_lowpass` 的**数值** | **NOT-RUN / 归用户数值轮** | 19 行全填 `1 / 12 / 1200`,是**单一占位值**,不是逐 cue 调过的值 |
| 遮挡的**听感** | **NOT-RUN** | 低通是否「变闷」而非「变哑」是人耳判定(`TD-audio-101/102` 听测批) |
| 遮挡几何的**充分性** | **NOT-RUN** | P0 关卡只有 16×1×16 外环墙;门 / 家具 / 多层结构的遮挡形态**无夹具覆盖** |
| 「启动序真的跑了 `BindOcclusion`」 | **论证,非观测** | 源文本判据(`test_bootRoot_appliesLattice_beforeAssemble` 同款)钉「写在 BootRoot 里且顺序正确」;今日世界几何 16×1×16 且相机在室内 ⇒ **值断言在 PlayMode 上判别力弱**,不给假证据 |
| `SpatialParamFallbacks` 的生产值 | **NOT-RUN** | 可观测面已建,但生产装配路径下无人读它(调试视图归 51,见 ADR-019 §五) |
