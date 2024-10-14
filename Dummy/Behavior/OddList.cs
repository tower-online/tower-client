namespace Tower.Dummy.Behavior;

public class OddList<T>
{
    private readonly List<T> _list = [];
    private readonly List<double> _odds = [];

    private readonly Random _rand = new Random(DateTimeOffset.Now.Nanosecond);
    
    public void Add(T value, double odd)
    {
        _list.Add(value);
        _odds.Add(odd);

        if (_odds.Sum() >= 1.0)
            throw new InvalidOperationException("Odds exceeding 1.0");
    }

    public T Pick()
    {
        var odd = _rand.NextDouble();
        var sum = 0.0;

        for (var i = 0; i < _list.Count; i += 1)
        {
            sum += _odds[i];
            if (sum >= odd) return _list[i];
        }

        return _list[^1];
    }
}