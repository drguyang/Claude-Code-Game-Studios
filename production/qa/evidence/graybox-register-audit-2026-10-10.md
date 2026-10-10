# M2 灰盒登记表审计(批次 F-3)

| 字段 | 值 |
|---|---|
| **日期** | 2026-10-10 |
| **对象** | M2 灰盒登记表(`production/milestones/README.md` §三之附,2026-10-03 落) |
| **依据** | `design/art/art-bible.md` §8.11(8.11.2 判定点 D1/D2/D3 · 8.11.4 落点 · 8.11.5 spike 隔离令)· `production/sprints/sprint-04.md` 行 7「灰盒登记表审计」 |
| **判定者** | qa-lead(§8.11.3「灰盒登记是否完整」) |
| **结论** | **登记面 ✅ 完备(无 D1 漏登)** · **1 项存量违例仍存**(spike 未隔离,§8.11.5) |

## 一、判据与结果

| # | 判据 | 结果 | 证据 |
|---|---|---|---|
| ① | 登记表存在且结构完整(§8.11.4:落点不另立清单;类/项/状态/依据四列;两态无第三态) | ✅ | `milestones/README.md:168-189` —— 表头四列齐;「真做 / 灰盒 / 不在 M2 面内」逐行带依据;附已知缺口段(贴图接入 → story-019 承接) |
| ② | **D1 无漏登**:实际入库资产对照登记表 | ✅ | 实测入库全集(见 §二)全部被表覆盖或属真资产行;其余灰盒类(角色/道具/物品/界面/HUD/VFX)**零资产入库** ⇒ D1 触发条件未发生,无漏登 |
| ③ | 4 项真形态件已真做(不可灰盒,G1–G6 投影) | ✅ | story-021 ✅ 10-08 · 022 ✅ 10-08 · 023 ✅ 10-09 · 024 ✅ 10-09(`skeuomorphic-ui/EPIC.md:111-114`)—— **4/4 齐** |
| ④ | §8.11.5:spike 工件隔离到 `prototypes/`,不得落 `unity/Assets/Scenes/` | ❌ **违例仍存(存量)** | 7 件仍在(实测 2026-10-10):`SpikeAssump6.{unity,uxml,uss}` · `SpikeAssump6_Panel.asset` · `SpikeS1_Temp.unity` · `SpikeS4_A1/A2/B.unity` · `SpikeCube.prefab`。10-03 已坐实登记(`expert-panel-art-asset-2026-10-03.md` §9.2 ③)**至今未处置** |
| ⑤ | (附)真形态件硬前置 story-019 贴图接入 | ◐ | `EPIC.md:109`:019-c/d/e ✅ 10-05 · 019-f ✅ 10-08 · **019-b Blocked**(PAGES_MAX spike)—— 供 F-6 M2 Exit 第 5 条复评,非本表缺陷 |

> **D2(出图)未触发**:本审计时点无人工 playtest,F-5 在其后。
> **D3(里程碑收口)**:M2 未转 Complete;本审计为 D3 的预演 —— 按①②,M2 范围灰盒无未登记项。

## 二、实测证据(可证伪)

```bash
# 实测入库美术资产全集(2026-10-10):18 png + 1 mat + spike 组
find unity/Assets -type f \( -name "*.png" -o -name "*.mat" -o -name "*.prefab" \) ! -path "*/Packages/*"
#   → 18 × Gameplay.UI/Skeuomorphic/Textures/**(真资产:五族切图 16 + casebook + focus_brass_2px)
#   → Scenes/WorldGround.mat(环境类灰盒 ⇒ 表内「环境 医馆 · 灰盒」类级覆盖)
#   → Scenes/SpikeCube.prefab(违例,见 ④)

# spike 违例 7 件(§8.11.5 隔离令)
ls unity/Assets/Scenes/ | grep -i spike
#   → SpikeAssump6_Panel.asset · SpikeAssump6.unity · SpikeAssump6.uss ·
#     SpikeAssump6.uxml · SpikeS1_Temp.unity · SpikeS4_A1/A2/B.unity

# SpikeCube 已进 Addressables(处置时须一并解引用)
grep -rln "52bc9a316f5e7e8d79f69e6c6efd5b31" unity/Assets | grep -v SpikeCube.prefab.meta
#   → unity/Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset
```

## 三、遗留(登记,非本审计执行面)

| # | 遗留 | 归属 | 说明 |
|---|---|---|---|
| L-1 | **spike 7 件隔离处置**(`unity/Assets/Scenes/` → `prototypes/`) | 待裁(非批次 F 范围) | 10-03 已登记的存量违例;处置涉及 Addressable `Default Local Group` 解引用 + meta/GUID 跨树移动,**多文件结构改动,须单独方案过目**,不擅动 |
| L-2 | story-019-b 图集页数 spike(Blocked) | skeuomorphic-ui | 形态件 ①② 压切图的硬前置之一;供 F-6 Exit 第 5 条复评 |

## 四、审计判定

**灰盒登记表完备**:D1 登记面无漏登(判据①②)· 4 真形态件 4/4 齐(判据③)· D3 预演通过。
**唯一违例** = §8.11.5 spike 隔离令(判据④),为 2026-10-03 已坐实的存量登记项,本审计确认**仍存**、未引入新违例。
**批次 F-3 完成**(2026-10-10)。
