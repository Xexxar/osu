// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the difficulty required to keep up with the note density and tapSpeed at which objects are needed to be tapped along with
    /// </summary>
    public class TapStamina : OsuSkill
    {
        private double StrainDecay = 1.0;
        protected override double SkillMultiplier => 4.45;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.05;
        private int repeatStrainCount = 1;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;

            StrainDecay = Math.Pow(31.0 / 32.0, 1000.0 / Math.Min(osuCurrent.StrainTime, 500.0));

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];

                if (Math.Abs(osuCurrent.StrainTime - osuPrevious.StrainTime) > 4.0) repeatStrainCount = 1;
                else repeatStrainCount++;
            }

            double strain = 75.0 / osuCurrent.StrainTime;

            return strain * Math.Pow(repeatStrainCount, 0.04);
        }
    }
}
