using MoznyZaklad.Data;
using MoznyZaklad.Prikazy;
using MoznyZaklad.Svet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MoznyZaklad.Server
{
    public class MudServer
    {
        private readonly TcpListener myServer;
        private bool isRunning;
        private readonly HerniSvet herniSvet;
        private readonly PrikazovyProcesor procesor;

        // Seznam pro sledování online hráčů
        public static readonly List<Hrac> OnlineHraci = new();

        public MudServer(int port)
        {
            herniSvet = new HerniSvet();
            procesor = new PrikazovyProcesor();

            // --- REGISTRACE PŘÍKAZŮ ---
            procesor.Zaregistruj(new InventarPrikaz());
            procesor.Zaregistruj(new JdiPrikaz());
            procesor.Zaregistruj(new KrikPrikaz());
            procesor.Zaregistruj(new MluvPrikaz());
            procesor.Zaregistruj(new PolozPrikaz());
            procesor.Zaregistruj(new ProzkoumejPrikaz());
            procesor.Zaregistruj(new RekniPrikaz());
            procesor.Zaregistruj(new VezmiPrikaz());
            procesor.Zaregistruj(new ZautocPrikaz());
            procesor.Zaregistruj(new StatusPrikaz());
            procesor.Zaregistruj(new ProdejPrikaz());
            procesor.Zaregistruj(new KupPrikaz());
            procesor.Zaregistruj(new PouzijPrikaz());
            procesor.Zaregistruj(new NabidkaPrikaz());
            procesor.Zaregistruj(new ZebricekPrikaz());

            // Registrace nového příkazu pro achievementy
            procesor.Zaregistruj(new AchievmentyPrikaz());

            procesor.Zaregistruj(new PomocPrikaz(procesor.VsechnyPrikazy));

            myServer = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            myServer.Start();
            isRunning = true;
            Console.WriteLine($"[SERVER]: MUD spuštěn na portu {((IPEndPoint)myServer.LocalEndpoint).Port}");
            _ = ServerLoop();
        }

        public void Stop()
        {
            isRunning = false;
            myServer.Stop();
            Console.WriteLine("[SERVER]: MUD server byl ukončen.");
        }

        // --- UNIVERZÁLNÍ METODA PRO RESPAWN ---
        public static void NaplanujRespawn(Mistnost mistnost, NPC npc, int sekundy)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(sekundy));

                lock (mistnost.NpcPostavy)
                {
                    if (!mistnost.NpcPostavy.Contains(npc))
                    {
                        mistnost.NpcPostavy.Add(npc);
                    }
                }

                lock (mistnost.Hraci)
                {
                    foreach (var h in mistnost.Hraci)
                    {
                        _ = h.PosliZpravu($"\n[!] Do oblasti se právě vrátil: {npc.Jmeno}.");
                    }
                }
            });
        }

        private async Task ServerLoop()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = await myServer.AcceptTcpClientAsync();
                    _ = Task.Run(() => ClientLoop(client));
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"Chyba serveru: {ex.Message}");
                }
            }
        }

        private async Task ClientLoop(TcpClient client)
        {
            Hrac? hrac = null;

            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                {
                    UcetDto? nactenaData = null;

                    // --- 1. MENU: PŘIHLÁŠENÍ / REGISTRACE ---
                    while (nactenaData == null)
                    {
                        await writer.WriteLineAsync("\n--- MUD: KRAJOVÁ BRÁNA ---");
                        await writer.WriteLineAsync("[1] Přihlásit se");
                        await writer.WriteLineAsync("[2] Registrovat nového hrdinu");
                        await writer.WriteAsync("Vaše volba: ");

                        string? volba = (await reader.ReadLineAsync())?.Trim();
                        if (volba != "1" && volba != "2") continue;

                        await writer.WriteAsync("Jméno: ");
                        string? jmeno = (await reader.ReadLineAsync())?.Trim();
                        await writer.WriteAsync("Heslo: ");
                        string? heslo = (await reader.ReadLineAsync())?.Trim();

                        if (string.IsNullOrWhiteSpace(jmeno) || string.IsNullOrWhiteSpace(heslo))
                        {
                            await writer.WriteLineAsync("Chyba: Jméno i heslo musí být vyplněno!");
                            continue;
                        }

                        if (volba == "2") // Registrace
                        {
                            if (SpravceUctu.Registrovat(jmeno, heslo))
                            {
                                await writer.WriteLineAsync("Registrace úspěšná! Nyní se můžeš přihlásit.");
                                Logger.Log($"Nový hrdina: {jmeno}");
                            }
                            else await writer.WriteLineAsync("Chyba: Jméno je již obsazené.");
                        }
                        else // Přihlášení
                        {
                            nactenaData = SpravceUctu.Prihlasit(jmeno, heslo);
                            if (nactenaData == null)
                            {
                                await writer.WriteLineAsync("Neplatné jméno nebo heslo.");
                            }
                        }
                    }

                    // --- 2. KONTROLA DUPLICITNÍHO PŘIHLÁŠENÍ ---
                    lock (OnlineHraci)
                    {
                        if (OnlineHraci.Any(h => h.Jmeno.Equals(nactenaData.Jmeno, StringComparison.OrdinalIgnoreCase)))
                        {
                            writer.WriteLine("\nChyba: Tvůj hrdina už je ve světě přítomen!");
                            return;
                        }
                    }

                    // --- 3. OŽIVENÍ HRÁČE ---
                    Mistnost mistnost = herniSvet.NajdiMistnost(nactenaData.AktualniMistnostId);
                    hrac = new Hrac(mistnost, writer, herniSvet)
                    {
                        Jmeno = nactenaData.Jmeno,
                        Zivoty = nactenaData.Zivoty,
                        MaxZivoty = nactenaData.MaxZivoty,
                        Utok = nactenaData.Utok,
                        Obrana = nactenaData.Obrana,
                        Penize = nactenaData.Penize,
                        // NAČTENÍ ACHIEVEMENTŮ ZE SOUBORU
                        DosazeneUspechy = nactenaData.DosazeneUspechy ?? new List<string>()
                    };
                    hrac.NaplnInventar(nactenaData.InventarIds, herniSvet.VratVsechnyPredmety());

                    // Přidání do online seznamů
                    lock (OnlineHraci) OnlineHraci.Add(hrac);
                    lock (hrac.AktualniMistnost.Hraci)
                    {
                        foreach (var jiny in hrac.AktualniMistnost.Hraci)
                            _ = jiny.PosliZpravu($"[System]: {hrac.Jmeno} právě vstoupil do místnosti.");
                        hrac.AktualniMistnost.Hraci.Add(hrac);
                    }

                    Logger.Log($"Hráč se připojil: {hrac.Jmeno}");
                    await writer.WriteLineAsync($"\nVítej zpět, hrdino {hrac.Jmeno}!");
                    await writer.WriteLineAsync(hrac.AktualniMistnost.VratPopisMistnosti(hrac));

                    // --- 4. HLAVNÍ SMYČKA PŘÍKAZŮ ---
                    bool clientConnected = true;
                    while (clientConnected)
                    {
                        await writer.WriteAsync("\n> ");
                        string? vstup = (await reader.ReadLineAsync())?.Trim();
                        if (vstup == null) break;
                        if (string.IsNullOrWhiteSpace(vstup)) continue;

                        // Speciální zkratky pro souboj
                        bool vSouboji = ZautocPrikaz.AktivniSouboje.ContainsKey(hrac);
                        if (vSouboji && int.TryParse(vstup, out _)) vstup = "utoc " + vstup;

                        // Blokování ostatních akcí v boji
                        if (vSouboji && !vstup.StartsWith("utoc", StringComparison.OrdinalIgnoreCase))
                        {
                            await writer.WriteLineAsync("V boji se musíš soustředit! [1] Útok | [2] Batoh | [3] Útěk");
                            continue;
                        }

                        if (vstup.ToLower() == "konec") break;

                        // Zpracování příkazu
                        string odpoved = procesor.Zpracuj(hrac, vstup);

                        // --- KONTROLA ÚSPĚCHŮ (POZICE A STATY) ---
                        // Kontrolujeme po každém příkazu (pohyb, vypití lektvaru síly atd.)
                        await SpravceUspechu.Zkontroluj(hrac, "pohyb");

                        // --- LOGIKA VÍTĚZSTVÍ A RESPAWNU (Poražení Draka) ---
                        if (odpoved == "VYHRAL_JSI_KONEC")
                        {
                            var mistnostBitvy = hrac.AktualniMistnost;

                            // Najdeme instanci draka v seznamu postav
                            var drak = mistnostBitvy.NpcPostavy.FirstOrDefault(n => n.Jmeno.Contains("Drak", StringComparison.OrdinalIgnoreCase));

                            // Trigger pro achievement "Drakobijec" z Uspechy.json
                            await SpravceUspechu.Zkontroluj(hrac, "zabiti_npc", "Drak");

                            // Respawn logika
                            if (drak != null)
                            {
                                lock (mistnostBitvy.NpcPostavy)
                                {
                                    mistnostBitvy.NpcPostavy.Remove(drak);
                                }
                                NaplanujRespawn(mistnostBitvy, drak, 300); // 5 minut
                            }

                            await writer.WriteLineAsync("\n***************************************************");
                            await writer.WriteLineAsync("     GRATULUJEME! PORAZIL JSI STRAŠLIVÉHO DRAKA!    ");
                            await writer.WriteLineAsync("          STAL JSI SE LEGENDOU TOHOTO SVĚTA.        ");
                            await writer.WriteLineAsync("    Můžeš pokračovat dál ve svém dobrodružství!     ");
                            await writer.WriteLineAsync("***************************************************\n");
                        }
                        else if (!string.IsNullOrWhiteSpace(odpoved))
                        {
                            await writer.WriteLineAsync(odpoved);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Chyba u hráče {hrac?.Jmeno ?? "Neznámý"}: {ex.Message}");
            }
            finally
            {
                if (hrac != null)
                {
                    lock (hrac.AktualniMistnost.Hraci)
                    {
                        hrac.AktualniMistnost.Hraci.Remove(hrac);
                        foreach (var jiny in hrac.AktualniMistnost.Hraci)
                            _ = jiny.PosliZpravu($"[System]: {hrac.Jmeno} se rozplynul v mlze (odpojil se).");
                    }

                    lock (OnlineHraci) OnlineHraci.Remove(hrac);

                    SpravceUctu.UlozStavHrace(hrac);
                    Logger.Log($"Odpojen: {hrac.Jmeno}. Stav uložen.");
                }
            }
        }
    }
}