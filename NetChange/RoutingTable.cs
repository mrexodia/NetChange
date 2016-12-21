using System;
using System.Linq;

using NetChange;

public static class RoutingTable
{
    private static Tuple<int, int> minimumNeighbor(int v)
    {
        int minimum = int.MaxValue;
        int prefNeighbor = -1;

        foreach (var w in Program.Neighbors.Keys)
        {
            int temp = Program.Ndisu[w, v];
            if (temp < minimum)
            {
                minimum = temp;
                prefNeighbor = w;
            }
        }
        return new Tuple<int, int>(prefNeighbor, minimum);
    }

    public static void Recompute(int v)
    {
        Log.WriteLine("// Recompute " + v);
        int u = Program.MijnPoort;
        int prevDistance = Program.Du[v];
        int prevNeighbor = Program.Nbu[v];
        var subN = Program.Du.Where(gt => gt.Value != -1).Count();

        Log.WriteLine("// Previous = " + prevDistance);
        if (u == v)
        {
            Program.Du[u] = 0;
            Program.Nbu[v] = u; // local
        }
        else
        {
            var minN = minimumNeighbor(v);
            var w = minN.Item1;
            var d = minN.Item2 + 1;

            if (d < subN)
            {
                Program.Du[v] = d;
                Program.Nbu[v] = w;
            }
            else
            {
                Program.Du[v] = subN;
                Program.Nbu[v] = -1; //undefined
            }
        }
        if (Program.Du[v] != prevDistance || Program.Nbu[v] != prevNeighbor)
        {
            Log.WriteLine("// CHANGE {0} -> {1}", v, Program.Du[v]);
            if (Program.Nbu[v] != -1 && Program.Du[v] < prevDistance)
            {
                Log.WriteLine(Program.OutputDistance, v, Program.Du[v], Program.Nbu[v]);
            }
            foreach (var x in Program.Neighbors.Keys)
            {
                Program.SendMessage(x, Program.MydistFormat, u, v, Program.Du[v]);
            }
        }
        else
        {
            Log.WriteLine("// NO CHANGE {0}", v);

            if (Program.Du[v] >= subN)
                Log.WriteLine(Program.OutputUnreachable, v);
        }
    }
}