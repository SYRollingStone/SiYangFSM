using System;
using System.Collections.Generic;

namespace SiYangFSM
{
    /// <summary>
    /// 状态接口：可以被状态机或外部驱动器调用。
    /// </summary>
    public interface IState
    {
        string Name { get; }

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
    public abstract class StateBase : IState
    {
        public string Name { get; }

        protected StateBase(string name)
        {
            Name = name;
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
    public class SimpleState : IState
    {
        public string Name { get; }

        private readonly Action _onEnter;
        private readonly Action _onExit;
        private readonly Action<float> _tick;
        private readonly Action<float> _fixedTick;
        private readonly Action<string, object> _onEvent;

        public SimpleState(
            string name,
            Action onEnter = null,
            Action onExit = null,
            Action<float> tick = null,
            Action<float> fixedTick = null,
            Action<string, object> onEvent = null)
        {
            Name = name;
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
    public class Transition
    {
        public IState From { get; }
        public IState To { get; }
        public ITransitionCondition Condition { get; }
        public int Priority { get; }

        /// <param name="from">null 表示 AnyState。</param>
        public Transition(IState from, IState to, ITransitionCondition condition, int priority = 0)
        {
            From = from;
            To = to ?? throw new ArgumentNullException(nameof(to));
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Priority = priority;
        }
    }

    /// <summary>
    /// 核心状态机。自身实现 IState，因此支持嵌套。
    /// </summary>
    public class StateMachine : IState
    {
        public string Name { get; }

        public IState CurrentState { get; private set; }
        public IState DefaultState { get; private set; }

        /// <summary>状态切换事件（旧状态，新状态）。</summary>
        public event Action<IState, IState> OnStateChanged;

        private readonly List<IState> _states = new List<IState>();
        private readonly List<Transition> _transitions = new List<Transition>();
        private readonly List<Transition> _anyStateTransitions = new List<Transition>();

        /// <summary>这个状态机使用的上下文对象，可以是任意类型。</summary>
        public object Context { get; }

        /// <summary>当前状态已停留时间。</summary>
        public float TimeInCurrentState { get; private set; }

        public StateMachine(string name, object context = null)
        {
            Name = name;
            Context = context;
        }

        #region 配置 API（运行时组装）

        public void AddState(IState state, bool setAsDefault = false)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!_states.Contains(state))
                _states.Add(state);

            if (setAsDefault || DefaultState == null)
                DefaultState = state;
        }

        /// <summary>From 为 null 相当于 AnyState。</summary>
        public void AddTransition(IState from, IState to, Func<bool> condition, int priority = 0)
        {
            var t = new Transition(from, to, new FuncCondition(condition), priority);
            if (from == null)
                _anyStateTransitions.Add(t);
            else
                _transitions.Add(t);
        }

        public void AddTransition(IState from, IState to, ITransitionCondition condition, int priority = 0)
        {
            var t = new Transition(from, to, condition, priority);
            if (from == null)
                _anyStateTransitions.Add(t);
            else
                _transitions.Add(t);
        }

        /// <summary>设置初始状态（如果不设置，就用第一个 AddState 的状态）。</summary>
        public void SetDefaultState(IState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            DefaultState = state;
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

        private Transition GetTriggeredTransition()
        {
            // 先检查 AnyState 转换（排除当前状态自身的情况）
            Transition best = null;

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

        private void ChangeState(IState newState, bool allowReenter = false)
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
        /// 强制切换到一个状态，可以指定是否允许重新进入当前状态。
        /// （你可以在外部直接用这个来实现随时跳转）
        /// </summary>
        public void ForceSetState(IState newState, bool allowReenter = true)
        {
            ChangeState(newState, allowReenter);
        }

        #endregion

        #region 递归深度判断当前状态

        /// <summary>
        /// 判断当前状态机是否处于（或处于目标状态机的子状态机中）
        /// </summary>
        /// <param name="state">要判断的目标状态（可以是普通状态或状态机）</param>
        /// <returns>如果当前状态或任意层级父状态是目标状态，则返回true</returns>
        public bool IsInState(IState state)
        {
            if (CurrentState == null) return false;
    
            // 情况1：当前状态就是目标状态
            if (CurrentState == state) return true;
    
            // 情况2：当前状态本身也是一个状态机（嵌套），则递归查询这个子状态机
            if (CurrentState is StateMachine subMachine)
            {
                return subMachine.IsInState(state);
            }
    
            return false;
        }

        /// <summary>
        /// 判断当前状态机是否处于目标状态，并尝试获取状态路径（用于调试或详细判断）
        /// </summary>
        /// <param name="targetState">目标状态</param>
        /// <param name="statePath">输出参数，从当前状态到目标状态的路径</param>
        /// <returns>是否处于目标状态</returns>
        public bool IsInState(IState targetState, out List<IState> statePath)
        {
            statePath = new List<IState>();
            return GetStatePathRecursive(this, targetState, statePath);
        }

        private bool GetStatePathRecursive(StateMachine currentMachine, IState targetState, List<IState> path)
        {
            if (currentMachine.CurrentState == null) return false;
    
            path.Add(currentMachine.CurrentState);
    
            if (currentMachine.CurrentState == targetState) return true;
    
            if (currentMachine.CurrentState is StateMachine subMachine)
            {
                return GetStatePathRecursive(subMachine, targetState, path);
            }
    
            path.RemoveAt(path.Count - 1); // 回溯
            return false;
        }

        #endregion
    }
}
