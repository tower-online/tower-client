using System;
using System.Collections.Generic;
using Godot;
using Tower.Entity;

namespace Tower.Player;

public partial class InteractionManager : Node
{
    private PlayerBase _localPlayer;
    private readonly List<Tuple<IInteractable, Node3D>> _interactables = [];
    private CanvasLayer _hud;
    private Label _interactionLabel;
    private string _interactionKeyName = "?";

    public override void _Ready()
    {
        var entityManager = GetNode<EntityManager>("/root/EntityManager");
        entityManager.LocalPlayerSpawnedEvent += OnLocalPlayerSpawned;

        _hud = GetNode<CanvasLayer>("HUD");
        _hud.Visible = false;
        _interactionLabel = GetNode<Label>("HUD/InteractionLabel");

        //TODO: This doesn't work
        // Get physical key name of Interaction
        // if (InputMap.HasAction("Interaction"))
        // {
        //     var events = InputMap.ActionGetEvents("Interaction");
        //     foreach (var e in events)
        //     {
        //         if (e is not InputEventKey keyEvent) continue;
        //         _interactionKeyName = OS.GetKeycodeString(keyEvent.PhysicalKeycode);
        //         break;
        //     }
        // } 
        
        UpdateInteractionHud();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("Interact"))
        {
            HandleInteraction();
        }
    }

    private void OnLocalPlayerSpawned(PlayerBase player)
    {
        _localPlayer = player;
        player.InteractionArea.AreaEntered += OnLocalPlayerEnteredInteractable;
        player.InteractionArea.AreaExited += OnLocalPlayerExitedInteractable;
    }

    private void OnLocalPlayerEnteredInteractable(Area3D area)
    {
        if (area is not IInteractable interactable)
            return;

        _interactables.Add(new Tuple<IInteractable, Node3D>(interactable, area));
        SortInteractablesByDistance();
        UpdateInteractionHud();
    }

    private void OnLocalPlayerExitedInteractable(Area3D area)
    {
        if (area is not IInteractable interactable)
            return;

        _interactables.Remove(new Tuple<IInteractable, Node3D>(interactable, area));
        SortInteractablesByDistance();
        UpdateInteractionHud();
    }

    private void SortInteractablesByDistance()
    {
        _interactables.Sort((a, b) =>
            _localPlayer.GlobalPosition.DistanceSquaredTo(a.Item2.GlobalPosition)
                .CompareTo(_localPlayer.GlobalPosition.DistanceSquaredTo(b.Item2.GlobalPosition))
        );
    }

    private void UpdateInteractionHud()
    {
        if (_interactables.Count == 0)
        {
            _hud.Visible = false;
            return;
        }

        _hud.Visible = true;
        _interactionLabel.Text = $"[E] {_interactables[0].Item1.GetInteractionPrompt()}";
    }

    private void HandleInteraction()
    {
        if (_interactables.Count == 0)
            return;
        
        _interactables[0].Item1.Interact();
    }
}