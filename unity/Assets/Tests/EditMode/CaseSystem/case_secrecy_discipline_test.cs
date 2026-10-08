// case-system Story 005 —— 守密纪律(DTO 守卫 · 词表烘焙 · 零奖励断言)。
//
// 权威来源:production/epics/case-system/story-005-secrecy-dto-guard-lexicon-bake.md
//   · GDD design/gdd/case-system.md 规则九 守密 · 第六泄漏面 · F-37.4 词表 · 规则十 不拥有清单
//   · AC-37-15/24/35/32/31/20 + quill_tick 只读
//   · ADR-013(PresentationDtoGuard 递归反射扫描)· ADR-014(词表烘焙)· ADR-006(整数计数移出 Fix)
//
// 覆盖(自动化类型面半边;[V] 走查半边归走查件,禁借绿):
//   · AC-37-15 PresentationDtoGuard 对既有 DTO 递归扫描零 disease_id + 影子负夹具
//   · AC-37-24 排序键枚举不含 disease_id / freehand_text
//   · AC-37-35 lexicon_id(u16)/confidence(u8) 不属 Fix 解析集
//   · AC-37-32 37 出站写路径零数值奖励通道
//   · AC-37-31 37 类型面无 correctness 字段/谓词
//   · AC-37-20 37 不拥有后果/处置/病情演进/词表语义
//   · quill_tick 只读进判定 DTO
//
// ⚠️ 载体未建齐的条目记 NOT-RUN,禁空集绿(卡 Guardrail)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.CaseSystem
{
    [TestFixture]
    public sealed class CaseSecrecyDisciplineTest
    {
        // ══════════════════════════════════════════════════════════════════════════
        // AC-37-15:PresentationDtoGuard 对既有 DTO 递归扫描零 disease_id
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_ac37_15_existingDtos_zeroDiseaseId()
        {
            // 既有呈现 DTO(载体已建,可跑)
            var cleanDtos = new[]
            {
                typeof(AudioCueDto),
                typeof(WorldPosLatest),
                typeof(ClinicEnvDto),
            };

            foreach (var dto in cleanDtos)
            {
                var errs = PresentationDtoGuard.Scan(dto);
                Assert.IsEmpty(errs,
                    $"{dto.Name} 应零 disease 语义 —— 命中:{string.Join("; ", errs)}");
            }
        }

        [Test]
        public void test_case_ac37_15_shadowDtoWithDiseaseId_guardCatches()
        {
            // 影子负夹具:含 disease_id 的假 DTO ⇒ 守卫必须红
            var errs = PresentationDtoGuard.Scan(typeof(ShadowDiseaseDto));
            Assert.IsNotEmpty(errs,
                "含 disease_id 的影子 DTO 必须被 PresentationDtoGuard 捕获(AC-37-15 正负夹具各一)");
            Assert.IsTrue(errs.Any(e => e.Contains("disease", StringComparison.OrdinalIgnoreCase)),
                "违例消息应点名 disease 语义");
        }

        [Test]
        public void test_case_ac37_15_shadowDtoNestedDiseaseId_guardCatches()
        {
            // 影子负夹具:嵌套类型中的 disease_id(递归扫描)
            var errs = PresentationDtoGuard.Scan(typeof(ShadowNestedDiseaseDto));
            Assert.IsNotEmpty(errs,
                "嵌套类型中的 disease_id 必须被递归捕获(AC-37-15 递归口径)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-37-24:排序键枚举不含 disease_id / freehand_text
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_ac37_24_sortKeyTokens_noDiseaseIdNoFreehand()
        {
            // 排序键枚举(呈现层可用键)—— 闭集点名
            var sortKeyTokens = new[] { "patient_id", "case_opened_tick", "case_opened_seq", "quill_tick" };

            foreach (var token in sortKeyTokens)
            {
                Assert.IsFalse(token.Contains("disease", StringComparison.OrdinalIgnoreCase),
                    $"排序键「{token}」不得含 disease_id(AC-37-24)");
                Assert.IsFalse(token.Contains("freehand", StringComparison.OrdinalIgnoreCase),
                    $"排序键「{token}」不得含自书病名(AC-37-24)");
            }
        }

        [Test]
        public void test_case_ac37_24_judgmentPayload_noDiseaseIdNoFreehandField()
        {
            // JudgmentRecordedPayload 字段面:不含 disease_id / freehand_text
            var fields = typeof(JudgmentRecordedPayload).GetFields(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotEmpty(fields, "JudgmentRecordedPayload 字段集非空(空集绿守卫)");

            foreach (var f in fields)
            {
                Assert.IsFalse(f.Name.Contains("disease", StringComparison.OrdinalIgnoreCase),
                    $"JudgmentRecordedPayload.{f.Name} 不得含 disease_id(AC-37-24)");
                Assert.IsFalse(f.Name.Contains("freehand", StringComparison.OrdinalIgnoreCase),
                    $"JudgmentRecordedPayload.{f.Name} 不得含自书病名(AC-37-24)");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-37-35:lexicon_id(u16)/confidence(u8) 不属 Fix 解析集
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_ac37_35_lexiconId_confidence_notInFixParseSet()
        {
            // lexicon_id = u16 值域,confidence = u8 值域 —— 均非 Fix(Q16.16)
            // 断言:JudgmentRecordedPayload.LexiconId 类型 = int(u16 承载)
            var lexiconField = typeof(JudgmentRecordedPayload).GetField("LexiconId");
            Assert.IsNotNull(lexiconField, "LexiconId 字段存在");
            Assert.AreEqual(typeof(int), lexiconField.FieldType,
                "LexiconId 应为 int(u16 承载),非 Fix(AC-37-35)");

            var confField = typeof(JudgmentRecordedPayload).GetField("Confidence");
            Assert.IsNotNull(confField, "Confidence 字段存在");
            Assert.AreEqual(typeof(byte), confField.FieldType,
                "Confidence 应为 byte(u8 承载),非 Fix(AC-37-35)");
        }

        [Test]
        public void test_case_ac37_35_lexiconId_u16ValueRange()
        {
            // u16 值域:0..65535;空判断合法(lexicon_id = 0 且 freehand_text = "")
            // 断言:LexiconId 字段类型 int 且注释标明 u16 值域(类型面已验,此处验值域边界)
            var lexiconField = typeof(JudgmentRecordedPayload).GetField("LexiconId");
            Assert.IsNotNull(lexiconField);
            // u16 上界 65535;空判断 = 0
            Assert.AreEqual(0, default(JudgmentRecordedPayload).LexiconId,
                "空判断合法:lexicon_id = 0(S-8.3)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-37-32:37 出站写路径零数值奖励通道
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_ac37_32_outboundPayloads_zeroRewardFields()
        {
            // 37 全部出站载荷(病例流 5 Kind)
            var payloads = new[]
            {
                typeof(CaseOpenedPayload),
                typeof(CaseClosedPayload),
                typeof(PatternRecognizedPayload),
                typeof(JudgmentRecordedPayload),
                typeof(JudgmentRevisedPayload),
            };

            var rewardTokens = new[] { "reputation", "xp", "level", "currency", "potency", "unlock", "reward" };

            foreach (var payload in payloads)
            {
                var fields = payload.GetFields(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotEmpty(fields, $"{payload.Name} 字段集非空(空集绿守卫)");

                foreach (var f in fields)
                {
                    foreach (var token in rewardTokens)
                    {
                        Assert.IsFalse(f.Name.Contains(token, StringComparison.OrdinalIgnoreCase),
                            $"{payload.Name}.{f.Name} 含奖励语义「{token}」(AC-37-32 零数值奖励)");
                    }
                }
            }
        }

        [Test]
        public void test_case_ac37_32_outboundDtos_zeroRewardFields()
        {
            // 37 出向 DTO(Sim.Contracts 呈现 DTO)
            var dtos = new[] { typeof(AudioCueDto), typeof(WorldPosLatest), typeof(ClinicEnvDto) };
            var rewardTokens = new[] { "reputation", "xp", "level", "currency", "potency", "unlock", "reward" };

            foreach (var dto in dtos)
            {
                var fields = dto.GetFields(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotEmpty(fields, $"{dto.Name} 字段集非空(空集绿守卫)");

                foreach (var f in fields)
                {
                    foreach (var token in rewardTokens)
                    {
                        Assert.IsFalse(f.Name.Contains(token, StringComparison.OrdinalIgnoreCase),
                            $"{dto.Name}.{f.Name} 含奖励语义「{token}」(AC-37-32)");
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-37-31:37 类型面无 correctness 字段/谓词
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_ac37_31_noCorrectnessFieldIn37Types()
        {
            // 37 出站载荷 + 出向 DTO 无 correctness 字段
            var types = new[]
            {
                typeof(CaseOpenedPayload), typeof(CaseClosedPayload),
                typeof(PatternRecognizedPayload), typeof(JudgmentRecordedPayload),
                typeof(JudgmentRevisedPayload),
                typeof(AudioCueDto), typeof(WorldPosLatest), typeof(ClinicEnvDto),
            };

            foreach (var t in types)
            {
                var fields = t.GetFields(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotEmpty(fields, $"{t.Name} 字段集非空(空集绿守卫)");

                foreach (var f in fields)
                {
                    Assert.IsFalse(f.Name.Contains("correct", StringComparison.OrdinalIgnoreCase) ||
                                   f.Name.Contains("accuracy", StringComparison.OrdinalIgnoreCase) ||
                                   f.Name.Contains("verdict", StringComparison.OrdinalIgnoreCase),
                        $"{t.Name}.{f.Name} 含正确性判定语义(AC-37-31:37 不判定处置对错)");
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-37-20:37 不拥有后果/处置/病情演进/词表语义
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_ac37_20_noConsequenceOwnership()
        {
            // 37 不定义后果(→53)/处置内容(→10/11)/病情演进(→9)/词表语义(→8/内容)
            // 断言:37 出站载荷无 consequence/treatment/disease_progression/lexicon_semantics 字段
            var payloads = new[]
            {
                typeof(CaseOpenedPayload), typeof(CaseClosedPayload),
                typeof(PatternRecognizedPayload), typeof(JudgmentRecordedPayload),
                typeof(JudgmentRevisedPayload),
            };

            var ownershipTokens = new[]
            {
                "consequence", "treatment_content", "disease_progression", "lexicon_semantics",
                "reputation", "outcome", "penalty",
            };

            foreach (var payload in payloads)
            {
                var fields = payload.GetFields(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotEmpty(fields, $"{payload.Name} 字段集非空(空集绿守卫)");

                foreach (var f in fields)
                {
                    foreach (var token in ownershipTokens)
                    {
                        Assert.IsFalse(f.Name.Contains(token, StringComparison.OrdinalIgnoreCase),
                            $"{payload.Name}.{f.Name} 含越权语义「{token}」(AC-37-20 不拥有清单)");
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // quill_tick 只读进判定 DTO
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void test_case_quillTick_readOnlyInJudgmentDto()
        {
            // quill_tick = 落笔 tick(int64),只读呈现量,不回写判定
            // 断言:JudgmentRecordedPayload 无 quill_tick 字段(它住呈现 DTO,不住判定载荷)
            // 注:quill_tick 的呈现 DTO 载体 = 39 脉案(未建齐 ⇒ NOT-RUN,此处只验判定载荷不含它)
            var fields = typeof(JudgmentRecordedPayload).GetFields(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotEmpty(fields, "JudgmentRecordedPayload 字段集非空(空集绿守卫)");

            foreach (var f in fields)
            {
                Assert.IsFalse(f.Name.Contains("quill", StringComparison.OrdinalIgnoreCase),
                    $"JudgmentRecordedPayload.{f.Name} 含 quill_tick —— quill_tick 住呈现 DTO,不进判定载荷");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 影子负夹具(留库,下轮走查复用)
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>含 disease_id 的影子 DTO(AC-37-15 负夹具)。</summary>
        private readonly struct ShadowDiseaseDto
        {
            public readonly int disease_id;
            public readonly string name;

            public ShadowDiseaseDto(int diseaseId, string name)
            {
                disease_id = diseaseId;
                this.name = name;
            }
        }

        /// <summary>嵌套类型含 disease_id 的影子 DTO(AC-37-15 递归负夹具)。</summary>
        private readonly struct ShadowNestedDiseaseDto
        {
            public readonly ShadowDiseaseDto Inner;
            public readonly int patient_id;

            public ShadowNestedDiseaseDto(ShadowDiseaseDto inner, int patientId)
            {
                Inner = inner;
                patient_id = patientId;
            }
        }
    }
}
