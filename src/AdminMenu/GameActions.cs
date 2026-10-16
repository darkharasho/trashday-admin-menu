using System;
using System.Collections.Generic;
using System.Linq;
using AdminMenu.Patches;
using Ox1;
using Trash;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AdminMenu
{
    /// <summary>One connected player as the Players tab shows it.</summary>
    internal sealed class PlayerInfo
    {
        public ulong ClientId;
        public string Name;
        public bool HasBody;
        public bool IsDead;
        public bool IsLocal;
    }

    /// <summary>
    /// Every call into the game, in one place. Each action returns the message for the status line, or throws
    /// <see cref="InvalidOperationException"/> with a message the player can act on. All of them assume the host.
    /// </summary>
    internal static class GameActions
    {
        public static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        public static PlayerController LocalPlayer()
        {
            foreach (var player in PlayerController.All)
            {
                if (player != null && player.IsOwner)
                    return player;
            }
            return null;
        }

        public static void Toast(string message)
        {
            Plugin.Log.LogInfo(message);
            if (ToastUI.Instance.TryGet(out var toast) && toast != null)
                toast.ShowToast(message);
        }

        // ---- Players ----

        public static List<PlayerInfo> Players()
        {
            var local = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
            var result = new List<PlayerInfo>();
            foreach (var online in OnlinePlayer.All)
            {
                if (online == null)
                    continue;
                var id = online.OwnerClientId;
                var name = online.PlayerName.Value;
                var info = new PlayerInfo
                {
                    ClientId = id,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Player {id}" : name,
                    IsLocal = id == local,
                };
                if (PlayerSpawner.TryGetPlayerOwnedBy(id, out var body) && body != null)
                {
                    info.HasBody = true;
                    info.IsDead = body.IsDead;
                }
                result.Add(info);
            }
            return result.OrderBy(p => p.IsLocal ? 0 : 1).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ReviveServerRpc only calls FullyHeal, and healing clears the dead flag, so both buttons use FullyHeal.
        public static string Revive(ulong clientId)
        {
            var player = RequireBody(clientId, out var name);
            if (!player.IsDead)
                throw new InvalidOperationException($"{name} isn't dead");
            player.Health.FullyHeal();
            return $"Revived {name}";
        }

        public static string Heal(ulong clientId)
        {
            var player = RequireBody(clientId, out var name);
            if (player.IsDead)
                throw new InvalidOperationException($"{name} is dead; use Revive");
            player.Health.FullyHeal();
            return $"Healed {name}";
        }

        // Damage is a 0-1 fraction; 1 kills, as the game's own trash.die does.
        public static string Kill(ulong clientId)
        {
            var player = RequireBody(clientId, out var name);
            if (player.IsDead)
                throw new InvalidOperationException($"{name} is already dead");
            Invincibility.Bypass = true;
            try
            {
                player.Health.TakeDamage(1f, player.transform.position + Vector3.up);
            }
            finally
            {
                Invincibility.Bypass = false;
            }
            return player.IsDead
                ? $"Killed {name}"
                : $"{name} took no damage (the game ignores hits within a second of the last one); try again";
        }

        public static string BringToMe(ulong clientId)
        {
            var host = RequireLocalPlayer();
            var player = RequireBody(clientId, out var name);
            if (player == host)
                throw new InvalidOperationException("that's you");
            var forward = ViewForward();
            player.Teleport(host.transform.position + forward * 1.5f + Vector3.up * 0.5f, Quaternion.LookRotation(-forward));
            return $"Brought {name} to you";
        }

        public static string GoTo(ulong clientId)
        {
            var host = RequireLocalPlayer();
            var player = RequireBody(clientId, out var name);
            if (player == host)
                throw new InvalidOperationException("that's you");
            var forward = Flat(player.transform.forward);
            host.Teleport(player.transform.position - forward * 1.5f + Vector3.up * 0.5f, Quaternion.LookRotation(forward));
            return $"Went to {name}";
        }

        // ---- Money ----

        public static float? Money() =>
            TeamInventory.Instance.TryGet(out var inventory) && inventory != null ? inventory.MoneyVar.Value : (float?)null;

        public static string AddMoney(float amount)
        {
            if (amount <= 0f || amount > Amounts.MaxMoney)
                throw new InvalidOperationException($"amount must be between 1 and {Amounts.MaxMoney:N0}");
            if (!TeamInventory.Instance.TryGet(out var inventory) || inventory == null)
                throw new InvalidOperationException("no team money in this level");
            inventory.Deposit(amount);
            return $"Added ${amount:N0} (team now has ${inventory.MoneyVar.Value:N0})";
        }

        // ---- Cart ----

        public static string CartSummary()
        {
            if (!TryGetCart(out var cart))
                return "No cart in this level.";
            var panels = cart.Panels ?? Array.Empty<WagonPanel>();
            var intact = panels.Count(p => p != null && !p.IsBroken);
            var tipped = cart.IsTippedOver() ? ", tipped over" : string.Empty;
            return $"Cart: {intact}/{panels.Length} panels intact, damage {cart.AccumDamage:P0}{tipped}";
        }

        public static string BringCart()
        {
            var host = RequireLocalPlayer();
            var cart = RequireCart();
            var forward = ViewForward();
            cart.Teleport(host.transform.position + forward * 3f + Vector3.up * 0.5f, Quaternion.LookRotation(forward));
            return "Brought the cart to you";
        }

        public static string FlipCart()
        {
            var cart = RequireCart();
            var yaw = cart.transform.eulerAngles.y;
            cart.Teleport(cart.transform.position + Vector3.up * 0.5f, Quaternion.Euler(0f, yaw, 0f));
            return "Flipped the cart upright";
        }

        public static string RepairCart()
        {
            var cart = RequireCart();
            var repaired = 0;
            foreach (var panel in cart.Panels ?? Array.Empty<WagonPanel>())
            {
                if (panel == null || !panel.IsBroken)
                    continue;
                panel.Repair(1);
                repaired++;
            }
            cart.AccumDamage = 0f;
            return repaired == 0 ? "No broken panels; cleared cart damage" : $"Repaired {repaired} panel(s) and cleared cart damage";
        }

        // ---- Level ----

        public static string NextCheckpoint()
        {
            var checkpoints = Object.FindObjectsByType<CheckpointTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (checkpoints.Length == 0)
                throw new InvalidOperationException("no checkpoints in this level");
            if (checkpoints.All(c => c == null || c.IsReached))
                throw new InvalidOperationException("every checkpoint is already reached");
            CheckpointTrigger.TeleportToNextCheckpoint();
            return "Teleported everyone to the next checkpoint";
        }

        public static string CompleteLevel()
        {
            if (!CampaignManager.Instance.TryGet(out var campaign) || campaign == null)
                throw new InvalidOperationException("no campaign running in this scene");
            campaign.CompleteLevel();
            return "Completing the level";
        }

        public static List<string> TeleportPoints() =>
            Object.FindObjectsByType<TeleportHere>(FindObjectsSortMode.None)
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Name))
                .Select(t => t.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

        public static string TeleportTo(string name)
        {
            var point = Object.FindObjectsByType<TeleportHere>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t != null && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (point == null)
                throw new InvalidOperationException($"teleport point {name} is gone");
            point.Execute();
            return $"Teleported everyone and the cart to {name}";
        }

        // ---- Placement ----

        /// <summary>A point <paramref name="distance"/> metres ahead of the host's view, level with the camera.</summary>
        public static Vector3 SpawnPoint(float distance)
        {
            if (LocalCamera.Instance.TryGet(out var camera) && camera != null)
                return camera.Position + ViewForward() * distance;
            var host = RequireLocalPlayer();
            return host.transform.position + ViewForward() * distance + Vector3.up;
        }

        /// <summary>Where the host is looking, flattened to the ground plane.</summary>
        private static Vector3 ViewForward()
        {
            if (LocalCamera.Instance.TryGet(out var camera) && camera != null)
                return Flat(camera.transform.forward);
            var host = LocalPlayer();
            return host != null ? Flat(host.transform.forward) : Vector3.forward;
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
        }

        private static PlayerController RequireLocalPlayer() =>
            LocalPlayer() ?? throw new InvalidOperationException("you have no player right now");

        private static PlayerController RequireBody(ulong clientId, out string name)
        {
            var online = OnlinePlayer.All.FirstOrDefault(p => p != null && p.OwnerClientId == clientId);
            name = online != null && !string.IsNullOrWhiteSpace(online.PlayerName.Value) ? online.PlayerName.Value : $"Player {clientId}";
            if (!PlayerSpawner.TryGetPlayerOwnedBy(clientId, out var player) || player == null)
                throw new InvalidOperationException($"{name} has no character right now");
            return player;
        }

        private static bool TryGetCart(out ShoppingCartController cart) =>
            ShoppingCartController.Instance.TryGet(out cart) && cart != null;

        private static ShoppingCartController RequireCart() =>
            TryGetCart(out var cart) ? cart : throw new InvalidOperationException("no cart in this level");
    }
}
