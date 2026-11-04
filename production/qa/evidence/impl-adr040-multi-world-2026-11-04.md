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
2. **真启动序端到端无 PlayMode 判据** —— 世界列表在生产路径(真目录)上没有任何 PlayMode 覆盖;
   `main_menu_wiring_test` 走的是**夹具缝**(`SaveBasePathOverride` 非空),那条路径逐字未变。
   所以「玩家真的点了『新起一局』并进了新世界」这件事,**今天没有一条自动判据**。
3. **`ReassembleForWorld` 无直接夹具** —— 它的两个构件(目录解析、换目录后各世界互不影响)都测了,
   但「菜单选择 → 重装配」这一跳没有。
4. **排序语义 = 「最后打开」**,不是「最后保存」;`Touch` 只在开局/换世界时写。
5. **`OQ-MM-5` 的「退出」一项仍未裁**;「设置」已在暂停壳第三行。
6. **规格修订未经 `/ux-review` 复评** —— `main-menu-42.md` 原为 Approved,本轮就地改写,
   `AC-MM-11/12/13` 三条为新增。
7. **`main_menu_static_test` 的「UI 零存档目录知识」仍成立**:42 侧只收 `MainMenuWorldView`,
   不 import `Directory` / 路径。
