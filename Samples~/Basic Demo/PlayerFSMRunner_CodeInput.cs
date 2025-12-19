// PlayerFSMRunner_CodeInput.cs
using UnityEngine;
using UnityEngine.InputSystem;   // 新 Input System
using SiYangFSM; // 你的 FSM 命名空间

public enum EnumCodeInput
{
    PlayerRoot,
    Dead,
    Locomotion,
    Idle,
    Walk,
    Run
}

public class PlayerFSMRunner_CodeInput : MonoBehaviour
{
    private StateMachine<EnumCodeInput> _fsm;
    private PlayerContext _ctx;

    private InputActionMap _map;
    private InputAction _moveAction;
    private InputAction _sprintAction;
    private InputAction _toggleDeadAction;

    private void Awake()
    {
        // 1. 创建 FSM
        _fsm = PlayerStateMachineFactory.CreatePlayerStateMachine();
        _ctx = (PlayerContext)_fsm.Context;

        _fsm.OnEnter();

        // 2. 纯代码创建 InputAction
        SetupInputActions();
    }

    private void OnDestroy()
    {
        if (_map != null)
        {
            _toggleDeadAction.performed -= OnToggleDeadPerformed;
            _map.Disable();
            _map.Dispose();
        }
    }

    private void SetupInputActions()
    {
        // 创建一个 ActionMap
        _map = new InputActionMap("Gameplay");

        // ===== Move: Vector2 =====
        _moveAction = _map.AddAction(
            name: "Move",
            type: InputActionType.Value);

        // 键盘 WASD 组合成 2DVector
        var wasdComposite = _moveAction.AddCompositeBinding("2DVector");
        wasdComposite.With("Up", "<Keyboard>/w");
        wasdComposite.With("Down", "<Keyboard>/s");
        wasdComposite.With("Left", "<Keyboard>/a");
        wasdComposite.With("Right", "<Keyboard>/d");

        // 再加一个手柄左摇杆
        _moveAction.AddBinding("<Gamepad>/leftStick");

        // ===== Sprint: Button =====
        _sprintAction = _map.AddAction(
            name: "Sprint",
            type: InputActionType.Button);

        _sprintAction.AddBinding("<Keyboard>/leftShift");
        _sprintAction.AddBinding("<Gamepad>/leftStickPress");

        // ===== ToggleDead: Button =====
        _toggleDeadAction = _map.AddAction(
            name: "ToggleDead",
            type: InputActionType.Button);

        _toggleDeadAction.AddBinding("<Keyboard>/k");
        _toggleDeadAction.AddBinding("<Gamepad>/rightShoulder");

        // 注册事件
        _toggleDeadAction.performed += OnToggleDeadPerformed;

        // 启用整个 ActionMap
        _map.Enable();
    }

    private void Update()
    {
        if (_fsm == null) return;

        // 1. 从 InputActions 读值到 Context

        // Move: Vector2
        _ctx.MoveInput = _moveAction.ReadValue<Vector2>();

        // Sprint: 按钮是否按下
        _ctx.IsSprinting = _sprintAction.IsPressed();

        // 2. 驱动状态机（只 Tick 顶层）
        _fsm.Tick(Time.deltaTime);

        // 3. 可选：用 _ctx.Speed 驱动位移 / 动画
        // var dir = new Vector3(_ctx.MoveInput.x, 0, _ctx.MoveInput.y);
        // transform.position += dir.normalized * _ctx.Speed * Time.deltaTime;
    }

    private void FixedUpdate()
    {
        _fsm?.FixedTick(Time.fixedDeltaTime);
    }

    private void OnToggleDeadPerformed(InputAction.CallbackContext ctxInput)
    {
        _ctx.IsDead = !_ctx.IsDead;
        Debug.Log($"Toggle IsDead = {_ctx.IsDead}");
    }

    public void CurrentIsRunBtnOnClick()
    {
        if (_fsm.IsInState(EnumCodeInput.Run))
        {
            Debug.Log("CurrentIsRunBtnOnClick is Run!");
        }
        else
        {
            Debug.Log("CurrentIsRunBtnOnClick is not Run!");
        }
    }
}
