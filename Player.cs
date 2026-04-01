using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MVPHerniSvet
{
    public class Player
    {
        public string Name { get; set; }
        public Room CurrentRoom { get; set; }
        public List<Item> Inventory { get; set; } = new List<Item>();
        public int MaxInventoryCapacity { get; set; } = 3;

        public StreamWriter Writer { get; set; }
        public StreamReader Reader { get; set; }
        public TcpClient TcpClient { get; set; }

        public async Task SendAsync(string message)
        {
            try
            {
                // PuTTY (Telnet/Raw) očekává \r\n pro nový řádek
                await Writer.WriteAsync(message + "\r\n");
                await Writer.FlushAsync();
            }
            catch {  }
        }
    }
}