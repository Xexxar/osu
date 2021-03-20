// ﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// // See the LICENCE file in the repository root for full licence text.
//
// using System;
// using osu.Game.Rulesets.Difficulty.Preprocessing;
// using osu.Game.Rulesets.Difficulty.Skills;
// using osu.Game.Rulesets.Mods;
// using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
// using osu.Game.Rulesets.Osu.Objects;
// using osuTK;
//
// namespace osu.Game.Rulesets.Osu.Difficulty.Skills
// {
//     /// <summary>
//     /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
//     /// </summary>
//     public class Aim : Skill
//     {
//         public Aim(Mod[] mods)
//             : base(mods)
//         {
//         }
//
//         protected override double SkillMultiplier => 50;
//         protected override double StrainDecayBase => 0.0;
//
//         public double DifficultyRating { get; private set; }
//         private const double stars_per_double = 1.05;
//         private double AimCurrentStrain = 1.0;
//
//
//         // TODO probably should look at a better way to do this than just *.925, maybe we can create a custom decay function that scales a bit more so we dont overweight sliders (yeah thats possible lol)
//         // Custom decay functions allow us to isolate our view to a certain count of objects, rather than relating things by this arbitrary concept known as "time"
//         // We do decay harsher past 300 ms because there should be little relationship between objects exceeding that amount.
//         private double strainFlowDecay(double ms) => Math.Pow(Math.Pow(.925, 1000 / Math.Min(ms, 300)), ms / 1000);//Math.Pow(.525, ms / 1000);
//         private double strainSnapDecay(double ms) => Math.Pow(Math.Pow(.85, 1000 / Math.Min(ms, 300)), ms / 1000);//Math.Pow(.25, ms / 1000);
//
//         // snap and flow multiplier variables
//         private double SkillSnapMultiplier => 1500;
//         private double currentSnapStrain;
//         private double SkillFlowMultiplier => 1200;
//         private double currentFlowStrain;
//         private double currentHybridStrain;
//
//         // General skill utility.
//         private double prevSnapProb;
//         private double prevSnapStrain;
//         private double prevFlowStrain;
//         private double weightedSnapProb;
//         private double weightedFlowProb;
//
//         private double snapStrainValueOf(double sliderVelocity,
//                                          OsuDifficultyHitObject osuCurrObj,
//                                          OsuDifficultyHitObject osuPrevObj,
//                                          Vector2 currVector,
//                                          Vector2 prevVector,
//                                          Vector2 diffPrevVector,
//                                          Vector2 diffNextVector,
//                                          double snapProb)
//         {
//                 // We define velocity by taking 2/3 the curre velocity + the difference.
//                 // This is a good way to reward for weird angles or changes in jump distance.
//                 double snapVelocity = (2 * currVector.Length + prevSnapProb * diffPrevVector.Length) / 3;
//                 snapVelocity *= (osuCurrObj.StrainTime / (osuCurrObj.StrainTime - 45));
//
//                 // Set snapStrain. Gotta multiply by snapProb to award for only snapped objects
//                 return (snapVelocity + sliderVelocity + Math.Sqrt(snapVelocity * sliderVelocity)) * snapProb;
//         }
//
//         private double flowStrainValueOf(double sliderVelocity,
//                                          OsuDifficultyHitObject osuCurrObj,
//                                          OsuDifficultyHitObject osuPrevObj,
//                                          Vector2 currVector,
//                                          Vector2 prevVector,
//                                          Vector2 diffPrevVector,
//                                          Vector2 diffNextVector,
//                                          double flowProb)
//         {
//                 // TODO comment this with explanations (i dont really remember whats going on either XD)
//                 double prevFlowProb = 1 - prevSnapProb * Math.Min(1, 2 * osuPrevObj.JumpDistance);
//
//                 double flowVelocity = (prevFlowProb * diffPrevVector.Length + 2 * osuCurrObj.JumpDistance / (osuCurrObj.StrainTime - 30)) / 3;
//
//                 double velChangeBonus = Math.Min(1, osuPrevObj.JumpDistance) * Math.Abs(currVector.Length - diffPrevVector.Length) * prevFlowProb;
//
//                 return (sliderVelocity + Math.Sqrt(flowVelocity * sliderVelocity) + velChangeBonus + flowVelocity) * flowProb;
//         }
//
//         protected override double StrainValueOf(DifficultyHitObject current)
//         {
//             if (current.BaseObject is Spinner)
//                 return 0;
//
//             double strain = 0;
//
//             if (Previous.Count > 1)
//             {
//                 // though we are on current, we want to use current as reference for previous,
//                 // this is kind of retarded, but real devs can make this less retarded probably
//                 // for what its worth, difficulty calculation is a function of Previous[0]
//                 var osuPrevObj = (OsuDifficultyHitObject)Previous[1];
//                 var osuCurrObj = (OsuDifficultyHitObject)Previous[0];
//                 var osuNextObj = (OsuDifficultyHitObject)current;
//
//                 // here we generate a value of being snappy or flowy that is fed into the gauss error function to build a probability.
//                 double snapProb = 1;// snapProbability(osuCurrObj, osuNextObj);
//                 double flowProb = (1 - snapProb) * Math.Min(1, 2 * osuCurrObj.JumpDistance); // reducing flow proabbility if distance is less than .5... we dont move l o l
//
//                 // Create velocity vectors,
//                 var prevVector = Vector2.Divide(osuPrevObj.DistanceVector, (float)osuPrevObj.StrainTime);
//                 var currVector = Vector2.Divide(osuCurrObj.DistanceVector, (float)osuCurrObj.StrainTime);
//                 var nextVector = Vector2.Divide(osuNextObj.DistanceVector, (float)osuNextObj.StrainTime);
//                 var diffPrevVector = Vector2.Add(currVector, prevVector);
//                 var diffNextVector = Vector2.Add(currVector, nextVector);
//
//                 // // Slider Stuff
//                 double sliderVelocity = 0;
//                 if (osuPrevObj.BaseObject is Slider osuSlider)
//                     sliderVelocity = (Math.Max(osuSlider.LazyTravelDistance, 1) / 50) / (50 + osuSlider.LazyTravelTime);
//
//                 //////////////////////////Calc difficulty/////////////////////////////////
//                 double snapStrain = snapStrainValueOf(sliderVelocity, osuCurrObj, osuPrevObj, currVector, prevVector, diffPrevVector, diffNextVector, snapProb);
//                 double flowStrain = flowStrainValueOf(sliderVelocity, osuCurrObj, osuPrevObj, currVector, prevVector, diffPrevVector, diffNextVector, flowProb);
//
//
//                 currentFlowStrain *= strainFlowDecay(osuCurrObj.DeltaTime);
//                 currentFlowStrain += flowStrain;
//                 currentSnapStrain *= strainSnapDecay(osuCurrObj.DeltaTime);
//                 currentSnapStrain += snapStrain;
//
//                 weightedSnapProb = .75 * weightedSnapProb + .25 * snapProb;
//                 weightedFlowProb = .75 * weightedFlowProb + .25 * flowProb;
//
//                 currentHybridStrain *= Math.Pow(.45, osuCurrObj.DeltaTime / 1000); //TODO make a strain skill
//                 currentHybridStrain += 2 * weightedSnapProb * weightedFlowProb * Math.Sqrt(flowStrain * prevSnapStrain + snapStrain * prevFlowStrain); //TODO make a multiplier
//
//
//                 strain = flowProb * currentFlowStrain + snapProb * currentSnapStrain + currentHybridStrain;
//
//                 // store the previous data so we can use it again next time
//                 prevSnapProb = snapProb;
//                 prevSnapStrain = snapStrain;
//                 prevFlowStrain = flowStrain;
//             }
//
//             return strain;
//         }
//
//         public void ProcessAim(DifficultyHitObject current)
//         {
//             AimCurrentStrain = StrainValueOf(current) * SkillMultiplier;
//
//             Previous.Push(current);
//
//             double k = Math.Log(2) / Math.Log(stars_per_double);
//
//             DifficultyRating = Math.Pow(Math.Pow(DifficultyRating, k) + Math.Pow(AimCurrentStrain, k), 1 / k);
//         }
//
//         // TODO This probably should be implemented as a attribute of OsuDifficultyHitObject... but needing osuNextObj makes it suck.
//         private double snapProbability(OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuNextObj)
//         {
//             double prob = 0;
//
//             // TODO remember what the hell this code actually does... I mean I know why it works, but its autistic to read
//             // literally so scuffed
//             if (osuCurrObj.JumpDistance > .75)
//             {
//                 double snapValue = 2 * (50 + (osuCurrObj.StrainTime - 50) / osuCurrObj.JumpDistance)
//                                      * ((osuCurrObj.StrainTime - 50) / osuCurrObj.StrainTime)
//                                      * (Math.Max(0, osuCurrObj.JumpDistance - .75) / osuCurrObj.JumpDistance);
//                 double flowValue =  osuCurrObj.StrainTime / osuCurrObj.JumpDistance
//                                   * (1 - .75 * Math.Pow(Math.Sin(Math.PI / 2.0 * Math.Min(1, Math.Max(0, osuNextObj.JumpDistance - 0.5))), 2)
//                                   * Math.Pow(Math.Sin((Math.PI - (double)osuCurrObj.Angle) / 2), 2));
//                 double diffValue = snapValue - flowValue;
//                 prob = 0.5 + 0.5 * erf(diffValue / (10 * Math.Sqrt(2)));
//           }
//           return prob;
//         }
//
//         // We use an ERF to calculate the probability. This just happened to be the first suggestion from google.
//         private static double erf(double x)
//         {
//             // constants
//             double a1 = 0.254829592;
//             double a2 = -0.284496736;
//             double a3 = 1.421413741;
//             double a4 = -1.453152027;
//             double a5 = 1.061405429;
//             double p = 0.3275911;
//
//             // Save the sign of x
//             int sign = 1;
//             if (x < 0)
//                 sign = -1;
//             x = Math.Abs(x);
//
//             // A&S formula 7.1.26
//             double t = 1.0 / (1.0 + p * x);
//             double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
//
//             return sign * y;
//         }
//     }
// }
