using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using Pixelyth.Managers;
using static Pixelyth.Patches.PatchHandler;

namespace Pixelyth.Patches.Safety
{
    public static class AntiGhostPatch
    {
        public static bool Enabled;
        public static bool Notify;

        [SecurityPatch]
        [HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.OnEvent), typeof(EventData))]
        public static class OnEventPatch
        {
            private static bool Prefix(EventData photonEvent)
            {
                if (!Enabled || photonEvent == null || photonEvent.Code != (byte)PunEvent.DestroyPlayer)
                    return true;

                try
                {
                    if (!(photonEvent.CustomData is Hashtable eventData) || !(eventData[0] is int targetActorNumber))
                        return true;

                    Player localPlayer = PhotonNetwork.LocalPlayer;
                    int localActorNumber = localPlayer == null ? -1 : localPlayer.ActorNumber;
                    int masterActorNumber = PhotonNetwork.CurrentRoom == null ? -1 : PhotonNetwork.CurrentRoom.MasterClientId;
                    bool targetsSelf = targetActorNumber < 0 || targetActorNumber == localActorNumber;
                    bool fromMaster = photonEvent.Sender == masterActorNumber;

                    if (!targetsSelf && fromMaster)
                        return true;

                    LogManager.LogWarning($"Blocked a {(targetsSelf ? "ghost" : "isolate")} event from actor {photonEvent.Sender} targeting actor {targetActorNumber}.");
                    if (Notify && PhotonNetwork.InRoom)
                        NotificationManager.SendNotification($"<color=grey>[</color><color=green>ANTI-GHOST</color><color=grey>]</color> Blocked an attempt to make {(targetsSelf ? "you" : "another player")} invisible.");
                    return false;
                }
                catch
                {
                    return true;
                }
            }
        }
    }
}
