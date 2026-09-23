using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniWar.Online;
using MiniWar.Server;

int checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception("FAILED: " + label); checks++; Console.WriteLine("PASS " + label); }
string root = Path.Combine(Path.GetTempPath(), "MiniWarTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string password = "Test-only-Password!234";
try
{
    var store = new AccountStore(root);
    var registered = store.Authenticate("테스터", password, true);
    Check(registered.Profile?.money == 300 && registered.Profile.items.Length == 2, "New account receives exactly one starter pistol and melee weapon");
    string itemId = registered.Profile!.items[0].id;
    Check(store.Authenticate("테스터", password, true).Profile == null, "Duplicate registration rejected");
    Check(store.Authenticate("../evil", password, true).Profile == null, "Invalid nickname rejected");
    Check(store.Authenticate("short", "a", true).Profile == null, "Short password rejected");
    Check(store.Authenticate("테스터", "incorrect", false).Profile == null, "Wrong password rejected");
    Check(!File.ReadAllText(Path.Combine(root, "accounts.json")).Contains(password), "Password is never saved in plaintext");
    store = new AccountStore(root);
    Check(store.Authenticate("테스터", password, false).Profile?.items[0].id == itemId, "Account and item identity survive store restart");
    Check(store.Authenticate("Alpha", password, true).Profile != null && store.Authenticate("alpha", password, true).Profile == null, "Case-insensitive unique nicknames");
    Check(store.Authenticate("InvalidBody", password, true, 9).Profile == null, "Invalid character selection rejected");
    var female = store.Authenticate("Female", password, true, (int)CharacterBody.Female).Profile!;
    Check(female.body == 1 && female.activeItemId == female.items[0].id, "Female selection saved with starter pistol equipped");
    Check(store.Authenticate("Female", password, false, 0).Profile!.body == 1, "Login cannot overwrite character selection");
    Check(store.Equip("Female", itemId).Profile == null, "Cannot equip another account's weapon");
    Check(store.Equip("Female", female.items[1].id).Profile?.activeItemId == female.items[1].id, "Owned melee can replace the held pistol");
    store = new AccountStore(root);
    Check(store.Authenticate("Female", password, false).Profile!.activeItemId == female.items[1].id, "Equipped weapon persists after restart");
    // Seed only this isolated test save: item acquisition gameplay is not part of the lobby slice.
    string accountPath = Path.Combine(root, "accounts.json");
    var fixture = JsonSerializer.Deserialize<AccountDatabase>(File.ReadAllText(accountPath), AccountStore.Json)!;
    var savedFemale = fixture.Accounts.Single(a => a.Nickname == "Female").Profile;
    savedFemale.items = savedFemale.items.Concat(new[] {
        new LanItem { id = "pistol-tier2", family = (int)WeaponFamily.Pistol, tier = 2 },
        new LanItem { id = "pistol-tier3", family = (int)WeaponFamily.Pistol, tier = 3 },
        new LanItem { id = "revolver-tier1", family = (int)WeaponFamily.Revolver, tier = 1 },
        new LanItem { id = "revolver-tier2", family = (int)WeaponFamily.Revolver, tier = 2 },
        new LanItem { id = "revolver-tier3", family = (int)WeaponFamily.Revolver, tier = 3 }
    }).ToArray();
    File.WriteAllText(accountPath, JsonSerializer.Serialize(fixture, AccountStore.Json));
    store = new AccountStore(root);
    var handguns = store.Authenticate("Female", password, false).Profile!.items.Where(i => LanRules.IsHandgun(i.family)).ToArray();
    Check(handguns.Count(i => i.family == (int)WeaponFamily.Pistol) == 3 && handguns.Count(i => i.family == (int)WeaponFamily.Revolver) == 3,
        "All three pistol and revolver tiers survive loading as independent weapon families");
    Check(store.Equip("Female", "revolver-tier2").Profile?.activeItemId == "revolver-tier2", "Owned revolver equips independently of pistol tier");
    store = new AccountStore(root);
    var loadedFemale = store.Authenticate("Female", password, false).Profile!;
    Check(loadedFemale.items.Single(i => i.id == loadedFemale.activeItemId).family == (int)WeaponFamily.Revolver, "Revolver identity and equipment persist on restart");

    var world = new LobbyWorld();
    bool allSlots = true;
    for (int i = 0; i < 60; i++) allSlots &= world.Join("p" + i, new LanProfile { nickname = "Player" + i }).Player != null;
    Check(allSlots, "All sixty simulated capacity slots available");
    Check(world.Join("overflow", new LanProfile { nickname = "Overflow" }).Player == null, "Total capacity enforced");
    Check(world.Snapshot(1).actors.Length == 30 && world.Snapshot(2).actors.Length == 30, "Two isolated 30-player channels");
    Check(world.Join("duplicate", new LanProfile { nickname = "Player0" }).Player == null, "Duplicate session rejected");
    Check(!world.Input("p0", new LanCommand { move = float.NaN, sequence = 1 }), "NaN input rejected");
    Check(!world.Input("p0", new LanCommand { aimAngle = float.NaN, sequence = 1 }), "NaN aim rejected");
    world.Input("p0", new LanCommand { move = 100000, sequence = 1 });
    world.Tick(.05f);
    Check(Math.Abs(world.Find("p0")!.X - (LanRules.TownSpawnX + .25f)) < .001f, "Client speed cannot exceed server movement speed");
    Check(!world.Input("p0", new LanCommand { move = -1, sequence = 1 }), "Replayed input rejected");
    for (int i = 0; i < 20; i++) world.Tick(.05f);
    float idleX = world.Find("p0")!.X;
    world.Tick(.05f);
    Check(world.Find("p0")!.X == idleX, "Stale movement stops within 0.5 seconds");
    world.Input("p0", new LanCommand { jump = true, sequence = 2 }); world.Tick(.05f);
    float velocity = world.Find("p0")!.VelocityY;
    world.Input("p0", new LanCommand { jump = true, sequence = 3 }); world.Tick(.05f);
    Check(world.Find("p0")!.VelocityY < velocity, "No repeated airborne jump exploit");
    Check(world.Chat("p0", "hello\n<color=red>world</color>")!.text == "hello<color=red>world</color>", "Chat control characters removed");
    Check(world.Chat("p0", "spam") == null, "Chat throttle enforced");
    Check(world.ChangeChannel("p0", 2).Length > 0, "Channel full check enforced");
    world.Leave("p30"); Check(world.ChangeChannel("p0", 2) == "", "Channel transfer works after a slot opens");
    Check(!world.Snapshot(1).actors.Any(a => a.nickname == "Player0"), "Channel transfer removes actor from old channel");
    Check(LanRules.DungeonPower(1) == 3.3f && LanRules.DungeonPower(2) == 1.8f && LanRules.DungeonPower(3) == 1.25f && LanRules.DungeonPower(4) == 1, "Confirmed dungeon multipliers retained");

    string netData = Path.Combine(root, "network");
    string fingerprint;
    string networkItem;
    await using (var host = new LanHost(netData, 0, IPAddress.Loopback))
    {
        host.Start(); fingerprint = host.Fingerprint;
        await using var a = await TestClient.Connect(host.Port, fingerprint);
        await using var b = await TestClient.Connect(host.Port, fingerprint);
        await a.Send(new LanCommand { op = "register", nickname = "Alice", password = password });
        var welcome = await a.Wait("welcome"); networkItem = welcome.profile!.items[0].id;
        Check(welcome.profile.money == 300, "TLS registration and welcome packet");
        await b.Send(new LanCommand { op = "register", nickname = "Bob", password = password, body = 1 }); var bob = (await b.Wait("welcome")).profile;
        LanEvent snap;
        do { snap = await a.Wait("snapshot"); } while (snap.actors.Length < 2);
        Check(snap.actors.Length == 2, "Two real TLS clients share the town");
        Check(snap.actors.Single(x => x.nickname == "Bob").body == 1, "Female selection replicated to another real client");
        await a.Send(new LanCommand { op = "equip", itemId = bob.items[0].id });
        Check((await a.Wait("error")).text.Contains("보유"), "Foreign item equip denied over real connection");
        await b.Send(new LanCommand { op = "equip", itemId = bob.items[1].id });
        Check((await b.Wait("profile")).profile.activeItemId == bob.items[1].id, "Equip acknowledgement contains saved profile");
        do { snap = await a.Wait("snapshot"); } while (snap.actors.Single(x => x.nickname == "Bob").weaponFamily != (int)WeaponFamily.Melee);
        Check(snap.actors.Single(x => x.nickname == "Bob").weaponFamily == 5, "Held weapon change replicated to another real client");
        await b.Send(new LanCommand { op = "input", aimAngle = 165, aiming = true, sequence = 1 });
        do { snap = await a.Wait("snapshot"); } while (!snap.actors.Single(x => x.nickname == "Bob").aiming);
        Check(snap.actors.Single(x => x.nickname == "Bob").aimAngle == 165 && snap.actors.Single(x => x.nickname == "Bob").facing == -1, "Aim direction replicated with facing");
        await a.Send(new LanCommand { op = "chat", text = "동료 테스트" });
        Check((await b.Wait("chat")).text == "동료 테스트", "Korean chat delivered to another client");
        await b.Send(new LanCommand { op = "channel", channel = 2 }); await b.Wait("channel");
        do { snap = await b.Wait("snapshot"); } while (snap.channel != 2);
        Check(snap.actors.Length == 1 && snap.actors[0].nickname == "Bob", "Channel switch isolated over real sockets");
        await a.Send(new LanCommand { op = "input", move = 1, sequence = 1 });
        do { snap = await a.Wait("snapshot"); } while (snap.actors[0].x <= LanRules.TownSpawnX);
        Check(snap.actors[0].x > LanRules.TownSpawnX, "Authoritative motion snapshots delivered");
        await using var intruder = await TestClient.Connect(host.Port, fingerprint);
        await intruder.Send(new LanCommand { op = "login", nickname = "Alice", password = password });
        Check((await intruder.Wait("error")).text.Contains("이미 접속"), "Second login cannot take over online account");
        await using var anonymous = await TestClient.Connect(host.Port, fingerprint);
        await anonymous.Send(new LanCommand { op = "chat", text = "not logged in" });
        Check((await anonymous.Wait("error")).text.Contains("로그인"), "Unauthenticated commands denied");
        bool wrongPinRejected = false;
        try { await using var wrongPin = await TestClient.Connect(host.Port, new string('0', 64)); }
        catch (System.Security.Authentication.AuthenticationException) { wrongPinRejected = true; }
        Check(wrongPinRejected, "Wrong server identity rejected before sending password");
    }
    string networkAccounts = Path.Combine(netData, "accounts.json");
    var networkFixture = JsonSerializer.Deserialize<AccountDatabase>(File.ReadAllText(networkAccounts), AccountStore.Json)!;
    var savedAlice = networkFixture.Accounts.Single(a => a.Nickname == "Alice").Profile;
    savedAlice.items[0].tier = 3;
    savedAlice.items = savedAlice.items.Append(new LanItem { id = "network-revolver", family = (int)WeaponFamily.Revolver, tier = 3 }).ToArray();
    File.WriteAllText(networkAccounts, JsonSerializer.Serialize(networkFixture, AccountStore.Json));
    await using (var restarted = new LanHost(netData, 0, IPAddress.Loopback))
    {
        restarted.Start(); Check(restarted.Fingerprint == fingerprint, "Server identity survives restart");
        await using var a = await TestClient.Connect(restarted.Port, fingerprint);
        await a.Send(new LanCommand { op = "login", nickname = "Alice", password = password });
        Check((await a.Wait("welcome")).profile.items[0].id == networkItem, "Server restart preserves registered account and inventory");
        await using var b = await TestClient.Connect(restarted.Port, fingerprint);
        await b.Send(new LanCommand { op = "login", nickname = "Bob", password = password }); await b.Wait("welcome");
        LanEvent snap;
        do { snap = await b.Wait("snapshot"); } while (snap.actors.Length < 2);
        var visiblePistol = snap.actors.Single(x => x.nickname == "Alice");
        Check(visiblePistol.weaponFamily == (int)WeaponFamily.Pistol && visiblePistol.weaponTier == 3, "Tier3 pistol remains a pistol on remote client");
        await a.Send(new LanCommand { op = "equip", itemId = "network-revolver" }); await a.Wait("profile");
        do { snap = await b.Wait("snapshot"); } while (snap.actors.Single(x => x.nickname == "Alice").weaponFamily != (int)WeaponFamily.Revolver);
        Check(snap.actors.Single(x => x.nickname == "Alice").weaponTier == 3, "Independent tier3 revolver replicated over real TLS sockets");
    }
    Check(new AccountStore(netData).Authenticate("Alice", password, false).Profile!.activeItemId == "network-revolver", "Network revolver equip is saved to account");
    string corrupt = Path.Combine(root, "corrupt"); Directory.CreateDirectory(corrupt); File.WriteAllText(Path.Combine(corrupt, "accounts.json"), "broken");
    bool failedClosed = false;
    try { _ = new AccountStore(corrupt); } catch (JsonException) { failedClosed = true; }
    Check(failedClosed, "Corrupt account database fails visibly without resetting progress");
    Console.WriteLine($"ALL {checks} CHECKS PASSED");
}
finally
{
    // Only this test's uniquely named temporary directory may be removed.
    string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (Path.GetFullPath(root).StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root).StartsWith("MiniWarTests-")) Directory.Delete(root, true);
}

sealed class TestClient(TcpClient tcp, SslStream stream) : IAsyncDisposable
{
    public static async Task<TestClient> Connect(int port, string pin)
    {
        var tcp = new TcpClient { NoDelay = true };
        try
        {
            await tcp.ConnectAsync(IPAddress.Loopback, port);
            var stream = new SslStream(tcp.GetStream(), false, (_, cert, _, _) => cert != null && Convert.ToHexString(SHA256.HashData(cert.GetRawCertData())) == pin);
            await stream.AuthenticateAsClientAsync("localhost"); return new TestClient(tcp, stream);
        }
        catch { tcp.Dispose(); throw; }
    }
    public Task Send(LanCommand command) => LanHost.WriteFrame(stream, JsonSerializer.SerializeToUtf8Bytes(command, AccountStore.Json), CancellationToken.None);
    public async Task<LanEvent> Wait(string op)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (true)
        {
            var value = JsonSerializer.Deserialize<LanEvent>(await LanHost.ReadFrame(stream, timeout.Token), AccountStore.Json)!;
            if (value.op == op) return value;
            if (value.op == "error") throw new Exception(value.text);
        }
    }
    public ValueTask DisposeAsync() { stream.Dispose(); tcp.Dispose(); return ValueTask.CompletedTask; }
}
