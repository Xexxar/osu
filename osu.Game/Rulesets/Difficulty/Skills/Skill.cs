﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Difficulty.Skills
{
    /// <summary>
    /// A bare minimal abstract skill for fully custom skill implementations.
    /// </summary>
    public abstract class Skill
    {
        public double CurrentStrain;

        /// <summary>
        /// <see cref="DifficultyHitObject"/>s that were processed previously. They can affect the difficulty values of the following objects.
        /// </summary>
        protected readonly ReverseQueue<DifficultyHitObject> Previous;

        /// <summary>
        /// Soft capacity of the <see cref="Previous"/> queue.
        /// <see cref="Previous"/> will automatically resize if it exceeds capacity, but will do so at a very slight performance impact.
        /// The actual capacity will be set to this value + 1 to allow for storage of the current object before the next can be processed.
        /// </summary>
        protected virtual int PreviousCollectionSoftCapacity => 0;

        /// <summary>
        /// Visual mods for use in skill calculations.
        /// </summary>
        protected IReadOnlyList<Mod> Mods => mods;

        private readonly Mod[] mods;

        protected Skill(Mod[] mods)
        {
            this.mods = mods;
            Previous = new ReverseQueue<DifficultyHitObject>(PreviousCollectionSoftCapacity + 1);
        }

        /// <summary>
        /// Process a <see cref="DifficultyHitObject"/>.
        /// </summary>
        /// <param name="current">The <see cref="DifficultyHitObject"/> to process.</param>
        public void Process(DifficultyHitObject current)
        {
            // Preprocessing
            RemoveExtraneousHistory(current);

            // Processing
            Calculate(current);

            // Postprocessing
            AddToHistory(current);
        }

        /// <summary>
        /// Remove objects from <see cref="Previous"/> that are no longer needed for calculations from the current object onwards.
        /// </summary>
        /// <param name="current">The <see cref="DifficultyHitObject"/> to be processed.</param>
        protected virtual void RemoveExtraneousHistory(DifficultyHitObject current)
        {
        }

        /// <summary>
        /// Add the current <see cref="DifficultyHitObject"/> to the <see cref="Previous"/> queue (if required).
        /// </summary>
        /// <param name="current">The <see cref="DifficultyHitObject"/> that was just processed.</param>
        protected virtual void AddToHistory(DifficultyHitObject current)
        {
        }

        /// <summary>
        /// Calculate the difficulty of a <see cref="DifficultyHitObject"/> and update current strain values accordingly.
        /// </summary>
        /// <param name="current">The <see cref="DifficultyHitObject"/> to calculate the difficulty of.</param>
        protected abstract void Calculate(DifficultyHitObject current);

        /// <summary>
        /// Returns the calculated difficulty value representing all <see cref="DifficultyHitObject"/>s that have been processed up to this point.
        /// </summary>
        public abstract double DifficultyValue();
    }
}
