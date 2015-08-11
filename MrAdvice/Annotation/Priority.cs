﻿#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion
namespace ArxOne.MrAdvice.Annotation
{
    using System;
    using System.Linq;
    using Advice;

    /// <summary>
    /// Marks an advice with priority.
    /// Advices are sorted from higher to lower (higher are processed first, lower last)
    /// If no priority is specified, the value is taken from DefaulLevel
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class Priority : Attribute
    {
        /// <summary>
        /// The default level (when Priority is not specified on the advice)
        /// </summary>
        public const int DefaultLevel = 0;

        /// <summary>
        /// Marks a property as abstract:
        /// removes backing field and actual get/set
        /// </summary>
        public const int Abstract = PriorityValue.Abstract;

        /// <summary>
        /// Gets or sets the level.
        /// </summary>
        /// <value>
        /// The level.
        /// </value>
        public int Level { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Priority"/> class.
        /// Assigns a priority
        /// </summary>
        /// <param name="level">The level.</param>
        public Priority(int level)
        {
            Level = level;
        }

        /// <summary>
        /// Gets priority level from the specified advice.
        /// </summary>
        /// <param name="advice">The advice.</param>
        /// <returns></returns>
        public static int GetLevel(IAdvice advice)
        {
            var priorityAttribute = advice.GetType().GetCustomAttributes(typeof(Priority), true).Cast<Priority>().SingleOrDefault();
            if (priorityAttribute != null)
                return priorityAttribute.Level;
            return DefaultLevel;
        }
    }
}
