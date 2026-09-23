using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MiniWar.Online;

namespace MiniWar.Server;

public sealed class Account
{
    public string Nickname { get; set; } = "";
    public string Salt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int Iterations { get; set; } = 210000;
    public LanProfile Profile { get; set; } = new();
}

public sealed class AccountDatabase
{
    public int Schema { get; set; } = 1;
    public List<Account> Accounts { get; set; } = new();
}

public sealed class AccountStore
{
    public static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    readonly string path;
    readonly object gate = new();
    readonly AccountDatabase database;

    public AccountStore(string directory)
    {
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "accounts.json");
        // A corrupt database must never silently reset every player's progression.
        database = File.Exists(path)
            ? JsonSerializer.Deserialize<AccountDatabase>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Empty account database.")
            : new AccountDatabase();
        if (database.Schema != 1) throw new InvalidDataException("Unsupported account schema.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in database.Accounts)
        {
            if (!ValidName(a.Nickname) || !names.Add(a.Nickname) || a.Iterations < 100000 || a.Iterations > 1000000
                || a.Profile == null || a.Profile.money < 0 || a.Profile.nickname != a.Nickname)
                throw new InvalidDataException("Invalid account record.");
            if (a.Profile.body is < 0 or > 1 || a.Profile.items == null || a.Profile.items.Any(i => !LanRules.IsWeaponFamily(i.family) || i.tier is < 1 or > 3))
                throw new InvalidDataException("Invalid character or weapon record.");
            if (string.IsNullOrEmpty(a.Profile.activeItemId)) a.Profile.activeItemId = a.Profile.items.FirstOrDefault()?.id;
        }
    }

    public static string Normalize(string value) => (value ?? "").Trim().Normalize(NormalizationForm.FormC);
    public static bool ValidName(string name) => Regex.IsMatch(name, @"\A[\p{L}\p{Nd}_-]{2,16}\z");
    static byte[] Hash(string password, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

    public (LanProfile? Profile, string Error) Authenticate(string rawName, string password, bool register, int body = 0)
    {
        string name = Normalize(rawName);
        if (!ValidName(name)) return (null, "닉네임은 글자·숫자·밑줄·하이픈 2~16자로 입력하세요.");
        if (password == null || password.Length < 8 || password.Length > 128) return (null, "비밀번호는 8~128자로 입력하세요.");
        Account? account;
        lock (gate) account = database.Accounts.Find(a => string.Equals(a.Nickname, name, StringComparison.OrdinalIgnoreCase));
        if (register)
        {
            if (body is < 0 or > 1) return (null, "캐릭터를 선택하세요.");
            if (account != null) return (null, "이미 사용 중인 닉네임입니다.");
            var salt = RandomNumberGenerator.GetBytes(16);
            var pistol = new LanItem { id = Guid.NewGuid().ToString("N"), family = 0, ammo = 12 };
            var melee = new LanItem { id = Guid.NewGuid().ToString("N"), family = 5 };
            account = new Account { Nickname = name, Salt = Convert.ToBase64String(salt), PasswordHash = Convert.ToBase64String(Hash(password, salt, 210000)),
                Profile = new LanProfile { nickname = name, body = body, activeItemId = pistol.id, money = 300, items = new[] { pistol, melee }, equipped = new[] { pistol.id, melee.id } } };
            lock (gate)
            {
                if (database.Accounts.Any(a => string.Equals(a.Nickname, name, StringComparison.OrdinalIgnoreCase))) return (null, "이미 사용 중인 닉네임입니다.");
                database.Accounts.Add(account);
                try { Save(); }
                catch { database.Accounts.Remove(account); throw; }
            }
            return (Copy(account.Profile), "");
        }
        // Spend the same work for an unknown nickname to reduce timing-based account enumeration.
        byte[] actual = Hash(password, account == null ? new byte[16] : Convert.FromBase64String(account.Salt), account?.Iterations ?? 210000);
        if (account == null || !CryptographicOperations.FixedTimeEquals(actual, Convert.FromBase64String(account.PasswordHash)))
            return (null, "닉네임 또는 비밀번호가 일치하지 않습니다.");
        lock (gate) return (Copy(account.Profile), "");
    }

    public (LanProfile? Profile, string Error) Equip(string nickname, string itemId)
    {
        lock (gate)
        {
            var account = database.Accounts.Find(a => a.Nickname == nickname);
            if (account == null || string.IsNullOrEmpty(itemId) || !account.Profile.items.Any(i => i.id == itemId))
                return (null, "보유한 무기만 장착할 수 있습니다.");
            if (account.Profile.activeItemId == itemId) return (Copy(account.Profile), "");
            string previous = account.Profile.activeItemId;
            account.Profile.activeItemId = itemId;
            try { Save(); }
            catch { account.Profile.activeItemId = previous; throw; }
            return (Copy(account.Profile), "");
        }
    }

    static LanProfile Copy(LanProfile profile) => JsonSerializer.Deserialize<LanProfile>(JsonSerializer.Serialize(profile, Json), Json)!;

    void Save()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(database, Json);
        string temp = path + ".pending";
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
        if (File.Exists(path)) File.Replace(temp, path, path + ".backup");
        else File.Move(temp, path);
    }
}
