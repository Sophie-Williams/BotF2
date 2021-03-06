// BuildProject.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using Supremacy.Buildings;
using Supremacy.Entities;
using Supremacy.Game;
using Supremacy.IO.Serialization;
using Supremacy.Resources;
using Supremacy.Tech;
using Supremacy.Types;
using Supremacy.Universe;
using Supremacy.Utility;
using System;
using System.ComponentModel;

namespace Supremacy.Economy
{
    [Flags]
    public enum BuildProjectFlags : byte
    {
        None = 0x00,
        OnHold = 0x01,
        Cancelled = 0x02,
        DeuteriumShortage = 0x04,
        DilithiumShortage = 0x08,
        RawMaterialsShortage = 0x10,
        Rushed = 0x20,
    }

    /// <summary>
    /// The base class for all construction projects in the game.
    /// </summary>
    [Serializable]
    public abstract class BuildProject : INotifyPropertyChanged
    {
        public const int MaxPriority = byte.MaxValue;
        public const int MinPriority = byte.MinValue;

        private readonly int _productionCenterId;

        private int _buildTypeId;
        private int _industryInvested;
        private MapLocation _location;
        private int _ownerId;
        private byte _priority;
        private BuildProjectFlags _flags;
        private ResourceValueCollection _resourcesInvested;

        /// <summary>
        /// Initializes a new instance of the <see cref="BuildProject"/> class.
        /// </summary>
        /// <param name="owner">The civilization initiating the <see cref="BuildProject"/>.</param>
        /// <param name="productionCenter">The construction location.</param>
        /// <param name="buildType">The design of the item being constructed.</param>
        protected BuildProject(
            Civilization owner,
            // ReSharper disable SuggestBaseTypeForParameter
            IProductionCenter productionCenter,
            // ReSharper restore SuggestBaseTypeForParameter
            TechObjectDesign buildType)
        {
            if (owner == null)
                throw new ArgumentNullException("owner");
            if (buildType == null)
                throw new ArgumentNullException("buildType");
            _ownerId = owner.CivID;
            _buildTypeId = buildType.DesignID;
            _location = productionCenter.Location;
            _productionCenterId = productionCenter.ObjectID;
            _resourcesInvested = new ResourceValueCollection();
        }

        /// <summary>
        /// Gets the production center at which construction is taking place.
        /// </summary>
        /// <value>The production center at which construction is taking place.</value>
        public virtual IProductionCenter ProductionCenter
        {
            get { return GameContext.Current.Universe.Objects[_productionCenterId] as IProductionCenter; }
        }

        public override string ToString()
        {
            return BuildDesign.LocalizedName;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="BuildProject"/> is an upgrade project.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="BuildProject"/> is an upgrade project; otherwise, <c>false</c>.
        /// </value>
        public virtual bool IsUpgrade
        {
            get { return false; }
        }

        /// <summary>
        /// Gets the description of the item under construction.
        /// </summary>
        /// <value>The description.</value>
        public virtual string Description
        {
            get { return ResourceManager.GetString(BuildDesign.Name); }
        }

        /// <summary>
        /// Gets or sets the location where construction is taking place.
        /// </summary>
        /// <value>The location.</value>
        public MapLocation Location
        {
            get { return _location; }
            set { _location = value; }
        }

        /// <summary>
        /// Gets the civilization that initiated this <see cref="BuildProject"/>.
        /// </summary>
        /// <value>The builder.</value>
        public Civilization Builder
        {
            get { return GameContext.Current.Civilizations[_ownerId]; }
        }

        /// <summary>
        /// Gets the design of the item being constructed.
        /// </summary>
        /// <value>The construction design.</value>
        public TechObjectDesign BuildDesign
        {
            get { return GameContext.Current.TechDatabase[_buildTypeId]; }
        }

        /// <summary>
        /// Gets the player-assigned priority of this <see cref="BuildProject"/>.
        /// </summary>
        /// <value>The player-assigned priority.</value>
        public int Priority
        {
            get { return _priority; }
            set { _priority = (byte)Math.Max(MinPriority, Math.Min(MaxPriority, value)); }
        }

        /// <summary>
        /// Gets or sets the amount of industry that has been invested thus far.
        /// </summary>
        /// <value>The industry invested.</value>
        public virtual int IndustryInvested
        {
            get { return _industryInvested; }
            protected set { _industryInvested = value; }
        }

        /// <summary>
        /// Gets the amount of resources that have been invested thus far.
        /// </summary>
        /// <value>The resources invested.</value>
        public ResourceValueCollection ResourcesInvested
        {
            get { return _resourcesInvested; }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="BuildProject"/> is completed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="BuildProject"/> is completed; otherwise, <c>false</c>.
        /// </value>
        public virtual bool IsCompleted
        {
            get
            {
                if (_industryInvested < IndustryRequired)
                {
                    GameLog.Core.Production.DebugFormat("{0} at {1} not complete - insufficient industry invested",
                        BuildDesign, _location);
                    return false;
                }

                foreach (ResourceType resource in EnumUtilities.GetValues<ResourceType>())
                {
                    if (_resourcesInvested[resource] < ResourcesRequired[resource])
                    {
                        GameLog.Core.Production.DebugFormat("{0} at {1} not complete - insufficient {0} invested",
                            BuildDesign, _location, resource);
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this project is cancelled.
        /// </summary>
        /// <value>
        /// <c>true</c> if this project is cancelled; otherwise, <c>false</c>.
        /// </value>
        public bool IsCancelled
        {
            get { return GetFlag(BuildProjectFlags.Cancelled); }
            protected set { SetFlag(BuildProjectFlags.Cancelled, value); }
        }

        /// <summary>
        /// Gets a value indicating whether this project is rushed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this project is rushed; otherwise, <c>false</c>.
        /// </value>
        public bool IsRushed
        {
            get { return GetFlag(BuildProjectFlags.Rushed); }
            set
            {
                SetFlag(BuildProjectFlags.Rushed, value); OnPropertyChanged("IsRushed");
            }
        }

        /// <summary>
        /// Gets a value indicating whether work on this project has been paused.
        /// </summary>
        /// <value>
        /// <c>true</c> if work on this project is paused; otherwise, <c>false</c>.
        /// </value>
        public bool IsPaused
        {
            get { return GetFlag(BuildProjectFlags.OnHold); }
            set { SetFlag(BuildProjectFlags.OnHold, value); }
        }

        /// <summary>
        /// Gets a value indicating whether the project is partially complete.
        /// </summary>
        /// <value>
        /// <c>true</c> if this project is partially complete; otherwise, <c>false</c>.
        /// </value>
        public bool IsPartiallyComplete
        {
            get { return (PercentComplete > 0.0f); }
        }

        /// <summary>
        /// Gets the percent completion of this <see cref="BuildProject"/>.
        /// </summary>
        /// <value>The percent completion.</value>
        public virtual Percentage PercentComplete
        {
            get { return (Percentage)((double)_industryInvested / IndustryRequired); }
        }

        /// <summary>
        /// Gets the total industry required to complete this <see cref="BuildProject"/>.
        /// </summary>
        /// <value>The industry required.</value>
        protected virtual int IndustryRequired
        {
            get { return BuildDesign.BuildCost; }
        }

        /// <summary>
        /// Gets the total resources required to complete this <see cref="BuildProject"/>.
        /// </summary>
        /// <value>The resources required.</value>
        protected virtual ResourceValueCollection ResourcesRequired
        {
            get { return BuildDesign.BuildResourceCosts; }
        }

        /// <summary>
        /// Gets the empire specific ShipInfoText
        /// </summary>
        /// <value>empire specific ShipInfoText</value>
        //public virtual string ShipInfoEmpire
        //{
        //    get
        //    {
        //        var _owner = Builder.Key;
        //        //GameLog.Core.General.DebugFormat("");
        //        var message = "DEFAULT_SHIP_INFO";
        //        switch (Builder.Key)
        //        {
        //            case "FEDERATION":
        //                message = "vfs:///Resources/UI/Federation/ColonyScreen/ship_selection.png";
        //                break;
        //            case "KLINGONS":
        //                message = "vfs:///Resources/UI/Klingons/ColonyScreen/ship_selection.png";
        //                break;
        //            case "ROMULANS":
        //                message = "vfs:///Resources/UI/Borg/ColonyScreen/ship_selection.png";
        //                break;
        //            case "DOMINION":
        //                message = "vfs:///Resources/UI/Dominion/ColonyScreen/ship_selection.png";
        //                break;
        //            case "TERRANEMPIRE":
        //                message = "vfs:///Resources/UI/TerranEmpire/ColonyScreen/ship_selection.png";
        //                break;
        //            case "BORG":
        //                message = "vfs:///Resources/UI/Borg/ColonyScreen/ship_selection.png";
        //                break;
        //            case "CARDASSIANS":
        //                message = "vfs:///Resources/UI/Cardassians/ColonyScreen/ship_selection.png";
        //                break;
        //            default:
        //                message = "vfs:///Resources/UI/Default/ship_selection.png";
        //                break;
        //                //case "FEDERATION":
        //                //    message ="FED_SHIP_INFO"; break;
        //                //case "KLINGONS":
        //                //    message = "KLING_SHIP_INFO"; break;
        //                //case "ROMULANS":
        //                //    message = "ROM_SHIP_INFO"; break;
        //                //case "DOMINION":
        //                //    message ="DOM_SHIP_INFO"; break;
        //                //case "TERRANEMPIRE":
        //                //    message ="TERRANS_SHIP_INFO"; break;
        //                //case "BORG":
        //                //    message ="BORG_SHIP_INFO"; break;
        //                //case "CARDASSIANS":
        //                //    message ="CARD_SHIP_INFO"; break;
        //                //default:
        //                //    message ="DEFAULT_SHIP_INFO"; break;
        //        }
        //        return ResourceManager.GetResourcePath(message);
        //    }
        //}

        /// <summary>
        /// Gets the number of turns remaining until this <see cref="BuildProject"/> is completed.
        /// </summary>
        /// <value>The turns remaining.</value>
        public virtual int TurnsRemaining
        {
            get { return GetTimeEstimate(); }
        }

        protected bool GetFlag(BuildProjectFlags flag)
        {
            return (_flags & flag) == flag;
        }

        protected void ClearFlag(BuildProjectFlags flag)
        {
            _flags &= ~flag;
        }

        protected void SetFlag(BuildProjectFlags flag, bool value = true)
        {
            if (value)
                _flags |= flag;
            else
                ClearFlag(flag);
        }

        public void InvalidateTurnsRemaining()
        {
            OnPropertyChanged("TurnsRemaining");
        }

        #region INotifyPropertyChanged Members
        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region IOwnedDataSerializableAndRecreatable Members
        public virtual void SerializeOwnedData(SerializationWriter writer, object context)
        {
            writer.WriteOptimized(_buildTypeId);
            writer.WriteOptimized(_industryInvested);
            writer.Write((byte)_location.X);
            writer.Write((byte)_location.Y);
            writer.WriteOptimized(_ownerId);
            writer.Write(_priority);
            writer.WriteObject(_resourcesInvested);
            writer.Write((byte)_flags);
        }

        public virtual void DeserializeOwnedData(SerializationReader reader, object context)
        {
            _buildTypeId = reader.ReadOptimizedInt32();
            _industryInvested = reader.ReadOptimizedInt32();
            _location = new MapLocation(reader.ReadByte(), reader.ReadByte());
            _ownerId = reader.ReadOptimizedInt32();
            _priority = reader.ReadByte();
            _resourcesInvested = (ResourceValueCollection)reader.ReadObject();
            _flags = (BuildProjectFlags)reader.ReadByte();
        }
        #endregion

        /// <summary>
        /// Gets the amount of industry available for investment during the current turn.
        /// </summary>
        /// <returns>The industry available.</returns>
        protected abstract int GetIndustryAvailable();

        /// <summary>
        /// Cancels this <see cref="BuildProject"/>.
        /// </summary>
        public virtual void Cancel()
        {
            IsCancelled = true;
        }

        /// <summary>
        /// Finishes this <see cref="BuildProject"/> and creates the newly constructed item.
        /// </summary>
        public virtual void Finish()
        {
            var civManager = GameContext.Current.CivilizationManagers[Builder];

            TechObject spawnedInstance;

            if (civManager == null || !BuildDesign.TrySpawn(Location, Builder, out spawnedInstance))
                return;

            //Wtf is going on here?
            ItemBuiltSitRepEntry newEntry = null;
            if (spawnedInstance != null)
            {
                if (spawnedInstance.ObjectType == UniverseObjectType.Building)
                {
                    newEntry = new BuildingBuiltSitRepEntry(Builder, BuildDesign, _location, (spawnedInstance as Building).IsActive);
                }
            }

            if (newEntry == null)
            {
                GameLog.Core.Production.DebugFormat("{0} built by {1} at {2}", BuildDesign, Builder, Location);
                newEntry = new ItemBuiltSitRepEntry(Builder, BuildDesign, Location);
            }

            civManager.SitRepEntries.Add(newEntry);
        }

        /// <summary>
        /// Gets estimated number of turns until this <see cref="BuildProject"/> is completed.
        /// </summary>
        /// <returns>The time estimate.</returns>
        public virtual int GetTimeEstimate()
        {
            var industryAvailable = GetIndustryAvailable();
            var industryRemaining = IndustryRequired - IndustryInvested;

            var turns = industryRemaining / industryAvailable;
            
            if (industryRemaining % industryAvailable > 0)
                ++turns;

            if (turns == 0 && !IsCompleted)
                turns = 1;

            return turns;
        }

        /// <summary>
        /// Gets the current industry costs left to this <see cref="BuildProject"/>
        /// </summary>
        /// <returns>The time estimate.</returns>
        public virtual int GetCurrentIndustryCost()
        {
            return IndustryRequired - IndustryInvested;
        }

        /// <summary>
        /// Gets the current industry costs left to this <see cref="BuildProject"/>
        /// </summary>
        /// <returns>The time estimate.</returns>
        public virtual int GetCurrentResourceCost(ResourceType resource)
        {
            return ResourcesRequired[resource] - ResourcesInvested[resource];
        }

        /// <summary>
        /// Gets the total amount of credits that it will require to complete this project
        /// </summary>
        /// <returns></returns>
        public virtual int GetTotalCreditsCost()
        {
            return GetCurrentIndustryCost() +
                EconomyHelper.ComputeResourceValue(ResourceType.Deuterium, GetCurrentResourceCost(ResourceType.Deuterium)) +
                EconomyHelper.ComputeResourceValue(ResourceType.Dilithium, GetCurrentResourceCost(ResourceType.Dilithium)) +
                EconomyHelper.ComputeResourceValue(ResourceType.RawMaterials, GetCurrentResourceCost(ResourceType.RawMaterials));
        }

        /// <summary>
        /// Advances this <see cref="BuildProject"/> by one turn.
        /// </summary>
        /// <param name="industry">The industry available for investment.</param>
        /// <param name="resources">The resources available for investment.</param>
        /// <remarks>
        /// Prior to returning, this function updates the <paramref name="industry"/>
        /// and <paramref name="resources"/> parameters to reflect the values
        /// remaining (the surplus that was not invested).
        /// </remarks>
        public void Advance(ref int industry, ResourceValueCollection resources)
        {
            if (IsPaused || IsCancelled)
                return;

            AdvanceOverride(ref industry, resources);
        }

        protected virtual void AdvanceOverride(ref int industry, ResourceValueCollection resources)
        {
            var timeEstimate = GetTimeEstimate();
            if (timeEstimate <= 0)
                return;

            var delta = Math.Min(
                industry,
                Math.Max(0, IndustryRequired - _industryInvested));

            industry -= delta;

            IndustryInvested += delta;
            ApplyIndustry(delta);
            OnPropertyChanged("IndustryInvested");

            ClearFlag(
                BuildProjectFlags.DeuteriumShortage |
                BuildProjectFlags.DilithiumShortage |
                BuildProjectFlags.RawMaterialsShortage);
            
            var resourceTypes = EnumHelper.GetValues<ResourceType>();

            for (var i = 0; i < resourceTypes.Length; i++)
            {
                var resource = resourceTypes[i];

                delta = ResourcesRequired[resource] - _resourcesInvested[resource];

                if (delta <= 0)
                    continue;

                if (timeEstimate == 1 &&
                    delta > resources[resource])
                {
                    SetFlag((BuildProjectFlags)((int)BuildProjectFlags.DeuteriumShortage << i));
                }

                if (resources[resource] <= 0)
                    continue;

                delta /= timeEstimate;
                delta += ((ResourcesRequired[resource] - _resourcesInvested[resource]) % timeEstimate);
                
                delta = Math.Min(delta, resources[resource]);

                resources[resource] -= delta;
                _resourcesInvested[resource] += delta;

                ApplyResource(resource, delta);
            }

            OnPropertyChanged("TurnsRemaining");
            OnPropertyChanged("PercentComplete");
            OnPropertyChanged("IsPartiallyComplete");

            if (IsCompleted)
                OnPropertyChanged("IsCompleted");
        }

        /// <summary>
        /// Determines whether a given project is equivalent to this <see cref="BuildProject"/>
        /// (the designs of the items being constructed are the same).
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns>
        /// <c>true</c> if the project is equivalent; otherwise, <c>false</c>.
        /// </returns>
        public virtual bool IsEquivalent(BuildProject project)
        {
            if (project == null)
                return false;
            if (project.GetType() != GetType())
                return false;
            if (project._buildTypeId != _buildTypeId)
                return false;
            return true;
        }

        /// <summary>
        /// Creates an equivalent clone of this <see cref="BuildProject"/>.
        /// </summary>
        /// <returns>The clone.</returns>
        public virtual BuildProject CloneEquivalent()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Applies the specified amount of industry to this <see cref="BuildProject"/>.
        /// </summary>
        /// <param name="industry">The industry to apply.</param>
        protected virtual void ApplyIndustry(int industry) { }

        /// <summary>
        /// Applies the specified amount of a given resource to this <see cref="BuildProject"/>.
        /// </summary>
        /// <param name="resource">The type of resource.</param>
        /// <param name="amount">The amount to apply.</param>
        protected virtual void ApplyResource(ResourceType resource, int amount) { }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}