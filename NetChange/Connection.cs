using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace NetChange
{
    public class Connection
    {
        public StreamReader Read;
        public StreamWriter Write;

        // Connection has 2 constructors: this constructor is used if we become client to another server.
        public Connection(int port)
        {
            var client = new TcpClient("localhost", port);
            Read = new StreamReader(client.GetStream());
            Write = new StreamWriter(client.GetStream())
            {
                AutoFlush = true
            };

            // The server cannot see which port we are client of, we have to report this as part of the protocol.
            Write.WriteLine(Program.MijnPoort);

            // Start the reader loop.
            new Thread(ReaderLoop).Start();
        }

        // This constructor is used if we are server and a client connects to us.
        public Connection(StreamReader read, StreamWriter write)
        {
            Read = read;
            Write = write;

            // Start the reader loop.
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
                catch (SocketException) { } // Failed to connect
                Log.WriteLine("// SafeConnect({0}) failed, retrying...", port);
                Thread.Sleep(100);
            }
        }

        // This loop reads messages and takes appropriate action.
        private void ReaderLoop()
        {
            // Make sure the message loop only starts after the connection is added to the neighbors
            lock (Program.NeighborLock)
            {
            }

            while (true)
            {
                var msg = Read.ReadLine();
                Log.WriteLine("// " + msg);
                var split = msg.Split(' ');
                var action = split[0];

                switch (action)
                {
                    case Program.ActionMydist:
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
                    break;

                    case Program.ActionForward:
                    {
                        var recipient = int.Parse(split[1]);
                        var message = msg.Substring(msg.IndexOf(split[1]) + split[1].Length + 1);
                        Program.ForwardMessage(recipient, message);
                    }
                    break;

                    case Program.ActionDisconnect:
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
                    break;

                    default:
                    {
                        Log.WriteLine(msg);
                    }
                    break;
                }
            }
        }
    }
}