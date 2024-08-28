using System.Collections.Generic;
using Godot;

namespace Tower.System;

[GlobalClass]
public sealed partial class StateMachine : Node
{
    private State _currentState;
    private readonly Dictionary<StringName, State> _states = new();

    [Export] private State InitialState { get; set; }

    public override void _Ready()
    {
        _currentState = InitialState;

        foreach (var node in GetChildren())
        {
            if (node is not State state) continue;
            _states[state.Name] = state;
            state.Transitioned += OnTransition;
        }
    }

    public override void _Process(double delta)
    {
        _currentState?.Update(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        _currentState?.PhysicsUpdate(delta);
    }

    private void OnTransition(StringName oldStateName, StringName newStateName)
    {
        if (!_states.TryGetValue(oldStateName, out var oldState) || _currentState != oldState) return;
        if (!_states.TryGetValue(newStateName, out var newState)) return;
        
        _currentState?.Exit();
        newState.Enter();
        _currentState = newState;
    }
}