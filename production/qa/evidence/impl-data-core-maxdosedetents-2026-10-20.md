# 实现轮 —— 甲档剩余项: `MAX_DOSE_DETENTS` 接线(含三方值分歧订正)

**日期**: 2026-10-20
**用户裁定**(AskUserQuestion):「**先改 JSON 为 7 重烘,再接**」
**前置**:
- `370345c`(6 份数据集的读者面 —— 甲档 2 份换源 + 乙丙 4 份实测登记)
- `001de75`(乙档第一份接线 —— `disease_action_axis` → DC-4)

---

## §0 「甲档剩下的」是哪一项 —— 逐字段普查的结果

甲档 2 份的**主读点**在 `370345c` 已接(`kind_priority` / `DOSE_BASE` / `actions[]` 行)。
本轮先做**全字段读者普查**,把「剩下的」精确定义出来 —— 不靠估计。

### 产物 1:`interaction_kinds`(7 字段 + 4 顶层)

| 字段 | 运行期读者(生产面实测) | 形态 | 本轮 |
|---|---|---|---|
| `kind_priority` | ✅ `KindPriorityTable` | 甲,已接 | — |
| `r_interact` | ⚠️ 载体 `InteractionRadius` **在**(含 4-DC-1 下界校验),但 `new InteractionSelector(` 生产面 **0 处** ⇒ 半径值在生产路径上**从未真的从产物取到** | **乙**(缺调用方) | 不接 |
| `routes_to` | ❌ `new KindRouteTable(` 生产面 **0 处** | **乙** | 不接 |
| `stable_id_source` | ❌ `new CandidateSetLoader(` 生产面 **0 处** | **乙** | 不接 |
| `suppresses_motor` | ❌ 模型/编解码器**之外**命中 **0** | **丙**(缺整条链) | 不接 |
| `duration_owner` / `_system_id` | ❌ 同上 | **丙** | 不接 |
| `intent_uplink` | ❌ 同上 | **丙** | 不接 |

### 产物 2:`prescription_actions`(3 顶层 + 行)

| 字段 | 运行期读者 | 形态 | 本轮 |
|---|---|---|---|
| `DOSE_BASE` | ✅ `PrescribePorts` | 甲,已接 | — |
| `actions[]` 行 | ✅ `PrescribeRequestDriver` | 甲,已接 | — |
| **`MAX_DOSE_DETENTS`** | ❌ **零读者** —— 驱动读常量 `9` | **甲,可接** | ✅ **本件** |
| `single_dose_max` | ❌ `ProgressionEvaluator.Evaluate` 在跑,但唯一生产调用方 `DiseaseVitalsService:260` **不传** `doseClampBound` ⇒ 恒 null | **乙**(半符合) | 不接 |

**判据(甲档 = 三条全中)**:`MAX_DOSE_DETENTS` 逐条核查 ——
① **读点在跑**:`DentchDoseSelector` 由 `PrescribeRequestDriver:128` **生产构造**;
② **消费者在跑**:`DoseDetentCount` → `CasebookVitalsSource:171` → `CasebookScreen:510`;
③ **数据来自硬编码**:喂的是 `const RegisteredMaxDoseDetents = 9`,而产物 = 7。
⇒ 三条全中,是本轮唯一可接项。

**为什么其余 6 项不接**:与 `370345c` / `001de75` 登记的判据**同一口径** ——
乙档塞 provider 而没人读 = 「已装载但无人用」(借绿家族);丙档消费链本身不存在。
**本件不改判**,只是把 4 项乙/丙细化到字段级(见上表)。

---

## §1 ⚠️ 普查中抓出一个真问题:`MAX_DOSE_DETENTS` 三方值全不相同

**下表是「改前」时点(普查那一刻)的三方读数** —— 改后三处已统一为 7(见 §2):

| 来源 | 改前值 | 出处 | 权威性 |
|---|---|---|---|
| **GDD** | **7** | `prescription-and-medication.md:794` —— 「✅ `7`(2026-10-07;原『建议 ≤ 9』的**窄化**)」 | **已裁**(数值轮定稿) |
| **产物 JSON** | 5 | `assets/data/prescription_actions.json:6` | **无出处** —— 文件 `_note` 逐字自述「**Story 001 占位种子 —— 本表数值 = 夹具值**,不是游戏平衡值(数值归用户轮)」 |
| **代码常量** | 9 | `DentchDoseSelector.RegisteredMaxDoseDetents` | **过期** —— 注释称「GDD 规则十三」,但取的是**窄化前**的旧建议 |

⇒ GDD 的 7 是**唯一有出处的**;另两个一个自称占位、一个引用了**已废弃的建议值**。

**这不是「三个都可以」,而是一条静默漂移**:三个值互不相同,且**没有任何机械判据发现它**
—— 产物的漂移门只比「盘上字节 == 现烘字节」,代码常量与产物之间**零对拍**。
后果:档数上限的**读数**与它自称的真源脱钩;当前因药档窄(`dose_range = (1,5)`)而无
可观测行为差异,属**潜伏**缺陷 —— 一旦药档放宽到 7,上限取 9 与 7 就会分叉。

**用户裁定**:先改 JSON 为 GDD 已裁的 7、重烘,再接。⇒ 三处统一为 7,**单向**:数值轮后续改值只须改 JSON。

---

## §2 改动落点

| # | 文件 | 改动 |
|---|---|---|
| D-1 | `assets/data/prescription_actions.json` | `MAX_DOSE_DETENTS`: 5 → **7**(对齐 GDD 已裁值) |
| D-2 | `unity/Assets/DataCooked/prescription_actions.cooked.bytes` | **重烘**(77 B → 77 B;`ConfigVersion 0xA5273A45 → 0x4D79D45B`) |
| A-1 | `Gameplay.Boot/PrescribeRequestDriver.cs` | 构造增可选参 `int? maxDoseDetents = null`;新增只读 `MaxDoseDetents` 属性;原常量 `MaxDoseDetents` 改名 `FallbackMaxDoseDetents` |
| A-2 | `Gameplay.Boot/CompositionRoot.cs` | 传 `maxDoseDetents: prescriptionActions?.MaxDoseDetents` |
| A-3 | `Gameplay.UI/Skeuomorphic/DentchDoseSelector.cs` | `RegisteredMaxDoseDetents`: 9 → **7** + 口径订正注释 |
| T-1 | `Tests/EditMode/DataPipeline/reader_wiring_gate_test.cs` | **+5 用例**(见 §3) |
| T-2 | `Tests/EditMode/PrescriptionMedication/prescription_tables_test.cs` | 夹具去字面量硬编码(见 §4) |
| T-3 | `Tests/PlayMode/PrescriptionMedication/dentch_input_test.cs` | 扫描上界与生产常量**解耦**(见 §4) |

### 2.1 为什么改名 `MaxDoseDetents` → `FallbackMaxDoseDetents`

原名让「常量」与「实例上实际生效的上限」同形,极易误读。改名后语义分离:

| 名 | 性质 | 语义 |
|---|---|---|
| `FallbackMaxDoseDetents`(static const) | 编译期常量 | 产物缺省时的回退值 |
| `MaxDoseDetents`(实例属性) | 运行期只读投影 | **本驱动实际生效的上限**(产物优先) |

⚠️ 改名后**代码面零引用**(实测 `grep -rn "PrescribeRequestDriver\.MaxDoseDetents"` ⇒
**1 行,且是注释里的路径说明**,见 §7.4)。

### 2.2 `MaxDoseDetents` ≠ `DoseDetentCount`(两个量,必须分开)

| 量 | 定义 | 由什么决定 |
|---|---|---|
| `MaxDoseDetents` | 档数**上限旋钮** | 产物 `MAX_DOSE_DETENTS` |
| `DoseDetentCount` | 该药**实际落点数** = `hi − lo + 1` | 药档 `dose_range` |

**当前 `dose_range = (1,5)` ⇒ 落点数恒 5**,上限 6 / 7 / 9 都不改变它。
⇒ **落点数不能当「上限已接线」的判据**(恒绿假门)。这一条由
`test_readers_maxDoseDetents_detentCountIsBlindToCeiling` 变成可红的机器。

---

## §3 门真身(`ReaderWiringGateTest` 新增 5 条,总 10 → **15**)

| 用例 | 判据 | 性质 |
|---|---|---|
| `maxDoseDetents_artifactMatchesFallbackConstant` | 真产物 `MAX_DOSE_DETENTS` ⟷ 回退常量**相等** | **对照**(钉住「两路径不许分叉」) |
| `maxDoseDetents_assemblyReadsArtifact` | 真产物经组合根 ⇒ 驱动上限 = 产物值 | 装配面 |
| **`maxDoseDetents_syntheticArtifact_drivesCeiling`** | **合成上限 6**(≠ 产物 7、≠ 回退 7)⇒ 驱动上限必为 6 | **换源(唯一承重)** |
| `maxDoseDetents_withoutArtifact_fallsBackToConstant` | 无产物 ⇒ 上限 = 回退常量 | 回退(回退路径没死) |
| `maxDoseDetents_detentCountIsBlindToCeiling` | 上限 6 vs 9 ⇒ `DoseDetentCount` **同值**且 ≠ 上限 | **判别力论证**(把「不能用落点数」变成机器) |

### 3.1 ⚠️ 判别力分布(禁借绿)

**五条里只有一条**(`syntheticArtifact_drivesCeiling`)能区分「读了产物」与「读了常量」。
其余四条是对照 / 装配面 / 回退 / 判别力论证,**单看恒绿**。
**不得**以「五条全绿」替代「换源已证」。

「换源」必须用合成夹具的**理由是可复算的**:真产物 = 回退常量 = 7 ⇒
用真产物测「源换了」,接线前后**恒绿**。

---

## §4 重烘连带订正的两处「把可变量当常量」的夹具

改 JSON 暴露了两条**夹具缺陷**(不是产品缺陷,但会让测试自报或静默缩水):

| # | 位置 | 原状 | 问题 | 订正 |
|---|---|---|---|---|
| T-2 | `prescription_tables_test.cs:257` | `Replace("\"MAX_DOSE_DETENTS\": 5", "…: 6")` | 值改成 7 后替换**落空** ⇒ 该测自报「夹具替换未生效 ⇒ 本测真空」 | 改为 `MutateMaxDoseDetents()`:从源文本抽出**现值**再 +1 |
| T-3 | `dentch_input_test.cs:199` | `for k = 1..RegisteredMaxDoseDetents` | 生产常量 9→7 后,扫描面**静默缩水 2 档**(9 仍是合法合成输入) | 加夹具自有常量 `SyntheticSweepMax = 9`,与生产取值**解耦** |

**⚠️ 这两条不是「为了变绿而改测试」**:判据本身与具体取值无关 ——
T-2 只要求「改一个数 ⇒ 哈希变」;T-3 只要求「对任意 k 确定生成」。
是夹具把**可变量**当成了**常量**。订正后两条的证明力**不降**(T-2 反而不再依赖具体值)。

---

## §5 证伪轮(改 → 跑 → 还原,同一命令内;`sha256` 逐字节校验)

脚本 `/tmp/fz-maxdetent.py`(临时件,不入库)。过滤器 = `ReaderWiringGateTest`(15 条)。

**基线 sha256**:`CompositionRoot.cs = 90dc7a9ab42c0f7f` · `PrescribeRequestDriver.cs = 28d3bf486bc1b33c`

### 5.1 实测结果(4/4 全部转红)

| # | 变异 | 转红 | 实测(`passed/total failed=N`) | 红点 |
|---|---|---|---|---|
| **F1** | 组合根不传上限(`maxDoseDetents: null`) | ✅ | `13/15 failed=2` | `syntheticArtifact_drivesCeiling` · `detentCountIsBlindToCeiling` |
| **F2** | 驱动忽略构造参(`_maxDoseDetents = FallbackMaxDoseDetents`) | ✅ | `13/15 failed=2` | 同上两条 |
| **F3** | 回退常量改 `99`(与产物分叉) | ✅ | `14/15 failed=1` | `artifactMatchesFallbackConstant` |
| **F4** | 属性改报落点数(`=> _selector.TickCount`) | ✅ | `11/15 failed=4` | `assemblyReadsArtifact` · `syntheticArtifact_drivesCeiling` · `withoutArtifact_fallsBackToConstant` · `detentCountIsBlindToCeiling` |

**还原**:F1–F4 每例 `finally` 还原后 `sha256` **逐字节一致**(脚本自校验,不符即 `exit 2`);
盘上残留检查 `grep -rn "if (false &&\|\[MUTATED\]" unity/Assets --include=*.cs` ⇒ **零残留**。

**三点须看懂(否则会误读成「红了就行」)**:

1. **F1 / F2 的红点与预期不同,且更强** —— 预期「装配面 + 换源」,实测红的是
   `syntheticArtifact_drivesCeiling` **加上** `detentCountIsBlindToCeiling`。
   后者的**前置断言**(`Assert.AreEqual(6, atSix.PrescribeDriver.MaxDoseDetents,"前置:上限 6 已生效")`)
   在接线断掉时先红 ⇒ 它不只是「论证」,还**顺带**守着接线。**这是加分,不是偏差**。
2. **F3 只红 1 条 —— 这正是「对照条」存在的意义** —— 回退常量与产物分叉时,
   只有那条专门钉「两路径不许分叉」的对照条响;其余四条**不受影响是对的**
   (它们不比常量与产物,只比驱动读数)。**若 F3 红了一片,反而说明夹具互相纠缠。**
3. **F4 红 4 条 = 判别力最大的一例** —— 把属性改成报落点数(即「拿落点数当上限」这个
   假门形态本身),装配面 / 换源 / 回退 / 判别力四条同时响。这说明
   `DoseDetentCount ≠ MaxDoseDetents` 这条区分**被四条夹具共同钉住**,不是单点脆弱。

---

## §6 完成度(双列 —— Landed / Proven)

| # | 内容 | Landed | Proven | Proven-by |
|---|---|---|---|---|
| D-1 | 源 JSON `5 → 7` | ✅ | ✅ | 字节解码(§7.1):`MAX_DOSE_DETENTS = 7` |
| D-2 | 重烘(`ConfigVersion` 变) | ✅ | ✅ | `0xA5273A45 → 0x4D79D45B`(§7.1 / §7.2) |
| A-1 | 驱动构造注入 + `MaxDoseDetents` 属性 | ✅ | ✅ | §5 F2(忽略构造参 ⇒ `syntheticArtifact_drivesCeiling` 红)· F4(报落点数 ⇒ 红 4 条) |
| A-2 | 组合根接线 | ✅ | ✅ | §5 F1(不传上限 ⇒ 红 2 条) |
| A-3 | `RegisteredMaxDoseDetents` 9 → 7 | ✅ | ⚠️ **部分** | §5 F3 只证「回退常量参与对照」;**「注释口径与 GDD 一致」本身无机械判据** ⇒ 不标已证 |
| T-1 | 5 新用例 | ✅ | ✅ | `ReaderWiringGateTest` 15/15 + §5 四条各自转红 |
| T-2 | `prescription_tables_test` 去字面量 | ✅ | ✅ | §8 全量回归(`test_singleDoseMax_configVersionTracksSourceChange` 未自报「夹具未生效」) |
| T-3 | `dentch_input_test` 扫描解耦 | ✅ | ❌ | **PlayMode NOT-RUN** —— 只落盘,未跑 ⇒ 不得标已证 |

**⚠️ 两处如实标 ✗ / 部分,不是「待补」而是「本轮不可证」**:
- **A-3** 的实体动作(9→7)有 F3 间接覆盖(回退常量改了会被对照条抓到);
  但**它的理由**(注释所称「GDD 规则十三 = 7」)是**散文**,无判据可钉 ⇒ 按
  `code-review` §Phase 8b 口径,写动作描述 = 不合格,故标**部分**。
- **T-3** 改的是 PlayMode 夹具,而 PlayMode 本轮 NOT-RUN(§9.4)⇒ 它连「能被跑」都未证。

---

## §7 可复算证据

### 7.1 产物逐字段解码(直接解字节,不经 codec)
```
$ python3 /tmp/decode-prescription-actions.py
头: fmt=1 schema=1 config=0x4D79D45B payloadLen=57 total=77
  DOSE_BASE        = 65536
  MAX_DOSE_DETENTS = 7   ← 期望 7
  single_dose_max  = 32768  (i64 raw Q16.16)
  rows             = 1
    row0: item_key='salicylic_acid' action_id=10 polarity='symptomatic'
消费 77 字节,载荷应为 77 —— ✅ 对齐
```

**脚本**(`/tmp/decode-prescription-actions.py`,临时件不入库;布局权威 = `Editor.Tools.Bake/PrescriptionActionsCookedWriter.cs` 头注):

```python
o = 20
dose_base, max_detents = struct.unpack_from("<ii", b, o); o += 8
single = struct.unpack_from("<q", b, o)[0]; o += 8          # ⚠️ i64,8 字节
print(f"  MAX_DOSE_DETENTS = {max_detents}")
rows = struct.unpack_from("<i", b, o)[0]; o += 4
# 逐行:item_key(i32len+UTF8) + action_id(i32) + polarity(i32len+UTF8)
print(f"消费 {o} 字节,载荷应为 {20+plen} —— {'✅ 对齐' if o == 20+plen else '❌ 不对齐'}")
```

**⚠️ 自查记录 —— 两次都栽在「字段宽度目测」上,两处都留档**:

| # | 错法 | 症状 | 真因 |
|---|---|---|---|
| ① (上一轮) | 把 `polarity` 当 **1 字节枚举** | 读到 63 ≠ 57 | `WriteRow` 里 `Polarity` 是**字符串**(i32 长 + UTF-8),不是枚举字节 |
| ② (**本轮重跑时抓到**) | 把 `single_dose_max` 当 **i32** | `rows = 0`,消费 36 ≠ 77 | 它是 **i64 raw Q16.16**(AC-11-09 派生量)⇒ 少读 4 字节,后续全部错位 |

**这一条为什么必须留档**:两次错法的症状**都不像崩溃** —— ① 会解出一个 ASCII 残片当字段,
② 会解出 `rows = 0`(一个**看起来完全合理**的空表)。**只有 `consumed == 20 + payloadLen`
这一条校验能把它们变成硬失败**。若只看「解出来的数像不像话」,两次都会静默通过。

⇒ **本条也为「上一轮声称已对齐」提供订正**:上一轮的解码脚本**没有**独立校验 consumed,
其「77 ✅ 对齐」是**手工核对的结论**,不是脚本判据。本轮补上了机器判据,并因此抓到 ②。

### 7.2 重烘日志

```
[大医精诚] 烘焙完成:prescription_actions.cooked.bytes = 77 B, 1 行行集,ConfigVersion = 0x4D79D45B
```

**重烘命令**(⚠️ `--execute-method` 必须写**全命名空间**):
```
unity build "$PWD/unity" --target StandaloneLinux64 \
  --execute-method DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BakePrescriptionActions
```

### 7.3 甲档逐字段读者的 grep 原文

**逐字输出**(排除 `/Tests/`;`⇒` 后为我加的判读,非命令输出):

```
$ grep -rn "new KindRouteTable(" unity/Assets --include=*.cs | grep -v "/Tests/"
  ⇒ 0 行(生产面零构造 ⇒ routes_to 是乙档)

$ grep -rn "new InteractionSelector(" unity/Assets --include=*.cs | grep -v "/Tests/"
  ⇒ 0 行(生产面零构造 ⇒ r_interact 是乙档)

$ grep -rn "new CandidateSetLoader(" unity/Assets --include=*.cs | grep -v "/Tests/"
  ⇒ 0 行(生产面零构造 ⇒ stable_id_source 是乙档)

$ grep -rn "SuppressesMotor\|DurationOwner\|IntentUplink" unity/Assets --include=*.cs \
    | grep -v "/Tests/" | grep -v "Editor.Tools.Bake" \
    | grep -v "InteractionKindCookedCodec.cs" | grep -v "InteractionKindTable.cs"
  ⇒ 0 行(丙档:模型与编解码器**之外**零消费点)
```

**⚠️ 丙档那行的排除项必须写全** —— 不排除时该 grep **命中 24 行**,
全部落在 `InteractionKindTable.cs`(类型/行模型自身)与 `InteractionKindCookedCodec.cs`(codec)。
这两处是**数据的定义与承载**,不是**消费** —— 「字段有定义、有承载、无消费」正是丙档的定义。
**若照抄一条不加排除的 grep 并写「0 行」就是伪造输出**;此处如实贴出两段。

### 7.4 改名后旧名残留检查

```
$ grep -rn "PrescribeRequestDriver\.MaxDoseDetents" unity/Assets --include=*.cs
unity/Assets/Tests/EditMode/DataPipeline/reader_wiring_gate_test.cs:302:
  // C. prescription_actions → PrescribeRequestDriver.MaxDoseDetents(甲档剩余项)
  ⇒ 1 行,**且是注释里的路径说明**(非代码引用)。代码面零引用 ⇒ 改名不漏改。
```

**新名使用点**(代码面,已核对):

```
unity/Assets/Gameplay.Boot/CompositionRoot.cs:342:   maxDoseDetents: prescriptionActions?.MaxDoseDetents);
unity/Assets/Gameplay.Boot/PrescribeRequestDriver.cs:108: public const int FallbackMaxDoseDetents = …;
unity/Assets/Gameplay.Boot/PrescribeRequestDriver.cs:158: _maxDoseDetents = maxDoseDetents ?? FallbackMaxDoseDetents;
unity/Assets/Gameplay.Boot/PrescribeRequestDriver.cs:123: public int MaxDoseDetents => _maxDoseDetents;
```

---

## §8 回归

**基线溯源三元组(强制)**:截至时刻 `2026-10-20 13:25 CST(05:25Z)` ·
批次 `sprint-15 数据管线甲档剩余项接线轮(全量 EditMode,单批)` ·
commit `001de75`(工作树 8 个未提交改动 + 本证据件)

**命令**:
```
unity test "$PWD/unity" --mode EditMode --output /tmp/s15-maxdetent-full.xml
```
XML `start-time=2026-10-20 05:23:43Z` · `end-time=05:25:05Z` · `duration=82.1 s`。

| 量 | 前次(`370345c` · 更新⑤) | 本次 | Δ | 预期 Δ | 判 |
|---|---|---|---|---|---|
| total | 4007 | **4012** | **+5** | +5(§3 的 C 段五用例) | ✅ |
| passed | 3971 | **3976** | **+5** | +5 | ✅ |
| failed | 0 | **0** | 0 | 0 | ✅ **零回归** |
| inconclusive | 0 | **0** | 0 | 0 | ✅ |
| skipped | 36 | **36** | 0 | 0 | ✅ |

**判据是等式 `total − 前次 = 新增数`,不是「全绿」** —— ✅ 精确成立。

**红点逐条扫描**:`failed=0`,且遍历 XML **无任何非 `Passed`/`Skipped` 的 `test-case`** ⇒ 零红点。
(36 条 `Skipped` 为**既有** Ignored 用例,前次同为 36,非本轮引入。)

**⚠️ 两处如实登记**:

1. **退出码未采集** —— 本轮以 `nohup … &` 起跑,stdout/stderr 落 `/tmp/s15-maxdetent-full.log`
   实测 **0 字节**(Unity 未向该管道写)。⇒ 判读**只依据 XML**(`failed=0` + 无红点),
   **不主张** `exit 0`。前次更新⑤记录的 `exit 0` 是那轮真采集到的,两者不可互相替代。
2. **XML 根节点 `result="Skipped:Ignored"`** —— 这是 NUnit 在**存在 Ignored 用例**时的
   根结果串,**不是失败信号**(`failed=0` 是权威量)。此处点明,防下游把该串误读成「整轮被跳过」。

**PlayMode**:NOT-RUN(§9.4)。
**IL2CPP 三格**:NOT-RUN(§9.5)。

---

## §9 未结事项(如实登记)

1. **`single_dose_max` 仍未接** —— `ProgressionEvaluator.Evaluate` 的 `doseClampBound` 唯一生产
   调用方 `DiseaseVitalsService:260` **不传**该参 ⇒ 恒 null(clamp 关闭)。属**乙档**(缺调用方),
   且 `MAX_ACTIVE_DOSE` 亦未定值(`ProgressionEvaluator:121-123` 自陈「两旋钮取值均待定」)
   ⇒ 须数值轮与 9 的装载面同批。
2. **`interaction_kinds` 的 6 个字段未接**(`r_interact` / `routes_to` / `stable_id_source` = 乙;
   `suppresses_motor` / `duration_owner` / `intent_uplink` = 丙)—— 见 §0 表,理由逐条可复算。
3. **`RegisteredMaxDoseDetents` 已对齐 GDD,但 GDD 侧的 `dose_range.hi ≤ 7` 约束未接线** ——
   binder 只在药自带非空 `dose_range` 时校验 `DOSE_BASE ≤ 65536 × hi`,**不校验 `hi ≤ MAX_DOSE_DETENTS`**;
   运行期由 `DentchDoseSelector` 的「档数 > 上限 ⇒ 硬失败」兜住。启动期无门。
4. **PlayMode NOT-RUN** —— 本轮改了 `dentch_input_test.cs`(PlayMode)但未跑。
5. **IL2CPP 三格 NOT-RUN** —— 本机仅 Mono。<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。

---

## §10 落账

| 件 | 状态 |
|---|---|
| 本证据原件 | ✅ |
| `tests/integration/item_database/README.md`(账本) | ✅ §2026-10-20 更新⑥ |
| `production/session-state/active.md`(Session Extract) | ✅ 第五段 |
| commit | **未做**(不经用户指令不 commit) |
