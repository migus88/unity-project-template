# Domain code patterns

Minimal shapes. Placeholders in `<>`. If a shape here disagrees with the code in `Core/Code/`, the code wins; fix this file.

## Flow presenter (owns the end of the domain)

```csharp
internal sealed class <Name>FlowPresenter : IAsyncStartable, IDisposable
{
    private IDisposable? _inputMaps;

    private readonly <Name>Args _args;
    private readonly IInputService _input;
    private readonly <Feature>Service _service;
    private readonly DomainCompletion<<Name>Result> _completion;

    public <Name>FlowPresenter(<Name>Args args, IInputService input, <Feature>Service service, DomainCompletion<<Name>Result> completion)
    {
        _args = args;
        _input = input;
        _service = service;
        _completion = completion;
    }

    public async UniTask StartAsync(CancellationToken ct)
    {
        _inputMaps = _input.Push(InputMaps.Player | InputMaps.Ui);
        var result = await _service.RunAsync(ct);
        _completion.Complete(result);
    }

    public void Dispose()
    {
        _inputMaps?.Dispose();
    }
}
```

## Presenter bound to a view

```csharp
internal sealed class <Feature>Presenter : IStartable, IDisposable
{
    private DisposableBag _subscriptions;

    private readonly <Feature>View _view;
    private readonly <Feature>Model _model;

    public <Feature>Presenter(<Feature>View view, <Feature>Model model)
    {
        _view = view;
        _model = model;
    }

    public void Start()
    {
        _model.Value.Subscribe(_view.SetValue).AddTo(ref _subscriptions);
        _view.ConfirmClicked
            .SubscribeAwait((_, ct) => ConfirmAsync(ct).AsValueTask(), AwaitOperation.Drop)
            .AddTo(ref _subscriptions);
    }

    private async UniTask ConfirmAsync(CancellationToken ct)
    {
        _view.SetInteractable(false);

        try
        {
            await _model.ConfirmAsync(ct);
        }
        finally
        {
            _view.SetInteractable(true);
        }
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
    }
}
```

## Model with observable state

```csharp
internal sealed class <Feature>Model : IDisposable
{
    public ReadOnlyReactiveProperty<int> Value => _value;

    private readonly ReactiveProperty<int> _value = new(0);

    public void Add(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be positive.");
        }

        _value.Value += amount;
    }

    public void Dispose()
    {
        _value.Dispose();
    }
}
```

## Request channel (one-off notification)

```csharp
internal sealed class <Thing>Requests : IDisposable
{
    public Observable<Unit> Requested => _requested;

    private readonly Subject<Unit> _requested = new();

    public void Request()
    {
        _requested.OnNext(Unit.Default);
    }

    public void Dispose()
    {
        _requested.Dispose();
    }
}
```

## Input handler with locking

```csharp
internal sealed class <Name>InputHandler : GameInput.IPlayerActions, ILockable<InputLockTag>, IStartable, IDisposable
{
    public InputLockTag LockTags => InputLockTag.Movement;

    private bool _isLocked;

    private readonly IInputService _input;
    private readonly ILockService<InputLockTag> _locks;
    private readonly <Name>InputState _state;

    public <Name>InputHandler(IInputService input, ILockService<InputLockTag> locks, <Name>InputState state)
    {
        _input = input;
        _locks = locks;
        _state = state;
    }

    public void Start()
    {
        _input.Actions.Player.AddCallbacks(this);
        _locks.Subscribe(this);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (_isLocked)
        {
            return;
        }

        _state.Move = context.canceled ? Vector2.zero : context.ReadValue<Vector2>();
    }

    public void HandleLocking()
    {
        _isLocked = true;
        _state.Move = Vector2.zero;
    }

    public void HandleUnlocking()
    {
        _isLocked = false;
        var move = _input.Actions.Player.Move;
        _state.Move = move.enabled ? move.ReadValue<Vector2>() : Vector2.zero;
    }

    public void Dispose()
    {
        _input.Actions.Player.RemoveCallbacks(this);
        _locks.Unsubscribe(this);
    }
}
```

Implement every other action of the map with an empty body. `ILockable`/`ILockService` come from `Migs.MLock.Interfaces` (asmdef reference `MLock.Runtime`).

## Config ScriptableObject

```csharp
[CreateAssetMenu(menuName = "<Name>/<Name> Config")]
internal sealed class <Name>Config : ScriptableObject
{
    [field: SerializeField, Min(0f)] public float DurationSeconds { get; private set; } = 1f;
    [field: SerializeField] public AudioCue CompletedCue { get; private set; } = null!;
}
```

Read-only at runtime. Use `[Min]`/`[Range]` and `OnValidate` for constraints.
