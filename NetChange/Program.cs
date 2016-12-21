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
    class Program
    {
        public const string Local = "local";
        public const string Localhost = "localhost";
        public const string NetChange = "NetChange ";

        public const string ActionMydist = "mydist";
        public const string ActionDisconnect = "disconnect";
        public const string ActionForward = "forward";

        public const string FormatLine = "{0} {1} {2}";
        public const string MydistFormat = ActionMydist + " " + FormatLine;
        public const string ForwardFormat = ActionForward + " {0} {1}";
        public const string DisconnectFormat = ActionDisconnect + " {0}";

        public const string OutputForwardFormat = "Bericht voor {0} doorgestuurd naar {1}";
        public const string OutputUnknown = "Poort {0} is niet bekend";
        public const string OutputConnected = "Verbonden: {0}";
        public const string OutputDisconnected = "Verbroken: {0}";
        public const string OutputUnreachable = "Onbereikbaar: {0}";
        public const string OutputDistance = "Afstand naar {0} is nu {1} via {2}";

        public const string InputRoutingTable = "R";
        public const string InputConnect = "C";
        public const string InputDisconnect = "D";
        public const string InputSendMessage = "B";

        public static object GlobalLock = new object();
        public static object NeighborLock = new object();

        public static int MijnPoort;
        public static NeighborHelper Neighbors = new NeighborHelper();
        public static Dictionary<int, int> Du = new Dictionary<int, int>();
        public static Dictionary<int, int> Nbu = new Dictionary<int, int>();
        public static NDIS Ndisu = new NDIS();
        public const int N = 20;


        private static void Main(string[] args)
        {
            // Create server
            MijnPoort = int.Parse(args[0]);
            Console.Title = NetChange + MijnPoort.ToString();
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
                    case InputRoutingTable:
                        PrintRoutingTable();
                        break;
                    case InputSendMessage:
                        ForwardMessage(int.Parse(split[1]), input.Substring(input.IndexOf(split[1]) + split[1].Length + 1));
                        break;
                    case InputConnect:
                        Connect(int.Parse(split[1]));
                        break;
                    case InputDisconnect:
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
            foreach (var node in Du)
            {
                SendMessage(port, MydistFormat, MijnPoort, node.Key, node.Value);
            }
        }

        private static void RemovePort(int port)
        {
            foreach (var node in Du)
            {
                SendMessage(port, MydistFormat, MijnPoort, node.Key, 20);
            }
            SendMessage(port, DisconnectFormat, MijnPoort);
        }

        public static void PrintRoutingTable()
        {
            lock (GlobalLock)
            {
                var subNetwork = Nbu.Where(x => x.Value != -1);
                var linePerNode = subNetwork.Select(v =>
                    string.Format(
                            FormatLine,
                            v.Key,
                            Du[v.Key],
                            isLocal(v)
                    ));
                var sortedLines = linePerNode.OrderBy(line => int.Parse(line.Split(' ')[0]));

                foreach (var i in sortedLines)
                {
                    Log.WriteLine(i);
                }
            }
        }
        private static string isLocal(KeyValuePair<int, int> v)
        {
            return v.Value == MijnPoort ? Local : v.Value.ToString();
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
                    Log.WriteLine(OutputForwardFormat, port, dest);
                    if (!Neighbors.ContainsKey(port))
                        Neighbors[dest].Write.WriteLine(ForwardFormat, port, message);
                    else
                        Neighbors[dest].Write.WriteLine(message);
                }
                else
                    Log.WriteLine(OutputUnknown, port);
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
            Log.WriteLine(OutputConnected, port);
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
                        Log.WriteLine(OutputDisconnected, port);
                    }
                    else
                        Log.WriteLine(OutputUnknown, port);
                }
            }
        }

    }
    public static class Log
    {
        public static void WriteLine(string format, params object[] args)
        {
            if (format.StartsWith("//"))
                return;
            Console.WriteLine(format, args);
        }
    }
}
