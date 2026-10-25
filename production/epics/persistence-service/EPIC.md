# Epic: 7a 持久化服务

> **Layer**: Foundation
> **GDD**: design/gdd/persistence-service.md
> **Architecture Module**: L3 契约程序集(`Sim.Codec`: `ISaveCodec` / `ISaveService` / `SaveHeader`)
> **Status**: ~~**Complete ✅ 2026-10-02**(2/2 stories)~~ **In Progress**(2026-10-25 撤回)
> **Stories**: 2 stories
>
> 🔁 **2026-10-25 状态撤回(原文不删 · 承 QA 签核 C-1 / `TD-persist-04`)**
>
> 原 `Complete ✅ 2026-10-02` 的判据是「**2/2 stories 标 Complete**」——
> 那是**按 story 计数**,从未按 **AC 条数**核对过。实测两个口径都对不上:
>
> | 口径 | 实际 | 说明 |
> |---|---|---|
> | story | **1/2** | `story-001` 已于 2026-10-25 被 QA 轮退回 `In Progress`(7 条 AC 全未勾,`Type: Logic` ⇒ 自动化证据是 BLOCKING 门) |
> | GDD AC | **7/20 有载体** | `story-001` 覆盖 `AC-7a-01…06 + 18`;`story-002` 覆盖 **0 条 GDD AC**(它兑现的是 ADR-029 的 `IPayloadEncoder`,不在本 GDD 的 AC 面上) |
>
> ⇒ **13 条 BLOCKING AC 从未被任何 story 认领**:`AC-7a-07…17` + `19` + `20`。
> 复算:`grep -rn "TR-persist-" production/epics/` ⇒ **0** —— 两个 story 用的是 AC 编号,完全不引 TR registry。
>
> **本行不改的**:`story-002` 的完成度(8/8 AC / 14/14 测试)**不受影响** ——
> 它兑现的 ADR-029 义务是另一条轴,与本 GDD 的 AC 账正交。

### AC 载体总表(2026-10-25 勘察实测 · 13 条零载体逐条定级)

> **勘察方式**:只读代码 + 只读 grep,**未跑 Unity**。逐条附可跑命令。
> **订正 `TD-persist-04` 的一句话**:该债记「13 条零载体」在**记账面**准确,
> 但**实现面并非全空** —— 4 条已在跑夹具,只有 1 条是全空白。整批当「补登记」处理会出错。

| AC | 判定 | 生产 | 夹具 | 处置 |
|---|---|---|---|---|
| `07` | PARTIAL | `Sim.Codec/SaveService.cs:146-165` 同步写 | 无 | 异步/重试 0,归新债 |
| `08` | PARTIAL | `ISaveService.cs:29-61` ①② | `folded_predicate_test.cs:614/629` | ③ IL 门 0,本轮补 |
| ↳ 🔁 **2026-10-25 已兑现** | **IMPLEMENTED_TESTED** | `Editor.Tools.Gates/SaveWritePathGate.cs`(IL 正面断言)+ `AssemblyGates.RunAll` 接线 | `Sim/save_write_path_gate_test.cs` **6 条** + `gate_wiring_manifest_test` 双处登记 | 证伪轮 **M7/M8/M9/M10 四轮全红**;⚠️ **`SaveBytes` 旁路仍待裁**(见下 §两条交叉结论 2),该条**不因本门而关闭** |
| `09` | PARTIAL | `ISaveService.cs:24-27` 半守 | 同上 | CI 扫描归 `TD-ci-01`(外部) |
| `10` | **IMPLEMENTED_TESTED** | `Sim/FoldedPredicate.cs:76-87` | `folded_predicate_test.cs` 9 条 | ✅ 已补登记 |
| `11` | **IMPLEMENTED_TESTED** | `Sim/FoldRow.cs:30-58` + `FoldRowCodec.cs:63-68` | `folded_predicate_test.cs:412/396` 等 | ✅ 已补登记 |
| `12` | **IMPLEMENTED_TESTED** | `Sim/EventStream.cs:267-285` | `reconstruction_and_write_path_test.cs:579/613/652/661` | ✅ 已补登记 |
| `13` | PARTIAL | `IdAuthority.cs:73-85` 机制在 | `id_authority.cs` 6 条 | **收集器 0 调用点**,本轮补 |
| ↳ 🔁 **2026-10-25 已兑现** | **IMPLEMENTED_TESTED** | `Sim/ItemDatabase/ItemInstanceHighWaterCollector.cs`(5 支 Kind 收集 + 委托 `ReconstructItemHighWater`) | `PersistenceService/item_instance_high_water_test.cs` **9 条** | 证伪轮 **M4/M5/M6 三轮全红**;⚠️ **生产调用点仍为 0**(装配期由 7a / `Gameplay.Boot` 接),本条只兑现「收集环存在且被钉住」 |
| `14` | PARTIAL | `SaveService.cs:374-378` 刻意抛 | `save_slot_contract_test.cs:393/414/423` | 迁移链 0 行,归新债 |
| ↳ 🔁 **2026-10-25 已兑现** | **IMPLEMENTED_TESTED** | `Sim.Codec/ISaveMigration.cs` + `SaveMigrationChain.cs`(逐版本 + **每级三条断言**)+ `SaveService.Load` `<` 分支调链 + `StripChecksum` | `PersistenceService/save_migration_chain_test.cs` **12 条** + `save_slot_contract_test.cs` 2 改写 + **1 正面对照新增** | 证伪轮 **MA1/MA2/MA3/MB4 四轮全红**;⚠️ **本条不实现任何真实迁移** —— `CurrentSaveVersion = 1` 且零 `v0` 写入者 ⇒ 无真实历史版本可迁。测的是**框架会不会放过坏迁移**;⚠️ `test_migrate_multiStep_runsEveryVersionInOrder`(「逐版本」)本轮**无独立反证**,如实登记 NOT-RUN |
| `15` | PARTIAL | `SaveService.cs:168-187` 回退 | `save_service_test.cs:98` 仅 (a) | (b) `Corrupt` 态 0,本轮补 |
| ↳ 🔁 **2026-10-25 已兑现** | **IMPLEMENTED_TESTED** | `Sim.Codec/SaveCorruptException.cs` + `LoadBytes` 终态分支(**零 I/O**) | `save_service_test.cs` **4 条**(含「槽目录文件数不变」禁止判据) | 证伪轮 **M1/M2/M3 三轮全红**;`(a)` 回退**提示**仍缺,但提示归 42 / 7b(见 `TD-persist-05` 末注) |
| `16` | PARTIAL | `SaveComposer.cs:82-90` 签名天然正交 | `save_composer_wiring_test.cs:345` 非同轴 | authority 替身夹具 0,归新债 |
| ↳ 🔁 **2026-10-25 已兑现** | **IMPLEMENTED_TESTED** | **零生产改动** —— `Compose` 本来就没有 authority 形参,`IdAuthority` 本来就是实例计数器 | `PersistenceService/save_authority_orthogonality_test.cs` **7 条**(四条独立性质 A/B/C/D + 反向对照 `ClampingStub`) | 证伪轮 **MB1/MB2/MB3 三轮全红**;⚠️ **本条补的是「从未存在的判据」,不是修缺陷** —— 把「两布局产出一致」拆成四条独立性质,因为单条在 `Compose` 不认识 authority 时**恒真**、零判别力 |
| `17` | ~~**NOT_IMPLEMENTED**~~ → **IMPLEMENTED_TESTED** | `Sim.Codec/ExitSaveRetrier.cs`(限时重试,假时钟可注入)· `Sim.Codec/SaveIncompleteFlag.cs`(**纯命名+纯判据,零 I/O 写**)· `SaveService.SaveOnExit` 重试+置位/清位 · `BootRoot` 的 `Application.wantsToQuit` 注册/卸挂 | `PersistenceService/save_exit_retry_and_flag_test.cs` **23 条** | 证伪轮 **MC1…MC15 十五轮**,**23/23 测试全部至少转红一次**;⚠️ **测试抓到一处真缺陷**(`ExitSaveRetrier` 成功分支未清 `lastFailure` ⇒ 成功被读成失败),修在**生产代码**;⚠️ 三处缺口如实登记:退出钩子注册面无自动化(PlayMode 面)· 日志文本无自动化 · **`BootRoot._sessionWasIncomplete` 零消费者**(提示归 42 / 7b) |
| `19` | **IMPLEMENTED_TESTED** | `SaveSlotLedger.cs:93-137` 等 | `save_slot_contract_test.cs` 9 条 | ✅ 已补登记 |
| `20` | PARTIAL | `CompositionRoot.cs:1030/1080` 判据函数 | `composition_root_test.cs:194-254` | 触发方/兜底 0,裁定丙终态 |

**分布**:`IMPLEMENTED_TESTED` **4** · `PARTIAL` **8** · `NOT_IMPLEMENTED` **1**。

> 🔁 **2026-10-25 执行轮后现值**(同表,逐条加注见上):
> `IMPLEMENTED_TESTED` **7**(4 条补登记 + `08`/`13`/`15` 三条本轮兑现)·
> `PARTIAL` **5**(`07` `09` `14` `16` `20`)· `NOT_IMPLEMENTED` **1**(`17`)。
> **复算命令**:本表 `↳ 🔁 2026-10-25 已兑现` 行数 = 3 ⇒ 原 4 + 3 = 7;`5 + 1 = 6`,加 7 = 13 条。
> ⚠️ **五条 PARTIAL + 一条 NOT_IMPLEMENTED 全部已立债**:`07`/`09`/`14`/`16`/`20`(定时兜底半边)
> → **`TD-persist-05`**;`17` → **`TD-persist-06`**。⇒ **13 条中「无载体且无债」的 = 0**。

> 🔁 **2026-10-25 `TD-persist-05` 兑现轮后现值(原文不删)**:
> `IMPLEMENTED_TESTED` **9**(上列 7 + `14` `16` 两条本轮兑现)·
> `PARTIAL` **3**(`07` `09` `20`)· `NOT_IMPLEMENTED` **1**(`17`)。
> **复算命令**:`grep -c "↳ 🔁 \*\*2026-10-25 已兑现\*\*" production/epics/persistence-service/EPIC.md`
> ⇒ **5**;原 4 + 5 = **9**;`3 + 1 = 4`,加 9 = **13** 条 ✓。
> ⚠️ **`14` / `16` 的兑现形态不同,勿混读**:
> `14` 补的是**缺失的执行体**(迁移链框架 0 行 → 有);`16` 补的是**从未存在的判据**
> (生产代码本来就对,但没有任何东西会因它被破坏而转红)。
> ⚠️ **`07` `09` `20` 三条现全部具名 blocked**,不再笼统归 05:
> `07` → 与 `TD-persist-06` 的 `SaveOnExit` 限时 join **同一改动两半**(且额外发现 SHA256 也在主线程);
> `09` → blocked-by **`TD-ci-01`**;`20` 定时兜底半边 → blocked-by **`TD-persist-04` 动作 ①**(瞬时源)。
> ⇒ **待处置 = 0**,但 **≠ 7a 已可交付**:四条未闭项各有具名阻塞方。
> 原件 = `production/qa/evidence/impl-td-persist-05-2026-10-25.md`。

> 🔁 **2026-10-25 `TD-persist-06` 兑现轮后现值(原文不删)** —— **`AC-7a-17` 全表唯一一条 `NOT_IMPLEMENTED` 已清零**:
> `IMPLEMENTED_TESTED` **10**(上列 9 + `17` 本轮兑现)· `PARTIAL` **3**(`07` `09` `20`)· `NOT_IMPLEMENTED` **0**。
> **复算命令**:`grep -c "↳ 🔁 \*\*2026-10-25 已兑现\*\*" production/epics/persistence-service/EPIC.md` ⇒ **5**;
> 本轮 `17` 行是**就地改判**(非新增 `↳` 行)⇒ 9 + 1 = **10**;`3 + 0 = 3`,合计 **13** 条 ✓。
> ⚠️ **`17` 的兑现形态是第三种**(区别于 `14` 的「补缺失执行体」与 `16` 的「补从未存在的判据」):
> 它补的是**跨装配的接线 + 一个新形态的标志位**,且**中途撞红了既有 BLOCKING 门**
> (`AC-7a-08③` IL 门:首版在 `SaveIncompleteFlag` 里直接 `File.WriteAllBytes`)。
> 用户裁定取「收进 `WriteAtomic`」而非放宽门 ⇒ **门一字不改**,代价是标志位语义由
> 「文件存在」收窄为「存在**且非空**」(清位写 0 字节墓碑而非删除)。
> ⚠️ **`07` 仍未兑现且不在本轮面内**(用户裁定只做 `17`):`SaveOnExit` 今日**同步**写盘,
> 「限时 join」实际是「同步写 + 同步重试」。**`09` / `20` 的 blocked 归属不变**。
> 原件 = `production/qa/evidence/impl-td-persist-06-2026-10-25.md`。

### 两条会改变判读的交叉结论

1. **生产可用性 = 0,与 13 条 AC 的登记是两件事。** `Checkpoint` 与 `OnNarrativeAnchor` 的**生产调用点均为 0**
   (`grep -rn "\.Checkpoint(\|OnNarrativeAnchor(" unity/Assets/ | grep -v /Tests/` ⇒ 0)。
   即使 19/20 的实现都在,整套存档在生产路径上仍不可用 —— 这是裁定「丙」的**有意终态**。
2. **`SaveBytes(SaveSlot, byte[])` 是公开旁路**,绕过全部三处门(`SaveService.cs:126-127`)。
   ⇒ `AC-7a-08` 的「无第四入口」在**接口面不成立**,而 GDD `:522-523` 只枚举了
   `Checkpoint` / `Load` / `SaveOnExit`,未把它纳入反面。
   **用户 2026-10-25 裁定:本轮只登记待裁,不改 GDD、不改代码** —— 它算不算第四入口是架构问题。
   > ✅ **2026-10-25 已裁(用户裁定 · 甲案)** —— 结论 = **`SaveBytes` 不是第四入口,
   > 但它违反了 AC-7a-08/09 判据 ① 的字面**。两件事必须分开读:
   > - **语义层:不是第四入口。** AC-7a-08 的「三处」是**触发时机**(事件触发 checkpoint ·
   >   定时兜底 · 退出保存 · 7b 手动槽)。`SaveBytes` 没有自己的触发源 —— 它的**唯一生产调用点**
   >   是 `SaveService.cs` 的 `Checkpoint` 内部。⇒ 真正的三处 = `Checkpoint` / `SaveOnExit` / 7b,
   >   **没有第四处**。
   > - **判据层:确实违反,且是判据被写窄造成的假绿。** GDD `:147` / `:522` / `:531` 三处一致写
   >   「公开可触发**写入**方法参数集**恰等于** `{SaveSlot}`」;`SaveBytes(SaveSlot, byte[])`
   >   两参落在字面内 ⇒ 违反。而执行体(`folded_predicate_test.cs`)只遍历 `{Checkpoint, Load}`,
   >   **比 GDD 字面窄** ⇒ 放过了它。ADR-010 `:361-363` 的括号句「…以本签名与
   >   `Load(SaveSlot)` 为真」把这个读法固化了(该句前提为假,已在 ADR 就地加注)。
   > - **真风险不是「第二条路」,是闸门没关。** 拿到 `ISaveService` 的任何代码都能借
   >   `SaveBytes` 写任意槽、绕过叙事上下文门与槽位只进不退门。
   > - **裁定动作(已落)**:`ISaveService` 只留 `Checkpoint` / `SaveOnExit` / `Load`
   >   (与 ADR-010 §Key Interfaces **逐字**一致);`SaveService.SaveBytes` / `LoadBytes`
   >   改 **`internal`**(经既有 `InternalsVisibleTo("Sim.Contracts.Tests")` 供夹具使用);
   >   判据改回**全量遍历** + 增实现侧断言(两原语不得 `public`)。
   > - **为何不删方法(乙案已驳回)**:9 条测试测的是原子写 / 校验和 / 双档回退 ——
   >   改调 `Checkpoint` 会让 compose 与门介入,「测的是什么」立刻模糊,等于**静默削弱**夹具。
   >   `internal` 保留测试面,同时关掉绕过门的公开通道。
   > - **代价实测**:生产调用点 `SaveBytes` **0** / `LoadBytes` **1**(均在 `SaveService` 内);
   >   测试 17 + 4 处**零改动**(全在 `Sim.Contracts.Tests`,IVT 已存在);
   >   测试持有的是具体类 `SaveService` 而非接口 ⇒ 删接口两行**不破坏任何现有夹具**。

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 存储抽象层（二进制 codec 接口 + 校验骨架） | Logic | ~~**Complete ✅ 2026-09-30**~~ → **In Progress**(2026-10-25 退回,承 C-1) | ADR-010 |
| 002 | 载荷编码契约 —— `IPayloadEncoder` + `IBlobSink` + `EncodeBoxed` 分派 | Logic | **Complete ✅ 2026-10-02**(14/14) | ADR-029 |

## Overview

7a 持久化服务是全案唯一「不产生任何游戏事实」的系统。它把三条逻辑流（病史/病例/世界）连同存档头变成字节写到盘上，再读回来还原成模拟态。

**核心交付**：
- `SaveHeader` — 存档头部结构
- `ISaveCodec` / `SaveCodec` — 按字段名/tag 编码
- `ISaveService` / `SaveService` — 低层原语（SaveBytes/LoadBytes）+ SHA256 校验和 + 原子写 + 双档 bak

**测试**：7/7 Passed · 全量 1447/1474 Passed 0 Failed
> 🔁 **2026-10-25 订正**:上一行的 `7/7 Passed` 是 **2026-09-30 story-001 首版收口时**的数,
> 且当时 `Type: Logic` 的 7 条 AC **一条都没勾**(`[x]`=0)。QA 轮复核实测
> `save_service_test.cs` 的 7 条测试只覆盖 `AC-7a-05/06/18` 三条,**另 4 条 NOT-RUN**
> (其中 `AC-7a-04` = 假绿 —— 断言只有「编码后应有字节」)。原文不删,此处标明该数的时点与口径。

## Next Step

~~**Epic 全部 1 个 story 已完成**。下一步 = 推进其他 epic 或处理跨系统待办。~~
🔁 **2026-10-25**:上句作废(Epic 已撤回 `Complete`)。现状 = **9 条 AC 缺口待处置**
(07/08/09/13/14/15/16/17/20),见上方 §AC 载体总表。`story-001` 的 4 条 NOT-RUN
(`AC-7a-01/02/03/04`)另有清偿载体:前两条归 `TD-persist-03` 真重放 · 03 归 `TD-ci-01` ·
04 须补真往返夹具。

🔁 **2026-10-25 执行轮后现值(原文不删)**:**9 条缺口处置完毕** ——
**3 条已兑现**(`08` ③ IL 门 · `13` 收集器 · `15` (b) `Corrupt` 态,各带证伪轮),
**6 条已立债**(`07`/`09`/`14`/`16`/`20` 定时兜底半边 → `TD-persist-05`;`17` → `TD-persist-06`)。
⇒ **待处置 = 0**,但 **≠ 7a 已可交付**:`TD-persist-05/06` 与下方 §两条交叉结论的 `SaveBytes` 待裁
仍是**未闭的 P0 面债**。**Epic 状态维持 `In Progress`**,不因本轮而翻 `Complete`。

⚠️ **回归三元组**(本轮实跑,非借用):
- 截至 `2026-10-25 03:10 UTC` · 批次 `s19-ac-fix-2` · commit `a2e00e7`(工作树 = HEAD + 本轮未提交改动)
- EditMode `4896 / 4861 / 0 / 35`(`unity/Logs/full-editmode-ac-fix2.xml`)
- PlayMode `202 / 202 / 0 / 0`(`unity/Logs/full-playmode-ac-fix2.xml`)
