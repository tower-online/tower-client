using System.Numerics;

namespace Tower.Dummy;

public class DummyPlayer(uint entityId, uint clientId, string characterName)
    : PlayerBase(entityId, clientId, characterName)
{
    public enum State : int
    {
        Idle,
        Moving
    }
    
    public DateTime LastTransition { get; set; } = DateTime.Now;
    public TimeSpan TransitionStay { get; set; } = TimeSpan.Zero;
    
    public Vector3 TargetDirection { get; set; }
    public Vector3 Position { get; set; }
}