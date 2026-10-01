#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniWar.Online
{
    public sealed class PortalVoter
    {
        public string Id;
        public bool Alive = true, Connected = true;
    }

    // The caller validates S-key intent, proximity and room clearance; this owns only the quorum clock.
    public sealed class LanPortalRally
    {
        public const double Countdown = 5;
        readonly Dictionary<string, string> entered = new Dictionary<string, string>();
        public string CountingPortal { get; private set; }
        public double Deadline { get; private set; }
        public int AliveCount { get; private set; }
        public string Entered(string id) => entered.TryGetValue(id, out var portal) ? portal : null;
        public int Count(string portal) => entered.Values.Count(p => p == portal);
        public void Toggle(string id, string portal)
        { if (Entered(id) == portal) entered.Remove(id); else entered[id] = portal; }
        public void Remove(string id) { entered.Remove(id); }
        public void Reset() { entered.Clear(); CountingPortal = null; Deadline = 0; AliveCount = 0; }
        public float Remaining(double now) => CountingPortal == null ? 0 : (float)Math.Max(0, Deadline - now);

        public string Evaluate(IReadOnlyList<PortalVoter> members, ISet<string> usablePortals, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || members == null || usablePortals == null
                || members.Any(m => m == null || string.IsNullOrEmpty(m.Id)) || members.Select(m => m.Id).Distinct().Count() != members.Count)
            { Reset(); return null; }
            var alive = new HashSet<string>(members.Where(m => m.Alive && m.Connected).Select(m => m.Id));
            AliveCount = alive.Count;
            foreach (var id in entered.Keys.ToArray())
                if (!alive.Contains(id) || !usablePortals.Contains(entered[id])) entered.Remove(id);
            var majority = entered.Values.GroupBy(p => p).FirstOrDefault(g => g.Count() > AliveCount / 2);
            if (AliveCount == 0 || majority == null) { CountingPortal = null; Deadline = 0; return null; }
            if (majority.Count() == AliveCount) return majority.Key;
            if (CountingPortal != majority.Key) { CountingPortal = majority.Key; Deadline = now + Countdown; }
            return now + .000001 >= Deadline ? majority.Key : null;
        }
    }
}
