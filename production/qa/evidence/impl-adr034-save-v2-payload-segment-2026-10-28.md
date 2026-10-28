# 实现轮证据原件 —— ADR-034 存档 v2 布局(载荷段)

- **对象**:`docs/architecture/adr-034-save-v2-payload-segment.md`(Accepted 2026-10-28)
- **日期**:2026-10-28 · **执行者**:Claude
- **性质**:本件 = **实现轮落盘证据**(实现五件 + 判据 V-1…V-7),**非评审报告**
- **判定**:ADR-034 §Validation **V-1…V-7 全部勾选**(逐条证据见 §三)

> ⚠️ **本件不追认任何既往判定** —— 评的是**当下**代码(承 `review-workflow.md` §不可追补)。
> ⚠️ **本文所有数字均附可跑命令**;回归数带三元组(截至时刻 · 批次 · commit),见 §六。

---

## 一、结论

| 项 | 结论 | 证据 |
|---|---|---|
| ① `IBlobRestoreSink` 契约 | ✅ 落盘(住 `Sim.Codec`,**未**进 `Sim.Contracts`) | §二-① |
| ② `SaveComposer` 载荷段 | ✅ 引用驱动 + 折叠后取引用 + 缺 blob fail-closed | §二-② |
| ③ `SaveService` 自定长折叠段 + 装池 + 版本门 | ✅ 三段按形状精确读,末尾 `EnsureFullyConsumed` | §二-③ |
| ④ `InMemoryBlobPool.Restore` | ✅ 保号装载 + 空洞 + **同号同内容幂等**(实跑修正) | §二-④ / §四 |
| ⑤ `test_ac9g` 边界段翻红为绿 | ✅ 原「正向断言失败」→「装回后重建成功,且载荷内容逐位保持」 | §二-⑤ |
| 接线 | ✅ `CompositionRoot` 两处(编码器写面 / 读档装载面) | §二-⑥ |

---

## 二、实现五件 —— 逐件落点

### ① `IBlobRestoreSink`(新文件)

| 项 | 落点 |
|---|---|
| 契约 | `unity/Assets/Sim.Codec/IBlobRestoreSink.cs:34` — `void Restore(int blobId, ReadOnlySpan<byte> blob)` |
| 为什么**不**复用 `IBlobSink` | 后者 `Store(...) -> int` 由池**发号**,无法装到调用方指定的号上;读档必须**保号**(`PayloadRef.BlobId` 重编号 ⇒ 每条引用同时失效) |
| 为什么**不**住 `Sim.Contracts` | 进 Contracts 会把池写面暴露给 `Sim` —— 正是 ADR-029 §② 挡掉的旁路(ADR-034 §⑤) |
| 实现 | `unity/Assets/Sim.Codec/InMemoryBlobPool.cs:71` |

### ② `SaveComposer` 载荷段

| 项 | 落点 |
|---|---|
| 形参 | `unity/Assets/Gameplay.Boot/SaveComposer.cs:103` — `IBlobPool blobPool = null`(**在 `bufferPool` 之后**,见 §七-①) |
| 计算时机 | `SaveComposer.cs:128` — 在 `finalEvents`(**折叠之后**)上取引用 |
| 段编码 | `SaveComposer.cs:170` — `EncodePayloadSegment` |
| 缺池 fail-closed | `SaveComposer.cs:195` — `blobPool == null` ⇒ 抛 |
| 缺 blob fail-closed | `SaveComposer.cs:207` — `TryGetBlob` 假 ⇒ 抛(报文点名 `BlobId` + 「读不回来」) |

### ③ `SaveService`

| 项 | 落点 |
|---|---|
| v1 拒收门 | `unity/Assets/Sim.Codec/SaveService.cs:719` — **早于**迁移链分支 |
| 折叠段自定长读 | `SaveService.cs:803` `ParseBody` — `count:i32` + `count × SaveLayout.FoldRowSize`,只推进游标 |
| 载荷段读 + 校验 | `SaveService.cs:803` 同方法内 —— count 非负 / 界内 / `blobId` **严格递增** / `length` 非负 |
| 读尽闭合 | `SaveService.cs:886` — `EnsureFullyConsumed()` 移到**载荷段之后**(v1 时由折叠段闭合) |
| 装池 | `SaveService.cs:899` `InstallPayloads` — **先装池、再解事件**(§④) |
| 未注入装载面 | `SaveService.cs:903` — 有载荷条目而 `_blobRestoreSink == null` ⇒ 抛(不静默跳过) |

### ④ `InMemoryBlobPool.Restore`

`unity/Assets/Sim.Codec/InMemoryBlobPool.cs:71-98`:
- 号可为任意非负值,落在已有序号之上时**中间留空洞**(`null`),**不补零字节**(补零会让「未驻留」与「空载荷」不可分);
- 同号 **且内容相同** ⇒ 幂等 no-op(实跑修正,见 §四);
- 同号 **内容不同** ⇒ 抛(池不可变,ADR-006 G-2)。

### ⑤ `test_ac9g` 边界段翻转(V-1 的载体)

`unity/Assets/Tests/EditMode/ModularBuilding/world_load_service_test.cs:556-599`。

原文断言「空池 + 真存档 ⇒ 抛」并**自注**「TD-persist-09 兑现时本段会转红(届时须改成本测的反面)」
—— 本轮即那一反面,原文保留为 🔁 闭环记录。新段四层断言(逐层都能单独抓错):

| 断言 | 抓什么 |
|---|---|
| `AreNotSame(_pool, restoredPool)` | 前提:装回用的是**全新空池**(模拟进程重启) |
| `restoredPool.Count == 2` | 载荷段真的装回了 2 条(不是「零条目也算通过」) |
| `DoesNotThrow(Rebuild)` | 端到端:装回后重建不撞「查无此 BlobId」 |
| `Structures.Count == 2` 且 `r2b.Orientation == 90` | **载荷内容**逐位保持 —— 只断「不抛」不足以排除装错号 / 截断(截断后朝向会是 0) |

### ⑥ 接线

| 面 | 落点 |
|---|---|
| 写面(编码器 + 写档) | `unity/Assets/Gameplay.Boot/CompositionRoot.cs:557` — `blobPool: blobPool` |
| 读面(装载) | `unity/Assets/Gameplay.Boot/CompositionRoot.cs:701` — `blobRestoreSink: blobPool` |

> ⚠️ **两处传的是同一个实例** —— 这正是 §四 那条实跑修正的成因。

---

## 三、判据 V-1…V-7 —— 逐条证据

| # | 判据 | 载体 | 结论 |
|---|---|---|---|
| **V-1** | 往返(主判据):空池读档 → 装池 → `Rebuild()` 成功,结构与 id 逐条一致 | `world_load_service_test.cs:427` `test_ac9g_realSaveBytes_roundTripRebuildsWorld`(翻转段 `:556-599`) | ✅ |
| **V-2** | 引用驱动:已折叠病人的载荷**不进**载荷段 | `save_composer_wiring_test.cs:411` `test_payloadSegment_excludesFoldedPatientsBlobs`(带「不折 ⇒ 2 条」对照,缺它则「折后为空」与「本来就没有载荷」不可分) | ✅ |
| **V-3** | 折叠段自定长:段后仍有字节 ⇒ **不**判损坏 | `fold_row_codec_test.cs:132` `test_foldrowcodec_readSegment_stopsAtSegmentBoundary`(与 `:118` 的 `unconsumedRemainder_rejected` 吃**同一份**「段 + 尾巴」缓冲,结论相反) | ✅ |
| **V-4** | v1 拒绝:报文含 `v1` 与「载荷字节」,**不是**「没有对应迁移脚本」 | `save_slot_contract_test.cs:487` `test_load_prePayloadVersion_isRejectedWithDedicatedMessage`(断言含 `v1` / `载荷字节` / `信息不存在`,并 `DoesNotContain("没有对应迁移脚本")`) | ✅ |
| **V-5** | 缺 blob fail-closed:写档时抛,不是读档时 | `save_composer_wiring_test.cs:438` `test_payloadSegment_missingBlob_failsAtWriteNotAtLoad`(两种缺法:池里查无 / 根本没给池) | ✅ |
| **V-6** | 同 BlobId 只写一次 | `save_composer_wiring_test.cs:459` `test_payloadSegment_sameBlobIdWrittenOnce`(断言 `count == 1` **且**号正是被引用的那个) | ✅ |
| **V-7** | 全量回归无回退 | 见 §六 | ✅ |

**§④ / §⑥ 的附加钉子**(不属 V-1…V-7 编号面,但属本 ADR 裁决面):

| 判据 | 载体 | 结论 |
|---|---|---|
| 有载荷条目而无装载面 ⇒ 抛(不得静默跳过) | `world_load_service_test.cs:614` `test_load_payloadsPresentWithoutRestoreSink_isRejected` | ✅ |
| v1 档**不得下沉**进迁移链(版本门早于链) | `save_slot_contract_test.cs:510` `test_load_prePayloadVersion_neverReachesMigrationChain`(注册假迁移步,断言零调用) | ✅ |
| 版本门拒绝**不得锁槽** | `save_slot_contract_test.cs:536` `test_load_prePayloadVersion_doesNotLockSlot` | ✅ |

---

## 四、实跑修正(第一次全量跑的 6 红)

**首轮全量** `total 5710 / passed 5669 / failed 6 / skipped 35`,6 红全部由本轮改动引入:

| # | 症状 | 根因 | 处置 |
|---|---|---|---|
| 1–5 | `InvalidOperationException: blobId=0 已装载 —— 池不可变(ADR-006 G-2)`(`LoadSequencerTest` ×2 · `InventoryLoadServiceTest` ×2 · `world_load_service_test` ×1) | **我的设计错,不是夹具错**:生产装配根把**同一个** `InMemoryBlobPool` 同时用作编码写面与读档装载面 ⇒「本会话写过 → 本会话读回」这一最普通的情形必然命中同号 | `Restore` 改为「同号**同内容** ⇒ 幂等 no-op;同号**异内容** ⇒ 抛」。契约同步写进 `IBlobRestoreSink.cs:24-28` 与 `InMemoryBlobPool.cs:82-87` |
| 6 | `FoldedPredicateTest.test_saveService_takesFoldedPredicateViaConstructor_only`:Expected 12 but was 13 | 构造子新增第 13 个形参 `IBlobRestoreSink` | 期望值与形参串同批 +1,并加 🔁 注写明前十二位次序不变 |

**为什么这不是「把测试改绿」**:5 条红断言的守约对象是「池不可变」,而修正后**异内容仍抛**
(那条断言依然有判别力,见 `InMemoryBlobPool.cs:89-94`);变的只是「同号同内容」这一格的语义,
它是**两条路径共用同一实例**的必然结果,不改就会让「写后即读」永久不可用。

---

## 五、覆盖退步(如实登记)

**失去一条夹具**:`save_slot_contract_test` 原有 `test_load_olderSaveVersion_withChain_migratesAndLoads`
—— 「注册了对应脚本 ⇒ `SaveService.Load` **真的**迁过去并读档成功」的**集成层**正面路径。

**为什么无法保留**:v2 起 `[PayloadSegmentMinVersion, CurrentSaveVersion) = [2, 2)` 是**空区间**
⇒ 「`SaveService` 会调用链」这条接线**今日无任何可达输入**(造任何 `< 2` 的档都会被版本门先截住)。
不是「懒得写」,是判据在当前常量下**不可构造**。

**替代了什么**:反向钉 `test_load_prePayloadVersion_neverReachesMigrationChain`(`:510`)——
断言版本门**先于**链(注册假迁移步 + 断言零调用),有人把门挪到链之后立刻转红。

**没兜住的部分**:`SaveMigrationChain.Migrate` 本体有单元夹具(`save_migration_chain_test.cs`),
但**`SaveService` 与链的接缝**今日零覆盖。⇒ 已登记新债 `TD-persist-10`(见 §八)。

> ⚠️ **本行特意写清「没兜住」** —— 写「链本体有夹具」了事会把**接缝**的缺口伪装成已覆盖
> (承 `.claude/docs/review-workflow.md` 义务 4「评据不得越权」)。

---

## 六、回归(V-7)

**三元组**:截至时刻 **2026-10-28 23:16 CST** · 批次 `adr034-impl`(第三轮全量) · commit **`9ba8cbb6`** + 未提交工作树

| 套件 | 退出码 | XML | 基线 | 差 |
|---|---|---|---|---|
| EditMode | `0` | `total=5714 passed=5679 failed=0 inconclusive=0 skipped=35` | `5709 / 5674 / 0 / 35` | **+5 / +5** |
| PlayMode | `2` | `total=238 passed=234 failed=0 inconclusive=2 skipped=2` | `238 / 234 / 0 / 2 / 2` | **0 / 0 / 0 / 0 / 0** |

**等式**:`5714 − 5709 = 5` **恰等于** `git diff` 数出的新增 `[Test]` 条数;`5679 − 5674 = 5` 同。删除 0 条。

> ⚠️ **EditMode 基线 `5709` 是算术推得,不是本轮另跑的一次实测** —— 本轮改动前未单独跑全量。
> 依据:`git diff -U0 -- unity/Assets/Tests | grep -cE '^\+[^+].*\[(Test|TestCase)'` = **5**、
> 删除侧 = **0**(且 `git status` 无任何 `D` 条目)⇒ 既有用例集未被删改,`5714 − 5` 即基线。
> 首轮全量(`5710`)独立佐证了同一区间:它是**加 V-3 夹具之前**的形态(新增 4 条)。

**PlayMode 的 `exit=2` 与 `inconclusive=2` 不是失败**:两条是
`U1Assump6GamepadFocusSpikeTest.test_u1assump6_judgement{1,2}_notRun`,函数体内**逐字**
`Assert.Inconclusive(...)`(`u1_assump6_gamepad_focus_spike_test.cs:204` / `:377`,
自注「本 Inconclusive 保留为历史登记」)。该文件本轮 `git status` **零改动**,
且与存档面无关 ⇒ 与既有基线同值。

---

## 七、本轮自己的错误记录

| # | 错误 | 发现方式 | 处置 |
|---|---|---|---|
| ① | `SaveComposer.Compose` 形参序是 `… existingFoldedRows, bufferPool, blobPool`,我一度想按位置传 `(existingFoldedRows, blobPool)` —— 会把池绑到 `bufferPool` 上 | 编译前读签名 | 全部改用具名实参 `blobPool: …` |
| ② | 写过一条**恒真断言**:`StringAssert.Contains("未驻留", ex.Message.IndexOf("未驻留") >= 0 ? "未驻留" : "", …)` | 自查(本项目「自造断言」形态) | 改为 `StringAssert.Contains("未驻留", ex.Message, …)` |
| ③ | 起草 ADR-034 时称「`IBlobSink` **零**生产实现」 | 复算命令跑不出该结论(模式带冒号,`class X : IBlobPool, IBlobSink` 匹配不到) | 改「**零落盘**实现」,原文就地加注(已在 ADR-034 起草轮订正) |
| ④ | 曾据摘要口述「迁移链正面路径由 `save_migration_chain_test` 钉住」 | 本轮实测该文件**不走** `SaveService.Load` | 改登记为覆盖退步(§五);实现注释(`SaveService.cs:744-746`)原本已写「失去一条接线夹具」,措辞正确 |

---

## 八、新债

`TD-persist-10` —— `SaveService` ↔ `SaveMigrationChain` 的接缝无集成夹具(`[2,2)` 空区间所致)。
详见 `docs/tech-debt-register.md` 同名条目(含「何时自动消解」的判据)。

---

## 九、复算命令

```bash
# 1) 判据载体是否都在盘上(逐条给行号)
grep -n "test_ac9g_realSaveBytes_roundTripRebuildsWorld" \
  unity/Assets/Tests/EditMode/ModularBuilding/world_load_service_test.cs
grep -n "test_payloadSegment_" unity/Assets/Tests/EditMode/PersistenceService/save_composer_wiring_test.cs
grep -n "test_foldrowcodec_readSegment_stopsAtSegmentBoundary" \
  unity/Assets/Tests/EditMode/PersistenceService/fold_row_codec_test.cs
grep -n "test_load_prePayloadVersion_" unity/Assets/Tests/EditMode/PersistenceService/save_slot_contract_test.cs

# 2) 版本常量是 2(不是 1)
grep -n "CurrentSaveVersion = \|PayloadSegmentMinVersion = " unity/Assets/Sim.Codec/SaveHeader.cs

# 3) 接线两处传同一个池
grep -n "blobPool: blobPool\|blobRestoreSink: blobPool" unity/Assets/Gameplay.Boot/CompositionRoot.cs

# 4) 回归数复算(解析本轮 XML)
python3 - <<'PY'
import xml.etree.ElementTree as ET, os
p = os.environ['CLAUDE_JOB_DIR'] + "/tmp/editmode-results3.xml"
r = ET.parse(p).getroot()
print({k: r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
PY
```
