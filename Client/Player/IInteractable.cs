namespace Tower.Player;

public interface IInteractable
{
    public void Interact();

    public string GetInteractionPrompt();
}