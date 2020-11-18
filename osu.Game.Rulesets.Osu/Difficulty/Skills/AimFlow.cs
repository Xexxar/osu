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
        public override double strainDecay(double ms) => Math.Pow(Math.Pow(.925, 1000 / Math.Min(ms, 150)), ms / 1000);
        private const float prevMultiplier = 0.45f;
        private const double degrees45 = Math.PI / 4;

        private double prevFlowProb = 0;
      //  private int count = -1;

        protected override double SkillMultiplier => 1300;
        protected override double StrainDecayBase => 0;
        protected override double StarMultiplierPerRepeat => 1.05;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
          if (current.BaseObject is Spinner)
              return 0;

          double smallCSBuff = 1.0;
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

              double flowProb = flowProbability(osuCurrObj, osuNextObj);

              // Sliders
              double sliderVelocity = 0;
              if (osuPrevObj.BaseObject is Slider osuSlider)
                sliderVelocity = (Math.Max(osuSlider.LazyTravelDistance, 1) / 50) / (50 + osuSlider.LazyTravelTime);

              var prevVector = Vector2.Divide(osuPrevObj.DistanceVector, (float)osuPrevObj.StrainTime);
              var currVector = Vector2.Divide(osuCurrObj.DistanceVector, (float)osuCurrObj.StrainTime);
              var diffVector = Vector2.Divide(Vector2.Subtract(osuCurrObj.DistanceVector, osuPrevObj.DistanceVector), (float)osuCurrObj.StrainTime);

              double velocity = (prevFlowProb * diffVector.Length + 2 * (Math.Pow(75 * currVector.Length, 2) / 75) / 3);//Math.Pow(osuCurrObj.JumpDistance, 1.5) / osuCurrObj.StrainTime + diffVector.Length * prevFlowProb ;

              // velocity = velocity * osuCurrObj.StrainTime;
              //
              // if (velocity > 1)
              //   velocity = Math.Pow(velocity, 2);
              //
              // velocity = velocity / osuCurrObj.StrainTime;

              // double currVelocity = osuCurrObj.JumpDistance / osuCurrObj.StrainTime;
              // double prevVelocity = osuPrevObj.JumpDistance / osuPrevObj.StrainTime;
              //
              double velChangeBonus = Math.Min(1, osuPrevObj.JumpDistance) * Math.Abs(currVector.Length - prevVector.Length) * prevFlowProb;
              prevFlowProb = flowProb;

              strain = (sliderVelocity + Math.Sqrt(velocity * sliderVelocity)
              + velChangeBonus + velocity) * flowProb;
          }
          return smallCSBuff * strain;
        }

        private double snapProbability(OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuNextObj)
        {
          double prob = 0;

          if (osuCurrObj.JumpDistance > .75)
          {
            double snapValue = 2 * (50 + (osuCurrObj.StrainTime - 50) / osuCurrObj.JumpDistance)
                                 * ((osuCurrObj.StrainTime - 50) / osuCurrObj.StrainTime)
                                 * (Math.Max(0, osuCurrObj.JumpDistance - .75) / osuCurrObj.JumpDistance);
            double flowValue =  osuCurrObj.StrainTime / osuCurrObj.JumpDistance
                                  * (1 - .75 * Math.Pow(Math.Sin(Math.PI / 2.0 * Math.Min(1, Math.Max(0, osuNextObj.JumpDistance - 0.5))), 2)
                                  * Math.Pow(Math.Sin((Math.PI - (double)osuCurrObj.Angle) / 2), 2));
            double diffValue = snapValue - flowValue;
            prob = 0.5 + 0.5 * erf(diffValue / (10 * Math.Sqrt(2)));
          }
          return prob;
        }

        private double flowProbability(OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuNextObj)
        {
          return 1 - snapProbability(osuCurrObj, osuNextObj);
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
