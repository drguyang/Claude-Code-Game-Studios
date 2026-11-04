# ADR-040 — 多世界存档(一个世界一个存档目录 + 选择索引)

| Field | Value |
|-------|-------|
| **Status** | **Accepted**(2026-11-04 · 起草于同日「先立债,按 D 起草」;随后用户指令「**按 D 案开工吧**」= 方向裁定 + 开工授权,故转 Accepted。⚠️ **D1–D11 的细项仍由本轮就地拟定,未经用户逐条确认** —— 同 ADR-034 / 037 / 038 / 039 口径) |
| **Date** | 2026-11-04 |
| **Supersedes** | 无 |
| **Related** | ADR-010(存档布局与唯一写入口)· ADR-005 / ADR-006(确定性边界 · 禁墙钟进 sim)· ADR-014(烘焙数据 · 玩家构建零 JSON 解析器)· ADR-013 §9 C3(42 只渲染)· GDD `persistence-service.md` 规则六 / 规则十三 / 规则十四 · GDD `save-slot-ui.md` 规则二 / 规则三 · UX `main-menu-42.md` **`OQ-MM-5`(未裁)** · 债 `TD-persist-20` |

---

## Context

### 1. 玩家可以玩很久,但**没有办法重新开始**

「重新开始新游戏」是本品类(开放世界 · 生存建造 · 经营)的常规功能。今日游戏内**零入口**:

| 位置 | 现有什么 | 有没有「新局」 |
|---|---|---|
| 主菜单 | 一行;有档「继续诊治」/ 无档「开始诊治」 | ❌(`main-menu-42.md:250` 的 `OQ-MM-5` **未裁**) |
| 暂停壳 | 继续 / 存档位 / 调治 | ❌ |
| 存档册(7b) | 逐槽「载入」+ 末页「留档」 | ❌ 无删除 / 无清档 |

⇒ 玩家今日唯一的办法是去删 `~/.config/unity3d/DefaultCompany/unity/saves/` 里的文件。

### 2. 就算补一颗按钮,现有「最近档」判据会让它**静默失效**

`SaveService.TryGetMostRecentSave`(`Sim.Codec/SaveService.cs:276-347`)选「最近档」的键是
**档内游戏 tick 最大**:

```csharp
long bestTick = 0L;                       // :285
...
if (!found || tick > bestTick)            // :329
```

新局从 tick 0 起 ⇒ 新局写下的任何档,tick 都远小于旧局 ⇒ **下次启动仍然载入旧局**。
玩家点了「新游戏」、关了、再开 —— 回到上一个世界。这不是 UI 能绕开的:
它逼着「新局」必须先回答**「两条时间线怎么分开」**。

### 3. 现有存档模型是**单时间线的检查点册**

- `SaveSlotLedger` 只进不退;`save_exit.bin`(槽 1)/ `save_auto.bin`(槽 2)/ 手动槽 ≥ 3;
- 7b 的口径逐字:「跨会话不设锁字段……**重开游戏 = 重新开始「今天」**」(`save-slot-ui.md:39-40`);
- `session_incomplete.flag` 住在 `basePath` 里,天然属于「这个世界」。

⇒ 「重开」在今天不是缺一个按钮,是缺**「世界」这个对象**。

### 4. 现成的两个接缝,让方案可以很便宜

1. `SaveService` 的构造**已经收 `basePath`**(`CompositionRoot.Assemble(saveBasePath: …)` 一路传下来);
   `BootRoot` 另有 `SaveBasePathOverride` 供夹具改指向。
2. 2026-11-04 落地的 `FirstSessionSave`(首份存档)守卫是
   「**这个 `SaveService` 的目录里有没有档**」——分目录之后它**自动**变成「这个世界有没有档」,零改动。

---

## Decision

### D1 — 一个世界 = 一个存档目录 + 一个 `SaveService` 实例

```
<savesRoot>/worlds.index          ← 索引(见 D3)
<savesRoot>/w1/save_exit.bin      ← 世界 1:退出保存
<savesRoot>/w1/save_auto.bin      ← 世界 1:自动槽
<savesRoot>/w1/save_3.bin…        ← 世界 1:手动槽
<savesRoot>/w2/…                  ← 世界 2
```

世界内的槽位语义**逐字不变**:退出 = 1 · 自动 = 2 · 手动 ≥ 3 · 只进不退(规则十四)、
`FoldedPredicate` / `ConfigVersion` 段 / 载荷段全部不变 —— **存档体一字节不改**。

### D2 — 世界身份 = 目录序号,**不进存档体、不进 sim**

目录名 `w<seq>`(seq 由索引分配、只增不减)。它是**表现/元数据层**的量:
不进 `SaveHeader`、不进事件流、不参与任何确定性求值。⇒ **`SaveVersion` 不变、无迁移链义务**
(与「C 案:把时间线 id 写进存档头」的关键差别)。

### D3 — 索引文件 `worlds.index`:二进制元数据,由 7a 写

每项 `{ worldSeq:i32, lastPlayedUtcTicks:i64 }`(可扩列,首版就这两个)。

- **必须二进制、必须手写解析**:ADR-014 明定玩家构建**零 JSON 解析器**;
- 写面走 `SaveService.WriteAtomic` 这个**唯一写盘原语**(`AC-7a-08` 的 IL 门只罩 `Sim.Codec`,
  索引若由 7a 写就必须在同一原语上,不另开第二条写盘面);
- 索引损坏 / 缺失 ⇒ **按「只有一个世界(w1)」重建**,并在主菜单落一条非阻塞告警
  (不静默:与 ADR-037 §④「未知 ≠ 无差异」同一条纪律,但此处可安全重建 —— 目录本身还在)。

### D4 — 「最近世界」看最后游玩时刻;「世界内最近档」仍看 tick

| 选择 | 判据 | 出处 |
|---|---|---|
| 选**世界** | `lastPlayedUtcTicks` 最大 | 本 ADR(新) |
| 选**世界内的档** | 档内 tick 最大 | `SaveService.TryGetMostRecentSave`(**不变**) |

⚠️ **边界裁定(防误读)**:墙钟**只用于选择面**,并且:
不进 `SaveHeader`、不进三流、不进 `sim`、不参与任何确定性求值。
它不是 ADR-005 / ADR-006 意义上的「第四来源」—— 它决定的是**这场回放从哪份档开始**,
而回放一旦开始,输入集合仍是 {事件流, 版本化烘焙数据, 二者的纯函数}。
本裁定与 `TD-persist-14` ⑤「会话开始置位」所需的会话身份**无关**,不复用那条缝。

### D5 — 「新游戏」= 新建世界目录,以空流开局

`CreateWorld()`:分配 `seq` → 建 `<root>/w<seq>/` → 新 `SaveService(basePath: 该目录)` →
空流 + 基线世界开局。**不动其它世界**。

`FirstSessionSave` **零改动**即生效:新世界目录里没有档 ⇒ 守卫通过 ⇒ 写一份自动档
(这就是 2026-11-04 那条「首份存档」在分目录后的自然形态)。

🔁 **2026-11-04 视觉走查轮订正(序号的「成形」判据)**:首发实现里**全新档的第一局被编号成
「第 2 局」**,且 `w1/` 永远不存在。两个来源叠加:① 启动序 1.9 装配 `SaveService` 时其构造器
**建目录**(`SaveService.cs:222`)⇒ 默认世界路径 `w1/` 被建出来但一件东西都没写;
② `BootRoot.TouchWorld(默认世界)` 给这个还不存在的世界写了 `worlds.index` 记录。
而 `CreateWorld` 的高水位把「目录」与「索引键」都当已用序号。
⇒ 现行语义:**目录高水位只算「已成形」的世界**(目录里至少有文件);
索引键仍**全部**计数(墓碑必须保住号,D2 不变);`TouchWorld` 对不存在 / 空目录**不写**。
判据 = `world_catalog_test.test_createWorld_emptyDirectoryDoesNotConsumeSeq` +
`multi_world_boot_wiring_test.test_firstRun_newWorldLandsInW1_notW2`。

### D6 — 「删除世界」= 显式动作,二次确认;**不软删**

删目录本身。理由:玩家显式要求删除,而「归档」会让「我的世界去哪了」变成一个无解答的问题
(且归档目录会与备份工具、`_orphans-*` 这类人工残留混在一起)。
世界内的「槽位只进不退」不变 —— 它是**世界内**的护栏,不阻止整个世界的删除。

🔁 **2026-11-04 实现(与本 ADR 同轮)**:
- 入口 = 主菜单末行「删一局」→ 删除态;**只用 Submit 一路输入**,不新增动作、不新增模态屏
  (`ModalId` 闭集不动);
- **二次确认 = 两次都落在同一个世界上**:第一次按选中(文案变「再按一次确认 · 第 N 局」),
  再按同一行才真删;按别的世界 = 换目标(不删);
- 「返回」离开删除态;
- **序号只增不减**:删除后索引里**保留墓碑条目**(`ListWorlds` 只列目录,故墓碑不上菜单),
  否则 `CreateWorld` 会**复用**刚删掉的号,而任何还记着该号的地方会指向另一个世界;
- 装配层:删完回推新世界列表;若删的正是**当前装配**的目录 ⇒ 换默认世界重装配
  (否则 `SaveService` 的 `basePath` 指向不存在的目录,下一次保存会把它原地重建)。

### D7 — 现有单目录存档的一次性迁移

首次以多世界形态启动时:`saves/*.bin` 与 `session_incomplete.flag` **整体搬进 `saves/w1/`**
(`Directory.Move`,原子),写索引(时刻取 `save_exit.bin` 的文件 mtime,取不到则写 0)。

- **字节零改动** ⇒ 不是存档格式迁移,不走 `ISaveMigration`,不改 `SaveVersion`;
- 搬迁失败 ⇒ **停止启动并报错**(E-13 口径),不「降级成空世界」—— 那会把玩家存档留在
  一个读不到的位置。

### D8 — 目录管理归装配层;`Sim.Codec` 的写盘面不扩大

建 / 删 / 搬目录发生在 `Gameplay.Boot`(装配层)。`Sim.Codec` 内**仍只有一处写盘原语**。

🔁 **起草时标为「待裁」的落点,已由实测证据定死(2026-11-04 实现轮)**:
`SaveWritePathGate`(`Editor.Tools.Gates/SaveWritePathGate.cs:53-62`)只允许
**`SaveService` 一个类**在 `Sim.Codec` 里碰写盘 API,方法级只放行 `WriteAtomic`;
`Directory.CreateDirectory` 也只有「类级」豁免。本类需要建目录 / 搬文件 / 写索引
⇒ 放进 `Sim.Codec` **必撞门**。⇒ **`WorldCatalog` 住 `Gameplay.Boot`**;
索引是**元数据、不是存档体**,因而不受「存档体只有一个写入口」约束,
但仍做原子写(临时文件 → 覆盖),不让「写一半的索引」被读进来。

### D9 — UI 归 42 + 7b(已落:主菜单列表形态)

**已实现的形态**(用户「按 D 案开工」后落地):主菜单 = 世界列表 + 「新起一局」——
没有世界 ⇒ 单行「开始诊治」(与旧形态逐字相同);N 个世界 ⇒ N+1 行
(第一行「继续诊治」,其余「第 N 局」,末行「新起一局」)。
规格已就地改写(`main-menu-42.md` 2026-11-04 修订,`OQ-MM-5` 的「新开一局」据此裁定为做)。
存档册(7b)仍是**世界内**的册子,语义不变 —— 它不承担「换世界」。
⚠️ 「第 N 局」用的是**世界序号**,不是日期/时长 —— 存档摘要(`OQ-MM-6`)仍未裁,本屏不得自造。

### D10 — `hasSave` / `_continueSlot` 的语义收窄为「**最近世界**有没有档」

`BootRoot.PresentMainMenuAsync`(`:1191`)今日问的是「这个目录里有没有档」。分目录后,
「继续诊治」= **最近世界**的最近档;「有没有档」= **至少有一个世界存在**。
⚠️ 今日 `TryGetMostRecentSave` 的 `basePath` 若仍是根目录,会扫到子目录外的 `w*/` 而找不到档
—— 这一处**必须同批改**,否则主菜单会恒显示「开始诊治」。

### D11 — 联机(45)与多世界:**开放问题,不在本 ADR 裁**

哪一方的世界是主机世界、跨世界存档是否可共享 —— 归 P1b 的 45 GDD 轮。
⚠️ 今日在联机路径上引入多世界 = 把未裁的东西实现掉,不得借绿。

---

## Consequences

**正面**

- 「重开」与「多周目」同时具备,且**存档体格式零改动**(无 v6、无迁移链、无黄金夹具重签);
- 首份存档 / 会话未完整标志 / 折叠谓词 / `ConfigVersion` 段**全部按目录天然隔离**,无需逐个改;
- `AC-7a-08`「恰好三处触发点」不变 —— 世界管理是**目录管理**,不是存档触发点。

**代价**

- 新件 `WorldCatalog`(或等价物)+ 索引格式 + 一次性搬迁;
- 主菜单 / 存档册的列表与动作(42 + 7b)是真正的工作量所在;
- 墙钟进入选择面 —— 需要 D4 的边界句长期被引用,否则容易滑进 sim。

---

## Alternatives

| 案 | 做法 | 为什么否 |
|---|---|---|
| **A. 只登记债,不做** | 保持单世界 | 品类常规功能缺失;玩家只能删文件。**今日采纳的是「先立债」**,不是「永久不做」 |
| **B. 入口 + 旧档整体归档** | 新局前把 `saves/*` 搬进 `archive-<ts>/` | 不是游戏惯例(是软件的数据保护习惯);玩家会问「我的旧世界去哪了」;且**仍然只有一条时间线**,第二个世界无法并存 |
| **C. 时间线 id 写进存档头** | `SaveHeader` 加字段 | 改布局 ⇒ `SaveVersion` v6 + 迁移链 + 黄金夹具重签;而 D 案用**目录**就达到了同样的分离效果,代价小一个数量级 |
| **D. 多世界目录 + 选择索引(本件)** | 见 §Decision | — |
| **E. 只改排序键(tick → 文件 mtime)** | 不动目录结构 | 文件 mtime 会被复制 / 恢复 / 同步工具改写;且**两个世界仍冲突在同一组槽号上**(槽位只进不退是全局的)⇒ 只解决「选谁」,不解决「并存」 |

---

## Risks

| # | 风险 | 处置 |
|---|---|---|
| **R1** | 墙钟被读成「进 sim 了」 | D4 写明边界;判据须落在「`sim` 程序集零墙钟 API、`SaveHeader` 无该字段」上 |
| **R2** | 一次性搬迁是**破坏性**的 | `Directory.Move` 原子;失败即启动失败(E-13),不降级 |
| **R3** | `hasSave` 语义变化会让主菜单**恒显示「开始诊治」**(D10 未同批改时的症状) | 该症状是静默的 ⇒ 须有夹具:根目录有 `w1/` 时主菜单仍报「有档」 |
| **R4** | 删除不可逆 | 二次确认;不做软删(D6 已裁) |
| **R5** | 索引与目录不一致(手工删了 `w1/`) | 以**目录为准**重建索引;孤儿目录按「世界存在、时刻未知」列出 |
| **R6** | `WorldCatalog` 落点未定 ⇒ 写盘面可能被撑成两条 | D8 已标为待裁;实现轮前必须定 |

---

## ADR Dependencies

- **ADR-010 §六**(存档布局与唯一写入口)—— 本件不改布局,只改「目录归属」;
- **ADR-005 / ADR-006**(确定性 / 定点边界)—— D4 的墙钟边界句是它们的延伸,不是例外;
- **ADR-014**(玩家构建零 JSON 解析器)—— D3 索引必须二进制;
- **ADR-013 §9 C3**(42 只渲染)—— D9 的 UI 分工;
- **依赖 `OQ-MM-5` 的裁定**(UX 层)—— 未裁之前本件**不转 Accepted**,`TD-persist-20` 不解除。

---

## Validation

🔁 **2026-11-04 实现轮**:V-1…V-5 有载体。
🔁 **2026-11-04 真启动序轮(补最后一跳)**:V-6 / V-7 / V-8 **全部转 PASS** ——
`multi_world_boot_wiring_test.cs`(2 条)从 `BootRoot` 的真启动序出发,用新缝 `SaveRootOverride`
把**存档根**指到临时目录(刻意**不设** `SaveBasePathOverride`,否则测的仍是单世界夹具缝),
于是世界列表 / 一次性迁移 / 世界目录解析 / 换世界重装配全部走**生产路径**。
⚠️ **这一轮抓到并修掉一处生产缺陷**:`ReassembleForWorld` 拿**同一个** `World` 实例重跑
`CompositionRoot.Assemble`,而 `ApplyBakedInitial` 是 fail-closed 的「只许一次」⇒ 换世界 /
新起一局 / 删当前世界**一点就崩**(实测:两条夹具全红,报文「已应用过一次」)。
修法收口在装配层(`world.BaselineModuleCatalog == null` 才应用),`World` 侧守卫**不放松**。

| # | 判据 | 载体 | 状态 |
|---|---|---|---|
| V-1 | 两个世界并存 ⇒ 「最近世界」取 `lastPlayedUtcTicks` 更大者 | `world_catalog_test.test_listWorlds_sortedByLastPlayed_notBySeq`(序号与时刻**刻意反向**) | **PASS** |
| V-2 | 新局不触碰其它世界目录(旧世界逐字节不变) | `world_switch_isolation_test.test_newWorld_doesNotTouchExistingWorld`(SHA-256 逐文件) | **PASS** |
| V-3 | 新局首份存档落在**新世界**目录,且旧世界的档不动 | 同上(两世界各装配一次,各自 `Load`) | **PASS** |
| V-4 | 现存单目录档经一次性搬迁后**逐字节可读**,且不触发任何 `ISaveMigration` | `world_catalog_test.test_migrateLegacy_movesRootSavesIntoW1_byteIdentical`(SHA + 无关文件不动 + 幂等) | **PASS** |
| V-5 | 索引缺失 / 损坏 ⇒ 以目录为准重建,不静默丢世界 | `world_catalog_test.test_corruptIndex_worldsSurvive_andNextWriteRebuilds` / `…test_listWorlds_directoryIsAuthoritative` | **PASS** |
| V-6 | 删除世界后该目录不存在,其它世界不受影响;序号不复用 | `world_catalog_test.test_deleteWorld_removesDirectoryAndIndexEntry_othersSurvive` / `…test_deleteMissingWorld_returnsFalse_noThrow`;屏级二次确认 = `main_menu_worlds_test` 5 条;**装配层这一跳** = `multi_world_boot_wiring_test.test_deleteWorld_throughBoot_removesOnlyChosenWorld`(真启动序:删完 `w2` 目录不在、`w1` 存档文件集逐字节不变、菜单行数当场回落) | **PASS**(目录级 + 屏级 + 装配级) |
| V-7 | `hasSave` 在「根目录下只有 `w1/`」时为 true(D10 的防回归) | `multi_world_boot_wiring_test.test_newWorld_createsOwnDirectoryAndSave_oldWorldUntouched`(只种 `w1/` ⇒ 第一行是「继续诊治」而非「开始诊治」) | **PASS** |
| V-8 | `SaveHeader` 无世界身份字段;`Sim` 程序集零墙钟 API | 静态复核(2026-11-04):`SaveHeader` 字段 = `MagicValue / Checksum / SaveVersion / WorldSeed / ConfigVersion / Tick / SnapshotOffset / 七个时间参数`,`grep -niE "worldseq\|world_id" unity/Assets/Sim.Codec/SaveHeader*.cs` ⇒ 零命中(`WorldSeed` 是 ADR-007 的 sim 种子,**不是**存档目录身份);`grep -rnE "DateTime\.(Utc)?Now\|Stopwatch\|Time\.realtimeSinceStartup\|Environment\.TickCount" unity/Assets/Sim --include=*.cs` ⇒ 零命中 | **PASS** |

⚠️ **本轮未做 / 不得借绿**:
- ~~**D6 删除世界**未实现~~ → **同日补做**(入口 + 二次确认 + 目录删除 + 序号高水位);
- ~~**世界列表的生产路径无 PlayMode 端到端**~~ → **同日补做**
  (`multi_world_boot_wiring_test`,载体见 V-6 / V-7);
- **D10 的 `hasSave` 语义收窄**只在**生产路径**(有 `WorldCatalog` 时)实现;
  夹具缝(`SaveBasePathOverride` 非空)保持旧语义(该目录就是世界目录) —— 这是**故意的**,
  既有 PlayMode 夹具按此断言;
- **世界列表的生产路径无 PlayMode 端到端**(`main_menu_wiring_test` 走夹具缝,那条路径逐字未变);
- `worlds.index` 的**时刻**在**开局选定世界时**与**菜单换世界后**写(`BootRoot.TouchWorld`)。
  ⇒ 排序语义 = **「最后打开」**,不是「最后保存」。P0 够用(玩家不会在同一个世界内重排);
  若要「最后保存时刻」,须在存档路径上再接一次 `Touch`,归下一轮。

---

## Engine Compatibility

**LOW**。纯 BCL 目录操作与字节布局,零 post-cutoff 引擎 API;
`SaveService` / `SaveSlotLedger` / `SaveHeaderCodec` 均不改。

---

## GDD Requirements Addressed

| AC / 规则 | 本 ADR 的关系 |
|---|---|
| `AC-7a-08`(触发点恰三处) | **不变** —— 世界管理是目录管理,复用 `Checkpoint(SaveSlot)`,入口签名集合逐字不变 |
| 规则六(三处触发点) | 每个世界内仍是三处;首份存档仍是「定时兜底的 t = 0 实例」 |
| 规则十三(会话未完整标志) | 标志住在 `basePath` ⇒ 分目录后天然按世界隔离 |
| 规则十四(槽位只进不退) | **世界内**语义不变;不约束跨世界 |
| 7b 规则二 / 规则三 | 存档册仍是「世界内的一本册子」;新增的「世界列表」是**另一层**,不改册子语义 |
| `OQ-MM-5` | **本 ADR 的前置**;未裁则本件止于 `Proposed` |
