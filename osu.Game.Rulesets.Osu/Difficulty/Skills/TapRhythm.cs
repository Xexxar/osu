// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    ///
    /// </summary>
    public class TapRhythm : OsuSkill
    {
        private double StrainDecay = 0.5;
        protected override double SkillMultiplier => 42;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.04;

        private int repeatStrainCount = 0;

        private const double quarter220 = 60000 / (4 * 220);

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            double strainTime = Math.Max(osuCurrent.DeltaTime, 46.875);
            StrainDecay = Math.Pow(0.925, 1000.0 / Math.Min(strainTime, 375.0));

            double strain = Math.Pow(75.0 / strainTime, 1.5);

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];

                if (Math.Abs(osuCurrent.StrainTime - osuPrevious.StrainTime) > 4.0) repeatStrainCount = 1;
                else repeatStrainCount++;
            }

            if (osuCurrent.BaseObject is Slider)
                strain /= 2.0;

            if (repeatStrainCount % 2.0 == 0)
                return 0;
            else
                return strain / Math.Pow(1.15, repeatStrainCount);
        }
    }
}
