using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetChange
{
    class Program
    {
        public static int MijnPoort;
        public static Dictionary<int, Connection> Neighbors = new Dictionary<int, Connection>();

        static void RoutingTable()
        {
            Console.WriteLine("RoutingTable()");
        }

        static void SendMessage(int port, string message)
        {
            Console.WriteLine("SendMessage({0}, \"{1}\")", port, message);
            Neighbors[port].Write.WriteLine(message);
        }

        static void Connect(int port)
        {
            Console.WriteLine("Connect({0})", port);
        }

        static void Disconnect(int port)
        {
            Console.WriteLine("Disonnect({0})", port);
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("FUCK YOU");
                return;
            }
            MijnPoort = int.Parse(args[0]);
            Console.Title = "Server: " + MijnPoort.ToString();
            new Server(MijnPoort);

            var bm = new int[args.Length - 1];
            for (var i = 0; i < bm.Length; i++)
            {
                var port = int.Parse(args[i + 1]);
                new Connection(port);
            }

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
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
