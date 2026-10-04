# 评审原件 —— 拟物 UI story-019-e 导入格式订正

> **评审对象**(工作树未提交改动):
> - 16 个 `unity/Assets/Gameplay.UI/Skeuomorphic/Textures/*-final.png.meta`
> - `unity/Assets/Editor.Tools.Gates/TextureBindingGates.cs`(E1 两门)
> - `unity/Assets/Editor.Tools.Gates/SkeuomorphicUiGates.cs`(聚合两行)
> - `unity/Assets/Tests/EditMode/SkeuomorphicUI/texture_binding_gate_test.cs`(+5 夹具)
> **权威件**: `production/epics/skeuomorphic-ui/story-019-texture-binding.md` §AC-42-E1
> **日期**: 2026-10-05 · **轮次**: 单轮(承「评审只做一轮」)
> **双代理**: 结构侧(`unity-specialist`)+ QA 侧(`qa-lead`),并行独立出报告

---

## 一、原判定(评审后、修复前)

**QA 侧:REJECT —— 1 BLOCKING(B-1)+ 1 MAJOR + 3 MINOR**(结构侧报告见 §三 登记)
**结构侧**:见 §三 —— 报告未在时限内交付,其已探明的面由本件的独立取证覆盖(不追认)。

### B-1(QA 侧首条)= **枚举误标** —— 判据错,结论翻转

QA 侧据本机 `UnityEditor.CoreModule.xml` **文本顺序**推断
「`SpriteImportMode`:`Single` 先于 `Multiple` ⇒ `spriteMode:1` = Multiple ⇒ 与单图九宫格矛盾」,
据此判 **BLOCKING:交付态不可辩护**。

## 二、独立取证(实测,推翻 B-1)

### 取证 1 · 枚举真值(运行期反射,非文档顺序)

临时 EditMode 探针 `__probe_enum_test` 直读枚举:

```
None=0   Single=1   Multiple=2   Polygon=3
```
日志 `unity/Logs/probe-enum.xml`(探针已删,日志留档)。

⇒ **`spriteMode: 1` = `SpriteImportMode.Single`**,不是 Multiple。
QA 侧与本 story 原文的括注 `(Multiple)` **同源错误** —— 该括注自
commit `6049204`(docs 提交)引入,**从未被任何实测核过**,随后扩散进
story-019 / art-assets-required 两件正文。

### 取证 2 · 引擎回读(Unity 自己说它导入了什么)

临时 EditMode 探针 `__probe_sprite_mode_test` 对 16 张逐张 `AssetImporter.GetAtPath` +
`LoadAllAssetsAtPath`:

```
16 张全部:mode=Single  type=Sprite  alphaTrans=True  spriteCount=1  spriteBorderActual=(0,0,0,0)
```
日志 `unity/Logs/probe-sprite-mode.xml`(探针已删,日志留档)。

⇒ 引擎实读 **`Single`**,与取证 1 一致;且**单图九宫格正是 Single 的正确用法**
(Unity 官方 `SpriteImportMode` 文档:`Single` = "a single image section extracted
automatically from the texture" —— 正是「独立九宫格图」的形态;
`Multiple` = "multiple image sections" —— 图集/切割才用)。
**本 story 的 16 张是彼此独立的元件贴图,不是图集** ⇒ **Single 是对的模式**。

### 结论

- **B-1 判据错,不成立。** 交付态 `spriteMode:1`(= Single)+ `spriteBorder` 零哨兵
  与「单图九宫格」**不冲突** —— 它正是 Single 模式。
- **但暴露了一个真缺陷(文档级)**:四处 `spriteMode: 1(Multiple)` 括注是**误标**,
  会误导 019-f 与后续评审(事实上已误导了本轮 QA 评审)。
  ⇒ **已修**:story-019 §AC-42-E1 + `art-assets-required-for-019` 正文改为
  「`spriteMode: 1`(= `SpriteImportMode.Single`)—— 枚举 `None=0/Single=1/Multiple=2/Polygon=3`,
  实测引擎回读 `Single`;「Multiple」是误标」。
- **`spriteBorderActual=(0,0,0,0)` 是预期态,不是缺陷** —— 其值归 **019-f 冻结件**
  (AC-42-C8);本轮**刻意留零哨兵**,由 `ValidateSpriteBorderLeftAsSentinel` 守住。

### Q1 假绿 / Q4 完整性 —— QA 侧判定**成立,保留**

- 16/16 三项键全绿(shell 直接数盘,不依赖 cwd 锚);目录恰 16 png + 16 meta,无孤儿。
- 反空跑守卫在位(`TextureBindingGates.cs` 两条门目录不存在/零命中 ⇒ 先塞错再 return)。
- 五族枚举 5+4+4+2+1 = 16 逐族对上;`git diff -U0` 核 `guid`/`spriteBorder`/
  `textureFormat`/`maxTextureSize` 等键**全零改动**(本件 §四 复算)。
- 唯一 AC 之外改动 = `nPOTScale: 1→0`(16/16)= 引擎导入副产物,已登记(见 §四 MINOR-3)。

---

## 三、结构侧报告登记(未在时限交付)

结构侧代理(`unity-specialist`)在 20 轮内未交付报告。
按「评审不可事后追补」纪律:**不追认其未出具的面**;其拟覆盖的
「Multiple 是否真为九宫格所需」知识风险面,**已由本件 §二 的实测取证独立结清**
(结论与其预设相反)。本件仅留此登记。

## 四、验证命令与结果(修复后)

```
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.SkeuomorphicUI" \
  --output "$PWD/unity/Logs/skeuo-019e.xml"
```
- **过滤跑**:`unity/Logs/skeuo-019e.xml` = **217 用例 / 204 passed / 0 failed / 13 skipped**
- **全量 EditMode**:`unity/Logs/full-editmode-019e.xml` = **2439 / 2395 / 0 / 43 / 1 inconclusive**
  (基线 2434/2390 ⇒ **+5 新增、零回归**;inconclusive = 既有 `SettingsExposureTest`)

**变异证明**

| MUT | 变异 | 结果 | 日志 |
|---|---|---|---|
| **MUT-E1a** | 单张 meta 回落 `spriteMode:0` | **1 红**(`test_ac42e1_all_sixteen_textures_have_sliced_import_format`) | `unity/Logs/mut-e1a.xml` |
| **MUT-E1b** | 单张 meta 注入 `spriteBorder:{64,64,64,64}` | **1 红**(`test_ac42e1_sprite_border_still_zero_sentinel_pending_019f`) | `unity/Logs/mut-e1b.xml` |

> ⚠️ QA 侧 MAJOR-1 指出「变异未留原件」—— **本件已附日志路径**;两变异文件均已还原。

### 残余 MINOR(如实登记,不判缺陷)

- **N1 `nPOTScale` 无门覆盖**:E1 的 expected 三元组不含它 ⇒ 被改回 `1` 时两门皆绿。
  判定为**引擎派生值**(textureType 0→8 的导入器副产物),**显式登记为不作判据**;
  若后续认定须判,由 019-f 一并纳入。
- **N2 `ResolveRepoRoot` 未用 `IsPathRooted`**(`TextureBindingGates.cs`):相对 candidate
  以进程 cwd 解析,依赖「cwd = `<repo>/unity`」隐式前提。当前安全(上溯 1 层命中;
  退化时守卫报错而非假绿),留作打磨项。
- **N3 新增负夹具恒真**:`test_negative_fixture_wrong_sprite_mode_would_be_caught` 为
  字符串常量自证。**判定力由 MUT-E1a 的门级变异提供**(已留原件);该夹具仅作**文档性**说明,
  不充当判别力证据。**保留**(删去会损失 AC↔夹具可追溯性),但在夹具处加注说明其性质。

## 五、UNKNOWN(未取证 —— 禁借绿)

- **U1** `-unity-slice-*` 在 **UI Toolkit 运行期**对 Single sprite 的确切行为
  (是否读 sprite 的 border / 是否需 `-unity-slice-type: sliced`)—— **未跑运行期**;
  归 019-d 的接图 + 截图签核(本 story 无贴图可看,与 019-c 同口径)。
- **U2** 019-f 冻结件的**落点**(Single 模式下填外层 `spriteBorder` 即生效 —— 与
  `spriteBorderActual` 的回读面一致;待美术值冻结时验证)。
- **U3** CI runner(非本机 cwd)下 `ResolveRepoRoot` 的实值 —— 未跑 CI。
