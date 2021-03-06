﻿// Copyright (c) 2009 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using Supremacy.Buildings;
using Supremacy.Economy;
using Supremacy.Game;
using Supremacy.Universe;
using Supremacy.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Supremacy.Scripting.Events
{
    [Serializable]
    public class EarthquakeEvent : UnitScopedEvent<Colony>
    {
        private bool _productionFinished;
        private bool _shipProductionFinished;
        // Update Earthquake 2 March 2019. Likelyhood of Event is in scripedevents.xml. Small balancing changes
        private int _occurrenceChance = 100000;
        

        [NonSerialized]
        private List<BuildProject> _affectedProjects;
        protected List<BuildProject> AffectedProjects
        {
            get
            {
                if (_affectedProjects == null)
                    _affectedProjects = new List<BuildProject>();
                return _affectedProjects;
            }
        }

        private List<Building> _affectedBuildings;
        protected List<Building> AffectedBuildings
        {
            get
            {
                if (_affectedBuildings == null)
                    _affectedBuildings = new List<Building>();
                return _affectedBuildings;
            }
        }

        public EarthquakeEvent()
        {
            _affectedProjects = new List<BuildProject>();
            _affectedBuildings = new List<Building>();
        }

        public override bool CanExecute
        {
            get { return _occurrenceChance > 0 && base.CanExecute; }
        }

        protected override void InitializeOverride(IDictionary<string, object> options)
        {
            object value;

            if (options.TryGetValue("OccurrenceChance", out value))
            {
                try
                {
                    _occurrenceChance = Convert.ToInt32(value);
                }
                catch
                {
                    GameLog.Client.GameData.ErrorFormat(
                        "Invalid OccurrenceChance value for event '{0}': {1}",
                        EventID,
                        value);
                }
            }
        }

        protected override void OnTurnStartedOverride(GameContext game)
        {
            _productionFinished = false;
            _shipProductionFinished = false; // turn off production for this turn
        }

        protected override void OnTurnPhaseFinishedOverride(GameContext game, TurnPhase phase)
        {
            if (phase == TurnPhase.PreTurnOperations)
            {
                var affectedCivs = game.Civilizations
                    .Where(
                        o => o.IsEmpire &&
                             o.IsHuman &&
                             RandomHelper.Chance(_occurrenceChance))
                    .ToList();

                var targetGroups = affectedCivs
                    .Where(CanTargetCivilization)
                    .SelectMany(c => game.Universe.FindOwned<Colony>(c)) // finds colony to affect in the civiliation's empire
                    .Where(CanTargetUnit)
                    .GroupBy(o => o.OwnerID);

                foreach (var group in targetGroups)
                {
                    var productionCenters = group.ToList();

                    var target = productionCenters[RandomProvider.Next(productionCenters.Count)];
                    GameLog.Client.GameData.DebugFormat("EarthquakeEvents.cs: target.Name: {0}", target.Name);

                    var affectedProjects = target.BuildSlots
                    .Concat((target.Shipyard != null) ? target.Shipyard.BuildSlots : Enumerable.Empty<BuildSlot>())
                    .Where(o => o.HasProject && !o.Project.IsPaused && !o.Project.IsCancelled)
                    .Select(o => o.Project);

                    foreach (var affectedProject in affectedProjects)
                    {
                        GameLog.Client.GameData.DebugFormat("EarthquakeEvents.cs: affectedProject: {0}", affectedProject.Description);


                        AffectedProjects.Add(affectedProject);
                    }

                    var targetCiv = target.Owner;
                    int targetColonyId = target.ObjectID;
                    var population = target.Population.CurrentValue;
                    var health = target.Health.CurrentValue;

                    OnUnitTargeted(target);

                    GameContext.Current.Universe.Get<Colony>(targetColonyId).Morale.AdjustCurrent(-5);

                    // Population
                    //Don't reduce the population if it is already low
                    if (population >= 65)
                    {
                        GameContext.Current.Universe.Get<Colony>(targetColonyId).Population.AdjustCurrent(-5);
                    }
                    GameContext.Current.Universe.Get<Colony>(targetColonyId).Population.UpdateAndReset();
                    GameContext.Current.Universe.Get<Colony>(targetColonyId).Health.AdjustCurrent(-(health/6));
                    GameContext.Current.Universe.Get<Colony>(targetColonyId).Health.UpdateAndReset();

                    // Facilities
                    int removeFood = 1; // If you have food 4 or more then take out 1
                    if (target.GetTotalFacilities(ProductionCategory.Food) < 4)
                        removeFood = 0;
                    target.RemoveFacilities(ProductionCategory.Food, removeFood);

                    int removeIndustry = 2;  // If you have industry 8 or more then take out 2
                    if (target.GetTotalFacilities(ProductionCategory.Industry) < 8)
                        removeIndustry = 0;
                    target.RemoveFacilities(ProductionCategory.Industry, removeIndustry);

                    int removeEnergy = 1; ;  // If you have energy 6 or more then take out 1
                    if (target.GetTotalFacilities(ProductionCategory.Energy) < 6)
                        removeEnergy = 0;
                    target.RemoveFacilities(ProductionCategory.Energy, removeEnergy);

                    int removeResearch = 1;   // If you have research 4 or more then take out 1
                    if (target.GetTotalFacilities(ProductionCategory.Research) < 4)
                        removeResearch = 0;
                    target.RemoveFacilities(ProductionCategory.Research, removeResearch);

                    game.CivilizationManagers[targetCiv].SitRepEntries.Add(
                        new ScriptedEventSitRepEntry(
                            new ScriptedEventSitRepEntryData(
                                targetCiv,
                                "EARTHQUAKE_HEADER_TEXT",
                                "EARTHQUAKE_SUMMARY_TEXT",
                                "EARTHQUAKE_DETAIL_TEXT",
                                "vfs:///Resources/Images/ScriptedEvents/Earthquake.png",
                                "vfs:///Resources/SoundFX/ScriptedEvents/Earthquake.wav",
                                () => GameContext.Current.Universe.Get<Colony>(targetColonyId).Name)));

                    GameContext.Current.Universe.UpdateSectors();

                    return;
                }

                if (phase == TurnPhase.Production)
                    _productionFinished = true; // turn production back on
                else if (phase == TurnPhase.ShipProduction)
                    _shipProductionFinished = true;

                if (!_productionFinished || !_shipProductionFinished)
                    return;

                foreach (var affectedProject in AffectedProjects)
                    affectedProject.IsPaused = false;

                AffectedProjects.Clear();
            }
        }
    }
}
