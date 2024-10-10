using Godot;
using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;
using Tower.Network;
using tower.network.packet;
using Tower.System;

namespace Tower.Lobby;

public partial class LobbyManager : Node
{
    [Signal]
    public delegate void SUpdateCharacterSlotsEventHandler(UpdateCharacterSlotsEventArgs args);
    
    private Connection _connectionManager;
    private GameManager _gameManager;

    private Control _characterSlots;
    private Control _characterCreator;
    private readonly PackedScene _characterSlotScene = GD.Load<PackedScene>("res://Lobby/CharacterSlot.tscn");

    private readonly ILogger _logger = new GodotLogger(nameof(LobbyManager));

    public override void _Ready()
    {
        _connectionManager = GetNode<Connection>("/root/ConnectionManager");
        _gameManager = GetNode<GameManager>("/root/GameManager");

        _characterSlots = GetNode<Control>("CharacterSlots");
        _characterCreator = _characterSlots.GetNode<Control>("CharacterCreator");
        _characterCreator.GetNode<Button>("CreateButton").Pressed += () =>
        {
            var holder = _characterCreator.GetNode<LineEdit>("CharacterNameHolder");
            HandleCharacterCreate(holder.Text);
        };

        SUpdateCharacterSlots += OnUpdateCharacterSlots;

        _ = Task.Run(UpdateCharacterSlots);
    }

    private async Task UpdateCharacterSlots()
    {
        if (_gameManager!.Username is null) return;

#if TOWER_PLATFORM_TEST
        var token = await Authentication.RequestAuthTokenAsync(
            Settings.RemoteHost, Settings.RemoteAuthPort, _gameManager.Username, Authentication.Platform.Test, _logger);
#elif TOWER_PLATFORM_STEAM
        //TODO
#endif
        if (token is null) return;
        _gameManager.AuthToken = token;

#if TOWER_PLATFORM_TEST
        var characters = await Authentication.RequestCharactersAsync(
            Settings.RemoteHost, Settings.RemoteAuthPort, _gameManager.Username,
            token, Authentication.Platform.Test, _logger);
#elif TOWER_PLATFORM_STEAM
        //TODO
#endif
        if (characters is null) return;
        GD.Print($"Got {characters.Count} characters");

        CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SUpdateCharacterSlots,
            new UpdateCharacterSlotsEventArgs(characters));
    }

    private void OnUpdateCharacterSlots(UpdateCharacterSlotsEventArgs args)
    {
        foreach (var characterName in args.Characters)
        {
            var slot = _characterSlotScene.Instantiate<CharacterSlot>();
            _characterSlots!.AddChild(slot);

            slot.CharacterName.Text = characterName;
            slot.StartButtonPressed += HandleCharacterStart;
        }
    }

    private void HandleCharacterCreate(string characterName)
    {
        //TODO
        // _ = Task.Run(async () =>
        // {
        //     if (!await Auth.RequestCharacterCreate(
        //             _gameManager.Username, _gameManager.AuthToken, characterName, "HUMAN")) return;
        //
        //     await UpdateCharacterSlots();
        // });
    }

    private void HandleCharacterStart(string characterName)
    {
        // Send ClientJoinRequest
        var builder = new FlatBufferBuilder(512);
        var request = ClientJoinRequest.CreateClientJoinRequest(builder,
            builder.CreateString(_gameManager.Username),
            builder.CreateString(characterName),
            builder.CreateString(_gameManager.AuthToken));
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.ClientJoinRequest, request.Value);
        builder.FinishSizePrefixed(packetBase.Value);

        if (!_connectionManager.IsConnected)
        {
            _ = Task.Run(async () =>
            {
                if (!await _connectionManager.ConnectAsync(Settings.RemoteHost, Settings.RemoteMainPort)) return;
                _connectionManager.SendPacket(builder.DataBuffer);
            });
        }
        else
        {
            _connectionManager.SendPacket(builder.DataBuffer);
        }
    }
}

public partial class UpdateCharacterSlotsEventArgs(List<string> characters) : GodotObject
{
    public List<string> Characters { get; } = characters;
}