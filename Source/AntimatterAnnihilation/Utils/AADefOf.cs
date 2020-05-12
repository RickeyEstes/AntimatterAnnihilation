﻿using RimWorld;
using Verse;

namespace AntimatterAnnihilation.Utils
{
    [DefOf]
    public static class AADefOf
    {
        static AADefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AADefOf));
        }

        public static DamageDef Annihilate;
        public static DamageDef EnergyBurn;
        public static ThingDef AntimatterCanister;
        public static ThingDef AntimatterComposite;
        public static ThingDef AA_ScissorBlade;
    }
}
