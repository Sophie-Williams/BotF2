// ColonyHelper.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;
using System.Collections.Generic;

using Supremacy.Economy;
using Supremacy.Game;
using Supremacy.Orbitals;
using Supremacy.Universe;

namespace Supremacy.AI
{
    public static class AIColonyHelper
    {
        public static bool IsInDanger(Colony colony)
        {
            if (colony == null)
                throw new ArgumentNullException("colony");
            return (PlayerAI.GetSectorDanger(colony.Owner, colony.Sector, 2, false) > 0);
        }

        public static BuildProject GetBuildProject(Colony colony)
        {
            if (colony == null)
                throw new ArgumentNullException("colony");
            return colony.BuildSlots[0].Project;
        }

        public static ICollection<Orbital> GetDefenders(Colony colony)
        {
            if (colony == null)
                throw new ArgumentNullException("colony");
            return GameContext.Current.Universe.FindAt(
                colony.Location,
                (Orbital orbital) => (orbital.Owner == colony.Owner));
        }

        public static int GetProjectedPopulation(Colony colony, int numberOfTurns)
        {
            var populationCopy = colony.Population.Clone();

            populationCopy.UpdateAndReset();

            if (populationCopy.IsMaximized)
                return populationCopy.CurrentValue;

            for (int i = 0; (i < numberOfTurns) && !populationCopy.IsMaximized; i++)
                populationCopy.AdjustCurrent(colony.GrowthRate);

            return populationCopy.CurrentValue;
        }
    }
}
