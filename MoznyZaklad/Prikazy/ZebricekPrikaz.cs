using MoznyZaklad.Data;
using MoznyZaklad.Svet;
using System.Text;

namespace MoznyZaklad.Prikazy
{
    public class ZebricekPrikaz : IPrikaz
    {
        public string Nazev => "zebricek";
        public string Napoveda => "zebricek - zobrazí žebříček nejbohatších hráčů ve hře";

        public string Proved(Hrac hrac, string[] argumenty)
        {
            // Zavolá správce výsledků, který už vyřeší online i offline hráče
            var nejbohatsi = SpravceVysledku.ZiskejNejbohatsi(10);

            if (nejbohatsi.Count == 0)
            {
                return "Žebříček je zatím prázdný. Nikdo nemá žádné peníze.";
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n  === TOP 10 NEJBOHATŠÍCH HRDINŮ === ");
            sb.AppendLine("-------------------------------------------");
            sb.AppendLine(string.Format("{0,-3} | {1,-15} | {2,-10}", "P.", "Jméno", "Zlaťáky"));
            sb.AppendLine("-------------------------------------------");

            for (int i = 0; i < nejbohatsi.Count; i++)
            {
                var hrdina = nejbohatsi[i];
                sb.AppendLine(string.Format("{0,-3} | {1,-15} | {2,-10}",
                    (i + 1) + ".",
                    hrdina.Jmeno,
                    hrdina.Penize));
            }
            sb.AppendLine("-------------------------------------------");

            return sb.ToString();
        }
    }
}