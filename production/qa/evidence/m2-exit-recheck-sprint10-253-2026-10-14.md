# M2 Exit `:253` 复评原件 —— 五族切图与 atlas 布局冻结

> **复评对象**:`production/milestones/README.md` §三 Exit Criteria 第 7 条
> 「五族切图与 atlas 布局冻结」(`:253`,M2 §三 **唯一未勾条**)
> **复评日期**:2026-10-14
> **性质**:M2 收口前的单条复评(勾选滞后的**实质推进**复核,非形式补勾)
> **前置链**:story-019(019-b 冻结轮 2026-10-14)· atlas-budget-freeze-2026-10-14.md
> (round-2 重冻同日)· 两张超规图重生(`e891ecd`)· atlas 门断言更新(`1c03f2f`)

---

## 一、复评问题

`:253` 保持 `[ ]` 的登记理由(2026-10-08 冻结轮时点):

> 余 **atlas 布局/页数半 = 019-b Blocked**(`PAGES_MAX` spike 未冻结)⇒ **本条保持 `[ ]`,禁借绿**

**复评问**:该理由在 2026-10-14 当下是否仍成立?

## 二、逐项实测(独立取证,非重读旧件)

| # | 复评点 | 命令/证据 | 实测结果 |
|---|---|---|---|
| 1 | PAGES_MAX spike 是否已跑 | `design/assets/specs/atlas-budget-freeze-2026-10-14.md`(round-2) | ✅ 已跑且冻结 —— 实测倒推:固定 2 页(Point+Bilinear)/ 每页 16 瓦片 / 单帧同屏瓦片上限 32;**非占位值**(承 2026-10-04 J 裁) |
| 2 | 实测门当前是否绿 | batch 三跑 `Logs/s09_atlas_reimport2.xml`(23:21,两张 1024² 重入库后) | ✅ **2/2 Passed**(`test_atlasCapacityModel_measured` + `test_raisedThreshold1024_eligibilityDistribution`) |
| 3 | 资格分布与断言一致性 | 同上 XML + `atlas_page_count_test.cs` 断言(`1c03f2f`) | ✅ eligible=18 / oversized=0,断言与实测同批更新,无第二真源漂移 |
| 4 | 切图冻结半(story-020 步①③) | `design/assets/specs/nine-slice-freeze-2026-10-08.md`(`freeze-v1` 机器块) | ✅ 在盘(2026-10-08 收口,本轮不变) |
| 5 | 019 接线 c/d/e/f | `story-019-texture-binding.md` §状态拆分 | ✅ 四档全 ✅(2026-10-05~10-08) |
| 6 | 重冻铁律是否兑现 | 冻结件 round-2 头注 + §三 资格表重冻 | ✅ 「图替换 ⇒ 重测重冻」已执行(round-1 超规 2 → round-2 超规 0) |
| 7 | 两张重生图量化达标 | `ai-generation-prompts.md` §九之三 | ✅ ① 网格谷 ±6px/覆盖率 4.6–29.1%/水印 2.04%;② 纤维 std 7.59/淡墨 p50 7.18:1 过 7:1 门/裁剪 0.000%/冷色 0.00% |

## 三、残留项与裁定(复评核心)

冻结轮自登记三件残留(「归实现轮,非本冻结阻塞」)。本复评逐件裁定其是否卡 M2 Exit:

| # | 残留 | 性质 | 是否卡 M2 Exit | 理由 |
|---|---|---|---|---|
| R1 | 构建期「瓦片取用 ≤32」断言门**接线** | 实现轮义务(门判据已冻结,接线是工程活) | **否 —— 转独立待办** | M2 判据字面 = 「冻结」非「运行期强制」;冻结件 + 实测门已把**判据**立住;接线是护栏自动化,不改变冻结事实。类比先例:C8 冻结件一致性门亦是冻结后才接棒。**风险登记**:接线前若新增大图,门是纸面的 —— 由「资格分布测试 18/0」兜底(任何 >1024² 新图会打红该断言) |
| R2 | 真机「某屏实际取到几瓦片」实测 | batch 结构性不可测(冻结件 §四,18 轮取证) | **否 —— 转独立待办** | batch 下**结构性不可证** ≠ 判据未达;容量模型(32 瓦片)是**上限**判据,真机实测是「实际用量」观测,两者量纲不同。真机轮归编辑器打开窗口期顺带取证 |
| R3 | GDD F5 量纲修订(页数→瓦片数) | 文档修订,归 42 的 GDD 轮 | **否 —— 转独立待办** | F5 是 GDD 侧旧假设,冻结件 §一 已登记冲突与调和口径;GDD 修订不回改已冻结的实测判据(单向:冻结件为准) |

**裁定**:三残留**均不卡 M2 Exit** —— 它们的共性是「判据已立、执行面后置」,而 M2 `:253` 的判据字面是「**冻结**」(切图早错 = 全局返工的返工险,已由冻结件消除)。

## 四、判定

**✅ `:253` 判据达成,翻 `[x]`** —— 组成两半均已实测闭合:
- **切图冻结半**:story-020 步①③ + `nine-slice-freeze-2026-10-08.md`(2026-10-08,本轮复核在盘);
- **atlas 布局/页数半**:019-b 冻结轮 + round-2 重冻 + 实测门 2/2(2026-10-14,本轮取证)。

**M2 Exit Criteria 由此全勾(§三 10/10)**。

## 五、验证命令(可复现)

```bash
# 1. 实测门(batch,先核工程独占:pgrep Unity + 查 unity/Temp/UnityLockfile)
cd unity && /home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -batchmode -nographics -projectPath "$(pwd)" -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI.atlas_page_count_test" \
  -testResults ../Logs/s09_atlas_verify.xml -logFile ../Logs/build-atlas-verify.log
# 期望:total="2" passed="2" failed="0"

# 2. 资格分布关键行
grep -o 'ELIGIBILITY|[^|]*|total=[0-9]*|eligible=[0-9]*|oversized=[0-9]*' ../Logs/build-atlas-verify.log
# 期望:threshold=1024|total=18|eligible=18|oversized=0
```

## 六、转出义务

- 三残留转独立待办(非 M2 欠账):R1 门接线 · R2 真机瓦片实测 · R3 F5 量纲修订
  —— 登记落 `production/milestones/README.md` `:253` 翻勾注。
- **禁借绿声明**:本原件评的是「冻结达成」;三残留若后续判实质化(如出现 >32 瓦片的屏),
  须按冻结件铁律重开冻结轮,**不因本翻勾豁免**。
