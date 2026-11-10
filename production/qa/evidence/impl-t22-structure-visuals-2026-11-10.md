# T22 批一 —— 建造槽位的最小结构视觉(`TD-building-05` 躯干)证据原件

**日期**:2026-11-10
**批次**:T22 批一(用户指令「开工 T22」,四选全取;批一只做「甲 · 结构最小视觉」)
**构建**:`unity/builds/Linux-x64/大医精诚.x86_64`,Development,**2026-11-10 19:49**(本地)产出
  (`PlayerBuildCli.BuildDevelopment` → `Build Finished, Result: Success`,32.4 s,206,766,102 B)
**运行**:`DISPLAY=:0`,1280×800,`-tutorialdiag`;存档根 = `~/.config/unity3d/DaYiJingCheng/大医精诚`
  (走查前原 `saves/` 移开 → 走查后原样移回;走查存档留 `saves.t22-structvis/`)

---

## 1. 病因与解除判据(原文照抄债册,避免走样)

`TD-building-05`:`World.unity` 只有 `Ground` + `Directional Light`,运行期**唯一**被实例化的
3D 物体是**病人胶囊** ⇒ 灶台 / 墙 / 门 / 柜 / 医馆面板**没有任何可视载体** ⇒
18 / 20 / 23 / 24 的入口**全部不可发现**(用户原话:「我找不到」)。

解除判据(债册原文两条):
> ① 真包走到 `(10,0,5)` 能看见一个可辨识体块;② `AC-18-21` 端到端人工腿跑通。

⚠️ **① 里的格号已过期**:`world_buildslots.json` 在 2026-11-06 空间解之后把灶台迁到了
**(14,0,14)**(本批实测 `14,0,14` 的 `slot_type = Utensil`)。本轮按**真格**走查,
不按债册里那个旧号。

---

## 2. 落地

| 层 | 文件 | 做什么 |
|---|---|---|
| 视觉形态 | `Gameplay.Presentation/Building/StructureVisualFactory.cs`(新) | 位掩码 → `SlotType` → **尺寸 + 色调**;`PrimitiveType.Cube`,摘 Collider;材质取 `Resources` 那张**病人共用的**占位材质(不做 `Shader.Find` —— `TD-build-03` 的教训:运行期手搓材质 ⇒ 打包版着色器被剥离) |
| 生成器 | `Gameplay.Presentation/Building/StructureVisualSpawner.cs`(新) | 按 `WorldBuildSlotsDataSet` **一次性**铺;跳过空槽;幂等(先清后建);根节点**懒建** |
| 装配 | `Gameplay.Boot/CompositionRoot.cs` | 只在 `dataProvider is AddressablesDataProvider` 时铺(与 17 采集块同一道守卫) |
| 服务袋 | `Gameplay.Boot/CompositionRootServices.cs` | 可选形参 + 属性(null = 本次装配没装世界数据) |
| 清理 | `Gameplay.Boot/BootRoot.cs` | `UnloadWorldSceneForMenuAsync` 与 `OnDestroy` 两处 `DestroyAll` |

**两条设计判断(不是顺手写的)**:

1. **空槽不画**(`BakedInitial == false` 跳过)。4 的选择器读的是**同一个** `baked_initial`;
   给它画个体块 = 「看得到的」与「选得中的」分叉。真产物 18 行里**恰 9 行**真站着东西
   (8 个空装饰位 + 1 个空帐篷位)。
2. **可辨识靠尺寸,不靠颜色**。类型之间在 Y/Z 上确有差异(墙薄高、灶台方矮、开关最小),
   灰度下也分得出;色调只是次要通道(承 `accessibility-requirements.md` 的「不以色单载」)。

---

## 3. 判据 —— `structure_visual_spawner_test`(7 条)

| # | 测试 | 钉的性质 |
|---|---|---|
| 1 | `test_slotTypeOf_picksLowestSetBit` | 位掩码取**最低位**;零掩码 ⇒ `None`,不抛 |
| 2 | `test_scale_differsAcrossSlotTypes` | 类型之间缩放**确有差异**(墙薄于灶台、灶台矮于面板…) |
| 3 | `test_spawnAll_createsOnePerBakedRow_atCellPosition` | 一个 baked 行一个体块;**位置 = 格、贴地(半高)** |
| 4 | `test_spawnAll_skipsUnbuiltSlot` | **空槽不画**,且此时**连根都不建**(负控) |
| 5 | `test_spawnAll_isIdempotent_noDoubling` | 重复 `SpawnAll` 不叠第二套(簿记 == 现场) |
| 6 | `test_destroyAll_removesEverything` | `DestroyAll` 连根清,簿记归零 |
| 7 | `test_realArtifact_nineBakedSlots_spawnNine` | **真产物** 18 行 / 9 baked ⇒ 9 体块;灶台在 `(14,0,14)`;**空帐篷位无体块** |

> 第 7 条读真产物(`Assets/DataCooked/world_buildslots.cooked.bytes`)是有意的:
> 内联字面量会让「表改了夹具仍绿」。夹具头注已按 `test-standards.md` 具名声明了这两处外部读。

---

## 4. 真包走查(①② 的判据面)

| 判据 | 结果 | 证据 |
|---|---|---|
| 装配真的铺了体块 | **9 个** | `p.log`:`[结构视觉] 占位体块 9 个(建造槽位骨架 + 灰盒,零美术资产)` |
| 开局就能看见墙 | ✅ | `w-00-world.png`(远处四道白色墙板) |
| 走到灶台 `(14,0,14)` 看见灶台 | ✅ | `w-01-at-stove.png`(砖红方块居中)+ `[tutorialdiag] cell=(14,0,14)` |
| **灶台可选中(18 的入口)** | ✅ | 按 `E` ⇒ 屏上出现 **「釜上无料」**(18 自己的应答,不是脉案) |
| 零异常 | ✅ | `grep -ac "Exception:" p.log` ⇒ **0** |

**「E 落到 18 而不是脉案」是本条的关键对照**:`TD-building-05` 的 2026-11-06 记录是
「绕灶台十腿按 `E`,十次全是同一张脉案」。本轮病人在 13 格外(交互半径 3),而灶台在 0 格
⇒ 一次即中。

---

## 5. 证伪轮(2026-11-10 · 窗口内不编辑被变异文件 · 还原 = SHA-256)

| 变异 | 改法 | 结果 |
|---|---|---|
| **M1** | 空槽也画(删 `if (!row.BakedInitial) continue;`) | `7` 条中 **2 红**:`skipsUnbuiltSlot` · `realArtifact_nineBakedSlots` |
| **M2** | 根节点恒建(退回懒建之前) | **1 红**:`skipsUnbuiltSlot`(其「连根都不建」断言) |
| **M3** | `CellToWorld` 不抬半高(体块中心落在地面) | **1 红**:`createsOnePerBakedRow_atCellPosition` |
| **M4** | `DestroyAll` 不清簿记 | **2 红**:`destroyAll_removesEverything` · `isIdempotent_noDoubling` |
| **M5** | 灶台缩放改成墙的(0.95,1.60,0.25) | **1 红**:`scale_differsAcrossSlotTypes` |

还原:`sha256sum -c` 两件 **成功**;`grep -rn FALSIFY` 在 `Building/` **零命中**(每轮还原后各查一次)。

**每条变异打在不同的判据上**(不是共享 token 借绿):M1/M2 打「空槽」,M3 打「位置」,
M4 打「清理」,M5 打「可辨识」。

---

## 6. 回归(三元组)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7399 / 7362 / 0 / 0 / 37` | `2026-11-10T11:56:35Z`–`11:58:41Z` | T22 批一 | `34e1da7c` + 工作树 |
| PlayMode | `376 / 371 / 0 / 2 / 3` | `2026-11-10T11:59:21Z`–`11:59:42Z` | T22 批一 | `34e1da7c` + 工作树 |

基线(T21 补轮):EditMode `7392 / 7355 / 0 / 0 / 37` · PlayMode `376 / 371 / 0 / 2 / 3`。
**等式**:`7399 − 7392 = 7`(本批新增夹具的 `[Test]` 计数);PlayMode **零变化**(本批无新 PlayMode 件)。

---

## 7. 中途抓到并修掉的两个真问题(首跑 28 红)

1. **`dataProvider != null` 的守卫写宽了** —— 首版在**任何** provider 下都
   `Load<WorldBuildSlotsDataSet>()`;夹具的 `IDataProvider` 假件只认自己那几个地址 ⇒
   装配期抛 ⇒ **25 条** DataPipeline / DiseaseSimulation 夹具转红。
   改用与 17 采集块**同一道**守卫(`is AddressablesDataProvider`)。
2. **我的夹具在全量跑里被别的测试干扰** —— 单跑 `7/7` 绿、全量跑 3 红:我用
   `GameObject.Find("StructureVisuals/…")` 找体块,可能命中**别的测试遗留**的同名根。
   改为走 spawner 自己的 `Root` 属性(`GameObject.Find` 从此不进本夹具)。

⚠️ 这两条都不是「测试挑剔」:第 1 条在生产里会**把装配整个拖死**,第 2 条是
`F3`(自指判据)/ 夹具隔离问题的同族 —— 记在此,供后来者少走一趟。

---

## 8. 债册状态(诚实口径)

`TD-building-05` → **`partial`**(`open` → `partial`,**`P0 面` 仍 `Y`**):

| 判据 | 状态 |
|---|---|
| ① 真包看见可辨识体块 | ✅ **PASS**(灶台 `(14,0,14)`,截图为证) |
| ② `AC-18-21` 端到端人工腿 | **NOT-RUN** —— 该 AC 是「**起货可读性**」(`CompleteTick` 到达后 1 tick 内器物状态可判),
本轮只证到「灶台可发现 + 可选中(「釜上无料」)」,**没有跑到起货** |

⇒ **不得**记 closed。剩余半边不是视觉问题,是 18 的**器物状态呈现**(归 18 / 美术轮,
`OQ-18-9` 的具名形态仍未裁)。

**另登记两条边界(不在本债面内,但本轮首见)**:
- **体块无碰撞**:玩家可以穿过墙 —— 有意(占位实体不吃物理,与病人胶囊同纪律)。
  「墙能穿」是 23 的物理/建造轮的事,不是本批引入的缺陷。
- 债册正文里的 `(10,0,5)` 是空间解之前的旧灶台格,已在本件 §1 注明现行值 `(14,0,14)`。
