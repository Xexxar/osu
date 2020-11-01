// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyAttributes : DifficultyAttributes
    {
        public double MissStarRatingIncrement;
        public double MissStarRatingExponent;

        public double AimRating;
        public double TapSpeedRating;

        public double AimSnapStrain;
        public IList<double> AimSnapComboStarRatings;
        public IList<double> AimSnapMissCounts;

        public double AimFlowStrain;
        public IList<double> AimFlowComboStarRatings;
        public IList<double> AimFlowMissCounts;

        public double TapStaminaStrain;
        public IList<double> TapStaminaComboStarRatings;
        public IList<double> TapStaminaMissCounts;

        public double TapSpeedStrain;
        public IList<double> TapSpeedComboStarRatings;
        public IList<double> TapSpeedMissCounts;

        public double AimHybridStrain;
        public IList<double> AimHybridComboStarRatings;
        public IList<double> AimHybridMissCounts;

        public double TapRhythmStrain;
        public IList<double> TapRhythmComboStarRatings;
        public IList<double> TapRhythmMissCounts;

        public double AccuracyStrain;

        public double ApproachRate;
        public double OverallDifficulty;

        public double countSliders;
        public double countCircles;
    }
}
