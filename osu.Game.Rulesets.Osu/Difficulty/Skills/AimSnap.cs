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
        private const float prevMultiplier = 0.45f;
        private double angle_thresh = Math.PI / 2.0 - Math.Acos(prevMultiplier / 2.0);
        private const double distThresh = 125;

        protected override double SkillMultiplier => 45;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.07;

        protected override double StrainValueOf(DifficultyHitObject current)
        // {
        //   double strain = 0.0;
        //   // first we grab vectors and straintimes for osuCurrent (2->3), osuNext (3->4), and OsuPrevious (1->2)
        //
        //   // first we want to calculate the probability of snap aim.
        //   // This is done by first calculating our x Value
        //   //
        //   // (osuCurrent. - (sin^2 (min(d2/90, pi/2))) * (d / 3) * sin^2(angle / 2) * (dt - 50)
        //   //
        //
        //   return strain;
        // }
        {
            StrainDecay = 0.15;

            if (current.BaseObject is Spinner)
                return 0;

            var osuNextObj = (OsuDifficultyHitObject)current;
            var dCurr2Next = osuNextObj.JumpDistance;

            double strain = 0;

            if (Previous.Count > 1 && osuNextObj.Angle != null)
            {
                var osuCurrentObj = (OsuDifficultyHitObject)Previous[0];
                StrainDecay = Math.Pow(.85, 1000.0 / Math.Min(osuCurrentObj.StrainTime, 200.0));

                var dPrev2Curr = osuCurrentObj.JumpDistance;

                //var osuPrevObj = (OsuDifficultyHitObject)Previous[1];
                //leave this commented out for now

                var x = (dPrev2Curr - Math.Pow(Math.Sin(Math.Min(dCurr2Next / 1.25, Math.PI / 2)), 2)) * (dPrev2Curr / 3) * Math.Pow(Math.Sin((double)osuCurrentObj.Angle), 2) * (osuCurrentObj.DeltaTime / 50);
                var snappiness = 0.5 * erf((-75 + x) / (25 * Math.Sqrt(2))) + 0.5;

                var vCurr2Next = Vector2.Divide(osuNextObj.DistanceVector, (float)osuNextObj.DeltaTime);
                var vPrev2Curr = Vector2.Multiply(Vector2.Divide(osuCurrentObj.DistanceVector, (float)osuCurrentObj.DeltaTime), (float)0.33);

                var adjVelocity = Vector2.Add(vCurr2Next, vPrev2Curr).Length;

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
