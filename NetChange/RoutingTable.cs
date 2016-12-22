using System;
using System.Linq;

namespace NetChange
{
    public static class RoutingTable
    {
        private static Tuple<int, int> MinimumNeighbor(int v)
        {
            var minimum = int.MaxValue;
            var prefNeighbor = -1;

            foreach (var w in Program.Neighbors.Keys)
            {
                var temp = Program.Ndisu[w, v];
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
            //Implemented from: http://www.cs.uu.nl/docs/vakken/b3cc/Prak/NetchangeBoek.pdf

            Log.WriteLine("// Recompute " + v);
            var u = Program.MijnPoort;
            var prevDistance = Program.Du[v];
            var prevNeighbor = Program.Nbu[v];
            var smartN = Program.Du.Count(x => x.Value != -1);

            Log.WriteLine("// Previous = " + prevDistance);
            if (u == v)
            {
                Program.Du[u] = 0;
                Program.Nbu[v] = u; // local
            }
            else
            {
                var minN = MinimumNeighbor(v);
                var w = minN.Item1;
                var d = minN.Item2 + 1;

                if (d < smartN)
                {
                    Program.Du[v] = d;
                    Program.Nbu[v] = w;
                }
                else
                {
                    Program.Du[v] = smartN;
                    Program.Nbu[v] = -1; // undefined
                }
            }

            if (Program.Du[v] != prevDistance || Program.Nbu[v] != prevNeighbor)
            {
                Log.WriteLine("// CHANGE {0} -> {1}", v, Program.Du[v]);
                if (Program.Nbu[v] != -1 && Program.Du[v] < prevDistance)
                {
                    Log.WriteLine("Afstand naar {0} is nu {1} via {2}", v, Program.Du[v], Program.Nbu[v]);
                }
                foreach (var x in Program.Neighbors.Keys)
                {
                    Program.SendMessage(x, Program.MydistFormat, u, v, Program.Du[v]);
                }
            }
            else
            {
                Log.WriteLine("// NO CHANGE {0}", v);

                if (Program.Du[v] >= smartN)
                    Log.WriteLine("Onbereikbaar: {0}", v);
            }
        }
    }
}