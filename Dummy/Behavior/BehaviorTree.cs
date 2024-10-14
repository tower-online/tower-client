namespace Tower.Dummy.Behavior;

public abstract class Node
{
    public enum TraverseResult
    {
        Success,
        Failure,
        Running
    }

    protected readonly List<Node> Children = [];

    public abstract TraverseResult Traverse();

    public void AddChild(Node child) => Children.Add(child);
}

public class ExecutionNode(Func<Node.TraverseResult> execute) : Node
{
    public override TraverseResult Traverse()
    {
        return execute();
    }
}

public class ConditionNode(Func<bool> check) : Node
{
    public override TraverseResult Traverse()
    {
        return check() ? Children[0].Traverse() : TraverseResult.Failure;
    }
}

public class FireConditionNode(Func<bool> check) : Node
{
    public override TraverseResult Traverse()
    {
        return check() ? Children[0].Traverse() : TraverseResult.Success;
    }
}

public class SequenceNode : Node
{
    public override TraverseResult Traverse()
    {
        foreach (var child in Children)
        {
            var result = child.Traverse();
            if (result == TraverseResult.Success) continue;
            return result;
        }

        return TraverseResult.Success;
    }
}

public class SelectorNode : Node
{
    public override TraverseResult Traverse()
    {
        foreach (var child in Children)
        {
            var result = child.Traverse();
            if (result == TraverseResult.Failure) continue;
            return result;
        }

        return TraverseResult.Failure;
    }
}

public class OddSelectorNode : Node
{
    public OddList<int> Indices { get; } = new();

    public override TraverseResult Traverse()
    {
        return Children[Indices.Pick()].Traverse();
    }
}

public class RandomSelectorNode : Node
{
    public override TraverseResult Traverse()
    {
        var i = Random.Shared.Next(0, Children.Count);
        return Children[i].Traverse();
    }
}

public class BehaviorTree(Node root)
{
    public Node Root { get; } = root;

    public void Run() => Root.Traverse();
}