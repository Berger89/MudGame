using MoznyZaklad.Svet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoznyZaklad.Prikazy
{
    public class ProzkoumejPrikaz : IPrikaz
    {
        public string Nazev => "prozkoumej";
        public string Napoveda => "prozkoumej [cislo] - vypise popis mistnosti nebo konkretniho predmetu";

        public string Proved(Hrac hrac, string[] argumenty)
        {
            // 1. Bez argumentu vypíšeme popis místnosti
            if (argumenty.Length < 2)
            {
                return hrac.AktualniMistnost.VratPopisMistnosti(hrac);
            }

            // 2. Pokud hráč zadal číslo
            if (int.TryParse(argumenty[1], out int index))
            {
                // --- HLEDÁNÍ V MÍSTNOSTI ---
                if (index >= 1 && index <= hrac.AktualniMistnost.Predmety.Count)
                {
                    var predmet = hrac.AktualniMistnost.Predmety[index - 1];
                    return $"[V místnosti] {predmet.Nazev}: {predmet.Popis}";
                }

                // --- HLEDÁNÍ V INVENTÁŘI ---
                if (index >= 1 && index <= hrac.Inventar.Count)
                {
                    var predmet = hrac.Inventar[index - 1];
                    return $"[V inventáři] {predmet.Nazev}: {predmet.Popis}";
                }

                return "Predmet s timto cislem u sebe ani v okoli nevidis.";
            }

            return "Neplatny argument. Pouzij 'prozkoumej' pro mistnost nebo 'prozkoumej <cislo>' pro predmet.";
        }
    }
}