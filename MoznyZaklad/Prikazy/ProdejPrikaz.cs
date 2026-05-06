using MoznyZaklad.Svet;
using System;
using System.Linq;

namespace MoznyZaklad.Prikazy
{
    public class ProdejPrikaz : IPrikaz
    {
        public string Nazev => "prodej";
        public string Napoveda => "prodej <cislo> - proda predmet z inventare obchodnikovi (cislo z 'inventar')";

        public string Proved(Hrac hrac, string[] argumenty)
        {
            if (argumenty.Length < 2) return "Co chces prodat? Zadej cislo z inventare.";

            // 1. Je v mistnosti obchodnik?
            var obchodnik = hrac.AktualniMistnost.NpcPostavy.OfType<ObchodnikNPC>().FirstOrDefault();
            if (obchodnik == null) return "Neni tu nikdo, komu bys mohl neco prodat.";

            // 2. Prevod argumentu na index
            if (!int.TryParse(argumenty[1], out int index) || index < 1 || index > hrac.Inventar.Count)
            {
                return "Neplatne cislo predmetu. Podivej se do 'inventar'.";
            }

            // 3. Ziskani predmetu z inventare
            var predmet = hrac.Inventar[index - 1];

            // 4. Kontrola hodnoty predmetu (Opraveno na <= 0)
            if (predmet.Cena <= 0)
            {
                return $"O tento predmet ({predmet.Nazev}) nema {obchodnik.Jmeno} zajem, je bezcenny.";
            }

            // 5. Transakce
            int zisk = predmet.Cena;
            hrac.Penize += zisk;
            hrac.Inventar.Remove(predmet);

            return $"Prodal jsi {predmet.Nazev} obchodnikovi {obchodnik.Jmeno} za {zisk} zlataku. Aktualne mas: {hrac.Penize} zlataku.";
        }
    }
}