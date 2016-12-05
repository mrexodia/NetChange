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
                    Console.WriteLine(Read.ReadLine());
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
                lock (Program.Neighbors)
                    Program.Neighbors.Add(port, connection);
            }
        }
    }

    class Program
    {
        public static int MijnPoort;
        public static Dictionary<int, Connection> Neighbors = new Dictionary<int, Connection>();

        private static void RoutingTable()
        {
            Console.WriteLine("// RoutingTable()");
        }

        private static void SendMessage(int port, string message)
        {
            Console.WriteLine("// SendMessage({0}, \"{1}\")", port, message);
            lock (Neighbors)
                Neighbors[port].Write.WriteLine(message);
        }

        private static void Connect(int port)
        {
            Console.WriteLine("// Connect({0})", port);
            var connection = Connection.SafeConnect(port);
            lock (Neighbors)
                Neighbors[port] = connection;
        }

        private static void Disconnect(int port)
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
                        foreach (var port in Neighbors.Keys)
                            Console.WriteLine("// {0}", port);
                        break;
#endif
                }
            }
        }
    }
}
