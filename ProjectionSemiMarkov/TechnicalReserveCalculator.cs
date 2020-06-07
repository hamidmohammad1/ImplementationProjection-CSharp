using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using static ProjectionSemiMarkov.HelperFunctions;

namespace ProjectionSemiMarkov
{
  public class TechnicalReserveCalculator : Calculator
  {
    /// <summary>
    /// States with reserve
    /// </summary>
    IEnumerable<State> technicalStatesWithReserve => TechnicalStateSpace.Where(x => x != State.Dead);

    /// <summary>
    /// A dictionary indexed on <see cref="Policy.policyId"/> and contains technical reserves.
    /// </summary>
    public Dictionary<string, Dictionary<(PaymentStream, Sign), Dictionary<State, double[]>>> TechnicalReserve
    { get; private set; }

    /// <summary>
    /// Allocating memory for arrays inside <see cref="TechnicalReserve"/>.
    /// </summary>
    private void AllocateMemoryAndInitialize()
    {
      TechnicalReserve = new Dictionary<string, Dictionary<(PaymentStream, Sign), Dictionary<State, double[]>>>();

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
          MarketStateSpace.ToList().ForEach(state => stateTechnicalReserve.Add(state, new double[numberOfTimePoints]));
          comStateTechnicalReserve.Add(comb, stateTechnicalReserve);
        }

        TechnicalReserve.Add(policyId, comStateTechnicalReserve);
      }
    }

    public Dictionary<string, Dictionary<(PaymentStream, Sign), Dictionary<State, double[]>>> Calculate()
    {
      AllocateMemoryAndInitialize();

      Parallel.ForEach(policies, policy => CalculateTechnicalReservePerPolicy(policy.Value));

      return TechnicalReserve;
    }

    private void CalculateTechnicalReservePerPolicy(Policy policy)
    {
      var stateTechnicalReserves = TechnicalReserve[policy.policyId];

      foreach (var (signedPayment, stateTechnicalReserve) in stateTechnicalReserves)
      {
        var contBenefits = policy.Payments[signedPayment].TechnicalContinuousPayment;
        var jumpBenefits = policy.Payments[signedPayment].TechnicalJumpPayment;
        var genderIntensity = technicalIntensities[policy.gender];

        CalculateTechnicalReservePerSignedPayment(
          policy.age, contBenefits, jumpBenefits, stateTechnicalReserve, genderIntensity);
      }
    }

    private void CalculateTechnicalReservePerSignedPayment(
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

        foreach (var soJournState in technicalStatesWithReserve)
        {
          var time = policyAge + IndexToTime(i + 0.5);

          // handling    -(r* + mu_(j,dot)(t_n') V_j(t_n) + b_j(t_n')
          stateTechnicalReserve[soJournState][i] =
            (contBenefits.TryGetValue(soJournState, out var value) ? value(time) : 0.0)
            - (technicalInterest + genderIntensity[soJournState][soJournState](time, 0.0)) * stateTechnicalReserve[soJournState][i + 1];

          // handling    sum_(k != j) mu_jk(t_n') (b_jk(t_n') + V_k(t_n))
          foreach (var toState in MarketStateSpace.Where(x => x != soJournState))
          {
            if (TransitionExists(genderIntensity, soJournState, toState))
            {
              if (TransitionExists(jumpBenefits, soJournState, toState))
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

    /// <summary>
    /// Calculating \rho = (V_{Active}^{\circ,*,+} + V_{Active}^{\circ,*,-})/ V_{Active}^{\circ,*,+} for each time point
    /// </summary>
    public Dictionary<string, double[]> CalculateFreePolicyFactor()
    {
      return TechnicalReserve
        .ToDictionary(policy => policy.Key,
          policy => policy.Value[(PaymentStream.Original, Sign.Positive)][State.Active]
            .Zip(policy.Value[(PaymentStream.Original, Sign.Negative)][State.Active], (x, y) => x + y)
            .Zip(policy.Value[(PaymentStream.Original, Sign.Positive)][State.Active], (x, y) => y == 0 ? 1.0 : x / y)
            .ToArray());
    }

    /// <summary>
    /// Calculate and return the technical reserves.
    /// </summary>
    public (Dictionary<string, Dictionary<State, List<double>>> OriginalTechnicalReserves,
      Dictionary<string, Dictionary<State, List<double>>> OriginalTechnicalPositiveReserves,
      Dictionary<string, Dictionary<State, double[]>> BonusTechnicalReserves)
      CalculateTechnicalReserve()
    {
      var standardStates = GiveCollectionOfStates(StateCollection.Standard);

      var originalTechReserves = TechnicalReserve
        .ToDictionary(x => x.Key, x => standardStates.ToDictionary(
          y => y,
          y => x.Value[(PaymentStream.Original, Sign.Positive)][y]
            .Zip(x.Value[(PaymentStream.Original, Sign.Negative)][y], (a, b) => (a + b))
            .ToList()));

      var originalTechPositiveReserves = TechnicalReserve
        .ToDictionary(x => x.Key, x => standardStates.ToDictionary(
          y => y, y => x.Value[(PaymentStream.Original, Sign.Positive)][y].ToList()));

      var bonusTechnicalReserves = TechnicalReserve
        .ToDictionary(x => x.Key, x => x.Value[(PaymentStream.Original, Sign.Positive)]);

      return (originalTechReserves, originalTechPositiveReserves, bonusTechnicalReserves);
    }
  }
}
