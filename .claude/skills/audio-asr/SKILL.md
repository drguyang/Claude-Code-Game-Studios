---
name: audio-asr
description: "QC and verification, NOT generation: transcribe generated speech with the StepFun ASR API and compare it against the intended text to catch wrong words, wrong language, dropped syllables and homophone misreads. Emphatically does NOT replace the human listening test — AC-44-03 [L] is a human judgement, and this skill must refuse to emit any verdict for non-lexical cues (sound effects, music, ambience, coughs, breath layers)."
argument-hint: "<音频文件> [--expect <期望文本>] [--model stepaudio-2.5-asr]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit, Bash, AskUserQuestion
model: sonnet
---

**这是质检技能,不是生成技能。**

它把已生成的语音转写成文字,与**预期文本**比对,抓「念错了 / 漏字了 / 念成别的语言了」。
它**不能**听声音好不好 —— 那是人耳的事。

> **本技能里最要紧的一句话:**
> 对**非语言**素材(音效、音乐、环境音、咳嗽、呼吸层)转写结果必然是空的。
> 把「空转写」读成「没问题 ⇒ PASS」是本项目最典型的假绿。
> 遇到这类 cue,**本技能不出任何判定**。

---

## Phase 0: Setup

密钥读自 `.claude/stepfun.json`(本地密钥件,mode 600)。**该文件不得被 git 跟踪** ——
工具每次运行都查这一条,查到即拒跑;未被 `.gitignore` 覆盖则告警。**不要**回显 key。

```bash
python3 tools/audio/stepfun_audio.py account
```

---

## Phase 1: 能力边界 —— 能查什么

接口 `POST /v1/audio/transcriptions`(multipart),`model: stepaudio-2.5-asr`,
`response_format=json`。支持的输入格式:`mp3 / pcm / ogg / wav`(≤100 MB)。

**可以查(仅词法域):**

| 用途 | 做法 |
|---|---|
| TTS 回读闭环 | 合成 → 转写 → 与预期文本比对,抓错读/漏字/多音字 |
| 台词与字幕文本是否一致 | VO 素材 ↔ 该 cue 的 `subtitle_text` 的**词法**半边 |
| 明显的时长异常 | 结合 `verify` 的时长看「每字秒数」是否离谱 |

**不能查,也不要假装能查:**

- ❌ **`AC-44-03 [L]`(人耳听测)。** **永不以本技能代表它通过。**
- ❌ **音色/音质** —— 闷不闷、有没有混响尾、像不像病人。
- ❌ **`AC-44-08` 医学真实性** —— 细湿啰音对不对,文本里看不出来。
- ❌ **循环无缝。**
- ❌ **`AC-44-09`(是否在报状态)。** 这是**语义**属性;一段完美播音如果在播报
  「脉象越界了」,它在所有机械检查上都是绿的,而它违规。
- ❌ **`AC-44-01` 可滤波性**(中性、无自带音高/环境)。

---

## Phase 2: 非语言素材的硬拒绝规则

**先判断这个 cue 是不是语言的。**

| cue | 判定 |
|---|---|
| `vo_patient_cough_damp_*`(12 支咳嗽) | **非语言 —— 拒绝出判定** |
| `sfx_breath_base_loop_small.wav` · `sfx_breath_adventitious_fine.wav` | **非语言 —— 拒绝** |
| `sfx_*` / `ui_*` / `amb_*` / `music_*` | **非语言 —— 拒绝** |
| 患者台词、旁白(有词的 VO) | 语言 —— 可做词法回读 |

对非语言素材,本技能**只报告一件事**:「该素材不是语音,我的判定不适用」,
并把话交回给人耳与对应技能。**不得**输出 PASS。

理由不是形式主义:一个 ASR 对一段咳嗽可能返回空串,也可能**幻觉**出一段
听起来很合理的文本。两者都不能推出「这段咳嗽是对的」——
但两者都很容易被读成「跑过了,没问题」。

---

## Phase 3: 转写

```bash
python3 tools/audio/stepfun_audio.py asr --file <音频> --expect "<期望文本>"
```

带 `--expect` 时会做词法比对,并在**空转写**时明确返回非零退出码 + 一句提示:
「非语言素材本就该是空的 —— 不得据此判 PASS」。

---

## Phase 4: 报告口径

输出必须**区分**「测量」与「判定」:

```
转写(测量,事实):    大夫我这胸口闷了有半个月了
期望(输入):          大夫我这胸口闷了有半个月了
词法比对(机械):      一致
```

然后**逐条列出本技能不能证明什么**(照抄 Phase 1 的 ❌ 清单),并明确写:

> 以上不构成任何验收结论。`AC-44-03 [L]` 是人耳听测。

若结论会被援引去关闭某条 BLOCKING 级 AC,**不要**由本技能写报告原件 ——
按 `.claude/docs/coding-standards.md` §Review Evidence Standards,报告原件须
**人**做评审并落 `production/qa/evidence/review-<对象>-<YYYY-MM-DD>.md`。
本技能在此**交接**,不代签。

---

## Phase 5: 留痕(需要批准)

若要把转写结果落盘:

```
我可以把这次转写结果写入 production/qa/evidence/<文件名> 吗?
```

**规范问法**(本项目协议原文):**"May I write this to `production/qa/evidence/<文件名>`?"**

得到同意再写。**只写测量值**(转写文本、比对结果、模型、时间),
**不写**「PASS / 已验证 / 合格」这类判定 —— 那不是我给的。

---

## 交接

- 语音生成 → `/audio-tts`
- 非语言素材(本技能拒绝的那一类)→ `/audio-sfx` / `/audio-music`
- 需要人耳验收或收口 → `/story-done`(评审报告原件由人落)
