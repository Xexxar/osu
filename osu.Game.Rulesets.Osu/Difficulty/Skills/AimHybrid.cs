// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    ///
    /// </summary>
    public class AimHybrid : OsuSkill
    {
        public override double strainDecay(double ms) => Math.Pow(.4, ms / 1000);
        private const float prevMultiplier = 0.65f;
        protected override double SkillMultiplier => 1.2;
        protected override double StrainDecayBase => 0;
        protected override double StarMultiplierPerRepeat => 1.05;
        private const double degrees45 = Math.PI / 4;
        private double priorProb = 0;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
          double smallCSBuff = 1.0;

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


              // here we generate a value of being snappy or flowy that is fed into the gauss error function to build a probability.
              double snapProb = 0;
              double flowProb = 1;

              if (osuCurrObj.JumpDistance > .75)
              {
                double snapValue = 2 * (50 + (osuCurrObj.StrainTime - 50) / osuCurrObj.JumpDistance)
                                     * ((osuCurrObj.StrainTime - 50) / osuCurrObj.StrainTime)
                                     * (Math.Max(0, osuCurrObj.JumpDistance - .75) / osuCurrObj.JumpDistance);
                double flowValue =  osuCurrObj.StrainTime / osuCurrObj.JumpDistance
                                      * (1 - .75 * Math.Pow(Math.Sin(Math.PI / 2.0 * Math.Min(1, Math.Max(0, osuNextObj.JumpDistance - 0.5))), 2)
                                      * Math.Pow(Math.Sin((Math.PI - (double)osuCurrObj.Angle) / 2), 2));
                double diffValue = snapValue - flowValue;
                snapProb = 0.5 + 0.5 * erf(diffValue / (10 * Math.Sqrt(2)));
                flowProb = 1- snapProb;

              //  Console.WriteLine("snapValue: " + snapValue);
              //  Console.WriteLine("flowValue " + flowValue);
              }

              // Create velocity vectors, scale prior by prevMultiplier
            //  var prevVector = Vector2.Multiply(Vector2.Divide(osuPrevObj.DistanceVector, (float)osuPrevObj.StrainTime), prevMultiplier);
              var prevVector = Vector2.Divide(osuPrevObj.DistanceVector, (float)osuPrevObj.StrainTime);
              var currVector = Vector2.Divide(osuCurrObj.DistanceVector, (float)osuCurrObj.StrainTime);
              var nextVector = Vector2.Divide(osuNextObj.DistanceVector, (float)osuNextObj.StrainTime);

              double snapAngleBuff = 0.0;
              double flowAngleBuff = 0.0;
              double angle = (double)osuCurrObj.Angle;

              double snapVelocity = currVector.Length;// * osuCurrObj.StrainTime / (osuCurrObj.StrainTime - 25);

              double flowVelocity = 0;
              if (osuCurrObj.JumpDistance < 1)
                flowVelocity = osuCurrObj.JumpDistance / osuCurrObj.StrainTime;
              else
                flowVelocity = Math.Pow(osuCurrObj.JumpDistance, 1.5) / osuCurrObj.StrainTime;

              if (angle < Math.PI / 4)
                snapAngleBuff = 0.0;
              else if (angle > 3 * Math.PI / 4)
                snapAngleBuff = 1.0;
              else
                snapAngleBuff = Math.Pow(Math.Sin(angle - Math.PI / 4), 2);

              if (angle < Math.PI / 4)
                flowAngleBuff = 1.0;
              else if (angle > 3 * Math.PI / 4)
                flowAngleBuff = 0.0;
              else
                flowAngleBuff = Math.Pow(Math.Sin(3 * Math.PI / 4 - angle), 2);

              double sliderVelocity = 0;
              if (osuPrevObj.BaseObject is Slider osuSlider)
                sliderVelocity = (Math.Max(osuSlider.LazyTravelDistance, 1) / 50) / (50 + osuSlider.LazyTravelTime);

              double snapStrain = (snapVelocity * (.6 + .3 * snapAngleBuff * Math.Min(1, osuPrevObj.JumpDistance))
                      + sliderVelocity
                      + Math.Sqrt(currVector.Length * sliderVelocity));

              double velChangeBonus = Math.Min(1, osuPrevObj.JumpDistance) * Math.Abs(currVector.Length - prevVector.Length) * priorProb;
              priorProb = flowProb;

             if (osuPrevObj.Angle < degrees45 && osuCurrObj.Angle < degrees45 && osuNextObj.Angle < degrees45)
               flowAngleBuff = 0;

              double flowStrain = (sliderVelocity + Math.Sqrt(flowVelocity * sliderVelocity)
              + velChangeBonus + flowVelocity * (1 + 1 * flowAngleBuff * Math.Min(1, osuPrevObj.JumpDistance)));

              strain = flowStrain * 1850 * flowProb +  2150 * snapStrain * snapProb;
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
