using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectionSemiMarkov
{
  public class ProbabilityQCalculator : Calculator
  {
    public Dictionary<string, Dictionary<State, double[][]>> QProbabilities { get; private set; }
    public Dictionary<string, (State, double)> PolicyIdInitialStateDuration { get; private set; }
    public double Time { get; private set; }
    public Dictionary<double, double> rStar { get; private set; }
    public Dictionary<double, double> r { get; private set; }
    public double pi1 { get; private set; }
    public double pi2 { get; private set; }
    Dictionary<string, Dictionary<State, double[][]>> Probabilities { get; }
    Dictionary<string, Dictionary<State, double[]>> TechnicalReserves { get; }

    /// <summary>
    /// Constructing ProbabilityCalculator.
    /// </summary>
    public ProbabilityQCalculator(
      Dictionary<string, (State, double)> policyIdInitialStateDuration,
      double time,
      Dictionary<string, Dictionary<State, double[][]>> probabilities,
      Dictionary<string, Dictionary<State, double[]>> technicalReserves
      )
    {
      // Deducing the state space from the possible transitions in intensity dictionary
      var allPossibleTransitions = marketIntensities[Gender.Female].Union(marketIntensities[Gender.Male]).ToList();
      stateSpace = allPossibleTransitions.SelectMany(x => x.Value.Keys)
      .Union(allPossibleTransitions.Select(y => y.Key)).Distinct();

      this.PolicyIdInitialStateDuration = policyIdInitialStateDuration;
      this.Time = time;

      //todo - need r and r^* as input, as well as pi_1 and pi_2
      this.rStar = rStar;
      this.r = r;
      this.pi1 = pi1;
      this.pi2 = pi2;

      this.Probabilities = probabilities;
      this.TechnicalReserves = technicalReserves;
    }

    /// <summary>
    /// Allocating memory for matrices inside <see cref="Probabilities"/>.
    /// </summary>
    private void AllocateMemoryAndInitialize()
    {
      QProbabilities = new Dictionary<string, Dictionary<State, double[][]>>();

      foreach (var (policyId, v) in policies)
      {
        var (initialState, initialDuration) = PolicyIdInitialStateDuration[policyId];
        var numberOfTimePoints = GetNumberOfTimePoints(v, Time);
        var stateProbabilities = new Dictionary<State, double[][]>();

        foreach (var state in stateSpace)
        {
          var arrayOfArray = new double[numberOfTimePoints][];

          for (var timeIndex = 0; timeIndex < numberOfTimePoints; timeIndex++)
            arrayOfArray[timeIndex] =
              new double[DurationSupportIndex(initialDuration, timeIndex) + 1];

          // Default values of array elements are zero, so we only set probability for last duration to one.
          arrayOfArray[0][arrayOfArray[0].Length - 1] = state == initialState ? 1 : 0;

          stateProbabilities.Add(state, arrayOfArray);
        }
        Probabilities.Add(policyId, stateProbabilities);
      }
    }

    public Dictionary<string, Dictionary<State, double[][]>> Calculate()
    {
      AllocateMemoryAndInitialize();

      var Dividends = new Dictionary<string, Dictionary<State, double[][]>>();

      Parallel.ForEach(policies, policy => ProbabilityQCalculatePerPolicy(policy.Value));

      return (Probabilities);
    }

    public void ProbabilityQCalculatePerPolicy(Policy policy)
    {
      var policyProbabilities = Probabilities[policy.policyId];
      var policyQProbabilities = Probabilities[policy.policyId];
      var numberOfTimePoints = policyProbabilities.First().Value.Length;
      var genderIntensity = marketIntensities[policy.gender];

      // Loop over each Time point
      for (var t = 1; t < numberOfTimePoints; t++)
      {
        var durationMaxIndexCur = DurationSupportIndex(policy.initialDuration, t);
        var durationMaxIndexPrev = DurationSupportIndex(policy.initialDuration, t - 1);

        var probIntegrals = new double[durationMaxIndexCur + 1];

        // Loop over j in p_{z0,j}(...)
        foreach (var j in stateSpace)
        {
          // Loop over l in Kolmogorov forward integro-differential equations (Prob. mass going in)
          foreach (var l in genderIntensity.Keys)
          {
            Func<double, double, double> intensity;
            if (!genderIntensity[l].TryGetValue(j, out intensity))
              continue;

            probIntegrals[1] = 0;
            // Riemann sum over duration for integrals
            for (var u = 1; u <= durationMaxIndexPrev; u++)
            {
              probIntegrals[u + 1] = probIntegrals[u] + (policyProbabilities[l][t - 1][u] - policyProbabilities[l][t - 1][u - 1])
                * genderIntensity[l][j](policy.age + Time + IndexToTime(t - 0.5),
                policy.initialDuration + IndexToTime(u - 0.5)) * stepSize;

              //todo: Should probably not have age in policy. Could consider splitting age and Time to allow for more general intesities.
            }

            // For j = l, we need to subtract the cumulative integral for each duration
            // For j \neq l, we need to add the whole integral from 0 to D(s) for each duration
            if (j == l)
            {
              for (var u = 1; u <= durationMaxIndexCur; u++)
                policyProbabilities[j][t][u] = policyProbabilities[j][t][u] - probIntegrals[u];
            }
            else
            {
              for (var u = 1; u <= durationMaxIndexCur; u++)
                policyProbabilities[j][t][u] = policyProbabilities[j][t][u] + probIntegrals.Last();
            }
          }

          // We add the previous probability p_ij(t_0,s,u,d+s-t_0) to get p_ij(t_0,s+h,u,d+s+h-t_0)= p_ij(t_0,s,u,d+s-t_0)+d/ds p_ij(t_0,s,u,d+s-t_0)*h
          for (var u = 1; u <= durationMaxIndexCur; u++)
            policyProbabilities[j][t][u] = policyProbabilities[j][t][u] + policyProbabilities[j][t - 1][u - 1];
        }
      }
    }

  }
}