using System;
using System.Collections.Generic;
using static ProjectionSemiMarkov.HelperFunctions;


namespace ProjectionSemiMarkov
{
  public abstract class Calculator
  {
    /// <summary>
    /// The used step size. We validate the <see cref="Policy.age"/>, <see cref="Policy.initialDuration"/> and
    /// <see cref="Policy.initialTime"/> are at form stepSize * n for some integer n.
    /// </summary>
    protected readonly double stepSize = 1.0 / 12.0;

    /// <summary>
    /// The state space. It is a subset of <see cref="State"/>.
    /// </summary>
    protected IEnumerable<State> stateSpace;

    /// <summary>
    /// A dictionary containing the intensities.
    /// </summary>
    protected Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>> intensities;

    /// <summary>
    /// A mapping from policy id to policy.
    /// </summary>
    protected Dictionary<string, Policy> policies;

    /// <summary>
    /// Calculate number of Time points for a policy.
    /// </summary>
    public int GetNumberOfTimePoints(Policy policy, double time)
    {
      // We assume the last Time and ages are at the form stepSize * n for some n.
      // We are adding one, to allocate for a Time 0.
      var value = (policy.expiryAge - policy.age - time) / stepSize + 1;
      if (!doubleIsInteger(value))
        throw new ArgumentException("Either expiry age or age is not a multiply of step size", policy.policyId);
      if (value <= 0)
        value = 0;

      return Convert.ToInt32(value);
    }

    /// <summary>
    /// Calculate the last duration index for a given timeIndex.
    /// </summary>
    public int DurationSupportIndex(double initialDuration, int timeIndex)
    {
      return (int)(initialDuration / stepSize + timeIndex);
    }

    protected double IndexToTime(double x)
    {
      return x * stepSize;
    }
  }
}
