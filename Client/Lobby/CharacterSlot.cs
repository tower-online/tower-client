using Godot;

namespace Tower.Lobby;

public partial class CharacterSlot : Node
{
    [Signal]
    public delegate void StartButtonPressedEventHandler(string characterName);
    
    public Label CharacterName { get; private set; }
    
    private Button StartButton { get; set; }
    
    public override void _Ready()
    {
        CharacterName = GetNode<Label>("CharacterName");
        StartButton = GetNode<Button>("StartButton");

        StartButton.Pressed += () => EmitSignal(SignalName.StartButtonPressed, CharacterName.Text);
    }
}