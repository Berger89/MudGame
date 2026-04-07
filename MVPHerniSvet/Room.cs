using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVPHerniSvet
{
    public class Room
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, Room> Exits { get; set; } = new Dictionary<string, Room>(StringComparer.OrdinalIgnoreCase);
        public List<Item> Items { get; set; } = new List<Item>();
        public List<Npc> Npcs { get; set; } = new List<Npc>();
        public List<Player> Players { get; set; } = new List<Player>(); // Hráči v této místnosti

        public Room(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}
