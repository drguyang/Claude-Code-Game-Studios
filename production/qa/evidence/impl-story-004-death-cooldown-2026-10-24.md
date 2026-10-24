# 实现证据:story-004 —— 死亡冷却 F-29-2(`DeathAllowed` 谓词 + 消费侧装配)

> **日期**:2026-10-24(实跑时刻见各表;UTC 与本地混用处逐处标注)
> **对象**:`production/epics/death-respawn/story-004-death-cooldown-from-stream.md`
> **判定**:谓词半边 **AC 全满足**(含 AC-29-15);证伪轮 **7/7 精确命中**;回归 **零失败**。
> ⚠️ **AC-29-08 / AC-29-10 全链仍 BLOCKED-BY-9,不记绿** —— 见 §五。
> **本文性质**:`coding-standards.md` §Review Evidence Standards 要求的**原件**
> (含「原判定 → 修复落点 → 验证命令」三列可证伪)

---

## 一、本件之前的状态(为什么这不只是「写一个谓词」)

```bash
$ grep -rn "new DeathCandidateConsumer\|new PlayerDiedWriter\|new InjuryStateChangedWriter" \
    unity/Assets --include='*.cs' | grep -v '/Tests/'
unity/Assets/Sim/EntityDeathGates.cs:50://  · 但 `InjuryStateChangedWriter` **自身零生产构造点** …
```

⇒ 三个消费侧类型**全部只有测试构造**。而产出侧虽已在 story-008 落地,消费侧从未接上
⇒ **死亡链在生产装配根处断开**(不是「缺一个实现」,是「装上了但没有人接」)。

更关键的一处:`CompositionRoot.cs:261` 当时写死 `actorClassOf = _ => ActorClass.Patient`。
那句对 **9 的 F4** 成立(只有病人有 course),但对 **29 的消费链不成立** ——
`DeathCandidateConsumer` 用**同一个面**判「该不该铸 `PlayerDied`」,恒 Patient
⇒ **类别闸永不放行 ⇒ 装了等于没装**。这是本件真正的起点。

---

## 二、落地清单(已落 / 已证 分列)

| ID | Landed | Proven | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| F-29-2 谓词 `DeathAllowed(actor,t)` | ✅ | ✅ | `Sim/DeathCooldownGate.cs:128-138` | `test_deathAllowed_exactlyAtCooldown_isAllowed` / `_oneTickBefore` / `_farBeyond`;证伪 **M1**(`>=`→`>`)⇒ 4 红 |
| `last_death_tick = max{Tick(e)}` | ✅ | ✅ | `DeathCooldownGate.cs:155-188` | `test_tryLastDeathTick_takesMaxNotLast`(乱序 `100/300/200`);证伪 **M2** ⇒ 2 红 |
| 空流首死恒允许 + 零哨兵事件 | ✅ | ✅ | `DeathCooldownGate.cs:134-135` | `test_deathAllowed_emptyStream_firstDeathAlwaysAllowed`(同时断言流长度 0);证伪 **M3** ⇒ 1 红 |
| **AC-29-15 零计数器** | ✅ | ✅ | `DeathCooldownGate.cs`(全类无状态字段) | **双判据**:结构 `test_zeroCounter_noPerActorStorageField` + 行为 `test_zeroCounter_rereadsStreamOnEveryEvaluation` |
| 逐 actor 独立 | ✅ | ✅ | 同上(纯函数性质) | `test_deathAllowed_actorsAreIndependent`;证伪 **M1** 连带红 |
| 重放确定性(实现注 4) | ✅ | ✅ | 同上 | `test_replay_sameEventSequenceGivesIdenticalAnswers`(两套独立装配重放同一序列,逐 tick 答案 `CollectionAssert.AreEqual`) |
| 配置域 `CD ≤ 0` 显式抛 | ✅ | ✅ | `DeathCooldownGate.cs:103-106` | `test_ctor_zeroCooldownThrows` / `_negativeCooldownThrows` / `_nullDependenciesThrow` |
| 消费侧装配(`Register`) | ✅ | ✅ | `CompositionRoot.cs:341` | `test_assemble_sinkIsRegisteredByAssembly`;证伪 **M4** ⇒ 1 红 |
| 类别面接真(玩家花名册) | ✅ | ✅ | `Sim/PlayerActorRegistry.cs`(新)· `CompositionRoot.cs:293` · `BootRoot.cs` | `test_registeredPlayer_producesOnePlayerDiedEndToEnd`;证伪 **M7** ⇒ 1 红 |
| `deathAllowed` 签名加 `tick` | ✅ | ✅ | `Sim/DeathCandidateConsumer.cs:45,70-82,99-101` | 编译期(一元面无法表达 F-29-2)+ 端到端测 |
| `death_cell` 流前缀查询 | ✅ | ⚠️ **部分** | `Sim/DeathCellQuery.cs`(新) | 端到端间接覆盖(载荷 `death_cell` 可解码);**「最后一条胜」无语义夹具** —— 见 §五 诚实边界 1 |
| `DEATH_COOLDOWN` 常量登记 | ✅ | ✅ | `design/registry/entities.yaml`(2026-10-24 补登记) | `test_registry_deathCooldownBlockExistsWithAllFields`(六字段)+ `test_registry_valueMatchesImplementationConst`;证伪 **M5** ⇒ 1 红 |

---

## 三、证伪轮(7 轮,全部精确命中)

**方法**:变异 → 跑 fixture → `cp` 还原 → **SHA-256 逐字节比对**(`sha256sum -c`)。
**未用** `git checkout` / `git stash`(承 `feedback_git_checkout_wipes_uncommitted`)。
**变异窗口**:`Sim/DeathCooldownGate.cs` · `Gameplay.Boot/CompositionRoot.cs` ·
`Sim/PlayerActorRegistry.cs` · `Gameplay.Boot/DiseaseVitalsService.cs` · `design/registry/entities.yaml`;
窗口内**未读**被变异文件;锚串唯一性由脚本 `assert count == 1` 强制(不唯一即退出码 2)。

| 轮 | 变异 | 红 | 红集 | 产物 |
|---|---|---|---|---|
| **M1** | 谓词边界 `>=` → `>` | 4 | `exactlyAtCooldown_isAllowed` / `actorsAreIndependent` / `zeroCounter_rereadsStream` / `assemble_exposesGate` | `tmp/falsify/m1.xml` |
| **M2** | `max{Tick}` → 取首条 | 2 | `takesMaxNotLast` / `zeroCounter_rereadsStream` | `tmp/falsify/m2.xml` |
| **M3** | 空流首死 `true` → `false` | 1 | `emptyStream_firstDeathAlwaysAllowed` | `tmp/falsify/m3.xml` |
| **M4** | 装配层拆 `deathCandidates.Register(...)` | 1 | `test_assemble_sinkIsRegisteredByAssembly` | `tmp/falsify/m4.xml` |
| **M5** | 登记值 `2400` → `2401` | 1 | `test_registry_valueMatchesImplementationConst` | `tmp/falsify/m5.xml` |
| **M6** | 拆产出侧类别闸 `if (_actorClassOf(actorId) != Player) return;` | 1 | `test_patientDeath_producesNoCandidate` | `tmp/falsify/m6.xml` |
| **M7** | 花名册 `ClassOfDeathCandidate` 恒 `Patient`(装了等于没装) | 1 | `test_registeredPlayer_producesOnePlayerDiedEndToEnd` | `tmp/falsify/m7.xml` |

> ⚠️ **M2 首轮是空的 —— 它抓出了我自己的一处假绿夹具(原文不删)**
>
> 原夹具写 `{At(300), At(100)}`。M2(取首条)在那里**也**返回 300
> ⇒ 该变异对 `test_tryLastDeathTick_takesMaxNotLast` **无效**,首轮只红了 1 条(且是别的原因)。
> 改为 `{At(100), At(300), At(200)}` 后三种实现的读数**两两可分**
> (首 = 100 错 / 末 = 200 错 / max = 300 对),M2 复跑转红 **2** 条。
> **教训**:「夹具刻意乱序」≠「夹具能区分实现」—— **乱序的位置**决定了它区分哪两种。

> ⚠️ **M7 首轮打错靶(原文不删)**:初版打的是 `BootRoot.cs` 的 `PlayerActors.Register`。
> 但新测试走的是 `bag.PlayerActors.Register(...)`(夹具自己登记),**不经 `BootRoot`**
> ⇒ 变异对目标测无效。改打 `PlayerActorRegistry.ClassOfDeathCandidate`(恒 Patient)——
> 那才是「装了等于没装」的真实形态,也是本件真正修掉的那处缺陷。

---

## 四、回归(带三元组)

| 套件 | 截至时刻 | 批次 | commit | 结果 | 产物 |
|---|---|---|---|---|---|
| fixture(本件) | 2026-10-23T23:35:47Z(XML `start-time`) | story-004 实现轮 | `3e8c5ca` + 工作树 | `18 / 18 / 0 / 0` | `unity/Logs/editmode-story004.xml`(套件节点 `DeathCooldownTest`) |
| EditMode 全量 | 2026-10-23T23:45:45Z(XML `start-time`) | story-004 实现轮(终轮) | `3e8c5ca` + 工作树 | `4736 / 4701 / 0 / 35` | `unity/Logs/s18-29story004-final-editmode-20261024.xml` |
| PlayMode 全量 | 2026-10-23T23:49:15Z(XML `start-time`) | story-004 实现轮(终轮) | `3e8c5ca` + 工作树 | `202 / 202 / 0 / 0` | `unity/Logs/s18-29story004-final-playmode-20261024.xml` |

**基线对照**(引用自旧件,**不是本轮实跑**,故不带本轮时间戳):

- EditMode 基线 `4717 / 4682 / 0 / 35` —— 引用自
  `production/qa/evidence/impl-story-008-death-candidate-sink-2026-10-24.md` §四
  @ 2026-10-23T22:50:55Z · `story-008 实现轮` · `d36390e` + 树。
- PlayMode 基线 `202 / 202 / 0 / 0` —— 引用自同一件 @ 2026-10-23T22:47Z · 同批 · 同 commit。
  本轮实跑与之一致(本件零新增 PlayMode 测试,属**预期**)。

### 增量等式(判据,不是修辞)

```
EditMode:  4736 − 4717 = +19   4701 − 4682 = +19   skipped 35 = 35   failed 0 = 0
PlayMode:  202  −  202 =   0    202 −  202 =   0                  failed 0 = 0
```

**全名级差集**(不是只比总数 —— 总数相等可以被「加一条删一条」满足):

```
新增 20 / 消失 1 ⇒ 净 +19 == Δtotal ✅
```

| 差集 | 条数 | 明细 |
|---|---|---|
| **新增** | 20 | `DeathCooldownTest.*` **18** 条(新文件)+ `DeathCandidateSinkTest.test_registeredPlayer_producesOnePlayerDiedEndToEnd` **1** 条(新测)+ `DeathCandidateSinkTest.test_assemble_sinkIsRegisteredByAssembly` **1** 条(**改名**) |
| **消失** | 1 | `DeathCandidateSinkTest.test_assemble_sinkIsNotYetRegistered` —— 被上一行**改名**取代(该测自己的注释里写明「29 装配轮落地时本测须同批改为已注册」⇒ **有账可查的改名**,非静默消失) |

⇒ 净 `20 − 1 = +19`,与 `Δtotal = +19` 相等;**零回归**。

**可复算命令**:

```bash
python3 -c "
import xml.etree.ElementTree as ET
def names(p):
    r=ET.parse(p).getroot()
    return {(tc.get('fullname') or tc.get('name')) for tc in r.iter('test-case')}
a=names('unity/Logs/s8-deathcandidate-editmode-20261024.xml')
b=names('unity/Logs/s18-29story004-final-editmode-20261024.xml')
print('新增',len(b-a),'消失',len(a-b),'净',len(b)-len(a))
for n in sorted(b-a): print(' +',n)
for n in sorted(a-b): print(' -',n)
"
# 期望:新增 20 消失 1 净 19
```

**可复算命令**:

```bash
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/editmode-story004.xml',
          'unity/Logs/s18-29story004-final-editmode-20261024.xml',
          'unity/Logs/s18-29story004-final-playmode-20261024.xml']:
    r=ET.parse(f).getroot(); a=r.attrib
    print(f.split('/')[-1], a['total'], a['passed'], a['failed'], a['skipped'], a['start-time'])
"
```

---

## 五、诚实边界(禁借绿)

1. **`DeathCellQuery` 无独立单测。** 「最后一条 `ActorCellEntered` 之 cell 胜」这条语义
   **没有专门夹具**,只有端到端间接覆盖(铸出的 `PlayerDied` 载荷 `death_cell` 可解码)。
   归 **story-005**(复活传送是它的主要消费者)补。
2. **`WorldSpawnCell = (0,1,0)` 是兜底,不是设计。** 玩家若从未跨格就死亡,`death_cell` 取该值
   —— 它是 `BootRoot.Teleport(new Vector3(0,1,0))` 经 `FloorToInt` **真实产生**的格,不是发明的。
   正确修法 = 1 在初始化时发一条 `ActorCellEntered`,但 **AC-29-09 明禁 29 追加该事件** ⇒ 归 1。
3. **`DEATH_COOLDOWN` 未走 ADR-014 配置注入。** 今日无 `death_cooldown` 的作者态 JSON / 烘焙产物
   ⇒ 值是 ctor 默认参数。注入缝已留(`cooldownTicks` 形参),数据面归**数值轮**。
4. **`DEATH_COOLDOWN` 未进 `DiseaseConstantRegistryGate.RequiredConstants`。**
   那个门是 **9 的常量门**;把 29 的量塞进去 = 跨域扩门。本常量的机械判据改由
   `death_cooldown_test.cs` 的 §⑧ 两条承担(块存在 + 值 = 实现 const)。
5. **AC-29-08 / AC-29-10 不记绿。**
   - **AC-29-08**(冷却内再次致死 ⇒ 不结算代价、不发 `PlayerDied`):**谓词半边**满足
     (`PlayerDiedWriter.cs:114-115` 在写流**之前**返回 `RejectCooldown`)。
     **全链**要求 9 的 F4 在生产上真推候选;而今日**玩家尚无染病路径** ⇒ 「玩家在冷却内
     再次致死」这一**场景**在生产上不可达。**不可达 ≠ 已验证**。
   - **AC-29-10**(复活来源的 `ActorCellEntered` 条数上界):本件**结构上**不产出该事件
     (AC-29-09),故不可能违反;但「上界成立」的**正面证据**须等 story-005 落地。
6. **本件的完成 ≠ 死亡链在生产上端到端可用。** 缺的是「玩家会染病」这条路径(9 侧)。
   `test_registeredPlayer_producesOnePlayerDiedEndToEnd` 证明的是**装配链本身通**,
   不是**游戏里会发生**。

---

## 六、复算命令(逐条)

```bash
# ① 18 条谓词测试
grep -c '^\s*\[Test\]' unity/Assets/Tests/EditMode/DeathRespawn/death_cooldown_test.cs

# ② 装配层消费侧注册点
grep -n 'deathCandidates.Register(deathConsumer)' unity/Assets/Gameplay.Boot/CompositionRoot.cs

# ③ 玩家登记点(生产)
grep -n 'PlayerActors.Register' unity/Assets/Gameplay.Boot/BootRoot.cs

# ④ 常量登记块(六字段齐 + 值)
python3 -c "
import re
s=open('design/registry/entities.yaml',encoding='utf-8').read()
m=re.search(r'^  - name: DEATH_COOLDOWN[ \t]*\r?$',s,re.M)
b=s[m.start():s.find('\n  - name: ',m.end())]
print('fields:',[f for f in ['status','source','referenced_by','value','unit','constraint']
                 if re.search(r'^[ \t]+'+f+r':[ \t]*',b,re.M)])
print('value:',re.search(r'value:\s*\"\s*(\d+)\s*tick',b).group(1))
"

# ⑤ 门 A 未受影响(Sim 引用集仍恰 = {BCL, Sim.Contracts})
python3 -c "
import json;d=json.load(open('unity/Assets/Sim/Sim.asmdef'))
print(d['references'], 'noEngineReferences=', d.get('noEngineReferences'))
"
```
