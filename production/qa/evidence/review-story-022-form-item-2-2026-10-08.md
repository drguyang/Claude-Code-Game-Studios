# 评审原件 — story-022 M2 形态件②(墨乾湿两态)· 2026-10-08

> **对象**: `production/epics/skeuomorphic-ui/story-022-m2-form-item-2-ink-wet-dry.md`
> **轮次**: 双代理评审**恰一轮**(用户指令);评审发起 2026-10-08,修复与复跑于同夜完成(2026-10-09 凌晨留痕)
> **判定**: 两代理均 **FIX-THEN-APPROVE** → 修复全落 → 复跑绿 → 转 APPROVE
> **交付件**: `SkeuoThemeVariables.uss` 两变量 · `SkeuoInk.uss` `.ink-wet`/`.ink-dry` ·
> `nine-slice-freeze-2026-10-08.md` :53/:54 + :86/:87 · `ink_wet_dry_test.cs` 三条 AC · story 文档

## 一、原判定(两代理逐条)

### 代码面(lead-programmer): FIX-THEN-APPROVE — 1 BLOCKING + 3 ADVISORY

| # | 级 | 位置 | 问题 |
|---|---|---|---|
| L1 | BLOCKING | story-022:95-97 | DoD 点名评审原件 `review-story-022-…-2026-10-08.md` 但文件不存在(流程性:原件本就待评审后落盘 —— **即本文件**) |
| L2 | ADVISORY | story-022:52 | 「L(干) 实测 0.0093」与公式复算不符,真值 **0.0087** |
| L3 | ADVISORY | test:117-120 | 公式锚定只白/黑,对齐 focus 测试先例缺 128 灰;`HueDegrees` 零锚(恒 0 ⇒ ΔH 恒过) |
| L4 | ADVISORY | test:210 | `GreaterOrEqual` 允许相等:两图退化同覆盖率方向锁假绿,与 AC-022-1 严格 Less 口径不一 |

**核过零问题维度**: USS/ADR-013/C2 · 越界检查(零施加点/未注册/零新贴图/洇开零代码) · 冻结记录/C8 双侧同步 · helper 公式独立复算(含 `%6` 左结合、负值回绕、环回) · 文档对账(233=230+3)。

### 测试面(qa-lead): FIX-THEN-APPROVE — 1 BLOCKING + 10 ADVISORY

| # | 级 | 位置 | 问题 |
|---|---|---|---|
| Q1 | BLOCKING | test:49-59 | `ExtractThemeColor` **不剥注释 + 不钉唯一匹配**:注释掉声明保留原文 ⇒ 抽死文本判据绿、运行期 `var()` 失效;残留同形旧声明首匹配读旧值;C2 门同源同盲无兜底 |
| Q2 | ADVISORY | test:174-178 | guid 只查 `.meta` 存在,不比对 meta 内 guid == 常量(第三方真源漂移可整体假绿) |
| Q3 | ADVISORY | test:184-191 | 冻结记录断言全文件 `Contains`,未限定 `freeze-v1` 围栏;行内空格 `\| 0 \|` 比 C8 脆 |
| Q4 | ADVISORY | test:149-158 | guid 断言未锚 `background-image: url(...)` 属性全形 |
| Q5 | ADVISORY | test:161-166 | 「color 走 var」为存在性断言:后置 `color:` 覆盖假绿 |
| Q6 | ADVISORY | test:116-120 | 白/黑钉不住 Rec.709 权重互换,注释「防权重退化」超实 |
| Q7 | ADVISORY | test:33-36 | 12° 阈值贴近灰 LSB 噪声上沿(≈12°/LSB),数值轮微调可假红,须登记分解口径 |
| Q8 | ADVISORY | test:210 + story:73 | 名 `covers_more`、story 写「大 vs 小」,断言却是 `GreaterOrEqual` |
| Q9 | ADVISORY | test:214-220 | `Coverage` 不看 alpha:RGBA 透明底 `(0,0,0,0)` 计墨,双图齐趋 1 系统性假绿 |
| Q10 | ADVISORY | story:39-40 | 边界裁定 4「两值均提案、判据不锚 hex」与 AC/测试锚湿=`#26241F`、art-bible §4.1/§4.5 分层口径打架 |
| Q11 | ADVISORY | 缺 | 「不进 SkeuoComponentRegistry」零守卫测试,误注册后 C3/C7 全绿不可见 |

**合规零问题维度**: 命名(承 `test_ac021_3` 先例) · AAA 齐 · 门式读仓例外声明充分 · 无随机/时间依赖 · `DestroyImmediate` 清理 · hex 字节比较大小写免疫 · 阈值【提案】标注不冒充终值。

## 二、修复落点(全 12 项 · 同批)

| # | 修复 | 落点 |
|---|---|---|
| L1 | 本原件落盘 | `production/qa/evidence/review-story-022-form-item-2-2026-10-08.md` |
| L2+Q10 | `0.0093`→`0.0087`;边界裁定 4 收窄为「干态不锚 hex 提案 / 湿态锚 §4.1 权威」 | story-022:52 · :39-40 段 |
| L3+Q6 | 公式锚定扩为 **白/黑/128灰 + 纯通道三锚(0.2126/0.7152/0.0722) + 色相两锚(红 0°/绿 120°)** | test `test_ac022_1` Assert ① |
| L4+Q8 | `GreaterOrEqual` → **`Greater`**(严格大);story 步③「≥」→「>」×2 | test AC-022-3 · story:72-74 |
| Q1 | `ExtractThemeColor` **先剥块/行注释 + `Assert.AreEqual(1, Matches.Count)` 钉唯一活声明** | test helper |
| Q2 | `.meta` 存在 **+ 正则比对 `^guid: <常量>$`** | test Assert ④ |
| Q3 | 围栏正则照 C8 同款(```freeze-v1\r?\n…\n```)+ 行匹配 `\s*\|\s*` 容忍行内空格(不比 C8 脆) | test Assert ⑤ |
| Q4 | guid 断言收为 `background-image:\s*url\(\s*"guid:…"\s*\)` 全形 | test Assert ① |
| Q5 | `color:` **恰 1 处 + 该处即对应 var**(后置覆盖即红) | test Assert ② |
| Q7 | 12° 注释补「近灰 LSB ≈12°/LSB 灵敏度;假红先 ΔH 分解,勿直接放宽」 | test `HueDriftMaxDegrees` 文档注 |
| Q9 | `Coverage` 开头 **fail-loud 全图 a==255**(RGBA 改判须先重裁口径) | test AC-022-3 |
| Q11 | 新增 Assert ⑥:`SkeuoComponentRegistry.cs` 源零 `"ink-wet"/"ink-dry"` 注册 | test Assert ⑥ |

## 三、验证命令(可证伪)

```bash
# 过滤(修复后终态)
unity test unity --mode EditMode --filter "SkeuomorphicUI" \
  --output unity/Logs/editmode_skeuo_final.xml
# → 233 total / 220 passed / 0 failed / 13 skipped(= 基线 230/217 +3 新)

# 全量 EditMode(基线 3007/2960/0/46/1inc +3 ⇒ 3010/2963/0/46/1inc)
unity test unity --mode EditMode \
  --output unity/Logs/editmode_full_20261009_form02.xml
unity test unity --mode PlayMode \
  --output unity/Logs/playmode_full_20261009_form02.xml
# → 98/97/0红/1跳 逐数 = 基线
```

## 四、判别力变异(6 发全中 · 逐发 python 反向恢复零残留)

| 发 | 注入 | 期望红 | 实测 |
|---|---|---|---|
| MUT-1 | dry `#191714`→`#16291c`(色相漂移,仅破 ΔH 分支) | 022-1 | ✅ 恰红 022-1 |
| MUT-2 | dry →`#3a352c`(色沉方向反转,ΔH 仍过) | 022-1 | ✅ 恰红 022-1 |
| MUT-3 | `.ink-wet` guid → ink_dry guid | 022-2 | ✅ 恰红 022-2 |
| MUT-4 | 两 png 字节对调(wet 覆盖 0.969→0.646) | 022-3 | ✅ 恰红 022-3 |
| MUT-5 | **dry 声明整行注释掉(Q1 BLOCKING 修复判别力)** | 022-1 | ✅ 恰红 022-1(修复前此注入为假绿) |
| MUT-6 | **`.ink-wet` 后置 `color: red`(Q5 恰一断言判别力)** | 022-2 | ✅ 恰红 022-2(修复前存在性断言下假绿) |

恢复终态:两 USS `grep MUT` = 0;png 对调两次回到原字节(复跑复绿实证);每发恰一红,无涟漪。

## 五、复跑实数(终态)

- 过滤 **233 / 220 passed / 0 failed / 13 skipped**(`editmode_skeuo_final.xml`)
- 全量 EditMode **3010 / 2963 / 0 红 / 46 跳 / 1 inc**(`editmode_full_20261009_form02.xml`)—— 与基线逐项一致,零回归
- 全量 PlayMode **98 / 97 / 0 红 / 1 跳**(`playmode_full_20261009_form02.xml`)—— 逐数一致
- 变异 6 发恰红 + 零残留(上表)

## 六、判定链

原判定(两代理 FIX-THEN-APPROVE)→ 12 项同批修复 → 补 2 发修复判别力变异恰红 →
过滤/全量双套零回归 → **转 APPROVE**。story-022 DoD 「双代理评审恰一轮 → 修复 → 复跑绿」就此闭环。
