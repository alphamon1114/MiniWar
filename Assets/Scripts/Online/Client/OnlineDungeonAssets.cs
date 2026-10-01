using System;
using System.Security.Cryptography;
using MiniWar.Dungeons;
using UnityEngine;

namespace MiniWar.Online
{
    public sealed class OnlineDungeonAssets : ScriptableObject
    {
        public RoomDungeon gateOutskirts;
        public static string Revision()
        {
            var data = Resources.Load<TextAsset>("Online/Dungeons/GateOutskirts");
            if (data == null) return "";
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data.bytes)).Replace("-", "");
        }
    }
}
