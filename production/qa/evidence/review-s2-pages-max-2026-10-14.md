# 评审原件 — S2 `PAGES_MAX` spike(019-b · sprint-09)

> **对象**: `story-019` AC-42-C9 / `sprint-09` §2 S2 —— 图集预算冻结
> **日期**: 2026-10-14
> **执行**: Claude Code(自行跑,非用户手动操作编辑器 —— 承 CLAUDE.md「凡 Unity CLI 能
> 确认/复现/调试的,一律自己跑」)
> **结论**: ✅ **判据已由实测倒推冻结,019-b Blocked 解除**
> **产出**: `design/assets/specs/atlas-budget-freeze-2026-10-14.md`(唯一真源)

---

## 一、原判定(冻结前)

`skeuomorphic-ui.md:1150` 明写 `PAGES_max`「**由 spike 定(F6),留白**」;
既有估值(TA 5–7 页 / AD 3 页)**均未做实际打包测算**;
2026-10-04 J 裁「**禁填占位值**(占位值 = 第二真源)」。
`story-019` AC-42-C9 原措辞「实测**图集页数 ≤ `PAGES_MAX`**」⇒ 判据指向的对象**未经实测确证存在**。

## 二、实测过程(18 轮迭代)

| 轮 | 假设 / 动作 | 结果 |
|---|---|---|
| 1–2 | 反射链候选名 `m_Renderer` | ❌ 引擎真名是 **`panelRenderer`**(TRACE 实证) |
| 3 | `panelRenderer` 下找 `m_Atlas` | ❌ 真结构是 `renderTreeManager` → `<atlas>k__BackingField` |
| 4 | 页数 = `m_Database` count | ❌ `m_Database` = `Dictionary<Texture,…>`,**count=0** |
| 5 | 挂 `RenderTexture` 作渲染目标 | ❌ 仍 0 |
| 6 | `DISPLAY=:0`(真 GL) | GL 起来了(`OpenGL 4.5.0 NVIDIA`),仍 0 |
| 7–9 | 强制 `UpdatePanels` / `RepaintPanels` / `RenderRootTree` / `ProcessChanges` | ❌ 仍 0 |
| 10 | 强制 `EnableRenderingAndInputCallbacks` | ❌ 仍 0 |
| 11–16 | dump `DynamicAtlas` / `DynamicAtlasPage` 全字段 | ✅ **拿到真结构** |
| 17–18 | 确认页纹理 `<atlas>k__BackingField` 恒空 | ✅ **根因锁定** |

## 三、修复落点(实测确证)

### 3.1 引擎真结构(推翻 GDD F5 假设)

```
PanelSettings → panelRenderer(UIRepaintUpdater) → renderTreeManager(RenderTreeManager)
              → <atlas>k__BackingField(DynamicAtlas)
```

`DynamicAtlas` 全部字段:`m_Database` · `m_PointPage` · `m_BilinearPage` · `m_ColorSpace` ·
`m_Panels` · `m_MinAtlasSize` · `m_MaxAtlasSize` · `m_MaxSubTextureSize` · `m_ActiveFilters` ·
`m_CustomFilter` · `textureRegistry`

`DynamicAtlasPage` 全部字段:`textureId` · `atlas` · `format` · `filterMode` · `minSize` ·
`maxSize` · `m_1Padding` · `m_2Padding` · `m_Allocator` · `m_Blitter` · `m_CurrentSize` · `disposed`
⇒ **无页链 / 无页列表**。

**结论**:真模型 = **固定 2 页**(Point + Bilinear),**不是** F5 假设的
「N 页 = ceil(Σ面积 / A_page)」可增长页集合。

### 3.2 冻结判据

```
ATLAS_PAGE_COUNT_FIXED      = 2
ATLAS_TILES_PER_PAGE        = 16   (= (4096/1024)²)
ATLAS_TILES_PER_FRAME_MAX   = 32   (= 2 × 16)
```

### 3.3 实测门(可复现)

`unity/Assets/Tests/PlayMode/SkeuomorphicUI/atlas_page_count_test.cs` —— batch PlayMode `[UnityTest]`,
内存 `PanelSettings`(零落盘)+ 18 张贴图挂根 + 反射读容量模型与资格分布。

**实测输出**:
```
CAPACITY|minAtlasSize=64|maxAtlasSize=4096|maxSubTextureSize=1024|tilesPerPage=16|pageModel=Fixed2(Point+Bilinear)
ELIGIBILITY|threshold=1024|total=18|eligible=16|oversized=2
```

## 四、验证命令

```bash
cd unity
DISPLAY=:0 /home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -batchmode -projectPath . -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI.atlas_page_count_test" \
  -testResults Logs/s09_atlas_final.xml
# 实测:total="2" passed="2" failed="0"   (2026-10-14)
```

## 五、未结项(登记,不阻塞本冻结)

| # | 项 | 归属 |
|---|---|---|
| ① | 构建期「单帧同屏瓦片 ≤ 32」断言**门未接线** | 实现轮 |
| ② | **真机实测**「某屏实际取到几瓦片」(batch 结构性不可测) | 后续轮 |
| ③ | **F5 量纲修订**(页数 → 单帧同屏瓦片数) | 42 的 GDD 轮 |
| ④ | 2 张 2048² 超规图 `/image-gen` 重生 1024² | 美术轮 |

## 六、batch 可测边界(重要 — 供后续 spike 复用)

**batchmode PlayMode 下 UI Toolkit 纹理永不注册进图集**(`m_Database` 恒 0)。
根因:页纹理 `<atlas>k__BackingField` 需**真实 GPU 绘制 pass** 才分配。
逐层排除见 §二。⇒ **凡依赖「纹理真进图集」的判据,batch 不可测**;
凡依赖「配置 / 容量 / 资格」的判据,batch **可测**。
