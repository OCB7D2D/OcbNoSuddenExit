using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Collections;

#pragma warning disable IDE0051 // Remove unused private members

public class NoSuddenExit : IModApi
{

    public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    [HarmonyPatch(typeof(Entity))]
    [HarmonyPatch("SendDetach")]
    public class Entity_SendDetach
    {

        static float ExitDelay = 2f;

        static TimerEventData Timer = null;

        static MethodInfo MethodGetWheelsOnGround = AccessTools
            .Method(typeof(EntityVehicle), "GetWheelsOnGround");

        static void ExitVehicle(TimerEventData timerData)
        {
            object[] data = (object[])timerData.Data;
            Entity entity = (Entity)data[0]; int id = (int)data[1];
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                SingletonMonoBehaviour<ConnectionManager>.Instance
                    .SendToServer(NetPackageManager.GetPackage<NetPackageEntityAttach>()
                    .Setup(NetPackageEntityAttach.AttachType.DetachServer, id, -1, -1));
            else
                SingletonMonoBehaviour<ConnectionManager>.Instance
                    .SendPackage(NetPackageManager.GetPackage<NetPackageEntityAttach>()
                    .Setup(NetPackageEntityAttach.AttachType.DetachClient, id, -1, -1));
            Timer = null;
            entity.Detach();
        }

        static void AbortTimer(TimerEventData timerData)
        {
            Timer = null;
        }

        static bool Prefix(Entity __instance, int ___entityId)
        {

            if (__instance is EntityPlayerLocal player)
            {
                // Abort queued exit?
                if (Timer != null)
                {
                    // This should trigger the `CloseEvent`
                    player.PlayerUI.windowManager.Close("timer");
                    // Nothing else to do
                    return false;
                }

                // Do a generic check if we are "in the air" (or not)
                if (__instance.AttachedToEntity is EntityVehicle vehicle)
                {
                    // Check how many wheels are colliding with something
                    if ((int) MethodGetWheelsOnGround.Invoke(vehicle, new object[]{}) > 0)
                    {
                        // No delay if on ground
                        return true;
                    }
                }

                // Create a new timer event
                Timer = new TimerEventData
                {
                    // Set arbitrary cookie data
                    Data = new object[]
                    {
                        __instance,
                        ___entityId
                    }
                };
                // Attach callback handler
                Timer.Event += ExitVehicle;
                // Attach callback handler
                Timer.CloseEvent += AbortTimer;
                // Get UI component for the timer
                XUiC_Timer TimerUI = player.PlayerUI
                    .xui.GetChildByType<XUiC_Timer>();
                // Attach our timer to UI component
                TimerUI.SetTimer(ExitDelay, Timer);
                // Make sure the UI widget is actually shown
                player.PlayerUI.windowManager.Open("timer", true);
                // Nothing more to do
                return false;
            }

            // Execute original
            return true;
        }
    }

}
