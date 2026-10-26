# 实现证据 —— foraging-003 三事件编排 + 身份铸造 + 恰一条 SkillGrown

> **日期**:2026-10-26
> **对象**:`production/epics/foraging/story-003-three-events-and-identity-mint.md`
> **被实现**:`unity/Assets/Sim/Foraging/HarvestOrchestrator.cs`(新,291 行)
> **被验证**:`unity/Assets/Tests/EditMode/Foraging/harvest_orchestrator_test.cs`(新,17 条)
> **裁定**:**COMPLETE WITH NOTES** —— AC-17-05 / AC-17-05b 记绿;
> **AC-17-01 不勾**(BLOCKED-BY 20 的两个写者);**R11 邻接半边**
>
> 🔁 **2026-10-26 订正(评审 + 修复轮后)**:本文件首版写于 `/code-review` **之前**,
> 其中「BLOCKING(实现缺陷)= 0」**已被评审推翻**。评审查出 **2 条 BLOCKING**
> (B-1 原子性自述为假 / B-2 事件序与 story 相反),均已修复并经变异证伪。
> 详见 `review-foraging-003-2026-10-26.md` §二 / §三 / §七。
> 夹具数亦由 **17 → 21**。原 §七 的「BLOCKING = 0」作为历史保留,不删。

---

## 一、交付面

| # | 文件 | 性质 | 说明 |
|---|---|---|---|
| ① | `unity/Assets/Sim/Foraging/HarvestOrchestrator.cs` | **新建** | 五步编排 + `IDropWriter` / `NullDropWriter` / `IGrowthEmitter` / `SkillGrownEmitterAdapter` · **315 行**(修复轮后) |
| ② | `unity/Assets/Tests/EditMode/Foraging/harvest_orchestrator_test.cs` | **新建** | **21 条**夹具(15 个方法,2 个参数化)· **646 行** |

> 🔁 首版为「四步编排 / 17 条夹具」。修复轮把 `Resolve` 的 ②③ 对调后成**五步**
> (`DropSpawned → ResourceHarvested → DropClaimed`),并新增 4 个 `TestCase`
> (B-1 三面 + B-2 次序)。

**门 A 合规**:`HarvestOrchestrator.cs` 住 `Sim`(`noEngineReferences: true`,引用集恰 = `{BCL, Sim.Contracts}`)——
纯 C# 编排,零引擎 API。测试件住 `Sim.Contracts.Tests`(Editor-only)。

---

## 二、AC 逐条(⚠️ 三档,禁合并成一条绿)

| AC | 判据 | 结果 |
|---|---|---|
| **AC-17-05** | 17 侧零本地铸造,唯一来源 = `IIdAuthority` | ✅ **绿**(4 条夹具,含 IL 构造点扫描) |
| **AC-17-05b** | 恰一条 `SkillGrown`,对象 = 品种,不因三事件多发 | ✅ **绿**(4 条夹具,含生产适配器真链路) |
| **AC-17-01** | 恰发三条既有 Kind | ⚠️ **不勾** —— BLOCKED-BY 20 的两个写者 |
| **R11 邻接** | 铸造点 = 出生事件当下(三载荷同一 id) | ⚠️ **半边**(17 侧自足那半已证;`DropSpawned` 无生产写者) |

### 2.1 AC-17-01 为什么不勾(必须如实说,禁借绿)

`entities.yaml`(ADR-024 唯一真源)逐字:

```
DropSpawned  author: "20 库存与物品(落地写者;触发方 ∈ {17 / 18 / 25 / 29} 见 constraint)"
DropClaimed  author: "20 库存与物品(主机裁决,唯一)"
```

而 `DropSpawned` 的 `constraint` 自己留了歧义警告:
> ⚠️ 17 侧「主机同 tick 连发」**未逐字点名 Drop\* 两支写者**,本登记采 ADR-009 §三/§五 + Amendment J ④
> 的一致口径……**实现期若 17 GDD 改口径回改本条**

**三处权威口径不一致**(2026-10-26 逐一核对):

| 来源 | `DropSpawned` / `DropClaimed` 写者 |
|---|---|
| `design/gdd/foraging.md:189-190`(规则二表) | **17(主机)** |
| `design/registry/entities.yaml`(`author:`) | **20 库存与物品** |
| `docs/architecture/adr-009:288-289`(§三 骨架) | **20 / 25**、**20** |

⇒ 本件**不裁定该冲突**(它跨 17/20 两个系统 GDD 与一份 Accepted ADR,须用户裁)。
本件的处置 = **结构上留好注入点**:`IDropWriter` 由构造注入,装配 `NullDropWriter` 时
17 只写 `ResourceHarvested` 一条 —— 这正是**今天**的真实状态,不是假装能跑的桩。

**可判伪的证据**:`test_ac1701_resourceHarvestedIsWritten_butDropEventsAreNot` 断
「`NullDropWriter` 装配下世界流只增 1 条」。20 落地后若有人换成真实现,这条**会红** ——
那正是「三连发终于能跑了」的信号,届时须更新它并复评 AC-17-01,而不是悄悄改绿。

**20 侧写者的实际落点**(勘察结论):`inventory-items/story-005-atomicity-consumption-recovery-no-ack.md`
第 47 行「统一写入事务模板(拾取/放下/消耗/建造扣/返还入箱共用)……`Append` 既有三支
(`DropClaimed`/`DropSpawned`/`DropDespawned`)」。⇒ AC-17-01 的真实前置 = **inventory story-005 Complete**。

### 2.2 R11 邻接的半边

已证:`test_r11_threeIdCarryingPayloadsShareTheSameInstanceId` 断
`ResourceHarvested`(真写)+ `DropSpawned` / `DropClaimed`(17 要求)三条载荷共用同一 id,
且 `test_r11_instanceIdIsMintedBeforeAnyWrite` 断一次采集**恰申请 1 个 id**(不是每条事件各申请一个)。

⚠️ **只三条,不是四条** —— `SkillGrown` 的载荷**不含 `instance_id`**(它按「对象 = 品种」归口,
不按实例)。把成长算进「同源」是概念错误。

未证:`DropSpawned` 本身没有生产写者 ⇒ 「id 真的从出生事件落进流」半条腿悬空。

---

## 三、两处实现期发现(须记)

### 3.1 生产适配器曾是「从未被执行过的一句自述」

首版 `harvest_orchestrator_test.cs` 的 15 条夹具**全部**注入 `SpyGrowthEmitter` ——
它只记录「编排器**请求**了成长」,**不证**「`SkillGrown` 真的进流」。
两者之间隔着 `SkillGrownEmitterAdapter`(生产类),而它在全仓**零调用点、零测试覆盖**。

这与项目记忆里记过的形态同族(sprint-16 音频 epic:`010/011/012` 测试跑自造 Fake、
生产类 grep 零命中)。**若不补,「成长已落流」就是一句从未被执行过的自述。**

**补件**:`test_ac1705b_productionAdapter_actuallyAppendsSkillGrownToHistoryStream` ——
用真 `SkillGrownEmitterAdapter(bag.EventSink, bag.Encoder)` 装配,断言:

- `SkillGrown` **真的**进了流(恰 1 条)
- 路由 = `StreamId.History`(病史流,不是世界流)
- `Patient = PatientId.None`(ADR-007 §四,不污染 `max(patient_id)` 高水位)
- 载荷**可解码**且逐字段等值:`ActorId` / `SkillId` / `ObjectId` / `Level = -1`(未升级哨兵)/
  `XpGain` 原样透传 / `PatientId = -1`

⚠️ 断言面刻意选**载荷内容**而非「流多了一条」:只数条数的话,编错 Kind / 编错载荷仍绿。

### 3.2 AC-17-05 的字面要求原先没收全

`AC-17-05` 原文 = 「反射扫描 sim 程序集的 `ItemInstanceId` **构造点**(type-surface 反射,非 grep),
唯一来源 = `IIdAuthority`」。首版三条夹具只扫了**类型面**:

- `test_ac1705_instanceIdComesFromIdAuthority_notLocallyMinted`(行为:发号序列)
- `test_ac1705_harvestOrchestratorType_hasNoLocalCounter`(字段面:无可变字段)
- `test_ac1705_simAssembly_hasExactlyOneIdAuthorityImplementation`(实现数恰一)

**缺口**:一个新增的写者完全可以在**另一个类型**里 `new ItemInstanceId(0)` 而三条全绿。

**补件**:`test_ac1705_simAssembly_hasExactlyOneItemInstanceIdCtorSite` ——
用 Mono.Cecil 直接读 `Sim.dll` 的 IL,收集全部 `ItemInstanceId` 的构造点
(`newobj` **与** `call .ctor`,`ItemInstanceId` 是 `struct` ⇒ 实路是后者),断言 ⊆ `{IdAuthority}`。
双向判据:上界(越界构造点 ⇒ 红)+ 下界(白名单零命中 ⇒ 红,防扫描面失效冒充绿)。

---

## 四、证伪轮(S12-4 · 强制)

**命中形态**:
① 定义域收窄(M1 本地铸造 / M6 成长对象 / M8 前置校验)
② 一次性幂等(M2 成长发两条)
③ 定序(M3 claimer 偏移 / M4 铸 id 拆两个)
④ 阈值边界(M5 `qty ≥ 1`)
⑤ 类型面(M7 可变字段)
⑥ **新增判据的判别力本身**(M9 IL 构造点)

### 4.1 首轮 7/8 —— M5 假绿(BLOCKING)

| 变异 | 结果 | 归因 |
|---|---|---|
| M1 本地铸造 id | `red=2` ✅ | 命中 |
| M2 成长发两条 | `red=4` ✅ | 命中(连带真红) |
| M3 claimer 偏移 | `red=1` ✅ | 命中 |
| M4 Spawn 另铸 id | `red=4` ✅ | 命中(连带真红) |
| **M5 `qty ≥ 1` → `> 0`** | **`red=0`** ❌ | **夹具假绿** |
| M6 成长对象 = 节点 | `red=2` ✅ | 命中 |
| M7 加可变字段 | `red=1` ✅ | 命中 |
| M8 停用前置校验 | `red=1` ✅ | 命中 |

**M5 的根因(按归因顺序,先查靶测再查变异)**:
`qty < 1` 有**两层**守卫 —— 编排器 `Validate`(`ParamName = "r"`)与写者 `Record`
(`ParamName = "qty"`)。停用上游后,下游**接住并抛出同一个异常类型**
⇒ 旧夹具「只断 `ArgumentOutOfRangeException` + 零事件」**仍然全绿**。
这是借绿:异常不再来自前置校验,而来自**已经铸了 id 之后**的写者。

> ⚠️ 与项目记忆里「共用 token 借绿」同型,但**症状不同**:那里是命中了别的守卫的**报文**,
> 这里是命中了别的守卫的**异常类型**。共同点 = 断言的判别维度比属性的判别维度粗。

### 4.2 M5 的修法(两条,一条比一条强)

| 补件 | 钉住的属性 | 说明 |
|---|---|---|
| `ex.ParamName == "r"` | 抛出点 = 编排器前置校验层 | 依赖参数名(实现细节),但足够区分两层 |
| `spyIds.CallCount == 0` | 非法请求**零消耗 instance_id** | **不依赖参数名**,只依赖「前置校验在发号之前」⇒ 更强 |

**为什么第二条更强**:id 铸造本身是副作用(推进机制 A 计数器)。非法请求若消耗了号,
下一次合法采集的 id 就会跳号 —— 这是可观测的语义破坏,而非实现细节。

### 4.3 第二轮:补测后全部通过

```
=== 基站(修夹具后)===
  total=17 passed=17 failed=0        ← 没把正确实现判红
── 施加 M5 ──
  total=17 passed=16 failed=1
    RED: test_invalidRequest_throwsBeforeAnyWrite
✅ M5 现已被夹具抓住
```

### 4.4 M9 —— 新增 IL 判据自身的判别力

新增的判据变绿 ≠ 它有判别力(「注释里声称有断言」的变体)。M9 在 `Sim` 内新增一个
**越界的 `ItemInstanceId` 构造点**(`_ = new ItemInstanceId(0);` —— 行为无影响,
只让 IL 多一个站点):

```
total=17 passed=16 failed=1
  RED: test_ac1705_simAssembly_hasExactlyOneItemInstanceIdCtorSite
✅ 且**恰只**它一条红
```

⇒ 两条结论同时成立:① IL 判据有效;② **行为测试对「越界构造点」零可见性** ⇒
IL 判据与行为判据**不可互替**(这正是新增它的理由)。

### 4.5 还原判据 = SHA-256 逐字节

```
HarvestOrchestrator.cs = d74b42ea2c907b3ef6be922c0358331c5f278fdf3beca290e927f8f2b8e71bf8
```

每轮「改 → 跑 → 还原」在**同一命令内**完成;每轮后与基线比对,不符即 `exit 9` 中止整轮。

⚠️ **一次作废重跑(如实登记)**:M5 补轮首跑时,我在基线跑测**进行中**编辑了测试文件
⇒ 基线与变异两轮的用例集不可比(15+1 新增 vs 16+1)。已 `TaskStop` 中止该轮、
核对被测文件零 `FALSIFY` 残留且 SHA-256 与基线一致,然后**测试文件定稿后**干净重跑。
承「证伪轮窗口内禁编辑被变异文件」——此处编辑的是**测试件**而非**被变异件**,
不属该禁令字面,但**同样破坏可比性**,故按同一纪律作废。

---

## 五、测试证据(三元组 · 可复算)

| 项 | 值 |
|---|---|
| 截至时刻 | 2026-10-26(本机) |
| 批次 | `f3-orch2`(夹具级)· `f3-full`(全量 EditMode) |
| commit | `22ffe86`(foraging-003 改动**未提交**,故为基线提交) |

| 级别 | total | passed | failed | skipped |
|---|---|---|---|---|
| **夹具级**(`f3-orch2`) | **16** | **16** | 0 | 0 |
| **夹具级**(`f3-m5-retry-base`,补 IL 测后) | **17** | **17** | 0 | 0 |
| **夹具级**(`f3-fix2`,**评审修复后**) | **21** | **21** | 0 | 0 |
| **全量**(`f3-full`,003 初版) | 5213 | 5178 | 0 | 35 |
| **全量**(`f3-full-final`,**评审修复后**) | **5217** | **5182** | 0 | 35 |
| 基线(`f2-full`,foraging-002 收口) | 5196 | 5161 | 0 | 35 |
| **增量(相对 f3-full)** | **+4** | **+4** | 0 | 0 |
| **增量(相对 f2-full 基线)** | **+21** | **+21** | 0 | 0 |

⇒ **+4 恰等于评审修复轮新增的用例数**(3 个 B-1 `TestCase` + 1 个 B-2);
**+21 恰等于本件夹具总数**。两者都对得上,零回归。

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 夹具级(须 17/17)
head -2 unity/Logs/f3-m5-retry-base.xml | grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"'
# 预期:total="17" passed="17" failed="0"

# ② 全量(须 5213/5178/0/35)
head -2 unity/Logs/f3-full.xml | grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"'
# 预期:total="5213" passed="5178" failed="0" inconclusive="0" skipped="35"

# ③ 失败用例数(须 0)
grep -c 'result="Failed"' unity/Logs/f3-full.xml

# ④ 无编译错误
grep -c "error CS" unity/Logs/f3-full.log

# ⑤ 证伪轮首轮 7/8(含 M5 逃逸)
grep -E "^\[M[0-9]\]|^7 / 8" /home/gu/.claude/jobs/181888b1/tmp/falsify_f3_run.log

# ⑥ M5 补轮(须 1 红)
cat /home/gu/.claude/jobs/181888b1/tmp/falsify_f3_m5_retry.log

# ⑦ M9 IL 判据判别力(须 1 红且恰只它)
cat /home/gu/.claude/jobs/181888b1/tmp/falsify_f3_m9.log
```

---

## 六、未及项(如实登记)

| 项 | 状态 |
|---|---|
| **AC-17-01**(三事件恰发) | **NOT-RUN / BLOCKED-BY** —— 20 的两个写者不存在(`inventory-items/story-005`) |
| **R11 邻接的落流半边** | **NOT-RUN** —— `DropSpawned` 无生产写者,id 是否真落流未证 |
| **三处权威口径冲突** | **处置已定,裁决锚待补** —— 评审 W-1:照 ADR-024 §① 取 `entities.yaml`(20)是**执行既有 ADR、非越权**;但 story `:17` 的 ADR 列表未登记该出处 |
| **PlayMode 集成测试** | **未建** —— story 声明的 `PlayMode/foraging_stream_events_test.cs` 不存在;本件证据全在 EditMode |
| **`gather_seq` 的节点计数** | 不属本件 —— 由 `ResourceHarvestWriter` 自算(foraging-002 前置) |
| **三事件同 tick 的全序键** | 未证 —— 需 20 落地后才能验 `(Tick, StreamPriority, Patient, Seq)` |
| **生产接线** | **未做** —— `HarvestOrchestrator` 零生产调用点(§7.1 ①) |
| **`Novelty` 求值** | **未做** —— 无求值方(§7.1 ②);照抄既有端口则「首次 ×3.0」永不触发 |
| **评审 W-2 / W-3 / W-4 / M-1 / M-3** | **未修** —— 见 `review-foraging-003-2026-10-26.md` §七 |

---

## 七、结论

| 项 | 结果 |
|---|---|
| BLOCKING(实现缺陷) | ~~**0**~~ → **2**(评审查出 B-1 / B-2)**均已修复 + 各自经变异证伪** |
| BLOCKING(证伪轮,首轮) | **1 —— M5 假绿,已修并重验通过**(第二轮 8/8 + M9) |
| BLOCKING(评审修复轮) | **0 遗留** —— M10/M11/M12/M13 四条变异各自命中 |
| AC 记绿 | **2/4**(AC-17-05 / 05b);AC-17-01 不勾;R11 半边 |
| 夹具 | **21/21** 绿(首版 17) |
| Falsifiability Gate | ✅ **PASS** —— 9/9 + 修复轮 4/4,共 **13 条变异全转红** |
| **裁定** | **COMPLETE WITH NOTES** |

### 7.1 交付性质(必须与「跑得起来」区分)

本件交付的是**形状完备的实现 + 自足的夹具**,**不是**可运行的采集链路。三条实测:

| # | 事实 | 判据 |
|---|---|---|
| ① | `HarvestOrchestrator` **全仓零生产调用点** | `grep -rn "HarvestOrchestrator\|IDropWriter\|IGrowthEmitter" --include=*.cs unity/Assets/` 只命中自身 + 测试件 |
| ② | `Novelty` **无生产者** | `grep NoveltyClass.` 只余常量定义(`PrescribeFlow.cs:202`)与守卫说明串;`PrescriptionPorts.cs:152` 自陈「novelty 仍取 Normal」 |
| ③ | `DropSpawned` / `DropClaimed` 写者 **不存在** | `WriterExistenceGate.cs:152-154` 早已登记为「20 落地写者未落」(2026-10-10) |

⇒ AC-17-01 不勾、R11 半边的根因就是 ③;①② 则是本件交付面之外、但会让链路跑不起来的前提。
