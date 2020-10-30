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

        private readonly int countHitCircles;
        private readonly int countSliders;
        private readonly int beatmapMaxCombo;

        private Mod[] mods;

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countGood;
        private int countMeh;
        private int countMiss;
        private const double combo_weight = 0.5;
        private const double aim_pp_factor = 1.5f;
        private const double speed_pp_factor = 1.5f;
        private const double total_factor = 1.1f;

        public OsuPerformanceCalculator(Ruleset ruleset, WorkingBeatmap beatmap, ScoreInfo score)
            : base(ruleset, beatmap, score)
        {
            countHitCircles = Beatmap.HitObjects.Count(h => h is HitCircle);
            countSliders = Beatmap.HitObjects.Count(h => h is Slider);

            beatmapMaxCombo = Beatmap.HitObjects.Count;
            // Add the ticks + tail of the slider. 1 is subtracted because the "headcircle" would be counted twice (once for the slider itself in the line above)
            beatmapMaxCombo += Beatmap.HitObjects.OfType<Slider>().Sum(s => s.NestedHitObjects.Count - 1);
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

            // Custom multipliers for NoFail and SpunOut.
            double aim_multiplier = 1.07f;
            double speed_multiplier = 1.0f;
            double total_multiplier = 1.2f; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things

            if (mods.Any(m => m is OsuModNoFail))
                total_multiplier *= 0.90f;

            if (mods.Any(m => m is OsuModSpunOut))
                total_multiplier *= 0.95f;

            double jumpAimValue = computeJumpAimValue(categoryRatings);
            double streamAimValue = computeStreamAimValue(categoryRatings);
            double staminaValue = computeStaminaValue(categoryRatings);
            double speedValue = computeSpeedValue(categoryRatings);
            double aimControlValue = computeAimControlValue(categoryRatings);
            double fingerControlValue = computeFingerControlValue(categoryRatings);
            double accuracyValue = computeAccuracyValue(categoryRatings);

            double totalAimValue = aim_multiplier * Math.Pow(
                Math.Pow(jumpAimValue, aim_pp_factor) +
                Math.Pow(streamAimValue, aim_pp_factor) +
                Math.Pow(aimControlValue, aim_pp_factor), 1.0f / aim_pp_factor);
            double totalSpeedValue = speed_multiplier * Math.Pow(
                Math.Pow(staminaValue, speed_pp_factor) +
                Math.Pow(speedValue, speed_pp_factor) +
                Math.Pow(fingerControlValue, speed_pp_factor), 1.0f / speed_pp_factor);

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

            double totalValue = total_multiplier * Math.Pow(
                Math.Pow(totalAimValue, total_factor) +
                Math.Pow(totalSpeedValue, total_factor) +
                Math.Pow(accuracyValue, total_factor), 1.0f / total_factor);

            if (categoryRatings != null)
            {
                categoryRatings.Add("Total Star Rating", 0.0675f * (Math.Pow(300000.0f * totalValue, 1.0f / 3.0f) + 4.0f) / 5.0f);
                categoryRatings.Add("Jump Aim", jumpAimValue);
                categoryRatings.Add("Stream Aim", streamAimValue);
                categoryRatings.Add("Stamina", staminaValue);
                categoryRatings.Add("Speed", speedValue);
                categoryRatings.Add("Aim Control", aimControlValue);
                categoryRatings.Add("Finger Control", fingerControlValue);
                categoryRatings.Add("Total Aim", totalAimValue);
                categoryRatings.Add("Total Speed", totalSpeedValue);
                categoryRatings.Add("Accuracy", accuracyValue);
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

        private double computeJumpAimValue(Dictionary<string, double> categoryRatings = null)
        {
            double jumpAimComboStarRating = interpComboStarRating(Attributes.JumpAimComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double jumpAimMissCountStarRating = interpMissCountStarRating(Attributes.JumpAimComboStarRatings.Last(), Attributes.JumpAimMissCounts, countMiss);
            double rawJumpAim = Math.Pow(jumpAimComboStarRating, combo_weight) * Math.Pow(jumpAimMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawJumpAim = Math.Pow(rawJumpAim, 0.8);

            double jumpAimValue = Math.Pow(5.0f * Math.Max(1.0f, rawJumpAim / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            double approachRateFactor = 1.0f;

            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);
            else if (Attributes.ApproachRate < 9.0f)
                approachRateFactor += 0.1f * (9.0f - Attributes.ApproachRate);

            jumpAimValue *= approachRateFactor;

            // Scale the jump aim value with accuracy
            double accScale = (1.0f + 3.0f * accuracy) / 4.0f;
            double ODScale = 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;
            jumpAimValue *= accScale * ODScale;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Jump Aim Combo Stars", jumpAimComboStarRating);
                categoryRatings.Add("Jump Aim True Stars", Attributes.JumpAimStrain);
            }

            return jumpAimValue;
        }

        private double computeStreamAimValue(Dictionary<string, double> categoryRatings = null)
        {
            double streamAimComboStarRating = interpComboStarRating(Attributes.StreamAimComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double streamAimMissCountStarRating = interpMissCountStarRating(Attributes.StreamAimComboStarRatings.Last(), Attributes.StreamAimMissCounts, countMiss);
            double rawStreamAim = Math.Pow(streamAimComboStarRating, combo_weight) * Math.Pow(streamAimMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawStreamAim = Math.Pow(rawStreamAim, 1.25f);

            double streamAimValue = Math.Pow(5.0f * Math.Max(1.0f, rawStreamAim / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);
            else if (Attributes.ApproachRate < 9.0f)
                approachRateFactor += 0.1f * (9.0f - Attributes.ApproachRate);

            streamAimValue *= approachRateFactor;

            // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
            if (mods.Any(m => m is OsuModHidden))
                streamAimValue *= 1.0f + 0.15f * (12.0f - Attributes.ApproachRate);

            // Scale the stream aim value with accuracy
            double accScale = (1.0f + 3.0f * accuracy) / 4.0f;
            double ODScale = 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;
            streamAimValue *= accScale * ODScale;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Stream Aim Combo Stars", streamAimComboStarRating);
                categoryRatings.Add("Stream Aim True Stars", Attributes.StreamAimStrain);
            }

            return streamAimValue;
        }

        private double computeAimControlValue(Dictionary<string, double> categoryRatings = null)
        {
            double aimControlComboStarRating = interpComboStarRating(Attributes.AimControlComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double aimControlMissCountStarRating = interpMissCountStarRating(Attributes.AimControlComboStarRatings.Last(), Attributes.AimControlMissCounts, countMiss);
            double rawAimControl = Math.Pow(aimControlComboStarRating, combo_weight) * Math.Pow(aimControlMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawAimControl = Math.Pow(rawAimControl, 0.75f);

            double aimControlValue = Math.Pow(5.0f * Math.Max(1.0f, rawAimControl / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);
            else if (Attributes.ApproachRate < 9.0f)
                approachRateFactor += 0.1f * (9.0f - Attributes.ApproachRate);

            aimControlValue *= approachRateFactor;

            // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
            if (mods.Any(m => m is OsuModHidden))
                aimControlValue *= 1.0f + 0.05f * (12.0f - Attributes.ApproachRate);

            // Scale the control aim value with accuracy
            double accScale = (1.0f + 3.0f * accuracy) / 4.0f;
            double ODScale = 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;
            aimControlValue *= accScale * ODScale;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Aim Control Combo Stars", aimControlComboStarRating);
                categoryRatings.Add("Aim Control True Stars", Attributes.AimControlStrain);
            }

            return aimControlValue;
        }

        private double computeStaminaValue(Dictionary<string, double> categoryRatings = null)
        {
            double staminaComboStarRating = interpComboStarRating(Attributes.StaminaComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double staminaMissCountStarRating = interpMissCountStarRating(Attributes.StaminaComboStarRatings.Last(), Attributes.StaminaMissCounts, countMiss);
            double rawStamina = Math.Pow(staminaComboStarRating, combo_weight) * Math.Pow(staminaMissCountStarRating, 1 - combo_weight);
            double staminaValue = Math.Pow(5.0f * Math.Max(1.0f, rawStamina / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Scale the stamina value with accuracy
            double accScale = 0.5f + Math.Pow(Math.Sin(2.5f * Math.PI * (accuracy - 0.8f)), 2.0f) / 2.0f;
            double ODScale = 0.5f + Math.Pow(Attributes.OverallDifficulty, 2) / 150;
            staminaValue *= 0.1f + accScale * ODScale;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Stamina Combo Stars", staminaComboStarRating);
                categoryRatings.Add("Stamina True Stars", Attributes.StaminaStrain);
            }

            return staminaValue;
        }

        private double computeSpeedValue(Dictionary<string, double> categoryRatings = null)
        {
            double speedComboStarRating = interpComboStarRating(Attributes.SpeedComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double speedMissCountStarRating = interpMissCountStarRating(Attributes.SpeedComboStarRatings.Last(), Attributes.SpeedMissCounts, countMiss);
            double rawSpeed = Math.Pow(speedComboStarRating, combo_weight) * Math.Pow(speedMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawSpeed = Math.Pow(rawSpeed, 1.25f);

            double speedValue = Math.Pow(5.0f * Math.Max(1.0f, rawSpeed / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Scale the speed value with accuracy
            double accScale = 0.5f + Math.Pow(Math.Sin(2.5f * Math.PI * (accuracy - 0.8f)), 2.0f) / 2.0f;
            double ODScale = 0.5f + Math.Pow(Attributes.OverallDifficulty, 2) / 150;
            speedValue *= 0.1f + accScale * ODScale;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Speed Combo Stars", speedComboStarRating);
                categoryRatings.Add("Speed True Stars", Attributes.SpeedStrain);
            }

            return speedValue;
        }

        private double computeFingerControlValue(Dictionary<string, double> categoryRatings = null)
        {
            double fingerControlComboStarRating = interpComboStarRating(Attributes.FingerControlComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double fingerControlMissCountStarRating = interpMissCountStarRating(Attributes.FingerControlComboStarRatings.Last(), Attributes.FingerControlMissCounts, countMiss);
            double rawFingerControl = Math.Pow(fingerControlComboStarRating, combo_weight) * Math.Pow(fingerControlMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawFingerControl = Math.Pow(rawFingerControl, 1.25f);

            double fingerControlValue = Math.Pow(5.0f * Math.Max(1.0f, rawFingerControl / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);

            fingerControlValue *= approachRateFactor;

            // Scale the finger control value with accuracy
            double accScale = 0.5f + Math.Pow(Math.Sin(2.5f * Math.PI * (accuracy - 0.8f)), 2.0f) / 2.0f;
            double ODScale = 0.5f + Math.Pow(Attributes.OverallDifficulty, 2) / 150;
            fingerControlValue *= 0.1f + accScale * ODScale;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Finger Control Combo Stars", fingerControlComboStarRating);
                categoryRatings.Add("Finger Control True Stars", Attributes.FingerControlStrain);
            }

            return fingerControlValue;
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
