using System;
using Alta.Networking;

namespace TavernLib.Utils;

public static class TavernEvents
{
    public static readonly TavernEvent<ISocket> SocketCreated = new();
    
    
    public sealed class TavernEvent<T>
    {
        private event Action<T> Handlers;

        public void Subscribe(Action<T> handler) => Handlers += handler;
        public void Unsubscribe(Action<T> handler) => Handlers -= handler;

        internal void Invoke(T arg) => Handlers?.Invoke(arg);
    }
}