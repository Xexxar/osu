// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : Skill
    {
        private double CurrentStrain;
        private const double Base = 0.25;
        private const double SkillMultiplier = 500;
        private const double stars_per_double = 1.15;
        private double DifficultyRating = 0;

        public Aim(Mod[] mods)
            : base(mods)
        {
        }

        public override double DifficultyValue() => DifficultyRating;

        protected override void RemoveExtraneousHistory(DifficultyHitObject current)
        {
            while (Previous.Count > 4)
                Previous.Dequeue();
        }

        protected override void AddToHistory(DifficultyHitObject current)
        {
            Previous.Enqueue(current);
        }

        protected sealed override void Calculate(DifficultyHitObject current)
        {
            CurrentStrain *= Math.Pow(Base, current.DeltaTime / 1000);
            CurrentStrain += StrainValueOf(current) * SkillMultiplier;

            double k = Math.Log(2) / Math.Log(stars_per_double);

            // Console.WriteLine("Speed: " + speedCurrentStrain);
            // Console.WriteLine("stamina: " + staminaCurrentStrain);

            DifficultyRating = Math.Pow(Math.Pow(DifficultyRating, k) + Math.Pow(CurrentStrain, k), 1 / k);
        }

        protected double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;

            double result = 0;

            if (Previous.Count > 1)
            {
                result = osuCurrObj.DistanceVector.Length / osuCurrObj.StrainTime;
            }

            return result;
        }
    }
}
