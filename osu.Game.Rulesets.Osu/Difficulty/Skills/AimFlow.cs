// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// duhhh
    /// </summary>
    public class AimFlow : OsuSkill
    {
        private double StrainDecay = 0.25;
        private const double pi_over_2 = Math.PI / 2.0;
        private const double distThresh = 135;
        private const double strainThresh = 50;

        protected override double SkillMultiplier => 177000;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.07;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 0.25;

            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            if (osuCurrent.BaseObject is Slider && osuCurrent.TravelTime < osuCurrent.StrainTime) StrainDecay = (osuCurrent.TravelTime - 50) / osuCurrent.StrainTime *
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(1.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) +
                (osuCurrent.StrainTime - (osuCurrent.TravelTime - 50)) / osuCurrent.StrainTime * StrainDecay;

            double distance = osuCurrent.JumpDistance + osuCurrent.TravelDistance;
            double strainTime = Math.Max(strainThresh, osuCurrent.StrainTime - osuCurrent.TravelTime + 50);
            double angleBonus = 1.0;
            double currStrain = 0.05 + 0.95 * Math.Pow(Math.Sin(pi_over_2 * Math.Min(distance / distThresh, 1.0)), 6.0);

            if (osuCurrent.Angle != null)
                angleBonus += Math.Pow(Math.Sin(osuCurrent.Angle.Value), 2.0) / 3.0;

            return angleBonus * currStrain / Math.Pow(strainTime, 2.0);
        }
    }
}
