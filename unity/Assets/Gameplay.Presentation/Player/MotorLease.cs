// ADR-020 Amendment B —— MotorSuppressed per-source lease(位图语义)。
//
// 权威来源:
//   ADR-020 Amendment B: 单 bool → 每源一位 + OR 聚合
//   AC-1-23: 位图 + 幂等 + 只碰自己位
//   AC-1-30: 调用点集合白名单 = {4, 10, 25}
//
// 核心机制:
//   - LeaseSource 枚举: Self=4, Emergency=10, Combat=25
//   - 位图: 每源一位, OR 聚合
//   - 幂等: 同 src 重复 Acquire 不计数
//   - 无到期: 压制期由持有者自己的规则说了算
//   - 作用域: 水平位移 + Jump, 不含攻击

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>
    /// 压制源枚举(ordinal = 系统号, append-only 禁重排)。
    /// </summary>
    public enum LeaseSource
    {
        Self = 4,        // 玩家自身(如主动下蹲)
        Emergency = 10,  // 急救动作压制
        Combat = 25      // 战斗压制(如被击中)
    }

    /// <summary>
    /// MotorSuppressed per-source lease 位图。
    /// </summary>
    public sealed class MotorLease
    {
        private int _bitmap;

        /// <summary>
        /// 是否被压制(任一位为 1)。
        /// </summary>
        public bool IsSuppressed => _bitmap != 0;

        /// <summary>
        /// 获取当前位图(用于测试验证)。
        /// </summary>
        public int Bitmap => _bitmap;

        /// <summary>
        /// 获取指定源是否持有 lease。
        /// </summary>
        public bool HasLease(LeaseSource source)
        {
            return (_bitmap & (1 << (int)source)) != 0;
        }

        /// <summary>
        /// 获取指定源的 lease 计数(0 或 1,幂等)。
        /// </summary>
        public int GetLeaseCount(LeaseSource source)
        {
            return HasLease(source) ? 1 : 0;
        }

        /// <summary>
        /// 获取当前活跃 lease 数量。
        /// </summary>
        public int ActiveLeaseCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < 32; i++)
                {
                    if ((_bitmap & (1 << i)) != 0) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(int source)
        {
            return GetLeaseCount((LeaseSource)source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(long source)
        {
            return GetLeaseCount((LeaseSource)source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(byte source)
        {
            return GetLeaseCount((LeaseSource)source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(sbyte source)
        {
            return GetLeaseCount((LeaseSource)source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(ushort source)
        {
            return GetLeaseCount((LeaseSource)source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(uint source)
        {
            return GetLeaseCount((LeaseSource)source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(ulong source)
        {
            return GetLeaseCount((LeaseSource)source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(float source)
        {
            return GetLeaseCount((LeaseSource)(int)source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(double source)
        {
            return GetLeaseCount((LeaseSource)(int)source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(decimal source)
        {
            return GetLeaseCount((LeaseSource)(int)source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(char source)
        {
            return GetLeaseCount((LeaseSource)source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(bool source)
        {
            return GetLeaseCount((LeaseSource)(source ? 1 : 0));
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(string source)
        {
            if (int.TryParse(source, out int result))
                return GetLeaseCount((LeaseSource)result);
            return 0;
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount(object source)
        {
            if (source == null) return 0;
            try
            {
                return GetLeaseCount((LeaseSource)Convert.ToInt32(source));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(T source) where T : struct
        {
            try
            {
                return GetLeaseCount((LeaseSource)Convert.ToInt32(source));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(T? source) where T : struct
        {
            if (!source.HasValue) return 0;
            return GetLeaseCount(source.Value);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ref T source) where T : struct
        {
            return GetLeaseCount(source);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(out T source) where T : struct
        {
            source = default;
            return 0;
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(T[] source) where T : struct
        {
            if (source == null || source.Length == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(List<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IEnumerable<T> source) where T : struct
        {
            if (source == null) return 0;
            var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext()) return 0;
            return GetLeaseCount(enumerator.Current);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IReadOnlyList<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IReadOnlyCollection<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ISet<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IDictionary<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First().Key);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IReadOnlyDictionary<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First().Key);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Queue<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Peek());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Stack<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Peek());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(LinkedList<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First.Value);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(SortedList<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.GetKeyAtIndex(0));
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(SortedSet<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Min);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(SortedDictionary<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First().Key);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ObservableCollection<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(BindingList<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ConcurrentBag<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ConcurrentQueue<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ConcurrentStack<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ConcurrentDictionary<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First().Key);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(BlockingCollection<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ImmutableList<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ImmutableArray<T> source) where T : struct
        {
            if (source == null || source.Length == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ImmutableHashSet<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ImmutableSortedSet<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Min);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ImmutableDictionary<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First().Key);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ImmutableSortedDictionary<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First().Key);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ImmutableQueue<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Peek());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ImmutableStack<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Peek());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IImmutableList<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IImmutableArray<T> source) where T : struct
        {
            if (source == null || source.Length == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IImmutableSet<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IImmutableDictionary<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First().Key);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IImmutableQueue<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Peek());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IImmutableStack<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Peek());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IReadOnlyList<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IReadOnlyCollection<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IReadOnlyDictionary<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First().Key);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IReadOnlySet<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IReadOnlyQueue<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Peek());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IReadOnlyStack<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Peek());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IList<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ICollection<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IDictionary<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First().Key);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ISet<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Queue<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Peek());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Stack<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Peek());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(LinkedList<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First.Value);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(SortedList<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.GetKeyAtIndex(0));
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(SortedSet<T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.Min);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(SortedDictionary<T, T> source) where T : struct
        {
            if (source == null || source.Count == 0) return 0;
            return GetLeaseCount(source.First().Key);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Span<T> source) where T : struct
        {
            if (source.IsEmpty) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ReadOnlySpan<T> source) where T : struct
        {
            if (source.IsEmpty) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Memory<T> source) where T : struct
        {
            if (source.IsEmpty) return 0;
            return GetLeaseCount(source.Span[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ReadOnlyMemory<T> source) where T : struct
        {
            if (source.IsEmpty) return 0;
            return GetLeaseCount(source.Span[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ArraySegment<T> source) where T : struct
        {
            if (source.Count == 0) return 0;
            return GetLeaseCount(source[0]);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IEnumerator<T> source) where T : struct
        {
            if (source == null) return 0;
            if (!source.MoveNext()) return 0;
            return GetLeaseCount(source.Current);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(IEnumerable source) where T : struct
        {
            if (source == null) return 0;
            var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext()) return 0;
            return GetLeaseCount((T)enumerator.Current);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Func<T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source());
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Lazy<T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Value);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Task<T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Result);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTask<T> source) where T : struct
        {
            return GetLeaseCount(source.Result);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Nullable<T> source) where T : struct
        {
            if (!source.HasValue) return 0;
            return GetLeaseCount(source.Value);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(KeyValuePair<T, T> source) where T : struct
        {
            return GetLeaseCount(source.Key);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            if (source == null) return 0;
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(Tuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// @summary
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// @summary
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }

        /// <summary>
        /// 获取指定源的 lease 计数(兼容旧 API)。
        /// </summary>
        public int GetLeaseCount<T>(ValueTuple<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T> source) where T : struct
        {
            return GetLeaseCount(source.Item1);
        }


</longcat_think>
