# 存档册可读性（7b）—— 字号到纸面下限 24px + 行高 44→50

| 字段 | 值 |
|---|---|
| **日期** | 2026-11-05 |
| **触发** | 用户实测：「你自己去实际看看和操作一下，D2 的选项读取和覆写字体看不清」 |
| **裁定** | 用户选定**乙**：`24px + 存档册行高 44→50`（另两案：甲 20px 几何不动 / 丙 全局行高 token 44→52） |
| **改动面** | `SkeuoThemeVariables.uss`（+2 token）· `SkeuoPaper.uss`（5 处字号/行高）· `save-slots-7b.md`（实现回写）· 走查夹具（量尺 **+2 项**） |
| **三元组** | 截至 `2026-11-05 16:32 CST`（`pm-final24.xml` 落盘时刻）· 批次 `存档册字号轮` · commit `ca2aa2cf` + 工作树 |

---

## 1. 归因：不是颜色，是字号（可复算）

取走查帧 `wb_02_armed.png` 的实测像素（墨 `RGB(41,36,24)` / 纸 `RGB(247,237,217)`），
按 WCAG 相对亮度算得**对比 13.29:1**，而承诺值是 **≥7:1** ⇒ 颜色侧合格。
⇒ 唯一未满足的判据是 **字号**：`save-slots-7b.md:312` 的「行字段 / 页角注 ≥24px @1080p」，
而实测行字 **15px**、墨字 **15px**、页角注 **11px** —— 该条自 spec 订立起**从未成立**。

## 2. 字号 → 行高曲线（一次跑量完，七档）

走查夹具内临时探针（跑完已删），二段态行的实测：

| `font-size` | label 高 | 墨字宿主 | 行高 | 对「二段态不得改变行高（±2px）」 |
|---|---|---|---|---|
| 15px（原值 `md`） | 30 | 44 | 45 | ✅ |
| 18px（`lg`，本轮中间态） | 35 | 44 | 45 | ✅ |
| 20px | 37 | 44 | 45 | ✅ ← 44px 行高下的天花板 |
| 22px（`xl`） | 40 | 46 | 47 | ❌ +3 |
| **24px（`--font-size-paper`，现取值）** | **43** | **49** | **50** | ✅（行高已同批抬到 50） |
| 26px | 46 | 52 | 53 | ❌ +3 |
| 28px（`lexeme`） | 49 | 55 | 56 | ❌ +6 |

关系式：`行高 = max(min-height, label + 6)`。24px 时 `43 + 6 = 49` ⇒ 行 **50**。

> ⚠️ **本轮抓到的第一个坑**：15px 与 18px 的 **label 高同为 35**。
> 只量盒子高会得出「CSS 没生效」的结论 —— 实际 `resolvedStyle.fontSize` 已是 18。
> ⇒ 走查量尺已**补** `openFontSize` / `markFontSize` 两项（常驻观测面），
> 防下一轮再犯同型误判。

> ⚠️ **第二个坑（纪律面）**：`Shot()` 首行是 `if (Application.isBatchMode) yield break;`。
> 带 `-batchmode` 跑图形走查 ⇒ **一张截图都不写**，量到的布局也不可作视觉判据。
> 图形走查必须**不带** `-batchmode`，只给 `DISPLAY`。

## 3. 实现

| 文件 | 改动 |
|---|---|
| `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss` | 新增 `--skeuo-font-size-paper: 24px`（24px 原无档位：`xs..xl` 上限 22px）· 新增 `--skeuo-save-row-height: 50px` |
| `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss` | `.save-slot-mark` / `.save-slot-choice-glyph`（含其 `.unity-text-element`）/ `.save-slot-page` 三处字号 → `--skeuo-font-size-paper`；`.save-slot-item` / `.save-slot-writable` 两处 `min-height` → `--skeuo-save-row-height` |
| `unity/Assets/Tests/PlayMode/SkeuomorphicUI/adr041_save_slots_walkthrough.cs` | 量尺补 `openFontSize` / `markFontSize` 两项（**只加观测，未改判据**） |
| `design/ux/save-slots-7b.md` §5.1 | 实现回写（字号轮） |

**未动**：`--skeuo-shared-row-height` 仍 **44px**（全局一动，脉案 / 库存 / 容器三屏的每页容量全部连带，各归其走查）。

## 4. 实测（走查 `[WB][量尺]`，可复跑）

```bash
cd unity
DISPLAY=:0 ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI.Adr041SaveSlotsWalkthrough" \
  -testResults "$PWD/Logs/pm-sb24b.xml" -logFile "$PWD/Logs/pm-sb24b.log"
grep -a "\[WB\]\[量尺\]\|\[WB\] 满页帧" unity/Logs/pm-sb24b.log
```

实跑输出：

```text
[WB][量尺] row=50.0(二段态前 50.0) mark=43.0 host=49.0 open=49.0 rewrite=49.0 \
           openLabel=43.0 openFontSize=24.0 markFontSize=24.0 flexDir=Row
[WB] 满页帧 armed=2 行高 50.0⇒50.0 行文字「叁 · 第叁日 夜」
```

- **二段态前后同为 50.0** ⇒ ADR-041 §D5 的「二段态不多占一行」不破。
- 册页高 **426 → 486** @1080p（6 槽 + 留档 + 页码），上下留白各 **297**，不溢出。
- 页容量仍 **6 行**（`SaveSlotsScreen.PageSize = 6` 未动）。

## 5. 门（机械判据，不是自述）

新增三条 **EditMode 静态门**，落 `unity/Assets/Tests/EditMode/SkeuomorphicUI/skeuo_readability_test.cs`
（承该件既有 ② 的判据形状：纯正则读 USS 字节，不引用 42 类型）：

| 门 | 判据 |
|---|---|
| `test_s3_2b_paperToken_atLeast24px` | `--skeuo-font-size-paper` 存在且 **≥24** |
| `test_s3_2b_saveBookText_bindsPaperToken` | `.save-slot-mark` / `.save-slot-page` / `.save-slot-choice-glyph .unity-text-element` 三块均绑该 token |
| `test_s3_2b_saveBookRows_useSaveRowHeightToken` | 该 token **≥44**(AB-3)；`.save-slot-item` / `.save-slot-writable` 的**生效块**(取最后一块，因 `save-slot-item` 在共享等重块里还有一次) 用 `var(--skeuo-save-row-height)` |

**证伪轮（三处变异，各红在靶点，SHA-256 逐字节还原）**

```text
- M1 纸面字号 token 24→20px        靶测 test_s3_2b_paperToken_atLeast24px=Failed        还原 SHA-256 ✅
- M2 行字段字号回退 md             靶测 test_s3_2b_saveBookText_bindsPaperToken=Failed   还原 SHA-256 ✅
- M3 槽位行 min-height 回退共享 44  靶测 test_s3_2b_saveBookRows_useSaveRowHeightToken=Failed  还原 SHA-256 ✅
```

脚本 = `$CLAUDE_JOB_DIR/tmp/falsify_savebook_type.py`（跑完即弃；每轮 `改 → 跑 → 还原` 同一进程内完成）。

⚠️ **门的边界（禁借绿）**：三条门只证「token 值与绑定」，**不证**「24px 装得进行高」——
后者是无渲染宿主量不出的几何腿（该件文件头已声明），归走查 `[WB][量尺]`。

## 6. 回归（三元组）

| 套件 | 结果 | 基线（同带三元组） | 等式 |
|---|---|---|---|
| EditMode | **`6939 / 6902 / 0 / 0 / 37`**（`ed-final24.xml`） | `6936 / 6899 / 0 / 37`（本轮加门前，同树） | **+3 / +3 = 新增门数 3** ✅ |
| PlayMode | **`341 / 337 / 0 / 2 / 2`**（`pm-final24.xml`） | `341 / 337 / 0 / 2 / 2` @ 2026-11-05 smoke 第二轮 | 零变化 ✅ |

三元组：截至 `2026-11-05 16:32 CST` · 批次 `存档册字号轮` · commit `ca2aa2cf` + 工作树。
（PlayMode `exit=2` 是正常退出码，判据看 XML `failed` 计数。）

```bash
python3 -c "
import xml.etree.ElementTree as ET
for f in ('ed-final24','pm-final24'):
    a=ET.parse('unity/Logs/%s.xml'%f).getroot().attrib
    print(f, a['total'],a['passed'],a['failed'],a['inconclusive'],a['skipped'])"
# ed-final24 6939 6902 0 0 37
# pm-final24 341 337 0 2 2
```

## 7. 仍未做（禁借绿）

1. **其它屏未审**：本轮只把存档册拉到 24px。库存 / 容器 / 设置壳等屏的行字号未审 ——
   `≥24px @1080p` 是全项目纸面承诺，不是本册专属。
2. **文本缩放 150% ⇒ 页容量降 4 行** 仍 `NOT-RUN`（`save-slots-7b.md` §11 既有缺口，与本轮无关）。
3. **人眼复验**：本轮只到「走查帧 + 量尺」；玩家真按「启封 / 重写」的手感未验。
