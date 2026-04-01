using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVPHerniSvet
{
    public class Npc
    {
        public string Name { get; set; }
        public string DialogText { get; set; }
        public Npc(string name, string dialog) { Name = name; DialogText = dialog; }
    }
}