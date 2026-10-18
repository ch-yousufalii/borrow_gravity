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
    /// Internet rooms through Unity Relay: the host gets a join code from Unity's relay service, the other player
    /// types it in, and both talk through Unity's relay servers (works across different networks / IPs, no port
    /// forwarding). Carries the same byte packets as the same-Wi-Fi link in <see cref="NetSession"/>.
    /// Needs the project linked to a Unity Cloud project (Relay enabled) and an internet connection.
    /// </summary>
    public static class RelayLink
    {
        static NetworkDriver driver;
        static NetworkConnection connection;
        static bool isHost;

        public static bool Active => driver.IsCreated;
        public static bool PeerConnected => driver.IsCreated && connection.IsCreated &&
                                            driver.GetConnectionState(connection) == NetworkConnection.State.Connected;

        /// <summary>Raised on the main thread: (bytes) for every packet from the other player.</summary>
        public static event Action<byte[]> Received;
        /// <summary>Raised when the link to the other player is up (client connected / host accepted).</summary>
        public static event Action Connected;
        public static event Action Disconnected;

        static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>Host: create a relay allocation for one guest and return its join code.</summary>
        public static async Task<string> HostAsync()
        {
            Shutdown();
            await SignIn();
            var allocation = await RelayService.Instance.CreateAllocationAsync(1);
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
            connection = driver.Connect(data.Endpoint); // through the relay server
            isHost = false;
        }

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
                    if (connection.IsCreated && driver.GetConnectionState(connection) == NetworkConnection.State.Connected)
                    {
                        driver.Disconnect(incoming); // one guest per room
                        continue;
                    }
                    connection = incoming;
                    Connected?.Invoke();
                }
            }
            if (!connection.IsCreated) return;

            NetworkEvent.Type ev;
            while ((ev = driver.PopEventForConnection(connection, out var reader)) != NetworkEvent.Type.Empty)
            {
                switch (ev)
                {
                    case NetworkEvent.Type.Connect:
                        Connected?.Invoke();
                        break;
                    case NetworkEvent.Type.Data:
                    {
                        var native = new NativeArray<byte>(reader.Length, Allocator.Temp);
                        reader.ReadBytes(native);
                        var bytes = native.ToArray();
                        native.Dispose();
                        Received?.Invoke(bytes);
                        break;
                    }
                    case NetworkEvent.Type.Disconnect:
                        connection = default;
                        Disconnected?.Invoke();
                        return;
                }
            }
        }

        public static void Send(byte[] buffer, int length)
        {
            if (!PeerConnected) return;
            if (driver.BeginSend(connection, out var writer) != 0) return;
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
                if (connection.IsCreated) driver.Disconnect(connection);
                driver.ScheduleUpdate().Complete();
                driver.Dispose();
            }
            driver = default;
            connection = default;
        }
    }
}
