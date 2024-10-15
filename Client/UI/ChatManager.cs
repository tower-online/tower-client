using Godot;
using Microsoft.Extensions.Logging;
using tower.network.packet;

namespace Tower.System;

public partial class ChatManager : Node
{
    private ConnectionManager _connectionManager;

    private readonly ILogger _logger = new GodotLogger(nameof(ChatManager));

    private Control _chatBoxes;
    private static readonly PackedScene ChatBoxScene = GD.Load<PackedScene>("res://UI/ChatBox.tscn");
    
    public override void _Ready()
    {
        _connectionManager = GetNode<ConnectionManager>("/root/ConnectionManager");
        _connectionManager.Connection.PlayerChatEvent += OnPlayerChat;

        _chatBoxes = GetNode<Control>("ChatBoxes");
    }

    private void OnPlayerChat(PlayerChat chat)
    {
        var chatBox = ChatBoxScene.Instantiate<Label>();
        chatBox.Text = $"{chat.Sender}: {chat.Message}";

        var target = chat.Target;
        
        _chatBoxes.AddChild(chatBox);

        if (_chatBoxes.GetChildCount() < 10) return;
        var oldChatBox = _chatBoxes.GetChild(0);
        _chatBoxes.RemoveChild(oldChatBox);
        oldChatBox.QueueFree();
    }
}