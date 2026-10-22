# 实现证据 · AC ③ 数据行(R2 战伤白名单五项)

**日期**:2026-10-22
**对象**:`assets/data/disease_registry.json`(新建)· `Sim/RegistrySchema.cs`(加 `TraumaHalfLife` + `R1_CHECK_32`)· `Editor.Tools.Gates/RuleNineGate.cs`(修 I2 假红)
**依据**:`design/gdd/disease-simulation.md` §R2 / §R1.3 / §二之一 ·
`death-and-respawn.md:96`(白名单闭集)· `OQ-25-7`(`combat-and-weapon-lines.md:1363`)·
`production/sprint-status.yaml` PRE-S18-A
**结论**:PRE-S18-A 的**伤情面**从「零数据行」→ **五行真数据 + 真校验器零违例**。
⚠️ **病种八行仍未建**(`DIS_*`),PRE-S18-A **未全部闭合**。

---

## 一、裁定面的精确边界(不许混读)

用户 2026-10-22 裁的是:

| 裁了 | 没裁 |
|---|---|
| 五项 `A_peak/SCALE` 比值(STAB/HEMORRHAGE=1.00 · CLAW=0.85 · BITE=0.75 · LACERATION=0.60) | `CRITICAL_THRESHOLD` / `COMA_THRESHOLD` / `DEATH_THRESHOLD` 三阈值 |
| 五项 `τ_peak`(200 / 300 / 500 / 600 / 700 tick) | `TREATMENT_WINDOW` / `CARE_GAP` / `RECOVER_THRESHOLD` |
| 效力 = **正式裁定** | 病种(`DIS_*`)八行的任何数值 |

> **「按现网 9000 算」= 拿它当计算基准验算可达死,不是裁定它为最终值。**
> 故 `entities.yaml` 的 `DEATH_THRESHOLD` 块 `value:` **仍是「待定」** ——
> 标 `decided` 会是借绿(把「用它算过」写成「裁过它」)。

---

## 二、`A_peak/SCALE` 的含义(为什么这个比值就是旋钮)

```
position_d(t)  = clamp(Progress_d(t) / SCALE_d, 0, 1)        -- F2
position_agg  = max_{d ∈ D} position_d(t)                    -- GDD :257
Progress 的自然项 = Base(τ) ⊔ Relapse(τ) + …
Base(τ ≥ τ_peak) = A_peak                                     -- 急性保持型
⇒ 单病种时  position_peak = A_peak / SCALE_d
```

⇒ **`A_peak/SCALE` 就是「该伤单独存在时的峰值 position」**,而
`position_agg ≥ DEATH_THRESHOLD` 是死亡门 ⇒ 该比值直接决定「能不能死」。

**统一取 `scale = 100`** 让 `A_peak` 直接等于百分数,便于人读(如 `INJ_CLAW` 是 `85`)。

⚠️ **`SCALE_d` 取消掉的那部分**:`Progress` 里战伤累积项是
`Σ magnitude × SCALE_d × Decay_injury`,再除以 `SCALE_d` ⇒ **`SCALE_d` 约掉**。
真正生效的是 `A_peak/Scale` 比值。这解释了为什么这个比值才是旋钮,而绝对量不是。

---

## 三、五项数据行

| id | key | `A_peak/Scale` | `τ_peak` | `τ_rise` | 可达死(校验 4b) |
|---|---|---|---|---|---|
| 2 | `INJ_STAB` | 1.00 | 300 | 40 | ✅ 7.3× |
| 3 | `INJ_LACERATION` | 0.60 | 600 | 80 | ✅ 4.4× |
| 4 | `INJ_HEMORRHAGE` | 1.00 | 200 | 30 | ✅ 7.3× |
| 5 | `INJ_CLAW` | 0.85 | 500 | 60 | ✅ 6.2× |
| 6 | `INJ_BITE` | 0.75 | 700 | 70 | ✅ 5.5× |

「×」= 相对现网 `DEATH_THRESHOLD = 9000 raw ≈ 0.1373` 的倍数。**五项最低者仍 4.4×** ⇒
无一退化为「过临界却同值不达死线」的无限迁延(校验 4b 的症状)。

**共同项**:`incubation=0` · `self_limit=false` · `plateau=false`(急性保持型)·
`boundary_mode="monotone"` · `trauma_half_life=60` · `Lethality=2` · `scale=100`。

> **为什么五项必须全是急性保持型**:GDD `:581` 的死亡支要求 `lethal ∧ self_limit=false`。
> 自愈型的伤**永远不会死人** —— 与 `death-and-respawn.md:96` 取舍理由 ①
> 「短兵可杀你、野兽可杀你」直接冲突。而校验 4 要求急性保持型必须 `lethal=true`。

---

## 四、新增的两件契约物

### 4.1 `DiseaseRegistryEntry.TraumaHalfLife`

GDD `:829-831` 要求 `kind: injury` 行带 `SCALE_d` + `trauma_half_life`,但两者
**都不在 schema 里**:

- `SCALE_d` = **既有的 `Scale` 字段**(同名同义,不是新字段)⇒ 无需新增;
- `trauma_half_life` = **真新增**。

值 `60`(3 s @ 20 Hz),`OQ-25-7` 已裁 2026-10-07。

### 4.2 `R1_CHECK_32` + `HalfLifeMax = 120`

```
kind: injury ⇒ 0 < trauma_half_life ≤ HALF_LIFE_MAX(120)
```

- **下界 0**:`trauma_half_life → 0` ⇒ `Decay → 0` ⇒ 连击完全不叠加,与幻想承诺 1 冲突;
- **上界 120**(A25a):`→ ∞` ⇒ `Decay → 1.0`(Q16.16 舍入可达)⇒ `1 − Decay = 0` ⇒ **A13 除零**;
- **只查 `kind: injury`** —— 其余 kind 该字段无意义(留 0),查它会造出
  「病种行必须填伤情字段」的假红。

---

## 五、⚠️ 同轮修掉的一个**门缺陷**(I2 假红)

### 症状

加 `R1_CHECK_32` 后,`RuleNineGateTest.test_ruleNine_realTree_ilHalfIsClean` 转红:

```
[RuleNine/I2] RegistrySchemaValidator::Validate —— 同一方法内以 3 个不同的
DiseaseKind 常量与 Kind 比较 (0, 2, 31) ⇒ 穷举式分支,规则九违例。
```

### 归因

`31` **不是 `DiseaseKind` 成员**(合法值域只有 0..3),它是 `R1_CHECK_31` 的异常码。
原 I2 判据是「`Kind` 装载后窗口 `i..i+3` 内的**任意** `ldc.i4` 都算」——
那个窗口会捞到与 Kind 无关的整数常量。

**⇒ 判据过宽 = 假红,与假绿同属静默失败。**

### 修法

收紧为:**常量必须紧跟比较指令**(`ceq` / `beq` / `beq.s` / `bne.un` / `bne.un.s`)
才计入 I2。`Kind == DiseaseKind.X` 的 IL 恒为 `ldfld Kind; ldc.i4.X; ceq`,
故两种相位仍覆盖,**判别力不损**。

### 判别力未丢的证据

`RuleNineGateTest` 的 16 条全过,其中:

- `ilHalf_injectedViolations_areRed` —— I1/I2/I3 三条夹具**各自**仍被抓到;
- `realTree_ilHalfIsClean` —— 真树恢复零违例。

⚠️ 若只跑前者会绿而后者仍红,就说明是判据坏了不是代码坏了 —— 两条一起看才成立。

---

## 六、判据测试(13 条)

`unity/Assets/Tests/EditMode/DiseaseSimulation/disease_registry_data_test.cs`

| 组 | 测试 | 证明什么 |
|---|---|---|
| ① 存在 | `test_dataFile_exists` | 文件在(空集冒充绿防线) |
| ① 行数 | `test_dataFile_hasExactlyTheFiveWhitelistRows` | 恰等于白名单闭集,不是 4 行也不是 6 行 |
| ② 真校验 | `test_rowsPassTheRealValidator` | **真** `RegistrySchemaValidator.Validate` 零违例 |
| ② 结构 | `test_allRows_areAcuteHoldType` | 五项均 `self_limit=false ∧ plateau=false` 且 `Lethality>0` |
| ② 常量 | `test_traumaHalfLife_isTheAdjudicatedValue` | 五项均 60(`OQ-25-7`) |
| ③ 可达死 | `test_allRows_canReachDeathThreshold` | `A_peak/SCALE ≥ ` **真** `StateMachine.DEATH_THRESHOLD` |
| ④ 注入 | `test_r1_32_zeroTraumaHalfLife_throws` / `test_r1_32_overMaxTraumaHalfLife_throws` | 新规则两端各自转红 |
| ④ 反向 | `test_r1_32_doesNotApplyToDiseaseRows` | 非 injury 行**不查**(防过宽) |
| ④ 注入 | `test_r1_23_apexAboveScale_throws` / `test_r1_22_acuteHoldWithoutLethal_throws` / `test_r1_01_duplicateId_throws` | 既有规则仍会红 |
| ④ 反向 | `test_missingRow_isDetected` | 少一行能被抓(不是「少一行也绿」) |

**实测**:`13 / 13 passed`(`Logs/regdata-test.xml`)。

> ⚠️ **③ 读的是真常量,不是抄数**:`new Fix(StateMachine.DEATH_THRESHOLD)`。
> 该常量一旦裁定成别的值,本条**自动**跟着变 —— 这才是判据,不是快照。
> 这也正是 §一 说的:阈值未裁,但「用现网值验算」是可以的,**前提是引用它而不是复制它**。

### ⚠️ 本测试的装载是**极简手写 JSON 读**,不是 binder

测试要验的是「**文件内容**能过校验器」,不是「某个 binder 能解析它」。
真装载走 ADR-014 阶段 2 的 `DiseaseRegistryBinder` —— **该 binder 尚不存在**,
故本条**不**构成「烘焙管线已通」的证据。`disease_registry.cooked` 一行未产。

---

## 七、连带修的一处既有测试

`RegistryBakeTest.test_r1_31_allDeclaredKinds_pass` 用 `_validEntries` 的
`Disease` 行改成 `Injury` 后断言「不抛」—— 新约束下该行缺 `trauma_half_life`,
**红的是 `R1-32`,不是 `R1-31`**,用例测错了对象。

修法:该用例验的是「kind 字段本身合法」,故须把该 kind 的**其余必填字段**补齐
(`TraumaHalfLife = 60`)。已加注说明理由。

---

## 八、可复算命令

```bash
cd unity

# 行数(应为 5)
python3 -c "import json;print(len(json.load(open('../assets/data/disease_registry.json'))['diseases']))"

# 白名单五项
python3 -c "import json;print(sorted(r['key'] for r in json.load(open('../assets/data/disease_registry.json'))['diseases']))"

# A_peak/Scale 与可达死
python3 - <<'PY'
import json
d=json.load(open('../assets/data/disease_registry.json'))
for r in d['diseases']:
    ap=int(r['natural_progress']['a_peak']); sc=int(r['scale'])
    print(f"{r['key']:16s} {ap/sc:.2f}  tau_peak={r['natural_progress']['tau_peak']:4d}")
PY

# 测试(13 条)
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.Unit.Sim.DiseaseRegistryDataTest" \
  -testResults "$PWD/Logs/regdata-test.xml" -logFile "$PWD/Logs/regdata-test.log"
```

---

## 九、结账表

| ID | Landed(已落) | Proven(已证) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| PRE-S18-A-1 | ✅ | ✅ | `assets/data/disease_registry.json` | `test_dataFile_hasExactlyTheFiveWhitelistRows` |
| PRE-S18-A-2 | ✅ | ✅ | 同上 + `RegistrySchema.cs` | `test_rowsPassTheRealValidator` —— **真**校验器零违例 |
| PRE-S18-A-3 | ✅ | ✅ | `RegistrySchema.cs`(`TraumaHalfLife` + `R1_CHECK_32`) | `test_r1_32_*` 三条(两端 + 反向) |
| PRE-S18-A-4 | ✅ | ✅ | `RuleNineGate.cs`(I2 收紧) | `ilHalf_injectedViolations_areRed` **与** `realTree_ilHalfIsClean` **同时**绿 |
| PRE-S18-A-5 | ✅ | ✅ | `registry_bake_test.cs` | 65/65(三测试面合跑) |
| **病种八行** | ✗ | ✗ | — | **未建** —— 数值未裁,建了就是假数据 |
| **烘焙管线** | ✗ | ✗ | — | **`DiseaseRegistryBinder` 不存在**,`.cooked` 一行未产 |
| **四阈值裁定** | ✗ | ✗ | `entities.yaml` 四块 `value:` 仍「待定」 | 用户未裁,不借绿 |

**结论**:AC ③ 的**伤情行**部分已兑现(五条 `Landed ✅ / Proven ✅`)。
**后三行刻意留 ✗** —— 病种、烘焙管线、阈值裁定都没做,一个字不粉饰。

---

## 十、全量回归

**基线溯源三元组**:截至 `2026-10-22` · 批次 `regress-ac3b` · 干净树。

```
total=4475  passed=4439  failed=0  inconclusive=0  skipped=36   error CS = 0
基线 regress-disconst = 4462 / 4426 / 0 / 36
Δtotal = Δpassed = +13     全名级差集 = 新增 13 / 消失 0
```

⚠️ **中间跑出过 2 条真回归**(`regress-ac3` 批:`4475/4437/2`),两条都由本轮改动引起,
均已修并复跑至全绿 —— 详见 §五 与 §七。**不是「一次就绿」**,把过程记在这里。
