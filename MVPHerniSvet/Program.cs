using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;

namespace MVPHerniSvet
{
    using System;
    using System.Threading.Tasks;

    namespace MVPHerniSvet
    {
        internal class Program
        {
            static async Task Main(string[] args)
            {
              
                MudGame game = new MudGame(8888);

               
                await game.StartAsync();
            }
        }
    }
}