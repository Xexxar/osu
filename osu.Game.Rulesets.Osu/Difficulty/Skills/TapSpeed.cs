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
        protected override double SkillMultiplier => 700;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.05;

        private int repeatStrainCount = 0;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;
            double strain = 0;

            if (Previous.Count > 1)
            {
                var osuPrevObj = (OsuDifficultyHitObject)Previous[1];
                var osuCurrObj = (OsuDifficultyHitObject)Previous[0];
                var osuNextObj = (OsuDifficultyHitObject)current;

                double strainTime = Math.Max(osuCurrObj.DeltaTime, 40);
                StrainDecay = Math.Pow(0.975, 1000.0 / Math.Min(strainTime, 200.0));

                double snapProb = 0;

                if (osuCurrObj.JumpDistance > .75)
                {
                  double snapValue = 2 * (50 + (osuCurrObj.StrainTime - 50) / osuCurrObj.JumpDistance)
                                       * ((osuCurrObj.StrainTime - 50) / osuCurrObj.StrainTime)
                                       * (Math.Max(0, osuCurrObj.JumpDistance - .75) / osuCurrObj.JumpDistance);
                  double flowValue =  osuCurrObj.StrainTime / osuCurrObj.JumpDistance
                                        * (1 - .75 * Math.Pow(Math.Sin(Math.PI / 2.0 * Math.Min(1, Math.Max(0, osuNextObj.JumpDistance - 0.5))), 2)
                                        * Math.Pow(Math.Sin((Math.PI - (double)osuCurrObj.Angle) / 2), 2));
                  double diffValue = snapValue - flowValue;
                  snapProb = 0.5 + 0.5 * erf(diffValue / (3 * Math.Sqrt(2)));
                }

                if (Math.Abs(osuCurrObj.StrainTime - osuPrevObj.StrainTime) > 10.0)
                  repeatStrainCount = 0;
                else
                  repeatStrainCount++;

                double variantStrain = 0;
                if (repeatStrainCount % 2 == 0)
                  variantStrain = 1.25 * (1 / strainTime) * Math.Pow(.95, repeatStrainCount);

                strain = Math.Max(snapProb * (1 / strainTime), (1-snapProb) * variantStrain);
            }

            return strain;
        }

        private static double erf(double x)
        {
            // constants
            double a1 = 0.254829592;
            double a2 = -0.284496736;
            double a3 = 1.421413741;
            double a4 = -1.453152027;
            double a5 = 1.061405429;
            double p = 0.3275911;

            // Save the sign of x
            int sign = 1;
            if (x < 0)
                sign = -1;
            x = Math.Abs(x);

            // A&S formula 7.1.26
            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return sign * y;
        }
    }
}
