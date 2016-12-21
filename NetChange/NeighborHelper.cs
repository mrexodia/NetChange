using System;
using System.Linq;
using System.Collections.Generic;

using NetChange;

public class NeighborHelper
{
    private static readonly Dictionary<int, Connection> _neighbors = new Dictionary<int, Connection>();

    public Connection this[int u] { get { return _neighbors[u]; } }

    public void Add(int port, Connection connection)
    {
        if (ContainsKey(port))
        {
            Log.WriteLine("// Neighbors already contains {0}", port);
            throw new InvalidOperationException();
        }
        _neighbors[port] = connection;
    }
    public void Remove(int port)
    {
        if (!ContainsKey(port))
        {
            Log.WriteLine("// Port {0} is not contained in neighbors", port);
            throw new InvalidOperationException();
        }
        _neighbors.Remove(port);
    }

    public int[] Keys { get { return _neighbors.Keys.ToArray(); } }
    public int Count { get { return _neighbors.Count; } }

    public bool ContainsKey(int port)
    {
        return _neighbors.ContainsKey(port);
    }
}