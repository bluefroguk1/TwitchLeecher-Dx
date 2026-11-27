using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitchLeecher.Core.Models
{
    public class DefaultQuality
    {
        #region Static Fields

        public static int SOURCE_RES = 1080;
        public static int AUDIO_ONLY_RES = 0;

        #endregion Static Fields

        #region Constructors

        public DefaultQuality(int resolution, string displayString)
        {
            if (string.IsNullOrWhiteSpace(displayString))
            {
                throw new ArgumentNullException(nameof(displayString));
            }

            VerticalResolution = resolution;
            DisplayString = displayString;
            IsSource = resolution == SOURCE_RES;
            IsAudioOnly = resolution == AUDIO_ONLY_RES;
        }

        #endregion Constructors

        #region Properties

        public int VerticalResolution { get; }

        public string DisplayString { get; }

        public bool IsSource { get; }

        public bool IsAudioOnly { get; }

        #endregion Properties

        #region Methods

        // The quality to download for this default: source, audio only, else the highest at or below
        // this resolution, else the lowest above it, else source.
        public TwitchVideoQuality PickFrom(List<TwitchVideoQuality> qualities)
        {
            TwitchVideoQuality sourceQuality = qualities.Find(q => q.IsSource) ?? qualities[0];

            if (IsSource)
            {
                return sourceQuality;
            }

            if (IsAudioOnly)
            {
                return qualities.FirstOrDefault(q => q.IsAudioOnly) ?? sourceQuality;
            }

            List<TwitchVideoQuality> visual = qualities.Where(q => !q.IsAudioOnly).ToList();

            return visual.Where(q => q.VerticalResolution <= VerticalResolution).OrderByDescending(q => q.VerticalResolution).FirstOrDefault()
                   ?? visual.Where(q => q.VerticalResolution >= VerticalResolution).OrderBy(q => q.VerticalResolution).FirstOrDefault()
                   ?? sourceQuality;
        }

        public override string ToString()
        {
            return DisplayString;
        }

        #endregion Methods
    }
}