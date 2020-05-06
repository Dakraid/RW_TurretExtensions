﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TurretExtensions
{
    public static class Patch_Building_TurretGun
    {
        [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.DrawExtraSelectionOverlays))]
        public static class DrawExtraSelectionOverlays
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
            {
#if DEBUG
                    Log.Message("Transpiler start: Building_TurretGun.DrawExtraSelectionOverlays (1 match)");
#endif

                var instructionList = instructions.ToList();

                var drawRadiusRingInfo = AccessTools.Method(typeof(GenDraw), nameof(GenDraw.DrawRadiusRing), new[] {typeof(IntVec3), typeof(float)});
                var tryDrawFiringConeInfo = AccessTools.Method(typeof(TurretExtensionsUtility), nameof(TurretExtensionsUtility.TryDrawFiringCone),
                    new[] {typeof(Building_Turret), typeof(float)});

                var radRingCount = instructionList.Count(i => HarmonyPatchesUtility.CallingInstruction(i) && (MethodInfo) i.operand == drawRadiusRingInfo);
                var radRingsFound = 0;

                for (var i = 0; i < instructionList.Count; i++)
                {
                    var instruction = instructionList[i];

                    // Look for branching instructions - start looking ahead
                    if (radRingsFound < radRingCount && HarmonyPatchesUtility.BranchingInstruction(instruction))
                    {
                        var j = 1;
                        while (i + j < instructionList.Count)
                        {
                            var xInstructionAhead = instructionList[i + j];

                            // Terminate if another branching instruction is found, or if the branch's destination was reached
                            if (HarmonyPatchesUtility.BranchingInstruction(xInstructionAhead) || xInstructionAhead.labels.Contains((Label) instruction.operand))
                                break;

                            // Look for a call to drawRadiusRing
                            if (HarmonyPatchesUtility.CallingInstruction(xInstructionAhead) && xInstructionAhead.OperandIs(drawRadiusRingInfo))
                            {
#if DEBUG
                                    Log.Message("Building_TurretGun.DrawExtraSelectionOverlays match 1 of 1");
#endif

                                yield return instruction; // num < x or num > x
                                yield return new CodeInstruction(OpCodes.Ldarg_0); // this
                                yield return instructionList[i - 2].Clone(); // num
                                yield return new CodeInstruction(OpCodes.Call, tryDrawFiringConeInfo); // TurretExtensionsUtility.TryDrawFiringCone(this, num)
                                
                                instruction = new CodeInstruction(OpCodes.Brtrue, instruction.operand);
                                radRingsFound++;
                                break;
                            }

                            j++;
                        }
                    }

                    yield return instruction;
                }
            }
        }

        [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.Tick))]
        public static class Tick
        {
            public static void Postfix(Building_TurretGun __instance, LocalTargetInfo ___forcedTarget)
            {
                // If the turret has CompSmartForcedTarget and is attacking a pawn that just got downed, automatically make it target something else
                var smartTargetComp = __instance.TryGetComp<CompSmartForcedTarget>();
                if (smartTargetComp == null || !(___forcedTarget.Thing is Pawn pawn)) return;
                
                if (!pawn.Downed && !smartTargetComp.attackingNonDownedPawn && (!smartTargetComp.Props.onlyApplyWhenUpgraded || __instance.IsUpgraded(out var upgradableComp)))
                {
                    smartTargetComp.attackingNonDownedPawn = true;
                }

                else if (pawn.Downed && smartTargetComp.attackingNonDownedPawn)
                {
                    smartTargetComp.attackingNonDownedPawn = false;
                    NonPublicMethods.Building_TurretGun_ResetForcedTarget(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.SpawnSetup))]
        public static class SpawnSetup
        {
            public static void Postfix(Building_TurretGun __instance, TurretTop ___top)
            {
                // Determine which way the turret initially faces when spawned
                var turretFrameworkExtension = TurretFrameworkExtension.Get(__instance.def);
                switch (turretFrameworkExtension.gunFaceDirectionOnSpawn)
                {
                    case TurretGunFaceDirection.North:
                        NonPublicProperties.TurretTop_set_CurRotation(___top, Rot4.North.AsAngle);
                        break;
                    case TurretGunFaceDirection.East:
                        NonPublicProperties.TurretTop_set_CurRotation(___top, Rot4.East.AsAngle);
                        break;
                    case TurretGunFaceDirection.South:
                        NonPublicProperties.TurretTop_set_CurRotation(___top, Rot4.South.AsAngle);
                        break;
                    case TurretGunFaceDirection.West:
                        NonPublicProperties.TurretTop_set_CurRotation(___top, Rot4.West.AsAngle);
                        break;
                    case TurretGunFaceDirection.Unspecified:
                        break;
                    default:
                        NonPublicProperties.TurretTop_set_CurRotation(___top, __instance.Rotation.AsAngle);
                        break;
                }
            }
        }

        [HarmonyPatch(typeof(Building_TurretGun), "BurstCooldownTime")]
        public static class BurstCooldownTime
        {
            public static void Postfix(Building_TurretGun __instance, ref float __result)
            {
                if (__instance.IsUpgraded(out var upgradableComp)) __result *= upgradableComp.Props.turretBurstCooldownTimeFactor;
            }
        }

        [HarmonyPatch(typeof(Building_TurretGun), "IsValidTarget")]
        public static class IsValidTarget
        {
            public static void Postfix(Building_TurretGun __instance, Thing t, ref bool __result)
            {
                // Cone of fire check
                if (__result && !t.Position.WithinFiringArcOf(__instance))
                    __result = false;
            }
        }

        [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.OrderAttack))]
        public static class OrderAttack
        {
            public static bool Prefix(Building_TurretGun __instance, LocalTargetInfo targ)
            {
                // Cone of fire check
                if (!targ.IsValid || targ.Cell.WithinFiringArcOf(__instance)) return true;
                
                Messages.Message("TurretExtensions.MessageTargetOutsideFiringArc".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
        }

        [HarmonyPatch(typeof(Building_TurretGun), "TryStartShootSomething")]
        public static class TryStartShootSomething
        {
            public static void Postfix(Building_TurretGun __instance, ref int ___burstWarmupTicksLeft)
            {
                ___burstWarmupTicksLeft = TurretExtensionsUtility.AdjustedTurretBurstWarmupTicks(___burstWarmupTicksLeft, __instance);
            }
        }

        [HarmonyPatch(typeof(Building_TurretGun), "CanSetForcedTarget", MethodType.Getter)]
        public static class CanSetForcedTarget
        {
            public static void Postfix(Building_TurretGun __instance, ref bool __result)
            {
                // If the turret isn't mannable, is player-controlled and is set to be able to force target, do so
                if (__instance.Faction != Faction.OfPlayer) return;
                
                var extensionValues = TurretFrameworkExtension.Get(__instance.def);
                var upgradableComp = __instance.TryGetComp<CompUpgradable>();

                // Upgradable comp doesn't exist/isn't upgraded and can force attack, or exists and upgraded and can force attack
                if (upgradableComp.Props.canForceAttack == null || ((upgradableComp.upgraded || !extensionValues.canForceAttack) &&
                                                                    (!upgradableComp.upgraded || !upgradableComp.Props.canForceAttack.Value))) return;

                if (!__instance.def.HasComp(typeof(CompMannable)))
                    __result = true;
                else
                    Log.Warning($"Turret (defName={__instance.def.defName}) has canForceAttack set to true and CompMannable; canForceAttack is redundant in this case.");
            }
        }

        [HarmonyPatch(typeof(Building_TurretGun), nameof(Building_TurretGun.GetInspectString))]
        public static class GetInspectString
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var instructionList = instructions.ToList();

                foreach (var instruction in instructionList)
                {
                    // Cooldown remaining shows on all turret inspect strings instead of just those with a cooldown > 5 seconds
                    if (instruction.opcode == OpCodes.Ldc_R4 && (float) instruction.operand == 5)
                        instruction.operand = float.Epsilon;

                    yield return instruction;
                }
            }
        }
    }
}