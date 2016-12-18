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
                Console.WriteLine("// CHANGE {0} -> {1}", v, Program.Du[v]);
                foreach (var x in Program.Neighbors.Keys)
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
                var action = split[0];

                if (action == "mydist" || action == "connect")
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

                            if (action == "connect")
                            {
                                var subNetwork = Program.Neighbors.Keys.ToList();
                                subNetwork.Add(Program.MijnPoort);

                                foreach (var port in subNetwork)
                                {
                                    Program.Ndisu[Program.MijnPoort, port] = Program.N;
                                    var message = string.Format("mydist {0} {1} {2}", Program.MijnPoort, port, Program.Du[port]);
                                    Program.SendMessage(sender, message);
                                }
                            }
                        }
                    }
                }
                else if (action == "forward")
                {
                    int recipient = int.Parse(split[1]);
                    var message = msg.Substring(msg.IndexOf(split[1]) + split[1].Length + 1);

                    if (recipient == Program.MijnPoort)
                        Console.WriteLine(message);
                    else
                        Program.ForwardMessage(recipient, message);
                }
                else
                {
                    Console.WriteLine(msg);
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
                    Program.Neighbors.Add(port, connection);
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
            private static readonly Dictionary<int, Connection> _neighbors = new Dictionary<int, Connection>();

            public Connection this[int u] { get { return _neighbors[u]; } }

            public void Add(int port, Connection connection)
            {
                if (ContainsKey(port))
                {
                    Console.WriteLine("// Neighbors already contains {0}", port);
                    throw new InvalidOperationException();
                }
                _neighbors[port] = connection;
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

        private static void Initialize()
        {
            var u = MijnPoort;
            foreach (var w in Neighbors.Keys)
            {
                Du[w] = N;
                Nbu[w] = -1; // undefined
            }
            Du[u] = 0;
            Nbu[u] = u; // local
            foreach (var w in Neighbors.Keys)
            {
                var message = string.Format("mydist {0} {0} 0", MijnPoort);
                SendMessage(w, message);
            }
        }
        private static void InitializePort(int port)
        {
            var message = string.Format("connect {0} {0} 0", MijnPoort);
            SendMessage(port, message);
        }
        private static void RemovePort(int port)
        {
            var message = string.Format("disconnect {0} {1} 0", MijnPoort, port);
            SendMessage(port, message);
        }

        public static void PrintRoutingTable()
        {
            lock (GlobalLock)
                foreach (var i in Nbu.Select(v => string.Format("{0} {1} {2}", v.Key, Du[v.Key], v.Value == MijnPoort ? "local" : v.Value.ToString())).OrderBy(s => int.Parse(s.Split(' ')[0])))
                    Console.WriteLine(i);
        }

        public static void SendMessage(int port, string message)
        {
            Console.WriteLine("// SendMessage({0}, \"{1}\")", port, message);
            lock (NeighborLock)
                Neighbors[port].Write.WriteLine(message);
        }

        public static void ForwardMessage(int port, string message)
        {
            Console.WriteLine("// SendMessage({0}, \"{1}\")", port, message);
            lock (NeighborLock)
            {
                int dest;
                if (Nbu.TryGetValue(port, out dest))
                {
                    Console.WriteLine("Bericht voor {0} doorgestuurd naar {1}", port, dest);
                    if (!Neighbors.ContainsKey(port))
                        Neighbors[dest].Write.WriteLine("forward {0} {1}", port, message);
                    else
                        Neighbors[dest].Write.WriteLine(message);
                }
                else
                    Console.WriteLine("Poort {0} is niet bekend", port);
            }
        }

        public static void Connect(int port)
        {
            Console.WriteLine("// Connect({0})", port);
            var connection = Connection.SafeConnect(port);
            lock (NeighborLock)
            {
                Neighbors.Add(port, connection);
                InitializePort(port);
            }
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
                            Neighbors.Add(port, Connection.SafeConnect(port));
                    }
                }

                //TODO: beter geen busywait
                while (true)
                {
                    lock (NeighborLock)
                        if (Neighbors.Count == args.Length - 1)
                            break;
                    Console.WriteLine("//Busywait");
                    Thread.Sleep(300);
                }

                lock (NeighborLock)
                    Initialize();
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
                        Console.WriteLine("// Command ongeldig!");
                        break;
                }
            }
        }
    }
}
