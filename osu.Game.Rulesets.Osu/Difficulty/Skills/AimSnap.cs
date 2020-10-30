// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class AimSnap : OsuSkill
    {
        private double StrainDecay = 0.15;
        private const float prevMultiplier = 0.33f;
        private const double distThresh = 125;

        protected override double SkillMultiplier => 45;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.07;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 1;

            if (current.BaseObject is Spinner)
                return 0;

            double strain = 0;

            if (Previous.Count > 1 && osuNextObj.Angle != null)
            {
                StrainDecay = Math.Pow(.85, 1000.0 / Math.Min(osuCurrentObj.StrainTime, 200.0));

                var osuPrevObj = (OsuDifficultyHitObject)Previous[1];
                var osuCurrentObj = (OsuDifficultyHitObject)Previous[0];
                var osuNextObj = (OsuDifficultyHitObject)current;

                var dCurr2Next = osuNextObj.JumpDistance;
                var dPrev2Curr = osuCurrentObj.JumpDistance;

                var x = (osuCurrentObj.JumpDistance - Math.Pow(Math.Sin(Math.Min(osuNextObj.JumpDistance / 1.25, Math.PI / 2)), 2))
                        * (osuCurrentObj.JumpDistance / 3)
                        * Math.Pow(Math.Sin((double)osuCurrentObj.Angle), 2)
                        * (osuCurrentObj.DeltaTime / 50);

                var snappiness = 0.5 * erf((-75 + x) / (25 * Math.Sqrt(2))) + 0.5;

                var prevVector = Vector2.Multiply(Vector2.Divide(osuPrevObj.DistanceVector, (float)osuPrevObj.StrainTime), prevMultiplier);
                var currVector = Vector2.Divide(osuCurrentObj.DistanceVector, (float)osuCurrentObj.StrainTime);

                var adjVelocity = Vector2.Add(CurrVector, vPrev2Curr).Length;

                strain = adjVelocity * snappiness;
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
