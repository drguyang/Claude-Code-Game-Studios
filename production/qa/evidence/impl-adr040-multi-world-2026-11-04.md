# ADR-040 D 案实现轮 —— 多世界存档(目录 / 索引 / 主菜单世界列表 / 新起一局)

> **日期**: 2026-11-04 · **sprint-24** · commit 基线 `25d0a361` + 工作树
> **触发**: 用户「按 D 案开工吧」(范围裁定 = **一路做到 UI 可点**)。
> **ADR 状态**: `Proposed` → **`Accepted`**(方向由该指令裁定;细项仍为就地拟定)。
> **判据面**: 每条给夹具名;数字带三元组。

---

## 1. 做成了什么(一句话)

**玩家现在可以在主菜单上看到自己的每一个世界、选一个继续、或者开一局新的。**
存档体格式**一字节未改** —— 分离靠目录,不靠存档头字段(`SaveVersion` 仍是 5,零迁移链)。

```
<savesRoot>/worlds.index        ← 索引(元数据:每个世界的最后打开时刻)
<savesRoot>/w1/save_exit.bin    ← 世界 1 —— 世界内的槽位语义一字未改
<savesRoot>/w1/save_auto.bin
<savesRoot>/w2/…
```

---

## 2. 存储层 `Gameplay.Boot/WorldCatalog.cs`(D1 / D2 / D3 / D7)

| 能力 | 契约 |
|---|---|
| `MigrateLegacy()` | 旧形态(存档直接躺在根目录)→ 整体搬进 `w1/`;**字节零改动**(不是格式迁移,不走 `ISaveMigration`);幂等;无关文件不动 |
| `ListWorlds()` | **目录是权威** + 索引提供时刻;按时刻降序;孤儿索引条目不列,无索引的目录照列(时刻 0) |
| `CreateWorld()` | 分配下一个序号、建目录、写时刻;**不写存档**(首份存档仍归 `FirstSessionSave`) |
| `Touch(seq, ticks)` | 记「最后打开」;**原子写**索引(tmp → 覆盖) |
| `TryParseWorldDir` | `w<正整数>` |

**为什么住 `Gameplay.Boot` 而不是 `Sim.Codec`(D8 的裁定依据,实测)**:
`SaveWritePathGate`(`Editor.Tools.Gates/SaveWritePathGate.cs:53-62`)只允许 **`SaveService` 一个类**
在 `Sim.Codec` 里碰写盘 API,方法级只放行 `WriteAtomic`;`Directory.CreateDirectory` 也只有类级豁免。
本类要建目录 / 搬文件 / 写索引 ⇒ 放进 `Sim.Codec` **必撞门**。
索引是**元数据不是存档体**,故不受「存档体只有一个写入口」约束;但它自己仍做原子写。

**夹具** `world_catalog_test.cs` **8 条**(`world_catalog` 命名空间内):
迁移字节不变 / 迁移幂等 / 按时刻排序(**序号与时刻刻意反向**) / 目录权威(索引孤儿 + 无索引目录) /
索引损坏不丢世界且下次重建 / 新建世界分配序号且成为最近 / 空根 / 目录名解析。

---

## 3. 装配层(`BootRoot.cs`)

| 位置 | 改动 |
|---|---|
| **1.9**(装配之前) | `ResolveWorldSaveDir()`:夹具缝 ⇒ 该目录即世界目录;生产 ⇒ `persistentDataPath/saves` 作根 → 一次性迁移 → 取**最近且有档**的世界;无世界 ⇒ `w1/` |
| **2.96**(菜单之后) | 选择不同世界 ⇒ `ReassembleForWorld()`:Dispose 旧袋 → 装新袋 → **重绑退出钩子 + 设备态读数** |
| 主菜单 | 世界列表(仅有档的世界)+ `SelectWorld` / `NewWorld` 两个意图;`ShowSceneLevelScreen(..., worlds)` |
| 时刻 | `TouchWorld()`:开局选定世界时、换世界后各记一次(ADR-040 D4 的选择面) |
| **守卫** | 只有 `_bootWithMainMenu == true` 才启用多世界与迁移 —— 生产(Boot.unity)恒 true;代码构造的 PlayMode 夹具恒 false |

⚠️ **守卫的理由是实测风险,不是洁癖**:代码构造的 PlayMode 夹具会走**真实** `persistentDataPath`;
若在装配期无条件迁移,跑一次测试就会搬动开发机上的真存档。

---

## 4. UI(42 侧)

`MainMenuScreen` 由「一张纸、一行」改为:

| 世界数 | 行 |
|---|---|
| 0 | 1 行「开始诊治」(与旧形态**逐字相同**),确认 = `NewWorld` |
| N ≥ 1 | N + 1 行:第一行「继续诊治」(`Continue`)、其余「第 N 局」(`SelectWorld(seq)`)、末行「新起一局」(`NewWorld`) |

- `IMainMenuCommandSink` 增 `SelectWorld(int)` / `NewWorld()`;`Continue()` 保留(= 最近世界)。
- **42 不拼文案**:世界行的文字由装配层给(`MainMenuWorldView.Label`),数据来源仍是 7a 的目录表。
- 规格 `main-menu-42.md` 已就地修订(§5 / §6 / §13 / §14),`OQ-MM-5` 的「新开一局」据此裁为**做**。

**夹具** `main_menu_worlds_test.cs` **4 条**:无世界单行 + 新起 / 多世界行数与路由(断 sink 收到的
**调用与参数**,不是「行存在」)/ 重画不留旧行 / 越界确认无操作。

---

## 5. 世界隔离(`world_switch_isolation_test.cs` · 1 条)

两世界各装配一次 → 在**新**世界写首份存档 → **旧世界逐文件 SHA-256 不变**;两个世界各自可 `Load`。
这是「新起一局 = 新建目录,不是清空旧的」的机械守(ADR-040 D5 / D6 · `AC-MM-13`)。

---

## 6. 回归(三元组)

**截至** `2026-11-04T05:56:44Z` · **批次** sprint-24 多世界轮 · **commit** `25d0a361` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `6855 / 6818 通过 / 0 失败 / 37 跳过` | 上轮 `6842/6805/0/37` ⇒ **+13**(8 目录 + 4 屏 + 1 隔离),零失败 |
| PlayMode | `332 / 328 通过 / 0 失败 / 2 不确定 / 2 跳过` | 上轮**同值** ⇒ 零回归 |

**中途一条红,已修**:`world_assembly_wiring_test.test_ac201b_...` 断言
`CompositionRoot.Assemble` 的实参逐字是 `world: world`;装配调用抽成 `AssembleServices()` 后
实参变成 `world: _bootWorld`。**改判据而非删断言**,并补反向腿:
`_bootWorld = world;` 必须存在(否则「传给 Assemble 的是个空世界」仍会溜过)。


---

## 7. 如实登记(不借绿)

1. **删除世界(ADR-040 D6)零实现** —— 菜单没有删除行。世界只增不减。`TD-persist-20` 维持 `partial`。
2. ~~**真启动序端到端无 PlayMode 判据**~~ —— **已于同日补上**(§9)。
   原文理由保留作档案:世界列表在生产路径(真目录)上没有任何 PlayMode 覆盖;
   `main_menu_wiring_test` 走的是**夹具缝**(`SaveBasePathOverride` 非空),那条路径逐字未变。
3. ~~**`ReassembleForWorld` 无直接夹具**~~ —— **已于同日补上**(§9.2 A/B 两条都穿过它;
   而且**夹具先红**,抓到它的一处生产缺陷:**二次应用开档基线 ⇒ 换世界一点就崩**,见 §9.3)。
4. **排序语义 = 「最后打开」**,不是「最后保存」;`Touch` 只在开局/换世界时写。
5. **`OQ-MM-5` 的「退出」一项仍未裁**;「设置」已在暂停壳第三行。
6. **规格修订未经 `/ux-review` 复评** —— `main-menu-42.md` 原为 Approved,本轮就地改写,
   `AC-MM-11/12/13` 三条为新增。
7. **`main_menu_static_test` 的「UI 零存档目录知识」仍成立**:42 侧只收 `MainMenuWorldView`,
   不 import `Directory` / 路径。

---

## 8. 同日补做 —— 删除世界(ADR-040 D6)

**触发**:用户指令「接着做删除世界」。

### 8.1 交互形态(**只用 Submit 一路输入**)

不新增输入动作、不新增模态屏(`ModalId` 闭集不动):

| 态 | 行 |
|---|---|
| 正常 | 世界行 + 「新起一局」+ **「删一局」**(仅当有世界) |
| 删除态 · 选目标 | 每世界一行「删除 第 N 局」+ 「返回」 |
| 删除态 · 待确认 | 选中那行变「**再按一次确认 · 第 N 局**」;按别的世界 = 换目标(**仍不删**) |

⇒ **二次确认的本体是「两次都落在同一个世界上」**,不是「按两次任意键」。

### 8.2 实现期抓到的一处真缺陷(不是补测试,是改代码)

首版 `DeleteWorld` 删完**同时删掉索引条目**;而 `CreateWorld` 按「现有目录最大序号 + 1」发号
⇒ 删掉 `w2` 之后新建会**复用 2**。任何还记着「世界 2」的地方就会指向一个完全不同的世界。
**改法**:索引条目保留成**墓碑**(`ListWorlds` 只列目录 ⇒ 墓碑不上菜单),
`CreateWorld` 的高水位同时看**目录与索引** ⇒ 序号**只增不减**(ADR-040 D2)。
⚠️ 这条是测试先红(`Expected: 3 But was: 2`)逼出来的 —— 我原来的断言就写着「序号不复用」,
而当时的实现做不到。

### 8.3 装配层

- 删完 ⇒ `_uiDriver.SetMainMenuWorlds(BuildWorldRows())` 回推新列表(屏自动退出删除态);
- **删的正是当前装配用的目录** ⇒ `ReassembleForWorld(ResolveDefaultWorldDir(...))` ——
  否则 `SaveService` 的 `basePath` 指向不存在的目录,**下一次保存会把它原地重建**;
- 重装配时同步刷新 `_continueSlot`(否则启动序末会拿上一个世界的槽去读档)。

### 8.4 夹具

| 文件 | 条数 | 判什么 |
|---|---|---|
| `main_menu_worlds_test` | **+5** | 「删一局」只在有世界时出现 · 两次确认必须同一世界 · 换目标不删 · 「返回」不删 · 新列表让删除态作废 |
| `world_catalog_test` | **+2** | 目录真的没了 + 别的世界不受影响 + 序号不复用 · 删不存在的世界返回 false |

### 8.5 仍未做

- **装配层的「回推列表 / 删当前世界时重装配」无直接夹具** —— 两个构件(目录删除、重装配)各有侧面覆盖,
  但这一跳没有。与上一节第 2 条同源:**真启动序仍无端到端**。
- 删除**不可逆**,且没有任何「回收站」—— 这是 D6 的刻意选择(不软删),不是遗漏。

### 8.6 回归(删除轮 · 三元组)

**截至** `2026-11-04T06:12:06Z` · **批次** sprint-24 删除世界轮 · **commit** `3b3fbf11` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `6862 / 6825 通过 / 0 失败 / 37 跳过` | 上一轮 `6855/6818/0/37` ⇒ **+7**(屏 5 + 目录 2),零失败 |
| PlayMode | `332 / 328 通过 / 0 失败 / 2 不确定 / 2 跳过` | **同值** ⇒ 零回归 |

---

## 9. 真启动序轮(2026-11-04 · 用户指令「补真启动序的 PlayMode 判据」)

### 9.1 补的是哪一跳

此前「世界列表走**真目录**」在生产路径上**没有任何 PlayMode 覆盖**:
`main_menu_wiring_test` 走的是夹具缝(`SaveBasePathOverride` 非空 ⇒ 那个目录**直接当世界目录**,
单世界形态),而 `world_catalog_test` / `main_menu_worlds_test` / `world_switch_isolation_test`
全部**绕开启动序**。⇒「玩家真的点了『新起一局』并进了新世界」这件事,当时**一条自动判据都没有**。

新缝 `BootRoot.SaveRootOverride`:把**存档根**指到临时目录(根下照常是 `w1/`、`w2/`)。
⚠️ 它与 `SaveBasePathOverride` **不是一回事**:后者把某个目录**直接当世界目录**。
新件刻意**不设**后者 —— 否则测的仍是单世界路径,等于没测。

### 9.2 两条判据(`unity/Assets/Tests/PlayMode/multi_world_boot_wiring_test.cs`)

| 测试 | 断言 |
|---|---|
| `test_newWorld_createsOwnDirectoryAndSave_oldWorldUntouched` | 只种 `w1/`(含一份档)⇒ 菜单 3 行且第一行是「继续诊治」(ADR-040 V-7)→ 点「新起一局」→ `w2/` **真的建出来**、`w2/save_auto.bin` **真的落盘**(`FirstSessionSave` 的 t=0 实例)→ `w1` 的档 SHA-256 不变、**存档文件集快照**(文件名 + 每份字节)完全不变 |
| `test_deleteWorld_throughBoot_removesOnlyChosenWorld` | 种 `w1`+`w2`(时刻刻意反向)⇒ 4 行、最近世界排第一 → 「删一局」→ 删除态 3 行 → 第一次按 `w2` **不得删** → 再按同一行 ⇒ `w2/` 真没了、`w1` 存档文件集逐字节不变、菜单回到正常态且 3 行 |

⚠️ **为什么等文件而不是等帧**:7a 的 `Checkpoint` 入队后由写线程落盘,`_booted == true`
不代表文件已在盘上 ⇒ 夹具轮询文件(`WaitForFile`),否则是偶发假红。

### 9.3 ⚠️ 夹具先红,逼出一处**生产缺陷**(不是补测试,是改代码)

**症状**:两条夹具首轮全红。`test_deleteWorld_…` 直接抛:

```
System.InvalidOperationException : `ApplyBakedInitial` 已应用过一次 —— 基线只在开档时点应用一次;
重新回到基线请用 `Reset()`(它清空后按序重建,不会与既有实例撞号)
  at DaYiJingCheng.Sim.World.World.ApplyBakedInitial (World.cs:177)
  at DaYiJingCheng.Gameplay.Boot.CompositionRoot.AssembleCore (CompositionRoot.cs:1439)
```

**根因**:`ReassembleForWorld`(`BootRoot.cs:1392`)换世界时拿**同一个** `World` 实例
重跑 `CompositionRoot.Assemble`,而开档基线 `ApplyBakedInitial` 是 fail-closed 的「只许一次」
⇒ **换世界 / 新起一局 / 删当前世界,玩家一点就崩**。这不是测试环境问题:
`world` 由 `MinimalWorldFactory.Create` 在启动序只建一次,重装配必然复用它。

**修法**(`CompositionRoot.cs`,装配层一处):

```csharp
if (world != null && world.BaselineModuleCatalog == null)
    world.ApplyBakedInitial(modulesCatalog);
```

- 基线是 `Overlay(0)` 的**派生态**,重装配**不该**再叠一遍;sim 那时还没开跑,状态原样;
- **守卫不放松**:`World.ApplyBakedInitial` 的直接调用者仍然 fail-closed
  (`world_buildslots_bake_test` 的「只许一次」夹具逐字未改,仍绿);
- 既有「经生产装配应用基线」夹具用**全新** `World`(`BaselineModuleCatalog == null`)⇒ 行为不变。

### 9.4 证伪轮(两次,第一次如实作废)

| 轮 | 变异 | 结果 |
|---|---|---|
| **M1(作废)** | `NewWorld` 回调里把 `_pendingWorldSaveDir` 指回**当前**世界目录 | 红了,但**靶错**:报的是「继续诊治读档失败(存档损坏)」—— 流程从读档支就崩了,**没走到我那条断言**。按「变异打错靶」纪律不计 |
| **M2(有效)** | `NewWorld` 回调里往**旧世界目录**写一份 `save_92.bin`(不改流程) | **红在我自己的断言**:`旧世界的**存档文件集**必须完全不变(文件名 + 每份字节)`,差分显示多出 `save_92.bin` |

M2 同时暴露了首版断言的弱点并已修:原断言只哈希**种下去的那一份**档,
抓不到「往旧世界多写一份」⇒ 改成**存档文件集快照**(`SnapshotSaveFiles`:文件名排序 + 每份 SHA-256),
A/B 两侧都用它。`session_incomplete.flag` 刻意不在快照内 —— 它在世界**被选中装配**时由 7a 读写,
属于「这个世界自己的状态」,不是「碰到了别的世界」。

还原 = **SHA-256 逐字节**(`BootRoot.cs` `dc3a53d6…`,变异前后同值),`grep -c MUT-FALSIFY` = **0**。

### 9.5 回归(三元组)

**截至** `2026-11-04T06:36:54Z` · **批次** sprint-24 真启动序轮 · **commit** `5590b8c5` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `6862 / 6825 通过 / 0 失败 / 37 跳过` | 上轮 `6862/6825/0/37` ⇒ **同值**,零回归(本轮无 EditMode 新增件) |
| PlayMode | `334 / 330 通过 / 0 失败 / 2 不确定 / 2 跳过` | 上轮 `332/328/0/2/2` ⇒ **+2/+2**,恰为新增两条 |

等式:`334 − 332 = 2` = 本文件 `[UnityTest]` 数。

### 9.6 仍未覆盖(如实登记,不借绿)

1. **一次性迁移走真启动序**(`MigrateLegacy`):新件种的是 `w1/`,根目录无旧档 ⇒ 迁移是 no-op。
   迁移本体由 EditMode `world_catalog_test` 覆盖(字节不变 / 幂等 / 无关文件不动)。
2. **在菜单上选「另一个世界」并读档**(`SelectWorld` 支):新件只走 `NewWorld` 与 `DeleteWorld`。
   重装配的共性已被 A 覆盖(换目录 + 首份存档),但「选中后读旧档」这一跳无真启动序判据。
3. 本件**不**断存档字节级正确 —— 种子档是故意非法的字节,只让「这个世界有档」成立。

---

## 10. 视觉走查(2026-11-04 · 用户指令「实际去看看目前的存档和启动界面是否符合视觉预期,注意 console 有无红色报错」)

### 10.1 怎么看

新件 `unity/Assets/Tests/PlayMode/SkeuomorphicUI/multi_world_visual_walkthrough.cs`:
**图形模式**实跑(`DISPLAY=:0`,**不带** `-batchmode`/`-nographics` —— 后者下 `WaitForEndOfFrame`
永不触发、零截图),1920×1080 RenderTexture,五帧落 `unity/Logs/shots/mw_*.png`。

```bash
DISPLAY=:0 ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -projectPath "$(cd unity && pwd -P)" -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI.MultiWorldVisualWalkthrough" \
  -testResults "$(pwd)/unity/Logs/playmode-mw-visual.xml" \
  -logFile "$(pwd)/unity/Logs/playmode-mw-visual.log"
```

⚠️ 比例可读:`PanelSettings.asset` = `ScaleWithScreenSize` + 参考 1920×1080 + Match 0.5
⇒ **1080p 下缩放因子 = 1**,截图与玩家所见 1:1(走查里设 `ConstantPixelSize/1` 是为了不受 Game 视图尺寸影响)。

### 10.2 五帧判读

| 帧 | 内容 | 判读 |
|---|---|---|
| `mw_01_menu_two_worlds` | 真启动序 · 两世界 | ✅ 四行「继续诊治 / 第 1 局 / 新起一局 / 删一局」,与 `AC-MM-11` 逐字一致;首行黄铜焦点环;世界来自**真目录**(日志 `rows=4`) |
| `mw_02_delete_select` | 删除态 · 选目标 | ✅ 三行「删除 第 2 局 / 删除 第 1 局 / 返回」 |
| `mw_03_delete_confirm` | 删除态 · 待确认 | ✅ 选中行变「再按一次确认 · 第 2 局」,位置不变 |
| `mw_04_saveslots_fresh` | 存档册 · 新局 | ✅ 页框 / 两行有档 / 一条空纸线 / 「留档」/ 页码 `1/1` |
| `mw_05_saveslots_manual` | 存档册 · 留过手动档 | ✅ 三行有档 + 空槽 + 留档;焦点在「留档」(= 生产写完一档后的形态) |

**纸面宽 480px 是 spec 取值**(`--skeuo-shared-modal-width @1680`),不是缩放事故。
**主菜单那张纸没有页框、存档册有** —— 这是已登记的 `TD-skeuo-09`(只有存档册挂了 `.paper-page`),
**不是本轮新问题**。

### 10.3 控制台:零红错

`grep -c "\[Error\]" unity/Logs/playmode-mw-visual.log` ⇒ **0**;两轮走查 `2/2 passed`
(UTF 遇到未处理 `LogError` 会直接判红 ⇒ 这是机械判据,不靠肉眼扫日志)。
日志里仅两条非红:`Licensing::Module Error: Access token is unavailable`(Unity 许可客户端,环境噪音)、
`Editor.Tools.Kindgen.asmdef will not be compiled, because it has no scripts`(**既有黄字**)。

### 10.4 ⚠️ 走查抓到一处缺陷:进删除态后**焦点悬空**(已修)

**现象**:第 2、3 帧的黄铜焦点环消失。走查里把焦点对象打出来,**由「看图猜」变成事实**:

```
[MW] focus/menu-shown     = name=main-menu-world-2 text=继续诊治
[MW] focus/delete-select  = <null>          ← 修复前
[MW] focus/delete-confirm = <null>          ← 修复前
```

**根因**:`MainMenuScreen.Rebuild()` 拆掉全部行重建,焦点挂在**旧元件**上;驱动只在**开屏**时
聚焦 `FirstFocusable`,`SetWorlds` 回推列表也不聚焦 ⇒ 修复前该文件 `Focus(` **零次**。
**后果**:无指针形态下 Submit 依赖焦点 ⇒ 手柄/键盘玩家按开「删一局」后**一步也走不了**
(因果在本仓 `SkeuoRuntimeDriver.OnTickEdge` 的注释里已写过:「焦点悬空,一步也走不了」)。
鼠标点仍可用,所以屏级 EditMode 夹具(只断文案与 sink 参数,且无面板 ⇒ `Focus()` 是空操作)全绿也抓不到。

**修法**(`MainMenuScreen.cs`):新增 `_focusIndex`(FocusIn 记录)+ `RestoreFocus()`(重建后落回,夹到行数内):
- 进删除态 ⇒ 第一个世界(它是下一步要选的目标);
- 进待确认 ⇒ **同一个世界**(不能沿用索引:鼠标点进来的行未必获焦);
- 装配层回推列表 ⇒ 首行。
`Fake` 算法一点没加 —— 仍是「落回某一行」,焦点移动归引擎 `FocusController`(ADR-013 §五)。

**机械判据**:`multi_world_boot_wiring_test` 的三条焦点断言(真启动序 + 真面板)。
**证伪**:把 `RestoreFocus()` 变空操作 ⇒ 新判据**红在自己那句**
「进删除态后焦点必须落在**第一个世界**上」;还原 SHA-256 逐字节一致(`a037c282…`),零 `MUT-` 残留。

⚠️ **一处「看似同样、其实不是」**(如实区分,避免误报):第 5 帧在首轮也掉过环,但那是**走查绕过**了
`OnTickEdge` 里生产必做的那次重落焦(`WritableRow.Focus()`)。本轮已让走查补同一步 ——
现在那一帧等于玩家真实所见,不是缺陷。

### 10.5 回归(三元组)

**截至** `2026-11-04T07:03:24Z` · **批次** sprint-24 视觉走查轮 · **commit** `781d5ef8` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `6862 / 6825 通过 / 0 失败 / 37 跳过` | 同值,零回归 |
| PlayMode | `336 / 332 通过 / 0 失败 / 2 不确定 / 2 跳过` | 上轮 `334/330` ⇒ **+2/+2**(走查件两条) |

等式:`336 − 334 = 2` = `MultiWorldVisualWalkthrough` 的 `[UnityTest]` 数。

---

## 11. 视觉走查 · 第二眼(2026-11-04 · 同一指令再走一遍,换成**真 7a 数据**)

### 11.1 这一轮比上一轮多看的两帧

上一轮的存档册是**固定视图**(我手搓 `SaveSlotView` 喂给屏)。这一轮补两帧走**真数据**:

| 帧 | 路径 | 判读 |
|---|---|---|
| `mw_06_menu_first_run` | 空存档根 ⇒ 首次启动 | ✅ 单行「开始诊治」+ 焦点环(与 spec §6 一致) |
| `mw_07_saveslots_realboot` | 空根 → 「开始诊治」→ 启动序跑完 → **生产 provider** 开册 | ✅ 册上三行:槽 1 空(退出槽还没写过)、槽 2「贰 · 第壹日 夜」(首份自动存档)、槽 3 空 + 「留档」+ `1/1` |

⇒ 「存档界面在**真数据**下也是对的」这件事,这轮才有画面证据。

### 11.2 ⚠️ 抓到第二处缺陷:**全新档的第一局被编号成「第 2 局」**

`mw_07` 首跑直接**红了** —— 不是像素问题,是存档落点:

```
Assert: 新局的首份存档须落盘 —— 本节要看的就是**真数据**下的册子   Expected: True  But was: False
[BootRoot][7a][ADR-040 D5] 切换世界目录:.../w1 → .../w2
```

全新档(空根)本来只有**一个**世界,而它被建成了 `w2/`,`w1/` 永远不存在;玩家看到的世界叫「第 2 局」。

**两个来源叠在一起**(缺任一个都不发作,故两条都得修):

1. 启动序 1.9 装配 `SaveService` 时,**构造器会建目录**(`SaveService.cs:222`)⇒ 默认世界路径 `w1/`
   被建出来,但一件东西都没写;
2. `BootRoot.TouchWorld(默认世界)` 给这个**还不存在**的 `w1` 写了一条 `worlds.index` 记录。

而 `WorldCatalog.CreateWorld` 的高水位把「目录」与「索引键」都当已用序号 ⇒ 下一个是 2。

**修法**:
- `WorldCatalog.CreateWorld`:目录高水位**只算已成形**的世界(`HasWorldContent` = 目录里至少有文件);
  索引键仍然**全部**计数(墓碑必须保住号,ADR-040 D2 不变)。
- `BootRoot.TouchWorld`:目录不存在 / 目录还是空的 ⇒ **不记「最后游玩」**(它还不是一个世界)。

**判据**:
- `multi_world_boot_wiring_test.test_firstRun_newWorldLandsInW1_notW2`(真启动序:空根 ⇒ 第一局在 `w1/`、`w2` 不存在、首份存档落在 `w1/`);
- `world_catalog_test.test_createWorld_emptyDirectoryDoesNotConsumeSeq`(目录级:空目录不占号、第一局就落 `w1`);
- 既有 `test_createWorld_allocatesNextSeq_andBecomesMostRecent` 同步补上「目录里要有档」的前置
  —— 它原来喂的是**空目录**,断言的语义在新规则下不再成立(这正是「夹具随语义更新」而非「改判据迁就实现」)。

**证伪(两条防线各打一次)**:

| 变异 | 结果 |
|---|---|
| M1b:`HasWorldContent` 恒 `true`(空目录也算已用) | 红在「不得跳到 w2」 |
| M2:`TouchWorld` 不拦未成形世界 | 红在「不得跳到 w2」 |

⚠️ **M1 首枪作废**:变异写坏了语法(把 `try` 换掉却留下 `catch`),那一轮是**编译失败**,
读到的 XML 根本不是我这条测试 —— 按「变异打错靶」纪律重打为 M1b。
还原 = SHA-256 逐字节(`WorldCatalog.cs` `9c4b1823…` / `BootRoot.cs` `853f631f…`),零 `MUT-` 残留。

### 11.3 观察到但**本轮不修**的一处外观问题(已立债)

`mw_07` 的左上角出现「止血 / 通气 / 跳过」三行,其中「跳过」是一条**横贯 1920px 全屏的米色纸带**。
- **位置本身是 spec 认过的**:`design/ux/pause-shell-42.md:342` 逐字「左上角三行 止血 / 通气 / 跳过
  是**急救候选列表**(游戏内容),不计入」;
- **但宽度没有契约**:`EmergencyCandidateList.Root` 是裸 `VisualElement`(零类、零定位),
  被直接 `Add` 到 UI 根 ⇒ UI Toolkit 默认 `align-items: stretch` 把它撑满屏宽;
  而候选项没有背景、只有 `.skip-entry` 有纸底 ⇒ 画面上就是「两条贴在深色背景上的文字 + 一条通栏纸带」。
- ⇒ 登记 **`TD-skeuo-13`**(形态待裁),**不在本轮擅改**(要不要给列表一个容器宽 / 贴哪一侧,是设计面)。

### 11.4 控制台

`[Error]` 计数 **0**;走查 `3/3 passed`(未处理 `LogError` 会判红)。噪音仍是那两条(许可客户端 / Kindgen asmdef)。

### 11.5 回归(三元组)

**截至** `2026-11-04T10:01:32Z` · **批次** sprint-24 视觉走查第二眼 · **commit** `57a3ac9e` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `6863 / 6826 通过 / 0 失败 / 37 跳过` | 上轮 `6862/6825` ⇒ **+1**(目录级新判据),零回归 |
| PlayMode | `338 / 334 通过 / 0 失败 / 2 不确定 / 2 跳过` | 上轮 `336/332` ⇒ **+2/+2**(首次启动走查 + 世界落点判据) |
