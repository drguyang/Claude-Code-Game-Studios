# 修复原件 · 运行时零 AudioListener(全场景静音)+ 占位病人品红

**日期**:2026-10-20
**触发**:用户实机报告「开了,看到品红色病人胶囊占位,**完全没声音**」
**结论**:两处**独立**的代码缺陷,均已修;自动化面全绿,**实听面待用户复核**。

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
