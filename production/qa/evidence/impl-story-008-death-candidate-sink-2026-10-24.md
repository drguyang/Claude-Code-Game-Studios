# 实现证据:story-008 —— 死亡候选产出侧(`IDeathCandidateSink`)与 F4 接线

> **日期**:2026-10-24(实跑时刻见各表;UTC 与本地混用处逐处标注)
> **对象**:`production/epics/disease-simulation/story-008-death-candidate-sink.md`
> **判定**:AC-9-21…28 **全部满足**;证伪轮 **5/5 精确命中**;回归 **零失败**
> **本文性质**:`coding-standards.md` §Review Evidence Standards 要求的**原件**
> (含「原判定 → 修复落点 → 验证命令」三列可证伪)

---

## 一、本件之前的状态(为什么这不是「实现一个接口」)

```bash
$ grep -rn "IDeathCandidateSink" unity/Assets --include='*.cs'
unity/Assets/Sim.Contracts/IDeathCandidateSource.cs:57:    public interface IDeathCandidateSink
unity/Assets/Sim/DeathCandidateConsumer.cs:27://   - 生产装配(9 侧 `IDeathCandidateSink` 实现 + 本类注册)归 9 / 29 的装配轮。

$ grep -rn "\.Publish(" unity/Assets --include='*.cs'
(零命中 —— 含 Tests/)
```

⇒ 产出侧接口**只有声明、零实现**;`Publish(` **全树零命中**(连测试都没调过一次)。

---

## 二、落地清单(已落 / 已证 分列)

| ID | Landed | Proven | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| AC-9-21 实现存在 + 住 Sim | ✅ | ✅ | `Sim/DeathCandidateSink.cs:62` | `test_sink_implementsInterface_andLivesInSim`;门 A 见下行 |
| AC-9-21 零引擎引用 | ✅ | ✅ | 同上 | `test_simAssembly_referencesZeroEngineAssemblies` + 证伪轮 **M5**(靶换成 `UnityEngine.MonoBehaviour` 的装配 ⇒ 转红) |
| AC-9-22 单消费者 | ✅ | ✅ | `DeathCandidateSink.cs:80-93` | `test_register_firstSucceeds` / `test_register_secondThrows` / `test_register_nullThrows` / `test_publish_withoutConsumer_isSilentAndDoesNotThrow` / `test_publish_forwardsToRegisteredConsumer` + 证伪轮 **M4** |
| AC-9-23 生产调用点 | ✅ | ✅ | `DiseaseVitalsService.cs:409`(`Publish(` 在 `:459`) | `test_stepAll_publishesWhenEnteringDeceased` / `test_publishTickIsTheTransferTick` + 证伪轮 **M3** |
| AC-9-24 类别闸 | ✅ | ✅ | `DiseaseVitalsService.cs:457` | `test_patientDeath_producesNoCandidate` + `test_playerClass_producesCandidate`(反向夹具)+ 证伪轮 **M1** |
| AC-9-25 吸收态不重复发 | ✅ | ✅ | `DiseaseVitalsService.cs:456`(转移闸) | `test_deceasedIsAbsorbing_noRepeatedPublish` + 证伪轮 **M2** |
| AC-9-26 类别来源 = 注入面 | ✅ | ✅ | `DiseaseVitalsService.cs:167` / `:599-602` | `test_patientDeath_producesNoCandidate`(经装配默认面)与 `test_playerClass_producesCandidate`(经注入面)**同一病种、同一 tick 数、仅类别不同** |
| AC-9-27 `CompositionRoot` 构造 + 暴露 | ✅ | ✅ | `CompositionRoot.cs:253` / `CompositionRootServices.cs:85` | `test_assemble_exposesSink`;`grep -rn "new DeathCandidateSink" unity/Assets --include='*.cs' \| grep -v '/Tests/'` ⇒ `CompositionRoot.cs:253` |
| AC-9-28 诚实边界双处登记 | ✅ | ✅ | `Sim/DeathCandidateSink.cs:33-40` · `design/gdd/death-and-respawn.md`(规则二注块) | 两处均可 grep;且**边界本身被测试钉住**:`test_patientDeath_producesNoCandidate` 证明「病人死≠玩家候选」 |

> ⚠️ **AC-9-26 的判据在实现期被就地订正**(原文不删)。原 `verify_cmd` 写「零命中(注释除外)」,
> 但实测唯一非注释命中是**构造里的未接线回退** `_actorClassOf = actorClassOf ?? (_ => ActorClass.Patient)`
> —— 那个回退是**刻意的**(与 `treatableBy` / `trendWindowTicks` 的 `null = 未接线` 同格,
> 承 `DiseaseVitalsService.cs:117-127` 既有口径),不是「硬编码」。
> 判据改为:**F4 求值路径零命中**(`EvaluateOutcome` 的调用点走 `_actorClassOf(pid)`)。

---

## 三、证伪轮(5 轮,全部精确命中)

**方法**:变异 → 跑 fixture → `cp` 还原 → **SHA-256 逐字节比对**。
**未用** `git checkout` / `git stash`(承 `feedback_git_checkout_wipes_uncommitted`)。
**变异窗口**:仅 `unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs` 与 `unity/Assets/Sim/DeathCandidateSink.cs`;
窗口内**未读**被变异文件;锚串唯一性由脚本 `assert count == 1` 强制(不唯一即退出码 2)。

| 轮 | 变异 | 红 | 红集 | 产物 |
|---|---|---|---|---|
| **M1** | 拆类别闸 `if (_actorClassOf(actorId) != ActorClass.Player) return;` | 1 | `test_patientDeath_producesNoCandidate` | `unity/Logs/s8-deathcandidate-m1.xml` |
| **M2** | 拆吸收态去重 `if (previous == InjuryState.Deceased) return;` | 4 | `test_deceasedIsAbsorbing_noRepeatedPublish`(实测 60 次)/ `test_playerClass_producesCandidate`(10)/ `test_publishTickIsTheTransferTick`(10)/ `test_stepAll_publishesWhenEnteringDeceased`(10) | `unity/Logs/s8-deathcandidate-m2.xml` |
| **M3** | 拆生产调用点 `PublishDeathCandidateIfTransferred(...)` | 4 | 同 M2 红集,但**实测值全 = 0**(零发) | `unity/Logs/s8-deathcandidate-m3.xml` |
| **M4** | 单消费者抛 → 静默覆盖 | 1 | `test_register_secondThrows` | `unity/Logs/s8-deathcandidate-m4.xml` |
| **M5** | 门 A 断言的靶换成 `typeof(UnityEngine.MonoBehaviour).Assembly` | 1 | `test_simAssembly_referencesZeroEngineAssemblies` | `unity/Logs/s8-deathcandidate-m5.xml` |

### ⚠️ M2 与 M3 红集相同 —— 已排除「变异打错靶」

两轮红集**名字**一样,但**失败原因截然不同**,故不是同一变异的两次观测:

| | M2(去重缺失) | M3(调用点缺失) |
|---|---|---|
| `test_deceasedIsAbsorbing_noRepeatedPublish` | `Expected: 1 / But was: 60` | `Expected: 1 / But was: 0` |
| `test_playerClass_producesCandidate` | `Expected: 1 / But was: 10` | `Expected: 1 / But was: 0` |

**多发 vs 零发**是两种相反失效模式。另:M2 只让「恰 1 次」类断言红,
`test_patientDeath_producesNoCandidate` **保持绿** —— 因为类别闸仍挡着(病人本就不该发)。
这正是 M1 存在的意义:拆掉类别闸后,恰好**只有那一条**转红。

### 还原校验

```bash
$ sha256sum unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs unity/Assets/Sim/DeathCandidateSink.cs
b78431eb6d723db56adf58bee6b8494d53ced21e0c924aac9cf1ac48cea7e728  .../DiseaseVitalsService.cs
e485e8a4696cae2b8d994275674b04c6f3152a80caaebe4304fa6d28664b7e5c  .../DeathCandidateSink.cs
```

与变异前记录的哈希**逐位相同**。测试文件 M5 还原后:
`3cf3a517d02e6a667b5c36b016f9b586fd21b2e269747097d35e592e8a17ce28`(与备份一致)。

---

## 四、回归(带三元组)

| 套件 | 截至时刻 | 批次 | commit | 结果 | 产物 |
|---|---|---|---|---|---|
| fixture(本件) | 2026-10-23T22:52Z(见 XML `start-time`) | story-008 实现轮 | `d36390e` + 工作树 | `16 / 16 / 0 / 0` | `unity/Logs/s8-deathcandidate-fixture.xml` |
| EditMode 全量 | 2026-10-23T22:50:55Z(XML `start-time`) | story-008 实现轮 | `d36390e` + 工作树 | `4717 / 4682 / 0 / 35` | `unity/Logs/s8-deathcandidate-editmode-20261024.xml` |
| PlayMode 全量 | 2026-10-23T22:47Z(同批) | story-008 实现轮 | `d36390e` + 工作树 | `202 / 202 / 0 / 0` | `unity/Logs/s8-deathcandidate-playmode-20261024.xml` |

**基线对照**(引用自旧件,**不是本轮实跑**,故不带本轮时间戳):

- EditMode 基线 `4701 / 4666 / 0 / 35` —— 引用自 `unity/Logs/oq7a10-restored.xml` @ 2026-10-24 05:51
  (sprint-18 OQ-7a-10 轮,commit `fde4af7` + 树)。
- PlayMode 基线 `202 / 202 / 0 / 0` —— 引用自 `production/qa/smoke-2026-10-22-sprint18-final.md:37`
  @ 2026-10-22 07:27 +0800 · `s18-playmode-full-20261022` · `220acc3`。本轮实跑与之一致。

### 增量等式(判据,不是修辞)

```
EditMode:  4717 − 4701 = +16   4682 − 4666 = +16   skipped 35 = 35
fixture:   16 条测试
⇒ total 增量 == 本件新增测试数,零回归
```

**可复算命令**:

```bash
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/s8-deathcandidate-editmode-20261024.xml',
          'unity/Logs/s8-deathcandidate-playmode-20261024.xml',
          'unity/Logs/s8-deathcandidate-fixture.xml']:
    r=ET.parse(f).getroot()
    print(f, r.get('total'), r.get('passed'), r.get('failed'), r.get('inconclusive'), r.get('skipped'))
"
```

预期输出:

```
unity/Logs/s8-deathcandidate-editmode-20261024.xml 4717 4682 0 0 35
unity/Logs/s8-deathcandidate-playmode-20261024.xml 202 202 0 0 0
unity/Logs/s8-deathcandidate-fixture.xml 16 16 0 0 0
```

> ⚠️ **可复现性边界(如实写明)**:`unity/Logs/` 被 `.gitignore:251` 忽略
> (`git check-ignore -v unity/Logs/s8-deathcandidate-m1.xml` ⇒ 命中)⇒ **XML 产物不入库**,
> 只存在于本机。上面的数字在别处**不能靠 `ls` 复现**,只能靠**重跑**复现。
> 重跑命令(任一套件):
> ```bash
> ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode \
>   -projectPath "<repo>/unity" -runTests -testPlatform EditMode \
>   -testResults "<out>.xml" -logFile "<out>.log"     # 注意:不带 -quit
> ```
> ⇒ 引用本件数字的下游件**须连同「需重跑」一并抄**,不得把路径当成「随时可查」。

---

## 五、未及核查项(禁借绿 —— 如实列出)

1. ~~**EditMode 全量回归在本文落笔时仍未出结果**~~ ⇒ **已回填**(2026-10-23T22:50:55Z 跑完,
   `4717 / 4682 / 0 / 35`,增量等式成立,见 §四)。原文保留作过程记录。
2. **`S18-AC5` 未结清,且本件不解它**。该条 `verify_cmd` 探的是**消费侧**构造
   (`new DeathCandidateConsumer|new PlayerDiedWriter|new InjuryStateChangedWriter`),
   而 AC 原文说的是「**产出**路径有生产调用点」—— **两半不是同一件事**。
   本件只满足前半句的字面;消费侧仍因 29 story-004 的 `DeathAllowed` 冷却门未实现而断着。
   ⇒ `S18-AC5` 应维持 `open`,并须**订正判据**(把「产出」与「消费」拆成两条),归记账轮。
3. **`DeathCandidateConsumer` 的生产构造 = 0**。本件的 `Register` 在装配层**未被调用**
   (`test_assemble_sinkIsNotYetRegistered` 把这一现状**钉成断言**,避免它被当成疏忽)。
4. **玩家染病路径不存在** ⇒ 生产上 `DeathCandidate` 恒不因玩家产生。这是 AC-9-28 的边界,
   不是缺陷,但**任何**「死亡链已通」的读法都是错的。

---

## 六、实现期发现的两处原稿错误(原文不删,就地订正)

1. **T4/T5/T6 的驱动 tick 数**:原稿按「`A_peak/SCALE = 0.70 ≥ DEATH = 0.58` ⇒ 判死」推算,
   漏了 F4 死亡支的第四条件 `t − last_intervention > TREATMENT_WINDOW(3600)`。
   若照原稿写,夹具会在 `t = 60` 断言 `Deceased`,而实际是 `Coma` ⇒ **测的不是死亡**。
   实测改取 `StateMachine.TREATMENT_WINDOW + 10`。
2. **AC-9-26 的 `verify_cmd`** 写「零命中(注释除外)」,与 Implementation Note 2
   要求的「`null` ⇒ 回退 `_ => ActorClass.Patient`」**自相矛盾**(回退本身就是一处代码命中)。
   已就地改为「F4 求值路径零命中」。
