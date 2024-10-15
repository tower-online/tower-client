using Godot;
using Microsoft.Extensions.Logging;
using tower.network.packet;

namespace Tower.System;

public partial class ChatManager : Node
{
    private ConnectionManager _connectionManager;

    private readonly ILogger _logger = new GodotLogger(nameof(ChatManager)); 
    
    public override void _Ready()
    {
        _connectionManager = GetNode<ConnectionManager>("/root/ConnectionManager");
        _connectionManager.Connection.PlayerChatEvent += OnPlayerChat;
    }

    private void OnPlayerChat(PlayerChat chat)
    {
        _logger.LogInformation("{}: {}", chat.TargetId, chat.Message);
    }
}