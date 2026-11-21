using GorillaLocomotion.Gameplay;
using GorillaNetworking;
using Pixelyth.Managers;
using UnityEngine;

namespace Pixelyth.Mods
{
    public static class Master
    {
        public static void FastBroomsticks()
        {
            if (!NetworkSystem.Instance.IsMasterClient)
            {
                NotificationManager.SendNotification("You must be the master client to change broomstick speed.");
                return;
            }

            foreach (NoncontrollableBroomstick broomstick in Resources.FindObjectsOfTypeAll<NoncontrollableBroomstick>())
                if (broomstick != null) broomstick.duration = 10f;
        }

        public static void SlowBroomsticks()
        {
            if (!NetworkSystem.Instance.IsMasterClient)
            {
                NotificationManager.SendNotification("You must be the master client to change broomstick speed.");
                return;
            }

            foreach (NoncontrollableBroomstick broomstick in Resources.FindObjectsOfTypeAll<NoncontrollableBroomstick>())
                if (broomstick != null) broomstick.duration = 100f;
        }

        public static void ResetBroomsticks()
        {
            foreach (NoncontrollableBroomstick broomstick in Resources.FindObjectsOfTypeAll<NoncontrollableBroomstick>())
                if (broomstick != null) broomstick.duration = 30f;
        }
    }
}
