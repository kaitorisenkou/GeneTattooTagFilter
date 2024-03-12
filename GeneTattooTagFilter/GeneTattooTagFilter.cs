using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection.Emit;
using System.Reflection;

namespace GeneTattooTagFilter {
    [StaticConstructorOnStartup]
    public class GeneTattooTagFilter {
        static GeneTattooTagFilter() {
            Log.Message("[GeneTattooTagFilter] Now active");
            var harmony = new Harmony("kaitorisenkou.GeneTattooTagFilter");
            harmony.Patch(
                AccessTools.Method(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.StyleItemAllowed), null, null),
                new HarmonyMethod(typeof(GeneTattooTagFilter), nameof(Patch_StyleItemAllowed), null),
                null,
                null,
                null
                );
            harmony.Patch(
                AccessTools.Method(typeof(Pawn_GeneTracker), "Notify_GenesChanged", null, null),
                null,
                null,
                new HarmonyMethod(typeof(GeneTattooTagFilter), nameof(Patch_NotifyGenesChanged), null),
                null
                );
            Log.Message("[GeneTattooTagFilter] Harmony patch complete!");
        }


        public static bool Patch_StyleItemAllowed(ref bool __result, Pawn_GeneTracker __instance, StyleItemDef styleItem) {
            if (!(styleItem is TattooDef)) {
                return true;
            }
            if (!ModLister.BiotechInstalled) {
                __result = true;
                return false;
            }
            foreach (var i in __instance.GenesListForReading) {
                if (!i.Active)
                    continue;
                var ext = i.def.GetModExtension<ModExtension_GeneTattooTagFilter>();
                if (ext != null) {
                    var def = styleItem as TattooDef;
                    if ((ext.faceFilter != null && def.tattooType == TattooType.Face && !ext.faceFilter.Allows(styleItem.styleTags)) ||
                        (ext.bodyFilter != null && def.tattooType == TattooType.Body && !ext.bodyFilter.Allows(styleItem.styleTags))) {
                        __result = false;
                        return false;
                    }
                }
            }
            __result = true;
            return false;
        }

        public static IEnumerable<CodeInstruction> Patch_NotifyGenesChanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var instructionList = instructions.ToList();
            int stage = 0;
            MethodInfo targetInfo = AccessTools.Method(typeof(PawnStyleItemChooser), nameof(PawnStyleItemChooser.RandomBeardFor));
            for (int i = 0; i < instructionList.Count; i++) {
                if (stage > 0 && instructionList[i].opcode == OpCodes.Ldloc_0) {
                    var oldLabels = new List<Label>(instructionList[i].labels);
                    Label newLabel = generator.DefineLabel();
                    instructionList[i].labels = new List<Label> { newLabel };
                    var newInstructions = new CodeInstruction[] {
                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Ldfld,AccessTools.Field(AccessTools.TypeByName("RimWorld.Pawn_GeneTracker+<>c__DisplayClass82_0"),"addedOrRemovedGene")),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(GeneTattooTagFilter),nameof(GeneAllowsTattoo))),
                        new CodeInstruction(OpCodes.Brtrue_S,newLabel),
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Stloc_1)

                    };
                    newInstructions[0].labels = oldLabels;
                    instructionList.InsertRange(i, newInstructions);
                    stage++;
                    break;
                }
                if (instructionList[i].opcode == OpCodes.Call && (MethodInfo)instructionList[i].operand == targetInfo) {
                    stage++;
                }
            }
            if (stage < 2) {
                Log.Error("[GeneTattooTagFilter] Patch_DrawHeadHair failed (stage:" + stage + ")");
            }
            return instructionList;
        }
        public static bool GeneAllowsTattoo(GeneDef addedOrRemove, Pawn_GeneTracker geneTracker) {
            var ext = addedOrRemove.GetModExtension<ModExtension_GeneTattooTagFilter>();
            if (ext == null) {
                return true;
            }
            Pawn pawn = geneTracker.pawn;
            bool body = PawnStyleItemChooser.WantsToUseStyle(pawn, pawn.style.BodyTattoo, TattooType.Body);
            bool face = PawnStyleItemChooser.WantsToUseStyle(pawn, pawn.style.FaceTattoo, TattooType.Face);
            if (!body) {
                pawn.style.BodyTattoo =
                    DefDatabase<TattooDef>.AllDefs.Where(t => PawnStyleItemChooser.WantsToUseStyle(pawn, t, TattooType.Body))
                    .RandomElementWithFallback(TattooDefOf.NoTattoo_Body);
            }
            if (!face) {
                pawn.style.FaceTattoo =
                    DefDatabase<TattooDef>.AllDefs.Where(t => PawnStyleItemChooser.WantsToUseStyle(pawn, t, TattooType.Face))
                    .RandomElementWithFallback(TattooDefOf.NoTattoo_Face);
            }
            return body && face;
        }
    }
}
