// sprint-08 S1.1 件 F2/G · 脉案命令路由(42 UI 事件 → 写者 · 2026-10-12)
//
// 权威来源:
//   casebook-39.md §5.2 / §7(OQ-CB-5 = 甲「循环落笔」:病名栏重复 Submit ⇒
//     空 → 疑似 → 确定 → 擦除回空;置信度**无独立焦点位**,档位随落笔 / 改判事件载荷写入)
//   casebook-39.md §7 表「病名落笔」行 → `JudgmentRecorded`;「改判(重写)」行 → `JudgmentRevised`
//   case-system.md 规则四(判断记录:读数 + 落笔 + 改写史;存新值,禁差分)
//   case-system.md:241(空判断合法:lexicon_id = 0 且 freehand_text = "" 表示空栏)
//   ADR-013 §9 C3(42 只渲染不持状态 —— 本类 = 装配层,把 42 的 Submit 意图接进写者)
//   ADR-011 Amendment C(2026-10-12:Submit 由 42 接焦点桥后转发,不设备级直读)
//   diagnosis-system S-8.3(三态 空 / 疑似 / 确定;空 = 合法终态)
//
// ⚠️ 本类住 **Gameplay.Boot**(装配层):实现 42 侧契约 `ICasebookCommandSink`,
//   把「病名栏 Submit」翻译成 `JudgmentWriter` 的写调用。**不造 UI、不持 sim 态**
//   —— 只持**档位循环态**(PendingConfidence)与当前绑定病人(会话对象)。
//
// ⚠️ **档位循环态为何住此(而非 42)**:用户裁定 2026-10-12 取甲案 ——
//   档位 = 事件载荷值(`judgment.confidence`)⇒ 属「游戏状态」;42 只渲染不持状态
//   (AC-42-D3 反射面),故档位住装配层,42 经 `ICasebookDtoSource.PendingConfidence`
//   **回读**(与 InkAgeTicks / SeenLexiconIds 同构:装配层算、42 只读)。
//
// ⚠️ **循环 = 空 → 疑似 → 确定 → 擦除**(S-8.3 恰三态,零第四档):
//   · 空 → Submit(lexiconId)     ⇒ JudgmentRecorded(confidence = 疑似 = 1)
//   · 疑似 → Submit(同 lexiconId) ⇒ JudgmentRevised(confidence = 确定 = 2)
//   · 确定 → Submit(同 lexiconId) ⇒ JudgmentRevised(lexicon_id = 0, confidence = 0)= 擦除
//   · 擦除后 = 空 ⇒ 回到首步。
//   擦除是**写一条空判断**(case-system.md:241),**不是删事件** —— 全序最后一条胜。
//
// ⚠️ **换词重来**:空态下指认**另一条**词 ⇒ 从空重新起笔(Recorded / 疑似)。
//   已落笔态下指认另一条词 ⇒ 视为**改判**(Revised),档位回到疑似(新词的第一笔)。

using System;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;   // JudgmentState(档位值域)
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;          // ICasebookCommandSink
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 脉案命令路由 —— 42 病名栏 Submit 的接收点(OQ-CB-5 甲循环落笔)。
    /// <para>实现 <see cref="ICasebookCommandSink"/>,把指认 / 循环落笔折成
    /// <see cref="JudgmentWriter"/> 的 Recorded / Revised 写调用。</para>
    /// </summary>
    public sealed class CasebookCommandRouter : ICasebookCommandSink
    {
        private readonly JudgmentWriter _judgment;
        private readonly Func<long> _currentTick;

        private PatientId _boundPatient = PatientId.None;
        private int _currentLexiconId;
        private JudgmentState _pending = JudgmentState.Empty;

        /// <param name="judgment">落笔 / 改判写者(37 的 JudgmentRecorded / Revised 生产者)。</param>
        /// <param name="currentTick">当前逻辑 tick 读数(写事件的 SimEvent.Tick —— 墨龄基准)。</param>
        public CasebookCommandRouter(JudgmentWriter judgment, Func<long> currentTick)
        {
            _judgment = judgment ?? throw new ArgumentNullException(nameof(judgment));
            _currentTick = currentTick ?? throw new ArgumentNullException(nameof(currentTick));
        }

        /// <summary>当前绑定病人(脉案会话对象;由查体驱动设置,与 CasebookSource.ChangePatient 同步)。</summary>
        public PatientId BoundPatient => _boundPatient;

        /// <summary>绑定病人(查体 / 切页时由装配层设置)。</summary>
        public void BindPatient(PatientId patient)
        {
            if (patient != _boundPatient)
            {
                // 换病人 ⇒ 档位与当前词复位(上一病人的落笔档不串到新病人)
                _boundPatient = patient;
                _currentLexiconId = 0;
                _pending = JudgmentState.Empty;
            }
        }

        /// <summary>待落笔置信度档(0 = 空 / 1 = 疑似 / 2 = 确定)—— 供 42 经 DTO 回读。</summary>
        public byte PendingConfidence => (byte)_pending;

        /// <summary>当前已指认词条 id(0 = 无;观测面 / 测试接缝)。</summary>
        public int CurrentLexiconId => _currentLexiconId;

        /// <inheritdoc/>
        public void SubmitDiseaseName(int lexiconId)
        {
            if (lexiconId <= 0)
                throw new ArgumentOutOfRangeException(nameof(lexiconId), lexiconId,
                    "指认词条 id 须 ≥ 1(0 = 空栏哨兵,不可指认)");
            AssertPatientBound();

            if (lexiconId != _currentLexiconId)
            {
                // 指认新词:空态 ⇒ 首落笔(Recorded / 疑似);已落笔 ⇒ 改判(Revised / 疑似)
                bool firstEver = _pending == JudgmentState.Empty;
                _currentLexiconId = lexiconId;
                _pending = JudgmentState.Suspected;

                if (firstEver)
                    _judgment.TryRecord(CurrentTick, _boundPatient, lexiconId,
                                        (byte)JudgmentState.Suspected);
                else
                    _judgment.TryRevise(CurrentTick, _boundPatient, lexiconId,
                                        (byte)JudgmentState.Suspected);
                return;
            }

            // 同词再 Submit ⇒ 档位循环(S-8.3 恰三态:空 → 疑似 → 确定 → 擦除)
            SubmitConfidence();
        }

        /// <inheritdoc/>
        public void SubmitConfidence()
        {
            AssertPatientBound();
            if (_currentLexiconId <= 0)
                throw new InvalidOperationException(
                    "病名栏循环落笔前须先指认词条(SubmitDiseaseName)—— 无词无从循环档位");

            switch (_pending)
            {
                case JudgmentState.Empty:
                    // 空 ⇒ 疑似(首落;理论上前一步已 Recorded,此处兜底为改判)
                    _pending = JudgmentState.Suspected;
                    _judgment.TryRevise(CurrentTick, _boundPatient, _currentLexiconId,
                                        (byte)JudgmentState.Suspected);
                    break;

                case JudgmentState.Suspected:
                    // 疑似 ⇒ 确定(改判)
                    _pending = JudgmentState.Confirmed;
                    _judgment.TryRevise(CurrentTick, _boundPatient, _currentLexiconId,
                                        (byte)JudgmentState.Confirmed);
                    break;

                case JudgmentState.Confirmed:
                    // 确定 ⇒ 擦除回空(case-system.md:241:lexicon_id = 0 + confidence = 0)
                    _pending = JudgmentState.Empty;
                    _judgment.TryRevise(CurrentTick, _boundPatient, lexiconId: 0,
                                        confidence: (byte)JudgmentState.Empty);
                    _currentLexiconId = 0;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(_pending), _pending,
                        "[CasebookCommandRouter] 档位超出 S-8.3 三态闭集(零第四档)");
            }
        }

        private long CurrentTick => _currentTick();

        private void AssertPatientBound()
        {
            if (_boundPatient.Value < 0)
                throw new InvalidOperationException(
                    "脉案命令路由未绑定病人 —— 须先 BindPatient(查体 / 切页时),无对象无从落笔");
        }
    }
}
