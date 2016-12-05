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

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("FUCK YOU");
                return;
            }
            MijnPoort = int.Parse(args[0]);
            new Server(MijnPoort);

            var bm = new int[args.Length - 1];
            for (var i = 0; i < bm.Length; i++)
            {
                var port = int.Parse(args[i + 1]);
                Neighbors[port] = new Connection(port);
            }

            while (true)
            {
                string input = Console.ReadLine();
                var split = input.Split(' ');
                switch (split[0])
                {
                    case "R":
                        {
                        }
                        break;

                    case "B":
                        {
                        }
                        break;

                    case "C":
                        {
                        }
                        break;

                    case "D":
                        {
                        }
                        break;
                }
                if (input.StartsWith("verbind"))
                {
                    int poort = int.Parse(input.Split()[1]);
                    if (Neighbors.ContainsKey(poort))
                        Console.WriteLine("Hier is al verbinding naar!");
                    else
                    {
                        // Leg verbinding aan (als client)
                        Neighbors.Add(poort, new Connection(poort));
                    }
                }
                else
                {
                    // Stuur berichtje
                    string[] delen = input.Split(new char[] { ' ' }, 2);
                    int poort = int.Parse(delen[0]);
                    if (!Neighbors.ContainsKey(poort))
                        Console.WriteLine("Hier is al verbinding naar!");
                    else
                        Neighbors[poort].Write.WriteLine(MijnPoort + ": " + delen[1]);
                }
            }
        }
    }
}
