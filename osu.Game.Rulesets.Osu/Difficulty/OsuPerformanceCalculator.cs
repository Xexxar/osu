// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using MathNet.Numerics;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        public new OsuDifficultyAttributes Attributes => (OsuDifficultyAttributes)base.Attributes;

        private readonly double countHitCircles;
        private readonly double countSliders;
        private readonly int beatmapMaxCombo;

        private Mod[] mods;

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countGood;
        private int countMeh;
        private int countMiss;
        private const double combo_weight = 0.5;
        private const double aim_pp_factor = 1.1f;
        private const double tapSpeed_pp_factor = 1.1f;
        private const double total_factor = 1.1f;
        private const double skills_factor = 1.1f;

        public OsuPerformanceCalculator(Ruleset ruleset, DifficultyAttributes attributes, ScoreInfo score)
           : base(ruleset, attributes, score)
        {
            countHitCircles = Attributes.countCircles;
            countSliders = Attributes.countSliders;
            beatmapMaxCombo = Attributes.MaxCombo;
        }

        public override double Calculate(Dictionary<string, double> categoryRatings = null)
        {
            mods = Score.Mods;
            accuracy = Score.Accuracy;
            scoreMaxCombo = Score.MaxCombo;
            countGreat = Convert.ToInt32(Score.Statistics[HitResult.Great]);
            countGood = Convert.ToInt32(Score.Statistics[HitResult.Good]);
            countMeh = Convert.ToInt32(Score.Statistics[HitResult.Meh]);
            countMiss = Convert.ToInt32(Score.Statistics[HitResult.Miss]);

            // Don't count scores made with supposedly unranked mods
            if (mods.Any(m => !m.Ranked))
                return 0;


            double aim_multiplier = 1.0f;
            double tapSpeed_multiplier = 1.0f;
            double total_multiplier = 1.0f; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things
            double skills_multiplier = 1.0f;

            // Custom multipliers for NoFail and SpunOut.
            if (mods.Any(m => m is OsuModNoFail))
                total_multiplier *= 0.90f;

            if (mods.Any(m => m is OsuModSpunOut))
                total_multiplier *= 0.95f;

            double aimSnapValue = computeAimSnapValue(categoryRatings);
            double aimFlowValue = computeAimFlowValue(categoryRatings);
            double tapTapStaminaValue = computeTapStaminaValue(categoryRatings);
            double tapSpeedValue = computeTapSpeedValue(categoryRatings);
            double aimHybridValue = computeAimHybridValue(categoryRatings);
            double tapRhythmValue = computeTapRhythmValue(categoryRatings);
            double accuracyValue = computeAccuracyValue(categoryRatings);

            double totalAimValue = aim_multiplier * Math.Pow(
                Math.Pow(aimSnapValue, aim_pp_factor) +
                Math.Pow(aimFlowValue, aim_pp_factor) +
                Math.Pow(aimHybridValue, aim_pp_factor), 1.0f / aim_pp_factor);
            double totalTapSpeedValue = tapSpeed_multiplier * Math.Pow(
                Math.Pow(tapTapStaminaValue, tapSpeed_pp_factor) +
                Math.Pow(tapSpeedValue, tapSpeed_pp_factor) +
                Math.Pow(tapRhythmValue, tapSpeed_pp_factor), 1.0f / tapSpeed_pp_factor);

            if (mods.Any(h => h is OsuModFlashlight))
            {
                double modBonus = 1.0;

                // Apply object-based bonus for flashlight.
                modBonus += (0.35f * Math.Min(1.0f, totalHits / 200.0f) +
                        (totalHits > 200 ? 0.3f * Math.Min(1.0f, (totalHits - 200) / 300.0f) +
                        (totalHits > 500 ? (totalHits - 500) / 1200.0f : 0.0f) : 0.0f));
                if (mods.Any(h => h is OsuModHidden))
                    modBonus *= 1.1f;

                totalAimValue *= modBonus;
            }

            double totalskillValue = skills_multiplier * Math.Pow(
                Math.Pow(totalAimValue, skills_factor) +
                Math.Pow(totalTapSpeedValue, skills_factor), 1.0f / skills_factor);

            double totalValue = total_multiplier * Math.Pow(
                            Math.Pow(totalskillValue, total_factor) +
                            Math.Pow(accuracyValue, total_factor), 1.0f / total_factor);

            if (categoryRatings != null)
            {
                categoryRatings.Add("Total Star Rating", 0.0675f * (Math.Pow(300000.0f * totalValue, 1.0f / 3.0f) + 4.0f) / 5.0f);
                categoryRatings.Add("Aim Snap pp", aimSnapValue);
                categoryRatings.Add("Aim Flow pp", aimFlowValue);
                categoryRatings.Add("Aim Hybrid pp", aimHybridValue);
                categoryRatings.Add("Tap Stamina pp", tapTapStaminaValue);
                categoryRatings.Add("Tap Speed pp", tapSpeedValue);
                categoryRatings.Add("Tap Rhythm pp", tapRhythmValue);
                categoryRatings.Add("Total Aim pp", totalAimValue);
                categoryRatings.Add("Total Tap pp", totalTapSpeedValue);
                categoryRatings.Add("Accuracy pp", accuracyValue);
            }

            return totalValue;
        }

        private double interpComboStarRating(IList<double> values, double scoreCombo, double mapCombo)
        {
            if (mapCombo == 0)
            {
                return values.Last();
            }

            double comboRatio = scoreCombo / mapCombo;
            double pos = Math.Min(comboRatio * (values.Count), values.Count);
            int i = (int)pos;

            if (i == values.Count)
            {
                return values.Last();
            }

            if (pos <= 0)
            {
                return 0;
            }

            double ub = values[i];
            double lb = i == 0 ? 0 : values[i - 1];

            double t = pos - i;
            double ret = lb * (1 - t) + ub * t;

            return ret;
        }

        // get star rating corresponding to miss count in miss count list
        private double missStarRating(double sr, int i) => sr * (1 - Math.Pow((i + 1), Attributes.MissStarRatingExponent) * Attributes.MissStarRatingIncrement);

        private double interpMissCountStarRating(double sr, IList<double> values, int missCount)
        {
            double increment = Attributes.MissStarRatingIncrement;
            double t;

            if (missCount == 0)
            {
                // zero misses, return SR
                return sr;
            }

            if (missCount < values[0])
            {
                t = missCount / values[0];
                return sr * (1 - t) + missStarRating(sr, 0) * t;
            }

            for (int i = 0; i < values.Count; ++i)
            {
                if (missCount == values[i])
                {
                    if (i < values.Count - 1 && missCount == values[i + 1])
                    {
                        // if there are duplicates, take the lowest SR that can achieve miss count
                        continue;
                    }

                    return missStarRating(sr, i);
                }

                if (i < values.Count - 1 && missCount < values[i + 1])
                {
                    t = (missCount - values[i]) / (values[i + 1] - values[i]);
                    return missStarRating(sr, i) * (1 - t) + missStarRating(sr, i + 1) * t;
                }
            }

            // more misses than max evaluated, interpolate to zero
            t = (missCount - values.Last()) / (beatmapMaxCombo - values.Last());
            return missStarRating(sr, values.Count - 1) * (1 - t);
        }

        private double computeAimSnapValue(Dictionary<string, double> categoryRatings = null)
        {
            double aimSnapComboStarRating = interpComboStarRating(Attributes.AimSnapComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double aimSnapMissCountStarRating = interpMissCountStarRating(Attributes.AimSnapComboStarRatings.Last(), Attributes.AimSnapMissCounts, countMiss);
            double rawAimSnap = Math.Pow(aimSnapComboStarRating, combo_weight) * Math.Pow(aimSnapMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawAimSnap = Math.Pow(rawAimSnap, 0.8);

            double aimSnapValue = Math.Pow(5.0f * Math.Max(1.0f, rawAimSnap / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            double approachRateFactor = 1.0f;

            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);
            else if (Attributes.ApproachRate < 9.0f)
                approachRateFactor += 0.2f * (9.0f - Attributes.ApproachRate);

            aimSnapValue *= approachRateFactor;

            // Scale the jump aim value with accuracy
            double accScale = (1.0f + 3.0f * accuracy) / 4.0f;
            double ODScale = 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;
            aimSnapValue *= accScale * ODScale;

            return aimSnapValue;
        }

        private double computeAimFlowValue(Dictionary<string, double> categoryRatings = null)
        {
            double aimFlowComboStarRating = interpComboStarRating(Attributes.AimFlowComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double aimFlowMissCountStarRating = interpMissCountStarRating(Attributes.AimFlowComboStarRatings.Last(), Attributes.AimFlowMissCounts, countMiss);
            double rawAimFlow = Math.Pow(aimFlowComboStarRating, combo_weight) * Math.Pow(aimFlowMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawAimFlow = Math.Pow(rawAimFlow, 1.25f);

            double aimFlowValue = Math.Pow(5.0f * Math.Max(1.0f, rawAimFlow / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.1f * (Attributes.ApproachRate - 10.33f);
      //      else if (Attributes.ApproachRate < 9.0f)
      //          approachRateFactor += 0.05f * (9.0f - Attributes.ApproachRate);

            aimFlowValue *= approachRateFactor;

            // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
            if (mods.Any(m => m is OsuModHidden))
                aimFlowValue *= 1.0f + 0.1f * (12.0f - Attributes.ApproachRate);

            // Scale the stream aim value with accuracy
            double accScale = (1.0f + 3.0f * accuracy) / 4.0f;
            double ODScale = 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;
            aimFlowValue *= accScale * ODScale;

            return aimFlowValue;
        }

        private double computeAimHybridValue(Dictionary<string, double> categoryRatings = null)
        {
            double aimHybridComboStarRating = interpComboStarRating(Attributes.AimHybridComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double aimHybridMissCountStarRating = interpMissCountStarRating(Attributes.AimHybridComboStarRatings.Last(), Attributes.AimHybridMissCounts, countMiss);
            double rawAimHybrid = Math.Pow(aimHybridComboStarRating, combo_weight) * Math.Pow(aimHybridMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawAimHybrid = Math.Pow(rawAimHybrid, 0.75f);

            double aimHybridValue = Math.Pow(5.0f * Math.Max(1.0f, rawAimHybrid / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.2f * (Attributes.ApproachRate - 10.33f);
            else if (Attributes.ApproachRate < 9.0f)
                approachRateFactor += 0.15f * (9.0f - Attributes.ApproachRate);

            aimHybridValue *= approachRateFactor;

            // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
            if (mods.Any(m => m is OsuModHidden))
                aimHybridValue *= 1.0f + 0.05f * (12.0f - Attributes.ApproachRate);

            // Scale the control aim value with accuracy
            double accScale = (1.0f + 3.0f * accuracy) / 4.0f;
            double ODScale = 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;
            aimHybridValue *= accScale * ODScale;

            return aimHybridValue;
        }

        private double computeTapStaminaValue(Dictionary<string, double> categoryRatings = null)
        {
            double tapTapStaminaComboStarRating = interpComboStarRating(Attributes.TapStaminaComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double tapTapStaminaMissCountStarRating = interpMissCountStarRating(Attributes.TapStaminaComboStarRatings.Last(), Attributes.TapStaminaMissCounts, countMiss);
            double rawTapStamina = Math.Pow(tapTapStaminaComboStarRating, combo_weight) * Math.Pow(tapTapStaminaMissCountStarRating, 1 - combo_weight);
            double tapTapStaminaValue = Math.Pow(5.0f * Math.Max(1.0f, rawTapStamina / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Scale the tapTapStamina value with accuracy
            double accScale = 0.5f + Math.Pow(Math.Sin(2.5f * Math.PI * (accuracy - 0.8f)), 2.0f) / 2.0f;
            double ODScale = 0.5f + Math.Pow(Attributes.OverallDifficulty, 2) / 150;
            tapTapStaminaValue *= 0.1f + accScale * ODScale;

            return tapTapStaminaValue;
        }

        private double computeTapSpeedValue(Dictionary<string, double> categoryRatings = null)
        {
            double tapSpeedComboStarRating = interpComboStarRating(Attributes.TapSpeedComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double tapSpeedMissCountStarRating = interpMissCountStarRating(Attributes.TapSpeedComboStarRatings.Last(), Attributes.TapSpeedMissCounts, countMiss);
            double rawTapSpeed = Math.Pow(tapSpeedComboStarRating, combo_weight) * Math.Pow(tapSpeedMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawTapSpeed = Math.Pow(rawTapSpeed, 1.25f);

            double tapSpeedValue = Math.Pow(5.0f * Math.Max(1.0f, rawTapSpeed / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Scale the tapSpeed value with accuracy
            double accScale = 0.5f + Math.Pow(Math.Sin(2.5f * Math.PI * (accuracy - 0.8f)), 2.0f) / 2.0f;
            double ODScale = 0.5f + Math.Pow(Attributes.OverallDifficulty, 2) / 150;
            tapSpeedValue *= 0.1f + accScale * ODScale;

            return tapSpeedValue;
        }

        private double computeTapRhythmValue(Dictionary<string, double> categoryRatings = null)
        {
            double tapRhythmComboStarRating = interpComboStarRating(Attributes.TapRhythmComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double tapRhythmMissCountStarRating = interpMissCountStarRating(Attributes.TapRhythmComboStarRatings.Last(), Attributes.TapRhythmMissCounts, countMiss);
            double rawTapRhythm = Math.Pow(tapRhythmComboStarRating, combo_weight) * Math.Pow(tapRhythmMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawTapRhythm = Math.Pow(rawTapRhythm, 1.25f);

            double tapRhythmValue = Math.Pow(5.0f * Math.Max(1.0f, rawTapRhythm / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);

            tapRhythmValue *= approachRateFactor;

            // Scale the finger control value with accuracy
            double accScale = 0.5f + Math.Pow(Math.Sin(2.5f * Math.PI * (accuracy - 0.8f)), 2.0f) / 2.0f;
            double ODScale = 0.5f + Math.Pow(Attributes.OverallDifficulty, 2) / 150;
            tapRhythmValue *= 0.1f + accScale * ODScale;

            return tapRhythmValue;
        }

        private double computeAccuracyValue(Dictionary<string, double> categoryRatings = null)
        {
            double sigmaCircle = 0;
            double sigmaSlider = 0;
            double sigmaTotal = 0;

            double zScore = 2.58f;
            double sqrt2 = Math.Sqrt(2.0f);
            double accMultiplier = 1200.0f;
            double accScale = 1.3f;

            // Slider sigma calculations
            if (countSliders > 0)
            {
                double sliderConst = Math.Sqrt(2.0f / countSliders) * zScore;
                double sliderProbability = (2.0f * accuracy + Math.Pow(sliderConst, 2.0f) - sliderConst * Math.Sqrt(4.0f * accuracy + Math.Pow(sliderConst, 2.0f) - 4.0f * Math.Pow(accuracy, 2.0f))) / (2.0f + 2.0f * Math.Pow(sliderConst, 2.0f));
                sigmaSlider = (199.5f - 10.0f * Attributes.OverallDifficulty) / (sqrt2 * SpecialFunctions.ErfInv(sliderProbability));
            }

            // Circle sigma calculations
            if (countHitCircles > 0)
            {
                double circleConst = Math.Sqrt(2.0f / countHitCircles) * zScore;
                double circleProbability = (2.0f * accuracy + Math.Pow(circleConst, 2.0f) - circleConst * Math.Sqrt(4.0f * accuracy + Math.Pow(circleConst, 2.0f) - 4.0f * Math.Pow(accuracy, 2.0f))) / (2.0f + 2.0f * Math.Pow(circleConst, 2.0f));
                sigmaCircle = (79.5f - 6.0f * Attributes.OverallDifficulty) / (sqrt2 * SpecialFunctions.ErfInv(circleProbability));
            }

            if (sigmaSlider == 0) return accMultiplier * Math.Pow(accScale, -sigmaCircle);
            if (sigmaCircle == 0) return accMultiplier * Math.Pow(accScale, -sigmaSlider);

            sigmaTotal = 1.0f / (1.0f / sigmaCircle + 1.0f / sigmaSlider);

            double accValue = accMultiplier * Math.Pow(accScale, -sigmaTotal);

            if (mods.Any(m => m is OsuModHidden))
                accValue *= 1.1f;

            return accValue;
        }

        private double totalHits => countGreat + countGood + countMeh + countMiss;
        private double totalSuccessfulHits => countGreat + countGood + countMeh;
    }
}
