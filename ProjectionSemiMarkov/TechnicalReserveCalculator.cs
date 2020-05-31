using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ProjectionSemiMarkov
{
  class TechnicalReserveCalculator : Calculator
  {
    /// <summary>
    /// States with reserve
    /// </summary>
    IEnumerable<State> statesWithReserve => stateSpace.Where(x => x != State.Dead);

    /// <summary>
    /// Technical interest
    /// </summary>
    readonly double technicalInterest;

    /// <summary>
    /// A dictionary indexed on <see cref="Policy.policyId"/> and contains technical reserves.
    /// </summary>
    Dictionary<string, Dictionary<(PaymentStream, Sign), Dictionary<State, double[]>>> technicalReserve;

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
      technicalReserve = new Dictionary<string, Dictionary<(PaymentStream, Sign), Dictionary<State, double[]>>>();

      foreach (var (policyId, v) in policies)
      {
        var numberOfTimePoints = GetNumberOfTimePoints(v, 0.0);
        var comStateTechnicalReserve = new Dictionary<(PaymentStream, Sign), Dictionary<State, double[]>>();

        var combs = new List<(PaymentStream, Sign)>
          {
            (PaymentStream.Original, Sign.Positive),
            (PaymentStream.Original, Sign.Negative),
            (PaymentStream.Bonus, Sign.Positive),
          };


        foreach (var comb in combs)
        {
          var stateTechnicalReserve = new Dictionary<State, double[]>();
          stateSpace.ToList().ForEach(state => stateTechnicalReserve.Add(state, new double[numberOfTimePoints]));
          comStateTechnicalReserve.Add(comb, stateTechnicalReserve);
        }

        technicalReserve.Add(policyId, comStateTechnicalReserve);
      }
    }

    public Dictionary<string, Dictionary<(PaymentStream, Sign), Dictionary<State, double[]>>> Calculate()
    {
      AllocateMemoryAndInitialize();

      Parallel.ForEach(policies, policy => CalculateTechnicalReservePerPolicy(policy.Value));

      return technicalReserve;
    }

    private void CalculateTechnicalReservePerPolicy(Policy policy)
    {
      var stateTechnicalReserves = technicalReserve[policy.policyId];

      foreach (var (signedPayment, stateTechnicalReserve) in stateTechnicalReserves)
      {
        var contBenefits = policy.Payments[signedPayment].TechnicalContinuousPayment;
        var jumpBenefits = policy.Payments[signedPayment].TechnicalJumpPayment;
        var genderIntensity = intensities[policy.gender];

        CalculateTechnicalReservePerSignedPayment(
          policy.age, contBenefits, jumpBenefits, stateTechnicalReserve, genderIntensity);
      }
    }

    public void CalculateTechnicalReservePerSignedPayment(
      double policyAge,
      Dictionary<State, Func<double, double>> contBenefits,
      Dictionary<State, Dictionary<State, Func<double, double>>> jumpBenefits,
      Dictionary<State, double []> stateTechnicalReserve,
      Dictionary<State, Dictionary<State, Func<double, double, double>>> genderIntensity)
    {
      // We subtract with two to get to second last index.
      for (var i = stateTechnicalReserve.First().Value.Length - 2; i >= 0; i--)
      {
        // Set t_n'  = (t_n - t_(n-1)))/2.
        // We are using Eulers method and assuming right and left derivative is equal, thus
        // V_j(t_(n-1))
        // = V_j(t_n) - dV_j(t_n) * (t_n - t_(n-1))
        // = V(t_n) - ( (r* + mu_(j,dot)(t_n') V_j(t_n) - b_j(t_n')
        //            - sum_(k != j) mu_jk(t_n') (b_jk(t_n') + V_k(t_n)) ) * (t_n - t_(n-1))

        foreach (var soJournState in statesWithReserve)
        {
          var time = policyAge + IndexToTime(i + 0.5);

          // handling    -(r* + mu_(j,dot)(t_n') V_j(t_n) + b_j(t_n')
          stateTechnicalReserve[soJournState][i] =
            (contBenefits.TryGetValue(soJournState, out var value) ? value(time) : 0.0)
            - (technicalInterest + genderIntensity[soJournState][soJournState](time, 0.0)) * stateTechnicalReserve[soJournState][i + 1];

          // handling    sum_(k != j) mu_jk(t_n') (b_jk(t_n') + V_k(t_n))
          foreach (var toState in stateSpace.Where(x => x != soJournState))
          {
            if (HelperFunctions.TransitionExists(genderIntensity, soJournState, toState))
            {
              if (HelperFunctions.TransitionExists(jumpBenefits, soJournState, toState))
                stateTechnicalReserve[soJournState][i] +=
                  (stateTechnicalReserve[toState][i] + jumpBenefits[soJournState][toState](time))
                                                         * genderIntensity[soJournState][toState](time, 0);
              else
                stateTechnicalReserve[soJournState][i] +=
                  stateTechnicalReserve[toState][i] * genderIntensity[soJournState][toState](time, 0);
            }
          }
          // handling   V(t_n) and multiplication with stepSize
          stateTechnicalReserve[soJournState][i] =
            stateTechnicalReserve[soJournState][i] * stepSize + stateTechnicalReserve[soJournState][i + 1];
        }
      }

    }
  }
}
