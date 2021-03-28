// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : Skill
    {
        protected override double SkillMultiplier => 1.6;
        protected override double StrainDecayBase => 0;

        private double speedSkillMultiplier => 16;
        private double speedStrainDecayBase => .875;
        private double speedCurrentStrain = 1.0;

        private double staminaSkillMultiplier => 1;
        private double staminaStrainDecayBase => .975;
        private double staminaCurrentStrain = 1.0;

        public double DifficultyRating { get; private set; }

        private const double stars_per_double = 1.05;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        protected override double StrainValueOf(DifficultyHitObject current)
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

                double ms = (osuPrevObj.StrainTime + osuCurrObj.StrainTime) / 2;


                // if (ms < 75)
                //     strain = Math.Pow(1 / (ms - 20), 1);
                // else
                    strain = 1 / (ms - 20);
            }

            return strain;
        }

        public void ProcessSpeed(DifficultyHitObject current)
        {
            speedCurrentStrain *= Math.Pow(Math.Pow(speedStrainDecayBase, 1000 / Math.Min(300, current.DeltaTime)), current.DeltaTime / 1000);
            speedCurrentStrain += StrainValueOf(current) * speedSkillMultiplier;

            staminaCurrentStrain *= Math.Pow(Math.Pow(staminaStrainDecayBase, 1000 / Math.Min(300, current.DeltaTime)), current.DeltaTime / 1000);
            staminaCurrentStrain += StrainValueOf(current) * staminaSkillMultiplier;

            Previous.Push(current);

            double k = Math.Log(2) / Math.Log(stars_per_double);

            double strain = SkillMultiplier * (speedCurrentStrain + staminaCurrentStrain);
            CurrentStrain = strain;

            // Console.WriteLine("Speed: " + speedCurrentStrain);
            // Console.WriteLine("stamina: " + staminaCurrentStrain);

            DifficultyRating = Math.Pow(Math.Pow(DifficultyRating, k) + Math.Pow(strain, k), 1 / k);
        }
    }
}
