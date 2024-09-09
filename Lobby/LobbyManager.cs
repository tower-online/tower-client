using System;
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tower.Network;
using Tower.System;

namespace Tower.Lobby;

public partial class LobbyManager : Node
{
    private Connection? _connectionManager;
    private GameManager? _gameManager;

    [Signal]
    public delegate void SUpdateCharacterSlotsEventHandler(UpdateCharacterSlotsEventArgs args);

    private Control? _characterSlots;
    private readonly PackedScene _characterSlotScene = GD.Load<PackedScene>("res://Lobby/CharacterSlot.tscn");

    public override void _Ready()
    {
        _connectionManager = GetNode<Connection>("/root/ConnectionManager");
        _gameManager = GetNode<GameManager>("/root/GameManager");

        _characterSlots = GetNode<Control>("CharacterSlots");

        SUpdateCharacterSlots += OnUpdateCharacterSlots;

        _ = Task.Run(UpdateCharacterSlots);
    }

    private async void UpdateCharacterSlots()
    {
        if (_gameManager!.Username is null) return;

#if TOWER_PLATFORM_TEST
        var token = await Auth.RequestToken(_gameManager.Username);
#elif TOWER_PLATFORM_STEAM
        //TODO
#endif
        if (token is null) return;
        // _gameManager.AuthToken = token;

        var characters = await Auth.RequestCharacters(_gameManager.Username, token);
        if (characters is null) return;
        GD.Print($"Got {characters.Count} characters");

        CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SUpdateCharacterSlots,
            new UpdateCharacterSlotsEventArgs(characters));
    }

    private void OnUpdateCharacterSlots(UpdateCharacterSlotsEventArgs args)
    {
        foreach (var character in args.Characters)
        {
            var slot = _characterSlotScene.Instantiate<Control>();
            _characterSlots!.AddChild(slot);

            slot.GetNode<Label>("Username").Text = character;
        }
        
        
        
        // Send ClientJoinRequest with acquired token
        // var builder = new FlatBufferBuilder(512);
        // var request =
        //     ClientJoinRequest.CreateClientJoinRequest(builder,
        //         builder.CreateString(Username),
        //         ClientPlatform.TEST, builder.CreateString(), builder.CreateString(token));
        // var packetBase = PacketBase.CreatePacketBase(builder, PacketType.ClientJoinRequest, request.Value);
        // builder.FinishSizePrefixed(packetBase.Value);
        //
        // SendPacket(builder.DataBuffer);
    }
}

public partial class UpdateCharacterSlotsEventArgs(List<string> characters) : GodotObject
{
    public List<string> Characters { get; } = characters;
}