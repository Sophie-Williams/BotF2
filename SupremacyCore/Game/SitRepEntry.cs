// SitRepEntry.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using Supremacy.Annotations;
using Supremacy.Diplomacy;
using Supremacy.Economy;
using Supremacy.Entities;
using Supremacy.Orbitals;
using Supremacy.Resources;
using Supremacy.Scripting;
using Supremacy.Tech;
using Supremacy.Text;
using Supremacy.Universe;
using Supremacy.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Linq;

namespace Supremacy.Game
{
    #region Base SitRepEntry
    /// <summary>
    /// Serverity of a situation report entry
    /// </summary>
    public enum SitRepPriority
    {
        /// <summary>
        /// A green situation report entry reflects a normal or informal status message.
        /// </summary>
        Green,
        /// <summary>
        /// A yellow situation report entry reflects a status message, where the player should consider to react.
        /// </summary>
        Yellow,
        /// <summary>
        /// A red siutation report entry reflects a urgend status message. The play must react.
        /// </summary>
        Red,
        /// <summary>
        /// A special event, like a battle, or an event.
        /// </summary>
        Special
    }

    [Flags]
    public enum SitRepCategory
    {
        General = 0x00000001,
        NewColony = 0x00000002,
        ColonyStatus = 0x00000004,
        Construction = 0x00000008,
        Resources = 0x00000010,
        Diplomacy = 0x00000020,
        Military = 0x00000040,
        Research = 0x00000080,
        Intelligence = 0x00000100,
        NewInfiltrate = 0x00000200,
        SpecialEvent = 0x00000400,
        FirstContact = 0x00000800,
    }

    /// <summary>
    /// Base class for all SitRep entries.
    /// </summary>
    [Serializable]
    public abstract class SitRepEntry
    {
        private readonly int _ownerId;
        private readonly SitRepPriority _priority;

        /// <summary>
        /// Gets the owner ID.
        /// </summary>
        /// <value>The owner ID.</value>
        protected int OwnerID
        {
            get { return _ownerId; }
        }

        /// <summary>
        /// Gets the owner.
        /// </summary>
        /// <value>The owner.</value>
        public Civilization Owner
        {
            get { return GameContext.Current.Civilizations[_ownerId]; }
        }

        public abstract SitRepCategory Categories { get; }

        /// <summary>
        /// Gets the priority.
        /// </summary>
        /// <value>The priority.</value>
        public SitRepPriority Priority
        {
            get { return _priority; }
        }

        /// <summary>
        /// Gets the summary text.
        /// </summary>
        /// <value>The summary text.</value>
        public abstract string SummaryText { get; }

        /// <summary>
        /// Gets the header text.
        /// </summary>
        /// <value>The header text.</value>
        public virtual string HeaderText
        {
            get { return SummaryText; }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="SitRepEntry"/> has detailed text.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="SitRepEntry"/> has detailed text; otherwise, <c>false</c>.
        /// </value>
        public virtual bool HasDetails
        {
            get { return !string.IsNullOrEmpty(DetailText); }
        }

        public bool HasSoundEffect
        {
            get { return (SoundEffect != null); }
        }

        public virtual string SoundEffect
        {
            get { return null; }
        }

        public virtual string SummaryImage
        {
            get { return null; }
        }

        /// <summary>
        /// Gets the detail text.
        /// </summary>
        /// <value>The detail text.</value>
        public virtual string DetailText
        {
            get { return null; }
        }

        /// <summary>
        /// Gets the detail image path.
        /// </summary>
        /// <value>The detail image path.</value>
        public virtual string DetailImage
        {
            get { return null; }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="SitRepEntry"/> is a priority entry.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="SitRepEntry"/> is a priority entry; otherwise, <c>false</c>.
        /// </value>
        public virtual bool IsPriority
        {
            get { return false; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SitRepEntry"/> class.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="priority">The priority.</param>
        protected SitRepEntry(Civilization owner, SitRepPriority priority)
        {
            if (owner == null)
                throw new ArgumentNullException("owner");
            _ownerId = owner.CivID;
            _priority = priority;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SitRepEntry"/> class.
        /// </summary>
        /// <param name="ownerId">The owner ID.</param>
        /// <param name="priority">The priority.</param>
        protected SitRepEntry(int ownerId, SitRepPriority priority)
        {
            if (ownerId == -1)
                throw new ArgumentException("invalid Civilization ID", "ownerId");
            _ownerId = ownerId;
            _priority = priority;
        }
    }
    #endregion

    #region Science Ship SitRepEntries
    [Serializable]
    public class ScienceShipResearchGainedSitRepEntry : SitRepEntry
    {

        private readonly Ship _scienceShip;
        private readonly int _researchGained;
        private readonly StarType _starType;
        //private readonly string _shipName;
        //private readonly Sector _sector;

        public Ship ScienceShip
        {
            get { return _scienceShip; }
        }

        public Sector Sector
        {
            get { return _scienceShip.Sector; }
        }

        public StarType StarType
        {
            get { return _scienceShip.Sector.System.StarType; }
        }

        public int ResearchGained
        {
            get { return _researchGained; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.Research; }
        }

        public override string SummaryText
        {
            get
            {
                string StarTypeFullText = "";
                    // _starType.ToString;
                switch (StarType)
                {
                    case StarType.Blue:
                    case StarType.Orange:
                    case StarType.Red:
                    case StarType.White:
                    case StarType.Yellow:
                         StarTypeFullText = _starType.ToString() + " star";
                        break;
                    default:
                        StarTypeFullText = _starType.ToString();
                        break;
                }

                return string.Format(ResourceManager.GetString("SITREP_RESEARCH_SCIENCE_SHIP"),
                    ScienceShip.Name, Sector, StarTypeFullText, _researchGained, StarTypeFullText);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public ScienceShipResearchGainedSitRepEntry(
            Civilization owner,
            Ship ScienceShip,
            int researchGained,
            StarType starType) 
            : base(owner, SitRepPriority.Yellow)
        {
            _scienceShip = ScienceShip;
            //_sector = sector;
            _researchGained = researchGained;
            _starType = starType;
        }
    }
    #endregion Science Ship SitRepEnteries

    #region Research SitRepEntries
    [Serializable]
    public class ResearchCompleteSitRepEntry : SitRepEntry
    {
        private readonly int _applicationId;
        private readonly int[] _newDesignIds;

        public ResearchApplication Application
        {
            get { return GameContext.Current.ResearchMatrix.GetApplication(_applicationId); }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.Research; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_RESEARCH_COMPLETED"),
                    ResourceManager.GetString(Application.Name), Application.Level);
            }
        }

        public override bool HasDetails
        {
            get { return true; }
        }

        public override string DetailText
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(ResourceManager.GetString(Application.Description));
                if ((_newDesignIds != null) && (_newDesignIds.Length > 0))
                {
                    sb.Append("[nl/]" + ResourceManager.GetString("SITREP_TECHS_NOW_AVAILABLE") + "[nl/]");
                    for (int i = 0; i < _newDesignIds.Length; i++)
                    {
                        var design = GameContext.Current.TechDatabase[_newDesignIds[i]];
                        if (design == null)
                            continue;
                        sb.Append("[nl/]");
                        sb.Append(ResourceManager.GetString(design.Name));

                    }
                }
                return sb.ToString();
            }
        }

        public override string DetailImage
        {
            get
            {
                var field = Application.Field;
                if (field != null)
                    return field.Image;
                return base.DetailImage;
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public ResearchCompleteSitRepEntry(
            Civilization owner,
            ResearchApplication application,
            ICollection<TechObjectDesign> newDesigns) : base(owner, SitRepPriority.Yellow)
        {
            if (application == null)
                throw new ArgumentNullException("application");
            _applicationId = application.ApplicationID;
            if (newDesigns != null)
            {
                int i = 0;
                _newDesignIds = new int[newDesigns.Count];
                foreach (TechObjectDesign design in newDesigns)
                {
                    _newDesignIds[i++] = design.DesignID;
                }
            }
        }
    }
    #endregion

    [Serializable]
    public class NewColonySitRepEntry : SitRepEntry
    {
        private readonly int _colonyId;

        public Colony Colony
        {
            get { return GameContext.Current.Universe.Objects[_colonyId] as Colony; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.NewColony; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_NEW_COLONY_ESTABLISHED"),
                    Colony.Sector.Name);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        // ReSharper disable SuggestBaseTypeForParameter
        public NewColonySitRepEntry(Civilization owner, Colony colony)
            : base(owner, SitRepPriority.Green)
        {
            if (colony == null)
                throw new ArgumentNullException("colony");
            _colonyId = colony.ObjectID;
        }
        // ReSharper restore SuggestBaseTypeForParameter
    }

    [Serializable]
    public class DeCamouflagedSitRepEntry : SitRepEntry
    {
        private readonly string _name;
        private readonly MapLocation _location;
        private readonly string _shipType;
        private int _scanPower;

        public string Name
        {
            get { return _name; }
        }

        public MapLocation Location
        {
            get { return _location; }
        }

        public Sector Sector
        {
            get { return GameContext.Current.Universe.Map[Location]; }
        }

        /// <summary>
        /// Gets or sets the type of the ship.
        /// </summary>
        /// <value>The type of the ship.</value>
        public string ShipType
        {
            get { return _shipType; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.Military; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(
                    ResourceManager.GetString("SITREP_SHIP_DECAMOUFLAGED"),
                    ResourceManager.GetString(Name),
                    _shipType,
                    _location,
                    _scanPower);
            }
        }

        public override bool IsPriority
        {
            get { return false; }
        }

        // ReSharper disable SuggestBaseTypeForParameter
        public DeCamouflagedSitRepEntry(Orbital orbital, int scanpower)
            : base(orbital.Owner, SitRepPriority.Yellow)
        {
            if (orbital == null)
                throw new ArgumentNullException("orbital");
            _name = orbital.Name;
            _shipType = orbital.OrbitalDesign.ShipType;
            _location = orbital.Location;
            _scanPower = scanpower;
        }
        // ReSharper restore SuggestBaseTypeForParameter
    }

    [Serializable]
    public class ItemBuiltSitRepEntry : SitRepEntry
    {
        private readonly int _itemTypeId;
        private readonly MapLocation _location;

        public TechObjectDesign ItemType
        {
            get { return GameContext.Current.TechDatabase[_itemTypeId]; }
        }

        public MapLocation Location
        {
            get { return _location; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.Construction; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_CONSTRUCTED_I"),
                    ResourceManager.GetString(ItemType.Name),
                    GameContext.Current.Universe.Map[Location].Name);

            }
        }

        public ItemBuiltSitRepEntry(Civilization owner, TechObjectDesign itemType, MapLocation location)
            : base(owner, SitRepPriority.Green)
        {
            if (itemType == null)
                throw new ArgumentNullException("itemType");
            _itemTypeId = itemType.DesignID;
            _location = location;
        }
    }

    [Serializable]
    public class BuildingBuiltSitRepEntry : ItemBuiltSitRepEntry
    {
        private readonly bool _isActive;

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_CONSTRUCTED_UNPOWERED"),
                    ResourceManager.GetString(ItemType.Name),
                    GameContext.Current.Universe.Map[Location].Name,
                    _isActive ? "" : " (unpowered)");
            }
        }

        public BuildingBuiltSitRepEntry(Civilization owner, TechObjectDesign itemType, MapLocation location, bool isActive)
            : base(owner, itemType, location)
        {
            _isActive = isActive;
        }
    }

    #region Intelligence SitRepEntries
    [Serializable]
    public class FoodReservesDestroyedTargetSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _destroyedFoodReserves;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_FOOD_RESERVES_DESTROYED"),
                    _destroyedFoodReserves, System.Name);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public FoodReservesDestroyedTargetSitRepEntry(Civilization owner, Colony target, int destroyedFoodReserves)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");
            _systemId = target.System.ObjectID;
            _destroyedFoodReserves = destroyedFoodReserves;
        }
    }
    [Serializable]
    public class FoodReservesDestroyedAttackerSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _destroyedFoodReserves;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_FOOD_RESERVES_DESTROYED_SUCCESSFULLY"),
                    _destroyedFoodReserves, System.Name);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public FoodReservesDestroyedAttackerSitRepEntry(Civilization owner, Colony target, int destroyedFoodReserves)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");
            _systemId = target.System.ObjectID;
            _destroyedFoodReserves = destroyedFoodReserves;
        }
    }

    [Serializable]
    public class PlanetaryDefenceAttackTargetSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _orbitalBatteriesDestroyed;
        private readonly int _shieldHealthRemoved;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_SABOTEURS_ATTACKED_PLANETARY_DEFENCES"),
                    System.Name, _orbitalBatteriesDestroyed, _shieldHealthRemoved);
                //Saboteurs have attacked the planetary defences at { 0}, destroying { 1} orbital batteries and damaged the planetary shields by { 2}.
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public PlanetaryDefenceAttackTargetSitRepEntry(Civilization owner, Colony target, int orbitalBatteriesDestroyed, int shieldHealthRemoved)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");
            _systemId = target.System.ObjectID;
            _orbitalBatteriesDestroyed = orbitalBatteriesDestroyed;
            _shieldHealthRemoved = shieldHealthRemoved;
        }
    }

    [Serializable]
    public class PlanetaryDefenceAttackAttackerSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _orbitalBatteriesDestroyed;
        private readonly int _shieldHealthRemoved;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_SABOTEURS_ATTACKED_PLANETARY_DEFENCES_SUCCESSFULLY"),
                    System.Name, _orbitalBatteriesDestroyed, _shieldHealthRemoved);
                //Our agents have attacked the planetary defences at { 0}, destroying { 1} orbital batteries and damaged the planetary shields by { 2}.
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public PlanetaryDefenceAttackAttackerSitRepEntry(Civilization owner, Colony target, int orbitalBatteriesDestroyed, int shieldHealthRemoved)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");
            _systemId = target.System.ObjectID;
            _orbitalBatteriesDestroyed = orbitalBatteriesDestroyed;
            _shieldHealthRemoved = shieldHealthRemoved;
        }
    }

    [Serializable]
    public class IntelAttackFailedSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_AGENTS_FAILED"),
                    //"Our agents have failed in their mission on {0}",
                    System.Name);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public IntelAttackFailedSitRepEntry(Civilization owner, Colony target)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");
            _systemId = target.System.ObjectID;
        }
    }

    [Serializable]
    public class IntelDefenceSucceededSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_AGENTS_FOILED_PLOT"),
                    //"Our agents have foiled a plot by spies on {0}",
                    System.Name);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public IntelDefenceSucceededSitRepEntry(Civilization owner, Colony target)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");
            _systemId = target.System.ObjectID;
        }
    }

    [Serializable]
    public class CreditsStolenTargetSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _creditsStolen;

        public StarSystem Target
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_CREDITS_WERE_STOLEN"),
                    _creditsStolen, Target.Name);

                // {0} credits were stolen from our treasury on { 1}.
            }
        }
        public override bool IsPriority
        {
            get { return true; }
        }

        public CreditsStolenTargetSitRepEntry(Civilization owner, Colony target, int creditsStolen)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");
            _systemId = target.System.ObjectID;
            _creditsStolen = creditsStolen;
        }
    }

    [Serializable]
    public class CreditsStolenAttackerSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _creditsStolen;

        public StarSystem Target
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_CREDITS_STOLEN_SUCCESSFULLY"),
                    //"Our agents stole {0} credits from the treasury on {1}.",
                    _creditsStolen, Target.Name);
            }
        }
        public override bool IsPriority
        {
            get { return true; }
        }

        public CreditsStolenAttackerSitRepEntry(Civilization owner, Colony target, int creditsStolen)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");
            _systemId = target.System.ObjectID;
            _creditsStolen = creditsStolen;
        }
    }

    [Serializable]
    public class TradeRouteCreditsStolenAttackerSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _lostCredits;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_TRADE_ROUTES_STOLEN_WORTH_SUCCESSFULLY"),
                    //"We have stolen {0} worth of goods from the trade routes on {1}",
                    _lostCredits, System.Name);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public TradeRouteCreditsStolenAttackerSitRepEntry(Civilization owner, Colony target, int lostCredits)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");
            _systemId = target.System.ObjectID;
            _lostCredits = lostCredits;
        }
    }

    [Serializable]
    public class TradeRouteCreditsStolenTargetSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _lostCredits;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_TRADE_ROUTES_STOLEN_WORTH"),
                    //"{0} credits worth of goods have been stolen from our trade routes on {1}",
                    _lostCredits, System.Name);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public TradeRouteCreditsStolenTargetSitRepEntry(Civilization owner, Colony target, int lostCredits)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");
            _systemId = target.System.ObjectID;
            _lostCredits = lostCredits;
        }
    }

    [Serializable]
    public class ProductionFacilitiesDestroyedTargetSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly ProductionCategory _facilityType;
        private readonly int _destroyedFacilities;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                switch (_facilityType)
                {
                    case ProductionCategory.Energy:
                        return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_FACILITIES_SABOTAGED_ENERGY"),
                            //"{0} energy facilities have been sabotaged on {1}",
                            _destroyedFacilities, System.Name);
                    case ProductionCategory.Food:
                        return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_FACILITIES_SABOTAGED_FOOD"),
                            //"{0} food facilities have been sabotaged on {1}",
                            _destroyedFacilities, System.Name);
                    case ProductionCategory.Industry:
                        return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_FACILITIES_SABOTAGED_INDUSTRY"),
                            //"{0} industrial facilities have been sabotaged on {1}",
                            _destroyedFacilities, System.Name);
                    case ProductionCategory.Intelligence:
                        return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_FACILITIES_SABOTAGED_INTELLIGENCE"),
                            //"{0} intelligence facilities have been sabotaged on {1}",
                            _destroyedFacilities, System.Name);
                    case ProductionCategory.Research:
                        return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_FACILITIES_SABOTAGED_RESEARCH"),
                            //"{0} research facilities have been sabotaged on {1}",
                            _destroyedFacilities, System.Name);
                    default:
                        return null;
                }

            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public ProductionFacilitiesDestroyedTargetSitRepEntry(Civilization owner, Colony target, ProductionCategory productionType, int destroyedFacilities)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");

            _systemId = target.System.ObjectID;
            _facilityType = productionType;
            _destroyedFacilities = destroyedFacilities;
        }
    }

    [Serializable]
    public class ProductionFacilitiesDestroyedAttackerSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly ProductionCategory _facilityType;
        private readonly int _destroyedFacilities;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                switch (_facilityType)
                {
                    case ProductionCategory.Energy:
                        return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_FACILITIES_SABOTAGED_ENERGY"),
                            //"We have sabotaged {0} energy facilities on {1}",
                            _destroyedFacilities, System.Name);
                    case ProductionCategory.Food:
                        return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_FACILITIES_SABOTAGED_FOOD"),
                            //"We have sabotaged {0} food facilities  on {1}",
                            _destroyedFacilities, System.Name);
                    case ProductionCategory.Industry:
                        return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_FACILITIES_SABOTAGED_INDUSTRY"),
                            //"We have sabotaged {0} industrial facilities on {1}",
                            _destroyedFacilities, System.Name);
                    case ProductionCategory.Intelligence:
                        return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_FACILITIES_SABOTAGED_INTELLIGENCE"),
                            //"We have sabotaged {0} intelligence facilities on {1}",
                            _destroyedFacilities, System.Name);
                    case ProductionCategory.Research:
                        return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_FACILITIES_SABOTAGED_RESEARCH"),
                            //"We have sabotaged {0} research facilities on {1}",
                            _destroyedFacilities, System.Name);
                    default:
                        return null;
                }

            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public ProductionFacilitiesDestroyedAttackerSitRepEntry(Civilization owner, Colony target, ProductionCategory productionType, int destroyedFacilities)
            : base(owner, SitRepPriority.Red)
        {
            if (target == null)
                throw new ArgumentNullException("colony");

            _systemId = target.System.ObjectID;
            _facilityType = productionType;
            _destroyedFacilities = destroyedFacilities;
        }
    }

    [Serializable]
    public class NewInfiltrateSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _gainedResearchPointsSum;
        private readonly int _gainedOfTotalResearchPoints;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                if (_gainedResearchPointsSum > 0)
                {
                    return string.Format(ResourceManager.GetString("SITREP_INFILTRATE_SUCCESSFULLY"),
                        //"Our spies have infiltrated the {0} at {1} and gained {2} of {3} research points.",
                        System.Owner, System.Name, _gainedResearchPointsSum, _gainedOfTotalResearchPoints);
                }
                else
                {
                    return string.Format(ResourceManager.GetString("SITREP_INFILTRATE_NO_SUCCESS"),
                        //"Our spies have tried to infiltrate the {0} at {1} but they had no success.",
                        System.Owner, System.Name);
                }
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public NewInfiltrateSitRepEntry(Civilization owner, Colony colony, int gainedResearchPointsSum, int gainedOfTotalResearchPoints)
            : base(owner, SitRepPriority.Red)
        {
            if (colony == null)
                throw new ArgumentNullException("colony");
            _systemId = colony.System.ObjectID;

            _gainedResearchPointsSum = gainedResearchPointsSum;
            _gainedOfTotalResearchPoints = gainedOfTotalResearchPoints;
        }
    }

    [Serializable]
    public class NewRaidSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _gainedCredits;
        private readonly int _totalCredits;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                if (_gainedCredits > 0)
                {
                    return string.Format(ResourceManager.GetString("SITREP_RAID_SUCCESSFULLY"),
                        //"The {0} at {1} have been raided: we got {2} of {3} credits.",
                        System.Owner, System.Name, _gainedCredits, _totalCredits);
                }
                else
                {
                    return string.Format(ResourceManager.GetString("SITREP_RAID_NO_SUCCESS"),
                        //"Our spies have tried to raid the {0} at {1} but they had no success.",
                        System.Owner, System.Name);
                }
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public NewRaidSitRepEntry(Civilization owner, Colony colony, int gainedCredits, int totalCredits)
            : base(owner, SitRepPriority.Red)
        {
            if (colony == null)
                throw new ArgumentNullException("colony");
            _systemId = colony.System.ObjectID;

            _gainedCredits = gainedCredits;
            _totalCredits = totalCredits;
        }
    }

    [Serializable]
    public class NewInfluenceSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _gainedCreditsSum;
        private readonly int _gainedOfTotalCredits;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                if (_gainedCreditsSum > 0)
                {
                    return string.Format(ResourceManager.GetString("SITREP_INFLUENCE_NO_SUCCESS"),
                        //"The {0} at {1} have been influenced: we got {2} of {3} credits.",
                        System.Owner, System.Name, _gainedCreditsSum, _gainedOfTotalCredits);
                }
                else
                {
                    return string.Format(ResourceManager.GetString("SITREP_INFLUENCE_NO_SUCCESS"),
                        //"Our spies have tried to influence the {0} at {1} but they had no success.",
                        System.Owner, System.Name);
                }
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public NewInfluenceSitRepEntry(Civilization owner, Colony colony, int gainedCreditsSum, int gainedOfTotalCredits)
            : base(owner, SitRepPriority.Red)
        {
            if (colony == null)
                throw new ArgumentNullException("colony");
            _systemId = colony.System.ObjectID;

            _gainedCreditsSum = gainedCreditsSum;
            _gainedOfTotalCredits = gainedOfTotalCredits;
        }
    }

    [Serializable]
    public class NewSabotageSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;
        private readonly int _removeEnergyFacilities;
        private readonly int _totalEnergyFacilities;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                if (_removeEnergyFacilities > 0)
                {
                    return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_SUCCESS"),
                    //"Successful sabotage mission to {0} {1}, (ship lost in action): {2} of {3} energy facilities destroyed.",
                       System.Owner, System.Location, _removeEnergyFacilities, _totalEnergyFacilities + _removeEnergyFacilities);
                }
                else
                {
                    return string.Format(ResourceManager.GetString("SITREP_SABOTAGE_FAILED"),
                        //"The sabotage mission to {0} at {1} failed and the sabotage ship was lost.",
                        System.Owner, System.Name);
                }
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public NewSabotageSitRepEntry(Civilization owner, Colony colony, int removeEnergyFacilities, int totalEnergyFacilities)
            : base(owner, SitRepPriority.Red)
        {
            if (colony == null)
                throw new ArgumentNullException("colony");
            _systemId = colony.System.ObjectID;

            _removeEnergyFacilities = removeEnergyFacilities;
            _totalEnergyFacilities = totalEnergyFacilities;
        }
    }
    #endregion

    [Serializable]
    public class StarvationSitRepEntry : SitRepEntry
    {
        private readonly int _systemId;

        public StarSystem System
        {
            get { return GameContext.Current.Universe.Objects[_systemId] as StarSystem; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_STARVATION"),
                    System.Name);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public StarvationSitRepEntry(Civilization owner, Colony colony)
            : base(owner, SitRepPriority.Red)
        {
            if (colony == null)
                throw new ArgumentNullException("colony");
            _systemId = colony.System.ObjectID;
        }
    }

    [Serializable]
    public class NegativeTreasurySitRepEntry : SitRepEntry
    {
        public override SitRepCategory Categories
        {
            get { return SitRepCategory.General; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_NEGATIVE_TREASURY"));
                //return "Your empire is out of funds and cannot pay its ship's maintenance.\nShips cannot repair hull damage and are degrading.";
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public NegativeTreasurySitRepEntry(Civilization owner)
            : base(owner, SitRepPriority.Red)
        {
        }
    }

    [Serializable]
    public class OrbitalDestroyedSitRepEntry : SitRepEntry
    {
        private readonly string _name;
        private readonly MapLocation _location;
        private readonly string _shipType;

        public string Name
        {
            get { return _name; }
        }

        public MapLocation Location
        {
            get { return _location; }
        }

        public Sector Sector
        {
            get { return GameContext.Current.Universe.Map[Location]; }
        }

        /// <summary>
        /// Gets or sets the type of the ship.
        /// </summary>
        /// <value>The type of the ship.</value>
        public string ShipType
        {
            get { return _shipType; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.Military; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(
                    ResourceManager.GetString("SITREP_ORBITAL_DESTROYED"),
                    ResourceManager.GetString(Name),
                    _shipType,
                    Location);
            }
        }

        public override bool IsPriority
        {
            get { return false; }
        }

        // ReSharper disable SuggestBaseTypeForParameter
        public OrbitalDestroyedSitRepEntry(Orbital orbital)
            : base(orbital.Owner, SitRepPriority.Yellow)
        {
            if (orbital == null)
                throw new ArgumentNullException("orbital");
            _name = orbital.Name;
            _shipType = orbital.OrbitalDesign.ShipType;
            _location = orbital.Location;
        }
        // ReSharper restore SuggestBaseTypeForParameter
    }

    #region Diplomatic SitRepEntries
    [Serializable]
    public class FirstContactSitRepEntry : SitRepEntry
    {
        private readonly Civilization _civilization;
        private readonly MapLocation _location;

        public Civilization Civilization
        {
            get { return _civilization; }
        }

        public MapLocation Location
        {
            get { return _location; }
        }

        public Sector Sector
        {
            get { return GameContext.Current.Universe.Map[Location]; }
        }

        public override bool HasDetails
        {
            get { return true; }
        }

        public override string DetailImage
        {
            get { return Civilization.Image; }
        }

        public override string DetailText
        {
            get { return Civilization.DiplomacyReport ?? Civilization.Race.Description; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.Diplomacy | SitRepCategory.FirstContact; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_FIRST_CONTACT"),
                    ResourceManager.GetString(Civilization.ShortName), Sector);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public FirstContactSitRepEntry(Civilization owner, Civilization civilization, MapLocation location)
            : base(owner, SitRepPriority.Yellow)
        {
            if (civilization == null)
                throw new ArgumentNullException("civilization");
            _civilization = civilization;
            _location = location;
        }
    }

    [Serializable]
    public class WarDeclaredSitRepEntry : SitRepEntry
    {
        private readonly Civilization _victim;
        private readonly Civilization _aggressor;
        private readonly CivString _detailText;

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.Diplomacy | SitRepCategory.Military; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_WAR_DECLARED"),
                    Aggressor.LongName, Victim.LongName);
            }
        }

        public override bool HasDetails
        {
            get { return ((Aggressor == Owner) || (Victim == Owner)); }
        }

        public override string DetailImage
        {
            get
            {
                return (Owner == Aggressor)
                    ? Victim.InsigniaPath
                    : Aggressor.InsigniaPath;
            }
        }

        public override string DetailText
        {
            get { return string.Format(_detailText.Value, Victim.LongName); }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public Civilization Victim
        {
            get { return _victim; }
        }

        public Civilization Aggressor
        {
            get { return _aggressor; }
        }

        public WarDeclaredSitRepEntry(Civilization owner, Civilization victim) : this(owner, owner, victim) { }

        public WarDeclaredSitRepEntry(Civilization owner, Civilization aggressor, Civilization victim)
            : base(owner, SitRepPriority.Red)
        {
            if (aggressor == null)
                throw new ArgumentNullException("aggressor");
            if (victim == null)
                throw new ArgumentNullException("victim");

            _victim = victim;
            _aggressor = aggressor;

            if (aggressor == owner || victim == owner)
            {
                _detailText = new CivString(
                    owner,
                    CivString.DiplomacyCategory,
                    owner == aggressor
                        ? "MESSAGE_SITREP_DETAILS_WAR_DECLARATION_US"
                        : "MESSAGE_SITREP_DETAILS_WAR_DECLARATION_THEM");
            }
            if (owner.Key == "BORG" && owner == aggressor)
            {
                _detailText = new CivString(owner, CivString.DiplomacyCategory,"MESSAGE_SITREP_RESISTANCE_IS_FUTILE");
            }
        }
    }

    [Serializable]
    public sealed class DiplomaticSitRepEntry : SitRepEntry
    {
        private readonly IDiplomaticExchange _exchange;

        [NonSerialized]
        private string _summaryText;
        [NonSerialized]
        private string _detailText;
        [NonSerialized]
        private string _image;
        [NonSerialized]
        private bool _hasEvaluatedSummaryText;
        [NonSerialized]
        private bool _hasEvaluatedDetailText;

        public DiplomaticSitRepEntry(Civilization owner, [NotNull] IDiplomaticExchange exchange)
            : base(owner.CivID, SitRepPriority.Special)
        {
            if (exchange == null)
                throw new ArgumentNullException("exchange");

            _exchange = exchange;
        }

        private string EnsureText(ref string text, ref bool resolved, bool detailed)
        {
            if (resolved)
                return text;

            var key = ResolveTextKey(detailed);
            if (key == null)
            {
                resolved = true;
                text = null;
                return text;
            }

            LocalizedTextGroup textGroup;
            LocalizedString localizedString;

            if (!LocalizedTextDatabase.Instance.Groups.TryGetValue(typeof(DiplomacySitRepStringKey), out textGroup) ||
                !textGroup.Entries.TryGetValue(key.Value, out localizedString))
            {
                resolved = true;
                text = string.Format("!!! MISSING TEXT: {0}.{1} !!!", typeof(DiplomacySitRepStringKey).Name, key);
                return text;
            }

            var scriptParameters = new ScriptParameters(
                new ScriptParameter("$sender", typeof(Civilization)),
                new ScriptParameter("$recipient", typeof(Civilization)));

            var scriptExpression = new ScriptExpression(returnObservableResult: false)
            {
                Parameters = scriptParameters,
                ScriptCode = StringHelper.QuoteString(localizedString.LocalText)
            };

            Civilization sender;
            Civilization recipient;

            var response = _exchange as IResponse;
            if (response != null)
            {
                switch (response.ResponseType)
                {
                    case ResponseType.Accept:
                    case ResponseType.Reject:
                        sender = response.Proposal.Sender;
                        recipient = response.Proposal.Recipient;
                        break;
                    case ResponseType.Counter:
                        sender = response.CounterProposal.Sender;
                        recipient = response.CounterProposal.Recipient;
                        break;
                    default:
                        resolved = true;
                        text = string.Format("!!! ERROR: UNEXPECTED RESPONSE TYPE: {0} !!!", response.ResponseType);
                        return text;
                }
            }
            else
            {
                sender = _exchange.Sender;
                recipient = _exchange.Recipient;
            }

            var parameters = new RuntimeScriptParameters
                             {
                                 new RuntimeScriptParameter(scriptParameters[0], sender),
                                 new RuntimeScriptParameter(scriptParameters[1], recipient)
                             };

            return scriptExpression.Evaluate<string>(parameters);
        }

        private DiplomacySitRepStringKey? ResolveTextKey(bool detailed)
        {
            var proposal = _exchange as IProposal;
            var response = _exchange as IResponse;

            if (proposal == null && response != null && response.ResponseType == ResponseType.Counter)
                proposal = response.CounterProposal;

            if (proposal != null)
            {
                if (proposal.HasTreaty())
                {
                    if (proposal.HasClause(ClauseType.TreatyCeaseFire))
                        return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.CeaseFireProposedSummaryText;
                    if (proposal.HasClause(ClauseType.TreatyNonAggression))
                        return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.NonAggressionPactProposedSummaryText;
                    if (proposal.HasClause(ClauseType.TreatyOpenBorders) || proposal.HasClause(ClauseType.TreatyTradePact))
                        return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.OpenBordersProposedSummaryText;
                    if (proposal.HasClause(ClauseType.TreatyAffiliation))
                        return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.AffiliationProposedSummaryText;
                    if (proposal.HasClause(ClauseType.TreatyDefensiveAlliance))
                        return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.DefensiveAllianceProposedSummaryText;
                    if (proposal.HasClause(ClauseType.TreatyFullAlliance))
                        return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.FullAllianceProposedSummaryText;
                    if (proposal.HasClause(ClauseType.TreatyMembership))
                        return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.MembershipProposedSummaryText;
                }

                if (proposal.IsGift())
                    return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.GiftOfferedSummaryText;
                if (proposal.IsDemand())
                    return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.TributeDemandedSummaryText;
                if (proposal.IsWarPact())
                    return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.WarPactProposedSummaryText;
            }

            if (response != null)
            {
                proposal = response.Proposal;

                if (response.ResponseType == ResponseType.Accept)
                {
                    if (proposal.HasTreaty())
                    {
                        if (proposal.HasClause(ClauseType.TreatyCeaseFire))
                            return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.CeaseFireAcceptedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyNonAggression))
                            return detailed ? DiplomacySitRepStringKey.NonAggressionPactAcceptedDetailText : DiplomacySitRepStringKey.NonAggressionPactAcceptedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyOpenBorders) || proposal.HasClause(ClauseType.TreatyTradePact))
                            return detailed ? DiplomacySitRepStringKey.OpenBordersAcceptedDetailText : DiplomacySitRepStringKey.OpenBordersAcceptedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyAffiliation))
                            return detailed ? DiplomacySitRepStringKey.AffiliationAcceptedDetailText : DiplomacySitRepStringKey.AffiliationAcceptedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyDefensiveAlliance))
                            return detailed ? DiplomacySitRepStringKey.DefensiveAllianceAcceptedDetailText : DiplomacySitRepStringKey.DefensiveAllianceAcceptedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyFullAlliance))
                            return detailed ? DiplomacySitRepStringKey.FullAllianceAcceptedDetailText : DiplomacySitRepStringKey.FullAllianceAcceptedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyMembership))
                            return detailed ? DiplomacySitRepStringKey.MembershipAcceptedDetailText : DiplomacySitRepStringKey.MembershipAcceptedSummaryText;
                    }

                    if (proposal.IsDemand())
                        return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.TributeAcceptedSummaryText;
                    if (proposal.IsWarPact())
                        return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.WarPactAcceptedSummaryText;
                }
                else if (response.ResponseType == ResponseType.Reject)
                {
                    if (proposal.HasTreaty())
                    {
                        if (proposal.HasClause(ClauseType.TreatyCeaseFire))
                            return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.CeaseFireRejectedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyNonAggression))
                            return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.NonAggressionPactRejectedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyOpenBorders) || proposal.HasClause(ClauseType.TreatyTradePact))
                            return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.OpenBordersRejectedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyAffiliation))
                            return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.AffiliationRejectedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyDefensiveAlliance))
                            return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.DefensiveAllianceRejectedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyFullAlliance))
                            return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.FullAllianceRejectedSummaryText;
                        if (proposal.HasClause(ClauseType.TreatyMembership))
                            return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.MembershipRejectedSummaryText;
                    }

                    if (proposal.IsDemand())
                        return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.TributeRejectedSummaryText;
                    if (proposal.IsWarPact())
                        return detailed ? default(DiplomacySitRepStringKey?) : DiplomacySitRepStringKey.WarPactRejectedSummaryText;
                }
            }

            var statement = _exchange as Statement;
            if (statement != null)
            {
                if (statement.StatementType == StatementType.WarDeclaration)
                    return detailed ? DiplomacySitRepStringKey.WarDeclaredDetailText : DiplomacySitRepStringKey.WarDeclaredSummaryText;
            }

            return null;
        }

        #region Overrides of SitRepEntry

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.Diplomacy; }
        }

        public override string SummaryText
        {
            get { return EnsureText(ref _summaryText, ref _hasEvaluatedSummaryText, false); }
        }

        public override string DetailText
        {
            get { return EnsureText(ref _detailText, ref _hasEvaluatedDetailText, true); }
        }

        public override string DetailImage
        {
            get
            {
                if (_hasEvaluatedDetailText)
                    return _image;

                if (!HasDetails)
                {
                    _image = null;
                    return _image;
                }

                Civilization sender;
                Civilization recipient;

                var response = _exchange as IResponse;
                if (response != null)
                {
                    switch (response.ResponseType)
                    {
                        case ResponseType.Accept:
                        case ResponseType.Reject:
                            sender = response.Proposal.Sender;
                            recipient = response.Proposal.Recipient;
                            break;
                        case ResponseType.Counter:
                            sender = response.CounterProposal.Sender;
                            recipient = response.CounterProposal.Recipient;
                            break;
                        default:
                            _image = null;
                            return _image;
                    }
                }
                else
                {
                    sender = _exchange.Sender;
                    recipient = _exchange.Recipient;
                }

                if (Owner == sender)
                    _image = recipient.InsigniaPath;
                else
                    _image = sender.InsigniaPath;

                return _image;
            }
        }

        #endregion
    }
    #endregion

    [Serializable]
    public class BuildQueueEmptySitRepEntry : SitRepEntry
    {
        private readonly int _colonyId;
        private readonly bool _shipyardQueue;

        public Colony Colony
        {
            get { return GameContext.Current.Universe.Objects[_colonyId] as Colony; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus | SitRepCategory.Construction; }
        }

        public override string SummaryText
        {
            get
            {
                if (_shipyardQueue)
                {
                    return string.Format(
                        ResourceManager.GetString("SITREP_SHIPYARD_BUILD_QUEUE_EMPTY"),
                        Colony.Name);
                }
                return string.Format(
                    ResourceManager.GetString("SITREP_PLANETARY_BUILD_QUEUE_EMPTY"),
                    Colony.Name);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        // ReSharper disable SuggestBaseTypeForParameter
        public BuildQueueEmptySitRepEntry(Civilization owner, Colony colony, bool shipyardQueue)
            : base(owner, SitRepPriority.Yellow)
        {
            if (colony == null)
                throw new ArgumentNullException("colony");
            _colonyId = colony.ObjectID;
            _shipyardQueue = shipyardQueue;
        }
        // ReSharper restore SuggestBaseTypeForParameter
    }

    [Serializable]
    public class ScriptedEventSitRepEntry : SitRepEntry, ISerializable
    {
        private readonly CivString _headerText;
        private readonly CivString _summaryText;
        private readonly CivString _detailText;
        private readonly string _detailImage;
        private readonly string _soundEffect;

        protected ScriptedEventSitRepEntry(SerializationInfo info, StreamingContext context)
            : base(info.GetInt32("OwnerID"), SitRepPriority.Special)
        {
            _headerText = (CivString)info.GetValue("_headerText", typeof(CivString));
            _detailText = (CivString)info.GetValue("_detailText", typeof(CivString));
            _summaryText = (CivString)info.GetValue("_summaryText", typeof(CivString));
            _detailImage = info.GetString("_detailImage");
            _soundEffect = info.GetString("_soundEffect");
        }

        public ScriptedEventSitRepEntry(ScriptedEventSitRepEntryData data)
            : base(data.Owner, SitRepPriority.Special)
        {
            _headerText = new CivString(data.Owner, CivString.ScriptedEventsCategory, data.HeaderText);
            _detailText = new CivString(data.Owner, CivString.ScriptedEventsCategory, data.DetailText);
            _summaryText = new CivString(data.Owner, CivString.ScriptedEventsCategory, data.SummaryText);
            _detailImage = data.DetailImage;
            _soundEffect = data.SoundEffect;
        }

        public override string SoundEffect
        {
            get { return _soundEffect; }
        }

        public override string HeaderText
        {
            get { return _headerText.Value; }
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.SpecialEvent; }
        }

        public override string SummaryText
        {
            get { return _summaryText.Value; }
        }

        public override string DetailText
        {
            get { return _detailText.Value; }
        }

        public override string DetailImage
        {
            get { return _detailImage; }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            /*info.AddValue(
                "_parameterResolvers",
                _parameterResolvers.Select(o => expressionSerializer.Serialize(o.CanReduce ? o.Reduce() : o).ToString(SaveOptions.DisableFormatting)).ToArray());*/

            info.AddValue("OwnerID", OwnerID);
            info.AddValue("_headerText", _headerText);
            info.AddValue("_summaryText", _summaryText);
            info.AddValue("_detailText", _detailText);
            info.AddValue("_detailImage", _detailImage);
            info.AddValue("_soundEffect", _soundEffect);
        }
    }

    //TODO: This needs fleshing out. Need a definite popup,
    //image with something to do with medical or death
    [Serializable]
    public class PopulationDyingSitRepEntry : SitRepEntry
    {
        private Colony _colony;

        public PopulationDyingSitRepEntry(Civilization owner, Colony colony) : base(owner, SitRepPriority.Red)
        {
            if (owner == null)
                throw new ArgumentException("owner");
            if (colony == null)
                throw new ArgumentException("colony");

            _colony = colony;
        }

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_POPULATION_DYING"),
                    _colony.Name);
            }
        }
    }

    //TODO: This needs fleshing out a bit more - needs a definite pop up,
    // image of graveyard or something
    [Serializable]
    public class PopulationDiedSitRepEntry : SitRepEntry
    {
        private Colony _colony;

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.ColonyStatus; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_POPULATION_DIED"),
                    _colony.Name);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public PopulationDiedSitRepEntry(Civilization owner, Colony colony) : base(owner, SitRepPriority.Red)
        {
            _colony = colony;
        }
    }

    [Serializable]
    public class ShipDestroyedInWormholeSitRepEntry : SitRepEntry
    {
        private MapLocation _wormholeLocation;

        public override SitRepCategory Categories
        {
            get { return SitRepCategory.General; }
        }

        public ShipDestroyedInWormholeSitRepEntry(Civilization owner, MapLocation wormholeLocation) : base(owner, SitRepPriority.Yellow)
        {
            _wormholeLocation = wormholeLocation;
        }
        
        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_FLEET_DESTROYED_UNSTABLE_WORMHOLE"), _wormholeLocation);
            }
        }
        
        public override bool IsPriority
        {
            get { return true; }
        }
        
    }

    [Serializable]
    public class BlackHoleEncounterSitRepEntry : SitRepEntry
    {
        private int _shipsDestroyed;
        private int _shipsDamaged;
        private StarSystem _location;
        
        public override SitRepCategory Categories
        {
            get { return SitRepCategory.General; }
        }

        public override string SummaryText
        {
            get
            {
                return string.Format(ResourceManager.GetString("SITREP_BLACK_HOLE_ENCOUNTER"), _location.Location, _shipsDestroyed, _shipsDamaged);
            }
        }

        public override bool IsPriority
        {
            get { return true; }
        }

        public BlackHoleEncounterSitRepEntry(Civilization owner, StarSystem location, int shipsDamaged, int shipsDestroyed) : base(owner, SitRepPriority.Red)
        {
            _location = location;
            _shipsDamaged = shipsDamaged;
            _shipsDestroyed = shipsDestroyed;
        }
    }
}
