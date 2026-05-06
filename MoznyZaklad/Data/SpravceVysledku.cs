using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MoznyZaklad.Server;

namespace MoznyZaklad.Data
{
    public class BohatyZaznam
    {
        public string Jmeno { get; set; } = "";
        public int Penize { get; set; }
    }

    public static class SpravceVysledku
    {
        // Používáme stejnou bezpečnou cestu jako u SpravceUctu, aby to vždy našlo složku Data
        private static readonly string SlozkaUctu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Data", "Ucty");

        public static List<BohatyZaznam> ZiskejNejbohatsi(int pocet = 10)
        {
            var vsechnyVysledky = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // 1. KROK: Načteme VŠECHNY soubory ze složky (pro offline hráče)
            if (Directory.Exists(SlozkaUctu))
            {
                foreach (var soubor in Directory.GetFiles(SlozkaUctu, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(soubor);
                        var data = JsonSerializer.Deserialize<UcetDto>(json);
                        if (data != null)
                        {
                            vsechnyVysledky[data.Jmeno] = data.Penize;
                        }
                    }
                    catch { /* poškozený soubor přeskočíme */ }
                }
            }

            // 2. KROK: Přepíšeme hodnoty těmi, co jsou zrovna ONLINE
            // Ti mají v RAM aktuálnější peníze než ty uložené v souboru
            lock (MudServer.OnlineHraci)
            {
                foreach (var hrac in MudServer.OnlineHraci)
                {
                    vsechnyVysledky[hrac.Jmeno] = hrac.Penize;
                }
            }

            // 3. KROK: Seřadíme to a vrátíme TOP 10
            return vsechnyVysledky
                .Select(x => new BohatyZaznam { Jmeno = x.Key, Penize = x.Value })
                .OrderByDescending(x => x.Penize)
                .Take(pocet)
                .ToList();
        }
    }
}