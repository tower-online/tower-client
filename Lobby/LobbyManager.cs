using Godot;
using System;
using System.Threading.Tasks;
using Tower.Network;
using Tower.System;

namespace Tower.Lobby;

public partial class LobbyManager : Node
{
    private Connection _connectionManager;
    private GameManager _gameManager;

    [Signal]
    public delegate void SUpdateCharacterSlotsEventHandler();

    private Control _charactersContainer;
    private readonly PackedScene _characterSlotScene = GD.Load<PackedScene>("res://Lobby/CharacterSlot.tscn");

    public override void _Ready()
    {
        _connectionManager = GetNode<Connection>("/root/ConnectionManager");
        _gameManager = GetNode<GameManager>("/root/GameManager");

        _charactersContainer = GetNode<Control>("Characters");

        SUpdateCharacterSlots += OnUpdateCharacterSlots;

        _ = Task.Run(UpdateCharacterSlots);
    }

    private async void UpdateCharacterSlots()
    {
#if TOWER_PLATFORM_TEST
        _gameManager.AuthToken = await Auth.RequestToken(_gameManager.Username);
#elif TOWER_PLATFORM_STEAM
        //TODO
#endif
        if (_gameManager.AuthToken == default) return;

        var characters = await Auth.RequestCharacters(_gameManager.Username, _gameManager.AuthToken);
        GD.Print($"Got {characters.Count} characters");

        // CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SUpdateCharacterSlots, );
    }

    private void OnUpdateCharacterSlots()
    {
        
    }
}