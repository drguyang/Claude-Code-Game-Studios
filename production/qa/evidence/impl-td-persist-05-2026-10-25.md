# `TD-persist-05` 偿还 —— `AC-7a-14` 迁移链 + `AC-7a-16` authority 正交

**日期**:2026-10-25
**债**:`docs/tech-debt-register.md` §`TD-persist-05`(五条缺口中的 ③ ④)
**用户裁定范围**:④ + ③;②(`AC-7a-09`)与 ⑤(`AC-7a-20` 定时兜底)如实标 blocked,不写执行体

---

## 一、范围与理由(为什么只有 ④ ③)

| # | AC | 本轮 | 理由 |
|---|---|---|---|
| ① | `AC-7a-07` 异步写盘 + 退避重试 | ❌ 不做 | 与 `TD-persist-06` 的 `SaveOnExit` 限时 join **是同一改动两半**;分开会把同一段 join 逻辑写两遍 |
| ② | `AC-7a-09` q 系列符号扫描 | ❌ **blocked** | ADR-012 已裁「载体 = CI 门」,而 `TD-ci-01` 的 CI 矩阵本身 `NOT-RUN`。债册原文:「单独落 = 无强制点,不得记为有载体」 |
| ③ | `AC-7a-14` 迁移链逐版本 | ✅ **本轮兑现** | 无前置(链框架可独立于「历史上有哪些版本」而成立) |
| ④ | `AC-7a-16` authority 正交 | ✅ **本轮兑现** | 无前置(`Compose` 已是纯函数) |
| ⑤ | `AC-7a-20` 定时兜底半边 | ❌ **blocked** | `narrativeContext = null`(用户裁定丙)⇒ 兜底调 `Checkpoint` **必抛** |

**blocked 登记落点**:`docs/tech-debt-register.md` §`TD-persist-05` 的 ②⑤ 两行(见 §六)。

---

## 二、③ `AC-7a-14` —— 迁移链(逐版本 + 两不变量)

### 2.1 落点

| 文件 | 性质 | 说明 |
|---|---|---|
| `unity/Assets/Sim.Codec/ISaveMigration.cs` | **新增** | 单步脚本契约:`uint FromVersion` + `byte[] Migrate(byte[])` |
| `unity/Assets/Sim.Codec/SaveMigrationChain.cs` | **新增** | 链驱动:查表 / 逐版本 / **每级三条断言** |
| `unity/Assets/Sim.Codec/SaveService.cs` | 改 | `<` 分支从「刻意抛 `NotSupportedException`」换成调链;构造增第 8 形参 `migrations`;新增 `StripChecksum` |
| `unity/Assets/Sim.Codec/SaveHeaderCodec.cs` | 改 | 新增两个偏移常量(`SaveVersionOffsetFixed` / `WorldSeedOffsetFixed`) |
| `unity/Assets/Tests/EditMode/PersistenceService/save_migration_chain_test.cs` | **新增** | 12 条 |
| `unity/Assets/Tests/EditMode/PersistenceService/save_slot_contract_test.cs` | 改 | 2 条改写 + 1 条新增 |
| `unity/Assets/Tests/EditMode/PersistenceService/folded_predicate_test.cs` | 改 | 构造函数形参数 7 → 8(前七位逐位类型断言保持不变) |

### 2.2 三条设计选择(各自有理由,不是默认)

**① 链工作在「不含校验和」形态**
校验和覆盖它之后的全部字节(ADR-010 §四)⇒ 正文一变校验和必变。链在**最后统一**算一次 `AddChecksum`,不让每级各自算 —— 否则「某步忘了重算」会成为一个**静默的坏档来源**。
配套新增 `SaveService.StripChecksum`(逆操作),夹具 `test_stripChecksum_isInverseOfAddChecksum` 钉住往返。

**② 事件段按「末尾等长」锚定,不按固定偏移**
迁移改的通常是**头部字段**(加字段 / 改字段宽)⇒ 正文整体平移。用固定偏移取事件段会在跨版本时取错位置。改为「正文 = 存档**末尾**那一段」,迁前取其从末尾算的字节数,迁后要求同长同内容 ⇒ 与头部长度如何变化**无关**,正是 AC 要的口径。
夹具 `test_migrate_wellBehavedStep_keepsWorldSeedAndBody` **刻意让头部变长 4 字节** —— 若断言按固定偏移写,它自己就会红。

**③ 不变量由链守,不由各脚本自觉**
AC-7a-14 若交给每个脚本自己遵守,任一个忘了守,破坏都是**静默**的(`WorldSeed` 一变全案病程与掷骰同时作废)。链在**每一级**的进出处各断言一次,任一条不成立当场抛。

### 2.3 每级三条断言

| # | 断言 | 违反形态 |
|---|---|---|
| ① | 脚本执行后版本号**恰为** `v+1` | 脚本没把自己的版本号写进头部 ⇒ 下一级查表 miss,而 miss 的表现与真实缺陷不可分 |
| ② | `WorldSeed` **逐位不变** | 违反 AC-7a-14 / ADR-007 §二 |
| ③ | 事件正文**逐位不变** | 违反「已发生事件原样保留」 |

### 2.4 ⚠️ 本条**不实现任何真实迁移** —— 原因是历史 v0 从未存在

全库 `SaveHeader.CurrentSaveVersion = 1` 且零 `v0` 写入者 ⇒ **不存在真实的历史版本可以迁**。

若为此造一个「假 v0 布局再迁它」,测的是夹具自己的想象力,不是产品行为 —— 那是自造断言顶替 AC 语义层的变体。

⇒ 本文件测的是**框架会不会放过一个坏迁移**(四件事:逐版本驱动 / 两不变量 / 三类坏脚本须被拦),**不是**「v0→v1 迁得对不对」。后者要等 `CurrentSaveVersion` 第一次升到 2 时,拿**真实存在过的 v1 存档**来测。
**真实脚本落点 = `CurrentSaveVersion` 首次升到 2 的那一轮。**

### 2.5 行为变化(如实登记)

`Load` 的 `<` 分支**拒绝的行为没变,异常类型变了**:

| | 旧 | 新 |
|---|---|---|
| 类型 | `NotSupportedException` | `InvalidOperationException` |
| 报文 | 「升链尚未实现,归 TD-persist-03」 | 「断链:版本 N 没有对应迁移脚本(目标 M)—— 已注册 [..]」 |
| 触发条件 | 任何 `SaveVersion < 当前` | 同左(今日生产装配的链为空 ⇒ 仍全部拒绝) |

夹具 `test_load_olderSaveVersion_withEmptyChain_failsClosed`(改写)+ `test_load_olderSaveVersion_withChain_migratesAndLoads`(**新增正面对照**)。
⚠️ 正面对照不可省:缺了它,「拒绝」与「链根本没被调用」**不可分**。

---

## 三、④ `AC-7a-16` —— authority 正交

### 3.1 为什么不能只断言「两种布局产出一样」

若只断言这一点,那在「`Compose` 根本不认识 authority」的实现下是**恒真**的 —— 恒真断言对变异**零判别力**(本项目假绿形态之一)。

⇒ 拆成四条**互相独立**的性质,每条对应一种具体的打破形态:

| # | 判据 | 被打破的形态 |
|---|---|---|
| **A** | `Compose` 参数集**逐字**不含 authority 类型(具名清单,非 `IsAssignableFrom`) | 有人给 `Compose` 加 `IIdAuthority` 形参 |
| **B** | `SaveComposer` **无可变静态字段** | 加 static 缓存 / static 计数器 ⇒ 组版读到进程级状态 |
| **C** | 两种 authority 布局在场时产出**逐位一致** | 组版读取 ambient authority |
| **D** | `IdAuthority` 计数器是**实例字段**,两实例互不干扰 | 计数器改 `static` ⇒ 「可注入替身」退化为「全局单例」 |

A/B/D 是结构判据(直接钉住打破形态),C 是行为对照。四条各自可被一次变异单独打红。

### 3.2 落点

`unity/Assets/Tests/EditMode/PersistenceService/save_authority_orthogonality_test.cs`(**新增**,7 条)。
零生产代码改动 —— 因为 `Compose` 本来就没有 authority 形参,`IdAuthority` 本来就是实例计数器。
**本条兑现的是「把这四条性质钉住」**,不是「修一个缺陷」。

⚠️ 这一点须明写:④ 与 ③ 不同类型的债。③ 补的是缺失的执行体;④ 补的是**从未存在的判据**。
`sprint-18` 的教训(S18-1 `AC ⑥` 靠一句「有断言」撑了三轮)正是这一形态:AC 存在、实现也对,**但没有任何东西会因它被破坏而转红**。

---

## 四、回归(三元组)

**截至** `2026-10-25 04:21 UTC` · **批次** `td05-3`(全量 · **终态**)· **commit** `72bb234`
(工作树 = HEAD + 本轮改动)。起跑批次 `td05-2` @ `04:01 UTC`,两者逐位相同。

| 套件 | 结果 | XML | 对照 |
|---|---|---|---|
| EditMode(起跑) | **`4917 / 4882 / 0 / 35`** | `unity/Logs/td05-2.xml` | 前值 `4897 / 4862 / 0 / 35` ⇒ **+20 / +20,零回归** |
| EditMode(**终态重跑**) | **`4917 / 4882 / 0 / 35`** | `unity/Logs/td05-3.xml` | 与起跑**逐位相同** ⇒ 证伪轮七轮「改 → 跑 → 还原」之后,盘上代码与起跑时逐字节相同,零残留 |

**为什么要有终态重跑**:证伪轮期间被变异文件各改过一次(7 轮共 5 文件)。
「还原命令返回成功」只证明 `cp` 执行了,**不证明**跑过的代码 = 现在的代码。
终态重跑 + 五文件 `sha256sum -c` 两侧夹住,才使「终态绿」可追溯到具体字节。

**复算命令**:

```bash
python3 - <<'PY'
import re
for p in ['unity/Logs/savebytes-final.xml','unity/Logs/td05-2.xml']:
    s=open(p,encoding='utf-8').read()
    d=dict(re.findall(r'(\w[\w-]*)="([^"]*)"',re.search(r'<test-run[^>]*>',s).group(0)))
    print(p, [d[k] for k in ('total','passed','failed','inconclusive','skipped')])
PY
```

**新增 20 条的去向**(`4917 − 4897 = 20`):

| 文件 | 条数 |
|---|---|
| `save_migration_chain_test.cs` | 12 |
| `save_authority_orthogonality_test.cs` | 7 |
| `save_slot_contract_test.cs`(新增的正面对照) | 1 |
| **合计** | **20** |

⚠️ **首跑有一条红,已修**:`test_saveService_takesFoldedPredicateViaConstructor_only` 断言构造形参数 = 7,我加了第 8 个(`migrations`)。该断言**是合法的**(它守的是「FoldRow 不经形参进 Sim.Codec」),修法是按既有 `🔁` 模式改 7 → 8 并**补第 8 位的逐位类型断言** —— 原来只数个数,现在锁类型,判据面反而变宽了。

---

## 五、证伪轮

**还原判据 = SHA-256 逐字节**:基准 `td05-baseline.sha256`(3 文件)/ `td05-baseline2.sha256`(2 文件),
`sha256sum -c` 全部「成功」。

---

### 5.0 方法:每轮「改 → 跑 → 立即还原」,还原判据是哈希不是 grep

每轮只动**一个**守卫,跑全量 EditMode,记下**实得**红名单,**立即** `cp` 回去并 `sha256sum -c`。
变异窗口开启时声明「窗口内不读也不写被变异文件」,窗口在 `sha256sum -c` 通过后关闭。

**为什么不只跑目标夹具**:一条守卫可能被多条夹具同时覆盖,只跑一条会漏掉「其实有 3 条在守它」。
MB4 就实得 3 红而非预期的 1 红 —— 这类「预期之外的多红」恰恰是**判据面比我想的更宽**的证据,须如实登记。

### 5.1 七轮结果(逐轮可复算,`unity/Logs/td05-<轮>.xml`)

| 轮 | 变异(停用/改坏什么) | 实得红数 | 转红的测试 | XML |
|---|---|---|---|---|
| **MA1** | 每级断言①:`if (now != v + 1)` → `if (now > 1000u)` | **1** | `test_migrate_stepThatDoesNotAdvanceVersion_throws` | `td05-MA1b.xml` |
| **MA2** | 每级断言②:`if (seedNow != seedBefore)` → 恒假 | **1** | `test_migrate_stepThatChangesWorldSeed_throws` | `td05-MA2.xml` |
| **MA3** | 每级断言③:事件段 `FixedTimeEquals` 守卫 → 恒假 | **1** | `test_migrate_stepThatChangesBody_throws` | `td05-MA3.xml` |
| **MB1** | `SaveComposer` 加重载,带 `IIdAuthority` 形参 | **1** | `test_compose_signature_containsNoAuthorityParameter` | `td05-MB1.xml` |
| **MB2** | `SaveComposer` 加 `private static int _ambientAuthorityTick;` | **1** | `test_saveComposer_hasNoMutableStaticState` | `td05-MB2.xml` |
| **MB3** | `IdAuthority` 两计数器改 `static` | **2** | `test_idAuthority_countersAreInstanceFields` · `test_idAuthority_twoInstances_doNotShareCounter` | `td05-MB3.xml` |
| **MB4** | 断链守卫:整块 `throw` → `continue;`(断链静默跳过该级) | **3** | `test_migrate_brokenChain_throwsNamingMissingVersion` · `test_load_olderSaveVersion_withEmptyChain_failsClosed` · `test_load_olderSaveVersion_doesNotLockSlot` | `td05-MB4.xml` |

**合计 7 轮,10 条测试至少转红一次,零假绿**。

**复算命令**:

```bash
python3 - <<'PY'
import re
for n in ['MA1b','MA2','MA3','MB1','MB2','MB3','MB4']:
    s=open(f'unity/Logs/td05-{n}.xml',encoding='utf-8').read()
    d=dict(re.findall(r'([\w-]+)="([^"]*)"',re.search(r'<test-run[^>]*>',s).group(0)))
    names=[re.search(r'name="([^"]*)"',b).group(1)
           for b in re.findall(r'<test-case\b[^>]*>',s) if 'result="Failed"' in b]
    print(f"{n}: total={d['total']} passed={d['passed']} failed={d['failed']} -> {names}")
PY
```

### 5.2 逐条按「命中形态 → 反证」登记

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| **定序**(逐版本、按 `FromVersion` 而非注册序) | `test_migrate_multiStep_runsEveryVersionInOrder` 断言 `calls == [0,1,2]` | **间接**:该条未单独打;MB4 的 `continue` 变异使它所在路径行为改变(断链分支)。⚠️ **本条判据本身未获独立反证** —— 见 §5.4 |
| **定义域收窄**(坏脚本三类须被拦) | 三条 `..._throws` | MA1 → 版本号不前进守卫停用 ⇒ `stepThatDoesNotAdvanceVersion_throws` 红<br>MA2 → WorldSeed 守卫停用 ⇒ `stepThatChangesWorldSeed_throws` 红<br>MA3 → 事件段守卫停用 ⇒ `stepThatChangesBody_throws` 红 |
| **共存/互斥**(authority 两布局可并存) | `test_idAuthority_twoInstances_doNotShareCounter` | MB3 → 计数器改 static ⇒ 该条红(2 红之一) |
| **阈值边界 / 结构面**(参数集不含 authority) | `test_compose_signature_containsNoAuthorityParameter` | MB1 → 加重载带 authority 形参 ⇒ 该条红 |
| **结构面**(无可变静态态) | `test_saveComposer_hasNoMutableStaticState` · `test_idAuthority_countersAreInstanceFields` | MB2 → `SaveComposer` 加 static ⇒ 前者红<br>MB3 → `IdAuthority` 计数器 static ⇒ 后者红 |
| **一次性/fail-closed**(断链必抛) | `test_migrate_brokenChain_throwsNamingMissingVersion` + 两条 `SaveSlotContractTest` | MB4 → `throw` → `continue` ⇒ **3 条同时红** |

### 5.3 两处「预期之外」,如实登记

**① MB4 预期 1 红,实得 3 红。** 我原以为只有 `brokenChain` 那一条守断链。实得:
- `test_migrate_brokenChain_throwsNamingMissingVersion`(直接)
- `test_load_olderSaveVersion_withEmptyChain_failsClosed`(经 `SaveService.Load` 间接 —— 空链 = 断链)
- `test_load_olderSaveVersion_doesNotLockSlot`(同路径,且实测抛的是 `InvalidDataException`
  「缺折叠段」而非 `InvalidOperationException` —— 说明断链被跳过后**流继续走到 `ParseBody`**,
  在那里因旧布局缺折叠段才炸;这正是「静默跳过」的危害形态:**它没报断链,报的是别的错**)

⇒ 这不是「一红覆盖三判据」的反向问题,而是**一条守卫有三层防线**:链层面 `brokenChain`(直测)、
`SaveService` 层面两条(一条测拒绝、一条测不锁槽)。三层都转红 = 判据面比预期宽,**是好事**。

**② MB3 实得 2 红。** `countersAreInstanceFields`(结构面)+ `twoInstances_doNotShareCounter`(行为面)
是**两条独立断言**,不是「一条测试两个判据」。两者都转红说明结构面与行为面**同时**被钉住 —— 单靠任一条,
另一种打破形态会逃逸(例如只改 `_nextPatient` 为 static 会让行为面红但若没有结构面,「加一个 static
缓存但行为暂未暴露」就无人管)。

### 5.4 ⚠️ 未获反证的一条:`test_migrate_multiStep_runsEveryVersionInOrder`

**判据**:`calls == [0,1,2]` —— 断言「逐版本、按版本序执行,不受注册序影响」。

**它的可证伪形态是什么?** 若链改成**按注册序**执行而非按 `FromVersion` 查表,该条应红。但:
- 我这一轮**没有**构造这个变异(测试里注册序是 `2U, 0U, 1U`,若真按注册序跑会得 `[2,0,1]`;
  但按注册序跑会让第一步就写版本 3,后续查表直接 miss ⇒ 多半抛异常而非静默得 `[2,0,1]`)。
- 若链把循环改成「只跑最后一级」或「跳过中间级」,MB4 的 `continue` 变异**部分**覆盖了后者
  —— 但那轮该条**没有转红**(3 红里不含它)。

⇒ **本条按 `NOT-RUN` 登记,不借 MB4 的绿**。「逐版本」这条 GDD 字面(`k → k+1 → … → N`)目前**没有**
一条独立反证记录。补法:下一轮加一条变异(把 `for (uint v = from; ...)` 改成只跑 `from` 一级),
要求该条转红。**本轮不做**,因为已还原且终态已重跑,再加一轮会让「终态跑时盘上代码 = 现在盘上代码」
的哈希链条多一个环(见 §5.5)。

### 5.5 哈希链条:终态跑时的盘上代码 = 现在盘上代码

证伪轮改过 5 个文件,终态重跑 `td05-3.xml` 是在**还原之后**跑的。为了让「终态结论」可追溯到具体字节:

```
$ sha256sum -c td05-baseline.sha256 && sha256sum -c td05-baseline2.sha256
Assets/Sim.Codec/SaveMigrationChain.cs: 成功
Assets/Sim.Codec/SaveService.cs: 成功
Assets/Tests/EditMode/PersistenceService/save_slot_contract_test.cs: 成功
Assets/Gameplay.Boot/SaveComposer.cs: 成功
Assets/Sim/ItemDatabase/IdAuthority.cs: 成功

$ grep -rn "FALSIFY" Assets/Sim.Codec/ Assets/Tests/EditMode/PersistenceService/
（0 处）
```

⚠️ **MA1 首跑（`td05-MA1.log`）没有产出 XML** —— 只有日志尾部的
`Test run completed. Exiting with code 2 (Failed). One or more tests failed.`。
「红 1」这个**数字**在当时无法点名是哪条测试。⇒ 已**补跑一轮**（`td05-MA1b.xml`，实得 1 红并点名），
现登记用的是补跑结果。**首跑那次不计入证据面**（无 XML = 无名单 = 不可复算），只作为
「为什么会补跑」的记录保留。

**六轮原始日志全部留存**（`td05-MA1.log` / `td05-MA1b.*` / `td05-MA2.*` / `td05-MA3.*` /
`td05-MB1.*` / `td05-MB2.*` / `td05-MB3.*` / `td05-MB4.*`），XML 均为 4917 条全量。

---

## 六、blocked 两行的登记落点(② `AC-7a-09` · ⑤ `AC-7a-20` 定时兜底)

两条**不写执行体**,理由不是「没时间」,是**写了也无强制点 / 写了必抛**:

| # | AC | blocked-by | 为什么不写执行体 |
|---|---|---|---|
| ② | `AC-7a-09`(q 系列符号扫描) | **`TD-ci-01`** | ADR-012 已裁「该 AC 的载体 = **CI 门**」;而 `TD-ci-01` 的 CI 矩阵本身 `NOT-RUN`。⇒ 单独落一个扫描器 = **没有东西会跑它** ⇒ 登记成「有载体」即是借绿,而债册原文已明写「单独落 = 无强制点,不得记为有载体」 |
| ⑤ | `AC-7a-20` **定时兜底半边** | **`TD-persist-04` 动作 ①**(瞬时源) | 定时兜底要回答「此刻是否安全」,而这套判据 = `TD-persist-04` 动作 ① 的**瞬时源**(累积 vs 瞬时之争尚未裁)。今日生产装配 `narrativeContext = null`(用户裁定丙)⇒ 兜底若调 `Checkpoint` **必抛** ⇒ 写了也只能测「抛了」,测不到兜底本身 |

**两行已落 `docs/tech-debt-register.md` §`TD-persist-05`**(逐条加注,原文不删)。

⚠️ **未写的不是「已完成」**:`AC-7a-09` 与 `AC-7a-20` 定时兜底半边的判定**维持 `PARTIAL` / 无载体**,
**不因本轮而改变**。本轮只把它们从「笼统归于 05」变成「具名 blocked + 具名阻塞方」。

---

## 七、本轮不做的一条,以及为什么

**① `AC-7a-07`(异步写盘 + 退避重试)本轮不做** —— 与 `TD-persist-06` 的 `SaveOnExit` **限时 join**
是**同一改动的两半**:异步写盘一旦引入,「退出保存」就必须在限时内 join 那个后台 worker;
而 join 的限时语义 = `SAVE_EXIT_RETRY_TIMEOUT`,归 06。分开做 = 同一段 join 逻辑写两遍,
且第二遍会与第一遍的语义漂移。⇒ 合并到 06 的轮次。

**一个额外发现(比债册记的更深)**:`AC-7a-07` 违反**不只**「写盘 IO 在主线程」——
SHA256 **也在**主线程算:`SaveComposer.Compose:118` 的 `AddChecksum` 而 `Compose` 在
`Checkpoint:298` **主线程**调用。⇒ GDD 那句「写盘 IO **与 SHA256** 都在非主线程」**两半都没兑现**。
债册原记只点了写盘那一半。此发现已在证据件登记,是否据此改写 `TD-persist-06` 的描述待用户裁。
