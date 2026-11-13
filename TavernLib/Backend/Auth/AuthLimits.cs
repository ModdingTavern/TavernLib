using System;
using System.Collections.Generic;
using System.Linq;

namespace TavernLib.Backend.Auth;

// Per-address limits on 1762, the launcher's numbers: 8 failures, or 5 wrong passwords, each less
// than 60 s after the one before, hold an address off until 60 s pass with no new failure. Open
// sockets are capped per address and in all.
internal class AuthLimits
{
    public const int MaxOpen = 64;
    public const int MaxOpenPerAddress = 8;
    public const int FailLimit = 8;
    public const int PasswordFailLimit = 5;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);
    private const int PruneAbove = 1024;

    private readonly object _sync = new();
    private readonly Dictionary<string, int> _open = new();
    private readonly HashSet<string> _capLogged = new();
    private bool _totalCapLogged;
    private int _openTotal;
    private readonly Dictionary<string, Strikes> _fails = new();
    private readonly Dictionary<string, Strikes> _passwordFails = new();

    private struct Strikes
    {
        public int Count;
        public DateTime Last;
    }

    public bool TryOpen(string address)
    {
        lock (_sync)
        {
            if (_openTotal >= MaxOpen)
            {
                if (!_totalCapLogged) TavernLogger.Warn($"{MaxOpen} connections are open on 1762; new ones are closed until some finish");
                _totalCapLogged = true;
                return false;
            }

            _open.TryGetValue(address, out var count);
            if (count >= MaxOpenPerAddress)
            {
                if (_capLogged.Add(address)) TavernLogger.Warn($"{address} has {MaxOpenPerAddress} connections open on 1762; its new ones are closed until some finish");
                return false;
            }

            _open[address] = count + 1;
            _openTotal++;
            return true;
        }
    }

    public void Close(string address)
    {
        lock (_sync)
        {
            _openTotal--;
            if (_openTotal < MaxOpen) _totalCapLogged = false;

            if (_open.TryGetValue(address, out var count) && count > 1)
            {
                _open[address] = count - 1;
                return;
            }

            _open.Remove(address);
            _capLogged.Remove(address);
        }
    }

    public bool HeldOff(string address) => Count(_fails, address) >= FailLimit;
    public bool PasswordHeldOff(string address) => Count(_passwordFails, address) >= PasswordFailLimit;

    public void Fail(string address)
    {
        if (address != null && Add(_fails, address) == FailLimit)
            TavernLogger.Warn($"Too many failed attempts from {address}; it is held off until 60 s pass without another");
    }

    public void PasswordFail(string address)
    {
        if (Add(_passwordFails, address) == PasswordFailLimit)
            TavernLogger.Warn($"Too many wrong passwords from {address}; it is held off until 60 s pass without another");
        Fail(address);
    }

    private int Count(Dictionary<string, Strikes> strikes, string address)
    {
        lock (_sync)
        {
            return strikes.TryGetValue(address, out var s) && DateTime.UtcNow - s.Last <= Window ? s.Count : 0;
        }
    }

    private int Add(Dictionary<string, Strikes> strikes, string address)
    {
        lock (_sync)
        {
            var now = DateTime.UtcNow;
            if (strikes.Count > PruneAbove)
                foreach (var stale in strikes.Where(pair => now - pair.Value.Last > Window).Select(pair => pair.Key).ToList())
                    strikes.Remove(stale);

            var count = strikes.TryGetValue(address, out var s) && now - s.Last <= Window ? s.Count + 1 : 1;
            strikes[address] = new Strikes { Count = count, Last = now };
            return count;
        }
    }
}
