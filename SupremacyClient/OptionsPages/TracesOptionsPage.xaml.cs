﻿using System;

using Supremacy.Annotations;
using Supremacy.Resources;

namespace Supremacy.Client.OptionsPages
{
    /// <summary>
    /// Interaction logic for TracesOptionsPage.xaml
    /// </summary>
    public partial class TracesOptionsPage : IClientOptionsPage
    {
        private readonly IResourceManager _resourceManager;

        public TracesOptionsPage([NotNull] IResourceManager resourceManager)
        {
            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");
            _resourceManager = resourceManager;
            InitializeComponent();
        }

        public string Header
        {
            get { return _resourceManager.GetString("SETTINGS_TRACES_TAB"); }
        }
    }
}
