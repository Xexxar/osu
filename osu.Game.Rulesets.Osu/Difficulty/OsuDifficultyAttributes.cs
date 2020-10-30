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
        public double SpeedRating;

        public double JumpAimStrain;
        public IList<double> JumpAimComboStarRatings;
        public IList<double> JumpAimMissCounts;

        public double StreamAimStrain;
        public IList<double> StreamAimComboStarRatings;
        public IList<double> StreamAimMissCounts;

        public double StaminaStrain;
        public IList<double> StaminaComboStarRatings;
        public IList<double> StaminaMissCounts;

        public double SpeedStrain;
        public IList<double> SpeedComboStarRatings;
        public IList<double> SpeedMissCounts;

        public double AimControlStrain;
        public IList<double> AimControlComboStarRatings;
        public IList<double> AimControlMissCounts;

        public double FingerControlStrain;
        public IList<double> FingerControlComboStarRatings;
        public IList<double> FingerControlMissCounts;

        public double AccuracyStrain;

        public double ApproachRate;
        public double OverallDifficulty;
        public int MaxCombo;
    }
}
