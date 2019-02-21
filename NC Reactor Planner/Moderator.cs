﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Media.Media3D;

namespace NC_Reactor_Planner
{
    public class Moderator : Block
    {
        public bool Active { get; set; }
        public ModeratorTypes ModeratorType { get; private set; }
        public double FluxFactor { get; private set; }
        public double EfficiencyFactor { get; private set; }
        public override bool Valid { get => Active & HasAdjacentValidFuelCell; }
        public bool HasAdjacentValidFuelCell { get; private set; }

        public Moderator(string displayName, ModeratorTypes type, Bitmap texture, Point3D position, double fluxFactor, double efficiencyFactor) : base(displayName, BlockTypes.Moderator, texture, position)
        {
            FluxFactor = fluxFactor;
            EfficiencyFactor = efficiencyFactor;
            Active = false;
            HasAdjacentValidFuelCell = false;
            ModeratorType = type;
        }

        public Moderator(Moderator parent, Point3D position) : this(parent.DisplayName, parent.ModeratorType, parent.Texture, position, parent.FluxFactor, parent.EfficiencyFactor)
        {
            ModeratorType = parent.ModeratorType;
        }

        public override void RevertToSetup()
        {
            Active = false;
            HasAdjacentValidFuelCell = false;
        }

        public void UpdateStats()
        {
            foreach (Vector3D offset in Reactor.sixAdjOffsets)
            {
                Tuple<int, BlockTypes> toOffset = WalkLineToValidSource(offset);
                Tuple<int, BlockTypes> oppositeOffset = WalkLineToValidSource(-offset);
                if (toOffset.Item1 > 0 & oppositeOffset.Item1 > 0)
                {
                    Active = true;
                    if (toOffset.Item1 == 1 & toOffset.Item2 == BlockTypes.FuelCell || oppositeOffset.Item1 == 1 & oppositeOffset.Item2 == BlockTypes.FuelCell)
                    {
                        HasAdjacentValidFuelCell = true;
                        return;
                    }
                }
            }
        }

        public Tuple<int, BlockTypes> WalkLineToValidSource(Vector3D offset)
        {
            int i = 0;
            while (++i <= Configuration.Fission.NeutronReach)
            {
                Point3D pos = Position + i * offset;
                Block block = Reactor.BlockAt(pos);
                if (Reactor.interiorDims.X >= pos.X & Reactor.interiorDims.Y >= pos.Y & Reactor.interiorDims.Z >= pos.Z & pos.X > 0 & pos.Y > 0 & pos.Z > 0 & i <= Configuration.Fission.NeutronReach)
                {
                    if (block.BlockType == BlockTypes.FuelCell)
                        if (block.Valid)
                            return Tuple.Create(i, BlockTypes.FuelCell);
                    if(block.BlockType == BlockTypes.Reflector)
                        if(block.Valid & i < Configuration.Fission.NeutronReach / 2 + 1)
                            return Tuple.Create(i, BlockTypes.Reflector);
                    if (block.BlockType != BlockTypes.Moderator)
                        return Tuple.Create(-1, BlockTypes.Air);
                }
                else
                    return Tuple.Create(-1, BlockTypes.Air);
            }
            return Tuple.Create(-1, BlockTypes.Air);
        }

        public override string GetToolTip()
        {
            string toolTip = DisplayName + " moderator\r\n";
            if (Position != Palette.dummyPosition)
            {
                if(!Active)
                    toolTip += "--Inactive!\r\n";
                if(Active)
                    toolTip += "In an active moderator line\r\n";
                if(!HasAdjacentValidFuelCell)
                    toolTip += "Cannot support any heatsinks\r\n";
            }
            toolTip += string.Format("Flux Factor: {0}\r\n", FluxFactor);
            toolTip += string.Format("Efficiency Factor: {0}\r\n", EfficiencyFactor);
            return toolTip;
        }

        public override Block Copy(Point3D newPosition)
        {
            return new Moderator(this, newPosition);
        }

        public override void ReloadValuesFromConfig()
        {
            FluxFactor = Configuration.Moderators[DisplayName].FluxFactor;
            EfficiencyFactor = Configuration.Moderators[DisplayName].EfficiencyFactor;
        }

    }

    public enum ModeratorTypes
    {
        Beryllium,
        Graphite,
        HeavyWater,
        //NotAModerator,
    }
}
