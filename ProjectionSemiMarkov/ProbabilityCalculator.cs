using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using static ProjectionSemiMarkov.HelperFunctions;

namespace ProjectionSemiMarkov
{
  public class ProbabilityCalculator : Calculator
  {
    /// <summary>
    /// A dictionary indexed on <see cref="Policy.policyId"/> and contains the semi-markov Probabilities.
    /// </summary>
    /// <remarks>
    /// Given a <c>policyId</c>, a <c>state</c>, a <c>timePoint</c>, a <c>duration</c>, then
    /// Probabilities[policyId][state][timePoint][duration] is the probability
    /// p_{z0,state}(initialTime, (timePoint - 1)*stepSize, initialDuration, (duration - 1)*stepSize)
    /// The z0, initialTime and initialDuration is found on the policy, see mapping <see cref="policies"/>.
    /// </remarks>
    public Dictionary<string, Dictionary<State, double[][]>> Probabilities { get; private set; }

    /// <summary>
    /// Constructing ProbabilityCalculator.
    /// </summary>
    public ProbabilityCalculator(
      Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>> intensities,
      Dictionary<string, Policy> policies)
    {
      // Deducing the state space from the possible transitions in intensity dictionary
      var allPossibleTransitions = intensities[Gender.Female].Union(intensities[Gender.Male]).ToList();
      stateSpace = allPossibleTransitions.SelectMany(x => x.Value.Keys)
      .Union(allPossibleTransitions.Select(y => y.Key)).Distinct();

      // Reindexing the intensities to step size
      this.intensities = intensities;
      this.policies = policies;
    }

    /// <summary>
    /// Allocating memory for matrices inside <see cref="Probabilities"/>.
    /// </summary>
    private void AllocateMemoryAndInitialize()
    {
      Probabilities = new Dictionary<string, Dictionary<State, double[][]>>();

      foreach (var (policyId, v) in policies)
      {
        var numberOfTimePoints = GetNumberOfTimePoints(v);
        var stateProbabilities = new Dictionary<State, double[][]>();

        foreach (var state in stateSpace)
        {
          var arrayOfArray = new double[numberOfTimePoints][];

          for (var timeIndex = 0; timeIndex < numberOfTimePoints; timeIndex++)
            arrayOfArray[timeIndex] = new double[DurationSupportIndex(v.initialDuration, timeIndex) + 1];

          // Default values of array elements are zero, so we only set probability for last duration to one.
          arrayOfArray[0][arrayOfArray[0].Length - 1] = state == v.initialState ? 1 : 0;

          stateProbabilities.Add(state, arrayOfArray);
        }
        Probabilities.Add(policyId, stateProbabilities);
      }
    }

    public override Dictionary<string, Dictionary<State, double[][]>> Calculate()
    {
      AllocateMemoryAndInitialize();

      Parallel.ForEach(policies, policy => ProbabilityCalculatePerPolicy(policy.Value));

      return(Probabilities);
    }

    public void ProbabilityCalculatePerPolicy(Policy policy)
    {
      var policyProbabilities = Probabilities[policy.policyId];
      var numberOfTimePoints = policyProbabilities.First().Value.Length;
      var genderIntensity = intensities[policy.gender];

      // Loop over each time point
      for (var t = 1; t < numberOfTimePoints; t++)
      {
        var durationMaxIndexCur = DurationSupportIndex(policy.initialDuration, t);
        var durationMaxIndexPrev = DurationSupportIndex(policy.initialDuration, t - 1);

        var probIntegralPieces = new double[durationMaxIndexCur + 1];

        // Loop over j in p_{z0,j}(...)
        foreach (var j in stateSpace)
        {
          // Loop over l in Kolmogorov forward integro-differential equations (Prob. mass going in)
          foreach (var l in genderIntensity.Keys)
          {
            Func<double, double, double> intensity;
            if (!genderIntensity[l].TryGetValue(j, out intensity))
              continue;

            probIntegralPieces[1] = 0;
            // Riemann sum over duration for integrals
            for (var u = 1; u <= durationMaxIndexPrev; u++)
            {
              probIntegralPieces[u + 1] = probIntegralPieces[u] + (policyProbabilities[l][t - 1][u] - policyProbabilities[l][t - 1][u - 1])
                * genderIntensity[l][j](policy.age + policy.initialTime + IndexToTime(t - 0.5),
                policy.initialDuration + IndexToTime(u - 0.5)) * stepSize;

              //todo: Should probably not have age in policy. Could consider splitting age and time to allow for more general intesities.
            }

            // For j = l, we need to subtract the cumulative integral for each duration
            // For j \neq l, we need to add the whole integral from the
            if (j == l)
            {
              for (var u = 1; u <= durationMaxIndexCur; u++)
                policyProbabilities[j][t][u] = policyProbabilities[j][t][u] - probIntegralPieces[u];
            }
            else
            {
              for (var u = 1; u <= durationMaxIndexCur; u++)
                policyProbabilities[j][t][u] = policyProbabilities[j][t][u] + probIntegralPieces.Last();
            }
          }

          // We add the start probability
          for (var u = 1; u <= durationMaxIndexCur; u++)
            policyProbabilities[j][t][u] = policyProbabilities[j][t][u] + policyProbabilities[j][t - 1][u - 1];
        }
      }
    }

  }
}