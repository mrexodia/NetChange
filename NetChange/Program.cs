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
    public static class RoutingTable
    {
        private static Tuple<int, int> minimumNeighbor(int v)
        {
            int minimum = int.MaxValue;
            int prefNeighbor = -1;

            foreach (var w in Program.Neigbors.Keys)
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
            Console.WriteLine("// Recompute " + v);
            int u = Program.MijnPoort;
            int prev = Program.Du[v];
            Console.WriteLine("// Previous = " + prev);
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

                if (d < Program.N)
                {
                    Program.Du[v] = d;
                    Program.Nbu[v] = w;
                }
                else
                {
                    Program.Du[v] = Program.N;
                    Program.Nbu[v] = -1; //undefined
                }
            }
            if (Program.Du[v] != prev)
            {
                Console.WriteLine("// CHANGE {0}", v);
                foreach (var x in Program.Neigbors.Keys)
                {
                    Program.SendMessage(x, string.Format("mydist {0} {1} {2}", u, v, Program.Du[v]));
                }
            }
            else
            {
                Console.WriteLine("// NO CHANGE {0}", v);
            }
        }
    }

    class Connection
    {
        // Deze loop leest wat er binnenkomt en print dit
        private void ReaderLoop()
        {
            while (true)
            {
                var msg = Read.ReadLine();
                Console.WriteLine("// " + msg);
                var split = msg.Split(' ');
                if (split[0] == "mydist")
                {
                    var w = int.Parse(split[1]);
                    var v = int.Parse(split[2]);
                    var d = int.Parse(split[3]);

                    lock (Program.GlobalLock)
                    {
                        Program.Ndisu[w, v] = d;
                        lock (Program.NeighborLock)
                        {
                            if (!Program.Du.ContainsKey(v))
                            {
                                Program.Du[v] = Program.N;
                                Program.Nbu[v] = -1;
                            }

                            RoutingTable.Recompute(v);
                        }
                    }
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
                Console.WriteLine("// SafeConnect({0}) failed, retrying...", port);
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

                Console.WriteLine("// Client maakt verbinding: " + port);

                // Zet de nieuwe verbinding in de verbindingslijst
                var connection = new Connection(clientIn, clientOut);
                lock (Program.NeighborLock)
                    Program.Neigbors.Add(port, connection);
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
                    Console.WriteLine("// NDIS: unknown key ({0}, {1}) -> {2}", u, v, N);
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
            private static readonly Dictionary<int, Connection> _neigbors = new Dictionary<int, Connection>();

            public Connection this[int u] { get { return _neigbors[u]; } }

            public void Add(int port, Connection connection)
            {
                if (_neigbors.ContainsKey(port))
                {
                    Console.WriteLine("// Neighbors already contains {0}", port);
                    throw new InvalidOperationException();
                }
                _neigbors[port] = connection;
            }

            public int[] Keys { get { return _neigbors.Keys.ToArray(); } }
            public int Count { get { return _neigbors.Count; } }
        }


        public static object GlobalLock = new object();
        public static object NeighborLock = new object();
        public static int MijnPoort;
        public static NeighborHelper Neigbors = new NeighborHelper();
        public static Dictionary<int, int> Du = new Dictionary<int, int>();
        public static Dictionary<int, int> Nbu = new Dictionary<int, int>();
        public static NDIS Ndisu = new NDIS();
        public const int N = 20;

        private static void Initialize()
        {
            var u = MijnPoort;
            foreach (var w in Neigbors.Keys)
            {
                Du[w] = N;
                Nbu[w] = -1; // undefined
            }
            Du[u] = 0;
            Nbu[u] = u; // local
            foreach (var w in Neigbors.Keys)
            {
                var message = string.Format("mydist {0} {0} 0", u);
                SendMessage(w, message);
            }
        }

        public static void RoutingTable()
        {
            lock (GlobalLock)
                foreach (var i in Nbu.Select(v => string.Format("{0} {1} {2}", v.Key, Du[v.Key], v.Value == MijnPoort ? "local" : v.Value.ToString())).OrderBy(s => int.Parse(s.Split(' ')[0])))
                    Console.WriteLine(i);
        }

        public static void SendMessage(int port, string message)
        {
            Console.WriteLine("// SendMessage({0}, \"{1}\")", port, message);
            lock (NeighborLock)
                Neigbors[port].Write.WriteLine(message);
        }

        public static void Connect(int port)
        {
            Console.WriteLine("// Connect({0})", port);
            var connection = Connection.SafeConnect(port);
            lock (NeighborLock)
                Neigbors.Add(port, connection);
        }

        public static void Disconnect(int port)
        {
            Console.WriteLine("// Disonnect({0})", port);
        }

        private static void Main(string[] args)
        {
            // Create server
            MijnPoort = int.Parse(args[0]);
            Console.Title = "NetChange " + MijnPoort.ToString();
            new Server(MijnPoort);

            lock (GlobalLock)
            {
                // Connect to the neighbors
                for (var i = 0; i < args.Length - 1; i++)
                {
                    var port = int.Parse(args[i + 1]);
                    if (port > MijnPoort)
                    {
                        lock (NeighborLock)
                            Neigbors.Add(port, Connection.SafeConnect(port));
                    }
                }

                //TODO: beter geen busywait
                while (true)
                {
                    lock (NeighborLock)
                        if (Neigbors.Count == args.Length - 1)
                            break;
                    Console.WriteLine("//Busywait");
                    Thread.Sleep(300);
                }

                lock (NeighborLock)
                    Initialize();
                Console.WriteLine("// Done initializing process {0}", MijnPoort);
            }

            // Read user input
            while (true)
            {
                string input = Console.ReadLine();
                var split = input.Split(' ');
                switch (split[0])
                {
                    case "R":
                        RoutingTable();
                        break;
                    case "B":
                        SendMessage(int.Parse(split[1]), input.Substring(input.IndexOf(split[1]) + split[1].Length + 1));
                        break;
                    case "C":
                        Connect(int.Parse(split[1]));
                        break;
                    case "D":
                        Disconnect(int.Parse(split[1]));
                        break;
                    default:
                        Console.WriteLine("// Command ongeldig!");
                        break;
                }
            }
        }
    }
}
