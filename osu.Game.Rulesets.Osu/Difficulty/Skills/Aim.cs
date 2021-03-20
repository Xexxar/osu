// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : Skill
    {
        public Aim(Mod[] mods)
            : base(mods)
        {
        }

        protected override double SkillMultiplier => 12.5;
        protected override double StrainDecayBase => 0.0;

        public double DifficultyRating { get; private set; }
        private const double stars_per_double = 1.125;
        private double AimCurrentStrain = 1.0;

        private double strainDecay(double ms) => Math.Pow(Math.Pow(.85, 1000 / Math.Min(ms, 300)), ms / 1000);//Math.Pow(.525, ms / 1000);


        protected override double StrainValueOf(DifficultyHitObject current) => StrainValueOf(current, 0);

        private double StrainValueOf(DifficultyHitObject current, double speedTapBonus)
        {
            if (current.BaseObject is Spinner)
                return 0;

            double strain = 0;

            if (Previous.Count > 1)
            {
                // though we are on current, we want to use current as reference for previous,
                // this is kind of retarded, but real devs can make this less retarded probably
                // for what its worth, difficulty calculation is a function of Previous[0]
                var osuPrevObj = (OsuDifficultyHitObject)Previous[1];
                var osuCurrObj = (OsuDifficultyHitObject)Previous[0];
                var osuNextObj = (OsuDifficultyHitObject)current;

                // Create velocity vectors,
                var prevVector = Vector2.Divide(osuPrevObj.DistanceVector, (float)osuPrevObj.StrainTime);
                var currVector = Vector2.Divide(osuCurrObj.DistanceVector, (float)osuCurrObj.StrainTime);
                var nextVector = Vector2.Divide(osuNextObj.DistanceVector, (float)osuNextObj.StrainTime);
                var diffPrevVector = Vector2.Add(currVector, prevVector);
                var diffNextVector = Vector2.Add(currVector, nextVector);
                var diffVelocityBonus = Math.Min(Vector2.Add(diffPrevVector, diffNextVector).Length, Vector2.Subtract(diffPrevVector, diffNextVector).Length);

                // double realVelocity = (osuCurrObj.StrainTime / (osuCurrObj.StrainTime - 15)) * Math.Max(currVector.Length, diffPrevVector.Length) + diffVelocityBonus;

                // // Slider Stuff
                double sliderVelocity = 0;
                if (osuPrevObj.BaseObject is Slider osuSlider)
                    sliderVelocity = (Math.Max(osuSlider.LazyTravelDistance, 1) / 50) / (50 + osuSlider.LazyTravelTime);

                double realVelocity = currVector.Length + speedTapBonus *
                                        ((osuCurrObj.StrainTime + osuPrevObj.StrainTime) / (osuCurrObj.StrainTime + osuPrevObj.StrainTime) * diffPrevVector.Length
                                          + diffVelocityBonus) / 2;

                strain = realVelocity + Math.Sqrt(sliderVelocity * realVelocity) + sliderVelocity;
            }

            return strain;
        }

        public void ProcessAim(DifficultyHitObject current, double currSpeedStrain)
        {
            double speedTapBonus = Math.Sqrt(1 + currSpeedStrain / 1.5);
            // Console.WriteLine(speedTapBonus);

            AimCurrentStrain *= strainDecay(current.DeltaTime);
            AimCurrentStrain += StrainValueOf(current, speedTapBonus) * SkillMultiplier;

            Previous.Push(current);

            double k = Math.Log(2) / Math.Log(stars_per_double);

            DifficultyRating = Math.Pow(Math.Pow(DifficultyRating, k) + Math.Pow(AimCurrentStrain, k), 1 / k);
        }
    }
}
