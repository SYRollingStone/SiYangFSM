using System;
using System.Collections.Generic;

namespace SiYangFSM
{
    /// <summary>
    /// 状态接口：可以被状态机或外部驱动器调用。
    /// </summary>
    public interface IState<T> where T : IComparable
    {
        T StateKey { get; } // 使用泛型类型作为状态标识

        /// <summary>进入状态时调用。</summary>
        void OnEnter();

        /// <summary>离开状态时调用。</summary>
        void OnExit();

        /// <summary>每帧更新，由外部传入 deltaTime。</summary>
        void Tick(float deltaTime);

        /// <summary>物理更新，可选。</summary>
        void FixedTick(float fixedDeltaTime);

        /// <summary>事件分发，可选。</summary>
        void HandleEvent(string eventId, object data = null);
    }

    /// <summary>
    /// 简单的状态基类，帮你填充空实现。
    /// </summary>
    public abstract class StateBase<T> : IState<T> where T : IComparable
    {
        public T StateKey { get; }

        protected StateBase(T stateKey)
        {
            StateKey = stateKey;
        }

        public virtual void OnEnter() { }
        public virtual void OnExit() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void FixedTick(float fixedDeltaTime) { }
        public virtual void HandleEvent(string eventId, object data = null) { }
    }

    /// <summary>
    /// 用委托快速创建状态，不想写子类的时候用这个。
    /// </summary>
    public class SimpleState<T> : IState<T> where T : IComparable
    {
        public T StateKey { get; }

        private readonly Action _onEnter;
        private readonly Action _onExit;
        private readonly Action<float> _tick;
        private readonly Action<float> _fixedTick;
        private readonly Action<string, object> _onEvent;

        public SimpleState(
            T stateKey,
            Action onEnter = null,
            Action onExit = null,
            Action<float> tick = null,
            Action<float> fixedTick = null,
            Action<string, object> onEvent = null)
        {
            StateKey = stateKey;
            _onEnter = onEnter ?? (() => { });
            _onExit = onExit ?? (() => { });
            _tick = tick ?? (_ => { });
            _fixedTick = fixedTick ?? (_ => { });
            _onEvent = onEvent ?? ((_, __) => { });
        }

        public void OnEnter() => _onEnter();
        public void OnExit() => _onExit();
        public void Tick(float deltaTime) => _tick(deltaTime);
        public void FixedTick(float fixedDeltaTime) => _fixedTick(fixedDeltaTime);
        public void HandleEvent(string eventId, object data = null) => _onEvent(eventId, data);
    }

    /// <summary>
    /// 转换条件接口，方便以后做更复杂的条件系统（组合条件等）。
    /// </summary>
    public interface ITransitionCondition
    {
        bool Evaluate();
    }

    /// <summary>
    /// 最常用的实现：直接用 Func&lt;bool&gt;。
    /// </summary>
    public class FuncCondition : ITransitionCondition
    {
        private readonly Func<bool> _func;

        public FuncCondition(Func<bool> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        public bool Evaluate() => _func();
    }

    /// <summary>
    /// 状态转换。
    /// </summary>
    public class Transition<T> where T : IComparable
    {
        public IState<T> From { get; }
        public IState<T> To { get; }
        public ITransitionCondition Condition { get; }
        public int Priority { get; }

        /// <param name="from">null 表示 AnyState。</param>
        public Transition(IState<T> from, IState<T> to, ITransitionCondition condition, int priority = 0)
        {
            From = from;
            To = to ?? throw new ArgumentNullException(nameof(to));
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Priority = priority;
        }
    }

    /// <summary>
    /// 核心状态机。自身实现 IState，因此支持嵌套。
    /// 状态存储由 List 改为 Dictionary：key = T(通常为枚举)。
    /// </summary>
    public class StateMachine<T> : IState<T> where T : IComparable
    {
        public T StateKey { get; }

        public IState<T> CurrentState { get; private set; }
        public IState<T> DefaultState { get; private set; }

        /// <summary>便捷：当前状态的 key（CurrentState==null 时返回 default(T)）。</summary>
        public T CurrentKey => CurrentState != null ? CurrentState.StateKey : default;

        /// <summary>状态切换事件（旧状态，新状态）。</summary>
        public event Action<IState<T>, IState<T>> OnStateChanged;

        /// <summary>这个状态机使用的上下文对象，可以是任意类型。</summary>
        public object Context { get; }

        /// <summary>当前状态已停留时间。</summary>
        public float TimeInCurrentState { get; private set; }

        // ✅ Dictionary 存状态：key = T
        private readonly Dictionary<T, IState<T>> _statesByKey = new Dictionary<T, IState<T>>();

        private readonly List<Transition<T>> _transitions = new List<Transition<T>>();
        private readonly List<Transition<T>> _anyStateTransitions = new List<Transition<T>>();

        public StateMachine(T stateKey, object context = null)
        {
            StateKey = stateKey;
            Context = context;
        }

        #region 配置 API（运行时组装）

        /// <summary>
        /// 添加状态。默认同 key 不允许重复；如需覆盖可 overwrite=true。
        /// </summary>
        public void AddState(IState<T> state, bool setAsDefault = false, bool overwrite = false)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var key = state.StateKey;

            if (_statesByKey.ContainsKey(key))
            {
                if (!overwrite)
                    throw new InvalidOperationException($"State with key '{key}' already exists.");
                _statesByKey[key] = state;
            }
            else
            {
                _statesByKey.Add(key, state);
            }

            if (setAsDefault || DefaultState == null)
                DefaultState = state;
        }

        /// <summary>根据 key 获取状态（不存在返回 null）。</summary>
        public IState<T> GetState(T key)
        {
            _statesByKey.TryGetValue(key, out var state);
            return state;
        }

        /// <summary>设置默认状态（对象）。</summary>
        public void SetDefaultState(IState<T> state)
        {
            DefaultState = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>✅ 设置默认状态（key）。</summary>
        public void SetDefaultState(T key)
        {
            var s = GetState(key);
            if (s == null) throw new KeyNotFoundException($"Default state key '{key}' not found.");
            DefaultState = s;
        }

        /// <summary>From 为 null 相当于 AnyState。</summary>
        public void AddTransition(IState<T> from, IState<T> to, Func<bool> condition, int priority = 0)
        {
            var t = new Transition<T>(from, to, new FuncCondition(condition), priority);
            if (from == null)
                _anyStateTransitions.Add(t);
            else
                _transitions.Add(t);
        }

        public void AddTransition(IState<T> from, IState<T> to, ITransitionCondition condition, int priority = 0)
        {
            var t = new Transition<T>(from, to, condition, priority);
            if (from == null)
                _anyStateTransitions.Add(t);
            else
                _transitions.Add(t);
        }

        #endregion

        #region IState 接口实现（用于嵌套）

        public void OnEnter()
        {
            // 进入状态机时自动进入默认状态
            if (CurrentState == null && DefaultState != null)
            {
                ForceSetState(DefaultState, allowReenter: true);
            }
        }

        public void OnExit()
        {
            if (CurrentState != null)
            {
                CurrentState.OnExit();
                CurrentState = null;
            }
            TimeInCurrentState = 0f;
        }

        public void Tick(float deltaTime)
        {
            TimeInCurrentState += deltaTime;

            // 先检查转换
            var transition = GetTriggeredTransition();
            if (transition != null)
            {
                ChangeState(transition.To);
            }

            // 再更新当前状态
            CurrentState?.Tick(deltaTime);
        }

        public void FixedTick(float fixedDeltaTime)
        {
            CurrentState?.FixedTick(fixedDeltaTime);
        }

        public void HandleEvent(string eventId, object data = null)
        {
            CurrentState?.HandleEvent(eventId, data);
        }

        #endregion

        #region 状态切换

        private Transition<T> GetTriggeredTransition()
        {
            Transition<T> best = null;

            // 先检查 AnyState 转换（排除当前状态自身的情况）
            foreach (var t in _anyStateTransitions)
            {
                if (t.To == CurrentState) continue; // 避免无意义自跳
                if (!t.Condition.Evaluate()) continue;

                if (best == null || t.Priority > best.Priority)
                    best = t;
            }

            // 再检查从当前状态出发的转换
            foreach (var t in _transitions)
            {
                if (t.From != CurrentState) continue;
                if (!t.Condition.Evaluate()) continue;

                if (best == null || t.Priority > best.Priority)
                    best = t;
            }

            return best;
        }

        private void ChangeState(IState<T> newState, bool allowReenter = false)
        {
            if (newState == null) return;

            if (!allowReenter && newState == CurrentState)
                return;

            var prev = CurrentState;

            prev?.OnExit();
            CurrentState = newState;
            TimeInCurrentState = 0f;
            CurrentState.OnEnter();

            OnStateChanged?.Invoke(prev, CurrentState);
        }

        /// <summary>
        /// 强制切换到一个状态对象，可以指定是否允许重新进入当前状态。
        /// </summary>
        public void ForceSetState(IState<T> newState, bool allowReenter = true)
        {
            ChangeState(newState, allowReenter);
        }

        /// <summary>
        /// ✅ 强制切换到一个状态 key（不存在会抛异常）。
        /// </summary>
        public void ForceSetState(T key, bool allowReenter = true)
        {
            var s = GetState(key);
            if (s == null) throw new KeyNotFoundException($"State key '{key}' not found.");
            ChangeState(s, allowReenter);
        }

        #endregion

        #region 递归深度判断当前状态（支持 key / 路径）

        /// <summary>
        /// 判断当前状态机是否处于（或处于子状态机中）某个状态对象
        /// </summary>
        public bool IsInState(IState<T> state)
        {
            if (state == null) return false;
            return IsInState(state.StateKey);
        }

        /// <summary>
        /// 直接用 key 判断（适配枚举判断）
        /// </summary>
        public bool IsInState(T stateKey)
        {
            if (CurrentState == null) return false;

            // 情况1：当前状态就是目标 key
            if (Comparer<T>.Default.Compare(CurrentState.StateKey, stateKey) == 0)
                return true;

            // 情况2：当前状态本身也是一个状态机（嵌套），递归查询子状态机
            if (CurrentState is StateMachine<T> subMachine)
                return subMachine.IsInState(stateKey);

            return false;
        }

        /// <summary>
        /// 判断当前状态机是否处于目标状态，并尝试获取状态路径（IState 版本）
        /// </summary>
        public bool IsInState(IState<T> targetState, out List<IState<T>> statePath)
        {
            statePath = new List<IState<T>>();
            if (targetState == null) return false;

            return GetStatePathRecursive(this, targetState.StateKey, statePath);
        }

        /// <summary>
        /// ✅ 判断当前状态机是否处于目标状态，并获取 key 路径（更适合枚举调试）
        /// </summary>
        public bool IsInState(T targetKey, out List<T> keyPath)
        {
            keyPath = new List<T>();
            return GetKeyPathRecursive(this, targetKey, keyPath);
        }

        private bool GetStatePathRecursive(StateMachine<T> currentMachine, T targetKey, List<IState<T>> path)
        {
            if (currentMachine.CurrentState == null) return false;

            path.Add(currentMachine.CurrentState);

            if (Comparer<T>.Default.Compare(currentMachine.CurrentState.StateKey, targetKey) == 0)
                return true;

            if (currentMachine.CurrentState is StateMachine<T> subMachine)
            {
                if (GetStatePathRecursive(subMachine, targetKey, path))
                    return true;
            }

            path.RemoveAt(path.Count - 1); // 回溯
            return false;
        }

        private bool GetKeyPathRecursive(StateMachine<T> currentMachine, T targetKey, List<T> path)
        {
            if (currentMachine.CurrentState == null) return false;

            path.Add(currentMachine.CurrentState.StateKey);

            if (Comparer<T>.Default.Compare(currentMachine.CurrentState.StateKey, targetKey) == 0)
                return true;

            if (currentMachine.CurrentState is StateMachine<T> subMachine)
            {
                if (GetKeyPathRecursive(subMachine, targetKey, path))
                    return true;
            }

            path.RemoveAt(path.Count - 1); // 回溯
            return false;
        }

        #endregion
    }
}
