﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;

namespace TurretExtensions
{
    public static class Patch_WorkGiver_ConstructDeliverResources
    {
        #region Shared Transpiler

        public static IEnumerable<CodeInstruction> IConstructibleCastCorrecterTranspiler(IEnumerable<CodeInstruction> instructions, OpCode iConstructibleOpcode)
        {
#if DEBUG
            Log.Message("Transpiler start: Patch_WorkGiver_ConstructDeliverResources.IConstructibleCastCorrecterTranspiler (1 match)");
#endif

            var instructionList = instructions.ToList();

            var iConstructibleThingInfo = AccessTools.Method(typeof(Patch_WorkGiver_ConstructDeliverResources), nameof(IConstructibleThing));

            for (var i = 0; i < instructionList.Count; i++)
            {
                var instruction = instructionList[i];

                // Add a call to our helper method before anything that attempts to cast IConstructible
                if (instruction.opcode == iConstructibleOpcode)
                {
                    var nextInstruction = instructionList[i + 1];
                    if (nextInstruction.opcode == OpCodes.Castclass || nextInstruction.opcode == OpCodes.Isinst)
                    {
#if DEBUG
                        Log.Message("Patch_WorkGiver_ConstructDeliverResources.IConstructibleCastCorrecterTranspiler match 1 of 1");
#endif

                        yield return instruction; // c
                        
                        instruction = new CodeInstruction(OpCodes.Call, iConstructibleThingInfo); // IConstructibleThing(c)
                    }
                }

                yield return instruction;
            }
        }

        private static Thing IConstructibleThing(IConstructible constructible)
        {
            if (constructible is CompUpgradable upgradableComp)
                return upgradableComp.parent;

            return constructible as Thing;
        }

        #endregion

        [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "FindNearbyNeeders")]
        public static class FindNearbyNeeders
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return IConstructibleCastCorrecterTranspiler(instructions, OpCodes.Ldarg_3);
            }
        }

        [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ResourceDeliverJobFor")]
        public static class ResourceDeliverJobFor
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return IConstructibleCastCorrecterTranspiler(instructions, OpCodes.Ldarg_2);
            }
        }
    }
}