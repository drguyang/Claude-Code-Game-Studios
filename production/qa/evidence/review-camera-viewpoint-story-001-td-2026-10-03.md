# 评审报告: camera-viewpoint Story 001(呈现纪律与边界)· Technical Director 独立评审

> **评审对象**:
> - story 件:`production/epics/camera-viewpoint/story-001-presentation-discipline.md`(6 AC)
> - 实现:`unity/Assets/Gameplay.Presentation/Camera/`(`ICameraRig.cs` · `CameraRig.cs`)
> - 测试:`unity/Assets/Tests/EditMode/CameraViewpoint/camera_presentation_discipline_test.cs`(12 例)
> - 参考:`CameraModeMachine.cs`(story 005)· `ILBodyScanner.cs`(story 001 player 复用件)
>
> **评审日期**: 2026-10-03
> **评审方式**: 独立源码复核 —— 逐条读**当下工作树**的代码/文档,不采信 commit message 与 Completion Notes 自述
> **声明**: **本报告评的是当下代码。** 未亲自跑 Unity 测试套件(静态复核);凡未亲自验证者标 `未验证`。
> 本报告以「发现真问题」为优先,不以给出通过为目的。

---

## 1. 结论摘要表(6 AC 逐条判决)

| AC | 判决 | 一句话依据 |
|---|---|---|
| **AC-2-01** | ⚠️ **部分成立** | ① 递归只覆盖 数组/泛型实参,**未递归进自定义 struct/class 字段**(与 AC 原文「递归捕获」不符);② 判据被**就地弱化** —— 从「不同历史 ⇒ 末帧逐位相同」改成「同起点 ⇒ 同输出」(确定性 ≠ 历史无关),测试内注释自陈了这次改判 |
| **AC-2-02** | ⚠️ **部分成立** | 真扫描两处 manifest ✅(文件均存在、均无 cinemachine);但**缺「注入变红」反空转夹具** —— story 自定「必须存在,否则视同未验」,测试内**无注入路径** |
| **AC-2-04** | ⚠️ **部分成立** | ② 真断言(`Append` 是 `callvirt`,扫描有效)✅;① **自授豁免** + 未断全白名单 + 相机与 1/42/44 **共用装配** ⇒ 该 AC 在当前装配结构下**不可独立判定**,且实际引用集(`Sim` + `Unity.Addressables` + `Unity.ResourceManager`)与 AC 原文白名单**不符** |
| **AC-2-05** | 🔴 **正向空转(vacuous)** | `ILBodyScanner` **只解析 `call`/`callvirt`,不解析 `newobj`(0x73)** ⇒ `new SimEvent(...)` 永远不被捕获 ⇒ 正向断言**结构性恒绿**;反向半边 = **重复断言同一个 `hits` 变量**,非真反向 |
| **AC-2-06** | ⚠️ **部分成立** | ① 判据为 **4 键硬编码 denylist**,且**漏掉实现自己硬编码的 `"ink_edge"`**;② 自指分支(设 FirstPerson ⇒ 断言 FirstPerson 分支);③ **未做(NOT-RUN)**;④ 实际**跑通并 PASS**(非 Completion Notes 所称 NOT-RUN),但只数 YAML 字符串、**不验宿主**、且扫的是 spike 场景 |

**总判**: **不建议在现状下维持 `Complete`**。AC-2-05 为 🔴 空转,AC-2-01/04/06 各有实质判据缺口。详见 §3/§4。

---

## 2. 架构一致性专节(🔴 重点)

### 2.1 `CameraMode` 的落点 —— ✅ 与 ADR-020 一致(一处小漂移)

- ADR-020 §Key Interfaces(`adr-020:410-424`)**不指定文件**,只在「系统 2:相机」代码块内以
  `public enum CameraMode { ... }` 紧邻 `public interface ICameraRig` 给出形状。
- 实现把 `CameraMode` 放进 `ICameraRig.cs`(与接口同文件)—— **与 ADR 形状自洽,无冲突**。
  该类型是契约形状的一部分,与接口同文件是合理落点。
- **小漂移(INFO)**:ADR-020 的代码块里 `FirstPerson` 是**注释**(`/* VR: FirstPerson(P1a) */`),
  实现把它落成**活枚举成员**。此为 story 005 明文授权(「`FirstPerson` 档 P0 只落枚举」),
  但 **ADR-020 的代码块现已陈旧** —— 应在文档回写轮补注「`FirstPerson` 已落活成员(P0 枚举 · P1a 实现)」。
- ⚠️ **更大的落点问题**:ADR-020 §Key Interfaces 把 `Mode { get; }` / `SetMode(...)` / `Tick(float)` /
  `Camera Camera { get; }` 全部声明为 **`ICameraRig` 的成员**;而实现的 `ICameraRig.cs`
  **只有 `YawBasis` / `Yaw` / `Pitch` 三个成员** —— 其余四个**在接口上根本不存在**。
  `Mode`/`SetMode` 被挪到具体类 `CameraRig` 与另一个类 `CameraModeMachine` 上;
  `Camera` 属性**完全缺席**。见 §3 新发现 #1、#2。

### 2.2 `CameraRig` 新增公开面是否污染生产接口 —— ⚠️ 是,且比「命名难看」更严重

`CameraRig` 新增四个 public 成员:`Mode` / `ActivePostProcessEffectsForTest` /
`SetModeForTest` / `ApplyLook`。逐项:

| 成员 | 判定 | 说明 |
|---|---|---|
| `Mode`(get-only) | ⚠️ 重复状态 | 与 `CameraModeMachine.Mode` 是**两个独立的「当前档」状态源**(见 #1) |
| `ActivePostProcessEffectsForTest` | 🔴 语义越界 | 返回**硬编码 `"ink_edge"`** = 2 侧定义了一个效果语义键(见 #4) |
| `SetModeForTest` | 🔴 绕过意图制 | **直接写 `_mode`**,是「档位」的第二写入点(见 #1) |
| `ApplyLook` | ⚠️ 与 `ApplyOrbit` 重叠 | 纯测试缝,与 003 的 `ApplyOrbit` 功能重叠(见 #3) |

**关于 `*ForTest` 后缀住在生产程序集**:本项本身**不是**致命 —— 项目内已有测试缝先例。
但它在这里叠加了两个放大因子:① 它写的是**生产状态**(`_mode`),不是只读探针;
② 它与 story 005 的档位状态机**并存且互不知晓** ⇒ 从「命名不好看」升级为「状态双源」。
**结论**:`*ForTest` 后缀可接受,但 `SetModeForTest` 必须收敛为**不写生产状态的测试缝**
(如改为经 `CameraModeMachine` 注入,或把档位读写统一到单一持有者)。

### 2.3 `ApplyLook` 与 `ApplyOrbit` 的关系 —— ⚠️ 功能重叠,应合并

- `ApplyOrbit(lookX, lookY, dtSeconds)`(story 003 交付)与 `ApplyLook(deltaYaw, deltaPitch)`(story 001 交付)
  **方法体实质相同**:两者都调用 `UpdateYaw` / `UpdatePitch`。
- `ApplyOrbit` **甚至不使用 `dtSeconds`**(`_ = dtSeconds;` 显式丢弃)⇒ 二者差异**仅剩参数个数**。
- 唯一消费者:`ApplyLook` 只被 story 001 的测试用;`ApplyOrbit` 只被 story 003 的测试用。
- **建议**:合并为单一输入 API(保留 `ApplyOrbit` 的签名以承载将来的 `dt` 语义),
  `ApplyLook` 删除或降为测试内部 helper。**当前状态 = 生产程序集里有两个近同义 public API**。

### 2.4 `Gameplay.Camera` 程序集缺失 ⇒ AC-2-04① 判据面影响 —— 🔴 不可独立判定

- **实测**:`unity/Assets` 下**无 `Gameplay.Camera` asmdef**。相机与系统 1(`Player/`)、
  系统 44(`Audio/`)、42-UGUI 侧(`Skeuomorphic/`)全部同住 **`Gameplay.Presentation`**。
- **ADR-025 §① 表(`adr-025:114`)明确**把 `Gameplay.Presentation` 的成员列为
  「**1 · 2 · 42-UGUI 侧 · 44** …」⇒ **相机与 1 同装配是 ADR-025 的既定裁决**,不是实现偏差。
  story 001 的 Implementation Notes 自陈「以清单为准」—— **实现选对了**。
- **但这对 AC-2-04① 是结构性的坏消息**:
  1. AC 原文判据 = 「**相机程序集**的引用集白名单断言(恰 = 边界程序集 + `UnityEngine` 表现层)」。
     相机**没有自己的程序集** ⇒ 断言只能落在**共用装配**上 ⇒ **无法隔离相机的引用**。
  2. 共用装配的**实际引用集** = `Sim.Contracts` + **`Sim`** + **`Unity.Addressables`** +
     **`Unity.ResourceManager`** + `UnityEngine`。其中:
     - `Sim` 与 AC 原文「恰 = 边界程序集」**直接冲突**(已由实现**自授具名豁免**吸收);
     - `Unity.Addressables` / `Unity.ResourceManager` **完全不在 AC 原文白名单内**,且测试**不检查**它们。
  3. 测试实际只做三件事:断无 `Sim.Codec`、断无 DOTS 四件、对 `Sim` 具名豁免。
     **既不断「恰 =」,也不断非白名单项** ⇒ AC-2-04① 的强度**远低于原文**。
- **结论**:AC-2-04① 在当前装配结构下**不可独立判定**,且**实现已自授豁免**
  (round2 报告 `review-player-controller-round2-2026-10-03.md:20,107` 把「自授豁免」正是列为
  A 类反模式的红线)。**处置建议**:AC-2-04① 降级为**装配级**判据并**显式重写白名单**
  (含 Addressables/ResourceManager),或记 `NOT-RUN`(待相机独立装配)。**不得**以「具名豁免 + [x]」结案。

### 2.5 AC-2-06② 的 `FirstPerson` 夹具注入是否足够 —— ⚠️ 不足,且是自指测试

- P0 无真 VR,story 允许夹具注入。实现路径:`rig.SetModeForTest(FirstPerson)` ⇒
  断言 `rig.ActivePostProcessEffectsForTest()` 为空。
- **问题**:`ActivePostProcessEffectsForTest()` 的**唯一逻辑**就是
  `if (_mode == FirstPerson) return empty;`。测试**设了那个字段,再断言那个分支** ⇒
  **自指**(证明分支存在,不证明 VR 真的禁用了效果)。
- **story 自己的 Edge Case 明确要求**「效果经**全局静态注册表**挂载 ⇒ 夹具断言 FirstPerson 生效期
  注册表内相机侧零项」—— **该半边未实现**。
- **结论**:AC-2-06② = **部分成立**;P0 可接受为**占位**,但必须登记为 partial,
  且**不得**把「分支自指」记作「VR 全禁已验」。真验证需 URP 后处理卷/全局注册表层面的夹具(P1a)。

### 2.6 `ActivePostProcessEffectsForTest` 硬编码 `"ink_edge"` —— 🔴 是,构成 AC-2-06① 违例

- AC-2-06① 原文:「效果的**渲染实现**在 2、**语义定义**在 8(数据表**不在 2 的程序集内**)」。
- 实现把 **`"ink_edge"` 字符串字面量写死在 2 的程序集内** —— 这是一个**效果键名**,
  即「2 侧定义了一个效果语义标识」。**这构成 AC-2-06① 的违例形态**(即使只是占位)。
- **更糟**:AC-2-06① 的测试(`test_ac206a`)用**硬编码 denylist** 判定:
  `{ "diagnosis_dim", "pulse_darken", "诊脉压暗", "effect_table" }` ——
  **它漏掉了实现自己硬编码的 `"ink_edge"`**。即:**判据抓不到实现正在做的事**。
- 该 denylist 是 4 个作者自选字面量,**任意新语义键都逃逸** ⇒ AC-2-06① 判据**近乎 vacuous**。
- **处置**:① 删除 `"ink_edge"` 字面量(占位也应走 `IDataProvider`,或至少用中性常量名
  且标注「非语义键」);② AC-2-06① 判据改为**正向断言**:2 的程序集内**零** `effect_*` 键、
  或改为「2 侧的效果标识集合 ⊆ 8 数据表的键集」(可证伪)。

### 2.7 与 story 005 的接缝 —— 🔴 档位状态双源

- story 005 的 `CameraModeMachine` 是**档位状态机**且自陈 **AC-2-17: `Mode` 的唯一写入点 = `SetMode`/结算路径**。
- story 001 的 `CameraRig` 却另持 `private CameraMode _mode` + public `SetModeForTest` 直写它。
- **实测**:`CameraRig` 内对 `CameraModeMachine` 的引用数 = **0**(grep 确认)⇒ **两个类互不知晓**。
- ⇒ 系统 2 内存在**两个「当前档」状态源**:`CameraModeMachine.Mode`(005 · 意图制)与
  `CameraRig._mode`(001 · 可被 `SetModeForTest` 任意写)。**AC-2-17「写入点数 == 1」在整系统范围内被打破**
  (005 的测试只在 `CameraModeMachine` 类体内数写入点,故**各自测一半,谁都没看到第二源**)。
- **这是本报告最重要的架构发现**:story 001 与 005 的**接缝无人守**。修法二选一:
  (a) `CameraRig` 删除 `_mode`,只读 `CameraModeMachine.Mode`(注入引用);或
  (b) 明确 `_mode` 是「渲染实现侧的镜像缓存」,`SetModeForTest` 改为只经状态机。
  **无论哪条,都须补一条跨 001↔005 的对账断言**(与 story 006 的跨系统对账面同源)。

---

## 3. 新发现(含严重度)

| # | 严重度 | 发现 | 证据 |
|---|---|---|---|
| **1** | 🔴 **HIGH** | **档位状态双源**:`CameraRig._mode` 与 `CameraModeMachine.Mode` 并存,互不引用;`SetModeForTest` 是第二写入点 ⇒ 破坏 005 的 AC-2-17「写入点 == 1」的系统级语义 | `CameraRig.cs:33,68,86` · `CameraModeMachine.cs:60,78` · grep `CameraModeMachine` in `CameraRig.cs` = 0 |
| **2** | 🔴 **HIGH** | **`ICameraRig` 未兑现 ADR-020 §Key Interfaces**:接口缺 `Mode` / `SetMode` / `Tick` / `Camera` 四成员;`OQ-2-6` 要求的「`Camera` 属性 VR 双眼语义待定」**doc comment 义务未交付**(story Implementation Notes 明列为本故事交付项) | `ICameraRig.cs:46-63`(仅 3 成员)· `adr-020:413-424` |
| **3** | 🔴 **HIGH** | **AC-2-05 正向空转**:`ILBodyScanner` 只解析 `call`/`callvirt`,**不解析 `newobj`(0x73)**;构造函数名解析为 `.ctor` ⇒ `new SimEvent(...)` **永不命中** ⇒ 断言结构性恒绿 | `ILBodyScanner.cs:54,62`(仅 Call/Callvirt;无 Newobj)· 测试 `:246-251` |
| **4** | 🔴 **HIGH** | **AC-2-06① 判据 vacuous 且漏检自身**:4 键 denylist 不包含实现硬编码的 `"ink_edge"`;且 `"ink_edge"` 本身就是 2 侧的效果语义键 | 测试 `:272` · `CameraRig.cs:79` |
| **5** | 🟠 **MEDIUM** | **AC-2-01① 递归不完整**:`IsGameFactType` 只递归**数组元素**与**泛型实参**,**不递归进自定义 struct/class 的字段** ⇒ story Edge Case 明列的「外层字段合法、内层 struct 藏 `PatientId`」**不被捕获** | 测试 `:74-82` |
| **6** | 🟠 **MEDIUM** | **AC-2-01② 判据被就地弱化**:从「不同历史 ⇒ 末帧逐位相同」改为「同起点 ⇒ 同输出」。后者对确定性纯函数**恒真**,不检验「崩溃/重启不改变事实」。测试注释自陈改判,但 story 件仍以原文 `[x]` | 测试 `:119-140` vs story `:39-40` |
| **7** | 🟠 **MEDIUM** | **AC-2-06④ 状态漂移**:Completion Notes 称走 `Assert.Ignore`(NOT-RUN),但 `unity/Assets/Scenes/` **存在 6 个 `.unity`** ⇒ `scenes.Count != 0` ⇒ 测试**实际执行并 PASS**(总计数 == 1)。判据只数 YAML 字符串、**不验宿主**、且扫的是 **spike 场景**(非 ADR-023 三场景拓扑) | `CameraRig` 测试 `:314-341` · `ls unity/Assets/Scenes` = Boot + 5 Spike |
| **8** | 🟠 **MEDIUM** | **AC-2-02 缺反空转夹具**:story 明文「注入 `com.unity.cinemachine` 断言变红,否则视同未验」;测试**无注入路径**,只读文件内容 | 测试 `:154-188` · story `:163` |
| **9** | 🟡 **LOW** | **AC-2-04① 自授豁免 + 白名单不完整**:实际引用集含 `Unity.Addressables`/`Unity.ResourceManager`,**不在 AC 原文白名单且测试不检查**;`Sim` 由实现单方豁免 | `Gameplay.Presentation.asmdef` · 测试 `:201-212` |
| **10** | 🟡 **LOW** | **`ApplyLook` / `ApplyOrbit` 近同义**:`ApplyOrbit` 丢弃 `dtSeconds`,二者方法体等价 | `CameraRig.cs:97-114` |
| **11** | 🟡 **LOW** | **AC-2-05 反向半边是重复断言**:`Assert.AreEqual(0, hits, ...)` 对**同一个** `hits` 变量二次断言,不是独立反向判据 | 测试 `:258`(与 `:251` 同变量) |
| **12** | ⚪ **INFO** | 测试 `:208-209` **重复行**(`Assert.IsFalse(refs.Contains("Unity.Entities"))` 连写两次);ADR-020 代码块的 `FirstPerson` 注释已陈旧 | 测试 `:208-209` · `adr-020:411` |

---

## 4. 转 Complete 的前置(必须,BLOCKING 级优先)

1. **🔴 结清档位状态双源(#1)** —— 删除或明确 `CameraRig._mode` 的定位;补一条跨 001↔005 的对账断言
   (与 story 006 的对账面同批)。在此之前,**AC-2-17 的「写入点 == 1」在系统级为假**。
2. **🔴 修 `ILBodyScanner` 或换判据(#3)** —— 支持 `newobj`(0x73)后重跑 AC-2-05;
   在修复前,AC-2-05 的 `[x]` 应**撤下**并记 `NOT-RUN`(空转判据不得记绿)。
3. **🔴 删除 `"ink_edge"` 字面量并重写 AC-2-06① 判据(#4)** —— 改为正向/可证伪判据。
4. **🟠 补 `ICameraRig` 的 ADR-020 成员或改 ADR(#2)** —— 至少交付 `OQ-2-6` 要求的 VR 语义 doc comment;
   `Mode`/`SetMode` 的归属须与 §2.7 的修复同批决定。
5. **🟠 AC-2-01① 递归补全(#5)** + **AC-2-01② 判据回滚到 AC 原文或正式改 AC(#6)** ——
   二者择一:要么实现真递归/真差分,要么**修订 AC 文本**(不得静默弱化后仍以原文 `[x]`)。
6. **🟠 AC-2-02 补注入变红夹具(#8)** —— story 自定「否则视同未验」。
7. **🟠 修正 AC-2-06④ 的 Completion Notes 与实测不符(#7)** —— 要么记 PASS(并说明扫 spike 场景的局限),
   要么补齐 ADR-023 三场景后重跑;当前「NOT-RUN」措辞与代码行为**不一致**。
8. **🟡 AC-2-04① 重写为装配级白名单(含 Addressables)或记 NOT-RUN(#9)** ——
   撤下「自授豁免 + [x]」的形态。
9. **文档回写**:ADR-020 §Key Interfaces 补注 `FirstPerson` 已落活成员;
   `ApplyLook`/`ApplyOrbit` 合并决定(#10)。

---

## 附:评审未覆盖 / 未验证项

- **未跑 Unity 测试套件**(静态复核)⇒ 12 例的**实际通过状态**未亲自验证;上表判决基于**源码逻辑**。
- `SimEvent` 的**载荷可达类型**、`entities.yaml` 的 Kind 登记**未核**(超出本故事判据面)。
- ADR-012 CI 载体**未建** ⇒ AC-2-05 反向半边、AC-2-06③ 的构建期形态**均未验证**。
- story 002/003/004 的机器数学**未核**(本故事 Out of Scope)。
