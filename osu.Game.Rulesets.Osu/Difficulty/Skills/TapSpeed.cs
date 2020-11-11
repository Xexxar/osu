// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// idk something lol
    /// </summary>
    public class TapSpeed : OsuSkill
    {
        private double StrainDecay = 0.9;
        protected override double SkillMultiplier => 7.5;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.025;

        private int repeatStrainCount = 0;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            double strainTime = Math.Max(osuCurrent.DeltaTime, 40);

            StrainDecay = Math.Pow(0.95, 1000.0 / Math.Min(strainTime, 200.0));

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];

                if (Math.Abs(osuCurrent.StrainTime - osuPrevious.StrainTime) > 10.0)
                  repeatStrainCount = 0;
                else
                  repeatStrainCount++;
            }

            double strain = Math.Pow(75 / strainTime, 2.5) * Math.Pow(.975, repeatStrainCount);

            return strain;
        }
    }
}
