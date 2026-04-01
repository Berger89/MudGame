using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVPHerniSvet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    namespace MVPHerniSvet
    {
        public class MudGame
        {
            private int _port;
            private List<Room> _world = new List<Room>();
            private Room _startingRoom;

            public MudGame(int port)
            {
                _port = port;
                BuildWorld();
            }

            public async Task StartAsync()
            {
                TcpListener listener = new TcpListener(IPAddress.Any, _port);
                listener.Start();

                Console.WriteLine($"MUD Server běží na portu {_port}. Čekám na hráče...");

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    Console.WriteLine($"Nové připojení z {client.Client.RemoteEndPoint}");
                    _ = HandleClientAsync(client);
                }
            }
            private void BuildWorld()
            {
                // 1. Vytvoření místností
                var namesti = new Room("Náměstí", "Centrum města. Uprostřed stojí stará kamenná kašna.");
                var hospoda = new Room("Hospoda", "Vzduch je těžký kouřem a vůní piva. U stolů sedí pár štamgastů.");
                var ulicka = new Room("Temná ulička", "Úzká špinavá ulička, kde se drží podivná mlha.");
                var kovarna = new Room("Kovárna", "Všude je slyšet bušení kladiva do kovadliny. Sálá odtud horko.");
                var brana = new Room("Městská brána", "Mohutná brána, která odděluje město od divočiny.");
                var hrad = new Room("Hradní nádvoří", "Dlážděná plocha před královským palácem. Hlídají tu stráže.");
                var knihovna = new Room("Stará knihovna", "Tiché místo plné prachu a tisíců starých svazků knih.");
                var trh = new Room("Tržiště", "Hlučné místo, kde obchodníci vykřikují své nabídky.");

                // 2. Propojení (Exits)
                // Náměstí je uzel
                namesti.Exits.Add("sever", hrad);
                namesti.Exits.Add("jih", brana);
                namesti.Exits.Add("vychod", trh);
                namesti.Exits.Add("zapad", kovarna);

                // Ostatní směry
                trh.Exits.Add("zapad", namesti);
                trh.Exits.Add("sever", hospoda);
                trh.Exits.Add("jih", ulicka);

                hospoda.Exits.Add("jih", trh);
                ulicka.Exits.Add("sever", trh);

                kovarna.Exits.Add("vychod", namesti);
                kovarna.Exits.Add("sever", knihovna);

                knihovna.Exits.Add("jih", kovarna);

                hrad.Exits.Add("jih", namesti);
                brana.Exits.Add("sever", namesti);

                // 3. Přidání předmětů (jen do některých místností)
                namesti.Items.Add(new Item("mince", "Zlatá lesklá mince."));
                ulicka.Items.Add(new Item("dyka", "Ostrá zrezivělá dýka ležící v louži."));
                knihovna.Items.Add(new Item("pergamenu", "Starý svitek s nečitelným textem."));
                kovarna.Items.Add(new Item("kladivo", "Těžké kovářské kladivo."));

                // 4. Přidání NPC (jen do některých místností)
                hospoda.Npcs.Add(new Npc("hostinsky", "Dáš si pivo, nebo chceš slyšet nějaké drby?"));
                kovarna.Npcs.Add(new Npc("kovar", "Mám moc práce, jestli nic nepotřebuješ, nezdržuj."));
                brana.Npcs.Add(new Npc("strazny", "Ven tě pustit nemůžu, venku je nebezpečno."));
                trh.Npcs.Add(new Npc("kupec", "Kupte si čerstvá jablka! Jen za jednu minci!"));

                // 5. Registrace do seznamu světa
                _world.Add(namesti);
                _world.Add(hospoda);
                _world.Add(ulicka);
                _world.Add(kovarna);
                _world.Add(brana);
                _world.Add(hrad);
                _world.Add(knihovna);
                _world.Add(trh);

                // Nastavení startu
                _startingRoom = namesti;
            }

            private async Task HandleClientAsync(TcpClient tcpClient)
            {
                Player player = new Player { TcpClient = tcpClient };
                try
                {
                    NetworkStream stream = tcpClient.GetStream();
                    player.Reader = new StreamReader(stream, Encoding.UTF8);
                    player.Writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                    await player.SendAsync("Vítej v MUDu! Zadej své jméno:");
                    string name = await player.Reader.ReadLineAsync();

                    if (string.IsNullOrWhiteSpace(name)) return;

                    player.Name = name.Trim();
                    player.CurrentRoom = _startingRoom;

                    lock (player.CurrentRoom.Players) { player.CurrentRoom.Players.Add(player); }

                    Console.WriteLine($"Hráč {player.Name} vstoupil do hry.");
                    await LookAround(player);

                    while (true)
                    {
                        await player.SendAsync("\nCo uděláš? > ");
                        string input = await player.Reader.ReadLineAsync();
                        if (input == null) break;

                        input = input.Trim();
                        if (string.IsNullOrEmpty(input)) continue;

                        await ProcessCommand(player, input);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Chyba u klienta {player.Name}: {ex.Message}");
                }
                finally
                {
                    if (player.CurrentRoom != null)
                    {
                        lock (player.CurrentRoom.Players) { player.CurrentRoom.Players.Remove(player); }
                    }
                    tcpClient.Close();
                    Console.WriteLine($"Hráč {player.Name ?? "Neznámý"} se odpojil.");
                }
            }

            private async Task ProcessCommand(Player player, string input)
            {
                string[] parts = input.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                string command = parts[0].ToLower();
                string arg = parts.Length > 1 ? parts[1].ToLower() : "";

                switch (command)
                {
                    case "jdi": await MovePlayer(player, arg); break;
                    case "prozkoumej": await LookAround(player); break;
                    case "vezmi": await TakeItem(player, arg); break;
                    case "poloz": await DropItem(player, arg); break;
                    case "inventar": await ShowInventory(player); break;
                    case "mluv": await TalkToNpc(player, arg); break;
                    case "pomoc": await ShowHelp(player); break;
                    case "konec": player.TcpClient.Close(); break;
                    default: await player.SendAsync("Neznámý příkaz."); break;
                }
            }

          
            private async Task LookAround(Player player)
            {
                Room r = player.CurrentRoom;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"--- {r.Name} ---");
                sb.AppendLine(r.Description);
                sb.AppendLine($"Východy: {string.Join(", ", r.Exits.Keys)}");
                if (r.Items.Count > 0) sb.AppendLine($"Předměty: {string.Join(", ", r.Items.Select(i => i.Name))}");
                if (r.Npcs.Count > 0) sb.AppendLine($"Postavy: {string.Join(", ", r.Npcs.Select(n => n.Name))}");
                var others = r.Players.Where(p => p != player).Select(p => p.Name).ToList();
                if (others.Count > 0) sb.AppendLine($"Hráči: {string.Join(", ", others)}");
                await player.SendAsync(sb.ToString());
            }

            private async Task MovePlayer(Player player, string direction)
            {
                if (player.CurrentRoom.Exits.TryGetValue(direction, out Room nextRoom))
                {
                    lock (player.CurrentRoom.Players) { player.CurrentRoom.Players.Remove(player); }
                    player.CurrentRoom = nextRoom;
                    lock (player.CurrentRoom.Players) { player.CurrentRoom.Players.Add(player); }
                    await LookAround(player);
                }
                else await player.SendAsync("Tam se nedostaneš.");
            }

            private async Task TakeItem(Player player, string itemName)
            {
                Item item = player.CurrentRoom.Items.FirstOrDefault(i => i.Name.ToLower() == itemName);
                if (item != null && player.Inventory.Count < player.MaxInventoryCapacity)
                {
                    player.CurrentRoom.Items.Remove(item);
                    player.Inventory.Add(item);
                    await player.SendAsync($"Vzal jsi {item.Name}.");
                }
                else await player.SendAsync("To nejde vzít.");
            }

            private async Task DropItem(Player player, string itemName)
            {
                Item item = player.Inventory.FirstOrDefault(i => i.Name.ToLower() == itemName);
                if (item != null)
                {
                    player.Inventory.Remove(item);
                    player.CurrentRoom.Items.Add(item);
                    await player.SendAsync($"Položil jsi {item.Name}.");
                }
                else await player.SendAsync("To u sebe nemáš.");
            }

            private async Task ShowInventory(Player player)
            {
                await player.SendAsync($"Inventář ({player.Inventory.Count}/{player.MaxInventoryCapacity}): " +
                    string.Join(", ", player.Inventory.Select(i => i.Name)));
            }

            private async Task TalkToNpc(Player player, string npcName)
            {
                Npc npc = player.CurrentRoom.Npcs.FirstOrDefault(n => n.Name.ToLower() == npcName);
                if (npc != null) await player.SendAsync($"[{npc.Name}]: {npc.DialogText}");
                else await player.SendAsync("Nikdo takový tu není.");
            }

            private async Task ShowHelp(Player player)
            {
                string help = @"Dostupné příkazy:
 - jdi <směr>         : Přesune tě do jiné místnosti (např. jdi sever).
 - prozkoumej         : Rozhlédne se po aktuální místnosti.
 - vezmi <předmět>    : Sebere předmět z místnosti do inventáře.
 - poloz <předmět>    : Odloží předmět z inventáře do místnosti.
 - inventar           : Zobrazí tvé předměty a kapacitu.
 - mluv <jméno>       : Promluvíš si s NPC postavou.
 - pomoc              : Zobrazí tuto nápovědu.
 - konec              : Odpojí tě ze hry.";

                await player.SendAsync(help);
            }
        }
    }
}