# 评审报告:camera-viewpoint Story 001(呈现纪律与边界)

> **评审对象**: `production/epics/camera-viewpoint/story-001-presentation-discipline.md`(6 AC)
> + 实现 `unity/Assets/Gameplay.Presentation/Camera/`(`ICameraRig.cs` / `CameraRig.cs`)
> + 测试 `unity/Assets/Tests/EditMode/CameraViewpoint/camera_presentation_discipline_test.cs`(12 例)
> **评审日期**: 2026-10-03
> **评审方式**: 独立静态评审(逐条复核判据机制,不采信 story 自述 / commit message)
> **声明**: **本报告评的是当下工作树的代码,不追认原判定。** 未本地运行 Unity UTF(见「未验证项」)。
> **权威依据**: ADR-020 §二/§五/§六/§七 · ADR-013 §9 C3 · ADR-018 §六 · ADR-023 ①

---

## 结论摘要表

### 6 条 AC 逐条判决

| AC | 判决 | 依据 |
|---|---|---|
| AC-2-01① 字段类型闭包 | **部分成立** | 递归只到「容器元素」,不进嵌套 struct 成员;实为 5 元**黑名单**而非 story 自述的「类型白名单」;`PayloadRef`/`EventOrderKey`/`ItemInstanceId` 等事件流类型全数漏网 |
| AC-2-01② 差分重算 | **🔴 未成立(判据恒真)** | 测试用**相同起点 + 相同序列**两实例比对 ⇒ 对任何确定性实例状态实现(含带隐藏静态态者)必然绿;AC 原文要求「**不同历史**的后半段收敛」,实现偷换为「同起点同输入」——**这正是本仓头号失效模式的第 3/5 型** |
| AC-2-02 零第三方 | **部分成立** | 双 manifest 真扫到(`found≥2` 守卫有效);但 story 自订的「**注入变红**反空转夹具」**缺失**,且 story 自己写明「夹具不存在 ⇒ 视同未验」 |
| AC-2-04① 引用集白名单 | **部分成立(降级)** | 相机无独立 asmdef,共用 `Gameplay.Presentation`;该 asmdef 实引 `Sim`+`Unity.Addressables`+`Unity.ResourceManager` ⇒ AC 的「**恰 = 边界程序集 + UnityEngine**」**不成立**,靠具名豁免变绿;无传递闭包;非白名单,是 2 名黑名单 + 4 名显式否 |
| AC-2-05 零 SimEvent | **🔴 未成立(正反两半皆空转)** | 正向:ILBodyScanner **不识别 `newobj`**,且匹配 `resolvedMethod.Name=="SimEvent"`(struct 构造名恒为 `.ctor`)、`get_Kind`(Kind 是**字段**无 getter)⇒ 三条谓词**永不命中**;反向:`Assert.AreEqual(0, hits)` **重复断言同一个 `hits`** ⇒ 无因果、无独立扫描面 |
| AC-2-06 效果归属/VR禁/不得报状态/AudioListener | **部分成立** | ① 名字黑名单 grep 且只扫 Camera 子目录(AC 说的是整个程序集);② 自指桩(方法自己 `if(_mode==FirstPerson) return Empty`);③ **无任何测试**(BLOCKED-BY 属实);④ 判据**实际在跑并绿**(见下),但**不查宿主**,且 story 自述「走 Assert.Ignore / NOT-RUN」**与事实相反** |

> **「6/6 AC 落地」不成立**:AC-2-05 两半皆空转、AC-2-06③ 无实现,至多 **4/6 部分交付**。

### 12 例判据有效性

| # | 测试 | 有效性 | 说明 |
|---|---|---|---|
| 1 | `test_ac201a_cameraFields_noGameFactTypes` | ⚠️ 部分 | 扫描面非空(守卫好);但黑名单 5 元、递归不到嵌套成员 |
| 2 | `test_ac201a_negativeFixture_reportsGameFactField` | ✅ 有效 | 对谓词本身可证伪(`SimEvent`/`List<SimEvent>`/`Fix[]` 命中) |
| 3 | `test_ac201a_noHiddenStaticState` | ⚠️ 部分 | 只守相机类型自身的可变静态字段;跨类型/跨程序集静态缓存不受守 |
| 4 | `test_ac201b_differentialRecompute_lastFrameBitIdentical` | 🔴 **恒真** | 同起点同输入;且 `Pitch`、`Fwd.y` 两断言比较的是**常量**(永真) |
| 5 | `test_ac202_noCinemachine_inBothManifests` | ⚠️ 部分 | 双文件扫描真实;缺「注入变红」夹具 |
| 6 | `test_ac204a_referenceWhitelist_noSimImplementation` | ⚠️ 部分 | 非白名单;`unexpected` 仅能捕获 `Sim.Codec`;无闭包;208-209 行重复 |
| 7 | `test_ac204b_zeroAppendCallSites` | ⚠️ 部分 | 名匹配 `Append`;依赖的 ILBodyScanner 有对齐/`newobj` 盲区(见专节) |
| 8 | `test_ac205_zeroSimEventConstruction` | 🔴 **两半皆空转** | 正向谓词永不命中;反向是同变量重断言 |
| 9 | `test_ac206a_effectSemanticsNotInCameraAssembly` | ⚠️ 部分 | 名字黑名单,只扫 Camera 子目录 |
| 10 | `test_ac206b_firstPersonMode_disablesPostProcessing` | ⚠️ 部分 | 自指桩;与 #11 成对照(至少排除「永远空清单」) |
| 11 | `test_ac206c_exploreMode_allowsEffects` | ✅ 对照有效 | 排除空实现,作用真实 |
| 12 | `test_ac206d_audioListenerExactlyOne` | ⚠️ 部分 | **实际执行并绿**(count=1);不查宿主;story 自述 NOT-RUN 失实 |

---

## 🔴 判据有效性专节(重点)

### A. AC-2-05:两半皆空转 —— **头号发现(S2/BLOCKING)**

**正向**(`test_ac205_zeroSimEventConstruction` 第一段):
```csharp
ILBodyScanner.ContainsMethodCall(m, "SimEvent")   // 永不命中
ILBodyScanner.ContainsMethodCall(m, "get_Kind")   // 永不命中
```
三条独立理由使该谓词**在数学上不可能命中**:
1. `ILBodyScanner` 只匹配 `OpCodes.Call`(0x28)/`OpCodes.Callvirt`(0x6F),**不识别 `newobj`(0x73)**
   —— 而 `new SimEvent(...)` 编译为 `newobj`。
2. 即便识别,`ResolveMethodToken` 取的是 `resolvedMethod.Name`,而 struct 构造函数名恒为 **`.ctor`**,
   不等于 `"SimEvent"`。
3. `SimEvent.Kind` 是 **`public readonly EventKind Kind;` 字段**(见 `unity/Assets/Sim.Contracts/SimEvent.cs:39`),
   非属性 ⇒ 无 `get_Kind` 方法。

⇒ 谓词与目标之间**无因果**,`Assert.AreEqual(0, hits)` 恒绿。

**反向**(第二段):
```csharp
Assert.AreEqual(0, hits, "正向已断言零构造点 ⇒ 反向前提成立(不依赖「不引 Sim」)");
```
这是对**同一个 `hits` 变量**的第二次断言,无任何新扫描。story 自己在 Completion Notes 承认
「反向记『已定义』不借绿」,但测试代码把它写成了**一条会通过的断言**——比 NOT-RUN 更危险:
它给报告一个「反向也绿」的假象。

**可复现验证**:
```bash
grep -n "OpCodes\.\(Call\|Callvirt\|Newobj\)" \
  unity/Assets/Tests/EditMode/PlayerController/ILBodyScanner.cs
# 输出只有 Call / Callvirt 两处;Newobj 零命中
sed -n '83,96p' unity/Assets/Tests/EditMode/PlayerController/ILBodyScanner.cs
# ResolveMethodToken 返回 resolvedMethod?.Name ⇒ ctor 名为 .ctor
grep -n "readonly EventKind Kind" unity/Assets/Sim.Contracts/SimEvent.cs
# 第 39 行:字段,非属性 ⇒ 无 get_Kind
```
**突变测试判决**:在 `CameraRig.cs` 里加 `private SimEvent _leak;`(或 `new SimEvent(...)`)⇒ 本测**仍然全绿**。

### B. AC-2-01②:恒真 —— 同起点同输入,无法证明「无隐藏状态」(S2/BLOCKING)

测试第 126-146 行:两个 `CameraRig` 都从**默认初值**出发,喂**同一序列**,断言末帧相同。
- 这对**任何**「实例内确定」的实现必然成立——**包括带隐藏静态态的实现**。
  隐藏静态态被 A、B 两实例**同步演化**,末帧照样逐位相同 ⇒ 该判据**不可证伪**。
- AC 原文(2026-09-16 订正版)明写「构造两条**不同历史**的实例(不同 yaw/档/中途重启),喂**后半段**相同输入」。
  实现把「不同历史」换成「相同起点」,把 AC 的**收敛性**要求偷换成一条**重言式**。
  开发者注释(第 119-123 行)自陈这是「2026-10-03 修正」——但该修正**改了 AC 的语义**而非实现,属越权。
- story 的 QA Test Case 里的 negative fixture(`v_anchor` 经静态缓存 ⇒ 两条末帧不同 ⇒ 红)**未实现**,
  且以本测的形态**不可能**触发(两实例同时起步,静态缓存演化一致)。

**次要空转**:`Assert.AreEqual(rigA.Pitch, rigB.Pitch)` —— 因 `ApplyLook(x, 0f)` 的 `deltaPitch` 恒 0,
`_pitch` 恒为默认 30f ⇒ 断言 `30==30`;**`basisA.Fwd.y` / `basisB.Fwd.y` 恒为 `0f`**(构造式 `new Vector3(sinYaw, 0f, cosYaw)`)
⇒ 断言 `0==0`。三处断言中的两处比较的是**常量**(本仓失效模式第 4 型)。

**可复现验证**:
```bash
sed -n '124,150p' unity/Assets/Tests/EditMode/CameraViewpoint/camera_presentation_discipline_test.cs
grep -n "new Vector3(sinYaw, 0f, cosYaw)" unity/Assets/Gameplay.Presentation/Camera/CameraRig.cs
```

### C. AC-2-06④:`Assert.Ignore` 分支是**死代码**,story 自述与事实相反(S3)

story 的 Completion Notes 写:「AC-2-06④ 走 `Assert.Ignore` —— 场景资产未建(ADR-023 三场景制未落地)⇒ NOT-RUN」。
**事实**:`unity/Assets/Scenes/` 下已有 6 个 `.unity`(`Boot.unity` 自 2026-09-22 起存在)⇒
`scenes.Count == 6` ≠ 0 ⇒ **不走 Ignore**,测试**实际执行**并对 `listeners == 1` 判绿。

即:该 AC **已经验过并通过**,却被自述成「未跑、不借绿」。方向上偏保守,但:
- 掩盖了判据**缺宿主校验**(AC 要求「其宿主 ∈ {主相机, 头显锚点}」—— 实现只数「`AudioListener`」子串出现次数,不查宿主);
- 「场景资产未建」的注释已**陈旧失实**。

**可复现验证**:
```bash
for f in unity/Assets/Scenes/*.unity; do echo "$(grep -c AudioListener "$f") $f"; done
# Boot.unity=1,其余 5 个=0 ⇒ 合计 1 ⇒ Assert.AreEqual(1, listeners) 通过,非 Ignore
ls unity/Assets/Scenes/*.unity | wc -l   # = 6,非 0
```

### D. AC-2-01①:递归深度不足 + 黑名单非白名单(S2)

- `IsGameFactType` 只递归 `IsArray` 与 `IsGenericType` 的元素,**不进自定义 struct/class 的成员**。
  ⇒ `struct Slot { PatientId Id; }` 作字段类型时**逃逸**(story QA Test Case 明写此 Edge case 须「递归捕获」)。
- `ForbiddenStateTypes` 是 **5 元黑名单**(`SimEvent`/`PatientId`/`Fix`/`StreamId`/`EventKind`),
  而 story 的 Implementation Notes 自述为「**白名单按类型** …… 不在白名单即红」。
  ⇒ `PayloadRef` / `EventOrderKey` / `ItemInstanceId` / `VitalsDto` / `WorldPos` 等**事件流/契约类型全数漏网**。
- 「能否被改名绕过」:按**类型**判,改名无效——这一点是设计对的;但**换类型**即可绕过。

**可复现验证**:
```bash
sed -n '35,82p' unity/Assets/Tests/EditMode/CameraViewpoint/camera_presentation_discipline_test.cs
```
**突变测试判决**:`CameraRig` 加 `private PayloadRef _lastRef;` ⇒ 本测**仍绿**。

### E. AC-2-04①:降级为「具名豁免」,非 AC 所写的白名单(S2)

```csharp
var waiver = new[] { "Sim" };
var forbidden = refs.Where(r => r == "Sim" || r == "Sim.Codec").ToList();
var unexpected = forbidden.Where(f => !waiver.Contains(f)).ToList();
```
- 实际引用集(读 `Gameplay.Presentation.asmdef`):`Sim.Contracts, Sim, Unity.Addressables, Unity.ResourceManager, UnityEngine`
  ⇒ AC 的「**恰 = 边界程序集 + UnityEngine 表现层**」**不成立**。
- `unexpected` 只能捕获 `Sim.Codec` 一个名字;**任何新命名的 sim 实现程序集**(如 `Sim.Internal`)不受守。
- **无传递闭包**(story QA Test Case 明确要求「沿引用图传递闭包」)。
- 208-209 行 `Assert.IsFalse(refs.Contains("Unity.Entities"), ...)` **重复两遍**(无害,但显示未审校)。

**「具名豁免只放行 `Sim`」的定性**:这是**已登记的偏差**而非 AC 的判据。
若按 AC 字面,**部分成立**;story 的 Completion Notes 已如实登记豁免,这点诚实,
但 `[x]` 勾选「AC-2-04 落地」与 AC 判据面不符。

### F. AC-2-06②:自指桩(S3)

```csharp
public IReadOnlyList<string> ActivePostProcessEffectsForTest()
{
    if (_mode == CameraMode.FirstPerson) return System.Array.Empty<string>();
    return new[] { "ink_edge" };
}
```
测试验的是**这个方法自己的 if 分支**,不是任何真实后处理配置/注册表。与 #11 的对照只排除了
「永远空清单」这一种空实现。QA Test Case 要求的「全局静态注册表挂载 ⇒ FirstPerson 生效期相机侧零项」
与 negative fixture(注入「急救时屏幕压暗」并保持)均**未实现**。
`SetModeForTest` 是 test-only 缝 ⇒ 「VR 禁效果」**未对任何真 VR 路径验证**(P0 无真 VR,可接受,但判据等级应记 ADVISORY 而非 BLOCKING)。

### G. ILBodyScanner 的共享盲区(影响 #7)

`ContainsMethodCall` 逐字节扫描、仅在 `call/callvirt` 后跳 4 字节操作数,**未按指令长度对齐**,
其他多字节指令的操作数中出现 `0x28`/`0x6F` 字节时可能错位;异常时 `catch { return false; }`(保守)。
对「**零命中**」型断言,这两种情形都产生**假阴性**(漏报 ⇒ 记绿)。此为 AC-1 侧既有债,本故事继承。

---

## 新发现(含严重度)

| # | 严重度 | 发现 | 落点 |
|---|---|---|---|
| N1 | **S2 / BLOCKING** | AC-2-05 正向谓词(`"SimEvent"` / `"get_Kind"`)在 IL 层**永不命中**(`newobj` 未识别 + ctor 名为 `.ctor` + `Kind` 是字段) | 测试 #8;ILBodyScanner |
| N2 | **S2 / BLOCKING** | AC-2-05 反向 = 对同一 `hits` 的**重复断言**,无独立扫描面 | 测试 #8 第 258 行 |
| N3 | **S2 / BLOCKING** | AC-2-01② 判据**恒真**:同起点同输入比对无法证明无隐藏状态;且偷换了 AC 的「不同历史」语义 | 测试 #4 |
| N4 | **S2** | AC-2-01① 递归不进嵌套成员 + 5 元黑名单(非白名单)⇒ `PayloadRef` 等可逃逸 | 测试 #1 |
| N5 | **S2** | AC-2-04① 非白名单、无闭包、仅 2 名黑名单;AC「恰 =」不成立 | 测试 #6;asmdef |
| N6 | **S3** | AC-2-02 缺 story 自订的「注入变红」反空转夹具 ⇒ 按 story 自己的口径「视同未验」 | 测试 #5 |
| N7 | **S3** | AC-2-06④ 判据**实际在跑并绿**,story 自述「走 Assert.Ignore / NOT-RUN」**失实**;且不查宿主 | 测试 #12;Completion Notes |
| N8 | **S3** | AC-2-06② 自指桩,验的是测试缝方法自身的分支 | 测试 #10;CameraRig:74-80 |
| N9 | **S4** | AC-2-01② 中 `Pitch` 与 `Fwd.y` 断言比较常量(空转) | 测试 #4 |
| N10 | **S4** | 测试 #6 第 208-209 行重复断言 | 测试 #6 |

**⚠️ 与既有失效模式对照(六型命中情况)**:第 1 型(只查字符串存在)⇒ N6/N8 变体;
第 2 型(白名单遍历致排他永假)⇒ N5;第 3 型(无因果)⇒ N2;第 4 型(余数/常量同值)⇒ N9;
第 5 型(把语义假设当事实)⇒ N3。**六型中命中五型。**

---

## 未验证项(本报告不覆盖)

- **未本地运行 Unity UTF** —— 无 `dotnet`/`mono`,且 Unity batch 需独占工程。
  以上「测试会绿/会跑」的判断均为**静态推演**(基于代码语义 + 磁盘资产实测),非运行时实测。
  建议以 `/smoke-check` 或 `unity build ... --executeMethod` batch 复核 12 例的实跑结果。
- AC-2-06③ 的载体(8/44 白名单数据表)未建 —— 无实现,属实。

---

## 转 Complete 的前置(硬门)

1. **N1+N2(AC-2-05)**:重写判据面 —— 正向须以 `newobj` 目标类型 = `SimEvent` 为判据(扩展 `ILBodyScanner` 识别 `newobj`,
   或改按**类型引用闭包**判),并以 negative fixture(相机内加一处 `new SimEvent(...)` 的突变)证明**会变红**;
   反向须是**独立**扫描(全仓构造点调用者闭包),不得复用同一 `hits`。
2. **N3(AC-2-01②)**:回到 AC 原文语义(不同历史的后半段收敛),或由 game-designer / technical-director
   **正式改 AC** 并说明「增量 API 下原 AC 不可满足」——不得由测试单方偷换;并加 negative fixture
   (`v_anchor` 静态缓存)证明会红。
3. **N4(AC-2-01①)**:递归进嵌套成员(或改「允许类型白名单」),补 `PayloadRef`/`EventOrderKey`/`ItemInstanceId` 等;
   negative fixture 覆盖 `struct Slot { PatientId Id; }`。
4. **N5(AC-2-04①)**:改为真白名单 + 传递闭包;或正式把 AC 降级为「黑名单 + 具名豁免」并同步 story 勾选口径。
5. **N6(AC-2-02)**:补 `com.unity.cinemachine` 注入夹具,断言测试变红。
6. **N7(AC-2-06④)**:补宿主校验;修正 Completion Notes 的失实自述。
7. **story 元数据**:`Status: Complete ✅ (6/6 AC)` 应改为**部分交付**;Completion Notes 中三处 `_待填_` 补全。

---

## 附:关键文件绝对路径

- story: `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/production/epics/camera-viewpoint/story-001-presentation-discipline.md`
- 测试: `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/CameraViewpoint/camera_presentation_discipline_test.cs`
- IL 扫描器: `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/PlayerController/ILBodyScanner.cs`
- 实现: `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Presentation/Camera/CameraRig.cs`
- 接口: `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Presentation/Camera/ICameraRig.cs`
- asmdef: `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Presentation/Gameplay.Presentation.asmdef`
- SimEvent: `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Sim.Contracts/SimEvent.cs`
- manifest(根): `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/Packages/manifest.json`
- manifest(unity): `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Packages/manifest.json`
