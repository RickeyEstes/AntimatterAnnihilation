﻿using System;
using System.Collections.Generic;
using AntimatterAnnihilation.Utils;
using RimWorld;
using UnityEngine;
using Verse;

namespace AntimatterAnnihilation.Buildings
{
    public class Building_AlloyFusionMachine : Building_TrayPuller
    {
        public const int TICKS_PER_FRAME = 3;
        public const int FRAME_COUNT = 20;
        public const int FRAME_STEP = 3;

        public const int PULL_INTERVAL = 120;
        public const int TICKS_PER_HYPER = 2500; // 1 hour.

        public const int GOLD_PER_HYPER = 4;
        public const int COMPOSITE_PER_HYPER = 3;
        public const int URANIUM_PER_HYPER = 2;

        public const float POWER_DRAW_NORMAL = 4500; // 1x speed
        public const float POWER_DRAW_OVERCLOCKED = POWER_DRAW_NORMAL * 2 + 1000; // 2x speed
        public const float POWER_DRAW_INSANITY = POWER_DRAW_NORMAL * 4 + 5000; // 4x speed

        public const int MAX_GOLD_STORED = GOLD_PER_HYPER * 4;
        public const int MAX_COMPOSITE_STORED = COMPOSITE_PER_HYPER * 4;
        public const int MAX_URANIUM_STORED = URANIUM_PER_HYPER * 4;

        private static Graphic[] activeGraphics;

        private static void LoadGraphics(Building thing)
        {
            activeGraphics = new Graphic[FRAME_COUNT];
            for (int i = 0; i < FRAME_COUNT; i++)
            {
                var gd = thing.DefaultGraphic.data;
                string num = (i * FRAME_STEP).ToString();
                while (num.Length < 4)
                    num = '0' + num;
                activeGraphics[i] = GraphicDatabase.Get(gd.graphicClass, $"AntimatterAnnihilation/Buildings/AlloyFusionMachine/{num}", gd.shaderType.Shader, gd.drawSize, Color.white, Color.white);
            }
        }

        public CompPowerTrader PowerTraderComp
        {
            get
            {
                if (_powerTraderComp == null)
                    this._powerTraderComp = GetComp<CompPowerTrader>();
                return _powerTraderComp;
            }
        }
        private CompPowerTrader _powerTraderComp;
        public override Graphic Graphic
        {
            get
            {
                return activeGraphic ?? base.DefaultGraphic;
            }
        }
        public bool IsActive
        {
            get
            {
                return isActiveInt;
            }
            protected set
            {
                isActiveInt = value;
            }
        }
        public int GoldToFill
        {
            get
            {
                return MAX_GOLD_STORED - storedGold;
            }
        }
        public int CompositeToFill
        {
            get
            {
                return MAX_COMPOSITE_STORED - storedComposite;
            }
        }
        public int UraniumToFill
        {
            get
            {
                return MAX_URANIUM_STORED - storedUranium;
            }
        }

        private Graphic activeGraphic;
        private bool isActiveInt = true;
        private int frameNumber;
        private long tickCount;
        private int outputSide;
        private int storedComposite;
        private int storedGold;
        private int storedUranium;
        private bool hasError;
        private string errorThingKey;
        private byte reasonNotRunning;
        private int ticksUntilOutput = -1;
        private int powerMode;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isActiveInt, "isFusionActive");
            Scribe_Values.Look(ref outputSide, "outputSide");
            Scribe_Values.Look(ref storedComposite, "storedComposite");
            Scribe_Values.Look(ref storedGold, "storedGold");
            Scribe_Values.Look(ref storedUranium, "storedUranium");
            Scribe_Values.Look(ref ticksUntilOutput, "ticksUntilOutput", -1);
            Scribe_Values.Look(ref powerMode, "powerMode");
        }

        public string RotToCardinalString(int rot)
        {
            switch (rot)
            {
                case 0:
                    return "AA.North".Translate();
                case 1:
                    return "AA.East".Translate();
                case 2:
                    return "AA.South".Translate();
                case 3:
                    return "AA.West".Translate();
                default:
                    return $"Unknown rotation: {rot}";
            }
        }

        public override void Tick()
        {
            base.Tick();

            tickCount++;

            // Change power draw based on active status.
            UpdatePowerDraw();

            // Update active/running state.
            UpdateActiveState();

            // Pull resources as long as there is power.
            UpdatePullResources();

            if (!IsActive)
            {
                frameNumber = 0;
                activeGraphic = base.DefaultGraphic;
                hasError = false;
                return;
            }

            // Update output.
            UpdateOutput();

            // Update active state visuals.
            UpdateActiveGraphics();
        }

        private void UpdatePowerDraw()
        {
            PowerTraderComp.PowerOutput = this.IsActive ? -GetActivePowerDraw() : PowerTraderComp.Props.basePowerConsumption;
        }

        public float GetActivePowerDraw()
        {
            switch (powerMode)
            {
                case 0:
                    return POWER_DRAW_NORMAL;
                case 1:
                    return POWER_DRAW_OVERCLOCKED;
                case 2:
                    return POWER_DRAW_INSANITY;
                default:
                    return POWER_DRAW_NORMAL;
            }
        }

        /// <summary>
        /// Gets the speed multiplier based on current power mode.
        /// </summary>
        public int GetActiveSpeed()
        {
            switch (powerMode)
            {
                case 0:
                    return 1;
                case 1:
                    return 2;
                case 2:
                    return 4;
                default:
                    return 1;
            }
        }

        private void UpdatePullResources()
        {
            if (tickCount % PULL_INTERVAL == 0 && PowerTraderComp.PowerOn)
            {
                hasError = false;
                storedUranium += TryPullFromAll("Uranium", UraniumToFill, out bool err);
                if (err)
                {
                    hasError = true;
                    errorThingKey = "AA.Uranium";
                }
                storedGold += TryPullFromAll("Gold", GoldToFill, out err);
                if (err)
                {
                    hasError = true;
                    errorThingKey = "AA.Gold";
                }
                storedComposite += TryPullFromAll("AntimatterComposite_AA", CompositeToFill, out err);
                if (err)
                {
                    hasError = true;
                    errorThingKey = "AA.AntimatterComposite";
                }
            }
        }

        private void UpdateActiveState()
        {
            IsActive = true; // Assume running for now.
            reasonNotRunning = 0;
            if (!PowerTraderComp.PowerOn)
            {
                reasonNotRunning = 1;
                IsActive = false;
            }
            else if (storedGold < GOLD_PER_HYPER || storedComposite < COMPOSITE_PER_HYPER || storedUranium < URANIUM_PER_HYPER)
            {
                reasonNotRunning = 2;
                IsActive = false;
            }
        }

        private void UpdateOutput()
        {
            if (ticksUntilOutput < 0)
            {
                ticksUntilOutput = TICKS_PER_HYPER / GetActiveSpeed();
            }
            if (ticksUntilOutput > 0)
            {
                ticksUntilOutput--;
                if (ticksUntilOutput == 0)
                {
                    // Give output.
                    PlaceOutput(1);

                    // Remove internal resources.
                    storedComposite -= COMPOSITE_PER_HYPER;
                    storedGold -= GOLD_PER_HYPER;
                    storedUranium -= URANIUM_PER_HYPER;

                    // Flag timer to be reset.
                    ticksUntilOutput = -1;
                }
            }
        }

        private void UpdateActiveGraphics()
        {
            if (activeGraphics == null)
            {
                LoadGraphics(this);
            }

            activeGraphic = activeGraphics[frameNumber];
            if (tickCount % TICKS_PER_FRAME == 0)
            {
                frameNumber++;
                if (frameNumber >= FRAME_COUNT)
                    frameNumber = 0;
            }
        }

        public int TryPullFromAll(string defName, int count, out bool hasInOutputSpot)
        {
            if (string.IsNullOrEmpty(defName))
                throw new ArgumentNullException(nameof(defName));

            hasInOutputSpot = false;
            if (count <= 0)
            {
                return 0;
            }

            int remaining = count;
            for (int i = 0; i < 4; i++)
            {
                var tray = GetTray(GetSpotOffset(i));
                if (tray == null)
                    continue;

                if (i == outputSide)
                {
                    // Give player a warning if there is an input item in the output slot.
                    if(TrayHasItem(tray, defName, 0))
                    {
                        hasInOutputSpot = true;
                    }
                    // Don't pull from output side tray.
                    continue;
                }

                int pulled = TryPullFromTray(tray, defName, remaining);
                remaining -= pulled;
                if (remaining <= 0)
                    break;
            }

            int totalGot = count - remaining;
            return totalGot;
        }

        public void PlaceOutput(int count)
        {
            if (count <= 0)
                return;

            Thing thing = ThingMaker.MakeThing(AADefOf.HyperAlloy_AA);
            thing.stackCount = count;

            GenPlace.TryPlaceThing(thing, this.Position + GetSpotOffset(outputSide), Find.CurrentMap, ThingPlaceMode.Near);
        }

        public IntVec3 GetSpotOffset(int rot)
        {
            switch (rot)
            {
                case 0:
                    return IntVec3.North * 2;
                case 1:
                    return IntVec3.East * 2;
                case 2:
                    return IntVec3.South * 2;
                case 3:
                    return IntVec3.West * 2;
                default:
                    return IntVec3.Zero;
            }
        }

        public override string GetInspectString()
        {
            string cardRot = RotToCardinalString(outputSide).CapitalizeFirst();
            string runningStatus;

            if (IsActive)
            {
                runningStatus = "AA.AFMRunning".Translate((ticksUntilOutput / 2500f).ToString("F2"));
            }
            else
            {
                switch (reasonNotRunning)
                {
                    case 1:
                        runningStatus = "AA.AFMNotRunningNoPower".Translate();
                        break;
                    case 2:
                        runningStatus = "AA.AFMNotRunningMissingStuff".Translate(GOLD_PER_HYPER, COMPOSITE_PER_HYPER, URANIUM_PER_HYPER);
                        break;
                    default:
                        runningStatus = "Not Running: Reason unknown.";
                        break;
                }
            }

            return base.GetInspectString() + $"\n{runningStatus}\n{"AA.AFMStoredAmount".Translate(storedGold, MAX_GOLD_STORED, storedComposite, MAX_COMPOSITE_STORED, storedUranium, MAX_URANIUM_STORED)}{(hasError ? $"\n<color=red>{"AA.AFMSlotError".Translate(errorThingKey.Translate().CapitalizeFirst(), cardRot)}</color>" : "")}\n{"AA.AFMOutputSide".Translate(cardRot)}";
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;

            var cmd = new Command_Action();
            cmd.defaultLabel = "AA.AFMChangeOutputSide".Translate();
            cmd.defaultDesc = "AA.AFMChangeOutputSideDesc".Translate();
            cmd.icon = Content.ArrowIcon;
            cmd.iconAngle = outputSide * 90f;
            cmd.action = () =>
            {
                outputSide++;
                if (outputSide >= 4)
                    outputSide = 0;
            };

            yield return cmd;

            var cmd2 = new Command_Action();
            cmd2.defaultLabel = "AA.AFMPowerLevel".Translate();
            cmd2.defaultDesc = "AA.AFMPowerLevelDesc".Translate(GetActiveSpeed() * 100, GetActivePowerDraw());
            cmd2.icon = powerMode == 2 ? Content.PowerLevelHigh : powerMode == 1 ? Content.PowerLevelMedium : Content.PowerLevelLow;
            cmd2.action = () =>
            {
                powerMode++;
                if (powerMode >= 3)
                    powerMode = 0;
            };

            yield return cmd2;
        }
    }
}