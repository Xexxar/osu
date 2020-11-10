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
        protected override double SkillMultiplier => 1.5;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.025;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;

            StrainDecay = Math.Pow(.99, 1000.0 / Math.Min(osuCurrent.StrainTime, 500.0));

            double strain = Math.Pow(75 / osuCurrent.StrainTime, 2.5);

            return strain;
        }
    }
}
