// GalaxyGenerator.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using log4net;
using Supremacy.Collections;
using Supremacy.Data;
using Supremacy.Entities;
using Supremacy.Game;
using Supremacy.Resources;
using Supremacy.Types;
using Supremacy.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace Supremacy.Universe
{
    public static class GalaxyGenerator
    {
        public const double MinDistanceBetweenStars = 1.25;
        public const int MinHomeworldDistanceFromInterference = 2;

        private static TableMap UniverseTables;
        private static StarType LastStarType;

        private static readonly Dictionary<StarType, int> StarTypeDist;
        private static readonly Dictionary<Tuple<StarType, PlanetSize>, int> StarTypeModToPlanetSizeDist;
        private static readonly Dictionary<Tuple<int, PlanetSize>, int> SlotModToPlanetSizeDist;
        private static readonly Dictionary<Tuple<StarType, PlanetType>, int> StarTypeModToPlanetTypeDist;
        private static readonly Dictionary<Tuple<PlanetSize, PlanetType>, int> PlanetSizeModToPlanetTypeDist;
        private static readonly Dictionary<Tuple<int, PlanetType>, int> SlotModToPlanetTypeDist;
        private static readonly Dictionary<Tuple<PlanetSize, MoonSize>, int> PlanetSizeModToMoonSizeDist;
        private static readonly Dictionary<Tuple<PlanetType, MoonSize>, int> PlanetTypeModToMoonSizeDist;

        static GalaxyGenerator()
        {
            UniverseTables = UniverseManager.Tables;

            StarTypeDist = new Dictionary<StarType, int>();
            StarTypeModToPlanetSizeDist = new Dictionary<Tuple<StarType, PlanetSize>, int>();
            StarTypeModToPlanetTypeDist = new Dictionary<Tuple<StarType, PlanetType>, int>();
            SlotModToPlanetSizeDist = new Dictionary<Tuple<int, PlanetSize>, int>();
            SlotModToPlanetTypeDist = new Dictionary<Tuple<int, PlanetType>, int>();
            PlanetSizeModToPlanetTypeDist = new Dictionary<Tuple<PlanetSize, PlanetType>, int>();
            PlanetSizeModToMoonSizeDist = new Dictionary<Tuple<PlanetSize, MoonSize>, int>();
            PlanetTypeModToMoonSizeDist = new Dictionary<Tuple<PlanetType, MoonSize>, int>();


            foreach (var starType in EnumHelper.GetValues<StarType>())
            {
                StarTypeDist[starType] = Number.ParseInt32(UniverseTables["StarTypeDist"][starType.ToString()][0]);
                foreach (var planetSize in EnumHelper.GetValues<PlanetSize>())
                {
                    StarTypeModToPlanetSizeDist[new Tuple<StarType, PlanetSize>(starType, planetSize)] =
                        Number.ParseInt32(
                            UniverseTables["StarTypeModToPlanetSizeDist"][starType.ToString()][planetSize.ToString()]);
                }
                foreach (var planetType in EnumHelper.GetValues<PlanetType>())
                {
                    StarTypeModToPlanetTypeDist[new Tuple<StarType, PlanetType>(starType, planetType)] =
                        Number.ParseInt32(
                            UniverseTables["StarTypeModToPlanetTypeDist"][starType.ToString()][planetType.ToString()]);
                }
            }

            for (var i = 0; i < StarSystem.MaxPlanetsPerSystem; i++)
            {
                foreach (var planetSize in EnumHelper.GetValues<PlanetSize>())
                {
                    SlotModToPlanetSizeDist[new Tuple<int, PlanetSize>(i, planetSize)] =
                        Number.ParseInt32(UniverseTables["SlotModToPlanetSizeDist"][i][planetSize.ToString()]);
                }
                foreach (var planetType in EnumHelper.GetValues<PlanetType>())
                {
                    SlotModToPlanetTypeDist[new Tuple<int, PlanetType>(i, planetType)] =
                        Number.ParseInt32(UniverseTables["SlotModToPlanetTypeDist"][i][planetType.ToString()]);
                }
            }

            foreach (var planetSize in EnumHelper.GetValues<PlanetSize>())
            {
                foreach (var planetType in EnumHelper.GetValues<PlanetType>())
                {
                    PlanetSizeModToPlanetTypeDist[new Tuple<PlanetSize, PlanetType>(planetSize, planetType)] =
                        Number.ParseInt32(
                            UniverseTables["PlanetSizeModToPlanetTypeDist"][planetSize.ToString()][planetType.ToString()
                                ]);
                }
            }

            foreach (var moonSize in EnumHelper.GetValues<MoonSize>())
            {
                foreach (var planetSize in EnumHelper.GetValues<PlanetSize>())
                {
                    PlanetSizeModToMoonSizeDist[new Tuple<PlanetSize, MoonSize>(planetSize, moonSize)] =
                        Number.ParseInt32(
                            UniverseTables["PlanetSizeModToMoonSizeDist"][planetSize.ToString()][moonSize.ToString()]);
                }
                foreach (var planetType in EnumHelper.GetValues<PlanetType>())
                {
                    PlanetTypeModToMoonSizeDist[new Tuple<PlanetType, MoonSize>(planetType, moonSize)] =
                        Number.ParseInt32(
                            UniverseTables["PlanetTypeModToMoonSizeDist"][planetType.ToString()][moonSize.ToString()]);
                }
            }
        }

        private static Collections.CollectionBase<string> GetStarNames()
        {
            var file = new FileStream(
                ResourceManager.GetResourcePath("Resources/Tables/StarNames.txt"),
                FileMode.Open,
                FileAccess.Read);

            var reader = new StreamReader(file);
            var names = new Collections.CollectionBase<string>();

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;

                names.Add(line.Trim());
            }

            return names;
        }

        private static IList<string> GetNebulaNames()
        {
            var file = new FileStream(
                ResourceManager.GetResourcePath("Resources/Tables/NebulaNames.txt"),
                FileMode.Open,
                FileAccess.Read);
            
            var reader = new StreamReader(file);
            var names = new List<string>();
            
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;

                names.Add(line.Trim());
            }

            return names;
        }

        private static int GetMinDistanceBetweenHomeworlds()
        {
            var size = Math.Min(
                GameContext.Current.Universe.Map.Width,
                GameContext.Current.Universe.Map.Height);
            
            // If its an MP game, we want the different Empires to be sufficiently far away from each others
            // TODO Disabled this for now as it turns out that it is still able to fail to place homeworlds.
            // Tried to rework the loop so that either it restarted the placement process or tried to re-place
            // this failing homeworld with a smaller distance requirement. But it keeps failing or crashing.
            /*if (GameContext.Current.IsMultiplayerGame)
            {
                return (int)(0.3f * (float)size);  // min distance would be of 30% of the full map distance, higher than that it starts to be difficult to place all 5 empires
            }*/

            // Ensure empireCount has a positive value to avoid a divide-by-zero error.
            var empireCount = Math.Max(1, GameContext.Current.Civilizations.Count(o => o.IsEmpire));

            return (size / empireCount);
        }

        public static void GenerateGalaxy(GameContext game)
        {
            GameContext.PushThreadContext(game);
            try
            {
                while (true)
                {
                    /* We reload the Universe Tables so that any changes made to the tables
                     * during runtime will be applied without restarting the game.  This
                     * will be useful for tweaking the tables during development.  We can
                     * fall back to using UniverseManager.Tables later on.
                     */
                    UniverseTables = TableMap.ReadFromFile(
                        ResourceManager.GetResourcePath("Resources/Tables/UniverseTables.txt"));

                    var galaxySizes = UniverseTables["GalaxySizes"];

                    var mapSize = new Dimension(
                        Number.ParseInt32(galaxySizes[game.Options.GalaxySize.ToString()]["Width"]),
                        Number.ParseInt32(galaxySizes[game.Options.GalaxySize.ToString()]["Height"]));

                    GameContext.Current.Universe = new UniverseManager(mapSize);

                    var starPositions = GetStarPositions().ToList();
                    var starNames = GetStarNames();

                    starNames.RandomizeInPlace();

                    Collections.CollectionBase<MapLocation> homeLocations;

                    if (!PlaceHomeworlds(starPositions, starNames, out homeLocations))
                        continue;

                    GenerateSystems(starPositions, starNames, homeLocations);
                    PlaceMoons();

                    LinkWormholes();

                    //Find somewhere to place the Bajoran end of the wormhole
                    StarSystem bajoranSystem;
                    MapLocation? bajoranWormholeLocation = null;
                    if (GameContext.Current.Universe.Find<StarSystem>().TryFindFirstItem(s => s.Name == "Bajor", out bajoranSystem))
                    {
                        foreach (var sector in bajoranSystem.Sector.GetNeighbors())
                        {
                            if ((sector.System == null) && (GameContext.Current.Universe.Map.GetQuadrant(sector) == Quadrant.Alpha))
                            {
                                bajoranWormholeLocation = sector.Location;
                                GameLog.Core.GalaxyGenerator.DebugFormat("Place for Bajoran wormhole found at {0}", sector.Location);
                                break;
                            }
                        }
                    }

                    //Find somewhere to place the Gamma end of the wormhole
                    MapLocation? gammaWormholeLocation = null;
                    MapLocation desiredLocation = new MapLocation(GameContext.Current.Universe.Map.Width / 4, GameContext.Current.Universe.Map.Height / 4);
                    if (GameContext.Current.Universe.Map[desiredLocation].System != null)
                    {
                        foreach (var sector in GameContext.Current.Universe.Map[desiredLocation].GetNeighbors())
                        {
                            if (sector.System == null)
                            {
                                gammaWormholeLocation = sector.Location;
                                GameLog.Core.GalaxyGenerator.DebugFormat("Place for Gamma wormhole found at {0}", sector.Location);
                                break;
                            }
                        }
                    }
                    else
                    {
                        gammaWormholeLocation = desiredLocation;
                        GameLog.Core.GalaxyGenerator.DebugFormat("Place for Gamma wormhole found at {0}", desiredLocation);
                    }

                    //We've found somewhere to place the wormholes. Now to actually do it
                    if ((gammaWormholeLocation != null) && (bajoranWormholeLocation != null))
                    {
                        StarSystem bajorWormhole = new StarSystem
                        {
                            StarType = StarType.Wormhole,
                            Name = "Bajoran",
                            WormholeDestination = gammaWormholeLocation,
                            Location = (MapLocation)bajoranWormholeLocation
                        };
                        GameContext.Current.Universe.Objects.Add(bajorWormhole);
                        GameContext.Current.Universe.Map[(MapLocation)bajoranWormholeLocation].System = bajorWormhole;
                        GameLog.Core.GalaxyGenerator.DebugFormat("Bajoran wormhole placed");

                        StarSystem gammaWormhole = new StarSystem
                        {
                            StarType = StarType.Wormhole,
                            Name = "Gamma",
                            WormholeDestination = bajoranWormholeLocation,
                            Location = (MapLocation)gammaWormholeLocation
                        };
                        GameContext.Current.Universe.Objects.Add(gammaWormhole);
                        GameContext.Current.Universe.Map[(MapLocation)gammaWormholeLocation].System = gammaWormhole;
                        GameLog.Core.GalaxyGenerator.DebugFormat("Gamma wormhole placed");
                    }
                    else
                    {
                        GameLog.Core.GalaxyGenerator.DebugFormat("Unable to place Bajoran and/or Gamma wormholes");
                    }

                    break;
                }
            }
            finally
            {
                GameContext.PopThreadContext();
            }
        }

        private static Collections.CollectionBase<MapLocation> GetStarPositions()
        {
            IGalaxyLayout layout;

            var width = GameContext.Current.Universe.Map.Width;
            var height = GameContext.Current.Universe.Map.Height;
            var number = (width * height);

            switch (GameContext.Current.Options.StarDensity)
            {
                case StarDensity.Sparse:
                    number /= 12;
                    break;
                case StarDensity.Medium:
                    number /= 10;
                    break;
                case StarDensity.Dense:
                default:
                    number /= 8;
                    break;
            }

            switch (GameContext.Current.Options.GalaxyShape)
            {
                case GalaxyShape.Ring:
                    layout = new RingGalaxyLayout();
                    break;
                case GalaxyShape.Cluster:
                    layout = new ClusterGalaxyLayout();
                    break;
                case GalaxyShape.Spiral:
                    layout = new SpiralGalaxyLayout();
                    break;
                case GalaxyShape.Elliptical:
                    layout = new EllipticalGalaxyLayout();
                    break;
                default:
                case GalaxyShape.Irregular:
                    layout = new IrregularGalaxyLayout();
                    break;
            }

            ICollection<MapLocation> positions;

            layout.GetStarPositions(out positions, number, width, height);

            var result = new Collections.CollectionBase<MapLocation>(positions.Count);

            positions.CopyTo(result);

            return result;
        }

        public static StarSystemDescriptor GenerateHomeSystem(Civilization civ)
        {
            var system = new StarSystemDescriptor();
            StarType starType;

            while (!(starType = GetStarType()).SupportsPlanets() || (starType == StarType.Nebula))
                continue;

            system.StarType = starType;
            system.Name = civ.HomeSystemName;
            system.Inhabitants = civ.Race.Key;
            system.Bonuses = (civ.CivilizationType == CivilizationType.MinorPower)
                                 ? SystemBonus.RawMaterials
                                 : SystemBonus.Dilithium | SystemBonus.RawMaterials;

            GeneratePlanetsWithHomeworld(system, civ);
            GameLog.Client.GameData.DebugFormat("GalaxyGenerator.cs: No HomeSystem defined - HomeSystemsGeneration will be done for={0}", civ.Name);
            return system;
        }

        private static void SetPlanetNames(StarSystem system)
        {
            if (system == null)
                throw new ArgumentNullException("system");
            for (var i = 0; i < system.Planets.Count; i++)
            {
                if (String.IsNullOrEmpty(system.Planets[i].Name))
                {
                    system.Planets[i].Name = (system.Planets[i].PlanetType == PlanetType.Asteroids)
                                                 ? "Asteroids"
                                                 : system.Name + " " + RomanNumber.Get(i + 1);
                }
            }
        }
        
        private static int GetIdealSlot(StarSystemDescriptor system, PlanetDescriptor planet)
        {
            var bestScore = 0;
            var bestSlot = 0;

            // ReSharper disable PossibleInvalidOperationException

            for (var iSlot = 0; iSlot <= system.Planets.Count; iSlot++)
            {
                var score = GetPlanetSizeScore(system.StarType.Value, planet.Size.Value, iSlot) +
                            GetPlanetTypeScore(system.StarType.Value, planet.Size.Value, planet.Type.Value, iSlot);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = iSlot;
                }
            }

            // ReSharper restore PossibleInvalidOperationException

            return bestSlot;
        }

        private static void FinalizaHomeworldPlacement(IList<string> starNames,
            HomeSystemsDatabase homeSystemDatabase, 
            Civilization civ, 
            MapLocation location)
        {
            var civManager = new CivilizationManager(GameContext.Current, civ);

            GameContext.Current.CivilizationManagers.Add(civManager);

            var homeSystemDescriptor = (homeSystemDatabase.ContainsKey(civ.Key))
                                        ? homeSystemDatabase[civ.Key]
                                        : GenerateHomeSystem(civ);

            var planets = new List<Planet>();
            var race = civ.Race;
            var homeSystem = new StarSystem();

            if (!homeSystemDescriptor.IsNameDefined)
            {
                if (starNames.Count > 0)
                {
                    homeSystemDescriptor.Name = starNames[0];
                    starNames.RemoveAt(0);
                }
                else
                {
                    homeSystemDescriptor.Name = civ.ShortName + " Home System";
                }
            }

            homeSystem.Name = homeSystemDescriptor.Name;
            homeSystem.Location = location;

            if (homeSystemDescriptor.IsInhabitantsDefined)
                race = GameContext.Current.Races[homeSystemDescriptor.Inhabitants];

            if (homeSystemDescriptor.StarType.HasValue)
            {
                homeSystem.StarType = homeSystemDescriptor.StarType.Value;
            }
            else
            {
                StarType starType;
                while (!(starType = GetStarType()).SupportsPlanets())
                    continue;
                homeSystem.StarType = starType;
            }

            if (homeSystemDescriptor.HasBonuses)
                homeSystem.AddBonus(homeSystemDescriptor.Bonuses);

            if (homeSystemDescriptor.Planets.Count == 0)
                GeneratePlanetsWithHomeworld(homeSystemDescriptor, civ);
            else
                GenerateUnspecifiedPlanets(homeSystemDescriptor);

            foreach (var planetDescriptor in homeSystemDescriptor.Planets)
            {
                if (planets.Count >= StarHelper.MaxNumberOfPlanets(homeSystem.StarType))
                    break;

                if (!planetDescriptor.IsSinglePlanet)
                    continue;

                var planet = new Planet();

                if (planetDescriptor.IsNameDefined)
                    planet.Name = planetDescriptor.Name;

                if (planetDescriptor.Size.HasValue)
                    planet.PlanetSize = planetDescriptor.Size.Value;

                if (planetDescriptor.Type.HasValue)
                {
                    if (!planetDescriptor.Size.HasValue)
                    {
                        switch (planetDescriptor.Type)
                        {
                            case PlanetType.Asteroids:
                                planet.PlanetSize = PlanetSize.Asteroids;
                                break;
                            case PlanetType.GasGiant:
                                planet.PlanetSize = PlanetSize.GasGiant;
                                break;
                        }
                    }
                    planet.PlanetType = planetDescriptor.Type.Value;
                }

                if (planetDescriptor.HasBonuses)
                    planet.AddBonus(planetDescriptor.Bonuses);

                planet.Variation = RandomHelper.Random(Planet.MaxVariations);
                planets.Add(planet);
            }

            homeSystem.AddPlanets(planets);

            SetPlanetNames(homeSystem);

            homeSystem.Owner = civ;
            GameContext.Current.Universe.Map[homeSystem.Location].System = homeSystem;

            PlaceBonuses(homeSystem);
            CreateHomeColony(civ, homeSystem, race);

            if (civManager.HomeColony == null)
                civManager.HomeColony = homeSystem.Colony;

            civManager.Colonies.Add(homeSystem.Colony);

            GameContext.Current.Universe.Objects.Add(homeSystem);
            GameContext.Current.Universe.Objects.Add(homeSystem.Colony);
        }

        private static bool PlaceEmpireHomeworlds(List<MapLocation> positions,
            IList<string> starNames,
            HomeSystemsDatabase homeSystemDatabase,
            List<Civilization> empireCivs,
            Collections.CollectionBase<MapLocation> empireHomeLocations,
            List<Civilization> chosenCivs,
            bool mustRespectQuadrants)
           
        {
            var minHomeDistance = GetMinDistanceBetweenHomeworlds();

            for (var index = 0; index < empireCivs.Count; index++)
            {
                
                var localIndex = index;

                int iPosition = positions.FirstIndexWhere((l) =>
                {
                    if (mustRespectQuadrants)
                    {
                        if (GameContext.Current.Universe.Map.GetQuadrant(l) != empireCivs[localIndex].HomeQuadrant)
                            return false;
                    }

                    return empireHomeLocations.All(t => MapLocation.GetDistance(l, t) >= minHomeDistance);
                });

                if (iPosition >= 0)
                {
                     
                    var location = positions[iPosition];
                    if (empireCivs[index].ShortName == "Dominion")
                    {
                        //Place Dominion in upper left one-quater of Gamma quadrant                        
                        MapLocation desiredLocation = new MapLocation(GameContext.Current.Universe.Map.Width / 8, GameContext.Current.Universe.Map.Height / 8);

                        foreach (var sector in GameContext.Current.Universe.Map[desiredLocation].GetNeighbors())
                        {
                            if (sector.System == null)
                            {
                                location = sector.Location;
                                empireHomeLocations.Add(location);
                                //GameLog.Core.GalaxyGenerator.DebugFormat("Upper left 1/8 of map place for Dominion found at {0}", sector.Location);
                            }
                        }
                    }
                    else if (empireCivs[index].ShortName == "Borg")
                    {
                        //Place Borg in upper left one-quater of Gamma quadrant                        
                        MapLocation desiredLocation = new MapLocation((GameContext.Current.Universe.Map.Width - 3), GameContext.Current.Universe.Map.Height / 8);

                        foreach (var sector in GameContext.Current.Universe.Map[desiredLocation].GetNeighbors())
                        {
                            if (sector.System == null)
                            {
                                location = sector.Location;
                                empireHomeLocations.Add(location);
                                //GameLog.Core.GalaxyGenerator.DebugFormat("Upper left 1/8 of map place for Borg found at {0}", sector.Location);
                            }
                        }
                    }
                    else
                    {
                        empireHomeLocations.Add(location);
                    }
                    chosenCivs.Add(empireCivs[index]);

                    GameLog.Core.GalaxyGenerator.DebugFormat("Civilization {0} placed as {2}", empireCivs[index].ShortName, location, empireCivs[index].CivilizationType);

                    positions.RemoveAt(iPosition);

                    FinalizaHomeworldPlacement(starNames, homeSystemDatabase, empireCivs[localIndex], location);
                    
                }
                else
                {
                    GameLog.Core.GalaxyGenerator.WarnFormat("Failed to find a suitable home sector for civilization {0}.  Galaxy generation will start over.",
                        empireCivs[index].ShortName);
                    empireCivs.RemoveAt(index--);
                    return false;
                }
            }

            return true;
        }

        private static void PlaceMinorRaceHomeworlds(List<MapLocation> positions,
            IList<string> starNames,
            HomeSystemsDatabase homeSystemDatabase,
            List<Civilization> minorRaceCivs,
            Collections.CollectionBase<MapLocation> minorHomeLocations,
            List<Civilization> chosenCivs)
        {
            var minorRaceFrequency = GameContext.Current.Options.MinorRaceFrequency;
            var galaxyCanon = GameContext.Current.Options.GalaxyCanon;
            var totalMinorRaces = 0;
            var minorRaces = new Dictionary<Quadrant, List<Civilization>>();
            var randomQuadrant = EnumHelper.GetValues<Quadrant>().FirstOrDefault();
            foreach (var quadrant in EnumHelper.GetValues<Quadrant>())
            {
                minorRaces[quadrant] = new List<Civilization>();
                randomQuadrant = quadrant;
                //GameLog.Core.GalaxyGenerator.DebugFormat("civ = {0}, HomeQuadrant = {1}", civ.Name, civ.HomeQuadrant);   // not finished yet
            }
            foreach (var civ in minorRaceCivs)
            {
                if (galaxyCanon == GalaxyCanon.Canon)
                {
                    if (!minorRaces.ContainsKey(civ.HomeQuadrant) && galaxyCanon == GalaxyCanon.Canon)
                        minorRaces[civ.HomeQuadrant] = new List<Civilization>();
                    minorRaces[civ.HomeQuadrant].Add(civ);
                    totalMinorRaces++;
                }
                else
                {
                    //minorRaces[quadrant] = new List<Civilization>();
                    minorRaces[randomQuadrant].Add(civ);
                    totalMinorRaces++;
                }
                //GameLog.Core.GalaxyGenerator.DebugFormat("civ = {0}, HomeQuadrant = {1}", civ.Name, civ.HomeQuadrant);   // not finished yet
            }

            float minorRacePercentage = 0.25f;
            int minorRaceLimit = 9999;

            var minorRaceTable = GameContext.Current.Tables.UniverseTables["MinorRaceFrequency"];
            if (minorRaceTable != null)
            {
                try
                {
                    var divisor = (double?)minorRaceTable.GetValue(minorRaceFrequency.ToString(), "AvailableSystemsDivisor");
                    if (divisor.HasValue)
                        minorRacePercentage = (float)(1d / divisor.Value);
                }
                catch (Exception e) //ToDo: Just log or additional handling necessary?
                {
                    GameLog.Core.GalaxyGenerator.Error(e);
                }

                try
                {
                    var limit = (int?)minorRaceTable.GetValue(minorRaceFrequency.ToString(), "MaxCount");
                    if (limit.HasValue)
                        minorRaceLimit = limit.Value;
                }
                catch (Exception e) //ToDo: Just log or additional handling necessary?
                {
                    GameLog.Core.GalaxyGenerator.Error(e);
                }
            }

            if (minorRacePercentage <= 0.0f)
                minorRacePercentage = 0.0f;
            else
                minorRacePercentage = Math.Min(1.0f, minorRacePercentage);

            var wantedMinorRaceCount = positions.Count * minorRacePercentage;
            wantedMinorRaceCount = Math.Min(wantedMinorRaceCount, minorRaceLimit);

            // here the minor races are placed
            foreach (var quadrant in EnumUtilities.GetValues<Quadrant>())
                minorRaces[quadrant].RandomizeInPlace();

            var chosenMinorRaces = new Dictionary<Quadrant, List<Civilization>>();
            foreach (var quadrant in EnumHelper.GetValues<Quadrant>())
                chosenMinorRaces[quadrant] = new List<Civilization>();
                
            for (int i = 0; i < wantedMinorRaceCount; i++)
            {
                int smallestMinorRaceCount = int.MaxValue;
                Quadrant quadrantWithLessMinors = EnumHelper.GetValues<Quadrant>().First();

                foreach (var quadrant in EnumHelper.GetValues<Quadrant>())
                {
                    if ((minorRaces[quadrant].Count > 0) && (chosenMinorRaces[quadrant].Count < smallestMinorRaceCount))
                    {
                        smallestMinorRaceCount = chosenMinorRaces[quadrant].Count;
                        quadrantWithLessMinors = quadrant;
                    }
                }

                if (minorRaces[quadrantWithLessMinors].Count == 0)
                {
                    GameLog.Core.GalaxyGenerator.WarnFormat("No more minor race definitions available (create more).  Galaxy generation will stop.");
                    return;
                }

                var minor = minorRaces[quadrantWithLessMinors][0];

                minorRaces[quadrantWithLessMinors].RemoveAt(0);

                // old    
                //int iPosition = positions.FirstIndexWhere(location => GameContext.Current.Universe.Map.GetQuadrant(location) == minor.HomeQuadrant);

                int iPosition = 0;
                if (GameContext.Current.Options.GalaxyCanon == GalaxyCanon.Canon)
                {
                    iPosition = positions.FirstIndexWhere(location => GameContext.Current.Universe.Map.GetQuadrant(location) == minor.HomeQuadrant);
                }
                else  // find random quadrants for minors
                {

                    Random rnd = new Random();
                    int quadrantnumber = rnd.Next(0, 3);

                    var randomQuadrant2 = Quadrant.Alpha;

                    if (quadrantnumber == 0)
                        randomQuadrant2 = Quadrant.Alpha;
                    if (quadrantnumber == 1)
                        randomQuadrant2 = Quadrant.Beta;
                    if (quadrantnumber == 2)
                        randomQuadrant2 = Quadrant.Gamma;
                    if (quadrantnumber == 3)
                        randomQuadrant2 = Quadrant.Delta;

                    iPosition = positions.FirstIndexWhere(location => GameContext.Current.Universe.Map.GetQuadrant(location) == randomQuadrant2);

                    //if (randomQuadrant != minor.HomeQuadrant) // getting a out of range crash from this gamelog so outcommented for now
                    //    GameLog.Client.GalaxyGenerator.DebugFormat("Minor-Quadrant randomize (file/real): {2}/{3} for > {0} placed, positions {1} ",  // no position for data protection
                    //    minor.ShortName, positions[iPosition], minor.HomeQuadrant, GameContext.Current.Universe.Map.GetQuadrant(positions[iPosition]));
                }
        

                if (iPosition >= 0)
                {
                    var chosenLocation = positions[iPosition];

                    if (minor.ShortName == "Bajorans")
                    {
                        //Place Bajor in lower half of Alpha quadrant                        
                        MapLocation desiredLocation = new MapLocation(GameContext.Current.Universe.Map.Width / 4, (GameContext.Current.Universe.Map.Height - 3));

                        foreach (var sector in GameContext.Current.Universe.Map[desiredLocation].GetNeighbors())
                        {
                            if (sector.System == null)
                            {
                                chosenLocation = sector.Location;
                                minorHomeLocations.Add(chosenLocation);
                                //GameLog.Core.GalaxyGenerator.DebugFormat("lower 1/4 of map is place for Bajor at {0}", sector.Location);
                            }
                        }
                    }

                    minorHomeLocations.Add(chosenLocation);
                    chosenCivs.Add(minor);
                    chosenMinorRaces[quadrantWithLessMinors].Add(minor);

                    FinalizaHomeworldPlacement(starNames, homeSystemDatabase, minor, chosenLocation);

                    positions.RemoveAt(iPosition);

                    //GameLog.Print("Civilization {0} placed at location {1}", minor.ShortName, chosenLocation);

                    // hide for data protection   // keep for debugging   
                    //GameLog.Client.GalaxyGenerator.DebugFormat("Minor-Quadrant (file/real): {2}/{3} for > {0} placed at {1} ",
                    //minor.ShortName, chosenLocation, minor.HomeQuadrant, GameContext.Current.Universe.Map.GetQuadrant(chosenLocation));
        }
                else
                {
                    GameLog.Core.GalaxyGenerator.WarnFormat(
                        "Failed to find a suitable home sector for civilization {0}.  Galaxy generation will stop.",
                        minor.ShortName);
                    return;
                }
            }
        }

        private static bool PlaceHomeworlds(List<MapLocation> positions,
            IList<string> starNames,
            out Collections.CollectionBase<MapLocation> homeLocations)
        {
            var homeSystemDatabase = HomeSystemsDatabase.Load();
            var minorRaceFrequency = GameContext.Current.Options.MinorRaceFrequency;
            var empires = new List<Civilization>();
            var minorRaces = new List<Civilization>();

            positions.RandomizeInPlace();

            foreach (var civ in GameContext.Current.Civilizations)
            {
                if (civ.IsEmpire)
                {
                    empires.Add(civ);
                }
                else if (minorRaceFrequency != MinorRaceFrequency.None)
                {
                    minorRaces.Add(civ);
                }
            }

            homeLocations = new Collections.CollectionBase<MapLocation>();
            var chosenCivs = new List<Civilization>();

            bool result = PlaceEmpireHomeworlds(positions, starNames, homeSystemDatabase, empires, homeLocations, chosenCivs, !GameContext.Current.IsMultiplayerGame);
            if (minorRaceFrequency != MinorRaceFrequency.None)
                PlaceMinorRaceHomeworlds(positions, starNames, homeSystemDatabase, minorRaces, homeLocations, chosenCivs);

            var unusedCivs = GameContext.Current.Civilizations.Except(chosenCivs).Select(o => o.CivID).ToHashSet();

            GameContext.Current.Civilizations.RemoveRange(unusedCivs);
            GameContext.Current.CivilizationManagers.RemoveRange(unusedCivs);

            return result;
        }

        private static void PlaceBonuses(StarSystem system)
        {
            if (system == null)
                throw new ArgumentNullException("system");

            /*
             * Dilithium and Raw Materials System Bonuses
             */
            if (system.IsInhabited && system.Colony.Owner.CanExpand)
            {
                system.AddBonus(SystemBonus.Dilithium | SystemBonus.RawMaterials);
            }
            else if (system.HasBonus(SystemBonus.Random))
            {
                if (system.Planets.Any(p => p.PlanetType.IsHabitable()))
                {
                    if (!system.HasDilithiumBonus && RandomHelper.Chance(4))
                        system.AddBonus(SystemBonus.Dilithium);

                    if (!system.HasRawMaterialsBonus && RandomHelper.Chance(3))
                        system.AddBonus(SystemBonus.RawMaterials);
                }
            }

            system.RemoveBonus(SystemBonus.Random);

            var foodPlacementCount = 0;
            var energyPlacementCount = 0;

            foreach (var planet in system.Planets)
            {
                if (planet.HasFoodBonus)
                    ++foodPlacementCount;

                if (planet.HasEnergyBonus)
                    ++energyPlacementCount;
            }

            /*
             * Energy and Food Planet Bonus
             */
            foreach (var planet in system.Planets)
            {
                if (planet.HasBonus(PlanetBonus.Random))
                {
                    if (!planet.HasEnergyBonus && energyPlacementCount < 2)
                    {
                        if (planet.PlanetType == PlanetType.Volcanic && RandomHelper.Chance(2) ||
                            planet.PlanetType == PlanetType.Desert && RandomHelper.Chance(3))
                        {
                            planet.AddBonus(PlanetBonus.Energy);
                            ++energyPlacementCount;
                        }
                    }

                    if (!planet.HasFoodBonus && foodPlacementCount < 2)
                    {
                        if ((planet.PlanetType == PlanetType.Terran ||
                             planet.PlanetType == PlanetType.Oceanic ||
                             planet.PlanetType == PlanetType.Jungle) && RandomHelper.Chance(3))
                        {
                            planet.AddBonus(PlanetBonus.Food);
                            ++foodPlacementCount;
                        }
                    }

                    planet.RemoveBonus(PlanetBonus.Random);
                }
            }
        }

        private static void GeneratePlanetsWithHomeworld(StarSystemDescriptor system, Civilization civ)
        {
            var homePlanet = new PlanetDescriptor();
            PlanetSize planetSize;
            homePlanet.Type = civ.Race.HomePlanetType;
            while (!(planetSize = EnumUtilities.NextEnum<PlanetSize>()).IsHabitable())
                continue;

            if (!system.IsStarTypeDefined)
            {
                while (!(system.StarType = GetStarType()).Value.SupportsPlanets() || (system.StarType.Value == StarType.Nebula))
                    continue;
            }

            homePlanet.Size = planetSize;
            homePlanet.Name = system.Name + " Prime";

            // ReSharper disable PossibleInvalidOperationException
            GeneratePlanets(system, StarHelper.MaxNumberOfPlanets(system.StarType.Value) - 1);
            // ReSharper restore PossibleInvalidOperationException

            system.Planets.Insert(
                GetIdealSlot(system, homePlanet),
                homePlanet);
        }

        private static void GenerateUnspecifiedPlanets(StarSystemDescriptor system)
        {
            GeneratePlanets(system, 0);
        }

        private static int GetDefinedPlanetCount(StarSystemDescriptor system)
        {
            var result = 0;
            foreach (var planetDescriptor in system.Planets)
            {
                if (planetDescriptor.IsSinglePlanet)
                    result++;
            }
            return result;
        }

        private static void GeneratePlanets(StarSystemDescriptor system, int maxNewPlanets)
        {
            int initialCount;
            if (!system.IsStarTypeDefined)
            {
                while (!(system.StarType = GetStarType()).Value.SupportsPlanets() ||
                       (system.StarType.Value == StarType.Nebula))
                    continue;
            }
            for (var i = 0; i < system.Planets.Count; i++)
            {
                if (!system.Planets[i].IsSinglePlanet)
                {
                    var attemptNumber = 0;
                    var newPlanets = 0;
                    var planetDescriptor = system.Planets[i];

                    initialCount = GetDefinedPlanetCount(system);
                    system.Planets.RemoveAt(i--);

                    // ReSharper disable PossibleInvalidOperationException

                    while ((newPlanets < planetDescriptor.MinNumberOfPlanets || attemptNumber < planetDescriptor.MaxNumberOfPlanets) &&
                           initialCount + attemptNumber < StarHelper.MaxNumberOfPlanets(system.StarType.Value))
                    {
                        var planetSize = GetPlanetSize(system.StarType.Value, initialCount);
                        if (planetSize != PlanetSize.NoWorld)
                        {
                            var planet = new PlanetDescriptor
                            {
                                Size = planetSize,
                                Type = GetPlanetType(
                                    system.StarType.Value,
                                    planetSize,
                                    initialCount + attemptNumber)
                            };
                            system.Planets.Insert(++i, planet);
                            newPlanets++;
                        }

                        attemptNumber++;
                    }

                    // ReSharper restore PossibleInvalidOperationException
                }
            }

            // ReSharper disable PossibleInvalidOperationException

            for (var i = 0; i < system.Planets.Count; i++)
            {
                var planetDescriptor = system.Planets[i];
                if (planetDescriptor.IsSinglePlanet)
                {
                    if (!planetDescriptor.IsSizeDefined)
                    {
                        while ((planetDescriptor.Size = GetPlanetSize(system.StarType.Value, i)) == PlanetSize.NoWorld)
                            continue;
                    }

                    if (!planetDescriptor.IsTypeDefined)
                        planetDescriptor.Type = GetPlanetType(system.StarType.Value, planetDescriptor.Size.Value, i);
                }
            }

            initialCount = GetDefinedPlanetCount(system);

            for (var i = 0;
                 (i < maxNewPlanets) &&
                 ((initialCount + i) < StarHelper.MaxNumberOfPlanets(system.StarType.Value));
                 i++)
            {
                var planetSize = GetPlanetSize(system.StarType.Value, initialCount + i);
                if (planetSize != PlanetSize.NoWorld)
                {
                    var planet = new PlanetDescriptor
                    {
                        Size = planetSize,
                        Type = GetPlanetType(system.StarType.Value, planetSize, initialCount + i)
                    };
                    system.Planets.Add(planet);
                }
            }

            // ReSharper restore PossibleInvalidOperationException        
        }

        private static void CreateHomeColony(Civilization civ, StarSystem system, Race inhabitants)
        {
            var civManager = GameContext.Current.CivilizationManagers[civ];
            var colony = new Colony(system, inhabitants);

            colony.Population.BaseValue = (int)(0.6f * system.GetMaxPopulation(inhabitants));
            colony.Population.Reset();
            colony.Name = system.Name;

            system.Colony = colony;
            colony.Morale.BaseValue = civManager.Civilization.BaseMoraleLevel;

            colony.Morale.Reset();

            civManager.MapData.SetExplored(colony.Location, true);
            civManager.MapData.SetScanned(colony.Location, true, 1);

            GameContext.Current.Universe.HomeColonyLookup[civ] = colony;
        }

        private static void GenerateSystems(
            IEnumerable<MapLocation> positions,
            IList<string> starNames,
            IIndexedCollection<MapLocation> homeLocations)
        {
            int maxPlanets;
            var nebulaNames = GetNebulaNames();

            switch (GameContext.Current.Options.PlanetDensity)
            {
                case PlanetDensity.Sparse:
                    maxPlanets = StarSystem.MaxPlanetsPerSystem - 4;
                    break;
                case PlanetDensity.Medium:
                    maxPlanets = StarSystem.MaxPlanetsPerSystem - 2;
                    break;
                default:
                    maxPlanets = StarSystem.MaxPlanetsPerSystem;
                    break;
            }

            nebulaNames.RandomizeInPlace();

            var gameContext = GameContext.Current;

            Parallel.ForEach(
                positions,
                position =>
                {
                    GameContext.PushThreadContext(gameContext);

                    try
                    {
                        var system = new StarSystem();
                        var planets = new List<Planet>();

                        StarType starType;

                        do { starType = GetStarType(); }
                        while (!StarHelper.CanPlaceStar(starType, position, homeLocations));

                        system.StarType = starType;
                        system.Location = position;

                        //Set the name
                        switch (system.StarType)
                        {
                            case StarType.BlackHole:
                                system.Name = "Black Hole";
                                break;
                            case StarType.NeutronStar:
                                system.Name = "Neutron Star";
                                break;
                            case StarType.Quasar:
                                system.Name = "Quasar";
                                break;
                            case StarType.RadioPulsar:
                                system.Name = "Radio Pulsar";
                                break;
                            case StarType.XRayPulsar:
                                system.Name = "X-Ray Pulsar";
                                break;
                            case StarType.Nebula:
                                if (nebulaNames.Count == 0)
                                    break;
                                system.Name = nebulaNames[0];
                                nebulaNames.RemoveAt(0);
                                break;
                            case StarType.Wormhole:
                                if (system.Quadrant == Quadrant.Delta) // No wormholes near Borg in Delta Quadrant
                                {
                                    system.StarType = StarType.BlackHole;
                                    system.Name = "Black Hole";
                                    GameLog.Core.GalaxyGenerator.DebugFormat("BlackHole in place of a Wormhole in Delta quadrant at {0}", system.Location);
                                    break;
                                }
                                GameLog.Core.GalaxyGenerator.DebugFormat("Wormhole placed at {0}", system.Location);
                                break;
                            default:
                                if (starNames.Count == 0)
                                {
                                    system.Name = "System " + system.ObjectID;
                                    break;
                                }
                                system.Name = starNames[0];
                                starNames.RemoveAt(0);
                                break;
                        }

                        // below "SupportsPlanets" doesn't work atm ... so here manually (to get some sector available to COLONIZE)
                        bool _supportPlanets = true;
                        switch (system.StarType)
                        {
                            case StarType.White:
                            case StarType.Blue:
                            case StarType.Yellow:
                            case StarType.Orange:
                            case StarType.Red:
                            case StarType.Nebula:
                                _supportPlanets = true;
                                break;
                            default:
                                _supportPlanets = false;
                                break;
                        }

                                //If the system supports planets, generate them
                                //doesn't work atm:   if (starType.SupportsPlanets())
                        if (_supportPlanets == true)
                        {
                            for (var i = 0; i < maxPlanets - 1; i++)
                            {
                                var planetSize = GetPlanetSize(system.StarType, i);
                                if (planetSize != PlanetSize.NoWorld)
                                {
                                    var planet = new Planet
                                                 {
                                                     PlanetSize = planetSize,
                                                     PlanetType = GetPlanetType(system.StarType, planetSize, i),
                                                     Variation = RandomHelper.Random(Planet.MaxVariations),
                                                     Bonuses = PlanetBonus.Random
                                                 };
                                    //planet.Slot = i;
                                    //if (planet.PlanetType != PlanetType.Asteroids)
                                    //    PlaceMoons(planet);
                                    planets.Add(planet);
                                }
                                if (system.StarType == StarType.Nebula)
                                    break;
                            }

                            if (planets.Count > 0)
                            {
                                var rndSystemBonusType = RandomHelper.Random(8);
                                switch (rndSystemBonusType)
                                {
                                    case 1:
                                        system.AddBonus(SystemBonus.Dilithium);
                                        break;
                                    case 2:
                                    case 3:
                                    case 4:
                                        system.AddBonus(SystemBonus.RawMaterials);
                                        break;
                                    case 5:
                                        system.AddBonus(SystemBonus.Dilithium);
                                        system.AddBonus(SystemBonus.RawMaterials);
                                        break;
                                    default:
                                        break;
                                }
                            }

                            system.AddPlanets(planets);
                            SetPlanetNames(system);
                            PlaceBonuses(system);
                        }

                        GameContext.Current.Universe.Objects.Add(system);
                        GameContext.Current.Universe.Map[position].System = system;
                    }
                    finally
                    {
                        GameContext.PopThreadContext();
                    }
                });
        }

        private static void LinkWormholes()
        {
            var wormholes = GameContext.Current.Universe.Find<StarSystem>(s => s.StarType == StarType.Wormhole).ToList();

            foreach (var wormhole in wormholes)
            {
                //Everything less than Nebula is a proper star
                wormhole.Name = GameContext.Current.Universe.FindNearest<StarSystem>(wormhole.Location,
                    s => s.StarType < StarType.Nebula, false).Name;

                GameLog.Core.GalaxyGenerator.DebugFormat("Wormhole at {0} named {1}", wormhole.Location, wormhole.Name);
            }

            while (wormholes.Count > 1)
            {
                GameContext.Current.Universe.Map[wormholes[0].Sector.Location].System.WormholeDestination = wormholes[1].Sector.Location;
                GameContext.Current.Universe.Map[wormholes[1].Sector.Location].System.WormholeDestination = wormholes[0].Sector.Location;
                GameLog.Core.GalaxyGenerator.DebugFormat("Wormholes at {0} and {1} linked", wormholes[0].Sector.Location, wormholes[1].Sector.Location);
                //Call this twice to remove the first 2 wormholes which are now linked
                wormholes.RemoveAt(0);
                wormholes.RemoveAt(0);
            }
        }

        private static StarType GetStarType()
        {
            var result = StarType.White;
            var maxRoll = 0;

            foreach (var type in EnumUtilities.GetValues<StarType>())
            {
                if (type == LastStarType)
                    continue;
                var currentRoll = RandomHelper.Roll(75 + StarTypeDist[type]);
                if (currentRoll > maxRoll)
                {
                    result = type;
                    LastStarType = type;
                    maxRoll = currentRoll;
                }
                GameLog.Core.GalaxyGenerator.DebugFormat("the GetStarType result = {0} maxRoll = {1}, currentRoll = {2}", result, maxRoll, currentRoll);
            }

            return result;
        }

        private static int GetPlanetSizeScore(StarType starType, PlanetSize planetSize, int slot)
        {
            return RandomHelper.Roll(100)
                   + StarTypeModToPlanetSizeDist[new Tuple<StarType, PlanetSize>(starType, planetSize)]
                   + SlotModToPlanetSizeDist[new Tuple<int, PlanetSize>(slot, planetSize)];
        }

        private static PlanetSize GetPlanetSize(StarType starType, int slot)
        {
            var result = PlanetSize.NoWorld;
            var maxRoll = 0;
            foreach (var size in EnumUtilities.GetValues<PlanetSize>())
            {
                var currentRoll = GetPlanetSizeScore(starType, size, slot);
                if (currentRoll > maxRoll)
                {
                    result = size;
                    maxRoll = currentRoll;
                }
            }
            return result;
        }

        private static PlanetType GetPlanetType(StarType starType, PlanetSize size, int slot)
        {
            if (size == PlanetSize.Asteroids)
                return PlanetType.Asteroids;

            var result = PlanetType.Barren;
            var maxRoll = 0;

            foreach (var type in EnumUtilities.GetValues<PlanetType>())
            {
                var currentRoll = GetPlanetTypeScore(starType, size, type, slot);
                if (currentRoll > maxRoll)
                {
                    result = type;
                    maxRoll = currentRoll;
                }
            }

            return result;
        }

        private static int GetPlanetTypeScore(StarType starType, PlanetSize planetSize, PlanetType planetType, int slot)
        {
            return RandomHelper.Roll(100)
                   + StarTypeModToPlanetTypeDist[new Tuple<StarType, PlanetType>(starType, planetType)]
                   + PlanetSizeModToPlanetTypeDist[new Tuple<PlanetSize, PlanetType>(planetSize, planetType)]
                   + SlotModToPlanetTypeDist[new Tuple<int, PlanetType>(slot, planetType)];
        }

        private static void PlaceMoons()
        {
            var moons = new List<MoonType>(Planet.MaxMoonsPerPlanet);
            foreach (var system in GameContext.Current.Universe.Find<StarSystem>())
            {
                foreach (var planet in system.Planets)
                {
                    var handicap = 0;

                    moons.Clear();

                    if (planet.PlanetType == PlanetType.Asteroids)
                        continue;

                    for (var i = 0; i < Planet.MaxMoonsPerPlanet; i++)
                    {
                        var maxRoll = handicap;
                        var moonSize = MoonSize.NoMoon;

                        foreach (var moon in EnumUtilities.GetValues<MoonSize>())
                        {
                            var currentRoll = RandomHelper.Roll(100)
                                              + PlanetSizeModToMoonSizeDist[new Tuple<PlanetSize, MoonSize>(planet.PlanetSize, moon)]
                                              + PlanetTypeModToMoonSizeDist[new Tuple<PlanetType, MoonSize>(planet.PlanetType, moon)]
                                              - handicap;

                            if (currentRoll > maxRoll)
                            {
                                moonSize = moon;
                                maxRoll = currentRoll;
                            }
                        }

                        if (moonSize != MoonSize.NoMoon)
                            moons.Add(moonSize.GetType(EnumUtilities.NextEnum<MoonShape>()));

                        handicap += (maxRoll / Planet.MaxMoonsPerPlanet);
                    }
                    planet.Moons = moons.ToArray();
                }
            }
        }
    }
}