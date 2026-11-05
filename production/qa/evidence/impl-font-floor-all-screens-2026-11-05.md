# 纸面字号全屏抬到 24px(结 `TD-skeuo-16` 的字号半边)

| 字段 | 值 |
|---|---|
| **日期** | 2026-11-05 |
| **触发** | 用户指令「再把其它屏的字号也抬到 24px」 |
| **承诺出处** | `design/accessibility-requirements.md:85`(纸面 UI ≥24px @1080p)· `save-slots-7b.md:312` |
| **三元组** | 截至 `2026-11-05 17:4x CST` · 批次 `全屏字号轮` · commit `2058b1ae` + 工作树 |

---

## 1. 改了什么(逐条可复算)

```bash
python3 - <<'PY'   # 扫「低于 24px 的字号声明」,改前 14 处 / 改后 0 处
import re, pathlib
vars={'--skeuo-font-size-xs':11,'--skeuo-font-size-sm':13,'--skeuo-font-size-md':15,
      '--skeuo-font-size-lg':18,'--skeuo-font-size-xl':22,'--skeuo-font-size-paper':24,
      '--skeuo-font-size-lexeme':28,'--skeuo-font-size-default':28}
n=0
for f in pathlib.Path('unity/Assets/Gameplay.UI/Skeuomorphic').glob('*.uss'):
    s=re.sub(r'/\*.*?\*/','',f.read_text(encoding='utf-8'),flags=re.S)
    for m in re.finditer(r'([^{}]+)\{([^}]*)\}',s):
        for fm in re.finditer(r'font-size:\s*var\((--[\w-]+)\)',m.group(2)):
            if vars.get(fm.group(1),24)<24: n+=1; print(f.name, m.group(1).strip())
print('低于 24px:',n)
PY
```

14 处 → `--skeuo-font-size-paper`(24px):

| 文件 | 类 | 原值 |
|---|---|---|
| `SkeuoBrass.uss` | `.brass` | md 15 |
| `SkeuoImplement.uss` | `.implement` | md 15 |
| `SkeuoInk.uss` | `.ink` / `.ink-title` / `.ink-body` | md 15 / lg 18 / sm 13 |
| `SkeuoMarks.uss` | `.mark-check` / `.mark-seal` | lg 18 / sm 13 |
| `SkeuoPaper.uss` | `.paper .ink` / `.pause-save-hint` / `.menu-notice` / `.skip-entry` | md 15 / xs 11 / xs 11 / md 15 |
| `SkeuoScroll.uss` | `.scroll .ink` | md 15 |
| `SkeuoSeal.uss` | `.seal` / `.seal-small` | lg 18 / md 15 |

⚠️ `.ink-title` / `.ink-body` / `.seal-small` **零挂载点**(`grep -rn "\"<class>\"" unity/Assets --include=*.cs` = 0),
一并改是为了不留下「看起来还有活类低于下限」的假线索。

## 2. 门(新增,替代「列名单」的做法)

`unity/Assets/Tests/EditMode/SkeuomorphicUI/skeuo_readability_test.cs`
→ `test_s3_2c_everyUssFontSize_atLeastPaperFloor`

- **扫全量**:九份 USS 里每一条 `font-size: var(--skeuo-font-size-*)` 解析成 px,与字面量 **24** 比;
  不列类名单(名单会腐烂:新加一个类就漏)。
- **空集守卫**:扫到的声明数 < 10 ⇒ 红(路径写错/正则失效时不会恒绿)。
- **证伪轮**:把 `.menu-notice` 改回 `--skeuo-font-size-xs`(11px)⇒ 该门当场红、**零连带**,
  `SHA-256` 逐字节还原。

## 3. 回归

| 套件 | 结果 | 等式 |
|---|---|---|
| EditMode | **`6940 / 6903 / 0 / 0 / 37`**(`ed-24all.xml`) | `6940 − 6939 = 1` = 新增门数 ✅ |
| PlayMode | **`341 / 337 / 0 / 2 / 2`**(`pm-24all.xml`) | 逐位不变 ✅ |

## 4. 实机复验(真 player,非夹具)

`CostTableProbeBuildCli` 出包(17.7 s)→ `DISPLAY=:0` 运行 → XTEST 发键 / XGetImage 截图:

| 屏 | 结论 | 图 |
|---|---|---|
| 主菜单 | 四行 24px+ 正常 | (前一轮 `player_main_menu_text.png`) |
| 世界 · 急救候选行 | 止血 / 通气 / 跳过 24px 清晰 | — |
| 暂停壳 | 五行 + 「上次保存：33 分钟前」副标题 24px,未溢出 | `player_pause_hint24.png` |
| 设置壳(调治) | 标题 + 空行正常(条目集仍归 44,`TD-skeuo-11`) | — |
| 脉案(E 键) | 八行 + 右列 病名/？/折,24px+ 清晰 | `player_casebook24.png` |
| 存档册 | 六槽 + 留档 + 页码 24px,页角注不溢出 | `player_savebook24.png` |

## 5. 仍未验(禁借绿)

**库存 / 容器 / 教学 / 炮制 / 纸近景** 五屏在 P0 **无键盘入口**(`TD-skeuo-03`:闭集 9 员里 7 员的
toggle 语义无法表达)⇒ 只有静态门保证字号,**溢出是视觉面、未验**。
故 `TD-skeuo-16` 记 **`partial`** 而非 `closed`。
