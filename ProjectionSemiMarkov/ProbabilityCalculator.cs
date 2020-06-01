using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectionSemiMarkov
{
  public class ProbabilityCalculator : Calculator
  {
    /// <summary>
    /// A dictionary indexed on <see cref="Policy.policyId"/> and contains the rho-modified semi-markov Probabilities.
    /// </summary>
    /// <remarks>
    /// Given a <c>policyId</c>, a <c>state</c>, a <c>timePoint</c>, a <c>duration</c>, then
    /// Probabilities[policyId][state][timePoint][duration] is the probability
    /// p_{z0,state}(initialTime, (timePoint - 1)*stepSize, initialDuration, (duration - 1)*stepSize)
    /// The z0, initialTime and initialDuration is found on the policy, see mapping <see cref="policies"/>.
    /// </remarks>
    public Dictionary<string, Dictionary<State, double[][]>> Probabilities { get; private set; }

    public Dictionary<string, Dictionary<State, double[][]>> RhoProbabilities { get; private set; }

    public Dictionary<string, (State, double)> PolicyIdInitialStateDuration { get; private set; }

    public double Time { get; private set; }

    public Dictionary<string, double[]> FreePolicyFactor { get; private set; }

    /// <summary>
    /// Constructing ProbabilityCalculator.
    /// </summary>
    public ProbabilityCalculator(
      Dictionary<string, (State, double)> policyIdInitialStateDuration,
      double time,
      Dictionary<string, double[]> freePolicyFactor)
    {
      // Deducing the state space from the possible transitions in intensity dictionary
      var allPossibleTransitions = marketIntensities[Gender.Female].Union(marketIntensities[Gender.Male]).ToList();
      stateSpace = allPossibleTransitions.SelectMany(x => x.Value.Keys)
      .Union(allPossibleTransitions.Select(y => y.Key)).Distinct();

      this.PolicyIdInitialStateDuration = policyIdInitialStateDuration;
      this.Time = time;
      this.FreePolicyFactor = freePolicyFactor;
    }

    /// <summary>
    /// Allocating memory for matrices inside <see cref="Probabilities"/>.
    /// </summary>
    private void AllocateMemoryAndInitialize(bool calculateRhoProbability)
    {
      Probabilities = new Dictionary<string, Dictionary<State, double[][]>>();
      AllocateMemoryAndInitializePerDictionary(Probabilities, false);

      if (calculateRhoProbability)
      {
        RhoProbabilities = new Dictionary<string, Dictionary<State, double[][]>>();
        AllocateMemoryAndInitializePerDictionary(RhoProbabilities, true);
      }
    }

    /// <summary>
    /// Allocating memory for matrices inside <see cref="dic"/>.
    /// </summary>
    private void AllocateMemoryAndInitializePerDictionary(
      Dictionary<string, Dictionary<State, double[][]>> dic,
      bool calculateRhoProbability)
    {
      var statesWhereRhoModifiedIsValid = new List<State> { State.Active, State.Disabled };

      foreach (var (policyId, v) in policies)
      {
        var (initialState, initialDuration) = PolicyIdInitialStateDuration[policyId];

        if (!statesWhereRhoModifiedIsValid.Contains(initialState) && calculateRhoProbability)
          throw new Exception($"Rho-Modified transition do not make sense in {initialState}");

        var numberOfTimePoints = GetNumberOfTimePoints(v, Time);
        var stateProbabilities = new Dictionary<State, double[][]>();

        var statesToCreateEntryFor = calculateRhoProbability
          ? new List<State>{State.FreePolicyActive, State.FreePolicyDead, State.FreePolicyDisabled, State.FreePolicySurrender}
          : stateSpace;

        foreach (var state in statesToCreateEntryFor)
        {
          var arrayOfArray = new double[numberOfTimePoints][];

          for (var timeIndex = 0; timeIndex < numberOfTimePoints; timeIndex++)
            arrayOfArray[timeIndex] =
              new double[DurationSupportIndex(initialDuration, timeIndex) + 1];

          // Default values of array elements are zero, so we only set probability for last duration to one.
          var boundary = calculateRhoProbability ? 0.0 : 1;
          arrayOfArray[0][arrayOfArray[0].Length - 1] = state == initialState ? boundary : 0;

          stateProbabilities.Add(state, arrayOfArray);
        }
        dic.Add(policyId, stateProbabilities);
      }
    }

    public Dictionary<string, Dictionary<State, double[][]>> Calculate(bool calculateRhoProbability)
    {
      AllocateMemoryAndInitialize(calculateRhoProbability);
      Parallel.ForEach(policies, policy => ProbabilityCalculatePerPolicy(policy.Value, calculateRhoProbability));

      return(Probabilities);
    }

    private void ProbabilityCalculatePerPolicy(Policy policy, bool calculateRhoProbability)
    {
      ProbabilityCalculatePerPolicy(policy, Probabilities[policy.policyId], false);

      if (calculateRhoProbability)
        ProbabilityCalculatePerPolicy(policy, RhoProbabilities[policy.policyId], true);
    }

    /// <remarks>
    /// Approximation of lebesgue integral
    /// int_0^t f(s) p(.., ds)
    /// \approx sum_i P( s_i < Z < s_(i+1)) f((s_(i+1)-s_i)/2)
    /// = sum_i (p(.., s_(i+1)) - p(.., s_i)) * f((s_(i+1)-s_i)/2)
    /// </remarks>
    private void ProbabilityCalculatePerPolicy(
      Policy policy,
      IReadOnlyDictionary<State, double[][]> prob,
      bool calculateRhoProbability)
    {
      var numberOfTimePoints = prob.First().Value.Length;
      var genderIntensity = marketIntensities[policy.gender];
      var (initialState, initialDuration) = PolicyIdInitialStateDuration[policy.policyId];

      var states = calculateRhoProbability
        ? new List<State> { State.FreePolicyActive, State.FreePolicyDead, State.FreePolicyDisabled, State.FreePolicySurrender }
        : stateSpace.ToList();
      var possibleMidStates = genderIntensity.Keys.Where(x => states.Contains(x));

      // Loop over each Time point
      for (var t = 1; t < numberOfTimePoints; t++)
      {
        var durationMaxIndexCur = DurationSupportIndex(initialDuration, t);
        var durationMaxIndexPrev = DurationSupportIndex(initialDuration, t - 1);

        var probIntegrals = new double[durationMaxIndexCur + 1];
        var sumRho = 0.0;

        // Loop over j in p_{z0,j}(...)
        foreach (var j in states)
        {
          // Handling of j = FreePolicyActive in Rho-Modified probabilities
          if (j == State.FreePolicyActive && calculateRhoProbability)
          {
            // Using a DurationSupportIndex, when the function name makes no sense here. The return value is correct one.
            for (var u = 1; u <= durationMaxIndexPrev; u++)
            {
              sumRho += genderIntensity[State.Active][State.FreePolicyActive](policy.age + Time + IndexToTime(t - 0.5), initialDuration + IndexToTime(u - 0.5))
                * (Probabilities[policy.policyId][initialState][t][u] - Probabilities[policy.policyId][initialState][t][u - 1]);
            }

            var midPointFreePolicyFactor = (FreePolicyFactor[policy.policyId][DurationSupportIndex(Time, t)]
              + FreePolicyFactor[policy.policyId][DurationSupportIndex(Time, t - 1)]) / 2;
            sumRho *= stepSize * midPointFreePolicyFactor;
          }

          // Loop over l in Kolmogorov forward integro-differential equations (Prob. mass going in)
          foreach (var l in possibleMidStates)
          {
            Func<double, double, double> intensity;
            if (!genderIntensity[l].TryGetValue(j, out intensity))
              continue;

            probIntegrals[1] = 0;
            // Riemann sum over duration for integrals
            for (var u = 1; u <= durationMaxIndexPrev; u++)
            {
              probIntegrals[u + 1] = probIntegrals[u] + (prob[l][t - 1][u] - prob[l][t - 1][u - 1])
                * genderIntensity[l][j](policy.age + Time + IndexToTime(t - 0.5),
                  initialDuration + IndexToTime(u - 0.5)) * stepSize;

              //todo: Should probably not have age in policy. Could consider splitting age and Time to allow for more general intesities.
            }

            // For j = l, we need to subtract the cumulative integral for each duration
            // For j \neq l, we need to add the whole integral from 0 to D(s) for each duration
            if (j == l)
            {
              for (var u = 1; u <= durationMaxIndexCur; u++)
                prob[j][t][u] = prob[j][t][u] - probIntegrals[u];
            }
            else
            {
              for (var u = 1; u <= durationMaxIndexCur; u++)
                prob[j][t][u] = prob[j][t][u] + probIntegrals.Last();
            }
          }

          // We add the previous probability p_ij(t_0,s,u,d+s-t_0) and (1_(j = J + 1) int ... ) to get
          // p_ij(t_0,s+h,u,d+s+h-t_0)= p_ij(t_0,s,u,d+s-t_0) + d/ds p_ij(t_0,s,u,d+s-t_0) * h
          for (var u = 1; u <= durationMaxIndexCur; u++)
            prob[j][t][u] = prob[j][t][u] + sumRho + prob[j][t - 1][u - 1];
        }
      }
    }
  }
}