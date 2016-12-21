using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetChange
{
    public class Server
    {
        public Server(int port)
        {
            // Listen for new connections.
            var server = new TcpListener(IPAddress.Any, port);
            server.Start();

            // Start a separate thread that takes new connections.
            new Thread(() => AcceptLoop(server)).Start();
        }

        private static void AcceptLoop(TcpListener handle)
        {
            while (true)
            {
                var client = handle.AcceptTcpClient();
                var clientIn = new StreamReader(client.GetStream());
                var clientOut = new StreamWriter(client.GetStream())
                {
                    AutoFlush = true
                };

                // The server doesn't know the port of the client and the client will send this as part of the protocol.
                var port = int.Parse(clientIn.ReadLine());

                Log.WriteLine("// Client maakt verbinding: " + port);

                // Put the new connection in our neighbors.
                lock (Program.NeighborLock)
                {
                    var connection = new Connection(clientIn, clientOut);
                    Program.Neighbors.Add(port, connection);
                    var network = Program.Du;
                    var neighbors = Program.Neighbors;

                    // Send the new client our (full) routing table.
                    foreach (var node in network)
                    {
                        Program.SendMessage(port, Program.MydistFormat, Program.MijnPoort, node.Key, node.Value);
                    }

                    // Notify our (old) direct neighbors that a new neighbor lives next door.
                    foreach (var neighbor in neighbors.Keys)
                    {
                        if (neighbor == port)
                            continue;
                        Program.SendMessage(neighbor, Program.MydistFormat, Program.MijnPoort, port, 1);
                    }
                }
            }
        }
    }
}