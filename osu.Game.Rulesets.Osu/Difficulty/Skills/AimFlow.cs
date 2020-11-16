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
    /// duhhh
    /// </summary>
    public class AimFlow : OsuSkill
    {
        private double StrainDecay = 0.25;
        private const float prevMultiplier = 0.45f;
        private const double degrees45 = Math.PI / 4;

        private double priorProb = 0;
        private int count = -1;

        protected override double SkillMultiplier => 1750;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.05;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            count++;
            double smallCSBuff = 1.0;
            StrainDecay = .25;

            if (current.BaseObject is Spinner)
                return 0;

            double strain = 0;

            if (Previous.Count > 1)
            {
                // though we are on current, we want to use current as reference for previous,
                // so for what its worth, difficulty calculation is a function of Previous[0]
                var osuPrevObj = (OsuDifficultyHitObject)Previous[1];
                var osuCurrObj = (OsuDifficultyHitObject)Previous[0];
                var osuNextObj = (OsuDifficultyHitObject)current;

                if (osuNextObj.BaseObject.Radius < 30)
                {
                    smallCSBuff = 1 + (30 - (float)osuNextObj.BaseObject.Radius) / 30;
                }

                var prevVector = Vector2.Divide(osuPrevObj.DistanceVector, (float)osuPrevObj.StrainTime);
                var currVector = Vector2.Divide(osuCurrObj.DistanceVector, (float)osuCurrObj.StrainTime);
                var nextVector = Vector2.Divide(osuNextObj.DistanceVector, (float)osuNextObj.StrainTime);

                StrainDecay = Math.Pow(.925, 1000.0 / Math.Min(osuCurrObj.StrainTime, 500.0));

                // Here we set a custom strain decay rate that decays based on # of objects rather than MS.
                // This is just so we can focus on balancing only the strain rewarded, and time no longer matters.
                // this will make a repeated pattern "cap out" or reach 85 maximum difficulty in 12 objects.
                double flowProb = 1;

                if (osuCurrObj.JumpDistance > .75)
                {
                  double snapValue = 2 * ((osuCurrObj.StrainTime - 50) / osuCurrObj.StrainTime)
                                       * (Math.Max(0, (osuCurrObj.JumpDistance - .75)) / osuCurrObj.JumpDistance)
                                       * (50 + (osuCurrObj.StrainTime - 50) / osuCurrObj.JumpDistance);
                  double flowValue =  osuCurrObj.StrainTime / osuCurrObj.JumpDistance
                                        * (1 - .75 * Math.Pow(Math.Sin(Math.PI / 2.0 * Math.Min(1, Math.Max(0, osuNextObj.JumpDistance - 0.5))), 2)
                                        * Math.Pow(Math.Sin((Math.PI - (double)osuCurrObj.Angle) / 2), 2));
                  double diffValue = snapValue - flowValue;
                  flowProb = 0.5 - 0.5 * erf(diffValue / (5 * Math.Sqrt(2)));
                }

                double sliderVelocity = 0;
                if (osuPrevObj.BaseObject is Slider osuSlider)
                  sliderVelocity = (Math.Max(osuSlider.LazyTravelDistance, 1) / 50) / (50 + osuSlider.LazyTravelTime);

                double angleBuff = 0.0;
                double angle = (double)osuCurrObj.Angle;

                if (angle < Math.PI / 4)
                  angleBuff = 1.0;
                else if (angle > 3 * Math.PI / 4)
                  angleBuff = 0.0;
                else
                  angleBuff = Math.Pow(Math.Sin(3 * Math.PI / 4 - angle), 2);

                // double velocity = 0;
                // if (osuCurrObj.JumpDistance < 1)
                //   velocity = (Math.Sin((Math.PI / 2) * (osuCurrObj.JumpDistance - 1)) + 1) / osuCurrObj.StrainTime;
                // else
                double velocity = osuCurrObj.JumpDistance / osuCurrObj.StrainTime;

                double velChangeBonus = Math.Min(1, osuPrevObj.JumpDistance) * Math.Abs(currVector.Length - prevVector.Length) * priorProb;
                priorProb = flowProb;




                // add them to get our final velocity, length is the observed velocity and thus the difficulty.
              //  var adjVelocity = Vector2.Subtract(currVector, Vector2.Multiply(prevVector, prevMultiplier)).Length;

               if (osuPrevObj.Angle < degrees45 && osuCurrObj.Angle < degrees45 && osuNextObj.Angle < degrees45)
                 angleBuff = 0;

                strain = (sliderVelocity + Math.Sqrt(velocity * sliderVelocity)
                + velChangeBonus + velocity * (1 + 1 * angleBuff * Math.Min(1, osuPrevObj.JumpDistance))) * flowProb;

                // Console.WriteLine("Count " + count);
                // Console.WriteLine("anglebuff: " + angleBuff);
                // Console.WriteLine("priorProb: " + priorProb);
                // Console.WriteLine("Strain: " + strain * 1000);
            }

            return smallCSBuff * strain;

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
