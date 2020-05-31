using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectionSemiMarkov
{
  class TechnicalReserveCalculator : Calculator
  {
    /// <summary>
    /// Technical interest
    /// </summary>
    double technicalInterest;

    /// <summary>
    /// A dictionary indexed on <see cref="Policy.policyId"/> and contains technical reserves.
    /// </summary>
    Dictionary<string, Dictionary<State, double[]>> technicalReserve;

    /// <summary>
    /// Constructing TechnicalReserveCalculator.
    /// </summary>
    public TechnicalReserveCalculator(
      Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>> intensities,
      double technicalInterest,
      Dictionary<string, Policy> policies)
    {
      // Deducing the state space from the possible transitions in intensity dictionary
      var allPossibleTransitions = intensities[Gender.Female].Union(intensities[Gender.Male]).ToList();
      this.stateSpace = allPossibleTransitions.SelectMany(x => x.Value.Keys)
      .Union(allPossibleTransitions.Select(y => y.Key)).Distinct();

      // Reindexing the intensities to step size
      this.intensities = intensities;
      this.technicalInterest = technicalInterest;
      this.policies = policies;
    }

    /// <summary>
    /// Allocating memory for arrays inside <see cref="technicalReserve"/>.
    /// </summary>
    private void AllocateMemoryAndInitialize()
    {
      technicalReserve = new Dictionary<string, Dictionary<State, double[]>>();

      foreach (var (policyId, v) in policies)
      {
        var numberOfTimePoints = GetNumberOfTimePoints(v);
        var stateTechnicalReserve = new Dictionary<State, double[]>();

        foreach (var state in stateSpace)
          stateTechnicalReserve.Add(state, new double[numberOfTimePoints]);

        technicalReserve.Add(policyId, stateTechnicalReserve);
      }
    }

    public override Dictionary<string, Dictionary<State, double[][]>> Calculate()
    {
      AllocateMemoryAndInitialize();

      Parallel.ForEach(policies, policy => CalculateTechnicalReservePerPolicy(policy.Value));

      var dummy = new Dictionary<string, Dictionary<State, double[][]>>();
      return (dummy);
    }

    private void CalculateTechnicalReservePerPolicy(Policy policy)
    {
      var stateTechnicalReserves = technicalReserve[policy.policyId];
      var contBenefits = policy.originalBenefits.technicalContinousPayment;
      var jumpBenefits = policy.originalBenefits.technicalJumpPayment;
      var genderIntensity = intensities[policy.gender];

      for (var i = stateTechnicalReserves.First().Value.Length - 2; i >= 0; i--)
      {
        // We do not handle payments in dead, so the reserve is zero.
        foreach (var soJournState in stateSpace.Where(x => x != State.Dead))
        {
          var time = policy.age + policy.expiryAge + IndexToTime(i + 0.5);

          stateTechnicalReserves[soJournState][i] = (contBenefits.TryGetValue(soJournState, out var value) ? value(time) : 0.0)
            - (technicalInterest + genderIntensity[soJournState][soJournState](time, 0.0)) * stateTechnicalReserves[soJournState][i + 1];

          foreach (var toState in stateSpace.Where(x => x != soJournState))
          {
            if (HelperFunctions.TransitionExists(genderIntensity, soJournState, toState))
            {
              if (HelperFunctions.TransitionExists(jumpBenefits, soJournState, toState))
              {
                stateTechnicalReserves[soJournState][i] = stateTechnicalReserves[soJournState][i]
                  + (stateTechnicalReserves[toState][i] + jumpBenefits[soJournState][toState](time))
                  * genderIntensity[soJournState][toState](time, 0);
              }
              else
              {
                stateTechnicalReserves[soJournState][i] = stateTechnicalReserves[soJournState][i]
                  + stateTechnicalReserves[toState][i] * genderIntensity[soJournState][toState](time, 0);
              }
            }
          }

          stateTechnicalReserves[soJournState][i] =
            stateTechnicalReserves[soJournState][i] * stepSize + stateTechnicalReserves[soJournState][i + 1];
        }
      }
    }
  }
}
