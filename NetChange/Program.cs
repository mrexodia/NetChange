using System;
using System.Collections.Generic;
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
        public static void Recompute(int v)
        {
            Console.WriteLine("// Recompute({0})", v);
            int u = Program.MijnPoort;
            int prev = Program.Du[v];
            if (u == v)
            {
                Console.WriteLine("//{0} u == v FUCKU", v);
                Program.Du[u] = 0;
                Program.Nbu[v] = u; // local
            }
            else
            {
                Console.WriteLine("NEIN");
                var minN = minimumNeighbor(v);
                var w = minN.Item1;
                var d = minN.Item2 + 1;
                Console.WriteLine("//{0} minN: {1},{2}", v, minN.Item1, minN.Item2);

                if (d < 20)
                {
                    Console.WriteLine("//{0} d<20", v);
                    Program.Du[v] = d;
                    Program.Nbu[v] = w;
                }
                else
                {
                    Console.WriteLine("//{0} else", v);
                    Program.Du[v] = 20;
                    Program.Nbu[v] = -1; //undefined
                }
            }
            if (Program.Du[v] != prev)
            {
                Console.WriteLine("//{0} YES CHANGE", v);
                foreach (var x in Program.Neighu.Keys)
                    Program.SendMessage(x, string.Format("mydist {0} {1} {2}", u, v, Program.Du[v]));
            }
            else
            {
                Console.WriteLine("//{0} NO CHANGE", v);
            }
        }

        private static Tuple<int, int> minimumNeighbor(int v)
        {
            int minimum = int.MaxValue;
            int prefNeighbor = -1;

            foreach (var w in Program.Neighu.Keys)
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
    }

    class Connection
    {
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
                catch { } // Kon niet verbinden
                Thread.Sleep(100);
            }
        }

        // Deze loop leest wat er binnenkomt en print dit
        private void ReaderLoop()
        {
            try
            {
                while (true)
                {
                    var msg = Read.ReadLine();
                    Console.WriteLine("// Message: " + msg);
                    var split = msg.Split(' ');
                    if (split[0] == "mydist")
                    {
                        var w = int.Parse(split[1]);
                        var v = int.Parse(split[2]);
                        var d = int.Parse(split[3]);
                        Program.Ndisu[v, w] = d;
                        RoutingTable.Recompute(v);
                    }
                }
            }
            catch { } // Verbinding is kennelijk verbroken
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
                lock (Program.Neighu)
                    Program.Neighu.Add(port, connection);
            }
        }
    }

    class Program
    {
        public class NDIS
        {
            public static Dictionary<Tuple<int, int>, int> Ndisu = new Dictionary<Tuple<int, int>, int>();

            public int this[int u, int v]
            {
                get { return Ndisu[new Tuple<int, int>(u, v)]; }
                set { Ndisu[new Tuple<int, int>(u, v)] = value; }
            }
        }

        public static int MijnPoort;
        public static Dictionary<int, Connection> Neighu = new Dictionary<int, Connection>();
        public static Dictionary<int, int> Du = new Dictionary<int, int>();
        public static Dictionary<int, int> Nbu = new Dictionary<int, int>();
        public static NDIS Ndisu = new NDIS();

        private static void Initialize()
        {
            var u = Program.MijnPoort;
            foreach (var w in Neighu)
            {
                Ndisu[w.Key, u] = 20;
            }
            foreach (var v in Neighu)
            {
                Du[v.Key] = 20;
                Nbu[v.Key] = -1; // undefined
            }
            Du[u] = 0;
            Nbu[u] = u; // local
            foreach (var w in Neighu)
            {
                var message = string.Format("mydist {0} {0} 0", u);
                SendMessage(w.Key, message);
            }
        }

        public static void RoutingTable()
        {
            Console.WriteLine("// RoutingTable()");
        }

        public static void SendMessage(int port, string message)
        {
            Console.WriteLine("// SendMessage({0}, \"{1}\")", port, message);
            lock (Neighu)
                Neighu[port].Write.WriteLine(message);
        }

        public static void Connect(int port)
        {
            Console.WriteLine("// Connect({0})", port);
            var connection = Connection.SafeConnect(port);
            lock (Neighu)
                Neighu[port] = connection;
        }

        public static void Disconnect(int port)
        {
            Console.WriteLine("// Disonnect({0})", port);
        }

        private static void Main(string[] args)
        {
            // Create server
            MijnPoort = int.Parse(args[0]);
            Console.Title = "Server: " + MijnPoort.ToString();
            new Server(MijnPoort);
            
            // Connect to the neighbors
            for (var i = 0; i < args.Length - 1; i++)
            {
                var port = int.Parse(args[i + 1]);
                new Thread(() => Connection.SafeConnect(port)).Start();
            }

            Console.WriteLine("// Iedereen gaat je moeder nemen");

            //TODO: beter geen busywait
            while (true)
            {
                lock (Neighu)
                    if (Neighu.Count == args.Length - 1)
                        break;
                Thread.Sleep(10);
            }

            Console.WriteLine("// Iedereen heeft je moeder genomen");

            Initialize();

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
#if DEBUG
                    case "N":
                        foreach (var port in Neighu.Keys)
                            Console.WriteLine("// {0}", port);
                        break;
#endif
                }
            }
        }
    }
}
