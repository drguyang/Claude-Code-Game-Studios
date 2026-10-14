# 图集预算冻结件(PAGES_MAX · S2 spike · round-2)

> **冻结轮次**: round-2 · **2026-10-14**(round-1 同日;round-2 = 两张超规图重生 1024² 入库后的重测重冻,承本件铁律)
> **对应**: `story-019` 分档 **019-b**(AC-42-C9)· `sprint-09` §2 S2
> **依据**: `skeuomorphic-ui.md` F5(图集与批次护栏)「`Pages_max` **由 spike 定,留白**」·
> 2026-10-04 J 裁「**禁填占位值**(占位值 = 第二真源)」·
> 用户 2026-10-14 三项裁定(决策门 = 只裁 `PAGES_MAX` · 冻结口径 = **实测倒推** ·
> 语义 = **改冻「入图集资格」**)
> **实测门**: `unity/Assets/Tests/PlayMode/SkeuomorphicUI/atlas_page_count_test.cs`
> (batch PlayMode · round-1 `Logs/s09_atlas_final.xml` / round-2 重测 `Logs/s09_atlas_reimport2.xml` · 均 **2/2 Passed** · 2026-10-14)
>
> 🔴 **本件是图集预算判据的唯一真源。**
> 改任何冻结值 = **重开冻结轮**(改本件 → 同批改门 → 复跑)。
> 任何一张图**重画 / 替换 / 新增** ⇒ 本轮作废,须重测重冻。

---

## 一、核心结论:引擎模型与 GDD F5 假设不符(须修订 F5)

实测(反射逐层,非文档推测)确证 Unity 6.3 的 UI Toolkit 图集**不是** GDD F5 假设的
「N 页 = `ceil(Σ面积 / A_page)`」可增长页集合。

**`DynamicAtlas` 的全部字段(实测)**:
`m_Database` · `m_PointPage` · `m_BilinearPage` · `m_ColorSpace` · `m_Panels` ·
`m_MinAtlasSize` · `m_MaxAtlasSize` · `m_MaxSubTextureSize` · `m_ActiveFilters` ·
`m_CustomFilter` · `textureRegistry`

**`DynamicAtlasPage` 的全部字段(实测)**:
`textureId` · `atlas` · `format` · `filterMode` · `minSize` · `maxSize` ·
`m_1Padding` · `m_2Padding` · `m_Allocator` · `m_Blitter` · `m_CurrentSize` · `disposed`
⇒ **无页链、无页列表**。

⇒ **真模型 = 固定 2 页**(Point 一页 + Bilinear 一页),各带一个分配器,从 `minSize`
长到 `maxSize`。

**后果**:
- `Pages_frame` 由构造**恒 ≤ 2** ⇒ F5 的「`Pages_frame ≤ Pages_max`」约束**恒真**,
  **不是有意义的门**;
- 真正的约束量是**单页瓦片容量**(`maxAtlasSize / maxSubTextureSize`)²。

> ⚠️ **F5 须修订**(归 42 的 GDD 轮):把「页数」量纲改为「**单帧同屏瓦片数**」,
> 或明确「2 页固定 + 容量约束」。**本件不擅自改 GDD**,只登记冲突与实测依据。

---

## 二、实测冻结值

| 量 | 实测值 | 来源 | 含义 |
|---|---|---|---|
| `minAtlasSize` | **64** | 门 `CAPACITY` 行 | 页初始边长(px) |
| `maxAtlasSize` | **4096** | 门 `CAPACITY` 行 | 单页边长上限(px) |
| `maxSubTextureSize` | **1024** | 抬升后(用户裁定) | 单图入集门槛(px) |
| **`tilesPerPage`** | **16** | `(4096/1024)²` | 单页可容满尺寸图数 |
| **页数** | **固定 2** | 门 `pageCount=2` 断言 | Point + Bilinear |
| **单帧同屏瓦片上限** | **32** | `16 × 2` | 两页合计容量 |

### 冻结判据(替代原 `Pages_max` 页数门)

```
PAGES_MAX 语义冻结为:
  ATLAS_PAGE_COUNT_FIXED = 2          (引擎固定,不可配置)
  ATLAS_TILES_PER_PAGE   = 16         (= (maxAtlasSize/maxSubTextureSize)²)
  ATLAS_TILES_PER_FRAME_MAX = 32      (= 2 × 16,单帧同屏瓦片硬上限)
```

**溢出告警阈值**:当**单帧同屏**的图集瓦片数 > **32** ⇒ 断批风险
(超出的图会被挤到下一帧或走非图集路径 ⇒ 额外 draw call)。

> ⚠️ **与 F5 公式的调和**:F5 的 `Pages_total = ceil(Σ面积 / A_page)` 在 2 页固定模型下
> 应改读为「**瓦片占用数** = `Σ_i ceil(w_i / maxSubTextureSize) × ceil(h_i / maxSubTextureSize)`」,
> 门落在**单帧同屏瓦片数**上,而非页数。

---

## 三、入图集资格分布(门槛 1024)

> **⚠️ 本节已于 2026-10-14 当日重冻(轮 round-2)** —— 触发 = 冻结件铁律「任何一张图重画/
> 替换 ⇒ 本轮作废,须重测重冻」:两张超规图同日重生 1024² 入库替换(meta/GUID 未动,
> 提交 `e891ecd`)。重测 = 三跑 batch(`Logs/s09_atlas_reimport2.xml` · **2/2 Passed**)。
> round-1 的资格数字保留于下表括注,不删(留重冻依据)。

实测 `ELIGIBILITY|threshold=1024|total=18|eligible=18|oversized=0`
(round-1:`total=18|eligible=16|oversized=2` —— 超规 2 张已重生结清)

| 状态 | 张数 | 明细 |
|---|---|---|
| **合格**(≤1024²) | **18**(round-1:16) | 五族 16 张(**`ui_icons_sprite` 1024² 重生版**,t2i `watermark:false`,网格谷 ±6px)+ `Casebook/casebook_paper_base` 1024²(频域补偿降采样,纤维 std 7.59 保持)+ `Brass/focus_brass_2px` 64²(用户明裁最小豁免) |
| **超规**(>1024²) | **0**(round-1:2) | ~~`ui_icons_sprite` 2048² · `Casebook/casebook_paper_base` 2048²~~ **均已重生 1024²**(留痕 `design/assets/ai-generation-prompts.md` §九之三;量化判读:① 覆盖率 4.6–29.1%/水印残值 2.04%;② 纤维 std 7.59/淡墨 p50 7.18:1 过 7:1 门) |

> ⚠️ **首轮实测纠正了手算**:手算「17 合格」是**错的**(误把 `ui_icons_sprite` 算作 1024²
> 合格)。实测 16 才是真值(round-1)。**数字以实测为准,禁手算** —— round-2 的 18 亦为实测
> (`test_raisedThreshold1024_eligibilityDistribution` 断言已随更新 16/2→18/0,提交 `1c03f2f`)。

**两张超规图的处置**(承用户 2026-10-14 裁定 + `feedback-images-serve-requirements`):
> 图片服务于需求,不合规格用 `/image-gen` **重生**为 1024²,
> **不得抬门槛到 2048 给它们开豁免**(豁免一开,规格被持续稀释)。
> `focus_brass_2px` 64² 是用户**明裁的最小切片豁免**(`art-bible §7.4`),保留。

---

## 四、batch 下的可测边界(18 轮取证)

**batchmode PlayMode 下纹理永不注册进图集**(`m_Database` 恒 `count=0`)。逐层排除:

| # | 假设 | 实测 | 结论 |
|---|---|---|---|
| ① | 无渲染目标 | 挂 `RenderTexture` 作 `PanelSettings.targetTexture` | **无效** |
| ② | 无 GL 设备 | `DISPLAY=:0` 下 `gfxDevice=OpenGLCore` / `OpenGL 4.5.0 NVIDIA` | **非此因** |
| ③ | 渲染回调被禁 | 强制 `EnableRenderingAndInputCallbacks` + `OnEnteredPlayMode` | **无效** |
| ④ | 未驱动 repaint | 强制 `RepaintPanels` / `RepaintPanel(panel)` / `RenderRootTree` / `ProcessChanges` / `OnUpdateDynamicTextures` | **无效** |
| ⑤ | 页纹理 | `<atlas>k__BackingField` **恒空** | **根因:需真实 GPU 绘制 pass** |

⇒ **「实测页数 N」在 batch 下结构性不可测**。
**残留**:真机 / 编辑器实测「某屏实际取到几瓦片」= 后续轮(登记,**不阻塞本冻结** ——
容量模型已足以定判据)。批下**可测**的 = 容量模型(2 页 / 16 瓦片/页)与资格分布,均已测。

---

## 五、验证命令(可复现)

```bash
# 独占工程后(先 pgrep + 查 unity/Temp/UnityLockfile)
cd unity
DISPLAY=:0 /home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -batchmode -projectPath . -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI.atlas_page_count_test" \
  -testResults Logs/s09_atlas_final.xml
# 期望:total="2" passed="2" failed="0"
# 关键行:[AtlasPageCount] CAPACITY|minAtlasSize=64|maxAtlasSize=4096|maxSubTextureSize=1024|tilesPerPage=16|pageModel=Fixed2(Point+Bilinear)
```

---

## 六、对上游的影响

| 对象 | 影响 |
|---|---|
| `skeuomorphic-ui.md` F5 | ⚠️ **须修订** —— 「页数」量纲改为「单帧同屏瓦片数」(归 42 的 GDD 轮) |
| `story-019` AC-42-C9 | 判据改锚到 `ATLAS_TILES_PER_FRAME_MAX = 32`;019-b **解 Blocked** |
| `nine-slice-freeze-2026-10-08.md` | 不受影响(切图值与图集预算正交) |
| `adr-013` §6.6 假设 6 / Verification ④ | **部分结清** —— 「图集阈值 spike(F6)」已跑;余「真机实测瓦片取用」待后续轮 |
| ~~2 张 2048² 超规图~~ | ✅ **已重生 1024² 结清(2026-10-14 当日,round-2 重冻)** —— `e891ecd`;量化判读与留痕见 §三 表 + `ai-generation-prompts.md` §九之三 |
