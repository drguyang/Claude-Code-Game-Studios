# S15-4 实现原件 —— TD-audio-028 空间化链接入生产

**日期**:2026-10-18
**对象**:`docs/tech-debt-register.md` §「story-018 补评批」`TD-audio-028` · `sprint-15.md` S15-4
**类型**:Integration(44 渲染面接线)
**判定口径**:每条 [A] 须有**可复算判据**;禁借绿(没跑的写没跑)。

---

## 一、原判定(债行逐字)

`TD-audio-028` 立案时的四项实测事实:

| # | 事实 | 判据 |
|---|---|---|
| ① | `AudioSourceRenderTarget` 直调 `pool.Spawn(...)` **不写 `transform.position`**、**不 pin spatial 三件套** | `AudioSourceRenderTarget.cs:77-82` |
| ② | 换算 / 遮挡 / 排名的唯一宿主 `SpatializationDirector` **生产零构造** | 全仓 `new SpatializationDirector(` 唯一命中 = 类自例注释 `:23` |
| ③ | `AudioAssemblySeams.LatticeSize` = **死字段,零读取方** | `AudioAssemblySeams.cs:42` |
| ④ | **S3-a 负例缺失** —— story-018 的 14 例证伪无一针对「自建第二池 ⇒ 转红」 | `runtime_assembly_test.cs:416` 只测共享池重载 |

**用户裁定四项(2026-10-18,AskUserQuestion,全部取推荐)**:
① `latticeSize` 保留 `1f` 但**显式登记为缺口**;② 遮挡**本轮恒 `false`**,登记缺口;
③ 听者锚点 = **主相机**(`CameraRig.Camera`,承 ADR-020 §七);④ `priority_class` / `min/max_distance`
**本轮用代码常量 + 登记债**(不扩事件表 schema)。

---

## 二、修复落点(逐条对原判定)

| 原判 | 落点 | 形态 |
|---|---|---|
| ① 不写位置 | `AudioSourceRenderTarget.Place(in cue, actorId)` → `SpatializationDirector.Spawn` | `PlayOneShot` / `StartLoop` **两条路径**都在 `Play()` **之前**调用(先播后挪会首帧响在原点) |
| ① 不 pin 三件套 | `AudioSourcePool.CreateSource()` | pin 在**唯一创建点**(全仓唯一 `AddComponent<AudioSource>`)⇒ 不存在「某条源漏 pin」形态;`spatialBlend=1` / `Logarithmic` / `dopplerLevel=0` / min=1 / max=12 |
| ② 导演生产零构造 | `CompositionRoot.AssembleCore` 音频装配段 | `new SpatializationDirector(latticeSize, audioPool)` —— **共享池重载**(自建重载仍在,生产不走) |
| ③ `LatticeSize` 死字段 | 同上,`audio?.LatticeSize > 0f ? audio.LatticeSize : AudioTuning.LatticeSizePlaceholder` | 死字段转为**唯一入参** |
| ④ S3-a 负例缺失 | `spatialization_wiring_test.cs::test_td028_selfBuiltPool_wouldBreakSinglePoolInvariant` | 自建池 ⇒ 池根 1 → **2** ⇒ 断言钉的就是失效形态本身 |

**新增/改动文件**:

- `unity/Assets/Gameplay.Presentation/Audio/AudioTuning.cs` —— 五个空间化种子常量(`LatticeSizePlaceholder` / `SpatialMinDistance` / `SpatialMaxDistance` / `PriorityClassSelf` / `PriorityClassWorldOrPhysiology` / `PriorityClassAmbience`),带「正式家不在此」的逐项注
- `unity/Assets/Gameplay.Presentation/Audio/AudioSourcePool.cs` —— `CreateSource()` pin 三件套
- `unity/Assets/Gameplay.Presentation/Audio/AudioSourceRenderTarget.cs` —— 新构造(池 + 导演 + 格边长 + 遮挡缝)、`Place`、`SetListenerPosition`、`SpatializedCount`、`LatticeSize`
- `unity/Assets/Gameplay.Boot/CompositionRoot.cs` —— 空间化装配段
- `unity/Assets/Gameplay.Boot/CompositionRootServices.cs` —— `Spatialization` / `RenderTarget` 两属性
- `unity/Assets/Gameplay.Boot/BootRoot.cs` —— 启动序第 5.1 步注入听者(主相机)
- `unity/Assets/Tests/EditMode/Audio/spatialization_wiring_test.cs` —— **新夹具,11 测**

### 实现轮另修一处真 bug(原债行未记载)

`StartLoop` 的池键 = `LoopActorOf(handleId)`(**负偏移**,`-handleId-1`),与 `cue.Source` **不同**。
首版 `Place(source, in cue)` 把**原 cue** 交给导演,而 `SpatializationDirector.Spawn` 内部用
`cue.Source` 取池条目 ⇒ **位置写到另一条源(或新建一条幽灵源)上,真正在响的循环源留在原点**。
呼吸层恰是所有声源里最长响的一层,该失效形态在听感上是「呼吸不随病人走」。

**修法**:`Place(in cue, int actorId)` 显式收池键,`actorId != cue.Source` 时重映射成新 DTO 再交导演。
**反证 F3**(把 `Place(in cue, actor)` 改回 `cue.Source`)⇒ `test_td028_startLoop_writesWorldPosition_onTheLoopSource` 红。

---

## 三、证伪轮(F1–F12 逐例转红)

**纪律**:改 → 跑 → 还原在**同一命令内**完成(`/tmp/fzrun.py`),`finally` 还原后做 SHA-256 逐字节校验,
每例均打印 `restored sha ok`。判据 = 对应断言**必须转红**;仍绿 ⇒ 该夹具弱判别面 = BLOCKING。

| # | 停用的守卫 | 转红用例 |
|---|---|---|
| F1 | 删 `PlayOneShot` 里的 `Place(...)` | `oneShot_writesWorldPosition_fromCueCell` · `listenerInjection_...` · `occludedTrue_appliesLowpass` |
| F2 | 删 `StartLoop` 里的 `Place(...)` | `startLoop_writesWorldPosition_onTheLoopSource` |
| F3 | `Place(in cue, actor)` → `Place(in cue, cue.Source)`(**回退本轮的池键修复**) | `startLoop_writesWorldPosition_onTheLoopSource` |
| F4 | `source.spatialBlend = 1f` → `0f` | `poolPinsSpatialTriple_atCreationPoint` |
| F5 | `source.dopplerLevel = 0f` → `1f` | `poolPinsSpatialTriple_atCreationPoint` |
| F6 | 导演走**自建池重载**(`new SpatializationDirector(latticeSize)`) | `productionAssembly_withSeams_sharesOnePool_withDirector` |
| F7 | 组合根**不建导演**(`spatialDirector = null`) | `productionAssembly_withSeams_sharesOnePool_withDirector` |
| F8 | `SetListenerPosition` 方法体清空 | `listenerInjection_movesTheSourceRelativeToListener` |
| F9 | 遮挡缝恒 `false`(忽略 `_occludedFor`) | `occludedTrue_appliesLowpass_throughSameSeam` |
| F10 | 遮挡缝恒 `true` | `occludedFalse_appliesNoLowpass` |
| F11 | `Place` 整体空转(`if (_spatial == null) return;` → `return;`) | 4 条(oneShot / startLoop / listener / occludedTrue) |
| F12 | `LatticeSize` 恒返回 0 | `productionAssembly_withSeams_sharesOnePool_withDirector` |

**全部 12 例转红,零假绿。**

⚠️ **F11 值得单记**:一次停用 `Place` 打红 4 条,说明该守卫被**四条不同角度**的断言覆盖
(位置写入 / 循环路径 / 听者距离 / 遮挡施加),不是单点依赖。

---

## 四、回归对账

| 项 | 值 | 出处 |
|---|---|---|
| 本夹具单跑 | **11 / 11 / 0** | `test-results.xml`(filter = `...Audio.SpatializationWiringTest`) |
| 全量 EditMode(本件后) | `3781 / 3735 / 1 / 45` | `unity/Logs/s154-full-editmode.xml` |
| 全量 EditMode(基线 S15-3) | `3770 / 3724 / 0 / 45` | `active.md` Session Extract |

**等式对账**:`3781 − 3770 = 11` = 本夹具新增条数;
`passed 3735 − 3724 = 11`;`failed 1 − 0 = 1`。

**唯一红**:`MovementFeedTest.test_move_afterFreeFall_horizontalDisplacementBoundedByConfig` ——
已知间歇红(`TD-player-01`),**单跑 5/5 全绿**实测:
`unity test unity --mode EditMode --filter DaYiJingCheng.Tests.Boot.MovementFeedTest` ⇒ `5 / 5 / 0`。
⚠️ **该绿不用于抵扣上面的计数**(借绿纪律):全量那一轮它就是红的,账就按红的记。

---

## 五、如实登记(禁借绿 · 本件未证的部分)

| 项 | 状态 | 说明 |
|---|---|---|
| **距离衰减 / 遮挡音色的人耳半边** | **NOT-RUN** | 归 `TD-audio-101/102` 听测批。本件只证「位置被写进 `transform`」「低通被按缝施加」,**不证**「听起来远近有别」 |
| **`latticeSize` 正式值** | **NOT-RUN** | 今日 = `1f` 占位;正式值归**系统 6**(`LATTICE_SIZE` 至今无值)⇒ 新立 `TD-audio-035` ① |
| **遮挡真 raycast** | **NOT-RUN** | 生产恒传 `occludedFor: () => false`,全库零遮挡投射代码 ⇒ `TD-audio-035` ② |
| **`priority_class` / `min` / `max_distance` 来自事件表** | **NOT-RUN** | 三列在 JSON 里有、有构建期键存在性门,但 `AudioEventRow` **未编码**它们 ⇒ 今日取代码常量 ⇒ `TD-audio-035` ③ |
| **IL2CPP 三格**(ADR-012 矩阵) | **NOT-RUN** | 本机仅 Mono |
| **`AudioSourcePool` 的 `min/maxDistance` 是否与听感匹配** | **数值面** | 12 m 取 `WorldBreath_Baseline` 行现值作对账锚;数值归用户 |

---

## 六、测试清单(11 测 → AC 映射)

| # | 测试 | 钉的判据 |
|---|---|---|
| 1 | `oneShot_writesWorldPosition_fromCueCell` | 位置由 `cue.Cell × latticeSize` 派生(jitter 是格内偏移,不越格) |
| 2 | `startLoop_writesWorldPosition_onTheLoopSource` | **循环源(负键)位置被写**,且 `cue.Source` 处无幽灵源 |
| 3 | `poolPinsSpatialTriple_atCreationPoint` | 三件套 + min/max 在**创建点**被 pin |
| 4 | `selfBuiltPool_wouldBreakSinglePoolInvariant` | **S3-a 负例**:自建池 ⇒ 池根 2 |
| 5 | `productionAssembly_withSeams_sharesOnePool_withDirector` | 生产装配:导演存在、共享袋内池、格边长同源、池根恰一 |
| 6 | `productionAssembly_withoutSeams_buildsNoDirector` | 无缝 ⇒ 不建导演(可观测不静默) |
| 7 | `listenerInjection_movesTheSourceRelativeToListener` | 注入前距离 0 / 注入后 ≈ 10 m |
| 8 | `withoutListener_distanceIsZero_notNaN` | 未注入 ⇒ 0 且非 NaN(降级明确) |
| 9 | `occludedFalse_appliesNoLowpass` | 缝 false ⇒ 不施加低通 |
| 10 | `occludedTrue_appliesLowpass_throughSameSeam` | 缝 true ⇒ 挂低通且 cutoff > 0(不静音) |
| 11 | `legacyConstructor_noSpatialization_keepsOldBehaviour` | 旧构造行为不变(既有 62 处调用不受影响) |

---

## 七、命令(第三方可重跑)

```bash
# 本夹具
rm -f test-results.xml
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Audio.SpatializationWiringTest" -- --log-file -
# 期望:total 11 / passed 11 / failed 0

# 全量 EditMode
rm -f test-results.xml
unity test unity --mode EditMode -- --log-file -
# 期望:total 3781 / passed 3735 / failed 1(唯一红 = TD-player-01 已知间歇)

# 证伪单例(示例:F3 —— 回退池键修复)
python3 /tmp/fzrun.py unity/Assets/Gameplay.Presentation/Audio/AudioSourceRenderTarget.cs \
  "            Place(in cue, actor);" "            Place(in cue, cue.Source);"
# 期望:restored sha ok; RED: test_td028_startLoop_writesWorldPosition_onTheLoopSource
```
