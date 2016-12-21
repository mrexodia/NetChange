using System;
using System.Collections.Generic;

using NetChange;

public class NDIS
{
    private static readonly Dictionary<Tuple<int, int>, int> _ndisu = new Dictionary<Tuple<int, int>, int>();

    public int this[int u, int v]
    {
        get
        {
            var key = new Tuple<int, int>(u, v);
            int val;
            if (_ndisu.TryGetValue(key, out val))
                return val;
            Log.WriteLine("// NDIS: unknown key ({0}, {1}) -> {2}", u, v, Program.N);
            return Program.N;
        }
        set
        {
            _ndisu[new Tuple<int, int>(u, v)] = value;
        }
    }
}