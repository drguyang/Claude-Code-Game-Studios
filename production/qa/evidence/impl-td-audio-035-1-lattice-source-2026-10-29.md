# 实现证据 —— `TD-audio-035` ①:格边长换源(44 不再自造 `LATTICE_SIZE`)

- **日期**:2026-10-29
- **批次**:音频债批 C · `TD-audio-035` ①(三项里只结 ①;②③ 仍 open)
- **commit**:`ca8ed516` + 工作树(见 §五 三元组)
- **对象**:`unity/Assets/Gameplay.Boot/{MinimalWorldFactory,AudioAssemblySeams,BootRoot,CompositionRoot}.cs` ·
  `unity/Assets/Gameplay.Presentation/Audio/{AudioTuning,LatticeToWorld}.cs` ·
  `unity/Assets/Tests/EditMode/Audio/{audio_lattice_source_test,spatialization_wiring_test}.cs` ·
  `unity/Assets/Tests/EditMode/LevelTool/world_geometry_load_test.cs`

---

## 一、原判定(债册原文,节选)

> ① **`latticeSize` = `AudioTuning.LatticeSizePlaceholder`(1f)** —— 正式值归**系统 6**
> (`LatticeToWorld` 文件头明令「44 不得自造比例」;**6 侧 `LATTICE_SIZE` 至今无值**)。

**实测复核(本轮)**:判定成立的**只剩一半**。

| 原文断言 | 实测(2026-10-29) | 判 |
|---|---|---|
| 44 读自己的占位常量 | 成立 —— `CompositionRoot` 的 `latticeSize` 恒取 `AudioTuning.LatticeSizePlaceholder` | ✅ 仍成立 |
| 6 侧 `LATTICE_SIZE` 至今无值 | **不成立** —— `assets/data/world_geometry.json` 自关卡工具 story-002(`a60dcfc6`)起带 `lattice_size_mm = 1000`,经 ADR-014 烘成 `world_geometry.cooked`,运行期可解(`WorldGeometryCookedCodec.LatticeSizeMm`) | ❌ **过期** |

**真缺口被原文写偏了**:不是「6 没给值」,是**运行期没有出口** —— `LatticeSizeMm` 除
`MinimalWorldFactory` 有一条 `Debug.Log` 之外**零消费方**(复算见 §四 命令 1),装配层拿不到 ⇒ 44 只能自造。

---

## 二、修复落点

| # | 落点 | 内容 |
|---|---|---|
| R-1 | `MinimalWorldFactory.cs` | 新增重载 `Create(IDataProvider, out int latticeSizeMm)`;`latticeSizeMm = geo.LatticeSizeMm;`(与日志同一变量)。原单参重载转调 `Create(provider, out _)`(39 处既有调用零改动) |
| R-2 | `AudioAssemblySeams.cs` | 新增 `MetersFromMillimetres(int)`(**mm→m 的唯一换算处**,`<= 0` 抛)+ `ApplyLatticeFromWorldGeometry(seams, mm)`(缝为 `null` ⇒ 空操作,与「缝不接 ⇒ `NullRenderTarget` 链」同一处置)。**`LatticeSize` 默认值 `1f → 0f`**(`0` = 未供给) |
| R-3 | `BootRoot.cs` | 启动序 2.8:`world = MinimalWorldFactory.Create(provider, out int latticeSizeMm);` 紧随一行 `ApplyLatticeFromWorldGeometry(_audioSeams, latticeSizeMm);` —— **在 `CompositionRoot.Assemble` 之前** |
| R-4 | `CompositionRoot.cs` / `AudioTuning.cs` / `LatticeToWorld.cs` | 注释订正:兜底常量退为「无世界几何的装配路径(62 处 EditMode 夹具)」专用;`LatticeToWorld` 头注「`LATTICE_SIZE` 至今无值」就地标为过期 |

**为什么把 mm→m 放在装配层**:`LatticeToWorld.ToWorld` 的 `latticeSize` 入参**已经是米**,
它的契约是「格 → 世界坐标」;单位换算是**数据边界**的事。两处各写一次 `/ 1000f` = 第二真源。

**为什么默认值要改**:旧默认 `1f` 是一份**悄悄生效的 44 自造比例** —— 它与 6 的
`1000 mm` 同值纯属巧合,因此在可观测面上与「正确换源」**完全同形**(这正是本债能长期潜伏的原因)。
改成 `0 = 未供给` 后,「组合根回落兜底」变成一个**显式分支**。

---

## 三、证伪轮(S12-4 形态:改 → 跑 → 原子还原 → SHA-256)

驱动脚本:`/home/gu/.claude/jobs/181888b1/tmp/falsify035.py`(每条变异独立 Unity 启动;
`total == 0` ⇒ `FILTER-MISS` 中止;期望测试名必须出现在红列表)。

| 变异 | 打靶 | 期望红 | 实测 |
|---|---|---|---|
| M1 | `latticeSizeMm = geo.LatticeSizeMm` → `= 1000` | `world_geometry_load_test::test_ac4_latticeSizeOutParam_comesFromArtifact` | **RED-命中**(total=15) |
| M2 | `return latticeSizeMm / 1000f` → `/ 1f` | `audio_lattice_source_test::test_metersFromMillimetres_convertsAtOneThousandth` | **RED-命中**(total=6,连带 `test_applyLattice_writesSixSourceValue`) |
| M3 | `if (latticeSizeMm <= 0)` → `<= int.MinValue` | `…::test_metersFromMillimetres_rejectsNonPositive` | **RED-命中**(total=6) |
| M4 | `seams.LatticeSize = MetersFromMillimetres(…)` → `_ = MetersFromMillimetres(…)` | `…::test_applyLattice_writesSixSourceValue` | **RED-命中**(total=6) |
| M5 | 摘掉 `BootRoot` 的 `ApplyLatticeFromWorldGeometry` 调用 | `…::test_bootRoot_appliesLattice_beforeAssemble` | **RED-命中**(total=6) |
| M6 | 默认值改回 `public float LatticeSize = 1f;` | `…::test_seam_default_isUnsupplied_notOneMetre` | **RED-命中**(total=6) |

**6/6 全红;6/6 还原后 `sha256` 逐字节 = 变异前**(脚本内断言,任一条不等即整轮判失败)。

---

## 四、可复算命令

```bash
# 1) 「6 有值但无出口」的复算:改动前,全生产树对 LatticeSizeMm 的消费方
cd unity && grep -rn "LatticeSizeMm" Assets --include=*.cs | grep -v "/Tests/"
# 改动前实测:仅 WorldGeometryCookedCodec(定义/校验)· MinimalWorldFactory(:200 的日志)· WorldLattice.cs
#            ⇒ 装配层无任何取值路径
# 改动后实测:以上 + MinimalWorldFactory 的 out 出口(1 处)
```

```bash
# 2) 两个夹具(逐夹具跑 —— 见 §六 的踩坑)
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.Unit.Audio.AudioLatticeSourceTest" -testResults /tmp/a.xml -logFile /tmp/a.log
# 实跑:total=6 passed=6 failed=0
```

```bash
# 3) world_geometry_load_test(含新增的 2500 mm 出口直钉)
#    -testFilter "DaYiJingCheng.Tests.LevelTool.world_geometry_load_test"
# 实跑:total=15 passed=15 failed=0(改前 14)
```

---

## 五、回归三元组(S12-0 口径)

| 套件 | 截至时刻 | 批次 | commit | 结果 | 对照 |
|---|---|---|---|---|---|
| EditMode | 2026-10-29 11:41 CST | 全量(无 filter) | `ca8ed516` + 工作树 | **`5869 / 5832 / 0 / 0 / 37`**(exit 0) | 见下等式 |
| PlayMode | 2026-10-29 11:41 CST | 全量(无 filter) | `ca8ed516` + 工作树 | **`238 / 234 / 0 / 2 / 2`**(exit 2) | 与基线**逐位相同** |

**EditMode 等式(不是照抄旧数)**:`5860 + 2 + 7 = 5869`

- `5860` = 最近一次全量实测 @ 2026-10-29 09:33 CST · `2236bf8a` + 工作树;
- `+2` = `885ac990`(2026-10-29 11:20)给 `intensity_bucket_selection_test` 补的性别直钉
  —— 复算:`git show 885ac990^:…/intensity_bucket_selection_test.cs | grep -c "\[Test\]"` 实测 **3**,
  现文件 **5**;
- `+7` = 本轮新增:`audio_lattice_source_test` **6** + `world_geometry_load_test` **1**。
  复算:`git show HEAD:<file> | grep -c "\[Test\]"` 对新文件 = 0 / 旧文件 = 14。

**PlayMode 的两条 Inconclusive 是既有的具名占位**,不是新失败
(`U1Assump6GamepadFocusSpikeTest.…_notRun`,方法名自带 `_notRun`)。exit 2 与基线同形,
**不写成绿、也不写成红**。

---

## 六、本轮踩到的坑(如实登记)

**Unity 只认最后一条 `-testFilter`。** 首跑我一次传了三个 filter,实跑只执行了最后一个
(`SpatializationWiringTest` 11/11),另外两个夹具**根本没跑** —— 而 XML 是 `total=11 failed=0`,
看上去全绿。**这是「假绿」的又一种形态:过滤器被静默折叠,`total` 不为 0 所以连
`FILTER-MISS` 守卫也拦不住。**
⇒ 纪律:**多夹具一律逐条串行跑**,或在跑前先断言「XML 里出现了期望的测试名」。

---

## 七、本轮**不**证(边界,防读成「空间化已完整」)

| 项 | 状态 | 归属 |
|---|---|---|
| `LATTICE_SIZE` 的**数值**是否正确 | **NOT-RUN / 归用户数值轮** | `P0ClinicLevel.LatticeSizeMm` 自陈「占位值,归用户数值轮」 |
| ② 遮挡真 raycast | **open** | 且本轮查出**缝形状不可用**:`Func<bool> occludedFor` 无 cell / 无 listener 入参、每次 Spawn 只求值一次 ⇒ 须先改签名 |
| ③ `priority_class` / `min_distance` / `max_distance` 表列承载 | **open(且原文前提有误)** | `priority_class` 全表零行零门;`min/max_distance` 仅 `WorldBreath_Baseline` 一行 |
| 「启动序真的跑了 `ApplyLatticeFromWorldGeometry`」 | **论证,非观测** | 「写在 `BootRoot` 里且在 `Assemble` 之前」由源文本门(`test_bootRoot_appliesLattice_beforeAssemble`)钉;今日该值为 `1000 mm = 1.0 m`,与旧兜底常量同值 ⇒ **值断言在 PlayMode 上无判别力**,不给假证据 |
