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

            if (Previous.Count > 0 && osuNextObj.Angle != null)
            {
                var osuCurrentObj = (OsuDifficultyHitObject)Previous[0];
                var dPrev2Curr = osuCurrentObj.JumpDistance;

                //var osuPrevObj = (OsuDifficultyHitObject)Previous[1];
                //leave this commented out for now

                var x = (dPrev2Curr - Math.Pow(Math.Sin(Math.Min(dCurr2Next / 1.25, Math.PI / 2)), 2)) * (dPrev2Curr / 3) * Math.Pow(Math.Sin((double)osuCurrentObj.Angle), 2) * (osuCurrentObj.DeltaTime / 50);
                var snappiness = 0.5 * erf((-75 + x) / (25 * Math.Sqrt(2))) + 0.5;
            }

            return strain;

            /**
            var osuCurrent = (OsuDifficultyHitObject)current;
            if (osuCurrent.BaseObject is Slider && osuCurrent.TravelTime < osuCurrent.StrainTime)
              StrainDecay = Math.Min(osuCurrent.TravelTime, osuCurrent.StrainTime - 30.0) / osuCurrent.StrainTime *
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(1.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) +
                Math.Max(30.0, osuCurrent.StrainTime - osuCurrent.TravelTime) / osuCurrent.StrainTime * StrainDecay;

            double strain = 0;

            if (Previous.Count > 0 && osuCurrent.Angle != null)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                if (osuCurrent.JumpDistance >= distThresh && osuPrevious.JumpDistance >= distThresh)
                {
                    if (osuCurrent.Angle.Value <= angle_thresh)
                        strain = Math.Abs((applyDiminishingDist(osuCurrent.DistanceVector).Length - prevMultiplier * applyDiminishingDist(osuPrevious.DistanceVector).Length) + osuCurrent.TravelDistance) / Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
                    else
                    {
                        Vector2 Prev1 = new Vector2(
                            osuPrevious.DistanceVector.X * (float)Math.Cos(angle_thresh) - osuPrevious.DistanceVector.Y * (float)Math.Sin(angle_thresh),
                            osuPrevious.DistanceVector.X * (float)Math.Sin(angle_thresh) + osuPrevious.DistanceVector.Y * (float)Math.Cos(angle_thresh)
                        );
                        Vector2 Prev2 = new Vector2(
                            osuPrevious.DistanceVector.X * (float)Math.Cos(-angle_thresh) - osuPrevious.DistanceVector.Y * (float)Math.Sin(-angle_thresh),
                            osuPrevious.DistanceVector.X * (float)Math.Sin(-angle_thresh) + osuPrevious.DistanceVector.Y * (float)Math.Cos(-angle_thresh)
                        );
                        double strain1 = (applyDiminishingDist(osuCurrent.DistanceVector) + prevMultiplier * applyDiminishingDist(Prev1)).Length;
                        double strain2 = (applyDiminishingDist(osuCurrent.DistanceVector) + prevMultiplier * applyDiminishingDist(Prev2)).Length;

                        strain = (Math.Min(strain1, strain2) + osuCurrent.TravelDistance) / Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
                    }
                }
            }
            else if (osuCurrent.JumpDistance >= distThresh)
                strain = (applyDiminishingDist(osuCurrent.DistanceVector).Length + osuCurrent.TravelDistance) / osuCurrent.StrainTime;

            return strain;
        }
        **/

            //private Vector2 applyDiminishingDist(Vector2 val) => val - (float)distThresh * val.Normalized();
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
