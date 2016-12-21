using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using NetChange;

public class Server
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
                var network = Program.Du;
                var neighbors = Program.Neighbors;

                foreach (var node in network)
                {
                    Program.SendMessage(port, Program.MydistFormat, Program.MijnPoort, node.Key, node.Value);
                }

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