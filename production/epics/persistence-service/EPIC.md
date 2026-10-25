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
| `15` | PARTIAL | `SaveService.cs:168-187` 回退 | `save_service_test.cs:98` 仅 (a) | (b) `Corrupt` 态 0,本轮补 |
| ↳ 🔁 **2026-10-25 已兑现** | **IMPLEMENTED_TESTED** | `Sim.Codec/SaveCorruptException.cs` + `LoadBytes` 终态分支(**零 I/O**) | `save_service_test.cs` **4 条**(含「槽目录文件数不变」禁止判据) | 证伪轮 **M1/M2/M3 三轮全红**;`(a)` 回退**提示**仍缺,但提示归 42 / 7b(见 `TD-persist-05` 末注) |
| `16` | PARTIAL | `SaveComposer.cs:82-90` 签名天然正交 | `save_composer_wiring_test.cs:345` 非同轴 | authority 替身夹具 0,归新债 |
| `17` | **NOT_IMPLEMENTED** | **全空** | 仅成功路径 | 五形扫描全 0,单独立债 |
| `19` | **IMPLEMENTED_TESTED** | `SaveSlotLedger.cs:93-137` 等 | `save_slot_contract_test.cs` 9 条 | ✅ 已补登记 |
| `20` | PARTIAL | `CompositionRoot.cs:1030/1080` 判据函数 | `composition_root_test.cs:194-254` | 触发方/兜底 0,裁定丙终态 |

**分布**:`IMPLEMENTED_TESTED` **4** · `PARTIAL` **8** · `NOT_IMPLEMENTED` **1**。

> 🔁 **2026-10-25 执行轮后现值**(同表,逐条加注见上):
> `IMPLEMENTED_TESTED` **7**(4 条补登记 + `08`/`13`/`15` 三条本轮兑现)·
> `PARTIAL` **5**(`07` `09` `14` `16` `20`)· `NOT_IMPLEMENTED` **1**(`17`)。
> **复算命令**:本表 `↳ 🔁 2026-10-25 已兑现` 行数 = 3 ⇒ 原 4 + 3 = 7;`5 + 1 = 6`,加 7 = 13 条。
> ⚠️ **五条 PARTIAL + 一条 NOT_IMPLEMENTED 全部已立债**:`07`/`09`/`14`/`16`/`20`(定时兜底半边)
> → **`TD-persist-05`**;`17` → **`TD-persist-06`**。⇒ **13 条中「无载体且无债」的 = 0**。

### 两条会改变判读的交叉结论

1. **生产可用性 = 0,与 13 条 AC 的登记是两件事。** `Checkpoint` 与 `OnNarrativeAnchor` 的**生产调用点均为 0**
   (`grep -rn "\.Checkpoint(\|OnNarrativeAnchor(" unity/Assets/ | grep -v /Tests/` ⇒ 0)。
   即使 19/20 的实现都在,整套存档在生产路径上仍不可用 —— 这是裁定「丙」的**有意终态**。
2. **`SaveBytes(SaveSlot, byte[])` 是公开旁路**,绕过全部三处门(`SaveService.cs:126-127`)。
   ⇒ `AC-7a-08` 的「无第四入口」在**接口面不成立**,而 GDD `:522-523` 只枚举了
   `Checkpoint` / `Load` / `SaveOnExit`,未把它纳入反面。
   **用户 2026-10-25 裁定:本轮只登记待裁,不改 GDD、不改代码** —— 它算不算第四入口是架构问题。

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
