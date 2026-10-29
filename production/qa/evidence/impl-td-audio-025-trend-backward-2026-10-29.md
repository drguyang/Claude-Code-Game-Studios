# 实现证据 —— `TD-audio-025`:13 改吃后向差分(9 侧 trend 不再是恒 0 占位)

- **日期**:2026-10-29
- **批次**:音频债批(用户裁定「audio-system 6」中的唯一代码可动作项)
- **用户裁定**:`AskUserQuestion` ⇒ **甲:13 改吃后向差分(推荐)**
- **对象**:
  - `unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs`(`GetVitals` 的 trend 来源)
  - `unity/Assets/Gameplay.Presentation/PatientAI/BehaviorMap.cs`(`TrendWired` 由 `const` 改属性 + 门)
  - `unity/Assets/Gameplay.Boot/CompositionRoot.cs`(装配层按 `W` 配置门)
  - `unity/Assets/Tests/EditMode/PatientAI/behavior_map_test.cs`(哨兵改真断言 + 3 条新靶)
  - `unity/Assets/Tests/EditMode/DiseaseSimulation/w_trend_eps_vitals_end_to_end_test.cs`(4 条新靶)
  - `unity/Assets/Tests/PlayMode/audio_wiring_test.cs`(过期注释订正)

---

## 一、病因(债册原文与实测复核)

债册 `TD-audio-025` 记:9 侧 `ComputeTrend` 恒 0 ⇒ 13 的痊愈锁存整条停用。**实测逐条复核:**

| # | 债行断言 | 实测(2026-10-29) | 判 |
|---|---|---|---|
| ① | `ComputeTrend` 返回 `Fix.Zero`(tripwire 占位) | **成立** —— `ProgressionEvaluator.cs:152` `result.Trend` 即它 | ✅ 病因在 |
| ② | 13 的痊愈判据 `trend ≤ 0` 在 trend 恒 0 时**恒真** | **成立** —— `BehaviorMap.cs:158` 合取式 | ✅ 病因在 |
| ③ | 后果 = 新病人首拍锁「痊愈」⇒ 呼吸层永不启动 | **成立**(2026-10-18 实机取证 `tick=2` 起 `term=True emit=False breath=None`) | ✅ 病因在 |
| ④ | 已加 `TrendWired = false` 门(停用 ≠ 误判) | **成立** —— 当时为 `public const bool` | ✅ 已落 |
| ⑤ | 处置要求:「实现 F2 中心差分 + 死区,届时改 `true`」 | **⚠️ 本条被用户裁定改写** | 见 §二 |

**⑤ 为什么不能照债行做**:债行要求实现**中心差分** `(Progress(t+W/2) − Progress(t−W/2))/W`。
但 GDD `disease-simulation.md:923-924` 的分工是:

- **中心差分** = 8 的**展示读数**(读未来 `t + W/2`,**非因果**);
- **后向差分** `ComputeTrendBackward` = **状态转移用**(只读过去,因果)。

13 用这个量做的是**终态锁存** —— 那是一次**不可逆**的状态转移(「一旦锁存不再重新求值」)。
用「读未来的展示量」做不可逆判定,等于把非因果引进表现层。⇒ 用户裁定**甲案:改吃后向差分**。

**关键发现**:`ComputeTrendBackward` **已实现且已接线**(`DiseaseVitalsService` 的 F4 路径,2026-10-22)。
本处只是把**同一个实现**再喂给 13 —— **不新增第二套趋势算法**(禁第二真源)。

---

## 二、修复落点

| # | 落点 | 内容 |
|---|---|---|
| R-1 | `DiseaseVitalsService.cs:300-305` | `GetVitals` 的 trend 由 `result.Trend` 改为 `ProgressionEvaluator.ComputeTrendBackward(...)`,传 `_trendWindowTicks` / `_trendEpsRaw` / `_treatableBy` |
| R-2 | `BehaviorMap.cs:129` | `public const bool TrendWired = false` → `public static bool TrendWired { get; private set; } = false` |
| R-3 | `BehaviorMap.cs:136-139` | 新增 `ConfigureTrendWired(bool)` —— **只开不关** |
| R-4 | `BehaviorMap.cs:145-146` | 新增 `ResetTrendWiredForTest()`(标 `EditorBrowsable(Never)`,仅供测试) |
| R-5 | `CompositionRoot.cs:319` | `BehaviorMap.ConfigureTrendWired(trendWindowTicks > 0)` |
| R-6 | `behavior_map_test.cs` | NOT-RUN 哨兵 → 真断言(+ `ConfigureTrendWired(true)` / `try…finally` 复位) |
| R-7 | `w_trend_eps_vitals_end_to_end_test.cs` | 4 条新靶(见 §三) |

### 关键设计判断(两处,都不是照抄)

**① 门必须跟着「数据是否配置」走,不能硬编码 `true`。**

`ComputeTrendBackward` 在 `W ≤ 0` 时**短路返回 0**(`:260`)。故:

- 硬编码 `TrendWired = true` ⇒ `W` 未填时**重现同一个故障**(「刚发病」被误锁「已痊愈」);
- 硬编码 `false` ⇒ 数值填好后**仍然停用**,须人再改一次代码。

两者都不对。正解 = **`W > 0` ⇒ 趋势真的是算出来的 ⇒ 门开**。数值一填即自动生效。

**② 门「只开不关」是一个独立属性,不是顺手加的。**

`ConfigureTrendWired` 若允许反向关闭,运行期任何一处误调都能**静默停用**已生效的痊愈判据 ——
那是只在运行时才显形的行为变更。故实现为 `if (wired) TrendWired = true;`
(单向锁存),并配**独立夹具**钉住(证伪轮证明它是独立属性,见 §三 M1)。

---

## 三、证伪轮(S12-4 形态:改 → 跑 → 原子还原 → SHA-256)

### 首轮 7 变异:3 红 / **4 假存活**

| 变异 | 靶场 | 结果 | 假存活根因 |
|---|---|---|---|
| M1 门恒开(无视配置) | `BehaviorMapTest` | **假存活** | `ConfigureTrendWired(false)` **零夹具** ⇒ 「只开不关」与「入参被尊重」两个属性都无覆盖 |
| M2 门恒关 | 同上 | RED | — |
| M3 痊愈判据去掉 trend 项 | 同上 | **假存活** | 夹具只喂 `trend = −0.5`(恒满足 `trend ≤ 0`)⇒ 删掉合取项结果不变 ⇒ **该分支零输入** |
| M4 痊愈判据去掉门 | 同上 | RED | — |
| M5 Reset 不复位 | 同上 | RED | — |
| M6 装配层恒开 | 同上 | **假存活** | **靶场选错** —— 变异打在 `CompositionRoot`,而 `BehaviorMapTest` 直接调 `PatientBehavior.Step` 喂字面量,根本不经装配 |
| M7 trend 改回占位 | 同上 | **假存活** | 同上 —— 变异打在 `DiseaseVitalsService`,该 fixture 不经它 |

> **本轮抓到两种假存活形态**:
> - **「靶场里没有靶」**(M1 / M3)—— 变异打的分支没有可达输入;
> - **「靶场选错」**(M6 / M7)—— 变异体与夹具不在同一执行路径上。后者比前者更隐蔽:
>   夹具是绿的、变异也真的改了代码,**只是那次运行没跑到它**。

### 补靶(两文件 7 条)

| 靶 | 文件 | 钉住的属性 |
|---|---|---|
| `test_ac13b1_terminalLatch_lowPositionButRisingTrend_doesNotLatchRecovered` | `behavior_map_test` | 痊愈判据的 `trend ≤ 0` 合取项(**刚发病** vs 已痊愈) |
| `test_configureTrendWired_cannotCloseOnceOpen` | 同上 | 门「只开不关」 |
| `test_configureTrendWired_falseOnClosedGate_doesNotOpen` | 同上 | 门「入参被尊重」 |
| `test_vitals_trend_isComputedNotZero_whenWindowConfigured` | `w_trend_eps_…` | 9 侧 trend **真的算出来**(W>0 时必须非零) |
| `test_vitals_trend_isExactlyZero_whenWindowNotConfigured` | 同上 | 阴性对照(`W ≤ 0` 短路) |
| `test_assembly_trendGate_staysClosed_whenWindowNotConfigured` | 同上 | 装配层**看 W** |
| `test_assembly_trendGate_opens_whenWindowConfigured` | 同上 | 与上配对(缺它则「门恒关」也绿) |

⚠️ **M6 / M7 的靶必须走真装配** —— 这也是为什么它们落在 `w_trend_eps_vitals_end_to_end_test`
(它本来就建 `CompositionRoot.Assemble` → `DiseaseVitalsService` 的真链路)。

### 复跑(补靶后 8 变异,一属性一条)—— **8/8 全红**

驱动脚本 `/home/gu/.claude/jobs/181888b1/tmp/falsify025b.py`:每条变异独立 Unity 启动
(逗号分隔双 filter 实测 `total=0` ⇒ Unity 只认最后一个 filter,故**每变异只跑它自己的靶场**);
前置逐条核对盘上无 `[MUTATED]` 串;还原后 SHA-256 逐字节校验。

| 变异 | 属性 | 靶场 | 期望红 | 实测 |
|---|---|---|---|---|
| M1 | 门**只能开不能关** | `BehaviorMapTest` | `test_configureTrendWired_cannotCloseOnceOpen` | **RED 1/48** ✅ |
| M2 | 门**不无视入参** | 同上 | `test_configureTrendWired_falseOnClosedGate_doesNotOpen` | **RED 1/48** ✅ |
| M3 | 门**能打开** | 同上 | `…recoversDoNotResurrect` + `…cannotCloseOnceOpen` | **RED 2/48** ✅ |
| M4 | 痊愈判据的 **`trend ≤ 0` 合取项** | 同上 | `…lowPositionButRisingTrend_doesNotLatchRecovered` | **RED 1/48** ✅ |
| M5 | 痊愈判据**受门控** | 同上 | `…trendUnwired_doesNotLatchRecovered` | **RED 1/48** ✅ |
| M6 | `ResetTrendWiredForTest` **真的复位** | 同上 | `…trendUnwired_doesNotLatchRecovered` + `…falseOnClosedGate_doesNotOpen` | **RED 2/48** ✅ |
| M7 | 装配层**看 W** | `WTrendEpsVitalsEndToEndTest` | `test_assembly_trendGate_staysClosed_whenWindowNotConfigured` | **RED 1/8** ✅ |
| M8 | 9 侧 trend **真的算出来** | 同上 | `test_vitals_trend_isComputedNotZero_whenWindowConfigured` | **RED 1/8** ✅ |

**基线**:`BehaviorMapTest` **48/48** · `WTrendEpsVitalsEndToEndTest` **8/8**(补靶前 45 / 4)。
**还原**:三文件 SHA-256 逐字节 = 变异前(脚本内断言,轮后 `OK` ×3)。

> **一属性一条的判据(承 `feedback_falsify_one_guard_per_property`)**:M1(「只开不关」)与
> M2(「不无视入参」)是 `if (wired) TrendWired = true;` 这一行的**两个独立属性** ——
> 首轮只有一条变异(M1 形态),实测发现**两个属性各自都需要专属靶**,故补两条夹具、两条变异。
> M1 与 M2 的红点是**不同测试**,证明它们不是同一个断言的两次复述。

---

## 四、可复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# 1) 9 侧不再读占位 trend(剥注释后,期望 0)
grep -v '^\s*//' unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs | grep -c 'Fix trend = result.Trend'

# 2) 门是属性而非 const —— ⚠️ **必须剥注释**,否则会命中历史注释里的 `const TrendWired = false`
python3 -c "
import re
src=open('unity/Assets/Gameplay.Presentation/PatientAI/BehaviorMap.cs',encoding='utf-8').read()
code='\n'.join(re.sub(r'//.*\$','',l) for l in src.splitlines())
print('const 命中:', code.count('const TrendWired'))          # 期望 0
print('属性声明命中:', code.count('public static bool TrendWired'))  # 期望 1
"

# 3) 装配层按 W 配置(期望 1)
grep -c 'ConfigureTrendWired(trendWindowTicks > 0)' unity/Assets/Gameplay.Boot/CompositionRoot.cs

# 4) 真表当前 W(数值轮未裁 ⇒ 期望 0,即门仍关、行为与 10-18 一致)
grep -n 'trend_window_ticks' assets/data/disease_registry.json
```

**实跑输出(2026-10-29)**:

```text
# 1) 0
# 2) const 命中: 0 / 属性声明命中: 1 / ConfigureTrendWired 定义: 1
# 3) 1
# 4) 11:  "trend_window_ticks": 0,
```

> ⚠️ **命令 2 首版写成 `grep | grep -c 'const'` ⇒ 实得 `1`(假命中)** —— 它命中的是
> `BehaviorMap.cs` 里记录历史状态的注释 `const TrendWired = false`。这正是本轮 032 ① 踩过的
> **同族坑**(「源文本判据必须明确它扫的是哪一层」)。改为**剥注释后按代码文本计数**。

---

## 五、回归三元组(S12-0 口径)

| 套件 | 截至时刻 | 批次 | commit | 结果 |
|---|---|---|---|---|
| EditMode | 2026-10-29 CST | 全量(无 filter) | `48ba0555` + 工作树 | **`5923 / 5886 / 0 / 37`**(exit 0) |
| PlayMode | 2026-10-29 CST | 全量(无 filter) | `48ba0555` + 工作树 | **`238 / 234 / 0 / 2 / 2`**(exit 2,与基线逐位相同) |

**EditMode 等式(不是照抄旧数)**:`5915 + 8 = 5923`

- `5915` = 上一轮(032 ① 收口)全量实测 @ 2026-10-29;
- `+8` = 两个改动夹具的新增 `[Test]` 数 —— **复算**:
  ```bash
  git diff unity/Assets/Tests/EditMode/PatientAI/behavior_map_test.cs \
           unity/Assets/Tests/EditMode/DiseaseSimulation/w_trend_eps_vitals_end_to_end_test.cs \
    | grep -c '^+        \[Test\]'
  # 实测:8
  ```

**PlayMode 的 2 条 Inconclusive 是既有的具名占位**(方法名自带 `_notRun`,U1 假设 6 spike 的故意未跑标记);
exit 2 与基线同形,**不写成绿、也不写成红**。

### 中途三跑如实登记(不是「一次过」)

| 跑次 | 结果 | 根因 |
|---|---|---|
| 1 | `BehaviorMapTest 45/45` | 首次滤镜用了 `DaYiJingCheng.Tests.Unit.PatientAI` ⇒ `total=0`(FILTER-MISS);正确命名空间是 `DaYiJingCheng.Tests.PatientAI` |
| 2 | `45/45` → 补靶后 `48/48` | — |
| 3 | `WTrendEps 4/4` → 补靶后 `8/8` | — |
| 4 | 全量 `5923 / 5886 / 0` | — |

### ⚠️ 证伪轮首轮 4 假存活(本轮最大的自我纠错)

见 §三。**7 变异首轮只有 3 红** —— 若不补靶就宣布「全红」,那 4 个未覆盖的属性会以绿的形式留下
却什么都没测。这正是 S12-4 Falsifiability Gate 存在的理由。

---

## 六、本轮**不**证(边界)

| 项 | 状态 | 归属 |
|---|---|---|
| `W` / `TREND_EPS` 的**数值** | **NOT-RUN / 归数值轮** | 真表 `trend_window_ticks: 0` 未变;本轮的判据全部用**合成值**(W=100 / W=0) |
| 真表下 13 的**实际行为变化** | **NOT-RUN / 零变化** | W=0 ⇒ 门仍关 ⇒ 与 2026-10-18 修复后**逐位一致**(见 §四 命令 4) |
| 呼吸层在实机上是否真的起 | **NOT-RUN** | PlayMode 侧见 `audio_wiring_test`;本轮的 EditMode 判据只证管道与门 |
| 后向差分 vs 中心差分的**视觉差异**(8 的读数) | **不在本轮面内** | 8 的展示读数仍走中心差分(未接),归 8 |
