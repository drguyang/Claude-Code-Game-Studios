# S19-1 实现证据原件 —— 槽位契约高层面 + 折叠接线 + Load 3/4 段

**日期**:2026-10-24
**任务**:`S19-1` = `TD-persist-02`(G-4)+ `TD-persist-01` 剩余两项(两债合一)
**基线溯源三元组**:截至 `2026-10-24 19:0x CST` · 批次 `S19-1 实现轮(第 6 轮全量 EditMode)` · commit `b102faf`(工作树含本件未提交改动)

---

## 一、交付物清单

### 1.1 生产侧(新增)

| 文件 | 职责 |
|---|---|
| `unity/Assets/Sim.Codec/SaveSlotLedger.cs` | 槽位状态宿主(`Locked` 集 / `next_writable_seq` / 叙事上下文门) |
| `unity/Assets/Sim.Codec/SlotProjections.cs` | `Writable` / `Loadable` **纯函数投影**(静态,零实例字段) |
| `unity/Assets/Sim.Codec/SaveHeaderCodec.cs` | 存档头部**读回**(此前全仓零读回方法) |
| `unity/Assets/Sim.Codec/SimEventListCodec.cs` | 事件列表段编解码(此前无列表容器编解码器) |
| `unity/Assets/Gameplay.Boot/SaveComposer.cs` | 折叠 + 组版闭包;**`HistoryFolder.Fold` 的唯一生产调用点** |

### 1.2 生产侧(改动)

| 文件 | 改动 |
|---|---|
| `unity/Assets/Sim.Codec/ISaveService.cs` | 加 `Checkpoint` / `SaveOnExit` / `Load` 三方法 |
| `unity/Assets/Sim.Codec/SaveService.cs` | 三方法实现;构造形参 3 → 6 |
| `unity/Assets/Gameplay.Boot/CompositionRoot.cs` | `saveBasePath` 形参 + `SaveService` 装配 |
| `unity/Assets/Gameplay.Boot/CompositionRootServices.cs` | `SaveService` 属性 |
| `unity/Assets/Gameplay.Boot/BootRoot.cs` | 唯一生产侧存档目录落点 |
| `unity/Assets/Editor.Tools.Gates/FsWriteBoundaryGate.cs` | 第 8 条声明(本件测试) |

### 1.3 测试侧

| 文件 | 条数 | 覆盖判据 |
|---|---|---|
| `unity/Assets/Tests/EditMode/PersistenceService/save_slot_contract_test.cs`(新) | **34** | ①②③④委托半⑤⑦⑦b⑧b⑧c |
| `unity/Assets/Tests/EditMode/PersistenceService/save_composer_wiring_test.cs`(新) | **13** | ④位置半 + 组版 fail-loud |
| `unity/Assets/Tests/EditMode/PersistenceService/folded_predicate_test.cs`(改) | +2 改 2 | ①反射断言 / ⑥⑦c 形参 3→6 |

> 复算:`grep -c "public void test_" unity/Assets/Tests/EditMode/PersistenceService/save_slot_contract_test.cs`
> 与 `save_composer_wiring_test.cs`。

---

## 二、判据逐条结账(Landed / Proven 分列)

| # | 判据 | Landed | Proven | Proven-by(可复算) |
|---|---|---|---|---|
| **①** | `ISaveService` 公开方法集 = 五 | ✅ | ✅ | `folded_predicate_test.cs` `test_iSaveService_publicMethodSetIsFive` |
| **②** | `AC-7a-19` 四子判据各一例 + 闭合回路 | ✅ | ✅ | `test_ac7a19a/b/c/d_*` 五条 + `test_ac7a19_closedLoop_*` |
| **③** | 两投影纯函数 + 独立灰置通道 | ✅ | ✅ | `test_projections_haveNoMutableInstanceFields` / `_areSameInputSameOutput` / `_haveIndependentGreyChannels` |
| **④位置半** | `HistoryFolder.Fold` ≥1 行且不在 `Sim.Codec/` | ✅ | ✅ | `SaveComposer.cs:136` 唯一调用点;`test_composer_isOnlyFoldCallSiteOutsideSimCodec` |
| **④委托半** | `_compose(` 落在 `Checkpoint` 体内 + fail-closed | ✅ | ✅ | `test_checkpoint_invokesComposeDelegate` / `_withoutCompose_failsClosed`;反证 M3 |
| **④字节半** | 「产出字节里折叠行出现」 | ❌ | ❌ | **`NOT-RUN`** —— 见 §四 |
| **⑤** | 折叠产出面变异反证 ≥3 轮 | ✅ | ✅ | 见 §三(6 变异 / 三轮) |
| **⑥** | 门 A 不变 + 构造形参 3→6 | ✅ | ✅ | `Sim.asmdef` `references` = `["Sim.Contracts"]`;`folded_predicate_test.cs` 形参计数断言 |
| **⑦** | `Load` 3/4 段 | ✅ | ✅ | `test_load_handsEventListToReplaySink` / `_withoutReplaySink_throwsInsteadOfSkipping` / `test_saveHeader_roundTripsAllFields` |
| **⑦b** | 迁移链三分支各有夹具 | ⚠️ | ⚠️ | 直通 ✅(`test_load_currentSaveVersion_passesThrough`)/ 前向拒绝 ✅(`test_load_rejectsFutureSaveVersion`)/ **升链 = fail-closed 拒绝**(`test_load_olderSaveVersion_isRejectedUntilMigrationExists` + `_doesNotLockSlot`)—— **真迁移仍 `NOT-RUN`**,见 §四 N-2 |
| **⑦c** | 形参 3→6,`:590` 必红须改 | ✅ | ✅ | `folded_predicate_test.cs` 同批改为 6,未删 |
| **⑦d** | 未注入重放委托 ⇒ 抛,禁静默跳过 | ✅ | ✅ | `test_load_withoutReplaySink_throwsInsteadOfSkipping` |
| **⑦e** | 读档锁在 `Verifying` 通过后置位 | ✅ | ✅ | `test_ac7a19d_loadLocksSlotForReads`;反证 M4 |
| **⑧** | 叙事上下文交注入委托 | ✅ | ✅ | 构造形参 `Func<bool>`;`test_narrativeContext_isOneQuantity_drivesBothProjectionsTogether` |
| **⑧b** | fail-closed,禁 fail-open | ✅ | ✅ | `test_checkpoint/load/ledger_withoutNarrativeContext_failsClosed`;反证 M6 |
| **⑧c** | `InNarrativeContext` 是一个量 | ✅ | ✅ | 同上「一个量」测试 |
| **⑧d** | 形参 3→6 | ✅ | ✅ | 同 ⑦c |
| **⑧e** | `AC-7a-20` 零载体,不得声称已满足 | —— | —— | **本件不声称** —— 见 §四 |

---

## 三、证伪轮(判据 ⑤)

**驱动**:`tools/falsify/unity_falsify.py` · **规格**:`tools/falsify/specs/s19-1-slot-contract.json`
**过滤器**:`SaveSlotContractTest;SaveComposerWiringTest`(45 条)

### 3.1 变异窗口声明(纪律留痕)

| 窗 | 施加变异 | 窗口内**不读**的文件 |
|---|---|---|
| 第一窗 | M1…M6 | `SlotProjections.cs` / `SaveSlotLedger.cs` / `SaveService.cs` / `SaveComposer.cs` |
| 第二窗 | M3 / M5(语法修正后) | `SaveService.cs` / `SaveComposer.cs` |
| 第三窗 | M3(预期订正后) | `SaveService.cs` |

> ⚠️ **自报违规一处**:第一窗内(M2 生效期间)我误读了一次 `SaveService.cs:262-266`。
> 当时 M2 变异的是 `SaveSlotLedger.cs`,该次读到的 `SaveService.cs` 仍是**基线态**
> ⇒ **不污染判别**,但确实违反了我自己声明的窗口纪律。如实登记,不掩盖。

### 3.2 结果(三轮合计 6/6 恰如预期)

| # | 变异 | 打在 | 预期转红 | 实测 | 判 |
|---|---|---|---|---|---|
| **M1** | `Writable` 删 `slot_seq >= next_writable_seq` 子句 | `SlotProjections.cs:65` | 2 条 | 同 2 条 | ✅ exact |
| **M2** | 停用只进不退守卫 | `SaveSlotLedger.cs:92` | 2 条 | 同 2 条 | ✅ exact |
| **M3** | `Checkpoint` 不调 `_compose` | `SaveService.cs:221` | **6 条** | 同 6 条 | ✅ exact(首跑 2 条 → 见 §3.3) |
| **M4** | 停用读档即锁守卫 | `SaveService.cs:270` | 1 条 | 同 1 条 | ✅ exact |
| **M5** | 组版有折叠行时静默只写 `Retained` | `SaveComposer.cs:143` | 2 条 | 同 2 条 | ✅ exact |
| **M6** | 叙事上下文门 fail-open | `SaveSlotLedger.cs:97` | 1 条 | 同 1 条 | ✅ exact |

**受保护路径指纹**:`unity/Assets/AddressableAssetsData` / `unity/Assets/Audio` 前后**逐字节相同**(三轮均验)。
**还原判据**:`restore_all()` 逐文件 **SHA-256** 比对通过;另核 `grep -c "FALSIFY-M"` 四文件**全 0**。

### 3.3 两处首跑失败 —— 都是 spec 缺陷,不是夹具缺陷(留痕)

**失败一:M3 / M5 编译失败(exit 6,无 XML)。**

首轮两条变异把 C# 语法打坏了,不是断言不承重:

- **M3**:原行 `byte[] bytes = _compose(slot)` **无分号**(下一行是 `?? throw …` 的续行)。
  我替换时加了 `;` ⇒ 语句被截断,`?? throw` 成为孤立表达式 ⇒ 编译错误。
- **M5**:`throw new NotSupportedException(` 只是**开括号**,后面还有 4 行参数 + `);`。
  替换成 `if (true) continue;` 后那 4 行字符串拼接悬空 ⇒ 编译错误。

**修正**:保留语法骨架,只改语义 ——
M3 改 `System.Array.Empty<byte>()`(仍无分号,`??` 续行成立);
M5 改 `if (true) _ = (`(吸收后续 4 行,丢弃报文后继续循环)。

**失败二:M3 判别面不清(实得 6 红,预期 2 红)。**

**没有把实得抄进预期**(那会让 `exactRed` 退化成 tautology)。先按 README 坑 1
逐条论证「它为什么**应该**红」,论证成立后才改预期:

| 红 | 为何应该红 |
|---|---|
| `test_checkpoint_invokesComposeDelegate` | 直接判据:调用次数 1→0 |
| `test_checkpoint_writesBytesProducedByCompose` | 直接判据:字节 72B→0B |
| `test_composeReturningNull_throwsInsteadOfWritingEmptySave` | compose 不调用 ⇒ null 分支永不触达 ⇒ 不抛 |
| `test_ac7a19_closedLoop_…` | 环 1 写出 0 字节档 ⇒ 环 2 `LoadBytes` 校验和必失败 |
| `test_ac7a19d_loadLocksSlotForReads` | 同因(夹具先 `Checkpoint` 再 `Load`) |
| `test_ac7a19d_narrativeAnchorUnlocksSlots` | 同因 |

后三条不是被 M3 直接打到,而是**依赖「`Checkpoint` 写出合法存档」这一前提**;
前提被毁则连带红。**这正是坑 1 的形态:变异违反的既有约束要逐条数,不只数新断言。**

改预期后**第三窗重跑一次**确认实得 = 新预期(避免 tautology),通过。

---

## 四、`NOT-RUN` 与不得声称项(逐条如实)

| # | 项 | 状态 | 归属 |
|---|---|---|---|
| **N-1** | **判据 ④ 字节半**「`Checkpoint` 产出的字节里折叠行出现」 | **`NOT-RUN`** | `TD-persist-03`(用户 2026-10-24 裁定并入) |
| **N-2** | **判据 ⑦b 升链**「逐版本升级 + `WorldSeed` 逐位保留」 | **`NOT-RUN`** | `TD-persist-03` |
| **N-3** | `AC-7a-20`(事件触发 checkpoint) | **全仓零载体** | `TD-persist-04` |
| **N-4** | `AC-7a-01` / `AC-7a-02` 语义层 | **零测试**(仅注释挂标) | `TD-persist-03`「相邻发现」· 须另立评审项 |
| **N-5** | 真实驱动源(谁在锚点事件调 `narrativeContext`) | **零落点** | `TD-persist-04` |
| **N-6** | `AC-51-D7`(`ConfigVersion` 比对) | **假绿** —— 装配根传 `configVersion: 0U`,恒「匹配」 | 须另裁 |

### 4.1 字节半为何是 `NOT-RUN` 而非「没写」(六条实测)

`SaveHeader` 七字段**无折叠段偏移** · `Sim.Codec` **零 `FoldRow` 编码器** ·
**全库无任何存档体生产器** · `SimEventCodec.Decode` **只解单事件头部**(无列表容器) ·
`CodecWriter` 数组写只有**定长** · `SaveHeader` **无读回方法**(本件已补,但那只解决头部)。

⇒ **存档体的字节布局从未被造出来**。`SaveComposer.cs:143` 的 `throw` 是**该缺失的证据**,
**不是它的替代品**。

### 4.2 升链为何也 `NOT-RUN`,以及本件做了什么

`CurrentSaveVersion = 1` 且全库**零 v0 写入者** ⇒ 升链**零触发面**(判据 ⑦b 自带的事实)。

本件**没有**实现迁移,但**堵上了一个静默接受坏档的洞**:改动前
`SaveVersion < CurrentSaveVersion` **无任何处理**,旧档会被 `ReadEventSegment`
按**当前版本布局**解析 —— 布局一旦变过就是**静默错读**。
现改为 fail-closed 抛 `NotSupportedException` 并点名 `TD-persist-03`。

配套两条夹具:
`test_load_olderSaveVersion_isRejectedUntilMigrationExists`(断言拒绝 + 报文点名)
与 `test_load_olderSaveVersion_doesNotLockSlot`(拒绝不得永久锁槽)。

> ⚠️ **不得**把这两条读成「⑦b 三分支已全部兑现」—— 真迁移仍 `NOT-RUN`。

---

## 五、回归(三元组)

| 轮 | 截至时刻 | 批次 | commit | total | passed | failed | skipped |
|---|---|---|---|---|---|---|---|
| 轮 2(基线) | 2026-10-24 18:34 CST | S19-1 实现轮 | `b102faf` | 4769 | 4734 | 0 | 35 |
| 轮 4 | 2026-10-24 18:43 CST | 同上 + `FsWriteBoundaryGate` 声明 + 自指排除 | `b102faf` | 4782 | 4747 | 0 | 35 |
| 轮 5(还原后) | 2026-10-24 18:54 CST | 证伪轮全部还原后复核 | `b102faf` | 4782 | 4747 | 0 | 35 |
| 轮 6(收口) | 2026-10-24 18:58 CST | 升链 fail-closed + 2 条新测 | `b102faf` | **4784** | **4749** | **0** | **35** |

**判据 = 等式**:轮 2 → 轮 4 增量 `+13 total / +13 passed`,**恰好等于**新增的
`SaveComposerWiringTest` 13 条。轮 4 → 轮 5 **逐位相同** ⇒ 证伪轮还原干净。

> 复算命令:
> ```bash
> python3 -c "
> import xml.etree.ElementTree as ET
> r=ET.parse('unity/Logs/s19-1-editmode4.xml').getroot()
> print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
> ```

### 5.1 轮 6 结果

```text
total=4784 passed=4749 failed=0 inconclusive=0 skipped=35
```

**等式校验**:轮 5 → 轮 6 增量 `+2 total / +2 passed`,**恰好等于**新增的两条升链夹具
(`test_load_olderSaveVersion_isRejectedUntilMigrationExists` /
`test_load_olderSaveVersion_doesNotLockSlot`)。零回归。

复算命令:
```bash
python3 -c "
import xml.etree.ElementTree as ET
for n in (2,4,5,6):
    r=ET.parse(f'unity/Logs/s19-1-editmode{n}.xml').getroot()
    print(n, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
```

