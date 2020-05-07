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
    public static class HarmonyPatchesUtility
    {
        public static bool IsFuelCapacityInstruction(this CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Ldfld &&
                   (FieldInfo) instruction.operand == AccessTools.Field(typeof(CompProperties_Refuelable), nameof(CompProperties_Refuelable.fuelCapacity));
        }

        public static bool CallingInstruction(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt;
        }

        public static bool LoadFieldInstruction(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Ldfld || instruction.opcode == OpCodes.Ldflda;
        }

        public static bool BranchingInstruction(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Bge_Un || instruction.opcode == OpCodes.Bge_Un_S || instruction.opcode == OpCodes.Ble_Un || instruction.opcode == OpCodes.Ble_Un_S;
        }
    }
}