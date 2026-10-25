# `TD-persist-04` 解除动作 ③ 执行轮 —— 13 条零载体 AC 的处置

**日期**:2026-10-25 · **执行**:AutoClaw 会话(`181888b1`)
**对象**:7a `persistence-service` · `TD-persist-04` 解除动作 ③(**13 条 BLOCKING AC 零载体**)
**用户裁定**:「**补登记 + 建债 + 修真缺陷**」;`SaveBytes` 处理 =「**需要先裁,本轮只登记**」

---

## 一、裁定面与结论

勘察实测把「13 条零载体」拆成了**三不是一种**的分布 —— 整批当「补登记」处理会出错:

| 判定 | 条数 | AC | 本轮处置 |
|---|---|---|---|
| `IMPLEMENTED_TESTED` | **4** | `10` `11` `12` `19` | ✅ **补登记**到 `story-001` §跨 story 承载的 AC |
| `PARTIAL` | **8** | `07` `08` `09` `13` `14` `15` `16` `20` | 3 条**修真缺陷** · 5 条**建债** |
| `NOT_IMPLEMENTED` | **1** | `17` | **建债**(`TD-persist-06`) |

**收口判据**:13 条中「**无载体且无债**」的 = **0**。
⚠️ 这**不等于** 7a 可交付 —— 6 条已立债的仍是**未闭的 P0 面债**,Epic 状态**维持 `In Progress`**。

---

## 二、三条真缺陷的兑现(Landed + Proven 两列)

| AC | Landed(已落) | Proven(已证) | File(s) |
|---|---|---|---|
| `15(b)` | ✅ | ✅ | `Sim.Codec/SaveCorruptException.cs`(新)· `Sim.Codec/SaveService.cs:196-203` |
| `13` | ✅ | ✅ | `Sim/ItemDatabase/ItemInstanceHighWaterCollector.cs`(新) |
| `08③` | ✅ | ✅ | `Editor.Tools.Gates/SaveWritePathGate.cs`(新)· `Editor.Tools.Gates/AssemblyGates.cs` `RunAll` 接线 |

### `AC-7a-15(b)` —— `Corrupt` 态零表达

**病因**:`LoadBytes` 在 final 与 bak 双双校验失败时抛**裸 `InvalidDataException`**,
与同路径另三处(前向不兼容 / magic 不符 / 布局损坏)**同型不可分** —— 而 GDD `:257` 的
`Corrupt` 是一个**独立的态**,态的边界须在类型上可表达。

**兑现**:`SaveCorruptException` + `LoadBytes` 终态分支。**关键判据 = 本分支零 I/O**
(「槽目录文件数不变」是**禁止**判据,不是「清理后数目恰好没变」)。

**两处实测订正(如实登记)**:
1. 首版按「继承 `InvalidDataException` 保持向后兼容」写 ⇒ **编译失败**:
   `error CS0509: cannot derive from sealed type 'InvalidDataException'`。
   修法:父类改 `Exception`,理由写进文件头。
2. 由 1 连带发现:**「兼容义务」本就不存在** ——
   `grep -rn "catch (InvalidDataException" unity/Assets/` ⇒ **0 命中**。
   故这不是「取舍」,是原假设为假。测试同步从「是 `InvalidDataException` 子类」
   **反转为**三条「不是」(不是 `SaveCorruptException` / 不派生 `IOException` / 不派生 `InvalidDataException`)。

### `AC-7a-13` —— 收集器零调用点

**病因**:`IdAuthority.ReconstructItemHighWater` 机制在,**生产调用点 = 0** ——
高水位永远不会被重构 ⇒ 读档后发重号。

**兑现**:`ItemInstanceHighWaterCollector.Collect`(5 支 `instance_id` Kind)+
`CollectAndReconstruct` 委托。**哨兵过滤单点**:收集器**不**过滤(原样交出负值),
过滤归 `ReconstructItemHighWater` 一处 —— 双实现将来必漂移。

**夹具把 registry 固化为可执行常量**:`test_ac7a13_collectorCoversExactlyTheRegistryInstanceIdKinds`
断言收集器覆盖面 **恰等于** 5 支 —— 将来 registry 新增 `instance_id` Kind 而收集器没跟,此测转红。

### `AC-7a-08 ③` —— IL 门从未有执行体

**病因**:AC-7a-08 是**三方守** ① 接口签名反射 ② 接口级正面枚举 ③ **IL 正面断言**。
前两方只看 `ISaveService` **自身的公开面**,**看不见旁路**;第 ③ 方的执行体**此前从未存在**。

**兑现**:`SaveWritePathGate` —— 两条判据(缺任一条,门就退化成恒绿):
- **A 写盘点形状**:`SaveService` 内出现写盘 API 调用的方法集合**恰等于** `{WriteAtomic}`,
  且 `WriteAtomic` 须**真的含**写盘调用(防空集冒充绿)。
- **B 全程序集无旁路**:`Sim.Codec` 内**除 `SaveService` 外**零写盘调用。

**三处实现期订正(如实登记)**:
1. **API 名全漏判**:名单是源文本简名(`File.WriteAllBytes`),IL 操作数是全名
   (`System.IO.File.WriteAllBytes`)⇒ 首跑只命中 `FileStream.Flush`。
   修法:只剥 `System.IO.` **一个**前缀(刻意不取「最后两段」—— 后者判据过宽 = 假红)。
2. **`Directory.CreateDirectory` 被判越界**:它在 `SaveService` 构造期(`SaveService.cs:113`)。
   修法:显式登记 `ClassLevelOnlyApiNames`(类级约束、方法级不约束),
   判据写清「删掉它,存档还能写吗?」不能 ⇒ 前置条件,不是写路径。
3. **门链自己的「新门须同批登记」判据转红**:全量首跑唯一红 = `GateWiringManifestTest`
   报「`SaveWritePathGate` 有声明,但 ClassConsumerManifest 没有判决」⇒ 双处登记后 15/15 绿。

---

## 三、证伪轮(十轮,逐轮 XML 实据)

**纪律**:每轮「改 → 跑 → 还原」在**同一命令内**完成;三份被变异文件
(`SaveService.cs` / `ItemInstanceHighWaterCollector.cs` / `SaveWritePathGate.cs`)
均 **SHA-256 逐字节**还原(基准哈希见 §五)。

| 轮 | 变异内容 | 红数 | 转红的测试 | XML |
|---|---|---|---|---|
| **M1** | `Corrupt` 态抛回裸 `InvalidDataException` | **2** | `bothFinalAndBakBad_throwsSaveCorrupt` · `leavesSlotDirectoryUntouched` | `falsify-M1.xml` |
| **M2** | 保留具名异常但**抛前删除残档** | **1** | `leavesSlotDirectoryUntouched` | `falsify-M2.xml` |
| **M3** | 删 bak 回退分支 | **2** | `checksumFallback_readsBakWhenFinalCorrupted` · `fallbackState_finalBadBakGood_doesNotEnterCorrupt` | `falsify-M3.xml` |
| **M4** | 收集器删 `DropDespawned` 分支 | **2** | `allFiveKindsContribute` · `collectorCoversExactlyTheRegistryInstanceIdKinds` | `falsify-M4.xml` |
| **M5** | 删 null decoder 守卫(静默返空集) | **1** | `nullDecoder_failsClosed` | `falsify-M5.xml` |
| **M6** | `Craft` 只收 output,丢 input | **1** | `allFiveKindsContribute` | `falsify-M6.xml` |
| **M7** | `SaveService` 内长出第二写盘点 | **3** | `realTree_zeroRed` · `whitelistedMethodReallyWritesToDisk` · `writeSurfaceIsNotEmpty` | `falsify-M7.xml` |
| **M8** | 另立类旁路写盘 | **3** | 同上三条 | `falsify-M8.xml` |
| **M9** | `WriteAtomic` 删 `File.WriteAllBytes` | **1** | `whitelistedMethodReallyWritesToDisk` | `falsify-M9.xml` |
| **M10** | IO 前缀剥离失效 | **1** | `whitelistedMethodReallyWritesToDisk` | `falsify-M10.xml` |

**复算命令(自包含)**:
```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios && python3 - <<'PY'
import re
for i in ['M1','M2','M3','M4','M5','M6','M7','M8','M9','M10']:
    s=open(f'unity/Logs/falsify-{i}.xml',encoding='utf-8').read()
    d=dict(re.findall(r'(\w[\w-]*)="([^"]*)"', re.search(r'<test-run[^>]*>',s).group(0)))
    names=[n for n,r in re.findall(r'<test-case[^>]*?\bname="([^"]+)"[^>]*?\bresult="(Failed)"',s)]
    print(f"{i:4s} total={d['total']:>5} failed={d['failed']:>3} -> {names}")
PY
```
**实跑输出**:十轮 failed 分别为 `2 / 1 / 2 / 2 / 1 / 1 / 3 / 3 / 1 / 1`,
**无一轮为 0** —— 每条判据都有独立判别力。

### ⚠️ M1 的连带效应(**不是**「一条变异覆盖两条判据」)

M1(异常类型退化)同时打红了 `leavesSlotDirectoryUntouched` —— 因为该测试也要先拿到
`SaveCorruptException` 才能继续。按「**一属性一条变异**」纪律,这**不算** M2 的替代品:
M1 只证「异常类型」这一属性;**「文件数不变」这条禁止判据需要 M2 独立证明**
(保留具名异常、但抛前删残档)。两轮各打一个属性,缺任一轮那条属性就是零覆盖。

---

## 四、回归数字(三元组 · 本轮实跑)

**截至** `2026-10-25 03:10 UTC`(XML 实戳)· **批次** `s19-ac-fix-2`(全量两套件)· **commit** `a2e00e7`
(工作树 = HEAD + 本轮未提交改动)

| 套件 | 结果 | XML | 基线对照 |
|---|---|---|---|
| **EditMode**(第 2 跑) | `4896 / 4861 / 0 / 35` | `unity/Logs/full-editmode-ac-fix2.xml` | 前基线 `4896 / 4860 / 1 / 35`(`full-editmode-ac-fix.xml`)⇒ **唯一红已修**,`total` 不变 |
| **EditMode**(第 3 跑 · 终态) | `4896 / 4861 / 0 / 35` | `unity/Logs/full-editmode-ac-fix3.xml` | 与第 2 跑**逐位相同** ⇒ 注释订正零影响(见 §五之二) |
| **PlayMode** | `202 / 202 / 0 / 0` | `unity/Logs/full-playmode-ac-fix2.xml` | 与 S19 基线 `202/202/0/0` 一致 ⇒ **零变化** |

**新测真在跑(不是跳过)**:从 EditMode XML 逐名复核 ——
`SaveWritePathGateTest` **6 条全 Passed** · `test_ac7a13_*` **10 条全 Passed** ·
`test_corruptState_*` **3 条 + `fallbackState_*` 1 条全 Passed**。

**复算命令**:
```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios && python3 -c "
import re
for f in ['full-editmode-ac-fix2','full-playmode-ac-fix2']:
    s=open(f'unity/Logs/{f}.xml',encoding='utf-8').read()
    print(f, re.search(r'<test-run[^>]*>',s).group(0)[:220])
"
```

---

## 五、还原校验(SHA-256)

三份被变异文件在**最后一轮变异结束后**逐字节还原。**当前**哈希:

```
0aad5d0e694cf74cfe3f90aca8df9235a4bb389ed4bb6bcf59856527bc253981  unity/Assets/Sim.Codec/SaveService.cs
640e787afe0edc0f01c329c74cf7182050a8a7a554637ba08c33abcdfffed471  unity/Assets/Sim/ItemDatabase/ItemInstanceHighWaterCollector.cs
b127e49a2c6f5b2c634831d60d84859e9c00bb3805ed2e22b4ad055ae93ca62d  unity/Assets/Editor.Tools.Gates/SaveWritePathGate.cs
```
复算:`sha256sum <上述三路径>`。

### ⚠️ 如实登记:`SaveService.cs` 的当前哈希 **≠** 证伪轮还原态哈希

**原因**(可由 mtime 独立复核):

| 文件 | mtime | 与证伪轮的关系 |
|---|---|---|
| `ItemInstanceHighWaterCollector.cs` | `10:57:02` | **未再改动** —— 早于 `full-editmode-ac-fix2.xml`(11:09 起跑),⇒ **当前哈希即还原态** |
| `SaveWritePathGate.cs` | `11:04:53` | **未再改动** —— 晚于 M10(`11:04:46` 起跑)7 秒,⇒ **当前哈希即还原态** |
| `SaveService.cs` | `11:15:26` | **已再改动** —— 全量跑完后(11:10)又做了**两处纯注释订正**(见 §八) |

⇒ `SaveService.cs` 的证伪轮还原态哈希**未能留存**(当时未单独记档),本件**不宣称**它等于当前值。
**可验证的替代判据**:`git diff unity/Assets/Sim.Codec/SaveService.cs` 显示本轮对它的**全部**改动,
其中 `11:15:26` 引入的那两处(🔁 订正注 + `<exception>` 订正)**均为注释**,不含任何 IL 变动。

### 五之二、终态判据(还原 + 全部注释订正**之后**的全量)

| 套件 | 结果 | XML | 起止(UTC) |
|---|---|---|---|
| EditMode(第 3 跑) | **`4896 / 4861 / 0 / 35`** | `full-editmode-ac-fix3.xml` | `03:16:41` → `03:18:19` |

**与第 2 跑**(`full-editmode-ac-fix2.xml`,`03:09:14`→`03:10:50`,`4896/4861/0/35`)**逐位相同**
⇒ 「`11:15:26` 那两处改动是纯注释、不改判据」这一断言**由实跑坐实**,不是推论。
⇒ 本轮的「还原完整 + 零回归」在地三跑上成立,三份被变异文件的**当前盘上状态即已验证状态**。

---

## 六、⚠️ 未及核查项(禁借绿)

1. ~~**`SaveBytes` 旁路未裁**~~ ✅ **2026-10-25 已裁(甲案)并落盘** —— 详见本节末尾
   「§九 `SaveBytes` 裁定」。本件 §三 写在裁定之前,原句保留:它准确描述了**当时**的状态。
2. **收集器生产调用点仍为 0** —— `AC-7a-13` 兑现的是「收集环存在且被钉住」,
   装配期由 7a / `Gameplay.Boot` 接。本轮的 `IMPLEMENTED_TESTED` **不覆盖**接线。
3. **`AC-7a-15(a)` 回退提示仍缺** —— 回退动作本身已实现(`LoadBytes:170-192`),
   缺的是「提示」承载,归 42 / 7b。
4. **本件未跑跨平台三格矩阵** —— `TD-ci-01` 仍未开;IL 门的判据是 Mono 产物。

---

## 七、建债清单

| 债号 | 内容 | 承载 AC |
|---|---|---|
| `TD-persist-05` | 四条「守约执行体缺失」+ `20` 定时兜底半边 | `07` `09` `14` `16` + `20`(半边) |
| `TD-persist-06` | 退出保存失败:限时重试 + 下次启动提示 | `17` |

两者正文已落 `docs/tech-debt-register.md`(索引表 + 正文),并已刷新 §五 权威输出块
(**二十二订**:条目 `105 → 107` · `open 71 → 73` · 未闭 P0 面 `72 → 74`)。

---

## 八、被发现并订正的三处「幽灵引据 / 过期陈述」

| 位置 | 原文 | 订正 |
|---|---|---|
| `SaveService.cs:12` | 「异步化归 `TD-persist-03`」 | 该债**不含异步化且已 closed** ⇒ 真归属 = `AC-7a-07` / `TD-persist-05` |
| `SaveService.cs` `<exception>` | 「子类关系保留既有 `catch (InvalidDataException)` 调用方的兼容」 | 父类是 `Exception`,`InvalidDataException` 为 `sealed` 不可继承,且全仓此类 `catch` **零命中** |
| `TD-persist-05` §实测 | ⑥ 定时兜底「`grep` ⇒ **0**」 | 实跑 **3 命中**(`CompositionRoot.cs:480/:1003` 的注释自陈)⇒ 数字是假的,已改记「3 命中,全为注释自陈」(结论不变) |

---

## 九、`SaveBytes` 裁定(同日追加 · 甲案)

**问题**:`SaveBytes(SaveSlot, byte[])` 算不算 AC-7a-08 的「第四入口」?

### 结论 = 两句,须分开读

| 层 | 结论 |
|---|---|
| **语义层** | **不是第四入口。** AC-7a-08 的「三处」是**触发时机**。`SaveBytes` 无独立触发源 —— 唯一生产调用点是 `SaveService` 的 `Checkpoint` 内部。⇒ 真正的三处 = `Checkpoint` / `SaveOnExit` / 7b,**没有第四处**。 |
| **判据层** | **的确违反,且是假绿。** GDD `:147` / `:522` / `:531` 三处一致:「公开可触发**写入**方法参数集**恰等于** `{SaveSlot}`」。`SaveBytes` 两参 ⇒ 违反。而执行体只遍历 `{Checkpoint, Load}`,**比 GDD 字面窄** ⇒ 放过了它。 |

### 真风险不是「第二条路」,是闸门没关

拿到 `ISaveService` 的任何代码都能借 `SaveBytes` 写任意槽,绕过 `Checkpoint` 的
**全部门**(叙事上下文 / 槽位只进不退 / 组版)。这已超出「多一个便利方法」的量级。

### 落点(Landed)

| 文件 | 改动 |
|---|---|
| `Sim.Codec/ISaveService.cs` | 删 `SaveBytes` / `LoadBytes` 两声明 ⇒ 接口恰三方法,与 ADR-010 §Key Interfaces **逐字**一致 |
| `Sim.Codec/SaveService.cs` | `SaveBytes` / `LoadBytes` `public` → **`internal`** |
| `Tests/.../folded_predicate_test.cs` | 判据改**全量遍历**(原 `test_iSaveService_checkpointAndLoadTakeOnlySaveSlot` → `test_iSaveService_everyPublicMethodTakesOnlySaveSlotOrNothing`)+ 新增实现侧断言 `test_saveService_lowLevelPrimitivesAreNotPublic` |
| `docs/architecture/adr-010-...md` | 就地加注(原文不删):标出 `:361-363` 括号句**前提为假**,明写「**不构成修订**」 |

### 为何不删方法(乙案驳回)

9 条测试测的是**原子写 / 校验和 / 双档回退**。改调 `Checkpoint` 会让 compose 与三道门介入,
「测的是什么」立刻模糊 —— 等于**静默削弱夹具**。`internal` 同时做到:关掉绕过门的公开通道
+ 保住测试面。

### 代价实测(改前核过,不是估算)

- 生产调用点:`SaveBytes` **0** · `LoadBytes` **1**(均在 `SaveService` 内)
- 测试调用:17 处 `.SaveBytes(` + 4 处 `.LoadBytes(` **零改动** ——
  全在 `Sim.Contracts.Tests`,而 `Sim.Codec/AssemblyInfo.cs:2` **已有**
  `InternalsVisibleTo("Sim.Contracts.Tests")`(既有声明,不是为本次新开的口子)
- 测试静态类型:全部持有具体类 `SaveService`,**无一持有 `ISaveService`**
  ⇒ 删接口两行**不破坏任何现有夹具**

### 与 `TD-persist-05` 的形态区别(须分开留档)

- `TD-persist-05` = **门没接上**(执行体不存在)
- 本条 = **门接了,但判据面被悄悄缩小**(执行体比字面窄)

⇒ 「补了执行体」**不等于**「判据面也对」。两种形态各登记一次。

登记:`docs/tech-debt-register.md` §`### TD-persist-07`(立债同批即 `closed`)。

### 九之补、裁定后的回归与证伪轮(实跑)

**回归三元组**:截至 `2026-10-25 03:29 UTC` · 批次 `sb-clean`(全量)· commit `aca65e2`
(工作树 = HEAD + 本次 `SaveBytes` 裁定改动)

| 套件 | 结果 | XML | 对照 |
|---|---|---|---|
| EditMode | **`4897 / 4862 / 0 / 35`** | `unity/Logs/savebytes-clean.xml` | 前值 `4896 / 4861 / 0 / 35` ⇒ **+1**,算式 `4896 − 2 + 3 = 4897`(2 条旧断言换成 3 条新断言) |
| PlayMode | **`202 / 202 / 0 / 0`** | `unity/Logs/savebytes-playmode.xml` | 与基线一致 ⇒ **零变化** |

**证伪轮(3 轮,BLOCKING 门 PASS · 每条新属性各一轮)**

| 轮 | 变异(改完跑、跑完还原,同命令内) | 红数 | 转红的测试 | XML |
|---|---|---|---|---|
| **MA** | `SaveService.SaveBytes` **改回 `public`** | **1** | `test_saveService_lowLevelPrimitivesAreNotPublic` | `sb-MA.xml` |
| **MB** | 接口 **+ 实现同步**加一个两参 `Probe(SaveSlot, byte[])`(可编译形态) | **2** | `everyPublicMethodTakesOnlySaveSlotOrNothing` · `publicMethodSetIsThree` | `sb-MB.xml` |
| **MC** | `SaveService.LoadBytes` **改回 `public`** | **1** | `test_saveService_lowLevelPrimitivesAreNotPublic` | `sb-MC.xml` |

⚠️ **MA 与 MC 各打一条同一测试,不构成「一条覆盖两判据」**:`test_saveService_lowLevelPrimitivesAreNotPublic`
对 `SaveBytes` 与 `LoadBytes` 是**两条独立断言**;MA 只证写侧那半有判别力,MC 只证读侧那半。
缺任一轮,对应那半就是零覆盖。

⚠️ **MB 首版作废(如实登记)**:初版只给接口加方法、不改实现 ⇒ `SaveService` 缺实现,
**编译失败、零 XML**。那测的是**编译器**,不是夹具 ⇒ 按「变异打对靶」纪律重做为
「接口 + 实现同步加」的可编译形态。首版无 XML 即无结论,不计入证伪轮。

**还原判据 = SHA-256 逐字节**(基准见 §五):
`sha256sum -c sb-baseline.sha256` ⇒ 两文件均「成功」。

### 九之三、终态重跑(`savebytes-final`,坐实「注释改动零影响」)

**为什么再跑一次**:`sb-clean` 起跑(`03:27:59Z`)之后,我又改了 `ISaveService.cs`
的一句注释 —— 原文写「见该件 §Migration 注」是**悬空引用**(ADR-010 无该节),
改为「见该件 **§Key Interfaces 代码块之后**的「🔁 2026-10-25 注」」。
改动 mtime `11:36:04` 本地 = `03:36:04Z`,落在 `sb-clean` 之后。
上轮(`§五之二`)正是因为同样情形(`SaveService.cs` 在起跑后改注释)重跑过 ⇒ **保持一致**。

| 跑 | 起止(UTC) | total / passed / failed / skipped | XML |
|---|---|---|---|
| `sb-clean` | `03:27:59Z → 03:29:37Z` | `4897 / 4862 / 0 / 35` | `savebytes-clean.xml` |
| **`sb-final`** | `03:36:45Z → 03:38:23Z` | **`4897 / 4862 / 0 / 35`** | `savebytes-final.xml` |

**逐位相同** ⇒「`11:36:04` 那次注释改动是纯注释、不改判据」这一断言**由实跑坐实**,不是推论。

**复算命令**:

```bash
python3 - <<'PY'
import re
for p in ['unity/Logs/savebytes-clean.xml','unity/Logs/savebytes-final.xml']:
    s=open(p,encoding='utf-8').read()
    d=dict(re.findall(r'(\w[\w-]*)="([^"]*)"',re.search(r'<test-run[^>]*>',s).group(0)))
    print(p, [d[k] for k in ('total','passed','failed','inconclusive','skipped','start-time','end-time')])
PY
```

**起跑前已重编译**:`savebytes-final.log:326` 有 `DisplayProgressbar: Compiling Scripts`,
`ISaveService.cs` mtime `11:36:04` **早于** XML `start-time` `11:36:45` ⇒ 本轮跑的是改动后的源码。
