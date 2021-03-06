using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Data;

using Microsoft.Practices.ServiceLocation;

using Supremacy.Annotations;
using Supremacy.Client.Input;
using Supremacy.Collections;
using Supremacy.Diplomacy;
using Supremacy.Entities;
using Supremacy.Game;
using Supremacy.Scripting;
using Supremacy.Text;
using Supremacy.Utility;

namespace Supremacy.Client.Views
{
    public class DiplomacyMessageViewModel : INotifyPropertyChanged
    {
        private readonly Civilization _sender;
        private readonly Civilization _recipient;
        private readonly ObservableCollection<DiplomacyMessageElement> _elements;
        private readonly ObservableCollection<DiplomacyMessageElement> _offerElements;
        private readonly ObservableCollection<DiplomacyMessageElement> _requestElements;
        private readonly ObservableCollection<DiplomacyMessageElement> _statementElements;
        private readonly ObservableCollection<DiplomacyMessageElement> _treatyElements;
        private readonly ReadOnlyObservableCollection<DiplomacyMessageElement> _elementsView;
        private readonly ReadOnlyObservableCollection<DiplomacyMessageElement> _offerElementsView;
        private readonly ReadOnlyObservableCollection<DiplomacyMessageElement> _requestElementsView;
        private readonly ReadOnlyObservableCollection<DiplomacyMessageElement> _statementElementsView;
        private readonly ReadOnlyObservableCollection<DiplomacyMessageElement> _treatyElementsView;
        private readonly ObservableCollection<DiplomacyMessageAvailableElement> _availableElements;
        private readonly ReadOnlyObservableCollection<DiplomacyMessageAvailableElement> _availableElementsView;

        private readonly DelegateCommand<DiplomacyMessageElement> _removeElementCommand;

        private readonly ScriptExpression _treatyLeadInTextScript;
        private readonly ScriptExpression _offerLeadInTextScript;
        private readonly ScriptExpression _requestLeadInTextScript;

        private readonly ScriptParameters _leadInParameters;
        private readonly RuntimeScriptParameters _leadInRuntimeParameters;

        private Order _sendOrder;

        public DiplomacyMessageViewModel([NotNull] Civilization sender, [NotNull] Civilization recipient)
        {
            if (sender == null)
                throw new ArgumentNullException("sender");
            if (recipient == null)
                throw new ArgumentNullException("recipient");

            _sender = sender;
            _recipient = recipient;
            _elements = new ObservableCollection<DiplomacyMessageElement>();
            _elementsView = new ReadOnlyObservableCollection<DiplomacyMessageElement>(_elements);
            _availableElements = new ObservableCollection<DiplomacyMessageAvailableElement>();
            _availableElementsView = new ReadOnlyObservableCollection<DiplomacyMessageAvailableElement>(_availableElements);

            _treatyElements = new ObservableCollection<DiplomacyMessageElement>();
            _treatyElementsView = new ReadOnlyObservableCollection<DiplomacyMessageElement>(_treatyElements);
            _offerElements = new ObservableCollection<DiplomacyMessageElement>();
            _offerElementsView = new ReadOnlyObservableCollection<DiplomacyMessageElement>(_offerElements);
            _requestElements = new ObservableCollection<DiplomacyMessageElement>();
            _requestElementsView = new ReadOnlyObservableCollection<DiplomacyMessageElement>(_requestElements);
            _statementElements = new ObservableCollection<DiplomacyMessageElement>();
            _statementElementsView = new ReadOnlyObservableCollection<DiplomacyMessageElement>(_statementElements);

            _removeElementCommand = new DelegateCommand<DiplomacyMessageElement>(
                ExecuteRemoveElementCommand,
                CanExecuteRemoveElementCommand);

            _leadInParameters = new ScriptParameters(
                new ScriptParameter("$sender", typeof(Civilization)),
                new ScriptParameter("$recipient", typeof(Civilization)));

            _leadInRuntimeParameters = new RuntimeScriptParameters
                                       {
                                           new RuntimeScriptParameter(_leadInParameters[0], _sender),
                                           new RuntimeScriptParameter(_leadInParameters[1], _recipient)
                                       };

            _treatyLeadInTextScript = new ScriptExpression(returnObservableResult: false)
                                      {
                                          Parameters = _leadInParameters
                                      };

            _offerLeadInTextScript = new ScriptExpression(returnObservableResult: false)
                                     {
                                         Parameters = _leadInParameters
                                     };

            _requestLeadInTextScript = new ScriptExpression(returnObservableResult: false)
                                       {
                                           Parameters = _leadInParameters
                                       };

            CollectionViewSource.GetDefaultView(_availableElementsView).GroupDescriptions.Add(new PropertyGroupDescription("ActionDescription"));
        }

        private bool CanExecuteRemoveElementCommand(DiplomacyMessageElement element)
        {
            return IsEditing && element != null && _elements.Contains(element);
        }

        private void ExecuteRemoveElementCommand(DiplomacyMessageElement element)
        {
            if (!CanExecuteRemoveElementCommand(element))
                return;

            RemoveElement(element);
        }

        public Civilization Sender
        {
            get { return _sender; }
        }

        public Civilization Recipient
        {
            get { return _recipient; }
        }

        public ReadOnlyObservableCollection<DiplomacyMessageElement> Elements
        {
            get { return _elementsView; }
        }

        public ReadOnlyObservableCollection<DiplomacyMessageElement> TreatyElements
        {
            get { return _treatyElementsView; }
        }

        public ReadOnlyObservableCollection<DiplomacyMessageElement> RequestElements
        {
            get { return _requestElementsView; }
        }

        public ReadOnlyObservableCollection<DiplomacyMessageElement> OfferElements
        {
            get { return _offerElementsView; }
        }

        public ReadOnlyObservableCollection<DiplomacyMessageElement> StatementElements
        {
            get { return _statementElementsView; }
        }

        public ReadOnlyObservableCollection<DiplomacyMessageAvailableElement> AvailableElements
        {
            get { return _availableElementsView; }
        }

        internal bool IsStatement
        {
            get { return _elements.All(o => o.ElementType <= DiplomacyMessageElementType.DenounceSabotageStatement); }
        }

        internal IDiplomaticExchange CreateMessage()
        {
            return IsStatement ? (IDiplomaticExchange)CreateStatement() : CreateProposal();
        }

        public void Send()
        {
            var isStatement = _elements.All(o => o.ElementType <= DiplomacyMessageElementType.DenounceSabotageStatement);
            if (isStatement)
            {
                var statement = CreateStatement();
                if (statement == null)
                    return;

                _sendOrder = new SendStatementOrder(statement);

                ServiceLocator.Current.GetInstance<IPlayerOrderService>().AddOrder(_sendOrder);
            }
            else
            {
                var proposal = CreateProposal();
                if (proposal == null)
                    return;

                _sendOrder = new SendProposalOrder(proposal);

                ServiceLocator.Current.GetInstance<IPlayerOrderService>().AddOrder(_sendOrder);
            }

            IsEditing = false;

            _availableElements.Clear();
        }

        public void Edit()
        {
            if (_sendOrder != null)
                ServiceLocator.Current.GetInstance<IPlayerOrderService>().RemoveOrder(_sendOrder);
            
            _sendOrder = null;
            
            IsEditing = true;
            PopulateAvailableElements();
        }

        public void Cancel()
        {
            if (_sendOrder != null)
                ServiceLocator.Current.GetInstance<IPlayerOrderService>().RemoveOrder(_sendOrder);

            _sendOrder = null;

            IsEditing = false;

            _availableElements.Clear();
            _elements.Clear();
        }

        #region Tone Property

        [field: NonSerialized]
        public event EventHandler ToneChanged;

        private Tone _tone;

        public Tone Tone
        {
            get { return _tone; }
            set
            {
                if (Equals(value, _tone))
                    return;

                _tone = value;
                _elements.ForEach(o => o.Tone = value);

                OnToneChanged();
            }
        }

        protected virtual void OnToneChanged()
        {
            ToneChanged.Raise(this);
            OnPropertyChanged("Tone");
        }

        #endregion

        #region IsEditing Property

        [field: NonSerialized]
        public event EventHandler IsEditingChanged;

        private bool _isEditing;

        public bool IsEditing
        {
            get { return _isEditing; }
            private set
            {
                if (Equals(value, _isEditing))
                    return;

                _isEditing = value;
                _elements.ForEach(o => o.IsEditing = value);

                OnIsEditingChanged();
            }
        }

        protected virtual void OnIsEditingChanged()
        {
            IsEditingChanged.Raise(this);
            OnPropertyChanged("IsEditing");
            InvalidateCommands();
        }

        #endregion

        #region OfferLeadInText Property

        [field: NonSerialized]
        public event EventHandler OfferLeadInTextChanged;

        private string _offerLeadInText;

        public string OfferLeadInText
        {
            get { return _offerLeadInText; }
            private set
            {
                if (Equals(value, _offerLeadInText))
                    return;

                _offerLeadInText = value;

                OnOfferLeadInTextChanged();
                OnHasOfferLeadInTextChanged();
            }
        }

        protected virtual void OnOfferLeadInTextChanged()
        {
            OfferLeadInTextChanged.Raise(this);
            OnPropertyChanged("OfferLeadInText");
        }

        #endregion

        #region HasOfferLeadInText Property

        [field: NonSerialized]
        public event EventHandler HasOfferLeadInTextChanged;

        public bool HasOfferLeadInText
        {
            get { return !string.IsNullOrWhiteSpace(OfferLeadInText); }
        }

        protected virtual void OnHasOfferLeadInTextChanged()
        {
            HasOfferLeadInTextChanged.Raise(this);
            OnPropertyChanged("HasOfferLeadInText");
        }

        #endregion

        #region RequestLeadInText Property

        [field: NonSerialized]
        public event EventHandler RequestLeadInTextChanged;

        private string _requestRequestLeadInText;

        public string RequestLeadInText
        {
            get { return _requestRequestLeadInText; }
            private set
            {
                if (Equals(value, _requestRequestLeadInText))
                    return;

                _requestRequestLeadInText = value;

                OnRequestLeadInTextChanged();
                OnHasRequestLeadInTextChanged();
            }
        }

        protected virtual void OnRequestLeadInTextChanged()
        {
            RequestLeadInTextChanged.Raise(this);
            OnPropertyChanged("RequestLeadInText");
        }

        #endregion

        #region HasRequestLeadInText Property

        [field: NonSerialized]
        public event EventHandler HasRequestLeadInTextChanged;

        public bool HasRequestLeadInText
        {
            get { return !string.IsNullOrWhiteSpace(RequestLeadInText); }
        }

        protected virtual void OnHasRequestLeadInTextChanged()
        {
            HasRequestLeadInTextChanged.Raise(this);
            OnPropertyChanged("HasRequestLeadInText");
        }

        #endregion

        #region TreatyLeadInText Property

        [field: NonSerialized]
        public event EventHandler TreatyLeadInTextChanged;

        private string _treatyLeadInText;

        public string TreatyLeadInText
        {
            get { return _treatyLeadInText; }
            private set
            {
                if (Equals(value, _treatyLeadInText))
                    return;

                _treatyLeadInText = value;

                OnTreatyLeadInTextChanged();
                OnHasTreatyLeadInTextChanged();
            }
        }

        protected virtual void OnTreatyLeadInTextChanged()
        {
            TreatyLeadInTextChanged.Raise(this);
            OnPropertyChanged("TreatyLeadInText");
        }

        #endregion

        #region HasTreatyLeadInText Property

        [field: NonSerialized]
        public event EventHandler HasTreatyLeadInTextChanged;

        public bool HasTreatyLeadInText
        {
            get { return !string.IsNullOrWhiteSpace(TreatyLeadInText); }
        }

        protected virtual void OnHasTreatyLeadInTextChanged()
        {
            HasTreatyLeadInTextChanged.Raise(this);
            OnPropertyChanged("HasTreatyLeadInText");
        }

        #endregion

        private void UpdateLeadInText()
        {
            var treatyLeadInId = DiplomacyStringID.None;
            var offerLeadInId = DiplomacyStringID.None;
            var requestLeadInId = DiplomacyStringID.None;

            if (_treatyElements.Count != 0)
            {
                var isWarPact = _treatyElements[0].ElementType == DiplomacyMessageElementType.TreatyWarPact;

                treatyLeadInId = isWarPact ? DiplomacyStringID.WarPactLeadIn : DiplomacyStringID.ProposalLeadIn;

                if (_offerElements.Count != 0)
                    offerLeadInId = isWarPact ? DiplomacyStringID.WarPactOffersLeadIn : DiplomacyStringID.ProposalOffersLeadIn;
                if (_requestElements.Count != 0)
                    requestLeadInId = isWarPact ? DiplomacyStringID.WarPactDemandsLeadIn : DiplomacyStringID.ProposalDemandsLeadIn;
            }
            else if (_requestElements.Count != 0)
            {
                if (_offerElements.Count != 0)
                {
                    offerLeadInId = DiplomacyStringID.ExchangeLeadIn;
                    requestLeadInId = DiplomacyStringID.ProposalDemandsLeadIn;
                }
                else
                {
                    requestLeadInId = DiplomacyStringID.DemandLeadIn;
                }
            }
            else if (_offerElements.Count != 0)
            {
                offerLeadInId = DiplomacyStringID.GiftLeadIn;
            }

            if (treatyLeadInId == DiplomacyStringID.None)
            {
                TreatyLeadInText = null;
            }
            else
            {
                _treatyLeadInTextScript.ScriptCode = QuoteString(LookupDiplomacyText(treatyLeadInId, _tone, _sender) ?? string.Empty);
                TreatyLeadInText = _treatyLeadInTextScript.Evaluate<string>(_leadInRuntimeParameters);
            }
            
            if (offerLeadInId == DiplomacyStringID.None)
            {
                OfferLeadInText = null;
            }
            else
            {
                _offerLeadInTextScript.ScriptCode = QuoteString(LookupDiplomacyText(offerLeadInId, _tone, _sender) ?? string.Empty);
                OfferLeadInText = _offerLeadInTextScript.Evaluate<string>(_leadInRuntimeParameters);
            }

            if (requestLeadInId == DiplomacyStringID.None)
            {
                RequestLeadInText = null;
            }
            else
            {
                _requestLeadInTextScript.ScriptCode = QuoteString(LookupDiplomacyText(requestLeadInId, _tone, _sender) ?? string.Empty);
                RequestLeadInText = _requestLeadInTextScript.Evaluate<string>(_leadInRuntimeParameters);
            }
        }

        internal static string QuoteString(string value)
        {
            if (value == null)
                return null;

            var sb = new StringBuilder(value.Length + 2);
            var bracketDepth = 0;

            sb.Append('"');

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var last = i == 0 ? '\0' : value[i - 1];
                if (c == '{' && last != '\\')
                    ++bracketDepth;
                else if (c == '}' && last != '\\')
                    --bracketDepth;
                else if (c == '"' && bracketDepth == 0)
                    sb.Append('\\');
                sb.Append(c);
            }
            
            sb.Append('"');

            return sb.ToString();
        }

        internal static string LookupDiplomacyText(DiplomacyStringID stringId, Tone tone, Civilization sender)
        {
            var civStringKey = new DiplomacyStringKey(sender != null ? sender.Key : null, stringId);

            LocalizedString localizedString;

            LocalizedTextGroup civTextGroup;
            LocalizedTextGroup defaultTextGroup;

            if (LocalizedTextDatabase.Instance.Groups.TryGetValue(civStringKey, out civTextGroup) &&
                civTextGroup.Entries.TryGetValue(tone, out localizedString))
            {
                return localizedString.LocalText;
            }

            var defaultStringKey = new DiplomacyStringKey(null, stringId);

            if (LocalizedTextDatabase.Instance.Groups.TryGetValue(defaultStringKey, out defaultTextGroup) &&
                defaultTextGroup.Entries.TryGetValue(tone, out localizedString))
            {
                return localizedString.LocalText;
            }

            if (civTextGroup != null && civTextGroup.DefaultEntry != null)
                return civTextGroup.DefaultEntry.LocalText;

            if (defaultTextGroup != null && defaultTextGroup.DefaultEntry != null)
                return defaultTextGroup.DefaultEntry.LocalText;

            return null;
        }

        internal void AddElement([NotNull] DiplomacyMessageAvailableElement availableElement)
        {
            if (availableElement == null)
                throw new ArgumentNullException("availableElement");

            var element = new DiplomacyMessageElement(_sender, _recipient, availableElement.ActionCategory, availableElement.ElementType, _removeElementCommand)
                          {
                              ParametersCallback = availableElement.ParametersCallback,
                              HasFixedParameter = availableElement.FixedParameter != null,
                              SelectedParameter = availableElement.FixedParameter,
                              IsEditing = IsEditing
                          };

            element.UpdateDescription();

            _elements.Add(element);

            switch (availableElement.ActionCategory)
            {
                case DiplomacyMessageElementActionCategory.Offer:
                    _offerElements.Add(element);
                    break;
                case DiplomacyMessageElementActionCategory.Request:
                    _requestElements.Add(element);
                    break;
                case DiplomacyMessageElementActionCategory.Propose:
                    _treatyElements.Add(element);
                    break;
                case DiplomacyMessageElementActionCategory.Commend:
                case DiplomacyMessageElementActionCategory.Denounce:
                case DiplomacyMessageElementActionCategory.WarDeclaration:
                    _statementElements.Add(element);
                    break;
            }

            PopulateAvailableElements();
            UpdateLeadInText();
        }

        private void RemoveElement(DiplomacyMessageElement element)
        {
            if (element == null)
                return;

            _elements.Remove(element);

            switch (element.ActionCategory)
            {
                case DiplomacyMessageElementActionCategory.Offer:
                    _offerElements.Remove(element);
                    break;
                case DiplomacyMessageElementActionCategory.Request:
                    _requestElements.Remove(element);
                    break;
                case DiplomacyMessageElementActionCategory.Propose:
                    _treatyElements.Remove(element);
                    break;
                case DiplomacyMessageElementActionCategory.Commend:
                case DiplomacyMessageElementActionCategory.Denounce:
                case DiplomacyMessageElementActionCategory.WarDeclaration:
                    _statementElements.Remove(element);
                    break;
            }

            PopulateAvailableElements();
            UpdateLeadInText();
        }

        private NewProposal CreateProposal(bool allowIncomplete = false)
        {
            if (_elements.Count == 0)
                return null;

            var clauses = new List<Clause>();

            foreach (var element in _elements)
            {
                var clauseType = DiplomacyScreenViewModel.ElementTypeToClauseType(element.ElementType);
                if (clauseType == ClauseType.NoClause)
                    continue;

                if (element.HasParameter)
                {
                    var selectedParameter = element.SelectedParameter;
                    if (selectedParameter == null && !allowIncomplete)
                        continue;

                    var parameterInfo = selectedParameter as IClauseParameterInfo;
                    if (parameterInfo != null)
                    {
                        if (parameterInfo.IsParameterValid)
                            selectedParameter = parameterInfo.GetParameterData();
                        else if (!allowIncomplete)
                            continue;
                    }

                    //
                    // It's possible for 'selectedParameter' to be null here.  We assume this is okay
                    // if IClauseParameterInfo.IsParameterValid returned 'true'.
                    //
                    clauses.Add(new Clause(clauseType, selectedParameter));
                }
                else
                {
                    clauses.Add(new Clause(clauseType));                    
                }
            }

            if (clauses.Count == 0)
                return null;

            return new NewProposal(_sender, _recipient, clauses);
        }

        private Statement CreateStatement()
        {
            if (_elements.Count != 1)
                return null;

            var statementType = DiplomacyScreenViewModel.ElementTypeToStatementType(_elements[0].ElementType);
            if (statementType == StatementType.NoStatement)
                return null;

            return new Statement(_sender, _recipient, statementType, _tone);
        }

        private void PopulateAvailableElements()
        {
            // ReSharper disable ImplicitlyCapturedClosure

            _availableElements.Clear();

            var diplomat = GameContext.Current.Diplomats[_sender];
            var currentProposal = CreateProposal(allowIncomplete: true);
            var currentStatement = CreateStatement();
            var recipientIsMember = DiplomacyHelper.IsMember(_recipient, _sender);

            /*
             * Statements must be the only element in a message.
             */
            if (_elements.Count == 0)
            {
                if (diplomat.CanCommendOrDenounceWar(_recipient, currentStatement))
                {
                    Func<IEnumerable<Civilization>> denouceWarParameters = () => diplomat.GetCommendOrDenounceWarParameters(_recipient, currentStatement).ToList();

                    Func<IEnumerable<Civilization>> commendWarParameters = () => denouceWarParameters().Where(
                        c =>
                        {
                            var status = GameContext.Current.DiplomacyData[_sender, c].Status;
                            return status != ForeignPowerStatus.Affiliated &&
                                   status != ForeignPowerStatus.Allied &&
                                   status != ForeignPowerStatus.Friendly;
                        }).ToList();

                    /*
                     * No commending wars being fought against our friends...
                     */
                    if (commendWarParameters().Any())
                    {
                        _availableElements.Add(
                            new DiplomacyMessageAvailableElement
                            {
                                ActionCategory = DiplomacyMessageElementActionCategory.Commend,
                                ParametersCallback = commendWarParameters,
                                ElementType = DiplomacyMessageElementType.CommendWarStatement
                            });
                    }

                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Denounce,
                            ParametersCallback = denouceWarParameters,
                            ElementType = DiplomacyMessageElementType.DenounceWarStatement
                        });
                }

                if (diplomat.CanCommendOrDenounceTreaty(_recipient, currentStatement))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Commend,
                            ParametersCallback = () => diplomat.GetCommendOrDenounceTreatyParameters(_recipient, currentStatement).ToList(),
                            ElementType = DiplomacyMessageElementType.CommendTreatyStatement
                        });

                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Denounce,
                            ParametersCallback = () => diplomat.GetCommendOrDenounceTreatyParameters(_recipient, currentStatement).ToList(),
                            ElementType = DiplomacyMessageElementType.CommendTreatyStatement
                        });
                }

                if (diplomat.CanProposeWarPact(_recipient, currentProposal))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Propose,
                            ParametersCallback = () => diplomat.GetWarPactParameters(_recipient, currentProposal).ToList(),
                            ElementType = DiplomacyMessageElementType.TreatyWarPact
                        });
                }

                if (!DiplomacyHelper.AreAtWar(_sender, _recipient))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.WarDeclaration,
                            ElementType = DiplomacyMessageElementType.WarDeclaration
                        });
                }
            }

            var anyActiveStatements = _elements.Any(o => o.ElementType <= DiplomacyMessageElementType.DenounceSabotageStatement);
            if (!anyActiveStatements)
            {
                /*
                 * Request...
                 */
                if (!recipientIsMember &&
                    _elements.All(
                        o => o.ElementType != DiplomacyMessageElementType.RequestGiveCreditsClause &&
                             o.ElementType != DiplomacyMessageElementType.OfferGiveCreditsClause))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Request,
                            ElementType = DiplomacyMessageElementType.RequestGiveCreditsClause,
                            FixedParameter = new CreditsDataViewModel(Diplomat.Get(_sender).OwnerTreasury)
                        });
                }

                var requestHonorMilitaryAgreementParameters = diplomat.GetRequestHonorMilitaryAgreementParameters(_recipient, currentProposal);
                if (requestHonorMilitaryAgreementParameters.Any())
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Request,
                            ElementType = DiplomacyMessageElementType.RequestHonorMilitaryAgreementClause,
                            ParametersCallback = () => diplomat.GetRequestHonorMilitaryAgreementParameters(_recipient, currentProposal)
                        });
                }

                /* 
                 * Propose...
                 */
                if (diplomat.CanProposeCeaseFire(_recipient, currentProposal))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Propose,
                            ElementType = DiplomacyMessageElementType.TreatyCeaseFireClause
                        });
                }

                if (diplomat.CanProposeNonAggressionTreaty(_recipient, currentProposal))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Propose,
                            ElementType = DiplomacyMessageElementType.TreatyNonAggressionClause
                        });
                }

                if (diplomat.CanProposeOpenBordersTreaty(_recipient, currentProposal))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Propose,
                            ElementType = DiplomacyMessageElementType.TreatyOpenBordersClause
                        });
                }

                if (diplomat.CanProposeAffiliation(_recipient, currentProposal))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Propose,
                            ElementType = DiplomacyMessageElementType.TreatyAffiliationClause
                        });
                }

                if (diplomat.CanProposeDefensiveAlliance(_recipient, currentProposal))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Propose,
                            ElementType = DiplomacyMessageElementType.TreatyDefensiveAllianceClause
                        });
                }

                if (diplomat.CanProposeFullAlliance(_recipient, currentProposal))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Propose,
                            ElementType = DiplomacyMessageElementType.TreatyFullAllianceClause
                        });
                }

                if (diplomat.CanProposeMembership(_recipient, currentProposal))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Propose,
                            ElementType = DiplomacyMessageElementType.TreatyMembershipClause
                        });
                }

                /*
                 * Offer...
                 */
                if (!recipientIsMember &&
                    _elements.All(
                        o => o.ElementType != DiplomacyMessageElementType.RequestGiveCreditsClause &&
                             o.ElementType != DiplomacyMessageElementType.OfferGiveCreditsClause))
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Offer,
                            ElementType = DiplomacyMessageElementType.OfferGiveCreditsClause,
                            FixedParameter = new CreditsDataViewModel(Diplomat.Get(_sender).OwnerTreasury)
                        });
                }

                var offerHonorMilitaryAgreementParameters = diplomat.GetOfferHonorMilitaryAgreementParameters(_recipient, currentProposal);
                if (offerHonorMilitaryAgreementParameters.Any())
                {
                    _availableElements.Add(
                        new DiplomacyMessageAvailableElement
                        {
                            ActionCategory = DiplomacyMessageElementActionCategory.Offer,
                            ElementType = DiplomacyMessageElementType.OfferHonorMilitaryAgreementClause,
                            ParametersCallback = () => diplomat.GetOfferHonorMilitaryAgreementParameters(_recipient, currentProposal)
                        });
                }
            }

            foreach (var availableElement in _availableElements)
            {
                var elementCopy = availableElement; // modified closure

                availableElement.AddCommand = new DelegateCommand(
                    () => AddElement(elementCopy),
                    () => IsEditing);
            }

            // ReSharper restore ImplicitlyCapturedClosure
        }

        public void InvalidateCommands()
        {
            _removeElementCommand.RaiseCanExecuteChanged();
        }

        public static DiplomacyMessageViewModel FromReponse([NotNull] IResponse response)
        {
            if (response == null)
                throw new ArgumentNullException("response");

            DiplomacyStringID leadInId;

            switch (response.ResponseType)
            {
                case ResponseType.NoResponse:
                    return null;
                case ResponseType.Accept:
                    if (response.Proposal.IsGift())
                        leadInId = DiplomacyStringID.AcceptGiftLeadIn;
                    else if (response.Proposal.IsDemand())
                        leadInId = DiplomacyStringID.AcceptDemandLeadIn;
                    else if (!response.Proposal.HasTreaty())
                        leadInId = DiplomacyStringID.AcceptExchangeLeadIn;
                    else 
                        leadInId = DiplomacyStringID.AcceptProposalLeadIn;
                    break;
                case ResponseType.Reject:
                    if (response.Proposal.IsGift())
                        leadInId = DiplomacyStringID.RejectProposalLeadIn; // should not happen
                    else if (response.Proposal.IsDemand())
                        leadInId = DiplomacyStringID.RejectDemandLeadIn;
                    else if (!response.Proposal.HasTreaty())
                        leadInId = DiplomacyStringID.RejectExchangeLeadIn;
                    else
                        leadInId = DiplomacyStringID.RejectProposalLeadIn;
                    break;
                case ResponseType.Counter:
                    leadInId = DiplomacyStringID.CounterProposalLeadIn;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var message = new DiplomacyMessageViewModel(response.Sender, response.Recipient)
                          {
                              Tone = response.Proposal.Tone,
                          };

            message._treatyLeadInTextScript.ScriptCode = QuoteString(LookupDiplomacyText(leadInId, message._tone, message._sender) ?? string.Empty);
            message.TreatyLeadInText = message._treatyLeadInTextScript.Evaluate<string>(message._leadInRuntimeParameters);

            return message;
        }

        #region Implementation of INotifyPropertyChanged

        [NonSerialized] private PropertyChangedEventHandler _propertyChanged;

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add
            {
                while (true)
                {
                    var oldHandler = _propertyChanged;
                    var newHandler = (PropertyChangedEventHandler)Delegate.Combine(oldHandler, value);

                    if (Interlocked.CompareExchange(ref _propertyChanged, newHandler, oldHandler) == oldHandler)
                        return;
                }
            }
            remove
            {
                while (true)
                {
                    var oldHandler = _propertyChanged;
                    var newHandler = (PropertyChangedEventHandler)Delegate.Remove(oldHandler, value);

                    if (Interlocked.CompareExchange(ref _propertyChanged, newHandler, oldHandler) == oldHandler)
                        return;
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            _propertyChanged.Raise(this, propertyName);
        }

        #endregion
    }
}