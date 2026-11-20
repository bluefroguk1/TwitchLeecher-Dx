using System;
using System.Collections.Generic;

namespace TwitchLeecher.Core.Models
{
    public class RuntimeData
    {
        #region Properties

        public Version Version { get; set; }

        public AuthInfo AuthInfo { get; set; }

        public MainWindowInfo MainWindowInfo { get; set; }

        public List<SearchParameters> RecentSearches { get; set; } = new List<SearchParameters>();

        #endregion Properties
    }
}