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
        private double StrainDecay = 0.25;
        private const float prevMultiplier = 0.45f;
        protected override double SkillMultiplier => 2500;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.05;

        private int count = -1;

        private double priorSnapProb = 0;
        private double priorFlowProb = 1;
        private double snapStrainPrior = 0;
        private double flowStrainPrior = 0;

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
                //var osuPriorObj = (OsuDifficultyHitObject)Previous[2];
                var osuPrevObj = (OsuDifficultyHitObject)Previous[1];
                var osuCurrObj = (OsuDifficultyHitObject)Previous[0];
                var osuNextObj = (OsuDifficultyHitObject)current;

                if (osuNextObj.BaseObject.Radius < 30)
                {
                    smallCSBuff = 1 + (30 - (float)osuNextObj.BaseObject.Radius) / 30;
                }

                // Here we set a custom strain decay rate that decays based on # of objects rather than MS.
                // This is just so we can focus on balancing only the strain rewarded, and time no longer matters.
                // this will make a repeated pattern "cap out" or reach 85 maximum difficulty in 12 objects.
                StrainDecay = Math.Pow(.925, 1000.0 / Math.Min(osuCurrObj.StrainTime, 500.0));

                // here we generate a value of being snappy or flowy that is fed into the gauss error function to build a probability.
                double flowProb = 1;
                double snapProb = 0;

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
                  snapProb = 1 - flowProb;
                }

                double grayProb = (4 * flowProb * snapProb);
                double diffSnapProb = snapProb - priorSnapProb; //1 - Math.Abs(snapProb + priorSnapProb - 1);
                double diffFlowProb = flowProb - priorFlowProb; //1 - Math.Abs(flowProb + priorFlowProb - 1);

                priorSnapProb = snapProb;
                priorFlowProb = flowProb;

                var prevVector = Vector2.Divide(osuPrevObj.DistanceVector, (float)osuPrevObj.StrainTime);
                var currVector = Vector2.Divide(osuCurrObj.DistanceVector, (float)osuCurrObj.StrainTime);

                double angleBuff = 0.0;
                double angle = (double)osuCurrObj.Angle;

                if (angle < Math.PI / 4)
                  angleBuff = 1.0;
                else if (angle > 3 * Math.PI / 4)
                  angleBuff = 0.0;
                else
                  angleBuff = Math.Pow(Math.Sin(Math.PI / 4 - angle), 2);

                double grayStrain = currVector.Length * grayProb;

                double snapVelocity = currVector.Length * osuCurrObj.StrainTime / (osuCurrObj.StrainTime - 45);
                double flowVelocity = osuCurrObj.JumpDistance / osuCurrObj.StrainTime;

                double snapStrain = snapVelocity * (.6 + .3 * angleBuff * Math.Min(1, osuPrevObj.JumpDistance)) ;
                double flowStrain = flowVelocity * (1.0 + 1.0 * (1 - angleBuff) * Math.Min(1, osuPrevObj.JumpDistance));

                strain = 30 * Math.Max(diffSnapProb * flowStrainPrior * snapStrain, diffFlowProb * flowStrain * snapStrainPrior);
                strain = Math.Max(grayStrain, strain);

                flowStrainPrior = flowStrain;
                snapStrainPrior = snapStrain;

                // Create velocity vectors, scale prior by prevMultiplier
                // var prevVector = Vector2.Multiply(Vector2.Divide(osuPrevObj.DistanceVector, (float)osuPrevObj.StrainTime), prevMultiplier);
                // var currVector = Vector2.Divide(osuCurrObj.DistanceVector, (float)osuCurrObj.StrainTime);



                // double sliderVelocity = 0;
                // if (osuPrevObj.BaseObject is Slider osuSlider)
                //   sliderVelocity = (Math.Max(osuSlider.LazyTravelDistance, 1) / 50) / (50 + osuSlider.LazyTravelTime);
                //
                // double angleBuff = 0.0;
                // double angle = (double)osuCurrObj.Angle;
                //
                // if (angle < Math.PI / 4)
                //   angleBuff = 1.0;
                // else if (angle > 3 * Math.PI / 4)
                //   angleBuff = 0.0;
                // else
                //   angleBuff = Math.Pow(Math.Sin(Math.PI / 4 - angle), 2);
                //
                // double snapVelocity = currVector.Length * osuCurrObj.StrainTime / (osuCurrObj.StrainTime - 45);
                // double flowVelocity = currVector.Length * osuCurrObj.StrainTime / (osuCurrObj.StrainTime - 15);
                //
                // var grayStrain = currVector.Length * grayProb;
                // var snapStrain = snapProb * Math.Min(1, Math.Max(0, osuPrevObj.JumpDistance-.5)) * Math.Min(1, Math.Max(0, osuNextObj.JumpDistance))
                // * (snapVelocity * (.66 + .33 * angleBuff * Math.Min(1, osuPrevObj.JumpDistance))
                //                   + sliderVelocity + Math.Sqrt(currVector.Length * sliderVelocity))
                //                   * diffSnapProb;
                // var flowStrain = flowProb * flowVelocity * diffFlowProb * (.66 + .66 * (1 - angleBuff) * Math.Min(1, osuPrevObj.JumpDistance));
                //
                // strain = Math.Max(grayStrain, Math.Max(snapStrain, flowStrain));

                // Console.WriteLine("Count " + count);
                // Console.WriteLine("anglebuff: " + angleBuff);
                // Console.WriteLine("diffsnap: "+ diffSnapProb);
                // Console.WriteLine("diffflow: " +  diffFlowProb);
                // Console.WriteLine("Strain: " + strain * 1000);
                        //* Math.Pow(Math.Sin(Math.PI / 2.0 * Math.Min(1, osuNextObj.JumpDistance)), 2);
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
