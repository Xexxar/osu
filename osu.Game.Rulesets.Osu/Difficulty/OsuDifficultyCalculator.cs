// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using MathNet.Numerics;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.0675;
        private const double star_rating_scale_factor = 1.485 * 3.0;
        private const double aim_star_factor = 1.1;
        private const double tapSpeed_star_factor = 1.1;
        private const double total_star_factor = 1.1;

        public OsuDifficultyCalculator(Ruleset ruleset, WorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        public double PointsTransformation(double skillRating) => Math.Pow(5.0f * Math.Max(1.0f, skillRating / difficulty_multiplier) - 4.0f, 3.0f) / 100000.0f;
        public double StarTransformation(double pointsRating) => difficulty_multiplier * (Math.Pow(100000.0f * pointsRating, 1.0f / 3.0f) + 4.0f) / 5.0f;

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = mods };

            // Todo: These int casts are temporary to achieve 1:1 results with osu!stable, and should be removed in the future
            double hitWindowGreat = (int)BeatmapDifficulty.DifficultyRange(beatmap.BeatmapInfo.BaseDifficulty.OverallDifficulty, 1800, 1200, 450) / clockRate;
            double overallDifficulty = hitWindowGreat > 1200 ? (1800 - hitWindowGreat) / 120 : (1200 - hitWindowGreat) / 150 + 5;
            double preempt = (int)BeatmapDifficulty.DifficultyRange(beatmap.BeatmapInfo.BaseDifficulty.ApproachRate, 1800, 1200, 450) / clockRate;

            int maxCombo = beatmap.HitObjects.Count;
            // Add the ticks + tail of the slider. 1 is subtracted because the head circle would be counted twice (once for the slider itself in the line above)
            maxCombo += beatmap.HitObjects.OfType<Slider>().Sum(s => s.NestedHitObjects.Count - 1);

            int totalHits = beatmap.HitObjects.Count;
            int hitCirclesCount = beatmap.HitObjects.Count(h => h is HitCircle);
            int spinnerCount = beatmap.HitObjects.Count(h => h is Spinner);
            int circles = beatmap.HitObjects.Count(h => h is HitCircle);
            int sliders = beatmap.HitObjects.Count(h => h is Slider);

            var aim = (OsuSkill)skills[0];
            var speed = (OsuSkill)skills[1];

            IList<double> aimComboSr = aim.ComboStarRatings;
            IList<double> aimMissCounts = aim.MissCounts;

            IList<double> speedComboSr = speed.ComboStarRatings;
            IList<double> speedMissCounts = speed.MissCounts;

            const double miss_sr_increment = OsuSkill.MISS_STAR_RATING_INCREMENT_MULTIPLIER;
            const double miss_sr_exponent = OsuSkill.MISS_STAR_RATING_INCREMENT_EXPONENT;

            double aimRating = aimComboSr.Last();
            double speedRating = speedComboSr.Last();

            double starRating = StarTransformation(star_rating_scale_factor * Math.Pow(
                Math.Pow(aimRating, total_star_factor) +
                Math.Pow(speedRating, total_star_factor), 1.0 / total_star_factor));

            string values = "Aim: " + Math.Round(aimRating, 2) +
            "\nSpeed: " + Math.Round(speedRating, 2);

            return new OsuDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,

                MissStarRatingIncrement = miss_sr_increment,
                MissStarRatingExponent = miss_sr_exponent,

                ApproachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5,
                OverallDifficulty = overallDifficulty,
                MaxCombo = maxCombo,
                CountCircles = circles,
                CountSliders = sliders,
                HitCircleCount = hitCirclesCount,
                SpinnerCount = spinnerCount,

                AimStrain = aimRating,
                AimComboStarRatings = aimComboSr,
                AimMissCounts = aimMissCounts,
                SpeedStrain = speedRating,
                SpeedComboStarRatings = speedComboSr,
                SpeedMissCounts = speedMissCounts,
            };
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                var lastLast = i > 1 ? beatmap.HitObjects[i - 2] : null;
                var last = beatmap.HitObjects[i - 1];
                var current = beatmap.HitObjects[i];

                yield return new OsuDifficultyHitObject(current, lastLast, last, clockRate);
            }
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap) => new Skill[]
        {
            new Aim(),
            new Speed()
        };

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
        };
    }
}
