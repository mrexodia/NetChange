using System.IO;
using System.Net.Sockets;
using System.Threading;

using NetChange;

public class Connection
{
    public StreamReader Read;
    public StreamWriter Write;

    // Connection heeft 2 constructoren: deze constructor wordt gebruikt als wij CLIENT worden bij een andere SERVER
    public Connection(int port)
    {
        TcpClient client = new TcpClient(Program.Localhost, port);
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

            if (action == Program.ActionMydist)
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
            else if (action == Program.ActionForward)
            {
                int recipient = int.Parse(split[1]);
                var message = msg.Substring(msg.IndexOf(split[1]) + split[1].Length + 1);

                if (recipient == Program.MijnPoort)
                    Log.WriteLine(message);
                else
                    Program.ForwardMessage(recipient, message);
            }
            else if (action == Program.ActionDisconnect)
            {
                var port = int.Parse(split[1]);
                lock (Program.GlobalLock)
                {
                    foreach (var nb in Program.Du)
                        Program.SendMessage(port, Program.MydistFormat, Program.MijnPoort, nb.Key, 20);
                    lock (Program.NeighborLock)
                        Program.Neighbors.Remove(port);
                }
            }
            else
            {
                Log.WriteLine(msg);
            }
        }
    }
}