#nullable disable
using System;

namespace MiniWar.Online
{
    // Keep existing save IDs stable; a revolver is a separate weapon, never a higher pistol tier.
    public enum WeaponFamily { Pistol = 0, SubmachineGun = 1, Shotgun = 2, Rifle = 3, SniperRifle = 4, Melee = 5, Revolver = 6 }
    public enum CharacterBody { Male = 0, Female = 1 }

    public static class LanRules
    {
        public const int Version = 5;
        public const int PartyCapacity = 4;
        public const int Port = 7777;
        public const int ChannelCapacity = 30;
        public const int MaxChannels = 2;
        public const float TownWidth = 42f;
        public const float TownSpawnX = 18f;
        public const float WalkSpeed = 5f;
        public const float DashSpeed = 15f;
        public const float DashDuration = .16f;
        public const float DashChainWindow = 1f;
        public const float DashCooldown = 3f;
        public const float JumpSpeed = 8f;
        public const float Gravity = 24f;
        public static bool IsWeaponFamily(int family) => family >= 0 && family <= (int)WeaponFamily.Revolver;
        public static bool IsHandgun(int family) => family == (int)WeaponFamily.Pistol || family == (int)WeaponFamily.Revolver;
        public static float DungeonPower(int players)
        {
            switch (players) { case 1: return 3.3f; case 2: return 1.8f; case 3: return 1.25f; case 4: return 1f; default: throw new ArgumentOutOfRangeException(nameof(players)); }
        }
    }

    // Only intent crosses the client -> server boundary. No money, damage or position setters.
    [Serializable] public sealed class LanCommand
    {
        public int version = LanRules.Version;
        public string op;
        public string nickname;
        public string password;
        public int body;
        public string itemId;
        public string text;
        public int channel;
        public float move;
        public bool jump;
        public bool dash; // Shift press edge, never a held sprint or client-provided speed.
        public float aimAngle; // Continuous world-space firing intent; only the visible pose is quantized.
        public bool aiming;
        public bool fire; // Held trigger; server chooses cadence and validates the equipped weapon.
        public long sequence;
        public string partyId;
        public string applicationId;
    }

    [Serializable] public sealed class LanItem
    {
        public string id;
        public int family;
        public int tier = 1;
        public int enhance;
        public int ammo;
    }

    [Serializable] public sealed class LanProfile
    {
        public string nickname;
        public int body;
        public string activeItemId;
        public int money;
        public int parts;
        public LanItem[] items = Array.Empty<LanItem>();
        public string[] equipped = Array.Empty<string>();
    }

    [Serializable] public sealed class LanActor
    {
        public string nickname;
        public float x;
        public float y;
        public int facing = 1;
        public int body;
        public int weaponFamily;
        public int weaponTier = 1;
        public int weaponEnhance;
        public float aimAngle; // Exact firing intent; rendering alone is quantized.
        public bool aiming;
        public float dashRemaining, dashCooldown, dashChainWindow;
        public int dashDirection = 1;
        public long dashSequence;
    }

    [Serializable] public sealed class LanShot
    {
        public long id;
        public string shooter;
        public int family;
        public int tier = 1;
        public int facing;
        public float x, y, angle;
    }

    [Serializable] public sealed class LanPartyListing
    {
        public string id, title, leader;
        public int count;
        public int capacity = LanRules.PartyCapacity;
    }

    [Serializable] public sealed class LanPartyMember
    {
        public string nickname;
        public int body;
    }

    [Serializable] public sealed class LanPartyApplication
    {
        public string id, nickname;
        public int body;
    }

    [Serializable] public sealed class LanParty
    {
        public string id, title, leader;
        public int capacity = LanRules.PartyCapacity;
        public LanPartyMember[] members = Array.Empty<LanPartyMember>();
        // Sent only to the leader of this party.
        public LanPartyApplication[] applications = Array.Empty<LanPartyApplication>();
    }

    [Serializable] public sealed class LanEvent
    {
        public int version = LanRules.Version;
        public string op;
        public string text;
        public string sender;
        public long tick;
        public int channel;
        public int[] channelCounts = Array.Empty<int>();
        public LanProfile profile;
        public LanActor[] actors = Array.Empty<LanActor>();
        public LanShot shot;
        public LanPartyListing[] parties = Array.Empty<LanPartyListing>();
        public LanParty party;
        public string pendingPartyId, pendingPartyTitle;
    }
}
