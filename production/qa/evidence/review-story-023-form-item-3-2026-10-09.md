# 评审原件 — story-023 M2 形态件③(急救零数字 + 可跳过)· 2026-10-09

> **对象**: `production/epics/skeuomorphic-ui/story-023-m2-form-item-3-emergency-zero-numeral-skip.md`
> **轮次**: 双代理评审**恰一轮**(用户指令)
> **判定**: 两代理均 **FIX-THEN-APPROVE** → 修复全落 → 补变异 + 复跑绿 → 转 APPROVE
> **交付件**: `SkeuoPaper.uss` `.skip-entry`(+19 行头注) · `emergency_skip_entry_test.cs` 三条 AC · story 文档

## 一、原判定(两代理逐条)

### 代码面(lead-programmer): FIX-THEN-APPROVE — 1 BLOCKING + 4 ADVISORY

| # | 级 | 位置 | 问题 |
|---|---|---|---|
| L1 | BLOCKING | story-023:86 | AC 勾「全量 EditMode + PlayMode 零回归」但留痕不支持:全量 XML 均为 story-022 时段产物(00:36/00:37)早于 023 首跑(00:51),023 后零全量日志;且与 DoD:95「全量留痕」未勾自相矛盾 |
| L2 | ADVISORY | test:79-85 | 头注锚正则 `/\*.*?跳过入口.*?\*/` 从全文件首 `/*` 起圈(行 1→76 巨型跨度),靠「O-10-1 全文件唯一」巧合收得紧;未来中间注释引用即假绿 |
| L3 | ADVISORY | story:54 | 自述锁「O-10-1」「owner」关键词,实际断言只有 O-10-1 + 「10 实现轮」,无 owner |
| L4 | ADVISORY | test:46-48 | `PropsOf` 的 `([\w-]+)\s*:` 把 `url("guid:…")` 的 `guid` 捕成幻影属性,交集基污染 |
| L5 | ADVISORY | 留痕 | 净态绿日志(00:51)在变异之前,收口不得沿用 —— 修复后须净态过滤复跑 + 全量留痕 |

**核过零问题**: USS/ADR-013 纪律(6 变量全实存/零内联/零新变量/零新图/单槽铁律许可形态)· 变异证据链独立核验 4 发失败集逐一吻合 · 越界零(零施加点/零 Registry 触碰/VR 零涉)· G2 名实相符(剥注释必要且正确;文本内容层漏面如实登记)· 四文档回刷 git diff 逐一核实 · DoD 未勾合规(022 先例)。

### 测试面(qa-lead): FIX-THEN-APPROVE — 1 BLOCKING + 7 ADVISORY

| # | 级 | 位置 | 问题 |
|---|---|---|---|
| Q1 | BLOCKING | test:74-85 | 023-1 主题承载是「var 计数 ≥5」,AC 点名载体**全部未钉**,三分支假绿:① 删 `background-color` 行(5≥5 绿,aged 纸底消失三 AC 仍全绿)② `color: black` 具名色(非 hex/rgb/裸数字,C4 也不拦)③ 异命名空间 `var(--paper-bg)`(未定义仅运行期透明)。story AC 文字「全 var」与实现不等价;4 发变异恰好全没打到 |
| Q2 | ADVISORY | test:79-81 | 同 L2(头注锚巨型跨度,巧合紧) |
| Q3 | ADVISORY | story:54 | 同 L3(owner 未断言) |
| Q4 | ADVISORY | test:105 | `\d+\s*/\s*\d+` 无白名单口径:未来合法比值(`aspect-ratio: 16/9` 型)假红无人敢动 |
| Q5 | ADVISORY | test:46-48 | 同 L4(幻影属性) |
| Q6 | ADVISORY | 缺 | 边界④「不进 Registry」零机械化(022 有同款守卫;误注册静默,16→5 槽不触 C3、非 texture 不触 C7) |
| Q7 | ADVISORY | test:42-43 | `BlockOf` 首配 + 单文件:第二处 `.skip-entry {` 后置覆盖(如 border 复活)对三 AC 全盲 |
| Q8 | ADVISORY | story:33-35 | AB-3「宽半」(列表项宽 ≥44)无落点 —— 023-3 只锁高半,10 轮不接则责任真空 |

**核过零问题**: 剥注释正确性(独立复算:不剥必误红 `40/39/43/43`/`43/57`/`AC-023-1/2/3` 三处)· `.focus-visible` 多选择器失配 fail-loud · 命名/AAA/例外声明/确定性合规 · `.skip-entry` ∩ `.ruled` 级联(min-height 同值同 var 恒等;危险 border 面已锁)· 量纲诚实(gap 纯前瞻非冒充)。

## 二、修复落点(全 13 项 · 同批)

| # | 修复 | 落点 |
|---|---|---|
| L1 | AC 收窄:「过滤绿 + 变异恰红 ✅;**全量归 DoD 留痕**」+注明原勾时序虚报 | story AC 第一条 |
| Q1 | 023-1 重写:**属性集 ⊇ 点名六属性**(删行即红)+ **逐声明值含 `var(--skeuo-`**(具名色/裸数字/异命名空间全灭)+ `valueWhiteList` 显式白名单占位(未来字面值须先入册) | test 023-1 Assert ② |
| L2+Q2 | 头注锚改**紧邻选择器注释组** `/\*(?:[^*]|\*(?!/))*\*/\s*\.skip-entry\s*\{` | test 023-1 Assert ③ |
| L3+Q3 | 补 `StringAssert.Contains("owner", …)`(头注确有 `owner = 42`);story:54 措辞同步为三关键词实况 | test 023-1 · story 步① |
| L4+Q5 | `PropsOf` 改行首锚 `^\s*([\w-]+)\s*:` + Multiline(消 `guid:` 幻影) | test helper |
| Q4 | X/N 扫描登记白名单口径:「合法出现须显式白名单(文件:行+理由)逐处放行,禁整体放宽正则」 | test 023-2 头注 |
| Q6 | 补 Registry 零注册守卫(`"skip-entry"` 源零命中,挂边界④) | test 023-1 Assert ④ |
| Q7 | 补**全库声明块恰 1**(全 `*.uss` 剥注释聚合扫,后置覆盖复活即红) | test 023-1 Assert ⑤ |
| Q8 | story 边界①补「AB-3 宽半由 10 轮候选动作列表载体锁定」 | story 边界裁定 1 |
| L5 | 修复后净态过滤复跑 + 全量双套重新留痕(不沿用变异前日志) | 本件 §五 |
| 非阻塞 | 删未用 `using UnityEngine;`;扫描报错改相对路径;Dependencies 补「(USS 结构面半)」 | test 头部 · story |

## 三、验证命令(可证伪)

```bash
unity test unity --mode EditMode --filter "SkeuomorphicUI" \
  --output unity/Logs/editmode_skeuo_023_final.xml      # → 236/223/0/13(基线 233/220 +3)
unity test unity --mode EditMode \
  --output unity/Logs/editmode_full_20261009_form03.xml  # → 3013/2966/0/46/1inc(基线 3010/2963 +3)
unity test unity --mode PlayMode \
  --output unity/Logs/playmode_full_20261009_form03.xml  # → 98/97/0/1(逐数 = 基线)
```

## 四、判别力变异(6 发全中 · 逐发 python 反向恢复零残留)

| 发 | 注入 | 期望 | 实测 |
|---|---|---|---|
| MUT-1 | `.skip-entry` 背景换内联 hex `#e8dcc4` | 023-1 | ✅ 023-1 红(+C4 全库门合理涟漪) |
| MUT-2 | Seal USS 注入 `content: "3/5"` | 023-2 | ✅ 023-2 红(+C4 涟漪) |
| MUT-3 | 加 `background-image: url(guid:…)` | 023-3 | ✅ 恰 023-3 红(单槽铁律+交集) |
| MUT-4 | 加 `border-bottom-width: var(…)` | 023-3 | ✅ 恰 023-3 红(属性交集) |
| MUT-5 | **删 `background-color` 行(Q1 修复判别力)** | 023-1 | ✅ 恰 023-1 红(修复前此注入假绿) |
| MUT-6 | **`color: black` 具名色(Q1 修复判别力)** | 023-1 | ✅ 恰 023-1 红(修复前 C4 不拦、此断言不查,假绿) |

恢复终态:两 USS `grep MUT` = 0;`SkeuoPaper.uss` 净态 +20 行(`.skip-entry` 块);`SkeuoSeal.uss` 零 diff。

## 五、复跑实数(终态 · 修复后净态,不沿用变异前日志)

- 过滤 **236 / 223 passed / 0 failed / 13 skipped**(`editmode_skeuo_023_final.xml`)
- 全量 EditMode **3013 / 2966 / 0 红 / 46 跳 / 1 inc**(`editmode_full_20261009_form03.xml`)—— 基线 3010/2963 +3,逐项一致
- 全量 PlayMode **98 / 97 / 0 红 / 1 跳**(`playmode_full_20261009_form03.xml`)—— 逐数 = 基线
- 变异 6 发恰红 + 零残留(上表)

## 六、判定链

原判定(两代理 FIX-THEN-APPROVE,合计 2 BLOCKING + 11 ADVISORY 去重 13 项)→ 同批修复全落 →
补 2 发修复判别力变异恰红 → 净态过滤 + 全量双套零回归 → **转 APPROVE**。
story-023 DoD「双代理评审恰一轮 → 修复 → 复跑绿」就此闭环。
