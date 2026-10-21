# B 轮 —— 44 表现层两处接线空白(用户裁定 2026-10-20:「只接挡位 + 尾音」)

> 范围:① 诊断档位 → `tier_map` → mixer(`TierFilterDriver` 生产零构造);② 尾音改走既有 loop 增益
> (修 W-1 / W-2)。**不碰**语声交叉淡化 / `TD-audio-102`。

## §一 原状态 → 改动落点

| # | 原状态(证据) | 改动落点 |
|---|---|---|
| T-1 | `TierFilterDriver.SetTargets` / `AudioMixerParameterSink` **生产零构造** ⇒ `tier_map` 五列无运行期承载 | 新 `Gameplay.Presentation/Audio/AudioTierDriver.cs`:`IAudioCueSinkLoopGainRead` 无关;**档位=整数只读委托**,档变才 `SetTargets`,每帧 `Tick(Time.deltaTime)`;缺列/无档 ⇒ 跳过 + 单次错误,**不写 0**;mixer null ⇒ `BindMixer(false)` fail-loud 一次并禁用 |
| T-2 | 诊断 `TierIndex` ∈ {0..3},`tier_map` 仅 "0"/"1"/"2";GDD F-44.1(:285-312)/ 规则五(:544-546)**未写映射** | `AudioTierMapping.ToMixerTier = min(TierIndex, 2)` —— ✅ **2026-10-20 已裁 = 甲案「上钳」**(见 §九;GDD 补注落 `audio-system.md` F-44.1 段末) |
| T-3 | `CompositionRoot` 无档位装配 | audio 段(:385-398)接:`queryLevel`(:255 提为变量,**与 `ExamineDriver` 同一出处**)→ `DiagnosisReadFloorEvaluator.TierIndex` → 驱动;mixer 经 `PlayerBusVolumeInitializer.MixerResolver`;`CompositionRootServices.AudioTierDriver`(新属性);`BootRoot.cs:775` 帧泵每帧 `Tick(Time.deltaTime)` |
| W-1 | 尾音走 `_sink.Emit(...)` 一次性 cue;`BreathTailRamp` 生产零调用;起点写死 1.0 | `ICueRenderTarget.SetLoopGain`(44 内部缝)+ `CueRenderSink.SetLoopGain` + `CueRenderSink.Emit` 对 `looped:true` **改为循环增益读数**(按 `Source` 路由);`PatientCueDriver.TailGain` 起点 = 44 读面 `TryGetLoopGain`(`IAudioCueSinkLoopGainRead`,驱动按接口探测;替身无读面退化本拍体征),**不再写 1.0**;**`IAudioCueSink` 四方法形状不改**(ADR-018 §二) |
| W-2 | 8-bit 量化(`_tailDone: byte`;`(byte)Math.Round(gain*255)`) | 封口簿记 `_tailDone: Dictionary<int, double>` 存全精度;写入仍经 DTO `Intensity: byte`(契约面,**不改契约**),单一换算点 `GainByte` |

## §二 证伪轮(停用 X ⇒ 测试红;同命令内还原 + SHA-256)

所有变异均 `cp 备份 → sed → unity test → cp 还原 → sha256sum`;基线 SHA-256 前 12 位:
`AudioTierDriver=23c583832768` · `PatientCueDriver=688c198496d8` · `CueRenderSink=66a2ffb5079a` ·
`audio_tier_driver_test=90b143fe3b78`。

| # | 停用 / 改写的守卫 | 转红的测试 |
|---|---|---|
| M1 | `if (tier != _currentTier)` → `if (true)`(每帧设目标) | `test_tier_sameTierAcrossFrames_setTargetsCalledOnce` · `test_tier_changed_threeTimes_setTargetsCalledPerChange` |
| M2 | 缺 contact 列时以 0 补 | `test_tier_missingColumn_skipsAndLogsOnce_noZeroWrite` · `test_tier_tick_writesRowValuesInContractOrder` |
| M3 | `BindMixer` 的 null 分支改为 `Start()` | `test_tier_nullMixer_disablesOnce_noWriteNoTick` |
| M4 | `if (_failedTier != tier)` → `if (true)`(每帧刷日志) | `test_tier_missingColumn_skipsAndLogsOnce_noZeroWrite` |
| M5 | 尾音起点写死 1.0 | `test_tail_drivenByLoopGain_decaysFromSteadyGain_noOneShot`(「首拍不得高于稳态增益」) |
| M6 | `TailGain` 直接返回 floor(无衰减) | 同上(「确实衰减了」) |
| M7 | `if (cue.Looped)` → `false`(尾音回退一次性) | 同上(终点断言) |
| M8 | `_tailDone` 改回 `byte` —— ⚠️ **须三处同改**(字段 + `(byte)gain` 封口 + `PruneIfAbsent` 重载);B 轮只改字段 ⇒ **编译失败、无 XML** | `test_tail_doneBookkeeping_isNotByteQuantized`(**2026-10-20 复裁实跑转红**,见 §八) |
| M9 | `TryGetLoopGain` 恒 false(读面失效) | 同上(前置「44 有在响循环的读数」) |
| M11 | `TailGain` 去掉 `IAudioCueSinkLoopGainRead` 分支(只用本拍体征折算) | `test_tail_drivenByLoopGain_decaysFromSteadyGain_noOneShot`(**2026-10-20 复裁实跑转红**,见 §八) |
| M13 | sink 写 `Intensity/510` | 同上(逐拍「44 读数 ≠ 写入的 loop 增益」) |

## §三 假绿 / 局限(如实登记)

1. **M8**:**(B 轮原记 · 2026-10-20 复裁推翻)** ~~`doneBookkeeping_isNotByteQuantized` 的
   `Is.TypeOf<double>()` 在 `byte` 变异下**不红** …… 本条的判别力**未经独立证明**~~
   —— 复裁实测:B 轮的变异**不合法**,只改了字段声明;字段类型一变,
   `PruneIfAbsent(Dictionary<int,double>.KeyCollection)` 重载与 `_tailDone[p] = gain;`(double→byte)
   **双双失配** ⇒ 装配**编译失败、不产出 XML** ⇒ 那不是「不红」,是**根本没跑到断言**。
   补全三处后实跑:**恰该条转红**(同对象的另一条**未红**)⇒ 判别力**成立**。详见 §八。
2. **M11**:**(B 轮原记 · 2026-10-20 复裁推翻)** ~~44 读面被绕过(改用本拍体征)在**当前夹具里不可观测**
   …… **本分支无判别力**,如实登记~~
   —— 复裁实测:B 的前提「读面与体征折算同值」**不成立** —— 尾音首拍(`End` 拍)体征已跃入死亡带
   (0.90 ⇒ 230),而 44 读面仍是**上一拍稳态**的 0.45(⇒ 115):两者**必然分叉**。
   去掉读面分支后实跑**转红**(首拍 0.902 > 稳态 0.451,判据「首拍不得高于稳态增益」)⇒ 判别力**成立**。详见 §八。
3. W-2 的**量化本身**仍在 `GainByte`(DTO `Intensity: byte` 契约);本修只把封口簿记移出 8-bit。

## §四 验证命令

- 定向:`unity test unity --mode EditMode --filter "…AudioTierDriverTest|…BreathTailLoopGainTest|…AudioTierWiringTest|…RuntimeAssemblyTest"` ⇒ **47/47**
- 全量结果见同目录 `impl-b-audio-tier-wiring-2026-10-20.md` 底部「§五 全量回归」(跑后追加)。

## §五 全量回归(2026-10-20 · `unity/Logs/b-tiers-full-2026-10-20.xml`)

| 项 | 值 |
|---|---|
| 基线 `full-editmode-2026-10-20.xml` | total 3928 · passed 3892 · failed 0 · skipped 36 |
| 本轮(连跑两次,同结果) | total **3938** · passed 3901 · failed **1** · skipped 36 |
| 等式 | Δtotal = +10 = 新增夹具 10 条(集合差验过:新增 10 / 消失 0)✅ |

**唯一红**:`DaYiJingCheng.Tests.Boot.MovementFeedTest.test_move_afterFreeFall_horizontalDisplacementBoundedByConfig`
(`dx = 0`,`expected ≈ 0.00089`)。

- **baseline XML(01:04)里该条 Passed** —— 与本轮环境不同。
- **隔离**:单独跑 `MovementFeedTest` **5/5 绿 ×2**;与 Audio 四组同跑 **75/75 绿**;全量 **两次均红** ⇒ 与
  全量执行序 / 耗时相关(**非纯随机**:全量两次都红)。
- `MovementFeed.cs` / `movement_feed_test.cs` 本轮**零改动**(git 未跟踪变更)。
- **判定:与本轮改动无因果证据,但亦未能证明无关**(全量级执行序才复现)。**需另立查因**(不归 B 轮);
  ⚠️ 在该红澄清前,不得据 `passed 3901` 称「全量全绿」。

## §七 范围外改动(非本 agent 所为,如实声明)

- `production/sprint-status.yaml` 在工作区出现修改,**不是本 agent 写的**(本 agent 未编辑该文件);
  同轮有其他 agent 并行 ⇒ 勿把它记入 B 轮。
- `runtime_assembly_test.cs` 的 W-3 修(§上 diff 中 `coughCue: 0` + 平稳期前置)在 B 轮首跑前已在工作区
  (非本 agent 所写);本 agent 只在其上未改。

## §六 收紧说明(相对 §二)

- B3 门(`AssemblyBoundaryTest`)首轮全量红 ⇒ `EntryInterfaceAllowlist` 增列 `ITierTargetSink` /
  `IAudioCueSinkLoopGainRead`(同 `IMixerParameterSink` 性质,门内已加注);`IMixerTierDiagnostics` 接口**删除**,
  改 `Action<string>` 委托(`AudioSourceRenderTarget.cs:17-19` 同一理由:白名单每加一条都削弱该门)。

---

## §八 M8 / M11 判别力复裁(2026-10-20 · 用户指令「处理M8M11」)

**背景**:B 轮把 M8 记为「未验证夹具」、M11 记为「无判别力」,两条**均不计入证伪表绿项**。
本轮复裁**两条均推翻** —— 判别力成立,原登记是**推演错误 / 变异非法**所致。

### 8.1 M8 —— 原变异**编译失败**,不是「不红」

| 项 | 事实 |
|---|---|
| B 轮做法 | 只把 `_tailDone` 的**字段声明**改回 `Dictionary<int, byte>` |
| 实际后果 | ① 字段类型变 ⇒ `PruneIfAbsent(Dictionary<int,double>.KeyCollection)` **重载失配**;② `_tailDone[p] = gain;`(double→byte)**无隐式转换** ⇒ **编译失败,不产出 XML** |
| 为何像「不红」 | 编译失败时 Unity 不写结果 XML ⇒ 跑测脚本读不到断言结果,只看到同批另一条夹具的红 —— 被误读成「本夹具无判别力」 |
| 复裁做法 | **三处同改**:字段 + 封口 `(byte)gain` + 重载签名(脚本 `/tmp/fz-m8m11.py`,锚点逐字取自现行文件) |
| 复裁结果 | **恰该条转红** —— `Expected: <System.Double> / But was: <System.Byte>`,同对象另一条**未红** |

> ⚠️ **方法论**:判别力的判据是「**改坏生产码 ⇒ 断言转红**」。「改坏生产码」的前提是**变异本身可编译** ——
> 变异编译失败时,「没跑到」会被伪装成「跑了但没红」。这与 `TD-epic-02` 那批的教训同族
> (那条是「删断言 ⇒ 变绿 = trivially true」,本条是「变异不编译 ⇒ 无 XML」)。

### 8.2 M11 —— 原前提「读面与体征折算同值」**为假**

| 项 | 事实 |
|---|---|
| B 轮前提 | 「夹具体征在 5 拍后进死亡带且**此后恒定**,读面与体征折算同值」 |
| 实测反驳 | 尾音**首拍**(即 `End` 拍)体征**已跃入**死亡带(`DeathBandMin=0.90` ⇒ `Intensity` 230),而 44 读面仍是**上一拍稳态** `0.45` 写的 115 ⇒ **两者必然分叉**(230 vs 115) |
| 复裁做法 | 删去 `IAudioCueSinkLoopGainRead` 分支,`TailGain` 只用本拍体征折算 |
| 复裁结果 | **转红** —— `Expected: ≤ 0.45098 / But was: 0.90196`,判据「尾音首拍不得高于稳态增益(W-1 第二处)」 |

> ⚠️ **这条正是 W-1 第二处要守的东西**:起点若取本拍体征,停顿首拍增益会**向上跳**
> (`0.451 → 0.902`,即 +6 dB 的**向上拐点**)= 可听辨的「换了个声音」= 变相报状态(违 GDD `:571-573`)。
> 44 读面存在的**全部理由**就是挡住这个跳变 —— 夹具现在把它钉住了。

### 8.3 复裁命令与还原

- 脚本:`/tmp/fz-m8m11.py`(改 → 跑 → 还原**同一命令内**;`finally` 还原后 SHA-256 逐字节校验)
- 基线 `PatientCueDriver.cs` SHA-256:`688c198496d84db4…` —— 跑后还原 **OK**(逐字节一致)
- 日志:`unity/Logs/fz-m8m11-{base,m8,m11}.xml`

| 轮次 | total | failed | 红项 |
|---|---|---|---|
| 基线 | 2 | 0 | — |
| M8 | 2 | **1** | `test_tail_doneBookkeeping_isNotByteQuantized` |
| M11 | 2 | **1** | `test_tail_drivenByLoopGain_decaysFromSteadyGain_noOneShot` |

> ✅ **结论**:M8 / M11 两条夹具的判别力**均成立**,原「未验证 / 无判别力」登记**撤销**。
> `BreathTailLoopGainTest` 的两条夹具**各自**被独立反证钉住(不是被同对象的另一条掩盖)。
> **代码未改** —— 本轮只跑变异复裁,生产码与测试码**一字未动**(仅订正本证据原件的 §二/§三/§八)。

### 8.4 复裁轮的全量对账(新增证据)

复裁后跑一次全量 EditMode(`unity/Logs/m8m11-full-2026-10-20.xml`),与 §五 的 B 轮全量**逐位相同**:

| 轮次 | total | passed | failed | skipped | 红项 |
|---|---|---|---|---|---|
| B 轮 `b-tiers-full-2026-10-20.xml` | 3938 | 3901 | 1 | 36 | `MovementFeedTest.test_move_afterFreeFall_…` |
| 复裁 `m8m11-full-2026-10-20.xml` | 3938 | 3901 | 1 | 36 | **同一条** |

> ⚠️ **这条对账本身是 §五 那个未解问题的**新证据**,不只是「零回归」的例行确认:
> 本轮**代码零改动**(变异全部还原、`PatientCueDriver.cs` SHA-256 回到 `688c198496d84db4…`),
> 同一份代码在全量执行下**仍然红** ⇒ 进一步支持 §五 的判定「**与本轮改动无因果**,
> 与全量执行序 / 耗时相关」。**但仍不等于已证无关** —— 该红的查因**仍归另立**(不归 B 轮、不归本轮)。
> ⚠️ 因此**不得**据 `passed 3901` 称「全量全绿」。

## §九 4 → 3 档位映射结案(2026-10-20 · 用户裁定 = 甲案「上钳」)

### 9.1 缺口的两端(实测,非推测)

| 侧 | 事实 | 出处 |
|---|---|---|
| **8 诊断** | `TierIndex ∈ {0,1,2,3}` —— 粗 / 中 / 细 / 满 | `diagnosis-system.md:244-248` · `DiagnosisTuning.SlotBounds = (10,20,50)` |
| **44 听诊** | `Tier ∈ {0,1,2}` —— 低 / 中 / 高(**只有三个位置**) | `audio-system.md:295`(F-44.1 表)· `:369`(F-44.2 `0–2`)· `:546`(规则五 5 **构建期硬门**要求键集 = `{"0","1","2"}`) |
| **数据** | `assets/data/audio_events.json` 的 `tier_map` **恰 3 行**,`passband_width_hz` = 500 / 900 / 1600 | 实测 |

⇒ 缺口性质:**不是实现漏写,是 44 的规格里只有三个位置,而 8 吐出四个值**。
二选一:把第四个值压进已有位置,还是给 44 加一个位置(= 改 44 的规格)。

### 9.2 四个候选与取舍

| 案 | 映射 | 听感后果 | 代价 | 裁定 |
|---|---|---|---|---|
| **甲** | 粗→低 · 中→中 · **细→高 · 满→高** | 听诊细节在 **Lv20 饱和**;满级收益落在**判定**与脉案精度,不在耳朵 | 零代码行为变化:补 GDD 一条注 + 同步 3 处「待裁」注释 + 结案本债子项 | ✅ **采纳** |
| 乙 | 粗→0 · 中→1 · 细→2 · **满→3** | Lv50 有独立听感(第 4 条通带曲线) | 改 GDD **三处硬规格**(`:295` Tier 值域 / `:369` 的 `0–2` / 规则五 5 键集门)+ `AC-44-01`/`AC-44-02` 的「三档」措辞 + `tier_map` 第 4 行 6 列(**数值归数值轮**) | ✗ |
| 丙 | 粗→低 · **中→中 · 细→中** · 满→高 | 「全细节」只给满级 | 把 8 的「细」档(`:255` 明写「第一次成为可据以排除的证据」)与「中」合并,抹掉「你终于听清了」那一刻 —— 与 **D-A 裁定的理据**(`:148` 要避免「听觉侧技能成长永不发生」)方向相反 | ✗ **否决** |
| 丁 | 44 自设三档阈值(如 1-19 / 20-49 / 50) | 满级 = 高档 | 直接违 **F-44.1 ②**(`:281`)「**44 不拥有数值** —— 精度档曲线 / 阈值 / 通带参数全部是 8 的 F-8.x 或旋钮的映射输入」 | ✗ **否决(架构禁区)** |

**甲 vs 乙 的取舍理据(为什么不是乙)**:44 对「高档」的定义(`:300`)已经是
「宽通带 + 低噪声底 + **全细节**」,而 8 对「满」的定义(`diagnosis-system.md:248`)是
「全通道满精度」—— **两者同义**。⇒ 满落高档**不是**信息损失,是同一个东西的两个名字。
乙要付三处硬规格 + 两条 AC 措辞的代价,买到的「Lv50 独立听感」在设计上**没有对应的语义**
(8 的档位语义表只给了四层的**能读出什么**,没给「耳朵更细」).

### 9.3 落地(本次改动面 · 零逻辑变更)

| # | 落点 | 改动 |
|---|---|---|
| 1 | `design/gdd/audio-system.md` F-44.1 段末 | **新增补注块** —— 映射值、理据、设计后果、两案否决、单一落点 |
| 2 | `Gameplay.Presentation/Audio/AudioTierDriver.cs` 文件头 + `AudioTierMapping` doc | 「待裁」→「已裁 = 甲案上钳」,补理据与后果 |
| 3 | `Gameplay.Boot/CompositionRoot.cs` audio 装配段 | 行内注同步 |
| 4 | `Tests/EditMode/Audio/audio_tier_driver_test.cs` | 测试 doc + 断言消息同步 |
| 5 | `docs/tech-debt-register.md` `TD-audio-035` | 子项结案;明写 **①②③ 三项代偿常量不受影响、仍 open** |

⚠️ **`ToMixerTier` 的实现一行未改** —— `min(TierIndex, 2)` 本来就是裁定结果。
⇒ 既有断言 `test_tier_mapping_clampsFourToTopTier` 的**断言值全部不变**
(它钉的一直就是裁定值),只需同步其措辞。
⚠️ **`assets/data/audio_events.json` / `tier_map` 构建期门 / 任何生产逻辑 / 任何断言值均未动**。

### 9.4 复算命令

```bash
# 映射结论可读面(唯一落点)
grep -n "ToMixerTier" -A8 unity/Assets/Gameplay.Presentation/Audio/AudioTierDriver.cs

# 数据仍是三行(本裁定不扩表)
python3 -c "import json;d=json.load(open('assets/data/audio_events.json'));print(sorted(d['tier_map']))"   # 期望 ['0','1','2']

# 定向复跑(断言值未变 ⇒ 应仍绿;非必需,用于确认注释改动未碰逻辑)
cd unity && unity test unity --mode EditMode \
  --filter "DaYiJingCheng.Tests.Unit.Audio.AudioTierDriverTest" --output ../unity/Logs/tier-map-ruling.xml
#   ⚠️ 全名含 `.Unit.` —— 写 `DaYiJingCheng.Tests.Audio.*` 会静默产出空 XML(非「无红」)
```

### 9.5 落地核验(2026-10-20 · 实跑)

| 项 | 结果 |
|---|---|
| `git diff` 三处代码件 | **只有注释 / 断言消息** —— `ToMixerTier` 函数体 `^$` 零变动(`min(TierIndex,2)` 一字未改) |
| 定向 `AudioTierDriverTest` `unity/Logs/tier-map-ruling.xml` | **7 total / 7 passed / 0 failed / 0 skipped** |
| 其中 `test_tier_mapping_clampsFourToTopTier` | ✅ Passed —— 断言值本就是裁定值,措辞同步未动断言 |
| 全量 EditMode `unity/Logs/tier-map-ruling-full.xml` | **3940 total / 3904 passed / 0 failed / 0 skipped-inconclusive / 36 skipped** —— **零红**。见 §9.6 |

七条实测名(留档,便于比对):

`test_tier_absentKey_skips_thenRecovers` · `test_tier_changed_threeTimes_setTargetsCalledPerChange` ·
`test_tier_mapping_clampsFourToTopTier` · `test_tier_missingColumn_skipsAndLogsOnce_noZeroWrite` ·
`test_tier_nullMixer_disablesOnce_noWriteNoTick` · `test_tier_sameTierAcrossFrames_setTargetsCalledOnce` ·
`test_tier_tick_writesRowValuesInContractOrder`

> ⚠️ **本节的「已证」边界**:证明的是「**裁定值 = 代码值**,且代码仍编译可跑」。
> **不证**「甲案在听感上正确」—— 那要听测(`AC-44-02` 三档可辨),仍归你的实听协议。

### 9.6 全量对账(带基线三元组 · 禁裸数)

**基线溯源三元组**:截至时刻 `2026-10-19T23:30Z`(本机 UTC)· 批次 `TD-audio-035 甲案裁定轮 · 全量` ·
commit `b2bbc9b` **+ 本轮未提交改动**(工作区非干净树 —— 见下方 ⚠️)

| 轮次 | total | passed | failed | inconclusive | skipped | 红项 |
|---|---|---|---|---|---|---|
| `TD-player-01` 修复后基线 `td01-full-2026-10-20.xml` | 3940 | 3904 | 0 | 0 | 36 | — |
| **本轮** `tier-map-ruling-full-2026-10-20.xml` | **3940** | **3904** | **0** | **0** | **36** | — |

**等式(禁借绿口径)**:

```
Δtotal   = 3940 − 3940 = 0   ✓ 本轮零增删测试件(只改注释与断言消息)
Δpassed  = 3904 − 3904 = 0   ✓
Δfailed  = 0 − 0 = 0         ✓
Δskipped = 36 − 36 = 0       ✓
```

> ✅ **四列逐位相同是该结果的正确形态,不是巧合**:本轮**没有新增或删除任何测试**,
> 也没有改动任何断言值 ⇒ 计数面**必须**零变动。若此处出现 +N,反而说明改动越界了。

> ⚠️ **commit 字段的诚实边界**:工作区在 `b2bbc9b` 之上带着本轮六文件改动(未提交)。
> 因此本次数值**不是**「`b2bbc9b` 干净树的全量」,而是「`b2bbc9b` + 甲案注释改动」。
> 提交后若下游要引用干净树基线,须重跑或按 commit 重新标注 —— **不得把本行改写成 `b2bbc9b` 的基线**。

> ⚠️ **`§五` 那条红(§八 复裁时仍在)本轮已不在** —— 那是 `TD-player-01` **完整修复**的结果
> (见 `impl-td-player-01-2026-10-20.md`),**不是**本轮注释改动带来的。
> 本轮**不主张**对该红的任何贡献。

### 9.7 二次全量(干净树 · 用户指令「提交后重跑」)

**基线溯源三元组**:截至时刻 `2026-10-19T23:33Z`(本机 UTC · 本机 `ubuntu-gu`)·
批次 `TD-audio-035 甲案裁定轮 · 干净树全量` · commit **`222ea2f`(工作区 `git status --porcelain` 零输出)**

| # | 轮次 | XML | commit | total | passed | failed | skipped | 红项 |
|---|---|---|---|---|---|---|---|---|
| 1 | 提交流程前(工作区带改动) | `tier-map-ruling-full.xml` | `b2bbc9b` **+ 未提交** | 3940 | 3904 | 0 | 36 | — |
| 2 | **提交后(干净树)** | `tier-map-ruling-full-clean.xml` | **`222ea2f`** | **3940** | **3904** | **0** | **36** | — |
| — | 参照基线(`TD-player-01` 修复后) | `td01-full-2026-10-20.xml` | 修复轮 | 3940 | 3904 | 0 | 36 | — |

**逐用例比对(不只是计数相同)**:

```
第一次用例数 3901 · 第二次用例数 3901
集合相同: True                      ← 用例名集合完全一致
结果差异: 零差异                     ← 逐用例 同名 → 同结果,无一条翻转
```

> ✅ **为什么这一节有独立价值,不是重复行**:§9.6 那次的 commit 字段是**「`b2bbc9b` + 未提交改动」**,
> 不能当干净树基线用(§9.6 的 ⚠️ 已写明)。本次在 `222ea2f` 的**干净树**上重跑 ⇒
> **这一行才是可被下游引用的基线**。
> 且两次**逐用例**零差异,把「同一份代码 → 同一结果」从「计数碰巧相同」提升为「用例级一致」——
> 排除了「一条红一条绿相互抵消」这种计数看不见的形态。

> ⚠️ **仍不在本节主张面内的三件事**:
> ① **跨平台确定性**(ADR-012 三格矩阵) —— 本机仅 Mono,**IL2CPP 格 NOT-RUN**;<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。
> ② **甲案的听感正确性** —— 要听测(`AC-44-02` 三档可辨),归实听协议;
> ③ **`AC-1-33②` / `AC-1-34` / `LOC-config` 其余 5 字段** 等既有开放项 —— 与本次裁定无关,状态不变。
