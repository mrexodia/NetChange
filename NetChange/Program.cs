using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace NetChange
{
    public static class Log
    {
        public static void WriteLine(string format, params object[] args)
        {
            if (format.StartsWith("//"))
                return;
            Console.WriteLine(format, args);
        }
    }

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

                var subN = Program.Du.Where(gt => gt.Value != -1).Count();

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
                if (Program.Nbu[v] != -1)
                {
                    Log.WriteLine("Afstand naar {0} is nu {1} via {2}", v, Program.Du[v], Program.Nbu[v]);
                }
                foreach (var x in Program.Neighbors.Keys)
                {
                    Program.SendMessage(x, string.Format("mydist {0} {1} {2}", u, v, Program.Du[v]));
                }
            }
            else
            {
                Log.WriteLine("// NO CHANGE {0}", v);

                if (Program.Du[v] == Program.N)
                    Log.WriteLine("Onbereikbaar: {0}", v);
            }
        }
    }

    class Connection
    {
        // Deze loop leest wat er binnenkomt en print dit
        private void ReaderLoop()
        {
            lock (Program.NeighborLock)
            {
            }

            while (true)
            {
                string msg;
                try
                {
                    msg = Read.ReadLine();
                }
                catch (IOException)
                {
                    //TODO: exception disconnect
                    return;
                }
                Log.WriteLine("// " + msg);
                var split = msg.Split(' ');
                var action = split[0];

                if (action == "mydist")
                {
                    var sender = int.Parse(split[1]);
                    var recipient = int.Parse(split[2]);
                    var d = int.Parse(split[3]);

                    lock (Program.GlobalLock)
                    {
                        Program.Ndisu[sender, recipient] = d;
                        lock (Program.NeighborLock)
                        {
                            if (!Program.Du.ContainsKey(recipient))
                            {
                                Program.Du[recipient] = Program.N;
                                Program.Nbu[recipient] = -1;
                            }
                            RoutingTable.Recompute(recipient);
                        }
                    }
                }
                else if (action == "forward")
                {
                    int recipient = int.Parse(split[1]);
                    var message = msg.Substring(msg.IndexOf(split[1]) + split[1].Length + 1);

                    if (recipient == Program.MijnPoort)
                        Log.WriteLine(message);
                    else
                        Program.ForwardMessage(recipient, message);
                }
                else if (action == "disconnect")
                {
                    var port = int.Parse(split[1]);
                    lock (Program.GlobalLock)
                    {
                        foreach (var nb in Program.Du)
                        Program.SendMessage(port, "mydist {0} {1} {2}", Program.MijnPoort, nb.Key, 20);
                        lock(Program.NeighborLock)
                            Program.Neighbors.Remove(port);
                    }
                }
                else
                {
                    Log.WriteLine(msg);
                }
            }
        }

        public StreamReader Read;
        public StreamWriter Write;

        // Connection heeft 2 constructoren: deze constructor wordt gebruikt als wij CLIENT worden bij een andere SERVER
        public Connection(int port)
        {
            TcpClient client = new TcpClient("localhost", port);
            Read = new StreamReader(client.GetStream());
            Write = new StreamWriter(client.GetStream())
            {
                AutoFlush = true
            };

            // De server kan niet zien van welke poort wij client zijn, dit moeten we apart laten weten
            Write.WriteLine(Program.MijnPoort);

            // Start het reader-loopje
            new Thread(ReaderLoop).Start();
        }

        // Deze constructor wordt gebruikt als wij SERVER zijn en een CLIENT maakt met ons verbinding
        public Connection(StreamReader read, StreamWriter write)
        {
            Read = read;
            Write = write;

            // Start het reader-loopje
            new Thread(ReaderLoop).Start();
        }

        public static Connection SafeConnect(int port)
        {
            while (true)
            {
                try
                {
                    return new Connection(port);
                }
                catch (SocketException) { } // Kon niet verbinden
                Log.WriteLine("// SafeConnect({0}) failed, retrying...", port);
                Thread.Sleep(100);
            }
        }
    }

    class Server
    {
        public Server(int port)
        {
            // Luister op de opgegeven poort naar verbindingen
            TcpListener server = new TcpListener(IPAddress.Any, port);
            server.Start();

            // Start een aparte thread op die verbindingen aanneemt
            new Thread(() => AcceptLoop(server)).Start();
        }

        private static void AcceptLoop(TcpListener handle)
        {
            while (true)
            {
                TcpClient client = handle.AcceptTcpClient();
                StreamReader clientIn = new StreamReader(client.GetStream());
                StreamWriter clientOut = new StreamWriter(client.GetStream())
                {
                    AutoFlush = true
                };

                // De server weet niet wat de poort is van de client die verbinding maakt, de client geeft dus als onderdeel van het protocol als eerst een bericht met zijn poort
                int port = int.Parse(clientIn.ReadLine());

                Log.WriteLine("// Client maakt verbinding: " + port);

                // Zet de nieuwe verbinding in de verbindingslijst
                lock (Program.NeighborLock)
                {
                    var connection = new Connection(clientIn, clientOut);
                    Program.Neighbors.Add(port, connection);
                    foreach (var nb in Program.Du)
                        Program.SendMessage(port, "mydist {0} {1} {2}", Program.MijnPoort, nb.Key, nb.Value);
                    foreach (var n in Program.Neighbors.Keys)
                    {
                        if (n == port)
                            continue;
                        Program.SendMessage(n, string.Format("mydist {0} {1} {2}", Program.MijnPoort, port, 1));
                    }
                }
            }
        }
    }

    class Program
    {
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

        public static object GlobalLock = new object();
        public static object NeighborLock = new object();
        public static int MijnPoort;
        public static NeighborHelper Neighbors = new NeighborHelper();
        public static Dictionary<int, int> Du = new Dictionary<int, int>();
        public static Dictionary<int, int> Nbu = new Dictionary<int, int>();
        public static NDIS Ndisu = new NDIS();
        public const int N = 20;

        private static void InitializePort(int port)
        {
            foreach (var nb in Du)
                SendMessage(port, "mydist {0} {1} {2}", MijnPoort, nb.Key, nb.Value);
        }

        private static void RemovePort(int port)
        {
            foreach (var nb in Du)
                SendMessage(port, "mydist {0} {1} {2}", MijnPoort, nb.Key, 20);

            SendMessage(port, "disconnect {0}", MijnPoort);
        }

        public static void PrintRoutingTable()
        {
            lock (GlobalLock)
                foreach (var i in Nbu.Where(x => x.Value != -1).Select(v => string.Format("{0} {1} {2}", v.Key, Du[v.Key], v.Value == MijnPoort ? "local" : v.Value.ToString())).OrderBy(s => int.Parse(s.Split(' ')[0])))
                    Log.WriteLine(i);
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
                        Neighbors[dest].Write.WriteLine("forward {0} {1}", port, message);
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
                        RemovePort(port);
                        Neighbors.Remove(port);
                        Log.WriteLine("Verbroken: {0}", port);
                    }
                    else
                        Log.WriteLine("Poort {0} is niet bekend", port);
                }
            }
        }

        private static void Main(string[] args)
        {
            // Create server
            MijnPoort = int.Parse(args[0]);
            Console.Title = "NetChange " + MijnPoort.ToString();
            new Server(MijnPoort);

            Du[MijnPoort] = 0;
            Nbu[MijnPoort] = MijnPoort; // local

            for (var i = 0; i < args.Length - 1; i++)
            {
                var port = int.Parse(args[i + 1]);
                if (port < MijnPoort)
                {
                    lock (NeighborLock)
                    {
                        Neighbors.Add(port, Connection.SafeConnect(port));
                        InitializePort(port);
                    }
                }
            }

            // Read user input
            while (true)
            {
                string input = Console.ReadLine();
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
    }
}
