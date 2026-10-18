using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Internet rooms through Unity Relay: the host gets a join code from Unity's relay service, the other players
    /// type it in, and everyone talks through Unity's relay servers (works across different networks / IPs, no port
    /// forwarding). Carries the same byte packets as the same-Wi-Fi link in <see cref="NetSession"/>.
    /// Needs the project linked to a Unity Cloud project (Relay enabled) and an internet connection.
    /// </summary>
    public static class RelayLink
    {
        static NetworkDriver driver;
        // slot -> connection; the client only uses slot 0 (the host), the host one slot per guest
        static readonly NetworkConnection[] connections = new NetworkConnection[4];
        static bool isHost;

        public static bool Active => driver.IsCreated;

        /// <summary>Raised on the main thread: (slot, bytes) for every packet from another player.</summary>
        public static event Action<int, byte[]> Received;
        /// <summary>Raised when a link is up (client connected to the host / host accepted a guest), with its slot.</summary>
        public static event Action<int> Connected;
        public static event Action<int> Disconnected;

        static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>Host: create a relay allocation for up to <paramref name="guests"/> players and return its join code.</summary>
        public static async Task<string> HostAsync(int guests)
        {
            Shutdown();
            await SignIn();
            var allocation = await RelayService.Instance.CreateAllocationAsync(Mathf.Clamp(guests, 1, connections.Length - 1));
            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            var data = allocation.ToRelayServerData("dtls");
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref data);
            driver = NetworkDriver.Create(settings);
            if (driver.Bind(NetworkEndpoint.AnyIpv4) != 0 || driver.Listen() != 0)
            {
                Shutdown();
                throw new Exception("Relay: could not open the room");
            }
            isHost = true;
            return code;
        }

        /// <summary>Client: join the host's relay allocation by its join code.</summary>
        public static async Task JoinAsync(string joinCode)
        {
            Shutdown();
            await SignIn();
            var join = await RelayService.Instance.JoinAllocationAsync(joinCode);
            var data = join.ToRelayServerData("dtls");
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref data);
            driver = NetworkDriver.Create(settings);
            if (driver.Bind(NetworkEndpoint.AnyIpv4) != 0)
            {
                Shutdown();
                throw new Exception("Relay: could not connect");
            }
            connections[0] = driver.Connect(data.Endpoint); // through the relay server
            isHost = false;
        }

        static bool IsUp(NetworkConnection c) => c.IsCreated && driver.GetConnectionState(c) == NetworkConnection.State.Connected;

        /// <summary>Run the transport and hand out events/packets (call every frame).</summary>
        public static void Pump()
        {
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();

            if (isHost)
            {
                NetworkConnection incoming;
                while ((incoming = driver.Accept()) != default)
                {
                    int slot = -1;
                    for (int i = 0; i < connections.Length && slot < 0; i++) if (!IsUp(connections[i])) slot = i;
                    if (slot < 0) { driver.Disconnect(incoming); continue; } // room full
                    connections[slot] = incoming;
                    Connected?.Invoke(slot);
                }
            }

            for (int slot = 0; slot < connections.Length; slot++)
            {
                if (!driver.IsCreated || !connections[slot].IsCreated) continue;
                NetworkEvent.Type ev;
                while (driver.IsCreated && (ev = driver.PopEventForConnection(connections[slot], out var reader)) != NetworkEvent.Type.Empty)
                {
                    if (ev == NetworkEvent.Type.Connect) Connected?.Invoke(slot);
                    else if (ev == NetworkEvent.Type.Data)
                    {
                        var native = new NativeArray<byte>(reader.Length, Allocator.Temp);
                        reader.ReadBytes(native);
                        var bytes = native.ToArray();
                        native.Dispose();
                        Received?.Invoke(slot, bytes);
                    }
                    else if (ev == NetworkEvent.Type.Disconnect)
                    {
                        connections[slot] = default;
                        Disconnected?.Invoke(slot);
                        break;
                    }
                }
            }
        }

        public static void Send(int slot, byte[] buffer, int length)
        {
            if (!driver.IsCreated || slot < 0 || slot >= connections.Length || !IsUp(connections[slot])) return;
            if (driver.BeginSend(connections[slot], out var writer) != 0) return;
            var native = new NativeArray<byte>(length, Allocator.Temp);
            NativeArray<byte>.Copy(buffer, native, length);
            writer.WriteBytes(native);
            native.Dispose();
            driver.EndSend(writer);
        }

        public static void Shutdown()
        {
            if (driver.IsCreated)
            {
                foreach (var c in connections) if (c.IsCreated) driver.Disconnect(c);
                driver.ScheduleUpdate().Complete();
                driver.Dispose();
            }
            driver = default;
            for (int i = 0; i < connections.Length; i++) connections[i] = default;
        }
    }
}
