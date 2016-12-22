using System;
using System.Collections.Generic;
using System.Linq;

namespace NetChange
{
    public static class Log
    {
        public static void WriteLine(string format, params object[] args)
        {
            if (format.StartsWith("//")) //comment to show debug statements
                return;
            Console.WriteLine(format, args);
        }
    }

    internal class Program
    {
        public const string ActionMydist = "mydist";
        public const string ActionDisconnect = "disconnect";
        public const string ActionForward = "forward";

        public const string MydistFormat = ActionMydist + " {0} {1} {2}";
        public const string ForwardFormat = ActionForward + " {0} {1}";
        public const string DisconnectFormat = ActionDisconnect + " {0}";

        public static object GlobalLock = new object();
        public static object NeighborLock = new object();

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
                    Log.WriteLine("// NDIS: unknown key ({0}, {1}) -> {2}", u, v, N);
                    return N;
                }
                set
                {
                    _ndisu[new Tuple<int, int>(u, v)] = value;
                }
            }
        }

        public static int MijnPoort;
        public static Dictionary<int, Connection> Neighbors = new Dictionary<int, Connection>();
        public static Dictionary<int, int> Du = new Dictionary<int, int>();
        public static Dictionary<int, int> Nbu = new Dictionary<int, int>();
        public static NDIS Ndisu = new NDIS();
        public const int N = 20;

        private static void Main(string[] args)
        {
            // Create server
            MijnPoort = int.Parse(args[0]);
            Console.Title = "NetChange" + MijnPoort;
            new Server(MijnPoort);

            lock (GlobalLock)
            {
                Du[MijnPoort] = 0;
                Nbu[MijnPoort] = MijnPoort; // local
            }

            for (var i = 0; i < args.Length - 1; i++)
            {
                var port = int.Parse(args[i + 1]);
                if (port > MijnPoort)
                    continue;
                lock (NeighborLock)
                {
                    Neighbors.Add(port, Connection.SafeConnect(port));
                    InitializePort(port);
                }
            }

            // Read user input
            while (true)
            {
                var input = Console.ReadLine();
                var split = input.Split(' ');
                switch (split[0])
                {
                    case "R":
                        PrintRoutingTable();
                        break;
                    case "B":
                        ForwardMessage(int.Parse(split[1]), input.Substring(input.IndexOf(split[1]) + split[1].Length + 1));
                        break;
                    case "C":
                        Connect(int.Parse(split[1]));
                        break;
                    case "D":
                        Disconnect(int.Parse(split[1]));
                        break;
                    default:
                        Log.WriteLine("// Command ongeldig!");
                        break;
                }
            }
        }

        private static void InitializePort(int port)
        {
            // Send the new neighbor (port) our (full) routing table.
            foreach (var node in Du)
            {
                SendMessage(port, MydistFormat, MijnPoort, node.Key, node.Value);
            }
        }

        public static void RemovePort(int port)
        {
            Neighbors.Remove(port);

            // Save the ports of the current routing table to prevent changes while recomputing
            var duk = Du.Keys.ToArray();

            // Recompute every port in our routing table
            foreach (var d in duk)
            {
                RoutingTable.Recompute(d);
            }
        }

        public static void PrintRoutingTable()
        {
            lock (GlobalLock)
            {
                var subNetwork = Nbu.Where(x => x.Value != -1);
                var linePerNode = subNetwork.Select(v =>
                    string.Format(
                            "{0} {1} {2}",
                            v.Key,
                            Du[v.Key],
                            v.Value == MijnPoort ? "local" : v.Value.ToString()
                    ));
                var sortedLines = linePerNode.OrderBy(line => int.Parse(line.Split(' ')[0]));

                foreach (var i in sortedLines)
                {
                    Log.WriteLine(i);
                }
            }
        }

        public static void SendMessage(int port, string message, params object[] args)
        {
            message = string.Format(message, args);
            Log.WriteLine("// SendMessage({0}, \"{1}\")", port, message);
            lock (NeighborLock)
                Neighbors[port].Write.WriteLine(message);
        }

        public static void ForwardMessage(int port, string message)
        {
            Log.WriteLine("// SendMessage({0}, \"{1}\")", port, message);
            lock (NeighborLock)
            {
                int dest;
                if (Nbu.TryGetValue(port, out dest) && Nbu[port] != -1)
                {
                    Log.WriteLine("Bericht voor {0} doorgestuurd naar {1}", port, dest);
                    if (!Neighbors.ContainsKey(port))
                        Neighbors[dest].Write.WriteLine(ForwardFormat, port, message);
                    else
                        Neighbors[dest].Write.WriteLine(message);
                }
                else
                    Log.WriteLine("Poort {0} is niet bekend", port);
            }
        }

        public static void Connect(int port)
        {
            Log.WriteLine("// Connect({0})", port);
            lock (NeighborLock)
            {
                var connection = Connection.SafeConnect(port);
                Neighbors.Add(port, connection);
                InitializePort(port);
            }
            Log.WriteLine("Verbonden: {0}", port);
        }

        public static void Disconnect(int port)
        {
            Log.WriteLine("// Disconnect({0})", port);

            lock (GlobalLock)
            {
                lock (NeighborLock)
                {
                    if (Neighbors.ContainsKey(port))
                    {
                        SendMessage(port, DisconnectFormat, MijnPoort);
                        RemovePort(port);

                        Log.WriteLine("Verbroken: {0}", port);
                    }
                    else
                        Log.WriteLine("Poort {0} is niet bekend", port);
                }
            }
        }
    }
}
