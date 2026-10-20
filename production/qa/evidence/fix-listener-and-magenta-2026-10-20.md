# 修复原件 · 运行时零 AudioListener + 快照静音捕获 + 占位病人品红

**日期**:2026-10-20
**触发**:用户实机报告「开了,看到品红色病人胶囊占位,**完全没声音**」
**结论**:三轮修复 —— ① 听音点缺失(已修,evidence §一);② 品红占位(已修,§二);
③ **`Master` 组被快照钉在 −74 dB**(§六,第二轮实机回报「中性灰,无声音」后定位)。
②③ 均为**静音/观感**缺陷;③ 才是「第二轮回访仍无声音」的真因。

> ⚠️ **本件的判定顺序留痕**:第一版原件把「无声」**唯一**归因为听音点缺失(§一),
> 那一半是对的(Editor.log 10 845 条可证),但**不完备** —— 修完听音点后用户回报仍无声。
> §六 记录第二根因与「为什么第一版没查到」。**不删 §一 的原文**。

---

## 一、原判定(缺陷 A:全场景静音)

### 1.1 实测证据

| 证据 | 内容 |
|---|---|
| 用户陈述 | 「完全没声音」 |
| `~/.config/unity3d/Editor.log` | `There are no audio listeners in the scene` **10 845 条**;首次出现于 `:627`,即该次 Play 的**首帧** |
| 场景资产 | `Boot.unity` **确实**含 `--- !u!81 &961739751` `AudioListener`,`m_Enabled: 1`,挂在 `m_IsActive: 1` 的 `Main Camera`(fileID 961739749)上 |

⇒ 场景文件是对的,**运行期**才是零听音点。

### 1.2 根因(代码,非资产)

`unity/Assets/Gameplay.Boot/BootRoot.cs`(修前):

```csharp
var presetCamera = GameObject.Find("Main Camera");
if (presetCamera != null)
    presetCamera.SetActive(false);          // ← 唯一持 listener 的相机被禁用

var cameraObj = new GameObject("CameraRig");
_cameraRig = cameraObj.AddComponent<CameraRig>();
cameraObj.AddComponent<UnityEngine.Camera>();   // ← 运行时相机,没有 AudioListener
```

**失效链**:预摆相机(唯一 listener)被禁用 → 运行时相机不补 listener → **active listener = 0**。

**为什么没被断言抓到**:同文件 `AssertSingleAudioListener()`(修前)用
`Resources.FindObjectsOfTypeAll<AudioListener>()` 计数,**该 API 含 inactive** ⇒ 数到 1
(那个已被禁用的)⇒ 断言通过。其注释原文:

> 「用含 active-only 的 FindObjectsOfType 得 0(预摆已禁用、CameraRig 不加)
> ⇒ 断言把**正确形态**判成错误。IncludeInactive 才是「挂载数」语义。」

**这句话把缺陷写成了规格** —— 它把「零 active」明确判为「正确形态」,于是断言永久失去
抓取能力。这是**自造断言顶替 AC 语义层**的形态之一:断言不是按 AC(单挂点 = 有且仅有 1 个
**听音**点)写的,而是按**当时实现碰巧产生的形状**写的。

### 1.3 修复

`BootRoot.cs:305-324` —— listener 随「主相机」迁到运行时相机,预摆那份**先摘除**:

```csharp
var presetCamera = GameObject.Find("Main Camera");
if (presetCamera != null)
{
    var presetListener = presetCamera.GetComponent<UnityEngine.AudioListener>();
    if (presetListener != null)
        UnityEngine.Object.DestroyImmediate(presetListener);
    presetCamera.SetActive(false);
}

var cameraObj = new GameObject("CameraRig");
_cameraRig = cameraObj.AddComponent<CameraRig>();
cameraObj.AddComponent<UnityEngine.Camera>();
cameraObj.AddComponent<UnityEngine.AudioListener>();   // 运行时主相机 = 唯一听音点
```

`BootRoot.cs:652` `AssertSingleAudioListener()` —— **双判据**(Unity 6 `FindObjectsByType`):

```csharp
var active = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
var all    = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
if (active.Length != 1 || all.Length != 1) throw new InvalidOperationException(...);
```

- `active = 1` ⇒ 抓「零听音点」(本次缺陷;修前口径**永远抓不到**)
- 总数 `= 1` ⇒ 抓「多挂点」(2026-10-10 的 31868 条洪泛,口径不变)
- 两者**必须同时成立**:任一单独存在都对应一种已实测过的失效模式

**为什么先摘除而不是留着**:留着会以 inactive 形态进入「挂载总数」计数 ⇒ 总数变 2 ⇒
断言按「多挂点」抛;即便放宽计数,Unity 的「多 listener」判定也不只看 active。摘除是唯一
同时满足两条判据的形状。

---

## 二、原判定(缺陷 B:品红占位胶囊)

### 2.1 根因

`unity/Assets/Gameplay.Boot/PatientVisualSpawner.cs:87`(修前):

```csharp
var mat = new Material(Shader.Find("Standard")) { color = new Color(0.7f, 0.7f, 0.7f) };
```

工程是 **URP**(`unity/Assets/Settings/PC_RPAsset.asset` 在盘),内置 `Standard` 着色器
不可用 ⇒ `Shader.Find` 返回 **null** ⇒ 渲染走错误着色器 = **品红**。

**副作用超出外观**:该行注释自称「中性灰 —— 零语义」,**该意图同样未兑现** —— 品红本身
就是一个强语义信号(「这里坏了」),会污染实听 / 走查时对画面的判读。

### 2.2 修复

`PatientVisualSpawner.cs` 新增 `CreatePlaceholderMaterial()`:

```csharp
var shader = Shader.Find("Universal Render Pipeline/Lit");
if (shader == null) shader = Shader.Find("Standard");
var mat = new Material(shader);
var neutralGrey = new Color(0.7f, 0.7f, 0.7f);
if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", neutralGrey);  // URP Lit
else mat.color = neutralGrey;                                               // Standard
```

按**属性存在性**写色 —— URP Lit 用 `_BaseColor`,`Standard` 用 `_Color`
(而 `Material.color` 只映射后者,直接写 `color` 在 URP 下静默无效)。

---

## 三、验证

### 3.1 基线溯源三元组(强制)

| 套件 | 三元组 | 结果 |
|---|---|---|
| EditMode 全量 | `2026-10-20 00:34 UTC` · 全量 · `66acd06`(工作树含本轮改动) | `3940 / 3904 / 0 / 36` |
| EditMode 对照基线 | 同 commit 干净树 · `impl-b-audio-tier-wiring-2026-10-20.md` §9.7 | `3940 / 3904 / 0 / 36` |
| **差值** | — | **0 / 0 / 0 / 0** |

产物:`unity/Logs/fix-listener-full-2026-10-20.xml`。

### 3.2 PlayMode 启动序(唯一能触发 `BootRoot.Start` 的自动化面)

> ⚠️ **EditMode 不执行 `BootRoot.Start`** ⇒ EditMode 全绿**不证明** listener 已修。
> 真正的自动化判据在下面四条(每条都跑真启动序,都会经过 `AssertSingleAudioListener`)。

| # | 测试 | 三元组 | 结果 | 对本次缺陷的判别面 |
|---|---|---|---|---|
| 1 | `DaYiJingCheng.Tests.PlayMode.CameraFollowWiringTest` | 2026-10-20 · `66acd06` | **1/1 Passed** | 启动序完成不抛 ⇒ 双判据通过;且断言「预摆相机被禁用」仍成立 |
| 2 | `DaYiJingCheng.Tests.PlayMode.AudioWiringTest` | 同上 | **5/5 Passed** | 真启动序 + 44 音频链(预载 / 单池 / mixer / cue 到达 / 病人 cue 驱动) |
| 3 | `DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI.ui_input_module_wiring_test` | 同上 | **3/3 Passed** | ⚠️ 该测**复用**泄漏的预摆相机 ⇒ 压测「预摆相机上 listener 被摘除后不影响启动序」 |
| 4 | `DaYiJingCheng.Tests.PlayMode.PatientVisualWiringTest` | 同上 | **1/1 Passed** | 病人视觉实体创建(缺陷 B 的类仍可正常 spawn) |

产物:`unity/Logs/fix-listener-{camera,audio,uiinput,patientvis}.xml`。

### 3.3 ⚠️ 判别力自评(反证轮未跑 —— 如实登记)

| 项 | 状态 |
|---|---|
| 双判据断言是否**真**能抓「零 active」 | **未做反证轮**。理由:要证它必须把 `AddComponent<AudioListener>()` 摘掉再跑 PlayMode ⇒ 会**真的**触发静音 + 万条日志洪泛(2026-10-10 EPIPE 死法),风险高于收益。⇒ **判为 NOT-RUN,不借绿** |
| 材质修复是否有自动化判据 | **无**。`Shader.Find` 返回 null 只在真渲染下可观测 ⇒ 归**用户实听/实看**,ADVISORY 级 |

### 3.4 待用户复核(实听面)

| 判据 | 如何验 |
|---|---|
| log 不再刷 `no audio listeners` | 重开编辑器 → Play → 查 `~/.config/unity3d/Editor.log`(或 Console)无该串 |
| 有声音 | 听:潜伏→达峰窗口内应有 `BreathLayer_Base_Calm` loop(约 2 s 窗口,见 `TD-listen-01` L-3) |
| 胶囊为中性灰,非品红 | 看病人占位实体 |

---

## 四、连带纠正的记录

`docs/tech-debt-register.md` 的 `TD-listen-01`:

1. 新增 **L-6**(运行时零 active listener),并把标题「四个阻断项」改为「五个」。
2. 原 §「与之无关、今日**确实可听**的部分」整表标 **⛔ 2026-10-20 作废**(原文保留不改写),
   并写明作废理由与元教训:**「代码里有 emit 路径」≠「耳朵能听到」**,中间隔着「有无听音点」,
   而当时的断言恰好把这一段写成了假的。

**该纠正推翻了我本人在本轮之前作出的判断** —— 我曾记录「今日确实可听:`Voice_Cough_Damp` +
`BreathLayer_Base_Calm`」,那是**只读了 emit 路径没有验证听音点**得出的,不成立。

---

## 五、未解面(不得因本件而读作已解决)

- **L-1 … L-5 全部仍 open** —— 本件只清了 L-6。三份听测协议的其余前置(无离场路径 / 档位无回写 /
  病程 1.55 s / 同屏凑不齐三人 / 被试 ≥2)一个都没动。
- **本件不改变任何 AC 的判定状态** —— 不新增 `[L]` 通过,不把任何听测项从 NOT-RUN 翻绿。
  「能听到声音」是**前置**,不是证据。
- **`.mixer` 的工作树改动**(`unity/Assets/Audio/DaYiJingCheng.mixer`)**不是本件产生的** ——
  会话开始时即存在,未由本轮触碰。

**未提交**(不经用户指令不 commit)。

---

## 六、第二根因:`Master` 组被快照钉在 −74 dB(2026-10-20 追加)

### 6.1 触发

§一 修完后用户回报:**「中性灰,无声音。」** —— 品红已消(§二 生效),但声音仍未回来。
复查 `Editor.log`:`no audio listeners` **计数 = 0**(`:627` 那批已消失)

⇒ **听音点确实修好了**,但声音仍不通 ⇒ 缺陷不在听音点,**在下游**。

### 6.2 根因

工作树里的 `unity/Assets/Audio/DaYiJingCheng.mixer` 有一处未提交改动,把 `Default` 快照写成:

```yaml
m_FloatValues:
    4b26ff2484ad0dd3dbba5a2756453a2e: -74.14725   # ← Master 组的 m_Volume
    74060fb5be3f940ec945eefb8ff36174: 5.05        # ← Master 组的 m_Pitch
```

HEAD 版本是 `...: 0`(0 dB = 不衰减)。`-74.14725 dB` 在 16-bit 输出下已低于最低有效位,
**听觉上就是静音**。

### 6.3 为什么运行期救不回来(机制)

mixer 里有两个**名字像但对象不同**的东西:

| 对象 | GUID | 谁管它 |
|---|---|---|
| `Master` **组**本身 | `4b26ff2484ad0dd3dbba5a2756453a2e` | **无人**。快照说了算 |
| 暴露参数 `bus_volume_master` | `cd069f7b2e4248a329448803d1ba3143` | `PlayerBusVolumeInitializer` 启动期 `SetFloat` |

`PlayerBusVolumeInitializer` 推的是 7 个 `bus_volume_*`(出厂默认线性 1.0 → 0 dB),
**碰不到 `Master` 组的 `m_Volume`**。于是 `bus_volume_master` = 0 dB 正常,
但它下面挂着 `Master` = −74 dB,实际输出被这一级掐死。**玩家把滑块推到 100% 也依然静音。**

### 6.4 为什么没有任何门抓到(这是本件的核心教训)

`MixerTopologyGates` **早有一道**判据覆盖「快照静音捕获」:

```csharp
public const double MuteCaptureFloorDb = -80.0;   // :954
...
foreach (double db in doc.CaptureDbValues)
    if (db <= MuteCaptureFloorDb) errors.Add("... 以拉到底实现的静音捕获 ...");
```

`test_mixerNoTierMuteFlag_assetSide_zeroErrors` 也确实对**真资产**运行。它没报,因为:

**`-74.14725 > -80`** —— 差 **5.85 dB**,擦边漏过。

**⇒ 根因不是「缺门」,是「门的判据边界比失效边界宽」。** 这道门只管「拉到底」,
而 −74 dB 已经足够静音却不在「到底」的定义里。

**为什么现有的 AC-2 门也管不着**:`ValidateSnapshotExposedDisjoint` 的判据是
「快照不得捕获**玩家 exposed 参数**」。`Master` 组不是 exposed 参数(暴露的是子 `bus_volume_master`)
⇒ 按它自己的规格**允许**。这是设计内的放行,不是 bug —— 说明必须**另开**一条判据。

### 6.5 处置

**a) 资产回退**:`git checkout -- unity/Assets/Audio/DaYiJingCheng.mixer`
(用户裁定「不是我调的」)。`Default` 快照恢复为 `Master: 0`,pitch 覆盖行消失。

**b) 新增构建期门**(`MixerTopologyGates.cs`):

| 项 | 内容 |
|---|---|
| 新常量 | `SilentCaptureFloorDb = -60.0` |
| 新判据 | `ValidateNoSilentCaptureOnPlayerAdjustedGroups(yaml)` —— 快照不得把**非 exposed 组**压到 ≤ −60 dB |
| 空转守卫 | `CountSilentCaptureScanPairs(yaml)` —— 解析到 0 个可判定配对 ⇒ NOT-RUN |
| 解析增强 | `MixerDoc.CapturePairs`(`键→值` 配对)—— 原来只有裸值 `CaptureDbValues`,**丢键** ⇒ 无法点名「哪个组」被压 |

**判据的三条边界(刻意收窄)**:

1. **只罩非 exposed 组** —— `bus_volume_*` 与 `tier_*` 由玩家滑块合法控制,压低是正当设定,
   不报(用户裁定「只罩非玩家组」)。
2. **−60 而非复用手边的 −80** —— 真资产五快照最深处 = `−6 dB`(四份 duck 快照),
   余量 54 dB;−60 既抓得到 −74,又不会误伤。
3. **不改 `MuteCaptureFloorDb`** —— 那条是 AC-44-01 ② 的原判据,改它会动既有 AC 语义面。
   两条判据并存,各管一段区间。

### 6.6 验证(含证伪)

**新增 5 条夹具**(`unity/Assets/Tests/EditMode/Audio/breath_layers_test.cs`):

| 测试 | 判据 |
|---|---|
| `test_silentCaptureOnGroup_realAsset_zeroErrors` | 真资产零错误(正向半边) |
| `test_silentCaptureOnGroup_masterAtMinus74_red` | **事故值复现** ⇒ 红,且点名 `Master` + `-74.14725` |
| `test_silentCaptureOnGroup_aboveLegacyFloor_zeroFromLegacyGate` | **老口径对照**:同一变异,旧 ≤ −80 判据**仍绿** ⇒ 证明新判据覆盖的是旧判据够不着的区间 |
| `test_silentCaptureOnGroup_notRunGuard` | 空转守卫(真资产配对数 > 0;空输入 NOT-RUN) |
| `test_silentCaptureOnGroup_exposedGroup_notFlagged` | exposed 组压到 −70 **不报**(豁免面正确) |

**证伪轮(三条,全部按「变异 → 跑 → 从好版本 cp 回 → sha256 比对」执行)**:

| # | 变异 | 结果 | 转红项 |
|---|---|---|---|
| F1 | `SilentCaptureFloorDb` −60 → −100 | **2 红 / 40 绿** | `masterAtMinus74_red` · `aboveLegacyFloor` |
| F2 | 同上(重跑确认可复现) | **2 红 / 40 绿** | 同上 |
| F3 | 停用 exposed 豁免(`if (false && ...)`) | **1 红 / 41 绿** | `exposedGroup_notFlagged` |

还原核对:`sha256sum` 与备份 `/tmp/gates_GOOD.cs` 一致(`17cbec6dbc856c2a…`),阈值回到 −60。

**全量 EditMode 回归**(三元组强制):

| 项 | 值 |
|---|---|
| 截至时刻 | **2026-10-20 00:55 UTC** |
| 批次 | 全量 · commit `3b97a85`(工作树含本轮未提交改动) |
| 本次结果 | **`3945 / 3909 / 0 / 36`** |
| 对照基线 | `3940 / 3904 / 0 / 36`(同 commit,§三 前次全量) |
| 差值 | `total +5` · `passed +5` · `failed 0` · `skipped 0` |

**等式成立**:新增 5 条夹具 = `total +5` = `passed +5`;`failed` 与 `skipped` 均未变 ⇒ **零回归**。
产物:`unity/Logs/gate-full-2026-10-20.xml`(2 951 145 B);5 条新测试名已在 XML 中逐一确认在盘。

**验证命令**(可复算,全名含 `.Unit.`):

```bash
cd unity && unity test unity --mode EditMode \
  --filter "DaYiJingCheng.Tests.Unit.Audio.BreathLayersTest" \
  --output "$PWD/Logs/gate-silent-capture.xml"
```

### 6.7 ⚠️ 本轮的一处过程失误(如实登记)

第一轮证伪脚本把 `git checkout -- <file>` 当作还原手段,而它还原的是**整个文件到 HEAD**
⇒ 把**全部新门代码**一并抹掉;备份 `cp` 排在该命令之后 ⇒ 备份到的是空壳。
**新门一度全部丢失**,靠重新写入找回(符号计数已复核:4 处 `CapturePairs` / 2 处
`ValidateNoSilentCaptureOnPlayerAdjustedGroups` / 1 处 `CountSilentCaptureScanPairs`)。

**教训**:证伪还原**不得**用 `git checkout` / `git stash`(粒度是整个文件/整个工作树),
必须「备份好版本 → `sed` 单点变异 → 跑 → `cp` 回 → `sha256` 比对」。
已写入个人记忆 `feedback_git_checkout_wipes_uncommitted.md`。

### 6.8 实听复核(✅ 已兑现 · 2026-10-20)

| 项 | 结果 |
|---|---|
| **用户实听确认有声音** | **✅ 通过** —— 用户实机陈述:**「有声音了」**(2026-10-20,`ccd06c8` 后重开编辑器 Play)。**这是本件唯一的最终判据,已兑现** |
| 品红占位 | ✅ 已消(用户同批陈述「中性灰」) |

**三层缺陷至此全部闭环**:

| # | 缺陷 | 判据 | 状态 |
|---|---|---|---|
| ① | 运行时零 active AudioListener | `no audio listeners` 由 10 845 条 → 0;PlayMode 启动序 4 件全绿 | ✅ |
| ② | `Master` 被快照钉在 −74 dB | 用户实听「有声音了」+ 构建期门 5 夹具 + 3 轮证伪 | ✅ |
| ③ | 品红占位胶囊 | 用户实看「中性灰」 | ✅ |

> ⚠️ **实听只证「有声」,不证任何 AC** —— 「能听到声音」是听测的**前置**,不是证据本身。
> `TD-listen-01` 的 L-1…L-5 **一个都没动**:无病人离场路径 · 听诊档位恒最低 ·
> 病程 1.55 s · 同屏凑不齐三人 · 被试 ≥2 人。三份听测协议**仍全部 NOT-RUN**。

### 6.9 仍未证 / 未查的一面

| 项 | 状态 |
|---|---|
| 旧口径 `MuteCaptureFloorDb` 是否也该收紧 | **未动,有意** —— 会改 AC-44-01 ② 语义面,须另裁 |
| `-74.14725` / `pitch 5.05` 的**产生原因** | **未知** —— 用户确认「不是我调的」;成因(编辑器误操作 / 视图切换写回)未查。已由构建期门兜住**后果**,但**成因未根治** |

