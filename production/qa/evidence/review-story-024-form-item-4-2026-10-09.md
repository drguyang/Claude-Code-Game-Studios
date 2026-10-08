# 评审原件 — story-024 M2 形态件④(一条真实状态反馈通道)· 2026-10-09

> **对象**: `production/epics/skeuomorphic-ui/story-024-m2-form-item-4-status-feedback-channel.md`
> **轮次**: 双代理评审**恰一轮**(用户指令)
> **判定**: 代码面 **FIX-THEN-APPROVE**(1 BLOCKING + 2 ADVISORY)· 测试面 **APPROVE**(2 ADVISORY 判非阻塞)
> → 修复全落 → 补 2 发修复判别力变异恰红 → 复跑绿 → 转 APPROVE
> **交付件**: `SignChannelBinder.cs`(通道分发纯函数)· `SkeuoPaper.uss` `.channel-reading` /
> `.channel-reading-negative`(+21 行头注)· `sign_channel_binder_test.cs` 三条 AC · story 文档

## 一、原判定(两代理逐条)

### 代码面(lead-programmer): FIX-THEN-APPROVE — 1 BLOCKING + 2 ADVISORY

| # | 级 | 位置 | 问题 |
|---|---|---|---|
| B1 | BLOCKING | `SignChannelBinder.cs:70-78`(根因)· 暴露于 `test:159-162` | AC-024-3「34 词条全落合法通道」实由 **switch** 保证,与 `Bind` 的非法枚举守卫**结构不等价** —— 真表行必落预建桶,守卫**不可达**;非法枚举 fail-loud 仅由夹具 `(SignChannel)99` 触发。AC 文字与断言机制不等价(023 Q1 同型) |
| A1 | ADVISORY | `SignChannelBinder.cs:96-99` · `test:366-370` | F-8.2 `:1003`「向下回退」与实现「向数组尾」等价依赖未写明约定(数组序 = slot 序,出处 `DiagnosisSignTable.cs:109-110`) |
| A2 | ADVISORY | `test:240` / `:273-274` | badge 扫描与块唯一性正则字符类缺 `/`(命名面带斜杠变体漏网,风险低) |

**核过零问题**: ① 纯函数零自定义映射 ✅ ② F-8.2 回退等价成立 ✅ ③ 非法输入四分支 fail-loud ✅
④ USS 两态类全 var(fg/faded 同族不同档,`ink-faded` 在 `SkeuoThemeVariables.uss:40` 实定义)/
零内联/零数字/零 content/零新图/头注锚 :329+空行+归 8/焦点单槽属性零交集 ✅
⑤ AC 文字与断言除 B1 外全等价 ✅ ⑥ 边界裁定五条全过(AddChannel 未改/真表只读/零徽章/
零未查态类/映射归 8)✅ ⑦ 与 021/022/023 无冲突 ✅

### 测试面(qa-lead): APPROVE — 2 ADVISORY

| # | 级 | 位置 | 问题 |
|---|---|---|---|
| A1 | ADVISORY | `test:64,359` | `ChannelReading.Polarity` 透传**零断言**(ac1 夹具默认 Positive 无阴性;ac3 解析 polarity 进 `SignLexemeRow` 却从不断言 `hit.Polarity`)⇒ 突变「硬编码 Positive」可逃逸 |
| A2 | ADVISORY | `test:195-197` | `font-family`/`font-size` 仅验 `Contains("var(--skeuo-")`,未钉精确 var 名(`color` 已钉) |
| A3 | ADVISORY | `test:53` | `BlockOf` 用 `[^}]*`,遇嵌套花括号截断(当前无嵌套,脆弱面登记) |
| A4 | ADVISORY | `test:313` | 真表数组抽取 `"([^"]*)"\|null` 遇转义引号切分错(当前无此类词,失败模式为红非假绿) |

**核过零问题**: 断言精度达标(023 Q1 修复口径完整落地)· 头注锚紧邻(023 L2 口径落地)·
PropsOf 行首锚消幻影(023 L4)· 真表端到端**独立复算对拍**不可绕过 · 6 发变异全中且归因正确 ·
命名/AAA/确定性/门式声明合规。**变异覆盖缺口**登记: Polarity 透传 ❌ · 空串档回退(IsNullOrEmpty
vs `!= null`)❌。

## 二、修复落点(全 5 项 · 同批)

| # | 修复 | 落点 |
|---|---|---|
| B1 | 增**真表 channel 原始值集恰 = 闭集六值**(`CollectionAssert.AreEquivalent`,独立扫原文)+ 各 slot **`Bind` 产物键集恰 = 六枚举**(看产物,与原文互为独立证据) | `test_ac024_3` Assert ①′ + Assert ② |
| A1(码) | 头注补「向数组尾 ≡ 向高 slot」的**等价性出处**(`DiagnosisSignTable.cs:109-110` 数组序即 slot 序) | `SignChannelBinder.cs` ② 头注 |
| A2(码) | 正则字符类 `[a-z0-9-]` → `[a-z0-9/-]`(两处) | `test` badge 扫描 + 块唯一性 |
| A1(测) | **Polarity 透传断言**:ac1 补阴性夹具双值;ac3 期望表加 `expectedPolarity` 并逐行断言 + 真表极性闭集二值均有实况 | `test_ac024_1` Assert ⑨ · `test_ac024_3` Assert ② |
| 缺口(测) | **空串档回退断言**:`[ "", "空串后词" ]` slot0 → 「空串后词」(IsNullOrEmpty 语义) | `test_ac024_1` Assert ④′ |

**未采纳**: 测试面 A2(钉 font var 精确名 —— AC 只要求「值全走 theme」,未点名 var 名)·
A3(BlockOf 平衡括号 —— 当前 USS 无嵌套,登记为脆弱面)· A4(转义引号 —— 失败模式为红非假绿)。

## 三、验证命令(可证伪)

```bash
unity test unity --mode EditMode --filter "SkeuomorphicUI" \
  --output unity/Logs/editmode_skeuo_024_final2.xml        # → 239/226/0/13(基线 236/223 +3)
unity test unity --mode EditMode \
  --output unity/Logs/editmode_full_20261009_form04.xml    # → 3016/2969/0/46/1inc(基线 3013/2966 +3)
unity test unity --mode PlayMode \
  --output unity/Logs/playmode_full_20261009_form04.xml    # → 98/97/0/1(逐数 = 基线)
```

## 四、判别力变异(8 发全中 · 逐发 python 反向恢复零残留)

| 发 | 注入 | 期望 | 实测 |
|---|---|---|---|
| MUT-1 | 回退循环 `i < words.Length` → `i <= slot`(截断回退) | ac1+ac3 | ✅ ac1+ac3 红 |
| MUT-2 | 分流全落 `FaceColor` | ac1+ac3 | ✅ ac1+ac3 红 |
| MUT-3 | 删全通道预建空桶 | ac1+ac3 | ✅ ac1+ac3 红 |
| MUT-4 | `.channel-reading-negative` 色换 `ink-fg`(降档丢失) | ac2 | ✅ 恰 ac2 红(净态复验) |
| MUT-5 | 注入 `.no-data` 徽章类 | ac2 | ✅ 恰 ac2 红(:329 门) |
| MUT-6 | `Order` 固定 0(保序丢失) | ac1+ac3 | ✅ ac1+ac3 红 |
| MUT-7 | **Polarity 硬编码 Positive(A1 修复判别力)** | ac1+ac3 | ✅ ac1+ac3 红(修复前此注入逃逸) |
| MUT-8 | **空串按 `!= null` 处理(缺口修复判别力)** | ac1 | ✅ 恰 ac1 红(修复前此注入逃逸) |

恢复终态:`SignChannelBinder.cs` / `SkeuoPaper.uss` 两文件 `grep MUT` = 0;
`SignChannelBinder.cs` 五锚点(预建循环/回退/分流/Order/order++)各 1;USS `.channel-reading-negative`
色 = `var(--skeuo-ink-faded)`。
⚠️ 过程记录:MUT-3/MUT-4 首轮恢复脚本锚点未匹配(MUT-3 恢复锚被自己的插入物破坏;MUT-4 变异串
在全文件出现 3 次,断言过严),均已定点修正并重验净态 —— 上表 MUT-4 为**净态复验**结果。

## 五、复跑实数(终态 · 修复后净态,不沿用变异前日志)

- 过滤 **239 / 226 passed / 0 failed / 13 skipped**(`editmode_skeuo_024_final2.xml`)—— 基线 236/223 +3
- 全量 EditMode **3016 / 2969 / 0 红 / 46 跳 / 1 inc**(`editmode_full_20261009_form04.xml`)—— 基线 3013/2966 +3,逐项一致
- 全量 PlayMode **98 / 97 / 0 红 / 1 跳**(`playmode_full_20261009_form04.xml`)—— 逐数 = 基线
- 变异 8 发恰红 + 零残留(上表)

## 六、判定链

原判定(代码面 FIX-THEN-APPROVE 1B+2A · 测试面 APPROVE 2A ⇒ 去重 5 项)→ 同批修复全落 →
补 2 发修复判别力变异(MUT-7 极性 / MUT-8 空串)恰红 → 净态过滤 + 全量双套零回归 → **转 APPROVE**。
story-024 DoD「双代理评审恰一轮 → 修复 → 复跑绿」就此闭环。
